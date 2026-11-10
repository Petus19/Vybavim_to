"""BM25, výplňové slová a spájanie poradí (RRF) – čistá logika bez modelu a databázy."""

import pytest

from ai_service.text_search import Bm25Index, normalize, reciprocal_rank_fusion, terms

DOCUMENTS = [
    ("cafe", "Logo pre kaviareň. Hľadáme grafika na logo kaviarne. Illustrator"),
    ("web", "Oprava chýb na webe vo WordPresse"),
    ("shop", "E-shop so sviečkami, chcem internetový obchod. WooCommerce"),
]


def test_normalize_removes_diacritics_and_case() -> None:
    assert normalize("Kaviareň Ťahanovce ÄÔ") == "kaviaren tahanovce ao"


def test_terms_drop_filler_words_and_shorten_to_prefix() -> None:
    # „potrebujem“, „na“, „pre“ nič nehovoria o tom, čo človek hľadá
    assert terms("Potrebujem logo pre kaviarne, na zajtra!") == ["log", "kavi", "zajtr"]


def test_bm25_finds_words_in_any_order_and_form() -> None:
    hits = Bm25Index(DOCUMENTS).rank("kaviarni logo")

    assert hits[0].key == "cafe"
    assert hits[0].full_match


def test_bm25_partial_match_is_not_full_match() -> None:
    hits = Bm25Index(DOCUMENTS).rank("oprava práčky")  # „oprava“ áno, „práčky“ nie

    assert [h.key for h in hits] == ["web"]
    assert not hits[0].full_match


def test_bm25_ignores_queries_made_of_filler_words() -> None:
    assert Bm25Index(DOCUMENTS).rank("niečo pre mňa na") == []


def test_bm25_on_empty_index_returns_nothing() -> None:
    assert Bm25Index[str]([]).rank("logo") == []


def test_reciprocal_rank_fusion_rewards_agreement() -> None:
    fused = reciprocal_rank_fusion([["a", "b", "c"], ["b", "c", "a"]])

    # b je 2. a 1. → najlepší súčet; a je 1. a 3.; c je 3. a 2.
    assert [key for key, _ in fused] == ["b", "a", "c"]
    assert fused[0][1] == pytest.approx(1 / 62 + 1 / 61)


def test_terms_join_hyphenated_words_and_skip_numbers() -> None:
    assert terms("Wi-Fi a e-shop do 300 eur") == ["wif", "eshop"]


def test_terms_unify_word_forms_ending_in_vowel() -> None:
    assert terms("logo loga logá") == ["log", "log", "log"]
    assert terms("SEO") == ["seo"]  # krátke slovo ostane celé
