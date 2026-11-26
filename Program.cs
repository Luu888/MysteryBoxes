using Microsoft.EntityFrameworkCore;
using MysteryBoxes.Data;
using MysteryBoxes.Models;
using MysteryBoxes.Services;

var builder = WebApplication.CreateBuilder(args);

// Mystery Boxes zawsze działa lokalnie przez HTTP na porcie 57286.
builder.WebHost.UseUrls("http://localhost:57286");
builder.Services.AddRazorPages();
builder.Services.AddSignalR();
builder.Services.AddDbContextFactory<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddSingleton<MysteryBoxService>();

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
    if (!columns.Contains("SponsorImages"))
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE Events ADD COLUMN SponsorImages TEXT NOT NULL DEFAULT ''");
    if (!columns.Contains("SponsorText"))
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE Events ADD COLUMN SponsorText TEXT NOT NULL DEFAULT ''");

    if (!await db.Events.AnyAsync())
        db.Events.Add(new DrawEvent { Title = "MYSTERY BOXES", IsActive = true });
    if (!await db.Prizes.AnyAsync())
    {
        for (var i = 1; i <= 24; i++)
            db.Prizes.Add(new Prize { Number = i, Name = $"Mystery Box {i}", ImageUrl = "/images/gift.svg", IsOpened = false });
    }
    await db.SaveChangesAsync();
}

// Bez logowania: odrzucamy zapisy wysyłane z innych stron otwartych w przeglądarce na tym komputerze.
app.Use(async (context, next) =>
{
    var origin = context.Request.Headers.Origin.ToString();
    if (!HttpMethods.IsGet(context.Request.Method) && origin.Length > 0
        && origin != $"{context.Request.Scheme}://{context.Request.Host}")
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }
    await next();
});
app.UseStaticFiles();
app.UseRouting();

app.MapRazorPages();
app.MapHub<MysteryHub>("/mysteryHub");
app.MapGet("/api/state", async (MysteryBoxService svc) => Results.Ok(await svc.GetStateAsync()));

app.MapPost("/api/admin/open", async (OpenInput input, MysteryBoxService svc) => Results.Ok(await svc.OpenBoxAsync(input.Number)));
app.MapPost("/api/admin/reset", async (MysteryBoxService svc) => Results.Ok(await svc.ResetBoxesAsync()));
app.MapPost("/api/admin/shuffle", async (MysteryBoxService svc) => Results.Ok(await svc.ShufflePrizesAsync()));
app.MapPost("/api/admin/title", async (TitleInput input, MysteryBoxService svc) => Results.Ok(await svc.SetTitleAsync(input.Title)));
app.MapPost("/api/admin/settings", async (ThemeInput input, MysteryBoxService svc) => Results.Ok(await svc.SetThemeAsync(input)));
app.MapPost("/api/admin/animation-sound", async (SoundInput input, MysteryBoxService svc) => Results.Ok(await svc.SetAnimationSoundAsync(input.SoundUrl)));
app.MapPost("/api/admin/sponsors", async (SponsorsInput input, MysteryBoxService svc) => Results.Ok(await svc.SetSponsorsAsync(input)));
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
});
app.MapPost("/api/admin/prizes", async (PrizeInput input, MysteryBoxService svc) => Results.Ok(await svc.UpdatePrizeAsync(input)));
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
});

app.Run();

record TitleInput(string Title);
record SoundInput(string? SoundUrl);
record OpenInput(int Number);
