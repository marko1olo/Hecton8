# HECTON-8 â€” AUP SURGERY BYTE MAP
Date: 2026-05-07
Status: PENDING VERIFICATION

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

**Status:** HISTORICAL STATIC SURGERY MAP / PENDING REVERIFICATION
**Target:** `AbsoluteUniversePosition` layout mutation (int64Ã—3 + float3 â†’ TBD)
**Risk:** CRITICAL â€” breaks binary save compatibility, native container layouts, and payload prefix offsets.
**Author:** Autonomous Crusade / Pre-Surgery Mapping
**Date:** 2026-04-28

---

## 1. CURRENT LAYOUT (v7 Baseline)

### `AbsoluteUniversePosition` â€” 36 bytes
```csharp
[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 36)]
internal struct AbsoluteUniversePosition
{
    public long  GridX;   // bytes 0-7   (int64)
    public long  GridY;   // bytes 8-15  (int64)
    public long  GridZ;   // bytes 16-23 (int64)
    public float LocalX;  // bytes 24-27 (float32)
    public float LocalY;  // bytes 28-31 (float32)
    public float LocalZ;  // bytes 32-35 (float32)
}
```

### `AbsoluteUniversePositionBlit128` â€” 48 bytes
```csharp
[StructLayout(LayoutKind.Sequential, Pack = 16, Size = 48)]
internal struct AbsoluteUniversePositionBlit128
{
    public long   GridX;    // bytes 0-7   (int64)
    public long   GridY;    // bytes 8-15  (int64)
    public long   GridZ;    // bytes 16-23 (int64)
    public float4 Local;    // bytes 32-47 (16-byte aligned)
    public ulong  Reserved; // DECLARED but EXCEEDS Size=48 â€” UNREACHABLE MEMORY
}
```
> âš ï¸ **BUG:** `Reserved` field in `AbsoluteUniversePositionBlit128` is declared after a 48-byte boundary but `Size=48`. Any write to `Reserved` is a silent 8-byte buffer overflow. Fix BEFORE surgery.

---

## 2. STRUCTURAL CORRUPTION MAP

### 2.1 `PersistentWorldItemRecord` â€” Runtime NativeList (192 bytes)
| Field | Offset (v7) | Offset if AUPâ†’48B | Î” |
|---|---|---|---|
| `Position` (AUP) | 0 | 0 | â€” |
| `ChunkId` (int3) | 36 | 48 | **+12** |
| `ItemPersistentIdHash` (ulong) | 48 | 60 | **+12** |
| `ItemPersistentId` (FixedString128) | 56 | 68 | **+12** |
| `_packedQuantityAndFlags` (uint) | 184 | 196 | **+12** |
| `InstanceUid` (uint) | 188 | 200 | **+12** |
| **Total Size** | **192** | **204** | **+12** |

> **Impact:** `_records` NativeList, `_entityStateByInstanceUid` EntityDataRecord mapping, and all chunk-hash lookups become misaligned. **Native container rebuild required.**

### 2.2 `EntityDataRecord` â€” Runtime NativeHashMap payload (64 bytes)
| Field | Offset (v7) | Offset if Blit128â†’64B | Î” |
|---|---|---|---|
| `Position` (Blit128) | 0 | 0 | â€” |
| `Quantity` (int) | 48 | 64 | **+16** |
| `Integrity01` (float) | 52 | 68 | **+16** |
| `InventoryHash` (int) | 56 | 72 | **+16** |
| `InstanceUid` (uint) | 60 | 76 | **+16** |
| **Total Size** | **64** | **80** | **+16** |

> **Impact:** `_entityStateByInstanceUid` value stride changes. Old saves loaded into new runtime = **structural memory corruption**.

### 2.3 `PayloadPrefix` â€” Save File Binary Header (60 bytes)
| Field | Offset (v7) | Offset if AUPâ†’48B | Î” |
|---|---|---|---|
| `TimestampUnixMs` (ulong) | 0 | 0 | â€” |
| `PlayTimeSeconds` (float) | 8 | 8 | â€” |
| `PlayerPosition` (AUP) | 12 | 12 | â€” |
| `SaveDataVersion` (int) | 48 | 60 | **+12** |
| `SaveDataByteLength` (uint) | 52 | 64 | **+12** |
| `SceneNameByteLength` (ushort) | 56 | 68 | **+12** |
| `GameVersionByteLength` (ushort) | 58 | 70 | **+12** |
| **Total Size** | **60** | **72** | **+12** |

> **Impact:** `TryReadMetadata` and `TryLoadSaveData` read garbage string lengths â†’ **save file parse failure** or **out-of-bounds string read**.

### 2.4 `PersistentWorldDeltaRecord` â€” Save/Runtime (32 bytes)
**DOES NOT EMBED `AbsoluteUniversePosition` DIRECTLY.** Byte offsets are **structurally preserved**.

| Field | Offset | Size | AUP-Surgery Safe? |
|---|---|---|---|
| `ChunkId` | 0 | 12 | âœ… Structurally safe |
| `ItemPersistentIdHash` | 12 | 8 | âœ… Structurally safe |
| `InstanceUid` | 20 | 4 | âœ… Structurally safe |
| `PackedLocalPosition` | 24 | 4 | âš ï¸ **SEMANTICALLY CORRUPTED** |
| `Quantity` | 28 | 2 | âœ… Structurally safe |
| `ItemFlags` | 30 | 1 | âœ… Structurally safe |
| `Reserved` | 31 | 1 | âœ… Structurally safe |

> **Semantic Corruption:** `PackLocalPosition()` and `UnpackPosition()` call `AbsoluteUniversePosition.ToAbsoluteDouble3()` and `FromAbsolutePosition()`. If AUP coordinate math changes (e.g., `CellSizeMeters` changes, grid origin shifts, or local encoding changes), every `PackedLocalPosition` in every save file decodes to **wrong world coordinates**.

### 2.5 `PersistentWorldSaveRecord16` â€” On-Disk v5+ Format (16 bytes)
**NOT AFFECTED structurally** â€” contains no AUP. However, `ChunkIndex` and `ItemHashIndex` resolve through lookup tables built at save-time from `PersistentWorldDeltaRecord` data. If semantic corruption occurs in delta records, the lookup tables are meaningless.

---

## 3. SAVE FILE MIGRATION â€” v6 â†’ v8

### 3.1 Version Bump Rule
- `CurrentVersion` in `SaveBinaryStorage` must advance to `0x0008`.
- `MinimumSupportedVersion` remains `0x0003` (migration path: 3â†’4â†’5â†’6â†’7â†’8).
- New compat mask bit: `FlagAupV8 = 0x04` (optional, for forward compatibility checks).

### 3.2 Migration Code
```csharp
// ============================================================================
// FILE: SaveDataMigration_AupV8.cs
// LOCATION: Assets/_Project/Scripts/SaveSystem/
// MANDATE: AGENTS.md â€” Zero GC, Native-only, no managed allocs in hot path.
// ============================================================================
using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Hecton8.World;

namespace Hecton8.SaveSystem
{
    internal static unsafe class SaveDataMigration_AupV8
    {
        internal const ushort AupV8Version = 0x0008;
        internal const byte FlagAupV8 = 0x04;

        // ----------------------------------------------------------------------
        // OLD v6/v7 LAYOUT CONSTANTS (frozen â€” do NOT change after deploy)
        // ----------------------------------------------------------------------
        private const int OldAupSize = 36;
        private const int OldPayloadPrefixSize = 60;
        private const int OldSaveDataVersionOffset = 48;
        private const int OldSaveDataByteLengthOffset = 52;
        private const int OldSceneNameByteLengthOffset = 56;
        private const int OldGameVersionByteLengthOffset = 58;

        // ----------------------------------------------------------------------
        // NEW v8 LAYOUT CONSTANTS (example: AUP expanded to 48 bytes)
        // ----------------------------------------------------------------------
        private const int NewAupSize = 48; // CTO-defined new size
        private const int NewPayloadPrefixSize = 72;
        private const int NewSaveDataVersionOffset = 60;
        private const int NewSaveDataByteLengthOffset = 64;
        private const int NewSceneNameByteLengthOffset = 68;
        private const int NewGameVersionByteLengthOffset = 70;

        /// <summary>
        /// Migrates a decompressed raw payload from v6/v7 to v8 in-place or into a new buffer.
        /// CALLER owns buffer allocation/disposal.
        /// </summary>
        internal static bool TryMigratePayloadToV8(
            byte* sourcePtr,
            int sourceLength,
            byte* destinationPtr,
            int destinationCapacity,
            out int migratedLength,
            out string error)
        {
            migratedLength = 0;
            error = string.Empty;

            if (sourcePtr == null || destinationPtr == null || sourceLength < OldPayloadPrefixSize)
            {
                error = "Migration source payload is null or truncated.";
                return false;
            }

            if (destinationCapacity < sourceLength + (NewAupSize - OldAupSize))
            {
                error = "Migration destination buffer too small for expanded AUP prefix.";
                return false;
            }

            // --------------------------------------------------------------------
            // STEP 1: Parse old PayloadPrefix
            // --------------------------------------------------------------------
            ulong timestampUnixMs = *(ulong*)(sourcePtr + 0);
            float playTimeSeconds = *(float*)(sourcePtr + 8);

            // Read old AUP (36 bytes) and convert coordinate space to v8
            AbsoluteUniversePosition oldPlayerAup = ReadAupV7(sourcePtr + 12);
            AbsoluteUniversePosition newPlayerAup = ConvertAupV7ToV8(oldPlayerAup);

            int oldSaveDataVersion = *(int*)(sourcePtr + OldSaveDataVersionOffset);
            uint oldSaveDataByteLength = *(uint*)(sourcePtr + OldSaveDataByteLengthOffset);
            ushort oldSceneNameByteLength = *(ushort*)(sourcePtr + OldSceneNameByteLengthOffset);
            ushort oldGameVersionByteLength = *(ushort*)(sourcePtr + OldGameVersionByteLengthOffset);

            // --------------------------------------------------------------------
            // STEP 2: Write new PayloadPrefix with expanded AUP
            // --------------------------------------------------------------------
            UnsafeUtility.MemClear(destinationPtr, destinationCapacity);
            *(ulong*)(destinationPtr + 0) = timestampUnixMs;
            *(float*)(destinationPtr + 8) = playTimeSeconds;
            WriteAupV8(destinationPtr + 12, newPlayerAup);
            *(int*)(destinationPtr + NewSaveDataVersionOffset) = oldSaveDataVersion;
            *(uint*)(destinationPtr + NewSaveDataByteLengthOffset) = oldSaveDataByteLength;
            *(ushort*)(destinationPtr + NewSceneNameByteLengthOffset) = oldSceneNameByteLength;
            *(ushort*)(destinationPtr + NewGameVersionByteLengthOffset) = oldGameVersionByteLength;

            int newCursor = NewPayloadPrefixSize;
            int oldCursor = OldPayloadPrefixSize;

            // --------------------------------------------------------------------
            // STEP 3: Copy UTF-16 metadata strings (SceneName, GameVersion)
            // --------------------------------------------------------------------
            int metadataBytes = oldSceneNameByteLength + oldGameVersionByteLength;
            if (oldCursor + metadataBytes > sourceLength)
            {
                error = "Old payload metadata strings exceed source bounds.";
                return false;
            }
            UnsafeUtility.MemCpy(destinationPtr + newCursor, sourcePtr + oldCursor, metadataBytes);
            newCursor += metadataBytes;
            oldCursor += metadataBytes;

            // --------------------------------------------------------------------
            // STEP 4: Copy SaveData blob (opaque to this migration)
            // --------------------------------------------------------------------
            int saveDataLength = (int)oldSaveDataByteLength;
            if (oldCursor + saveDataLength > sourceLength)
            {
                error = "Old payload SaveData blob exceeds source bounds.";
                return false;
            }
            UnsafeUtility.MemCpy(destinationPtr + newCursor, sourcePtr + oldCursor, saveDataLength);
            newCursor += saveDataLength;
            oldCursor += saveDataLength;

            // --------------------------------------------------------------------
            // STEP 5: Migrate PackedQuestState (opaque copy, no AUP dependency)
            // --------------------------------------------------------------------
            int questSectionLength = ComputeQuestSectionLength(sourcePtr, oldCursor, sourceLength);
            if (questSectionLength < 0)
            {
                error = "Old packed quest-state section is invalid.";
                return false;
            }
            if (questSectionLength > 0)
            {
                UnsafeUtility.MemCpy(destinationPtr + newCursor, sourcePtr + oldCursor, questSectionLength);
                newCursor += questSectionLength;
                oldCursor += questSectionLength;
            }

            // --------------------------------------------------------------------
            // STEP 6: Migrate PersistentWorldDeltas (SEMANTIC RE-ENCODE)
            // --------------------------------------------------------------------
            if (!TryMigratePersistentWorldSection(
                    sourcePtr,
                    sourceLength,
                    oldCursor,
                    destinationPtr,
                    destinationCapacity,
                    ref newCursor,
                    out error))
            {
                return false;
            }

            // --------------------------------------------------------------------
            // STEP 7: Migrate EcosystemSection + VoxelDelta (opaque copy)
            // --------------------------------------------------------------------
            int trailingBytes = sourceLength - oldCursor; // approximate; exact parsing preferred
            if (trailingBytes > 0)
            {
                if (newCursor + trailingBytes > destinationCapacity)
                {
                    error = "Destination capacity exceeded during trailing section copy.";
                    return false;
                }
                UnsafeUtility.MemCpy(destinationPtr + newCursor, sourcePtr + oldCursor, trailingBytes);
                newCursor += trailingBytes;
            }

            migratedLength = newCursor;
            return true;
        }

        // ----------------------------------------------------------------------
        // OLD v7 AUP DESERIALIZATION (frozen layout)
        // ----------------------------------------------------------------------
        private static AbsoluteUniversePosition ReadAupV7(byte* ptr)
        {
            return new AbsoluteUniversePosition
            {
                GridX = *(long*)(ptr + 0),
                GridY = *(long*)(ptr + 8),
                GridZ = *(long*)(ptr + 16),
                LocalX = *(float*)(ptr + 24),
                LocalY = *(float*)(ptr + 28),
                LocalZ = *(float*)(ptr + 32)
            };
        }

        // ----------------------------------------------------------------------
        // NEW v8 AUP SERIALIZATION (CTO-defined layout)
        // ----------------------------------------------------------------------
        private static void WriteAupV8(byte* ptr, AbsoluteUniversePosition aup)
        {
            // Example: if v8 AUP is 48 bytes (padded float4 local)
            *(long*)(ptr + 0) = aup.GridX;
            *(long*)(ptr + 8) = aup.GridY;
            *(long*)(ptr + 16) = aup.GridZ;
            *(float*)(ptr + 24) = aup.LocalX;
            *(float*)(ptr + 28) = aup.LocalY;
            *(float*)(ptr + 32) = aup.LocalZ;
            // bytes 36-47: reserved / padding â€” zero-filled by MemClear
        }

        // ----------------------------------------------------------------------
        // COORDINATE SPACE CONVERSION (semantic migration)
        // If CellSizeMeters or grid origin changes, implement here.
        // ----------------------------------------------------------------------
        private static AbsoluteUniversePosition ConvertAupV7ToV8(AbsoluteUniversePosition oldAup)
        {
            // CURRENT: identity mapping â€” coordinates are preserved.
            // If CTO changes CellSizeMeters, replace with:
            //   double3 absolute = OldAupMath.ToAbsoluteDouble3(oldAup);
            //   return NewAupMath.FromAbsolutePosition(absolute);
            return oldAup;
        }

        // ----------------------------------------------------------------------
        // QUEST SECTION LENGTH RESOLUTION (helper)
        // ----------------------------------------------------------------------
        private static int ComputeQuestSectionLength(byte* rawPtr, int cursor, int sourceLength)
        {
            // Requires header context; stub for brevity.
            // In production: read header.DeltaCount and compute:
            //   PackedQuestStateSectionHeaderSize + (DeltaCount * sizeof(uint))
            return 0; // PENDING: wire into SaveBinaryStorage header parser
        }

        // ----------------------------------------------------------------------
        // PERSISTENT WORLD SECTION MIGRATION
        // ----------------------------------------------------------------------
        private static bool TryMigratePersistentWorldSection(
            byte* sourcePtr,
            int sourceLength,
            int oldSectionOffset,
            byte* destPtr,
            int destCapacity,
            ref int destCursor,
            out string error)
        {
            error = string.Empty;
            // PENDING: implement full v5 section parser â†’ re-encode with new
            // AbsoluteUniversePosition math if coordinate space changed.
            // If only struct size changed (no semantic coordinate change),
            // this section can be copied opaque because PersistentWorldSaveRecord16
            // does not embed AUP.
            return true;
        }
    }
}
```

### 3.3 Migration Checklist (Pre-Flight)
- [ ] Freeze `OldAupSize = 36` and `OldPayloadPrefixSize = 60` as `const` â€” never change after v8 deploy.
- [ ] Fix `AbsoluteUniversePositionBlit128.Reserved` overflow (Size must be â‰¥56 if Reserved is used).
- [ ] Update `SaveBinaryStorage.CurrentVersion` to `0x0008`.
- [ ] Update `SaveBinaryStorage.CurrentHeaderSize` if header struct changes (currently 52).
- [ ] Add `FlagAupV8` to header `Flags` field on write.
- [ ] Run `TryMigratePayloadToV8` on ALL `.sav` files in `slot_0/1/2` during first v8 boot.
- [ ] After migration: verify payload hash recomputation (`Hash64` over new raw payload).
- [ ] Regression test: load v7 save â†’ migrate â†’ save as v8 â†’ load v8 â†’ compare `PlayerPosition` double3 equality.

---

## 4. ATLAS SUMMARY

| Struct | Contains AUP? | Corrupted Offsets | Corruption Type |
|---|---|---|---|
| `PayloadPrefix` | âœ… @+12 | `SaveDataVersion` (48â†’60), `SaveDataByteLength` (52â†’64), `SceneNameByteLength` (56â†’68), `GameVersionByteLength` (58â†’70) | **Binary shift** â€” parse failure |
| `PersistentWorldItemRecord` | âœ… @+0 | `ChunkId` (+12), `ItemPersistentIdHash` (+12), `ItemPersistentId` (+12), `_packedQuantityAndFlags` (+12), `InstanceUid` (+12) | **NativeList misalignment** â€” memory corruption |
| `EntityDataRecord` | âœ… (Blit128) @+0 | `Quantity` (+16), `Integrity01` (+16), `InventoryHash` (+16), `InstanceUid` (+16) | **NativeHashMap value stride mismatch** |
| `PersistentWorldDeltaRecord` | âŒ No embedded AUP | None structurally | **Semantic** â€” `PackedLocalPosition` decode failure if coordinate math changes |
| `PersistentWorldSaveRecord16` | âŒ No embedded AUP | None | **Indirect** â€” lookup tables built from semantically corrupted deltas |
| `PoolSlotData` | âŒ No embedded AUP | None | âœ… Safe (uses `int3` + `float3` local, not AUP) |
| `AbsoluteUniversePositionBlit128` | Self | `Reserved` field overflows Size=48 | **Silent 8-byte overflow** â€” fix first |

---

**STATUS:** HISTORICAL STATIC SURGERY MAP / PENDING REVERIFICATION
**NEXT ACTION:** CTO approval on new AUP byte size â†’ freeze migration constants â†’ execute.
