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

R32 architecture R4/proof-wording correction is the latest artifact-backed local static DOC_GLOBAL boundary for architecture/root documentation. R31 remains the prior current-boundary propagation layer, R30 remains the prior internal-currentness layer, R29 remains the prior stale-gate/global-authority layer, R28 remains the prior interior-boundary layer, and R27 remains the latest source-counter/index snapshot until rerun.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## 2026-05-19 DOC_GLOBAL R32 Current Boundary Note

R32 artifact-backed reread evidence keeps this file as a static DataMonolith binary-spec contract, not product payload existence, runtime I/O, or platform-storage proof. Current root/architecture boundary is `Docs/Reports/2026-05-19_DOCUMENTATION_R32_ARCHITECTURE_R4_AND_PROOF_WORDING_LOCAL.md`; R31 remains the prior current-boundary propagation correction. R30 remains the prior internal-currentness correction, R29 remains the prior stale-gate/global-authority correction, R28 remains the prior interior-boundary correction, and R27 source counters are retained until a newer counter pass reruns them. Current static gates: `Tools/AtlasCheck.py` remains red on `59` missing refs (RealtimeCSG vendor refs plus absent `VaultXRayWindow.cs` and `HectonMapMagicVegetationBridgeFloraCollisionProxies.cs`); `Docs/Modding/Validate_Mod_API_Static.ps1` now passes (`Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`) as static-tool orientation only; do not treat PASS as current proof without artifact path, command, timestamp, environment, and output. Unity/runtime/profiler/player-build proof remains absent.

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

## I/O Truth

Source truth as of 2026-05-19 SHINOBU_103:

- runtime monolith load: `H8StaticDataArena` requests `GlobalDataVault` BufferID `71103`, attempts MMF on desktop, and falls back to direct `FileStream.Read(Span<byte>)` into Vault-owned bytes on hostile platforms.
- runtime no-vault behavior: allocation fails closed; `H8StaticDataArena` does not allocate a private persistent `NativeArray<byte>` fallback.
- editor bake: `H8DataMonolithCompiler` uses editor-only `MemoryStream` and `File.WriteAllBytes`
- checksum: runtime recomputes XXHash3-64 over bytes `[16..blobLength)` before setting Ready.

Do not document the Data Monolith as Unity runtime/profiler/player-build proven until a guarded Unity import, bake, boot, and GC/profiler pass produces fresh artifacts.

STATUS: STATIC_SOURCE REVIEWED / RUNTIME PENDING
