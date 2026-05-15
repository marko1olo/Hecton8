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

## 2026-05-15 - Report Breakdown Consistency Hardening

What was wrong:
- Normal artifact validation checked the top-line summary, but not whether `profileBreakdown`, `tierBreakdown`, and `packCountBreakdown` still summed back to that summary.

What was done:
- Added breakdown consistency validation for encounters, kills, escaped, timeouts, under-30 kills, kill rates, and under-30 rates.
- Added regression tests for tampered profile totals, tampered tier rates, and missing pack breakdown.

Cinematic cheats used:
- No new simulation truth. This is report integrity hardening.

Exact microseconds saved:
- Unity runtime saving remains 0 us measured because no Unity runtime path changed.
- Offline validation saves strict-rerun time for common report tampering cases.

Evidence:
- `python -B -c "import ast, pathlib; ..."`
- Result: syntax parse passed.
- `python -B -m unittest Tools.test_ai_battle_sim`
- Result: 32 tests passed in 3.001 s.
- `python -B Tools\AiBattleSim.py --encounters 10000 --report Tools\AiBattleSim_Report.json --check-artifacts`
- Result: `ARTIFACT_CHECK_PASSED`.
- `python -B Tools\AiBattleSim.py --encounters 10000 --report Tools\AiBattleSim_Report.json --check-artifacts --verify-rerun`
- Result: `ARTIFACT_CHECK_PASSED`, `rerunVerified=True`.

Status:
- INSTINCTS DEFINED for data and Python evidence.
- Unity runtime verification remains PENDING VERIFICATION.

## 2026-05-15 - Identity and Source Binding

What was wrong:
- The brain/report identity fields existed, but they were not enforced by the checker. A copied artifact from the wrong prompt or wrong domain could pass technical validation.

What was done:
- Added expected agent id, role, domain, evidence class, runtime proof, source prompt path, prompt id, numbered task count, and header task claim constants.
- Added report identity checks for `generatedBy`, `evidenceClass`, `runtimeUnityProof`, and `brainPath`.
- Added regression tests for wrong prompt id, wrong task count, wrong domain, and report identity drift.

Cinematic cheats used:
- No new simulation truth. This is batch-protocol evidence binding.

Exact microseconds saved:
- Unity runtime saving remains 0 us measured because no Unity runtime path changed.
- Integration risk reduced: cross-agent artifact reuse now fails in the CLI validator.

Evidence:
- `python -B -c "import ast, pathlib; ..."`
- Result: syntax parse passed.
- `python -B -m unittest Tools.test_ai_battle_sim`
- Result: 29 tests passed in 10.740 s.
- `python -B Tools\AiBattleSim.py --encounters 10000 --report Tools\AiBattleSim_Report.json`
- Result: `INSTINCTS DEFINED`, kills 5224, killRate 0.5224, under30KillRate 0.0.
- `python -B Tools\AiBattleSim.py --encounters 10000 --report Tools\AiBattleSim_Report.json --check-artifacts`
- Result: `ARTIFACT_CHECK_PASSED`.
- `python -B Tools\AiBattleSim.py --encounters 10000 --report Tools\AiBattleSim_Report.json --check-artifacts --verify-rerun`
- Result: `ARTIFACT_CHECK_PASSED`, `rerunVerified=True`.

Status:
- INSTINCTS DEFINED for data and Python evidence.
- Unity runtime verification remains PENDING VERIFICATION.

## 2026-05-15 - DataVault Lane Contract Hardening

What was wrong:
- The checker validated BufferID names, but it did not require the exact decision feature-lane set. A missing, extra, or duplicate feed row could change the importer contract while still passing if utility rows did not reference the bad lane.

What was done:
- Added `EXPECTED_FEATURES` to `Tools/AiBattleSim.py`.
- Enforced exact seven lanes: `distanceSq01`, `sound01`, `light01`, `movement01`, `lineOfSight01`, `packSynergy01`, `attackCooldown01`.
- Added tests for missing, extra, and duplicate feature lanes.

Cinematic cheats used:
- No simulation truth added. This preserves the GlobalDataVault proxy-data contract and avoids scene-object sensing.

Exact microseconds saved:
- Unity runtime saving remains 0 us measured because no Unity runtime path changed.
- Future importer savings are preserved by keeping a stable, prepackable seven-lane input set.

Evidence:
- `python -B -c "import ast, pathlib; ..."`
- Result: syntax parse passed.
- `python -B -m unittest Tools.test_ai_battle_sim`
- Result: 25 tests passed in 10.738 s.
- `python -B Tools\AiBattleSim.py --encounters 10000 --report Tools\AiBattleSim_Report.json`
- Result: `INSTINCTS DEFINED`, kills 5224, killRate 0.5224, under30KillRate 0.0.
- `python -B Tools\AiBattleSim.py --encounters 10000 --report Tools\AiBattleSim_Report.json --check-artifacts`
- Result: `ARTIFACT_CHECK_PASSED`.
- `python -B Tools\AiBattleSim.py --encounters 10000 --report Tools\AiBattleSim_Report.json --check-artifacts --verify-rerun`
- Result: `ARTIFACT_CHECK_PASSED`, `rerunVerified=True`.
- Contract result: `globalDataVaultFeedCount=7`.

Status:
- INSTINCTS DEFINED for data and Python evidence.
- Unity runtime verification remains PENDING VERIFICATION.

## 2026-05-15 - Fail-Closed Validator Hardening

What was wrong:
- Some malformed artifacts could stress checker error paths or evade comparison:
- Missing `behaviorOrder` could throw instead of cleanly failing.
- Invalid self-audit numeric fields could throw inside artifact checks.
- Report-side `behaviorCounts` drift was not compared against fresh validation.

What was done:
- Added guarded numeric reads for artifact self-audit fields.
- Changed `behaviorOrder` validation to reject non-list/missing values without throwing.
- Added report `behaviorCounts` drift comparison.
- Added temp brain helper for tests.
- Added regression tests for missing `behaviorOrder`, invalid `selfAudit.targetKillRateMin`, and report `behaviorCounts` drift.

Cinematic cheats used:
- No new simulation truth. This is evidence-path hardening only.

Exact microseconds saved:
- Unity runtime saving remains 0 us measured because no Unity runtime path changed.
- Avoided CI/importer failure mode: malformed artifacts return structured errors instead of crashing.

Evidence:
- `python -B -c "import ast, pathlib; ..."`
- Result: syntax parse passed.
- `python -B -m unittest Tools.test_ai_battle_sim`
- Result: 22 tests passed in 15.108 s.
- `python -B Tools\AiBattleSim.py --encounters 10000 --report Tools\AiBattleSim_Report.json`
- Result: `INSTINCTS DEFINED`, kills 5224, killRate 0.5224, under30KillRate 0.0.
- `python -B Tools\AiBattleSim.py --encounters 10000 --report Tools\AiBattleSim_Report.json --check-artifacts`
- Result: `ARTIFACT_CHECK_PASSED`.
- `python -B Tools\AiBattleSim.py --encounters 10000 --report Tools\AiBattleSim_Report.json --check-artifacts --verify-rerun`
- Result: `ARTIFACT_CHECK_PASSED`, `rerunVerified=True`.

Status:
- INSTINCTS DEFINED for data and Python evidence.
- Unity runtime verification remains PENDING VERIFICATION.

## 2026-05-15 - Full Brain Contract Hardening

What was wrong:
- Utility matrix validation was strict, but non-row contracts were still soft: behavior parameters, cadence, self-audit thresholds, Math LOD tiers, pack dot rules, and black-box telemetry could drift without failing the checker.

What was done:
- Added validation for all five behavior parameter blocks.
- Added numeric range checks for damage, terror, cooldown, and decision cadence.
- Enforced hysteresis bounds.
- Enforced self-audit paths and 10,000 required encounters.
- Enforced Low/Middle/High/Ultra Math LOD tiers.
- Enforced four pack hunting rules and required `dot` conditions.
- Enforced 300-frame black-box telemetry, dump path, and required entry fields.
- Added six regression tests for those failure modes.

Cinematic cheats used:
- Every behavior parameter still requires a `cinematicCheat` string. This keeps visual fake intent attached to the data contract instead of drifting into runtime truth simulation.

Exact microseconds saved:
- Unity runtime saving remains 0 us measured because no Unity runtime path changed.
- Avoided runtime risk: incomplete LOD/black-box/pack contracts now fail in Python before importer/runtime work.

Evidence:
- `python -B Tools\AiBattleSim.py --encounters 10000 --report Tools\AiBattleSim_Report.json`
- Result: `INSTINCTS DEFINED`, kills 5224, killRate 0.5224, under30KillRate 0.0.
- `python -B Tools\AiBattleSim.py --encounters 10000 --report Tools\AiBattleSim_Report.json --check-artifacts`
- Result: `ARTIFACT_CHECK_PASSED`.
- `python -B Tools\AiBattleSim.py --encounters 10000 --report Tools\AiBattleSim_Report.json --check-artifacts --verify-rerun`
- Result: `ARTIFACT_CHECK_PASSED`, `rerunVerified=True`.
- `python -B -m unittest Tools.test_ai_battle_sim`
- Result: 19 tests passed in 18.922 s.
- Contract result: `behaviorParameterCount=5`, `mathLodTierCount=4`, `packRuleCount=4`, `blackBoxCapacityFrames=300`.

Status:
- INSTINCTS DEFINED for data and Python evidence.
- Unity runtime verification remains PENDING VERIFICATION.

## 2026-05-15 - Utility Matrix Completeness Hardening

What was wrong:
- The validator counted 50 utility rows and 10 rows per behavior, but it did not prove that every context/behavior pair existed exactly once.
- A duplicate pair could have hidden a missing transition and still passed the old row-count checks.

What was done:
- Added `EXPECTED_CONTEXT_COUNT=10`.
- Added duplicate context rejection.
- Added exact 10 x 5 context/behavior pair validation.
- Added `contextCount` and `utilityPairCount` to validation output.
- Added tests for duplicate context ids and duplicate context/behavior pairs.
- Regenerated `Tools/AiBattleSim_Report.json` with the updated validation schema.

Cinematic cheats used:
- No new truth simulation. This is offline guardrail work preventing broken utility data from reaching runtime.

Exact microseconds saved:
- Unity runtime saving remains 0 us measured because no Unity runtime path changed.
- Avoided importer/runtime risk: broken utility pairs fail before any NativeArray packing or gameplay lookup.

Evidence:
- `python -B Tools\AiBattleSim.py --encounters 10000 --report Tools\AiBattleSim_Report.json`
- Result: `INSTINCTS DEFINED`, kills 5224, killRate 0.5224, under30KillRate 0.0.
- `python -B Tools\AiBattleSim.py --encounters 10000 --report Tools\AiBattleSim_Report.json --check-artifacts`
- Result: `ARTIFACT_CHECK_PASSED`.
- `python -B Tools\AiBattleSim.py --encounters 10000 --report Tools\AiBattleSim_Report.json --check-artifacts --verify-rerun`
- Result: `ARTIFACT_CHECK_PASSED`, `rerunVerified=True`.
- `python -B -m unittest Tools.test_ai_battle_sim`
- Result: 13 tests passed in 9.356 s.
- Matrix result: `contextCount=10`, `utilityPairCount=50`.

Status:
- INSTINCTS DEFINED for data and Python evidence.
- Unity runtime verification remains PENDING VERIFICATION.

## 2026-05-15 - Deterministic Evidence Binding

What was wrong:
- A sane report was still not enough. Without a hard fingerprint, a stale report from another brain revision could pass field-level validation if its numbers stayed within bounds.

What was done:
- Added `brainDigest` and `simulationDigest` SHA-256 fields to `Tools/AiBattleSim.py` reports.
- Added `simulatorSchemaVersion=2`.
- Hardened artifact validation to reject digest drift.
- Added strict `--verify-rerun` mode that reruns the requested encounter count and compares simulation digest, summary, and calibration.
- Added regression tests for brain digest tampering and rerun summary drift.

Cinematic cheats used:
- No new physical simulation truth. The work remained offline proof infrastructure for the same visual-fake-first utility brain.

Exact microseconds saved:
- Unity runtime saving remains 0 us measured because no Unity runtime path changed.
- Release-gate value: stale/tampered AI evidence fails before runtime import, avoiding wasted Unity profiling cycles.

Evidence:
- Regenerated `Tools/AiBattleSim_Report.json` with `brainDigest=07dad20de885023d068e93c97ae468732cb077efd6fc0b01420279900389e246`.
- Full result-stream digest: `simulationDigest=97a10330bb86ded1a29b82aa896ac9acbe46d768fe0c403360c80e08bca50867`.
- Current live `H8Memory.cs` BufferID count: 66.
- `python -B Tools\AiBattleSim.py --encounters 10000 --report Tools\AiBattleSim_Report.json --check-artifacts`
- Result: `ARTIFACT_CHECK_PASSED`, killRate 0.5224, under30KillRate 0.0.
- `python -B Tools\AiBattleSim.py --encounters 10000 --report Tools\AiBattleSim_Report.json --check-artifacts --verify-rerun`
- Result: `ARTIFACT_CHECK_PASSED`, `rerunVerified=True`.
- `python -B -m unittest Tools.test_ai_battle_sim`
- Result: 11 tests passed in 13.026 s.

Regression model:
- CPU: no Unity runtime CPU added. Strict rerun is offline and intentionally expensive.
- GC: no Unity hot path changed. Runtime GC proof remains PENDING VERIFICATION until importer/playmode.
- Memory: report grew by digest fields only; no runtime NativeArray allocated.
- Cadence: no gameplay cadence changed.
- Correctness: report is now bound to exact brain JSON and deterministic result stream.

Failure modes:
- If future edits change `Leviathan_Brain.json`, `brainDigest` mismatch will fail artifact validation until the report is regenerated.
- If simulator semantics change, schema/digest mismatch forces rerun evidence.
- If future importer parses JSON at runtime, that remains a separate zero-GC violation outside this data/tooling task.

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

## 2026-05-15 - Breakdown Key Hardening Pass

What was wrong:
- Breakdown totals and per-row rates were validated, but the checker did not require the exact subgroup key universe. Missing profiles, invented tiers, or wrong pack labels could still pass if totals were manually balanced.
- A previous prompt extraction attempt timed out and printed a neighboring agent fragment; that was not accepted as parsing evidence.

What was done:
- Hardened `Tools/AiBattleSim.py` to require exact profile keys, tier keys, and pack-count keys in the report breakdowns.
- Added regression tests rejecting missing profile keys, extra tier keys, and wrong pack-count keys.
- Re-extracted the exact `AI_BEHAVIOR_BIOMIMETIC_DESIGNER` prompt block with a bounded CLI regex and confirmed the XML contains 7 concrete tasks.

Cinematic cheats used:
- No new physical simulation truth was added. This pass hardened offline evidence around the existing deterministic utility brain.

Exact microseconds saved:
- Unity runtime saving remains 0 us measured because no Unity runtime path changed.
- Integration value: subgroup evidence drift now fails in the CLI checker before import or playmode testing.

Evidence:
- `python -B -c "import ast, pathlib; ..."` -> `SYNTAX_OK`.
- `python -B -m unittest Tools.test_ai_battle_sim` -> 35 tests passed in 12.411 s.
- `python -B Tools\AiBattleSim.py --encounters 10000 --report Tools\AiBattleSim_Report.json --check-artifacts --verify-rerun` -> `ARTIFACT_CHECK_PASSED`, `rerunVerified=True`.
- killRate 0.5224, under30KillRate 0.0.
- `brainDigest=07dad20de885023d068e93c97ae468732cb077efd6fc0b01420279900389e246`.
- `simulationDigest=97a10330bb86ded1a29b82aa896ac9acbe46d768fe0c403360c80e08bca50867`.

Regression model:
- CPU: no Unity runtime CPU added. Strict rerun is offline and intentionally expensive.
- GC: no Unity hot path changed. Runtime GC proof remains PENDING VERIFICATION until importer/playmode.
- Memory: no runtime memory allocation added.
- Cadence: no gameplay cadence changed.
- Correctness: subgroup breakdown evidence now proves both numeric consistency and exact expected subgroup labels.

Failure modes:
- Adding/removing player profiles, tiers, or pack-count classes requires deliberate simulator/report/test updates.
- Unity runtime importer remains separate work; no runtime behavior is claimed beyond data/tooling evidence.

Status:
- INSTINCTS DEFINED for data and Python evidence.
- Unity runtime verification remains PENDING VERIFICATION.

## 2026-05-15 - Report Sample Evidence Hardening

What was wrong:
- The report exposed 20 representative encounter samples, but normal artifact validation did not prove those samples were structurally valid.
- A malformed profile label, impossible outcome combination, or broken terror/HP range could mislead integration review even if summary numbers passed.

What was done:
- Hardened `Tools/AiBattleSim.py` to validate sample count, sample index order, profile/tier/pack membership, outcome exclusivity, kill-time bounds, HP bounds, terror bounds, and `meanTerror <= maxTerror`.
- Added regression tests for missing sample rows, invalid sample profile labels, and contradictory sample outcomes.

Cinematic cheats used:
- No new simulation truth. This pass only hardens the offline evidence report around the existing utility-brain simulation.

Exact microseconds saved:
- Unity runtime saving remains 0 us measured because no Unity runtime path changed.
- Integration value: malformed human-facing report samples now fail before the report is trusted or imported.

Evidence:
- `python -B -c "import ast, pathlib; ..."` -> `SYNTAX_OK`.
- `python -B -m unittest Tools.test_ai_battle_sim` -> 38 tests passed in 10.347 s.
- `python -B Tools\AiBattleSim.py --encounters 10000 --report Tools\AiBattleSim_Report.json --check-artifacts --verify-rerun` -> `ARTIFACT_CHECK_PASSED`, `rerunVerified=True`.
- killRate 0.5224, under30KillRate 0.0.
- `brainDigest=07dad20de885023d068e93c97ae468732cb077efd6fc0b01420279900389e246`.
- `simulationDigest=97a10330bb86ded1a29b82aa896ac9acbe46d768fe0c403360c80e08bca50867`.

Regression model:
- CPU: no Unity runtime CPU added. Python sample validation is offline report checking.
- GC: no Unity hot path changed. Runtime GC proof remains PENDING VERIFICATION until importer/playmode.
- Memory: no runtime memory allocation added.
- Cadence: no gameplay cadence changed.
- Correctness: report samples are now constrained to the same profile, tier, pack, and outcome contracts as the simulator summary.

Failure modes:
- Future sample shape changes require corresponding checker and test updates.
- Unity runtime importer remains separate work; no runtime behavior is claimed beyond data/tooling evidence.

Status:
- INSTINCTS DEFINED for data and Python evidence.
- Unity runtime verification remains PENDING VERIFICATION.
