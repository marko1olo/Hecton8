# Large Source Owner Review - 2026-06-05

Status: `PENDING VERIFICATION`.
Evidence class: `STATIC_SOURCE`.
Scope: compact owner review for large texture/audio source buckets derived from `ASSET_GUID_REFERENCE_MATRIX_20260605.csv`.

CSV companion: `Docs/AssetAudit/LARGE_SOURCE_OWNER_REVIEW_20260605.csv`.

This file is not deletion authorization, import acceptance, material proof, Addressables proof, audio runtime proof, VRAM proof, or listening proof. It exists to stop large texture/audio rows from being hidden inside broad GUID tables.

## Static Findings

| Review ID | Bucket | Count | Total MiB | Owner | Immediate risk |
|---|---|---:|---:|---|---|
| `LSR-01` | P0 source-cleared large audio | 1 | 32.47 | Audio lifecycle/source owner | Prior `Underwater Ambient.wav` prefab refs are source-cleared; large route-critical cue still needs Unity readback and route/removal proof. |
| `LSR-02` | ScifiFacility material-reachable large textures | 29 | 597.84 | Texture/material/streaming owner | Non-project textures are material-reachable and too large to ignore. |
| `LSR-03` | Other large texture/audio rows | 13 | 122.62 | Texture/material/streaming owner | Mixed first-party texture/source rows need owner classification. |
| `LSR-04` | Unreferenced large audio | 11 | 248.78 | Audio lifecycle/source owner | Atmos and breathing WAV sources are unreferenced in static GUID text only. |
| `LSR-05` | ScifiFacility unreferenced large textures | 4 | 65.60 | Third-party integrity and texture owner | Vendor-path textures need quarantine review before any strip. |

## Representative Paths

- P0 audio: `Assets/_Project/Audio/Underwater Ambient.wav`.
- Unreferenced large audio: `Assets/_Project/Audio/Atmos 1.wav`, `Assets/_Project/Audio/Atmos 2.wav`, `Assets/_Project/Audio/Atmos 3.wav`, `Assets/_Project/Audio/Atmos 4.wav`, `Assets/_Project/Audio/Atmos 5.wav`, loop variants, and `Assets/_Project/Audio/Breathing/inside suit sounds (too loud).wav`.
- Material-reachable ScifiFacility examples: `Assets/ScifiFacility/Textures/Base_dirt_roughness.png`, `Base_02_dirt_roughness.png`, `BrushedMetal_dirt_roughness.png`, `DetailSheet_normal.png`, `Labels_basecolor.tga`.
- Other large texture examples: `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/TOP1Rock028_2K-JPG_NormalGL.jpg` and imported coral detail/albedo rows.

## Rules

- Static `UNREFERENCED_STATIC_ASSET` is not safe-delete proof.
- Do not delete `.meta` files or assets from this review.
- Do not strip vendor-path content without third-party integrity review.
- Do not import or promote large source textures as final art from size or reachability alone.
- Do not accept direct large audio refs without owner, load phase, release phase, route, import readback, memory proof, and runtime mix proof.

## Required Next Work

- Audio owner must use `AUDIO_P0_STATIC_EXECUTION_REFINEMENT_20260605.csv` for `Underwater Ambient.wav` and `AUDIO_SOURCE_TECHNICAL_OWNER` review for Atmos/breathing WAV rows.
- Texture/material owner must use material readback and import-role review before deciding whether ScifiFacility rows are product-route candidates, vendor leftovers, or quarantine candidates.
- Addressables owner may not assign groups or keys until Unity readback proves active route and lifecycle owner.
- Cleanup owner may only prepare quarantine review notes. Deletion requires code/editor/Addressables/Resources/package search and explicit owner approval.

## Regression Model

- CPU: static review only; no runtime CPU change.
- GC: no runtime code touched; no `0 B/frame` claim.
- Memory/VRAM: source sizes indicate risk only. Runtime residency is unproved.
- Cadence: no runtime cadence changed.
- Correctness: large rows now have owner buckets; no asset is accepted or deleted.

Final status: `PENDING VERIFICATION`.
