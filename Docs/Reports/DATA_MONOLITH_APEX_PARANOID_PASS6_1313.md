# DATA_MONOLITH_APEX_PARANOID_PASS6_1313

Date: 2026-05-25
Agent: 1313
Scope: Data Monolith runtime decoder residue, active release token proof, validator split.

## Prompt Proof

- Source: `Docs/Tasks/CURRENT_BATCH.md`
- Extraction: CLI regex over full `<AGENT_PROMPT id="1313">`
- Prompt length: `12203`
- Task headings: `Task 01` through `Task 10`
- Task count: `10`

## Runtime Managed Codec Patch

Problem:
- `H8StaticDataArena.TryReadLocalizedText` used `Encoding.UTF8.GetCharCount` and `Encoding.UTF8.GetChars` in active runtime accessor code.
- These methods did not create strings, but they preserved a managed codec dependency in the release read path.

Patch:
- `Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs:1022` now calls `TryDecodeUtf8`.
- `Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs:1038` now calls `TryDecodeUtf8`.
- `Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs:1045-1122` implements a manual UTF-8 decoder over `ReadOnlySpan<byte>` into caller-owned `Span<char>`.
- The decoder rejects truncated sequences, invalid continuations, overlong encodings, UTF-16 surrogate scalar input, and scalar values above `U+10FFFF`.
- `System.Text` is now guarded by `#if UNITY_EDITOR || DEVELOPMENT_BUILD`; the remaining `Encoding.UTF8` use is editor/development telemetry dump only at `H8StaticDataArena.cs:2242`.

## Active Release Scan

File: `Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs`

Forbidden token set:
- `new `
- `FileStream`
- `BinaryWriter`
- `UnityWebRequest`
- `DownloadHandlerFile`
- `FileInfo`
- `Path.Combine`
- `string.Format`
- `.ToString(`
- `System.Linq`
- `Enumerable.`
- `.Split(`
- `File.ReadAll`
- `Encoding.UTF8`
- `UTF8Encoding`
- `GetCharCount`
- `GetChars`
- `throw new`
- `catch (Exception`
- `catch(Exception)`

Results:
- Windows release model: active lines `2046`, forbidden hits `0`.
- Android/non-Windows release model: active lines `1652`, forbidden hits `0`.
- `rg "GetSectionDataPointer\(" Assets/_Project/Scripts -g "*.cs"`: no hits.
- Bootstrap owned slices `WriteBootStateRecord` and `WriteFatalBootstrapLog`: forbidden hits `0` for `new NativeArray`, `UTF8Encoding`, `Encoding.UTF8`, `Substring`, `string.Format`, `.ToString(`, `.Split(`, `new float3`, `new double3`, `FileStream`, `BinaryWriter`, `catch (Exception`.

## Binary Payload Proof

Command class: Python validator, not dotnet, not Unity build.

Passing payload mode:
- Report: `Docs/Reports/DATA_MONOLITH_H8BIN_VALIDATOR_1313.json`
- Status: `PASS`
- Files checked: `2`
- Structs parsed: `32`
- Bytes processed: `1100480`
- Elapsed seconds: `0.430863`
- Flags: `--allow-runtime-text-loaders --allow-unbaked-artifacts`

Active `static_data.h8bin`:
- Path: `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`
- Bytes: `1064384`
- Magic: `0x4D443848`
- Format: `2`
- Header bytes: `64`
- Directory offset/bytes: `64/64`
- Schema hash: `0x33313331`
- Checksum: `0x19D880780D6E1B46`
- Directory format: `2`
- Section count: `26`
- Section table offset/bytes: `128/416`
- Data start: `576`
- Flags: `0x00000001`
- Item section: id `1`, record size `80`, count `4`, offset `576`

## Release Blocker Proof

Failing strict release mode:
- Report: `Docs/Reports/DATA_MONOLITH_H8BIN_VALIDATOR_RELEASE_BLOCKERS_1313.json`
- Status: `FAIL`
- Findings: `8`

Strict blockers:
- `UNBAKED_ARTIFACT`: `Assets/StreamingAssets/Hecton8/camera_trauma_profiles.csv`
- `UNBAKED_ARTIFACT`: `Assets/StreamingAssets/Hecton8/haptic_response_profiles.csv`
- `UNBAKED_ARTIFACT`: `Assets/StreamingAssets/Hecton8/PDA/pda_interface_profiles.csv`
- `RUNTIME_TEXT_STREAMINGASSETS_LOAD`: `Assets/_Project/Scripts/Core/HectonInputRuntime_HapticSynth.cs:517`
- `RUNTIME_TEXT_STREAMINGASSETS_LOAD`: `Assets/_Project/Scripts/Ecosystem/NutrientDriftRuntime.cs:1065`
- `RUNTIME_TEXT_STREAMINGASSETS_LOAD`: `Assets/_Project/Scripts/Ecosystem/NutrientDriftRuntime_Carrion.cs:733`
- `RUNTIME_TEXT_STREAMINGASSETS_LOAD`: `Assets/_Project/Scripts/VFX/CameraJuiceSystem_CameraJuiceBurst.cs:740`

Non-blocking info:
- `H8VB_SCHEMA_VALIDATED`: `Assets/StreamingAssets/Hecton8/Audio/vocal_banks.h8bin`

## DTO/AUP/Isolation/Fail-Closed

- DTO layout remains covered by `Docs/Reports/DATA_MONOLITH_BYTE_OFFSET_MAP_1313.md` and `Docs/Reports/DATA_MONOLITH_LAYOUT_GUARD_ALL_DTO_1313.md`.
- `H8ItemRecord` remains strict-order PASS: `RecipeMask0 @0`, `RecipeMask1 @8`, 4-byte fields `16..72`, 2-byte fields `76/78`, size `80`.
- `H8DataBlobHeader` remains the only strict-order ABI exception because the file begins with magic/version/header bytes before `Checksum64 @8`.
- Data Monolith does not perform force, collision, or distance simulation. AUP DTO storage remains byte-layout data only. Cross-domain AUP math found in span consumers is not owned by this pass.
- Public Data Monolith pointer API remains removed. Normal section reads use `ReadOnlySpan<T>`. Remaining pointer token is the private Burst item-hash helper behind a span overload.
- Non-Windows release still fails closed without native/PAL monolith asset loading.

## Verdict

Pass 6 fixed a real runtime managed codec surface in `H8StaticDataArena`.

Release readiness is still rejected:
- Android/Quest production loading still has no zero-GC native/PAL bridge.
- Strict validator still finds unbaked runtime CSV artifacts and StreamingAssets text-load routes outside the 1313 source slice.
- Unity import, player boot, profiler, and GC proof were not run.
- No dotnet build was run.
