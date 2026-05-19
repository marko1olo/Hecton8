# LOG_SHINOBU_107

## 2026-05-19 SIGNAL_CORRIDOR_PURIFIER Forensic Report

Status: STATIC PASS FOR OWNED SIGNAL CORRIDOR CHANGES. COMPILE BLOCKED BY EXTERNAL DELETED WORLD SOURCE.

What was wrong:
- Player/UI/AI hot loops still had direct `GlobalRegistry` reads, turning service lookup into per-frame coupling.
- `SignalBus<T>` carried 1-to-1 request misuse (`SaveRequestSignal` bus lane) and unbounded burst exposure.
- High-traffic signals carried weak layout/AUP contracts.
- `HectonEventBus` was used for `PlayerTakeDamageEvent`, a managed cancellable hot damage path.
- `signal_tuning_profiles.csv` parser existed without the checked-in tuning artifact.
- Mod projection Burst jobs had incomplete explicit Burst/no-alias contracts.

What was done:
- Replaced hot Player/UI/AI registry reads with cached service fields plus `IGlobalRegistryHotSwapListener` refresh. The method-scoped scanner now reports zero direct `GlobalRegistry.*` in Player/UI/AI `Tick`, `FixedTick`, `LateFrameTick`, `Update`, `FixedUpdate`, `LateUpdate`, or `Update*`.
- Converted `CombatDamageSignal`, `PlayerStateSignal`, and `AcousticPingSignal` to explicit 64-byte value DTO paths with AUP-safe fields and no managed references.
- Added `SignalBus<T>` frame snapshots from `GlobalDataVault` with `NativeArrayOptions.UninitializedMemory`.
- Added deterministic flush ordering for state-mutating lanes.
- Added coalescing for acoustic AUP grid cells and combat target-hash groups.
- Added continuous frame caps based on `SignalBusRegistry.GlobalQualityWeight01`, system stress, and vault-backed CSV tuning.
- Removed the `SignalBus<SaveRequestSignal>` lane and kept save request handling owner-local inside `SaveManager`.
- Deferred deconstruction raycast work through `RaycastCommand` instead of doing synchronous physics queries inside signal drain.
- Added vault-backed signal telemetry ring and dump path `Docs/AgentLogs/Dump_SIGNAL_CORRIDOR.bin`.
- Added editor validation for `ISignal` layout alignment and `Pack=1`.
- Added UI Toolkit Signal Traffic Monitor with histogram, dump button, CSV reload, and live signal injection.
- Removed `HectonEventBus.Publish(new PlayerTakeDamageEvent(...))` from `HectonSurvivalSystem.TakeDamage`.
- Added default `Assets/StreamingAssets/signal_tuning_profiles.csv`.
- Added `[NoAlias]` to the touched mod projection Burst job inputs/output writer.

Cinematic Cheats used:
- Acoustic burst truth is faked as perceptual groups. Multiple pings in one AUP grid cell merge into one loud ping. Before: downstream consumers could process every ping. After: one struct per perceptual cell.
- Combat impact storms are faked as target-hash aggregates. Multiple damage packets for one target/type/channel merge scalar damage while preserving route ownership.
- Deconstruction line-of-sight moved one phase later through deferred raycast. The player gets the same validation result; the signal drain no longer blocks on same-frame physics.
- Continuous quality curve collapses optional signal richness under stress instead of a binary low-tier branch.

Exact microseconds saved, estimates until profiler can run:
- Hot registry cache removals: 0.2-1.0 us per eliminated service read on i3/MX350. Aggregate UI/PDA/HUD-heavy frame estimate: 2-8 us.
- Acoustic 500-ping burst coalesced to 1-16 cells on low quality: expected 50-250 us saved depending on consumer fanout.
- Combat target-hash coalescing: expected 5-80 us saved during stacked hazard/combat frames.
- Deferred deconstruction raycast: expected 5-40 us removed from signal drain spikes.
- Removed `PlayerTakeDamageEvent` managed dispatch: one managed event allocation plus dispatch/probe removed per player damage call; sub-us isolated, several us under hazard stacks.
- SaveRequest bus deletion: cold-start only; one unused lane avoided.

Compile verification:
- `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` timed out after 124 seconds without compiler errors.
- Later `dotnet build Assembly-CSharp.csproj --no-restore -nologo -clp:ErrorsOnly -maxcpucount:1` failed before SHINOBU files compiled:
  `CSC : error CS2001: Source file 'C:\hades\Hecton8\Assets\_Project\Scripts\World\HectonMapMagicVegetationBridgeFloraCollisionProxies.cs' could not be found.`
- Current proof: `Test-Path Assets\_Project\Scripts\World\HectonMapMagicVegetationBridgeFloraCollisionProxies.cs` is false and `Hecton8.Core.csproj:531` still includes it.
- No rebuild was launched after dotnet processes appeared again; hardware gate respected.

<SELF_AUDIT>
  <TASK_RECONCILIATION>
    <TASK id="01" status="[PASS]">Hot-method scanner reports zero direct GlobalRegistry calls in Player/UI/AI hot methods. Remaining broad hits are registration, cold cache hydration, or non-hot helpers.</TASK>
    <TASK id="02" status="[PASS]">High-frequency first-party damage EventBus path removed. Remaining EventBus usages are death, boot, spawn, random/meta/item collection, or Mod API projection candidates.</TASK>
    <TASK id="03" status="[PASS]">Touched signal DTOs are unmanaged fields only. Editor validator now rejects non-unmanaged `ISignal` through `UnsafeUtility.SizeOf<T>()` failure.</TASK>
    <TASK id="04" status="[PASS]">PlayerStateSignal, AcousticPingSignal, CombatDamageSignal, SignalTelemetryFrame, and SignalTuningProfile use explicit layouts sized 32 or 64 bytes.</TASK>
    <TASK id="05" status="[PASS]">MockSignalGenerators inject deterministic AcousticPing and CombatDamage bursts without UnityEngine.Random.</TASK>
    <TASK id="06" status="[PASS]">SignalBus<T>.ParallelWriter exists and no producer Complete() was added for publication.</TASK>
    <TASK id="07" status="[PASS]">FlushPreSimulation snapshots queue contents into contiguous frame arrays exposed through ReadOnlySpan/NativeArray read-only views.</TASK>
    <TASK id="08" status="[PASS]">Dear Lie coalescing implemented for AcousticPingSignal and CombatDamageSignal.</TASK>
    <TASK id="09" status="[PASS]">Frame caps use `math.lerp(minSignals, maxSignals, curvedQuality)` from GlobalQualityWeight and stress. No binary hardware switch added.</TASK>
    <TASK id="10" status="[PASS]">Touched Player/UI/AI systems use cached services and cold hot-swap listener refresh.</TASK>
    <TASK id="11" status="[PASS]">SaveRequest SignalBus lane removed. Docking/Wake request-named lanes remain classified as command broadcasts requiring owner route cards, not proven request/response in this pass.</TASK>
    <TASK id="12" status="[PASS]">ConstructionManager deconstruction raycast now uses RaycastCommand and completes next LateFrameTick.</TASK>
    <TASK id="13" status="[PASS]">Signal flush rejects non-finite/out-of-bounds spatial payloads and increments corruption counters.</TASK>
    <TASK id="14" status="[PASS]">State-mutating lanes sort deterministic snapshots before consumption.</TASK>
    <TASK id="15" status="[PASS]">Signal snapshot buffers use GlobalDataVault and NativeArrayOptions.UninitializedMemory.</TASK>
    <TASK id="16" status="[PASS]">300-frame vault telemetry ring and Dump_SIGNAL_CORRIDOR.bin dump path implemented.</TASK>
    <TASK id="17" status="[PASS]">Editor layout validator scans ISignal structs for Pack=1 and size multiple-of-8 violations.</TASK>
    <TASK id="18" status="[PASS]">UI Toolkit Signal Traffic Monitor reads telemetry ring/lane counters and highlights shedding.</TASK>
    <TASK id="19" status="[PASS]">Allocation-free CSV parser plus checked-in StreamingAssets CSV baseline implemented.</TASK>
    <TASK id="20" status="[PASS]">Editor live injector supports mock damage, mock footstep, combat damage, and acoustic burst.</TASK>
  </TASK_RECONCILIATION>

  <STRUCT_LAYOUT_VERIFICATION compile_runtime="blocked_by_external_missing_world_source">
    <PlayerStateSignal size="64">
      <Field offset="0" size="48" name="PositionAup"/>
      <Field offset="48" size="4" name="Intensity01"/>
      <Field offset="52" size="4" name="SourceHash"/>
      <Field offset="56" size="4" name="Frame"/>
      <Field offset="60" size="1" name="State"/>
      <Field offset="61" size="1" name="Flags"/>
      <Padding offset="62" size="2"/>
    </PlayerStateSignal>
    <AcousticPingSignal size="64">
      <Field offset="0" size="48" name="PositionAup"/>
      <Field offset="48" size="4" name="RadiusMeters"/>
      <Field offset="52" size="4" name="Intensity01"/>
      <Field offset="56" size="4" name="SourceId"/>
      <Field offset="60" size="1" name="Channel"/>
      <Field offset="61" size="1" name="Flags"/>
      <Padding offset="62" size="2"/>
    </AcousticPingSignal>
    <CombatDamageSignal size="64">
      <Field offset="0" size="24" name="ImpactAup_double3"/>
      <Field offset="24" size="12" name="Direction_float3"/>
      <Field offset="36" size="4" name="Magnitude"/>
      <Field offset="40" size="4" name="DamageType"/>
      <Field offset="44" size="4" name="TargetHash"/>
      <Field offset="48" size="4" name="SourceHash"/>
      <Field offset="52" size="4" name="Frame"/>
      <Field offset="56" size="2" name="SourceId"/>
      <Field offset="58" size="2" name="TargetId"/>
      <Field offset="60" size="1" name="Channel"/>
      <Field offset="61" size="1" name="Flags"/>
      <Field offset="62" size="1" name="IntegrityDelta"/>
      <Field offset="63" size="1" name="Reserved0"/>
    </CombatDamageSignal>
    <SignalTelemetryFrame size="64">
      <Field offset="0" size="4" name="Frame"/>
      <Field offset="4" size="4" name="TotalPushedSignals"/>
      <Field offset="8" size="4" name="PeakSignalsPerFrame"/>
      <Field offset="12" size="4" name="CoalescedSignals"/>
      <Field offset="16" size="4" name="DroppedSignals"/>
      <Field offset="20" size="4" name="CorruptedSignals"/>
      <Field offset="24" size="4" name="ActiveLaneCount"/>
      <Field offset="28" size="4" name="Flags"/>
      <Field offset="32" size="4" name="GlobalQualityMilli"/>
      <Field offset="36" size="4" name="SystemStressMilli"/>
      <Field offset="40" size="8" name="Reserved0"/>
      <Field offset="48" size="8" name="Reserved1"/>
      <Field offset="56" size="8" name="Reserved2"/>
    </SignalTelemetryFrame>
    <SignalTuningProfile size="32">
      <Field offset="0" size="4" name="LaneHash"/>
      <Field offset="4" size="4" name="MinFrameSignals"/>
      <Field offset="8" size="4" name="MaxFrameSignals"/>
      <Field offset="12" size="4" name="CoalescingRadiusMeters"/>
      <Field offset="16" size="4" name="Priority"/>
      <Field offset="20" size="4" name="Flags"/>
      <Field offset="24" size="8" name="Reserved0"/>
    </SignalTuningProfile>
  </STRUCT_LAYOUT_VERIFICATION>

  <SCALABILITY_CURVE>
    SignalBus<T>.ResolveFrameLimit reads GlobalQualityWeight01, multiplies it by lerp(1.0, 0.35, systemStress), then applies smoothstep q*q*(3-2*q). At quality below 0.3, non-critical VFX lanes collapse toward 1-frame facts and critical lanes stay above minimum caps. The checked-in CSV can lower min/max and raise coalescing radius for survival-tier hardware; high and ultra tiers can raise max cap and reduce coalescing radius without recompilation.
  </SCALABILITY_CURVE>

  <H_PHI_VAULT_STATUS>
    <PersistentNativeArrays>Signal snapshots, tuning rows, CSV scratch, and telemetry ring are resolved from GlobalDataVault handles, not allocated as private NativeArray fields. Cold managed priority arrays remain in SignalPriorityTable as fixed 64-row fallback tables, outside gameplay hot path.</PersistentNativeArrays>
    <VaultBufferHandle id="0x40000000 | (laneHash & 0x3FFFFFFF)" owner="SystemID.CoreDataVault" usage="per-lane SignalBus frame snapshot"/>
    <VaultBufferHandle id="73038" owner="SystemID.CoreDiagnostics" usage="SignalTelemetryFrame[300] black-box ring"/>
    <VaultBufferHandle id="73039" owner="SystemID.CoreDiagnostics" usage="telemetry cursor int[1]"/>
    <VaultBufferHandle id="73040" owner="SystemID.CoreDiagnostics" usage="SignalTuningProfile[64]"/>
    <VaultBufferHandle id="73041" owner="SystemID.CoreDiagnostics" usage="profile count int[1]"/>
    <VaultBufferHandle id="73042" owner="SystemID.CoreDiagnostics" usage="CSV scratch byte[8192]"/>
  </H_PHI_VAULT_STATUS>

  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    <Job name="ProjectCombatDamageSignalsJob" consumes="SignalBus<CombatDamageSignal>.GetFrameSnapshotArray()" outputs="NativeQueue<ModEventDto>.ParallelWriter" aliasing="[ReadOnly, NoAlias] Signals; [NoAlias] Output"/>
    <Job name="ProjectWeatherChangedSignalsJob" consumes="SignalBus<WeatherChangedSignal>.GetFrameSnapshotArray()" outputs="NativeQueue<ModEventDto>.ParallelWriter" aliasing="[ReadOnly, NoAlias] Signals; [NoAlias] Output"/>
    <Dependency>ModEventProjectionBridge schedules projection jobs and finalizes via DispatcherJobSwap, not arbitrary producer Complete() for signal publishing.</Dependency>
    <Dependency>ConstructionManager deconstruction consumes request queue, schedules RaycastCommand, and applies after deferred completion in LateFrameTick.</Dependency>
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>

  <COMPILE_GUARD>
    No new asmdef or sibling Runtime assembly reference was added. Changes route through existing Core contracts, SignalBus<T>, GlobalRegistry cold cache, or editor-only UI. Compile cannot be proven until the external missing World source referenced by Hecton8.Core.csproj is restored or the project metadata is corrected by the World owner.
  </COMPILE_GUARD>

  <DEAR_LIE_CONFIRMATION>
    Acoustic and combat signal storms are perceptual aggregates, not physical truth replay. Before: O(N*C) downstream processing for N signals and C consumers. After: O(N*K) bounded in-place grouping where K is capped by continuous frame limit, then O(K*C) downstream processing. With N=500 and K=16 on low quality, consumer-visible facts drop by about 31x.
  </DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

---

Loop 12 delta: CS1612 residue and request-lane boundary.

What was wrong:
- `ScalabilityChangedEvent`, `AcousticZoneChangedEvent`, and `DirectorAIMusicSignal` were still sequential/property-backed `ISignal` DTOs.
- `SaveRequestSignal` no longer had an active bus lane, but still implemented `ISignal`, which made a local request packet look like a legal broadcast payload.

What was done:
- Converted `ScalabilityChangedEvent` to a 16-byte explicit readonly field payload: offsets 0 `PreviousTier`, 1 `CurrentTier`, 2 `PreviousQualityTier`, 3 `CurrentQualityTier`, 4 `Reserved0`, 8 `Reserved1`.
- Converted `AcousticZoneChangedEvent` to a 16-byte explicit readonly field payload: offsets 0 `IsInterior`, 1 `Reserved0`, 2 `Reserved1`, 4 `Reserved2`, 8 `Reserved3`.
- Converted `DirectorAIMusicSignal` to a 32-byte explicit readonly field payload: offsets 0 `Position`, 12 `Value`, 16 `EventType`, 17 `BoolValue`, 18 `Reserved0`, 20 `Reserved1`, 24 `Reserved2`.
- Updated music consumers to interpret `IsInterior` and `BoolValue` byte fields via `!= 0`.
- Removed `ISignal` from `SaveRequestSignal`.

Cinematic Cheats used:
- No physical simulation was added. The change keeps acoustic-zone and director music cues as scalar facts; DSP and presentation remain owners of the visual/audio illusion.

Exact microseconds saved, estimates until profiler can run:
- Property-backed DTO removal: sub-us per snapshot drain, mostly from eliminating accessor/conversion calls and defensive copy risk.
- Save request boundary: no frame-time change; one invalid route removed from signal tooling.

Verification:
- Static property-residue scan over matched `ISignal` structs in `GlobalSignals.cs` and `HectonSignalLaneContract.cs` reports no matched payload with `=>` or `{ get; }`.
- `rg` reports no `SaveRequestSignal : ISignal`, no `SignalBus<SaveRequestSignal>`, and no save-request lane publish/dequeue helpers.
- `git diff --check` passed for Loop 12 touched files with line-ending warnings only.
- Compile not relaunched: `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs` is still absent and would fail before owned files compile.

<SELF_AUDIT_LOOP_12>
  <TASK_RECONCILIATION>
    <TASK id="03" status="[PASS]">Property-backed signal DTO residue removed from the three matched field-access offenders in owned signal files.</TASK>
    <TASK id="04" status="[PASS]">The three refactored DTOs now use explicit 16/32-byte layouts with manual offsets; PlayerStateSignal and AcousticPingSignal already have explicit 64-byte layouts with tail padding.</TASK>
    <TASK id="11" status="[PASS]">SaveRequestSignal is no longer an ISignal and cannot be initialized as a typed broadcast lane.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <ScalabilityChangedEvent size="16" math="1+1+1+1+4+8=16"/>
    <AcousticZoneChangedEvent size="16" math="1+1+2+4+8=16"/>
    <DirectorAIMusicSignal size="32" math="12+4+1+1+2+4+8=32"/>
  </STRUCT_LAYOUT_VERIFICATION>
  <COMPILE_GUARD>No new sibling assembly reference was added. Build remains blocked by the existing missing World source outside Signal Corridor ownership.</COMPILE_GUARD>
</SELF_AUDIT_LOOP_12>

---

Loop 13 delta: central lane dispatch devirtualization.

What was wrong:
- `SignalBusRegistry.FlushPreSimulation()` walked an `ISignalLane[]` and called `FlushPreSimulation` through interface dispatch for all centrally initialized lanes.
- The fallback registry was valid as cold infrastructure, but using it as the normal frame-phase dispatch path violated the IL2CPP devirtualization rule.

What was done:
- Added a direct generic dispatch table that calls `SignalBus<T>.FlushPreSimulation(lowTier, systemStressMilli)` for every `SignalBus<T>.EnsureInitialized()` lane owned by `GlobalSignals`.
- Added matching direct post-simulation snapshot clearing.
- Registration now marks central lanes as direct and stores only non-direct dynamic lanes in `_fallbackLaneIndices`.
- Fallback `ISignalLane` dispatch remains for dynamic/debug lanes, telemetry, and cold dispose, not for the normal central lane set.

Cinematic Cheats used:
- No simulation was expanded. The patch spends less CPU on communication plumbing so saved time remains available for visual/audio consumers downstream of the signal facts.

Exact microseconds saved, estimates until profiler can run:
- Static count removes 132 normal-path interface flush calls and 132 normal-path interface clear calls per frame.
- Estimated low-end saving: 2-20 us/frame depending on runtime backend and lane count initialized at boot.

Verification:
- Static parity scan reports `EnsureInitialized=132`, `DirectFlush=132`, `DirectClear=132`, `DirectPolicy=132`, mismatches `0`.
- Source scan confirms no `_lanes[i].FlushPreSimulation` or `_lanes[i].ClearPostSimulation` direct hot loop remains; fallback dispatch iterates `_fallbackLaneIndices`.
- `git diff --check` passed for `GlobalSignals.cs` with line-ending warning only.
- Compile not relaunched because `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs` is still absent.

<SELF_AUDIT_LOOP_13>
  <TASK_RECONCILIATION>
    <TASK id="06" status="[PASS]">Normal central lane flush now uses generic `SignalBus<T>` static dispatch, not interface-array dispatch.</TASK>
    <TASK id="07" status="[PASS]">Pre-simulation snapshot phase behavior remains the same; only the registry dispatch route changed.</TASK>
    <TASK id="09" status="[PASS]">Direct dispatch still feeds the same quality/stress arguments into each lane's continuous cap resolver.</TASK>
  </TASK_RECONCILIATION>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    <Dispatch consumes="SignalBusRegistry.LowTierMode, SignalBusRegistry.SystemStressMilli, GlobalSignals.SimulationPaused" outputs="per-lane frame snapshots"/>
    <DirectPath count="132" route="SignalBus<T>.FlushPreSimulation"/>
    <FallbackPath route="_fallbackLaneIndices -> ISignalLane" scope="dynamic lanes only"/>
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No asmdef or sibling runtime reference was added. The edited file remains in Core/Signal Corridor. Current compile proof is blocked by the known missing World source.</COMPILE_GUARD>
</SELF_AUDIT_LOOP_13>

---

Loop 14 delta: rollback/input DTO explicit-layout closure.

What was wrong:
- Eight rollback/input signal DTOs still used sequential layout in `GlobalSignals.cs`.
- `InputStateSignal` wrapped `InputState`, and hot signal consumers read `InputState.Move`, `InputState.Look`, and `InputState.VerticalAxis` computed properties.

What was done:
- Converted `InputSignal`, `StateCorrectionSignal`, `DesyncDetectedSignal`, `SyncFenceSignal`, `KccVelocitySignal`, `InputStateSignal`, `LockstepSnapshotSignal`, and `SystemGlitchSignal` to explicit layouts with manual `FieldOffset` padding.
- Added a size guard for `InputStateSignal(32)` beside the existing rollback signal guards.
- Removed the three computed `InputState` axis properties and replaced lane consumers with direct field dequantization:
  - move = `(MoveX, MoveY) * AxisInvQuantizeScale`
  - look = `(LookX, LookY) * LookInvQuantizeScale`
  - vertical = `Vertical * AxisInvQuantizeScale`

Cinematic Cheats used:
- No physics fidelity was added. The rollback lane continues to transmit quantized input and authoritative state facts; presentation systems spend the saved CPU, not the corridor.

Exact microseconds saved, estimates until profiler can run:
- Input dequantization property removal: sub-us per consumer read; expected gain is mostly eliminating hidden accessor copies on deterministic input snapshots.
- Explicit layout: frame-time gain is data-layout risk reduction, not a direct ALU reduction. It protects memcpy/sort/cache behavior for rollback lanes on ARM64.

Verification:
- Targeted scan reports PASS for explicit layouts on all eight patched DTOs.
- Targeted old sequential-layout scan reports zero matches for those DTOs.
- Targeted scan reports no remaining `InputState.Move`, `InputState.Look`, or `InputState.VerticalAxis` definitions or hot signal-consumer reads.
- `ValidateSignalSize<InputStateSignal>(32)` is present with the existing rollback size guards.
- `git diff --check` passed for Loop 14 files with line-ending warnings only.
- Compile not relaunched: `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs` remains absent.

<SELF_AUDIT_LOOP_14>
  <TASK_RECONCILIATION>
    <TASK id="03" status="[PASS]">Removed computed input DTO properties from the signal-consumer path; patched signal payloads remain unmanaged public-field DTOs.</TASK>
    <TASK id="04" status="[PASS]">Converted eight rollback/input signal DTOs to explicit layouts with manual padding.</TASK>
    <TASK id="14" status="[PASS]">Rollback-facing payloads now have fixed offsets for deterministic ordering/memcpy tooling; existing sort policy remains active.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <InputSignal size="48" math="8+8+4+4+4+4+4+1+1+2+8=48"/>
    <StateCorrectionSignal size="128" math="48+12+12+16+4+4+4+4+4+1+1+2+4+4+8=128"/>
    <DesyncDetectedSignal size="32" math="4+4+4+4+4+1+1+2+4+4=32"/>
    <SyncFenceSignal size="128" math="48+12+12+16+4+4+4+4+1+1+2+4+8+8=128"/>
    <KccVelocitySignal size="80" math="48+12+4+4+4+4+1+1+2=80"/>
    <InputStateSignal size="32" math="24+4+1+1+2=32"/>
    <LockstepSnapshotSignal size="32" math="8+4+4+4+4+4+4=32"/>
    <SystemGlitchSignal size="32" math="4+4+4+4+4+4+1+1+2+4=32"/>
  </STRUCT_LAYOUT_VERIFICATION>
  <COMPILE_GUARD>No new assembly reference or sibling-domain dependency was added. Build is still blocked by the known missing World source outside SHINOBU ownership.</COMPILE_GUARD>
</SELF_AUDIT_LOOP_14>

---

Loop 15 delta: GlobalSignals and procedural-audio payload closure.

What was wrong:
- `GlobalSignals.cs` still contained real public `ISignal` DTOs using sequential layout after the rollback pass.
- `AudioEvent` embedded nested audio payload structs whose layout was not explicit at the signal ABI boundary.
- `AudioPingTriggerInfo` exposed `StartTimeSeconds` as a computed property, which is an accessor method on a payload used inside a typed signal.

What was done:
- Converted the remaining `GlobalSignals.cs` sequential signal DTOs touched by the scan to explicit layouts.
- Converted `AudioPingTriggerInfo`, `HullStressSignal`, and `StructuralStressAudioInfo` in `ProceduralAudioEvents.cs` to explicit public readonly field payloads.
- Replaced `AudioEvent` with a 128-byte explicit union: 16-byte header plus variant payload at offset 16.
- Updated `ValidateSignalSize<global::Hecton8.Core.Contracts.Signals.AudioEvent>` from 144 to 128.
- Removed `AudioPingTriggerInfo.StartTimeSeconds`; source scan found no consumers.

Cinematic Cheats used:
- The audio route now transports one active variant instead of carrying both variant payload slots. No acoustic simulation was added; the lane carries the compact fact and leaves procedural richness to the audio renderer.

Exact microseconds saved, estimates until profiler can run:
- `AudioEvent` saves 16 bytes per queued/flushed event. In a 128-event burst that is 2048 bytes less snapshot bandwidth.
- Property removal is sub-us per read; the real gain is eliminating accessor-method risk from a payload type used in signal snapshots.

Verification:
- Source scan reports zero real `Sequential ISignal` declarations in `GlobalSignals.cs`.
- Targeted payload scan reports no `{ get; }`, no `=>`, and no `LayoutKind.Sequential` inside the Loop 15 DTO declarations.
- `rg` reports no `AudioEvent` 144-byte size guard in active source; the active guard is `AudioEvent(128)`.
- `git diff --check` passed for `GlobalSignals.cs` and `ProceduralAudioEvents.cs` with line-ending warnings only.
- Compile not relaunched because `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs` remains absent.

<SELF_AUDIT_LOOP_15>
  <TASK_RECONCILIATION>
    <TASK id="03" status="[PASS]">Global signal DTO residue and nested audio payload property residue removed from the typed corridor surface.</TASK>
    <TASK id="04" status="[PASS]">Loop 15 DTOs now use explicit offsets; `AudioEvent` is 128 bytes, not 144.</TASK>
    <TASK id="17" status="[PASS]">Targeted static scan confirms no sequential layout remains in the patched GlobalSignals/audio payload set.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <AudioEvent size="128" offsets="0 Kind byte; 1 Reserved0 byte; 2 Reserved1 ushort; 4 Reserved2 uint; 8 Reserved3 ulong; 16 AudioPing(48) union StructuralStress(96)" math="16 header + max(48,96) payload = 112, explicit tail to 128"/>
    <AudioPingTriggerInfo size="48" math="8+4+4+4+12+4+4+1+1+2+4=48"/>
    <StructuralStressAudioInfo size="96" math="48 AUP + 12 world + 9 floats/uint padding region to 96"/>
    <DataVaultUpdateSignal size="32" math="4+4+4+4+4+4+2+2+4=32"/>
    <VoxelCarveEvent size="128" math="8 + 4*float3 + 2*double3 + 2 floats + 4 bytes + 3 uint pads = 128"/>
  </STRUCT_LAYOUT_VERIFICATION>
  <H_PHI_VAULT_STATUS>No private NativeArray/NativeList/NativeHashMap was added. Loop 15 changes are DTO-only and reuse existing SignalBus/Vault buffers.</H_PHI_VAULT_STATUS>
  <COMPILE_GUARD>No asmdef or sibling runtime reference was added. Audio payload edits are cross-domain ABI edits required because `AudioEvent` is a Core signal payload.</COMPILE_GUARD>
</SELF_AUDIT_LOOP_15>

---

Loop 16 delta: localized public `ISignal` explicit-layout guard.

What was wrong:
- A repo-wide source scan still found localized public `ISignal` DTOs declared as sequential structs outside `GlobalSignals.cs`.
- The editor validator only rejected `Pack=1` and non-8-byte sizes, so a future sequential signal could pass if its size was aligned.

What was done:
- Converted 30 localized public `ISignal` DTOs to explicit `FieldOffset` layouts without changing domain producer/consumer logic.
- Widened refactored 40/48-byte signal payloads to 64 bytes where the strict signal DTO rule required manual padding: `DroneFleetInventoryTransactionSignal`, `MockPlayerPositionSignal`, `ThermodynamicsMockDamageSignal`, `FloraSpawnedSignal`, `DeltaCrusherMockLaserFireSignal`.
- Strengthened `SignalPayloadLayoutValidator` to reject any reflected `ISignal` whose `StructLayoutAttribute.Value` is not `LayoutKind.Explicit`.

Cinematic Cheats used:
- No new simulation was introduced. This pass is ABI hygiene: one fact stays one route, with stable memory stride and no physical-system expansion.

Exact microseconds saved, estimates until profiler can run:
- Main measurable bandwidth win remains Loop 15's 16 bytes/event on `AudioEvent`.
- Loop 16 is a preventive ARM64/cache-line safety pass. Expected gain is avoiding unaligned/vectorization regressions, not reducing a specific hot loop today.

Verification:
- `rg --pcre2 -U` scan for `[StructLayout(LayoutKind.Sequential...)] public ... struct ... : ISignal` reports zero active source matches.
- Source scan over public `ISignal` declarations reports zero structs missing nearby `StructLayout(LayoutKind.Explicit)`.
- Search found no active source `SizeOf<T>` or `ValidateSignalSize<T>` expectations for the old 40/48-byte localized signal sizes.
- `git diff --check` passed for all Loop 16 files with line-ending warnings only.
- Compile not relaunched: the external World source `HectonMapMagicVegetationBridgeFloraCollisionProxies.cs` is still absent and no dotnet/csc process was present.

<SELF_AUDIT_LOOP_16>
  <TASK_RECONCILIATION>
    <TASK id="03" status="[PASS]">Localized public signal payloads are explicit public-field unmanaged DTOs after this pass; no managed fields were introduced.</TASK>
    <TASK id="04" status="[PASS]">Sequential localized public `ISignal` declarations are removed from active source.</TASK>
    <TASK id="17" status="[PASS]">Editor validator now fails non-explicit `ISignal` layouts, Pack=1, and non-8-byte signal sizes.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <DroneFleetInventoryTransactionSignal size="64" math="5 ints 20 + float3 12 + uint flags 4 + uint pad 4 + 3 ulong pads 24 = 64"/>
    <MockPlayerPositionSignal size="64" math="double3 24 + frame/seed/flags/pad uints 16 + 3 ulong pads 24 = 64"/>
    <ThermodynamicsMockDamageSignal size="64" math="double3 24 + float3 12 + damage float 4 + entity/flags uints 8 + 2 ulong pads 16 = 64"/>
    <FloraSpawnedSignal size="64" math="FloraAupCell 24 + species/plant/biomass/matrix/pad uint region 24 + 2 ulong pads 16 = 64"/>
    <DeltaCrusherMockLaserFireSignal size="64" math="double3 24 + radius 4 + two 1-byte fields + ushort 2 + material/frame uints 8 + two uint pads 8 + two ulong pads 16 = 64"/>
  </STRUCT_LAYOUT_VERIFICATION>
  <H_PHI_VAULT_STATUS>No private persistent collections were added. Some widened DTOs are stored by existing domain vault/scratch buffers; buffer ownership remains with their original domains.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>No new jobs were added. Existing SignalBus producers/consumers and job dependency chains were not modified.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>Loop 16 touches signal ABI structs only. No asmdef, sibling-runtime reference, or direct domain dependency was added.</COMPILE_GUARD>
</SELF_AUDIT_LOOP_16>

---

Loop 17 delta: mod projection continuous quality gate.

What was wrong:
- `ModEventProjectionBridge.ResolveProjectionCap()` used `GlobalRegistry.ScalabilityTierProfileByte == 0` and jumped between `10` and `50` projected events.
- `ProjectCombatDamageSignalsJob` and `ProjectWeatherChangedSignalsJob` received a `LowTier` byte, leaving a binary policy inside the native-to-managed projection bridge.

What was done:
- Replaced the tier byte branch with `SignalBusRegistry.GlobalQualityWeight01`.
- Added smoothstep curve math: `q*q*(3-2*q)`, then `math.lerp(10, 50, curve)` and `math.clamp`.
- Passed `float QualityWeight01` into both Burst jobs.
- Preserved the public mod DTO low-sample bit by deriving it from `math.step(QualityWeight01, 0.3f)` instead of polling the registry tier.

Cinematic Cheats used:
- No simulation was added. The bridge still projects compact sampled facts from `CombatDamageSignal` and `WeatherChangedSignal`; under pressure it exposes fewer mod callbacks rather than asking gameplay systems to emit alternate event truth.

Exact microseconds saved, estimates until profiler can run:
- Worst-case managed projection opportunity count drops continuously from 50 toward 10 as quality weight falls.
- At thermal quality near 0.1, smoothstep is about 0.028, cap is about 11 instead of the old high-tier 50. This avoids up to 39 managed callback dispatch opportunities in a frame; exact cost depends on subscribed mods and remains unmeasured.

Verification:
- `rg` reports zero `ScalabilityTierProfileByte` in `ModEventProjectionBridge.cs`.
- `rg` reports no `LowTier` job field in `ModEventProjectionBridge.cs`; the remaining `LowTierProjectionCap` name is a cap constant.
- `git diff --check` passed for `ModEventProjectionBridge.cs` with line-ending warning only.
- Compile not relaunched by user instruction and because the known external World source is still absent.

<SELF_AUDIT_LOOP_17>
  <TASK_RECONCILIATION>
    <TASK id="02" status="[PASS]">No new gameplay `HectonEventBus` traffic was added; the edited bridge remains a mod/API projection surface.</TASK>
    <TASK id="09" status="[PASS]">Projection load shedding now consumes continuous `SignalBusRegistry.GlobalQualityWeight01` and smoothstep/lerp math.</TASK>
    <TASK id="06" status="[PASS]">Existing typed SignalBus source lanes stay unchanged; only the projection budget and job scalar changed.</TASK>
  </TASK_RECONCILIATION>
  <SCALABILITY_CURVE_EXPLANATION>Projection cap = round(lerp(10, 50, q*q*(3-2*q))). At q=0.1 the cap is about 11; at q=0.5 it is 30; at q=1.0 it is 50. Below 0.3, mod callbacks are sampled aggressively while first-party signal truth remains in typed snapshots.</SCALABILITY_CURVE_EXPLANATION>
  <H_PHI_VAULT_STATUS>No private NativeArray/NativeList/NativeHashMap was added. Existing `_projectedEvents` and `_cullTelemetry` ownership remains unchanged in this legacy mod bridge.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>Jobs still consume `SignalBus<CombatDamageSignal>.GetFrameSnapshotArray()` and `SignalBus<WeatherChangedSignal>.GetFrameSnapshotArray()` and output to `_projectedEvents.AsParallelWriter()`. `[ReadOnly, NoAlias]` and `[NoAlias]` remain present.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No asmdef, sibling runtime reference, or new cross-domain concrete dependency was added.</COMPILE_GUARD>
</SELF_AUDIT_LOOP_17>

---

Loop 18 delta: inventory native payload Pack=1 removal.

What was wrong:
- `InventoryPhysicalDropRequestPayload` used `Pack=1` while carrying two `Vector3` values, an `ulong`, and scalar fields through cross-domain inventory event routing.
- `InventoryEventPayload` was sequential NativeQueue data with implicit offsets.

What was done:
- Converted `InventoryEventPayload` to `[StructLayout(LayoutKind.Explicit, Size = 24)]`.
- Converted `InventoryPhysicalDropRequestPayload` to `[StructLayout(LayoutKind.Explicit, Size = 48)]`.
- Added named `_pad0` at offset 44 so the 48-byte drop payload has explicit tail padding.

Cinematic Cheats used:
- None added. This pass does not simulate dropped items differently; it only removes unsafe ABI packing from the routing payload.

Exact microseconds saved, estimates until profiler can run:
- No direct ALU win claimed. The value is avoiding ARM64 unaligned access penalties and future NativeQueue/Burst copy instability on item-drop packets.

Verification:
- `rg` reports no `Pack = 1` in `InventoryEvents.cs`.
- `git diff --check` passed for `InventoryEvents.cs` with line-ending warning only.
- Compile not relaunched by instruction and because the known external World source is still absent.

<SELF_AUDIT_LOOP_18>
  <TASK_RECONCILIATION>
    <TASK id="04" status="[PASS]">Removed Pack=1 from a native inventory routing payload and pinned offsets explicitly.</TASK>
    <TASK id="17" status="[PASS]">Inventory native payloads now have explicit layouts; static scan found no Pack=1 in `InventoryEvents.cs`.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <InventoryEventPayload size="24" offsets="0 TotalMassKg float; 4 CarryCapacityKg float; 8 Load01 float; 12 ItemHashId uint; 16 ReferenceSlot int; 20 EventType ushort; 22 Reserved ushort" math="4+4+4+4+4+2+2=24"/>
    <InventoryPhysicalDropRequestPayload size="48" offsets="0 RuntimePosition Vector3; 12 InitialImpulse Vector3; 24 GeneticsMask ulong; 32 ItemHashId uint; 36 Quantity int; 40 QualityMilli ushort; 42 Reserved ushort; 44 _pad0 uint" math="12+12+8+4+4+2+2+4=48"/>
  </STRUCT_LAYOUT_VERIFICATION>
  <H_PHI_VAULT_STATUS>No private persistent collections were added. Existing `InventoryEvents` NativeQueues and sidecar arrays remain unchanged.</H_PHI_VAULT_STATUS>
  <COMPILE_GUARD>No asmdef, sibling runtime reference, or new route dependency was added.</COMPILE_GUARD>
</SELF_AUDIT_LOOP_18>

---

Loop 19 delta: mod projection player-context hot cache.

What was wrong:
- `ModEventProjectionBridge.ResolvePlayerRuntimePosition()` called `GlobalRegistry.Player` while scheduling projected mod events.
- The bridge can execute every frame when projected mod subscribers exist, so the resolver was still a hot registry polling point.

What was done:
- Added `_playerRuntimeContext` to the bridge.
- Filled it once in `Install()`.
- Registered the bridge as an `IGlobalRegistryHotSwapListener`.
- Refreshed the cached player context on `GlobalRegistryServiceSlot.Player` replacement.
- Changed `ResolvePlayerRuntimePosition()` to read the cached interface only.

Cinematic Cheats used:
- None added. This is dependency-routing hygiene; position still comes from the owner `IPlayerRuntimeContext` snapshot.

Exact microseconds saved, estimates until profiler can run:
- Sub-microsecond per projected frame from removing one registry slot read.
- Higher value is compile-wall and coupling reduction: projected mods no longer force a GlobalRegistry dependency inside the frame resolver.

Verification:
- `rg` shows the only `GlobalRegistry.Player` in `ModEventProjectionBridge.cs` is the cold cache fill during `Install()`.
- `rg` confirms `ResolvePlayerRuntimePosition()` reads `_playerRuntimeContext`.
- `git diff --check` passed for `ModEventProjectionBridge.cs` with line-ending warning only.
- Compile not relaunched by instruction and because the known external World source is still absent.

<SELF_AUDIT_LOOP_19>
  <TASK_RECONCILIATION>
    <TASK id="01" status="[PASS]">Removed hot `GlobalRegistry.Player` lookup from the mod projection resolver.</TASK>
    <TASK id="10" status="[PASS]">Added hot-swap rebinding for the cached player context.</TASK>
  </TASK_RECONCILIATION>
  <DEPENDENCY_INJECTION_REBINDING>Cold fill: `_playerRuntimeContext = GlobalRegistry.Player` in `Install()`. Rebind: `OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot.Player, ...)` assigns the current service as `IPlayerRuntimeContext`. Hot resolver: no GlobalRegistry call.</DEPENDENCY_INJECTION_REBINDING>
  <H_PHI_VAULT_STATUS>No new native collection was added.</H_PHI_VAULT_STATUS>
  <COMPILE_GUARD>No asmdef, sibling runtime reference, or new concrete dependency was added.</COMPILE_GUARD>
</SELF_AUDIT_LOOP_19>
