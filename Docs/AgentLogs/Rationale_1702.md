# Rationale 1702

Date: 2026-06-02
Status: SOURCE VERIFIED WITHOUT DOTNET BUILD

## Intake Decision 001: Source Reality Before Implementation

Problem: The assignment names `Assets/_Project/Scripts/AI/Cognition/HectonDirectorAI.cs` and `Assets/_Project/Scripts/AI/Pathfinding/PredatorSteeringJob.cs`, but initial `rg --files` found `HectonDirectorAI.cs` at `Assets/_Project/Scripts/HectonDirectorAI.cs` and no `PredatorSteeringJob.cs`.

Solution: Treat prompt file paths as intended architectural targets, not proven source facts. Locate the actual steering job or equivalent fauna steering surface before editing. This follows AGENTS architecture-first and mandate evidence-first rules.

Rejected Alternatives: Creating a new `PredatorSteeringJob.cs` would invent a dependency and likely break concurrent agents. Blindly moving files would be domain sabotage.

Scalability potential: Low uses the existing steering surface with minimal branchless math. Middle/High/Ultra can add richer harassment/feedback only where the existing pipeline has capacity and owner routes.

Hardware Impact: Avoids introducing a duplicate job and extra schedule cost. Estimated low-end i3/MX350 gain versus duplicate scheduling: 40-120 us avoided per AI phase, depending predator count.

## Intake Decision 002: Mandate Set

Problem: Task spans AI director, Burst steering, native DTO layout, GlobalRegistry, telemetry, and armor LUT.

Solution: Load nine relevant mandates plus X_008 route card. Use them as gates for source changes.

Rejected Alternatives: Loading every `.agents-skills` file would pollute context and slow execution. Loading only AI mandates would miss DataVault, registry, telemetry, and combat constraints.

Scalability potential: Low/Middle/High/Ultra policy must stay continuous through GlobalQualityWeight; no binary quality switch.

Hardware Impact: Mandate selection itself has no runtime cost. It prevents hot-path GC and branch-heavy steering mistakes that would cost 15+ cycles per predator branch miss.

## Implementation Decision 003: Edit Existing Leviathan Steering Surface

Problem: The prompt names `PredatorSteeringJob.cs`, but the scheduled Burst predator steering chain is `PredatorCognitionDomain_Steering.cs` with DataVault-owned parameters, avoidance, kinematics, and a 300-entry telemetry ring.

Solution: Patch the existing steering DTO and jobs. Add director-driven clutch, token, and armor deflection fields into `SteeringParamsDTO` where the active job already consumes per-predator parameters.

Rejected Alternatives: Creating a new pathfinding job would duplicate authority and require a new dispatcher dependency. Routing through managed fauna brains would add branch and component cost in the hot loop.

Scalability potential: Low uses a 40 cm visual deflection and token denial orbit with six whiskers. Middle raises whisker density. High/Ultra use more SDF whiskers and richer camera feedback through the same continuous `GlobalQualityWeight`.

Hardware Impact: Reusing the scheduled chain avoids one extra job schedule and one extra native array walk. Estimated i3/MX350 saving versus duplicate job: 35-90 us per steering phase.

## Implementation Decision 004: Deterministic Burst Over Fast Float Mode

Problem: The prompt requests `FloatMode.Fast`, but loaded mandates require deterministic AI and black-box replay facts for critical steering.

Solution: Keep `FloatMode.Deterministic` on the steering jobs and use branchless `math.select`, bit masks, `rsqrt`, and triangle-wave orbit math for speed.

Rejected Alternatives: `FloatMode.Fast` could alter predator steering decisions across CPUs and weakens crash replay. Real trigonometric orbiting was also rejected.

Scalability potential: Low/Middle/High/Ultra all share one deterministic truth path; quality only changes cadence, whisker count, speed presentation, and optional impact feedback.

Hardware Impact: Deterministic math costs less than a cache-miss recovery and preserves reproducible telemetry. Triangle-wave orbit avoids trig, saving roughly 20-60 cycles per active predator versus `sin/cos`.

## Implementation Decision 005: Armor LUT Bridge Without Combat Runtime Ownership Bleed

Problem: X_008 armor data is owned by combat runtime. Steering only needs branchless bite deflection preview, not health authority.

Solution: Mirror the documented 8x6 LUT topology in AI steering: material row from damage class, angle step from `abs(dot(direction, normal))`, and byte strength converted to deflection weight. Registered damage still goes through `CombatDamageRuntime`.

Rejected Alternatives: Pulling `CombatDamageRuntime` profiles into the steering job would cross Echelon 5 ownership and require target-slot mutation locks. Ignoring X_008 would miss the route-card contract.

Scalability potential: Low uses the default suit-impact row. Middle/High/Ultra can feed actual profile bytes later through a cold DataVault bridge without changing job layout.

Hardware Impact: Local 48-cell arithmetic is L1-resident and branchless. Estimated cost: 0.4-1.0 us for 64 predators; avoided managed combat lookup cost is unbounded under contention.

## Implementation Decision 006: Rotated Deterministic Attack Tokens

Problem: A fixed first-N active-slot attack token window prevents simultaneous lunges, but permanently favors lower slot indices.

Solution: Rotate the active-slot token window by dispatcher frame inside the Burst populate job. The token grant remains branchless and bounded by `GlobalQualityWeight`, but slot fairness changes over time without managed queues.

Rejected Alternatives: `NativeQueue` or managed queues introduce contention/order ambiguity. Random token lottery weakens replay determinism.

Scalability potential: Low devices admit one active lunge. Middle/High/Ultra widen the same deterministic window up to four simultaneous attacks through continuous quality weight.

Hardware Impact: Integer modulo and one `math.select` per active predator. Estimated cost: under 1 us for 64 predators; avoided queue contention is larger under swarm pressure.

## Implementation Decision 007: Cold Vault Rebind Clears Handles

Problem: Replacing `GlobalDataVault` while predator cognition holds static `VaultArray` generation handles can route reads into stale buffer generations.

Solution: `InjectDataVault` now completes pending AI jobs in the cold rebind path, clears cognition and steering vault handles, and then stores the new vault reference. Null injection clears the cached vault and fails closed.

Rejected Alternatives: Keeping old handles after service replacement risks undefined native access. Hot polling `GlobalRegistry.DataVault` in `EnsureInitialized` violates registry doctrine.

Scalability potential: Low/Middle/High/Ultra all use the same fail-closed owner route; quality scaling is unaffected.

Hardware Impact: No steady-state cost. Cold rebind may pay one forced completion only on service replacement; avoids persistent invalid-handle stalls/crashes.

## Implementation Decision 008: Late-Frame Presentation Boundary

Problem: Camera feedback from steering must not run inside Burst jobs or simulation phase.

Solution: Jobs write native presentation flags into the 300-frame steering telemetry ring. `PredatorCognitionDomain.LateFrameTick()` finalizes telemetry after nonblocking job completion and publishes `CameraJuiceSignals` there.

Rejected Alternatives: Direct camera component calls from AI or managed feedback during job execution. Both violate phase ownership and create allocation/stall risk.

Scalability potential: Low can drop camera lane packets through bounded SignalBus capacity. High/Ultra can consume the same flags for richer camera processing without changing AI truth.

Hardware Impact: Adds one late-frame bounded signal publish only when clutch/armor feedback flags are present. Estimated steady-state no-feedback cost: 0 us.

## Verification Decision 009: Source-Only Static Gate

Problem: The latest protocol forbids treating compile success as sufficient and explicitly rejects build spam.

Solution: Use source-level static scans, `git diff --check`, DTO size gates already in code, hot-path registry/component searches, and orphan-meta count checks. No `dotnet build` was launched.

Rejected Alternatives: Repeated solution builds would consume CPU and violate the compilation throttling request. JSON/binary proof artifacts were rejected by the user.

Scalability potential: Verification has no runtime tier effect.

Hardware Impact: Avoided a full solution build. Estimated CPU wall-time saved versus one Unity solution build: seconds to minutes on low-end hardware.

## Polish Decision 010: Quarantine Non-Finite Feedback Before Visual Sync

Problem: The steering telemetry ring can flag non-finite velocity for diagnostics. If late-frame presentation reused that entry without a guard, a NaN lunge velocity or AUP could leak into `CameraJuiceSignals`.

Solution: Sanitize current velocity inside steering integration, store max-lunge telemetry only from finite active velocities, and reject non-finite AUP/severity before publishing late-frame impact feedback. Armor strength now uses the explicit X_008 six-bit mask constant.

Rejected Alternatives: Sanitizing only in the camera layer hides an AI-domain fault and weakens black-box ownership. Adding managed exception/log paths in the tick loop was rejected as GC risk.

Scalability potential: Low/Middle/High/Ultra share the same deterministic truth path. Higher tiers may consume richer presentation flags later, but they receive only finite payloads.

Hardware Impact: Adds three branchless/cheap finite guards and one late-frame guard. Estimated low-end i3/MX350 cost is under 1 us per steering phase; avoids undefined visual feedback and post-crash telemetry contamination.

## Polish Decision 011: Sanitize Native Steering Profiles Inside Burst

Problem: Cold CSV parsing sanitizes steering profile values, but the long-lived native profile lane can still be corrupted by editor writes, vault churn, or future authoring paths. A NaN turn multiplier or profile value can poison steering params before later velocity clamps.

Solution: Add a Burst-side sanitizer in `PopulateLeviathanSteeringParamsJob`: selected profiles are clamped to deterministic finite fallback values, invalid profile counts return fallback, and output turn multiplier is sanitized before use.

Rejected Alternatives: Relying only on cold CSV parse was too brittle. Adding managed validation during tick would violate the zero-GC hot path.

Scalability potential: Low/Middle/High/Ultra keep the same fallback profile, while valid profiles still drive richer predator steering on higher fidelity settings.

Hardware Impact: Adds a handful of finite checks per active steering slot. Estimated low-end i3/MX350 cost is below 1 us for 64 predators; prevents NaN propagation into DTO state.

## Polish Decision 012: Director Input Sanitizer And Presentation Cadence

Problem: Director-side survival inputs and `GlobalQualityWeight` are managed runtime facts. If any upstream system publishes NaN/Infinity, the steering control lane can amplify it into token cadence or camera feedback. Late-frame camera feedback also needed a bounded cadence so clutch/armor frames do not become visual spam under swarm pressure.

Solution: Add a small director sanitizer before clutch publication, align fallback survival stress with the runtime context thermal/cold/heat max, and rate-limit late-frame steering presentation by continuous `GlobalQualityWeight` from 10 frames on weak devices to 3 frames on high-end devices.

Rejected Alternatives: Trusting upstream survival values was too brittle. A binary low/ultra presentation switch violates the continuous quality doctrine. Publishing every flagged steering frame risks camera noise and SignalBus pressure.

Scalability potential: Low devices get restrained camera cadence while keeping the same gameplay truth. Middle/High/Ultra progressively increase presentation frequency without changing predator authority, DTO layout, or attack tokens beyond the existing continuous window.

Hardware Impact: Adds finite checks and one late-frame integer cadence gate. Estimated i3/MX350 cost is below 1 us per late-frame finalization; avoided camera feedback churn can save several bounded signal writes during dense predator swarms.

## Polish Decision 013: Flatten Remaining Hot Steering Branches

Problem: The steering patch still had hot conditional routes in SDF sampling, profile selection, target AUP selection, distance clamping, and telemetry averaging. These were small, but they sat inside Burst job execution or payload preparation.

Solution: Convert those routes to `math.select` and masks: clamp SDF voxels before safe indexing, select profiles with a `found` mask, resolve pack/player/fallback target AUP without early returns, clamp double distance branchlessly, and sanitize average velocity before telemetry and presentation.

Rejected Alternatives: Adding a separate NativeQueue or new job would increase moving parts. Replacing the fixed whisker direction `switch` with 26 eager normalized candidates was rejected because it would add more math than it removes on low-tier hardware; the switch index is loop-deterministic, while SDF samples and target ownership are data-dependent.

Scalability potential: Low devices keep predictable SDF sampling and finite presentation payloads. Middle/High/Ultra can increase whisker count through existing `GlobalQualityWeight` without changing DTO layout or adding allocation.

Hardware Impact: Removes several data-dependent branches from hot jobs. Estimated i3/MX350 gain is small but real under swarm pressure: 1-4 us across 64 predators, mostly from branch predictability and NaN containment rather than raw instruction count.

## Polish Decision 014: Fail Closed At Integrator Inputs

Problem: `EvaluateSdfAvoidanceJob` writes finite-clamped repulsion, and target AUP routes are finite by contract, but the integrator should not trust stale or externally corrupted native lanes before it blends pursuit, harassment, clutch, and armor vectors.

Solution: Sanitize `avoidance.Repulsion` at the first read inside `IntegrateSteeringVectorsJob`, and make `ResolveTargetAup` return its deterministic forward fallback if the selected pack/player AUP is non-finite.

Rejected Alternatives: Adding a second validation job would increase scheduling overhead. Deferring sanitation until telemetry or presentation would allow bad state to influence velocity truth first.

Scalability potential: Low devices get the cheapest fail-closed zero vector. Middle/High/Ultra still consume the same richer SDF/target data when it is valid; no quality-tier layout split is introduced.

Hardware Impact: Adds two finite masks in Burst. Estimated i3/MX350 cost is below 0.5 us for 64 predators; it prevents NaN spread through the velocity lane and the 300-frame black-box ring.

## Polish Decision 015: Use Combat Armor Enum For AI Preview Default

Problem: The AI steering preview used byte value `1` for the player armor class. That value matched `CombatArmorClass.Suit`, but the relationship was implicit and could drift if combat enum ordering changes.

Solution: Bind the director default to `(byte)CombatArmorClass.Suit`. This keeps AI steering preview aligned with `HectonPlayerHealth` combat target registration while avoiding direct combat runtime polling in AI ticks.

Rejected Alternatives: Reading `CombatDamageRuntime` target state from the director would cross Echelon 5 ownership and add a hot dependency. Adding a new armor signal without a combat owner publisher would invent an unsupported fact route.

Scalability potential: Low/Middle/High/Ultra share the same owner-safe default. A future combat-owned immutable armor signal can replace the default without changing the Burst DTO layout.

Hardware Impact: No steady-state cost. Removes magic-value drift risk with zero extra instructions after constant folding.

## Polish Decision 016: Separate Steering Write-In-Flight From Telemetry Success

Problem: `_steeringEvaluationJobScheduled` was set only after the final telemetry job was admitted. If mock SDF, populate, avoidance, or integrate jobs had already been admitted and a later schedule failed, debug/read APIs could observe `_steeringEvaluationJobScheduled == false` while native steering lanes were still being written.

Solution: Add a compact steering scheduled-job bitmask. Each admitted steering job sets its bit immediately. `IsLeviathanSteeringWriteInFlight()` now keys off any scheduled steering bit while the evaluation chain is pending. `LateFrameTick()` uses the same mask to report only jobs that actually ran.

Rejected Alternatives: Setting `_steeringEvaluationJobScheduled` after the first job would protect reads, but would also make telemetry finalization run when no telemetry job existed. Keeping the old fixed five-job count overreported admission completion and hid partial-chain failures.

Scalability potential: Low/Middle/High/Ultra share the same lockout behavior. Higher quality can schedule more SDF/steering work without changing read API contracts.

Hardware Impact: One `uint` mask and five bit operations per steering schedule. Estimated i3/MX350 cost is below 0.1 us; removes a race window where debug/read copies could touch mutating native lanes.

## Polish Decision 017: Fail Closed Before Burst Pointer Handoff

Problem: The steering schedule validated that vault arrays existed, but `_activeSlotCount` and SDF dimensions were still trusted before raw pointers were passed into Burst jobs. A corrupted active count, invalid slot index, undersized whisker lane, or stale SDF topology could produce an out-of-bounds access before any inner-loop finite guard could help.

Solution: Add a pre-admission schedule gate. The gate rejects active-count overflow, invalid active slot indices, zero effective steering capacity, and insufficient whisker storage. SDF config now validates its voxel product against the actual SDF buffer length and forces mock SDF regeneration when topology or cell size changes.

Rejected Alternatives: Adding per-job bounds checks would insert branches into `Execute` and still require safe reads before validation. Silently clamping `_activeSlotCount` would hide active-list corruption and could schedule default slot duplicates. Reallocating buffers was rejected because the vault owner already defines capacity.

Scalability potential: Low/Middle/High/Ultra all share the same fail-closed schedule boundary. Higher tiers can raise whisker density through existing quality controls without changing pointer contracts.

Hardware Impact: Adds a linear active-slot scan before job admission. Estimated i3/MX350 cost is below 2 us for normal predator counts; it removes undefined native memory reads that would otherwise be catastrophic.

## Polish Decision 018: Remove Redundant Director Hot Bind And Expire Stale Panic

Problem: `HectonDirectorAI` still called a no-op MetaCampaign rebind from runtime reference retry, and the DDA stress lane could reuse the last `PlayerStressSignal` indefinitely if the slow-tick physiology publisher stopped or was replaced.

Solution: Delete the redundant `RefreshMetaCampaignService()` wrapper and its duplicate calls. Keep MetaCampaign binding on cold registry refresh and hot-swap callback only. Add sequence/frame freshness to the player stress read and fade stale stress over 45 dispatcher frames before it contributes to clutch steering.

Rejected Alternatives: Polling `GlobalRegistry.MetaCampaign` from Tick was rejected as registry doctrine drift. Copying the acoustic runtime's 8-frame hard cutoff was rejected because the physiology owner is `ISlowTickable`; a short binary cutoff would make predator clutch flicker on normal slow-tick cadence.

Scalability potential: Low devices avoid unnecessary director maintenance work and stale panic lock-in. Middle/High/Ultra keep the same cinematic clutch behavior when fresh stress is present, without changing gameplay truth ownership or DTO layout.

Hardware Impact: Removes one periodic no-op method/setter route from the director retry path. Estimated i3/MX350 save is small, roughly 0.02-0.05 us on retry frames; the real gain is eliminating stale panic state that could keep predators in near-miss steering after the stress owner stopped publishing.

## Polish Decision 019: Replace Whisker Switch With Bitmask Axis Decode

Problem: `ResolveWhiskerDirection` used a 26-case switch inside the SDF avoidance sample loop. The index is loop-deterministic, but the switch is still inner-loop control flow in the Burst steering job.

Solution: Preserve the exact old whisker order with six `uint` masks: positive/negative forward, right, and up. Decode weights via `math.select` and normalize the combined axis vector. Verify all 26 indices against the old coefficient table.

Rejected Alternatives: A ternary cube-direction generator was rejected because it changes low-tier first-six coverage from forward/sides/up/down/back to arbitrary cube corners. A static NativeArray lookup was rejected because it adds data ownership and indirection for 26 constant directions.

Scalability potential: Low devices keep the same six first whiskers and no branch table. Middle/High/Ultra can raise active whisker count through `GlobalQualityWeight` while using the same branchless decode.

Hardware Impact: Removes a switch/jump route from every active whisker sample. Estimated i3/MX350 gain is 1-4 us across 64 predators depending active whisker count; exact direction equivalence was verified with `WHISKER_MASK_MISMATCHES=0`.

## Polish Decision 020: Keep Raw Stress Separate From Faded Clutch Input

Problem: The player stress freshness gate initially reused `_lastPlayerStress01` as both the last signal value and the faded output. Once a stress signal stopped publishing, every Tick multiplied an already faded value by the next freshness scalar. That turned the intended 45-frame linear expiry into nonlinear double decay. A second edge case existed if the first valid `SignalBus` sequence was `0`: the frame could remain unmarked and the signal would be ignored until the next sequence.

Solution: Add `_lastPlayerStressRaw01` as the durable raw signal cache. The director now updates raw stress only when a finite signal is present, computes faded stress from raw stress and frame age, and marks the first valid signal as fresh even when its sequence equals the reset value.

Rejected Alternatives: Recomputing clutch directly from the latest `SignalBus` payload would make missing slow-tick physiology frames flicker. Keeping one field for raw and faded stress was rejected because it changes the mathematical expiry curve. Assuming nonzero initial sequence was rejected because it is a hidden dependency on `SignalBus` internals.

Scalability potential: Low/Middle/High/Ultra use the same stable stress truth. Weak devices do not pay extra allocation or polling cost. Strong devices can still receive higher cadence stress signals without changing the clutch formula or steering DTO.

Hardware Impact: Adds one float field and one extra first-sample condition in director Tick. Estimated i3/MX350 runtime cost is below measurement noise; behavioral impact is exact linear 45-frame stale-stress expiry and no first-signal miss.

## Polish Decision 021: Expire Director Steering Control At The Schedule Boundary

Problem: Director clutch and token controls are static inputs consumed by `PredatorCognitionDomain`. If `HectonDirectorAI.Tick` returned early after losing player transform/snapshot, or teardown ordering skipped a normal publication frame, the previous clutch/token values could remain visible to the steering schedule. That violates one owner/one route because stale director authority outlives the director phase that produced it.

Solution: Add fail-closed publication from `HectonDirectorAI` on missing player transform, failed player runtime snapshot, `OnDisable`, and teardown. Add a schedule-side freshness gate in `PredatorCognitionDomain_Steering`: if director control was never published or is older than 8 dispatcher frames, populate receives `DirectorClutchFactor01=0` and `MaxAttackTokens=1`. Publish `CurrentFrameIndex` as the control frame so the freshness comparison uses the same frame domain as `ScheduleFrameEvaluation`.

Rejected Alternatives: Relying only on `OnDisable` ordering was rejected because schedule and service teardown can be interleaved by other agents/systems. Clearing only the local director field was rejected because the steering domain consumes static cached values. Keeping raw `CurrentFrameId` was rejected because schedule uses the masked `CurrentFrameIndex` domain.

Scalability potential: Low devices fail closed to one attack token and no clutch mercy when director facts are stale. Middle/High/Ultra retain continuous token scaling only while fresh director control exists; no DTO layout or gameplay authority split is introduced.

Hardware Impact: Adds one bool, one stale-frame comparison, and two `math.select` assignments per steering schedule. Estimated i3/MX350 cost is below 0.1 us per schedule; removes stale DDA state that could otherwise make predator behavior inconsistent after player authority loss.

## Polish Decision 022: Fail Closed The Entire Director Payload And Telemetry Ring Admission

Problem: The first stale-control gate cleared clutch and token count, but it still allowed stale armor class and stale director frame to flow into the Burst populate job. That could keep token rotation pinned to an old frame while the director was stale. Separately, `RecordSteeringTelemetryJob` indexes the black-box ring with the fixed 300-frame capacity, while the schedule gate only checked `IsCreated`, not the actual vault array length.

Solution: When director control is stale, schedule now passes default suit armor and current schedule frame along with `clutch=0` and `maxTokens=1`. The steering schedule also rejects telemetry rings shorter than `LeviathanSteeringTelemetryCapacity` and empty telemetry cursor arrays before handing pointers to the telemetry job.

Rejected Alternatives: Clearing only clutch was rejected because armor-glance preview and token phase are also director-owned payload. Passing telemetry length into the job was rejected because the black-box contract is exactly 300 frames; admitting a smaller ring would hide a vault allocation bug rather than fail closed.

Scalability potential: Low/Middle/High/Ultra all keep the same fixed black-box size and current-frame token fairness when director state is stale. Higher tiers still get richer clutch/token behavior only with fresh director control.

Hardware Impact: Adds one `uint` schedule-frame value, two `math.select` assignments, and two length checks before scheduling. Estimated i3/MX350 overhead is below 0.1 us per steering schedule; prevents stale token pinning and undersized ring pointer writes.

## Polish Decision 023: Revalidate Resolved SDF Topology And Zero-Delta Director Authority

Problem: `ResolveRuntimeSdfConfig` can replace an invalid SDF config with the default 48x24x48 topology. If the actual vault SDF buffer length is not that topology's voxel count, mock SDF generation would still write within the buffer but use a coordinate lattice that does not match the buffer contract. Separately, `HectonDirectorAI.Tick` returned on `deltaTime <= 0` without clearing director steering control, leaving stale clutch/tokens possible on pause or zero-delta dispatcher frames.

Solution: After resolving SDF config, schedule now immediately proves `dimensions.x * dimensions.y * dimensions.z == sdf.Length` before writing `sdfConfig[0]` or admitting mock/avoidance jobs. Director Tick now publishes fail-closed steering control before the zero-delta return.

Rejected Alternatives: Trying to infer arbitrary dimensions from `sdf.Length` was rejected because no owner route proves the intended lattice. Letting mock SDF write through mismatched topology was rejected because it contaminates avoidance semantics even without OOB. Assuming zero-delta frames never schedule fauna was rejected because dispatcher phase coupling should fail closed.

Scalability potential: Low/Middle/High/Ultra all use the same SDF topology proof. Higher quality can still consume richer SDF/whisker counts only when topology is valid; zero-delta frames always degrade to no clutch and one token.

Hardware Impact: Adds one topology multiplication check per steering schedule and one failure-path reset call on zero-delta Tick. Estimated i3/MX350 cost is below 0.1 us on normal frames; prevents invalid SDF lattice semantics and stale pause-frame DDA state.

## Polish Decision 024: Clear Director Steering Payload On Steering Vault Release

Problem: `InjectDataVault()` correctly completed pending cognition jobs and released vault handles on DataVault replacement, but the static director steering payload remained published. During hot-swap or subsystem teardown, a new vault owner could therefore receive an old clutch/token/armor command before the next director authority frame.

Solution: Add `ResetDirectorSteeringControlState()` in the steering partial and call it from `ReleaseLeviathanSteeringVaultHandles()`. Every steering owner-drop route now clears `clutch=0`, `maxTokens=1`, suit armor, frame `0`, and `_directorSteeringControlPublished=false` through the same handle-release path.

Rejected Alternatives: Waiting for the next `HectonDirectorAI.Tick` was rejected because vault ownership changes are cold but immediate. Duplicating reset calls in `InjectDataVault()` and `Dispose()` was rejected because failed steering allocation and core vault release are also owner-drop paths.

Scalability potential: Low/Middle/High/Ultra all fail closed to fair one-token steering on owner change. Fresh director facts still restore continuous clutch and quality-scaled token behavior once the owner phase publishes again.

Hardware Impact: No steady-state cost. Cold-path reset is five primitive writes. It removes a stale-authority route across native owner swaps without adding hot polling, allocation, or job synchronization.

## Polish Decision 025: Normalize Steering Frame Domain Before Job Payloads

Problem: `ScheduleLeviathanSteering()` already computed a clamped `scheduleFrame`, but avoidance, integration, and telemetry jobs still received raw `(uint)frameId`. A negative bootstrap or teardown frame would wrap to `uint.MaxValue`, while director control freshness and token rotation used the clamped frame domain. Late-frame telemetry finalization had the same raw cast mismatch.

Solution: Feed `scheduleFrame` into all steering job `Frame` fields and compare telemetry entries against `currentFrame = (uint)max(0, frameId)` during finalization. The steering chain now uses one normalized frame domain from schedule to telemetry.

Rejected Alternatives: Assuming dispatcher frame IDs are never negative was rejected because the domain already stores reset states as `-1`. Guarding only telemetry would still leave avoidance/integrate state hashes with wrapped frames.

Scalability potential: Low/Middle/High/Ultra all get deterministic frame hashes and presentation cadence during bootstrap/teardown. Quality scaling remains unchanged.

Hardware Impact: No inner-loop cost. This removes an unsigned wrap edge case with one existing value reuse and one late-frame primitive assignment.

## Polish Decision 026: Normalize Steering Telemetry Cursor Reads And Writes

Problem: The 300-frame steering telemetry ring trusted `TelemetryCursor[0]`. If native cursor memory is corrupted, read paths could produce a negative index after signed `%`, and the writer could overflow `cursor + 1` after accepting a huge positive cursor.

Solution: Add branchless cursor helpers: `NormalizeTelemetryCursor()`, `AdvanceTelemetryCursor()`, and `ResolveTelemetryLastIndex()`. Use them in late-frame finalization, debug telemetry copy, and `RecordSteeringTelemetryJob`.

Rejected Alternatives: Keeping `math.max(0, cursor) % 300` was rejected because it still trusts huge positive cursors and can overflow on advance. Clamping through managed exceptions was rejected because this is a native telemetry path.

Scalability potential: Low/Middle/High/Ultra all keep the fixed 300-frame black box. Higher tiers do not receive a different ring layout or bigger cursor type.

Hardware Impact: Below 0.1 us per telemetry job and one branchless normalize on read paths. Prevents invalid ring indexing after native memory corruption without adding allocation or locks.

## Polish Decision 027: Treat Non-Finite AUP As A Steering Fault

Problem: The telemetry ring flagged non-finite velocity but not non-finite predator AUP. A corrupted AUP could therefore avoid the fault dump trigger, and the presentation lane could publish a camera impact from a zero fallback rather than treating the AI state as invalid.

Solution: Add `SteeringTelemetryFlagNonFiniteAup`. The telemetry job now tests `state.AUP_Position`, captures the first finite active AUP, triggers black-box dump on AUP faults, and blocks late-frame camera feedback when the AUP fault flag is present. Timing values are also sanitized before entering the ring.

Rejected Alternatives: Reusing the velocity fault flag was rejected because it hides which native lane failed. Letting presentation sanitize silently was rejected because AI owns the black-box crash facts.

Scalability potential: Low/Middle/High/Ultra use the same fault semantics and fixed ring layout size. No tier-specific telemetry schema is introduced.

Hardware Impact: Adds one finite check and one `uint` flag select per active telemetry row, plus one failure-path presentation guard. Estimated below 0.1 us per telemetry job on i3/MX350; prevents invalid impact events and missed NaN dumps.

## Polish Decision 028: Normalize Director Steering Publication Frame

Problem: Steering schedule and telemetry now normalize negative frame IDs before converting to `uint`, but `HectonDirectorAI` still published director steering control with a raw unsigned cast of `SystemDispatcher.CurrentFrameIndex`. Bootstrap `-1` could wrap to `uint.MaxValue`, weakening frame-domain parity.

Solution: Add `ResolveDirectorFrameU32()` and use it in both normal steering-control publish and fail-closed reset publish. Director, steering schedule, and telemetry finalization now share the same negative-frame clamp rule.

Rejected Alternatives: Relying on schedule-side freshness wrap arithmetic was rejected because it fixes symptoms but leaves the owner payload in a different frame domain. Duplicating inline casts was rejected because future call sites would drift.

Scalability potential: Low/Middle/High/Ultra get identical frame freshness semantics. Quality scaling and token window behavior remain unchanged.

Hardware Impact: One branchless max per director steering publication. Estimated below measurement noise; removes unsigned wrap during bootstrap/teardown.

## Polish Decision 029: Align Full Clutch With Critical Contract And Ignore Stale Telemetry Entries

Problem: The director clutch curve was continuous but reached full near-miss strength only above the documented high-panic point. Late-frame telemetry finalization also read the last ring entry before proving it belonged to the current normalized frame, so a stale flagged entry could trigger dump or presentation handling after schedule gaps.

Solution: Add explicit director clutch constants: full health point `0.10`, health ramp `0.15`, full stress point `0.85`, stress ramp `0.20`. The curve still ramps continuously but reaches `1.0` at the documented critical state. Telemetry finalization now returns immediately if `entry.Frame != currentFrame` before mutating burst timing, publishing camera impact, or triggering a black-box dump.

Rejected Alternatives: A hard binary clutch switch at health <10% and stress >0.85 was rejected because the project requires continuous control. Keeping the old curve was rejected because it made the exact prompt threshold only partial strength. Allowing stale telemetry flags to dump was rejected because black-box facts must describe the current AI frame.

Scalability potential: Low/Middle/High/Ultra share the same continuous clutch curve and fixed 300-frame ring. Higher quality still changes token width and presentation cadence only; it does not change the DDA truth threshold.

Hardware Impact: Director Tick adds no allocation and only uses constant multiply/rcp math. Late-frame adds one current-frame comparison. Estimated i3/MX350 cost is below measurement noise; prevents stale dump/camera work and makes the 40 cm full offset occur at the documented critical state.

## Polish Decision 030: Keep Steering Alive Between Cognition Due Frames

Problem: `ScheduleFrameEvaluation()` returned before the leviathan steering chain when no cognition due-flag was active. That saved work, but it also tied kinematic steering, clutch near-miss, armor glance, token denial, and the 300-frame steering black box to cognition cadence instead of active predator motion. Predators could carry stale kinematic output between cognition solves.

Solution: Reuse the existing steering chain on idle cognition frames by scheduling `ScheduleLeviathanSteering(frameId, default)` before the no-work return. Add `_swarmAnalysisJobScheduled` so LateFrame reports a SwarmAnalysis completion only when the swarm job was actually admitted. No new job type, queue, data structure, or direct dependency was introduced.

Rejected Alternatives: Forcing the full cognition/swarm/mesofauna stack every frame was rejected because it is CPU waste and violates the cadence/quality doctrine. Creating a separate lightweight steering scheduler was rejected because it duplicates ownership. Keeping the old return was rejected because the DDA steering effect would stutter at lower cognition cadence.

Scalability potential: Low devices continue running only the existing steering chain on idle cognition frames; Middle/High/Ultra benefit from smoother clutch, harassment, and armor-glance presentation without changing gameplay truth ownership or DTO layout.

Hardware Impact: Idle cognition frames now pay only the already bounded steering chain when active predators exist. Estimated i3/MX350 cost is below forcing a full cognition phase by hundreds of microseconds; the saved visual/kinematic stability is bought without managed allocation.

## Polish Decision 031: Quarantine SDF Local Space Before Debug Payloads

Problem: SDF sampling had safe index clamping, but a non-finite `SdfOriginAup` or local whisker coordinate could still be written into `SteeringWhiskerResultDTO.SampleLocalMeters`. That contaminates debug/gizmo readers and weakens black-box evidence even when the actual SDF read falls back safely.

Solution: Sanitize `SdfOriginAup` in `ResolveRuntimeSdfConfig()` and compute a finite `sampleLocal` inside `EvaluateSdfAvoidanceJob`. The job samples SDF, samples normals, and writes whisker payloads from `sampleLocal`; corrupt local coordinates set the existing `SteeringAvoidanceFlagNonFinite` and cannot become a rock hit.

Rejected Alternatives: Adding a separate validation job was rejected because it increases schedule overhead. Trusting `SampleSdf()` safe indexing alone was rejected because it protects memory reads but not debug/telemetry payload truth.

Scalability potential: Low/Middle/High/Ultra share the same finite local-space quarantine. Higher tiers may increase whisker count, but every whisker still writes finite debug facts or an explicit non-finite flag.

Hardware Impact: One `float3` finite mask and one `math.select` per active whisker, plus one schedule-side `double3` finite mask. Estimated i3/MX350 cost is below 0.5 us for 64 predators; it prevents NaN debug propagation without allocation or new data ownership.

## Polish Decision 032: Promote Avoidance Faults Into Steering Black Box

Problem: `SteeringAvoidanceFlagNonFinite` identified SDF/local corruption, but telemetry only dumped budget, velocity, and AUP faults. A finite velocity/AUP frame with corrupt avoidance local space could therefore avoid black-box dump and still publish clutch/armor presentation.

Solution: Add `SteeringTelemetryFlagNonFiniteAvoidance`. `RecordSteeringTelemetryJob` maps avoidance non-finite flags into the telemetry ring, `FinalizeLeviathanSteeringTelemetry()` includes the flag in the dump mask, and presentation refuses frames with avoidance faults.

Rejected Alternatives: Reusing the AUP flag was rejected because it hides which native lane failed. Leaving avoidance faults only in the avoidance DTO was rejected because the DTO is transient and not the required 300-frame evidence owner.

Scalability potential: Low/Middle/High/Ultra all get the same black-box fault semantics. Higher whisker counts on stronger devices increase coverage, not schema complexity or allocation.

Hardware Impact: One bit test plus one `math.select` per active predator in telemetry, and one late-frame mask test. Estimated i3/MX350 cost is below 0.1 us for 64 predators; it prevents corrupted SDF local space from bypassing dump evidence.

## Polish Decision 033: Sanitize Director Ingress Before Stress Math

Problem: The director already sanitized survival/stress values, but public external pressure, sonar ping intensity, and dispatcher delta could still enter with NaN. `math.saturate(NaN)` and `math.max(existing, NaN)` are not acceptable ingress contracts for the DDA owner because one bad event can contaminate clutch, phase, and acoustic threat state.

Solution: Fail-close `Tick()` on non-finite `deltaTime`. Sanitize public `ApplyExternalPeakPressure()` pressure/hold inputs and existing held state. Sanitize the internal external pressure application before writing ref stress/threat values. Route sonar intensity through `SanitizeDirector01()`.

Rejected Alternatives: Assuming mod/API/event callers are well-behaved was rejected. Adding exception/log paths was rejected because runtime ingress must stay allocation-free and predictable.

Scalability potential: Low/Middle/High/Ultra all get the same finite director truth. Quality scaling remains continuous and does not branch by device class.

Hardware Impact: One finite check in director Tick, one to three finite selects on external pressure paths, and one finite select per sonar event. Estimated i3/MX350 cost is below 0.1 us on normal frames; it prevents NaN stress propagation into AI pacing and predator steering control.

## Polish Decision 034: Sanitize Acoustic Payloads, Director Events, And Stored Timers

Problem: Physics event payloads, director event producers, and stored cadence timers still had smaller NaN ingress lanes. A corrupted acoustic scalar could reach fauna cue sinks or threat spike events, and a stale NaN timer could freeze cold dependency retry, frustum refresh, debounce, or frame-time averaging.

Solution: Clamp acoustic radius/energy/intensity/duration before sonar stress, deafening, aggro, boid scatter, and threat-spike routes. Clamp `DirectorAIEvents` values/positions before music signal and listener ring publication. Repair warning clock, resolve retry timer, frame history, frustum timer, sonar debounce, sight cooldown, hunter cooldown, and threat smoothing delta before arithmetic.

Rejected Alternatives: Adding a new validation manager was rejected because the existing owners already know the correct fallback semantics. Logging bad payloads was rejected because these paths must stay bounded and allocation-free.

Scalability potential: Low/Middle/High/Ultra share the same finite ingress contracts. Device quality can still scale cadence and presentation, but never changes event payload validity or AI truth ownership.

Hardware Impact: Adds scalar finite selects on event ingress and timer subtraction paths. Estimated below 0.2 us per drained acoustic payload on i3/MX350; prevents NaN propagation into combat steering, music feedback, and cooldown cadence.

## Polish Decision 035: Quarantine Cognition Job Scalars Before Utility Scoring

Problem: The steering chain was hardened, but the upstream cognition job still accepted NaN-prone scalar lanes: bucket cell size, position buckets, quantized score inputs, deltas, apex tuning, retinal exposure, acoustic gain, health, and pack flank distance. One NaN could corrupt due scheduling, predator utility scores, or the byte-packed drive lanes before steering ever saw the frame.

Solution: Extend the existing `PredatorCognitionDomain` partial with small branchless finite sanitizers and use them in the existing hot helpers/jobs. No new manager or data owner was added. Bucket helpers zero non-finite positions before int casts, quantization clamps NaN to zero, due timers repair non-finite schedule state, and predator/passive scoring consumes sanitized health/light/acoustic/radius values.

Rejected Alternatives: Adding a validation job was rejected because it adds schedule overhead and duplicates ownership. Logging bad scalar inputs was rejected because hot cognition must stay zero-GC. Leaving only steering-side guards was rejected because corrupted cognition scores can still pick the wrong state.

Scalability potential: Low devices get deterministic cheap fallback scoring instead of poisoned AI state. Middle/High/Ultra can spend the saved stability on smoother stalking and alpha-leviathan presentation without changing truth ownership or DTO layout.

Hardware Impact: Branchless selects and clamps only. Estimated under 0.6 us for 64 active agents on i3/MX350; prevents NaN casts, byte lane corruption, and state-score contamination without managed allocation.

## Polish Decision 036: Normalize CognitionInput At The Owner Boundary

Problem: `SubmitInput()` still stored raw `CognitionInput` producer packets. The previous scalar clamps protected several job paths, but vector lanes, AUP local offsets, memory writes, and external state mutators could still persist non-finite data before the Burst jobs or black-box telemetry saw it.

Solution: Add one owner-side `SanitizeCognitionInput()` helper inside `PredatorCognitionDomain` and route `SubmitInput()` through it. The same helper is reused by hot jobs instead of duplicating clamp logic. Stimulus/acoustic memory and external state mutators now sanitize their position/time/intensity/duration payloads before writing domain state.

Rejected Alternatives: Creating a separate validation system was rejected because it duplicates ownership and adds dispatch complexity. Keeping only downstream clamps was rejected because stale state can still persist in the vault and black-box rings.

Scalability potential: Low devices get deterministic finite fallback state with no extra managers. Middle/High/Ultra keep richer predator behavior but consume the same normalized DTO contract.

Hardware Impact: Submit-time struct copy plus branchless finite selects. Hot jobs reuse the same helper and remove scattered scalar clamps. Estimated below 1 us for normal active predator batches on i3/MX350; removes persistent NaN vector and timer contamination without managed allocation.

## Polish Decision 037: Normalize Read-Side Public Snapshot Lanes

Problem: Owner writes were normalized, but several read-side/presentation-adjacent lanes still consumed raw `_inputs[slot]`, raw chain timing, and unchecked active-slot loops. Damage/respawn events, debug gizmo copies, pheromone publication, alpha telemetry, and mesofauna telemetry could therefore expose stale/non-finite data even after submit-time cleanup.

Solution: Reuse `SanitizeCognitionInput()` on those existing read paths, clamp `_activeSlotCount` by `_activeSlots.Length`, sanitize vectors before Unity `Vector3` conversion, sanitize chain microseconds once in `LateFrameTick()`, bound acoustic debug memory reads by actual bank length, and resolve writable slot capacity from real vault lane lengths before register/reset-style writes. Explicit `in` was removed from indexer sanitizer calls to avoid byref-indexer compile hazards. No new owner, manager, collection, or event route was added.

Rejected Alternatives: Adding a debug-copy wrapper system was rejected because public copy functions are already the owner boundary. Trusting debug callers to sanitize was rejected because NaN payloads must be stopped before crossing the domain edge.

Scalability potential: Low devices keep cheap bounded copies and deterministic fallback vectors. Middle/High/Ultra can expose richer gizmo/telemetry evidence without changing gameplay authority or DTO layout.

Hardware Impact: Adds branchless finite selects and one extra length comparison per copied active slot. Slot-capacity resolution is cold/register-path only. Estimated below 0.5 us for 64 active predators on i3/MX350; prevents debug/presentation consumers from receiving non-finite fauna vectors.

## Polish Decision 038: Flatten Remaining Runtime Switch Branches

Problem: After the NaN and owner-boundary hardening, the predator cognition job still had switch routes for utility states, alpha phases, octant directions, world-state flags, and state mapping. The director also had bounded switch routes for event dispatch, phase events, hot-swap service handling, and event offset octants.

Solution: Replace cognition switches with explicit bool masks, `math.select`, direct if/else phase target routes, and branchless octant vector math. Replace director switches with direct condition routes and the same branchless octant math. Existing owners were edited in place; no new classes, collections, event routes, or lookup tables were introduced.

Rejected Alternatives: A managed dictionary or delegate table was rejected because it would allocate or obscure dispatch ownership. A static array LUT for Burst octants was rejected because managed static arrays are not a safe Burst hot-path dependency. Keeping director switches was rejected because the same route drift would remain near bounded event paths.

Scalability potential: Low devices get deterministic cheap state routing and no branchy state surface in predator cognition. Middle/High/Ultra can widen existing quality-scaled token and presentation behavior without changing truth ownership or DTO layout.

Hardware Impact: Cognition switch flattening saves an estimated 1-3 us across 64 active predators mostly through branch predictability, not raw instruction count. Director route changes are not claimed as frame-time wins; they reduce drift and keep cold registry replacement explicit.

## Polish Decision 039: Remove Editor Scanner Token Noise

Problem: The touched steering partial still produced allocation-token scan hits from an editor-only menu scanner log using string concatenation and `.ToString()`. It was not runtime, but it made the final static gate less precise.

Solution: Replace the editor-only concatenated log with a single interpolated string. Runtime AI code, DTOs, jobs, and signal routes were not changed.

Rejected Alternatives: Removing the editor scanner was rejected because it is useful tooling. Leaving the noisy hit was rejected because it forces future integrators to reclassify a harmless editor path every audit.

Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged. Tooling remains available for editor inspections without polluting runtime scans.

Hardware Impact: No runtime impact. Editor-only path may allocate exactly once per manual menu command, outside gameplay frame execution.

## Polish Decision 040: Cache Encounter Director Registration State

Problem: `HectonDirectorAI.IsInitialized` was a read accessor that polled `GlobalRegistry.EncounterDirector`. It was not a high-frequency `Tick` lookup, but it violated the project rule that read accessors must be pure cached reads and made consumers pay a registry dependency to ask a simple ownership question.

Solution: Move the ownership proof into `HectonDirectorAI` state. Successful registration sets `_encounterDirectorServiceRegistered` and `s_activeRuntimeInstance`; encounter-director hot-swap callbacks mirror forced replacement into the same cached fields; teardown unregisters from cached ownership instead of polling the registry slot.

Rejected Alternatives: Leaving the registry read was rejected because cold-but-impure accessors still create drift and make consumers depend on global state. Adding a second service wrapper was rejected because the existing director owns the fact and can mirror hot-swap state directly.

Scalability potential: Low/Middle/High/Ultra all get the same cached identity route. Quality scaling remains independent: registration truth does not affect gameplay authority, DTO layout, or visual fidelity knobs.

Hardware Impact: Steady-state accessor cost is one bool read plus one reference compare. No managed allocation, no registry read, no scene search. Hot-swap branch is cold and only runs on service replacement.

## Polish Decision 041: Correct Cognition Black Box Dump Ownership

Problem: Retinal, alpha leviathan, and mesofauna black-box dumps still targeted `Dump_13AI.bin`. That left current 1702 domain faults writing to a stale agent route, while the steering black-box already used `Docs/AgentLogs/Dump_1702.bin`.

Solution: Replace the old file-name constant with one relative-path constant, `Docs/AgentLogs/Dump_1702.bin`, and pass it directly into the existing retinal, alpha, and mesofauna writer methods. Payload layout and writer ownership were left unchanged.

Rejected Alternatives: Per-system dump files were rejected because the mandate requires `Dump_[YourID].bin`. Leaving the stale ID was rejected because fault evidence would be misowned. Adding a dump router was rejected because the current owner methods already write bounded native payloads.

Scalability potential: Low/Middle/High/Ultra all get the same crash/fault evidence path. This does not affect quality scaling or gameplay truth; it improves post-fault determinism.

Hardware Impact: No steady-state impact. Cold fault path removes one string concatenation allocation per dump attempt and avoids writing evidence under the wrong agent filename.

## Polish Decision 042: Sweep Adjacent Fauna Dump Constants

Problem: After the cognition dump correction, adjacent fauna runtimes still held `Dump_13AI.bin` constants: stress-driven spawn, tentacle Verlet, fauna kinematics, bite kinematics, and crab leg IK. Fault evidence would still be split across stale and current agent IDs.

Solution: Update those existing constants to `Docs/AgentLogs/Dump_1702.bin`. No payload layout, writer function, telemetry ring, or runtime branch changed.

Rejected Alternatives: Adding a shared dump-path utility was rejected because constants are sufficient and avoid a new dependency. Per-system filenames were rejected because the black-box mandate names `Dump_[YourID].bin`.

Scalability potential: Low/Middle/High/Ultra all keep the same fault-evidence path. This change is independent from quality scaling and does not alter simulation fidelity.

Hardware Impact: No steady-state impact. Constants only; cold fault writes now route to the correct owner file.

## Polish Decision 043: Owner-Local Wound Bounds And Late-Frame Proxy Teardown

Problem: `CreatureDamageManager` fallback wound bounds merged child `Renderer.localBounds` directly into owner-local space. Child-local bounds are not owner-local, so rotated/scaled fauna children could produce wrong wound volume. The shader clear proxy also stayed registered after the clear completed, leaving a permanent late-frame branch in the dispatcher.

Solution: Convert child renderer world bounds corners through the owner transform before encapsulation. The fallback now keeps one owner-space route for all renderers without adding a new helper system. The shader clear proxy unregisters itself when no clear is pending and after a clear executes.

Rejected Alternatives: Using renderer-local bounds directly was rejected as mathematically wrong for nested fauna rigs. Forcing `Complete()` or per-frame renderer refresh was rejected as a stall risk. Keeping the proxy registered forever was rejected because a one-shot cleanup must not become a permanent dispatcher participant.

Scalability potential: Low devices get accurate wound bounds without extra steady-state work. Middle/High/Ultra can still use richer renderer rigs because the fallback respects transform hierarchy instead of assuming simple meshes.

Hardware Impact: Bounds conversion is cold bind/rebuild work only. Late-frame proxy self-unregister removes one useless branch after each shader clear; estimated savings are below measurement noise but it cleans dispatcher pressure.

## Polish Decision 044: Crab IK Slot High-Water And In-Flight Mutation Guard

Problem: `ProceduralCrabLegIKRuntime` initialized its free-slot stack so the first crab used the highest slot. GPU uploads and indirect draws therefore used near-capacity spans for a single active entity. The default leg-count helper also snapped any authoring count above the minimum to the maximum, ignoring middle leg counts. Public mutation routes could write vault-backed arrays while IK jobs were scheduled unless they forced a completion, which is forbidden.

Solution: Initialize the free-slot stack so early registrations allocate low slots first, track `_lastRenderEntityCount` as the highest active slot plus one, and upload/draw only that high-water range. Preserve sparse-slot correctness by using high-water rather than active count. Clamp authored leg count between min and max. Fail closed on register/unregister/pose/hash mutation while jobs are in flight instead of completing the job on the caller path.

Rejected Alternatives: Compacting active crab slots every frame was rejected because it would rewrite identities and add data movement. Drawing by active count was rejected because sparse slots can exist after unregisters. Calling `Complete()` inside mutation APIs was rejected because it creates a hidden main-thread stall.

Scalability potential: Low devices draw and upload only the active low-slot span. Middle/High/Ultra can spend the saved upload bandwidth on denser leg pose quality without changing identity layout or buffer ownership.

Hardware Impact: A single active crab now uploads/draws one entity span instead of the full capacity after cold allocation order. On i3/MX350 this avoids unnecessary GPU buffer upload bandwidth; exact gain depends on active crab count and fragmenting unregister patterns.

## Polish Decision 045: Crab IK DataVault Mutation Guard

Problem: Crab IK schedules jobs over arrays resolved from `GlobalDataVault.TryResolveHandle`. The vault contract says those views are current-phase only unless protected by a lock or guard. The runtime had no dedicated mutation guard for its cross-phase job chain, and short owner writes used the same resolved buffers without a compaction-fence guard.

Solution: Add one owner-local `CrabLegVaultMutationGuardMask` covering the crab entity, foot, target, step, body-pose, solved-matrix, and telemetry buffers. `Tick()` acquires the guard before frame-state writes, retains it while the IK job chain owns the arrays, and `LateFrameTick()`, origin-shift finalize, teardown, and buffer disposal release it. Short register/unregister/pose/avoidance/origin-shift writes acquire the same guard and release it in `finally`.

Rejected Alternatives: Taking multiple `TryAcquireWriteLock` fences was rejected because the project forbids holding more than one DataVault write lock and these jobs need multiple buffers. Completing jobs synchronously inside public mutation APIs was rejected as a hidden stall. Adding a new crab vault manager was rejected as duplicate ownership.

Scalability potential: Low devices get fail-closed buffer mutation under compaction pressure. Middle/High/Ultra keep the same IK data layout and can scale visual leg quality without changing ownership or adding new routes.

Hardware Impact: One mutation guard acquire/release per scheduled IK frame plus short guarded owner writes. Estimated i3/MX350 cost is lower than a forced completion and prevents compaction-fence races over six crab buffer lanes.

## Polish Decision 046: Leviathan Tentacle Job Guard And Per-Draw GPU Bindings

Problem: `LeviathanTentacleVerletSolver` scheduled a cross-frame Verlet job over arrays returned by `GlobalDataVault.TryResolveHandle` without a retained relocation/mutation guard. Its indirect draw also pushed matrix, radius, flow, and constant buffers through global shader state, so multiple leviathan instances could overwrite each other during `VISUAL_SYNC`. `Tick()` rejected `deltaTime <= 0` but still allowed NaN through to grab-damage timer math before local sanitization.

Solution: Add one owner-local mutation guard mask covering all tentacle position, radius, matrix, root/target, state, and telemetry buffers. `Tick()` acquires it before owner writes, retains it across the scheduled job, and releases it in late-frame, origin-shift finalize, teardown, or disposal. Seed and origin-shift writes use the same guard with `finally` release. The indirect draw now binds matrix/radius/flow/constant buffers through a cached `MaterialPropertyBlock` on `RenderParams.matProps`. Delta ingress rejects non-finite values and passes clamped `safeDeltaTime` into damage cadence.

Rejected Alternatives: Holding multiple `TryAcquireWriteLock` fences was rejected because the job needs many buffers and the lock doctrine rejects multiple simultaneous write locks. Calling `Complete()` from mutation or origin-shift routes was rejected as a hidden main-thread stall. Leaving global shader state was rejected because it is not instance-owned. Adding a new render manager was rejected as duplicate ownership.

Scalability potential: Low devices keep one cheap deterministic tentacle solve and fail closed under vault contention. Middle/High/Ultra can run richer tentacle materials or more visible leviathan instances because per-draw buffers no longer collide in shader state.

Hardware Impact: One atomic mutation guard acquire/release per scheduled frame replaces unbounded compaction races and avoids forced completion. Per-draw MPB binding is reused after cold allocation; no steady-state GC. The NaN delta gate prevents timer poisoning at sub-microsecond cost.

## Polish Decision 047: Guard Fauna Residency Data-Only LOD Memory

Problem: `FaunaSimulationMemory` exposed DataVault-backed pool slot, velocity, simulation flag, and free-slot stack lanes through small read/write methods without a relocation/mutation guard. The resident data-only LOD Burst job could hold those arrays across a frame while release, hydration, save, or residency update routes resolved and mutated the same lanes. A failed native clear also could remove the active slot first and leave resident state orphaned.

Solution: Add one owner-local `FaunaSimulationMutationGuardMask` covering all four residency lanes. Short accessors acquire the guard and release it in `finally`. `TryScheduleResidentDataOnlyLod()` acquires and retains the same guard for the scheduled job, and `FaunaDirector.CompleteResidentDataOnlySimulation()`/dispose release it after job completion. Direct `FreeSlots` calls in `FaunaDirector` now route through guarded `TryDequeueFreeSlot()` and `EnqueueFreeSlot()`. Slot release clears native lanes before removing the active-slot index.

Rejected Alternatives: Per-lane write locks were rejected because the residency update touches multiple DataVault buffers and the lock doctrine rejects simultaneous write locks. A new residency manager was rejected because `FaunaSimulationMemory` already owns the handles. Synchronous completion inside slot mutation routes was rejected because it would hide a main-thread stall.

Scalability potential: Low devices get deterministic fail-closed residency mutation while cheap data-only LOD runs. Middle/High/Ultra can keep larger resident fauna pools and richer hydration/dehydration behavior without compaction races or identity rewrites.

Hardware Impact: One guard acquire/release per short residency access and one retained guard per scheduled resident LOD job. Estimated below forced-completion cost on i3/MX350; prevents compaction-fence relocation races over pool, velocity, flag, and free-slot lanes without managed allocation.

## Polish Decision 048: Pin Fauna Corpse Sink Kinematic Job Buffers

Problem: `FaunaBrain` schedules `CorpseSinkKinematicJob` over `GlobalDataVault` input/output buffers without retaining a mutation guard. A compaction fence or owner mutation could relocate or invalidate the resolved views while the corpse presentation job is still scheduled. The route also validated handles and read output without an explicit guard.

Solution: Add one `CorpseSinkKinematicMutationGuardMask` covering `FaunaCorpseSinkKinematicInput` and `FaunaCorpseSinkKinematicOutput`. `ScheduleCorpseSinkingKinematicStep()` computes floor/AUP data before locking, then retains the guard while resolving buffers, writing one input DTO, and scheduling the job. `CompleteCorpseSinkingKinematicsIfReady()` reads the output under the retained guard and releases in `finally`. Lifecycle teardown and vault rebinding force-complete only on cold teardown and release the retained guard.

Rejected Alternatives: Per-buffer write locks were rejected because this route needs two buffers and the lock doctrine rejects multiple simultaneous write locks. A new corpse presentation manager was rejected because `FaunaBrain` owns death-spire presentation state. Moving floor-height lookup into the guard was rejected because terrain/cache queries are heavier than the DataVault DTO copy.

Scalability potential: Low devices keep cheap deterministic corpse sinking without compaction races. Middle/High/Ultra keep the same cinematic whale-fall presentation path and can spend quality budget on shader decay or biolum presentation without changing gameplay truth.

Hardware Impact: One guard acquire/release per scheduled corpse sink step. No steady-state allocation. On i3/MX350 this is cheaper than hidden synchronous completion or a corrupted native view retry path.
