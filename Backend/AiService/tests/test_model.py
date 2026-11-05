"""Skutočný embedding model na slovenských vetách. Stiahne ~470 MB, preto sa bežne nespúšťa.

Spustenie: uv run pytest -m model
"""

import numpy as np
import pytest

from ai_service.config import Settings

pytestmark = pytest.mark.model


def test_model_puts_similar_meaning_closer_than_different_topic() -> None:
    from ai_service.embeddings import SentenceTransformerEmbedder

    embedder = SentenceTransformerEmbedder(Settings().embedding_model)
    logo, identity, network = embedder.embed(
        ["Logo pre kaviareň", "Vizuálna identita pre bistro", "Oprava počítačovej siete v kancelárii"]
    )

    assert embedder.dimension == 384
    assert np.linalg.norm(logo) == pytest.approx(1.0, abs=1e-4)  # normalizovaný vektor
    assert float(logo @ identity) > float(logo @ network) + 0.1
