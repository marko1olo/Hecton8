# 3002 Proof Harness Replacement Spec

ID: `3002_PROOF_HARNESS_REPLACEMENT_SPEC`
Date: 2026-06-04
Status: `STATIC VERIFIED`
Evidence class: `STATIC_DOC` + `STATIC_SOURCE` + `STATIC_FILESYSTEM`
Runtime proof: `PENDING VERIFICATION`
Unity / Play Mode / build: NOT RUN
Write scope used: `Docs/Reports/Batch30/3002_PROOF_HARNESS_REPLACEMENT_SPEC.md`

## Boundary

No Unity Editor run, Play Mode run, build, source edit, asset edit, process kill, screenshot capture, profiler capture, scene save, or visual acceptance was performed.

This report specifies the replacement route for `H8VisualProofCapture1912.cs`. It is not evidence that any packet currently passes ProofGate.

First-20-minutes route impact: removes a proof-process blocker for surface, shoreline, shallow-water, mid-depth route, Aegir/celestial, and regression-view review. It does not improve graphics, optimization, or gameplay by itself.

## Mandates Followed

- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`

## Authority And Evidence Read

STATIC_DOC:
- `AGENTS.md`
- `quality.md`
- `camera.md`
- `presentation.md`
- `rendering.md`
- `water.md`
- `Tools/ProofGate/README.md`
- `Docs/Reports/Batch29/2902_OWNED_PROOF_HARNESS_IMPLEMENTATION_PATCH_PLAN.md`
- `Docs/Reports/Batch29/2904_PROOF_GATE_SCHEMA_AND_HARNESS_MANIFEST_CONTRACT.md`

STATIC_SOURCE:
- `Tools/ProofGate/validate_proof_packet.py`
- `Tools/ProofGate/unity_process_proof_watchdog.py`
- `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs`

STATIC_FILESYSTEM:
- `Docs/Actual Domains of Project.txt` was checked and is absent.
- `Docs/Reports/Batch30/3002_PROOF_HARNESS_REPLACEMENT_SPEC.md` did not exist before this pass.

## Current 1912 Harness Rejection

Claim: `H8VisualProofCapture1912.cs` is not acceptable as the replacement proof harness.
Evidence Class: `STATIC_SOURCE`
Artifact: `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs`
Residual risk: compile/runtime behavior was not tested.

Reasons:

- Output path is `Docs/Screenshots/MCP`, not `Docs/Screenshots/HectonProofPackets/h8_1475_{session}/`.
- Output is raw PNG plus text metadata. It does not emit `manifest.json`, `manifest.sha256`, copied clean-window Unity log, or packet-local checksums.
- It captures one surface-style image, not the six canonical production views required by ProofGate.
- Filenames do not match required ProofGate names.
- It has no manifest-bound route predicates, depth predicates, UI policy, quality weight, quality label, render scale, post-stack hash, scene field, or log window.
- It uses `Camera.main`, `FindFirstObjectByType`, and `FindObjectsByType` as editor convenience discovery. That is not a reusable owner-bound proof route.
- `QuarantineSurfaceRejectsAndExit()` disables renderers, calls `EditorSceneManager.MarkSceneDirty(scene)`, and saves the production scene with `EditorSceneManager.SaveScene(scene)`. That is disqualifying for a diagnostic proof pass.
- Error handling writes error text and calls `Debug.LogException(ex)`, which can inject dirty log tokens into the capture window.
- There is no post-final-screenshot clean log wait, no log copy after final screenshot, and no check that log mtime is newer than final screenshot.
- Text metadata is not a ProofGate substitute. ProofGate rejects raw PNG sets as `RAW_PNG_SET` when `manifest.json` is missing.

## Required Output Tree

Packet root:

```text
Docs/Screenshots/HectonProofPackets/h8_1475_{session}/
```

Required files:

```text
Docs/Screenshots/HectonProofPackets/h8_1475_{session}/
  manifest.json
  manifest.sha256
  UnityEditor_h8_1475_{session}.log
  screenshots/
    01_surface_coast_aegir_ui_off.png
    02_shoreline_close_1m.png
    03_underwater_0_5m.png
    04_underwater_20_50m_route.png
    05_aegir_celestial_long.png
    06_regression_low_oblique.png
```

Diagnostic files must not live under `Assets`. With the current strict validator, optional diagnostic PNGs should live outside `screenshots/`, for example:

```text
Docs/Screenshots/HectonProofPackets/h8_1475_{session}/diagnostics/
```

Reason: current `--strict` code scans `packet_root/screenshots/*.png` and rejects unknown files as `UNKNOWN_SCREENSHOT_FILE`. The CLI flag `--allow-diagnostic-view` exists but is not used by the validator source inspected here.

## Six Production Views

Each required view must have a manifest screenshot record with the validator-required fields, exact filename, exact `view_index`, `production_view: true`, `diagnostic_view: false`, `route_predicate_pass: true`, and `depth_predicate_pass: true`.

| Index | View ID | Filename | Required route predicate | Required camera/depth metadata |
|---:|---|---|---|---|
| 1 | `surface_coast_aegir_ui_off` | `01_surface_coast_aegir_ui_off.png` | Surface/coast/Aegir anchor is owner-bound; UI policy passes; no diagnostic substitution. | `ui_visible: false`; finite camera position/rotation/FOV; surface depth record; quality weight/label; render scale; post-stack hash. |
| 2 | `shoreline_close_1m` | `02_shoreline_close_1m.png` | Shoreline anchor id present; camera is within the route rig tolerance for a close shoreline read; route state hash present. | finite camera pose; route anchor id non-empty; shoreline distance should be recorded by the harness even though current validator only checks anchor presence. |
| 3 | `underwater_0_5m` | `03_underwater_0_5m.png` | Shallow underwater route anchor and return/readability cue are owner-bound. | `camera_visual_depth_meters` in `0.25..5.0`; `underwater_active: true`; underwater owner snapshot; depth zone id/name/hash; render and post-stack metadata. |
| 4 | `underwater_20_50m_route` | `04_underwater_20_50m_route.png` | Mid-depth route/return cue is owner-bound; depth zone contains predicate depth. | `camera_visual_depth_meters` in `20.0..50.0`; `underwater_active: true`; depth zone id/name/hash; route state hash; render and post-stack metadata. |
| 5 | `aegir_celestial_long` | `05_aegir_celestial_long.png` | Aegir/celestial long-view anchor id present and route predicate passes. | finite long-lens or owned capture pose; route anchor id non-empty; quality/render metadata; UI policy stated. |
| 6 | `regression_low_oblique` | `06_regression_low_oblique.png` | Regression oblique anchor id present; camera pose matches owned regression rig tolerance. | finite oblique camera pose; route anchor id non-empty; quality/render metadata; no diagnostic substitution. |

Surface, shoreline, Aegir, ocean surface, photic shallows, and medium-depth hero-route captures must remain bright, legible, and above the Subnautica-level floor. ProofGate cannot judge that. Human visual review remains mandatory after static packet pass.

## Manifest Contract

Top-level required fields:

- `schema_name`: `hecton8.proof_packet_gate.v1`
- `schema_version`: `1`
- `harness_name`
- `harness_version`
- `packet_id`: `h8_1475`
- `session_id`: exact `{session}`
- `created_utc`: written after final screenshot and log closure
- `created_local`
- `active_scene`: expected route scene, normally `02_HECTON_WORLD`
- `evidence_class`: honest capture label only; no profiler/player/device label
- `final_disposition`
- `may_submit_as_runtime_proof`: current validator requires this to be true if `final_disposition` is `ACCEPTED_BY_HARNESS`; this field must not be reported as `PLAYER-CAPTURE VERIFIED` or visual acceptance
- `global_quality_weight`: finite float `0.0..1.0`
- `global_quality_label`: `qNNN`; never `low`, `medium`, `high`, or `ultra`
- `route_owner_name`
- `route_session_id`
- `camera_source`
- `ui_policy`
- `log_path`
- `log_sha256`
- `log_window_start_utc`
- `log_window_end_utc`
- `log_window_start_offset`
- `log_window_end_offset`
- `post_capture_clean_seconds`: `>= 60`
- `screenshots`
- `derived_checks`

Required `derived_checks`, all true only from actual harness checks:

- `all_required_views_present`
- `all_required_views_unique`
- `all_required_views_have_sha256`
- `all_production_views_ui_policy_pass`
- `all_depth_predicates_pass`
- `all_route_predicates_pass`
- `quality_weight_is_continuous_float`
- `post_capture_log_window_clean`
- `manifest_written_after_final_screenshot`
- `log_last_write_after_final_screenshot`
- `screenshots_outside_assets_folder`
- `no_asset_import_dependency`

Per-screenshot required fields:

- `view_index`
- `view_id`
- `production_view`
- `diagnostic_view`
- `file_path`
- `file_name`
- `sha256`
- `byte_size`
- `png_width`
- `png_height`
- `capture_requested_utc`
- `file_created_utc`
- `file_last_write_utc`
- `capture_source`
- `camera_name`
- `camera_position_world`
- `camera_rotation_euler`
- `field_of_view_degrees`
- `route_anchor_id`
- `route_state_id`
- `route_state_hash`
- `route_predicate_pass`
- `route_predicate_failures`
- `camera_visual_depth_meters`
- `depth_zone_id`
- `depth_zone_name`
- `depth_zone_hash`
- `depth_predicate_pass`
- `underwater_active`
- `global_quality_weight`
- `global_quality_label`
- `render_scale_current`
- `render_scale_target`
- `post_stack_hash`
- `ui_visible`
- `log_offset_or_timestamp_at_capture`
- `packet_id`
- `session_id`

Checksums and timestamps:

- `manifest.sha256` must contain the SHA256 of final `manifest.json`.
- Every PNG `sha256` must match file bytes.
- Every PNG `byte_size` must match filesystem size.
- Every PNG `png_width` and `png_height` must match PNG IHDR and be at least `1280x720`.
- Every PNG `file_last_write_utc` must match filesystem mtime within 5 seconds.
- `created_utc` and manifest file mtime must be after the final screenshot mtime.
- Log mtime must be after final screenshot mtime.

## Clean Log Contract

The replacement harness must use an offset-first clean log window:

1. Read `Application.consoleLogPath` during preflight.
2. Record `log_window_start_offset` and `log_window_start_utc` before first view staging.
3. For each screenshot, record `log_offset_or_timestamp_at_capture`.
4. After the final screenshot, wait at least 60 seconds.
5. Record `log_window_end_offset`, `log_window_end_utc`, and `post_capture_clean_seconds`.
6. Copy the Unity log into packet root as `UnityEditor_h8_1475_{session}.log`.
7. Store `log_path` and `log_sha256` in `manifest.json`.
8. Scan the declared window before writing accepted disposition.
9. Write `manifest.json` after screenshots and copied log are stable.
10. Write `manifest.sha256` last.

Dirty tokens in the scanned log window reject the packet. The validator source currently rejects tokens under these classes:

- `DIRTY_LOG_ERROR`: `Error`, `Exception`, `LogError`, `shader error`, `material error`, `H8_PLAYMODE_EXIT`, `Access token is unavailable`, `ready lock`
- `DIRTY_LOG_WARNING`: `Warning`, `forced`
- `DIRTY_LOG_LEAK`: `Found 1 leak`, `Leak Detected`
- `DIRTY_LOG_IMPORT`: invalid assembly load, `Asset Pipeline Refresh`, `Library/PackageCache`, `AssetDatabase.Refresh`, `RefreshInfo`
- `DIRTY_LOG_COMPILE`: `CompileScripts`
- `DIRTY_LOG_DOMAIN_RELOAD`: `Domain Reload`, `ReloadAssembly`
- `DIRTY_LOG_ILPP`: `ILPP`, `PostProcessing ILPP`
- `DIRTY_LOG_MCP_TRANSPORT`: MCP WebSocket or transport startup failures

Current validator slices Python decoded text by `log_window_start_offset` and `log_window_end_offset`. A Unity C# harness must either emit Python-compatible decoded-string offsets or the validator must be patched and tested to define offsets as UTF-8 byte offsets before implementation freeze.

## Quarantine And Diagnostic Pass

Diagnostic capture may run only if it cannot save or contaminate the production scene.

Rules:

- Never call `EditorSceneManager.SaveScene(scene)` from diagnostic/quarantine capture.
- Never call `EditorSceneManager.MarkSceneDirty(scene)` as part of accepted diagnostic flow.
- Never write diagnostic output under `Assets`.
- Never use diagnostic PNGs to satisfy the six production views.
- Refuse the diagnostic pass if the target production scene is already dirty and that dirty state cannot be isolated.
- Store all renderer enabled states before mutation and restore them in `finally`.
- Prefer temporary objects/components with `HideFlags.DontSaveInEditor`.
- After diagnostic mutation, reload the scene without saving or close the temporary scene/session to discard in-memory changes.
- If a temporary copied scene is required, it must be outside production acceptance and cannot be used as production route proof unless the manifest truth label states staged diagnostic only.
- Any diagnostic render that changes scene visibility must be labeled diagnostic and kept out of `screenshots/` for current strict ProofGate compatibility.

The current `QuarantineSurfaceRejectsAndExit()` violates this route because it disables renderers and saves `02_HECTON_WORLD`.

## Reusable Harness Code Placement

If the Unity owner implements this route, acceptable future locations are:

Runtime proof contracts and DTOs:

```text
Assets/_Project/Scripts/Proof/Capture/
```

Editor-only harness:

```text
Assets/_Project/Scripts/Editor/Proof/
```

Required implementation boundaries:

- Runtime `TryGet*ProofSnapshot` accessors must be pure reads.
- Runtime DTOs must be structs with finite numeric fields, flags, and hashes only. No managed strings, arrays, `GameObject`, `Transform`, `Camera`, `Material`, or scene searches.
- Editor harness may allocate, use JSON, hash files, parse PNG IHDR, and read logs because it is cold editor tooling.
- Editor harness must not place output under `Assets`.
- Existing public interfaces should be expanded through new proof-specific interfaces, not mutated.
- `H8VisualProofCapture1912.cs` should not be extended into the reusable harness. It is a one-off raw capture/quarantine utility and has a disqualifying save path.

## ProofGate Raw Vs Manifest Validation

Current ProofGate behavior:

- A folder with PNGs and no `manifest.json` is `REJECTED_STATIC_GATE` with `RAW_PNG_SET`.
- A folder with no manifest and no PNGs is `MISSING_MANIFEST`.
- A manifest-bound packet is checked for manifest fields, manifest SHA, six exact production PNGs, PNG dimensions, PNG hashes, PNG timestamps, no `.png.meta` sibling, no `Assets` path, route/depth/UI predicates, continuous quality fields, clean log window, log SHA, and freshness.
- `PASS_STATIC_GATE` means the packet may proceed to human/runtime review. It is not `PLAYER-CAPTURE VERIFIED`, `PROFILER VERIFIED`, or visual acceptance.

Required policy:

- Raw MCP screenshots can be used only as diagnostic context.
- Raw PNG groups must never be promoted by naming convention or chat report.
- Human visual review starts only after a manifest packet passes `validate_proof_packet.py --strict`.
- ProofGate should continue rejecting raw-vs-manifest mismatches: if a PNG exists but is absent from the manifest, strict mode must reject; if manifest claims a PNG whose bytes/hash/timestamp differ, reject.
- Watchdog output must remain static status only. It may identify raw screenshot groups or blocked packets, but it cannot upgrade proof labels.

Minimum static gate command after implementation:

```text
python Tools/ProofGate/validate_proof_packet.py --packet-root Docs/Screenshots/HectonProofPackets/h8_1475_{session} --packet-id h8_1475 --session-id {session} --expected-quality qNNN --min-post-capture-clean-seconds 60 --strict
```

## Continuous Quality Capture Expectations

The following lane names are review language only. The manifest must use `global_quality_weight` as a continuous float and `global_quality_label` as `qNNN`, never binary labels.

Compact / Minimum Survival:
- Same six views, route predicates, depth predicates, DTO schema, and manifest fields.
- Lower render scale or reduced secondary effects may be recorded.
- Surface, sky, coastline, ocean surface, photic shallows, and route cues must remain bright, readable, and premium. No bloom requirement. No muddy darkness as a shortcut.

Middle:
- Baseline production packet target.
- Same truth ownership and clean log contract.
- Better material response, route dressing, silt/water readability, and stable render scale may appear as sensory detail only.

High:
- Same acceptance predicates and schema.
- Higher fidelity may show through richer reflection, foam, caustic hints, wet material response, longer HLOD residency, and stronger surface/celestial composition.
- Gameplay truth, route truth, save identity, and DTO layout do not change.

Ultra:
- Extra diagnostics or capture polish may exist only after the six production views pass.
- No new acceptance shortcut.
- No schema drift, no new gameplay truth, and no changed route authority.

## Regression Model

CPU:
- Report-only task has no runtime CPU impact.
- Future harness is editor-cold; any runtime read model must remain pure and allocation-free.

GC:
- Report-only task has no runtime GC impact.
- Future runtime accessors require static/source review and profiler proof before any zero-GC claim.

Memory/VRAM:
- Report-only task has no memory or VRAM impact.
- Future packet output under `Docs` avoids Unity import and `.meta` contamination.

Cadence:
- Report-only task has no runtime cadence impact.
- Future harness must not use gameplay hot-path polling or scene search.

Correctness:
- Main correctness risk is label inflation: static packet pass being reported as runtime or visual proof. This report forbids that.
- Secondary risk is diagnostic scene mutation. Replacement route forbids saving diagnostic changes.

## Failure Modes

- Missing manifest or raw PNG folder: `REJECTED_STATIC_GATE`.
- Strict packet with diagnostics in `screenshots/`: likely `UNKNOWN_SCREENSHOT_FILE`.
- `global_quality_label` set to `low`, `medium`, `high`, or `ultra`: `BINARY_QUALITY_LABEL`.
- Underwater depth outside bands: `DEPTH_PREDICATE_FAIL`.
- UI visible in surface UI-off view: `UI_POLICY_FAIL`.
- Copied log older than final screenshot: `STALE_LOG`.
- Dirty tokens in log window: matching `DIRTY_LOG_*` rejection.
- C# byte offsets disagree with Python decoded-string offsets: clean log slice may scan the wrong window.
- Manifest written before screenshots finish: `MANIFEST_STALE`.
- Screenshot under `Assets` or with `.png.meta`: rejected.
- Quarantine pass saves scene: process rejection even if screenshots exist.

## Final Verification State

What was verified:
- Authority docs and listed evidence files were read.
- Current validator contract was inspected.
- Current 1912 source was inspected.
- Replacement output tree, manifest schema, view list, log contract, diagnostic route, code placement, ProofGate policy, and continuous quality expectations are specified.

What remains `PENDING VERIFICATION`:
- Unity compile.
- Harness implementation.
- Editor capture.
- ProofGate execution on a real `h8_1475_{session}` packet.
- Play Mode/player capture truth.
- Profiler, GC, Frame Debugger, Memory Profiler, device, and human visual acceptance.

Final classification: this is a static replacement specification. No runtime proof was produced or claimed.
