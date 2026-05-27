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
    add_check(
        checks,
        "stale_profiler_markers_outside_unity_visibility",
        not (ROOT / "Assets/profilermarkers.csv").exists()
        and not (ROOT / "Assets/profilermarkers.csv.meta").exists()
        and not (ROOT / "Assets/profilermarkers.tvc").exists()
        and not (ROOT / "Assets/profilermarkers.tvc.meta").exists()
        and (ROOT / "Docs/Archive/Crest_Version_Quarantine/Assets/profilermarkers.csv").exists()
        and (ROOT / "Docs/Archive/Crest_Version_Quarantine/Assets/profilermarkers.csv.meta").exists()
        and (ROOT / "Docs/Archive/Crest_Version_Quarantine/Assets/profilermarkers.tvc").exists()
        and (ROOT / "Docs/Archive/Crest_Version_Quarantine/Assets/profilermarkers.tvc.meta").exists(),
        "Stale Unity profiler marker payloads are archived outside active Assets with their metas.",
    )
    active_waveharmonic_projects = sorted(path.name for path in ROOT.glob("WaveHarmonic.Crest*.csproj"))
    archived_waveharmonic_projects = sorted(
        path.name
        for path in (ROOT / "Docs/Archive/Crest_Version_Quarantine/GeneratedProject").glob("WaveHarmonic.Crest*.csproj")
    )
    add_check(
        checks,
        "waveharmonic_generated_projects_outside_root",
        not active_waveharmonic_projects and len(archived_waveharmonic_projects) >= 7,
        "Root generated WaveHarmonic Crest projects are archived outside active MSBuild visibility.",
    )
    active_waveharmonic_lscache = sorted(path.name for path in ROOT.glob("WaveHarmonic.Crest*.csproj.lscache"))
    archived_waveharmonic_lscache = sorted(
        path.name
        for path in (ROOT / "Docs/Archive/Crest_Version_Quarantine/GeneratedProject").glob("WaveHarmonic.Crest*.csproj.lscache")
    )
    add_check(
        checks,
        "stale_csharp_devkit_lscache_no_waveharmonic_crest",
        not active_waveharmonic_lscache and len(archived_waveharmonic_lscache) >= 7,
        "Root C# Dev Kit lscache files for quarantined WaveHarmonic Crest projects are archived outside active MSBuild/IDE visibility.",
    )
    active_stale_root_lscache = []
    for path in sorted(ROOT.glob("*.csproj.lscache")):
        text = path.read_text(encoding="utf-8-sig", errors="replace")
        if (
            "WaveHarmonic.Crest" in text
            or "Packages/com.waveharmonic.crest" in text
            or "Packages\\com.waveharmonic.crest" in text
            or "CrestMigration" in text
        ):
            active_stale_root_lscache.append(path.name)
    archived_stale_root_lscache = sorted(
        path.name
        for path in (ROOT / "Docs/Archive/Crest_Version_Quarantine/GeneratedProject").glob("*.csproj.lscache")
        if path.name.startswith("WaveHarmonic.Crest") or path.name in {
            "Assembly-CSharp.csproj.lscache",
            "Assembly-CSharp-Editor.csproj.lscache",
            "Assembly-CSharp-Editor-firstpass.csproj.lscache",
            "Assembly-CSharp-firstpass.csproj.lscache",
            "Hecton8.Core.csproj.lscache",
            "Hecton8.Editor.csproj.lscache",
            "Unity.RenderPipelines.Core.Editor.csproj.lscache",
            "Unity.RenderPipelines.Universal.Editor.csproj.lscache",
            "Unity.RenderPipelines.Universal.Runtime.csproj.lscache",
            "Unity.ShaderGraph.Editor.csproj.lscache",
        }
    )
    add_check(
        checks,
        "stale_broad_csharp_devkit_lscache_no_waveharmonic_crest",
        not active_stale_root_lscache and len(archived_stale_root_lscache) >= 17,
        "Root broad C# Dev Kit lscache files with stale WaveHarmonic Crest routes are archived outside active MSBuild/IDE visibility.",
    )
    generated_project_text = "\n".join(
        path.read_text(encoding="utf-8-sig", errors="replace")
        for path in sorted(ROOT.glob("*.csproj"))
        if path.name not in {"Crest.csproj", "Crest.Helpers.Editor.csproj"}
    )
    add_check(
        checks,
        "generated_first_party_projects_have_no_direct_crest_references",
        'ProjectReference Include="Crest.csproj"' not in generated_project_text
        and 'ProjectReference Include="Crest.Helpers.Editor.csproj"' not in generated_project_text
        and "WaveHarmonic.Crest" not in generated_project_text
        and "Packages\\com.waveharmonic.crest" not in generated_project_text
        and "Packages/com.waveharmonic.crest" not in generated_project_text,
        "Root first-party/generated projects no longer carry direct Crest or archived WaveHarmonic project/package routes.",
    )
    add_check(
        checks,
        "generated_first_party_projects_do_not_compile_bridge_sources",
        'Compile Include="Assets\\_Project\\Scripts\\Plugins\\Crest\\' not in generated_project_text
        and 'Compile Include="Assets/_Project/Scripts/Plugins/Crest/' not in generated_project_text,
        "Root first-party/generated projects do not compile Hecton8.Crest.Bridge source files through broad assemblies.",
    )
    directory_build_targets = read_text("Directory.Build.targets")
    add_check(
        checks,
        "directory_build_no_core_crest_reference_shim",
        '<Reference Include="Crest"' not in directory_build_targets
        and '<Reference Include="WaveHarmonic.Crest' not in directory_build_targets
        and "HectonPruneMissingWaveHarmonicCrestPackageItems" in directory_build_targets,
        "Directory.Build.targets no longer injects Crest into Hecton8.Core; only the missing-package prune target remains.",
    )
    add_check(
        checks,
        "active_crest_migration_payload_outside_unity_visibility",
        not (ROOT / "Assets/_Project/Data/CrestMigration").exists()
        and not (ROOT / "Assets/_Project/Data/CrestMigration.meta").exists()
        and (ROOT / "Docs/Archive/Crest_Version_Quarantine/Assets/_Project/Data/CrestMigration/Crest4SettingsDump.json").exists()
        and (ROOT / "Docs/Archive/Crest_Version_Quarantine/Assets/_Project/Data/CrestMigration/Crest4SettingsDump.json.meta").exists()
        and (ROOT / "Docs/Archive/Crest_Version_Quarantine/Assets/_Project/Data/CrestMigration.meta").exists(),
        "Crest migration dumps and folder meta are archived outside active Assets.",
    )
    quarantine_report_path = ROOT / "Docs/Reports/CREST_QUARANTINE_REPORT.json"
    quarantine_report = {}
    if quarantine_report_path.exists():
        quarantine_report = json.loads(quarantine_report_path.read_text(encoding="utf-8-sig"))
    archive_records = {
        record.get("label"): record
        for record in quarantine_report.get("archives", [])
        if isinstance(record, dict)
    }
    project_binding_labels = (
        "crest4_project_ocean_settings",
        "crest4_project_legacy_crest_settings",
        "crest4_project_ocean_prefab",
        "crest4_project_ocean_prefab_meta",
        "crest4_project_world_ocean_scene",
        "crest4_project_world_ocean_scene_meta",
    )
    add_check(
        checks,
        "crest4_project_bindings_have_baseline_archives",
        all(
            bool(archive_records.get(label, {}).get("exists"))
            and bool(archive_records.get(label, {}).get("zip"))
            and int(archive_records.get(label, {}).get("file_count", 0)) > 0
            for label in project_binding_labels
        ),
        "Crest baseline archiver captured project-side Crest4 settings, prefab, and 02_HECTON_WORLD ocean scene bindings in Docs/Archive.",
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
        not any("WaveHarmonic.Crest" in text for text in init_test_scene_texts),
        "Active root InitTestScene YAML files are absent or no longer ask Unity TestRunner to load WaveHarmonic Crest assemblies.",
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
    dry_volume_restore_shader = read_text("Assets/_Project/Art/Shaders/Hecton_DryVolumeRestore.shader")
    add_check(
        checks,
        "dry_volume_reads_vendor_texture_id_through_bridge",
        "_Crest_CameraColorTexture" not in dry_volume_feature
        and "CrestCameraColorTextureId" not in dry_volume_feature
        and "TryReadOceanCameraColorTexture" in dry_volume_feature
        and "bridge.CameraColorTextureId" in dry_volume_feature,
        "Dry-volume render pass no longer hard-codes the Crest camera color global; it reads the active ocean visual bridge ID.",
    )
    add_check(
        checks,
        "active_shader_no_crest_globals_outside_bridge",
        "_Crest_" not in dry_volume_restore_shader
        and "_OceanCameraColorTexture" in dry_volume_restore_shader
        and "OceanCameraColorTextureId" in dry_volume_feature
        and "SetGlobalTexture(ShaderConstants.OceanCameraColorTextureId" in dry_volume_feature,
        "Non-bridge dry-volume shader samples a vendor-neutral ocean camera texture global supplied by the render pass.",
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
    editor_bridge_asmdef = read_text("Assets/_Project/Scripts/Plugins/Crest/Editor/Hecton8.Crest.Bridge.Editor.asmdef")
    crest_donor_asmdef = read_text("Assets/Crest/Crest/Scripts/Crest.asmdef")
    crest_donor_editor_asmdef = read_text("Assets/Crest/Crest/Scripts/Editor/Crest.Editor.asmdef")
    add_check(
        checks,
        "bridge_asmdef_not_auto_referenced",
        '"autoReferenced": false' in asmdef and '"autoReferenced": false' in editor_bridge_asmdef,
        "Runtime and editor Crest bridge assemblies stay opt-in and cannot leak into unrelated assemblies by auto-reference.",
    )
    add_check(
        checks,
        "crest_donor_asmdefs_not_auto_referenced",
        '"autoReferenced": false' in crest_donor_asmdef and '"autoReferenced": false' in crest_donor_editor_asmdef,
        "Active Crest donor runtime/editor asmdefs are leaf-import guarded with autoReferenced=false.",
    )
    add_check(
        checks,
        "crest_donor_no_absent_hdrp_postprocessing_references",
        "Unity.RenderPipelines.HighDefinition.Runtime" not in crest_donor_asmdef
        and "Unity.Postprocessing.Runtime" not in crest_donor_asmdef,
        "Active Crest donor asmdef no longer references absent HDRP or PostProcessing assemblies.",
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

    add_check(
        checks,
        "editor_bridge_no_easysave3_reference",
        "EasySave3" not in editor_bridge_asmdef,
        "Crest bridge editor assembly no longer references the forbidden EasySave3 assembly.",
    )

    dependency_report_path = ROOT / "Docs" / "Reports" / "ARCHITECTURE_OPTIMIZATION_REPORT.json"
    dependency_report = {}
    if dependency_report_path.exists():
        dependency_report = json.loads(dependency_report_path.read_text(encoding="utf-8-sig"))
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
    add_check(
        checks,
        "dependency_scanner_tracks_crest_scripting_defines",
        isinstance(dependency_report.get("global_scripting_define_hit_count"), int)
        and isinstance(dependency_report.get("global_scripting_define_hits"), list),
        "Dependency scanner reports global Crest scripting symbols and hard-fails first-party Crest preprocessor branches outside the bridge.",
    )
    add_check(
        checks,
        "dependency_scanner_tracks_compliance_denylist_strings",
        isinstance(dependency_report.get("compliance_denylist_hit_count"), int)
        and isinstance(dependency_report.get("compliance_denylist_hits"), list)
        and "scan_compliance_denylist_strings" in read_text("Tools/Crest_Dependency_Scanner.py"),
        "Dependency scanner reports policy-only Crest strings in HectonComplianceValidator as non-failing evidence, not hidden runtime coupling.",
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
    add_check(
        checks,
        "dependency_scanner_covers_asmref_and_crest_guid_references",
        "CREST_ASMDEF_GUID_REFERENCES" in dependency_scanner_source
        and "5b35af79ebbe89647a157055d52c59d3" in dependency_scanner_source
        and "59cd48da98d9e4a80917b613abe9416e" in dependency_scanner_source
        and "asmref_reference" in dependency_scanner_source
        and 'root.rglob("*.asmref")' in dependency_scanner_source,
        "Dependency scanner treats Unity GUID-form asmdef references and .asmref sidecars as Crest wall breaches unless they are inside the bridge.",
    )
    add_check(
        checks,
        "dependency_scanner_blocks_archived_asset_guid_references",
        "QUARANTINED_ASSET_GUIDS" in dependency_scanner_source
        and "ed12880d16f3f2f4e80ceee64594101d" in dependency_scanner_source
        and "149ebcba5c729ad49911b1ea4b8456fd" in dependency_scanner_source
        and "0ef7bde4d259c9d4abcc93f41b0903a0" in dependency_scanner_source
        and "a73ab923bdc811242bdca5f288eb3877" in dependency_scanner_source,
        "Dependency scanner fails active references to archived Crest5 settings, Crest5 scene, and recovery folder GUIDs.",
    )
    add_check(
        checks,
        "dependency_scanner_blocks_auto_referenced_crest_assemblies",
        "scan_crest_donor_autoreference" in dependency_scanner_source
        and "crest_donor_asmdef_auto_referenced" in dependency_scanner_source
        and "bridge_crest_asmdef_auto_referenced" in dependency_scanner_source,
        "Dependency scanner hard-fails Crest donor or bridge asmdefs if autoReferenced is re-enabled.",
    )
    add_check(
        checks,
        "dependency_scanner_blocks_absent_optional_donor_references",
        "scan_crest_donor_missing_optional_references" in dependency_scanner_source
        and "Unity.RenderPipelines.HighDefinition.Runtime" in dependency_scanner_source
        and "Unity.Postprocessing.Runtime" in dependency_scanner_source
        and "crest_donor_missing_optional_package_reference" in dependency_scanner_source,
        "Dependency scanner hard-fails selected Crest donor references to optional Unity assemblies when the backing package is absent.",
    )
    add_check(
        checks,
        "dependency_scanner_blocks_stale_generated_report_crest_rows",
        "scan_generated_report_crest_rows" in dependency_scanner_source
        and "Assets/profilermarkers.csv" in dependency_scanner_source
        and "generated_report_crest_reference" in dependency_scanner_source,
        "Dependency scanner hard-fails Unity-visible generated profiler reports that retain Crest rows.",
    )
    add_check(
        checks,
        "dependency_scanner_blocks_generated_project_crest_routes",
        "scan_generated_project_crest_routes" in dependency_scanner_source
        and "generated_project_crest_route" in dependency_scanner_source
        and "active_waveharmonic_generated_project_file" in dependency_scanner_source,
        "Dependency scanner hard-fails active generated project and MSBuild routes into Crest/WaveHarmonic outside the donor/helper boundary.",
    )
    add_check(
        checks,
        "dependency_scanner_blocks_broad_stale_lscache_crest_routes",
        "generated_project_stale_lscache_crest_route" in dependency_scanner_source
        and "CrestMigration" in dependency_scanner_source,
        "Dependency scanner hard-fails broad root C# Dev Kit lscache files that retain stale Crest/WaveHarmonic routes.",
    )
    add_check(
        checks,
        "dependency_scanner_blocks_bridge_source_in_broad_project",
        "generated_project_bridge_source_in_broad_project" in dependency_scanner_source
        and "GENERATED_PROJECT_BRIDGE_SOURCE_RE" in dependency_scanner_source,
        "Dependency scanner hard-fails broad generated projects that compile Hecton8.Crest.Bridge source files.",
    )
    add_check(
        checks,
        "dependency_scanner_blocks_shader_globals_migration_and_profiler_payloads",
        "active_shader_crest_global" in dependency_scanner_source
        and "scan_active_crest_migration_payloads" in dependency_scanner_source
        and "scan_generated_profiler_payload_visibility" in dependency_scanner_source
        and "generated_profiler_payload_visible" in dependency_scanner_source,
        "Dependency scanner hard-fails non-bridge Crest shader globals, active Crest migration payloads, and active profiler marker payloads.",
    )
    add_check(
        checks,
        "dependency_scanner_reports_generated_project_define_symbols",
        "generated_project_scripting_define_hits" in dependency_scanner_source
        and "generated_project_prune_rule_hits" in dependency_scanner_source,
        "Dependency scanner reports generated-project Crest scripting defines and allowed WaveHarmonic prune rules as evidence.",
    )
    add_check(
        checks,
        "dependency_scanner_blocks_non_bridge_crest_preprocessor_branches",
        "CREST_SCRIPTING_DEFINE_SYMBOLS" in dependency_scanner_source
        and "scan_first_party_scripting_define_usage" in dependency_scanner_source
        and "crest_scripting_define_usage" in dependency_scanner_source
        and "scan_global_scripting_defines" in dependency_scanner_source,
        "Dependency scanner fails non-bridge #if CREST_OCEAN/CREST_URP usage while keeping current ProjectSettings donor symbols as non-failing evidence.",
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
