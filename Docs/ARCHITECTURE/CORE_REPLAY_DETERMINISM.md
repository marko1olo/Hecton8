# CORE_REPLAY Determinism

Date: 2026-05-12
Status: STATIC_SOURCE REVIEWED / DEVELOPMENT-ONLY RUNTIME PENDING

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-21 R51 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, shader import, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
Current DOC_GLOBAL boundary (2026-05-21 R51): `Docs/Reports/2026-05-21_DOCUMENTATION_R51_ROOT_ARCHITECTURE_ENCODING_BOUNDARY_READORDER_AND_ROUTE_GAPS_LOCAL.md` is the latest local static root/architecture encoding repair, boundary-gap, read-order, route-card/static-contract, and source/AtlasCheck orientation correction. R50 remains the prior generated-atlas regeneration, stale R48 interior-boundary, dump-target wording, and source-counter drift correction. R49 remains the prior AtlasCheck-red-state/boundary-gap/route-field/source-counter correction. R48 remains the prior date-rollover/AtlasCheck/source-counter correction. R47 remains the prior authority-spine/runtime-wording/counter-drift correction. R46 remains the prior interior-authority/route-field/proof-language correction. R45/R44/R43/R42/R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers. Current AtlasCheck remains red until `Tools/AtlasCheck.py` exits `0`; runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## 2026-05-20 DOC_GLOBAL R46 Root/Architecture Boundary Note

R51 root/architecture encoding/boundary/read-order/route-card/source-counter correction (`Docs/Reports/2026-05-21_DOCUMENTATION_R51_ROOT_ARCHITECTURE_ENCODING_BOUNDARY_READORDER_AND_ROUTE_GAPS_LOCAL.md`) keeps this file as a static architecture/source contract, not runtime proof. Current DOC_GLOBAL boundary is `Docs/Reports/2026-05-21_DOCUMENTATION_R51_ROOT_ARCHITECTURE_ENCODING_BOUNDARY_READORDER_AND_ROUTE_GAPS_LOCAL.md`; R50 remains prior at `Docs/Reports/2026-05-21_DOCUMENTATION_R50_ROOT_ARCHITECTURE_ATLAS_REGEN_R48_INTERIOR_DUMPTARGET_AND_COUNTER_DRIFT_LOCAL.md`; R49 remains prior at `Docs/Reports/2026-05-21_DOCUMENTATION_R49_ROOT_ARCHITECTURE_ATLASCHECK_BOUNDARY_ROUTE_FIELDS_AND_COUNTER_DRIFT_LOCAL.md`; R48 remains prior at `Docs/Reports/2026-05-21_DOCUMENTATION_R48_ROOT_ARCHITECTURE_DATE_ROLLOVER_ATLASCHECK_AND_COUNTER_REFRESH_LOCAL.md`; R47 remains prior at `Docs/Reports/2026-05-20_DOCUMENTATION_R47_ROOT_ARCHITECTURE_AUTHORITY_SPINE_RUNTIME_WORDING_AND_COUNTER_DRIFT_LOCAL.md`; R46/R45/R44/R43/R42/R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers. Current static gates: `Tools/AtlasCheck.py` remains red on `ATLAS_CHECK_FAIL references=6881 missing=60` (one Dynamic Decals vendor asset ref, RealtimeCSG vendor icon/readme image refs, and missing HectonMaskChannelPacker and HectonMaterialChannelPackValidator source refs in the current atlas); `Docs/Modding/Validate_Mod_API_Static.ps1` passes (`Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`) as static-tool orientation only. Runtime proof remains absent.

Owner Source: `Assets/_Project/Scripts/Core/DodReplayRecorder.cs`

## Boundary

`DodReplayRecorder` is compiled for editor/development builds. It is a black-box replay recorder, not a shipping gameplay dependency.

## Constants

| Constant | Value |
|---|---:|
| `SnapshotIntervalFrames` | 10 |
| `MaxSnapshotSources` | 1024 |
| `SnapshotScratchBytes` | 2,097,152 |
| `ReplayFileWriteScratchBytes` | 65,536 |
| `InputJournalCapacity` | 512 |
| `SidecarCapacity` | 256 |
| `GhostCapacity` | 128 |
| `PanicPayloadCapacity` | 64 |
| `PanicPayloadStrideBytes` | 256 |
| `AupTrackedSubjectCapacity` | 256 |
| `AupDriftWindowFrames` | 1000 |
| `HeaderSizeBytes` | 128 |
| `SegmentHeaderSizeBytes` | 64 |
| `ReplayVersion` | 2 |
| `ReplayFileCapacityBytes` | 523,239,424 |
| `ReplayMagic` | `0x48385245504C4159` |
| FNV64 offset | `14695981039346656037` |
| FNV64 prime | `1099511628211` |

## Snapshot Layout

Each snapshot starts with `DodReplaySnapshotHeader` at 128 bytes. Segment records use 64-byte headers. Several sidecar records are fixed 32-byte structs.

Segment payload logic:

1. collect source memory snapshots
2. compute FNV64 over source bytes
3. suppress unchanged payloads by comparing previous/current hash
4. write a segment header
5. copy changed bytes into the snapshot scratch page

The source copy path uses guarded memory copy. Rejected copies are reported to `UnsafeMemoryCopyGuard`.

## Circular File

Replay storage is a fixed-size circular file:

```text
if writeOffset + byteCount > ReplayFileCapacityBytes:
    writeOffset = 0
```

The file is opened with `FileStream(FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.RandomAccess)`.

## Writer Thread

The recorder uses:

- `Thread` named `H8.DODReplayWriter`
- `AutoResetEvent` signal
- `_writerGate` monitor
- 64 KB managed file write staging buffer

The managed `byte[]` staging is cold/development-only. It is not allowed in shipping hot paths.

## Captured Surfaces

The recorder has lanes for:

- input journal
- job profiles
- Burst panic records
- AUP drift records
- entity ghost breadcrumbs
- logistics flows
- atmosphere cells
- VRAM allocations
- physics smoke records

## Determinism Use

Use replay data to answer:

- which frame changed state
- which owner hash changed bytes
- whether AUP drift exceeded threshold
- whether job timings changed around a fault
- whether input sequence diverged

Do not use this as proof of full determinism without replay compare tooling and a passing run artifact.

STATUS: STATIC_SOURCE REVIEWED / DEVELOPMENT-ONLY RUNTIME PENDING
