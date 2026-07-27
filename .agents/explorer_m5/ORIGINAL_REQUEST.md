## 2026-07-27T02:23:07Z
<USER_REQUEST>
You are dispatched as teamwork_preview_explorer for Milestone 5: Voxel SDF Sampling Reconnaissance & Root Cause Audit.
Working Directory for metadata: C:\hades\Hecton8\.agents\explorer_m5

MANDATORY HECTON-8 INTAKE DIRECTIVE:
1. You MUST load and follow authority files: C:\hades\Hecton8\AGENTS.md, C:\hades\Hecton8\GEMINI.md, C:\hades\Hecton8\voxels.md, and C:\hades\Hecton8\terrain.md.
2. PROOF OF READING: Your first output/plan MUST start with a direct quote or explicit reference to a key constraint, constant (e.g., `GlobalQualityWeight`, `used/total > 0.90`), or API check from voxels.md or AGENTS.md. (e.g., quote voxels.md line 98: "GlobalQualityWeight may scale SDF resolution, extraction distance, diagnostic depth, and rebuild cadence, but it must not change collision truth, carve permission, or save delta identity.").

Your Objective:
Perform a surgical, read-only diagnostic audit of HectonVoxelEngine.cs, HectonAnomalyEngine.cs, HectonAnomalySdfJobs.cs, and related scripts to address requirements R1, R2, R3:

1. R1 & R2 Audit (Camera & Quality Bias Removal, Deterministic SDF Noise):
   - Inspect `VoxelCliffOverhangNoiseJob` in `Assets/_Project/Scripts/World/HectonAnomalySdfJobs.cs` (lines 791-865). Note how `noisePos` is calculated as `gridPos * VoxelSizeMeters * NoiseFrequency` using local array index coordinates `(x, y, z)`. Analyze why passing canonical double3/float3 `OriginAup` (or `sdfOriginAup`) into `VoxelCliffOverhangNoiseJob` and evaluating `noisePos = (OriginAup + gridPos * VoxelSizeMeters) * NoiseFrequency` restores 100% world-coordinate alignment and determinism across chunk splits and origins.
   - Inspect `ApplyVoxelCliffOverhangNoise` in `Assets/_Project/Scripts/World/HectonAnomalyEngine.cs` (lines 711-745) and its invocation in `HectonVoxelEngine.cs` (lines 10815-10826). Check what parameters are passed and how `OriginAup` should be wired through.
   - Verify if any remaining code paths in `HectonVoxelEngine.cs` gate or scale SDF noise evaluation by `GlobalQualityWeight` or camera view vectors prior to mesh extraction.

2. R3 Audit (Capacity Overflow Protection):
   - Inspect `ResolveStreamingMeshRawScratchCapacity` and `ResolveStreamingMeshRawScratchQualityCapacity` in `Assets/_Project/Scripts/HectonVoxelEngine.cs` (lines 12252-12274).
   - Analyze how `ResolveStreamingMeshRawScratchQualityCapacity` lerps down to `StreamingMeshRawVertexScratchLowTierCapacity` (262,144) when `GlobalQualityWeight` is low, and how `if (desired > qualityCapacity) return qualityCapacity;` truncates `desired` memory allocation below what a dense chunk requires. Explain why this causes Marching Cubes scratch buffer overflows and causes chunk silhouettes to disappear.
   - Formulate the exact fix recommendation for `ResolveStreamingMeshRawScratchCapacity` so `desired` (e.g. `totalCellCount * MC_BUFFER_MULTIPLIER`) is NEVER truncated below `desired` for dense chunks regardless of quality tier.

Output Requirements:
- Write `analysis.md` and `handoff.md` under `C:\hades\Hecton8\.agents\explorer_m5\`.
- Provide exact code diffs and step-by-step implementation instructions for the Worker in `handoff.md`.
- Report findings back to Orchestrator via send_message.

</USER_REQUEST>
