# AUTOFIX6 Log

## 2026-05-25 Runtime Diagnostics Hygiene

What was wrong:
Direct Unity diagnostics remained in first-party runtime files across flow-field, fauna, physics, scene runtime, signal lanes, voxel, underwater visuals, survival, pooling, narrative, interaction, seam, and save-event code. These calls bypass the central `H8Debug` policy and make release-path diagnostic allocation risk harder to prove.

What was done:
Replaced direct `Debug.LogWarning`, `Debug.LogError`, and `Debug.LogException` / `UnityEngine.Debug.LogException` call sites with `Hecton8.Core.H8Debug` in 32 C# files. Preserved messages, context objects, exception payloads, and control flow. No public API, YAML, prefab, scene, asset, package, project setting, simulation, save identity, DTO, or authority route changes.

Files changed:
- Assets/_Project/Scripts/FlowFieldVisualizer.cs
- Assets/_Project/Scripts/Fauna/FaunaPOI.cs
- Assets/_Project/Scripts/Fauna/FaunaBrain.cs
- Assets/_Project/Scripts/Core/ThreadSafeCommandQueue.cs
- Assets/_Project/Scripts/GlobalPhysicsStateManager.cs
- Assets/_Project/Scripts/Core/SceneRuntimeService.cs
- Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs
- Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs
- Assets/_Project/Scripts/Core/Signals/GlobalSignals.RuntimeLifecycle.cs
- Assets/_Project/Scripts/Core/BootstrapContracts/BootstrapStatus.cs
- Assets/_Project/Scripts/HectonVoxelVolume.cs
- Assets/_Project/Scripts/HectonVoxelEngine.cs
- Assets/_Project/Scripts/HectonUnderwaterVisuals.cs
- Assets/_Project/Scripts/HectonSurvivalSystem.cs
- Assets/_Project/Scripts/Gameplay/FirstHourDirector.cs
- Assets/_Project/Scripts/ObjectPoolManager.cs
- Assets/_Project/Scripts/LandingImpactVFX.cs
- Assets/_Project/Scripts/NarrativeEvents.cs
- Assets/_Project/Scripts/NarrativeDiscovery.cs
- Assets/_Project/Scripts/Narrative/LoreDatabaseManager.cs
- Assets/_Project/Scripts/Narrative/Campaign/MetaCampaignService.cs
- Assets/_Project/Scripts/ModuleCatalog.cs
- Assets/_Project/Scripts/Interaction/SuitDamageEvents.cs
- Assets/_Project/Scripts/Interaction/SaveStation.cs
- Assets/_Project/Scripts/Interaction/PlayerInteraction.cs
- Assets/_Project/Scripts/SeamRegistry.cs
- Assets/_Project/Scripts/SeamGapDitherRenderer.cs
- Assets/_Project/Scripts/Interaction/PhysicalInteractionHandler.cs
- Assets/_Project/Scripts/Interaction/PhysicalHandReceiverRegistry.cs
- Assets/_Project/Scripts/SaveEvents.cs
- Assets/_Project/Scripts/Interaction/PhysicalHandController.cs
- Assets/_Project/Scripts/Interaction/InteractionEvents.cs

Cinematic cheats used:
None. This was runtime hygiene, not presentation/simulation work. The cheap path was to centralize diagnostics instead of changing systems.

Exact microseconds saved:
Static-only estimate: 0us in normal non-fault gameplay frames. Fault/debug paths now route through conditional `H8Debug`; any actual saved CPU/GC requires Unity Profiler/GCMonitor proof. Status: PENDING VERIFICATION.

Verification:
- Scoped direct-debug call-site scan: clean. No matches for `^\s*(UnityEngine\.)?Debug\.(LogWarning|LogError|LogException)` across the 32 edited files.
- H8Debug routed call count: 74.
- `git diff --check`: exit 0. Git reported LF->CRLF working-copy warnings only.
- Build: not run. Gate blocked by CPU=74 and active dotnet process 64580.
- Unity runtime/profiler: not run. PENDING external Unity artifact.

Regression model:
CPU: neutral outside diagnostic fault paths. GC: lower policy risk due conditional facade, but measured proof absent. Memory/VRAM: no change. Cadence: no dispatcher/tick route changed. Correctness: diagnostic messages and contexts preserved. Failure mode: if `H8Debug` facade signature changes, compile fails; current signature supports message/context/exception overloads.
