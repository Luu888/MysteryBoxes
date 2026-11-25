namespace MysteryBoxes.Models;
public class Prize
{
    public int Id { get; set; }
    public int Number { get; set; }
    public string Name { get; set; } = "";
    public string ImageUrl { get; set; } = "/images/gift.svg";
    public bool IsOpened { get; set; }
}
public class DrawEvent
{
    public int Id { get; set; }
    public string Title { get; set; } = "MYSTERY BOXES";
    public string Background { get; set; } = "#071a13";
    public string BoxColor { get; set; } = "#164632";
    public string Accent { get; set; } = "#54ffae";
    public int Columns { get; set; } = 6;
    public int SubLuckMultiplier { get; set; } = 1;
    public string AnimationSoundUrl { get; set; } = "/sounds/reveal.mp3";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}
public class DrawRound
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Idle"; // Open, Closed, WinnerSelected, Completed, Cancelled
    public string? WinnerTwitchId { get; set; }
    public string? WinnerName { get; set; }
    public int? ChosenPrizeNumber { get; set; }
}
public class Participant
{
    public int Id { get; set; }
    public int RoundId { get; set; }
    public string TwitchUserId { get; set; } = "";
    public string UserName { get; set; } = "";
    public bool IsSubscriber { get; set; }
    public int TicketWeight { get; set; } = 1;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
public class TwitchAuth
{
    public int Id { get; set; }
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public string BroadcasterId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
}
