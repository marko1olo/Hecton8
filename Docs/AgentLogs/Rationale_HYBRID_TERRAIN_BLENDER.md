# Rationale: HYBRID_TERRAIN_BLENDER

Status: PENDING VERIFICATION

## Initial Scope Decision

Problem: Hybrid MapMagic heightmap and first-party voxel caves produce hard terrain/cave intersections.
Solution: Implement isolated terrain seam pipeline under the World/Terrain boundary, consuming chunk signals and using Burst/native mesh data plus shader dither fallback.
Rejected Alternatives: Runtime GameObject skirts and classic Unity mesh vertex arrays are rejected because they hide symptoms, allocate, and create fragile scene dependencies.
Scalability potential: Low uses dither-only cheap concealment. Middle uses bounded seam snapping near player. High/Ultra can add finite-difference normals, blend masks, and visual overkill around close hero seams.
Hardware Impact: MX350/i3 path avoids vertex snapping on Low and caps work to async chunk generation, target 0 B hot-path GC and sub-0.1ms steady-state frame cost.

## Mandate Selection Decision

Problem: Task crosses voxel SDF, terrain rendering, async mesh updates, telemetry, and global registration.
Solution: Loaded VOX_MapMagic_Voxel_Seam_Alignment_Integration, VOX_Voxel_SDF_Geometry_MarchingCubes_Pipeline, REND_Terrain_VirtualTexturing, OPT_Zero_GC, OPT_Cinematic_Cheat, OPT_Performance_Budgets, DBG_Telemetry, and ARCH_Global_Registry.
Rejected Alternatives: Reading unrelated AI/audio/UI mandates would increase noise and risk cross-domain edits.
Scalability potential: Mandates define Low/Middle/High/Ultra seam behavior and require dither fallback before expensive geometry edits.
Hardware Impact: Selection preserves MX350 budget by prioritizing shader fake on Low and chunk-time jobs on higher tiers.
