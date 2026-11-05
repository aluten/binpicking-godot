# Overdracht

## Doel

Bin picking leren, uitleggen en demonstreren in een openbare repository, zonder het vertrouwelijke
model uit het echte project. Elke tussenstap moet zichtbaar en uitlegbaar zijn.

De kern van het probleem is dat een zwart onderdeel weinig goede dieptemetingen oplevert en dat alle
onderdelen op elkaar lijken. De pipeline hoeft niet alle onderdelen te herkennen, maar slechts één
goed bereikbaar onderdeel per cyclus te vinden:

```text
diepte -> puntenwolk -> bakfilter -> vlakke kandidaten -> afdichtingsscore
       -> botsingscheck -> pick -> controle na het optillen -> plaatsen
```

## Status

| Fase | Onderdeel | Status |
|---|---|---|
| 0 | Onderdeel gekozen, repository en CAD-verwijzing | Klaar |
| 1 | CAD naar Godot, stabiel vallende schakels | Klaar, test op Windows nog uitvoeren |
| 2 | Pallet met 720 schakels | Werkt en is opgeslagen; nog geen physics na het laden |
| 3 | Dieptecamera | Perfecte diepte werkt, sensormodel ontbreekt |
| 4 | Eerste pick met zuignap | Nog niet begonnen |
| 5 | Foutgevallen en statistieken | Nog niet begonnen |
| 6 | Positiebepaling met CAD-model | Nog niet begonnen |
| 7 | Robot en publicatie | Nog niet begonnen |

## Keuzes

| Keuze | Reden |
|---|---|
| Eerst alleen simulatie | Geen hardware nodig; een echte camera kan later achter een interface |
| Godot met C# | Goede C#-integratie en een pluginstructuur |
| igus R4.32.10.125.0 | Openbaar te downloaden, zwart, standaard gesloten en schakels kunnen in elkaar haken |
| Model niet in Git | Toestemming voor verspreiding is niet bevestigd, dus iedereen downloadt het zelf |
| Eerst een pipeline zonder AI | Elke stap blijft te laten zien en uit te leggen |
| 5 opzetranden en 720 schakels, niet 3000 | 3000 komt ruim 4 m hoog op één pallet; zie [bak.md](bak.md) |
| Scripts in plaats van handmatig exporteren | Iedereen kan hetzelfde resultaat opnieuw maken |

## Openstaande punten

- **Publicatie:** de repository is privé. Spreek voor het openbaar maken met de werkgever af wie de
  rechten heeft, ook op werk van de stagiair.
- **Licentie:** er is nog geen licentie voor de code gekozen.
- **Beeldmateriaal:** controleer bij igus of video's en screenshots van het model mogen.
- **Geheimhouding:** neem geen cyclustijden, gripperkeuzes of camera-instellingen over uit het echte project.
- **Windows-test:** `requirements.txt` en de physics-test zijn nog niet op Windows uitgevoerd.
- **Git LFS:** nog niet in gebruik, omdat er geen grote bestanden in Git staan.

## Volgende stap

Hang de dieptecamera boven de pallet en voeg het sensormodel toe, zodat te zien is wat een echte camera
aan een zwarte bak zou meten. Daarna volgt de eerste pick met de zuignap.
