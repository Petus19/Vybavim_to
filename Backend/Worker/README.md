# Worker

Spracovanie úloh z fronty: e-maily, notifikácie, obrázky, PDF faktúry, Stripe webhooky, cron úlohy a odosielanie udalostí z outboxu.

Referencuje rovnaké moduly a `Common/` ako `Api`, ale beží ako samostatný proces, ktorý sa škáluje nezávisle.
