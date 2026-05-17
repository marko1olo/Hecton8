# XENO_TAXONOMY_WRITER Log

## 2026-05-16 Taxonomy Compilation

What was wrong:
- No dedicated `Data/Localization/en_US_Taxonomy.json` existed for the batch taxonomy/autopsy mandate.
- Active fauna data references 22 current fauna templates, while the prompt minimum asked for 20 reports. Stopping at 20 would leave active fauna undocumented.
- Existing scanner entries were not clinical autopsy reports and did not carry biome hashes, binomial validation, or exact engine damage/tool contracts.

What was done:
- Added `Tools/Taxonomy/compile_taxonomy.py`.
- Added `Tools/Taxonomy/verify_taxonomy.py`.
- Generated minified `Data/Localization/en_US_Taxonomy.json`.
- Wrote 22 fauna autopsy reports, 10 L-system flora biopsy entries, and 6 corrupted Leviathan necropsy variants.
- Precomputed FNV-1a UTF-16LE hashes for LocIDs, entity IDs, biome IDs, and family IDs.
- Linked harvest guidance to exact project constants/tool IDs: `CombatDamageTypes.*`, `CombatArmorClass.*`, `HarvestableTemplate.MaterialClass.*`, `FloraDataTemplate.VulnerabilityMask.*`, `tool_survival_blade`, `tool_harpoon_launcher`, `tool_laser_cutter`, `tool_stun_pistol`, `tool_salvage_sampler`, and `tool_env_analyzer`.
- Embedded Lotka-Volterra predator-prey coefficients from `EcosystemDirector.cs:720-723`: prey birth `0.012`, predation `0.00045`, predator growth `0.00014`, predator death `0.006`.

Cinematic Cheats used:
- Taxonomy is offline localization data only; no creature simulation, scene objects, prefabs, ScriptableObjects, or Unity runtime logic were added.
- Flora entries describe global shader sway, flow cues, and presentation alarms instead of per-frond or fluid simulation.
- Corrupted Leviathan variants are stress-gated text payloads; no runtime hallucination system was implemented.

Exact Microseconds saved:
- Profiler-backed microseconds saved: `0` claimed. No profiler artifact exists and no runtime code was changed.
- Static runtime cost added: `0 us/frame` because edits are offline scripts and localization JSON only.
- Expected avoided cost versus ScriptableObject/Unity import path: PENDING VERIFICATION, not claimed as a measured saving.

Evidence:
- `python Tools\Taxonomy\compile_taxonomy.py` wrote `Data\Localization\en_US_Taxonomy.json`; output counts: fauna `22`, flora `10`, madness `6`.
- `python -m py_compile Tools\Taxonomy\compile_taxonomy.py Tools\Taxonomy\verify_taxonomy.py` exited `0`.
- `python Tools\Taxonomy\verify_taxonomy.py Data\Localization\en_US_Taxonomy.json` returned `TAXONOMY VERIFY PASS fauna=22 flora=10 madness=6 status=TAXONOMY COMPILED polishStatus=VERIFIED MASTER GRADE`.
- Stats pass: bytes `50066`, newline_count `0`, max Text `278`, max harvest note `94`, max weak point `40`.

REGRESSION MODEL:
- CPU: no Unity runtime code touched; offline compiler/verifier only.
- GC: no gameplay hot path touched; runtime GC proof absent.
- Memory: JSON payload is 50,066 bytes; localization runtime import not measured.
- Cadence: no Tick/Update/SlowTick cadence changed.
- Correctness: verifier checks binomial format, counts, hashes, constants, minification, UI limits, and final status.

HOT PATH IMPACT:
- No gameplay hot path impact. Evidence class is STATIC_DOC plus CLI_PYTHON_SCRIPT.

FAILURE MODES:
- Future runtime localization loader may not ingest this separate taxonomy file until wired.
- UI display still needs Unity/PDA route proof.
- If engine constants are renamed later, verifier whitelist must be updated.

WHY KEPT/REJECTED:
- Kept separate taxonomy JSON to avoid concurrent edits to existing `en_US.json`.
- Rejected Unity ScriptableObject authoring because task 8 required offline-only work.
- Rejected runtime simulation or creature-system dependencies; taxonomy only describes existing ecology and harvest contracts.

## 2026-05-16 Hard-Data Reaudit

What was wrong:
- First taxonomy pass had no `.h8bin` cache, no explicit endian/alignment audit, no independent million-step economy proof, and no atlas/H-Phi audit artifact.
- Hash collision proof existed only inside taxonomy checks, not as a project-wide hash sweep.
- The global economy Monte Carlo report can be overwritten by unrelated audit runs, so it is not stable evidence for this agent.

What was done:
- Updated `Tools/Taxonomy/compile_taxonomy.py` to emit `Data/Localization/en_US_Taxonomy.h8bin` plus `Docs/AgentLogs/TaxonomyBinaryAudit_XENO_TAXONOMY_WRITER.json`; initial V1 layout was superseded by binary V2 in the later full-sweep pass.
- Updated `Tools/Taxonomy/verify_taxonomy.py` to verify binary magic/version, explicit little-endian `<` structs, CRCs, 16-byte file/text alignment, toaster/RTX data, sterile-lore bans, industrial clinical terms, zero distinct-ID FNV collisions, atlas metadata, and H-Phi stateless lookup flags.
- Added `Tools/Taxonomy/audit_taxonomy_data.py`, writing `Docs/AgentLogs/TaxonomyDataAudit_XENO_TAXONOMY_WRITER.json/.md`.
- Added `Tools/Taxonomy/run_taxonomy_economy_million_step.py`, writing `Docs/AgentLogs/TaxonomyEconomyMillionStep_XENO_TAXONOMY_WRITER.json/.md`.
- Captured economy recipe graph proof in `Docs/Reports/Economy_Integrity_Audit_XENO_TAXONOMY_WRITER.json/.md`.
- Ran adjacent data verifiers: Lore, H8 hashes, Babel, Crafting Costs, Sabine Baker, Tide Baker.

Cinematic Cheats used:
- Taxonomy remains data-only. Ultra visuals are hash-derived codex overlay metadata, not simulated biology, pressure, optics, or acoustics.
- Physics-owned proofs stay in their own data systems: Sabine/Beer-Lambert/HydrostaticPressure via `VerifySabineBaker.py`; taxonomy declares no physics LUT/matrix ownership.
- Low-tier toaster path is stripped text/hash data; RTX path is extra presentation metadata off the same IDs.

Exact Microseconds saved:
- Measured runtime savings: `0 us/frame` claimed; no Unity runtime loader, scene, prefab, or C# hot path changed.
- Expected low-end benefit if future loader consumes `.h8bin`: reduced JSON parsing and direct aligned record lookup. This is PENDING VERIFICATION, not measured.
- Microseconds lost in runtime: `0 us/frame` by source scope.

Evidence:
- Initial `python Tools\Taxonomy\compile_taxonomy.py` V1 pass wrote JSON and `.h8bin`; binary bytes `10336`, aligned16 `True`. Superseded by V2 evidence below.
- `python Tools\Taxonomy\verify_taxonomy.py Data\Localization\en_US_Taxonomy.json` returned `hashCollisions=0 binaryAligned16=yes`.
- `python Tools\Taxonomy\run_taxonomy_economy_million_step.py` returned `players=5299 steps=1000220 failures=0 cycles=0`.
- `python Tools\Taxonomy\audit_taxonomy_data.py` returned `TAXONOMY DATA AUDIT PASS hashCollisions=0 binaryAligned16=True monteCarloSteps=1000220 atlasDomains=85`.
- `python Tools\VerifyLore.py --check` returned lore blob `alignment=16 endian=<`.
- `python Tools\VerifyH8HashCollisions.py --write-json ... --write-report ...` returned records `1018`, collisions `0`.
- `python Tools\VerifyBabel.py` returned records `32443`, bytes `1517184`, alignment `16`, endian `little`, hashCollisions `0`.
- `python Tools\VerifyCraftingCosts.py` returned binary bytes `6608`, alignment `16`, endian `<`, hash_pairs `175`, collisions `0`.
- `python Tools\VerifySabineBaker.py` returned `SABINE_LUT_VERIFIED`, record `<ff`, SIMD group `<ffff`, tiers `high,middle,rtx_overkill,toaster_i3`.
- `python Tools\VerifyTideBaker.py` returned `status: PASS`.

REGRESSION MODEL:
- CPU: offline Python only. Runtime CPU unchanged.
- GC: no Unity hot path touched. GCMonitor proof not applicable to this offline data task.
- Memory: added bounded JSON/binary/report files. Runtime memory not retained because no loader was changed.
- Cadence: no Tick/Update/SlowTick cadence changed.
- Correctness: hard gates now cover binomial format, hashes, binary layout, lore tone, scalability fields, atlas domain fit, economy million-step path, and graph cycles.

HOT PATH IMPACT:
- None by source scope. Evidence class remains CLI/static until a runtime localization/data-vault loader is wired and profiled.

FAILURE MODES:
- `en_US_Taxonomy.h8bin` is not yet wired to a runtime loader.
- Taxonomy Ultra metadata is presentation-only; if future codex UI uses it, UI profiling is required.
- Economy proof covers current first-base raw demand and recipe graph cycles; broader live gameplay economy remains PENDING VERIFICATION.

WHY KEPT/REJECTED:
- Kept offline artifacts to obey NO_UNITY and avoid cross-agent runtime conflicts.
- Rejected fake Beer-Lambert/Dalton/Sabine constants in taxonomy because those belong to audio/optics/pressure data systems.
- Rejected global `Economy_MonteCarlo_Audit.json` as the only proof because concurrent audit runs can overwrite it.

## 2026-05-16 Full Verify Sweep / Binary V2

What was wrong:
- Binary V1 held main text offsets but did not expose Toaster summary and RTX-overkill metadata as binary payload lanes.
- The first full `Verify*.py` sweep failed on two objective points: stale Babel dictionary bytes and a missing economy-proof path for hull stress verification.
- `VerifyReplayHasherReference.py` required an explicit external `xxhash` module path.

What was done:
- Upgraded taxonomy binary cache to version `2`, record format `<IIIIIIIIIIII`, record size `48`.
- Added binary offsets/lengths for main text, Toaster summary, and RTX-overkill metadata per taxonomy entry.
- Extended verifier to read all three payload lanes byte-for-byte, check null terminators, validate 16-byte offsets, validate packed metadata, and verify tier payload FNV hash.
- Rebuilt Babel with `python Tools\BabelCompiler.py`.
- Added `Tools/Taxonomy/run_verify_sweep.py`.
- Added a compatible economy proof JSON derived from the real XENO million-step run for verifiers expecting `final_summary`.
- Installed `xxhash` into a temporary verification path outside the repository and passed it to `VerifyReplayHasherReference.py`.

Cinematic Cheats used:
- Tier payloads remain presentation metadata. Toaster lane is stripped text. RTX lane carries deterministic gradient/noise metadata for codex overlays. No physical simulation was added.
- Binary records buy future UI richness through direct offsets, not runtime object state.

Exact Microseconds saved:
- Measured runtime savings: `0 us/frame` claimed; no Unity runtime path changed.
- Future expected savings: direct binary lane lookup instead of parsing full JSON for low-tier codex rows. PENDING VERIFICATION until runtime loader exists.

Evidence:
- `python Tools\Taxonomy\compile_taxonomy.py` wrote `en_US_Taxonomy.h8bin` version `2`, bytes `27536`, record size `48`, aligned16 `True`.
- `python Tools\Taxonomy\verify_taxonomy.py Data\Localization\en_US_Taxonomy.json` passed after binary V2.
- `python Tools\Taxonomy\audit_taxonomy_data.py` returned `TAXONOMY DATA AUDIT PASS hashCollisions=0 binaryAligned16=True monteCarloSteps=1000220 atlasDomains=85`.
- `python Tools\BabelCompiler.py` rebuilt Babel: entries `32580`, languages `17`, bytes `1523984`, endian `<`, alignment `16`, collisions_resolved `0`.
- `python Tools\VerifyBabelDictionary.py` returned `BABEL VERIFIED`.
- `python Tools\VerifyHullStressBudget.py --economy-json Docs\AgentLogs\EconomyMonteCarlo_XENO_TAXONOMY_WRITER_Compatible.json --write-report Docs\AgentLogs\HullStressBudget_XENO_TAXONOMY_WRITER.json` returned `status=PASS`.
- `python Tools\Taxonomy\run_verify_sweep.py` returned `VERIFY SWEEP PASS passed=25/25`.

REGRESSION MODEL:
- CPU: offline Python only; no runtime CPU path changed.
- GC: no Unity hot-path code changed.
- Memory: binary grew from `10336` to `27536` bytes to carry explicit tier payload lanes. Still bounded and 16-byte aligned.
- Cadence: no Tick/Update/SlowTick cadence changed.
- Correctness: full verifier corpus now passes 25/25.

HOT PATH IMPACT:
- None by source scope. Runtime ingestion remains PENDING VERIFICATION.

FAILURE MODES:
- Runtime loader still does not consume `en_US_Taxonomy.h8bin`.
- If Babel sources change, Babel must be rebuilt before `VerifyBabelDictionary.py`.
- Temporary `xxhash` verification dependency lives outside the repo; if temp storage is wiped, reinstall before running replay verifier.

WHY KEPT/REJECTED:
- Kept binary V2 because it closes the JSON-only tier payload gap.
- Rejected vendoring `xxhash` into the repository.
- Rejected marking failed verifier outputs as unrelated because the user requested a full verification sweep.

## 2026-05-16 Deep Freeze Consistency Audit

What was wrong:
- Current disk memory still contained historical V1 wording that could be misread as current binary truth.
- Previous reports proved individual gates, but no single audit reopened JSON, binary, atlas, economy, verify-sweep, and log/status files together.

What was done:
- Corrected stale current-layout wording in status/rationale/log.
- Added `Tools/Taxonomy/deep_freeze_taxonomy_audit.py`.
- The deep-freeze audit checks minified JSON, exact counts, banned sterile/placeholder entry terms, taxonomy hash collisions, binary V2 header/CRC/payload bytes, 16-byte alignment, PROJECT_ATLAS 85-domain fit, H-Phi stateless flags, economy proof, verify sweep, and stale V1 wording.

Cinematic Cheats used:
- No runtime simulation was added. Toaster/RTX payload lanes remain deterministic codex presentation data, not physics.

Exact Microseconds saved:
- Runtime savings claimed: `0 us/frame`.
- Runtime cost added: `0 us/frame`.
- Potential future saving from binary lane lookup remains PENDING VERIFICATION until a runtime loader consumes it.

Evidence:
- `python -B -c "compile(...)"` returned `AST_COMPILE_PASS` for `deep_freeze_taxonomy_audit.py`.
- `python -B Tools\Taxonomy\deep_freeze_taxonomy_audit.py` returned `TAXONOMY DEEP FREEZE PASS binaryV=2 record=48 steps=1000220 sweep=25/25`.
- `Docs/AgentLogs/TaxonomyDeepFreezeAudit_XENO_TAXONOMY_WRITER.json` reports binary version `2`, record size `48`, file bytes `27536`, hPhi privateRuntimeStateAddedActual `false`, verify sweep `25/25`, economy steps `1000220`, graph cycles `0`.

REGRESSION MODEL:
- CPU: offline Python only.
- GC: no Unity runtime code touched.
- Memory: no runtime memory retained; report files only.
- Cadence: no game cadence changed.
- Correctness: direct artifact reopen now covers current binary, source JSON, and status/log consistency.

HOT PATH IMPACT:
- None by source scope. Runtime remains PENDING VERIFICATION.

FAILURE MODES:
- The deep-freeze audit will fail if future binary schema changes without updating verifier expectations.
- Runtime loader proof is still absent.

WHY KEPT/REJECTED:
- Kept a separate deep-freeze audit because repeated report chains are not enough; the binary must be reopened and compared directly.
- Rejected `py_compile` as syntax proof for this script after Windows returned a pycache rename denial; used no-bytecode AST compile instead.

## 2026-05-16 Hard-Science Manifest Trace

What was wrong:
- The taxonomy artifact correctly stated it owns no Beer-Lambert, Dalton, or Sabine LUTs, but the proof chain still depended on separate verifier outputs instead of being pulled into the deep-freeze audit.

What was done:
- Extended `Tools/Taxonomy/deep_freeze_taxonomy_audit.py` with `physicsTrace`.
- Reran Optics, Sabine, Dalton, and Data Inquisition verifiers.
- Deep-freeze now reopens physics manifests and rejects drift in formulas, endian contracts, alignment, hash collisions, and toaster/RTX tiers.

Cinematic Cheats used:
- Physics truth remains in owner LUTs/manifests. Taxonomy stays codex data. High-tier overkill is presentation metadata, not molecule simulation.

Exact Microseconds saved:
- Runtime savings claimed: `0 us/frame`.
- Runtime cost added: `0 us/frame`.

Evidence:
- `python Tools\VerifyOpticsBaker.py --report Docs\AgentLogs\OpticsVerification_XENO_TAXONOMY_WRITER.json` returned `OPTICS_LUT_VERIFIED`, `matrixBytes=393216`, `aligned16=True`, `byteOrder=little-endian`, `pack=<e`, `fnvCollisions=0`.
- `python Tools\DaltonGasToxicityBaker.py --verify` returned status `PASS`, bytes `128128`, header bytes `64`, row bytes `64`, row count `2001`, aligned16 `true`, fnvCollisionCount `0`.
- `python Tools\VerifySabineBaker.py` returned `SABINE_LUT_VERIFIED`, binary bytes `524288`, record `<ff`, SIMD group `<ffff`, fnvCollisions `0`, tiers `high,middle,rtx_overkill,toaster_i3`.
- `python Tools\VerifyDataInquisition.py --report Docs\AgentLogs\DataInquisition_XENO_TAXONOMY_WRITER.json` returned 38 binaries aligned16, 8 manifests endian `<`, 145 struct formats checked, MonteCarloSteps `1000000`, hashCollisions `0`, atlasDomains `85`.
- `python -B Tools\Taxonomy\deep_freeze_taxonomy_audit.py` returned `TAXONOMY DEEP FREEZE PASS binaryV=2 record=48 steps=1000220 sweep=25/25`.

REGRESSION MODEL:
- CPU: offline Python verification only.
- GC: no Unity hot-path code changed.
- Memory: no runtime allocations added.
- Cadence: no runtime cadence changed.
- Correctness: hard-science manifest trace now gates deep-freeze status.

HOT PATH IMPACT:
- None by source scope. Runtime remains PENDING VERIFICATION.

FAILURE MODES:
- Deep-freeze will now fail if optics/Dalton/Sabine manifests lose real-physics tokens, endian contracts, alignment, or scalability tiers.

WHY KEPT/REJECTED:
- Kept physics ownership outside taxonomy.
- Rejected duplicating physics constants in taxonomy.

## 2026-05-16 SHA Seal / Revalidation

What was wrong:
- The deep-freeze audit still trusted manifest SHA fields indirectly and did not seal critical artifacts into one current digest report.
- The first fresh full verifier sweep returned 24/25 because `VerifyVramBudgets.py` crashed on `binaryCache.tierSliceFormula`.

What was done:
- Extended `Tools/Taxonomy/deep_freeze_taxonomy_audit.py` with direct SHA-256 recomputation for Optics, Dalton, and Sabine binaries.
- Added `Docs/AgentLogs/TaxonomyArtifactSeal_XENO_TAXONOMY_WRITER.json` covering 18 critical taxonomy, physics, evidence, and taxonomy-tool artifacts.
- Added industrial/noir clinical density gating to the deep-freeze entry audit.
- Ran `python Tools\VerifyVramBudgets.py` standalone against current disk state; it passed.
- Reran `python Tools\Taxonomy\run_verify_sweep.py`; replacement sweep passed 25/25.
- Reran taxonomy verifier, taxonomy data audit, and deep-freeze after the sweep replacement.

Cinematic Cheats used:
- No runtime simulation was added. Toaster and RTX lanes remain sealed data lanes for presentation, not molecule-level truth.

Exact Microseconds saved:
- Runtime savings claimed: `0 us/frame`.
- Runtime cost added: `0 us/frame`.
- Future binary loader benefit remains PENDING VERIFICATION.

Evidence:
- `python -B -c "compile(...deep_freeze_taxonomy_audit.py...)"` returned `AST_COMPILE_PASS`.
- `python Tools\VerifyVramBudgets.py` returned `VFX_VRAM_BUDGETS_OK ... BINARY=Data/System/VFX_Budgets.h8bin MANIFEST=Data/System/VFX_Budgets.manifest.json`.
- `python Tools\Taxonomy\run_verify_sweep.py` returned `VERIFY SWEEP PASS passed=25/25`.
- `python -B Tools\Taxonomy\deep_freeze_taxonomy_audit.py` returned `TAXONOMY DEEP FREEZE PASS binaryV=2 record=48 steps=1000220 sweep=25/25`.
- `python Tools\Taxonomy\verify_taxonomy.py Data\Localization\en_US_Taxonomy.json` returned `TAXONOMY VERIFY PASS fauna=22 flora=10 madness=6 hashCollisions=0 binaryAligned16=yes status=TAXONOMY COMPILED polishStatus=VERIFIED MASTER GRADE`.
- `python Tools\Taxonomy\audit_taxonomy_data.py` returned `TAXONOMY DATA AUDIT PASS hashCollisions=0 binaryAligned16=True monteCarloSteps=1000220 atlasDomains=85`.

REGRESSION MODEL:
- CPU: offline hash and verification scripts only.
- GC: no Unity runtime code touched.
- Memory: no runtime memory retained; generated audit JSON only.
- Cadence: no runtime cadence changed.
- Correctness: direct file digest checks now fail on byte drift, and full sweep evidence is current 25/25.

HOT PATH IMPACT:
- None by source scope. Runtime remains PENDING VERIFICATION.

FAILURE MODES:
- Deep-freeze now fails if a manifest SHA drifts, a binary loses 16-byte alignment, an entry loses industrial/clinical density, or the verify sweep report is not 25/25.

WHY KEPT/REJECTED:
- Kept strict digest sealing because size/alignment without hash proof is incomplete.
- Rejected weakening `VerifyVramBudgets.py`; current disk data already satisfies the stricter contract.

## 2026-05-16 Source Struct Contract / Metric Phi Reclean

What was wrong:
- Output binary checks were strong, but taxonomy source code still lacked an AST-level gate proving every `struct.*` format remained literal and little-endian.
- A full XENO verifier sweep failed 24/25 because `VerifyMetricPhiDataTruth.py` consumed stale Metric Phi sweep evidence.

What was done:
- Added `sourceStructContract` to `Tools/Taxonomy/deep_freeze_taxonomy_audit.py`.
- The audit now parses all `Tools/Taxonomy/*.py`, resolves module string constants, checks all `struct.pack`, `struct.unpack`, `struct.calcsize`, and `struct.Struct` calls, rejects dynamic formats, enforces `<`, and checks taxonomy header/record constants remain 16-byte aligned.
- Corrected the auditor's own dynamic `struct.calcsize(fmt)` self-violation by using a fixed source-size map.
- Reran `VerifyMetricPhiDataTruth.py` standalone, reran full XENO `run_verify_sweep.py`, reran `VerifyDataInquisition.py`, reran deep-freeze after the inquisition report overwrite.

Cinematic Cheats used:
- No runtime simulation added. Taxonomy remains offline codex data; high-tier visual richness remains deterministic metadata.

Exact Microseconds saved:
- Runtime savings claimed: `0 us/frame`.
- Runtime cost added: `0 us/frame`.

Evidence:
- `python -B -c "compile(...deep_freeze_taxonomy_audit.py...)"` returned `AST_COMPILE_PASS`.
- First new deep-freeze run failed on `Tools/Taxonomy/deep_freeze_taxonomy_audit.py:351:dynamic_struct_format`; the self-violation was fixed.
- Final `python -B Tools\Taxonomy\deep_freeze_taxonomy_audit.py` returned `TAXONOMY DEEP FREEZE PASS binaryV=2 record=48 steps=1000220 sweep=25/25 structCalls=14`.
- `sourceStructContract`: 6 files scanned, 14 calls checked, 0 failures, header 64 bytes aligned16, record 48 bytes aligned16.
- `python Tools\VerifyMetricPhiDataTruth.py --json-output Docs\AgentLogs\MetricPhiDataTruth_XENO_TAXONOMY_WRITER.json --markdown-output Docs\AgentLogs\MetricPhiDataTruth_XENO_TAXONOMY_WRITER.md` returned `DATA_TRUTH_VERIFIED checks=36 failed=0 binary_files=42 unaligned=0 struct_format_sites=167 endian_failures=0`.
- `python Tools\Taxonomy\run_verify_sweep.py` returned `VERIFY SWEEP PASS passed=25/25`.
- `python Tools\VerifyDataInquisition.py --report Docs\AgentLogs\DataInquisition_XENO_TAXONOMY_WRITER.json` returned binaries `41`, aligned16 `true`, manifests `9`, endian `<`, structFormats `156`, hashCollisions `0`, atlasDomains `85`.

REGRESSION MODEL:
- CPU: offline AST/file verification only.
- GC: no Unity runtime code touched.
- Memory: report files only; no runtime retained memory.
- Cadence: no runtime cadence changed.
- Correctness: future taxonomy source edits now fail deep-freeze if they use dynamic or non-little-endian struct layouts.

HOT PATH IMPACT:
- None by source scope. Runtime remains PENDING VERIFICATION.

FAILURE MODES:
- Deep-freeze fails if a taxonomy tool introduces native-endian `@`, big-endian `>`, network `!`, implicit `=`, dynamic format variables, or non-16-byte header/record layouts.
- Full sweep can still fail if project-level Metric Phi evidence is stale; current run is clean.

WHY KEPT/REJECTED:
- Kept AST source gating because output-only binary proof is insufficient for future schema drift.
- Rejected whitelisting the auditor's dynamic `calcsize` call; the validator must obey the same rules it enforces.

## 2026-05-16 Binary Manifest Contract / Stable Evidence Reclean

What was wrong:
- `Data/Localization/en_US_Taxonomy.h8bin` had a verified V2 layout, but no standalone manifest for SHINOBU-style ingest.
- Sabine and Tide owner artifacts were stale against current verifier contracts.
- XENO's full sweep read the shared Metric Phi report path; concurrent agents kept overwriting it with unrelated failures.

What was done:
- Added `Data/Localization/en_US_Taxonomy.manifest.json`.
- Updated `Tools/Taxonomy/compile_taxonomy.py` to emit the manifest with SHA-256, CRC32, row formula, string-table offsets, little-endian struct fields, and runtime lookup contract.
- Updated `Tools/Taxonomy/verify_taxonomy.py` and `Tools/Taxonomy/deep_freeze_taxonomy_audit.py` to reopen and verify the manifest.
- Updated XENO `Tools/Taxonomy/run_verify_sweep.py` so Metric Phi data-truth reads explicit `Docs/Reports/METRIC_PHI_VERIFY_SWEEP_POST_MUTATION_FINAL.json`, not the shared mutable default.
- Reran Sabine/Tide owner bakers/verifiers, XENO sweep, data inquisition, taxonomy verifier, taxonomy data audit, and deep-freeze.

Cinematic Cheats used:
- No runtime simulation was added. Taxonomy remains an aligned stateless data payload; Toaster and RTX paths are deterministic presentation lanes.

Exact Microseconds saved:
- Runtime savings claimed: `0 us/frame`.
- Runtime cost added: `0 us/frame`.
- Future binary-loader parse savings remain PENDING VERIFICATION.

Evidence:
- Manifest probe: `H8.TAXONOMY.BINARY_MANIFEST.V2 TAXONOMY_BINARY_CACHE_LOCKED 27536 38 recordOffset=64+entryIndex*48 minified=True`.
- `python -B -c "ast.parse(...run_verify_sweep.py...)"` returned `AST_RUN_VERIFY_SWEEP_PASS`.
- `python -B Tools\VerifyMetricPhiDataTruth.py --sweep-input Docs\Reports\METRIC_PHI_VERIFY_SWEEP_POST_MUTATION_FINAL.json ...` returned `DATA_TRUTH_VERIFIED checks=37 failed=0 binary_files=43 unaligned=0 struct_format_sites=274 endian_failures=0`.
- `python -B Tools\Security\VerifyReplayHasherReference.py --xxhash-path C:\Users\User\AppData\Local\Temp\metric_phi_xxhash_ref --fuzz-count 256` returned `XXH3_REFERENCE_AND_SHUFFLE_FUZZ_OK xxh3=466 shuffle=256`.
- `python -B Tools\Taxonomy\run_verify_sweep.py` returned `VERIFY SWEEP PASS passed=25/25`.
- `python -B Tools\VerifyDataInquisition.py --report Docs\AgentLogs\DataInquisition_XENO_TAXONOMY_WRITER.json` returned `binaries=43 aligned16=true manifests=11 endian=< structFormats=273 monteCarloSteps=1000000 hashCollisions=0 atlasDomains=85`.
- `python -B Tools\Taxonomy\verify_taxonomy.py Data\Localization\en_US_Taxonomy.json` returned `TAXONOMY VERIFY PASS fauna=22 flora=10 madness=6 hashCollisions=0 binaryAligned16=yes status=TAXONOMY COMPILED polishStatus=VERIFIED MASTER GRADE`.
- `python -B Tools\Taxonomy\audit_taxonomy_data.py` returned `TAXONOMY DATA AUDIT PASS hashCollisions=0 binaryAligned16=True monteCarloSteps=1000220 atlasDomains=85`.
- `python -B Tools\Taxonomy\deep_freeze_taxonomy_audit.py` returned `TAXONOMY DEEP FREEZE PASS binaryV=2 record=48 steps=1000220 sweep=25/25 structCalls=14`.
- `Docs\AgentLogs\TaxonomyArtifactSeal_XENO_TAXONOMY_WRITER.json` now seals 19 artifacts.

REGRESSION MODEL:
- CPU: offline Python verification only.
- GC: no Unity runtime code touched.
- Memory: one manifest plus refreshed reports; no runtime retained memory.
- Cadence: no runtime cadence changed.
- Correctness: manifest readback now fails on SHA/CRC/layout/string-table drift; XENO sweep no longer depends on the shared mutable Metric Phi default report.

HOT PATH IMPACT:
- None by source scope. Runtime remains PENDING VERIFICATION.

FAILURE MODES:
- Deep-freeze fails if the manifest is missing, pretty-printed, not V2, not little-endian, not 16-byte aligned, has stale SHA/CRC, or disagrees with JSON/binary bytes.
- XENO sweep fails if the explicit Metric Phi evidence input is removed or if current H-Phi/data-truth checks fail.
- Shared Metric Phi report churn remains a project-level concurrency risk outside taxonomy ownership.

WHY KEPT/REJECTED:
- Kept a machine-readable binary manifest because binary loader contracts should not be implicit in compiler code.
- Kept explicit Metric Phi sweep input because broad shared reports are unstable under parallel agents.
- Rejected weakening any verifier or marking stale owner artifacts as unrelated.

## 2026-05-17 CRC Scope Contract / V2 Typo Purge

What was wrong:
- `payloadCrc32` was valid but its byte scope was implicit. A loader author could misread it without opening `compile_taxonomy.py`.
- `verify_taxonomy.py` still had a stale V1 diagnostic for a V2 header-flags failure.

What was done:
- Added `structFormats`, `crcScopes`, `crcByteRanges`, and `headerConstants` to `Data/Localization/en_US_Taxonomy.manifest.json`.
- Updated `Tools/Taxonomy/verify_taxonomy.py` and `Tools/Taxonomy/deep_freeze_taxonomy_audit.py` to hard-fail if those fields are missing or wrong.
- Corrected the stale V1 error text to V2.
- Regenerated taxonomy JSON, binary, and manifest.

Cinematic Cheats used:
- None added. Taxonomy remains static codex data with deterministic Toaster and RTX presentation lanes.

Exact Microseconds saved:
- Runtime savings claimed: `0 us/frame`.
- Runtime cost added: `0 us/frame`.
- Loader parse/CRC benefit remains PENDING VERIFICATION.

Evidence:
- `python -B -c "ast.parse(...compile_taxonomy.py/verify_taxonomy.py/deep_freeze_taxonomy_audit.py...)"` returned `AST_TAXONOMY_SCOPE_CONTRACT_PASS`.
- `python -B Tools\Taxonomy\compile_taxonomy.py` wrote `en_US_Taxonomy.h8bin bytes=27536 aligned16=True`.
- Manifest probe: `structFormats {'header':'<4sHHIIIIIIII24s','record':'<IIIIIIIIIIII'}` and `crcByteRanges {'payloadCrc32Start':64,'payloadCrc32End':27536}`.
- CRC probe: header payload CRC `0x41827838` equals the CRC32 of binary bytes `[64:27536]`; SHA-256 matched.
- `python -B Tools\Taxonomy\verify_taxonomy.py Data\Localization\en_US_Taxonomy.json` returned taxonomy PASS.
- `python -B Tools\Taxonomy\audit_taxonomy_data.py` returned taxonomy data audit PASS.
- `python -B Tools\Taxonomy\run_verify_sweep.py` returned `VERIFY SWEEP PASS passed=25/25`.
- `python -B Tools\VerifyDataInquisition.py --report Docs\AgentLogs\DataInquisition_XENO_TAXONOMY_WRITER.json` returned `binaries=44 aligned16=true manifests=11 endian=< structFormats=273 monteCarloSteps=1000000 hashCollisions=0 atlasDomains=85`.
- `python -B Tools\Taxonomy\deep_freeze_taxonomy_audit.py` returned `TAXONOMY DEEP FREEZE PASS binaryV=2 record=48 steps=1000220 sweep=25/25 structCalls=14` after the report overwrite.

REGRESSION MODEL:
- CPU: offline verification only.
- GC: no Unity runtime code touched.
- Memory: manifest fields add small static JSON bytes only.
- Cadence: no runtime cadence changed.
- Correctness: SHINOBU ingest can validate exact CRC byte ranges without source-code inference.

HOT PATH IMPACT:
- None by source scope. Runtime remains PENDING VERIFICATION.

FAILURE MODES:
- Verifier now fails if CRC scopes, byte ranges, struct format dictionary, or header constants drift.
- Deep-freeze still fails if artifact SHA/CRC/layout/string-table data disagrees with current binary bytes.

WHY KEPT/REJECTED:
- Kept explicit scope metadata because binary checksums without byte ranges are ambiguous.
- Rejected leaving stale V1 wording in a V2 binary verifier.
