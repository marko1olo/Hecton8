# LOG - AI_BEHAVIOR_BIOMIMETIC_DESIGNER

## 2026-05-15 - Leviathan Utility Brain

What was wrong:
- The Alpha Leviathan assignment had no extracted data table, no 50-row utility surface, no explicit sensory weighting, no pack flanking math artifact, and no 10,000 encounter evidence for the requested brain.
- The prompt header claimed 15 tasks, but the XML contained 7 numbered tasks. I executed the concrete 7 and did not invent the missing 8.

What was done:
- Created `Data/AI/Leviathan_Brain.json`.
- Defined 10 contexts x 5 behaviors = 50 utility score rows for Circle, Hide, Breach, FalseCharge, RealAttack.
- Defined sensory weights: sound 0.42, light 0.26, movement 0.32, plus line-of-sight and pack synergy modifiers.
- Defined pack hunting synergy using dot-product flanking rules over `EntityAUPs`, `EntityVelocities`, and `EntityFlags`.
- Created `Tools/AiBattleSim.py`.
- Created `Tools/test_ai_battle_sim.py`.
- Ran 10,000 deterministic dummy-player encounters and wrote `Tools/AiBattleSim_Report.json`.
- Implemented artifact validation for score count, non-finite numbers, DataVault feed references, frustration guard, subgroup kill-rate guard, and report status.
- Upgraded DataVault validation to parse the live `H8Memory.cs` BufferID enum instead of trusting a copied list.

Cinematic cheats used:
- Circle uses silhouette, occlusion, and hydrophone pass-by instead of expensive physical orbit truth.
- Hide uses fog/silt vanish and proxy target movement instead of scene stealth queries.
- Breach uses AUP-safe lunge plus pooled presentation cues; no water-column physics.
- FalseCharge uses near miss, screen pressure, and haptic intent; damage is capped and cooldown-gated.
- RealAttack is a single combat truth packet; no repeated bite spam.

Exact microseconds saved:
- Measured Unity runtime saving: 0 us, because no Unity runtime path was changed.
- Avoided runtime risk: no new `Update`, no C# hot path, no scene object query, no AudioSource/Light/Camera/Transform dependency.
- Projected importer cost if prepacked into NativeArray rows: 2-5 us for score table reads, 4-12 us for pack dot gates by tier.
- Projected savings versus angle/acos group tactics: low single-digit microseconds per decision on weak CPU, plus 0 B/frame GC by design.

Evidence:
- `python Tools\AiBattleSim.py --encounters 10000 --report Tools\AiBattleSim_Report.json`
- Result: status `INSTINCTS DEFINED`, kills 5224/10000, killRate 0.5224, under30KillRate 0.0, averageKillTimeSeconds 76.401, meanTerror 0.74994.
- `python Tools\AiBattleSim.py --encounters 10000 --report Tools\AiBattleSim_Report.json --check-artifacts`
- Result: `ARTIFACT_CHECK_PASSED`. Subgroup caps passed: maxProfileKillRate 0.61865, maxTierKillRate 0.53716, maxPackKillRate 0.64279. BufferID source parsed from `H8Memory.cs`, count 34.
- `python -m py_compile Tools\AiBattleSim.py Tools\test_ai_battle_sim.py`
- Result: passed.
- `python -m unittest Tools.test_ai_battle_sim`
- Result: 7 tests passed.

Regression model:
- CPU: no Unity runtime CPU added; offline simulator only.
- GC: no Unity hot path changed; runtime GC proof remains PENDING VERIFICATION until importer/playmode.
- Memory: new JSON/report/tool files only; no runtime NativeArray allocated.
- Cadence: authored cadence is data only; future runtime importer must use dispatcher/vault, not JSON parsing in gameplay.
- Correctness: simulator consumes the authored brain JSON and validates DataVault feed names against current `H8Memory.cs` BufferID list.

Failure modes:
- If future runtime importer parses JSON during gameplay, that would violate zero-GC policy.
- If light/audio are wired from scene components instead of pre-resolved vault lanes, task 7 would regress.
- If designers raise `RealAttack` rows or damage without rerunning the simulator, frustration guard can regress.

Status:
- INSTINCTS DEFINED for data and Python evidence.
- Unity runtime verification remains PENDING VERIFICATION.

## 2026-05-15 - Final Artifact Hardening Pass

What was wrong:
- The first artifact checker proved the happy path, but it trusted too much report-side state. A manually stale report could keep `INSTINCTS DEFINED` while hiding changed validation counts or impossible kill-rate summaries.
- The subgroup-guard unit test wrote temporary data under the project `Temp/` tree.

What was done:
- Hardened `Tools/AiBattleSim.py::check_artifacts`.
- It now rejects fallback BufferID validation, stale report validation counts, killRate outside `selfAudit` target bounds, under-30 kill-rate drift, and subgroup cap drift.
- Updated `Tools/test_ai_battle_sim.py` to use system temp directories and added target-rate and stale-validation regression tests.

Cinematic cheats used:
- No new simulation truth was added. The work stayed in offline proof tooling and preserved the existing visual-fake-first brain contract.

Exact microseconds saved:
- Unity runtime saving remains 0 us measured because no Unity runtime path changed.
- Integration risk reduction: stale report failure now happens in the CLI validator before any importer/runtime handoff.

Evidence:
- `python -B Tools\AiBattleSim.py --encounters 10000 --report Tools\AiBattleSim_Report.json --check-artifacts`
- Result: `ARTIFACT_CHECK_PASSED`, encounters 10000, killRate 0.5224, under30KillRate 0.0.
- `python -B -m unittest Tools.test_ai_battle_sim`
- Result: 9 tests passed in 3.618 s on final readback.
- `python -B -c "import ast, pathlib; ..."`
- Result: syntax parse passed for simulator and tests without writing bytecode.

Status:
- INSTINCTS DEFINED for data and Python evidence.
- Unity runtime verification remains PENDING VERIFICATION.
