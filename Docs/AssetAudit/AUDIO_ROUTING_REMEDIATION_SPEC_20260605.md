# Audio Routing Remediation Spec - 2026-06-05

Status: `PENDING_VERIFICATION`.
Evidence class: `STATIC_SOURCE`, `STATIC_DOC`, `AUDIO_WAVEFORM_QA`.
Scope: MusicDirector/static audio routing remediation plan. No files under `Assets` were changed.

Read before execution:

- `Docs/AssetAudit/ASSET_ACTION_QUEUE_20260605.md`
- `Docs/AssetAudit/AUDIO_PROFILE_USAGE_REVIEW_20260605.md`
- `Docs/AssetAudit/AUDIO_WAVEFORM_REVIEW_20260605.md`
- `Docs/Reports/AssetSystem_20260605/AUDIO_POLICY_CONFLICT_AND_CUE_DISPOSITION_3213_20260605.md`
- `Docs/Reports/AssetSystem_20260605/AUDIO_IMPORT_POLICY_DECISION_BRIEF_3217_20260605.md`
- `Docs/Audio/audio_asset_ledger.csv`
- `Docs/Audio/audio_profile_usage_20260605.csv`
- `audio.md`
- `streaming.md`

Mandates followed:

- `QA_Evidence_Text_Filter_Audit`
- `AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC`
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory`

## P0 Mixer Routing

Problem:

- Static profile usage review found `_musicMixerGroup` and `_stingerMixerGroup` null in `MusicDirectorConfig_Global.asset`.
- This blocks any mix-readiness claim.

Required owner action:

- Unity owner must read the config asset through Unity API/Inspector.
- Assign or create the approved mixer groups only if the audio architecture owner confirms the intended route.
- If mixer groups are intentionally null because DSP/native routing bypasses Unity mixers, write that as an explicit route exception with owner, phase, and proof target.

Hard rejections:

- Do not silently leave null refs and claim MusicDirector readiness.
- Do not create string-event or MasterAudio-style routes.
- Do not claim DSP safety from serialized mixer refs.

Proof gate:

- Config readback artifact.
- Runtime MusicDirector capture showing music/stinger routing.
- Console state.
- No constant music bed in first-exit/shallow route.

## P0 Direct Player Prefab Audio Refs

Problem:

- Current static prefab scan found 24 direct AudioClip refs in `Assets/_Project/Prefabs/Player.prefab`; prior `Underwater Ambient.wav` and `dive_splash.wav` direct refs are source-cleared but pending Unity prefab readback.
- Heavy/loop clips include `Underwater Ambient.wav` as readback/removal proof debt; direct serialized refs currently remain in footstep/UI routes only.
- Static refs do not prove Addressables ownership, release route, zero-GC lifecycle, or hot-path playback safety.

Required owner action:

- Split refs into categories:
  - `world_or_player_loop`: underwater ambience, breath/suit/swim loops.
  - `short_sfx`: footsteps, dive splash, short UI.
  - `ui_feedback`: click/notification/nope.
- For each category, assign owner, load route, release route, and runtime playback route.
- Heavy/loop clips need owned lifecycle or documented exception; generic SFX must not be streaming. Preserve the 3213 taxonomy split: `sfx` and `player_loop` are separate classes.
- Short SFX should remain short, bounded, and non-streaming.

Hard rejections:

- No Addressables readiness claim from prefab serialization.
- No `AudioSource.PlayOneShot` hot-path acceptance without architecture owner proof.
- No streaming generic SFX.
- No unmanaged/DSP safety claim without runtime telemetry.

Proof gate:

- Filled `Docs/Audio/audio_asset_ledger.csv` owner and Addressable fields.
- Unity prefab readback.
- Runtime audio path proof and 0 B/frame telemetry.

## P1 Music Cadence / Long Beds

Problem:

- All 84 music tracks are serialized in profiles, so library coverage is not the immediate blocker.
- Waveform review shows dense/loud long beds and stingers.
- Profile usage review found repeated stinger reuse and long bed rows >= 300 seconds.

Priority tracks:

- `Assets/_Project/Audio/Music for Game/shelf_1_Abandoned Depths.ogg`
- `Assets/_Project/Audio/Music for Game/abyss_3_Deep Trench Drone.ogg`
- `Assets/_Project/Audio/Music for Game/stinger_dangerous_1_Iron_Teeth.ogg`
- Other repeated stingers listed in `AUDIO_PROFILE_USAGE_REVIEW_20260605.md`.

Required owner action:

- Listening pass for first-exit, shallow shelf, storm/tension, silence windows, and stingers.
- Confirm pause windows, stinger cooldown, cross-tension borrowing, and profile transitions in runtime.
- Confirm music never becomes a constant emotional blanket during player decision moments.
- Mark repeated stingers as intentional reuse or replace/profile-specialize them.

Hard rejections:

- No runtime acceptance from static profile rows.
- No music-bed acceptance from waveform only.
- No always-on long bed in first-exit/photic route.

Proof gate:

- Listening notes with scene/context.
- Runtime MusicDirector state/cue capture.
- Mixer route readback.
- No Console spam.

## P1 / P2 Import Policy Conflicts

Problem:

- Current ledger has music streaming Vorbis Q70, many ambient clips streaming Vorbis Q45, and player-layer loops that need a separate low-latency/load-policy decision.
- AGENTS has ambient/music compressed-in-memory wording; streaming mandate prefers long clips streaming. This conflict must be resolved by route policy before mass import edits.
- `AUDIO_IMPORT_POLICY_DECISION_BRIEF_3217_20260605.md` recommends a hybrid exception table: duration-based default, AGENTS quality targets retained, generic SFX streaming forbidden, music/long non-critical ambience streaming through owned MusicDirector/Addressables routes, and player-critical exceptions ledgered and proved before import edits. This recommendation is not adopted stable authority until a controller/human patch lands in the stable docs.

Required owner action:

- Write one audio import policy note before import changes:
  - music long beds: streaming or compressed-in-memory per final owner decision;
  - ambient long beds: Q70 target or explicit tiered exception;
  - short SFX: non-streaming ADPCM/decompress route;
  - VO: placeholder vs final spoken content route;
  - player loops: classify separately from generic SFX.
- Apply changes only through Unity import API after process gate is clean.

Proof gate:

- Import readback.
- Memory/residency proof.
- Runtime mix and GC proof.

## Low / Middle / High / Ultra Consequences

- Low/compact: fewer concurrent beds, strict ducking, short SFX non-streaming, no hidden upload/import spikes. Silence windows protect gameplay readability.
- Middle: richer ambience layering only if warnings/tools/fauna remain readable.
- High: longer LOD-like audio tails, stronger music transitions, richer stinger set.
- Ultra: higher density and broader dynamic mix, but no change to gameplay truth or save/cue identity.

Final status: `PENDING_VERIFICATION`.
