# DATA MONOLITH APEX PARANOID PASS 4 - 1313

Date: 2026-05-25
Agent: 1313
Domain: Echelon 1 Core Infrastructure / Data Monolith Static Data Pipeline
Evidence: STATIC_SOURCE_NO_DOTNET_NO_UNITY

## Prompt Recheck

- Source: `Docs/Tasks/CURRENT_BATCH.md`
- Tag: `<AGENT_PROMPT id="1313">`
- Task count: 10
- Focus: public pointer surface, read-only section consumption, repeated vault handle resolution.

## Pointer Surface Quarantine

Problem:
- `GetSectionDataPointer` had become read-only internally, but it still exposed a public mutable `void*` API.
- C# cannot encode `const T*`, so the public signature itself was an unsafe authority leak even if current consumers only read.

Patch:
- External biome consumers now use `H8StaticDataArena.GetSectionSpan<H8BiomeRecord>(H8DataSectionId.Biomes)`.
- `GetSectionDataPointer` is now private at `Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs:623`.

External consumers migrated:
- `Assets/_Project/Scripts/HectonVoxelEngine.cs:8255`
- `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:3604`
- `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:3627`
- `Assets/_Project/Scripts/World/Biomes/BiomeBoundarySdfRuntime.cs:786`
- `Assets/_Project/Scripts/World/GPUScatterDirector.cs:1094`

Search result:
- `rg "GetSectionDataPointer\\(" Assets/_Project/Scripts -g "*.cs"` now returns only `H8StaticDataArena.cs` internal hits.
- No external domain still calls the pointer API.

## Handle Resolution Tightening

Problem:
- `TryGetSectionSpan` and the private pointer path resolved the DataVault arena twice per lookup: once through `TryGetSection`, then again for the payload pointer.

Patch:
- Added `TryGetSectionFromArena(NativeArray<byte>.ReadOnly arena, ...)` at `H8StaticDataArena.cs:540-575`.
- `TryGetSection`, `TryGetSectionSpan`, and private `GetSectionDataPointer` now resolve the read-only arena once per call and reuse the section-table lookup over the same alias.

Relevant lines:
- `TryGetSection`: `H8StaticDataArena.cs:531-537`
- `TryGetSectionFromArena`: `H8StaticDataArena.cs:540-575`
- `TryGetSectionSpan`: `H8StaticDataArena.cs:595-615`
- private `GetSectionDataPointer`: `H8StaticDataArena.cs:623-642`

## Static Scan

Active release token scan for `H8StaticDataArena.cs`:
- Windows release active lines: `2047`
- Windows release forbidden hits: `0`
- Non-Windows release active lines: `1653`
- Non-Windows release forbidden hits: `0`

Forbidden tokens scanned:
- `new`
- `FileStream`
- `BinaryWriter`
- `UnityWebRequest`
- `DownloadHandlerFile`
- `FileInfo`
- `Path.Combine`
- `string.Format`
- `.ToString(`
- LINQ
- `catch (Exception)`
- `.Split(`
- `File.ReadAll*`
- literal string concatenation

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
- No Android/Quest native/PAL monolith asset loader exists.
- 262 strict parser/file/config blockers remain outside 1313 ownership.
- No Unity import/player boot/profiler/GC proof was run.
- This pass closes the public pointer surface and removes duplicate read-only handle resolution; it does not prove full release readiness.
