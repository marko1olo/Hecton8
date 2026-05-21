#!/usr/bin/env python3
"""Static polish audit for SHINOBU_260 Crest quarantine.

This does not compile Unity code. It verifies the quarantine-specific guardrails
that can be proven from source text while the CPU/rebuild gate is closed.
"""

from __future__ import annotations

import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
REPORT_PATH = ROOT / "Docs" / "Reports" / "CREST_QUARANTINE_POLISH_AUDIT.json"


def read_text(relative_path: str) -> str:
    return (ROOT / relative_path).read_text(encoding="utf-8")


def add_check(checks: list[dict[str, object]], name: str, passed: bool, detail: str) -> None:
    checks.append(
        {
            "name": name,
            "status": "PASS" if passed else "FAIL",
            "detail": detail,
        }
    )


def main() -> int:
    checks: list[dict[str, object]] = []

    package_path = ROOT / "Packages" / "com.waveharmonic.crest"
    quarantine_path = ROOT / "Docs" / "Archive" / "Crest_Version_Quarantine" / "Packages" / "com.waveharmonic.crest"
    add_check(
        checks,
        "crest5_package_outside_unity_visibility",
        not package_path.exists() and quarantine_path.exists(),
        "Packages/com.waveharmonic.crest absent; archived package exists under Docs/Archive.",
    )
    add_check(
        checks,
        "crest_debuggers_owned_by_bridge_folder",
        not (ROOT / "Assets/_Project/Scripts/World/CrestFoamDebugger.cs").exists()
        and not (ROOT / "Assets/_Project/Scripts/World/CrestFoamDebugger.cs.meta").exists()
        and not (ROOT / "Assets/_Project/Scripts/World/CrestDepthCacheDebugger.cs").exists()
        and not (ROOT / "Assets/_Project/Scripts/World/CrestDepthCacheDebugger.cs.meta").exists()
        and (ROOT / "Assets/_Project/Scripts/Plugins/Crest/CrestFoamDebugger.cs").exists()
        and (ROOT / "Assets/_Project/Scripts/Plugins/Crest/CrestFoamDebugger.cs.meta").exists()
        and (ROOT / "Assets/_Project/Scripts/Plugins/Crest/CrestDepthCacheDebugger.cs").exists()
        and (ROOT / "Assets/_Project/Scripts/Plugins/Crest/CrestDepthCacheDebugger.cs.meta").exists(),
        "Crest-specific forensic MonoBehaviours and metas live under the Crest bridge folder, not World.",
    )

    core_urp_guard = read_text("Assets/_Project/Scripts/Core/HectonUrpTextureRequirementsGuard.cs")
    core_registry_contracts = read_text("Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs")
    add_check(
        checks,
        "core_no_crest_named_diagnostic_strings",
        "Crest parity path" not in core_urp_guard
        and "Crest-adapter singletons" not in core_registry_contracts,
        "Core diagnostic text names the generic ocean route instead of the Crest donor implementation.",
    )

    ocean_prefab = read_text("Assets/_Project/Prefabs/Ocean_Crest.prefab")
    player_prefab = read_text("Assets/_Project/Prefabs/Player.prefab")
    add_check(
        checks,
        "crest5_prefab_adapter_reference_removed",
        "Crest5KinematicsAdapter" not in ocean_prefab
        and "51fcb9de0aa92b842be404fec8bf21d4" not in ocean_prefab
        and "4153056372701123456" not in ocean_prefab,
        "Ocean_Crest prefab no longer carries the quarantined Crest5 adapter component, script GUID, or fileID.",
    )
    add_check(
        checks,
        "player_prefab_has_no_direct_underwater_renderer",
        "Crest::Crest.UnderwaterRenderer" not in player_prefab
        and "1b0c0a69611596146aceb2f60532940c" not in player_prefab
        and "9079297290110143596" not in player_prefab,
        "Player prefab no longer owns an active Crest.UnderwaterRenderer component; underwater pass ownership stays behind the bridge command path.",
    )

    active_crest5_assets_absent = not (ROOT / "Assets/_Project/Data/CrestMigration/Crest5_WaveSpectrum.asset").exists() and not (ROOT / "Assets/_Project/Data/CrestMigration/Crest5_WaveSpectrum.asset.meta").exists() and not (ROOT / "Assets/_Project/Data/CrestMigration/Crest5_FoamSettings.asset").exists() and not (ROOT / "Assets/_Project/Data/CrestMigration/Crest5_FoamSettings.asset.meta").exists()
    archived_crest5_assets_present = (ROOT / "Docs/Archive/Crest_Version_Quarantine/Assets/_Project/Data/CrestMigration/Crest5_WaveSpectrum.asset").exists() and (ROOT / "Docs/Archive/Crest_Version_Quarantine/Assets/_Project/Data/CrestMigration/Crest5_WaveSpectrum.asset.meta").exists() and (ROOT / "Docs/Archive/Crest_Version_Quarantine/Assets/_Project/Data/CrestMigration/Crest5_FoamSettings.asset").exists() and (ROOT / "Docs/Archive/Crest_Version_Quarantine/Assets/_Project/Data/CrestMigration/Crest5_FoamSettings.asset.meta").exists()
    add_check(
        checks,
        "crest5_migration_assets_outside_unity_visibility",
        active_crest5_assets_absent and archived_crest5_assets_present,
        "Crest5 WaveHarmonic serialized settings assets and metas are archived under Docs/Archive, not active Assets/_Project/Data.",
    )

    old_wave_shader_path = ROOT / "Assets/_Project/Art/Shaders/Crest_SargassumWaveDamping.shader"
    old_foam_shader_path = ROOT / "Assets/_Project/Art/Shaders/Crest_SargassumFoamDamping.shader"
    bridge_wave_shader_path = "Assets/_Project/Scripts/Plugins/Crest/Shaders/Crest_SargassumWaveDamping.shader"
    bridge_foam_shader_path = "Assets/_Project/Scripts/Plugins/Crest/Shaders/Crest_SargassumFoamDamping.shader"
    bridge_oil_shader_path = "Assets/_Project/Scripts/Plugins/Crest/Shaders/Crest_SargassumOilFilm.shader"
    bridge_wave_shader = read_text(bridge_wave_shader_path)
    add_check(
        checks,
        "crest_input_shaders_owned_by_bridge_folder",
        not old_wave_shader_path.exists()
        and not (ROOT / "Assets/_Project/Art/Shaders/Crest_SargassumWaveDamping.shader.meta").exists()
        and not old_foam_shader_path.exists()
        and not (ROOT / "Assets/_Project/Art/Shaders/Crest_SargassumFoamDamping.shader.meta").exists()
        and not (ROOT / "Assets/_Project/Art/Shaders/Crest_SargassumOilFilm.shader").exists()
        and not (ROOT / "Assets/_Project/Art/Shaders/Crest_SargassumOilFilm.shader.meta").exists()
        and (ROOT / bridge_wave_shader_path).exists()
        and (ROOT / f"{bridge_wave_shader_path}.meta").exists()
        and (ROOT / bridge_foam_shader_path).exists()
        and (ROOT / f"{bridge_foam_shader_path}.meta").exists()
        and (ROOT / bridge_oil_shader_path).exists()
        and (ROOT / f"{bridge_oil_shader_path}.meta").exists()
        and "../../../../../Crest/Crest/Shaders/OceanGlobals.hlsl" in bridge_wave_shader,
        "Crest-specific sargassum input shaders and metas live under the Crest bridge folder; direct Crest HLSL includes are no longer in shared Art/Shaders.",
    )
    add_check(
        checks,
        "crest5_scene_outside_unity_visibility",
        not (ROOT / "Assets/_Project/Scenes/03_HECTON_WORLD_CREST5.unity").exists()
        and not (ROOT / "Assets/_Project/Scenes/03_HECTON_WORLD_CREST5.unity.meta").exists()
        and (ROOT / "Docs/Archive/Crest_Version_Quarantine/Assets/_Project/Scenes/03_HECTON_WORLD_CREST5.unity").exists()
        and (ROOT / "Docs/Archive/Crest_Version_Quarantine/Assets/_Project/Scenes/03_HECTON_WORLD_CREST5.unity.meta").exists(),
        "Binary Crest5 sandbox scene and meta are archived outside Unity visibility.",
    )
    es3_defaults = read_text("Assets/Plugins/Easy Save 3/Resources/ES3/ES3Defaults.asset")
    init_test_scene_texts = [
        path.read_text(encoding="utf-8", errors="replace")
        for path in sorted((ROOT / "Assets").glob("InitTestScene*.unity"))
    ]
    add_check(
        checks,
        "easy_save_defaults_no_crest_assemblies",
        "\n    - Crest\n" not in es3_defaults and "WaveHarmonic.Crest" not in es3_defaults,
        "Easy Save global assembly scan defaults no longer list Crest or WaveHarmonic assemblies outside the bridge.",
    )
    add_check(
        checks,
        "root_init_test_scenes_no_waveharmonic_crest",
        len(init_test_scene_texts) > 0 and not any("WaveHarmonic.Crest" in text for text in init_test_scene_texts),
        "Root InitTestScene YAML files no longer ask Unity TestRunner to load WaveHarmonic Crest assemblies.",
    )

    visual_bridge_contract = read_text("Assets/_Project/Scripts/Core/IOceanVisualBridge.cs")
    old_visual_bridge_terms = (
        "HasUnderwaterRenderer",
        "TryGetUnderwaterRenderer",
        "EnsureUnderwaterRenderer",
        "IsUnderwaterRendererEnabled",
        "SetUnderwaterRendererEnabled",
        "IsUnderwaterRendererActive",
        "CopyUnderwaterRendererSettings",
    )
    add_check(
        checks,
        "visual_bridge_contract_vendor_neutral",
        not any(term in visual_bridge_contract for term in old_visual_bridge_terms)
        and "CameraColorTextureId" in visual_bridge_contract
        and "HasUnderwaterPass" in visual_bridge_contract
        and "TryGetUnderwaterPass" in visual_bridge_contract
        and "EnsureUnderwaterPass" in visual_bridge_contract
        and "CopyUnderwaterPassSettings" in visual_bridge_contract,
        "Core visual bridge no longer exposes Crest-named underwater renderer verbs; vendor texture ID is read through the bridge.",
    )

    add_check(
        checks,
        "ocean_kinematics_base_vendor_neutral",
        not (ROOT / "Assets/_Project/Scripts/HectonCrestOceanKinematics.cs").exists()
        and not (ROOT / "Assets/_Project/Scripts/HectonCrestOceanKinematics.cs.meta").exists()
        and (ROOT / "Assets/_Project/Scripts/HectonOceanKinematicsBridgeBase.cs").exists()
        and (ROOT / "Assets/_Project/Scripts/HectonOceanKinematicsBridgeBase.cs.meta").exists()
        and "HectonOceanKinematicsBridgeBase" in read_text("Assets/_Project/Scripts/HectonOceanKinematicsBridgeBase.cs")
        and "HectonCrestOceanKinematics" not in read_text("Assets/_Project/Scripts/Plugins/Crest/CrestBridge.cs"),
        "Shared first-party ocean kinematics base no longer carries a Crest-specific type or filename.",
    )

    dry_volume_feature = read_text("Assets/_Project/Scripts/Visor/HectonDryVolumeFeature.cs")
    add_check(
        checks,
        "dry_volume_reads_vendor_texture_id_through_bridge",
        "_Crest_CameraColorTexture" not in dry_volume_feature
        and "CrestCameraColorTextureId" not in dry_volume_feature
        and "TryReadOceanCameraColorTexture" in dry_volume_feature
        and "bridge.CameraColorTextureId" in dry_volume_feature,
        "Dry-volume render pass no longer hard-codes the Crest camera color global; it reads the active ocean visual bridge ID.",
    )

    scooter_shafts_feature = read_text("Assets/_Project/Scripts/Visor/HectonScooterVolumetricShaftsFeature.cs")
    dry_volume_stencil_source = read_text("Assets/_Project/Scripts/Visor/HectonDryVolumeStencilSource.cs")
    surface_weather_profile = read_text("Assets/_Project/Scripts/Atmosphere/SurfaceWeatherProfile.cs")
    biome_profile = read_text("Assets/_Project/Scripts/HectonBiomeProfile.cs")
    fluid_engine = read_text("Assets/_Project/Scripts/HectonFluidEngine.cs")
    sargassum_drag = read_text("Assets/_Project/Scripts/World/SargassumGlobalDragManager.cs")
    sargassum_damping = read_text("Assets/_Project/Scripts/World/SargassumCrestDampingController.cs")
    add_check(
        checks,
        "low_risk_non_bridge_text_uses_ocean_vocabulary",
        "keeps Crest water" not in scooter_shafts_feature
        and "after Crest underwater fog" not in dry_volume_stencil_source
        and "Target Crest" not in surface_weather_profile
        and "Crest foam" not in surface_weather_profile
        and "CREST" not in biome_profile
        and "Crest _ScatterColour" not in biome_profile
        and "Crest FFT rendering" not in fluid_engine
        and "Crest damping" not in sargassum_drag
        and "future Crest 5" not in sargassum_damping
        and "texture for Crest 5" not in sargassum_damping,
        "Comments/tooltips in low-risk non-bridge authoring text use ocean donor vocabulary; serialized Player/World ABI names remain documented debt.",
    )

    mock = read_text("Assets/_Project/Scripts/Environment/Fluids/EmergencyMockOceanKinematicsAdapter.cs")
    add_check(
        checks,
        "emergency_mock_value_type",
        "public readonly struct EmergencyMockOceanKinematicsAdapter" in mock
        and "class EmergencyMockOceanKinematicsAdapter" not in mock,
        "Fallback adapter is a readonly struct, not a managed class container.",
    )
    add_check(
        checks,
        "emergency_mock_burst_flags",
        "[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]" in mock,
        "Fallback sampling job keeps the explicit Burst compile flags.",
    )
    add_check(
        checks,
        "emergency_mock_no_hidden_complete",
        ".Complete(" not in mock,
        "Fallback scheduling returns JobHandle and does not force same-frame completion.",
    )

    contracts = read_text("Assets/_Project/Scripts/Environment/Fluids/Contracts/OceanAdapterContracts.cs")
    add_check(
        checks,
        "request_dto_explicit_32_bytes",
        "[StructLayout(LayoutKind.Explicit, Size = 32)]" in contracts
        and "[FieldOffset(0)] public double3 RequestAUP;" in contracts
        and "[FieldOffset(24)] public uint CallerHashID;" in contracts
        and "[FieldOffset(28)] private uint _pad0;" in contracts,
        "OceanSampleRequestDTO offset contract matches the SHINOBU_260 mandate.",
    )
    add_check(
        checks,
        "no_hot_dto_properties",
        "{ get; set; }" not in contracts and "{ get; private set; }" not in contracts,
        "Strict ocean DTO contract exposes raw fields, not hot-path properties.",
    )

    vault = read_text("Assets/_Project/Scripts/Environment/Fluids/OceanAdapterVaultRoute.cs")
    old_shared_ocean_lanes = (
        "BufferID.ShinobuOceanWaveReadbackQueries",
        "BufferID.ShinobuOceanWaveReadbackResults",
        "BufferID.ShinobuOceanTelemetryRing",
        "BufferID.ShinobuOceanBeaufortProfiles",
        "BufferID.ShinobuOceanLodState",
        "BufferID.ShinobuOceanCsvScratch",
    )
    add_check(
        checks,
        "vault_lane_ids_do_not_reuse_atmosphere_ocean_ids",
        not any(token in vault for token in old_shared_ocean_lanes)
        and "RequestBufferID = (BufferID)72960" in vault
        and "CsvScratchBufferID = (BufferID)72965" in vault,
        "SHINOBU_260 owns local numeric Vault IDs 72960..72965 instead of reusing Atmosphere ShinobuOcean lanes.",
    )
    add_check(
        checks,
        "vault_lane_no_iscreated_property",
        "IsCreated =>" not in vault and "bool IsCreated" not in vault,
        "Vault lane binding uses static validation helpers rather than struct properties.",
    )
    add_check(
        checks,
        "vault_uninitialized_lanes",
        vault.count("NativeArrayOptions.UninitializedMemory") >= 6,
        "Request, result, telemetry, profile, global water, and CSV lanes request uninitialized memory.",
    )

    csv = read_text("Assets/_Project/Scripts/Environment/Fluids/OceanPerformanceProfileCsv.cs")
    add_check(
        checks,
        "csv_header_no_string_helper",
        "StartsWithAscii" not in csv and "string text" not in csv and "StartsWithProfileHeader" in csv,
        "CSV profile header detection is byte-only and does not carry a managed string helper.",
    )

    gizmo = read_text("Assets/_Project/Scripts/Plugins/Crest/Editor/CrestAupSamplingGizmo.cs")
    add_check(
        checks,
        "gizmo_aup_localizes_before_vector3",
        "runtimeOriginAUP = HectonFloatingOrigin.CurrentTotalOffsetDouble" in gizmo
        and "localAUP = aup - runtimeOriginAUP" in gizmo
        and "new Vector3((float)localAUP.x" in gizmo
        and "new Vector3((float)aup.x" not in gizmo,
        "Editor AUP x-ray subtracts the floating origin before casting to Vector3.",
    )
    add_check(
        checks,
        "gizmo_vault_read_editor_only",
        "#if UNITY_EDITOR" in gizmo and "GlobalRegistry.DataVault" in gizmo and "GlobalDataVault.TryGetLatestCreated" not in gizmo,
        "Editor gizmo reads the cold diagnostic DataVault route and does not use latest-created fallback.",
    )

    asmdef = read_text("Assets/_Project/Scripts/Plugins/Crest/Hecton8.Crest.Bridge.asmdef")
    add_check(
        checks,
        "bridge_asmdef_not_auto_referenced",
        '"autoReferenced": false' in asmdef,
        "Runtime Crest bridge stays opt-in and cannot leak into unrelated assemblies by auto-reference.",
    )

    bridge = read_text("Assets/_Project/Scripts/Plugins/Crest/CrestBridge.cs")
    has_underwater_start = bridge.find("public bool HasUnderwaterPass")
    try_underwater_start = bridge.find("public Component TryGetUnderwaterPass", has_underwater_start)
    ensure_underwater_start = bridge.find("public Component EnsureUnderwaterPass", try_underwater_start)
    underwater_read_body = bridge[has_underwater_start:ensure_underwater_start] if has_underwater_start >= 0 and ensure_underwater_start > has_underwater_start else ""
    add_check(
        checks,
        "base_bridge_no_ocean_singleton_polling",
        ".Instance" not in bridge
        and "protected virtual Crest.OceanRenderer ReadBoundOceanRenderer()" in bridge
        and "ReadBoundOceanRenderer()" in bridge,
        "Base Crest visual bridge reads only cold-bound renderer/cache hooks and does not poll Crest singletons.",
    )
    add_check(
        checks,
        "base_bridge_underwater_reads_are_cache_only",
        "GetComponent<Crest.UnderwaterRenderer>" not in underwater_read_body
        and "IsCachedUnderwaterRendererForCamera" in underwater_read_body
        and "Crest.UnderwaterRenderer.Instance" not in bridge,
        "Underwater Has/Try bridge reads use cached component identity; component lookup remains in Ensure command path only.",
    )

    depth_cache_bootstrap = read_text("Assets/_Project/Scripts/Plugins/Crest/HectonCrestOceanDepthCacheBootstrap.cs")
    add_check(
        checks,
        "depth_cache_bootstrap_no_ocean_singleton_fallback",
        "Crest.OceanRenderer.Instance" not in depth_cache_bootstrap,
        "Depth-cache bootstrap uses its serialized/local Crest binding and does not recover through the Crest global singleton.",
    )

    underwater_visuals = read_text("Assets/_Project/Scripts/HectonUnderwaterVisuals.cs")
    add_check(
        checks,
        "underwater_visuals_no_crest_reflection_fallback",
        '"Crest.OceanRenderer"' not in underwater_visuals
        and '"Crest.UnderwaterRenderer"' not in underwater_visuals
        and "ResolveEditorOceanMaterialFallback" not in underwater_visuals
        and "ResolveEditorUnderwaterRendererFallback" not in underwater_visuals,
        "HectonUnderwaterVisuals routes Crest visual access through IOceanVisualBridge instead of string reflection fallbacks.",
    )
    add_check(
        checks,
        "underwater_visuals_vendor_neutral_pass_vocabulary",
        "UnderwaterRenderer" not in underwater_visuals
        and "crestSkyBaseFogLink" in underwater_visuals
        and "[FormerlySerializedAs(\"crestSkyBaseFogLink\")]" in underwater_visuals,
        "Presentation code uses underwater pass vocabulary; the only retained Crest sky field name is serialized ABI migration metadata.",
    )

    runtime_adapter = read_text("Assets/_Project/Scripts/Plugins/Crest/CrestOceanRuntimeAdapter.cs")
    schedule_start = runtime_adapter.find("public JobHandle ScheduleWaveHeightRequests")
    schedule_end = runtime_adapter.find("public bool TryReadGlobalWaterLevel", schedule_start)
    schedule_body = runtime_adapter[schedule_start:schedule_end] if schedule_start >= 0 and schedule_end > schedule_start else ""
    add_check(
        checks,
        "runtime_adapter_no_hot_component_lookup",
        "TryGetComponent" not in schedule_body and "ResolveOceanRenderer" not in runtime_adapter,
        "ScheduleWaveHeightRequests uses cold cached binding/fallback AUP and does not repair dependencies in the hot submission path.",
    )
    add_check(
        checks,
        "runtime_adapter_no_transform_root_aup_reconstruction",
        "renderer.Root.position" not in runtime_adapter and "activeOriginAUP + new double3" not in runtime_adapter,
        "Runtime adapter no longer reconstructs AUP authority from Transform.position.",
    )

    legacy_adapter = read_text("Assets/_Project/Scripts/Plugins/Crest/Crest4KinematicsAdapter.cs")
    resolve_start = legacy_adapter.find("private Crest.OceanRenderer ResolveOceanRenderer")
    resolve_end = legacy_adapter.find("private static double3 ResolveOceanRootAUP", resolve_start)
    resolve_body = legacy_adapter[resolve_start:resolve_end] if resolve_start >= 0 and resolve_end > resolve_start else ""
    tuning_start = legacy_adapter.find("public bool TryBuildBurstTuning(float simulationTimeSeconds")
    tuning_end = legacy_adapter.find("        /// <summary>\n        /// Publishes", tuning_start)
    tuning_body = legacy_adapter[tuning_start:tuning_end] if tuning_start >= 0 and tuning_end > tuning_start else ""
    availability_start = legacy_adapter.find("public override bool IsAvailable")
    weather_start = legacy_adapter.find("public override bool TryGetSurfaceWeatherState", availability_start)
    availability_body = legacy_adapter[availability_start:weather_start] if availability_start >= 0 and weather_start > availability_start else ""
    weather_end = legacy_adapter.find("public override bool ApplySurfaceWeatherState", weather_start)
    weather_body = legacy_adapter[weather_start:weather_end] if weather_start >= 0 and weather_end > weather_start else ""
    collision_start = legacy_adapter.find("private bool TryReadCollisionProvider")
    collision_end = legacy_adapter.find("private void TryResolveLocalOceanRendererBinding", collision_start)
    collision_body = legacy_adapter[collision_start:collision_end] if collision_start >= 0 and collision_end > collision_start else ""
    add_check(
        checks,
        "legacy_crest4_adapter_no_hot_component_repair",
        "TryResolveLocalOceanRendererBinding" not in resolve_body
        and "TryGetComponent" not in resolve_body
        and "Debug.Log" not in resolve_body
        and "TryReadBoundOceanRenderer" in availability_body
        and "ResolveSeaLevel(TryReadBoundOceanRenderer())" in availability_body
        and "ResolveOceanRenderer()" not in availability_body,
        "Legacy Crest4 adapter no longer repairs component bindings from read accessors or ResolveOceanRenderer; binding discovery stays in Awake.",
    )
    add_check(
        checks,
        "legacy_crest4_read_accessors_do_not_log_or_poll_registry",
        "ResolveOceanRenderer()" not in weather_body
        and "Debug.Log" not in weather_body
        and "GlobalRegistry." not in availability_body
        and "Debug.Log" not in collision_body
        and "ResolveOceanRenderer()" not in collision_body
        and "GlobalRegistry.Fluid" not in legacy_adapter
        and "TryReadCollisionProvider" in legacy_adapter,
        "Legacy weather/sea-level/collision read paths use cached fields and do not log, poll GlobalRegistry, or repair bindings.",
    )
    add_check(
        checks,
        "legacy_crest4_tuning_is_cached_read_only",
        "TryReadBoundOceanRenderer()" in tuning_body
        and "ResolveOceanRenderer()" not in tuning_body
        and "GlobalRegistry." not in tuning_body
        and "Debug.Log" not in tuning_body
        and "ResolveSeaLevel(oceanRenderer)" in tuning_body,
        "TryBuildBurstTuning reads the cached Crest owner and does not route through logging resolver or registry fallback.",
    )

    editor_asmdef = read_text("Assets/_Project/Scripts/Plugins/Crest/Editor/Hecton8.Crest.Bridge.Editor.asmdef")
    add_check(
        checks,
        "editor_bridge_no_easysave3_reference",
        "EasySave3" not in editor_asmdef,
        "Crest bridge editor assembly no longer references the forbidden EasySave3 assembly.",
    )

    dependency_report_path = ROOT / "Docs" / "Reports" / "ARCHITECTURE_OPTIMIZATION_REPORT.json"
    dependency_report = {}
    if dependency_report_path.exists():
        dependency_report = json.loads(dependency_report_path.read_text(encoding="utf-8"))
    add_check(
        checks,
        "dependency_scanner_last_report_zero_breaches",
        dependency_report.get("breach_count") == 0,
        "Last Crest dependency scanner report has breach_count=0.",
    )
    add_check(
        checks,
        "dependency_scanner_tracks_vocabulary_debt",
        isinstance(dependency_report.get("vocabulary_debt_hit_count"), int)
        and isinstance(dependency_report.get("vocabulary_debt_hits"), list),
        "Dependency scanner reports non-failing Crest vocabulary debt separately from asmdef/direct-reference breaches.",
    )
    dependency_scanner_source = read_text("Tools/Crest_Dependency_Scanner.py")
    add_check(
        checks,
        "dependency_scanner_uses_rg_for_broad_serialized_surface",
        "rg" in dependency_scanner_source
        and "--json" in dependency_scanner_source
        and "scan_active_assets_with_python" in dependency_scanner_source
        and "handle.read(MAX_ACTIVE_ASSET_SCAN_BYTES)" in dependency_scanner_source
        and "active_crest5_package_visible" in dependency_scanner_source,
        "Dependency scanner uses ripgrep for the widened active serialized surface, keeps bounded Python fallback, and hard-fails visible Crest5 package reactivation.",
    )

    failed = [check for check in checks if check["status"] != "PASS"]
    payload = {
        "agent_id": "SHINOBU_260",
        "domain": "CREST_VERSION_QUARANTINE_DIRECTOR",
        "evidence_class": "STATIC_SOURCE_ONLY_NO_REBUILD",
        "status": "PASS" if not failed else "FAIL",
        "failed_count": len(failed),
        "checks": checks,
    }

    REPORT_PATH.parent.mkdir(parents=True, exist_ok=True)
    REPORT_PATH.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(json.dumps(payload, indent=2, sort_keys=True))
    return 0 if not failed else 1


if __name__ == "__main__":
    raise SystemExit(main())
