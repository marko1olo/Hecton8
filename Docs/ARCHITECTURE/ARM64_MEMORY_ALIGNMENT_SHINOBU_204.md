# ARM64 Memory Alignment - SHINOBU_204



- Status: partial enforcement pass.
- Last static scan text reported zero `StructLayout(...Pack=...)` under `Assets/_Project/Scripts`.
- Current proof requires rerun artifact path, command/tool, timestamp, environment, and output.
- Core DTO explicit-layout coverage now includes Bridge, input, black-box, foveated telemetry, simulation buckets, weather metadata, queue payloads, content Vault records, global telemetry, and dispatcher records.
- Core runtime snapshots:
  - brine/power/UI/player context;
  - acoustic AUP and object-batch render payloads;
  - spatial intrinsics, native bitmask, logistics spline descriptor;
  - native-memory snapshot source and arena allocation metadata;
  - input dispatch, scheduling black-box, GlobalSignals transform, GlobalTelemetryBus records.
- and every `StructLayout` record inside `GlobalRegistryContracts.cs`
- full non-Pack Sequential layout conversion remains open outside the owner-safe Core set.


## Rules



- Runtime DTOs intended for NativeArray, Vault, Burst, SignalBus, GPU structured upload, or telemetry must not use Pack=1.



- Refactored DTOs use `LayoutKind.Explicit`.
- Exact sizes: `16`, `32`, `64`, `128`, or larger exact 64-byte multiples.
- Larger rows are allowed only for dual AUP data or fixed spectra that cannot fit in 128 bytes.



- 8-byte primitives and AUP-style fields must sit on offsets divisible by 8.



- Padding must be explicit fields so default initialization zeroes every ABI byte.



- Unity job structs containing NativeArray or NativeQueue handles may stay Sequential, but Pack=1 is forbidden and NativeArray fields should use NoAlias where ownership proves non-overlap.



## Added Enforcement



- Editor window: Hecton8/Diagnostics/Memory Alignment X-Ray.



- Strict CLI entry: Hecton8.Editor.Arm64MemoryAlignmentXRayWindow.RunArm64MemoryAlignmentCli.



- Mock proof entry: Hecton8.Editor.Arm64MemoryAlignmentXRayWindow.GenerateMockLayoutStressTest.



- Source fixer/report entry:
  - Tool: `Hecton8.Editor.Arm64LayoutSourceFixer.RunCli`.
  - Parses Core/Physics with Roslyn AST.
  - Prints Roslyn assembly provenance.
  - Removes explicit `Pack` arguments with per-attribute rewrite bookkeeping.
  - Hard-fails Sequential DTO candidates or parser binding failures as `[BLOCKED] AST` / `[BLOCKED] AST_BINDING`.
  - Latest strict target-root scans: `0` Sequential attributes and `0` Pack attributes under Core/Physics.



- Signal payload cold fence: SignalBus<T>.EnsureInitialized rejects invalid 16/32/64/128/192-byte strides using UnsafeUtility.SizeOf<T>() and no reflection.



- Report path: Docs/Reports/ARM64_ALIGNMENT_XRAY_REPORT.txt.



- Source fixer report path: Docs/Reports/ARM64_LAYOUT_SOURCE_FIXER_REPORT.txt.



- Continuous traversal gate: `ModuloSimulationBucketer` consumes `GlobalQualityWeight` and deterministically dithers slow-bucket group activation between 1/2/4 power-of-two groups. DTO layout stays invariant; traversal bandwidth scales by average active buckets.



- Queue payload quantization:
  - Targeted NativeQueue payloads: AudioLog, AtlasSignal, Bootstrap, GameBootstrapper, BiomeMatrix.
  - Additional lanes: Crafting, Weather, Localization, Narrative, Scan, ModuleStatus, Inventory.
  - Core lanes: Core Registry and Core command queue.
  - Layout: Explicit `16/32/64/128`-byte strides.



- Weather ABI quantization:
  - `CurrentMeta`: Explicit Size=32.
  - `WeatherEventPayload`: Explicit Size=128.
  - `WeatherRuntimeSnapshot`: Explicit Size=192.
  - Gerstner wave offsets: 64/96/128 cache-line boundaries.



- Core content/dispatcher quantization:
  - 32B: `ContentBundleRefState`, `DispatcherStateDTO`, `DispatcherPipelineTelemetryEntry`.
  - 64B: `ContentAuthorityTelemetryEntry`, `TelemetryEvent`.
  - 16B: `ContentPendingLoadState`, `ContentVisualFeatureBudget`, `ContentLoreBlockIndex`, `MockTimeDilationSignal`.



- Core runtime snapshot quantization:
  - 16B: `BatteryRuntimeSnapshot`.
  - 32B: `BrineLayerSample`, `UIStateData`, `UIValueSlot`, `PlayerLookState`, `PlayerInteractionRuntimeState`.
  - 128B: `PlayerMovementRuntimeState`, `PlayerSurvivalRuntimeState`.



- Core render/audio explicit exceptions remain ABI-preserved.
- `AcousticAup`: Explicit Size=40.
- `ObjectBatchInstance`: Explicit Size=80.
- `ObjectBatchChunk`: Explicit Size=40.
- They are not yet cache-line resized.



- Core intrinsic/arena quantization: `HectonAabb` Explicit Size=32, `HectonSphere` Explicit Size=16, `NativeBitmask256` Explicit Size=32.
- Additional explicit sizes: `SplineDescriptor` 64, `NativeAllocationSnapshotSource` 32, `ArenaAllocation` 32.
- `NativeArenaSlice<T>` remains Sequential Size=32 as a generic-layout TypeLoad exception with pointer-first fields and named tail padding.



- Core input/registry quantization:
  - `BufferedActionEntry`: Explicit Size=16.
  - `JobAdmissionBlackboxEntry`: Explicit Size=32.
  - `CombatDamageSignalAupShiftTransformer`: Explicit Size=16.
  - `InputState`: Explicit Size=24.
  - `PlayerInputState`: Explicit Size=64.
  - `XRInputState`: Explicit Size=64.
  - `GlobalRegistryContracts.cs`: 0 `LayoutKind.Sequential` hits by targeted source scan.
  - AUP-backed registry records keep embedded `AbsoluteUniversePosition` fields at 8-byte aligned offsets.
  - Confirmed AUP layout: Size=48.



- Global telemetry black-box quantization: `NanSweeperJob`, `MockOriginShiftFireJob`, and editor-only `BlackboxEditorFrame` are Explicit Size=32. Targeted scan finds 0 `LayoutKind.Sequential` hits in `GlobalTelemetryBus.Blackbox.cs`.



- DTO property purge: `ModuleStatusEventPayload` exposes no bool properties; status is `uint StatusFlags` via static helpers. Listed `IsEmpty`/`IsCreated`/`HasActiveInput`/`IsFinite` members are methods.



## Runtime Telemetry



- BufferID.Arm64AlignmentTelemetryRing = 642.



- BufferID.Arm64AlignmentTelemetryCursor = 643.



- AlignmentTelemetryEntry is 64 bytes and records the last 300 alignment faults through Arm64AlignmentTelemetry.



- Telemetry ring and cursor request `NativeArrayOptions.UninitializedMemory`.
- The 19,200-byte ring is cleared once with `UnsafeUtility.MemClear`.
- The one-int cursor is assigned `0`.
- No scheduled Burst clear job: tiny diagnostic buffer, same-frame schedule/readback risk.



- Fault dump route:
  - Path: `Docs/AgentLogs/Dump_SHINOBU_204.bin`.
  - Compile scope: `UNITY_EDITOR || DEVELOPMENT_BUILD`.
  - Release player behavior: returns `false`; no file I/O.
  - Automatic trigger: `TryRecordFault` after ring write and cursor update.
  - Manual trigger: `DumpFaultHistory`.
  - Header: 20-byte little-endian `magic`, `version`, `count`, `rowBytes`.
  - Payload: raw `AlignmentTelemetryEntry` rows, circular oldest-to-newest.



- Optional scene diagnostic: `Arm64AlignmentFaultGizmo`.
- Fence: `UNITY_EDITOR`.
- Reads newest fault entry through diagnostic latest-vault route.
- Subtracts `HectonFloatingOrigin.CurrentTotalOffsetDouble` in double precision.
- Clamps local scene delta and draws a red wire cube.



## Current Debt



- Pack=1 debt: STATIC_SOURCE orientation only; last recorded strict source scan text reported 0 hits. Link a full scan tuple before using it as current proof.



- Explicit-layout Pack debt: STATIC_SOURCE orientation only; last recorded strict source scan text reported 0 hits. Link a full scan tuple before using it as current proof.



- Any Pack-parameter debt: STATIC_SOURCE orientation only; last recorded strict source scan text reported 0 hits. Link a full scan tuple before using it as current proof.



- Signal payload fence debt: STATIC_SOURCE orientation only; last recorded source-visible scan text reported 0 ISignal layout/size violations. Link a full scan tuple before using it as proof.



- Remaining Sequential-layout debt: present in non-Pack structs, especially GPU/HLSL interop, authoring records, and domain-owned data templates. These require owner review before forced Explicit conversion.



- Latest owner-safe Core Sequential conversions:
  - H8Bridge binary entries; InputDeterminism DTOs; duplicate Input/Determinism contracts.
  - GlobalTelemetryBus black-box DTOs/jobs/editor frame; `TelemetryEvent`.
  - FoveatedSimulationTelemetryEntry; SimulationBucket frame/rebalance/black-box rows.
  - CurrentMeta; WeatherRuntimeSnapshot; ContentAuthority Vault/telemetry/pending-load/budget records.
  - ContentLoreBlockIndex; DispatcherStateDTO; DispatcherPipelineTelemetryEntry.
  - MockTimeDilationSignal; BrineLayerSample; BatteryRuntimeSnapshot.
  - UIStateData; UIValueSlot; player movement/look/survival/interaction states.
  - AcousticAup; ObjectBatchInstance; ObjectBatchChunk.
  - HectonAabb; HectonSphere; NativeBitmask256; SplineDescriptor.
  - NativeAllocationSnapshotSource; ArenaAllocation; BufferedActionEntry; JobAdmissionBlackboxEntry.
  - CombatDamageSignalAupShiftTransformer; PlayerInputState family.
  - Every `StructLayout` record in GlobalRegistryContracts.
  - NativeQueue event payloads; RegistryEventPayload; EntityCommand; StorageReservationCommitResolvedPayload.
- `AnomalySignal` duplicate padding from the signal-fence pass was removed after the scoped build attempt exposed it.



- Known ABI exception: Core `InputStateDTO` remains 24 bytes because rollback/netcode contracts hard-code `InputStateDTO` offsets at 24-byte boundaries. It is explicit and aligned, but not yet 32-byte quantized.



- Build proof is absent.
- Scoped `Hecton8.Core.csproj` build under green CPU gate failed with 117 dependency-wall errors from missing sibling/domain contracts.
- One SHINOBU-owned `AnomalySignal` duplicate padding error was fixed afterward.
- Latest CPU gate rose above 50 percent; rerun is forbidden.
