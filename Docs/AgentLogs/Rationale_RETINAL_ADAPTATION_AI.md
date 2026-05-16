# RETINAL_ADAPTATION_AI Rationale

Status: BLOCKED BY DEPENDENCY - retinal/adjacent alpha telemetry scope static-verified after DataVault/ABI inquisition; project build fails in external systems.

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
