using Cleanarr.Models;
using Newtonsoft.Json.Linq;

namespace Cleanarr.Services;

public class RuleEvaluator
{
    public class Condition
    {
        public string Field { get; set; } = string.Empty;
        public string Operator { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string ValueType { get; set; } = string.Empty;
        public string? LogicalOperator { get; set; }
    }

    public static bool EvaluateMovie(Movie movie, List<Condition> conditions)
    {
        if (conditions.Count == 0) return false;

        bool result = EvaluateCondition(GetMovieFieldValue(movie, conditions[0].Field), conditions[0]);

        for (int i = 1; i < conditions.Count; i++)
        {
            var condition = conditions[i];
            var conditionResult = EvaluateCondition(GetMovieFieldValue(movie, condition.Field), condition);

            // Previous condition's logical operator determines how to combine
            var logicalOp = conditions[i - 1].LogicalOperator;
            if (logicalOp == "AND")
            {
                result = result && conditionResult;
            }
            else if (logicalOp == "OR")
            {
                result = result || conditionResult;
            }
        }

        return result;
    }

    public static bool EvaluateSeries(Series series, List<Episode> episodes, List<Condition> conditions)
    {
        if (conditions.Count == 0) return false;

        bool result = EvaluateCondition(GetSeriesFieldValue(series, episodes, conditions[0].Field), conditions[0]);

        for (int i = 1; i < conditions.Count; i++)
        {
            var condition = conditions[i];
            var conditionResult = EvaluateCondition(GetSeriesFieldValue(series, episodes, condition.Field), condition);

            var logicalOp = conditions[i - 1].LogicalOperator;
            if (logicalOp == "AND")
            {
                result = result && conditionResult;
            }
            else if (logicalOp == "OR")
            {
                result = result || conditionResult;
            }
        }

        return result;
    }

    private static object? GetMovieFieldValue(Movie movie, string field)
    {
        return field switch
        {
            "lastWatched" => movie.LastWatched,
            "added" => movie.Added,
            "requestedDate" => movie.RequestedDate,
            "requestedBy" => movie.RequestedBy,
            "watchedBy" => movie.WatchedBy,
            "sizeOnDisk" => movie.SizeOnDisk,
            "year" => movie.Year,
            "monitored" => movie.Monitored,
            "watchCount" => GetWatchCount(movie.WatchHistory),
            "title" => movie.Title,
            "quality" => movie.Quality,
            _ => null
        };
    }

    private static object? GetSeriesFieldValue(Series series, List<Episode> episodes, string field)
    {
        return field switch
        {
            "lastWatched" => episodes.OrderByDescending(e => e.LastWatched).FirstOrDefault()?.LastWatched,
            "added" => series.Added,
            "requestedDate" => series.RequestedDate,
            "requestedBy" => series.RequestedBy,
            "totalSize" => series.TotalSize,
            "year" => series.Year,
            "monitored" => series.Monitored,
            "watchCount" => GetSeriesWatchCount(episodes),
            "title" => series.Title,
            "episodeCount" => episodes.Count,
            _ => null
        };
    }

    private static int GetWatchCount(string? watchHistoryJson)
    {
        if (string.IsNullOrEmpty(watchHistoryJson)) return 0;
        try
        {
            var history = JArray.Parse(watchHistoryJson);
            return history.Select(h => h["user"]?.Value<string>()).Distinct().Count();
        }
        catch
        {
            return 0;
        }
    }

    private static int GetSeriesWatchCount(List<Episode> episodes)
    {
        var allUsers = new HashSet<string>();
        foreach (var episode in episodes)
        {
            if (!string.IsNullOrEmpty(episode.WatchHistory))
            {
                try
                {
                    var history = JArray.Parse(episode.WatchHistory);
                    foreach (var watch in history)
                    {
                        var user = watch["user"]?.Value<string>();
                        if (!string.IsNullOrEmpty(user))
                        {
                            allUsers.Add(user);
                        }
                    }
                }
                catch { }
            }
        }
        return allUsers.Count;
    }

    private static bool EvaluateCondition(object? fieldValue, Condition condition)
    {
        var compareValue = GetCompareValue(condition.Value, condition.ValueType);

        return condition.Operator switch
        {
            "bigger" => CompareBigger(fieldValue, compareValue),
            "smaller" => CompareSmaller(fieldValue, compareValue),
            "equals" => CompareEquals(fieldValue, compareValue, condition.Value),
            "not_equals" => !CompareEquals(fieldValue, compareValue, condition.Value),
            "contains" => CompareContains(fieldValue, compareValue, exact: true),
            "contains_partial" => CompareContains(fieldValue, compareValue, exact: false),
            "not_contains" => !CompareContains(fieldValue, compareValue, exact: true),
            "not_contains_partial" => !CompareContains(fieldValue, compareValue, exact: false),
            "before" => CompareBefore(fieldValue, compareValue),
            "after" => CompareAfter(fieldValue, compareValue),
            "in_last" => CompareInLast(fieldValue, compareValue),
            "in_next" => CompareInNext(fieldValue, compareValue),
            _ => false
        };
    }

    private static object? GetCompareValue(string value, string valueType)
    {
        return valueType switch
        {
            "customDays" => int.TryParse(value, out var days) ? days : 0,
            "customNumber" => double.TryParse(value, out var num) ? num : 0.0,
            "customDate" => DateTime.TryParse(value, out var date) ? date : DateTime.MinValue,
            "customText" => value,
            "boolean" => bool.TryParse(value, out var boolVal) ? boolVal : false,
            "null" => null,
            _ => value
        };
    }

    private static bool CompareBigger(object? fieldValue, object? compareValue)
    {
        if (fieldValue == null || compareValue == null) return false;
        
        if (fieldValue is long longVal && compareValue is double doubleVal)
            return longVal > doubleVal;
        if (fieldValue is int intVal && compareValue is double doubleVal2)
            return intVal > doubleVal2;
        if (fieldValue is double dVal1 && compareValue is double dVal2)
            return dVal1 > dVal2;
            
        return false;
    }

    private static bool CompareSmaller(object? fieldValue, object? compareValue)
    {
        if (fieldValue == null || compareValue == null) return false;
        
        if (fieldValue is long longVal && compareValue is double doubleVal)
            return longVal < doubleVal;
        if (fieldValue is int intVal && compareValue is double doubleVal2)
            return intVal < doubleVal2;
        if (fieldValue is double dVal1 && compareValue is double dVal2)
            return dVal1 < dVal2;
            
        return false;
    }

    private static bool CompareEquals(object? fieldValue, object? compareValue, string originalValue)
    {
        // Handle null comparison
        if (originalValue == "null")
            return fieldValue == null;
            
        if (fieldValue == null && compareValue == null) return true;
        if (fieldValue == null || compareValue == null) return false;
        
        if (fieldValue is bool boolVal && compareValue is bool boolCompare)
            return boolVal == boolCompare;
            
        return fieldValue.ToString() == compareValue.ToString();
    }

    private static bool CompareContains(object? fieldValue, object? compareValue, bool exact)
    {
        if (fieldValue == null || compareValue == null) return false;
        
        var fieldStr = fieldValue.ToString() ?? "";
        var compareStr = compareValue.ToString() ?? "";
        
        if (exact)
            return fieldStr.Equals(compareStr, StringComparison.OrdinalIgnoreCase);
        else
            return fieldStr.Contains(compareStr, StringComparison.OrdinalIgnoreCase);
    }

    private static bool CompareBefore(object? fieldValue, object? compareValue)
    {
        if (fieldValue is not DateTime dateField) return false;
        
        DateTime compareDate;
        if (compareValue is int days)
        {
            compareDate = DateTime.UtcNow.AddDays(-days);
        }
        else if (compareValue is DateTime dt)
        {
            compareDate = dt;
        }
        else
        {
            return false;
        }
        
        return dateField < compareDate;
    }

    private static bool CompareAfter(object? fieldValue, object? compareValue)
    {
        if (fieldValue is not DateTime dateField) return false;
        
        DateTime compareDate;
        if (compareValue is int days)
        {
            compareDate = DateTime.UtcNow.AddDays(days);
        }
        else if (compareValue is DateTime dt)
        {
            compareDate = dt;
        }
        else
        {
            return false;
        }
        
        return dateField > compareDate;
    }

    private static bool CompareInLast(object? fieldValue, object? compareValue)
    {
        if (fieldValue is not DateTime dateField) return false;
        if (compareValue is not int days) return false;
        
        var cutoffDate = DateTime.UtcNow.AddDays(-days);
        return dateField >= cutoffDate && dateField <= DateTime.UtcNow;
    }

    private static bool CompareInNext(object? fieldValue, object? compareValue)
    {
        if (fieldValue is not DateTime dateField) return false;
        if (compareValue is not int days) return false;
        
        var cutoffDate = DateTime.UtcNow.AddDays(days);
        return dateField >= DateTime.UtcNow && dateField <= cutoffDate;
    }
}
