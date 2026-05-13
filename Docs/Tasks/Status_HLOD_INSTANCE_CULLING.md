# Status_HLOD_INSTANCE_CULLING

Batch prompt: `HLOD_INSTANCE_CULLING`
Agent role: `GPU_INSTANCER_ARCHITECT`
Domain: ECHELON 2 / BRG Scatter Director GPU instancing and compute culling
Task count: 19
Status: PENDING VERIFICATION

## Loop 0 - Initialization

- [x] Prompt extracted from `Docs/Tasks/CURRENT_BATCH.md` using CLI regex from cover to cover. DOD: strict prompt isolation. Alternative rejected: relying on IDE tab or truncated MCP read. Estimate: 25 us.
- [x] Relevant mandates read: `REND_GPU_Occlusion_Culling_6000`, `REND_GPU_Sovereignty`, `REND_URP_Graphics_HotPath_Optimization_HLOD`, `GPU_Compute_Kernels_Kernels_Optimization_MX350`, `GPU_Compute_Warp_Sizing_Mobile`, `REND_Instanced_Flora_Physics`, `MATH_Coordinate_Precision_AUP_FloatingOrigin`, `ARCH_Global_Registry_ServiceLocator_DI_Init`, `OPT_Zero_GC_Policy_AllocFree_Mandate`. DOD: mandate-gated before code. Alternative rejected: generic Unity instancing from memory. Estimate: 40 us.

## Tasks

- [ ] 1. SINGLETON ERADICATION: Purge `FloraCullingManager.Instance`. Register `IInstanceCullingService`.
- [ ] 2. SIGNAL MIGRATION: Consume `CameraPositionSignal` and `CameraFrustumSignal` natively.
- [ ] 3. ASMDEF ISOLATION: `Hecton8.Graphics.Culling` depends ONLY on `Contracts`.
- [ ] 4. DEAD CODE HUNT: Eradicate CPU-side `for` loops checking `math.distancesq` for static flora rendering.
- [ ] 5. COMPUTE KERNEL: Write `InstanceCulling.compute` with `AllInstances` input and `VisibleInstances` append output.
- [ ] 6. FRUSTUM PLANES: Pass six camera frustum planes and cull against instance bounds.
- [ ] 7. DISTANCE FADE: Cull beyond 200m.
- [ ] 8. INDIRECT ARGS GENERATION: Use `GraphicsBuffer.CopyCount` into indirect args buffer. Zero CPU readback.
- [ ] 9. AUP SHIFT SAFETY: Offset matrices in a Burst job when `AupShiftSignal` fires.
- [ ] 10. HI-Z PREPARATION: SDF texture cull for solid rock on MX350 path.
- [ ] 11. DYNAMIC BATCHING OVERRIDE: Ensure this compute path is sole authority for these procedural props.
- [ ] 12. WIND SWAY DATA: Pack phase/sway seed into matrix component.
- [ ] 13. ZERO-GC: Dispatch path has no CPU allocations.
- [ ] 14. MATH LOD: Low tier cull distance reduced to 100m.
- [ ] 15. VRAM BUDGET ABORT: If VRAM >1600MB, reject odd instance IDs.
- [ ] 16. BLACKBOX DUMP: Push visible/culled flora counts to telemetry.
- [ ] 17. EVENT BUS: Emit `CullingOverloadSignal` above 50,000 visible instances.
- [ ] 18. CROSS-DOMAIN AUDIT: Ensure `FloraInteractionManager` uses culled buffer for vertex sway.
- [ ] 19. OMEGA COMPILE CHECK: Verify compute shader thread groups align with 64-warp sizes.

## Verification Log

- Pending: repository scan.
- Pending: Unity compile.
- Pending: compute shader import validation.
