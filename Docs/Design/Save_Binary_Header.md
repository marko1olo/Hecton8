# Save Binary Header
Date: 2026-05-14
Owner: SAVE_HASH_CRYPTOGRAPHER
Status: INTEGRITY SECURED / PYTHON_REFERENCE_FUZZ_VERIFIED / PENDING UNITY VERIFICATION

## Scope

This file defines the byte contract for the save integrity header and the XXH3 bit-shuffle oracle. Runtime writer integration is not performed in this slice.

Authority checked:

- `AGENTS.md`
- `.agents-skills/DATA_Save_Persistence_Binary_Delta_Checksum.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/MATH_AUP_Determinism_Sync.txt`
- `.agents-skills/MATH_Deterministic_RNG_SlotMachine.txt`
- `.agents-skills/OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`

## Current Header Reality

`Assets/_Project/Scripts/SaveBinaryStorage.cs` currently defines `SaveFileHeader` as `56` bytes, not `52` bytes.

| Offset | Size | Field | Encoding |
|---:|---:|---|---|
| 0 | 4 | `MagicValue` | little-endian `uint`, expected `0x48454354` |
| 4 | 2 | `Version` | little-endian `ushort` |
| 6 | 1 | `CompatMask` | byte |
| 7 | 1 | `Flags` | byte |
| 8 | 8 | `TimestampUnixMs` | little-endian `ulong` |
| 16 | 4 | `Checksum` | little-endian `uint`, indexed checksum-chain root |
| 20 | 4 | `DeltaCount` | little-endian `uint` |
| 24 | 4 | `EntityCount` | little-endian `uint` |
| 28 | 4 | `PlayerOffset` | little-endian `uint` |
| 32 | 4 | `DeltaOffset` | little-endian `uint` |
| 36 | 4 | `EntityOffset` | little-endian `uint` |
| 40 | 8 | `HashPayload64` | little-endian `ulong` |
| 48 | 8 | `HashHeader64` | little-endian `ulong` |

Current `HashHeader64` rule: zero `HashHeader64`, then hash the first `48` bytes with XXH3-64. This excludes `HashHeader64` by byte span.

## V10 MasterStateHash Offset

The next ABI-compatible extension must keep the first `56` bytes intact and append the 128-bit master hash.

| Offset | Size | Field | Encoding |
|---:|---:|---|---|
| 56 | 8 | `MasterStateHashLo` | little-endian `ulong` |
| 64 | 8 | `MasterStateHashHi` | little-endian `ulong` |

`CurrentHeaderSizeV10 = 72`. The first writer that emits this field must bump the save header version from `0x0009` to `0x000A`; version `0x0009` readers must continue treating byte `56+` as absent.

`MasterStateHash` storage order is exactly `lo64` then `hi64`; the canonical byte dump is `BitConverter.GetBytes(lo64)` followed by `BitConverter.GetBytes(hi64)` on a little-endian writer. A Burst C# writer must not use platform-native struct dumps for this field unless the struct has explicit layout and the byte order is separately tested.

## MasterStateHash Preimage

The plain 128-bit state hash is two XXH3-64 lanes:

```text
preimage =
  ASCII("H8SAVE_MASTER_V1") ||
  le_u32(MagicValue) ||
  le_u16(Version) ||
  u8(CompatMask) ||
  u8(Flags) ||
  le_u64(TimestampUnixMs) ||
  le_u32(Checksum) ||
  le_u32(DeltaCount) ||
  le_u32(EntityCount) ||
  le_u32(PlayerOffset) ||
  le_u32(DeltaOffset) ||
  le_u32(EntityOffset) ||
  le_u64(HashPayload64) ||
  le_u64(WorldSeed) ||
  le_i64(AUP.SectorHash)

plain_lo = XXH3_64(preimage || ASCII("_LO"))
plain_hi = XXH3_64(preimage || ASCII("_HI") || le_u64(plain_lo))
```

`HashHeader64` is intentionally excluded to avoid a circular dependency with the existing header hash. `MasterStateHashLo/Hi` are also excluded because they are the result fields.

For the global `.sav` metadata master, `AUP.SectorHash = 0`. For a paged sector or `.h8db` record, use the owning `SectorEntry.SectorHash`.

This is an integrity and tamper-friction layer, not a cryptographic MAC. XXH3 is non-cryptographic. If adversarial tamper resistance becomes a hard security requirement, add a keyed MAC in a separate field instead of pretending this is encryption.

## 128-Bit Shuffle Rule

The saved hash is the plain hash XOR-masked and rotated. All math is unsigned and masked after every operation.

```text
mask_lo = XXH3_64(ASCII("H8SAVE_SHUFFLE_LO_V1") || le_u64(WorldSeed) || le_i64(AUP.SectorHash))
mask_hi = XXH3_64(ASCII("H8SAVE_SHUFFLE_HI_V1") || le_i64(AUP.SectorHash) || le_u64(WorldSeed) || le_u64(mask_lo))
rotation = (mask_lo ^ (mask_hi >> 1)) & 127

plain128 = plain_lo | (plain_hi << 64)
mask128  = mask_lo  | (mask_hi  << 64)
stored128 = rotl128(plain128 ^ mask128, rotation)

MasterStateHashLo = stored128 & 0xffffffffffffffff
MasterStateHashHi = stored128 >> 64
```

Reverse validation:

```text
plain128 = rotr128(stored128, rotation) ^ mask128
```

Rejected alternative: per-byte random permutation. It is slower, more error-prone in Burst, and provides no meaningful security improvement over an invertible 128-bit rotate after the XOR mask.

## Python/Burst Cross-Platform Contract

`Tools/Security/ReplayHasher.py` is the OSHINO oracle.

The SHINOBU Burst port must match it exactly:

- Read all multibyte integers little-endian.
- Treat `WorldSeed` and `AUP.SectorHash` as two's-complement 64-bit lane bytes.
- Mask every add, multiply, xor, and rotate to unsigned 64-bit or 128-bit width.
- Return `uint2`/`ulong2` lanes in low-high order, not high-low display order.
- Never hash `Transform.position`; hash AUP sector/local authority only.
- Do not serialize a managed `struct` by raw memory unless it is `[StructLayout]` and has an explicit size/padding proof.

CLI reference commands:

```powershell
python .\Tools\Security\ReplayHasher.py self-test
python .\Tools\Security\ReplayHasher.py master --flags 0x0C --timestamp-unix-ms 0x0000018F3D123456 --checksum 0xDEADBEEF --delta-count 37 --entity-count 1024 --player-offset 72 --delta-offset 4096 --entity-offset 8192 --hash-payload64 0x0123456789ABCDEF --world-seed 123456789 --sector-hash -987654321
```

Expected master vector for the second command:

```text
plain_lo=0x82C250ACAADCFCEE
plain_hi=0x750FEB3BE2F001A7
stored_lo=0x32C38E7EA8C9246D
stored_hi=0x8CB2B6D20A988126
stored_le=6d24c9a87e8ec3322681980ad2b6b28c
```

## DTO Padding Mandate

Binary/native DTOs must obey these rules before PHI_VOD or any runtime writer blits them:

- Use `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = N)]` only when every 8-byte field starts at an offset divisible by `8`, or when the writer uses explicit little-endian field writes instead of unaligned loads.
- Prefer explicit `byte` flags over `bool`; managed `bool` layout is not a save ABI.
- No `string`, managed array, `List<T>`, `Dictionary<K,V>`, `HashSet<T>`, `Vector3`, `Quaternion`, or Unity object reference inside a blitted DTO.
- Pad to a `16` byte multiple for sector records and `64` byte multiple for high-frequency indexed blocks.
- Store AUP as `long gridX/Y/Z` plus millimeter-quantized integer local offsets for new authority records. Existing float locals are legacy compatibility only.

## SaveData.cs ARM-Killer Audit

Static source audit of `Assets/_Project/Scripts/SaveData.cs` found:

- No currently marked `[BinaryBlittableSafe]` DTO in the scanned file has an obvious 8-byte field at a non-8 offset.
- `PlayerStatsDTO` is not explicitly laid out and contains `bool`; it must remain field-serialized, not raw-blitted.
- `ProceduralFaunaStateDTO` and `HibernatedFaunaStateDTO` contain `bool` fields and lack explicit layout; PHI_VOD should replace them with fixed byte flags before any native persistence path.
- `RunModifiersDTO` contains four `bool` fields and a `string`; it is managed compatibility data only.
- `ModuleDTO`, `ModuleGraphNodeDTO`, `PDAMarkerEntryDTO`, `ProceduralLorePlacementDTO`, barter/log/scan DTOs, and several root `SaveData` fields contain managed strings or arrays. They are not native DTOs.
- `Dictionary<string, *>`, `HashSet<int>`, and `List<string>` fields in root `SaveData` are migration/compatibility debt and must not enter Burst or raw save pages.

PHI_VOD handoff: create or reuse fixed blit mirrors for the managed compatibility DTOs above. Do not mutate existing public DTO field order during the active batch without a legacy wrapper.

## Evidence Boundary

Evidence class: `STATIC_SOURCE` and `STATIC_DOC`.

Commands used:

- `Select-String` against `SaveBinaryStorage.cs` for `SaveFileHeader`, `CurrentHeaderSize`, `HashPayload64`, `HashHeader64`.
- `Select-String` against `SaveData.cs` for `[StructLayout]`, `[BinaryBlittableSafe]`, `bool`, managed collections, and string fields.
- `python -m compileall .\Tools\Security\ReplayHasher.py` -> PASS.
- `python .\Tools\Security\ReplayHasher.py self-test` -> PASS, including embedded branch and shuffle vectors.
- `python .\Tools\Security\ReplayHasher.py master ...` -> PASS, expected `stored_le=6d24c9a87e8ec3322681980ad2b6b28c`.
- Isolated comparison against Python `xxhash.xxh3_64_intdigest` across 136 deterministic seed/length vectors plus 128 randomized seeded/fuzz cases -> PASS (`XXH3_REFERENCE_AND_SHUFFLE_FUZZ_OK 264 cases`).
- `<POLISH_MANDATE>` extraction from `Docs/Tasks/CURRENT_BATCH.md` -> TAG ABSENT; local anti-bloat pass executed on owned artifacts.

Unity import, Unity Console, Play Mode, GCMonitor, profiler, player build, and IL2CPP/ARM runtime proof remain `PENDING VERIFICATION`.
