# Bin picking in Godot

Een werkende, uitlegbare simulatie van bin picking met een zuignap: één zwart onderdeel pakken uit
een pallet vol dezelfde zwarte onderdelen. Het project gebruikt een igus-kabelrupsschakel als
openbaar voorbeeld, en draait voorlopig volledig in simulatie.

> Status: de CAD-stap, een pallet met 720 schakels en een dieptecamera met sensormodel werken. De
> keuze van een pickpunt en de pick zelf moeten nog worden gebouwd. Zie
> [docs/overdracht.md](docs/overdracht.md).

## Snel starten

1. Installeer de software uit [docs/vereisten.md](docs/vereisten.md).
2. Download het igus-model. De bestanden staan bewust niet in deze repository:
   1. Open de [igus-CAD-pagina van R4.32.10.125.0](https://www.igus-cad.com/default.aspx?cul=en-GB&ArtNr=R4.32.10.125.0&mandant=INT).
   2. Kies bij **Chain length** de optie **Quantity chain links** en vul **9** in.
   3. Kies **STEP** als formaat en klik op de downloadknop.
   4. Download het bestand via het **Download center**.
   5. Bewaar het als `cad/r4.32.stp`.
3. Maak de Godot-bestanden, vanuit de hoofdmap van de repository:
   ```powershell
   py -3.12 -m venv .venv
   .venv\Scripts\activate
   pip install -r tools\cad_pipeline\requirements.txt
   python tools\cad_pipeline\convert_link.py cad\r4.32.stp godot\generated
   ```
4. Open `godot/project.godot` in Godot 4.7.2 (.NET-editie) en klik op de hamer om te bouwen.
5. Open `link_physics_test.tscn` en druk op F6. De Output moet eindigen met `RESULTAAT: GESLAAGD`.

6. Open `pallet_bin.tscn` en druk op F6 om de pallet met schakels te bekijken. Bestaat de vulling nog
   niet, dan zie je de bak eerst vollopen. Bediening staat linksboven in beeld.

## Documentatie

| Document | Waarvoor |
|---|---|
| [docs/vereisten.md](docs/vereisten.md) | Wat je moet installeren, waarom, en bekende valkuilen |
| [docs/cad-pijplijn.md](docs/cad-pijplijn.md) | Hoe van een STEP-bestand een bruikbare schakel wordt |
| [docs/bak.md](docs/bak.md) | Hoe de pallet is opgebouwd en gevuld, en de bediening |
| [docs/sensormodel.md](docs/sensormodel.md) | Hoe de virtuele camera zwarte onderdelen meet |
| [docs/testen.md](docs/testen.md) | Welke tests er zijn, hoe je ze leest en wat ze opleverden |
| [docs/overdracht.md](docs/overdracht.md) | Status, gemaakte keuzes, open punten en volgende stappen |

## Mappen

| Map | Inhoud | In Git |
|---|---|---|
| `godot/` | Godot-project met de testscènes | Ja |
| `godot/generated/` | Gegenereerde meshes uit het igus-model | Nee |
| `src/BinPicking.Core/` | Algoritmes zonder Godot, nu het sensormodel | Ja |
| `tests/BinPicking.Core.Tests/` | Tests van die algoritmes | Ja |
| `tools/cad_pipeline/` | Scripts van STEP naar Godot | Ja |
| `cad/` | Het gedownloade igus-model | Nee |
| `docs/` | Documentatie | Ja |

## Over het model

igus is niet betrokken bij dit project. Download het CAD-model zelf bij igus; de voorwaarden van igus
gelden daarvoor. Plaats het model of de daaruit gegenereerde bestanden niet in deze repository.
