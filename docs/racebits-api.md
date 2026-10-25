# RaceBits-API: bevindingen fase 0

Vastgelegd op 25 okt 2026, op basis van experimenten met de geïnstalleerde RaceBits.dll en reflectie op de openbare API (alleen namen en signaturen, geen gedecompileerde code). De experimenten zijn gedaan op kopieën: de werkkopie `C:\Users\Moor\xpk\NewXPackTest` (met backup) en kopieën in `tests\Fixtures\work\`. Voor en na elke run is gecontroleerd dat de RTB-map en `Documenten\Race Track Builder` niet zijn veranderd.

## Go/no-go-advies

**Advies: GO, route A (RaceBits zonder scherm).** Definitief zodra jij de acceptatie in XPacker hebt gedaan (zie onderaan).

| Voorwaarde uit het plan | Uitkomst |
| --- | --- |
| RaceBits laadt zonder venster | Ja. `Globals.Initialize` met een headless D3D11-device (`DriverType.Hardware`, feature level 11_0) in 0,4 s. Een onzichtbaar WinForms-venster (route A2) is niet nodig. |
| XPack openen in de volgorde van XPacker | Ja. NewXPackTest in 0,2 s, de grootste eigen XPack (70 objecten, 381 materialen) in 5,6 s koud en 1,1 s warm. |
| `LODOut` + 1 en `CreateObject(obj, false)` | Ja. Precies één regel in één bestand verandert: `<LODOut>2000</LODOut>` wordt `<LODOut>2001</LODOut>` in `Objects\Billboards_L_1.xml`. Na opnieuw openen leest RaceBits 2001. |
| Geen onverwachte wijzigingen | Ja voor objecten en String Objects: round-trip is byte-identiek. **Nee voor materialen** bij een XPack die met v2000 is gemaakt (zie "Twee RaceBits-versies"). |
| XPacker toont de nieuwe LODOut | Nog door jou te controleren. |

Twee punten moet je beslissen of weten voordat fase 2 begint:

1. **Welke RTB-branch is het doel: v1950 of v2000?** Met v1950 verliest `CreateMaterialXML` de velden `rf_*` en `GrassFX_*` die v2000 heeft geschreven. Lezen en objecten werken met allebei.
2. **Een RaceBits-fout kan een modale dialoog openen** (MessageBox). In `serve`-modus blokkeert dat de server. Fase 1 krijgt daarom een bewaking: elke RaceBits-aanroep met een time-out, en dialogen automatisch sluiten of voorkomen.

## Twee RaceBits-versies

| | Geïnstalleerd nu | `_FbxDedupePatch\RaceBits.original.dll` |
| --- | --- | --- |
| Steam-branch | `v1950` (build 25819081, RTB v1.9.5.6, 23 okt) | `v2000` beta |
| SHA-256 | `944c3fe8fe825af1944b2649884966393233c60ed9d8e33c54a8d70add340f0a` | `3d9105bbe55b2c8de3ea2d87082a326c1677c2f53603cf896c4e581b88d26e24` |
| Grootte | 920 064 bytes | 1 488 896 bytes |
| Gepatcht? | Nee (geen verwijzing naar RTBImportDedupe) | Nee (back-up van vóór de patch) |
| FileVersion | 1.0.0.0 | 1.0.0.0 |

De FileVersion is altijd 1.0.0.0. Alleen de hash identificeert een build, en dat is wat VersionGuard moet gebruiken.

`herstel.bat` kopieert de v2000-DLL over de v1950-DLL. Dan staat een v2000-RaceBits naast een v1950-RTB en een v1950-XPacker. **Niet doen** zolang Steam op v1950 staat.

**API-verschillen die ertoe doen** (v2000 → v1950):

- Ontbreekt in v1950: `XPack.FillEmptyDefaultListsFromAllItems()`, `XPack.DefaultObjects/DefaultSObjects/DefaultWalls/DefaultDecals/DefaultTracks`, `Mat.GetMissingRequiredTextures()`, `Mat.GetRequiredTextureMaps()`, `Mat.SupportsAC` en verwanten, de velden `Mat.GrassFX_*`, `Mat.rf_Specular/rf_Roughness/rf_NormalIntensity/rf_AlbedoTint`, `Mat.TexMap7–9`, `Mat.Perlin*`.
- Nieuw in v1950: `Globals.UserXPacksFolder`, `XPack.IsOldBTBPack`, `XPack.SetIsThemePackFlag()`, `Mat.rF_DoubleSided`, `BObject.FacesWarning(ref string)`.
- Gevolg voor het plan: regel `material-required-texture-missing` (fase 4) bestaat niet op v1950. `FillEmptyDefaultListsFromAllItems` wordt via reflectie aangeroepen als hij bestaat.

## Starten

```text
AppDomain.AssemblyResolve  alleen voor RaceBits, RaceFbx, FbxWrapper, RTBPlugins, Serilog, SharpDX* uit de RTB-map
SetDllDirectory(rtbDir)    voor libfbxsdk.dll en sharpdx_direct3d11_effects_x64.dll
new SharpDX.Direct3D11.Device(DriverType.Hardware)   (terugval: Warp)
RaceBits.Globals.Initialize(device, applicationFolder, projectsFolder)
```

- `applicationFolder` = de RTB-map. `Globals.ApplicationsXPacksFolder` wordt dan `<RTB>\XPacks`, een map die niet bestaat (de meegeleverde themes staan in `<RTB>\Themes`).
- `projectsFolder` = `Documenten\Race Track Builder`. Gebruik `Environment.SpecialFolder.MyDocuments`; die volgt de OneDrive-omleiding (`C:\Users\Moor\OneDrive\Documenten`).
- `Globals.UserXPacksFolder` (alleen v1950) komt uit Mijn documenten, **niet** uit `projectsFolder`. Ook met een andere `projectsFolder` bleef hij `OneDrive\Documenten\Race Track Builder\XPacks`.
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
pack.FillEmptyDefaultListsFromAllItems();      // alleen v2000
```

- `LoadFromFolder(XPackFolder, XPackname)`: het eerste argument is de **bovenliggende** map, het tweede de naam van de XPack-map.
- De constructor met het zip-pad is nodig. `XPackFolder` wordt afgeleid uit `XPackFullZipFilePath` als `<map van de zip>\<naam>`. Met `new XPack()` geeft de getter van `XPackFolder` een `ArgumentException` ("Het pad heeft een ongeldige indeling").
- `VerifyAndSetReferences` geeft alleen een bool. Voor de meldingen roep je de losse methoden aan: `VerifyMaterials(packs, errors)`, `VerifyObjectsAndSetReferences(errors)`, `VerifySObjectsAndSetObjects(errors)` en `VerifyCrossSections(errors)` (v1950; in v2000 met `packs` erbij). Voorbeeldmelding: `Error validating <xpack>, Objects\...\M_X - Materials\Objects\...\M_X not found.`
- **MAX_PATH.** RaceBits draait op .NET Framework 4.8 en kan niet met paden van meer dan 260 tekens werken (`PathTooLongException`, verpakt in "Error loading …"). Het langste pad in je grootste XPack is 227 tekens: binnen de grens, maar met weinig marge. Een werkkopie in een diepe map gaat al mis.
- Paden van items zijn `<XPack>\<Soort>\<pad>`, bijvoorbeeld `NewXPackTest\Objects\Billboards_L_1`. `XPack.GetPathWithPackNameRemoved` en `GetPathWithPackNameAndItemTypeRemoved` halen de voorvoegsels weg.
- Bij het laden worden de textures niet naar de GPU gestuurd: de grootste XPack (1,2 GB, 399 textures) gebruikt maar ~100 MB werkgeheugen en ~124 MB private bytes. `Icon` is na het laden `null` voor objecten en materialen (`LoadIcons()` laadt ze apart).
- Defaults staan niet in de XML. Een object zonder `<Shadow>` leest als `Shadow=None`, `ShadowCast=Solid`, `Render=True`, `Purpose=Object`.

## Opslaan

| Aanroep | Wat er gebeurt | Round-trip zonder wijziging |
| --- | --- | --- |
| `CreateObject(obj, false)` | Schrijft `<XPack>\Objects\<pad>.xml` opnieuw, inclusief de mesh-data | **Byte-identiek** (getest op 36 KB met mesh en 163 B leeg object) |
| `CreateSObject(sobj, xpackName)` | Tweede argument is de **naam van de XPack** (bv. `"NewXPackTest"`) | Byte-identiek |
| `CreateMaterialXML(mat)` | Schrijft `<XPack>\Materials\<pad>.xml` | **Niet identiek op v1950** bij XPacks van v2000: `rf_Specular`, `rf_Roughness`, `rf_NormalIntensity`, `rf_AlbedoTint` en `GrassFX_*` verdwijnen. Er komt `<AC_GameSurface>ROAD</AC_GameSurface>` bij als die ontbrak. |
| `CreateMaterialIconFile(mat)` | Schrijft `<XPack>\Materials\<pad>.jpg` (128×128) | Byte-identiek; werkt headless |
| `ZipTo(pad)` | Maakt de zip | Zelfde 42 bestanden en groottes als de export van XPacker. Verschil: v1950 schrijft `\` in de namen in de zip, de export (met v2000) heeft `/`. In fase 7 controleren of RTB beide leest. |

- Getallen schrijft RaceBits zelf. Het spike-proces draaide met `InvariantCulture`; `LODOut` 2001 kwam er als `2001` uit. Het gedrag onder `nl-NL` als `CurrentCulture` is nog niet getest. Daarom zet xpk bij het opstarten `InvariantCulture`.
- Er schrijft niets naar stdout. Toch zet `xpk serve` als eerste `Console.Out` om naar stderr.

## FBX-import

`ImportFBX(fbxPath, rootPath)` werkt headless:

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
| RTB leest de zips uit | `C:\Users\Moor\OneDrive\Documenten\Race Track Builder\XPacks` | `Globals.UserXPacksFolder` (v1950), `XPacksCache.xml` in de RTB-map |
| Meegeleverde themes | `<RTB>\Themes\*.zip` | `XPacksCache.xml` |

Het doel van `ZipTo` is dus `<Documenten>\Race Track Builder\XPacks\<naam>.zip`. Het zip-pad dat je aan de constructor van `XPack` geeft (`<map>\<naam>.zip`) bepaalt alleen `XPackFolder`; `ZipTo` schrijft naar het pad dat je meegeeft.

## Gevonden in je eigen XPacks

- **TD by CT - Track Environment 2 - Buildings:** de objecten `Infrastructure\Grandstand_End`, `Grandstand_Mid` en `Grandstand_Start` verwijzen samen naar 15 materialen in `Materials\Objects\Infrastructure\SM_Grandstand_*`. Die mappen zijn leeg, en de drie objecten hebben ook geen icoon. Dit is een goed eerste testgeval voor de validatieregels in fase 4.

## Acceptatie door jou (fase 0)

1. Wijs XPacker tijdelijk naar `C:\Users\Moor\xpk` als XPacks-map, of kopieer `C:\Users\Moor\xpk\NewXPackTest` naar een **nieuwe** naam in je XPacker-map. Daar staat al een `NewXPackTest`, dat is het origineel.
2. Open NewXPackTest en controleer bij het object **Billboards_L_1** dat LOD Out **2001** is. De andere objecten moeten ongewijzigd zijn: Balloon_Arch 2000, Barrier Pack 1000, New Empty Object 2000.
3. Kies de RTB-branch voor dit project (v1950 of v2000).
4. Terugzetten kan met `C:\Users\Moor\AppData\Local\xpk\backups\NewXPackTest\20261025-204909\` (volledige kopie van vóór alle experimenten).
