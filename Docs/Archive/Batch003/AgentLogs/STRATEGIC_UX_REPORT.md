# STRATEGIC UX REPORT

Agent: STRATEGIC_AUDITOR_UX  
Domain: Echelon 8 Presentation & UX / Somatic Experience  
STATUS: STRATEGICALLY VERIFIED  
Date: 2026-05-12  

Verification boundary: static source audit only. The user explicitly forbade build/dotnet build. No Unity Editor run, headset run, Steam Deck run, profiler capture, or XR Frame Debugger proof was performed. Runtime timings are architectural estimates until captured on target hardware.

## Executive Verdict

The project has a strong central input foundation: `PlayerInputState`, `IInputService`, `GlobalRegistry.Input`, `InputDispatcher`, and Steam Deck PAL fields already exist. `XRDevice.isPresent` was not found. The named `LaserCutter` paths read action state (`GlobalRegistry.Input`/`PlayerInputState`) instead of device APIs, and `HectonSubmarineOS` was not found using direct hardware input in the targeted scan.

The failure risk is not the tool layer. The risk is that interaction/UI presentation code still branches on XR runtime state directly, HUD layout is fixed-lane authoring rather than dynamic aspect/FOV layout, VR HUD pose is late-frame but not before-render/late-latched, Babel readability has locale handling but no physical-device readability context, and replay records hardware event cadence instead of a normalized deterministic input tick.

## Mandates Applied

- `CTRL_Device_Abstraction_Haptics.txt`
- `UI_Diegetic_Physical_Interfaces.txt`
- `UI_Localization_Babel_RTL_FontSwap_ZeroAlloc.txt`
- `UI_Data_Streaming_ZeroGC_Optimization.txt`
- `REND_VR_Stencil_Masking.txt`
- `REND_Foveated_Simulation_LOD.txt`
- `PHYS_Kinematic_Interaction_Hands.txt`
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`

## 1. Input Abstraction / VR Trap

Finding: `XRDevice.isPresent` was not found. Core XR ownership is centralized in `HectonXRRuntimeState` and `InputDispatcher`, which is the correct boundary. `HectonXRRuntimeState.RefreshFrameState` uses `XRSettings.enabled && XRSettings.isDeviceActive` (`Assets/_Project/Scripts/Core/HectonXRRuntimeState.cs:95-97`). `InputDispatcher` owns XR controller sampling and exposes `PlayerInputState` (`Assets/_Project/Scripts/Core/InputDispatcher.cs:330-355`, `863-907`, `1001-1141`).

Clean paths:
- `LaserCutter` reads `GlobalRegistry.Input`, `PlayerInputState`, `PlayerInputAction.SecondaryFire`, and `MoveDelta` (`Assets/_Project/Scripts/LaserCutter.cs:847-851`, `943-947`, `1484-1488`).
- `HectonSubmarineOS` is UI/systems runtime and did not show direct XR/input polling in the targeted scan (`Assets/_Project/Scripts/Gameplay/HectonSubmarineOS.cs:547`).

Silo violations:
- `PhysicalHandController` reads `HectonXRRuntimeState.IsXRActive` and `TryGetXRInputState` directly (`Assets/_Project/Scripts/Interaction/PhysicalHandController.cs:638`, `649`, `701`, `711`, `1580`, `1821`).
- `PhysicalInteractionHandler` gates physical panels on `HectonXRRuntimeState.IsXRActive` (`Assets/_Project/Scripts/Interaction/PhysicalInteractionHandler.cs:250`, `259`, `477`, `1131`, `1134`).
- `PhysicalToolGripOffsets` allocates/branches by XR state (`Assets/_Project/Scripts/Interaction/PhysicalToolGripOffsets.cs:42`, `48`).
- `DiegeticPanelController` disables desktop fallback by XR global state (`Assets/_Project/Scripts/UI/DiegeticPanelController.cs:1786`).

Required architecture: tools and UI must depend on action/capability structs, not hardware. Keep XR API crossing in Core/PAL only. Add a bridge contract such as `IInputPoseProvider` or extend `IInputService` with a zero-GC capability snapshot:

- `InputDeviceClass`: MouseKeyboard, Gamepad, SteamDeck, XRHands, XRControllers.
- `PointerMode`: Ray, Cursor, Touch, HandPose.
- `HandPoseState[2]`: pose, grip/pinch/action bits, tracking validity, haptic lane id.
- `ActionBitmask`: semantic actions only.

Downstream code asks "can provide physical hand pose" or "has ray pointer" instead of "is XR active".

Scalability:
- Low: one action snapshot per frame, no per-tool device checks.
- Middle: action snapshot plus Deck gyro/trackpad flags.
- High: XR hand/controller pose snapshots behind Core bridge.
- Ultra: richer hand skeleton/haptics behind capability bits, same downstream API.

## 2. UI Fragmentation / Curved Visor Scalability

Finding: the visor projection path has partial dynamic scaling, but `DiegeticHudManualLayout` cannot solve dynamic aspect ratio by itself.

Evidence:
- `DiegeticHudManualLayout` serializes `startOffset`, `itemExtent`, `spacing`, `crossOffset`, and `depthOffset` (`Assets/_Project/Scripts/UI/DiegeticHudManualLayout.cs:21-32`).
- `RebuildLayout` writes fixed local positions and completes the job immediately (`Assets/_Project/Scripts/UI/DiegeticHudManualLayout.cs:64-95`).
- The job math is lane offset only, not a viewport/aspect/FOV solver (`Assets/_Project/Scripts/UI/DiegeticHudManualLayout.cs:195-205`).
- `DiegeticVisorHudMesh` uses authored `horizontalDegrees`, `verticalDegrees`, distance, and curvature (`Assets/_Project/Scripts/UI/DiegeticVisorHudMesh.cs:38-39`, `318-319`, `626-631`).
- `SuitHUDV4CanvasOverlay.ResolveProjectionCanvasWorldScale` scales projection canvas from camera FOV/aspect/reference resolution (`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:1942`, `2043`, `2057`).
- `HectonUIScaler` keeps world-space canvas scale stable rather than repacking world-space layout, so it does not solve Deck/VR readability/reflow alone.

Deck failure mode: 800p constrains pixel density; fixed lane spacing and NASA-punk compact labels will collide or shrink below readable size.

VR failure mode: 110-degree FOV has a comfort/readability cone. A label that is acceptable on a flat display can be too peripheral or too small in angular terms. Fixed offsets also fail when IPD, headset FOV, and camera distance change.

Required architecture: replace manual platform offsets with a deterministic `HudViewportMetrics` layout pass:

- Inputs: physical resolution, safe area, aspect, camera vertical/horizontal FOV, reference pixels per meter, comfort cone degrees, UI scale tier, language text expansion bucket.
- Outputs: fixed-array slot rectangles/poses, text scale tier, collapse priority, projection band.
- Cadence: rebuild on metrics/language/layout hash changes only, not every frame.
- Rejected: Unity layout groups on hot path and per-platform manual bone-offset authoring.

## 3. Latency & Somatics / VR Visual Cheat

Finding: existing code has late-frame tick lanes and HUD projection pose update, but no true before-render late latching/timewarp fake for visor HUD.

Evidence:
- `SystemDispatcher` refreshes XR state and resolves delta from XR state (`Assets/_Project/Scripts/Core/SystemDispatcher.cs:912-917`).
- `HectonXRRuntimeState` samples XR active state, refresh rate, eye poses, and shader globals (`Assets/_Project/Scripts/Core/HectonXRRuntimeState.cs:95-121`, `288-340`).
- `SuitHUDV4CanvasOverlay.LateFrameTick` updates projection pose late (`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:1447`).
- Source search found no `Application.onBeforeRender`/`beforeRender` hook for HUD reprojection.

VR failure mode: a 16.6 ms simulation frame can be acceptable on i3 flat display but can miss the VR comfort target. If HUD pose and semantic data wait for the same simulation frame, the visor appears stuck to the old head pose.

Required visual cheat:
- Keep semantic HUD data on fixed low cadence: 30/60 Hz by tier.
- Render HUD to RT or mesh state from last stable semantic tick.
- In before-render or latest possible XR presentation hook, update only visor transform and a shader reprojection offset from newest head pose.
- Store previous/current head view-projection matrix; shader applies small UV/parallax correction on curved HUD.
- If sim misses budget, freeze semantic values for one frame but keep pose-locked display fresh.
- Use stencil/foveated HUD masks so the fake spends pixels only where the visor exists.

Rejected: making simulation faster by pushing full HUD rebuilds into VR path. The correct cheat is pose-only presentation correction.

## 4. Text Readability / UI_LOCALIZATION_BABEL

Finding: Babel/font streaming has good zero-GC structure, but contextual readability is incomplete.

Evidence:
- `LabelSwapScheduler` caps swaps at 18 per tick and restores text with `SetCharArray` (`Assets/_Project/Scripts/UI/LabelSwapScheduler.cs:12`, `66-99`).
- `TMP_TextRegistry` uses fixed backing arrays and a hierarchy-hash map (`Assets/_Project/Scripts/UI/TMP_TextRegistry.cs:33-39`, `126`).
- `LocalizedTMPAutoSizer` supports locale font size multipliers, TMP auto-size, overflow behavior, and `LocOverflowHandler` (`Assets/_Project/Scripts/UI/LocalizedTMPAutoSizer.cs:206-217`, `316`).
- `WorldSpaceTMPSharpnessController` adjusts `_FaceDilate` and `_OutlineSoftness` by camera distance (`Assets/_Project/Scripts/UI/WorldSpaceTMPSharpnessController.cs:13-16`).

Gap: no evidence of device DPI, physical pixel height, VR angular glyph height, Steam Deck 800p minimums, foveal/peripheral readability tiers, or user readability preference being part of the font scale decision.

Required architecture:
- Add `HudTextReadabilityContext`: display class, physical resolution, render scale, world distance, projected pixel height, angular glyph height, language expansion bucket, readability preference, and foveal/peripheral zone.
- Clamp minimum readable body text:
  - Deck/800p: body text should not drop below a practical 18-22 px equivalent; critical labels 28-32 px equivalent.
  - VR: use angular glyph height, not canvas font size; minimum practical threshold should be tiered around 0.35-0.5 degrees for body text depending on headset.
- Use pooled SDF readability buckets instead of per-label unique material instances: Low, Standard, Far, Critical, Peripheral.
- Update readability at low cadence with hysteresis, not every frame.

Rejected: per-frame TMP preferred-size scans and unrestricted material instancing. Those create CPU/GPU churn and defeat zero-GC intent.

## 5. Input Determinism / Replay Sampling

Finding: `DodReplayRecorder` captures raw input events and frame/timestamp metadata. It does not normalize input into a deterministic action tick.

Evidence:
- `DodReplayInputEvent` stores `PrecisionTimestamp`, `FrameIndex`, `Sequence`, `DeviceHash`, `ControlHash`, `PhaseHash`, and values (`Assets/_Project/Scripts/Core/DodReplayRecorder.cs:98-112`).
- Input journal capacity is 512 (`Assets/_Project/Scripts/Core/DodReplayRecorder.cs:312`).
- Recorder hooks `InputSystem.onEvent` (`Assets/_Project/Scripts/Core/DodReplayRecorder.cs:937`).
- Raw event recording writes `FrameIndex = Time.frameCount` and precision timestamp (`Assets/_Project/Scripts/Core/DodReplayRecorder.cs:974-991`).
- Snapshots run every 10 frames (`Assets/_Project/Scripts/Core/DodReplayRecorder.cs:308`, `783-790`).

Failure mode: a 1000 Hz mouse can consume the 512-event journal in about 0.512 seconds. A 60 Hz VR controller consumes the same ring in about 8.53 seconds. That means replay fidelity and failure history depend on hardware sampling rate, not game intent.

Required architecture: authoritative replay must record a 120 Hz normalized input tick:

- `InputTickIndex`: monotonically increasing 120 Hz tick.
- `ActionBitmask`: normalized action state.
- `Move`, `Look`, `Scroll`, `Trigger01`, `Grip01`.
- `PointerRay` and `HandPoseState[2]` with tracking-valid flags.
- `DeviceClassMask` and `PlatformFlags`.
- `HardwareSequenceStart/End`: diagnostic range into optional raw event journal.

1000 Hz mouse: accumulate deltas into the next 8.333 ms input tick, clamp/saturate by policy, write one normalized tick.  
60 Hz VR controller: hold/interpolate pose for presentation only; gameplay/replay reads the same 120 Hz normalized tick stream.  
Raw hardware events stay as a diagnostic sidecar, not the authoritative replay source.

Recommended ring: minimum 2048 normalized input ticks. At 120 Hz this preserves 17.07 seconds of input history. A 512-tick ring would preserve only 4.26 seconds.

## Failure Matrix

- Steam Deck: fixed HUD slots and non-contextual font scale can make PDA/visor text unreadable at 800p. Deck trackpad/mouse-like deltas also need normalized tick accumulation.
- VR: direct XR state leaks create mode-specific branches outside Core; missing before-render HUD reprojection risks stale head-locked UI; fixed layout ignores comfort cone and angular glyph size.
- Low i3/MX350: hot-path UI rebuilds/material churn are suspicious above 0.1 ms. Keep layout/hash/font work event-driven and bucketed.
- High/Ultra: same bridge should support richer hands, foveated visor layers, and visual overkill without changing tools or replay format.

## Cinematic Cheats Required

- Visor HUD semantic freeze + pose-only before-render reprojection.
- Curved HUD shader parallax/timewarp offset from latest head pose.
- Foveated/stencil HUD masks instead of full-screen overlay cost.
- SDF readability buckets instead of per-label dynamic material edits.
- 120 Hz normalized replay ticks with raw event diagnostics only.

## Strategic Status

STATUS: STRATEGICALLY VERIFIED

No code was changed. No build was run. The report identifies current failure points and the minimum architecture changes required before Steam Deck/VR claims are credible.
