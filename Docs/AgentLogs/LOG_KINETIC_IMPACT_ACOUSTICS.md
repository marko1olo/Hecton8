# LOG_KINETIC_IMPACT_ACOUSTICS

## 2026-05-14 - DSP_ACOUSTIC_LEAD - Procedural Collision Audio
Status: PENDING VERIFICATION

What was wrong:
- High-speed collision energy had no procedural acoustic route through the central audio contract.
- Collision audio risked becoming another singleton or authored-clip path instead of using the project GlobalRegistry/EventBus lanes.
- Underwater impact tone needed a deterministic 800 Hz muffle without synchronous water/physics queries.
- Impact energy could be extreme or non-finite, which is unsafe for gain, distortion, and telemetry.
- The Burst oscillator compile surface initially used exact exponential filter decay, which was unnecessary for a short cinematic thud.

What was done:
- Extended `IAudioService` with `QueueHighSpeedImpactSignal(in HighSpeedImpactSignal signal)`.
- Implemented service routing in `SpatialAudioManager`: finite guards, AUP runtime conversion, passive radar emitter queueing, and forwarding to `GlobalRegistry.PlayerCriticalAudio`.
- Confirmed `PlayerCriticalProceduralAudioRenderer` owns the high-speed snapshot path, derives mass from `LostKineticEnergy` and `ImpactSpeed`, recalculates `0.5 * mass * speedSq`, clamps to `KineticImpactMaximumSafeEnergyJoules`, and maps the result into thud, distortion, low-pass, echo tap, and telemetry.
- Confirmed low-tier/MX350 fallback exits to `lowTierKineticImpactClip` through the existing pooled `PlayAtPoint` API, not `AudioSource.PlayClipAtPoint`.
- Confirmed procedural path uses 150 Hz -> 40 Hz thud over 0.2 s, hard clipping at extreme energy, 800 Hz underwater low-pass, `NativeQueue<SonarEchoTap>` echo routing, and `PeakImpactEnergyJoules` black-box telemetry.
- Added/kept Burst compile surface `KineticImpactSineOscillatorJob` and replaced exact `math.exp` low-pass coefficient with `ApproximateExpNegPositive` reciprocal approximation.

Cinematic cheats used:
- One pitch-descending sine thud stands in for structural deformation and collision acoustics.
- Existing metallic clang/granular bed supplies perceived material bite instead of a material-accurate solver.
- Native sonar echo tap is reused for impact reflection instead of a new acoustic ray/portal simulation.
- Underwater muffling is one scalar waterline comparison plus 800 Hz low-pass, not volume tracing.
- Low tier uses one baked clip; Middle uses thud+clang; High uses thud+echo; Ultra keeps bounded stronger distortion/echo with the same contract.

Exact microseconds saved:
- Singleton/audio-source avoidance: estimated 8-20 us per accepted impact admission and 0 B/frame hot path.
- Bounded signal scan: 32 high-speed signals, target under 20 us worst-case scan.
- Low-tier baked fallback: avoids the full 0.2 s oscillator/LPF window on i3/MX350; cost is one pooled source setup.
- Echo reuse: one `NativeQueue<SonarEchoTap>` enqueue instead of a new managed queue/path, target under 10 us admission.
- Energy clamp/math guard: <1 us scalar ALU, prevents unsafe gain and telemetry corruption.
- Omega polish: removes one exact exponential from Burst oscillator setup; micro-level CPU saved, no allocation change.

Verification:
- `rg -n -F 'PlayClipAtPoint' Assets/_Project/Scripts` returned no matches.
- Owned-file scans found no managed `foreach`, no `math.exp` in synthesis after polish, no unconditional `math.normalize`; `.ToString()` hits are editor/cold bootstrap reporting only.
- `git diff --check` passed except CRLF normalization warnings.
- Unity MCP `validate_script` failed with `Unity session not available; reason no_unity_session`.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -p:UseSharedCompilation=false -m:1` failed in 32 s with 132 unrelated missing namespace/type errors, including `Hecton8.Environment.Fluids`, `Hecton8.Audio.Propagation`, `Hecton8.Audio.Virtualization`, `Hecton8.Physics.CCD`, `MacroSwarm`, and `SoundEmissionSignal`.

Integrator note:
- Do not treat this as verified green. Unity compile remains PENDING VERIFICATION until the editor session and global asmdef dependency wall are fixed.

## 2026-05-14 - DSP_ACOUSTIC_LEAD - Loop 6 Material/Mass Upgrade
Status: PENDING VERIFICATION

What was wrong:
- The renderer still inferred high-speed material from source kind, which made player/vehicle/leviathan impacts too generic.
- Player and vehicle high-speed packets were not writing authored material IDs, effective mass, or material hash even though the signal contract supports them.
- Mass reconstruction from lost energy was acceptable fallback behavior, but not the best path for AAA scaling when actual rigidbody mass exists.

What was done:
- `HectonPlayerMotor` and `VehicleMotor` now resolve target impact material through `IPhysicsImpactMaterialProvider`, set source material as metal, write `EffectiveMass`, and compose `MaterialHash`.
- `PlayerCriticalProceduralAudioRenderer` now prefers `signal.EffectiveMass` for `0.5 * mass * speedSq`, keeps lost-energy fallback for legacy packets, and routes material IDs into clang, echo, hollow resonance, pitch, and duplicate hashing.
- `AdvancedAcousticsSmokeTester` now asserts effective-mass and high-speed material consumption.
- Verified `FaunaBrain` already writes equivalent high-speed material/mass fields in HEAD.

Cinematic cheats used:
- Material is a compact byte family, not a surface-accurate contact solver.
- Organic/metal/glass switches scale existing clang/echo/pitch multipliers instead of adding new PCM layers.
- Low tier still exits to one baked clip; material work only improves high-speed packet admission and DSP scalar mapping.

Exact microseconds saved:
- Avoided a new material resolver service: 0 extra persistent allocations and no new queue.
- Reused existing `IPhysicsImpactMaterialProvider`: one event-only lookup per emitted high-speed impact.
- Renderer material blend: byte switches and scalar multipliers, estimated <2 us per accepted impact.
- Kept signal size at 96 bytes: no lane memory growth.

Verification:
- `git diff --check` passed except CRLF normalization warnings.
- `rg PlayClipAtPoint` returned no matches.
- Owned kinetic scans found no new `foreach`, `math.exp`, `math.normalize`, `.ToString()`, `string.Format`, or interpolation hits.
- Unity MCP validation failed at transport level: `http://127.0.0.1:8088/mcp`.
- First `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -p:UseSharedCompilation=false -m:1` failed with `CS2001` because `Assets/_Project/Scripts/UI/DiegeticTooltipSystem.cs` was deleted while still referenced by the project file.
- After another process restored that UI file, the rerun reached the existing 132-error global namespace/asmdef wall: examples include `Hecton8.Environment.Fluids`, `Hecton8.Physics.CCD`, `Hecton8.Audio.Propagation`, `Hecton8.Audio.Virtualization`, `MacroSwarm`, and `AcousticAup`.

## 2026-05-14 - DSP_ACOUSTIC_LEAD - Loop 7 Echo Tap Queue Churn Re-Audit
Status: PENDING VERIFICATION

What was wrong:
- Kinetic impact echoes generated one tap but still cleared, enqueued, and drained through the shared sonar upload queue.
- That path added unnecessary native queue traffic and could discard unrelated pending active-sonar upload work if both paths touched the queue in the same frame.
- `CURRENT_BATCH.md` has rotated and no longer contains `KINETIC_IMPACT_ACOUSTICS`; persistent task files remain the active memory for this prompt.

What was done:
- `TryPublishKineticImpactEchoTap` now writes the generated `SonarEchoTap` directly into `inactiveTapBuffer[0]` and publishes `tapCount = 1`.
- Active sonar batching remains unchanged on `NativeQueue<SonarEchoTap>`.
- `AdvancedAcousticsSmokeTester` now asserts the direct kinetic tap write.

Cinematic cheats used:
- Collision echo remains a single authored procedural tap into the existing binaural/portal echo lane.
- No new acoustic ray solver, new queue, or material-specific PCM layer was added.
- Low tier remains baked clip only; high/ultra spend the saved work on the existing material-colored thud/clang/echo stack.

Exact microseconds saved:
- Saves up to 32 guarded queue dequeue attempts from `ClearSonarEchoTapUploadQueue`.
- Saves one `NativeQueue.Enqueue` plus one `TryDequeue` per accepted high-tier kinetic echo.
- Removes one shared-queue contention/stomp surface for collision echo admission.
- Runtime allocation delta: 0 B/frame.

Verification:
- `git diff --check -- Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs` passed except CRLF normalization warnings.
- `rg -n "PlayClipAtPoint" Assets/_Project/Scripts` returned no matches.
- Targeted owned-file scan found only editor/cold `AdvancedAcousticsSmokeTester` assertion text for `math.exp` and `builder.ToString()`.
- Source readback after a shared-workspace overwrite confirms `inactiveTapBuffer[0] = tap` exists in both renderer and smoke tester.
- Historical note: one `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -p:UseSharedCompilation=false -m:1` pass succeeded before the overwrite was detected, but that is not final proof.
- Final `Hecton8.Core.csproj` rerun after reapplying the patch failed with `CS2012` because `Unity.RenderPipelines.Universal.Runtime.dll` was locked by another process.
- Unity MCP resources are empty/unavailable in this context.
- `Assembly-CSharp.csproj` and `Assembly-CSharp-Editor.csproj` builds timed out; spawned dotnet build-server/processes were shut down or stopped. Unity compile remains PENDING VERIFICATION.

## 2026-05-15 - DSP_ACOUSTIC_LEAD - Loop 8 Duplicate Impact Admission H-Phi Pass
Status: PENDING VERIFICATION

What was wrong:
- Same-frame high-speed collision dedupe remembered only the immediately previous packet, so interleaved A/B/A producer output could play the same impact twice.
- Accepted impacts computed the FNV signature twice: once for duplicate check and once when recording the last packet.
- Invalid impact packets were hashed before finite rejection.

What was done:
- `PlayerCriticalProceduralAudioRenderer` now owns an 8-entry fixed `HighSpeedImpactDuplicateEntry` ring plus the existing last-packet fast path.
- Each duplicate-ring entry has a valid byte so zeroed cold entries cannot suppress a legitimate frame 0 or zero-signature packet.
- `TryHandleHighSpeedImpactSignal` finite-checks before hashing, computes `ResolveHighSpeedImpactSignature(in signal)` once, and records that precomputed signature on both low-tier and procedural accepted paths.
- `AdvancedAcousticsSmokeTester` now asserts the fixed ring, precomputed-signature record call, and valid-entry guard.

Cinematic cheats used:
- Duplicate handling is packet-signature based, not a spatial contact solver.
- The ring is fixed at 8 entries because the scan cap is 32 and the purpose is repeated packet suppression, not global collision history.
- Low tier remains one baked clip; high/ultra get fewer false duplicate thuds/echo taps inside the same DSP lane.

Exact microseconds saved:
- Saves one 10-field FNV mix on every accepted impact.
- Saves all signature hashing for invalid speed/energy packets.
- Adds at most 8 struct comparisons per scanned signal, estimated under 2 us worst-case for the 32-signal cap on i3/MX350.
- Prevents full duplicate thud/clang/echo render windows when a producer repeats A/B/A in one frame; runtime allocation delta remains 0 B/frame.

Verification:
- `git diff --check -- Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs` passed except CRLF normalization warnings.
- `rg -n -F "KineticImpactDuplicateHistoryCapacity = 8"` found renderer and smoke tester anchors.
- `rg -n -F "RecordHighSpeedImpactSignal(signal.Frame, signalSignature)"` found low-tier/procedural record calls and the smoke anchor.
- `rg -n -F "entry.Valid != 0"` found renderer and smoke tester anchors.
- `rg -n -F "IsDuplicateHighSpeedImpactSignal(in signal)"` returned no matches.
- `rg -n -F "PlayClipAtPoint" Assets/_Project/Scripts/Audio Assets/_Project/Scripts/Gameplay Assets/_Project/Scripts/Core` returned no matches.
- Targeted owned-file scan found only pre-existing cold/editor `Debug.Log`/`ToString()`/assertion text, not new hot-path allocation.
- Dotnet build/rebuild was not run by explicit user order. Unity compile remains PENDING VERIFICATION until Editor console/MCP validation is available.

## 2026-05-15 - DSP_ACOUSTIC_LEAD - Loop 9 Kinetic Policy Cache H-Phi Pass
Status: PENDING VERIFICATION

What was wrong:
- Kinetic impact admission still read scalability tier and low-memory state from `GlobalRegistry` inside the packet path.
- The MX350/low-tier baked fallback resolved `GlobalRegistry.Audio` every time a fallback impact clip was queued.
- A per-frame unconditional policy refresh would have been fake cleanup: it moves registry coupling rather than bounding it.

What was done:
- Added `KineticImpactQualityPolicyRefreshFrames = 30`, `_kineticImpactLowTierFallback`, and `RefreshKineticImpactQualityPolicyIfStale(Time.frameCount)`.
- `Tick` warms the policy cache on a stale cadence; direct service calls also refresh only if stale.
- Added `_kineticLowTierAudioService` and `ResolveKineticLowTierAudioService()`; the cached interface is cleared on disable/destroy.
- `AdvancedAcousticsSmokeTester` now asserts the policy-cache constant, stale-refresh call, and cached low-tier audio-service helper.

Cinematic cheats used:
- The low-tier gate remains a coarse device/memory policy, not a continuously simulated acoustic budget.
- The 30-frame cache cadence is deliberate: collision audio LOD does not need per-packet tier polling.
- High/Ultra still spend budget on procedural thud/clang/echo; Low/MX350 keeps the baked clip.

Exact microseconds saved:
- Saves two registry reads per scanned high-speed packet after cache warmup; worst-case 32-packet scan avoids up to 64 registry reads in that frame.
- Saves one registry read on warm cached low-tier fallback clip admission.
- Added work is one integer stale check and one policy refresh every 30 frames; runtime allocation delta remains 0 B/frame.

Verification:
- `git diff --check -- Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs` passed except CRLF normalization warnings.
- `rg -n -F "KineticImpactQualityPolicyRefreshFrames = 30"` found renderer and smoke tester anchors.
- `rg -n -F "RefreshKineticImpactQualityPolicyIfStale(Time.frameCount)"` found renderer tick/admission calls and the smoke anchor.
- `rg -n -F "ResolveKineticLowTierAudioService()"` found renderer fallback call/helper and the smoke anchor.
- Scoped hot-path scan found no `PlayClipAtPoint`; broad owned-file scan found only pre-existing cold/editor `Debug.Log`, `math.exp` assertion text, and `builder.ToString()`.
- Source-only H-Phi spot counts for the renderer: `GlobalRegistry=30`, `SignalBus=1`, `NativeArray=232`, `StructLayout=6`, `UpdateMethods=0`, `FindObject=0`, `GetComponent=10`, `KineticPolicyCache=7`.
- Dotnet build/rebuild was not run by explicit user order. Unity compile remains PENDING VERIFICATION until Editor console/MCP validation is available.

## 2026-05-15 - DSP_ACOUSTIC_LEAD - Loop 10 Audio Service Cache And Component Lookup H-Phi Pass
Status: PENDING VERIFICATION

What was wrong:
- Cave reverb and binaural target sampling still resolved `GlobalRegistry.Audio` directly inside audio tick paths.
- Granular voice count, reverb DSP tier, sonar SDF probe count, and kinetic fallback still had scattered quality/scalability policy reads.
- Optional `PlayerTransportCoordinator` fallback lookup could retry in two transport-audio helpers every tick while absent.

What was done:
- Added `_spatialAudioManager` and `ResolveSpatialAudioManager()`; cave reverb and binaural sampling now use cached spatial-audio service resolution.
- Added cached audio quality policy fields for scalability tier, quality tier, and low-memory profile, refreshed through `RefreshAudioQualityPolicyIfStale(Time.frameCount)`.
- Moved granular voice count, reverb DSP tier, sonar SDF probe count, and kinetic fallback onto cached policy values.
- Added `TransportCoordinatorLookupRetryFrames = 30` and `TryResolvePlayerTransportCoordinator()`; transport audio helpers share that bounded resolver.
- Added smoke assertions for cached spatial audio, cached quality/scalability values, and cadence-gated transport lookup.

Cinematic cheats used:
- Audio LOD policy is a coarse 30-frame scalar cache, not a continuous hardware budget simulation.
- Reverb/binaural telemetry still comes from the existing spatial audio service; no new acoustic solver or queue was added.
- Optional transport coordinator recovery is cadence-based, preserving behavior without per-frame component search.

Exact microseconds saved:
- Saves two `GlobalRegistry.Audio` reads per normal DSP tick after spatial-audio cache warmup.
- Saves 3-5 quality/scalability registry reads per active tick/probe path after warmup, with one policy refresh per 30 frames.
- Saves up to two failed `TryGetComponent` calls per tick when the optional transport coordinator is absent.
- Runtime allocation delta remains 0 B/frame.

Verification:
- `git diff --check -- Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs` passed except CRLF normalization warnings.
- `rg -n -F "GlobalRegistry.ScalabilityTier"` now returns only the cache refresh assignment.
- `rg -n -F "GlobalRegistry.QualityTier"` now returns only the cache refresh assignment.
- `rg -n -F "GlobalRegistry.H8_LOW_MEMORY_PROFILE"` now returns only the cache refresh assignment.
- `rg -n -F "ResolveSpatialAudioManager()"` found renderer and smoke tester anchors.
- `rg -n -F "TransportCoordinatorLookupRetryFrames = 30"` and `rg -n -F "TryResolvePlayerTransportCoordinator()"` found renderer and smoke tester anchors.
- Scoped hot-path scan found no `PlayClipAtPoint`; broad owned-file scan found only pre-existing cold/editor `Debug.Log`, `math.exp` assertion text, and `builder.ToString()`.
- Source-only H-Phi spot counts for the renderer: `GlobalRegistry=26`, `SignalBus=1`, `NativeArray=232`, `StructLayout=6`, `UpdateMethods=0`, `FindObject=0`, `GetComponent=9`, `CachedQuality=14`, `CachedSpatial=9`, `TransportLookupGate=5`.
- Dotnet build/rebuild was not run by explicit user order. Unity compile remains PENDING VERIFICATION until Editor console/MCP validation is available.

## 2026-05-15 - DSP_ACOUSTIC_LEAD - Loop 11 Cross-Domain Resolver Cadence H-Phi Pass
Status: PENDING VERIFICATION

What was wrong:
- Low-tier biome reverb still pulled `GlobalRegistry.MapMagic` from the cave reverb tick path.
- Forward echo probe and ambient-pressure audio fallback read `GlobalRegistry.Player` through separate helpers.
- Apex heartbeat threat and structural hull fallback still used direct service locator reads; structural fallback could retry three hull-service reads in one tick while absent.

What was done:
- Added `AudioServiceLookupRetryFrames = 30` for optional cross-domain resolver retries.
- Added cached MapMagic biome policy through `ResolveCachedBiomeId()`.
- Added cached player runtime context through `ResolvePlayerRuntimeContext()`.
- Added cached ecosystem and hull read-model resolvers through `ResolveEcosystemDirectorService()` and `ResolveSubmarineHullReadModel()`.
- Reset structural retry gates when player/transport binding changes.
- Added smoke assertions for all bounded cross-domain resolver helpers.

Cinematic cheats used:
- Biome reverb remains a cached 2-bit flavor branch, not a live terrain acoustic simulation.
- Cross-domain audio cues still consume scalar contracts; no new object graph, physics query, or solver was added.
- Missing optional services are retried on cadence instead of every audio tick.

Exact microseconds saved:
- Saves one MapMagic registry read per low-tier cave reverb tick after warmup.
- Saves up to two player-context registry reads per active tick/probe path after warmup.
- Saves one ecosystem registry read per SlowTick after warmup.
- Saves up to three structural hull fallback registry reads per tick while the read model is absent.
- Runtime allocation delta remains 0 B/frame.

Verification:
- `git diff --check -- Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs` passed except CRLF normalization warnings.
- `rg -n "ResolvePlayerRuntimeContext\\(|ResolveEcosystemDirectorService\\(|ResolveSubmarineHullReadModel\\(|ResolveCachedBiomeId\\(|AudioServiceLookupRetryFrames = 30"` found renderer and smoke tester anchors.
- `rg -n -F "PlayClipAtPoint" Assets/_Project/Scripts/Audio Assets/_Project/Scripts/Gameplay Assets/_Project/Scripts/Core` returned no matches.
- Broad owned-file scan found only pre-existing cold/editor `Debug.Log`, `math.exp` assertion text, and `builder.ToString()`.
- Source-only H-Phi spot counts for the renderer: `GlobalRegistry=23`, `SignalBus=1`, `NativeArray=232`, `StructLayout=6`, `UpdateMethods=0`, `FindObject=0`, `GetComponent=9`, `CachedQuality=14`, `CachedSpatial=9`, `CrossDomainResolver=16`, `TransportLookupGate=5`.
- Dotnet build/rebuild was not run by explicit user order. Unity compile remains PENDING VERIFICATION until Editor console/MCP validation is available.

## 2026-05-15 - DSP_ACOUSTIC_LEAD - Loop 12 Deep Psychosis Audio Resolver H-Phi Pass
Status: PENDING VERIFICATION

What was wrong:
- `DeepPsychosisController` still read player, environmental strain, audio service, and acoustic-zone registry slots directly from SlowTick/dependency/cue methods.
- Helmet whisper fallback went straight to `GlobalRegistry.AcousticZone` from the cue path.
- The source smoke suite did not guard this DSP-owned psychosis cue path against future direct registry polling.

What was done:
- Added cached resolvers for `IPlayerRuntimeContext`, `EnvironmentalStrainManager`, `IAudioService`, and `AcousticZoneController`.
- Bound all four optional service refreshes to the existing 30-frame dependency retry cadence.
- Replaced direct psychosis SlowTick/dependency/cue service reads with resolver calls.
- Routed helmet whisper fallback through `PlayHelmetWhisperCue()` over the cached acoustic-zone reference.
- Added editor smoke assertions for the deep psychosis resolver helpers and method-body no-direct-registry checks.

Cinematic cheats used:
- Kept deterministic xorshift cue placement and authored clip pools instead of any physical hallucination or acoustic simulation.
- Kept pooled `IAudioService.PlayAtPoint`; no AudioSource spawn, coroutine scheduler, or clip synthesis was added.
- Service refresh is cadence-bound because psychosis cues do not need frame-perfect registry rebinding.

Exact microseconds saved:
- Saves up to one player-context registry read per dependency refresh after warmup.
- Saves one environmental-strain registry read per active SlowTick after warmup.
- Saves one audio-service registry read per psychosis cue playback after warmup.
- Saves up to one acoustic-zone registry read per helmet whisper fallback after warmup.
- Runtime allocation delta remains 0 B/frame.

Verification:
- `git diff --check -- Assets/_Project/Scripts/Audio/DeepPsychosisController.cs Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs` passed except CRLF normalization warnings.
- `rg -n -F "GlobalRegistry." Assets/_Project/Scripts/Audio/DeepPsychosisController.cs` shows direct reads only in registration and resolver refresh bodies.
- Source counters for `DeepPsychosisController`: `GlobalRegistry=11`, `CachedResolvers=8`, `GetComponent=3`, `FindObject=0`, `UpdateMethods=0`, `NewHot=0`.
- Scoped forbidden scan found no `PlayClipAtPoint`, `PlayOneShot`, coroutine, managed collection, or hot formatting in `DeepPsychosisController`.
- Dotnet build/rebuild was not run by explicit user order. Unity compile remains PENDING VERIFICATION until Editor console/MCP validation is available.

## 2026-05-15 - DSP_ACOUSTIC_LEAD - Loop 13 Acoustic Zone Audio Service Cache H-Phi Pass
Status: PENDING VERIFICATION

What was wrong:
- `AcousticZoneController` scattered direct `GlobalRegistry.Audio` reads across transition, madness, vegetation, fatal-pressure, sonar, manta, storm, ambient-routing, and emitter-occlusion paths.
- Emitter occlusion pattern-matched the concrete `SpatialAudioManager` from the registry every update.
- The smoke tester only guarded acoustic-zone native queue payloads, not audio service lookup hygiene.

What was done:
- Added a 30-frame acoustic-zone audio service resolver cache.
- Added cached concrete `SpatialAudioManager` resolution for emitter occlusion.
- Replaced transition/static/sonar/vegetation/manta/storm cue service reads with `ResolveAudioService()`.
- Replaced emitter-occlusion concrete service lookup with `ResolveSpatialAudioManager()`.
- Cleared cached audio services on disable/destroy.
- Added smoke assertions for resolver anchors and no-direct-registry method bodies.

Cinematic cheats used:
- Kept authored transition/static clips and pooled `PlayStatic2D`; no simulated acoustic fluid, coroutine, or AudioSource spawn was introduced.
- Emitter occlusion still uses the existing fixed 24-sample active-emitter copy, not a new ray grid or acoustic solver.
- Service rebinding is cadence-bound because these cues do not require frame-perfect registry polling.

Exact microseconds saved:
- Saves one audio-service registry read on each transition cue after warmup.
- Saves one read on madness whispers, vegetation pulses, fatal-pressure pulses, sonar fallback, manta misfire, and storm pulses after warmup.
- Saves one concrete audio-service registry read per emitter-occlusion update after warmup.
- Runtime allocation delta remains 0 B/frame.

Verification:
- `git diff --check -- Assets/_Project/Scripts/AcousticZoneController.cs Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs` passed except CRLF normalization warnings.
- `rg -n -F "GlobalRegistry.Audio" Assets/_Project/Scripts/AcousticZoneController.cs` returns only the resolver refresh body.
- Source counters for `AcousticZoneController`: `GlobalRegistry.Audio=1`, `ResolveAudioService=10`, `ResolveSpatial=2`, `FindObject=0`, `UpdateMethods=0`, `PlayClipAtPoint=0`, `StartCoroutine=0`.
- Scoped forbidden scan found only pre-existing cold/editor diagnostics and the cold `new List<AudioSource>(32)` bootstrap buffer.
- Dotnet build/rebuild was not run by explicit user order. Unity compile remains PENDING VERIFICATION until Editor console/MCP validation is available.

## 2026-05-15 - DSP_ACOUSTIC_LEAD - Loop 14 Music Director Runtime Resolver H-Phi Pass
Status: PENDING VERIFICATION

What was wrong:
- `HectonMusicDirector` read player, acoustic-zone, and audio-service registry slots directly from dependency/base-context/mixer-routing methods.
- Music resolver hygiene had no smoke coverage, so future changes could reintroduce direct polling in context reevaluation paths.

What was done:
- Added cached player runtime context, audio service, and acoustic-zone resolver helpers.
- Routed dependency resolution, base-context detection, and mixer routing through those helpers.
- Cleared music director runtime service caches on disable/destroy.
- Added smoke assertions for resolver anchors and method-body no-direct-registry checks.

Cinematic cheats used:
- Kept authored music profiles, dual voice pool, and mixer group routing; no adaptive music solver or runtime clip generation was added.
- Service rebinding is cadence-bound at 30 frames because music context does not require frame-perfect registry polling.

Exact microseconds saved:
- Saves one player registry read per dependency refresh after warmup.
- Saves one acoustic-zone registry read per base-context evaluation after warmup.
- Saves one audio-service registry read per mixer routing resolution after warmup.
- Runtime allocation delta remains 0 B/frame.

Verification:
- `git diff --check -- Assets/_Project/Scripts/Audio/HectonMusicDirector.cs Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs` passed except CRLF normalization warnings.
- Direct player/audio/acoustic registry reads in `HectonMusicDirector` are now confined to resolver refresh bodies.
- Source counters for `HectonMusicDirector`: `GlobalRegistryPlayer=1`, `GlobalRegistryAudio=1`, `GlobalRegistryAcoustic=1`, `ResolverCalls=6`, `FindObject=0`, `UpdateMethods=0`, `StartCoroutine=0`.
- Scoped forbidden scan found only editor/development diagnostics and editor smoke strings.
- Dotnet build/rebuild was not run by explicit user order. Unity compile remains PENDING VERIFICATION until Editor console/MCP validation is available.

## 2026-05-15 - DSP_ACOUSTIC_LEAD - Loop 15 Spatial Audio Service Policy Resolver H-Phi Pass
Status: PENDING VERIFICATION

What was wrong:
- `SpatialAudioManager` owns repeated spatial policy and optional-service paths: virtual physical voice limit, listener AUP, acoustic-zone interior state, acoustic portal policy, global wind howl, and water-density muffle.
- Those paths need hard smoke coverage so future edits do not reintroduce direct `GlobalRegistry` polling in the guarded method bodies.
- The active `Docs/Tasks/CURRENT_BATCH.md` has rotated to unrelated agents, so the original assignment must remain sourced from the persistent kinetic status/rationale files.

What was done:
- Verified spatial policy caching through `SpatialAudioPolicyRefreshFrames = 30`.
- Verified optional player/weather/acoustic-zone/surface-weather service lookup caching through `SpatialAudioRegistryRetryFrames = 30`.
- Confirmed `RefreshVirtualPhysicalVoiceLimit`, `TryResolvePlayerListenerAup`, `IsListenerInteriorZoneActive`, `TryResolveAcousticPortalPath`, `ShouldUseAcousticPortalPath`, `ResolveGlobalWindHowlTarget01`, `ResolveGlobalWindHowlOccluded`, and `UpdateListenerWaterDensityMul` consume cached resolver helpers.
- Extended `AdvancedAcousticsSmokeTester` with direct guards for portal policy, voice-limit policy, listener AUP, water-density update, wind target, and wind occlusion method bodies.

Cinematic cheats used:
- Spatial service rebinding is cadence-bound at 30 frames because audio LOD, portal policy, wind, and water muffle do not need frame-perfect registry refresh.
- No new acoustic solver, object search, or event lane was added; the system spends saved lookup budget on existing portal/wind/virtualization effects.
- Low tier remains cheap through cached policy and bounded optional-service fallback; high tier keeps the same richer spatial paths.

Exact microseconds saved:
- Saves two policy registry reads per virtual voice or acoustic portal policy refresh after warmup.
- Saves one player registry read on listener AUP and water-density update paths after warmup.
- Saves one acoustic-zone registry read in interior checks and wind occlusion paths after warmup.
- Saves one weather and one surface-weather registry read in wind howl target/occlusion paths after warmup.
- Runtime allocation delta remains 0 B/frame.

Verification:
- `git diff --check -- Assets/_Project/Scripts/SpatialAudioManager.cs Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs` passed except CRLF normalization warnings.
- Direct spatial policy/service registry reads are confined to resolver refresh bodies: `GlobalRegistry.ScalabilityTier`, `GlobalRegistry.H8_LOW_MEMORY_PROFILE`, `GlobalRegistry.Player`, `GlobalRegistry.Weather`, `GlobalRegistry.AcousticZone`, and `GlobalRegistry.SurfaceWeather`.
- Source counters for `SpatialAudioManager`: `PolicyDirect=2`, `RuntimeServiceDirect=4`, `PlayerCriticalAudioDirect=2`, `PolicyResolvers=6`, `RuntimeResolvers=12`, `FindObject=0`, `UpdateMethods=0`.
- Source counters for the smoke tester: `SmokeSpatialResolverAsserts=16`.
- Scoped forbidden scan found only pre-existing comments/cold diagnostics/editor smoke strings: no new runtime `PlayClipAtPoint`, coroutine, managed hot collection, `math.exp`, or string formatting was introduced by this pass.
- Dotnet build/rebuild was not run by explicit user order. Unity compile remains PENDING VERIFICATION until Editor console/MCP validation is available.

## 2026-05-15 - DSP_ACOUSTIC_LEAD - Loop 16 Music Director World-State Resolver H-Phi Pass
Status: PENDING VERIFICATION

What was wrong:
- `HectonMusicDirector` still read `DepthZone`, `SurfaceWeather`, and `FirstHour` registry slots directly after the earlier player/audio/acoustic resolver pass.
- Direct reads were present in music dependency refresh, storm pressure, depth stinger gates, rare discovery gates, and first-hour pressure boost.
- The smoke tester did not guard those remaining music world-state lookup paths.

What was done:
- Added cached `ResolveDepthZoneDirector()`, `ResolveSurfaceWeatherDirector()`, and `ResolveFirstHourDirector()` helpers on the existing 30-frame music dependency cadence.
- Cleared the new runtime service caches with the existing music cache reset path.
- Replaced direct depth-zone, surface-weather, and first-hour registry reads in guarded music tension/stinger methods.
- Extended `AdvancedAcousticsSmokeTester` with music world-state resolver anchors and method-body no-direct-registry checks.

Cinematic cheats used:
- Music tension still uses authored scalar pressure inputs; no adaptive composition solver or world-state packet expansion was added.
- First-hour, storm, and depth gates are cadence-bound because authored music decisions do not require frame-perfect registry refresh.
- The low-tier path keeps cheap authored routing while high-tier scenes keep storm/depth/first-hour stinger polish.

Exact microseconds saved:
- Saves one depth-zone registry read per dependency refresh after warmup.
- Saves one surface-weather registry read per storm pressure refresh after warmup.
- Saves up to one first-hour registry read per guarded depth/rare-discovery/tension evaluation after warmup.
- Runtime allocation delta remains 0 B/frame.

Verification:
- `git diff --check -- Assets/_Project/Scripts/Audio/HectonMusicDirector.cs Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs` passed.
- Direct music registry reads are now confined to resolver refresh bodies: `GlobalRegistry.Player`, `GlobalRegistry.Audio`, `GlobalRegistry.AcousticZone`, `GlobalRegistry.DepthZone`, `GlobalRegistry.SurfaceWeather`, and `GlobalRegistry.FirstHour`.
- Source counters for `HectonMusicDirector`: `DirectPlayer=1`, `DirectAudio=1`, `DirectAcousticZone=1`, `DirectDepthZone=1`, `DirectSurfaceWeather=1`, `DirectFirstHour=1`, `ResolverCalls=15`, `FindObject=0`, `UpdateMethods=0`, `StartCoroutine=0`.
- Source counters for the smoke tester: `SmokeMusicResolverAsserts=16`.
- Scoped forbidden scan found only existing editor/development diagnostics and editor smoke strings.
- Dotnet build/rebuild was not run by explicit user order. Unity compile remains PENDING VERIFICATION until Editor console/MCP validation is available.

## 2026-05-15 - DSP_ACOUSTIC_LEAD - Loop 17 Prologue And Vocal Warning Regression Guard H-Phi Pass
Status: PENDING VERIFICATION

What was wrong:
- `PrologueAcousticOrchestrator` is a late-frame audio bridge and must not drift back into live quality-policy registry polling.
- `VocalWarningSystem` already used cached services and scalability events, but its `Tick`/`SlowTick` hot paths lacked explicit smoke guards against future registry/string/log regressions.
- The active batch file remains rotated to unrelated agents, so this loop stayed sourced from persistent kinetic audio task files.

What was done:
- Verified prologue quality policy seeding through `RefreshQualityPolicyCold()` and live tier updates through `IScalabilityChangedEventListener`.
- Verified `LateFrameTick` has no direct `GlobalRegistry.ScalabilityTier`, `GlobalRegistry.ScalabilityTierProfileByte`, or `GlobalRegistry.H8_LOW_MEMORY_PROFILE` polling.
- Extended `AdvancedAcousticsSmokeTester` prologue assertions for scalability event registration, cache updates, and no late-frame quality refresh.
- Extended `AdvancedAcousticsSmokeTester` vocal-warning assertions for cold service seeding, scalability event registration, and hot-path absence of registry polling, `.ToString()`, and `Debug.Log`.

Cinematic cheats used:
- Prologue audio keeps a scalar low-tier/proxy flag and authored transition state instead of any physical re-entry acoustic simulation.
- Quality rebinding is event/cold-cache driven because prologue audio transition publishing does not require frame-perfect policy polling.
- VWS remains an authored warning queue with native byte/cooldown buffers, not a dynamic speech synthesis system.

Exact microseconds saved:
- Prologue saves three registry reads every previous 60-frame quality refresh window after warmup.
- VWS runtime cost is unchanged; the saved cost is future-risk prevention in `Tick` and `SlowTick`.
- Runtime allocation delta remains 0 B/frame in guarded paths.

Verification:
- `git diff --check -- Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs Assets/_Project/Scripts/Audio/VocalWarningSystem.cs Assets/_Project/Scripts/Audio/Prologue/PrologueAcousticOrchestrator.cs` passed except CRLF normalization warning on the editor smoke file.
- Prologue counters: `PrologueDirectQuality=3`, `PrologueLateFrameQualityPoll=0`, `ScalabilityEventCalls=3`, `SmokePrologueAsserts=9`, `FindObject=0`, `UpdateMethods=0`, `StartCoroutine=0`.
- Vocal counters: `VocalTickRegistry=0`, `VocalSlowRegistry=0`, `VocalTickStrings=0`, `VocalSlowStrings=0`, `SmokeVocalAsserts=13`, `VocalScalabilityEvents=3`.
- Scoped forbidden scan found only editor smoke diagnostics and assertion text.
- Dotnet build/rebuild was not run by explicit user order. Unity compile remains PENDING VERIFICATION until Editor console/MCP validation is available.

## 2026-05-15 - DSP_ACOUSTIC_LEAD - Loop 18 Critical Renderer Scalability Event H-Phi Pass
Status: PENDING VERIFICATION

What was wrong:
- `PlayerCriticalProceduralAudioRenderer` still hid quality-policy registry reads behind `RefreshAudioQualityPolicyIfStale`.
- The helper was 30-frame cadence-gated, but it was still reachable from `Tick`, reverb tier selection, kinetic fallback, and sonar probe LOD.
- `GlobalRegistry.ScalabilityTier`, `GlobalRegistry.QualityTier`, and `GlobalRegistry.H8_LOW_MEMORY_PROFILE` belonged in a cold seed/event path, not in a hot helper.

What was done:
- Added `IScalabilityChangedEventListener` to the renderer and registered/unregistered it with `ScalabilityEvents`.
- Added `RefreshAudioQualityPolicyCold()` for cold seeding only.
- Replaced hot cadence refresh calls with `EnsureAudioQualityPolicyCached()`, which contains no registry reads.
- Preserved hardware `_cachedQualityTier` for native reverb tier instead of overwriting it from the two-profile scalability event byte.
- Extended `AdvancedAcousticsSmokeTester` to guard cold-only registry seeding and no direct quality registry polling in renderer hot methods.

Cinematic cheats used:
- Quality state is a cached scalar policy, not a per-frame adaptive DSP negotiation.
- If cache seeding is missed, the renderer falls back to Unknown/low-memory true so toaster behavior wins over accidental overkill.
- High-tier audio still spends budget on native reverb, granular voice count, sonar probes, and kinetic impact polish rather than service lookup.

Exact microseconds saved:
- Saves up to three registry reads every previous 30-frame quality refresh window after warmup.
- Removes hidden registry lookup spikes from kinetic impact fallback admission, reverb tier selection, and sonar probe LOD.
- Runtime allocation delta remains 0 B/frame.

Verification:
- `git diff --check -- Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs` passed except CRLF normalization warnings.
- Old cadence symbols are absent: `RefreshAudioQualityPolicyIfStale`, `RefreshKineticImpactQualityPolicyIfStale`, and `KineticImpactQualityPolicyRefreshFrames`.
- Direct quality registry reads are confined to `RefreshAudioQualityPolicyCold()`.
- Method-body counters: `TickQualityRegistry=0`, `ReverbQualityRegistry=0`, `KineticFallbackQualityRegistry=0`, `SonarProbeQualityRegistry=0`, `EnsureQualityRegistry=0`, `ColdQualityRegistry=3`.
- Scoped forbidden scan found only pre-existing editor/cold diagnostics and assertion text.
- Dotnet build/rebuild was not run by explicit user order. Unity compile remains PENDING VERIFICATION until Editor console/MCP validation is available.

## 2026-05-15 - DSP_ACOUSTIC_LEAD - Loop 19 Spatial Audio Scalability Event H-Phi Pass
Status: PENDING VERIFICATION

What was wrong:
- `SpatialAudioManager` still used `RefreshSpatialAudioPolicyIfStale` to poll scalability and low-memory registry state every 30 frames.
- The hidden reads were reachable from virtual voice limit and acoustic portal policy paths.
- The smoke tester still described the spatial quality policy as cadence-gated instead of event/cold-cache driven.

What was done:
- Added `IScalabilityChangedEventListener` to `SpatialAudioManager`.
- Added cold seeding through `RefreshSpatialAudioPolicyCold()` and event updates through `OnScalabilityChanged`.
- Replaced hot policy refresh calls with `EnsureSpatialAudioPolicyCached()`, which has no registry reads.
- Kept `SpatialAudioRegistryRetryFrames = 30` intact for optional player/weather/acoustic-zone/surface-weather service lookups.
- Updated `AdvancedAcousticsSmokeTester` spatial assertions for event registration, cold-only policy seeding, and no hidden registry reads in the hot policy guard.

Cinematic cheats used:
- Spatial quality remains a scalar policy cache, not a frame-perfect hardware negotiation.
- Unseeded policy defaults to Unknown/low-memory true, preserving the toaster path before visual/audio overkill.
- High-tier portal and virtualization behavior keeps the saved budget for actual spatial audio work, not platform service lookup.

Exact microseconds saved:
- Saves two registry reads every previous 30-frame spatial policy refresh window after warmup.
- Removes hidden scalability/low-memory lookup spikes from virtual voice and portal policy paths.
- Runtime allocation delta remains 0 B/frame.

Verification:
- `git diff --check -- Assets/_Project/Scripts/SpatialAudioManager.cs Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs` passed except CRLF normalization warnings.
- Old source symbols are absent: `SpatialAudioPolicyRefreshFrames` and `RefreshSpatialAudioPolicyIfStale`.
- Direct spatial quality registry reads are confined to `RefreshSpatialAudioPolicyCold()`.
- Method-body counters: `EnsureSpatialPolicyRegistry=0`, `ColdSpatialPolicyRegistry=2`, `ResolveCachedTierRegistry=0`, `ResolveCachedLowMemoryRegistry=0`, `VoiceLimitPolicyRegistry=0`, `PortalPolicyRegistry=0`.
- Scoped forbidden scan found only pre-existing editor/cold diagnostics and assertion text.
- Dotnet build/rebuild was not run by explicit user order. Unity compile remains PENDING VERIFICATION until Editor console/MCP validation is available.

## 2026-05-15 - DSP_ACOUSTIC_LEAD - Loop 20 Spatial Foveated Director Resolver H-Phi Pass
Status: PENDING VERIFICATION

What was wrong:
- `RefreshFoveatedDirector()` still polled `GlobalRegistry.FoveatedSimulationDirector` from the spatial audio slow lane.
- Virtual voice foveation only needs an optional cached director, not a service-locator read on every slow tick.
- The smoke tester did not guard foveated-director lookup confinement.

What was done:
- Added `_foveatedDirectorResolveFrame`.
- Added `ResolveFoveatedSimulationDirector()` using `SpatialAudioRegistryRetryFrames = 30`.
- Converted `RefreshFoveatedDirector()` to delegate to the bounded resolver.
- Preserved existing semantics where a missing registry sample does not discard a cached director unless no cached director exists.
- Extended `AdvancedAcousticsSmokeTester` with foveated resolver and no-direct-slow-lane registry checks.

Cinematic cheats used:
- Virtual voice foveation remains a cached scalar tier input, not a per-voice service resolution.
- Missing optional service keeps the default active tier path; no expensive fallback discovery was added.
- High-tier scenes still get foveated virtual voice priority when the service is present, with lookup work bounded.

Exact microseconds saved:
- Saves one optional service-locator read per spatial SlowTick after warmup.
- Runtime allocation delta remains 0 B/frame.

Verification:
- `git diff --check -- Assets/_Project/Scripts/SpatialAudioManager.cs Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs` passed except CRLF normalization warnings.
- Direct foveated registry reads are confined to `ResolveFoveatedSimulationDirector()`.
- Method-body counters: `SlowTickFoveatedRegistry=0`, `RefreshFoveatedRegistry=0`, `ResolveFoveatedRegistry=1`, `VirtualVoiceTierRegistry=0`.
- Scoped forbidden scan found only pre-existing editor/cold diagnostics and assertion text.
- Dotnet build/rebuild was not run by explicit user order. Unity compile remains PENDING VERIFICATION until Editor console/MCP validation is available.

## 2026-05-15 - DSP_ACOUSTIC_LEAD - Loop 21 Spatial Player-Critical Runtime Hot-Swap Cache H-Phi Pass
Status: PENDING VERIFICATION

What was wrong:
- Player-critical procedural audio forwarding must not read `GlobalRegistry.PlayerCriticalAudio` from queue admission paths.
- The runtime cache needed an early play-mode `OnEnable()` seed so prologue handoff is not dependent on `_isInitialized`.
- Smoke coverage only checked a narrow string and did not prove hot-swap unregister/rebind hygiene.

What was done:
- Kept `QueuePrologueAudioTransition()` and `QueueHighSpeedImpactSignal()` on `_cachedPlayerCriticalAudio`.
- Moved play-mode runtime cache seeding and hot-swap listener registration into `OnEnable()` before the `_isInitialized` branch.
- Retained the idempotent `InitializeService()` seed.
- Verified `SpatialAudioManager` receives ref-forwarded and compatibility hot-swap callbacks for `GlobalRegistryServiceSlot.PlayerCriticalAudioRuntime`.
- Hardened `AdvancedAcousticsSmokeTester` to require hot-swap unregister, ref callback, slot handling, payload cache update, cold-only seed, and no `GlobalRegistry.` in the queue methods.

Cinematic cheats used:
- Impact/prologue admission is a cached pointer check, not a dynamic service discovery pass.
- Missing renderer fails closed instead of searching the scene or allocating a fallback.
- High-tier collision audio keeps budget for procedural transient synthesis/radar cues rather than registry lookup.

Exact microseconds saved:
- Saves one player-critical service-locator read per prologue transition forwarding after cache warmup.
- Saves one player-critical service-locator read per valid high-speed impact forwarding after cache warmup.
- Runtime allocation delta remains 0 B/frame.

Verification:
- `git diff --check -- Assets/_Project/Scripts/SpatialAudioManager.cs Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs` passed except CRLF normalization warnings.
- Duplicate symbol scan shows one `RefreshCachedAudioRuntimeServicesCold`, one `TryRegisterHotSwapListener`, one `TryUnregisterHotSwapListener`.
- Method-body counters: `QueuePrologue GlobalRegistry=0`, `QueueHighSpeed GlobalRegistry=0`, `ColdPlayerCriticalAudio=1`, `CacheRebound GlobalRegistry=0`, `HotSwapCallbacks GlobalRegistry=0`.
- Scoped forbidden scan found only pre-existing editor/cold diagnostics, comments, and assertion strings.
- Dotnet build/rebuild was not run by explicit user order. Unity compile remains PENDING VERIFICATION until Editor console/MCP validation is available.

## 2026-05-15 - DSP_ACOUSTIC_LEAD - Loop 22 Habitat Portal Construction Cache H-Phi Pass
Status: PENDING VERIFICATION

What was wrong:
- Habitat acoustic portal graphing still pulled `GlobalRegistry.ConstructionRuntime` directly from `TryBuildHabitatAcousticPortalGraph()`.
- The portal path is audio-owned, but the dependency source is logistics/construction; the correct bridge is the registry hot-swap lane.
- Smoke coverage did not guard this portal graph method against direct registry polling.

What was done:
- Added `_cachedConstructionManager` to `SpatialAudioManager`.
- Cold-seeded `_cachedConstructionManager` through `RefreshCachedAudioRuntimeServicesCold()`.
- Cleared the cached construction manager during service shutdown.
- Updated the cache on `GlobalRegistryServiceSlot.Logistics` hot-swap payloads.
- Replaced the portal graph method's direct registry read with `_cachedConstructionManager`.
- Added smoke assertions for cold-only construction seed, logistics rebind, payload cache update, and no `GlobalRegistry.` in `TryBuildHabitatAcousticPortalGraph()`.

Cinematic cheats used:
- Habitat acoustics remain a bounded portal graph fake, not full room acoustic simulation.
- The graph route uses cached construction data; no scene search, no physics query, no allocation fallback.
- High-tier portal richness spends CPU on graph traversal and attenuation, not service lookup.

Exact microseconds saved:
- Saves one construction service-locator read per habitat acoustic portal graph attempt after cache warmup.
- Runtime allocation delta remains 0 B/frame.

Verification:
- `git diff --check -- Assets/_Project/Scripts/SpatialAudioManager.cs Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs Docs/Tasks/Status_KINETIC_IMPACT_ACOUSTICS.md Docs/AgentLogs/Rationale_KINETIC_IMPACT_ACOUSTICS.md Docs/AgentLogs/LOG_KINETIC_IMPACT_ACOUSTICS.md` passed except CRLF normalization warnings.
- Method-body counters: `HabitatPortalGraph GlobalRegistry=0`, `PortalPath GlobalRegistry=0`, `ColdConstructionRuntime=1`, `CacheRebound GlobalRegistry=0`.
- Scoped forbidden scan found only pre-existing editor/cold diagnostics, comments, and assertion strings.
- Dotnet build/rebuild was not run by explicit user order. Unity compile remains PENDING VERIFICATION until Editor console/MCP validation is available.
