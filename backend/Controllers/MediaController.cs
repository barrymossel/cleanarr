using Cleanarr.Data;
using Cleanarr.Models;
using Cleanarr.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;

namespace Cleanarr.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MediaController : ControllerBase
{
    private readonly CleanarrDbContext _db;
    private readonly MediaSyncService _syncService;

    public MediaController(CleanarrDbContext db, MediaSyncService syncService)
    {
        _db = db;
        _syncService = syncService;
    }

    [HttpGet("movies")]
    public async Task<ActionResult<IEnumerable<Movie>>> GetMovies()
    {
        return await _db.Movies.OrderByDescending(m => m.Added).ToListAsync();
    }

    [HttpGet("series")]
    public async Task<ActionResult<IEnumerable<object>>> GetSeries()
    {
        var series = await _db.Series
            .OrderByDescending(s => s.Added)
            .ToListAsync();

        var result = new List<object>();
        foreach (var s in series)
        {
            var episodes = await _db.Episodes
                .Where(e => e.SeriesId == s.Id)
                .OrderBy(e => e.SeasonNumber)
                .ThenBy(e => e.EpisodeNumber)
                .ToListAsync();

            result.Add(new
            {
                s.Id,
                s.SonarrId,
                s.Title,
                s.Year,
                s.Added,
                s.RequestedDate,
                s.RequestedBy,
                s.TotalSize,
                s.Monitored,
                Episodes = episodes
            });
        }

        return Ok(result);
    }

    [HttpDelete("movie/{id}")]
    public async Task<IActionResult> DeleteMovie(int id)
    {
        var movie = await _db.Movies.FindAsync(id);
        if (movie == null)
            return NotFound();

        // Delete via Radarr API and get TMDb ID
        int? tmdbId = null;
        try
        {
            var radarrUrl = _syncService.GetConfig("RadarrUrl");
            var radarrApiKey = _syncService.GetConfig("RadarrApiKey");

            if (string.IsNullOrEmpty(radarrUrl) || string.IsNullOrEmpty(radarrApiKey))
                return BadRequest("Radarr not configured");

            var httpClient = new HttpClient();
            
            // Get movie details to extract TMDb ID
            var getRequest = new HttpRequestMessage(HttpMethod.Get, 
                $"{radarrUrl}/api/v3/movie/{movie.RadarrId}");
            getRequest.Headers.Add("X-Api-Key", radarrApiKey);
            var getResponse = await httpClient.SendAsync(getRequest);
            
            if (getResponse.IsSuccessStatusCode)
            {
                var movieJson = await getResponse.Content.ReadAsStringAsync();
                var movieData = JObject.Parse(movieJson);
                tmdbId = movieData["tmdbId"]?.Value<int>();
            }
            
            // Delete from Radarr
            var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, 
                $"{radarrUrl}/api/v3/movie/{movie.RadarrId}?deleteFiles=true");
            deleteRequest.Headers.Add("X-Api-Key", radarrApiKey);
            
            var deleteResponse = await httpClient.SendAsync(deleteRequest);
            deleteResponse.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            return BadRequest($"Error deleting movie from Radarr: {ex.Message}");
        }

        // Delete from Overseerr if we have TMDb ID
        if (tmdbId.HasValue)
        {
            await _syncService.DeleteOverseerrRequestByTmdbIdAsync(tmdbId.Value, "movie");
        }

        _db.Movies.Remove(movie);
        await _db.SaveChangesAsync();

        return Ok();
    }

    [HttpDelete("series/{id}")]
    public async Task<IActionResult> DeleteSeries(int id)
    {
        var series = await _db.Series.FindAsync(id);
        if (series == null)
            return NotFound();

        // Delete via Sonarr API and get TMDb ID
        int? tmdbId = null;
        try
        {
            var sonarrUrl = _syncService.GetConfig("SonarrUrl");
            var sonarrApiKey = _syncService.GetConfig("SonarrApiKey");

            if (string.IsNullOrEmpty(sonarrUrl) || string.IsNullOrEmpty(sonarrApiKey))
                return BadRequest("Sonarr not configured");

            var httpClient = new HttpClient();
            
            // Get series details to extract TMDb ID (Sonarr v4+ has tmdbId field)
            var getRequest = new HttpRequestMessage(HttpMethod.Get, 
                $"{sonarrUrl}/api/v3/series/{series.SonarrId}");
            getRequest.Headers.Add("X-Api-Key", sonarrApiKey);
            var getResponse = await httpClient.SendAsync(getRequest);
            
            if (getResponse.IsSuccessStatusCode)
            {
                var seriesJson = await getResponse.Content.ReadAsStringAsync();
                var seriesData = JObject.Parse(seriesJson);
                tmdbId = seriesData["tmdbId"]?.Value<int>();
                
                if (!tmdbId.HasValue)
                {
                    Console.WriteLine($"[DELETE] No TMDb ID found for series {series.Title}, checking TVDb ID");
                }
            }
            
            // Delete from Sonarr
            var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, 
                $"{sonarrUrl}/api/v3/series/{series.SonarrId}?deleteFiles=true");
            deleteRequest.Headers.Add("X-Api-Key", sonarrApiKey);
            
            var deleteResponse = await httpClient.SendAsync(deleteRequest);
            deleteResponse.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            return BadRequest($"Error deleting series from Sonarr: {ex.Message}");
        }

        // Delete from Overseerr if we have TMDb ID
        if (tmdbId.HasValue)
        {
            await _syncService.DeleteOverseerrRequestByTmdbIdAsync(tmdbId.Value, "tv");
        }
        else
        {
            Console.WriteLine($"[DELETE] No TMDb ID available for {series.Title}, cannot delete from Overseerr");
        }

        var episodes = await _db.Episodes.Where(e => e.SeriesId == id).ToListAsync();
        
        _db.Episodes.RemoveRange(episodes);
        _db.Series.Remove(series);
        await _db.SaveChangesAsync();

        return Ok();
    }

    [HttpDelete("episode/{id}")]
    public async Task<IActionResult> DeleteEpisode(int id)
    {
        var episode = await _db.Episodes.FindAsync(id);
        if (episode == null)
            return NotFound();

        // Delete via Sonarr API
        try
        {
            var sonarrUrl = _syncService.GetConfig("SonarrUrl");
            var sonarrApiKey = _syncService.GetConfig("SonarrApiKey");

            if (string.IsNullOrEmpty(sonarrUrl) || string.IsNullOrEmpty(sonarrApiKey))
                return BadRequest("Sonarr not configured");

            var httpClient = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Delete, 
                $"{sonarrUrl}/api/v3/episodefile/{episode.EpisodeFileId}");
            request.Headers.Add("X-Api-Key", sonarrApiKey);
            
            var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            return BadRequest($"Error deleting episode from Sonarr: {ex.Message}");
        }

        _db.Episodes.Remove(episode);
        await _db.SaveChangesAsync();

        return Ok();
    }

    [HttpPost("sync")]
    public async Task<IActionResult> TriggerSync()
    {
        await _syncService.SyncAllAsync();
        return Ok(new { message = "Sync started" });
    }
}
