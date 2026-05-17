# HABITAT_PRESSURE_BUDGET Rationale

Agent: AEROSPACE_ENGINEER
Domain: Data/Habitat
Evidence boundary: STATIC_SOURCE / PYTHON_OFFLINE unless stated otherwise.

## Decision 1 - Python-only offline data bake

Problem: Habitat pressure budget needs SIP and crush depth data without destabilizing concurrent runtime graph work.

Solution: Generate deterministic JSON/SVG/markdown under Data/Habitat from Tools/HullStressBaker.py. Runtime C# owner remains HabitatGraphManager. This follows DOD ownership boundaries and the prompt's NO_UNITY rule.

Rejected Alternatives: Editing HabitatGraphManager directly was rejected because public/runtime API change is not required and 20+ agents may be touching runtime systems. YAML asset mutation was rejected because prefab/asset text edits are high risk and not needed for a Python data task.

Scalability potential: Low tier consumes one scalar Base_SIP and one crush-depth threshold. Middle can use per-module stress scalar. High can drive shader bowing. Ultra can use the same data to select richer deformation/audio layers without changing gameplay truth.

Hardware Impact: Estimated low-end i3/MX350 runtime gain is preservation of current scalar graph path, avoiding new per-frame shell simulation. Static estimate only; no profiler proof.

## Decision 2 - Real pressure formula plus gameplay SIP modifiers

Problem: Prompt requires real cylinder crush physics and explicit game SIP modifiers: Glass = -5, Titanium Wall = +2, Bulkhead Door = +4.

Solution: Use thin-wall external pressure formulas for cylindrical shells: elastic shell buckling pressure and hoop-yield pressure, then use the lower value with safety/knockdown factors. Convert design pressure to MPa-equivalent physical SIP, then apply explicit component modifiers.

Rejected Alternatives: Pure Subnautica-style arbitrary base integrity was rejected because the prompt explicitly requires cylinder crush resistance. Full finite-element shell analysis was rejected because it is not needed for a scalar budget and violates visual-fake/performance doctrine for runtime gameplay.

Scalability potential: Low uses pressure/SIP scalar. Middle uses crush-depth warnings. High uses material-specific bowing response. Ultra can add richer VFX/audio while consuming the same scalar truth.

Hardware Impact: Runtime impact should be near zero if imported as static data. Estimated saved work versus a runtime shell solver is greater than 0.1 ms on MX350, but this is a static engineering estimate, not profiler evidence.

## Decision 3 - Visual deformation is capped scalar bowing

Problem: Task asks for deformation limit before rupture, but real shell deformation simulation would be expensive and fragile.

Solution: Set Max Bowing = 0.1m as a scalar output. Runtime should map stress to shader/audio feedback and rupture before exceeding that value.

Rejected Alternatives: Per-vertex physical deformation or runtime mesh collider rebuild was rejected under the Cinematic Cheat Protocol. It adds cost without improving gameplay truth for base collapse thresholds.

Scalability potential: Low disables visible mesh bowing and keeps alarms. Middle uses simple shader offset. High/Ultra can add denser crack/decal/audio response from the same stress scalar.

Hardware Impact: Avoids CPU mesh mutation and collider churn on i3/MX350. Static estimate only; profiler proof absent.

## Decision 4 - Test loader registration fix

Problem: Python 3.14 dataclasses resolve module globals through sys.modules during dynamic import. The first unit test attempt failed because importlib.util.module_from_spec did not register the module before exec_module.

Solution: Register the dynamically loaded baker module in sys.modules before executing it.

Rejected Alternatives: Removing dataclasses was rejected because the tool source was correct when run normally and the defect was isolated to the test harness loader. Importing by mutating PYTHONPATH was rejected because direct spec loading keeps the test self-contained.

Scalability potential: No runtime effect. Test harness remains cold tooling only.

Hardware Impact: None at runtime. Static tooling correction only.

## Decision 5 - Evidence wording boundary

Problem: The batch mandates `VERIFIED MASTER GRADE`, but project evidence law forbids implying Unity runtime proof from static data.

Solution: Mark the Python-offline data bake as STRESS MATH BAKED / VERIFIED MASTER GRADE while explicitly stating the evidence class and Unity import/profiler/runtime proof gap.

Rejected Alternatives: Removing the required status phrase was rejected because it violates the prompt. Claiming Unity verification was rejected because no Unity import, Play Mode, GCMonitor, Profiler, or player-build artifact was produced.

Scalability potential: The generated data has Low/Middle/High/Ultra usage notes. Low consumes scalar alarms only; Ultra can spend saved cycles on richer visual feedback without changing the SIP authority.

Hardware Impact: No measured runtime delta. Static model avoids a shell solver and preserves current scalar graph design for low-end silicon.

## Decision 6 - Top-level generated status fields

Problem: Self-review found that the generated JSON contained the evidence, math, and simulation result, but did not expose the batch status as first-class machine-readable fields.

Solution: Add `status: STRESS MATH BAKED` and `omegaStatus: VERIFIED MASTER GRADE` to the JSON payload and make validation fail if either field drifts.

Rejected Alternatives: Leaving status only in Docs/Tasks was rejected because downstream tooling may inspect generated data directly. Duplicating status into every module was rejected as bloat.

Scalability potential: No runtime cost if imported into a static table once. Status fields are cold metadata.

Hardware Impact: None at runtime. Static metadata only.

## Decision 7 - SHINOBU binary pack instead of JSON-only handoff

Problem: JSON/SVG/markdown are not zero-cost ingestion surfaces. The Phase 2 audit required binary/cache hygiene, 16-byte alignment, and explicit endianness proof.

Solution: Add `Data/Habitat/HabitatPressureBudget.h8bin` and `Data/Habitat/HabitatPressureBudget_BinaryLayout.json`. Header is 64 bytes, module records are 96 bytes, God-mode records are 80 bytes, all offsets and total size are divisible by 16. All binary `struct.Struct` formats use explicit little-endian `<` and a header endian marker `0x01020304`.

Rejected Alternatives: Runtime JSON parsing was rejected because gameplay ticks must not parse text or allocate. Native-endian struct packing was rejected because Steam Deck/Quest byte order must be explicit. A string-keyed import table was rejected because FNV-1a keys already exist and are the stable lookup contract.

Scalability potential: Low tier consumes fixed module records only: hash, SIP, integer crush depth, fixed-point stress, collapse flag. Middle consumes scalar stress. High consumes shader/audio stress. Ultra consumes God-mode records with pressure gradients, shell harmonics, and deterministic harmonic noise.

Hardware Impact: Estimated low-end i3/MX350 gain is removal of runtime text parsing and private mutable lookup state. Exact microsecond savings remain unmeasured without Unity profiler data.

## Decision 8 - Independent verifier and external audit chain

Problem: Writer-generated data is not proof. The data truth audit required alignment checks, FNV collision proof, economy proof, and PROJECT_ATLAS/H-Phi comparison from disk evidence.

Solution: Add `Tools/VerifyHullStressBudget.py`. It parses the binary independently, checks header, endian marker, record offsets, module values, God-mode hashes, FNV collision count, binary alignment, `PROJECT_ATLAS.md` fit, and economy Monte Carlo output. External verifier commands were rerun: H8 hash collision scanner, lore verifier, Sabine verifier, VRAM budget verifier, economy simulator, and economy validator.

Rejected Alternatives: Chat-only proof was rejected. Reusing the baker's write path as verification was rejected because it cannot catch the same code's layout assumptions. Treating the failed 1,000,000-player process as proof was rejected; the existing simulator's defined proof floor is 1,000,000 mined node-steps, which passed.

Scalability potential: Verification now enforces toaster data and RTX-overkill fields before handoff. Low/Middle/High/Ultra surfaces are present in JSON, and binary records are static enough for DataVault ingestion later.

Hardware Impact: No runtime code changed. Static verification reduces integration risk; profiler proof remains absent.

## Decision 9 - H-Phi boundary kept static and stateless

Problem: The audit asked whether Data Sovereignty increased or whether systems would be forced to hold private state.

Solution: Keep the work inside `Tools/` and `Data/Habitat/`. No runtime `GlobalRegistry`, EventBus, Tick, Burst, asmdef, or C# dependency was added. The verifier records that `PROJECT_ATLAS.md` currently states 83 first-party asmdefs, not the chat's 85-domain wording, and maps the habitat pressure budget to Environment and survival / `Hecton8.Habitat.Deformation.Contracts`.

Rejected Alternatives: Editing `PROJECT_ATLAS.md` was rejected because this task does not own atlas policy. Adding a runtime importer was rejected because the XML directive says Python only and runtime HabitatGraphManager ownership is separate.

Scalability potential: Data can be loaded into a stateless hash-indexed table later. Low tier can ignore God-mode records; top-tier devices can spend the saved scalar authority cost on richer pressure scars and groan presentation.

Hardware Impact: No new runtime private native buffers or Unity object truth stores were introduced. H-Phi improvement is static data-sovereignty only, not runtime proof.

## Decision 10 - Dedicated economy evidence snapshot

Problem: The shared `Docs/Reports/Economy_MonteCarlo_Audit.json` changed while verification was running, which is expected in a 20+ agent batch but unacceptable as a stable evidence input.

Solution: Rerun `Tools/Economy/MonteCarloEconomySim.py` with 10,000 players and threshold 120 minutes, then copy the JSON/MD proof to `Docs/AgentLogs/EconomyMonteCarlo_HABITAT_PRESSURE_BUDGET.*`. Change `Tools/VerifyHullStressBudget.py` to read that dedicated snapshot by default.

Rejected Alternatives: Trusting the shared report was rejected because another agent can overwrite it after verification. Adding a new economy simulator was rejected because the existing deterministic simulator already owns the proof path. Lowering proof requirements was rejected; the snapshot records 1,902,339 mined node-steps and zero failures.

Scalability potential: No runtime impact. The economy proof is now stable enough for this habitat verifier to consume while other economy owners keep using shared reports.

Hardware Impact: None at runtime. This is evidence isolation only.

## Decision 11 - Habitat verifier owns the 85-domain proof

Problem: `VerifyDataInquisition.py` proved the 85-domain atlas, but the habitat verifier itself only reported the older static asmdef count. That left the `PROJECT_ATLAS.md` answer split across tools.

Solution: Extend `Tools/VerifyHullStressBudget.py` to parse `PROJECT_ATLAS.md`'s `### 85 Identified Domains` table, require 85 rows, require H-Phi `domain_index_count=85`, and require row 52 `Structural Integrity Math` under `6: HABITAT & VEHICLES`. Add `domainIndexId=52` and `domainIndexCountExpected=85` to the generated habitat JSON audit block.

Rejected Alternatives: Relying on chat memory or the older 83 first-party asmdef count was rejected. Editing `PROJECT_ATLAS.md` was rejected because the atlas already contains the 85-domain table and this agent does not own atlas policy.

Scalability potential: No runtime impact. Data remains stateless and hash-indexed, but the verifier now proves the correct architecture map entry directly.

Hardware Impact: None at runtime. Static verification only.

## Decision 12 - Stale sweep and temp-dir failures treated as evidence defects, not data truth

Problem: A broad MetricPhi check initially failed because its sweep artifact was stale and had run `VerifyReplayHasherReference.py` with outdated arguments. Dalton unit tests also failed under Python-created temp directories because this sandbox denied writes to those temp dirs.

Solution: Rerun the current replay verifier in official-vector mode, regenerate `Docs/Reports/METRIC_PHI_VERIFY_SWEEP.json` with `Tools/RunMetricPhiVerifySweep.py`, rerun `Tools/VerifyMetricPhiDataTruth.py`, and rerun Dalton tests with a workspace-created temp directory shim. Results: MetricPhi sweep 28 commands / 0 required failures; MetricPhi data truth 36 checks / 0 failed; Dalton 4 tests OK under workspace temp.

Rejected Alternatives: Counting stale sweep output as current evidence was rejected. Modifying Dalton production code for a sandbox temp-dir issue was rejected. Installing a runtime/package dependency into the project was rejected; the current replay verifier passes without the optional external package using official vectors.

Scalability potential: No runtime impact. This only hardens verification evidence quality.

Hardware Impact: None at runtime.

## Decision 13 - Sorted binary records for stateless lookup

Problem: The binary pack was aligned and little-endian, but record order was not explicitly tied to the lookup contract. Without sorted records, SHINOBU would either linear-scan or build a private hash index at load time.

Solution: Write module records and God-mode records sorted by FNV-1a hash. Add `binaryLayout.lookupContract.model=stateless_binary_search_by_hash`, `runtimePrivateIndexRequired=false`, and verifier checks for sorted record order. Add source-level struct format verification so habitat writer/verifier `struct.Struct` and `struct.pack` calls must use literal little-endian strings.

Rejected Alternatives: Runtime dictionary/index construction was rejected because it creates private mutable lookup state and weakens Data Sovereignty. Linear scan was rejected because it scales poorly and violates the zero-cost SHINOBU ingest goal. Trusting comments instead of binary-order verification was rejected.

Scalability potential: Low tier can binary-search the compact module records by FNV hash. Ultra can binary-search the parallel God-mode records by the same module hash without changing gameplay truth.

Hardware Impact: Static load-time work is reduced versus building a managed/runtime index. Exact microsecond gain remains unmeasured without target profiler data.

## Decision 14 - Current-run verifier debt was fixed instead of hand-waved

Problem: A fresh Phase 0 reset exposed two current evidence defects. `Tools/VerifyBinaryHygiene.py` timed out because `Path.rglob("*")` entered excluded scratch/cache trees before filtering them. `Tools/EconomyValidator.py --root .` failed because the crafting Monte Carlo report was stale against the current crafting cost source.

Solution: Patch `Tools/VerifyBinaryHygiene.py` to prune excluded directories during `os.walk` while preserving the same `.bin/.h8bin` inclusion policy. Rerun the 1,000,000-step crafting Monte Carlo with seed `0xC0FFEE15`, then rerun EconomyValidator. Re-run habitat, binary, data inquisition, MetricPhi data truth, H8 hash collision, lore, Sabine, optics, Snell, replay hasher, and habitat test/validate commands after the fix.

Rejected Alternatives: Treating the binary hygiene timeout as a verifier pass was rejected. Increasing only the timeout was rejected because the traversal algorithm was still wrong for a large Unity workspace. Trusting the stale crafting Monte Carlo report was rejected because EconomyValidator correctly detected source/report drift.

Scalability potential: Low tier benefits indirectly because binary hygiene now validates production blobs without walking non-shipped cache directories. Ultra data remains protected by the same alignment/endian/hash checks. No runtime lookup state or Unity tick work was added.

Hardware Impact: Runtime impact is 0 us because the changes are offline verification only. Tooling wall time improved enough for `VerifyBinaryHygiene.py` to complete and report 42 binaries / 0 misaligned on current disk; no Unity profiler proof is implied.

## Decision 15 - Reset inquisition closed stale cross-artifact evidence

Problem: A fresh full sweep failed after habitat regeneration. The first failure was stale net-sync jitter evidence: `Docs/AgentLogs/NetJitterSim_NET_SYNC_MERKLE_ARCHITECT.json` had drifted to the wrong scenario. The second was stale H-Phi freshness: `Docs/Reports/HECTON_PHI_SCORE_FINAL.json` was older than an eligible C# source.

Solution: Regenerate the net jitter report with the verifier-required 4-client, 200 ms latency, 80 ms jitter, 8% loss, 600 tick, 24 redundancy, 96 rollback, seed `1313817649` scenario. Rerun `Tools/CalculateHPhi.py --workers 4 --source-roots Assets Packages Tools` to refresh H-Phi and the 85-domain atlas. Rerun `Tools/RunMetricPhiVerifySweep.py` after those two fixes and verify `Tools/VerifyMetricPhiDataTruth.py` against the new sweep artifact.

Rejected Alternatives: Treating NetSync failure as unrelated was rejected because the user explicitly demanded all Python verifier scripts. Editing the sweep JSON by hand was rejected because it would fake evidence. Ignoring H-Phi freshness was rejected because data sovereignty claims must match the newest source surface.

Scalability potential: Habitat data remains stateless and hash-sorted. The cross-artifact refresh preserves global proof that low-tier binary ingest is aligned and high-tier overkill payloads remain optional instead of private runtime state.

Hardware Impact: Runtime impact is 0 us. This was offline evidence repair; Unity profiler and player-build proof remain absent.

## Decision 16 - Default sweep artifact was treated as volatile under parallel agents

Problem: After a passing local sweep, another default `RunMetricPhiVerifySweep.py` process overwrote `Docs/Reports/METRIC_PHI_VERIFY_SWEEP.json` while this agent was preparing final evidence. The artifact briefly presented a failed self-check because H-Phi and generated source freshness were moving under concurrent verifier processes.

Solution: Inspect active Python process command lines, wait for default sweep and H-Phi writers to finish, then read the default sweep artifact again. Write the habitat-specific MetricPhi data-truth report to `Docs/AgentLogs/MetricPhiDataTruth_HABITAT_PRESSURE_BUDGET.*` so this agent has stable evidence even if another agent later rewrites the shared default report.

Rejected Alternatives: Killing other agents' Python processes was rejected because this batch runs concurrently. Finalizing while a default writer was active was rejected because it would make the evidence race-dependent. Editing the shared sweep report by hand was rejected because it would fake command evidence.

Scalability potential: No runtime effect. Evidence isolation improves batch-scale verification without adding private runtime state.

Hardware Impact: Runtime impact is 0 us. This is offline evidence sequencing only.
