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
        var code = ctx.Request.Query["code"].ToString();
        var state = ctx.Request.Query["state"].ToString();
        var oauthError = ctx.Request.Query["error"].ToString();
        var oauthErrorDescription = ctx.Request.Query["error_description"].ToString();
        if (!string.IsNullOrWhiteSpace(oauthError))
            return Results.BadRequest($"Twitch OAuth: {oauthError} – {oauthErrorDescription}");
        if (string.IsNullOrEmpty(code) || !ctx.Request.Cookies.TryGetValue("twitch_oauth_state", out var saved) || saved != state)
            return Results.BadRequest("Nieprawidłowy OAuth state. Rozpocznij łączenie ponownie z panelu administratora.");
        var twitch = opt.Value;
        if (string.IsNullOrWhiteSpace(twitch.ClientId))
            return Results.Problem("Brak Twitch:ClientId. Aplikacja nie wczytała wartości z appsettings.json.");
        if (string.IsNullOrWhiteSpace(twitch.ClientSecret))
            return Results.Problem("Brak Twitch:ClientSecret. Uzupełnij nowy sekret w appsettings.json.");

        // Wczytujemy i walidujemy dokładnie te wartości, które trafią do żądania tokenowego.
        var clientId = twitch.ClientId.Trim();
        var clientSecret = twitch.ClientSecret.Trim();
        var redirectUri = twitch.RedirectUri.Trim();

        if (string.IsNullOrWhiteSpace(clientId))
            return Results.Problem("Brak Twitch:ClientId w konfiguracji aplikacji.");
        if (string.IsNullOrWhiteSpace(clientSecret))
            return Results.Problem("Brak Twitch:ClientSecret w konfiguracji aplikacji.");
        if (string.IsNullOrWhiteSpace(redirectUri))
            return Results.Problem("Brak Twitch:RedirectUri w konfiguracji aplikacji.");

        var http = clients.CreateClient();
        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "https://id.twitch.tv/oauth2/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["code"] = code,
                ["grant_type"] = "authorization_code",
                ["redirect_uri"] = redirectUri
            })
        };

        // client_id jest także w query string jako dodatkowa kompatybilność;
        // wymagane parametry pozostają również w body x-www-form-urlencoded.
        tokenRequest.RequestUri = new Uri(QueryHelpers.AddQueryString(
            "https://id.twitch.tv/oauth2/token",
            "client_id",
            clientId));

        Console.WriteLine($"[Twitch OAuth] Token exchange: client_id present=true; length={clientId.Length}; redirect_uri={redirectUri}; content_type={tokenRequest.Content!.Headers.ContentType}; body_length={(await tokenRequest.Content.ReadAsByteArrayAsync()).Length}");

        using var tokenResp = await http.SendAsync(tokenRequest);
        if (!tokenResp.IsSuccessStatusCode)
        {
            var errorBody = await tokenResp.Content.ReadAsStringAsync();
            Console.WriteLine($"[Twitch OAuth] Token exchange failed: HTTP {(int)tokenResp.StatusCode}; response={errorBody}");
            return Results.Problem("Twitch OAuth token exchange failed: " + errorBody);
        }
        var token = await tokenResp.Content.ReadFromJsonAsync<TokenResponse>();
        if (token?.access_token is null) return Results.Problem("Brak tokenu Twitch.");
        http.DefaultRequestHeaders.Add("Client-Id", clientId);
        http.DefaultRequestHeaders.Add("Authorization", "Bearer " + token.access_token);
        UserResponse? users;
        try
        {
            users = await http.GetFromJsonAsync<UserResponse>("https://api.twitch.tv/helix/users");
        }
        catch (HttpRequestException ex)
        {
            return Results.Problem("Twitch przyjął token, ale pobranie profilu nie powiodło się: " + ex.Message);
        }
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
