# Rationale - AUDIO_MATERIAL_SYNTHESIZER

Status: ACOUSTICS PROFILED - PENDING UNITY VERIFICATION  
Evidence: STATIC_DOC / FILESYSTEM / CLI_TOOLING

## Decision 1 - Scope Boundary

Problem: The prompt asks for acoustic material profiling and an echo-tap simulation, but existing audio runtime ownership already sits in `SpatialAudioManager` and `PlayerCriticalProceduralAudioRenderer`.

Solution: Keep this batch in data, documentation, and offline Python simulation. The data is shaped for the existing DSPGraph/SPSC contracts and can be consumed later by the runtime owner without introducing a parallel manager.

Rejected Alternatives: A new Unity runtime acoustic manager was rejected because it would compete with `SpatialAudioManager`. Direct managed one-shot playback or event-string routing was rejected by the DSP mandate.

Scalability potential: Low uses scalar coefficients, nearest RT60 lookup, mono/near-stereo cues. Middle uses richer material echo color and denser zone updates. High uses early reflection taps and native spatializer. Ultra spends saved budget on 7.1/binaural detail and convolution tails only after profiler proof.

Hardware Impact: On low-end silicon like i3/MX350, offline-baked scalars avoid per-frame acoustic solving and keep the runtime path inside cached snapshots. Estimated gain: 5-25 us per active acoustic zone update versus computing material response/taps at runtime; profiler proof absent.

## Decision 2 - Fake-First Acoustics

Problem: Real acoustic simulation for 20 materials, granular events, sonar returns, and Leviathan doppler would require geometry queries, convolution, and per-source spectral work that exceeds the 0.1 ms suspicion threshold without proof.

Solution: Use deterministic perceptual fakes: scalar absorption/reflection/transmission, low-pass cutoffs, authored echo tap densities, granular grain parameters, and a clamped pitch curve. The Python simulator proves the fake envelope and clipping budget offline.

Rejected Alternatives: Wave-equation propagation, full HRTF convolution as default, and per-material runtime impulse response generation were rejected. They are not required for gameplay truth and violate the underwater audio mandate on MX350.

Scalability potential: Low: 6 cone rays, 4 radar sources, scalar LPF/reverb. Middle: 12 cone taps and material-colored delays. High: 24 taps plus hybrid early reflections. Ultra: 7.1 object routing and convolution tails with the same data authority.

Hardware Impact: Low-tier path stays visually and sonically convincing through filtering, sub-bass, and delayed taps. High-tier path can be excessive through denser taps and spatial detail without changing gameplay state. Estimated low-end saving: 0.1-0.4 ms avoided versus live acoustic IR generation; measured proof absent.

## Decision 3 - Psychoacoustic Dread

Problem: The Alpha Leviathan needs a panic response without turning underwater audio into a fragile velocity-true doppler simulator. Raw doppler is weak underwater and can flicker if tied directly to radial velocity.

Solution: Use a clamped authored pitch fake for the critical roar only: physical ratio is stored as reference, authored pitch ratio is limited to +/-2 semitones, and the curve requires 128-sample smoothing plus 2.5 seconds hysteresis. Sub-bass at 28 Hz carries the dread channel while the main roar stays intelligible.

Rejected Alternatives: Global underwater doppler was rejected because the HRTF mandate states doppler is not mandatory underwater and stable pitch/pressure cues are preferred. Full per-source velocity truth was rejected because it spends CPU on a cue the player will not reliably localize underwater.

Scalability potential: Low uses one sub-bass layer and scalar ducking. Middle adds material echo color. High adds controlled early reflections around the roar. Ultra can add 7.1 movement and binaural ambiguity after profiler proof, but the same curve remains the authority.

Hardware Impact: On i3/MX350 the curve is a table lookup and gain scalar after cold data import. Estimated runtime cost is below 1 us per critical roar update, profiler proof absent. The saved budget is spent on stronger high-tier spatial tails instead of more physics.

## Decision 4 - All-Material Clipping Audit

Problem: A default-material clipping pass is not enough evidence for the prompt requirement that 16 voices never exceed 1.0 amplitude. A louder material profile could breach even if `steel_hull` passed.

Solution: `Tools/AudioSim.py` now audits every material row in `Data/Audio/Acoustic_Material_Profiles.json` and fails the CLI if any 16-voice dry-plus-tap bound exceeds 1.0. The latest run reports 20 materials, 0 failures, worst case `aluminum_panel`, peak bound 0.98000000.

Rejected Alternatives: Sampling only steel or titanium was rejected because it leaves a silent data regression path. Trusting a runtime limiter was rejected because clipping should be prevented before the DSPGraph soft clipper has to hide it.

Scalability potential: Low uses the same precomputed gain-bound logic for scalar material profiles. Middle/High/Ultra can add denser taps, but every material still has to pass the same all-material audit before data is accepted.

Hardware Impact: Runtime cost remains 0 us until data is consumed because this is offline validation. Future runtime path can import the safe gain ceiling instead of discovering overload during playback.
