# RaceBits-API: bevindingen fase 0

Vastgelegd op 25 okt 2026, op basis van experimenten met RaceBits.dll van v1950 en daarna van v2.0 (de doelversie) en reflectie op de openbare API (alleen namen en signaturen, geen gedecompileerde code). De experimenten zijn gedaan op kopieën: de werkkopie `C:\Users\Moor\xpk\NewXPackTest` (met backup) en kopieën in `tests\Fixtures\work\`. Voor en na elke run is gecontroleerd dat de RTB-map en `Documenten\Race Track Builder` niet zijn veranderd.

## Go/no-go-conclusie

**GO, route A (RaceBits zonder scherm), met RTB v2.0 als doel.** Besloten op 25 okt 2026, na de acceptatie door Jeroen: XPacker toont bij Billboards_L_1 LOD Out 2001, en alle andere objecten zijn ongewijzigd.

| Voorwaarde uit het plan | Uitkomst |
| --- | --- |
| RaceBits laadt zonder venster | Ja, op v1950 en op v2.0. `Globals.Initialize` met een headless D3D11-device (`DriverType.Hardware`, feature level 11_0) in 0,4 s. Een onzichtbaar WinForms-venster (route A2) is niet nodig. |
| XPack openen in de volgorde van XPacker | Ja. NewXPackTest in 0,2 s. De grootste eigen XPack (70 objecten, 381 materialen) in 1,4 s op v2.0 (v1950: 5,6 s koud, 1,1 s warm). |
| `LODOut` + 1 en `CreateObject(obj, false)` | Ja. Ten opzichte van een eenmaal opgeslagen versie verandert precies één regel in één bestand: `<LODOut>2000</LODOut>` wordt `<LODOut>2001</LODOut>`. Na opnieuw openen leest RaceBits 2001. |
| Geen onverwachte wijzigingen | Ja, op v2.0. De eerste keer opslaan normaliseert (standaardwaarden vallen weg, ontbrekende velden komen erbij); er gaat niets verloren en daarna is opslaan idempotent. Zie "Opslaan". |
| XPacker toont de nieuwe LODOut | Ja (acceptatie, gedaan op de v1950-werkkopie). |

Aandachtspunt voor fase 1: **een RaceBits-fout kan een modale dialoog openen** (MessageBox). In `serve`-modus blokkeert dat de server. Daarom komt er een bewaking: elke RaceBits-aanroep krijgt een time-out, en dialogen worden automatisch gesloten of voorkomen.

## RaceBits-versies

| | **v2.0 (doel, geïnstalleerd)** | v2000 eerder (`_FbxDedupePatch\RaceBits.original.dll`) | v1950 (eerder geïnstalleerd) |
| --- | --- | --- | --- |
| Steam-branch | `v2000`, build 26041913, RTB v2.0.0.0 | `v2000` beta, oudere build | `v1950`, build 25819081, RTB v1.9.5.6 |
| SHA-256 | `611efe142ff387bc17a6f0eb9c236d7d426a21b8f7c3fce2ce01c275a43694ff` | `3d9105bbe55b2c8de3ea2d87082a326c1677c2f53603cf896c4e581b88d26e24` | `944c3fe8fe825af1944b2649884966393233c60ed9d8e33c54a8d70add340f0a` |
| Grootte | 1 488 896 bytes | 1 488 896 bytes | 920 064 bytes |
| Gepatcht? | Nee | Nee (back-up van vóór de patch) | Nee |

- De FileVersion is altijd 1.0.0.0. Alleen de hash identificeert een build, en dat is wat VersionGuard moet gebruiken. `611efe14` is de eerste bekende hash.
- `611efe14` en `3d9105bb` hebben precies dezelfde openbare API (3999 members, 0 verschillen) en verschillen in 53 bytes: dezelfde code, opnieuw gebouwd. `herstel.bat` zet dus een oudere build terug en is niet meer nodig.
- RTB v2.1 komt eraan. Na die update is RaceBits alleen-lezen tot de integratietests groen zijn (plan, 7.4).

**API-verschillen v2.0 → v1950** (ter info; v1950 wordt niet ondersteund):

- Ontbreekt in v1950: `XPack.FillEmptyDefaultListsFromAllItems()`, `XPack.DefaultObjects/DefaultSObjects/DefaultWalls/DefaultDecals/DefaultTracks`, `Mat.GetMissingRequiredTextures()`, `Mat.GetRequiredTextureMaps()`, `Mat.SupportsAC` en verwanten, de velden `Mat.GrassFX_*`, `Mat.rf_Specular/rf_Roughness/rf_NormalIntensity/rf_AlbedoTint`, `Mat.TexMap7–9`, `Mat.Perlin*`.
- Nieuw in v1950: `Globals.UserXPacksFolder`, `XPack.IsOldBTBPack`, `XPack.SetIsThemePackFlag()`, `Mat.rF_DoubleSided`, `BObject.FacesWarning(ref string)`.
- Andere signatuur: `XPack.VerifyCrossSections(XPacks, List<string>)` in v2.0, `VerifyCrossSections(List<string>)` in v1950.
- Op v1950 verliest `CreateMaterialXML` de velden `rf_*` en `GrassFX_*` van een XPack die met v2.0 is gemaakt. Dat was de reden om v2.0 als doel te kiezen.
- `Xpk.Core` roept `FillEmptyDefaultListsFromAllItems` en `Globals.UserXPacksFolder` via reflectie aan, zodat de spike op beide versies draait. Vanaf fase 1 mag de code direct tegen v2.0 programmeren.

## Starten

```text
AppDomain.AssemblyResolve  alleen voor RaceBits, RaceFbx, FbxWrapper, RTBPlugins, Serilog, SharpDX* uit de RTB-map
SetDllDirectory(rtbDir)    voor libfbxsdk.dll en sharpdx_direct3d11_effects_x64.dll
new SharpDX.Direct3D11.Device(DriverType.Hardware)   (terugval: Warp)
RaceBits.Globals.Initialize(device, applicationFolder, projectsFolder)
```

- `applicationFolder` = de RTB-map. `Globals.ApplicationsXPacksFolder` wordt dan `<RTB>\XPacks`, een map die niet bestaat (de meegeleverde themes staan in `<RTB>\Themes`).
- `projectsFolder` = `Documenten\Race Track Builder`. Gebruik `Environment.SpecialFolder.MyDocuments`; die volgt de OneDrive-omleiding (`C:\Users\Moor\OneDrive\Documenten`).
- v2.0 heeft geen `Globals.UserXPacksFolder`. De zip-map is `<projectsFolder>\XPacks` (`RtbEnvironment.XPacksDirs`). In v1950 kwam `UserXPacksFolder` uit Mijn documenten, niet uit `projectsFolder`.
- `Initialize` en het openen schrijven niets in de RTB-map of in Documenten (gecontroleerd met momentopnames). De Serilog-logger van RaceBits is niet geconfigureerd en schrijft dus niets. `Log.txt` in de RTB-map komt van RTB en XPacker zelf.
- In de AssemblyResolve-handler moet je een assembly die al geladen is teruggeven. Een PowerShell-scriptblock als handler geeft een StackOverflow; schrijf hem in C#.
- RaceBits-typen mogen pas na het registreren van de handler gebruikt worden. Zet de code die ze gebruikt in een aparte methode met `[MethodImpl(NoInlining)]`.

## Openen (zelfde volgorde als XPacker)

```csharp
var pack = new RaceBits.XPack(Path.Combine(parentDir, name + ".zip"));  // de zip hoeft niet te bestaan
pack.LoadFromFolder(parentDir, name);          // true bij succes; exception "Error loading <item>" bij een fout
var packs = new RaceBits.XPacks(parentDir);
packs.Add(pack);
pack.VerifyAndSetReferences(packs);            // false bij ontbrekende verwijzingen, maar laadt wel door
pack.FillEmptyDefaultListsFromAllItems();      // v2.0 (bestaat niet in v1950)
```

- `LoadFromFolder(XPackFolder, XPackname)`: het eerste argument is de **bovenliggende** map, het tweede de naam van de XPack-map.
- De constructor met het zip-pad is nodig. `XPackFolder` wordt afgeleid uit `XPackFullZipFilePath` als `<map van de zip>\<naam>`. Met `new XPack()` geeft de getter van `XPackFolder` een `ArgumentException` ("Het pad heeft een ongeldige indeling").
- `VerifyAndSetReferences` geeft alleen een bool. Voor de meldingen roep je de losse methoden aan: `VerifyMaterials(packs, errors)`, `VerifyObjectsAndSetReferences(errors)`, `VerifySObjectsAndSetObjects(errors)` en `VerifyCrossSections(packs, errors)` (v1950: zonder `packs`). Voorbeeldmelding: `Error validating <xpack>, Objects\...\M_X - Materials\Objects\...\M_X not found.`
- **MAX_PATH.** RaceBits draait op .NET Framework 4.8 en kan niet met paden van meer dan 260 tekens werken (`PathTooLongException`, verpakt in "Error loading …"). Het langste pad in je grootste XPack is 227 tekens: binnen de grens, maar met weinig marge. Een werkkopie in een diepe map gaat al mis.
- Paden van items zijn `<XPack>\<Soort>\<pad>`, bijvoorbeeld `NewXPackTest\Objects\Billboards_L_1`. `XPack.GetPathWithPackNameRemoved` en `GetPathWithPackNameAndItemTypeRemoved` halen de voorvoegsels weg.
- Bij het laden worden de textures niet naar de GPU gestuurd: de grootste XPack (1,2 GB, 399 textures) gebruikt op v2.0 maar ~95 MB werkgeheugen en ~118 MB private bytes. `Icon` is na het laden `null` voor objecten en materialen (`LoadIcons()` laadt ze apart).
- Defaults staan niet in de XML. Een object zonder `<Shadow>` leest als `Shadow=None`, `ShadowCast=Solid`, `Render=True`, `Purpose=Object`.

### Fouten bij het openen (fase 1.2)

Experiment op kopieën van Default-testpack (26 okt 2026, v2.0):

| Situatie | Gedrag van RaceBits | xpk |
| --- | --- | --- |
| Object-XML afgebroken (`<Object><LODOut>abc`) | `LoadFromFolder` gooit `Exception("Error loading Default-testpack\Objects\...\Tree01")` met de XmlException-tekst erin. Geen MessageBox. | `LOAD_FAILED` met het itempad |
| Materiaal-XML afgebroken | Idem: "Error loading ...\Materials\Road\Cobble\Cobble001". Geen MessageBox. | `LOAD_FAILED` |
| Materiaal-XML verwijderd dat door objecten gebruikt wordt | Laden lukt; `VerifyAndSetReferences` geeft `false`; de losse Verify-aanroepen geven de meldingen | waarschuwing in de samenvatting |

- Fouten bij het laden geven dus een exception, geen dialoog. De dialoog uit fase 0 kwam van `ImportFBX`. Toch draait xpk elke RaceBits-aanroep via `RaceBitsCall`: op een eigen STA-thread met een time-out (60 s). Een dialoog (`#32770`) op die thread wordt gelezen, gesloten met WM_CLOSE, en gemeld als `RACEBITS_DIALOG`. Het laden op een andere thread dan die van `Globals.Initialize` werkt zonder problemen.
- Default-testpack via RaceBits: 28 objecten, 4 String Objects, 28 materialen, 67 textures, 13 Cross Sections (de map heeft meer bestanden: iconen `.jpg`, `Thumbs.db` en `.rbr`).

### Paden en verwijzingen in het geheugen (fase 1.5)

- `BObject.Path`, `SObject.Path`, `Mat.Path` en `Texture.XPackPath` zijn volledig: `<XPack>\<Soort>\<pad>` (textures met extensie).
- `TextureMap.Path` is ook volledig (`Default-testpack\Textures\Objects\Vegetation\Trees\Tree01.dds`), en leeg (`""`) voor een ongebruikt slot. `TextureMap.Texture` is na het laden `null` (textures worden pas bij weergave geladen).
- `BObjectPolyList.MaterialPath` is relatief aan `Materials\` (zoals in de XML); `.Material` is na `VerifyAndSetReferences` gezet.
- `BObjectHierarcyTemplate.BObjectPath` is na het laden volledig (in de XML staat het relatief).
- Een String Object verwijst naar objecten via `Start/Pattern/End.Pieces[i].BObject`; `SObject.Materials` was leeg in de fixture.
- `Mat.GetRequiredTextureMaps()` geeft paren (`"Diffuse"`, TextureMap). Het materiaal-Purpose is geen veld: `PurposeContains(Purposes)`.
- `XPack.Textures` bevat alleen beeldbestanden: de `.rbr`-bestanden in `Textures\` en `Thumbs.db` tellen niet mee.

## Opslaan

Round-trip zonder wijziging op **v2.0** (de doelversie):

| Aanroep | Wat er gebeurt | Round-trip zonder wijziging |
| --- | --- | --- |
| `CreateObject(obj, false)` | Schrijft `<XPack>\Objects\<pad>.xml` opnieuw, inclusief de mesh-data | Eerste keer: `<SeparationMinDistance>0</SeparationMinDistance>` (standaard) valt weg, de rest is gelijk, ook de mesh van 1,2 MB. Tweede keer: **byte-identiek** aan de eerste. Na opnieuw openen zijn alle eigenschappen gelijk. |
| `CreateSObject(sobj, xpackName)` | Tweede argument is de **naam van de XPack** (bv. `"NewXPackTest"`) | Byte-identiek |
| `CreateMaterialXML(mat)` | Schrijft `<XPack>\Materials\<pad>.xml` | Velden die ontbraken komen erbij met hun standaardwaarde (`GrassFX_Mask*`, en bij oudere materialen ook `rf_*` en `AC_GameSurface`). Er verdwijnt niets. |
| `CreateMaterialIconFile(mat)` | Schrijft `<XPack>\Materials\<pad>.jpg` (128×128); werkt headless | Wordt opnieuw getekend: soms identiek, soms een paar bytes anders (4714 → 4713) |
| `ZipTo(pad)` | Maakt de zip | Zelfde 42 bestanden en groottes als de export van XPacker. Verschil: `ZipTo` schrijft `\` in de namen in de zip, de export van XPacker van 23 aug heeft `/`. In fase 7 controleren of RTB beide leest. |

Gevolg voor de tests (plan 2.4, `RoundTrip_NoChanges_FilesIdentical`): vergelijk semantisch (opnieuw openen en de eigenschappen vergelijken), en controleer daarnaast dat een tweede keer opslaan byte-identiek is. Een wijziging vergelijk je met een eenmaal genormaliseerde versie.

Op v1950 was de object-round-trip wel byte-identiek, maar verloor `CreateMaterialXML` de velden `rf_*` en `GrassFX_*`.

- Getallen schrijft RaceBits zelf. Het spike-proces draaide met `InvariantCulture`; `LODOut` 2001 kwam er als `2001` uit. Het gedrag onder `nl-NL` als `CurrentCulture` is nog niet getest. Daarom zet xpk bij het opstarten `InvariantCulture`.
- Er schrijft niets naar stdout. Toch zet `xpk serve` als eerste `Console.Out` om naar stderr.

## FBX-import

`ImportFBX(fbxPath, rootPath)` werkt headless, op v1950 en op v2.0 met hetzelfde resultaat:

- Met `rootPath = ""` en `RunOff.fbx` kwamen er vier bestanden bij: `Objects\RunOff.xml`, `Materials\Objects\RunOff\M_TrackEdge_Opaque.xml` en `.jpg` (materiaalicoon), en `Textures\Objects\RunOff\T_TrackEdge_Opaque_BaseColor.dds` (gekopieerd van het absolute pad in de FBX). Daarna `CreateObject(obj, false)`.
- De objectnaam is de bestandsnaam. `rootPath` wordt er als tekst **voor** gezet: met `"Imported"` werd het `ImportedBench`.
- Met `rootPath = "Imported\"` toonde RaceBits een **MessageBox** "Failed Ongeldige tekens in pad." en bleef het proces hangen. De betekenis van `rootPath` wordt in fase 6 verder uitgezocht.
- Alleen de texture die bij de shader hoort is gekopieerd (Diffuse: BaseColor). De normal map in de FBX is niet meegenomen.
- `Bench_FBX(1).fbx` gaf een object zonder PolyLists. Mogelijk wordt de geometrie in dat bestand niet ondersteund; niet verder onderzocht.

## Iconen

- Materiaaliconen maakt RaceBits (`CreateMaterialIcon`, `CreateMaterialIconFile`), ook headless.
- Objecticonen staan als `Objects\<pad>.jpg` naast de XML. RaceBits heeft geen openbare methode om ze te maken; `ImportFBX` en `CreateObject` maken er geen. XPacker tekent ze zelf. Plan fase 7 (waarschuwing `icon-stale`) blijft dus nodig.

## Threading en processen

- Getest op een STA-thread (CLI) en vanuit de xUnit-testrunner. `Globals` is statisch: één `RtbEnvironment` per proces.
- Een RaceBits-fout kan een modale dialoog openen. Bewaak elke aanroep met een time-out, en in `serve`-modus nooit op de UI-thread wachten zonder bewaking.

## MCP SDK op net48

- `ModelContextProtocol` 2.3.0 (netstandard2.0) werkt op net48. Er is geen `Microsoft.Extensions.Hosting` nodig: `McpServer.Create(new StdioServerTransport("xpk"), options)` en `RunAsync`.
- De tools komen van `McpServerTool.Create(MethodInfo)` met `[McpServerTool]` en `[Description]`.
- Getest met de SDK-client (xUnit) en met de hand met JSON-RPC: `initialize`, `tools/list` en `tools/call` geven geldige antwoorden, en op stdout staat alleen JSON.
- Na het sluiten van stdin stopt de server in 44 ms (Claude Code geeft 300 ms).
- Let op: als Claude Code `out\xpk.exe serve` draait, staat het bestand vast en faalt `dotnet build`. Registreer de server dus niet in een sessie waarin je aan xpk zelf bouwt, of bouw naar een andere map.

## Waar staan de XPacks? (0.4)

| Wat | Map | Bron |
| --- | --- | --- |
| XPacker opent bron-XPacks (mappen) uit | `C:\Users\Moor\OneDrive\Documenten\Race Track Builder XPacker` | Instelling `XPacksFolder` in `%LOCALAPPDATA%\RaceTrackBuilder\XPacker.exe_Url_*\1.0.0.0\user.config` |
| XPacker kent de RTB-map | `C:\Users\Moor\OneDrive\Documenten\Race Track Builder` | Instelling `RaceTrackBuilderFolder` in dezelfde user.config |
| RTB leest de zips uit | `C:\Users\Moor\OneDrive\Documenten\Race Track Builder\XPacks` | `<projectsFolder>\XPacks`, `XPacksCache.xml` in de RTB-map (v1950: `Globals.UserXPacksFolder`) |
| Meegeleverde themes | `<RTB>\Themes\*.zip` | `XPacksCache.xml` |

Het doel van `ZipTo` is dus `<Documenten>\Race Track Builder\XPacks\<naam>.zip`. Het zip-pad dat je aan de constructor van `XPack` geeft (`<map>\<naam>.zip`) bepaalt alleen `XPackFolder`; `ZipTo` schrijft naar het pad dat je meegeeft.

## Gevonden in je eigen XPacks

- **TD by CT - Track Environment 2 - Buildings:** de objecten `Infrastructure\Grandstand_End`, `Grandstand_Mid` en `Grandstand_Start` verwijzen samen naar 15 materialen in `Materials\Objects\Infrastructure\SM_Grandstand_*`. Die mappen zijn leeg, en de drie objecten hebben ook geen icoon. Dit is een goed eerste testgeval voor de validatieregels in fase 4.

## Acceptatie fase 0

- [x] XPacker toont bij **Billboards_L_1** LOD Out **2001**; de andere objecten zijn ongewijzigd (Jeroen, 25 okt 2026).
- [x] RTB-branch gekozen: **v2.0** (Steam-branch `v2000`). Over v2.1 wordt later besloten.

De volledige kopie van vóór alle experimenten staat in `C:\Users\Moor\AppData\Local\xpk\backups\NewXPackTest\20261025-204909\`.
