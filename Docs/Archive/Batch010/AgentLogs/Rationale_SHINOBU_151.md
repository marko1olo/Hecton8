# SHINOBU_151 Rationale - Dynamic Point Light Culling Director

Status: POLISH STATIC PASS / COMPILE BLOCKED BY CPU GATE
Evidence Class: STATIC_SOURCE until Unity compilation and profiler logs exist.

## Decision 01 - Replace Unity Light Churn With Vault Payloads

Problem: Hundreds of point/spot lights in abyss/base spaces cause CPU-side light object state churn and Forward+/cluster work pressure when scripts toggle `Light.enabled`.
Solution: Build a presentation-only Vault pipeline: source DTOs -> Burst culling -> radix importance order -> top-N GPU payload -> double-buffered `GraphicsBuffer` upload in VISUAL_SYNC.
Rejected Alternatives: Unity `Light` enable/disable in Update was rejected because it rebuilds renderer light state and allocates component-driven work. LODGroup-only disabling was rejected because it hides control inside GameObject hierarchy and cannot be thermal-weighted continuously.
Scalability potential: Low uses 8 active shader lights and aggressive fade; Middle raises useful local lamps; High keeps more near-field glow; Ultra spends saved CPU/GPU on 64 active mathematical lights plus SH bounce fake.
Hardware Impact: i3/MX350 avoids per-object Light churn and bounds shader light loop; expected saving is in avoided stalls, not yet measured. Status: PENDING VERIFICATION.

## Decision 02 - AUP First, Float Local Second

Problem: Floating origin scale can corrupt far-map culling if absolute double/world positions are cast to float before subtraction.
Solution: Store light and camera positions as `double3`; Burst job subtracts camera AUP first, then casts the local delta to `float3` for plane/distance math.
Rejected Alternatives: `Transform.position` truth and `Vector3.Distance` were rejected because they are presentation-only and precision-unsafe at 100 km scale.
Scalability potential: All tiers share correctness; Low probes fewer frames, Ultra can keep richer telemetry and more active light survivors.
Hardware Impact: subtraction is three double ops per light; cheaper than debug rework from false culls. Status: PENDING VERIFICATION.

## Decision 03 - Explicit DTO Layouts

Problem: NativeArray DTOs with properties or packed layouts create defensive copies and platform alignment hazards.
Solution: `LightCullStateDTO` is explicit 32 bytes exactly as assigned; GPU/telemetry/source/profile structs use explicit 64/96-byte layouts with public raw fields only.
Rejected Alternatives: Sequential layout and C# properties were rejected. `Pack=1` was rejected by ARM64 mandate.
Scalability potential: Low gets compact cache lines; Ultra can consume richer GPU payloads without changing authoritative gameplay truth.
Hardware Impact: `LightCullStateDTO` is 32 bytes, so 5000 states are 160 KB; sequential worker access stays cache-friendly on i3/MX350. Status: PENDING VERIFICATION.

## Decision 04 - Visual Fake GI Bounce

Problem: Real dynamic GI from hundreds of abyss lights is unaffordable and not gameplay truth; a direct culler-owned probe injection job also creates a cross-owner late-frame blocking point.
Solution: Convert top survivors into Vault-owned `CustomDynamicProbeLightDTO` records and expose `TryGetProbeBounceReadback` for the probe-grid owner. The culler publishes the fake bounce stream; it does not mutate probe memory or complete a probe job.
Rejected Alternatives: Ray tracing, per-pixel voxel GI, Unity realtime GI, and direct `InjectDynamicLightJob.Complete()` from the culling director were rejected as too expensive or ownership-inverted.
Scalability potential: Low uses near-zero bounce; Middle/High get controlled probe tint; Ultra gets stronger fake bounce from already-surviving priority lights.
Hardware Impact: bounded by submitted light count 8..64; culler-side cost is already paid in the payload builder. Probe-grid owner decides if and when to spend the injection cost. Status: STATIC SOURCE PASS / COMPILE PENDING.

## Decision 05 - Black Box and Failure Handling

Problem: A lighting culler can silently produce NaNs or empty submissions during origin shifts, causing visual blackouts without forensic state.
Solution: 300-entry `LightCullingTelemetryEntry` ring in Vault and binary dump to `Docs/AgentLogs/Dump_LIGHT_DIRECTOR.bin` on NaN or timeout.
Rejected Alternatives: Debug.Log, string status, and editor-only screenshots were rejected as non-forensic and allocation-prone.
Scalability potential: Low records compact aggregate state; Ultra can interpret all 300 frames for visual-route tuning.
Hardware Impact: 300 x 64 bytes = 19.2 KB; one write per completed frame. Status: PENDING VERIFICATION.

## Decision 06 - Compile-Wall and Namespace Hygiene

Problem: Graphics runtime code can silently drag sibling runtime assemblies into every C# compile if it imports another domain directly.
Solution: Keep SHINOBU_151 inside `Hecton8.Lighting.asmdef` with Core/Core.Contracts/Core.Memory plus Unity packages only. Static scan of owned runtime source is clean for `using Hecton8.World|Gameplay|Environment|AI|Physics|Audio|Ecosystem|Vehicles|Habitat|Combat`.
Rejected Alternatives: Directly depending on world/netcode/base-building assemblies was rejected because it widens the Compile Wall and creates false ownership of presentation data.
Scalability potential: Low through Ultra all receive the same decoupled presentation path; renderer work scales through data, not assembly coupling.
Hardware Impact: Runtime frame gain is indirect; developer hardware avoids avoidable sibling-domain recompiles. Status: STATIC SOURCE PASS / COMPILE PENDING.

## Decision 07 - Editor Facade Without Refresh String Churn

Problem: The tuner readout originally used per-refresh label string concatenation and automatic scene-object search, which is acceptable for a prototype but not for a zero-GC tooling standard.
Solution: Use UI Toolkit `IntegerField`, `FloatField`, and `Toggle` readouts with `SetValueWithoutNotify`; refresh at 250 ms through the root scheduler; scene search only on cold init or explicit `Find Runtime`.
Rejected Alternatives: IMGUI, runtime Canvas, and `EditorApplication.update` label concatenation were rejected because they hide allocation churn and make editor tooling less deterministic.
Scalability potential: Low-tier authoring machines avoid editor churn while artists still tune fade, importance, SDF threshold, and quality override. Ultra machines get the same readout without polluting runtime architecture.
Hardware Impact: Editor-only saving; no runtime frame claim. Status: STATIC SOURCE PASS / COMPILE PENDING.

## Decision 08 - Vault Lock Failure Containment

Problem: Cold mock generation mutates Vault buffers outside the frame pipeline. If a schedule/complete fault occurs before unlock, later culling frames can inherit stale ownership flags.
Solution: Source/state mock seed and mock SDF seed use separate Vault locks with `try/finally` unlock. SDF generation no longer locks source/state buffers it does not mutate.
Rejected Alternatives: Trusting cold path jobs to never fault was rejected. Private `NativeArray` fallback was rejected by Vault law.
Scalability potential: Low devices can use mock stress data without poisoning later frames; Ultra devices can generate the same 5000-source stress set and higher survivor count.
Hardware Impact: No hot-path cost; prevents lock leaks that would cause frame skips or missing lights. Status: STATIC SOURCE PASS / COMPILE PENDING.

## Decision 09 - Manual Frustum Planes, No Managed Plane Scratch

Problem: `GeometryUtility.CalculateFrustumPlanes(Camera, Plane[])` requires a managed `Plane[]` scratch field and hides math behind Unity object API.
Solution: Extract six frustum planes directly from `projectionMatrix * worldToCameraMatrix`, normalize with guarded `math.rsqrt(math.max(lengthSq, 0.000001f))`, and shift plane distance into camera-local space before Burst culling.
Rejected Alternatives: Managed `Plane[]`, `GeometryUtility`, and absolute float plane tests were rejected. The culling route must be math-visible and AUP-local.
Scalability potential: Low devices avoid managed scratch and object API work; Ultra devices get the same plane contract and spend saved cycles on more survivor lights.
Hardware Impact: Removes managed frustum scratch and one Unity helper call per cull schedule. Microseconds not measured because compile/profiler gate is still blocked. Status: STATIC SOURCE PASS / COMPILE PENDING.

## Decision 10 - Sqrt-Free Mock SDF

Problem: The deterministic mock SDF used `math.length(p.xz)`, which is a hidden sqrt even if it is cold/test generation.
Solution: Replace radial side distance with a squared radial pseudo-SDF sign approximation: `(r^2 - lengthSq) / r`. The culling gate only needs stable sign/relative blockage for the mock wall.
Rejected Alternatives: Keeping the sqrt was rejected because the task exists to police light math under stress and tests must not normalize bad examples.
Scalability potential: Low devices can generate mock pressure data cheaper; Ultra keeps deterministic stress shape without teaching future agents to use sqrt.
Hardware Impact: Removes one sqrt per mock SDF voxel. For a 16^3 grid, that is 4096 sqrt calls avoided during mock generation. Status: STATIC SOURCE PASS / COMPILE PENDING.

## Decision 11 - Stable Unity Import GUIDs

Problem: New `.cs` assets without `.meta` files force Unity to mint GUIDs locally, creating nondeterministic import state for integrators.
Solution: Add stable `.meta` files for the new dynamic-light folder and all new C# source/test/editor files.
Rejected Alternatives: Letting Unity generate GUIDs during import was rejected because it produces machine-local evidence and noisy merges.
Scalability potential: No runtime effect; developer iteration remains deterministic across machines.
Hardware Impact: No frame-time impact. Reduces import/reimport churn risk. Status: STATIC SOURCE PASS / COMPILE PENDING.

## Decision 12 - Unseeded Buffer Fail-Closed

Problem: `Sources` and `MockSdfSamples` are intentionally allocated with `UninitializedMemory`. If mock generation is disabled or a buffer is reallocated, using default counts would evaluate garbage sources or randomly cull lights against garbage SDF values.
Solution: Source buffer reallocations reset `_sourceBufferSeeded` and `_activeSourceCount` to zero. SDF reallocations reset `_mockSdfSeeded`; `BuildSettings` publishes `SdfSampleCount=0` until `GenerateMockSdfSamples` succeeds. The 300-frame telemetry ring is cold-cleared because forensic buffers must not start as garbage.
Rejected Alternatives: Trusting uninitialized memory to be harmless was rejected. Clearing all large streams was also rejected; only small counters/cursors/telemetry are cleared.
Scalability potential: Low devices avoid false blackouts from random occlusion; Ultra keeps the same deterministic seed path and can opt into richer SDF when a real mirror exists.
Hardware Impact: Hot path cost is zero. Cold telemetry clear is 19.2 KB; source/state/SDF/sort/payload still avoid bulk zero-fill. Status: STATIC SOURCE PASS / COMPILE PENDING.

## Decision 13 - GPU Payload Buffer Prewarm

Problem: Creating the `GraphicsBuffer` pair on the first VISUAL_SYNC upload can introduce a first-light stutter exactly when the abyss lighting route comes online.
Solution: `EnsureNativeStorage` now prewarms both structured `GraphicsBuffer` instances at the 64-light maximum capacity. The upload path still calls `EnsureGpuBuffers` only as recovery if buffers were released or resized.
Rejected Alternatives: Lazy first-upload allocation was rejected because shader/buffer warmup belongs to boot, not the first visible lighting frame.
Scalability potential: Low devices avoid first-use stalls; Ultra devices get the same prewarmed 64-record payload capacity.
Hardware Impact: Cold GPU allocation is moved earlier; no measured runtime saving until Unity profiler proof is available. Status: STATIC SOURCE PASS / COMPILE PENDING.

## Decision 14 - Timeout Fault Latch, No Counter Data Race

Problem: The timeout path originally wrote `RuntimeCounters` while the culling job could still be running and writing the same counter buffer.
Solution: Timeout detection now only latches `_timeoutFaultPending` and dumps the already-owned telemetry ring once per scheduled job. The `TimedOut` flag is written into counters only after `_pendingCullHandle.Complete()` returns and the Vault locks are released.
Rejected Alternatives: Writing counters from the main thread while the job is active was rejected as a data race. Completing the job immediately on timeout was also rejected because it would stall the frame at the worst possible moment. Repeated dump writes every late frame were rejected as IO thrash.
Scalability potential: Low thermal-pressure devices avoid main-thread emergency stalls; Ultra keeps the same forensic flag without compromising job ownership.
Hardware Impact: Hot path adds one managed bool check after completed jobs. It removes a potential cache/data race on the single 64-byte counter block. Status: STATIC SOURCE PASS / COMPILE PENDING.

## Decision 15 - Strict Vault Ready Gate

Problem: `_nativeStorageReady` did not include every buffer required by the declared pipeline. A missing sort scratch, probe stream, CSV/profile table, or self-audit buffer could leave the director half-ready.
Solution: The ready gate now requires every SHINOBU_151 Vault handle requested at boot: source/state/settings, both GPU payloads, telemetry ring/cursor, sort streams, CSV/profile/SDF/probe buffers, counters, frustum planes, and self-audit.
Rejected Alternatives: Letting scheduling functions discover missing buffers later was rejected because it creates silent no-op frames and makes the route harder to autopsy.
Scalability potential: All tiers fail closed if the Vault lane is incomplete; no device gets partial lighting truth.
Hardware Impact: No hot-path cost beyond boolean checks at boot. Status: STATIC SOURCE PASS / COMPILE PENDING.

## Decision 16 - Source Manifest Commit Fence

Problem: Source validity was mirrored in `_activeSourceCount`. A failed mock seed lock could leave a nonzero private count pointing at uninitialized source memory, and external source writers had no Vault-native count contract.
Solution: Add `DynamicPointLightSourceManifestDTO[1]` in Vault buffer `71458`. The culler reads source count only when the manifest has `Committed` set; mock generation commits after the source/state job completes; external writers can publish through `TryCommitExternalSourceCount` after fully writing the source/state window.
Rejected Alternatives: Keeping private count authority was rejected because it violates one fact -> one owner -> one route. Letting external writers mutate culler settings was rejected because the culler overwrites settings every schedule. Auto-generating mock data during external commit initialization was rejected because it could overwrite a real source window.
Scalability potential: Low devices fail closed to zero sources when the manifest is absent; Middle/High/Ultra can feed real light records without Unity `Light` objects or direct assembly coupling.
Hardware Impact: One 64-byte clear-memory manifest replaces ambiguous private state. It prevents uninitialized source reads without clearing the 96-byte source stream. Status: STATIC SOURCE PASS / COMPILE PENDING.

## Decision 17 - Complete Fence Classification

Problem: Static source review showed three `JobHandle.Complete()` calls in the director. Without classification, later reviewers can misread cold/mock fences or teardown drains as forbidden hot-path `Schedule().Complete()` stalls.
Solution: The VISUAL_SYNC reclaim now checks `IsCompleted` before `Complete()` and only then releases Vault locks/uploads. Mock source and mock SDF generation remain cold/editor fences because the source manifest and SDF seeded flag must not publish before backing data is written. Shutdown uses a drain only to release locked Vault windows before unregistering the owner.
Rejected Alternatives: Removing all `Complete()` calls was rejected because it would publish uncommitted mock data or leak Vault locks during disable. Blocking immediately on the culling job from timeout handling remains rejected; timeout still latches and returns until the handle completes.
Scalability potential: Low devices avoid emergency frame stalls; Middle/High/Ultra get deterministic cold mock stress data and deterministic teardown without corrupting shared Vault locks.
Hardware Impact: Hot path remains non-blocking until the job is already completed. Cold fences cost only manual/editor/mock generation time and are not frame-loop claims. Status: STATIC SOURCE PASS / COMPILE PENDING.

## Decision 18 - GPU Mapping Unlock and Shader Scalar Hygiene

Problem: `GraphicsBuffer.LockBufferForWrite` was followed by a straight-line copy/unlock. If a future edit inserted a throwing validation path between them, the mapped buffer could remain locked. The visual sync path also used `new Vector4(...)` syntax for value-type shader scalars, which is not GC but weakens simple static zero-allocation audits.
Solution: Wrap mapped GPU payload copies in `try/finally` so `UnlockBufferAfterWrite` always runs after a successful lock. Replace shader vector constructor syntax with `default` value assignment.
Rejected Alternatives: Leaving the upload path uncloaked by `finally` was rejected because GPU buffer mapping is a scarce render resource. Replacing the upload with managed staging arrays was rejected by the GraphicsBuffer/Vault route.
Scalability potential: Low devices avoid a wedged mapped buffer after an exceptional upload path; Ultra keeps the same direct payload upload and shader scalar route.
Hardware Impact: No measured frame claim. Static cost is one `finally` region around a mapped copy; it protects correctness without adding managed allocation. Status: STATIC SOURCE PASS / COMPILE PENDING.

## Decision 19 - Settings DTO NaN Ingress Guard

Problem: Several serialized tuning scalars entered `DynamicPointLightCullingSettingsDTO` without a common finite fallback. Downstream jobs handled many cases, but NaN should be rejected at the settings boundary before it can influence SDF thresholds, bounce gain, range, or shader constants.
Solution: Add `DynamicPointLightCullingMath.SanitizeFinite` and apply it in `BuildSettings` for fade distance, importance, SDF threshold, SDF cell size, bounce gain, near-field boost, thermal fade strength, max range, and submit epsilon.
Rejected Alternatives: Relying only on downstream job-level clamps was rejected because NaN defense belongs at every boundary where external/editor tuning can enter. Throwing editor/runtime exceptions was rejected because this system must fail closed and keep rendering.
Scalability potential: Low devices shed load predictably instead of letting bad tuning produce NaN intensity or no lights; Ultra preserves overkill ranges/gain only when finite.
Hardware Impact: A few scalar finite checks per schedule, not per-light heavy work. Prevents NaN propagation into GPU payload and blackbox state. Status: STATIC SOURCE PASS / COMPILE PENDING.

## Decision 20 - Legacy Light Archaeology Boundary

Problem: Project-scope scan still finds gameplay-owned Unity `Light` toggles in `PlayerFlashlight`, `RepairTool`, `Gameplay/DeployableFlare`, `Gameplay/GravTrap`, and a flashlight voxel-shadow provider. It also finds authored scene/prefab Light components. None matched the assigned `LightDistanceCull`/`Vector3.Distance` distance-cull offender pattern, but they are still legacy Unity-light emitters.
Solution: Do not delete gameplay-owned scripts from SHINOBU_151. Record the offenders and keep the dynamic point-light culling lane as the replacement route: real writers must publish `DynamicPointLightSourceDTO` rows plus SourceManifest `71458`, then the culler submits top-N GPU payloads without Unity `Light` objects.
Rejected Alternatives: Editing PlayerFlashlight/DeployableFlare/GravTrap directly was rejected because those are gameplay/tool ownership domains and would violate the multi-agent boundary without their owner. Ignoring the scan was rejected because Task 01 archaeology requires evidence.
Scalability potential: Low/Middle/High/Ultra all need the same migration route: owner-local gameplay systems can publish light source DTOs continuously while this culler decides which visual lights survive under quality/thermal pressure.
Hardware Impact: No immediate measured frame claim from untouched legacy emitters. Static archaeology found `13` authored Light YAML components and direct gameplay toggles; SHINOBU-owned runtime has zero Unity `Light` submission. Status: STATIC SOURCE PASS / CROSS-DOMAIN MIGRATION PENDING.

## Decision 21 - Raw Pointer DTO Access Proof

Problem: The culling jobs used `NativeArray[index]` for source/state/payload DTOs. That can still compile to native memory access, but it does not provide explicit evidence for the assigned `LightCullStateDTO*`/`UnsafeUtility.AsRef` requirement and leaves future reviewers guessing about defensive struct copy avoidance.
Solution: Add `DynamicPointLightNativeAccess` with `NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr`, `NativeArrayUnsafeUtility.GetUnsafePtr`, and `UnsafeUtility.AsRef<T>`. Mock seed, evaluation, GPU payload, probe stream, and counters now write DTOs through ref access in the Burst job file; source/state reads that feed heavy math use `ref readonly` where mutation is not needed.
Rejected Alternatives: Leaving plain indexers was rejected because the assignment explicitly requires raw pointer/ref proof. Converting the public API to raw pointers was rejected because the Vault and Unity job scheduler already own buffer lifetime and safety boundaries; the unsafe seam belongs inside the job file only.
Scalability potential: Low/Middle/High/Ultra keep the same culling behavior. The change strengthens Burst alias/copy evidence without creating a new quality tier or direct domain dependency.
Hardware Impact: Expected benefit is defensive-copy removal and clearer vectorization input, not a measured runtime claim. Static proof exists; compile/profiler proof remains blocked by CPU gate. Status: STATIC SOURCE PASS / COMPILE PENDING.

## Decision 22 - Manifest-Authority Editor Readback

Problem: `TryGetStatesReadback` returned `_activeSourceCount` before resolving state/source arrays. External source writers publish through SourceManifest `71458`, so an editor gizmo/tuner read could lag behind the authoritative Vault manifest if the private mirror was stale.
Solution: Resolve arrays first, then report `count = min(ReadCommittedSourceCount(), states.Length, sources.Length)`. This keeps debug readback in the same one-fact route as scheduling and settings.
Rejected Alternatives: Keeping `_activeSourceCount` in readback was rejected because it makes the editor facade a second authority. Reading raw source capacity was rejected because uncommitted uninitialized Vault memory must remain invisible.
Scalability potential: All hardware tiers get the same fail-closed debug view. Low-tier mock stress and Ultra external-source feeds both expose only committed records.
Hardware Impact: One manifest read in editor/debug readback; no per-light hot-path cost. Status: STATIC SOURCE PASS / COMPILE PENDING.

## Decision 23 - Polynomial Quality Budget Curve

Problem: The active light cap used a direct linear `math.lerp(8, 64, quality)`. It was continuous, but the polish mandate explicitly requires `math.lerp`, `math.step`, and polynomial curves so the system breathes under thermal pressure instead of behaving like a flat slider.
Solution: Change `ResolveMaxActiveLights` to gate zero quality with `math.step(0.000001f, quality)`, apply a cubic smooth polynomial `q*q*(3-2*q)`, then lerp 8..64. Thermal pressure now uses the same polynomial before damping the quality scalar to 35 percent at full pressure.
Rejected Alternatives: Binary `if (lowEnd)` tiers were rejected. A hard tier table was rejected because it would pop lighting density. Leaving pure linear mapping was rejected because it does not buy enough aggressive shedding below 0.3 quality.
Scalability potential: Low devices collapse toward 8..10 survivors earlier and at a 5 Hz cadence; Middle keeps a curved mid-budget; High/Ultra reach 64 survivors and near-field overkill gain smoothly.
Hardware Impact: Cost is a few scalar multiplies per schedule. Expected gain under pressure is lower shader loop count and fewer payload uploads without introducing device branches. Status: STATIC SOURCE PASS / COMPILE PENDING.

## Decision 24 - Schedule Readiness Before Length Access

Problem: `ScheduleCullingPipeline` resolved Vault arrays and then computed `count` from `sources.Length`/`states.Length` before the method's local `IsCreated` gate. Normal boot should create those buffers first, but fail-closed code must not depend on that ordering when another agent or Vault failure changes initialization timing.
Solution: Move the full NativeArray readiness gate before any `Length` read, including frustum, SDF, profile, sort, GPU payload, probe, and counter arrays. Count is clamped only after every required lane is proven created.
Rejected Alternatives: Relying on `_nativeStorageReady` alone was rejected because this scheduler should remain locally safe if the flag and handles drift. Catching exceptions was rejected because hot scheduling should be branch-simple and fail closed.
Scalability potential: All tiers return without scheduling if a Vault lane is missing; none read uninitialized or default handles.
Hardware Impact: A small fixed set of readiness branches per schedule; avoids undefined scheduling work and crash-forensics noise. Status: STATIC SOURCE PASS / COMPILE PENDING.
