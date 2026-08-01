using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ParishRota.Functions;

/// <summary>
/// The single entry point for everything Readers and the Coordinator send
/// (ADR 0003 — WhatsApp is the only interface).
///
/// Meta calls this in two different ways: a one-off GET to verify the
/// subscription, then a POST for every inbound message.
/// </summary>
public class WhatsAppWebhook(ILogger<WhatsAppWebhook> logger, IConfiguration configuration)
{
    // Anonymous because Meta cannot present a Functions key. Authenticity comes
    // from the verify token on GET and, once implemented, the X-Hub-Signature-256
    // HMAC on POST — see the TODO below.
    [Function("WhatsAppWebhook")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "whatsapp")] HttpRequest req)
    {
        if (HttpMethods.IsGet(req.Method))
        {
            return HandleVerification(req);
        }

        // TODO: verify the X-Hub-Signature-256 HMAC against the Meta app secret
        // before trusting this payload. Until that lands, the endpoint accepts
        // anything that can reach it.
        var payload = await new StreamReader(req.Body).ReadToEndAsync();

        // Meta retries on slow responses and disables endpoints that keep timing
        // out, so acknowledge immediately and do the real work off the request
        // path (a queue trigger, once intent parsing exists).
        logger.LogInformation("Inbound WhatsApp payload received ({Length} bytes).", payload.Length);

        return new OkResult();
    }

    /// <summary>
    /// Meta's subscription handshake: echo back hub.challenge, but only when the
    /// token it presents matches the one we configured.
    /// </summary>
    private IActionResult HandleVerification(HttpRequest req)
    {
        var mode = req.Query["hub.mode"].ToString();
        var token = req.Query["hub.verify_token"].ToString();
        var challenge = req.Query["hub.challenge"].ToString();

        var expected = configuration["WHATSAPP_VERIFY_TOKEN"];

        if (mode == "subscribe"
            && !string.IsNullOrEmpty(expected)
            && string.Equals(token, expected, StringComparison.Ordinal))
        {
            logger.LogInformation("WhatsApp webhook verification succeeded.");
            return new ContentResult
            {
                Content = challenge,
                ContentType = "text/plain",
                StatusCode = StatusCodes.Status200OK
            };
        }

        logger.LogWarning("WhatsApp webhook verification rejected (mode={Mode}).", mode);
        return new StatusCodeResult(StatusCodes.Status403Forbidden);
    }
}
