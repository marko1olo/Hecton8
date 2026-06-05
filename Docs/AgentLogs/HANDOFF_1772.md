# Handoff 1772 - PDA / Scanner / Audio / Runtime

Evidence class: STATIC_SOURCE.

Changed English packet rows:

| packet_id | source packet | runtime contract |
|---|---|---|
| `P046_PUMP_ROOM_HANDSHAKE` | `Docs/Lore/AppliedContent/packets/RS010_PRESSURE_MACHINERY_RETURN_ROUTE.packets.json` | baked static data only |
| `P049_SONAR_RETURN_ROUTE` | `Docs/Lore/AppliedContent/packets/RS010_PRESSURE_MACHINERY_RETURN_ROUTE.packets.json` | baked static data only |
| `P060_FIRST_HOUR_SPINE` | `Docs/Lore/AppliedContent/packets/RS012_PLAYER_LIABILITY_ESCAPE.packets.json` | baked static data only |
| `P061_MAINTENANCE_ECOLOGY` | `Docs/Lore/AppliedContent/packets/RS013_COLONY_ATLAS_MAINTENANCE.packets.json` | baked static data only |
| `P221_PHOTIC_MAT_BASELINE` | `Docs/Lore/AppliedContent/packets/RS045_PHOTIC_SHELF_NATIVE_ECOLOGY.packets.json` | baked static data only |
| `P291_PHOTIC_MAT_CODEX_CARD` | `Docs/Lore/AppliedContent/packets/RS059_ECOLOGY_CODEX_SPECIMEN_CARDS.packets.json` | baked static data only |

Scanner agents:

- Use existing `scanner` fields. They now contain player-action cues for pump pressure, stale sonar echo, first-hour evidence, repair ecology, and photic mat risk.
- Do not route scanner text through Markdown at runtime.
- Do not add scan reveals before each packet's existing `unlock.primary`.

Audio agents:

- Existing audio fields were not rewritten in this pass.
- Audio remains short bark/support text, not primary instruction.
- Do not infer new VO scope from these PDA text changes.

Runtime/import agents:

- IDs, title keys, article IDs, unlock gates, POI tags, biome tags, locale keys, and direction values are unchanged.
- Non-English rows are stale against the new English source and require localization refresh before native-final publication.
- Related links were not added because no current verified schema field exists in the selected in-game wiki packet rows.
- Delivery alignment is static-doc only. No Unity object reference is claimed.

Player-facing result:

- Pump articles tell the player what to watch and distrust.
- Sonar article tells the player how return routes decay.
- First-hour article preserves bright-shelf value and frames early evidence.
- Maintenance ecology article keeps Atlas/life logic non-mystical and operational.
- Photic mat articles keep shallow brightness premium while making oxygen, route, sample, and seam hazards explicit.

## Additional Pass - 2026-06-04

Additional changed English packet rows:

- `P017_AEGIR_MOON_LADDER` in `Docs/Lore/AppliedContent/packets/RS004_AEGIR_SYSTEM_HECTON8_ECOLOGY.packets.json`
- `P018_HECTON8_DROWNED_GEOLOGY` in `Docs/Lore/AppliedContent/packets/RS004_AEGIR_SYSTEM_HECTON8_ECOLOGY.packets.json`
- `P020_HECTON8_ECOLOGY_REGISTRY` in `Docs/Lore/AppliedContent/packets/RS004_AEGIR_SYSTEM_HECTON8_ECOLOGY.packets.json`
- `P031_PHOTIC_SHELF_LIFE` in `Docs/Lore/AppliedContent/packets/RS007_DEPTH_ECOLOGY_FACTORY_TEMPLE.packets.json`
- `P032_PRESSURE_LADDER_DEPTH_BANDS` in `Docs/Lore/AppliedContent/packets/RS007_DEPTH_ECOLOGY_FACTORY_TEMPLE.packets.json`
- `P033_CABLE_REEF_SYMBIOSIS` in `Docs/Lore/AppliedContent/packets/RS007_DEPTH_ECOLOGY_FACTORY_TEMPLE.packets.json`

Scanner and PDA agents:

- Use packet JSON as source. The updated en_US Markdown mirrors are parity copies, not runtime authority.
- `P017` now depends on the current Aegir ladder names: Skarn, Vela, Claw, Lumen, Thorne, Anvil, Kestrel, HECTON-8, Mute.
- `P032` carries explicit depth bands and should not be surfaced before its existing unlock.
- Do not treat bright photic water as a low-risk zone; the updated text makes visibility useful while route return and oxygen remain active risks.

Localization agents:

- Refresh all non-English rows for the six additional packet IDs from the updated en_US authority.
- Prioritize `P017` because old moon names remain in non-English rows.
- Keep RTL review separate for `ar_SA` and `he_IL`.

Runtime/import agents:

- IDs, title keys, unlock gates, POI tags, biome tags, locale keys, and text directions are unchanged.
- No new runtime dependency, registry route, Unity object, or PDA database import was introduced.
- `AppliedLoreRuntimeAudit.py --source-only` is still blocked by unrelated `external_site/ru_RU/P456_SITE_HOME_LONGFORM_BRIEF.md` frontmatter.
