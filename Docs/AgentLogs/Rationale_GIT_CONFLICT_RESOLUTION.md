# Rationale_GIT_CONFLICT_RESOLUTION

Problem: GitHub Desktop left a retained auto-stash and a dirty worktree, so a later pull/merge/push path can overwrite local changes.
Solution: Treat Git state as source of truth, create a separate Codex safety stash before any destructive operation, then reconcile by diff evidence.
Rejected Alternatives: `git reset --hard`, blanket `--ours`/`--theirs`, or deleting the Desktop stash. These destroy evidence or silently drop another agent/laptop's work.
Scalability potential: Repository hygiene has no runtime tier. Low/Middle/High/Ultra behavior is unchanged; no gameplay frame cost added.
Hardware Impact: 0 us runtime impact on i3/MX350; all operations are editor/dev Git operations.

Problem: CLI `git fetch origin` returned `Repository not found`.
Solution: Continue local reconciliation against the already-fetched `origin/main` pointer and record remote auth/access as a separate blocker if push fails.
Rejected Alternatives: changing credentials or remote URL without proof. That risks breaking GitHub Desktop auth.
Scalability potential: Not a runtime system.
Hardware Impact: 0 us runtime impact.

Problem: `CarveDebrisComputeRenderer` semantic merge introduced calls to carve radius/shape validators without carrying the helper methods, and left an obsolete `ResolveCamera()` path referencing a removed field.
Solution: Added zero-allocation byte validators and deterministic radius resolver for sphere/box/capsule carve packets; removed the stale `Camera.main` resolver path instead of restoring the field.
Rejected Alternatives: Re-adding `_cameraResolveAttempted` would compile but preserve a forbidden camera lookup path; blanket revert would drop global cave SDF binding work from the incoming change.
Scalability potential: Low uses existing low-tier particle cap; Middle/High/Ultra keep richer debris with global SDF gating. Visual overkill path remains the high-tier 64-particle carve injection and cave SDF advection.
Hardware Impact: 0 allocation; estimated i3/MX350 gain versus stale camera fallback is avoidance of any accidental `Camera.main` hierarchy lookup. Static estimate only; profiler proof absent.

Problem: Push kept returning GitHub 404 after merge completion even though `origin` pointed at `https://github.com/marko1olo/Hecton8.git`.
Solution: Identified credential divergence: Git Credential Manager listed `shlomapetia` while GitHub Desktop was signed in as `marko1olo`. Added local repo credential isolation (`credential.helper =` then `credential.helper = manager`) to bypass stale global `store`, pinned credential username to `marko1olo`, ran GCM browser login for `marko1olo`, then pushed.
Rejected Alternatives: Changing the remote URL blindly, deleting Windows credentials, deleting stashes, or force pushing. Those would hide the real auth fault or risk remote/user data.
Scalability potential: Not a runtime system. Low/Middle/High/Ultra gameplay tiers unchanged.
Hardware Impact: 0 us runtime impact. Dev-path gain is removal of repeated failed push attempts from the local workflow.

Problem: Parallel agents kept modifying the worktree during the Git cleanup, so each successful push was followed by a new dirty local tail.
Solution: Treat each verified clean staged snapshot as an atomic checkpoint: run conflict-marker and whitespace checks, commit the current tail, push, fetch, and verify `origin/main...HEAD` before deciding whether to chase another tail.
Rejected Alternatives: Killing GitHub Desktop, force pushing, resetting the worktree, or claiming a clean tree while files were still changing. These either destroy active work or produce false repository state.
Scalability potential: Not a runtime system. Gameplay Low/Middle/High/Ultra behavior unchanged; repository operations only.
Hardware Impact: 0 us runtime impact on i3/MX350. Dev-path gain is bounded conflict exposure by publishing completed checkpoint batches instead of leaving all active work local.

Problem: The user asked to continue honestly while other agents were still producing live changes after the last pushed head.
Solution: Fetch first, prove committed history is synchronized, classify the new dirty tail with diff/stat/check scans, then checkpoint only the current evidence-backed snapshot.
Rejected Alternatives: Reporting "clean" because pushed history matched remote, or chasing an infinite stream without marking it as live parallel work. Both misrepresent the actual local state.
Scalability potential: Not a runtime system. Repository state tracking only; gameplay tier behavior unchanged.
Hardware Impact: 0 us runtime impact. Dev-path gain is clearer separation between pushed commits and live post-push edits.

Problem: The user explicitly requested commit, push, and pull if needed while resolving conflicts carefully.
Solution: Fetch and compare `origin/main...HEAD` before any staging. Because the count was `0 0`, no pull/merge was required; the only active work was local dirty tail from parallel agents. Validate the tail, then checkpoint and push.
Rejected Alternatives: Pulling with no incoming remote commits, force-pushing, deleting stashes, or resetting dirty files. These add risk without solving the actual state.
Scalability potential: Git-only operation. No runtime tier behavior changes.
Hardware Impact: 0 us runtime impact. Dev-path gain is avoiding unnecessary merge churn and preserving active local work.

Problem: Continued work found another graphics/VFX/docs dirty tail after the last verified push, while remote history still matched local committed history.
Solution: Treat the dirty tail as post-push live work, prove no incoming remote commits and no active conflict markers, then checkpoint the evidence-backed snapshot.
Rejected Alternatives: Calling the repo clean because committed history matched remote, or pulling with no incoming commits. Both would misstate the live worktree.
Scalability potential: Git-only operation. Runtime Low/Middle/High/Ultra behavior unchanged by this repository operation.
Hardware Impact: 0 us runtime impact. Dev-path gain is reduced local conflict exposure by publishing another validated batch.

Problem: New live audit tail now includes runtime scripts plus documentation while remote history is synchronized.
Solution: Fetch first, prove no incoming remote commits, scan for unmerged paths and conflict markers, inspect the runtime diff surface, then checkpoint only the validated snapshot.
Rejected Alternatives: Blind pull, force push, reset, or assuming docs-only risk. Runtime scripts changed, so the script diff surface required a separate sanity pass before staging.
Scalability potential: Git-only operation. Low/Middle/High/Ultra runtime behavior is not changed by the repository operation; any authored runtime tier effects remain owned by the producing agents.
Hardware Impact: 0 us runtime impact on i3/MX350. Compile/profiler proof remains absent; this pass only reduces integration conflict exposure.

Problem: Immediately after pushing `2b7a88602`, another large live tail appeared, but the committed branch still matched `origin/main`.
Solution: Treat pushed history and working-tree state as separate facts. Verify the push by fetch/divergence, then classify the new tail with stat, whitespace, and conflict-marker checks before another bounded checkpoint.
Rejected Alternatives: Reporting "all clean", deleting the new work, or pulling when no remote commits exist. Those would hide active local agent output or create needless merge churn.
Scalability potential: Git-only operation. No gameplay tier behavior changes are claimed by this integration pass.
Hardware Impact: 0 us runtime impact. The only measured gain is integration hygiene; Unity compile/profiler proof remains pending.

Problem: After `c6682a0be` was pushed and verified, a smaller 11-file local tail remained.
Solution: Keep the same evidence gate: classify it as local post-push work, confirm no conflict markers or whitespace errors beyond CRLF warnings, then checkpoint it separately.
Rejected Alternatives: Stopping with a verified but unpushed small tail, or folding it into the prior already-pushed commit by amend/force-push. Both conflict with the requested careful push workflow.
Scalability potential: Git-only operation. No Low/Middle/High/Ultra runtime claims made.
Hardware Impact: 0 us runtime impact. Compile/profiler proof remains pending.

Problem: After `88f698e08` was pushed, another 22-file local tail appeared, showing ongoing parallel writes rather than a finite merge-cleanup queue.
Solution: Make one final bounded checkpoint for this run after the same conflict-marker and diff-check gates, then stop with explicit state instead of claiming a clean tree.
Rejected Alternatives: Infinite commit loop, reset, amend/force-push, or pretending active local edits are conflicts. The correct boundary is synchronized remote plus clearly reported residual local state if it appears again.
Scalability potential: Git-only operation. No runtime tier claims.
Hardware Impact: 0 us runtime impact. Compile/profiler proof remains pending.
