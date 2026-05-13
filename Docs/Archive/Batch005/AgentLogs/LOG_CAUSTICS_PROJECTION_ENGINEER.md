# LOG_CAUSTICS_PROJECTION_ENGINEER

## 2026-05-13 Analytical Caustics Build
What was wrong: Existing caustics were texture/projection-style and not bound to live Gerstner waves, weather, AUP rebasing, depth, shadows, or quality tier policy. The architecture also had no `ICausticsService` contract or black-box telemetry for the runtime caustic state.

What was done: Added `Hecton8.Graphics.Caustics` with `AnalyticalCausticsService`, `Hecton_CausticsGenerator.compute`, and `R8G8B8A8_UNorm` global map output. Added `ICausticsService` registration through `GlobalRegistry` and bootstrap reflection. Published ocean Gerstner waves through `GlobalDataVault`. Integrated `_HectonCausticsMap` / `_HectonCausticsAUP` into `Hecton_CoreLit.hlsl`, multiplied caustics by scene-depth attenuation and main light shadowing, and suppressed the legacy projector-style manager when the registry service is active.

Cinematic Cheats used: No ray tracing, no `asin`, no `acos`. The compute shader fakes refraction from Gerstner derivatives, uses slope-driven RGB offsets for chromatic breakup, and outputs a compact 512 map. Low/MX350 disables compute and relies on the existing fragment procedural fallback. OMEGA pass replaced direct utility `normalize`/`sqrt` with `rsqrt` math and replaced reflection stride calculation with a fixed 32-byte GPU struct stride.

Exact Microseconds saved: PENDING VERIFICATION. Estimated saved cost on MX350/i3 is the full 512x512 compute dispatch because Low/MX350 never dispatches. Estimated saved cost versus legacy projector is reduced fill-rate and no per-renderer material path. Exact profiler timing cannot be recorded until unrelated compile errors are cleared.

Verification: Prompt was extracted from `Docs/Tasks/CURRENT_BATCH.md` by CLI. `AnalyticalCausticsService.cs` validates with Unity MCP at 0 warnings / 0 errors. Static scans found no `CausticsManager.Instance`, no banned `asin`/`acos`, no direct `sqrt(`/`normalize(` in the compute shader, and no `math.normalize`, `Marshal.SizeOf`, `foreach`, `string.Format`, interpolated strings, or `.ToString()` in the caustics-owned service. Unity compile is blocked by unrelated project errors, most recently `DeployableSdfDrillRuntime.cs` missing `CombatDamageSignal` members after an earlier `EcosystemDirector.cs` duplicate-method snapshot, so Vulkan/DX11 compute compile remains blocked.

## 2026-05-13 Re-Audit Upgrade Pass
What was wrong: The analytical compute path still did three full Gerstner derivative solves per pixel for RGB split. Low/Unknown tier disabled dispatch but could still allocate compute-only buffers/RT. Black-box telemetry did not include hashes and could dump repeatedly on persistent non-finite state. Unity also caught a real D3D11 compute shader error from using `line` as a local token.

What was done: Reworked chromatic aberration to one Gerstner solve plus cheap derivative-driven RGB edge weights. Added tier wave caps: Unknown/Low/MX350 = 0 compute, Mid = 8 waves, High = 12 waves, Ultra = 16 waves. Moved compute scratch, GPU buffer, and `R8G8B8A8_UNorm` RT allocation to lazy Mid+ dispatch path. Added state/context hashes and dispatch wave count to the 300-frame telemetry ring, and gated crash dumps to once per non-finite incident. Renamed compute local `line` to `ridgeLine`.

Cinematic Cheats used: Chromatic aberration is now a refraction-vector color fake rather than two extra wave simulations. Low-tier caustics are fragment-only. High/Ultra spend saved wave-loop cost on live 12/16-wave analytical response instead of three redundant color solves.

Exact Microseconds saved: PENDING VERIFICATION. Analytical pass math was cut from three full wave loops to one per pixel. On a 512 map at 16 waves, that removes 8,388,608 wave iterations per dispatch before accounting for sincos cost. MX350/i3 still save the whole dispatch plus compute-only allocations.

Verification: Static scans found no banned `asin`/`acos`, no direct `sqrt(`/`normalize(`, no `float line`, no `math.normalize`, no `Marshal.SizeOf`, no `foreach`, no `string.Format`, no interpolated strings, and no `.ToString()` in the caustics-owned hot path. Unity MCP `validate_script` reports 0 warnings / 0 errors for `AnalyticalCausticsService.cs`. Unity console now reports only unrelated `WorldChunkResidencyManager.cs(400,142): CS0535` for `IStreamingBackpressureService.IsChunkImpostorAudioMuted(long)`; no caustics errors remain in the current console snapshot.

## 2026-05-13 Resource-Policy Upgrade Pass
What was wrong: The compute shader was under `Assets/Resources` and the service had a `Resources.Load` fallback. That violates project loading policy and hides dependency ownership from bootstrap. The abyss depth gate also had no hysteresis, so a boundary hover could flip dispatch state repeatedly.

What was done: Moved `Hecton_CausticsGenerator.compute` to `Assets/_Project/Art/Shaders` with `.meta` preserved. Removed the runtime `Resources.Load` path. Added `GameBootstrapper.analyticalCausticsCompute` for serialized compute injection into `AnalyticalCausticsService.AssignComputeShader`, with `try/finally` cleanup for the cold reflection scratch. Added a missing-kernel latch and -100m disable / -95m enable hysteresis for abyss gating.

Cinematic Cheats used: The shader remains a live Gerstner-derivative fake, not ray traced light transport. Low/MX350 keeps fragment fallback only. Mid/High/Ultra explicitly bind the analytical compute through bootstrap instead of searching globally at runtime.

Exact Microseconds saved: PENDING VERIFICATION. Removed hidden runtime asset lookup from the service path. The hysteresis prevents repeated compute resource release/rebuild when the player oscillates around abyss cutoff. Full profiler timing remains blocked by the external World/Streaming compile error.

Verification: Static scan confirms no `Resources.Load` in `Assets/_Project/Scripts/Graphics/Caustics` or the caustics bootstrap path. `Assets/_Project/Art/Shaders/Hecton_CausticsGenerator.compute` and `.meta` exist; the old `Assets/Resources/Hecton_CausticsGenerator.compute` path is absent. Unity imports the moved compute shader as `UnityEngine.ComputeShader` with GUID `27b7cf5d630bd8d4dbc699ff38f19ac2`. `AnalyticalCausticsService.cs` validates with 0 diagnostics. Current Unity console has no caustics entries; full project compile is blocked by non-caustics `H8MacroDatabaseService.cs` unsafe-await errors and fileless Worker1 import errors. Scene binding moved to the authored `BootstrapController` because `GameBootstrapper` is runtime-added.

## 2026-05-13 Bootstrap Binding Completion Pass
What was wrong: `00_BOOTSTRAP` contains `BootstrapController`; `GameBootstrapper` is added dynamically at runtime. The previous serialized compute slot on `GameBootstrapper` could not be authored in the scene, leaving the analytical compute path dependent on a null reference even though the asset was imported correctly.

What was done: Added a scene-owned `BootstrapController.analyticalCausticsCompute` field and bound it in `00_BOOTSTRAP.unity` to compute GUID `27b7cf5d630bd8d4dbc699ff38f19ac2`. Added a cold `GameBootstrapper` adoption path that copies this reference from `BootstrapController` into the runtime bootstrapper before caustics service registration. The no-owner bootstrap path now resolves the bootstrap scene controller first, preventing a race where bootstrap starts before the compute reference handoff.

Cinematic Cheats used: No runtime asset lookup and no physical light transport. The compute reference is deterministic scene data; Low/MX350 still use shader fallback only, while Mid+ can activate analytical caustics from the bootstrap-owned compute shader.

Exact Microseconds saved: 0 us/frame. This is a correctness/perf-policy fix: it removes hidden loading and avoids a permanent fallback caused by null compute binding. The only new query is a cold bootstrap-scene owner lookup.

Verification: `BootstrapController.cs` validates with Unity MCP at 0 warnings / 0 errors. `AnalyticalCausticsService.cs` still validates at 0 warnings / 0 errors. Static scan confirms the bootstrap scene references `Hecton_CausticsGenerator.compute` by GUID and no caustics `Resources.Load` path exists. `GameBootstrapper.cs` validation remains blocked by its existing validator duplicate-signature report for `ResolveUnityTestRunnerProcess`, while the Unity console currently reports non-caustics `GlobalDataVault.cs` errors only.

## 2026-05-13 Abyss Semantics Pass
What was wrong: The abyss gate stopped compute dispatch, but `_HectonProjectedCausticsParams.x` could remain positive. That meant CoreLit could fall back to procedural caustics below -100m, contradicting the no-sunlight rule.

What was done: `PublishShaderGlobals` now receives the depth-gate state and publishes zero intensity while abyss-gated. `PublishDisabledGlobals` clears projected caustic params as well as runtime active state, preventing stale fallback light after disable/destroy.

Cinematic Cheats used: Low-tier shallow water still uses the cheap procedural fake. Abyss uses the cheapest and correct fake: no caustic contribution at all.

Exact Microseconds saved: PENDING VERIFICATION. In abyss, the dispatch was already skipped; this also removes fragment fallback caustic energy. Exact GPU savings require a clean project compile and profiler pass.

Verification: `AnalyticalCausticsService.cs` validates with Unity MCP at 0 warnings / 0 errors after the patch.

## 2026-05-13 Resource Lifecycle Recovery Pass
What was wrong: Compute-resource release on Low/MX350 or abyss disposal removed the wave scratch buffer, but the cached ocean wave version survived. If the ocean vault version did not change before re-enable, caustics could skip repopulating the new scratch buffer and upload zeroed waves.

What was done: `AnalyticalCausticsService` now invalidates `_lastWaveMetaVersion` when allocating a new wave scratch buffer and when releasing compute-only resources. The next Mid+ dispatch after a depth/tier recovery must refill the scratch from `GlobalDataVault` before upload.

Cinematic Cheats used: The aggressive release policy remains intact: weak hardware and abyss states still pay no compute RT/buffer cost, while Mid+ restores the analytical fake from live Gerstner data instead of falling back to a static authored texture.

Exact Microseconds saved: 0 us/frame steady state. This prevents a correctness failure without adding per-frame copying. The only extra cost is a bounded 16-wave refill on resource re-enable.

Verification: Prompt re-extracted by CLI. Static scans found no caustics `Resources.Load`, no banned Snell trig, and no hot-path allocation patterns in `AnalyticalCausticsService.cs`. Unity MCP validates `AnalyticalCausticsService.cs` at 0 warnings / 0 errors. Unity console filter for `Caustics` returns 0 entries; full project compile is still blocked outside VFX by `PlayerKinematicsRuntime.cs` missing `MethodImpl` imports and fileless import errors.
