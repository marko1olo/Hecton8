# Audio Route Owner Requirement Matrix - 2026-06-05

Status: `PENDING_VERIFICATION / STATIC_AUDIO_QA_ONLY`.
Evidence class: `STATIC_DOC + STATIC_SOURCE + AUDIO_WAVEFORM_QA`.
Runtime proof: absent.
Unity proof: absent.
Listening pass: absent.
Asset mutation: none.

CSV companion: `Docs/AssetAudit/AUDIO_ROUTE_OWNER_REQUIREMENT_MATRIX_20260605.csv`.

## Scope

This matrix routes current music, ambient, player-loop, UI, and VO audio blockers to owner packets. It does not prove runtime mix behavior, import settings, mixer bindings, DSP route, Addressables residency, GC, memory, frame time, or listening quality.

Source inputs:

- `Docs/Audio/audio_asset_ledger.csv`
- `Docs/Audio/audio_remediation_matrix_20260605.csv`
- `Docs/AssetAudit/AUDIO_LISTENING_PASS_QUEUE_20260605.csv`
- `Docs/AssetAudit/AUDIO_PROFILE_ROUTE_MATRIX_20260605.csv`
- `Docs/AssetAudit/AUDIO_DIRECT_REF_DETAIL_20260605.csv`
- `Docs/AssetAudit/AUDIO_FILE_TECHNICAL_PROPERTIES_20260605.csv`
- `Docs/AssetAudit/AUDIO_LOUDNESS_TECHNICAL_PROPERTIES_20260605.csv`

## Owner Routing Rules

- MusicDirector mixer/profile rows start with `ASSET_OWNER_10_MUSICDIRECTOR_AUDIO_ROUTING_PACKET.md`, then `ASSET_OWNER_28_AUDIO_REMEDIATION_EXECUTION_PACKET.md` for P0 remediation execution.
- Player prefab direct refs start with `ASSET_OWNER_08_AUDIO_DIRECT_REF_UNWIRING_PACKET.md`, then `ASSET_OWNER_23_AUDIO_SOURCE_TECHNICAL_REMEDIATION_PACKET.md` for technical source/import constraints.
- Import-policy exceptions start with `ASSET_OWNER_19_AUDIO_IMPORT_AUTHORITY_ADOPTION_PACKET.md`.
- Long beds, loudness, channel/sample-rate risk, and waveform/listening proof start with `ASSET_OWNER_23_AUDIO_SOURCE_TECHNICAL_REMEDIATION_PACKET.md`.
- Runtime mix claims require Unity/runtime capture, Console, listening notes, owner route, and 0 B/frame proof. Static CSVs are not proof.

## Matrix Summary

| Row | Priority | Route | Owner packets | Required proof | Reject if |
|---|---|---|---|---|---|
| AUDROUTE-01 | P0 | MusicDirector mixer routing | 10, 19, 28 | Unity config readback, mixer/DSP route proof, runtime MusicDirector capture, Console clean | null mixer refs remain unexplained or static profile refs are treated as mix proof |
| AUDROUTE-02 | P0 | Underwater Ambient source-cleared prefab refs | 08, 23, 28 | prefab readback, owner/load/release or removal ledger, runtime playback or absence proof, 0 B/frame, listening notes | Unity readback still shows unmanaged direct prefab ref or retained long bed masks warning/player cues |
| AUDROUTE-03 | P0 | Dive splash source-cleared prefab refs | 08, 23, 28 | prefab readback, removal/duplicate disposition, runtime playback or absence proof, 0 B/frame | Unity readback still shows unmanaged refs or replacement/absence route is unexplained |
| AUDROUTE-04 | P0 | Player breath loop | 23, 28 | suit-route listening pass, import readback, player-loop exception proof, 0 B/frame | loop is too hot, loops badly, streams as generic SFX, or masks warnings |
| AUDROUTE-05 | P0 | Suit interior loop | 23, 28 | first-exit/shallow listening pass, import readback, ducking proof | bed remains too loud or has no player-body route owner |
| AUDROUTE-06 | P1 | Swimming surface loop | 23, 28 | listening pass, latency/start proof, import exception decision | start jitter, inaudible movement feedback, or generic streaming SFX |
| AUDROUTE-07 | P1 | Dense ambient bed | 23, 28 | active-bank limit, warning ducking proof, memory proof | ambience masks warnings or first-exit/shallow route cues |
| AUDROUTE-08 | P1 | Low steady ambience | 23, 28 | listening pass, import readback, active-bank owner proof | unclear route identity, poor import quality, or no release owner |
| AUDROUTE-09 | P1 | Shelf loud long bed | 10, 23, 28 | MusicDirector capture, listening notes, pause/window proof | constant emotional blanket or over-compressed bed masks cues |
| AUDROUTE-10 | P1 | Abyss long drone | 10, 23, 28 | tension/cadence proof, depth-gated playback, listening notes | drone plays outside owned abyss/deep route or becomes always-on tension |
| AUDROUTE-11 | P1 | Danger stinger | 10, 23, 28 | stinger cooldown, event owner, mix priority, listening notes | repeats too often, masks warnings, or triggers without event ownership |
| AUDROUTE-12 | P2 | UI click | 17, 23, 28 | HUD/menu listening pass, UI mix proof, runtime 0 B/frame | inaudible, allocates, or bypasses owned UI/audio path |
| AUDROUTE-13 | P2 | VO stub sanity | 19, 23 | import/readback only if VO owner touches it; localization/subtitle proof for final VO | placeholder stub treated as final VO loudness or final localization proof |

## Low / Middle / High / Ultra Consequences

- Low/compact: admit only critical player, warning, UI, and route cues. Music breadth and decorative ambience remain narrow until ownership and masking proof exist.
- Middle: normal profile breadth may start only after mixer route, direct-ref, ducking, and listening proof.
- High: spend budget on cleaner transitions, profile-specific stinger subsets, ambience layering, and silence windows after cue priority proof.
- Ultra: denser music/stinger/ambient layering is allowed only if warning hierarchy, player loop clarity, owner routes, cue IDs, load/release, and 0 B/frame proof remain intact.

Final status: `PENDING_VERIFICATION`.
