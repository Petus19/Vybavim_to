"""Spustí všetky metódy nad testovacou sadou a vyrobí tabuľky výsledkov (Markdown)."""

from collections.abc import Sequence
from dataclasses import dataclass

from ai_service.evaluation.dataset import QUERY_KINDS, Dataset, EvalQuery
from ai_service.evaluation.metrics import mean, recall_at_k, reciprocal_rank
from ai_service.evaluation.retrievers import Retriever, SemanticRetriever

K = 5  # koľko výsledkov používateľ reálne uvidí bez posúvania


@dataclass(frozen=True)
class MethodResult:
    name: str
    recall_at_k: float
    mrr: float
    recall_by_kind: dict[str, float]
    misses: tuple[str, ...]  # dopyty, kde nie je ani jeden správny inzerát v top K – na ručnú kontrolu


def evaluate(retriever: Retriever, queries: Sequence[EvalQuery]) -> MethodResult:
    recalls: dict[str, float] = {}
    reciprocal_ranks = []
    misses = []
    for query in queries:
        ranked = [key for key, _ in retriever.rank(query.text)]
        recalls[query.id] = recall_at_k(ranked, query.relevant, K)
        reciprocal_ranks.append(reciprocal_rank(ranked, query.relevant))
        if recalls[query.id] == 0:
            misses.append(query.id)

    by_kind = {
        kind: mean([recalls[q.id] for q in queries if q.kind == kind])
        for kind in QUERY_KINDS
        if any(q.kind == kind for q in queries)
    }
    return MethodResult(
        retriever.name, mean(list(recalls.values())), mean(reciprocal_ranks), by_kind, tuple(misses)
    )


@dataclass(frozen=True)
class ThresholdRow:
    threshold: float
    answered: float  # podiel dopytov s odpoveďou, kde najlepšie skóre prešlo hranicou (chceme vysoko)
    rejected: float  # podiel dopytov BEZ odpovede, kde sme správne povedali „nič vhodné“ (chceme vysoko)


def threshold_analysis(retriever: SemanticRetriever, dataset: Dataset, steps: int = 6) -> list[ThresholdRow]:
    """Od akého skóre ukázať výsledok? Sémantické hľadanie inak VŽDY niečo vráti.

    Každý model má inú škálu (MiniLM ~0,2–0,8, E5 ~0,8–0,9), preto hranice berieme rovnomerne
    medzi najnižším a najvyšším najlepším skóre, ktoré model na tejto sade dal.
    """
    top_scores = {q.id: retriever.rank(q.text)[0][1] for q in dataset.queries}
    answerable = [top_scores[q.id] for q in dataset.queries if q.relevant]
    unanswerable = [top_scores[q.id] for q in dataset.queries if not q.relevant]
    low, high = min(top_scores.values()), max(top_scores.values())
    thresholds = [low + (high - low) * i / steps for i in range(steps)]
    return [
        ThresholdRow(
            t,
            mean([1.0 if s >= t else 0.0 for s in answerable]),
            mean([1.0 if s < t else 0.0 for s in unanswerable]),
        )
        for t in thresholds
    ]


def results_table(results: Sequence[MethodResult]) -> str:
    kinds = list(results[0].recall_by_kind)
    lines = [
        f"| Method | recall@{K} | MRR | " + " | ".join(kinds) + " |",
        "|---|---|---|" + "---|" * len(kinds),
    ]
    for r in results:
        per_kind = " | ".join(f"{r.recall_by_kind[kind]:.2f}" for kind in kinds)
        lines.append(f"| {r.name} | **{r.recall_at_k:.2f}** | {r.mrr:.2f} | {per_kind} |")
    return "\n".join(lines)


def threshold_table(rows: Sequence[ThresholdRow]) -> str:
    lines = [
        "| Min. score | answerable queries still answered | no-answer queries rejected |",
        "|---|---|---|",
    ]
    lines += [f"| {row.threshold:.3f} | {row.answered:.0%} | {row.rejected:.0%} |" for row in rows]
    return "\n".join(lines)
