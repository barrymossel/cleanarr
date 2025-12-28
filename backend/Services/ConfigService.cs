using System.Text.Json;

namespace Cleanarr.Services;

public class ConfigService
{
    private readonly string _configPath;
    private readonly string _settingsFile;
    private Dictionary<string, string> _settings = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public ConfigService()
    {
        _configPath = Environment.GetEnvironmentVariable("CONFIG_PATH") ?? "/config";
        _settingsFile = Path.Combine(_configPath, "settings.json");
        
        Directory.CreateDirectory(_configPath);
        LoadSettings();
    }

    private void LoadSettings()
    {
        if (File.Exists(_settingsFile))
        {
            try
            {
                var json = File.ReadAllText(_settingsFile);
                _settings = JsonSerializer.Deserialize<Dictionary<string, string>>(json) 
                    ?? new Dictionary<string, string>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading settings: {ex.Message}");
                _settings = new Dictionary<string, string>();
            }
        }
        else
        {
            // Create default settings file
            _settings = new Dictionary<string, string>
            {
                { "RadarrUrl", "" },
                { "RadarrApiKey", "" },
                { "SonarrUrl", "" },
                { "SonarrApiKey", "" },
                { "TautulliUrl", "" },
                { "TautulliApiKey", "" },
                { "OverseerrUrl", "" },
                { "OverseerrApiKey", "" },
                { "CronSchedule", "0 */6 * * *" }
            };
            SaveSettings();
        }
    }

    private void SaveSettings()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            File.WriteAllText(_settingsFile, json);
            
            // Set file permissions (readable/writable by owner and group)
            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                try
                {
#pragma warning disable CA1416
                    // chmod 664
                    File.SetUnixFileMode(_settingsFile, 
                        UnixFileMode.UserRead | UnixFileMode.UserWrite | 
                        UnixFileMode.GroupRead | UnixFileMode.GroupWrite | 
                        UnixFileMode.OtherRead);
#pragma warning restore CA1416
                }
                catch { /* Ignore permission errors */ }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving settings: {ex.Message}");
        }
    }

    public async Task<string?> GetAsync(string key)
    {
        await _lock.WaitAsync();
        try
        {
            return _settings.TryGetValue(key, out var value) ? value : null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SetAsync(string key, string value)
    {
        await _lock.WaitAsync();
        try
        {
            _settings[key] = value;
            SaveSettings();
        }
        finally
        {
            _lock.Release();
        }
    }

    public string Get(string key, string defaultValue = "")
    {
        return _settings.TryGetValue(key, out var value) ? value : defaultValue;
    }

    public Dictionary<string, string> GetAll()
    {
        return new Dictionary<string, string>(_settings);
    }
}
