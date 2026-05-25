# AUTOFIX4 Rationale

Date: 2026-05-25
Domain: Cross-domain diagnostic/runtime hygiene
Status: DONE - STATIC VERIFIED / BUILD GATED

Problem: Direct `Debug.*` calls remain spread through runtime gameplay, visual fallback, validation, and smoke paths. The problem is not the text of the messages; the problem is bypassing the project-owned diagnostic route.
Solution: Preserve messages and context objects while replacing direct Unity logger calls with `Hecton8.Core.H8Debug`. This keeps existing dev/build guards and avoids semantic gameplay changes.
Rejected Alternatives: A repository-wide blind replacement was rejected because bootstrap kill switches, signal ABI fences, and editor build validators need separate ownership review. Deleting diagnostics was rejected because black-box and validation evidence must remain visible.
Scalability potential: Low tier avoids unmanaged diagnostic scatter in bursty fallback paths; middle/high/ultra retain richer diagnostic text through the same facade without changing gameplay truth.
Hardware Impact: Expected low-end i3/MX350 gain is small and burst-bound, estimated in tens of microseconds during diagnostics storms. Runtime profiler proof remains PENDING VERIFICATION.

Problem: This pass runs inside a dirty multi-agent worktree.
Solution: Scope edits to explicit files discovered by `rg`, keep changes mechanical, and do not revert unrelated content.
Rejected Alternatives: Full subsystem refactors were rejected because they would collide with parallel agent work and exceed the diagnostic hygiene objective.
Scalability potential: Stable diagnostic ownership supports future quality-weighted telemetry cadence without changing DTO layouts.
Hardware Impact: No steady-frame gain is claimed without profiler evidence.

Problem: A 32-file slice still included several systems with validator and black-box dump diagnostics.
Solution: Keep validation/dump semantics exactly intact while redirecting the visible failure route through `H8Debug`, including context overloads and exception routes where present.
Rejected Alternatives: Rebuilding validation ownership in this pass was rejected because DTO contracts and black-box dump formats are separate authority surfaces.
Scalability potential: Low tier keeps the cheapest visual/runtime fallback diagnostics; middle/high/ultra can preserve richer report text through the same channel.
Hardware Impact: Expected benefit is reduced diagnostic-route scatter and cleaner future gating. Microsecond savings are not accepted until profiler proof exists.

Problem: Build verification was requested by mandate, but the machine was above the allowed load gate.
Solution: Run static scoped verification and skip compilation under the explicit AGENTS CPU rule.
Rejected Alternatives: Starting `dotnet` under 64.18% CPU was rejected because the local rule forbids builds when CPU is under work above 50%.
Scalability potential: No runtime quality-tier claim is made from static diagnostics.
Hardware Impact: 0 us accepted until a clean build and profiler/GC proof are captured.
