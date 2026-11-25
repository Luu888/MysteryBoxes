using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MysteryBoxes.Data;

namespace MysteryBoxes.Services;

public class TwitchEventSubService(IServiceScopeFactory scopes, IHttpClientFactory clients, IOptions<TwitchOptions> options,
    MysteryBoxService boxes, ILogger<TwitchEventSubService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Listen(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { log.LogWarning(ex, "Twitch EventSub disconnected; retrying."); }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
    private async Task Listen(CancellationToken ct)
    {
        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri("wss://eventsub.wss.twitch.tv/ws"), ct);
        var buffer = new byte[8192];
        while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            using var ms = new MemoryStream(); ValueWebSocketReceiveResult result;
            do { result = await ws.ReceiveAsync(buffer.AsMemory(), ct); ms.Write(buffer, 0, result.Count); } while (!result.EndOfMessage);
            using var doc = JsonDocument.Parse(ms.ToArray());
            var root = doc.RootElement; var meta = root.GetProperty("metadata");
            var type = meta.GetProperty("message_type").GetString();
            if (type == "session_welcome")
            {
                var session = root.GetProperty("payload").GetProperty("session").GetProperty("id").GetString();
                if (session is not null)
                {
                    var subscribed = await Subscribe(session, ct);
                    if (!subscribed)
                    {
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Twitch not connected yet", ct);
                        return;
                    }
                }
            }
            else if (type == "notification")
            {
                var sub = root.GetProperty("payload").GetProperty("subscription");
                if (sub.GetProperty("type").GetString() == "channel.chat.message")
                {
                    var ev = root.GetProperty("payload").GetProperty("event");
                    var text = ev.GetProperty("message").GetProperty("text").GetString()?.Trim() ?? "";
                    var userId = ev.GetProperty("chatter_user_id").GetString() ?? "";
                    var user = ev.GetProperty("chatter_user_name").GetString() ?? "";
                    var isSubscriber = false;
                    if (ev.TryGetProperty("badges", out var badges) && badges.ValueKind == JsonValueKind.Array)
                    {
                        isSubscriber = badges.EnumerateArray().Any(b =>
                            b.TryGetProperty("set_id", out var setId) &&
                            (setId.GetString() == "subscriber" || setId.GetString() == "founder"));
                    }
                    if (text.Equals("!ticket", StringComparison.OrdinalIgnoreCase))
                        await boxes.JoinAsync(userId, user, isSubscriber);
                    else if (text.StartsWith("!paczka ", StringComparison.OrdinalIgnoreCase) &&
                             int.TryParse(text.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault(), out var n))
                        await boxes.ChooseBoxAsync(n, userId, user);
                }
            }
            else if (type == "session_reconnect")
            {
                // Force a fresh session. The outer worker reconnects and creates a new subscription.
                // This is a fallback path; normal reconnect URL handoff can be added for zero-gap reconnects.
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Reconnect", ct);
                return;
            }
            else if (type == "session_keepalive") { }
        }
    }
    private async Task<bool> Subscribe(string session, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await factory.CreateDbContextAsync(ct);
        var auth = await db.TwitchAuth.FirstOrDefaultAsync(ct);
        if (auth is null) return false;
        var http = clients.CreateClient();
        http.DefaultRequestHeaders.Add("Client-Id", options.Value.ClientId);
        http.DefaultRequestHeaders.Add("Authorization", "Bearer " + auth.AccessToken);
        var body = new { type = "channel.chat.message", version = "1", condition = new { broadcaster_user_id = auth.BroadcasterId, user_id = auth.BroadcasterId }, transport = new { method = "websocket", session_id = session } };
        var response = await http.PostAsJsonAsync("https://api.twitch.tv/helix/eventsub/subscriptions", body, ct);
        if (!response.IsSuccessStatusCode)
        {
            log.LogWarning("EventSub subscription failed: {Status} {Body}", response.StatusCode, await response.Content.ReadAsStringAsync(ct));
            return false;
        }
        return true;
    }
}
