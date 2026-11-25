using Microsoft.EntityFrameworkCore;
using MysteryBoxes.Models;

namespace MysteryBoxes.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Prize> Prizes => Set<Prize>();
    public DbSet<DrawEvent> Events => Set<DrawEvent>();
    public DbSet<DrawRound> Rounds => Set<DrawRound>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<TwitchAuth> TwitchAuth => Set<TwitchAuth>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Prize>().HasIndex(x => x.Number).IsUnique();
        b.Entity<Participant>().HasIndex(x => new { x.RoundId, x.TwitchUserId }).IsUnique();
    }
}
