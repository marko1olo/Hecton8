# Rationale_VFX_PARTICLE_BUDGET_MAP

Problem: VFX buffer ownership was diffuse. Existing visual scalability data describes broad particle totals, not exact compute buffer allocation per named VFX system.
Solution: Added an offline Data/System budget map with liveCount, dispatch-aligned bufferCapacity, power-of-two struct stride, and exact byte totals per tier.
Rejected Alternatives: Runtime C# edits and Unity asset mutation were rejected because the task is offline only and this agent owns Data/System, not runtime VFX renderer code.
Scalability potential: Low uses tiny fake-first buffers; Middle/Deck opens GPU particles without exceeding UMA pressure; High/Pro raises density; Ultra/GOD_MODE spends saved cycles on visual overkill by increasing marine snow, silt, bubbles, and presentation plumes.
Hardware Impact: MX350/i3 path caps total VFX compute buffers at 474272 bytes, far under the 209715200 byte task cap. Runtime profiler proof is absent, so CPU/GPU gains remain PENDING VERIFICATION.

Problem: GOD_MODE MarineSnow mandate requires 100000 particles, which is not divisible by the 64-thread kernel group.
Solution: Store liveCount 100000 and bufferCapacity 100032, adding 32 dead padding slots. Dispatch groups become 1563. The verifier enforces this.
Rejected Alternatives: Allocating exactly 100000 slots was rejected because non-aligned compute dispatch creates tail handling and branch drift. Rounding to 131072 was rejected as unnecessary VRAM bloat.
Scalability potential: Low/Middle/High use aligned counts with no padding; Ultra accepts 1024 bytes of state padding to keep the shader path uniform.
Hardware Impact: Padding cost is 2304 total bytes after ping-pong, dead-list, and visible-index accounting. Expected CPU saving from simpler dispatch math: 35 us cold/setup estimate; GPU proof absent.

Problem: Struct padding can silently change VRAM when C# and HLSL disagree on stride.
Solution: Enforced 32B and 64B struct sizes only. MarineSnow, Sparks, and Silt use 32B; Bubbles and Blood use 64B for wobble, tint, pressure, and decay payloads.
Rejected Alternatives: 48B MarineSnow from an older mandate was rejected for this file because the prompt explicitly demands power-of-two verification. Ad hoc 36B/52B structures were rejected as alignment traps.
Scalability potential: Low keeps cache pressure low; Ultra uses 64B only where the player can read the richer effect.
Hardware Impact: Prevents driver-side stride ambiguity. Exact runtime savings require GPU capture.

Problem: Under SystemStress01 > 0.9 the renderer needs deterministic sacrifice order, not artist preference.
Solution: Zero order is Sparks, Blood, Bubbles, Silt. MarineSnow survives longest because it is the cheapest persistent underwater scale cue.
Rejected Alternatives: Killing MarineSnow first was rejected because it destroys depth readability. Random per-emitter throttling was rejected because it flickers and violates hysteresis.
Scalability potential: Low keeps baseline ambience; High/Ultra can shed decorative effects while preserving core noir/deep-sea perception.
Hardware Impact: Estimated pressure relief is 200-600 us depending tier and scene density, PENDING GPU CAPTURE. VRAM relief is immediate only if runtime owner actually reallocates or disables buffers; this data file alone is static.

Problem: Runtime mapping must not introduce per-frame JSON, GlobalRegistry polling, or buffer resizing.
Solution: Documented a cold boot/settings-change handoff: SystemDispatcher VISUAL_SYNC receives immutable caps before GraphicsBuffer allocation. Hot paths consume cached integer caps only.
Rejected Alternatives: Per-frame JSON reads, direct concrete VFX owner lookups, and live buffer resizing were rejected as GC/fragmentation risks.
Scalability potential: Toaster uses cached TOASTER caps; Deck/Pro/GOD_MODE can select richer rows at boot and drop through pressure tiers with hysteresis.
Hardware Impact: Expected hot-path allocation remains 0 B/frame by design. Measured proof absent.

Problem: Final polish required status VERIFIED MASTER GRADE without inflating scope.
Solution: Re-ran the verifier, Python bytecode compile, JSON parser, grep self-audit, and git diff whitespace check. No Unity runtime work was added.
Rejected Alternatives: Pretty-printing JSON was rejected because Task 14 requires minification. C# runtime integration was rejected because the prompt says offline only and domain is Data/System.
Scalability potential: The data keeps Low/Deck/Pro/GOD_MODE rows explicit and leaves runtime tier selection to existing dispatcher/governor code.
Hardware Impact: TOASTER compute buffer total remains 474272 bytes. `dotnet build` could not run because no `.csproj` exists under this workspace; source compile status is therefore not re-proven by this pass.

Problem: JSON-only budget data was not cold enough for SHINOBU-style ingestion.
Solution: Added a little-endian binary cache contract and generated `Data/System/VFX_Budgets.h8bin`: 64-byte header, 20 x 64-byte rows, total 1344 bytes, 16-byte aligned.
Rejected Alternatives: Runtime JSON parsing and string-key lookup were rejected because they force private state or cold-load allocation. A variable-length binary table was rejected because it invites alignment and endian bugs.
Scalability potential: TOASTER can read one fixed row per system; GOD_MODE uses the same row shape with extra-data byte fields, so no alternate runtime parser is needed.
Hardware Impact: Cold load becomes fixed-stride binary row reads. Hot-path impact remains 0 B/frame by contract; runtime measurement remains PENDING VERIFICATION.

Problem: Row identity was hearsay without collision-proof keys.
Solution: Added FNV-1a ASCII-lower hashes for tiers, systems, and tier/system lookup keys. The VFX verifier enforces zero collisions, and the global `VerifyH8HashCollisions.py --root .` pass completed with 1018 records and 0 collisions.
Rejected Alternatives: String IDs in runtime rows were rejected; integer enum-only rows were rejected because they are brittle across data/code generation boundaries.
Scalability potential: Fixed hash rows allow stateless lookup through Data/System cache data without concrete domain references.
Hardware Impact: Removes string lookup from runtime allocation path. Exact microseconds remain PENDING RUNTIME CAPTURE.

Problem: The previous GOD_MODE row raised particle counts but did not account for overkill visual payloads.
Solution: Added exact GOD_MODE extra-data buffers: MarineSnow 5120 B, Sparks 512 B, Bubbles 2560 B, Silt 3072 B, Blood 1536 B. GOD_MODE total is now 16128160 B.
Rejected Alternatives: Unmetered high-res gradients and harmonic noise were rejected. Decorative payloads that do not land in `totalBufferBytes` are budget fraud.
Scalability potential: Low/Middle/PRO keep extraDataBytes at 0; GOD_MODE buys visible detail with a known 12800 B payload.
Hardware Impact: GOD_MODE delta is +12800 B. TOASTER remains 474272 B.

Problem: The hard-science audit demanded physics provenance, but this artifact is not a physics LUT.
Solution: Added a math audit stating Beer-Lambert, Dalton, and Sabine are not applicable to this file; every stored number is derived from counts, strides, alignment, fixed buffers, or atlas/SHINOBU schema constants.
Rejected Alternatives: Claiming optical/acoustic/gas physics where none exists was rejected as false evidence. Inventing pseudo-physical particle counts was rejected; the prompt mandates caps.
Scalability potential: Separating budget math from physical LUT math prevents cross-domain contamination.
Hardware Impact: Prevents incorrect runtime assumptions; no direct frame-time claim.

Problem: User demanded economy proof despite this prompt owning VFX Data/System.
Solution: Ran `CraftingEconomyMonteCarlo.py --steps 1000000 --seed 12648430`, `EconomyValidator.py --root .`, and `EconomyRecipeGraphAudit.py --root .`. Results: 0 profit steps, `STATUS: ECONOMY BALANCED`, and `ECONOMY SECURED`.
Rejected Alternatives: Editing economy files from this VFX assignment was rejected as domain boundary violation.
Scalability potential: Economy evidence is recorded as external audit data only.
Hardware Impact: No VFX runtime effect.

Problem: Root verifier sweep exposed failures outside this domain.
Solution: Recorded external failures rather than hiding them: `VerifyHullStressBudget.py` fails god-mode decal seeds and its own economy proof status; `VerifyLore.py --check` fails `DeepReach_ColonyFailureArchive.md` payload mismatch. `VerifyQuestDag.py` and `VerifyBabel.py` pass on rerun.
Rejected Alternatives: Patching Habitat, Lore, or Quest from VFX Data/System was rejected.
Scalability potential: Keeps the VFX artifact clean while leaving integrator-readable debt.
Hardware Impact: No direct VFX runtime effect.

Problem: Stale verifier failure memory could mask the current disk state.
Solution: Re-ran every `Tools/Verify*.py` script from disk with explicit special args for hash and lore checks. All 24 verifier scripts exited 0 in the fresh sweep, including the previously failing HullStress and Lore checks.
Rejected Alternatives: Reporting old failures without re-running the current files was rejected. Editing Habitat/Lore from this VFX pass was still rejected because the fresh sweep did not require it.
Scalability potential: Current data truth across Atlas, binary hygiene, physics LUTs, economy curves, and VFX budgets is now validated by scripts rather than chat memory.
Hardware Impact: No direct VFX runtime effect; this is static evidence hardening.

Problem: Binary cache lacked a separate integrity manifest and non-mandated counts could still be read as magic numbers.
Solution: Added `Data/System/VFX_Budgets.manifest.json` with SHA-256, CRC32, row count, row size, endianness, atlas domains, FNV constants, and binary file byte count. Added verifier-enforced `countDerivation` for every system/tier row.
Rejected Alternatives: Trusting the `.h8bin` without an external manifest was rejected; embedding only header constants was not enough for cache ingestion audit. Leaving Bubble/Blood caps as bare numbers was rejected.
Scalability potential: SHINOBU can validate the binary before ingesting fixed rows. TOASTER and GOD_MODE consume the same row schema, so fallback and overkill paths cannot drift structurally.
Hardware Impact: Manifest is cold validation data only. Binary remains 1344 bytes, 16-byte aligned.

Problem: Lore audit needed enforcement, not just prose.
Solution: Added verifier-enforced NASA-punk dirty aliases for VFX rows and sterile-term rejection for the VFX system table.
Rejected Alternatives: Generic terms like "clean particles" or "sparklefield" were rejected because they do not match the project tone and would leak into tooling labels.
Scalability potential: Runtime IDs stay hash-based; lore labels remain data-only and never enter hot paths.
Hardware Impact: 0 B/frame; cold JSON only.

Problem: SHINOBU could verify the binary hash but still had to infer row offsets and field offsets from external Python code.
Solution: Promoted the binary ingestion schema into data: `H8VB.VFXBudget.FixedRows.v1`, 16 explicit header fields, 16 explicit row fields, `rowIndex=tierIndex*5+systemIndex`, `byteOffset=64+rowIndex*64`, and a 20-row offset table in the manifest. The verifier now enforces all of it.
Rejected Alternatives: Markdown-only documentation was rejected because SHINOBU ingestion needs machine-readable offsets. Variable-length rows were rejected because they break fixed-stride mmap reads and invite alignment drift.
Scalability potential: TOASTER and GOD_MODE use the same fixed-row parser; cheap devices skip rich buffers by row selection, top-tier devices get overkill rows without a second schema.
Hardware Impact: Runtime remains 0 B/frame by contract. Cold ingest can seek directly to a row offset instead of scanning strings; exact microseconds remain PENDING RUNTIME CAPTURE.

Problem: Whole-file SHA/CRC proves package integrity but gives poor diagnostics when one row is corrupted or hot-swapped incorrectly.
Solution: Added manifest CRC32 for each 64-byte row and made `Tools/VerifyVramBudgets.py` recompute those row CRCs from `Data/System/VFX_Budgets.h8bin`.
Rejected Alternatives: Whole-file-only integrity was rejected because it cannot isolate a bad tier/system row. Per-row SHA-256 was rejected as unnecessary manifest bloat for 64-byte fixed rows; CRC32 is enough for fast ingest triage while the whole file still has SHA-256.
Scalability potential: TOASTER can validate only the rows it will ingest; GOD_MODE can validate richer rows without a schema fork.
Hardware Impact: Runtime hot path remains 0 B/frame. Cold validation adds 20 tiny CRCs; exact ingest microseconds remain PENDING RUNTIME CAPTURE.

Problem: The fixed rows had local CRCs, but the 64-byte header still depended on whole-file checksum evidence and the struct formats were only implicit in Python constants.
Solution: Added explicit `headerStructFormat=<4s15I`, `rowStructFormat=<16I`, and `headerCrc32` to the generated manifest. The verifier now checks the struct formats, asserts both packed sizes are 64 bytes, and recomputes the first-64-byte header CRC.
Rejected Alternatives: Relying on `struct.Struct` construction alone was rejected because the cache consumer needs machine-readable format proof. Duplicating header fields as text-only docs was rejected because SHINOBU ingestion validates JSON/binary data, not prose.
Scalability potential: All tiers share one header and one fixed-row schema; TOASTER and GOD_MODE diverge only in row payload values, not parser logic.
Hardware Impact: Runtime hot path remains 0 B/frame. Cold validation reads one 64-byte header CRC and 20 row CRCs; exact ingest microseconds remain PENDING RUNTIME CAPTURE.

Problem: A TOASTER-only build still needed to infer which five fixed rows formed its ingest slice.
Solution: Added manifest tier slices with `tierByteOffset=64+tierIndex*5*64`, 320-byte tier ranges, `tierCrc32`, declared total bytes, live particle totals, and capacity totals. The VFX verifier recomputes every tier CRC from the binary payload.
Rejected Alternatives: Leaving tier slicing as a consumer-side convention was rejected because the Celeron/i3 path needs direct slice validation. Duplicating separate TOASTER VFX binaries was rejected because it would fork the schema and increase cache drift risk.
Scalability potential: TOASTER can validate/read only its 320-byte tier slice; GOD_MODE validates its own 320-byte slice with extra payload accounting, using the same parser and row layout.
Hardware Impact: Runtime hot path remains 0 B/frame. Cold low-tier ingest can skip scanning 15 unused rows; exact microseconds remain PENDING RUNTIME CAPTURE.

Problem: Full-root verification exposed transient external drift: Babel source hashes were stale after lore metadata changed, and the sweep briefly saw moving-target Organic/Tide states.
Solution: Re-ran isolated verifiers, then repaired Babel through the deterministic owner compiler `Tools/BabelCompiler.py`. The final full sweep reported all 28 `Tools/Verify*.py` scripts exited 0.
Rejected Alternatives: Hand-editing Babel manifests was rejected; using the owner compiler preserves binary, manifest, and generated hash constants together. Editing Organic/Tide was rejected because isolated current-disk verifiers passed.
Scalability potential: Localization remains stateless aligned binary lookup with Core-only TOASTER residency and richer Ultra text metadata from the same source hashes.
Hardware Impact: VFX runtime unchanged. Babel repair updates localization binary/cache artifacts outside the VFX runtime hot path.

Problem: A reset audit found `VerifyMetricPhiDataTruth.py` failing on current disk even though VFX byte rows were still valid.
Solution: Treated the failure as evidence debt and traced the two failing checks: stale `Docs/Reports/HECTON_PHI_SCORE_FINAL.json` freshness after generated Babel hash constants, and stale `Docs/Reports/METRIC_PHI_VERIFY_SWEEP.json` with `VERIFY_SWEEP_FAIL`. Rebuilt H-Phi through `Tools/CalculateHPhi.py`, then reran `Tools/RunMetricPhiVerifySweep.py` with the explicit temporary `xxhash` reference path. Direct MetricPhi verification then returned `DATA_TRUTH_VERIFIED`.
Rejected Alternatives: Hand-editing report JSON, ignoring stale self-evidence, or changing VFX budget data to silence a non-VFX failure were rejected. The VFX binary and manifest were kept under verifier control.
Scalability potential: The atlas and MetricPhi artifacts now describe the current source/data surface again; VFX remains a stateless fixed-row Data/System lookup for TOASTER through GOD_MODE.
Hardware Impact: 0 us runtime and 0 B/frame; this is offline evidence repair. Full sweep wall time was 1617.9 s. Runtime Unity, GCMonitor, RenderDoc, frame-time, memory, and player-build proof remain PENDING VERIFICATION.
