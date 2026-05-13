# LOG_CONSOLE_MEDIC

## 2026-05-13T06:45:36+04:00

What was wrong:
- Unity Console/MCP initially showed stale compile errors for `TetherInstance`, `CrashTelemetryBuffer`, `SaveManager`, `GlobalSignals`, `EcosystemDirector`, `HectonPlayerMovement`; current source already contained those members/imports, so no code was changed for stale entries.
- Live Unity log then showed current C# blockers in `AcousticPortalPropagation.cs`: CS1612 from `NativeArray<AcousticPortalNode>` value fields passed into `in` AUP distance calls.
- Live Unity log showed current foveated contract drift: `FaunaBrain.Foveated.cs` did not satisfy the expanded `IFoveatedSimulationTarget` interface.
- Live Unity log showed signal type resolution errors for `CombatDamageSignal`, `WeatherChangedSignal`, and `AcousticPingSignal`; current source now contains `Hecton8.Core.Signals` imports, so no duplicate patch was applied.
- Live Unity log showed shader import errors in `Hecton_HologramAssembly.shader`: `line` token parse failure and resulting invalid `smoothstep`.
- Live Unity log showed shader warnings in `Hecton_CoreLit.hlsl`: `skip_variants` and `multi_compile` pragmas inside an HLSL include.
- Unity verification is blocked: `mcpforunity://instances` reports 0 instances, `read_console` returns `Unity session not available`, and `Logs/CodexUnityLaunch_20260512_hud_surface.log` stopped updating after 2026-05-13 02:29:38 UTC.

What was done:
- `Assets/_Project/Scripts/Audio/Propagation/AcousticPortalPropagation.cs`: copied `AcousticPortalNode.Position` values into stack-local `AcousticAup` variables before `DistanceMeters(in ..., in ...)`.
- `Assets/_Project/Scripts/Fauna/FaunaBrain.Foveated.cs`: brought the foveated target implementation to the expanded contract and used cached foveated distance as frozen-wrap fallback to avoid a write-only-field warning path.
- `Assets/_Project/Art/Shaders/Hecton_HologramAssembly.shader`: renamed the HLSL local `line` to `gridLine`; preserved the existing cheap grid-line visual fake.
- `Assets/_Project/Art/Shaders/Hecton_CoreLit.hlsl`: removed invalid ShaderLab pragmas from the shared HLSL include; pass-level shaders keep their own variant controls.

Cinematic cheats used:
- Acoustic: preserved AUP portal path approximation and stack copies; no managed path reconstruction.
- Fauna: foveated frozen wrap remains a controlled far-predator presentation cheat gated by distance, voxel sampling, and existing director hunt target.
- Shader: hologram grid remains a `frac`/`smoothstep` fake, not geometry or simulation.

Exact microseconds saved:
- Acoustic compile fix: 0 us measured runtime; expected 0 us hot-path regression, stack copy only.
- Foveated contract repair: no profiler artifact available; expected low-end savings come from preserving frozen predator cold paths, not from this patch itself.
- Shader token/pragmas: 0 us measured runtime; import/compiler path fix only.
- Avoided mass line-ending rewrite across 30 shader files: unmeasured runtime, but prevented large diff churn and editor reimport debt.

Verification:
- Unity MCP Console verification: BLOCKED, no active Unity MCP instance.
- Live Unity log verification: BLOCKED, log did not refresh after final edits.
- `dotnet build Hecton8.Core.csproj`: rejected as proof; generated Unity csproj currently fails on broad asmdef/dependency graph issues unrelated to the active Console evidence.

## 2026-05-13T07:06:52+04:00

What was wrong:
- Second-pass source audit found the same CS1612-class acoustic risk still present in `FindNearestNode`: `node.Position` was passed directly into `in` parameters.
- `FaunaBrain.Foveated` cached raw distance from the foveated manager; a NaN would keep the frozen-wrap gate unstable.
- `Hecton_HologramAssembly.shader` recomputed fabricator-local Y in the fragment stage after the vertex stage already had the same value.
- Verification remains blocked: MCP `read_console` and `validate_script` return `no_unity_session`, `mcpforunity://instances` reports 0 instances, and `CodexUnityLaunch_20260512_hud_surface.log` still stops at 2026-05-13 02:29:38 UTC.

What was done:
- `Assets/_Project/Scripts/Audio/Propagation/AcousticPortalPropagation.cs`: copied `node.Position` to a stack-local `AcousticAup` in `FindNearestNode` before `IsFinite` and `DistanceMeters`.
- `Assets/_Project/Scripts/Fauna/FaunaBrain.Foveated.cs`: clamped non-finite or negative foveated distance to 0 before caching.
- `Assets/_Project/Art/Shaders/Hecton_HologramAssembly.shader`: added `fabricatorLocalY` varying and used it in `Frag` for clip/edge math.

Cinematic cheats used:
- Acoustic remains a fixed NativeArray portal approximation; no managed route rebuild.
- Fauna frozen-wrap remains a distance-gated presentation cheat with voxel solid rejection.
- Hologram assembly keeps the 2D grid/edge fake and removes duplicate coordinate transform work.

Exact microseconds saved:
- Acoustic second-pass fix: 0 us measured; compile-risk removal with one stack copy per candidate node.
- Foveated distance clamp: 0 us measured; NaN guard only, no allocation.
- Hologram varying: 0 us measured by profiler because Unity is unreachable; expected GPU saving is one float4x4 multiply per affected hologram fragment.

## 2026-05-13T08:47:20+04:00

What was wrong:
- Fresh Unity log tail still contained old C# errors, but current source and manual `Hecton8.Core.rsp` compile showed those specific Core errors were stale.
- Burst failed entry-point scan because `Hecton8.Vehicles.VFX` did not exist in `Library/ScriptAssemblies`; manual VFX compile exposed the real blocker: `HullDentShaderController` referenced `SystemDispatcher.CurrentFrameUnscaledDeltaTime`, which is `internal` to Core and invisible from the VFX asmdef.
- `PlayerInventory` had a complete titanium repair signal drain method and state, but `SlowTick()` did not invoke it, leaving a dead gameplay repair path.
- Unity MCP Console remains unavailable: `read_console` returns `no_unity_session`, so no live Console-green claim is possible from tools.

What was done:
- `Assets/_Project/Scripts/Vehicles/VFX/HullDentShaderController.cs`: replaced the illegal internal dispatcher time read with `GlobalRegistry.TickDispatcher.TimeSnapshot.UnscaledDeltaTime`, finite fallback to `Time.unscaledDeltaTime`, and a 1 second clamp for hitch-safe dent repair fade.
- `Assets/_Project/Scripts/PlayerInventory.cs`: connected `DrainRepairToolTitaniumSignals()` into `SlowTick()` after salinity signal drain and before degradation/corrosion passes.
- Re-ran Unity-generated Roslyn compiles for `Hecton8.Core.rsp`, `Hecton8.Vehicles.VFX.rsp`, `Hecton8.Audio.Propagation.rsp`, and `Hecton8.AI.Foveated.rsp`.
- Re-ran all 36 non-editor/non-test runtime `Hecton8*.rsp` files; every runtime assembly exited with code 0.

Cinematic cheats used:
- Hull dents remain shader-only presentation data; no mesh deformation, no physics collider mutation, no managed simulation layer.
- Inventory titanium repair coupling uses existing `SignalBus` frame snapshot and slow tick; no per-frame scan or managed event allocation.
- VFX repair fade uses dispatcher time through the public service contract, preserving centralized tick authority.

Exact microseconds saved:
- VFX asmdef fix: 0 us measured runtime; restored compile path with one scalar sanitize on late-frame VFX tick.
- Titanium repair drain connection: estimated less than 1 us on slow tick with no signals; zero managed allocation.
- Runtime compile sweep: 0 us runtime; process verification only, prevents missing-assembly Burst failure from persisting.

Verification:
- Manual Unity `csc` runtime sweep: PASS, 36/36 non-editor/non-test `Hecton8*.rsp`.
- `git diff --check` on touched files: PASS except Git line-ending notices; no whitespace errors.
- Unity MCP Console verification: BLOCKED, `no_unity_session`.
- `Library/ScriptAssemblies` refresh: BLOCKED from tools; Unity Editor import/domain refresh must copy Bee outputs into ScriptAssemblies.

## 2026-05-13T09:05:52+04:00

What was wrong:
- Editor/test compile sweep found `Hecton8.EditModeTests.rsp` failing with CS1654 because `AcousticPortalPropagationTests` used `using` declarations for mutable native containers and then wrote through their indexers.
- `Hecton8.Editor.rsp` failed on missing `Assets/_Project/Scripts/Editor/ScreenSpaceLightShaftPrefabRepair.cs`; `git status` shows that file and its meta are tracked deletions from a concurrent change.
- The live Unity log still contains stale C# errors from before the runtime fixes, plus shader line-ending warnings and Unity Connect curl errors; MCP Console remains unavailable.

What was done:
- `Assets/_Project/Tests/Editor/AcousticPortalPropagationTests.cs`: replaced `using NativeArray/NativeList` declarations with explicit native locals and reverse-order `try/finally` disposal.
- Re-ran `Hecton8.EditModeTests.rsp`: PASS.
- Re-ran all other editor/test response files except original `Hecton8.Editor.rsp`: PASS, 5/5.
- Compiled a temporary filtered copy of `Hecton8.Editor.rsp` without the missing deleted source path: PASS. No project source or Bee artifact was modified for this probe.

Cinematic cheats used:
- Test coverage still targets the acoustic portal corner route and sealed bulkhead muffle/delay fake.
- No runtime visual/physics systems were changed in this loop.

Exact microseconds saved:
- Test fix: 0 us runtime; editor-only deterministic native disposal.
- Filtered editor compile probe: 0 us runtime; evidence separation only.

Verification:
- Runtime sweep from prior pass remains PASS, 36/36 non-editor/non-test `Hecton8*.rsp`.
- Editor/test sweep after test patch: PASS for `Hecton8.EditModeTests`, `Hecton8.PlayModeTests`, `Hecton8.Optimization.Editor`, `Hecton8.QA.Editor`, `Hecton8.UI.Editor`.
- `Hecton8.Editor.rsp`: original response file BLOCKED by stale reference to a tracked-deleted source file; filtered probe PASS.
- Unity MCP Console verification: BLOCKED, `no_unity_session`.
