# Rationale_TERRAIN_GPR_SYSTEM

Status: PENDING VERIFICATION

## Mandate Selection

Problem: GPR must query subsurface geology without object instantiation or singleton coupling.
Solution: Use voxel SDF and ore-position SoA buffers, GlobalRegistry contract surface, persistent NativeArrays, and GPU structured/indirect buffers.
Rejected Alternatives: Unity Physics.SphereCastAll and GameObject markers; both allocate or scale poorly and ignore SDF truth.
Scalability potential: Low=16 rays and cheap ring draw, Middle=32 rays, High=64 rays, Ultra=64 rays with denser visual pulse material and longer history.
Hardware Impact: On i3/MX350, avoiding managed queries and object markers is estimated to save 150-400 us per active scan burst versus physics/object UI probes.
