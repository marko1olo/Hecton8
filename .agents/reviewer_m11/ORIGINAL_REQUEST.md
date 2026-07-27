## 2026-07-27T03:02:03Z
<USER_REQUEST>
You are a Reviewer subagent (teamwork_preview_reviewer) assigned to perform code review for the implementation of Hecton8 R1, R2, and R3 requirements.

Working Directory: C:\hades\Hecton8\.agents\reviewer_m11

## Authority & Domain Rules
Read:
- `C:\hades\Hecton8\AGENTS.md`
- `C:\hades\Hecton8\voxels.md`
- `C:\hades\Hecton8\terrain.md`

Include exact receipt: "Authority used: AGENTS.md; PROJECT_BIBLES.md; voxels.md; terrain.md."

## Review Scope & Instructions
Review all modified files:
1. `Assets/_Project/Scripts/HectonVoxelVolume.cs` (lines 2000–2020 & 4120–4145)
2. `Assets/_Project/Scripts/HectonPlayerSpawner.cs` (lines 144, 399, 434)
3. `Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsJobs.cs` (lines 202, 654–669)
4. `Assets/_Project/Scripts/HectonVoxelEngine.cs` (lines 4180–4195)
5. `Assets/_Project/Scripts/World/HydraulicErosionJob.cs` (lines 847, 893–899, 937–943)
6. `Assets/_Project/Scripts/World/WorldProceduralTerrainThermalWeatheringJobs.cs` (lines 28–32, 41–48)

Check:
- Correctness of signal publishing (`WorldChunkPhysicsBakedSignal` with `FlagColliderActive | FlagHeightmapSynced` and dynamic chunk sizing).
- Interface compliance and URP Cave Shader vertex color channel spec (R: Floor weight, G: Wall weight, B: 0, A: AO byte).
- Safeguards for NaNs, unnormalized normals, zero division.
- Boundary erosion mass conservation and clamp bounds in `HydraulicErosionJob.cs` and `WorldProceduralTerrainThermalWeatheringJobs.cs`.

Write your review report to `C:\hades\Hecton8\.agents\reviewer_m11\review.md` and handoff to `C:\hades\Hecton8\.agents\reviewer_m11\handoff.md`. Send a message with your verdict (PASS / VETO).
</USER_REQUEST>
