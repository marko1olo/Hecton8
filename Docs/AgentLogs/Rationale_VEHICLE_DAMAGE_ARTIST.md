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

## Decision 4 - Object-Space Shader Denting

Problem: Impact dents must survive AUP/floating-origin shifts and still deform vertices before world transformation.

Solution: Store impact points in submarine/object local space and call `HectonCoreLitApplyHullDentsOS` before `GetVertexPositionInputs`. The shader computes `distSq = dot(delta, delta)` against `_HectonHullDents`.

Rejected Alternatives: World-space dent points, per-frame local-to-world dent matrices, or CPU-rebased dent arrays. Those are origin-shift sensitive or add per-frame correction work.

Scalability potential: Low = skip deformation and expose scar darkening scalar. Middle = same helper with lower active count. High = full fixed 16. Ultra = full fixed 16 with surface cheat allowed to be stronger in materials.

Hardware Impact: Low-tier MX350 pays no 16-dent vertex loop. High tier pays max 16 dot products per hull vertex only on affected shader variants. CPU saved versus mesh mutation remains 100-800 us per impact burst.

## Decision 5 - Packed Radius/Depth In W

Problem: Prompt requires `Vector4` dents where xyz is local point and w packs radius/depth, leaving no extra per-dent channel.

Solution: Quantize radius to 1/16 meter in low 8 bits and depth to 8-bit normalized meters in high bits, stored as an exact float integer. Shader unpacks with `floor`, subtraction, and reciprocal constants.

Rejected Alternatives: Separate depth arrays, structured buffers, or `float16` bit reinterpretation. Separate arrays violate prompt; structured buffers are heavier; half bit reinterpretation is compiler/API-risky.

Scalability potential: Low/Mid/High/Ultra use the same compact payload; higher tiers spend saved CPU/GPU budget on better material response, not more payload.

Hardware Impact: Saves one extra vector array upload and keeps the global payload to 16 vectors. Estimated upload reduction versus two arrays: 64 bytes per dirty frame, plus simpler shader constant access.

## Decision 6 - Normal And Collider Lies Stay Lies

Problem: Physical normals and colliders are expensive to rebuild and would turn a visual feedback feature into a gameplay authority system.

Solution: Do not recalculate normals and do not touch colliders. Use dent depth to darken albedo and lower smoothness, faking occlusion and scraped paint while collision remains pristine.

Rejected Alternatives: MeshCollider rebuild, runtime mesh normal recalculation, normal-map baking on impact. Those create CPU spikes, allocations, and simulation divergence.

Scalability potential: Low = texture-masked scar only. Middle = scar plus a few vertex dents. High = full dents plus surface darkening. Ultra = full dents with heavier material response possible later.

Hardware Impact: Collider rebuild cost avoided entirely; expected savings range from 200 us to multiple milliseconds on dense hull meshes. Current shader cheat adds only fragment lerps and no CPU work.

## Decision 7 - MX350 Bypass Path

Problem: A 16-dent vertex loop on every hull vertex is unjustified on MX350/i3 when hull damage is primarily feedback.

Solution: Set `_HectonHullDentParams.y` on low tier. The vertex helper returns early and the fragment path uses `_DetailMask` as a cheap damage scar texture modulated by the global scar scalar.

Rejected Alternatives: Running reduced loop count in shader, branching per dent, or CPU-side mesh simplification. Reduced loops still burn vertex ALU; CPU simplification is irrelevant to impact feedback and can allocate.

Scalability potential: Low = texture scar only. Middle = full path but low active count. High/Ultra = full 16 dents and stronger noir damage response.

Hardware Impact: Saves up to 16 dot/falloff branches per hull vertex on MX350; estimated GPU vertex-stage saving is content-dependent, expected 0.03-0.18 ms on hull-heavy views.

## Decision 8 - Blackbox And Signal Without Direct Audio Coupling

Problem: Hull deformation needs post-mortem visibility and audio groan hooks without hard-wiring VFX into the audio runtime.

Solution: Add `CrashTelemetryBuffer.ReportHullDentState` for `ActiveHullDents` and publish unmanaged `HullDeformedSignal` through `GlobalSignals`.

Rejected Alternatives: `Debug.Log`, direct audio service calls, or no telemetry. Logs allocate and are not black-box state; direct audio coupling breaks the event-bus boundary; no telemetry violates the crash-reporting mandate.

Scalability potential: Low/Mid/High/Ultra share the same event and telemetry lane. Higher tiers can subscribe for richer groan layers without changing hull VFX.

Hardware Impact: One telemetry ring write and one unmanaged signal enqueue on accepted impacts only. Estimated low-end cost: 2-8 us per accepted impact, zero persistent managed GC.

## Decision 9 - Compile Wall Handling

Problem: Required compile verification cannot be completed through the available paths: `dotnet build Hecton8.Core.csproj` fails on existing missing assembly references, and Unity MCP refresh/read-console returned editor timeout/no session.

Solution: Mark Task 19 as dependency-blocked while still proving source-level shader invariants: fixed `[unroll]` 16-loop, no `distance()` in dent path, squared `dot` distance, low-tier bypass.

Rejected Alternatives: Falsely reporting a green compile, or reverting valid hull dent work to satisfy an unrelated build graph failure. Both hide facts.

Scalability potential: Compile proof is pending, but the implemented code preserves Low/Middle/High/Ultra paths and avoids runtime fallback allocation.

Hardware Impact: No runtime gain from the blocked compiler itself; source path still targets 0 B/impact and low-tier shader-loop bypass.

## OMEGA POLISH CHANGES

Problem: Final anti-bloat audit found remaining division syntax in `HullDentShaderController` hot-ish math and required explicit compile-wall proof.

Solution: Replaced dent intensity, scar scalar, and event intensity divisions with `math.rcp(...)` reciprocal multiplies. Re-ran zero-GC/source audits and `dotnet build Hecton8.Core.csproj`.

Rejected Alternatives: Leaving divisions as-is because they are small, or falsely claiming Unity compiler verification. The polish mandate explicitly rejects both.

Scalability potential: Low = vertex dent loop bypass plus texture-masked scar. Middle = fixed dent count with same packed payload. High = full 16 loop. Ultra = full 16 loop with stronger material response available without changing payload.

Hardware Impact: Reciprocal multiplies shave only sub-microsecond CPU time per accepted impact, but remove avoidable scalar division from the controller. Main gain remains: 0 B/impact, no mesh/collider rebuild, low-tier shader-loop bypass.

Exact cinematic cheats used: shader-only vertex depression, packed dent radius/depth in one float, albedo/smoothness darkening instead of normal rebuild, pristine collider lie, low-tier texture scar instead of vertex dents.

Final Git Diff: relevant hull-dent paths currently dirty are `Assets/_Project/Art/Shaders/Hecton_DryZoneLit.shader`, `Assets/_Project/Scripts/Vehicles/VFX/HullDentShaderController.cs`, `Docs/Tasks/Status_VEHICLE_DAMAGE_ARTIST.md`, and this rationale file. `GlobalSignals.cs` is dirty in the worktree for unrelated inventory signal work; hull deformation signal support is already present in tracked source and was verified by source grep.

Verification State: PENDING due blocked global compile dependencies. `dotnet build Hecton8.Core.csproj --no-restore` still fails on existing missing references (`Hecton8.Core.Scheduling`, `Hecton8.Core.Memory.Layout`, `Hecton8.Audio.Propagation`, `Hecton8.Physics.CCD`, fluid/brine contracts). Unity MCP compile and console remained unavailable after refresh timeout/no session.
