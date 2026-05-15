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

Problem: `git push origin ac7e9977d81bcec15e8c3656aaefb64b908e6e78:refs/heads/visual-lod-grade-architect` timed out twice. `git ls-remote --heads origin visual-lod-grade-architect` returned no branch.

Solution: Stop only the stale push processes with command lines containing the exact visual matrix commit hash, preserve the local branch, and record the remote transport timeout as a dependency blocker.

Rejected Alternatives: Killing unrelated git commands was rejected; command-line inspection showed other agents had separate economy/fetch/status processes. Repeated blind push loops were rejected after two long transport timeouts.

Scalability potential: Not runtime. The local branch preserves the exact commit for integrator pickup or later push when GitHub transport is functional.

Hardware Impact: No runtime impact. Offline-only repository operation.

## Decision 8 - Contaminated Commit Rejection

Problem: A later candidate commit `8cb27a224fd65be564783f7eaa1f4706ef32d70d` included 38 files, including unrelated shader/prologue/leviathan/Git-conflict logs.

Solution: Reject that commit for handoff, do not push it, and recreate the handoff from current `origin/main` with an explicit owned-file allowlist.

Rejected Alternatives: Pushing the contaminated commit was rejected because it violates domain boundary and would ship unrelated agents' files.

Scalability potential: Not runtime. This preserves clean integration boundaries in a multi-agent workspace.

Hardware Impact: No runtime impact.

## Decision 7 - Simulator Drift Test

Problem: A one-shot stress run does not prevent future drift in the matrix or simulator.

Solution: Add `Tools/test_visual_stress_sim.py` with unit coverage for TOASTER VRAM guard, GOD_MODE/PRO density ratio, fixed visual density scores, and same-JSON GOD_MODE fallback references.

Rejected Alternatives: Manual re-run only was rejected because it does not fail automatically.

Scalability potential: Low - keeps TOASTER under the hard guard. High - keeps GOD_MODE overkill above 5x PRO.

Hardware Impact: Offline-only. No runtime cost.
