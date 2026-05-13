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
