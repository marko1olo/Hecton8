# Rationale_TIDE_FOURIER_BAKER

Status: OFFLINE VERIFIED MASTER GRADE / RUNTIME PENDING

## Decision 1 - Offline Data Authority

Problem: Runtime tide formulas already exist in `HectonSeismicTideDirector`, but the prompt requires an auxiliary Python/data baker and output under `Data/Environment/`.
Solution: Keep Unity source untouched and emit a lightweight raw little-endian float table, metadata JSON, PNG, and SHINOBU mapping doc from an offline Python baker.
Rejected Alternatives: Editing `HectonFluidEngine` or `HectonSeismicTideDirector` would cross the assigned DATA/MATH boundary and add integration risk during a concurrent batch.
Scalability potential: Low uses one cold-loaded 2,400-byte table and linear interpolation. Middle uses the required 9,600-byte table. High/Ultra can layer shader/audio/foam response from the 38,400-byte 15-minute table without changing authority.
Hardware Impact: MX350/i3 runtime impact is expected to be one table lookup plus one lerp if integrated; measured profiler proof absent.

## Decision 2 - Harmonic King Tide Alignment

Problem: Exact Day 14 and Day 42 flood peaks are design-authored beats, not something a pure physical tide solver can guarantee without repeated alignments.
Solution: Use five phase-locked harmonic constituents derived from relative tide potential `M / r^3` and first-order eccentricity modulation `3e`. The fixed seed supplies deterministic whole-cycle phase offsets, while the Day 14 syzygy anchor forces Day 14 and Day 42 to be exact local maxima.
Rejected Alternatives: The first-pass raised-cosine pulse was rejected because it proved a data event, not harmonic alignment. Runtime orbital simulation, per-water-cell simulation, and hidden force fields were also rejected because the player needs believable water level and warning logic, not physical celestial truth.
Scalability potential: Low tier samples the 4-hour table. Middle samples the 1-hour table. High and Ultra can spend saved CPU on denser foam, caustics, audio pressure, flood warning sirens, and bilge/pump-room VFX from the 15-minute table.
Hardware Impact: Runtime math is shifted out of frame time; estimated saving versus a five-harmonic per-frame solve is sub-microsecond per consumer, static estimate only.

## Decision 3 - Explicit Endian Packing

Problem: A binary tide table without explicit byte order can silently fail on non-standard tooling or future platform bake hosts.
Solution: Use `struct.Struct("<f").pack_into` for every sample and test the byte sequence against `struct.pack("<f", value)`.
Rejected Alternatives: `numpy.ndarray.tofile()` and native-endian `astype(np.float32)` writes were rejected because they hide the `<f` contract the prompt explicitly requires.
Scalability potential: The payload stays 9,600 bytes and can be cold-loaded into a fixed native buffer on all tiers.
Hardware Impact: Runtime avoids parsing and endian branching; MX350/i3 impact is limited to cold-load bandwidth. Measured runtime proof absent.

## Decision 4 - Tiered Binary Payloads

Problem: A single 1-hour table satisfies the original byte-count contract but does not answer the toaster/RTX scalability pillar.
Solution: Keep `Tide_Harmonics.bin` as the required 2400-float / 9600-byte main contract and add `Tide_Harmonics_Low.bin` at 600 floats / 2400 bytes plus `Tide_Harmonics_Ultra.bin` at 9600 floats / 38400 bytes. All three are raw `<f` and 16-byte aligned.
Rejected Alternatives: Runtime decimation and runtime upsampling were rejected because they add branches and private state to consumers.
Scalability potential: Low uses cheap 4-hour cadence for i3/Celeron. Middle uses the required 1-hour table. High/Ultra can drive foam, caustic gradients, flood siren timing, and bilge-alarm polish from 15-minute data.
Hardware Impact: Low-tier payload is 2400 bytes. Main payload is 9600 bytes. Ultra payload is 38400 bytes. Runtime proof remains pending until Unity integration/profiler capture.

## Decision 5 - Hash, Atlas, and Cross-Data Audit

Problem: SHINOBU ingest needs collision-free IDs and the user demanded proof across hash, economy, lore, Sabine, optics, and Dalton data surfaces.
Solution: Added local FNV audit to tide metadata and ran the existing project hash verifier. Also ran direct million-step economy Monte Carlo, lore verification, Sabine verifier, Beer-Lambert optics verifier, and Dalton verifier.
Rejected Alternatives: Treating these as outside-domain and ignoring them was rejected because the current user directive explicitly requested the inquisition pass. Editing those domains was also rejected; only verification artifacts were produced.
Scalability potential: Tide remains stateless data; Project Atlas fit is domain 62 `Tide & Seismic Generator`; H-Phi data sovereignty improves conceptually because consumers can read fixed binary buffers instead of owning private harmonic state.
Hardware Impact: Verification adds no runtime cost. Runtime integration still requires DataVault/cold-load implementation and profiler proof before any final performance claim.

## Decision 6 - Omega Evidence Boundary

Problem: The batch demands `VERIFIED MASTER GRADE`, but project QA law forbids runtime verification claims without Unity Console, PlayMode, GCMonitor, profiler, and player-build artifacts.
Solution: Mark this slice as `OFFLINE VERIFIED MASTER GRADE / RUNTIME PENDING`. This is the strict evidence boundary: Python bake, binary validation, tests, FNV, atlas, and cross-data verification are complete; Unity runtime integration is not claimed.
Rejected Alternatives: Claiming full runtime verification from Python/static evidence was rejected as false verification language.
Scalability potential: The data is ready for Low/Main/Ultra SHINOBU ingest, but the future runtime owner must cold-load through DataVault or equivalent fixed buffers.
Hardware Impact: Offline evidence proves byte size and alignment. Actual frame-time, GC, and memory impact require Unity profiler proof on target hardware.

## Decision 7 - Source-Derived Constants

Problem: The previous tide weights were defensible in prose but still looked like invented fictional constants under hard-science review.
Solution: Replaced loose moon weights with constants from `Assets/_Project/Scripts/HectonCelestialEngine.cs` `CinematicOrbitDefinition` defaults: Moon0 radius 410000 m, e=0.072, period=8 h, gravityWeight=1.0; Moon1 radius 690000 m, e=0.118, period=12 h, gravityWeight=0.72. Secondary forcing now derives as `0.72 * (410000 / 690000)^3`. Solar forcing derives as real approximate solar/lunar tide ratio `27000000 / 389^3 = 0.4586854459`. Anomalies derive as `3e`.
Rejected Alternatives: Keeping manually selected ratios was rejected. Parsing values from YAML/scene files was rejected because the authoritative default constants are already in source and no scene edit is part of this DATA/MATH task.
Scalability potential: The same derived constants bake Low/Main/Ultra payloads; SHINOBU consumers stay stateless.
Hardware Impact: No runtime cost. Stronger source traceability reduces integration/debug cost; profiler proof remains pending.

## Decision 8 - H-Phi Local Model

Problem: A data artifact can still damage architecture if it forces private runtime state or concrete dependencies.
Solution: Added `hPhiLocalModel` to `Tide_Harmonics.json`: the payload requires zero local native buffers, zero managed mutable stores, and zero Unity-object truth stores. The payload sovereignty score is `1.0` for the data artifact itself. Runtime DataVault ownership remains `PENDING_INTEGRATION` because no Unity runtime code was changed.
Rejected Alternatives: Claiming final global H-Phi improvement from an offline data artifact was rejected; global H-Phi requires current source/tool output and integration proof.
Scalability potential: Low/Main/Ultra tables stay stateless and can be selected by hardware tier without mutating authority.
Hardware Impact: No runtime allocation requirement is introduced by the payload. Actual cold-load buffer ownership and profiler proof remain for the runtime integrator.

## Decision 9 - Strict Artifact Integrity Verifier

Problem: The verifier proved binary sizes and king tides, but did not explicitly prove PNG container validity, SHINOBU mapping text, metadata-to-binary scalar parity, or forbidden text hygiene.
Solution: Extended `Tools/VerifyTideBaker.py` to verify PNG IHDR, required mapping contract tokens, root/tier SHA parity, min/max parity, king-tide sample parity, H-Phi zero-store fields, and tide-owned text hygiene.
Rejected Alternatives: Leaving these as manual shell checks was rejected because evidence must be repeatable and stored in `VerifyTideBaker_TIDE_FOURIER_BAKER.json`.
Scalability potential: The stricter verifier protects Low/Main/Ultra payload selection from stale metadata and prevents SHINOBU from ingesting mismatched cold data.
Hardware Impact: No runtime cost. Offline verification cost only; runtime proof remains pending until Unity integration/profiler capture.

## Decision 10 - Repeatable Inquisition Runner

Problem: Cross-data evidence was still a manual chain of shell commands, making regressions easy to miss and hard to reproduce.
Solution: Added `Tools/VerifyTideInquisition.py`, which runs fourteen verifier commands and validates their generated reports for binary alignment, little-endian manifests, struct formats, Monte Carlo exploit pressure, FNV collisions, atlas domain count, lore tone, scalability tiers, and H-Phi stateless contracts.
Rejected Alternatives: Keeping the evidence as chat/shell history was rejected. Editing unrelated VFX data after a transient `VerifyVramBudgets` read failure was also rejected; the direct verifier rerun passed without touching VFX-owned files, then the full inquisition passed.
Scalability potential: The inquisition report gives SHINOBU ingestion one tide-owned gate for Low/Main/Ultra payloads and the adjacent data surfaces they depend on.
Hardware Impact: No runtime cost. Offline verification only; runtime cold-load and profiler proof remain pending for the eventual Unity integration owner.

## Decision 11 - Constant Provenance Ledger

Problem: The math audit still relied partly on prose; source-derived constants were documented, but there was no machine-readable proof that every tide constant was classified and traceable.
Solution: Added `constantProvenance` to `Tide_Harmonics.json` and verifier enforcement in both `VerifyTideBaker.py` and `VerifyTideInquisition.py`. The ledger covers 32 constants across prompt contracts, source-code constants, derived values, standard hash/time/binary constants, SHINOBU alignment, scalability policy, determinism, and explicit design scalars.
Rejected Alternatives: Leaving constants as comments or rationale text was rejected because it cannot be mechanically audited. Converting design thresholds into fake physics was also rejected; warning/target tide heights are labeled as design scalars, not physical constants.
Scalability potential: Low/Main/Ultra table selection now has a checked provenance contract, preventing stale or unclassified constants from entering SHINOBU ingest.
Hardware Impact: No runtime cost. Metadata and verifier only; binary payload SHA remains unchanged.

## Decision 12 - Compact SHINOBU Manifest

Problem: The raw binaries and full metadata were correct, but SHINOBU ingest still lacked a compact sidecar that states layout, CRC32, tier order, byte offsets, and no-header policy without forcing consumers to parse the large metadata document.
Solution: Added `Data/Environment/Tide_Harmonics.manifest.json` with headerless raw `<f` layout, CRC32/SHA256 per tier, first-sample bytes, king-tide byte offsets, tier selection, and DataVault hot-path policy. `VerifyTideBaker.py` now rejects stale or incorrect manifest formulas.
Rejected Alternatives: Adding binary headers was rejected because the prompt requires a lightweight float array and existing byte-size contract is exactly 9,600 bytes for the main payload. Runtime loader code was rejected because this agent is offline DATA/MATH only.
Scalability potential: SHINOBU can select Low/Main/Ultra from a small manifest while the raw payloads stay immutable and stateless.
Hardware Impact: No runtime algorithm cost. Sidecar is cold-load validation only; raw binary SHA remains unchanged.

## Decision 13 - Fixed-Record SHINOBU Index

Problem: JSON sidecars are cold-load acceptable, but SHINOBU cluster ingest also needs a minimal binary lookup surface that can be memory-mapped or copied without parsing key/value text.
Solution: Added `Data/Environment/Tide_Harmonics.index.h8bin` as three headerless `<8I` records: tier hash, tier index, sample count, sample stride in milli-hours, byte size, CRC32, first sample as `uint32`, and flags. `VerifyTideBaker.py`, `VerifyTideInquisition.py`, the manifest, and unit tests now enforce the index size, record layout, hashes, and 16-byte alignment.
Rejected Alternatives: Adding headers to `Tide_Harmonics.bin` was rejected because it would break the exact 9,600-byte prompt contract. Requiring runtime JSON parsing in hot paths was rejected because the payload should be copied once into DataVault-owned fixed buffers, then sampled by index.
Scalability potential: Low/Main/Ultra payload selection can be resolved from a 96-byte fixed index while the full raw tide tables remain immutable stateless data.
Hardware Impact: No runtime algorithm cost is introduced by this DATA/MATH slice. Cold ingest can avoid string parsing after the index is loaded; profiler proof remains pending until Unity/DataVault integration.

## Decision 14 - Struct Format Scanner Hardening

Problem: The data inquisition scanner produced false endian failures on constant-backed `struct` calls, including derived runtime-pack formats, loop-guarded net-sync formats, and verifier-internal `struct.calcsize(fmt)` helpers. That made endian proof dependent on fragile manual reruns.
Solution: Hardened `Tools/VerifyDataInquisition.py` so it resolves `len(...)`, integer padding math, `struct.calcsize(...)`, nested/local assignments, imported module constants, guarded dynamic little-endian loops, external container sentinels, and scanner-helper introspection without weakening payload `pack`/`unpack` checks.
Rejected Alternatives: Editing unrelated net-sync/submarine payload files was rejected because their formats were already guarded or explicitly `<`. Ignoring the failing inquisition was rejected because binary hygiene must be repeatable.
Scalability potential: The scanner now proves more binary callsites mechanically, increasing confidence that Low/Main/Ultra tide data and adjacent data surfaces remain SHINOBU-ingestable without per-platform byte swapping.
Hardware Impact: No runtime cost. This is offline QA/tooling only; runtime profiler proof remains pending.
