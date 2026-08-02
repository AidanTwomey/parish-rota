# HTTP endpoints

Parish Rota exposes one HTTP endpoint. It exists so Meta can reach us; it is not
a public API and has no consumers of its own (ADR 0003 — WhatsApp is the only
interface). There is deliberately no OpenAPI document: the request contract
belongs to Meta, and the only rule worth stating — the HMAC on
`X-Hub-Signature-256` — is a header contract a spec describes poorly.

Update this file in the same change as the code it describes.

| Route | Methods | Auth level | Handler |
| --- | --- | --- | --- |
| `/api/whatsapp` | `GET`, `POST` | Anonymous | `WhatsAppWebhook.Run` |

Anonymous is not an oversight. Meta cannot present an Azure Functions key, so
authenticity comes from the verify token on `GET` and the signature on `POST`.

Base URLs:

- Local: `http://localhost:7071/api/whatsapp`
- Azure: `https://<function-app>.azurewebsites.net/api/whatsapp`

---

## `GET /api/whatsapp` — subscription verification

Meta calls this once, when you save the callback URL in **WhatsApp →
Configuration**. It also re-checks periodically.

**Query parameters**

| Name | Description |
| --- | --- |
| `hub.mode` | Always `subscribe`. Anything else is rejected. |
| `hub.verify_token` | Must equal the `WHATSAPP_VERIFY_TOKEN` app setting, compared ordinally. |
| `hub.challenge` | An opaque string to echo back. |

**Responses**

| Status | Body | When |
| --- | --- | --- |
| `200` | `hub.challenge` verbatim, `text/plain` | Mode is `subscribe` and the token matches |
| `403` | empty | Any other mode, a wrong token, or **no token configured** |

The body must be the raw challenge — no quotes, no JSON wrapper — because Meta
compares it byte for byte against what it sent.

An unset `WHATSAPP_VERIFY_TOKEN` fails closed. It never degrades into accepting
any token, which would let anyone bind their own Meta app to this endpoint.

---

## `POST /api/whatsapp` — inbound messages

Every inbound Reader message and every delivery receipt arrives here, provided
the app is subscribed to the `messages` webhook field.

**Headers**

| Name | Description |
| --- | --- |
| `X-Hub-Signature-256` | `sha256=` followed by 64 lowercase hex characters: HMAC-SHA256 of the **raw request body**, keyed on `WHATSAPP_APP_SECRET`. |

Verification rules (`MetaSignature.IsValid`), all failing closed:

- No `WHATSAPP_APP_SECRET` configured → reject
- Header absent, or not prefixed `sha256=` → reject
- Digest not exactly 64 characters, or not valid hex → reject
- Digest mismatch → reject

The comparison is `CryptographicOperations.FixedTimeEquals`, so it does not leak
how much of a guessed signature was correct. The body is read as bytes and never
round-tripped through a string — re-encoding would change the whitespace and no
valid signature would ever match.

**Responses**

| Status | Body | When |
| --- | --- | --- |
| `200` | empty | Signature verified |
| `403` | empty | Signature missing, malformed, or wrong |
| `403` | empty | `WHATSAPP_APP_SECRET` not configured |

Both rejections return `403` with no body — misconfiguration and attack are
indistinguishable from outside, on purpose. They are distinguishable in the
logs: a missing secret logs at **Error** naming the setting, a bad signature
logs at **Warning** with the payload size.

The handler acknowledges immediately and does no work on the request path. Meta
retries on slow responses and disables endpoints that keep timing out, so real
processing belongs off this path once intent parsing exists.

### Not yet implemented

The payload is logged and discarded. Nothing is parsed, stored or replied to.

---

## Settings

| Setting | Source | Purpose |
| --- | --- | --- |
| `WHATSAPP_VERIFY_TOKEN` | Invented by you; GitHub secret → Terraform | Proves the endpoint is yours during the handshake |
| `WHATSAPP_APP_SECRET` | Meta → App settings → Basic | Keys the inbound signature |

Both reach the Function App as app settings via `TF_VAR_*` (ADR 0007).

---

## Exercising it

Against a local `func start`, using the values in `local.settings.json`:

```bash
U=http://localhost:7071/api/whatsapp
S=local-dev-app-secret

# Verification handshake — expect 200 and body "12345"
curl -i "$U?hub.mode=subscribe&hub.verify_token=local-dev-verify-token&hub.challenge=12345"

# Wrong token — expect 403
curl -i "$U?hub.mode=subscribe&hub.verify_token=wrong&hub.challenge=12345"

# Signed POST — expect 200
B='{"object":"whatsapp_business_account","entry":[{"id":"0"}]}'
SIG=$(printf %s "$B" | openssl dgst -sha256 -hmac "$S" -hex | sed 's/^.* //')
curl -i -X POST "$U" -H 'Content-Type: application/json' \
  -H "X-Hub-Signature-256: sha256=$SIG" --data-raw "$B"

# Same signature, altered body — expect 403
curl -i -X POST "$U" -H 'Content-Type: application/json' \
  -H "X-Hub-Signature-256: sha256=$SIG" --data-raw "${B}x"
```

`printf %s` and `--data-raw` matter: a trailing newline or any reformatting
between signing and sending changes the bytes, and the signature will not verify.

Last verified against a local host — all six cases below behaved as documented:

| Case | Expected |
| --- | --- |
| `GET` correct verify token | `200`, body `12345` |
| `GET` wrong verify token | `403` |
| `POST` no signature | `403` |
| `POST` valid signature | `200` |
| `POST` body altered after signing | `403` |
| `POST` signature from a different secret | `403` |

The same rules are asserted executably in `tests/ParishRota.Functions.Tests/MetaSignatureTests.cs`,
whose expected digest comes from RFC 4231 rather than from the implementation.
