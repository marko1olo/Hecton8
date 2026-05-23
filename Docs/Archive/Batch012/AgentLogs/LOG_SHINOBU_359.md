# LOG SHINOBU_359

## 2026-05-23 H_PHI_ASSEMBLY_COMPLIANCE_GATE

What was wrong:
- Existing `Tools/AssemblyDependencyAudit.py` only reported cycles/core concrete refs/runtime concrete refs. It did not expose strict `Hecton8.Core.Contracts` boundary violations, unresolved first-party/GUID references, duplicate assembly names, binary/schema report output, ARM64 struct layout checks, CS1612 property checks, or AUP precision checks.
- `Docs/QUALITY_GATES.md` already named `Tools\AssemblyDependencyAudit.py`; adding a second validator would split gate ownership.

What was done:
- Extended `Tools/AssemblyDependencyAudit.py` in place.
- Added mmap-backed readers for `.asmdef`, `.meta`, C# source, JSON schema files, and generated report reads.
- Added bounded thread-pool loading for asmdefs and C# audit files.
- Added DAG payload with nodes, edges, acyclic status, topological order, cycles, unresolved references, and duplicate assembly names.
- Added strict Core.Contracts route gate: `--fail-on-core-contract-boundary`.
- Added strict unresolved refs gate: `--fail-on-unresolved-first-party-refs`.
- Added optional binary/schema gate: `--binary-schema-audit`.
- Added ARM64 alignment, Pack=1, runtime bool, size multiple-of-8, CS1612 property, schema offset, and AUP float/cast checks.
- Added watchdog default: `--watchdog-seconds 10.0`.
- Added self-contained mock fixtures and regression checks via `--test`.
- Updated `Tools/test_assembly_dependency_audit.py` to cover strict boundary and binary parser checks.
- Wrote/updated reports:
  - `Docs/AgentLogs/AssemblyDependencyAudit_HFI_AUDIT.json`
  - `Docs/AgentLogs/AssemblyDependencyAudit_HFI_AUDIT.md`
  - `Docs/Reports/BINARY_SCHEMA_AUDIT_REPORT.json`
  - `Docs/Reports/METRIC_PHI_DATA_TRUTH_AUDIT.json`

Cinematic Cheats used:
- None. This is offline CI/static validation, not a visual or physical simulation. The relevant cheat is compile-wall prevention: fail graph debt before Unity import burns developer time.

Exact Microseconds saved:
- Runtime frame: `0 us`; no runtime Unity path changed.
- Offline measured facts:
  - First full-token binary/schema pass: `67125.172 ms`, killed by 10s watchdog on normal run.
  - Coarse prefilter pass: `29664.476 ms`, still too slow.
  - Final line-window pass: `8800.127 ms`, exits under the 10s watchdog but still emits `Performance_Regression_Warning` because it exceeds 500ms.
  - Default asmdef-only audit wall observed around `1.7 s` in this dirty workspace.

Verification:
- `python -m py_compile Tools\AssemblyDependencyAudit.py Tools\test_assembly_dependency_audit.py`: exit `0`.
- `python Tools\test_assembly_dependency_audit.py`: exit `0`, `5` tests.
- `python Tools\AssemblyDependencyAudit.py --test`: exit `0`, `SELF_TEST_PASS`.
- `python Tools\AssemblyDependencyAudit.py`: exit `0`, `172` asmdefs, `0` cycles, `148` Core.Contracts boundary violations, `1` unresolved ref.
- `python Tools\AssemblyDependencyAudit.py --fail-on-cycles`: exit `0`.
- `python Tools\AssemblyDependencyAudit.py --binary-schema-audit`: exit `0`, report written, `60` ARM64, `276` CS1612, `112` AUP, `0` schema mismatches, elapsed `8800.127 ms`.
- `python Tools\AssemblyDependencyAudit.py --fail-on-core-contract-boundary`: exit `1`, expected on `148` current violations.
- `python Tools\AssemblyDependencyAudit.py --fail-on-unresolved-first-party-refs`: exit `1`, expected on `Hecton8.Input.Generated`.

Residual risks:
- Static source only. No Unity import, Unity Console, player build, profiler, GCMonitor, or generated project compile proof was run.
- Binary/schema C# parser is intentionally lightweight and deterministic. It is a gate, not a Roslyn semantic compiler.
- Tasks 16 and 18 from XML require C# Editor/Scene tooling and were blocked by the offline Python domain boundary.

## 2026-05-23 Polish Pass - CSV/OOP/Self-Audit Closure

What was wrong:
- Task 17 was only partially represented by JSON schema inputs; no `binary_schema_profiles.csv` parser existed.
- Task 19 was not executed in the first pass; no SHINOBU_359-owned `QA_OPTIMIZATION_REPORT.json` section existed.
- No XML self-audit artifact existed on disk.
- The first combined deep command `--binary-schema-audit --oop-test-audit --write-self-audit` exceeded the default 10s watchdog because the OOP scanner stripped comments/strings for every C# file after the binary scan.

What was done:
- Added `--schema-profile-csv` with mmap byte parsing, bounded CSV slicing, deterministic FNV-1a profile hashes, and binary report counters.
- Added `--oop-test-audit` / `--fail-on-oop-test-findings`. The scanner prefilters by mmap token markers before exact comment/string stripping and upserts `shinobu359AssemblyComplianceGate` into `Docs/Reports/QA_OPTIMIZATION_REPORT.json`.
- Added `--write-self-audit` and generated `Docs/Reports/SHINOBU_359_SELF_AUDIT.xml` with the 20-task reconciliation, compile guard counts, non-runtime DTO/Vault status, and Dear Lie proof.
- Updated `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` with a SHINOBU_359 boundary entry.
- Extended `Tools/test_assembly_dependency_audit.py` to `7` tests, covering CSV FNV parsing and OOP Physics/GameObject detection.

Cinematic Cheats used:
- Static graph proof replaces Unity import probing and manual review: O(N+E) asmdef DAG validation plus optional O(F) mmap scans. No scene load, GameObject construction, Physics calls, or dotnet rebuild.

Exact Microseconds saved:
- Runtime frame: `0 us`; no Unity runtime path changed.
- Binary/schema deep pass latest: `4389.933 ms`, down from the previous `8800.127 ms`, still correctly marked `Performance_Regression_Warning`.
- OOP scanner latest: `958.506 ms` after mmap prefiltering.
- Combined binary+OOP+self-audit command survived the strict default `10.0 s` watchdog and finished with exit `0` under the shell `20 s` cap.

Verification:
- `python -m py_compile Tools\AssemblyDependencyAudit.py Tools\test_assembly_dependency_audit.py`: exit `0`.
- `python Tools\test_assembly_dependency_audit.py`: exit `0`, `7` tests.
- `python Tools\AssemblyDependencyAudit.py --test`: exit `0`, `SELF_TEST_PASS`.
- `python Tools\AssemblyDependencyAudit.py --fail-on-cycles`: exit `0`, graph remains acyclic.
- `python Tools\AssemblyDependencyAudit.py --binary-schema-audit --oop-test-audit --write-self-audit`: exit `0`; binary `60` ARM64, `276` CS1612, `112` AUP, `0` schema mismatches; OOP `18` findings.
- `python Tools\AssemblyDependencyAudit.py --fail-on-core-contract-boundary`: exit `1`, expected on current `148` violations.
- `python Tools\AssemblyDependencyAudit.py --fail-on-unresolved-first-party-refs`: exit `1`, expected on `Hecton8.Input.Generated`.
- `python Tools\AssemblyDependencyAudit.py --oop-test-audit --fail-on-oop-test-findings`: exit `1`, expected on current `18` OOP test findings.

Residual risks:
- C# UI Toolkit tuner (Task 16) and Scene Gizmo (Task 18) remain blocked by the explicit Tools-only/no C# modification law in the SHINOBU_359 prompt.
- Static source only. No Unity import, Console, player build, profiler, GCMonitor, or dotnet compile was executed.

## 2026-05-23 Polish Pass - Runtime Using Boundary Audit

What was wrong:
- The `.asmdef` DAG gate caught serialized cross-assembly edges, but it did not catch source-level `using Hecton8.*` imports inside runtime C# files. Those imports are the early leak before somebody adds a sibling asmdef reference to make Unity compile.
- The first using-boundary implementation did a full per-file ownership/check pass every time and could push the combined deep command over the 10s watchdog on the current workspace.

What was done:
- Added `--using-leak-audit`, writing `Docs/Reports/ASSEMBLY_USING_BOUNDARY_AUDIT_SHINOBU_359.json`.
- Added `--fail-on-using-boundary`, returning exit `1` on source-level cross-domain runtime imports.
- Added a no-resolve path helper and a source-tree stamp cache at `Docs/Reports/ASSEMBLY_USING_BOUNDARY_AUDIT_SHINOBU_359.cache.json`.
- Integrated using-boundary findings into `Docs/Reports/SHINOBU_359_SELF_AUDIT.xml`.
- Extended Python tests to `8` cases, including a mock cross-domain `using Hecton8.AI.Cognition` from a `Hecton8.World.Runtime` file.

Cinematic Cheats used:
- Compile-wall import probing is still replaced with static evidence. The new check is namespace-token proof tied to nearest asmdef scope, not Unity project generation, scene load, or dotnet compile.

Exact Microseconds saved:
- Runtime frame: `0 us`; no runtime Unity code changed.
- Current full deep command with binary+OOP+using+self-audit exits under the default 10s watchdog.
- That full deep report: binary elapsed `3789.109 ms`; OOP cached elapsed `204.209 ms`; using-boundary elapsed `1162.454 ms`; OOP findings `18`; using findings `2193`.

Verification:
- `python -m py_compile Tools\AssemblyDependencyAudit.py Tools\test_assembly_dependency_audit.py`: exit `0`.
- `python Tools\test_assembly_dependency_audit.py`: exit `0`, `8` tests.
- `python Tools\AssemblyDependencyAudit.py --test`: exit `0`, `SELF_TEST_PASS`.
- `python Tools\AssemblyDependencyAudit.py --fail-on-cycles`: exit `0`; current asmdefs `173`, cycles `0`.
- `python Tools\AssemblyDependencyAudit.py --binary-schema-audit --oop-test-audit --using-leak-audit --write-self-audit`: exit `0`.
- `python Tools\AssemblyDependencyAudit.py --using-leak-audit --fail-on-using-boundary`: exit `1`, expected on `2193` findings.
- `python Tools\AssemblyDependencyAudit.py --oop-test-audit --fail-on-oop-test-findings`: exit `1`, expected on `18` findings; cached report elapsed `204.209 ms`.

Residual risks:
- `using` scanner is namespace-token based, not Roslyn semantic binding. It is a pre-import hard gate for obvious source leaks.
- The `2193` findings are current repository debt. They were not auto-fixed because each requires contract extraction or facade migration.

## 2026-05-23 Polish Pass - Binary Split Cache

What was wrong:
- Binary/schema audit was still the dominant offline cost. A cold split-cache migration pass measured `5416.293 ms`; the prior all-in-one binary cache also made exact warm hits parse an 8.7MB JSON cache, causing warm binary timing above the performance warning threshold.
- If a caller used a broad schema root containing reports, the audit's own cache/report files could be mistaken for schema inputs because their filenames contain `binary` or `schema`.

What was done:
- Added `--binary-cache-path` for a small aggregate proof cache: `Docs/Reports/ASSEMBLY_BINARY_SCHEMA_AUDIT_SHINOBU_359.cache.json`.
- Added `--binary-file-cache-path` for large per-file C# parse entries: `Docs/Reports/ASSEMBLY_BINARY_SCHEMA_AUDIT_SHINOBU_359.filecache.json`.
- Changed exact warm binary hits to load only the small aggregate cache; file cache loads only when the aggregate stamp is invalid.
- Added exact output-file exclusion for report/cache/metric paths during schema input discovery.
- Extended unit tests to `11` cases, including aggregate cache reuse and file-cache reuse after a schema-profile change.
- Updated self-audit XML to record binary cache hit/miss/timing status.

Cinematic Cheats used:
- Manual C# layout review and Unity import probing remain replaced by static mmap/source proof. Cache split avoids repeating the O(F) source parse on unchanged inputs; exact warm path is stamp/aggregate proof only.

Exact Microseconds saved:
- Runtime frame: `0 us`; no Unity runtime path changed.
- Offline binary warm path observed in this pass: `343.276 ms`, `cacheHit=True`, `cacheHits=2368`, `cacheMisses=0`, `Performance_Regression_Warning=False`.
- Full deep warm command under default `10.0 s` watchdog: exit `0`; binary `343.276 ms`, using-boundary `692.06 ms`, OOP findings `18`, using findings `2193`.

Verification:
- `python -m py_compile Tools\AssemblyDependencyAudit.py Tools\test_assembly_dependency_audit.py`: exit `0`.
- `python Tools\test_assembly_dependency_audit.py`: exit `0`, `11` tests.
- `python Tools\AssemblyDependencyAudit.py --test`: exit `0`, `SELF_TEST_PASS`.
- `python Tools\AssemblyDependencyAudit.py --binary-schema-audit --watchdog-seconds 0`: exit `0`; migration/cold binary elapsed `5416.293 ms`.
- `python Tools\AssemblyDependencyAudit.py --binary-schema-audit --oop-test-audit --using-leak-audit --write-self-audit`: exit `0` under default watchdog; binary elapsed `343.276 ms`.

Residual risks:
- Cold first-run binary parsing still exceeds the 500ms warning threshold; the cache path is the practical developer-loop route. CI can still enforce cold proof by deleting caches before a scheduled deep run.
- Static source only. No Unity import, generated project compile, profiler, GCMonitor, or player-build proof was executed.

## 2026-05-23 Metric Correction - Warm Cache Filesystem Cost

What was wrong:
- The binary split-cache section above recorded `343.276 ms` as latest. That was a valid observed warm run, but a later full deep pass rewrote the canonical binary report with a slower filesystem-stamp result. This section is superseded by the later git-index-file stamp proof below.
- The parser cache still hit (`cacheHit=True`, `cacheHits=2368`, `cacheMisses=0`). The warning is caused by filesystem enumeration and source/schema/profile stamping under active workspace IO, not by reparsing unchanged C# files.

What was done:
- Updated `Docs/Tasks/Status_SHINOBU_359.md`, `Docs/AgentLogs/Rationale_SHINOBU_359.md`, and `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` to match the current report values.
- Left the earlier `343.276 ms` value documented as best observed warm cache timing, not current latest.

Cinematic Cheats used:
- Same offline static proof path. No Unity import, scene probing, GameObject construction, Physics calls, or dotnet rebuild.

Exact Microseconds saved at that point:
- Runtime frame: `0 us`; no Unity runtime path changed.
- Then-canonical binary warm proof exceeded the performance warning threshold with `cacheMisses=0`; later git-index-file stamping superseded this result.
- Then-canonical using-boundary proof reported `2193` findings; later cached passes superseded its timing.

Verification at that point:
- JSON report `Docs/Reports/ASSEMBLY_BINARY_SCHEMA_AUDIT_REPORT_SHINOBU_359.json` showed `cacheHit=True`, `cacheMisses=0`, and a warning-threshold breach at that point.
- JSON report `Docs/Reports/ASSEMBLY_USING_BOUNDARY_AUDIT_SHINOBU_359.json` showed `violationCount=2193` at that point.

Residual risks:
- Warm binary exact path is parser-cache efficient but still not consistently sub-500ms on this dirty multi-agent workspace. A future pass should replace full C# tree enumeration with a persisted manifest or OS file watcher snapshot if sub-500ms is a hard requirement.

## 2026-05-23 Polish Pass - Git Index Binary Stamp

What was wrong:
- The binary aggregate cache no longer reparsed unchanged C#, but the warm proof still paid Python `os.scandir` and per-file stat cost for all `2368` C# files before it could prove the aggregate cache.
- A narrow prompt extraction regex also failed when `CURRENT_BATCH.md` used `<AGENT_PROMPT id="SHINOBU_359" role="..." chat_name="...">`.

What was done:
- Added a production-only git index file stamp for `Assets/_Project/Scripts/**/*.cs`: `.git/index` size/mtime represents tracked and staged C# state, while dirty/untracked/deleted C# paths are represented by size/mtime or deletion tokens.
- Replaced the slower dirty path command with `git status --porcelain=v1 -z --untracked-files=all`.
- Changed binary-only CLI flow so `--binary-schema-audit` does not pre-enumerate every C# file before the cache check.
- Re-extracted the SHINOBU_359 prompt with an attribute-aware regex: `PROMPT_CHARS=20960`, `TASK_COUNT=20`.

Cinematic Cheats used:
- Unity import/compile probing remains replaced with static graph and source-stamp proof. The new stamp uses the VCS index as the cheap source-of-truth accelerator for clean files.

Exact Microseconds saved:
- Runtime frame: `0 us`; no Unity runtime path changed.
- Binary-only warm proof improved to `116.059 ms`, `cacheHit=True`, `cacheMisses=0`, `Performance_Regression_Warning=False`.
- Latest full deep proof: binary `156.111 ms`, using-boundary `444.154 ms`, OOP findings `18`, using findings `2193`.

Verification:
- `python -m py_compile Tools\AssemblyDependencyAudit.py Tools\test_assembly_dependency_audit.py`: exit `0`.
- `python Tools\test_assembly_dependency_audit.py`: exit `0`, `11` tests.
- `python Tools\AssemblyDependencyAudit.py --binary-schema-audit`: exit `0`, binary elapsed `116.059 ms` on isolated warm hit.
- `python Tools\AssemblyDependencyAudit.py --binary-schema-audit --oop-test-audit --using-leak-audit --write-self-audit`: exit `0`, binary elapsed `156.111 ms`.

Residual risks:
- Git-backed stamping is enabled only for the production script root. Non-production/temp roots keep the filesystem stamp fallback.

## 2026-05-23 Verification Closeout - Git Index File Stamp

What was wrong:
- The previous warm-cache proof still had stale ledger/log values from older filesystem-stamp runs.
- The binary audit did not need another C# parse; it needed consistent proof records and final artifact validation.

What was done:
- Synchronized `Docs/Tasks/Status_SHINOBU_359.md`, `Docs/AgentLogs/Rationale_SHINOBU_359.md`, `Docs/AgentLogs/LOG_SHINOBU_359.md`, and `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` to the current reports.
- Kept the implementation scoped to `Tools/AssemblyDependencyAudit.py`, tests, and offline docs/reports. No C# source, `.asmdef`, Unity asset, runtime DTO, Vault route, or SignalBus payload was changed.
- Revalidated report files and parser caches after the documentation correction.

Cinematic Cheats used:
- Unity import/build probing is still replaced by static source proof: O(N+E) asmdef graph validation, mmap source scans, and a VCS-backed source stamp. No scene load, GameObject construction, Physics calls, or dotnet rebuild was required.

Exact Microseconds saved:
- Runtime frame: `0 us`; no Unity runtime path changed.
- Isolated binary warm proof: `116.059 ms`, `cacheHit=True`, `cacheMisses=0`, `Performance_Regression_Warning=False`.
- Latest full deep proof: binary `156.111 ms`, using-boundary `444.154 ms`, OOP findings `18`, using findings `2193`.
- The git-index-file stamp avoids full Python C# path enumeration on exact binary warm hits; dirty proof remains explicit with `648` dirty, `2` deleted, and `172` untracked C# paths in the current workspace.

Verification:
- `python -m py_compile Tools\AssemblyDependencyAudit.py Tools\test_assembly_dependency_audit.py`: exit `0`.
- `python Tools\test_assembly_dependency_audit.py`: exit `0`, `11` tests.
- `python Tools\AssemblyDependencyAudit.py --test`: exit `0`, `SELF_TEST_PASS`.
- `python Tools\AssemblyDependencyAudit.py --fail-on-cycles`: exit `0`, current graph cycles `0`.
- JSON/XML parse check for SHINOBU_359 reports/caches/self-audit: exit `0`.
- Scoped `git diff --check`: exit `0` with CRLF normalization warnings only.

Residual risks:
- Strict debt gates still fail intentionally on existing repository debt: `148` Core.Contracts boundary violations, `1` unresolved first-party reference, `2193` source-level using-boundary violations, `60` ARM64 findings, `276` CS1612 property findings, `112` AUP precision findings, and `18` OOP test findings.
- Static source only. No Unity import, generated project compile, profiler, GCMonitor, player build, or runtime smoke proof was executed.
