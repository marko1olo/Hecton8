# Status_HLOD_IMPOSTOR_BAKER

Agent: HLOD_IMPOSTOR_BAKER
Domain: RENDER_ARCHITECT
Prompt extracted: C:\hades\Hecton8\Docs\Tasks\CURRENT_BATCH.md
Status hygiene: new file created, no stale task state found.
Overall status: PENDING VERIFICATION

## Mandates Selected Before Coding

- REND_URP_Graphics_HotPath_Optimization_HLOD.txt
- REND_GPU_Sovereignty.txt
- REND_GPU_Occlusion_Culling_6000.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt
- STRM_World_Streaming_Residency_Chunk_Management.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt

## Primary Tasks

- [x] 1. MENU INTEGRATION | Done | DOD: `HECTON-8/Bake HLOD Impostor` MenuItem added in editor assembly; rejected runtime trigger and Amplify-only menu; estimate 0 us runtime, editor-only cost.
- [x] 2. CAMERA RIG | Done | DOD: 8 deterministic upper/lower octa directions, preview camera, clone centered around bounds; rejected scene camera capture; estimate saves 200-800 us per runtime impostor by removing runtime camera work.
- [x] 3. RENDER TEXTURES | Done | DOD: albedo/alpha atlas and normal/depth atlas baked via replacement shader; rejected per-frame capture/readback; estimate saves multi-ms runtime readback stalls.
- [x] 4. ATLAS PACKING | Done | DOD: 4x2 tile packing plus `HectonOctahedralImpostorData` bounds/depth metadata; rejected texture-per-view runtime cache; estimate saves 7 texture binds per impostor material group.
- [x] 5. THE IMPOSTOR SHADER | Done | DOD: URP shader wrapper plus `Hecton_Impostor.hlsl`; rejected Built-In pipeline shader; estimate 0 CPU us per tile switch because selection is shader-side.
- [x] 6. BILLBOARD MATH | Done | DOD: shader builds camera-facing right/up basis from `_WorldSpaceCameraPos`; rejected Transform.LookAt; estimate saves 0.4-2.0 us per 100 impostors CPU-side.
- [x] 7. VIEW ANGLE SELECTION | Done | DOD: shader ranks 8 octa directions and maps to 4x2 atlas; rejected CPU tile switches; estimate saves one per-instance CPU upload per frame.
- [x] 8. DITHERED BLENDING | Done | DOD: IGN dither selects primary/secondary tile on non-low tiers; rejected alpha-blended ghost quads; estimate saves one extra draw and blend overdraw per impostor.
- [x] 9. NORMAL RECONSTRUCTION | Done | DOD: normal/depth RGB reconstructs world normal for headlight-facing cheap lighting; rejected unlit-only result; estimate spends 1 texture sample to save full far geometry lighting.
- [x] 10. BRG INTEGRATION | Done | DOD: `HectonOctahedralImpostorRenderer` uses native instance buffer and `Graphics.RenderMeshIndirect`; rejected GameObject impostor spawning; estimate keeps draw submission to one indirect call.
- [x] 11. DISTANCE GATE | Done | DOD: WCRM late-frame signal drain publishes LOD2 impostor matrices for dehydrated far chunks beyond the real-geometry radius; rejected new `ChunkState` bit; estimate keeps swap work to one dense native matrix stream.
- [x] 12. DEPTH OFFSET | Done | DOD: fragment shader writes `SV_Depth` with baked depth alpha bias; rejected pure flat alpha quad; estimate spends one scalar bias to reduce far-object intersection artifacts.
- [x] 13. AUP SHIFT SAFETY | Done | DOD: shader adds `_GlobalFloatingOffset`, renderer bounds follow origin shift; rejected raw world-float-only math; estimate prevents jitter without per-instance CPU recook.
- [x] 14. MATH LOD | Done | DOD: Low/MX350/Unknown sets quality flag disabling secondary-tile dither; rejected one-cost shader path; estimate saves IGN branch/sample decision on low tier.
- [x] 15. ZERO-GC | Done | DOD: streaming handoff uses persistent HLOD matrix/native SOA and renderer buffer binding; rejected Instantiate/Destroy proxy swap; estimate avoids managed allocations in runtime swap.
- [x] 16. VRAM BUDGET | Done | DOD: baker writes a capped 2048x2048 atlas asset with 4x2 occupied tiles; rejected per-object 4K atlases; estimate caps atlas residency to 16 MB RGBA32 before compression per atlas pair.
- [x] 17. TELEMETRY | Done | DOD: renderer reports `ActiveImpostors` into `CrashTelemetryBuffer` fixed ring; rejected string/log-only telemetry; estimate 1 black-box write per 60 frames.
- [x] 18. OMEGA COMPILE CHECK | BLOCKED BY DEPENDENCY | DOD: local scripts validated and shader asset inspected; global Unity compile blocked by unrelated active compiler errors, latest observed in `SimulationBucketingContracts.cs` and `DeployableSdfDrillContracts.cs`; rejected false green report; estimate 0 us runtime, verification dependency only.

## Loop Log

- Loop 0 started. Prompt extracted and state files initialized. STATUS: PENDING VERIFICATION.
- Loop 1 complete. Tasks 1-5 implemented. Unity compile requested; global project compile is blocked by unrelated existing errors. Filtered console for `HectonOctahedral` returned 0 errors. STATUS: LOCAL IMPLEMENTATION DONE, GLOBAL COMPILE BLOCKED BY DEPENDENCY.
- Loop 2 complete. Tasks 6-10 implemented. Unity `validate_script` returned 0 diagnostics for renderer, data, editor baker, and types. STATUS: LOCAL SCRIPT VALIDATION PASSED.
- Loop 3 complete. Tasks 11-17 implemented. `WorldChunkResidencyManager.cs` and editor baker validate with 0 diagnostics. Global compile remains blocked by unrelated `AcousticEcholocationRaymarch.cs` variable shadowing. STATUS: LOCAL VALIDATION PASSED, GLOBAL COMPILE BLOCKED BY DEPENDENCY.
- Loop 4 complete. Task 18 marked blocked by external compile wall. Shader file is readable through Unity MCP and console has 0 `Hecton_Octahedral`/`HectonOctahedral` errors. STATUS: ALL CORE TASKS DONE OR BLOCKED.
- Loop 5 complete. Recursive re-read/polish ran with an overly strict polish-tag query; corrected in Loop 7. Fixed low-tier IGN cost and pixel-depth calculation. Console filter for `Hecton_Octahedral` returned 0 entries after asset refresh. STATUS: PENDING VERIFICATION.
- Loop 6 complete. Continued recheck found stale WCRM `OctahedralImpostorInstance` publish storage after the signal-driven matrix path superseded it; removed that dead persistent array and moved renderer matrix-stream mode/time/fade to shader globals to avoid per-frame material dirtiness. Unity MCP validation timed out/disconnected after editor readiness waits; local `dotnet build Assembly-CSharp.csproj --no-restore` still fails on unrelated project-wide missing namespace/type errors and stale generated csproj state. STATUS: LOCAL FILE CHECKS PASSED, GLOBAL COMPILE STILL BLOCKED.
- Loop 7 complete. OMEGA polish mandate re-read and applied. Packed fallback impostor payload to 32 bytes, made renderer GPU buffers/args lazy, removed editor tile-copy allocations, replaced atlas index division with 4x2 bitmask/shift, and re-ran `dotnet build Hecton8.Core.csproj --no-restore`; build remains blocked by unrelated missing namespaces/types plus generated csproj not listing the new renderer yet. STATUS: PENDING VERIFICATION, GLOBAL COMPILE BLOCKED.
- Loop 8 complete. Fixed-atlas inquisition removed `_AtlasGrid` and unused `_DepthScale` from shader CBUFFER/material writes, removed the renderer/editor property IDs and writes, and started fixed atlas addressing. Loop 10 corrected the final scale to the actual 2048x2048 square-cell atlas contract. `validate_script` reports 0 diagnostics for renderer, baker, and types; console filters return 0 HLOD errors. `dotnet build Hecton8.Core.csproj --no-restore` remains blocked by 92 unrelated missing namespace/type errors plus generated csproj not listing the new renderer. STATUS: PENDING VERIFICATION, GLOBAL COMPILE BLOCKED.
- Loop 9 complete. Stable impostor fade path no longer computes IGN fade noise after fade reaches 0.999; transition dithering still runs during fade-in/out only. `rg` confirms the branch is present and fixed atlas constants remain. Unity asset refresh timed out waiting for editor readiness; one console retry returned 0 `Hecton_Impostor` errors, while `Hecton_Octahedral` read retries timed out. Renderer `validate_script` still reports 0 diagnostics. STATUS: PENDING VERIFICATION, GLOBAL COMPILE BLOCKED.
- Loop 10 complete. Re-read caught three correctness/perf defects: runtime alpha was multiplied by `_BaseColor.a` twice, depth values were still computed after the 32-byte payload stopped using them, and shader UVs sampled 1024-high atlas rows while the baker writes 512-square captures. Fixed alpha/color separation, removed dead depth magnitude/max work, added a dedicated unlit albedo/alpha editor replacement shader, forced bake clones to LOD0, and corrected atlas UVs to the 2048x2048 4x4 square-cell lattice with 4x2 occupied cells. `validate_script` reports 0 diagnostics for baker/data/renderer and console filters return 0 HLOD shader/script errors. STATUS: PENDING VERIFICATION, GLOBAL COMPILE BLOCKED.
- Loop 11 complete. Renderer matrix fallback now computes combined draw bounds inside `BindMatricesAsOctahedralFallback`, covering the path where GPU-culling source buffer allocation fails before `ResolveMatrixBounds` runs. First Unity validation disconnected mid-command; retry reports 0 diagnostics for renderer and console filter returns 0 `HectonOctahedral` errors. STATUS: PENDING VERIFICATION, GLOBAL COMPILE BLOCKED.
- Loop 12 complete. Normal/depth bake shader now uses explicit safe-normal math instead of raw `normalize()`, falling back to world up on zero-length normals so bad source meshes cannot bake NaNs into the normal atlas. `rg` confirms no `normalize(` remains in the normal/depth shader; Unity console reads for that shader timed out because the MCP session was not ready. STATUS: PENDING VERIFICATION, GLOBAL COMPILE BLOCKED.
