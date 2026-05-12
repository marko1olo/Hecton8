# LOG_SONAR_POINT_CLOUD

## 2026-05-12 - Compute Shader Holo-Map
What was wrong: PDA sonar point cloud was tied to CPU-generated point payloads and point primitive rendering, while the batch required a GPU SDF raymarch, append buffer, and indirect quad draw. Predator AUP data existed behind the encounter director but had no decoupled GPU-buffer contract for presentation consumers.

What was done:
- Added `Assets/_Project/Art/Shaders/Hecton_SonarMap.compute` with `CSClearArgs` and `CSRaymarch`; 8x8x8 thread topology, low-tier 4x4x4 active lanes.
- Reworked `PDAMapTab` to refresh SDF texture metadata for the compute path, allocate persistent append/args/predator fallback buffers, reset append count, dispatch compute only when PDA map is visible to camera, and draw via `Graphics.DrawMeshInstancedIndirect`.
- Rebuilt `Hecton_PDA_SonarPointCloud.shader` as a camera-facing instanced quad shader with height color, ping radius discard, predator red pulse, depth fade, dithered alpha, and deterministic signal jitter.
- Added `IEncounterDirectorService.TryGetPredatorAupGpuBuffer`, implemented by `HectonDirectorAI`/`EncounterDirector`, so VFX reads predator AUP data through GlobalRegistry instead of a concrete dependency.
- Logged recon, status, and rationale files.

Cinematic cheats used:
- Sonar surface detection is a sparse sign-crossing approximation, not acoustic propagation.
- Ping scanline is a shader radius mask with dithered discard, not physical wavefront simulation.
- Signal noise is hash/frac jitter, not texture noise.
- Depth fade is dithered soft-particle approximation, not transparent sorting.

Exact microseconds saved:
- Removed CPU point-cloud payload/upload path: estimated 55-90 us on i3/MX350 refresh frames.
- Low-tier 4x4x4 active lanes vs 8x8x8: estimated 8x fewer active ray lanes when hardware tier is low/shared-memory/MX350.
- `GraphicsBuffer.CopyCount` avoids CPU readback stall: estimated 40-120 us sync risk avoided under visible PDA load.
- Camera culling skips full compute+draw when PDA is not in view: estimated 20-80 us avoided per skipped frame.
- RCP polish over divisions: estimated 2-5 us shader-lane gain under visible PDA load.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` succeeded with 0 errors and 4 pre-existing warnings.
- Unity MCP refresh timed out; console read failed because the Unity session became unavailable.
- Editor.log currently shows Unity compile blocked by `WorldChunkResidencyManager.cs` CS8156 errors, outside this domain. No current sonar C# errors in the dotnet compile.

Status: PENDING VERIFICATION because compute shader import cannot be conclusively checked until Unity session/WorldChunkResidencyManager compile wall clears.

## 2026-05-12 - Runtime Wiring Recheck
What was wrong: The compute holo-map worked as code, but the spectrum/map tab is runtime-created. Without a serialized runtime owner, the compute shader could be absent in player builds. A second pass also found that low-tier 4x4x4 mode limited predator injection to four slots, and stale SDF textures could survive transient source failure.

What was done:
- Added serialized point-cloud shader and sonar compute refs to `PlayerPDA`.
- Forwarded those refs through `PDASpectrumTab.ConfigureMapRuntimeAssets` into `PDAMapTab.ConfigurePointCloudAssets`.
- Wired `Assets/_Project/Prefabs/Player.prefab` to `Hecton_PDA_SonarPointCloud.shader` and `Hecton_SonarMap.compute` using verified `.meta` GUIDs.
- Moved compute predator injection ahead of the SDF LOD early-out, using `x + y * 8` on `z == 0` to cover all 16 predator slots.
- Added `_pointCloudSdfReady` as a separate source-validity gate so old 3D textures cannot render stale maps.
- Hardened asset/kernel resolution with one-shot lookup and `HasKernel`/`IsSupported` checks.

Cinematic cheats used:
- Low tier still fakes the cave shell with 4x4x4 SDF lanes; predator dots are cheap AUP sprites appended on GPU.
- Stale-data protection is a visibility gate, not a heavy texture clear.
- Runtime asset binding uses serialized direct refs instead of loader code.

Exact microseconds saved:
- Serialized refs remove player-build lookup/retry cost: estimated 10-40 us avoided on weak hardware during tab creation/failure paths.
- Low-tier predator fix preserves 8x fewer SDF lanes while restoring 16 visual contacts.
- Stale SDF gate avoids wasted compute+draw on invalid payload frames: estimated 20-80 us skipped when source is offline.
- One-shot shader/kernel lookup avoids repeated failure probes: estimated 3-15 us per missing-asset LateFrame avoided.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` succeeded with 0 errors and 1 unrelated `WorldSpatialHashGrid` warning.
- `git diff --check` reported no whitespace errors on the touched sonar/PDA files; only line-ending normalization warnings.
- Focused scan found no `Resources.Load`, `GetData`, or `SetData` in the touched runtime PDA files. Editor-only `AssetDatabase.LoadAssetAtPath` remains guarded by `#if UNITY_EDITOR`.
- Unity telemetry ping succeeded, but console read still failed with `Unity session not available`; compute import remains unverified in-editor.

Status: PENDING VERIFICATION until Unity session is available for shader/compute import and visual capture.

## 2026-05-12 - Bounds and Hot-Path Tightening
What was wrong: The indirect draw bounds were built from scalar width/height estimates, which can under-cull a rotated PDA panel. `EnsurePointCloudResources` also repeated a buffer bind and kernel probe on the visible frame path. Static HLSL scan found an unnecessary `float3(_GridDimensions)` constructor that increased import-risk noise.

What was done:
- Rebuilt point-cloud draw bounds from the actual four map world corners plus both depth extremes.
- Replaced approximate normal scaling with `math.rsqrt(normal.sqrMagnitude)`.
- Removed the now-dead approximate magnitude helper.
- Removed the duplicate visible-frame `_SonarPoints` buffer bind and redundant kernel probe from `EnsurePointCloudResources`.
- Simplified compute shader `_GridDimensions` usage to avoid ambiguous vector construction.

Cinematic cheats used:
- Culling remains a conservative AABB around the diegetic panel, not an expensive oriented bounds system.
- No extra point density or physics truth was added; the saved CPU submission work stays reserved for visual quality on higher tiers.

Exact microseconds saved:
- Duplicate bind/probe removal: estimated 1-4 us CPU per visible PDA frame on weak hardware.
- Oriented-corner AABB costs below 1 us but prevents whole-cloud disappearance from under-culling.
- Compute syntax cleanup has no runtime cost; it reduces shader import risk.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` succeeded with 0 warnings and 0 errors.
- Focused banned-call scan found no runtime `Resources.Load`, `GetData`, `SetData`, `Camera.main`, `FindObject`, `GameObject.Find`, or `StartCoroutine` in touched PDA runtime files.
- `git diff --check` reported no whitespace errors on touched sonar/PDA files; only line-ending normalization warnings.
- Unity MCP reports no running Unity instance (`instance_count: 0`), so compute import and visual capture remain unverified.

Status: PENDING VERIFICATION until Unity Editor import and visual proof are available.

## 2026-05-12 - Tier Hysteresis Recheck
What was wrong: The GPU point-cloud render path resolved low-tier mode twice in one visible frame. A dynamic scalability change could dispatch one density while shading as another, and immediate switches could cause PDA holo-map flicker or accidental high-tier bursts on weak hardware.

What was done:
- Added `ResolvePointCloudLowTier` with a 2-second candidate window.
- Passed the resolved tier into `DispatchSonarPointCloud` so compute density and material height colorization stay locked for the frame.
- Reset the tier gate on PDA enable to avoid stale disabled-tab state.
- Rechecked `IEncounterDirectorService` implementers; only `HectonDirectorAI` implements the interface, so the predator-buffer method has no missing implementer.

Cinematic cheats used:
- The low path remains a stable 4x4x4 SDF shell fake with 16 cheap GPU predator dots.
- The high path keeps the denser 8x8x8 shell and height color only after the tier request is stable.
- No physical sonar model or per-point CPU smoothing was added.

Exact microseconds saved:
- Hysteresis avoids transient 64-to-512 compute lane flips on MX350/i3-class hardware: estimated 15-35 us saved during tier oscillation.
- Single tier resolution avoids dispatch/material mismatch with below-1 us CPU state cost.
- Interface implementer audit found no extra virtual dispatch or duplicate predator upload path.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` succeeded with 0 warnings and 0 errors.
- `git diff --check` reported no whitespace errors on touched files; only line-ending normalization warnings.
- Focused banned-call scan found no runtime `Resources.Load`, `GetData`, `SetData`, `Camera.main`, `FindObject`, `GameObject.Find`, `StartCoroutine`, or `ToArray` in touched PDA runtime files.
- Unity console read still fails with `Unity session not available`; compute shader import and visual capture remain unverified.

Status: PENDING VERIFICATION until Unity Editor import and visual proof are available.

## 2026-05-12 - Constant Buffer and Warp Contract
What was wrong: The sonar compute dispatch still pushed several uniforms individually and assumed the HLSL thread group size from C#. That is binding overhead plus a future shader/C# drift risk.

What was done:
- Packed compute uniforms into `HectonSonarMapConstants` in `Hecton_SonarMap.compute`.
- Added a persistent 96-byte `GraphicsBuffer.Target.Constant` in `PDAMapTab` and bound it through `SetConstantBuffer` when supported.
- Kept a no-allocation fallback path using individual `SetVector` calls for unsupported constant-buffer backends.
- Replaced hardcoded dispatch group math with `ComputeShader.GetKernelThreadGroupSizes` cached at kernel resolve.
- Released the constant buffer and reset cached kernel group sizes during PDA teardown/compute replacement.

Cinematic cheats used:
- Still uses sparse SDF sign-crossing instead of physical sonar propagation.
- Low tier remains a 4x4x4 shell with full predator dot coverage.
- Packed constants buy CPU time for visual polish rather than more simulation truth.

Exact microseconds saved:
- Constant-buffer packing removes roughly five to eight Unity compute property sets on the normal visible dispatch path: estimated 3-8 us CPU on i3/MX350.
- Kernel group query has one cold metadata call and removes the risk of dispatch over/under-coverage after future HLSL numthreads tuning.
- Prior retained savings still apply: no CPU point payload, no `GetData`, no per-frame asset lookup, and stable tier hysteresis.

Verification:
- Core-only compile: `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` succeeded with 0 warnings and 0 errors.
- Full project-reference compile succeeded with 47 warnings from external Unity/third-party package projects and 0 errors; no warnings came from `Hecton8.Core` in the focused compile.
- `git diff --check` found no whitespace errors in touched sonar/PDA files; only repository CRLF normalization warnings.
- Focused banned-call scan returned no matches for runtime `Resources.Load`, `GetData`, `SetData`, `Camera.main`, `FindObject`, `GameObject.Find`, `StartCoroutine`, `ToArray`, managed collection creation, `foreach`, `string.Format`, or `.ToString` in touched runtime files.
- Unity console briefly exposed only unrelated Crest/ocean validator errors; MCP transport failed afterward because the Unity editor shut down. Editor.log contains unrelated ocean validator errors and Celestial NativeArray leak reports, no current sonar evidence.

Final diff:
- `PDAMapTab.cs`: constant-buffer upload path, fallback path, compute group-size query, constant buffer release.
- `Hecton_SonarMap.compute`: packed `HectonSonarMapConstants` cbuffer with scalar/dispatch vectors.
- Prior sonar batch changes remain: point-cloud shader, compute shader asset/meta, Player prefab wiring, GlobalRegistry encounter interface expansion, encounter-director buffer exposure, and status/rationale/recon logs.

Status: PENDING VERIFICATION until Unity can load the compute asset and a PDA holo-map visual capture can prove shader import and runtime presentation.
