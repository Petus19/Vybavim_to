"""Metriky kvality vyhľadávania. Vstup je vždy zoradený zoznam kľúčov (najlepší prvý) a množina správnych."""

from collections.abc import Collection, Sequence


def recall_at_k(ranked: Sequence[str], relevant: Collection[str], k: int) -> float:
    """Aký podiel správnych inzerátov je medzi prvými k výsledkami.

    Príklad: správne sú {A, B}, výsledky [A, X, Y, Z, W] → recall@5 = 1/2.
    """
    if not relevant:
        raise ValueError("Recall sa dá spočítať len pre dopyt so správnou odpoveďou.")
    found = sum(1 for key in ranked[:k] if key in relevant)
    return found / len(relevant)


def reciprocal_rank(ranked: Sequence[str], relevant: Collection[str], k: int = 10) -> float:
    """1 / poradie PRVÉHO správneho výsledku (1., 2., 3. … → 1, 1/2, 1/3); 0, ak nie je medzi prvými k.

    Priemer cez dopyty je MRR (Mean Reciprocal Rank) – hovorí, ako vysoko je prvý dobrý výsledok.
    """
    for position, key in enumerate(ranked[:k], start=1):
        if key in relevant:
            return 1 / position
    return 0.0


def mean(values: Sequence[float]) -> float:
    return sum(values) / len(values) if values else 0.0
