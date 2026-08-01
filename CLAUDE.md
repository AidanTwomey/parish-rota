# Parish Rota

A multi-tenant WhatsApp-based rota service for Catholic parish Readers. First tenant: Holy Innocents, Orpington.

## Read first

- `CONTEXT.md` — the ubiquitous language. Use these terms exactly (Reader, Rota, Rota Period, Slot, Position, Drop, Cover Request, Swap, Home Mass, Availability Prompt, Volunteer Call, One-off Mass, Coordinator). Challenge and update it when the model shifts.
- `docs/adr/` — architectural decisions. Notably: WhatsApp-only via Meta Cloud API (0001), multi-tenant from the start (0002), no UI anywhere (0003), liturgical-calendar rota periods (0004), single shared WhatsApp number (0005), Azure Functions + Cosmos free tier (0006).

## Stack

- C# / .NET, Azure Functions (Consumption plan)
- Cosmos DB free tier, partition key `parishId`
- Infrastructure as code with Bicep
- Intent parsing of inbound WhatsApp messages via Claude Haiku 4.5 (structured outputs); when the parse is ambiguous, the bot asks a clarifying question or escalates to the Coordinator — it never guesses
- Reader roster is a manually maintained CSV for the MVP

## Conventions

- Domain logic (liturgical calendar computation, rota generation, cover-request workflow) is developed **test-first** using the `/tdd` skill.
- Adapter code (WhatsApp webhook, Cosmos persistence, outbound messaging) is tested pragmatically — integration-style tests, no strict TDD ceremony.
- Keep running cost near zero: no always-on compute, minimise business-initiated WhatsApp template messages (they are the only per-message cost).
- `dotnet test` runs the test suite.
