## 2026-05-25 AUTOFIX3

What was wrong:
- Event/smoke/UI/visual/fallback diagnostics still used direct `UnityEngine.Debug.Log*` in another 34 source files.
- `H8Debug` could not preserve Unity object context for exception logs.

What was done:
- Added `Hecton8.Core.H8Debug.LogException(Exception, UnityEngine.Object)`.
- Converted selected diagnostics to `H8Debug` in 34 non-facade files.

Cinematic cheats used:
- No simulation or new runtime effect added. Diagnostics stay editor/development-only; release-player builds do not pay for these selected diagnostic paths.

Proof artifacts:
- Scoped identifier-bound `rg` over converted files returned no direct Unity `Debug.Log*` matches.
- `git diff --check` exit 0; only LF/CRLF normalization warnings.
- Build gate: CPU average 70.8%; AGENTS.md forbids launching build above 50%. No compiler process was listed in the gate output.

Exact microseconds saved:
- Measured: PENDING VERIFICATION because build/profiler run was blocked by CPU gate.
- Static estimate: 0 us steady-frame gameplay; release-player diagnostic path is stripped by conditional facade calls.
