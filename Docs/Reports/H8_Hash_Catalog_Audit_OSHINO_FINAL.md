# H8 Hash Catalog Audit

Status: HISTORICAL HASH-CATALOG STATIC SNAPSHOT / RUNTIME PENDING VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-18 R13 Report Snapshot Boundary

This report file is a snapshot/provenance document. It is active only where it agrees with:

- `Docs/README.md`
- `Docs/Reports/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

Historical `PASS`, `VERIFIED`, `current`, `latest`, counter, compile, runtime, 0-GC, frame-time, cost, and performance statements inside this report are not current proof unless the exact claim links a fresh artifact path, command/tool, timestamp, evidence class, and unresolved-error list. No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied by this file alone.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Summary

- Total records: 1018
- Items: 209
- Biomes: 523
- Signals: 286
- Generated header check: not checked
- Collision status: 0 collisions
- Runtime impact: 0 us/frame, 0 B/frame

## Artifacts

- Generated C#: `Assets/_Project/Scripts/Core/Generated/H8Hashes.cs`
- Markdown audit: `Docs/Reports/H8_Hash_Catalog_Audit.md`
- JSON manifest: `Docs/Reports/H8_Hash_Catalog_Audit.json`

## Hash Modes

- `ascii_lower`: 26
- `loc_utf16`: 813
- `signal_label`: 179

## Group Counts

- `Biomes.BiomeNames`: 324
- `Biomes.FamilyIds`: 26
- `Biomes.MatrixAssetNames`: 108
- `Biomes.ProfileIds`: 65
- `Items.CodeLiteralIds`: 5
- `Items.DisplayNames`: 60
- `Items.LocalizedNameKeys`: 72
- `Items.PersistentIds`: 72
- `Signals.AuthoredSignalIds`: 107
- `Signals.ISignalStructNames`: 143
- `Signals.SignalBusNames`: 3
- `Signals.StructNames`: 33

## Verification Commands

- `python -B Tools\VerifyH8HashCollisions.py --check-csharp Assets/_Project/Scripts/Core/Generated/H8Hashes.cs`
- `python -B Tools\VerifyH8HashCollisions.py --check-csharp Assets/_Project/Scripts/Core/Generated/H8Hashes.cs --write-report Docs/Reports/H8_Hash_Catalog_Audit.md --write-json Docs/Reports/H8_Hash_Catalog_Audit.json`
- `python -B -m unittest Tools.test_h8_hash_collisions`
- Generated C# runtime/allocation scan: `NO_RUNTIME_LOGIC_HITS`

## Boundary

- Unity import is not claimed here; the current shell has no Unity Editor executable.
- This audit is an offline source/data/hash verification artifact.
