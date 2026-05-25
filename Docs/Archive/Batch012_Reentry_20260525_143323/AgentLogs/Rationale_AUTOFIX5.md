# AUTOFIX5 Rationale

Date: 2026-05-25
Domain: Cross-domain diagnostic/runtime hygiene
Status: DONE - STATIC VERIFIED / BUILD GATED

Problem: Direct Unity diagnostics remain in runtime, smoke, validation, and exception-bridge files. The issue is route ownership, not message content.
Solution: Convert direct `Debug.*` and `UnityEngine.Debug.*` calls to `Hecton8.Core.H8Debug` in a bounded 20-40 file slice, preserving compile guards and context overloads.
Rejected Alternatives: Repo-wide replacement was rejected because bootstrap, signal ABI, and build validators require separate authority review. Removing logs was rejected because the project relies on failure evidence and black-box traces.
Scalability potential: Low tier keeps diagnostics out of scattered direct Unity routes during rare fallback storms; middle/high/ultra keep the same evidence through a centralized route that can later be quality/cadence gated.
Hardware Impact: Static hygiene only. Expected i3/MX350 impact is small and burst-bound; accepted runtime gain remains 0 us until profiler evidence exists.

Problem: The worktree is dirty and several files are already modified by other agents.
Solution: Make only mechanical line-level edits in explicit files, avoid reverting any unrelated changes, and verify the touched slice instead of claiming global cleanup.
Rejected Alternatives: Architecture refactor and signature changes were rejected because they would collide with parallel work.
Scalability potential: Stable diagnostics route reduces future telemetry fragmentation without changing gameplay truth ownership.
Hardware Impact: No steady-frame claim without Unity profiler proof.

Problem: Several selected files are high-importance validators or black-box adjacent systems, not disposable debug utilities.
Solution: Route only the visible diagnostics through `H8Debug`; keep validation decisions, buffer limits, exception capture, and disable/fallback branches untouched.
Rejected Alternatives: Mutating validator policy or capacity constants was rejected because this task is diagnostic hygiene, not ownership redesign.
Scalability potential: Low hardware keeps the same fail-fast constraints; higher tiers retain richer evidence without adding simulation or allocation paths.
Hardware Impact: Accepted runtime gain remains 0 us; static route cleanup only.

Problem: Smoke testers and construction/tool fallback files still emitted direct Unity diagnostics.
Solution: Convert the route while preserving test failure wording and timeout/exception branches.
Rejected Alternatives: Rewriting smoke tests into a new harness was rejected; that would be a separate QA architecture task.
Scalability potential: Smoke/fallback diagnostics remain available on all tiers and do not create new gameplay dependencies.
Hardware Impact: No accepted microsecond gain without profiler proof.

Problem: Compilation verification was required, but the machine violated the explicit build gate.
Solution: Run static verification and skip build under the CPU rule.
Rejected Alternatives: Starting a build at 91.50% CPU was rejected because AGENTS forbids builds when system CPU is under work above 50%.
Scalability potential: No runtime quality-tier claim is made from static-only verification.
Hardware Impact: 0 us accepted until Unity build/profiler/GC proof exists.
