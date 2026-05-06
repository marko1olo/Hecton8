# Warning Cleanup

Date: 2026-05-07
Status: PENDING VERIFICATION
Scope: first-party compiler warning cleanup plus current Unity editor warning readback

## Mandates Followed

- `.agents-skills/PROJECT_LTS_Compatibility_Layer.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt`
- `.agents-skills/OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `.agents-skills/VOX_Voxel_World_Logic_Carving_Persistence.txt`
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`

## What Was Wrong

Fresh `Hecton8.Core.csproj` compile initially returned `8 Warning(s)` and `0 Error(s)`:

- `HectonVoxelEngine.cs`: obsolete `Object.GetInstanceID()` in native memory label generation.
- `PlayerCriticalProceduralAudioRenderer.cs`: unused `MinnaertBubbleBurstJob` fields never assigned.
- `HectonCelestialEngine.cs`: unused serialized `flareFadeMarginDegrees`.

After those fixes, the next Core compile exposed one remaining `CS0618`:

- `ModalWindow.cs`: obsolete `FindFirstObjectByType<T>(FindObjectsInactive)` in singleton fallback lookup.

Unity console refresh then showed one runtime/editor warning:

- `SystemDispatcher.cs`: `TELEMETRY_LOAD_SHEDDING` was emitted through `Debug.LogWarning` even though load-shedding telemetry was already routed through `CrashTelemetryBuffer` and `GlobalTelemetryBus`.

After the editor smoke-test update, a later script refresh exposed two more first-party console issues:

- `SettingsPanel.cs`: `NullReferenceException` from slider binding after script/domain reload because nonserialized listener delegates were null.
- `SystemDispatcher.cs`: slow dispatcher phase warnings were still emitted through `Debug.LogWarning` even though stall telemetry was already published.

Fresh foundation guard then exposed one hard source-gate failure:

- `MainMenuController.cs`: native `Update()` fallback bypassed dispatcher cadence when `_registeredToTickManager` was false.

## What Changed

- `Assets/_Project/Scripts/HectonVoxelEngine.cs`
  - replaced `GetInstanceID()` with Unity 6 `GetEntityId()` for the modified-cells native memory label.
- `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs`
  - removed the unused `MinnaertBubbleBurstJob` wrapper.
  - kept the live `RenderMinnaertBubbleBurstKernel(...)` path unchanged.
- `Assets/_Project/Scripts/HectonCelestialEngine.cs`
  - removed the unused serialized `flareFadeMarginDegrees` field.
- `Assets/_Project/Scripts/ModalWindow.cs`
  - replaced `FindFirstObjectByType<ModalWindow>(FindObjectsInactive.Include)` with `FindAnyObjectByType<ModalWindow>(FindObjectsInactive.Include)`.
  - singleton claiming still runs through `TryClaimInstance()`.
- `Assets/_Project/Scripts/Core/SystemDispatcher.cs`
  - removed the development-console `Debug.LogWarning` branch for late-frame event load shedding.
  - removed development-console slow dispatcher, foveated completion, and forced job-completion warning branches while preserving `GlobalTelemetryBus.PublishJobBarrierStall(...)`.
  - kept `CrashTelemetryBuffer.ReportLateFrameLoadShedding`, `CrashTelemetryBuffer.ReportEventCascadeWarning`, and `GlobalTelemetryBus.PublishPerformanceWarning`.
- `Assets/_Project/Scripts/UI/SettingsPanel.cs`
  - added idempotent listener-action caching before `OnEnable()` slider/button binding.
  - guarded `BindSliders()` against post-reload null delegate fields.
- `Assets/_Project/Scripts/Audio/Editor/DSPThreadSafetySmokeTester.cs`
  - updated the source smoke-test expectation from the removed unused `MinnaertBubbleBurstJob` wrapper to the live `RenderMinnaertBubbleBurstKernel(...)` producer path.
- `Assets/_Project/Scripts/MainMenuController.cs`
  - removed the native `Update()` dispatcher-bypass fallback.
  - main menu cadence now depends on the existing `GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI)` path and bootstrap route enforcement.

## Verification

Command:

```text
dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary
```

Final Core result:

- `Build succeeded.`
- `0 Warning(s)`
- `0 Error(s)`
- elapsed: `00:00:27.18`

Command:

```text
dotnet build Hecton8.Editor.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary
```

Final Editor result:

- `Build succeeded.`
- `0 Warning(s)`
- `0 Error(s)`
- elapsed: `00:00:15.12`

Command:

```text
dotnet build Hecton8.World.Dots.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary
```

Final DOTS result:

- `Build succeeded.`
- `0 Warning(s)`
- `0 Error(s)`
- elapsed: `00:00:04.03`

Command:

```text
dotnet build Hecton8.PlayModeTests.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary
```

Final PlayModeTests result:

- `Build succeeded.`
- `0 Warning(s)`
- `0 Error(s)`
- elapsed: `00:00:15.62`

Command:

```text
.\Tools\ReloadAudit\Scan-FoundationGuards.ps1
```

Final foundation guard result:

- exit code: `0`
- generated: `Docs/Reports/2026-05-03_FOUNDATION_GUARD_SCAN.md`
- `Unauthorized Unity loop methods`: `0`
- `UnsafeUtility.MemCpy outside guard`: `0`
- synchronous job `.Run(` sites: `0`
- release-reachable direct hot-path `Debug.Log` sites: `0`

Targeted editor smoke test:

```text
Hecton8.Audio.Editor.DSPThreadSafetySmokeTester.Run(out report)
```

- result: `PASS`
- report ended with `STATUS: PASS`

Unity MCP verification sequence:

- Console was cleared before the final console-read check.
- `refresh_unity(scope=scripts, compile=request, wait_for_ready=true)` requested script compilation and reported `resulting_state: compiling`.
- Follow-up `refresh_unity(scope=scripts, compile=none, wait_for_ready=true)` reported `resulting_state: idle`.
- Final `read_console(types=[error, warning])` returned `0` entries.

## Zero-GC / Hot Path Impact

- Voxel native label generation is a cold label path and already returns a managed string.
- Audio bubble synthesis hot path still calls the same Burst-decorated static kernel; only an unused job wrapper was removed.
- Modal lookup is a cold singleton fallback path.
- SettingsPanel listener caching is lifecycle/reload binding work, not per-frame work.
- MainMenuController no longer owns a native Unity `Update()` fallback; runtime cadence remains dispatcher-owned.
- SystemDispatcher now avoids the development-console warning branch for late-frame load shedding; telemetry emission remains in the existing native/telemetry path.
- No LINQ, per-frame managed collections, coroutine use, or new hot-path allocations were introduced by this cleanup.

## In-Game Result

Not verified in gameplay. This pass proves current command-line Core compile cleanliness and current editor console warning/readback cleanliness after script refresh. It does not prove Play Mode stability, frame time, GC, audio output, celestial visuals, modal UI flow, or player-build readiness.

## Residual Risks

- `Tools/ReloadAudit/Scan-FoundationGuards.ps1` now exits `0` after `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md`; this warning-cleanup report still does not prove Play Mode, profiler, GC, or player-build readiness.
- The worktree was dirty before this pass; unrelated source, asset, shader, document, and artifact changes were not reverted.
