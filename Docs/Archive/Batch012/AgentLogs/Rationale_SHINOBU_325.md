# Rationale_SHINOBU_325

Status: POLISH PASS ACTIVE / RENDERGRAPH ABI PATCHED / BUILD BLOCKED BY CPU 93 AND ACTIVE CSC-DOTNET / RUNTIME PROOF PENDING

## Decision 001: Authority And Data Route

Problem: Visor trauma rendering must not become a second gameplay authority or a hot global polling route.
Solution: Treat screen-space wounds as presentation-only "Dear Lie" data owned by SHINOBU_325, sourced from existing unmanaged damage signals or mock data, stored in fixed-capacity unmanaged buffers, and excluded from rollback truth.
Rejected Alternatives: Canvas overlays, DecalProjector GameObjects, managed event callbacks, and per-frame GlobalRegistry polling are too slow, allocate under stress, or violate the global authority law.
Scalability potential: Low uses newest small decal window and accelerated decay; Middle keeps moderate decal count; High adds stronger crack refraction; Ultra evaluates maximum active decals and richer normal perturbation.
Hardware Impact: On i3/MX350, expected benefit is avoiding hierarchy churn, transparent overdraw stacks, and SetData stalls; exact gain is PENDING VERIFICATION.

## Decision 002: DTO Layout

Problem: GPU upload payload must be stable for ARM64, Burst, NativeArray, and HLSL StructuredBuffer reads.
Solution: Use `TraumaDecalDTO` with `[StructLayout(LayoutKind.Explicit, Size = 80)]`: `float4x4 LocalToWorld` at 0, `uint DecalTypeHash` at 64, `float Opacity01` at 68, `float BirthTime` at 72, `uint Flags` at 76.
Rejected Alternatives: C# properties, sequential auto-layout, runtime bools, and packed structs risk CS1612 copies, platform-sensitive offsets, or unaligned access.
Scalability potential: Same DTO works for Low through Ultra; quality changes active count and shader math, not layout or authority route.
Hardware Impact: On i3/MX350, 128 decals cost 10KB per buffer; double-buffered payload remains ~20KB plus telemetry, negligible versus VRAM budget.

## Decision 003: Reuse Existing Screen-Space Route, Rename ABI

Problem: The repository already contained an old SHINOBU_275 screen-space visor wound route, so a second SHINOBU_325 runtime would duplicate ownership and create refactoring loops.
Solution: Promote the existing route to the active SHINOBU_325 trauma ABI: `TraumaDecalDTO`, `_GlobalVisorTrauma`, `Hecton_VisorTrauma.shader`, and `Dump_SHINOBU_325.bin`.
Rejected Alternatives: Creating a new feature beside `DeferredDecalPass` would double render passes, queues, Vault buffers, and editor tooling without changing the actual presentation problem.
Scalability potential: Low through Ultra keep one pass and one payload route; saved CPU/GPU budget goes into active count and shader detail.
Hardware Impact: On i3/MX350, avoiding duplicate passes prevents an estimated 150-300us frame cost and avoids additional transparent overdraw.

## Decision 004: RenderGraph Fullscreen Composite

Problem: Runtime `DecalProjector` spawn turns every blood/crack impact into hierarchy, culling, renderer, and material pressure.
Solution: Keep one RenderGraph raster pass, one fullscreen `CoreUtils.DrawFullScreen`, one StructuredBuffer, and depth-based reconstruction in `Hecton_VisorTrauma.shader`.
Rejected Alternatives: URP DecalProjector, spawned quads, Canvas overlays, particle splats, and material clones scale per impact and are uncontrolled on weak hardware.
Scalability potential: Low runs 8 active decals with cheaper procedural math; Middle uses moderate count; High enables stronger refraction; Ultra consumes all 128 entries and richer atlas/procedural sampling.
Hardware Impact: On i3/MX350, expected gain is 0.2-1.0ms under dense trauma versus projector/object stacks. Exact profiler proof is pending.

## Decision 005: BufferID Collision Repair

Problem: The inherited local range `71490..71496` collides with central `H8Memory` entries: auxiliary equipment and propwash GPU lanes already own those values.
Solution: Move SHINOBU_325 trauma presentation buffers to local `73190..73196`, then reserve `73197..73198` for Vault-owned request ingress, and document the collision in the route card and binary payload ledger.
Rejected Alternatives: Keeping the old values would risk Vault aliasing, state corruption, and false diagnostics. Adding enum entries was not needed for a local presentation/proof lane.
Scalability potential: Stable buffer identity from Low through Ultra; quality only changes live count and shader cost.
Hardware Impact: On i3/MX350, the gain is correctness and reduced integration risk, not direct frame time.

## Decision 006: AUP And Depth Reconstruction

Problem: Absolute float world coordinates fail in large-world camera motion and produce decal shimmer or wrong projection.
Solution: Keep impact/root positions in AUP until matrix generation, upload camera-relative matrices, and reconstruct scene position from depth in the shader before local projection tests.
Rejected Alternatives: Uploading absolute float positions or sampling Transform positions per decal violates AUP and hierarchy rules.
Scalability potential: Same math path scales from Low to Ultra; high tiers spend extra ALU on crack/refraction rather than different authority data.
Hardware Impact: On i3/MX350, cost is bounded by active count and one fullscreen pass; precision defects are prevented without scene object scans.

## Decision 007: Scanner As Proof Artifact

Problem: A chat claim that DecalProjector spawn is gone is not acceptable evidence.
Solution: Add `Tools/Trauma_Projector_Inquisition.py`, reuse the existing projector scanner, extend it for trauma overlay patterns, and write JSON into `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json`.
Rejected Alternatives: Manual source notes and broad project grep miss serialized renderer features and are not repeatable.
Scalability potential: Scanner is offline proof only; runtime quality cost is zero.
Hardware Impact: No runtime hardware impact. It prevents regression into object-based trauma decals.

## Decision 008: Historical SHINOBU_275 Docs Superseded

Problem: Old route cards documented `_GlobalVisorWounds`, `Hecton_VisorWounds.shader`, and colliding `71490..71496` values.
Solution: Add SHINOBU_325 route card/ledger entry and mark SHINOBU_275 docs historical at the top.
Rejected Alternatives: Editing only source would leave integrators with a stale route map and likely reintroduce the collision.
Scalability potential: Documentation does not alter runtime tiers; it protects the single-owner rule.
Hardware Impact: No direct runtime impact; avoids integration churn and Vault corruption.

## Decision 009: Vault-Backed Request Ingress

Problem: The previous SHINOBU_325 route used a private persistent `NativeQueue<DecalRequestSignal>` as the impact ingress buffer. It was prewarmed, but still owned allocator memory outside `GlobalDataVault`, failing the strengthened H-Phi/Vault law.
Solution: Replace the private queue with `DecalRequestSignal[1024]` in Vault BufferID `73197` and `DecalRequestQueueStateDTO[1]` in Vault BufferID `73198`. Public ingress locks those two buffers briefly and writes fixed ring slots; visual sync locks them with the rest of the trauma route and the Burst matrix job drains by `ReadIndex`/`PendingCount`.
Rejected Alternatives: Keeping the `NativeQueue` as a documented exception would preserve hidden allocator ownership. Routing every impact directly to `TraumaDecalDTO` would merge request ingress and visual projection state, making drop accounting and mock reservation weaker.
Scalability potential: Low through Ultra keep the same 1024 request capacity; `GlobalQualityWeight` controls drain budget through `MaxActiveDecals`, not buffer identity.
Hardware Impact: On i3/MX350, this removes private persistent allocator fragmentation risk and keeps request counters in one 64B row. Direct frame-time gain is expected to be small; memory sovereignty and integration safety are the main gain.

## Decision 010: Post-Ingress Audit Hardening

Problem: After replacing the request queue, lock-failure paths could return without contributing to dropped-ingress telemetry, and the JSON proof writer could fail validation if an earlier agent left a UTF-8 BOM in the shared report.
Solution: Count failed request-ring locks and failed ring/state resolves as dropped ingress, update the editor layout validator message to include all active trauma/request ABI rows, and read the shared optimization report as `utf-8-sig` before writing normal UTF-8.
Rejected Alternatives: Ignoring lock failure drops would hide backpressure under visual-sync contention. Treating the BOM as a manual cleanup issue would make the scanner proof brittle in a multi-agent workspace.
Scalability potential: Low through Ultra keep identical authority and buffer identity; only telemetry fidelity and proof robustness changed.
Hardware Impact: On i3/MX350, the runtime cost is one bounded integer counter update on failure paths only. The gain is blackbox clarity under thermal/CPU contention.

## Decision 011: RenderGraph Static Atlas Binding

Problem: `RasterCommandBuffer.SetGlobalTexture(int, Texture2DArray)` is a known compile-wall shape in this project, while `RasterCommandBuffer.SetGlobalTexture(int, TextureHandle)` is supported by the local SRP package.
Solution: Keep `_BlitTexture` and `_CameraDepthTexture` as RenderGraph `TextureHandle` bindings, but bind the optional trauma atlas as a material texture before the RenderGraph render function. The render function now binds only graph texture handles, the imported `GraphicsBuffer`, and scalar/vector IDs before one fullscreen draw.
Rejected Alternatives: Importing a serialized `Texture2DArray` asset into RenderGraph would require new RTHandle ownership machinery for a static optional asset. Using the existing global static texture bridge would compile, but it writes global shader state for an atlas that can remain material-local.
Scalability potential: Low through Ultra still use the same pass; `_GlobalVisorTraumaParams.w` continuously chooses procedural fallback versus atlas sampling without changing shader variants.
Hardware Impact: On i3/MX350, frame cost is unchanged; the gain is avoiding a known compile blocker and keeping the hot render func on graph-visible resources.
