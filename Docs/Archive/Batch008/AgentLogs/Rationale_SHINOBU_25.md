# Rationale_SHINOBU_25

Agent: SHINOBU_25
Status: TASKS IMPLEMENTED / COMPILE BLOCKED BY EXTERNAL DEPENDENCIES

## Pre-Code Analysis

[ANALYSIS]
Target: deterministic seismic director producing raw shake offsets, turbidity scalar, debris/audio/damage/fauna signals, and editor control facade without camera MonoBehaviour shake.
Affected systems: Environment/Atmosphere seismic domain, AUP math, GlobalDataVault-like native storage, typed signal lanes, crash telemetry, editor diagnostics. Direct camera, silt, debris, fauna, combat, and audio implementations are not owned and must be mocked or decoupled.
Zero GC proof: hot paths must use fixed NativeArray or fixed buffers, index for-loops, no LINQ, no string formatting, no Unity object mutation, no runtime allocations, and no GlobalRegistry polling inside Tick or Burst jobs.
State check: fixed 16 seismic event slots, 300 telemetry slots, explicit inactive Magnitude == 0 sentinel, no Dictionary/Pool reliance in hot paths, no double SlowTick dependency, safe disabled state if dependencies are missing.
Rule quote: Cinematic Cheat Protocol says default solution is deterministic presentation fake; prompt mandates no terrain deformation, no Transform.localPosition camera shake, and VR comfort clamps rotation to zero with translation <= 0.05m.
[/ANALYSIS]

## Non-Trivial Decisions

Problem: Earthquake must feel catastrophic without SDF rebuilds or camera transform shake.
Solution: Use deterministic oscillator data, silt/debris/acoustic/fauna/damage signals, and a raw ShakeOffsetDTO consumed later by the render/VR pipeline.
Rejected Alternatives: Unity CameraShake MonoBehaviour, PerlinNoise in Update, terrain deformation, direct references to Camera/Silt/Debris systems.
Scalability potential: Low uses sine-only oscillator and clamped turbidity; Middle adds deterministic cheap noise; High adds richer signal fan-out; Ultra raises visual overkill through consumers, not through seismic CPU cost.
Hardware Impact: Estimated low-end i3/MX350 gain versus terrain rebuild/camera MonoBehaviour approach: avoids multi-ms rebuild spikes and GC; target seismic kernel remains under 0.1 ms pending profiler proof.

Problem: VR rotation during quake causes sickness and conflicts with tracking late-latching.
Solution: In comfort mode, zero RotationEuler and clamp TranslationOffset length to 0.05 m before writing ShakeOffsetDTO.
Rejected Alternatives: Rotational roll/pitch camera shake, Transform.localRotation changes, per-camera late Update overrides.
Scalability potential: Low and VR comfort get stable head tracking; High/Ultra non-VR may allow bounded rotation for cinematic output.
Hardware Impact: No measurable extra cost; saves corrective camera work and avoids unstable coordinate jitter.

Problem: Adding SHINOBU_25 buffer names to the global memory enum dirties a massive core file and contributes to compile-wall churn.
Solution: Keep seismic buffer identity as local typed `BufferID`/`SystemID` cast constants in `SeismicDirectorConstants`; remove SHINOBU_25 enum additions from `H8Memory.cs`.
Rejected Alternatives: Expanding `H8Memory.cs` with domain-specific enum labels, or adding new asmdef references to make the names visible.
Scalability potential: Low/Middle/High/Ultra all use the same numeric Vault slots without forcing unrelated agents to recompile on seismic label churn.
Hardware Impact: Runtime cost unchanged; developer iteration avoids unnecessary core-source invalidation.

Problem: Existing tide/seismic telemetry owned a private `NativeArray`, which violates the H-Phi data-sovereignty rule for new critical-state work.
Solution: Move the legacy tide telemetry ring handle to `GlobalDataVault` alongside the new 300-frame seismic director blackbox.
Rejected Alternatives: Leaving the pre-existing `NativeArray` because it was outside the new prompt; creating another private native ring for quake telemetry.
Scalability potential: Low devices get one central Vault memory ownership path; high/ultra can add richer telemetry without local owner arrays.
Hardware Impact: Hot writes stay contiguous; estimated 1-5 us saved during ownership checks and leak accounting versus private NativeArray registration.

Problem: New quake fan-out signals could allocate their first lane during the earthquake frame.
Solution: Define compact unmanaged partial signal structs and prewarm their typed `SignalBus<T>` lanes during service initialization.
Rejected Alternatives: String UnityEvents, direct calls into debris/audio/fauna systems, or first-push queue allocation during a quake.
Scalability potential: Low tier can shed low-capacity signal lanes; high/ultra can consume the same contracts for visual overkill.
Hardware Impact: Moves NativeQueue/NativeList cold allocation out of the quake frame; avoids unpredictable first-event hitch.

Problem: Compile verification is required, but the current Core project fails on unrelated ecosystem/binary-manifest symbols before SHINOBU_25 can be proven clean.
Solution: Record the exact failing compile command and dependency-wall errors; continue local SHINOBU implementation without editing the unrelated ecosystem domain.
Rejected Alternatives: Refactoring/fixing `BinaryLayoutManifest`, `GlobalWorldSampler`, or ecosystem installer from the seismic domain.
Scalability potential: Keeps domain boundary intact while preserving honest verification state.
Hardware Impact: No runtime impact; avoids a cross-domain compile-wall spiral.

Problem: A quake must feel chaotic without Perlin camera shake or heavyweight rigidbody simulation.
Solution: Burst oscillator sums bounded sine waves and optional 3D simplex noise into one Vault `ShakeOffsetDTO`, using inverse-square/edge falloff around each epicenter.
Rejected Alternatives: MonoBehaviour `Update()` camera shake, Transform mutation, per-object force propagation, or terrain deformation.
Scalability potential: Low/Toaster uses sine-only; Middle/High add noise; Ultra can consume the same offsets for richer render/audio effects.
Hardware Impact: Keeps quake math to 16 fixed slots and one output write; estimated 80-600 us/frame saved versus camera/object fan-out.

Problem: Physical cave collapse is expensive and domain-hostile.
Solution: For magnitude > 8, emit typed debris/acoustic/damage/panic signals and let owned systems fake rocks, mud, dents, and audio pressure.
Rejected Alternatives: SDF/terrain rebuilds, spawning Unity GameObjects, or directly invoking debris/audio/fauna classes.
Scalability potential: Low tier can shed cosmetic lanes; High/Ultra can turn the same signals into visual overkill without changing seismic truth.
Hardware Impact: Avoids multi-ms world rebuild and Instantiate spikes; cost becomes a few unmanaged signal pushes.

Problem: Long-tail quake rumble needs persistence without managed timers.
Solution: Mutate the Vault event slot magnitude in the Burst job with `math.exp(-DecayRate * dt)` and use `Magnitude == 0` as the inactive sentinel.
Rejected Alternatives: Coroutine decay, `List<SeismicEvent>` lifetime management, or allocating new quake objects.
Scalability potential: Fixed 16 slots scale identically across hardware; high tier spends extra budget in consumers rather than state management.
Hardware Impact: 5-30 us/event update saved and zero GC.

Problem: The same quake math must survive both toaster hardware and high-end visual overkill.
Solution: Drive the oscillator from `SystemHealthIndex`: low tier drops simplex noise and clamps turbidity, while high/ultra retain noise and export richer signal fan-out.
Rejected Alternatives: One-size-fits-all noise, GPU-choking silt spikes, or quality-specific code paths in downstream systems.
Scalability potential: Low uses sine-only and capped silt; Middle uses bounded noise; High/Ultra allow consumers to add debris, audio, and silt overkill.
Hardware Impact: Estimated low-tier gain 10-60 us CPU plus reduced particle overdraw pressure.

Problem: 100km AUP coordinates will jitter if absolute positions are cast to float before distance math.
Solution: Subtract camera AUP and epicenter AUP as `double3`, then cast only the local delta to `float3` for falloff/oscillator math.
Rejected Alternatives: `float3` absolute world positions, Unity `Transform.position` as quake truth, or sector-blind distance checks.
Scalability potential: Same math works at origin and 50km out; high/ultra can add visual effects without corrupting gameplay radius.
Hardware Impact: CPU delta is negligible; prevents visible coordinate jitter and false damage/debris radius.

Problem: VR users cannot receive rotational quake shake without sickness and tracking conflicts.
Solution: Treat Vault/XR comfort bit as hard law: zero rotation, clamp translation to 0.05m, and publish zero camera jitter while still emitting silt/debris/audio signals.
Rejected Alternatives: roll/pitch/yaw camera shake, camera Transform offsets, or per-HMD corrective scripts.
Scalability potential: VR low/high share stable head tracking; non-VR high/ultra can still consume bounded rotation.
Hardware Impact: Avoids camera-correction work and prevents comfort regressions; no measurable added CPU.

Problem: Event spawning must not allocate or shift arrays during gameplay.
Solution: Use 16 fixed Vault slots with `Magnitude <= .01` as inactive and weakest-slot overwrite fallback when all slots are active.
Rejected Alternatives: `List<T>`, `new SeismicEventDTO` object wrappers, queue growth, or moving active slots to compact.
Scalability potential: Same fixed memory footprint from toaster to ultra; high-tier overkill lives in consumers.
Hardware Impact: Trigger-time cost is bounded to 16 slot probes; estimated 20-100 us/event and all GC avoided.

Problem: A seismic failure must be diagnosable without relying on console text.
Solution: Write a 300-frame 64-byte telemetry ring in the Vault and dump it to `Docs/AgentLogs/Dump_SEISMIC_DIRECTOR.bin` on compute wait, raw translation >5m, or invalid math flags.
Rejected Alternatives: `Debug.Log`, profiler-only proof, or local private NativeArray blackbox.
Scalability potential: Low gets fixed tiny telemetry; high/ultra can add consumers without changing the ring contract.
Hardware Impact: One 64B write per frame; forensic value outweighs negligible memory bandwidth.

Problem: Designers need seismic control without recompiling C#.
Solution: Provide `Tectonic Event Tuner` EditorWindow and zero-allocation CSV key parser writing directly to Vault tuning memory.
Rejected Alternatives: ScriptableObject-only runtime copies, JSON parsing, `string.Split`, or editor buttons that mutate MonoBehaviour fields instead of Vault truth.
Scalability potential: Low/Middle/High/Ultra tuning can be adjusted by data while the kernel remains fixed.
Hardware Impact: Hot path unchanged; editor poll avoids managed CSV churn and limits file reads to 0.5s cadence.

Problem: The final compile attempt changed failure surface after restore but still remained outside SHINOBU_25.
Solution: Record that restore succeeded and the build now fails in SaveSystem, GlobalTelemetryBus, SomaticKinematics, TerminalOS, and PredatorCognition, with no emitted seismic director errors.
Rejected Alternatives: Editing persistence/telemetry/fauna/UI domains from the seismic agent, or claiming a clean build without evidence.
Scalability potential: Keeps SHINOBU_25 isolated and leaves an exact dependency note for integration.
Hardware Impact: No runtime impact; protects developer iteration from cross-domain repair spiral.
