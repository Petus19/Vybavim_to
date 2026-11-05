"""Embedding model: text → vektor čísel, v ktorom sú texty s podobným významom blízko seba."""

from typing import Protocol

import numpy as np
from numpy.typing import NDArray


class Embedder(Protocol):
    """Čokoľvek, čo vie z textov vyrobiť vektory. Protocol = rozhranie (interface v C#).

    Testy podstrčia rýchly falošný embedder, služba skutočný model.
    """

    model_name: str
    dimension: int

    def embed(self, texts: list[str]) -> NDArray[np.float32]:
        """Vráti maticu tvaru (počet textov, dimension); každý riadok má dĺžku 1."""
        ...


class SentenceTransformerEmbedder:
    """Predtrénovaný viacjazyčný model zo sentence-transformers. Beží lokálne na procesore."""

    def __init__(self, model_name: str) -> None:
        # Import až tu: knižnica je veľká (PyTorch) a testy s falošným embedderom ju tak nenačítajú
        from sentence_transformers import SentenceTransformer

        self.model_name = model_name
        self._model = SentenceTransformer(model_name, device="cpu")
        self.dimension = int(self._model.get_embedding_dimension() or 0)

    def embed(self, texts: list[str]) -> NDArray[np.float32]:
        # normalize_embeddings=True: každý vektor má dĺžku 1, takže kosínusová podobnosť
        # je obyčajný skalárny súčin – rýchle a rovnaké, ako to počíta pgvector
        return self._model.encode(texts, normalize_embeddings=True, convert_to_numpy=True, batch_size=32)
