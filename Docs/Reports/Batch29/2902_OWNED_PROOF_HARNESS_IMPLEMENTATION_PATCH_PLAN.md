# 2902 Owned Proof Harness Implementation Patch Plan

Agent: 2902_OWNED_PROOF_HARNESS_IMPLEMENTATION_PATCH_PLAN  
Date: 2026-06-04  
Evidence labels: STATIC_DOC + STATIC_SOURCE + STATIC_FILESYSTEM  
Runtime proof: PENDING_VERIFICATION  
Unity / Play Mode / build: NOT RUN  
Write scope used: `Docs/Reports/Batch29/2902_OWNED_PROOF_HARNESS_IMPLEMENTATION_PATCH_PLAN.md`

## Scope

Produce an implementation-grade static patch plan for the first-party HECTON proof harness that can create `h8_1475` packets acceptable to `Tools/ProofGate/validate_proof_packet.py`.

No `Assets/**`, source, Unity, Play Mode, build, process kill, or runtime capture action was performed.

## Authority And Evidence Read

STATIC_DOC:
- `AGENTS.md`
- `quality.md`
- `testing.md`
- `camera.md`
- `performance.md`
- `.agents-skills/ARCH_Execution_Phases.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `Docs/Reports/Batch28/2801_OWNED_PROOF_HARNESS_SOURCE_ROUTE_AUDIT.md`
- `Docs/Reports/Batch28/2802_FALSE_UNDERWATER_ROUTE_CAMERA_PREDICATE_AUDIT.md`
- `Docs/Reports/Batch28/2805_LOG_PROCESS_PROOF_GATE_TOOL_AUDIT.md`

STATIC_SOURCE:
- `Tools/ProofGate/validate_proof_packet.py`
- `Tools/ProofGate/test_validate_proof_packet.py`
- `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs`
- `Assets/_Project/Scripts/World/DepthZoneDirector.cs`
- `Assets/_Project/Scripts/Core/HomeostasisBrain.ScalabilityDictator.cs`
- `Assets/_Project/Scripts/World/DynamicResolutionScaler.cs`

STATIC_FILESYSTEM:
- `Docs/Reports/Batch29/2902_OWNED_PROOF_HARNESS_IMPLEMENTATION_PATCH_PLAN.md` did not exist before this report.

`Docs/Actual Domains of Project.txt` produced no substantive domain content in the current read. Narrow domain used: proof harness, camera/capture, route/depth predicates, log-window validation, quality/performance proof.

## Static Findings

Claim: `Tools/ProofGate/validate_proof_packet.py` now exists and is the hard schema target.  
Evidence Class: STATIC_SOURCE  
Artifact: `Tools/ProofGate/validate_proof_packet.py`  
Residual risk: Tool was not executed in this pass.

Claim: The validator requires `manifest.json`, `manifest.sha256`, six production PNGs, matching PNG IHDR dimensions, SHA256, timestamps, clean log binding, non-binary `global_quality_label`, continuous `global_quality_weight`, route/depth predicates, and no `Assets` contamination.  
Evidence Class: STATIC_SOURCE  
Artifact: `Tools/ProofGate/validate_proof_packet.py`, `Tools/ProofGate/test_validate_proof_packet.py`  
Residual risk: Runtime harness field naming must match the validator exactly or packets reject.

Claim: `HectonUnderwaterVisuals` exposes live `CurrentDepth` and `IsUnderwater`, but no immutable public proof snapshot.  
Evidence Class: STATIC_SOURCE  
Artifact: `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs`  
Residual risk: Implementing proof accessors in this large file may hit compile dependencies and stale private-state assumptions.

Claim: `DepthZoneDirector` implements `IDepthZoneReadModel`, but public proof surface is only `CurrentZone`; exact current depth, zone bounds, contains-depth result, and source frame are missing.  
Evidence Class: STATIC_SOURCE  
Artifact: `Assets/_Project/Scripts/World/DepthZoneDirector.cs`  
Residual risk: Existing `SlowTick()` reads `survivalSystem.Depth`; camera-depth proof will need an explicit source or route harness input.

Claim: continuous quality and dynamic-resolution snapshots already have viable static sources.  
Evidence Class: STATIC_SOURCE  
Artifact: `HomeostasisBrain.TryGetHardwareDictatorSnapshot(...)`, `DynamicResolutionScaler.TryGetSnapshot(...)`  
Residual risk: Harness still needs runtime owner availability and stale-snapshot rejection.

## Proposed File List

Runtime contracts and DTOs:
- `Assets/_Project/Scripts/Proof/Capture/HectonProofCaptureContracts.cs`
- `Assets/_Project/Scripts/Proof/Capture/HectonProofRouteCaptureRig.cs`
- `Assets/_Project/Scripts/Proof/Capture/HectonProofRouteViewSpec.cs`
- `Assets/_Project/Scripts/Proof/Capture/HectonProofReadModels.cs`

Owner patches required:
- `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs`
- `Assets/_Project/Scripts/World/DepthZoneDirector.cs`

Editor harness:
- `Assets/_Project/Scripts/Editor/Proof/HectonOwnedProofCaptureWindow.cs`
- `Assets/_Project/Scripts/Editor/Proof/HectonOwnedProofCaptureHarness.cs`
- `Assets/_Project/Scripts/Editor/Proof/HectonOwnedProofManifestWriter.cs`
- `Assets/_Project/Scripts/Editor/Proof/HectonProofPngProbe.cs`
- `Assets/_Project/Scripts/Editor/Proof/HectonProofLogWindowValidator.cs`
- `Assets/_Project/Scripts/Editor/Proof/HectonProofRoutePredicateEvaluator.cs`
- `Assets/_Project/Scripts/Editor/Proof/HectonProofPacketHasher.cs`

Optional editor tests after implementation:
- `Assets/_Project/Scripts/Editor/Proof/Tests/HectonOwnedProofManifestWriterTests.cs`
- `Assets/_Project/Scripts/Editor/Proof/Tests/HectonProofRoutePredicateEvaluatorTests.cs`

Do not put proof output under `Assets`. Packet root must be:

`Docs/Screenshots/HectonProofPackets/h8_1475_{session_id}/`

## Runtime DTO And Interface Definitions

All proof DTOs must be value types. No `string`, `GameObject`, `Transform`, `Camera`, `Material`, managed arrays, `List<T>`, `Dictionary<K,V>`, or Unity object references in runtime DTOs. Store IDs as fixed hashes, ints, flags, finite floats, and small fixed structs. Editor code may translate hashes to manifest strings in cold file-writing code.

Suggested namespace: `Hecton8.Proof.Capture`.

`HectonProofVector3`
- `float X`
- `float Y`
- `float Z`

`HectonProofPoseSnapshot`
- `HectonProofVector3 PositionWorld`
- `HectonProofVector3 EulerWorld`
- `HectonProofVector3 ForwardWorld`
- `float FieldOfViewDegrees`
- `float NearClip`
- `float FarClip`
- `uint CameraNameHash`
- `uint CameraOwnerHash`
- `uint SourceFrame`
- `uint Flags`

`HectonUnderwaterProofSnapshot`
- `uint OwnerHash`
- `int OwnerInstanceId`
- `uint SnapshotSequence`
- `uint SourceFrame`
- `float WaterLevelY`
- `float CameraDepthMeters`
- `float PlayerDepthMeters`
- `float ResolvedVisualDepthMeters`
- `float SignedDepthFromSurface`
- `float Turbidity`
- `float FogDensity`
- `float CausticsStrength`
- `float MotesVisibility`
- `float MotesEmissionRate`
- `uint WaterLevelSourceHash`
- `uint CaptureCameraHash`
- `uint MaterialToggleHash`
- `uint PostStackToggleHash`
- `uint FogStateHash`
- `uint AmbientStateHash`
- `uint RejectionFlags`
- `byte UnderwaterActive`
- `byte UnderwaterPassActive`
- `byte CausticsEnabled`
- `byte MotesEnabled`

`HectonDepthZoneProofSnapshot`
- `uint OwnerHash`
- `int OwnerInstanceId`
- `uint SnapshotSequence`
- `uint SourceFrame`
- `float CurrentDepthMeters`
- `float ZoneMinDepth`
- `float ZoneMaxDepth`
- `uint ZoneIdHash`
- `uint ZoneDisplayNameHash`
- `uint ZoneHash`
- `uint DepthSourceHash`
- `uint RouteSegmentHash`
- `uint RejectionFlags`
- `byte ContainsDepth`
- `byte IsStale`
- `byte PendingTransition`
- `byte Reserved`

`HectonRouteProofSnapshot`
- `uint PacketIdHash`
- `uint SessionHash`
- `uint ViewIdHash`
- `uint RouteAnchorHash`
- `uint RouteStateHash`
- `uint RouteOwnerHash`
- `uint RouteCueHash`
- `float ExpectedDepthMin`
- `float ExpectedDepthMax`
- `float ActualDepthMeters`
- `float ShorelineDistanceMeters`
- `float CameraAnchorPositionToleranceMeters`
- `float CameraAnchorAngleToleranceDegrees`
- `uint PredicateFailureFlags`
- `byte ProductionView`
- `byte DiagnosticView`
- `byte UiVisible`
- `byte RoutePredicatePass`

Runtime interfaces:
- `public interface IUnderwaterVisualProofReadModel { bool TryGetUnderwaterProofSnapshot(out HectonUnderwaterProofSnapshot snapshot); }`
- `public interface IDepthZoneProofReadModel { bool TryGetDepthZoneProofSnapshot(float predicateDepthMeters, uint routeSegmentHash, out HectonDepthZoneProofSnapshot snapshot); }`
- `public interface IHectonRouteCaptureProofReadModel { bool TryGetRouteCaptureSnapshot(uint viewIdHash, out HectonRouteProofSnapshot snapshot); }`

Purity rules:
- `TryGet*` accessors must be pure read accessors.
- They must not allocate, publish signals, touch `GlobalRegistry`, search scene, sync camera state, complete jobs, mutate global state, call `AssetDatabase`, read/write files, or read back GPU state.
- Owners publish or cache truth in their normal owner phase. Harness only reads snapshots after owner state is stable.
- Editor harness may use managed strings, JSON, SHA256, file IO, PNG parsing, and log reads because that path is cold/editor-only.

## Owner Patch Requirements

`HectonUnderwaterVisuals`:
- Implement `IUnderwaterVisualProofReadModel`.
- Build the proof snapshot from already-owned fields and the same depth/underwater-pass logic used for presentation.
- Snapshot must include camera depth, player/proxy depth when available, water level source, underwater active, underwater pass active, fog/turbidity/ambient hashes, caustics state, motes/marine snow state, material/post-stack hashes, owner frame/sequence, and rejection flags for unresolved camera, disabled owner, missing material, stale owner, or invalid finite values.
- Do not expose private debug fields as proof. Debug fields are STATIC_SOURCE only, not acceptance data.

`DepthZoneDirector`:
- Implement `IDepthZoneProofReadModel`.
- Cache last evaluated survival depth, current zone, source frame/sequence, and stale age in existing owner flow.
- `TryGetDepthZoneProofSnapshot(predicateDepthMeters, routeSegmentHash, out snapshot)` must evaluate zone min/max/contains for the predicate depth and state whether it matches current owner depth.
- The accessor must not force a `SlowTick()` recompute or search zones in a hot loop unless it uses bounded existing arrays and is explicitly cold/editor proof only. Preferred route: cache the last owner result and validate the supplied predicate depth against current zone bounds.

`HectonProofRouteCaptureRig`:
- Own all `h8_1475` view ids, route anchors, production/diagnostic roles, expected depth bands, UI policy, route cue id, and camera anchor tolerances.
- Must reject unnamed temp cameras for production views.
- May allow proof-rig camera only if manifest truth label states staged/editor harness and route anchor tolerance passes.

## Editor Harness Sequence

1. User opens `HectonOwnedProofCaptureWindow` and selects packet id `h8_1475`.
2. Harness creates session id: `sYYYYMMDD_HHmmss` or deterministic user-supplied id.
3. Output root:
   `Docs/Screenshots/HectonProofPackets/h8_1475_{session_id}/`
4. Preflight resolves owners once through cold `GlobalRegistry` access:
   - `IUnderwaterVisualProofReadModel`
   - `IDepthZoneProofReadModel`
   - `IHectonRouteCaptureProofReadModel`
   - dynamic resolution runtime
   - hardware dictator snapshot route
5. Preflight rejects if required owners are missing, disabled, duplicate, stale, or not in accepted scene.
6. Harness records initial `Application.consoleLogPath`, baseline log byte offset, and `log_window_start_utc`.
7. For each required view:
   - stage route rig view id;
   - place or select the owned capture camera;
   - wait until the camera transform and owner snapshots agree;
   - evaluate route/depth/UI/quality/render-scale predicates before capture;
   - capture PNG to `screenshots/{required_filename}`;
   - wait for file existence, stable file size, valid PNG magic/IHDR, finite dimensions, and SHA256.
8. Capture optional diagnostic overlay only as `07_proof_debug_overlay_route_state.png`; it cannot substitute for a production view.
9. Wait at least 60 seconds after final screenshot.
10. Copy `Application.consoleLogPath` into packet as `UnityEditor_h8_1475_{session_id}.log`.
11. Record `log_window_end_utc`, `log_window_start_offset`, `log_window_end_offset`, `post_capture_clean_seconds`, and `log_sha256`.
12. Validate clean log window using the same forbidden token profile as `validate_proof_packet.py`.
13. Write `manifest.json` only after screenshots and copied log are stable.
14. Write `manifest.sha256` from the final manifest bytes.
15. Run the static packet gate as a manual follow-up command after implementation:
   `python Tools/ProofGate/validate_proof_packet.py --packet-root Docs/Screenshots/HectonProofPackets/h8_1475_{session_id} --packet-id h8_1475 --session-id {session_id} --expected-quality qNNN --strict`

No claim above `EDITOR_VERIFIED` is allowed from this harness. Human visual review and runtime/player proof remain separate gates.

## Screenshot View Spec Table

| Index | View ID | Filename | Role | Required predicates |
|---:|---|---|---|---|
| 1 | `surface_coast_aegir_ui_off` | `01_surface_coast_aegir_ui_off.png` | Production | UI hidden, surface/coast/Aegir route anchor, route predicate pass, finite camera pose, quality/render snapshot present |
| 2 | `shoreline_close_1m` | `02_shoreline_close_1m.png` | Production | shoreline anchor id present, shoreline distance field present, camera within anchor tolerance, route predicate pass |
| 3 | `underwater_0_5m` | `03_underwater_0_5m.png` | Production | camera visual depth `0.25..5.0m`, underwater active true, underwater pass active, water owner snapshot valid, route predicate pass |
| 4 | `underwater_20_50m_route` | `04_underwater_20_50m_route.png` | Production | camera visual depth `20..50m`, underwater active true, depth zone snapshot contains depth, route/return cue present |
| 5 | `aegir_celestial_long` | `05_aegir_celestial_long.png` | Production | celestial route anchor id, UI policy pass, camera pose bound, quality/render snapshot present |
| 6 | `regression_low_oblique` | `06_regression_low_oblique.png` | Production | oblique route anchor id, route predicate pass, stable camera pose, no diagnostic substitution |
| 7 | `proof_debug_overlay_route_state` | `07_proof_debug_overlay_route_state.png` | Diagnostic only | Optional; may explain state but cannot satisfy any production view |

Visual review requirements after packet creation:
- Surface, coastline, Aegir, moons, ocean surface, and photic shallows must remain bright, legible, premium, and not hidden by darkness.
- Underwater images must show believable water volume, route context, terrain/return/risk cues, depth falloff, and premium material state.
- Static packet pass does not judge visual taste; human visual gate remains mandatory.

## Manifest Field Mapping To Validator Requirements

The harness manifest must use the field names expected by the current validator.

Top-level fields:
- `schema_name`: `hecton8.proof_packet_gate.v1`
- `schema_version`: integer, start with `1`
- `harness_name`: e.g. `HectonOwnedProofCaptureHarness`
- `harness_version`: semantic or integer version string
- `packet_id`: `h8_1475`
- `session_id`: exact session id used in packet folder and log
- `created_utc`: manifest write time after final screenshot and log closure
- `created_local`: local timestamp for operator trace
- `active_scene`: active scene name/path
- `evidence_class`: harness evidence label; use `EDITOR_VERIFIED` only after actual Editor capture, otherwise never
- `final_disposition`: `ACCEPTED_BY_HARNESS` only when every derived check is true
- `may_submit_as_runtime_proof`: true only for accepted harness packet; validator still outputs static gate only
- `global_quality_weight`: finite float `0.0..1.0`
- `global_quality_label`: `qNNN`, derived from weight; never `low`, `medium`, `high`, or `ultra`
- `route_owner_name`: route rig owner name
- `route_session_id`: same session id or route rig session id
- `camera_source`: e.g. `owned_harness`
- `ui_policy`: e.g. `ui_off` or per-view policy summary
- `log_path`: copied log path in packet
- `log_sha256`: SHA256 of copied log
- `log_window_start_utc`, `log_window_end_utc`
- `log_window_start_offset`, `log_window_end_offset`
- `post_capture_clean_seconds`: `>= 60`
- `screenshots`: six production records plus optional diagnostic record
- `derived_checks`: object containing every validator-required boolean set from actual harness checks

Required `derived_checks`:
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

Per screenshot fields:
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

Important mismatch from earlier Batch28 drafts: current validator expects `schema_name`, `byte_size`, `png_width`, `png_height`, and `global_quality_label`. A manifest using only draft names like `schema`, `byte_length`, `width`, or `height` will reject.

## Log Offset And Window Handling Plan

Evidence target: clean post-capture log window. Static source is not runtime proof.

Implementation:
- Read `Application.consoleLogPath` during preflight.
- Store `baselineOffsetBytes = currentLogFile.Length`.
- Store `log_window_start_utc` immediately before first view staging.
- For each screenshot, record `log_offset_or_timestamp_at_capture` as current byte offset or UTC timestamp.
- After final screenshot, wait at least 60 seconds.
- Store `log_window_end_utc` and `log_window_end_offset`.
- Copy the full editor log to packet root as `UnityEditor_h8_1475_{session_id}.log`.
- Store `log_path`, `log_sha256`, offset window, and duration in manifest.
- Scan only `[log_window_start_offset, log_window_end_offset)` when offsets are valid.
- Reject on any current validator dirty token in the window:
  `Error`, `Exception`, `Warning`, `LogError`, leak markers, shader/material errors, compile/import/domain reload/ILPP markers, MCP transport errors, `AssetDatabase.Refresh`, `RefreshInfo`, and package-cache import noise.
- Reject if the copied log mtime is older than final screenshot.
- Reject if no offset window exists. Strict fallback full-log scan is allowed by the validator but should be treated as degraded and likely noisy.

Process hygiene:
- Harness must not clear historical logs as proof.
- Harness must not kill MCP or Unity processes.
- If MCP bridge warnings appear during the window, packet is dirty. Do not hide them.

## Compile Dependency Risk

STATIC_SOURCE risks:
- Adding runtime proof interfaces under `Proof/Capture` can create assembly visibility failures if existing asmdefs do not reference the new namespace.
- Patching `HectonUnderwaterVisuals` is high-risk because the file is large, uses `ExecuteAlways`, private runtime/editor branches, and many material/post-stack fields.
- Adding interfaces directly to `Hecton8.Core.Contracts` can trigger cross-assembly contract drift. Prefer a narrow runtime proof contract visible to owners and editor harness.
- Existing `IDepthZoneReadModel` should not be mutated if other agents depend on it. Add `IDepthZoneProofReadModel` instead.
- Runtime DTOs with managed strings or Unity references will violate zero-GC/native-boundary rules and may break Burst/static layout checks.
- Editor harness must be inside an editor-only path or asmdef. Runtime assemblies must not reference `UnityEditor`.
- File output under `Docs` avoids Unity import loops; any output under `Assets` can trigger `.meta` contamination and validator rejection.
- `ScreenCapture.CaptureScreenshot` can be asynchronous; manifest must wait for stable files before hashing.
- If current Unity Console already has compiler errors, implementation cannot claim compile success until a separate Unity/compile proof pass clears them.

## Proof Requirements After Implementation

Implementation completion is not acceptance. Required follow-up evidence:

- STATIC_SOURCE: source review confirms DTO/interface purity, validator field names, no source output under `Assets`, and no hot `GlobalRegistry` polling.
- EDITOR_VERIFIED: Unity Editor opens, scripts compile, harness window opens, preflight owner resolution reports valid owners.
- EDITOR_VERIFIED: harness creates `Docs/Screenshots/HectonProofPackets/h8_1475_{session_id}/` with manifest, manifest SHA, copied log, six production PNGs, and optional diagnostic PNG.
- STATIC_FILESYSTEM: `python Tools/ProofGate/validate_proof_packet.py ... --strict` returns `PASS_STATIC_GATE`.
- PLAYER-CAPTURE VERIFIED or PLAYER_BUILD_VERIFIED: only after actual gameplay/player capture path exists and is reviewed. The static validator cannot grant this.
- PROFILER_VERIFIED: required before any zero-GC, frame-time, memory, or runtime-cost claim.
- PLAYER-CAPTURE VERIFIED + human visual gate: required before claiming the screenshots prove surface/underwater visual quality.

Minimum command after implementation:

```text
python Tools/ProofGate/validate_proof_packet.py --packet-root Docs/Screenshots/HectonProofPackets/h8_1475_{session_id} --packet-id h8_1475 --session-id {session_id} --expected-quality qNNN --min-post-capture-clean-seconds 60 --strict
```

## No-Runtime-Allocation Constraints

Runtime side:
- Runtime DTOs are structs with finite numeric fields and hashes only.
- `TryGet*ProofSnapshot` methods are pure and allocation-free.
- No LINQ, reflection, managed `new`, string formatting, `ToString()`, coroutines, scene search, `GetComponent()`, or `GlobalRegistry.Get<T>()` in repeated proof/read paths.
- No persistent native containers added for the proof harness.
- No GPU readback, material clone, texture clone, or renderer material mutation in proof snapshot accessors.
- Route rig view staging may be editor/cold only. It must not become a gameplay scheduler.

Editor side:
- Managed allocation, JSON, SHA256, PNG IHDR parsing, strings, and file IO are allowed because the harness is cold editor proof generation.
- Editor harness still must avoid `Assets` output and import-triggering proof files.
- Editor harness must not claim zero-GC without profiler/GCMonitor artifact.

## Scalability Consequences

Weak device / Minimum Survival:
- Same route ids, DTO schema, manifest fields, and predicates.
- Lower render scale can be recorded, but screenshots still need readable route, premium water/sky/surface identity, and complete owner snapshots.

Middle:
- Baseline six production views with exact same truth fields.
- No binary quality behavior; `global_quality_weight` remains a float and label remains `qNNN`.

High:
- Same truth ownership and manifest schema.
- Higher fidelity may appear through render scale, underwater material/post-stack hashes, caustics, motes, and route dressing, all recorded as sensory detail only.

Ultra:
- Extra diagnostic captures are allowed only after the six production views pass.
- Ultra cannot alter gameplay truth, DTO layout, route authority, save identity, or validator acceptance predicates.

## Strongest Blockers

1. No first-party owned proof harness currently exists in the required runtime/editor paths.
2. `HectonUnderwaterVisuals` lacks immutable public proof snapshots for exact camera depth, underwater active state, pass state, material/post-stack/fog/caustics/motes truth.
3. `DepthZoneDirector` exposes only `CurrentZone`; exact predicate-grade depth, min/max/contains, source frame, and stale flags are missing.
4. Current validator field names are strict; older draft manifest names will reject.
5. Clean post-capture log window is mandatory and likely fragile because editor/MCP/import warnings are hard rejects.
6. No runtime, compile, Unity, screenshot, profiler, or visual proof was produced by this planning task.

## Final Classification

This report is STATIC_DOC + STATIC_SOURCE + STATIC_FILESYSTEM only. It is a patch plan, not proof that `h8_1475` can currently pass. Runtime implementation, Unity compile, harness execution, static packet-gate execution, and human visual review remain PENDING_VERIFICATION.
