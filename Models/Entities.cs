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
    public string AnimationSoundUrl { get; set; } = "/sounds/reveal.mp3";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}
