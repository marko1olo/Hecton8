# UX_HPHI_SIGNAL_WIRING Final Report

Status: H-PHI VERIFIED (compile proof blocked by upstream assembly wall)
Domain: ECHELON 8 - PRESENTATION & UX

## What Was Wrong
- Main gameplay UI ownership was still compatible with dispatcher Update-lane execution instead of VISUAL_SYNC-only reactive binding.
- HUD dirty evaluation was cadence-heavy and only partially signal-aware.
- Interaction prompt glyph changes were not driven by `InputStateSignal` snapshots.
- Low-tier fallback work needed an explicit 15Hz Math LOD gate.
- Telemetry lacked a scoped `ActiveUiUpdatesPerFrame` counter.

## What Was Done
- `SuitHUDV4CanvasOverlay` consumes `PlayerStateSignal`, `InventoryChangedSignal`, `InputStateSignal`, and `SystemHealthSignal` through `SignalBus<T>` snapshots in late-frame flow.
- `InteractionUI` is registered as `ILateFrameTickable` and consumes `InputStateSignal.CurrentInputSchemeHash` for keyboard/gamepad/SteamDeck/XR prompt style changes.
- Legacy runtime updatable ownership is actively unregistered for the scoped UI controllers when late-frame ownership is registered.
- TMP hot writes stay on `SetCharArray` through existing HUD/prompt char buffers.
- XR projection follow uses `CinematicMath.FastNlerp`; static scan shows no `Quaternion.Slerp` in touched UI.
- Low/Mx350 dirty fallback uses `(cadenceFrame & 3) == 0`, preserving 15Hz at 60fps while signal hits bypass the throttle.
- Added/used hash-only `ActiveUiUpdatesPerFrame` telemetry through `HphiReactiveUiTelemetry.RecordActiveUiUpdate()`.
- Documented H-Phi Update deletion evidence in `Docs/AgentLogs/HphiUiUpdateDeletion_UX_HPHI_SIGNAL_WIRING.md`.

## Cinematic Cheats Used
- Signal-bypassed 15Hz dirty gate for low-tier UI.
- Power-of-two bitmask cadence check instead of modulo.
- Rational `ApproximateOneMinusExpNeg` damping instead of real exponential.
- `CinematicMath.FastNlerp` XR lazy-follow instead of `Quaternion.Slerp`.
- TMP `SetCharArray` staging buffers instead of managed text assignment.

## Verification
- `rg` singleton scan over `Assets/_Project/Scripts/UI`: no `FindObjectOfType`, `FindObjectsOfType`, `FindAnyObjectByType`, `FindFirstObjectByType`, `Player.Instance`, or `Inventory.Instance`.
- `rg` Update scan over `Assets/_Project/Scripts/UI`: no direct `Update`, `LateUpdate`, or `FixedUpdate`.
- Scoped UI hot-path scan: no `string.Format`, LINQ operators/usings, interpolation `$"`, `SetText`, or `TMP_Text.text` in touched UI files.
- VR scan: `FastNlerp` present; `Quaternion.Slerp` absent in touched UI files.
- `git diff --check`: clean except line-ending warnings.
- `dotnet build Hecton8.Core.csproj --no-restore`: failed on pre-existing non-UX missing namespaces/contracts (`Environment.Fluids`, `Core.Scheduling`, `Audio.Virtualization`, `Physics.CCD`, `World.Terrain`, WFC/outpost interfaces, `MacroSwarm`, etc.); no touched UI file diagnostics in filtered output.

## Microseconds Saved
- Measured saved time: 0 us. Profiler proof is blocked by the upstream compile wall.
- Static estimate: sub-1 us per low-tier HUD dirty-evaluation tick from replacing `% 4` with `& 3`; larger expected savings come from moving two scoped UI owners off Update-lane and dirty-gating no-change text/icon refresh, but exact microseconds require a compiling Unity profiler run.

# UX_HPHI_SIGNAL_WIRING Upgrade Pass 1 - Visual Sync Sweep

Status: PENDING VERIFICATION (compile still blocked by upstream non-UX assembly errors)
Domain: ECHELON 8 - PRESENTATION & UX

## What Was Wrong
- Additional UI-owned visual controllers still registered through dispatcher Update via `ITickable` / `IUpdatable`.
- Acoustic radar, submarine sonar map, analog gauge, and PDA GPU map were split across Update sampling and LateFrame drawing.
- Submarine sonar map quality LOD read `GlobalRegistry.ScalabilityTier` every visual frame and could flip immediately.

## What Was Done
- `AnalogGaugeNeedle3D` is now `ILateFrameTickable` only; spring motion samples `SystemDispatcher.CurrentFrameDeltaTime`.
- `AcousticRadarSphereRenderer` is now `ILateFrameTickable` only; blip matrix sampling happens immediately before instanced draw.
- `SubmarineSonarHoloMapRenderer` is now `ILateFrameTickable` only; mesh sampling and draw run in VISUAL_SYNC.
- `PDAMapTab` no longer implements `ITickable` / `IUpdatable`; cadence refresh moved into private `RunVisualSync(float)`.
- Submarine sonar map added 0.5s quality-tier probe cadence plus 2s hysteresis before changing grid LOD.

## Cinematic Cheats Used
- Same-frame VISUAL_SYNC sampling/draw instead of Update-to-Late handoff.
- Quality-tier hysteresis to avoid visible LOD flicker.
- Cached dispatcher delta access instead of a Tick relay field.

## Verification
- Static scan over the four edited UI files: no `ITickable`, `IUpdatable`, `TryRegisterUpdatable`, public `Tick`, LINQ, `string.Format`, interpolation `$"`, `SetText`, `.text =`, or `Quaternion.Slerp`.
- `git diff --check`: clean except CRLF normalization warnings.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /p:BuildInParallel=false`: failed on existing non-UX missing references (`IGroundRadarService`, `IWorldResourceSpawnerReadModel`, `IOutpostGenerationService`, `JobAdmissionTelemetryBridge`, `H8BinaryWorldPager`, `FaunaKinematicsRuntime`, `TerrainChunkGeneratedSignal`). Filtered output showed no diagnostics for the four edited UI files.

## Microseconds Saved
- Measured saved time: 0 us. Unity profiler proof unavailable until compile wall is repaired.
- Static estimate: four dispatcher Update slots removed; submarine quality-tier reads reduced from about 60/s to 2/s while visible; exact i3/MX350 frame-time delta remains PENDING VERIFICATION.

# UX_HPHI_SIGNAL_WIRING Upgrade Pass 2 - Micro Visual Controllers

Status: STATIC VERIFIED (compile proof blocked by timeout/upstream assembly wall)
Domain: ECHELON 8 - PRESENTATION & UX

## What Was Wrong
- Eight additional UI-owned micro controllers still implemented `ITickable` / `IUpdatable` and registered presentation work into dispatcher Update.
- Several effects could stay registered while idle instead of owning scheduler time only while active.
- `UIScreenShake.Shake(float)` lost custom intensity before the active shake consumed it.
- PDA archaeology scramble quality read the scalability tier directly in the visible animation path.

## What Was Done
- `UIScreenShake`, `UIFadeTransition`, `SettingsPanelAnimator`, `LocalizedTextMadnessFx`, `ShaderCompassRibbon`, `AudioWaveformAnimator`, `DiegeticPdaFocusDistanceController`, and `PDADataArchaeologyDecryptLabel` now use `ILateFrameTickable`.
- Active visual work samples `SystemDispatcher.CurrentFrameDeltaTime` in VISUAL_SYNC and unregisters when idle, inactive, disabled, or complete.
- `UIScreenShake` now carries caller intensity through `_activeShakeIntensity`.
- `DiegeticPdaFocusDistanceController` registers only while focus is active, so its non-alloc raycast owner is not parked while disabled.
- `PDADataArchaeologyDecryptLabel` keeps pooled char-buffer rendering and adds 0.5s quality-tier probes with 2s hysteresis.
- Updated `Docs/AgentLogs/HphiUiUpdateDeletion_UX_HPHI_SIGNAL_WIRING.md`: scoped dispatcher Update-lane UI owners purged/neutralized now totals 14.

## Cinematic Cheats Used
- Active-only visual ownership instead of always-registered Update owners.
- Same-phase VISUAL_SYNC sampling for UI animation.
- Cheap deterministic screen-shake noise envelope.
- Quality-tier hysteresis and fixed 2 Hz quality probes.
- TMP `SetCharArray`/pooled buffer path preserved for decrypt label text.

## Verification
- Static scan over the eight edited files: no `ITickable`, `IUpdatable`, `TryRegisterUpdatable`, public `Tick`, LINQ, `string.Format`, interpolation `$"`, `SetText`, `.text =`, `Quaternion.Slerp`, or `math.slerp`.
- `git diff --check`: clean except CRLF normalization warnings.
- `dotnet build Hecton8.Core.csproj --no-restore --nologo -v:minimal -m:1 /p:BuildInParallel=false`: timed out after 184 seconds before diagnostics. Prior compile filters already show a non-UX assembly/domain wall; no compile success is claimed.

## Microseconds Saved
- Measured saved time: 0 us. Unity profiler proof remains blocked.
- Static estimate: eight additional dispatcher Update slots removed this pass; fourteen total scoped UI Update-lane owners purged/neutralized across all passes; PDA scramble quality reads reduced from about 60/s to 2/s while visible; idle shake/fade/panel/focus owners unregister instead of occupying scheduler slots.

# UX_HPHI_SIGNAL_WIRING Upgrade Pass 3 - Localization And Terminal Visual Owners

Status: FILTERED COMPILE VERIFIED (project compile still blocked by non-UX errors)
Domain: ECHELON 8 - PRESENTATION & UX

## What Was Wrong
- Ten more clean UI owners still used dispatcher Update for visual-only or event-driven presentation work.
- Localization auto-size and RTL mirror used `GlobalRegistry.Updatables.Contains` as registration proof.
- Tooltip, settings preview, terminal typing, SDF sharpness, and dev dashboards were not aligned with VISUAL_SYNC ownership.

## What Was Done
- Converted `WorldSpaceTMPSharpnessController`, `LocalizedTMPAutoSizer`, `LocalizedLayoutMirror`, `SettingsLivePreview`, `SettingsComparisonView`, `UITooltip`, `BIOSMessageStreamer`, `HectonSubmarineOsDisplay`, `BlackBoxMetricDashboard`, and `EngineHealthOverlay` to `ILateFrameTickable`.
- Replaced Update registration/unregistration with `GlobalRegistry.TryRegisterLateFrameTickable` and `GlobalRegistry.UnregisterLateFrameTickable`.
- Preserved existing active-only/dirty-only behavior for tooltip, settings live preview, terminal typing, and one-shot localization/layout passes.
- Kept all TMP render paths on existing `SetCharArray` / char-buffer flows.
- Updated H-Phi deletion evidence: scoped dispatcher Update-lane UI owners purged/neutralized now totals 24.

## Cinematic Cheats Used
- Active-only VISUAL_SYNC ownership.
- One-shot layout and autosize deferral.
- Fixed-cadence terminal character reveal.
- Cached dispatcher delta instead of Tick delta relay.
- Existing preallocated TMP buffers for settings, tooltip, BIOS, and submarine OS text.

## Verification
- Static scan over the ten edited files: no `ITickable`, `IUpdatable`, `TryRegisterUpdatable`, public `Tick`, LINQ, `string.Format`, interpolation `$"`, `SetText`, `.text =`, `Quaternion.Slerp`, or `math.slerp`.
- `git diff --check`: clean except CRLF normalization warnings.
- `dotnet build Hecton8.Core.csproj --no-restore --disable-build-servers --nologo -v:minimal -m:1 /nr:false /p:BuildInParallel=false /p:UseSharedCompilation=false`: failed only on existing non-UX errors in `FaunaKinematicsRuntime.cs(242,17)` and `PlayerCriticalProceduralAudioRenderer.cs(10002,31)`. No diagnostics referenced the ten edited UI files.

## Microseconds Saved
- Measured saved time: 0 us. Profiler proof remains unavailable until the upstream compile wall is fixed.
- Static estimate: ten additional dispatcher Update slots removed this pass; twenty-four total scoped UI Update-lane owners purged/neutralized across all passes; localization/layout one-shot owners no longer probe Update buckets; active tooltip/terminal/settings owners now drain in VISUAL_SYNC only while needed.
