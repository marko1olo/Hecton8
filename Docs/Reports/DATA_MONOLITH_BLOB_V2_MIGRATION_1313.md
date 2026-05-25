# DATA MONOLITH BLOB V2 MIGRATION - 1313

Date: 2026-05-25
Evidence class: STATIC_BINARY / STATIC_SOURCE / TOOL_VALIDATOR
Build policy: no dotnet, no Unity build, no Unity import executed.

## Verdict

- `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` is no longer stale against `H8DataLayoutConstants.FormatVersion = 2` and `SchemaHash = 0x33313331`.
- The active blob checksum is internally valid after the `H8ItemRecord` ABI migration.
- Python validator result after validator-contract patch: `PASS`, 1 file, 32 parsed structs, 26 sections, 3339 sampled records.
- Remaining release blockers are not this blob: Android/Quest native/PAL loader is still missing, global production parser purge is still incomplete, and Unity/player/profiler proof is not executed.

## Binary Header

| Field | Value |
|---|---:|
| Bytes | `1064384` |
| Magic | `0x4D443848` |
| FormatVersion | `2` |
| HeaderBytes | `64` |
| Checksum64 stored | `0x19D880780D6E1B46` |
| Checksum64 recomputed over `bytes[64..end)` | `0x19D880780D6E1B46` |
| DirectoryOffset | `64` |
| DirectoryBytes | `64` |
| SectionTableOffset | `128` |
| SectionCount | `26` |
| Flags | `0x00000001` |
| SchemaHash | `0x33313331` |

## Directory

| Field | Value |
|---|---:|
| DirectoryFormatVersion | `2` |
| SectionTableBytes | `416` |
| TableEnd | `544` |
| DataStartOffset | `576` |
| Alignment rule | `AlignUp(544, 64) = 576` |
| LocalizationOffset | `1063104` |
| LocalizationBytes | `831` |
| Flags | `0x00000001` (`BlobFlagLittleEndian`) |

## Item Section

| Field | Value |
|---|---:|
| SectionId | `1` |
| RecordSize | `80` |
| Count | `4` |
| OffsetBytes | `576` |

## H8ItemRecord V2 Offset Map

| Offset | Bytes | Field |
|---:|---:|---|
| `0` | `8` | `RecipeMask0` |
| `8` | `8` | `RecipeMask1` |
| `16` | `4` | `HashId` |
| `20` | `4` | `RecordIndex` |
| `24` | `4` | `CategoryHash` |
| `28` | `4` | `Flags` |
| `32` | `4` | `MassKg` |
| `36` | `4` | `VolumeM3` |
| `40` | `4` | `BaseQuality` |
| `44` | `4` | `RarityWeight` |
| `48` | `4` | `CraftTimeSeconds` |
| `52` | `4` | `NameUtf8Offset` |
| `56` | `4` | `DescriptionUtf8Offset` |
| `60` | `4` | `NameUtf8ByteLength` |
| `64` | `4` | `DescriptionUtf8ByteLength` |
| `68` | `4` | `Cost` |
| `72` | `4` | `AccessFrequency` |
| `76` | `2` | `MaxStack` |
| `78` | `2` | `RecipeIngredientCount` |

Size proof: `80 % 8 == 0`.

## First Record Byte Probe

```text
item+00=0000000000000000
item+08=0000000000000000
item+16=B8302110
item+20=00000000
item+24=1CA70C34
item+28=00000000
item+32=9A99193E
item+36=6F12833A
item+40=0000803F
item+44=00000000
item+48=00000000
item+52=A9010000
item+56=B9010000
item+60=0F000000
item+64=21000000
item+68=26000000
item+72=0000A442
item+76=1000
item+78=0000
```

## Migration Map

Old v1 bytes were remapped into v2 records:

- `new[0..7] = old[16..23]`
- `new[8..15] = old[24..31]`
- `new[16..19] = old[0..3]`
- `new[20..23] = old[4..7]`
- `new[24..27] = old[8..11]`
- `new[28..31] = old[12..15]`
- `new[32..67] = old[32..67]`
- `new[68..71] = old[72..75]`
- `new[72..75] = old[76..79]`
- `new[76..77] = old[68..69]`
- `new[78..79] = old[70..71]`

Old checksum: `0x0D49885F30E5DF35`.
New checksum: `0x19D880780D6E1B46`.
Temporary pre-migration backup: `%TEMP%\static_data_h8bin_pre_v2_1313.bak`.

## Validator Contract Patch

- `Tools/h8bin_validator.py:2212` adds `align_up`.
- `Tools/h8bin_validator.py:3188-3195` now accepts aligned `DataStartOffset`; this matches `H8DataMonolithCompiler.cs:858-860` and `H8DataMonolithCompiler.cs:1074`.
- `Tools/h8bin_validator.py:3325-3327` now allows `BlobFlagLittleEndian`; this matches `H8DataLayoutConstants.BlobFlagLittleEndian` at `H8DataMonolithTypes.cs:36` and compiler writes at `H8DataMonolithCompiler.cs:1075/1110`.

## Validator Output

```text
h8bin_validator status=PASS files=1 structs=32 mb=1.015076 seconds=0.140687
```
