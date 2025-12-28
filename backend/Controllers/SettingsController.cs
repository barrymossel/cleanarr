using Cleanarr.Services;
using Microsoft.AspNetCore.Mvc;

namespace Cleanarr.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly ConfigService _config;

    public SettingsController(ConfigService config)
    {
        _config = config;
    }

    [HttpGet]
    public ActionResult<Dictionary<string, string>> GetSettings()
    {
        return Ok(_config.GetAll());
    }

    [HttpGet("version")]
    public ActionResult<object> GetVersion()
    {
        var versionFile = "/app/VERSION";
        var version = "unknown";
        var buildDate = "unknown";

        try
        {
            if (System.IO.File.Exists(versionFile))
            {
                version = System.IO.File.ReadAllText(versionFile).Trim();
            }
            
            // Get build date from assembly
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var buildDateAttr = assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                .FirstOrDefault() as System.Reflection.AssemblyInformationalVersionAttribute;
            
            // Fallback to file creation time if attribute not available
            if (System.IO.File.Exists(versionFile))
            {
                buildDate = System.IO.File.GetLastWriteTimeUtc(versionFile).ToString("yyyy-MM-dd HH:mm:ss UTC");
            }
        }
        catch
        {
            // If anything fails, just return unknown
        }

        return Ok(new { version, buildDate });
    }

    [HttpPost]
    public async Task<IActionResult> SaveSettings([FromBody] Dictionary<string, string> settings)
    {
        foreach (var setting in settings)
        {
            await _config.SetAsync(setting.Key, setting.Value);
        }

        return Ok(new { message = "Settings saved successfully" });
    }

    [HttpPost("test/{service}")]
    public async Task<IActionResult> TestConnection(string service)
    {
        var httpClient = new HttpClient();
        
        try
        {
            string url = "";
            string apiKey = "";

            switch (service.ToLower())
            {
                case "radarr":
                    url = await _config.GetAsync("RadarrUrl") ?? "";
                    apiKey = await _config.GetAsync("RadarrApiKey") ?? "";
                    if (!string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(apiKey))
                    {
                        var request = new HttpRequestMessage(HttpMethod.Get, $"{url}/api/v3/system/status");
                        request.Headers.Add("X-Api-Key", apiKey);
                        var response = await httpClient.SendAsync(request);
                        response.EnsureSuccessStatusCode();
                    }
                    break;

                case "sonarr":
                    url = await _config.GetAsync("SonarrUrl") ?? "";
                    apiKey = await _config.GetAsync("SonarrApiKey") ?? "";
                    if (!string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(apiKey))
                    {
                        var request = new HttpRequestMessage(HttpMethod.Get, $"{url}/api/v3/system/status");
                        request.Headers.Add("X-Api-Key", apiKey);
                        var response = await httpClient.SendAsync(request);
                        response.EnsureSuccessStatusCode();
                    }
                    break;

                case "tautulli":
                    url = await _config.GetAsync("TautulliUrl") ?? "";
                    apiKey = await _config.GetAsync("TautulliApiKey") ?? "";
                    if (!string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(apiKey))
                    {
                        var request = new HttpRequestMessage(HttpMethod.Get, 
                            $"{url}/api/v2?apikey={apiKey}&cmd=get_server_info");
                        var response = await httpClient.SendAsync(request);
                        response.EnsureSuccessStatusCode();
                    }
                    break;

                case "overseerr":
                    url = await _config.GetAsync("OverseerrUrl") ?? "";
                    apiKey = await _config.GetAsync("OverseerrApiKey") ?? "";
                    if (!string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(apiKey))
                    {
                        var request = new HttpRequestMessage(HttpMethod.Get, $"{url}/api/v1/settings/main");
                        request.Headers.Add("X-Api-Key", apiKey);
                        var response = await httpClient.SendAsync(request);
                        response.EnsureSuccessStatusCode();
                    }
                    break;

                default:
                    return BadRequest("Unknown service");
            }

            return Ok(new { message = "Connection successful" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = $"Connection failed: {ex.Message}" });
        }
    }
}
