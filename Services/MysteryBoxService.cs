using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MysteryBoxes.Data;
using MysteryBoxes.Models;

namespace MysteryBoxes.Services;

public class MysteryBoxService(IDbContextFactory<AppDbContext> factory, IHubContext<MysteryHub> hub, TwitchChatService chat)
{
    public async Task<object> GetStateAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var ev = await db.Events.FirstOrDefaultAsync(x => x.IsActive);
        var round = ev is null ? null : await db.Rounds.Where(x => x.EventId == ev.Id).OrderByDescending(x => x.Id).FirstOrDefaultAsync();
        return new {
            title = ev?.Title ?? "MYSTERY BOXES", background = ev?.Background ?? "#071a13",
            boxColor = ev?.BoxColor ?? "#164632", accent = ev?.Accent ?? "#54ffae", columns = ev?.Columns ?? 6, subLuckMultiplier = ev?.SubLuckMultiplier ?? 1,
            animationSoundUrl = ev?.AnimationSoundUrl ?? "/sounds/reveal.mp3",
            status = round?.Status ?? "Idle", winner = round?.WinnerName,
            prizes = await db.Prizes.OrderBy(x => x.Number).Select(x => new { x.Number, x.Name, x.ImageUrl, x.IsOpened }).ToListAsync(),
            participants = round is null ? 0 : await db.Participants.CountAsync(x => x.RoundId == round.Id)
        };
    }
    public async Task<object> GetAdminStateAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var state = await GetStateAsync();
        var rounds = await db.Rounds.OrderByDescending(x => x.Id).Take(30).Select(x => new { x.Id, x.Status, x.WinnerName, x.ChosenPrizeNumber, x.CreatedAt }).ToListAsync();
        var participants = await db.Rounds.OrderByDescending(x => x.Id).Select(x => x.Id).FirstOrDefaultAsync();
        var list = participants == 0 ? new List<Participant>() : await db.Participants.Where(x => x.RoundId == participants).OrderBy(x => x.JoinedAt).ToListAsync();
        return new { state, rounds, participants = list.Select(x => new { x.UserName, x.JoinedAt }) };
    }
    public async Task<bool> JoinAsync(string id, string name, bool isSubscriber = false)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name)) return false;
        await using var db = await factory.CreateDbContextAsync();
        var ev = await db.Events.FirstOrDefaultAsync(x => x.IsActive);
        if (ev is null) return false;
        var round = await db.Rounds.Where(x => x.EventId == ev.Id).OrderByDescending(x => x.Id).FirstOrDefaultAsync();
        if (round?.Status != "Open") return false;
        if (await db.Participants.AnyAsync(x => x.RoundId == round.Id && x.TwitchUserId == id)) return false;
        var multiplier = isSubscriber ? Math.Clamp(ev.SubLuckMultiplier, 1, 100) : 1;
        db.Participants.Add(new Participant { RoundId = round.Id, TwitchUserId = id, UserName = name, IsSubscriber = isSubscriber, TicketWeight = multiplier });
        await db.SaveChangesAsync();
        await chat.SendAsync($"@{name} zapisano do losowania! Powodzenia.");
        await Notify();
        return true;
    }
    public async Task<object> StartSignupsAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var ev = await EnsureEvent(db);
        var current = await db.Rounds.Where(x => x.EventId == ev.Id).OrderByDescending(x => x.Id).FirstOrDefaultAsync();
        if (current != null && current.Status is "Open" or "Closed" or "WinnerSelected")
            return new { ok = false, message = "Poprzednia runda nie została zakończona lub anulowana." };
        var round = new DrawRound { EventId = ev.Id, Status = "Open" };
        db.Rounds.Add(round); await db.SaveChangesAsync();
        await chat.SendAsync("🎟️ Zapisy do Mystery Boxes otwarte! Wpisz !ticket, aby wziąć udział.");
        await Notify(); return new { ok = true, message = "Zapisy otwarte." };
    }
    public async Task<object> CloseSignupsAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var r = await CurrentRound(db);
        if (r?.Status != "Open") return new { ok = false, message = "Zapisy nie są otwarte." };
        r.Status = "Closed"; await db.SaveChangesAsync();
        await chat.SendAsync("🔒 Zapisy zamknięte! Za chwilę losujemy zwycięzcę.");
        await Notify(); return new { ok = true, message = "Zapisy zamknięte." };
    }
    public async Task<object> DrawWinnerAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var r = await CurrentRound(db);
        if (r?.Status != "Closed") return new { ok = false, message = "Najpierw zamknij zapisy." };
        var people = await db.Participants.Where(x => x.RoundId == r.Id).ToListAsync();
        if (people.Count == 0) return new { ok = false, message = "Brak uczestników." };
        var totalWeight = people.Sum(x => Math.Max(1, x.TicketWeight));
        var ticket = Random.Shared.Next(1, totalWeight + 1);
        Participant winner = people[0];
        foreach (var person in people)
        {
            ticket -= Math.Max(1, person.TicketWeight);
            if (ticket <= 0) { winner = person; break; }
        }
        r.WinnerTwitchId = winner.TwitchUserId; r.WinnerName = winner.UserName; r.Status = "WinnerSelected";
        await db.SaveChangesAsync();
        await chat.SendAsync($"🎉 Zwycięzcą Mystery Boxes jest {winner.UserName}! Wybierz paczkę komendą !paczka NUMER.");
        await Notify(); return new { ok = true, winner = winner.UserName };
    }
    public async Task<object> ChooseBoxAsync(int number, string twitchId, string userName)
    {
        await using var db = await factory.CreateDbContextAsync();
        var r = await CurrentRound(db);
        if (r?.Status != "WinnerSelected") return new { ok = false, message = "Nie ma zwycięzcy oczekującego na wybór." };
        if (r.WinnerTwitchId != twitchId) return new { ok = false, message = "Tylko wylosowany zwycięzca może wybrać paczkę." };
        var prize = await db.Prizes.FirstOrDefaultAsync(x => x.Number == number && !x.IsOpened);
        if (prize is null) return new { ok = false, message = "Ta paczka jest już odkryta lub nie istnieje." };
        prize.IsOpened = true; r.ChosenPrizeNumber = number; r.Status = "Completed";
        await db.SaveChangesAsync();
        await chat.SendAsync($"🎁 {userName} wybrał paczkę nr {number}: {prize.Name}!");
        await Notify();
        return new { ok = true, prize = prize.Name, image = prize.ImageUrl, number };
    }
    public async Task<object> CancelRoundAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var r = await CurrentRound(db);
        if (r is null || r.Status is "Completed" or "Cancelled") return new { ok = false, message = "Brak aktywnej rundy." };
        r.Status = "Cancelled"; await db.SaveChangesAsync();
        await chat.SendAsync("Runda Mystery Boxes została anulowana przez streamera.");
        await Notify(); return new { ok = true };
    }
    public async Task<object> NewEventAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var active = await db.Events.FirstOrDefaultAsync(x => x.IsActive);
        if (active != null) active.IsActive = false;
        var ev = new DrawEvent { Title = active?.Title ?? "MYSTERY BOXES", Background = active?.Background ?? "#071a13", BoxColor = active?.BoxColor ?? "#164632", Accent = active?.Accent ?? "#54ffae", Columns = active?.Columns ?? 6, SubLuckMultiplier = active?.SubLuckMultiplier ?? 1, AnimationSoundUrl = active?.AnimationSoundUrl ?? "/sounds/reveal.mp3" };
        db.Events.Add(ev);
        foreach (var p in await db.Prizes.ToListAsync()) p.IsOpened = false;
        await db.SaveChangesAsync();
        await chat.SendAsync("Nowe wydarzenie Mystery Boxes rozpoczęte. Wszystkie paczki są ponownie zakryte.");
        await Notify(); return new { ok = true };
    }
    public async Task<object> ShufflePrizesAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var r = await CurrentRound(db);
        if (r != null && r.Status is "Open" or "Closed" or "WinnerSelected") return new { ok = false, message = "Nie można tasować podczas aktywnej rundy." };
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
    public async Task<object> SetSubLuckAsync(int multiplier)
    {
        if (multiplier is < 1 or > 100)
            return new { ok = false, message = "Mnożnik musi być w zakresie 1–100." };
        await using var db = await factory.CreateDbContextAsync();
        var ev = await EnsureEvent(db);
        ev.SubLuckMultiplier = multiplier;
        await db.SaveChangesAsync();
        await Notify();
        return new { ok = true, message = $"Mnożnik subluck ustawiony na {multiplier}x." };
    }
    private static string ValidColor(string? value, string fallback) =>
        !string.IsNullOrWhiteSpace(value) && System.Text.RegularExpressions.Regex.IsMatch(value, "^#[0-9a-fA-F]{6}$") ? value : fallback;
    private static async Task<DrawEvent> EnsureEvent(AppDbContext db)
    {
        var ev = await db.Events.FirstOrDefaultAsync(x => x.IsActive);
        if (ev != null) return ev;
        ev = new DrawEvent(); db.Events.Add(ev); await db.SaveChangesAsync(); return ev;
    }
    private static Task<DrawRound?> CurrentRound(AppDbContext db) =>
        db.Rounds.Where(x => db.Events.Any(e => e.IsActive && e.Id == x.EventId)).OrderByDescending(x => x.Id).FirstOrDefaultAsync();
    private Task Notify() => hub.Clients.All.SendAsync("stateChanged");
}
public record PrizeInput(int Number, string Name, string ImageUrl);
public record ThemeInput(string? Background, string? Box, string? Accent, int? Columns);
