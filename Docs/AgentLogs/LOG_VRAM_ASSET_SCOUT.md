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
