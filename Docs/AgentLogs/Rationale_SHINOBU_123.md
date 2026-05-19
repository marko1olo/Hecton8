# Rationale_SHINOBU_123

Date: 2026-05-19
Agent: SHINOBU_123
Declared Role: LEVIATHAN_PROCEDURAL_IK_RIGGER
Status: POLISH PASS 3 APPLIED; COMPILE PENDING CPU GATE

## Decision 01: Halt Before Coding

Problem: The user assigned `SHINOBU_123`, but `Docs/Tasks/CURRENT_BATCH.md` does not contain an `<AGENT_PROMPT id="SHINOBU_123">` block or the role text `LEVIATHAN_PROCEDURAL_IK_RIGGER`.

Solution: Stop implementation and record disk evidence. The batch protocol makes the extracted XML block the primary directive; without it, task count is zero and code edits would be fabricated scope.

Rejected Alternatives: Using `SHINOBU_120` or another nearby prompt was rejected because strict parsing requires deletion of neighboring prompts from working context. Inferring tasks from the user's one-sentence description was rejected because it would bypass task count, phase ordering, and exact DOD requirements.

Scalability potential: Not evaluated. No system design may proceed until the authoritative prompt exists. Expected Low/Middle/High/Ultra behavior for IK cannot be recorded from missing source.

Hardware Impact: No runtime code changed. Estimated gain on low-end i3/MX350: 0 us. Estimated risk avoided: unbounded, because a fabricated IK system could conflict with the real fauna/render/vault domains.

## Mandatory Mandate State

Relevant mandates were identified only at category level and not applied to code:

- `ANIM_IK_FABRIK_GroundSnapping_Procedural.txt`
- `ANIM_Contextual_Physical_IK.txt`
- `REND_GPU_Driven_Animation_VAT.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `MATH_AUP_Determinism_Sync.txt`
- `ARCH_Execution_Phases.txt`

No mandate text was used to implement code because implementation is blocked by missing XML authority.

## Decision 02: Reject Chat-Only Ultra Mandate As Task Substitute

Problem: The user repeated the `SHINOBU_123` assignment and supplied an Ultra polish mandate, but the current batch file still lacks the required `<AGENT_PROMPT id="SHINOBU_123">` block. The Ultra mandate references a "20-TASK matrix", an Editor Facade, fallback mock, and exact DTO checks that do not exist for this agent in `CURRENT_BATCH.md`.

Solution: Re-run CLI extraction, read the required binary payload ledger and architecture indexes, then keep implementation blocked. The repeated chat objective can describe intent, but it cannot supply exact task count, phase gates, DTO layout, buffer IDs, or cross-domain boundaries.

Rejected Alternatives: Creating a Leviathan IK system from the one-sentence chat mission was rejected because it would invent architecture and potentially cross the AI/Fauna/Rendering/Vault domains without a route card. Searching generic "Leviathan" hits was rejected as authority because they point to tools, lore, and archived evidence, not this batch assignment.

Scalability potential: Not evaluated in code. A valid future design must support continuous GlobalQualityWeight across Low/Middle/High/Ultra: low cadence spline/dear-lie VAT, middle Burst FABRIK at reduced chain iterations, high per-frame chain solve, ultra extra secondary tentacle/tissue matrices. This is a requirement sketch only, not an implemented claim.

Hardware Impact: No runtime code changed. Estimated low-end i3/MX350 gain: 0 us. Estimated avoided cost: one unauthorized C# compile and a likely merge conflict in occupied AI/render/memory domains.

## Decision 03: Reactivate After XML Prompt Appeared

Problem: `CURRENT_BATCH.md` now contains `<AGENT_PROMPT id="SHINOBU_123">` with 20 tasks after the earlier missing-prompt state.

Solution: Treat the disk XML as new authority and preserve the old blocker history as stale evidence only. Proceed inside the existing Leviathan/Fauna animation owner instead of creating a duplicate rigger.

Rejected Alternatives: A new `LEVIATHAN_PROCEDURAL_IK_RIGGER` assembly was rejected because there are no local asmdefs and the existing `LeviathanTerrainIkJob` already owns `BufferID.LeviathanBoneMatrices`. A second rigger would split one fact across two owners.

Scalability potential: Low: 8-10 visible spine bones and one nearest SDF lookup. Middle: reduced segment count and 2-5 constraints. High/Ultra: up to 20 matrices and 10 constraint pulls with SDF/height fallback and jaw/tentacle strike blend.

Hardware Impact: Expected i3/MX350 gain versus Animator fallback: 0.15-0.40 ms per apex fauna by deleting Animator graph evaluation and Transform hierarchy writes.

## Decision 04: Replace Raw float4x4 Buffer With 64B Bone DTO

Problem: The XML requires `LeviathanBoneDTO` explicit 64B with `float4x4 LocalToWorld` at offset 0. Existing code wrote raw `float4x4` to Vault.

Solution: Introduce `LeviathanBoneDTO` and retarget the existing `LeviathanBoneMatrices` buffer to that DTO. The binary stride remains 64B, so the GPU buffer ABI stays matrix-compatible.

Rejected Alternatives: Adding a second render buffer was rejected because it would duplicate the bone truth and require a copy job. Keeping raw `float4x4` was rejected because it fails the explicit DTO audit.

Scalability potential: Same render cost, stronger ABI. Low through Ultra all use the same 64B payload, with work scaled by count and iterations.

Hardware Impact: No extra copy. Estimated gain versus a wrapper-copy buffer: 10-30 us per frame for 20 bones because GraphicsBuffer upload remains a single memcpy.

## Decision 05: Continuous Quality Instead Of Tier Branches

Problem: The previous solver used `IsLowTier` and hard segment/iteration gates.

Solution: Feed `HomeostasisBrain.GlobalQualityWeight` into `LeviathanTerrainIkJob`; segment count and iteration count are polynomial-lerped. SDF sampling collapses to nearest below 0.3 and trilinear above it.

Rejected Alternatives: Keeping low/high booleans was rejected because thermal throttling must degrade continuously. Using AnimationCurve was rejected because it is managed/editor-facing.

Scalability potential: Low: nearest SDF and one constraint pass. Middle: partial segment budget. High: full segment budget. Ultra: 10-pull solve and procedural strike detail.

Hardware Impact: Expected low-end savings 80-220 us from fewer pulls and nearest sampling; high-end spends those cycles on smoother matrix motion.

## Decision 06: Animator Removal In FaunaBrain

Problem: `FaunaBrain` still cached an `Animator`, held a trigger hash, and fired `SetTrigger` for glancing blows.

Solution: Remove the Animator field/hash/calls and route glancing-blow recovery into the procedural strike cleanup path. LOD no longer toggles Animator enabled state.

Rejected Alternatives: Keeping Animator as an emergency fallback was rejected because the task requires total removal for giant creatures and the procedural Vault path already exists.

Scalability potential: Low through Ultra now share the same deterministic procedural path; presentation LOD changes affect Vault/GPU payload, not Animator state.

Hardware Impact: Expected low-end gain 0.05-0.20 ms per active leviathan by eliminating managed Animator updates and trigger dispatch.

## Decision 07: Collider Proxy DTO Instead Of Unity CapsuleCollider Instantiation

Problem: Task 13 requires collision proxy staging without runtime `CapsuleCollider` creation.

Solution: Add `LeviathanCapsuleColliderDTO` as a 64B explicit Vault payload and stage capsule center/axis/radius/half-height from solved bone positions.

Rejected Alternatives: Instantiating or enabling Unity `CapsuleCollider` components was rejected because it creates Transform/Physics main-thread churn. Reusing unrelated tentacle correction buffers was rejected because it hides ownership.

Scalability potential: Collider proxy count follows active segment count. Low devices publish fewer proxies; high devices can publish the full set.

Hardware Impact: Expected low-end gain 50-150 us versus collider component churn during active apex encounters.

## Decision 08: Patch Existing Tentacle Solver Instead Of Parallel Rewrite

Problem: `LeviathanTentacleVerletSolver` remained in the Leviathan appendage domain with `[Pack=1]` telemetry, `NativeArray<float4x4>` matrix buffers, `FloatMode.Fast`, and binary low-tier gates.

Solution: Retarget tentacle matrices to `LeviathanBoneDTO`, convert telemetry to explicit 64B layout, add `[NoAlias]`, switch the job to deterministic synchronous Burst, and drive segment budget, iterations, noise, pulse, and material scalar from `GlobalQualityWeight`.

Rejected Alternatives: Creating a second tentacle solver was rejected because the existing file owns `BufferID.LeviathanTentacle*`. Keeping raw `float4x4` was rejected because it leaves a non-audited matrix ABI beside the new 64B DTO. Keeping Fast Burst was rejected because tentacle grabs can affect combat/damage truth.

Scalability potential: Low: 6 integrated nodes per tentacle plus collapsed triangle-wave visual tail. Middle: partial node budget and 1-2 constraints. High/Ultra: 20 nodes and 3 constraints with suction pulse overdraw.

Hardware Impact: Expected low-end i3/MX350 gain 60-180 us when eight tentacles are active, mainly from skipping 14 integrated nodes per tentacle under low quality.

## Decision 09: Binary Rig Parser With Endian Guard

Problem: `TryHydrateRigDefinitionsBinaryCold()` scanned for `leviathan_rig_definitions.h8bin` but always returned false, making the binary route a fake implementation.

Solution: Add a bounded cold parser for a 16-byte header and 16-byte aligned rig rows, with accepted `H8LR`/`LVRG` magic and `math.reversebytes` endian handling. Parsed data hydrates segment positions, previous positions, 64B matrices, and 16B constraints in Vault. Missing or invalid payloads still fall back to deterministic mock rig.

Rejected Alternatives: Hand-authoring a binary asset was rejected because the payload baker is not authoritative here. Throwing on absent payload was rejected because Task 01 requires isolated CI testing. Parsing strings or JSON was rejected because rig constraints are binary/CSV byte paths.

Scalability potential: Low through Ultra share the same loaded skeleton truth. Continuous quality later controls how many rows are evaluated, not which binary file is selected.

Hardware Impact: Runtime hot-path gain is 0 us because this is boot/cold path. Stability gain is avoiding boot failure when the binary is absent and avoiding divergent mock-vs-binary codepaths.

## Decision 10: Exact Named IK Stage Jobs

Problem: Pass 1 folded multiple XML tasks into `LeviathanTerrainIkJob`, which made the task reconciliation weak even though runtime behavior existed.

Solution: Add concrete deterministic Burst jobs: `MockLeviathanTargetJob`, `ProceduralSpineMotionJob`, `InverseKinematicsFABRIKJob`, `SecondaryMotionSpringJob`, `ComputeFinalBoneMatricesJob`, and `StageCreatureCollidersJob`. They use explicit DTOs, `[NoAlias]`, finite guards, and quality-driven math.

Rejected Alternatives: Renaming the existing composite job was rejected because it would churn the live runtime and increase merge risk. Adding interface arrays was rejected because IL2CPP virtual dispatch blocks Burst devirtualization.

Scalability potential: Low: one iteration, six-node/fake appendage motion, low shader payload. Middle: partial segment and iteration count. High/Ultra: full matrices, full FABRIK iterations, richer collider proxies and shader-fed motion.

Hardware Impact: The named jobs do not add cost unless scheduled; they provide a reusable split pipeline for later dispatcher integration. Expected future savings versus monolith scheduling: 20-60 us by skipping whole stages when quality/culling suppresses them.

## Decision 11: Editor Facade Became Real Tuning Surface

Problem: The tuner window only displayed quality and DTO sizes. Task 17 explicitly required swim frequency, sine amplitude, FABRIK tolerance, and damping sliders.

Solution: Add UI Toolkit sliders and bind them to selected `FaunaKinematicsRuntime` serialized fields via `SerializedObject`. Runtime now passes swim frequency/amplitude into `LeviathanTerrainIkJob`, so those two sliders affect the actual spine wave.

Rejected Alternatives: A custom inspector was rejected because the prompt requested a tuner window. Directly referencing the internal runtime type from the editor assembly was rejected to avoid visibility/asmdef risk.

Scalability potential: Designers can tune one set of scalar inputs that the solver scales continuously through `GlobalQualityWeight`, preserving weak-device and ultra-device behavior without recompiling.

Hardware Impact: Runtime cost of the fields is negligible. Tuning control should reduce iteration waste by allowing lower amplitude/frequency profiles on constrained devices without code changes.

## Decision 12: Build Gate Obeyed

Problem: The code changes are substantial enough to justify compile verification, but the user forbids dotnet build when CPU load exceeds 50% or dotnet/csc is running.

Solution: Checked `dotnet`/`csc` processes and CPU load. No process was listed, but CPU load was `97%`, so no build was launched. Static grep and `git diff --check` were run instead.

Rejected Alternatives: Ignoring the CPU rule was rejected because it is an explicit user constraint. Claiming compile success from static checks was rejected because project rules classify that as false proof.

Scalability potential: Not a runtime design choice. It preserves developer machine responsiveness and avoids compile-wall harm during concurrent agent work.

Hardware Impact: Avoided an expensive build under load. Estimated developer-machine stall avoided: 30-120 seconds on this checkout.

## Decision 13: Keep Build Blocked After Latest Gate

Problem: After the canonical log append, compile verification was still desirable, but the latest machine gate reported CPU `100%` and an active `dotnet` process `36732`.

Solution: Do not start a second build. Preserve static verification only: grep checks, `git diff --check`, status/log evidence, and explicit compile-pending labels.

Rejected Alternatives: Launching `dotnet build` in parallel was rejected because the user explicitly forbids builds when CPU exceeds 50% or dotnet/csc is running. Marking compile as verified was rejected because no compiler was executed.

Scalability potential: Not a runtime feature. This preserves iteration stability during multi-agent work and avoids self-inflicted compile-wall contention.

Hardware Impact: Avoided competing with an active dotnet process at 100% CPU. Estimated avoided developer-machine stall: 30-180 seconds.

## Decision 14: Add Unity Meta For New Script Assets

Problem: `LeviathanProceduralAnimationTunerWindow.cs` and `LeviathanProceduralIkStageJobs.cs` were new Unity script assets without checked-in `.meta` files, which would let Unity generate unstable GUIDs later.

Solution: Add explicit `.meta` files with unique GUIDs and standard `MonoImporter` payloads.

Rejected Alternatives: Relying on Unity auto-generation was rejected because it creates GUID churn and can break references across multi-agent checkouts.

Scalability potential: Not runtime. It protects asset identity and avoids editor import noise.

Hardware Impact: Runtime gain 0 us. Developer-time gain is avoiding avoidable asset reimport/reference churn.

## Decision 15: Current Build Gate Remains CPU-Blocked

Problem: A later gate check found no active dotnet/csc process, but CPU load remained `93%`, still above the explicit 50% ceiling.

Solution: Continue blocking build execution and leave compile status pending. Static checks remain the only valid proof for this pass.

Rejected Alternatives: Running a build after the dotnet process exited was rejected because the CPU gate alone still fails. Waiting indefinitely inside this turn was rejected because it would not change the code state and would compete with ongoing agent work.

Scalability potential: Not runtime. The decision preserves local iteration stability during concurrent batch execution.

Hardware Impact: Avoided adding a Unity/dotnet compile load on a machine already reporting 93% CPU.

## Decision 16: Replace Telemetry Padding With Forensic Lanes

Problem: Task 16 required root AUP, evaluated bones, average FABRIK iterations, and Burst compute time. The 96B terrain telemetry DTO only stored quality and iterations in anonymous padding, which made the black-box dump weak evidence.

Solution: Preserve the 96B ring-buffer ABI and replace the unused padding lanes with named fields: `GlobalQualityWeight` at byte 60, `double3 RootAup` at byte 64, `AverageFabrikIterations` at byte 88, and `BurstSolveMicros` at byte 92. Runtime stamps root AUP from `AbsoluteUniversePosition.ToAbsoluteDouble3()` and patches the latest telemetry entry after job completion with measured schedule-to-completion microseconds.

Rejected Alternatives: Expanding telemetry to 128B was rejected because the existing 300-entry Vault ring and dump stride were already aligned and sufficient. Leaving generic padding was rejected because the dump would not answer the forensic question.

Scalability potential: Low/Middle/High/Ultra all share one 96B telemetry contract; quality weight and iteration count show exactly how much math was shed each frame.

Hardware Impact: Hot job cost is four scalar writes plus one `double3` write per frame. Expected i3/MX350 overhead is below 1 us; forensic gain is eliminating blind crash reports.

## Decision 17: Editor Facade Snapshot Contract

Problem: The tuner exposed sliders and DTO sizes but did not show live generation time and bone count. A reflection-based readout would work in Editor, but it would be architecturally lazy and allocate/box values during inspection.

Solution: Add `LeviathanProceduralTunerSnapshot` and `ILeviathanProceduralTunerSource`. The internal runtime implements the public snapshot interface, and the UI Toolkit window reads active bones, solver microseconds, resolved iterations, and quality through that interface.

Rejected Alternatives: Private-field reflection and `GetComponents` array scans were rejected. A runtime UI panel was rejected because Task 17 requested an Editor facade.

Scalability potential: Designers can see when low quality collapses segment/iteration work and when high/ultra spends budget on smoother IK without recompiling.

Hardware Impact: Runtime hot-path impact is 0 us unless the editor asks for a snapshot. Editor readout avoids reflection boxing churn.

## Decision 18: Exact Gizmo Color Semantics

Problem: Task 19 specified green spine bones, red active IK chains, and blue secondary springs. The previous x-ray used one cyan color, which proved Vault reads but not the requested semantic debug surface.

Solution: Keep the same Vault bone source and assign colors per segment: green for standard spine, red for the head/active IK target chain, blue for tail secondary spring overlay during tail-whip secondary motion.

Rejected Alternatives: Transform traversal was rejected because the rig truth is in Vault matrices. A separate debug GameObject hierarchy was rejected because it would reintroduce managed Transform state for an Animator-replacement system.

Scalability potential: The gizmo reflects the same active segment budget that `GlobalQualityWeight` resolved; low devices show fewer evaluated bones, high/ultra show the full chain.

Hardware Impact: Editor-only. Runtime player cost is 0 us.

## Decision 19: Pass 3 Build Gate Still Fails

Problem: Pass 3 changed C# runtime/editor files and should be compiler-verified, but the explicit build gate forbids `dotnet build` while CPU load exceeds 50% or another compiler process is active.

Solution: Rechecked the gate after static verification. `Get-Process dotnet,csc` produced no process output, but CPU load was `99%`, so no build was launched.

Rejected Alternatives: Launching a build at 99% CPU was rejected because it violates the user's machine-protection rule. Reporting compile success from static checks was rejected because no compiler executed.

Scalability potential: Not runtime. This protects the multi-agent iteration environment from a self-inflicted compile wall.

Hardware Impact: Avoided adding dotnet/Unity compile load to a system already at 99% CPU; estimated developer-machine stall avoided: 30-180 seconds.
