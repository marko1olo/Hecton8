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
