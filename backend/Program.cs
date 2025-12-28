using Cleanarr.Data;
using Cleanarr.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database
var configPath = Environment.GetEnvironmentVariable("CONFIG_PATH") ?? "/config";
Directory.CreateDirectory(configPath);
var dbPath = Path.Combine(configPath, "cleanarr.db");

builder.Services.AddDbContext<CleanarrDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// Services
builder.Services.AddSingleton<ConfigService>();
builder.Services.AddScoped<MediaSyncService>();
builder.Services.AddScoped<SuggestionService>();
builder.Services.AddHostedService<CronJobService>();

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Ensure database exists (no migrations needed, data comes from APIs)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CleanarrDbContext>();
    db.Database.EnsureCreated();
    
    // Set database file permissions if on Unix
    if (Environment.OSVersion.Platform == PlatformID.Unix && File.Exists(dbPath))
    {
        try
        {
#pragma warning disable CA1416
            // chmod 664
            File.SetUnixFileMode(dbPath, 
                UnixFileMode.UserRead | UnixFileMode.UserWrite | 
                UnixFileMode.GroupRead | UnixFileMode.GroupWrite | 
                UnixFileMode.OtherRead);
#pragma warning restore CA1416
        }
        catch { /* Ignore permission errors */ }
    }
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();
app.UseStaticFiles();
app.UseRouting();

app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
