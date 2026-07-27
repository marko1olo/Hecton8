## 2026-07-27T02:25:52Z
<USER_REQUEST>
You are dispatched as teamwork_preview_worker for Milestone 6: SDF Sampling Determinism & Capacity Protection Implementation.
Working Directory for metadata: C:\hades\Hecton8\.agents\worker_m6_voxel

MANDATORY HECTON-8 INTAKE DIRECTIVE:
1. You MUST load and follow authority files: C:\hades\Hecton8\AGENTS.md, C:\hades\Hecton8\GEMINI.md, C:\hades\Hecton8\voxels.md, and C:\hades\Hecton8\terrain.md.
2. PROOF OF READING: Your first output/plan MUST start with a direct quote or explicit reference to a key constraint, constant (e.g., `GlobalQualityWeight`, `used/total > 0.90`), or API check from voxels.md or AGENTS.md. (e.g., quote voxels.md line 98: "GlobalQualityWeight may scale SDF resolution, extraction distance, diagnostic depth, and rebuild cadence, but it must not change collision truth, carve permission, or save delta identity.").

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A Forensic Auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

Primary Input:
Read `C:\hades\Hecton8\.agents\explorer_m5\handoff.md` for exact code diffs and implementation guidelines.

Tasks to Execute:
1. Edit `Assets/_Project/Scripts/World/HectonAnomalySdfJobs.cs`:
   - Add `public double3 OriginAup;` to `VoxelCliffOverhangNoiseJob`.
   - In `Execute(int index)`, compute `double3 worldPosAup = OriginAup + (double3)(gridPos * VoxelSizeMeters);` and `float3 noisePos = (float3)worldPosAup * NoiseFrequency;`.

2. Edit `Assets/_Project/Scripts/World/HectonAnomalyEngine.cs`:
   - Add `double3 originAup = default` parameter to `ApplyVoxelCliffOverhangNoise`.
   - Assign `OriginAup = IsFiniteAup(originAup) ? originAup : double3.zero` on `VoxelCliffOverhangNoiseJob`. Check if `IsFiniteAup` exists in `HectonAnomalyEngine.cs` (or use `math.all(math.isfinite(originAup))`).

3. Edit `Assets/_Project/Scripts/HectonVoxelEngine.cs`:
   - At line 10823, update `ApplyVoxelCliffOverhangNoise` invocation to pass `sdfOriginAup`.
   - In `ResolveStreamingMeshRawScratchCapacity(int totalCellCount)` (around line 12252), fix capacity logic so `desired` allocation is NEVER truncated by `qualityCapacity` below `desired`. Compute:
     ```csharp
     long desired = (long)math.max(1, totalCellCount) * MC_BUFFER_MULTIPLIER;
     int qualityCapacity = ResolveStreamingMeshRawScratchQualityCapacity();
     long capacity = math.max(desired, (long)qualityCapacity);
     capacity = math.min(capacity, (long)StreamingMeshRawVertexScratchVisualOverkillCapacity);
     return capacity < 1L ? 1 : (int)capacity;
     ```

4. Edit `Assets/_Project/Scripts/Editor/AnomalyTestHarness.cs`:
   - At line 926, update `ApplyVoxelCliffOverhangNoise` invocation to pass `double3.zero`.

5. Compilation & Test Verification:
   - Execute build / test commands via `run_command` (e.g., `dotnet build Assets/_Project/Scripts/Hecton8.Core.csproj` or `dotnet build` if available, or verify clean compile).
   - Document build results and test execution logs in `handoff.md`.

Output Requirements:
- Write `changes.md` and `handoff.md` under `C:\hades\Hecton8\.agents\worker_m6_voxel\`.
- Report completion and handoff path back to Orchestrator via send_message.

</USER_REQUEST>
