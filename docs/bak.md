# De bak: pallet met opzetranden

De bak is een europallet met 5 houten opzetranden erop: de `pallet collar` zoals
[Fabory 8W372370](https://www.fabory.com/en_FR/pallet-stacking-frame-l-1200-x-w-800-mm-4-hinges-useful-height-200-mm-pine/p/8W372370).

| Maat | Waarde | Bron |
|---|---|---|
| Buitenmaat per rand | 1200 x 800 mm | Fabory |
| Hoogte per rand | 200 mm | Fabory |
| Aantal randen | 5, dus 1000 mm hoog | Keuze |
| Plankdikte | 20 mm | Aanname |
| Binnenmaat | 1160 x 760 mm | Berekend uit de aanname |
| Schakels | 720 | Keuze |

## Waarom niet 3000 schakels

Op één pallet zouden 3000 schakels ruim 4 m hoog komen. De camera ziet toch alleen de bovenste lagen,
dus voor het visieprobleem verandert er weinig. Het aantal staat in `TargetLinks` in `PalletBinDemo.cs`.

## Bediening

| Invoer | Actie |
|---|---|
| Linker- of rechtermuisknop slepen | Draaien |
| Middelste muisknop slepen | Verschuiven |
| Muiswiel | Zoomen |
| 1 / 2 / 3 | Overzicht / vanaf de sensor / dichtbij |
| 4 | 3D / perfecte diepte / sensor |
| B | Zwarte of lichte schakels |
| R | Bak opnieuw vullen en meekijken |

## Hoe de bak wordt gevuld

Physics op honderden schakels tegelijk is te zwaar, dus de vulling gebeurt in stappen en wordt daarna
opgeslagen in `godot/generated/pallet_vulling.json`. Dat bestand staat niet in Git; zonder bestand vult
de scène de bak opnieuw.

1. Laat 24 schakels vallen, net boven de hoogste schakel.
2. Wacht tot ze stilliggen, maximaal 4 s.
3. Bevries schakels die meer dan 12 cm onder de hoogste schakel liggen en stil zijn.
4. Steekt zo'n schakel in de wand of bodem, laat hem dan opnieuw vallen.
5. Herhaal tot alle 720 schakels zijn geplaatst, en sla de posities op.

Een geladen vulling gebruikt een `MultiMesh`: één mesh die 720 keer wordt getekend, zonder physics.

## Wat we leerden

| Probleem | Oorzaak | Oplossing |
|---|---|---|
| Schakels staken tot 24 cm in de wand | Dunne botsingswanden; voorbij het midden wordt een schakel naar buiten gedrukt | Botsingswanden en vloer van 2 m dik; alleen de zichtbare planken zijn dun |
| Enkele schakels bleven 5 cm in de wand steken | Ze zaten vast tussen bevroren schakels | Opnieuw laten vallen |
| Meer `position_steps` | Hielp niet aantoonbaar; de physics-test werd slechter | Standaardwaarde behouden |

## Beperkingen

- De planken en het beslag zijn eenvoudige blokken; de precieze vorm is niet nagemeten.
- De massa van 0,15 kg per schakel is een aanname.
- Een geladen vulling heeft nog geen physics. Die komt bij de pick.
- Elke vulling is anders; ook de hoogte varieert, in de runs tussen 774 en 1004 mm.
