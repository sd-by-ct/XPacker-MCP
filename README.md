# XPacker-MCP (xpk)

API en MCP voor XPacker (van RTB - Race Track Builder): de inhoud van een XPack lezen en, vanaf fase 2, wijzigen. xpk gebruikt `RaceBits.dll` uit de RTB-installatie; die wordt niet meegeleverd. Alleen voor eigen gebruik.

Zie `docs/PLAN.md` voor het plan, `docs/racebits-api.md` en `docs/xpack-format.md` voor de bevindingen.

## Bouwen en testen

```
dotnet build
dotnet test --filter Category!=Integration
dotnet test
```

`dotnet build` zet `xpk.exe` in `out\`. De integratietests hebben RTB v2.0 nodig en de test-XPack in `tests\Fixtures\source\Default-testpack` (zie `tests/Fixtures/README.md`).

## Gebruik (fase 1: alleen lezen)

`--xpack` is een map, of de naam van een XPack in de XPacker-map (`Documenten\Race Track Builder XPacker`).

```
xpk list xpacks
xpk info    --xpack NewXPackTest
xpk list objects   --xpack NewXPackTest --select "Bomen\**" --fields Shadow,LODOut
xpk list materials --xpack NewXPackTest --select "re:Kerb"
xpk list textures  --xpack NewXPackTest --limit 50 --offset 50
xpk get object   "Vegetation\Trees\Tree01" --xpack NewXPackTest
xpk get material "Objects\Barrier Pack\Concrete" --xpack NewXPackTest --json
```

- **Selector:** `Bomen` is een map met alles eronder (niet `Bomen2`), `Bomen\*` is één niveau, `Kerbs\**` zijn alle niveaus, `re:<regex>` is een reguliere expressie. `\` en `/` werken allebei; hoofdletters maken niet uit.
- **Getallen** staan altijd met een punt (`0.6`), ook op een Nederlandse Windows. Bij het invoeren (vanaf fase 2) zijn `0.6` en `0,6` allebei goed; `1.000,5` wordt geweigerd.
- **`--json`** geeft `{ "ok": true, "data": …, "warnings": [] }`, of bij een fout `{ "ok": false, "error": { "code": "XPACK_NOT_FOUND", "message": "…" } }`.
- Lijsten geven standaard maximaal 200 items, met `total` en `next_offset`.
- Exitcode: 0 = gelukt, 1 = fout (met code), 2 = verkeerd gebruik.

## Config

Optioneel, in `%APPDATA%\xpk\config.json`. Alle velden zijn optioneel:

```json
{
  "rtbDir": "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Race Track Builder",
  "backupDir": "C:\\Users\\<jij>\\AppData\\Local\\xpk\\backups",
  "knownRaceBitsSha256": ["611efe142ff387bc17a6f0eb9c236d7d426a21b8f7c3fce2ce01c275a43694ff"],
  "allowUnknownRaceBits": false
}
```

Na een RTB-update heeft `RaceBits.dll` een andere hash. xpk is dan alleen-lezen (`xpk info` toont het) tot de integratietests groen zijn en de nieuwe hash in `knownRaceBitsSha256` staat.
