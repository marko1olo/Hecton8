# RS005 Carrier / Escape / Deep Reach Pressure

Status: production-facing draft.

Purpose: convert ship ownership, crash stranding, Deep Reach return pressure, false material exit, and player motive into applied packets usable by PDA, scanner, terminals, VO subtitles, wiki pages, website copy, route cards, binding maps, and DataMonolith bake.

## Packets

- `P021_BLACK_KEEL_CUSTODY`: Black Keel as public claim-pool custody, not a loyal personal ship.
- `P022_DROP_CAPSULE_DAMAGE`: damaged drop capsule as shelter, not ascent vehicle.
- `P023_DEEP_REACH_RETURN_CLAIM`: current Deep Reach goal: coordinates, resource, Atlas access, evidence control.
- `P024_FALSE_EXIT_MATERIAL`: profitable partial ending that does not resolve the truth chain.
- `P025_PROFESSIONAL_MOTIVE`: professional interest becoming personal through Barnard/frontier evidence.

## Runtime Boundary

Authoring JSON and markdown are not runtime inputs. Runtime consumes baked packet records, route records, hashes, masks, and localized string-pool slices only.
