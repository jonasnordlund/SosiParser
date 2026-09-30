# SosiParser (.NET Framework 4.8)

En C#-portering av "lite"-varianten av [sosi.js](https://github.com/atlefren/sosi.js), för att läsa SOSI-filer
och exportera dem som GeoJSON eller TopoJSON. Inga externa beroenden.

```
SosiParser.sln
├── src/Sosi/            Klassbiblioteket (Sosi.dll, namespace Sosi)
├── src/SosiConvert/     Kommandoradsprogram (SosiConvert.exe)
└── tests/Sosi.Tests/    MSTest: JS-testerna porterade + .NET-specifika tester (testdata kopierad från sosi.js/data)
```

Bygg och testa:

```bash
dotnet build SosiParser.sln -c Release
```

```bash
dotnet test SosiParser.sln
```

Projekten är SDK-style med `net48` som mål och C# 7.3, så de öppnas i Visual Studio 2019/2022.

## Användning

```csharp
using Sosi;

var parser = new SosiParser();
var sosidata = parser.Parse(mySosiDataFile);          // newline-separerat .SOS-innehåll
// eller: parser.ParseFile(@"C:\data\fil.sos")        // läser filen och avgör teckenkodningen

var header = sosidata.Header;                         // .hode
string srid = header.Srid;                            // "EPSG:25832"
int epsg = header.EpsgCode;                           // 25832
BoundingBox bbox = header.BoundingBox;                // .bbox (MinX, MinY, MaxX, MaxY, ToArray())

int featureCount = sosidata.Features.Count;           // .features.length()
var features = sosidata.Features;                     // .features.all()      – sorterade på Id
var inFileOrder = sosidata.FileOrderedFeatures;       // .features.all(true)  – filordning
var firstFeature = sosidata.Features[0];              // lägsta Id
var firstInFile = sosidata.FileOrderedFeatures[0];    // .features.at(0)
var byId = sosidata.Features.GetById(200);            // .features.getById(200), O(1), null om saknas
var sameById = sosidata.Features.FirstOrDefault(f => f.Id == 200); // fungerar också (LINQ, O(n))

string geojson = sosidata.Export(FileTypes.GeoJson);
string topojson = sosidata.Export(FileTypes.TopoJson, "name_of_objects");
string pretty = sosidata.Export(FileTypes.GeoJson, indented: true);
```

`Features` och `FileOrderedFeatures` delar samma underliggande objekt; `Features` är en
`SosiFeatureCollection` (`IReadOnlyList<SosiFeature>`) med uppslag på id.

### Feature och geometri

| .NET                                   | sosi.js                        |
|----------------------------------------|--------------------------------|
| `feature.Id`, `feature.GeometryType`   | `id`, `geometryType`           |
| `feature.Attributes["OBJTYPE"]`        | `attributes.OBJTYPE`           |
| `Point` (`X`, `Y`, `Z`, `HasTiepoint`, `TiepointCode`) | `Point` (`x`, `y`, `z`, `has_tiepoint`, `knutepunktkode`) |
| `LineString.Points` / `.Tiepoints`     | `LineString.kurve` / `.knutepunkter` |
| `LineStringFromArc` (BUEP, ärver `LineString`) | `LineStringFromArc`    |
| `Polygon.Shell`, `.Holes`, `.Center`, `.ShellRefs`, `.HoleRefs` | `flate`, `holes`, `center`, `shellRefs`, `holeRefs` |

Geometrin byggs när `feature.Geometry` läses första gången (som i JS), så en FLATE kan referera
KURVE:or som kommer senare i filen.

Attributvärden är, precis som i lite-versionen, råa strängar från filen (inklusive eventuella
citattecken), en `IReadOnlyList<string>` för upprepade attribut (t.ex. `LTEMA`) eller en
`IReadOnlyDictionary<string, string>` för grupperade attribut (t.ex. `KOPIDATA`).
`feature.GetAttributeString(key)` ger alltid en sträng.

Fel i data ger `SosiParseException`; geometrityper som inte stöds (t.ex. `TRASE`) ger
`NotSupportedException` när geometrin läses.

## Kommandorad

```bash
SosiConvert geojson indata.sos utdata.geojson --indent
```

```
usage: SosiConvert <format> <infile.sos> [outfile] [--name <objects>] [--indent]
       format: geojson | topojson | info
```

## Skillnader mot sosi.js (lite)

Resultatet är avsett att vara identiskt med JS-versionen för giltiga filer. Medvetna avvikelser:

- **Teckenkodning:** `ParseFile`/`ReadAllText` väljer UTF-8 om filen har BOM eller är giltig UTF-8,
  annars kodningen i `..TEGNSETT` (ISO8859-1/10/15, ANSI, DOSN8, ND7/DECN7).
- **`!` inom citattecken** (`..NAVN "Hei!"`) tolkas inte som kommentar.
- **`..NØ`/`..NØH`/`..REF`** känns igen som hela nyckelord. JS letade efter delsträngar, så t.ex.
  `...NØYAKTIGHET` eller `..REFERANSE` kunde felaktigt tolkas som geometri/referenser.
- En attributrad direkt efter `..REF`-raderna slängs inte längre (JS tappade den).
- Saknad `.HODE`, `TRANSPAR`, `ORIGO-NØ` eller `ENHET`, okänd `KOORDSYS`/`GEOSYS`, saknade
  refererade kurvor m.m. ger `SosiParseException` med ett begripligt meddelande i stället för
  JavaScript-krascher. Saknas `OMRÅDE` blir `BoundingBox` `null`.
- `Version`/`Level` är `double?` (`null` i stället för `NaN`).
- TopoJSON utan namn får objektnamnet `"features"` (JS gav en tom sträng).
- Proj4-definitionen för KOORDSYS 26 är rättad till zon 36 (SRID oförändrad).
- `DEF`/`OBJDEF`, som JS läste in till tomma objekt, exponeras inte.
