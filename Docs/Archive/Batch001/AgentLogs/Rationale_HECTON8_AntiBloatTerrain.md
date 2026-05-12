# Rationale: HECTON8_AntiBloatTerrain

## Anti-Bloat Continuation: 2026-05-11

Problem: The latest static audit found compile validation blocked by missing interaction contract types, after terrain/voxel-adjacent math changes had already been applied.
Solution: Treat this as a compile-medic boundary issue only; inspect the existing first-party interaction bridge and repair the contract visibility without inventing a new interaction system.
Rejected Alternatives: Reverting terrain/voxel math changes would not address the missing contract symbols. Adding runtime wrappers or concrete cross-domain dependencies would violate the GlobalRegistry/interface boundary.
Scalability potential: Low tier keeps the cheap math path and shader LOD keywords; High and Ultra spend saved CPU on shader-side visual smoothing rather than returning to CPU-heavy normals.
Hardware Impact: Expected low-end gain remains from cast-bias quantization, literal yaw quaternions, nearest-grid normals, and shader LOD gating; exact timing still requires Unity profiler proof on MX350/i3 hardware.

Problem: The prior batch status existed but the rationale file was missing, which violates the local audit trail rule.
Solution: Recreate this rationale file before further code edits, with concrete bottlenecks and rejected alternatives.
Rejected Alternatives: Chat-only reporting was rejected because AGENTS.md states CTO review consumes files on disk.
Scalability potential: Disk logs preserve decisions across context compaction and parallel agents.
Hardware Impact: No runtime impact; process repair only.

Problem: `VoxelNormalJob.SampleNearestGridGradientAndAo` still spent scalar work on a bounded AO clamp and passed the density array into a method that already lives inside the job.
Solution: Count solid neighbors as integers, read `densityField` directly from the job field, and remove the redundant `saturate` because six neighbors at `1/9` scale produce an AO range of `[0.333333, 1]`.
Rejected Alternatives: Restoring multi-tap smooth normals or adding a second AO sampling ring was rejected; those recover visual quality with CPU bandwidth instead of the mandated shader-side fake.
Scalability potential: Low tier keeps nearest-grid normals and cheap integer AO. High and Ultra preserve the same CPU path and buy back the visual loss in `Hecton_AbyssalVoxelRock.shader` through screen-space normal smoothing and cavity noise.
Hardware Impact: Estimated gain is small but hot: roughly 0.4-1.2 microseconds per 50k voxel-normal vertices on low-end Burst targets, mainly from dropping clamp ALU and a NativeArray argument copy from the inner helper call.
