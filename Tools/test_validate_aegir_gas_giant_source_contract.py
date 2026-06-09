import json
import shutil
import sys
import unittest
import uuid
from contextlib import contextmanager
from collections.abc import Iterator
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import ValidateAegirGasGiantSourceContract as aegir  # noqa: E402

TEST_TMP_ROOT = TOOLS_ROOT.parent / "Temp" / "AegirGasGiantSourceContractTests"


@contextmanager
def temp_project_root() -> Iterator[Path]:
    TEST_TMP_ROOT.mkdir(parents=True, exist_ok=True)
    root = TEST_TMP_ROOT / f"case_{uuid.uuid4().hex}"
    root.mkdir(parents=True, exist_ok=False)
    try:
        yield root
    finally:
        shutil.rmtree(root, ignore_errors=True)


def write(root: Path, rel_path: str, text: str) -> None:
    path = root / rel_path
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


def write_bytes(root: Path, rel_path: str, payload: bytes) -> None:
    path = root / rel_path
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(payload)


def material_slot(name: str, guid: str) -> str:
    return (
        f"    - {name}:\n"
        f"        m_Texture: {{fileID: 2800000, guid: {guid}, type: 3}}\n"
        "        m_Scale: {x: 1, y: 1}\n"
        "        m_Offset: {x: 0, y: 0}\n"
    )


def texture_meta(guid: str, *, mip: int = 1, readable: int = 0, streaming: int = 1, max_size: int = 2048) -> str:
    return (
        "fileFormatVersion: 2\n"
        f"guid: {guid}\n"
        "TextureImporter:\n"
        "  mipmaps:\n"
        f"    enableMipMap: {mip}\n"
        f"  isReadable: {readable}\n"
        f"  streamingMipmaps: {streaming}\n"
        f"  maxTextureSize: {max_size}\n"
    )


def gas_prefab(mesh_guid: str) -> str:
    return (
        "%YAML 1.1\n"
        "--- !u!33 &1\n"
        f"  m_Mesh: {{fileID: 4300000, guid: {mesh_guid}, type: 2}}\n"
        "--- !u!23 &2\n"
        "  m_CastShadows: 0\n"
        "  m_ReceiveShadows: 0\n"
        "  m_DynamicOccludee: 0\n"
        "  m_MotionVectors: 0\n"
        "  m_LightProbeUsage: 0\n"
        "  m_ReflectionProbeUsage: 0\n"
        "  m_Materials:\n"
        f"  - {{fileID: 2100000, guid: {aegir.CANONICAL_GAS_MATERIAL_GUID}, type: 2}}\n"
    )


def surface_weather_profile(kind: int, storm_emission_multiplier: float) -> str:
    return (
        "%YAML 1.1\n"
        "MonoBehaviour:\n"
        f"  weatherKind: {kind}\n"
        f"  stormEmissionMultiplier: {storm_emission_multiplier}\n"
    )


def healthy_scene() -> str:
    return (
        "%YAML 1.1\n"
        "--- !u!1001 &1\n"
        "PrefabInstance:\n"
        "  m_SourcePrefab: {fileID: 100100000, guid: "
        f"{aegir.PRODUCTION_PREFAB_GUID}, type: 3}}\n"
    )


def scene_with_bad_overrides() -> str:
    return (
        "%YAML 1.1\n"
        "--- !u!1001 &1\n"
        "PrefabInstance:\n"
        "  m_Modification:\n"
        "    m_Modifications:\n"
        f"    - target: {{fileID: 1, guid: {aegir.PRODUCTION_PREFAB_GUID}, type: 3}}\n"
        "      propertyPath: m_Mesh\n"
        "      value:\n"
        f"      objectReference: {{fileID: 10207, guid: {aegir.UNITY_BUILTIN_PRIMITIVE_GUID}, type: 0}}\n"
        f"    - target: {{fileID: 2, guid: {aegir.PRODUCTION_PREFAB_GUID}, type: 3}}\n"
        "      propertyPath: m_CastShadows\n"
        "      value: 1\n"
        "      objectReference: {fileID: 0}\n"
        f"    - target: {{fileID: 2, guid: {aegir.PRODUCTION_PREFAB_GUID}, type: 3}}\n"
        "      propertyPath: m_LightProbeUsage\n"
        "      value: 1\n"
        "      objectReference: {fileID: 0}\n"
        "  m_SourcePrefab: {fileID: 100100000, guid: "
        f"{aegir.PRODUCTION_PREFAB_GUID}, type: 3}}\n"
    )


def scene_with_duplicate_aegir_prefabs() -> str:
    return healthy_scene() + healthy_scene().replace("--- !u!1001 &1\n", "--- !u!1001 &2\n")


def write_healthy_fixture(root: Path, *, include_proof: bool = True) -> None:
    write(
        root,
        aegir.SKY_MATERIAL,
        "%YAML 1.1\nMaterial:\n"
        f"  m_Shader: {{fileID: 4800000, guid: {aegir.CANONICAL_SKY_SHADER_GUID}, type: 3}}\n"
        "  m_SavedProperties:\n    m_TexEnvs:\n"
        + material_slot("_AegirBandTex", aegir.CANONICAL_BAND_GUID),
    )
    write(
        root,
        aegir.GAS_MATERIAL,
        "%YAML 1.1\nMaterial:\n"
        f"  m_Shader: {{fileID: 4800000, guid: {aegir.CANONICAL_IMPOSTOR_SHADER_GUID}, type: 3}}\n"
        "  m_SavedProperties:\n    m_TexEnvs:\n"
        + material_slot("_MainTex", aegir.CANONICAL_BAND_GUID)
        + material_slot("_DetailTex", aegir.CANONICAL_DETAIL_GUID)
        + material_slot("_StormTex", aegir.CANONICAL_STORM_GUID)
        + "    m_Floats:\n"
        + "    - _StormEmission: 1\n",
    )
    for texture_path, guid in (
        (aegir.CANONICAL_BAND_TEXTURE, aegir.CANONICAL_BAND_GUID),
        (aegir.CANONICAL_DETAIL_TEXTURE, aegir.CANONICAL_DETAIL_GUID),
        (aegir.CANONICAL_STORM_TEXTURE, aegir.CANONICAL_STORM_GUID),
    ):
        write(root, texture_path, "png")
        write(root, texture_path + ".meta", texture_meta(guid))

    write(root, aegir.CANONICAL_PRODUCTION_MESH + ".meta", f"guid: {aegir.CANONICAL_PRODUCTION_MESH_GUID}\n")
    write(root, aegir.PRODUCTION_PREFAB, gas_prefab(aegir.CANONICAL_PRODUCTION_MESH_GUID))
    write(root, aegir.LEGACY_PROLOGUE_PREFAB, gas_prefab(aegir.CANONICAL_PRODUCTION_MESH_GUID))
    write(root, aegir.ORBIT_SCENE, healthy_scene())
    write(
        root,
        aegir.EDITOR_VALIDATOR,
        "Aegir Gas Giant Source Contract ValidateTextureImport ValidateOrbitSceneOverrides "
        "RepairPrefabSource RepairOrbitSceneFromMenu RevertAegirScenePrefabOverrides "
        "PrefabUtility.RevertPropertyOverride(property, InteractionMode.AutomatedAction); "
        "ValidateMaterialFloat(gasMaterial, \"_StormEmission\", 1f ValidateRuntimeSourceContracts(report); "
        "EnsureMaterialFloat(GasGiantMaterialPath, \"_StormEmission\", 1f); private static int EnsureMaterialFloat "
        "CheckedSourceCount BadRuntimeSourceContract ResolveAegirSkyProjectionStormEmission() "
        "_AegirStormEmissionInvalidWarningHash AegirStormEmissionWarningCooldownFrames "
        "ReportAegirStormEmissionInvalidIfNeeded "
        "Shader.SetGlobalFloat(_ID_H8AegirStormEmission, ResolveAegirSkyProjectionStormEmission()); "
        "Shader.SetGlobalFloat(_aegirStormEmissionId, 1f); stormBand * cloudTexture * 0.15 * stormEmission "
        "stormEmissionMultiplier weather-driven storm emission "
        "RestoreCelestialTextureDefaults(); ClearAegirMaterialRuntimeCache(); "
        "aegirRenderer.SetPropertyBlock(null); _aegirSharedMaterial = null; "
        "PublishOceanCelestialProjectionGlobals(aegirDirection) "
        "_ID_HectonEclipseWaterShadowParams _ID_HectonEclipseWaterShadowDirection "
        "_ID_HectonRingCausticsParams _ID_HectonRingCausticsDirection "
        "ResolveAupOceanShadowCenterRuntimeXZ TryResolvePlayerAup "
        f"{aegir.UNITY_BUILTIN_PRIMITIVE_GUID} {aegir.PRODUCTION_PREFAB_GUID}",
    )
    write(root, aegir.PRODUCT_FACE_VALIDATOR, "ValidateAegirGasGiantSource(report);")
    write(
        root,
        aegir.CELESTIAL_ENGINE,
        "TryClaimCelestialRuntimeAuthority() DisableDuplicateCelestialPresentation() "
        "PublishCelestialRuntimeSnapshot(!usingPublishedCelestialSnapshot) "
        "GlobalRegistry.PublishCelestialRuntimeSnapshot(in snapshot) "
        "PublishAegirSkyProjectionGlobals(aegirDirection) "
        "PublishOceanCelestialProjectionGlobals(aegirDirection) ResolveAegirSkyProjectionQuality01 "
        "return math.max(math.saturate(profile.minimumQuality), quality); "
        "ResolveAegirSkyProjectionVisibility01 ValidateAegirRendererMaterialCold PublishAegirPresentationWarning "
        "TryRaiseCelestialSunAngleChanged TryRaiseCelestialPlanetPhaseChanged "
        "TryRaiseCelestialEclipseStarted TryRaiseCelestialEclipseEnded "
        "ReportCelestialEventDropIfBackpressured _CelestialEventDropWarningHash "
        "QueueDeferredRegister QueueDeferredUnregister ApplyDeferredListenerMutations "
        "DispatchToListener ReportQueueOverflow ReportDuplicateListenerRegistration "
        "ReportListenerRejected ReportListenerDispatchException ReportUnregisterMiss DrainQueuedEvents "
        "CelestialTruthReadFailure ReportCelestialTruthFallbackIfNeeded "
        "ResolveCelestialTruthFailureContextHash _CelestialTruthFallbackWarningHash "
        "_AegirDuplicateOwnerWarningHash _AegirMissingMaterialWarningHash _AegirMissingBandTextureWarningHash "
        "_AegirStormEmissionInvalidWarningHash AegirStormEmissionWarningCooldownFrames "
        "ReportAegirStormEmissionInvalidIfNeeded ResolveAegirSkyProjectionStormEmission() "
        "block.SetFloat(_ID_StormEmission, ResolveAegirSkyProjectionStormEmission()); "
        "ClearAegirSkyProjectionGlobals ClearAegirMaterialRuntimeCache "
        "_H8AegirSunDirection _H8AegirPlanetCenterRadius _H8AegirRingPlaneInner _H8AegirOrbitScalars "
        "_H8AegirFlowPhaseValid _H8AegirStormEmission _H8GlobalQualityWeight "
        "_ID_HectonEclipseWaterShadowParams _ID_HectonEclipseWaterShadowDirection "
        "_ID_HectonRingCausticsParams _ID_HectonRingCausticsDirection "
        "ResolveAupOceanShadowCenterRuntimeXZ TryResolvePlayerAup "
        "internal void ClearSurfaceWeatherOverride() { _surfaceWeatherOverrideActive = false; "
        "_surfaceWeatherFogOverrideActive = false; _surfaceWeatherStormEmissionMultiplier = 1f; "
        "if (Application.isPlaying) { UpdateSkyMaterial(); UpdateAegirMaterial(); } } "
        "private void OnDisable() { RestoreCelestialTextureDefaults(); ClearAegirMaterialRuntimeCache(); "
        "ClearCelestialTruthReadCache(); } "
        "private void OnDestroy() { RestoreCelestialTextureDefaults(); ClearAegirMaterialRuntimeCache(); } "
        "private void ClearAegirMaterialRuntimeCache() { aegirRenderer.SetPropertyBlock(null); "
        "_aegirMPB.Clear(); _aegirSharedMaterial = null; _aegirMainTexDefault = null; "
        "_aegirDetailTexDefault = null; _aegirEmissionMapDefault = null; "
        "_aegirCelestialOcclusionTexDefault = null; }",
    )
    write(
        root,
        aegir.ORBITAL_RELATIVITY_DIRECTOR,
        "ICelestialRuntimeSnapshotReadModel readModel = ResolveCelestialRuntimeSnapshotReadModel(); "
        "TryReadPublishedCelestialSnapshot( IsCelestialSnapshotReadable(in snapshot) "
        "ReportCelestialSnapshotFallbackIfNeeded(failure) "
        "CelestialSnapshotReadFailure.MissingService CelestialSnapshotReadFailure.InvalidSnapshot "
        "ResolveCelestialSnapshotFallbackSeverity "
        "CelestialSnapshotFallbackAnomalyCooldownFrames "
        "Shader.SetGlobalVector(_aegirSunDirectionId Shader.SetGlobalVector(_aegirPlanetCenterRadiusId "
        "Shader.SetGlobalFloat(_aegirStormEmissionId, 1f) "
        "Shader.SetGlobalFloat(_aegirFlowPhaseValidId, 1f) "
        "CacheCelestialRuntimeSnapshotReadModel(currentService as ICelestialRuntimeSnapshotReadModel)",
    )
    write(
        root,
        aegir.SURFACE_WEATHER_DIRECTOR,
        "stormEmissionMultiplier = asset.StormEmissionMultiplier, "
        "activeCelestialEngine.SetSurfaceWeatherOverride( _currentState.stormEmissionMultiplier, "
        "activeCelestialEngine.ClearSurfaceWeatherOverride(); "
        "ClearCelestialSurfaceWeatherOverride(previousCelestialEngine); "
        "CacheCelestialEngine(currentCelestialEngine); "
        "private const float FallbackClearCalmStormEmissionMultiplier = 0.95f; "
        "private const float FallbackClearBreezeStormEmissionMultiplier = 1.0f; "
        "private const float FallbackOvercastStormEmissionMultiplier = 1.2f; "
        "private const float FallbackHeavyRainStormEmissionMultiplier = 1.55f; "
        "private const float FallbackElectricalStormStormEmissionMultiplier = 1.85f; "
        "FallbackClearCalmStormEmissionMultiplier, FallbackClearBreezeStormEmissionMultiplier, "
        "FallbackOvercastStormEmissionMultiplier, FallbackHeavyRainStormEmissionMultiplier, "
        "FallbackElectricalStormStormEmissionMultiplier, "
        "private void OnDestroy() { DisposeWeatherMathBuffers(forceCompletePendingJob: true); "
        "ClearWeatherBindings(); FlushWeatherShaderGlobals(); TryUnregisterService(); }",
    )
    write(
        root,
        aegir.AEGIR_SKY_SHADER,
        "_H8AegirSunDirection _H8AegirPlanetCenterRadius _H8AegirRingPlaneInner _H8AegirOrbitScalars "
        "_H8AegirFlowPhaseValid _H8AegirStormEmission _H8GlobalQualityWeight "
        "float AegirStormEmission() clamp(_H8AegirStormEmission, 0.0, 4.0) "
        "bool RaySphere( bool RayRingPlane( float RingShadow( float3 DrawAegir( "
        "SAMPLE_TEXTURE2D(_AegirBandTex AegirFlowPhase(flowSpeed) "
        "float hardTerminator = smoothstep(-0.08, 0.18, ndotl); "
        "float limbDarken = lerp(1.0, 0.58 color += _AtmosphereTint.rgb * scatter; "
        "RingShadow(hitPoint, lightDir "
        "systemVisibility = saturate(1.0 - _H8AegirSunDirection.w) float3 planetColor = DrawAegir "
        "stormBand * cloudTexture * 0.15 * stormEmission "
        "bands += float3(0.095, 0.052, 0.022) * stormSignal * stormEmission",
    )
    write(
        root,
        aegir.AEGIR_IMPOSTOR_SHADER,
        "_PlanetPhase _H8GlobalQualityWeight _H8AegirSunDirection _HectonCelestialLightReadability0 "
        "TEXTURE2D(_MainTex); TEXTURE2D(_DetailTex); TEXTURE2D(_StormTex); "
        "_StormEmission (\"Runtime Storm Emission\" half _StormEmission; "
        "_WarmTint.rgb * stormMask * _StormStrength * _StormEmission "
        "baseUv.x = frac(baseUv.x + _Rotation + _GlobalRotation + syncTime * _AutoRotationSpeed); "
        "half phase = smoothstep(_PhaseCenter - _PhaseSoftness "
        "half limbDarken = lerp(1.0h, 0.58h, pow(limb, 1.25h)); "
        "_HectonCelestialLightReadability0.w / 112.0 "
        "horizonVeil = 1.0h - smoothstep( color = lerp(color, veilColor, atmosphereMask); "
        "systemVisibility = min(systemVisibility",
    )
    write(
        root,
        aegir.PROOF_TOOL,
        "CANONICAL_BAND_TEXTURE CANONICAL_DETAIL_TEXTURE CANONICAL_STORM_TEXTURE "
        "surface_clear_full underwater_up horizon_veil phase_degrees limb "
        "storm_emission stormEmissionMultiplier quality_weight qualityWeight "
        "underwater-up readability quality-tier fallback AEGIR_GAS_GIANT_PROOF_CONTACT_SHEET_BUILT",
    )
    if include_proof:
        write_bytes(root, aegir.PROOF_IMAGE, b"\x89PNG\r\n\x1a\nfixture")
        write(
            root,
            aegir.PROOF_MANIFEST,
            json.dumps(
                {
                    "status": "AEGIR_GAS_GIANT_PROOF_CONTACT_SHEET_BUILT",
                    "image": aegir.PROOF_IMAGE,
                    "sourceTextures": [
                        {"role": "bands", "path": aegir.CANONICAL_BAND_TEXTURE, "guid": aegir.CANONICAL_BAND_GUID},
                        {"role": "detail", "path": aegir.CANONICAL_DETAIL_TEXTURE, "guid": aegir.CANONICAL_DETAIL_GUID},
                        {"role": "storms", "path": aegir.CANONICAL_STORM_TEXTURE, "guid": aegir.CANONICAL_STORM_GUID},
                    ],
                    "views": [
                        {"id": "surface_clear_full", "mode": "surface", "phaseDegrees": 18.0, "cloud": 0.05, "fog": 0.12, "underwater": 0.0, "horizonOcclusion": 0.0, "stormEmissionMultiplier": 0.82, "qualityWeight": 1.0},
                        {"id": "surface_cloud_fog_half", "mode": "surface", "phaseDegrees": 72.0, "cloud": 0.42, "fog": 0.34, "underwater": 0.0, "horizonOcclusion": 0.0, "stormEmissionMultiplier": 1.22, "qualityWeight": 0.92},
                        {"id": "underwater_up", "mode": "underwater", "phaseDegrees": 48.0, "cloud": 0.16, "fog": 0.22, "underwater": 1.0, "horizonOcclusion": 0.0, "stormEmissionMultiplier": 1.0, "qualityWeight": 0.72},
                        {"id": "horizon_veil", "mode": "horizon", "phaseDegrees": 36.0, "cloud": 0.2, "fog": 0.48, "underwater": 0.0, "horizonOcclusion": 0.42, "stormEmissionMultiplier": 1.48, "qualityWeight": 1.0},
                        {"id": "crescent_low_light", "mode": "surface", "phaseDegrees": 126.0, "cloud": 0.08, "fog": 0.18, "underwater": 0.0, "horizonOcclusion": 0.0, "stormEmissionMultiplier": 0.92, "qualityWeight": 0.84},
                        {"id": "heavy_fog_occlusion", "mode": "horizon", "phaseDegrees": 94.0, "cloud": 0.58, "fog": 0.68, "underwater": 0.0, "horizonOcclusion": 0.3, "stormEmissionMultiplier": 1.92, "qualityWeight": 0.48},
                    ],
                    "contract": {"offlineProof": True, "unityRuntimeProof": False, "covers": ["weather-driven storm emission", "quality-tier fallback"]},
                }
            ),
        )
    for file_name, kind, min_multiplier, max_multiplier in aegir.SURFACE_WEATHER_PROFILE_CONTRACTS:
        multiplier = (min_multiplier + max_multiplier) * 0.5
        write(
            root,
            f"{aegir.SURFACE_WEATHER_PROFILE_ROOT}/{file_name}",
            surface_weather_profile(kind, multiplier),
        )


class AegirGasGiantSourceContractTests(unittest.TestCase):
    def test_healthy_fixture_passes(self) -> None:
        with temp_project_root() as tmp:
            root = Path(tmp)
            write_healthy_fixture(root)

            report = aegir.validate(root)

        self.assertEqual("AEGIR_GAS_GIANT_SOURCE_CONTRACT_PASS", report.status)
        self.assertEqual(0, report.error_count)
        self.assertEqual(5, report.checked_source_files)
        self.assertEqual(2, report.checked_proofs)
        self.assertEqual(10, report.checked_weather_profiles)

    def test_wrong_sky_texture_and_scene_override_fail(self) -> None:
        with temp_project_root() as tmp:
            root = Path(tmp)
            write_healthy_fixture(root)
            write(
                root,
                aegir.SKY_MATERIAL,
                "%YAML 1.1\nMaterial:\n"
                f"  m_Shader: {{fileID: 4800000, guid: {aegir.CANONICAL_SKY_SHADER_GUID}, type: 3}}\n"
                "  m_SavedProperties:\n    m_TexEnvs:\n"
                + material_slot("_AegirBandTex", aegir.CANONICAL_STORM_GUID),
            )
            write(root, aegir.ORBIT_SCENE, scene_with_bad_overrides())

            report = aegir.validate(root)

        codes = {finding.code for finding in report.findings}
        self.assertEqual("AEGIR_GAS_GIANT_SOURCE_CONTRACT_FAIL", report.status)
        self.assertIn("BAD_MATERIAL_TEXTURE_GUID", codes)
        self.assertIn("BAD_AEGIR_SCENE_OVERRIDE", codes)

    def test_bad_texture_import_and_legacy_builtin_prefab_fail(self) -> None:
        with temp_project_root() as tmp:
            root = Path(tmp)
            write_healthy_fixture(root)
            write(
                root,
                aegir.CANONICAL_BAND_TEXTURE + ".meta",
                texture_meta(aegir.CANONICAL_BAND_GUID, mip=0, readable=1, streaming=0, max_size=1024),
            )
            write(root, aegir.LEGACY_PROLOGUE_PREFAB, gas_prefab(aegir.UNITY_BUILTIN_PRIMITIVE_GUID))

            report = aegir.validate(root)

        codes = {finding.code for finding in report.findings}
        self.assertEqual("AEGIR_GAS_GIANT_SOURCE_CONTRACT_FAIL", report.status)
        self.assertIn("BAD_TEXTURE_IMPORT", codes)
        self.assertIn("BUILTIN_PRIMITIVE_MESH", codes)

    def test_duplicate_orbit_aegir_prefab_fails(self) -> None:
        with temp_project_root() as tmp:
            root = Path(tmp)
            write_healthy_fixture(root)
            write(root, aegir.ORBIT_SCENE, scene_with_duplicate_aegir_prefabs())

            report = aegir.validate(root)

        codes = {finding.code for finding in report.findings}
        self.assertEqual("AEGIR_GAS_GIANT_SOURCE_CONTRACT_FAIL", report.status)
        self.assertIn("DUPLICATE_AEGIR_SCENE_PREFAB", codes)

    def test_wrong_material_shader_guid_fails(self) -> None:
        with temp_project_root() as tmp:
            root = Path(tmp)
            write_healthy_fixture(root)
            write(
                root,
                aegir.GAS_MATERIAL,
                "%YAML 1.1\nMaterial:\n"
                "  m_Shader: {fileID: 4800000, guid: 11111111111111111111111111111111, type: 3}\n"
                "  m_SavedProperties:\n    m_TexEnvs:\n"
                + material_slot("_MainTex", aegir.CANONICAL_BAND_GUID)
                + material_slot("_DetailTex", aegir.CANONICAL_DETAIL_GUID)
                + material_slot("_StormTex", aegir.CANONICAL_STORM_GUID),
            )

            report = aegir.validate(root)

        codes = {finding.code for finding in report.findings}
        self.assertEqual("AEGIR_GAS_GIANT_SOURCE_CONTRACT_FAIL", report.status)
        self.assertIn("BAD_MATERIAL_SHADER_GUID", codes)

    def test_missing_material_storm_emission_default_fails(self) -> None:
        with temp_project_root() as tmp:
            root = Path(tmp)
            write_healthy_fixture(root)
            source = (root / aegir.GAS_MATERIAL).read_text(encoding="utf-8")
            (root / aegir.GAS_MATERIAL).write_text(source.replace("    - _StormEmission: 1\n", ""), encoding="utf-8")

            report = aegir.validate(root)

        codes = {finding.code for finding in report.findings}
        self.assertEqual("AEGIR_GAS_GIANT_SOURCE_CONTRACT_FAIL", report.status)
        self.assertIn("BAD_MATERIAL_STORM_EMISSION_DEFAULT", codes)

    def test_runtime_source_contract_drift_fails(self) -> None:
        with temp_project_root() as tmp:
            root = Path(tmp)
            write_healthy_fixture(root)
            write(root, aegir.CELESTIAL_ENGINE, "ClearAegirSkyProjectionGlobals")

            report = aegir.validate(root)

        codes = {finding.code for finding in report.findings}
        self.assertEqual("AEGIR_GAS_GIANT_SOURCE_CONTRACT_FAIL", report.status)
        self.assertIn("RUNTIME_SOURCE_CONTRACT_DRIFT", codes)

    def test_celestial_surface_weather_clear_missing_material_refresh_fails(self) -> None:
        with temp_project_root() as tmp:
            root = Path(tmp)
            write_healthy_fixture(root)
            path = root / aegir.CELESTIAL_ENGINE
            source = path.read_text(encoding="utf-8")
            path.write_text(source.replace("UpdateSkyMaterial(); UpdateAegirMaterial();", ""), encoding="utf-8")

            report = aegir.validate(root)

        codes = {finding.code for finding in report.findings}
        self.assertEqual("AEGIR_GAS_GIANT_SOURCE_CONTRACT_FAIL", report.status)
        self.assertIn("BAD_CELESTIAL_SURFACE_WEATHER_CLEAR_CONTRACT", codes)

    def test_celestial_aegir_material_cache_cleanup_missing_fails(self) -> None:
        with temp_project_root() as tmp:
            root = Path(tmp)
            write_healthy_fixture(root)
            path = root / aegir.CELESTIAL_ENGINE
            source = path.read_text(encoding="utf-8")
            path.write_text(source.replace("ClearAegirMaterialRuntimeCache();", ""), encoding="utf-8")

            report = aegir.validate(root)

        codes = {finding.code for finding in report.findings}
        self.assertEqual("AEGIR_GAS_GIANT_SOURCE_CONTRACT_FAIL", report.status)
        self.assertIn("BAD_CELESTIAL_AEGIR_CACHE_LIFECYCLE", codes)

    def test_orbital_consumer_contract_drift_fails(self) -> None:
        with temp_project_root() as tmp:
            root = Path(tmp)
            write_healthy_fixture(root)
            write(
                root,
                aegir.ORBITAL_RELATIVITY_DIRECTOR,
                "ICelestialRuntimeSnapshotReadModel readModel = ResolveCelestialRuntimeSnapshotReadModel(); "
                "Shader.SetGlobalVector(_aegirSunDirectionId",
            )

            report = aegir.validate(root)

        codes = {finding.code for finding in report.findings}
        self.assertEqual("AEGIR_GAS_GIANT_SOURCE_CONTRACT_FAIL", report.status)
        self.assertIn("RUNTIME_SOURCE_CONTRACT_DRIFT", codes)

    def test_weather_profile_flat_storm_multiplier_fails(self) -> None:
        with temp_project_root() as tmp:
            root = Path(tmp)
            write_healthy_fixture(root)
            write(
                root,
                f"{aegir.SURFACE_WEATHER_PROFILE_ROOT}/SurfaceWeatherProfile_HeavyRain.asset",
                surface_weather_profile(3, 1.0),
            )

            report = aegir.validate(root)

        codes = {finding.code for finding in report.findings}
        self.assertEqual("AEGIR_GAS_GIANT_SOURCE_CONTRACT_FAIL", report.status)
        self.assertIn("BAD_AEGIR_WEATHER_STORM_MULTIPLIER", codes)

    def test_fallback_weather_flat_storm_multiplier_fails(self) -> None:
        with temp_project_root() as tmp:
            root = Path(tmp)
            write_healthy_fixture(root)
            path = root / aegir.SURFACE_WEATHER_DIRECTOR
            source = path.read_text(encoding="utf-8")
            path.write_text(
                source.replace("private const float FallbackHeavyRainStormEmissionMultiplier = 1.55f;", "private const float FallbackHeavyRainStormEmissionMultiplier = 1.0f;"),
                encoding="utf-8",
            )

            report = aegir.validate(root)

        codes = {finding.code for finding in report.findings}
        self.assertEqual("AEGIR_GAS_GIANT_SOURCE_CONTRACT_FAIL", report.status)
        self.assertIn("BAD_AEGIR_FALLBACK_STORM_MULTIPLIER", codes)

    def test_surface_weather_destroy_unregister_before_clear_fails(self) -> None:
        with temp_project_root() as tmp:
            root = Path(tmp)
            write_healthy_fixture(root)
            path = root / aegir.SURFACE_WEATHER_DIRECTOR
            source = path.read_text(encoding="utf-8")
            path.write_text(
                source.replace(
                    "ClearWeatherBindings(); FlushWeatherShaderGlobals(); TryUnregisterService();",
                    "TryUnregisterService(); ClearWeatherBindings(); FlushWeatherShaderGlobals();",
                ),
                encoding="utf-8",
            )

            report = aegir.validate(root)

        codes = {finding.code for finding in report.findings}
        self.assertEqual("AEGIR_GAS_GIANT_SOURCE_CONTRACT_FAIL", report.status)
        self.assertIn("BAD_SURFACE_WEATHER_TEARDOWN_CONTRACT", codes)

    def test_duplicate_celestial_runtime_snapshot_owner_fails(self) -> None:
        with temp_project_root() as tmp:
            root = Path(tmp)
            write_healthy_fixture(root)
            write(
                root,
                "Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs",
                "GlobalRegistry.PublishCelestialRuntimeSnapshot(in celestial);",
            )

            report = aegir.validate(root)

        codes = {finding.code for finding in report.findings}
        self.assertEqual("AEGIR_GAS_GIANT_SOURCE_CONTRACT_FAIL", report.status)
        self.assertIn("DUPLICATE_CELESTIAL_RUNTIME_SNAPSHOT_OWNER", codes)

    def test_shader_quality_contract_drift_fails(self) -> None:
        with temp_project_root() as tmp:
            root = Path(tmp)
            write_healthy_fixture(root)
            write(
                root,
                aegir.AEGIR_SKY_SHADER,
                "_H8AegirSunDirection _H8AegirPlanetCenterRadius _H8AegirRingPlaneInner _H8AegirOrbitScalars "
                "_H8AegirFlowPhaseValid _H8GlobalQualityWeight "
                "systemVisibility = saturate(1.0 - _H8AegirSunDirection.w) float3 planetColor = DrawAegir",
            )

            report = aegir.validate(root)

        codes = {finding.code for finding in report.findings}
        self.assertEqual("AEGIR_GAS_GIANT_SOURCE_CONTRACT_FAIL", report.status)
        self.assertIn("RUNTIME_SOURCE_CONTRACT_DRIFT", codes)

    def test_missing_proof_artifact_fails(self) -> None:
        with temp_project_root() as tmp:
            root = Path(tmp)
            write_healthy_fixture(root, include_proof=False)

            report = aegir.validate(root)

        codes = {finding.code for finding in report.findings}
        self.assertEqual("AEGIR_GAS_GIANT_SOURCE_CONTRACT_FAIL", report.status)
        self.assertIn("MISSING_AEGIR_PROOF_MANIFEST", codes)

    def test_flat_proof_storm_emission_coverage_fails(self) -> None:
        with temp_project_root() as tmp:
            root = Path(tmp)
            write_healthy_fixture(root)
            manifest_path = root / aegir.PROOF_MANIFEST
            manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
            for view in manifest["views"]:
                view["stormEmissionMultiplier"] = 1.0
            manifest_path.write_text(json.dumps(manifest), encoding="utf-8")

            report = aegir.validate(root)

        codes = {finding.code for finding in report.findings}
        self.assertEqual("AEGIR_GAS_GIANT_SOURCE_CONTRACT_FAIL", report.status)
        self.assertIn("BAD_AEGIR_PROOF_STORM_EMISSION_COVERAGE", codes)

    def test_flat_proof_quality_coverage_fails(self) -> None:
        with temp_project_root() as tmp:
            root = Path(tmp)
            write_healthy_fixture(root)
            manifest_path = root / aegir.PROOF_MANIFEST
            manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
            for view in manifest["views"]:
                view["qualityWeight"] = 1.0
            manifest_path.write_text(json.dumps(manifest), encoding="utf-8")

            report = aegir.validate(root)

        codes = {finding.code for finding in report.findings}
        self.assertEqual("AEGIR_GAS_GIANT_SOURCE_CONTRACT_FAIL", report.status)
        self.assertIn("BAD_AEGIR_PROOF_QUALITY_COVERAGE", codes)


if __name__ == "__main__":
    unittest.main()
