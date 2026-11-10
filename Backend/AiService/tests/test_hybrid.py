"""Filter hybridného hľadania: čo je relevantné a čo už nie."""

from ai_service.hybrid import fuse
from ai_service.text_search import KeywordHit


def test_keeps_results_above_min_score_or_with_all_words() -> None:
    semantic = [("logo", 0.62), ("etikety", 0.45), ("wifi", 0.12), ("seo", 0.20)]
    keyword = [KeywordHit("seo", 4.0, full_match=True), KeywordHit("wifi", 1.0, full_match=False)]

    results = fuse(semantic, keyword, min_score=0.41)

    assert {r.key for r in results} == {"logo", "etikety", "seo"}  # wifi: nízke skóre aj čiastočná zhoda
    seo = next(r for r in results if r.key == "seo")
    assert seo.keyword_match
    assert seo.semantic_score == 0.20


def test_returns_nothing_when_nothing_is_relevant() -> None:
    semantic = [("logo", 0.30), ("wifi", 0.22)]

    assert fuse(semantic, [], min_score=0.41) == []


def test_keyword_only_result_has_no_semantic_score() -> None:
    results = fuse([], [KeywordHit("seo", 4.0, full_match=True)], min_score=0.41)

    assert [(r.key, r.semantic_score) for r in results] == [("seo", None)]


def test_order_follows_rank_fusion() -> None:
    semantic = [("a", 0.9), ("b", 0.8)]
    keyword = [KeywordHit("b", 5.0, full_match=True), KeywordHit("a", 1.0, full_match=True)]

    results = fuse(semantic, keyword, min_score=0.0)

    assert [r.key for r in results] == ["a", "b"]  # rovnaký súčet bodov → stabilné poradie


def test_partial_word_match_needs_at_least_weak_meaning() -> None:
    # „nefunguje mi wifi“: slovo „wifi“ sedí, význam je slabší (0.27) → ostane
    # „oprava práčky“: slovo „oprava“ sedí, ale význam nesúvisí (0.07) → nie
    keyword = [KeywordHit("wifi", 3.0, full_match=False), KeywordHit("web", 2.0, full_match=False)]

    results = fuse([("wifi", 0.27), ("web", 0.07)], keyword, min_score=0.45)

    assert [r.key for r in results] == ["wifi"]


def test_strong_evidence_ranks_before_weak_partial_match() -> None:
    # „znak firmy“: BM25 dá na 1. miesto inzerát so slovom „firmy“ (slabý význam 0.28),
    # sémantika na 1. miesto logo (0.46) – logo musí byť prvé
    semantic = [("logo", 0.46), ("seo", 0.28)]
    keyword = [KeywordHit("seo", 2.0, full_match=False)]

    assert [r.key for r in fuse(semantic, keyword, min_score=0.45)] == ["logo", "seo"]
