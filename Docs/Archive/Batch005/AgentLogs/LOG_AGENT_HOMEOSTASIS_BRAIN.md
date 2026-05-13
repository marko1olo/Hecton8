# LOG_AGENT_HOMEOSTASIS_BRAIN

## 2026-05-13 22:40 VERIFIED SYSTEM BRAIN
What was wrong:
- No existing persistent status/rationale files for `AGENT_HOMEOSTASIS_BRAIN`.
- `Docs/Tasks/CURRENT_BATCH.md` did not contain the agent prompt; direct user XML was used and all failed extractions were recorded.
- Hardware pressure had no unified SOA lane feeding `SystemDispatcher.KillSwitchMask`.
- Initial HUD integration was signal-only; Task 12 required an actual visor warning.

What was done:
- Added `Assets/_Project/Scripts/Core/HomeostasisBrain.cs`.
- Added persistent `NativeArray<float> GlobalHardwareMetrics` slots: FPS EWMA, jitter sigma, CPU temp/proxy, GPU util/proxy, battery life.
- Added cached Android thermal bridge under `UNITY_ANDROID && !UNITY_EDITOR` with `PowerManager.getThermalHeadroom` and thermal status fallback.
- Added Windows/editor fallback using `UnityEngine.Device.Application.targetFrameRate` and `SystemInfo` hardware proxies.
- Added Burst SHI function pointer using jitter 40%, temp 40%, battery 20%, with doubled battery pressure on Low/Mx350.
- Added 120-frame jitter sigma and unstable flag above 2.0 ms.
- Added `SystemBit : ulong`, `KillSwitchSignal`, and `SystemHealthSignal`.
- Integrated homeostasis mask, pressure level, foveated tier, slow tick 2 Hz, and emergency time dilation into `SystemDispatcher`.
- Integrated foveated Tier 3 pressure into `FoveatedSimulationManager`.
- Added Level 1, Level 2, Level 3 sacrifice masks with hysteresis restore bands.
- Added sequential restoration after SHI < 0.30 for 300 frames, one bit restored every 60 frames.
- Added 300-frame `HomeostasisBlackBoxEntry` native ring and NaN dump to `Docs/AgentLogs/Dump_AGENT_HOMEOSTASIS_BRAIN.bin`.
- Added HUD consumption in `SuitHUDV4CanvasOverlay`; it renders flickering `OPTIMIZING CORE SYSTEMS` through the existing status char-array path when pressure level is above 1.
- Fixed two compile blockers encountered during integration: dirty `HectonUnderwaterVisuals.cs` brace/duplicate hot-swap state and missing `Hecton8.UI.Diegetic.Contracts` asmdef reference in `Hecton8.Core.asmdef`.

Cinematic cheats used:
- Presentation is sacrificed before simulation: caustics, particle advection, high-res fog, SSR.
- Foveated pressure Tier 3 reduces far simulation quality before touching near-field state.
- Emergency time dilation buys CPU time while remaining diegetic instead of pretending the device can sustain full load.
- HUD warning uses existing status text flicker instead of a new UI hierarchy.

Exact microseconds saved:
- Homeostasis monitor normal path estimated at 8-20 us.
- JNI thermal sampling amortized to roughly 12 us at 30-frame cadence instead of per-frame Java lookup spikes.
- SHI scalar math under 1 us.
- Blackbox ring write under 1 us.
- HUD warning consume/write path under 2 us per HUD tick.
- Level 1 sacrifice expected gain: 200-600 us on i3/MX350 class GPU.
- Level 2 sacrifice expected gain: 600-1500 us from far simulation, SSR, IK, sway, and foveated compression.
- Level 3 emergency expected gain: multiple milliseconds by cutting non-critical VFX/boid cost, slow tick to 2 Hz, and time dilation to 0.8.

Verification:
- Static Unity validation passed earlier for `HomeostasisBrain.cs`, `SystemDispatcher.cs`, and `FoveatedSimulationManager.cs`.
- `Unity refresh` timed out waiting for editor readiness, but `Library/ScriptAssemblies/Hecton8.Core.dll` regenerated at 2026-05-13 22:35:25.
- Editor log scan found no current C# errors for `HomeostasisBrain`, `SuitHUDV4CanvasOverlay`, or touched assembly files.
- `dotnet build Hecton8.Core.csproj` is not authoritative here; it fails on pre-existing generated-project missing assembly references outside this agent scope.
- `git diff --check` reported line-ending warnings only.

## 2026-05-13 22:55 CONTINUED POLISH PASS
What was wrong:
- `HomeostasisBrain` created its own fallback readings before checking the cached `HardwareThermalService` snapshot.
- Fallback battery sampling still touched `SystemInfo` every frame.
- Blackbox flags could carry stale pressure bits for one frame after recovery because flags were built before pressure resolution and then passed by value.
- Existing systems already consume `SystemHealthIndexSignal`; the new typed homeostasis signal did not feed that legacy lane.

What was done:
- Added `GlobalRegistry.HardwareThermal.TryGetSnapshot` as the first hardware source.
- Mapped cached thermal severity, battery percent, and temperature into `GlobalHardwareMetrics`.
- Added a `HardwareThermalSnapshot` telemetry flag so service-backed metrics are not mislabeled as Windows fallback.
- Cached fallback low-hardware bias at initialization instead of reading memory/VRAM size every frame.
- Throttled fallback battery polling to once per 60 frames.
- Changed `ApplyPressurePolicy` to return final flags so `HomeostasisBlackBoxEntry.Flags` reflects the post-policy state.
- Added legacy `SystemHealthIndexSignal` publish on the same cadence as the homeostasis telemetry packet.

Cinematic Cheats used:
- Cached thermal severity becomes the primary load-shed source; direct device probing is a fallback only.
- Health pressure is broadcast to streaming systems so far-world residency can react before visible near-field features are touched.

Exact Microseconds saved:
- Cached hardware bias removes repeated memory/VRAM `SystemInfo` calls from the hot path; estimated 1-3 us/frame on low-end desktop.
- Battery polling drops from every frame to once per 60 frames; estimated 1-5 us/frame avoided on portable platforms.
- Flag fix has no measurable cost; it improves blackbox forensic accuracy.

Verification:
- `dotnet build Hecton8.Core.csproj` still fails on pre-existing missing assembly references, but filtered output showed no errors from `HomeostasisBrain`, `SuitHUDV4CanvasOverlay`, `SystemDispatcher`, or `FoveatedSimulationManager`.
- Unity refresh timed out again waiting for editor readiness; editor log scan had no current C# errors for touched files.
- `git diff --check` reported line-ending warnings only.
