# HECTON-8 Audio Bible

Status: AUTHORING STANDARD - PENDING UNITY/PROFILER VERIFICATION
Evidence class: STATIC_DOC
Scope: soundscape, sonar, hydrophones, warnings, suit voice, creatures, machinery, UI audio, music discipline, mix states, and audio performance taste.

## 0. Prime Audio Law

Sound arrives first. HECTON-8 should often make the player afraid or informed before the image explains why.

Every audio cue must carry one of these functions:

- route information;
- threat presence;
- pressure state;
- machine state;
- oxygen/suit state;
- signal trust;
- tool feedback;
- evidence memory;
- warning priority.

Generic ambience, generic monster sounds, clean sci-fi beeps, and constant music beds are rejected.

## 1. Soundscape Layers

A scene mix should have:

- Abyss bed: pressure rumble, low current, hull stress, distant water body.
- Machine layer: pump, relay, fan, valve, hatch, conduit, ballast, power hum.
- Signal layer: sonar, hydrophone, radio carrier, black-box fragment, scanner ping.
- Threat layer: partial creature signal, scrape, displacement, breath, silence break.
- Player layer: suit breath, tool, warning, body, interaction.
- Evidence layer: old audio logs, terminal playback, corrupted fragments.

Do not let all layers speak at once. Mix state must prioritize current player decision.

## 2. Sonar And Hydrophone

Sonar is partial truth:

- bearing before identity;
- confidence before certainty;
- occlusion and stale data;
- active ping creates risk;
- passive listening is safer but weaker;
- returns must respect environment and creature behavior.

Clean omniscient radar is rejected. Pure decorative pings are rejected.

## 3. Warnings And Suit Voice

Warnings must be sparse, prioritized, and physical:

- name the system;
- state the severity;
- use cadence and tone by priority;
- suppress spam;
- pair critical warnings with UI/haptic where appropriate;
- fail closed if warning data is stale.

Suit voice should be disciplined, not chatty. It should sound like expensive equipment under stress, not a joke machine.

## 4. Creature Audio

Creature audio must shape behavior:

- calls imply distance, size, mood, or territory;
- movement sounds indicate route risk;
- silence can be a cue;
- reactions should tie to noise, light, blood, power, hull stress, or intrusion;
- full creature vocal reveal should be earned.

Generic roar libraries are rejected unless transformed into a unique underwater/acoustic identity.

## 5. Machinery Audio

Machines are verbs:

- pump starts, stutters, cavitates, and fails;
- pressure door seals, grinds, locks, or jams;
- relay hum shifts under load;
- power route changes soundscape;
- damaged panels buzz, click, leak, or arc.

A machine with no sound state feels decorative.

## 6. Music Discipline

Music must not flatten dread:

- use silence and low-frequency pressure first;
- music enters when it sharpens a decision or transition;
- avoid constant beds that tell the player how to feel;
- avoid heroic comfort unless it is narratively earned and fragile.

The ocean and machines should carry more fear than score.

## 7. UI Audio

UI audio is instrument feedback:

- toggle click;
- relay thunk;
- archive verify chirp;
- pressure warning pulse;
- corrupted read crackle;
- route plot tick;
- disabled command dead click.

No clean mobile-app sounds. No decorative menu whooshes unless tied to a physical carrier.

## 8. Performance And Implementation

Audio systems must obey:

- no managed allocation in hot paths;
- pooled events and voices;
- data-driven cue IDs;
- priority and virtualization;
- SPSC/ring buffers where mandated;
- low-cadence environmental parameter updates;
- mix snapshots tied to gameplay state;
- no string cue lookup in runtime hot paths.

High-end may add richer convolution, layers, or detail, but compact must keep route, warning, and threat information.

### 8.1 Managed Audio Callback Boundary

Release audio must not synthesize, decode, mix, lock DataVault views, acquire mutation guards, run `Stopwatch`, or touch gameplay-owned state inside Unity managed `OnAudioFilterRead(float[] data, int channels)` callbacks. The approved production route is native/DSPGraph output or a native audio-kernel bridge fed by preallocated SPSC rings and double-buffered parameter snapshots.

If a transitional component still contains `OnAudioFilterRead`, it is blocked from release acceptance until one of these is proven:

- the component is excluded from release player builds;
- the callback is only a measured transfer shim from a prefilled native ring and carries no synthesis, decoding, locking, allocation, string work, file IO, scene lookup, or gameplay query;
- the route has an explicit waiver and a DSP profiler capture showing no underrun, no GC, no blocking, and no budget breach on compact hardware.

Mock audio banks, emergency procedural profiles, missing mixer-parameter fallbacks, and runtime-added audio components are recovery paths only. Production scenes must ship authored banks, mixer bindings, listener components, audio roots, and warmed pools before gameplay begins.

### 8.2 Current Runtime Source Anchors

Evidence class: STATIC_SOURCE only. These anchors describe the current code route; they do not prove Unity import, mixer binding, native plugin availability, profiler/GC, or player-build audio acceptance.

Current owners and routes:

- `Assets/_Project/Scripts/AcousticZoneController.cs` owns acoustic-zone presentation, water/flood muffle signals, mixer snapshot transitions, queued transition cues, storm/static interference, sonar impulse response, vegetation overlays, and acoustic read-model state. It consumes soundscape, physics impact, sonar ping, atmosphere, player, physics, dispatcher, audio service, and music/director registry slots. It must not own pressure, flooding truth, player state, sonar truth, or save state.
- `Assets/_Project/Scripts/Audio/VocalWarningSystem.cs` owns bounded vocal warning priority/cooldown/dispatch state. It publishes current/dispatch/profiles/tuning/telemetry through DataVault buffers owned by `SystemID.AudioVocalWarning`, uses `SignalBus<VocalWarningSignal>` for producers, and reports signal rejection as a fault instead of silently accepting warning spam.
- `Assets/_Project/Scripts/Audio/Synthesis/VocalBankPlaybackRuntime.cs` owns vocal cue playback, mock-bank fallback, waveform/telemetry/csv metadata buffers, `PlayVoiceOverSignal`, `VocalCueSignal`, and `SubtitleCueSignal` consumption. It is the playback route for vocal-warning phrases and voice-over/subtitle handoff; it is not the authority for the gameplay fact behind a warning line.
- `Assets/_Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs`, `Assets/_Project/Scripts/Audio/HectonSensoryKernelNativeBridge.cs`, `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs`, and `Assets/_Project/Scripts/Audio/PlayerCriticalBufferJobs.cs` are the current critical procedural/native bridge route. `AudioFrameSpscRingBuffer` uses a power-of-two frame ring with telemetry; `HectonSensoryKernelNativeBridge` validates descriptor magic, alignment, shared-state metadata, capacity, and native plugin availability before registering the shared ring.
- `Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs` owns adaptive stem mix buffers, rules, commands, mock depth/predator/tension inputs, telemetry, DataVault mutation guards, celestial-light readability binding, player/survival/damage/biome/narrative signal consumption, and `GlobalQualityWeight` cadence/quality scaling.
- `Assets/_Project/Scripts/Audio/HectonMusicDirector.cs` owns music cue selection and music-director registry service. It reads player, audio, acoustic-zone, biome, encounter, depth, weather, first-hour, vocal warning, and audio-log read models/signals; it must not become threat, biome, depth, or narrative truth.
- `Assets/_Project/Scripts/Audio/Echolocation/AcousticEcholocationRaymarch.cs` and sonar/audio consumers may present acoustic evidence, but sonar truth remains with sonar/spectrum owners.
- `Assets/_Project/Scripts/Audio/Prologue/PrologueAcousticOrchestrator.cs` owns prologue transition audio state driven by atmospheric reentry, reentry acoustic stress, and prologue completion signals. It must publish neutral transition state on disable instead of leaving stale transition pressure in the mix.

Lifecycle and service law:

- Audio runtimes that register with `GlobalRegistry` must unregister in `OnDisable` and clear cached services/read models when registry replacement occurs.
- DataVault-backed audio routes must acquire/release mutation guards and release owned buffers on disable/rebind. A missing DataVault is a blocked degraded route, not permission to allocate ad hoc managed state in hot paths.
- SignalBus-backed audio producers must use bounded `TryPush`/`TryPushTracked` routes. Obsolete direct raise methods that hide drops are not acceptable for new producers.
- Runtime-created or lazily repaired `AudioSource`, mixer group, mock bank, or fallback clip binding is recovery only. It cannot be cited as production binding proof.
- Music, warnings, acoustic zones, vocal playback, and critical procedural audio must scale with `GlobalQualityWeight` by layer density, cadence, voice count, filtering/detail, telemetry cadence, or optional effect richness. They must not remove route-critical warnings, threat cues, sonar meaning, or machine-state cues.

Failure modeling required before acceptance:

- no audio service, missing dispatcher, stale registry slot, duplicate music/acoustic owner, or service replacement during playback;
- SignalBus queue full, repeated subscribe/unsubscribe, producer using obsolete raise path, or warning/cue drop hidden as success;
- DataVault missing, stale handle, buffer capacity mismatch, mutation guard not released, interrupted job, or telemetry ring cursor corruption;
- native audio plugin unavailable, descriptor magic mismatch, null/unaligned pointer, invalid power-of-two capacity, bad shared-state metadata, busy native bridge, or ring overrun/underrun;
- `OnAudioFilterRead` doing synthesis/decoding/locking/allocation beyond an approved shim;
- missing authored mixer snapshots, exposed mixer parameters, mixer groups, banks, clips, subtitles, or listener/root binding;
- mock bank, emergency grain bank, fallback clip, or runtime component repair used as production proof;
- quality reduction hides a critical warning, sonar cue, route cue, machine state, or creature/threat cue;
- scene unload/domain reload leaves a stale acoustic state, current warning, music cue, native ring, or prologue transition active.

## 9. Audio QA Gates

Reject if:

- cue has no gameplay information;
- monster sound is generic;
- music hides system audio;
- warnings spam;
- UI audio feels like app chrome;
- no low-tier mix exists;
- hot path allocates;
- release player uses unmanaged-unproven `OnAudioFilterRead` synthesis or decode paths;
- production audio depends on mock banks, emergency profiles, or runtime component repair;
- cue lookup uses strings;
- silence is never used;
- player cannot infer anything from the sound.

## 10. Truth Ownership

Audio owns presentation of sound, mix, priority, cue identity, spatialization, and warning cadence. Audio does not own pressure, oxygen, AI, tool, route, damage, or mission truth. It consumes stable events and snapshots from the owning systems.

Critical audio must have a source fact. If the cue implies hull breach, creature proximity, oxygen collapse, route signal, or archive corruption, the owning system must publish that fact.

## 11. GlobalQualityWeight Scaling

Compact preserves warning priority, sonar meaning, threat cues, suit breath, and core machine state with fewer layers and cheaper spatialization. Middle adds richer ambience and occlusion. High adds stronger hydrophone detail and mix transitions. Ultra adds dense secondary layers, richer reverb/occlusion, and cinematic detail without hiding critical cues.

## First-20 Route Hook

- First-20 moment: world load, first exit, swim, tool, hazard, and save/load must expose route, oxygen/pressure, sonar/signal trust, tool feedback, warning priority, and machine state through sound.
- Route blocker removed: audio cannot be generic ambience, constant score, decorative ping, or warning spam that masks route-critical cues.
- Proof class: Play Mode/player capture for route mix, Profiler/GCMonitor for runtime audio paths, screenshot or static cue sheet for owner/source mapping, and save/load artifact when audio-log or recovered signal state persists.

## 12. Proof Artifacts

Audio work must provide:

- cue ID list;
- owner event/source fact;
- priority/mix behavior;
- spam suppression rule;
- low-tier mix path;
- hot-path allocation note;
- managed audio callback/native bridge status;
- authored bank, mixer binding, and runtime component prewarm proof;
- capture or test scene where practical;
- subtitle/caption route for critical speech or warnings.

## 13. Acceptance Sentence

Audio is accepted only when it carries information under pressure, has a clear truth source, respects mix priority, scales without losing critical cues, and proves it does not allocate or spam in hot paths.
