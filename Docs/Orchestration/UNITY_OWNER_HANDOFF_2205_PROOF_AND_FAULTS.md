# Unity Owner Handoff 2205 Proof And Faults

Status: ACTIONABLE GATES ONLY
Source: Worker 2205 static screenshot/log/proof-route audit.

## Blockers

1. Fix or disable the repeated celestial fault before any new visual acceptance claim:
   - `ArgumentNullException: Value cannot be null. Parameter name: dest`
   - Stack: `Renderer.GetPropertyBlock(null)` -> `HectonCelestialEngine.UpdateAegirMaterial()` line 6724 -> `FlushCelestialVisualSync()` -> `LateFrameTick()` -> `SystemDispatcher.RunDispatcherLateFrame()`.
   - Required proof: clean capture-session log after fix, no repeated exception.

2. Prove scene route stability:
   - Preferred: `00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD`.
   - Allowed only if assigned: direct `02_HECTON_WORLD`.
   - Reject if `H8_PLAYMODE_EXIT`, invalid forced load, forced fallback, or post-load null spam appears.

3. Re-capture underwater proof through the real accepted camera path:
   - `underwater_0_5m`: must show actual shallow underwater state, photic clarity, depth/water volume, and no hard flat plane.
   - `underwater_20_50m_route`: must show medium-depth route/depth cue, terrain/route structure, turbidity, and no surface-plane clipping.
   - If using a temp camera, mark it temp-camera only. It cannot clear GameView/post-stack acceptance.

4. Re-capture the full packet under one ID:
   - surface coast Aegir UI-off;
   - optional UI-on;
   - shoreline close 1 m;
   - underwater 0-5 m;
   - underwater 20-50 m route;
   - Aegir/celestial;
   - regression low oblique;
   - clean runtime log after final screenshot.

5. Screenshot route:
   - Write packet artifacts to `Docs/Screenshots/MCP`.
   - Do not write screenshots to `Assets/Screenshots`.
   - If any screenshot tool path is changed, prove no AssetDatabase import/rebuild loop during capture.

## Current Do-Not-Claim List

- Do not claim visual acceptance.
- Do not claim runtime clean.
- Do not claim underwater proof accepted.
- Do not claim 1465 stability.
- Do not claim 1472/1473 acceptance from filenames alone.

## Owner Domains

| Issue | Owner |
|---|---|
| `HectonCelestialEngine.UpdateAegirMaterial()` null property block | Celestial/material runtime owner |
| `H8_PLAYMODE_EXIT_AFTER_INVALID_FORCED_LOAD_1465` | Bootstrap/scene-load owner |
| Invalid underwater proof route | Water/rendering/camera/proof harness owner |
| MCP transport failures during audit | MCP bridge/proof harness owner |
| Screenshot path hygiene | Screenshot tool/proof harness owner |

## Acceptance After Fix

The controller can reconsider acceptance only after a new single-ID packet has complete visual artifacts plus clean post-capture runtime log. Static reports or screenshots without runtime tail remain `PENDING VERIFICATION`.
