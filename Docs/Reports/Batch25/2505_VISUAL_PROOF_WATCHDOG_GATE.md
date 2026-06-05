# 2505 Visual Proof Watchdog Gate

Status: STATIC WATCHDOG GATE / NO UNITY RUN / NO RUNTIME ACCEPTANCE
Agent: 2505
Evidence class: STATIC_DOC + STATIC_FILESYSTEM + STATIC_LOG_TAIL
Scope: next visual proof packet for 1475/current route owner.

## Boundary

No Unity, build, import, scene edit, code edit, material edit, shader edit, or asset edit was performed by 2505.

This gate does not accept visuals. It defines the reject/acceptance checklist the orchestrator must apply to the next Unity-owner packet. Static docs and old logs remain lower evidence than same-session player capture plus clean post-capture log.

First-20-minutes blocker removed: visual proof packet ambiguity for surface, shoreline, photic shallows, medium-depth route, Aegir/sky, and compact oblique route proof.

## Authority Read

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `TASTE.md`
- `VISION_LOCKS.md`
- `quality.md`
- `testing.md`
- `presentation.md`
- `water.md`
- `rendering.md`
- `Docs/Reports/Batch23/2305_VISUAL_ACCEPTANCE_RUBRIC.md`
- `Docs/Reports/Batch24/2401_CURRENT_SCENE_DELTA_UNDERWATER_CUT_AUDIT.md`
- `Docs/Reports/Batch24/2402_UNDERWATER_MATERIAL_RECEIVER_AUDIT.md`

Mandates loaded:

- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `.agents-skills/REND_VFX_Fluid_Aesthetics_Compute_Particles.txt`
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`

## Latest Static Evidence Checked

Latest screenshot directory: `Docs/Screenshots/MCP`

Latest 1474 diagnostic files:

| File | Last write | SHA256 |
|---|---:|---|
| `h8_1474_diag_surface_from_mcp.png` | 2026-06-04 18:43:40 | `BCC226BECDB9FDFA0201BEA13F376AD0810F29A835ED978E5B50CCFD3753F9AD` |
| `h8_1474_diag_shore_foam_from_mcp.png` | 2026-06-04 18:43:42 | `F8872A7FE82349B7C98913136EBF699EC935ABE1473B6C9C355EA74988088A5D` |
| `h8_1474_diag_underwater_route_from_mcp.png` | 2026-06-04 18:43:43 | `8902EB2BECEB83C2080FA9B10B1FCFAC3A465A5D9886A2762AD49620732F3495` |

Latest relevant log checked: `Docs/AgentLogs/UnityEditor_visual_audit_restart_1474b.log`

- Last write observed: 2026-06-04 19:15:22.
- Fault token counts in the checked log:
  - `Access token is unavailable`: 1
  - `not valid. Loading of assembly skipped`: 427
  - `Found 1 leak`: 8
  - `UnityEngine.Debug:LogError`: 4
  - `System.Exception`: 19
  - `Asset Pipeline Refresh`: 28
  - `CompileScripts`: 6
  - `H8_PLAYMODE_EXIT`: 0
- Tail still contains MCP WebSocket warning stack, asset import activity, and Asset Pipeline Refresh after the 1474 screenshots.

`Assets/Screenshots` check:

- Directory exists.
- Child count observed: 0.
- No current screenshot import-loop file evidence found there. This does not clear import-loop risk if a future packet writes captures under `Assets`.

No 1475 screenshots or metadata manifest were found in `Docs/Screenshots/MCP` at this pass.

## Current Static Verdict On 1474 Diagnostics

Verdict: REJECTED VISUAL PROOF.

Reasons:

- `MISSING_VIEW`: latest 1474 has only 3 diagnostic screenshots, not the required 6-view packet.
- `MISSING_METADATA`: no manifest/checksum/camera/depth/quality/log metadata beside the 1474 diagnostics.
- `RUNTIME_FAULT_TAIL`: latest checked log contains errors, warnings, exceptions, native leak reports, invalid assembly skips, import refreshes, and compile entries.
- `SLAB_HARD_CUT`: Batch24 static audits identify the current underwater route as still showing hard horizontal slab/cut risk.
- `NO_CAUSTIC_PROOF`: Batch24 material audit identifies caustic response as a small streak/sheet-risk receiver, not accepted shallow water lace.
- `EMPTY_UNDERWATER`: Batch24 audits still reject the underwater route for weak volume layering, empty/flat seabed read, and pasted substrate risk.

This is reject-only evidence. It cannot be promoted by prose.

## Required 1475 Packet Files

All files must come from one capture session, one route state, and one quality lane. Diagnostic substitutes do not replace packet views.

Naming format:

`h8_1475_s01_[view]_[q000-100]_[uion|uioff]_[yyyyMMdd_HHmmss].png`

Required files:

| Required view | Required filename pattern | Must show |
|---|---|---|
| Surface | `h8_1475_s01_surface_coast_aegir_q060_uioff_*.png` | Bright ocean surface, wave/specular response, coastline/terrain, sky/clouds, Aegir context, no UI. |
| Shoreline close | `h8_1475_s01_shoreline_close_1m_q060_uioff_*.png` | 1 m waterline, organic foam contact, wet rock transition, material breakup, scale cue. |
| Underwater 0-5 m | `h8_1475_s01_underwater_0_5m_q060_uioff_*.png` | Actual underwater camera, photic clarity, water volume, seabed/rocks/biota/route, justified caustic/floor response. |
| Underwater 20-50 m route | `h8_1475_s01_underwater_20_50m_route_q060_uioff_*.png` | Actual medium-depth route, near/mid/far depth structure, return cue, terrain silhouette, no water-plane cut. |
| Aegir/celestial long | `h8_1475_s01_aegir_celestial_long_q060_uioff_*.png` | Large textured Aegir/moons/sky behind horizon/atmosphere, readable scale, not primitive disc/stripe. |
| Regression low-oblique | `h8_1475_s01_regression_low_oblique_q060_uioff_*.png` | Compact-like oblique composition with water/shore/terrain/sky or route cue still readable. |
| Metadata manifest | `h8_1475_s01_manifest.json` | All screenshot paths, file sizes, SHA256, local/UTC timestamps, scene, route state, camera transform/FOV/depth band, capture source, UI state, `GlobalQualityWeight`, render scale, post/underwater/foam/caustic/fog states, route harness version, log path, fault summary. |
| Log tail copy or pointer | `Docs/AgentLogs/UnityEditor_visual_audit_1475_s01.log` or manifest `log_path` | Must be newer than final screenshot, stable before judging, and clean for the capture window. |

If `GlobalQualityWeight` is not `0.60`, replace `q060` with the actual continuous value as a three-digit integer. Binary quality labels are not enough.

## Reject-Code Mapping For 1475/Next Route

| Code | Trigger | Required correction before another packet |
|---|---|---|
| `MISSING_VIEW` | Any of the six required views is absent or replaced by a diagnostic variant. | Recapture all six views in one session. |
| `FALSE_LABEL` | File label says underwater/shore/surface but image composition does not match that route state. | Recapture with manifest camera/depth proof. |
| `MISSING_METADATA` | Manifest missing or does not include timestamps, camera/depth, quality, toggles, checksums, and log path. | Provide `h8_1475_s01_manifest.json` with exact fields. |
| `MISSING_FOAM` | Shoreline lacks organic foam/wet contact, or foam is a uniform sheet/lace/grid/debug overlay. | Prove contact-cause foam and wetness transition at shoreline close view. |
| `NO_CAUSTICS` | Lit shallow/underwater receivers lack justified caustic/floor response, or caustics are only a streak/noise/sheet. | Prove subtle broken caustic lace on valid lit receivers; no global neon. |
| `EMPTY_UNDERWATER` | Underwater view is transparent, empty, surface duplicate, or lacks near/mid/far terrain, substrate, biota, route, scale, or volume. | Add or prove route structure, seabed material, haze layering, and return cue. |
| `SLAB_HARD_CUT` | Hard horizontal shelf, rectangular blue wall, visible plane, service cube, waterline slice, ceiling lid, curtain, or rendered occlusion helper. | Disable/replace visible service geometry with authored terrain/water/fog fake that preserves route readability. |
| `STALE_LOG` | Log missing, log `LastWriteTime` older than final screenshot, no post-capture tail, moving log during judgment, or log lacks capture-window closure. | Provide stable clean log tail after final screenshot. |
| `RUNTIME_WARNING` | Capture-window log contains `Error`, `Exception`, `Warning`, `LogError`, `Found 1 leak`, shader/material errors, invalid assembly skips, compile/import/update loop, or forced-load exit. | Fix or isolate runtime/editor fault and recapture. |
| `AEGIR_SKY_SHORE_FAIL` | Surface/sky/Aegir/shore is dark, muddy, one-note, crayon-like, weakly textured, below Subnautica-level, or uses darkness/fog to hide weak art. | Prove bright readable ocean, textured Aegir/sky, wet shore, material breakup, and route/scale cue. |

Any single reject code blocks visual acceptance.

## Objective Freshness Checks

The orchestrator should run these checks before taste judging:

1. Screenshot set:
   - All six required view files exist in `Docs/Screenshots/MCP` or another `Docs` proof directory.
   - All six share packet id `1475`, session id `s01` or later, same quality value, and same UI state unless manifest explains a deliberate UI-on duplicate.
   - NTFS `LastWriteTime`, manifest timestamp, file size, and SHA256 agree for each screenshot.
   - No required file is reused from 1473/1474 or a diagnostic route.

2. Manifest:
   - `h8_1475_s01_manifest.json` exists.
   - Manifest includes scene, route state, camera transform/FOV/depth band, capture source, UI state, `GlobalQualityWeight`, render scale, post stack, underwater renderer, fog, foam, caustic, and water states.
   - Manifest names the exact log path and capture harness/script version.

3. Log tail:
   - Log `LastWriteTime` is after the final screenshot.
   - Log file is stable for at least 60 seconds before judging.
   - Tail covers the capture window after the final screenshot.
   - Tail has zero reject tokens for the capture window: `Error`, `Exception`, `Warning`, `LogError`, `Found 1 leak`, `shader error`, `material error`, `not valid. Loading of assembly skipped`, `CompileScripts`, `Asset Pipeline Refresh`, `H8_PLAYMODE_EXIT`, `forced`.

4. `Assets/Screenshots` import-loop check:
   - No packet screenshots are written under `Assets`.
   - `Assets/Screenshots` is absent or empty.
   - Latest log does not show Asset Pipeline Refresh triggered by screenshot file writes.

5. Console/runtime faults:
   - No relevant console errors/warnings during or after capture.
   - No import/update/compile loop during capture.
   - No native leak report in same session.
   - No shader/material/URP/Crest fault.

If a log is still being written during judgment, verdict stays `PENDING VERIFICATION` or `REJECTED VISUAL PROOF`; never accepted.

## Visual Checklist

| Gate | Pass requirement | Reject examples |
|---|---|---|
| Surface | Bright, readable ocean color; waves/specular; terrain/coast material; sky/cloud/Aegir context; route or scale cue. | Muddy/dark surface, one-note tint, flat water plane, weak Aegir, no route cue. |
| Shoreline | Foam has contact cause; wetness transition; textured rock; shallow transparency/depth falloff. | Missing foam, grid/lace/sheet foam, hard black strips, pale slab shore. |
| Underwater 0-5 m | Actual underwater state; photic clarity; visible volume; seabed/rocks/biota/route; justified caustic/floor response. | Surface duplicate, empty seabed, no caustics where lit, hard waterline cut, acid/flat tint. |
| Underwater 20-50 m | Actual route depth; near/mid/far haze structure; route/return cue; terrain silhouette; no clipping. | Flat shell terrain, empty haze, hard slab, same frame as shallow/surface. |
| Aegir/sky | Aegir large, textured, atmospheric, stable behind horizon; sky/clouds readable. | Primitive disc, muddy sine stripes, pasted sphere, invisible celestial proof. |
| Low-oblique | Compact-like composition still reads water, shore/terrain, sky or route; no LOD collapse. | Flat planes, clipped water, no route/scale cue, hidden debug overlay. |

## One-Page Judge Form

Packet:
Session:
Judge:
Evidence class:
Screenshot directory:
Manifest path:
Log path:

Freshness:

| Check | PASS/FAIL | Evidence |
|---|---|---|
| Six required screenshots present |  |  |
| Manifest present and checksum/timestamps match |  |  |
| Log newer than final screenshot and stable 60 seconds |  |  |
| No `Assets/Screenshots` screenshot writes |  |  |
| No runtime/editor fault tokens in capture window |  |  |

Visual gates:

| View | PASS/FAIL | Reject code(s) | Notes |
|---|---|---|---|
| Surface |  |  |  |
| Shoreline close |  |  |  |
| Underwater 0-5 m |  |  |  |
| Underwater 20-50 m |  |  |  |
| Aegir/celestial |  |  |  |
| Regression low-oblique |  |  |  |

Verdict:

- `REJECTED VISUAL PROOF` if any freshness or visual gate fails.
- `PLAYER-CAPTURE CANDIDATE` only if every gate passes and same-session artifacts exist.
- `PENDING VERIFICATION` if artifacts are incomplete, moving, or lower evidence than the claim.

Top reject codes:

1.
2.
3.

Required recapture/fix:

-
-
-

## Scalability Consequences For The Judge

- Compact/Low: reject any packet where ocean color, shore wetness, terrain silhouette, Aegir/sky, route cue, or instrument readability collapses. Compact can reduce density/resolution, not art direction.
- Middle: must look genuinely good, not merely functional. It needs foam/wet contact, underwater volume, material response, and route structure.
- High: must spend available budget on richer water/shore material, caustic hints, silt/particle layering, and stronger sky/Aegir integration without changing route truth.
- Ultra: may add sensory overkill, but it cannot hide unresolved slabs, empty seabed, stale logs, or runtime faults.

## Final Gate Sentence

The next packet is accepted only when six same-session screenshots, manifest, stable clean log tail, and visual review all pass. Any missing view, missing metadata, stale/faulted log, visible slab/cut, absent foam/caustics, empty underwater route, or weak surface/sky/Aegir/shore result is `REJECTED VISUAL PROOF`.
