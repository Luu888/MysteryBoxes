using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace MysteryBoxes.Services;

public class TwitchChatService(IHttpClientFactory clients, TwitchTokenService tokens, IOptions<TwitchOptions> options, ILogger<TwitchChatService> log)
{
    public async Task SendAsync(string message)
    {
        var auth = await tokens.GetAsync();
        if (auth is null) return;
        var botId = auth.BroadcasterId;
        var http = clients.CreateClient();
        http.DefaultRequestHeaders.Add("Client-Id", options.Value.ClientId);
        http.DefaultRequestHeaders.Add("Authorization", "Bearer " + auth.AccessToken);
        var response = await http.PostAsJsonAsync("https://api.twitch.tv/helix/chat/messages", new
        {
            broadcaster_id = auth.BroadcasterId, sender_id = botId, message
        });
        if (!response.IsSuccessStatusCode)
            log.LogWarning("Twitch chat send failed: {Status} {Body}", response.StatusCode, await response.Content.ReadAsStringAsync());
    }
}
