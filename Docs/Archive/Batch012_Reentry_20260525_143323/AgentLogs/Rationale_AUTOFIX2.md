# AUTOFIX2 Rationale

Date: 2026-05-25
Agent: AUTOFIX2
Domain: Cross-domain diagnostic/runtime hygiene

## Decision 1

Problem: Remaining runtime/dev diagnostic call sites still call `UnityEngine.Debug.Log*` directly. In production player builds this keeps noisy managed logging paths reachable and bypasses the project-owned diagnostic gate.
Solution: Convert selected low-risk call sites to `Hecton8.Core.H8Debug` where they are diagnostic-only, smoke-test-only, visual-budget-only, or cold validation. This preserves editor/development visibility and strips release-player noise through the existing gate.
Rejected Alternatives: A new logging abstraction would touch public routes and cause coordination risk while many agents are active. A global rewrite of every direct log would risk hiding critical bootstrap failures.
Scalability potential: Low/MX350 avoids release diagnostic string/log work; Middle/High/Ultra keep development diagnostics available without adding gameplay simulation cost.
Hardware Impact: Expected per-event savings are microburst-level only; gain is avoided release-player managed logging and lower fault-surface, not steady-frame speed.
Phase: cold diagnostics, smoke validation, editor/development diagnostic branches.
Ownership: no gameplay truth ownership changed.
Evidence: pending scoped `rg` after patch.

## Decision 2

Problem: Smoke/dev/UI/visual diagnostic failures were still using direct `Debug.LogWarning`, `Debug.LogError`, or `Debug.LogException` in 28 files.
Solution: Replaced those calls with the existing `Hecton8.Core.H8Debug` facade. This keeps editor/development feedback but removes release-player direct Unity logging from these non-authority paths.
Rejected Alternatives: Editing bootstrap/fatal routes was rejected because some release crash diagnostics may intentionally bypass the development-only gate. Editing scenes/prefabs was rejected because this pass is source-only and many agents are active.
Scalability potential: Low/MX350 gains lower release diagnostic surface; Middle keeps diagnostics in development builds; High/Ultra can run the same smoke/visual checks without production log noise.
Hardware Impact: Exact measured microseconds saved: PENDING VERIFICATION. Static impact is release IL stripping of selected diagnostic calls; no profiler/build was allowed by CPU gate.
Phase: cold diagnostics, smoke validation, UI fallback diagnostics, VISUAL_SYNC validation diagnostics.
Ownership: no ownership route changed; no DTO/save/prefab/scene mutation.
Evidence: scoped `rg '(^|[^A-Za-z0-9_])(?:Debug|UnityEngine\.Debug)\.Log...'` returned no matches for the 28 touched files. `git diff --check` exit 0 with LF/CRLF warnings only. Build skipped: CPU 52.1%, no dotnet/csc listed.
