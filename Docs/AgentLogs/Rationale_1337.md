# Rationale_1337

## Session Start

Problem: Agent prompt was not in workspace root `current_batch.md`; active batch file is `Docs/Tasks/CURRENT_BATCH.md`.
Solution: Extracted only `<AGENT_PROMPT id="1337" ...>` using PowerShell regex over the full file.
Rejected Alternatives: Reading neighboring prompts or archived batches; both violate strict parsing and batch hygiene.
Scalability potential: N/A, routing decision only.
Hardware Impact: Avoids wasted scans and build invocations on i3/MX350-class host.

Problem: Physics culling can break constraints if treated as pure distance-only sleeping.
Solution: Phase 0 will map rigidbody state owners, AUP provenance, and keep-awake/constraint dependencies before code mutation.
Rejected Alternatives: Add per-object Update distance checks, Camera.main polling, Vector3.Distance loops, or immediate Sleep/Wake in Burst job.
Scalability potential: Low uses contracted radius and aggressive cadence; Middle keeps primitive colliders near player; High expands radius and smoother wake bands; Ultra spends saved CPU on richer near-field debris/visual physics while distant bodies remain culled.
Hardware Impact: Target gain is reduced PhysX integration/contact work on i5-1135G7/MX350; exact microseconds remain pending code and measurement.

## Phase 0 Decisions

Problem: The prompt names `PhysicsCullingOverseer.cs`, but the codebase already owns culling in `GlobalPhysicsStateManager` plus `Physics/GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs`.
Solution: Harden the existing owner and document adjacent root edits as critical cross-domain surgery; avoid second culling authority.
Rejected Alternatives: Creating a new overseer file with duplicate tracked body lists; that would violate one fact -> one owner and risk conflicting Sleep/Wake decisions.
Scalability potential: Low keeps one compact Vault lane and changed-index drain; Middle/High/Ultra reuse the same job while expanding radius/fidelity continuously.
Hardware Impact: Avoids duplicate O(N) scans and duplicate native buffers on i3/MX350.

Problem: AUP body cache is stale because `TryUpdateTrackedBodyAupCache` returns the prior value before sampling the current Rigidbody position.
Solution: Refresh current runtime-position-derived AUP each culling slow tick, only falling back to last valid AUP when current position is invalid or origin resolution fails.
Rejected Alternatives: Using stale cache for speed; it can sleep moving constraints or newly activated debris at the wrong coordinate. Searching scene AUP owners per body is rejected as hot-path scene polling.
Scalability potential: Low through Ultra all need correct truth coordinates; quality may alter radius, not position authority.
Hardware Impact: Adds one existing position sample/origin conversion per tracked body per 0.1s slow tick; prevents expensive false sleep/wake churn and invalid broadphase transitions.

Problem: Physics culling radius scale is a constant and violates continuous GlobalQualityWeight policy.
Solution: Map HomeostasisBrain.GlobalQualityWeight through smoothstep to a radius scale from 0.5x to 1.5x, squared for the job.
Rejected Alternatives: Binary quality tiers or build-time hardware switches; both violate continuous quality doctrine.
Scalability potential: Low = 25m debris wake from 50m base, Middle = near 50m, High/Ultra = up to 75m physical fidelity radius.
Hardware Impact: Low-end hardware contracts active physics volume; top-tier machines spend saved CPU on larger near-field simulation instead of more truth owners.

Problem: Telemetry rings exist but are 32B and do not prove job/sync microseconds, quality, or dump route.
Solution: Widen culling telemetry entries to 64B with explicit offsets and write cold binary dump from the fixed 300-entry rings.
Rejected Alternatives: Debug.Log telemetry or managed List snapshots; both allocate and are not postmortem-safe enough.
Scalability potential: Same fixed 300-entry ring at all quality levels; telemetry captures how quality changes radius and cost.
Hardware Impact: 64B writes are O(1) and cold dump is crash/over-budget only; no frame-loop managed allocation.

## Implementation Decisions

Problem: Existing culling DTO is 40B, not 64B, but it is already a Vault-owned route consumed by Burst jobs.
Solution: Retained `PhysicsCullingDTO` stride and widened telemetry DTOs to 64B, where the black-box mandate needs fixed-ring postmortem state.
Rejected Alternatives: Expanding the hot culling DTO to 64B only for symmetry; that would add 24B per body to the hottest job lane without a consumer need.
Scalability potential: Low keeps tighter memory bandwidth; Middle/High/Ultra retain same culling truth and spend quality only on radius/fidelity.
Hardware Impact: Saves roughly 48KB over 2048 tracked bodies versus forced 64B hot DTO expansion.

Problem: Culling needed continuous quality without mutating gameplay truth.
Solution: Quality scales only `HardwareRadiusSqScale`; it does not change DTO identity, authority ownership, save identity, or culling command semantics.
Rejected Alternatives: Low/high tier branch tables and separate DTO layouts; both violate the scalability pillar.
Scalability potential: Low 0.5x radius, middle continuous transition, high/ultra 1.5x radius.
Hardware Impact: On i3/MX350 fewer distant bodies remain active; on high-end machines more near-field physics can stay alive.

Problem: Black-box dump must preserve the last 300 frames without heap snapshots.
Solution: The cold dump writes a 64B header followed by raw body and frame telemetry rings from existing NativeArray-backed Vault buffers.
Rejected Alternatives: Serializing JSON or strings during fault path; text serialization allocates and loses fixed-layout replay proof.
Scalability potential: Same dump format at all quality weights; quality/radius fields explain why a body was culled.
Hardware Impact: No steady-state frame cost; dump I/O occurs only after global blackbox gate is active.

Problem: A runtime fuzzer must not spawn 1000 Unity bodies to prove a data-lane culling path.
Solution: Added editor menu route that calls existing `GenerateMockPhysicsBodies(1000)` and `FireMockSeismicShockwave(1337)`.
Rejected Alternatives: GameObject spam scene test; that benchmarks transform/PhysX instantiation, not the culling data lane.
Scalability potential: Mock lane can stress the same Burst culling buffers from low through ultra.
Hardware Impact: Avoids editor scene allocation storm; validates native culling scale path.

## Validation Decisions

Problem: The build gate eventually passed, but `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` failed in cross-domain Gameplay VR somatic code before a green compile could be recorded.
Solution: Stopped at the domain boundary and recorded Task 14 as dependency-blocked. The compiler error set names `Assets/_Project/Scripts/Gameplay/VRSomaticProvider.cs` and `Assets/_Project/Scripts/Gameplay/VRSomaticProvider.HorizonLock.cs`; no Physics culling file is named.
Rejected Alternatives: Editing `VRSomaticProvider*` from the Physics culling task; that violates domain ownership and risks corrupting another agent's active work. Re-running builds without code changes would waste host time.
Scalability potential: N/A for Physics culling runtime; this is a build-integrity dependency.
Hardware Impact: One gated build consumed 80.91 seconds wall time. No further build loops until the Gameplay dependency is fixed.

Problem: Compile status changed after purge and build-server contention cleared.
Solution: Re-ran one gated build only after CPU/csc/dotnet gate passed. `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` succeeded with 0 warnings and 0 errors in 36.40s.
Rejected Alternatives: Continuing to report dependency-blocked after the gate became clean; that would be stale evidence.
Scalability potential: N/A runtime; build proof restored.
Hardware Impact: One additional gated build, no repeated compile loop.

Problem: APEX purge found that my validator/fuzzer used managed `throw new`, and legacy editor radii readers used broad `catch (Exception)`.
Solution: Replaced the validator with fail-closed `ValidateForEditor()` and editor log returns; replaced broad catches with `IOException`, `UnauthorizedAccessException`, and `SecurityException`. Simulation hot path remains no-throw.
Rejected Alternatives: Keeping throws because they were editor/cold. The purge scanner requirement is stricter than the initial validator decision.
Scalability potential: N/A runtime; failure mode is now deterministic fail-closed instead of managed exception control flow.
Hardware Impact: No hot-frame cost; removes exception metadata path from touched culling files.

Problem: Padding used `uint`/`ushort` fields and `PhysicsCullingCounter64` placed 4-byte fields after 56 bytes of cacheline padding, which weakens the pointer-first/byte-padding proof.
Solution: Converted culling DTO padding to explicit private byte fields and moved `PhysicsCullingCounter64.Value`/`Flags` to offsets 0/4 with private byte padding from 8..63.
Rejected Alternatives: Arguing that `uint` padding is layout-equivalent. The audit requirement explicitly asks for private byte padding.
Scalability potential: Same 64B counter stride at all quality levels; no semantic route change.
Hardware Impact: Maintains 64B stride while removing ambiguous padding interpretation on ARM64.

Problem: The Burst distance job subtracted AUP in double precision but cast directly to `float3` without an explicit component clamp.
Solution: Added `PhysicsCullingLocalDeltaClampMeters` and `math.clamp(deltaDouble, -limit, +limit)` before float cast.
Rejected Alternatives: Relying on finite checks and float range. That proves non-NaN, not deterministic local precision at extreme AUP.
Scalability potential: Low through Ultra use identical truth math; quality only scales culling radius.
Hardware Impact: Three scalar clamps per candidate in the culling job; buys deterministic stability near large world coordinates.

Problem: Native collection ownership needed syntax-tree evidence, not grep.
Solution: Ran `Tools/VaultNativeAliasRoslynAudit` on the three touched C# files copied into `.codex_tmp/agent1337_scope`. Result: 21 native field declarations, 0 persistent, 21 transient job parameters, 0 parse failures.
Rejected Alternatives: Regex-only native collection scan. It cannot classify owner type or job interfaces.
Scalability potential: N/A runtime; proof artifact only.
Hardware Impact: No runtime cost.

Problem: Re-audit found an unused legacy `PhysicsDistanceCullingJob` in `GlobalPhysicsStateManager.cs` with five transient NativeArray fields and an obsolete relative-AUP distance path.
Solution: Removed the private unused job entirely and kept the scheduled Shinobu37 path as the single culling solver. Native-field Roslyn audit dropped from 21 to 16 declarations, still 0 persistent fields.
Rejected Alternatives: Leaving dead code because it was not scheduled. Dead Burst job DTOs still pollute evidence and preserve the wrong AUP pattern.
Scalability potential: Low through Ultra use one culling solver route; no duplicate authority to diverge under quality scaling.
Hardware Impact: Compile/runtime code surface is smaller; no runtime microsecond claim because the dead job was not scheduled.

Problem: The active culling job still had solver decision branches after the fail-closed guards.
Solution: Rewrote the Shinobu37 distance job decision path to mask/math.select style: hysteresis, depth scale, behind-camera scale, sleep/wake threshold, kinematic, mesh-strip, command bits, age update, and changed-index write are branchless. Rewrote mock shockwave wake and changed-index compaction similarly where safe.
Rejected Alternatives: Claiming guards as solver branches or deleting bounds checks. Bounds checks stay because fail-closed OOB prevention is more important than a fake branchless report.
Scalability potential: Continuous `GlobalQualityWeight` still drives radius scale; lower devices contract active physical volume, upper tiers keep larger near-field physics without changing truth layout.
Hardware Impact: Branch predictor pressure reduced in the scheduled culling job; exact microseconds remain pending Unity profiler capture.

Problem: Branch audit needed syntax-tree evidence, not token grep.
Solution: Built a temporary Roslyn scanner under `.codex_tmp/PhysicsCullingBranchAudit` and wrote `Docs/Reports/PHYSICS_CULLING_BRANCH_AUDIT_1337.json`. Result: 4 job Execute if-statements, all fail-closed bounds guards; 0 non-guard job Execute branches; 0 distance solver decision branches.
Rejected Alternatives: Regex branch counts. Regex cannot separate job owner, Execute body, and fail-closed bounds guards.
Scalability potential: N/A proof artifact.
Hardware Impact: Scanner build was cold tooling only; no runtime cost.

Problem: Code changed after prior compile proof.
Solution: Shut down idle MSBuild build-server nodes, then re-ran one gated project build after CPU/csc/dotnet gate passed. `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /nr:false` succeeded with 0 warnings and 0 errors in 79.00s.
Rejected Alternatives: Reporting the older build as final proof after code mutation.
Scalability potential: N/A runtime; compile proof restored.
Hardware Impact: One additional gated build; `/nr:false` prevents persistent MSBuild node reuse after the proof.

Problem: APEX rerun exposed that Physics culling relied on `TryResolveHandle` fence checks but did not explicitly acquire GlobalDataVault write locks around the phases that publish culling writes and job result drains.
Solution: Added `VaultBufferBinding<T>.TryAcquireWriteLock/ReleaseWriteLock`, wrapped scheduling plus result dispatch phases in fail-closed lock acquisition with `finally` release blocks, and made cold setter writes fail-closed through the same write-lock route when no phase lock is held. Locks are released immediately after scheduling or dispatch publication, never across frames or async yields.
Rejected Alternatives: Claiming `TryResolveHandle` alone as sufficient. It checks the compaction fence, but it is weaker evidence than explicit write-lock acquisition tied to the owner system id.
Scalability potential: Low keeps the same culling truth but can fail closed during compaction contention; Middle/High/Ultra preserve the same lock route while expanding radius through continuous quality.
Hardware Impact: Lock acquisition is per culling slow-tick/dispatch phase, not per body job lane. It adds scalar metadata checks but prevents compaction/dangling-view failure modes on cheap devices.

Problem: The first post-lock-patch project build was blocked by concurrent World/Vegetation code outside the Physics domain.
Solution: Did not edit World files. Waited for the build gate to clear, re-ran one gated build after CPU/dotnet/csc were clear, and recorded the final green compile.
Rejected Alternatives: Editing `Assets/_Project/Scripts/World/*` from a Physics culling assignment; that violates the domain boundary and risks overwriting other agents' work.
Scalability potential: N/A runtime; compile proof only.
Hardware Impact: Final gated build consumed 34.93 seconds wall time with 0 warnings and 0 errors. Build servers were shut down afterward.

Problem: Re-audit found `ApplyPhysicsCullingCommand` could trigger `DumpPhysicsCullingBlackBox` for invalid culling input while dispatch write-locks were still held.
Solution: Replaced the lock-phase dump call with two scalar pending fields and execute the black-box dump only after `ReleasePhysicsCullingDispatchLocks1337()` completes in the dispatch `finally`.
Rejected Alternatives: Keeping the dump inside the invalid-input branch because it is rare. Rare I/O while holding Vault write-locks still blocks compaction and violates the synchronization proof.
Scalability potential: Low through Ultra keep identical fail-closed wake/restore behavior; the only changed route is cold dump timing after lock release.
Hardware Impact: No hot allocation added. Invalid-input fault path stores one byte and one float during dispatch, then performs file/event dump after lock release. Final build after patch: 0 warnings, 0 errors, 50.13 seconds.

Problem: Global hot-path scanner reported managed-risk creations because it intentionally scans whole files and cold/value-type construction, not only simulation hot methods.
Solution: Added and ran a targeted Roslyn proof tool under `.codex_tmp/PhysicsCullingZeroGcHotPathAudit` scanning 21 named hot methods and `IJob.Execute` bodies for managed reference creation, native collection allocation, strings, LINQ, foreach, interpolation, and string concatenation.
Rejected Alternatives: Reporting the noisy global scanner as zero-GC proof. It is useful as a broad smoke test, but the purge gate requires focused hot-path evidence.
Scalability potential: N/A runtime; proof artifact only.
Hardware Impact: Scanner artifact only. Result: 0 zero-GC hot-path hits.

Problem: APEX rerun 12 needed a fresh build proof, but the first `dotnet build` wrapper timed out while child dotnet nodes were still alive, leaving no trustworthy compile result.
Solution: Treated that build as invalid evidence, waited for dotnet/csc to clear, obeyed the CPU gate, then ran one longer-timeout build. Final gate: CPU=28.88%, dotnet=0, csc=0; build succeeded with 0 warnings and 0 errors in 98.76s; build servers were shut down afterward.
Rejected Alternatives: Counting the timed-out wrapper as green, launching a second build while dotnet nodes were alive, or editing unrelated files without compiler evidence.
Scalability potential: N/A runtime; this preserves build proof integrity under a 20+ agent workspace.
Hardware Impact: One discarded timed-out wrapper plus one valid gated build; no runtime code changed in rerun 12.

Problem: The latest rejection asked to edit other needed files carefully if done, but no new compiler or scanner defect pointed outside the Physics culling artifacts.
Solution: Updated only the disk proof files: `Status_1337.md`, `Rationale_1337.md`, `LOG_1337.md`, and `PHYSICS_CULLING_OPTIMIZATION_REPORT_1337.json`. Production C# remained unchanged after the green static/build proof.
Rejected Alternatives: Touching broad dirty files from other agents or expanding the domain to chase unrelated status noise.
Scalability potential: N/A runtime; keeps one owner and one proof route.
Hardware Impact: Documentation/report-only update; 0 hot-frame cost.

Problem: APEX rerun 13 repeated the same rejection and required a new disk-backed proof, not a replay of rerun 12 claims.
Solution: Re-extracted prompt 1337, copied the three touched C# files into a fresh rerun scope, reran native/zero-GC/branch/broad hotpath scanners, replayed lock/fence evidence, and ran one gated build. Final gate: CPU=8.81%, dotnet=0, csc=0; build succeeded with 0 warnings and 0 errors in 125.80s.
Rejected Alternatives: Reusing prior report hashes without regenerating scanner outputs, touching unrelated dirty files from other agents, or compiling before the CPU/dotnet/csc gate cleared.
Scalability potential: N/A runtime; this is proof integrity for the existing culling implementation.
Hardware Impact: One valid gated build and scanner replay; no production C# changed in rerun 13.

Problem: The prompt still asks for root `current_batch.md`, but both `C:\hades\current_batch.md` and `C:\hades\Hecton8\current_batch.md` are absent.
Solution: Used the only active batch file containing `<AGENT_PROMPT id="1337">`: `Docs/Tasks/CURRENT_BATCH.md`. The extracted block is 18613 bytes, contains 19 task markers, and hashes to `3e605cefcb2f812e4e2febd79f1e87c3d527fb27627de85b3f5c8ffa05c01387`.
Rejected Alternatives: Creating a synthetic root batch file or reading neighboring prompts.
Scalability potential: N/A routing only.
Hardware Impact: Avoids wasted filesystem scans and wrong-agent work.

Problem: Deep audit found two remaining short-circuit boolean OR expressions inside the scheduled culling solver path: `sleepActive` and the six-plane `IsOutsideFrustum` test.
Solution: Replaced both with non-short-circuit boolean OR so Burst sees a straight-line boolean reduction in the inner solver body while the fail-closed bounds guards keep normal `||` for early return safety.
Rejected Alternatives: Leaving the code because the Roslyn branch scanner already reported 0 non-guard `if` branches. That would be technically incomplete because short-circuit `||` can still lower as conditional control flow inside a hot path.
Scalability potential: Low devices get less branch unpredictability in the culling job; Middle/High/Ultra keep the same continuous `GlobalQualityWeight` radius expansion without adding a new physics solver.
Hardware Impact: No measured microsecond claim without Unity profiler capture. Static gates after patch: native 16 total / 0 persistent / 16 job transient; targeted zero-GC 21 hot methods / 0 hits; job Execute non-guard branches 0; job-range `||` remains only in fail-closed guards.

Problem: The user requested online research, including community sources, but production engine decisions must be based on authoritative technical documents.
Solution: Used official Unity documentation as binding input: Rigidbody sleeping removes sleeping bodies from physics calculations and warns against unnecessary WakeUp; NativeContainer safety requires unmanaged data in jobs and deterministic read/write restrictions; NativeContainer views can become invalid when dynamic storage moves.
Rejected Alternatives: Treating Reddit comments as implementation authority. Community anecdotes can suggest investigation targets, but they are not stable contracts for HECTON-8 physics architecture.
Scalability potential: Confirms the existing sleep/kinematic/mesh-strip cheat: low-end sheds far physics, higher tiers spend continuous quality budget on a larger active physics radius.
Hardware Impact: Supports the current design direction; no new runtime code added from online material beyond the branchless cleanup found by local audit.

Problem: A compile proof after the branchless cleanup would be useful, but the host build gate is red.
Solution: Did not run `dotnet build`. Gate samples reported CPU=83.61% and later CPU=100%, with Code/Python/Codex as top CPU consumers and no dotnet/csc processes. Static Roslyn and token scanners were used instead.
Rejected Alternatives: Launching a project build under >50% CPU and starving sibling agents; this violates the build-throttling mandate.
Scalability potential: N/A runtime; this preserves multi-agent host stability.
Hardware Impact: Avoided a heavy MSBuild run while the machine was saturated.

Problem: Normal acoustic, impact, and `WakeRequestSignal` pulses called `WakeCulledBodiesNear`, which force-completed any active culling job and scanned every tracked body on the main thread.
Solution: Routed normal wake pulses into the existing Burst wake lane as value-copied AUP wake-region signals. `WakeCulledBodiesNear` now first queues a wake region; the old O(N) barrier path remains only as overflow/unavailable fallback. The pending wake capacity is 16, matching the signal frame flush limit, and the job copies signal values before scheduling so later main-thread queue writes cannot race with a scheduled job.
Rejected Alternatives: Deleting the immediate wake barrier entirely; public `WakeBodiesNear` still needs a fail-closed fallback if the native queue is full or unavailable. Passing the pending signal `NativeArray` into the job was also rejected because new wake pulses could write the same backing buffer while the scheduled job reads it.
Scalability potential: Low devices avoid forced job completion and broad main-thread scans for normal wake pulses; Middle/High/Ultra preserve immediate fallback only under overflow while batching wake decisions in parallel.
Hardware Impact: Replaces repeated O(N) main-thread wake scans with one Burst wake job over existing DTO rows. No measured microsecond claim until Unity Profiler capture; static proof after patch stayed zero-GC and 0 persistent native fields.

Problem: The wake job became a first-class event route but did not record schedule ticks or register its active `JobHandle` with `H8Memory`, unlike the distance culling job.
Solution: Added `_physicsCullingJobScheduleTicks = Stopwatch.GetTimestamp()` and `H8Memory.RegisterActiveJob(OwnerSystemId, _physicsCullingJobHandle)` to the wake-job scheduling path.
Rejected Alternatives: Leaving telemetry accounting only on the distance job. That would make wake-pulse job time disappear from the black-box trail after moving real events into this route.
Scalability potential: Same accounting across low, middle, high, and ultra quality; quality changes radius, not the observability contract.
Hardware Impact: One timestamp read and one active-job registration per queued wake batch.

Problem: The build gate after rerun 15 was red.
Solution: Did not run `dotnet build`. Gate sample returned CPU=100%, dotnet=1, csc=1; process sample showed another dotnet/csc compile already active.
Rejected Alternatives: Starting a competing build and violating the multi-agent CPU/build-server rule.
Scalability potential: N/A runtime; host stability.
Hardware Impact: Avoided MSBuild contention while another compiler was already running.

Problem: Targeted physics wake requests wrote `AwakeResults`, `CommandResults`, and `ChangedIndices` before the next culling job was scheduled; `SchedulePhysicsChangedIndexClear` could erase that manual changed-index publication before `DispatchPhysicsCullingResults`.
Solution: Moved targeted wakes to direct owner-phase restoration: after any active job has completed and before a new job is scheduled, the request restores the culled body state on the main thread, applies the preserved frozen velocity plus impulse, resets culling age, and never writes the job-result scratch lanes. Removed the now-dead `AddPhysicsStateChangedIndex` helper.
Rejected Alternatives: Keeping the manual changed-index path and hoping no job clear runs after it. Scheduling a tiny per-target job was also rejected: Unity docs make NativeContainer write dependencies explicit, and a one-body job would add scheduling overhead with no batch locality.
Scalability potential: Low devices avoid lost wake requests without adding another broad scan; Middle/High/Ultra keep the same batched region wake job for area pulses and use direct O(k) sync only for explicit target wakes.
Hardware Impact: Bounded by `PhysicsCullingTargetWakeQueueCapacity` (64) and only runs on requested target wakes. Static rerun 16: native 16 total / 0 persistent / 16 job transient; targeted zero-GC 21 hot methods / 0 hits; job Execute non-guard branches 0.

Problem: The build gate after rerun 16 was still red.
Solution: Did not run `dotnet build`. Gate sample returned CPU=100%, dotnet=0, csc=0.
Rejected Alternatives: Launching MSBuild under saturated CPU and competing with other agents.
Scalability potential: N/A runtime; host stability.
Hardware Impact: Avoided a heavy compile while the machine was saturated.

Problem: Hot wake enqueue routes still called `EnsureNativeState()`, which can bind through `GlobalRegistry`, refresh cold dependencies, release/ensure Vault buffers, and grow native storage from acoustic/impact/wake event paths.
Solution: Removed `EnsureNativeState()` from `QueueTargetedPhysicsWakeRequest` and `TryQueuePhysicsCullingWakeRegion`. Both routes now fail closed if buffers are absent and write only through method-local `NativeArray<T>` views acquired by `TryAcquireWriteLock`, with `ReleaseWriteLock` in `finally`.
Rejected Alternatives: Keeping the bootstrap call because it usually no-ops after initialization. The route is still hot and must not contain a hidden allocation/bootstrap path. Writing through separate silent indexer locks was also rejected because targeted wake mirror and count could be partially updated under contention.
Scalability potential: Low devices avoid registry/bootstrap work during wake spam; Middle/High/Ultra preserve the same batched wake-region route and direct targeted wake queue without changing culling truth or DTO layout.
Hardware Impact: Removes a cold-state validation/allocation branch from wake events. Static rerun 17: native 16 total / 0 persistent / 16 job transient; targeted zero-GC 21 hot methods / 0 hits; job Execute non-guard branches 0. Build deferred because the first gate returned CPU=100%, dotnet=1, csc=1 and the latest gate still returned CPU=100%, dotnet=0, csc=0.

Problem: Wake signal dependency audit found `WakeRequestSignal` was ABI-correct but used two private `ulong` fields as tail padding, which does not satisfy the byte-padding proof required for unmanaged DTO contracts consumed by Physics culling.
Solution: Replaced the 48..63 tail with explicit private byte fields and added `WakeRequestSignal` size/offset checks to `PhysicsCullingLayout1337`. SignalBus snapshot lifecycle was also checked: `FlushPostSimulation` clears frame snapshot count before copying pending signals, so Physics culling does not replay stale wake requests.
Rejected Alternatives: Leaving the Core signal DTO untouched because it is outside the nominal Physics folder. This DTO is the wake route contract for culling, so a padding-only edit is justified and safer than duplicating a Physics-local wake DTO.
Scalability potential: Low through Ultra keep the same wake route and same 64B stride; only proof and validator coverage changed.
Hardware Impact: Runtime ABI unchanged. Static rerun 18: native 5 files / 16 total / 0 persistent / 16 job transient; targeted zero-GC 21 hot methods / 0 hits; job Execute non-guard branches 0. Build deferred because CPU gate was 96.36%; latest retry remained red at 70.27%.

Problem: Wake storm overflow still had a hidden emergency path: if the deferred wake-region queue was full or unavailable, `WakeCulledBodiesNear` force-completed the culling job and scanned all tracked bodies on the main thread.
Solution: Removed the immediate fallback from the normal wake route. Region wake overflow now fails closed by incrementing culling contention telemetry; normal acoustic, impact, and signal wakes remain on the deferred Burst wake lane.
Rejected Alternatives: Keeping the fallback for "responsiveness." It fires exactly during pressure, violates the no hidden `.Complete()` rule, and converts wake spam into O(N) main-thread work.
Scalability potential: Low devices avoid worst-case sync spikes during wake bursts; Middle/High/Ultra keep the same batched route and can spend quality budget on a wider active radius instead of emergency scans.
Hardware Impact: Removes one force-complete plus full tracked-body scan from wake overflow. Static rerun 19 after patch: native 5 files / 16 total / 0 persistent / 16 job transient; targeted zero-GC 21 hot methods / 0 hits; job Execute non-guard branches 0. Build deferred because CPU gate was 93.04%, then 98.46%.

Problem: Two unmanaged DTOs had meaningful byte flags after artificial byte-padding holes: `FrozenVelocityDTO.HasVelocity` at 28 after bytes 24..27, and `PhysicsCullingDebugBody` flags at 36..37 after bytes 32..35.
Solution: Moved `HasVelocity` to 24 and debug flags to 32..33, keeping sizes unchanged at 32B and 40B. Added debug-body offset checks to `PhysicsCullingLayout1337`.
Rejected Alternatives: Leaving the layout because total size was already a multiple of 8. The mandate requires pointer/8-byte, then 4-byte, then 2-byte, then 1-byte fields before padding; these flags should begin the 1-byte section.
Scalability potential: Runtime behavior unchanged across tiers; this tightens ABI proof for Vault/debug consumers.
Hardware Impact: No hot-frame work added and no stride change. Layout validator now covers the corrected offsets.

Problem: Targeted wake flush was still a compaction-window weak point. `FlushPhysicsTargetWakeRequests` read and wrote wake mirror/count, frozen velocity, state age, and DTO lanes through individual `VaultBufferBinding` indexer operations instead of one scoped write-lock phase.
Solution: Added `TryAcquirePhysicsTargetWakeFlushLocks1337` and `ReleasePhysicsTargetWakeFlushLocks1337`. The full targeted wake flush now acquires wake mirror, wake count, frozen velocity, state age, and DTO write locks up front, processes direct owner-phase wake restores, clears consumed requests, and releases every lock in `finally`.
Rejected Alternatives: Relying on indexer-level locks. That path is technically safe for a single write but weak for a multi-lane transaction because the Vault can observe partial publication between lanes under contention.
Scalability potential: Low devices avoid lock churn during target-wake bursts; Middle/High/Ultra keep exact targeted wakes as O(k) owner sync, while area wakes stay batched in the Burst wake-region job.
Hardware Impact: Removes multiple per-request lock acquire/release pairs from targeted wake flush. Static rerun 20: native 5 files / 16 total / 0 persistent / 16 job transient; targeted zero-GC 21 hot methods / 0 hits; job Execute non-guard branches 0.

Problem: Wake-region enqueue failure telemetry was double counted by `WakeCulledBodiesNear`: `TryQueuePhysicsCullingWakeRegion` already increments contention on lock or capacity failure, then the caller incremented again for every false return, including invalid input and floating-origin shift.
Solution: `WakeCulledBodiesNear` now delegates to `TryQueuePhysicsCullingWakeRegion` without caller-side counting. The enqueue method remains the single owner of contention telemetry for real native-lane failure cases.
Rejected Alternatives: Keeping the double count because it is "only telemetry." The black-box ring is a proof artifact; false contention spikes hide real lock pressure.
Scalability potential: Same runtime wake behavior across tiers; cleaner telemetry lets quality tuning distinguish real wake pressure from invalid or temporarily blocked routes.
Hardware Impact: Removes one scalar increment on failed normal wake calls. Build deferred because CPU gate was 96.35%, dotnet=0, csc=0.

Problem: Targeted wake flush still had one structural mutation escape path: if a queued targeted wake pointed at a stale/null `Rigidbody`, `ProcessTargetedPhysicsWakeRequest` called `RemoveTrackedBodyAt`, which starts with `CompletePhysicsCullingJobForStateMutationBarrier(discardResults: true)` and mutates tracked-body lanes.
Solution: Null targeted wake requests now fail closed with an immediate return while the scoped wake flush locks are held. Stale body removal remains handled by the normal unregister/structural maintenance paths outside targeted wake flush.
Rejected Alternatives: Removing the body immediately for cleanliness. That mixes cleanup authority into a wake transaction, can force a hidden job completion under Vault write-locks, and increases contention exactly during wake bursts.
Scalability potential: Low devices avoid barrier spikes during target-wake bursts; Middle/High/Ultra retain direct targeted wake restore for valid bodies and batched region wakes for area pulses.
Hardware Impact: Removes a possible hidden job completion and structural lane mutation from a bounded O(k) targeted wake loop. Static rerun 21: native 5 files / 16 total / 0 persistent / 16 job transient; targeted zero-GC 21 hot methods / 0 hits; job Execute non-guard branches 0. Build deferred because gate was CPU=100%, dotnet=2, csc=0.

Problem: The main culling schedule and dispatch phases still contained `RemoveTrackedBodyAt` calls for null bodies while scoped Vault write-locks were held. That method begins with a culling job state-mutation barrier and performs lane swaps.
Solution: Added `RemoveNullTrackedBodiesOutsidePhysicsCullingLocks` and call it before schedule locks and after dispatch locks when needed. In-lock null bodies now write fail-closed ignore/default state or set a scalar deferred-cleanup flag.
Rejected Alternatives: Keeping cleanup local to the exact null branch. Local cleanup looks tidy, but it hides structural mutation and possible job completion inside NativeContainer write ownership windows.
Scalability potential: Low devices avoid worst-case cleanup stalls during culling result publication; Middle/High/Ultra preserve the same culling math and continuous quality scaling without adding another job.
Hardware Impact: Removes possible hidden sync/structural mutation from schedule and dispatch lock phases. Static rerun 22: native 5 files / 16 total / 0 persistent / 16 job transient; targeted zero-GC 21 hot methods / 0 hits; job Execute non-guard branches 0. Build deferred because CPU gate was 100%.

Problem: Removing an already-destroyed/null body could not call `GetEntityId()`, so `_trackedBodyIndexByInstanceId` could keep a stale key pointing at whatever body was swapped into the removed lane. Targeted wake could then wake the wrong Rigidbody.
Solution: Cached `EntityInstanceHash` in `RigidbodyState` at registration/update time and remove both cached entity and cached instance keys before lane swap, even when the Unity object is already null.
Rejected Alternatives: Guessing instance hash from `ulong EntityId` or waiting for dictionary overwrite. The manager uses `GetEntityId().GetHashCode()` for targeted wake lookup, so the exact key must be stored by the owner.
Scalability potential: All tiers get deterministic targeted wake identity; no quality-specific behavior or DTO layout change.
Hardware Impact: One extra int in the managed `RigidbodyState` array and two dictionary removes on structural body removal only. No hot job or DTO cost.

Problem: `FixedTick` still performed two broad owner-thread scans outside the Burst culling path: collider LOD hysteresis scanned every tracked body at the physics fixed-step rate, and added-mass tensor updates scanned every tracked body even when hydrodynamic submersion had not changed.
Solution: Moved collider LOD hysteresis to the existing slow-cadence budget and replaced added-mass tensor updates with a bounded preallocated dirty-index queue. `SetHydrodynamicSubmersionInternal` marks a body dirty only when sanitized submersion/fully-submerged state changes, `DrainAddedMassTensorDirtyQueue` applies only dirty bodies, lane swaps requeue moved dirty state, and runtime clear resets the queue.
Rejected Alternatives: Leaving the scans because they are not Burst jobs; that is exactly how fixed-step CPU leaks survive culling. Creating another job was rejected because the work mutates Unity `Rigidbody` properties and would require same-frame main-thread readback, violating the tiny-job/readback rule.
Scalability potential: Low devices avoid 50Hz O(N) owner scans for debris/weed bodies; Middle keeps visual collider simplification on 10Hz hysteresis; High and Ultra preserve the same richer near-field physics radius while spending CPU on visible effects, not dormant body bookkeeping.
Hardware Impact: Static estimate removes one full added-mass tracked-body scan from every fixed tick and cuts collider LOD scan cadence from 50Hz to roughly 10Hz. At 2048 tracked bodies this removes about 2048 added-mass iterations per physics tick plus four of five collider LOD passes; exact microseconds require Unity Profiler capture.

Problem: Dirty added-mass state could become stale if a tracked body is removed by lane swap while its old index is still queued.
Solution: `RemoveTrackedBodyAt` checks the moved `RigidbodyState.AddedMassDirty`, clears the old dirty flag, and requeues the moved body at its new index before publishing the updated state.
Rejected Alternatives: Scanning the whole dirty queue on every removal. That would reintroduce O(k) cleanup work into structural mutation and is unnecessary when the moved state carries its own dirty bit.
Scalability potential: Low through Ultra keep deterministic tensor updates without per-removal queue compaction. Quality does not change the truth route.
Hardware Impact: One branch on structural body removal only; avoids stale dirty entries without touching the fixed-step hot path.

Problem: `EvaluateConnections` cleared `CompensationRefCount` and `CullingLockRefCount` for every tracked body every FixedTick, even when the connection registry was empty or only one tether/dock connection existed.
Solution: Added a bounded `int[MaxTrackedConnections * 3]` touched-index list. Connection lock application records only bodies whose refs were touched; the next `EvaluateConnections` clears only that previous touched set before recomputing live connections. Structural body removal flips a one-shot full-clear flag so lane swaps cannot strand stale refs.
Rejected Alternatives: Keeping the full tracked-body clear because it is simple; that burns O(N) fixed-step CPU to maintain O(connection count) truth. A dictionary/set was rejected because it would add managed hashing and possible allocation pressure; duplicates in a bounded int array are cheaper and safe.
Scalability potential: Low devices pay O(k connections) rather than O(2048 bodies) for tether/dock protection; Middle/High/Ultra keep the same deterministic culling locks while spending saved CPU on visible near-field physics.
Hardware Impact: In the common no-connection case, `EvaluateConnections` now returns after scalar checks. With a few active tethers/docks it clears at most the previous touched indices and scans the 128-slot connection registry, not all tracked bodies. Exact microseconds require Unity Profiler capture.

Problem: `PhysicsCullingTelemetryEntry` used explicit layout but placed `CullingFlags`, `FrameHash`, and `Reserved0` after `ushort`/byte fields, violating the project field-order proof even though the size stayed 64B.
Solution: Reordered the private telemetry entry so 4-byte fields occupy offsets 40..48, the 2-byte field sits at 52, semantic byte fields start at 54, and tail padding is explicit private bytes. Updated `ValidatePhysicsCullingPrivateTelemetryLayout1337` to check the new offsets.
Rejected Alternatives: Leaving the old ABI because it was technically aligned. The mandate requires pointer/8-byte, then 4-byte, then 2-byte, then 1-byte fields before padding; the validator must prove that order, not just total size.
Scalability potential: Runtime behavior and telemetry stride are unchanged across low, middle, high, and ultra. The change tightens ABI proof for the black-box ring.
Hardware Impact: No hot-frame work added; one cold validator predicate changed. Static rerun 24 layout/zero-GC gates remain green.

Problem: Null tracked-body cleanup still had routes that could call `RemoveTrackedBodyAt` while a culling job was active; that method force-completes/discards active culling work as a structural barrier.
Solution: Added `_deferredNullTrackedBodyCleanup` and `TryRemoveNullTrackedBodyAt`. Null cleanup now removes immediately only when no culling job is scheduled; otherwise it sets a scalar deferred flag and cleanup runs after non-blocking job completion, outside scheduling/dispatch write-lock phases. `ClearRuntimeState` resets the flag.
Rejected Alternatives: Forcing completion for cleanliness or leaving null entries until arbitrary future structural mutation. Forced completion is the hidden sync defect; unbounded staleness risks stale dictionary and lane state.
Scalability potential: Low devices avoid surprise sync spikes under destroyed debris/weed churn; Middle/High/Ultra keep the same owner-phase cleanup semantics without changing culling truth.
Hardware Impact: Removes a possible active-job force-complete from null-body cleanup. The steady-state cost is one boolean flag check in existing owner ticks; exact microseconds require Unity Profiler capture.

Problem: `BufferID.RigidbodyAUPs` is consumed by rollback/hash/QA readers as `AupExactDouble3`, but the physics owner published camera-relative delta during culling schedule and runtime-space position during origin-shift commit.
Solution: Changed `_rigidbodyAUPs` writes to publish `AbsoluteUniversePosition.ToAbsoluteDouble3()` from the proven current body AUP. Local camera-relative delta remains only inside the culling solver path through `PhysicsCullingDTO` and `CameraAbsoluteAup` subtraction.
Rejected Alternatives: Keeping relative values because the current culling job does not consume `_rigidbodyAUPs`. Cross-domain readers do consume that Vault lane, and its current report contract is exact AUP double3.
Scalability potential: All device tiers get deterministic rollback/hash coordinates; quality can scale culling radius, not coordinate authority or DTO truth.
Hardware Impact: Replaces one double3 delta write with one absolute double3 write on the existing slow culling tick; no new allocation and no extra scan.

Problem: The impact-to-wake payload route still had a DTO proof gap. `PhysicsImpactSignal` used `uint` and `ulong` tail padding at offsets 104..127 after semantic byte fields, while Physics culling creates and consumes this impact signal for wake routing.
Solution: Replaced the tail padding with explicit private byte fields and added `PhysicsImpactSignal` plus private `PhysicsImpactEventData` size/offset checks to `PhysicsCullingLayout1337`.
Rejected Alternatives: Leaving the Core contract untouched because the file is outside `Assets/_Project/Scripts/Physics`. This signal is part of the 1337 wake route, so the padding-only contract edit is a justified cross-domain interface fix. Editing unrelated KCC, vehicle, or fluid DTO padding debt was rejected because those are not culling/sleep ownership and likely belong to other agents.
Scalability potential: Low, Middle, High, and Ultra keep the same 128B signal ABI and same wake behavior; the fix only hardens deterministic layout proof for the impact wake lane.
Hardware Impact: Runtime stride and semantic offsets unchanged. Static rerun 25: native audit 4 files / 16 total / 0 persistent / 16 job transient; zero-GC 21 hot methods / 0 hits; branch audit 0 non-guard job Execute branches. Build deferred because the first gate was CPU=100%, dotnet=2, csc=0 and latest retry was CPU=99.81%, dotnet=0, csc=0.

Problem: Reset/shutdown clearing in `ClearShinobu37PhysicsCullingState` still cleared DTO, spatial hash, wake mirror/count, changed-count, telemetry, and mock wake lanes through repeated `VaultBufferBinding` indexer writes. Each setter is individually compaction-aware, but the reset transaction could publish partially cleared lanes and churn locks across hundreds or thousands of slots.
Solution: Added `TryAcquirePhysicsCullingClearLocks1337` and `ReleasePhysicsCullingClearLocks1337`. The reset path now attempts one scoped write-lock set for the created Shinobu37 culling lanes, clears under that scoped ownership, and releases in `finally`. If a scoped clear lock cannot be acquired, the previous guarded per-index write behavior remains as a cold fallback so teardown does not leave managed state stranded.
Rejected Alternatives: Reusing the scheduling lock wrapper. It locks result scratch lanes that the reset method does not clear and would make the reset path depend on unrelated buffers being created. Hard-failing the whole reset on one missing/contended lane was also rejected because shutdown/scene-transition cleanup must still make best effort without inventing another owner route.
Scalability potential: Low devices avoid reset/scene-transition lock churn when large debris or weed sets are tracked; Middle/High/Ultra keep the same culling truth and wake behavior. Quality scaling remains continuous through the existing radius/cadence logic and is not tied to reset semantics.
Hardware Impact: Hot frame cost is 0 us. Cold reset effect: replaces many per-slot write-lock attempts with one scoped lock set when available. Static rerun 26: native audit 5 files / 16 total / 0 persistent / 16 job transient; targeted zero-GC 21 hot methods / 0 hits; branch audit 0 non-guard job Execute branches. Build deferred because CPU gate stayed red at 99.81% then 100%, dotnet=0, csc=0.

Problem: Root `ClearRuntimeState` still cleared `_impactEvents`, `_lastValidPositions`, `_rigidbodyAUPs`, culling result lanes, and body telemetry through repeated `VaultBufferBinding` indexer writes. The writes were individually guarded, but root reset could still publish partially-cleared native lanes and churn write locks during scene transition or shutdown.
Solution: Added `ClearPhysicsImpactEventQueue1337`, `TryAcquirePhysicsRuntimeClearLocks1337`, and `ReleasePhysicsRuntimeClearLocks1337`. Root reset now clears impact and runtime culling lanes under scoped compaction-aware Vault write locks and releases them in `finally`.
Rejected Alternatives: Folding root reset into the Shinobu37 clear-lock wrapper. The root manager owns separate impact/AUP/result/telemetry lanes, so coupling it to Shinobu37 reset would lock unrelated buffers and hide ownership. A hard failure on lock contention was also rejected for teardown; indexer fallback remains guarded and best-effort.
Scalability potential: Low devices with many debris/weed bodies avoid reset lock churn; Middle/High/Ultra preserve the same culling truth and wake behavior. Quality scaling remains continuous and does not alter reset semantics.
Hardware Impact: 0 us steady-frame cost. Cold reset effect: replaces per-slot write-lock attempts across root physics lanes with one scoped lock set when available.

Problem: `RefreshTrackedBodies` ran every `FixedTick` and wrote `_lastValidPositions[i]` through the `VaultBufferBinding` indexer for every finite tracked body. That meant one write-lock acquire/release attempt per body in the fixed-step owner loop.
Solution: `RefreshTrackedBodies` now acquires `_lastValidPositions` once per owner-phase tick, writes through the resolved `NativeArray<float3>`, and releases in `finally`. If the lock cannot be acquired, the route fails closed by leaving last-valid position publication unchanged for that tick and increments culling contention telemetry. Null-body cleanup is deferred rather than running structural lane removal while the last-position lock is held.
Rejected Alternatives: Keeping per-body indexer writes because each setter is safe. Safe per write is still too expensive and weak as a transaction in a 50Hz loop. Holding the lock while calling `RemoveTrackedBodyAt` was rejected because structural lane swaps must stay outside the scoped Vault write-lock window.
Scalability potential: Low devices avoid O(N) lock churn in the fixed-step physics owner; Middle/High/Ultra keep the same AUP truth and can spend saved frame budget on larger near-field physics radius through `GlobalQualityWeight`.
Hardware Impact: Static estimate removes up to one write-lock acquire/release pair per tracked body per fixed tick. At 2048 tracked bodies and 50Hz, this removes up to 102400 lock attempts per second from the owner-thread path. Exact microseconds require Unity Profiler capture.

Problem: `PhysicsApplySystem` event/force payload DTOs used ushort, uint, and ulong fields as tail padding after semantic byte fields. The ABI size was correct, but this violated the byte-padding proof required by the ARM64 layout mandate.
Solution: Replaced tail padding in `ForcePacket`, `AcousticPingEvent`, `AcousticImpulseEvent`, `LargeAcousticImpulseEvent`, and `RemovedPhysicsEventPayload` with explicit private byte fields at every padding offset. Added `PHYSICS_APPLY_LAYOUT_AUDIT_1337_RERUN28.json` with byte offset maps.
Rejected Alternatives: Treating wide padding as equivalent because field offsets already matched. The mandate explicitly requires private byte padding, and wide padding masks field-order regressions.
Scalability potential: Low, Middle, High, and Ultra keep identical packet/event ABI sizes and semantic offsets. The fix hardens deterministic payload layout without changing quality behavior.
Hardware Impact: 0 us claimed. Runtime stride is unchanged; the gain is removal of an ARM64 proof defect before it reaches player devices.

Problem: The existing layout validator did not cover `PhysicsApplySystem` DTOs, so the byte-padding fix would remain a manual, non-enforced claim.
Solution: Expanded `PhysicsCullingLayout1337.Validate` to check sizes and key offsets for `ForcePacket`, `AcousticPingEvent`, `AcousticImpulseEvent`, `LargeAcousticImpulseEvent`, and `RemovedPhysicsEventPayload` using `UnsafeUtility.SizeOf` and `UnsafeUtility.GetFieldOffset`.
Rejected Alternatives: Relying on chat/report-only offset maps. The project needs a cold proof artifact that runs in editor validation.
Scalability potential: No runtime simulation path changes. All tiers get the same validator guard before content or code changes ship.
Hardware Impact: Editor/cold validation only; no fixed-frame cost.

Problem: `ApplyKinematicWeldSnap` applied `AddForce(correction / FixedStepSeconds, ForceMode.VelocityChange)` and then immediately set the body kinematic, disabled gravity, zeroed velocities, snapped pose, and called `Sleep`. That impulse is discarded by the same method and bypasses the deferred force packet route for no useful player-visible effect.
Solution: Removed the correction impulse and its fixed-step constant. The weld snap remains immediate because BaseModule and PlayerBuilder placement contracts expect accepted snaps to set the final transform without waiting for a deferred physics tick.
Rejected Alternatives: Deferring the entire weld snap through `ForcePacket`; this would break placement feedback and still cannot represent `useGravity/isKinematic` state in the current packet DTO. Keeping the impulse as a realism gesture was rejected as wasted PhysX work.
Scalability potential: Low devices avoid needless PhysX wake/force work during construction snaps; Middle/High/Ultra keep exact placement behavior and spend CPU on visible near-field physics instead.
Hardware Impact: Removes one direct `AddForce` call from every non-kinematic weld snap. Exact microseconds require Unity Profiler capture; expected effect is small but structurally clean.

Problem: `PhysicsApplySystem.PostFixedTick` still allowed the force-packet swap and validation path to call `EnsureForcePacketBuffers` / `EnsureValidationBuffers`, which can reach `IDataVault.EnsureGenerationHandle` from a hot fixed-step phase.
Solution: Replaced the swap with `TrySwapForcePacketBuffers`, which resolves only existing Vault handles and fails closed by clearing front/back counts. `ScheduleFrontPacketValidation` no longer ensures validation buffers; it uses existing handles only.
Rejected Alternatives: Keeping a hot ensure because the buffers are normally already created. The defect is exactly the abnormal path during DataVault hotswap, allocation lock, or boot ordering; hot paths must fail closed rather than grow native storage.
Scalability potential: Low devices avoid surprise Vault growth or allocation-lock contention during fixed-step physics; Middle, High, and Ultra retain the same force-packet capacity and validation behavior.
Hardware Impact: Static effect removes potential native buffer ensure/growth from `PostFixedTick`. Normal-frame cost is unchanged except for a few scalar handle checks; exact microseconds require Unity Profiler capture.

Problem: Force packet back/front/validation/mask buffers are Vault-owned, but enqueue, flush clear, validation-copy, and clear-queued routes wrote raw `NativeArray` views without `TryAcquireWriteLock`.
Solution: Added typed force-packet and byte-buffer write-lock helpers that call `IDataVault.TryAcquireWriteLock`, validate existing handle/capacity, and release in `finally`. The hot enqueue path now mutates `PhysicsForceCommandBack` only under the compaction-aware writer lock.
Rejected Alternatives: Using the old `TryGetExistingVaultBuffer` because it is generation-checked. Generation-checking proves identity, not mutation ownership, compaction fence, or active writer exclusion.
Scalability potential: All tiers keep the same packet layout and capacity. Low devices get safer fail-closed behavior during contention; higher tiers can continue to spend saved culling budget on richer visible physics without changing authority routes.
Hardware Impact: Adds one writer-lock acquire/release around each force-packet enqueue and scoped locks around validation/flush transactions. This is intentional correctness cost; it replaces unguarded Vault writes that could break compaction safety. Runtime measurement still requires Profiler capture.

Problem: `ValidateForcePacketsJob` was scheduled against Vault-backed validation buffers without registering the active handle with `H8Memory`.
Solution: `ScheduleFrontPacketValidation` now calls `H8Memory.RegisterActiveJob(OwnerSystemId, _packetValidationHandle)` immediately after scheduling. Normal nonblocking completion and forced validation-buffer release clear the active job with `RegisterActiveJob(default)`.
Rejected Alternatives: Treating the job as too small to register. The job owns native views across a dispatcher boundary, so the native memory sentinel needs the same evidence path as the culling jobs.
Scalability potential: No gameplay or quality behavior change. Low through Ultra get the same packet validation path with stronger leak/fence telemetry.
Hardware Impact: One static registry update per validation schedule and one clear on completion. Expected cost is below profiler noise; proof remains static until Unity Profiler capture.

Problem: Collider discovery used small `List<T>` scratch capacities: 4 for mesh/sleep collider registration and 8 for submarine hull modifiable-contact arming. A normal compound Rigidbody or submarine hull can exceed those capacities and force managed List growth during registration/first-arm.
Solution: Raised mesh, sleep, and submarine collider discovery scratch capacities to 64 cold slots while keeping semantic cached collider limits unchanged.
Rejected Alternatives: Rewriting discovery to a custom transform traversal in this pass. That risks changing multi-collider GameObject behavior and needs wider scene validation. The capacity patch is narrow, behavior-preserving, and removes the realistic allocation case.
Scalability potential: Low devices avoid first-contact/first-registration GC spikes for compound debris and submarine hulls. Middle/High/Ultra retain the same culling sleep semantics; richer collider behavior still needs a separately profiled design.
Hardware Impact: Cold managed memory increases by small fixed List backing arrays. In exchange, normal compound discovery avoids runtime List growth. Exact GC proof requires Unity Profiler/GCMonitor.

Problem: Force-packet flush and validation used chained write-lock acquisition, then released buffers unconditionally on a partial acquisition failure. `ReleaseWriteLock` is internally guarded, but the proof did not show a one-to-one acquire/release route.
Solution: Replaced the chained expressions with explicit `frontLocked`, `validationPacketsLocked`, and `validationMaskLocked` flags. Failure and finally paths release only locks whose matching acquire succeeded.
Rejected Alternatives: Relying on `ReleaseWriteLock` no-op behavior. That is defensive implementation detail, not a strong synchronization proof.
Scalability potential: All quality tiers keep the same deferred force-packet path. Low devices get deterministic fail-closed contention behavior; higher tiers keep the same safe validation path while spending physics budget on visible bodies.
Hardware Impact: Adds a few scalar booleans in fixed-step force-packet transactions. Prevents incorrect lock-release accounting; exact runtime cost is below profiler noise and requires Unity Profiler capture.

Problem: `PhysicsApplySystem` force, torque, velocity, and pose packet routes called `GlobalPhysicsStateManager.RegisterTrackedBody` on every packet. For already tracked bodies this can rescan components, refresh collider caches, and write culling DTO state on a path that should only enqueue a packet.
Solution: Added `RegisterTrackedBodyIfMissing`, which uses the cached runtime manager and entity-id dictionary to no-op for already tracked bodies. PhysicsApplySystem packet routes now use this fast path.
Rejected Alternatives: Removing registration entirely from force packets. That would drop first-time bodies from culling ownership when a force is the first physics touch. Keeping full registration on every packet was rejected as hot-route structural work.
Scalability potential: Low devices avoid repeated component/collider registration work under force storms; Middle/High/Ultra preserve first-touch registration and can spend saved CPU on larger visible near-field physics.
Hardware Impact: Replaces repeated full registration for known bodies with one dictionary lookup and reference check. At N force packets per frame on already tracked debris, this avoids N component scans and DTO refreshes; exact microseconds require Unity Profiler capture.

Problem: Tracked-body registration and lane removal still published related native lanes (`RigidbodyLastValidPositions`, `PhysicsCullingDTO`, `FrozenVelocityDTO`, `StateAge`) through independent indexer-lock writes. If contention or compaction appeared mid-sequence, native culling state could be partially published.
Solution: Added `TryAcquirePhysicsTrackedBodyLaneMutationLocks1337` and `ReleasePhysicsTrackedBodyLaneMutationLocks1337`. New-body registration and lane swap/removal now acquire one scoped lock set and release in `finally`; failure rolls back registration or fails closed without structural mutation.
Rejected Alternatives: Letting each `VaultBufferBinding` indexer acquire its own write lock. It is safe per individual write but too weak for a multi-lane body identity transaction. Holding persistent native views was rejected; the lock set is method-local and never crosses a frame.
Scalability potential: All tiers preserve one body identity route. Low devices fail closed during compaction instead of publishing half a body lane; Middle/High/Ultra keep deterministic culling/wake behavior while quality only changes radius/cadence.
Hardware Impact: Registration/removal are structural paths, not per-body Burst jobs. The steady-state cost is 0 us; structural mutation now pays one scoped lock set instead of multiple independent indexer locks.

Problem: Origin-shift prepare/commit, safe-teleport reset, AUP jitter correction, and NaN recovery still wrote `RigidbodyLastValidPositions` and sometimes `RigidbodyAUPs` through per-indexer Vault lock fallback.
Solution: Added `TryAcquireTrackedBodyPositionPublishLocks1337` and `ReleaseTrackedBodyPositionPublishLocks1337`. These owner-phase recovery routes now acquire optional scoped write locks once, write through resolved `NativeArray` views when locks are held, and fail closed by skipping native publication if compaction or contention blocks the lock.
Rejected Alternatives: Keeping per-indexer fallback because these paths are rare. Rare structural/recovery paths are exactly where compaction and origin-shift fences happen, so per-write lock churn is still a correctness smell. Holding locks across asynchronous work was not introduced.
Scalability potential: Low devices avoid lock churn during origin shifts and safe teleports; Middle/High/Ultra keep exact AUP truth publication while quality remains limited to culling radius/cadence.
Hardware Impact: Hot steady-state cost is 0 us. Cold origin-shift/safe-teleport routes replace up to one write-lock attempt per body per lane with one scoped lock pair. Exact microseconds require Unity Profiler capture.

Problem: `RigidbodyAUPs` was not part of tracked-body lane mutation. New tracked bodies had no initial AUP publication, and lane removal/swap moved `LastValidPositions` but left `RigidbodyAUPs` stale until the next culling schedule.
Solution: Extended `TryAcquirePhysicsTrackedBodyLaneMutationLocks1337` to lock `RigidbodyAUPs`, wrote initial exact AUP on registration, moved/cleared AUP lane data during removal, and blocked existing-body DTO refresh while a culling job is scheduled.
Rejected Alternatives: Waiting for the next slow culling schedule to repair the AUP lane. Rollback/hash/debug readers can consume the lane between registration/removal and the next slow tick, so stale AUP is a real data sovereignty defect.
Scalability potential: All quality levels preserve one body identity lane and one exact AUP lane; weak devices fail closed under contention rather than exposing stale coordinates, while stronger devices keep identical truth and spend budget only on visible physics radius.
Hardware Impact: Structural mutation now locks one additional `double3` Vault lane and writes one extra 24B AUP value on registration/removal. Steady-state culling job cost is unchanged.

Problem: The compile gate briefly passed, but the Unity-generated `Assembly-CSharp.csproj` was absent from `C:\hades\Hecton8`.
Solution: Ran one build attempt and recorded the objective MSB1009 failure instead of fabricating compile proof. `C:\hades\hades.sln` still references `Hecton8\Assembly-CSharp.csproj`, so project-file regeneration is required before a valid compile can be recorded.
Rejected Alternatives: Building the full solution with missing project references, editing generated project files by hand, or reporting static parser success as a compile.
Scalability potential: N/A build infrastructure issue.
Hardware Impact: Failed immediately; no long CPU load or repeated build loop.

Problem: The scoped position/AUP publication fix introduced a nested-lock escape route. `PrepareTrackedBodiesForOriginShiftInternal`, `CommitTrackedBodiesForOriginShiftInternal`, and `ResetTrackedBodiesForSafeTeleportInternal` acquired `RigidbodyLastValidPositions` / `RigidbodyAUPs` write locks, then called `TryRemoveNullTrackedBodyAt` for null bodies. If removal proceeded, `RemoveTrackedBodyAt` re-entered tracked-body lane locks and released the same `VaultBufferBinding` write-lock state before the outer loop finished.
Solution: Null bodies in these scoped publication phases now only set `_deferredNullTrackedBodyCleanup = true`; actual structural removal is left to `RemoveNullTrackedBodiesOutsidePhysicsCullingLocks` after the lock window.
Rejected Alternatives: Making `VaultBufferBinding` reentrant or keeping nested removal because it is rare. Reentrant counted locks would expand the local binding contract and still allow structural lane swaps in a phase that only intends position publication.
Scalability potential: Low devices get deterministic fail-closed cleanup without lock churn during origin-shift/safe-teleport. Middle/High/Ultra keep the same AUP truth lane and culling behavior; quality remains a radius/cadence scalar, not a synchronization mode.
Hardware Impact: 0 us steady-frame cost. Cold recovery/origin-shift routes avoid premature lock release and defer structural cleanup by at most one owner tick.

Problem: `ResetTrackedBodiesForSafeTeleportInternal` wrote LastValid/AUP lanes and teleported tracked bodies without the explicit culling-job mutation barrier used by origin-shift prepare/commit/finalize.
Solution: Added `CompletePhysicsCullingJobForStateMutationBarrier(discardResults: true)` at the start of safe-teleport reset before scoped position/AUP write locks are acquired.
Rejected Alternatives: Relying on `Physics.simulationMode = Script` from `HectonFloatingOrigin`. That pauses PhysX stepping, not an already scheduled Burst culling job reading Vault lanes.
Scalability potential: All quality levels use the same safe-teleport state reset. Weak devices avoid data races under slow jobs; high-tier devices keep identical behavior and spend performance budget on larger visible physics radius.
Hardware Impact: Cold teleport path may wait for an active culling job. No steady-frame cost. This is a synchronization correctness fix, not a micro-optimization.

Problem: Root runtime clear and Shinobu37 culling clear helpers acquired optional Vault write-locks but released every possible lane unconditionally. The implementation was functionally defended by `VaultBufferBinding.ReleaseWriteLock()` returning false when no lock was held, but the synchronization proof still depended on a hidden no-op path rather than exact acquisition accounting.
Solution: Added uint acquisition masks to `TryAcquirePhysicsRuntimeClearLocks1337` and `TryAcquirePhysicsCullingClearLocks1337`. Each successful lane acquisition sets one bit; both normal and failure cleanup release only those bits, then zero the mask on failure.
Rejected Alternatives: Keeping unconditional release because it is currently guarded. Defensive no-op release is not a strong lock proof and makes future changes to `VaultBufferBinding` risky.
Scalability potential: Low devices get deterministic teardown/reset lock accounting under contention and compaction. Middle/High/Ultra keep the same reset semantics and continuous quality behavior; no gameplay truth or DTO layout changes.
Hardware Impact: 0 us steady-frame claim. Cold reset adds only scalar bit operations and removes misleading release attempts across lanes that were never locked. Exact runtime measurement still requires Unity Profiler capture.

Problem: The clear/reset paths still failed open. `ClearRuntimeState`, `ClearPhysicsImpactEventQueue1337`, and `ClearShinobu37PhysicsCullingState` incremented contention when scoped Vault write-lock acquisition failed, then continued clearing native lanes through `VaultBufferBinding` indexer fallback writes.
Solution: Runtime clear and Shinobu37 culling clear now skip all Vault-backed clear loops unless the scoped write-lock set is acquired. Impact event queue clear returns immediately on lock failure. Local scalar state is still cleared where it cannot mutate Vault memory.
Rejected Alternatives: Retaining fallback per-indexer clearing because reset/shutdown is cold. Cold reset is a compaction-heavy phase, so continuing writes after failed lock acquisition is the exact failure mode the lock protocol exists to prevent.
Scalability potential: Low devices fail closed during shutdown/reset contention instead of doing hundreds of lock attempts under compaction pressure. Middle/High/Ultra preserve the same reset semantics when locks are available; quality remains continuous and unrelated to clear authority.
Hardware Impact: 0 us steady-frame cost. Cold contention paths now avoid O(N) fallback lock churn; exact reset timing requires Unity Profiler capture.

Problem: `SetPhysicsCullingTuning` and `GenerateMockPhysicsBodies` were cold/debug routes, but both mutated Vault-backed culling lanes without a scoped write-lock transaction.
Solution: `SetPhysicsCullingTuning` now acquires `_physicsCullingTuning` before writing and releases in `finally`. Mock body generation now discards any active culling job, acquires DTO/frozen velocity/state age/state snapshot locks as one scoped transaction, and fails closed if any lock is unavailable.
Rejected Alternatives: Treating editor/debug routes as exempt. These buffers live in `GlobalDataVault`; cold callers can still collide with compaction or active jobs, so they need the same ownership proof as runtime routes.
Scalability potential: Low devices avoid debug/test wakeups causing unsafe compaction writes while profiling culling load. Middle/High/Ultra can still generate mock culling bodies for stress work without changing gameplay truth.
Hardware Impact: Mock generation pays four write-lock acquisitions once per manual generation. No normal frame cost.

Problem: The live impact event queue still had a multi-step Vault mutation outside one scoped write-lock transaction. `EnqueueImpactEvent` wrote `_impactEvents[writeIndex]`, and `FlushImpactEvents` read, cleared, advanced cursors, decremented queue count, and published impact signals from the same route. The binding indexer is individually guarded, but the queue transaction was not provably compaction-safe as one owner phase.
Solution: Added a fixed cold `PhysicsImpactEventData[256]` flush scratch. `EnqueueImpactEvent` now acquires `_impactEvents.TryAcquireWriteLock`, writes through the resolved `NativeArray<PhysicsImpactEventData>`, and releases in `finally`. `FlushImpactEvents` now drains event DTOs into the preallocated scratch under the same scoped lock, releases the Vault lock, then publishes `SignalBus<ImpactSignal>` and `PhysicsEvents.TryNotifyImpact` outside the lock.
Rejected Alternatives: Publishing events while holding the Vault write lock was rejected because callbacks and signal routes are external authority work and should not extend the compaction fence window. Per-event lock acquire/release was also rejected because a burst of 256 impact events would churn the lock 256 times during a frame. Leaving indexer fallback in place was rejected because it proves only individual writes, not queue transaction ownership.
Scalability potential: Low devices avoid lock churn and compaction-risky queue publication during collision bursts. Middle, High, and Ultra keep the same impact audio/wake semantics while the culling budget remains available for visible near-field physics instead of emergency synchronization.
Hardware Impact: Steady collision cost changes from repeated binding-indexer write/read/clear transactions to one enqueue lock and one drain lock per flush. The new scratch is 256 fixed unmanaged DTO slots inside a managed array allocated cold with the manager. No per-frame managed allocation is introduced; exact microseconds require Unity Profiler capture.

Problem: A compile proof after rerun 35 would be useful, but the host build gate is red.
Solution: Did not run `dotnet build`. Gate sample reported CPU=100%, dotnet=0, csc=0, with Python/Codex/Code consuming the host. Static Roslyn scanners, direct token scans, JSON parse checks, `git diff --check`, and a cryptographic verification hash were recorded instead.
Rejected Alternatives: Launching MSBuild under saturated CPU. That violates the project rule forbidding builds under >50% CPU and would interfere with other agents.
Scalability potential: N/A runtime; this preserves multi-agent host stability.
Hardware Impact: Avoided a heavy compile while the workstation was saturated.

Problem: `PhysicsApplySystem.FlushValidatedFrontBuffer` acquired the front force-packet and validation-mask Vault write-locks, then kept both locks while executing Unity `Rigidbody` calls, critical acoustic impulse routing, and proxy-light side effects.
Solution: Added fixed cold `ForcePacket[64]` and `byte[64]` apply snapshots. The flush now copies validated packet DTOs into those snapshots and clears the Vault front buffer while locks are held, releases both locks in `finally`, and only then runs `WakeUp`, `MovePosition`, velocity writes, `AddForce*`, `AddTorque`, AUP cache updates, and critical acoustic publication.
Rejected Alternatives: Keeping the original in-lock application path because the buffers are small. The size is bounded, but Unity API and event routes are external work and can extend the compaction fence window. Allocating a fresh array per flush was rejected because it would create managed GC pressure in a fixed-step route.
Scalability potential: Low devices get shorter compaction/write-lock windows during dense force-packet flushes. Middle, High, and Ultra preserve identical force truth and can spend physics budget on more visible near-field bodies rather than synchronization stalls.
Hardware Impact: Adds two fixed cold arrays: 64 `ForcePacket` slots and 64 bytes. Steady flush adds a bounded DTO copy of up to 64 packets, but removes Unity/API/event work from the Vault lock window. Exact microseconds require Unity Profiler capture; correctness gain is shorter lock hold time and safer compaction behavior.

Problem: Rerun 36 needed proof that the new flush lock window no longer covers Unity/API side effects.
Solution: Ran a targeted token audit over `FlushValidatedFrontBuffer`: the region from method start through first front-buffer release contains 0 hits for `body.`, `WakeUp`, `MovePosition`, `MoveRotation`, `AddForce`, `AddTorque`, `EmitCriticalAcousticImpulse`, `SignalBus`, `PhysicsEventBus`, or proxy-light routes. The expected Unity/API tokens appear only after release.
Rejected Alternatives: Manual visual review only. A line-range token audit is cheaper and produces a reproducible proof artifact in `Docs/Reports/PHYSICS_APPLY_FRONT_BUFFER_LOCK_AUDIT_1337_RERUN36.json`.
Scalability potential: Same as above; no gameplay truth or DTO layout changed.
Hardware Impact: Static audit only. No runtime cost.

Problem: A compile proof after rerun 36 would be useful, but the host build gate is still red.
Solution: Did not launch `dotnet build`. Gate samples reported CPU=87% and then 100%, dotnet=0, csc=0. Static Roslyn scanners, direct token scans, lock-region token audit, `git diff --check`, and proof hashes were recorded instead.
Rejected Alternatives: Launching MSBuild under >50% CPU. That violates AGENTS and would interfere with the active multi-agent workspace.
Scalability potential: N/A runtime; preserves workstation stability.
Hardware Impact: Avoided heavy compile load on an already saturated host.

Problem: `ScheduleFrontPacketValidation` trusted `_frontCount` for front/validation NativeArray copy length and `ValidateForcePacketsJob` schedule length. If `_frontCount` is corrupted or set by a route that missed the upstream clamp, the validation phase can read/write beyond the fixed `MaxQueuedPackets` buffers or schedule a job over invalid slots.
Solution: Added a local `queuedCount` snapshot and `validationCount = math.min(queuedCount, MaxQueuedPackets)` before any NativeArray access. Non-positive counts reset `_frontCount` and return fail-closed. The copy loop and validation job schedule now use `validationCount`, `_frontCount` is mirrored to that count before schedule, and clip warning publication runs after all scoped Vault write-locks are released.
Rejected Alternatives: Relying only on `ClampFrontBufferCountToCapacity()` earlier in the fixed-step pipeline. Upstream clamp is useful, but the validation phase is the last owner before Burst scheduling and must defend its own buffer boundaries.
Scalability potential: Low devices fail closed under force-packet corruption without a safety exception or validation job overrun. Middle, High, and Ultra keep identical force truth and bounded 64-packet validation; quality scaling remains a physics visibility/radius concern, not a packet-buffer layout switch.
Hardware Impact: Adds one integer min and one branch on the validation scheduling route. It prevents an out-of-bounds NativeArray access and avoids catastrophic job failure; exact microseconds require Unity Profiler capture.

Problem: A compile proof after rerun 37 would be useful, but the host build gate is red again.
Solution: Did not launch `dotnet build`. Gate sample reported CPU=97%, dotnet=0, csc=0. Recorded fresh Roslyn native-field, zero-GC hotpath, branch, direct-token, scoped diff, and validation-count audit artifacts instead.
Rejected Alternatives: Launching MSBuild under >50% CPU or retrying until the workstation clears. That violates AGENTS and would interfere with other active agents.
Scalability potential: N/A runtime; preserves multi-agent host stability.
Hardware Impact: Avoided heavy compile load on a saturated workstation.

Problem: `PhysicsApplySystem.FreezeToxicBody` recovered a toxic Rigidbody by writing `body.position` before setting `isKinematic=true` and `detectCollisions=false`, then slept it. If the toxic vector came from rotation, the body rotation could remain non-finite.
Solution: Reordered the fault path: zero velocities, set kinematic, disable collisions, write recovered position only if finite, replace non-finite rotation with `Quaternion.identity`, publish transform, then sleep. This matches the local safe-teleport doctrine: isolate the Rigidbody from broadphase before teleport/sanitization work.
Rejected Alternatives: Calling the wider `GlobalPhysicsStateManager.TeleportBodyWithoutBroadphaseImpulse` helper from this fault path. That helper can restore previous collision/kinematic state and queue velocity through the force router; toxic recovery needs fail-closed quarantine, not a normal teleport restore.
Scalability potential: Low devices fail closed without a broadphase impulse or NaN rotation leak; Middle/High/Ultra keep the same recovery truth and spend quality budget only on visible physics radius/cadence.
Hardware Impact: Fault-path only. Adds one quaternion read, one finite check, and one native transform publish when a Rigidbody is already toxic; 0 steady-frame cost.

Problem: `ValidateForcePacketsJob` used `mode < ForceMode.Force || mode > ForceMode.VelocityChange` as validity proof. Unity `ForceMode` values are not a contractually safe contiguous range, and the project has many `ForceMode.Acceleration` callers for buoyancy, towing, movement, and collapse physics.
Solution: Replaced numeric range validation with explicit branchless membership over `Force`, `Acceleration`, `Impulse`, and `VelocityChange`; validity now writes through `math.select`. The only remaining job `if` is a fail-closed NativeArray length guard.
Rejected Alternatives: Keeping range validation and assuming enum ordering. That risks silently dropping valid acceleration packets in the validation job. A `switch` was rejected because it would reintroduce decision branches in the job body.
Scalability potential: Low devices keep cheap acceleration-based buoyancy/ambient forces instead of misclassifying them and causing retry/wake churn; Middle/High/Ultra preserve exact force semantics and can spend saved synchronization budget on visible near-field bodies.
Hardware Impact: Removes two job decision branches and replaces them with scalar boolean masks. Exact microseconds require Unity Profiler capture; correctness gain is that `ForceMode.Acceleration` packets no longer fail validation.

Problem: A compile proof after rerun 38 would be useful, but the host build gate is red.
Solution: Did not launch `dotnet build`. Gate sample reported CPU=65%, dotnet=1, csc=0. Recorded fresh native-field, zero-GC hotpath, branch, direct-token, JSON-parse, scoped diff, and recovery/force-mode audit artifacts instead.
Rejected Alternatives: Launching MSBuild while another dotnet process exists and CPU is above 50%. That violates AGENTS and interferes with other active agents.
Scalability potential: N/A runtime; preserves multi-agent host stability.
Hardware Impact: Avoided heavy compile load while the workstation was already busy.

Problem: `QueuePoseSet` cached the requested `sanitizedPosition` into `_lastFiniteBodyAups` before the pose command was enqueued, validated, or applied.
Solution: Cache the current `body.position` during enqueue and keep target-position AUP publication in the existing successful apply path after `MovePosition`.
Rejected Alternatives: Leaving target pre-cache because pose packets are critical. Critical packets can still saturate, fail validation, or be skipped because the body disappears; fallback coordinates must reflect actual body truth, not an uncommitted command.
Scalability potential: Low devices under packet pressure fail closed with the last real body AUP; Middle/High/Ultra preserve exact teleport/pose truth once the command applies, without changing quality or DTO layout.
Hardware Impact: 0 us steady-frame claim. The change replaces one AUP cache input with another existing `Vector3` read and prevents later recovery/wake fallback work from using a target pose that never executed.

Problem: A compile proof after rerun 39 would be useful, but the host build gate remains red.
Solution: Did not launch `dotnet build`. Gate sample reported CPU=82%, dotnet=0, csc=0. Recorded fresh native-field, zero-GC hotpath, branch, direct-token, JSON-parse, scoped diff, and pose AUP cache audit artifacts instead.
Rejected Alternatives: Launching MSBuild under >50% CPU or treating static parser success as a compile.
Scalability potential: N/A runtime; preserves multi-agent host stability.
Hardware Impact: Avoided heavy compile load on a saturated workstation.

Problem: `IsFiniteQuaternion` only checked component finiteness. A zero-length quaternion is finite but invalid for Rigidbody rotation and could bypass `EnsureFiniteBodyState` or be re-written in `FreezeToxicBody`.
Solution: Require finite `lengthsq` and `lengthsq > MinMagnitudeSq` in `IsFiniteQuaternion`, matching the existing `TryNormalizeQuaternion` guard.
Rejected Alternatives: Leaving zero-length rotations to Unity normalization. The recovery path is explicitly for corrupted physics state; it must fail closed before handing the value back to PhysX.
Scalability potential: Low devices avoid repeated bad-state recovery churn; Middle/High/Ultra preserve exact pose truth when rotations are valid and fall back to identity only for poisoned state.
Hardware Impact: Adds one `lengthsq` and finite scalar check in toxic-state validation paths. Normal hot force application cost is unchanged; exact microseconds require Unity Profiler capture.

Problem: A compile proof after rerun 40 would be useful, but the host build gate is red again.
Solution: Did not launch `dotnet build`. Gate sample reported CPU=65%, dotnet=7, csc=0. Recorded fresh native-field, zero-GC hotpath, branch, direct-token, JSON-parse, scoped diff, and quaternion validity audit artifacts instead.
Rejected Alternatives: Launching MSBuild while dotnet processes already exist and CPU is above 50%.
Scalability potential: N/A runtime; preserves multi-agent host stability.
Hardware Impact: Avoided heavy compile load while the workstation was busy.

Problem: `PhysicsForceRouter.IsFiniteQuaternion` had a separate finite-rotation predicate from `PhysicsApplySystem.IsFiniteQuaternion` and accepted `lengthsq == Infinity` as long as the comparison against epsilon was true.
Solution: Router validation now routes through a helper that requires finite components, finite `lengthsq`, and `lengthsq > QuaternionMagnitudeEpsilonSq`.
Rejected Alternatives: Trusting callers to normalize or clamp before reaching the force router. The router is the public packet ingress path, so it must reject poisoned orientation values at the boundary.
Scalability potential: Low devices avoid repeated invalid-pose correction churn; Middle/High/Ultra keep exact valid pose truth without adding a new solver or quality branch.
Hardware Impact: Adds one scalar `lengthsq` finite check to cold/ingress quaternion validation. No new native buffers, jobs, or managed allocations.

Problem: `PhysicsForceRouter.ApplyKinematicWeldSnap` accepted finite non-zero quaternions and wrote them directly to `Rigidbody.rotation`; non-unit orientation data could enter PhysX during weld snap.
Solution: Added router-local `TryNormalizeQuaternion`, normalized and sign-canonicalized accepted rotations before assignment, and made `IsFiniteQuaternion` delegate to that helper.
Rejected Alternatives: Relying on Unity/PhysX implicit quaternion normalization. This is a deterministic physics boundary and should write one canonical rotation, not engine-dependent cleanup.
Scalability potential: Low devices avoid unstable broadphase/contact correction from malformed weld rotations. Middle/High/Ultra preserve the same weld snap semantics; quality scaling remains radius/cadence, not rotation validity.
Hardware Impact: Weld-snap path pays one reciprocal square root only when a snap is requested. No steady culling job cost; exact microseconds require Unity Profiler capture.

Problem: A compile proof after rerun 42 was attempted and failed, but the error source is outside the Physics culling domain.
Solution: Recorded the failure as a foreign dependency blocker: `Assets\Candice AI for Games\Scripts\Libs\Candice Save System\Overrides\CandiceSQLiteProvider.cs` cannot resolve `Mono.Data` / `SqliteDataReader`. Did not edit Candice, MapMagic, or generated package files.
Rejected Alternatives: Patching third-party save-system references from the Physics culling assignment, or reporting static gates as a green compile. Both would be dishonest and would interfere with other owners.
Scalability potential: N/A runtime; build proof is blocked outside the domain.
Hardware Impact: One gated build consumed about 24.6 seconds wall time before failing on external dependencies.

Problem: Pose packet validation, toxic recovery, weld snap, origin-shift teleport, and added-mass baseline had split quaternion contracts. Several routes proved only finite components or non-zero magnitude, and some wrote the original quaternion back to Unity after validation instead of writing a normalized value.
Solution: Unified these routes around finite `lengthsq` plus reciprocal-square-root normalization. `ValidateForcePacketsJob` now rejects non-finite packed pose length. `FreezeToxicBody`, `ApplyKinematicWeldSnap`, `TeleportBodyWithoutBroadphaseImpulse`, origin-shift snapshots, NaN recovery, and added-mass inertia rotation restoration now write only sanitized normalized quaternions or fail closed to identity. Weld snap now disables `detectCollisions` before direct Rigidbody pose mutation, publishes the transform, then restores the previous collision flag.
Rejected Alternatives: Trusting Unity/PhysX to normalize malformed quaternions was rejected because these are deterministic physics boundaries and fault-recovery paths. Leaving weld snap collisions enabled during direct pose mutation was rejected because the local physics mandate allows transform mutation only while the Rigidbody is kinematic and collisions are disabled. Adding a new solver or queued job for construction snap was rejected because this is a cold placement route and the cheap deterministic snap is sufficient.
Scalability potential: Low devices avoid contact/broadphase correction churn from malformed rotations or collision-enabled teleports. Middle, High, and Ultra preserve exact valid pose truth; saved CPU remains available for visible near-field physics and higher culling radius/cadence instead of recovery work.
Hardware Impact: Adds one `lengthsq` and one `rsqrt` on cold/exceptional snap, teleport, recovery, and baseline routes. The Burst packet validation path adds one scalar finite check for SetPose packets and no managed allocation. Exact microseconds require Unity Profiler capture; expected steady culling-job impact is below measurement noise.

Problem: A compile proof after rerun 43 is still unsafe to run under the multi-agent host rules.
Solution: Did not launch `dotnet build`. Gate after a 30s wait reported CPU=96% and `VBCSCompiler` active. Recorded static source gates and scoped diff proof instead.
Rejected Alternatives: Running MSBuild under >50% CPU or while compiler infrastructure is active. That violates AGENTS and would interfere with other agents.
Scalability potential: N/A runtime; preserves workstation stability.
Hardware Impact: Avoided a heavy compile on a saturated host.

Problem: Targeted physics wake flush held scoped Vault write-locks while restoring Unity `Rigidbody` and `Collider` state.
Solution: Split targeted wake processing into two phases. The queue phase acquires only wake mirror/count locks, copies request DTOs into a fixed 64-entry cold scratch array, clears the native queue, and releases in `finally`. The apply phase runs `ProcessTargetedPhysicsWakeRequest` after lock release, so Unity side effects no longer execute inside the queue transaction.
Rejected Alternatives: Keeping frozen velocity, state-age, and culling DTO locks around the whole wake flush was rejected because `RestoreAllPhysicsCullingState` mutates Unity physics objects. A full native/Unity split of every restore helper was deferred because it would widen the patch surface; the immediate defect was the long scoped lock spanning Unity API calls.
Scalability potential: Low devices avoid compaction stalls and lock contention spikes during wake storms. Middle, High, and Ultra preserve the same wake truth and can spend saved sync budget on larger continuous culling radius/cadence.
Hardware Impact: Adds a bounded copy of at most 64 unmanaged request DTOs per wake flush. It removes a long lock-held Unity restore window; expected benefit appears under burst wake pressure, exact microseconds require Unity Profiler capture.

Problem: `PhysicsStateReporter.OnEnable` called `RegisterTrackedBody` after reporter creation, which can re-enter the full registration path immediately after `EnsureReporter` adds the component.
Solution: Changed the reporter to call `RegisterTrackedBodyIfMissing`, keeping the event relay alive while avoiding duplicate registration work for already tracked rigidbodies.
Rejected Alternatives: Removing reporter self-registration entirely was rejected because scene-enabled reporters still need to recover after domain reload or runtime enable. Leaving the full registration route was rejected because it can repeat cold collider/material cache work unnecessarily.
Scalability potential: Low devices avoid redundant cold registration spikes when many bodies are enabled. Higher tiers preserve the same tracking truth and reporter behavior.
Hardware Impact: Cold/enable-path only. Saves an O(registration) duplicate pass for already tracked bodies; no steady-frame cost.

Problem: Compile verification after rerun 44 produced a mixed result.
Solution: Full `Hecton8.slnx` build was stopped after a long timeout to avoid leaving a runaway build process. A narrower `Assembly-CSharp.csproj` build was then run under a green CPU/dotnet/csc gate and failed on existing third-party `CandiceSQLiteProvider.cs` errors: missing `Mono.Data` and `SqliteDataReader`. Build servers were shut down afterward.
Rejected Alternatives: Reporting the build as green, editing the Candice save-system dependency from the Physics assignment, or launching repeated builds under compiler-server residue. All three would be dishonest or cross-domain interference.
Scalability potential: N/A runtime; build proof remains blocked outside the physics culling domain.
Hardware Impact: One full-solution build was stopped after timeout; one targeted build consumed about 34 seconds and failed on non-Physics references.

Problem: `QueueTargetedPhysicsWakeRequest` treated a negative corrupted `PhysicsCullingCounter64.Value` as an unsigned overflow and rewrote the queue counter to capacity. That makes the next `FlushPhysicsTargetWakeRequests` believe all mirror slots are live, so stale targeted wake DTOs can be replayed.
Solution: Added an explicit fail-closed negative/capacity guard in the enqueue path and clamped the flush drain count to `[0, queueCapacity]`. Invalid counter state now clears the queue count to zero, sets the failure flag, and increments numeric contention telemetry without writing a mirror slot.
Rejected Alternatives: Relying on the existing `(uint)writeIndex >= (uint)capacity` guard. It prevents out-of-bounds writes but converts negative corruption into a full-queue replay. Clearing the entire mirror every bad enqueue was rejected because the count reset is sufficient to make stale slots unreachable, and the normal flush/clear paths already zero reachable slots.
Scalability potential: Low devices under wake storms avoid unnecessary Rigidbody/Collider restore work from stale DTO replay. Middle, High, and Ultra preserve the same targeted wake truth for valid requests while keeping the sync phase bounded by live request count.
Hardware Impact: Adds two integer comparisons in the targeted wake enqueue route and one integer clamp in flush. Steady-frame cost is below profiler resolution; avoided worst case is up to 64 stale targeted restore attempts in a corrupted wake frame.

Problem: Build proof after rerun 45 would interfere with the active multi-agent host.
Solution: Did not launch `dotnet build`. The first gate sample reported CPU=70% and `VBCSCompiler` PID 50784 active; the latest sample dropped to CPU=46% but the same compiler process remained active. Recorded Roslyn zero-GC and branch audits, token scans, `git diff --check`, and a verification hash instead.
Rejected Alternatives: Running MSBuild under CPU > 50% or with compiler infrastructure active. That violates AGENTS and can disturb other agents.
Scalability potential: N/A runtime; this preserves workstation stability.
Hardware Impact: Avoided a heavy compile on a saturated host.

Problem: `DispatchPhysicsCullingResults` still held scoped culling dispatch Vault locks while `ApplyPhysicsCullingCommand` executed Unity `Rigidbody` and `Collider` side effects.
Solution: Split dispatch into a bounded native snapshot phase and a Unity apply phase. The snapshot phase locks only changed indices/count plus awake/command/distance result lanes, copies live entries into fixed cold scratch arrays, clears the changed-count queue, and releases in `finally`. The apply phase performs `Sleep`, `WakeUp`, kinematic/collision changes, collider strip/restore, sleep signal publication, body-state writes, and telemetry after release.
Rejected Alternatives: Keeping culling DTO, state-age, frozen velocity, body telemetry, and frame telemetry locks around the whole dispatch. That made native compaction wait on Unity API calls. Fully rewriting every restore helper into native-command DTOs was rejected for this pass because the current bounded snapshot removes the lock-window defect without expanding the mutation surface.
Scalability potential: Low devices avoid compaction stalls during the slow culling sync window. Middle, High, and Ultra preserve identical sleep/wake truth while the saved lock budget can support larger continuous culling radius or denser near-field bodies.
Hardware Impact: Adds four fixed cold managed arrays and one bounded copy per changed body. Removes a long lock-held Unity API window; exact microseconds require Unity Profiler capture, but the critical risk was synchronization latency, not arithmetic cost.

Problem: Compile proof after rerun 46 is still unsafe under current host load.
Solution: Did not launch `dotnet build`. Gate sample reported CPU=63% with both `dotnet.exe` and `csc.exe` active. Recorded lock-region scan, Roslyn zero-GC audit, branch audit, token scans, `git diff --check`, and verification hash instead.
Rejected Alternatives: Running MSBuild under CPU > 50% or while compiler processes are active. That violates the multi-agent build throttle and risks interfering with other agents.
Scalability potential: N/A runtime; preserves workstation stability.
Hardware Impact: Avoided a heavy compile on a busy host.

Problem: After the dispatch lock split, body telemetry still acquired `_physicsCullingTelemetry` once per changed body through the `VaultBufferBinding` indexer.
Solution: Added `FlushPhysicsCullingDispatchTelemetry`. Dispatch now applies Unity side effects first, computes the current culled count, acquires the telemetry ring once, writes all valid changed-body telemetry entries in one scoped `try/finally` lock, then clears dispatch scratch. The telemetry lock region contains no Unity `Rigidbody` or `Collider` API calls.
Rejected Alternatives: Keeping per-body binding writes was rejected because a mass state-change frame can produce thousands of write-lock attempts. Re-acquiring the old broad dispatch locks was rejected because it would reintroduce Unity side effects inside native lock windows.
Scalability potential: Low devices avoid lock churn spikes when many far bodies change sleep state. Middle, High, and Ultra preserve the same telemetry truth with lower synchronization overhead, leaving budget for larger culling radius/cadence or denser near-field debris.
Hardware Impact: Replaces up to 2048 telemetry write-lock acquisitions with one scoped acquisition per dispatch. Added arithmetic is a bounded loop over the already snapshotted changed-body list. Exact microseconds require Unity Profiler capture.

Problem: Compile proof after rerun 47 is still unsafe under current host load.
Solution: Did not launch `dotnet build`. Gate sample reported CPU=100%; sampled process list showed no dotnet/csc process, but CPU rule alone blocks the build. Recorded Roslyn gates, token scans, lock-region proof, `git diff --check`, and verification hash instead.
Rejected Alternatives: Running MSBuild under CPU > 50%. That violates AGENTS and can disturb active workers.
Scalability potential: N/A runtime; preserves workstation stability.
Hardware Impact: Avoided a heavy compile on a saturated host.
