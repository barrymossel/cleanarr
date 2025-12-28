# Cleanarr Changelog

## Version 1.1.0 - 2024-12-28

### New Features
- **Multi-Watcher Support**: Movies and episodes now track ALL viewers, not just the most recent one
  - Display format: "LastPerson 👥 3" (shows last watcher + total count badge)
  - Hover tooltip shows complete watch history with dates in Dutch format
  - Example: "John - 18 dec 2024\nSarah - 19 dec 2024\nMike - 20 dec 2024 ✓"
  - Most recent viewer marked with ✓

### Database Changes
- Added `WatchHistory` field to Movie and Episode models
- Stores JSON array: `[{"user":"John","date":"2024-12-18"},...]`
- Maintains `LastWatched` and `WatchedBy` for backwards compatibility

### Technical Updates
- Enhanced Tautulli sync to build complete watch history
- Deduplicates watches per user per day
- Automatic migration - existing data preserved

---

## Version 1.0.0 - 2024-12-28

### Initial Release Features
- **Version Display**: Settings page shows version number and build date
- **Favicon**: Custom favicon with broom + media icon
- **Multi-user watch tracking** (basic - single viewer)
- **Monitored/Unmonitored status badges** for movies and series
- **Sortable columns** in all tables
- **Sticky table headers** for better scrolling
- **Docker deployment** with PUID/PGID support

### Services Integration
- Radarr sync for movies
- Sonarr sync for series
- Tautulli sync for watch history
- Overseerr sync for requests

### Database
- SQLite database
- Auto-created schema
- JSON configuration file
