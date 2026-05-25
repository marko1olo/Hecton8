# DATA MONOLITH APEX PARANOID PASS 5 - 1313

Date: 2026-05-25
Agent: 1313
Domain: Echelon 1 Core Infrastructure / Data Monolith Static Data Pipeline
Evidence: STATIC_SOURCE_NO_DOTNET_NO_UNITY

## Prompt Recheck

- Source: `Docs/Tasks/CURRENT_BATCH.md`
- Tag: `<AGENT_PROMPT id="1313">`
- Task count: 10
- Focus: internal pointer helper purge and span-only Data Monolith section reads.

## Internal Pointer Helper Purge

Problem:
- PASS4 removed external `GetSectionDataPointer` users, but the private helper and internal record pointer reads still existed.
- That left a second section-read contract in the loader and kept unnecessary pointer aliases in normal query methods.

Patch:
- Removed `GetSectionDataPointer` entirely.
- Converted all internal section record readers to `ReadOnlySpan<T>` from `GetSectionSpan<T>`.
- `TryResolveLootItem` now resolves the loot section span once and passes it into `TryFindLootTableRange(ReadOnlySpan<H8LootCdfRecord>, ...)`.
- Kept only the private Burst pointer helper for `H8ItemRecord` binary search; the public route is the `ReadOnlySpan<H8ItemRecord>` overload.

Span-read conversion lines in `H8StaticDataArena.cs`:
- `H8ItemRecord`: `622`, `661`, `1222`
- `H8CreatureTraitRecord`: `636`, `1244`
- `H8LootCdfRecord`: `739`, `2326`, `2331`
- `H8BiomeHeatmapCellRecord`: `785`
- `H8VoxelMaterialRecord`: `818`
- `H8AudioClipRegistryRecord`: `849`
- `H8DepthPressureSampleRecord`: `890`
- `H8SubmarineHullConstantRecord`: `918`
- `H8PhysicsMaterialRecord`: `949`
- `H8BiomeRecord`: `1271`
- `H8GhostModuleRecord`: `1293`
- `H8SopErrorRecord`: `1315`

Pointer search:
- `rg "GetSectionDataPointer\\(" Assets/_Project/Scripts -g "*.cs"`: no hits.
- Remaining record pointer token: private `TryFindByHash([NoAlias] H8ItemRecord* ...)` at `H8StaticDataArena.cs:669`, invoked only by span overload at `H8StaticDataArena.cs:696-700`.

## Static Scan

Active release token scan for `H8StaticDataArena.cs`:
- Windows release active lines: `1957`
- Windows release forbidden hits: `0`
- Non-Windows release active lines: `1563`
- Non-Windows release forbidden hits: `0`

Preprocessor balance:
- `H8StaticDataArena.cs`: `IF=20 ENDIF=20`
- `HectonVoxelEngine.cs`: `IF=23 ENDIF=23`
- `WorldChunkResidencyManager.cs`: `IF=16 ENDIF=16`
- `BiomeBoundarySdfRuntime.cs`: `IF=1 ENDIF=1`
- `GPUScatterDirector.cs`: `IF=5 ENDIF=5`

`git diff --check`:
- CRLF warnings only.

## Rejection Line

Still rejected:
- Android/Quest/non-Windows production loading still fails closed without native/PAL monolith asset bridge.
- 262 strict production parser/file/config blockers remain outside 1313 ownership.
- Unity import/player boot/profiler/GC proof was not run.
- This pass removes the section pointer helper and normalizes reads on spans. It does not prove device release readiness.
