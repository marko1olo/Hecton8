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

Static proof: `Docs/Reports/ARCHITECTURE_OPTIMIZATION_REPORT.json` reports `breach_count=0`, `allowed_hit_count=40`, `reflection_string_hit_count=0`, and non-failing `vocabulary_debt_hit_count=111`. The scanner now covers active serialized text in `Assets`, `ProjectSettings`, and `Packages` for Crest5/WaveHarmonic/direct UnderwaterRenderer/bare Crest assembly-list breaches, active `Packages/com.waveharmonic.crest` visibility, shader/HLSL/compute Crest includes outside the bridge, Unity `.asmref` sidecars, Unity `GUID:<asmdef-guid>` references to the active Crest 4 asmdefs, and active backreferences to archived Crest5/recovery asset GUIDs.

The scanner also fails if active Crest donor asmdefs or Crest bridge asmdefs become auto-referenced. This keeps Crest opt-in at the assembly importer level, not only at the direct-reference level.

Scanner throughput proof: after broadening the serialized surface, `scan_active_assets` was moved to `rg --json` with a Python fallback. Full scanner wall time on this workspace dropped from about 262 seconds to about 35.5 seconds while preserving `breach_count=0`.

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

`CrestOceanRuntimeAdapter` lives only inside `Hecton8.Crest.Bridge`. It accepts `NativeArray<OceanSampleRequestDTO>`, returns a `JobHandle`, subtracts ocean-root AUP in `double3`, casts only local deltas to `float3`, and marks output as delayed by 1-3 frames. It does not call `JobHandle.Complete()`. The hot submission path does not call `TryGetComponent`, repair bindings, or reconstruct AUP authority from `Transform.position`.

`EmergencyMockOceanKinematicsAdapter.GenerateEmergencyMockOceanAdapter()` bypasses Crest entirely and produces deterministic sine-wave results for profiling when Crest is broken. It is a value type, not a managed fallback object.

Legacy `Crest4KinematicsAdapter` remains present for old `Hecton8.Physics` consumers. It is not the strict forward route, but its binding repair was fenced: `ResolveOceanRenderer()` no longer calls `TryGetComponent` or logs, `TryBuildBurstTuning` uses the cached renderer, `TryGetSurfaceWeatherState`/flow/collision reads use cached binding, and `SeaLevel` does not fall back through `GlobalRegistry`.

Base `CrestBridge` no longer polls `Crest.OceanRenderer.Instance` or `Crest.UnderwaterRenderer.Instance`; visual material/camera helpers read only the renderer supplied by a concrete bridge adapter, and underwater Has/Try helpers read a cache populated by the command path.

`IOceanVisualBridge` exposes vendor-neutral underwater pass verbs and `CameraColorTextureId`. Non-bridge render code must not hard-code `_Crest_CameraColorTexture`; `HectonDryVolumeFeature` reads the active bridge's texture ID before scheduling dry-volume restore.

`HectonUnderwaterVisuals` no longer contains `"Crest.OceanRenderer"` or `"Crest.UnderwaterRenderer"` reflection fallbacks. It consumes the bridge through `IOceanVisualBridge` only. The old serialized field name `crestSkyBaseFogLink` is preserved only as `[FormerlySerializedAs("crestSkyBaseFogLink")]` migration metadata for `oceanSkyBaseFogLink`.

The shared first-party base formerly named `HectonCrestOceanKinematics` is now `HectonOceanKinematicsBridgeBase` with the same `.meta` GUID. This removes a Crest-specific first-party type name without remapping serialized scene objects because the class is an abstract base, not an attached component.

`HectonCrestOceanDepthCacheBootstrap` no longer falls back to `Crest.OceanRenderer.Instance`. It still belongs to World/depth-cache integration for broader lifecycle ownership, but the Crest singleton recovery path is removed from this quarantine bridge.

`Ocean_Crest.prefab` no longer carries the quarantined Crest5 adapter MonoBehaviour. Exact scans found no remaining `Crest5KinematicsAdapter`, script GUID `51fcb9de0aa92b842be404fec8bf21d4`, or component fileID `4153056372701123456` in active prefabs/scenes/assets. This prefab format does not contain `m_RootGameObject`; raw-YAML proof is therefore the root GameObject component-list removal plus exact GUID/fileID absence.

`Player.prefab` no longer carries a direct `Crest::Crest.UnderwaterRenderer` MonoBehaviour. Exact scan found no remaining component fileID `9079297290110143596`, script GUID `1b0c0a69611596146aceb2f60532940c`, or `Crest::Crest.UnderwaterRenderer` class identifier in the prefab. Underwater pass ownership stays behind the bridge command path.

Crest-specific sargassum input shaders now live under `Assets/_Project/Scripts/Plugins/Crest/Shaders/` with original metas preserved:

- `Crest_SargassumWaveDamping.shader(.meta)`
- `Crest_SargassumFoamDamping.shader(.meta)`
- `Crest_SargassumOilFilm.shader(.meta)`

Exact shader scan reports only bridge-owned Crest shader/HLSL references. Shared `Assets/_Project/Art/Shaders` no longer owns direct Crest HLSL include paths.

`Assets/Plugins/Easy Save 3/Resources/ES3/ES3Defaults.asset` no longer lists `Crest` or `WaveHarmonic.Crest*` in global serializer assembly defaults. Root `Assets/InitTestScene*.unity` files no longer list `WaveHarmonic.Crest*` in TestRunner `m_AssembliesWithTests`.

`Assets/_Recovery` no longer exists under active Unity visibility. It was moved with its `.meta` to `Docs/Archive/Crest_Version_Quarantine/Assets/_Recovery` after static scan found binary recovery scenes with `Crest::Crest.UnderwaterRenderer` and `Crest5KinematicsAdapter` strings. This is archival containment; the folder is Unity recovery payload, not an authoritative runtime source route.

Known serialized vocabulary debt outside this agent's safe write boundary: `SargassumCrestDampingController` and `HectonPlayerMovement.useCrestOceanHeight` still carry Crest in serialized Player/World names. They do not create a direct Crest assembly reference and should be remapped only by the owning agents with Unity serialization validation.

Loop 12 low-risk text polish removed donor names from non-serialized comments/tooltips in Visor, Atmosphere, Environment, Fluid, and Sargassum authoring code. Remaining vocabulary debt is tracked by `Crest_Dependency_Scanner.py` as non-failing `vocabulary_debt_hits`, not as compile-wall breaches.

Task 12 status: blocked by dependency. Full suppression of Crest `OceanRenderer.OnEnable`/`Start` requires an invasive vendor-source lifecycle patch by a later Crest-internal agent. This pass does not edit donor lifecycle code.

## Static Verification

- `python Tools/Crest_Baseline_Archiver.py --execute`: passed.
- `python Tools/Crest_Dependency_Scanner.py`: passed with `breach_count=0`, `allowed_hit_count=40`, `reflection_string_hit_count=0`, `vocabulary_debt_hit_count=111`.
- `python Tools/Crest_Quarantine_Polish_Audit.py`: passed with `failed_count=0`, including `legacy_crest4_adapter_no_hot_component_repair`, `base_bridge_no_ocean_singleton_polling`, `base_bridge_underwater_reads_are_cache_only`, `depth_cache_bootstrap_no_ocean_singleton_fallback`, `legacy_crest4_read_accessors_do_not_log_or_poll_registry`, `legacy_crest4_tuning_is_cached_read_only`, `player_prefab_has_no_direct_underwater_renderer`, `crest5_migration_assets_outside_unity_visibility`, `crest_input_shaders_owned_by_bridge_folder`, `crest5_scene_outside_unity_visibility`, and `dependency_scanner_covers_asmref_and_crest_guid_references`.
- `Tools/Crest_Quarantine_Polish_Audit.py` also gates `dependency_scanner_blocks_archived_asset_guid_references`, proving the normal scanner will fail active links to archived Crest5/recovery object GUIDs.
- `Tools/Crest_Quarantine_Polish_Audit.py` also gates `crest_donor_asmdefs_not_auto_referenced`, `bridge_asmdef_not_auto_referenced`, and `dependency_scanner_blocks_auto_referenced_crest_assemblies`.
- `Tools/Crest_Quarantine_Polish_Audit.py` also gates `underwater_visuals_no_crest_reflection_fallback`, `underwater_visuals_vendor_neutral_pass_vocabulary`, `visual_bridge_contract_vendor_neutral`, `dry_volume_reads_vendor_texture_id_through_bridge`, `crest5_prefab_adapter_reference_removed`, `ocean_kinematics_base_vendor_neutral`, `low_risk_non_bridge_text_uses_ocean_vocabulary`, and `dependency_scanner_tracks_vocabulary_debt`.
- `python Tools/BufferIDSovereigntyAudit.py --report-path Docs/Reports/SHINOBU_260_BufferIDSovereigntyAudit.md --json-path Docs/Reports/SHINOBU_260_BufferIDSovereigntyAudit.json`: passed as static evidence; global `duplicateValueCount=3` comes from unrelated `H8Memory.cs` values `70534..70536`, while `72960..72965` are local casts only in `OceanAdapterVaultRoute.cs`.
- `python -m py_compile Tools/Crest_Baseline_Archiver.py Tools/Crest_Dependency_Scanner.py Tools/Crest_Quarantine_Polish_Audit.py Tools/BufferIDSovereigntyAudit.py`: passed.
- `git diff --check` for touched Crest bridge, tools, and report files: passed; only Git CRLF conversion warnings were emitted.
- Exact active asset scan: no `WaveHarmonic.Crest`, Crest5 script GUIDs `382a5d8b1147b4e78a31353c022b8e15` / `03aa24b56404b45a190a2cfc0c7cc100`, `Crest::Crest.UnderwaterRenderer`, `Crest5_WaveSpectrum`, or `Crest5_FoamSettings` hits remain under active `Assets/_Project`.
- Broad serialized exact scan: no active Crest5/WaveHarmonic/direct UnderwaterRenderer/bare Crest assembly hits remain under `ProjectSettings`, `Packages`, or `Assets` outside `Assets/Crest` and the Crest bridge.
- Assembly sidecar exact scans: no non-bridge active `.asmdef` or `.asmref` references to Crest assembly names or Crest asmdef GUIDs `5b35af79ebbe89647a157055d52c59d3` / `59cd48da98d9e4a80917b613abe9416e`.
- Archived asset GUID exact scan: no active references under `Assets`, `ProjectSettings`, or `Packages` to `ed12880d16f3f2f4e80ceee64594101d`, `149ebcba5c729ad49911b1ea4b8456fd`, `0ef7bde4d259c9d4abcc93f41b0903a0`, or `a73ab923bdc811242bdca5f288eb3877`.
- Auto-reference exact check: active Crest donor runtime/editor asmdefs and Crest bridge runtime/editor asmdefs all retain `autoReferenced=false`.
- Exact shader scan: Crest HLSL include hits exist only under `Assets/_Project/Scripts/Plugins/Crest/Shaders/`.
- Exact scene/build scan: no active `03_HECTON_WORLD_CREST5` hits remain under `ProjectSettings` or `Assets/_Project/Scenes`.
- asmdef JSON parse check: passed for touched asmdefs.
- dotnet/Unity compile: skipped because the latest gate found active `dotnet`/`csc` processes and CPU sampled at 88; build gate forbids `dotnet`/`csc` under load.
