## 2026-07-27T02:26:47Z
Milestone 7: Empirical Stress Test & Verification of Voxel SDF Determinism & Scratch Capacity.
Working Directory for metadata: C:\hades\Hecton8\.agents\challenger_m7_voxel

MANDATORY HECTON-8 INTAKE DIRECTIVE:
1. You MUST load and follow authority files: C:\hades\Hecton8\AGENTS.md, C:\hades\Hecton8\GEMINI.md, C:\hades\Hecton8\voxels.md, and C:\hades\Hecton8\terrain.md.
2. PROOF OF READING: Your first output/plan MUST start with a direct quote or explicit reference to a key constraint, constant (e.g., `GlobalQualityWeight`, `used/total > 0.90`), or API check from voxels.md or AGENTS.md. (e.g., quote voxels.md line 98: "GlobalQualityWeight may scale SDF resolution, extraction distance, diagnostic depth, and rebuild cadence, but it must not change collision truth, carve permission, or save delta identity.").

Your Objective:
Empirically stress-test the math and determinism of the code changes in `HectonAnomalySdfJobs.cs`, `HectonAnomalyEngine.cs`, and `HectonVoxelEngine.cs`:

1. Noise Determinism Verification (R1 & R2):
   - Mathematically verify that evaluating `noisePos` using `worldPosAup = OriginAup + (double3)(gridPos * VoxelSizeMeters)` yields identical coordinates for point P = (64, 0, 0) evaluated as:
     - Local (64, 0, 0) with OriginAup = (0, 0, 0)
     - Local (0, 0, 0) with OriginAup = (64, 0, 0)
   - Confirm that camera orientation or `GlobalQualityWeight` changes do not affect `OriginAup` or `noisePos`.

2. Capacity Allocation Protection Verification (R3):
   - Verify `ResolveStreamingMeshRawScratchCapacity(int totalCellCount)` math for a 64x64x64 chunk (`totalCellCount = 262144`, `desired = 524288`):
     - At `GlobalQualityWeight = 0.0f` (`qualityCapacity = 262144`): confirm `capacity` is 524,288 (NOT truncated to 262,144).
     - At `GlobalQualityWeight = 0.5f`: confirm `capacity` is 524,288.
     - At `GlobalQualityWeight = 1.0f` (`qualityCapacity = 786432`): confirm `capacity` is 786,432.
   - Confirm zero risk of scratch capacity overflow or disappearing silhouettes at lower quality tiers.

Output Requirements:
- Write `challenger_report.md` and `handoff.md` under `C:\hades\Hecton8\.agents\challenger_m7_voxel\`.
- Provide an explicit verdict: `PASS` or `FAIL`.
- Report back to Orchestrator via send_message.
