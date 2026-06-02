# LOG_PhaseSafetyAuditor

## 2026-06-01 - Static Phase Safety Audit

Agent: PhaseSafetyAuditor
Domain: Echelon 8 Presentation/UX phase safety audit crossing rendering, VFX, audio, UI, and transform presentation.
Task count: 1
Source edits: none.
Build/runtime: not run by explicit mission constraint.
Batch extraction: `CURRENT_BATCH.md` not present; assignment came from chat `<SUB_AGENT_PROMPT>`.

### What Was Wrong
Direct Unity presentation writes were found inside hot phases. These violate the simulation-before-presentation rule because renderer/material/particle/audio/UI/transform presentation mutation must flush from settled snapshots in `LateFrameTick` or `VISUAL_SYNC`, not during `Tick`/`FixedTick`.

### Concrete Violations
1. `Assets/_Project/Scripts/LandingImpactVFX.cs`
   - `Tick(float deltaTime)` at line 267 calls `ApplyPostProcessing()` at line 274.
   - `ApplyPostProcessing()` starts line 565 and writes URP volume override values:
     - line 573: `_chromatic.intensity.value = ...`
     - line 583: `_vignette.intensity.value = ...`
     - lines 592-593: `_vignette.smoothness.value` / `_vignette.rounded.value`
     - lines 597-601: fallback vignette smoothness/rounded writes.
   - Why unsafe: GPU/post-process presentation state is mutated from update phase.
   - Local patch: implement `ILateFrameTickable`; `Tick` computes pending chromatic/vignette DTO fields and registers LateFrame; LateFrame applies the volume `.value` writes.
   - Exact microseconds saved: 0 us measured. Estimated target after patch: 6-25 us/frame on i3/MX350 when active; lower on high-end, but avoids phase jitter.

2. `Assets/_Project/Scripts/Interaction/VRValveWheelHandle.cs`
   - `Tick(float deltaTime)` starts line 166.
   - `Tick` calls `ApplyControllerAngularDeltaDegrees(...)` at line 207.
   - `ApplyControllerAngularDeltaDegrees(...)` calls `ApplyWheelVisual()` at line 163.
   - `ApplyWheelVisual()` starts line 240 and writes `_resolvedVisual.localRotation = ...` at line 243.
   - Why unsafe: visual wheel rotation is presentation transform mutation in tick phase.
   - Local patch: keep `_accumulatedDegrees`/`_isOpen01` simulation state in Tick; queue pending wheel rotation and flush transform in `LateFrameTick`.
   - Cinematic cheat: existing no-trig approximate axis rotation is acceptable; move it to LateFrame or cache the quaternion in Tick.
   - Exact microseconds saved: 0 us measured. Estimated target after patch: 2-12 us/frame per active wheel on low-end CPU.

3. `Assets/_Project/Scripts/Vehicles/DropPod/DropPodAirlockController.cs`
   - `IFixedTickable.FixedTick(float fixedDeltaTime)` starts line 174.
   - FixedTick calls `ApplyHatchRotation(...)` at line 201.
   - `ApplyHatchRotation(float t)` starts line 301 and writes `hatch.localRotation = ...` at line 307.
   - FixedTick also calls `DispatchHandTarget()` at line 202, which routes hand IK targets at lines 347-369.
   - Why unsafe: hatch visual transform and IK presentation route are driven from fixed simulation phase.
   - Local patch: FixedTick advances `_seal01` and queues hatch pose / hand target DTO; `LateFrameTick` applies hatch transform and dispatches IK target.
   - Cinematic cheat: hatch can remain smooth by NLERP in LateFrame; no physics realism needed for visible door interpolation.
   - Exact microseconds saved: 0 us measured. Estimated target after patch: 3-18 us/fixed-frame while hatch moves.

4. `Assets/_Project/Scripts/Interaction/LifePodSeatStrapLatch.cs`
   - `Tick(float deltaTime)` starts line 219.
   - `Tick` can call `CompleteLatch(...)` at line 243.
   - `CompleteLatch(...)` starts line 286 and calls `ApplyLatchedVisual()` at line 296.
   - `ApplyLatchedVisual()` starts line 300 and writes `strapVisual.localRotation = ...` at line 308.
   - Why unsafe: strap visual rotation is written inside tick completion path.
   - Local patch: latch truth remains in Tick; queue latched visual rotation and flush in LateFrame. `ResetLatchVisualState()` line 269 is cold/public path and should use the same queue when called during play.
   - Exact microseconds saved: 0 us measured. Estimated target after patch: 1-8 us per latch completion on low-end hardware.

5. `Assets/_Project/Scripts/Gameplay/MantaScooter.cs`
   - `Tick(float deltaTime)` starts line 613.
   - `Tick` calls `TickDriveRelease(...)` at line 640.
   - `TickDriveRelease(...)` starts line 1242 and can call `DeactivateScooter()` at line 1254.
   - `DeactivateScooter()` starts line 747 and calls `RestoreHeadlightDefaults()` at line 757.
   - `RestoreHeadlightDefaults()` starts line 2006, `RestoreHeadlight(...)` starts line 2017.
   - Direct Light writes:
     - line 2022: `headlight.color = ...`
     - line 2024: `headlight.intensity = ...`
     - line 2025: `headlight.range = ...`
   - Why unsafe: Light component presentation state is mutated from tick phase during deactivation.
   - Local patch: queue headlight reset into the existing `LateFrameTick` presentation path that already handles `_headlightPresentationDirty` and global clear.
   - Cinematic cheat: headlight shutdown can be a one-frame LateFrame flush or a quality-scaled fade; no gameplay truth should depend on Light fields.
   - Exact microseconds saved: 0 us measured. Estimated target after patch: 3-20 us on deactivation frame, plus reduced simulation lane jitter.

### Local Patch Candidates / Boundary Notes
- `Assets/_Project/Scripts/Gameplay/BaseAirlock.cs`: `Tick` line 461 can reach `TeleportPlayer(...)` and `TeleportBody(...)`; `TeleportBody` writes `Rigidbody.position` and `Rigidbody.rotation` at lines 1105-1106. This is gameplay authority/physics state, not pure visual presentation. Owner should either document it as an authoritative teleport route or move it to a fixed/post-fixed owner lane. Do not patch blindly from Echelon 8.
- `Assets/_Project/Scripts/HectonPlayerMovement.cs`: `FixedTick` can call `VisorHUDController.TriggerEnvironmentalDistortion(...)` / `GlitchPulse(...)`; inspected `VisorHUDController` and found these only set timers/dirty flags, while material writes occur in `LateFrameTick` via `ApplyMaterialProperties()`. Candidate for stricter DTO/event routing, not a direct GPU write.

### Inspected Non-Violations / False Positives
- `Assets/_Project/Scripts/BaseModule.cs`: Tick queues oxygen hum state; `AudioSource.Play/Stop/volume/pitch` flush in `FlushOxygenScrubberHumVisualState()` from `LateFrameTick`.
- `Assets/_Project/Scripts/AcousticZoneController.cs`: Tick queues audio transitions; ambient source mutation flushes in LateFrame.
- `Assets/_Project/Scripts/Gameplay/PlayerActionController.cs`: Tick queues camera bob/audio; LateFrame flushes.
- `Assets/_Project/Scripts/Interaction/PhysicalBatteryCompartment.cs`: Tick queues snap/door visuals; LateFrame applies transforms except cold/immediate teardown paths.
- `Assets/_Project/Scripts/Interaction/PhysicalSnapSwitch.cs`: Tick queues angle; LateFrame applies lever rotation.
- `Assets/_Project/Scripts/World/SargassumCollapseChunk.cs`: Tick queues particle emission; LateFrame applies `ParticleSystem` emission/play/stop.
- `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs`: Tick queues fade mask; LateFrame writes shader global.
- `Assets/_Project/Scripts/World/SargassumGlobalDragManager.cs`: Tick queues dynamic texture/shader/render work; LateFrame applies GPU state and rendering.
- `Assets/_Project/Scripts/World/Biolum/HectonBiolumManager.cs`: Tick calculates/queues biolum globals; LateFrame performs shader writes.
- `Assets/_Project/Scripts/Environment/GlobalWeatherDirector.cs` and `Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs`: Tick computes state; LateFrame writes weather shader/audio/visor presentation.
- `Assets/_Project/Scripts/Lighting/InteriorGIProbeVolumeRuntime.cs`: Tick schedules simulation; LateFrame publishes completed GPU upload/shader buffers.
- `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs`: queued visual tick is executed in `LateFrameTick`; large visual writes are LateFrame-owned despite stale comments mentioning Tick.
- `Assets/_Project/Scripts/AmbientWaterMotionManager.cs`: Tick accumulates visual delta; LateFrame writes bobbing transforms.
- `Assets/_Project/Scripts/AtlasSignal/SignalBeacon.cs`: Tick solves beacon state; LateFrame writes shader static.

### What Was Done
- Read project authority files and phase/zero-GC/render/VFX/audio/UI/signal mandates.
- Scanned `Assets/_Project/**/*.cs` for `Tick`, `FixedTick`, `Update`, `FixedUpdate`, and `Execute` scopes with renderer/material/shader/particle/audio/UI/transform tokens.
- Ran a local call-chain scan for hot methods that reach helper methods containing presentation writes.
- Manually inspected high-signal candidates and separated direct violations from existing queue/flush implementations.
- No C# source files were edited.

### Cinematic Cheats Used
- Recommended NLERP/approximate quaternion continuation for valve, hatch, and strap visuals; no need for physical simulation.
- Recommended DTO pending fields and one-frame LateFrame flush for post-process and light writes.
- Recommended continuous `GlobalQualityWeight` scaling by cadence/amplitude/fade strength, not binary low/ultra switches.

### Exact Microseconds Saved
- Verified saved time: 0 us. This was an audit-only pass with no runtime profiler or source patch.
- Expected integrator patch target: remove 1-5 Unity presentation writes from hot lanes per active offender. Estimated low-end i3/MX350 impact: 1-80 us/frame depending on offender activity and Unity component path. These are estimates, not measured claims.
