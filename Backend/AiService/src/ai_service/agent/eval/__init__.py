"""Vyhodnotenie agenta: scenáre s očakávaným výsledkom, spustené proti bežiacemu lokálnemu systému.

Spustenie: `uv run python -m ai_service.agent.eval --provider local|api` (pozri __main__.py).
Úspech scenára sa neposudzuje „od oka“ – overuje sa výsledný stav cez verejné API (vznikol inzerát
v správnej kategórii? bola ponuka prijatá? nezapísalo sa nič?), zavolané nástroje a odpoveď.
"""
