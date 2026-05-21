# Rationale_SHINOBU_275

State: POLISH ACTIVE / COMPILE BLOCKED BY EXTERNAL DEPENDENCY ERRORS

## Decision 001: Domain Boundary

Problem: The assignment touches combat damage signals and rendering, but the owned output is visor/suit trauma presentation.
Solution: Keep gameplay truth in the combat owner lane and consume only immutable/unmanaged damage snapshots or bridge DTOs in VISUAL_SYNC. Presentation state remains excluded from rollback/gameplay authority.
Rejected Alternatives: Direct combat class references or gameplay state mutation from renderer code; both violate signal segregation and rollback isolation.
Scalability potential: Low uses newest/cheapest wounds only; middle raises capacity; high/ultra spend saved CPU on richer crack/refraction blending without changing gameplay truth.
Hardware Impact: i3/MX350 avoids GameObject decals, hierarchy churn, and SRP Batcher damage; estimated CPU saving from rejected object decals is 80-300 us under wound spam, pending profiler proof.

## Decision 002: Render Path

Problem: DecalProjector GameObjects and Canvas overlays would add draw calls, overdraw, material churn, and allocation risk.
Solution: Use a RenderGraph-owned fullscreen/visor pass plus a `GraphicsBuffer` of unmanaged `VisorDecalDTO` records. The physical wound is a visual fake.
Rejected Alternatives: Unity `DecalProjector`, spawned quads, UI Canvas blood masks, per-instance material property blocks.
Scalability potential: Low evaluates 8 active records with accelerated fade; middle 32-64; high 96; ultra 128 with stronger normal/refraction response.
Hardware Impact: MX350 gets one bounded pass instead of N object submissions. Estimated main-thread saving 100-500 us in heavy combat, GPU cost remains PENDING until Frame Debugger/Profiler.

## Decision 003: DTO Layout

Problem: GPU/Burst data must not trigger CS1612 copies or ARM64 unaligned reads.
Solution: Define `VisorDecalDTO` as explicit 80-byte unmanaged payload: matrix at 0, hash at 64, opacity at 68, birth frame at 72, flags at 76. Request/profile lifetime is packed into the high bits of `DecalTypeHash` so the original XML ABI stays intact.
Rejected Alternatives: auto-properties, nested managed config classes, enums without fixed backing, packed layout.
Scalability potential: Same fixed stride supports all quality tiers; quality changes count/cadence, not DTO shape.
Hardware Impact: 128 records x 80B = 10240B, double-buffered GPU upload still negligible on MX350 if dirty-gated.

## Decision 004: Cold Route Cache

Problem: The inherited decal runtime could resolve `GlobalRegistry` from the visual sync path invoked by RenderGraph.
Solution: Added `WarmupColdGlobalRoutes()` and moved visual sync execution to dispatcher `LateFrameTick`; `AddRenderPasses()` only captures the camera context, publishes a staged prior GPU buffer, and enqueues the pass. `RecordRenderGraph()` now consumes only cached/published render state.
Rejected Alternatives: Polling `GlobalRegistry` during RenderGraph record; relying on scene search; calling `GlobalDataVault.TryGetLatestCreated()`; mutating Vault state from `RecordRenderGraph()`.
Scalability potential: Low/middle/high/ultra all keep the same route; quality changes capacity only, not authority.
Hardware Impact: Removes registry lookup and visual-sync work from the critical render-record path. Estimated low-end gain is small, 2-15 us, but prevents hidden route churn.

## Decision 005: Circular Overwrite

Problem: The prior full-buffer behavior could fade a band then drop a still-visible decal when saturated.
Solution: Use `TotalWritten % capacity` as the single write index. Saturated buffers overwrite oldest visual state deterministically.
Rejected Alternatives: Dynamic resizing, list removal, priority sort, GameObject pooling.
Scalability potential: Low 8 slots; middle 32-64; high 96; ultra 128. Same modulo rule at every tier.
Hardware Impact: Avoids branch-heavy saturation handling and any container growth. Estimated saving 5-30 us during damage bursts on i3/MX350.

## Decision 006: Shader Fake Over Physics

Problem: Helmet glass fractures and torn blood edges can be simulated physically, but that wastes frame budget and adds unstable gameplay-looking state.
Solution: Use procedural shard lines, edge serration, and UV refraction in `Hecton_VisorWounds.shader`, then align the existing visor mega-shader with `ResolveTornVisorEdgeMask`.
Rejected Alternatives: fractured mesh, particles, spawned decals, Canvas textures, per-impact normal maps.
Scalability potential: Low evaluates fewer wounds with simpler alpha; middle raises count; high/ultra increase refraction/normal response through continuous quality weight.
Hardware Impact: One fullscreen pass plus bounded loop replaces N object submissions. Estimated main-thread saving 100-500 us under spam; GPU cost pending profiler.

## Decision 007: Static Proof Route

Problem: A claim that GameObject decals were purged is worthless without repeatable evidence.
Solution: Added `Tools/Decal_Projector_Inquisition.py`; it scans project assets, detects active `DecalProjector` routes and active URP decal renderer features, then merges PASS into `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json`.
Rejected Alternatives: Manual grep pasted to chat; deleting inactive renderer feature stubs without ownership review.
Scalability potential: Static proof is hardware-independent; render cost is still controlled by 8..128 active wound records.
Hardware Impact: Proof artifact found 0 active violations and 2 inactive URP decal renderer features. Estimated runtime gain comes from the implemented path, not the scanner.

## Decision 008: Dispatcher Late-Frame Visual Sync

Problem: Running `ExecuteVisualSync()` from `AddRenderPasses()` still placed signal ingestion, Vault mutation, job scheduling, and mapped upload adjacent to render enqueue rather than a dispatcher-owned phase.
Solution: `DeferredDecalPass` now implements `ILateFrameTickable`. `AddRenderPasses()` stages camera context and publishes only a previously staged GPU buffer; `LateFrameTick()` runs `ExecuteVisualSync()` using `SystemDispatcher.CurrentFrameDeltaTime`.
Rejected Alternatives: Keeping `Time.deltaTime` in render enqueue, completing jobs inside RenderGraph record, or creating a MonoBehaviour sidecar solely for this feature.
Scalability potential: Low-tier can reuse the prior published buffer for one frame without gameplay truth impact; mid/high/ultra still receive the next-frame visual overkill payload.
Hardware Impact: Render record becomes a read-only composition path. Estimated i3/MX350 render-thread risk reduction: 5-25 us under wound spam, pending Frame Debugger/Profiler proof.

## Decision 009: Standalone Shader ABI

Problem: A `UsePass` wrapper hides shader ABI changes and makes RenderGraph/debug proof depend on another shader asset's pass import.
Solution: `Hecton_VisorWounds.shader` now owns the full pass source and matches `_GlobalVisorWounds`, `_GlobalVisorWoundCount`, and `_GlobalVisorWoundRefractionParams` directly.
Rejected Alternatives: Keeping `UsePass`, duplicating old `_HectonDeferredDecal*` bindings, or relying on material remap glue.
Scalability potential: One shader path scales wound count and refraction continuously; low-tier has fewer active records, ultra gets stronger refraction without variant explosion.
Hardware Impact: Removes an import/debug ambiguity, not a measurable runtime win by itself. It prevents pass mismatch failures on low-end QA devices.

## Decision 010: Camera AUP Basis Correction

Problem: CPU wound matrices could localize against a player AUP while the shader reconstructs against the render camera, causing large-world visor drift.
Solution: `ResolveCameraAup(Camera)` now derives the camera AUP from the current runtime origin plus the camera transform before falling back to player context/current origin.
Rejected Alternatives: Absolute world floats, player-only AUP, or shader-side large-coordinate reconstruction.
Scalability potential: Same DTO and shader route across all quality weights; quality changes count/intensity only.
Hardware Impact: Prevents far-origin jitter without extra GPU cost. Estimated low-end saving is indirect: avoids debug/reprojection churn rather than reducing frame time.

## Decision 011: Mock Normal NaN Vaccination

Problem: The emergency mock wound lane used `math.normalize` inside a Burst job. The current inputs were non-zero by construction, but that still left a hidden exception to the explicit denominator-guard rule.
Solution: Replaced the normalize call with `lengthsq -> max(0.0001f) -> rsqrt` plus a finite fallback normal. This keeps the editor/CI mock lane under the same NaN policy as production signal ingestion.
Rejected Alternatives: Keeping the implicit normalize because the source vector includes `y=1`; that assumes future edits never change the generator.
Scalability potential: Same branch-free fast path on every quality tier; low-tier mock capacity still scales through request count, not math shape.
Hardware Impact: Negligible runtime cost in editor-only mock path; removes a rare invalid-normal propagation risk before it can poison matrix generation.

## Decision 012: Pending Visual-Sync Drain

Problem: A deferred visual-sync job could remain pending until another valid camera context arrived, leaving Vault locks and a stale published buffer path dependent on camera cadence.
Solution: `DeferredDecalPass.LateFrameTick()` drains pending work before new staging, `Dispose()` force-completes the pending job, and `ExecuteVisualSync()` returns no duplicate upload while a job is still outstanding.
Rejected Alternatives: Completing every scheduled job immediately, or relying on `AddRenderPasses()` to produce another camera context before pending state is released.
Scalability potential: Low-tier may reuse the last published buffer for one frame while the async job finishes; mid/high/ultra retain the same route with larger active counts.
Hardware Impact: Avoids same-frame blocking and prevents rare lock retention. Estimated gain is stability, not a direct microsecond claim; profiler proof remains pending.

## Decision 013: Cold Storage And Hot-Swap Route

Problem: `ExecuteVisualSync()` could create Vault handles/queues through `EnsureInitialized()` from the visual path, and camera enqueue could retry registry setup repeatedly.
Solution: Cold storage initialization moved to feature `Create()` and `DataVault` hot-swap handling; `ExecuteVisualSync()` now requires `IsInitializedForRead()`. Player and dispatcher rebinds are handled through `IGlobalRegistryHotSwapListener`.
Rejected Alternatives: Polling `GlobalRegistry` from `AddRenderPasses()` or lazily creating Native containers during render-adjacent visual sync.
Scalability potential: All quality tiers share the same authority route; quality changes capacity/refraction only.
Hardware Impact: Removes hidden cold work from the hot visual lane. Estimated i3/MX350 render-thread risk reduction remains 2-15 us, pending profiler.

## Decision 014: RenderGraph Buffer ABI

Problem: The old path mutated material buffer state outside RenderGraph and used a blit helper that did not declare the imported wound buffer or depth texture as pass inputs.
Solution: `DeferredDecalPass.RecordRenderGraph()` is now a raster pass: imports the `GraphicsBuffer`, declares `UseBuffer(Read)`, declares source/depth reads, writes a new composite texture, and binds `_GlobalVisorWounds` with `RasterCommandBuffer.SetGlobalBuffer` inside the render func.
Rejected Alternatives: `AddBlitPass`, pre-pass `Material.SetBuffer`, and hidden global state mutation without RenderGraph resource declarations.
Scalability potential: Same pass scales by `_GlobalVisorWoundCount` from 8..128; no binary pass switch.
Hardware Impact: Prevents RG hazard/stale binding on TBDR and desktop renderers. Estimated render-record risk reduction 5-25 us under wound spam.

## Decision 015: Active Noir Mega-Shader And Visual Frame Source

Problem: Torn-edge work in `HectonVisorUberPost.shader` did not affect the serialized deep-sea noir path, and the wound runtime still used Unity `Time.time`/`Time.frameCount` for visual phase and signal dedupe.
Solution: Ported torn edge/crack fake into active `Hecton_VisorGlitchACES.shader`; runtime signal dedupe/state frame now uses `TimeSliceScheduler.CurrentFrameId` with a cold fallback counter. Direct `Time.*` calls were removed from owned runtime and feature files.
Rejected Alternatives: Swapping renderer assets to a different post shader without proof, or leaving `Time.time` as a visual randomizer.
Scalability potential: Low quality keeps cheaper edge/refraction amplitude; high/ultra spend shader ALU on stronger crack masks and torn-edge distortion using the same continuous quality weight.
Hardware Impact: Corrects active route proof and removes scheduler drift risk. No direct frame-time saving claimed; prevents dead shader work and timing nondeterminism.

## Decision 016: Editor Gizmo Facade And Namespace Isolation

Problem: Task 18 visual matrix proof still depended on a runtime `MonoBehaviour` gizmo surface, and the visor runtime imported `Hecton8.World` even though the pass is presentation-owned.
Solution: Moved the live wound matrix draw path into `ScreenSpaceDecalTunerWindow` through `SceneView.duringSceneGui` and compiled the legacy gizmo component only under `UNITY_EDITOR`. Removed the `Hecton8.World` using directive from `DynamicDecalVaultRuntime`; AUP conversion now routes through `GlobalSignals.TryRuntimePositionToAup` and cached player snapshots.
Rejected Alternatives: Keeping a player-build debug component, creating GameObjects for proof, or preserving direct World namespace calls in the visor runtime because the root asmdef technically compiles them together.
Scalability potential: Low/middle/high/ultra runtime tiers are unchanged; the editor facade can draw 1..128 records for inspection without changing live capacity or shader payload shape.
Hardware Impact: Player runtime cost is 0 us for the editor gizmo. Compile-wall impact is reduced by removing a conceptual World namespace import from the visor-owned file; frame-time impact is not claimed.

## Decision 017: Shader Variant Warmup

Problem: The new screen-space wound shader and active noir mega-shader were bound by renderer assets, but the bootstrap warmup list only referenced `HectonDeferredCaustics.shadervariants`; first combat could still compile a visor wound variant.
Solution: Added `Hecton_VisorWounds` and `Hecton_VisorGlitchACES` pass-0 variants to the already bootstrap-referenced `HectonDeferredCaustics.shadervariants` collection. This uses the existing `GameBootstrapper.WarmConfiguredShaderVariantCollectionsAsync` route.
Rejected Alternatives: Calling `Shader.WarmupAllShaders`, adding runtime `Shader.Find`, creating a new collection and editing bootstrap scene dependencies, or relying on material creation as implicit warmup.
Scalability potential: All hardware tiers warm the same two fixed variants at boot; quality still controls record count/refraction, not shader keywords.
Hardware Impact: Prevents first-use shader hitch during combat. No steady-state frame-time gain claimed; boot warmup cost is paid in the existing staged bootstrap lane.

## Decision 018: Shader NaN Vaccination

Problem: Static HLSL review found `normalize` calls in the new wound pass and legacy uber route. Even if current seeds are usually non-zero, the zero-vector path is an unnecessary NaN risk.
Solution: Replaced both calls with explicit `seed * rsqrt(max(dot(seed, seed), 0.0001))`, matching the Burst-side denominator guard policy.
Rejected Alternatives: Leaving HLSL `normalize` because UV/local offsets include small biases, or relying on driver/compiler behavior.
Scalability potential: Same finite normal math at all quality weights; quality still only scales amplitude and active count.
Hardware Impact: No measurable steady-state saving claimed. This is long-session stability insurance that prevents non-finite crack/refraction offsets from poisoning the post stack.

## Decision 019: Pure Read Snapshot Accessors

Problem: Public editor/diagnostic readers named `TryGetTuning`, `TryGetRuntimeState`, and `TryGetLatestTelemetry` were still acquiring Vault locks and resolving `NativeArray` storage. That violates the project doctrine that read accessors must be pure and must not mutate global lock state.
Solution: Added immutable owner-phase snapshots for tuning, runtime state, and latest telemetry. The snapshots are written only when the owner already mutates authoritative buffers: default tuning seed, tuning write, visual-sync finalize, GPU upload telemetry write, telemetry push, and fault marking. The `TryGet*` methods now only copy the last snapshot and return a validity bit.
Rejected Alternatives: Keeping the locks because the calls are editor-facing; using `GlobalDataVault.TryGetLatestCreated()` as a diagnostic fallback; adding a managed event bridge for UI refresh.
Scalability potential: Low/middle/high/ultra all keep the same authority route. Snapshot reads do not affect `GlobalQualityWeight`, DTO layout, or capacity; only owner-phase math controls visual fidelity.
Hardware Impact: No steady-frame saving is claimed. The practical gain is removing rare lock contention and hidden mutation from editor telemetry/debug UI on low-end silicon; it also eliminates a reentrant tuning lock risk during signal ingestion.

## Decision 020: Vault Rebind Ring Hygiene

Problem: DataVault hot-swap reset cleared buffer handles and snapshots but left the telemetry cursor and cached camera position intact. A fresh ring should not inherit an old write index.
Solution: `ResetColdStorageForRebind()` now resets `_telemetryCursor` and `_lastCameraWorldPosition` together with Vault handles and snapshots.
Rejected Alternatives: Leaving cursor continuity across a new Vault instance; using a separate diagnostic search to infer the first non-empty slot.
Scalability potential: This does not alter quality tiers or wound capacity. It preserves forensic readability at every tier.
Hardware Impact: No frame-time saving claimed; it prevents misleading crash-ring ordering after Vault replacement.

## Decision 021: Packed Per-Decal Lifetime Restored

Problem: `DecalRequestSignal.LifetimeSeconds` and CSV material profiles were being parsed, but the active `VisorDecalDTO` slot at offset 72 was treated as a visual phase scalar, so the decay job ignored per-impact/per-profile lifetimes.
Solution: Preserve XML-mandated `BirthTime@72` in C# and HLSL while packing sanitized request/profile lifetime centiseconds into the high bits of `DecalTypeHash`. `DecayVisorDecalOpacityJob` unpacks lifetime and scales fade by `baseLifetime / lifetime` plus material-specific decay multipliers.
Rejected Alternatives: Occupying offset 72 with lifetime, packing lifetime into flags, adding another float and inflating the DTO stride, or ignoring CSV lifetime because base fade already existed.
Scalability potential: Low-tier still clamps active count and fade pressure continuously; high/ultra can keep important glass cracks and profile-tuned wound types longer without changing capacity or authority.
Hardware Impact: No extra memory bandwidth; the same 80B row is used. ALU cost is one guarded unpack/reciprocal per active decal in the bounded decay loop.

## Decision 022: Tuning Revision Non-Zero Wrap

Problem: `WriteTuning()` incremented `Revision` directly. If it wrapped from `uint.MaxValue` to 0, the next seed pass could treat a valid tuning row as empty.
Solution: Revision wraps to 1 instead of 0. The DTO layout and authority route stay unchanged.
Rejected Alternatives: Leaving the theoretical wrap because editor tuning is cold; widening the field to 64-bit and changing layout.
Scalability potential: No quality-tier behavior changes. The guard preserves hot-reload tuning continuity across all tiers.
Hardware Impact: No runtime frame saving; one cold branch in editor/tuning write path.

## Decision 023: BirthTime ABI Documentation Closure

Problem: The extracted XML block requires `BirthTime@72`; an earlier lifetime correction made code/docs drift from that contract.
Solution: Updated `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` and `Docs/ARCHITECTURE/SHINOBU_275_SCREEN_SPACE_VISOR_WOUNDS_ROUTE_CARD.md` so offset 72 is explicitly `BirthTime`, while lifetime is documented as packed high bits in `DecalTypeHash`.
Rejected Alternatives: Keeping lifetime in the offset-72 slot because it was convenient for decay; adding a new field for visual phase and inflating the DTO; hiding lifetime in flags.
Scalability potential: Low-tier can accelerate profile/request lifetimes through decay pressure while high/ultra retain long glass cracks and richer profile-tuned wounds. The quality scalar still changes cadence/count/intensity, not payload identity.
Hardware Impact: No frame-time saving. It prevents a reviewer or downstream integrator from rebinding offset 72 as lifetime and breaking XML shader ABI on every tier.

## Decision 024: NativeQueue Pending Fence

Problem: A static API audit found two queue lifetime hazards: `ResetStaticState()` disposed `_requests` before completing a pending visual-sync job, and public/mock ingress could query `_requests.Count` or enqueue while `GenerateVisorDecalMatricesJob` was still dequeuing the same `NativeQueue`.
Solution: `ResetStaticState()` now completes the pending visual-sync handle before unregistering/disposing `_requests`. `TryEnqueueRequest()` and mock generation now fail closed while `_pendingVisualSyncActive` is true and accumulate dropped-ingress telemetry instead of touching the queue.
Rejected Alternatives: Completing every visual-sync job synchronously to keep the queue open; adding a second persistent `NativeQueue` buffer during polish; allowing public enqueue during pending and trusting Unity safety handles.
Scalability potential: Low-tier devices can skip one frame of ingress during heavy thermal/pending conditions instead of blocking. High/ultra still process the next dispatcher window; quality controls capacity and fade, not queue ownership.
Hardware Impact: No frame-time saving claimed. This prevents rare NativeQueue safety exceptions or undefined queue access on domain reset/slow frames without adding heap allocations or another persistent container.

## Decision 025: Pending Queue Route Proof Sync

Problem: The queue ownership fix was visible in C# but route-card, architecture summary, binary payload ledger, and final log text still described only a generic fixed request queue. That makes a future integration pass likely to reintroduce enqueue/count access during the scheduled dequeue window.
Solution: Documented the pending visual-sync ownership rule in the route card, screen-space wound architecture note, binary payload ledger, and `LOG_SHINOBU_275.md`: ingress fails closed while `_pendingVisualSyncActive` is true, increments dropped-ingress telemetry, and reset/rebind force-completes before queue reset.
Rejected Alternatives: Treating the code as self-documenting; adding a blocking `Complete()` in public ingress to preserve every visual wound; allocating a second queue without profiler proof.
Scalability potential: Low and thermally constrained frames prefer bounded visual loss over blocking. Middle/high/ultra process the next dispatcher window with the same capacity/refraction quality curve.
Hardware Impact: Proof-only plus regression prevention. No runtime microsecond saving is claimed; the route avoids an avoidable main-thread fence and preserves zero-GC queue ownership semantics.

## Decision 026: Editor-Only Decal Buffer Acquire

Problem: The pure read-accessor doctrine was correctly applied to `TryGetTuning`, `TryGetRuntimeState`, and `TryGetLatestTelemetry`, but `TryAcquireDecalBufferRead` still exposed a Vault lock/unlock debug lane from the runtime type. The route card also described editor/gizmo readers as pure snapshots, which was false for the live matrix draw path.
Solution: Wrapped `TryAcquireDecalBufferRead` and `ReleaseDecalBufferRead` in `#if UNITY_EDITOR` and updated the route card/architecture note to label them as explicit editor-only acquire/release debug APIs, not runtime `TryGet*` accessors.
Rejected Alternatives: Returning a copied managed array for the gizmo, keeping the acquire method visible in player builds, or completing pending jobs from the read path to force a fresh snapshot.
Scalability potential: Low/middle/high/ultra runtime tiers are unchanged. The editor can inspect live matrices without adding player-build API surface or changing the continuous 8..128 wound capacity curve.
Hardware Impact: Player runtime cost remains 0 us for the live gizmo. The change removes an accidental player-build lock API and preserves pure snapshot reads for low-end silicon without claiming a measurable frame-time saving.

## Decision 027: Compile Wall Boundary

Problem: A guarded targeted `Hecton8.Core.csproj` build was necessary after the editor-only acquire patch, but the build failed before validating the wound route because unrelated project files reference missing types.
Solution: Treat the compile as blocked by external dependency errors and do not edit out-of-domain files. The failing files are `TerminalOsRuntime.cs`, `ContentRuntimeServices.cs`, `BulkheadContainmentJobs.cs`, `ScannerTool.cs`, and `RepairTool.cs`; none are owned by SHINOBU_275.
Rejected Alternatives: Fixing unrelated TerminalOS/content/construction/scanner/repair dependencies, running broader rebuild attempts, or reverting SHINOBU_275 code without an owned-file compiler error.
Scalability potential: No runtime behavior change. This protects domain isolation while preserving static proof for the screen-space wound route until the integrator clears the shared compile wall.
Hardware Impact: No frame-time claim. Avoids churn in unrelated assemblies and prevents a cross-domain dependency patch from expanding compile risk.

## Decision 028: BirthTime ABI Restoration With Packed Lifetime

Problem: The original SHINOBU_275 XML mandates `VisorDecalDTO.BirthTime` at offset 72, but the per-profile lifetime restoration had used that same slot for lifetime, creating a C#/HLSL/docs ABI conflict with the source directive.
Solution: Restore `BirthTime@72` in `VisorDecalDTO` and both wound shaders. Preserve request/profile lifetime by packing sanitized lifetime centiseconds into `DecalTypeHash` bits 8..23; bits 0..3 carry wound type and bits 4..7 carry atlas slice, so shader branch and atlas selection decode separate nibbles.
Rejected Alternatives: Expanding `VisorDecalDTO` beyond 80B; dropping CSV/request lifetimes; using `Flags` as a lifetime container; keeping lifetime in offset 72 and treating the XML as stale without evidence.
Scalability potential: Low-tier still fades packed-lifetime wounds faster through continuous thermal/quality fade pressure; middle/high/ultra retain longer glass/profile wounds without changing DTO stride, Vault BufferIDs, shader resource binding, or authority route.
Hardware Impact: No new memory bandwidth and no new persistent allocation. Cost is bit-pack on spawn and bit-unpack inside the bounded decay loop; shader cost is low-byte masking plus a nibble shift. This is cheaper than an 84B/96B DTO stride and preserves ARM64 alignment.

## Decision 029: Loop 11 Compile Probe Boundary

Problem: Loop 11 changed C# ABI code, so the compile wall needed one guarded probe once CPU and compiler-process gates were open.
Solution: Ran the targeted `Hecton8.Core.csproj` build with build servers disabled and single-process compilation. It failed only on external `ContentRuntimeServices.cs` missing `VRAMMonitor`, `VRAMPressureMonitor`, and `AssetLifecycleGovernor`; no owned SHINOBU_275 file appeared in compiler errors.
Rejected Alternatives: Skipping compile after C# changes; editing `ContentRuntimeServices.cs` out of domain; running broad rebuild loops after the external wall was already identified.
Scalability potential: No runtime behavior change. The decision preserves domain isolation and prevents unrelated content/VRAM dependencies from contaminating the visor wound route.
Hardware Impact: No frame-time claim. Compile probe consumed build time only after policy gates opened; no additional runtime allocations or shader variants were introduced.

## Decision 030: Type/Atlas Payload Separation

Problem: Loop 11 packed lifetime into `DecalTypeHash` but left the low byte semantically overloaded: material profiles wrote `AtlasSlice` through `DecalRequestSignal.MaterialHash`, and the matrix job then used that same value as shader type. A blood profile on atlas slice 7 could therefore render with the wrong procedural branch and decay scale.
Solution: Keep the 80B `VisorDecalDTO` unchanged and split the existing low payload byte: bits 0..3 are wound type, bits 4..7 are atlas slice, bits 8..23 remain lifetime centiseconds. `EnqueueSignalImpact()` now packs type and atlas before the Burst matrix job, `TryBuildMatrix()` normalizes legacy/raw request payloads, HLSL branch logic reads the low nibble, and atlas sampling reads the high nibble.
Rejected Alternatives: Adding `AtlasSlice` as a sixth DTO field, expanding the row to 84/96B, using `Flags` as an atlas side channel, or accepting profile-driven type drift as "visual only." All would either increase bandwidth, corrupt unrelated flag semantics, or break the one-payload proof.
Scalability potential: Low still processes 8 records with the same ALU; middle/high/ultra can use richer atlas sheets while procedural type behavior remains stable. GlobalQualityWeight continues to scale count/refraction/fade pressure, not payload identity.
Hardware Impact: No extra memory bandwidth and no persistent allocation. Cost is one nibble pack during signal ingestion and one nibble shift in shader atlas mode; this replaces a potential 16B-aligned DTO expansion and prevents wrong-branch overdraw/debug churn on low-end silicon.

## Decision 031: AUP Bridge And Tonemap Boundary Closure

Problem: A read-only subagent audit found three evidence mismatches and one render-pipeline risk: the route card omitted the retained `GlobalSignals` AUP bridge used for camera/runtime-position localization, an inspector tooltip described `DecalTypeHash` as a resolved slice index, a historical LOG self-audit used the old lifetime field name at offset 72, and `Hecton_VisorGlitchACES.shader` applied a manual ACES curve before URP Volume Tonemapping.
Solution: Documented `GlobalSignals.CurrentRuntimeOriginAup()` / `TryRuntimePositionToAup()` as a Core-owned read-only AUP bridge with visual-sync cadence, cached player/current-origin fallback, no publishing, and telemetry faulting. Corrected the atlas tooltip to the nibble ABI. Repaired LOG XML to `BirthTime@72` and removed the duplicate Loop 12 report. Removed the manual fragment tonemap curve so the Noir shader remains pre-tonemap and URP Volume owns final ACES.
Rejected Alternatives: Adding a new camera-AUP owner interface during polish without an existing source owner; leaving stale inspector/log wording because runtime code was correct; documenting double tonemapping as an accepted final-resolve route even though the feature injects `BeforeRenderingPostProcessing` and active Volume profiles already enable ACES.
Scalability potential: Low/middle/high/ultra keep the same wound DTO, SignalBus damage ingress, AUP bridge, and quality-scaled record count. Removing the pre-post ACES curve preserves the continuous grain/glitch/crack quality gates while preventing color compression from stacking differently across hardware profiles.
Hardware Impact: Documentation changes are proof-only. Shader change removes one per-pixel rational ACES approximation before URP post; on low-end silicon this saves a small ALU chain and, more importantly, prevents double-tonemap contrast loss under visor damage.

## Decision 032: Noir HDR Clamp And Visual Clock Closure

Problem: After removing the local ACES curve, the active `Hecton_VisorGlitchACES.shader` still clamped the HDR color path with `saturate(color)`, compressing linear values above 1.0 before URP Volume Tonemapping. The active Noir partial also still read `Time.frameCount`, `Time.timeAsDouble`, and `Time.time`, which made the SHINOBU_275 owned-route timing proof incomplete once the Noir shader became part of the visor wound presentation chain.
Solution: Removed color-path `saturate(color)` and preserved raw linear HDR while keeping finite/non-negative guards and retaining `saturate` for scalar masks and UVs. Replaced direct Unity Time reads in `HectonVisorUberPostFeature.Noir.cs` with `TimeSliceScheduler.CurrentFrameId` for frame/profile cadence, an owner-local fallback frame counter for cold/editor gaps, and finite `SystemDispatcher.CurrentFrameDeltaTime` accumulation for wrapped grain/glitch phase. Updated SHINOBU_235 and SHINOBU_275 architecture docs plus the binary payload ledger.
Rejected Alternatives: Leaving the clamp because it was cheaper than debugging HDR; using Unity `Time.*` because the effect is presentation-only; adding a new global clock service from this polish pass; clamping HDR at 1.0 and relying on post-ACES grain to restore contrast. These would either keep false evidence, introduce new global surface, or visibly flatten trauma/noir highlights on high-tier profiles.
Scalability potential: Low devices keep the same continuous GlobalQualityWeight-driven grain/glitch/crack admission and avoid extra shader variants. Middle/high/ultra preserve HDR headroom for stronger crack tint, torn-edge heat, and stress chroma before the single URP ACES owner resolves the frame. Quality still changes fidelity, cadence, and intensity only; it does not alter DTO layout, save identity, or authority route.
Hardware Impact: Removing the color clamp saves a tiny per-pixel clamp chain but no measured frame-time claim is made. The main value is correctness: MX350 avoids double color compression, high-end profiles keep visual overkill headroom, and the active route now has no direct Unity Time dependency in the focused SHINOBU_275 scan.

## Decision 033: Loop 15 Hot Ingress And Tiny Job Closure

Problem: The RenderGraph/GPU upload audit found that runtime public wound ingress still called `EnsureInitialized()`. A first impact before `DeferredDecalPass.Create()` could therefore poll `GlobalRegistry`, allocate/prewarm the `NativeQueue`, acquire Vault handles, and seed tuning from a producer call. The same loop also found active Noir one-row `IJob.Run()` wrappers and old `Time.frameCount` reads in the touched host file.
Solution: `TryEnqueueRuntimeImpact()` and `TryEnqueueAupImpact()` now fail closed on `IsInitializedForRead()` and cannot perform cold work. Cold creation remains in `TryInitializeColdStorage()`, feature create, DataVault rebind, editor/mock tooling, CSV/profile load, and fault/bootstrap lanes. Active Noir constant generation/upload moved to dispatcher `LateFrameTick`; `AddRenderPasses()` only consumes an already valid double-buffered constant buffer. One-row Noir mock/parameter jobs were collapsed into direct scalar owner-phase methods. The shared host file used the dispatcher frame source for reconstruction telemetry, the then-existing fluid path, and depthless-TBDR cache cadence; Loop 18 later removed the concrete fluid path entirely.
Rejected Alternatives: Letting producer ingress initialize the route; adding a blocking same-frame fence to preserve every first impact; retaining one-record Burst jobs for cosmetic math; excluding the touched host file from the Time scan; adding a new global presentation clock. All either hide cold work, add scheduler overhead, or expand global surface without proof.
Scalability potential: Low devices fail closed during missing/cold presentation setup instead of spiking on queue/Vault creation. Middle/high/ultra keep the same 8..128 wound curve and the same HDR Noir shader route. Quality continues to scale capacity, fade pressure, profile cadence, and shader intensity only; it does not change DTO layout, BufferID identity, or rollback/save authority.
Hardware Impact: Prevents a first-impact cold spike on i3/MX350 and avoids tiny-job scheduling overhead for one CBuffer row. No measured frame-time claim is made; profiler/GC proof remains pending behind the CPU/build gate.

## Decision 034: Loop 16 Evidence Ordering And Proof Refresh

Problem: The Loop 15 report had been appended, but a mechanical insertion placed it above older Loop 7/6/reentry blocks in `LOG_SHINOBU_275.md`. That violates the repository's top-old/bottom-new reporting protocol and makes the final proof chain ambiguous even when the source patch is correct.
Solution: Move the single Loop 15 report block to EOF, verify there is no duplicate Loop 15 block, rerun the static decal inquisition scanner, rerun focused forbidden-token scans, rerun tiny Noir job scans, rerun JSON validation, and resample the compile gate. No C# or shader behavior changed in this evidence-ordering pass.
Rejected Alternatives: Leaving the report order wrong because the source code was already fixed; appending a second Loop 15 copy; deleting old historical report blocks; launching a build while CPU policy blocks it.
Scalability potential: No runtime tier changes. This preserves proof integrity for low/middle/high/ultra behavior already documented in Loop 15.
Hardware Impact: Proof-only. Latest scanner PASS is 2026-05-21T18:35:14Z with 0 active object/URP decal violations; build remains blocked because CPU sampled 100% and policy forbids compilation above 50%.

## Decision 035: Loop 17 Cached Player Snapshot Host Route

Problem: The shared visor host still used `PlayerRuntimeContextService.TryGetActiveRuntimeContext()` from render enqueue and kept a direct `Hecton8.Gameplay` namespace import. That is a static context fallback in a presentation render path, and it weakens the claim that the active route consumes cached owner snapshots instead of searching/syncing context state.
Solution: Remove the static player-context fallback and explicit Gameplay namespace import. `TryBuildRuntimeState()` now reads cached `_noirPlayerContext` only, pulls survival status from `TryGetSurvivalRuntimeState`, pulls hull stress from `TryGetMovementStressRuntimeState`, and keeps wet-lens as a presentation-only read from the cached movement owner exposed by the cached context. If the cached context is missing, the pass fails closed.
Rejected Alternatives: Keeping the static fallback for visual convenience; dropping wet-lens visuals entirely; adding a new cross-domain player presentation interface during a polish pass; polling `GlobalRegistry.Player` from render enqueue.
Scalability potential: Low devices avoid a hidden context fallback in the render path and fail closed if bootstrap has not published the cached context. Middle/high/ultra retain the same visual signals and continuous quality curve once the cached context exists.
Hardware Impact: Removes a potential scene/context synchronization path from render enqueue. No measured frame-time claim is made; later Loop 18 scanner PASS is 2026-05-21T18:52:59Z and build remains blocked by the 100% CPU gate.

## Decision 036: Loop 18 Concrete Fluid Boundary Removal

Problem: The same shared visor host still imported `Hecton8.Physics`, cached `HectonFluidEngine`, handled `GlobalRegistryServiceSlot.FluidRuntime`, and sampled `TrySampleMaelstromWarp()` for a cosmetic pressure warp. That creates a concrete sibling-domain edge in the presentation host without a contracts-only fluid read model.
Solution: Remove the concrete fluid owner field, registry replacement handling, rebind cadence, and maelstrom warp sample. Preserve visual pressure by adding `ResolvePressureSurgeVisual01()`, a deterministic screen-space scalar derived from already-owned presentation inputs: ambient pressure, cached hull stress, and the continuous low-tier weight. It only increases stress/active-signal drive; it does not change gameplay pressure truth or DTO layout.
Rejected Alternatives: Adding a new core contract in a polish loop; using reflection/dynamic dispatch to hide the dependency; keeping the direct fluid read because the effect is visual-only; deleting pressure trauma entirely. These either expand global surface, keep hidden coupling, or reduce visual feedback unnecessarily.
Scalability potential: Low devices get the cheapest scalar surge curve with no fluid rebind or service read. Middle/high/ultra preserve trauma intensity through the same pressure/stress signals and can still spend shader ALU on Noir crack/glitch/wound effects. A future fluid contract can add richer maelstrom data without changing SHINOBU_275 DTO, save identity, or authority route.
Hardware Impact: Removes one cached concrete Physics service edge and a 30-frame rebind path from the touched host. No measured frame-time claim is made; this is compile-wall and authority-boundary hardening.

## Decision 037: Loop 19 Reconstruction Hot-Path Closure

Problem: Loop 18 left three active reconstruction debts: a stale direct-executed mapped-upload `IJob`, render-enqueue access to CSV/Vault profile lanes, and a single reconstruction CBuffer mutated before RenderGraph consumed it. The legacy visor shader also still used binary low-tier gates that produced visible quality snapping.
Solution: Delete the fake `VisorWoundMappedUploadJob` and copy mapped upload rows through a direct owner `UnsafeUtility.MemCpy` helper. Keep CSV load in cold create/hot-swap paths only, snapshot parsed `NoirAestheticProfileDTO` rows into a fixed cold cache, and select profiles without Vault locks from `AddRenderPasses()`. Reconstruction constants now use A/B `GraphicsBuffer` targets and publish an active buffer; AB split is set inside the RenderGraph raster command, not through material mutation during enqueue. Raw history availability uses the cached camera history read accessor. Legacy shader quality gates now use continuous `smoothstep`/`lerp` weights.
Rejected Alternatives: Scheduling or directly executing a one-row Burst job for mapped copy; locking `GlobalDataVault` profile rows from render enqueue; retrying CSV file IO from `AddRenderPasses()`; expanding the shared reconstruction DTO for AB split during polish; retaining hard `lowTier > 0.5` shader branches.
Scalability potential: Low devices continuously shed heat haze, light shaft intensity/sample budget, comfort edge detail, and droplet refraction without snap transitions; middle devices retain partial effects; high/ultra preserve richer reconstruction grain/chroma/vignette and visual-overkill shafts through the same DTO, BufferID, and authority route.
Hardware Impact: Removes a fake scheduler-shaped upload path, one render-frame profile Vault lock, one render-frame CSV retry, a single-buffer CBuffer overwrite risk, and per-frame history component lookup. No measured frame-time claim; static scanner PASS is 2026-05-21T19:12:28Z and compile is blocked by host policy: first `dotnet`/`VBCSCompiler` were active, then CPU sampled 57.95% with `VBCSCompiler` still active.

## Decision 038: Loop 20 RenderGraph And Dispatcher Ownership Closure

Problem: Read-only subagents found that Loop 19 still left owned hazards: the mapped upload helper was scoped under `GenerateVisorDecalMatricesJob` while `DeferredDecalPass` called `DynamicDecalVaultRuntime.CopyDecalsToMappedUploadBuffer`; reconstruction constants/Vault telemetry were still produced from `AddRenderPasses()`; visor post and wound atlas properties were still material mutations outside the RenderGraph render function; active shaders used engine `_Time`; and Noir color profile cache misses resolved the Vault profile array from LateFrame.
Solution: Move `CopyDecalsToMappedUploadBuffer()` onto `DynamicDecalVaultRuntime`. Make `AddRenderPasses()` stage only camera/runtime/reconstruction input and consume the last active CBuffer; `LateFrameTick()` now builds/uploads reconstruction constants, writes the Vault mirror, records telemetry, and handles the black-box dump. Move visor post scalar/vector/texture state into `PostPassData` and bind it with `RasterCommandBuffer.SetGlobal*`; move legacy shader trauma scalars out of `UnityPerMaterial`. Bind the wound atlas inside the wound raster function. Replace shader `_Time` reads with `_HectonUberVisualTime` / `_H8UberNoirVisualTime` published from the dispatcher-wrapped visual clock. Snapshot `NoirColorProfileDTO` rows into a fixed cold cache after CSV parse so LateFrame selection does not resolve profile Vault rows.
Rejected Alternatives: Restoring a fake one-row mapped upload job; running reconstruction upload from render enqueue because it is presentation-only; keeping dirty-gated material property writes; using Unity shader `_Time` for "only visual" noise; resolving Vault profile arrays on LateFrame cache misses; adding new DTO fields for visual time.
Scalability potential: Low devices avoid render-record Vault locks, material mutation, and profile NativeArray reads while continuing to shed heat haze, refraction, shafts, grain, and reconstruction detail continuously. Middle devices keep partial effects. High/ultra still spend shader ALU on visual-overkill shafts, reconstruction grain/chroma, and wound refraction without changing DTO layout, BufferID identity, save state, or authority route.
Hardware Impact: Expected low-end gain is risk reduction rather than a measured frame-time claim: render enqueue loses CBuffer mapping, Vault telemetry writes, file-dump possibility, material property mutation, and hot profile NativeArray resolve. Static scanner PASS is 2026-05-21T19:34:06Z. Compile remains blocked by host policy because CPU sampled 78.57% despite no active compiler process.

## Decision 039: Loop 21 Black-Box Dump Writer Hygiene

Problem: `DynamicDecalVaultRuntime.DumpBlackBox()` still used `BinaryWriter`. The path is crash/diagnostic rather than steady-frame, but it added a managed writer wrapper and hid the exact little-endian dump layout behind per-field API calls.
Solution: Replace `BinaryWriter` with stack-span writes: a fixed 16-byte header (`DumpMagic`, reason flags, telemetry capacity, cursor) and fixed 64-byte telemetry rows. Float fields are emitted through `math.asuint` and all `uint` values are written byte-by-byte in little-endian order. The row keeps the explicit 64B telemetry stride by leaving bytes 56..63 as zero pad.
Rejected Alternatives: Keeping `BinaryWriter` because the crash path is cold; writing raw native struct memory with `UnsafeUtility.MemCpy` and trusting platform endianness; compacting the row to 56B and contradicting the route card.
Scalability potential: No visual tier behavior changes. Low devices get a deterministic, cheap fault artifact when a non-finite/upload fault happens; middle/high/ultra keep the same 300-frame forensic ring and the same shader/Vault authority route.
Hardware Impact: No steady-frame saving claimed. Crash-path output avoids one managed writer object and per-field writer dispatch, and the dump format is explicit enough for a raw parser to consume without reflection or BinaryReader assumptions. Static scanner PASS is 2026-05-21T19:41:50Z. Compile remains blocked by host policy: CPU sampled 100%/83% with `VBCSCompiler` PID 32428 active, then 73% with no compiler process returned.

## Decision 040: Loop 22 RenderGraph Texture Binding And Non-Throwing State Row

Problem: The Loop 20 claim that visor post textures and wound atlas state were command-buffer bound was not fully true. `DeferredDecalPass` still called `Material.SetTexture` for the wound atlas inside the raster render function, and `HectonVisorUberPostFeature.BindPostShaderParameters()` still mutated four texture slots on the material. `DynamicDecalVaultRuntime` also retained a managed `InvalidOperationException` in the runtime state row helper.
Solution: Bind the wound atlas, crack texture, lens dirt texture, blue-noise texture, and VR comfort mask through `RasterCommandBuffer.SetGlobalTexture` using existing pass data. Replace the throwing state-row helper with a non-throwing pointer guard; invalid state row access marks the existing layout fault bit and returns false from visual sync.
Rejected Alternatives: Keeping `Material.SetTexture` because it happens inside a render function; moving texture state back to setup; adding a new exception/logging surface; or expanding the DTO/Vault route to work around a one-row state read. These keep hidden render-state mutation or managed exception behavior in the presentation hot lane.
Scalability potential: Low devices avoid extra material dirtiness while keeping the same 8..128 wound count curve and cheap shader masks. Middle/high/ultra keep the same visual-overkill texture masks, wound atlas, cracks, grain, and refraction without changing DTO layout, BufferID identity, save/rollback authority, or quality curve.
Hardware Impact: No measured frame-time claim. Expected low-end effect is render-record risk reduction and failure-path hygiene: no material texture mutation from the two owned raster functions, and no managed exception allocation if the Vault state row is invalid. Static scanner PASS is 2026-05-21T20:02:57Z. Compile remains blocked by host policy because CPU sampled 51% and compiler-process count returned 2.

## Decision 041: Loop 23 Cold-State Seeding And Clear-Memory Vault Entry

Problem: `DynamicDecalVaultRuntime` still requested decal instance/upload/profile Vault buffers with `UninitializedMemory`, then corrected missing runtime state from VISUAL_SYNC by directly executing `ClearDecalsJob.Execute(i)` up to 128 times on the main thread. That made the normal first visual frame carry a cold corrective branch and allowed stale payload bits to exist until the first sync touched them.
Solution: Request the presentation-owned decal instance, upload scratch, tuning, telemetry, and material profile buffers as `ClearMemory`. Seed `DecalRuntimeStateDTO` during cold initialization with `RuntimeInitializedFlag`, current continuous `GlobalQualityWeight`, thermal pressure, derived max-active count, and normal refraction intensity. Replace the visual-sync direct job-execute clear loop with a cold/fallback `UnsafeUtility.MemClear` helper for the instance and upload buffers.
Rejected Alternatives: Keeping `UninitializedMemory` and relying on a visual-sync clear loop; scheduling a one-frame clear job from the first render tick; treating stale buffer contents as harmless because active count starts at zero. Those options either keep a hot corrective branch, add scheduler work, or allow stale active flags to be counted by decay/upload if state is externally reset.
Scalability potential: Low devices enter the wound route with zeroed buffers and a quality-scaled active count instead of spending first-frame CPU on corrective clearing. Middle/high/ultra retain the same 8..128 continuous capacity curve and can still spend shader work on wound refraction/crack detail without changing DTO layout or authority route.
Hardware Impact: No measured frame-time claim. Expected low-end effect is removal of up to 128 direct `ClearDecalsJob.Execute` calls from first visual sync and deterministic zeroed Vault rows before shader/upload consumption. Static scanner PASS is 2026-05-21T20:11:44Z. Compile remains blocked by host policy because CPU sampled 97% and compiler-process count returned 2.

## Decision 042: Loop 24 Designer Facade Provenance And CSV Schema Gate

Problem: `ScreenSpaceDecalTunerWindow` exposed sliders, a mock load button, and CSV load, but it did not show the designer-facing proof required by the facade mandate: source route, schema id/hash, binary/DataMonolith caveat, validation state, row count, or byte-layout summary. Wrong-column CSV files could reach the cold profile parser and rely on default/fallback fields instead of failing at the authoring boundary.
Solution: Add editor-only bridge metadata labels for source CSV, schema `H8_VISOR_DECAL_PROFILE_CSV_V1`, lowercase FNV-1a schema hash, runtime Vault route, DataMonolith output caveat, last validation state, row count, selected header hash, and explicit `VisorDecalDTO` / `DecalMaterialProfileDTO` byte layouts. CSV load now computes the selected file's header hash and rejects schema mismatch before entering the cold Vault loader.
Rejected Alternatives: Leaving the facade as sliders only; claiming a production `.h8bin` bake that does not exist in this workspace; moving validation into runtime player paths; adding a new binary compiler in the visor domain during polish.
Scalability potential: Low devices gain no runtime cost because this is editor-only. Middle/high/ultra keep the same visual-overkill material profile route once cold-loaded; quality scaling still changes capacity/refraction/fade only and does not alter DTO layout or authority.
Hardware Impact: No frame-time claim. The patch prevents bad authoring data from seeding the 71495 profile table and documents that `static_data.h8bin` remains unclaimed by this facade. Static scanner PASS is 2026-05-21T20:24:06Z. Compile remains blocked by host policy because CPU sampled 89% with `dotnet` PID 37944 and `VBCSCompiler` PID 9584 active.

## Decision 043: Loop 25 Active RenderGraph Texture Binding Correction

Problem: Focused render-binding verification contradicted the older Loop 22 claim: `DeferredDecalPass` still called `Material.SetTexture` for `_GlobalVisorWoundAtlas`, and `HectonVisorUberPostFeature` still called `Material.SetTexture` for crack, lens dirt, blue-noise, and VR comfort textures.
Solution: Bind all five textures through `RasterCommandBuffer.SetGlobalTexture` using existing property IDs inside the RenderGraph raster functions. No shader property name, DTO, BufferID, or authority route changed.
Rejected Alternatives: Keeping string-name material mutation because it is inside a render function; moving the bindings back to setup; adding new shader property IDs; widening pass data; claiming the stale Loop 22 proof as sufficient.
Scalability potential: Low devices avoid material dirtiness/state mutation in the presentation render path. Middle/high/ultra keep the same texture-driven visor crack, dirt, blue-noise, and VR comfort visual detail with unchanged continuous quality gates.
Hardware Impact: No measured frame-time claim. Expected effect is render-state risk reduction and fewer material dirty paths. Static scanner PASS is 2026-05-21T20:31:51Z. Compile remains blocked by host policy because CPU sampled 100% with 10 compiler processes.

## Decision 044: Loop 26 Disk-State Render Binding Correction

Problem: The prior Loop 25 report was contradicted by a fresh disk scan. `DeferredDecalPass` still contained `Material.SetTexture(ShaderConstants.DecalAtlasName, data.DecalAtlas)`, and `HectonVisorUberPostFeature.BindPostShaderParameters()` still contained four `Material.SetTexture` calls for crack, lens dirt, blue-noise, and VR comfort textures. The stale string-name constants also remained in the owned source, so the regression path was still compile-valid.
Solution: Replace the five actual disk calls with `RasterCommandBuffer.SetGlobalTexture` using the existing integer property IDs. Delete `DecalAtlasName`, `CrackTextureName`, `LensDirtTextureName`, `BlueNoiseTextureName`, and `VrComfortMaskTextureName` so the owned RenderGraph sources no longer keep string shader-property texture mutation helpers.
Rejected Alternatives: Treating the old Loop 25 log as authoritative; leaving unused string-name constants because the current calls could be patched; moving texture binding to setup; widening pass data or changing shader property names; launching a build while CPU/compiler policy blocks it.
Scalability potential: Low devices avoid material dirtiness/state mutation in the presentation render path while retaining the same continuous 8..128 wound capacity and same low-tier texture fallback flags. Middle/high/ultra keep the same atlas/crack/dirt/blue-noise/comfort-mask visuals through the existing quality curves and shader parameters.
Hardware Impact: No measured frame-time claim. Expected low-end effect is render-state hazard removal and compile-surface hardening. Static scanner PASS is 2026-05-21T23:09:36Z. Focused render-binding scan is clean. Compile remains blocked by host policy: CPU sampled 52.75% with `dotnet` PID 13796 and `VBCSCompiler` PID 41344 active.

## Decision 045: Loop 27 Signal Ingress Profile And Tuning Snapshot Amortization

Problem: `TryIngestGlobalImpactSignals()` handled signal snapshots inside the visual-sync owner lock, but each accepted signal still called `TryResolveMaterialProfile()` which resolved the material-profile Vault buffer again. Each signal also copied the live tuning snapshot through `ResolveLiveTuning()` even though tuning is immutable for that snapshot pass.
Solution: Resolve the material-profile `NativeArray<DecalMaterialProfileDTO>` once at the start of `TryIngestGlobalImpactSignals()` when `_materialProfileCount > 0`, then pass the array and capacity into `EnqueueSignalImpact()` / `TryResolveMaterialProfile()`. Copy `DecalTuningDTO` once per signal-snapshot pass and pass it into `EnqueueSignalImpact()`.
Rejected Alternatives: Leaving per-signal Vault descriptor resolve because the handle is cached; adding a persistent private material-profile array; locking a new buffer from the signal loop; moving CSV/profile ownership into signal producers; launching a build while CPU/compiler policy blocks it.
Scalability potential: Low devices with dense impact spam avoid repeated descriptor reads and tuning DTO copies while keeping the same continuous quality capacity and decay curves. Middle/high/ultra retain full atlas/profile-driven wound variety and visual-overkill texture detail without changing BufferIDs, DTO layout, SignalBus payloads, or authority route.
Hardware Impact: No measured frame-time claim. Expected low-end effect is O(1) profile buffer resolve per snapshot instead of O(N_signals) and one 32B tuning DTO copy per snapshot instead of per accepted signal. Static scanner PASS is 2026-05-21T23:17:46Z. Compile remains blocked by host policy: CPU sampled 92.18% with `VBCSCompiler` PID 7372 active.
