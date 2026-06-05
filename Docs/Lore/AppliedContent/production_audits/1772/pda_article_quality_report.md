# PDA Article Quality Report 1772

Evidence class: STATIC_SOURCE / STATIC_DOC.

## Changed Pages

| packet_id | page | before issue | after result |
|---|---|---|---|
| `P046_PUMP_ROOM_HANDSHAKE` | `Docs/Lore/AppliedContent/in_game_wiki/en_US/P046_PUMP_ROOM_HANDSHAKE.md` | internal "defines" language; weak action value | tells the player to read intake, outlet pressure, return-corridor sound, and pump tradeoff |
| `P049_SONAR_RETURN_ROUTE` | `Docs/Lore/AppliedContent/in_game_wiki/en_US/P049_SONAR_RETURN_ROUTE.md` | navigation design note | tells the player stale pings can change with surge, silt, fauna, cable, and cargo load |
| `P060_FIRST_HOUR_SPINE` | `Docs/Lore/AppliedContent/in_game_wiki/en_US/P060_FIRST_HOUR_SPINE.md` | opening-structure spec | turns first-hour sequence into recovered evidence and preserves bright-shelf value |
| `P061_MAINTENANCE_ECOLOGY` | `Docs/Lore/AppliedContent/in_game_wiki/en_US/P061_MAINTENANCE_ECOLOGY.md` | taxonomy lecture | tells the player which living surfaces can conduct, seal, repeat, tag, or misroute |
| `P221_PHOTIC_MAT_BASELINE` | `Docs/Lore/AppliedContent/in_game_wiki/en_US/P221_PHOTIC_MAT_BASELINE.md` | shallow contrast spec | makes bright mats a route cue, oxygen source, and contamination risk |
| `P291_PHOTIC_MAT_CODEX_CARD` | `Docs/Lore/AppliedContent/in_game_wiki/en_US/P291_PHOTIC_MAT_CODEX_CARD.md` | specimen card talked to writers | tells the player how to sample loose edges without damaging sealed seams |

## Delivery Route Check

| packet_id | map fit | result |
|---|---|---|
| `P046_PUMP_ROOM_HANDSHAKE` | early pressure machinery / first wreck route | `ROUTE_OK_EARLY_GAME` |
| `P049_SONAR_RETURN_ROUTE` | early return-vector and route-decay route | `ROUTE_OK_EARLY_GAME` |
| `P060_FIRST_HOUR_SPINE` | starting/early first-hour chain | `ROUTE_OK_STARTING_TO_EARLY` |
| `P061_MAINTENANCE_ECOLOGY` | cable reef symbiosis / Atlas maintenance ecology | `ROUTE_OK_MIDGAME` |
| `P221_PHOTIC_MAT_BASELINE` | photic shelf life baseline contrast sample | `ROUTE_OK_EARLY_GAME` |
| `P291_PHOTIC_MAT_CODEX_CARD` | photic shelf ecology specimen card | `ROUTE_OK_EARLY_GAME` |

No Unity delivery object or runtime PDA database reference was invented. Delivery results are static-map alignment only.

## Related Links

No related-link section exists in the selected packet schema or generated page body. No crosslinks were added because the task requires verified existing article IDs or packet IDs and the current in-game wiki page format does not expose a related-link field.

Verified candidate relationships for future schema work only:

| source packet | candidate relation | candidate packet |
|---|---|---|
| `P046_PUMP_ROOM_HANDSHAKE` | pressure machinery / return-route pair | `P049_SONAR_RETURN_ROUTE` |
| `P049_SONAR_RETURN_ROUTE` | first-hour route pressure pair | `P060_FIRST_HOUR_SPINE` |
| `P060_FIRST_HOUR_SPINE` | first Atlas/life evidence | `P061_MAINTENANCE_ECOLOGY` |
| `P221_PHOTIC_MAT_BASELINE` | specimen-card expansion | `P291_PHOTIC_MAT_CODEX_CARD` |
| `P291_PHOTIC_MAT_CODEX_CARD` | baseline ecology parent | `P221_PHOTIC_MAT_BASELINE` |

## Remaining Weak Pages

- Non-English rows for the selected packets are stale against the edited en_US source.
- Several non-English selected rows still carry draft-native-pass markers or visible mixed-language placeholders.
- Some sibling packet rows in the same packet files still contain writer-facing "defines" text. They were outside the small selected set required by Task 05.
- Encyclopedia Markdown remains mostly writer-facing and weakly mapped to AppliedContent packet IDs. See `encyclopedia_appliedcontent_comparison.md`.

## Quality Scaling

GlobalQualityWeight must scale presentation density, not lore truth.

| tier | consequence |
|---|---|
| Low | title, scanner, unlock state, and one short PDA summary are enough |
| Middle | add field note and terminal bridge |
| High | add audio bark and POI/biome tagging in UI |
| Ultra | add verified related links, route map surfacing, and richer article presentation |

Facts, packet IDs, article IDs, unlock gates, locale keys, and spoiler boundaries do not change by quality tier.

## Dependency Note

No dependency on agents 1770-1771 or 1773-1779 was introduced.

## Additional Pass - 2026-06-04

Additional changed pages:

- `P017_AEGIR_MOON_LADDER`: now teaches moon-order timing, storm-shell windows, wrong-light risk, and the fact that HECTON-8 is the fixed reference, not a decorative sky note.
- `P018_HECTON8_DROWNED_GEOLOGY`: now converts geology into route evidence: ridges, collapsed shelves, canyon funnels, brine curtains, vent scars, cargo cache odds, and upgrade gates.
- `P020_HECTON8_ECOLOGY_REGISTRY`: now separates native shelf life, cable-adapted life, and Atlas repair life without turning the entry into taxonomy wallpaper.
- `P031_PHOTIC_SHELF_LIFE`: now protects the bright-surface floor while giving mat, grazer, pressureweed, oxygen, predator, storm, and route-line consequences.
- `P032_PRESSURE_LADDER_DEPTH_BANDS`: now gives banded operating consequences from 0 to 5600 meters and ties descent to seals, battery draw, oxygen, and return pings.
- `P033_CABLE_REEF_SYMBIOSIS`: now tells the player which cable growths insulate, which cuts harm the route, and which organisms mark drone or repair traffic.

Route and gameplay check:

- Surface and photic shelf remain bright and readable, not darkness-covered.
- Depth pressure is expressed as player decisions: descend, retreat, scan, cut, preserve, upgrade, or distrust stale route data.
- Atlas ecology remains operational and physical. It conducts, seals, routes, misroutes, insulates, and attracts grazers; it is not framed as mystical knowledge.
- Aegir moon ladder is now aligned with the current root canon names.

Remaining weak areas:

- Non-English mirrors for all six additional entries are stale.
- Encyclopedia Markdown still has weaker AppliedContent mapping than the packet source.
- Some sibling packet rows outside the twelve 1772-edited IDs still contain writer-facing phrasing and should be handled by future scoped passes.

Quality scaling:

- Low: show title, unlock state, scanner cue, and one short PDA summary.
- Middle: add field note and terminal bridge.
- High: add audio bark, biome/POI surfacing, and route-risk highlights.
- Ultra: add verified related links and richer route map presentation after the schema has a real related-link field.

Facts, packet IDs, unlock gates, locale keys, and spoiler boundaries do not change by quality tier.
