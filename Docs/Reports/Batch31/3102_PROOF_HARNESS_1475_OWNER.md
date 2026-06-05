# 3102 Proof Harness 1475 Owner

Status: `BLOCKED BY PROCESS GATE / PATCH DESIGN STATIC VERIFIED`

Evidence class: `STATIC_DOC`, `STATIC_SOURCE`, `STATIC_PROCESS_SAMPLE`.

Runtime/editor proof: `PENDING VERIFICATION`.

## Verdict

`Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs` is rejected as a proof harness base.

No implementation was performed because the process gate is blocked.

## Process Gate

Sampled active blockers:

- Unity `11620`
- dotnet `15340`
- Unity.ILPP.Runner `13512`
- UnityAutoQuitter `13852`
- UnityShaderCompiler `9532`

Per task instruction, this pass did not edit/import C# while those processes were active.

## Mandates And Authority Read

- `AGENTS.md`
- `quality.md`
- `camera.md`
- `presentation.md`
- `rendering.md`
- `water.md`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `taskslocal/batch31_night_visual_recovery/3102_PROOF_HARNESS_1475_OWNER.txt`
- `Docs/Reports/Batch30/3002_PROOF_HARNESS_REPLACEMENT_SPEC.md`
- `Docs/Reports/Batch31/PROOF_HARNESS_REPLACEMENT_SPEC_20260605.md`
- `Tools/ProofGate/README.md`
- `Tools/ProofGate/validate_proof_packet.py`
- `Tools/ProofGate/test_validate_proof_packet.py`
- `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs`

## Source Findings

`H8VisualProofCapture1912.cs` rejection remains factual:

- `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs:16` writes to `Docs/Screenshots/MCP`, not `Docs/Screenshots/HectonProofPackets/h8_1475_{session}/`.
- `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs:69-70` and `:115-116` emit raw PNG/text metadata only.
- `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs:80`, `:134`, and `:200` call `Debug.LogException`, contaminating proof log windows.
- `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs:142` exposes `QuarantineSurfaceRejectsAndExit()`.
- `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs:157-172` disables scene renderers by name-token heuristic.
- `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs:186-188` calls `EditorSceneManager.MarkSceneDirty(scene)` and `EditorSceneManager.SaveScene(scene)`.

ProofGate static contract is already present:

- `Tools/ProofGate/validate_proof_packet.py:33` defines the six exact production views.
- `Tools/ProofGate/validate_proof_packet.py:42`, `:66`, and `:81` define required manifest, derived-check, and screenshot fields.
- `Tools/ProofGate/validate_proof_packet.py:31`, `:288`, and `:409` reject binary quality labels and require `qNNN`.
- `Tools/ProofGate/validate_proof_packet.py:120` and `:529` scan forbidden dirty log tokens.
- `Tools/ProofGate/validate_proof_packet.py:450-452` rejects unknown screenshot files under strict mode.
- `Tools/ProofGate/validate_proof_packet.py:541` rejects invalid log-window offsets.
- `Tools/ProofGate/validate_proof_packet.py:633` rejects raw PNG sets when `manifest.json` is missing.

## Exact Clean-Gate Patch Design

Allowed future write scope only:

- `Assets/_Project/Scripts/Proof/Capture/H8ProofCaptureContracts.cs`
- `Assets/_Project/Scripts/Editor/Proof/H8ProofPacket1475Harness.cs`
- Optional tests only if ProofGate schema changes: `Tools/ProofGate/test_validate_proof_packet.py`

Forbidden future write scope:

- `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs`
- any `.unity`, `.prefab`, `.asset`, material, shader, or scene file
- any path under `Assets` for screenshots/logs/diagnostics

### `H8ProofCaptureContracts.cs`

Purpose: compile-safe proof DTO surface for owner-bound snapshots. Keep runtime-accessible data minimal and pure.

Required contents:

- `namespace Hecton8.Proof.Capture`
- `public enum H8ProofViewId : byte` with six canonical entries matching ProofGate order.
- `public readonly struct H8ProofViewPredicateSnapshot` with finite numeric fields only: view id, route anchor hash, route state hash, depth zone hash, visual depth meters, underwater flag, UI visible flag, route predicate flag, depth predicate flag, render scale current/target, post-stack hash, global quality weight.
- `public interface IH8ProofCaptureSnapshotProvider` with a pure `TryGetProofViewSnapshot(H8ProofViewId viewId, out H8ProofViewPredicateSnapshot snapshot)` accessor.

Rules:

- No managed strings, arrays, `GameObject`, `Transform`, `Camera`, `Material`, scene searches, allocations, publication, or state mutation in accessors.
- If owner providers are not ready, editor harness may use explicit static capture-route constants but must label those records as harness route predicates, not gameplay runtime acceptance.

### `H8ProofPacket1475Harness.cs`

Purpose: editor-only cold harness that creates a manifest-bound static proof packet.

Required boundaries:

- `#if UNITY_EDITOR`
- namespace `Hecton8.Editor.Proof`
- static menu/CLI entrypoint for `h8_1475` session capture.
- Output root exactly `Docs/Screenshots/HectonProofPackets/h8_1475_{session}/`.
- Screenshots under packet-local `screenshots/` only.
- Copied Unity log at packet root as `UnityEditor_h8_1475_{session}.log`.

Required flow:

1. Preflight process/scene guard: refuse if active scene is dirty, compiling, updating, or import worker state is active.
2. Read `Application.consoleLogPath`; record start offset and start UTC before staging the first view.
3. Open or use `02_HECTON_WORLD` only without saving. Do not call `SaveScene`, `MarkSceneDirty`, `AssetDatabase.Refresh`, or renderer-disable quarantine.
4. Stage six capture poses using a temporary camera or existing capture camera state restored in `finally`; temporary objects must use `HideFlags.DontSaveInEditor`.
5. Render each canonical filename at minimum `1280x720`; record PNG byte size, IHDR dimensions, SHA256, filesystem timestamps, pose, route/depth/UI predicates, render scale, post-stack hash, `global_quality_weight`, and `qNNN` label.
6. After final screenshot, wait at least 60 seconds while recording the post-capture clean log interval.
7. Copy the Unity log into the packet root after the wait; record end offset/end UTC and log SHA256.
8. Build `manifest.json` only after screenshots and copied log are stable.
9. Write `manifest.sha256` last.
10. Exit with non-zero code if any required predicate or file check fails.

Required helper methods:

- `BuildSessionId()`
- `ResolvePacketRoot(sessionId)`
- `TryReadQualityWeight(out float weight, out string qLabel)`
- `CaptureView(H8ProofViewId viewId, string fileName, ...)`
- `WriteManifest(...)`
- `ComputeSha256(path)`
- `ReadPngIhdr(path, out width, out height)`
- `CopyLogWindow(...)`
- `ScanLogWindowForDirtyTokens(...)`
- `RestoreCameraState(...)`

Rejected helper behavior:

- no `Debug.LogException` inside accepted capture window;
- no raw `.txt` metadata as proof substitute;
- no diagnostic PNG in `screenshots/`;
- no binary labels: `low`, `medium`, `high`, `ultra`;
- no screenshot or packet path under `Assets`;
- no runtime readiness or visual acceptance wording from static packet pass.

## Test / Validation Plan After Gate Clears

1. Re-sample process gate; require no Unity/dotnet/csc/ILPP/ShaderCompiler/import blockers.
2. Implement the new files only.
3. Run:

```powershell
python -m unittest discover -s Tools\ProofGate -p test_*.py
```

4. Generate a synthetic packet only if Unity is still unavailable; label it `STATIC_MANIFEST_TEST`, not runtime proof.
5. For real capture, run the editor harness and then:

```powershell
python Tools\ProofGate\validate_proof_packet.py --packet-root Docs\Screenshots\HectonProofPackets\h8_1475_{session} --packet-id h8_1475 --session-id {session} --expected-quality qNNN --min-post-capture-clean-seconds 60 --strict
```

6. A static pass only permits human/runtime visual review. It is not visual acceptance.

## First-20-Minutes Route Impact

Removes a proof-process blocker for validating surface coast, shoreline, shallow underwater, mid-depth route, Aegir/celestial, and low-oblique regression views. It does not improve graphics, optimization, or gameplay by itself.

## Quality Scaling Consequences

- Compact: same six views, same predicates, lower render scale allowed only if surface/shallow/mid-depth readability remains premium.
- Middle: baseline packet path; no schema or truth change.
- High: richer sensory capture allowed through render settings already owned by scene systems; proof schema unchanged.
- Ultra: extra diagnostics may be written outside `screenshots/`; no acceptance shortcut and no route truth change.

## Regression Model

- CPU: no code changed in this pass. Future harness is editor-cold; runtime accessors must stay pure.
- GC: no runtime claim. Future editor allocations are allowed; runtime DTO/provider accessors must not allocate.
- Memory/VRAM: no asset or runtime resource changed. Future proof output lives under `Docs`, avoiding Unity import.
- Cadence: no runtime cadence changed. Future harness must not poll hot gameplay systems.
- Correctness: largest risks are scene contamination, dirty proof logs, raw PNG promotion, and proof-label inflation. Current design blocks all four.

## Verification State

Verified:

- Required authority files were read.
- Current process gate is blocked.
- Existing harness rejection is source-backed.
- ProofGate schema and rejection behavior are source-backed.
- Exact patch design is documented.

Pending:

- C# implementation.
- Unity compile/import.
- Editor capture.
- Real h8_1475 proof packet.
- ProofGate strict run on real packet.
- Play Mode/player capture, profiler, GC, Frame Debugger, Memory Profiler, device proof, and human visual acceptance.
