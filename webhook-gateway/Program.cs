using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://0.0.0.0:{Environment.GetEnvironmentVariable("PORT") ?? "8080"}");
builder.Services.AddHttpClient("upstream", c => c.Timeout = TimeSpan.FromSeconds(20));
var app = builder.Build();

var secret = Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET") ?? "";
var upstream = Environment.GetEnvironmentVariable("UPSTREAM_URL") ?? "";
var gatewaySecret = Environment.GetEnvironmentVariable("GATEWAY_SECRET") ?? "";

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/payments/webhook", async (HttpRequest req, IHttpClientFactory factory, ILogger<Program> log) =>
{
    using var ms = new MemoryStream();
    await req.Body.CopyToAsync(ms);
    var body = ms.ToArray();
    var header = req.Headers["Stripe-Signature"].ToString();

    if (!Verify(body, header, secret))
    {
        log.LogWarning("Rejected webhook: invalid signature");
        return Results.BadRequest(new { error = "invalid signature" });
    }

    if (string.IsNullOrEmpty(upstream))
    {
        log.LogError("UPSTREAM_URL is not set");
        return Results.StatusCode(503);
    }

    var msg = new HttpRequestMessage(HttpMethod.Post, upstream) { Content = new ByteArrayContent(body) };
    msg.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
    msg.Headers.TryAddWithoutValidation("Stripe-Signature", header);
    msg.Headers.TryAddWithoutValidation("X-Gateway-Secret", gatewaySecret);

    try
    {
        var resp = await factory.CreateClient("upstream").SendAsync(msg);
        log.LogInformation("Forwarded webhook, upstream returned {Status}", (int)resp.StatusCode);
        return Results.StatusCode((int)resp.StatusCode);
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Upstream unreachable");
        return Results.StatusCode(502);
    }
});

app.Run();

static bool Verify(byte[] body, string header, string secret)
{
    if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(header)) return false;

    long t = 0;
    var sigs = new List<string>();
    foreach (var part in header.Split(','))
    {
        var kv = part.Split('=', 2);
        if (kv.Length != 2) continue;
        var key = kv[0].Trim();
        if (key == "t") long.TryParse(kv[1], out t);
        else if (key == "v1") sigs.Add(kv[1].Trim());
    }
    if (t == 0 || sigs.Count == 0) return false;
    if (Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - t) > 300) return false;

    var payload = Encoding.UTF8.GetBytes($"{t}.").Concat(body).ToArray();
    using var h = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
    var expected = Convert.ToHexString(h.ComputeHash(payload)).ToLowerInvariant();

    return sigs.Any(s => CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(s), Encoding.UTF8.GetBytes(expected)));
}
