# LOG_HLOD_INSTANCE_CULLING

## 2026-05-13 - Session Start

What was wrong: Prompt identified a CPU-side 100,000-instance submission bottleneck for procedural flora/BRG-style instance data.
What was done: Prompt and mandates extracted. Status and rationale logs initialized.
Cinematic Cheats used: Manual compute culling is scoped to procedural flora; authored MeshRenderer flora remains eligible for Unity GPU Resident Drawer.
Exact Microseconds saved: PENDING VERIFICATION. No profiler capture yet.

## 2026-05-13 - Compute Culling Implementation

What was wrong:
- Procedural flora/manual-BRG data path could submit source instance matrices without a dedicated contract-owned compute culling service.
- CPU ownership of visible count would risk PCIe/readback stalls and duplicate culling authority.
- Cross-domain access needed a registry contract, not singleton discovery or Core-to-Graphics implementation coupling.

What was done:
- Implemented/verified `IInstanceCullingService` contract, `InstanceCullingService`, `InstanceCulling.compute`, `Hecton8.Graphics.Culling.asmdef`, and `InstanceCullingServiceRegistryBridge`.
- Registered `InstanceCullingRuntime` in `GlobalRegistry` and exposed `GlobalRegistry.InstanceCulling`.
- Added native camera position/frustum signal payloads and culling dispatch descriptors.
- Wrote compute frustum/distance/SDF culling into an append visible-matrix buffer.
- Used `GraphicsBuffer.CopyCount` to place visible count directly into indirect args.
- Added rare AUP matrix shift path using Burst `IJobParallelFor`.
- Added delayed telemetry readback, 300-frame black-box ring, invalid-state dump to `Docs/AgentLogs/Dump_HLOD_INSTANCE_CULLING.bin`, and `CullingOverloadSignal` above 50,000 visible instances.
- Audited `FloraInteractionManager` handoff so vertex sway can consume the culled visible buffer through the registry contract.
- Ran OMEGA polish: flattened main compute visibility decision into `step()` masks; confirmed no managed `foreach`, `string.Format`, interpolation, `.ToString()`, `math.sqrt`, or `math.normalize` in the new culling slice.

Cinematic Cheats used:
- Voxel SDF texture replaces full Hi-Z occlusion for rock/terrain rejection.
- Low tier clamps distance to 100m; standard path clamps to 200m.
- VRAM >1600MB rejects odd instance IDs deterministically instead of expensive density solving.
- Matrix spare component packing preserves sway/radius scalar without an extra buffer bind.

Exact Microseconds saved:
- CPU submission/PCIe matrix compaction avoided: estimated 180-450 us at 100k instances, pending Unity profiler capture.
- CPU frustum loop avoided: estimated 90-220 us at 100k instances, pending profiler capture.
- CPU readback stall avoided by `CopyCount`: estimated 200-2000 us worst-case stall risk removed.
- Registry lookup/direct dependency churn avoided: estimated 5-15 us versus scene lookup patterns.
- Confirmed measured microseconds: PENDING. Unity MCP reports `no_unity_session`; shader import/profiler capture could not be executed.

Verification:
- PASS: `dotnet build Hecton8.World.Contracts.csproj --no-restore -m:2 /nr:false` -> 0 warnings, 0 errors.
- PASS: Filtered `Hecton8.Core.csproj` build output contains no `InstanceCulling`, `CullingOverloadSignal`, or culled-flora errors.
- RED GLOBAL: Full `Hecton8.Core.csproj` build fails with 109 unrelated errors from other domains (`BinaryBlittableSafe`, `SoundEmissionSignal`, `AcousticAup`, `AcousticPathResult`).
- PASS: `git diff --check` on touched files reports line-ending warnings only.
- BLOCKED: Unity `refresh_unity`, `validate_script`, `read_console`, and `unity_reflect` are blocked by missing Unity session.

Status: PENDING UNITY IMPORT / CORE IMPLEMENTED.
