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
Solution: Replaced copied source-of-truth validation with live parsing of `Assets/_Project/Scripts/Core/Memory/H8Memory.cs` BufferID enum. The report now records `knownBufferSourceStatus: SOURCE_PARSED` and `knownBufferCount: 34`.
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
Hardware Impact: No runtime impact. Final test readback runs 9 tests in 3.618 s with `python -B`, so it does not create new bytecode in the project.
