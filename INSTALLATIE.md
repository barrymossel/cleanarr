# Cleanarr Installation

## Stap 1: Upload bestanden
1. Open WinSCP en verbind met je OMV server
2. Maak een nieuwe folder: `/home/jouw-gebruiker/cleanarr`
3. Upload ALLE bestanden naar deze folder

## Stap 2: Build de Docker image
1. Open PuTTY en verbind met je OMV server
2. Voer deze commando's uit:

```bash
cd /home/jouw-gebruiker/cleanarr
docker build -t cleanarr:latest .
```

Dit kan 5-10 minuten duren.

## Stap 3: Docker Compose setup in OMV
1. Open OMV web interface
2. Ga naar: Services → Compose → Files
3. Klik op "+" om nieuwe compose file toe te voegen
4. Naam: `cleanarr`
5. Plak de inhoud van `docker-compose.yml`
6. Klik Save

**Let op:** Cleanarr heeft GEEN PUID/PGID nodig (in tegenstelling tot Sonarr/Radarr), omdat het geen directe toegang tot media bestanden nodig heeft. Alle verwijderingen gaan via de Sonarr/Radarr API's.

## Stap 4: Start Cleanarr
1. In de Compose Files lijst, vind je `cleanarr`
2. Klik op de "Up" knop (groene pijl omhoog)

## Stap 5: Open Cleanarr
Open in je browser: `http://jouw-omv-ip:7979`

## Stap 6: Configureer settings
1. Ga naar Settings tab
2. Vul alle API URLs en Keys in:
   - Radarr: meestal http://radarr:7878
   - Sonarr: meestal http://sonarr:8989
   - Tautulli: meestal http://tautulli:8181
   - Overseerr: meestal http://overseerr:5055
3. Test elke connectie
4. Sla op en klik "Sync Now" op Movies of Series pagina

**Belangrijk:** Alle delete acties worden via de Sonarr/Radarr API's uitgevoerd, dus die apps moeten toegang hebben tot de media bestanden.

## Troubleshooting
- Als de build faalt: check of je genoeg schijfruimte hebt
- Als containers niet starten: check logs in OMV Compose interface
- Als API's niet werken: check of de URLs kloppen en of de containers in hetzelfde Docker netwerk zitten
- Permission errors bij /config: niet nodig om aan te passen, Cleanarr draait als container default user

## Poorten
- Cleanarr: 7979
- Zorg dat deze poort niet al in gebruik is
