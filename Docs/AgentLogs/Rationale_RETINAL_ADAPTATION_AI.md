# RETINAL_ADAPTATION_AI Rationale

Status: CODE BUILD VERIFIED - retinal/adjacent fauna ABI scope static-verified after DataVault/ABI/typed-lane/core-native-array/active-slot/hash-map eviction and Pack=1 descriptor polish. Unity runtime/profiler verification still not executed.

## Decision 1 - Existing Owner Boundary
Problem: Prompt domain names `Assets/_Project/Scripts/AI/Perception/`, but the active source owner for predator utility cognition is `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs`; no `AI/Perception` folder exists.
Solution: Keep runtime state in the existing cognition owner and add only a narrow perception math helper under the requested domain if code changes are needed. This preserves the current dispatcher/job pipeline.
Rejected Alternatives: Creating a parallel perception manager would duplicate predator state, create ordering risk, and likely poll registry or signals from a hot path.
Scalability potential: Low keeps the existing 4-light cap and slow cadence. Middle/High/Ultra can spend saved CPU on richer steering thrash and presentation flashes without adding light raycasts.
Hardware Impact: Avoids new scene objects and collider queries; expected gain on i3/MX350 is avoiding a potential O(predators * lights) Unity-object path and keeping work in Burst-friendly flat arrays.

## Decision 2 - Visual Fake First
Problem: Headlight exposure could be modeled with real light queries, visibility raycasts, or cone colliders.
Solution: Use pure dot-product cone math, distance falloff, and existing predator utility states. Physical truth is not required for gameplay correctness here.
Rejected Alternatives: LightTrigger colliders and raycast line-of-sight checks were rejected because they allocate/cost more and break deterministic batch cognition. Full retinal optics were rejected as non-gameplay simulation.
Scalability potential: Low turns away. Middle keeps stable flee/frenzy scoring. High/Ultra can inject deterministic thrash and chaotic biolum presentation from the same blind flag.
Hardware Impact: Four light records and one float exposure lane are cache-resident; estimated cost remains well below 0.1 ms for 256 slots on i3/MX350 pending profiler proof.

## Decision 3 - Dot Product Repair
Problem: Existing code used `dot(predatorForward, lightToPredatorDir) <= -0.8`, which was behaviorally close but inverted relative to the prompt and easy to regress.
Solution: Added `RetinalExposureMath.ResolvePredatorToLightDot()` under `Assets/_Project/Scripts/AI/Perception/` and changed the job to use positive predator-to-light dot `> 0.9`.
Rejected Alternatives: Keeping the negative threshold was rejected because task 14 explicitly calls out inverted dot logic. Renaming only inside the large job was rejected because the prompt domain requested AI/Perception ownership.
Scalability potential: Low and Middle use identical scalar dot math. High and Ultra can layer extra reaction steering on the same positive dot without changing light-source data.
Hardware Impact: No extra memory traffic; one negation is folded into the dot math by Burst. Threshold tightens glare response and should reduce false-positive blind accumulation on i3/MX350.

## Decision 4 - Runtime Stress Cadence
Problem: Retinal low-cadence mode only followed low hardware tier, not actual runtime stress.
Solution: Added a cold scalar check against `SystemDispatcher.HomeostasisPressureLevel` and `CurrentFrameUnscaledDeltaTime` so predator retinal cognition drops to 1 Hz under pressure.
Rejected Alternatives: Adding a new signal/event was rejected as unnecessary single-use traffic. Polling a separate performance service was rejected because dispatcher already owns the frame pressure read-model.
Scalability potential: Low/stressed uses 1 Hz retinal checks. Middle/High/Ultra stay at predator utility cadence until frame pressure rises.
Hardware Impact: Saves repeated retinal work during frame budget misses; expected benefit is proportional to active predator count, with one cheap branch per schedule preparation.

## Decision 5 - High-Tier Thrash And Frenzy
Problem: Existing blind aversion turned predators away, but high-tier behavior did not add visible thrash, and frenzy species only doubled current aggression instead of maxing it.
Solution: Added deterministic triangle-wave steering noise for high-tier blinded flee and changed retinal frenzy to clamp aggression to `1f`.
Rejected Alternatives: Random noise was rejected because it breaks determinism. Rigidbody impulses were rejected because they are physical simulation for a visual response.
Scalability potential: Low uses lateral flinch. Middle keeps utility response. High/Ultra get more violent motion from scalar waves and can bind the existing blind signal to richer biolum presentation.
Hardware Impact: Additional cost only on blinded high-tier predators; estimated under 1 us for a small active group on i3/MX350 and zero cost for non-blinded predators.

## Decision 6 - Finite Guard And Exponential Decay
Problem: Retinal exposure recovery was linear, and the light loop did not explicitly reject non-finite reconstructed AUP positions before distance and dot math.
Solution: Replaced darkness recovery with `FastExpNegPade13` exponential decay and added finite guards before distance, dot, and stimulus accumulation.
Rejected Alternatives: Full `math.exp` was unnecessary because the local job already uses a Pade approximation. Letting the post-job scan clean NaN later was rejected because bad state could influence output first.
Scalability potential: Low uses the same cheap decay and guards. High/Ultra can keep longer blind hold presentation without changing simulation truth.
Hardware Impact: Adds a few branch-only finite checks per candidate light; avoids NaN propagation and black-box dumps during corrupt signal input. Expected MX350 impact is sub-microsecond at 4-light cap.

## Decision 7 - Compile Bridge And Dependency Wall
Problem: The repository `Hecton8.Core.csproj` uses an explicit source include list, so the new `Assets/_Project/Scripts/AI/Perception/RetinalExposureMath.cs` file was not compiled by the first validation build.
Solution: Added a single compile include for the new perception math file, then reran `dotnet build` to isolate my changes from existing workspace failures.
Rejected Alternatives: Moving the helper into the fauna file was rejected because the prompt domain explicitly names `AI/Perception`. Editing player kinematics, animation IK, or VFX listener systems was rejected because they are outside the retinal task domain and already fail independently.
Scalability potential: Low/Middle/High/Ultra behavior is unaffected by the project bridge entry; the runtime math remains bounded by the 4-light retinal cache and tier/stress cadence.
Hardware Impact: Runtime impact is 0 us/frame. Compile validation is blocked by unrelated dependency surfaces, not by retinal math.

## Decision 8 - Retinal DataVault Eviction
Problem: Retinal arrays were persistent static `NativeArray` fields allocated by `PredatorCognitionDomain`, which violates current DataVault sovereignty.
Solution: Added `RetinalAdaptationVault` and new `BufferID.PredatorRetinal*` lanes. `PredatorCognitionDomain` now resolves DataVault aliases for exposure, blindness, last-published blind state, light sources, and retinal telemetry.
Rejected Alternatives: Keeping local retinal arrays was rejected. Moving every unrelated cognition array in this pass was rejected as cross-domain integrator work with high break risk; the retinal task-owned buffers were migrated.
Scalability potential: Low/Middle/High/Ultra now share central relocation/generation ownership. High/Ultra can read the same blind state for presentation without duplicating truth.
Hardware Impact: Runtime cost after cold resolve is 0 us/frame. i3/MX350 benefit is centralized memory ownership and no local retinal persistent allocation churn.

## Decision 9 - ARM64 ABI Packing
Problem: Retinal light and telemetry records used Pack=4 or implicit tail padding; Quest/Android requires deterministic blittable stride.
Solution: `LightSourceData` is Pack=1 Size=96 with explicit tail fields. `RetinalTelemetryEntry` is Pack=1 Size=32 with explicit reserved tail. DataVault base alignment remains 64-byte.
Rejected Alternatives: Relying on compiler padding was rejected. Shrinking telemetry to 28 bytes was rejected because 32-byte stride is cleaner for DataVault and binary tooling.
Scalability potential: All tiers get the same ABI. High/Ultra can consume telemetry without platform-specific marshal assumptions.
Hardware Impact: Light cache grows by 8 bytes per source, max 32 bytes total at 4-light cap. Telemetry remains 32 bytes per frame, 9600 bytes total for 300 frames.

## Decision 10 - High-Tier Biolum Strobe
Problem: Blind state was published, but no presentation consumer made blinded predators visibly flash.
Solution: Fauna presentation now consumes the existing `FaunaStateChangedSignal` typed lane through `ReadOnlySpan<FaunaStateChangedSignal>` and applies a high-tier-only deterministic triangle-wave biolum strobe.
Rejected Alternatives: A new retinal VFX signal was rejected as duplicate. Direct Biolum manager calls were rejected as cross-domain coupling. Random flicker was rejected for determinism.
Scalability potential: Low/Medium skip the strobe and keep the cheap flee/frenzy response. High/Ultra spend saved cycles on visible chaotic biolum pulses from the same blind truth.
Hardware Impact: Low-end impact is 0 us/frame because the strobe is tier-gated. High-tier cost is a small span scan and two triangle waves while the fauna brain ticks.

## Decision 11 - Alpha Black-Box Vault Alias
Problem: The same cognition owner still had one private Alpha Leviathan telemetry `NativeArray`, leaving a data-sovereignty exception next to the retinal buffers.
Solution: Resolve `_alphaLeviathanTelemetryRing` from `GlobalDataVault` using the existing `BufferID.AlphaLeviathanTelemetryRing` lane, request the full 300-frame * 64-slot capacity, and write frame/slot-indexed entries. Retinal slot reset now goes through `ClearRetinalSlot` so registration does not index unavailable vault aliases during bootstrap.
Rejected Alternatives: Keeping the local ring was rejected because it preserved private persistent telemetry ownership. Adding a new buffer ID was rejected because an Alpha telemetry lane already exists. Requesting only 300 entries was rejected because a later 19,200-entry owner could resize the DataVault block and invalidate stale views. Returning registration failure when DataVault is late was rejected because it would break fauna spawn order for a non-authoritative retinal cache.
Scalability potential: Low/Middle/High/Ultra share one central black-box lane. Low-end devices keep fault-only disk I/O. High/Ultra can retain per-slot Alpha telemetry without another allocation owner.
Hardware Impact: Runtime ownership cost remains 0 us/frame after cold resolve. Memory footprint for this shared lane is 19,200 entries by design, matching the existing 300-frame/64-slot Alpha telemetry contract. Register/unregister adds three cold-path `IsCreated` checks; no profiler microseconds were measured.

## Decision 12 - Retinal Signal Sanitation And Strobe Sentinel
Problem: Retinal light cache upsert still trusted dequeued signal scalars, and high-tier blind-strobe duplicate suppression used `0` as its implicit first-frame value.
Solution: Reject non-finite light AUP/range/intensity/spot values and brownout-suppressed lights before cache upsert, clamp finite range/intensity/spot values in the cache record, and use `uint.MaxValue` as the blind-strobe frame sentinel reset on fauna lifecycle transitions.
Rejected Alternatives: Relying only on `GlobalSignals` sanitation was rejected because the retinal cache is a four-slot scarce resource and should be self-defending. Keeping frame `0` as the strobe sentinel was rejected because frame-0 signals and pooled objects are valid runtime cases.
Scalability potential: Low-tier devices avoid wasting one of four light records on corrupt payloads. Middle/High/Ultra get the same deterministic headlight truth, while high-tier strobe no longer misses the first valid blind frame.
Hardware Impact: Signal sanitation adds five scalar branches per dequeued light signal and zero per-predator hot-loop cost. Sentinel reset is cold lifecycle work only. No profiler microseconds were measured.

## Decision 13 - DataVault Duplicate Compile Unblock
Problem: `GlobalDataVault` contained two identical `ValidateAbiLayout()` methods, blocking compilation of the DataVault interface now used by retinal buffers.
Solution: Removed the second identical method body only. The remaining validator still runs from `EnsureInitialized()`.
Rejected Alternatives: Leaving the compile wall was rejected because this retinal pass depends on DataVault for all retinal state. Reworking DataVault ABI validation was rejected as cross-domain overreach.
Scalability potential: All tiers retain the same DataVault ABI validation path; no runtime behavior changes.
Hardware Impact: Runtime impact is 0 us/frame; this was compile-only duplicate removal. No profiler microseconds were measured.

## Decision 14 - Typed Headlight Signal Lane
Problem: Retinal cognition still used the compatibility `GlobalSignals.TryDequeueSubmarineLightsChanged` shape, which models a destructive single-consumer queue and conflicts with the typed-lane/read-only-span mandate now used by other consumers.
Solution: `PredatorCognitionDomain` now reads `SignalBus<SubmarineLightsChangedSignal>.GetFrameSnapshot()` as `ReadOnlySpan<SubmarineLightsChangedSignal>` and processes only the newest 64 entries before the four-slot retinal cache. Current `GlobalSignals.Publish(in SubmarineLightsChangedSignal)` already routes to the typed lane, so no duplicate signal was created.
Rejected Alternatives: Keeping destructive queue consumption was rejected because it can starve other consumers and hides ordering behind legacy API shape. Dual-publishing queue plus lane was rejected because divergent truth would be worse than the original problem. A new retinal-specific headlight signal was rejected as interface duplication.
Scalability potential: Low/toaster path scans at most 64 signal records and still evaluates at the existing stress cadence with a four-light retinal cache. Middle keeps the same deterministic cache truth. High/Ultra can let multiple systems, including silt/boid/predator presentation, observe the same typed headlight lane without cross-domain polling.
Hardware Impact: Work is bounded to `min(snapshot length, 64)` records per cognition tick and zero managed allocation. No profiler microseconds were measured; the static cost is signal-scan only, not per-predator hot-loop work.

## Decision 15 - Core Cognition NativeArray Vault Eviction
Problem: Retinal arrays were vaulted, but the same cognition owner still allocated broader predator `NativeArray` lanes locally for cores, controls, input/output, memory, swarm scratch, pack roles, claim tables, siege tables, and evaluation cadence.
Solution: Added `BufferID.PredatorCognition*` lanes and resolved every domain-owned `NativeArray` through `GlobalDataVault.GetBuffer<T>` with `SystemID.AICognition`. Teardown now releases aliases without disposing vault memory. Cold initialization clears reused vault buffers and releases partial aliases if any buffer fails to resolve.
Rejected Alternatives: Keeping local persistent `new NativeArray` ownership was rejected because the user mandate explicitly rejects private `NativeArray` state. Disposing the DataVault arrays from `PredatorCognitionDomain` was rejected because the vault owns their lifetime. Migrating `NativeList` and `NativeParallelHashMap` into this pass was rejected because `IDataVault` currently exposes `NativeArray<T>` buffer views only; converting those containers requires a separate structural rewrite of active-slot and hash lookup semantics.
Scalability potential: Low/toaster keeps the same 256-slot bounded arrays and stress cadence, but centralizes memory ownership and eliminates local native-array allocation churn. Middle uses the same deterministic data layout. High/Ultra can read the same vault-owned cognition truth for richer presentation, telemetry, and postmortem analysis without duplicating state.
Hardware Impact: Runtime ownership cost after cold DataVault resolve is 0 us/frame. Cold clearing touches bounded 256-slot lanes plus fixed memory banks during domain initialization only. No profiler microseconds were measured.

## Decision 16 - Active Slot NativeList Eviction
Problem: After core `NativeArray` eviction, `PredatorCognitionDomain` still kept active predator slots in a local persistent `NativeList<int>`, leaving one native container with private ownership and capacity semantics outside the DataVault.
Solution: Added `BufferID.PredatorCognitionActiveSlots` and resolved `_activeSlots` as a vault-backed `NativeArray<int>` with `SystemID.AICognition`. Added `_activeSlotCount` as the dense active-window count, replaced `AddNoResize`/`RemoveAtSwapBack` with explicit append and swap-back removal, and scheduled jobs using `_activeSlotCount`. Reset `_activeSlotCount` on partial vault failure, alias release, cold clear, and dispose.
Rejected Alternatives: Keeping `NativeList<int>` was rejected because it preserved local persistent native ownership. Iterating the full 256-slot vault buffer was rejected because stale cleared slots would pollute swarm bounds and telemetry. Inventing a new list-capable vault API was rejected as a cross-domain interface change during batch execution.
Scalability potential: Low/toaster path scans only active predators and keeps the 1Hz stressed retinal cadence. Middle keeps deterministic dense scheduling. High/Ultra still spend saved cycles on deterministic retinal thrash and biolum strobe without duplicating active-slot truth.
Hardware Impact: Runtime ownership cost after cold DataVault resolve is 0 us/frame. Swap-back removal remains O(active slots) search plus O(1) removal, matching the old list behavior. Avoiding full-capacity stale scans reduces memory traffic on i3/MX350, but exact microseconds were not measured. `dotnet build` is green with 0 warnings and 0 errors; Unity runtime/profiler evidence is absent.

## Decision 17 - Native HashMap Vault Eviction
Problem: After the `NativeArray` and active-slot passes, `PredatorCognitionDomain` still owned two local persistent `NativeParallelHashMap` containers for species pack target sharing and species tuning lookup.
Solution: Added vault-backed SoA lanes for species target ids, target positions, target count, species tuning ids, species tuning values, and tuning count. `SwarmAnalysisJob` now appends species targets into bounded arrays through an atomic count, and `PredatorCognitionJob` resolves species targets/tuning through bounded count-window scans. `SpeciesCognitionTuning` is now Pack=1 Size=32 and validated by `UnsafeUtility.SizeOf`.
Rejected Alternatives: Keeping local `NativeParallelHashMap` ownership was rejected because it preserved private native state. Adding a hash-map API to `IDataVault` was rejected as a batch-time interface change with wider integration risk. A managed dictionary was rejected because Burst jobs cannot read it and it would violate zero-GC/hot-path rules.
Scalability potential: Low/toaster keeps the active species window bounded to 256 and avoids hash-map ownership. Middle keeps deterministic array data flow. High/Ultra continue using pack coordination and retinal visual overkill from the same vault-owned truth without a duplicate target registry.
Hardware Impact: Runtime ownership cost after cold DataVault resolve is 0 us/frame. Lookup changes from hash probe to bounded linear scan over <=256 entries; exact CPU delta was not measured. For MX350/i3, the scan is cache-linear and avoids separate hash-map allocation metadata. `dotnet build` is green with 0 warnings and 0 errors; Unity runtime/profiler evidence is absent.

## Decision 18 - Shared Workspace Build Wall
Problem: A fresh build after the reporting update no longer passes because the shared workspace has new compile failures outside the retinal/cognition files.
Solution: Keep the retinal implementation intact, record the current build wall as external, and avoid cross-domain edits to gameplay tool, bootstrap, or fluid feedback systems without ownership. The retinal static audit remains clean.
Rejected Alternatives: Editing `GameBootstrapper`, `FluidFeedbackListener`, `PlayerTool`, `PlayerToolManager`, or `PlayerNoiseEmitter` from the retinal agent was rejected because those are outside the assigned AI/COGNITION retinal domain and would risk trampling parallel agents' work. Reporting the older green build as current was rejected because it would be false after the fresh build.
Scalability potential: Low/Middle/High/Ultra retinal behavior is unchanged by the external compile wall: the domain still uses four-light dot-product exposure, stress cadence, DataVault-backed SoA state, typed signal lanes, black-box telemetry, and high-tier deterministic visual overkill.
Hardware Impact: No runtime change. Latest build failed with 11 external errors and 0 retinal/cognition errors; exact microseconds remain unmeasured because Unity runtime/profiler captures were not executed.

## Decision 19 - RuntimeDescriptor Pack=1 ABI Closure
Problem: `FaunaDataTemplate.RuntimeDescriptor` was explicit-size 64 bytes but still declared `Pack = 4`, leaving one adjacent fauna runtime payload outside the current ARM64/Quest Pack=1 mandate.
Solution: Changed `RuntimeDescriptor` to `StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)` and kept the explicit size unchanged. Re-ran a Pack audit across `PredatorCognitionDomain`, `AI/Perception`, and `FaunaDataTemplate`.
Rejected Alternatives: Keeping Pack=4 was rejected because the inquisition mandate now requires Pack=1 on native payload structs in the retinal/fauna cognition scope. Repacking fields or changing the size was rejected because the payload already has a stable 64-byte size and only the packing contract needed closure.
Scalability potential: Low/toaster, Middle, High, and Ultra all receive the same deterministic descriptor stride. This does not alter retinal behavior: low path remains dot-product glare and turn-away/flee; high/ultra keep deterministic thrash and biolum strobe from the same DataVault truth.
Hardware Impact: Runtime impact is 0 us/frame; this is ABI metadata on a 64-byte descriptor. `dotnet build` is green with 0 warnings and 0 errors. Exact microseconds were not measured; Unity runtime/profiler verification remains absent.

## Decision 20 - Volatile Shared Build Evidence Handling
Problem: Multiple build walls appeared and disappeared in external systems while parallel agents edited the workspace, making stale compiler output unsafe as final evidence.
Solution: Treat only the latest current-state compiler pass as final build evidence, and record intermediate external walls as volatile shared-workspace observations. The retinal pass only changed the fauna descriptor Pack value after the previous report; no external source file edits were made by this pass.
Rejected Alternatives: Claiming ownership of external fixes was rejected because the source changes landed outside this pass. Editing `PhysicsApplySystem`, `TetherInstance`, `SargassumMicroFaunaBoids`, or `LockstepStateValidator` from the retinal agent was rejected once current file snapshots showed those walls had already moved or cleared.
Scalability potential: Retinal low/mid/high/ultra behavior is unchanged. The system remains dot-product glare, DataVault SoA state, typed signal lanes, finite guards, 300-frame black-box telemetry, low-tier turn-away, and high-tier deterministic visual overkill.
Hardware Impact: No runtime change. Final current-state `dotnet build` is green with 0 warnings and 0 errors. Exact microseconds remain unmeasured; Unity runtime/profiler verification remains absent.

## Decision 21 - Blind-State AUP Authority And Helper Payload ABI Closure
Problem: Blind-state publication still reconstructed AUP from runtime-relative cognition core position, and private helper payloads relied on implicit layout with bool fields that can be ambiguous on ARM64/Quest.
Solution: Resolve blind-state signal AUP from the slot input position plus committed floating-origin offset and call `AbsoluteUniversePosition.FromAbsolutePosition`. Add created checks before retinal telemetry indexes vault aliases. Mark `RetinalLightResult` and `AlphaLeviathanDirective` as `Pack = 1` explicit-size payloads and replace directive bools with byte flags.
Rejected Alternatives: Keeping `FromRuntimePosition` was rejected because it can publish relative coordinates as absolute truth. Adding a new blind signal was rejected because `FaunaStateChangedSignal` already carries the state. Keeping implicit bool layout was rejected because platform ABI clarity matters more than convenience. Editing unrelated transient build-race files was rejected as cross-domain overreach.
Scalability potential: Low/toaster still uses the same four-light dot-product exposure, stress cadence, and turn-away/flee fake. Middle keeps deterministic AUP-relative signal truth. High/Ultra retain deterministic thrash and typed-lane biolum strobe from the same blind state without duplicating data.
Hardware Impact: Hot retinal candidate math is unchanged. Added work is limited to edge signal AUP construction and two telemetry alias guards; exact microseconds were not measured. Latest heartbeat `dotnet build` is blocked by an external `DiegeticGyroCompassRuntime` `NativeSlice.IsCreated` compile error; no emitted errors cite retinal/cognition files. Unity runtime/profiler evidence is absent.

## Decision 22 - External Build Wall Cleared Without Cross-Domain Edit
Problem: The previous current-state build was blocked by an external UI/navigation `NativeSlice.IsCreated` compile error, preventing retinal final validation from being current.
Solution: Rechecked the source before editing; `DiegeticGyroCompassRuntime` had already changed to a length guard. Reran the build from current files and recorded the green result. Retinal-domain static audits were rerun after the build to keep the current status grounded in evidence.
Rejected Alternatives: Editing UI/navigation from the retinal agent was rejected because the file was outside the assigned domain and no longer needed a retinal-owned patch. Reporting the older blocked build was rejected because the current source now builds. Claiming profiler microseconds from a compiler pass was rejected.
Scalability potential: Low/toaster, Middle, High, and Ultra retinal behavior is unchanged: four-light dot-product exposure, DataVault SoA state, typed signal lanes, finite guards, 300-frame telemetry, low-tier turn-away, and high-tier deterministic thrash/biolum strobe.
Hardware Impact: Runtime change is 0 us/frame because no runtime source changed in this loop. Current `dotnet build` is green with 0 warnings and 0 errors. Exact microseconds remain unmeasured; Unity runtime/profiler verification remains absent.

## Decision 23 - Dump-Failure Telemetry Without Managed Log Strings
Problem: Retinal and adjacent Alpha black-box dump failure catches still used `Debug.LogError` with string concatenation, leaving managed string reporting in a critical failure path.
Solution: Replace the log strings with hashed `GlobalTelemetryBus.PublishPerformanceWarning` calls using dedicated dump-failure hashes. The binary dump path remains the primary evidence; dump-write failure now still surfaces as data telemetry without managed message formatting.
Rejected Alternatives: Keeping `Debug.LogError` was rejected because it is not data-oriented and allocates strings on the exact path that should report failure safely. Writing a second text sidecar was rejected because disk failure is the condition being handled. Swallowing the exception silently was rejected because it hides a black-box failure.
Scalability potential: Low/toaster avoids managed logging and keeps fault-only disk I/O. Middle/High/Ultra receive the same stable telemetry hash and can surface richer UI or tooling outside the cognition owner without a new signal.
Hardware Impact: Steady-frame runtime cost is 0 us/frame. Failure-path cost is one telemetry warning publish instead of managed log string creation; exact microseconds were not measured. Current `dotnet build` is green with 0 warnings and 0 errors; Unity runtime/profiler verification remains absent.
