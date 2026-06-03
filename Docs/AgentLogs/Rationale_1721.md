# Agent 1721 Rationale

State: SOURCE POLISHED / BUILD BLOCKED BY UPSTREAM COMPILE WALL

## Decisions

### D01: Preserve Existing Runtime Texture Array Authority

Problem: The prompt assumes runtime material cloning in `TerminalOsRuntime.cs`, but static scan found no `new Material`, `.material`, `.materials`, `SetPixel`, or `SetPixels` in TerminalOS runtime. The real gap is missing baked CRT projection/mask assets and shader slots.
Solution: Keep the existing dynamic terminal texture array as the owner of changing text/state, add shared baked CRT assets as global/shared material inputs, and generate those assets offline through `UiScreenMeshProjector1721`.
Rejected Alternatives: Replacing the whole terminal OS with static PNG albedo would delete active terminal state and break existing DataVault/SignalBus routes. Creating per-terminal materials would violate SRP Batcher and memory doctrine.
Scalability potential: Low uses 1024 albedo/mask and 512 LUT; Middle uses 2048 albedo/mask and 1024 LUT; High uses 3072 albedo/mask and 1536 LUT; Ultra uses 4096 albedo/mask and 2048 LUT.
Hardware Impact: i3/MX350 avoids per-terminal material clones and live distortion math; expected gain is qualitative/static until profiler capture. Exact measured runtime gain: PENDING VERIFICATION.

### D02: LUT Normalization For Barrel Distortion

Problem: Polynomial barrel distortion can push screen corners outside [0,1], causing edge clamps and bezel stretching.
Solution: Compute max distorted corner radius and multiply by inverse safe scale before converting back to UV. Validate every LUT pixel before writing assets.
Rejected Alternatives: Letting shader clamp distorted UVs hides the failure and creates smeared bezels. Per-fragment polynomial is rejected for compact lanes.
Scalability potential: Low uses fewer pixels, same math; Middle/High/Ultra increase static resolution only. Runtime truth and DTO layout stay unchanged.
Hardware Impact: i3/MX350 samples a compact LUT instead of evaluating live radial polynomial; precise microseconds require profiler proof.

### D03: Editor-Only Mesh-Aware CRT Baker

Problem: The assignment requires screen projection onto curved physical terminal meshes, but no safe runtime mesh projection path is acceptable under the terminal hot path.
Solution: Added `UiScreenMeshProjector1721` as an `#if UNITY_EDITOR` `EditorWindow` and MenuItem baker. It loads the existing curved terminal anchor mesh path `Assets/_Project/Art/Meshes/M_Diegetic_HUD_V4_CurvedPanel.asset`, derives aspect and curvature weight from mesh bounds, passes them through a fixed compute parameter struct, and bakes the distortion result into serialized assets.
Rejected Alternatives: Runtime mesh sampling was rejected because it would add per-screen geometry/pixel work. A pure 2D default LUT was rejected because it ignores the physical screen anchor.
Scalability potential: Low = 1024 albedo/MRAO + 512 LUT; Middle = interpolated 1536-2304 range through continuous weight; High = 3072 + 1536 LUT; Ultra = 4096 + 2048 LUT.
Hardware Impact: i3/MX350 pays only texture fetches in runtime. Offline bake uses GPU compute and readback inside Editor only.

### D04: Packed Atlas Contract

Problem: Separate burn-in, scanline, glass, roughness, AO, and emissive textures would multiply sampler cost and VRAM residency for every terminal screen.
Solution: Compute output uses one RGBA projection LUT where R/G = distorted UV, B = burn-in, A = periodic scan/noise. Main albedo uses RGB with template alpha. Packed MRAO uses R = metallic, G = roughness/scratches, B = AO, A = emissive/phosphor. Importer forces Clamp and compressed high-quality platform overrides: BC7 for Standalone, ASTC_6x6 for Android/iPhone.
Rejected Alternatives: Raw uncompressed EXR/PNG imports were rejected. Individual mask texture files were rejected because they add sampler pressure with no gameplay value.
Scalability potential: Same channel contract across Low/Middle/High/Ultra; only static texture resolution changes through `GlobalQualityWeight`.
Hardware Impact: A single 4096 BC7 RGBA texture is 16 MiB. Four independent 4K screen atlases are 64 MiB and fit the 110 MB target. Four complete albedo+MRAO pairs are 128 MiB before LUT overhead; that exceeds 110 MB, so four-group Ultra must share MRAO, reduce group count, or use High-tier resolution. Exact Unity residency requires Memory Profiler.

### D05: Shared Runtime Binding Without Material Clones

Problem: The terminal runtime needs baked CRT assets without constructing per-terminal materials or dynamic runtime textures.
Solution: Added serialized baked CRT texture references and weights to `TerminalOsRuntime`, bound through the existing shared `terminalArrayMaterial` and shader globals. The dirty hash only rebinds when references/weights change; steady-state `LateFrameTick` only checks integer/hash state.
Rejected Alternatives: Per-renderer material cloning, `Renderer.material`, and runtime `new Texture2D` were rejected. MaterialPropertyBlock arrays were not added because current terminal state already uses texture array slices and shared buffers; adding a second per-renderer route would create duplicate authority.
Scalability potential: Low sets smaller baked assets and lower weights; Middle/High/Ultra increase visual density with the same runtime route.
Hardware Impact: i3/MX350 avoids SRP Batcher breakage and managed material allocation. Exact microseconds saved remain unclaimed without Unity Profiler capture.

### D06: Compaction Fence Audit Boundary

Problem: TerminalOS passes native pointers from DataVault arrays into jobs. If a DataVault compaction relocation occurs around schedule time, unsafe consumers must fail closed or complete under owner control.
Solution: The existing `TryOpenVaultBuffer` and `TryReadVaultBuffer` both reject if `_vault.IsCompactionFenceActive` before and after handle resolution. The 1721 changes do not add any DataVault read/write, pointer, or job schedule path; they only bind serialized textures/material uniforms. Existing risky lanes remain owner-phase code, not part of the new CRT projection route.
Rejected Alternatives: Adding a new vault lane for baked textures was rejected because baked textures are Unity assets, not native gameplay truth. Polling `GlobalDataVault.TryGetLatestCreated()` was rejected as bootstrap/editor/diagnostic only.
Scalability potential: No tier changes; compaction behavior remains independent of quality.
Hardware Impact: Zero added native memory pressure and zero added compaction race surface on low-end silicon.

### D07: Verification Gate Discipline

Problem: The prompt requires a build, but project policy forbids launching `dotnet build` above 50% CPU or while another compiler/dotnet process is active.
Solution: Sampled CPU/process gate. The gate first blocked at `CPU_LOAD=100`, then opened at `CPU_LOAD=46`, `DOTNET_COUNT=0`, `CSC_COUNT=0`. One throttled `Hecton8.Core.csproj` build was run. It failed on upstream files outside 1721 scope: `H8AppliedLoreRuntime.cs(70)` CS8168/CS8350 and `PredatorCognitionDomain.cs(6901)` CS0128. `TerminalOsRuntime.cs` was included and emitted no errors. Editor baker coverage is pending Unity project regeneration because `Hecton8.Project.Editor.csproj` is absent.
Rejected Alternatives: Starting repeated builds was rejected. Editing Data/Monolith or Fauna compile errors was rejected because it violates the 1721 domain boundary and would overwrite parallel agents.
Scalability potential: No runtime tier impact.
Hardware Impact: Prevents host contention; no runtime microsecond claim.

### D08: Source-Only Polish Over Report I/O

Problem: The newest directive rejects JSON proof artifacts and asks for source-level proof instead.
Solution: Removed the generated JSON report artifact from the current change set, tightened runtime readiness, added shader-side horizontal tear driven by baked alpha, added compute DTO stride validation, and fixed a public/private C# signature risk in the baker.
Rejected Alternatives: Keeping stale JSON hashes after code edits was rejected. Running `dotnet build` under `CPU_LOAD=100` with 8 active dotnet processes was rejected by compile throttle; one later build under `CPU_LOAD=46` exposed upstream errors, not 1721 errors.
Scalability potential: Low through Ultra still use the same code path; higher tiers consume higher offline bake resolution and stronger baked visual detail.
Hardware Impact: No new CPU hot-path allocations; shader animation is GPU-side visual math only.

### D09: Preserve Projection LUT Precision

Problem: Importing the UV projection LUT through the same BC7/ASTC path as albedo and MRAO can quantize distorted UV coordinates and create shimmer on curved terminal glass.
Solution: Split texture importer roles inside `UiScreenMeshProjector1721`: albedo remains sRGB compressed, MRAO remains linear compressed, projection LUT becomes linear RGBAHalf uncompressed, non-readable, clamp-filtered, and validated for finite RGBA values before import.
Rejected Alternatives: Keeping LUT compression was rejected because saved VRAM is not worth corrupting a coordinate texture. Runtime shader recomputation was rejected because it moves editor math into every terminal fragment.
Scalability potential: Low keeps LUT resolution at 512, Middle/High scale through continuous `GlobalQualityWeight`, Ultra reaches 2048 only when the budget allows it. No binary quality switch.
Hardware Impact: i3/MX350 pays a smaller low-tier LUT and avoids projection artifacts; high-end machines spend extra static VRAM for stable curved-screen fidelity. Runtime CPU impact remains 0.

### D10: Avoid Dead Baked Texture Samples

Problem: The shader previously sampled projection LUT, baked albedo, and packed MRAO before applying the ready flag. Disabled or incomplete baked assets still paid three texture fetches per terminal fragment.
Solution: Move baked texture sampling behind a uniform `UNITY_BRANCH` on `_TerminalScreenBakedProjectionReady`. Disabled path samples only `_TerminalTextureArray` and then applies existing comfort black mask.
Rejected Alternatives: Keeping multiply-by-zero after texture fetch was rejected because it wastes GPU bandwidth on low-end hardware. CPU-side material variants were rejected because they reintroduce material authority churn.
Scalability potential: Low devices can leave baked projection disabled or partial without hidden sampler cost. Middle/High/Ultra enable the branch and spend the saved budget on baked fidelity.
Hardware Impact: i3/MX350 disabled path loses three texture reads per fragment. Exact microseconds require GPU capture; no numeric gain is claimed.

### D11: Editor Baker Folder Failure Must Be Explicit

Problem: `TryEnsureAssetFolder` could return false after `AssetDatabase.CreateFolder` failure without a concrete reason.
Solution: Check returned GUID per created segment and set failure text if folder creation fails or final folder remains invalid.
Rejected Alternatives: Relying on Unity console side effects was rejected because batch agents need deterministic failure text.
Scalability potential: No runtime tier impact.
Hardware Impact: Runtime 0; editor retry cost reduced when asset database refuses a path.

### D12: Baked Texture Set Must Fail Closed

Problem: A non-null but wrong texture assignment could enable CRT projection with mismatched albedo/MRAO dimensions or a non-square projection LUT.
Solution: `ResolveBakedCrtProjectionReady()` now validates square albedo, matching MRAO dimensions, square LUT, and minimum dimensions before setting shader ready state.
Rejected Alternatives: Trusting inspector assignments was rejected because manual asset swaps are common during visual iteration. Runtime exception logging was rejected because terminal screens must fail closed, not spam logs.
Scalability potential: Low through Ultra keep the same validation route; only texture resolution changes.
Hardware Impact: Runtime CPU impact is limited to binding refresh, not steady material allocation; invalid GPU sampling state is blocked.

### D13: Remove Enabled-Path Duplicate Terminal Sample

Problem: After gating baked texture samples, the enabled shader path still sampled `_TerminalTextureArray` once before the branch and again after projected UV distortion.
Solution: Move terminal array sampling into branch-local paths. Disabled path samples original UV once; enabled path samples projected UV once after LUT distortion.
Rejected Alternatives: Keeping a dead pre-sample was rejected because enabled CRT mode is the expensive visual mode and must not waste sampler bandwidth.
Scalability potential: Low disabled path stays cheapest; Middle/High/Ultra baked path spends exactly one terminal content fetch plus baked visual fetches.
Hardware Impact: One terminal array sample removed from every baked CRT fragment on i3/MX350-class GPUs. Exact microseconds require GPU capture.

### D14: Prewarm Baker Param Upload

Problem: The baker used a temporary `NativeArray<ScreenProjectorBakeParams1721>` for a single compute parameter struct, and local evidence did not prove the `SetData(NativeArray<T>)` overload across generated project files.
Solution: Use one static one-element `ScreenProjectorBakeParams1721[]` scratch buffer and `ComputeBuffer.SetData(Array)`.
Rejected Alternatives: `new[] { parameters }` was rejected as per-bake GC. Keeping temporary `NativeArray` was rejected as unnecessary editor allocation churn.
Scalability potential: No runtime tier impact; editor baker stays deterministic.
Hardware Impact: Runtime 0; editor bake path removes one small allocation/dispose pair per bake.

### D15: Remove Redundant Editor Churn

Problem: `OnDisable()` cleared the designer-selected compute shader reference, and `TryBake()` called a global `AssetDatabase.Refresh()` after exact `ImportAsset` plus `SaveAndReimport` calls.
Solution: Preserve the serialized compute reference and rely on targeted imports/reimports plus `SaveAssets`.
Rejected Alternatives: Global refresh was rejected because it is a broad editor stall with no added correctness after exact asset imports.
Scalability potential: No runtime tier impact; baker iteration is less disruptive across all machines.
Hardware Impact: Runtime 0; editor stall risk reduced on low-end workstations.

### D16: Dirty-Gate Baked CRT Binding Validation

Problem: `LateFrameTick` recalculated baked CRT binding hash every frame, including Unity texture dimension reads and object entity ID folding. This was allocation-free, but still unnecessary native property polling in the presentation phase.
Solution: Add `_bakedCrtBindingDirty` and `_bakedCrtProjectionReady` cache. Lifecycle and editor `OnValidate` mark the CRT binding dirty; `LateFrameTick` resolves texture validity only while dirty; shared material/global binding consumes cached readiness.
Rejected Alternatives: Leaving per-frame hash polling was rejected because inspector-time texture swaps do not justify steady-state native calls. A new manager or registry route was rejected because this is local material binding state.
Scalability potential: Low through Ultra keep the same baked atlas contract. Lower devices avoid steady polling; higher tiers spend saved CPU margin on shader-side CRT polish, not new authority routes.
Hardware Impact: i3/MX350 removes recurring Unity texture/object property polling from the terminal visual phase. Exact microseconds are not claimed without Profiler capture.
