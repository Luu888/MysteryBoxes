using Microsoft.EntityFrameworkCore;
using MysteryBoxes.Models;

namespace MysteryBoxes.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Prize> Prizes => Set<Prize>();
    public DbSet<DrawEvent> Events => Set<DrawEvent>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Prize>().HasIndex(x => x.Number).IsUnique();
    }
}
