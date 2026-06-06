# Audio Asset Taxonomy - 2026-06-05

Status: `PENDING_PROOF`
Evidence boundary: `STATIC_DOC`, `STATIC_SOURCE`, `AUDIO_WAVEFORM_QA` only.

No Unity run, import edit, prefab edit, build, play mode, profiler, listening pass, or asset mutation was performed for this taxonomy. Static rows prove file/reference/document presence only.

## Mandates Followed

- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`

## Ownership Boundary

- Taxonomy owner: `Docs/AssetAudit`, static planning only.
- Execution owners required: MusicDirector owner, Player/audio lifecycle owner, ambient-bank owner, UI/audio owner, Audio import/lifecycle owner, Streaming/Addressables owner, localization/narrative owner.
- No taxonomy row is an import decision. Import changes require stable policy, Unity import readback, runtime route proof, memory/residency proof, listening notes, and 0 B/frame proof.

## Static Taxonomy Summary

- Music: 84 rows, all `Streaming` + `Vorbis` + quality `0.7`, all referenced by MusicDirector profiles in static profile evidence.
- Ambient loops: 12 rows; 8 `Streaming` at quality `0.45`, 4 `CompressedInMemory` at quality `0.7`.
- Player/suit/swim loops: 5 rows; 4 `Streaming` at quality `0.45`, 1 `CompressedInMemory` at quality `0.7`; one suit row remains placeholder-marked.
- Short SFX: 30 rows; no generic streaming SFX in the static ledger; 20 P1 remediation rows are direct `Player.prefab` refs.
- UI SFX: 5 rows; mixed `CompressedInMemory` and `DecompressOnLoad`; 4 P1 remediation rows are direct `Player.prefab` refs.
- VO stubs: 2 placeholder rows at quality `0.22`; they do not establish final VO, localization, subtitle, or loudness policy.

## P0 Listening And Remediation Order

1. MusicDirector mixer routing: `MusicDirectorConfig_Global.asset` has null `_musicMixerGroup` and `_stingerMixerGroup`; routing proof is blocked before music taste can be judged.
2. `Underwater Ambient.wav` prior direct refs: current source has `Player.prefab` lines 137 and 239 cleared to `{fileID: 0}`; 193s, `Streaming`, `Vorbis`, Q0.45; Unity prefab readback, owner/removal ledger, and warning ducking proof are absent.
3. `dive_splash.wav` prior direct refs: current source has the line 1066 and 1067 splash fields removed from `Player.prefab`; 1.729s ADPCM; Unity prefab readback and water-contact playback route proof are absent.
4. Player breath loop: `breathing breath in and out 1.mp3`; waveform peak -0.16 dBFS and RMS -14.03 dBFS; reject generic SFX classification.
5. Suit interior loop: `inside suit sounds (too loud).wav`; filename and waveform review mark first-person mix debt.

Remediation matrix P0 count: 6 rows total, with 2 `music_mixer_config` rows and 4 `player_or_world_loop` rows.

## P1 Listening And Remediation Order

6. `swimming -onwater.wav`: player surface swim loop; needs latency/start and import exception proof.
7. `Atmos 1 Loop.wav`: dense ambient bed; waveform RMS -17.88 dBFS; warning and route masking risk.
8. `spaceship sounds - ambient.mp3`: lower steady ambience; Q0.45 and unclear route role.
9. `shelf_1_Abandoned Depths.ogg`: loud first-route long music bed; waveform RMS -11.05 dBFS; needs pause/window proof.
10. `abyss_3_Deep Trench Drone.ogg`: long high-peak tense drone; tension ownership required.
11. `stinger_dangerous_1_Iron_Teeth.ogg`: repeated/dense danger stinger; cooldown and priority proof required.

Remediation matrix P1 count: 44 rows, with 20 `short_sfx`, 11 `repeated_stinger`, 7 `long_music_bed`, 4 `ui_feedback`, and 2 `import_or_placeholder_risk` rows.

P2 queue remains UI click audibility and VO stub sanity. These are lower priority than P0/P1 route blockers, not accepted assets.

## Import-Policy Authority Conflict

The current sources conflict:

- Root AGENTS audio text targets Vorbis Q70 for ambient/music and says ambient/music load as compressed memory while allowing streaming music.
- STRM asset lifecycle mandate classifies clips over 10 seconds as `Streaming`, clips 2-10 seconds as `CompressedInMemory`, and clips up to 2 seconds as `DecompressOnLoad`.
- The decision brief recommends a hybrid exception table: duration default, Q70 target retained, generic SFX streaming forbidden, music and non-critical long ambience allowed to stream through owned routes, and player-critical exceptions ledgered and proved before import edits.

That hybrid route is a recommendation, not stable authority. Mass import edits remain blocked until a stable policy patch is accepted by the correct owner.

## Direct AudioClip Reference Risk

Current static prefab scan finds 24 direct `AudioClip` refs in `Assets/_Project/Prefabs/Player.prefab`: footsteps and UI feedback clips. Prior `Underwater Ambient.wav` and `dive_splash.wav` direct refs are source-cleared in the working tree and still need Unity prefab readback plus route proof.

Direct prefab serialization does not prove Addressables ownership, ref-count/release route, playback route, hot-path allocation behavior, mixer/DSP route, or audio-thread safety. Future owners must classify these refs by route and either move them into owned lifecycle paths or document scoped exceptions with proof.

## Mixer And Routing Risk

`Assets/_Project/Data/Audio/Music/Configs/MusicDirectorConfig_Global.asset` has null `_musicMixerGroup` and `_stingerMixerGroup` fields in static config evidence. This blocks route-level mix acceptance.

Allowed owner actions:

- Assign approved mixer groups through Unity after process gate clearance and readback.
- Or document an owned DSP/native bypass with phase, owner, route, and proof target.

Rejected action: leaving the null route unexplained while treating MusicDirector mixing as accepted.

## Placeholder VO Handling

`VOStub_Chen_Log01_EN.wav` and `VOStub_Chen_Log01_RU.wav` are placeholder rows. The waveform review marks the EN stub at peak -38.76 dBFS and RMS -63.21 dBFS, which is not useful final dialogue loudness evidence.

Future VO work needs final duration set, localization/subtitle route, import readback, memory/mix proof, and accessibility proof. Stub rows must stay placeholder-blocked until that exists.

## Claims Blocked Without Runtime And Listening Proof

The current evidence cannot support claims about:

- Runtime MusicDirector behavior, pause windows, profile transition timing, or stinger cooldown.
- Mixer or DSP/native routing behavior.
- Audio import state after any future policy change.
- Addressables residency, ref-count, release order, active bank limits, or memory pressure behavior.
- 0 B/frame playback, audio-thread safety, underruns, or callback safety.
- Warning, UI, breath, movement, or threat cue audibility under music/ambient beds.
- Final VO loudness, subtitle timing, localization behavior, or accessibility.

## Scalability Consequences

- Low lane: keep one active ambient bank under pressure, music streaming by owned route, player breath/warning/UI cues admitted before decorative beds, and no generic streaming SFX.
- Middle lane: allow additional ambient support only if warning/tool/fauna readability remains proven by listening notes and runtime data.
- High lane: widen MusicDirector prefetch and stinger variety only after route priority and cooldown proof.
- Ultra lane: spend extra headroom on richer mix layers, reverb, and transitions without changing cue ownership, Addressables keys, save identity, or release order.

CSV companion: `Docs/AssetAudit/AUDIO_ASSET_TAXONOMY_20260605.csv`.
