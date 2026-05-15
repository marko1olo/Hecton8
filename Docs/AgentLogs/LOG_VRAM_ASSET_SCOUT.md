# LOG_VRAM_ASSET_SCOUT

## 2026-05-15T22:27:00+03:00 - SPLIT REDLINE FLAG PAYLOAD VALIDATOR PASS

What was wrong:
- Split redline CSV validation proved paths belonged to the broad CSV, but did not prove the `flags` payload matched.
- Same-path stale risk labels could still mislead asset owners.

What was done:
- Added `texture_flags_by_path`, `mesh_flags_by_path`, and `render_texture_flags_by_path` maps in `validate_generated_reports()`.
- Added mismatch checks for texture, mesh, and RenderTexture split redline `flags` against broad CSV `redline_flags`.
- Recreated active VRAM status/rationale/log files after the active files were moved to archive during this continuation.

Cinematic cheats used:
- None. This is offline tooling/report validation, not runtime rendering work.

Exact microseconds saved:
- Runtime code changed: none.
- Immediate runtime CPU saving: 0us.
- Tooling correctness improvement: stale redline risk labels now fail the no-scan report validator.

Verification:
- PYTHONDONTWRITEBYTECODE=1 python AST syntax parse for `Tools/MemoryBudgetCheck.py` and `Tools/test_memory_budget_check.py`: PASS.
- PYTHONDONTWRITEBYTECODE=1 python Tools/MemoryBudgetCheck.py --root . --validate-reports: PASS; `reports valid: textures=1652 meshes=302 render_textures=1 texture_redlines=946 mesh_redlines=293 rt_redlines=1 rt_hotspots=61 scan_roots=Assets,Packages,Data`.
- PYTHONDONTWRITEBYTECODE=1 python -m unittest discover -s Tools -p test_memory_budget_check.py: PASS, 17 tests, elapsed 6.553 seconds.
- PYTHONDONTWRITEBYTECODE=1 python Tools/MemoryBudgetCheck.py --root . --ci: EXPECTED FAIL with `ci_exit_code=2`; current redlines/overflow still produce `[CRITICAL_VRAM_OVERFLOW]`.
- Python bytecode cleanup for `MemoryBudgetCheck*` and `test_memory_budget_check*`: PASS.

Evidence boundary:
- STATIC_SOURCE / FILESYSTEM / PY_UNIT_TEST only.
- Unity import, Memory Profiler, player build, and runtime frame time remain PENDING VERIFICATION.

## 2026-05-15T23:00:00+03:00 - TEXTURE JSON REDLINE DETAIL PARITY PASS

What was wrong:
- The active status said regenerated JSON texture redline payloads were validated, but the actual no-scan validator still allowed stale JSON texture detail payloads if broad CSV and split CSV counts/flags matched.
- That left a handoff risk: downstream tooling could consume stale JSON dimensions, first-party markers, or BC7 estimates while the CSV reports were correct.

What was done:
- Added `texture_redlines` payload entries to `VRAM_Budget_Audit.json` with path, dimensions, full-mip BC7 MiB, first-party marker, flags, and recommendation.
- Added `--validate-reports` checks for texture JSON path set, flags, dimensions, first-party marker, and estimate parity against `VRAM_Texture_Redlines.csv`.
- Added a synthetic regression test that mutates JSON texture flags and BC7 estimates and proves validation fails.
- Regenerated the active VRAM report artifacts.

Cinematic cheats used:
- None. This is offline report validation, not runtime rendering work.

Exact microseconds saved:
- Runtime code changed: none.
- Immediate runtime CPU saving: 0us.
- Tooling correctness improvement: texture remediation payloads now fail fast if JSON drifts from the split remediation queue.

Verification:
- PYTHONDONTWRITEBYTECODE=1 Python AST syntax parse for `Tools/MemoryBudgetCheck.py` and `Tools/test_memory_budget_check.py`: PASS.
- Focused unittest for summary payload and texture JSON drift: PASS, 2/2.
- PYTHONDONTWRITEBYTECODE=1 python Tools/MemoryBudgetCheck.py --root .: PASS; regenerated reports.
- PYTHONDONTWRITEBYTECODE=1 python Tools/MemoryBudgetCheck.py --root . --validate-reports: PASS; `reports valid: textures=1652 meshes=302 render_textures=1 texture_redlines=946 mesh_redlines=293 rt_redlines=1 rt_hotspots=61 scan_roots=Assets,Packages,Data`.
- PYTHONDONTWRITEBYTECODE=1 python -m unittest discover -s Tools -p test_memory_budget_check.py -v: PASS, 21 tests, elapsed 5.657 seconds.
- PYTHONDONTWRITEBYTECODE=1 python Tools/MemoryBudgetCheck.py --root . --ci: EXPECTED FAIL with `ci_exit_code=2`; current static redlines/overflow still produce `[CRITICAL_VRAM_OVERFLOW]`.
- Python bytecode cleanup: PASS, `PYTHON_CACHE_COUNT 0`.
- git diff --check on VRAM-owned touched files: PASS, no whitespace errors; CRLF warnings only.
- Active batch XML extraction: BLOCKED because `Docs/Tasks/CURRENT_BATCH.md` is missing; archived batch files were not used as active authority.

Evidence boundary:
- STATIC_SOURCE / FILESYSTEM / PY_UNIT_TEST only.
- Unity import, Memory Profiler, player build, and runtime frame time remain PENDING VERIFICATION.

## 2026-05-15T22:33:00+03:00 - BROAD REDLINE SET PARITY PASS

What was wrong:
- Split CSVs and JSON were tied together, but the broad CSV's own non-empty `redline_flags` sets were not explicitly compared to both.
- A broad inventory drift could survive if derived artifacts stayed internally consistent.

What was done:
- Added broad redline path sets for texture, mesh, and RenderTexture rows.
- Added JSON counter checks against broad redline sets.
- Required split CSV path sets to equal broad redline path sets.

Cinematic cheats used:
- None. This is offline report validation, not runtime rendering work.

Exact microseconds saved:
- Runtime code changed: none.
- Immediate runtime CPU saving: 0us.
- Tooling correctness improvement: broad inventory, JSON summary, and split remediation queues now agree on redline sets.

Verification:
- PYTHONDONTWRITEBYTECODE=1 python AST syntax parse for `Tools/MemoryBudgetCheck.py` and `Tools/test_memory_budget_check.py`: PASS.
- PYTHONDONTWRITEBYTECODE=1 python Tools/MemoryBudgetCheck.py --root . --validate-reports: PASS; `reports valid: textures=1652 meshes=302 render_textures=1 texture_redlines=946 mesh_redlines=293 rt_redlines=1 rt_hotspots=61 scan_roots=Assets,Packages,Data`.
- PYTHONDONTWRITEBYTECODE=1 python -m unittest discover -s Tools -p test_memory_budget_check.py: PASS, 17 tests, elapsed 9.753 seconds.
- PYTHONDONTWRITEBYTECODE=1 python Tools/MemoryBudgetCheck.py --root . --ci: EXPECTED FAIL with `ci_exit_code=2`; current redlines/overflow still produce `[CRITICAL_VRAM_OVERFLOW]`.
- Python bytecode cleanup for `MemoryBudgetCheck*` and `test_memory_budget_check*`: PASS.

Evidence boundary:
- STATIC_SOURCE / FILESYSTEM / PY_UNIT_TEST only.
- Unity import, Memory Profiler, player build, and runtime frame time remain PENDING VERIFICATION.

## 2026-05-15T22:41:00+03:00 - MARKDOWN REPORT DRIFT GUARD PASS

What was wrong:
- CSV and JSON were protected by `--validate-reports`, but `VRAM_Budget_Audit_Summary.md` and `VRAM_Remediation_Plan.md` could still be stale.
- Those Markdown files are the human-facing handoff; stale counts there mislead CTO/art-owner cleanup decisions.

What was done:
- Extended `validate_generated_reports()` to read the summary Markdown and remediation plan.
- Added required snippet checks for evidence boundary text, scan roots, key texture/mesh/RT counts, gate text, and remediation priority headings.
- Wired `--validate-reports` to pass `--summary` and `--plan` paths into the validator.

Cinematic cheats used:
- None. This is offline report validation, not runtime rendering work.

Exact microseconds saved:
- Runtime code changed: none.
- Immediate runtime CPU saving: 0us.
- Tooling correctness improvement: human-facing reports now fail validation if key values drift from machine artifacts.

Verification:
- PYTHONDONTWRITEBYTECODE=1 python AST syntax parse for `Tools/MemoryBudgetCheck.py` and `Tools/test_memory_budget_check.py`: PASS.
- PYTHONDONTWRITEBYTECODE=1 python Tools/MemoryBudgetCheck.py --root . --validate-reports: PASS; `reports valid: textures=1652 meshes=302 render_textures=1 texture_redlines=946 mesh_redlines=293 rt_redlines=1 rt_hotspots=61 scan_roots=Assets,Packages,Data`.
- PYTHONDONTWRITEBYTECODE=1 python -m unittest discover -s Tools -p test_memory_budget_check.py: PASS, 17 tests, elapsed 3.686 seconds.
- PYTHONDONTWRITEBYTECODE=1 python Tools/MemoryBudgetCheck.py --root . --ci: EXPECTED FAIL with `ci_exit_code=2`; current redlines/overflow still produce `[CRITICAL_VRAM_OVERFLOW]`.
- Python bytecode cleanup for `MemoryBudgetCheck*` and `test_memory_budget_check*`: PASS.
- LOG_ORDER_OK: 3 chronological active-continuation report headers, latest `2026-05-15T22:41:00+03:00`.

Evidence boundary:
- STATIC_SOURCE / FILESYSTEM / PY_UNIT_TEST only.
- Unity import, Memory Profiler, player build, and runtime frame time remain PENDING VERIFICATION.

## 2026-05-15T22:50:00+03:00 - JSON PAYLOAD PARITY PASS

What was wrong:
- `--validate-reports` protected broad CSV rows and split redline CSVs, but JSON `mesh_redlines` and `render_textures` payloads could still be stale with matching counts.
- Downstream agents often consume JSON first; stale JSON flags or RenderTexture estimates would corrupt remediation priority.

What was done:
- Added JSON mesh redline path/flag parity against `VRAM_Mesh_Redlines.csv`.
- Added JSON RenderTexture path/flag/dimension/estimate parity against `VRAM_Budget_Audit.csv`.
- Added a synthetic regression test that mutates JSON mesh flags and RenderTexture estimates, then proves validation fails.

Cinematic cheats used:
- None. This is offline report validation, not runtime rendering work.

Exact microseconds saved:
- Runtime code changed: none.
- Immediate runtime CPU saving: 0us.
- Tooling correctness improvement: stale JSON remediation payloads now fail the no-scan validator.

Verification:
- PYTHONDONTWRITEBYTECODE=1 Python AST syntax parse for `Tools/MemoryBudgetCheck.py` and `Tools/test_memory_budget_check.py`: PASS.
- PYTHONDONTWRITEBYTECODE=1 python Tools/MemoryBudgetCheck.py --root . --validate-reports: PASS; `reports valid: textures=1652 meshes=302 render_textures=1 texture_redlines=946 mesh_redlines=293 rt_redlines=1 rt_hotspots=61 scan_roots=Assets,Packages,Data`.
- PYTHONDONTWRITEBYTECODE=1 python -m unittest discover -s Tools -p test_memory_budget_check.py: PASS, 18 tests, elapsed 9.490 seconds.
- PYTHONDONTWRITEBYTECODE=1 python Tools/MemoryBudgetCheck.py --root . --ci: EXPECTED FAIL with `ci_exit_code=2`; current redlines/overflow still produce `[CRITICAL_VRAM_OVERFLOW]`.

Evidence boundary:
- STATIC_SOURCE / FILESYSTEM / PY_UNIT_TEST only.
- Unity import, Memory Profiler, player build, and runtime frame time remain PENDING VERIFICATION.

## 2026-05-15T22:52:40+03:00 - CSV SCHEMA AND EVIDENCE-CLASS GUARD PASS

What was wrong:
- `--validate-reports` accepted loose CSV schemas as long as a few consumed columns existed.
- A broad or split report could lose evidence-boundary columns, or mutate `evidence_class`, while count and payload parity still passed.
- The active JSON report was stale for texture redline payloads after the stricter validator path was present.

What was done:
- Added exact header contracts for `VRAM_Budget_Audit.csv`, texture redlines, mesh redlines, RenderTexture redlines, and RenderTexture hotspot CSV.
- Added broad CSV `evidence_class == STATIC_SOURCE` validation.
- Added RenderTexture hotspot `evidence_class == STATIC_SOURCE` validation.
- Added regression tests for broad CSV schema drift and evidence-class drift.
- Regenerated the VRAM report artifacts so `VRAM_Budget_Audit.json` includes current `texture_redlines` parity payload.

Cinematic cheats used:
- None. This is offline report validation, not runtime rendering work.

Exact microseconds saved:
- Runtime code changed: none.
- Immediate runtime CPU saving: 0us.
- Tooling correctness improvement: report consumers now fail fast on schema drift, stale texture JSON payloads, or false runtime evidence labels.

Verification:
- PYTHONDONTWRITEBYTECODE=1 Python AST syntax parse for `Tools/MemoryBudgetCheck.py` and `Tools/test_memory_budget_check.py`: PASS.
- PYTHONDONTWRITEBYTECODE=1 python Tools/MemoryBudgetCheck.py --root . --validate-reports: PASS; `reports valid: textures=1652 meshes=302 render_textures=1 texture_redlines=946 mesh_redlines=293 rt_redlines=1 rt_hotspots=61 scan_roots=Assets,Packages,Data`.
- PYTHONDONTWRITEBYTECODE=1 python -m unittest discover -s Tools -p test_memory_budget_check.py: PASS, 21 tests, elapsed 5.215 seconds.
- PYTHONDONTWRITEBYTECODE=1 python Tools/MemoryBudgetCheck.py --root . --ci: EXPECTED FAIL with `ci_exit_code=2`; current redlines/overflow still produce `[CRITICAL_VRAM_OVERFLOW]`.
- Python bytecode cleanup check: PASS, no `__pycache__` result under the touched tooling path.

Evidence boundary:
- STATIC_SOURCE / FILESYSTEM / PY_UNIT_TEST only.
- Unity import, Memory Profiler, player build, and runtime frame time remain PENDING VERIFICATION.
