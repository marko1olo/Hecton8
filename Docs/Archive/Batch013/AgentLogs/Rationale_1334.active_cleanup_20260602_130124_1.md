# Rationale 1334 - DOCUMENTATION_CONSOLIDATION_AND_FLUFF_PURGER

Date: 2026-05-26
Evidence class: STATIC_DOC + CLI_TEXT_SCAN

## Decision 001 - Scope Boundary

Problem: The prompt requests broad documentation surgery while parallel agents may be editing code and docs.
Solution: Limit write scope to `Docs/` and root `.md`/`.txt`; all executable assets are forbidden. Track all changed paths for boundary audit.
Rejected Alternatives: Editing source constants to match docs is outside assignment and would create cross-domain churn.
Scalability potential: Low/Middle/High/Ultra unaffected at runtime; clearer docs reduce wrong agent work across all hardware targets.
Hardware Impact: 0 runtime gain measured. Static process avoids build/editor CPU load on i3/MX350 host.

## Decision 002 - Evidence Language

Problem: Documentation cleanup can easily produce false verification claims.
Solution: Use `STATIC_DOC` and `CLI_TEXT_SCAN` labels only unless a fresh runtime artifact exists. No `VERIFIED` language for runtime claims.
Rejected Alternatives: Reporting source-text hits as runtime proof violates `QA_Evidence_Text_Filter_Audit`.
Scalability potential: Keeps future low/middle/high/ultra quality decisions tied to measured artifacts, not prose.
Hardware Impact: 0 runtime gain measured; prevents wasted compile/profiler passes from stale instructions.

## Decision 003 - Build Suppression

Problem: Prompt forbids compilation and the user explicitly bans `dotnet build`.
Solution: Use PowerShell/Python text scans only; maintain command log proof for zero build/editor invocation.
Rejected Alternatives: Running `dotnet build` to prove no source impact is unnecessary and forbidden for this docs-only task.
Scalability potential: Preserves CPU budget for parallel compile agents.
Hardware Impact: Avoids saturating low-end i3/MX350 workstation during concurrent work.

## Decision 004 - Superseded SignalBus Report Archive

Problem: Active `Docs/Reports` contained repeated 1303 SignalBus/tether audit revisions; eleven markdown variants alone consumed about 520k words and duplicated the same route topic.
Solution: Keep V16 as the current active evidence snapshot; move 47 older 1303 revision artifacts to `Docs/_Archive/Reports_1334_2026-05-26/SignalBus1303Superseded/` with SHA-256, mtime, byte-size, and original-path manifest.
Rejected Alternatives: Compressing raw evidence reports by hand would destroy forensic detail. Deleting old versions would break provenance.
Scalability potential: Low/Middle/High/Ultra runtime unchanged; future agents read one active proof instead of stale iterations, reducing wrong implementation routes.
Hardware Impact: 0 runtime gain measured. Static active-corpus read volume dropped by 473831 words before final report overhead.

## Decision 005 - TASTE Root Exception

Problem: `DOC_GOVERNANCE.md` says root text anchors are only `AGENTS.md`, `MASTER_RELEASE_WORK_PLAN.md`, and `BUILD_PLAYTEST_ISSUES.md`, but root `AGENTS.md` explicitly requires reading `TASTE.md`.
Solution: Leave `TASTE.md` in root and record it as an authority conflict/exception. Do not edit `AGENTS.md`; it is explicitly forbidden.
Rejected Alternatives: Moving `TASTE.md` into `Docs/` would make `AGENTS.md` stale and break the first-read workflow for gameplay/design reviews.
Scalability potential: Taste authority still enforces weak/middle/high/ultra presentation rules; no gameplay truth route changes.
Hardware Impact: 0 runtime gain measured.

## Decision 006 - Stale Data Monolith Wording Correction

Problem: The stricter 1334 contradiction fuzzer flagged two active sentences where `static_data.h8bin` could be read as absent or not yet real.
Solution: Reworded both to the current source/filesystem fact: `static_data.h8bin` exists; import/boot/runtime proof remains pending.
Rejected Alternatives: Suppressing the fuzzer pattern would hide a real comprehension hazard for future agents.
Scalability potential: Keeps Data Monolith readiness separated from runtime proof across low/middle/high/ultra targets.
Hardware Impact: 0 runtime gain measured; prevents agents from rebuilding or rebaking due to stale absence language.

## Decision 007 - Final Report Hash

Problem: A report cannot contain a SHA-256 of itself without changing its own bytes.
Solution: Store `selfSha256ExcludingSelfField`: SHA-256 over canonical sorted JSON before adding the self-hash fields.
Rejected Alternatives: Whole-file self-hash inside the same JSON is mathematically unstable without an external sidecar or fixed-point scheme.
Scalability potential: Static provenance only; no runtime scalability effect.
Hardware Impact: 0 runtime gain measured.

## Decision 008 - APEX Override Runtime Gate Scope

Problem: The rejection requested deep C#/runtime gates, but agent 1334's tracked write set contains no `.cs`, `.shader`, `.compute`, `.prefab`, or `.asset` files.
Solution: Run the gate scanner against the exact 1334 touched path set from `DOCUMENTATION_OPTIMIZATION_REPORT_1334.json`; report touched C# scope as empty instead of auditing unrelated work from other agents.
Rejected Alternatives: Auditing all dirty C# files would attribute other agents' changes to 1334 and create false ownership.
Scalability potential: Keeps runtime proof ownership exact; no low/middle/high/ultra runtime behavior changed by docs-only work.
Hardware Impact: 0 runtime gain measured; avoids wasting CPU on unrelated C# syntax scans.

## Decision 009 - Repeat APEX Proof Boundary

Problem: A repeated rejection can pressure the agent into claiming whole-codebase runtime certification outside the 1334 domain.
Solution: Re-extract the exact `1334` prompt block with a singleline regex, then re-scan only the 1334 tracked changed paths. Keep the final JSON green only because the touched C# scope is empty.
Rejected Alternatives: Expanding ownership to unrelated dirty C# files would create false attribution and violate the parallel-agent boundary model.
Scalability potential: No runtime behavior changed; the proof preserves exact ownership across weak/middle/high/ultra targets.
Hardware Impact: 0 runtime gain measured; no build/editor process invoked.

## Decision 010 - Empty Runtime Scope Is Not Runtime Certification

Problem: Repeated APEX demands can be misread as requiring a false all-code runtime certification from a docs-only agent.
Solution: Preserve the narrower truth: `1334` has zero touched C# files, so native-field, hot-path, AUP, lock, DTO, throw, and solver gates scan zero files. Report this as empty-scope verification only.
Rejected Alternatives: Running a whole-project C# audit would be useful as a separate task, but it would not prove `1334` implementation quality and would mix other agents' ownership.
Scalability potential: No runtime behavior changed; protects documentation governance from cross-domain evidence pollution.
Hardware Impact: 0 runtime gain measured; no compiler, editor, or heavy analyzer process invoked.
