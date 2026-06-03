# H8 Hash Catalog Audit

Status: HASHES SYNCHRONIZED

## Summary

- Total records: 1243
- Items: 209
- Biomes: 523
- Signals: 511
- Generated header check: up-to-date
- Collision status: 0 collisions
- Runtime impact: 0 us/frame, 0 B/frame

## Artifacts

- Generated C#: `Assets/_Project/Scripts/Core/Generated/H8Hashes.cs`
- Markdown audit: `Docs/Reports/H8_Hash_Catalog_Audit.md`
- JSON manifest: `Docs/Reports/H8_Hash_Catalog_Audit.json`

## Hash Modes

- `ascii_lower`: 26
- `loc_utf16`: 814
- `signal_label`: 403

## Group Counts

- `Biomes.BiomeNames`: 324
- `Biomes.FamilyIds`: 26
- `Biomes.MatrixAssetNames`: 108
- `Biomes.ProfileIds`: 65
- `Items.CodeLiteralIds`: 5
- `Items.DisplayNames`: 60
- `Items.LocalizedNameKeys`: 72
- `Items.PersistentIds`: 72
- `Signals.AuthoredSignalIds`: 108
- `Signals.ISignalStructNames`: 303
- `Signals.SignalBusNames`: 6
- `Signals.StructNames`: 94

## Verification Commands

- `python -B Tools\VerifyH8HashCollisions.py --check-csharp Assets/_Project/Scripts/Core/Generated/H8Hashes.cs`
- `python -B Tools\VerifyH8HashCollisions.py --check-csharp Assets/_Project/Scripts/Core/Generated/H8Hashes.cs --write-report Docs/Reports/H8_Hash_Catalog_Audit.md --write-json Docs/Reports/H8_Hash_Catalog_Audit.json`
- `python -B -m unittest Tools.test_h8_hash_collisions`
- Generated C# runtime/allocation scan: `NO_RUNTIME_LOGIC_HITS`

## Boundary

- Unity import is not claimed here; the current shell has no Unity Editor executable.
- This audit is an offline source/data/hash verification artifact.
