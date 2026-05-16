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
