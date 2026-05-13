# RECON_CORE_JOB_ADMISSION_SCHEDULER

Status: PENDING VERIFICATION
Scan command: `rg -n "\.Schedule\s*\(" Assets/_Project/Scripts -g "*.cs"`

## Summary

- Total naked `.Schedule()` call sites under `Assets/_Project/Scripts`: 266.
- Core systems sampled for immediate risk: voxel, world generation, physics-adjacent, core dispatcher.
- `Hecton8.Core.Scheduling.dll` compiled in Unity Bee; downstream `Hecton8.Core.dll` is blocked by unrelated shared compile errors.

## Immediate Hotspots

- `Assets/_Project/Scripts/HectonVoxelEngine.cs`: many meshing and density jobs remain naked; physics bake scheduling is now gated through `TryScheduleVoxelPhysicsBake` and delays one frame when `Lane2_Voxel` is starved.
- `Assets/_Project/Scripts/HectonWorldGenerator.cs`: terrain vertex/normal/color jobs and terrain physics bake jobs remain naked; should be next migration target for `Lane2_Voxel`.
- `Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs`: importance scoring remains naked; should migrate to `Lane4_VFX` or `Lane3_AI` depending on final ownership.
- `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs`: residency scan/sort now use `Lane1_World` admission and report EWMA completion.

## Representative Results

- `HectonWorldGenerator.cs:1381` vertex job naked schedule.
- `HectonWorldGenerator.cs:1787` terrain collider bake naked schedule.
- `HectonVoxelEngine.cs:6158` density/marching-cubes pipeline naked schedule.
- `HectonVoxelEngine.cs:8238` collider triangle extraction naked schedule.
- `Core/FoveatedSimulationManager.cs:741` importance scoring naked schedule.

## Integrator Notes

- Do not mass-rewrite all 266 sites in one pass. Route by owner domain and lane.
- Mandatory first wave after this task: terrain generation and foveated manager, because they can saturate worker threads on i3/MX350.
- Keep fail-open behavior only before bootstrap service exists. After `GameBootstrapper` registers `IJobAdmissionService`, systems must degrade or defer.
