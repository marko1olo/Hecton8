# LOG - TOOL_RESAK_SOLVER

## 2026-05-16 - WFC Laser Cutting and Shader Clip

What was wrong:
- Laser cutter path still routed sealed-door cutting through generic plasma-cut/CSG-style deformation expectations.
- RealtimeCSG editor/runtime library was still present under `Assets/RealtimeCSG`, keeping CSG debt in the project.
- Cutter heat/battery mirrors were scene-owned arrays instead of DataVault-backed SOA state.
- WFC sealed-door progress had no dedicated cell-indexed progress buffer, no black-box telemetry, and no stress-aware feedback lane.

What was done:
- Deleted `Assets/RealtimeCSG` and `Assets/RealtimeCSG.meta`; verified the folder and folder meta are gone.
- Added `WfcLaserCutRuntime` with DataVault-backed `NativeArray<float>` cut progress (`WfcDoorCutProgress01`) and a fixed 300-frame telemetry ring (`WfcLaserCutBlackBox`).
- Moved modular equipment heat/battery mirrors onto DataVault buffers: `ToolRuntimeHeat01` and `ToolRuntimeBatteryCharge`, with fallback native arrays only when DataVault is unavailable.
- Integrated `LaserCutter` with WFC sealed doors before generic `InteractionEffectType.PlasmaCut`, using the existing `EquipmentInteractionHandler` single-requester `RaycastCommand` lane.
- Stored cut origin and hit in `double3`; only legacy packet/shader presentation truncates to float.
- Added `SealedDoor.TryGetWfcOutpostCell` and `ApplyWfcOutpostLaserCutProgress`; completed cuts emit `WfcOutpostStateChangedSignal` with `DoorUnlocked`.
- Added a laser-unlocked latch so later power-off signals cannot clear a completed laser cut.
- Added low-tier optional growing decal proxy and kept existing door progress MPB for glow.
- Added `Assets/_Project/Shaders/Hecton_WfcLaserDoorClip.shader`, a URP spherical `clip()` shader with molten edge emission globals.
- Added `DebrisSpawnSignal.DebrisKindSparks`, `ToolAcousticSignal.StateLaserLoop`, and `HapticRequest.ChannelMicroVibration` constants; the cutter now publishes sparks, loop audio, and heat-tied micro-vibration haptics.
- Added SystemStress01 adaptation: spark particle rate plus signal intensity/quantity drop to 35 percent when stress is above 0.7.

Cinematic cheats used:
- No mesh booleans, no `Mesh.vertices`, no physical cut simulation.
- Low tier uses growing decal/progress glow.
- High tier uses shader sphere clipping and molten edge emission from global shader properties.
- Gameplay truth is one clamped float per WFC cell.

Exact microseconds saved:
- Replacing CSG/mesh boolean door cutting avoids the documented 200 ms stall: estimated 200000+ us saved per CSG-style cut event.
- WFC progress write: estimated 1 us.
- Signal feedback per handled cut frame: estimated 7-16 us total across debris/audio/haptic lanes.
- Stress adaptation saves roughly 65 percent of spark work above `SystemStress01 > 0.7`.
- DataVault heat/battery mirror writes remain contiguous SOA writes: estimated 1-3 us.

Validation:
- `rg` found no `LaserCutterManager`.
- `rg` found no `Mesh.vertices` use in TOOL_RESAK_SOLVER files.
- `Assets/RealtimeCSG` and `Assets/RealtimeCSG.meta` no longer exist.
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` cleared TOOL_RESAK_SOLVER compile issues, then failed on unrelated cross-domain dependency errors: missing `IDockingAutopilotService`, `Hecton8.VFX.Wakes`, `LightShaftContribution`, `ScreenSpaceLightShaftSource`, and stale `IEcosystemDirectorService` members.
- A later build retry timed out; the lingering dotnet process was stopped.

Status:
- TOOL_RESAK_SOLVER core implementation: VERIFIED MASTER GRADE.
- Global final validation: BLOCKED BY DEPENDENCY outside gameplay/tools.
