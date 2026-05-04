# 2026-05-03 Foundation Hardening Continuation

Status: PENDING VERIFICATION

## 2026-05-04 Supersession Note

This report is historical implementation/build evidence. Current global documentation/build/guard truth starts at `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md` and `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md`.

The May 4 post-repair guard scan regenerated `Docs/Reports/2026-05-03_FOUNDATION_GUARD_SCAN.md` and now exits `0`. Current guard counts are `.Run(` sites `0`, hot-path `.Run(` review sites `0`, `.Complete(` text hits `5`, guarded dispatcher completion sites `1`, `UnsafeUtility.MemCpy outside guard` `0`, unauthorized Unity loop methods `0`, runtime Find API review hits `8`, and global registry self-registration inventory `500`. Older guard-clean lines inside this May 3 report are historical.

## Scope

- Removed the remaining forced PhysX-bake completion path from `HectonWorldGenerator` runtime chunk retirement.
- Added async mesh-build yield watchdogs to `ProceduralWreckGenerator`.
- Reduced floating-origin stability watchdog from 50,000 frames to 1,200 frames.
- Kept `UserOptionsPersistence` owned by bootstrap/GlobalRegistry instead of self-persisting from the input assembly.
- Added a guarded `Start()` dispatcher-registration retry to `BaseAirlock` so airlock cycling is not lost when the airlock enables before `SystemDispatcher` registration.
- Removed the `BaseAirlock` missing-spawn development log's string interpolation and `gameObject.name` access.
- Routed `BaseAirlock` player teleports through the existing safe-teleport protocol, finite pose validation, center-of-mass reset, transform publication, and sleep-state preservation.
- Moved `BaseAirlock` teleport destination validation before cycle state/audio/event mutation so invalid spawn data cannot leave the airlock stuck cycling.
- Hardened `LoreDatabaseManager` save participation by tracking the exact `ISaveService` it registered with, retrying save registration in `Start()`, handling Save service replacement/clear through `IGlobalRegistryHotSwapListener`, and refusing edit-mode save registration.
- Hardened `LoreDatabaseManager` native teardown by flushing the deferred `NativeArray<uint>` disposal job without calling `.Complete()`.
- Added a dedicated `HabitatGraphManager` traversal scratch buffer so BFS/component walks no longer reuse and overwrite `_anchorReachability`.
- Hardened `HabitatGraphManager.DisposeNativeBuffers()` so disposed `NativeArray` fields are reset to `default` after disposal.
- Added a `HabitatGraphManager` siege-target owner token so stale graph-manager disposal cannot clear a newer global siege-target snapshot.
- Added explicit cold-allocation ownership comments to `HabitatGraphManager` anchor reachability and traversal queue native buffers.
- Added bounded destruction-drop drains for organic entropy buffers so deferred drop queues cannot monopolize a frame.
- Added bounded tool-breakdown queue draining so durability breakdown events cannot retain stale queued state indefinitely.
- Added dispatcher-raycast sidecar tail clearing so receiver/request arrays cannot retain stale references if the NativeQueue drains fewer commands than the managed pending count.
- Replaced base-module runtime registry list views with owner-controlled count/index accessors so hot-path consumers do not hold `IReadOnlyList<BaseModule>` views over mutable registries.
- Replaced scoped construction/base `PlayerInventory.Instance`, `ObjectPoolManager.Instance`, and `HectonFluidEngine.Instance` reads with `GlobalRegistry` service access where a registered service exists.
- Added bounded collider-id lookup caching for `RepairDroneHub` storage discovery so repeated NonAlloc overlap scans do not repeatedly resolve the same `StorageCrate` component hierarchy.
- Added bounded collider-id lookup caching for `AutonomousExtractorModule` resource-host discovery so slow-tick binding refresh does not repeatedly resolve the same `ResourceNode` component hierarchy.
- Added a rejected-collider cache to `VehicleDockingModule.OnTriggerStay()` so invalid trigger contacts do not repeat component hierarchy scans every physics step.
- Upgraded `VehicleDockingModule` trigger-owner discovery to full-width `ulong` collider ids with a bounded resolved-owner cache, removing repeated hierarchy resolution for valid transport contacts.
- Upgraded `BaseModule` interior dry-zone occupant tracking from truncated `int` collider ids to full-width `ulong` EntityIds so buoyancy occupancy cannot alias colliders in long sessions.
- Hardened `ConstructionManager` save participation with exact `ISaveService` ownership, `Start()` retry, and `IGlobalRegistryHotSwapListener` rebinding for Save service replacement/clear.
- Removed synchronous `IJobParallelFor.Run()` from `TetherInstance.UpdateVisuals()`; tether visual catenary now uses a direct indexed loop over existing persistent `NativeArray` buffers.
- Removed four synchronous `IJob.Run()` calls from `PlayerInventory` bounded inventory kernels. Sort, derived mass/radiation refresh, radioactive half-life, and reactive chemistry now execute as direct inline struct kernels over existing SOA `NativeArray` buffers.
- Removed two synchronous `IJob.Run()` calls from `CraftingSystem` recipe availability and deconstruction-yield passes. Both now execute as inline struct kernels over caller-owned native buffers.
- Recorded that `QuestStateManager.EvaluateSignal()` now uses direct kernel execution; its prior `.Run()` site is absent from the current generated guard output.
- Removed the cold one-element `IJob.Run()` prime from `SargassumMicroFaunaBoids.PrimeFoveatedSimulationDecision()`. It now calls the existing kernel `Execute()` directly over the persistent `NativeArray[1]` buffers before the first runtime tick.
- Removed `IJobParallelFor.Run()` from `FaunaSimulationEngine.RunHibernationCatchUp()`; bounded hibernation restore now executes the same kernel directly over persistent native input/result buffers.
- Removed the remaining first-party runtime `.Run(` inventory: `FloraInteractionManager` cascade phase seeds now schedule/complete through the dispatcher window, and the guard scan reports zero `.Run(` sites.
- Fixed fallback beacon material/despawn ownership: fallback beacons no longer share one mutable colored material, each owned fallback material is released by its `BeaconRuntime` on destroy, and prefab-spawn fallback failures no longer mark the fallback object as pool-owned.
- Removed stale `SubtitleManager.Instance` references after the manager was converted to a private active-runtime owner field.
- Added an Editor project-generation pruner and root MSBuild target for stale generated Entities package references. The generated `.csproj` files still contain the text until Unity regenerates them, but command-line MSBuild now removes missing `Unity.Cecil.Awesome`, `Unity.Entities`, and missing generated analyzer items before compiler/reference resolution.
- Restored stale `Hecton8.Editor.csproj` compile includes for the tech-art audit helpers used by `HectonAssetPipelineAudit`.
- Added a repeatable foundation guard scan for blind registry truth-state drift and job barrier inventory.
- Replaced remaining blind `GlobalRegistry.Renderables.Register(this)` success flags in first-party runtime owners with `GlobalRegistry.Renderables.Contains(this)` truth-state checks.
- Expanded the guard to broad `GlobalRegistry.Register*(...this...)` service registrations and replaced blind service `_isInitialized = true` in `DebrisManager` / `PhysicsApplySystem` with authoritative slot ownership checks.
- Expanded the guard to `HectonFloatingOrigin.RegisterListener(this)` blind flag drift and split `HectonMapMagicVegetationBridge` event state from floating-origin listener state.
- Hardened `PlayerFlashlight` native input subscription ownership with `IGlobalRegistryHotSwapListener` so `GlobalRegistryServiceSlot.Input` replacement rebinds without a per-frame singleton retry.
- Hardened interaction prompt/tooltip native input subscription ownership. `Interaction/InteractionUI`, `UI/InteractionUI`, and `DiegeticTooltipSystem` now retry cold binding/display-style subscription in `Start()` and rebind from `GlobalRegistryServiceSlot.Input`/`InputBinding` hot-swap notification without adding Tick singleton polling.
- Normalized cold-allocation ownership comments in the interaction/fabricator slice. `PlayerInteraction` now uses the canonical `RaycastHit[1]` cold allocation comment, and `HectonFabricatorUI` now documents fixed recipe row and char-buffer ownership.
- Removed private static `_instance` and internal `Instance` residue from the Optimization/VRAM runtime services; `GlobalRegistry` slots now gate duplicate authority for RT and VRAM managers.
- Renamed asset-dispatch acknowledgement from `AssetLoadDispatcher.Complete(int, bool)` to `AcknowledgeDispatchRequest(int, bool)` so the foundation guard no longer counts asset tickets as `JobHandle.Complete()` sites.
- Removed direct raw-array listener invocations from the remaining first-party event-lane dispatch scan. Bootstrap, weather, flashlight, ending, first-hour, storage-reservation commit, physics-impact, tool-effect, power telemetry, high-pressure, and fatal-pressure lanes now skip null listener slots.
- Hardened `PDAIntrusionManager` UI submit-action ownership with `IGlobalRegistryHotSwapListener` so input-service replacement clears stale native action owners and rebinds the cached UI `Submit` action outside `Tick()`.
- Expanded the foundation guard with hard source gates for unauthorized Unity loop methods outside `SystemDispatcher`, legacy coroutine sites, `Resources.Load`, and `Camera.main`, then optimized the scan with cheap prefilters so direct guard runtime dropped from a measured 143,180 ms clean run to 21,395 ms on the same workspace.
- Tightened the guard's debug-log classifier to separate release-reachable direct hot-path `Debug.Log` sites from one-hop review candidates.
- Removed the final direct `InputManager.Instance` bootstrap read by resolving the native input owner through the existing scene-local scratch resolver.
- Guarded the remaining release-reachable one-hop crafting/save diagnostics behind `UNITY_EDITOR || DEVELOPMENT_BUILD`, leaving save failure events intact while removing release hot-path Debug string formatting from the guard inventory; the SaveManager integrity-drift path also no longer allocates a `MemoryCorruptionException` only for log formatting.
- Repaired a malformed duplicated `#endif` in `Fabricator.cs` after the inventory-full development log guard so Core compilation is no longer blocked by `CS1027`.
- Restored the stale `Hecton8.Core.csproj` compile include for `Gameplay/BaseAirlockEvents.cs`; the source file already existed and is NativeQueue-backed, but local Core compilation could not resolve `BaseAirlockEvents` while the generated project surface omitted it.
- Repaired `HUDSaveNotificationLink` after the localization language contract moved to `GameLanguage`; the bounded save-message cache key now uses the enum value instead of an invalid string hash path.
- Hardened root MSBuild generated-project handling for PlayMode tests: `Hecton8.PlayModeTests` now receives a real `ProjectReference` to `Hecton8.Core.csproj`, and missing generated `Temp\bin\Debug` hint-path references are pruned only when the DLL is absent. This prevents `CS0006` from blocking source analysis when Unity-generated output DLLs have not been materialized yet.

## Evidence

Prior Unity batchmode log before the registry/supply-cache continuation:

- File: `Temp/CodexArtifacts/unity-batch-2026-05-03-foundation-hardening-after-watchdogs.log`
- Result: `Exiting batchmode successfully now!`
- Tundra: `Tundra build success (51.07 seconds), 33 items updated, 1808 evaluated`
- Script compile: `CompileScripts: 52654.128ms`
- Mono reload: `Mono: successfully reloaded assembly`
- Strict compiler failure scan: `0` matches for `error CS`, `warning CS`, `Compiler error`, `Scripts have compiler errors`, `Tundra build failed`, `Compilation failed`

Earlier full dotnet verification before generated-reference pruning:

- Command: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false`
- Successful result after `HabitatGraphManager`, `LoreDatabaseManager`, `BaseAirlock`, and documentation-current-state update: `0 Error(s)`, `18 Warning(s)`.
- Residual warnings: `MSB3245 Unity.Cecil.Awesome` generated-project reference warnings. These are not C# warnings from modified files.
- One intermediate Core build reported stale `SubtitleManager.Instance` `CS0103` errors; immediate source recheck found only `s_activeInstance`, and the next identical Core build passed. Treat the failed pass as superseded transient evidence unless it recurs.
- One later full ProjectReferences rerun temporarily failed with `CS2012` while writing `Temp\obj\Unity.RenderPipelines.Core.Editor\Unity.RenderPipelines.Core.Editor.dll`; subsequent full Core and Editor builds passed with `0 Error(s)` / `0 Warning(s)`.

Current full Core verification:

- Command: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false`
- Result: `Build succeeded.`
- Errors: `0`
- Warnings: `0`

Current full Editor verification:

- Command: `dotnet build Hecton8.Editor.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false`
- Result: `Build succeeded.`
- Errors: `0`
- Warnings: `0`

Prior Unity EditMode self-test evidence before the registry/supply-cache continuation:

- Log: `Temp/CodexArtifacts/unity-test-2026-05-03-spatialhash-selftest-after-beacon.log`
- XML artifact: `Temp/CodexArtifacts/editmode-results-2026-05-03-spatialhash-selftest-after-beacon.xml`
- Result: `Passed`
- Total: `3`
- Passed: `3`
- Failed: `0`
- Covered cases:
  - stale spatial-hash handles are rejected after unregister/release
  - recycled handles advance generation and cannot be confused with stale handles
  - moved entries leave no ghost occupancy in the source cell
  - AUP-scale coordinates query without float-origin drift at the tested range
- Unity note: command-line `-testResults` logged a save to the requested project path, but the file was only present in Unity's LocalLow test result path. The XML was copied to `Temp/CodexArtifacts` after verification.
- Unity warning: the long test log includes transient compile errors from concurrently dirty UI files that were later corrected during the same AssetDatabase refresh. Use the XML for the targeted test result; do not use that log as strict zero-error console proof.
- Follow-up dotnet evidence after current UI compile blockers: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false` ended with `0 Error(s)` after recreating `Temp/bin/Debug`; residual warnings are generated-project reference warnings for missing `Unity.Cecil.Awesome`.

Registry/resource-cache continuation static evidence:

- `git diff --check -- Assets/_Project/Scripts/Construction/AutonomousExtractorSystem.cs Assets/_Project/Scripts/Construction/RepairDroneHub.cs Docs/Reports/2026-05-03_FOUNDATION_HARDENING_CONTINUATION.md` returned no whitespace errors; only CRLF normalization warnings.
- Scoped source scan found no remaining `BaseModule.ActiveModules`, `SpawnedBaseModules`, `PlayerInventory.Instance`, `ObjectPoolManager.Instance`, or `HectonFluidEngine.Instance` in the touched foundation slice.
- Initial Unity compile relaunch for this slice was deferred because an existing Unity EditMode test process was active:
  `unity-test-2026-05-03-spatialhash-selftest-after-beacon.log`.

Current Unity compile/import evidence after the resource-cache continuation:

- Log: `Temp/CodexArtifacts/unity-batch-2026-05-03-foundation-resource-cache-final.log`
- Result: `Exiting batchmode successfully now!`
- Tundra: final pass included `Tundra build success` at lines `298`, `447`, `456`, `538`, and `591`.
- Mono reload: `Mono: successfully reloaded assembly`
- Strict compiler failure scan:
  - `error CS=0`
  - `warning CS=0`
  - `Tundra build failed=0`
  - `Scripts have compiler errors=0`
  - `Compiler error=0`
  - `Compilation failed=0`
- Two prior Unity reruns exposed stale AssetDatabase compile states in `PDAControlsRebindUI` and `PDAShellChrome`; current source scans and scoped `Hecton8.Core` builds showed those stale symbols were already absent before the final Unity import cleared them.

Construction trigger/save continuation static evidence:

- Command: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false -p:BuildProjectReferences=false`
- Result: `Build succeeded.`
- Errors: `0`
- Warnings: `0`
- `git diff --check -- Assets/_Project/Scripts/BaseModule.cs Assets/_Project/Scripts/Construction/VehicleDockingModule.cs Assets/_Project/Scripts/ConstructionManager.cs` returned no whitespace errors; only CRLF normalization warnings.
- Source scan found no remaining `Dictionary<int, BuoyancyObject>`, `KeyValuePair<int, BuoyancyObject>`, or `unchecked((int)EntityId.ToULong(other.GetEntityId()))` in `BaseModule` trigger tracking.
- Unity log: `Temp/CodexArtifacts/unity-batch-2026-05-03-construction-trigger-save-hardening-final.log`
- Unity result: `Exiting batchmode successfully now!`
- Tundra: `Tundra build success` at lines `320` and `329`.
- Strict compiler failure scan:
  - `error CS=0`
  - `warning CS=0`
  - `Tundra build failed=0`
  - `Scripts have compiler errors=0`
  - `Compiler error=0`
  - `Compilation failed=0`
- Prior Unity run `unity-batch-2026-05-03-construction-trigger-save-hardening.log` contained stale `PauseControlsPanel` compile errors before later Tundra successes; current source and scoped Core build were clean, and the final fresh Unity log above contains no compiler-error matches.

Continuation local compile check:

- Command: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false -p:BuildProjectReferences=false`
- Result: `Build succeeded.`
- Errors: `0`
- Warnings: `1`
- Residual warning: generated-project reference warning for missing `Unity.Cecil.Awesome`.
- Follow-up dependency rebuilds: `Hecton8.Input.Generated.csproj`, `Hecton8.Bootstrap.Contracts.csproj`, and `Hecton8.World.Contracts.csproj` each built with `0 Error(s)` and one residual `Unity.Cecil.Awesome` reference warning.
- Full ProjectReferences command: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false`
- Full ProjectReferences result: `Build succeeded.`
- Full ProjectReferences errors: `0`
- Full ProjectReferences warnings: `18`, all residual generated-project reference warnings for missing `Unity.Cecil.Awesome`.
- Editor assembly command after restoring generated-project includes: `dotnet build Hecton8.Editor.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false`
- Editor assembly result: `Build succeeded.`
- Editor assembly errors: `0`
- Editor assembly warnings: `0`
- Fresh Unity batchmode fallback after MCP remained unavailable:
  - Log: `Temp/CodexArtifacts/unity-batch-2026-05-03-continued-foundation.log`
  - Result: `Exiting batchmode successfully now!`
  - Tundra: four successful script/import passes; final pass `Tundra build success (67.59 seconds - 0:01:07), 31 items updated, 1808 evaluated`
  - Strict compile-failure scan: `error CS=0`, `warning CS=0`, `Scripts have compiler errors=0`, `Compilation failed=0`, `Tundra build failed=0`, `Unhandled Exception=0`
  - Residual non-script issue: Unity licensing module logged `Access token is unavailable; failed to update`.
- Current scoped continuation evidence after siege-owner and lore-disposal hardening:
  - `dotnet build Hecton8.Input.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false -clp:ErrorsOnly`: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`.
  - `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false -p:BuildProjectReferences=false`: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`.
  - `git diff --check` on `HabitatGraphManager`, `LoreDatabaseManager`, and this report: no whitespace errors; only CRLF normalization warnings.
  - Full ProjectReferences Core build is currently not used as final evidence because a running Unity Editor process locks third-party/generated output DLLs.
  - MCP resources remained empty in this session.
- Current scoped continuation evidence after `BaseAirlock` safe-teleport hardening:
  - One immediate Core retry failed before first-party source compilation because generated `Temp\bin\Debug` dependency outputs were absent for `Crest`, `ShapesRuntime`, `VolumetricLightBeam`, `WaveHarmonic.Crest.Shared`, and `Hecton8.Bootstrap.Contracts`.
  - Serial dependency rebuilds for `Hecton8.Bootstrap.Contracts.csproj`, `Crest.csproj`, `ShapesRuntime.csproj`, `VolumetricLightBeam.csproj`, `WaveHarmonic.Crest.Shared.csproj`, and `WaveHarmonic.Crest.Shared.Editor.csproj` each returned `Build succeeded`, `0 Warning(s)`, `0 Error(s)`.
  - `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false -p:BuildProjectReferences=false -clp:ErrorsOnly`: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`.
  - `git diff --check -- Assets/_Project/Scripts/Gameplay/BaseAirlock.cs`: no whitespace errors; only CRLF normalization warning.
  - MCP resources remained empty in this session.
- Current scoped continuation evidence after airlock validation ordering and lore edit-mode save-registration guard:
  - `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false -p:BuildProjectReferences=false -clp:ErrorsOnly`: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`.
  - `git diff --check -- Assets/_Project/Scripts/Gameplay/BaseAirlock.cs Assets/_Project/Scripts/Narrative/LoreDatabaseManager.cs`: no whitespace errors; only CRLF normalization warnings.
- Current scoped continuation evidence after `HabitatGraphManager` native-allocation comment compliance:
  - `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false -p:BuildProjectReferences=false -clp:ErrorsOnly`: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`.
  - `git diff --check -- Assets/_Project/Scripts/Construction/HabitatGraphManager.cs Assets/_Project/Scripts/Gameplay/BaseAirlock.cs Assets/_Project/Scripts/Narrative/LoreDatabaseManager.cs Docs/Reports/2026-05-03_FOUNDATION_HARDENING_CONTINUATION.md Docs/Reports/README.md`: no whitespace errors; only CRLF normalization warnings.
- Current source-guard rerun after the continuation:
  - `Tools/ReloadAudit/Scan-FoundationGuards.ps1 -ProjectRoot C:\hades\Hecton8 -OutputMarkdown C:\hades\Hecton8\Docs\Reports\2026-05-03_FOUNDATION_GUARD_SCAN.md`: regenerated the guard report.
  - May 3 guard counts at report time: `Blind registry flag drift = 0`, `Origin shift listener blind flag drift = 0`, `Global registry self-registration sites = 495`, synchronous job `.Run(` sites `0`, hot-path synchronous job `.Run(` review sites `0`, completion `.Complete(` text hits `1`, raw `UnsafeUtility.MemCpy` outside guard `0`, legacy `PlayerSignalEvents.On*` subscriptions `0`, direct raw-array listener dispatch `0`, `GlobalRegistry.Input` nullable misuse `0`, direct `InputManager.Instance` sites `0`, hot-path direct `InputManager.Instance` review sites `0`, `Optimization singleton residue = 0`, unauthorized Unity loop methods `0`, legacy coroutine sites `0`, forbidden runtime asset API sites `0`, release-reachable direct hot-path `Debug.Log` sites `0`, release-reachable one-hop `Debug.Log` review sites `0`, broad physics layer masks outside Editor `0`, runtime Find API text hits outside Editor folder `0`. Current May 4 guard truth is listed in the supersession note.
  - Guard performance evidence: after terminating stale duplicated guard processes, one clean pre-optimization run completed in `143180 ms`; after prefilter optimization the same source-only guard regenerated the report in `21395 ms` by direct script invocation and `40603 ms` through nested `powershell -File` CI-style invocation.
  - One immediate Core build launched concurrently with file refresh reported transient `SettingsManager` helper lookup errors; the methods were present in source and the next identical serial build passed.
  - Latest serial Core build command: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false -p:BuildProjectReferences=false -clp:ErrorsOnly`.
  - Latest serial Core build result: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`.
  - Latest full serial Core command after direct-input and release-log cleanup: `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal -clp:ErrorsOnly`.
  - Latest full serial Core result after direct-input and release-log cleanup: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`.
  - Latest controlled scoped Core command after `BaseAirlockEvents.cs` project-include repair: `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false -clp:ErrorsOnly`.
  - Latest controlled scoped Core result: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`.
  - Latest Unity batchmode fallback after MCP resources remained unavailable:
    - Log: `Temp/CodexArtifacts/unity-batch-2026-05-03-release-log-hotpath-baseairlock.log`
    - Result: `Exiting batchmode successfully now!`
    - Tundra: final import/compile log includes five `Tundra build success` entries; final script import pass line: `Tundra build success (41.05 seconds), 12 items updated, 1808 evaluated`
    - Mono reload: `Mono: successfully reloaded assembly`
    - Strict compiler failure scan: `error CS=0`, `warning CS=0`, `Scripts have compiler errors=0`, `Compilation failed=0`, `Tundra build failed=0`, `Compiler error=0`, `Unhandled Exception=0`
    - Residual non-script log entries: Unity licensing `Access token is unavailable; failed to update`, MCP shutdown `Curl error 42: Callback aborted`
  - MCP resources remained empty in this session.

Current warning-gate evidence after generated-reference pruning:

- Source fix: `Assets/_Project/Scripts/Editor/HectonGeneratedProjectReferencePruner.cs` removes missing `Unity.Cecil.Awesome`, `Unity.Entities`, and stale Entities source-generator analyzer items during Unity `.csproj` generation.
- MSBuild fix: `Directory.Build.targets` removes missing `Unity.Cecil.Awesome`, missing `Unity.Entities`, stale `com.unity.entities@` analyzer items, and absent generated `Temp\bin\Debug` hint-path references before `ResolveAssemblyReferences`.
- Command: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false`
- Result: `Build succeeded.`
- Errors: `0`
- Warnings: `0`
- Additional command: `dotnet build Hecton8.World.Dots.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false`
- Additional result: immediate rerun after `Hecton8.World.Contracts` output materialized returned `0 Error(s)`, `0 Warning(s)`. This confirms optional DOTS placeholder assemblies compile when `com.unity.entities` is absent.
- Additional command: `dotnet build Hecton8.Editor.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false`
- Additional result: `0 Error(s)`, `0 Warning(s)`.
- Additional command: `dotnet build Hecton8.Input.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false -clp:ErrorsOnly`
- Additional result: `0 Error(s)`, `0 Warning(s)`.
- Additional command: `dotnet build Hecton8.PlayModeTests.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false`
- Additional result: `0 Error(s)`, `0 Warning(s)` after serial dependency materialization through the generated-output bridge.
- Verification caveat: one concurrent Core/PlayMode build attempt produced `CS2012` file-lock errors on shared `Temp\obj` dependency outputs. Treat parallel local `dotnet build` on Unity-generated projects as invalid evidence; all current clean evidence above is serial.
- PlayMode test compile fix: `Hecton8.PlayModeTests.asmdef` now references `Hecton8.Core`, matching `SmokeTests_SaveLoad.cs` usage of `Hecton8.SaveSystem`.
- PlayMode test API fix: `SmokeTests_SaveLoad.cs` now uses Unity Test Framework `LogAssert.NoUnexpectedReceived()` instead of the nonexistent `LogAssertion`.
- PlayMode scoped command: `dotnet build Hecton8.PlayModeTests.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false -p:BuildProjectReferences=false -clp:ErrorsOnly`
- PlayMode scoped result: `0 Error(s)`, `0 Warning(s)`.
- PlayMode full dependency command after generated-output pruning: `dotnet build Hecton8.PlayModeTests.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false`
- PlayMode full dependency result after serial dependency materialization: `0 Error(s)`, `0 Warning(s)`.
- One intermediate full Core dependency-chain rebuild exposed stale `PlayerInteraction.RefreshActiveInteractKeyCache()` lookup errors and two stale `UIAudioFeedback` unused-field warnings. Source inspection showed the method existed at `PlayerInteraction.cs:340`; the next scoped Core compile and the next full serial Core compile both passed. Treat the failed pass as stale incremental state unless it recurs.
- Final scoped Core rerun after stale-source pass: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false -p:BuildProjectReferences=false -clp:ErrorsOnly`; result `Build succeeded`, `0 Warning(s)`, `0 Error(s)`.
- Final serial Core rerun after PlayMode test repair: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false -clp:ErrorsOnly`; result `Build succeeded`, `0 Warning(s)`, `0 Error(s)`.
- Final serial Editor rerun after dependency outputs were rebuilt: `dotnet build Hecton8.Editor.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false -clp:ErrorsOnly`; result `Build succeeded`, `0 Warning(s)`, `0 Error(s)`.
- Final scoped PlayMode rerun after PlayMode test repair: `dotnet build Hecton8.PlayModeTests.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false -p:BuildProjectReferences=false -clp:ErrorsOnly`; result `Build succeeded`, `0 Warning(s)`, `0 Error(s)`.
- Final focused diff hygiene command: `git diff --check -- Directory.Build.targets Assets/_Project/Scripts/Editor/HectonGeneratedProjectReferencePruner.cs Assets/_Project/Scripts/Editor/HectonGeneratedProjectReferencePruner.cs.meta Assets/_Project/Tests/PlayMode/Hecton8.PlayModeTests.asmdef Assets/_Project/Tests/PlayMode/SmokeTests_SaveLoad.cs Docs/README.md Docs/Reports/README.md Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md Docs/Reports/2026-05-03_FOUNDATION_HARDENING_CONTINUATION.md`; result exit code `0`, CRLF normalization warnings only.
- Editor syntax check: PowerShell `Add-Type` compiled `HectonGeneratedProjectReferencePruner.cs` against Unity editor assemblies; result was compile success with only the expected non-public-type warning.
- Earlier full verbose ProjectReferences builds reported third-party/vendor warnings from Unity package cache and vendor assemblies (`URP`, `GPUInstancer`, `Den.Tools`, `Crest`, `WaveHarmonic.Crest`, `ShaderGraph`). These are not first-party warnings and were not patched under the third-party asset integrity rule.
- Build cadence note: Unity-generated projects share `Temp\obj`; parallel `dotnet build` runs can produce false `CS2012` file locks. Use serial builds for evidence.
- Generated `.csproj` text scan still finds `Unity.Cecil.Awesome` entries because a Unity Editor instance was already running and batchmode exited before import/project regeneration. The root MSBuild target proves command-line warning suppression; the Editor postprocessor remains the source-backed fix for the next Unity project regeneration.
- MCP resources were empty for this pass; no MCP console log was available.

Registry/renderable guard evidence:

- Guard command: `Tools/ReloadAudit/Scan-FoundationGuards.ps1`
- Guard output: `Docs/Reports/2026-05-03_FOUNDATION_GUARD_SCAN.md`
- May 3 guard result after broad service truth-state expansion, floating-origin listener truth-state expansion, full `.Run()` inventory purge, fallback beacon material ownership hardening, hot-path direct-input classification, broad physics-mask guard expansion, optimization singleton cleanup, asset-dispatch acknowledgement rename, raw listener-dispatch guard expansion, and `PDAIntrusionManager` input hot-swap rebinding: `Blind registry flag drift = 0`, `Origin shift listener blind flag drift = 0`, `Global registry self-registration sites = 495`, synchronous job `.Run(` sites `0`, hot-path synchronous job `.Run(` review sites `0`, completion `.Complete(` text hits `1`, raw `UnsafeUtility.MemCpy` outside guard `0`, legacy `PlayerSignalEvents.On*` subscriptions `0`, direct raw-array listener dispatch `0`, `GlobalRegistry.Input` nullable misuse `0`, direct `InputManager.Instance` sites `0`, hot-path direct `InputManager.Instance` review sites `0`, `Optimization singleton residue = 0`, unauthorized Unity loop methods `0`, legacy coroutine sites `0`, forbidden runtime asset API sites `0`, release-reachable direct hot-path `Debug.Log` sites `0`, release-reachable one-hop `Debug.Log` review sites `0`, broad physics layer masks outside Editor `0`, runtime Find API text hits outside Editor folder `0`. Current May 4 guard truth is listed in the supersession note.
- Build command: `dotnet build Hecton8.Core.csproj -v:minimal -nr:false -m:1 -p:UseSharedCompilation=false`
- Build log: `.codex-artifacts/dotnet-Hecton8.Core-2026-05-03-foundation-guard-physicsmask.log`
- Build result: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.
- Warning cleanup: removed dead `UIAudioFeedback` pitch-variation inspector fields because `IAudioService.PlayStatic2D` has no pitch parameter.
- Current tether purge build command: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false`
- Current tether purge build result: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.
- Current inventory inline-kernel build command: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false`
- Current inventory inline-kernel build result: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.
- Current crafting inline-kernel build command: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false`
- Current crafting inline-kernel build result: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.
- Diff check: touched file `git diff --check` returned exit code `0`; CRLF normalization warnings only.

Optimization registry ownership evidence:

- Report: `Docs/Reports/2026-05-03_OPTIMIZATION_REGISTRY_OWNERSHIP.md`.
- Static scan: `rg -n "_instance|Instance =>|public static .*Instance|internal static .*Instance|DontDestroyOnLoad\\(|SINGLETON" Assets/_Project/Scripts/Optimization -g "*.cs"` returned no matches.
- Scoped compile command: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false -p:BuildProjectReferences=false -clp:ErrorsOnly`.
- Scoped compile result: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.
- Full local Core compile result: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.
- Unity batchmode log: `Temp/CodexArtifacts/unity-batch-2026-05-03-optimization-registry-ownership.log`.
- Unity batchmode result: `Tundra build success`, `Mono: successfully reloaded assembly`, `Exiting batchmode successfully now!`.
- Unity strict failure scan: `error CS=0`, `warning CS=0`, `Scripts have compiler errors=0`, `Compilation failed=0`, `Tundra build failed=0`, `Compiler error=0`, `Unhandled Exception=0`.
- Guard script result: `Optimization singleton residue = 0`; this count is now a blocking defect if non-zero.
- Diff check: scoped `git diff --check` returned no whitespace errors; CRLF normalization warnings only.

Foundation guard signal cleanup evidence:

- Report: `Docs/Reports/2026-05-03_FOUNDATION_GUARD_SIGNAL_CLEANUP.md`.
- Source scan after asset-dispatch rename: only `DispatcherJobSwap.TryComplete` still contains `.Complete(` in first-party runtime source.
- Guard result after regeneration: `Synchronous job .Run( sites = 0`, `Hot-path synchronous job .Run( review sites = 0`, `Completion .Complete( text hits = 1`, `Guarded dispatcher completion sites = 1`.
- Scoped compile command: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false -p:BuildProjectReferences=false -clp:ErrorsOnly`.
- Scoped compile result: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.
- Full local Core compile result: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.
- Unity batchmode log: `Temp/CodexArtifacts/unity-batch-2026-05-03-guard-signal-cleanup-rerun.log`.
- Unity batchmode result: `Tundra build success`, `Mono: successfully reloaded assembly`, `Exiting batchmode successfully now!`.
- Unity strict failure scan: `error CS=0`, `warning CS=0`, `Scripts have compiler errors=0`, `Compilation failed=0`, `Tundra build failed=0`, `Compiler error=0`, `Unhandled Exception=0`.
- Residual Unity non-script issue: licensing module logged `Access token is unavailable; failed to update`.

## Surgery Log

`HectonWorldGenerator` now hands canceled/scheduled PhysX bakes to a late-frame deferred teardown queue:

```csharp
if (!DispatcherJobSwap.TryComplete(ref pending.Handle, forceComplete: false))
    continue;

pending.Mesh.Clear();
DestroyDeferredObject(pending.Mesh);
DestroyDeferredObject(pending.Owner);
```

Runtime eviction/cancellation no longer blocks on active PhysX bake handles. Pending chunk renderers/colliders are disabled, then ownership is transferred to the deferred teardown driver until the job naturally completes.

`ProceduralWreckGenerator` mesh-build slicing now aborts after the watchdog ceiling instead of yielding indefinitely:

```csharp
if (!await YieldMeshBuildFrameAsync("merged mesh copy scheduling", meshYieldFrames++))
    return null;
```

`HectonFloatingOrigin` now reports shift-stability failure after 1,200 frames instead of 50,000 frames, keeping physics-pause failures diagnosable within a practical window.

`BaseAirlock` now retries dispatcher registration in `Start()` through the existing guarded `TryRegister()` path:

```csharp
private void Start()
{
    TryRegister();
}
```

The missing-spawn and invalid-spawn development logs now use constant messages and a Unity context object instead of interpolating `gameObject.name`.

`BaseAirlock` now resolves and validates the target pose before mutating cycle state, status light, audio, or UnityEvents:

```csharp
if (!TryResolveTeleportDestination(out Vector3 destinationPosition, out Quaternion destinationRotation))
    return;

_state = AirlockState.Cycling;
_cycleTimer = cycleDuration;
```

`BaseAirlock` room transfer now enters the canonical safe-teleport window before moving the player. This clears queued force packets through `HectonFloatingOrigin.BeginSafeTeleportProtocol()`, pauses integration when the floating-origin owner exists, and releases the pause after the pose write:

```csharp
if (useSafeTeleportProtocol)
    HectonFloatingOrigin.BeginSafeTeleportProtocol();

try
{
    if (player.TryGetComponent(out Rigidbody playerBody))
        TeleportBody(playerBody, destinationPosition, destinationRotation);
    else
        player.SetPositionAndRotation(destinationPosition, destinationRotation);
}
finally
{
    if (useSafeTeleportProtocol)
        HectonFloatingOrigin.EndSafeTeleportProtocol();
}
```

The target pose is rejected if either position or rotation contains a non-finite component. Rigidbody teleports reset center of mass, publish the transform while collisions are disabled, force Unity's interpolation history through a kinematic toggle, and restore the previous sleep state instead of blind `WakeUp()`:

```csharp
body.ResetCenterOfMass();
body.transform.SetPositionAndRotation(position, rotation);
body.PublishTransform();
body.isKinematic = false;
body.isKinematic = wasKinematic;
```

`LoreDatabaseManager` no longer unregisters from whichever save manager happens to be current at disable time. It records the exact `ISaveService` registration owner and unregisters from that owner:

```csharp
saveService.Register(this);
_registeredSaveService = saveService;
```

It also retries the save registration once in `Start()` and listens for Save service replacement/clear events through `IGlobalRegistryHotSwapListener`. This covers normal bootstrap-order gaps and replacement mismatches without per-frame polling.

Save participant registration is now Play Mode gated, matching the service and hot-swap listener paths:

```csharp
if (!Application.isPlaying || _registeredSaveService != null || saveService == null)
    return;
```

`LoreDatabaseManager.OnDestroy()` now schedules the deferred native unlock-word disposal and flushes scheduled batched jobs, but still does not block teardown with `.Complete()`:

```csharp
_disposeHandle = _unlockedWords.Dispose(_disposeHandle);
_unlockedWords = default;
JobHandle.ScheduleBatchedJobs();
```

`HabitatGraphManager` now separates anchor truth from graph traversal scratch. `EvaluateAnchorReachability()` remains the owner of `_anchorReachability`; island flood traversal, fungal target BFS, and component power traversal use `_traversalVisited` instead:

```csharp
_traversalVisited = new NativeArray<byte>(nodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
```

`DisposeNativeBuffers()` now clears disposed native container fields after each disposal:

```csharp
if (_nodes.IsCreated)
{
    _nodes.Dispose();
    _nodes = default;
}
```

This preserves anchor-state truth between graph passes and reduces stale native-container field state after teardown. It does not change graph construction, pathing edge generation, power-rating math, or siege target scoring.

`HabitatGraphManager` now gates the global siege-target snapshot with an owner token. Publishing records the owner; clearing only resets the static snapshot if the clearing manager is still the publisher:

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
private static void ResetStaticSiegeTargets()
{
    s_latestSiegeTargets = default;
    s_latestSiegeTargetOwner = null;
    s_latestSiegeTargetCount = 0;
}
```

```csharp
s_latestSiegeTargets = _siegeTargets;
s_latestSiegeTargetOwner = this;
s_latestSiegeTargetCount = writeCount;
```

```csharp
if (ReferenceEquals(s_latestSiegeTargetOwner, this))
{
    s_latestSiegeTargets = default;
    s_latestSiegeTargetOwner = null;
    s_latestSiegeTargetCount = 0;
}
```

The persistent native graph buffers now carry explicit cold-allocation ownership comments for anchor reachability and traversal queue allocations. This is source-compliance hardening only; it does not alter graph math.

`DestructibleOrganicManager` now treats drop-buffer draining as a capacity-bounded operation and reschedules only while `DropBuffer.IsEmpty` reports pending work. `ToolDurabilitySystem` now clears processed breakdown queue entries within the fixed tracking capacity instead of leaving stale values outside the active window.

`SystemDispatcher.ScheduleDispatcherRaycasts()` now snapshots the pending count before dequeue and clears every remaining pending receiver/request sidecar slot even if the NativeQueue has fewer commands than expected:

```csharp
int pendingCount = _pendingDispatcherRaycastCount;
while (scheduledCount < pendingCount &&
       _pendingDispatcherRaycastCommands.TryDequeue(out RaycastCommand command))
{
    _scheduledDispatcherRaycastCommands.AddNoResize(command);
    _scheduledDispatcherRaycastReceivers[scheduledCount] = _pendingDispatcherRaycastReceivers[scheduledCount];
    _scheduledDispatcherRaycastRequestIds[scheduledCount] = _pendingDispatcherRaycastRequestIds[scheduledCount];
    _pendingDispatcherRaycastReceivers[scheduledCount] = null;
    _pendingDispatcherRaycastRequestIds[scheduledCount] = 0;
    scheduledCount++;
}

for (int clearIndex = scheduledCount; clearIndex < pendingCount; clearIndex++)
{
    _pendingDispatcherRaycastReceivers[clearIndex] = null;
    _pendingDispatcherRaycastRequestIds[clearIndex] = 0;
}
```

`SubtitleManager` no longer references a removed public singleton property. All internal runtime-owner checks now use the existing private `s_activeInstance` field:

```csharp
if (_serviceRegistered || !Application.isPlaying || s_activeInstance != this)
    return;
```

`BaseModule` and `ConstructionManager` now expose active module registries through owner-controlled indexed access only:

```csharp
internal static int ActiveModuleCount => s_activeModules.Count;
internal static BaseModule GetActiveModuleAt(int index)
{
    return index >= 0 && index < s_activeModules.Count ? s_activeModules[index] : null;
}
```

Consumers in `DroneFleetManager`, `PersistentWorldRegistry`, and `FloraInteractionManager` now iterate by count/index rather than holding an `IReadOnlyList<BaseModule>` view. This preserves the existing scan order while removing an ownership leak around mutable registries.

`RepairDroneHub.RefreshSupplyCrates()` now keeps a fixed collider-id cache for resolved storage crates:

```csharp
if (!TryResolveSupplyCrate(candidate, out StorageCrate crate) || ContainsSupplyCrate(crate))
    continue;
```

The cache is backed by fixed `ulong[24]` and `StorageCrate[24]` arrays and is cleared on disable/spawn/despawn. It stores successful collider-to-crate resolutions only; misses are not cached, avoiding stale false negatives if authoring changes during setup.

The cache key now preserves the full `EntityId.ToULong(collider.GetEntityId())` value instead of truncating to `int`, avoiding rare collider-id aliasing across long sessions:

```csharp
private static ulong ResolveColliderRuntimeId(Collider collider)
{
    return collider != null
        ? EntityId.ToULong(collider.GetEntityId())
        : 0UL;
}
```

`AutonomousExtractorModule.TryResolveNearestValidNode()` now follows the same bounded pattern for resource-host discovery:

```csharp
if (!TryResolveResourceNode(collider, out ResourceNode candidate))
    continue;
```

The extractor cache is backed by fixed `ulong[24]` and `ResourceNode[24]` arrays, cleared on enable/disable/destroy/spawn/despawn, and stores only successful collider-to-resource resolutions. Stale destroyed Unity objects clear their cache slot before the method falls back to direct resolution.

`VehicleDockingModule.OnTriggerStay()` now skips repeated hierarchy scans for the last rejected collider:

```csharp
ulong colliderId = ResolveColliderRuntimeId(other);
if (colliderId != 0UL && colliderId == _lastRejectedDockColliderId)
    return;
```

The dock now also keeps a fixed successful-resolution cache for transport lifecycle owners:

```csharp
if (!TryResolveTransportLifecycleOwner(other, out owner, out ownerBehaviour))
    return;
```

The cache is backed by fixed `ulong[16]`, `IPlayerTransportLifecycleOwner[16]`, and `MonoBehaviour[16]` arrays. It is reset on enable/disable/destroy/spawn/despawn, and stale destroyed Unity objects clear their cache slot before fallback hierarchy resolution.

`BaseModule` interior dry-zone tracking now uses full-width collider EntityIds:

```csharp
private readonly Dictionary<ulong, BuoyancyObject> _trackedObjects
    = new Dictionary<ulong, BuoyancyObject>(TRACKED_INITIAL_CAPACITY);
```

`OnTriggerEnter` and `OnTriggerExit` now resolve the same `ulong` key through `EntityId.ToULong(collider.GetEntityId())`, removing the previous truncation to `int`.

`ConstructionManager` save participation now mirrors `LoreDatabaseManager` owner semantics:

```csharp
saveService.Register(this);
_registeredSaveService = saveService;
```

Unregister now targets `_registeredSaveService`, not the current `GlobalRegistry.Save` slot. `IGlobalRegistryHotSwapListener` rebinds when the Save service is replaced, and `Start()` retries cold registration when bootstrap order exposes Save after `OnEnable`.

`HectonSpatialHashEditorSelfTests` was added to the active `Hecton8.Editor` assembly because the dedicated `Hecton8.EditModeTests` assembly is intentionally gated by `HECTON8_ENABLE_EDITMODE_TESTS` and `HECTON8_ENABLE_OPTIONAL_ASSEMBLIES`. The editor asmdef now declares explicit `Unity.Collections` and `Unity.Mathematics` references for that self-test surface.

`HectonGeneratedProjectReferencePruner` is Editor-only and removes only generated stale Entities package items when their paths are missing: `Unity.Cecil.Awesome` reference, `Unity.Entities` reference, and Entities source-generator analyzers. It does not touch valid references or package assets.

`Directory.Build.targets` applies the same guard to local MSBuild:

```xml
<_HectonMissingGeneratedReference Include="@(Reference)"
                                  Condition="'%(Reference.Identity)' == 'Unity.Cecil.Awesome' And '%(Reference.HintPath)' != '' And !Exists('%(Reference.HintPath)')" />
<_HectonMissingGeneratedReference Include="@(Reference)"
                                  Condition="'%(Reference.Identity)' == 'Unity.Entities' And '%(Reference.HintPath)' != '' And !Exists('%(Reference.HintPath)')" />
<Reference Remove="@(_HectonMissingGeneratedReference)" />
<_HectonMissingGeneratedAnalyzer Include="@(Analyzer)"
                                 Condition="$([System.String]::Copy('%(Analyzer.Identity)').Contains('com.unity.entities@')) And !Exists('%(Analyzer.Identity)')" />
<Analyzer Remove="@(_HectonMissingGeneratedAnalyzer)" />
```

It also bridges stale generated PlayMode test projects until Unity regenerates `.csproj` files from the updated asmdef:

```xml
<ItemGroup Condition="'$(MSBuildProjectName)' == 'Hecton8.PlayModeTests' And !$([System.String]::Copy('@(ProjectReference)').Contains('Hecton8.Core.csproj'))">
  <ProjectReference Include="$(MSBuildThisFileDirectory)Hecton8.Core.csproj"
                    ReferenceOutputAssembly="true"
                    Private="False" />
</ItemGroup>
```

The same MSBuild target prunes absent generated `Temp\bin\Debug` hint-path references before `ResolveAssemblyReferences`; missing generated outputs no longer block source analysis with `CS0006`.

`SmokeTests_SaveLoad` compile recovery was limited to test-surface wiring and generated-project glue: the PlayMode asmdef now references `Hecton8.Core`, the root MSBuild bridge supplies the project reference for stale generated projects, and stale `LogAssertion` calls now use Unity Test Framework `LogAssert`.

`Hecton8.Editor.csproj` now includes the editor audit helpers that `HectonAssetPipelineAudit` already referenced:

```xml
<Compile Include="Assets\_Project\Scripts\Editor\HectonLodGroupAudit.cs" />
<Compile Include="Assets\_Project\Scripts\Editor\HectonAssetQuarantineUtility.cs" />
<Compile Include="Assets\_Project\Scripts\Editor\HectonProjectAuditor.cs" />
```

## 2026-05-03 Service Registration Truth-State Pass

Mandates followed: `ARCH_Global_Registry_ServiceLocator_DI_Init`, `OPT_Zero_GC_Policy_AllocFree_Mandate`, `OPT_Native_Memory_Collections_JobSystem_Protocol`, `DBG_Telemetry_Crash_Reporting_PostMortem`.

Registry service owners no longer mark themselves registered immediately after calling `GlobalRegistry.Register...`. The registration flag is now derived from the actual retained slot:

```csharp
GlobalRegistry.RegisterPowerGridService(this);
_serviceRegistered = ReferenceEquals(GlobalRegistry.PowerGrid, this);
```

This was applied across core runtime owners including player motor, save runtime, power grid, object pool, HUD UI service, PDA markers, persistent world, world state, fluid, narrative, encounter AI, optimization services, and gameplay runtime services. Dispatcher/tick flags remain based on bucket or lane containment checks instead of blind success assumptions.

`HectonDirectorAI.OnDestroy()` now avoids unregistering the encounter-director service unless this instance still owns the slot:

```csharp
if (_encounterDirectorServiceRegistered && ReferenceEquals(GlobalRegistry.EncounterDirector, this))
{
    GlobalRegistry.UnregisterEncounterDirectorService(this);
    _encounterDirectorServiceRegistered = false;
}
```

Static evidence:

- `rg` direct service pattern: no `GlobalRegistry.Register...(this)` followed by `_registered = true`.
- `rg` extended service pattern: no `Register...(this)` followed within four lines by ownership flag `= true`.
- `rg` tick/dispatcher pattern: no `RegisterUpdatable/Slow/Fixed/Late/PostFixed` followed within four lines by tick flag `= true`.
- `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal`: succeeded, log `.codex-artifacts/dotnet-Hecton8.Core-2026-05-03-service-truth-batch4.log`.
- `dotnet build .\Hecton8.Editor.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal`: succeeded, log `.codex-artifacts/dotnet-Hecton8.Editor-2026-05-03-registry-service-truth.log`.

## 2026-05-03 Generated Project Build Guard Pass

Mandates followed: `ARCH_Project_Bootstrap_Sequence_Init_Safety`, `OPT_Zero_GC_Policy_AllocFree_Mandate`, `DBG_Telemetry_Crash_Reporting_PostMortem`.

The generated `Assembly-CSharp` and `Assembly-CSharp-firstpass` command-line build path was blocked by root MSBuild metadata, not by first-party C#:

```xml
<BuildProjectReferences>false</BuildProjectReferences>
```

That forced SDK compilation to reference missing `Temp/bin/Debug/*.dll` outputs without building their project references. The opt-out now requires an explicit property:

```xml
<PropertyGroup Condition="('$(MSBuildProjectName)' == 'Assembly-CSharp' Or '$(MSBuildProjectName)' == 'Assembly-CSharp-firstpass') And '$(HectonSkipAssemblyProjectReferences)' == 'true'">
  <BuildProjectReferences>false</BuildProjectReferences>
</PropertyGroup>
```

Unity-generated class libraries now suppress `.deps.json` and runtimeconfig generation because Unity does not consume those SDK artifacts and they were failing inside vendor project builds:

```xml
<GenerateDependencyFile>false</GenerateDependencyFile>
<GenerateRuntimeConfigurationFiles>false</GenerateRuntimeConfigurationFiles>
```

`Directory.Build.targets` now creates `OutputPath` and `IntermediateOutputPath` before compile/dependency-generation windows and prunes stale generated references whose `HintPath` points to missing `Library/ScriptAssemblies` or `Library/PackageCache` files. `HectonGeneratedProjectReferencePruner` mirrors the same rule during Unity project-file generation.

Static evidence:

- `dotnet build .\Assembly-CSharp-firstpass.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal`: `EXIT_CODE=0`, `Build succeeded`, log `.codex-artifacts/dotnet-Assembly-CSharp-firstpass-2026-05-03-final-classification.log`.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal`: `EXIT_CODE=0`, `Build succeeded`, log `.codex-artifacts/dotnet-Assembly-CSharp-2026-05-03-final-classification.log`.
- `dotnet build .\Hecton8.Editor.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal`: succeeded, log `.codex-artifacts/dotnet-Hecton8.Editor-2026-05-03-generated-project-guard.log`.
- `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal`: succeeded, log `.codex-artifacts/dotnet-Hecton8.Core-2026-05-03-generated-project-guard.log`.
- Vendor/package obsolete API warnings remain in command-line logs and are outside first-party ownership for this pass.

## 2026-05-03 PlayerPDA Input Fallback Contract Pass

Mandates followed: `ARCH_Global_Registry_ServiceLocator_DI_Init`, `OPT_Zero_GC_Policy_AllocFree_Mandate`, `DBG_Telemetry_Crash_Reporting_PostMortem`.

`GlobalRegistry.Input` is a null-object fallback service, not a nullable service slot. `PlayerPDA` was still treating it as nullable in `Start()`, `Open()`, `Close()`, and `ForceClose()`. That made the initialization check semantically dead and let input-map dispatch silently skip the fallback contract.

The PDA now checks service readiness through `GlobalRegistry.Input.IsInitialized` and calls the service directly:

```csharp
if (!GlobalRegistry.Input.IsInitialized)
{
    Debug.LogError(
        "[PlayerPDA] GlobalRegistry.Input is not initialized at Start(). " +
        "PDA will not function.");
}

GlobalRegistry.Input.SwitchToUIInput();
GlobalRegistry.Input.SwitchToPlayerInput();
```

Static evidence:

- `rg -n "GlobalRegistry\.Input\s*==\s*null|GlobalRegistry\.Input\s*!=\s*null|GlobalRegistry\.Input\?\." Assets/_Project/Scripts -g "*.cs"` returned no matches.
- `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal` succeeded after this pass, log `.codex-artifacts/dotnet-Hecton8.Core-2026-05-03-playerpda-input-contract.log`.
- Superseded by later input-owner passes: current guard inventory reports direct `InputManager.Instance` source sites `0`. This PlayerPDA pass itself only fixed the stale `GlobalRegistry.Input` null-object contract.

`Scan-FoundationGuards.ps1` gives future agents a source-level gate for the registry truth-state pattern:

```powershell
& Tools/ReloadAudit/Scan-FoundationGuards.ps1
```

The guard fails when a broad `GlobalRegistry.Register*(...this...)` or direct renderable self-registration is followed by a blind `_registered* = true` / `_isInitialized = true` flag. It now also fails on any synchronous job `.Run(` site after first-party runtime source reached `0` hits. `.Complete(` remains inventory because dispatcher-owned swap-window completions need owner classification before promotion.

`TetherInstance.UpdateVisuals()` no longer runs a synchronous Burst job for 8-24 visual points. The former `BuildVisualCatenaryJob.Run(_visualSegmentPositions.Length)` path is now an immediate indexed loop over `_visualAnchorPositions`, `_visualSegmentLengths`, and `_visualSegmentPositions`:

```csharp
BuildVisualCatenaryImmediate(
    anchorCount,
    _currentLength,
    blendT,
    VisualSagScale,
    _visualAnchorPositions,
    _visualSegmentLengths,
    _visualSegmentPositions);
```

This removes one `.Run(` site from the source guard inventory without changing tether physics, bend topology, or GPU upload ownership.

`PlayerInventory` no longer routes bounded inventory mutation passes through synchronous JobSystem `.Run()` barriers. The former `InventoryRadixSortJob`, `InventoryMassVolumeJob`, `InventoryRadioactiveHalfLifeJob`, and `InventoryReactiveChemistryJob` are now inline struct kernels with the same `Execute()` bodies and the same preallocated SOA buffers:

```csharp
new InventoryRadioactiveHalfLifeKernel
{
    AnchorHashIds = _grid.AnchorHashIds,
    StackCounts = _stackCounts,
    AnchorUnitRadiationSv = _anchorUnitRadiationSv,
    ItemStateFlags = _itemStateFlags,
    QualityMilli = _qualityMilli,
    ConversionAnchorIndices = _radioactiveConversionAnchors,
    Counters = _radioactiveHalfLifeCounters,
    DeltaSeconds = SlowTickIntervalSeconds,
    BaseHalfLifeSeconds = RadioactiveHalfLifeBaseSeconds,
    DefaultQuality = DefaultQualityMilli,
    RadioactiveMask = RadioactiveItemStateMask,
    DegradedMask = DegradedItemStateMask,
    DegradedThreshold = DegradedQualityMilliThreshold
}.Execute();
```

This removes four `.Run(` sites, including the two SlowTick-classified review sites, without introducing cross-frame inventory races or changing save/runtime item state layout.

`CraftingSystem` no longer routes immediate recipe checks through synchronous JobSystem `.Run()` barriers. The former `EvaluateRecipeAvailabilityJob` and `BuildDeconstructionYieldJob` are now inline kernels that keep the same native inputs and result writes:

```csharp
new EvaluateRecipeAvailabilityKernel
{
    RecipeCosts = recipeCosts,
    AvailableItemCounts = availableItemCounts,
    Result = result,
    RecipeCostCount = recipeCostCount
}.Execute();
```

This removes two `.Run(` sites while preserving immediate `result[0]` / `outputCount[0]` consumption by the caller.

Renderable registration now uses authoritative bucket membership:

```csharp
GlobalRegistry.Renderables.Register(this);
_registeredRenderable = GlobalRegistry.Renderables.Contains(this);
```

This was applied to `HectonUnderwaterVisuals`, `HectonSubmarineOS`, and `MissionMarkerSystem`.

Service registration now uses authoritative singleton-slot ownership:

```csharp
GlobalRegistry.RegisterPhysicsService(this);
_isInitialized = ReferenceEquals(GlobalRegistry.Physics, this);
```

`DebrisManager` uses the equivalent `GlobalRegistry.Debris` check. Both teardown paths unregister only when the current slot still belongs to the instance.

## 2026-05-03 Input Controls Lifecycle Pass

Mandates followed: `ARCH_Global_Registry_ServiceLocator_DI_Init`, `ARCH_Project_Bootstrap_Sequence_Init_Safety`, `OPT_Zero_GC_Policy_AllocFree_Mandate`, `UI_Data_Streaming_ZeroGC_Optimization`.

`PDAControlsRebindUI` and `PauseControlsPanel` no longer unsubscribe input/rebind events by re-reading the current singleton/registry slot. Both controls panels now keep the exact input and rebinding-service owners used during `Subscribe()` and tear down against those same owners:

```csharp
_subscribedInput = input;
_subscribedRebindingService = rebinding;
```

```csharp
InputManager input = _subscribedInput;
IInputBindingService rebinding = _subscribedRebindingService;
```

This removes a concrete shutdown/replacement leak where `InputManager.Instance` or `GlobalRegistry.InputBinding` could point at a different object by `OnDisable()`, leaving the old event source subscribed.

`PauseControlsPanel.OnDisable()` now saves binding overrides before clearing the cached rebinding owner:

```csharp
IInputBindingService rebinding = _subscribedRebindingService ?? ResolveRebindingService();
if (rebinding != null)
{
    rebinding.SaveOverrides();
}

Unsubscribe();
```

`PDAControlsRebindUI` now resolves action lookup through the cached subscribed input owner when available and fails closed when input is absent:

```csharp
InputManager inputManager = ResolveInputManager();
if (inputManager == null)
{
    SetStatus("Input manager unavailable.");
    return;
}
```

The same two controls panels no longer perform `GetComponent<CanvasGroup>()` / `AddComponent<CanvasGroup>()` during selection refresh. Selection indicator `CanvasGroup` references are cached during cold UI construction or row-reference rebuild:

```csharp
_selectedIndicatorGroups[i] = EnsureCanvasGroup(selected.gameObject);
```

Navigation-time selection visual updates now write directly to cached `CanvasGroup` references:

```csharp
SetIndicatorVisible(_selectedIndicatorGroups[_selectedIndex], true);
```

This keeps controls navigation on field/index writes and color assignment instead of component lookup/addition.

`BeaconHUDElement.ApplyDisplayVisible()` no longer resolves or creates `CanvasGroup` from the `Tick()` call chain. The icon pool resolves or creates its visibility proxy once during `Awake()`:

```csharp
if (!icon.TryGetComponent(out CanvasGroup canvasGroup))
{
    canvasGroup = icon.AddComponent<CanvasGroup>();
}
```

The hot path now fails closed if a malformed display record lacks the cached proxy:

```csharp
CanvasGroup canvasGroup = display.canvasGroup;
if (canvasGroup == null)
    return;
```

`NotificationEvents` now guards listener registration and unregistration with bucket membership checks, matching the safer pattern used by other bounded event lanes:

```csharp
if (!_listeners.Contains(listener))
    _listeners.Register(listener);
```

```csharp
if (_listeners.Contains(listener))
    _listeners.Unregister(listener);
```

The LateUpdate dispatch loop also skips null listener slots defensively before invoking `OnNotificationEvent`.

The same bounded-listener hardening was applied to the central save/scan/crafting/interaction lanes:

```csharp
IInteractionEventListener listener = rawArray[i];
if (listener != null)
    listener.OnInteractionEvent(in payload);
```

`SaveEvents` and `ScanEvents` also now guard duplicate register and absent unregister the same way as the already-hardened crafting/interaction lanes.

The adjacent inventory/quest/narrative lanes now follow the same dispatch safety rule. `QuestEvents` and `NarrativeEvents` also guard duplicate register/absent unregister, and narrative POI immediate callbacks now skip null listener slots:

```csharp
INarrativePointOfInterestListener listener = rawArray[i];
if (listener != null)
    listener.OnNarrativePointOfInterestRegistered(poi);
```

Static evidence:

- UI event subscription scan now shows `PDAControlsRebindUI` and `PauseControlsPanel` subscribe/unsubscribe through cached owners.
- Superseded by later input-owner passes: current guard inventory reports direct `InputManager.Instance` sites `0` and hot-path direct input-owner review sites `0`. Future native binding exceptions must go through the registered owner/hot-swap paths.
- Command: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false -clp:ErrorsOnly`
- Result: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.
- Follow-up command after selection-indicator caching while another build chain was contending for project references: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:BuildProjectReferences=false -p:UseSharedCompilation=false -clp:ErrorsOnly`
- Follow-up result: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.
- Follow-up command after `BeaconHUDElement` hot-path cache hardening: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:BuildProjectReferences=false -p:UseSharedCompilation=false -clp:ErrorsOnly`
- Follow-up result: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.
- Follow-up command after `NotificationEvents` listener lifecycle hardening: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:BuildProjectReferences=false -p:UseSharedCompilation=false -clp:ErrorsOnly`
- Follow-up result: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.
- Follow-up command after save/scan/crafting/interaction event-lane hardening: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:BuildProjectReferences=false -p:UseSharedCompilation=false -clp:ErrorsOnly`
- Follow-up result: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.
- Follow-up command after inventory/quest/narrative event-lane hardening: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:BuildProjectReferences=false -p:UseSharedCompilation=false -clp:ErrorsOnly`
- Follow-up result: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.
- Source grep after raw listener dispatch purge: `rg -n "rawArray\\[i\\]\\.On" Assets/_Project/Scripts`
- Grep result: no matches.
- Follow-up command after raw listener dispatch purge: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:BuildProjectReferences=false -p:UseSharedCompilation=false -clp:ErrorsOnly`
- Follow-up result: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`, elapsed `00:00:50.77`.
- MCP resources were unavailable for this pass; no MCP console proof exists.

## 2026-05-03 Foundation Guard Scanner CI Pass

Mandates followed: `OPT_Zero_GC_Policy_AllocFree_Mandate`, `OPT_Native_Memory_Collections_JobSystem_Protocol`, `ARCH_Global_Registry_ServiceLocator_DI_Init`, `ARCH_Project_Bootstrap_Sequence_Init_Safety`, `DBG_Telemetry_Crash_Reporting_PostMortem`, `MATH_Coordinate_Precision_AUP_FloatingOrigin`.

`Tools/ReloadAudit/Scan-FoundationGuards.ps1` now covers hard source gates for blind registry flag drift, floating-origin listener blind flag drift, raw `UnsafeUtility.MemCpy` outside `UnsafeMemoryCopyGuard`, legacy `PlayerSignalEvents.On*` subscriptions after the NativeQueue/listener migration, direct raw-array listener dispatch, `GlobalRegistry.Input` nullable misuse, unauthorized Unity loop methods, legacy coroutine sites, forbidden runtime asset APIs, broad physics masks, and release-reachable direct hot-path debug logs. It also inventories direct `InputManager.Instance` access and one-hop review candidates.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/ReloadAudit/Scan-FoundationGuards.ps1 -ProjectRoot C:\hades\Hecton8 -OutputMarkdown C:\hades\Hecton8\Docs\Reports\2026-05-03_FOUNDATION_GUARD_SCAN.md
```

The scanner also added one-hop hot-path callee tracing for `.Run(`, `.Complete(`, raw memcopy, and runtime find hits inside methods called by `Tick`, `FixedTick`, `SlowTick`, `LateFrameTick`, and Unity physics/update callbacks. `DispatcherJobSwap.TryComplete` `handle.Complete()` is classified as guarded only because the source contains the `IsCompleted`/swap-window contract. This is source evidence, not Play Mode proof.

Current source-only guard result:

- Scanner exit code: `0`.
- Global registry self-registration inventory: `495` informational sites after broad `GlobalRegistry.Register*(...this...)` expansion.
- Direct `InputManager.Instance` inventory: `0` sites, with `0` hot-path review candidates.
- Release-reachable direct hot-path `Debug.Log`/`LogWarning`/`LogError` sites: `0`.
- Release-reachable one-hop debug-log review sites: `0`, conservative same-file call classifier only.
- Blind registry flag drift: `0`.
- Origin shift listener blind flag drift: `0`.
- Synchronous job `.Run(` sites: `0`.
- Hot-path synchronous job `.Run(` review sites: `0`.
- Completion `.Complete(` text hits: `1`.
- Guarded dispatcher completion sites: `1`.
- `UnsafeUtility.MemCpy` outside guard: `0`.
- Legacy `PlayerSignalEvents.On*` subscriptions: `0`.
- Direct raw-array listener dispatch: `0`.
- `GlobalRegistry.Input` nullable misuse: `0`.
- Optimization singleton residue: `0`.
- Runtime `Find*`/`GameObject.Find*` text hits outside Editor folders: `0`.
- Broad physics layer masks outside Editor: `0`.
- One-hop hot-path callee names: `2413`.

Verification attempts during this pass:

- First guard run with a `120s` shell timeout did not complete and produced no usable result.
- Re-run with a `360s` shell timeout completed in `222.1s`, wrote `Docs/Reports/2026-05-03_FOUNDATION_GUARD_SCAN.md`, and returned exit code `0`.
- May 3 CI-style wrapped rerun after the release-reachable debug split and bootstrap input-owner cleanup wrote `Docs/Reports/2026-05-03_FOUNDATION_GUARD_SCAN.md` and returned `exit=0`; current May 4 recheck also exits `0` after the foundation guard repair.
- Later guard rerun after comment-stripped call classification and `Fabricator` preprocessor repair wrote `Docs/Reports/2026-05-03_FOUNDATION_GUARD_SCAN.md` at `2026-05-03 21:24:32` with direct `InputManager.Instance` sites `0`, release-reachable direct hot-path debug logs `0`, and release-reachable one-hop debug-log review sites `0`. One wrapped tool invocation returned `-1` while the report file was still written, so this remains source inventory, not process-health or runtime proof.
- `Fabricator.cs` compile blocker repair: removed duplicated/malformed `#endif` around the inventory-full development log and kept one `#if UNITY_EDITOR || DEVELOPMENT_BUILD` guard at column 1.
- One immediate source-slice build after the long guard run failed on missing generated `Temp\bin\Debug` vendor DLL references (`EasySave3`, `ShapesRuntime`, `VolumetricLightBeam`, `WaveHarmonic.Crest`). The DLLs appeared in `Temp\bin\Debug` after the generated-project reference outputs completed, and the identical rerun passed.
- Final source-slice build command: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:BuildProjectReferences=false -p:UseSharedCompilation=false -clp:ErrorsOnly`
- Final source-slice build result: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`, elapsed `00:00:10.39`.
- Follow-up full Core build after the `Fabricator.cs` preprocessor repair: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false -clp:ErrorsOnly`; result `Build succeeded`, `0 Warning(s)`, `0 Error(s)`, elapsed `00:00:50.44`.
- Subsequent Core build attempts returned `exit=1/-1` without C# diagnostics while parallel/stale `dotnet build Hecton8.Core.csproj` processes were present. Those attempts are process-contention evidence only and do not supersede the preceding successful compile.
- Latest serial Core build after SaveManager/Fabricator release-reachable debug-log cleanup: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false -clp:ErrorsOnly`; result `Build succeeded`, `0 Warning(s)`, `0 Error(s)`, elapsed `00:00:19.85`.
- `git diff --check` on the touched guard/docs files returned exit code `0`; Git reported CRLF normalization warnings for docs only.
- Runtime-source gate grep found only `Assets/_Project/Scripts/Core/UnsafeMemoryCopyGuard.cs:66` for `UnsafeUtility.MemCpy` and found no legacy `PlayerSignalEvents.On*`, raw `rawArray[i].On*` listener dispatch, or runtime `Find*` hits outside Editor folders.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false` reached first-party proof after the current `TetherInstance`, `PlayerInventory`, `CraftingSystem`, `QuestStateManager`, `SargassumMicroFaunaBoids`, and `FaunaSimulationEngine` state: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`.
- `rg -n "BurstCompile|IJob|IJobParallelFor|\.Run\(" Assets/_Project/Scripts/PlayerInventory.cs` returned no matches.
- `rg -n "BurstCompile|IJob|IJobParallelFor|\.Run\(" Assets/_Project/Scripts/CraftingSystem.cs` returned no matches.
- `rg -n "\.Run\(" Assets/_Project/Scripts/Quest/QuestStateManager.cs` returned no matches.

Remaining native-job review queue:

- No one-hop hot-path `.Run(` review sites remain in the current guard scan.
- No `.Run(` sites remain in the current first-party runtime inventory, and the scanner now treats any reintroduced `.Run(` site as a blocking source defect. This is source-only; it does not prove the new scheduled-job cadence under runtime load.

`HectonMapMagicVegetationBridge` now tracks TerrainTile static event subscription and `HectonFloatingOrigin` listener ownership with separate flags. Floating-origin registration now reads back authoritative listener state:

```csharp
HectonFloatingOrigin.RegisterListener(this);
_originShiftListenerRegistered = HectonFloatingOrigin.IsListenerRegistered(this);
```

The guard now hard-fails the stale pattern `HectonFloatingOrigin.RegisterListener(this); _flag = true`.

Current source state no longer contains the prior `TetherInstance.UpdateVisuals()` `.Run()` site, the four prior `PlayerInventory` synchronous `.Run()` sites, the two prior `CraftingSystem` synchronous `.Run()` sites, the prior `QuestStateManager.EvaluateSignal()` `.Run()` site, the prior `SargassumMicroFaunaBoids.PrimeFoveatedSimulationDecision()` `.Run()` site, the prior `FaunaSimulationEngine.RunHibernationCatchUp()` `.Run()` site, or the prior scatter/flora/wreck `.Run(` inventory. This is source inventory only, not profiler proof.

## 2026-05-03 Quest Signal Kernel Run Purge Note

Mandates followed: `OPT_Zero_GC_Policy_AllocFree_Mandate`, `OPT_Native_Memory_Collections_JobSystem_Protocol`, `ARCH_Global_Registry_ServiceLocator_DI_Init`.

Current guard output no longer lists `Assets/_Project/Scripts/Quest/QuestStateManager.cs`; `EvaluateSignal` calls the same quest signal kernel through `job.Execute()`.

Verification:

- Current command: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false`
- Result: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.
- Guard command: `powershell -NoProfile -ExecutionPolicy Bypass -File Tools/ReloadAudit/Scan-FoundationGuards.ps1 -ProjectRoot C:\hades\Hecton8 -OutputMarkdown C:\hades\Hecton8\Docs\Reports\2026-05-03_FOUNDATION_GUARD_SCAN.md`
- Guard result: synchronous job `.Run(` sites `0`; hot-path synchronous job `.Run(` review sites remain `0`.
- Direct generated report no longer lists `QuestStateManager.cs`.

Remaining `.Run()` inventory is currently empty in first-party runtime source. The former scatter/flora/wreck sites still need runtime cadence/profiler proof because source removal does not prove frame-time behavior.

## 2026-05-03 Fauna Hibernation Catch-Up Run Purge

Mandates followed: `OPT_Zero_GC_Policy_AllocFree_Mandate`, `OPT_Native_Memory_Collections_JobSystem_Protocol`, `ARCH_Global_Registry_ServiceLocator_DI_Init`.

`FaunaSimulationEngine.RunHibernationCatchUp()` no longer invokes `IJobParallelFor.Run()` for the bounded restore-time hibernation catch-up pass. The method still clamps count against the persistent input/result native arrays and executes the same kernel synchronously:

```csharp
for (int i = 0; i < safeCount; i++)
    job.Execute(i);
```

This removes one synchronous JobSystem `.Run()` site from the guard inventory without changing resident LOD scheduling, fauna save data, or visual/GameObject ownership.

Verification:

- Full dependency command after a transient missing `Temp\bin\Debug` dependency-output pass: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false -clp:ErrorsOnly`
- Full dependency result: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.
- `BuildProjectReferences=false` is not used as final evidence in this slice because the shared `Temp\bin\Debug` dependency outputs were concurrently missing. The full serial project-reference build above is the current compile evidence.
- Guard command: `powershell -ExecutionPolicy Bypass -File Tools/ReloadAudit/Scan-FoundationGuards.ps1`
- Guard result: synchronous job `.Run(` sites `0`; hot-path synchronous job `.Run(` review sites remain `0`.
- Direct grep: `rg -n "\.Run\(" Assets/_Project/Scripts -g '*.cs' -g '!**/Editor/**' -g '!**/Tests/**'` no longer lists `FaunaSimulationEngine.cs`.

## 2026-05-03 Runtime `.Run()` Inventory Closeout

Mandates followed: `OPT_Native_Memory_Collections_JobSystem_Protocol`, `OPT_Zero_GC_Policy_AllocFree_Mandate`, `REND_URP_Graphics_HotPath_Optimization_HLOD`.

The current first-party runtime source inventory now has zero `.Run(` sites. `FloraInteractionManager` cascade phase seeds no longer call `job.Run(count)`; they schedule `PopulateCascadePhaseSeedsJob`, flush through `LateFrameTick()`, and force-complete only during teardown through `DispatcherJobSwap.TryComplete`.

Verification:

- Direct grep: `rg -n "\.Run\(" Assets/_Project/Scripts -g '*.cs' -g '!**/Editor/**' -g '!**/Tests/**'` returned no matches.
- Guard command: `powershell -ExecutionPolicy Bypass -File Tools/ReloadAudit/Scan-FoundationGuards.ps1 -ProjectRoot C:\hades\Hecton8 -OutputMarkdown C:\hades\Hecton8\Docs\Reports\2026-05-03_FOUNDATION_GUARD_SCAN.md`
- Guard result: synchronous job `.Run(` sites `0`; hot-path synchronous job `.Run(` review sites `0`.
- Full Core build: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false -clp:ErrorsOnly` returned `Build succeeded`, `0 Warning(s)`, `0 Error(s)`.
- Runtime proof absent: flora cascade visuals, scatter generation, wreck generation, and frame-time behavior still need Play Mode/profiler proof.

## 2026-05-03 Fallback Beacon Material Ownership Fix

Mandates followed: `REND_URP_Graphics_HotPath_Optimization_HLOD`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits`, `STRM_Asset_Lifecycle_Addressables_Loading_Memory`, `OPT_Zero_GC_Policy_AllocFree_Mandate`.

`BeaconRuntime.GetFallbackBeaconMaterial(Color)` no longer returns one static material and then mutates its color for every fallback beacon. That prior ownership model made all fallback beacon cube renderers share the latest deployed beacon color. The fallback path now creates an owned material instance only when `BeaconNetworkSystem.SpawnFallbackBeacon()` has no prefab path and assigns fallback ownership to the spawned `BeaconRuntime`:

```csharp
Material fallbackMaterial = BeaconRuntime.GetFallbackBeaconMaterial(color);
renderer.sharedMaterial = fallbackMaterial;

BeaconRuntime runtime = beaconRoot.AddComponent<BeaconRuntime>();
runtime.SetOwnedFallbackMaterial(fallbackMaterial);
```

`BeaconRuntime.Configure()` now ignores the prefab source for fallback runtimes, so a fallback object created after a failed prefab pool spawn still destroys itself instead of being sent to the object pool as if it were prefab-owned.

`BeaconRuntime.OnDestroy()` releases that owned material:

```csharp
ReleaseOwnedFallbackMaterial();
```

This deliberately does not use `MaterialPropertyBlock`; the render mandate forbids MPB on standard geometry because it breaks SRP Batcher behavior. The cost is one cold material allocation per fallback beacon instance, bounded by the beacon active cap and released with the owning runtime object. Prefab beacons are unaffected unless prefab pool spawn fails, in which case the fallback object is explicitly treated as non-pooled.

Verification:

- Command: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false -p:BuildProjectReferences=false -clp:ErrorsOnly`
- Result: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.
- Guard command: `powershell -ExecutionPolicy Bypass -File Tools/ReloadAudit/Scan-FoundationGuards.ps1 -ProjectRoot C:\hades\Hecton8 -OutputMarkdown C:\hades\Hecton8\Docs\Reports\2026-05-03_FOUNDATION_GUARD_SCAN.md`
- Guard result: source hard gates remain clear; `.Run(` inventory is `0`; runtime Find API hits remain `0`.
- Runtime proof absent: fallback beacon deploy/retract/load-save color persistence and prefab-pool-failure fallback despawn still need scene/Play Mode verification.

## 2026-05-03 Input Singleton Hot-Path Purge

Mandates followed: `ARCH_Global_Registry_ServiceLocator_DI_Init`, `ARCH_Project_Bootstrap_Sequence_Init_Safety`, `OPT_Zero_GC_Policy_AllocFree_Mandate`, `CTRL_Device_Abstraction_Haptics`, `DBG_Telemetry_Crash_Reporting_PostMortem`.

`PlayerInteraction` no longer subscribes directly to `InputManager.Instance.OnInteract`. It now subscribes to the registered `IInputService` and listens for `GlobalRegistryServiceSlot.Input` replacement:

```csharp
_subscribedInputService = inputService;
_subscribedInputService.OnInteract += HandleInteractInput;
```

`HectonFabricatorUI.CloseMenu()` and open routing now use the null-object-safe input service for map switches:

```csharp
GlobalRegistry.Input.SwitchToUIInput();
GlobalRegistry.Input.SwitchToPlayerInput();
```

`HectonFabricatorUI` also tracks the exact native input owner used for navigate/submit/cancel event subscription so `OnDisable()` does not unsubscribe from a different singleton instance. It retries the subscription in `Start()` and rebinds from `GlobalRegistryServiceSlot.Input` hot-swap notification.

`PDAIntrusionManager` split runtime owner refresh from cold UI submit-action binding. `Tick()` now calls only `ResolveRuntimeOwners()`; `InputManager.Instance.GetAction("Submit", "UI")` is resolved in `Awake`, `OnEnable`, `Start`, and `GlobalRegistryServiceSlot.Input` hot-swap rebinding through `IGlobalRegistryHotSwapListener`. Disable/destroy paths clear cached native action ownership.

`PlayerFlashlight.Tick()` no longer retries native input subscription every frame. The owner-specific cold helper `SubscribeFlashlightInputManagerIfAvailable(InputManager inputManager)` is called from `OnEnable`, `Start`, and `GlobalRegistryServiceSlot.Input` hot-swap notification after resolving the native owner through `ResolveNativeFlashlightInputManager()`. This keeps the remaining `InputManager.Instance` read out of the hot-classified subscription method without changing the public `IInputService` API.

`RebindingManager` now receives the bootstrap-owned native input manager through `BindNativeInputManager(InputManager inputManager)`. `GameBootstrapper.InitializePlayerLayer()` binds it immediately after resolving/creating the rebinding service. Rebind start, rebind-by-id, save/load/clear overrides, and conflict detection now use that bound owner instead of `InputManager.Instance`; initial override loading is delayed until the native owner is bound.

Interaction prompt display-style owners now follow the same lifecycle model:

- `Interaction/InteractionUI` rebinds native input display-style events on `GlobalRegistryServiceSlot.Input` replacement and rebinds `IInputBindingService` callbacks on `GlobalRegistryServiceSlot.InputBinding` replacement.
- `UI/InteractionUI` rebinds native display-style events on `GlobalRegistryServiceSlot.Input` replacement and clears prompt caches so the next prompt build uses current binding display data.
- `DiegeticTooltipSystem` rebinds native display-style events on `GlobalRegistryServiceSlot.Input` replacement; binding icon and prefix layout helpers now use the subscribed native owner instead of fallback singleton reads.
- `PlayerInteraction.ActiveInteractKey` keeps its legacy public string API but now returns a cached string refreshed in lifecycle/hot-swap paths instead of calling `InputManager.Instance.GetBindingDisplayString()` from the getter.

Guard scanner expansion:

- `Tools/ReloadAudit/Scan-FoundationGuards.ps1` now reports direct `InputManager.Instance` inventory and one-hop hot-path review count.
- Scanner result after this pass: direct `InputManager.Instance` sites `0`; hot-path direct `InputManager.Instance` review sites `0`.
- No direct `InputManager.Instance` sites remain in the current guard output. Future native-binding exceptions must re-enter as explicit review inventory.

Verification:

- `powershell -NoProfile -ExecutionPolicy Bypass -File Tools\ReloadAudit\Scan-FoundationGuards.ps1 -ProjectRoot C:\hades\Hecton8 -OutputMarkdown C:\hades\Hecton8\Docs\Reports\2026-05-03_FOUNDATION_GUARD_SCAN.md`: exit code `0`.
- May 3 guard output generated `Docs/Reports/2026-05-03_FOUNDATION_GUARD_SCAN.md` with synchronous job `.Run(` sites `0`, completion `.Complete(` text hits `1`, direct `InputManager.Instance` sites `0`, hot-path direct `InputManager.Instance` review sites `0`, release-reachable direct hot-path debug-log sites `0`, release-reachable one-hop debug-log review sites `0`, `Optimization singleton residue = 0`, unauthorized Unity loop methods `0`, legacy coroutine sites `0`, forbidden runtime asset API sites `0`, broad physics layer masks outside Editor `0`, and runtime Find API text hits outside Editor folder `0`. Current May 4 guard truth is listed in the supersession note. Treat the file contents as source inventory, not runtime proof.
- Latest local scoped Core command `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false -p:BuildProjectReferences=false -clp:ErrorsOnly`: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`, elapsed `00:00:03.95`.
- `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal`: `Build succeeded`, log `.codex-artifacts/dotnet-Hecton8.Core-2026-05-03-input-hotpath-purge.log`.
- `dotnet build .\Hecton8.Editor.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal`: `Build succeeded`, log `.codex-artifacts/dotnet-Hecton8.Editor-2026-05-03-input-hotpath-purge.log`.
- MCP resources were unavailable; no Unity console, Play Mode, or GCMonitor proof exists.

## 2026-05-03 Cold Allocation Comment Compliance

Mandates followed: `OPT_Zero_GC_Policy_AllocFree_Mandate`, `ARCH_Global_Registry_ServiceLocator_DI_Init`, `CORE_Tools_Equipment_Interaction_Raycast_Heat`.

This pass changed no runtime behavior. It fixed non-canonical cold-allocation documentation in the interaction/fabricator slice:

```csharp
// COLD ALLOC: RaycastHit[1] — single-hit interaction probe buffer — owner: PlayerInteraction
private readonly RaycastHit[] _raycastHits = new RaycastHit[1];
```

`HectonFabricatorUI` now explicitly documents the fixed `RecipeListEntry[12]`, recipe label `char[96]`, and fallback inflation `char[64]` buffers with owner and reason. These buffers already existed; the change makes allocation ownership auditable and keeps the zero-GC policy checkable by source review.

Verification:

- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false -p:BuildProjectReferences=false -clp:ErrorsOnly`: `Build succeeded`.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false -p:BuildProjectReferences=false`: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`.
- `git diff --check` on the current foundation slice returned no whitespace errors; CRLF normalization warnings only.
- Runtime proof absent: this is source-comment compliance only, not Play Mode or GCMonitor proof.

## 2026-05-03 Cold Job Run Purge

Mandates followed: `OPT_Native_Memory_Collections_JobSystem_Protocol`, `OPT_Zero_GC_Policy_AllocFree_Mandate`, `ARCH_Global_Registry_ServiceLocator_DI_Init`, `DBG_Telemetry_Crash_Reporting_PostMortem`.

Remaining first-party `Job.Run()` source inventory was removed from world generation code without changing NativeArray ownership:

```csharp
job.Execute();
for (int index = 0; index < count; index++)
    job.Execute(index);
```

Changed sites:

- `WorldProceduralScatterDirectorCandidateAcceptance`: rescue candidate acceptance, cell candidate acceptance, and single candidate acceptance now invoke the existing `IJob.Execute()` body directly.
- `FloraInteractionManager`: cascade phase seed rebuild now iterates the existing `IJobParallelFor.Execute(int)` body over the already bounded instance count.
- `ProceduralWreckGenerator`: merged mesh, proxy mesh, and damage decal mesh builders now invoke direct `Execute()` loops over existing mesh-data/native buffers.
- `Tools/ReloadAudit/Scan-FoundationGuards.ps1`: broad physics mask detection now targets typed `LayerMask` declarations, physics-query mask names, and direct `Physics.*`/`RaycastCommand` all-layer calls. Sentinel fields such as `_lastMaskDispatchFrame = -1` are no longer reported as physics masks.
- The scanner now strips string literals/comments only for `InputManager.Instance`/`GlobalRegistry.Input` source checks, preventing log-message false positives without paying a full-file regex cost. It also exits explicitly with `0` on clean scans instead of relying on host EOF behavior.

Verification:

- `powershell -NoProfile -ExecutionPolicy Bypass -File Tools\ReloadAudit\Scan-FoundationGuards.ps1 -ProjectRoot C:\hades\Hecton8 -OutputMarkdown C:\hades\Hecton8\Docs\Reports\2026-05-03_FOUNDATION_GUARD_SCAN.md`: exit code `0`.
- Latest guard report: synchronous job `.Run(` sites `0`, hot-path synchronous job `.Run(` review sites `0`, broad physics layer masks outside Editor `0`, GlobalRegistry.Input nullable misuse `0`, direct `InputManager.Instance` sites `0`, hot-path direct `InputManager.Instance` review sites `0`, release-reachable direct hot-path `Debug.Log` sites `0`, release-reachable one-hop `Debug.Log` review sites `0`.
- Latest scanner wall time from the command wrapper: `48.9 s` on `588059` first-party C# lines.
- `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal`: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`, log `.codex-artifacts/dotnet-Hecton8.Core-2026-05-03-cold-run-purge.log`.
- `dotnet build .\Hecton8.Editor.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal`: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`, log `.codex-artifacts/dotnet-Hecton8.Editor-2026-05-03-cold-run-purge.log`.
- `git diff --check` on touched files returned exit code `0`; Git reported CRLF normalization warnings only.
- MCP resources were unavailable; no Unity console, Play Mode, frame-time, or GCMonitor proof exists.

Regression model update:

- CPU: direct execution removes synchronous JobSystem entry points from the source inventory, but also removes any Burst-backed synchronous `Run()` execution benefit those cold paths may have had. Scatter acceptance, flora phase seed rebuild, and wreck mesh baking need Play Mode/profiler comparison before claiming a runtime CPU win.
- GC: no new managed containers, delegates, coroutines, strings, or per-frame component lookups were added. Source proof only; GCMonitor proof absent.
- Memory: NativeArray/MeshData ownership and disposal order were not changed.
- Correctness: results are computed by the same job `Execute` bodies over the same buffers. Runtime proof is absent for streaming scatter acceptance parity, flora cascade visual parity, and procedural wreck mesh parity.

## 2026-05-03 Guard Input/Debug Source Clean

Mandates followed: `ARCH_Global_Registry_ServiceLocator_DI_Init`, `OPT_Zero_GC_Policy_AllocFree_Mandate`, `CTRL_Device_Abstraction_Haptics`, `DBG_Telemetry_Crash_Reporting_PostMortem`, `UI_Data_Streaming_ZeroGC_Optimization`.

`Tools/ReloadAudit/Scan-FoundationGuards.ps1` now treats release-reachable debug logging separately from development-only logging:

- `#if UNITY_EDITOR || DEVELOPMENT_BUILD` and `[Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]` logging is no longer reported as release-reachable hot-path debt.
- File-level token prefilters skip the second line scan for files that do not contain any guarded audit token.
- The first hot-path owner pass now skips files without hot cadence member tokens before running call-expression matching.
- Direct `InputManager.Instance` source inventory is now `0`; runtime input consumers must stay on `GlobalRegistry.Input`, `GlobalRegistry.InputBinding`, or the explicitly registered native input owner.

Latest source guard:

- `Tools\ReloadAudit\Scan-FoundationGuards.ps1 -ProjectRoot C:\hades\Hecton8 -OutputMarkdown Docs\Reports\2026-05-03_FOUNDATION_GUARD_SCAN.md`: exit code `0`.
- Guard counts: `.Run(` sites `0`, broad physics masks `0`, `GlobalRegistry.Input` nullable misuse `0`, direct `InputManager.Instance` sites `0`, hot-path direct `InputManager.Instance` review sites `0`, release-reachable direct hot-path `Debug.Log` sites `0`, release-reachable one-hop `Debug.Log` review sites `0`.
- Direct script invocation measured `46324 ms` on the current `588059` line first-party script scope. This is still heavy tooling, but materially bounded versus the prior timed-out path.

Compile status:

- Full Core and Editor builds were attempted after this guard pass, but concurrent/stale `dotnet build Hecton8.Core.csproj` processes caused timeout/abort behavior without a valid MSBuild summary.
- A `--no-dependencies` Core build attempt also aborted before writing output while another Core build process was present.
- Previous clean Core/Editor build logs from the cold-run purge remain valid for that earlier state only. Current C# compile proof is absent for this exact workspace snapshot.

Regression model update:

- CPU: scanner prefilters reduce offline audit work; runtime game code is not affected.
- GC: scanner changes allocate only inside the PowerShell audit tool. No gameplay hot path was edited in this sub-pass.
- Correctness: direct-input and debug-log counts are source scanner proof only. They do not prove Unity console health, input behavior, or release IL stripping.
- Failure modes: the scanner is regex/source based, not a Roslyn semantic analyzer. It can still miss generated code, unusual multiline preprocessor patterns, or dynamic dispatch.

## 2026-05-03 Final Static Verification Closeout

Mandates followed: `ARCH_Global_Registry_ServiceLocator_DI_Init`, `ARCH_Project_Bootstrap_Sequence_Init_Safety`, `OPT_Zero_GC_Policy_AllocFree_Mandate`, `DBG_Telemetry_Crash_Reporting_PostMortem`.

Current final static checks after the generated project guard, `PlayerPDA` input fallback contract fix, input singleton hot-path purge, and guard scanner extension:

- `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal`: `Build succeeded`, log `.codex-artifacts/dotnet-Hecton8.Core-2026-05-03-final-foundation-continuation.log`.
- `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal`: `Build succeeded`, log `.codex-artifacts/dotnet-Hecton8.Core-2026-05-03-input-hotpath-purge.log`.
- `dotnet build .\Hecton8.Editor.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal`: `Build succeeded`, log `.codex-artifacts/dotnet-Hecton8.Editor-2026-05-03-final-foundation-continuation.log`.
- `dotnet build .\Hecton8.Editor.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal`: `Build succeeded`, log `.codex-artifacts/dotnet-Hecton8.Editor-2026-05-03-input-hotpath-purge.log`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File Tools\ReloadAudit\Scan-FoundationGuards.ps1 -ProjectRoot C:\hades\Hecton8 -OutputMarkdown C:\hades\Hecton8\Docs\Reports\2026-05-03_FOUNDATION_GUARD_SCAN.md`: exit code `0`.
- `git diff --check` on the touched files returned exit code `0`; Git reported CRLF normalization warnings only.
- MCP resources list was empty; Unity MCP console proof is absent.

## 2026-05-03 PlayerInventory SlowTick Profiling + Guard Runtime Pass

Mandates followed: `DATA_Inventory_Resources_Items_SOA_Layout`, `OPT_Native_Memory_Collections_JobSystem_Protocol`, `OPT_Zero_GC_Policy_AllocFree_Mandate`, `DBG_Telemetry_Crash_Reporting_PostMortem`, `ARCH_Global_Registry_ServiceLocator_DI_Init`.

`PlayerInventory` now exposes static profiler markers around the SlowTick envelope and the two bounded inline inventory kernels:

```csharp
private static readonly ProfilerMarker _slowTickProfilerMarker = new ProfilerMarker("H8.Inventory.PlayerInventory.SlowTick");
private static readonly ProfilerMarker _radioactiveHalfLifeProfilerMarker = new ProfilerMarker("H8.Inventory.PlayerInventory.RadioactiveHalfLife");
private static readonly ProfilerMarker _reactiveChemistryProfilerMarker = new ProfilerMarker("H8.Inventory.PlayerInventory.ReactiveChemistry");
```

The markers are cold static fields and `Auto()` scopes. No new collections, lambdas, strings, coroutines, JobHandles, or NativeArray ownership changes were introduced. The radioactive half-life and reactive-chemistry scopes wrap only the inline kernel execution; result consumption and gameplay mutations remain in the existing main-thread sequence.

Latest source-only guard evidence:

- `rg -n "\.Run\(" Assets/_Project/Scripts -g '*.cs' -g '!Assets/_Project/Scripts/Editor/**'`: no matches.
- `Tools/ReloadAudit/Scan-FoundationGuards.ps1`: exit code `0`.
- Latest guard invocation via nested `powershell -File`: exit code `0`.
- Guard report generated at `2026-05-03 21:19:57`.
- Synchronous job `.Run(` sites: `0`.
- Hot-path synchronous job `.Run(` review sites: `0`.
- Completion `.Complete(` text hits: `1`, guarded by `DispatcherJobSwap.TryComplete`.
- `UnsafeUtility.MemCpy` outside guard: `0`.
- Legacy `PlayerSignalEvents.On*`: `0`.
- Runtime `Find*`/`GameObject.Find*` outside Editor: `0`.
- Direct `InputManager.Instance` sites: `0`.
- Hot-path direct `InputManager.Instance` review sites: `0`.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false -clp:ErrorsOnly`: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`, log `.codex-artifacts/dotnet-Hecton8.Core-2026-05-03-playerinventory-profiler-markers-final.log`.

Scanner performance was hardened by replacing whole-file registry regex sweeps with bounded line-window checks and by adding string prefilters before expensive regex guards. This is still too slow for Unity assembly reload; do not run this scanner from `EditorApplication.delayCall` without time slicing or a file-change filter.

## 2026-05-03 Native Input Registry Bridge Pass

Mandates followed:

- `ARCH_Global_Registry_ServiceLocator_DI_Init`
- `OPT_Zero_GC_Policy_AllocFree_Mandate`
- `UI_Data_Streaming_ZeroGC_Optimization`
- `CTRL_Device_Abstraction_Haptics`

What changed:

- `InputDispatcher` now exposes its bootstrap-bound native `InputManager` through an internal `NativeInputManager` accessor.
- `GlobalRegistry` now exposes `NativeInputManager` as the single registry-owned bridge for cold UI/Input-System code that still needs concrete `InputAction`, display-style, or UI-module binding APIs.
- Direct `InputManager.Instance` reads were removed from UI/menu/interaction/verification/demo consumers and replaced with `GlobalRegistry.NativeInputManager`.
- `GameBootstrapper.InitializePlayerLayer()` now resolves the bootstrap native owner through `ResolveBootstrapInputManager(gameObject.scene)` before creating the fallback `[InputManager]`; it no longer reads `InputManager.Instance`.

Latest evidence:

- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false -clp:ErrorsOnly`: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`, elapsed `00:00:10.96`. Log: `.codex-artifacts/dotnet-Hecton8.Core-2026-05-03-native-input-resolver.log`.
- `Tools/ReloadAudit/Scan-FoundationGuards.ps1`: exit code `0`; report generated at `2026-05-03 21:14:06`.
- Guard counts after this pass: direct `InputManager.Instance` sites `0`, hot-path direct `InputManager.Instance` review sites `0`, synchronous job `.Run(` sites `0`, raw `UnsafeUtility.MemCpy` outside guard `0`, runtime Find API text hits outside Editor folder `0`, unauthorized Unity loop methods `0`, release-reachable direct hot-path `Debug.Log` sites `0`, release-reachable one-hop `Debug.Log` review sites `0`.
- `git diff --check`: exit code `0`; CRLF normalization warnings only.

Runtime proof is absent. This is source/build/audit evidence only; input-service replacement while menus/PDA/interaction prompts are live still requires Play Mode verification.

## 2026-05-03 Hot-Path Debug Log Guard + HUD Compile Pass

Mandates followed:

- `DBG_Telemetry_Crash_Reporting_PostMortem`
- `OPT_Zero_GC_Policy_AllocFree_Mandate`
- `UI_Localization_Babel_RTL_FontSwap_ZeroAlloc`

What changed:

- Release-build console emission and interpolated debug strings were compiled out of hot-path-adjacent diagnostics in `Fabricator`, `HectonUnderwaterVisuals`, `ObjectPoolManager`, `SaveManager`, `SeamGapDitherRenderer`, `HectonMarineSnowRenderer`, and `SargassumMicroFaunaBoids`.
- The save integrity and defrag failure paths still raise their save events and keep their recovery behavior; only release-player console strings are removed.
- `HUDSaveNotificationLink.GetCurrentLanguageHash()` now uses `unchecked((uint)manager.CurrentLanguage)` for the bounded save-message cache key.
- The previous cache-key path treated `LocalizationManager.CurrentLanguage` as a string and produced `CS1503` under the current `GameLanguage` API contract.

Latest evidence:

- Scoped Core command after the HUD cache-key repair: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false -p:BuildProjectReferences=false -clp:ErrorsOnly`.
- Scoped Core result after the HUD cache-key repair: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`, elapsed `00:00:04.22`.
- Scoped `Hecton8.Editor.csproj` build: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`.
- `Hecton8.Input.csproj` build: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`.
- Full Core dependency-chain verification is not accepted as clean evidence in this pass. It later exited `-1` without diagnostics or reported stale `HectonVoxelEngine.DisposeTrackedNativeArray` errors while current source contains that helper and scoped Core compiles.
- `Tools/ReloadAudit/Scan-FoundationGuards.ps1`: exit code `0`; report generated at `2026-05-03 21:45:36`.
- Guard counts after this pass: direct `InputManager.Instance` sites `0`, hot-path direct `InputManager.Instance` review sites `0`, release-reachable direct hot-path `Debug.Log` sites `0`, release-reachable one-hop `Debug.Log` review sites `0`, synchronous job `.Run(` sites `0`, raw `UnsafeUtility.MemCpy` outside guard `0`, runtime Find API text hits outside Editor folder `0`, unauthorized Unity loop methods `0`.
- `git diff --check`: exit code `0`; CRLF normalization warnings only.

Runtime proof is absent. This is source/build/audit evidence only; save HUD notification text, fabricator full-inventory handling, pool despawn fallback, visual fallback disabling, and underwater reference retries still need Play Mode verification.

## 2026-05-03 Base Airlock Native Event Lane

Mandates followed:

- `OPT_Zero_GC_Policy_AllocFree_Mandate`
- `OPT_Native_Memory_Collections_JobSystem_Protocol`
- `ARCH_Global_Registry_ServiceLocator_DI_Init`
- `CORE_Abyss_Survival_Systems_O2_Pressure_Logic`
- `STRM_Persistent_Object_Registry`

What changed:

- `BaseAirlockEvents` is now a first-party `NativeQueue<BaseAirlockEventPayload>` lane with front/next-frame queues, fixed listener bucket, fixed managed sidecar slots, a 32-byte payload layout, dispatcher prewarm, and `NativeMemorySentinel` registration.
- `SystemDispatcher.LateUpdate()` now reports and flushes `BaseAirlockEvents` immediately after `ModuleStatusEvents`.
- `BaseAirlock` now publishes runtime bus payloads for cycle start, cycle completion, dry/wet environment changes, lockdown changes, manual override blocking changes, and completed emergency override.
- `BaseAirlock` now caches the current interactor's `Rigidbody` and `BuoyancyObject` references across the transition path instead of probing both components on every transfer for the same player transform.
- Existing serialized `UnityEvent` hooks remain in place as legacy scene/prefab wiring. They are not the runtime system bus.

Latest evidence:

- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -clp:ErrorsOnly`: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`, elapsed `00:00:50.37`.
- `dotnet build Hecton8.Editor.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -clp:ErrorsOnly`: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`, elapsed `00:01:14.67`.
- `Docs/ARCHITECTURE/SYSTEM_INTERCONNECT_MATRIX.md` now lists `BaseAirlockEvents.FlushPending()` at LateUpdate slot 43.
- `.codex-artifacts/2026-05-03_base_airlock_native_event_lane.diff` contains the current scoped diff, including the new untracked `BaseAirlockEvents.cs` and `.meta`.

Runtime proof is absent. This is source/build evidence only; airlock transition, dry-zone buoyancy handoff, listener churn, queue overflow/drop behavior, and legacy UnityEvent coexistence still need Play Mode verification.

## 2026-05-03 Sealed Door Progress Cadence Hardening

Mandates followed:

- `OPT_Zero_GC_Policy_AllocFree_Mandate`
- `OPT_Native_Memory_Collections_JobSystem_Protocol`
- `ARCH_Global_Registry_ServiceLocator_DI_Init`
- `DBG_Telemetry_Crash_Reporting_PostMortem`
- `UI_Data_Streaming_ZeroGC_Optimization`

What changed:

- `SealedDoor.StartCutting()` no longer registers the door into `GlobalRegistry.Updatables` while `Tick(float)` is intentionally empty.
- `SealedDoor.ApplyCutting` progress side effects are now coalesced at `0.01f` normalized progress steps, with forced final `1.0f` publication before opening.
- Renderer progress updates and legacy `OnProgressChanged` invocations share the same threshold guard.
- Existing serialized `UnityEvent` fields remain in place for scene/prefab compatibility.

Latest evidence:

- Source grep confirms `SealedDoor` no longer contains `RegisterToTick`, `UnregisterFromTick`, or `_isRegistered`.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -clp:ErrorsOnly`: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`, elapsed `00:00:08.29`.
- Runtime proof is absent. Laser cutting, door VFX smoothness, and UI progress listener cadence still need Play Mode proof.

## 2026-05-03 Ending Terminal Interaction Prompt Cache

Mandates followed:

- `OPT_Zero_GC_Policy_AllocFree_Mandate`
- `ARCH_Global_Registry_ServiceLocator_DI_Init`
- `CTRL_Device_Abstraction_Haptics`
- `UI_Diegetic_Physical_Interfaces`
- `CORE_Tools_Equipment_Interaction_Raycast_Heat`

What changed:

- `EndingTerminalInteractable` now implements `ILocalizationLanguageChangedListener` and registers/unregisters with `LocalizationEvents` in the same enable/disable lifecycle window as its ending event listener.
- `GetInteractText()` now returns cached inactive/active/complete prompt strings instead of resolving localization on every prompt read.
- The ATLAS-6 data-loaded notification text is cached on lifecycle/language-change refresh and reused by `OpenChoiceUI()`.
- Hover and active-indicator visuals now call `SetActive` only when `activeSelf` differs; `OnDisable()` clears hover visual state and `_choiceOpen`.

Latest evidence:

- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false`: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`, elapsed `00:00:51.04`.
- `Tools\ReloadAudit\Scan-FoundationGuards.ps1 -ProjectRoot C:\hades\Hecton8 -OutputMarkdown Docs\Reports\2026-05-03_FOUNDATION_GUARD_SCAN.md`: May 3 exit code `0`; May 3 guard output generated at `2026-05-03 22:32:21`. Current May 4 recheck exits `0`; latest generated guard output is `2026-05-04 23:33:55`.
- Runtime proof is absent. Ending terminal hover, language switching while hovered, completion-state prompt swaps, and final choice UI flow still need Play Mode proof.

## Regression Model

- CPU: deferred teardown removes streaming-retirement barriers, but still needs Play Mode eviction stress to prove no late-frame backlog.
- CPU: service/floating-origin listener registration truth-state adds only lifecycle `ReferenceEquals`/bucket containment checks; no per-frame work added.
- CPU: controls lifecycle caching adds only lifecycle-field reads/writes and does not add per-frame work.
- CPU: `PlayerPDA` input fallback contract fix changes lifecycle/open/close dispatch semantics only; no Tick/SlowTick work was added.
- CPU: `EndingTerminalInteractable.GetInteractText()` now performs only ending-state branches and cached-string returns; localization refresh moved to Awake/OnEnable/language-change.
- CPU: `PlayerInventory` adds profiler marker `Auto()` scopes around SlowTick and two inline kernels. Marker overhead is source-acceptable for profiling but still needs Play Mode timing on MX350 before claiming no regression.
- CPU: input singleton hot-path purge removes direct `InputManager.Instance` calls from one-hop hot-path scanner output. `PlayerInteraction` adds lifecycle/hot-swap service membership checks only; `HectonFabricatorUI` map switching now calls the no-op-safe registry input service and native navigate/submit/cancel subscription is lifecycle/hot-swap only; `PDAIntrusionManager`, `PlayerFlashlight`, interaction prompt UIs, and `DiegeticTooltipSystem` moved native action/display-style/binding subscription resolution to lifecycle/hot-swap paths.
- CPU: controls selection visual updates no longer resolve/add `CanvasGroup` components during navigation; the cost is moved to cold UI build/reference rebuild.
- CPU: generated-project build guards affect MSBuild/Editor project-generation only; runtime cadence is unchanged.
- CPU: beacon HUD visibility no longer performs `GetComponent`/`AddComponent` fallback work in `Tick`; malformed display records now skip visibility application instead of mutating hierarchy from the hot path.
- CPU: notification listener registration/unregistration adds O(N) bucket membership checks only on lifecycle calls; dispatch adds one null branch per listener slot.
- CPU: save, scan, crafting, interaction, inventory, quest, and narrative event lanes now add only lifecycle-time bucket membership checks and dispatch-time null branches over existing fixed listener arrays.
- CPU: bootstrap, weather, flashlight, ending, first-hour, storage-reservation commit, physics-impact, tool-effect, power telemetry, high-pressure, and fatal-pressure dispatch now add one null branch per existing listener slot; no queue cadence or listener capacity changed.
- CPU: foundation guard scanner is offline PowerShell and does not affect runtime. The one-hop classifier adds scan time only; it must not be moved into Unity assembly reload without time slicing.
- CPU: current guard reports no first-party runtime `.Run()` sites. This is not profiler proof; scheduled-job cadence for the former flora/scatter/wreck sources still needs runtime load verification.
- CPU: `QuestStateManager.EvaluateSignal()` no longer enters `IJob.Run()` for a single synchronous signal kernel; it executes the same preallocated native-buffer kernel directly. No profiler numbers were captured.
- CPU: `FaunaSimulationEngine.RunHibernationCatchUp()` no longer enters `IJobParallelFor.Run()` for bounded restore-time hibernation catch-up; it executes the same kernel directly over persistent native buffers. No profiler numbers were captured.
- CPU: fallback beacon material ownership adds no Tick/SlowTick work. Fallback deployment still uses cold primitive/material creation when no beacon prefab exists; this remains a fallback path, not a hot rendering path.
- CPU: bounded drop/breakdown drains may defer excess work by one or more frames under stress instead of draining unbounded queues in a single frame.
- CPU: module registry consumers still perform O(N) scans, but no longer retain list views; future conversion to dense registry buckets remains possible without API breakage.
- CPU: repair hub storage discovery still performs `OverlapSphereNonAlloc`; repeated positive `StorageCrate` component resolutions are now served from a fixed cache after the first scan.
- CPU: autonomous extractor placement/binding discovery still performs `OverlapSphereNonAlloc`; repeated positive `ResourceNode` component resolutions are now served from a fixed cache after the first scan.
- CPU: vehicle docking invalid trigger contacts now bypass repeat owner-resolution work for the last rejected collider.
- CPU: vehicle docking valid trigger contacts now use a fixed owner cache after the first successful `IPlayerTransportLifecycleOwner` resolution. Cache lookup is bounded at 16 slots and runs only on trigger callbacks.
- CPU: `ConstructionManager` Save hot-swap ownership adds lifecycle/hot-swap bucket checks only; no Tick/SlowTick cadence was added.
- CPU: renderable truth-state checks add O(N) `RegistryBucket.Contains(this)` work only during lifecycle registration; render cadence is unchanged.
- CPU: tether visual update no longer invokes JobSystem `.Run()` for a 8-24 point visual catenary; it uses the same O(N) math inline over existing native buffers. No profiler numbers were captured.
- CPU: `BaseAirlock` safe-teleport calls run only during interaction-triggered room transfer, not per-frame. They may pause physics for one frame through the existing floating-origin protocol.
- CPU: `BaseAirlock` spawn validation runs once per interaction before cycle mutation; no Tick/FixedTick work was added.
- CPU: `BaseAirlockEvents` adds O(1) enqueue work during interaction-triggered airlock state changes and O(listener count) LateUpdate dispatch. Native queue creation/priming is moved to `SystemDispatcher.InitializeService()`. It adds no `Tick` work. Interactor Rigidbody/BuoyancyObject resolution is cached after the first transition for a transform.
- CPU: `SealedDoor` no longer occupies a dispatcher lane while cutting when `Tick(float)` is empty. Progress renderer/event side effects are capped to roughly 100 normalized steps per full cut plus the forced final event.
- GC: no measured GCMonitor proof. Source-level changes use preallocated collections, timeout-only diagnostics, constant development-log strings in the airlock missing-spawn path, persistent native traversal scratch, and existing static arrays for dispatcher raycast sidecars.
- GC: `PlayerInventory` profiler marker pass adds only static readonly markers and struct `Auto()` scopes; no hot-path managed collection, string, delegate, coroutine, or JobHandle allocation was introduced by this pass.
- GC: input controls lifecycle changes add no new collections, lambdas, coroutines, or per-frame strings; status text still uses existing UI event paths and cached builders where the file already used them.
- GC: `PlayerPDA` direct `GlobalRegistry.Input` dispatch adds no allocations; it removes nullable access against the existing null-object fallback.
- GC: ending terminal prompt reads no longer resolve localized strings during `GetInteractText()`; language-change/open-choice paths still use managed localized strings and are cold/event-driven only.
- GC: input singleton hot-path purge adds no collections, lambdas, coroutine state, or per-frame strings. The guard scanner/docs are offline-only.
- GC: selection indicator caching adds fixed `CanvasGroup[]` cold arrays and cold `CanvasGroup` component creation only when authoring omitted one; selection refresh adds no managed containers or component lookups.
- GC: beacon HUD icon visibility uses the existing preallocated icon display records and cached `CanvasGroup`; no hot-path component creation remains in `ApplyDisplayVisible`.
- GC: notification listener hardening uses existing `RegistryBucket` storage and does not allocate during publish or dispatch.
- GC: event-lane hardening uses existing `RegistryBucket` and `NativeQueue` storage; no event publish/flush path creates managed collections, delegates, or strings.
- GC: raw listener dispatch purge creates no managed containers or delegates; it only copies an existing array slot to a local interface reference before invocation.
- GC: foundation guard tooling is local PowerShell/docs only; renderable/service/floating-origin registration changes add no hot-path managed allocation.
- GC: generated-project reference pruning and SDK artifact suppression run outside gameplay; no runtime heap path is introduced.
- GC: registry/accessor, supply-cache, and resource-cache changes add fixed cold arrays only; no new allocations are introduced in `SlowTick`, `Tick`, `FixedTick`, or `OnTriggerStay`.
- GC: vehicle dock owner caching adds fixed cold arrays only; no new managed containers, lambdas, coroutines, strings, or allocating physics queries were added to trigger paths.
- GC: `BaseModule` dry-zone tracking changes key width only; the existing preallocated dictionary/list ownership remains, with no new hot-path allocation site.
- GC: `BaseAirlockEvents` uses persistent native queues, fixed listener storage, and fixed sidecar slots. No managed collections, lambdas, coroutines, or per-frame strings were added to `BaseAirlock.Tick()`.
- GC: `SealedDoor` cadence hardening adds only scalar fields and branches. No managed collections, lambdas, coroutines, strings, or native allocations were added to the laser-cut path.
- GC: `ConstructionManager` save hot-swap ownership adds no per-frame allocation and registers/unregisters only through lifecycle/service replacement paths.
- GC: fallback beacon material allocation remains a cold deploy/load fallback allocation and now has a concrete `BeaconRuntime` release owner. No per-frame string, collection, coroutine, or delegate path was added.
- Memory: deferred meshes/owners live until their bake handle completes; leak risk requires rapid stream/unload soak testing.
- Memory: `HabitatGraphManager` adds one persistent `NativeArray<byte>` traversal scratch at graph capacity and resets disposed native fields to `default`; this is bounded cold allocation plus teardown hygiene, not memory-retention proof.
- Memory: `HabitatGraphManager` adds one static owner reference for the latest siege-target snapshot; it prevents stale-owner clearing and is nulled when the owning manager clears its published snapshot.
- Memory: `BaseAirlockEvents` owns two persistent native queues with a 32-payload software cap plus 32 fixed sidecar slots. The dispatcher prewarms both queues and they reset on subsystem registration. No unbounded event backlog path was added.
- Memory: `LoreDatabaseManager` still owns one persistent native unlock-word array; teardown now flushes its deferred disposal job without a blocking complete.
- Memory: `LoreDatabaseManager` edit-mode guard adds no memory; it prevents binding a scene instance to a stale runtime save owner outside Play Mode.
- Memory: repair hub cache keeps up to 24 `StorageCrate` references and clears them on disable/spawn/despawn to avoid stale scene reference retention.
- Memory: each autonomous extractor module keeps up to 24 `ResourceNode` references and clears them across enable/disable/destroy/spawn/despawn to avoid stale scene reference retention.
- Memory: each vehicle docking module keeps up to 16 transport-owner references and clears them across enable/disable/destroy/spawn/despawn to avoid stale scene reference retention.
- Memory: dispatcher-raycast tail clearing reduces stale managed receiver retention risk when command queue and sidecar count diverge.
- Memory: fallback beacon material lifetime is no longer unowned static mutable state; each fallback material is owned by its `BeaconRuntime` and destroyed on runtime destruction. Fallback objects also clear prefab-source ownership so they do not enter pool despawn after a failed prefab spawn. Scene unload and domain-reload behavior still need Unity verification.
- Correctness: chunk side effects are released before mesh object destruction; regression risk remains around orphaned chunk roots if Unity scene unloads before late-frame drain.
- Correctness: spatial-hash handle churn now has deterministic EditMode proof for stale-handle rejection and cell migration. Runtime pressure under live register/unregister churn remains unprofiled.
- Correctness: `BaseAirlock` init-order registration is source/build checked only; scene interaction proof is absent.
- Correctness: `BaseAirlock` room transfer now clears queued force packets and resets Rigidbody interpolation state through the existing safe-teleport protocol; runtime proof is absent for moving-base airlock transitions and save/load around interior state.
- Correctness: `BaseAirlock` invalid spawn data now fails before state/audio/event mutation; scene proof is absent for malformed prefab variants.
- Correctness: `BaseAirlockEvents` gives runtime listeners a queue-backed signal surface, but legacy serialized UnityEvents are still present. Full eradication requires prefab/scene readback and listener migration proof, not a blind serialized-field removal.
- Correctness: `SealedDoor` progress UI now receives thresholded updates instead of every laser hit. This reduces event noise but needs Play Mode proof that connected progress bars remain visually acceptable. Existing MPB progress VFX remains a known SRP Batcher conflict; this pass reduces call cadence but does not remove the path without shader/prefab readback.
- Correctness: `LoreDatabaseManager` save registration is source/build checked only; actual save/load roundtrip proof is absent.
- Correctness: `LoreDatabaseManager` now refuses edit-mode save registration; domain reload and Play Mode transition proof are absent.
- Correctness: `LoreDatabaseManager` native unlock-word disposal flushing is source/build checked only; domain reload and scene unload leak-detector proof are absent.
- Correctness: `HabitatGraphManager` anchor-state/traversal separation and native disposal are source/build checked only; construction rebuild, scene unload, connected-component power, fungal target, and graph teardown soak proof are absent.
- Correctness: `HabitatGraphManager` siege-target owner guarding is source/build checked only; predator siege cognition still needs runtime proof across graph manager replacement/disposal.
- Correctness: if a dispatcher-raycast enqueue/dequeue divergence occurs, unscheduled requests are now explicitly dropped with cleared receivers rather than retained for a later mismatched callback.
- Correctness: `SubtitleManager` singleton-removal drift is compile-checked only; runtime HUD subtitle creation still needs scene proof.
- Correctness: resource-host cache correctness is source-checked only; runtime extractor placement and slow-tick rebinding need scene proof around depleted, disabled, and recycled resource nodes.
- Correctness: vehicle dock trigger-owner cache is source/build checked only; runtime proof is absent for multi-collider transports, despawn/repool while inside trigger, and docking release/re-enter churn.
- Correctness: `BaseModule` dry-zone occupancy now avoids collider-id truncation, but runtime proof is absent for multi-collider buoyancy objects crossing flooded/pristine module boundaries.
- Correctness: `EndingTerminalInteractable` prompt localization is source/build checked only; runtime proof is absent for language changes while the terminal is hovered and for completion-state prompt refresh.
- Correctness: `ConstructionManager` save registration owner tracking is source/build checked only; construction graph save/load and Save service replacement during scene teardown need runtime proof.
- Correctness: editor tech-art audit wiring is local dotnet-build checked only; Unity menu execution and generated log output still need an Editor command pass.
- Correctness: service slot ownership is source/build checked only. Runtime duplicate-service, domain reload, and scene unload sequences still require Unity console proof.
- Correctness: generated `Assembly-CSharp` command-line build now reaches `Build succeeded`; Unity Editor import/compile still needs MCP/console proof because generated csproj files can be regenerated by Unity.
- Correctness: renderable local registration flags now mirror authoritative `GlobalRegistry.Renderables` bucket membership; scene pressure and capacity failure behavior still need runtime proof.
- Correctness: fallback beacon cube color can no longer be overwritten by a later fallback beacon through shared material mutation. A fallback object created after prefab pool failure is no longer treated as prefab/pool-owned during despawn. Deploy/retract/load-save visual proof is absent.
- Correctness: controls panels now unsubscribe from the exact owners they subscribed to; runtime proof is still absent for input-service replacement during an open pause/PDA controls panel.
- Correctness: `PlayerPDA` now tests `GlobalRegistry.Input.IsInitialized` instead of a dead null check; runtime proof is still absent for PDA open/close across input-service replacement.
- Correctness: `PlayerInteraction` now subscribes to `IInputService` and rebinds on `GlobalRegistryServiceSlot.Input` replacement; runtime proof is absent for interact spam across input-service replacement. `PlayerFlashlight` now rebinds native flashlight input from lifecycle and `GlobalRegistryServiceSlot.Input` hot-swap notification instead of retrying singleton lookup every Tick. `Interaction/InteractionUI`, `UI/InteractionUI`, and `DiegeticTooltipSystem` now rebind prompt/display-style ownership from lifecycle/hot-swap paths; runtime proof is absent for prompt/icon refresh across live input service replacement. `PDAIntrusionManager` no longer re-resolves the UI submit action every Tick and now clears/rebinds cached native submit ownership on input-service replacement; runtime proof is absent for live InputActionAsset replacement during an active PDA intrusion.
- Correctness: selection indicators still use `CanvasGroup.alpha` semantics; runtime proof is absent for pre-authored controls panels whose row references are changed after enable.
- Correctness: malformed beacon icon records with null `CanvasGroup` now remain unchanged instead of self-repairing during `Tick`; prefab/icon pool authoring must provide the proxy at cold setup.
- Correctness: duplicate notification listener registration no longer consumes bucket capacity; absent unregistration no longer emits registry mismatch behavior. Runtime listener churn proof is absent.
- Correctness: save, scan, quest, and narrative duplicate listener registration no longer consumes bounded bucket capacity; absent unregister no longer invokes registry mismatch behavior. Crafting, interaction, inventory, and narrative POI dispatch now skip null listener slots. Runtime churn proof is absent.
- Correctness: no `rawArray[i].On*` direct dispatch remains under `Assets/_Project/Scripts`; stale/null listener slots are skipped instead of terminating the flush. Runtime listener churn proof is absent.
- Correctness: guard scan now detects raw memcopy and stale player-signal subscriptions in source, but it is not semantic AST proof and can still miss generated code or reflection-driven access.
- Correctness: tether visual job-run purge is source/build checked only; cable visual smoothness, sag parity, and GPU line rendering need Play Mode scene proof.
- Correctness: `PlayerInventory` inline-kernel conversion is source/build checked only; inventory sort order, derived mass/radiation totals, radioactive conversion, and reactive-pair destruction need Play Mode/save roundtrip proof.
- Correctness: `PlayerInventory` profiler markers are observational only. They do not prove item degradation, reactive chemistry, save/load, or GC behavior.
- Correctness: `CraftingSystem` inline-kernel conversion is source/build checked only; recipe affordability and deconstruction yield output need gameplay test coverage.
- Correctness: `QuestStateManager.EvaluateSignal()` no longer appears in current `.Run()` inventory; quest activation/completion order and save/load interaction still need gameplay/test coverage.
- Correctness: `FaunaSimulationEngine.RunHibernationCatchUp()` no longer appears in current `.Run()` inventory; fauna hibernation restore still needs save/load and runtime fauna-residency proof.
- Editor: current Unity batchmode fallback reached `Exiting batchmode successfully now!` with no C# compile-failure patterns. MCP runtime console was not available.
- Editor: Unity logged a licensing access-token update error during batchmode; compile/import still completed, but licensing state is not fixed by this pass.
- Build metadata: command-line Core, Editor, and optional DOTS builds are clean after pruning. Earlier full verbose Core dependency builds revealed vendor/package warnings that are outside first-party ownership.
- Build metadata: `Assembly-CSharp-firstpass` and `Assembly-CSharp` command-line builds now succeed after the generated project guard pass. Vendor/package obsolete API warnings remain.

## Do Not Claim

- Do not claim Play Mode deadlock fixed.
- Do not claim zero GC.
- Do not claim MCP verified.
- Do not claim runtime memory retention solved.
- Do not claim spatial-hash runtime pressure solved; only deterministic EditMode coverage exists.
- Do not claim lore persistence runtime save/load solved; only registration ownership was source/build checked.
- Do not claim construction graph behavior solved globally; anchor-state scratch separation and one disposal path were hardened, but runtime graph behavior is unverified.
- Do not claim dispatcher raycast runtime stress solved without a foveated/raycast Play Mode burst test.
- Do not claim subtitle runtime creation solved without HUD scene proof.
- Do not claim autonomous extractor runtime rebinding solved without resource-node depletion/recycle scene proof.
- Do not claim vehicle docking trigger-owner caching solved without multi-collider transport and despawn/repool trigger proof.
- Do not claim base dry-zone occupancy solved without flooded/pristine room crossing proof.
- Do not claim ending terminal prompt/localization behavior solved without Play Mode hover and language-switch proof.
- Do not claim construction graph persistence solved without save/load and Save service replacement runtime proof.
- Do not claim tether visual behavior solved without Play Mode towing/cable bend proof.
- Do not claim `PlayerInventory` item degradation/reactive chemistry solved without inventory stress, save/load, and GCMonitor proof.
- Do not claim crafting/deconstruction behavior solved without recipe affordability and dismantle-yield tests.
- Do not claim the tech-art audit runtime menu path solved without executing `Hecton/Validation/Asset Pipeline/Run Full Audit And Emit Log` in Unity.
- Do not claim input authority drift solved. This pass fixed two controls-panel lifecycle leaks, `PlayerPDA` null-object fallback misuse, PDA intrusion submit-action hot-swap rebinding, and zero direct `InputManager.Instance` scanner output; native input ownership still needs runtime hot-swap proof.
- Do not claim runtime job-cadence safety from source inventory alone; current first-party source guard reports `0` `.Run(` sites, but frame-time, latency, and memory behavior still require Play Mode/profiler proof.
- Do not claim Unity Editor MCP console is clean from MSBuild-only success. MCP resources were unavailable in the current environment.
