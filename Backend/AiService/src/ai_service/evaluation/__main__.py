"""`uv run python -m ai_service.evaluation [--model NÁZOV ...]` – vypíše výsledky ako Markdown."""

import argparse
from collections.abc import Sequence

from ai_service.config import get_settings
from ai_service.embeddings import SentenceTransformerEmbedder
from ai_service.evaluation.dataset import EvalListing, load_dataset
from ai_service.evaluation.retrievers import (
    Bm25Retriever,
    HybridRetriever,
    KeywordRetriever,
    Retriever,
    SemanticRetriever,
)
from ai_service.evaluation.run import K, evaluate, results_table, threshold_analysis, threshold_table


def semantic(model_name: str, listings: Sequence[EvalListing]) -> SemanticRetriever:
    embedder = SentenceTransformerEmbedder(model_name)
    if "e5" in model_name:  # modely E5 sú trénované s predponami a bez nich dávajú horšie výsledky
        return SemanticRetriever(embedder, listings, query_prefix="query: ", passage_prefix="passage: ")
    return SemanticRetriever(embedder, listings)


def main() -> None:
    parser = argparse.ArgumentParser(description="Evaluate keyword, BM25, semantic and hybrid search.")
    parser.add_argument(
        "--model",
        action="append",
        help="embedding model to evaluate (can be repeated); default = the service's model",
    )
    args = parser.parse_args()
    models = args.model or [get_settings().embedding_model]

    dataset = load_dataset()
    queries = dataset.answerable
    keyword = KeywordRetriever(dataset.listings)
    bm25 = Bm25Retriever(dataset.listings)
    semantics = [semantic(name, dataset.listings) for name in models]

    hybrids = [HybridRetriever(bm25, s) for s in semantics]
    retrievers: list[Retriever] = [keyword, bm25, *semantics, *hybrids]
    results = [evaluate(r, queries) for r in retrievers]

    print(f"{len(dataset.listings)} listings, {len(queries)} answerable queries", end="")
    print(f" + {len(dataset.queries) - len(queries)} with no correct answer\n")
    print(results_table(results))
    print(f"\nQueries with no correct listing in the top {K}:")
    for r in results:
        print(f"- {r.name}: {', '.join(r.misses) or '–'}")
    for retriever in semantics:
        print(f"\nMinimum score for {retriever.name}:\n")
        print(threshold_table(threshold_analysis(retriever, dataset)))


if __name__ == "__main__":
    main()
