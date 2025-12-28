using Cleanarr.Data;
using Cleanarr.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace Cleanarr.Services;

public class SuggestionService
{
    private readonly CleanarrDbContext _db;

    public SuggestionService(CleanarrDbContext db)
    {
        _db = db;
    }

    public async Task GenerateSuggestionsAsync()
    {
        Console.WriteLine("Generating suggestions...");
        
        var rules = await _db.SuggestionRules.Where(r => r.Enabled).ToListAsync();
        
        // Clear old non-dismissed suggestions
        var oldSuggestions = await _db.Suggestions.Where(s => !s.Dismissed).ToListAsync();
        _db.Suggestions.RemoveRange(oldSuggestions);
        
        foreach (var rule in rules)
        {
            await ApplyRuleAsync(rule);
        }
        
        await _db.SaveChangesAsync();
        
        var count = await _db.Suggestions.CountAsync(s => !s.Dismissed);
        Console.WriteLine($"Generated {count} suggestions");
    }

    private async Task ApplyRuleAsync(SuggestionRule rule)
    {
        List<RuleEvaluator.Condition> conditions;
        try
        {
            conditions = JsonConvert.DeserializeObject<List<RuleEvaluator.Condition>>(rule.ConditionsJson) 
                         ?? new List<RuleEvaluator.Condition>();
        }
        catch
        {
            Console.WriteLine($"Failed to parse conditions for rule: {rule.Name}");
            return;
        }

        if (conditions.Count == 0)
        {
            Console.WriteLine($"Rule '{rule.Name}' has no conditions");
            return;
        }

        if (rule.ApplyToMovies)
        {
            await ApplyRuleToMoviesAsync(rule, conditions);
        }

        if (rule.ApplyToSeries)
        {
            await ApplyRuleToSeriesAsync(rule, conditions);
        }
    }

    private async Task ApplyRuleToMoviesAsync(SuggestionRule rule, List<RuleEvaluator.Condition> conditions)
    {
        var movies = await _db.Movies.ToListAsync();

        foreach (var movie in movies)
        {
            if (RuleEvaluator.EvaluateMovie(movie, conditions))
            {
                _db.Suggestions.Add(new Suggestion
                {
                    MediaType = "Movie",
                    MediaId = movie.Id,
                    MediaTitle = movie.Title,
                    MediaYear = movie.Year,
                    MediaSize = movie.SizeOnDisk,
                    PosterUrl = movie.PosterUrl,
                    RuleType = rule.Name,
                    Reason = rule.Description,
                    CreatedDate = DateTime.UtcNow
                });
            }
        }
    }

    private async Task ApplyRuleToSeriesAsync(SuggestionRule rule, List<RuleEvaluator.Condition> conditions)
    {
        var seriesList = await _db.Series.ToListAsync();

        foreach (var series in seriesList)
        {
            var episodes = await _db.Episodes.Where(e => e.SeriesId == series.Id).ToListAsync();

            if (RuleEvaluator.EvaluateSeries(series, episodes, conditions))
            {
                _db.Suggestions.Add(new Suggestion
                {
                    MediaType = "Series",
                    MediaId = series.Id,
                    MediaTitle = series.Title,
                    MediaYear = series.Year,
                    MediaSize = series.TotalSize,
                    PosterUrl = series.PosterUrl,
                    RuleType = rule.Name,
                    Reason = rule.Description,
                    CreatedDate = DateTime.UtcNow
                });
            }
        }
    }
}
