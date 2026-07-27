## 2026-07-27T02:26:45Z
You are dispatched as teamwork_preview_reviewer for Milestone 7: Code Review of SDF Sampling Determinism & Scratch Capacity Protection.
Working Directory for metadata: C:\hades\Hecton8\.agents\reviewer_m7_voxel

MANDATORY HECTON-8 INTAKE DIRECTIVE:
1. You MUST load and follow authority files: C:\hades\Hecton8\AGENTS.md, C:\hades\Hecton8\GEMINI.md, C:\hades\Hecton8\voxels.md, and C:\hades\Hecton8\terrain.md.
2. PROOF OF READING: Your first output/plan MUST start with a direct quote or explicit reference to a key constraint, constant (e.g., `GlobalQualityWeight`, `used/total > 0.90`), or API check from voxels.md or AGENTS.md. (e.g., quote voxels.md line 98: "GlobalQualityWeight may scale SDF resolution, extraction distance, diagnostic depth, and rebuild cadence, but it must not change collision truth, carve permission, or save delta identity.").

Your Objective:
Perform an independent, high-reliability code review of the changes made by the Worker in Milestone 6 across:
- `Assets/_Project/Scripts/World/HectonAnomalySdfJobs.cs`
- `Assets/_Project/Scripts/World/HectonAnomalyEngine.cs`
- `Assets/_Project/Scripts/HectonVoxelEngine.cs`
- `Assets/_Project/Scripts/Editor/AnomalyTestHarness.cs`

Review Criteria:
1. R1 & R2 Correctness: Verify `VoxelCliffOverhangNoiseJob` uses canonical `OriginAup` (double3) to evaluate `noisePos = (float3)(OriginAup + (double3)(gridPos * VoxelSizeMeters)) * NoiseFrequency`. Confirm that noise evaluation is 100% world-coordinate aligned and deterministic across chunk origins and camera view directions.
2. R3 Correctness: Verify `ResolveStreamingMeshRawScratchCapacity(int totalCellCount)` in `HectonVoxelEngine.cs` returns `math.max(desired, (long)qualityCapacity)` clamped to `StreamingMeshRawVertexScratchVisualOverkillCapacity`. Confirm `desired` allocation is never truncated for dense chunks on low quality tiers.
3. Interface & Asmdef Conformance: Confirm all signatures and struct layouts match project contracts.

Output Requirements:
- Write `review.md` and `handoff.md` under `C:\hades\Hecton8\.agents\reviewer_m7_voxel\`.
- Provide an explicit verdict: `PASS` or `REJECT` with rationale.
- Report back to Orchestrator via send_message.
