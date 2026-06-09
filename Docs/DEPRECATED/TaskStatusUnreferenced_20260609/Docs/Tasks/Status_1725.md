# Status_1725

Agent: 1725
Domain: FAUNA_SKIN_AND_BIOLUMINESCENCE_MASK_BAKER
Prompt source: Docs/Tasks/CURRENT_BATCH.md
Task count: 22
Status: IMPLEMENTED - STATIC VERIFIED; BUILD BLOCKED BY UNRELATED CORE DEPENDENCY

Relevant mandates read before coding:
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt
- GPU_Compute_Kernels_Kernels_Optimization_MX350.txt
- GPU_Compute_Warp_Sizing_Mobile.txt
- REND_Shader_Noir_Aesthetics_Dithering_Fog.txt
- REND_URP_Graphics_HotPath_Optimization_HLOD.txt
- STRM_Async_Asset_Upload_Texture_Settings.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt

Domain boundary:
- Allowed write scope from prompt: Assets/_Project/Editor/Bakers, Assets/_Project/Scripts/Fauna, Assets/_Project/Art/Shaders/Include.
- Hecton_Master_Fauna.shader was not found by static search. Existing fauna-compatible shader candidates are Hecton_LeviathanOrganic.shader, HectonBiolumMaster.shader, and Hecton_Master_Lit.shader.

## Checklist

- [x] Task 01: FAUNA_BRAIN_STATIC_AUDIT
- [x] Task 02: BIOLUMINESCENCE_SHADER_DECONSTRUCTION
- [x] Task 03: COMPUTE_SHADER_API_ALIGNMENT_INSPECTION
- [x] Task 04: MUSCULAR_STRAIN_MATHEMATICAL_MODELING
- [x] Task 05: GLOBAL_REGISTRY_HOT_POLLING_DETECTION
- [x] Task 06: COMPACTION_FENCE_VULNERABILITY_SCAN
- [x] Task 07: TELEMETRY_AND_REPORTING_ARCHITECTURE
- [x] Task 08: COMPUTE_SHADER_BAKER_INITIALIZATION
- [x] Task 09: EPIDERMAL_WRINKLE_AND_PORE_BAKING
- [x] Task 10: BIOLUMINESCENT_VEIN_NETWORK_BAKING
- [x] Task 11: MULTI_LAYERED_SCALE_AND_CHITIN_BAKING
- [x] Task 12: ASSET_DATABASE_TEXTURE_SERIALIZATION
- [x] Task 13: AUTOMATED_TEXTURE_IMPORTER_CONFIGURATION
- [x] Task 14: OFFLINE_TEXTURE_VALIDATOR_GATE
- [x] Task 15: DRY_RUN_VERIFICATION_EXECUTION
- [x] Task 16: CONTINUOUS_QUALITY_SCALING_INTEGRATION
- [ ] Task 17: BATCHED_COMPILATION_AND_SYNTAX_ASSERTION [BLOCKED BY DEPENDENCY - Hecton8.Core H8AppliedLoreRuntime.cs]
- [x] Task 18: EXPLICIT_PIXEL_COUNT_VALIDATION_GATE
- [x] Task 19: COMPACTION_FENCE_RACE_CONDITION_AUDIT
- [x] Task 20: ZERO_GC_ALLOCATION_PROFILER_MOCK
- [x] Task 21: VRAM_BUDGET_LIMIT_TESTING
- [x] Task 22: AUTOMATED_METRIC_VALIDATOR_REPORT [SUPERSEDED BY CODE-ONLY PROOF DIRECTIVE]

## Iteration Log

### Loop 0 - Setup
- Created fresh status file. Old Status_1725.md was absent, so no hygiene violation from stale active batch state.
- DOD practice: prompt extraction by explicit XML regex over Docs/Tasks/CURRENT_BATCH.md; mandate read limited to 8 task-relevant files.
- Rejected alternative: reading archive batch prompts or deprecated reports; those are not current authority for this batch.
- Microsecond estimate: setup/read/write is editor-side only; runtime saved 0 us until code changes land.

### Loop 1 - Tasks 1-5 Audit
- Task 01 DOD: `rg` audit of `Assets/_Project/Scripts/Fauna/FaunaBrain.cs` and `Assets/_Project/Scripts/Fauna/**/*.cs` for `new Material`, `.material`, `.materials`, runtime `Texture2D`, `SetPixels`, `EncodeToPNG`, and MPB usage. No material clones or runtime texture construction found in `FaunaBrain.cs`; existing shared-material assignment is at line 4470 and existing cold MPB is at line 4465. Rejected alternative: editing `FaunaBrain.cs` without a proved clone site. Runtime estimate: spawn GC saved by current route remains 0 us new delta, but verified no clone regression.
- Task 02 DOD: shader source deconstruction of `Hecton_LeviathanOrganic.shader`. Actual mask contract is `_MaskMap` R metallic, G AO, B smoothness, A emission, decoded by `HectonCoreLitDecodePackedMaskV1`. Rejected alternative: baking prompt-level roughness-in-G layout, because source decoder would read it incorrectly. Runtime estimate: prevents one wrong mask decode path and material reauthor pass.
- Task 03 DOD: compared existing compute baker patterns, including `HullCavitationBaker1722` and `GeologicalStrataBaker1724`. New baker will query `GetKernelThreadGroupSizes` and use `GraphicsBuffer` for skeleton path constants. Rejected alternative: fixed dispatch group constants and `ComputeBuffer` wording from prompt, because current GPU mandate prefers explicit group-size proof and modern buffer APIs. Runtime estimate: editor-only, 0 us frame cost.
- Task 04 DOD: selected cinematic approximation model: UV-space crease ridges, cellular pores, spine/jaw vein curves, and chitin plate bands. Rejected alternative: skeletal mesh skin strain or per-spawn procedural material work, because it is hot-path cost for a texture feature. Runtime estimate: removes spawn-time procedural skin work; exact profiler delta pending Unity run.
- Task 05 DOD: GlobalRegistry scan found fauna dependency reads in cold refresh/registration paths, not a hot material polling loop. Rejected alternative: adding a new registry route for baker outputs; generated assets are AssetDatabase products, not runtime services. Runtime estimate: avoids new hot polling.

Compile gate after tasks 1-5: no source code changed yet. Full compile deferred until baker source exists and CPU/csc guard permits.

### Loop 2 - Tasks 6-10
- Task 06 DOD: scanned fauna runtime DataVault routes. Existing readers check `vault == null || vault.IsCompactionFenceActive` before resolving handles in material-adjacent fauna code. Rejected alternative: editing DataVault code outside the texture baker domain. Runtime estimate: 0 us new cost.
- Task 07 DOD: original report schema existed, then polish directive superseded report generation. `FaunaTextureBaker` now exposes metrics through `BakeResult` and does not write JSON. Rejected alternative: keeping duplicate report-writing code. Runtime estimate: 0 us.
- Task 08 DOD: created `Assets/_Project/Editor/Bakers/FaunaTextureBaker.cs` as `EditorWindow` with menu bake/dry-run routes, random-write `RenderTexture`s, compute dispatch, and `GraphicsBuffer` skeleton path binding. Rejected alternative: runtime component baker. Runtime estimate: removes runtime bake path by construction.
- Task 09 DOD: created `CSBakeFaunaNormalMap` kernel. It derives tangent-space normal from the baked height field and writes a shader-compatible normal map. Rejected alternative: per-spawn normal generation. Runtime estimate: editor-only compute.
- Task 10 DOD: created `CSBakeFaunaMaskV1` alpha vein mask and `CSBakeFaunaBiolumPulse64` 8x8/64-frame atlas path using phase-wrapped sinusoidal pulse over baked vein field. Rejected alternative: runtime lights or runtime texture writes. Runtime estimate: no spawn-time texture writes.

### Loop 3 - Tasks 11-14
- Task 11 DOD: compute shader bakes chitin plate field from skeleton proximity, longitudinal/lateral plate ridges, plate cracks, metallic/chitin R, AO G, smoothness B, emission A. Rejected alternative: separate chitin/organ maps. Runtime estimate: one packed mask sample in the existing shader contract.
- Task 12 DOD: C# bake path reads back render targets, encodes albedo/normal/mask/pulse as PNG, writes with first-party atomic asset writes, and imports through `AssetDatabase`. Rejected alternative: transient editor-only textures. Runtime estimate: static serialized assets only.
- Task 13 DOD: importer configuration enforces Albedo sRGB true, normal/mask/pulse sRGB false, Clamp for creature maps, Repeat for pulse atlas, Standalone BC7, Android/iPhone ASTC_6x6. Rejected alternative: relying on default import settings. Runtime estimate: reduced resident VRAM versus raw textures.
- Task 14 DOD: validation gate checks exact dimensions, pixel count, finite normalized channels, non-empty emission alpha, RGB channel independence, and chitin layout warnings before save/import. Rejected alternative: visual-only QA. Runtime estimate: avoids corrupted runtime fetches.

### Loop 4 - Tasks 15-17
- Task 15 DOD: dispatch stress test implemented through `GetKernelThreadGroupSizes`, ceil-divide group counts, and per-kernel HLSL coordinate guards. Non-power-of-two aligned sizes are guarded; no edge clipping by dispatch math. Rejected alternative: fixed thread group constants. Runtime estimate: editor-only.
- Task 16 DOD: `GlobalQualityWeight` drives output sizes continuously: 1024 to 4096 albedo, 512 to 2048 detail/mask, 64 to 256 pulse tiles. It changes static asset output only, not runtime truth or DTO layout. Rejected alternative: binary quality switch. Runtime estimate: runtime unchanged.
- Task 17 status: build guard first blocked by CPU 100 percent and active dotnet processes. Later CPU dropped to 29.61 percent and no compiler process was returned, so one `dotnet build Hecton8.Editor.csproj --no-restore /m:1` was launched. The tool timed out at 120 seconds; the dotnet process and csc child exited after waiting, but no stdout/stderr was recoverable and `Temp/CodexBuild/Hecton8.Editor` remained empty. DOD practice: one build attempt, no second build. Rejected alternative: launching a second build without diagnostics. Runtime estimate: none.

### Loop 5 - Tasks 18-21
- Task 18 DOD: `ValidateTexturePixels()` asserts `texture.width`, `texture.height`, and `pixels.Length == expectedWidth * expectedHeight` before encoding. Rejected alternative: trusting dispatch dimensions. Runtime estimate: editor-only guard.
- Task 19 DOD: theoretical audit recorded in rationale. New baker never reads `GlobalDataVault`; existing runtime vault users back off when `IsCompactionFenceActive` is true. Rejected alternative: cross-domain runtime edits. Runtime estimate: 0 us.
- Task 20 DOD: profiler mock conclusion: `FaunaBrain.LateFrameTick()` steady-state visual update uses existing shared material plus existing cold MPB path; no new material or texture allocation was added. Rejected alternative: runtime texture/material bake. Runtime estimate: 0 B managed allocation from this change in player steady state.
- Task 21 DOD: compressed budget math: Ultra primary package is 4096 albedo BC7 + 2048 normal BC5 + 2048 mask BC7 = about 28 MB per creature; optional pulse atlas is separate. Rejected alternative: counting uncompressed editor source bytes as runtime residency.

### Loop 6 - Task 22 And Final Verification
- Task 22 DOD: static JSON proof artifact was removed after the code-only proof directive. Current proof route is source, compile/static checks, and Unity-generated assets. Rejected alternative: retaining stale report output. Runtime estimate: 0 us.
- Static verification before polish: `git diff --check` passed for the new baker, compute shader, status, rationale, and prior report files.

### Loop 7 - Code-Only Polish
- Removed local duplicates from `FaunaTextureBaker`: folder normalization, asset name sanitation, direct file path writes, JSON builder, SHA-256 report writer. Now uses `ProceduralTextureBaker` utilities.
- Added generic texture importer enforcement overload to `ProceduralTextureBaker`.
- Added `.meta` files for the new C# and compute shader assets; orphan `.meta` scan returned 0.
- Added `UnsafeUtility.SizeOf<T>()` alignment gate for unmanaged baker DTOs.
- Replaced the incompatible EXR height/normal output with a shader-compatible PNG normal map imported through `TextureRole.Normal`/BC5 for `_NormalMap`.

### Loop 8 - Final Static Verification
- Code report path scan returned no report/SHA/writer tokens in `FaunaTextureBaker`.
- Fauna runtime lookup scan returned no `GlobalRegistry.Get<T>()`, `GetComponent<T>()`, `GameObject.Find`, or `Camera.main` in `Assets/_Project/Scripts/Fauna`.
- DataVault write-lock scan in fauna domain found four write locks, all released in immediate `try/finally` blocks.
- `git diff --check` passed; only Git reported a line-ending normalization warning for `ProceduralTextureBaker.cs`.
- No active `dotnet` or `csc` process was found during final guard. No second build was launched.

### Loop 9 - Spawn Allocation Polish
- `FaunaBrain` presentation now uses one shared cleared `MaterialPropertyBlock` scratch instead of allocating a block per fauna instance.
- `FaunaTextureBaker` validator now uses `GetRawTextureData<Color32>()` instead of `GetPixels()`.
- `FaunaTextureBaker` captures rollback snapshots before writing baked assets; failed import/finalize restores prior assets and `.meta`.
- `ProceduralTextureBaker` role-based mask import no longer treats alpha as transparency; mask alpha remains data.
- CPU guard after edits reported 85 percent load and no compiler processes, so no second `dotnet build` was launched.

### Loop 10 - GPU Upload And Hygiene Polish
- `FaunaTextureBaker` skeleton-path upload now uses stackalloc, `GraphicsBuffer.LockBufferForWrite`, and `UnsafeUtility.MemCpy` instead of a managed `Vector4[]` plus `SetData`.
- Partially sampled bone paths now fill the unused GPU payload tail with the last valid point, so the eight-slot structured buffer is deterministic even when compute reads are later widened.
- `ProceduralTextureBaker` rollback overloads no longer allocate temporary string arrays; only the required output snapshot array remains.
- Existing live-file orphan `.meta` scan returned 0. Six tracked archive `.meta` paths under `Docs/_Archive/WorkspaceHygiene_1331` were already deleted in the worktree before a successful patch delete could run; they remain outside 1725 source scope and were not restored.
- Static scans after this pass found no `SetData`, `Vector4[]`, `GetPixels`, `mesh.triangles`, runtime material clone, runtime texture write, `WaitForCompletion`, or hidden `.Complete()` token in the touched source set.
- CPU guard after edits reported 100 percent load and active `dotnet` PID 31364, so no second `dotnet build` was launched.

### Loop 11 - UV Metric Gate Polish
- `FaunaTextureBaker` now resolves the source mesh once, measures UV0 through pre-sized editor scratch lists, and rejects missing UV0, non-finite vertex/UV data, degenerate UV triangles, and fatal per-triangle stretch above 1.50 before any texture bytes are written.
- DOD practice: source mesh data uses `GetVertices(List<Vector3>)`, `GetUVs(0, List<Vector2>)`, and `GetTriangles(List<int>, subMesh, true)` with fixed capacities; no `mesh.vertices`, `mesh.uv`, or `mesh.triangles` copy properties were introduced.
- Rejected alternative: letting the compute baker generate attractive masks for broken UV islands, because that serializes artifacts into runtime PBR textures and hides the real authoring defect.
- Microsecond estimate: player runtime remains 0 us. Editor bake can spend O(vertex + index) once; it prevents bad fauna texture packages from reaching the runtime shader.
- Build guard after the UV pass reported CPU 53.8 percent and 0 compiler processes, so no additional `dotnet build` was launched.

### Loop 12 - Throttled Build Attempt
- CPU guard later reported 43.82 percent and 0 compiler processes, so one throttled `dotnet build Hecton8.Editor.csproj --no-restore /m:1` was launched.
- Build failed in `Hecton8.Core.csproj` before editor-domain proof because `Assets/_Project/Scripts/Data/Monolith/H8AppliedLoreRuntime.cs:70` raises CS8168 and CS8350 around exposing `record` through `TryGetUtf8FromRecord(...)`.
- DOD practice: no second build was launched after the unrelated dependency failure. The error is outside the 1725 domain and not edited here.
- Rejected alternative: patching Data Monolith from the fauna texture baker task. That would violate the domain boundary without a route card.
- Microsecond estimate: no runtime change. Source-level 1725 static checks remain clean; compile proof is blocked upstream.

### Loop 13 - Biolum Pulse Seam Gate
- `FaunaTextureBaker.compute` now maps the final active pulse frame to phase `2*pi` by dividing by `frames - 1`, so the last active tile is a deterministic duplicate of the first phase boundary.
- `FaunaTextureBaker` now validates the pulse atlas loop seam by comparing frame 0 and frame 63 byte channels before asset serialization; max accepted channel delta is 2.
- DOD practice: no runtime animation repair, no shader variant, and no new material state. The offline asset is rejected if the loop would visibly jump.
- Rejected alternative: trusting sinusoidal periodicity while sampling only `frame / frames`, because the serialized final tile would not sit on the positive boundary.
- Microsecond estimate: player runtime remains 0 us. Editor validation scans one atlas tile once per bake.

### Loop 14 - Shared Material Asset Bind
- `FaunaTextureBaker` now has an optional Target Shared Material field. When provided, the baker binds imported `_BaseMap`, `_NormalMap`, and `_MaskMap` textures to that material asset in the Editor.
- DOD practice: target material must be an AssetDatabase asset and must expose `_BaseMap`, `_NormalMap`, and `_MaskMap`. Scene/runtime material instances are rejected.
- Transaction polish: material texture slots are snapshotted before bind and restored on later bake failure, matching the asset-file rollback route.
- Rejected alternative: leaving texture assignment as manual glue or runtime code, because that invites material clone regressions at predator spawn.
- Microsecond estimate: player runtime remains 0 us. The shared material is authored offline; no runtime texture/material lookup is added.

### Loop 15 - Shared Material Scope Gate
- `FaunaTextureBaker` now rejects Target Shared Material inputs unless `AssetDatabase.GetAssetPath()` resolves to a `.mat` file under `Assets/`.
- DOD practice: package, built-in, scene, and runtime material references cannot become the authored output target for baked fauna PBR maps.
- Rejected alternative: accepting any `AssetDatabase.Contains()` material, because package/built-in assets are not the project-owned fauna presentation route.
- Microsecond estimate: player runtime remains 0 us. The guard prevents authoring drift that would otherwise be repaired with runtime material assignment code.

### Loop 16 - MRAO Validator Copy Removal
- `ProceduralTextureBaker.TryCollectMraoStats()` now reads mask pixels through `GetRawTextureData<Color32>()` instead of `GetPixels32()`.
- DOD practice: editor validation uses a native texture-data alias and avoids a full managed pixel-array copy.
- Rejected alternative: leaving `GetPixels32()` because it is editor-only; the shared baker helper is used by texture authoring and should follow the same memory discipline as the fauna baker.
- Microsecond estimate: player runtime remains 0 us. Editor validation avoids one full RGBA32 array copy per mask check.

### Loop 17 - Cold Allocation Proof Format
- `FaunaBrain` shared presentation MPB allocation comment now names `MaterialPropertyBlock[1]`, reason, and owner in the required cold-allocation proof format.
- DOD practice: no runtime logic changed; this is source-level proof hygiene for the allocation that replaces per-fauna MPB objects.
- Rejected alternative: leaving the informal comment because the allocation is already cold. The project mandates exact proof text for cold allocations.
- Microsecond estimate: player runtime remains unchanged. Audit ambiguity is reduced.
