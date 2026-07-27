## 2026-07-27T02:26:49Z
<USER_REQUEST>
You are dispatched as teamwork_preview_auditor for Milestone 7: Forensic Integrity Audit.
Working Directory for metadata: C:\hades\Hecton8\.agents\auditor_m7_voxel

MANDATORY HECTON-8 INTAKE DIRECTIVE:
1. You MUST load and follow authority files: C:\hades\Hecton8\AGENTS.md, C:\hades\Hecton8\GEMINI.md, C:\hades\Hecton8\voxels.md, and C:\hades\Hecton8\terrain.md.
2. PROOF OF READING: Your first output/plan MUST start with a direct quote or explicit reference to a key constraint, constant (e.g., `GlobalQualityWeight`, `used/total > 0.90`), or API check from voxels.md or AGENTS.md. (e.g., quote voxels.md line 98: "GlobalQualityWeight may scale SDF resolution, extraction distance, diagnostic depth, and rebuild cadence, but it must not change collision truth, carve permission, or save delta identity.").

Your Objective:
Perform a strict forensic integrity audit of the source changes in `Assets/_Project/Scripts/World/HectonAnomalySdfJobs.cs`, `Assets/_Project/Scripts/World/HectonAnomalyEngine.cs`, `Assets/_Project/Scripts/HectonVoxelEngine.cs`, and `Assets/_Project/Scripts/Editor/AnomalyTestHarness.cs`.

Forensic Integrity Verification Protocol:
1. Zero Mocks & Genuine Implementation Audit:
   - Check for hardcoded test returns, dummy/facade implementations, or shortcut bypasses.
   - Verify that `VoxelCliffOverhangNoiseJob` genuinely evaluates `noisePos` from `OriginAup + (double3)(gridPos * VoxelSizeMeters)` in absolute AUP world space.
   - Verify that `ApplyVoxelCliffOverhangNoise` genuinely assigns `OriginAup` on `VoxelCliffOverhangNoiseJob` and `HectonVoxelEngine.cs` passes `sdfOriginAup`.
   - Verify `ResolveStreamingMeshRawScratchCapacity` genuinely returns `math.max(desired, (long)qualityCapacity)` clamped to `StreamingMeshRawVertexScratchVisualOverkillCapacity`, guaranteeing that desired memory is never truncated under low quality tiers.
2. Codebase Consistency & Asmdef Integrity:
   - Verify clean C# syntax and assembly definition compliance.

Output Requirements:
- Write `audit_report.md` and `handoff.md` under `C:\hades\Hecton8\.agents\auditor_m7_voxel\`.
- State explicit verdict: `CLEAN` or `INTEGRITY VIOLATION`.
- Report back to Orchestrator via send_message.

</USER_REQUEST>
