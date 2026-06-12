# DATA MONOLITH LAYOUT GUARD ALL DTO - 1313

Date: 2026-05-25
Evidence: STATIC_SOURCE_NO_DOTNET_NO_UNITY

## Patch

- `Assets/_Project/Scripts/Editor/DataMonolith/H8DataMonolithLayoutGuard.cs:85` now calls `ExpectAllDeclaredLayouts()` after the existing fixed ABI checks.
- `Assets/_Project/Scripts/Editor/DataMonolith/H8DataMonolithLayoutGuard.cs:121-152` enumerates all 32 Data Monolith DTO structs.
- `Assets/_Project/Scripts/Editor/DataMonolith/H8DataMonolithLayoutGuard.cs:155` validates each DTO as explicit-layout only.
- `Assets/_Project/Scripts/Editor/DataMonolith/H8DataMonolithLayoutGuard.cs:178-182` requires declared `FieldOffsetAttribute` and verifies actual `UnsafeUtility.GetFieldOffset`.
- `Assets/_Project/Scripts/Editor/DataMonolith/H8DataMonolithLayoutGuard.cs:193-200` enforces natural field alignment up to 8 bytes.
- `Assets/_Project/Scripts/Editor/DataMonolith/H8DataMonolithLayoutGuard.cs:202-215` rejects overlapping fields and undeclared padding holes.
- `Assets/_Project/Scripts/Editor/DataMonolith/H8DataMonolithLayoutGuard.cs:219-242` rejects `bool`, `string`, managed refs, and unsupported field types.

## Static Verification

- Text DTO coverage script: `DTO_STRUCTS=32`, `FAILURES=0`.
- Guard enumerator count: `GUARD_EXPECT_DECLARED_CALLS=32`.
- Guard preprocessor balance: `IF=1`, `ENDIF=1`, balanced.
- `Docs/Reports/*1313*.json` parse: pass at time of report; final session parse is recorded in `DATA_MONOLITH_BLOB_V2_MIGRATION_1313`.
- `git diff --check` for patched guard/report files: pass, CRLF warning only.
- Active `H8StaticDataArena` Windows release forbidden-token hits: 0.
- Active `H8StaticDataArena` Android/non-Windows release forbidden-token hits: 0.

## Remaining Rejections

- `H8DataBlobHeader` still violates strict descending field-size order by ABI: `Magic`/version/header bytes must stay at offsets `0/4/6` before `Checksum64` at `8`.
- Checked-in `static_data.h8bin` now matches v2 `H8ItemRecord` schema: format `2`, schema `0x33313331`, checksum `0x19D880780D6E1B46`, Python validator `PASS`.
- Quest/Android production loading remains fail-closed until a native/PAL asset route exists.
- 262 strict cross-domain production parser/file/config blockers remain outside 1313 ownership.
