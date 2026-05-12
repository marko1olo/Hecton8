# Rationale_ANIMATION_IK

Status: PENDING VERIFICATION

## Mandates Loaded

- `ANIM_Contextual_Physical_IK.txt`
- `ANIM_IK_FABRIK_GroundSnapping_Procedural.txt`
- `REND_GPU_Driven_Animation_VAT.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `MATH_Rsqrt_i3_SIMD.txt`
- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`

## Initial Boundary Decision

Problem: `CURRENT_BATCH.md` is absent; task source exists as `Docs/Tasks/CURRENT_BATCH.txt`.

Solution: Extracted the `ANIMATION_IK` XML block from `CURRENT_BATCH.txt` using a raw PowerShell regex and ignored neighboring prompts.

Rejected Alternatives: Basic MCP/document read was rejected because the prompt explicitly requires CLI extraction to avoid truncation. Reusing prior `HECTON8_MOTION_IK` logs was rejected because the active ID is `ANIMATION_IK`.

Scalability potential: Low/Middle/High/Ultra unaffected by this document-only decision.

Hardware Impact: 0 us runtime impact on i3/MX350.

## Scalability Pillar Baseline

Problem: Animation tasks span CPU IK, VAT shader animation, and physics hardening; a balanced middle tier would violate the prompt.

Solution: Maintain tier split during implementation: Low uses 10 Hz IK/VAT interpolation, Middle uses limited CPU IK, High uses 30 Hz predator IK, Ultra spends saved CPU on richer visual deformation/VAT blend detail.

Rejected Alternatives: Always-on full IK was rejected because MX350 target budget makes it non-defensible. Shader-only for near player-critical hands was rejected because gameplay contact needs local correctness.

Scalability potential: Low = VAT interpolation and sparse IK; Middle = limited batched IK; High = 30 Hz predator IK; Ultra = overkill visual bloat/VAT/detail while keeping authority bounded.

Hardware Impact: Expected low-end gain is pending source discovery and profiler proof.

## Loop 1 Decisions

Problem: Zero-mass singularity could produce Infinity if hydrodynamic added-mass acceleration divides by `mass + addedMass` when mass is zero or non-finite.

Solution: Kept the existing guarded implementation in `Assets/_Project/Scripts/Gameplay/HectonPlayerMotor.cs`: finite mass is sanitized, body mass is clamped to `0.001f`, added mass is computed statelessly, and `safeMass` is clamped before `math.rcp`.

Rejected Alternatives: Stateful `HydrodynamicAddedMassVelocity` history was rejected because it violates prompt task 3 and gives corrupted mass bugs memory. Raw `force / mass` was rejected because `mass == 0` remains a physics singularity.

Scalability potential: Low/Middle/High/Ultra all use the same safe scalar; visual budget is spent elsewhere, not on exact hydrodynamic history.

Hardware Impact: Estimated 0.8 us per call for guard math; avoids catastrophic INF/NaN physics recovery cost on i3/MX350.

Problem: Predator spine IK must not run full-rate at distance or on MX350.

Solution: Kept `ProceduralLeviathanSpineIK` adaptive cadence: `GlobalRegistry.ScalabilityTier` plus 20m squared-distance gate selects 30 Hz or 10 Hz solve cadence.

Rejected Alternatives: 60 Hz full predator IK was rejected because distant predator belief is preserved by VAT/shader presentation. Disabling near IK entirely was rejected because strikes and head look remain player-facing presentation.

Scalability potential: Low = 10 Hz IK plus VAT interpolation; Middle = default gated IK; High = 30 Hz predator IK; Ultra can spend saved cycles on richer shader deformation.

Hardware Impact: Estimated 35 us saved per skipped predator spine solve on low-end silicon; profiler proof pending.

Problem: Tail motion and FABRIK/pole correction could burn cycles on exact sine/sqrt paths.

Solution: Verified triangle-wave tail paths in `ProceduralLeviathanSpineIK` and `BoidFishInstanced.shader`; verified pole projection uses rsqrt-backed normalization in `ContextualPhysicalIkRig`.

Rejected Alternatives: `math.sin`, `FromToRotation`, and exact `sqrt` pole distances were rejected because these are presentation-side animation, not authoritative physics.

Scalability potential: Low keeps cheap triangle wave; High/Ultra can use saved ALU for VAT frame blending, hit flash, bloat, and richer material response.

Hardware Impact: Estimated 2-5 us CPU equivalent per active solve path plus lower GPU ALU in tail shader; runtime measurement pending.

## Loop 2 Decisions

Problem: Corpse death motion needed readable drift without adding a skeletal corpse sim.

Solution: Kept `FaunaBrain` death spiral path: instance-stable hashes seed X/Z corkscrew phases, `TrianglePulse01` drives lateral drift, and the Rigidbody is moved with bounded descent plus angular velocity.

Rejected Alternatives: Per-bone corpse simulation and sine-based roll were rejected because death presentation does not need authoritative body physics.

Scalability potential: Low uses rigidbody corpse drift only; Middle keeps shader bloat/fade; High and Ultra can layer richer material response over the same deterministic drift.

Hardware Impact: Estimated 8 us saved per corpse versus a multi-bone corpse solve on i3/MX350.

Problem: Swim animation and damage response needed motion continuity at distance without CPU bones.

Solution: Kept VAT frame A/B sampling and blend in `BoidFishInstanced.shader`; kept `_HitFlash`/bloat shader work in `Hecton_LeviathanOrganic.shader`; kept `_BreathingPhase` dominant-axis chest offset in `SuitVisor.shader`.

Rejected Alternatives: Animator transitions for hit flash, CPU chest scale bones, and CPU far-swim skeletons were rejected because they move presentation work off the GPU and add hot-path state.

Scalability potential: Low uses sparse CPU IK plus VAT interpolation; Middle keeps shader bloat; High/Ultra spend saved CPU on richer material and emission response.

Hardware Impact: Estimated 20-60 us CPU saved per visible distant school/predator group by avoiding skeleton evaluation; shader cost remains bounded ALU/texture work.

Problem: Landing weight lean used height delta only and did not explicitly project slope normal into the player frame as required.

Solution: Added `ResolveSlopeLeanRadians` in `ContextualPhysicalIkRuntime`: foot normals are blended, projected onto root forward/right with `math.project`, and converted into bounded COM pitch/roll. `ContextualPhysicalIkRig` now feeds the same lean into the spine chain with a 0.35 share so slope response affects the visible torso/spine, not just pelvis offset.

Rejected Alternatives: `Quaternion.FromToRotation` to align the whole body to ground normal was rejected because it is too blunt, not controllable, and more expensive than projected scalar lean. A new ground raycast was rejected because existing batched foot probes already own slope contact.

Scalability potential: Low and Middle reuse existing contact targets; High and Ultra can raise visual overkill in shader/material layers without increasing slope probe count.

Hardware Impact: Estimated 0.6 us per active IK entity for two `math.project` calls; avoids an extra raycast and exact quaternion alignment cost.

Problem: 0 HP handoff must keep last motion believable without keeping VAT mesh and skeleton alive together.

Solution: Kept `FaunaSimplifiedRagdollHandoff.BeginHandoff`: VAT renderer is disabled, last vertex/rigidbody velocity is projected into `_initialVelocity`, and four rigidbody joints receive deterministic linear/angular velocity.

Rejected Alternatives: Full ragdoll hierarchy and blend-tree death transitions were rejected because corpse readability needs four-joint silhouette motion, not a full animation graph.

Scalability potential: Low gets four-joint physics only; Middle keeps corpse bloat fade; High/Ultra add material overkill on top of the same cheap handoff.

Hardware Impact: Estimated 50+ us saved per corpse versus full ragdoll activation on i3/MX350.

Problem: Loop 2 compile initially failed while shared files were being edited by parallel agents.

Solution: Re-ran the same controlled `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` after source settled; build passed with 0 warnings and 0 errors.

Rejected Alternatives: Editing `SubmarineFluidDynamics` or unrelated movement changes was rejected after the next build proved the failures were transient external workspace churn.

Scalability potential: No runtime behavior change; this is verification hygiene.

Hardware Impact: 0 us runtime impact.

## Batch Source Continuity

Problem: The original `Docs/Tasks/CURRENT_BATCH.txt` disappeared during parallel work and `Docs/Tasks/CURRENT_BATCH.md` appeared with the same `ANIMATION_IK` prompt.

Solution: Re-extracted the `ANIMATION_IK` block from `CURRENT_BATCH.md` using the required raw CLI regex and continued from disk state.

Rejected Alternatives: Continuing from memory only was rejected because Anti-Amnesia requires the master prompt on disk every 3 tasks when available.

Scalability potential: No runtime behavior change.

Hardware Impact: 0 us runtime impact.

## Loop 3 Decisions

Problem: Squid/tentacle presentation needed target seeking without a full FABRIK hierarchy or runtime allocations.

Solution: Kept `FaunaTentacleConstrainedIkJob` as a Burst `IJobParallelFor`: each chain owns four joint poses, tip targets are `AbsoluteUniversePosition`, conversion uses a reference AUP, and the S-curve is a two-control-point side offset with `rsqrt` length approximation.

Rejected Alternatives: Managed Transform chains and per-frame `List` scratch were rejected because they allocate and read Unity objects inside the IK solve. Full iterative FABRIK was rejected for this four-point predator silhouette task.

Scalability potential: Low evaluates fewer chains or lower cadence; Middle keeps 4-point chains; High/Ultra can spend budget on shader suckers/bloat over the same pose data.

Hardware Impact: Estimated 12-30 us saved per tentacle group versus managed Transform FABRIK on i3/MX350.

Problem: Damage reaction needed instant readability without animator transition churn.

Solution: Kept `_HitFlash` as the damage signal in `FaunaBrain`, with shader-side `smoothstep`, normal bloat, and emission in `Hecton_LeviathanOrganic.shader`.

Rejected Alternatives: Animator hit states and material instantiation per hit were rejected because the task requires shader property bloat and no animator transition path for damage flash.

Scalability potential: Low uses the same single scalar; High/Ultra increase material richness without CPU animation cost.

Hardware Impact: Estimated 4 us CPU saved per hit by skipping animator state change and transition evaluation.

Problem: Animation event hooks can allocate or dispatch string messages at unpredictable times.

Solution: Verified no `AnimationEvent`, `SendMessage`, or string animator parameter calls exist in `FaunaBrain`/Fauna sources. `FaunaBrain.Tick` advances attack decisions through distance/phase/time checks (`TryAdvanceAttackTelegraph`, AUP distance gates, and procedural intent updates).

Rejected Alternatives: Unity AnimationEvents and string-based animator calls were rejected because they are opaque, allocation-prone, and not deterministic enough for fauna AI.

Scalability potential: Low can cold-tick fauna cognition; High/Ultra can keep richer procedural presentation without adding event dispatch.

Hardware Impact: Estimated 2-5 us saved per active fauna event window and avoids GC spikes from string dispatch.

Problem: Footstep clip and pitch variation needed deterministic randomness without `UnityEngine.Random` or exact speed magnitude.

Solution: Kept `PlayerFootstepAudio` LCG (`1664525u + 1013904223u`) for clip/pitch selection and `ApproximatePlanarMagnitude(max + 0.375 * min)` for speed scaling. Surface hits are reused from player movement instead of a mandatory fresh raycast.

Rejected Alternatives: `UnityEngine.Random.Range`, `Vector3.magnitude`, and always-raycast footsteps were rejected because they are nondeterministic or wasteful.

Scalability potential: Low/Middle use reused movement probe data; High/Ultra can add richer surface audio sets without changing RNG determinism.

Hardware Impact: Estimated 3 us saved per footstep event and one raycast avoided when cached movement hit is valid.

Problem: Suit breathing shader globals can become redundant driver calls.

Solution: Kept `PlayerSwimPresentationController.PublishBreathingPhase`: phase is clamped, quantized to a signed byte bucket, and `Shader.SetGlobalFloat(_BreathingPhase)` is skipped when unchanged.

Rejected Alternatives: Publishing a float every tick and driving a chest bone were rejected because the shader only needs a coarse breathing scalar.

Scalability potential: Low uses quantized global only; High/Ultra can increase suit material detail while retaining the same one-scalar driver.

Hardware Impact: Estimated 3 us saved on unchanged frames by skipping redundant global shader writes.

## Loop 3 Compile Blocker

Problem: Loop 3 compile failed in `Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs` with missing methods: `TryUnregisterWreckSlowTick`, `ProcessNearFieldDebris`, `ProcessArtifactDiscovery`, `UpdateDebrisGravityStateless`, `ValidateBlackBoxState`, `RefreshLootRecords`, `PrepareWreckWorldState`, and `ConfigureIntegrityProxy`.

Solution: Treated it as an external world-wreckage dependency. Searched the file and confirmed the identifiers are call sites only at the time of failure. Continued within the Animation/IK domain.

Rejected Alternatives: Editing `ProceduralWreckGenerator.cs` was rejected because it is outside this assignment's domain and the missing methods are a large world-generation implementation, not a local animation compile fix.

Scalability potential: No Animation/IK runtime behavior change.

Hardware Impact: 0 us runtime impact from this decision.

## Loop 4 Decisions

Problem: IK target distance math must avoid square roots and exact `distance` calls.

Solution: Verified checked IK targets use squared distance or squared-length plus `rsqrt`: `ContextualPhysicalIkRuntime` and `ProceduralLeviathanSpineIK` use `math.lengthsq`; `FaunaTentacleConstrainedIk` uses `targetDistanceSq` and `math.rsqrt`.

Rejected Alternatives: `math.distance`, `Vector3.Distance`, and `.magnitude` were rejected in hot IK target checks because squared comparison is sufficient.

Scalability potential: Low/Middle keep the same cheap math; High/Ultra can spend saved scalar cost on shader detail.

Hardware Impact: Estimated 1-3 us saved per IK batch on i3/MX350 by avoiding sqrt paths.

Problem: Hot IK evaluation must not allocate managed scratch.

Solution: Verified no `foreach` exists in checked hot IK files. The only `new List` hits in `ProceduralLeviathanSpineIK` are cold reusable scratch fields allocated at component lifetime, not inside Tick/job loops.

Rejected Alternatives: Per-frame list construction or LINQ enumeration were rejected because both allocate or hide iteration cost.

Scalability potential: Low avoids GC stalls; High/Ultra can scale visible IK count without allocation spikes.

Hardware Impact: Estimated GC spike avoidance, with 0 B/frame expected for checked IK loops.

Problem: IK state flags must avoid branchy state resolution where data selection is enough.

Solution: Verified state-driven paths use `math.select`: `FaunaTentacleConstrainedIk` resolves `TipAnchoredMask`, bend sign, authored length fallback, and dominant side with `math.select`; contextual IK uses `math.select` for enable masks and slope normal validity.

Rejected Alternatives: State-machine `if/else` blocks for per-chain state were rejected because they fragment SIMD/Burst hot loops.

Scalability potential: Low keeps branch-light 4-point solves; High/Ultra can raise chain count without branch divergence dominating.

Hardware Impact: Estimated 1 us saved per tentacle group from branch-light state selection.

Problem: The predator spine solve still needed explicit native position input to satisfy the no-`Transform.position` hot-loop requirement.

Solution: Converted `ProceduralLeviathanSpineIK.SolveSpineJob` from `IJobParallelForTransform` to `IJobParallelFor`, added `_vertebraWorldPositions`, snapshots Transform positions once before scheduling, and makes the Burst loop consume `NativeArray<float3>` instead of Transform access.

Rejected Alternatives: Reading `Transform.position` in the job or keeping `TransformAccessArray` just for validity was rejected because Unity transform access is not the desired hot-loop data path.

Scalability potential: Low gets tighter native-only IK loops; High/Ultra can schedule more vertebra chains without TransformAccess overhead in the solve.

Hardware Impact: Estimated 5-12 us saved per active leviathan spine solve on i3/MX350 by removing TransformAccess job overhead from the solve path.

Problem: `FaunaTentacleConstrainedIk.cs` must have Unity metadata and project compilation coverage.

Solution: Verified `Assets/_Project/Scripts/Fauna/FaunaTentacleConstrainedIk.cs.meta` exists and `Hecton8.Core.csproj` includes the file. No compiler diagnostics were emitted for this file in the latest build attempts.

Rejected Alternatives: Relying on Unity auto-import without `.meta` evidence was rejected because the task explicitly requires metadata verification.

Scalability potential: No runtime behavior change.

Hardware Impact: 0 us runtime impact.

Problem: Loop 4 compile remains blocked by the same external world-wreckage dependency.

Solution: Re-ran the controlled build after the native-position patch. The only errors were still the 12 missing method errors in `ProceduralWreckGenerator.cs`; Animation/IK files emitted no diagnostics.

Rejected Alternatives: Implementing the missing wreckage methods was rejected because that file is outside the Animation/IK domain and the missing methods represent a separate world system.

Scalability potential: No Animation/IK runtime behavior change.

Hardware Impact: 0 us runtime impact from this blocker.

## OMEGA POLISH CHANGES

Problem: Final anti-bloat pass needed to prove the Animation/IK changes did not add hidden managed churn, exact math, or TransformAccess hot-loop reads after tasks 1-20 were checked or blocked.

Solution: Parsed `<POLISH_MANDATE id="OMEGA_POLISH">` only after core task closure. Re-scanned the touched Animation/IK files for `foreach`, `string.Format`, `.ToString(`, interpolated string literals, `new List`, exact sqrt/normalize/distance APIs, and whitespace errors. Scoped findings: no foreach/string formatting/exact distance/sqrt offenders in touched hot paths; `new List` hits are only cold reusable scratch fields on `ProceduralLeviathanSpineIK`. `git diff --check` reported CRLF normalization warnings only.

Rejected Alternatives: Broad repo cleanup was rejected because the compile wall is moving through unrelated construction/save/core files owned by other agents. Editing those systems under this prompt would violate the domain boundary and create false ownership.

Scalability potential: Low = 10 Hz predator spine IK, prebuilt NativeArray position snapshots, triangle/VAT presentation doing the work. Middle = limited near-body contextual IK and four-point tentacle chains. High = 30 Hz near predator IK plus shader bloat/VAT blend. Ultra = spend saved CPU on richer material/VAT visual overkill without adding bone simulation or per-frame managed scratch.

Hardware Impact: Low-end i3/MX350 estimate: 5-12 us saved per active leviathan solve by removing TransformAccess from the Burst solve, 0.6 us per active contextual IK entity for projected slope lean instead of extra raycast/quaternion alignment, 12-30 us per tentacle group by keeping constrained IK in Burst NativeArray data, 20-60 us per far visible group by keeping VAT in shader. Exact profiler proof remains pending Unity play-mode verification.

Problem: Need to identify which honest calculations were replaced by cinematic cheats.

Solution: Replaced or retained fake-first presentation paths: hydrodynamic added-mass stays stateless scalar with clamped reciprocal instead of history integration; tail surge and death corkscrew use triangle waves instead of sine or corpse skeleton simulation; far swim uses VAT frame lerp instead of CPU skeletons; exosuit breathing uses dominant-axis vertex offset from `_BreathingPhase` instead of chest bones; damage reaction uses `_HitFlash` shader bloat/emission instead of animator transitions; slope lean uses projected scalar lean instead of whole-body exact normal alignment.

Rejected Alternatives: Standard Unity AnimationEvents, Animator damage states, `Quaternion.FromToRotation`, full ragdoll activation, full tentacle FABRIK, TransformAccess IK jobs, and sine-driven secondary motion were rejected because they buy little immersion for measurable frame-time and GC risk.

Scalability potential: Low and Middle preserve readable silhouettes using sparse native IK plus shader cheats; High and Ultra spend the recovered budget on presentation density, not on uncontrolled simulation.

Hardware Impact: Combined estimated Animation/IK budget recovery is 40-110 us in active mixed scenes before VAT group savings; far-group VAT avoids additional CPU skeleton work. These are estimates until profiler capture.

Problem: Final compile had to be run after polish.

Solution: Ran `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`. The build is blocked by external systems: `ConstructionManager.cs` missing `Hecton8.Physics.SyncTransforms`, `HabitatGraphManager.cs` missing `TransitionHatchMeshState`, and `SaveBinaryPayloadCodec.cs` / `SaveBinaryStorage.cs` save-system errors. The previous external blockers moved between attempts; no diagnostics were emitted for `ContextualPhysicalIkRuntime.cs`, `ContextualPhysicalIkRig.cs`, or `ProceduralLeviathanSpineIK.cs`.

Rejected Alternatives: Fixing Construction/SaveBinary/HabitatGraph from the Animation/IK prompt was rejected because those are separate domains and this workspace is shared with concurrent agents.

Scalability potential: No Animation/IK runtime behavior change.

Hardware Impact: 0 us runtime impact from the compile-wall decision.
