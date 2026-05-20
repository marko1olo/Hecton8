# Data Monolith H8BIN Spec

Date: 2026-05-12
Status: STATIC_SOURCE REVIEWED / RUNTIME PENDING

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-20 R47 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.

R47 root/architecture authority-spine/runtime-wording/counter-drift correction (`Docs/Reports/2026-05-20_DOCUMENTATION_R47_ROOT_ARCHITECTURE_AUTHORITY_SPINE_RUNTIME_WORDING_AND_COUNTER_DRIFT_LOCAL.md`) is the latest local static DOC_GLOBAL boundary for architecture/root documentation. R46 remains the prior interior-authority/route-field/proof-language correction. R45 remains the prior R43/R44 residue/proof-artifact/source-counter correction; R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; R42 remains the prior counter/route-boundary/proof-label correction; R41 remains the prior global-authority/internal-residue correction; R40 remains the prior R38-residue/source-counter correction; R39 remains the prior authority-counter/proof-wording correction; R38/R37/R36/R35/R34 remain prior static correction layers. Runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## 2026-05-20 DOC_GLOBAL R46 Root/Architecture Boundary Note

R47 root/architecture authority-spine/runtime-wording/counter-drift correction (`Docs/Reports/2026-05-20_DOCUMENTATION_R47_ROOT_ARCHITECTURE_AUTHORITY_SPINE_RUNTIME_WORDING_AND_COUNTER_DRIFT_LOCAL.md`) (R46 prior interior-authority/route-field/proof-language correction; R45 prior R43/R44 residue/proof-artifact/source-counter correction) keeps this file as a static DataMonolith binary-spec contract, not product payload existence, runtime I/O, or platform-storage proof. Current DOC_GLOBAL boundary is `Docs/Reports/2026-05-20_DOCUMENTATION_R47_ROOT_ARCHITECTURE_AUTHORITY_SPINE_RUNTIME_WORDING_AND_COUNTER_DRIFT_LOCAL.md`; R46 remains prior at `Docs/Reports/2026-05-20_DOCUMENTATION_R46_ROOT_ARCHITECTURE_INTERIOR_AUTHORITY_ROUTE_FIELDS_AND_PROOF_LANGUAGE_LOCAL.md`; R45 remains prior at `Docs/Reports/2026-05-20_DOCUMENTATION_R45_ROOT_ARCHITECTURE_R43_R44_RESIDUE_PROOF_ARTIFACTS_AND_COUNTERS_LOCAL.md`; R44 remains prior at `Docs/Reports/2026-05-20_DOCUMENTATION_R44_ROOT_ARCHITECTURE_INTERNAL_RESIDUE_EXACT_ROUTE_FIELDS_LOCAL.md`; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; R42 remains the prior counter/route-boundary/proof-label correction; R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers. Current static gates: `Tools/AtlasCheck.py` remains red on `ATLAS_CHECK_FAIL references=6781 missing=61` (one Dynamic Decals vendor asset ref, RealtimeCSG vendor icon/readme image refs, missing HectonMaskChannelPacker/HectonMaterialChannelPackValidator editor source refs, and missing HabitatDamageBakePipeline source ref in the current atlas); `Docs/Modding/Validate_Mod_API_Static.ps1` passes (`Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`) as static-tool orientation only. Runtime proof remains absent.

## Source Anchors

Evidence class: STATIC_SOURCE / FILESYSTEM path check. These anchors prove current path visibility only, not DataMonolith load success, StreamingAssets presence, profiler, or player-build proof.

- `Assets/_Project/Scripts/Data/Monolith/H8DataMonolithTypes.cs`
- `Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs`
- `Assets/_Project/Scripts/Data/Monolith/H8DataHash.cs`
- `Assets/_Project/Scripts/Editor/DataMonolith/H8DataMonolithCompiler.cs`
- `Assets/_Project/Scripts/Editor/DataMonolith/H8DataMonolithCompilerWindow.cs`
- `Assets/_Project/Scripts/Editor/DataMonolith/Hecton8.DataMonolith.Editor.asmdef`

Owner symbols above are filesystem-verified by the explicit source-anchor paths; symbol names alone are not evidence artifacts.

## Current File

| Field | Value |
|---|---|
| Extension | `.h8bin` |
| Default path | `StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` |
| runtime owner | `H8StaticDataArena` |
| editor compiler | `H8DataMonolithCompiler` |
| editor assembly | `Hecton8.DataMonolith.Editor` |
| hash owner | `H8DataHash` |

## Operational Readiness Rule

Data Monolith is not runtime-ready until the active player payload exists at:

```text
Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin
```

Required proof before a readiness claim:

- editor import completed with no Data Monolith compiler errors
- bake emitted the active StreamingAssets payload above
- boot loaded `H8StaticDataArena` from that payload
- checksum/section-count validation passed
- Unity Console, Play Mode, and player-build smoke evidence are attached when
  the route is player-facing

`Data/Balance/Baked/H8StaticData.bin`, authoring CSVs, compiler source files, or
Addressables folder presence do not prove active runtime payload readiness.

## Binary Layout

```text
0x00  H8DataBlobHeader      16 bytes
0x10  H8DataBlobDirectory   64 bytes
0x50  H8DataSectionEntry[]  16 bytes each
...   section payloads       16-byte aligned
...   UTF-8 localization pool
```

## Header

`H8DataBlobHeader`:

| Field | Type | Meaning |
|---|---|---|
| `Magic` | `uint` | `H8DM`, duplicated in the directory for corruption triage |
| `FormatVersion` | `ushort` | binary format version |
| `HeaderBytes` | `ushort` | fixed value `16` |
| `Checksum64` | `ulong` | XXHash3-64 for bytes `[16..blobLength)` |

## Directory

`H8DataBlobDirectory` is fixed at 64 bytes. It stores magic, version, section count, section table offset/bytes, blob byte count, data start offset, localization offset/bytes, flags, and reserved fields.

`H8DataSectionEntry` is 16 bytes:

```text
SectionId:uint
RecordSize:uint
Count:uint
OffsetBytes:uint
```

## Sections

Current section ids:

| Id | Section |
|---:|---|
| 1 | `Items` |
| 2 | `Creatures` |
| 3 | `Biomes` |
| 4 | `Recipes` |
| 5 | `BiomeHeatmap` |
| 6 | `QuestNodes` |
| 7 | `QuestEdges` |
| 8 | `LootCdf` |
| 9 | `VoxelMaterials` |
| 10 | `AudioClipRegistry` |
| 11 | `VfxScalars` |
| 12 | `DepthPressureCurve` |
| 13 | `ToolHeatCapacity` |
| 14 | `SubmarineHullConstants` |
| 15 | `NarrativeTriggers` |
| 16 | `PhysicsMaterials` |
| 17 | `GhostModules` |
| 18 | `RadiationIntensityMap` |
| 19 | `SpawnCreditCosts` |
| 20 | `LightAttenuationCurve` |
| 21 | `SopErrors` |
| 22 | `HudLayouts` |
| 23 | `LocalizationUtf8` |
| 24 | `SectorPageDirectory` |
| 25 | `Economy` |
| 26 | `PhysicsConstants` |

## Critical Record Sizes

| Record | Size |
|---|---:|
| `H8ItemRecord` | 80 |
| `H8CreatureTraitRecord` | 64 |
| `H8CreatureGenomeTraitBlock` | 32 |
| `H8BiomeRecord` | 64 |
| `H8EconomyRecord` | 64 |
| `H8PhysicsConstantsRecord` | 64 |
| `H8DataMonolithTelemetryEntry` | 64 |
| `H8StaticLocalizationReference` | 16 |

Other monolith records use explicit layout and source-owned sizes. Consumers must use source constants and `UnsafeUtility.SizeOf<T>()`, not hand-written byte math.

## UTF-8 Text Slices

All localization keys, names, descriptions, addressable keys, and static error messages are stored in
the `LocalizationUtf8` section as null-terminated UTF-8 bytes. Text-bearing fixed records store
unsigned offsets plus byte lengths. Empty/missing strings use `uint.MaxValue` as the offset sentinel
and `0` as byte length.

## ItemID Hashing

`H8DataHash.ComputeFnv1A32`:

| Step | Value |
|---|---|
| offset basis | `2166136261u` |
| prime | `16777619u` |
| case handling | ASCII `A-Z` folds to lowercase |
| null/empty | returns `0` |
| computed zero | remapped to `1` |

Item, creature, biome, recipe, and many section ids use the same 32-bit FNV-1a contract.

## Cross-Reference Gate

`H8DataMonolithCompiler` validates authored source rows before blob output. The gate rejects item
references that cannot resolve to a baked `Items` hash in these fields:

- item authoring: `recipe`
- recipe authoring: `output`, `output_id`, `ingredients`, `recipe`
- loot authoring: `item_id`, `item`
- economy authoring: `item_id`, `item`, `output_id`, `output`, `recipe_output_id`,
  `recipe_output`, `ingredients`, `ingredient_ids`, `recipe`, `recipe_items`

The validator uses raw CSV rows and synthetic JSON provenance rows instead of post-sort binary
records, because sorted `Recipes` and rebuilt `LootCdf` records no longer preserve source location.
Failure messages must include source file, CSV line or JSON source index, owner, field, packed token
index when applicable, authored value, and computed FNV-1a hash.

## Runtime Consumer Bridges

Static-data consumers must read the resident monolith through `H8StaticDataArena` section spans or
owner-provided helper methods. `ScavengingLootOracle` is currently compiled in Core and therefore
imports `Hecton8.Data` directly only as a monolith consumer bridge: it copies the first contiguous
`LootCdf` table from `ReadOnlySpan<H8LootCdfRecord>` into its Vault-owned `LootTableEntryDTO`
buffer. Production player builds do not synthesize the emergency loot table when the monolith has no
loot rows; editor/self-audit paths may still schedule the deterministic emergency mock for tooling.
The Scavenging editor/manual loot CSV self-audit reads selected CSV files through `FileStream` into a
Temp `NativeArray<byte>` and then uses `TryIngestLootDistributionCsvBytes`; it must not use
`File.ReadAllBytes` or managed `byte[]` staging as a static-data bridge.

## Assembly Boundary Truth

Static source truth as of 2026-05-19 SHINOBU_103: Data Monolith runtime files are still compiled by
`Hecton8.Core.csproj` from `Assets/_Project/Scripts/Data/Monolith`. There is no dedicated
`Hecton8.Data.Runtime.asmdef` yet. Creating one requires a planned bootstrap boundary migration,
because `GameBootstrapper` in Core currently calls `H8StaticDataArena`, while the arena depends on
Core Vault and fatal-boot contracts. Do not claim compile-wall isolation for Data Monolith until that
route is split through contracts or a boot-owned facade.

Editor source truth as of 2026-05-19 SHINOBU_103: Data Monolith editor tooling is scoped by
`Assets/_Project/Scripts/Editor/DataMonolith/Hecton8.DataMonolith.Editor.asmdef`. It references
`Hecton8.Core`, `Unity.Burst`, `Unity.Collections`, and `Unity.Mathematics` only, includes Editor
platforms only, and has stable `.meta` GUIDs for the asmdef and compiler window. Unity import/project
regeneration proof is still pending. The compiler window binary inspector must call the same
`H8DataMonolithCompiler.TryValidateOutputBlob` gate used by the player-build preprocessor before it
prints ad hoc header/checksum/section diagnostics; the inspector is not allowed to maintain a looser
validation contract than release builds. Inspector validation must be non-destructive to the baker's
stored `LastError`, so cross-reference and CSV validation failures remain visible after UI refresh.
The primary `BAKE MONOLITH` facade command is intentionally larger than the secondary toolbar actions
(`260 x 42`, bold) because it is the human-owned route into the authoritative binary artifact.
Automated editor bakes must route through `H8DataMonolithFileSystemWatcher.RequestBake()`, not direct
`BakeAll()` calls from import callbacks. The scheduler debounces source changes for 0.75 seconds,
skips while Unity is compiling, and blocks overlapping bakes with an interlocked in-progress flag.
Play-mode hot reload for a successful local bake must queue the canonical `static_data.h8bin` path
directly on the editor main-thread drain path. The loopback socket remains only as an external bridge:
packets are capped at 1024 characters, must target the exact canonical output path, and the listener is
stopped on play-mode exit, assembly reload, and editor quit.

## I/O Truth

Source truth as of 2026-05-19 SHINOBU_103:

- runtime monolith load: `H8StaticDataArena` requests `GlobalDataVault` BufferID `71103`, stages non-filesystem StreamingAssets URIs such as Android/Quest `jar:` paths into `Application.temporaryCachePath`, attempts MMF on desktop filesystem paths, and falls back to direct `FileStream.Read(Span<byte>)` into Vault-owned bytes on hostile platforms.
- runtime no-vault behavior: allocation fails closed; `H8StaticDataArena` does not allocate a private persistent `NativeArray<byte>` fallback.
- editor bake: `H8DataMonolithCompiler` uses editor-only `MemoryStream`, reads CSV sources through a bounded worker pool capped by CPU capacity, writes `static_data.h8bin.tmp`, validates the temp file, promotes it atomically, then validates the production blob.
- editor auto-bake: AssetPostprocessor and filesystem change notifications enqueue one debounced bake route instead of baking synchronously during Unity import or on every file-write event.
- checksum: runtime recomputes XXHash3-64 over bytes `[16..blobLength)` before setting Ready.
- runtime directory gate: `H8StaticDataArena` uses `H8DataLayoutAudit.GetExpectedRecordSize` to reject wrong section count, section order, record stride, nonzero empty offsets, data-start mismatch, unaligned section starts, and localization directory/table drift before setting Ready.

Do not document the Data Monolith as Unity runtime/profiler/player-build proven until a guarded Unity import, bake, boot, and GC/profiler pass produces fresh artifacts.

STATUS: STATIC_SOURCE REVIEWED / RUNTIME PENDING
