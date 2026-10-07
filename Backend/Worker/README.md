# Worker

Spracovanie úloh z fronty (Redis): e-maily, notifikácie, obrázky, PDF faktúry, Stripe webhooky a cron úlohy.

Používa rovnaké moduly a `Common/` ako `Api`, ale beží ako samostatný proces, ktorý sa škáluje nezávisle.
