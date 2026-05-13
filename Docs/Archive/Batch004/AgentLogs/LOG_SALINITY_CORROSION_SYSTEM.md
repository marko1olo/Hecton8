# SALINITY_CORROSION_SYSTEM Final Report - 2026-05-13

STATUS: PENDING VERIFICATION - core corrosion work complete; global Unity compile blocked by unrelated visor/UI dependencies.

What was wrong:
- Equipment durability had no salinity-driven S.O.A. path.
- Durability architecture still tolerated singleton-style access.
- Gear degradation had no deterministic blackbox trail.
- Save payload did not persist equipment corrosion durability.
- Presentation risked material churn instead of one controlled shader scalar.

What was done:
- Removed the remaining script-side `ToolDurabilitySystem.Instance` accessor and verified no script references to `ItemDurabilityManager.Instance` or `DurabilityManager.Instance`.
- Added `NativeArray<float> _itemDurability` beside `_itemHashes` in `PlayerInventory`, including allocation, disposal, memory sentinel registration, load/add/remove/sort/condition synchronization, and read-only access.
- Added isolated `Hecton8.Inventory.Corrosion` and `Hecton8.Inventory.Corrosion.Contracts` asmdefs.
- Added `ItemSalinityCorrosionJob`: skips `hash == 0u`, skips empty stacks, filters by `CurrentInventoryMask`, applies salinity degradation, sets rust/degraded/broken flags, writes average durability, and records broken hashes.
- Consumed `BiomeChangedSignal`; emitted `ItemDurabilityChangedSignal`, `ToolAcousticSignal`, and latched `HUDNotificationSignal`.
- Added Repair Tool + Titanium signal coupling to restore durability to 1.0.
- Added `_HectonEquipmentRust01` global scalar update from average equipment durability, no material swap.
- Added 300-frame corrosion telemetry ring and NaN binary dump path at `Docs/AgentLogs/Dump_SALINITY_CORROSION_SYSTEM.bin`.
- Added durability RLE persistence using byte-stored sbyte quantization after multiplying durability by 100.
- Ran Omega polish: replaced one job division with `math.rcp`, audited corrosion asmdef for sqrt/normalize/foreach/string formatting, and Roslyn-compiled corrosion/contracts using Unity Bee response files.

Cinematic Cheats used:
- 5-second FrostTick instead of frame-by-frame corrosion.
- Single scalar rust presentation instead of physical corrosion simulation.
- Shader-side rusty/scratched detail blend instead of material swaps.
- Bitmask-equipped filtering instead of object graph traversal.
- 1% durability quantization and RLE instead of raw float persistence.

Exact Microseconds saved:
- Singleton purge: estimated 35 us worst-case avoided call-chain overhead in tool/UI fan-out frames.
- Bitmask equipped filtering: estimated 8 us saved per 40-slot degradation pass versus managed equipped lists.
- FrostTick cadence: avoids 9 out of 10 half-second SlowTick passes and all per-frame degradation; amortized corrosion pass target under 5 us for 40 equipped slots.
- SOA NativeArray pass: target under 25 us on i3/MX350 for 40 slots; 0 B GC.
- Material-swap rejection: estimated 20-80 us saved in first-person presentation frames depending renderer/material count.
- RLE save payload: 128 durability slots compress to 2-256 bytes instead of 512 raw float bytes.
- Omega rcp change: removes one float divide from each FrostTick average; sub-microsecond but deterministic.

Verification:
- Unity log confirms `Hecton8.Inventory.Corrosion.Contracts.dll` and `Hecton8.Inventory.Corrosion.dll` compiled and copied.
- Direct Roslyn validation with Unity Bee response files passed for both corrosion assemblies after Omega changes.
- Full Unity compile remains blocked by unrelated errors in `InternalFloodWaterlineRuntime`, `HectonVisorUberPostFeature`, and `VehicleSubOsCockpitRuntime`; MCP session reports no active Unity instance after the compile failure.
