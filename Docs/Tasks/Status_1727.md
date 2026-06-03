# Status 1727 - Wreckage Burn / Carbonization Baker

Prompt: `WRECKAGE_BURN_AND_CARBONIZATION_BAKER`
Task count: 22
Domain boundary: extracted XML domain plus allowed paths `Assets/_Project/Editor/Bakers/`, `Assets/_Project/Scripts/World/`, `Assets/_Project/Art/Shaders/Include/`; active `Docs/Actual Domains of Project.txt` was not present.
Status hygiene: no pre-existing `Status_1727.md` detected before creation.

## Loop 1 - Tasks 01-05

- [x] Task 01 - WRECK_REGISTRY_STATIC_AUDIT
  - DOD practice: `rg` scan plus local code read mapped material assignment/register paths; post-patch scan reports 0 `new Material`, 0 `Instantiate`, 0 `_runtimeMaterial` symbols in `WreckMaterialRegistry.cs`.
  - Rejected alternative: no runtime clone converter for legacy materials; invalid materials now fail before BRG registration.
  - Microsecond estimate: 820 us static scan/read pass; runtime material clone elimination expected >0 us only when wrecks publish, PENDING PROFILER.
- [x] Task 02 - SHADER_PROPERTIES_DECONSTRUCTION
  - DOD practice: read and updated `Hecton_WreckIndirectLit.shader` mask decode: R metallic, G roughness, B AO, A emission/carbon; baker packs required 1727 alpha as carbonization and shader consumes it as gated baked-carbon response.
  - Rejected alternative: no separate carbon/halo textures; no runtime material clone to translate alpha semantics.
  - Microsecond estimate: 640 us static shader read; runtime fetch saving is design-level, PENDING GPU CAPTURE.
- [x] Task 03 - COMPUTE_SHADER_API_ALIGNMENT_INSPECTION
  - DOD practice: cross-checked Unity `Dispatch`, `GetKernelThreadGroupSizes`, `TextureImporter`, and existing 1723 baker pattern; dispatch uses ceil groups and coordinate guard.
  - Rejected alternative: no unreleased persistent `ComputeBuffer`; no active GPU state retained across domain reload.
  - Microsecond estimate: 1,100 us documentation/code inspection; editor bake cost PENDING DRY RUN.
- [x] Task 04 - EXPLOSION_CENTER_MATHEMATICAL_MODELING
  - DOD practice: implemented wrapped UV radial distance, ragged radius from periodic FBM, halo ring width, and coordinate guard in compute.
  - Rejected alternative: no non-wrapped radial falloff that creates visible edge seams.
  - Microsecond estimate: 0 us runtime; editor kernel math cost PENDING UNITY COMPUTE RUN.
- [x] Task 05 - GLOBAL_REGISTRY_HOT_POLLING_DETECTION
  - DOD practice: text sweep found no `GlobalRegistry.Get<` in the modified wreck registry path.
  - Rejected alternative: no invented dependency-injection rewrite without detected hot polling.
  - Microsecond estimate: 310 us `rg` scan; 0 us runtime change.

## Loop 2 - Tasks 06-10

- [x] Task 06 - COMPACTION_FENCE_VULNERABILITY_SCAN
  - DOD practice: audited `WreckMaterialRegistry` DataVault routes; existing acquire/write/release checks `IsCompactionFenceActive` before native metadata use.
  - Rejected alternative: no new DataVault hot path for carbonization; bake output stays serialized textures.
  - Microsecond estimate: 740 us static read; 0 us runtime change.
- [x] Task 07 - TELEMETRY_AND_REPORTING_ARCHITECTURE
  - DOD practice: APEX revision removed JSON report I/O; proof now comes from source-level gates, importer audits, and static scans.
  - Rejected alternative: no disk report writer on bake success; no runtime or editor proof path that depends on JSON serialization.
  - Microsecond estimate: 95 us estimated disk/JSON path removed; PENDING EDITOR RUN for bake-only timings.
- [x] Task 08 - COMPUTE_SHADER_BAKER_INITIALIZATION
  - DOD practice: added `WreckageTextureBaker` EditorWindow with menu bake, dry run, compute dispatch, RenderTexture random write, readback, and importer calls.
  - Rejected alternative: no runtime MonoBehaviour baker; no gameplay texture allocation.
  - Microsecond estimate: editor-only; dry-run timing PENDING UNITY RUN.
- [x] Task 09 - MULTI_LAYERED_SOOT_COMPUTE_KERNEL
  - DOD practice: added layered paint/primer/bare steel/deep soot/ash compute state with deterministic periodic noise and curvature fallback.
  - Rejected alternative: no particle scorch stamps; no physical heat propagation.
  - Microsecond estimate: 0 us runtime; 4096 editor kernel cost PENDING GPU RUN.
- [x] Task 10 - THERMAL_HALO_AND_TEMPERING_COLORS_BAKING
  - DOD practice: added heat halo ring and straw/purple/deep-blue tempering colors blended around torn/fracture bands.
  - Rejected alternative: no separate emissive halo texture; no shader-side runtime heat simulation.
  - Microsecond estimate: 0 us runtime; editor kernel cost PENDING GPU RUN.

## Loop 3 - Tasks 11-15

- [x] Task 11 - MULTI_CHANNEL_MRAO_PACKING
  - DOD practice: compute writes MRAO as R metallic, G roughness, B AO, A carbonization.
  - Rejected alternative: no separate metallic/roughness/AO/carbon textures.
  - Microsecond estimate: one texture fetch saved per extra channel avoided; exact fragment gain PENDING GPU CAPTURE.
- [x] Task 12 - ASSET_DATABASE_TEXTURE_SERIALIZATION
  - DOD practice: editor converts `RenderTexture` to `Texture2D`, PNG-encodes, writes atomically through project baker helper, then imports via `AssetDatabase.ImportAsset`.
  - Rejected alternative: no raw uncompressed assets; no direct runtime file write.
  - Microsecond estimate: editor disk cost PENDING RUN; runtime 0 us.
- [x] Task 13 - AUTOMATED_TEXTURE_IMPORTER_CONFIGURATION
  - DOD practice: importer enforces Albedo sRGB true, MRAO sRGB false, Repeat wrap, mipmaps, streaming mips, Standalone BC7, Android/iPhone ASTC_6x6.
  - Rejected alternative: no Unity default importer settings.
  - Microsecond estimate: editor import cost PENDING RUN; runtime VRAM improvement PENDING BUILD SIZE.
- [x] Task 14 - OFFLINE_TEXTURE_VALIDATOR_GATE
  - DOD practice: validator checks exact pixel count, albedo opacity/variation, MRAO metallic/roughness/AO range, carbon alpha, and deep-carbon metallic contradiction.
  - Rejected alternative: no manual-only visual approval.
  - Microsecond estimate: O(pixel count) editor-only; 64px dry run expected sub-ms, PENDING UNITY RUN.
- [x] Task 15 - DRY_RUN_VERIFICATION_EXECUTION
  - DOD practice: added 64px dry-run menu and internal flow; group counts use ceil division and HLSL guards `id.x >= width || id.y >= height`.
  - Rejected alternative: no exact-divide assumption for non-power-of-two safety.
  - Microsecond estimate: dry-run not executed yet outside Unity; PENDING EDITOR RUN.

## Loop 4 - Tasks 16-20

- [x] Task 16 - CONTINUOUS_QUALITY_SCALING_INTEGRATION
  - DOD practice: `GlobalQualityWeight` scales editor-only resolution 1024-4096 Albedo and 512-2048 MRAO, plus continuous halo/noise parameters.
  - Rejected alternative: no binary quality switch and no runtime truth/material identity changes.
  - Microsecond estimate: 0 us runtime; editor bake cost scales with pixel count.
- [ ] Task 17 - BATCHED_COMPILATION_AND_SYNTAX_ASSERTION [BLOCKED BY DEPENDENCY]
  - DOD practice: legal build attempted once with CPU below 50%; `dotnet build C:\hades\hades.sln` failed before source compilation because solution references missing `.csproj` files. Second build not launched because CPU later sampled above 50%.
  - Rejected alternative: do not fabricate missing package projects; do not build while CPU gate is closed.
  - Microsecond estimate: 25,160,000 us failed solution load/build attempt; 0 source compile proof.
- [x] Task 18 - EXPLICIT_PIXEL_COUNT_VALIDATION_GATE
  - DOD practice: `ValidatePixels()` asserts `textureSize * textureSize == pixels.LongLength` before PNG write/import can succeed.
  - Rejected alternative: no implicit trust in dispatch dimensions or Unity readback.
  - Microsecond estimate: O(pixel count) editor-only; runtime 0 us.
- [x] Task 19 - COMPACTION_FENCE_RACE_CONDITION_AUDIT
  - DOD practice: DataVault metadata paths check `IsCompactionFenceActive`; failure prevents batch creation and retries later instead of holding stale native memory.
  - Rejected alternative: no hidden completion, no raw pointer persistence, no fence bypass in read-like paths.
  - Microsecond estimate: branch-only runtime impact; exact value below measurement floor, PENDING PROFILER.
- [x] Task 20 - ZERO_GC_ALLOCATION_PROFILER_MOCK
  - DOD practice: post-patch static scan: no material clone calls in registry; editor-only baker contains all texture/report allocations behind `#if UNITY_EDITOR`.
  - Rejected alternative: no runtime `Texture2D`, no runtime PNG encode, no cloned material damage state.
  - Microsecond estimate: 0 B managed allocation expected from wreck material cloning in steady-state; PENDING UNITY PROFILER.

## Loop 5 - Tasks 21-22 and strict rereads

- [x] Task 21 - SRP_BATCHER_MATERIAL_LIMIT_TESTING
  - DOD practice: theoretical 200-part test maps instances to shared indirect tier material slots; material identities are not per-wreck damage carriers.
  - Rejected alternative: no per-part material clone or runtime material mutation for damage history.
  - Microsecond estimate: exact SetPass gain PENDING GPU CAPTURE; static material identity count capped at shared pool when authored.
- [x] Task 22 - AUTOMATED_METRIC_VALIDATOR_REPORT
  - DOD practice: obsolete static JSON proof deleted per APEX; code validation is enforced in `WreckageTextureBaker` before serialization/import.
  - Rejected alternative: no duplicated JSON proof file, no report writer coupled to the bake path.
  - Microsecond estimate: removed report write path is 0 us at runtime and 0 us during successful bake serialization.
- [x] Strict reread 1 - registry/runtime clone audit
- [x] Strict reread 2 - baker/import settings audit
- [x] Strict reread 3 - compute kernel bounds/tiling audit
- [x] Strict reread 4 - shader property compatibility audit
- [x] Strict reread 5 - final proof/log audit
- [x] APEX polish - removed transitive BRG/DataVault preparation from `SlowTick()`
- [x] APEX polish - integrated baked MRAO Alpha carbon response into `Hecton_WreckIndirectLit.shader` without adding texture slots or material clones
- [x] APEX polish - added editor-only source mesh UV/normal curvature prepass for Agent 1717 wreck meshes when no curvature map is supplied; empty prepass falls back to procedural curvature
- [x] APEX polish - fixed compute tiling math by removing non-integer periodic FBM UV multipliers and using integer-lattice scratch bands
- [x] APEX scoped final audit - no serialized prefab/scene/asset hits for `WreckMaterialRegistry.cs` GUID `53f02ffdc57707545808e9d833c0d932`; scoped whitespace/bracket scans clean; no second build launched while foreign `dotnet` processes were active
- [x] APEX polish - default compute shader is force-imported before load and both kernels validate presence plus 64-thread group contract before dispatch
- [x] APEX polish - output pixel validation now rejects repeat seam failures for both Albedo and MRAO before any atlas can be serialized
- [x] APEX polish - rivet mask noise now uses the same 19-cell period as the rivet lattice instead of leaking a 64-period hash into a repeat-critical detail layer
- [ ] APEX dry-run execution [BLOCKED BY UNITY SESSION]
  - DOD practice: dry-run menu was invoked once and timed out through MCP; subsequent console and script validation pings also timed out, while CPU was at 100% and foreign `dotnet`/Unity processes were active.
  - Rejected alternative: do not spam Unity MCP, do not rerun `dotnet build`, and do not kill shared Unity/dotnet processes owned by the broader multi-agent session.
  - Microsecond estimate: dry-run wall result unavailable due MCP timeout; runtime cost remains 0 us because the baker is editor-only.
