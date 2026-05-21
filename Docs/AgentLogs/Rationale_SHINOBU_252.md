# SHINOBU_252 Rationale

Status: POLISH PASS ACTIVE / SUBAGENT AUDIT PATCHED / CSV FENCE HARDENED / COMPILE BLOCKED BY EXTERNAL PROJECT FILE GAP

## Bootstrap Decision
Problem: Agent state files were missing, so progress would not survive context compression.
Solution: Created `Docs/Tasks/Status_SHINOBU_252.md` and `Docs/AgentLogs/Rationale_SHINOBU_252.md` before code changes.
Rejected Alternatives: Chat-only state; violates anti-amnesia protocol and leaves no proof artifact.
Scalability potential: Low/Middle/High/Ultra unchanged; this is workflow state only.
Hardware Impact: 0 us runtime; protects build iteration traceability on low-end i3/MX350 by avoiding repeated archaeology.

## Decision 01 - Presentation-Only Pylons
Problem: Physical base supports would add Transform/GameObject churn and tempt physics truth into a visual problem.
Solution: Put support truth in `PylonMatrixDTO` and `FoundationPylonSurfaceDTO` DataVault buffers, then draw with one procedural indirect GPU batch.
Rejected Alternatives: `Instantiate()` pylon prefabs, `List<Transform>` support tracking, Rigidbody/Collider supports, or mesh-per-pylon GameObjects.
Scalability potential: Low = one center pylon per module; Middle = two visible mathematical pylons; High = three; Ultra = four with stronger shader flare.
Hardware Impact: Low-end i3/MX350 avoids per-pylon Transform traversal and renderer submissions; expected saving is 20-80 us per visible base chunk depending on old support count.

## Decision 02 - Explicit DTO Split
Problem: `PylonMatrixDTO` had a hard 64-byte prompt contract, but terrain embedding needed normal/flare data.
Solution: Kept `PylonMatrixDTO` as exact 64 bytes at offset 0 and moved normal/flare/length/hash into a second 64-byte `FoundationPylonSurfaceDTO`.
Rejected Alternatives: Enlarging `PylonMatrixDTO`, using DTO properties, or overloading matrix columns with unrelated metadata.
Scalability potential: Low devices upload only fixed 64-byte matrix/surface lanes; high devices can use the surface lane for richer shader fakes without changing layout.
Hardware Impact: 128 bytes per possible pylon slot; predictable cache-line reads and no ARM64 padding ambiguity.

## Decision 03 - Burst SDF Raymarch
Problem: Per-support `Physics.Raycast` or main-thread SDF reads would violate zero-GC and frame dictatorship.
Solution: `CalculateFoundationPylonsJob` runs bounded downward SDF raymarch in Burst over module AUPs, writing one fixed output slot per possible ray.
Rejected Alternatives: Unity PhysX, `HectonVoxelVolume.TryReadRuntimeSdfDensity` inside pylon length calculation, or same-frame spawned ray probes.
Scalability potential: Low = 1 nearest-neighbor SDF proxy sample and 1 ray; Middle = blended nearest/trilinear march; High = more steps; Ultra = 96 steps and 4 rays.
Hardware Impact: Work is parallel and data-local; expected saving versus managed/PhysX probes is 0.1-0.3 us per ray plus avoided scheduler/scene-query stalls. Low quality now avoids the 6-sample gradient path and long march loop.

## Decision 04 - Mock SDF Fallback
Problem: Foundation renderer must remain deterministic when the authored voxel SDF buffer is absent.
Solution: `GenerateMockSeafloorSDFJob` writes a deterministic sloped-plane SDF into a foundation-owned DataVault buffer.
Rejected Alternatives: Null terrain fallback, scene searches, or editor-only sampled terrain heights.
Scalability potential: Same quality ladder as real SDF; mock is only the data source.
Hardware Impact: 64^3 float fill is cold/fallback Burst work; hot path remains fixed NativeArray reads.

## Decision 05 - AUP Camera-Relative Matrices
Problem: Casting absolute universe positions to float would jitter or overflow far from origin.
Solution: Keep module/ray/hit math in `double3`; subtract camera AUP before writing `float4x4`.
Rejected Alternatives: Direct float world positions or registry polling for hot origin state.
Scalability potential: Low/Middle/High/Ultra share the same precision route; quality never changes authority or DTO layout.
Hardware Impact: One `double3` subtraction per pylon; prevents visual instability on every device tier.

## Decision 06 - Dear Lie Embedding
Problem: Real terrain deformation for support feet is excessive for a visual-only base support.
Solution: Compute SDF normal and emit shader flare; the vertex shader widens bottom vertices toward terrain contact.
Rejected Alternatives: Cutting terrain meshes, boolean intersection, or simulating seabed compression.
Scalability potential: Low = small flare; Middle = moderate; High = stronger; Ultra = visual overkill flare and banding.
Hardware Impact: Moves immersion cost to simple vertex math; CPU cost is one gradient sample group per hit.

## Decision 07 - Extension Culling and Warning Signal
Problem: Supports that cannot reach terrain must not stretch infinitely or become hidden gameplay state.
Solution: Over-limit or invalid rays write zero-scale matrices and OR failure flags; renderer publishes unmanaged `BaseStructuralWarningSignal`.
Rejected Alternatives: Drawing impossible pylons, logging managed warnings, or mutating structural truth.
Scalability potential: All tiers fail closed identically; quality only changes visual/ray budgets.
Hardware Impact: Zero-scale inactive pylons skip useful fragment work and avoid transform/prefab cleanup.

## Decision 08 - DataVault and Upload Policy
Problem: Foundation buffers are large enough that default clearing, managed upload paths, and hot external Vault lookup are wasteful.
Solution: DataVault buffers request `NativeArrayOptions.UninitializedMemory`; jobs write counters/matrices explicitly; renderer uploads with double-buffered `GraphicsBuffer.LockBufferForWrite`; external voxel SDF reads use a cold-cached `VaultGenerationHandle<byte>` and hot `TryResolveHandle`.
Rejected Alternatives: managed arrays, `SetData` every frame, `TryGetBuffer` in late-frame scheduling, or GlobalRegistry hot polling.
Scalability potential: Low devices upload fewer active slots; ultra devices spend saved CPU on more rays/flare.
Hardware Impact: One memcpy per buffer; avoids SDF/matrix staging clear costs and avoids `TryGetBuffer` external-view mutation in the hot schedule path.

## Decision 09 - Telemetry Black Box
Problem: NaN/overextension failures need post-mortem state without managed logs in hot paths.
Solution: `FoundationTelemetryEntry[300]` records frame, counts, flags, max length, quality, hash; dump path is `Docs/AgentLogs/Dump_FOUNDATION_CALCULATOR.bin`.
Rejected Alternatives: chat reports, Debug.Log spam, or variable-length managed collections.
Scalability potential: Fixed ring size independent of quality tier.
Hardware Impact: 19.2 KB fixed telemetry lane; negligible write cost during late-frame finalize.

## Decision 10 - Tooling and Proof
Problem: This route is easy to regress back to PhysX/prefabs unless guarded.
Solution: Added `Base Grounding Tuner`, `FoundationPylonLayoutValidator`, `Foundation_Physics_Inquisition`, edit tests, architecture route card, and shared construction report entry.
Rejected Alternatives: undocumented code-only implementation.
Scalability potential: Editor tuning exposes low/middle/high/ultra knobs without runtime layout changes.
Hardware Impact: 0 us shipping hot path; editor-only validation cost.

## Decision 11 - Ultra Polish Corrections
Problem: Second-pass audit found unregistered local BufferID casts, 32-byte per-module counters vulnerable to false sharing, a read-style editor accessor that initialized Vault handles, and a hot SDF route using `TryGetBuffer`.
Solution: Registered `70960..70974` in the binary payload ledger; changed `FoundationPylonFrameCounters` to 64 bytes with padding `32..63`; changed editor read state to accept an already supplied `IDataVault` and only resolve existing handles; cached the voxel SDF generation handle during cold setup.
Rejected Alternatives: Local numeric BufferID casts without ledger owner, 32-byte adjacent counter rows, read accessor side effects, and hot external Vault buffer lookup.
Scalability potential: Low/Middle/High/Ultra keep the same DTO identity and authority route; quality changes only visual math cost and presentation density.
Hardware Impact: Low-end i3/MX350 avoids potential MESI cache-line contention between worker threads and avoids hot `TryGetBuffer` mutation overhead during VISUAL_SYNC.

## Decision 12 - Presentation Fast Math Boundary
Problem: Audit suggested `FloatMode.Deterministic` because counters, result hashes, and warning signals exist.
Solution: Kept `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]` because SHINOBU_252 pylon matrices, normals, counters, hashes, and warnings are presentation/proof lanes and are explicitly rollback/save excluded. The user mandate requires Fast for mathematical jobs except rollback, kinematics, or authoritative state integrations.
Rejected Alternatives: Deterministic Burst for visual-only pylon supports, which would spend CPU on cross-platform bit identity not consumed by gameplay truth.
Scalability potential: All quality tiers keep rollback exclusion; high/ultra spend saved CPU on trilinear SDF and gradient flare.
Hardware Impact: Avoids the deterministic-math cost on Quest/MX350 class hardware while preserving authority boundaries.

## Decision 13 - Active Draw-List Compaction
Problem: The first procedural draw route could still upload/draw fixed slots after overextension or low-quality ray suppression, relying on zero-scale matrices and shader discard to hide inactive supports.
Solution: Added `CompactFoundationPylonDrawListJob` after counter reduction. It walks the generated matrix/surface rows once, packs active supports to the front of the buffers, rewrites `FrameCounters[0].SlotCount` to active count, and lets `BuildFoundationPylonIndirectArgsJob` write `InstanceCount` from that compacted count.
Rejected Alternatives: Uploading all possible slots, fragment-discarding inactive instances, or introducing per-pylon GameObjects to avoid empty slots.
Scalability potential: Low = one active center support and one uploaded instance; Middle = only active two-ray rows; High/Ultra = more active rows and shader flare when quality buys it. Quality still does not change DTO identity or authority ownership.
Hardware Impact: Low-end i3/MX350 avoids CPU memcpy and GPU vertex work for every inactive/cull slot. Exact profiler proof is pending; static cost is one O(active-capacity) Burst pass over contiguous 64-byte rows.

## Decision 14 - Shader ABI and Cold Material State
Problem: Material colors were set every draw, integer flags crossed the vertex/fragment boundary without explicit no-interpolation, and shader constants were not in the SRP material CBuffer.
Solution: Cached material colors and updated them only on change, called `Material.SetPass(0)` only during cold setup as a shader warmup hint, moved `_BaseColor/_EmbeddedColor` into `UnityPerMaterial`, marked flags `nointerpolation`, and disabled fallback shader variants with `Fallback Off`.
Rejected Alternatives: Per-frame material property churn, implicit integer interpolation, or a legacy fallback that can add unwanted shader variant work.
Scalability potential: Low/Middle/High/Ultra share the same shader ABI; higher tiers spend on more active instances and visual flare, not redundant material state writes.
Hardware Impact: Removes two unconditional material property calls per render and reduces shader ABI ambiguity. Runtime proof still requires Unity import/Frame Debugger.

## Decision 15 - Non-Destructive Editor Report Output
Problem: The editor inquisition menu wrote its sidecar proof JSON to the shared `Docs/Reports/CONSTRUCTION_OPTIMIZATION_REPORT.json` path, which could overwrite other agents' report entries when a human clicked the menu.
Solution: Changed the menu output to `Docs/Reports/CONSTRUCTION_OPTIMIZATION_REPORT_SHINOBU_252.json`; the shared aggregate report remains a manually maintained integration artifact with the SHINOBU_252 entry.
Rejected Alternatives: Leaving a destructive editor proof button or adding a JSON merge writer inside an editor validation utility.
Scalability potential: Runtime quality tiers are unchanged; this protects human tooling and multi-agent evidence integrity.
Hardware Impact: 0 us runtime. Prevents report loss during editor validation on low-end development machines.

## Decision 16 - Camera-Relative GPU Reconstruction
Problem: `CalculateFoundationPylonsJob` correctly wrote camera-relative matrices, but the shader and draw bounds treated those translations as Unity world positions. That would offset pylons by the negative camera position when the camera transform was not at the world origin.
Solution: Store the camera world position captured at schedule time, build draw bounds by adding that offset to compacted matrix centers, and pass `_H8FoundationPylonCameraWorldOffset` to the shader so it reconstructs world position before `TransformWorldToHClip`.
Rejected Alternatives: Baking absolute float world positions into `PylonMatrixDTO`, which violates the AUP rule, or using `_WorldSpaceCameraPos`, which can mismatch the target camera used for scheduling.
Scalability potential: Low/Middle/High/Ultra keep the same AUP-safe matrix layout; only active instance count and visual flare scale.
Hardware Impact: One cached `Vector4` material update when camera-offset batches change; prevents a correctness bug without adding per-vertex CPU work.

## Decision 17 - Vault Job Locks and Pure Editor Read
Problem: Scheduled jobs consumed DataVault-backed `NativeArray` views without explicitly blocking Vault compaction, and editor read state used `TryResolveHandle` rather than the pure `TryReadHandle` path.
Solution: Lock SHINOBU-owned buffers, socket input buffers, and optional encoded voxel SDF for the scheduled job lifetime with owner-tagged `TryLockBuffer`; unlock after finalized upload or teardown. Added `TryReadVaultViews` and routed editor/gizmo reads through `TryReadHandle`.
Rejected Alternatives: Relying on phase immobility documentation only, or letting read accessors touch fault telemetry/resolution counters.
Scalability potential: Quality still changes only math/upload density; lock coverage does not alter DTO identity or route ownership.
Hardware Impact: Adds a small fixed lock/unlock cost around batch scheduling; prevents relocation aliasing while Burst jobs hold Vault views.

## Decision 18 - Origin Snapshot and Player Material Path
Problem: The late-frame schedule path read `HectonFloatingOrigin.CurrentTotalOffsetDouble`, and player builds could keep `pylonMaterial` null because material creation was guarded by `UNITY_EDITOR`.
Solution: `FoundationPylonGpuBatch` now caches origin AUP during cold setup and receives committed shifts through `IOriginShiftListener`; pending batches are discarded on origin shifts. Material creation is available in player when `pylonShader` is serialized; only AssetDatabase lookup remains editor-only.
Rejected Alternatives: Hot registry polling for origin every frame, or requiring every prefab to serialize a ready material before the runtime can render.
Scalability potential: Low/Middle/High/Ultra use the same origin snapshot; quality does not change precision ownership.
Hardware Impact: Removes a hot registry-backed origin read and fixes player render initialization without adding hot allocations.

## Decision 19 - Cold Allocation Evidence Format
Problem: `FoundationPylonGpuBatch` had cold allocation comments that stated intent but did not match the exact root mandate format.
Solution: Updated the lock array, GraphicsBuffer, and runtime material fallback comments to `COLD ALLOC: Type[capacity] - reason - owner`.
Rejected Alternatives: Treating comment format as cosmetic; the project uses these markers for static audit.
Scalability potential: Low/Middle/High/Ultra unchanged; this preserves tooling/audit clarity without touching runtime math.
Hardware Impact: 0 us runtime; avoids future audit churn on weak development machines.

## Decision 20 - Camera Authority Cache
Problem: `FoundationPylonGpuBatch.EnsureCameraCold()` still used `Camera.main` as a cold fallback. It was not a hot LateFrame path, but the token creates a scene/tag search regression vector.
Solution: Removed `Camera.main`; cache `GlobalRegistry.Player` in cold setup and register `IGlobalRegistryHotSwapListener` so player camera replacements update the cached `targetCamera`.
Rejected Alternatives: Leaving the cold tag lookup, adding scene search, or polling `GlobalRegistry.Player` during `LateFrameTick`.
Scalability potential: Low/Middle/High/Ultra unchanged; camera authority route is independent of pylon quality density.
Hardware Impact: Avoids hidden tag lookup cost on setup/rebind frames and prevents future hot-path fallback drift.

## Decision 21 - CSV Scratch Buffer Ingest
Problem: The profile parser used `ReadOnlySpan<byte>`, but `TryLoadProfilesFromCsvFile` still allocated a managed `byte[]` with `File.ReadAllBytes` before handing data to the parser.
Solution: Read the cold designer CSV through `FileStream` directly into DataVault scratch buffer `70972` via `NativeArrayUnsafeUtility.GetUnsafePtr` and `Span<byte>`, then parse a slice of that scratch lane.
Rejected Alternatives: Keeping a managed `byte[]` allocation and arguing it was editor-only; the task specifically requires a zero-GC CSV bridge into unmanaged tuning rows.
Scalability potential: Low/Middle/High/Ultra unchanged at runtime; designers can reload low/mid/high/ultra ray profiles without changing pylon DTO identity or authority route.
Hardware Impact: 0 us hot path; avoids one CSV-size managed allocation per designer reload on low-end i3/MX350 editor hardware.

## Decision 22 - Named Foundation Buffer IDs
Problem: SHINOBU-owned Vault buffers used private numeric casts for `70960..70974`; a subagent compile-wall audit correctly flagged that as invisible ownership even though the ledger reserved the range.
Solution: Added named `BufferID.FoundationSnapping*` enum entries in `H8Memory.cs` and changed `FoundationSnappingCalculatorRuntime` constants to those symbols.
Rejected Alternatives: Continuing with local `(BufferID)709xx` casts or adding a parallel foundation-local enum that DataVault tools cannot inspect.
Scalability potential: Low/Middle/High/Ultra unchanged; this hardens authority identity and route tooling, not runtime fidelity.
Hardware Impact: 0 us runtime. Static ownership clarity avoids future integration churn and accidental buffer reuse on constrained developer hardware.

## Decision 23 - Low-Tier SDF and Vault Forensics Hardening
Problem: The low-quality route skipped the march loop but still paid the 6-sample SDF normal gradient; telemetry cursor memory was uninitialized; DataVault views could stale across locks; SDF dimensions used int products; CSV repeated module rows could collide; telemetry dump created a managed byte array; telemetry/cursor rows were outside the job lock window.
Solution: Low quality now uses the first nearest-neighbor SDF proxy read and an up-normal only. Telemetry cursor is seeded per handle generation and wrapped with unsigned modulo. Schedule preflight is read-only, then buffers are locked and Vault views re-resolved before jobs. Tuning/SDF configs are sanitized before scheduling. SDF products use 64-bit guards before int indexing. CSV rows land in fixed `profileIndex * MaxRaysPerModule + rayIndex` slots. Telemetry dump writes a `ReadOnlySpan<byte>` directly from native memory, and telemetry/cursor buffers are locked until finalize/upload ends.
Rejected Alternatives: Low-tier gradient sampling, trusting `UninitializedMemory` cursor content, retaining pre-lock `NativeArray` aliases, int SDF flat-index math, contiguous repeated CSV append assumptions, and managed `byte[]` dump staging.
Scalability potential: Low = one SDF read per ray, up-normal, one center support; Middle = nearest/trilinear blend and limited march; High = full trilinear march plus gradient normal; Ultra = maximum ray count, 96 steps, strongest shader flare. Authority route and DTO layout stay fixed.
Hardware Impact: Low-end i3/MX350 saves six SDF reads per hit plus any skipped march iterations; Vault re-resolve/lock cost is fixed and buys relocation safety. Native dump path avoids a telemetry-sized managed allocation during crash forensics.

## Decision 24 - Generated Project File Boundary
Problem: The local generated `.csproj` files have `EnableDefaultItems=false`; static search found no compile includes for the new SHINOBU_252 source files.
Solution: Recorded the project-generation gap and left generated project files untouched. Unity import/project regeneration must own those includes; hand-editing generated project files would create churn and conflict with Unity's asset database.
Rejected Alternatives: Manually adding `Compile Include` entries to generated `.csproj` files just to force local dotnet coverage.
Scalability potential: Runtime tiers unchanged. This protects compile-wall hygiene for all agents sharing the workspace.
Hardware Impact: 0 us runtime; avoids unnecessary project-file churn and broad recompilation on weak development machines.

## Decision 25 - Read Accessor Purity Naming
Problem: The internal helper `TryResolveModuleInputs` could populate the fallback preview module row when socket input was unavailable. That mutation is legitimate, but the `TryResolve*` name violates the doctrine that read accessors are pure.
Solution: Renamed the helper to `TryPrepareModuleInputs`; the pure preflight remains `TryResolveModuleInputRoute`, and the mutating fallback path is explicit through preparation/population naming.
Rejected Alternatives: Keeping the mutating `TryResolve*` name or splitting into a tiny job just to avoid a cold preview fallback write.
Scalability potential: Low/Middle/High/Ultra unchanged; this is authority-route clarity only.
Hardware Impact: 0 us runtime. Prevents future agents from treating a mutating preparation step as a pure read route.

## Decision 26 - Profile Row Concurrency Fence
Problem: CSV profile reloads could write `RayOrigins`/`ProfileRanges` while a scheduled pylon job was reading those DataVault rows.
Solution: Added SHINOBU profile read/write fences. `FoundationPylonGpuBatch` holds a profile read fence from schedule to `DispatcherJobFence.TryFinalizeCompleted`; `TryLoadProfilesFromCsvFile` refuses during active read fences and locks `RayOrigin/ProfileRange/CsvScratch` before parsing.
Rejected Alternatives: Treating CSV reload as harmless editor-only work, or relying on DataVault compaction locks as if they were writer locks.
Scalability potential: Low/Middle/High/Ultra unchanged in layout; profile rows can still author tier-specific ray positions without racing active jobs.
Hardware Impact: 0 us normal hot path beyond two integer fence operations per scheduled batch. Prevents undefined row tearing on low-end editor hardware.

## Decision 27 - Pure Socket/Foundation Read Views
Problem: The hot foundation schedule path still used resolve-style Vault view helpers that can mutate fault telemetry when handles are stale.
Solution: Removed SHINOBU-owned `TryResolveVaultViews`, routed foundation hot access through `TryReadVaultViews`, and added a narrow `ShinobuSocketConstructionRuntime.TryReadVaultViews` bridge using `TryReadHandle` for the consumed socket rows.
Rejected Alternatives: Continuing to call `TryResolveVaultViews` in VISUAL_SYNC, or inventing a new direct dependency on socket internals.
Scalability potential: Quality tiers unchanged; authority route is cleaner because reads do not publish fault telemetry or mutate global state.
Hardware Impact: Avoids hidden fault-telemetry mutation on stale handles; cost is equivalent pointer view construction.

## Decision 28 - Black Box Fault Dump
Problem: `NonFinite` flags were recorded, but the 300-frame telemetry ring was not dumped automatically on detected non-finite pylon math. Also one invalid-length branch failed to set `NonFinite`.
Solution: Non-finite resolved length now ORs `FoundationPylonFlags.NonFinite`; finalize calls `DumpTelemetry` when counters contain `NonFinite`.
Rejected Alternatives: Publishing only a warning signal or waiting for a crash handler to infer the pylon state later.
Scalability potential: Fault path is quality-independent; all tiers produce the same forensic proof artifact.
Hardware Impact: 0 us normal path except one flag test after telemetry write. Fault path writes 19.2 KB native telemetry to disk.

## Decision 29 - Continuous Ray Budget and Shader ALU Cut
Problem: Integer ray count could pop topology at quality thresholds, and the pylon shader spent per-vertex trig plus fragment `pow` and unguarded normalizations.
Solution: Added a shaped continuous `ResolveRayBudget`; transitional support rays fade in by radius/flare scale before becoming full supports. Replaced shader `sin/cos` with a 16-entry ring LUT, replaced `pow(x,2)` with `x*x`, and guarded normalizations with `SafeNormalize`.
Rejected Alternatives: Binary low/high ray-count switch, stochastic support popping, and hiding inactive supports only through fragment discard.
Scalability potential: Low = one center support and one nearest SDF lookup; Middle/High = fractional next support fades in; Ultra = four full supports and full flare. The shader buys visual contact with cheaper ALU.
Hardware Impact: MX350/Quest-class GPUs avoid 96 trigonometric evaluations per instance and one fragment `pow`; exact GPU timing still requires Frame Debugger/profiler.

## Decision 30 - Shader Player Inclusion
Problem: Runtime material fallback can only create the pylon material in player builds if the shader is serialized or included; static search showed no scene/prefab/material reference.
Solution: Added `Hecton_FoundationPylon.shader` GUID `0e3d6c95b94344c7b864f17da3f25205` to `ProjectSettings/GraphicsSettings.asset` `m_AlwaysIncludedShaders`.
Rejected Alternatives: Relying on `AssetDatabase` editor lookup, `Resources.Load`, or a manually serialized scene reference that was not present.
Scalability potential: All tiers use the same single-pass shader; inclusion does not change runtime quality decisions.
Hardware Impact: 0 us per frame; avoids missing shader/material fallback in player builds and enables boot-time shader availability.

## Decision 31 - CSV Fence and Four-Corner Default Route
Problem: The public CSV byte parser could be called directly while scheduled pylon jobs held profile rows for reading, broad header detection skipped valid module names beginning with `module`, empty module hash cells collapsed to the FNV offset basis, and the default no-CSV ray layout produced center-plus-three-corners at Ultra.
Solution: Wrapped `TryLoadProfilesFromCsvBytes` in the profile write fence, routed `TryLoadProfilesFromCsvFile` through a private unlocked parser after it already owns the write fence and Vault locks, rejected empty hash tokens, changed header detection to exact first-token matches, and made ray zero move continuously from center to the missing fourth corner as `GlobalQualityWeight` approaches 1.0.
Rejected Alternatives: Trusting external callers to take the write fence, keeping `StartsWith("module")`, accepting empty hash rows as a valid module profile, or increasing `MaxRaysPerModule` beyond the prompt's 1..4 budget.
Scalability potential: Low keeps the center support; Middle/High retain smooth transitional supports; Ultra resolves four corner supports without changing DTO layout or authority route. CSV-authored profiles still override the default pattern.
Hardware Impact: 0 us hot path beyond a `smoothstep` in the fallback profile route. Prevents profile mutation races and avoids one missing-corner visual support on high-end machines.

## Decision 32 - Shader ABI, Variant Warmup, and Radius Semantics
Problem: The pylon shader was always-included but not variant-preloaded, runtime `SetPass(0)` warmup could create a cold render hitch, transparent support rendering increased unsorted overdraw risk, and the procedural cylinder uses local radius 0.5 while the C# matrix stored radius in X/Z scale.
Solution: Added a pylon shader variant collection and preloaded it in `GraphicsSettings.asset`, removed component-local `SetPass`, changed the shader to opaque `Queue=Geometry` with `ZWrite On`, changed matrix X/Z scale to diameter, and computed draw bounds from local half-extents plus flare inflation. Extended layout/tests to include `FoundationPylonSurfaceDTO` tail offsets and matrix diameter proof.
Rejected Alternatives: Relying on scene material references, transparent procedural supports, runtime `SetPass` warmup, or matching visuals by doubling radius inside the shader.
Scalability potential: Low/Middle/High/Ultra keep one shader variant and one buffer ABI; higher tiers spend saved CPU/GPU budget on more active supports and stronger flare, not extra variants or transparent sort work.
Hardware Impact: Removes one cold `SetPass` call from the component, reduces transparent overdraw/sort risk on MX350/Quest-class GPUs, and fixes bounds underestimation that could cull wide flared pylons.

## Decision 33 - Socket Snapshot Fence and Schedule Failure Cleanup
Problem: Socket module buffers are a cross-domain input. Relocation locks and pure `TryReadHandle` views prevent pointer movement and fault telemetry, but they do not prove the socket owner cannot write the same module/counter rows while Foundation jobs read them. Profile CSV fences also used plain static ints, and schedule exceptions could leak Vault locks/fences.
Solution: Added `Interlocked`/`Volatile` fences for SHINOBU profile rows, exposed socket module read/write fences in `ShinobuSocketConstructionRuntime`, wrapped mock socket grid writes in the socket write fence, held the socket read fence from Foundation schedule through job finalization/teardown, and wrapped the schedule chain in `try/finally` cleanup. Exceptional partial schedules force-complete the last known handle before releasing locks; normal scheduling still returns the handle to the dispatcher. Pure output NativeArrays now carry `[WriteOnly]`, and public mock SDF indexing mirrors the 64-bit product guard.
Rejected Alternatives: Treating DataVault relocation locks as writer locks, adding an unowned direct dependency to a socket manager, blocking the main thread on normal scheduling, or documenting the race as owner-phase discipline without executable fences.
Scalability potential: Quality tiers are unchanged; low devices pay fixed atomic fence cost while avoiding row tearing, and high/ultra can safely consume denser socket module snapshots without changing DTO layout or authority route.
Hardware Impact: Normal path adds a few atomic read/write fence operations per scheduled batch. Fault path can force-complete only after a scheduling exception to release locks safely; this is not a same-frame hot-path completion.

## Decision 34 - Profile Edit Lock Ownership
Problem: `TryLoadProfilesFromCsvFile` always called `EndProfileEditLocks(vault)` in `finally`, even if only the first or second Vault buffer lock had been acquired before a later lock failed. Because DataVault buffer locks are reference-counted by buffer, not by caller identity, an unconditional unlock could decrement another owner phase's lock count.
Solution: `TryBeginProfileEditLocks` now returns the exact acquired lock count, and `EndProfileEditLocks(vault, lockedCount)` releases only `RayOrigin`, `ProfileRange`, and `CsvScratch` entries actually locked by this caller.
Rejected Alternatives: Keeping unconditional unlock for simplicity, or adding wider DataVault ownership metadata outside SHINOBU_252 scope.
Scalability potential: Low/Middle/High/Ultra unchanged; the CSV bridge remains cold/editor profile input and does not change runtime pylon authority or DTO layout.
Hardware Impact: 0 us hot path. Prevents rare cold reload lock-count corruption that could stall later low-end editor iterations or expose a buffer to relocation while another scheduled read expected it locked.

## Decision 35 - Bootstrap Shader Warmup and Legacy Socket Pure Read
Problem: Dewey found the pylon shader variant collection was preloaded in `GraphicsSettings` but absent from the explicit `GameBootstrapper` warmup list serialized in `00_BOOTSTRAP.unity`. Hegel also found the touched socket file was missing the `BinaryBlittableSafe` namespace and kept a legacy `TryResolveVaultViews` facade that still called `TryResolveHandle`.
Solution: Added pylon SVC GUID `0e3d6c95b94344c7b864f17da3f25207` to `00_BOOTSTRAP.unity` `shaderVariantCollections`, added the `Hecton8.Core.Memory.Layout` import, and changed `TryResolveVaultViews` into a pure compatibility wrapper over `TryReadVaultViews`.
Rejected Alternatives: Relying only on `ProjectSettings/GraphicsSettings.asset`, requiring all old socket callers to migrate in this pass, or leaving a read-looking compatibility method on resolve-side Vault telemetry.
Scalability potential: All tiers keep one shader variant and the same pure socket snapshot route; Low avoids surprise shader work during active play, Ultra spends budget on support density/flare rather than runtime variant discovery.
Hardware Impact: 0 us per frame. Boot warmup moves shader cost into the loading-screen route; pure socket compatibility avoids hidden resolution-counter mutation on active reads.

## Verification Gate
Problem: The prompt forbids launching dotnet build while CPU is under work or dotnet/csc is already running; later build attempts hit project-file errors outside SHINOBU_252.
Solution: First gate check blocked build at CPU 100 percent with seven `dotnet` processes. After the gate cleared, `dotnet build .\Assembly-CSharp.csproj --nologo /m:1` restored projects and failed on missing external files: `Assets/_Project/Scripts/World/Contracts/GroundRadarContracts.cs` and `Assets/_Project/Scripts/IBuildPlacementRule.cs`. A second post-polish guarded restore/build was launched only after `CPU=12%` and `dotnet/csc=0`; it failed on the same two external missing source files before any SHINOBU_252 compile diagnostic was emitted. After Loop 11, a gate sample was `CPU=28%` and `dotnet/csc=1`, so the next build was correctly suppressed. After Loop 14, static scans, JSON parsing, and whitespace checks passed; build was suppressed because the fresh sample was `CPU=91.7%` and `dotnet/csc=0`. After Loop 16, Loop 17, and Loop 20, build was again suppressed because `CPU=100%` and `dotnet/csc=0`.
Rejected Alternatives: Starting concurrent compiler work during saturation, inventing stubs outside the assigned domain, or reverting unrelated project-file changes.
Scalability potential: Prevents extra compile contention on weak developer hardware and keeps domain boundaries intact.
Hardware Impact: Guarded build attempts consumed restore/compile time only when the CPU/dotnet gate was open; both stopped before SHINOBU_252 source diagnostics because referenced projects were missing source files.
