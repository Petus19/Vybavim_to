"""Odhad ceny z počtu tokenov a cenníkových cien (USD za milión tokenov).

Je to ODHAD pre porovnanie modelov vo vyhodnotení, nie faktúra – presnú spotrebu ukazuje konzola
poskytovateľa. Lokálny model (Ollama) nestojí nič.
"""

from dataclasses import dataclass

from ai_service.agent.llm import Usage


@dataclass(frozen=True)
class Price:
    input: float  # nové (necachované) vstupné tokeny
    output: float
    cache_read: float  # vstup prečítaný z cache
    cache_write_factor: float = 1.25  # zápis do cache stojí 1,25-násobok vstupu

    @property
    def cache_write(self) -> float:
        return self.input * self.cache_write_factor


# Cenník Claude API (november 2026). Pri zmene cien alebo novom modeli stačí upraviť túto tabuľku.
PRICES: dict[str, Price] = {
    "claude-opus-5-5": Price(input=4.00, output=20.00, cache_read=0.20),
    "claude-sonnet-5-5": Price(input=2.00, output=10.00, cache_read=0.10),
    "claude-haiku-5-5": Price(input=0.10, output=0.50, cache_read=0.01),
}


def estimate_cost(provider_name: str, usage: Usage) -> float | None:
    """Cena v USD; 0 pre lokálny model, None pre model, ktorého cenu nepoznáme."""
    if provider_name.startswith("ollama/"):
        return 0.0
    price = PRICES.get(provider_name.removeprefix("anthropic/"))
    if price is None:
        return None
    return (
        usage.input_tokens * price.input
        + usage.cache_write_tokens * price.cache_write
        + usage.cache_read_tokens * price.cache_read
        + usage.output_tokens * price.output
    ) / 1_000_000
