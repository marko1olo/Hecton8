# DATA_MONOLITH_DTO_ORDER_PATCH_1313

Agent: 1313
Date: 2026-05-25
Evidence class: STATIC_SOURCE

## Patch

- `H8DataMonolithTypes.cs:18`: `FormatVersion = 2`.
- `H8DataMonolithTypes.cs:33`: `SchemaHash = 0x33313331`.
- `H8DataMonolithTypes.cs:217-235`: `H8ItemRecord` reordered to 8-byte fields, then 4-byte fields, then 2-byte fields.
- `H8DataMonolithLayoutGuard.cs:53-59`: layout guard updated for the new item offsets.

## Byte Map

- `RecipeMask0`: offset 0, size 8.
- `RecipeMask1`: offset 8, size 8.
- `HashId`: offset 16, size 4.
- `AccessFrequency`: offset 72, size 4.
- `MaxStack`: offset 76, size 2.
- `RecipeIngredientCount`: offset 78, size 2.
- Struct size: 80, multiple of 8.
- Natural alignment failures: 0.

## Blob Status

- Checked-in blob exists: `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`.
- Blob bytes: 1064384.
- Blob header format: 2.
- Blob schema: `0x33313331`.
- Code expected format: 2.
- Code expected schema: `0x33313331`.
- Checksum: `0x19D880780D6E1B46`.
- Validator: `Tools/h8bin_validator.py` PASS.
- Verdict: static blob schema v2 validation passed. Unity import/player boot/profiler proof is still not executed.
