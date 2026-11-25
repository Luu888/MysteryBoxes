using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using MysteryBoxes.Data;
using MysteryBoxes.Models;
using MysteryBoxes.Services;

var builder = WebApplication.CreateBuilder(args);

// Mystery Boxes zawsze działa lokalnie przez HTTP na porcie 57286.
// Ten sam adres jest używany przez Twitch OAuth RedirectUri.
builder.WebHost.UseUrls("http://localhost:57286");
builder.Services.AddRazorPages(options =>
{
    // Zabezpieczamy cały panel administracyjny konwencją Razor Pages.
    // Logowanie pozostaje publiczne, aby można było uzyskać cookie administratora.
    options.Conventions.AuthorizePage("/Admin");
    options.Conventions.AllowAnonymousToPage("/Login");
});
builder.Services.AddSignalR();
builder.Services.AddHttpClient();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Login";
        o.AccessDeniedPath = "/Login";
        o.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }
            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
        o.Cookie.Name = "MysteryBoxes.Admin";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        o.ExpireTimeSpan = TimeSpan.FromHours(12);
        o.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();
builder.Services.Configure<TwitchOptions>(builder.Configuration.GetSection("Twitch"));
builder.Services.AddDbContextFactory<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddSingleton<TwitchChatService>();
builder.Services.AddSingleton<MysteryBoxService>();
builder.Services.AddSingleton<TwitchTokenService>();
builder.Services.AddHostedService<TwitchEventSubService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    await db.Database.EnsureCreatedAsync();
    // EnsureCreated does not alter an existing SQLite schema; add the animation setting when upgrading.
    var columns = new List<string>();
    await using (var cmd = db.Database.GetDbConnection().CreateCommand())
    {
        cmd.CommandText = "PRAGMA table_info('Events')";
        if (cmd.Connection!.State != System.Data.ConnectionState.Open) await cmd.Connection.OpenAsync();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) columns.Add(reader.GetString(1));
    }
    if (!columns.Contains("AnimationSoundUrl"))
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE Events ADD COLUMN AnimationSoundUrl TEXT NOT NULL DEFAULT '/sounds/reveal.mp3'");

    if (!await db.Events.AnyAsync())
        db.Events.Add(new DrawEvent { Title = "MYSTERY BOXES", IsActive = true });
    if (!await db.Prizes.AnyAsync())
    {
        for (var i = 1; i <= 24; i++)
            db.Prizes.Add(new Prize { Number = i, Name = $"Mystery Box {i}", ImageUrl = "/images/gift.svg", IsOpened = false });
    }
    await db.SaveChangesAsync();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapHub<MysteryHub>("/mysteryHub");
app.MapGet("/api/state", async (MysteryBoxService svc) => Results.Ok(await svc.GetStateAsync()));
app.MapGet("/api/admin/state", async (MysteryBoxService svc) => Results.Ok(await svc.GetAdminStateAsync())).RequireAuthorization();

app.MapPost("/api/admin/start", async (MysteryBoxService svc) => Results.Ok(await svc.StartSignupsAsync())).RequireAuthorization();
app.MapPost("/api/admin/close", async (MysteryBoxService svc) => Results.Ok(await svc.CloseSignupsAsync())).RequireAuthorization();
app.MapPost("/api/admin/draw", async (MysteryBoxService svc) => Results.Ok(await svc.DrawWinnerAsync())).RequireAuthorization();
app.MapPost("/api/admin/cancel-round", async (MysteryBoxService svc) => Results.Ok(await svc.CancelRoundAsync())).RequireAuthorization();
app.MapPost("/api/admin/new-event", async (MysteryBoxService svc) => Results.Ok(await svc.NewEventAsync())).RequireAuthorization();
app.MapPost("/api/admin/shuffle", async (MysteryBoxService svc) => Results.Ok(await svc.ShufflePrizesAsync())).RequireAuthorization();
app.MapPost("/api/admin/title", async (TitleInput input, MysteryBoxService svc) => Results.Ok(await svc.SetTitleAsync(input.Title))).RequireAuthorization();
app.MapPost("/api/admin/settings", async (ThemeInput input, MysteryBoxService svc) => Results.Ok(await svc.SetThemeAsync(input))).RequireAuthorization();
app.MapPost("/api/admin/subluck", async (SubLuckInput input, MysteryBoxService svc) => Results.Ok(await svc.SetSubLuckAsync(input.Multiplier))).RequireAuthorization();
app.MapPost("/api/admin/animation-sound", async (SoundInput input, MysteryBoxService svc) => Results.Ok(await svc.SetAnimationSoundAsync(input.SoundUrl))).RequireAuthorization();
app.MapPost("/api/admin/upload-sound", async (HttpRequest req, IWebHostEnvironment env) =>
{
    if (!req.HasFormContentType) return Results.BadRequest("Wymagany multipart/form-data.");
    var form = await req.ReadFormAsync();
    var file = form.Files.GetFile("file");
    if (file is null || file.Length == 0) return Results.BadRequest("Nie wybrano pliku.");
    if (file.Length > 10 * 1024 * 1024) return Results.BadRequest("Maksymalny rozmiar pliku to 10 MB.");
    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (ext is not (".mp3" or ".wav" or ".ogg")) return Results.BadRequest("Dozwolone formaty: MP3, WAV, OGG.");
    var folder = Path.Combine(env.WebRootPath, "uploads", "sounds");
    Directory.CreateDirectory(folder);
    var name = $"{Guid.NewGuid():N}{ext}";
    await using (var stream = File.Create(Path.Combine(folder, name))) await file.CopyToAsync(stream);
    return Results.Ok(new { url = "/uploads/sounds/" + name });
}).RequireAuthorization();
app.MapPost("/api/admin/prizes", async (PrizeInput input, MysteryBoxService svc) => Results.Ok(await svc.UpdatePrizeAsync(input))).RequireAuthorization();
app.MapPost("/api/admin/upload", async (HttpRequest req, IWebHostEnvironment env, MysteryBoxService svc) =>
{
    if (!req.HasFormContentType) return Results.BadRequest("Wymagany multipart/form-data.");
    var form = await req.ReadFormAsync();
    var file = form.Files.GetFile("file");
    if (file is null || file.Length == 0) return Results.BadRequest("Nie wybrano pliku.");
    if (file.Length > 5 * 1024 * 1024) return Results.BadRequest("Maksymalny rozmiar pliku to 5 MB.");
    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (ext is not (".png" or ".jpg" or ".jpeg" or ".webp" or ".gif")) return Results.BadRequest("Dozwolone: PNG, JPG, WEBP, GIF.");
    var folder = Path.Combine(env.WebRootPath, "uploads");
    Directory.CreateDirectory(folder);
    var name = $"{Guid.NewGuid():N}{ext}";
    await using (var stream = File.Create(Path.Combine(folder, name))) await file.CopyToAsync(stream);
    return Results.Ok(new { url = "/uploads/" + name });
}).RequireAuthorization();

app.MapGet("/twitch/connect", TwitchOAuth.Connect);
app.MapGet("/twitch/callback", TwitchOAuth.Callback);
app.MapPost("/api/admin/twitch/disconnect", async (TwitchTokenService tokens) => { await tokens.DeleteAsync(); return Results.Ok(new { ok = true }); }).RequireAuthorization();
app.MapGet("/api/admin/twitch/status", async (TwitchTokenService tokens) =>
{
    var a = await tokens.GetAsync();
    return Results.Ok(new { connected = a is not null, name = a?.DisplayName, expiresAt = a?.ExpiresAt });
}).RequireAuthorization();

app.MapPost("/api/chat/join", async (JoinInput input, MysteryBoxService svc) =>
    Results.Ok(new { joined = await svc.JoinAsync(input.TwitchUserId, input.UserName) }));
app.MapPost("/api/chat/choose", async (ChooseInput input, MysteryBoxService svc) =>
    Results.Ok(await svc.ChooseBoxAsync(input.Number, input.TwitchUserId, input.UserName)));

app.Run();

record TitleInput(string Title);
record SubLuckInput(int Multiplier);
record SoundInput(string? SoundUrl);
record JoinInput(string TwitchUserId, string UserName);
record ChooseInput(int Number, string TwitchUserId, string UserName);
