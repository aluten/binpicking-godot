# Vereisten

Dit is alles wat nodig is om het project op een Windows-computer te bouwen en te draaien.

## Software

| Onderdeel | Versie | Waarvoor | Wanneer nodig |
|---|---|---|---|
| Godot, .NET-editie | 4.7.2 | Scène, rendering en physics | Altijd |
| .NET SDK | 8.0.x | Compileert de C#-code | Altijd |
| Git | Recent | Versiebeheer | Altijd |
| Git LFS | Recent | Is geïnstalleerd, maar nog niet in gebruik | Pas bij grote bestanden in Git |
| Python | 3.12 | Draait de CAD-scripts | Bij het maken van de schakel |
| Code-editor | Visual Studio, Rider of VS Code | C# bewerken en debuggen | Aanbevolen |

Niet nodig: SolidWorks, Blender, een dieptecamera, een robot, CUDA of een NVIDIA-kaart in het
bijzonder. Forward+ vereist wel een grafische kaart die Vulkan of Direct3D 12 ondersteunt.

Godot 4.7.2 maakt een project met `net8.0`. Daarom legt `global.json` in de hoofdmap SDK 8 vast.

### Installeren en controleren

```powershell
winget install Microsoft.DotNet.SDK.8
winget install Python.Python.3.12
dotnet --list-sdks
git --version
py -0p
```

Download Godot van [godotengine.org](https://godotengine.org/) en kies de **.NET**-versie. Bewaar de
hele map, want `GodotSharp` moet naast de `.exe` blijven staan.

## Python-pakketten

`tools/cad_pipeline/requirements.txt` legt de geteste versies vast.

| Pakket | Wat het doet in dit project |
|---|---|
| `trimesh` | Laadt, meet, bewerkt en exporteert meshes |
| `cascadio` | Zet STEP om naar driehoeken met de CAD-kernel OpenCASCADE |
| `numpy` | Rekent met coördinaten |
| `scipy` | Meet de grootste cirkel op een vlak en de afwijking tussen modellen |
| `pillow` | Tekent het vlak als raster voor die cirkelmeting |
| `fast_simplification` | Maakt de lichte versie van de mesh |
| `coacd` | Verdeelt de schakel in convexe delen voor de botsingsvorm |

## Bekende valkuilen

| Melding of probleem | Oorzaak | Oplossing |
|---|---|---|
| `Could not load file or assembly 'System.Runtime, Version=11.0.0.0'` | Godot gebruikt een nieuwere .NET-SDK die Godot niet aankan | Laat `global.json` staan en start Godot opnieuw |
| Geen hamerknop in Godot | Het C#-project is pas net aangemaakt | Start Godot opnieuw |
| `Python is niet gevonden` | De Microsoft Store-snelkoppeling staat in de weg | Gebruik `py -3.12`, of zet de aliassen uit onder Windows-instellingen |
| Regels verschijnen niet in de Output | Het script hangt niet aan de node | Sleep het script op de node |
| Segmentation fault bij het laden van de STEP | `coacd` is geladen vóór het inlezen | Importeer `coacd` pas na het laden, zoals `convert_link.py` doet |
| Schakels zakken centimeters in de vloer | Standaardwaarden van Jolt zijn te ruim | Zie [testen.md](testen.md#physics-instellingen) |
