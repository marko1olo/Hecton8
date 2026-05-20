# EQUIPMENT_SOA_LAYOUT
Date: 2026-05-07

Status: PENDING VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.

R45 root/architecture R43/R44 residue/proof-artifact/source-counter correction (`Docs/Reports/2026-05-20_DOCUMENTATION_R45_ROOT_ARCHITECTURE_R43_R44_RESIDUE_PROOF_ARTIFACTS_AND_COUNTERS_LOCAL.md`) is the latest local static DOC_GLOBAL boundary for architecture/root documentation. R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; R42 remains the prior counter/route-boundary/proof-label correction; R41 remains the prior global-authority/internal-residue correction; R40 remains the prior R38-residue/source-counter correction; R39 remains the prior authority-counter/proof-wording correction; R38/R37/R36/R35/R34 remain prior static correction layers. Runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Source Anchors

Evidence class: STATIC_SOURCE / FILESYSTEM path check. These anchors prove current path visibility only, not tool prefab wiring, input runtime, haptics, save/load, profiler, or player-build proof.

- `Assets/_Project/Scripts/ModularEquipmentEngine.cs`
- `Assets/_Project/Scripts/Tools/ToolMetadata.cs`
- `Assets/_Project/Scripts/Tools/EquipmentThermalBatteryContracts.cs`
- `Assets/_Project/Scripts/Interaction/EquipmentInteractionHandler.cs`
- `Assets/_Project/Scripts/Interaction/EquipmentInteractionContracts.cs`
- `Assets/_Project/Scripts/Tools/ToolKinematics/ToolKinematicsRuntime.cs`
- `Assets/_Project/Scripts/Tools/ToolKinematics/Contracts/ToolKinematicsContracts.cs`
- `Assets/_Project/Data/Tools`

Verification: PENDING VERIFICATION

## 2026-05-20 SHINOBU_224 Static Refresh

Static source update only; Unity import, Play Mode, profiler, GCMonitor, and player-build proof remain pending.

Current active-equipment additions:
- `ActiveEquipmentDTO` remains explicit 32 bytes at offsets `0/4/8/12/16/20/24-31`; it is still the rollback/UI snapshot ABI.
- Active tool battery, heat, and active-use wear now integrate in `ModularEquipmentEngine.EquipmentStateIntegrationJob`.
- Wear rate is not stored in DTO padding. It is a separate Vault stream: `BufferID.ShinobuActiveEquipmentWearDrainRates = 71316`, `NativeArray<float> _activeEquipmentWearDrainRates`.
- Service readiness fails closed unless the wear-rate stream exists; there is no private `NativeArray` fallback for active equipment battery/heat/wear truth.
- Cold buffer acquisition now uses `IDataVault.GetGenerationHandle<T>` plus `TryResolveHandle`; SHINOBU_224 no longer asks the Vault for direct `GetBuffer<T>` external views.
- Equipped-tool AUP sampling uses `IPlayerRuntimeContext.TryGetPlayerPoseSnapshot`; cached tool transform sampling is fallback-only for detached/non-equipped registered tools.
- Hardware-spec tuning source is now present at `Assets/_Project/Data/Tools/tool_hardware_specs.csv`; cold ingest uses `ReadOnlySpan<byte>` into `ShinobuActiveEquipmentHardwareSpecs`. Parser rows can use a numeric/hex runtime hash or the lower-case FNV spec hash. Runtime matching checks `PlayerTool.RuntimeToolId` first and cached `PlayerTool.RuntimeToolSpecHashId` second, so name-keyed CSV rows are no longer inert against `Animator.StringToHash` tool IDs.
- Post-simulation readback remains a blind `UnsafeUtility.MemCpy` from `_activeEquipmentStates` to `_publishedActiveEquipmentStates`.
- Overheat and depleted signals are written by the Burst integration job directly to typed `SignalBus<EquipmentOverheatSignal>` and `SignalBus<ToolDepletedSignal>` parallel writers; the equipment domain no longer owns private overheat/depletion `NativeQueue` buffers or a post-fence queue-drain loop.
- Wireless/tool brownout feedback no longer subscribes to `Hecton8.Power` telemetry events. `ModularEquipmentEngine` reads cached Core `IPowerGridService` aggregate generation/consumption/battery snapshot scalars and converts them to the same flicker signal locally.
- Fault telemetry dumps to `Docs/AgentLogs/Dump_SHINOBU_224.bin`.

Current Vault active-equipment buffer IDs:
- `71300` active DTO writer
- `71301` published DTO readback
- `71302` AUP samples
- `71303` grid load requests
- `71304` telemetry ring
- `71305` telemetry cursor
- `71306` integration counters
- `71308` tuning DTO
- `71309` hardware spec DTOs
- `71311-71315` tool state/stat/type/status/environment mirrors
- `71316` wear drain rates

## 2026-05-11 Historical Override + 2026-05-17 Actuality Pointer

- Historical data boundary snapshot: `Docs/Reports/2026-05-11_DOCUMENTATION_CURRENT_DATA_CONTINUATION.md`.
- Historical manifest: `Docs/Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json`.
- Historical actuality manifest: `Docs/Reports/2026-05-17_ACTIVE_DOCUMENTATION_ACTUALITY_MANIFEST.json` (historical snapshot only; do not use for current counts or proof).
- Current actuality ledger: `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`.
- Visual-realistic-fake doctrine snapshot: `Docs/Reports/2026-05-11_AGENTS_SKILLS_VISUAL_FAKE_AUDIT.md`; re-check `.agents-skills` for newer mandates before implementation.
- Historical May 14/R43 CLI compile wording is stale report text, not current proof. Current static/tool boundary is R45 root/architecture R43/R44 residue/proof-artifact/source-counter correction (`Docs/Reports/2026-05-20_DOCUMENTATION_R45_ROOT_ARCHITECTURE_R43_R44_RESIDUE_PROOF_ARTIFACTS_AND_COUNTERS_LOCAL.md`) (R44 prior internal-residue/exact-route-field/proof-wording correction); R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; R42 remains the prior counter/route-boundary/proof-label correction; R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers; AtlasCheck fails `ATLAS_CHECK_FAIL references=6741 missing=59` (one Dynamic Decals missing vendor asset ref, RealtimeCSG vendor icon/readme image refs, and missing HabitatDamageBakePipeline source ref in the current atlas); Mod API static validation passes (`Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`) as static-tool orientation only; do not treat PASS as current proof without artifact path, command, timestamp, environment, and output. Unity import, Console, Play Mode, profiler, GCMonitor, player build, scene wiring, save/load, and visual proof remain PENDING VERIFICATION.
- Existing May 4 boundary sections in this file are historical unless they describe local system intent not contradicted by newer reports.
- Unity import, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, scene wiring, and visual quality remain `PENDING VERIFICATION`.
## Historical 2026-05-04 Boundary

- Read `Docs/Reports/2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md`, `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`, `Docs/Reports/2026-05-04_WARNING_CLEANUP.md`, `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md`, and `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md` before treating this layout as current runtime truth.
- This document is a source-backed equipment SOA contract, not proof that every tool prefab, save path, UI readout, or haptics path is currently wired.
- Re-open `ModularEquipmentEngine`, `EquipmentInteractionHandler`, and current tool assets before surgery.

Mandates followed:
- `CORE_Tools_Equipment_Interaction_Raycast_Heat.txt`
- `DATA_Inventory_Resources_Items_SOA_Layout.txt`
- `CTRL_Device_Abstraction_Haptics.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `PHYS_Physics_Integrity_Determinism_ForceMode.txt`
- `VOX_Voxel_World_Logic_Carving_Persistence.txt`
- `LOGI_Energy_Networks_Power_Grid_Graph_Flow.txt`

## Runtime Ownership

Historical note: this section predates the 2026-05-20 SHINOBU_224 static refresh. Treat `_toolIndexById` and O(1) hash-map lookup below as old intent unless current source still contains that field. Current SHINOBU_224 source uses fixed 16-slot owner mirrors plus Vault-backed SoA streams for active local equipment.

Authoritative owner: `ModularEquipmentEngine`

Hot-path storage:
- `NativeArray<ToolState> _toolStates`
- `NativeArray<ToolRuntimeStats> _toolStats`
- `NativeArray<byte> _toolTypes`
- `NativeArray<float> _currentHeat`
- `NativeArray<float> _batteryCharge`
- `NativeArray<uint> _statusMasks`
- `NativeHashMap<uint,int> _toolIndexById`

Lookup path is O(1):
1. `toolId -> slotIndex` via `_toolIndexById`
2. `slotIndex -> ToolState` via `_toolStates[slotIndex]`
3. `slotIndex -> ToolRuntimeStats` via `_toolStats[slotIndex]`

No gameplay system should read mutable tool state from MonoBehaviour fields in hot path.

## ToolState Layout

Definition:

```csharp
[StructLayout(LayoutKind.Sequential, Size = 32)]
public struct ToolState
{
    public float CurrentBattery;
    public float InternalHeat;
    public float Durability;
    public uint UpgradeBitmask;
    public uint StatusMask;
    public byte ToolTypeId;
    public byte ModuleSlotCount;
    public ushort Reserved0;
    public ulong Reserved1;
}
```

Byte layout:
- `0-3` `CurrentBattery`
- `4-7` `InternalHeat`
- `8-11` `Durability`
- `12-15` `UpgradeBitmask`
- `16-19` `StatusMask`
- `20` `ToolTypeId`
- `21` `ModuleSlotCount`
- `22-23` `Reserved0`
- `24-31` `Reserved1`

Proof:
- `4 + 4 + 4 + 4 + 4 + 1 + 1 + 2 + 8 = 32 bytes`
- no hidden tail padding because explicit `Size = 32`

Semantics:
- `CurrentBattery`: absolute runtime charge units already scaled by compiled battery capacity
- `InternalHeat`: runtime heat scalar. Nominal operating band is `[0..1]`. Overcharge is allowed to push beyond `1.0`; `> 1.5` is catastrophic.
- `Durability`: normalized `[0..1]`
- `UpgradeBitmask`: compiled active module flags
- `StatusMask`: runtime disabled/low-power/overheat/broken/depth-failure flags
- `ToolTypeId`: byte SOA tool type derived from the stable runtime hash ID
- `ModuleSlotCount`: active hardware slots mirrored for hot-path consumers

## Upgrade Bitmask Legend

`ToolUpgradeBits : uint`

- Bit `0` `0x00000001` = `RangeBoost`
- Bit `1` `0x00000002` = `EfficiencyPlus`
- Bit `2` `0x00000004` = `ThermalOverclock`
- Bit `3` `0x00000008` = `WirelessCharging`
- Bit `4` `0x00000010` = `HighCapacityCell`
- Bit `5` `0x00000020` = `CoolingSink`
- Bit `6` `0x00000040` = `KineticAccelerator`
- Bit `7` `0x00000080` = `StandardBattery`
- Bit `8` `0x00000100` = `ThermalShield`
- Bit `9` `0x00000200` = `DepthHardened`
- Bit `10` `0x00000400` = `OxygenRebreather`

## Runtime Status Mask Legend

`ToolRuntimeStatusMasks : uint`

- Bit `0` `0x00000001` = `Active`
- Bit `1` `0x00000002` = `Disabled`
- Bit `2` `0x00000004` = `LowPower`
- Bit `3` `0x00000008` = `Overheated`
- Bit `4` `0x00000010` = `Broken`
- Bit `5` `0x00000020` = `DepthFailed`
- Bit `6` `0x00000040` = `HeatWarningHapticQueued`

Composite modules are allowed. Example:
- `ToolModule_HighCapCell.asset` currently compiles `HighCapacityCell | WirelessCharging`
- `ToolModule_KineticAccelerator.asset` currently compiles `KineticAccelerator | ThermalOverclock`

## Known Tool IDs

- `tool_flashlight`
- `tool_harpoon_launcher`
- `tool_laser_cutter`
- `tool_repair`
- `tool_logic_spanner`

## Logic Spanner Runtime States

Tool ID:
- `tool_logic_spanner`

Operational states:
- `Idle`: no source node armed
- `SourceArmed`: node A captured, waiting for node B
- `LinkCommitted`: temporary bypass edge inserted, tool ready to arm a new source

Graph effect:
- node A + node B are resolved to habitat node IDs
- `HabitatGraphManager` stores the pair in a temporary bypass list
- the next `Rebuild(...)` appends that bypass into `_edgeBuffer`
- `PublishGraphKernel()` forwards the bypass into `LogisticsNetworkGraph.AddEdge(...)`

This means runtime traversal still reads CSR only. The Logic Spanner mutates the cold-path topology source, not the hot-path traversal contract.

## Branchless Upgrade Math

Cold-path recompute happens on module install/remove. Hot path never branches on module authoring data.

Reference kernel:

```csharp
float enabled = math.select(0f, 1f, (upgradeMask & (uint)bit) != 0u);
float actualRate = baseRate * (1f + bonus * enabled);
```

Meaning:
- bit absent -> `enabled = 0` -> multiplier `1`
- bit present -> `enabled = 1` -> multiplier `1 + bonus`

This is the required branchless form for upgrade-conditioned scalar math.

Applied example for `CoolingSink`:

```csharp
stats.CooldownRate = ApplyBitBonus(
    stats.CooldownRate,
    mask,
    ToolUpgradeBits.CoolingSink,
    coolingSinkBonus);
```

## Battery Drain Atomic Path

`CurrentBattery` is mutated through an atomic compare-exchange loop against the native array slot. That keeps battery drain deterministic if multiple systems request drain on the same tool in one frame.

Reference flow:

```csharp
int observedBits = Volatile.Read(ref batteryBits);
while (true)
{
    float currentBattery = math.max(0f, math.asfloat(observedBits));
    float nextBattery = math.max(0f, currentBattery - absoluteBatteryDelta);
    int nextBits = math.asint(nextBattery);
    int originalBits = Interlocked.CompareExchange(ref batteryBits, nextBits, observedBits);
    if (originalBits == observedBits)
        break;

    observedBits = originalBits;
}
```

Read path:
- `TryGetToolState(toolId, out state)` patches `state.CurrentBattery` from the atomic slot before returning
- `GetBatteryNormalized(toolId, fallback)` reads the same atomic slot and divides by compiled capacity

Write path:
- `SetBattery(toolId, normalizedBattery)` converts normalized charge to absolute units and uses `Interlocked.Exchange`
- `ConsumeBattery(toolId, normalizedDelta)` uses compare-exchange against the slot-local float bits

## Haptic Queue Contract

Owner: `ToolHapticsRuntime`

Storage:
- front `NativeArray<HapticCommand>[16]`
- back `NativeArray<HapticCommand>[16]`

Write rule:
- gameplay writes only to back buffer this frame

Read rule:
- `Tick(dt)` compacts the front buffer, decrements `DurationRemaining`, and applies `DecayRate`
- `LateFrameTick()` appends this frame's back-buffer commands into remaining front-buffer capacity
- input/device layer reads only the front buffer after the late-frame merge

No resize. No managed queue. No per-frame allocation.

Reference integration:

```csharp
protected void QueueToolHapticFeedback(float powerDelivered, float ratedPower, byte priority = 1)
{
    ToolHapticsRuntime.EnqueueToolFeedback(powerDelivered, ratedPower, priority);
}
```

The queue normalizes amplitude with:

```csharp
float normalizedPower = ratedPower > 0.0001f
    ? math.saturate(powerDelivered / ratedPower)
    : 0f;
```

Runtime command defaults:
- `HighFreqIntensity = normalizedPower`
- `DecayRate = 1.5f`
- `MotorMask = 0b0010`
- decay law = `amplitude *= exp(-dt * DecayRate)`

Motor mapping in `InputDispatcher`:
- `LowFreqIntensity` -> left motor when `MotorMask & 0b0001`
- `HighFreqIntensity` -> right motor when `MotorMask & 0b0010`
- front-buffer mix is applied through `Gamepad.SetMotorSpeeds(lowMotor, highMotor)`

## Overcharge Path

Input contract:
- overcharge request = `PrimaryFire + Sprint` while the tool is equipped

Runtime effect:
- `GetPowerScalar(...)` returns `compiledPower * 3.0` while overcharge is requested
- `Tick(dt)` grows `InternalHeat` exponentially:

```csharp
float heatGrowth = math.exp(math.max(0f, state.InternalHeat) * 1.35f);
state.InternalHeat += runtimeHeatRate * 1.75f * heatGrowth * dt;
```

Failure threshold:
- if `InternalHeat > 1.5f`
- runtime removes one inventory instance of the tool
- runtime damages `HectonPlayerHealth`
- runtime clears the active modular slot

## Brownout Response

Wireless-tool brownout is runtime-only and does not mutate authored module data.

Owner:
- `ModularEquipmentEngine` reads cached Core `IPowerGridService` scalars
  (`TotalGeneration`, `TotalConsumption`, `BatterySnapshot`) during its dispatcher tick
- no `Hecton8.Power` telemetry listener or sibling runtime event subscription is part of
  the equipment runtime boundary

Rule:
- if `SupplyRatio < 0.40`
- and the tool has `WirelessCharging`
- then `EfficiencyScalar *= 0.5f`

Visual response:
- tools with indicator renderers query the modular runtime for brownout flicker
- emission intensity is modulated through `MaterialPropertyBlock`

## Voxel Weld Delta

Repair-tool voxel welding now persists additive cells as flagged modified entries instead of pretending every delta is subtractive.

Stored per dirty cell:
- signed density payload
- material id
- mode flag: subtractive or additive

Rebuild rule:
- subtractive cells merge with `math.min`
- additive cells merge with `math.max`

Save compatibility:
- older dense voxel saves without `cellFlags` load as subtractive-only data
