using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MysteryBoxes.Data;
using MysteryBoxes.Models;

namespace MysteryBoxes.Services;

public class TwitchTokenService(IDbContextFactory<AppDbContext> factory, IHttpClientFactory clients, IOptions<TwitchOptions> options, ILogger<TwitchTokenService> log)
{
    public async Task<TwitchAuth?> GetAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var auth = await db.TwitchAuth.FirstOrDefaultAsync();
        if (auth is null) return null;
        if (auth.ExpiresAt <= DateTime.UtcNow.AddMinutes(5))
        {
            try
            {
                var http = clients.CreateClient();
                var response = await http.PostAsJsonAsync("https://id.twitch.tv/oauth2/token", new
                {
                    client_id = options.Value.ClientId, client_secret = options.Value.ClientSecret,
                    grant_type = "refresh_token", refresh_token = auth.RefreshToken
                });
                if (!response.IsSuccessStatusCode) { log.LogWarning("Twitch refresh failed: {Body}", await response.Content.ReadAsStringAsync()); return auth; }
                var token = await response.Content.ReadFromJsonAsync<TokenResponse>();
                if (token?.access_token is not null)
                {
                    auth.AccessToken = token.access_token;
                    auth.RefreshToken = token.refresh_token ?? auth.RefreshToken;
                    auth.ExpiresAt = DateTime.UtcNow.AddSeconds(token.expires_in);
                    await db.SaveChangesAsync();
                }
            }
            catch (Exception ex) { log.LogError(ex, "Twitch token refresh failed."); }
        }
        return auth;
    }
    public async Task SaveAsync(TwitchAuth auth)
    {
        await using var db = await factory.CreateDbContextAsync();
        var old = await db.TwitchAuth.FirstOrDefaultAsync();
        if (old is not null) db.TwitchAuth.Remove(old);
        db.TwitchAuth.Add(auth); await db.SaveChangesAsync();
    }
    public async Task DeleteAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        db.TwitchAuth.RemoveRange(db.TwitchAuth);
        await db.SaveChangesAsync();
    }
    private record TokenResponse(string? access_token, string? refresh_token, int expires_in);
}
