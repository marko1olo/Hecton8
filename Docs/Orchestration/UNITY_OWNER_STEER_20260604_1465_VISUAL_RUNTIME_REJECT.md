# Unity Owner Steer 2026-06-04 1465 Visual Runtime Reject

Target visible thread: `Продолжить работу по логам`

Use as steer only. Do not start a new Unity owner.

## Evidence Reviewed

- `Docs/Screenshots/MCP/h8_1465_game_surface_ui_off.png`
- `Docs/Screenshots/MCP/h8_1465_shoreline_close_1m.png`
- `Docs/Screenshots/MCP/h8_1465_underwater_0_5m_open.png`
- `Docs/Screenshots/MCP/h8_1465_underwater_20_50m_route.png`
- `Docs/Screenshots/MCP/h8_1465_aegir_long.png`
- `Docs/Screenshots/MCP/h8_1465_regression_low_oblique.png`
- `Docs/AgentLogs/UnityEditor_visual_audit_restart.log`

## Steer Text

1465 is NOT accepted. Treat it as a visual and runtime reject.

Runtime/proof issue:
- Unity log shows repeated `ArgumentNullException: Value cannot be null`.
- Unity log also shows `H8_PLAYMODE_EXIT_AFTER_INVALID_FORCED_LOAD_1465`.
- Do not call the pass complete while forced-load/null exceptions exist.

Visual reject points:
- Water became over-bright acid/turquoise and reads like a flat tinted plane with black smear/patched artifacts. It is not premium ocean surface.
- White dash/debug artifacts remain visible on the horizon/water band.
- Underwater captures still show a hard white horizontal plane/band and wire/debug-like lines. This must be fixed before adding clutter.
- The underwater route still has a flat grey-green seabed with weak material identity and almost no believable photic reef/geology route.
- The dark floating/underwater shapes read as primitive shards/proxy scatter, not fauna/rocks/flora with authored silhouettes.
- Island terrain still reads as grey striped terrain shell, not authored coastline/geology.
- Aegir/moon/celestial read is still weak or primitive in the checked captures.

Next priorities:
1. Fix the `ArgumentNullException`/forced-load exit before any acceptance claim.
2. Remove white debug/dash/wire artifacts and the underwater white band.
3. Stabilize ocean color/material response; avoid acid-turquoise flat-plane look.
4. Remove primitive-looking scatter/shards if they are not final authored assets.
5. Use active-scene rows from `Docs/Reports/Batch21/2104_PRIMITIVE_NULL_DEFAULT_STATIC_VALIDATOR.csv` to hunt primitive/default/null/proxy sources.
6. After fixes, produce a fresh proof set: surface UI off, shoreline close, 0-5 m underwater, 20-50 m route, Aegir long, regression low oblique.

Do not optimize the look by hiding it with fog/darkness. Surface and photic water remain bright but must also be detailed and believable.
