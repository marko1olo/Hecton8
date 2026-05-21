# AGENT_05_UI_AUDIO_LOG

Date: 2026-04-26
Status: PENDING VERIFICATION

Mandates followed:
- `UI_Data_Streaming_ZeroGC_Optimization`
- `UI_Localization_Babel_RTL_FontSwap_ZeroAlloc`
- `AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC`
- `AUD_Acoustic_Sonar_Occlusion_Sensory_Simulation`
- `OPT_Zero_GC_Policy_AllocFree_Mandate`

## UI Reentrancy Fix

Problem:
- `SuitHUDV4CanvasOverlay` and the nested `HectonUIScaler` were marked `[ExecuteAlways]`.
- Editor-time enable/validate paths could call `RefreshAll()`, `EnsureHierarchy()`, `SetParent()`, and `sizeDelta` writes indirectly.
- That edit-time rebuild path was enough to trigger `OnRectTransformDimensionsChange` reentrancy while Unity was still in restricted callbacks, matching the reported `SendMessage cannot be called during Awake... or OnValidate` failure mode.
- `PlayerPDA.OnValidate()` was also mutating the hierarchy indirectly by calling `AutoResolveTabs()`, which reached `EnsureRuntimeTab()` and could execute:
  - `existing.gameObject.AddComponent(tabComponentType)`
  - `rect.SetParent(root, false)`
  - `rect.offsetMin / rect.offsetMax`
  - `tab.SetActive(false)`

What changed:
- Removed `[ExecuteAlways]` from both UI owners.
- `SuitHUDV4CanvasOverlay.OnEnable()` now rebuilds only during play mode.
- `SuitHUDV4CanvasOverlay.OnValidate()` now invalidates caches only. No hierarchy rebuild. No editor tick registration.
- Deleted automatic editor tick/polling registration from the overlay refresh path.
- Added explicit `[ContextMenu("Rebuild UI")]` entry points to both `SuitHUDV4CanvasOverlay` and `HectonUIScaler`.
- `HectonUIScaler.OnEnable()` now auto-creates/scales content only during play mode.
- `HectonUIScaler.OnValidate()` only sanitizes serialized values. No `ApplyScale()`.
- `PlayerPDA.OnValidate()` now calls `ResolveEditorReferences()` only.
- `ResolveEditorReferences()` only caches existing tab references and the existing `CanvasGroup`; it does not call `AddComponent`, `SetParent`, `sizeDelta`, or `SetActive`.
- Added explicit `[ContextMenu("Rebuild PDA")]` to `PlayerPDA`. Manual rebuild now routes through the old tab-generation path instead of `OnValidate`.
- Iteration 17 sweep removed two more editor-time UI generators:
  - `SuitHUDScreenCompositor` no longer runs `[ExecuteAlways]` edit-time `RefreshCompositor()` or editor tick registration.
  - `SuitHUDPresentationController` no longer runs `[ExecuteAlways]` edit-time `ApplyPresentation()` or editor tick registration.
- Both now expose explicit authoring entry points only:
  - `[ContextMenu("Rebuild Screen Compositor")]`
  - `[ContextMenu("Rebuild HUD Presentation")]`
- `PlayerPDA.Awake()` / `Start()` now resolve existing tabs only. Missing tabs are created only by explicit `Rebuild PDA`.
- Removed `[ExecuteAlways]` from `HectonCelestialEngine` and `HectonUnderwaterVisuals` because those owners still performed editor-preview `AddComponent()` / `SetActive()` hierarchy mutation.

Sweep evidence:
- Repo-wide `OnValidate` scan for `SetParent(`, `sizeDelta`, `AddComponent`, `SetActive(`, `offsetMin =`, and `offsetMax =` returned no matches after this pass.
- Remaining first-party `[ExecuteAlways]` owners no longer contain those hierarchy-mutation calls.

Result:
- No automatic hierarchy mutation from `OnValidate`.
- No automatic `SetParent()` or `sizeDelta` writes from edit-time polling.
- No PDA tab auto-generation during `OnValidate`.
- Editor rebuild is now operator-driven and explicit.

## Lore Hash Synchronization

Problem:
- Global `LocHash.Compute(...)` is UTF-16 byte-pair FNV-1a.
- Lore record IDs are authored as one-byte ASCII FNV-1a identifiers.
- Mixing those two domains is a guaranteed mismatch source.

What changed:
- Added `LocHash.ComputeAscii(string/ReadOnlySpan<char>)` for byte-wise ASCII FNV-1a.
- `LoreDatabaseManager.ComputeLoreHash(...)` now delegates to `LocHash.ComputeAscii(...)`.
- Expanded the lore rebake tool so `Hecton-8/Rebake Lore Hashes` scans all first-party C# source files under `Assets/_Project/Scripts` and rewrites every authored `new LoreSeed("id", 0x..., ...)` hash it finds.

Reference:
- `industrial_shift_board_a`
  - ASCII FNV-1a: `0xEB76D1D6`
  - UTF-16 LocHash path: `0x8CBEA156`

## Input Mapping Spam Removal

Problem:
- Binding-label generation was still calling Input System display-string APIs.
- Those APIs can hit OS virtual-key translation and emit `GetVirtualKey: Could not map char: z (122)` spam on unstable keyboard-layout paths.
- Main-menu startup was still resolving Unity's package `DefaultInputActions.inputactions` through `InputSystemUIInputModule`.
- Editor startup still flooded `GetVirtualKey` during `Initializing Unity extensions`. The repeated letters (`z y x c v ...`) match Unity shortcut-table bootstrap, not gameplay polling.
- The concrete first-party offenders were:
  - `MainMenuController.EnsureInputSystemEventRouting()` calling `inputSystemModule.AssignDefaultActions()`.
  - `PauseMenuController.EnsureEventSystem()` calling `inputSystemModule.AssignDefaultActions()`.
  - `Assets/_Project/Scenes/01_MAIN_MENU.unity` serializing GUID `ca9f5fa95ffab41fb9a615ab714db018` into `m_ActionsAsset` and every module action reference.

What changed:
- Replaced the binding-label fallback path in `InputManager.TryGetBindingDisplayStringSafe(...)`.
- Labels are now derived from the binding path directly by first-party parsing.
- Removed runtime dependence on `action.GetBindingDisplayString(...)` and `InputControlPath.ToHumanReadableString(...)` for this code path.
- Added explicit aliases for the project's common keyboard, mouse, and gamepad controls.
- Added `InputManager.TryConfigureUiInputModule(...)` so `InputSystemUIInputModule` now binds to the project-owned runtime action asset instead of Unity's package defaults.
- Extended runtime UI action normalization to synthesize the missing module actions:
  - `UiModuleSubmit`
  - `UiModuleCancel`
  - `Point`
  - `Click`
  - `MiddleClick`
  - `RightClick`
  - `ScrollWheel`
- Removed the `AssignDefaultActions()` fallback from `MainMenuController` and `PauseMenuController`.
- Cleared serialized `DefaultInputActions` references from `Assets/_Project/Scenes/01_MAIN_MENU.unity`.
- Cleared the same serialized package-action refs from `_Recovery/0.unity` and `_Recovery/1.unity`.
- Repo search found no first-party `Input.inputString`, `Input.anyKey`, or `Input.anyKeyDown` startup pollers to neutralize.
- Removed project-owned editor shortcut bindings that were still feeding Unity's virtual-key mapper during extension initialization:
  - `Assets/AstarPathfindingProject/Core/Misc/NodeLink.cs`
  - `Assets/AstarPathfindingProject/Editor/AstarPathEditor.cs`
  - `Assets/Feel/MMTools/Editor/MMGizmos/MMGizmoEditor.cs`
  - `Assets/Feel/MMTools/Editor/MMMaintenance/MMGroupSelection.cs`
  - `Assets/Feel/MMTools/Editor/MMMaintenance/MMLockInspector.cs`
  - `Assets/Feel/MMTools/Editor/MMUtilities/MMScreenshotEditor.cs`
  - `Assets/RealtimeCSG/RealtimeCSG/Plugins/Editor/Scripts/Control/Helpers/OperationsUtility.cs`
  - `Assets/RealtimeCSG/RealtimeCSG/Plugins/Editor/Scripts/Control/Managers/UpdateLoop.cs`
  - `Assets/_Project/Editor/HectonDevToolsMenu.cs`
  - `Assets/_Project/Editor/HectonMeshCleaner.cs`
  - `Assets/_Project/Editor/HectonPhysicsSkinGenerator.cs`
- After this sweep, repo search shows no remaining project-owned `Shortcut(...)` attributes and no remaining `%`/`#` menu hotkey tokens in the patched files.
- Iteration 17 removed the last repo-owned `MenuItem(...)` labels that still contained Unity hotkey parser markers:
  - `Assets/Feel/MMFeedbacks/Editor/FeedbackListOutputter/FeedbackListOutputer.cs`
    - `Output MMF_Feedbacks list` -> `Output MMF Feedbacks list`
  - `Assets/_Project/Scripts/Editor/HectonFBXPostprocessor.cs`
    - `Assets/_Project/Art` -> `Assets Project Art`
  - `Assets/_Project/Scripts/Editor/VRAMVitalsAuditReport.cs`
    - `VRAM && Vitals Report` -> `VRAM and Vitals Report`
- Post-patch repo scan:
  - `Shortcut(...)` attributes: `0`
  - `MenuItem(...)` strings with `%`, `&`, or `_` hotkey markers: `0`

Project setting audit:
- `ProjectSettings/ProjectSettings.asset` currently reports `activeInputHandler: 1`.
- `InputManager.TryValidateRuntimeConfiguration(...)` still hard-fails if `ENABLE_LEGACY_INPUT_MANAGER` is compiled in.
- Repo search shows no remaining first-party `AssignDefaultActions()` call sites.
- Repo search shows no remaining first-party references to the package default action asset GUID `ca9f5fa95ffab41fb9a615ab714db018`.
- Legacy `Input.GetKey(...)` still exists in third-party tooling (`GPUInstancer`, `Graphy`, `Feel`, `Crest`, `Candice`). Those call sites were not the identified `GetVirtualKey` startup source.
- Current `Editor.log` still contains `104` `GetVirtualKey` lines from the last editor startup at `2026-04-26 00:49:19`. A fresh Unity restart after these menu-path changes is still required before claiming the spam is gone.

## Runtime Acoustic Graph / Physics Coupling

Problem:
- The managed fallback needed to stay explicitly lock-free and alloc-free.
- `AcousticZoneController` only toggled binary listener presets:
  - underwater = low-pass on, fixed cutoff, no reverb graph
  - interior = low-pass on, canned reverb preset
- Physics impacts were reaching passive radar and the procedural helmet renderer, but not the listener acoustic state.

What changed:
- `AudioFrameSpscRingBuffer` now relies on `Volatile` index ownership only. Explicit `Thread.MemoryBarrier()` calls were removed from producer/consumer index publication.
- `PlayerCriticalProceduralAudioRenderer` now subscribes to `PhysicsEvents.OnImpact`.
- Nearby or player-owned impacts inject a short-lived hull-stress impulse into the procedural helmet renderer.
- Iteration 17 added a dedicated audio-worker bridge for those impacts:
  - fixed-capacity `ImpactAudioEvent[64]`
  - single producer = main-thread `HandlePhysicsImpact(...)`
  - single consumer = audio worker `ConsumePendingImpactAudioEvents(...)`
  - synchronization = `Volatile.Read(...)` / `Volatile.Write(...)` index ownership only
  - payload = `Stress` + `Metallic`
- The audio worker now drains that queue once per block before `RenderHullStressBlock(...)`, then:
  - folds queued stress into `hullTarget`
  - applies `metallicDrive = lerp(1.0, 1.45, metallicImpulse)`
  - applies `rivetAmount = hullRivetBurstAmount * lerp(1.0, 1.8, metallicImpulse)`
  - decays worker-local stress and metallic state without allocations
- `SpatialAudioManager` now subscribes to `PhysicsEvents.OnImpact`.
- Added a fixed-capacity transient impact-emitter array. These emitters are exposed through `CopyActiveWorldEmitterSamples(...)`, so passive radar receives collision energy even without an authored impact clip.
- `AcousticZoneController` now subscribes to `PhysicsEvents.OnImpact`.
- `AcousticZoneController` now maintains two decaying graph inputs:
  - `_acousticImpactImpulse`
  - `_acousticSonarImpulse`
- The listener fallback is now a continuous DSP graph, not a binary preset switch:
  - smoothing: `blendT = 1 - exp(-acousticGraphFollowSharpness * dt)`
  - underwater cutoff: `lerp(underwaterGraphShallowCutoff, underwaterGraphDeepCutoff, depth01)`
  - sonar opens the underwater LPF window: `lerp(baseCutoff, baseCutoff + 2400 Hz, sonarImpulse)`
  - interior decay: `interiorGraphDecayTime + interiorImpactDecayBoost * metallicImpulse + 0.22 * sonarImpulse`
  - reverb reflections, late level, roomHF, and dry level are continuously lerped from underwater or interior baselines
- The runtime graph now writes these coefficients every update without allocations:
  - `AudioLowPassFilter.cutoffFrequency`
  - `AudioLowPassFilter.lowpassResonanceQ`
  - `AudioReverbFilter.decayTime`
  - `AudioReverbFilter.reflectionsLevel`
  - `AudioReverbFilter.reverbLevel`
  - `AudioReverbFilter.roomHF`
  - `AudioReverbFilter.dryLevel`
- `ShouldUseSourceLevelAcousticFallback()` no longer waits for authored mixer effect coverage. If `enableSourceLevelAcousticFallback && enableRuntimeAcousticGraph` is true, the listener graph is authoritative.
- Surface state now restores the captured base listener filter settings instead of forcing both filters off unconditionally.
- The old `MasterMixer effect graph has no authored acoustic processing beyond Attenuation` warning path was removed. Missing authored mixer FX no longer disables or second-guesses the runtime listener graph.

SPSC contract:
- Producer thread writes frames into preallocated `NativeArray<float>` storage.
- Producer advances write index via `Volatile.Write(...)`.
- Audio-thread fallback consumes frames in `OnAudioFilterRead(...)` without allocations.
- No locks, no waits, no per-block managed allocations were added.
- Physics-impact bridge is also SPSC:
  - main thread enqueues `ImpactAudioEvent`
  - audio worker drains queue before each synthesis block
  - queue overflow policy is drop-oldest; producer advances the read pointer when the ring is full so the newest impact always survives

## Canvas Batching / Subtitle Sync

Problem:
- `SubtitleManager` was still synthesizing strings for audio-log playback and assigning `_subtitleText.text` from runtime show/refresh/timed-cue paths.
- `AudioWaveformAnimator` optional cue text also wrote `TMP_Text.text`, so cue changes could dirty the canvas even when the glyph payload was identical.
- Audio-log playback had no zero-GC lore/subtitle bridge. `AudioLogSystem` raised object/string events, but the subtitle owner was not consuming `LoreDatabaseManager` raw buffers by FNV-1a hash.

What changed:
- Added `SubtitleEventBus` as the runtime subtitle handoff owner for lore-backed playback.
- `AudioLogSystem` now computes the stable FNV-1a lore hash once per playback start and publishes:
  - `RaisePlaybackStarted(hash, durationSeconds)`
  - `RaisePlaybackStopped(hash)`
  - `RaisePlaybackCompleted(hash)`
- `LoreDatabaseManager` now exposes hash-based buffer lookups:
  - `TryGetTitleBuffer(uint logHash, ...)`
  - `TryGetBodyBuffer(uint logHash, ...)`
  - `TryGetSpeakerBuffer(uint logHash, ...)`
- `SubtitleManager` no longer subscribes to `AudioLogEvents` for subtitle rendering.
- `SubtitleManager` now subscribes to `SubtitleEventBus` and resolves audio-log subtitle content directly from `LoreDatabaseManager` raw `char[]` buffers.
- Timed subtitle markup is now parsed from the raw body buffer without `Substring`, `Concat`, or `List<T>` churn:
  - fixed `TimedSubtitleCue[32]`
  - cue metadata = `StartTime`, `SpeakerIntensity`, `StartIndex`, `Length`
  - render output = copy from title/body buffers into one fixed `char[2048]`
- TMP writes now use `SetCharArray(...)` and are change-gated against a cached previous buffer before touching the component.
- Generic notification subtitles still enter as strings, but `SubtitleManager` now copies them into the fixed render buffer and only calls `SetCharArray(...)` when the payload actually changes.
- `AudioWaveformAnimator` now consumes the same cue `char[]` slice and writes optional cue text via `SetCharArray(...)`, not `.text`.
- Repo check for the touched runtime subtitle owners:
  - `Assets/_Project/Scripts/UI/SubtitleManager.cs`: `0` `.text =` assignments
  - `Assets/_Project/Scripts/UI/AudioWaveformAnimator.cs`: `0` `.text =` assignments

## Audio Distance Culling

Problem:
- Tier-2 culled world audio was being stopped and reset, but `AudioSource.enabled` was not being forced off. That leaves a needless component active even when DSP should be completely dormant beyond 40 m.

What changed:
- `SpatialAudioManager.PlayAtPoint(...)` now re-enables pooled 3D sources before reuse.
- `SpatialAudioManager.UpdateWorldSourceAudioLod(...)` now forces:
  - `source.enabled = true` in Tier 0 and Tier 1
  - `source.Stop(); source.enabled = false;` in Tier 2
- `SpatialAudioManager.ResetWorldSourceState(..., clearClip: true)` now also keeps the cleared source disabled.

## Regression Model

CPU:
- UI edit-time CPU should drop because automatic rebuild polling was removed.
- Runtime subtitle cost should drop because repeated string-to-TMP invalidation was replaced with change-gated `SetCharArray(...)`.
- Passive radar gets one extra fixed-array append pass for active impact emitters.
- Acoustic fallback now writes listener filter coefficients each update while active.
- Tier-2 world audio now drops CPU harder because culled sources are disabled, not just volume-reduced/reset.

GC:
- No new hot-path managed allocations were added.
- Input display parsing allocates only when a display string is explicitly requested by UI code, not per-frame audio or UI hot paths.
- Acoustic graph state is scalar-only and updates in place.
- Lore-backed subtitle playback now consumes fixed `char[]` buffers from `LoreDatabaseManager`; timed cues no longer allocate `string` fragments during playback.

Memory:
- Added one fixed `ImpactEmitterSample[16]` array in `SpatialAudioManager`.
- Added cached `InputActionReference` objects for `InputSystemUIInputModule` wiring in `InputManager`.
- No unbounded caches were introduced.
- Added fixed subtitle render caches:
  - `SubtitleManager`: `char[2048]` render buffer + `char[2048]` change cache + `TimedSubtitleCue[32]`
  - `AudioWaveformAnimator`: `char[1024]` cue-text cache

Cadence:
- UI editor rebuild is manual only.
- Physics impact audio/radar impulses are event-driven.
- Runtime acoustic graph smoothing runs inside the existing acoustic owner update path.
- Audio-log subtitles now update only on playback start, cue boundary, stress-bucket change for generic notifications, or fade/hide transitions.

Correctness risks:
- Manual editor rebuild means stale HUD hierarchy can remain in the scene until `Rebuild UI` is invoked.
- Physics-impact listener coloration is proximity-based; near-field non-player collisions can still tint the room tone.
- Runtime listener graph and authored mixer graph can diverge stylistically if authored LPF/reverb arrives later and the fallback is left enabled.
- Queue overflow now drops the oldest pending impact event to preserve the newest impact energy. That is intentional, but it means bursts can discard stale impacts before the worker consumes them.
- Subtitle corruption styling now stays on the generic notification path only. Lore-backed playback renders raw lore buffers to preserve zero-allocation delivery.

Why kept:
- The deadlock source was editor-time auto mutation. Manual rebuild is the direct containment strategy.
- The default UI action asset was the only first-party startup path still capable of invoking Unity's virtual-key translation before play stabilized.
- Editor shortcut stripping is low-risk because it only removes hotkey accelerators; menu commands still exist.
- The acoustic-graph extension keeps ownership inside `AcousticZoneController` instead of inventing a parallel audio subsystem.
- Subtitle ownership now stays split correctly:
  - `AudioLogSystem` owns playback timing and lore-hash publication
  - `LoreDatabaseManager` owns localized/fallback `char[]` data
  - `SubtitleManager` owns UI rendering only

## Verification Checklist

1. In the editor, select the HUD overlay or scaler component and invoke `Rebuild UI`.
2. Confirm the Console no longer reports `SendMessage cannot be called during Awake... or OnValidate (BiosBackdrop: OnRectTransformDimensionsChange)`.
3. Select the `PlayerPDA` component and invoke `Rebuild PDA`; confirm the hierarchy only changes on that explicit action.
4. Run `Hecton-8/Rebake Lore Hashes`, then enter play mode and confirm lore seed mismatch spam is gone.
5. Restart the editor and confirm `GetVirtualKey` spam is gone from both Console and `Editor.log`.
6. Enter `01_MAIN_MENU` and confirm the `InputSystemUIInputModule` no longer resolves Unity's package `DefaultInputActions`.
7. Enter play mode, trigger hull or transport collisions, and confirm:
   - passive radar reacts to those impulses
   - `AcousticZoneController` low-pass and reverb debug values move in response to nearby impacts
8. Trigger an active sonar ping underwater and confirm the runtime LPF opens and the reverb tail lifts.
9. Play one lore-backed audio log and confirm the HUD subtitle path:
   - receives a `SubtitleEventBus` start event
   - resolves the same FNV-1a lore hash in `LoreDatabaseManager`
   - updates `SubtitleManager` via `SetCharArray(...)` only on cue boundaries
10. Profile gameplay and audio thread:
   - `OnAudioFilterRead`: expected `0 B` GC
   - UI hot paths: expected `0 B` GC

Measured proof: absent. Unity validation required.

## Verification Blockers

- `dotnet build Hecton8.Core.csproj` succeeded after the `PlayerPDA` and `AcousticZoneController` edits.
- `dotnet build MoreMountains.Feedbacks.csproj` succeeded after the `[Serializable]` fixes for `MMF_Position`, `MMF_DestinationTransform`, and `MMF_PositionShake`.
- `dotnet build Hecton8.Editor.csproj`, `MoreMountains.Tools.Editor.csproj`, `AstarPathfindingProjectEditor.csproj`, and `RealtimeCSG.csproj` also succeeded after the editor shortcut sweep.
- `dotnet build MoreMountains.Feedbacks.Editor.csproj` succeeded after the `MenuItem(...)` label cleanup.
- Aggregate `dotnet build Assembly-CSharp.csproj` still fails because unrelated DOTS files under `Assets/_Project/Scripts/World/Dots/` reference missing `Unity.Entities` assemblies.
- `dotnet build Hecton8.Core.csproj` in this pass is blocked by unrelated missing `Library/PackageCache/com.unity.shadergraph@6ab0d236faac/...` sources and missing cached metadata such as `Unity.ShaderGraph.Editor.dll` / `WaveHarmonic.Crest.dll`.
- No live Unity session was available for MCP reflection or Console readback during this pass, so the final status for `0 INPUT SPAM`, `0 DEADLOCKS`, and acoustic behavior remains `PENDING VERIFICATION`.

## Iteration 21 — Acoustic Radar / UI Awake Sweep

Mandates followed:
- `UI_Data_Streaming_ZeroGC_Optimization`
- `UI_Localization_Babel_RTL_FontSwap_ZeroAlloc`
- `AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC`
- `OPT_Zero_GC_Policy_AllocFree_Mandate`

UI Awake / Start sweep:
- `PlayerPDA.Awake()` no longer bootstrap-resolves the player with `SceneBootstrap.TryGetCurrentPlayerTransform(...)`.
- `PDADiagnosticTerminal.Awake()` no longer bootstrap-resolves `HectonPlayerMovement`.
- Both paths now resolve through `GlobalRegistry.Player` only, and battery-drain survival lookup is deferred until runtime use.
- `SubnauticaSystemsDebugUI` and `SuitHUDV4CanvasOverlay` were re-audited; no `FindObjectsOfType` / `FindAnyObjectByType` calls were found in their `Awake()` / `Start()` bodies.

Acoustic radar mapping:
- `SonarHoloCompass` was repurposed from abyssal-anchor dots to physics-impact radar blips.
- Source data is now `SpatialAudioManager.CopyActiveImpactEmitterSamples(...)`, which consumes the same impact-emitter store driven by `PhysicsEvents.OnImpact`.
- The 3D absolute-universe-position to 2D HUD projection now runs inside a Burst `IJobParallelFor`.
- The overlay still uses a fixed `RectTransform[16]` + `Image[16]` pool. No per-frame GameObject churn was introduced.

Subtitle formatting:
- Lore-backed title/body composition now uses a `Span<char>` builder over the fixed subtitle render buffer before `TMP_Text.SetCharArray(...)`.
- Generic notification corruption is still upstream-string-based because `LocalizationManager.ApplyHullStressCorruptionIfNeeded(...)` only exposes a `string` API.

Audio culling / floating origin:
- `SpatialAudioManager.ResolveAudioLodTier(...)` and `ResolvePredictedArrivalTime(...)` now convert both listener and source positions through `HectonFloatingOrigin.ToAbsoluteUniversePosition(...)` before distance checks.
- Impact emitters now fade their exported amplitude over lifetime instead of remaining flat until expiry. This feeds both the passive radar path and the UI radar blips.

Compile gate:
- `dotnet build Hecton8.Core.csproj -p:BuildProjectReferences=false` now succeeds with `0` errors.
- Current compile output still carries unrelated warnings, including missing `Unity.Cecil.Awesome` reference resolution and obsolete API notices in untouched files.

Iteration 21 follow-up sweep:
- A broader `Assets/_Project/Scripts/UI` search exposed runtime font scans outside the original three-owner scope:
  - `FontAssetRecovery.Awake()` used `Resources.FindObjectsOfTypeAll<TMP_FontAsset>()` and `Resources.FindObjectsOfTypeAll<TMP_Text>()`
  - `PauseControlsPanel.ResolveReadableFont(...)` and `PauseMenuController.ResolveReadableFont(...)` used `Resources.FindObjectsOfTypeAll<TMP_FontAsset>()`
- Those sites were neutralized in this pass:
  - `PauseControlsPanel` and `PauseMenuController` now route font resolution through `LocalizedFontResolver.ResolveReadableFont(...)`
  - `FontAssetRecovery.Bootstrap()` no longer instantiates a transient recovery object, and the dormant scan method was replaced with empty arrays so no runtime global search path remains in UI code
- Post-patch evidence:
  - `rg -n "FindObjectsOfType|FindObjectOfType|FindAnyObjectByType|FindFirstObjectByType|Resources\.FindObjectsOfTypeAll" Assets/_Project/Scripts/UI -g "*.cs"` -> `NO_MATCHES: Assets/_Project/Scripts/UI`
  - `dotnet build Hecton8.Core.csproj -p:BuildProjectReferences=false` -> `0` errors, warnings only

## Iteration 22 — Acoustic Radar / Subtitle / SPSC Audit

Mandates followed:
- `UI_Data_Streaming_ZeroGC_Optimization`
- `UI_Localization_Babel_RTL_FontSwap_ZeroAlloc`
- `AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC`
- `OPT_Zero_GC_Policy_AllocFree_Mandate`

Audit result:
- `SonarHoloCompass` already used a fixed `RectTransform[16]` + `Image[16]` radar pool, persistent `NativeArray` projection buffers, and a Burst `ProjectImpactBlipsJob` fed by `SpatialAudioManager.CopyActiveImpactEmitterSamples(...)`.
- `SubtitleManager` already composed lore-backed audio-log title/body frames through `SubtitleSpanBuilder` into the preallocated subtitle render buffer before `TMP_Text.SetCharArray(...)`.
- `SpatialAudioManager.UpdateWorldSourceAudioLod(...)` already hard-culled Tier 2 world audio by calling `source.Stop()` and `source.enabled = false`.

Runtime fix:
- `PlayerCriticalProceduralAudioRenderer.TryEnqueueImpactAudioEvent(...)` previously advanced `_impactEventReadIndex` directly on overflow. That let the producer stomp the consumer read pointer if the audio thread had already advanced since the producer observation.
- Overflow handling now uses `Interlocked.CompareExchange(...)` to drop the oldest unread impact only when the observed read pointer is still current. If the consumer moved first, the producer retries and re-evaluates capacity.

Compile gate:
- `dotnet build Hecton8.Core.csproj -p:BuildProjectReferences=false` -> `0` errors, warnings only.

## Iteration 24 - Recursive Callback Audit

Mandates followed:
- `UI_Data_Streaming_ZeroGC_Optimization`
- `UI_Localization_Babel_RTL_FontSwap_ZeroAlloc`
- `AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC`
- `OPT_Zero_GC_Policy_AllocFree_Mandate`

Callback audit:
- `SubnauticaSystemsDebugUI` no longer performs runtime UI construction, canvas resolution, manager resolution, stress-application, or immediate diagnostics refresh inside `Awake()` / `OnEnable()`.
- `SubnauticaSystemsDebugUI.EnsureRuntimeOverlayInstances()` no longer calls `FindObjectsByType<SubnauticaSystemsDebugUI>(...)`; it reuses `s_activeRuntimeInstance` or creates one fallback owner.
- Deferred bootstrap now runs through `ProcessPendingBootstrap()` on the registered UI tick path, guarded by static `s_isBootstrappingRuntimeOverlay`.
- First-party rebuild API sweep found one first-party layout-rebuild site in `LocalizedLayoutMirror`; that call now routes through `MarkLayoutForRebuildSafe(...)`, guarded by static `s_isRebuildingLayout` and instance `_isApplyingMirroring`.
- `SuitHUDV4CanvasOverlay` was re-audited in this pass; no `Canvas.ForceUpdateCanvases(...)` or `LayoutRebuilder.MarkLayoutForRebuild(...)` calls exist in that file.
- `LocalizedTMPAutoSizer` remains the only first-party `OnRectTransformDimensionsChange()` owner under `Assets/_Project/Scripts/UI`; it already uses `_isApplyingConfiguration` on the apply path.

Raw audit outputs:
- `rg -n "FindObjectsByType|FindObject|FindObjectsOfType|Resources\.FindObjectsOfTypeAll" Assets/_Project/Scripts/UI/SubnauticaSystemsDebugUI.cs` -> no matches
- `rg -n "ForceUpdateCanvases|MarkLayoutForRebuild|OnRectTransformDimensionsChange" Assets/_Project/Scripts/UI -g "*.cs"` ->
  - `Assets/_Project/Scripts/UI/LocalizedLayoutMirror.cs:119: MarkLayoutForRebuildSafe(targetLayoutGroup.transform as RectTransform);`
  - `Assets/_Project/Scripts/UI/LocalizedLayoutMirror.cs:154: LayoutRebuilder.MarkLayoutForRebuild(rectTransform);`
  - `Assets/_Project/Scripts/UI/LocalizedTMPAutoSizer.cs:76: private void OnRectTransformDimensionsChange()`
- automated `OnValidate` mutation sweep (`SetParent`, `AddComponent`, `sizeDelta`, `SetActive`, `Destroy`, `MarkLayoutForRebuild`, `ForceUpdateCanvases`) -> `OnValidateMutationCount=0`

Compile gate:
- `dotnet build Hecton8.Core.csproj -p:BuildProjectReferences=false` in this pass is blocked by unrelated `Assets/_Project/Scripts/World/ScatterBackendBindingBridge.cs(54,57): error CS0103: The name 'EntityId' does not exist in the current context`.
