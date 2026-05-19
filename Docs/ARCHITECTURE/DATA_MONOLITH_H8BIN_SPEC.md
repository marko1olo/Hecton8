# Data Monolith H8BIN Spec

Date: 2026-05-12
Status: STATIC_SOURCE REVIEWED / RUNTIME PENDING

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## 2026-05-19 DOC_GLOBAL R31 Current Boundary Note

R31 reread confirmed this file remains a static DataMonolith binary-spec contract, not product payload existence, runtime I/O, or platform-storage proof. Current root/architecture boundary is `Docs/Reports/2026-05-19_DOCUMENTATION_R31_ARCHITECTURE_CURRENT_BOUNDARY_PROPAGATION_LOCAL.md`; R30 remains the prior internal-currentness correction, R29 remains the prior stale-gate/global-authority correction, R28 remains the prior interior-boundary correction, and R27 source counters are retained until a newer counter pass reruns them. Current static gates: `Tools/AtlasCheck.py` remains red on `57` RealtimeCSG vendor references; `Docs/Modding/Validate_Mod_API_Static.ps1` now passes (`Status=PASS`, `SchemaRevision=14`, `SourceSignals=160`, `ModCommandSizeBytes=64`) as static-tool orientation only; do not treat PASS as current proof without artifact path, command, timestamp, environment, and output. Unity/runtime/profiler/player-build proof remains absent.

Owners: `H8DataMonolithTypes`, `H8StaticDataArena`, `H8DataHash`, `H8DataMonolithCompiler`

## Current File

| Field | Value |
|---|---|
| Extension | `.h8bin` |
| Default path | `StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` |
| runtime owner | `H8StaticDataArena` |
| editor compiler | `H8DataMonolithCompiler` |
| hash owner | `H8DataHash` |

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
| `WorldSeed` | `uint` | authored seed or 0 |
| `AppVersionHash` | `uint` | FNV-1a app version hash |
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

## Critical Record Sizes

| Record | Size |
|---|---:|
| `H8ItemRecord` | 64 |
| `H8CreatureTraitRecord` | 64 |
| `H8CreatureGenomeTraitBlock` | 32 |
| `H8BiomeRecord` | 64 |

Other monolith records are explicitly packed and sized in source. Consumers must use source constants and `UnsafeUtility.SizeOf<T>()`, not hand-written byte math.

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

## I/O Truth

Source truth as of 2026-05-12:

- runtime monolith load: `H8StaticDataArena` uses boot-only `File.ReadAllBytes` staging, then blits into persistent native memory
- editor bake: `H8DataMonolithCompiler` uses editor-only `MemoryStream` and `File.WriteAllBytes`
- save system I/O is FileStream/native-window based; Data Monolith has not yet been converted to FileStream streaming

Do not document the Data Monolith as POSIX/FileStream-complete until `H8StaticDataArena` stops using `File.ReadAllBytes`.

STATUS: STATIC_SOURCE REVIEWED / RUNTIME PENDING
