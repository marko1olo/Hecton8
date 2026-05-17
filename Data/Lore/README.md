# HECTON-8 Lore Binary Package

Status: RAW H8LR BAKED / VERIFY BEFORE HANDOFF
Owner: ENCYCLOPEDIA_LORE_BAKER

## Artifact

- Blob: `Data/Lore/Encyclopedia.h8bin`
- Manifest: `Data/Lore/Encyclopedia.manifest.json`
- Source directory: `Docs/Lore`
- Current source: `Docs/Lore/Lore_Bible.md`
- PDA technical log payload: `Data/Lore/PdaTechnicalLogs.h8jsonl`
- PDA technical log binary: `Data/Lore/PdaTechnicalLogs.h8bin`
- PDA technical log toaster binary: `Data/Lore/PdaTechnicalLogs_Toaster.h8bin`
- PDA technical log manifest: `Data/Lore/PdaTechnicalLogs.manifest.json`
- PDA technical log generator: `Tools/BuildPdaTechnicalLogs.py`
- PDA technical log validator: `Tools/LoreTechValidator.py`
- PDA technical log binary packer: `Tools/PackPdaTechnicalLogs.py`
- PDA technical log verifier: `Tools/VerifyPdaTechnicalLogs.py`

## PDA Technical Logs

`Data/Lore/PdaTechnicalLogs.h8jsonl` contains 100 minified JSONL rows for industrial PDA technical logs. Each row includes:

- `Text`: full PDA page text.
- `CompactText`: low-tier stripped text for Celeron/i3 displays.
- `TierData`: Low/Middle/High/Ultra consumption policy.
- `ExtraData`: 4096-gradient and harmonic-noise metadata for high-tier PDA overlays.

The canonical localization fields are synced into `Data/Localization/en_US.json` and compiled into `Data/Localization/en_US.bin` through `Tools/LocToBinary.py`. The full PDA source is also packed into `Data/Lore/PdaTechnicalLogs.h8bin` by `Tools/PackPdaTechnicalLogs.py`.

`Tools/PackPdaTechnicalLogs.py` also emits `Data/Lore/PdaTechnicalLogs_Toaster.h8bin`, a compact-only H8PT profile for Celeron/i3-class ingest. It keeps the same sorted LocHash record table and link/stress fields, stores `CompactText` in the Text section, strips the compact/extra sections to zero length, and records a separate `ToasterBinary` contract in the manifest.

Validation:

```powershell
python Tools\BuildPdaTechnicalLogs.py
python Tools\LoreTechValidator.py
python Tools\LocToBinary.py --input Data\Localization\en_US.json --output Data\Localization\en_US.bin --normalize
python Tools\PackPdaTechnicalLogs.py
python Tools\PackPdaTechnicalLogs.py --check --list
python Tools\VerifyPdaTechnicalLogs.py
python Tools\LoreChecker.py --output Docs\AgentLogs\LoreChecker_ENCYCLOPEDIA_TECHNICAL_WRITER.json --report-only
```

`Tools/LoreTechValidator.py` enforces the 1500 character PDA UI cap, 240 character low-tier compact cap, LocHash-compatible FNV-1a hashes, `<link=0xXXXXXXXX>` target existence, five Habitat Integrity corruption variants, no-fantasy/no-sterile-sci-fi term guards, hard-science term coverage, zero hash collisions, and localization sync.

`Tools/PackPdaTechnicalLogs.py` writes little-endian `H8PT` records with explicit `<` struct formats. Header is 64 bytes, each index record is 64 bytes, each `ExtraVisualRecord` is 64 bytes, every section offset is 16-byte aligned, and every per-record `Text`/`CompactText`/`ExtraVisualRecord` payload starts on a 16-byte boundary. Runtime binary ingest does not require JSON parsing.

The PDA manifest mirrors the `H8PT` header for ingestion audits: raw JSONL `FNV1a32` source hash, table/text/compact/extra offsets, section lengths, payload end, and trailer padding are emitted and rechecked by `Tools/VerifyPdaTechnicalLogs.py`. Any stale source, stale binary, or manifest/header drift fails the verifier.

The same manifest carries section-level CRC32 integrity for the full binary, header, record table, full text, compact text, extra visual records, and combined payload. `Tools/VerifyPdaTechnicalLogs.py` rejects any CRC drift before claiming the artifact is fresh.

`Tools/VerifyPdaTechnicalLogs.py` is intentionally independent of the packer. It unpacks the H8PT header and records directly with `<4sHHIIIIIIIIIIIIII` and `<IIIIIIIIIIIIIIII`, validates the full and toaster binaries against JSONL payloads, and rejects stale source hashes, stale CRCs, bad offsets, broken links, nonzero toaster visual fields, and atlas/H-Phi drift.

The manifest now includes a `LookupContract` for stateless PDA access: `uint32_binary_search_sorted_record_table`, `LocHash` key, strict ascending table order, `SearchIterationsMax=7` for the current 100 rows, and `not_found_no_exception` fallback. `Tools/test_pda_technical_logs.py` decodes the H8PT table directly and proves TECH_01/TECH_50/TECH_100 plus a missing hash resolve within that bound without private caches.

The PDA manifest also carries a static physics coverage audit from `Tools/LoreTechValidator.py`:

- Dalton partial pressure: `TECH_02`, `TECH_03`, `TECH_47`.
- Beer-Lambert attenuation: `TECH_21`, `TECH_58`.
- Sabine RT60 reverb: `TECH_57`.
- Torricelli ingress: `TECH_42`.
- Hydrostatic project scale: `TECH_07`.
- Scalar flood proxy: `TECH_03`, `TECH_14`, `TECH_16`, `TECH_18`, `TECH_42`.

These are lore/source provenance gates, not runtime simulation authority.

`Tools/LoreTechValidator.py` also emits a per-entry `ToneAudit`. Every PDA page must carry at least one industrial failure term from the enforced lexicon; the current manifest has 100 audited rows and zero weak entries. `Tools/test_pda_technical_logs.py` independently decodes the H8PT record table and verifies every packed `Text` and `CompactText` payload round-trips to the JSONL source.

`ExtraData` derivation is defined in `Tools/PdaTechSchema.py` and verified by `Tools/test_pda_technical_logs.py`. Required constants:

- `HydrostaticPaPerMeterX100 = round(1025 * 9.80665 * 100) = 1005182`.
- `ProjectPressureMPaPer100mX1000 = 1000`.
- `GradientResolution = 4096`.
- `NoiseSeed = LocHash(TECH_ID:Title:PDA_EXTRA)`.
- `HarmonicHzX100 = 6000 + ((NoiseSeed >> 20) % 6001)`.

These `ExtraData` fields are presentation metadata only. They do not own simulation physics.

In JSONL, `ExtraData` remains human-auditable authoring source. In H8PT, the same data is packed as a fixed `ExtraVisualRecord`:

```text
<IIIIIIIIIIIIIIII
SchemaVersion, DerivationModelHash, VisualDataAuthorityHash, VisualProfileHash,
GradientLutHash, GradientResolution, GradientIndex, NoiseSeed,
HarmonicOctaves, HarmonicHzX100, OverlayFlagsHash, OverlayIndex,
StressState, HydrostaticPaPerMeterX100, SurfacePressurePa, ProjectPressureMPaPer100mX1000
```

PROJECT_ATLAS fit is explicitly recorded in `Data/Lore/PdaTechnicalLogs.manifest.json`:

- `69` - Zero-GC Subtitles (Babel)
- `70` - Diegetic Terminals (3D UI)
- `72` - PDA Encyclopedia Streaming
- `73` - AUP Narrative Triggers

## Binary Layout

Little-endian.

- Header: `<4sIII>`, 16 bytes.
- Magic: `H8LR`.
- Version: `1`.
- Count: `uint32`.
- Header reserved field: `uint32`, always zero.
- Record: `<IIII>`, 16 bytes.
- Record fields: `uint32 hash`, `uint32 absolute_offset`, `uint32 raw_length`, `uint32 reserved_zero`.
- Payload: raw UTF-8 Markdown.
- Alignment: every payload starts on a 16-byte boundary.
- Compression: none. Runtime reads raw spans; zlib is rejected.
- Hash: FNV-1a over ASCII filename stem, A-Z folded to lowercase.
- Source validation: every canonical id must be repository-relative, normalized, ASCII, forward-slash `.md`; every Markdown payload must decode as UTF-8 before compression.
- Manifest schema: root and entry keys must exactly match the emitted schema; entry hashes must be canonical `0xXXXXXXXX` strings; integer fields must be JSON integers; strings, floats, booleans, malformed JSON, malformed entry containers, and unknown fields fail verification.

## Required Verification

Run before packaging:

```powershell
python Tools\LorePacker.py --check --hash-audit --list
python Tools\VerifyLore.py --check --list
python -B -m unittest Tools.test_verify_lore -v
```

The CLI anchors relative paths to the repository root, so the same commands work when launched from a subdirectory. Blob, manifest, and extracted Markdown writes use `.tmp` + atomic replace.

Expected active records:

```text
0xAEC57EAC Docs/Lore/Lore_Bible.md
0xBC52DB39 Docs/Lore/Archives/DeepReach_ColonyFailureArchive.md
```

To extract by path:

```powershell
python Tools\VerifyLore.py --source-path Docs\Lore\Lore_Bible.md --output-text .codex-artifacts\NARRATIVE_LORE_STREAMING_BAKER_latest_extract.md
```

Do not place operator notes under `Docs/Lore`; every Markdown file there is source content for the blob.
