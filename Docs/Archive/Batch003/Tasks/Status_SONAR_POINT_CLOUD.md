# Status_SONAR_POINT_CLOUD

Agent: VFX_TECHNICAL_ARTIST  
Domain: PRESENTATION & UX / GPU VFX HOLOGRAPHIC CARTOGRAPHY  
Source prompt: Docs/Tasks/CURRENT_BATCH.md `<AGENT_PROMPT id="SONAR_POINT_CLOUD">`  
Status: PENDING VERIFICATION

## Loop 1 - Prompt, Domain, Mandates
- [x] Extract XML prompt cover-to-cover. DOD: regex extraction from CURRENT_BATCH.md, task count verified at 15. Rejected: IDE tab memory. Estimate: 20 us.
- [x] Domain boundary read. DOD: Actual Domains file read before edits. Rejected: broad VFX ownership assumption. Estimate: 8 us.
- [x] Mandates read. DOD: GPU compute, descriptor binding, URP hotpath, zero GC, frame budget, noir shader, AUP precision, warp sizing. Rejected: CPU point-cloud continuation. Estimate: 25 us.

## Loop 2 - Tasks 1-5
- [x] Task 1: Wrote `Assets/_Project/Art/Shaders/Hecton_SonarMap.compute` with `CSRaymarch` `[numthreads(8,8,8)]`. DOD: one dispatch group covers 8x8x8, low tier early-outs at axis 4. Rejected: CPU/Burst grid generation. Estimate: 18 us GPU.
- [x] Task 2: Compute samples `_VoxelSdfTexture3D` and records sign transitions around density 0. DOD: previous/current SDF sign crossing with interpolated hit. Rejected: passability-cell occupancy sampling. Estimate: 20-45 us GPU.
- [x] Task 3: Valid hits append local PDA-space positions to a persistent append buffer. DOD: `GraphicsBuffer.Target.Append`, local position from player-relative SDF hit. Rejected: `NativeArray` upload. Estimate: 4 us CPU saved per dispatch plus upload avoidance.
- [x] Task 4: Indirect quad draw implemented in `PDAMapTab.RenderPointCloud`. DOD: single quad mesh, `Graphics.DrawMeshInstancedIndirect`, material reads `StructuredBuffer<float4>`. Rejected: `Graphics.RenderPrimitives` point topology. Estimate: 35-70 us CPU/GPU overhead saved on refresh frames.
- [x] Task 5: Ping scanline cheat implemented. DOD: `_AcousticPingSignal` drives radius mask and dither clip in vertex/fragment path. Rejected: physical acoustic propagation. Estimate: 6 us shader cost, buys visual scanline.
- [x] Loop 2 compile check: `dotnet build Hecton8.Core.csproj --no-restore ...` succeeded after removing stale CPU scaffold. Warnings are pre-existing audio/world unused-field warnings.

## Loop 3 - Tasks 6-10
- [x] Task 6: Height colorization implemented in shader. DOD: local Y maps deep blue to red/orange, predator overrides red. Rejected: CPU-authored color buffer. Estimate: 2 us shader.
- [x] Task 7: AUP/player offset handled in compute sampling. DOD: player runtime position plus SDF volume origin/cell size transforms world sample into texture UVW. Rejected: absolute world mesh baking. Estimate: 0 allocations.
- [x] Task 8: Memory recycling implemented. DOD: append buffer, args buffer, predator fallback, material, quad mesh are persistent; per-frame `SetCounterValue(0)` and clear-args kernel reset draw count. Rejected: per-frame buffer rebuild. Estimate: 40-90 us CPU saved.
- [x] Task 9: Camera culling implemented. DOD: no compute dispatch unless PDA map frame resolves and camera forward points at map. Rejected: unconditional LateFrame dispatch. Estimate: full dispatch skipped when PDA not viewed.
- [x] Task 10: Low-tier Math LOD implemented. DOD: Low/Unknown/MX350/shared-memory path uses 4x4x4 live threads and disables height colorization. Rejected: balanced middle tier. Estimate: 8x fewer active lanes on low path.
- [x] Loop 3 compile check: focused grep shows no `SetData`, `GetData`, `RenderPrimitives`, or CPU point payload path in `PDAMapTab.cs`.

## Loop 4 - Tasks 11-15
- [x] Task 11: Predator AUP injection implemented through `IEncounterDirectorService.TryGetPredatorAupGpuBuffer`. DOD: UI binds existing GlobalRegistry encounter buffer, compute appends red pulsing dots. Rejected: scene search/direct EncounterDirector dependency. Estimate: <2 us CPU bind, 16 max GPU dots.
- [x] Task 12: Depth fade implemented. DOD: point-cloud shader samples scene depth and dither-fades quads against PDA glass. Rejected: transparent blend overdraw. Estimate: 1 depth sample per quad fragment.
- [x] Task 13: Zero CPU bottleneck verified. DOD: `GraphicsBuffer.CopyCount(_pointCloudAppendBuffer, _pointCloudIndirectArgsBuffer, sizeof(uint))`; no CPU readback. Rejected: `GetData` point count. Estimate: avoids CPU/GPU sync stall.
- [x] Task 14: Recon scan logged to `Docs/AgentLogs/RECON_SONAR_POINT_CLOUD.md`. DOD: scanned `Assets/_Project/Scripts/UI` for `MeshFilter`, sonar, map, hologram references. Rejected: relying on prior docs only. Estimate: 12 us.
- [BLOCKED BY DEPENDENCY] Task 15: Omega compute shader import/syntax. DOD attempted: Unity refresh triggered via MCP and Editor.log scanned. Blocker: Unity session unavailable after refresh timeout; Unity script compilation is currently blocked by `WorldChunkResidencyManager.cs` CS8156 errors outside this domain. Static scan found compute kernels/properties, and C# `dotnet build` succeeds. Rejected: editing world/audio compile errors outside assigned domain. Estimate: blocked.

## Loop 5 - Recursive Reverification
- [x] Re-read XML prompt after implementation. DOD: extracted SONAR_POINT_CLOUD tag again from CURRENT_BATCH.md. Rejected: memory-only checklist. Estimate: 20 us.
- [x] Billboard audit complete. DOD: shader uses camera basis from `UNITY_MATRIX_I_V` plus `SafeNormalize`, equivalent to safe billboard basis. Rejected: CPU-facing quads. Estimate: 0 CPU.
- [x] Signal noise added. DOD: deterministic per-instance frac jitter driven by `_AcousticPingSignal.z`; no texture/noise buffer. Rejected: animated CPU jitter upload. Estimate: 3 ALU ops per vertex.
- [x] Status remains PENDING VERIFICATION per prompt. DOD: compile/import dependency prevents VERIFIED status. Rejected: fake green report.

## Omega Gate
- [x] Core tasks are checked or blocked. POLISH_MANDATE may now be parsed.

## Loop 6 - Runtime Wiring and Regression Fixes
- [x] Rechecked runtime-created PDA tab chain. DOD: `PlayerPDA` now forwards serialized shader/compute refs to `PDASpectrumTab`, and `PDASpectrumTab` forwards them to the runtime-created `PDAMapTab`. Rejected: `Resources.Load` and repeated player-build asset lookup. Estimate: saves 10-40 us startup/search risk and prevents missing compute in player.
- [x] Wired `Assets/_Project/Prefabs/Player.prefab` to `Hecton_PDA_SonarPointCloud.shader` and `Hecton_SonarMap.compute`. DOD: GUID/fileID refs match `.meta` importers (`4800000` shader, `7200000` compute). Rejected: relying on editor-only AssetDatabase fallback. Estimate: 0 us runtime lookup.
- [x] Fixed low-tier predator slot coverage. DOD: compute injects up to 16 predator AUP points before SDF LOD lane early-out, while low-tier SDF sampling remains 4x4x4. Rejected: raising low-tier grid to 8x8x8 only for predators. Estimate: keeps 8x fewer SDF lanes and restores 16 red contacts.
- [x] Added stale SDF readiness gate. DOD: point-cloud draw requires `_pointCloudSdfReady`, and EMP/offline/payload failures clear it. Rejected: destroying/recreating the 3D texture on transient source failure. Estimate: avoids false holo-map frames with no new allocation.
- [x] Hardened compute/material lookup. DOD: point-cloud asset lookup is one-shot, compute kernel resolution checks `HasKernel` and `IsSupported`, and serialized refs reset the kernel cache when changed. Rejected: repeated `Shader.Find`/kernel probing every LateFrame. Estimate: saves 3-15 us on missing-asset frames.
- [x] Loop 6 compile check: `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` succeeded with 0 errors and 1 unrelated `WorldSpatialHashGrid` warning. Unity console read still blocked by `Unity session not available`.

## Loop 7 - Bounds and Hot-Path Tightening
- [x] Re-extracted SONAR_POINT_CLOUD prompt and re-read GPU/URP/zero-GC/AUP/UI mandates. DOD: primary task count remains 15. Rejected: memory-only continuation. Estimate: 20 us.
- [x] Fixed indirect draw culling bounds. DOD: `TryResolvePointCloudFrame` now builds AABB from all four map corners plus depth offset, so rotated diegetic PDA panels are not under-culled. Rejected: scalar width/height AABB approximation. Estimate: prevents missing quads with ~7 struct `Encapsulate` calls.
- [x] Replaced approximate normal scaling with `math.rsqrt(normal.sqrMagnitude)`. DOD: exact no-sqrt normalization for PDA depth axis. Rejected: biased L1-ish magnitude approximation. Estimate: same order CPU cost, better culling correctness.
- [x] Removed redundant per-visible-frame buffer bind/kernel probe from `EnsurePointCloudResources`. DOD: `_SonarPoints` binding remains at material creation and draw submission only. Rejected: duplicate `SetBuffer` before every `RenderPointCloud`. Estimate: saves 1 buffer bind and one resolved-kernel branch per visible PDA frame.
- [x] Simplified compute shader `_GridDimensions` cast. DOD: removed `float3(_GridDimensions)` constructor risk; shader now uses `_GridDimensions` directly. Rejected: leaving avoidable import ambiguity. Estimate: verification risk reduction.
- [x] Loop 7 verification: `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` succeeded with 0 warnings and 0 errors. Focused banned-call scan found no runtime `Resources.Load`, `GetData`, `SetData`, `Camera.main`, `FindObject`, `GameObject.Find`, or `StartCoroutine` in touched PDA runtime files. Unity MCP reports `instance_count: 0`; compute import remains PENDING VERIFICATION.

## Loop 8 - Tier Hysteresis and Integration Recheck
- [x] Re-read status, rationale, prompt-derived mandates, and current code before editing. DOD: local files were treated as source of truth after context compaction. Rejected: chat-memory continuation. Estimate: 20 us.
- [x] Stabilized point-cloud Math LOD. DOD: `RenderPointCloud` now resolves one low-tier decision through `ResolvePointCloudLowTier`, passes it into `DispatchSonarPointCloud`, and reuses the same value for shader height colorization. Rejected: separate compute/material tier reads in one visible frame. Estimate: prevents 64/512-lane dispatch mismatch.
- [x] Added 2-second tier hysteresis and enable reset. DOD: transient scalability flips must persist before changing dispatch axis; PDA re-enable starts from current requested tier. Rejected: immediate quality toggles that can flicker on dynamic tier changes. Estimate: avoids 15-35 us transient high-tier bursts on weak silicon.
- [x] Rechecked encounter interface ownership. DOD: only `HectonDirectorAI` implements `IEncounterDirectorService`, so the new predator-buffer method has no missing implementation debt. Rejected: broad cross-domain scan edits. Estimate: 5 us verification.
- [x] Loop 8 verification: `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` succeeded with 0 warnings and 0 errors. `git diff --check` found no whitespace errors on touched files, only line-ending normalization warnings. Focused banned-call scan found no runtime `Resources.Load`, `GetData`, `SetData`, `Camera.main`, `FindObject`, `GameObject.Find`, `StartCoroutine`, or `ToArray`. Unity console read is still blocked by `Unity session not available`; compute import remains PENDING VERIFICATION.

## Loop 9 - Constant Buffer and Warp Contract
- [x] Re-read status, rationale, prompt, domain, and mandates before continuation. DOD: attribute-aware CLI extraction found the exact `SONAR_POINT_CLOUD` prompt with 15 tasks; GPU compute, descriptor binding, zero-GC, URP, noir, performance, and AUP mandates were re-opened. Rejected: strict id-only XML regex and memory-only continuation. Estimate: 25 us.
- [x] Packed compute uniforms into `HectonSonarMapConstants`. DOD: grid, volume, cell size, player position, scalar params, and dispatch params now live in one 96-byte constant buffer upload when supported. Rejected: six separate compute `SetVector`/`SetFloat` calls on the normal path. Estimate: 3-8 us CPU saved per visible dispatch on i3/MX350.
- [x] Added compatibility fallback. DOD: if `SystemInfo.supportsSetConstantBuffer` is false or the constant buffer is invalid, the code uses the old individual `SetVector` path without allocation. Rejected: constant-buffer-only path that could fail on unsupported graphics backends. Estimate: 0 regression on fallback devices.
- [x] Replaced hardcoded compute group math with kernel query. DOD: `GetKernelThreadGroupSizes` caches the live `CSRaymarch` group dimensions and dispatch counts use integer ceil division. Rejected: `(dispatchAxis + 7) >> 3` drift from HLSL `[numthreads]`. Estimate: verification and portability gain; runtime cost is one kernel-query on resolve, not per dispatch.
- [x] Released new GPU resource. DOD: `_sonarMapConstantsBuffer` releases in `ReleaseResources`, and kernel/group-size cache resets when compute refs change. Rejected: relying on domain reload cleanup. Estimate: prevents 96-byte GPU leak per PDA lifetime.
- [x] Loop 9 verification: `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` succeeded with 0 warnings and 0 errors. Full project-reference build succeeded with 47 external package warnings and 0 errors. `git diff --check` found no whitespace errors on touched files, only line-ending normalization warnings. Focused banned-call scan returned no matches. Unity console briefly reported only unrelated Crest/ocean validator errors; later MCP transport failed because the editor shut down. Compute import and visual capture remain PENDING VERIFICATION.
