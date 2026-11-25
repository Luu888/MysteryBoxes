using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using MysteryBoxes.Models;

namespace MysteryBoxes.Services;

public static class TwitchOAuth
{
    public static IResult Connect(HttpContext ctx, IOptions<TwitchOptions> opt)
    {
        var t = opt.Value;
        if (string.IsNullOrWhiteSpace(t.ClientId) || string.IsNullOrWhiteSpace(t.ClientSecret) || string.IsNullOrWhiteSpace(t.RedirectUri))
            return Results.Problem("Uzupełnij Twitch:ClientId, ClientSecret i RedirectUri w appsettings.json.");
        var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        ctx.Response.Cookies.Append("twitch_oauth_state", state, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax, Secure = ctx.Request.IsHttps, MaxAge = TimeSpan.FromMinutes(10) });
        var url = QueryHelpers.AddQueryString("https://id.twitch.tv/oauth2/authorize", new Dictionary<string, string?>
        {
            ["client_id"] = t.ClientId, ["redirect_uri"] = t.RedirectUri, ["response_type"] = "code",
            ["scope"] = "user:read:chat user:write:chat", ["state"] = state
        });
        return Results.Redirect(url);
    }
    public static async Task<IResult> Callback(HttpContext ctx, IHttpClientFactory clients, IOptions<TwitchOptions> opt, TwitchTokenService store)
    {
        var code = ctx.Request.Query["code"].ToString(); var state = ctx.Request.Query["state"].ToString();
        if (string.IsNullOrEmpty(code) || !ctx.Request.Cookies.TryGetValue("twitch_oauth_state", out var saved) || saved != state)
            return Results.BadRequest("Nieprawidłowy OAuth state.");
        var http = clients.CreateClient();
        var tokenResp = await http.PostAsJsonAsync("https://id.twitch.tv/oauth2/token", new
        {
            client_id = opt.Value.ClientId, client_secret = opt.Value.ClientSecret, code,
            grant_type = "authorization_code", redirect_uri = opt.Value.RedirectUri
        });
        if (!tokenResp.IsSuccessStatusCode) return Results.Problem("Twitch OAuth token exchange failed: " + await tokenResp.Content.ReadAsStringAsync());
        var token = await tokenResp.Content.ReadFromJsonAsync<TokenResponse>();
        if (token?.access_token is null) return Results.Problem("Brak tokenu Twitch.");
        http.DefaultRequestHeaders.Add("Client-Id", opt.Value.ClientId);
        http.DefaultRequestHeaders.Add("Authorization", "Bearer " + token.access_token);
        var users = await http.GetFromJsonAsync<UserResponse>("https://api.twitch.tv/helix/users");
        var u = users?.data?.FirstOrDefault();
        if (u?.id is null) return Results.Problem("Nie udało się pobrać konta Twitch.");
        await store.SaveAsync(new TwitchAuth { AccessToken = token.access_token, RefreshToken = token.refresh_token ?? "", BroadcasterId = u.id, DisplayName = u.display_name ?? u.login ?? "Twitch", ExpiresAt = DateTime.UtcNow.AddSeconds(token.expires_in) });
        ctx.Response.Cookies.Delete("twitch_oauth_state");
        return Results.Redirect("/Admin");
    }
    private record TokenResponse(string? access_token, string? refresh_token, int expires_in);
    private record UserResponse(List<User>? data);
    private record User(string? id, string? login, string? display_name);
}
