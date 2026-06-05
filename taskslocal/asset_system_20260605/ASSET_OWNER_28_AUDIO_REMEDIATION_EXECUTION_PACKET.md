# Asset Owner 28 - Audio Remediation Execution Packet

Status: `PENDING_VERIFICATION`.
Evidence class: `STATIC_DOC`, `STATIC_SOURCE`, `AUDIO_SOURCE_PROBE`, `AUDIO_WAVEFORM_QA`.
Scope: future-owner packet for audio routing and import remediation using the asset-front ledgers.
Hard boundary for this packet: no Unity run, no import edit, no prefab edit, no scene edit, no mixer edit, no Addressables operation, no build, no profiler run, no listening pass, no runtime proof, and no `Assets/` mutation was performed.

## Mandates Followed

- `AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC`: audio-thread work needs native/DSPGraph or approved bridge proof, no locks, waits, managed synthesis/decode, game-world queries, or dynamic allocations.
- `AUDIO_Hrtf_Binaural_Spatialization`: compact underwater audio defaults to cheap perceptual fakes; HRTF/convolution is optional high/ultra proof work, not a blocker for P0 routing.
- `OPT_Zero_GC_Policy_AllocFree_Mandate`: future playback, routing, UI warning, player-loop, stinger, and transition paths require `0 B/frame` proof.
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits`: audio work must preserve compact-lane budgets and cannot report frame, DSP, memory, or GC claims without measured artifacts.
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory`: admitted clips need owner, key/group or documented exception, ref-count, release phase, active-bank budget, memory proof, and pressure behavior.
- `ARCH_Signal_Lane_Segregation`: first-party hot cues use typed owner routes or `SignalBus<AudioSignal>` style lanes, not string event names, single-use EventIDs, or cross-domain concrete callbacks.

## Required Authority And Evidence Reads For Future Owner

- `AGENTS.md`
- `HECTON8_ORCHESTRATOR.md` only because this is a task packet / local orchestration handoff.
- `audio.md`
- `performance.md`
- `Docs/AssetAudit/ASSET_SYSTEM_INDEX_20260605.md`
- `Docs/AssetAudit/AUDIO_REMEDIATION_MATRIX_REVIEW_20260605.md`
- `Docs/Audio/audio_remediation_matrix_20260605.csv`
- `Docs/AssetAudit/AUDIO_LOUDNESS_TECHNICAL_PROPERTIES_20260605.md`
- `Docs/AssetAudit/AUDIO_DIRECT_REF_DETAIL_20260605.md`
- Use existing packets `ASSET_OWNER_10_MUSICDIRECTOR_AUDIO_ROUTING_PACKET.md`, `ASSET_OWNER_19_AUDIO_IMPORT_AUTHORITY_ADOPTION_PACKET.md`, and `ASSET_OWNER_23_AUDIO_SOURCE_TECHNICAL_REMEDIATION_PACKET.md` as prior static handoffs, not acceptance proof.

## First-20 Route Hook

This removes audio-route blockers for first world load, first exit, shallow shelf orientation, breath/suit continuity, warning/UI audibility, and music restraint.

Audio is not accepted if music or ambience masks breath, oxygen, pressure, sonar, UI, tool, threat, or route cues. Constant beds, decorative stingers, and generic ambience are rejected even when technically routed.

## Evidence Basis

The remediation matrix has `58` rows:

- `6` P0 rows.
- `44` P1 rows.
- `8` P2 rows.

P0 rows:

1. `Assets/_Project/Data/Audio/Music/Configs/MusicDirectorConfig_Global.asset`: null `_stingerMixerGroup`.
2. `Assets/_Project/Data/Audio/Music/Configs/MusicDirectorConfig_Global.asset`: null `_musicMixerGroup`.
3. `Assets/_Project/Audio/Movement/dive_splash.wav`: direct `Player.prefab` `AudioClip` ref, reported at line `1067`, currently `CompressedInMemory`, `ADPCM`, duration `1.729s`.
4. `Assets/_Project/Audio/Movement/dive_splash.wav`: direct `Player.prefab` `AudioClip` ref, reported at line `1066`, currently `CompressedInMemory`, `ADPCM`, duration `1.729s`.
5. `Assets/_Project/Audio/Underwater Ambient.wav`: direct `Player.prefab` `AudioClip` ref, reported at line `137`, currently `Streaming`, `Vorbis`, duration `193s`.
6. `Assets/_Project/Audio/Underwater Ambient.wav`: direct `Player.prefab` `AudioClip` ref, reported at line `239`, currently `Streaming`, `Vorbis`, duration `193s`.

Static direct prefab refs do not prove runtime failure. They block readiness because owner, load/release route, Addressables status, playback path, and `0 B/frame` proof are absent.

Loudness/source probe facts:

- `138` audio rows covered.
- `45` short/critical rows measured by ffmpeg source probe.
- `93` long music/ambient rows deferred to listening owner.
- `7` near-0dB source peak flags.
- `11` very quiet source mean flags.
- Source loudness is input evidence only. It is not Unity import proof, mixer proof, route mix proof, warning audibility proof, or listening acceptance.

## Non-Negotiable Boundaries

- No MasterAudio route. First-party audio does not use MasterAudio event strings.
- No `AudioSource.PlayOneShot` hot-path acceptance without architecture proof. Default future route is owned pooled/native/DSP playback with profiler and GC proof.
- No fake listening pass. A listening pass must name context, cue state, masking result, route decision, and proof artifact.
- No import changes from CSV/static docs alone. Import edits require Unity import readback, audio owner decision, route owner, rollback path, and proof plan.
- No raw YAML prefab, mixer, asset, or scene mutation unless the future owner proves Unity API mutation is impossible and validates file structure afterward.
- No generic streaming SFX. Short SFX/UI/player-critical cues need latency and lifecycle proof before any exception.
- No runtime readiness claim from MusicDirector profile refs, direct prefab refs, waveform stats, static ledgers, or source loudness probes.

## Execution Order For Future Owner

1. Confirm Unity/process gate permits readback. Do not run builds if CPU is above project threshold or `dotnet` / `csc.exe` is active.
2. Unity-read `MusicDirectorConfig_Global.asset`; capture `_musicMixerGroup` and `_stingerMixerGroup` state.
3. Close the MusicDirector output route before judging bed or stinger taste:
   - assign approved mixer groups through Unity, or
   - document an owned native/DSP bypass with owner, phase, cadence, failure mode, shutdown/release behavior, telemetry, and proof target.
4. Prove MusicDirector runtime output after route closure: profile entry/exit, crossfade, stinger path, warning ducking, silence windows, and Console clean state.
5. Unity-read `Player.prefab` and map the four P0 direct refs to owning components and serialized fields. Do not trust line numbers without readback.
6. Classify `Underwater Ambient.wav` refs:
   - routed ambient bank,
   - player-loop exception,
   - fixed startup/core exception,
   - Addressables-owned long ambience,
   - or removal/replacement candidate.
7. Classify `dive_splash.wav` refs:
   - player/movement cue,
   - pooled short SFX cue,
   - startup-fixed exception,
   - or removal/replacement candidate.
8. For any retained direct ref, fill owner, cue id/hash, load phase, release/shutdown phase, playback route, priority/ducking, fallback, import readback, lifecycle proof, and hot-path allocation proof.
9. Update `Docs/Audio/audio_asset_ledger.csv` only if the future task explicitly allows ledger edits. Fill owner, Addressables group/key or exception, route use, placeholder state, and proof state from evidence only.
10. Resolve import-policy conflict before broad import edits:
    - AGENTS static clause says ambient/music compressed in memory.
    - Streaming/asset lifecycle law says clips above `10s` stream unless a bounded exception is proved.
    - Current safe direction is hybrid: short generic SFX non-streaming, MusicDirector/long ambience streaming through owned lifecycle, player-loop class separated from generic SFX, exceptions ledgered row-by-row.
11. Perform Unity import readback for P0 and selected P1/P2 rows before mutation: load type, compression, quality, sample rate, force mono, preload/background flags, loop flags, platform overrides, and source/import channel behavior.
12. Do not mutate imports until route owner, lifecycle owner, rollback target, and proof artifacts are named.
13. Build Addressables/lifecycle plan for admitted long beds and ambience: group/key, active-bank cap, ref-count, release phase, pressure behavior, fallback, and orphan-handle audit.
14. Prove memory/residency for active audio banks and streaming buffers with Memory Profiler or equivalent runtime artifact. Static source size is not resident memory proof.
15. Run listening proof only after P0 route blockers are closed. Required contexts:
    - first exit,
    - shallow shelf,
    - storm/tension,
    - warning/UI overlap,
    - breath/suit/swim continuity,
    - silence windows,
    - MusicDirector profile transitions,
    - stinger cooldown and spam suppression.
16. Record loudness decisions as route/mix decisions, not source-probe acceptance. Near-0dB peaks need clipping/ducking review; quiet means need audibility review; long beds need masking and cadence review.
17. Run runtime proof: Play Mode/player capture for route mix, Profiler/GCMonitor for allocations, audio/DSP proof for callback safety, Console clean state, and Memory Profiler for active-bank residency.
18. If runtime evidence contradicts static rows, the runtime evidence wins. Mark the static row stale and update the ledger only with proof.

## Import Policy Gates

Future owner must not apply a broad import preset. Every row needs class and route.

- `music`: MusicDirector-owned, long, normally streaming through lifecycle owner unless a bounded resident exception is proved. Vorbis target and sample rate need Unity readback and policy decision.
- `ambient`: long ambience is not generic SFX. Treat as active bank with owner, active count, memory pressure behavior, ducking rule, and listening proof.
- `player_loop`: breath, suit, swim, movement continuity, warning pulse, and first-person loops are latency-critical presentation. They need class-specific prewarm and continuity proof.
- `sfx`: short generic SFX must not stream. Use ADPCM/decompress or compressed-in-memory according to duration, latency, and owner route.
- `ui`: instrument feedback, not app chrome. Needs audibility and warning-priority proof.
- `voice`: current VO stubs are placeholders. Do not use them to settle final VO/localization/import policy.

## Mixer Routing Gates

MusicDirector P0 is not closed until one route is proven:

- approved mixer groups are assigned and Unity-read back, or
- an owned native/DSP bypass is documented and measured.

Required proof:

- Unity config readback for `_musicMixerGroup` and `_stingerMixerGroup`.
- Runtime MusicDirector capture for profile entry/exit, crossfade, stingers, and ducking.
- Console clean state.
- No unmanaged-proven release dependency on managed `OnAudioFilterRead` synthesis/decode/mix callbacks.
- No MasterAudio, no string event route, no hidden hot-path scene search.

## Addressables And Audio Memory Gates

Admitted long music/ambient/player-loop rows need:

- owner;
- cue id/hash;
- Addressables group/key or documented fixed-startup exception;
- load phase;
- release/shutdown phase;
- active bank count;
- streaming buffer budget;
- ref-count behavior;
- pressure response;
- fallback route;
- orphan-handle audit.

Compact lane must prioritize breath, warnings, sonar/threat, UI/instrument feedback, tool state, and route cues before decorative beds or secondary music breadth.

## Runtime, Profiler, And GC Proof Boundaries

Accepted proof classes:

- `STATIC_VERIFIED`: path/config/source facts only.
- `EDITOR_VERIFIED`: Unity import/config/prefab readback with Console state.
- `PLAYMODE_VERIFIED`: route playback and cue priority observed in scene.
- `PROFILER_VERIFIED`: runtime Profiler/GCMonitor/audio/DSP evidence.
- `MEMORY_VERIFIED`: Memory Profiler or equivalent resident/committed audio memory evidence.
- `PLAYER_CAPTURE_VERIFIED`: player capture demonstrating mix readability.

Forbidden proof claims:

- `0 B/frame` from static scan.
- route mix acceptance from source loudness.
- runtime cadence from profile YAML.
- Addressables readiness from direct prefab refs.
- listening acceptance without capture/notes/context.
- measured microseconds without profiler artifact.

## Rejection And Rollback Gates

Reject or revert future changes if:

- MusicDirector mixer refs remain null and no owned DSP/native bypass is documented.
- Direct `Player.prefab` refs remain unclassified after attempted route adoption.
- generic SFX are made streaming.
- player loops stream by duration alone and show start jitter, masking, or lifecycle ambiguity.
- long music/ambient admission increases memory risk without compact-lane evidence.
- music, ambience, stingers, or UI feedback mask breath, oxygen, pressure, sonar, tool, route, threat, or warning cues.
- source loudness probe is treated as mix acceptance.
- placeholder VO is used to set final VO policy.
- any runtime route allocates in hot paths or depends on string cue lookup.

Rollback target: restore the prior import/config/prefab state, mark affected rows blocked, keep static ledgers as evidence only, and reassign to Audio plus Streaming owners.

## Continuous GlobalQualityWeight Consequences

- Low / compact: one active long music/ambient context unless measured memory permits more. Critical cue truth, warning priority, breath/suit continuity, route cues, and UI feedback stay admitted first. Layer count, reverb cost, prefetch breadth, secondary beds, and diagnostics shrink smoothly.
- Middle: current context plus likely-next context may be admitted after memory, ducking, and player-loop proof.
- High: spend headroom on smoother transitions, richer hydrophone detail, profile-specific stingers, stronger reverb/occlusion, and better silence-window control.
- Ultra: dense secondary beds, broader stinger palette, richer spatial/reverb detail, and wider prefetch are allowed only after critical cue readability and lifecycle proof remain intact.

`GlobalQualityWeight` must not change cue truth, cue IDs, owner route, Addressables keys, release order, save identity, DTO layout, source-fact authority, or warning facts.

## Regression Model

- CPU: no runtime work changed by this packet. Future risk comes from decode, streaming dispatch, MusicDirector crossfade, stinger scheduling, mixer work, and DSP contention.
- GC: no runtime work changed by this packet. Future risk comes from string cue lookup, direct event routes, managed callbacks, dynamic collections, logging, UI text, and transition code.
- Memory: no import/residency changed by this packet. Future risk comes from long-bed active banks, duplicate direct refs, streaming buffers, broad prefetch, stereo/high-rate short cues, and missing release.
- Cadence: no runtime cadence changed by this packet. Future risk comes from constant beds, repeated stingers, missing silence windows, warning masking, and player-loop start jitter.
- Correctness: static P0 blockers are mapped only. Import state, lifecycle, mixer routing, DSP path, listening result, and runtime behavior remain unproved.

Final disposition: `PENDING_VERIFICATION`.
