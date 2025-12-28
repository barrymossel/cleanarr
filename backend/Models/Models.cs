using System.ComponentModel.DataAnnotations;

namespace Cleanarr.Models;

public class Movie
{
    [Key]
    public int Id { get; set; }
    public int RadarrId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Year { get; set; }
    public string Quality { get; set; } = string.Empty;
    public long SizeOnDisk { get; set; }
    public DateTime Added { get; set; }
    public DateTime? RequestedDate { get; set; }
    public string? RequestedBy { get; set; }
    public DateTime? LastWatched { get; set; }
    public string? WatchedBy { get; set; }
    public string? WatchHistory { get; set; }  // JSON: [{"user":"John","date":"2024-12-18"},...]
    public string FolderPath { get; set; } = string.Empty;
    public bool Monitored { get; set; } = true;
    public string? PosterUrl { get; set; }
}

public class Series
{
    [Key]
    public int Id { get; set; }
    public int SonarrId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Year { get; set; }
    public DateTime Added { get; set; }
    public DateTime? RequestedDate { get; set; }
    public string? RequestedBy { get; set; }
    public long TotalSize { get; set; }
    public bool Monitored { get; set; } = true;
    public string? PosterUrl { get; set; }
}

public class Episode
{
    [Key]
    public int Id { get; set; }
    public int SeriesId { get; set; }
    public int SonarrEpisodeId { get; set; }
    public int EpisodeFileId { get; set; }
    public int SeasonNumber { get; set; }
    public int EpisodeNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Quality { get; set; } = string.Empty;
    public long SizeOnDisk { get; set; }
    public DateTime? AirDate { get; set; }
    public DateTime? LastWatched { get; set; }
    public string? WatchedBy { get; set; }
    public string? WatchHistory { get; set; }  // JSON: [{"user":"John","date":"2024-12-18"},...]
    public string FilePath { get; set; } = string.Empty;
}

public class SuggestionRule
{
    [Key]
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public bool ApplyToMovies { get; set; } = true;
    public bool ApplyToSeries { get; set; } = true;
    public string ConditionsJson { get; set; } = "[]"; // JSON array of conditions
    public bool IsCustom { get; set; } = false; // true for user-created rules
}

// Condition structure (stored as JSON):
// {
//   "field": "lastWatched",           // Field to check
//   "operator": "before",              // Comparison operator
//   "value": "180",                    // Value to compare against
//   "valueType": "customDays",         // Type: customDays, customDate, customNumber, customText, etc.
//   "logicalOperator": "AND"           // AND or OR (null for last condition)
// }

public class Suggestion
{
    [Key]
    public int Id { get; set; }
    public string MediaType { get; set; } = string.Empty; // "Movie" or "Series"
    public int MediaId { get; set; }
    public string MediaTitle { get; set; } = string.Empty;
    public int? MediaYear { get; set; }
    public long MediaSize { get; set; }
    public string? PosterUrl { get; set; }
    public string RuleType { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public bool Dismissed { get; set; } = false;
    public DateTime CreatedDate { get; set; }
}
