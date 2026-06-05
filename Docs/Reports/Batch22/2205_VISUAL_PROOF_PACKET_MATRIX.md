# 2205 Visual Proof Packet Matrix

Status: STATIC VERIFIED AUDIT / VISUAL ACCEPTANCE REJECTED
Agent: 2205
Scope: `Docs/Screenshots/MCP`, visual-audit logs, proof route labels. No Unity, no builds, no Play Mode.

## Authority Basis

- `AGENTS.md`: no accepted visual/runtime claims without current proof.
- `TASTE.md`, `VISION_LOCKS.md`: surface, shoreline, Aegir, photic shallows, and medium-depth hero routes must be bright, readable, premium, and at least Subnautica-level.
- `quality.md`: static evidence cannot upgrade to runtime proof; screenshot labels must match artifacts.
- `water.md`: underwater proof must show readable water volume, depth, route structure, caustic/foam/waterline reason where relevant, and cannot be generic blue/flat planes.
- `world.md`: world proof must show route decision, geology/industry/ecology reason, landmark, and low-tier readability.

## Required Packet Columns

Minimum full visual packet:

| Required artifact | Acceptance rule |
|---|---|
| Surface | Bright/readable ocean, coast, Aegir/sky context, no darkness hiding weak art. |
| Shoreline close | Waterline, foam/wetness/material transition, shoreline scale, no flat strip. |
| Underwater 0-5 m | Actual shallow underwater/GameView-like proof with post stack and photic clarity. |
| Underwater 20-50 m | Actual medium-depth route proof with depth cue, terrain/route, turbidity, no surface-plane clipping. |
| Aegir/celestial | Readable textured Aegir/moons/sky, not muddy procedural stripes. |
| Regression low oblique | Low/compact readability angle, no broken water/terrain composition. |
| Log | Current corresponding runtime log. |
| Fault status | Clean or named unresolved faults. |

## Matrix

| Packet | Surface | Shoreline | Underwater 0-5 m | Underwater 20-50 m | Aegir/celestial | Regression | Log | Fault status | Verdict |
|---|---|---|---|---|---|---|---|---|---|
| 1465 | `h8_1465_game_surface_ui_off.png` present. | `h8_1465_shoreline_close_1m.png` present. | `h8_1465_underwater_0_5m_open.png` present. | `h8_1465_underwater_20_50m_route.png` present. | `h8_1465_aegir_long.png` present. | `h8_1465_regression_low_oblique.png` present. | `UnityEditor_visual_audit_restart.log`. | Repeated `ArgumentNullException` in `HectonCelestialEngine.UpdateAegirMaterial`; `H8_PLAYMODE_EXIT_AFTER_INVALID_FORCED_LOAD_1465`. | REJECT. Full labels exist, runtime fault gate fails. |
| 1466 | `h8_1466_surface_clean_ui_off.png` present. | `h8_1466_shoreline_clean_1m.png` present. | Missing. | Missing. | Missing as separate proof. | `h8_1466_regression_low_oblique_clean.png` present. | Same evidence family only. | Underwater proof absent. | REJECT. Incomplete packet. |
| 1467 | `h8_1467_surface_teal_ui_off.png` present. | `h8_1467_shoreline_teal_1m.png` present. | Missing. | Missing. | Missing as separate proof. | `h8_1467_regression_teal_low_oblique.png` present. | Same evidence family only. | Underwater proof absent. | REJECT. Incomplete packet. |
| 1468 | `h8_1468_surface_coast_aegir_ui_off.png` present. | `h8_1468_shoreline_close_1m.png` present. | `h8_1468_underwater_0_5m.png` present. | `h8_1468_underwater_20_50m_route.png` present. | Surface file includes Aegir; no separate long proof found. | `h8_1468_regression_low_oblique.png` present. | `UnityEditor_visual_audit_restart_1468.log`. | Repeated `ArgumentNullException` begins in latest restart log; MCP transport failures also present. | REJECT. Runtime fault gate fails; visual packet not accepted. |
| 1469 | `h8_1469_surface_coast_aegir_ui_off.png` present. | `h8_1469_shoreline_close_1m.png` present. | `h8_1469_underwater_0_5m.png` present but visually invalid: hard horizon/surface split and flat pale plane. | `h8_1469_underwater_20_50m_route.png` present but detached/invalid-looking route proof. | Surface file includes Aegir; no separate long proof found. | `h8_1469_regression_low_oblique.png` present. | `UnityEditor_visual_audit_restart_1468.log`. | Repeated celestial `ArgumentNullException`; no clean post-capture log. | REJECT. Underwater visual proof fails. |
| 1470 | `h8_1470_surface_coast_aegir_ui_off.png` present. | `h8_1470_shoreline_close_1m.png` present. | `h8_1470_underwater_0_5m.png` present; same invalid shallow-water pattern as 1469-1472. | `h8_1470_underwater_20_50m_route.png` present; surface-plane clipping/flatness risk. | Surface file includes Aegir; no separate long proof found. | `h8_1470_regression_low_oblique.png` present. | `UnityEditor_visual_audit_restart_1468.log`. | Repeated celestial `ArgumentNullException`; no clean post-capture log. | REJECT. Underwater visual proof fails. |
| 1471 | `h8_1471_surface_coast_aegir_ui_off.png` present. | `h8_1471_shoreline_close_1m.png` present. | `h8_1471_underwater_0_5m.png` present; same invalid shallow-water pattern as 1469-1472. | `h8_1471_underwater_20_50m_route.png` present; surface-plane clipping/flatness risk. | Surface file includes Aegir; no separate long proof found. | `h8_1471_regression_low_oblique.png` present. | `UnityEditor_visual_audit_restart_1468.log`. | Repeated celestial `ArgumentNullException`; no clean post-capture log. | REJECT. Underwater visual proof fails. |
| 1472 | `h8_1472_surface_coast_aegir_ui_off.png` present. | `h8_1472_shoreline_close_1m.png` present. | `h8_1472_underwater_0_5m.png` present; inspected: hard horizontal water/sky/material split and flat pale plane. | `h8_1472_underwater_20_50m_route.png` present; inspected: surface plane cuts through frame, shallow/medium-depth composition not credible. | Surface file includes Aegir; no separate long proof found. | `h8_1472_regression_low_oblique.png` present. | `UnityEditor_visual_audit_restart_1468.log`. | Repeated celestial `ArgumentNullException`; no clean post-capture log. | REJECT. Latest required packet before 1473 still fails visual and runtime gates. |
| 1473 | `h8_1473_surface_coast_aegir_ui_off.png` present. | `h8_1473_shoreline_close_1m.png` present. | `h8_1473_underwater_0_5m.png` present but inspected: composition matches surface/coast view, not underwater proof. | `h8_1473_underwater_20_50m_route.png` present but inspected: composition matches surface/coast view, not medium-depth proof. | `h8_1473_aegir_longshot_crop_source.png` present; surface includes Aegir. | `h8_1473_regression_low_oblique.png` present. | Latest log is older than 1473 screenshot timestamps and has no clean post-capture runtime tail. | Fault status after 1473 capture is not proven clean. | REJECT. Latest packet has mislabeled underwater artifacts. |

## Current Visual Verdict

REJECT / PENDING UNITY OWNER. Packets 1468-1473 contain enough filenames to look complete, but the visual content does not satisfy the underwater 0-5 m and 20-50 m gates, and the logs do not prove a clean runtime route after capture.

## Detached Temp-Camera Risk

Underwater captures that do not show actual GameView underwater state, active post stack, water volume behavior, route depth, and normal camera path are insufficient. A temporary or detached proof camera can bypass post-processing, waterline clipping, fog/turbidity, UI/cockpit overlays, and culling/layer behavior. Current underwater screenshots show exactly that risk: flat pale planes, hard surface cuts, or surface-view duplication.

## Allowed Proof Labels

- `STATIC VERIFIED`: file/log/source/report inspected only.
- `PLAYER-CAPTURE VERIFIED`: screenshot exists and visually matches its route label.
- `PENDING VERIFICATION`: label exists but route/runtime proof is missing or suspect.
- `REJECTED VISUAL PROOF`: artifact exists but fails route/taste/content requirements.

Forbidden label upgrade: no `accepted`, `pass`, `clean`, `stable`, `runtime verified`, or `visual accepted` without the full visual packet plus clean runtime fault status.
