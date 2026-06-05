# 2302 Underwater Proof Harness Audit

Status: STATIC VERIFIED AUDIT / UNDERWATER PROOF REJECTED
Agent: 2302
Scope: screenshot/proof harness routes and 1473 evidence. Unity was not run.

## Evidence Basis

- `Docs/Screenshots/MCP/h8_1473_underwater_0_5m.png`
- `Docs/Screenshots/MCP/h8_1473_underwater_20_50m_route.png`
- `Docs/Screenshots/MCP/h8_1473_mainrt_underwater_0_5m.png`
- `Docs/Reports/Batch22/2205_VISUAL_PROOF_PACKET_MATRIX.md`
- `Docs/Reports/Batch22/2205_RUNTIME_FAULT_MATRIX.md`
- `Docs/AgentLogs/UnityEditor_visual_audit_restart_1468.log`
- `Docs/AgentLogs/UnityEditor_visual_audit_restart_1474.log`
- `Docs/AgentLogs/UnityEditor_visual_audit_restart_1474b.log`
- Screenshot/capture source under `Assets/_Project/Scripts` and `Assets/Feel/MMTools`.

## Mandate Summary

- Static text/source scans are not runtime proof.
- Surface, shoreline, photic shallows, and 20-50 m hero routes must be bright, readable, premium, and at least Subnautica-level.
- Underwater proof must name camera, active scene, depth, underwater state owner, post stack, fog/turbidity, water renderer/Crest state, route cue, and clean post-capture log tail.
- Any screenshot written under `Assets` is rejected for proof packets because it can trigger import loops.
- Proof harness must be editor/dev-only and must not become shipping runtime overhead.

## Capture Tool Inventory

| Tool/source | Evidence | Output route | Runtime allocation risk | Verdict |
|---|---|---|---|---|
| `Assets/Feel/MMTools/Tools/MMUtilities/MMScreenshot.cs` | Static source. Default `FolderName = "Docs/Screenshots"`; legacy `Assets/Screenshots` is remapped to `Docs/Screenshots`. Uses `ScreenCapture` or manual `RenderTexture`/`Texture2D.ReadPixels`/`EncodeToPNG`. | Defaults to `Docs/Screenshots`; custom absolute/relative folder can still redirect. | Allocating/manual PNG path. Editor/debug only. | Usable only for non-shipping captures; not enough metadata. |
| `Assets/Feel/MMTools/Editor/MMUtilities/MMScreenshotEditor.cs` | Static source. Menu items write to `Docs/Screenshots`. | `Docs/Screenshots`. | Editor-only `ScreenCapture`. | Path is acceptable; metadata absent. |
| `Assets/_Project/Scripts/SaveThumbnailCaptureFeature.cs` | Static source. URP render feature uses RTHandle and `SaveThumbnailSystem.TryQueueGpuReadback`. | Save thumbnail route, not visual proof packet route. | Intended runtime feature, async GPU readback; not audited here as proof harness. | Not authority for underwater proof. |
| `Assets/_Project/Scripts/UI/SaveThumbnailCapture.cs` | Static source. UI bridge calls `SaveThumbnailSystem.CaptureThumbnail`. | Save system route. | Depends on save thumbnail system. | Not proof packet route. |
| `Assets/_Project/Scripts/Editor/HectonMcpHttpBridgeAutostart1428.cs` | Static source. Auto-starts MCP HTTP bridge. | No direct screenshot output. | Editor-only async bridge startup. | Transport helper only. |
| Dynamic MCP capture snippets | Log evidence contains `MCPDynamicCode:<Execute>g__Capture` and `H8_1458_CAPTURE ... Docs/Screenshots/MCP`. | `Docs/Screenshots/MCP`. | Unknown dynamic code; likely editor/dev allocating path. | Likely source of 1458+ named packet captures; metadata insufficient. |

## Assets/Screenshots Audit

Current filesystem: `Assets/Screenshots` exists and contains no files.

Historical risk: `UnityEditor_visual_audit_restart.log` records import of `Assets/Screenshots/screenshot-20260604-114736.png`. That proves an earlier route wrote into `Assets` and triggered Unity import. It is not current-file evidence, but it remains a hard reject condition for 1474+.

Accepted proof output root: `Docs/Screenshots/MCP` or another explicit `Docs/Reports/BatchXX/...` proof path. Rejected root: anything under `Assets`.

## 1473 Mechanism Findings

Likely mechanism:

- `Docs/AgentLogs/UnityEditor_visual_audit_restart_1468.log` contains dynamic MCP capture evidence for earlier packets: `MCPDynamicCode:<Execute>g__Capture`.
- Named packet files for 1458-1465 are logged as `H8_CAPTURE_*_DONE` or `H8_1458_CAPTURE ... Docs/Screenshots/MCP`.
- 1473 files are present in `Docs/Screenshots/MCP`, but no clean same-session metadata block was found that proves active scene, camera source, depth, underwater state, post stack, fog, or clean runtime status after final capture.

Hard static facts:

- `h8_1473_surface_coast_aegir_ui_on/off`, `h8_1473_shoreline_close_1m`, `h8_1473_underwater_0_5m`, `h8_1473_underwater_20_50m_route`, `h8_1473_regression_low_oblique`, and `h8_1473_aegir_longshot_crop_source` are 1008x567 files written 2026-06-04 17:35:18-17:35:42.
- Later `h8_1473_mainrt_*` and `h8_1473_rt_*` experiments are 1280x720 files written 2026-06-04 17:37:00-18:02:23.
- `h8_1473_mainrt_surface_underwater_renderer_off.png` and `h8_1473_mainrt_surface_underwater_renderer_on.png` have identical SHA256: `85F27BE3E421013081D7212DF14903EF9B4D3D63679D49D2E5F51351319A8361`. The underwater renderer toggle produced no pixel change for that surface frame.
- `UnityEditor_visual_audit_restart_1474.log` and `UnityEditor_visual_audit_restart_1474b.log` are only Unity launch/licensing tails. They do not prove post-1473 scene/camera/console state.

Inherited 2205 visual verdict:

- 1473 underwater files are rejected as mislabeled/weak underwater proof. The 0-5 m and 20-50 m images were reported as surface/coast-like rather than valid underwater route proof.
- Runtime cleanliness for 1473 remains unproven because no clean log tail after the capture window proves exceptions/errors stopped.

## Valid Underwater Capture Definition

A valid underwater capture must have all of these:

1. Active scene is `02_HECTON_WORLD`, unless the task explicitly assigns another scene.
2. Capture is from player main camera, cockpit camera, or GameView main route. A detached debug camera is allowed only if filename and metadata label it `debug_detached`, and it cannot satisfy final player-route proof by itself.
3. Camera depth is logged from water surface reference. For `underwater_0_5m`, camera depth must be >0 m and <=5 m. For `underwater_20_50m_route`, camera depth must be >=20 m and <=50 m.
4. Player/cockpit root position and depth are logged. Camera/player separation is logged.
5. Underwater state owner is named, and `underwater_state_active=true`.
6. Underwater renderer exists and is enabled. Crest underwater pass state is logged as active or the non-Crest route is explicitly documented.
7. RenderSettings fog enabled/color/density and water fog/turbidity state are logged.
8. Post stack/profile is enabled for final proof. A/B diagnostic captures with post off must be marked diagnostic and cannot be final underwater proof.
9. Capture shows route structure: terrain, return cue, water volume, depth cue, and player-relevant navigation or risk. Generic blue/green fog is not enough.
10. Clean log tail after final screenshot proves no unresolved exception/error, no forced invalid load, no screenshot import loop, `isCompiling=false`, and `isUpdating=false`.

## Invalid Underwater Proof Definition

Reject underwater proof if any condition is true:

- Filename says underwater but metadata does not prove depth and underwater state.
- Camera is above water, at surface, or depth is missing.
- Active scene is missing/wrong/stale.
- Detached camera hides player route truth and is not labeled debug.
- Post stack, underwater renderer, fog/turbidity, or Crest underwater pass state is missing/disabled/unknown.
- Surface/coast image is duplicated or near-duplicated under an underwater name.
- Surface water plane/slab cuts through frame as the dominant visual.
- Capture shows generic blue/green swamp cast, flat haze, no route cue, no caustic/light reason, no foam/waterline where required, or no underwater material response.
- Capture occurs during or before import/compile/update activity.
- Log tail is older than the final screenshot.
- Any screenshot artifact is written under `Assets`.
- Runtime fault appears in the same capture window and no clean rerun follows it.

## Required Complete Proof Packet

Minimum packet for 1474+:

| Artifact | Required proof |
|---|---|
| Surface | Bright/readable surface, coastline, ocean skin, Aegir/sky context, foam/specular where visible. |
| Shoreline close | 1 m class shoreline with waterline, foam, wetness/material transition, no flat strip/slab. |
| Underwater 0-5 m | Camera/player depth metadata, underwater state active, post/fog/renderer active, photic clarity, route cue. |
| Underwater 20-50 m | Depth metadata in range, route structure, turbidity/depth cue, terrain/return cue, no surface-plane clipping. |
| Aegir/celestial | Aegir/moons/sky readable and textured, not cropped source only unless marked diagnostic. |
| Low oblique regression | Low/compact readability angle showing ocean/terrain composition without broken planes. |
| Runtime log tail | Same-session log tail after final PNG, with scene, camera, state metadata and exception/error counts. |
| Metadata CSV/JSON | One row per image using `2302_PROOF_PACKET_METADATA_SCHEMA.csv` fields. |

## 1474+ Reject Conditions

Reject the packet immediately for:

- Missing 0-5 m underwater capture.
- Missing 20-50 m underwater route capture.
- Underwater false label.
- Surface duplicate or near-duplicate under underwater filename.
- No per-capture depth/state metadata.
- No foam/waterline proof for shoreline close.
- No caustic/light/fog/water-volume state for underwater proof.
- Green swamp cast, generic blue fog, flat slab plane, or no route cue.
- Wrong active scene or stale scene state.
- Detached debug camera passed as final route proof.
- Disabled post stack in final proof.
- Underwater renderer/Crest state missing or inactive.
- Runtime fault in capture window.
- Log tail older than last screenshot.
- Screenshot import loop or any file written under `Assets`.

## Existing Tool Metadata Gap

Current tools do not emit the full required metadata:

- `MMScreenshot` can redirect legacy `Assets/Screenshots` defaults to `Docs/Screenshots`, but it does not log active scene, camera identity, depth, underwater state, post profile, fog, Crest pass state, or exception deltas.
- `MMScreenshot` RenderTexture mode uses manual `new RenderTexture`, `Texture2D`, `ReadPixels`, `EncodeToPNG`, and `File.WriteAllBytes`. This is not a shipping runtime path.
- MCP dynamic capture logs show artifact paths and byte counts for some packets, but not enough state for underwater truth.
- Save thumbnail capture is not a visual proof harness.

Proposed wrapper: editor/dev-only `UnderwaterProofPacketCapture` that runs from Unity owner thread, moves the allowed capture camera only for capture, records metadata before/after each image, writes PNG plus CSV/JSON under `Docs/Screenshots/MCP`, then restores camera/scene state. It must not alter scene visuals except capture camera transform and temporary UI visibility toggles explicitly restored in `finally`.

## Rollback / No-Op Behavior

Required wrapper behavior:

- Cache camera transform, target texture, culling mask, clear flags, FOV, post-processing flags, and UI visibility before each capture.
- Restore all cached state in `finally`.
- Do not enable/disable water, Crest, fog, post, foam, caustics, silt, or scene renderers for final proof. A/B diagnostic toggles are allowed only in diagnostic captures and must be labeled.
- Do not save scene or assets.
- Do not write under `Assets`.
- On metadata failure, write no acceptance marker and mark image `REJECTED_VISUAL_PROOF`.

## Tier / Performance Consequences

- Compact: proof wrapper must not change shipping runtime. Captures may reduce resolution only if the packet says Compact/low-res and still preserves route readability.
- Middle: primary expected lane. Full packet should be captured here with final player-route camera and post stack.
- High: may add richer caustics/silt/foam/light shafts, but metadata and gameplay truth cannot differ from Middle.
- Ultra: visual overkill is allowed only as sensory richness. It cannot be the only lane where route/depth/underwater state is readable.

The proof harness is editor/dev tooling. Any PNG encoding, synchronous `ReadPixels`, extra camera render, file I/O, or metadata serialization must be outside shipping gameplay hot paths.

## Unity-Owner Checklist

Paste this into the active Unity thread:

1. Open/play the assigned route without import/build/play-mode side work from other agents.
2. Capture into `Docs/Screenshots/MCP` only.
3. For each screenshot, log metadata row before and after capture: scene, route mode, `isPlaying`, `isCompiling`, `isUpdating`, camera name/id/source, camera position/rotation, water surface Y, camera depth, player position/depth, underwater state owner/value, post profile, RenderSettings fog, underwater renderer enabled, Crest underwater pass state, foam/caustic/silt state, exception/error counts.
4. Capture required packet: surface, shoreline close, underwater 0-5 m, underwater 20-50 m, Aegir/celestial, low oblique regression, runtime log tail.
5. Do not use detached debug camera for final route proof. If used for diagnosis, label filename and metadata `debug_detached`.
6. Do not toggle post/underwater renderer/fog for final proof. A/B toggles are diagnostic only.
7. After final PNG, append clean log tail newer than the final screenshot timestamp.
8. Reject packet if any metadata field is unknown, any screenshot is under `Assets`, any runtime exception/error remains, or underwater label/depth/state disagree.

## Proof Packet Gate

1473 remains rejected. 1474+ can pass only when every underwater image has state/depth/camera/render metadata and a same-session clean log tail after the last screenshot. Labels and filenames are not evidence.

## Exact Missing Evidence

- No per-capture depth metadata for `h8_1473_underwater_0_5m.png`.
- No per-capture depth metadata for `h8_1473_underwater_20_50m_route.png`.
- No active-scene proof tied to each 1473 underwater file.
- No camera source/id proof tied to each 1473 underwater file.
- No player/cockpit position/depth proof tied to each 1473 underwater file.
- No underwater state owner/value proof tied to each 1473 underwater file.
- No post profile/fog/Crest underwater pass metadata tied to each 1473 underwater file.
- No clean same-session log tail after the last 1473 screenshot experiment at 2026-06-04 18:02:23.
- No proof that runtime exceptions were absent during 1473 final capture.

## Evidence Class

All findings in this report are `STATIC VERIFIED` from source, file metadata, hashes, and logs. No Unity runtime, Play Mode, Frame Debugger, profiler, or visual re-capture was executed by agent 2302.
