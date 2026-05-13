# LOG_HLOD_IMPOSTOR_BAKER

## 2026-05-13 HLOD Octahedral Impostor System

What was wrong:
- Horizon bases/wrecks at 2 km would require far-field triangle rendering and per-object LOD churn that does not fit MX350 budget.
- Existing `ImpostorSystem` is GameObject/material-instance oriented and Amplify-gated, not a native indirect draw path.
- `ChunkState` has no spare byte bit for LOD2, so adding a direct enum flag would corrupt state semantics.

What was done:
- Added `HECTON-8/Bake HLOD Impostor` editor baker.
- Added 8-view upper/lower octa capture into 2048x2048 atlas assets: albedo/alpha and normal/depth.
- Added `HectonOctahedralImpostorData` and unmanaged `OctahedralImpostorInstance`.
- Added `Hecton_Impostor.hlsl`, `Hecton_OctahedralImpostor.shader`, and editor normal/depth bake shader.
- Added `HectonOctahedralImpostorRenderer` using persistent native upload storage and `Graphics.RenderMeshIndirect`.
- Added `WorldChunkResidencyManager` slow-tick native publish buffer for chunks beyond 500m/default impostor radius.
- Added `CrashTelemetryBuffer.ReportActiveImpostors` black-box ring entry.

Cinematic cheats used:
- 8 baked atlas views replace far 3D geometry.
- IGN dither switches between neighboring views on non-low tiers; Low/MX350 snaps to nearest tile.
- Baked normal/depth fakes headlight response and occlusion bias on a flat quad.
- Fixed world-up billboard basis prevents roll-coupled horizon swim.

Exact microseconds saved:
- Exact measured savings: BLOCKED. Unity global compile is currently failing outside this work, preventing playmode/profiler capture.
- Static budget estimate: runtime camera capture/readback removed = multi-ms avoided per bake candidate at runtime.
- Static budget estimate: CPU tile switching removed = 0 per-instance CPU uploads per frame.
- Static budget estimate: Transform billboard churn removed = 200-1000 us per 500 impostors on low-end CPU class, pending profiler proof.
- Static budget estimate: swap path is one NativeArray publish and one indirect draw; target under 100 us per 512 chunks on slow tick, pending profiler proof.

Verification:
- `validate_script`: 0 diagnostics for `HectonOctahedralImpostorRenderer.cs`.
- `validate_script`: 0 diagnostics for `HectonOctahedralImpostorData.cs`.
- `validate_script`: 0 diagnostics for `HectonOctahedralImpostorTypes.cs` on retry/basic.
- `validate_script`: 0 diagnostics for `HectonOctahedralImpostorBaker.cs`.
- `validate_script`: 0 diagnostics for `WorldChunkResidencyManager.cs`.
- Unity console filter: 0 `HectonOctahedral` / `Hecton_Octahedral` errors.
- Global compile blocked by unrelated files; latest observed external errors are in `SimulationBucketingContracts.cs` and `DeployableSdfDrillContracts.cs`.

Status:
- Core tasks 1-17 complete.
- Task 18 marked BLOCKED BY DEPENDENCY for global compile/profiler proof.
- Earlier polish query was too strict for attributed tags; Loop 7 re-read and applied `<POLISH_MANDATE id="OMEGA_POLISH">`.

## 2026-05-13 Continued Recheck / Loop 6

What was wrong:
- WCRM had a dead persistent `_lod2ImpostorInstances` array left behind after the newer signal-driven HLOD matrix path became the active authority.
- Renderer tick was still writing matrix-stream mode and fade timing through `Material.Set*` every frame, despite the shader consuming those as globals.
- Unity MCP validation became unavailable: script validate calls timed out/disconnected, and editor readiness waits hit 60-second timeouts.

What was done:
- Removed the obsolete WCRM `NativeArray<OctahedralImpostorInstance>` field, allocation, sentinel registration, and disposal.
- Kept the HLOD matrix packing unchanged after verifying WCRM, `InstanceCulling.compute`, and `Hecton_Impostor.hlsl` agree on center, radius, flags, and spawn-time fields.
- Moved `_HectonUseVisibleMatrixStream`, `_HectonImpostorTimeSeconds`, and `_HectonImpostorFadeOutSeconds` to `Shader.SetGlobal*` updates.
- Avoided fallback quad/args allocation from `ClearBinding()`; the renderer now only rebuilds indirect args when a draw path exists.

Cinematic cheats used:
- Same 8-view octa impostor lie; no new physical simulation added.
- Matrix stream now feeds the GPU culling visible buffer directly, preserving one indirect draw for the far skyline.

Exact microseconds saved:
- Removed one dead native array: estimated 128 bytes per configured chunk plus sentinel overhead, about 64 KB at 512 chunks.
- Moved three per-frame material property writes off the material path; exact CPU microseconds are not measured because global compile/playmode is still blocked.
- Avoided cold fallback quad/args work during renderer clear; exact savings depend on clear frequency.

Verification:
- `rg` confirms `_lod2ImpostorInstances` has zero remaining references.
- `rg` confirms renderer now uses `Shader.SetGlobalInt/Float` for matrix-stream mode and fade timing.
- Local `dotnet build Assembly-CSharp.csproj --no-restore` still fails, but on unrelated project-wide missing namespaces/types and stale generated csproj state; the probe also shows new untracked Unity scripts are not yet represented in generated csproj until Unity project sync/import recovers.
- Unity MCP `validate_script` and `read_console` retries failed because the Unity plugin session was not ready/disconnected.

Status:
- Implementation upgraded.
- Global compile/profiler proof remains BLOCKED BY DEPENDENCY, not claimed green.

## 2026-05-13 OMEGA Polish Pass / Loop 7

What was wrong:
- Fallback impostor instances still carried unused matrix/depth/flag lanes in the GPU payload.
- Renderer startup performed cold buffer/mesh/args allocation before any far impostor batch existed.
- Editor baker used an extra tile texture plus `GetPixels` tile copies.
- Runtime atlas addressing used divide/modulo despite the fixed 4x2 atlas topology.

What was done:
- Packed `OctahedralImpostorInstance` to explicit 32-byte layout: `CenterFade` + `SizeFlags`.
- Made renderer buffers, fallback quad, and local indirect args lazy; matrix-stream culling no longer requires fallback `_instanceBuffer`.
- Replaced editor tile-copy path with direct atlas `ReadPixels` into the destination tile and guarded render-state restoration with `try/finally`.
- Replaced atlas tile modulo/divide with `viewIndex & 3u` and `viewIndex >> 2`.

Cinematic cheats used:
- 8-view octa atlas, fixed 4x2 addressing.
- World-up camera-facing billboards.
- Non-low dither only; Low/MX350 nearest-angle snap.
- Baked normal/depth lighting and depth bias on one quad.

Exact microseconds saved:
- Exact measured frame savings: BLOCKED by project compile state.
- Static payload win: fallback structured buffer drops from 112 bytes to 32 bytes per instance, about 40 KB saved at 512 fallback impostors.
- Static startup win: renderer no longer allocates upload buffer, indirect args, or fallback mesh during `Awake`.
- Static editor win: removes one tile `Texture2D` and 16 `GetPixels` copies per bake.
- Static shader win: one atlas modulo/divide pair replaced by bitmask/shift.

Verification:
- OMEGA mandate re-read from `CURRENT_BATCH.md`.
- `rg` found no remaining stale payload names, tile-copy allocation path, `.normalized`, foreach/string formatting, or dead WCRM buffer in touched files.
- `dotnet build Hecton8.Core.csproj --no-restore` remains red due unrelated missing namespaces/types; latest probe still includes generated csproj not seeing the new untracked renderer script.
- Unity MCP validation remains unavailable because there is no active Unity session.

Status:
- Polish applied.
- STATUS remains PENDING VERIFICATION because global compile/profiler proof is blocked externally.

## 2026-05-13 Fixed-Atlas Inquisition / Loop 8

What was wrong:
- `_AtlasGrid` and `_DepthScale` survived in the shader/material path after the atlas topology was already fixed to 4x2.
- Renderer and editor baker still carried property IDs/writes for those values, creating avoidable material state and authoring churn.

What was done:
- Removed `_AtlasGrid` and `_DepthScale` from `Hecton_OctahedralImpostor.shader` properties and UnityPerMaterial CBUFFER.
- Changed `HectonImpostorAtlasUv` to fixed constants; Loop 10 corrected the final scale to `float2(0.25, 0.25)` for the square-cell atlas.
- Removed renderer `AtlasGridId`, `DepthScaleId`, `_lastData`, and the associated material writes.
- Removed editor baker `_AtlasGrid` and `_DepthScale` material writes and the now-unused `data` parameter.

Cinematic cheats used:
- The atlas is treated as a contract, not a dynamic system: 8 fixed octa views in 4x2 occupied square cells.
- Depth remains a baked alpha bias on the normal/depth atlas; no mesh shell or runtime geometry was added.

Exact microseconds saved:
- Exact measured frame savings: BLOCKED by global compile/playmode state.
- Static CPU win: two material writes removed on impostor data/material changes.
- Static SRP-batcher win: two unused material constants removed from the impostor CBUFFER.
- Static shader win: no atlas-grid reciprocal or max guard in tile mapping.

Verification:
- `rg` confirms no `_AtlasGrid`, `_DepthScale`, `AtlasGridId`, `DepthScaleId`, or `_lastData` remain in the touched HLOD renderer/shader/editor path.
- `rg` confirms the active atlas UV function now uses fixed `float2(0.25, 0.25)` and the shader call passes only `uv` and `selectedView`.
- Unity MCP `validate_script` reports 0 diagnostics for `HectonOctahedralImpostorRenderer.cs`, `HectonOctahedralImpostorBaker.cs`, and `HectonOctahedralImpostorTypes.cs`.
- Unity console filters for `HectonOctahedral`, `Hecton_Octahedral`, and `Hecton_Impostor` return 0 errors.
- `dotnet build Hecton8.Core.csproj --no-restore --nologo --verbosity:minimal` still fails with 92 project-wide errors in unrelated missing namespaces/types and generated csproj staleness for the new renderer.

Status:
- Implementation upgraded again.
- STATUS remains PENDING VERIFICATION.

## 2026-05-13 Safe Normal Bake / Loop 12

What was wrong:
- Normal/depth bake shader used raw `normalize(input.normalWS)`.
- Malformed or zero-length source normals could bake undefined normal values into the shared atlas.

What was done:
- Replaced raw normalize with explicit length-squared guard and `rsqrt`.
- Invalid normals now bake as world-up instead of undefined data.

Cinematic cheats used:
- Runtime still uses baked normal response on one quad; the bake now sanitizes bad source normals before the cheat is stored.

Exact microseconds saved:
- Runtime frame savings: no change.
- Editor-only ALU cost increases slightly; accepted to prevent NaN/lighting artifacts in the atlas.

Verification:
- `rg` confirms no `normalize(` remains in `Hecton_EditorOctaImpostorNormalDepth.shader`.
- Unity console reads for that shader timed out because the MCP session was not ready.

Status:
- Implementation hardened.
- STATUS remains PENDING VERIFICATION.

## 2026-05-13 Matrix Fallback Bounds / Loop 11

What was wrong:
- `BindNativeMatrices` could fall back before `ResolveMatrixBounds` if the GPU-culling matrix source buffer was unavailable.
- That path could render with stale/default draw bounds.

What was done:
- `BindMatricesAsOctahedralFallback` now computes combined bounds while converting matrices into packed impostor instances.
- The fallback path now assigns `_drawBounds` and `_hasBoundsOverride` directly.

Cinematic cheats used:
- Same single-quad far impostor lie; the fallback path now gets correct culling volume instead of relying on a previous path.

Exact microseconds saved:
- Exact measured frame savings: BLOCKED by global compile/playmode state.
- Static correctness win: prevents bad culling/overdraw in degraded GPU-culling fallback.
- Cost is bind-time only, not per-frame `Tick`.

Verification:
- First Unity `validate_script` disconnected mid-command; retry reports 0 diagnostics for `HectonOctahedralImpostorRenderer.cs`.
- Console filter for `HectonOctahedral` returns 0 errors.
- `rg` confirms `BindMatricesAsOctahedralFallback` now writes combined bounds.

Status:
- Implementation upgraded again.
- STATUS remains PENDING VERIFICATION.

## 2026-05-13 Stable-Fade Hot Path / Loop 9

What was wrong:
- Fade dithering still computed interleaved gradient noise for stable, fully visible impostors.
- That makes a transition-only visual cheat part of the permanent fragment path.

What was done:
- Added a stable-fade guard in `Hecton_OctahedralImpostor.shader`.
- Fade dither and fade `clip` now run only when `input.fade01 < 0.999h`.
- Fully visible impostors keep alpha clip, depth bias, fixed atlas selection, and normal/depth lighting without transition noise.

Cinematic cheats used:
- Dithered residency fade remains during swap-in/swap-out.
- Stable far silhouettes use the cheaper hard-present state.

Exact microseconds saved:
- Exact measured frame savings: BLOCKED by global compile/playmode state.
- Static fragment win: one IGN dot/fract sequence and one fade clip removed for stable impostor fragments.
- Transition cost is preserved only during fade windows.

Verification:
- `rg` confirms the fade guard is present and fixed atlas constants remain.
- `refresh_unity(scope=assets, compile=none, wait_for_ready=true)` timed out after 60 seconds waiting for editor readiness.
- Console filter for `Hecton_Impostor` returned 0 entries on retry; `Hecton_Octahedral` console reads timed out due Unity session readiness.
- Renderer `validate_script` still reports 0 diagnostics.

Status:
- Implementation upgraded again.
- STATUS remains PENDING VERIFICATION.

## 2026-05-13 Atlas Contract and Bake Truth / Loop 10

What was wrong:
- Runtime alpha was multiplied by `_BaseColor.a` twice before alpha clip.
- Dead depth magnitude/max calculations survived after the fallback payload was packed to 32 bytes.
- Shader UVs treated the 2048 atlas as two 1024-high rows while the baker writes 512-square captures into two occupied rows.
- Albedo baking used the source material path, risking baked lighting and LOD-biased captures.

What was done:
- Split runtime albedo sampling so alpha is tinted once and RGB tint is applied only to color.
- Removed unused depth magnitude/max work from data and renderer conversion paths.
- Corrected atlas contract to a 2048x2048, 4x4 square-cell lattice with 4x2 occupied cells; shader tile scale is now `0.25 x 0.25`.
- Added `Hecton_EditorOctaImpostorAlbedoAlpha.shader` for unlit albedo/alpha capture.
- Baker now forces cloned `LODGroup`s to LOD0 before capture.

Cinematic cheats used:
- Unlit editor albedo/alpha truth plus runtime fake headlight response from baked normal/depth.
- 8 occupied octa views remain packed in square atlas cells.
- Stable fade, fixed-up billboard, and low-tier nearest-view snap remain.

Exact microseconds saved:
- Exact measured frame savings: BLOCKED by global compile/playmode state.
- Static CPU win: one `Vector3.magnitude` sqrt and two dead max-depth chains removed from conversion paths.
- Static runtime correctness win: fixed atlas UV scale costs the same ALU but samples the intended tile.
- Static visual win: albedo no longer bakes scene lighting for later double-lighting.

Verification:
- `validate_script` reports 0 diagnostics for baker, data, and renderer.
- Console filters for `Hecton_Impostor`, `Hecton_Octahedral`, `OctahedralImpostorAlbedoAlpha`, and `HectonOctahedral` return 0 errors.
- `rg` confirms no stale `TileSize`, `AtlasRows`, `_Cutoff`, or dead depth magnitude/max calculation remains in touched HLOD files; runtime shader now samples albedo raw and applies `_BaseColor.a` once to alpha.

Status:
- Implementation upgraded again.
- STATUS remains PENDING VERIFICATION.
