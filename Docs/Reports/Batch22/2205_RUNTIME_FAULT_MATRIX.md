# 2205 Runtime Fault Matrix

Status: STATIC LOG AUDIT / RUNTIME CLEANLINESS REJECTED
Agent: 2205
Scope: `Docs/AgentLogs/UnityEditor_visual_audit*.log`, screenshot route scripts, named 1465-1473 proof packet logs. No Unity, no builds.

## Evidence Files

| File | Last write | Evidence class | Notes |
|---|---:|---|---|
| `Docs/AgentLogs/UnityEditor_visual_audit.log` | 2026-06-04 11:27:46 | STATIC LOG | Old compile-failure evidence: `PowerGridPrefabFactory.cs` missing `WorldProceduralFinalPrefabQualityGate`. Stale for latest packet unless no newer compile proof supersedes it. |
| `Docs/AgentLogs/UnityEditor_visual_audit_restart.log` | 2026-06-04 16:18:57 | STATIC LOG | 1465-era runtime fault evidence, including repeated `ArgumentNullException` and `H8_PLAYMODE_EXIT_AFTER_INVALID_FORCED_LOAD_1465`. |
| `Docs/AgentLogs/UnityEditor_visual_audit_restart_1468.log` | 2026-06-04 17:34:30 | STATIC LOG | Latest found visual-audit runtime log. Contains repeated `ArgumentNullException` in `HectonCelestialEngine.UpdateAegirMaterial()` and early MCP transport failures. No clean post-1473 capture tail found. |

## Fault Matrix

| Fault | Evidence | Current classification | Owner domain | Required proof to close |
|---|---|---|---|---|
| Repeated `ArgumentNullException: Value cannot be null. Parameter name: dest` | `UnityEditor_visual_audit_restart.log` lines around 24866-25817; `UnityEditor_visual_audit_restart_1468.log` first seen around line 5603, last sampled around 16008. Stack: `Renderer.GetPropertyBlock(null)` -> `HectonCelestialEngine.UpdateAegirMaterial()` line 6724 -> `FlushCelestialVisualSync()` -> `LateFrameTick()` -> `SystemDispatcher.RunDispatcherLateFrame()`. | UNRESOLVED. Latest log still contains repeated fault. | Celestial/material runtime owner; dispatcher late-frame route is affected consumer path. | Code fix or material-property-block init proof plus clean Play Mode/GameView route log after the fix. Must show no repeated exception across capture sequence. |
| `H8_PLAYMODE_EXIT_AFTER_INVALID_FORCED_LOAD_1465` | `UnityEditor_visual_audit_restart.log` line 25828. | UNRESOLVED FOR 1465 PACKET; PENDING FOR LATEST. No later log proves clean 00 -> 01 -> 02 route or assigned direct 02 route without invalid forced load. | Bootstrap/scene-load owner. | Clean route log: 00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD, or explicitly assigned direct 02 route, with no forced invalid load exit. |
| MCP transport connection failures | `UnityEditor_visual_audit_restart_1468.log` lines 634, 716, 863; `UnityEditor_visual_audit_restart.log` lines 26154, 26220. | PENDING PROOF. It can corrupt proof collection but is not the visual runtime itself. | MCP bridge/proof harness owner. | Clean capture-session log showing MCP bridge stable or documented fallback capture route. |
| Forced synchronous recompile/import refresh during audit session | `UnityEditor_visual_audit_restart.log` lines 16707, 17373, 21050, 23803 and associated asset pipeline refresh lines; `UnityEditor_visual_audit_restart_1468.log` lines 3809, 4047, 4513. | STALE/SESSION NOISE unless followed by runtime capture without import loop. Still a risk if screenshots are written under `Assets`. | Editor/import/proof harness owner. | Capture route writes outside `Assets` plus log showing no screenshot import loop during proof packet. |
| Old compile failure: missing `WorldProceduralFinalPrefabQualityGate` | `UnityEditor_visual_audit.log` line 3614. | STALE/NOT CURRENT for 1472+ unless no newer compile/import proof exists. | Editor assembly owner. | Current compile/import log, not required from 2205 because no build/Unity slot. |
| Missing named visual objects from earlier pass | `UnityEditor_visual_audit_restart.log` lines 20777-20779: `H8_PHOTIC_REEF_ASSET_PASS_1457 missing`, `H8_UnderwaterSurfaceSheet_1455 missing`, `H8_UnderwaterHazeCurtain_1454 missing`. | STALE/NOT CURRENT for 1468+ unless same objects are still required by current scene route. | Water/rendering/world owner. | Current scene-object proof or explicit deprecation note from Unity owner. |
| Screenshot route to `Assets/Screenshots` | `MMScreenshot.cs` contains legacy constant but resolves default/legacy names to `Docs/Screenshots`; `MMScreenshotEditor.cs` writes `Docs/Screenshots`. Named MCP artifacts are in `Docs/Screenshots/MCP`. | RESOLVED BY STATIC SOURCE FOR MMScreenshot ROUTE; MCP subfolder capture owner still needs explicit route note. | Screenshot tool/proof harness owner. | Capture-session log or script showing named packet route writes to `Docs/Screenshots/MCP` only. |
| 1472/1473 post-capture clean status | Latest found log `UnityEditor_visual_audit_restart_1468.log` last write 17:34:30; 1473 screenshots written 17:35:18-17:35:42. | PENDING/UNPROVEN. Latest screenshots are newer than latest log; no clean runtime tail after 1473 capture found. | Proof harness owner plus runtime fault owners. | Single capture-session log after final screenshot with `isPlaying`, `isCompiling`, `isUpdating`, no exception/error spam, and no forced exit. |

## Current Runtime Verdict

REJECT / PENDING UNITY OWNER. Runtime stability cannot be claimed. The latest available visual-audit log contains repeated celestial `ArgumentNullException`; the 1465 forced-load exit remains unresolved for that packet; and no clean log tail proves 1472 or 1473 capture happened after faults were fixed.

## Minimum Runtime Proof For Stability

Required before any visual packet can be accepted:

1. Clean route log for `00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD`, or explicitly assigned direct `02_HECTON_WORLD` route.
2. No `H8_PLAYMODE_EXIT`, invalid forced load, forced-load fallback, repeated null, exception, shader error, material error, or screenshot import loop during the capture window.
3. Post-capture log tail after the final screenshot showing `isPlaying`, `isCompiling=False`, `isUpdating=False` or equivalent stable state.
4. Screenshot artifacts written under `Docs/Screenshots/MCP`, not `Assets/Screenshots`.
5. If any fault occurred earlier in the session, owner, route, fix evidence, and fresh clean re-run must be present.
