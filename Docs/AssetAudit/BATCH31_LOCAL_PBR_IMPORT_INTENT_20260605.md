# Batch31 Local PBR Import Intent

Evidence class: `STATIC_SOURCE`.
Evidence scope: `STATIC_IMAGE_IMPORT_INTENT`.

Unity was not run. No `Assets` files, `.meta` files, materials, prefabs, scenes, Addressables groups, or project settings were edited.
This artifact is an importer-facing static contract, not visual acceptance and not runtime readiness.

## Summary

- Source index: `Docs/GeneratedAssets/Batch31_LocalPBR/Batch31_LocalPBR_INDEX.json`
- Packages: 3
- Texture rows: 21
- Runtime import candidate rows: 6
- Blocked rows: 3
- Error rows: 0
- Review rows: 0
- Static-pass rows: 18
- Channel-semantics blocked packages: 3

## Channel Contract Block

- Source package contract: `Source package calls map MRAO; playbook allows R=Metallic G=Roughness/Smoothness B=AO A=Emission/Wetness`
- Target route contract: `_MaskMap ARM R=AO G=Roughness B=Metallic A=Emission/default1`
- Required owner decision before Unity promotion: choose shader target and repack or relabel packed masks. Do not import Batch31 `MRAOSource` as `_MaskMap` by name alone.

## Package Verdicts

| Verdict | Package | Runtime rows | Blocked rows | Error rows | Review rows | Issues | Warnings |
|---|---|---:|---:|---:|---:|---|---|
| BLOCKED | `TX_B31_WetBasaltShoreline_1429` | 2 | 1 | 0 | 0 | `` | `blocked_channel_contract_mrao_vs_arm;not_unity_imported;not_visual_acceptance` |
| BLOCKED | `TX_B31_PhoticSeabedSubstrate_2102` | 2 | 1 | 0 | 0 | `` | `blocked_channel_contract_mrao_vs_arm;not_unity_imported;not_visual_acceptance` |
| BLOCKED | `TX_B31_PhoticShellSandSubstrate_2102` | 2 | 1 | 0 | 0 | `` | `blocked_channel_contract_mrao_vs_arm;not_unity_imported;not_visual_acceptance` |

## Import Intent

| Verdict | Package | Role | Size | sRGB | Type | Standalone | Android | Low/Middle/High/Ultra | Path | Warnings | Issues |
|---|---|---|---:|---:|---|---|---|---|---|---|---|
| PASS_STATIC | `TX_B31_WetBasaltShoreline_1429` | Albedo | 1024x1024 | 1 | Default | BC7 | ASTC_6x6 | 1024/2048/2048/2048 | `Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_WetBasaltShoreline_1429/TX_B31_WetBasaltShoreline_1429_AlbedoSource.png` | `` | `` |
| PASS_STATIC | `TX_B31_WetBasaltShoreline_1429` | HeightSource | 1024x1024 | 0 | Default | BC4_or_BC7_after_shader_owner | ASTC_6x6_after_shader_owner | 1024/2048/2048/2048 | `Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_WetBasaltShoreline_1429/TX_B31_WetBasaltShoreline_1429_HeightSource.png` | `` | `` |
| BLOCKED | `TX_B31_WetBasaltShoreline_1429` | PackedMask | 1024x1024 | 0 | Default | BC7 | ASTC_6x6 | 1024/2048/2048/2048 | `Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_WetBasaltShoreline_1429/TX_B31_WetBasaltShoreline_1429_MRAOSource.png` | `mrao_R_channel_flat_review` | `blocked_channel_semantics_mrao_vs_arm` |
| PASS_STATIC | `TX_B31_WetBasaltShoreline_1429` | Normal | 1024x1024 | 0 | NormalMap | BC5 | ASTC_6x6 | 1024/2048/2048/2048 | `Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_WetBasaltShoreline_1429/TX_B31_WetBasaltShoreline_1429_NormalSource.png` | `` | `` |
| PASS_STATIC | `TX_B31_WetBasaltShoreline_1429` | NormalTilePreview | 1024x1024 | 0 | ReferenceOnly | DO_NOT_IMPORT_AS_RUNTIME_TEXTURE | DO_NOT_IMPORT_AS_RUNTIME_TEXTURE | 0/0/0/0 | `Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_WetBasaltShoreline_1429/TX_B31_WetBasaltShoreline_1429_Normal_tile2x2.png` | `` | `` |
| PASS_STATIC | `TX_B31_WetBasaltShoreline_1429` | SourceReference | 1024x1024 | 1 | ReferenceOnly | DO_NOT_IMPORT_AS_RUNTIME_TEXTURE | DO_NOT_IMPORT_AS_RUNTIME_TEXTURE | 0/0/0/0 | `Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_WetBasaltShoreline_1429/TX_B31_WetBasaltShoreline_1429_SourceCrop.png` | `` | `` |
| PASS_STATIC | `TX_B31_WetBasaltShoreline_1429` | TilePreview | 1024x1024 | 1 | ReferenceOnly | DO_NOT_IMPORT_AS_RUNTIME_TEXTURE | DO_NOT_IMPORT_AS_RUNTIME_TEXTURE | 0/0/0/0 | `Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_WetBasaltShoreline_1429/TX_B31_WetBasaltShoreline_1429_Albedo_tile2x2.png` | `` | `` |
| PASS_STATIC | `TX_B31_PhoticSeabedSubstrate_2102` | Albedo | 1024x1024 | 1 | Default | BC7 | ASTC_6x6 | 1024/2048/2048/2048 | `Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_PhoticSeabedSubstrate_2102/TX_B31_PhoticSeabedSubstrate_2102_AlbedoSource.png` | `` | `` |
| PASS_STATIC | `TX_B31_PhoticSeabedSubstrate_2102` | HeightSource | 1024x1024 | 0 | Default | BC4_or_BC7_after_shader_owner | ASTC_6x6_after_shader_owner | 1024/2048/2048/2048 | `Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_PhoticSeabedSubstrate_2102/TX_B31_PhoticSeabedSubstrate_2102_HeightSource.png` | `` | `` |
| BLOCKED | `TX_B31_PhoticSeabedSubstrate_2102` | PackedMask | 1024x1024 | 0 | Default | BC7 | ASTC_6x6 | 1024/2048/2048/2048 | `Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_PhoticSeabedSubstrate_2102/TX_B31_PhoticSeabedSubstrate_2102_MRAOSource.png` | `mrao_R_channel_flat_review` | `blocked_channel_semantics_mrao_vs_arm` |
| PASS_STATIC | `TX_B31_PhoticSeabedSubstrate_2102` | Normal | 1024x1024 | 0 | NormalMap | BC5 | ASTC_6x6 | 1024/2048/2048/2048 | `Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_PhoticSeabedSubstrate_2102/TX_B31_PhoticSeabedSubstrate_2102_NormalSource.png` | `` | `` |
| PASS_STATIC | `TX_B31_PhoticSeabedSubstrate_2102` | NormalTilePreview | 1024x1024 | 0 | ReferenceOnly | DO_NOT_IMPORT_AS_RUNTIME_TEXTURE | DO_NOT_IMPORT_AS_RUNTIME_TEXTURE | 0/0/0/0 | `Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_PhoticSeabedSubstrate_2102/TX_B31_PhoticSeabedSubstrate_2102_Normal_tile2x2.png` | `` | `` |
| PASS_STATIC | `TX_B31_PhoticSeabedSubstrate_2102` | SourceReference | 1024x1024 | 1 | ReferenceOnly | DO_NOT_IMPORT_AS_RUNTIME_TEXTURE | DO_NOT_IMPORT_AS_RUNTIME_TEXTURE | 0/0/0/0 | `Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_PhoticSeabedSubstrate_2102/TX_B31_PhoticSeabedSubstrate_2102_SourceCrop.png` | `` | `` |
| PASS_STATIC | `TX_B31_PhoticSeabedSubstrate_2102` | TilePreview | 1024x1024 | 1 | ReferenceOnly | DO_NOT_IMPORT_AS_RUNTIME_TEXTURE | DO_NOT_IMPORT_AS_RUNTIME_TEXTURE | 0/0/0/0 | `Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_PhoticSeabedSubstrate_2102/TX_B31_PhoticSeabedSubstrate_2102_Albedo_tile2x2.png` | `` | `` |
| PASS_STATIC | `TX_B31_PhoticShellSandSubstrate_2102` | Albedo | 1024x1024 | 1 | Default | BC7 | ASTC_6x6 | 1024/2048/2048/2048 | `Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_PhoticShellSandSubstrate_2102/TX_B31_PhoticShellSandSubstrate_2102_AlbedoSource.png` | `` | `` |
| PASS_STATIC | `TX_B31_PhoticShellSandSubstrate_2102` | HeightSource | 1024x1024 | 0 | Default | BC4_or_BC7_after_shader_owner | ASTC_6x6_after_shader_owner | 1024/2048/2048/2048 | `Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_PhoticShellSandSubstrate_2102/TX_B31_PhoticShellSandSubstrate_2102_HeightSource.png` | `` | `` |
| BLOCKED | `TX_B31_PhoticShellSandSubstrate_2102` | PackedMask | 1024x1024 | 0 | Default | BC7 | ASTC_6x6 | 1024/2048/2048/2048 | `Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_PhoticShellSandSubstrate_2102/TX_B31_PhoticShellSandSubstrate_2102_MRAOSource.png` | `mrao_R_channel_flat_review` | `blocked_channel_semantics_mrao_vs_arm` |
| PASS_STATIC | `TX_B31_PhoticShellSandSubstrate_2102` | Normal | 1024x1024 | 0 | NormalMap | BC5 | ASTC_6x6 | 1024/2048/2048/2048 | `Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_PhoticShellSandSubstrate_2102/TX_B31_PhoticShellSandSubstrate_2102_NormalSource.png` | `` | `` |
| PASS_STATIC | `TX_B31_PhoticShellSandSubstrate_2102` | NormalTilePreview | 1024x1024 | 0 | ReferenceOnly | DO_NOT_IMPORT_AS_RUNTIME_TEXTURE | DO_NOT_IMPORT_AS_RUNTIME_TEXTURE | 0/0/0/0 | `Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_PhoticShellSandSubstrate_2102/TX_B31_PhoticShellSandSubstrate_2102_Normal_tile2x2.png` | `` | `` |
| PASS_STATIC | `TX_B31_PhoticShellSandSubstrate_2102` | SourceReference | 1024x1024 | 1 | ReferenceOnly | DO_NOT_IMPORT_AS_RUNTIME_TEXTURE | DO_NOT_IMPORT_AS_RUNTIME_TEXTURE | 0/0/0/0 | `Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_PhoticShellSandSubstrate_2102/TX_B31_PhoticShellSandSubstrate_2102_SourceCrop.png` | `` | `` |
| PASS_STATIC | `TX_B31_PhoticShellSandSubstrate_2102` | TilePreview | 1024x1024 | 1 | ReferenceOnly | DO_NOT_IMPORT_AS_RUNTIME_TEXTURE | DO_NOT_IMPORT_AS_RUNTIME_TEXTURE | 0/0/0/0 | `Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_PhoticShellSandSubstrate_2102/TX_B31_PhoticShellSandSubstrate_2102_Albedo_tile2x2.png` | `` | `` |

## Scalability Consequences

- Low: runtime candidates are capped at 1024, one packed mask sampler, mipmaps on, read/write off.
- Middle: runtime candidates may use 2048 where route importance justifies memory.
- High: same sampler count; saved time should buy stronger normal/detail/material response, not extra gameplay truth.
- Ultra: richer near-field shader/detail can be layered later, but Batch31 source identity and channel contract must stay stable.

## Residual Risk

- Static checksum and image-channel inspection do not prove Unity import settings, material binding, compression quality, route screenshots, memory residency, frame time, or GC.
- MRAO/ARM convention is unresolved in current docs; this is intentionally left as a blocker, not guessed.
