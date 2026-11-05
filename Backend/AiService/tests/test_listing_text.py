"""Text inzerátu pre model a jeho odtlačok."""

from tests.fakes import document


def test_text_contains_title_description_and_skills() -> None:
    doc = document(1, "Logo pre kaviareň", "Hľadáme grafika.")

    text = doc.text()

    assert text.startswith("Logo pre kaviareň.")
    assert "Hľadáme grafika." in text


def test_content_hash_changes_with_text_or_model() -> None:
    original = document(1, "Logo", "Popis")
    same = document(1, "Logo", "Popis")
    edited = document(1, "Logo", "Iný popis")

    assert original.content_hash("model-a") == same.content_hash("model-a")
    assert original.content_hash("model-a") != edited.content_hash("model-a")
    assert original.content_hash("model-a") != original.content_hash("model-b")
