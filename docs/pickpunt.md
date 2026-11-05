# Pickpunt kiezen

Het doel is niet om alle schakels te herkennen, maar om één plek te vinden waar de zuignap kan afdichten.
`GraspPlanner` in `src/BinPicking.Core/Grasping` kent Godot niet en werkt alleen op gemeten diepte.

## Stappen

Voor elke 6e pixel in de bak:

| Stap | Vraag | Standaardeis |
|---|---|---|
| 1. Bakfilter | Ligt het punt binnen de bak? | 1 cm van de wand, minstens 3 cm boven de bodem |
| 2. Metingen | Zijn er genoeg punten onder de nap? | Minstens 15% van de napoppervlakte |
| 3. Vlakheid | Past er een vlak door de punten? | 90% binnen 3 mm |
| 4. Hoek | Kan de nap er recht op? | Maximaal 35 graden schuin |
| 5. Houder | Raakt de houder iets hogers? | Binnen Ø 50 mm niets meer dan 5 mm boven het vlak |
| 6. Opening | Past de houder door de bakopening? | Langs de normaal binnen de wanden |

De nap is Ø 30 mm. De eisen staan in `GraspSettings.cs` en zijn een eerste keuze, niet afgesteld op een
echte zuignap.

Score = gemeten deel x deel in het vlak x cos(hoek) x (0,5 + 0,5 x hoogte in de bak). De beste kandidaten
blijven over, onderling minstens een napbreedte uit elkaar.

## Bekijken

Open `pallet_bin.tscn` en druk op F6. In het begin zijn er geen pickpunten te zien, zodat je eerst de bak en
de meting kunt uitleggen.

| Toets P | In beeld |
|---|---|
| Eerste keer | De beste kandidaten in **blauw** |
| Tweede keer | De keuze in **groen** met de aanrijrichting |
| Derde keer | Weer verborgen |

In de sensorweergave (toets 4) staan ze als cirkels. De Output laat zien hoeveel kandidaten bij elke stap
afvallen. Met toets R kun je een lagere vulling maken en vergelijken.

## Resultaat

Op de Linux-cloudcomputer, met vaste vullingen en één seed:

| | Vol, zwart | Vol, licht | 96 schakels, zwart |
|---|---:|---:|---:|
| Beoordeeld | 5.940 | 15.385 | 262 |
| Te weinig metingen | 334 | 0 | 238 |
| Niet vlak | 2.434 | 10.348 | 17 |
| Te schuin | 1.123 | 2.963 | 1 |
| Houder raakt | 877 | 868 | 3 |
| Buiten de opening | 117 | 193 | 0 |
| Goedgekeurd | 1.055 | 1.013 | 3 |
| Beste kandidaten met minstens 90% afdichting | 19 van 20 | 20 van 20 | 3 van 3 |

**Op zwart werkt het vol goed, maar laag in de bak blijven weinig plekken over.** Verder weg komt er minder
licht terug, waardoor de meeste plekken te weinig metingen hebben. Daar is de marge dus klein.

De eisen zijn hierop aangepast. Bij 25% metingen en 95% binnen 2 mm vond de planner op deze lage vulling
niets, terwijl op perfecte diepte wel 463 plekken werden goedgekeurd. De meting ruist immers, dus voor het
zoeken geldt nu 3 mm; de controle blijft 2 mm.

## Wat de controle wel en niet zegt

De werkelijke afdichting vergelijkt het gekozen vlak met de perfecte diepte: welk deel van de nap ligt binnen
2 mm? Dat is alleen in simulatie mogelijk, en heeft grenzen:

- Het is één vulling en één seed, dus geen betrouwbaar slagingspercentage.
- De vulling gebruikt de vereenvoudigde mesh, niet de volledige schakel.
- Twee schakels op gelijke hoogte kunnen samen als één vlak tellen.
- Gaten in de meting worden als vrije ruimte behandeld bij de houdercontrole.
- Er is nog niets gepakt: vacuüm, gewicht en vastgehaakte schakels komen pas bij de pick aan bod.
