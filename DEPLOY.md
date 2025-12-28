# Cleanarr - Nieuwe Versie met Overseerr Delete

## 🎯 Belangrijke Veranderingen

### ✅ Settings in JSON bestand
- **Settings opgeslagen in**: `/config/settings.json` (blijft bewaard)
- **Database in**: `/config/cleanarr.db` (kan veilig verwijderd worden)
- **Geen migrations meer** - database wordt automatisch aangemaakt

### ✅ PUID/PGID Support
- Werkt nu zoals Sonarr/Radarr
- Standaard: `PUID=1000` en `PGID=1000`
- Pas aan in `docker-compose.yml` voor jouw gebruiker

### ✅ Overseerr Delete Functionaliteit
- **Movie delete**: Haalt TMDb ID op uit Radarr → zoekt en verwijdert Overseerr request
- **Series delete**: Haalt TMDb ID op uit Sonarr → zoekt en verwijdert Overseerr request
- **Geen database kolommen nodig** - alles real-time via APIs
- **Altijd betrouwbaar** - gebruikt TMDb ID die nooit verandert

## 🚀 Deployment

### 1. Stop huidige container
```bash
cd /pad/naar/cleanarr
docker-compose down
```

### 2. Verwijder ALLES behalve config folder
```bash
# Verwijder oude code
rm -rf backend frontend .dockerignore Dockerfile docker-compose.yml

# BELANGRIJK: Verwijder oude database (data komt toch uit APIs)
rm -f config/cleanarr.db

# Settings blijven bewaard in config/settings.json
```

### 3. Unzip nieuwe versie
```bash
unzip cleanarr.zip
cd cleanarr
```

### 4. Update .env bestand met PUID/PGID

Kopieer `.env.example` naar `.env` en pas aan:
```bash
cp .env.example .env
nano .env
```

Voorbeeld `.env`:
```env
PATH_TO_APPDATA=/volume1/docker
TIME_ZONE_VALUE=Europe/Amsterdam
PUID=1026    # <-- PAS AAN NAAR JOUW USER ID
PGID=100     # <-- PAS AAN NAAR JOUW GROUP ID
```

**Hoe vind je jouw PUID/PGID?**
```bash
id jouw_gebruikersnaam
# Output: uid=1026(jouw_gebruikersnaam) gid=100(users)
#         ^^^^ PUID          ^^^^ PGID
```

De `docker-compose.yml` gebruikt deze waardes automatisch.

### 5. Build en start
```bash
docker-compose build
docker-compose up -d
```

### 6. Check logs
```bash
docker-compose logs -f
```

Je zou moeten zien:
```
Setting up permissions with PUID=1000 and PGID=1000
Starting Cleanarr as abc (PUID=1000, PGID=1000)
```

### 7. Open browser en configureer
```
http://jouw-server-ip:7979
```

Ga naar Settings en vul je API keys in. Deze worden opgeslagen in `/config/settings.json`.

## 📁 File Structure

```
/config/
├── settings.json     ← Settings (blijft bewaard) ✅
└── cleanarr.db       ← Database (kan verwijderd worden, data uit APIs) ⚠️
```

## 🎬 Hoe Overseerr Delete Werkt

### Bij Movie Delete:
1. **Radarr API**: GET `/api/v3/movie/{id}` → haalt TMDb ID op
2. **Radarr API**: DELETE `/api/v3/movie/{id}?deleteFiles=true`
3. **Overseerr API**: GET `/api/v1/request?filter=all` → zoek naar TMDb ID match
4. **Overseerr API**: DELETE `/api/v1/request/{requestId}`
5. **Cleanarr DB**: Verwijder movie uit database

### Bij Series Delete:
1. **Sonarr API**: GET `/api/v3/series/{id}` → haalt TMDb ID op (Sonarr v4+)
2. **Sonarr API**: DELETE `/api/v3/series/{id}?deleteFiles=true`
3. **Overseerr API**: GET `/api/v1/request?filter=all` → zoek naar TMDb ID match
4. **Overseerr API**: DELETE `/api/v1/request/{requestId}`
5. **Cleanarr DB**: Verwijder series + episodes uit database

**Voordeel van deze aanpak:**
- ✅ Geen extra database kolommen nodig
- ✅ Geen migration problemen
- ✅ Werkt altijd, TMDb ID verandert nooit
- ✅ Toekomstbestendig

## 🔧 Troubleshooting

### Database errors na update
```bash
# Verwijder database (data wordt opnieuw opgehaald uit APIs)
rm config/cleanarr.db
docker-compose restart
```

### Permission errors
```bash
# Check PUID/PGID in docker-compose.yml
# Check of je user/group correct zijn
id jouw_gebruikersnaam
```

### Settings kwijt
```bash
# Check of settings.json bestaat
cat config/settings.json
# Als niet: configureer via UI, wordt automatisch aangemaakt
```

### Overseerr delete werkt niet
Check logs:
```bash
docker-compose logs -f | grep OVERSEERR
```

Mogelijk oorzaken:
- Overseerr URL/API Key niet geconfigureerd
- TMDb ID niet gevonden in Radarr/Sonarr response
- Request bestaat al niet meer in Overseerr

## 📝 Verschillen met Oude Versie

| Feature | OUD | NIEUW |
|---------|-----|-------|
| Settings | SQLite database | JSON bestand |
| Database migrations | Nodig bij updates | Niet meer nodig |
| Overseerr delete | Niet beschikbaar | ✅ Via TMDb ID |
| Permissions | Fixed 777 | PUID/PGID support |
| Data verlies bij rebuild | Settings weg | Settings blijven ✅ |

## ⚠️ Let Op

- **Eerste keer starten**: Database wordt opnieuw aangemaakt (leeg)
- **Sync uitvoeren**: Klik op "Sync Now" om data op te halen uit Radarr/Sonarr/Tautulli/Overseerr
- **Settings bewaard**: Je API keys e.d. blijven staan in `settings.json`
