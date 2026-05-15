# Rationale: DOC_HONEST_ANALYSIS

Date: 2026-05-15
Domain: Documentation Integrity / Echelon 9 Meta, Polish & Integration
Evidence class: STATIC_DOC + FILESYSTEM + STATIC_SOURCE + CLI_COMPILE

## Decision 1: Audit Scope

Problem: The user asked to continue honest document analysis after root cleanup. The risk is wasting time rewriting historical snapshots instead of finding current contradictions that mislead future agents.

Solution: Audit active/stable documentation and current report indexes for stale counters, moved-root references, missing linked artifacts, and verification claims above available evidence class. Deprecated/archive/task/log folders are excluded from broad scans unless needed for provenance.

Rejected Alternatives: Full recursive rewrite was rejected because historical dated reports should remain snapshots. Chat-only conclusions were rejected because project protocol requires file-backed status/rationale/logs.

Scalability potential: Low/Middle/High/Ultra runtime behavior unchanged. Cleaner docs reduce agent context pollution and false work.

Hardware Impact: Runtime microseconds saved on i3/MX350: 0 claimed.

## Decision 2: Patch Current Indexes, Preserve Historical Reports

Problem: Some active navigation docs had stale root wording after the cleanup, while many dated reports are intentionally historical.

Solution: Patch current index/navigation surfaces only: `Docs/README.md`, `Docs/Reports/README.md`, Archivarius atlas/classification, compute report index wording, and the compute bundle README. Create a new dated report for the audit findings.

Rejected Alternatives: Rewriting all old dated reports was rejected because it destroys snapshot value and creates high churn. Ignoring current index drift was rejected because it misleads future agents.

Scalability potential: Low/Middle/High/Ultra runtime behavior unchanged. Fewer stale paths in active indexes reduce false task branching.

Hardware Impact: Runtime microseconds saved on i3/MX350: 0 claimed.

## Decision 3: No Broad Manifest Regeneration

Problem: Broad documentation/source counts are volatile under parallel agents, and a full manifest refresh would be a separate task.

Solution: Record narrow counts from this pass and explicitly demote May 13 broad counters to historical orientation where not rerun.

Rejected Alternatives: Promoting the narrow scan to a full manifest was rejected because it did not scan every active path with the same rules as DOC_AUDIT. Claiming all docs honest was rejected because static grep cannot prove that.

Scalability potential: Process-only; runtime unchanged.

Hardware Impact: Runtime microseconds saved on i3/MX350: 0 claimed.

## Decision 4: Demote Stale Current/Latest Pointers In Archivarius Navigation

Problem: Active Archivarius navigation files still treated May 11 manifest/continuation and May 4 actuality sweep as latest/current counter or project-truth boundaries. That misroutes future agents even though the reports themselves are historical snapshots.

Solution: Patch only active navigation surfaces and domain-map trust notes so May 13 DOC_AUDIT X-Ray and May 15 documentation honest analysis are read before older counters, root paths, and build-artifact claims. Keep historical report files intact.

Rejected Alternatives: Rewriting every dated report was rejected because it destroys forensic snapshot value. Leaving domain-map top notes unchanged was rejected because they are active entry points, not archival prose.

Scalability potential: Runtime Low/Middle/High/Ultra unchanged. Documentation routing is cleaner for cheap and high-end hardware work because agents start from current evidence boundaries before consuming old subsystem notes.

Hardware Impact: Runtime microseconds saved on i3/MX350: 0 claimed.

## Decision 5: Clear Transient Core GPR Reference Drift, Not World GPR Runtime

Problem: A fresh H-Phi summary after concurrent churn showed Core asmdef debt at `26`, one above the R49 accepted ceiling. The optional unused-reference scan identified `Hecton8.World.GPR` as a high-confidence unused Core asmdef reference with `SourceInCoreCompileSurfaceCount=0` during transient workspace/index drift.

Solution: Align current file/index state so `Assets/_Project/Scripts/Hecton8.Core.asmdef` contains no `Hecton8.World.GPR` Core reference, then rerun the Core graph gate and Core CLI compile. World GPR runtime and contracts were not changed.

Rejected Alternatives: Broad Core graph cleanup was rejected because it crosses many owner domains. Editing C# GPR/runtime code was rejected because this task owns documentation/integration hygiene, not World implementation. Keeping the unused reference was rejected because it is measurable H-Phi dependency debt with direct tool evidence.

Scalability potential: Low/Middle/High/Ultra runtime behavior unchanged. Smaller Core dependency surface reduces compile graph coupling and helps future hardware-specific systems stay isolated behind contracts.

Hardware Impact: Runtime microseconds saved on i3/MX350: 0 claimed; no runtime path changed.
