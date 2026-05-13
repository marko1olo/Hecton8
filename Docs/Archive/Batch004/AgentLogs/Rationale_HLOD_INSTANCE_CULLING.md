# Rationale_HLOD_INSTANCE_CULLING

Status: PENDING UNITY IMPORT / CORE IMPLEMENTED

## Decision 0 - Manual Procedural Culling Boundary

Problem: The prompt requires custom compute append-buffer culling, while project mandates prefer Unity GPU Resident Drawer for MeshRenderer-owned static environment props.
Solution: Treat this work as the procedural flora/manual BRG path only. This matches the mandate exception for generated data that never exists as stable MeshRenderer GameObjects.
Rejected Alternatives: Owning authored MeshRenderer flora through raw indirect draw would violate GPU Resident Drawer sovereignty. CPU frustum culling would preserve the current PCIe waste.
Scalability potential: Low uses shorter distance and downsample gates; Middle keeps 200m cull; High/Ultra can spend saved submission bandwidth on denser procedural flora and richer sway.
Hardware Impact: Estimated low-end i3/MX350 gain is reduced CPU submission and PCIe upload pressure. Exact microseconds remain PENDING VERIFICATION until Unity/Profiler capture.

## Decision 1 - Thread Group Floor

Problem: Compute dispatch must not assume desktop-sized groups.
Solution: Use `[numthreads(64,1,1)]` and query group size from C# before dispatch count calculation.
Rejected Alternatives: Hardcoded 256-thread dispatch would violate the MX350/Pascal floor and warp-sizing mandate.
Scalability potential: Low stays at 64. High/Ultra may add wider variants only after GPU capture.
Hardware Impact: Prevents avoidable occupancy/register pressure on MX350. Exact gain PENDING GPU CAPTURE.

## Decision 2 - Contract Boundary And Registry Bridge

Problem: Graphics culling must be globally discoverable without creating a Core <-> Graphics circular dependency or a hidden singleton.
Solution: Put `IInstanceCullingService` and payload structs in `Hecton8.World.Contracts`; register it through `GlobalRegistry.InstanceCulling`; use `InstanceCullingServiceRegistryBridge` in Core to cast a serialized `MonoBehaviour` to the contract.
Rejected Alternatives: Direct `FindObjectOfType`, static singleton, or Core referencing `Hecton8.Graphics.Culling` would be dependency rot and would break concurrent agent boundaries.
Scalability potential: Low/Middle use the same registry slot; High/Ultra can swap a richer culling implementation through the same contract without touching flora interaction code.
Hardware Impact: Removes per-frame discovery and keeps hot code cached. Estimated gain on i3/MX350 is small per call, roughly 5-15 us avoided when compared to scene lookup patterns, but the main gain is architectural containment.

## Decision 3 - GPU Count Authority

Problem: CPU readback of visible count would destroy the point of append-buffer culling by stalling the frame.
Solution: Use `GraphicsBuffer.CopyCount` to place the append count into indirect args offset 4, matching the instance-count field. Any `AsyncGPUReadback` is delayed telemetry only and not used for rendering decisions.
Rejected Alternatives: `GetData`, `AsyncGPUReadback` gating draw count, or CPU-side visible array compaction. All are too slow or too latent for frame authority.
Scalability potential: Low can still draw via indirect args with fewer visible instances; Ultra can increase procedural density without increasing CPU submission.
Hardware Impact: Avoids PCIe round trips and sync points. Estimated gain on i3/MX350 is 200-2000 us in worst-case readback-stall scenarios; exact value pending profiler.

## Decision 4 - AUP Shift As Rare Structural Work

Problem: AUP rebases invalidate matrix translations but should not force per-frame CPU matrix rebuilds.
Solution: Expose `ApplyAupShift` to lock the caller buffer and offset translations with a Burst `IJobParallelFor` only when a rebase signal is processed.
Rejected Alternatives: Applying AUP offset in the shader every dispatch costs ALU forever; rebuilding all matrices on CPU repeats source generation work. Both waste the normal case.
Scalability potential: Low/Middle pay only on rare shifts. High/Ultra can preserve denser matrices because shift cost is isolated and deterministic.
Hardware Impact: Cheap devices avoid persistent per-instance ALU tax. Estimated gain is scene-size dependent; the concrete win is zero steady-state hot-path cost.

## Decision 5 - SDF Cheat, Math LOD, VRAM Abort

Problem: Real Hi-Z occlusion and full-density flora are not acceptable on MX350-class hardware under VRAM pressure.
Solution: Use a 3D voxel SDF texture as the occlusion lie, force Low tier to 100m, and reject odd instance IDs when reported VRAM exceeds 1600MB.
Rejected Alternatives: Full Hi-Z pyramid integration, random culling, or balanced middle-ground density. Hi-Z is too much ownership for this slice; random culling is nondeterministic.
Scalability potential: Low = 100m + optional half-rate. Middle = 200m. High = 200m + SDF. Ultra = same contract can accept denser source buffers and richer occlusion later.
Hardware Impact: On i3/MX350, half-rate rejection cuts procedural instance pressure by 50% under stress. SDF cull saves overdraw in rock-heavy biomes without depth-pyramid cost.

## Decision 6 - Black Box And Overload Signal

Problem: A culling system can silently fail by drawing everything or nothing; the integrator needs fixed-size forensic data, not chat claims.
Solution: Maintain a 300-frame native ring of source/visible/culled counts, flags, hash, and shift id; dump it to `Docs/AgentLogs/Dump_HLOD_INSTANCE_CULLING.bin` on invalid state; publish `CullingOverloadSignal` above 50,000 visible instances.
Rejected Alternatives: Console logging and ad hoc debug UI allocate, spam, or vanish on crash. Direct callbacks would couple systems.
Scalability potential: Low uses overload signal to back off density; Ultra can route the same signal into visual-overkill throttles instead of hard disabling systems.
Hardware Impact: Fixed memory only. Telemetry readback is throttled every 3 frames and never gates rendering.

## Decision 7 - Verification Wall

Problem: Unity import/compute shader compile is required, but the active MCP Unity session is unavailable.
Solution: Perform source-level verification, `dotnet build` for the contracts assembly, filtered Core build checks for this task's identifiers, `git diff --check`, and mark Unity import as blocked rather than green.
Rejected Alternatives: Claiming compile success from stale generated csproj files or unrelated local C# checks would be a fake report.
Scalability potential: No runtime scalability effect; this protects integration quality.
Hardware Impact: None. Exact microsecond measurements remain pending until Unity session and profiler access are restored.

## OMEGA POLISH CHANGES

Problem: The polish mandate required an anti-bloat pass after core task closure.
Solution: Re-read the original prompt and `<POLISH_MANDATE>`, audited shader/service/bridge files for managed `foreach`, `string.Format`, interpolation, `.ToString()`, `math.sqrt`, `math.normalize`, and unconditional expensive math. The main shader visibility path was changed from chained boolean decisions into float masks using `step()` for plane, distance, and SDF pass/fail checks.
Rejected Alternatives: Leaving branch-heavy boolean accumulation in the compute shader; adding a Hi-Z depth pyramid inside this domain; using random thinning under VRAM pressure.
Scalability potential: Low uses 100m distance and deterministic half-rate VRAM thinning. Middle uses 200m. High/Ultra preserve the same contract for richer future occlusion and higher source density without CPU submission growth.
Hardware Impact: On i3/MX350, expected wins are PCIe matrix upload reduction and lower divergent branch pressure. Exact microseconds remain pending because Unity shader import/profiler capture is blocked by `no_unity_session`.

Exact cinematic cheats used:
- Voxel SDF cull replaces true Hi-Z occlusion for rock/terrain rejection.
- Low-tier 100m range replaces full-distance botanical honesty.
- VRAM >1600MB odd-instance bitmask rejection replaces complex density solving.
- Matrix spare component packing avoids an extra wind seed buffer bind.

Final Git Diff snapshot:
- `git diff --stat` for the active working tree reports deltas in `Assets/_Project/Scripts/Core/GlobalRegistry.cs`, `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs`, `Docs/AgentLogs/LOG_HLOD_INSTANCE_CULLING.md`, `Docs/AgentLogs/Rationale_HLOD_INSTANCE_CULLING.md`, and `Docs/Tasks/Status_HLOD_INSTANCE_CULLING.md`. The rest of the culling source set is present in the index/HEAD snapshot and was audited from disk: `Assets/_Project/Art/Shaders/InstanceCulling.compute`, `Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs`, `Assets/_Project/Scripts/Graphics/Culling/Hecton8.Graphics.Culling.asmdef`, `Assets/_Project/Scripts/World/Contracts/InstanceCullingContracts.cs`, `Assets/_Project/Scripts/Core/InstanceCullingServiceRegistryBridge.cs`, `Assets/_Project/Scripts/Core/GlobalSignals.cs`, `Assets/_Project/Scripts/World/FloraInteractionManager.cs`.
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false /p:BuildProjectReferences=false` remains red with 109 unrelated errors including missing `BinaryBlittableSafe`, `SoundEmissionSignal`, `AcousticAup`, and `AcousticPathResult`. Filtered output for this task's identifiers is empty.
