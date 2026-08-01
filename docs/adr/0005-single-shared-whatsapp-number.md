# Single shared WhatsApp number across all parishes

All tenants share one Meta Business account, one WhatsApp number (displayed generically as "Parish Rota"), and one webhook. A sender's phone number identifies the Reader, and their Parish is resolved from that — no per-parish numbers. This avoids repeating Meta's business verification per tenant. The schema still stores a `phoneNumberId` per Parish so a parish can be moved to its own branded number later as configuration, not surgery. The rare person belonging to two parishes is disambiguated in conversation.
