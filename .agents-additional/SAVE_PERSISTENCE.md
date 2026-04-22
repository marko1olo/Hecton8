# SAVE_PERSISTENCE.md — HECTON-8 Technical Mandate
## Delta-Saves | Binary Streams | Atomic Ops | Zero-GC

---

## [RULE-01] DELTA-PERSISTENCE CORE CONTRACT

Store ONLY divergence from world seed. World seed is deterministic; never re-serialize
what can be re-generated.

**Delta Identity Function:**
```
IsDirty(cell) = (cell.sdf_value != SeedSDF(cell.world_pos)) OR (cell.material != SeedMaterial(cell.world_pos))
```

**Dirty-Cell Registration (per edit):**
```
WHEN voxel_edit_occurs:
    key = PackCellKey(chunk_idx_24bit, local_cell_18bit)  → uint64
    dirty_map.TryAdd(key, new DeltaCell { sdf, material, flags })
NEVER store key if SeedSDF(pos) == new_sdf AND SeedMaterial(pos) == new_mat
```

**ChunkIndex / LocalOffset Encoding:**
```
universe_key (uint64):
  [63..40] = chunk_x (24-bit signed, world-space chunk coord)
  [39..16] = chunk_z (24-bit signed)
  [15..8 ] = chunk_y (8-bit depth layer, 0-255 → 0m to -2550m)
  [7..0  ] = local_cell (8-bit Morton-encoded within 4×4×4 sub-cell)
```

---

## [DATA-01] SAVE FILE BINARY LAYOUT — FIXED HEADER SPEC

All multi-byte fields: **Little-Endian**. No padding between header fields.

```
Offset  Size  Field
──────────────────────────────────────────────────────────────
0x00    4B    MAGIC         = 0x48454354 ('HECT')
0x04    2B    VERSION       = uint16 (current: 0x0001)
0x06    1B    COMPAT_MASK   = bitfield (see [DATA-02])
0x07    1B    FLAGS         = bitfield (compression, encryption stubs)
0x08    8B    TIMESTAMP     = uint64 Unix epoch ms
0x10    4B    DELTA_COUNT   = uint32 (count of DeltaCell records)
0x14    4B    ENTITY_COUNT  = uint32
0x18    4B    PLAYER_OFFSET = uint32 (byte offset from file start)
0x1C    4B    DELTA_OFFSET  = uint32
0x20    4B    ENTITY_OFFSET = uint32
0x24    4B    XXH32_HEADER  = checksum of bytes [0x00..0x23]
0x28    4B    XXH32_PAYLOAD = checksum of everything after header
0x2C    [N]   PAYLOAD       = LZ4-compressed blocks (see [MATH-02])
```

Total header: **44 bytes fixed**. Never changes size across versions (use COMPAT_MASK for extensions).

---

## [DATA-02] VERSION COMPAT_MASK — MIGRATION BITFIELD

```
COMPAT_MASK (uint8):
  bit 0 = SDF storage format v1 (fixed-point Q8.8)
  bit 1 = SDF storage format v2 (float32)        ← current
  bit 2 = Entity schema v1
  bit 3 = Entity schema v2                        ← current
  bit 4 = reserved
  bit 5 = reserved
  bit 6 = ENCRYPTION_STUB (future)
  bit 7 = EXPERIMENTAL (dev builds only)

Migration Logic:
  IF (file.COMPAT_MASK & SUPPORTED_MASK) != file.COMPAT_MASK:
      REJECT → fallback to .bak → log COMPAT_FAIL
  ELSE IF file.COMPAT_MASK != CURRENT_MASK:
      run FieldMigrator[old_mask → current_mask] before deserialize
```

---

## [DATA-03] DELTA CELL RECORD — FIXED-SIZE BLITTABLE STRUCT

```
DeltaCell (20 bytes, 4-byte aligned):
  uint64  universe_key       (see [RULE-01] encoding)
  float   sdf_value          (signed distance, meters, Q0 = surface)
  uint8   material_id        (0-255 material palette index)
  uint8   flags              (bit0=harvested, bit1=placed, bit2=flooded, bit3-7=reserved)
  uint16  metadata           (material-specific: ore_grade, crack_state, etc.)
```

**Blit Pattern (Zero-GC):**
```
src_ptr  = NativeArray<DeltaCell>.GetUnsafeReadOnlyPtr()
dst_ptr  = FileBuffer.GetUnsafePtr() + DELTA_OFFSET
byte_len = delta_count * sizeof(DeltaCell)   // 20 bytes * N
UnsafeUtility.MemCpy(dst_ptr, src_ptr, byte_len)
// Zero allocations. Zero boxing. Zero intermediate List<T>.
```

---

## [DATA-04] ENTITY DELTA RECORD — VARIABLE SCHEMA, FIXED FRAME

```
EntityRecord:
  uint32  entity_id          (stable GUID low-32, collision-checked on load)
  uint8   entity_type        (enum: 0=item, 1=npc, 2=installation, 3=creature)
  uint8   state_flags        (bit0=dead, bit1=looted, bit2=activated, bit3=hostile_locked)
  uint16  payload_size       (bytes of type-specific data following)
  float3  world_pos          (12B, Universe Space)
  float   rotation_y        (yaw only; pitch/roll reconstructed from NavMesh)
  uint8[] type_payload       (payload_size bytes, type-defined schema)

// Inventory items embed ItemID[uint16] + quantity[uint16] + durability[uint8] per slot
// NPC state embeds BehaviorState[uint8] + faction_id[uint8] + health[float]
```

---

## [MATH-01] CHECKSUM — XXHASH32 KERNEL

Use XXH32. Avoid MD5/CRC32 (slower). Avoid SHA (overkill, CPU-heavy on MX350 target).

**XXH32 Streaming Pseudo-kernel:**
```
CONST PRIME1 = 0x9E3779B1
CONST PRIME2 = 0x85EBCA77
CONST PRIME3 = 0xC2B2AE3D
CONST PRIME4 = 0x27D4EB2F
CONST PRIME5 = 0x165667B1

XXH32(data_ptr, len, seed=0x48454354):
    acc = seed + PRIME5
    ptr = data_ptr
    remaining = len

    WHILE remaining >= 16:
        FOR lane IN [0..3]:
            acc_lane[lane] = ROTL32(acc_lane[lane] + READ_LE_U32(ptr) * PRIME2, 13) * PRIME1
            ptr += 4
        remaining -= 16

    acc = ROTL32(acc_lane[0],1) + ROTL32(acc_lane[1],7) +
          ROTL32(acc_lane[2],12)+ ROTL32(acc_lane[3],18)
    acc += len

    WHILE remaining >= 4:
        acc = ROTL32(acc + READ_LE_U32(ptr) * PRIME3, 17) * PRIME4
        ptr += 4; remaining -= 4

    WHILE remaining > 0:
        acc = ROTL32(acc + READ_U8(ptr) * PRIME5, 11) * PRIME1
        ptr += 1; remaining -= 1

    // Avalanche
    acc ^= acc >> 15; acc *= PRIME2
    acc ^= acc >> 13; acc *= PRIME3
    acc ^= acc >> 16
    RETURN acc
```

Compute separately: `XXH32_HEADER` over bytes [0x00..0x23], `XXH32_PAYLOAD` over raw pre-compression payload.
Store both in header before LZ4 pass. Verify both on load before decompression.

---

## [MATH-02] LZ4 BLOCK COMPRESSION STRATEGY

**FORBID:** LZMA, Brotli, Deflate. All cause >10ms CPU stalls on i3-10gen single-core spikes.
**USE:** LZ4 Block format (not LZ4 Frame). Manual block chunking for streaming.

```
BLOCK_SIZE = 256KB  // sweet spot: L2 cache friendly on mobile/low-end

CompressPayload(raw_ptr, raw_len):
    block_count = CEIL(raw_len / BLOCK_SIZE)
    FOR i IN [0..block_count):
        src      = raw_ptr + i * BLOCK_SIZE
        src_len  = MIN(BLOCK_SIZE, raw_len - i * BLOCK_SIZE)
        dst_max  = LZ4_COMPRESSBOUND(src_len)  // = src_len + src_len/255 + 16
        dst      = scratch_buffer[i]            // pre-allocated NativeArray
        compressed_len = LZ4_compress_default(src, dst, src_len, dst_max)
        WriteBlockHeader(compressed_len, src_len)  // 8B: [comp_size:4][orig_size:4]
        WriteBlockData(dst, compressed_len)

// Expected ratio on voxel SDF deltas: 3:1 to 5:1
// 64MB raw delta budget → ~13-21MB on disk
```

---

## [FLOW-01] ATOMIC SAVE PIPELINE — ZERO DATA-LOSS PROTOCOL

```
THREAD: BackgroundWorker (never Main Thread)

STEP 1 — SNAPSHOT CAPTURE (max 50ms window):
    LOCK dirty_map (brief spinlock, <1ms)
    snapshot = dirty_map.Clone() into pre-allocated NativeArray<DeltaCell>
    entity_snapshot = world.entity_registry.SnapshotDirty()
    UNLOCK dirty_map
    // Main thread unblocked. Background continues.

STEP 2 — SERIALIZE TO TEMP BUFFER:
    buffer = global_save_buffer  // 64MB ceiling NativeArray<byte>, pre-allocated at startup
    WriteHeader(buffer, snapshot.Length, entity_snapshot.Length)
    UnsafeUtility.MemCpy(buffer + DELTA_OFFSET, snapshot.ptr, snapshot.ByteLength)
    SerializeEntities(buffer + ENTITY_OFFSET, entity_snapshot)

STEP 3 — CHECKSUM:
    header_xxh = XXH32(buffer, HEADER_SIZE - 8)  // exclude checksum fields themselves
    payload_xxh = XXH32(buffer + PAYLOAD_START, payload_raw_len)
    WRITE header_xxh → buffer[0x24]
    WRITE payload_xxh → buffer[0x28]

STEP 4 — COMPRESS:
    compressed_payload = LZ4_BlockCompress(buffer + PAYLOAD_START, payload_raw_len)
    // overwrite payload region in-buffer

STEP 5 — WRITE TO TEMP:
    path_tmp = "{SaveDir}/{slot}.tmp"
    FileStream.Write(buffer, total_compressed_size)   // background thread, async
    FileStream.Flush()
    FileStream.Close()

STEP 6 — VERIFY WRITTEN FILE:
    read_back_header = ReadHeaderOnly(path_tmp)
    verify_xxh = XXH32(read_back_header, HEADER_SIZE - 8)
    IF verify_xxh != stored_xxh: GOTO ABORT

STEP 7 — ROTATE:
    IF File.Exists("{slot}.sav"):
        File.Move("{slot}.sav" → "{slot}.bak")  // atomic on NTFS/ext4 same-volume
    File.Move("{slot}.tmp" → "{slot}.sav")       // atomic rename — OS guarantees

STEP 8 — DONE:
    dirty_map.ClearDirtyFlags()  // reset, not dispose
    SIGNAL save_complete_event

ABORT:
    File.Delete("{slot}.tmp")
    LOG("SAVE_FAIL: checksum mismatch on write-verify. .bak preserved.")
    SIGNAL save_failed_event
```

---

## [SAFE-01] CORRUPTION RECOVERY PROTOCOL

```
LoadGame(slot):
    path_sav = "{slot}.sav"
    path_bak = "{slot}.bak"

    TryLoad(path_sav):
        header = ReadHeader(path_sav)
        IF header.MAGIC != 0x48454354: FAIL("bad magic")
        IF (header.COMPAT_MASK & SUPPORTED_MASK) != header.COMPAT_MASK: FAIL("compat")
        verify = XXH32(header_bytes, HEADER_SIZE - 8)
        IF verify != header.XXH32_HEADER: FAIL("header checksum")
        payload_raw = LZ4_Decompress(ReadPayload(path_sav))
        verify2 = XXH32(payload_raw, payload_raw.Length)
        IF verify2 != header.XXH32_PAYLOAD: FAIL("payload checksum")
        RETURN DeserializeDelta(payload_raw)

    result = TryLoad(path_sav)
    IF result.FAILED:
        LOG("PRIMARY_CORRUPT → attempting .bak")
        result = TryLoad(path_bak)
        IF result.FAILED:
            LOG("BOTH_CORRUPT → load seed-only world, preserve corrupted files for debug")
            RETURN WorldState.FromSeedOnly(world_seed)
    RETURN result
```

---

## [SAFE-02] MEMORY GUARD — 64MB BUFFER CEILING

```
AT STARTUP (once):
    save_buffer = NativeArray<byte>(64 * 1024 * 1024, Allocator.Persistent)
    // Never resize. Never reallocate mid-session.

IF (estimated_payload_size > 60MB):  // 60MB threshold, 4MB headroom
    SPLIT into two slots: {slot}_A.sav + {slot}_B.sav
    Store chunk-range partition in each header FLAGS byte
    // Rare case: only occurs with >3M dirty voxels (extreme demolition)

IF system_ram < 6GB (detected at boot):
    REDUCE save_buffer ceiling to 32MB
    INCREASE LZ4 block compression aggressiveness (LZ4_HC level 4, max 9ms budget)
```

---

## [SCALE-01] LOW-LOAD FLUSH SCHEDULING

```
NEVER save during:
    - Active physics simulation burst (>800 rigidbodies awake)
    - SDF terrain generation job running
    - Shader compilation stall (first-load)
    - Frame time > 20ms (50 FPS threshold)

PERMIT save during:
    - Player stationary at Save Terminal (explicit trigger)
    - Scene transition fade (black screen, 0 rendering load)
    - Background idle: frametime < 12ms for 3 consecutive seconds

CHECK each frame:
    IF (save_pending AND frame_time_ms < 12.0f AND !terrain_gen_active):
        DispatchBackgroundSave()
        save_pending = false
```

---

## [SCALE-02] VRAM THRIFT — MX350 2GB CONSTRAINT

```
// GPU-side state that MUST be captured before save flush:
GPU_STATE_CAPTURE_LIST:
    [ ] Ocean dynamic foam texture (128×128 R8, 16KB) → blit to CPU RAM → save as raw bytes
    [ ] Active SDF volume dirty-region (GPU readback, async)  → schedule 2 frames ahead of save
    [ ] Submarine hull stress texture (64×64 R16, 8KB) → readback on save trigger

GPU_READBACK_PATTERN:
    frame N-2: AsyncGPUReadback.Request(sdf_dirty_volume)
    frame N-1: AsyncGPUReadback.Request(foam_rt)
    frame N  : save_pending = true
    frame N+1: IF all_readbacks_complete: DispatchBackgroundSave()
    // Never stall GPU pipeline for save. Async only.

// Do NOT store full GPU texture atlas (would exceed 64MB RAM budget)
// Reconstruct procedural textures from material_id + metadata on load
```

---

## [RULE-02] FORBIDDEN PATTERNS — HARD STOPS

```
FORBID  JsonUtility.ToJson()          // 10x overhead vs binary for world data
FORBID  BinaryFormatter               // reflection-heavy, GC bomb
FORBID  PlayerPrefs for world state   // 4KB limit, main-thread, registry writes
FORBID  File.WriteAllBytes on MainThread
FORBID  Coroutine-based I/O           // frame-coupled, cannot guarantee off-thread
FORBID  LZMA / Deflate / Brotli       // CPU stall >15ms on i3-10gen
FORBID  List<T> or Dictionary<K,V> in hot serialization path  // GC alloc
FORBID  Enum.ToString() or .GetName() in save path            // reflection + alloc
FORBID  StreamingAssets write access  // platform-restricted, read-only on Android/iOS
```

---

## [RULE-03] PERFORMANCE BUDGET CONSTRAINTS

```
OPERATION                        TARGET      HARD LIMIT
────────────────────────────────────────────────────────
dirty_map snapshot (spinlock)    <0.5ms      1ms
MemCpy serialize to buffer       <2ms        4ms
XXH32 checksum (60MB)            <3ms        5ms
LZ4 compress (60MB, 256KB blk)  <35ms       45ms
File write (async, NVMe)         <8ms        15ms  (background, not budgeted on frame)
File write (async, HDD 5400rpm)  <80ms       —     (fully background, no frame impact)
TOTAL main-thread stall          0ms         0ms   (snapshot spinlock only)
TOTAL background wall-clock      <50ms       60ms
```

---

## [MATH-03] SDF DELTA COMPRESSION PRE-PASS — QUANTIZATION

Before LZ4, quantize SDF values to reduce entropy:

```
QUANTIZE sdf_value (float32 → int16):
    RANGE: [-4.0m .. +4.0m]  (only near-surface cells tracked as delta)
    SCALE: int16 = CLAMP(ROUND(sdf_value * 8192.0f), -32768, 32767)
    // Resolution: 1/8192 meters ≈ 0.12mm — sufficient for voxel gameplay

    IF |sdf_value| > 4.0f:
        // Cell is deep solid or deep void — should not be in delta map
        // If present: strip from snapshot before serialization (defensive clean)

RECONSTRUCT:
    sdf_value = (float)stored_int16 / 8192.0f

// int16 vs float32: 50% size reduction per cell before compression
// DeltaCell struct: swap float sdf_value → int16 sdf_q + uint16 metadata = same 20B
```

---

## [FLOW-02] STARTUP LOAD SEQUENCE

```
APPLICATION START:
    1. Detect save slot (slot 0-2, check existence of .sav / .bak)
    2. Allocate save_buffer (NativeArray, Persistent) — do once, never free until quit
    3. Initialize dirty_map (NativeHashMap<uint64, DeltaCell>, capacity=65536, Persistent)
    4. Read save header only (44 bytes) → validate MAGIC + VERSION + COMPAT_MASK
    5. IF valid: schedule async full payload read + decompress on worker thread
    6. WHILE loading: stream world seed generation on main thread (visible to player)
    7. ON load complete: apply delta patches over seed-generated world
       // Delta application: O(N) where N = dirty_map.Count, not world size
    8. Restore entity states from EntityRecord list
    9. Restore player state (position, inventory, o2_level, hull_integrity)
   10. Dispose temp decompress buffer; retain dirty_map + save_buffer for session
```

---

## [DATA-05] PLAYER STATE RECORD — FIXED LAYOUT

```
PlayerState (72 bytes fixed):
  float3   world_pos            (12B)
  float4   rotation_quat        (16B)
  float    hull_integrity       (0.0-1.0)
  float    o2_level             (0.0-1.0)
  float    pressure_depth_m     (current depth meters)
  float    body_temperature     (Celsius, survival mechanic)
  uint16   active_tool_id
  uint16   suit_upgrade_flags   (bitfield, 16 upgrade slots)
  uint32   session_time_seconds (total play time this save)
  uint32   dive_count           (stat)
  float    credits              (in-world currency)
  uint8[8] hotbar_item_ids      (8 slots × uint8 item palette index)
  uint8    difficulty_flags
  uint8    narrative_flags[3]   (24 story-branch bits)
  uint32   rng_seed_state       (restore deterministic loot RNG continuity)
  uint32   XXH32_player         (checksum of preceding 68 bytes)
```

---
