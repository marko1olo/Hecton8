# [ARCHIVE] Pre-Strict Architecture Snapshot

Date: 2026-05-24
Owner: X_012 DOCUMENTATION_CLEANUP_AND_ACTUALIZATION_ENGINE
Original: Docs/ARCHITECTURE/SHINOBU_353_HAPTIC_SYNTHESIS_ROUTE_CARD.md
Rule: historical snapshot only; not active doctrine.

# SHINOBU_353 Haptic Synthesis Route Card

Date: 2026-05-23
Evidence class: STATIC_DOC / STATIC_SOURCE
Status: ACCEPTED_STATIC / YELLOW_RUNTIME_PROOF_PENDING

Route ID: SHINOBU_353_HAPTIC_SYNTHESIS
Owner: `InputDispatcher`
Owner domain: HAPTIC_FEEDBACK_EVENT_TRANSLATOR
Owning file/system: `Assets/_Project/Scripts/Core/HectonInputRuntime_HapticSynth.cs`

Problem: Physics, damage, and tool systems need tactile presentation without direct managed `Gamepad.current.SetMotorSpeeds` calls from physics or gameplay producers.
Why owner-local data is insufficient: Impact, high-speed impact, damage, and tool acoustic events have multiple first-party producers and cannot be owned by the PAL haptic drain.
Why direct caller/owner interface is insufficient: The producers are in physics/gameplay/tool domains and must not depend on concrete input-device runtime classes.

Instrument:
  [ ] GlobalRegistry cold service/interface
  [x] SignalBus<T> first-party broadcast
  [ ] GlobalSignals bridge/direct queue
  [ ] HectonEventBus mod/API/cold event
  [x] GlobalDataVault / IDataVault
  [x] Black-box/telemetry route

Producer/consumer phase: Producers publish typed unmanaged signals during their owner phases. `InputDispatcher` prewarms required haptic Vault handles and requests `GlobalSignals.EnsureHapticPulseSignalLaneInitialized()` during cold dispatcher registration, then refuses the haptic dispatcher route until those handles resolve. `HapticSynthesisSimulationSystem` schedules the Burst synthesis/coalescence chain in `DispatcherPhase.Simulation`; `HapticSynthesisPostSimulationSystem` consumes the completed dispatcher fence in `DispatcherPhase.PostSimulation`; `InputDispatcher.DrainToolHaptics` remains the only hardware PAL write phase.
Cadence/capacity: Continuous `GlobalQualityWeight` maps synthesis cadence from 0.016 seconds at weight 1.0 to 0.1 seconds at weight 0.0. The same scalar maps material-profile scan depth from 12.5% to 100% of the active profile table and coalescence reads only the generated pulse prefix when telemetry is available. `HapticPulseSignal` capacity is 8; synthesis scratch pulse capacity is 64; telemetry ring is 300 entries; haptic profile CSV scratch is `BufferID.ShinobuHapticSynthesisCsvScratch byte[4096]`.
Expected max events/reads per frame: reads up to configured current-frame snapshots for `ImpactSignal`, `HighSpeedImpactSignal`, `CombatDamageSignal`, and `ToolAcousticSignal`; emits at most one final `HapticPulseSignal` and one `HapticCommandDTO`.
GlobalQualityWeight behavior: Cadence, material-profile scan depth, detail blending, and weighted coalescence scale continuously. Layout, save identity, rollback authority, and signal route do not change with quality.

Accessor purity:
  [x] No Get/TryGet/Resolve/Read API publishes signals
  [x] No Get/TryGet/Resolve/Read API syncs scene state
  [x] No Get/TryGet/Resolve/Read API completes jobs
  [x] No Get/TryGet/Resolve/Read API searches the scene
  [x] Runtime global registry use is cold registration/unregistration only
  [!] `EnsureHapticSynthesisNativeBuffers` and `GlobalSignals.EnsureHapticPulseSignalLaneInitialized` can acquire native storage only during owner setup, dispatcher registration, DataVault rebind, or fail-closed fallback if the dispatcher route is unavailable. The normal registered simulation/post-simulation path is prewarmed by registration, but Play Mode proof is still required before GREEN.

Payload/data shape:
Managed fields present: no in runtime DTOs and signals.
UnityEngine.Object fields present: no in runtime DTOs and signals.
Layout proof: `HapticPulseSignal` 16 bytes; `HapticPhysicalImpulseDTO` 64 bytes; `HapticProfileDTO` 32 bytes; `HapticTuningDTO` 32 bytes; `HapticTelemetryEntry` 64 bytes; CSV scratch is raw byte storage and not a DTO.
Overflow/failure: Overflow drops pulses and sets `HapticSynthesisFaultFlags.PulseOverflow`; NaN is sanitized; exact synchronous fallback timing over 200 microseconds flags telemetry and requests dump. The dispatcher-scheduled path records a timing proxy but does not raise budget faults from dispatcher-wide latency until profiler proof is attached.

Telemetry fields: frame, player AUP, raw signal count, dropped count, generated pulse count, final low/high amplitude, burst microseconds, flags, quality, state hash.
Black-box fields: 300-entry `BufferID.ShinobuHapticSynthesisTelemetryRing`, dumped to `Docs/AgentLogs/Dump_SHINOBU_353.bin` on NaN, overflow, or budget breach.
Profiler marker: pending Unity profiler proof. Static timing/proxy field exists but runtime profiler artifact is absent.
GC proof required: Play Mode GCMonitor 0 B/frame for active collision/tool haptic route.

Shutdown/disposal: `InputDispatcher.ReleaseInputVaultHandles` calls `ReleaseHapticSynthesisVaultHandles`, unregisters dispatcher systems, releases all haptic synthesis Vault handles, and clears local cursors. DataVault rebind releases old handles, reacquires deterministic/haptic buffers when initialized, and re-registers the haptic dispatcher route from the registry phase.
Scene unload behavior: handles are released through the cached `IDataVault`; stale handles fail closed through `TryResolveInputBuffer`.
Stale-handle behavior: `TryResolveInputBuffer` rejects zero BufferID, failed resolve, uncreated buffer, or insufficient length.

Rejected alternatives:
  [x] owner-local field: rejected because producers span physics/gameplay/tools
  [x] cached owner interface: rejected because haptics need many producer fan-in, not one caller
  [x] existing SignalBus lane only: existing source lanes are reused, but final PAL envelope needs its own narrow payload
  [x] existing Vault buffer: input haptic command buffer is PAL output, not synthesis scratch/black-box storage
  [x] cold HectonEventBus hook: rejected because this is first-party hot gameplay presentation
  [x] direct hardware calls: rejected outside PAL

Why this does not increase global monolith risk: The route consumes existing typed signals, stores bounded native scratch/telemetry buffers, emits one narrow `HapticPulseSignal`, and keeps hardware ownership in `InputDispatcher`.
H-Phi impact expected: Static route clarity improves by moving haptic synthesis scratch and telemetry into named BufferIDs without adding gameplay authority.
Proof required before GREEN: Unity import, controller device verification, GCMonitor capture, Burst/profiler timing capture, and clean compile after the current external Combat/VFX/Construction build wall is resolved.
Reviewer: Integrator / Core Architecture
Review disposition: YELLOW
Reason: Static source and route-card evidence are present; runtime/profiler/player proof is not.
Required fixes before GREEN: attach Unity import, controller device, profiler, and GC artifacts; static source now fences haptic Vault acquisition to cold registration/rebind/fallback paths.
