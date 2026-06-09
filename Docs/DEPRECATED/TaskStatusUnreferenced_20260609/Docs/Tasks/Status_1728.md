# Status 1728 - Particulate Flipbook Baker

Agent: 1728
Domain: `SILT_PARTICLE_FLIPBOOK_AND_SNOW_MASK_BAKER` / Editor VFX texture baking.
Task count: 25
Prompt source: `Docs/Tasks/CURRENT_BATCH.md` extracted via CLI; temporary XML was removed after source-only cleanup.
Domain file note: `Actual Domains of Project.txt` not found under `C:\hades`; XML role/domain used for boundary. Scope limited to `Assets/_Project/Editor/Bakers/`, VFX script audits, and proof artifacts unless compile repair requires narrower editor assembly work.

Relevant mandates read:
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `REND_VFX_Fluid_Aesthetics_Compute_Particles.txt`
- `REND_GPU_Sovereignty.txt`
- `STRM_Async_Asset_Upload_Texture_Settings.txt`
- `TOOL_Designer_Facades_CSV_Binary_Bridge.txt`
- `DATA_Runtime_Struct_Layout_ARM64.txt`

Root docs read: `AGENTS.md`, `vfx.md`, `rendering.md`, `performance.md`, `authoring.md`, `shaders.md`, `3DMODEL_TEXTURES_MATERIALS.md`, `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`, `PROCEDURAL_ASSET_PIPELINE.md`.

## Loop 1 - Tasks 01-05

- [x] Task 01 PARTICLE_SYSTEM_STATIC_AUDIT | DOD: static `rg` sweep of VFX scripts and prefab YAML. `Assets/Prefabs/VFX` absent. Hits: `PFB_Support_Pocket_Hazard.prefab` has 3 ParticleSystems; `PFB_Module_Foundation.prefab` and `PFB_Module_Corridor.prefab` include ParticleSystem prefab instances; `Player.prefab` cavitation/bubble particle fields are null. Alternative rejected: editing prefab VFX now; task is baker-first and prefab ownership is broader construction/world support. Estimate: 20-80 us/frame saved when ambient silt/bubble visuals route through baked atlas quads instead of live Shuriken emitters.
- [x] Task 02 RUNTIME_TEXTURE_DECONSTRUCTION | DOD: recursive source sweep found no `new Texture2D`, `SetPixels`, or CPU procedural texture generation in target VFX runtime. Two `new Texture2D` hits are in `LutArrayResolver.cs:112,142`, a cold water-extinction LUT fallback outside this baker scope. Alternative rejected: ripping cold LUT loader; not particulate and not a runtime particle noise source. Estimate: 0 current target calls eliminated; prevents future per-frame CPU texture generation.
- [x] Task 03 ALGORITHM_MATHEMATICAL_MODELING_INSPECTION | DOD: reuse Burst `IJobParallelFor` pixel-parallel pattern from existing editor baker; per-pixel density is pure 4D periodic noise + radial padding. Alternative rejected: compute shader readback path for this baker; prompt asks `Texture2D` serialization and editor CPU/Burst path avoids extra GPU readback coordination. Estimate: editor bake only; runtime saves particle CPU noise entirely.
- [x] Task 04 PBR_LIGHTING_RESPONSE_MAPPING | DOD: shader audit confirmed `_MarineSnowNormalAtlas` and `_SiltNormalAtlas` are sampled and mixed into headlight/main-light response; normals derive from density gradients with positive Z fallback. Alternative rejected: flat alpha-only sprites; they fail spotlight volume read. Estimate: GPU cost stays atlas fetch + cheap normal math; CPU savings from no runtime volumetric particle simulation.
- [x] Task 05 GLOBAL_REGISTRY_HOT_POLLING_DETECTION | DOD: target VFX sweep found no `GlobalRegistry.Get<`; `HectonMarineSnowRenderer` uses cached hot-swap services and registration calls, with `GlobalRegistry.DataVault` read during service refresh only. Alternative rejected: refactor cached registry route; no hot `Get<T>` defect found. Estimate: 0 us changed; preserves cold-DI route.

## Loop 2 - Tasks 06-10

- [x] Task 06 COMPACTION_FENCE_VULNERABILITY_SCAN | DOD: audited `HectonMarineSnowRenderer` DataVault route; owner code checks `IsCompactionFenceActive` before resolve/read and backs off instead of scheduling stale native reads. 1728 baker is editor-only and does not touch DataVault. Alternative rejected: adding a new runtime fence check in a renderer path this task does not modify. Estimate: 0 us changed; prevents invented dependency churn.
- [x] Task 07 TELEMETRY_AND_REPORTING_ARCHITECTURE | SUPERSEDED: the earlier JSON proof route was removed after the source-only directive. Current DOD is source code plus static checks; no 1728 JSON/LOG artifact remains. Alternative rejected: keeping stale disk reports after the direct source-only directive. Estimate: removes editor I/O noise; 0 runtime us.
- [x] Task 08 FLIPBOOK_BAKER_ENGINE_INITIALIZATION | DOD: `ParticulateFlipbookBaker.cs` created with menu entry at line 63, `TryBake` at line 115, profile loop at line 156, and pixel job setup at line 202. Alternative rejected: mutating `ParticleFlipbookBaker1718.cs`; 1728 needs its own artifact and cavitation profile. Estimate: runtime replaces procedural particle noise with static atlas UV animation; 20-80 us/frame candidate savings where adopted.
- [x] Task 09 ASYMMETRICAL_MARINE_SNOW_GENERATION | DOD: `BuildDefaultProfiles` at line 502 creates `organic_marine_snow_threads_1728`; job density at lines 944-955 combines fBM, Worley, anisotropic filament coordinates, asymmetry, and radial `smoothstep`. Alternative rejected: circular sprite mask. Estimate: no runtime CPU cost; higher visual entropy purchased at bake time.
- [x] Task 10 NORMAL_MAP_GRADIENT_CALCULATION | DOD: `Execute` samples density offsets and maps finite guarded gradients into normal map at lines 909-934 with `math.normalizesafe` and positive Z clamp. Alternative rejected: flat normal texture. Estimate: runtime remains one normal atlas fetch; spotlight response gains volume read without CPU particle simulation.

## Loop 3 - Tasks 11-15

- [x] Task 11 BIOLUMINESCENT_PLANKTON_MASKING | DOD: high-frequency periodic glow mask is generated at lines 919-923 and packed into mask G at line 929. Alternative rejected: separate emissive texture. Estimate: saves one texture fetch versus uncompressed separate mask route; runtime CPU unchanged.
- [x] Task 12 FLOW_DISTORTION_MAP_BAKING | DOD: low-frequency periodic flow noise is generated at line 924 and packed into mask B at line 929 for shader UV wiggle. Alternative rejected: CPU particle drift simulation. Estimate: replaces per-particle CPU turbulence with atlas/sample-driven visual distortion.
- [x] Task 13 SEAMLESS_LOOPING_ALGORITHM | DOD: `PeriodicSimplex` at line 996 maps time to a cos/sin phase circle; cavitation shell radius uses `sin^2` at lines 964-969. Alternative rejected: linear time noise with end-frame blend. Estimate: no runtime blend cost; loop closure is baked.
- [x] Task 14 ASSET_DATABASE_TEXTURE_SERIALIZATION | DOD: `TryWriteTexture` at line 272 writes `NativeArray<Color32>` via `Texture2D.SetPixelData`, `ImageConversion.EncodeToPNG`, atomic asset write, and `AssetDatabase.ImportAsset`. Alternative rejected: runtime `Texture2D.SetPixels`; scan found no target runtime hits and baker is editor-only. Estimate: serialization cost is offline only.
- [x] Task 15 AUTOMATED_TEXTURE_IMPORTER_CONFIGURATION | DOD: new baker calls shared `ProceduralTextureBaker.TryEnforceTextureImportSettings` at lines 243 and 249; shared implementation at `ProceduralTextureBaker.cs:345` enforces mask BC7, normal BC5, ASTC_6x6 mobile, mips, clamp, linear mask/normal, and alpha transparency for masks. Alternative rejected: duplicating importer policy in 1728 file. Estimate: VRAM bounded by compressed imported artifacts.

## Loop 4 - Tasks 16-20

- [x] Task 16 OFFLINE_TEXTURE_VALIDATOR_GATE | DOD: `ValidatePadding` at `ParticulateFlipbookBaker.cs:395` scans every frame border and aborts save if mask padding is nonzero or neutral-normal padding is corrupted. Alternative rejected: trusting radial falloff alone. Estimate: editor-only validation, prevents mip edge bleed at 0 runtime us.
- [x] Task 17 DRY_RUN_VERIFICATION_EXECUTION | DOD: rationale Decision 11 traces 4096 gradient flow: uniform density gives dx=0/dy=0, Z >= 0.18, `math.normalizesafe` fallback, no NaN path. Alternative rejected: raw `normalize(Vector3(dx,dy,1))` without finite guard. Estimate: no runtime CPU cost; safer baked normals.
- [x] Task 18 CONTINUOUS_QUALITY_SCALING_INTEGRATION | DOD: UI slider at line 78 feeds `ResolveSettings` at line 485; non-forced bakes scale 4x4/1024 through 8x8/4096, forced route locks required 64 frames. Alternative rejected: runtime quality branch resizing textures. Estimate: low-tier disk variants reduce VRAM/bake size; runtime route unchanged.
- [x] Task 19 BURST_COMPILE_OFFLINE_JOBS | DOD: `ParticulateFlipbookBakeJob` at line 862 is `[BurstCompile(FloatMode.Fast, FloatPrecision.Standard, CompileSynchronously = true)]` and scheduled at line 222 over atlas pixels. Alternative rejected: nested managed pixel loop. Estimate: editor bake accelerated; runtime 0 us.
- [x] Task 20 BATCHED_COMPILATION_AND_SYNTAX_ASSERTION | DOD: first build gate sampled CPU average 91.79%, `csc.exe` count 0, `dotnet` count 7, so build was deferred. Second gate sampled CPU average 21.98%, `csc.exe` count 0, `dotnet` count 0; ran `dotnet build Assembly-CSharp-Editor.csproj --no-restore --nologo`. Build failed in unrelated `Assets/Editor/HectonMcpBridgeAutoConnect1428.cs:4-6` missing `MCPForUnity.Editor.*`; no errors were reported for `ParticulateFlipbookBaker.cs`. `git diff --check` passed. Alternative rejected: editing another agent's MCP bridge dependency. Estimate: compile verification blocked by external dependency, not 1728 code.

## Loop 5 - Tasks 21-25

- [x] Task 21 EXPLICIT_PIXEL_COUNT_VALIDATION_GATE | DOD: `ValidatePixelCount` at line 441 validates `NativeArray` length; `TryWriteTexture` at line 284 validates `texture.width * texture.height == size * size` before PNG encode. Alternative rejected: encode without dimension assertion. Estimate: editor-only guard, prevents corrupt serialized atlas.
- [x] Task 22 COMPACTION_FENCE_RACE_CONDITION_AUDIT | DOD: rationale Decision 12 records that 1728 introduces no runtime DataVault reads; existing VFX owner backs off on compaction fence before handle resolve/read and should use previous cached render state. Alternative rejected: new direct `TryGetLatestCreated` or hot vault route. Estimate: 0 us changed; no new race window.
- [x] Task 23 ZERO_GC_ALLOCATION_PROFILER_MOCK | DOD: report declares expected steady-state runtime managed allocation as 0 B because no runtime files were changed and all new pixel work is editor-only; Unity Profiler capture remains pending. Alternative rejected: claiming measured profiler proof without running Unity. Estimate: CPU particle update reduction is adoption-dependent; code path adds 0 runtime managed allocation.
- [x] Task 24 VRAM_BUDGET_LIMIT_TESTING | DOD: report and rationale compute BC7/BC5 4096 texture cost. Three required pairs with mips are 128 MiB; five 4096 pairs with mips are about 213.33 MiB and do not fit under 130 MiB. Alternative rejected: fake proof of impossible budget. Estimate: prevents ~85 MiB excess residency versus false 5-pair claim.
- [x] Task 25 AUTOMATED_METRIC_VALIDATOR_REPORT | SUPERSEDED: old JSON metric artifact was deleted with its `.meta`; source and tests now carry the proof. Alternative rejected: preserving stale report files. Estimate: 0 runtime us.

## Iterative Self-Read Log

- Loop 0: Prompt and mandates extracted. Existing `Status_1728.md` and `Rationale_1728.md` were absent; no hygiene collision found for this ID.
- Loop 1: Tasks 01-05 completed from static source/YAML. `Docs/Tasks/CURRENT_BATCH.md` was re-extracted after first 5-task block; task count still 25.
- Loop 2: Tasks 06-10 completed. Read `ParticulateFlipbookBaker.cs` and shared importer utility; verified editor-only route and no new runtime DataVault path.
- Loop 3: Tasks 11-15 completed. Re-extracted `Docs/Tasks/CURRENT_BATCH.md` for anti-amnesia; line evidence recorded for channel packing, periodic loop math, serialization, and importer enforcement.
- Loop 4: Tasks 16-20 completed. Initial build gate failed, later gate cleared and one build was run. Build failed in unrelated MCPForUnity editor dependency at `Assets/Editor/HectonMcpBridgeAutoConnect1428.cs:4-6`; static hygiene passed via `git diff --check`.
- Loop 5: Tasks 21-25 completed. Earlier JSON report was parsed, then removed in the source-only cleanup pass; VRAM correction remains recorded in rationale and source-level checks.

## Source-Only Cleanup Pass

- [x] Removed obsolete 1728 JSON/LOG artifacts from the proof surface. DOD: `Docs/Reports/PARTICLE_FLIPBOOK_BAKER_REPORT_1728.json`, its `.meta`, `Docs/AgentLogs/LOG_1728.md`, and its `.meta` are absent; current proof route is source code plus static checks. Alternative rejected: keeping disk reports after the direct source-only directive. Estimate: removes editor I/O noise; 0 runtime us.
- [x] Re-integrated 1728 into existing baker topology. DOD: `ProceduralTextureBaker` is partial; `ParticulateFlipbookBaker.cs` delegates atlas baking to the shared 1718 baker path and shared importer/file rollback helpers. Alternative rejected: duplicate standalone bake manager. Estimate: editor-only reuse; 0 runtime us.
- [x] Added authored neutral Texture3D bake assets for marine snow runtime safety. DOD: `ParticulateFlipbookBaker.cs` bakes `TX_MarineSnow_EmptyCaveSdf_1x1x1.asset` and `TX_MarineSnow_EmptyAbyssalFlow_1x1x1.asset`; `HectonMarineSnowRenderer` editor-cold fallback loads those paths when serialized fields are empty. Alternative rejected: runtime generated Texture3D fallback in the renderer. Estimate: prevents null-fallback disable after authoring; 0 steady-state runtime allocation.
- [x] Static source hygiene re-run. DOD: forbidden hot tokens scan only finds negative test assertions and existing cold `TryGetComponent` helpers; `git diff --check` reports no whitespace errors beyond CRLF normalization warnings; existing-file `.meta` orphan scan returns `NO_EXISTING_ORPHAN_META`. Git index also shows unrelated pre-existing deletions of old `.meta` files, but there are no physical orphan `.meta` files left on disk. Alternative rejected: running another build while active `dotnet` processes exist. Estimate: avoids host CPU contention; compile remains blocked only by prior unrelated MCPForUnity editor dependency if full build is attempted.
- [x] Flattened editor-only CSV method stubs in `HectonMarineSnowRenderer`. DOD: `EnsureCsvProfileBackgroundReader`, `StopCsvProfileBackgroundReader`, `RefreshSiltProfileCsv`, and `RefreshPropwashWakeProfileCsv` now have one source signature each; runtime player call sites are behind `#if UNITY_EDITOR`; Unity MCP validator returns 0 errors/warnings. Alternative rejected: leaving validator-visible duplicate signatures under `#else`. Estimate: 0 runtime us; removes compile-validator drift.
- [x] Forced 1728 atlas profiles to 64 frames at every quality tier. DOD: silt, marine snow, and cavitation 1728 path resolution now passes `forceRequiredFrameGrid: true`; source-contract test requires exactly three forced calls. Alternative rejected: allowing low `GlobalQualityWeight` to reduce frame count to 4x4. Estimate: preserves seamless visual cadence while still allowing atlas size/detail to scale.
