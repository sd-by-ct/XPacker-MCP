# XPack-formaat: bevindingen fase 0

Vastgelegd op 25 okt 2026. Bron: de werkkopie `NewXPackTest`, een kopie van `TD by CT - Track Environment 2 - Buildings` (70 objecten, 10 String Objects, 381 materialen) en `ComplexTest` (alleen gelezen, voor child objects). Alles is gewone XML (`<?xml version="1.0"?>`, UTF-8 zonder BOM, inspringing van 2 spaties, CRLF).

## Mappen

```text
<XPack>\                      bronmap; XPacker en RaceBits openen deze map
  Description.xml             auteur, datum, beschrijving, IsThemePack
  Objects\<pad>.xml           object: eigenschappen + mesh-data
  Objects\<pad>.jpg           object-icoon (gemaakt door XPacker, niet door RaceBits)
  SObjects\<pad>.xml          String Object
  Materials\<pad>.xml         materiaal
  Materials\<pad>.jpg         materiaal-icoon 128x128 (RaceBits: CreateMaterialIconFile)
  Textures\<pad>.<png|jpg|dds> texturebestanden
  CrossSections\<pad>.xml     Cross Section (en .jpg icoon)
<XPack>.zip                   export voor RTB; dezelfde bestanden, gezipt
```

- `<pad>` kan submappen bevatten, zoals `Objects\Infrastructure\Grandstand_End.xml`. Na een FBX-import is het `Materials\Objects\<object>\<materiaal>` en `Textures\Objects\<object>\<texture>`.
- In RaceBits heet een item `<XPack>\<Soort>\<pad>`, zonder extensie (bv. `NewXPackTest\Objects\Billboards_L_1`).
- Verwijzingen tussen items zijn relatief aan de soort, **zonder** XPack-naam en zonder `Objects\`/`Materials\` ervoor:
  - een PolyList van een object → materiaal: `<Material Path="Objects\Barrier Pack\Concrete" />` (= `Materials\Objects\Barrier Pack\Concrete.xml`)
  - een materiaal → texture: `<Path>Objects\Barrier Pack\ConcreteBarrier.jpg</Path>` (= `Textures\Objects\Barrier Pack\ConcreteBarrier.jpg`)
  - een child object → object: `<BObjectPath>Den_Tak_Dood</BObjectPath>` (= `Objects\Den_Tak_Dood.xml`)
  - een String Object → object: `<Object>Pits\PitWall_Stand_A</Object>`
  - een Cross Section → materiaal: `<SurfaceMaterial MaterialPath="Road\Markings\WhiteLine003">`
- Hoe zit het met verwijzingen naar andere XPacks? Dat is in fase 0 niet gezien. Uitzoeken in fase 1.

## Welke bestanden horen bij een item

| Item | Bestanden | Schrijft via RaceBits |
| --- | --- | --- |
| Object | `Objects\<pad>.xml`, optioneel `Objects\<pad>.jpg` (icoon) | `CreateObject(obj, false)` schrijft alleen de `.xml` |
| String Object | `SObjects\<pad>.xml` | `CreateSObject(sobj, xpackName)` |
| Materiaal | `Materials\<pad>.xml`, `Materials\<pad>.jpg` | `CreateMaterialXML(mat)`, `CreateMaterialIconFile(mat)` |
| Texture | `Textures\<pad>` | via import (`ImportFBX` kopieert de texture) |
| Cross Section | `CrossSections\<pad>.xml`, `CrossSections\<pad>.jpg` | `CreateCrossSection(cs)` (niet getest) |

## Object (`<Object>`)

Waarden die gelijk zijn aan de standaard worden niet geschreven. In een XPack van 70 objecten komt `<Shadow>` maar 51 keer voor, `<Render>` 1 keer en `<Purpose>` 10 keer. Zonder `<Shadow>` leest RaceBits `Shadow=None`. Elementen in de volgorde waarin ze voorkomen:

| Element | Voorbeeld | Opmerking |
| --- | --- | --- |
| `Hierarcy` / `HierarcyObject` | `BObjectPath`, `Scale`, `Rotation` (graden), `Translation`, optioneel `RandomLimits` (`ScaleAllMin`, `ScaleAllMax`, …) | child objects; in RaceBits `BObject.HierarcyTemplate` |
| `IsComplexObject` | `True` | |
| `Description` | | |
| `Render` | `False` | standaard True |
| `Collisions`, `ShadowReceiver`, `ReceiveLighting` | `True` | |
| `Shadow` | `None`, `Caster`, `Object` | enum `Shadows` (1, 2, 4) |
| `ShadowCast` | `Solid`, `Texture` | |
| `Purpose` | | enum `Purposes` (flags: Terrain 1, Track 2, Wall 4, Object 8) |
| `UseRBRCollisionColumn`, `RBRCollisionColumn` | | |
| `LODIn`, `LODOut` | `0`, `2000` | altijd aanwezig |
| `SeparationGroup`, `SeparationMinDistance` | `Sign/Posts`, `0` | |
| `PolyLists` / `PolyList` | | zie mesh-data |
| `CutPolys` | | altijd aanwezig (meestal leeg) |

## Mesh-data

De mesh staat **in de object-XML zelf**, per materiaal één `PolyList`:

```xml
<PolyList>
  <Material Path="Objects\Billboards_L_1\RacingBanners" />
  <Vertices>
    <Positions><![CDATA[-127.8997 -0.1322936 -127.8997 127.8997 …]]></Positions>   x y z per vertex
    <Normals><![CDATA[0 -1 -1.629207E-07 …]]></Normals>                           x y z per vertex
    <UV0><![CDATA[0.2347919 -0.3763586 …]]></UV0>                                 u v per vertex
  </Vertices>
  <FaceIndices IsAntiClockwise="True"><![CDATA[0 1 2 1 0 3 …]]></FaceIndices>   3 indices per driehoek
</PolyList>
```

- Floats met een punt en zo nodig een exponent (`-1.629207E-07`), gescheiden door spaties. RaceBits leest ze met `Globals.LoadXMLCDataToFloat`.
- Een object van 1,2 MB is vrijwel helemaal mesh. Lees dus nooit hele object-XML's in het geheugen alleen voor eigenschappen, of lees ze via RaceBits.
- Een complex object zonder eigen mesh heeft een `PolyList` met lege `Vertices`, of `<PolyLists />`.

## String Object (`<SObject>`)

```xml
<SObject Collisions="True" ShadowReceiver="True" ReceiveLighting="True">
  <SObjectType>Building</SObjectType>
  <LODIn>0</LODIn>
  <LODOut>1000</LODOut>
  <Start />
  <Pattern>
    <Pieces>
      <Piece SpaceBefore="1.3">
        <Object>Pits\PitWall_Stand_B</Object>
        <CurveWithLine>True</CurveWithLine>
        <Scale x="1.15" y="1.15" z="1.15" />
      </Piece>
    </Pieces>
  </Pattern>
  <End />
</SObject>
```

- Booleans staan hier als **attributen** op `<SObject>`, niet als elementen zoals bij objecten.
- `Piece` kan ook `Translate` en `Rotation` hebben.

## Materiaal (`<material>`)

Alle velden worden geschreven, ook als ze de standaardwaarde hebben. Volgorde in een v2000-bestand:

`ShaderType`, `DefaultApplicationType`, `PerMeterCoverage`, `GameSurface`, `Purpose`, `rF_SpecularPower`, `rF_SpecularColor R G B`, `rF_rFactorMaterialName`, `rf_Specular`\*, `rf_Roughness`\*, `rf_NormalIntensity`\*, `rf_AlbedoTint R G B`\*, `rF_TransparencyType`, `rF_TransparencyChromaColor R G B`, `AC_GameSurface` (v1950 schrijft hem altijd), `AC_Ambient`, `AC_Diffuse`, `AC_Specular`, `AC_SpecularPower`, `AC_AlphaRef`, `AC_DetailUVMultiplier`, `AC_DetailNormalBlend`, `AC_MultR/G/B/A`, `AC_MagicMult`, `AC_TransparencyType`, `AC_ksTree`, `GrassFX_Size`\*, `GrassFX_Width`\*, `GrassFX_Cut`\*, `GrassFX_Mask*`\*, daarna de texture-maps.

\* bestaat alleen in v2000. Met v1950 gaan deze velden bij `CreateMaterialXML` verloren.

Een texture-map (`DiffMap`, `BumpMap`, …; in RaceBits `TexMap1..n`):

```xml
<DiffMap>
  <Path>Objects\Barrier Pack\ConcreteBarrier.jpg</Path>
  <texture_coordinate>
    <Angle>0</Angle>
    <scale x="1" y="1" />
    <offset x="0" y="0" />
  </texture_coordinate>
  <MIPBias>0</MIPBias>
</DiffMap>
```

## Description.xml

```xml
<XPack>
  <Author>[Your Name]</Author>
  <Date>19 jul 2026</Date>
  <Description />
  <IsThemePack>False</IsThemePack>
</XPack>
```

`Date` is vrije tekst in de taal van Windows (`19 jul 2026`), geen ISO-datum.

## Zip

- Dezelfde bestanden als de bronmap, met paden relatief aan de XPack-map (`Objects\Barrier Pack.xml`).
- De export van XPacker (gemaakt met v2000) gebruikt `/` als scheidingsteken, `ZipTo` met v1950 gebruikt `\`. Namen en groottes zijn verder identiek (42 bestanden, 37 135 166 bytes).

## Grenzen

- Paden tot 260 tekens (MAX_PATH van .NET Framework 4.8). Het langste pad in de grootste eigen XPack is 227 tekens.
