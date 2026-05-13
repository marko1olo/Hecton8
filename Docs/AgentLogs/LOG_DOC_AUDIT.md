# DOC_AUDIT Log

## 2026-05-13 - Item Identity / Catalog Validator Hardening

What was wrong:

- Active DOC_AUDIT status/rationale/log had been archived into `Docs/Archive/Batch004/`, leaving no active DOC_AUDIT disk memory for new continuation work.
- R21 closed the static resource-node primary harvest gaps to `0 / 27` missing `worldPrefab` and `0 / 27` non-catalog, but one identity contamination remained: root `Assets/_Project/Data/Items/Data_Copper.asset` and cataloged raw `Assets/_Project/Data/Items/Resources/Raw/Data_Copper.asset` both author `stableId: Data_Copper`.
- Existing docs said a separate duplicate-stable-id validator was still needed.

What was done:

- Restarted active DOC_AUDIT status/rationale/log as R22, with Batch004 recorded as archived memory.
- Patched `Assets/_Project/Scripts/Editor/ContentSanityValidator.cs`.
- Added validator counters and errors for duplicate `ItemData.PersistentId`, null `ItemCatalog.allItems` entries, duplicate catalog hashes, missing runtime descriptors, and `ItemCatalog` lookup ambiguity.
- Promoted the new validator boundary to stable/current docs.

Cinematic Cheats used:

- None. This is authored-data validation, not visual simulation.

Exact Microseconds saved:

- 0 us/frame. The changed code is editor-only validation under `#if UNITY_EDITOR`.

Verification:

- Static only: source readback, YAML duplicate scan, and diff checks.
- No Unity menu execution, Unity import, Console proof, Play Mode, profiler, Addressables build, player build, or runtime route proof was run.
