using Cleanarr.Models;
using Microsoft.EntityFrameworkCore;

namespace Cleanarr.Data;

public class CleanarrDbContext : DbContext
{
    public CleanarrDbContext(DbContextOptions<CleanarrDbContext> options)
        : base(options)
    {
    }

    public DbSet<Movie> Movies { get; set; }
    public DbSet<Series> Series { get; set; }
    public DbSet<Episode> Episodes { get; set; }
    public DbSet<SuggestionRule> SuggestionRules { get; set; }
    public DbSet<Suggestion> Suggestions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Movie>()
            .HasIndex(m => m.RadarrId)
            .IsUnique();

        modelBuilder.Entity<Series>()
            .HasIndex(s => s.SonarrId)
            .IsUnique();

        modelBuilder.Entity<Episode>()
            .HasIndex(e => e.SonarrEpisodeId)
            .IsUnique();

        // Seed default suggestion rules
        modelBuilder.Entity<SuggestionRule>().HasData(
            new SuggestionRule 
            { 
                Id = 1, 
                Name = "Not Watched (180 days)", 
                Description = "Movies/Series not watched in 180 days",
                Enabled = true,
                ApplyToMovies = true,
                ApplyToSeries = true,
                IsCustom = false,
                ConditionsJson = "[{\"field\":\"lastWatched\",\"operator\":\"before\",\"value\":\"180\",\"valueType\":\"customDays\",\"logicalOperator\":null}]"
            },
            new SuggestionRule 
            { 
                Id = 2, 
                Name = "Fully Watched (2+ people)", 
                Description = "Movies watched by 2 or more people",
                Enabled = true,
                ApplyToMovies = true,
                ApplyToSeries = false,
                IsCustom = false,
                ConditionsJson = "[{\"field\":\"watchCount\",\"operator\":\"bigger\",\"value\":\"2\",\"valueType\":\"customNumber\",\"logicalOperator\":null}]"
            },
            new SuggestionRule 
            { 
                Id = 3, 
                Name = "Ignored Request (90 days)", 
                Description = "Requested but not watched for 90 days",
                Enabled = true,
                ApplyToMovies = true,
                ApplyToSeries = true,
                IsCustom = false,
                ConditionsJson = "[{\"field\":\"requestedDate\",\"operator\":\"before\",\"value\":\"90\",\"valueType\":\"customDays\",\"logicalOperator\":\"AND\"},{\"field\":\"lastWatched\",\"operator\":\"equals\",\"value\":\"null\",\"valueType\":\"null\",\"logicalOperator\":null}]"
            },
            new SuggestionRule 
            { 
                Id = 4, 
                Name = "Unmonitored Cleanup (30 days)", 
                Description = "Unmonitored and not watched for 30 days",
                Enabled = false,
                ApplyToMovies = true,
                ApplyToSeries = true,
                IsCustom = false,
                ConditionsJson = "[{\"field\":\"monitored\",\"operator\":\"equals\",\"value\":\"false\",\"valueType\":\"boolean\",\"logicalOperator\":\"AND\"},{\"field\":\"lastWatched\",\"operator\":\"before\",\"value\":\"30\",\"valueType\":\"customDays\",\"logicalOperator\":null}]"
            }
        );
    }
}
