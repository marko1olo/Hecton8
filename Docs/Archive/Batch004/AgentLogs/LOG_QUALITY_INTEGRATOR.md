# LOG_QUALITY_INTEGRATOR

2026-05-13 QUALITY PASS

What was wrong:
- Unity log contained stale Core compile errors in InternalFloodWaterlineRuntime, HectonVisorUberPostFeature, VehicleSubOsCockpitRuntime, and later Burst failed to resolve Hecton8.Vehicles.VFX.
- Replaying current Unity Bee Roslyn input proved Core now compiles after parallel working-tree edits.
- Replaying Hecton8.Vehicles.VFX isolated the live defect: HullDentShaderController crossed an asmdef boundary and accessed internal SystemDispatcher.CurrentFrameUnscaledDeltaTime.

What was done:
- Patched Assets/_Project/Scripts/Vehicles/VFX/HullDentShaderController.cs.
- Replaced direct internal SystemDispatcher time access with allocation-free ResolveUnscaledDeltaTime using GlobalRegistry.TickDispatcher.TimeSnapshot and Time.unscaledDeltaTime fallback.
- Verified `Hecton8.Core.rsp` exit 0.
- Verified `Hecton8.Vehicles.VFX.rsp` exit 0.

Cinematic cheats used:
- None added. Existing hull dent system remains shader-only; no physical deformation simulation was introduced.

Exact microseconds saved:
- 0 us/frame claimed. The change fixes compilation and preserves O(1), allocation-free late-frame repair fade timing.

Pending verification:
- Unity MCP `refresh_unity` timed out after 60s and console access returned unavailable earlier. Editor postprocess/ScriptAssemblies refresh is still pending; no manual DLL copy was performed.

## 2026-05-13 Quality Integration Start

What was wrong: Request was broad and not tied to a single system.
What was done: Started evidence-first triage path.
Cinematic Cheats used: None yet.
Exact Microseconds saved: 0 claimed.

## 2026-05-13 Quality Integration Compile Pass 2

What was wrong:
- Fresh Core validation exposed a live compile wall in `PredatorCognitionDomain.cs`: `PredatorCognitionJob` called `ResolveRuntimePosition`, but the containing helper had been renamed to telemetry scope.
- Stale Bee response inventory did not include the new Power/RTG asmdefs or RTG editmode test, so old SaveData missing-field errors were not trustworthy.
- The earlier Vehicles.VFX fix removed the internal access violation, but the time helper still needed hot-path service-cache discipline.

What was done:
- Added a private static `ResolveRuntimePosition` helper inside `PredatorCognitionJob`, matching existing AUP runtime conversion used by adjacent job code.
- Cached `ITickDispatcher` in `HullDentShaderController` during enable/registration and removed per-frame `GlobalRegistry.TickDispatcher` polling from the late-frame time resolver.
- Rebuilt Core through both temporary current-source validation and the normal Bee response.
- Validated `Hecton8.Power.Generators.Contracts`, `Hecton8.Power.Generators`, `RtgDecayMathTests`, and optional `Hecton8.World.Dots` through manual response files because Unity's current Bee graph had not generated those assemblies yet.

Cinematic Cheats used:
- None added. Existing hull dents remain shader-only; fauna retinal reaction remains deterministic math; RTG validation did not change runtime behavior.

Exact Microseconds saved:
- 0 us/frame claimed. The pass removes compile walls and one per-frame registry property read in Vehicles.VFX, but no profiler sample was captured.

Verification:
- `dotnet ... csc.dll @Temp/QualityIntegrator/MissingAsm/Hecton8.Core.current.validation.rsp` exit 0 after fauna patch.
- `dotnet ... csc.dll @Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.rsp` exit 0.
- `dotnet ... csc.dll @Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Vehicles.VFX.rsp` exit 0 after dispatcher-cache patch.
- `dotnet ... csc.dll @Temp/QualityIntegrator/MissingAsm/Hecton8.Power.Generators.Contracts.validation.rsp` exit 0.
- `dotnet ... csc.dll @Temp/QualityIntegrator/MissingAsm/Hecton8.Power.Generators.validation.rsp` exit 0 with one warning: obsolete `Object.GetInstanceID()` in `RadioisotopeThermalGenerator.cs(767)`.
- `dotnet ... csc.dll @Temp/QualityIntegrator/MissingAsm/Hecton8.EditModeTests.Rtg.validation.rsp` exit 0.
- `dotnet ... csc.dll @Temp/QualityIntegrator/MissingAsm/Hecton8.World.Dots.validation.rsp` exit 0.
- `git diff --check` on touched source/log files exit 0, with only Git line-ending warnings.

Pending verification:
- `refresh_unity` timed out after 60s waiting for editor readiness.
- `read_console` returned `no_unity_session` twice. Unity Console is PENDING VERIFICATION.
- No Player build, PlayMode, profiler, Memory Profiler, or frame-debugger proof was captured.
