# Status - AI_BEHAVIOR_BIOMIMETIC_DESIGNER

Prompt ID: AI_BEHAVIOR_BIOMIMETIC_DESIGNER
Role: AI_PROGRAMMER
Domain: ECHELON 3 - FLORA, FAUNA & BIOTA / Predator Cognition Utility AI
Task count parsed: 7 numbered tasks. Batch header says "15 TITANIUM TASKS"; no extra tasks were invented.
Current status: INSTINCTS DEFINED - Python/data verified; Unity runtime verification remains PENDING VERIFICATION

## Mandates Loaded
- AI_Creature_Cognition_States.txt
- AI_Director_Encounter_Manager.txt
- AI_Flocking_Boids_Swarm_SpatialHash_Logic.txt
- MATH_Deterministic_RNG_SlotMachine.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt

## Pre-Code Analysis
Target: `Data/AI/Leviathan_Brain.json`, `Tools/AiBattleSim.py`, simulator evidence outputs under `Tools/`.
Affected systems: offline AI balance data, fauna cognition design table, Python evidence tooling. No Unity scene, prefab, asset YAML, project setting, or public C# API changes planned.
Zero GC proof: runtime hot path is not changed. JSON defines data for prepacked/vault-owned runtime ingestion. Simulator is offline Python and not a gameplay hot path.
State check: no pool, dictionary, or SlowTick mutation in Unity code. Existing runtime owner discovered: `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs`; existing DataVault owner discovered: `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs`.
Rule quote: "All AI decisions are based on data available in the GlobalDataVault" is satisfied through an explicit `globalDataVaultFeeds` contract in the JSON, not direct concrete class references.

## Checklist
- [x] Task 1 - DECISION TABLES: create `Data/AI/Leviathan_Brain.json` | Justification: DOD data-table authoring, schemaVersion/status/authority fields included | Alternatives rejected: C# runtime mutation in shared `PredatorCognitionDomain.cs` | Estimate: 0 us hot path, offline import only
- [x] Task 2 - STATE TRANSITIONS: define 50 utility scores for Circle, Hide, Breach, False Charge, Real Attack | Justification: 10 contexts x 5 behavior rows gives explicit inspectable utility surface | Alternatives rejected: hidden hardcoded Python-only behavior table | Estimate: 50 scalar row reads, approx 2-5 us if prepacked in NativeArray
- [x] Task 3 - SENSORY WEIGHTS: define Sound vs Light vs Movement aggression effects | Justification: scalar weighted sum, sound 0.42 / light 0.26 / movement 0.32 | Alternatives rejected: ray/AudioSource/Light component queries | Estimate: <1 us scalar math after vault feed read
- [x] Task 4 - PACK HUNTING MATH: define synergy rules using dot-product flanking | Justification: dot-product flank gates match mandate and avoid angle/acos | Alternatives rejected: A* group tactics, per-agent physics truth | Estimate: 4-12 us for up to four stalker candidates depending tier
- [x] Task 5 - AI SIMULATOR: write `Tools/AiBattleSim.py` and run 10,000 encounters | Justification: deterministic CLI simulator consumed the authored JSON and produced `Tools/AiBattleSim_Report.json` | Alternatives rejected: manual outcome claims without evidence | Estimate: offline only; 10k report elapsed 195.149 s inside Python with subgroup validation
- [x] Task 6 - SELF-AUDIT LOOP 1: lower aggression if all kills are under 30 seconds | Justification: report shows kills 5224/10000, under30Kills 0, allKillsUnder30 false, subgroupGuardPassed true; no aggression reduction required | Alternatives rejected: fake adjustment when guard was not tripped | Estimate: 0 us runtime; offline audit scalar
- [x] Task 7 - SELF-AUDIT LOOP 2: verify decisions only use GlobalDataVault-available data | Justification: artifact validator now parses `Assets/_Project/Scripts/Core/Memory/H8Memory.cs` BufferID enum and checks every utility input against `globalDataVaultFeeds` | Alternatives rejected: copied/stale BufferID list, non-vault light/audio scene objects | Estimate: 0 us hot path until importer exists

## Iteration Loop Ledger
- [x] Loop 1 - Prompt extraction, domain check, mandate ingestion, file discovery
- [x] Loop 2 - Data table authoring and schema sanity
- [x] Loop 3 - Simulator implementation and 10k battle run
- [x] Loop 4 - Self-audit: frustration threshold, DataVault feed audit, artifact validation
- [x] Loop 5 - Regression/test pass, own-code readback, final log append
- [x] Loop 6 - Final hardening: artifact checker now rejects stale report validation, fallback BufferID source, target kill-rate drift, and subgroup cap drift
- [x] Loop 7 - Deterministic proof binding: report now includes current brain SHA-256 and full simulation stream digest; strict 10k rerun verification passed
- [x] Loop 8 - Matrix completeness hardening: validator now rejects duplicate contexts and missing/duplicated context-behavior utility pairs
- [x] Loop 9 - Full contract hardening: validator now rejects malformed behavior parameters, cadence hysteresis, self-audit settings, Math LOD tiers, pack dot rules, and black-box telemetry
- [x] Loop 10 - Fail-closed hardening: malformed `behaviorOrder`, invalid `selfAudit`, and report behavior-count drift now reject cleanly without validator crashes
- [x] Loop 11 - DataVault lane hardening: validator now enforces the exact seven required decision feature lanes and rejects missing/extra/duplicate feed rows
- [x] Loop 12 - Identity/source binding: validator now rejects wrong prompt id, task count, domain, report agent id, evidence class, runtime-proof status, and brain path
- [x] Loop 13 - Breakdown consistency hardening: artifact checker now validates profile/tier/pack subgroup totals and per-row rates against summary
- [x] Loop 14 - Breakdown key hardening: artifact checker now requires exact profile/tier/pack subgroup keys and rejects missing, extra, or wrong subgroup labels
- [x] Loop 15 - Sample evidence hardening: artifact checker now validates the 20 representative encounter samples for index, profile, tier, pack count, outcome exclusivity, kill-time range, HP range, and terror range
- [x] Loop 16 - Summary/counter integrity hardening: artifact checker now validates summary outcome arithmetic, rates, behavior/context counter keys, behavior/context total parity, and subgroup counter totals
- [x] Loop 17 - Frustration/calibration audit hardening: artifact checker now validates `frustrationAudit` fields against summary/selfAudit/subgroup maxima and validates calibration shape, passes, lowered flag, and aggression scalar consistency
- [x] Loop 18 - Sensory/cooldown contract hardening: aligned RealAttack cooldown to the declared 18 s minimum and added validator coverage for sensory formula tokens, sensory multipliers, and RealAttack/FalseCharge cooldown minimums
- [x] Loop 19 - Context/utility metadata hardening: artifact checker now validates exact context order/id set, context band fields, context descriptions, sequential utility score ids, and utility reason text
- [x] Loop 20 - Pack `math.dot` contract hardening: authored pack flanking formula/rules now use explicit `math.dot`, and validator rejects generic dot, angle/acos math, rule id/order drift, missing range gate, and missing rule descriptions/effects

## Evidence
- `python -B Tools\AiBattleSim.py --encounters 10000 --report Tools\AiBattleSim_Report.json` -> regenerated schema v2 report with `brainDigest` and `simulationDigest`; status `INSTINCTS DEFINED`, kills 4220, killRate 0.422, under30KillRate 0.0.
- `python -B Tools\AiBattleSim.py --encounters 10000 --report Tools\AiBattleSim_Report.json --check-artifacts` -> `ARTIFACT_CHECK_PASSED`; hardened checker confirms live BufferID source, matching report validation, killRate 0.422 inside target, under30KillRate 0.0, subgroup caps intact.
- `python -B Tools\AiBattleSim.py --encounters 10000 --report Tools\AiBattleSim_Report.json --check-artifacts --verify-rerun` -> `ARTIFACT_CHECK_PASSED`, `rerunVerified=True`; digest rerun matched `8d2131741fc2d1fc03900eec6a8aba4631c753e143a6b2e068db21bf1db92d7b`.
- `python -B -c "import ast, pathlib; ..."` -> syntax parse passed for `Tools/AiBattleSim.py` and `Tools/test_ai_battle_sim.py` without writing bytecode.
- `python -B -m unittest Tools.test_ai_battle_sim` -> 55 tests passed in 7.321 s after pack `math.dot` contract hardening.
- Prompt extraction -> bounded CLI regex extracted the complete `<AGENT_PROMPT id="AI_BEHAVIOR_BIOMIMETIC_DESIGNER">` block with 7 numbered tasks and status `INSTINCTS DEFINED`.
- `.sln/.csproj` scan -> none found in workspace, so `dotnet build` was not applicable for this data/Python task.
- Polish mandate extraction -> `<POLISH_MANDATE>` tag not found in `Docs/Tasks/CURRENT_BATCH.md`.
- Anti-bloat scan -> only expected hits: `AudioSource` appears in a JSON note rejecting AudioSource queries; `math.sqrt` appears only inside the Python `rsqrt()` helper for offline dot normalization.
- GlobalDataVault source -> `Assets/_Project/Scripts/Core/Memory/H8Memory.cs` parsed live; current BufferID count in report is 66.
- Digest evidence -> `brainDigest=8f96fe1c84c9b136aac2a4a0fcc3550e62ddaae28d0b6d66c782e064eae80edf`; `simulationDigest=8d2131741fc2d1fc03900eec6a8aba4631c753e143a6b2e068db21bf1db92d7b`.
- Matrix evidence -> report validation records `contextCount=10` and `utilityPairCount=50`.
- Contract evidence -> report validation records `behaviorParameterCount=5`, `mathLodTierCount=4`, `packRuleCount=4`, `blackBoxCapacityFrames=300`.
- Fail-closed evidence -> tests cover missing `behaviorOrder`, invalid `selfAudit.targetKillRateMin`, and report `behaviorCounts` drift.
- DataVault lane evidence -> report validation records `globalDataVaultFeedCount=7`; tests cover missing, extra, and duplicate feature lanes.
- Identity evidence -> report identity is `AI_BEHAVIOR_BIOMIMETIC_DESIGNER`, `CLI_PYTHON_BATTLE_SIMULATION`, `PENDING VERIFICATION`, `Data/AI/Leviathan_Brain.json`; tests reject wrong source prompt id, task count, domain, and report identity.
- Breakdown evidence -> tests reject tampered `profileBreakdown` totals, `tierBreakdown` rates, and missing `packCountBreakdown`.
- Breakdown key evidence -> tests reject missing required profile keys, extra tier keys, and wrong pack-count keys.
- Sample evidence -> tests reject missing samples, invalid sample profile labels, and contradictory sample outcomes.
- Counter evidence -> tests reject summary outcome-total drift, summary behavior-counter drift, missing summary context keys, and subgroup behavior-counter total drift.
- Audit evidence -> tests reject frustration target-range drift, frustration max-rate drift, and calibration final-aggression drift.
- Cooldown/formula evidence -> tests reject missing sensory formula feature tokens, RealAttack cooldown below declared minimum, and FalseCharge cooldown below declared minimum.
- Context/utility metadata evidence -> tests reject context order drift, invalid context bands, utility score id sequence drift, and missing utility reasons.
- Pack math evidence -> tests reject generic dot formulas without `math.dot`, pack rule id/order drift, and angle-math fallback in pack dot conditions.
- Temp hygiene -> updated tests to use system temp directories; no `Temp/AiBattleSimTests` or `Temp/AiBattleSimVerify.*` artifact remains in the repo.
