# 2205 Acceptance Packet Checklist

Status: STATIC VERIFIED CHECKLIST / ACCEPTANCE NOT MET
Agent: 2205

## Active Mandates Recorded

1. Proof labels must match artifacts; static files do not prove runtime stability.
2. Surface, shoreline, ocean skin, Aegir, moons, and photic shallows must be bright, readable, premium, and at least Subnautica-level.
3. Underwater 0-5 m and 20-50 m captures must be actual underwater route proof, not detached camera artifacts or mislabeled surface views.
4. Runtime clean status needs current logs; no acceptance with repeated nulls, invalid forced-load exit, or missing post-capture clean tail.
5. Screenshots must stay out of `Assets/Screenshots`; current named proof should route to `Docs/Screenshots/MCP`.
6. Crash/null faults need owner, route, and proof. `HectonCelestialEngine.UpdateAegirMaterial()` currently has an unresolved repeated exception signature.
7. Continuous quality scaling remains required; no low/high binary visual claim can excuse weak compact visuals.
8. No Unity slot, no builds, no Play Mode were used by 2205.

## Full Visual Packet Gate

| Gate | Required artifact | Current result |
|---|---|---|
| Surface | Surface/coast/GameView capture with UI-off and optional UI-on. | Present for 1465, 1466, 1467, 1468, 1469, 1470, 1471, 1472, 1473. Not sufficient alone. |
| Shoreline close | 1 m waterline/shoreline close capture with foam/wetness/material transition. | Present for 1465-1473 except naming variation in 1466/1467. Visual quality still pending/rejected for flat waterline risk. |
| Underwater 0-5 m | Actual shallow underwater route capture with photic clarity and GameView/post-stack truth. | Missing for 1466-1467. 1469-1472 inspected as visually invalid hard-plane/flat split. 1473 inspected as mislabeled surface-like view. |
| Underwater 20-50 m | Actual medium-depth route capture with depth cue, terrain/route, turbidity, no surface-plane clipping. | Missing for 1466-1467. 1472 inspected as surface plane clipping through frame. 1473 inspected as surface-like duplicate. |
| Aegir/celestial | Aegir/sky/moon proof with texture detail and believable atmosphere. | Present as surface composition for 1468-1473; 1465 has `aegir_long`; 1473 has `aegir_longshot_crop_source`. Runtime celestial exception blocks acceptance. |
| Regression low oblique | Low/compact oblique readability proof. | Present for 1465-1473. Does not clear water/depth/runtime failures. |
| Scene/GameView consistency | GameView and scene/capture route truth, no detached-camera bypass. | Not proven. Current underwater frames suggest detached/invalid camera or water/post-stack mismatch. |
| Runtime log | Current clean log after final capture. | Not present. Latest log has repeated `ArgumentNullException`; 1473 screenshots are newer than latest log. |

## Minimum Accepted Proof Labels

Use only:

- `STATIC VERIFIED`: source/report/log/image inventory inspected.
- `PLAYER-CAPTURE VERIFIED`: screenshot exists and visually matches route label.
- `PLAYMODE VERIFIED`: fresh Play Mode route observed and log clean.
- `PROFILER VERIFIED`: profiler/GC/Frame Debugger/Memory Profiler artifact exists.
- `PENDING VERIFICATION`: claim lacks matching proof.
- `REJECTED`: proof exists but fails route, visual, or runtime gate.

Do not use:

- `accepted`
- `stable`
- `clean`
- `pass`
- `runtime safe`
- `visual done`
- `Subnautica-level`

unless every relevant artifact and runtime proof is present.

## Minimum Full Acceptance Packet

A future Unity owner packet must include all of the following in one dated handoff:

1. `Docs/Screenshots/MCP/h8_[ID]_surface_coast_aegir_ui_off.png`
2. `Docs/Screenshots/MCP/h8_[ID]_surface_coast_aegir_ui_on.png` if UI is part of the proof route.
3. `Docs/Screenshots/MCP/h8_[ID]_shoreline_close_1m.png`
4. `Docs/Screenshots/MCP/h8_[ID]_underwater_0_5m.png`
5. `Docs/Screenshots/MCP/h8_[ID]_underwater_20_50m_route.png`
6. `Docs/Screenshots/MCP/h8_[ID]_aegir_long.png` or exact accepted celestial equivalent.
7. `Docs/Screenshots/MCP/h8_[ID]_regression_low_oblique.png`
8. Capture-session log after final screenshot with no unresolved exceptions/errors.
9. Route statement: GameView/main camera, scene camera, or temp proof camera. Temp proof camera cannot clear GameView acceptance by itself.
10. Runtime route statement: `00 -> 01 -> 02` or explicitly assigned direct `02`.
11. Fault closure note for any prior null, invalid forced load, shader/material error, MCP bridge failure, or screenshot route risk.
12. Compact/Middle/High/Ultra consequence note: compact keeps ocean/shore/route readability; middle adds density; high adds richer water/lighting/material; ultra adds overkill only, not new truth.

## Current Acceptance Decision

REJECT. Current evidence does not meet visual or runtime acceptance. The latest available screenshots do not prove actual underwater 0-5 m or 20-50 m route quality, and the latest available log does not prove runtime cleanliness after capture.
