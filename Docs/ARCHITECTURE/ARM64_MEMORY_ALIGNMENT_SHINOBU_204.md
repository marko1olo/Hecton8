# ARM64 Memory Alignment - SHINOBU_204

Status: partial enforcement pass. Last recorded static scan text reported zero `StructLayout(...Pack=...)` parameters under `Assets/_Project/Scripts`; rerun and link an artifact path, command/tool, timestamp, environment, and output before treating that count as current proof. Core DTO explicit-layout coverage was expanded for Bridge, input, black-box, foveated telemetry, simulation-bucketing records, weather current metadata, queue payloads, content authority Vault records, global telemetry events, dispatcher telemetry/state records, Core runtime snapshots for brine/power/UI/player context, acoustic AUP, object-batch render payloads, spatial intrinsics, native bitmask, logistics spline descriptor, native-memory snapshot source, arena allocation metadata, input dispatch records, scheduling black-box records, GlobalSignals transform records, GlobalTelemetryBus black-box jobs/editor frame, and every `StructLayout` record inside `GlobalRegistryContracts.cs`; full non-Pack Sequential layout conversion remains open outside the owner-safe Core set.

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-21 R51 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, shader import, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
Current DOC_GLOBAL boundary (2026-05-21 R51): `Docs/Reports/2026-05-21_DOCUMENTATION_R51_ROOT_ARCHITECTURE_ENCODING_BOUNDARY_READORDER_AND_ROUTE_GAPS_LOCAL.md` is the latest local static root/architecture encoding repair, boundary-gap, read-order, route-card/static-contract, and source/AtlasCheck orientation correction. R50 remains the prior generated-atlas regeneration, stale R48 interior-boundary, dump-target wording, and source-counter drift correction. R49 remains the prior AtlasCheck-red-state/boundary-gap/route-field/source-counter correction. R48 remains the prior date-rollover/AtlasCheck/source-counter correction. R47 remains the prior authority-spine/runtime-wording/counter-drift correction. R46 remains the prior interior-authority/route-field/proof-language correction. R45/R44/R43/R42/R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers. Current AtlasCheck remains red until `Tools/AtlasCheck.py` exits `0`; runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->


## Rules

- Runtime DTOs intended for NativeArray, Vault, Burst, SignalBus, GPU structured upload, or telemetry must not use Pack=1.
- Refactored DTOs use LayoutKind.Explicit with exact sizes of 16, 32, 64, 128, or larger exact 64-byte multiples when the payload contains dual AUP data or fixed analytic fallback spectra that cannot fit in 128 bytes.
- 8-byte primitives and AUP-style fields must sit on offsets divisible by 8.
- Padding must be explicit fields so default initialization zeroes every ABI byte.
- Unity job structs containing NativeArray or NativeQueue handles may stay Sequential, but Pack=1 is forbidden and NativeArray fields should use NoAlias where ownership proves non-overlap.

## Added Enforcement

- Editor window: Hecton8/Diagnostics/Memory Alignment X-Ray.
- Strict CLI entry: Hecton8.Editor.Arm64MemoryAlignmentXRayWindow.RunArm64MemoryAlignmentCli.
- Mock proof entry: Hecton8.Editor.Arm64MemoryAlignmentXRayWindow.GenerateMockLayoutStressTest.
- Source fixer/report entry: Hecton8.Editor.Arm64LayoutSourceFixer.RunCli. It parses Core/Physics with Roslyn AST, prints Roslyn assembly provenance, mechanically removes explicit `Pack` arguments with per-attribute rewrite bookkeeping, and hard-fails Sequential DTO candidates or parser binding failures as `[BLOCKED] AST` / `[BLOCKED] AST_BINDING` until owner layout proof exists. Latest strict target-root scans report 0 Sequential attributes and 0 Pack attributes under Core/Physics.
- Signal payload cold fence: SignalBus<T>.EnsureInitialized rejects invalid 16/32/64/128/192-byte strides using UnsafeUtility.SizeOf<T>() and no reflection.
- Report path: Docs/Reports/ARM64_ALIGNMENT_XRAY_REPORT.txt.
- Source fixer report path: Docs/Reports/ARM64_LAYOUT_SOURCE_FIXER_REPORT.txt.
- Continuous traversal gate: `ModuloSimulationBucketer` consumes `GlobalQualityWeight` and deterministically dithers slow-bucket group activation between 1/2/4 power-of-two groups. DTO layout stays invariant; traversal bandwidth scales by average active buckets.
- Queue payload quantization: targeted NativeQueue payloads in AudioLog, AtlasSignal, Bootstrap, GameBootstrapper, BiomeMatrix, Crafting, Weather, Localization, Narrative, Scan, ModuleStatus, Inventory, Core Registry, and Core command queue now use Explicit 16/32/64/128-byte strides.
- Weather ABI quantization: `CurrentMeta` is Explicit Size=32, `WeatherEventPayload` is Explicit Size=128, and `WeatherRuntimeSnapshot` is Explicit Size=192 with Gerstner waves aligned to cache-line boundaries at offsets 64/96/128.
- Core content/dispatcher quantization: `ContentBundleRefState` is Explicit Size=32, `ContentAuthorityTelemetryEntry` is Explicit Size=64, `ContentPendingLoadState` is Explicit Size=16, `ContentVisualFeatureBudget` is Explicit Size=16, `ContentLoreBlockIndex` is Explicit Size=16, `TelemetryEvent` is Explicit Size=64, `DispatcherStateDTO` is Explicit Size=32, `DispatcherPipelineTelemetryEntry` is Explicit Size=32, and `MockTimeDilationSignal` is Explicit Size=16.
- Core runtime snapshot quantization: `BrineLayerSample` is Explicit Size=32, `BatteryRuntimeSnapshot` is Explicit Size=16, `UIStateData` is Explicit Size=32, `UIValueSlot` is Explicit Size=32, `PlayerMovementRuntimeState` is Explicit Size=128, `PlayerLookState` is Explicit Size=32, `PlayerSurvivalRuntimeState` is Explicit Size=128, and `PlayerInteractionRuntimeState` is Explicit Size=32.
- Core render/audio explicit exceptions: `AcousticAup` is Explicit Size=40, `ObjectBatchInstance` is Explicit Size=80, and `ObjectBatchChunk` is Explicit Size=40. These preserve existing audio/render authoring ABI and are not yet cache-line resized.
- Core intrinsic/arena quantization: `HectonAabb` is Explicit Size=32, `HectonSphere` is Explicit Size=16, `NativeBitmask256` is Explicit Size=32, `SplineDescriptor` is Explicit Size=64, `NativeAllocationSnapshotSource` is Explicit Size=32, and `ArenaAllocation` is Explicit Size=32. `NativeArenaSlice<T>` remains Sequential Size=32 as a generic-layout TypeLoad exception, with pointer-first ordered fields and named tail padding.
- Core input/registry quantization: `BufferedActionEntry` is Explicit Size=16, `JobAdmissionBlackboxEntry` is Explicit Size=32, `CombatDamageSignalAupShiftTransformer` is Explicit Size=16, `InputState` is Explicit Size=24, `PlayerInputState` is Explicit Size=64, `XRInputState` is Explicit Size=64, and `GlobalRegistryContracts.cs` now has 0 `LayoutKind.Sequential` hits by targeted source scan. AUP-backed registry records keep embedded `AbsoluteUniversePosition` fields at 8-byte aligned offsets using the confirmed AUP Size=48 layout.
- Global telemetry black-box quantization: `NanSweeperJob` is Explicit Size=32, `MockOriginShiftFireJob` is Explicit Size=32, and editor-only `BlackboxEditorFrame` is Explicit Size=32. `GlobalTelemetryBus.Blackbox.cs` now has 0 `LayoutKind.Sequential` hits by targeted source scan.
- DTO property purge: `ModuleStatusEventPayload` no longer exposes bool properties; status is a `uint StatusFlags` mask read through static helpers. `NativeBitmask256.IsEmpty`, `NativeArenaSlice<T>.IsCreated`, `XRInputState.HasActiveInput`, and `EcosystemBiomassAuditSample.IsFinite` are methods, not DTO properties.

## Runtime Telemetry

- BufferID.Arm64AlignmentTelemetryRing = 642.
- BufferID.Arm64AlignmentTelemetryCursor = 643.
- AlignmentTelemetryEntry is 64 bytes and records the last 300 alignment faults through Arm64AlignmentTelemetry.
- The telemetry ring and cursor request `NativeArrayOptions.UninitializedMemory`; the 19,200-byte ring is cleared once with `UnsafeUtility.MemClear`, and the one-int cursor is explicitly assigned 0. A scheduled Burst clear job is intentionally not used for this tiny diagnostic buffer to avoid a same-frame schedule/readback loop.
- Fault dump path: Docs/AgentLogs/Dump_SHINOBU_204.bin. The dump writer is compiled only under `UNITY_EDITOR || DEVELOPMENT_BUILD`; release players return `false` and perform no file I/O. In editor/development builds `TryRecordFault` attempts the dump immediately after the ring write and cursor update, and `DumpFaultHistory` exposes the same writer for manual export. The dump emits a 20-byte little-endian header (`magic`, `version`, `count`, `rowBytes`) followed by raw `AlignmentTelemetryEntry` row bytes in circular oldest-to-newest order.
- Optional scene diagnostic: Arm64AlignmentFaultGizmo is `UNITY_EDITOR` fenced, reads the newest fault entry through the diagnostic latest-vault route, subtracts `HectonFloatingOrigin.CurrentTotalOffsetDouble` in double precision, clamps the local scene delta, and draws a red wire cube.

## Current Debt

- Pack=1 debt: STATIC_SOURCE orientation only; last recorded strict source scan text reported 0 hits. Link a full scan tuple before using it as current proof.
- Explicit-layout Pack debt: STATIC_SOURCE orientation only; last recorded strict source scan text reported 0 hits. Link a full scan tuple before using it as current proof.
- Any Pack-parameter debt: STATIC_SOURCE orientation only; last recorded strict source scan text reported 0 hits. Link a full scan tuple before using it as current proof.
- Signal payload fence debt: STATIC_SOURCE orientation only; last recorded source-visible scan text reported 0 ISignal layout/size violations. Link a full scan tuple before using it as current proof.
- Remaining Sequential-layout debt: present in non-Pack structs, especially GPU/HLSL interop, authoring records, and domain-owned data templates. These require owner review before forced Explicit conversion.
- Latest owner-safe Core Sequential conversions: H8Bridge binary entries, InputDeterminism DTOs, duplicate Input/Determinism contracts, GlobalTelemetryBus black-box DTOs/jobs/editor frame, FoveatedSimulationTelemetryEntry, SimulationBucketFrameState, SimulationBucketRebalanceResult, SimulationBucketBlackBoxEntry, CurrentMeta, WeatherRuntimeSnapshot, ContentAuthority Vault/telemetry/pending-load/budget records, ContentLoreBlockIndex, GlobalTelemetryBus TelemetryEvent, DispatcherStateDTO, DispatcherPipelineTelemetryEntry, MockTimeDilationSignal, BrineLayerSample, BatteryRuntimeSnapshot, UIStateData, UIValueSlot, PlayerMovementRuntimeState, PlayerLookState, PlayerSurvivalRuntimeState, PlayerInteractionRuntimeState, AcousticAup, ObjectBatchInstance, ObjectBatchChunk, HectonAabb, HectonSphere, NativeBitmask256, SplineDescriptor, NativeAllocationSnapshotSource, ArenaAllocation, BufferedActionEntry, JobAdmissionBlackboxEntry, CombatDamageSignalAupShiftTransformer, PlayerInputState family, every `StructLayout` record in GlobalRegistryContracts, NativeQueue event payloads, RegistryEventPayload, EntityCommand, and StorageReservationCommitResolvedPayload. `AnomalySignal` duplicate padding from the signal-fence pass was removed after the scoped build attempt exposed it.
- Known ABI exception: Core `InputStateDTO` remains 24 bytes because rollback/netcode contracts currently hard-code `InputStateDTO` offsets at 24-byte boundaries. It is explicit and aligned, but not yet 32-byte quantized.
- Build proof is absent. A scoped `Hecton8.Core.csproj` build was attempted under a green CPU gate and failed with 117 dependency-wall errors from missing sibling/domain contracts; one SHINOBU-owned `AnomalySignal` duplicate padding error was fixed afterward. Latest CPU gate rose above 50 percent, so rerun is forbidden.
