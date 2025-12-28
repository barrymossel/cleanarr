# Cleanarr Update - Monitored Status Feature

## 🎯 Wat is Nieuw

### ✨ Monitored/Unmonitored Status Kolom

Zie nu in één oogopslag de status van je movies en series:

- **✅ Available** - Downloaded en monitored
- **⏳ Pending** - 0KB, monitored (wacht op download)
- **⚠️ Missing** - 0KB, unmonitored (veilig te verwijderen)
- **🔕 Unmonitored** - Downloaded maar niet meer monitored

### 📋 Voordelen

1. **Duidelijk onderscheid** tussen:
   - Films die nog moeten downloaden (Pending)
   - Films die missing zijn (Missing - safe to delete)
   
2. **Betere beslissingen** bij verwijderen:
   - ⚠️ Missing = veilig verwijderen
   - ⏳ Pending = wacht op download, niet verwijderen!

3. **Sync van Radarr/Sonarr**:
   - Monitored status wordt automatisch opgehaald

## 🚀 Deployment

### 1. Stop huidige container
```bash
docker stop cleanarr
```

### 2. Verwijder oude image (BELANGRIJK!)
```bash
docker rmi cleanarr:latest
```

### 3. Verwijder database
```bash
# Database moet opnieuw aangemaakt worden voor nieuwe kolom
rm /volume1/docker/cleanarr/cleanarr.db

# Settings blijven bewaard in settings.json!
```

### 4. Unzip nieuwe versie
```bash
cd /pad/naar/cleanarr
rm -rf backend frontend Dockerfile docker-entrypoint.sh
unzip cleanarr.zip
```

### 5. Build nieuwe image
```bash
docker build -t cleanarr:latest .
```

### 6. Start container
```bash
docker-compose up -d
```

### 7. Sync uitvoeren
Open http://your-server:7979 en klik op "Sync Now" om data op te halen.

## 📊 Status Badges Uitleg

### Movies/Series

| Badge | Betekenis | Safe to Delete? |
|-------|-----------|-----------------|
| ✅ Available | Downloaded + monitored | ❌ Nee |
| ⏳ Pending | 0KB + monitored | ❌ Nee - wacht op download |
| ⚠️ Missing | 0KB + unmonitored | ✅ Ja - veilig te verwijderen |
| 🔕 Unmonitored | Downloaded + unmonitored | ⚠️ Wellicht - controleer eerst |

### Wanneer is iets "Missing"?

Een film/serie is **Missing** wanneer:
- ❌ Geen bestanden op schijf (0KB)
- 🔕 Unmonitored in Radarr/Sonarr
- 📁 Lege map staat nog op schijf

→ **Veilig om via Cleanarr te verwijderen!**

### Wanneer is iets "Pending"?

Een film/serie is **Pending** wanneer:
- ❌ Nog niet gedownload (0KB)
- ✅ WEL monitored in Radarr/Sonarr
- ⏰ Wacht op release of beschikbaarheid

→ **NIET verwijderen - download komt nog!**

## 🔧 Database Wijzigingen

### Nieuwe Kolommen

**Movies tabel:**
- `Monitored` (boolean, default: true)

**Series tabel:**
- `Monitored` (boolean, default: true)

### Waarom database verwijderen?

Bij gebruik van `EnsureCreated()` moet de database opnieuw aangemaakt worden om nieuwe kolommen toe te voegen. Geen probleem want:
- ✅ Settings blijven bewaard in `settings.json`
- ✅ Data wordt opnieuw opgehaald uit APIs bij sync

## ❓ Veelgestelde Vragen

**Q: Blijven mijn settings bewaard?**  
A: Ja! Settings zitten in `/config/settings.json` en blijven staan.

**Q: Moet ik alles opnieuw configureren?**  
A: Nee, alleen database verwijderen en opnieuw syncen.

**Q: Waarom moet ik de image verwijderen?**  
A: Docker cached layers. `docker rmi` forceert een clean rebuild.

**Q: Wat als ik per ongeluk een "Pending" film verwijder?**  
A: Request hem opnieuw in Overseerr - de Overseerr request wordt ook verwijderd bij delete.

**Q: Werkt dit voor oude data?**  
A: Ja, na sync krijgt alles de juiste monitored status.

## 📝 Changelog

### Version met Monitored Status (2024-12-18)

**Added:**
- ✨ Monitored status kolom in Movies tabel
- ✨ Monitored status kolom in Series tabel
- ✨ Status badges in UI (Available/Pending/Missing/Unmonitored)
- 🎨 Badge styling in CSS

**Changed:**
- 📊 UI: Extra "Status" kolom toegevoegd aan Movies
- 📊 UI: Extra "Status" kolom toegevoegd aan Series
- 🔄 Sync: Haalt `monitored` veld op uit Radarr/Sonarr

**Fixed:**
- 🐛 docker-compose.yml gebruikt nu `APPUSER_PUID` en `APPUSER_PGID`
- 🐛 .env.example updated met juiste variabele namen

## 🎬 Voorbeeld Output

### Voorheen:
```
Movie Name (2024)    0 KB    Added: 2024-01-15
```
Onduidelijk: Is dit pending of missing?

### Nu:
```
Movie Name (2024)    0 KB    ⚠️ Missing    Added: 2024-01-15
```
Duidelijk: Missing - veilig te verwijderen!

```
Movie Name (2024)    0 KB    ⏳ Pending    Added: 2024-01-15
```
Duidelijk: Pending - nog in download queue!
