# Worker *(planned)*

A separate process for background jobs: emails, notifications, image processing, PDF invoices, Stripe webhooks, scheduled jobs and publishing events from the outbox.

It will reference the same modules and `Common/` as `Api`, but run as its own process that scales independently. Currently only an empty host – the job queue (Hangfire or Wolverine) is still to be chosen.
