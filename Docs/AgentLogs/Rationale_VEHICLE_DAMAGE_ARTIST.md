# VEHICLE_DAMAGE_ARTIST Rationale

Status: PENDING VERIFICATION

## Decision 0 - Visual Fake First

Problem: Hull damage currently has gameplay health/integrity consequences without visible hull trauma, and CPU mesh mutation or MeshCollider rebuilds would violate frame-time and zero-GC constraints.

Solution: Use shader-space localized dent presentation driven by a fixed-size global `Vector4[16]` buffer. Store impact coordinates in submarine local space, upload with global shader properties only on signal/repair changes, and leave collision authority pristine.

Rejected Alternatives: Runtime `Mesh.vertices` deformation, mesh swapping, dynamic MeshCollider rebuild, and per-object MaterialPropertyBlock paths. They are too expensive, break SRP batching, allocate or force collider/broadphase churn, and add gameplay truth where the prompt explicitly demands a visual lie.

Scalability potential: Low = bypass vertex dent loop and expose scar/decal scalar only. Middle = capped active dent count. High = 16 local dents. Ultra = same 16 dent authority with richer albedo/smoothness darkening and longer dent residency.

Hardware Impact: MX350/i3 target saves CPU mesh write and MeshCollider update cost entirely. Estimated CPU saved versus naive mesh deformation: 100-800 us per impact burst depending on mesh size; actual proof pending profiler.

## Decision 1 - Local-Space Dent Authority

Problem: HECTON-8 uses AUP/floating origin, so storing world-space dent coordinates can drift or jump on origin shifts.

Solution: Convert impact position to the submarine hull root local space when the signal is consumed and store only local coordinates in the ring buffer.

Rejected Alternatives: World-space shader dents or global floating-origin shader offsets for dent positions. Those are shift-sensitive and require extra per-frame correction.

Scalability potential: Local-space dents are stable across all tiers and do not require tier-specific rebase logic.

Hardware Impact: Removes rebase correction upload and per-frame matrix adjustment. Estimated low-end gain: 5-20 us/frame avoided during active dent visualization; proof pending.

## Decision 2 - Global Shader Buffer Instead Of Per-Hull MPB

Problem: Per-submarine `MaterialPropertyBlock` updates would multiply CPU work by renderer count and risk SRP batch breaks during impact bursts.

Solution: Upload a single fixed `_HectonHullDents` global `Vector4[16]` plus `_HectonHullDentParams` only when impact or repair state changes. Radius/depth are quantized and packed into `w`.

Rejected Alternatives: Per-renderer MPBs, dynamic ComputeBuffer allocation, mesh vertex writes, and mesh swapping. They cost CPU, memory churn, or renderer-state churn for a presentation-only lie.

Scalability potential: Low = shader sees low-tier flag and bypasses vertex loop, using scar scalar. Middle = same buffer with fewer active dents. High = full 16 dents. Ultra = full 16 dents plus stronger surface darkening.

Hardware Impact: MX350/i3 avoids renderer iteration and MPB marshaling. Estimated save versus 8-renderer MPB update: 40-160 us/impact burst, plus preserved batching.

## Decision 3 - Read-Only Breach Coupling Contract

Problem: Repair fade needs to know when a real breach disappears without coupling the vehicle VFX assembly back into structural physics internals.

Solution: Expand `ISubmarineHullBreachReadModel` with `ActiveBreachCount` and `TryGetActiveBreach(index, out Vector4)`. VFX reads existing local-space breach outputs and fades dents when no active breach remains nearby.

Rejected Alternatives: New repair signal lane, direct dependency from `SubmarineStructuralGrid` to vehicle VFX, or polling private NativeArrays through reflection. Those increase bus noise, create circular ownership, or are brittle.

Scalability potential: Low = repair fade still runs at 16 dents but shader ignores deformation loop. Middle/High/Ultra = same local read path with richer shader presentation.

Hardware Impact: Worst case 16 dents x 64 breaches = 1024 squared-distance checks on repair frames, no allocation. Estimated low-end cost: 6-20 us in late frame when breaches exist; zero when no breach model or no active dents.
