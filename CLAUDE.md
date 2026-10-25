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
