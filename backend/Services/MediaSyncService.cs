using Cleanarr.Data;
using Cleanarr.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;

namespace Cleanarr.Services;

public class MediaSyncService
{
    private readonly CleanarrDbContext _db;
    private readonly ConfigService _config;
    private readonly HttpClient _httpClient;
    private readonly SuggestionService _suggestionService;

    public MediaSyncService(CleanarrDbContext db, ConfigService config, SuggestionService suggestionService)
    {
        _db = db;
        _config = config;
        _httpClient = new HttpClient();
        _suggestionService = suggestionService;
    }

    public string GetConfig(string key)
    {
        return _config.Get(key);
    }

    public async Task SyncAllAsync()
    {
        await SyncRadarrAsync();
        await SyncSonarrAsync();
        await SyncTautulliAsync();
        await SyncOverseerrAsync();
        
        // Generate suggestions after sync
        await _suggestionService.GenerateSuggestionsAsync();
    }

    private async Task SyncRadarrAsync()
    {
        var url = _config.Get("RadarrUrl");
        var apiKey = _config.Get("RadarrApiKey");
        
        if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(apiKey))
            return;

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{url}/api/v3/movie");
            request.Headers.Add("X-Api-Key", apiKey);
            
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            var movies = JArray.Parse(content);

            foreach (var movieData in movies)
            {
                var radarrId = movieData["id"]?.Value<int>() ?? 0;
                var movie = await _db.Movies.FirstOrDefaultAsync(m => m.RadarrId == radarrId);

                if (movie == null)
                {
                    movie = new Movie { RadarrId = radarrId };
                    _db.Movies.Add(movie);
                }

                movie.Title = movieData["title"]?.Value<string>() ?? "";
                movie.Year = movieData["year"]?.Value<int>() ?? 0;
                movie.SizeOnDisk = movieData["sizeOnDisk"]?.Value<long>() ?? 0;
                movie.Added = movieData["added"]?.Value<DateTime>() ?? DateTime.UtcNow;
                movie.FolderPath = movieData["path"]?.Value<string>() ?? "";
                movie.Monitored = movieData["monitored"]?.Value<bool>() ?? true;

                var qualityProfile = movieData["movieFile"]?["quality"]?["quality"]?["name"]?.Value<string>();
                movie.Quality = qualityProfile ?? "Unknown";
                
                // Extract poster URL
                var images = movieData["images"] as JArray;
                var poster = images?.FirstOrDefault(img => img["coverType"]?.Value<string>() == "poster");
                if (poster != null)
                {
                    var remoteUrl = poster["remoteUrl"]?.Value<string>();
                    if (!string.IsNullOrEmpty(remoteUrl))
                    {
                        movie.PosterUrl = remoteUrl;
                    }
                }
            }

            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Radarr sync error: {ex.Message}");
        }
    }

    private async Task SyncSonarrAsync()
    {
        var url = _config.Get("SonarrUrl");
        var apiKey = _config.Get("SonarrApiKey");
        
        if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(apiKey))
            return;

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{url}/api/v3/series");
            request.Headers.Add("X-Api-Key", apiKey);
            
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            var seriesList = JArray.Parse(content);

            foreach (var seriesData in seriesList)
            {
                var sonarrId = seriesData["id"]?.Value<int>() ?? 0;
                var series = await _db.Series.FirstOrDefaultAsync(s => s.SonarrId == sonarrId);

                if (series == null)
                {
                    series = new Series { SonarrId = sonarrId };
                    _db.Series.Add(series);
                }

                series.Title = seriesData["title"]?.Value<string>() ?? "";
                series.Year = seriesData["year"]?.Value<int>() ?? 0;
                series.Added = seriesData["added"]?.Value<DateTime>() ?? DateTime.UtcNow;
                series.TotalSize = seriesData["statistics"]?["sizeOnDisk"]?.Value<long>() ?? 0;
                series.Monitored = seriesData["monitored"]?.Value<bool>() ?? true;

                // Extract poster URL
                var images = seriesData["images"] as JArray;
                var poster = images?.FirstOrDefault(img => img["coverType"]?.Value<string>() == "poster");
                if (poster != null)
                {
                    var remoteUrl = poster["remoteUrl"]?.Value<string>();
                    if (!string.IsNullOrEmpty(remoteUrl))
                    {
                        series.PosterUrl = remoteUrl;
                    }
                }

                await _db.SaveChangesAsync();

                // Sync episodes for this series
                await SyncEpisodesForSeriesAsync(sonarrId, url, apiKey);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Sonarr sync error: {ex.Message}");
        }
    }

    private async Task SyncEpisodesForSeriesAsync(int seriesId, string url, string apiKey)
    {
        try
        {
            // Get episodes for this series
            var request = new HttpRequestMessage(HttpMethod.Get, $"{url}/api/v3/episode?seriesId={seriesId}");
            request.Headers.Add("X-Api-Key", apiKey);
            
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            var episodes = JArray.Parse(content);

            // Get ALL episode files for this series at once (more efficient)
            var episodeFilesRequest = new HttpRequestMessage(HttpMethod.Get, $"{url}/api/v3/episodefile?seriesId={seriesId}");
            episodeFilesRequest.Headers.Add("X-Api-Key", apiKey);
            
            var episodeFilesResponse = await _httpClient.SendAsync(episodeFilesRequest);
            episodeFilesResponse.EnsureSuccessStatusCode();
            
            var episodeFilesContent = await episodeFilesResponse.Content.ReadAsStringAsync();
            var episodeFiles = JArray.Parse(episodeFilesContent);
            
            // Create a dictionary for quick lookup: episodeFileId -> file data
            var episodeFileDict = episodeFiles.ToDictionary(
                ef => ef["id"]?.Value<int>() ?? 0,
                ef => ef
            );

            var dbSeries = await _db.Series.FirstOrDefaultAsync(s => s.SonarrId == seriesId);
            if (dbSeries == null) return;

            foreach (var episodeData in episodes)
            {
                // Only process episodes that have a file
                var hasFile = episodeData["hasFile"]?.Value<bool>() ?? false;
                if (!hasFile)
                    continue;

                var sonarrEpisodeId = episodeData["id"]?.Value<int>() ?? 0;
                var episodeFileId = episodeData["episodeFileId"]?.Value<int>() ?? 0;
                
                var episode = await _db.Episodes.FirstOrDefaultAsync(e => e.SonarrEpisodeId == sonarrEpisodeId);

                if (episode == null)
                {
                    episode = new Episode { SonarrEpisodeId = sonarrEpisodeId };
                    _db.Episodes.Add(episode);
                }

                episode.SeriesId = dbSeries.Id;
                episode.Title = episodeData["title"]?.Value<string>() ?? "";
                episode.SeasonNumber = episodeData["seasonNumber"]?.Value<int>() ?? 0;
                episode.EpisodeNumber = episodeData["episodeNumber"]?.Value<int>() ?? 0;
                episode.AirDate = episodeData["airDateUtc"]?.Value<DateTime>();
                episode.EpisodeFileId = episodeFileId;
                
                // Get file data from our dictionary
                if (episodeFileId > 0 && episodeFileDict.ContainsKey(episodeFileId))
                {
                    var fileData = episodeFileDict[episodeFileId];
                    
                    episode.SizeOnDisk = fileData["size"]?.Value<long>() ?? 0;
                    episode.FilePath = fileData["path"]?.Value<string>() ?? "";
                    
                    var qualityObj = fileData["quality"]?["quality"];
                    episode.Quality = qualityObj?["name"]?.Value<string>() ?? "Unknown";
                }
                else
                {
                    Console.WriteLine($"Warning: Episode {episode.Title} (S{episode.SeasonNumber}E{episode.EpisodeNumber}) has episodeFileId {episodeFileId} but no file data found");
                    episode.SizeOnDisk = 0;
                    episode.FilePath = "";
                    episode.Quality = "Unknown";
                }
            }

            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Episodes sync error for series {seriesId}: {ex.Message}");
        }
    }

    private async Task SyncTautulliAsync()
    {
        var url = _config.Get("TautulliUrl");
        var apiKey = _config.Get("TautulliApiKey");
        
        if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(apiKey))
            return;

        try
        {
            // Get history from Tautulli
            var request = new HttpRequestMessage(HttpMethod.Get, 
                $"{url}/api/v2?apikey={apiKey}&cmd=get_history&length=1000");
            
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            var data = JObject.Parse(content);
            var history = data["response"]?["data"]?["data"] as JArray;

            if (history == null) return;

            // Build watch history maps: key -> list of (user, date)
            var movieWatches = new Dictionary<string, List<(string user, DateTime date)>>();
            var episodeWatches = new Dictionary<string, List<(string user, DateTime date)>>();

            foreach (var item in history)
            {
                var mediaType = item["media_type"]?.Value<string>();
                var title = item["title"]?.Value<string>();
                var user = item["user"]?.Value<string>();
                var watchedDate = item["stopped"]?.Value<long>();

                if (watchedDate == null || string.IsNullOrEmpty(user)) continue;
                var watched = DateTimeOffset.FromUnixTimeSeconds(watchedDate.Value).DateTime;

                if (mediaType == "movie" && !string.IsNullOrEmpty(title))
                {
                    if (!movieWatches.ContainsKey(title))
                        movieWatches[title] = new List<(string, DateTime)>();
                    movieWatches[title].Add((user, watched));
                }
                else if (mediaType == "episode")
                {
                    var seasonNum = item["parent_media_index"]?.Value<int>();
                    var episodeNum = item["media_index"]?.Value<int>();
                    var grandparentTitle = item["grandparent_title"]?.Value<string>();
                    
                    if (!string.IsNullOrEmpty(grandparentTitle) && seasonNum.HasValue && episodeNum.HasValue)
                    {
                        var key = $"{grandparentTitle}|S{seasonNum}E{episodeNum}";
                        if (!episodeWatches.ContainsKey(key))
                            episodeWatches[key] = new List<(string, DateTime)>();
                        episodeWatches[key].Add((user, watched));
                    }
                }
            }

            // Update movies with watch history
            foreach (var kvp in movieWatches)
            {
                var movie = await _db.Movies.FirstOrDefaultAsync(m => m.Title == kvp.Key);
                if (movie != null)
                {
                    // Build unique watch history (user + date)
                    var uniqueWatches = kvp.Value
                        .GroupBy(w => new { w.user, Date = w.date.Date })
                        .Select(g => g.OrderByDescending(w => w.date).First())
                        .OrderBy(w => w.date)
                        .ToList();

                    // Create JSON array
                    var watchHistoryJson = new JArray();
                    foreach (var watch in uniqueWatches)
                    {
                        watchHistoryJson.Add(new JObject
                        {
                            ["user"] = watch.user,
                            ["date"] = watch.date.ToString("yyyy-MM-dd")
                        });
                    }
                    movie.WatchHistory = watchHistoryJson.ToString(Newtonsoft.Json.Formatting.None);

                    // Set last watched (most recent)
                    var mostRecent = uniqueWatches.LastOrDefault();
                    if (mostRecent != default)
                    {
                        movie.LastWatched = mostRecent.date;
                        movie.WatchedBy = mostRecent.user;
                    }
                }
            }

            // Update episodes with watch history
            foreach (var kvp in episodeWatches)
            {
                var parts = kvp.Key.Split('|');
                var seriesTitle = parts[0];
                var episodeKey = parts[1]; // S1E1 format
                
                var seasonMatch = System.Text.RegularExpressions.Regex.Match(episodeKey, @"S(\d+)E(\d+)");
                if (!seasonMatch.Success) continue;
                
                var seasonNum = int.Parse(seasonMatch.Groups[1].Value);
                var episodeNum = int.Parse(seasonMatch.Groups[2].Value);

                var series = await _db.Series.FirstOrDefaultAsync(s => s.Title == seriesTitle);
                if (series != null)
                {
                    var episode = await _db.Episodes.FirstOrDefaultAsync(e => 
                        e.SeriesId == series.Id && 
                        e.SeasonNumber == seasonNum && 
                        e.EpisodeNumber == episodeNum);

                    if (episode != null)
                    {
                        // Build unique watch history
                        var uniqueWatches = kvp.Value
                            .GroupBy(w => new { w.user, Date = w.date.Date })
                            .Select(g => g.OrderByDescending(w => w.date).First())
                            .OrderBy(w => w.date)
                            .ToList();

                        // Create JSON array
                        var watchHistoryJson = new JArray();
                        foreach (var watch in uniqueWatches)
                        {
                            watchHistoryJson.Add(new JObject
                            {
                                ["user"] = watch.user,
                                ["date"] = watch.date.ToString("yyyy-MM-dd")
                            });
                        }
                        episode.WatchHistory = watchHistoryJson.ToString(Newtonsoft.Json.Formatting.None);

                        // Set last watched (most recent)
                        var mostRecent = uniqueWatches.LastOrDefault();
                        if (mostRecent != default)
                        {
                            episode.LastWatched = mostRecent.date;
                            episode.WatchedBy = mostRecent.user;
                        }
                    }
                }
            }

            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Tautulli sync error: {ex.Message}");
        }
    }

    private async Task SyncOverseerrAsync()
    {
        var url = _config.Get("OverseerrUrl");
        var apiKey = _config.Get("OverseerrApiKey");
        
        if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("[OVERSEERR DEBUG] Overseerr URL or API Key not configured");
            return;
        }

        try
        {
            Console.WriteLine("[OVERSEERR DEBUG] Starting Overseerr sync...");
            var request = new HttpRequestMessage(HttpMethod.Get, $"{url}/api/v1/request?take=100&sort=added");
            request.Headers.Add("X-Api-Key", apiKey);
            
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[OVERSEERR DEBUG] API Response length: {content.Length} chars");
            
            var data = JObject.Parse(content);
            var requests = data["results"] as JArray;

            if (requests == null)
            {
                Console.WriteLine("[OVERSEERR DEBUG] No results found in response");
                return;
            }

            Console.WriteLine($"[OVERSEERR DEBUG] Found {requests.Count} requests");

            int processedCount = 0;
            int matchedCount = 0;
            
            foreach (var req in requests)
            {
                processedCount++;
                var mediaType = req["media"]?["mediaType"]?.Value<string>();
                var requestedBy = req["requestedBy"]?["displayName"]?.Value<string>();
                var createdAt = req["createdAt"]?.Value<DateTime>();
                var title = req["media"]?["title"]?.Value<string>() ?? req["media"]?["name"]?.Value<string>();
                var requestId = req["id"]?.Value<int>() ?? 0;
                
                Console.WriteLine($"\n[OVERSEERR DEBUG] Request #{processedCount}: {title} ({mediaType})");
                Console.WriteLine($"  Requested by: {requestedBy}, Created: {createdAt}");
                Console.WriteLine($"  Request ID: {requestId}");
                
                // Get the external service info (Radarr/Sonarr IDs)
                // Note: externalServiceId is the Radarr/Sonarr ID we need
                // externalServiceSlug is actually the TMDb ID (not a slug!)
                var externalServiceId = req["media"]?["externalServiceId"]?.Value<int?>();
                var serviceId = req["media"]?["serviceId"]?.Value<int?>();
                var serviceId4k = req["media"]?["serviceId4k"]?.Value<int?>();
                
                Console.WriteLine($"  externalServiceId: {externalServiceId}");
                Console.WriteLine($"  serviceId: {serviceId}, serviceId4k: {serviceId4k}");

                if (mediaType == "movie")
                {
                    // Use externalServiceId (Radarr ID) for matching
                    var actualServiceId = externalServiceId ?? serviceId ?? serviceId4k;
                    
                    if (actualServiceId.HasValue)
                    {
                        Console.WriteLine($"  Looking for movie with RadarrId: {actualServiceId.Value}");
                        var movie = await _db.Movies.FirstOrDefaultAsync(m => m.RadarrId == actualServiceId.Value);
                        
                        if (movie != null)
                        {
                            Console.WriteLine($"  ✓ Found movie: {movie.Title} (ID: {movie.RadarrId})");
                            if (movie.RequestedBy == null)
                            {
                                movie.RequestedBy = requestedBy;
                                movie.RequestedDate = createdAt;
                                matchedCount++;
                                Console.WriteLine($"  ✓ Updated request info");
                            }
                            else
                            {
                                Console.WriteLine($"  ℹ Already has request info: {movie.RequestedBy}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"  ✗ No movie found with RadarrId: {actualServiceId.Value}");
                            // Show all movie IDs we have
                            var allMovieIds = await _db.Movies.Select(m => m.RadarrId).ToListAsync();
                            Console.WriteLine($"  Available RadarrIds in DB: {string.Join(", ", allMovieIds.Take(10))}...");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"  ✗ No service ID found for this movie");
                    }
                }
                else if (mediaType == "tv")
                {
                    // Use externalServiceId (Sonarr ID) for matching
                    var actualServiceId = externalServiceId ?? serviceId ?? serviceId4k;
                    
                    if (actualServiceId.HasValue)
                    {
                        Console.WriteLine($"  Looking for series with SonarrId: {actualServiceId.Value}");
                        var series = await _db.Series.FirstOrDefaultAsync(s => s.SonarrId == actualServiceId.Value);
                        
                        if (series != null)
                        {
                            Console.WriteLine($"  ✓ Found series: {series.Title} (ID: {series.SonarrId})");
                            if (series.RequestedBy == null)
                            {
                                series.RequestedBy = requestedBy;
                                series.RequestedDate = createdAt;
                                matchedCount++;
                                Console.WriteLine($"  ✓ Updated request info");
                            }
                            else
                            {
                                Console.WriteLine($"  ℹ Already has request info: {series.RequestedBy}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"  ✗ No series found with SonarrId: {actualServiceId.Value}");
                            // Show all series IDs we have
                            var allSeriesIds = await _db.Series.Select(s => s.SonarrId).ToListAsync();
                            Console.WriteLine($"  Available SonarrIds in DB: {string.Join(", ", allSeriesIds.Take(10))}...");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"  ✗ No service ID found for this series");
                    }
                }
            }

            await _db.SaveChangesAsync();
            Console.WriteLine($"\n[OVERSEERR DEBUG] Sync complete: {processedCount} processed, {matchedCount} matched");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OVERSEERR DEBUG] ERROR: {ex.Message}");
            Console.WriteLine($"[OVERSEERR DEBUG] Stack trace: {ex.StackTrace}");
        }
    }

    public async Task<bool> DeleteOverseerrRequestByTmdbIdAsync(int tmdbId, string mediaType)
    {
        try
        {
            var overseerrUrl = GetConfig("OverseerrUrl");
            var overseerrApiKey = GetConfig("OverseerrApiKey");

            if (string.IsNullOrEmpty(overseerrUrl) || string.IsNullOrEmpty(overseerrApiKey))
            {
                Console.WriteLine("[OVERSEERR DELETE] Overseerr not configured, skipping");
                return false;
            }

            var httpClient = new HttpClient();
            
            // Search for the request by TMDb ID
            var searchRequest = new HttpRequestMessage(HttpMethod.Get, 
                $"{overseerrUrl}/api/v1/request?filter=all&take=100&skip=0");
            searchRequest.Headers.Add("X-Api-Key", overseerrApiKey);
            
            var searchResponse = await httpClient.SendAsync(searchRequest);
            if (!searchResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"[OVERSEERR DELETE] Failed to search requests: {searchResponse.StatusCode}");
                return false;
            }

            var searchJson = await searchResponse.Content.ReadAsStringAsync();
            var searchData = JObject.Parse(searchJson);
            var requests = searchData["results"] as JArray;

            if (requests == null || requests.Count == 0)
            {
                Console.WriteLine("[OVERSEERR DELETE] No requests found in Overseerr");
                return false;
            }

            // Find matching request by TMDb ID and media type
            foreach (var req in requests)
            {
                var reqMediaType = req["media"]?["mediaType"]?.Value<string>();
                var reqTmdbId = req["media"]?["tmdbId"]?.Value<int>();
                var requestId = req["id"]?.Value<int>();

                if (reqMediaType == mediaType && reqTmdbId == tmdbId && requestId.HasValue)
                {
                    Console.WriteLine($"[OVERSEERR DELETE] Found matching request ID {requestId.Value} for TMDb ID {tmdbId}");
                    
                    // Delete the request
                    var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, 
                        $"{overseerrUrl}/api/v1/request/{requestId.Value}");
                    deleteRequest.Headers.Add("X-Api-Key", overseerrApiKey);
                    
                    var deleteResponse = await httpClient.SendAsync(deleteRequest);
                    
                    if (deleteResponse.IsSuccessStatusCode || deleteResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        Console.WriteLine($"[OVERSEERR DELETE] Successfully deleted request {requestId.Value}");
                        return true;
                    }
                    else
                    {
                        Console.WriteLine($"[OVERSEERR DELETE] Failed to delete request {requestId.Value}: {deleteResponse.StatusCode}");
                        return false;
                    }
                }
            }

            Console.WriteLine($"[OVERSEERR DELETE] No matching request found for TMDb ID {tmdbId} ({mediaType})");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OVERSEERR DELETE] Error: {ex.Message}");
            return false;
        }
    }
}
