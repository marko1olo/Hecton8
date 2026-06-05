# Asset Owner 10 - MusicDirector Audio Routing Packet

Status: `PENDING_VERIFICATION`.
Evidence class: `STATIC_DOC`, `STATIC_SOURCE`, `AUDIO_WAVEFORM_QA` only.
Scope: next Audio/MusicDirector owner execution packet for MusicDirector profile/cue routing, null mixer group blockers, long-bed cadence risk, repeated stinger risk, and lifecycle proof requirements.

No Unity run, import edit, prefab edit, scene edit, build, play mode, profiler, listening pass, runtime test, or `Assets/` mutation was performed.

## Mandates Followed

- `AGENTS.md`
- `.agents-skills/AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `audio.md`

## Boundary

This packet is an execution handoff, not acceptance evidence. Static profile rows prove references exist. They do not prove mixer output, DSP/native route, Addressables ownership, release order, runtime cadence, warning priority, memory residency, audio-thread safety, or `0 B/frame`.

First-20 route moment affected: first surface exit, photic shallow read, player breath/audio continuity, warning/UI audibility, and medium-depth music restraint. MusicDirector must not replace route-driven sound with constant beds.

## P0 Blocker: Null MusicDirector Mixer Route

Static evidence reports `Assets/_Project/Data/Audio/Music/Configs/MusicDirectorConfig_Global.asset` with null `_musicMixerGroup` and null `_stingerMixerGroup`.

Runtime mix claims are blocked until the next owner proves one of these routes:

- Assign approved Unity mixer groups through Unity and capture config readback.
- Or document an owned native/DSP bypass with owner, phase, cadence, failure mode, proof target, and readback path.

Rejected route: treating MusicDirector profile cue refs as mix proof while music/stinger output groups are null.

## Current Risk Set

- Long beds: seven MusicDirector bed rows are `>=300s`; `shelf_1_Abandoned Depths.ogg` is waveform-flagged loud/dense and `abyss_3_Deep Trench Drone.ogg` is waveform-flagged high-peak long drone. Static evidence does not prove pause windows, crossfades, tension ownership, or warning ducking.
- Repeated stingers: 11 repeated stinger GUID groups exist. Recovery stingers repeat across 8 profiles, danger stingers across 7, discovery stingers across 6, hallucination across 3. Reuse may be intentional, but it needs cooldown, event owner, priority, and listening proof.
- Fallback profile: many long/short/stinger refs can become hidden always-on content if the legal trigger and exit route are not owned.
- Shallow/shelf profiles: first-exit and medium-depth routes can be masked by constant beds or dense stingers. Silence windows are a requirement, not polish.
- Ambient bank interaction: long ambient rows include Q0.45 cases and direct prefab risks. MusicDirector must not mask breath, warnings, UI, fauna, tools, or route signals.

## Route Requirements For Next Owner

- Use MusicDirector owner route or approved native DSP/SpatialAudioManager route where applicable.
- No MasterAudio strings. First-party route does not use MasterAudio event names.
- No direct clip/event-string lookup in hot paths. Cue identity must be data-driven through numeric IDs or stable hashes if new IDs are needed.
- No `AudioSource.PlayOneShot` in hot paths. Use owned pooled/native/DSP route with proof.
- Long music/ambient clips require Addressables/lifecycle proof: key/handle ownership, ref-count, load mode, release order, active-bank limit, and memory/residency evidence.
- Player-critical cues outrank beds: breath, oxygen/pressure warnings, suit state, UI/instrument feedback, sonar/threat route cues.
- Repeated stingers require owned event source, cooldown, warning-priority behavior, and spam suppression.
- If Unity mixer groups are bypassed, the bypass must still prove audio-thread safety, no managed callback synthesis/decode, no locks/spin/blocking, and underrun handling.

## Execution Order

1. Static config grep/readback target: confirm `_musicMixerGroup` and `_stingerMixerGroup` in `MusicDirectorConfig_Global.asset`; record exact Unity-side assigned objects or documented bypass.
2. Close MusicDirector route before listening taste: no profile bed/stinger acceptance before mixer/native route is owned.
3. Prove shallow first-exit profile: transition, silence windows, stinger cooldown, and warning/player cue audibility.
4. Prove long-bed cadence: shelf, abyss, cave, fallback long beds must show pause windows and non-masking behavior.
5. Prove repeated stinger cadence: mark reuse intentional or replace/specialize; capture cooldown and priority.
6. Prove lifecycle: Addressables/load/release/memory handle evidence for long beds and active banks.
7. Run listening pass in documented queue order after P0 route blockers close.

## Acceptance Gates

- Static config grep/readback: required before route work can proceed.
- Unity config readback: `PENDING`.
- Runtime MusicDirector capture: `PENDING`.
- Listening pass for first exit, shallow/shelf, long beds, warning/UI overlap, silence windows, and stingers: `PENDING`.
- GC proof: `PENDING`; no `0 B/frame` claim exists.
- Memory/residency proof: `PENDING`; no Addressables handle, active-bank, or Memory Profiler proof exists.
- Audio-thread/DSP safety: `PENDING`; no underrun, callback, or native bridge proof exists.
- Console clean state: `PENDING`.

## Regression Model

- CPU: risk from decode, prefetch, crossfade, stinger scheduling, and any managed fallback. Future owner must profile MusicDirector transitions and playback route.
- GC: risk from string cue lookup, direct clip/event routes, callbacks, coroutines, unmanaged direct refs, and allocation during transitions. Future proof must use GCMonitor/Profiler, not static docs.
- Memory: risk from broad long-bed residency, unmanaged direct prefab refs, Q0.45 ambient exceptions, and missing Addressables release. Prove handle ledger and resident memory.
- Cadence: risk from constant long beds, repeated stinger spam, fallback always-on behavior, and missing silence windows. Prove runtime timing and cooldown.
- Correctness: risk from null mixer refs, unowned event triggers, warning masking, profile bleed outside route context, and treating waveform/static rows as runtime proof.
- Warning priority: oxygen, pressure, suit, UI, sonar, threat, and tool cues override music/stingers. Music that hides decision signals is rejected.

## GlobalQualityWeight Consequences

- Low: one active owned music/ambient context where possible; breath, warnings, suit/UI, sonar, and route cues dominate. No decorative stinger breadth. Continuous weight may reduce layer count, prefetch breadth, reverb cost, and update cadence, not cue truth.
- Middle: add likely-next profile prefetch and limited ambience only after route, ducking, and memory proof. Preserve silence windows and player-loop priority.
- High: spend headroom on smoother transitions, profile-specific stingers, richer reverb/occlusion, and better mix detail after cooldown and warning-priority proof.
- Ultra: allow dense secondary beds, broader prefetch, convolution/reverb, and richer stinger palette only when critical cues remain readable and ownership/release routes are unchanged.

## Hand-Off Disposition

Next owner: Audio/MusicDirector owner with Streaming/Addressables support.

Do not start with broad import mutation. Do not judge music taste before P0 routing is closed. Do not claim readiness from this packet.

Final disposition: `PENDING_VERIFICATION`.
