# LOG_ENCYCLOPEDIA_LORE_BAKER

## 2026-05-16 - Intake

What was wrong -> Live status/rationale were missing, and the existing lore blob used zlib compression with a non-prompt record layout.

What was done -> Created live status/rationale/log files and started replacing the lore pipeline with the raw aligned binary contract from the current XML.

Cinematic Cheats used -> Not applicable. This is data streaming, not simulation.

Exact Microseconds saved -> Pending measurement. Target saving is decompression and string-allocation avoidance; Unity proof remains absent.

Regression model -> CPU/GC/memory/cadence/correctness pending until packer, verifier, collision check, and binary audit pass.

## 2026-05-16 - Raw H8LR Lore Bake

What was wrong -> Existing `Data/Lore/Encyclopedia.h8bin` used zlib compression and a stale `<IQI>` record shape, violating the live OSHINO XML.

What was done -> Added `Tools/LorePacker.py`, replaced `Tools/VerifyLore.py`, rebuilt `Tools/test_verify_lore.py`, updated `Data/Lore/README.md`, baked raw UTF-8 into `Data/Lore/Encyclopedia.h8bin`, regenerated `Data/Lore/Encyclopedia.manifest.json`, and generated `Assets/_Project/Scripts/Core/Generated/H8LoreHashes.cs`.

Cinematic Cheats used -> None. Data path chooses uncompressed raw bytes as the zero-GC cheat over runtime decompression.

Verification -> `python Tools/LorePacker.py --check --hash-audit --list` baked 2 entries, 41,488 bytes, 0 collisions. `python Tools/VerifyLore.py --check --list` passed with `compression=none/raw-utf8 alignment=16 endian=<`. `python -m unittest Tools.test_verify_lore -v` ran 10 tests OK. Blob SHA-256: `9F0CBDB779EBADA5A9F21ACFDBF97FFC97113144EBD16B9B8692DD53E59A96B9`.

Exact Microseconds saved -> Unity runtime measurement absent. Expected saving is decompression and allocation removal from lore lookup; evidence is static/offline only.

Regression model -> CPU: lower at lookup by removing zlib. GC: lower risk by raw span slices. Memory: blob grows versus compressed but remains 41,600 bytes. Cadence: no per-frame work added. Correctness: verifier compares every payload to source.

## 2026-05-16 - Data Truth Inquisition

What was wrong -> Cross-domain verification exposed stale data: PDA technical logs lacked `CompactText`, and Beer-Lambert optics had regressed to a 33,554,432-byte spectral matrix while its status required 393,216 bytes.

What was done -> Regenerated PDA logs with `Tools/BuildPdaTechnicalLogs.py`; recompiled localization with `Tools/LocToBinary.py --verify-only`; regenerated optics with `Tools/OpticsBaker.py`; ran Dalton, Sabine, hash-collision, lore-tone, binary-alignment, and crafting Monte Carlo audits.

Cinematic Cheats used -> Optics remains a Beer-Lambert LUT instead of shader `exp()`. Lore remains raw data lookup instead of runtime string construction.

Verification -> `LoreTechValidator` passed 100 PDA logs. `LoreChecker` passed 188 entries with unresolved=0. Global hash audit reported 1018 records and 0 collisions. Crafting Monte Carlo ran 1,000,000 steps with profit_steps=0. Optics verify passed at 393,216 bytes. Dalton verify passed. Sabine verify passed. Global `Data/**/*.bin` and `.h8bin` alignment scan passed.

Exact Microseconds saved -> Runtime profiler proof absent. Static data savings: optics binary restored from 33,554,432 bytes to 393,216 bytes; lore lookup removes decompression path.

Regression model -> CPU/GC/memory improved at data level; Unity runtime still PENDING VERIFICATION. Correctness is backed by source-to-binary verification, collision checks, and Monte Carlo audit output.

## 2026-05-16 - Cognitive Reset And Final Inquisition Rerun

What was wrong -> The prior status overclaimed atlas compliance. `Data/Lore/Encyclopedia.manifest.json` had a prose `project_atlas_fit`, and `Docs/PROJECT_ATLAS.md` did not explicitly bind `DATA/LORE` to domain 72.

What was done -> Converted `project_atlas_fit` into a structured manifest object, added the atlas binding row, hardened tests to assert atlas fit, kept the sterile-term gate in the packer, rebaked `Data/Lore/Encyclopedia.h8bin`, removed verified `Tools/**/__pycache__` execution caches, and reran the verification suite.

Cinematic Cheats used -> Runtime lore remains a raw `H8LR` span-slice data artifact. The cheat is no decompression, no runtime string hashing, no private state, and metadata-only rich PDA previews.

Verification -> `LorePacker --check --hash-audit --list` passed with 2 entries, 41,488 bytes, SHA-256 `9F0CBDB779EBADA5A9F21ACFDBF97FFC97113144EBD16B9B8692DD53E59A96B9`. `VerifyLore --check --verify-source --verify-manifest --list` passed. `python -m unittest Tools.test_verify_lore -v` ran 10 tests OK. Manual binary audit passed `<4sIII`, `<IIII`, aligned offsets, and atlas fit. PROJECT_ATLAS fit check passed. Lore tone audit found no forbidden sterile terms. `LoreChecker` PASS 188 entries unresolved=0. `LoreTechValidator` PASS 100 PDA logs. `CraftingEconomyMonteCarlo --steps 1000000` reported profit_steps=0. Data inquisition reported PASS, 38 binaries aligned, 8 manifests endian `<`, 1,000,000 Monte Carlo steps, hashCollisions=0, atlasDomains=85. Optics, Dalton, and Sabine verifiers passed. `VerifyH8HashCollisions.py` reported 1,018 records and 0 collisions. Data plus `Assets/_Project/Data` binary alignment scan checked 38 files, all aligned16.

Exact Microseconds saved -> 0 us measured in Unity because no Unity profiler/GCMonitor logs are available. Offline data path removes zlib decompression and runtime lore string hashing by construction; measured runtime savings remain PENDING VERIFICATION.

Regression model -> CPU: static/offline risk reduced; runtime proof absent. GC: no runtime C# path added and H8LR remains raw span-ready data. Memory: lore blob is 41,488 bytes and aligned; broader data binaries are aligned16. Cadence: no per-frame system added. Correctness: source-to-binary verification, atlas binding, hash collision audit, and economy Monte Carlo all pass in CLI.

## 2026-05-16 - Verifier Sweep Cache Repair

What was wrong -> The no-arg verifier sweep exposed stale generated caches: Babel dictionary bytes failed deterministic rebuild, taxonomy binary cache had a stale record stride, and data inquisition required exact `BeerLambert` metadata in the optics manifest.

What was done -> Ran `Tools/BabelCompiler.py`, `Tools/Taxonomy/compile_taxonomy.py`, and patched `Tools/OpticsBaker.py` so generated optics metadata carries `BeerLambert / Beer-Lambert`. Reran the affected verifiers plus the broad data inquisition and binary alignment scans.

Cinematic Cheats used -> Data remains precomputed and stateless: Babel and taxonomy are direct binary lookup caches; optics stays a Beer-Lambert LUT instead of shader exponentials.

Verification -> `VerifyBabelDictionary.py` PASS: 45 sources, 32,579 entries, 17 languages, 1,523,792 bytes, endian `<`, aligned16. `VerifyBabel.py` PASS: hashCollisions=0. `Taxonomy/verify_taxonomy.py` PASS: fauna=22, flora=10, madness=6, binaryAligned16=yes. `VerifyDataInquisition.py` PASS: binaries=38, manifests=8, structFormats=138, monteCarloSteps=1,000,000, hashCollisions=0, atlasDomains=85.

Exact Microseconds saved -> 0 us measured in Unity. Static repair removes stale binary-cache load failure risk and keeps data loaders on direct aligned reads.

Regression model -> CPU: deterministic cache rebuild only, no runtime code added. GC: no C# runtime allocation path added. Memory: Babel 1,523,792 bytes, taxonomy 27,536 bytes, lore 41,488 bytes, all aligned. Cadence: no per-frame system added. Correctness: all affected verifiers pass in CLI.

## 2026-05-17 - Corrective Data Truth Rerun

What was wrong -> Disk truth had drifted again. `Tools/DaltonGasToxicityBaker.py` was absent despite a prior pass claim, `Status_ENCYCLOPEDIA_LORE_BAKER.md` still reported a 41,488-byte H8LR blob, and the current raw lore artifact is 41,920 bytes. The first full Metric Phi sweep exposed a real final-gate failure in `VerifyMetricPhiDataTruth`.

What was done -> Added `Tools/DaltonGasToxicityBaker.py`; hardened `Tools/VerifyMetricPhiDataTruth.py`; patched `Tools/H8VerifyCore.py` to exclude external JPEG/PSD container headers from HECTON binary endian checks; reran lore, physics, economy, hash, binary hygiene, Metric Phi, and taxonomy verifier suites. Updated status/rationale to remove runtime overclaiming.

Cinematic Cheats used -> Raw H8LR keeps lore as aligned UTF-8 span data instead of zlib decompression. Optics stays a Beer-Lambert LUT. Sabine and Dalton remain precomputed binary truth tables. High-tier visuals consume extra metadata/gradients/noise fields; low-tier paths use stripped aligned lookups.

Verification -> `LorePacker --check --hash-audit --list` PASS: 2 entries, 41,920 bytes, 0 collisions. `VerifyLore --check --verify-source --verify-manifest --list` PASS. `python -B -m unittest Tools.test_verify_lore -v` PASS: 12 tests. `CraftingEconomyMonteCarlo --steps 1000000` PASS: profit_steps=0. `VerifyH8HashCollisions.py` PASS: 1,046 records, 0 collisions. `VerifyBinaryHygiene.py` PASS: 46 binaries, 0 misaligned. `VerifyDataInquisition.py` PASS: 46 binaries, 11 manifests, endian `<`, structFormats=273, atlasDomains=85. `OpticsBaker --verify` PASS: 393,216 bytes. `DaltonGasToxicityBaker --verify` PASS: 128,128-byte main, 4,080-byte toaster, 96,112-byte overkill. `SabineBaker --verify-only` and `VerifySabineBaker.py` PASS: 524,288 bytes, `<ff`. `RunMetricPhiVerifySweep.py` PASS: 35/35 commands, required_failures=0. `VerifyMetricPhiDataTruth.py` PASS: 37 checks, 46 binaries, 133 relevant struct sites, endian_failures=0. `Taxonomy/run_verify_sweep.py` PASS: 25/25. `git diff --check` returned only CRLF conversion warnings.

Exact Microseconds saved -> 0 us measured in Unity. Static estimates only: removed zlib/runtime hash risk on lore lookup, kept direct aligned reads for SHINOBU ingest, and prevented false final-gate passes/failures in Metric Phi data truth. Runtime CPU/GC/frame savings require Unity Profiler or GCMonitor artifacts.

Regression model -> CPU: no runtime code changed; offline validators stronger. GC: no gameplay allocation path added; H8LR remains raw span-ready data. Memory: current lore blob is 41,920 bytes; 46 data binaries are aligned16. Cadence: no Tick/Update system added. Correctness: current CLI/static suite is green; Unity import, Play Mode, Profiler, GCMonitor, player build, and visual runtime checks remain PENDING VERIFICATION.
