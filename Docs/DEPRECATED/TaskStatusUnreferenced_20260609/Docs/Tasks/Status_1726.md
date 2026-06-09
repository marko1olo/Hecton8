# Status_1726 - Visor Glass Dirt, Scratch, Salt Mask Baker

Agent: 1726
Domain: VISOR_GLASS_DIRT_AND_SCRATCH_MASK_BAKER / UI-Rendering-Editor Baker
Task count: 22
Prompt source: Docs/Tasks/CURRENT_BATCH.md, <AGENT_PROMPT id="1726">
Proof state: PENDING_UNITY_EDITOR_BAKE

## Hygiene
- Status_1726.md was missing at session start: treated as clean state.
- Rationale_1726.md was missing at session start: treated as clean state.
- Mandated domain file missing: Docs/Actual Domains of Project.txt. Operative boundary is XML role plus allowed directories: Assets/_Project/Editor/Bakers/, Assets/_Project/Scripts/UI/, Assets/_Project/Art/Shaders/Include/.

## Mandates Read Before Coding
- UI_Data_Streaming_ZeroGC_Optimization.txt
- UI_Diegetic_Physical_Interfaces.txt
- GPU_Compute_Kernels_Kernels_Optimization_MX350.txt
- GPU_Compute_Warp_Sizing_Mobile.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt
- REND_Shader_Noir_Aesthetics_Dithering_Fog.txt
- STRM_Async_Asset_Upload_Texture_Settings.txt

## Root Docs Read Before Coding
- TASTE.md
- PROJECT_BIBLES.md
- ui.md
- UI_DIEGETIC_HUD_STANDARDS.md
- shaders.md
- rendering.md
- compute.md
- performance.md
- authoring.md
- data.md
- telemetry.md
- quality.md
- Docs/QUALITY_GATES.md

## Checklist
- [x] Task 01 VISOR_OVERLAY_STATIC_AUDIT - SuitHUDV4CanvasOverlay.cs static scan found no visor glass dirt/fog CPU pixel path; only runtime Texture2D write is acoustic radar payload, not visor wear. DOD: rg scan for Texture2D/SetPixel/Apply plus targeted method read. Rejected alternative: changing unrelated acoustic radar path. Estimate: 900 us static analyzer intent, excluding disk I/O.
- [x] Task 02 VISOR_SHADER_DECONSTRUCTION - SuitVisor.shader deconstructed around scratch, fingerprint, runoff, condensation, and refraction dirt composition. DOD: identified insertion points for packed mask before dirt/refraction solve. Rejected alternative: replacing the whole visor shader path. Estimate: 1400 us shader routing.
- [x] Task 03 COMPUTE_SHADER_API_ALIGNMENT_INSPECTION - Existing baker patterns reviewed: ChemicalRustBaker1723, GeologicalStrataBaker1724, FaunaTextureBaker. DOD: Unity editor baker path uses RenderTexture random write, GetKernelThreadGroupSizes, AssetDatabase import, try/finally cleanup, and OnDisable/OnDestroy release routes. Rejected alternative: CPU Texture2D procedural bake. Estimate: 1000 us.
- [x] Task 04 CONDENSATION_MATHEMATICAL_MODELING - Selected 8x8/64 frame alpha atlas with periodic time vector from sin/cos; R/G/B remain stable atlas-wide masks. DOD: continuous loop model without runtime texture writes. Rejected alternative: per-frame CPU texture upload or particle fog simulation. Estimate: 850 us.
- [x] Task 05 GLOBAL_REGISTRY_HOT_POLLING_DETECTION - rg found no GlobalRegistry.Get/TryGet hot polling pattern in Assets/_Project/Scripts/UI; overlay uses GlobalRegistry.DataVault only for cold glitch-table binding. DOD: directory scan plus source line read. Rejected alternative: adding a new registry dependency. Estimate: 500 us.
- [x] Task 06 COMPACTION_FENCE_VULNERABILITY_SCAN - DataVault glitch-table read guards IsCompactionFenceActive before and after TryReadOnlyHandle; UIStateStore reads have fallback/finite checks. DOD: inspected BindGlitchTableVault, TryResolveGlitchTablePointer, and headless value readers. Rejected alternative: direct pointer reuse during fence. Estimate: 700 us.
- [x] Task 07 TELEMETRY_AND_REPORTING_ARCHITECTURE - Original JSON report route was superseded by the user APEX code-proof directive; proof is source code plus static verification and concise AgentLog only. DOD: no runtime/report JSON writer remains in VisorMaskBaker.cs. Rejected alternative: stale JSON artifact. Estimate: 450 us.
- [x] Task 08 COMPUTE_SHADER_BAKER_INITIALIZATION - Created Assets/_Project/Editor/Bakers/VisorMaskBaker.cs as EditorWindow with MenuItems, RenderTexture random write target, GraphicsBuffer params, GetKernelThreadGroupSizes dispatch, and cleanup. DOD: editor-only compile boundary via #if UNITY_EDITOR. Rejected alternative: runtime MonoBehaviour baker. Estimate: 2400 us.
- [x] Task 09 GLASS_DIRT_AND_SCRATCH_BAKING - Added VisorMaskBaker.compute R dirt/fingerprint and G scratch formulas using edge wear, smudge ellipses, line scratches, micro scratches. DOD: RG output from compute kernel guarded by dispatch bounds. Rejected alternative: Texture2D CPU pixel loops. Estimate: 2800 us.
- [x] Task 10 CONDENSATION_FLIPBOOK_BAKING - Added 8x8/64 alpha atlas generation and SuitVisor.shader flipbook sampler using _Time, drift speed, and optional manual frame/blend. DOD: endpoint-seamless time circle uses frame/63, with strict alpha loop validation delta. Rejected alternative: per-frame texture upload. Estimate: 2300 us.
- [x] Task 11 SALT_AND_CRUST_MAPPING - Added B channel salt crust from edge bands, lower lip accumulation, periodic brine noise, and crystalline flecks; shader adds salt to refraction dirt and glare tint. DOD: B max validation gate. Rejected alternative: additional salt texture. Estimate: 1100 us.
- [x] Task 12 ASSET_DATABASE_TEXTURE_SERIALIZATION - Baker readbacks editor RT to Texture2D, EncodeToPNG, atomic ProceduralTextureBaker write, ImportAsset. Output path: Assets/_Project/Art/Textures/UI/TX_Visor_{assetName}_Masks.png. DOD: no runtime serialization route. Rejected alternative: literal Assets/Art path outside project root convention. Estimate: 1000 us.
- [x] Task 13 AUTOMATED_TEXTURE_IMPORTER_CONFIGURATION - TextureImporter enforced sRGB false, Clamp, Bilinear, no mipmaps for atlas edge safety, CompressedHQ, Standalone BC7, Android/iPhone ASTC_6x6. DOD: codified importer branch. Rejected alternative: default uncompressed import. Estimate: 850 us.
- [x] Task 14 OFFLINE_TEXTURE_VALIDATOR_GATE - Baker validates dimensions, pixel count, finite normalized channels, non-empty RGBA, and alpha loop delta before accepting the PNG asset. DOD: invalid pixels log "Visor mask validation violation detected!" and abort. Rejected alternative: accepting PNG output blindly. Estimate: 1200 us.
- [x] Task 15 DRY_RUN_VERIFICATION_EXECUTION - Added MenuItem dry-run using 64x64 RT, same compute kernel, same group-size query, same OOB kernel guard. DOD: static dispatch math uses CeilDivide(width, groupX). Rejected alternative: assuming 2048 divisible without guard. Estimate: 500 us.
- [x] Task 16 CONTINUOUS_QUALITY_SCALING_INTEGRATION - GlobalQualityWeight slider maps offline resolution from 512 to 2048 continuously with smoothstep and 64-pixel alignment. Runtime shader truth unchanged. DOD: no gameplay DTO/authority change. Rejected alternative: binary low/ultra switch. Estimate: 450 us.
- [x] Task 17 BATCHED_COMPILATION_AND_SYNTAX_ASSERTION - Early CPU sample was 88%, so build was blocked; final CPU sample was 23% with no dotnet/csc, but build still was not launched because latest APEX directive required static validation/no build spam. Static git diff/meta/source scans passed. DOD: no fake compile claim. Rejected alternative: forcing build under load or running gratuitous build. Estimate: 350 us.
- [x] Task 18 EXPLICIT_PIXEL_COUNT_VALIDATION_GATE - ValidateMask asserts texture.width/height and raw Color32 pixel count == expected resolution squared. DOD: native raw pixel gate before PNG acceptance. Rejected alternative: trusting RenderTexture dimensions only. Estimate: 300 us.
- [x] Task 19 COMPACTION_FENCE_RACE_CONDITION_AUDIT - No runtime UI data-vault edits made; existing overlay backs off when compaction fence is active before/after TryReadOnlyHandle and UIStateStore readers return fallback. DOD: theoretical race documented in rationale. Rejected alternative: new hot pointer route. Estimate: 650 us.
- [x] Task 20 ZERO_GC_ALLOCATION_PROFILER_MOCK - Runtime glass wear path is shader sampling only; new Texture2D/Apply appears only inside UNITY_EDITOR baker readback. DOD: rg runtime mutation scan. Rejected alternative: SuitHUDV4CanvasOverlay CPU fog texture. Estimate: 500 us.
- [x] Task 21 VRAM_BUDGET_LIMIT_TESTING - 2048x2048 BC7 mask estimate is 4,194,304 bytes plus container/import overhead; two masks remain under 16 MiB target. DOD: importer enforces BC7/ASTC compressed masks. Rejected alternative: raw RGBA32 16 MiB per mask. Estimate: 400 us.
- [x] Task 22 AUTOMATED_METRIC_VALIDATOR_REPORT - Superseded by APEX code-proof directive. Removed VisorMaskBaker JSON writer and deleted stale Docs/Reports/VISOR_BAKER_REPORT_1726.json artifact. DOD: source-only proof path. Rejected alternative: keeping stale report hashes. Estimate: 700 us.

## Iteration Log
- Loop 0 initialized state. No code edited yet. DOD practice: prompt extraction plus mandate/root-doc read before implementation. Rejected alternative: coding from user summary without XML extraction. Estimate: 1600 us CLI/static setup excluding file I/O latency.
- Loop 1 completed tasks 1-7. Compile verification: no code delta yet, so no Unity compile launched. DOD practice: static evidence before source edits. Rejected alternative: edits before knowing if runtime CPU texture writes exist. Estimate: 5800 us analyst time excluding file I/O latency.
- Loop 2 completed tasks 8-14. Files added: VisorMaskBaker.cs, VisorMaskBaker.compute. Shader contract added to SuitVisor.shader. DOD practice: editor-only bake, compute OOB guard, importer/validator before save. Rejected alternative: runtime Texture2D fog/scratch update. Estimate: 12400 us implementation reasoning excluding file I/O.
- Loop 3 completed tasks 15-18. Dry-run path and pixel count gates exist. Build verification not run: initial CPU_LOAD=88 blocked, later APEX no-spam directive kept validation static. `git diff --check` passed. DOD practice: no fake compile pass. Rejected alternative: launching dotnet under load or gratuitously. Estimate: 1600 us.
- Loop 4 completed tasks 19-21. Runtime proof: new visor mask path is shader sampler + static texture; no UI DataVault writes or pointer lanes added. DOD practice: theoretical compaction and zero-GC runtime audit. Rejected alternative: material clone/CPU property texture route. Estimate: 1550 us.
- Loop 5 completed task 22, then APEX polish removed the JSON report route. Final Unity editor bake remains PENDING_UNITY_EDITOR_BAKE until the MenuItem dispatch runs inside Unity. DOD practice: source-code proof over stale disk report. Rejected alternative: pretending bake metrics exist. Estimate: 700 us.
- Loop 6 APEX continuation: removed duplicated asset-folder/path/dispatch/file-write helpers from VisorMaskBaker by routing through ProceduralTextureBaker, added atomic output rollback plus visor material binding snapshot/restore, and simplified validation to raw Color32 gates without managed Color[] metrics. Build not run: CPU_LOAD=85. Estimate: 2100 us.
- Loop 7 allocation and allocation-failure polish: prewarmed the single GpuBakeParams payload array, added explicit RenderTexture.Create() failure abort/cleanup, and added a one-path rollback overload to ProceduralTextureBaker so VisorMaskBaker no longer allocates a temporary path array. Static scan passed for duplicated params arrays and direct visor file writes; the only File.WriteAllBytes hit is the shared atomic writer owner. Build not run: CPU_LOAD=100 and active dotnet PID 48092. Estimate: 800 us.
- Loop 8 GPU device gate polish: added symmetric compute-shader and R8G8B8A8_UNorm LoadStore/ReadPixels support checks for bake and dry-run so unsupported editor GPUs fail before PNG rollback capture, RT allocation, dispatch, or material mutation. Build not run under no-spam policy until CPU/dotnet gate opens. Estimate: 650 us.
- Loop 9 condensation seam polish: changed compute condensation phase from frame/64 to frame/63 so the 64th atlas slot duplicates the loop endpoint, then tightened alpha loop validator threshold from 0.25 to 0.05. Build not run: CPU gate still closed. Estimate: 700 us.
- Loop 10 channel-contract correction: fixed VisorMaskBaker.compute so RGB dirt/scratch/salt evaluate over full visor UV while only alpha uses 8x8 local flipbook UV. This removes accidental 8x8 repetition in stable glass wear channels. Build not run: static shader patch only under no-spam policy. Estimate: 420 us.
- Loop 11 runtime ALU bypass polish: changed SuitVisor.shader `_VisorMaskStrengths` default to zero and added material-strength branch gates so procedural scratch, static smudge, and condensation smudge are skipped when VisorMaskBaker binds the packed mask with strengths=1. DOD practice: old materials without baked masks keep fallback procedural wear; baked materials stop paying fallback noise loops. Rejected alternative: runtime material clone or removing fallback functions. Estimate: 1200 us.
- Loop 12 static verification after ALU bypass: `git diff --check` returned no whitespace errors, exact forbidden-token scan over VisorMaskBaker/SuitVisor/compute/UI returned no `GetComponent(`, `GlobalRegistry.Get`, pixel API, material clone, `WaitForCompletion`, or `.Complete()` hits, and brace/preprocessor balance passed. Build not run: CPU_LOAD=97 and active dotnet/csc PIDs 47240/48164. Estimate: 900 us.
- Loop 13 baked condensation edge bypass: moved condensation time, fingerprint condensation sample, procedural smudge, frost blue-noise warp, and procedural edge shaping inside the procedural-condensation branch. Baked mask materials now use the baked alpha atlas directly with no extra condensation noise path. DOD practice: mixed strengths stay continuous through `lerp(1.0, proceduralEdge, proceduralCondensationWeight)`. Rejected alternative: deleting procedural condensation fallback for unbaked materials. Estimate: 850 us.
- Loop 14 authored scratch sampler bypass: added `authoredScratchWeight = 1 - _VisorMaskStrengths.y` and branch-gated `_ScratchNormalMap` sampling/unpack. Baked scratch materials now derive scratch normals from baked mask gradients without paying the authored normal-map sampler. DOD practice: unbaked materials keep the original scratch normal fallback; mixed strengths attenuate authored normal contribution continuously. Estimate: 750 us.
- Loop 15 packed mask sampler gate: added uniform `visorMaskActive` and branch-gated `_VisorMaskTex` RGB sample plus alpha flipbook sampling. Unbaked materials with `_VisorMaskStrengths = 0` no longer pay packed-mask texture fetches that are multiplied to zero. DOD practice: baked materials still sample the same packed asset; fallback materials stay pure procedural/authored. Estimate: 900 us.
- Loop 16 static verification after sampler gates: `git diff --check` returned no whitespace errors, targeted shader grep confirmed packed RGB/alpha samples are strength-gated, exact forbidden-token scan stayed empty, and brace/preprocessor balance passed. Build not run: CPU_LOAD=100 and active dotnet/csc PID 47240. Estimate: 850 us.
- Loop 17 RT allocation fail-fast cleanup: replaced exception-throwing `CreateMaskRenderTexture` with `TryCreateMaskRenderTexture` and routed both full bake and dry-run through controlled `false + failure` returns. DOD practice: allocation failure stays in the existing cleanup path without exception-as-control-flow. Rejected alternative: catching the thrown allocation failure at outer bake scope. Estimate: 700 us.
- Loop 18 material contract fail-fast reorder: moved `TryCaptureDefaultVisorMaterialSnapshot` before output folder creation and rollback snapshot capture. DOD practice: invalid material/shader contract now fails before asset transaction setup. Rejected alternative: capturing rollback for an output that cannot be bound to the required material. Estimate: 450 us.
- Loop 19 static verification after C# fail-fast polish: flow grep confirmed material snapshot before output folder/rollback and `TryCreateMaskRenderTexture` on full/dry-run paths; `git diff --check` returned no whitespace errors; exact forbidden-token scan stayed empty; brace/preprocessor balance passed. Build not run: CPU_LOAD=99 and active dotnet/csc PID 47240. Estimate: 850 us.
