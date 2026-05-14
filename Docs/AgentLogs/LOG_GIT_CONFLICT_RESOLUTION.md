# LOG_GIT_CONFLICT_RESOLUTION

What was wrong:
- GitHub Desktop had created `stash@{1}` (`!!GitHub_Desktop<main>`) and left the worktree dirty, so pull/push flow was blocked by local files that would be overwritten.
- CLI `git fetch origin` returned `Repository not found`, so shell-side remote sync cannot be trusted from this environment.
- The dirty tree contained a semantic merge break in `CarveDebrisComputeRenderer`: missing carve radius/shape helpers and stale `_cameraResolveAttempted` references after the hot path was changed away from camera auto-resolution.

What was done:
- Created a separate Codex backup stash before reconciliation: `stash@{0}` (`codex/git-conflict-resolution backup before reconciliation 2026-05-14`).
- Reapplied the dirty state and preserved the GitHub Desktop stash. No `reset --hard`, no checkout discard, no stash deletion.
- Confirmed no unmerged Git paths.
- Confirmed no active `<<<<<<<` / `>>>>>>>` conflict markers outside ignored/generated/archive paths.
- Ran `git diff --check`; only CRLF normalization warnings were emitted.
- Added zero-allocation carve event validators and a deterministic radius resolver for sphere/box/capsule carve events.
- Removed stale debris camera auto-resolver path instead of restoring `Camera.main` fallback.

Cinematic Cheats used:
- Git side: none; repository hygiene only.
- Runtime side: debris remains visual feedback, not physical truth. Low tier uses existing reduced particle cap; high tier keeps richer carve debris and global cave SDF advection.

Exact Microseconds saved:
- Measured profiler proof absent. Runtime measured saved: 0 us.
- Static estimate: removal of stale camera auto-resolve path prevents any accidental `Camera.main` hierarchy lookup from this renderer; exact frame gain is PENDING VERIFICATION.

Verification:
- STATIC_SOURCE: no conflict markers, no unmerged paths, diff check clean except line-ending warnings.
- CLI_COMPILE: PENDING VERIFICATION. `dotnet build Assembly-CSharp.csproj` failed three times for tooling/infrastructure reasons: parallel MSBuild child node failure, missing generated `Temp/obj/project.assets.json` under `--no-restore`, and restore/build timeout after 10 minutes.
- UNITY_CONSOLE / PLAYMODE / PROFILER: not run in this shell.
