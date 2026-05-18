# Unclaimed Future System Seams

Date: 2026-05-17
Status: CONTRACT-ONLY / PENDING RUNTIME VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Purpose

This document records low-conflict seams for future systems when the current batch has no visible
agent ownership trail. It is not runtime proof and does not authorize implementation inside another
agent's dirty domain.

Use this file to prepare clean future handoffs without adding direct dependencies on code that does
not exist yet.

## Evidence Scan

Filesystem evidence checked on 2026-05-17:

- `Docs/Tasks/CURRENT_BATCH.md` contains `SHINOBU_01` through `SHINOBU_40`.
- R8 trail scan found `Status_*.md` and `Rationale_*.md` present for `SHINOBU_21`, `SHINOBU_31`, `SHINOBU_32`, `SHINOBU_33`, `SHINOBU_34`, `SHINOBU_35`, `SHINOBU_36`, `SHINOBU_39`, and `SHINOBU_40`.
- R8 trail scan found no visible `Status_*.md`, `LOG_*.md`, or `Rationale_*.md` for `SHINOBU_37` and `SHINOBU_38`.
- `LOG_*.md` files for the claimed slots above are not uniformly present in active `Docs/AgentLogs`, so this is ownership-trail evidence, not completion proof.

Interpretation:

- `SHINOBU_21`, `31`, `32`, `33`, `34`, `35`, `36`, `39`, and `40` are no longer unclaimed; treat the seams below as handoff/reservation notes only.
- `SHINOBU_37` and `SHINOBU_38` still have no current visible Status/LOG/Rationale trail.
- This is not a lock system. A missing status file only means no current on-disk agent trail was found.
- If a future agent creates a status/log/rationale file for one of these slots, that agent owns the
  domain and this document becomes a handoff reference only.

## Occupied Domains To Avoid

Do not add runtime code in these domains during opportunistic future-seam work:

- `SHINOBU_01` through `SHINOBU_36`.
- `SHINOBU_39` through `SHINOBU_40`.

Reason: Status/Rationale and/or recent LOG evidence exists, and the working tree contains active
source edits in core memory, signals, world sampling, voxel deltas, kinematics, UI, flora, scatter,
AI, submarine dynamics, cables, logistics, ecosystem, audio, thermodynamics, shaders, silt, economy,
scanner, seismic, glow, drones, terminal, synth, and origin-shift surfaces.

## Safe Seam Rules

Future-seam work may add only:

- Stable documentation contracts.
- Machine-readable reservation records that do not change runtime behavior.
- Editor-only checklists or validators that fail only when explicitly run.
- Source comments only when the source owner is inactive and the touched file is otherwise clean.

Future-seam work must not:

- Add enum values to runtime command/source APIs before the owner kernel exists.
- Register new `SignalBus<T>` lanes without a producer, consumer, overflow policy, and duplicate-name
  scan.
- Add `GlobalRegistry` slots for absent services.
- Create `NativeArray`, `NativeQueue`, or `NativeHashMap` ownership outside the eventual system owner.
- Modify save, DataVault, content, or mod schema files without updating every linked audit and
  validator in the same change.

## Seam Reservation State

| Batch slot | Role | Current seam | Allowed preparatory work | Forbidden until owner exists |
|---|---|---|---|---|
| `SHINOBU_21` | Physiology and decompression | Visible Status/Rationale trail exists; survival code exists in pieces, but no mod-safe survival command kernel is public. | Handoff-only: keep survival command boundaries, TTL, telemetry, save exclusion, and rejection payloads documented. | Direct oxygen/nitrogen mutation from mods, save truth writes, direct DataVault/native handle access. |
| `SHINOBU_31` | Compile-time and asmdef architecture | Visible Status/Rationale trail exists. | Handoff-only: record desired assembly boundaries and dependency checks. | Moving asmdefs or changing references while many agents have dirty source edits. |
| `SHINOBU_32` | Hardware scalability dictator | Visible Status/Rationale trail exists. | Handoff-only: record tier policy requirements and proof gates. | Runtime quality switching changes without device/profiler evidence. |
| `SHINOBU_33` | Telemetry and crash forensics | Visible Status/Rationale trail exists. | Handoff-only: record blackbox ownership, dump paths, and fixed-size telemetry payload needs. | Adding global telemetry containers outside the final DataVault/blackbox owner. |
| `SHINOBU_34` | Save-game Merkle tree | Visible Status/Rationale trail exists; static co-op/save docs exist. | Handoff-only: document hash version handoff and migration gates. | Changing save version, header, or hash layout without save-owner compile/runtime proof. |
| `SHINOBU_35` | Chunk residency and streaming | Visible Status/Rationale trail exists; residency code exists. | Handoff-only: document sector payload contracts and I/O proof gates. | Runtime paging changes or Addressables/world-truth rewrites. |
| `SHINOBU_36` | Input determinism and haptics | Visible Status/Rationale trail exists; haptic files exist. | Handoff-only: reserve read-only haptic command boundaries and device-tier fallbacks. | Direct device API calls from gameplay/mods or string event haptic routes. |
| `SHINOBU_37` | Physics culling and LOD | Culling/LOD files exist; no visible current owner trail. | Record service boundaries and culling overload telemetry needs. | New culling service registration or collider sleep policy changes. |
| `SHINOBU_38` | QA watchdog endurance bot | QA files exist; no visible current owner trail. | Record headless scenario contracts and output paths. | Claiming QA verification without fresh Unity/PlayMode/batch logs. |
| `SHINOBU_39` | Zero-GC localization and subtitles | Visible Status/Rationale trail exists; localization/subtitle files exist. | Handoff-only: reserve subtitle payload layouts and zero-GC text constraints. | Runtime string formatting, TMP string writes, or new public localization API. |
| `SHINOBU_40` | Master integrator and dispatcher | Visible Status/Rationale trail exists. | Handoff-only: record integration dependency notes. | Cross-domain code moves, source rewrites, or compile-wall fixes without ownership. |

## Source Reality Classification - 2026-05-17

R8 source/trail scan result: only `SHINOBU_37` and `SHINOBU_38` are unclaimed by visible agent files.
The other rows are claimed or partially claimed by Status/Rationale evidence and remain here only as
handoff/reservation context, not as greenfield permission.

| Slot | Runtime reality | Evidence | Safe future prep now | Runtime surface still missing |
|---|---|---|---|---|
| `SHINOBU_21` | Partial survival/physiology system exists. | `HectonSurvivalSystem.cs`, `SurvivalStats.cs`, `RuntimeSurvivalStats.cs`, `Gameplay/SurvivalPhysiologyScalarJob.cs`, `Physiology/PlayerStressMetricsRuntime.cs`, `Core/Contracts/HectonSurvivalContract.cs`. | Keep survival mod command as a contract-only reservation. Define oxygen-floor TTL, rejection, unload revoke, and save-exclusion policy. | Public `ModCommandOpcode.SurvivalOverride`, `ModCommandTargetSystem.PlayerSurvival`, first-party owner kernel, and 300-frame survival blackbox proof. |
| `SHINOBU_31` | Assembly architecture exists in fragments. | Many `*.asmdef` files exist across Core, World, UI, QA, AI, Physics, Graphics, Modding-adjacent domains. | Draft dependency-gate expectations only. | Generated asmdef dependency dashboard and fail-fast compile ownership. |
| `SHINOBU_32` | Hardware/scalability partial system exists. | `HardwareTierDetector.cs`, `HardwareProfileCatalog.cs`, `Core/Contracts/ScalabilityContract.cs`, `Graphics/Scalability/ThermalDynamicResolutionAdapter.cs`, `World/DynamicResolutionScaler.cs`, `Docs/ARCHITECTURE/SCALABILITY_MATRIX.md`. | Record tier handoff rules and proof matrix. | Single platform proof ledger tying Low/Middle/High/Ultra tier decisions to measured device data. |
| `SHINOBU_33` | Crash/telemetry partial system exists. | `CrashTelemetryBuffer.cs`, `RuntimeWatchdog.cs`, `FrameTimeWatchdog.cs`, `BlackBoxHeartbeatThread.cs`, `GlobalTelemetryBus.cs`, editor blackbox reader. | Reserve diagnostic payloads and dump-path contracts. | Public bounded `TelemetryMarker` command kernel and a cross-system 300-frame ownership manifest. |
| `SHINOBU_34` | Save hash/Merkle work is staged and documented. | `SaveSystem/SaveMasterHashV10.cs`, `Docs/ARCHITECTURE/COOP_MERKLE_STATE_DELTA_PROTOCOL.md`, `Docs/Modding/Net_Protocol_v1.md`. | Keep version-ledger and redacted probe requirements in docs. | Active save-version promotion, generated save hash manifest, runtime Merkle loopback, and `SaveHashProbe` redacted API. |
| `SHINOBU_35` | Chunk residency/streaming is a real runtime surface. | `World/WorldChunkResidencyManager.cs`, `WorldStreamingDirector.cs`, `WorldStreamingLayer.cs`, `World/HectonWorldStreamingTypes.cs`, `World/HectonVoxelStreamingBridge.cs`. | Define external hint boundaries only. | Owner-approved `ChunkInterestHint` command target, storage-pressure rejection policy, and mod-safe sector-hash DTO. |
| `SHINOBU_36` | Haptics/input partial system exists. | `Tools/ToolHapticsRuntime.cs`, `Tools/HapticWaveformLibrary.cs`, `Input/Determinism/DeterministicInputContracts.cs`, `Docs/Design/VR_Haptic_Waveforms_Quest.json`. | Reserve waveform-hash payloads and accessibility gates. | Public `HapticPulse` kernel routed through `ToolHapticsRuntime` without direct device API access. |
| `SHINOBU_37` | Culling/LOD partial system exists. | `Graphics/Culling/InstanceCullingService.cs`, `World/CullingManager.cs`, `World/LODSystemManager.cs`, `Rendering/Scatter/GpuScatterLodManager.cs`, `Editor/LODValidationWindow.cs`. | Record overload telemetry and registration boundaries. | Owner-approved physics/collider culling handoff and service registration policy. |
| `SHINOBU_38` | QA/headless partial system exists. | `QA/QAEnduranceWatchdogBot.cs`, `QA/Headless/HeadlessSimulationRunner.cs`, `QA/Editor/QAEnduranceBatchRunner.cs`. | Define scenario marker and output artifact contracts. | Current batch-owned endurance proof logs and optional `QaScenarioMarker` command gate for test packages. |
| `SHINOBU_39` | Localization/subtitle partial system exists. | `LocalizationManager.cs`, `LocalizationEvents.cs`, `UI/SubtitleManager.cs`, `UI/Localization/H8LocHashes.cs`, `ModdingAPI/ModLocalizationBridge.cs`. | Reserve subtitle cue payloads and zero-GC token rules. | Public `SubtitleCue` kernel, missing-token rejection telemetry, and hot-path text proof. |
| `SHINOBU_40` | Process/integration slot, not a single runtime system. | `CURRENT_BATCH.md` has no visible `SHINOBU_40` status/log/rationale trail in the current scan. | Keep dependency notes and conflict boundaries in docs. | Integrator-owned dashboard tying compile, docs, validator, and proof artifacts together. |

## Nonexistent Runtime Surface Queue

The following are useful future systems, but they are not source truth today. Do not reference them
from runtime code until the owner implements and verifies them:

- `ModCommandOpcode.SurvivalOverride` and `ModCommandTargetSystem.PlayerSurvival`.
- `ModCommandOpcode.HapticPulse` and `ModCommandTargetSystem.Haptics`.
- `ModCommandOpcode.SubtitleCue` and `ModCommandTargetSystem.Localization`.
- `ModCommandOpcode.TelemetryMarker` and `ModCommandTargetSystem.Telemetry`.
- `ModCommandOpcode.QaScenarioMarker` and `ModCommandTargetSystem.QA`.
- `ModCommandOpcode.ChunkInterestHint` and `ModCommandTargetSystem.ChunkResidency`.
- `ModCommandOpcode.SaveHashProbe` and `ModCommandTargetSystem.SaveMerkle`.
- Generated asmdef dependency dashboard with owner/status/log links.
- Cross-system blackbox ownership manifest for the fixed last-300-frame rule.
- Platform proof ledger binding Low/Middle/High/Ultra choices to measured device data.

## Implemented Code Seam - 2026-05-17

The only runtime code seeded from this pass is a contract-only layer:

- `Assets/_Project/Scripts/Global/Contracts/FutureSystemSeamContracts.cs`
- `Assets/_Project/Scripts/Global/Contracts/FutureSystemSeamPacking.cs`
- `Assets/_Project/Scripts/Global/Contracts/FutureKernelBlackboxRing.cs`
- `Assets/_Project/Scripts/Global/Contracts/FutureSystemSeamSelfAudit.cs`

These files provide unmanaged, explicitly laid out DTOs and stateless validators:

- `FutureSystemSeamRecord64` - 64-byte reservation record.
- `FutureCommandEnvelope64` - 64-byte future command envelope matching the current mod command
  packet size without adding public `ModCommandOpcode` values.
- `FutureKernelBlackboxEntry64` - 64-byte blackbox entry layout for owner-provided
  300-frame rings.
- `FutureKernelBlackboxRingState64` - 64-byte ring-state header for owner-provided
  300-frame buffers.
- `FutureSystemSeamBinaryHeader64` - 64-byte little-endian blob header for generated
  reservation artifacts.
- `FutureSystemSeamAuditReport64` - 64-byte report DTO for deterministic contract self-audits.
- `FutureSystemSeamContracts` - payload builders, validation flags, owner-slot mapping, fixed
  300-frame cursor helper, and source-absence proof bits.
- `FutureSystemSeamPacking` - allocation-free span CSV parser plus caller-buffer binary writer.
- `FutureKernelBlackboxRing` - stateless append/read helpers for caller-owned blackbox buffers.
- `FutureSystemSeamSelfAudit` - stateless builder/auditor for the seven current dormant
  reservations, binary writer probe, public-API closure check, survival envelope probe, and
  owner-provided blackbox ring probe.

The code deliberately does not allocate native memory, register services, create signal lanes,
touch `GlobalRegistry`, edit mod enums, or activate runtime behavior. It exists so future owners
can compile against the same payload/validation contract before implementing kernels.
The ring helper does not own storage; future runtime owners must provide the mandated 300-entry
buffer from their own approved memory surface, preferably DataVault.

The human-readable authoring bridge is isolated in its own non-auto-referenced assemblies:

- `Assets/_Project/Scripts/Global/FutureSeams/Authoring/Hecton8.Global.FutureSeams.Authoring.asmdef`
- `Assets/_Project/Scripts/Global/FutureSeams/Authoring/FutureSystemSeamProfile.cs`
- `Assets/_Project/Scripts/Global/FutureSeams/Editor/Hecton8.Global.FutureSeams.Editor.asmdef`
- `Assets/_Project/Scripts/Global/FutureSeams/Editor/FutureSystemSeamProfileEditor.cs`
- `Assets/_Project/Scripts/Global/FutureSeams/Editor/FutureSystemSeamStaticValidator.cs`

The ScriptableObject facade can seed the seven non-public reservations and export an `.h8bin`
reservation blob from editor code. This is an authoring/export bridge only; it is not a runtime
loader and does not create gameplay behavior.

The editor menu `Hecton8/Architecture/Validate Future System Seams` runs the same self-audit using
editor-owned scratch arrays. It validates default reservations, `.h8bin` packing, public mod API
closure, the survival override envelope, and the 300-entry blackbox ring contract without activating
any runtime service.

## Handoff Rule

When any future owner claims one of these slots, this file stops being permission and becomes a
backlog note. The owner must update this file, the owning architecture doc, and any mod reservation
before changing source enums, signal lanes, DataVault buffers, or save/content schemas.

## First Safe Reservation

The immediate safe reservation is the mod-facing future command-kernel boundary in
`Docs/Modding/Future_Command_Kernel_Reservations.md`.

That reservation deliberately does not add `ModCommandOpcode` values. It only defines what future
owners must prove before the public API expands.

## Proof Limits

- Static filesystem and source/doc scan only.
- No Unity import proof.
- No Unity Console proof.
- No Play Mode proof.
- A temporary Roslyn syntax/compile check against Unity editor assemblies passed; this is not Unity
  import proof.
- No profiler, GCMonitor, Memory Profiler, player build, platform build, save/load, or visual proof.
- Runtime microseconds saved: `0us`.
- Latest temporary Roslyn/net10 harness result: all six fixed DTOs are `64` bytes; self-audit returns
  `ok=True`, `records=7`, `mask=0x000000FE`, `flags=0x0000003F`, `bytes=512`, `blackbox=300`,
  public mod API counts `8/7`, and `reportErrors=0x00000000`.
