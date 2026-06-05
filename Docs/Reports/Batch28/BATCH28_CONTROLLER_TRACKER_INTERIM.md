# Batch28 Controller Tracker Interim

Date: 2026-06-04 21:40 +04:00.

## Current Front

- `1474` six-view packet remains `REJECTED`.
- `h8_1908_surface_runtime_ui_on.png` is a single raw surface screenshot, not a proof packet.
- Unity owner remains the separate GUI Codex thread `Продолжить работу по логам`.
- GUI delivery of Batch27 steer is not claimed; correct-thread screenshot proof is still absent.

## Direct Controller Source Patch

Files touched by controller:
- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`
- `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs`

Patch intent:
- remove `HectonUnderwaterVisuals` service self-publication from arbitrary `OnEnable()`;
- publish `GlobalRegistry.UnderwaterVisuals` from `GameBootstrapper.ResolveSceneActivationReferences()` through `BeginSceneRuntimePublicationGate()` / `EndSceneRuntimePublicationGate()`;
- use existing cold scene root traversal, not runtime scene search or per-frame registry retry.

Status:
- `PENDING UNITY RUNTIME VERIFICATION`.

Static/compile-smoke evidence:
- `git diff --check` passed for touched source files, only CRLF warnings.
- `Docs/Logs/UnityLaunch_1909.log` shows import/reload after the patch and no inspected `error CS` / `Compilation failed`.

Non-proof:
- `UnityLaunch_1909.log` is dirty with initial refresh/domain reload/compile, MCP warning/error entries, ILPP/shader compiler activity.
- No clean play/exit log proves registry publication.
- No `1475` proof packet exists.

Important ownership note:
- `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs` already had unrelated uncommitted visual/readback/water-level edits before this controller patch. The controller-owned change in that file is only removal of `GlobalRegistry.RegisterUnderwaterVisualsRuntime(this)` from runtime `OnEnable()`.

## Batch28 Subagents

Launched as no-Unity static/report-only wave:
- `2801_OWNED_PROOF_HARNESS_SOURCE_ROUTE_AUDIT` -> Faraday / `019e93b6-2d52-7982-9f90-e922e678d368`
- `2802_FALSE_UNDERWATER_ROUTE_CAMERA_PREDICATE_AUDIT` -> Beauvoir / `019e93b6-2daa-7f12-ba56-eec6ea4999d5`
- `2803_SHORELINE_FOAM_PHOTIC_TERRAIN_STATIC_ART_ROUTE_AUDIT` -> Ptolemy / `019e93b6-2e09-7252-aafb-18254b83bb16`
- `2804_AEGIR_SKY_ROUTE_STATIC_OWNER_AUDIT` -> Volta / `019e93b6-2e5e-7a03-bb24-39d7d1131748`
- `2805_LOG_PROCESS_PROOF_GATE_TOOL_AUDIT` -> Epicurus / `019e93b6-2efb-7090-a058-e2b24a47487e`

Expected outputs:
- `Docs/Reports/Batch28/2801_OWNED_PROOF_HARNESS_SOURCE_ROUTE_AUDIT.md`
- `Docs/Reports/Batch28/2802_FALSE_UNDERWATER_ROUTE_CAMERA_PREDICATE_AUDIT.md`
- `Docs/Reports/Batch28/2803_SHORELINE_FOAM_PHOTIC_TERRAIN_STATIC_ART_ROUTE_AUDIT.md`
- `Docs/Reports/Batch28/2804_AEGIR_SKY_ROUTE_STATIC_OWNER_AUDIT.md`
- `Docs/Reports/Batch28/2805_LOG_PROCESS_PROOF_GATE_TOOL_AUDIT.md`

Status:
- all five reports completed;
- all five local subagents closed by controller;
- final synthesis created at `Docs/Reports/Batch28/BATCH28_SYNTHESIS_FOR_UNITY_OWNER.md`;
- Unity-owner steer created at `Docs/Orchestration/UNITY_OWNER_STEER_20260604_BATCH28_SYNTHESIS.md`.

## Proof Gate Tool

Implemented:
- `Tools/ProofGate/validate_proof_packet.py`
- `Tools/ProofGate/test_validate_proof_packet.py`
- `Tools/ProofGate/__init__.py`

Verification:
- `python -m unittest discover -s Tools/ProofGate -p test_*.py`
- result: `Ran 17 tests`, `OK`

Control negative run:
- `python Tools/ProofGate/validate_proof_packet.py --packet-root Docs/Screenshots/MCP --packet-id h8_1474 --session-id mcp --strict`
- result: `REJECTED_STATIC_GATE`, `RAW_PNG_SET`
- reports:
  - `Docs/Reports/Batch28/ProofPacketGate_h8_1474_mcp.json`
  - `Docs/Reports/Batch28/ProofPacketGate_h8_1474_mcp.md`

Evidence boundary:
- static packet gate only;
- not runtime proof;
- not profiler proof;
- not visual acceptance;
- not player capture verification.

## Next Required Proof

Unity-owner must verify:
- no ready-lock rejection for `HectonUnderwaterVisuals` registration;
- one active enabled underwater owner;
- `GlobalRegistry.UnderwaterVisuals` bound to that scene owner;
- no seam/native leak stack from `SeamGapDitherRenderer`;
- route-correct `1475` packet with manifest/checksums/camera/depth/quality/toggles/log path and clean post-capture log.
