# CORE_REPLAY Determinism

Date: 2026-05-12
Status: STATIC_SOURCE REVIEWED / DEVELOPMENT-ONLY RUNTIME PENDING

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.

R45 root/architecture R43/R44 residue/proof-artifact/source-counter correction (`Docs/Reports/2026-05-20_DOCUMENTATION_R45_ROOT_ARCHITECTURE_R43_R44_RESIDUE_PROOF_ARTIFACTS_AND_COUNTERS_LOCAL.md`) is the latest local static DOC_GLOBAL boundary for architecture/root documentation. R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; R42 remains the prior counter/route-boundary/proof-label correction; R41 remains the prior global-authority/internal-residue correction; R40 remains the prior R38-residue/source-counter correction; R39 remains the prior authority-counter/proof-wording correction; R38/R37/R36/R35/R34 remain prior static correction layers. Runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## 2026-05-20 DOC_GLOBAL R45 Root/Architecture Boundary Note

R45 root/architecture R43/R44 residue/proof-artifact/source-counter correction (`Docs/Reports/2026-05-20_DOCUMENTATION_R45_ROOT_ARCHITECTURE_R43_R44_RESIDUE_PROOF_ARTIFACTS_AND_COUNTERS_LOCAL.md`) (R44 prior internal-residue/exact-route-field/proof-wording correction) keeps this file as static/development-only replay determinism orientation, not full deterministic replay proof. Current DOC_GLOBAL boundary is `Docs/Reports/2026-05-20_DOCUMENTATION_R45_ROOT_ARCHITECTURE_R43_R44_RESIDUE_PROOF_ARTIFACTS_AND_COUNTERS_LOCAL.md`; R44 remains prior at `Docs/Reports/2026-05-20_DOCUMENTATION_R44_ROOT_ARCHITECTURE_INTERNAL_RESIDUE_EXACT_ROUTE_FIELDS_LOCAL.md`; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; R42 remains the prior counter/route-boundary/proof-label correction; R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers. Current static gates: `Tools/AtlasCheck.py` remains red on `ATLAS_CHECK_FAIL references=6741 missing=59` (one Dynamic Decals missing vendor asset ref, RealtimeCSG vendor icon/readme image refs, and missing HabitatDamageBakePipeline source ref in the current atlas); `Docs/Modding/Validate_Mod_API_Static.ps1` passes (`Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`) as static-tool orientation only. Runtime proof remains absent.

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
