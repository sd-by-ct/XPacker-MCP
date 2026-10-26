# Test-XPack (fixture)

De integratietests werken op `tests/Fixtures/source/Default-testpack`. Die map staat **niet** in git:
het is de standaard-XPack van BTB/RTB (auteur Brendon Pywell), geen eigen werk, en de repo bevat geen RTB-bestanden.

## Neerzetten

1. Kopieer de XPack-map `Default-testpack` naar `tests\Fixtures\source\Default-testpack`
   (zodat `tests\Fixtures\source\Default-testpack\Description.xml` bestaat).
2. Pas niets aan in die map. De tests kopiëren hem per test naar `tests\Fixtures\work\<testnaam>\` en werken alleen op die kopie.

Ontbreekt de map, dan falen de integratietests met de melding "Fixture ontbreekt".

## Wat de tests verwachten (RaceBits v2.0)

| Wat | Aantal / voorbeeld |
| --- | --- |
| Objecten | 28, waarvan complexe objecten met child objects: `Vegetation\Large Trees\Tree04_complex`, `Tree05_complex` |
| String Objects | 4 (`Ripple\Pyramid1`, `Ripple\SawLeft`, `Ripple\SawRight`, `Wall\Metal\PortableRails`) |
| Materialen | 28 |
| Textures | 67 |
| Cross Sections | 13 |
| Zelfde naam, andere inhoud | `Textures\Ripple001.dds` en `Textures\Ripple\ripple001.dds` |
