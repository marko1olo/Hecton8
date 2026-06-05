# Asset Static Hygiene Sweep - 2026-06-05

Status: `PENDING VERIFICATION`.
Evidence class: `STATIC_DOC` / `STATIC_SOURCE`.
Scope: `Docs/AssetAudit`, `Docs/Audio`, `taskslocal/asset_system_20260605`, and `Docs/Reports/AssetSystem_20260605`.

This sweep checks claim hygiene and queue coherence only. It does not prove Unity import, material binding, Addressables residency, runtime audio, frame time, VRAM, GC, or visual quality.

## Commands

- `rg -n --glob '*.md' --glob '*.csv' "\b(VERIFIED|Verified|verified|READY|Ready|ready|COMPLETE|Complete|complete|accepted|acceptance|0 GC|0 B/frame|runtime-ready|runtime ready|visual acceptance|Unity acceptance|Addressables readiness|PENDING VERIFICATION)\b" Docs/AssetAudit Docs/Audio Docs/Reports/AssetSystem_20260605 taskslocal/asset_system_20260605`
- `rg -n --glob '*.md' --glob '*.csv' "STATIC VERIFIED|runtime-ready|runtime ready|VISUAL PASS|READY_FOR|READY|VERIFIED|COMPLETE|DONE|0 B/frame|Addressables-ready|Unity-verified|Unity verified|final acceptance" Docs/AssetAudit Docs/Audio Docs/Reports/AssetSystem_20260605 taskslocal/asset_system_20260605`
- `Import-Csv Docs/Audio/audio_asset_ledger.csv | Group-Object class`
- `Import-Csv Docs/Audio/audio_asset_ledger.csv | Group-Object owner`
- `Import-Csv Docs/Audio/audio_asset_ledger.csv | Group-Object addressable_group`
- `Import-Csv Docs/AssetAudit/TEXTURE_CANDIDATE_DISPOSITION_20260605.csv | Group-Object disposition`
- `Import-Csv Docs/AssetAudit/ASSET_ACTION_QUEUE_20260605.csv | Group-Object priority`
- `Import-Csv Docs/AssetAudit/ASSET_ACTION_QUEUE_20260605.csv | Group-Object domain`

## Claim Hygiene Result

- No asset-front doc in this scope claimed Unity/runtime/material/visual readiness from static scans.
- `PENDING VERIFICATION` is consistently present on asset-front review docs.
- Hits for `VERIFIED`, `READY`, `COMPLETE`, `0 B/frame`, `acceptance`, and `Addressables readiness` were negative rules, future proof requirements, cue filenames, or blockers.
- No `STATIC VERIFIED`, `VISUAL PASS`, `Unity verified`, `runtime-ready`, or `final acceptance` claim was found in the scanned scope.

Residual risk: `rg` is text evidence only. Concurrent workers can add new files after this sweep.

## Ledger Snapshot

Audio ledger class counts:

| Class | Count |
|---|---:|
| ambient | 12 |
| music | 84 |
| player_loop | 5 |
| sfx | 30 |
| ui | 5 |
| voice | 2 |

Audio ownership:

| Field | Result |
|---|---|
| owner | `PENDING_OWNER` for 138 rows |
| addressable_group | `PENDING_ADDRESSABLES` for 138 rows |
| addressable_key | `PENDING_ADDRESSABLES` for 138 rows |

Texture disposition counts:

| Disposition | Count |
|---|---:|
| `CANDIDATE_BLOCKED_BY_MATERIAL_PROOF` | 49 |
| `REJECTED_VISIBLE_SUPPORT_ONLY` | 1 |
| `SOURCE_CANDIDATE_BLOCKED_BY_READBACK` | 6 |
| `SOURCE_CANDIDATE_NEEDS_CLEAN_PBR` | 10 |
| `SOURCE_ONLY_NOT_IMPORTED` | 50 |
| `SOURCE_PROTOTYPE_NOT_FINAL` | 1 |
| `UI_SOURCE_ATLAS_PROOF_PENDING` | 7 |
| `UNASSIGNED_STATIC_SOURCE` | 66 |

Action queue counts:

| Priority | Count |
|---|---:|
| P0 | 4 |
| P1 | 5 |
| P2 | 2 |

Action queue domains:

| Domain | Count |
|---|---:|
| audio_lifecycle | 1 |
| audio_routing | 1 |
| flora_materials | 1 |
| music_cadence | 1 |
| scene_flow_context | 1 |
| sky_aegir | 1 |
| sky_slots | 1 |
| terrain_pbr | 1 |
| texture_import | 1 |
| ui_sprite | 1 |
| water_visual | 1 |

## Current P0 Blockers

| Domain | Blocker | Required Owner Route |
|---|---|---|
| water_visual | Rejected foam source is serialized-reachable through active world/ocean users. | Read back effective foam slots, author route-owned foam/contact RGBA masks, prove Crest contribution, replace through Unity API only. |
| flora_materials | `WorldProceduralProxy` flora/coral/kelp materials are serialized in active world scene. | Read renderer users, replace with route-owned photic materials only after visual proof, keep proxy pools rejected. |
| audio_routing | `MusicDirectorConfig_Global.asset` has null music and stinger mixer groups. | Assign or define correct mixer route through Unity owner, then prove MusicDirector runtime routing and stinger behavior. |
| audio_lifecycle | `Player.prefab` has direct AudioClip refs without Addressables/release proof. | Classify owner route for every direct ref, move heavy/loop clips under owned load/release or document runtime exception. |

## Regression Model

- CPU: no runtime code changed. Static scan only.
- GC: no runtime code changed. No `0 B/frame` claim.
- Memory/VRAM: no residency measured. Static source/CSV only.
- Cadence: no runtime cadence changed.
- Correctness: improves orchestration hygiene by separating blocked action rows from runtime acceptance.
