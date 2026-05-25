# DATA MONOLITH APEX PARANOID PASS 3 - 1313

Date: 2026-05-25
Agent: 1313
Domain: Echelon 1 Core Infrastructure / Data Monolith Static Data Pipeline
Evidence: STATIC_SOURCE_STATIC_BINARY_NO_DOTNET_NO_UNITY

## Prompt Recheck

- Source: `Docs/Tasks/CURRENT_BATCH.md`
- Tag: `<AGENT_PROMPT id="1313" role="DATA_MONOLITH_BAKER_AND_RELEASE_PURGER">`
- Task count: 10
- Rechecked mandates: Zero-GC, Data Monolith binary bridge, ARM64 layout, GlobalDataVault pure read accessors, native-memory ownership.

## DataVault Read Accessor Correction

Problem found during self-review:
- Public Data Monolith read paths were opening the resident arena through mutable `IDataVault.TryResolveHandle`.
- This violated the project doctrine that read accessors must be pure and consumers must read immutable snapshots or cached interfaces.

Patch:
- Added `TryRefreshArenaReadOnly(out NativeArray<byte>.ReadOnly arena)` at `Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs:2103-2110`.
- The helper uses `IDataVault.TryReadOnlyHandle`, not `TryResolveHandle`.

Read paths now routed through read-only vault aliases:
- `TryGetSection`: `H8StaticDataArena.cs:534`
- `TryGetSectionSpan`: `H8StaticDataArena.cs:594`
- `GetSectionDataPointer`: `H8StaticDataArena.cs:623`
- `TryGetArena`: `H8StaticDataArena.cs:738`
- `TryGetResidentBlob`: `H8StaticDataArena.cs:752`
- `TryReadLocalizedText`: `H8StaticDataArena.cs:1042`
- `TryGetLocalizedUtf8Block`: `H8StaticDataArena.cs:1099`
- `TryGetLocalizedUtf8Span(offset,length)`: `H8StaticDataArena.cs:1126`
- `TryGetLocalizedUtf8Span(offset)`: `H8StaticDataArena.cs:1165`
- `GetStaticLocalizationReferenceCount`: `H8StaticDataArena.cs:1196`
- `TryGetNextStaticLocalizationReference`: `H8StaticDataArena.cs:1220`
- `ComputeResidentPayloadHash64`: `H8StaticDataArena.cs:1436`
- `TryValidateResidentArena`: `H8StaticDataArena.cs:1879`
- `IsDirectoryValid`: `H8StaticDataArena.cs:2004`

Mutable arena resolution remains only in write/hydration paths:
- `TryInitializeFromMemory`: `H8StaticDataArena.cs:474`
- Editor/development file read into arena: `H8StaticDataArena.cs:1456`
- Windows native file read into arena: `H8StaticDataArena.cs:1785`
- Helper definition: `H8StaticDataArena.cs:2093-2100`

Residual unsafe fact:
- `GetSectionDataPointer` still returns `void*` because existing consumers use the public pointer API. C# cannot express a const unmanaged pointer. The pointer is now sourced from a read-only DataVault alias, and searched external consumers only read copied records.

## Static Token Scan

Preprocessor model:
- Windows release: `UNITY_STANDALONE_WIN=true`, `UNITY_EDITOR=false`, `DEVELOPMENT_BUILD=false`, no WebGL/Android/iOS.
- Non-Windows release: no standalone Windows/editor/development/mobile symbols.

Forbidden tokens:
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

Results:
- Windows release active lines: 2032
- Windows release forbidden hits: 0
- Non-Windows release active lines: 1638
- Non-Windows release forbidden hits: 0

## Non-Build Validation

- `rg` proof: mutable arena resolver appears only at lines `474`, `1456`, `1785`, and helper `2093`.
- `rg` proof: read-only resolver appears at lines `534`, `594`, `623`, `738`, `752`, `1042`, `1099`, `1126`, `1165`, `1196`, `1220`, `1436`, `1879`, `2004`, and helper `2103`.
- Current 1313 JSON reports parsed before this report: `JSON_1313_PARSE_OK count=17`.
- `git diff --check -- H8StaticDataArena.cs`: CRLF warning only.
- No dotnet, Unity import, Unity build, or player boot was executed.

## Rejection Line

Release readiness is still rejected:
- Android/Quest/non-Windows production loading still fails closed; no native/PAL monolith asset bridge exists.
- 262 strict production parser/file/config blockers remain outside 1313 ownership.
- `H8DataBlobHeader` remains a strict-order ABI exception by file-format design.
- Unity player boot, profiler allocation capture, and device GC proof are absent.
- This pass fixes DataVault read purity in the owned runtime loader. It does not prove full release readiness.
