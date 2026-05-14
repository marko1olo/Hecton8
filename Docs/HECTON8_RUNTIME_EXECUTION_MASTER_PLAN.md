# HECTON-8 Runtime Execution Master Plan

Status: PENDING VERIFICATION
Verification: `PENDING VERIFICATION`
Date: 2026-05-14

This is the working execution plan for getting HECTON-8 to a stable, optimized, verifiable runtime state.

Current-state boundary:

- 2026-05-14 DOC_AUDIT override: read `Docs/Reports/2026-05-13_DOC_AUDIT_XRAY.md` before treating May 11 counters or build-artifact paths as current evidence.
- The May 11 compile artifact paths cited below are absent from the current filesystem. They are stale report text, not current artifact-backed proof. Current R43 external root `Hecton8*.csproj` no-restore CLI compile evidence is `0 Warning(s)` / `0 Error(s)` after restore assets and referenced `Temp\bin\Debug` DLLs exist; it is not Unity runtime proof.
- This stable plan is the runtime execution authority. Dated reports are evidence/counter snapshots only.
- Read `Docs/README.md`, `.agents-skills/README.md`, `Docs/ARCHITECTURE/README.md`, current source, `Docs/Reports/2026-05-13_DOC_AUDIT_XRAY.md`, and then older evidence reports before using this plan as execution guidance.
- This plan is still directionally valid, but it is not a runtime verification report.
- May 11 report text claimed current compile evidence at `CodexArtifacts/2026-05-11_DOCS_CONTINUATION_CORE_BUILD_R1.summary.txt`, but DOC_AUDIT did not find that summary or raw log. Treat it as stale report text.
- Current Unity MCP, Unity Console, Play Mode, profiler, GCMonitor, player build, import, scene wiring, visual quality, zero-GC, frame time, and memory retention remain `PENDING VERIFICATION`.
- Older May 4/May 8/May 9/May 11 builds remain historical only where newer May 13 docs do not cover the question.
- DOTS remains optional/prototype-only until source and profiler evidence prove otherwise.

This plan replaces vague "finish DOTS" thinking with a hard sequence:

1. stabilize the real runtime owner stack
2. finish the scatter refactor
3. stop architecture drift
4. only then decide whether DOTS is worth keeping

No step is considered complete without logs, profiler captures, or Unity verification.

---

## 1. Current Truth

### 1.1 What is actually true in code

- `WorldProceduralScatterDirector` is still the runtime owner for scatter.
- scatter already has a backend seam, classic adapters, a shadow path, and a minimal Entities prototype.
- the current Entities backend is not a proven optimization path.
- the current Entities backend does not own live placements.
- `ReservedLiveOwnership` is intentionally gated back to `Shadow`.
- `SceneBootstrap` is still a large owner; only the read-model extraction has started.
- `HectonFluidEngine`, `ProximityColliderSystem`, `FaunaDirector`, and `HectonVoxelEngine` remain hybrid systems, not valid first-wave ECS migrations.

### 1.2 Hard findings that must be carried into all future work

- The current Entities scatter backend is a prototype, not a production backend.
- The current Entities scatter backend performs its real simulation work on the main thread during completion, so it does not yet deliver the promised DOTS value.
- The current shadow parity signal is too weak. Candidate count delta alone is not parity.
- The scatter refactor is only partially complete. Working memory and state machine groundwork exist, but the owner is still too large and too stateful.
- The current codebase has substantial workspace noise: backup files, temp files, partial migrations, and parallel doc variants. This increases future agent error risk.

### 1.3 Strategic conclusion

- Full-project ECS migration is the wrong move.
- Immediate priority is not "push DOTS further."
- Immediate priority is "finish the hybrid runtime cleanup and prove the real bottlenecks."
- DOTS stays optional until profiler evidence justifies it.

---

## 2. Non-Negotiable Rules

- Apply `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt` before adding any water, light, flow, pressure, deformation, tether, ambience, particle, or distant-motion simulation.
- Reject simulation when shader/VFX/audio/haptic/proxy presentation can preserve player belief.
- Promote durable policy changes into `AGENTS.md`, `.agents-skills`, or stable `Docs/*.md` files. Do not leave project rules buried only in dated reports.
- Any single runtime system cost above `0.1ms` is suspicious until profiler proof and load-shed behavior exist.
- Do not create a second scatter runtime owner.
- Do not create a separate coral/rock/ruin runtime stack.
- Do not move player, UI, save, bootstrap, active AI, or pooled Rigidbody gameplay to ECS in the current phase.
- Do not enable live DOTS placement ownership until parity and profiler proof exist.
- Do not claim optimization without before/after measurements.
- Do not accept "candidate count looks similar" as correctness proof.
- Do not keep misleading inspector text, rollout flags, or dead paths once the real direction is decided.

---

## 3. Master Goal

Reach a runtime state where:

- scatter is stable, maintainable, and measurably optimized
- bootstrap and world systems remain deterministic
- no second runtime owner exists
- hot paths stay zero-GC by measurement, not by hope
- DOTS is either removed, hard-disabled, or proven with numbers

---

## 4. Execution Order

## Phase 0 - Freeze Dangerous Work

Goal: stop making the architecture worse while refactor work is still incomplete.

Tasks:

- Freeze live DOTS ownership work. Requested execution mode must remain `Disabled` or `Shadow`.
- Treat `ScatterEntitiesSimulationBackend` as prototype-only.
- Forbid any new ECS expansion outside scatter until scatter owner cleanup is finished.
- Forbid migration of save, UI, player, bootstrap, active fauna, or pooled physics gameplay into ECS.
- Stop adding new scatter-side ad-hoc fields to `WorldProceduralScatterDirector`.

Exit criteria:

- No live DOTS ownership path is enabled.
- No new runtime owner is introduced.
- All current tasks route through the existing scatter owner.

Owner split:

- Developer: enforce branch discipline and reject speculative ECS work.
- Coding agent: refuse architecture drift and keep changes inside the existing owner boundary.

---

## Phase 1 - Establish a Real Baseline

Goal: get factual proof of the current runtime cost before further changes.

Tasks:

- Verify Unity package import state for `com.unity.entities`.
- Verify the project compiles in Unity after current asmdef changes.
- Capture baseline profiler data for scatter-heavy runtime.
- Capture GC, main-thread cost, total frame time, texture memory, and total reserved memory.
- Capture scatter-specific timings: dispatch, sampling, processing, rescue, restore, reconcile, diagnostics.
- Capture whether bootstrap prewarm changes startup cost or hitch profile.

Required captures:

- Profiler before any new runtime change
- Profiler after each major phase
- Console log proof of package import / compile state
- GCMonitor or equivalent runtime data

Exit criteria:

- Baseline metrics exist and are attached to the current branch state.
- Scatter bottleneck is described with numbers, not adjectives.

Owner split:

- Developer: run Unity, gather logs, profiler captures, screenshots, and playmode proof.
- Coding agent: prepare capture checklist, normalize markers, and make sure instrumentation is readable and not misleading.

---

## Phase 2 - Finish the Scatter Refactor Without DOTS

Goal: finish the refactor described in the scatter proposal before any serious DOTS rollout.

This is the main production phase.

Tasks:

- Reduce `WorldProceduralScatterDirector` from monolith owner to strict coordinator owner.
- Keep all inspector state in the owner, but move non-inspector logic and working state out where possible.
- Finish `ScatterWorkingMemory` so all temporary compute-side collections live behind `_memory`.
- Remove remaining loose field clusters that should be working-memory state.
- Finish the async two-tick sampling pipeline and keep its lifecycle explicit.
- Keep `SamplingSnapshot` as the single source of truth for processing-time observer math.
- Keep deterministic functions untouched: `StableRandom01`, `ComputeStableHash`, `ComputeRuleIdHash`, `ComposeKey`.

Missing work from the proposal that still needs real implementation or equivalent closure:

- explicit rescue-processing context boundary
- explicit spawn-processing batch service boundary
- clearer suppression gate placement in processing
- further breakup of giant switch-heavy and branch-heavy logic inside the owner
- cleanup of duplicate responsibility between classic owner path and backend seam path

Recommended execution slice inside this phase:

1. move remaining temporary processing buffers fully behind `ScatterWorkingMemory`
2. extract stateless spawn batch processor
3. isolate rescue placement logic behind one narrow contract
4. isolate suppression and retained-placement logic behind one narrow contract
5. reduce owner direct knowledge of low-level data structures
6. remove or collapse dead partials, dead branches, and misleading debug state

Exit criteria:

- scatter owner still owns runtime, but no longer contains avoidable service logic
- sampling lifecycle is explicit and safe
- spawn/reconcile progression is explicit and testable
- no new GC allocations appear in hot scatter paths by measurement
- no bootstrap regression appears after refactor

Owner split:

- Developer: verify runtime behavior in scene, especially bootstrap prime, scatter refresh cadence, pool reuse, rescue placement, suppression, and cave/seafloor vertical behavior.
- Coding agent: implement the extraction and cleanup work while preserving ownership, determinism, and serialization safety.

---

## Phase 3 - Decide the Fate of the Current DOTS Prototype

Goal: stop living with an ambiguous half-real prototype.

Recommended default path:

- Keep the package and seam if needed.
- Keep the runtime execution mode disabled or shadow-only.
- Mark the current Entities backend as prototype-only in code and docs.
- Do not market it internally as an optimization until it actually is one.

Decision options:

### Option A - Keep Only the Seam, Downgrade the Prototype

Use when:

- the current prototype provides no measurable frame gain
- parity proof is weak
- maintenance cost exceeds value

Actions:

- keep contracts, asmdefs, registry, and owner seam
- keep shadow path only if it still helps future work
- remove misleading rollout wording
- make inspector/debug text explicitly say "prototype / not production / no live ownership"

### Option B - Build a Real DOTS Backend

Use only if profiler evidence says scatter bookkeeping is still a major CPU problem after Phase 2.

If this path is taken, the backend must represent real runtime state:

- cell entities
- occupancy state
- eligibility state
- layer quotas
- suppression state
- refresh dirty flags
- candidate output buffers

It must not:

- spawn prefabs
- touch `ObjectPoolManager`
- submit GPUI buffers directly
- own save state
- become a second placement owner

Exit criteria:

- either the prototype is formally downgraded and contained
- or a real backend spec exists with parity criteria stronger than candidate count

Owner split:

- Developer: approve whether DOTS remains in scope after Phase 2 metrics.
- Coding agent: either harden the seam and downgrade the prototype, or implement the real data backend under explicit parity gates.

---

## Phase 4 - Rebuild DOTS Validation Properly If DOTS Survives

Goal: if DOTS stays, validate it correctly.

Required parity checks:

- candidate identity parity
- layer parity
- key parity
- suppression parity
- restore / retain parity
- candidate order relevance only where order matters
- total frame cost, not only isolated backend cost

Required performance checks:

- main-thread delta
- total frame delta
- GC delta
- memory delta
- sync/readback penalty

Automatic rejection conditions:

- simulation is faster but total frame is slower
- sync fences erase the backend gain
- memory rises without visible density benefit
- suppression or restore semantics diverge
- DOTS path starts demanding a second owner

Exit criteria:

- DOTS either proves value with captures
- or DOTS remains shadow-only and out of the critical path

---

## Phase 5 - Fix the Hybrid Systems in the Correct Order

Goal: improve the real game bottlenecks without architecture theater.

### 5.1 `HectonFluidEngine`

Truth:

- already uses native buffers and Burst jobs
- still does `Schedule -> Complete` in the same fixed tick
- still writes back through classic `Rigidbody`

Plan:

- profile gather cost, complete cost, and apply cost separately
- overlap schedule/complete only if profiler says it matters
- reduce gather/writeback cost before considering ECS

### 5.2 `ProximityColliderSystem`

Truth:

- already follows the right frame-delayed hybrid cadence
- should be treated as a reference implementation for safe schedule/complete behavior

Plan:

- keep hybrid
- use it as a pattern source for other runtime jobs

### 5.3 `FaunaDirector`

Truth:

- remains owner-heavy, pool-heavy, behavior-heavy
- not a valid first-wave ECS migration

Plan:

- keep active fauna classic
- only consider a later passive ecology bookkeeping layer for ECS
- do not move active creature controllers or Rigidbody logic

### 5.4 `HectonVoxelEngine` and geology stack

Truth:

- already has a heavy hybrid native/job pipeline
- major costs remain mesh/collider/object lifecycle

Plan:

- keep generation and mesh ownership classic/hybrid
- only consider DOTS later for request scheduling, residency, and stale-request state

Exit criteria:

- no system is moved to ECS just because it sounds modern
- each hybrid system has a measured optimization target

---

## Phase 6 - Bootstrap and Runtime Correctness Pass

Goal: make startup, scene handoff, and runtime readiness deterministic and auditable.

Tasks:

- keep `SceneBootstrap` as owner for now
- expand use of `BootstrapState` only as a read-model, not as a hidden second owner
- verify bootstrap scatter prewarm path in real runtime
- verify world-ready and player-ready ordering
- verify startup memory metrics are captured before gameplay starts
- verify no save/bootstrap race causes scatter or runtime systems to start too early

Exit criteria:

- startup order is stable
- bootstrap state readers do not bypass owner contracts
- no runtime system depends on scene search where bootstrap already publishes state

Owner split:

- Developer: run boot sequence verification in Unity.
- Coding agent: reduce code ambiguity, remove hidden dependency paths, and tighten bootstrap read-model usage.

---

## Phase 7 - Workspace and Repository Hygiene

Goal: reduce future coding-agent failure risk.

Current problem:

- the worktree contains backup files, temp fragments, duplicate variants, partial fixes, and mixed state across runtime and docs

Tasks:

- identify all backup/temp/source-of-truth conflicts
- decide what is canonical and what is trash
- remove or archive non-canonical runtime script variants
- remove temporary split files and stale backups from active source folders
- make sure asmdef layout reflects real ownership, not accidental migration leftovers

Exit criteria:

- next coding agent can navigate the repo without selecting the wrong file
- source-of-truth runtime files are obvious

Owner split:

- Developer: approve destructive cleanup before deletion of suspicious files.
- Coding agent: prepare cleanup list, perform safe cleanup only after approval when risk exists.

---

## Phase 8 - Final Runtime Hardening

Goal: move from "refactor in progress" to "production-ready and measurable."

Tasks:

- run code review pass on all scatter hot paths
- confirm no forbidden `Update` drift reappeared
- confirm no pooled lifecycle leaks
- confirm no unsafe job disposal ordering
- confirm no inspector serialization regressions
- confirm no misleading debug or rollout text remains
- confirm no dead prototype path silently stays enabled

Exit criteria:

- codebase is internally honest
- runtime paths are understandable
- future work no longer depends on reverse-engineering half-finished intent

---

## 5. Definition of Done

The game is not "done" when code looks cleaner.

The runtime slice is only done when all of the following are true:

- Unity compile/import is clean
- playmode boot is stable
- scatter owner remains singular
- scatter hot path GC is verified
- main-thread scatter cost is measured before and after
- total frame time is measured before and after
- no bootstrap regression is observed
- no save/suppression regression is observed
- DOTS is either proven or explicitly non-production

Until then: `PENDING VERIFICATION`

---

## 6. Responsibility Split

## Developer responsibilities

- run Unity
- provide profiler captures
- provide Console logs
- confirm package import state
- verify scene startup, scatter runtime behavior, and playmode correctness
- approve cleanup of ambiguous or destructive file removals
- make product-level decisions on whether DOTS remains in scope after Phase 2

## Coding agent responsibilities

- implement scatter refactor steps without changing owner boundaries
- keep deterministic math untouched
- keep serialization safe
- hard-disable or downgrade misleading prototype paths when needed
- extract contracts and reduce monolith size
- maintain docs and execution order
- refuse speculative ECS migration outside approved scope

---

## 7. Recommended Immediate Next Slice

This is the exact next route for the next coding agent:

1. do not touch live DOTS ownership
2. audit and clean the scatter owner/file graph
3. finish Phase 2 extraction work around working memory, spawn batching, rescue/suppression contracts
4. downgrade misleading DOTS rollout wording and prototype assumptions if they are not true
5. prepare the Unity verification checklist for the developer
6. wait for profiler data before any real DOTS expansion

If profiler data later proves scatter bookkeeping is still a major cost:

7. write a real DOTS backend spec
8. implement it behind shadow-only rollout
9. validate parity with identity/suppression semantics
10. only then discuss live ownership

---

## 8. Final Directive

Do not chase DOTS for appearance.

Finish the hybrid architecture first.

Measure the real runtime.

Keep one owner.

Remove lies from code and docs.

Anything else is churn.
