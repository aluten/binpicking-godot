# Van CAD naar Godot

Godot kan geen STEP lezen. STEP beschrijft exacte wiskundige vlakken, terwijl Godot met driehoeken
werkt. Bovendien heeft één schakel drie verschillende vormen nodig, elk voor een eigen doel.

```text
igus-site -> cad/r4.32.stp -> convert_link.py -> godot/generated/ -> Godot
                  |                                     |
          inspect_step.py                        check_link.py
```

## Stappen

| Stap | Script | Wat er gebeurt | Waarom |
|---|---|---|---|
| 1. Bekijken | `inspect_step.py` | Toont onderdelen, maten en eenheid | Eerst weten wat er in het bestand zit |
| 2. Kiezen | `convert_link.py` | Pakt de grootste gesloten schakel uit de ketting | igus levert minimaal 9 schakels; volume meten vraagt een gesloten mesh |
| 3. Rechtzetten | `convert_link.py` | Legt de schakel recht en in het midden | De schakels liggen gebogen in de ketting |
| 4. Vereenvoudigen | `convert_link.py` | Maakt een lichte versie | Duizenden volledige schakels zijn te zwaar |
| 5. Botsingsvorm | `convert_link.py` | Maakt convexe delen met `coacd` | Physics-engines rekenen snel met convexe vormen |
| 6. Zuignapvlak meten | `convert_link.py` | Zoekt de grootste cirkel op beide platte vlakken | Een zuignap heeft een dicht vlak nodig |
| 7. Controleren | `check_link.py` | Meet de afwijking van de eenvoudige modellen | Vereenvoudigen kan de vorm te veel veranderen |

## Resultaat

Assenstelsel: X is de lengte van de ketting, Y de bovenkant en Z de breedte. Alle maten zijn in meters.

| Bestand | Doel | Inhoud | Gemeten kwaliteit |
|---|---|---|---|
| `schakel_visual.glb` | Precies beeld, de onderdelen rond de pick | 16.392 driehoeken | 110,2 x 54,0 x 122,0 mm |
| `schakel_lod.glb` | De rest van de bak | 5.000 driehoeken | Mediaan 0,3 mm, maximaal 2,1 mm afwijking; 56,1 mm hoog |
| `schakel_collision.glb` | Physics | 24 convexe delen | 129,2 cm³ tegenover 123,3 cm³, maximaal 3,0 mm afwijking |
| `schakel_rapport.json` | Alle gemeten waarden | | |

## Zuignapvlak

| Vlak | Afmeting | Grootste cirkel |
|---|---|---|
| Boven (Y+) | 47 x 100 mm | Ø 47 mm |
| Onder (Y-) | 41 x 100 mm | Ø 41 mm |

Binnen die cirkels is het vlak dicht. Elders steekt de schakel tot 2 mm boven het vlak uit, dus de
rand of houder van de nap heeft ruimte nodig. Het voorlopige uitgangspunt is een nap van Ø 30 mm.

## Instellingen die je kunt aanpassen

| Instelling | Waarde | Effect |
|---|---|---|
| `LOD_FACES` | 5000 | 1.500 gaf meer dan 5 mm afwijking, 8.000 bleef onder 1 mm |
| `--max-hulls` | 24 | Meer delen is nauwkeuriger maar trager in de physics |
| `--threshold` | 0.03 | Lager is nauwkeuriger, maar geeft sneller meer delen |

De berekening stopte op het maximum van 24 delen, dus dit is een bewuste bovengrens.

## Na een nieuwe download

Draai `inspect_step.py`, `convert_link.py` en `check_link.py` opnieuw en vergelijk de maten met de
tabel hierboven. De namen van de onderdelen veranderen per download en zijn dus geen vast kenmerk.
