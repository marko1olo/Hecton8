# 2026-04-29 Logic Spanner / Overcharge Surgery Log
Date: 2026-05-07
Status: PENDING VERIFICATION
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## R4 Interior Actuality Boundary

This document is active only as static documentation/source orientation. Current authority is `AGENTS.md`, `.agents-skills`, `Docs/Actual Domains of Project.txt`, current source files, current verification artifacts, and the latest DOC_GLOBAL reports.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Mandates followed:
- `CORE_Tools_Equipment_Interaction_Raycast_Heat.txt`
- `DATA_Inventory_Resources_Items_SOA_Layout.txt`
- `CTRL_Device_Abstraction_Haptics.txt`
- `LOGI_Energy_Networks_Power_Grid_Graph_Flow.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `PHYS_Physics_Integrity_Determinism_ForceMode.txt`

## Scope

- Added `LogicSpannerTool` for diegetic habitat bypass wiring.
- Added `ToolMetadata_LogicSpanner.asset` as the authored metadata template for `tool_logic_spanner`.
- Extended `HabitatGraphManager` to append temporary bypass edges into the cold-path topology source before CSR publish.
- Kept the existing `InputDispatcher` motor bridge and corrected `ToolHapticsRuntime` decay to the requested exponential form.
- Added tool overcharge handling in `ModularEquipmentEngine` using `PrimaryFire + Sprint` as the held modifier chord.

## Haptic Queue Integration

Physical bridge:
- `ToolHapticsRuntime` owns the bounded double buffer.
- `InputDispatcher.DrainToolHaptics()` reads the front buffer once per frame.
- `LowFreqIntensity` is routed to the left motor when `MotorMask & 0b0001`.
- `HighFreqIntensity` is routed to the right motor when `MotorMask & 0b0010`.
- `Gamepad.SetMotorSpeeds(lowMotor, highMotor)` publishes the mixed output.

Decay math now used in the queue:

```csharp
command.DurationRemaining = math.max(0f, command.DurationRemaining - deltaTime);
float decayFactor = math.exp(-math.max(0f, command.DecayRate) * math.max(0f, deltaTime));
command.LowFreqIntensity = math.saturate(command.LowFreqIntensity * decayFactor);
command.HighFreqIntensity = math.saturate(command.HighFreqIntensity * decayFactor);
```

Dispatcher motor mapping:

```csharp
float lowContribution = (command.MotorMask & 0b0001) != 0
    ? math.saturate(command.LowFreqIntensity)
    : 0f;
float highContribution = (command.MotorMask & 0b0010) != 0
    ? math.saturate(command.HighFreqIntensity)
    : 0f;

_cachedGamepad.SetMotorSpeeds(lowMotor, highMotor);
```

## Logic Spanner Path

Tool ID:
- `tool_logic_spanner`

Runtime behavior:
1. Primary fire on module A arms a source node.
2. Primary fire on module B requests `ConstructionManager.TryCreateTemporaryBypass(...)`.
3. `HabitatGraphManager.TryAddTemporaryBypass(...)` records the module-node pair by stable node ID.
4. `Rebuild(...)` resolves those node IDs back to module indices and appends an undirected bypass edge into `_edgeBuffer`.
5. `PublishGraphKernel()` forwards that edge into `LogisticsNetworkGraph.AddEdge(...)`, preserving CSR-only hot-path traversal.

## Overcharge Path

Input contract:
- held overcharge request = `PrimaryFire + Sprint`

Runtime math:

```csharp
float heatGrowth = math.exp(math.max(0f, state.InternalHeat) * OverchargeHeatExponent);
state.InternalHeat = math.max(
    0f,
    state.InternalHeat + (runtimeHeatRate * OverchargeHeatScale * heatGrowth * deltaTime));
```

Failure rule:
- `GetPowerScalar(...)` returns `compiledPower * 3.0f` while overcharge is requested.
- if `InternalHeat > 1.5f`, the runtime:
  - removes one matching tool item from `PlayerInventory`
  - applies direct damage to `HectonPlayerHealth`
  - clears the active modular runtime slot

## Validation Facts

Historical targeted validator readback reported `0` errors for `PlayerTool.cs`, `ModularEquipmentEngine.cs`, `Construction/HabitatGraphManager.cs`, `ConstructionManager.cs`, and `LogicSpannerTool.cs`, plus one warning-level heuristic for `ToolHapticsRuntime.cs`. Artifact link is absent; current compile proof remains `PENDING VERIFICATION`.

Unity console:
- not clean
- unrelated `PhysicsApplySystem.cs` compile errors remain active

Status:

- `PENDING VERIFICATION`
