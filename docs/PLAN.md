# Ontwikkelplan XPacker API & MCP

Oct 25, 2026 · @Jeroen

## Doel en scope

We bouwen één Windows-programma, `xpk.exe`, dat XPacks leest en wijzigt via RaceBits.dll. Het programma werkt als commandoregeltool en als MCP-server voor Claude. Na fase 3 heb je een eerste bruikbare versie: lezen, bulk-wijzigen en MCP. De rest volgt per fase, en elke fase levert iets op dat je los kunt gebruiken.

**Wel in scope**

- Lezen van objecten, String Objects, materialen en textures
- Bulk-wijzigen per map of zoekpatroon, met proefdraai (dry-run) en automatische backup
- Validatie op veelgemaakte fouten, gericht op Assetto Corsa
- Dupliceren en eigenschappen kopiëren van object naar object
- FBX-import met hergebruik van materialen en textures. Dit vervangt de IL-patch.
- Zip naar RTB

**Buiten scope**

- RTB-banen bewerken (Venue.bin)
- Het scherm van XPacker aanpassen of patchen
- Verspreiden aan derden. Het is voor eigen gebruik, omdat RaceBits.dll niet meegeleverd mag worden.

**Zo gebruik je dit plan**

1. Maak een lege map met git, bijvoorbeeld `C:\dev\xpk`.
2. Zet de CLAUDE.md uit de sectie "Werken met Claude Code" in die map.
3. Exporteer dit plan als Markdown en zet het erin als `docs/PLAN.md`.
4. Start Claude Code in die map, op je Windows-pc, en geef de startprompt van fase 0.
5. Doe één fase per sessie. Aan het eind van elke fase test jij het resultaat in XPacker, aan de hand van de acceptatiecriteria.
6. Fase 0 is een go/no-go. Valt die negatief uit, dan stapt het plan over op route B: de XML-bestanden direct bewerken, zonder RaceBits.dll. Zie Architectuur.

*(Tekening in het online document: roadmap met 8 fases en 3 gates.)*

In totaal grof 10 tot 14 sessies. Elke fase levert iets op dat je los kunt gebruiken. Na fase 0 worden de inschattingen bijgesteld.

## Randvoorwaarden

Deze regels gelden voor elke taak in het plan. Claude Code leest ze ook via de CLAUDE.md.

| Onderwerp | Regel |
| --- | --- |
| Platform | Windows 10/11 x64, op de pc waar RTB staat. Bouwen en testen gebeurt daar, niet in de cloud. |
| Runtime | .NET Framework 4.8 en `PlatformTarget` x64, omdat RaceBits.dll een 64-bit .NET Framework-assembly is. RaceFbx.dll is C++/CLI en laadt niet in .NET 8. |
| Bouwen | SDK-stijl csproj met `<TargetFramework>net48</TargetFramework>` plus het pakket `Microsoft.NETFramework.ReferenceAssemblies`. Zo werkt `dotnet build` zonder Visual Studio; wel is de .NET 8 SDK nodig. |
| RTB-bestanden | Verwijzen naar `RaceBits.dll`, `RaceFbx.dll`, `SharpDX*.dll` en `Serilog.dll` in de RTB-map via `HintPath` met `<Private>false</Private>`. Deze bestanden komen nooit in de output-map of in git. |
| Laden bij runtime | Een `AssemblyResolve`-handler laadt de RTB-assemblies uit de ingestelde RTB-map. `SetDllDirectory(rtbDir)` regelt de native DLL's, zoals de FBX SDK. |
| RTB-map | Alleen-lezen. Er wordt nooit iets geschreven in `C:\Program Files (x86)\Steam\steamapps\common\Race Track Builder`. |
| Schrijven | Alleen in de XPack-map die bewerkt wordt, en in de backupmap. |
| XPacker open | Schrijven is geweigerd zolang `XPacker.exe` draait, tenzij je `force` meegeeft. |
| Backup | Voor elke schrijfactie worden de bestanden die gaan veranderen gekopieerd naar `%LOCALAPPDATA%\xpk\backups\<xpack>\<yyyyMMdd-HHmmss>\`. |
| Proefdraai | Elke schrijfactie staat standaard op `dry_run = true`. Die laat zien wat er zou veranderen, zonder iets weg te schrijven. |
| Getallen | Overal cultuuronafhankelijk, met een punt als decimaalteken, in JSON, CLI en logs. Dat geldt ook op een Nederlandse Windows. |
| stdout | In `serve`-modus is stdout alleen voor het MCP-protocol. Alle logging gaat naar stderr en naar `%LOCALAPPDATA%\xpk\logs\`. Bij het opstarten wordt `Console.Out` omgeleid naar stderr, zodat ook RaceBits niets op stdout kan schrijven. |
| Versiecontrole | Bij het opstarten worden FileVersion en SHA-256 van RaceBits.dll gelogd. Bij een onbekende versie (dus na een RTB-update) start xpk alleen-lezen. Schrijven kan pas weer na `allowUnknownRaceBits: true` in de config en een geslaagde testrun. |
| Originele RaceBits | Bouw en test tegen de originele RaceBits.dll. Draai eerst `_FbxDedupePatch\herstel.bat`. De import-patch wordt in fase 6 vervangen door gewone code. |
| Taal | Code, identifiers en MCP-toolbeschrijvingen in het Engels, omdat het model daar het best mee werkt. README en meldingen voor jou in het Nederlands. |
| Distributie | Alleen voor eigen gebruik. De repo bevat geen RTB-bestanden en geen gedecompileerde RTB-code. |

## Architectuur

Alles draait in één .NET Framework 4.8-proces, `xpk.exe`, dat bestaat uit drie lagen:

- **Core** praat met RaceBits.dll.
- **CLI** is bedoeld voor de terminal en voor scripts.
- **MCP** draait via `xpk serve` (stdio) en wordt door Claude Code of Claude Desktop als lokale server gestart.

De server houdt één XPack in het geheugen. Daardoor zijn opvolgende aanroepen snel. Elke schrijfactie gaat door dezelfde vaste stappen: selecteren, plannen, backup maken, toepassen en wegschrijven.

*(Tekening in het online document: architectuur met drie lagen in één proces.)*

Claude en de terminal gebruiken dezelfde Core. Wijzigingen aan XPack-bestanden lopen via RaceBits. Core maakt zelf alleen de backups, en zet ze terug als dat nodig is.

**Wat RaceBits al biedt.** Dit zijn openbare methodes, gevonden in de analyse van RaceBits.dll:

| Doel | RaceBits-aanroep | Opmerking |
| --- | --- | --- |
| Starten | `Globals.Initialize(Device, exeDir, docsDir)` | XPacker geeft een Direct3D11-device mee, plus de RTB-map en `Documenten\Race Track Builder`. |
| Openen | `new XPack(path)` en `LoadFromFolder(string, string)`, gevolgd door `VerifyAndSetReferences(XPacks)` en `FillEmptyDefaultListsFromAllItems()` | Precies de volgorde die XPacker zelf gebruikt. |
| Lezen | `Objects`, `SObjects`, `Materials`, `Textures`, `CrossSections` | `BObject` heeft properties, zoals Render, Shadow, ShadowCast, LODIn/LODOut, Purpose en RandomLimits. `Mat` heeft publieke velden, zoals ShaderType, AC\_*, GrassFX\_*, rF\_\* en TexMap1–9. |
| Object opslaan | `CreateObject(BObject, false)` | Schrijft `<XPack>\<pad>.xml`, net als XPacker bij Save. |
| Materiaal opslaan | `CreateMaterialXML(Mat)` en `CreateMaterialIconFile(Mat)` |  |
| String Object opslaan | `CreateSObject(SObject, string)` |  |
| Samenvoegen | `PointThingsToNewMaterial(old, new)` en `RemoveMaterialAndFiles(old)` | XPacker gebruikt deze aanroepen zelf ook. Dit is de basis voor dedupe. |
| Kopiëren | `CopyMaterial(Mat)`, `Mat.Clone()` en `Mat.Copy()` |  |
| Controleren | `MaterialInUse(Mat)` en `Mat.GetMissingRequiredTextures()` |  |
| Importeren | `ImportFBX(fbxPath, folder)` en daarna `CreateObject` |  |
| Inpakken | `ZipTo(string)` |  |
| Beschrijving | `SaveToXML()` | Schrijft alleen Description.xml. |

De exacte betekenis van parameters en de randgevallen zijn nog niet getest. Fase 0 legt ze vast in `docs/racebits-api.md`.

**Technische keuzes**

| Keuze | Besluit | Waarom | Alternatief |
| --- | --- | --- | --- |
| Toegang tot XPack | Route A, via RaceBits.dll | Zelfde XML-formaat en verwijzingen als XPacker, dus minder kans op kapotte XPacks | Route B: XML direct lezen en schrijven, als RaceBits zonder scherm niet werkt |
| Direct3D | Headless `new SharpDX.Direct3D11.Device(DriverType.Hardware)` | `Globals.Initialize` vraagt om een device; een venster is niet nodig | `DriverType.Warp`, softwarematig |
| MCP | Officiële C# SDK `ModelContextProtocol` (stdio) | Het Core-pakket ondersteunt netstandard2.0 en is daardoor bruikbaar op net48. Fase 0 test dit. | Zelf een minimale JSON-RPC-lus schrijven: initialize, tools/list en tools/call |
| JSON | System.Text.Json, meegeleverd met de MCP SDK | Geen tweede JSON-bibliotheek nodig | Newtonsoft.Json |
| Tests | xUnit 2.x op net48, uitgevoerd met `dotnet test` | Werkt zonder Visual Studio | NUnit |
| Assembly laden | `AssemblyResolve` laadt **alleen** RTB-assemblies uit de RTB-map: RaceBits, RaceFbx, SharpDX\* en Serilog | Anders ontstaan conflicten met andere System.\*-versies in de RTB-map |  |
| Status | Server houdt de XPack in het geheugen en maakt van de bestanden een momentopname (mtime + grootte) | Snelle aanroepen. Gewijzigd op schijf betekent eerst `xpack_reload`. | Bij elke aanroep opnieuw laden: traag bij grote XPacks |

## Projectstructuur

Er komen drie projecten in één solution. Ze leveren samen één `xpk.exe` op. De bestanden zijn opgedeeld per verantwoordelijkheid, zodat Claude Code steeds een klein stuk tegelijk kan aanpassen.

```text
xpk/
  CLAUDE.md                  vaste regels voor Claude Code (sjabloon onderaan)
  README.md                  gebruik, installatie, MCP koppelen (NL)
  Directory.Build.props      net48, x64, LangVersion, $(RtbDir)
  xpk.sln
  .mcp.json                  MCP-registratie voor Claude Code (fase 3)
  docs/
    PLAN.md                  dit plan
    racebits-api.md          bevindingen fase 0: aanroepen, volgorde, valkuilen
    xpack-format.md          bevindingen fase 0: mappen en XML per itemsoort
  src/Xpk.Core/              alle logica, geen I/O naar console
    Env/RtbEnvironment.cs    RTB-map vinden, AssemblyResolve, SetDllDirectory, D3D-device, Globals.Initialize
    Env/XpkConfig.cs         %APPDATA%\xpk\config.json lezen
    Env/VersionGuard.cs      FileVersion + SHA-256 van RaceBits.dll, bekende versies
    Session/XPackSession.cs  openen, herladen, momentopname van schijf
    Model/*.cs               platte snapshots: ObjectInfo, MaterialInfo, TextureInfo, SObjectInfo
    Props/PropertyMap.cs     whitelist van eigenschappen per itemsoort, met get/set en type
    Select/Selector.cs       pad, map, glob (*, **) en re:-regex
    Write/ChangeSet.cs       geplande wijzigingen: item, eigenschap, oud, nieuw
    Write/Writer.cs          backup, toepassen, wegschrijven via RaceBits
    Write/Backup.cs          backups maken, tonen, terugzetten
    Write/ProcessGuard.cs    weigert als XPacker.exe draait
    Validate/*.cs            regels en Validator
    Ops/*.cs                 Duplicate, CopyProperties, FbxImport, MaterialMerge, ZipToRtb
  src/Xpk.Mcp/               MCP-tools, dunne laag over Core
    McpHost.cs               stdio-server opzetten, stdout beschermen
    Tools/*.cs               één bestand per toolgroep
  src/Xpk.Cli/               xpk.exe: CLI-commando's + `serve`
    Program.cs
    Commands/*.cs
  tests/Xpk.Tests/
    Unit/                    zonder RTB: Selector, PropertyMap-conversies, ChangeSet
    Integration/             met RTB: [Trait("Category","Integration")]
    Fixtures/README.md       hoe de test-XPack gemaakt wordt
  tools/
    make-fixture.ps1         kopieert een kleine eigen XPack naar tests\Fixtures\work
    after-rtb-update.ps1     integratietests na een RTB-update, nieuwe hash toevoegen
```

**Test-XPack (fixture).** Een kleine kopie van een eigen XPack, met:

- 3 objecten in 2 mappen
- 1 object met child objects
- 3 materialen
- 2 kerb-textures met dezelfde naam maar een andere inhoud
- 1 String Object

De bron staat in `tests/Fixtures/source` en zit in git, omdat het je eigen werk is. Elke test werkt op een verse kopie in `tests/Fixtures/work`, die in `.gitignore` staat.

## MCP-tools en CLI

Er komen 23 tools in totaal, verdeeld over zes fases. Elke tool roept precies één Core-functie aan. De CLI gebruikt dezelfde functies.

| Tool | Invoer | Uitvoer | Schrijft | Fase |
| --- | --- | --- | --- | --- |
| `xpack_list` | – | XPacks: naam, map, laatst gewijzigd | nee | 1 |
| `xpack_open` | `name` of `path` | samenvatting: aantallen, auteur, map, RaceBits-versie | nee | 1 |
| `xpack_reload` | – | samenvatting | nee | 1 |
| `objects_list` | `select`?, `fields`? | lijst objecten met gevraagde velden | nee | 1 |
| `object_get` | `path` | alle eigenschappen, child-templates, gebruikte materialen | nee | 1 |
| `sobjects_list` / `sobject_get` | `select`? / `path` | idem voor String Objects | nee | 1 |
| `materials_list` / `material_get` | `select`? / `path` | shader, AC\_\*-velden, TexMaps, aantal keer gebruikt | nee | 1 |
| `textures_list` | `select`? | pad, bytes, breedte × hoogte, formaat, MD5, gebruikt door | nee | 1 |
| `objects_set` | `select`, `values`, `dry_run`=true, `force`=false | wijzigingsrapport: item, eigenschap, oud → nieuw | ja | 2 |
| `sobjects_set` / `materials_set` | idem | idem | ja | 2 |
| `backups_list` / `backup_restore` | – / `id` | backups / herstelde bestanden | ja | 2 |
| `validate` | `rules`?, `select`? | bevindingen: ernst, regel, item, melding, voorstel | nee | 4 |
| `object_duplicate` / `material_duplicate` / `sobject_duplicate` | `path`, `new_name`, `folder`?, `dry_run` | nieuw pad | ja | 5 |
| `properties_copy` | `from`, `to` (selector), `fields`, `dry_run` | wijzigingsrapport | ja | 5 |
| `fbx_import` | `fbx_path`, `folder`, `reuse`=true, `dry_run` | objecten, hergebruikte en nieuwe materialen/textures, tags | ja | 6 |
| `materials_merge` | `keep`, `remove[]`, `dry_run` | omgezette verwijzingen, verwijderde materialen | ja | 6 |
| `zip_to_rtb` | `dry_run` | pad van de zip | ja | 7 |

**Afspraken voor alle tools**

- **Selector.** Een pad is relatief aan de itemsoort, bijvoorbeeld `Bomen\Eik_01`. `*` staat voor één niveau en `**` voor alle onderliggende niveaus, zoals `Kerbs\**`. Met `re:` begint een regex. Zowel `\` als `/` werken, en hoofdletters maken niet uit. Een map selecteert al zijn onderdelen, zonder de `StartsWith`-fout van XPacker: `Bomen` selecteert niet ook `Bomen2`.
- **Waarden.** De namen komen uit `PropertyMap`, bijvoorbeeld `{"Shadow":"None","LODOut":300.5,"Render":true}`. Enums geef je bij naam, getallen met een punt. Bij een onbekende naam stopt de tool met een lijst van geldige namen. Een verkeerd type wordt geweigerd, zonder gedeeltelijke wijziging.
- **Alles of niets.** Een schrijfactie valideert eerst alle wijzigingen. Pas daarna wordt er iets weggeschreven. Faalt het wegschrijven halverwege, dan zet de backup automatisch alles terug.
- **Resultaat.** Altijd JSON met `ok`, `data`, `warnings[]` en bij een fout `error.code` + `error.message`. Foutcodes zijn onder meer `XPACKER_RUNNING`, `CHANGED_ON_DISK`, `UNKNOWN_PROPERTY`, `INVALID_VALUE`, `NO_MATCH`, `READ_ONLY_VERSION`, `RTB_RUNNING` en `NO_XPACK_OPEN`.
- **Grote lijsten.** Lijsten geven standaard maximaal 200 items terug, met `total` en `next_offset`.

**CLI (zelfde functies)**

```text
xpk list objects  --xpack Veluwe --select "Bomen\**" --fields Shadow,LODOut [--json]
xpk get  material --xpack Veluwe Kerbs\Kerb_RW
xpk set  objects  --xpack Veluwe --select "Bomen\**" Shadow=None LODOut=300 [--apply] [--force]
xpk validate      --xpack Veluwe [--rules texture-missing,unused-material]
xpk backups       --xpack Veluwe [--restore <id>]
xpk serve         start de MCP-server (stdio)
```

Zonder `--apply` is elke schrijfopdracht een proefdraai.

## Fase 0: spike en go/no-go

**Doel.** Bewijzen dat RaceBits zonder het scherm van XPacker een XPack kan openen, één eigenschap kan wijzigen en kan opslaan, en dat XPacker het resultaat foutloos opent. Werk altijd op een **kopie** van een XPack. Inschatting: 1 tot 2 sessies.

- [ ] **0.1 Omgeving**
  - Installeer de .NET 8 SDK (`winget install Microsoft.DotNet.SDK.8`), Git for Windows en Claude Code.
  - Draai `_FbxDedupePatch\herstel.bat`. Controleer daarna dat de SHA-256 van RaceBits.dll begint met `3d9105bb`.
  - Maak `C:\dev\xpk` aan, met `git init`, `Directory.Build.props` en een lege solution met de drie projecten.
  - Controle: `dotnet build` slaagt.
- [ ] **0.2 RtbEnvironment**
  - `RtbEnvironment.Initialize(XpkConfig cfg)` regelt het laden: `AssemblyResolve` alleen voor RTB-assemblies, `SetDllDirectory(cfg.RtbDir)`, een headless D3D11-device en `Globals.Initialize(device, rtbDir, Documenten\Race Track Builder)`.
  - Properties: `RaceBitsVersion`, `RaceBitsSha256`, `XPacksDirs`.
  - Test `Initialize_LoadsRaceBits_ReportsOriginalHash`: de SHA-256 begint met `3d9105bb`.
- [ ] **0.3 Openen, wijzigen, opslaan**
  - Tijdelijk commando `xpk spike <xpackmap>`.
  - Open de XPack in de volgorde van XPacker: `new XPack(path)`, `LoadFromFolder`, `VerifyAndSetReferences` en `FillEmptyDefaultListsFromAllItems`. Toon de aantallen.
  - Verhoog `LODOut` van één object met 1 en roep `CreateObject(obj, false)` aan.
  - Leg de diff van de XML-bestanden vast, ook van een `CreateObject` zonder wijziging (round-trip).
- [ ] **0.4 Waar staan de XPacks?** Bepaal welke map XPacker gebruikt bij openen en welke `ZipTo` als doel krijgt. Kandidaten zijn `Globals.ApplicationsXPacksFolder` en `Documenten\Race Track Builder`.
- [ ] **0.5 MCP SDK op net48**
  - Een hello-tool met het pakket `ModelContextProtocol` (stdio).
  - Registreer met `claude mcp add --transport stdio xpk-hello -- C:\dev\xpk\out\xpk.exe serve`.
  - Roep de tool aan vanuit Claude Code.
  - Lukt het niet, dan gaat de keuze naar een eigen minimale JSON-RPC-lus.
- [ ] **0.6 Verkenning voor latere fases**, zonder scherm testen:
  - `ImportFBX` met een klein FBX-bestand
  - `ZipTo` naar een tijdelijke map
  - `CreateMaterialIconFile`
  - of object-iconen uit RaceBits komen of uit XPacker zelf
  - de laadtijd en het geheugengebruik van je grootste XPack
- [ ] **0.7 Vastleggen**
  - In `docs/racebits-api.md`: aanroepen, volgorde, betekenis van parameters, threading en valkuilen.
  - In `docs/xpack-format.md`: mappen, welke bestanden bij een object, materiaal en String Object horen, en waar de mesh-data staat.

**Go/no-go**

| Uitkomst | Voorwaarde | Vervolg |
| --- | --- | --- |
| Go, route A | 0.3 werkt, XPacker toont de nieuwe LODOut en er zijn geen onverwachte wijzigingen in andere bestanden | Fase 1 |
| Route A2 | `Globals.Initialize` of `LoadFromFolder` faalt zonder venster | Een onzichtbaar WinForms-venster als host gebruiken, net als XPacker. Daarna 0.3 opnieuw. |
| Route B | A2 lukt niet binnen 1 sessie | Plan bijstellen: XML direct lezen en schrijven op basis van `xpack-format.md`. Import en zip vervallen dan of blijven in XPacker. |

**Acceptatie door jou:**

- Open de kopie-XPack in XPacker en controleer dat de gewijzigde LODOut te zien is.
- Lees de go/no-go-conclusie in `docs/racebits-api.md`.

## Fase 1: lezen en CLI

**Doel.** Je kunt een XPack openen en alle objecten, String Objects, materialen en textures opvragen via de CLI. Er wordt nog niets geschreven. Inschatting: 1 tot 2 sessies.

**Interfaces die latere fases gebruiken**

```csharp
enum ItemKind { Object, StringObject, Material, Texture }

sealed class XpkConfig { string RtbDir; string BackupDir; string[] KnownRaceBitsSha256; bool AllowUnknownRaceBits;
                         static XpkConfig Load(string path = null); }   // %APPDATA%\xpk\config.json
sealed class VersionGuard { bool IsReadOnly { get; } string Reason { get; } static VersionGuard Check(RtbEnvironment env, XpkConfig cfg); }

sealed class XPackSession {
  static XPackSession Open(RtbEnvironment env, string nameOrPath);
  XPackSummary Summary();
  void Reload();
  bool ChangedOnDisk();                     // vergelijkt mtime + grootte met momentopname
  internal RaceBits.XPack Pack { get; }
}

sealed class PropertyDef { string Name; Type ValueType; bool AcRelevant; object Get(object item); void Set(object item, object value); }
static class PropertyMap { IReadOnlyList<PropertyDef> For(ItemKind kind); object ParseValue(PropertyDef def, string raw); object ParseValue(PropertyDef def, JsonElement raw); }

sealed class Selector { static Selector Parse(string pattern); bool Matches(string itemPath); }

static class Query {
  IReadOnlyList<ItemInfo> List(XPackSession s, ItemKind kind, Selector sel = null, string[] fields = null, int offset = 0, int limit = 200);
  ItemInfo Get(XPackSession s, ItemKind kind, string path);
}
```

**Taken (test-first, één commit per taak)**

- [ ] **1.1 Config en VersionGuard.** Tests:
  - `UnknownHash_IsReadOnly`
  - `KnownHash_IsWritable`
  - `AllowUnknown_IsWritable_WithWarning`
- [ ] **1.2 XPackSession.** Integratietests op de fixture:
  - `Open_Fixture_CountsMatch`: 3 objecten, 3 materialen, 1 String Object
  - `ChangedOnDisk_AfterTouch_IsTrue`
  - `Reload_ResetsSnapshot`
  - `Open_PathWithSpacesAndAccents`
- [ ] **1.3 PropertyMap.** De whitelist komt uit fase 0. `AcRelevant` volgt de simulatortabel uit de handleiding. Tests:
  - `Object_HasCoreFields`: Render, Shadow, ShadowCast, LODIn, LODOut, Purpose, Collisions, Driveable
  - `ParseValue_DecimalPoint_UnderDutchCulture`: met `CurrentCulture = nl-NL` wordt `"0.6"` 0.6f
  - `ParseValue_Comma_Accepted`: `"0,6"` wordt 0.6f
  - `ParseValue_Ambiguous_Rejected`: `"1.000,5"` geeft `INVALID_VALUE`
  - `ParseValue_EnumByName_CaseInsensitive`
- [ ] **1.4 Selector.** Unittests:
  - `Folder_DoesNotMatchSiblingWithSamePrefix`: `Bomen` matcht niet `Bomen2\X`
  - `Star_MatchesOneLevel`
  - `DoubleStar_MatchesDeep`
  - `Slash_And_Backslash_Equivalent`
  - `Regex_Prefix`
  - `CaseInsensitive`
- [ ] **1.5 Query en snapshots.** `TextureInfo` leest breedte en hoogte uit de bestandsheader, voor DDS (128 bytes) en PNG, zonder D3D. Daarbij komen MD5 en `UsedBy[]`. Tests:
  - `ListObjects_WithSelector_ReturnsOnlyMatches`
  - `GetMaterial_HasTexMapsAndUsageCount`
  - `Textures_KerbVariants_DifferentMd5`
- [ ] **1.6 CLI.** De commando's `list`, `get` en `info`, met `--json`. Tekstuitvoer als tabel; alle getallen zonder cultuur. Test `Cli_ListJson_IsValidJson`.

**Verificatie**

- `dotnet test --filter Category!=Integration`: alles groen
- `dotnet test`: alles groen

**Acceptatie door jou.** Vergelijk `xpk list objects --xpack <kopie> --select "<map>\**"` met XPacker. Controleer 5 objecten en 3 materialen veld voor veld.

## Fase 2: schrijven en veiligheid

**Doel.** Eigenschappen van veel items tegelijk wijzigen, met proefdraai, backup en terugzetten. Hiermee is de Shadow-dropdown-bug van XPacker bij map-bewerking omzeild. Inschatting: 2 sessies.

**Interfaces**

```csharp
sealed class Change { string ItemPath; string Property; object Old; object New; }
sealed class ChangeSet { ItemKind Kind; IReadOnlyList<Change> Changes; int MatchedItems; IReadOnlyList<string> FilesToWrite; }

static class Writer {
  ChangeSet Plan(XPackSession s, ItemKind kind, Selector sel, IDictionary<string, object> values);   // geen bijwerkingen
  WriteResult Apply(XPackSession s, ChangeSet cs, bool force = false);                               // backup, toepassen, wegschrijven
}
sealed class WriteResult { string BackupId; IReadOnlyList<string> FilesWritten; IReadOnlyList<string> Warnings; }

interface IProcessLister { bool IsRunning(string processName); }      // te vervangen in tests
static class ProcessGuard { void EnsureSafe(IProcessLister p, bool force); }   // XPacker.exe draait: XPACKER_RUNNING

static class Backup {
  string Create(XPackSession s, IEnumerable<string> files);              // geeft het backup-id terug: yyyyMMdd-HHmmss
  IReadOnlyList<BackupInfo> List(string xpackName);
  IReadOnlyList<string> Restore(string xpackName, string backupId);
}
```

**Wegschrijven per itemsoort**

| Itemsoort | Aanroep |
| --- | --- |
| Object | `CreateObject(obj, false)` |
| Materiaal | `CreateMaterialXML(mat)` |
| String Object | `CreateSObject(sobj, <tweede argument uit fase 0>)` |

Geen andere aanroepen, tenzij fase 0 heeft aangetoond dat ze nodig zijn.

**Taken**

- [ ] **2.1 Plan.** Tests:
  - `Plan_DoesNotModifyPack`: de snapshots voor en na zijn gelijk
  - `Plan_UnknownProperty_UNKNOWN_PROPERTY`
  - `Plan_SkipsUnchangedValues`
  - `Plan_NoMatch_NO_MATCH`
  - `Plan_ReadOnlyVersion_READ_ONLY_VERSION`
- [ ] **2.2 ProcessGuard en controle op schijfwijzigingen.** Tests:
  - `Apply_XPackerRunning_Refused`
  - `Apply_Force_Allowed`
  - `Apply_AfterExternalEdit_CHANGED_ON_DISK`
- [ ] **2.3 Backup.** Tests:
  - `Backup_ThenRestore_FilesByteIdentical`
  - `Restore_UnknownId_Error`
- [ ] **2.4 Apply.**
  - De volgorde is vast: guard, backup, toepassen in het geheugen, wegschrijven en een nieuwe momentopname.
  - Bij een exception wordt de backup teruggezet en daarna `Reload()` uitgevoerd.
  - Tests:
    - `RoundTrip_NoChanges_FilesIdentical`. Als de bytes afwijken door getalnotatie, dan semantisch vergelijken via genormaliseerde XML en dit vastleggen in `racebits-api.md`.
    - `Apply_LODOut_OnlyTargetFilesChanged`
    - `Apply_ShadowEnum_OnFolder_AllItemsChanged`
    - `Apply_FailureMidway_RollsBack`, met een gesimuleerde fout na het tweede bestand
- [ ] **2.5 Materialen en String Objects.** Dezelfde tests als 2.4, voor `Material` en `StringObject`.
- [ ] **2.6 CLI.** Het commando `set` (zonder `--apply` een proefdraai) en het commando `backups`.
  - De proefdraai toont het aantal items, de wijzigingen (maximaal 50 regels plus een totaal) en de bestanden die geschreven gaan worden.

**Acceptatie door jou**

1. Zet op de kopie-XPack `Shadow=None` op een hele map.
2. Open XPacker: alle objecten in die map tonen None, en de rest is ongewijzigd.
3. Draai `xpk backups --restore <id>`.
4. Open XPacker opnieuw: alles is weer zoals eerst.

## Fase 3: MCP-server

**Doel.** Claude Code en Claude Desktop kunnen de lees- en schrijftools uit fase 1 en 2 gebruiken. Na deze fase is er een eerste bruikbare versie. Inschatting: 1 sessie.

**Taken**

- [ ] **3.1 McpHost**
  - `xpk serve` start de stdio-server.
  - De allereerste regel is `Console.SetOut(Console.Error)`. Het protocol gebruikt de echte stdout-stream, zodat RaceBits of Serilog het protocol niet kunnen verstoren.
  - De server stopt netjes bij het einde van stdin. Volgens de documentatie sluit Claude Code op Windows stdin en beëindigt het de processen na 300 ms.
  - Test `Serve_RaceBitsLogLine_NotOnStdout`.
- [ ] **3.2 Tools**
  - Eén bestand per groep, in `src/Xpk.Mcp/Tools/`:
    - `XPackTools`
    - `QueryTools`
    - `WriteTools`
    - `BackupTools`
  - Elke tool is een dunne laag over Core. Fouten gaan als `isError` met `error.code` terug, niet als exception.
- [ ] **3.3 Toolbeschrijvingen**
  - Engels, kort en expliciet.
  - Elke schrijftool krijgt deze zin: `Defaults to dry_run=true. Show the change report to the user and only call again with dry_run=false after they confirm.`
  - De velden staan in het inputschema, met enums waar die bestaan.
- [ ] **3.4 Gelijktijdigheid.** Eén `SemaphoreSlim` rond de sessie, zodat tool-aanroepen strikt na elkaar worden verwerkt.
- [ ] **3.5 Registratie**
  - Projectniveau: `.mcp.json` in de repo, met `"xpacker": {"command": "C:\\dev\\xpk\\out\\xpk.exe", "args": ["serve"]}`.
  - Voor al je projecten: `claude mcp add --transport stdio --scope user xpacker -- C:\dev\xpk\out\xpk.exe serve`.
  - Controle: `claude mcp list` toont `✔ Connected`.
  - Claude Desktop: dezelfde server in de config-file van Claude Desktop, onder `mcpServers`.
- [ ] **3.6 End-to-end test**
  - Een xUnit-test start `xpk.exe serve` met de MCP-client uit de SDK.
  - Tests:
    - `ToolsList_ContainsPhase1And2Tools`
    - `ObjectsSet_DefaultIsDryRun_NoFilesChanged`
    - `ObjectsSet_Apply_WritesAndReturnsBackupId`

**Acceptatie door jou.** Vraag in Claude Code: "Zet in de kopie-XPack alle objecten onder Bomen op Shadow None."

- Claude toont eerst het proefdraairapport.
- Pas na jouw akkoord past Claude het toe.
- XPacker toont het resultaat.

## Fase 4: validatie

**Doel.** Eén aanroep geeft alle bekende problemen in een XPack, gesorteerd op ernst, met een voorstel per probleem. Inschatting: 1 tot 2 sessies.

**Interface**

```csharp
enum Severity { Error, Warning, Info }
sealed class Finding { string RuleId; Severity Severity; string ItemPath; string Message; string Suggestion; }
interface IRule { string Id { get; } Severity Severity { get; } IEnumerable<Finding> Check(XPackSession s); }
static class Validator { IReadOnlyList<Finding> Run(XPackSession s, string[] ruleIds = null, Selector sel = null); }
```

**Regels in de eerste versie**

| Regel-id | Ernst | Controle | Bron |
| --- | --- | --- | --- |
| `texture-missing` | Error | Een TexMap verwijst naar een bestand dat niet bestaat | bestandscontrole |
| `material-required-texture-missing` | Error | `Mat.GetMissingRequiredTextures()` is niet leeg | RaceBits |
| `child-missing-object` | Error | Een child-template verwijst naar een object dat niet bestaat | `HierarcyTemplate` |
| `object-lod-order` | Error | LODOut > 0 en LODOut ≤ LODIn | eigenschappen |
| `child-random-rotation-ignored` | Warning | Een child-template heeft een rotatie in RandomLimits. Die heeft geen effect (bug 9). | bugrapport |
| `texture-not-pow2` | Warning | Breedte of hoogte is geen macht van 2 | header |
| `texture-duplicate-content` | Info | Dezelfde MD5 onder verschillende namen | MD5 |
| `material-unused` | Info | `MaterialInUse(mat)` is false | RaceBits |
| `ac-irrelevant-field` | Info | Een veld dat AC niet gebruikt heeft een andere waarde dan de standaard, zoals rF\_\* | simulatortabel uit de handleiding |

Nieuwe regels voeg je toe als een klasse in `Validate/Rules/`. Er is geen registratie nodig: de klassen worden via reflectie gevonden.

**Taken**

- [ ] **4.1 Validator en regels.**
  - Elke regel heeft één test op een fixture-variant waarin de fout bewust is aangebracht.
  - Elke regel heeft één test die op de schone fixture niets vindt.
  - Voorbeelden: `TextureMissing_Detected`, `TextureMissing_CleanFixture_None`.
- [ ] **4.2 Uitvoer.** De volgorde is Error, Warning, Info, en daarbinnen op item-pad.
  - De CLI toont per regel een totaal.
  - De MCP-tool `validate` geeft maximaal 200 bevindingen, met `total`.

**Acceptatie door jou.** Draai `validate` op een kopie van je echte XPack en loop 10 bevindingen na. Elke onterechte melding wordt een testgeval voordat de regel wordt aangepast.

## Fase 5: dupliceren en kopiëren

**Doel.** Objecten, materialen en String Objects dupliceren, en eigenschappen van een bron-item kopiëren naar andere items. Het kopiëren gebruikt de Writer uit fase 2. Inschatting: 1 tot 2 sessies.

**Aanpak per itemsoort.** Te bevestigen in de eerste taak.

| Itemsoort | Dupliceren via |
| --- | --- |
| Object | De XML van de bron lezen, `BObject.LoadFromXML(node, path)` op een nieuw object aanroepen, `Path` en `Name` zetten via `BObjects.GetUniquePath(...)`, toevoegen aan `Objects`, `CreateObject(nieuw, false)` en daarna `Reload()` |
| Materiaal | `XPack.CopyMaterial(mat)`, nieuwe naam en pad, `CreateMaterialXML` en `CreateMaterialIconFile`. Textures worden gedeeld, niet gekopieerd. |
| String Object | Zoals bij Object, met `CreateSObject` |

**Interfaces**

```csharp
static class Duplicate { DuplicateResult Run(XPackSession s, ItemKind kind, string sourcePath, string newName, string folder = null, bool dryRun = true); }
sealed class DuplicateResult { string NewPath; IReadOnlyList<string> FilesToWrite; string BackupId; }
static class CopyProperties { ChangeSet Plan(XPackSession s, ItemKind kind, string fromPath, Selector to, string[] fields); }   // daarna Writer.Apply
```

**Taken**

- [ ] **5.1 Verkenning.**
  - Bevestig de aanpak per itemsoort op de fixture en leg die vast in `racebits-api.md`.
  - Ook vast te leggen: worden mesh-data en child-templates mee gekopieerd?
- [ ] **5.2 Duplicate.** Tests:
  - `DuplicateObject_NewFileExists_OriginalByteIdentical`
  - `DuplicateObject_NameTaken_GetsUniquePath`
  - `DuplicateMaterial_TexturesShared_NotCopied`
  - `DuplicateObject_WithChildren_TemplatesCopied`
- [ ] **5.3 CopyProperties.** Tests:
  - `Copy_OnlyListedFields`
  - `Copy_ToFolder_AllTargetsChanged`
  - `Copy_ChildTemplates_DeepCopied`. De child-templates zijn een apart veld, `HierarcyTemplate`, en worden alleen gekopieerd als je dat expliciet vraagt.
- [ ] **5.4 CLI en MCP.**
  - Commando's `duplicate` en `copy`.
  - Tools `object_duplicate`, `material_duplicate`, `sobject_duplicate` en `properties_copy`.
  - Beide met proefdraai als standaard.

**Acceptatie door jou**

- Dupliceer een boom en een kerbmateriaal.
- Kopieer de LOD- en schaduwinstellingen van één boom naar de hele map.
- Open XPacker en controleer.
- Plaats het duplicaat in RTB en controleer of het daar werkt.

## Fase 6: FBX-import met hergebruik

**Doel.** Een FBX importeren waarbij bestaande materialen en textures hergebruikt worden als de naam overeenkomt en de inhoud van de texture identiek is. Bij een andere inhoud komt er een nieuw materiaal met een tag, bijvoorbeeld voor kerb-kleuren. Dit vervangt de IL-patch volledig. Inschatting: 2 sessies.

**Wat `ImportFBX` nu doet** (uit de analyse van RaceBits.dll):

1. `RaceFbx.Scene.Import(fbx)` leest het bestand.
2. Er komt een nieuw object, met de bestandsnaam als naam. `GetUniquePath` zorgt dat die naam uniek is.
3. Per materiaal komt er een nieuwe `Mat` in `Materials\Objects\<object>\`. De texture wordt gekopieerd naar `Textures\Objects\<object>\`.
4. Direct daarna volgen `CreateMaterialXML` en `CreateMaterialIconFile`, en aan het eind `CreateObject`.

Elk object krijgt dus eigen kopieën van materialen en textures. Bestaande bestanden van andere objecten worden niet overschreven. Daarom kan de dedupe veilig **na** de import gebeuren.

**Aanpak**

1. **Analyse (de proefdraai).**
   - `RaceFbx.Scene.Import` levert de materialen en `FirstTextureFileName` per materiaal.
   - Daarvan worden de MD5-hashes berekend en vergeleken met de bestaande materialen en textures.
   - Het rapport geeft per materiaal: `reuse <pad>`, `new` of `new <naam>_<tag>`.
   - Er wordt niets geschreven.
2. **Import.** ProcessGuard, `ImportFBX(fbx, folder)`, en de nieuwe mappen worden vastgelegd voor een eventuele rollback.
3. **Dedupe.**
   - Een match: `PointThingsToNewMaterial(nieuw, bestaand)` en `RemoveMaterialAndFiles(nieuw)`. Daarna wordt de gekopieerde texture verwijderd als niets hem meer gebruikt.
   - Een gelijke naam met een andere inhoud: het materiaal wordt hernoemd naar `<naam>_<md5[0..6]>`.
4. **Wegschrijven.** `CreateObject(obj, false)`, zodat de omgezette materiaalverwijzingen worden opgeslagen. Daarna `Reload()` en `validate` op het nieuwe object.

**De matchregels komen uit de patch.** Neem ze over uit `_FbxDedupePatch\src\ImportDedupe.cs`, met deze regels:

- Kandidaatnamen zijn `naam` en `naam_<tag>`.
- Met dezelfde extensie wordt er vergeleken op grootte en MD5.
- Met een andere extensie (png of dds) alleen op naam.
- Zonder texture is de kandidaat `Blank_<naam>`.
- Het zoeken gebeurt over de hele XPack.

**Interfaces**

```csharp
sealed class FbxPlanItem { string FbxMaterial; string TextureFile; string Md5; string Action; string TargetPath; }   // Action: reuse | new | new-tagged
static class FbxImport {
  IReadOnlyList<FbxPlanItem> Analyze(XPackSession s, string fbxPath);
  FbxImportResult Run(XPackSession s, string fbxPath, string folder, bool reuse = true, bool dryRun = true);
}
static class MaterialMerge { MergeResult Run(XPackSession s, string keepPath, string[] removePaths, bool dryRun = true); }
```

**Taken**

- [ ] **6.1 Test-FBX's.** Jij maakt ze in Blender en zet ze in `tests/Fixtures/fbx/`:
  - `kerb_red.fbx` en `kerb_blue.fbx`: zelfde materiaalnaam `Kerb`, zelfde texturenaam `kerb.png`, andere inhoud
  - `kerb_red_again.fbx`: identiek aan rood
  - `plain.fbx`: zonder texture
- [ ] **6.2 Analyze.** Tests:
  - `Analyze_Identical_ReportsReuse`
  - `Analyze_SameNameDifferentContent_ReportsTagged`
  - `Analyze_NoTexture_MatchesBlank`
  - `Analyze_WritesNothing`
- [ ] **6.3 Run en dedupe.** Tests:
  - `Import_RedThenRedAgain_SecondReusesFirst`
  - `Import_RedThenBlue_BlueTagged`
  - `Import_ObjectXml_PointsToExistingMaterial`
  - `Import_Failure_RemovesNewFolders`
- [ ] **6.4 MaterialMerge.** Hiermee ruim je bestaande dubbele materialen op. Een kandidaat haal je uit de regel `texture-duplicate-content`. Test `Merge_RepointsAllObjects_RemovesFiles`.
- [ ] **6.5 CLI en MCP.** Het commando `import` en de tools `fbx_import` en `materials_merge`.

**Acceptatie door jou**

1. Importeer drie kerb-varianten.
2. Er zijn twee materialen: Kerb en Kerb\_\<tag>.
3. De nieuwe objecten gebruiken het bestaande materiaal.
4. In RTB zien de kerbs er goed uit.

## Fase 7: zip naar RTB, iconen en afronding

**Doel.** De keten sluiten: bewerken, valideren en inpakken voor RTB, zonder XPacker. Daarna volgt release 1.0. Inschatting: 1 sessie.

**Taken**

- [ ] **7.1 ZipToRtb.**
  - De doelmap komt uit fase 0 (0.4), en daarna roept de tool `ZipTo(pad)` aan.
  - Het doel is `<map>\<XPack-naam>.zip`, net als in XPacker.
  - Er wordt eerst `validate` gedraaid. Bij een bevinding met ernst Error stopt de tool, tenzij je `force` meegeeft.
  - Als `RaceTrackBuilder.exe` draait, stopt de tool met `RTB_RUNNING`, want de zip kan dan in gebruik zijn.
  - De oude zip gaat eerst naar de backupmap.
  - Tests:
    - `Zip_ErrorsPresent_Refused`
    - `Zip_Creates_FileContainsObjectXml`
    - `Zip_OldZipBackedUp`
- [ ] **7.2 Iconen.**
  - Werkt `CreateMaterialIconFile` zonder scherm (fase 0, 0.6), dan wordt het icoon bijgewerkt na elke materiaalwijziging.
  - Object-iconen tekent XPacker zelf. Komen ze niet uit RaceBits, dan blijft dat in XPacker. De tool geeft dan de waarschuwing `icon-stale`.
- [ ] **7.3 Release.**
  - `dotnet build -c Release` naar `C:\dev\xpk\out\`.
  - De README in het Nederlands beschrijft:
    - installatie
    - de config
    - MCP koppelen
    - backups terugzetten
    - wat te doen na een RTB-update
  - Git-tag `v1.0`.
- [ ] **7.4 Na een RTB-update.**
  - Een script `tools/after-rtb-update.ps1` draait de integratietests.
  - Bij groen voegt het de nieuwe SHA-256 toe aan `KnownRaceBitsSha256`.
  - Bij rood blijft xpk alleen-lezen en toont het script welke tests falen.

**Acceptatie door jou.** De volledige keten op een kopie, alleen via Claude:

1. FBX importeren
2. bulk-instellingen zetten
3. valideren
4. zip maken
5. de baan in RTB openen en het resultaat zien

## Teststrategie

De tests zitten in drie lagen. Elke taak is pas klaar als de tests groen zijn en jouw acceptatiecheck slaagt.

| Laag | Wat | Wanneer | Commando |
| --- | --- | --- | --- |
| Unit | Selector, PropertyMap-conversies, ChangeSet en regels met nep-data, zonder RTB | bij elke wijziging | `dotnet test --filter Category!=Integration` |
| Integratie | Echte RaceBits op een verse kopie van de fixture | aan het eind van elke taak | `dotnet test` |
| Acceptatie | Jij controleert in XPacker en RTB | aan het eind van elke fase | de checklist per fase |

**Vaste regel voor integratietests.** Een test werkt nooit op `tests/Fixtures/source`. Hij kopieert eerst naar een eigen map onder `tests/Fixtures/work\<testnaam>`.

**Review Focus.** Dit zijn de vijf situaties die het eerst misgaan in de praktijk. Ze hebben elk een eigen test in de fase die de code bevat.

| Situatie | Verwacht gedrag | Test (fase) |
| --- | --- | --- |
| XPacker staat open en je slaat daar iets op terwijl de server draait | Schrijven stopt met `CHANGED_ON_DISK` en de melding om eerst te herladen. Er gaat geen werk verloren. | `Apply_AfterExternalEdit_CHANGED_ON_DISK` (2) |
| Nederlandse Windows: `0,6` of `0.6` ingetypt | Beide worden 0,6. `1.000,5` wordt geweigerd, nooit stil 1000,5 of 6. | `ParseValue_*` (1) |
| De selector raakt niets, of veel meer dan bedoeld (`Bomen` naast `Bomen2`) | `NO_MATCH`, of een proefdraai die het aantal items vooraan toont, met een waarschuwing boven 50 items | `Folder_DoesNotMatchSiblingWithSamePrefix` (1), `Plan_NoMatch_NO_MATCH` (2) |
| RTB-update via Steam vervangt RaceBits.dll | Alleen-lezen, met de duidelijke melding `READ_ONLY_VERSION`, tot de update-check groen is | `UnknownHash_IsReadOnly` (1), `Plan_ReadOnlyVersion_READ_ONLY_VERSION` (2) |
| Paden met spaties, accenten of een door OneDrive omgeleide map Documenten | Openen en schrijven werken. Bestanden die alleen online staan, geven een duidelijke fout. | `Open_PathWithSpacesAndAccents` (1) |

## Risico's

Het grootste risico is fase 0: werkt RaceBits zonder het scherm van XPacker? Daarom staat dat vooraan, en is er een terugvaloptie.

| Risico | Kans | Impact | Maatregel |
| --- | --- | --- | --- |
| RaceBits werkt niet headless, vanwege Direct3D of WinForms | middel | hoog | Route A2 (onzichtbaar venster), anders route B (XML direct). Beslist in fase 0. |
| Je werkt in XPacker en in xpk tegelijk aan dezelfde XPack | middel | hoog: werk kwijt | ProcessGuard, controle op schijfwijzigingen en een backup vóór elke schrijfactie |
| Claude voert een te brede bulk-wijziging uit | middel | middel | Proefdraai als standaard, de toolbeschrijving eist bevestiging, backups en terugzetten |
| Een RTB-update wijzigt RaceBits.dll | middel | middel | VersionGuard (alleen-lezen), integratietests, alleen openbare namen gebruiken, het script na een update |
| `CreateObject` schrijft niet byte-identiek terug, bijvoorbeeld door getalnotatie | middel | laag | Semantisch vergelijken in tests en vastleggen in `racebits-api.md` |
| De MCP SDK geeft versieconflicten op net48 | laag | middel | Binding redirects automatisch laten genereren, anders een eigen JSON-RPC-lus |
| Het proces wordt beëindigd tijdens het schrijven, omdat Claude Code na 300 ms de processen stopt | laag | middel | Schrijfacties zijn klaar voordat het antwoord terugkomt. Bij de volgende start: onafgemaakte backup gevonden, aanbieden om terug te zetten. |
| Object-iconen kunnen alleen in XPacker gemaakt worden | middel | laag | Waarschuwing `icon-stale`. Icoon bijwerken in XPacker. |
| Licentie of EULA van RTB | laag | middel | Alleen eigen gebruik, geen RTB-bestanden in de repo, niet verspreiden |

## Werken met Claude Code

De werkwijze is één fase per sessie, met een vaste CLAUDE.md en een korte startprompt. Claude Code leest de CLAUDE.md bij elke start, dus de harde regels hoef je niet te herhalen.

**Eenmalig voorbereiden**

1. Installeer Claude Code in PowerShell met `irm https://claude.ai/install.ps1 | iex`. Installeer daarnaast [Git for Windows](https://git-scm.com/downloads/win), zodat Claude Code Bash kan gebruiken, en de .NET 8 SDK. Zie de [installatiedocs](https://code.claude.com/docs/en/setup).
2. Draai `_FbxDedupePatch\herstel.bat`, zodat de originele RaceBits.dll terug is.
3. Kopieer een XPack naar `C:\dev\xpk-work\`. Dat is je werkkopie.
4. Maak `C:\dev\xpk` aan, zet de CLAUDE.md hieronder erin en dit plan als `docs/PLAN.md`.
5. Open een terminal in `C:\dev\xpk` en start `claude`.

**CLAUDE.md (sjabloon)**

```markdown
# xpk – XPacker API & MCP

Windows-tool die XPacks van Race Track Builder leest en wijzigt via RaceBits.dll.
CLI + MCP-server (`xpk serve`). Plan: docs/PLAN.md. Bevindingen: docs/racebits-api.md, docs/xpack-format.md.

## Harde regels
- Nooit schrijven in C:\Program Files (x86)\Steam\steamapps\common\Race Track Builder.
- RTB-DLL's nooit kopiëren naar out/ en nooit committen. References via HintPath + <Private>false</Private>.
- Alleen werken op kopieën: C:\dev\xpk-work\ of tests/Fixtures/work/. Nooit op tests/Fixtures/source/.
- Geen gedecompileerde RTB-code in de repo.
- In `serve`-modus komt niets op stdout behalve MCP-berichten.
- Getallen altijd met CultureInfo.InvariantCulture.
- Schrijfacties: dry_run standaard true. Backup voor elke schrijfactie.

## Techniek
- .NET Framework 4.8, x64, SDK-stijl csproj, Microsoft.NETFramework.ReferenceAssemblies.
- Tests: xUnit 2.x. Integratietests krijgen [Trait("Category","Integration")].
- MCP: officiële C# SDK (ModelContextProtocol), stdio.
- Code, identifiers en toolbeschrijvingen in het Engels. README en meldingen voor de gebruiker in het Nederlands.

## Werkwijze
- Werk per taak uit docs/PLAN.md, test-first: test schrijven, zien falen, implementeren, groen.
- Eén commit per taak: `fase N.M: <wat>`.
- Na elke taak: `dotnet build` en `dotnet test --filter Category!=Integration`. Aan het eind van de taak ook `dotnet test`.
- Onbekend gedrag van RaceBits: eerst een klein experiment, dan vastleggen in docs/racebits-api.md, dan pas bouwen.
- Wijk je af van het plan, leg dan de beslissing en de reden vast in docs/PLAN.md.
- Stop en vraag bij: wijzigingen buiten de taak, een nieuwe dependency, twijfel over het XPack-formaat.
- Sluit elke fase af met de acceptatiechecklist voor Jeroen.

## Commando's
- dotnet build
- dotnet test --filter Category!=Integration
- dotnet test
- out\xpk.exe list objects --xpack C:\dev\xpk-work\<XPack> --json
```

**Startprompt fase 0**

```text
Lees CLAUDE.md en docs/PLAN.md. Voer fase 0 uit, taak 0.1 tot en met 0.7.
Werk alleen op de kopie C:\dev\xpk-work\<XPack>. Laat na elke taak zien wat je hebt gedaan
en wat ik moet controleren. Sluit af met het go/no-go-advies in docs/racebits-api.md.
```

**Startprompt fase 1 tot en met 7** (vervang N)

```text
Lees CLAUDE.md, fase N in docs/PLAN.md, docs/racebits-api.md en docs/xpack-format.md.
Werk fase N eerst uit in plan mode en wacht op mijn akkoord. Werk daarna taak voor taak,
test-first, één commit per taak. Sluit af met alle tests groen en de acceptatiechecklist voor mij.
```

**Vaste routine**

- Begin elke fase in plan mode (Shift+Tab).
- Gebruik `/clear` tussen fases. Dan begint Claude weer schoon, met de documenten als geheugen.
- Na jouw acceptatie: zet afwijkingen of nieuwe wensen als `- [ ]` onder de fase in `docs/PLAN.md`, voordat je de volgende fase start.
- Vanaf fase 3 test je de MCP-tools in dezelfde Claude Code-sessie, na `claude mcp list`.

## Open beslissingen

Deze punten wil ik van je weten voordat fase 0 start, of uiterlijk voor de fase die erbij staat. Bij elk punt staat wat er gebeurt als je niets kiest.

- [ ] **Bron voor de fixture** (fase 1): welke eigen XPack? Standaard: de kleinste die objecten met child objects en kerb-varianten heeft.
- [ ] **Repo** (fase 0): alleen lokaal of ook een privé-repo op GitHub? Standaard: lokaal.
- [ ] **Komma als decimaalteken in de CLI** (fase 1): accepteren? Standaard: `0,6` en `0.6` worden allebei geaccepteerd en dubbelzinnige waarden geweigerd.
- [ ] **Claude Desktop** (fase 3): ook koppelen, of alleen Claude Code? Standaard: allebei.
- [ ] **Import-patch** (fase 6): de IL-patch vervalt zodra fase 6 klaar is. Tot die tijd gebruik je de import in XPacker met de originele RaceBits, dus zonder dedupe. Alternatief: de v2-patch tijdelijk terugzetten en die hash toevoegen aan `KnownRaceBitsSha256`.
- [ ] **Extra itemsoorten** (na v1): Cross Sections en Decals zijn nu buiten scope. Toevoegen gaat volgens hetzelfde patroon, voor ongeveer 1 sessie per soort.

**Totale inschatting.** Grof 10 tot 14 Claude Code-sessies, plus jouw acceptatietests. Fase 0 bepaalt of dat klopt. Valt de uitkomst op route B, dan komt er een herzien plan.

**Bronnen**

- [Claude Code installatie](https://code.claude.com/docs/en/setup)
- [Claude Code en MCP](https://code.claude.com/docs/en/mcp)
- [ModelContextProtocol.Core op NuGet](https://www.nuget.org/packages/ModelContextProtocol.Core/2.0.0) (doelframeworks: netstandard2.0, net8.0 en hoger)
- Analyse van RaceBits.dll en XPacker.exe uit eerdere sessies: openbare API, `ImportFBX` en de opslagpaden
