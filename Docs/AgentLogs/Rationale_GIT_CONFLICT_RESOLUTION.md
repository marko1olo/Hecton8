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
