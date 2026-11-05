# Sensormodel

Godot levert perfecte diepte. Een echte camera doet dat niet, zeker niet op zwarte onderdelen. Het
sensormodel maakt van perfecte diepte wat een structured-light-camera ongeveer zou meten: een projector
legt een patroon op de bak en een camera ernaast meet dat patroon terug.

**De standaardwaarden zijn illustratief en niet gemeten aan een echte camera.** Het model laat de relatieve
invloed van materiaal, hoek en schaduw zien. Het voorspelt niet wat een specifieke camera meet.

## Opbouw

```text
Godot: DepthSensorRig                 BinPicking.Core: DepthSensorModel
  diepte vanaf de camera   --+
  diepte vanaf de projector --+-->  gaten en ruis  -->  gemeten diepte + reden per gat
  reflectie per pixel      --+
```

Het model zit in `src/BinPicking.Core` en kent Godot niet. Daardoor is het los te testen en kan later een
echte camera dezelfde stappen voeden.

## Wat het model doet

| Effect | Berekening | Waarom |
|---|---|---|
| Weinig licht | Signaal = reflectie x cos(hoek) x (referentieafstand / afstand)². Laag signaal geeft kans op een gat | Zwart en schuin kaatst weinig patroon terug |
| Schaduw | Ziet de projector op dezelfde plek iets dichterbij, dan valt daar geen patroon | Camera en projector staan naast elkaar |
| Randen | Bij een dieptesprong ontbreekt de meting vaak | Het patroon breekt op een rand |
| Ruis | Groeit met afstand en bij minder signaal | Zwakker patroon is onzekerder |

De kleur in het beeld geeft de reden: **magenta** is weinig licht, **blauw** schaduw en **geel** een rand.
De instellingen staan in `DepthSensorSettings.cs`.

## Bekijken

Open `pallet_bin.tscn` en druk op F6. Toets **4** wisselt tussen 3D, perfecte diepte en sensor. Toets **B**
wisselt tussen zwarte en lichte schakels. De sensor hangt 1,5 m boven de rand, met een beeld van 1280 x 960.

## Resultaat op de opgeslagen vulling

Gerenderd met een softwarerenderer op de Linux-cloudcomputer:

| Schakels | Zonder meting | Weinig licht | Schaduw | Rand | Ruis |
|---|---|---:|---:|---:|---:|
| Zwart | 67% | 54% | 7% | 5% | 0,6 mm |
| Licht | 14% | 1% | 7% | 5% | 0,4 mm |

Hout en achtergrond: 3% zonder meting. De percentages tellen door afronding niet altijd precies op. Platte
vlakken die recht naar de camera wijzen houden de meeste metingen, en dat zijn juist de vlakken waar een
zuignap op past. Daarop bouwt de volgende stap.

## Testen

```powershell
dotnet run --project tests/BinPicking.Core.Tests
```

De test gebruikt geen extra pakketten en controleert onder andere dat zwart meer gaten geeft dan licht, dat
ruis met de afstand groeit, dat schaduw aan de juiste kant van een blok valt en dat een seed herhaalbaar is.
