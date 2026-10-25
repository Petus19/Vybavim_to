# AiService *(planned – phase 2–3)*

A separate **Python (FastAPI)** service for AI and machine-learning features:

- semantic search ("logo for a café" finds graphic designers) using embeddings stored in PostgreSQL with **pgvector**
- matching specialists to listings and recommendations
- an LLM assistant that helps users write a listing
- content moderation (spam, scams) and fraud detection

It will not access the database directly: it subscribes to domain events (e.g. `ListingCreated`) and calls the public module APIs, so it has the same permissions as a regular user. The PostgreSQL image already includes pgvector.
