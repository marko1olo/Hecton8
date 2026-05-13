# Rationale_HLOD_INSTANCE_CULLING

Status: PENDING VERIFICATION

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
