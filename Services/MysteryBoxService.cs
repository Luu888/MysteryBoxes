using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MysteryBoxes.Data;
using MysteryBoxes.Models;

namespace MysteryBoxes.Services;

public class MysteryBoxService(IDbContextFactory<AppDbContext> factory, IHubContext<MysteryHub> hub)
{
    public async Task<object> GetStateAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var ev = await db.Events.FirstOrDefaultAsync(x => x.IsActive);
        return new {
            title = ev?.Title ?? "MYSTERY BOXES", background = ev?.Background ?? "#071a13",
            boxColor = ev?.BoxColor ?? "#164632", accent = ev?.Accent ?? "#54ffae", columns = ev?.Columns ?? 6,
            animationSoundUrl = ev?.AnimationSoundUrl ?? "/sounds/reveal.mp3",
            prizes = await db.Prizes.OrderBy(x => x.Number).Select(x => new { x.Number, x.Name, x.ImageUrl, x.IsOpened }).ToListAsync()
        };
    }
    public async Task<object> OpenBoxAsync(int number)
    {
        await using var db = await factory.CreateDbContextAsync();
        // Warunkowy UPDATE: podwójne kliknięcie lub druga karta panelu nie otworzy paczki ponownie.
        var changed = await db.Prizes.Where(x => x.Number == number && !x.IsOpened)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsOpened, true));
        if (changed == 0) return new { ok = false, message = "Ta paczka jest już odkryta lub nie istnieje." };
        var prize = await db.Prizes.FirstAsync(x => x.Number == number);
        await Notify();
        return new { ok = true, message = $"Paczka nr {number}: {prize.Name}" };
    }
    public async Task<object> ResetBoxesAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        await db.Prizes.ExecuteUpdateAsync(s => s.SetProperty(x => x.IsOpened, false));
        await Notify(); return new { ok = true, message = "Wszystkie paczki są ponownie zakryte." };
    }
    public async Task<object> ShufflePrizesAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var prizes = await db.Prizes.OrderBy(x => x.Number).ToListAsync();
        if (prizes.Any(x => x.IsOpened)) return new { ok = false, message = "Przetasowanie dostępne tylko wtedy, gdy wszystkie paczki są zakryte." };
        var shuffled = prizes.OrderBy(_ => Random.Shared.Next()).Select(x => new { x.Name, x.ImageUrl }).ToList();
        for (var i = 0; i < prizes.Count; i++) { prizes[i].Name = shuffled[i].Name; prizes[i].ImageUrl = shuffled[i].ImageUrl; }
        await db.SaveChangesAsync(); await Notify(); return new { ok = true, message = "Nagrody przetasowane." };
    }
    public async Task<object> UpdatePrizeAsync(PrizeInput input)
    {
        if (input.Number is < 1 or > 24 || string.IsNullOrWhiteSpace(input.Name)) return new { ok = false, message = "Nieprawidłowy numer lub nazwa." };
        await using var db = await factory.CreateDbContextAsync();
        var p = await db.Prizes.FirstOrDefaultAsync(x => x.Number == input.Number);
        if (p is null) return new { ok = false, message = "Nie znaleziono paczki." };
        p.Name = input.Name.Trim(); p.ImageUrl = string.IsNullOrWhiteSpace(input.ImageUrl) ? "/images/gift.svg" : input.ImageUrl.Trim();
        await db.SaveChangesAsync(); await Notify(); return new { ok = true };
    }
    public async Task<object> SetTitleAsync(string title)
    {
        await using var db = await factory.CreateDbContextAsync();
        var ev = await EnsureEvent(db); ev.Title = string.IsNullOrWhiteSpace(title) ? "MYSTERY BOXES" : title.Trim();
        await db.SaveChangesAsync(); await Notify(); return new { ok = true };
    }
    public async Task<object> SetThemeAsync(ThemeInput input)
    {
        await using var db = await factory.CreateDbContextAsync();
        var ev = await EnsureEvent(db);
        ev.Background = ValidColor(input.Background, ev.Background); ev.BoxColor = ValidColor(input.Box, ev.BoxColor);
        ev.Accent = ValidColor(input.Accent, ev.Accent); ev.Columns = Math.Clamp(input.Columns ?? ev.Columns, 3, 8);
        await db.SaveChangesAsync(); await Notify(); return new { ok = true };
    }
    public async Task<object> SetAnimationSoundAsync(string? soundUrl)
    {
        await using var db = await factory.CreateDbContextAsync();
        var ev = await EnsureEvent(db);
        ev.AnimationSoundUrl = string.IsNullOrWhiteSpace(soundUrl) ? "/sounds/reveal.mp3" : soundUrl.Trim();
        await db.SaveChangesAsync();
        await Notify();
        return new { ok = true, message = "Zapisano dźwięk animacji." };
    }
    private static string ValidColor(string? value, string fallback) =>
        !string.IsNullOrWhiteSpace(value) && System.Text.RegularExpressions.Regex.IsMatch(value, "^#[0-9a-fA-F]{6}$") ? value : fallback;
    private static async Task<DrawEvent> EnsureEvent(AppDbContext db)
    {
        var ev = await db.Events.FirstOrDefaultAsync(x => x.IsActive);
        if (ev != null) return ev;
        ev = new DrawEvent(); db.Events.Add(ev); await db.SaveChangesAsync(); return ev;
    }
    private Task Notify() => hub.Clients.All.SendAsync("stateChanged");
}
public record PrizeInput(int Number, string Name, string ImageUrl);
public record ThemeInput(string? Background, string? Box, string? Accent, int? Columns);
