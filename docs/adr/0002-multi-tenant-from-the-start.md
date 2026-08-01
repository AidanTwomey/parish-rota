# Multi-tenant from the start

The system serves one parish today (Holy Innocents, Orpington) but is modelled as multi-tenant from day one: every entity (Mass, Reader, Rota, Coordinator) belongs to a Parish. Retrofitting tenancy into a single-tenant data model is far more expensive than carrying a `parishId` from the beginning, and other parishes are a plausible future.
