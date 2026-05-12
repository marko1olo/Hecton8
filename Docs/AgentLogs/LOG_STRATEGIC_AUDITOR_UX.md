# LOG_STRATEGIC_AUDITOR_UX

## 2026-05-12 - Strategic UX Audit

STATUS: STRATEGICALLY VERIFIED

Verification boundary: static source audit only. User explicitly forbade build/dotnet build. No Unity runtime, headset, Steam Deck, XR Frame Debugger, or profiler capture was run.

What was wrong:
- Input architecture has a good Core bridge, but interaction/UI code still branches on XR runtime state directly.
- `DiegeticHudManualLayout` is fixed-offset lane math and cannot prove dynamic aspect/FOV readability across Deck and VR by itself.
- Visor HUD projection updates in late-frame, but there is no true before-render/late-latched HUD reprojection path.
- Babel/font streaming is zero-GC oriented, but contextual readability does not include Deck pixel height, VR angular glyph size, foveal/peripheral zone, or user readability preference.
- `DodReplayRecorder` records raw input hardware events instead of a normalized deterministic input tick.

What was done:
- Audited `Assets/_Project/Scripts/Core`, `Interaction`, `UI`, `Gameplay`, and tool paths for XR/device/input coupling.
- Verified `XRDevice.isPresent` was not found.
- Verified `LaserCutter` reads `GlobalRegistry.Input`/`PlayerInputState` action state rather than direct XR/mouse hardware.
- Verified `HectonSubmarineOS` did not show direct input/XR hardware dependency in the targeted scan.
- Flagged direct XR runtime coupling in `PhysicalHandController`, `PhysicalInteractionHandler`, `PhysicalToolGripOffsets`, and `DiegeticPanelController`.
- Audited `DiegeticHudManualLayout`, `DiegeticVisorHudMesh`, `HectonUIScaler`, and `SuitHUDV4CanvasOverlay` for Deck/VR scaling.
- Audited `HectonXRRuntimeState`, `SystemDispatcher`, and late-frame HUD paths for somatic latency risk.
- Audited `LocalizedTMPAutoSizer`, `LocOverflowHandler`, `FontStreamingManager`, `LabelSwapScheduler`, `TMP_TextRegistry`, and `WorldSpaceTMPSharpnessController` for Babel readability.
- Audited `DodReplayRecorder` for input sampling determinism.
- Wrote `Docs/AgentLogs/STRATEGIC_UX_REPORT.md`.
- Updated `Docs/Tasks/Status_STRATEGIC_AUDITOR_UX.md`.
- Updated `Docs/AgentLogs/Rationale_STRATEGIC_UX.md`.

Cinematic Cheats used or mandated:
- Keep HUD semantic data at a fixed cadence and late-reproject pose only.
- Use shader UV/parallax correction for curved visor timewarp fake.
- Restrict VR visor cost with stencil/foveated masks.
- Use pooled SDF readability buckets instead of per-label material churn.
- Normalize input into 120 Hz replay ticks while retaining raw events as diagnostics.

Exact microseconds saved, static estimates pending profiler proof:
- Input bridge policing: 5-20 us per active tool/UI frame versus repeated hardware-mode checks outside Core.
- Deterministic HUD metrics layout: 22-60 us per layout refresh versus generic dynamic layout or manual corrective rebuilds.
- VR HUD pose-only reprojection cheat: 300-800 us equivalent frame budget preserved during sim misses versus full semantic HUD rerender/rebuild.
- Babel SDF bucket policy: 10-40 us per 100 labels versus unbounded per-label material writes.
- 120 Hz normalized input replay: 50-300 us spike avoidance during high-rate raw input bursts.
- Replay ring pressure proof: 512 raw events last 0.512 s at 1000 Hz mouse input and 8.53 s at 60 Hz VR input. A 2048 normalized tick ring at 120 Hz lasts 17.07 s independent of hardware event rate.

Build status:
- Not run. User explicitly requested no build and no dotnet build.
