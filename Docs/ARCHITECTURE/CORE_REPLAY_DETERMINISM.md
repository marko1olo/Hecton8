# CORE_REPLAY Determinism

Date: 2026-05-12

Status: STATIC_SOURCE REVIEWED / DEVELOPMENT-ONLY RUNTIME PENDING

Owner domain: core replay determinism / development-only telemetry

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
