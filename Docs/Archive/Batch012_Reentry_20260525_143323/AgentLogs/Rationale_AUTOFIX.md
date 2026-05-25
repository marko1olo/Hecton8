# AUTOFIX Rationale

Date: 2026-05-25
Status: ACTIVE / PENDING VERIFICATION

## Scope Decision

Problem: User requested broad autonomous repair, but AGENTS forbids fake reports, public API mutation without confirmation, YAML mutation risk, and runtime readiness claims from static scans.
Solution: Start with static defects that are cross-domain, locally fixable, and low-risk: unguarded runtime diagnostics and release-string allocation risks. This removes measurable hygiene debt without changing gameplay truth, prefab data, save identity, or public contracts.
Rejected Alternatives: Architecture rewrites, project settings edits, prefab YAML edits, and speculative systems work. Those require route cards, Unity validation, or domain ownership proof.
Scalability potential: Low/MX350 removes release diagnostic formatting and console noise; Middle/High/Ultra keep development diagnostics in editor/dev builds while preserving visual budget for taste-facing effects.
Hardware Impact: Expected micro savings are per-call small, but guarded diagnostics prevent pathological spikes/log spam and release heap strings on i3/MX350.

## Mandate Mapping

Problem: Work spans many domains.
Solution: Apply only mandates with direct static relevance: Zero-GC hot paths, Debug Log Hygiene, Execution Phases, Performance Budget, Visual Fake First.
Rejected Alternatives: Reading every mandate before first edit; high context cost with no direct improvement.
Scalability potential: Same policy across tiers; debug visibility remains in dev builds, release remains quiet.
Hardware Impact: Prevents string-format allocation and logger overhead on weak silicon; no gameplay cadence changes.

## Pass 1 Diagnostic Route

Problem: Multiple runtime systems emitted naked `Debug.Log*` / `Debug.LogException` diagnostics from runtime or cold-failure paths. Some built interpolated/concatenated strings before logging.
Solution: Route selected diagnostics through existing `Hecton8.Core.H8Debug` conditional methods so editor/development builds keep evidence while release builds omit the call and its argument construction.
Rejected Alternatives: Adding new logger API overloads would expand public surface; raw `#if` around every site would increase local preprocessor clutter; leaving naked logs relies on console behavior instead of compile-time stripping.
Scalability potential: Low/MX350 avoids release diagnostic formatting on fault paths; Middle keeps clean release behavior; High/Ultra retain dev diagnostics when built as development players without changing visual features.
Hardware Impact: Per-site gain is microseconds only on fault paths, but removes release string construction and logger dispatch from systems that already own black-box or telemetry routes.

## Pass 2 Diagnostic Ceiling

Problem: After Pass 1, selected touched systems still had guarded-but-raw `Debug.*` calls, creating mixed diagnostic policy and future copy-paste risk.
Solution: Normalize the remaining selected sites to `H8Debug` until the user-mandated 20-40 source-file window reached exactly 40 files.
Rejected Alternatives: Touching more than 40 files, replacing every project log blindly, or adding a new logger overload. Broad replacement would cross ownership boundaries and increase compile risk in an already dirty tree.
Scalability potential: Same behavior on Low/Middle/High/Ultra release builds: diagnostics stripped; development builds retain evidence. Saved CPU/GC budget is available for taste-facing visuals, not new gameplay truth.
Hardware Impact: i3/MX350 avoids fault-path string formatting and Unity logger dispatch in release. Exact runtime savings require profiler proof; static expected range remains 2-12 us per emitted diagnostic path.

## Compile Gate

Problem: AGENTS forbids `dotnet` build when CPU is under load or compiler process is active.
Solution: Checked `dotnet`/`csc` process list and CPU. No compiler process was active, but CPU averaged 77.1%.
Rejected Alternatives: Running build anyway to manufacture a report; violates explicit guard.
Scalability potential: None. This is process safety.
Hardware Impact: Prevented build contention on a busy workstation.
