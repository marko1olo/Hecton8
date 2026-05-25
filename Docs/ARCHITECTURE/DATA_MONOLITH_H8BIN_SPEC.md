# Data Monolith H8BIN Spec

Date: 2026-05-23
Status: PENDING VERIFICATION
Owner: X_012 DOCUMENTATION_CLEANUP_AND_ACTUALIZATION_ENGINE
Evidence class: STATIC_DOC / STATIC_SOURCE

## Current Source Boundary

| Item | Value |
|---|---|
| runtime payload | `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` |
| current workspace payload status | present; `1,064,384` bytes |
| H8DM header size | `64` bytes |
| H8DM directory size | `64` bytes |
| H8DM format version | `1` |
| H8DM schema hash | `0x58303032` |
| source data root | `Assets/_SourceData/DataMonolith` |
| bake output | `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` |
| editor menu | `Hecton8/Data Monolith/Bake Static Data` |

The Data Monolith stores immutable static data. It is not the save container.

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

Current static file presence is not Unity/player proof.
