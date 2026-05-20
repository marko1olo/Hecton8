# SHINOBU_134 Rationale

Status: PENDING VERIFICATION

## Decision 001 - Ownership Before Mutation

Problem: Shadow-culling assignment overlaps MeshRenderer, Unity GPU Resident Drawer, manual BRG, GlobalDataVault, rollback netcode, lighting probes, and third-party renderers.
Solution: Inspect first-party rendering and native-buffer systems before deleting or replacing anything. Keep third-party files untouched unless they are direct first-party contamination in the assigned domain.
Rejected Alternatives: Blind removal of every shadowCastingMode reference would damage Amplify/GPUInstancer/Bakery/Crest vendor code and violate third-party integrity. Raw prefab/YAML edits are rejected until file IDs and ownership are proven.
Scalability potential: Low uses aggressive mathematical no-shadow flags and short distances; Middle keeps directional shadows with dither; High extends residency and fade bands; Ultra spends saved cycles on longer shadow residency and richer debug/probe visibility.
Hardware Impact: Expected gain is GPU shadow-pass draw reduction. Exact i3/MX350 gain is PENDING VERIFICATION until Frame Debugger/Profiler data exists.

## Decision 002 - Data Contract First

Problem: The assignment requires Burst processing over 50,000 instances and explicitly forbids property DTOs and misaligned structs.
Solution: Implement or locate a first-party shadow-culling data contract with explicit 32-byte ShadowCullStateDTO, raw fields only, AUP double3 arrays, and a static layout validator.
Rejected Alternatives: C# properties, classes, and implicit sequential layout are rejected because they invite CS1612 copies and ARM64 offset drift.
Scalability potential: Low can process fewer effective shadow candidates through stride/aggression; Middle keeps full 50k evaluation with shorter max distance; High/Ultra can keep longer fade bands and more shadow-only candidates.
Hardware Impact: 32-byte cull state gives predictable 1.6 MB for 50,000 states, cache-linear traversal, and no managed heap pressure on i3/MX350.

## Decision 003 - Vault-Owned Shadow Lane Instead Of Renderer Mutation

Problem: Per-renderer `shadowCastingMode` or `forceRenderingOff` writes inside frame cadence would force Unity-side renderer state rebuilds and couple this task to MeshRenderer/LODGroup ownership.
Solution: Own only the shadow decision data: Vault IDs 71340..71350 hold instances, states, illumination, frustum planes, counters, telemetry, runtime tuning, profile rules, CSV scratch, HZB depth tiles, and indirect args. VisualSync publishes GPU buffers from completed Vault state.
Rejected Alternatives: Direct `Renderer.shadowCastingMode` loops, LODGroup YAML mutation, or direct calls into another rendering sibling assembly were rejected because they break the compile wall and route authority through Unity black boxes.
Scalability potential: Low collapses max shadow distance and uses aggressive SDF/HZB gates; Middle keeps directional caster silhouettes with dither; High extends shadow residency; Ultra preserves small/point casters and richer shadow-only margins.
Hardware Impact: Expected win is fewer shadow-map submissions and less CPU renderer-state churn. Exact i3/MX350 gain is PENDING VERIFICATION until Frame Debugger and Profiler captures exist.

## Decision 004 - Dispatcher Split For Non-Blocking Job Chaining

Problem: Scheduling and completing shadow work from a single VisualSync callback would either stall VisualSync or leave the SystemDispatcher dependency graph blind to the Burst work.
Solution: Register two cold phase adapter objects: a Simulation adapter schedules and returns the culling `JobHandle`; a VisualSync adapter only commits completed results after the dispatcher has had a chance to complete the combined simulation fence.
Rejected Alternatives: `Schedule()+Complete()` in one Tick, arbitrary `Complete()` in VisualSync without `IsCompleted`, and unmanaged orphaned handles. Force-complete remains only in explicit mock run/teardown paths.
Scalability potential: Low can skip scheduling when a previous job is still pending; Middle/High keep one-frame shadow-state latency; Ultra can spend the completed state on wider shadow-only ranges without blocking the frame.
Hardware Impact: Avoids serializing worker threads on low-end CPUs. Exact microsecond saving is PENDING VERIFICATION.

## Decision 005 - HZB And SDF As Presentation Fakes

Problem: Real CPU raycasts or voxel raymarches for every caster would turn shadow culling into a physics/lighting simulation and violate the 0.1ms suspicion threshold.
Solution: Use a 16x16 max Vault HZB tile grid plus per-instance `OcclusionScalar` as the owner-local input. The Burst job maps AUP-local AABBs to HZB tiles and clears shadow flags when front depth is behind the tile depth; SDF occlusion is a scalar threshold, not a ray march.
Rejected Alternatives: MeshCollider/Physics.Raycast tests, per-caster voxel marching, or waiting for GPU readback from another owner before implementing the culling route. Real GPU HZB can later fill the same `ShadowCullHzbTileDTO` buffer.
Scalability potential: Low uses 8-ish effective HZB resolution and a tight occlusion bias for aggressive load shedding; Middle blends toward 12-14 tiles; High/Ultra use 16 tiles and conservative bias while spending saved CPU on visual overkill in shaders.
Hardware Impact: Big-O remains O(n) over casters plus O(t) HZB tiles, instead of O(n*rays*steps). For 50k casters, eliminating even four voxel samples per caster avoids 200k sample operations; measured proof absent.

## Decision 006 - Indirect Args Are Data, Not A Managed Visible List

Problem: Building a managed visible-index list after culling would allocate or at least force CPU-side list maintenance, then duplicate data the GPU can consume as a count.
Solution: `ReduceShadowCullTelemetryJob` counts visible shadow casters into a 64-byte counter record; `BuildShadowIndirectArgsJob` writes one `ShadowCullIndirectArgsDTO` row; VisualSync copies it through `GraphicsBuffer.LockBufferForWrite`.
Rejected Alternatives: `List<int>` visible casters, `Renderer.enabled` toggles, or CPU loop instantiating procedural shadow geometry.
Scalability potential: Low emits fewer indirect instances from aggressive flags; Middle/High keep one indirect row stable; Ultra can consume the same row with richer shader work and longer residency.
Hardware Impact: Avoids managed list walk and PCIe upload of sparse visible IDs. Exact microsecond saving is PENDING VERIFICATION.

## Decision 007 - ARM64 And False-Sharing Padding

Problem: The original 32-byte shadow state is cache-friendly, but shared counters written around job completion can sit next to unrelated memory and create false-sharing risk.
Solution: Keep `ShadowCullStateDTO` exactly 32 bytes per XML contract and make `ShadowCullCountersDTO` exactly 64 bytes with explicit HZB/SDF/visible/profile/state-hash fields. No `[Pack=1]` exists in owned files.
Rejected Alternatives: Sequential counters with implicit padding, runtime `bool` fields in DTOs, or packing the state to fewer bytes.
Scalability potential: Low/Medium benefit from predictable cache-line ownership; High/Ultra can add telemetry detail through separate aligned records instead of bloating the primary 32-byte state.
Hardware Impact: Protects ARM64/Quest-style alignment and reduces counter false-sharing risk. Exact gain is PENDING VERIFICATION.

## Decision 008 - Verification Boundary Is Static Until Unity Runs

Problem: A prior full solution build failed in unrelated Core/Visor/Equipment/Ecosystem missing DTO/type dependencies, and the user explicitly forbids launching another build until it is technically needed.
Solution: Use static verification now: source scans for forbidden hot-path constructs, Burst attributes, asmdef references, meta GUID uniqueness, and `git diff --check`. Defer Unity import/profiler/build proof until the build gate is open or an integrator clears unrelated dependencies.
Rejected Alternatives: Running `dotnet build` as a ritual under known unrelated blockers, or reporting profiler/GC numbers without runtime capture.
Scalability potential: No runtime visual claim is made from static scans; the implemented math exposes Low, Middle, High, and Ultra behavior for later profiler validation.
Hardware Impact: No measured hardware impact is claimed. Estimated savings remain estimates until Frame Debugger/Profiler evidence exists.

## Decision 009 - Layout Validator Expanded Beyond The Primary State DTO

Problem: The XML task names `ShadowCullStateDTO`, but the ULTRA mandate requires proof for every primary DTO involved in counters, HZB, indirect args, telemetry, runtime state, editor snapshots, and CSV profile ingestion. A validator that only checks the 32-byte state could miss a later ARM64 drift in the 64-byte counter or indirect argument records.
Solution: Extend `AbyssalShadowLayoutAudit` with `ValidateAllLayouts()` and explicit offset checks for state, instance, counters, telemetry, runtime state, HZB tile, indirect args, tuner snapshot, profile rule, and CSV parse result DTOs. The editor facade now calls the aggregate validator; the live gizmo method is wrapped in `#if UNITY_EDITOR` so player builds keep the debug hook out of runtime IL.
Rejected Alternatives: Leaving the wider structs documented only in the log, or adding a runtime per-frame assertion. Documentation-only proof rots; hot-path assertions violate the 0.1 ms suspicion threshold.
Scalability potential: Low and Middle devices benefit because layout drift is caught before it becomes ARM64 unaligned memory traffic; High and Ultra retain richer telemetry/indirect proof without expanding the 32-byte primary state.
Hardware Impact: No new hot-path cost. The expected benefit is prevention: a 64-byte counter cache-line guarantee and 32-byte state stride are statically guarded before Quest/ARM64 profiling.

## Decision 010 - External Producer Dependency Ingress

Problem: The shadow lane consumed illumination, HZB, frustum, and instance buffers, but the first pass only had internal mock seeding. Without a producer ingress, the runtime could silently overwrite Lighting/HZB/World data with fallback mocks or schedule against producer-owned writes without a clear dependency route.
Solution: Add `TryResolveProducerBuffers(...)` and `RegisterExternalProducerDependency(...)` on the runtime plus static active facades. External owners can resolve the Vault buffers, schedule their own producer job, then hand this runtime a `JobHandle`, active instance count, HZB tile count, and ownership flags. `ScheduleCullingPass` now uses `JobHandle.CombineDependencies(dependsOn, producerDependency)` before mock/HZB/evaluate jobs and suppresses mock regeneration when external instance/HZB data is marked written. A published GPU-buffer facade exposes the completed state and indirect args to the renderer without a sibling assembly reference.
Rejected Alternatives: Direct `using Hecton8.Lighting` or `using Hecton8.World` callbacks, polling other systems from the culling tick, or keeping a managed visible-instance list. All of those break compile-wall routing or duplicate data truth.
Scalability potential: Low can accept sparse external active counts and coarse HZB tile counts; Middle/High can publish fuller HZB grids; Ultra can feed richer instance/probe data while the culling math stays the same O(n + t) lane.
Hardware Impact: No measured frame claim. Expected impact is fewer stale/uninitialized candidates evaluated when external owners publish a real active count, and no main-thread stall from producer completion because the handle is chained rather than completed immediately.

## Decision 011 - HZB Camera-Basis Correction

Problem: The HZB fake originally mapped AUP-local candidates with `center.xy`, which only matches a screen-depth pyramid when the camera basis is aligned to world axes. A real GPU HZB readback owner needs the culler to use the same camera-local right/up/forward basis that produced the depth tiles.
Solution: Add `SetHzbViewBasis(float3 right, float3 up, float3 forward)` and `SetActiveHzbViewBasis(...)`. `EvaluateShadowCullingJob` now receives sanitized HZB basis vectors and computes tile UV from `dot(center, right/up)` and depth from `dot(center, forward) - dot(abs(forward), extents)`. The math remains AUP-local and guarded by `math.max` and `math.normalizesafe`.
Rejected Alternatives: Treating HZB as world-top-down occlusion forever, adding a full camera matrix DTO without a current producer contract, or CPU raycasting against terrain. The basis route is the smallest correction that aligns CPU cull math with GPU HZB tiles.
Scalability potential: Low can keep coarse 8-ish tile grids with the corrected basis; Middle/High/Ultra can consume denser external HZB grids without changing the culler data contract.
Hardware Impact: Adds three dot products per HZB-tested candidate. Expected benefit is correctness: fewer false occlusion decisions when camera orientation changes, preserving shadow quality without CPU raycasts.

## Decision 012 - Frame Identity Removed From Unity Time

Problem: Point-light shadow allowance originally used a deterministic hash of `InstanceHash ^ Frame`. The runtime previously fell back to `Time.frameCount` when the dispatcher frame was unavailable. Presentation state is rollback-excluded, but Unity wall-frame identity still weakens cross-client repeatability and violates the stricter deterministic RNG mandate.
Solution: Remove `Time.frameCount` from SHINOBU_134 runtime. Dispatcher `context.Frame` remains authoritative for runtime/telemetry cadence; fallback frame identity now advances from Vault-owned `AbyssalShadowRuntimeStateDTO.Frame`. Decision 024 later removed frame-rerolled point-light admission entirely and kept the frame route for telemetry only.
Rejected Alternatives: Keeping `Time.frameCount` because shadows are visual-only, or adding a local random state. The former hides nondeterminism; the latter adds state that rollback does not own.
Scalability potential: No quality-path change in this decision. Low/Middle/High/Ultra share the same frame identity source for runtime cadence and telemetry hash windows; point-light lottery stability is handled by Decision 024.
Hardware Impact: No measured frame-time change. The benefit is deterministic repeatability and cleaner replay analysis in the 300-frame blackbox.

## Decision 013 - GPU Mapping Unlock Guard

Problem: `GraphicsBuffer.LockBufferForWrite` requires a matching unlock. The previous upload path was linear and correct under normal execution, but a Unity Editor/driver exception between lock and unlock would leave a mapped buffer in a bad state and poison later VisualSync uploads.
Solution: Wrap both state-buffer and indirect-args buffer mappings in `try/finally`, keeping the same `UnsafeUtility.MemCpy` upload route and double-buffering. No managed visible list or renderer mutation is introduced.
Rejected Alternatives: `SetData` with managed arrays, CPU visible-index lists, or ignoring the fault because the path is usually stable. `SetData` hides allocation/copy cost; a mapped-buffer leak is a catastrophic debug failure.
Scalability potential: Low/Middle/High/Ultra unaffected in math. The same upload path stays stable when quality changes upload counts or external producers change active windows.
Hardware Impact: No measured microsecond saving. This is driver/resource safety for long endurance sessions.

## Decision 014 - Vault-Only Producer Access

Problem: External producer/control methods previously called `EnsureInitialized`, which also creates GraphicsBuffers. A Lighting/HZB/World producer resolving Vault input arrays should not accidentally allocate GPU upload buffers outside the render boot/schedule path.
Solution: Split producer/tuner/CSV/snapshot paths to `EnsureVaultBuffers` only. Runtime `OnEnable` attempts cold prewarm through `EnsureInitialized` when the Vault is already available; simulation/upload paths still ensure GPU buffers before publishing. This keeps external producer access H-PHI/Vault-only.
Rejected Alternatives: Leave hidden GPU allocation in producer access, or move GraphicsBuffer ownership into producer domains. Hidden allocation violates boot clarity; producer-owned GPU buffers would duplicate render truth.
Scalability potential: Low devices avoid surprise GPU allocation when only staging data; High/Ultra still get double-buffered upload buffers prewarmed or ensured before visual publication.
Hardware Impact: No measured frame saving. The expected impact is reduced allocation jitter risk when external systems register data producers during runtime transitions.

## Decision 015 - Vault Lock Failure Is Fail-Fast

Problem: Job scheduling must not treat `TryLockBuffer` as decorative. If a producer, renderer bridge, or another phase already owns one of the SHINOBU_134 Vault buffers, scheduling a Burst read/write chain anyway would violate one-owner data authority and make the blackbox lie about the frame that actually ran.
Solution: `ScheduleCullingPass` uses `TryLockJobBuffers(out lockedCount)`. On any failed lock it reverse-unlocks only buffers acquired by that attempt, records `TelemetryFlagVaultLockFailed`, preserves the registered producer dependency/count flags for the next attempt, and returns the incoming `JobHandle` without scheduling mock/evaluate/reduce/indirect jobs.
Rejected Alternatives: Ignoring failed locks, force-completing the conflicting owner, or unlocking the full buffer set on a partial acquisition. Ignoring the failure corrupts ownership; force-complete creates stalls; full unlock could release a buffer this attempt never acquired.
Scalability potential: Low/Middle devices under thermal pressure can skip a contested shadow frame cleanly instead of blocking. High/Ultra keep the same quality math but retain deterministic ownership proof when multiple producers feed richer HZB/illumination data.
Hardware Impact: 0 us measured. Expected benefit is stall/corruption avoidance; a skipped contested cull frame is cheaper than a main-thread wait or poisoned Vault state.

## Decision 016 - Mock Stress Facade Must Fail Closed

Problem: `RunMockCullingOnce()` is the editor/CI proof path for the 50k synthetic culling lane. After the Vault-lock fail-fast guard, `ScheduleCullingPass` can legitimately return without scheduling. The facade previously called `CompletePendingJob()` afterward, which returns `true` when no job is pending, producing a false positive mock pass.
Solution: After `ScheduleCullingPass`, require `_jobPending` before force-completing. If scheduling failed because Vault ownership was contested or buffers were unresolved, the facade returns `false` and leaves telemetry with the lock-failure record.
Rejected Alternatives: Trusting telemetry alone, throwing from the editor path, or force-locking buffers for the mock. Telemetry alone is too easy to miss in CI; throwing damages editor iteration; force-locking breaks one-owner authority.
Scalability potential: Low through Ultra unchanged in runtime math. The benefit is proof integrity: a claimed 50k stress pass now means a Burst job was actually scheduled.
Hardware Impact: 0 us measured. Prevents fake validation, not a frame-time optimization.

## Decision 017 - HZB Mock Radial Falloff Must Be Sqrt-Free

Problem: The primary culling kernel was squared-distance only, but the HZB mock seeding job still used `math.length(uv)`. That is a hidden sqrt in a Burst job and violates the spirit of the shadow lane's squared-distance discipline, even if the tile count is small.
Solution: Replace radial length with `radialSq = math.saturate(math.dot(uv, uv))` and feed the occluder scalar from squared radial distance. The mock HZB remains a visual/depth fake, but no longer burns sqrt ALU for tile falloff.
Rejected Alternatives: Leave it because HZB tiles cap at 256, or precompute a managed radial table. Small waste still rots patterns; a managed table adds ownership/allocation surface for no benefit.
Scalability potential: Low uses the same coarse HZB fake without sqrt; Middle/High/Ultra still scale tile count and bias through quality weight while staying O(t) with cheap dot products.
Hardware Impact: Expected saving is tiny because t <= 256, but it removes one sqrt-class operation per mock HZB tile. Measured proof absent.

## Decision 018 - Layout Reflection Belongs To Editor Facade

Problem: `AbyssalShadowLayoutAudit` used reflection (`typeof(T).GetField`) and lived in the runtime DTO source. The validator is cold and useful, but player/runtime IL should not carry editor proof machinery when the XML explicitly asks for an editor-time validation script.
Solution: Move `AbyssalShadowLayoutAudit` into `Assets/_Project/Scripts/Graphics/Culling/Editor/AbyssalShadowTunerWindow.cs` under `#if UNITY_EDITOR` and add the required `Unity.Collections.LowLevel.Unsafe` import there. Runtime source keeps only DTOs, parser/dump utilities, and AUP/frustum math.
Rejected Alternatives: Leave runtime reflection because it is not hot path, or duplicate a second validator. Cold runtime reflection still expands the player surface; duplicate validators drift.
Scalability potential: Low through Ultra unchanged. The benefit is compile/player hygiene: layout proof remains human-accessible in editor without adding runtime reflection API surface.
Hardware Impact: 0 us measured. Prevents runtime IL bloat and accidental player-side reflection calls.

## Decision 019 - Bayer Dither Must Be Fade-Band Only

Problem: The shader include clipped every shadow caster against the Bayer matrix using `IlluminationScalar`, even when `DitherFadeActive` was not set. That made dim-but-valid casters noisy forever instead of reserving dither for the fade-band Dear Lie.
Solution: Add HLSL flag constants for `CastShadows` and `DitherFadeActive`, keep the hard `clip` when `CastShadows` is absent, and return before Bayer math unless the dither flag is present.
Rejected Alternatives: Keep illumination-driven permanent shadow thinning, or split another DTO field. Permanent thinning makes shadow quality unstable; adding a field breaks the exact 32B state ABI without necessity.
Scalability potential: Low still gets fade-band dissolve under aggressive cull distances; Middle/High/Ultra keep full shadows for admitted casters outside the fade band. The dither fake remains proportional to quality-driven cull math.
Hardware Impact: Slight GPU ALU reduction for non-fading casters because Bayer index/threshold work is skipped. Measured proof absent.

## Decision 020 - CSV Profile Reload Must Preserve Last Good Rules

Problem: `LoadProfileCsv()` cleared the live profile rule table before parsing. A missing-header, corrupt, or zero-valid-row CSV could erase designer tuning and silently revert all culling profiles to defaults until another successful load.
Solution: Parse first and require `ParsedRuleCount > 0`. Only after a successful parse does the runtime clear the stale tail beyond the parsed prefix. Zero-valid-row files return false and keep the previous live rules.
Rejected Alternatives: Keep clearing first for simplicity, or stage into a new persistent NativeArray. Clearing first breaks hot reload safety; a second persistent array violates the Vault law and adds memory surface.
Scalability potential: Low/Middle/High/Ultra keep their previous tuned cull/radius/fade rules when a bad CSV is pushed. A successful shorter profile set still removes stale tail rows deterministically.
Hardware Impact: 0 us measured. Cold editor/reload path only; protects tuning integrity.

## Decision 021 - Control Mutations Must Not Race Scheduled Readers

Problem: `SetLocalizedFrustumPlanes()` writes frustum planes and `LoadProfileCsv()` mutates profile rules. Both arrays are read by `EvaluateShadowCullingJob`. Without a scheduled-reader guard, an editor button or producer facade could mutate those NativeArrays while Burst is reading them.
Solution: Gate both mutation paths with `_jobPending`. If a culling job is scheduled, the mutation facade refuses the write; the next frame can retry after VisualSync completes the job and unlocks buffers.
Rejected Alternatives: Complete the culling job inside the setter/reload path, or add a second persistent staging table. Completion would stall the main thread from editor/control code; staging duplicates profile ownership and violates the Vault law.
Scalability potential: Low through Ultra unchanged. The benefit is deterministic memory ownership when richer high/ultra profile/HZB/frustum producers are active.
Hardware Impact: 0 us measured. Prevents NativeArray read/write races and possible Unity safety exceptions.

## Decision 022 - CSV Reload Is Transactional, Not Partial Commit

Problem: A fail-closed zero-row guard still allowed partial mutation if the CSV contained one valid rule followed by malformed content, and the parser silently stopped at rule capacity. That can corrupt live tuning with a half-valid profile table.
Solution: Add `AbyssalShadowProfileCsv.Validate(...)` as a no-commit pass. `LoadProfileCsv()` now requires at least one parsed rule, zero rejected lines, and no capacity overflow before calling the commit parser. The commit pass then clears only the stale tail.
Rejected Alternatives: Accept partial valid prefixes, or allocate a persistent staging NativeArray. Partial prefixes are not designer intent; persistent staging violates H-PHI and duplicates profile truth.
Scalability potential: Low/Middle/High/Ultra retain last-good tuning if a designer drops a malformed or oversized profile file. Valid files still update scalar rules for all quality weights.
Hardware Impact: 0 us measured. Cold reload scans CSV twice, which is acceptable outside gameplay hot paths and avoids corrupt frame behavior.

## Decision 023 - CSV Float Tokens Must Be Exhaustive

Problem: The byte parser accepted numeric prefixes without verifying full token consumption. A malformed scalar like `1abc` could pass as `1.0`, bypassing the transactional validation pass.
Solution: `TryParseFloat()` now rejects tokens when the parser cursor does not end exactly at the trimmed token boundary after optional sign, integer, and fractional digits.
Rejected Alternatives: Rely on designers to keep CSV clean, or route through managed `float.Parse`. Trust is not validation; managed parsing adds culture/allocation risk and is outside the zero-GC parser design.
Scalability potential: All quality tiers keep deterministic profile scalar inputs; malformed values cannot perturb low-tier culling aggression or ultra-tier shadow budgets.
Hardware Impact: 0 us measured. Cold parser correctness only.

## Decision 024 - Shadow State Needs Hysteresis, Not Frame Flicker

Problem: Distance, darkness, SDF, caster-radius, frustum, and point-light gates were immediate threshold decisions. That can make shadow flags flip on camera jitter or quality-weight oscillation. The point-light lane also used a frame-varying hash, which is deterministic but visually unstable.
Solution: `EvaluateShadowCullingJob` now reads the previous `ShadowCullStateDTO` for the same `InstanceHash` and applies continuous hysteresis bands: 3-5 m for distance/frustum, scalar bands for darkness/SDF, radius bands for small casters, and point-light budget hysteresis. Point-light decimation now uses an instance-stable hash, not `Frame`, so admitted point shadows do not re-roll every frame.
Rejected Alternatives: Adding a second persistent history buffer or using wall-clock debounce. A second buffer violates the Vault footprint for a 32B ABI lane; wall-clock debounce violates deterministic scheduling and rollback presentation replay.
Scalability potential: Low keeps aggressive culling but requires recast candidates to move back inside the tighter threshold; Middle/High reduce edge flicker; Ultra preserves admitted shadows through wider frustum/distance margins without changing the DTO ABI.
Hardware Impact: Adds one previous-state read and a handful of lerp/add comparisons per candidate. Expected cost is smaller than a false shadow-map redraw burst from threshold chatter; measured proof absent.

## Decision 025 - Seeded Mock State Is Not History

Problem: `GenerateMockCullingDataJob` seeds `States` with `CastShadows` and `DistanceSq=0`. The first evaluate pass could treat that seeded row as previous history and apply the relaxed hysteresis side before a real cull result exists.
Solution: Previous-state hysteresis now requires matching `InstanceHash`, finite `DistanceSq`, `DistanceSq > 0`, and absence of `NonFinite`. Seed/default rows do not influence the first real cull evaluation.
Rejected Alternatives: Clearing a separate validity buffer or adding a new DTO flag. Both would expand memory/ABI surface for a condition that can be derived from the existing 32B state.
Scalability potential: Low-tier first-frame culling remains aggressive instead of inheriting a seeded cast-shadow bias; Middle/High/Ultra still get hysteresis after the first evaluated frame.
Hardware Impact: Adds two scalar checks in the previous-state gate. Measured proof absent.

## Decision 026 - Tuner Writes Must Not Skew In-Flight Telemetry

Problem: `ApplyTunerSettings()` could mutate `AbyssalShadowRuntimeStateDTO` while a culling job was pending. The Burst job already captured scalar copies, but completion telemetry reads the runtime row; a mid-job tuner write could make the blackbox report settings that were not used by the evaluated frame.
Solution: Gate `ApplyTunerSettings()` with `_jobPending`, matching the frustum and CSV mutation policy. Tuning changes now retry on the next frame after VisualSync completes the culling handle.
Rejected Alternatives: Force-completing the culling handle from the editor/tuner path or adding a second runtime-state staging buffer. Force completion stalls the main thread; staging duplicates owner truth.
Scalability potential: Low/Middle/High/Ultra tuning remains live, but transitions occur on clean frame boundaries with truthful telemetry.
Hardware Impact: 0 us measured. Prevents control-path telemetry corruption and avoids forced job completion stalls.
