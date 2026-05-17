# LOG_SNELL_LENS_REFRACTION_LUT

## 2026-05-16 - Snell Refraction LUT Bake

What was wrong: SHINOBU mask/porthole refraction would otherwise solve Snell vectors in the post shader. Initial bake used valid Snell math but exposed several calibration constants too directly for a hard-science data path.

What was done: Added `Tools/SnellBaker.py`, `Tools/test_snell_baker.py`, `Data/Visuals/Refraction_LUT_RGBA16F.bin`, `Data/Visuals/Refraction_LUT_RGBA16F_MINIMAL_128.bin`, `Data/Visuals/Refraction_LUT_RGBA16F_ULTRA_512.bin`, `Data/Visuals/Refraction_LUT_DistortionGrid.png`, `Data/Visuals/Refraction_LUT_RGBA16F.json`, and `Docs/Design/Snell_Refraction_LUT_Shader_Mapping.md`.

Cinematic Cheats used: baked Snell-law visual refraction into flat RGBA16F textures; RGB stores chromatic radial offsets, A stores shared tangential curvature. Runtime avoids per-pixel `asin/sin/tan` Snell solve. TIR is clamped offline and logged in manifest.

Exact Microseconds saved: measured proof absent. Static source estimate is that each sampled pixel avoids two-interface Snell solve, trigonometric calls, TIR branch, and chromatic dispersion math. Profiler/GCMonitor/Frame Debugger evidence is still `PENDING UNITY VERIFICATION`; no fake timing is claimed.

Binary facts:

- Base: `Data/Visuals/Refraction_LUT_RGBA16F.bin`, `524288` bytes, 16-byte aligned, `R16G16B16A16_SFloat`.
- Toaster: `Data/Visuals/Refraction_LUT_RGBA16F_MINIMAL_128.bin`, `131072` bytes, 16-byte aligned.
- Ultra: `Data/Visuals/Refraction_LUT_RGBA16F_ULTRA_512.bin`, `2097152` bytes, 16-byte aligned.
- Endian: little-endian half-float; probe matched `struct.pack("<e")`.
- FNV-1a: SNELL output filename collisions `0`; project hash verifier reported `1018` records and `0` collisions.

Math facts:

- Path: Water `1.33` -> Glass `1.5` -> Air `1.0`.
- Glass RGB IOR split derives from `(glassGreenIor - airIor) / (2 * glassAbbeNumber)`.
- Lens surface tilt derives from spherical-cap aperture/sagitta geometry.
- Exact perpendicular incidence row is half-float zero for all curvature rows and all channels.
- TIR boundary unit test passes around `asin(1.0 / 1.33)`.

Scalability:

- Low/toaster: 128 or 256 RGBA16F LUT, one sample, no harmonic extra data.
- Middle: base LUT plus smoother material-mask blending.
- High/Ultra: 512 LUT plus manifest `extraData` harmonic wet-glass fields for visual overkill.

Verification commands executed:

- `python -m py_compile Tools\SnellBaker.py Tools\test_snell_baker.py` -> PASS.
- `python Tools\SnellBaker.py` -> `SNELL_BAKER_STATUS: LENS BAKED`, `bytes=524288`.
- `python Tools\SnellBaker.py --verify` -> PASS.
- `python Tools\test_snell_baker.py` -> `5` tests PASS.
- broad `Data`/`Assets/_Project/Data` `.bin/.h8bin` alignment audit -> `38` total, `0` failures.
- `python Tools\VerifyH8HashCollisions.py` -> `HASH COLLISIONS: 0`.
- `python Tools\VerifyLore.py --check --verify-source --verify-manifest` -> PASS.
- economy import-only Monte Carlo -> `1328604` mined-node steps, `7000` successes, `0` failures.
- `Verify*.py` batch -> SNELL verifier PASS; `VerifyReplayHasherReference.py` requires `--xxhash-path` when run no-arg; explicit temp-`xxhash` run PASS; `VerifyLore.py` explicit check PASS; `VerifyBabelDictionary.py` repaired and PASS; `VerifyHullStressBudget.py` PASS.

Regression model:

- CPU: Python-only offline bake; no runtime code touched.
- GC: no Unity hot path touched; measured GC proof absent.
- Memory: base payload `0.5 MiB`, toaster payload `0.125 MiB`, ultra payload `2 MiB`.
- Cadence: no Tick/Update/FixedUpdate changes.
- Correctness: byte size, endian, alignment, finite values, exact zero row, TIR boundary, and direct texture format validated by Python/static evidence.

Status: `VERIFIED MASTER GRADE` for SNELL-owned Python/static data artifacts. Runtime remains `PENDING UNITY VERIFICATION`.

## 2026-05-16 - Escalation Re-Audit

What was wrong: second-pass audit found two real debts. First, `MAX_UV_OFFSET` still used an unexplained fractional attenuation instead of a physical aperture bound. Second, the broad verifier sweep exposed a deterministic rebuild mismatch in current-batch Babel dictionary artifacts after `BabelCompiler.py --validate-only` wrote manifest/constants before the binary.

What was done: changed SNELL max UV travel to the physical aperture projection `lensApertureRadius / viewportHalfWidth = 0.1`, regenerated all three SNELL binaries, added `Tools/VerifySnellRefractionLut.py`, hardened unit tests, purged stale tier labels from the manifest/doc, completed the Babel compiler run, and re-ran targeted data truth verifiers.

Cinematic Cheats used: still a baked Snell-law visual fake, now with aperture-bounded TIR saturation rather than arbitrary attenuation. Runtime remains one RGBA16F LUT sample; High/Ultra spend the saved ALU on blackwater glass grime, edge chromatic boost, micro-scratches, cracks, wet streaks, and local noir glare.

Exact Microseconds saved: measured proof still absent. Static estimate remains removal of per-pixel two-interface Snell solve, trig, chromatic dispersion, and TIR branch from the post path. Unity profiler/GCMonitor/Frame Debugger evidence is still `PENDING UNITY VERIFICATION`.

Second-pass evidence:

- XML re-read: attribute-safe extraction confirmed `SNELL_LENS_REFRACTION_LUT`, `DATA/MATH`, `15` tasks.
- `python Tools\SnellBaker.py` -> `SNELL_BAKER_STATUS: LENS BAKED`, `bytes=524288`, `maxAbsOffset=0.09997559`.
- `python Tools\SnellBaker.py --verify` -> PASS.
- `python Tools\test_snell_baker.py` -> `5` tests PASS.
- `python Tools\VerifySnellRefractionLut.py` -> PASS, `fnvCollisionCount=0`.
- SNELL binaries: `131072`, `524288`, `2097152` bytes; all 16-byte aligned; little-endian half probe PASS; exact perpendicular half-zero PASS.
- Stale marker scan over SNELL baker/doc/manifest -> no old clamp/tier-label markers.
- `python Tools\CraftingEconomyMonteCarlo.py --steps 1000000` -> `profit_steps=0`.
- Economy import simulation -> `1328604` mined-node steps, `7000` successes, `0` failures, dependency gaps `[]`.
- `python Tools\VerifyLore.py --check --verify-source --verify-manifest` -> PASS, alignment `16`, endian `<`.
- `python Tools\VerifyH8HashCollisions.py` -> `1018` records, `HASH COLLISIONS: 0`.
- `python Tools\VerifyBinaryHygiene.py` in broad sweep -> `binaryCount=39`, `misalignedCount=0`.
- `python Tools\VerifyDataInquisition.py` -> `atlasDomains=85`, `hashCollisions=0`, `endian=<`.
- `python Tools\VerifyMetricPhiDataTruth.py` -> `checks=33 failed=0`.
- `python Tools\Architecture\VerifyNetSyncMerkleProtocol.py` -> `DOMAIN_LABELS=85`, `BINARY_PAYLOADS_ALIGNED=39`.
- Isolated replay oracle -> `XXH3_REFERENCE_AND_SHUFFLE_FUZZ_OK xxh3=338 shuffle=128`; temp `xxhash` directory removed.
- Babel repair -> `VerifyBabel.py` and `VerifyBabelDictionary.py` PASS; Babel blob `1523984` bytes, alignment `16`, endian little/`<`, hash collisions `0`.

Regression model:

- CPU: SNELL remains offline Python bake plus one runtime texture sample contract; no Unity runtime code changed by SNELL.
- GC: no gameplay hot path touched by SNELL; measured runtime GC proof absent.
- Memory: Toaster `0.125 MiB`, base `0.5 MiB`, Ultra `2 MiB`; all flat binaries.
- Cadence: no Tick/Update/FixedUpdate changes.
- Correctness: aperture-derived clamp, Snell IOR path, exact perpendicular zero, TIR boundary, endian, alignment, FNV, atlas/H-Phi data-truth checks validated by CLI/static evidence.

Residual boundary: Unity import, SHINOBU material binding, profiler timing, GCMonitor, and visual capture remain `PENDING UNITY VERIFICATION`.

## 2026-05-16 - Metric Phi Sweep Correction

What was wrong: the corrected outer `Verify*.py` sweep exposed a stale Metric Phi evidence path. `RunMetricPhiVerifySweep.py` can record an optional no-arg replay verifier row, and `VerifyMetricPhiDataTruth.py` selected that first row instead of the successful external `xxhash` proof row. That produced a false `verify_replay_hasher_reference` failure after the sweep itself had no required failures.

What was done: patched `Tools/VerifyMetricPhiDataTruth.py` to select a `VerifyReplayHasherReference` row with `returnCode == 0`. Reran `RunMetricPhiVerifySweep.py --xxhash-path .codex_tmp\metric_phi_xxhash_ref`, deleted the temp dependency directory, reran `VerifyMetricPhiDataTruth.py`, then reran SNELL, lore, and crafting Monte Carlo checks.

Cinematic Cheats used: unchanged SNELL path. Runtime refraction remains baked aperture-bound Snell data, not shader-side physical optics.

Exact Microseconds saved: measured proof still absent. No Unity runtime code changed in this correction.

Verification:

- `METRIC_PHI_VERIFY_SWEEP_STATUS: VERIFY_SWEEP_PASS`, `commands=29`, `required_failures=0`.
- `METRIC_PHI_DATA_TRUTH_STATUS: DATA_TRUTH_VERIFIED`, `checks=36`, `failed=0`.
- Replay reference row in sweep: required `true`, return code `0`.
- `python Tools\VerifySnellRefractionLut.py` -> PASS.
- `python Tools\SnellBaker.py --verify` -> PASS.
- `python Tools\test_snell_baker.py` -> `5` tests PASS.
- `python Tools\VerifyLore.py --check --verify-source --verify-manifest` -> PASS.
- `python Tools\CraftingEconomyMonteCarlo.py --steps 1000000` -> `profit_steps=0`, `NO_INFINITE_RESOURCE_OR_ENERGY_LOOP`.

Regression model:

- CPU/GC/memory/cadence: no SNELL runtime code touched.
- Correctness: data-truth/H-Phi gate now reads the successful replay oracle instead of a non-required usage guard.
- Residual risk: Unity import, SHINOBU binding, profiler, GCMonitor, frame-time, and visual proof remain pending.
## 2026-05-16 - Post-Mutation Data Truth Inquisition

What was wrong:
- The broad data chain had to be rerun after current file mutations; stale reports are not evidence.
- The SNELL stale-marker verifier contained several audit terms literally in its own marker table, creating false-positive audit surface.
- Rationale prose still carried one neutral tier term that did not match the dirty industrial optics voice.

What was done:
- Split the marker literals in `Tools/VerifySnellRefractionLut.py` without weakening the scanner.
- Replaced stale prose terms in SNELL status/rationale files.
- Re-ran `python Tools\RunMetricPhiVerifySweep.py --xxhash-path .codex_tmp\metric_phi_xxhash_ref`: `VERIFY_SWEEP_PASS`, `commands=28`, `required_failures=0`.
- Removed `.codex_tmp\metric_phi_xxhash_ref` after validating the resolved path stayed inside the workspace.
- Re-ran `VerifyMetricPhiDataTruth.py`, `VerifyDataInquisition.py`, `VerifyBinaryHygiene.py`, `VerifyH8HashCollisions.py`, `VerifyNetSyncMerkleProtocol.py`, `CraftingEconomyMonteCarlo.py --steps 1000000`, and the SNELL verifier.

Cinematic Cheats used:
- Refraction remains a baked Snell-law presentation LUT, not per-pixel runtime optical simulation.
- Low tier pays one aligned RGBA16F sample; high tier spends the saved ALU on grime, chromatic edge response, and overkill glass treatment.

Exact Microseconds saved:
- `0` measured microseconds claimed in this pass; no Unity profiler capture was run.
- Static runtime path remains unchanged from the LUT design: shader-side Snell solve avoided by cold baked data. Profiler proof remains `PENDING UNITY VERIFICATION`.

Verification:
- `VerifySnellRefractionLut.py`: PASS, `bytes=524288`, `maxAbsOffset=0.09997559`, `zeroPerpendicular=True`, `fnvCollisionCount=0`.
- `VerifyMetricPhiDataTruth.py`: `DATA_TRUTH_VERIFIED`, `checks=36`, `failed=0`, `binary_files=39`, `unaligned=0`.
- `VerifyDataInquisition.py`: `binaries=38`, `aligned16=true`, `endian=<`, `monteCarloSteps=1000000`, `hashCollisions=0`, `atlasDomains=85`.
- `VerifyBinaryHygiene.py`: `binaryCount=39`, `misalignedCount=0`.
- `VerifyH8HashCollisions.py`: `1018` records, `HASH COLLISIONS: 0`.
- `VerifyNetSyncMerkleProtocol.py`: `DOMAIN_LABELS=85`, `BINARY_PAYLOADS_ALIGNED=39`.
- `CraftingEconomyMonteCarlo.py --steps 1000000`: `profit_steps=0`.
- Targeted SNELL stale-marker scan: PASS.

## 2026-05-16 - Spectral Dispersion Hardening

What was wrong:
- The chromatic split was physically motivated but still too close to hand-tuned art data: a repeating-third Abbe value plus symmetric RGB half-deltas.
- Shader mapping documentation still carried the old red/blue IOR values and half-delta formula.
- Regenerating the optical payload invalidated previous binary-hash evidence.

What was done:
- Replaced the chromatic split with Cauchy dispersion: Fraunhofer C/D/F wavelengths, prompt glass IOR fixed at the D line, Abbe `Vd=58.0`, and derived `cauchyA`/`cauchyBNm2`.
- Regenerated `Refraction_LUT_RGBA16F.bin`, `Refraction_LUT_RGBA16F_MINIMAL_128.bin`, `Refraction_LUT_RGBA16F_ULTRA_512.bin`, manifest, and preview.
- Updated `Tools/test_snell_baker.py` to assert spectral invariants and `Tools/VerifySnellRefractionLut.py` to reject manifest drift.
- Updated `Docs/Design/Snell_Refraction_LUT_Shader_Mapping.md`, status, rationale, and this log.

Cinematic Cheats used:
- Still a baked LUT, not runtime optics. The player sees a physically plausible glass response while MX350 pays one RGBA16F sample.
- High/Ultra still buy grime, edge color response, and harmonic wet-glass metadata with saved ALU.

Exact Microseconds saved:
- `0` measured microseconds claimed; Unity profiler was not run.
- Static runtime cost remains the same as the prior LUT path: one texture sample instead of per-pixel Snell/Cauchy math.

Verification:
- `python -m py_compile Tools\SnellBaker.py Tools\VerifySnellRefractionLut.py Tools\test_snell_baker.py`: PASS.
- `python Tools\SnellBaker.py`: `LENS BAKED`, `bytes=524288`, `zeroPerpendicular=True`, `maxAbsOffset=0.09997559`.
- `python Tools\SnellBaker.py --verify`: PASS.
- `python Tools\test_snell_baker.py`: 6 tests OK.
- `python Tools\VerifySnellRefractionLut.py`: PASS, `fnvCollisionCount=0`.
- `Docs/Reports/METRIC_PHI_VERIFY_SWEEP.json`: `VERIFY_SWEEP_PASS`, `totalCommands=33`, `requiredFailures=0`.
- `python Tools\VerifyMetricPhiDataTruth.py`: `checks=36`, `failed=0`, `binary_files=42`, `unaligned=0`, `endian_failures=0`.
- `python Tools\VerifyDataInquisition.py`: `binaries=41`, `aligned16=true`, `endian=<`, `monteCarloSteps=1000000`, `hashCollisions=0`, `atlasDomains=85`.
- `python Tools\VerifyBinaryHygiene.py --report Docs\Reports\SNELL_POST_MUTATION_BINARY_HYGIENE.json`: `binaryCount=42`, `misalignedCount=0`.
- `python Tools\VerifyH8HashCollisions.py`: `1018` records, `HASH COLLISIONS: 0`.
- `python Tools\Architecture\VerifyNetSyncMerkleProtocol.py`: `DOMAIN_LABELS=85`, `BINARY_PAYLOADS_ALIGNED=42`.
- `python Tools\CraftingEconomyMonteCarlo.py --steps 1000000`: `profit_steps=0`.

## 2026-05-16 - Overkill Extra Data Derivation

What was wrong:
- High/Ultra `extraData` still carried fixed presentation constants for harmonic noise, wet-glass scratch strength, and edge chromatic boost.
- One default Metric Phi sweep produced a console/report mismatch under concurrent report mutation.

What was done:
- Replaced high-tier harmonic frequency with `fraunhoferD_nm / sourceWavelengthNm * band`.
- Replaced harmonic amplitude with `maxUvOffset * spectralSpreadRatio * resolutionScale / band`.
- Replaced harmonic phase with `fract(glassIorAtWavelength * fraunhoferD_nm / sourceWavelengthNm)`.
- Replaced wet-glass scratch strength with `sqrt(fresnelR0GlassAir) * resolutionScale`.
- Replaced edge chromatic boost with `1 + spectralSpreadRatio * resolutionScale`.
- Added tests and verifier checks for derived extra data.
- Reran the broad sweep into SNELL-owned report files to avoid default-report race contamination.

Cinematic Cheats used:
- Refraction remains a deterministic offline LUT. Overkill metadata gives high-end glass more grime and harmonic structure without runtime per-pixel physical optics.

Exact Microseconds saved:
- `0` measured microseconds claimed. Unity profiler was not run.
- Static runtime contract remains unchanged: one RGBA16F lookup, no runtime Cauchy/Fresnel solve required.

Verification:
- `python Tools\SnellBaker.py`: `LENS BAKED`, `bytes=524288`, `zeroPerpendicular=True`.
- `python Tools\SnellBaker.py --verify`: PASS.
- `python Tools\test_snell_baker.py`: 7 tests OK.
- `python Tools\VerifySnellRefractionLut.py`: PASS, `fnvCollisionCount=0`.
- `python Tools\RunMetricPhiVerifySweep.py --xxhash-path .codex_tmp\metric_phi_xxhash_ref --json-output Docs\Reports\SNELL_METRIC_PHI_VERIFY_SWEEP.json --markdown-output Docs\Reports\SNELL_METRIC_PHI_VERIFY_SWEEP.md`: `VERIFY_SWEEP_PASS`, `commands=35`, `required_failures=0`.
- `python Tools\VerifyMetricPhiDataTruth.py --sweep-input Docs\Reports\SNELL_METRIC_PHI_VERIFY_SWEEP.json`: `checks=37`, `failed=0`, `binary_files=42`, `unaligned=0`, `endian_failures=0`.
- `python Tools\VerifyDataInquisition.py`: `binaries=41`, `aligned16=true`, `manifests=9`, `atlasDomains=85`.
- `python Tools\VerifyBinaryHygiene.py --report Docs\Reports\SNELL_POST_MUTATION_BINARY_HYGIENE.json`: `binaryCount=42`, `misalignedCount=0`.
- `python Tools\VerifyH8HashCollisions.py`: `1018` records, `HASH COLLISIONS: 0`.
- `python Tools\Architecture\VerifyNetSyncMerkleProtocol.py`: `DOMAIN_LABELS=85`, `BINARY_PAYLOADS_ALIGNED=42`.
- `python Tools\CraftingEconomyMonteCarlo.py --steps 1000000`: `profit_steps=0`, seed `3237998101`.
- `.codex_tmp\metric_phi_xxhash_ref`: removed after workspace-bound path validation.

## 2026-05-16 - Geometry Assumption Ledger

What was wrong:
- `effectiveSamplePlaneOffset` was physically equal to half the lens center thickness but was still encoded as an independent geometry value.
- The manifest named lens dimensions without a machine-readable evidence boundary explaining that production CAD was not supplied in this batch prompt.

What was done:
- Changed `EFFECTIVE_SAMPLE_PLANE_OFFSET_M` to derive from `LENS_CENTER_THICKNESS_M * 0.5`.
- Added `derivedConstants.effectiveSamplePlaneOffset` with formula and ratio.
- Added `assumptionLedger.opticalGeometry` with evidence class, CAD absence, units, source notes, formulas, and `runtimeAuthority=false`.
- Updated shader mapping documentation, SNELL unit tests, and `VerifySnellRefractionLut.py` so geometry disclosure is enforced by code.
- Regenerated the manifest and reran post-mutation data gates.

Cinematic Cheats used:
- Refraction remains a deterministic presentation LUT, not runtime physical optics.
- The ledger makes the authoring assumptions explicit while preserving the one-sample low-tier path and the high-tier grime metadata path.

Exact Microseconds saved:
- `0` measured microseconds claimed. Unity profiler was not run.
- Static runtime contract is unchanged: base payload remains `524288` bytes and is sampled as RGBA16F.

Verification:
- `python -m py_compile Tools\SnellBaker.py Tools\VerifySnellRefractionLut.py Tools\test_snell_baker.py`: PASS.
- `python Tools\SnellBaker.py`: `LENS BAKED`, `bytes=524288`, `zeroPerpendicular=True`, `maxAbsOffset=0.09997559`.
- `python Tools\SnellBaker.py --verify`: PASS.
- `python Tools\test_snell_baker.py`: 8 tests OK.
- `python Tools\VerifySnellRefractionLut.py`: PASS, `fnvCollisionCount=0`.
- `python Tools\RunMetricPhiVerifySweep.py --xxhash-path .codex_tmp\metric_phi_xxhash_ref --json-output Docs\Reports\SNELL_METRIC_PHI_VERIFY_SWEEP.json --markdown-output Docs\Reports\SNELL_METRIC_PHI_VERIFY_SWEEP.md`: `VERIFY_SWEEP_PASS`, `commands=35`, `required_failures=0`.
- `python Tools\VerifyMetricPhiDataTruth.py --sweep-input Docs\Reports\SNELL_METRIC_PHI_VERIFY_SWEEP.json`: `checks=37`, `failed=0`, `binary_files=42`, `unaligned=0`, `endian_failures=0`.
- `python Tools\VerifyDataInquisition.py`: `binaries=41`, `aligned16=true`, `manifests=9`, `atlasDomains=85`.
- `python Tools\VerifyBinaryHygiene.py --report Docs\Reports\SNELL_POST_MUTATION_BINARY_HYGIENE.json`: `binaryCount=42`, `misalignedCount=0`.
- `python Tools\VerifyH8HashCollisions.py`: `1018` records, `HASH COLLISIONS: 0`.
- `python Tools\Architecture\VerifyNetSyncMerkleProtocol.py`: `DOMAIN_LABELS=85`, `BINARY_PAYLOADS_ALIGNED=42`.
- `python Tools\CraftingEconomyMonteCarlo.py --steps 1000000`: `steps=1000000`, `profit_steps=0`, seed `3237998101`.
- `.codex_tmp\metric_phi_xxhash_ref`: removed after workspace-bound path validation.

## 2026-05-16 - SHINOBU Texture Ingestion Contract

What was wrong:
- The manifest had exact payload sizes and texture shape, but direct SHINOBU ingestion still relied on loader assumptions for row stride, byte offset, mip use, linear/sRGB state, and sampler setup.
- The docs named tier payload sizes but did not make row stride a machine-checked field.

What was done:
- Added `textureIngestion` contract generation in `Tools/SnellBaker.py`.
- Base contract: `byteOffset=0`, `bytesPerTexel=8`, `rowStrideBytes=2048`, `payloadBytes=524288`, no mips, linear data, clamp/bilinear sampler intent, cold-load upload phase.
- Tier row strides are now explicit and verified: toaster `1024`, base/low/middle `2048`, high/ultra `4096`.
- Added verifier checks for every tier contract in `Tools/VerifySnellRefractionLut.py`.
- Added test coverage in `Tools/test_snell_baker.py`; suite now has 11 tests.
- Updated `Docs/Design/Snell_Refraction_LUT_Shader_Mapping.md` with row stride and byte offset data.

Cinematic Cheats used:
- No runtime loader or shader simulation was added. The visual refraction remains a flat offline LUT.
- The saved runtime work still buys high-tier grime/harmonic glass metadata instead of per-pixel optics.

Exact Microseconds saved:
- `0` measured microseconds claimed. Unity profiler was not run.
- Runtime contract remains one stateless RGBA16F lookup; base payload remains `524288` bytes.

Verification:
- `python -m py_compile Tools\SnellBaker.py Tools\VerifySnellRefractionLut.py Tools\test_snell_baker.py`: PASS.
- `python Tools\SnellBaker.py`: `LENS BAKED`, `bytes=524288`, `zeroPerpendicular=True`, `maxAbsOffset=0.09997559`.
- `python Tools\SnellBaker.py --verify`: PASS.
- `python Tools\test_snell_baker.py`: 11 tests OK.
- `python Tools\VerifySnellRefractionLut.py`: PASS, `fnvCollisionCount=0`.
- `textureIngestion`: base `rowStrideBytes=2048`, `payloadBytes=524288`, `byteOffset=0`, `mipmaps=false`, `sRgb=false`, `linearData=true`.
- `python Tools\RunMetricPhiVerifySweep.py --xxhash-path .codex_tmp\metric_phi_xxhash_ref --json-output Docs\Reports\SNELL_METRIC_PHI_VERIFY_SWEEP.json --markdown-output Docs\Reports\SNELL_METRIC_PHI_VERIFY_SWEEP.md`: `VERIFY_SWEEP_PASS`, `commands=35`, `required_failures=0`.
- `python Tools\VerifyMetricPhiDataTruth.py --sweep-input Docs\Reports\SNELL_METRIC_PHI_VERIFY_SWEEP.json`: `checks=37`, `failed=0`, `struct_format_sites=171`, `endian_failures=0`, `binary_files=42`, `unaligned=0`.
- `python Tools\VerifyDataInquisition.py`: `binaries=42`, `aligned16=true`, `manifests=11`, `atlasDomains=85`.
- `python Tools\VerifyBinaryHygiene.py --report Docs\Reports\SNELL_POST_MUTATION_BINARY_HYGIENE.json`: `binaryCount=42`, `misalignedCount=0`.
- `python Tools\VerifyH8HashCollisions.py`: `1018` records, `HASH COLLISIONS: 0`.
- `python Tools\Architecture\VerifyNetSyncMerkleProtocol.py`: `DOMAIN_LABELS=85`, `BINARY_PAYLOADS_ALIGNED=42`.
- `python Tools\CraftingEconomyMonteCarlo.py --steps 1000000`: `steps=1000000`, `profit_steps=0`, seed `3237998101`.
- `.codex_tmp\metric_phi_xxhash_ref`: removed after workspace-bound path validation.

## 2026-05-16 - Local Metadata FNV Audit

What was wrong:
- SNELL had output filename FNV proof and the project-wide hash scan, but local manifest IDs were not collision-audited as their own SHINOBU lookup set.
- The first new audit run exposed a duplicate string reference to `Refraction_LUT_RGBA16F.bin`, which was a duplicate ID path, not two different strings with the same hash.

What was done:
- Added `collect_metadata_ids()` and `build_fnv_collision_audit()` to `Tools/SnellBaker.py`.
- Added `metadataIdHashAudit` to `Data/Visuals/Refraction_LUT_RGBA16F.json`.
- Included profile IDs, channel keys, axis names, payload filenames, law names, geometry IDs, schema, texture format, and evidence class.
- De-duplicated identical ID strings before collision testing so only distinct-string hash collisions fail.
- Updated `Tools/VerifySnellRefractionLut.py` to regenerate and compare the metadata audit.
- Updated `Tools/test_snell_baker.py`; suite now has 10 tests.

Cinematic Cheats used:
- No runtime optics were added. This is offline hash hygiene for a stateless refraction data lookup.
- High/Ultra visual metadata remains derived and optional; low-tier payload stays compact.

Exact Microseconds saved:
- `0` measured microseconds claimed. Unity profiler was not run.
- Runtime contract remains unchanged: base payload `524288` bytes, headerless little-endian RGBA16F.

Verification:
- First local test run failed correctly: duplicate `Refraction_LUT_RGBA16F.bin` field references were treated as a false collision by the initial audit.
- Final `python -m py_compile Tools\SnellBaker.py Tools\VerifySnellRefractionLut.py Tools\test_snell_baker.py`: PASS.
- Final `python Tools\SnellBaker.py`: `LENS BAKED`, `bytes=524288`, `zeroPerpendicular=True`, `maxAbsOffset=0.09997559`.
- Final `python Tools\SnellBaker.py --verify`: PASS.
- Final `python Tools\test_snell_baker.py`: 10 tests OK.
- Final `python Tools\VerifySnellRefractionLut.py`: PASS, `fnvCollisionCount=0`.
- `metadataIdHashAudit`: `count=32`, `uniqueHashCount=32`, `collisionCount=0`.
- `python Tools\RunMetricPhiVerifySweep.py --xxhash-path .codex_tmp\metric_phi_xxhash_ref --json-output Docs\Reports\SNELL_METRIC_PHI_VERIFY_SWEEP.json --markdown-output Docs\Reports\SNELL_METRIC_PHI_VERIFY_SWEEP.md`: `VERIFY_SWEEP_PASS`, `commands=35`, `required_failures=0`.
- `python Tools\VerifyMetricPhiDataTruth.py --sweep-input Docs\Reports\SNELL_METRIC_PHI_VERIFY_SWEEP.json`: `checks=37`, `failed=0`, `struct_format_sites=167`, `endian_failures=0`, `binary_files=42`, `unaligned=0`.
- `python Tools\VerifyDataInquisition.py`: `binaries=41`, `aligned16=true`, `manifests=9`, `atlasDomains=85`.
- `python Tools\VerifyBinaryHygiene.py --report Docs\Reports\SNELL_POST_MUTATION_BINARY_HYGIENE.json`: `binaryCount=42`, `misalignedCount=0`.
- `python Tools\VerifyH8HashCollisions.py`: `1018` records, `HASH COLLISIONS: 0`.
- `python Tools\Architecture\VerifyNetSyncMerkleProtocol.py`: `DOMAIN_LABELS=85`, `BINARY_PAYLOADS_ALIGNED=42`.
- `python Tools\CraftingEconomyMonteCarlo.py --steps 1000000`: `steps=1000000`, `profit_steps=0`, seed `3237998101`.
- `.codex_tmp\metric_phi_xxhash_ref`: removed after workspace-bound path validation.

## 2026-05-16 - Physics Law Ledger

What was wrong:
- The manifest proved Snell/Cauchy/Fresnel through separate fields, but it did not have a single hard-science law ledger for the data-truth audit.
- Beer-Lambert, Dalton, and Sabine were not explicitly marked as non-applicable to this UV-offset payload.

What was done:
- Added `physicsLawLedger` to `Data/Visuals/Refraction_LUT_RGBA16F.json`.
- Primary laws now recorded: Snell refraction, Cauchy optical dispersion, Fresnel normal-incidence reflectance, and spherical-cap geometry.
- Non-applicable laws now recorded with reasons: Beer-Lambert attenuation, Dalton partial pressure, and Sabine reverberation.
- Added test and verifier checks so the ledger cannot be removed silently.

Cinematic Cheats used:
- Refraction remains a baked presentation fake backed by optical laws.
- No runtime physical optics, gas simulation, or acoustic model was added to the visual LUT.

Exact Microseconds saved:
- `0` measured microseconds claimed. Unity profiler was not run.
- Runtime contract remains unchanged: one stateless RGBA16F lookup for the base refraction data.

Verification:
- `python -m py_compile Tools\SnellBaker.py Tools\VerifySnellRefractionLut.py Tools\test_snell_baker.py`: PASS.
- `python Tools\SnellBaker.py`: `LENS BAKED`, `bytes=524288`, `zeroPerpendicular=True`, `maxAbsOffset=0.09997559`.
- `python Tools\SnellBaker.py --verify`: PASS.
- `python Tools\test_snell_baker.py`: 9 tests OK.
- `python Tools\VerifySnellRefractionLut.py`: PASS, `fnvCollisionCount=0`.
- `python Tools\RunMetricPhiVerifySweep.py --xxhash-path .codex_tmp\metric_phi_xxhash_ref --json-output Docs\Reports\SNELL_METRIC_PHI_VERIFY_SWEEP.json --markdown-output Docs\Reports\SNELL_METRIC_PHI_VERIFY_SWEEP.md`: `VERIFY_SWEEP_PASS`, `commands=35`, `required_failures=0`.
- `python Tools\VerifyMetricPhiDataTruth.py --sweep-input Docs\Reports\SNELL_METRIC_PHI_VERIFY_SWEEP.json`: `checks=37`, `failed=0`, `struct_format_sites=167`, `endian_failures=0`, `binary_files=42`, `unaligned=0`.
- `python Tools\VerifyDataInquisition.py`: `binaries=41`, `aligned16=true`, `manifests=9`, `atlasDomains=85`.
- `python Tools\VerifyBinaryHygiene.py --report Docs\Reports\SNELL_POST_MUTATION_BINARY_HYGIENE.json`: `binaryCount=42`, `misalignedCount=0`.
- `python Tools\VerifyH8HashCollisions.py`: `1018` records, `HASH COLLISIONS: 0`.
- `python Tools\Architecture\VerifyNetSyncMerkleProtocol.py`: `DOMAIN_LABELS=85`, `BINARY_PAYLOADS_ALIGNED=42`.
- `python Tools\CraftingEconomyMonteCarlo.py --steps 1000000`: `steps=1000000`, `profit_steps=0`, seed `3237998101`.
- `.codex_tmp\metric_phi_xxhash_ref`: removed after workspace-bound path validation.

## 2026-05-16 - Static Endian Contract Audit

What was wrong:
- SNELL proved the emitted payload by byte probe, but the verifier did not statically audit every SNELL-owned Python binary format site.
- A future `struct.pack`/`calcsize` edit could have drifted to native-endian or big-endian without failing the SNELL-owned verifier.

What was done:
- Added AST-based source auditing to `Tools/VerifySnellRefractionLut.py`.
- The verifier now scans `Tools/SnellBaker.py`, `Tools/VerifySnellRefractionLut.py`, and `Tools/test_snell_baker.py`.
- Every audited `struct.pack`, `struct.unpack`, `struct.pack_into`, `struct.unpack_from`, and `struct.calcsize` format must resolve to a literal or constant beginning with `<`.
- Half-float `np.dtype` calls are checked for little-endian `<f2`.
- Added unit coverage so the SNELL test suite runs the same binary-contract audit.

Cinematic Cheats used:
- No runtime optics were added. Refraction remains a deterministic little-endian RGBA16F data lookup.
- The cheap path stays cheap; the verifier is cold offline tooling.

Exact Microseconds saved:
- `0` measured microseconds claimed. Unity profiler was not run.
- Runtime contract is unchanged: base payload remains `524288` bytes and sampled as a texture.

Verification:
- `python -m py_compile Tools\SnellBaker.py Tools\VerifySnellRefractionLut.py Tools\test_snell_baker.py`: PASS.
- `python Tools\SnellBaker.py --verify`: `LENS BAKED`, `bytes=524288`, `zeroPerpendicular=True`, `maxAbsOffset=0.09997559`.
- `python Tools\test_snell_baker.py`: 9 tests OK.
- `python Tools\VerifySnellRefractionLut.py`: PASS, `fnvCollisionCount=0`.
- `python Tools\RunMetricPhiVerifySweep.py --xxhash-path .codex_tmp\metric_phi_xxhash_ref --json-output Docs\Reports\SNELL_METRIC_PHI_VERIFY_SWEEP.json --markdown-output Docs\Reports\SNELL_METRIC_PHI_VERIFY_SWEEP.md`: `VERIFY_SWEEP_PASS`, `commands=35`, `required_failures=0`.
- `python Tools\VerifyMetricPhiDataTruth.py --sweep-input Docs\Reports\SNELL_METRIC_PHI_VERIFY_SWEEP.json`: `checks=37`, `failed=0`, `struct_format_sites=167`, `endian_failures=0`, `binary_files=42`, `unaligned=0`.
- `python Tools\VerifyDataInquisition.py`: `binaries=41`, `aligned16=true`, `manifests=9`, `atlasDomains=85`.
- `python Tools\VerifyBinaryHygiene.py --report Docs\Reports\SNELL_POST_MUTATION_BINARY_HYGIENE.json`: `binaryCount=42`, `misalignedCount=0`.
- `python Tools\VerifyH8HashCollisions.py`: `1018` records, `HASH COLLISIONS: 0`.
- `python Tools\Architecture\VerifyNetSyncMerkleProtocol.py`: `DOMAIN_LABELS=85`, `BINARY_PAYLOADS_ALIGNED=42`.
- `python Tools\CraftingEconomyMonteCarlo.py --steps 1000000`: `steps=1000000`, `profit_steps=0`, seed `3237998101`.
- `.codex_tmp\metric_phi_xxhash_ref`: removed after workspace-bound path validation.

## 2026-05-17 - FP16 Quantization and Layout Sentinels

What was wrong:
- The LUT was half-float, aligned, and little-endian, but there was no explicit proof of float32-to-FP16 quantization loss.
- Row stride and byte count did not prove exact row-major channel addressing for SHINOBU ingestion.
- A full Metric Phi sweep exposed a verifier-ordering failure: `VerifyMetricPhiDataTruth` validated a stale self-check payload after late transient recoveries.

What was done:
- Added `half_quantization_error_bound()` and `validation.halfQuantization` to `Tools/SnellBaker.py` and `Data/Visuals/Refraction_LUT_RGBA16F.json`.
- Added `binaryLayoutSentinels` to the base, toaster, low/middle, and high/ultra binary records.
- Expanded `Tools/VerifySnellRefractionLut.py` to validate SHA-256, half quantization, sentinels, endian contracts, ingestion contracts, law ledger, FNV audits, and tier metadata.
- Expanded `Tools/test_snell_baker.py` to `12` tests, including raw half-byte sentinel checks.
- Updated `Docs/Design/Snell_Refraction_LUT_Shader_Mapping.md` with the quantization and sentinel ingestion contract.
- Patched `Tools/RunMetricPhiVerifySweep.py` so late non-self transient recoveries refresh and rerun the self-check.

Cinematic Cheats used:
- Refraction remains a static RGBA16F visual fake. No runtime Snell/Cauchy/Fresnel solve was added.
- Low tier still pays one LUT sample; high/ultra keep the derived visual-overkill metadata.

Exact Microseconds saved:
- `0` measured runtime microseconds claimed. Unity profiler was not run.
- Expected runtime path remains unchanged: cold-loaded stateless texture lookup instead of shader-side trigonometric refraction.

Verification:
- `python -m py_compile Tools\RunMetricPhiVerifySweep.py Tools\SnellBaker.py Tools\VerifySnellRefractionLut.py Tools\test_snell_baker.py`: PASS.
- `python Tools\test_snell_baker.py`: 12 tests OK.
- `python Tools\SnellBaker.py`: `LENS BAKED`, `bytes=524288`, `zeroPerpendicular=True`, `maxAbsOffset=0.09997559`.
- `python Tools\SnellBaker.py --verify`: PASS, `bytes=524288`.
- `python Tools\VerifySnellRefractionLut.py`: PASS, `fnvCollisionCount=0`.
- Manifest quantization: `maxAbsoluteError=3.0517578125e-05`, `maxRoundToNearestError=3.0517578125e-05`, `maxErrorWithinBound=True`.
- Manifest layout sentinels: base sentinel count `5`, midpoint green half hex `7621`, row stride `2048`, payload `524288`.
- `python Tools\RunMetricPhiVerifySweep.py --xxhash-path .codex_tmp\metric_phi_xxhash_ref --json-output Docs\Reports\SNELL_METRIC_PHI_VERIFY_SWEEP.json --markdown-output Docs\Reports\SNELL_METRIC_PHI_VERIFY_SWEEP.md`: `VERIFY_SWEEP_PASS`, `commands=35`, `required_failures=0`.
- `python Tools\VerifyMetricPhiDataTruth.py --sweep-input Docs\Reports\SNELL_METRIC_PHI_VERIFY_SWEEP.json`: `checks=37`, `failed=0`, `binary_files=43`, `unaligned=0`, `struct_format_sites=274`, `endian_failures=0`.
- `python Tools\VerifyDataInquisition.py`: `binaries=43`, `aligned16=true`, `manifests=11`, `atlasDomains=85`.
- `python Tools\VerifyBinaryHygiene.py --report Docs\Reports\SNELL_POST_MUTATION_BINARY_HYGIENE.json`: `binaryCount=43`, `misalignedCount=0`.
- `python Tools\VerifyH8HashCollisions.py`: `1018` records, `HASH COLLISIONS: 0`.
- `python Tools\Architecture\VerifyNetSyncMerkleProtocol.py`: `DOMAIN_LABELS=85`, `BINARY_PAYLOADS_ALIGNED=43`.
- `python Tools\CraftingEconomyMonteCarlo.py --steps 1000000`: `steps=1000000`, `profit_steps=0`, seed `3237998101`.
- `.codex_tmp\metric_phi_xxhash_ref`: removed after workspace-bound path validation.

## 2026-05-17 - Texel-Center Sampling Contract

What was wrong:
- The SHINOBU HLSL mapping used raw `float2(view01, curvature01)` for `SAMPLE_TEXTURE2D`.
- That was not a hard data contract for bilinear endpoint sampling; it relied on sampler edge behavior instead of mapping normalized inputs to baked texel centers.
- The first post-patch verifier run was launched in parallel with a bake and read the manifest during rewrite.

What was done:
- Added `sampleCoordinateContract` to `Data/Visuals/Refraction_LUT_RGBA16F.json`.
- Added `build_sample_coordinate_contract()` in `Tools/SnellBaker.py`.
- Added verifier checks for base/toaster/low/middle/high/ultra sample coordinate contracts in `Tools/VerifySnellRefractionLut.py`.
- Added unit coverage in `Tools/test_snell_baker.py`; suite now has `13` tests.
- Updated `Docs/Design/Snell_Refraction_LUT_Shader_Mapping.md` to use `_H8SnellRefractionLut_TexelSize` and formula `sampleUv = ((axis01 * (axisCount - 1)) + 0.5) / axisCount`.
- Regenerated the binaries and manifest, then reran verification serially.

Cinematic Cheats used:
- Refraction remains a static RGBA16F visual fake. No shader-side Snell solve was added.
- The fix protects visual believability at endpoints across toaster/base/ultra tier payloads.

Exact Microseconds saved:
- `0` measured runtime microseconds claimed. Unity profiler was not run.
- The shader adds a tiny dimension-driven remap and avoids wrong-edge sampling; measured frame-time proof remains `PENDING UNITY VERIFICATION`.

Verification:
- `python -m py_compile Tools\SnellBaker.py Tools\VerifySnellRefractionLut.py Tools\test_snell_baker.py Tools\RunMetricPhiVerifySweep.py`: PASS.
- `python Tools\test_snell_baker.py`: 13 tests OK.
- `python Tools\SnellBaker.py`: `LENS BAKED`, `bytes=524288`, `zeroPerpendicular=True`, `maxAbsOffset=0.09997559`.
- `python Tools\SnellBaker.py --verify`: PASS, `bytes=524288`.
- `python Tools\VerifySnellRefractionLut.py`: PASS, `fnvCollisionCount=0`.
- Sample contract base: `viewScale=0.99609375`, `viewBias=0.001953125`, `last=0.998046875`.
- Sample contract tiers: toaster bias `0.00390625`, ultra bias `0.0009765625`.
- `python Tools\RunMetricPhiVerifySweep.py --xxhash-path .codex_tmp\metric_phi_xxhash_ref --json-output Docs\Reports\SNELL_METRIC_PHI_VERIFY_SWEEP.json --markdown-output Docs\Reports\SNELL_METRIC_PHI_VERIFY_SWEEP.md`: `VERIFY_SWEEP_PASS`, `commands=35`, `required_failures=0`.
- `python Tools\VerifyMetricPhiDataTruth.py --sweep-input Docs\Reports\SNELL_METRIC_PHI_VERIFY_SWEEP.json`: `checks=37`, `failed=0`, `binary_files=44`, `unaligned=0`, `struct_format_sites=274`, `endian_failures=0`.
- `python Tools\VerifyDataInquisition.py`: `binaries=44`, `aligned16=true`, `manifests=11`, `atlasDomains=85`.
- `python Tools\VerifyBinaryHygiene.py --report Docs\Reports\SNELL_POST_MUTATION_BINARY_HYGIENE.json`: `binaryCount=44`, `misalignedCount=0`.
- `python Tools\VerifyH8HashCollisions.py`: `1018` records, `HASH COLLISIONS: 0`.
- `python Tools\Architecture\VerifyNetSyncMerkleProtocol.py`: `DOMAIN_LABELS=85`, `BINARY_PAYLOADS_ALIGNED=44`.
- `python Tools\CraftingEconomyMonteCarlo.py --steps 1000000`: `steps=1000000`, `profit_steps=0`, seed `3237998101`.
- `.codex_tmp\metric_phi_xxhash_ref`: removed after workspace-bound path validation; unrelated `%TEMP%\metric_phi_xxhash_ref` process from another agent was not touched.
