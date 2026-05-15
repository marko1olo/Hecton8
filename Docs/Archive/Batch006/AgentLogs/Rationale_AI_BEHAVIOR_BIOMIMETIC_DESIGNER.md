# Rationale - AI_BEHAVIOR_BIOMIMETIC_DESIGNER

## 2026-05-15 - Intake

Problem: The extracted prompt asks for a Leviathan utility brain, 50 utility scores, sensory weights, pack-hunting dot math, and a 10,000 encounter simulator. The batch header says 15 tasks but the XML contains 7 concrete numbered tasks.
Solution: Execute the 7 concrete tasks and document the count mismatch. Avoid inventing undocumented work.
Rejected Alternatives: Inventing eight extra tasks would violate the strict parsing protocol and create architecture drift.
Scalability potential: Low tier uses simple scalar utility tables and dot gates. Middle tier uses polynomial modifiers. High tier adds richer sensory composition. Ultra can spend saved CPU on presentation overkill after data authority is stable.
Hardware Impact: Data-table brain has no runtime allocation by itself. Expected low-end i3/MX350 gain is avoidance of branch-heavy ad hoc AI tuning; estimate pending until simulator outputs are generated.

Problem: Existing Unity runtime already contains `PredatorCognitionDomain` and `AlphaLeviathanCognitionContracts`; direct C# mutation risks compile walls and cross-agent conflict.
Solution: Build the requested brain as an authored data artifact plus offline simulator, with explicit GlobalDataVault feed names for future importer/runtime consumption.
Rejected Alternatives: Editing `PredatorCognitionDomain.cs` blindly is too risky during parallel batch execution and is not required by the prompt's concrete file paths.
Scalability potential: Data can be prepacked into contiguous arrays, tiered by Low/Middle/High/Ultra without changing runtime interfaces.
Hardware Impact: Avoids new hot-path logic and keeps C# compile blast radius at zero unless validation proves a missing contract.

Problem: The utility brain needed explicit state transitions without changing runtime code under parallel-agent pressure.
Solution: Authored `Data/AI/Leviathan_Brain.json` with 10 contexts x 5 behaviors = 50 utility score rows. The rows reference only named features backed by current GlobalDataVault BufferIDs from `H8Memory.cs`.
Rejected Alternatives: A monolithic behavior tree or direct C# switch table would create compile risk and hide tuning data from designers.
Scalability potential: Low reads scalar bands. Middle uses context rows. High evaluates pack candidates. Ultra spends saved logic on visual-overkill breach/circle presentation.
Hardware Impact: Expected low-end i3/MX350 benefit is zero added runtime allocation and cache-friendly prepack potential; estimated 2-12 us per decision depending tier after future importer work.

Problem: Pack hunting can become expensive and non-deterministic if treated as full group planning.
Solution: Defined dot-product flank gates using positions/velocities from `EntityAUPs`, `EntityVelocities`, and `EntityFlags`.
Rejected Alternatives: A* group tactics, Unity NavMesh, and real flock coordination were rejected as heavier than the needed terror cue.
Scalability potential: Low evaluates one candidate. Middle evaluates two. High/Ultra evaluate up to four and buy richer presentation when the math stays cheap.
Hardware Impact: Dot gates avoid acos/sqrt angle work; expected savings versus angular comparisons are low single-digit microseconds per decision on weak CPU.

Problem: The first simulator smoke produced no kills because the model overused far-distance hiding and startup cooldowns.
Solution: Tuned the authored data and simulator model to use shorter attack cooldowns, stronger combat truth only after valid utility selection, and coarser deterministic simulation steps. The final 10k run produced killRate 0.5224 and under30KillRate 0.0.
Rejected Alternatives: Lowering the target kill-rate floor to make a weak simulator pass was rejected. The data/model was corrected instead.
Scalability potential: Low-tier remains scalar and predictable. Ultra-tier still adds `visual_overkill_ultra` spectacle rows without increasing fast-kill frustration.
Hardware Impact: Runtime remains data-only pending importer. Offline simulator elapsed 195.149 s for 10,000 encounters with subgroup validation; no Unity frame cost added.

Problem: Self-audit required aggression reduction only if every encounter killed the player before 30 seconds.
Solution: Implemented calibration loop in `Tools/AiBattleSim.py`. It multiplies aggression by 0.82 and reruns if all-fast-kill or excessive under30 rate occurs. Final 10k run did not trigger the reduction: 5224 kills, 0 under-30 kills.
Rejected Alternatives: Always lowering aggression would weaken [GOD_MODE] and contradict the evidence.
Scalability potential: Same audit can run on future tuned tables before runtime import.
Hardware Impact: No runtime cost; offline guard prevents shipping a frustration table.

Problem: The prompt required all decisions to use data available in GlobalDataVault.
Solution: `Tools/AiBattleSim.py --check-artifacts` validates every utility row input against `globalDataVaultFeeds`; every feed references existing BufferID names discovered in `H8Memory.cs`.
Rejected Alternatives: Referencing direct AudioSource, Light, Camera, Transform, or scene-object state was rejected.
Scalability potential: Future importer can prepack these JSON rows into vault-owned NativeArrays without changing the decision semantics.
Hardware Impact: Avoids scene queries and object references in hot paths; projected MX350 benefit is stable 0 B/frame decision input flow once imported.

Problem: Omega polish required anti-bloat readback after core tasks, but the batch file has no `<POLISH_MANDATE>` tag.
Solution: Ran local anti-bloat scan against `Tools/AiBattleSim.py` and `Data/AI/Leviathan_Brain.json`. Findings were bounded: `AudioSource` appears only in a JSON rejection note; `math.sqrt` appears only inside the Python `rsqrt()` helper for offline 2D dot normalization.
Rejected Alternatives: Ignoring the missing polish tag or inventing a hidden polish task.
Scalability potential: No new runtime bloat introduced. Future importer should keep rows prepacked and avoid JSON parsing in gameplay.
Hardware Impact: Runtime impact remains 0 us because no Unity runtime path changed. Offline scan verified no direct Unity object query dependency in the new files.

Problem: The first global pass hid a subgroup problem: `flashlight_track` killRate was 0.79748, above the target max of 0.72.
Solution: Added `subgroupKillRateMax` to the brain contract and enforced it across profile, tier, and pack-count breakdowns in `Tools/AiBattleSim.py`. Tuned close-light behavior toward Circle/Breach spectacle and reduced light-assisted real-attack damage. Final maxProfileKillRate is 0.61865, maxTierKillRate 0.53716, maxPackKillRate 0.64279.
Rejected Alternatives: Keeping only global kill-rate validation was rejected because it can hide a player-style frustration trap.
Scalability potential: Future player profiles can be added to the same subgroup validator without Unity runtime mutation.
Hardware Impact: No runtime cost; offline validation added evidence coverage only.

Problem: The simulator and report could regress silently without tests.
Solution: Added `Tools/test_ai_battle_sim.py` covering the 50-row utility contract, current artifact check, short-run frustration contract, unknown vault-buffer rejection, missing-row rejection, and subgroup-guard rejection.
Rejected Alternatives: Trusting only the generated report was rejected because report artifacts can drift from code.
Scalability potential: Future brain schema additions can extend the same test file without touching Unity runtime.
Hardware Impact: No runtime cost; the initial offline suite covered the core artifact regressions before the later hardening pass expanded it.

Problem: The DataVault feed validator used a copied BufferID list, which can drift from the real `H8Memory.cs` enum.
Solution: Replaced copied source-of-truth validation with live parsing of `Assets/_Project/Scripts/Core/Memory/H8Memory.cs` BufferID enum. The current regenerated report records `knownBufferSourceStatus: SOURCE_PARSED` and `knownBufferCount: 66`.
Rejected Alternatives: Keeping the fallback list as the normal path was rejected because it can claim stale validity.
Scalability potential: New vault BufferIDs become valid automatically after the source enum changes; removed BufferIDs will fail artifact validation.
Hardware Impact: Offline validation cost only. Runtime impact remains 0 us because validation does not execute in Unity gameplay.

Problem: The artifact checker accepted too much trust from the generated report: stale validation counts, fallback BufferID source, and tampered kill-rate summaries could evade rejection if the top-level status remained `INSTINCTS DEFINED`.
Solution: Hardened `Tools/AiBattleSim.py::check_artifacts` to require live `SOURCE_PARSED` BufferID validation, compare report validation counts against fresh validation, enforce target kill-rate and under-30 guards from `selfAudit`, and reject subgroup cap drift even if the boolean was not manually flipped.
Rejected Alternatives: Trusting the report JSON because it was locally generated was rejected. Evidence artifacts must fail closed after any hand edit or schema drift.
Scalability potential: Future balance profiles can add more report fields while this checker still enforces the current authority chain: brain JSON -> live `H8Memory.cs` BufferID enum -> report audit.
Hardware Impact: Offline validation only. Runtime impact remains 0 us; this reduces integration risk without adding gameplay CPU or GC.

Problem: The regression test for subgroup-guard rejection wrote under `Temp/AiBattleSimTests`, leaving repo-local test debris after execution.
Solution: Switched report-mutation tests to `tempfile.TemporaryDirectory` with unittest cleanup and added two more regression tests for impossible target kill-rate drift and stale report validation drift.
Rejected Alternatives: Keeping repo-local temp artifacts was rejected because batch agents already produce enough untracked noise.
Scalability potential: More artifact tamper tests can reuse the system temp helper without polluting the workspace.
Hardware Impact: No runtime impact. Final test readback after digest hardening runs 11 tests in 13.026 s with `python -B`, so it does not create new bytecode in the project.

Problem: The report could still be a valid-looking artifact for a different brain revision because it lacked a hard fingerprint binding to the exact JSON and full simulated result stream.
Solution: Added `brainDigest` and `simulationDigest` SHA-256 fields to `Tools/AiBattleSim.py` reports, plus `simulatorSchemaVersion=2`. The artifact checker now rejects brain digest drift and invalid simulation digests. A strict `--verify-rerun` pass reruns all 10,000 encounters and compares digest, summary, and calibration against the saved report.
Rejected Alternatives: Trusting report numbers after schema hardening was rejected. A report must prove it belongs to the current brain and current simulator semantics.
Scalability potential: Future importer/CI can use cheap digest checks for normal validation and opt into `--verify-rerun` for release gates or suspicious balance edits.
Hardware Impact: Offline validation cost only. Runtime impact remains 0 us; the digest does not enter Unity gameplay. Current live `H8Memory.cs` BufferID count is 66, so the old 34-count evidence is obsolete.

Problem: The first strict rerun attempt exceeded the shell timeout before printing a footer under system load, even though the report file had been written.
Solution: Re-ran strict verification as a redirected background process, polled the output, and recorded only the completed result: `ARTIFACT_CHECK_PASSED`, `rerunVerified=True`, digest matched `97a10330bb86ded1a29b82aa896ac9acbe46d768fe0c403360c80e08bca50867`.
Rejected Alternatives: Treating the timed-out foreground command as proof was rejected.
Scalability potential: Long release-grade reruns can be run under redirected output without losing evidence to shell timeout.
Hardware Impact: No runtime impact. Temporary verifier output was removed after recording the result.

Problem: The validator proved 50 utility rows and 10 rows per behavior, but it did not prove that every context/behavior pair existed exactly once. Duplicate pairs could hide a missing transition while preserving row counts.
Solution: Added strict matrix validation to `Tools/AiBattleSim.py`: expected context count is 10, context ids must be unique, and all 50 context/behavior pairs must appear exactly once. The report now records `contextCount=10` and `utilityPairCount=50`.
Rejected Alternatives: Trusting total row count was rejected because duplicate rows can make the runtime lookup silently overwrite a missing pair.
Scalability potential: Future contexts or behaviors must update constants and tests deliberately, preventing silent drift in the utility surface.
Hardware Impact: Offline validation only. Runtime impact remains 0 us; this prevents an importer from packing a broken decision matrix.

Problem: Matrix validation changed the report validation schema, so the previous report failed the current artifact checker until regenerated.
Solution: Regenerated `Tools/AiBattleSim_Report.json`, reran artifact validation, reran the strict 10,000 encounter verification, and expanded the unit suite to 13 tests.
Rejected Alternatives: Keeping a stale report and documenting the mismatch was rejected. Evidence must match the current validator.
Scalability potential: CI can detect stale report schemas through `report.validation contextCount/utilityPairCount mismatch`.
Hardware Impact: No runtime impact. Strict rerun remained offline and passed with the same brain and simulation digest.

Problem: The brain JSON contained important contracts beyond utility scores, but the validator did not enforce them. A malformed behavior parameter block, missing Math LOD tier, bad cadence hysteresis, missing dot rule, or broken black-box telemetry contract could pass if the utility rows remained valid.
Solution: Added full contract validation to `Tools/AiBattleSim.py`: behavior parameter count, numeric parameter ranges, cinematic cheat presence, cadence bounds with hysteresis, self-audit thresholds, all four Math LOD tiers, four dot-based pack rules, and 300-frame black-box telemetry with the required dump path/fields.
Rejected Alternatives: Treating those fields as documentation-only was rejected. They are importer/runtime contracts and must fail closed before handoff.
Scalability potential: Future runtime import can trust the report to reject incomplete tiering or telemetry drift before packing data into vault-owned arrays.
Hardware Impact: Offline validation only. Runtime impact remains 0 us. The black-box contract remains data-level until an AI runtime owner maps it to the actual telemetry ring.

Problem: Full contract validation changed the report schema and made the pre-existing report stale.
Solution: Regenerated `Tools/AiBattleSim_Report.json`, reran artifact validation, reran strict 10,000 encounter verification, and expanded the unit suite to 19 tests. Current report records `behaviorParameterCount=5`, `mathLodTierCount=4`, `packRuleCount=4`, and `blackBoxCapacityFrames=300`.
Rejected Alternatives: Keeping the stale report was rejected for the same evidence-integrity reason as prior schema changes.
Scalability potential: CI can detect stale contract counts through report validation mismatches.
Hardware Impact: No runtime impact. Strict rerun remains offline and passed with unchanged brain/simulation digests.

Problem: Some malformed inputs could stress validator error paths instead of returning clean failure evidence. Missing `behaviorOrder` could throw during tuple conversion, invalid self-audit fields could throw during artifact checks, and report-side `behaviorCounts` drift was not compared.
Solution: Added fail-closed validation helpers to `Tools/AiBattleSim.py`: malformed `behaviorOrder` now records `behaviorOrder drift`; artifact self-audit reads use numeric guards; `report.validation.behaviorCounts` is compared against fresh validation. Added regression tests for all three cases.
Rejected Alternatives: Assuming only valid JSON reaches the checker was rejected. Batch artifacts are hand-edited by multiple agents and must fail cleanly.
Scalability potential: CI and future importer scripts can consume checker failures as structured evidence instead of crashing on malformed artifacts.
Hardware Impact: Offline validation only. Runtime impact remains 0 us.

Problem: Fail-closed checker changes required fresh evidence.
Solution: Regenerated `Tools/AiBattleSim_Report.json`, reran artifact validation, reran strict 10,000 encounter verification, and expanded the unit suite to 22 tests.
Rejected Alternatives: Reusing the old report was rejected because digest/schema evidence must track the current checker.
Scalability potential: More malformed-input tests can use the existing temp-brain/temp-report helpers without writing repo-local test debris.
Hardware Impact: No runtime impact. Strict rerun remains offline and passed with unchanged brain/simulation digests.

Problem: The validator proved referenced GlobalDataVault buffers existed, but it did not enforce the exact feature-lane set required by the authored brain. A missing unused lane, duplicated lane, or extra non-contract lane could drift the importer contract.
Solution: Added `EXPECTED_FEATURES` to `Tools/AiBattleSim.py` and required the exact seven decision lanes: `distanceSq01`, `sound01`, `light01`, `movement01`, `lineOfSight01`, `packSynergy01`, and `attackCooldown01`. Missing, extra, and duplicate features now fail validation.
Rejected Alternatives: Allowing open-ended feed rows was rejected because this is a handoff contract, not a loose notes file.
Scalability potential: Future feature lanes must be added deliberately to the expected set, tests, and report evidence instead of drifting into the JSON silently.
Hardware Impact: Offline validation only. Runtime impact remains 0 us; future importer gets a stable lane set for prepacked NativeArray ingestion.

Problem: DataVault lane hardening changed validator semantics and needed fresh evidence.
Solution: Regenerated `Tools/AiBattleSim_Report.json`, reran artifact validation, reran strict 10,000 encounter verification, and expanded the unit suite to 25 tests. Current report records `globalDataVaultFeedCount=7`.
Rejected Alternatives: Reusing the old evidence was rejected because task 7 is explicitly about DataVault-backed decisions.
Scalability potential: CI can reject feature-lane drift before runtime import.
Hardware Impact: No runtime impact. Strict rerun remains offline and passed with unchanged brain/simulation digests.

Problem: The brain and report had identity/source metadata, but the checker did not enforce it. A copied artifact from another agent, wrong domain, wrong task count, or wrong report identity could pass if the technical rows still looked valid.
Solution: Added identity/source validation to `Tools/AiBattleSim.py`: expected agent id, role, domain, brain evidence class, runtime proof status, prompt path, prompt id, numbered task count 7, header task claim 15, report agent id, report evidence class, runtime proof status, and brain path. Added regression tests for wrong prompt id, wrong task count, wrong domain, and report identity drift.
Rejected Alternatives: Treating identity metadata as decorative was rejected. Batch protocol requires exact prompt extraction and domain binding.
Scalability potential: CI can now reject cross-agent artifact reuse before data reaches an importer.
Hardware Impact: Offline validation only. Runtime impact remains 0 us.

Problem: Identity/source binding changed validator semantics and required fresh evidence.
Solution: Regenerated `Tools/AiBattleSim_Report.json`, reran artifact validation, reran strict 10,000 encounter verification, and expanded the unit suite to 29 tests.
Rejected Alternatives: Reusing old evidence was rejected because source identity is part of the batch contract.
Scalability potential: Future source-prompt changes must be updated deliberately in constants and tests.
Hardware Impact: No runtime impact. Strict rerun remains offline and passed with unchanged brain/simulation digests.

Problem: Normal artifact validation checked the top-line summary and relied on strict rerun for full consistency, but profile/tier/pack breakdown tables could be tampered without being caught by the cheap check.
Solution: Added breakdown consistency validation to `Tools/AiBattleSim.py`: profile, tier, and pack-count breakdowns must exist; their encounters, kills, escaped, timeouts, and under-30 totals must match the summary; per-row kill rates and under-30 rates must match row counts.
Rejected Alternatives: Requiring `--verify-rerun` for every subgroup consistency check was rejected because cheap internal consistency catches common report drift faster.
Scalability potential: CI can run normal artifact checks frequently and reserve strict reruns for release gates.
Hardware Impact: Offline validation only. Runtime impact remains 0 us.

Problem: Breakdown consistency needed regression coverage.
Solution: Added tests for tampered `profileBreakdown` kill totals, tampered `tierBreakdown` kill rate, and missing `packCountBreakdown`. Test suite now has 32 tests.
Rejected Alternatives: Depending on strict rerun only was rejected because normal artifact validation should be meaningful on its own.
Scalability potential: Future breakdown groups can reuse the same validator pattern.
Hardware Impact: No runtime impact. Strict rerun remains offline and passed with unchanged brain/simulation digests.

Problem: Breakdown consistency still allowed the wrong subgroup universe if totals matched. A report could omit `disciplined_escape`, invent a tier, or use a non-contract pack-count key while preserving summary totals.
Solution: Added exact subgroup-key validation to `Tools/AiBattleSim.py`: profile keys must match all six dummy-player profiles, tier keys must match Low/Middle/High/Ultra, and pack keys must match 0/1/2/3. Added tests for missing profile key, extra tier key, and wrong pack key.
Rejected Alternatives: Trusting whatever subgroup labels appear in the JSON was rejected because it allows silent evidence drift after profile/tier changes.
Scalability potential: Future profile, tier, or pack-count expansion must update constants, report generation, validation, and tests deliberately.
Hardware Impact: Offline validation only. Runtime impact remains 0 us; the strict 10,000 rerun still passed with unchanged brain and simulation digests.

Problem: The previous prompt extraction readback command timed out and printed a neighboring prompt fragment, which is unusable evidence.
Solution: Re-extracted the agent prompt with a bounded CLI regex against `Docs/Tasks/CURRENT_BATCH.md` and confirmed the exact `AI_BEHAVIOR_BIOMIMETIC_DESIGNER` block: 7 numbered tasks and required status `INSTINCTS DEFINED`.
Rejected Alternatives: Treating the timed-out neighboring fragment as acceptable was rejected. Batch parsing evidence must point to this agent tag only.
Scalability potential: Bounded tag extraction avoids contamination from neighboring agents during later anti-amnesia checks.
Hardware Impact: Offline documentation/readback only. Runtime impact remains 0 us.
