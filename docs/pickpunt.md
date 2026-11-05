# Pickpunt kiezen

Het doel is niet om alle schakels te herkennen, maar om één plek te vinden waar de zuignap kan afdichten.
`GraspPlanner` in `src/BinPicking.Core/Grasping` kent Godot niet en werkt alleen op gemeten diepte.

## Stappen

Voor elke 6e pixel in de bak:

| Stap | Vraag | Standaardeis |
|---|---|---|
| 1. Bakfilter | Ligt het punt binnen de bak? | Tussen wanden, rand en bodem |
| 2. Metingen | Zijn er genoeg punten onder de nap? | Minstens 25% van de napoppervlakte |
| 3. Vlakheid | Past er een vlak door de punten? | 95% binnen 2 mm |
| 4. Hoek | Kan de nap er recht op? | Maximaal 35 graden schuin |
| 5. Houder | Raakt de houder iets hogers? | Binnen Ø 50 mm niets meer dan 5 mm boven het vlak |
| 6. Opening | Past de houder door de bakopening? | Langs de normaal binnen de wanden |

De nap is Ø 30 mm. De eisen staan in `GraspSettings.cs` en zijn een eerste keuze, niet afgesteld op een
echte zuignap.

Score = gemeten deel x deel in het vlak x cos(hoek) x (0,5 + 0,5 x hoogte in de bak). De beste kandidaten
blijven over, onderling minstens een napbreedte uit elkaar.

## Bekijken

Open `pallet_bin.tscn` en druk op F6. **Groen** is het beste pickpunt met de aanrijrichting, **blauw** zijn de
andere kandidaten. In de sensorweergave (toets 4) staan ze als cirkels. De Output laat zien hoeveel
kandidaten bij elke stap afvallen.

## Resultaat op de opgeslagen vulling

Op de Linux-cloudcomputer, met één vaste vulling en seed:

| | Zwarte schakels | Lichte schakels |
|---|---:|---:|
| Beoordeeld | 9.618 | 19.181 |
| Te weinig metingen | 1.064 | 0 |
| Niet vlak | 5.841 | 15.390 |
| Te schuin | 1.306 | 2.351 |
| Houder raakt | 518 | 477 |
| Buiten de opening | 73 | 140 |
| Goedgekeurd | 816 | 823 |
| Beste 20 met minstens 90% afdichting | 20 | 20 |

Ondanks 67% ontbrekende metingen vindt de planner dus goede punten op zwart, omdat de vlakken die recht naar
de camera wijzen de meeste metingen houden.

## Wat de controle wel en niet zegt

De werkelijke afdichting vergelijkt het gekozen vlak met de perfecte diepte: welk deel van de nap ligt binnen
2 mm? Dat is alleen in simulatie mogelijk, en heeft grenzen:

- Het is één vulling en één seed, dus geen betrouwbaar slagingspercentage.
- De vulling gebruikt de vereenvoudigde mesh, niet de volledige schakel.
- Twee schakels op gelijke hoogte kunnen samen als één vlak tellen.
- Gaten in de meting worden als vrije ruimte behandeld bij de houdercontrole.
- Er is nog niets gepakt: vacuüm, gewicht en vastgehaakte schakels komen pas bij de pick aan bod.
