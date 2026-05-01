# EQUIPMENT_SOA_LAYOUT

Status: REFERENCE
Verification: PENDING VERIFICATION

## 2026-05-01 Current-State Boundary

- Read `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md` before treating this layout as current runtime truth.
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

Authoritative owner: `ModularEquipmentEngine`

Hot-path storage:
- `NativeArray<ToolState> _toolStates`
- `NativeArray<ToolRuntimeStats> _toolStats`
- `NativeHashMap<uint,int> _toolIndexById`

Lookup path is O(1):
1. `toolId -> slotIndex` via `_toolIndexById`
2. `slotIndex -> ToolState` via `_toolStates[slotIndex]`
3. `slotIndex -> ToolRuntimeStats` via `_toolStats[slotIndex]`

No gameplay system should read mutable tool state from MonoBehaviour fields in hot path.

## ToolState Layout

Definition:

```csharp
[StructLayout(LayoutKind.Sequential, Size = 16)]
public struct ToolState
{
    public float CurrentBattery;
    public float InternalHeat;
    public float Durability;
    public uint UpgradeBitmask;
}
```

Byte layout:
- `0-3` `CurrentBattery`
- `4-7` `InternalHeat`
- `8-11` `Durability`
- `12-15` `UpgradeBitmask`

Proof:
- `4 + 4 + 4 + 4 = 16 bytes`
- no hidden tail padding because explicit `Size = 16`

Semantics:
- `CurrentBattery`: absolute runtime charge units already scaled by compiled battery capacity
- `InternalHeat`: runtime heat scalar. Nominal operating band is `[0..1]`. Overcharge is allowed to push beyond `1.0`; `> 1.5` is catastrophic.
- `Durability`: normalized `[0..1]`
- `UpgradeBitmask`: compiled active module flags

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
- `PowerGridTelemetryEvents` publishes aggregate supply telemetry
- `ModularEquipmentEngine` caches `SupplyRatio`

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
