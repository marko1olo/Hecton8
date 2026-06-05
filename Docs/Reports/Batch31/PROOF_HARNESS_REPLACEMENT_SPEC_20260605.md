# Proof Harness Replacement Spec - 2026-06-05

Status: STATIC SPEC / NO IMPLEMENTATION

Evidence class: `STATIC_SOURCE`, `STATIC_DOC`, `STATIC_FILESYSTEM`, `STATIC_PROCESS_SAMPLE`.

## Verdict

`Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs` is rejected as a proof harness base.

Do not extend it. Replace it.

## Unsafe Existing Harness

- `H8VisualProofCapture1912.cs:142` has `QuarantineSurfaceRejectsAndExit()`.
- `H8VisualProofCapture1912.cs:157` through `:172` enumerate scene renderers and set `renderer.enabled = false`.
- `H8VisualProofCapture1912.cs:186` through `:188` call `EditorSceneManager.MarkSceneDirty(scene)` and `EditorSceneManager.SaveScene(scene)`.
- `H8VisualProofCapture1912.cs:455` through `:478` use name-token reject heuristics instead of owner-bound route predicates.
- `H8VisualProofCapture1912.cs:72` through `:80`, `:126` through `:134`, and `:192` through `:200` write exception text / `Debug.LogException`, which can dirty the proof log window.

Capture-only methods are not enough:

- `CaptureSurfaceAndExit` writes raw PNG/text under `Docs/Screenshots/MCP`, no scene save.
- `CaptureWithPoseAndExit` restores camera pose in memory and does not save scene.
- Both remain ProofGate-invalid because they do not emit manifest, checksum, copied clean log, six canonical screenshots, or predicate records.

## Required Proof Packet

Packet root:

`Docs/Screenshots/HectonProofPackets/h8_1475_{session}/`

Required files:

- `manifest.json`
- `manifest.sha256`
- `UnityEditor_h8_1475_{session}.log`
- six exact PNGs under `screenshots/`:
  - `01_surface_coast_aegir_ui_off.png`
  - `02_shoreline_close_1m.png`
  - `03_underwater_0_5m.png`
  - `04_underwater_20_50m_route.png`
  - `05_aegir_celestial_long.png`
  - `06_regression_low_oblique.png`

Required manifest content:

- packet id `h8_1475`
- session id
- continuous `global_quality_weight`
- expected `qNNN`
- screenshot records with canonical filenames and route/depth/UI predicates
- derived checks required by `Tools/ProofGate/validate_proof_packet.py`
- post-capture clean log interval of at least 60 seconds

## Replacement File Scope

Use new files only:

- runtime DTO/contracts: `Assets/_Project/Scripts/Proof/Capture/`
- editor harness: `Assets/_Project/Scripts/Editor/Proof/`
- output: `Docs/Screenshots/HectonProofPackets/h8_1475_{session}/`

Do not write temporary images or logs under `Assets`.

## Hard Requirements

- Never call `SaveScene`.
- Never call `MarkSceneDirty`.
- Never disable scene renderers as proof cleanup.
- Never use filename-only or name-token-only acceptance predicates.
- Never make raw PNG folders acceptance candidates.
- Copy the Unity editor log into the packet.
- Run:

```powershell
python Tools\ProofGate\validate_proof_packet.py --packet-root Docs\Screenshots\HectonProofPackets\h8_1475_{session} --packet-id h8_1475 --session-id {session} --expected-quality qNNN --min-post-capture-clean-seconds 60 --strict
```

## Current State

- `Docs/Screenshots/HectonProofPackets` is missing.
- Existing artifacts are raw under `Docs/Screenshots/MCP`.
- Watchdog still rejects with raw/missing manifest and dirty log/process blockers.

## Worker Assignment

Assign only after Unity/build process gate is clean.

The worker must implement replacement harness, validate it on a synthetic packet if Unity is unavailable, then produce a real packet only after visual scene work is ready for proof.
