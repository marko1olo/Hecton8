# Audio Profile Route Matrix - 2026-06-05

Status: `STATIC_MATRIX_ONLY`.
Evidence boundary: `STATIC_DOC`, `STATIC_SOURCE`, `AUDIO_WAVEFORM_QA`.

No Unity run, import edit, prefab edit, build, play mode, profiler, listening pass, or `Assets` mutation was performed. This file maps static MusicDirector profiles, cue families, and audio route risks so the next audio owner knows where ownership is blocked.

CSV companion: `Docs/AssetAudit/AUDIO_PROFILE_ROUTE_MATRIX_20260605.csv`.

## Mandates Followed

- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`

## Static Inputs

- `Docs/AssetAudit/README.md`
- `Docs/AssetAudit/AUDIO_ASSET_TAXONOMY_20260605.md`
- `Docs/AssetAudit/AUDIO_ASSET_TAXONOMY_20260605.csv`
- `Docs/Audio/audio_profile_usage_20260605.csv`
- `Docs/Audio/audio_asset_ledger.csv`
- `Docs/Audio/audio_remediation_matrix_20260605.csv`
- `Docs/AssetAudit/AUDIO_LISTENING_PASS_QUEUE_20260605.csv`
- `Docs/AssetAudit/AUDIO_IMPORT_POLICY_EXCEPTION_TABLE_20260605.csv`
- `Docs/AssetAudit/AUDIO_PROFILE_USAGE_REVIEW_20260605.md`
- `Docs/AssetAudit/AUDIO_WAVEFORM_REVIEW_20260605.md`

## Evidence Summary

| Evidence Source | Static Count |
|---|---:|
| `audio_profile_usage_20260605.csv` rows | 227 |
| MusicDirector profile route rows | 10 |
| MusicDirector profile cue rows | 150 |
| Profile bleed rows | 36 |
| Null config rows | 2 |
| Direct `Player.prefab` AudioClip refs | 28 |
| `audio_asset_ledger.csv` rows | 138 |
| Ledger music rows | 84 |
| Ledger ambient rows | 12 |
| Ledger player-loop rows | 5 |
| Ledger short SFX rows | 30 |
| Ledger UI rows | 5 |
| Ledger VO rows | 2 |
| Remediation rows | 58 |
| Listening queue rows | 13 |

Counts are static source/doc evidence only. They do not prove runtime routing, import state, Addressables ownership, memory behavior, mixer output, cue cadence, or no-allocation playback.

## Route Risk Findings

### Null MusicDirector Mixer Refs

`MusicDirectorConfig_Global.asset` has null `_musicMixerGroup` and `_stingerMixerGroup` rows in static config evidence. That blocks mix route judgment for every MusicDirector profile. The next owner must either assign approved mixer groups through Unity after gate clearance or document an owned native/DSP bypass with phase, owner, proof target, and readback plan.

Rejected route: leaving null mixer fields unexplained while treating profile cue refs as mix proof.

### Repeated Stingers

Static profile evidence lists 11 repeated stinger GUID groups. Recovery stingers repeat across 8 profiles; danger stingers repeat across 7 profiles; discovery stingers repeat across 6 profiles; hallucination repeats across 3 profiles. Reuse may be intentional, but it is not cadence proof.

Required owner action: mark reuse intentional or specialize replacements, then prove cooldown, event owner, and warning-priority behavior.

### Long Music Beds

Seven profile long-bed rows are `>=300s`. Waveform QA flags `shelf_1_Abandoned Depths.ogg` as loud/dense and `abyss_3_Deep Trench Drone.ogg` as high-peak long drone. Static refs cannot prove MusicDirector pause windows or silence discipline.

Rejected route: constant shelf/abyss/cave bed that masks player/system cues or replaces route-driven sound discipline.

### Direct Player Prefab Refs

Current `Player.prefab` static scan has 24 direct `AudioClip` refs: footstep sets and UI feedback. Prior `Underwater Ambient.wav` and `dive_splash.wav` direct refs are source-cleared in the working tree but pending Unity prefab readback. Direct serialization is not Addressables ownership, release proof, playback-route proof, or audio-thread proof.

Required owner action: classify every direct ref by cue family, then write owner/load/release/playback route or a scoped exception with proof target.

### Player-Loop Exception Risk

Breath, suit, and swim clips are player-loop candidates, not generic SFX. The policy table marks the duration default as insufficient for first-person continuity. Waveform QA flags the breath loop as hot and the suit loop as loudness debt by filename.

Required owner action: define low-latency route, import exception, release route, latency proof, and listening proof per loop.

### Placeholder VO

`VOStub_Chen_Log01_EN.wav` and `VOStub_Chen_Log01_RU.wav` are placeholder rows. Waveform QA shows the EN stub is too small to inform final dialogue loudness. These rows cannot drive final VO import, localization, subtitle timing, mix, or accessibility policy.

Rejected route: treating stub loudness or duration as final VO evidence.

### Import-Policy Conflict

Root audio text, STRM duration rules, and the hybrid exception table are not the same authority. The current static recommendation is useful planning input, not permission for broad import mutation. Music, ambience, player loops, short SFX, UI, and VO need a stable owner decision before import edits.

Rejected route: import mutation from static recommendation alone.

## Profile And Cue Matrix

| Priority | Route/Profile | Cue Family | Current Disposition | Evidence | Blocking Risk | Owner Next Action |
|---|---|---|---|---|---|---|
| P0 | MusicDirectorConfig_Global | Mixer routing | `BLOCKED_NULL_MIXER_ROUTE` | STATIC_SOURCE | Music/stinger outputs cannot be judged while mixer refs are null. | Assign mixer groups or document owned native/DSP bypass. |
| P0 | Player.prefab direct clip route | Direct prefab AudioClip refs | `BLOCKED_DIRECT_PREFAB_AUDIO_REFS` | STATIC_SOURCE | Direct refs do not prove owner, release, playback route, or hot-path behavior. | Classify every direct ref and write route/exception proof target. |
| P0 | `music.profile.shallow` | First-exit and photic shallow profile | `STATIC_REF_ONLY_BLOCKED_BY_ROUTE` | STATIC_SOURCE | First-20 shallow route can be masked by constant beds or stingers. | Prove shallow transitions, silence windows, cooldown, and cue audibility. |
| P1 | `music.profile.shelf` | Shelf and medium-depth profile | `RISK_LONG_BED_PROFILE` | AUDIO_WAVEFORM_QA | Loud long beds lack pause-window proof. | Prove cadence, transitions, and cue priority. |
| P1 | `music.profile.abyss` | Abyss tension profile | `RISK_TENSION_OWNERSHIP` | AUDIO_WAVEFORM_QA | Long drone may play outside owned pressure/tension route. | Prove abyss-only gating and transition cadence. |
| P1 | `music.profile.cave` | Cave pressure profile | `RISK_PRESSURE_BED_CADENCE` | STATIC_SOURCE | Long pressure beds and shared stingers may become continuous cave noise. | Prove cave entry/exit, bed spacing, cooldown, and warning audibility. |
| P1 | `music.profile.thermal` | Thermal route profile | `STATIC_REF_ONLY_NEEDS_EVENT_GATE` | STATIC_SOURCE | Shared stingers may erase thermal route identity. | Prove thermal trigger conditions, cooldown, and route identity. |
| P1 | `music.profile.combat` | Combat profile | `RISK_WARNING_PRIORITY` | STATIC_SOURCE | Combat music can mask damage, oxygen, pressure, and tool warnings. | Prove warning priority, ducking, tension entry/exit, and cooldown. |
| P1 | `music.profile.fallback` | Fallback ambient profile | `RISK_FALLBACK_MASKING` | STATIC_SOURCE | Fallback can become hidden always-on content. | Define legal trigger, exit route, and non-interference rules. |
| P2 | `music.profile.main_menu` | Main menu profile | `STATIC_REF_ONLY_NEEDS_ROUTE_PROOF` | STATIC_SOURCE | Static refs do not prove menu load/release or transition route. | Prove menu music load, release, transition, and mixer route. |
| P2 | `music.profile.prologue` | Prologue profile | `STATIC_REF_ONLY_NEEDS_HANDOFF_PROOF` | STATIC_SOURCE | Prologue cue and bleed refs lack handoff proof. | Prove prologue entry/exit and transition to world route. |
| P2 | `music.profile.base` | Base interior profile | `STATIC_REF_ONLY_NEEDS_INTERIOR_GATE` | STATIC_SOURCE | Base music may mask alarms or leak outside base context. | Prove base-entry gate, alarm ducking, and world-profile exit. |
| P1 | Repeated stinger library | Discovery, danger, recovery stingers | `RISK_REPEATED_STINGER_SPAM` | STATIC_SOURCE | Reuse can erase profile identity and occupy warning space. | Mark reuse intentional or replace; prove cooldown and priority. |
| P1 | Long MusicDirector beds | Exploration and tension long beds | `RISK_CONSTANT_MUSIC_BED` | AUDIO_WAVEFORM_QA | Long beds can flatten route emotion and mask systems. | Prove pause windows, crossfades, tension ownership, and audibility. |
| P0 | Ambient bank route | Long ambience and pressure beds | `BLOCKED_IMPORT_AND_DUCKING_PROOF` | AUDIO_WAVEFORM_QA | Q45 rows conflict with Q70 target; dense beds can mask cues. | Resolve active-bank route, import exception, ducking, release, and memory proof. |
| P0 | Player loop route | Breath, suit, and swim loops | `BLOCKED_PLAYER_LOOP_EXCEPTION_DECISION` | AUDIO_WAVEFORM_QA | Player loops can start late, sound too hot, or mask warnings. | Define low-latency route, import exception, release path, and listening proof. |
| P1 | Short SFX route | Footsteps, splash, impacts, bubbles, thruster one-shots | `PENDING_OWNER_ROUTE` | STATIC_SOURCE | SFX refs lack owner, mono/import readback, playback route, and no-allocation proof. | Fill owner ledger, prefab readback, import readback, and playback route proof. |
| P1 | UI feedback route | HUD/menu/instrument feedback | `PENDING_UI_AUDIBILITY_AND_OWNER` | AUDIO_WAVEFORM_QA | UI feedback may be inaudible or unowned. | Perform UI listening pass, import readback, ducking proof, and no-allocation route proof. |
| P1 | VO stub route | Placeholder VO stubs | `PLACEHOLDER_BLOCKED` | AUDIO_WAVEFORM_QA | Stubs cannot define final VO policy. | Keep placeholder flag; define final VO route only when final lines exist. |
| P0 | Import-policy authority state | Music, ambience, player loops, SFX, UI, VO | `PENDING_AUTHORITY_DECISION` | STATIC_DOC | Static docs conflict; broad import mutation is unsafe. | Get stable owner decision before import edits. |
| P0 | Listening and remediation order | P0/P1/P2 queue | `STATIC_QUEUE_ONLY` | STATIC_DOC | Taste checks before ownership/routing/policy closure give false signal. | Execute queue in order after process gate clears and record proof artifacts. |

## Scalability Consequences

- Low: keep one active ambient/music context where possible, admit breath/warnings/UI before decorative beds, block unmanaged direct-ref expansion, and avoid generic streaming SFX.
- Middle: add ambience/music breadth only after owner route, ducking, and memory proof; do not skip shallow/surface readability.
- High: spend headroom on profile transitions, stinger variety, reverb, and richer mix only after cooldown and warning-priority proof.
- Ultra: extend density and fidelity without changing cue ownership, Addressables keys, release order, save identity, or critical cue priority.

## Regression Model

- CPU: no runtime code touched; no CPU improvement or regression claimed.
- GC: no runtime code touched; no no-allocation claim.
- Memory/residency: static load-type and owner risk only; no resident memory proof.
- Cadence: static long-bed/stinger risks only; no runtime cadence changed.
- Correctness: route ownership and blocker ordering are clearer; runtime proof remains absent.

Final status: `STATIC_MATRIX_ONLY`.
