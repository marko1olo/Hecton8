# Rationale_FAUNA_BITE_IK_SOLVER

Created 2026-05-16.

## Decision 0 - Fresh Agent Memory
Problem: Required status/rationale files were missing at startup.
Solution: Create fresh files before code edits so context survives compression and batch progress is auditable.
Rejected Alternatives: Writing progress only in chat is rejected by batch protocol and loses state under compression.
Scalability potential: Low/Middle/High/Ultra unaffected; this is process infrastructure.
Hardware Impact: No runtime impact on i3/MX350.

## Decision 1 - Mandate Set
Problem: Bite IK crosses animation, physics-contact truth, AUP, signal, telemetry, and allocation domains.
Solution: Read OPT_Zero_GC_Policy_AllocFree_Mandate, PHYS_Physics_Integrity_Determinism_ForceMode, ANIM_Contextual_Physical_IK, ANIM_IK_FABRIK_GroundSnapping_Procedural, MATH_AUP_Determinism_Sync, MATH_Coordinate_Precision_AUP_FloatingOrigin, DBG_Telemetry_Crash_Reporting_PostMortem, and ARCH_Signal_Lane_Segregation.
Rejected Alternatives: Only reading required prompt files would leave Black Box and SignalBus rules under-specified; reading the entire registry would pollute focus.
Scalability potential: Low uses head aim fake; Middle uses jaw IK; High uses independent jaw/tentacle wrapping; Ultra adds richer telemetry and appendage overkill in VISUAL_SYNC.
Hardware Impact: Mandate choice forces bounded jobs, no managed allocations, no Unity physics overlaps, and load-shed path for i3/MX350.

## Decision 2 - Integration Surface
Problem: Bite IK must mutate the same `LeviathanBones` NativeArray that the GPU upload already consumes, but the assigned folder did not exist.
Solution: Create the authoritative `Assets/_Project/Scripts/Animation/Fauna/` surface for bite IK types/jobs and add a narrow integration call inside `FaunaKinematicsRuntime` after the existing spine job completes and before GPU upload.
Rejected Alternatives: A separate bone buffer would violate the task's direct `LeviathanBones` mutation requirement; Unity Animator IK is explicitly forbidden; runtime Physics overlaps are forbidden by prompt.
Scalability potential: Low rotates/scales the head bone only, Middle solves jaw, High/Ultra add mandible/tentacle wrap around target bounds.
Hardware Impact: MX350 path is constant-time head aim fake; high-tier path spends saved CPU on richer bone placement and contact feedback.

## Decision 3 - DataVault Bite State
Problem: Jaw target, current pose, and blackbox telemetry must survive outside managed animation object state without per-frame allocation.
Solution: Add `JawIkTargets`, `CurrentJawPos`, `BiteIkSolveEvents`, and `BiteIkTelemetryCursor` IDs to `H8Memory.BufferID`, then alias vault-owned NativeArrays from `FaunaKinematicsRuntime`.
Rejected Alternatives: Private arrays on the MonoBehaviour would hide state from other systems and duplicate memory; managed `List<T>` would violate zero-GC hot path.
Scalability potential: Low stores one target and one pose; Middle/High/Ultra use the same fixed buffers and spend only math, not memory churn.
Hardware Impact: Expected i3/MX350 gain is zero allocations and stable cache footprint; 300 telemetry entries are fixed-size and bounded.

## Decision 4 - Bite Kernel Math
Problem: Canned predator bites clip target hulls and world-space float math loses precision at large AUP offsets.
Solution: `ProceduralBiteJob` converts target AUP to predator-local space, uses bounded closest-point descent against a target AABB, clamps all reach/acos values, and writes bone matrices directly.
Rejected Alternatives: Unity `Animator.SetIKPosition`, physics overlaps, and exact hull mesh queries were rejected as non-deterministic, slower, and outside the Burst math mandate.
Scalability potential: Low uses head aim fake; Middle solves two mandibles; High wraps tentacle anchors around a cylinder approximation; Ultra reuses high-tier math without expanding allocations.
Hardware Impact: MX350 avoids multi-bone IK under load; RTX-tier devices spend the same deterministic frame on richer mandible/tentacle placement.

## Decision 5 - Signals and Feedback
Problem: Strike, spark, haptic, and snap audio must be decoupled from direct predator-to-target calls.
Solution: Publish and consume `FaunaStateChangedSignalKinds.Strike`, then emit `DebrisSpawnSignal`, `HapticRequest(ChannelCrush)`, and `AcousticPingSignal(ChannelJawSnap)` from completed `CurrentJawPos` flags.
Rejected Alternatives: `BiteManager.Instance`, animation event damage, and direct component calls were rejected because 20+ concurrent agents need typed lanes and GlobalRegistry/DataVault boundaries.
Scalability potential: Low still gets head fake and throttled feedback; High/Ultra adds visual spark/crush/audio exactly when IK contact reaches the hull.
Hardware Impact: Feedback runs after job completion with frame throttles; low-end systems avoid per-frame contact object allocation.

## Decision 6 - Snap Miss Recovery
Problem: A target outside maximum jaw reach needs an explicit procedural miss instead of silent full extension.
Solution: Add a deterministic local-space triangle-wave recoil for `ResultFlagMiss`, keeping the creature visually retracting without requesting a canned clip.
Rejected Alternatives: Playing a recovery animation clip or extending reach beyond the configured maximum would reintroduce clipping and content dependency.
Scalability potential: Low shows the miss as head recoil; Middle/High/Ultra show the same recoil with mandible/tentacle detail layered on top.
Hardware Impact: Recoil is a few scalar ops and one normalized vector; no measurable i3/MX350 cost versus the solve.

## Decision 7 - Compile Wall
Problem: Final `dotnet build` cannot exit 0 because unrelated cross-agent systems currently fail compilation.
Solution: Retry restore/build, verify no emitted errors target the bite IK file or bite integration lines, and mark final validation as `[BLOCKED BY DEPENDENCY]`.
Rejected Alternatives: Editing `GlobalRegistry`, voxel debris, bootstrap contracts, world/VFX bridge symbols, player motor helpers, or missing signal contracts would violate the assigned domain boundary.
Scalability potential: Bite IK scalability remains implemented; integration cannot be objectively master-validated until the shared compile wall is cleared.
Hardware Impact: No runtime impact from the blocked validation. The implemented path remains bounded for i3/MX350 and scales visual detail on high-end devices.

## Decision 8 - Omega Polish Result
Problem: Omega polish demands `VERIFIED MASTER GRADE`, but objective build validation is impossible while external compile failures remain.
Solution: Run anti-bloat scans on owned code, write the final log, and report the status as dependency-blocked instead of claiming the grade.
Rejected Alternatives: Faking master-grade status or expanding into unrelated systems would violate evidence-based coding and the domain boundary.
Scalability potential: Low/Middle/High/Ultra bite IK paths remain present; polish grade is blocked by repository state, not by the bite LOD design.
Hardware Impact: No runtime impact. The low-end and high-end paths remain bounded as recorded above.

## Decision 9 - Vault Handles Over Cached Arrays
Problem: The second H-Phi audit found `FaunaKinematicsRuntime` still carried persistent `NativeArray<T>` fields for spine, bone, and telemetry views even though ownership had moved to `GlobalDataVault`.
Solution: Replace those fields with generation-checked `VaultBufferHandle<T>` fields and resolve short-lived views only at scheduling, GPU upload, origin-shift rebase, and telemetry dump boundaries.
Rejected Alternatives: Leaving private `NativeArray<T>` fields would preserve a feudal data island; resolving buffers by raw pointer without a generation check would make relocation bugs silent.
Scalability potential: Low/Middle/High/Ultra all share one vault-owned bone stream. Low-tier still uses the cheap head fake; High/Ultra still spend only math and VFX signals, not memory churn.
Hardware Impact: i3/MX350 gain is lower stale-view risk and no private array lifetime to leak; high-end devices keep the same GPU upload path for visual overkill bones.

## Decision 10 - Stale Bite Feedback Gate
Problem: If a strike ended after a contact frame, the previous `CurrentJawPos` flags could survive in the vault and allow late spark/haptic/audio feedback after the target was gone.
Solution: Clear target and rest pose on inactive strike, then gate feedback by nonzero target hash and the most recent solved frame before publishing debris, haptics, hull dents, or jaw snap audio.
Rejected Alternatives: Trusting cooldowns alone would still leak stale feedback; clearing the whole telemetry ring would erase black-box evidence.
Scalability potential: Low-tier gets no fake contact spam; High/Ultra retain overkill feedback only on current solved contact.
Hardware Impact: A couple of scalar frame/hash checks prevent needless signal traffic on MX350 and Steam Deck while preserving high-tier effects when real contact exists.

## Decision 11 - Deterministic Shared IK Stream
Problem: Bite IK consumes the shared Leviathan bone stream after terrain IK; `FloatMode.Fast` in the terrain pass could diverge across ARM64, Metal, and x86 targets.
Solution: Move the shared terrain IK job to deterministic Burst mode and tag its vault helper allocations with `SystemID.AnimationFauna`.
Rejected Alternatives: Keeping fast math was cheaper but weaker for cross-platform reproducibility; leaving `AICognition` ownership on animation buffers hid the true memory owner.
Scalability potential: Low keeps one-iteration terrain/head fake; High/Ultra keep full segment and appendage placement without adding allocations.
Hardware Impact: Expected MX350 cost is bounded by existing low-tier segment count; determinism buys fewer cross-platform animation faults, which is worth the tiny scalar overhead.

## Decision 12 - Managed State Hook Removal
Problem: `FaunaBrain` exposed `Action<AIState> OnStateChanged` and invoked it on state changes, creating a managed delegate escape hatch in the same integration surface as the bite strike publisher.
Solution: Remove the field and invocation after confirming no in-repo subscribers. Strike state now remains on the typed `FaunaStateChangedSignal` lane.
Rejected Alternatives: Keeping an unused delegate violates the no-private-callback rule; replacing all unrelated legacy `PhysicsEventBus` fauna audio/EMP behavior would cross physics/audio ownership and is marked as external debt instead of faked as fixed.
Scalability potential: Low/Middle/High/Ultra avoid an unused managed callback branch. Bite signal flow stays typed-lane only.
Hardware Impact: Estimated i3/MX350 gain is a few scalar branch/call checks avoided on state transitions; main value is removing a managed extension point from this slice.

## Decision 13 - Bite Buffer Handle Segmentation
Problem: The bite feedback path previously resolved target, pose, telemetry ring, and cursor together even when only current pose was needed.
Solution: Keep DataVault ownership, but split helpers into full solve buffers, pose-only feedback, and telemetry-only dump paths through `VaultBufferHandle<T>`.
Rejected Alternatives: Reintroducing private `NativeArray<T>` views would violate H-Phi data eviction; resolving every bite buffer for every feedback check burns cache and expands the failure surface.
Scalability potential: Low/Middle/High/Ultra share the same fixed vault packets. Low-tier feedback only touches current pose; High/Ultra still get telemetry and contact signals without extra allocations.
Hardware Impact: Estimated i3/MX350 gain is small, roughly 3-10 us on feedback frames from avoiding unnecessary vault resolves. No profiler capture was available; this is a static estimate only.

## Decision 14 - Target-Oriented Hull Approximation
Problem: Predator-axis closest-point and cylinder wrapping were cheap but wrong when the submarine or target hull was angled relative to the predator.
Solution: Use the target basis vectors from `JawIkTarget` to solve against an oriented box and wrap tentacle anchors around the target-forward cylinder axis in predator-local space.
Rejected Alternatives: Mesh collision, physics overlap/cast, or per-triangle hull queries were rejected as non-deterministic and too expensive for Quest/Steam Deck. Keeping the axis-aligned fake was cheaper but produced visible bite drift on angled glass.
Scalability potential: Low still uses head aim only; Middle gains more accurate jaw contact; High/Ultra gain cleaner independent mandible/tentacle wrap around a rotated hull.
Hardware Impact: Estimated i3/MX350 cost is a handful of dot/cross operations only on the non-low-tier path. High-end hardware spends the same deterministic math to buy better contact staging.

## Decision 15 - Corpse Sink DataVault Eviction
Problem: The touched fauna integration file still had private persistent `NativeArray<T>` scratch buffers for corpse sinking, which violated the local data-sovereignty audit even though it is adjacent to the bite path.
Solution: Move corpse-sink input/output scratch to `GlobalDataVault` handles owned by `SystemID.AnimationFauna`, resolve only for scheduling/completion, and force-complete pending work before clearing handles.
Rejected Alternatives: Leaving these buffers because they are not bite-specific would keep a private native island in a file modified for this agent; disposing vault-owned memory locally would be a lifetime bug.
Scalability potential: Low/Middle/High/Ultra all keep fixed-size corpse-sink scratch. No visual tier change; this is memory governance and leak-risk reduction.
Hardware Impact: No measured frame-time saving. Static expectation is lower leak/stale-view risk on memory-constrained Quest/Android and Steam Deck.

## Decision 16 - Loop 7 Compile Wall
Problem: After the OBB/cylinder and DataVault-handle pass, repository compilation still exits 1.
Solution: Rerun `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /clp:ErrorsOnly`, record the 243-error external wall, and keep this slice marked dependency-blocked instead of claiming `VERIFIED MASTER GRADE`.
Rejected Alternatives: Patching `HectonUnderwaterVisuals`, `SargassumMicroFaunaBoids`, `RepairTool`, or `ToolDurabilitySystem` would cross the assigned animation/IK boundary and interfere with other agents.
Scalability potential: Bite IK Low/Middle/High/Ultra paths remain implemented; full integration proof waits on the external compile wall.
Hardware Impact: No runtime impact from the compile wall. The implemented domain path remains bounded and zero-GC by static scan evidence.

## Decision 17 - Target Basis Degeneracy Guard
Problem: Authored target right/up/forward vectors can be invalid or nearly parallel, which would distort the oriented hull approximation and tentacle cylinder wrap.
Solution: Re-orthogonalize target axes in Burst with finite guards and a perpendicular fallback before any OBB closest-point or high-tier wrap solve.
Rejected Alternatives: Trusting content-authored axes would be fragile on imported submarine/player bounds; using physics or mesh queries to recover the basis would violate the deterministic no-overlap mandate.
Scalability potential: Low ignores the basis in head-fake mode; Middle/High/Ultra get stable angled hull contact even with imperfect target metadata.
Hardware Impact: Static cost is a few dot/cross/rsqrt operations on non-low-tier solves. No profiler capture was available; no measured microsecond claim is made.

## Decision 18 - Loop 8 Compile Wall Narrowed
Problem: After the basis guard, the repository still does not build, but the emitted wall changed from many external failures to one external namespace failure.
Solution: Rerun build and record the current blocker: `Assets/_Project/Scripts/Ecosystem/EcosystemRuntimeInstaller.cs(1,18)` cannot resolve `Hecton8.AI.Ecosystem`.
Rejected Alternatives: Editing ecosystem namespace structure from the animation/IK agent would violate domain ownership. Claiming `FINAL_VALIDATION` as passed would be false.
Scalability potential: Bite IK tiering remains unchanged; validation is still blocked outside this domain.
Hardware Impact: No runtime impact from the external compile wall.

## Decision 19 - Predator Lunge Physics Query Removal
Problem: `FaunaBrain` still used `Physics.CapsuleCastNonAlloc` during the predator lunge presentation, allowing a Unity physics query to influence attack contact.
Solution: Capture the target bounds and basis at telegraph time, then run a deterministic swept-sphere against an oriented box using scalar math.
Rejected Alternatives: Keeping CapsuleCast was too platform-variable and violated the no-physics-overlap bite audit. Mesh casts were rejected as slower and less predictable. Ignoring lunge collision entirely would reintroduce attack clipping.
Scalability potential: Low uses the same cheap swept-OBB fake; Middle/High/Ultra spend saved physics-query cost on IK contact, debris, haptics, dents, and acoustic signals.
Hardware Impact: Static estimate is 40-120 us saved on active lunge frames on i3/MX350 by removing the physics cast and RaycastHit scan. No profiler capture was available.

## Decision 20 - Typed Lane Replacement For Legacy Fauna Emits
Problem: The touched fauna attack path still emitted through `PhysicsEventBus` for EMP and mimic acoustic behavior.
Solution: Route mimic pings through existing `AcousticPingSignal` and EMP attack through existing typed `CombatDamageSignal` with `DamageTypeMask.Emp`.
Rejected Alternatives: Inventing a duplicate acoustic lane would fragment signal consumers; keeping `PhysicsEventBus` in this touched path failed the neural-connectivity audit.
Scalability potential: Low/Middle/High/Ultra use fixed typed payloads and `GlobalSignals.Publish`; no per-tier allocation or listener enumeration.
Hardware Impact: Static estimate is a small dispatch saving on emission frames by avoiding legacy listener fanout. No measured microsecond claim is made.

## Decision 21 - Final Build Green
Problem: Previous validation was blocked by external compile errors, so `FINAL_VALIDATION` could not honestly be claimed.
Solution: After the lunge/query purge and signal purge, rerun `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /clp:ErrorsOnly`; it exits 0 with 0 warnings and 0 errors.
Rejected Alternatives: Reporting earlier blocked state as final would be stale; skipping rebuild after EventBus/physics edits would be unverifiable.
Scalability potential: Build-green state confirms the current Low/Middle/High/Ultra bite implementation is at least C#-valid in the shared project.
Hardware Impact: No direct runtime impact from compilation. Runtime impact is covered by Decisions 19 and 20.

## Decision 22 - Adjacent Tentacle ABI Pack
Problem: The broader fauna IK ARM64 audit found `FaunaTentacleConstrainedIkChain` and `FaunaTentacleJointPose` using explicit field offsets without `Pack = 1`, leaving native/Burst layout dependent on platform defaults even though the payloads are 32-byte job packets.
Solution: Add `Pack = 1` to both explicit `StructLayout` declarations and re-run the struct layout scan over owned bite IK plus the adjacent tentacle IK file.
Rejected Alternatives: Leaving the file untouched because it is outside `Assets/_Project/Scripts/Animation/Fauna/` would preserve a concrete Quest/Android ABI risk in a fauna IK payload. Moving the entire tentacle solver to this agent's ownership was rejected as domain drift.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged; this is deterministic binary layout hardening for mobile/Quest and desktop Burst consistency.
Hardware Impact: No measured frame-time change. Static expectation is zero runtime cost and lower platform-layout failure risk.

## Decision 23 - Leviathan Tentacle Memory Owner
Problem: `LeviathanTentacleVerletSolver` allocated and released native tentacle solver buffers through `H8Memory` with `SystemID.External`, hiding an animation/fauna IK allocation under the external bucket.
Solution: Keep the existing cold allocation/deferred release pattern intact but tag both calls with `SystemID.AnimationFauna` so the memory sentinel attributes the buffers to the correct owner.
Rejected Alternatives: Leaving `SystemID.External` violates the memory sentinel audit. A full DataVault migration is the right larger cleanup but was not done in this step because it requires adding buffer IDs and replacing all persistent NativeArray fields in a broad adjacent solver while the repo has an external compile wall.
Scalability potential: Low/Middle/High/Ultra solver behavior is unchanged; owner telemetry now remains useful on Quest/Android, Steam Deck, and PC when tracking leaks or pressure.
Hardware Impact: No measured frame-time change. Static expectation is zero runtime cost and better leak attribution on memory-constrained devices.

## Decision 24 - Procedural Crab ABI Pack Sweep
Problem: The adjacent procedural crab IK runtime carried sequential data, telemetry, and Burst job packet structs without explicit `Pack = 1`, so the ARM64/Quest layout audit still had platform-default layout ambiguity outside the bite kernel.
Solution: Add `Pack = 1` to every `StructLayout(LayoutKind.Sequential)` declaration in `ProceduralCrabLegIKRuntime.cs` and re-run the no-missing-pack scan over the adjacent fauna IK files and owned bite IK folder.
Rejected Alternatives: Only fixing bite-specific packets would leave obvious adjacent IK ABI debt. Rewriting crab IK native ownership was rejected for this step because it is a broader DataVault migration and the repository currently has an external compile wall.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged; this is portable native metadata hardening for job payloads and telemetry.
Hardware Impact: No measured frame-time change. Static expectation is zero runtime cost and lower layout mismatch risk on Quest/Android and IL2CPP.

## Decision 25 - Leviathan Tentacle DataVault Eviction
Problem: `LeviathanTentacleVerletSolver` still owned persistent private `NativeArray<T>` fields for positions, previous positions, radii, GPU matrices, stretch fractions, constraint scratch, root/target AUP caches, state bits, and black-box telemetry after the earlier owner-ID-only correction.
Solution: Add dedicated `LeviathanTentacle*` `BufferID` values, replace private persistent arrays with `VaultBufferHandle<T>` fields, and resolve short-lived `NativeArray<T>` views from `GlobalDataVault` only at seeding, Burst scheduling, origin-shift rebase, upload, contact damage, and telemetry dump boundaries.
Rejected Alternatives: Keeping `H8Memory.Allocate` with `SystemID.AnimationFauna` was better than `External` but still a private data island. Reusing `LeviathanBoneMatrices` or bite buffers would corrupt ownership and conflate tentacle Verlet state with spine or jaw state. Calling `ReleaseOwnerBuffers(SystemID.AnimationFauna)` on teardown was rejected because the owner bucket is shared by adjacent animation/fauna systems.
Scalability potential: Low keeps one-iteration cheap tentacle motion and fixed-size vault buffers; Middle/High/Ultra use the same vault-owned streams while spending CPU/GPU budget on richer matrix/radius upload, suction pulse, flow-reactive motion, and high-tier AUP contact direction.
Hardware Impact: No profiler capture was available, so 0 us measured. Static expectation is lower leak/stale-view risk on Quest/Android and Steam Deck, no new per-frame allocation, and no claimed frame-time saving beyond removing private native lifetime management.

## Decision 26 - Procedural Crab DataVault Eviction
Problem: `ProceduralCrabLegIKRuntime` still owned persistent private `NativeArray<T>` fields for crab entity state, foot positions, target feet, step scheduler state, raycast command/result buffers, low-tier raycast masks, body pose upload data, solved joint matrices, and black-box telemetry.
Solution: Add dedicated `ProceduralCrab*` `BufferID` values, replace the private arrays with `VaultBufferHandle<T>` fields, and resolve short-lived views only at entity registration, pose updates, Burst scheduling, origin-shift rebase, indirect GPU upload, telemetry write, and crash dump boundaries.
Rejected Alternatives: Keeping local `new NativeArray<T>` allocations plus `NativeMemorySentinel` registrations would preserve a private data island. Reusing the dispatcher raycast buffers was rejected because crab ground probes have a different lifetime and would create cross-system aliasing. Calling `ReleaseOwnerBuffers(SystemID.AnimationFauna)` on teardown was rejected because the owner bucket is shared by bite, spine, tentacle, and adjacent fauna animation systems.
Scalability potential: Low/MX350 keeps the existing two-leg raycast budget and cheap analytical leg fake; Middle/High/Ultra keep all-leg probes and richer body tilt/joint matrix upload without any new private memory ownership.
Hardware Impact: No profiler capture was available, so 0 us measured. Static expectation is lower leak/stale-view risk on Quest/Android and Steam Deck, no per-frame allocation, and no claimed CPU speedup beyond removing private native lifetime management.

## Decision 27 - Tier1 Fauna Proxy Pack Correction
Problem: The broad ARM64/Quest audit found `FaunaTier1LodProxyEntry` using `StructLayout(LayoutKind.Sequential, Pack = 4, Size = 64)`, leaving an adjacent low-tier fauna proxy with nonstandard packing while the rest of the audited IK payloads use `Pack = 1`.
Solution: Change the declaration to `Pack = 1` and retain explicit `Size = 64`; field order and runtime behavior do not change.
Rejected Alternatives: Leaving `Pack = 4` because the file is adjacent rather than authoritative would preserve a platform-layout exception in the low-tier fauna visual path. Reordering fields was rejected because the explicit size already keeps the packet stable.
Scalability potential: Low/MX350 keeps the cheap proxy path with deterministic ABI metadata; Middle/High/Ultra behavior is unchanged and still spends detail budget in the richer IK/tentacle paths.
Hardware Impact: 0 us measured. Static expectation is zero runtime cost and lower IL2CPP/ARM64 layout risk on Quest/Android and Steam Deck.

## Decision 28 - Dead Predator Memory Deletion
Problem: `FaunaBrain.Compatibility.cs` still contained an unused `PredatorMemory` struct with a private persistent `NativeArray<float4>`, local allocation, and sentinel registration path.
Solution: Verify there are no in-repo references, then delete the dead struct instead of moving unused memory to the vault. Preserve `using System` because the file still uses `[Flags]`.
Rejected Alternatives: DataVault-migrating a dead compatibility type would keep a public API and allocation surface nobody calls. Leaving it in place would keep a dormant private native island in a fauna file already touched by this slice.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged; the win is removing unused memory ownership and future leak surface from adjacent fauna cognition compatibility.
Hardware Impact: 0 us measured. Static expectation is lower memory-governance risk on Quest/Android and Steam Deck, with no claimed frame-time improvement.

## Decision 29 - Loop 16 External Compile Wall
Problem: After the dead-code deletion was corrected for the missing `System` using, repository compilation still exits 1.
Solution: Rerun the serialized build and record the current external blocker: `Assets/_Project/Scripts/AcousticZoneController.cs(3175,17)` cannot resolve `Type`.
Rejected Alternatives: Editing audio/acoustic ownership from the animation IK agent would violate the domain boundary. Claiming build green would be false.
Scalability potential: Bite IK and adjacent fauna IK scalability remain unchanged; full integration proof waits on the external compile wall.
Hardware Impact: No runtime impact from the compile wall.

## Decision 30 - Build Green Revalidation After Concurrent Edits
Problem: The latest compile wall was moving under concurrent external edits; `AcousticZoneController` and `HectonSurvivalSystem` errors were stale against the live worktree by the time they were inspected.
Solution: Inspect the live files, avoid overwriting other agents' corrections, rerun a serialized build with explicit exit capture, and record the current result: build exits 0 with 0 warnings and 0 errors.
Rejected Alternatives: Reverting or overwriting external edits would risk destroying other agents' work. Keeping the stale blocked status would be false once the live build passed.
Scalability potential: Low/Middle/High/Ultra bite IK paths now have objective C# build proof again; no tier behavior changed.
Hardware Impact: 0 us measured. Build revalidation has no runtime impact.

## Decision 31 - Leviathan Shader Metal Audit
Problem: The multiplatform inquisition requires checking the owned visual surface for Metal/Mac hazards, especially compute thread-group and DirectX-only shortcuts.
Solution: Scan `Hecton_LeviathanTentacleIndirect.shader` and `Hecton_LeviathanOrganic.shader` for compute kernels, `numthreads`, RW resources, D3D-only macros, derivative intrinsics, `tex2Dlod`, and renderer restrictions; no matches were found.
Rejected Alternatives: Treating shader compliance as irrelevant to animation IK would miss the Leviathan tentacle/jaw visual upload surface. Editing shaders without a detected violation was rejected as churn.
Scalability potential: Low keeps CPU-side cheap IK/proxy paths; High/Ultra retain existing Leviathan visual surfaces without introducing Metal-incompatible overkill.
Hardware Impact: 0 us measured. Static expectation is lower platform risk only; no frame-time claim.

## Decision 32 - Fauna Simulation DataVault Eviction
Problem: The broad fauna inquisition found `FaunaSimulationMemory` still owning persistent local `NativeArray<T>` buffers and a `NativeQueue<int>` for residency pool slots, velocities, flags, and free slots.
Solution: Add `FaunaSimulation*` `BufferID` values, store only `VaultBufferHandle<T>` metadata in `FaunaSimulationMemory`, and replace the free-slot queue with a fixed-capacity DataVault-backed stack. `FaunaDirector` mutation sites resolve local `NativeArray<T>` views before index writes so the existing residency behavior stays intact.
Rejected Alternatives: Keeping the `NativeQueue<int>` with a sentinel label would still be a private native island. Reusing bite, tentacle, or procedural crab buffers would corrupt ownership. Moving the whole `FaunaDirector` residency system into this agent's domain was rejected as broad AI/gameplay ownership drift.
Scalability potential: Low/MX350 keeps cheap dehydrated fauna data-only motion without private memory ownership; Middle/High/Ultra keep the same residency fidelity and can spend saved governance risk on richer visible IK/VFX instead of allocator churn. This is memory sovereignty, not a new visual feature.
Hardware Impact: 0 us measured. Static expectation is lower leak/stale-view risk on Quest/Android and Steam Deck, no per-frame allocation, and no claimed CPU speedup beyond removing private native lifetime management.

## Decision 33 - Loop 18 External Compile Wall
Problem: Compile validation cannot currently reach a clean C# proof after the fauna simulation memory patch.
Solution: Run `dotnet restore Hecton8.Core.csproj` only because `Temp/obj/Hecton8.Core/project.assets.json` was missing, then run one serialized `dotnet build --no-restore`; it exits 1 on missing external source `Assets/_Project/Scripts/Gameplay/WaterTransitionHandler.cs`.
Rejected Alternatives: Rebuilding repeatedly would violate the user's instruction and would not fix a missing gameplay source. Editing the `.csproj` or recreating `WaterTransitionHandler.cs` from the animation IK slice would cross ownership and risk erasing another agent's work.
Scalability potential: Bite IK Low/Middle/High/Ultra behavior is unchanged. Fauna simulation DataVault ownership is in place, but full integration proof waits on the external gameplay file reference.
Hardware Impact: No runtime impact from the compile wall.

## Decision 34 - Data-Only Fauna LOD NaN Guard
Problem: The adjacent fauna residency job still ran with `FloatMode.Fast` and wrote dehydrated slot position from unguarded AUP, delta-time, velocity, and distance math.
Solution: Switch `DataOnlyFaunaLodJob` to deterministic Burst mode, reject non-finite player/slot AUP and distance state, zero bad velocity, and only write back finite next positions.
Rejected Alternatives: Trusting dehydrated slot data would allow one bad velocity or origin value to poison resident AUP state on mobile. Routing the pass through Unity physics was rejected as slower and non-deterministic.
Scalability potential: Low/MX350 keeps the same cheap data-only movement fake; Middle/High/Ultra get the same deterministic residency path while visible IK/VFX can spend frame budget elsewhere.
Hardware Impact: No profiler capture was produced, so 0 us measured. Static expectation is lower crash risk on Quest/Android/Steam Deck at the cost of a few scalar finite checks on the low-frequency resident LOD cadence.

## Decision 35 - Vault Free-Slot Reset Collapse
Problem: `FaunaSimulationFreeSlotStack.Reset()` refilled the fixed free-slot stack by calling `Enqueue()` once per slot, resolving the same DataVault handle on every iteration.
Solution: Resolve the vault-backed slot buffer once, then fill the stack directly up to the resolved capacity.
Rejected Alternatives: Keeping per-slot handle resolution was unnecessary cold-path debt. Reintroducing a local `NativeQueue<int>` was rejected because Loop 18 already evicted free-slot ownership to the vault.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged; cold reset and emergency reset paths are cleaner and still fixed-capacity.
Hardware Impact: No measured microseconds. Static estimate is reduced cold reset overhead proportional to resident capacity by removing repeated vault handle resolution, with no per-frame allocation.

## Decision 36 - Loop 19 Compile Wall
Problem: After the resident LOD stability patch, repository compilation still cannot prove green.
Solution: Run one meaningful serialized build after compile-impacting edits; it exits 1 with 4 errors in currently dirty Core/VFX files: missing `HectonSignalLaneContract` context, `AudioEvent` not satisfying `ISignal`, and ambiguous `CameraJuiceImpactSignal`.
Rejected Alternatives: Rebuilding repeatedly would waste time and violate the user's instruction. Editing dirty Core/VFX files from the animation IK slice would cross ownership and risk overwriting active work by other agents.
Scalability potential: Bite IK and fauna resident LOD scalability remain unchanged; final build proof waits on the external signal/VFX compile wall.
Hardware Impact: No runtime impact from the compile wall.
