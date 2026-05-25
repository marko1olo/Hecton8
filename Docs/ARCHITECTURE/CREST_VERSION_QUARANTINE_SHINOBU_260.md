# Crest Version Quarantine - SHINOBU_260

## Ownership

Agent: SHINOBU_260
Domain: CREST_VERSION_QUARANTINE_DIRECTOR
Active donor: Crest 4 under `Assets/Crest`
Quarantined donor: Crest 5 moved from `Packages/com.waveharmonic.crest` to `Docs/Archive/Crest_Version_Quarantine/Packages/com.waveharmonic.crest`

## Restore Artifacts

Baseline backup folder: `Docs/Archive/Crest_Baseline_Backup/`
Ignore policy: local `.gitignore` ignores archived payloads.
Backup zips produced by `Tools/Crest_Baseline_Archiver.py --execute`:

- `crest4_assets_crest_20260521_104429.zip`: 642 files, 8,514,554 source bytes.
- `crest5_embedded_package_20260521_104429.zip`: 750 files, 15,946,158 source bytes.

Loop 20 widened the baseline command to archive project-side Crest4 bindings that are not part of the vendor folder but are required for restore:

- `crest4_project_ocean_settings_20260521_232038.zip`: `Assets/_Project/Data/Ocean`, 10 files, 4,423 source bytes.
- `crest4_project_legacy_crest_settings_20260521_232038.zip`: `Assets/_Project/crest`, 6 files, 1,745 source bytes.
- `crest4_project_ocean_prefab_20260521_232038.zip`: `Assets/_Project/Prefabs/Ocean_Crest.prefab`, 1 file, 22,374 source bytes.
- `crest4_project_ocean_prefab_meta_20260521_232038.zip`: prefab meta, 1 file, 161 source bytes.
- `crest4_project_world_ocean_scene_20260521_232038.zip`: `Assets/_Project/Scenes/02_HECTON_WORLD.unity`, 1 file, 33,756,552 source bytes.
- `crest4_project_world_ocean_scene_meta_20260521_232038.zip`: scene meta, 1 file, 162 source bytes.

`Packages/packages-lock.json` no longer contains `com.waveharmonic.crest`.

Additional Loop 13 active-asset quarantine artifacts:

- `Docs/Archive/Crest_Version_Quarantine/Assets/_Project/Data/CrestMigration/Crest5_WaveSpectrum.asset(.meta)`
- `Docs/Archive/Crest_Version_Quarantine/Assets/_Project/Data/CrestMigration/Crest5_FoamSettings.asset(.meta)`
- `Docs/Archive/Crest_Version_Quarantine/Assets/_Project/Scenes/03_HECTON_WORLD_CREST5.unity(.meta)`
- `Docs/Archive/Crest_Version_Quarantine/Assets/_Recovery/`
- `Docs/Archive/Crest_Version_Quarantine/Assets/_Recovery.meta`

## Assembly Wall

Only these first-party assemblies may reference `Crest`:

- `Hecton8.Crest.Bridge`
- `Hecton8.Crest.Bridge.Editor`

Shared first-party assemblies no longer reference Crest or WaveHarmonic:

- `Hecton8.Plugins`
- `Hecton8.Editor`
- `Hecton8.Project.Editor`

Crest 4 asmdefs are leaf-import guarded with `autoReferenced=false`:

- `Assets/Crest/Crest/Scripts/Crest.asmdef`
- `Assets/Crest/Crest/Scripts/Editor/Crest.Editor.asmdef`

- Static proof: `Docs/Reports/ARCHITECTURE_OPTIMIZATION_REPORT.json` reports `breach_count=0`, `allowed_hit_count=40`, `reflection_string_hit_count=0`, `global_scripting_define_hit_count=1`, `compliance_denylist_hit_count=6`, and non-failing `vocabulary_debt_hit_count=111`.
- Scanner covers active serialized text in `Assets`, `ProjectSettings`, and `Packages`.
- Targets: Crest5/WaveHarmonic/direct UnderwaterRenderer/bare Crest assembly-list breaches, active `Packages/com.waveharmonic.crest` visibility.
- Additional targets: shader/HLSL/compute Crest includes outside the bridge, Unity `.asmref`, `GUID:<asmdef-guid>` refs to active Crest 4 asmdefs, archived Crest5/recovery GUID backrefs.
- non-bridge first-party `#if CREST_OCEAN` / `#if CREST_URP` branches, and policy-only Crest denylist strings in the editor compliance validator.

Loop 21 donor reference cleanup:

- `Assets/Crest/Crest/Scripts/Crest.asmdef` no longer references `Unity.RenderPipelines.HighDefinition.Runtime` or `Unity.Postprocessing.Runtime`.
- Backing HDRP/Postprocessing packages are absent from `manifest.json`, `packages-lock.json`, and physical `Packages/`.
- Selected active Crest4 donor remains URP-scoped.

Loop 21 generated-report cleanup: stale `Assets/profilermarkers.csv(.meta)` moved to `Docs/Archive/Crest_Version_Quarantine/Assets/`. The archived CSV still preserves Crest profiler rows as forensic evidence, but it is no longer Unity-visible active project input.

The scanner also fails if active Crest donor asmdefs or Crest bridge asmdefs become auto-referenced. This keeps Crest opt-in at the assembly importer level, not only at the direct-reference level.

`ProjectSettings/ProjectSettings.asset` still carries Standalone `CREST_OCEAN` and `CREST_URP` scripting defines.

- They are non-failing donor-state evidence.
- Active Crest 4 donor uses `CREST_URP` internally.
- Any first-party non-bridge use is a scanner breach.

Scanner throughput proof:

- `scan_active_assets` moved to `rg --json` with Python fallback.
- Wall time dropped from about `262s` to about `35.5s`.
- `breach_count=0` preserved.

## Contract Route

Forward route: `Hecton8.Environment.Fluids.Contracts` owns the strict unmanaged ocean contract. Legacy `Hecton8.Physics.IHectonOceanKinematics` remains intact to avoid breaking parallel agents, but new bridge traffic must use `Hecton8.Environment.Fluids.IHectonOceanKinematics`.

`OceanSampleRequestDTO` is explicit 32 bytes:

- `RequestAUP`: offset 0, `double3`, 24 bytes.
- `CallerHashID`: offset 24, `uint`, 4 bytes.
- `_pad0`: offset 28, `uint`, 4 bytes.

`OceanSampleResultDTO` is explicit 64 bytes:

- `SourceAUP`: offset 0, `double3`, 24 bytes.
- `WaterHeight`: offset 24, `float`, 4 bytes.
- `SurfaceVelocity`: offset 28, `float3`, 12 bytes.
- `WaveNormal`: offset 40, `float3`, 12 bytes.
- `LatencyMilliseconds`: offset 52, `float`, 4 bytes.
- `StatusFlags`: offset 56, `uint`, 4 bytes.
- `_pad0`: offset 60, `uint`, 4 bytes.

`OceanAdapterTelemetryEntry` is explicit 64 bytes and is written to a 300-entry vault ring.

## Vault Lanes

SHINOBU_260 owns local numeric Vault IDs after a sub-agent audit proved the older `ShinobuOcean*` names are already owned by `ShinobuOceanSurfaceAtmosphereRuntime` with incompatible element types. No core enum edit was required.

- Requests: `(BufferID)72960` `OceanSampleRequestDTO[50000]`
- Results: `(BufferID)72961` `OceanSampleResultDTO[50000]`
- Telemetry ring: `(BufferID)72962` `OceanAdapterTelemetryEntry[300]`
- Profiles: `(BufferID)72963` `OceanPerformanceProfileDTO[16]`
- Global water level: `(BufferID)72964` `OceanGlobalWaterLevelDTO[1]`
- CSV scratch: `(BufferID)72965` `byte[65536]`

All large bridge lanes are acquired with `NativeArrayOptions.UninitializedMemory`; writers must overwrite active slots deterministically.

## Runtime Boundary

`CrestOceanRuntimeAdapter` boundary:

- Namespace: `Hecton8.Crest.Bridge` only.
- Input: `NativeArray<OceanSampleRequestDTO>`.
- Output: `JobHandle`.
- Math: subtract ocean-root AUP in `double3`; cast only local deltas to `float3`.
- Latency: output delayed by 1-3 frames.
- Forbidden: `JobHandle.Complete()`, `TryGetComponent`, binding repair, `Transform.position` AUP reconstruction.

`EmergencyMockOceanKinematicsAdapter.GenerateEmergencyMockOceanAdapter()` bypasses Crest entirely and produces deterministic sine-wave results for profiling when Crest is broken. It is a value type, not a managed fallback object.

- Legacy `Crest4KinematicsAdapter` remains present for old `Hecton8.Physics` consumers.
- It is not the strict forward route.
- Binding repair was fenced:
  - `ResolveOceanRenderer()` no longer calls `TryGetComponent` or logs;
  - `TryBuildBurstTuning` uses the cached renderer;
  - weather/flow/collision reads use cached binding;
  - `SeaLevel` does not fall back through `GlobalRegistry`.

- Base `CrestBridge` no longer polls `Crest.OceanRenderer.Instance` or `Crest.UnderwaterRenderer.Instance`.
- Visual material/camera helpers read only the renderer supplied by a concrete bridge adapter.
- Underwater Has/Try helpers read a cache populated by the command path.

`IOceanVisualBridge` exposes vendor-neutral underwater pass verbs and `CameraColorTextureId`. Non-bridge render code must not hard-code `_Crest_CameraColorTexture`; `HectonDryVolumeFeature` reads the active bridge's texture ID before scheduling dry-volume restore.

- `HectonUnderwaterVisuals` no longer contains `"Crest.OceanRenderer"` or `"Crest.UnderwaterRenderer"` reflection fallbacks.
- It consumes the bridge through `IOceanVisualBridge` only.
- Old field `crestSkyBaseFogLink` is only `[FormerlySerializedAs("crestSkyBaseFogLink")]` migration metadata.
- Current field: `oceanSkyBaseFogLink`.

The shared first-party base formerly named `HectonCrestOceanKinematics` is now `HectonOceanKinematicsBridgeBase`.

- `.meta` GUID is unchanged.
- Crest-specific first-party type name is removed.
- No scene remap: class is abstract base, not attached component.

`HectonCrestOceanDepthCacheBootstrap` no longer falls back to `Crest.OceanRenderer.Instance`. It still belongs to World/depth-cache integration for broader lifecycle ownership, but the Crest singleton recovery path is removed from this quarantine bridge.

`Ocean_Crest.prefab` no longer carries the quarantined Crest5 adapter MonoBehaviour.

Exact scans found no remaining `Crest5KinematicsAdapter`, script GUID `51fcb9de0aa92b842be404fec8bf21d4`, or component fileID `4153056372701123456` in active prefabs/scenes/assets. This prefab format lacks `m_RootGameObject`; raw-YAML proof is component-list removal plus GUID/fileID absence.

`Player.prefab` no longer carries direct `Crest::Crest.UnderwaterRenderer`.

- Exact scan found no fileID `9079297290110143596`.
- Exact scan found no script GUID `1b0c0a69611596146aceb2f60532940c`.
- Exact scan found no `Crest::Crest.UnderwaterRenderer` class identifier.
- Underwater pass ownership stays behind bridge command path.

Crest-specific sargassum input shaders now live under `Assets/_Project/Scripts/Plugins/Crest/Shaders/` with original metas preserved:

- `Crest_SargassumWaveDamping.shader(.meta)`
- `Crest_SargassumFoamDamping.shader(.meta)`
- `Crest_SargassumOilFilm.shader(.meta)`

Exact shader scan reports only bridge-owned Crest shader/HLSL references. Shared `Assets/_Project/Art/Shaders` no longer owns direct Crest HLSL include paths.

`Assets/Plugins/Easy Save 3/Resources/ES3/ES3Defaults.asset` no longer lists `Crest` or `WaveHarmonic.Crest*` in global serializer assembly defaults. Root `Assets/InitTestScene*.unity` files no longer list `WaveHarmonic.Crest*` in TestRunner `m_AssembliesWithTests`.

`Assets/_Recovery` no longer exists under active Unity visibility.

It was moved with `.meta` to `Docs/Archive/Crest_Version_Quarantine/Assets/_Recovery` after static scan found binary recovery scenes with Crest strings.

This is archival containment. The folder is Unity recovery payload, not an authoritative runtime source route.

Known serialized vocabulary debt outside this agent's safe write boundary:
- `SargassumCrestDampingController`
- `HectonPlayerMovement.useCrestOceanHeight`

They create no direct Crest assembly reference. Remap only by owning agents with Unity serialization validation.

- Loop 12 low-risk text polish removed donor names from non-serialized comments/tooltips in Visor, Atmosphere, Environment, Fluid, and Sargassum authoring code.
- Remaining vocabulary debt is tracked by `Crest_Dependency_Scanner.py` as non-failing `vocabulary_debt_hits`, not as compile-wall breaches.
- Loop 21 also tracks policy-only `Crest` / `WaveHarmonic.Crest*` strings in `HectonComplianceValidator.cs` as non-failing `compliance_denylist_hits`, preserving the editor gate while preventing false hidden-coupling reports.

Task 12 status: blocked by dependency. Crest `OceanRenderer.OnEnable`/`Start` suppression requires vendor lifecycle patch by Crest-internal agent. This pass does not edit donor code.

## Static Verification

- `python Tools/Crest_Baseline_Archiver.py --execute`: passed.
- `python Tools/Crest_Dependency_Scanner.py`: passed with `breach_count=0`, `allowed_hit_count=40`, `reflection_string_hit_count=0`, `global_scripting_define_hit_count=1`, `compliance_denylist_hit_count=6`, `vocabulary_debt_hit_count=111`.
- `python Tools/Crest_Quarantine_Polish_Audit.py`: passed with `failed_count=0`, including `legacy_crest4_adapter_no_hot_component_repair`, `base_bridge_no_ocean_singleton_polling`, `base_bridge_underwater_reads_are_cache_only`, `depth_cache_bootstrap_no_ocean_singleton_fallback`, `legacy_crest4_read_accessors_do_not_log_or_poll_registry`, `legacy_crest4_tuning_is_cached_read_only`, `player_prefab_has_no_direct_underwater_renderer`, `crest5_migration_assets_outside_unity_visibility`, `crest_input_shaders_owned_by_bridge_folder`, `crest5_scene_outside_unity_visibility`, and `dependency_scanner_covers_asmref_and_crest_guid_references`.
- `Tools/Crest_Quarantine_Polish_Audit.py` also gates `dependency_scanner_blocks_archived_asset_guid_references`, proving the normal scanner will fail active links to archived Crest5/recovery object GUIDs.
- `Tools/Crest_Quarantine_Polish_Audit.py` also gates `crest_donor_asmdefs_not_auto_referenced`, `bridge_asmdef_not_auto_referenced`, and `dependency_scanner_blocks_auto_referenced_crest_assemblies`.
- `Tools/Crest_Quarantine_Polish_Audit.py` also gates `dependency_scanner_tracks_crest_scripting_defines` and `dependency_scanner_blocks_non_bridge_crest_preprocessor_branches`.
- `Tools/Crest_Quarantine_Polish_Audit.py` also gates `dependency_scanner_tracks_compliance_denylist_strings`, proving editor compliance denylist strings are visible non-failing evidence.
- `Tools/Crest_Quarantine_Polish_Audit.py` also gates `crest_donor_no_absent_hdrp_postprocessing_references`, `stale_profiler_markers_outside_unity_visibility`, `dependency_scanner_blocks_absent_optional_donor_references`, and `dependency_scanner_blocks_stale_generated_report_crest_rows`.
- `Tools/Crest_Quarantine_Polish_Audit.py` also gates `crest4_project_bindings_have_baseline_archives`, proving the latest baseline report includes project-side Crest4 settings, prefab, and scene binding archives.
- `Tools/Crest_Quarantine_Polish_Audit.py` also gates `underwater_visuals_no_crest_reflection_fallback`, `underwater_visuals_vendor_neutral_pass_vocabulary`, `visual_bridge_contract_vendor_neutral`, `dry_volume_reads_vendor_texture_id_through_bridge`, `crest5_prefab_adapter_reference_removed`, `ocean_kinematics_base_vendor_neutral`, `low_risk_non_bridge_text_uses_ocean_vocabulary`, and `dependency_scanner_tracks_vocabulary_debt`.
- `python Tools/BufferIDSovereigntyAudit.py --report-path Docs/_Archive/Reports_X_012_2026-05-23/SHINOBU_260_BufferIDSovereigntyAudit.md --json-path Docs/Reports/SHINOBU_260_BufferIDSovereigntyAudit.json`: passed as static evidence; global `duplicateValueCount=3` comes from unrelated `H8Memory.cs` values `70534..70536`, while `72960..72965` are local casts only in `OceanAdapterVaultRoute.cs`.
- `python -m py_compile Tools/Crest_Baseline_Archiver.py Tools/Crest_Dependency_Scanner.py Tools/Crest_Quarantine_Polish_Audit.py Tools/BufferIDSovereigntyAudit.py`: passed.
- `git diff --check` for touched Crest bridge, tools, and report files: passed; only Git CRLF conversion warnings were emitted.
- Exact active asset scan: no `WaveHarmonic.Crest`, Crest5 script GUIDs `382a5d8b1147b4e78a31353c022b8e15` / `03aa24b56404b45a190a2cfc0c7cc100`, `Crest::Crest.UnderwaterRenderer`, `Crest5_WaveSpectrum`, or `Crest5_FoamSettings` hits remain under active `Assets/_Project`.
- Broad serialized exact scan: no active Crest5/WaveHarmonic/direct UnderwaterRenderer/bare Crest assembly hits remain under `ProjectSettings`, `Packages`, or `Assets` outside `Assets/Crest` and the Crest bridge.
- Assembly sidecar exact scans: no non-bridge active `.asmdef` or `.asmref` references to Crest assembly names or Crest asmdef GUIDs `5b35af79ebbe89647a157055d52c59d3` / `59cd48da98d9e4a80917b613abe9416e`.
- Archived asset GUID exact scan: no active references under `Assets`, `ProjectSettings`, or `Packages` to `ed12880d16f3f2f4e80ceee64594101d`, `149ebcba5c729ad49911b1ea4b8456fd`, `0ef7bde4d259c9d4abcc93f41b0903a0`, or `a73ab923bdc811242bdca5f288eb3877`.
- Auto-reference exact check: active Crest donor runtime/editor asmdefs and Crest bridge runtime/editor asmdefs all retain `autoReferenced=false`.
- Scripting define exact check: `CREST_OCEAN` and `CREST_URP` appear in Standalone PlayerSettings and active Crest donor code; no first-party non-bridge `.cs`, `.asmdef`, `.asmref`, or `.rsp` file uses those symbols.
- Donor optional reference exact check: no active `Unity.RenderPipelines.HighDefinition.Runtime` or `Unity.Postprocessing.Runtime` reference remains in `Assets/Crest/Crest/Scripts/Crest.asmdef`.
- Generated report exact check: no active `Assets/profilermarkers.csv(.meta)` remains; archived `Docs/Archive/Crest_Version_Quarantine/Assets/profilermarkers.csv` retains the stale Crest rows for forensic trace only.
- Exact shader scan: Crest HLSL include hits exist only under `Assets/_Project/Scripts/Plugins/Crest/Shaders/`.
- Exact scene/build scan: no active `03_HECTON_WORLD_CREST5` hits remain under `ProjectSettings` or `Assets/_Project/Scenes`.
- asmdef JSON parse check: passed for touched asmdefs.
- Unity/dotnet rebuild check: skipped by explicit build gate because active `csc` and `dotnet` processes were present during final verification.
- dotnet/Unity compile: skipped because the latest gate found active `dotnet`/`csc` processes and CPU sampled at 88; build gate forbids `dotnet`/`csc` under load.

## Loop 22/23 Addendum: Generated Project And Payload Wall

Generated project quarantine:

- Root `WaveHarmonic.Crest*.csproj` and `WaveHarmonic.Crest*.csproj.lscache` files are archived under `Docs/Archive/Crest_Version_Quarantine/GeneratedProject/`.
- Broad root generated first-party `.csproj` files no longer carry direct `Crest.csproj`, `Crest.Helpers.Editor.csproj`, `WaveHarmonic.Crest*.csproj`, or `Packages/com.waveharmonic.crest` routes.
- `Directory.Build.targets` no longer injects `Crest` or `WaveHarmonic.Crest*` references into `Hecton8.Core`; only the missing-package prune target remains.
- `Tools/Crest_Dependency_Scanner.py` scans `.csproj`, `.lscache`, `.sln`, `.slnx`, `.props`, `.targets`, and `.rsp`.
- Target: hard generated-project Crest routes outside donor/helper boundaries.
- Current report keeps generated-project `CREST_OCEAN` / `CREST_URP` symbols as evidence only.
- Counts: `generated_project_scripting_define_hit_count=67`, `generated_project_prune_rule_hit_count=6`.

Shader and stale-payload quarantine:

- `Assets/_Project/Art/Shaders/Hecton_DryVolumeRestore.shader` samples `_OceanCameraColorTexture`, not `_Crest_CameraColorTexture`.
- `Assets/_Project/Scripts/Visor/HectonDryVolumeFeature.cs` reads `IOceanVisualBridge.CameraColorTextureId` and republishes the source texture to the vendor-neutral global for the shared shader pass.
- `Assets/profilermarkers.tvc(.meta)`, active `Assets/_Project/Data/CrestMigration/Crest4SettingsDump.json(.meta)`, and `Assets/_Project/Data/CrestMigration.meta` are archived outside Unity visibility.
- The active `Assets/_Project/Data/CrestMigration/` folder has been removed after verifying it was empty.
- The dependency scanner hard-fails non-bridge `_Crest_*` shader globals, active `profilermarkers.*`, active CrestMigration payloads, and root WaveHarmonic generated-project/lscache files. The polish audit gates all of these walls.

Latest proof after Loop 23:

- `git diff --check` for Loop 22/23 touched files: passed.
- Report parse: `Docs/Reports/ARCHITECTURE_OPTIMIZATION_REPORT.json`, `Docs/Reports/CREST_QUARANTINE_POLISH_AUDIT.json`, `Docs/Reports/CREST_QUARANTINE_REPORT.json`, and `Docs/Reports/SHINOBU_260_SELF_AUDIT.xml` parse successfully.
- `python -m py_compile Tools/Crest_Dependency_Scanner.py Tools/Crest_Quarantine_Polish_Audit.py`: passed.
- `python Tools/Crest_Dependency_Scanner.py`: passed with `breach_count=0`, `allowed_hit_count=40`, `global_scripting_define_hit_count=1`, `generated_project_scripting_define_hit_count=67`, `generated_project_prune_rule_hit_count=6`, `compliance_denylist_hit_count=6`, `vocabulary_debt_hit_count=111`.
- `python Tools/Crest_Quarantine_Polish_Audit.py`: passed with `failed_count=0`.
- Full `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` was executed after Loop 22 generated-project cleanup when the gate opened: 0 errors, 171 warnings, elapsed 00:02:20.78.
- Second build after Loop 23 C# render-pass/shader patch was not launched.
- Gate state: active `VBCSCompiler`; CPU sampled at `89.8%`.
- Status: gated compile proof gap, not hidden success.

## Loop 24 Addendum: No-Python Broad Lscache Closure

User instruction for this loop: do not launch `.py` scripts because they are deadlocking the PC. Verification is therefore PowerShell/rg/static-text only.

Additional quarantine:

- Active SHINOBU_260 status/rationale/log files were restored from `Docs/Archive/Batch011/` because the active files were absent and Git-marked deleted.
- 10 broad root C# Dev Kit cache files with stale WaveHarmonic/CrestMigration routes were moved to `Docs/Archive/Crest_Version_Quarantine/GeneratedProject/`:
  - `Assembly-CSharp.csproj.lscache`
  - `Assembly-CSharp-Editor.csproj.lscache`
  - `Assembly-CSharp-Editor-firstpass.csproj.lscache`
  - `Assembly-CSharp-firstpass.csproj.lscache`
  - `Hecton8.Core.csproj.lscache`
  - `Hecton8.Editor.csproj.lscache`
  - `Unity.RenderPipelines.Core.Editor.csproj.lscache`
  - `Unity.RenderPipelines.Universal.Editor.csproj.lscache`
  - `Unity.RenderPipelines.Universal.Runtime.csproj.lscache`
  - `Unity.ShaderGraph.Editor.csproj.lscache`
- The archive now contains 17 `.csproj.lscache` files for Crest quarantine: the prior 7 WaveHarmonic-named files plus the 10 broad stale cache files.
- `Tools/Crest_Dependency_Scanner.py` now includes `generated_project_stale_lscache_crest_route` to fail broad root lscache files that retain stale `WaveHarmonic.Crest`, `Packages/com.waveharmonic.crest`, or `CrestMigration` text.
- `Tools/Crest_Quarantine_Polish_Audit.py` now includes `stale_broad_csharp_devkit_lscache_no_waveharmonic_crest` and `dependency_scanner_blocks_broad_stale_lscache_crest_routes`.

No-Python proof:

- PowerShell `Select-String` over remaining root `*.csproj.lscache`: `NO_ROOT_STALE_LSCACHE_HITS`.
- `rg` source scan confirms the new scanner/audit gate names.
- `git diff --check -- Tools/Crest_Dependency_Scanner.py Tools/Crest_Quarantine_Polish_Audit.py`: passed with CRLF warnings only.

## Loop 25 Addendum: No-Python Side-Audit Integration

- `Hecton8.Core.csproj` no longer compiles `Assets/_Project/Scripts/Plugins/Crest/CrestDepthCacheDebugger.cs` or `CrestFoamDebugger.cs`; PowerShell reports `NO_BROAD_CSPROJ_BRIDGE_SOURCE_HITS`.
- `Tools/test_memory_budget_check.py` no longer points at `Packages/com.waveharmonic.crest`; the HDR parser fixture is active `Assets/ScifiFacility/Textures/sky_hdr.hdr`.
- `Tools/Crest_Dependency_Scanner.py` now includes `generated_project_bridge_source_in_broad_project`; `Tools/Crest_Quarantine_Polish_Audit.py` gates the same condition through `generated_first_party_projects_do_not_compile_bridge_sources` and `dependency_scanner_blocks_bridge_source_in_broad_project`.
- `Ocean_Crest.prefab`, `Assets/_Project/Data/Ocean/*.asset`, and `Assets/_Project/crest/*.asset` keep selected Crest4 donor bindings. They are active donor route evidence and have baseline backups; this loop does not raw-edit selected donor prefab/settings YAML.
- `ARCHITECTURE_OPTIMIZATION_REPORT.json` and `CREST_QUARANTINE_POLISH_AUDIT.json` were not regenerated because `.py` execution was forbidden. Source gates updated; report regeneration waits for Python deadlock clearance.
