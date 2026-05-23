# Rationale SHINOBU_359

Evidence class: STATIC_SOURCE unless a command output is listed.

## Decision 01 - Existing Gate Ownership

Problem: The task asks for an offline `.asmdef` dependency validator, while the repository already contains `Tools/AssemblyDependencyAudit.py` and `Docs/QUALITY_GATES.md` names it as the assembly dependency / compile-wall gate.
Solution: Extend `Tools/AssemblyDependencyAudit.py` in place and preserve its existing CLI shape. This obeys the DOD rule "one fact -> one owner -> one route -> one proof artifact."
Rejected Alternatives: Creating a new standalone validator would duplicate exit-code ownership and split CI evidence.
Scalability potential: Low/Middle/High/Ultra all benefit from shorter compile walls and deterministic CI; no runtime quality switch is introduced.
Hardware Impact: 0 us/frame runtime. Offline estimate is tens of milliseconds on i3/MX350-class storage if mmap is used for small JSON files.

## Decision 02 - Domain Boundary

Problem: The XML prompt includes C# Editor windows, Scene gizmos, Vault DTO mutation, and Play Mode claims, but the engineering identity and user request are offline Python preflight.
Solution: Keep implementation read-only in `Tools/` and write reports under `Docs/Reports` / `Docs/AgentLogs`. Mark C# editor/gizmo tasks as domain-boundary pending unless explicit scope changes.
Rejected Alternatives: Modifying C# editor tooling during an offline gate task would violate the Global Authority Law and risk compile walls.
Scalability potential: Low uses static gate only; Middle/High/Ultra can consume richer reports without changing runtime truth.
Hardware Impact: Avoids Unity compile/import churn on i3/MX350 and protects machines already running other agents.

## Decision 03 - Strict Core.Contracts Boundary As A Gate, Not Auto-Fix

Problem: Current serialized asmdef graph has `148` cross-domain runtime references that do not route through `Hecton8.Core.Contracts`, plus `17` concrete sibling refs from `Hecton8.Core`. Blind deletion would break active domains and other agents.
Solution: Add strict read-only detection and hard-fail flag `--fail-on-core-contract-boundary`. The report lists offending assemblies and references while leaving source assets untouched.
Rejected Alternatives: Editing `.asmdef` references without source call-site migration, contract/facade route, and Unity import proof.
Scalability potential: Low/Middle/High/Ultra all gain compile-wall visibility; no gameplay truth changes.
Hardware Impact: 0 us/frame. Prevents future CI/iteration stalls on i3/MX350 by failing bad graph growth before Unity import.

## Decision 04 - C# Layout Parser Scope

Problem: The first full-token C# scan took `67125.172 ms`, then `29664.476 ms` after coarse prefiltering, and tripped the 10s watchdog. That violates the offline gate budget.
Solution: Replace full-file token scan in production path with deterministic line-window state parsing around struct declarations, `StructLayout`, `FieldOffset`, properties, and AUP float-risk lines. Keep `--test` fixtures for parser regression.
Rejected Alternatives: Raising watchdog time or hiding the binary/schema pass behind prose.
Scalability potential: Low tier can run asmdef-only gate quickly; higher CI tiers can run binary/schema audit and consume richer hotspots. No runtime visual tier switch is introduced.
Hardware Impact: Latest warm binary/schema audit is `156.111 ms` in the full deep pass and no longer flags `Performance_Regression_Warning`; default asmdef graph audit remains the cheaper merge gate.

## Decision 05 - Binary Report Semantics

Problem: The prompt demands `Docs/Reports/BINARY_SCHEMA_AUDIT_REPORT.json` and metric rows, but current source contains legacy layout/AUP/property debt. A default hard fail would break the existing broad audit command unexpectedly.
Solution: Keep default `python Tools/AssemblyDependencyAudit.py` as PASS_WITH_WARNINGS unless explicit fail flags are passed; strict flags return `1` for asmdef gates, `2` for ARM64, `3` for CS1612 property, `4` for schema mismatch, and `6` for AUP precision.
Rejected Alternatives: Silent warnings without exit-code controls, or default-breaking every existing CI call before integrator approves the new gate.
Scalability potential: Low CI uses cycle/core-boundary hard flags; deeper validation tiers enable binary/schema flags as debt is burned down.
Hardware Impact: 0 us/frame. Current binary report found `60` ARM64, `276` CS1612, `112` AUP, `0` schema mismatches.

## Decision 06 - Offline CSV Profiles Instead Of Vault Mutation

Problem: Task 17 asks for a `binary_schema_profiles.csv` parser and Vault-backed unmanaged lookup writes, while the SHINOBU_359 prompt explicitly forbids C# source or asset metadata mutation and restricts the agent to `Tools/`.
Solution: Add `--schema-profile-csv` to the Python gate. It reads the CSV with mmap byte slicing, parses bounded rows, computes deterministic FNV-1a hashes, and records profile/parse-error counts in `BINARY_SCHEMA_AUDIT_REPORT.json`. Vault writes are not performed by this offline gate.
Rejected Alternatives: Adding a C# cold-boot parser or mutating `GlobalDataVault` would violate the Tools-only/global-authority boundary and trigger compile-wall risk.
Scalability potential: Low CI can omit the CSV if absent; Middle/High/Ultra CI can provide stricter profile rows without changing runtime DTO layout or authority routes.
Hardware Impact: 0 us/frame. On i3/MX350-class machines the CSV parse is bounded by file size and uses one mmap read.

## Decision 07 - OOP Scanner Prefilter Under Watchdog

Problem: The first combined `--binary-schema-audit --oop-test-audit --write-self-audit` deep pass exceeded the default 10s watchdog because the OOP scanner stripped comments/strings in every C# file after the binary audit had already scanned 2367 files.
Solution: Add a cheap mmap prefilter for OOP-relevant tokens (`Physics`, `GameObject`, `Instantiate`) plus World/test markers before the exact comment/string stripping pass. The combined deep pass now survives the strict 10s watchdog and the cached OOP report records `204.209 ms`.
Rejected Alternatives: Raising the default watchdog or disabling OOP self-audit would hide the host-tool cost instead of removing it.
Scalability potential: Low CI runs asmdef-only; Middle/High/Ultra CI can enable OOP and binary passes. The gate depth changes CI proof richness, not runtime truth.
Hardware Impact: 0 us/frame. Offline host scan cost reduced enough for the combined run to finish under the default watchdog on the current dirty workspace.

## Decision 08 - Self-Audit Truth Over Runtime DTO Fiction

Problem: The polish mandate asks for DTO byte offsets, Vault BufferIDs, JobHandles, and GlobalQualityWeight curves, but SHINOBU_359 introduced no C# runtime payload, no Burst job, no NativeArray, no Vault lane, and no gameplay tick.
Solution: Generate `Docs/Reports/SHINOBU_359_SELF_AUDIT.xml` stating the exact non-runtime status: primary runtime DTO byte size `0`, Vault BufferIDs `none`, Burst jobs `0`, job handles `none`, and compile guard counters from the asmdef graph.
Rejected Alternatives: Inventing a fake DTO/BufferID ledger entry or adding C# editor/gizmo code against the explicit Tools-only law.
Scalability potential: The offline gate scales CI validation depth continuously through CLI flags; runtime GlobalQualityWeight is intentionally not touched.
Hardware Impact: 0 us/frame. The report prevents later agents from assuming nonexistent runtime memory ownership.

## Decision 09 - Runtime Using Boundary Scanner

Problem: A clean `.asmdef` DAG is necessary but not sufficient. Runtime C# files can still import sibling namespaces through `using Hecton8.*`, creating source-level coupling that later forces asmdef edges or compile-wall workarounds.
Solution: Add `--using-leak-audit` and `--fail-on-using-boundary`. The scanner maps each C# file to the nearest owning asmdef scope, skips editor assemblies, and flags runtime imports where `domain_key(using)` differs from the owner domain and the namespace is not `Hecton8.Core.Contracts`.
Rejected Alternatives: Editing `using` statements directly. That would break code without first extracting Core.Contracts DTOs/facades and proving Unity import.
Scalability potential: Low CI can run asmdef-only; Middle/High/Ultra CI can enable using-boundary strictness. The validation depth changes proof coverage only, not gameplay truth or runtime quality.
Hardware Impact: 0 us/frame. Current cached pass reports `2193` source-level using-boundary violations and uses a source-tree stamp cache; an earlier full deep report was slower, and the current canonical using report records `444.154 ms`.

## Decision 10 - Source Tree Stamp Cache

Problem: The first using-boundary scan repeatedly normalized paths and loaded per-file findings, pushing the full binary+OOP+using deep command over the default 10s watchdog.
Solution: Add a source-tree stamp over runtime-mapped C# paths, file size, mtime, owning assembly, and domain. If the stamp matches, the scanner reuses the aggregate proof. If it changes, the scanner rescans all runtime files and rewrites the cache. `normalize_path_no_resolve()` avoids repeated `Path.resolve()` filesystem calls in this hot offline path.
Rejected Alternatives: Increasing the default watchdog or adding folder skip lists. Both would hide the cost or violate the "every file same gate" rule.
Scalability potential: Low-end developer machines get fast repeated strict audits; high-end CI still gets the full cold proof when source changes.
Hardware Impact: 0 us/frame. Host-only wall time for the full deep command returned under the 10s watchdog after this change.

## Decision 11 - OOP Aggregate Cache

Problem: Even after using-boundary cache, the full binary+OOP+using deep command could exceed the default 10s watchdog under concurrent workspace churn because the OOP scanner kept re-reading and comment/string-stripping unchanged C# files.
Solution: Add `Docs/Reports/QA_OPTIMIZATION_REPORT_SHINOBU_359.cache.json` keyed by a C# source-tree stamp. If the stamp matches, the OOP scanner reuses the aggregate finding proof; if any C# path/size/mtime changes, it rescans.
Rejected Alternatives: Raising the default watchdog or omitting OOP from the deep self-audit command. Both weaken the preflight gate.
Scalability potential: Low-end developer machines get fast repeated OOP strict checks; CI still cold-scans when source changes.
Hardware Impact: 0 us/frame. Latest cached OOP report elapsed `204.209 ms`; full deep command returned under the default 10s watchdog after warm cache.

## Decision 12 - Binary Aggregate/File Cache Split

Problem: The binary/schema pass still spent `5416.293 ms` on a cold split-cache migration and the earlier all-in-one cache pushed exact warm reads to roughly `750 ms` because Python had to parse an 8.7MB file just to confirm the aggregate proof.
Solution: Split binary cache ownership into a small aggregate stamp cache at `Docs/Reports/ASSEMBLY_BINARY_SCHEMA_AUDIT_SHINOBU_359.cache.json` and a large per-file parse cache at `Docs/Reports/ASSEMBLY_BINARY_SCHEMA_AUDIT_SHINOBU_359.filecache.json`. Exact tree matches load only the small aggregate. Aggregate misses load the file cache and rescan only changed C# files. The schema input collector excludes the audit's own report/cache/metric output files exactly, so generated proof files cannot become input schemas when a broad schema root is supplied.
Rejected Alternatives: Keeping a single large cache file, raising the watchdog, or invalidating every parsed struct when one file changes. Broad folder skips were also rejected because the SHINOBU_359 prompt requires every input file to obey the same gate.
Scalability potential: Low-end developer machines avoid reparsing unchanged C# structs (`cacheMisses=0` latest); best isolated exact warm proof is now `116.059 ms`, and the latest full deep binary subpass is `156.111 ms`. Middle/High/Ultra CI can still run cold or partial invalidation passes with the same strict findings and no runtime quality switch.
Hardware Impact: 0 us/frame. Latest warm binary summary: `2368` C# files stamped, `4807` structs represented, `60` ARM64 findings, `276` CS1612 findings, `112` AUP findings, `cacheHit=True`, `cacheMisses=0`, `Performance_Regression_Warning=False`; the remaining cost is git index/status proof plus report serialization, not C# parse rerun.

## Decision 13 - Git Index Binary Stamp

Problem: Exact warm binary cache hits still fluctuated above the 500ms warning threshold because Python enumerated and statted all `Assets/_Project/Scripts/**/*.cs` files before proving the aggregate cache.
Solution: Use the `.git/index` file size/mtime as the tracked/staged invalidator and use size/mtime stats only for dirty, untracked, or deleted paths reported by `git status --porcelain=v1 -z --untracked-files=all`. Fallback remains the filesystem tree stamp when the source root is not the production script tree or git is unavailable.
Rejected Alternatives: Trusting a stale aggregate cache without source proof, raising the threshold, or excluding dirty files from the stamp. Those options would weaken the gate.
Scalability potential: Low-end developer machines get a faster repeated binary proof; CI still gets exact invalidation when the git index, dirty file mtimes, untracked files, deleted files, schema roots, or schema profile CSV change.
Hardware Impact: 0 us/frame. Current `csTree` proof mode is `git-index-file+dirty-stat`: `.git/index` stamps clean tracked/staged state, `648` dirty files are stat-backed, `2` deleted files are token-backed, and `172` untracked files are stat-backed; latest full deep binary elapsed `156.111 ms`.
