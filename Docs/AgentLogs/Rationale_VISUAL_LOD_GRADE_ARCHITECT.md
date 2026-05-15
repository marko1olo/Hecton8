# Rationale_VISUAL_LOD_GRADE_ARCHITECT

Evidence boundary: STATIC_DOC / FILESYSTEM / PYTHON_OFFLINE. Runtime proof is absent.

## Decision 1 - Scope Boundary

Problem: The batch asks for a GOD_MODE vs TOASTER tier matrix and stress simulation, not live URP asset mutation.

Solution: Create a standalone JSON contract in `Data/System/Visual_Scalability_Matrix.json` and an offline Python stress tester in `Tools/VisualStressSim.py`.

Rejected Alternatives: Direct Unity project-setting mutation was rejected because AGENTS.md forbids changing Quality/URP settings without explicit task ownership. Runtime wrappers around URP/Crest/MapMagic were rejected because the task is metadata for `REND_DYNAMIC_RESOLUTION_ADAPTER`.

Scalability potential: Low uses baked AO, depth fog, HLOD cards, mip bias, and capped particles. Middle tiers increase sample counts. High and Ultra spend saved CPU/GPU budget on stronger visual density: longer LOD residency, richer fog layering, higher particle counts, and material micro-detail.

Hardware Impact: On i3/MX350, the config target keeps total estimated render residency under the 1.6 GB guard by forcing mip bias, disabling SSR/POM/Bloom, and reserving driver/OS overhead. On top-tier hardware, the matrix intentionally increases data density instead of leaving visuals flat.

Toaster view: The player sees stable noir fog, baked AO, silhouettes, and believable particles with no physical truth waste.

God-machine view: The player sees denser water volume, SSR/refraction, 16-tap POM, triple volumetric noise, detail normals, micro-detail ORM, and 200k particles when VRAM headroom is real.

## Decision 2 - Tier Naming and Budget Shape

Problem: The project has MINIMAL/LOW/MED/HIGH/ULTRA docs, while the batch demands TOASTER/DECK/PRO/GOD_MODE.

Solution: Preserve batch tier names as stable adapter IDs, then map them to known hardware classes and guard thresholds. TOASTER is the MX350 planning path; GOD_MODE is an RTX 4080-class visual overkill path.

Rejected Alternatives: Reusing MINIMAL/LOW/MED/HIGH directly was rejected because it would fail the prompt contract. Inventing more than four tiers was rejected because the adapter handoff needs a compact row table.

Scalability potential: Low - TOASTER uses depth fog and no POM/SSR. Middle - DECK adds minimal refraction and one fog layer. High - PRO enables 8-tap POM and half-res SSR. Ultra - GOD_MODE enables 16-tap POM, full SSR, screen-space refraction, and triple fog noise.

Hardware Impact: TOASTER estimated VRAM is 1560 MiB including 400 MiB driver/OS reserve, leaving 40 MiB under the 1.6 GB guard. GOD_MODE estimated VRAM is 11418 MiB under a 14745 MiB guard.

## Decision 3 - Visual Currency

Problem: Reducing runtime truth on TOASTER can produce flat visuals if the saved budget is not redirected.

Solution: The matrix explicitly spends freed budget upward: longer LOD residency, stronger material micro-detail, higher particle density, richer fog layering, and higher post/render scale on capable GPUs.

Rejected Alternatives: A balanced middle-ground preset was rejected. HECTON-8 requires brutal downscale and visible upscale, not a single compromised aesthetic.

Scalability potential: Low - baked AO and silhouettes. Middle - one-layer fake fog and light refraction. High - hero material detail and half-res screen effects. Ultra - dense particles, triple volumetric noise, and expensive close-surface shading.

Hardware Impact: i3/MX350 avoids SSR, POM, Bloom, and detail texture residency. RTX-class hardware spends memory and GPU cycles on visible density rather than extra gameplay simulation.

## Decision 4 - Stress Simulation

Problem: Static JSON can claim compliance while violating the TOASTER guard or failing GOD_MODE visual overkill.

Solution: Add `Tools/VisualStressSim.py`, a deterministic offline simulation that reads the JSON, estimates VRAM and normalized GPU cycles, and fails on required tier, particle, fallback, density, or TOASTER memory violations.

Rejected Alternatives: Manual review was rejected because it cannot catch drift. Unity runtime validation was not used because this task is metadata/tooling and no Unity MCP/Profiler capture was available.

Scalability potential: Low - the tool locks TOASTER under 1.6 GB. Middle - DECK/PRO can be compared as intermediate rows. High - GOD_MODE density is quantified against PRO so visual overkill cannot silently collapse.

Hardware Impact: Sim result: TOASTER 1560 MiB, PRO density 338.4, GOD_MODE density 3078.4, GOD_MODE/PRO ratio 9.097. These are offline estimates, not profiler proof.

## Decision 5 - Git Push Block

Problem: The batch says commit/push, but the repository is already `main...origin/main [ahead 5, behind 4]` with a large unrelated dirty worktree.

Solution: Do not pull, rebase, force-push, or stage unrelated files. Use temporary git index commits only, verify each candidate with `git diff-tree`, and keep local branch `visual-lod-grade-architect` only on an owned-files-only commit. The current clean commit hash is stored in `Docs/AgentLogs/VisualMatrixCommit_VISUAL_LOD_GRADE_ARCHITECT.txt`.

Rejected Alternatives: Force push was rejected as data loss risk. Pull/rebase was rejected because it would merge remote state into a dirty 20-agent workspace and could trample unrelated work.

Scalability potential: Not a runtime feature. The artifact handoff remains isolated to `Data/System`, `Tools`, and this agent's logs.

Hardware Impact: No hardware impact. Runtime hot paths are untouched.

## Decision 6 - Remote Push Timeout

Problem: `git push origin ac7e9977d81bcec15e8c3656aaefb64b908e6e78:refs/heads/visual-lod-grade-architect` timed out twice. `git ls-remote --heads origin visual-lod-grade-architect` initially returned no branch.

Solution: Stop only the stale push processes with command lines containing the exact visual matrix commit hash, preserve the local branch, recreate a clean current-base commit, then retry a bounded push. Final bounded push to `origin/visual-lod-grade-architect` succeeded.

Rejected Alternatives: Killing unrelated git commands was rejected; command-line inspection showed other agents had separate economy/fetch/status processes. Repeated blind push loops were rejected after two long transport timeouts.

Scalability potential: Not runtime. The remote branch now preserves the clean handoff for integrator review.

Hardware Impact: No runtime impact. Offline-only repository operation.

## Decision 8 - Contaminated Commit Rejection

Problem: A later candidate commit `8cb27a224fd65be564783f7eaa1f4706ef32d70d` included 38 files, including unrelated shader/prologue/leviathan/Git-conflict logs.

Solution: Reject that commit for handoff, do not push it, and recreate the handoff from current `origin/main` with an explicit owned-file allowlist.

Rejected Alternatives: Pushing the contaminated commit was rejected because it violates domain boundary and would ship unrelated agents' files.

Scalability potential: Not runtime. This preserves clean integration boundaries in a multi-agent workspace.

Hardware Impact: No runtime impact.

## Decision 9 - Clean Commit Handoff

Problem: The local handoff must be usable without pulling unrelated dirty workspace files.

Solution: Create clean commit `04329730fd3d9e4563ba2d1045302ce9b99ed73f` from current `origin/main` using a temporary index and an explicit allowlist. `git diff-tree` reports exactly these files: `Data/System/Visual_Scalability_Matrix.json`, `Tools/VisualStressSim.py`, `Tools/test_visual_stress_sim.py`, status, rationale, log, and stress report.

Rejected Alternatives: Including the ignored patch file in the clean branch commit was rejected because it is a handoff artifact, not source. The patch and bundle remain on disk under `Docs/AgentLogs`.

Scalability potential: Not runtime. This gives the integrator a small branch diff and separate patch/bundle fallback.

Hardware Impact: No runtime impact.

## Decision 10 - Final Status Commit

Problem: The first successful remote push carried the clean matrix/code handoff but status still described remote timeout.

Solution: Add a docs-only follow-up commit on top of the clean branch so `origin/visual-lod-grade-architect` records the final completed state.

Rejected Alternatives: Leaving remote branch with blocked status was rejected because it would create a false report for the CTO.

Scalability potential: Not runtime. It improves handoff evidence integrity.

Hardware Impact: No runtime impact.

## Decision 7 - Simulator Drift Test

Problem: A one-shot stress run does not prevent future drift in the matrix or simulator.

Solution: Add `Tools/test_visual_stress_sim.py` with unit coverage for TOASTER VRAM guard, GOD_MODE/PRO density ratio, fixed visual density scores, and same-JSON GOD_MODE fallback references.

Rejected Alternatives: Manual re-run only was rejected because it does not fail automatically.

Scalability potential: Low - keeps TOASTER under the hard guard. High - keeps GOD_MODE overkill above 5x PRO.

Hardware Impact: Offline-only. No runtime cost.

## Decision 11 - Post-Resume Verification Discipline

Problem: After the first completion report, a verification command used an escaped quote pattern and returned `TASK_COUNT=0`, which could be misread as a prompt extraction failure.

Solution: Re-run extraction with `rg` against `Docs/Tasks/CURRENT_BATCH.md`, verify the exact `VISUAL_LOD_GRADE_ARCHITECT` XML block, and record that the block contains 10 numbered tasks even though its local header text says 15 titanium tasks.

Rejected Alternatives: Treating the stale `TASK_COUNT=0` result as authoritative was rejected because the raw XML block visibly contains tasks 1-10. Editing the batch file was rejected because the prompt source is not this agent's artifact.

Scalability potential: Not runtime. This protects the matrix handoff from false task-count rejection.

Hardware Impact: No runtime impact. Offline validation was rerun and still reports TOASTER 1560 MiB and GOD_MODE/PRO density ratio 9.097.

## Decision 12 - Final Verification Push Without Dirty Index

Problem: Post-resume evidence changed only three owned documentation files, but the live `main` worktree contains unrelated multi-agent modifications.

Solution: Create and push the final verification evidence with a temporary git index and an allowlist containing only this agent's status, rationale, and log files. The branch-tip commit containing this paragraph records the completed verification push.

Rejected Alternatives: Staging the live index was rejected because it would include unrelated work. Rewriting `main` was rejected because the task owns only the visual scalability handoff branch.

Scalability potential: Not runtime. The branch remains isolated for integrator review.

Hardware Impact: No runtime impact.

## Decision 13 - Polish Hardening for Drift Failure

Problem: The post-completion anti-bloat audit found no `<POLISH_MANDATE>` tag in `Docs/Tasks/CURRENT_BATCH.md`, but it did find a legitimate robustness gap: `build_report()` tried to stress every required tier before it could produce a structured failure report. If a future edit removed `GOD_MODE`, the tool would throw a Python key error instead of returning a deterministic audit failure.

Solution: Add an early missing-tier check in `build_report()` and expose `REQUIRED_GOD_FALLBACK_REFS` as a module constant so tests verify every expensive GOD_MODE fallback path points at an existing `godModeFallbacks` key.

Rejected Alternatives: Leaving the throw path was rejected because stress tooling must fail cleanly in CI-style use. Adding a schema package was rejected because it would introduce dependency weight for a small fixed JSON contract.

Scalability potential: Not runtime. The matrix handoff becomes harder to corrupt during future tier edits.

Hardware Impact: No runtime impact. Offline validation still reports TOASTER 1560 MiB and GOD_MODE/PRO density ratio 9.097.

## Decision 14 - Required Field Preflight Before Stress Math

Problem: The first polish hardening handled missing tier rows, but a missing nested field such as `GOD_MODE.shaderFeatures.pom.tapCount` could still throw before the report returned structured failures. The CLI summary also assumed every tier row existed.

Solution: Add required top-level and per-tier field preflight before stress math, validate that every GOD_MODE fallback reference resolves to an existing `godModeFallbacks` key, and make `print_summary()` emit `MISSING` rows for partial failure reports.

Rejected Alternatives: Wrapping broad `try/except` around stress math was rejected because it would hide the exact missing contract field. Adding a full schema dependency was again rejected as unnecessary weight for this fixed matrix.

Scalability potential: Not runtime. The adapter handoff now has stricter drift detection for missing fields and broken fallback references.

Hardware Impact: No runtime impact. Offline validation still reports TOASTER 1560 MiB and GOD_MODE/PRO density ratio 9.097.
