# Testen

| Scène | Controleert | Slaagt als |
|---|---|---|
| `depth_test.tscn` | Komt de diepte nauwkeurig in C# terecht? | Drie metingen wijken maximaal 1 mm af |
| `link_physics_test.tscn` | Werken import, schaal en stapelen? | Geen regel met `FOUT` |

Open de scène in Godot, klik op de hamer en druk op F6. De resultaten staan in de Output.

## Dieptetest

Een camera kijkt van boven op blokken met een bekende hoogte. De test gebruikt perfecte diepte en zegt
dus nog niets over het gedrag van een echte camera op zwarte onderdelen.

Resultaat op een RTX 4070 Laptop GPU met Direct3D 12: 0,00 mm afwijking op alle drie de punten.

## Physics-test

De test laat 30 schakels in een bak van 50 x 50 cm vallen, wacht tot ze stilliggen en tilt daarna de
bovenste schakel 30 cm op.

| Regel | Betekenis |
|---|---|
| Afmeting | Kloppen import en schaal? |
| Tot rust | Komt de stapel binnen 20 s tot stilstand? |
| Buiten de bak | Zijn er schakels ontsnapt? |
| Diepst in de vloer | Hoe ver zakt het zichtbare model in de vloer? De grens is 4 mm |
| Physics-tijd | Hoeveel rekentijd kost een stap? Bij 60 Hz is er 16,7 ms beschikbaar |
| Optillen | Hoeveel schakels beweegt de pick mee? Dit is informatie, geen eis |

De grens van 4 mm is de gemeten afwijking van de botsingsvorm (3 mm) plus 1 mm speling. De massa van
0,15 kg per schakel is een aanname en geen igus-waarde.

### Physics-instellingen

De standaardwaarden van Jolt passen bij spelobjecten, niet bij onderdelen van een paar centimeter.
Daarom staat in `project.godot`:

| Instelling | Standaard | Hier |
|---|---|---|
| `penetration_slop` | 0,02 m | 0,0005 m |
| `speculative_contact_distance` | 0,02 m | 0,002 m |

Met de standaardwaarden zakte het model tot 20,3 mm in de vloer, en dan kloppen het stapelen en
het in elkaar haken niet meer. Een dunne vloer van 5 cm bleek ook te zwak: een schakel werd er een keer doorheen gedrukt.
De vloer is daarom 50 cm dik en de wanden 30 cm.

### Resultaten

Gemeten met Godot 4.7.2 zonder beeldscherm op een Linux-cloudcomputer, dus **niet** op de Windows-laptop.

| Seed | Tot rust | In de vloer | Physics gem. / max | Mee bewogen van 29 | Hoogste stijging |
|---|---|---|---|---|---|
| 1 | 6,9 s | 3,4 mm | 11,1 / 28,2 ms | 0 | 0,3 mm |
| 2 | 5,0 s | 2,7 mm | 6,3 / 8,7 ms | 1 | 1,0 mm |
| 3 | 2,7 s | 2,7 mm | 13,3 / 21,2 ms | 14 | 22,5 mm |

Twee conclusies:

- **In elkaar haken komt voor en wisselt sterk.** Dat maakt een mislukte pick zichtbaar en meetbaar.
- **Rekentijd is de grens.** Rond de 30 actieve schakels zit de physics op deze computer al dicht bij
  de 16,7 ms. De bak met 3000 kan dus alleen als bijna alles bevroren is.

De resultaten verschillen tussen runs, ook bij dezelfde seed: seed 1 gaf in een herhaling 7,0 ms
gemiddeld. Vergelijk dus geen losse metingen.
