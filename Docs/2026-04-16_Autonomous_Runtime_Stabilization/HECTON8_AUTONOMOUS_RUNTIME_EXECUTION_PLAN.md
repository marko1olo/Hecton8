# HECTON-8 Autonomous Runtime Stabilization Plan

Status: `ACTIVE`
Verification: `PENDING VERIFICATION`
Date: `2026-04-16`
Author: `Codex`

This document is the execution ledger for autonomous code-first work in the current branch state.

It exists for one reason:

- stop random local edits
- stop decorative planning
- anchor every change to evidence, ownership, and regression control

No item in this document counts as complete without code inspection evidence, build evidence, or user-provided Unity/runtime logs.

---

## 1. Current Operating Truth

### 1.1 Repository truth

- The repository is already noisy and partially edited.
- Runtime, UI, world, bootstrap, prefabs, scenes, docs, and shaders are all currently dirty in the worktree.
- Several recently touched runtime files are likely mid-flight and unsafe for blind refactor.
- Existing plans already exist, but they are split across multiple dates and focus areas.
- That means new work must avoid architectural theater and avoid trampling active edits.

### 1.2 Technical truth

- The codebase has explicit AGENTS.md constraints that prohibit hope-based claims.
- The main production pressure remains runtime stability, zero-GC hot paths, ownership clarity, and visual/runtime polish within MX350-class limits.
- `Scatter` remains the declared primary CPU offender.
- Multiple first-party runtime files still expose compliance risks:
  - `Camera.main` fallback use
  - coroutine use in runtime smoke tools
  - native `Update` outside allowed boundaries
  - direct `Instantiate` in selected paths
  - branch noise and repo-source-of-truth ambiguity

### 1.3 Constraint truth

- Unity may be unavailable or reloading for a long time.
- Work must therefore proceed code-first.
- No runtime fix is considered proven without logs or playmode verification.
- Final status after any code-only pass remains `PENDING VERIFICATION`.

---

## 2. Mission

Stabilize the highest-confidence runtime code paths that can be improved safely in the current dirty branch without:

- changing public APIs without necessity
- creating a second owner for any subsystem
- colliding with already-modified critical files unless the evidence justifies it
- inventing new architecture
- claiming success without runtime proof

---

## 3. Hard Rules For This Pass

### 3.1 Allowed

- read docs and runtime code
- identify concrete compliance violations
- patch low-risk first-party code outside active collision zones
- add audit/plan documentation
- run local code/build verification available from the workspace

### 3.2 Forbidden

- broad refactors in already-heavily-modified owner files without a narrow target
- scene/prefab destructive cleanup
- speculative DOTS expansion
- public API drift unless dependency impact is understood
- “cleanup” edits with no measurable runtime or maintenance value

---

## 4. Execution Strategy

The pass is intentionally staged.

### Phase 0 — Evidence Intake

Goals:

- read root contracts and runtime master plan
- inspect dirty worktree state
- find first-party compliance violations
- separate active-user-edit zones from safer patch zones

Exit criteria:

- list of candidate files with reason
- conflict-risk estimate per candidate
- clear decision on what not to touch

### Phase 1 — Plan Anchor

Goals:

- create a dedicated dated folder
- write this plan as the execution ledger
- lock the workstream so further changes remain traceable

Exit criteria:

- plan file exists
- next slices are explicit

### Phase 2 — Safe Runtime Compliance Wins

Goals:

- patch small but real runtime risks in files not already in active conflict
- prioritize camera ownership/cold fallback hygiene and other compliance issues that can regress silently
- preserve existing architecture

Candidate classes for this phase:

- `HectonUnderwaterVisuals`
- `SaveSlotThumbnail`
- `SettingsLivePreview`
- other first-party runtime files not currently modified in git status

Expected win profile:

- lower hidden scene-search reliance
- cleaner cold-path camera resolution
- better determinism and failure behavior
- no hot-path allocation increase

Exit criteria:

- targeted patches applied
- local compile-oriented inspection completed
- regression notes documented

### Phase 3 — Conflict-Aware Deeper Audit

Goals:

- inspect high-value dirty files without overwriting active user work
- identify whether any current modified owner file contains a blocking compliance defect that is too serious to ignore

Priority targets:

- `WorldProceduralScatterDirector*`
- `SceneBootstrap`
- `GameTickManager`
- `CullingManager`
- `LODSystemManager`
- `PDAAtlasSignalTab`
- `SettingsManager`

Method:

- read, do not patch blindly
- only escalate to edit if:
  - defect is concrete
  - patch can be minimal
  - collision risk is acceptable

Exit criteria:

- blocking findings logged
- patch/no-patch decision per owner file

### Phase 4 — Verification Pass

Goals:

- run local non-Unity validation that is actually possible now
- inspect diffs
- verify no accidental drift into forbidden patterns

Checks:

- targeted grep regression scan
- project-level build attempt if feasible from current workspace state
- file diff review for touched files only

Exit criteria:

- verification notes captured
- unresolved runtime proof gaps explicitly called out

---

## 5. Work Queue

### Lane A — Documentation and Control

1. Create dedicated plan folder and document.
2. Keep a running execution ledger in this folder if work expands.
3. Record what was intentionally avoided and why.

### Lane B — Safe Runtime Compliance

1. Audit unmodified first-party runtime files for:
   - `Camera.main`
   - cold fallback misuse
   - repeated scene-search behavior
   - weak null/failure handling
2. Patch the safest subset.
3. Re-scan to confirm no new violations were introduced.

### Lane C — Active-Conflict File Review

1. Review dirty owner files.
2. Tag each as:
   - `safe to ignore for this pass`
   - `needs user log verification`
   - `contains immediate defect`
3. Only patch the third category.

### Lane D — Regression Guard

1. Avoid broad formatting noise.
2. Touch the minimum number of files.
3. Do not rewrite scene/prefab assets.
4. Preserve public surface unless proven safe.

---

## 6. Candidate Defect Classes

### 6.1 Camera ownership drift

Symptoms:

- runtime code falls back to `Camera.main`
- cold fallback may silently bind wrong camera in multi-camera setups
- some UI/runtime systems rely on implicit scene search instead of explicit ownership

Action:

- convert to explicit cached resolution with guardrails
- keep fallback cold-only
- avoid repeated lookup
- improve null behavior and diagnostics where needed

### 6.2 Hidden compliance drift

Symptoms:

- native `Update` outside the allowed scope
- coroutine usage in runtime-only logic
- direct `Instantiate` in gameplay/runtime paths

Action:

- classify before editing
- fix only if the owner and side effects are clear

### 6.3 Dirty-branch collision

Symptoms:

- file already heavily modified by user or other agent
- patching risks merge damage or semantic overwrite

Action:

- prefer read-only audit
- defer unless defect is blocking

---

## 7. File Selection Rules

Patch first if all are true:

- first-party
- runtime relevant
- not a scene/prefab asset
- not heavily edited in current worktree
- defect is concrete
- fix is local

Do not patch yet if any are true:

- file is a major active owner with broad current edits
- code path is unclear without Unity inspection
- patch would force public API drift
- fix would become speculative

---

## 8. Regression Model

Every code change in this pass must be evaluated against:

- CPU: any extra polling, scene search, branch churn, or per-frame cost
- GC: any new allocations in Tick/Update/FixedUpdate/slow cadence
- memory: any new cache/state lifetime without explicit ownership
- cadence: registration/unregistration timing, startup order, post-disable safety
- correctness: wrong camera binding, null fallback behavior, stale state, visual preview mismatch

If a fix improves one axis and worsens another without proof, it is rejected.

---

## 9. Verification Model

### 9.1 Available now

- repo inspection
- grep audits
- diff inspection
- local build attempt if solution state allows it

### 9.2 Not available without user/runtime confirmation

- GCMonitor proof
- Profiler proof
- playmode correctness proof
- visual verification in Unity
- startup race confirmation

### 9.3 Mandatory final wording for this pass

If Unity/runtime evidence is absent:

- status remains `PENDING VERIFICATION`
- claims must be code-review-only

---

## 10. Immediate Next Slice

1. finish candidate scan for safe unmodified runtime files
2. patch the first low-risk compliance issues
3. inspect diffs
4. attempt local verification available from code/build
5. document remaining risk and proof gaps

---

## 11. Explicit Non-Goals For This Pass

- no visual art direction redesign
- no speculative architecture rewrite
- no scene authoring sweep
- no prefab-wide synchronization pass
- no DOTS migration expansion
- no broad cleanup of third-party packages

---

## 12. Completion Criteria

This pass is considered materially useful only if it produces all of the following:

- one dedicated execution plan document in a dedicated folder
- at least one real first-party runtime code improvement
- no accidental damage to active user work
- a verified list of what remains blocked on Unity evidence

Anything less is paperwork.

---

## 13. Execution Log

### Pass 01 — Completed

Scope:

- establish dedicated dated plan folder
- deliver first safe low-conflict runtime improvements

Applied changes:

- `Assets/_Project/Scripts/UI/SaveSlotThumbnail.cs`
  - camera resolution now prefers bootstrap player camera, then local hierarchy, then `Camera.main`
  - thumbnail capture now reuses a dedicated `Texture2D` scratch buffer instead of allocating one per capture
  - thumbnail display now reuses its `Texture2D` and loads PNG data with `markNonReadable=true`
- `Assets/_Project/Scripts/UI/SettingsLivePreview.cs`
  - FOV preview no longer jumps straight to `Camera.main`
  - camera resolution now prefers bootstrap player camera and local hierarchy first
  - retry cadence throttles repeated resolve attempts when preview is dirty but camera is still unresolved
- `Assets/_Project/Scripts/AsyncLoadHelper.cs`
  - disabled legacy helper no longer creates a hidden `DontDestroyOnLoad` runtime owner
  - static singleton reference now clears on destroy

Verification status:

- code-review verified
- diff verified
- local build verification unavailable in shell
- Unity/runtime verification absent
- status remains `PENDING VERIFICATION`

### Pass 02 — Deferred Candidates

Deferred because of active worktree conflict or wider owner risk:

- `WorldProceduralScatterDirector*`
- `HectonUnderwaterVisuals`
- `CullingManager`
- `LODSystemManager`
- `PDAAtlasSignalTab`
- `SettingsManager`

Safe next candidates if another low-conflict pass is required:

- audit non-owner UI/runtime helper classes for one-time allocation hygiene
- audit cold-path camera resolution in currently unmodified support classes
- classify runtime smoke/verifier coroutine usage into acceptable test-only vs architecture drift

### Pass 03 — Coroutine Classification

Findings:

- current `StartCoroutine` hits under first-party scripts are concentrated in:
  - `*RuntimeSmokeTester`
  - `Tools/*Verifier`
  - `Dev/*SmokeTester`
- no new evidence in this scan shows coroutine use inside normal shipped gameplay loop owners
- result: coroutine cleanup is still desirable for consistency, but it is not the highest-value production runtime target in the current low-conflict pass
