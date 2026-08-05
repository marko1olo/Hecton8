# Data Monolith H8BIN Spec

Date: 2026-06-01
Status: STATIC FILE PRESENT / HEADER PARSE RECORDED / UNITY RUNTIME PROOF PENDING
Owner: X_012 DOCUMENTATION_CLEANUP_AND_ACTUALIZATION_ENGINE
Evidence class: STATIC_DOC / STATIC_SOURCE / H8BIN_HEADER_PARSE

## Current Source Boundary

| Item | Value |
|---|---|
| runtime payload | `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` |
| current workspace payload status | present; `7,457,664` bytes, mtime 2026-06-07, measured 2026-08-05 (supersedes 2026-06-01 check: `1,804,864` bytes) |
| H8DM header size | `64` bytes |
| H8DM directory size | `64` bytes |
| H8DM format version | `2` |
| H8DM schema hash | `0x33313332` |
| current checksum64 | `0xA85210353432862A` |
| section count | `28` |
| source data root | `Assets/_SourceData/DataMonolith` |
| bake output | `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` |
| editor menu | `Hecton8/Data Monolith/Bake Static Data` |

The Data Monolith stores immutable static data. It is not the save container.

DTO structs are byte-exact binary records. The current hard contract is explicit 8-byte-safe layout: `StructLayout(LayoutKind.Explicit, Size = ...)` with fixed offsets. Runtime `Pack=1` is rejected; packed file-format records must be copied into aligned runtime structs before NativeArray, Burst, SignalBus, telemetry, save staging, or GPU upload use.

Cold boot loading contract:

- URL-backed `StreamingAssets` paths use the bootstrap `Awaitable` phase.
- Direct filesystem paths can hydrate synchronously into the Vault arena.
- Android player builds use the NDK `AAssetManager` source-plugin bridge for `static_data.h8bin`; bytes stream directly into the Vault arena. Packaging proof requires `AndroidTargetArchitectures: 2` and `androidSplitApplicationBinary: 0`.
- Android `h8bin` entries must be uncompressed/FD-backed. The bridge treats `AAsset_openFileDescriptor64` failure as a hard read failure before hydration.
- WebGL remains fail-closed until a zero-copy browser staging path exists.

## Save Container Boundary

Current save source constants:

- `SaveBinaryStorage.CurrentVersion = 0x000B`
- `CurrentHeaderSize = 56`
- `LegacyHeaderSize = 44`
- `AlignedSectionHeaderVersion = 0x000B`

Older save-container version wording is stale unless the document explicitly labels it as migration history.

## Required Validation

Before claiming Data Monolith runtime readiness, provide artifacts for:

1. source import
2. bake command
3. H8DM header parse
4. boot/runtime load
5. checksum/hash validation
6. player-build package inclusion

Current static file presence and header parsing are not Unity/player profiler proof.
