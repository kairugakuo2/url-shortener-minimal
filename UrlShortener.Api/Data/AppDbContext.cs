using Microsoft.EntityFrameworkCore;
using UrlShortener.Api.Models;

// bridge between C# code and SQLite database
namespace UrlShortener.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options){ }

    public DbSet<UrlMap> UrlMaps => Set<UrlMap>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UrlMap>()
            .HasIndex(u => u.ShortCode)
            .IsUnique();
    }
}