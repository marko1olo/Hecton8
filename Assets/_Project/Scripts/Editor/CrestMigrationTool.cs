using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using OldCrest = Crest;
using NewCrest = WaveHarmonic.Crest;

namespace Hecton8.Editor
{
    /// <summary>
    /// Dumps Crest 4 scene settings and prepares a parallel Crest 5 migration scene.
    /// </summary>
    internal static class CrestMigrationTool
    {
        private const string SourceScenePath = "Assets/_Project/Scenes/02_HECTON_WORLD.unity";
        private const string TargetScenePath = "Assets/_Project/Scenes/03_HECTON_WORLD_CREST5.unity";
        private const string MigrationDataFolder = "Assets/_Project/Data/CrestMigration";
        private const string DumpAssetPath = MigrationDataFolder + "/Crest4SettingsDump.json";
        private const string SpectrumAssetPath = MigrationDataFolder + "/Crest5_WaveSpectrum.asset";
        private const string FoamSettingsAssetPath = MigrationDataFolder + "/Crest5_FoamSettings.asset";
        private const string Crest5WaterMaterialPath = "Packages/com.waveharmonic.crest/Runtime/Materials/Water.mat";
        private const string Crest5WaterVolumeMaterialPath = "Packages/com.waveharmonic.crest/Runtime/Materials/Water Volume.mat";
        private const string Crest5WaterObjectName = "Crest5_WaterRenderer";

        [Serializable]
        private sealed class Crest4SettingsDump
        {
            public string generatedUtc;
            public string sourceScenePath;
            public string sourceOceanObjectPath;
            public OceanDump ocean = new();
            public AnimatedWavesDump animatedWaves = new();
            public FoamDump foam = new();
            public ShapeFftDump shapeFft = new();
            public WaveSpectrumDump waveSpectrum = new();
            public VisualDump visuals = new();
            public string collisionMigrationNote;
        }

        [Serializable]
        private sealed class OceanDump
        {
            public string cameraPath;
            public string viewpointPath;
            public string primaryLightPath;
            public float globalWindSpeed;
            public bool overrideGravity;
            public float gravity;
            public float gravityMultiplier;
            public int layer;
            public float minScale;
            public float maxScale;
            public float dropDetailHeightBasedOnWaves;
            public int lodDataResolution;
            public int geometryDownSampleFactor;
            public int lodCount;
            public float extentsSizeMultiplier;
            public float teleportThreshold;
            public bool createSeaFloorDepthData;
            public bool createFoamSim;
            public bool createDynamicWaveSim;
            public bool createFlowSim;
            public bool createShadowData;
            public bool createClipSurfaceData;
            public bool createAlbedoData;
            public string legacyMaterialAssetPath;
        }

        [Serializable]
        private sealed class AnimatedWavesDump
        {
            public float waveResolutionMultiplier;
            public float attenuationInShallows;
            public float shallowsMaximumDepth;
            public int collisionSource;
            public int maxQueryCount;
            public bool pingPongCombinePass;
        }

        [Serializable]
        private sealed class FoamDump
        {
            public bool prewarm;
            public float foamFadeRate;
            public float waveFoamStrength;
            public float waveFoamCoverage;
            public int filterWaves;
            public float shorelineFoamMaxDepth;
            public float shorelineFoamStrength;
            public float simulationFrequency;
        }

        [Serializable]
        private sealed class ShapeFftDump
        {
            public string objectPath;
            public float waveDirectionHeadingAngle;
            public bool overrideGlobalWindSpeed;
            public float windSpeed;
            public float respectShallowWaterAttenuation;
            public int resolution;
            public bool evaluateSpectrumAtRuntimeEveryFrame;
            public float windTurbulence;
            public float maxVerticalDisplacement;
            public float maxHorizontalDisplacement;
            public float timeLoopLength;
            public string spectrumAssetPath;
        }

        [Serializable]
        private sealed class WaveSpectrumDump
        {
            public float waveDirectionVariance;
            public float gravityScale;
            public float multiplier;
            public float chop;
            public float[] powerLogarithmicScales;
            public bool[] powerDisabled;
            public float[] chopScales;
            public float[] gravityScales;
            public int model;
        }

        [Serializable]
        private sealed class VisualDump
        {
            public string legacyMaterialAssetPath;
            public Color depthFogDensity;
            public Color diffuse;
            public Color subSurface;
            public Color subSurfaceColour;
            public Color subSurfaceShallowColour;
            public float transparency;
            public float refractionStrength;
            public Color crest5Absorption;
            public Color crest5Scattering;
            public Color crest5AbsorptionColor;
        }

        [MenuItem("Tools/Hecton8/Crest/Dump Crest 4 Settings")]
        private static void DumpCrest4SettingsMenu()
        {
            if (!TryDumpCrest4Settings(out string message))
            {
                Debug.LogError(message);
                return;
            }

            Debug.Log(message);
        }

        [MenuItem("Tools/Hecton8/Crest/Build Crest 5 Parallel Scene")]
        private static void BuildParallelSceneMenu()
        {
            if (!TryBuildParallelScene(out string message))
            {
                Debug.LogError(message);
                return;
            }

            Debug.Log(message);
        }

        private static bool TryDumpCrest4Settings(out string message)
        {
            EnsureMigrationFolderExists();

            Scene sourceScene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
            if (!sourceScene.IsValid())
            {
                message = $"Crest migration dump failed. Could not open scene `{SourceScenePath}`.";
                return false;
            }

            OldCrest.OceanRenderer legacyOcean = FindComponentInScene<OldCrest.OceanRenderer>(sourceScene);
            if (legacyOcean == null)
            {
                message = $"Crest migration dump failed. No Crest 4 `OceanRenderer` found in scene `{SourceScenePath}`.";
                return false;
            }

            OldCrest.ShapeFFT legacyShapeFft = legacyOcean.GetComponent<OldCrest.ShapeFFT>();
            if (legacyShapeFft == null)
                legacyShapeFft = legacyOcean.GetComponentInChildren<OldCrest.ShapeFFT>(true);

            Crest4SettingsDump dump = CreateDump(sourceScene, legacyOcean, legacyShapeFft);
            File.WriteAllText(DumpAssetPath, JsonUtility.ToJson(dump, true));
            AssetDatabase.Refresh();

            message = $"Crest 4 dump written to `{DumpAssetPath}`.";
            return true;
        }

        private static bool TryBuildParallelScene(out string message)
        {
            if (!TryDumpCrest4Settings(out message))
                return false;

            EnsureParallelSceneExists();

            Crest4SettingsDump dump = LoadDump();
            if (dump == null)
            {
                message = $"Crest 5 migration failed. Could not read dump `{DumpAssetPath}`.";
                return false;
            }

            Scene targetScene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);
            if (!targetScene.IsValid())
            {
                message = $"Crest 5 migration failed. Could not open scene `{TargetScenePath}`.";
                return false;
            }

            OldCrest.OceanRenderer legacyOcean = FindComponentInScene<OldCrest.OceanRenderer>(targetScene);
            if (legacyOcean != null)
            {
                legacyOcean.gameObject.SetActive(false);
                EditorUtility.SetDirty(legacyOcean.gameObject);
            }

            GameObject crest5Root = FindSceneObjectByName(targetScene, Crest5WaterObjectName);
            if (crest5Root == null)
                crest5Root = new GameObject(Crest5WaterObjectName);

            if (legacyOcean != null)
            {
                crest5Root.transform.SetPositionAndRotation(legacyOcean.transform.position, legacyOcean.transform.rotation);
                crest5Root.transform.localScale = legacyOcean.transform.localScale;
            }

            NewCrest.WaterRenderer water = crest5Root.GetComponent<NewCrest.WaterRenderer>();
            if (water == null)
                water = crest5Root.AddComponent<NewCrest.WaterRenderer>();

            NewCrest.ShapeFFT crest5Shape = crest5Root.GetComponent<NewCrest.ShapeFFT>();
            if (crest5Shape == null)
                crest5Shape = crest5Root.AddComponent<NewCrest.ShapeFFT>();

            ApplyDumpToWaterRenderer(targetScene, dump, water, crest5Shape);

            EditorUtility.SetDirty(crest5Root);
            EditorUtility.SetDirty(water);
            EditorUtility.SetDirty(crest5Shape);
            EditorSceneManager.MarkSceneDirty(targetScene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(targetScene);

            message = $"Crest 5 parallel scene prepared at `{TargetScenePath}`.";
            return true;
        }

        private static Crest4SettingsDump CreateDump(Scene sourceScene, OldCrest.OceanRenderer legacyOcean, OldCrest.ShapeFFT legacyShapeFft)
        {
            SerializedObject oceanObject = new SerializedObject(legacyOcean);
            Crest4SettingsDump dump = new()
            {
                generatedUtc = DateTime.UtcNow.ToString("O"),
                sourceScenePath = sourceScene.path,
                sourceOceanObjectPath = GetHierarchyPath(legacyOcean.transform)
            };

            dump.ocean.cameraPath = GetHierarchyPath(ReadObjectReference<Camera>(oceanObject, "_camera")?.transform);
            dump.ocean.viewpointPath = GetHierarchyPath(ReadObjectReference<Transform>(oceanObject, "_viewpoint"));
            dump.ocean.primaryLightPath = GetHierarchyPath(ReadObjectReference<Light>(oceanObject, "_primaryLight")?.transform);
            dump.ocean.globalWindSpeed = ReadFloat(oceanObject, "_globalWindSpeed");
            dump.ocean.overrideGravity = ReadBool(oceanObject, "_overrideGravity");
            dump.ocean.gravity = ReadFloat(oceanObject, "_gravity");
            dump.ocean.gravityMultiplier = ReadFloat(oceanObject, "_gravityMultiplier");
            dump.ocean.layer = ReadInt(oceanObject, "_layer");
            dump.ocean.minScale = ReadFloat(oceanObject, "_minScale");
            dump.ocean.maxScale = ReadFloat(oceanObject, "_maxScale");
            dump.ocean.dropDetailHeightBasedOnWaves = ReadFloat(oceanObject, "_dropDetailHeightBasedOnWaves");
            dump.ocean.lodDataResolution = ReadInt(oceanObject, "_lodDataResolution");
            dump.ocean.geometryDownSampleFactor = ReadInt(oceanObject, "_geometryDownSampleFactor");
            dump.ocean.lodCount = ReadInt(oceanObject, "_lodCount");
            dump.ocean.extentsSizeMultiplier = ReadFloat(oceanObject, "_extentsSizeMultiplier");
            dump.ocean.teleportThreshold = ReadFloat(oceanObject, "_teleportThreshold");
            dump.ocean.createSeaFloorDepthData = ReadBool(oceanObject, "_createSeaFloorDepthData");
            dump.ocean.createFoamSim = ReadBool(oceanObject, "_createFoamSim");
            dump.ocean.createDynamicWaveSim = ReadBool(oceanObject, "_createDynamicWaveSim");
            dump.ocean.createFlowSim = ReadBool(oceanObject, "_createFlowSim");
            dump.ocean.createShadowData = ReadBool(oceanObject, "_createShadowData");
            dump.ocean.createClipSurfaceData = ReadBool(oceanObject, "_createClipSurfaceData");
            dump.ocean.createAlbedoData = ReadBool(oceanObject, "_createAlbedoData");
            dump.ocean.legacyMaterialAssetPath = AssetDatabase.GetAssetPath(ReadObjectReference<Material>(oceanObject, "_material"));
            PopulateVisualDump(dump.visuals, dump.ocean.legacyMaterialAssetPath);

            OldCrest.SimSettingsAnimatedWaves animatedWavesSettings = ReadObjectReference<OldCrest.SimSettingsAnimatedWaves>(oceanObject, "_simSettingsAnimatedWaves");
            if (animatedWavesSettings != null)
            {
                SerializedObject animatedWavesObject = new SerializedObject(animatedWavesSettings);
                dump.animatedWaves.waveResolutionMultiplier = ReadFloat(animatedWavesObject, "_waveResolutionMultiplier");
                dump.animatedWaves.attenuationInShallows = ReadFloat(animatedWavesObject, "_attenuationInShallows");
                dump.animatedWaves.shallowsMaximumDepth = ReadFloat(animatedWavesObject, "_shallowsMaxDepth");
                dump.animatedWaves.collisionSource = ReadInt(animatedWavesObject, "_collisionSource");
                dump.animatedWaves.maxQueryCount = ReadInt(animatedWavesObject, "_maxQueryCount");
                dump.animatedWaves.pingPongCombinePass = ReadBool(animatedWavesObject, "_pingPongCombinePass");
            }

            OldCrest.SimSettingsFoam foamSettings = ReadObjectReference<OldCrest.SimSettingsFoam>(oceanObject, "_simSettingsFoam");
            if (foamSettings != null)
            {
                SerializedObject foamObject = new SerializedObject(foamSettings);
                dump.foam.prewarm = ReadBool(foamObject, "_prewarm");
                dump.foam.foamFadeRate = ReadFloat(foamObject, "_foamFadeRate");
                dump.foam.waveFoamStrength = ReadFloat(foamObject, "_waveFoamStrength");
                dump.foam.waveFoamCoverage = ReadFloat(foamObject, "_waveFoamCoverage");
                dump.foam.filterWaves = ReadInt(foamObject, "_filterWaves");
                dump.foam.shorelineFoamMaxDepth = ReadFloat(foamObject, "_shorelineFoamMaxDepth");
                dump.foam.shorelineFoamStrength = ReadFloat(foamObject, "_shorelineFoamStrength");
                dump.foam.simulationFrequency = ReadFloat(foamObject, "_simulationFrequency");
            }

            if (legacyShapeFft == null)
                return dump;

            SerializedObject shapeObject = new SerializedObject(legacyShapeFft);
            dump.shapeFft.objectPath = GetHierarchyPath(legacyShapeFft.transform);
            dump.shapeFft.waveDirectionHeadingAngle = ReadFloat(shapeObject, "_waveDirectionHeadingAngle");
            dump.shapeFft.overrideGlobalWindSpeed = ReadBool(shapeObject, "_overrideGlobalWindSpeed");
            dump.shapeFft.windSpeed = ReadFloat(shapeObject, "_windSpeed");
            dump.shapeFft.respectShallowWaterAttenuation = ReadFloat(shapeObject, "_respectShallowWaterAttenuation");
            dump.shapeFft.resolution = ReadInt(shapeObject, "_resolution");
            dump.shapeFft.evaluateSpectrumAtRuntimeEveryFrame = !ReadBool(shapeObject, "_spectrumFixedAtRuntime");
            dump.shapeFft.windTurbulence = ReadFloat(shapeObject, "_windTurbulence");
            dump.shapeFft.maxVerticalDisplacement = ReadFloat(shapeObject, "_maxVerticalDisplacement");
            dump.shapeFft.maxHorizontalDisplacement = ReadFloat(shapeObject, "_maxHorizontalDisplacement");
            dump.shapeFft.timeLoopLength = ReadFloat(shapeObject, "_timeLoopLength");

            OldCrest.OceanWaveSpectrum legacySpectrum = ReadObjectReference<OldCrest.OceanWaveSpectrum>(shapeObject, "_spectrum");
            if (legacySpectrum == null)
                return dump;

            dump.shapeFft.spectrumAssetPath = AssetDatabase.GetAssetPath(legacySpectrum);

            SerializedObject spectrumObject = new SerializedObject(legacySpectrum);
            dump.waveSpectrum.waveDirectionVariance = ReadFloat(spectrumObject, "_waveDirectionVariance");
            dump.waveSpectrum.gravityScale = ReadFloat(spectrumObject, "_gravityScale");
            dump.waveSpectrum.multiplier = ReadFloat(spectrumObject, "_multiplier");
            dump.waveSpectrum.chop = ReadFloat(spectrumObject, "_chop");
            dump.waveSpectrum.powerLogarithmicScales = ReadFloatArray(spectrumObject, "_powerLog");
            dump.waveSpectrum.powerDisabled = ReadBoolArray(spectrumObject, "_powerDisabled");
            dump.waveSpectrum.chopScales = ReadFloatArray(spectrumObject, "_chopScales");
            dump.waveSpectrum.gravityScales = ReadFloatArray(spectrumObject, "_gravityScales");
            dump.waveSpectrum.model = ReadInt(spectrumObject, "_model");
            dump.collisionMigrationNote = BuildCollisionMigrationNote(dump.animatedWaves.collisionSource);

            return dump;
        }

        private static void ApplyDumpToWaterRenderer(Scene scene, Crest4SettingsDump dump, NewCrest.WaterRenderer water, NewCrest.ShapeFFT crest5Shape)
        {
            water.WindSpeed = dump.ocean.globalWindSpeed;
            water.OverrideGravity = dump.ocean.overrideGravity;
            water.GravityOverride = dump.ocean.gravity;
            water.GravityMultiplier = dump.ocean.gravityMultiplier;
            water.Layer = dump.ocean.layer;
            water.ScaleRange = new Vector2(dump.ocean.minScale, dump.ocean.maxScale);
            water.DropDetailHeightBasedOnWaves = dump.ocean.dropDetailHeightBasedOnWaves;
            water.LodResolution = dump.ocean.lodDataResolution;
            water.GeometryDownSampleFactor = dump.ocean.geometryDownSampleFactor;
            water.LodLevels = dump.ocean.lodCount;
            water.ExtentsSizeMultiplier = dump.ocean.extentsSizeMultiplier;
            water.TeleportThreshold = dump.ocean.teleportThreshold;
            water.Viewer = FindComponentAtPath<Camera>(scene, dump.ocean.cameraPath);
            water.PrimaryLight = FindComponentAtPath<Light>(scene, dump.ocean.primaryLightPath);
            Material surfaceMaterial = AssetDatabase.LoadAssetAtPath<Material>(Crest5WaterMaterialPath);
            Material volumeMaterial = AssetDatabase.LoadAssetAtPath<Material>(Crest5WaterVolumeMaterialPath);
            water.Material = surfaceMaterial;

            SerializedObject waterObject = new SerializedObject(water);
            SetObjectReference(waterObject, "_Viewpoint", FindTransformByHierarchyPath(scene, dump.ocean.viewpointPath));
            SetObjectReference(waterObject, "_VolumeMaterial", volumeMaterial);
            SetBool(waterObject, "_DepthLod._Enabled", dump.ocean.createSeaFloorDepthData);
            SetBool(waterObject, "_FoamLod._Enabled", dump.ocean.createFoamSim);
            SetBool(waterObject, "_DynamicWavesLod._Enabled", dump.ocean.createDynamicWaveSim);
            SetBool(waterObject, "_FlowLod._Enabled", dump.ocean.createFlowSim);
            SetBool(waterObject, "_ShadowLod._Enabled", dump.ocean.createShadowData);
            SetBool(waterObject, "_ClipLod._Enabled", dump.ocean.createClipSurfaceData);
            SetBool(waterObject, "_AlbedoLod._Enabled", dump.ocean.createAlbedoData);
            SetFloat(waterObject, "_AnimatedWavesLod._WaveResolutionMultiplier", dump.animatedWaves.waveResolutionMultiplier);
            SetFloat(waterObject, "_AnimatedWavesLod._AttenuationInShallows", dump.animatedWaves.attenuationInShallows);
            SetFloat(waterObject, "_AnimatedWavesLod._ShallowsMaximumDepth", dump.animatedWaves.shallowsMaximumDepth);
            SetInt(waterObject, "_AnimatedWavesLod._CollisionSource", (int)MapCollisionSource(dump.animatedWaves.collisionSource));
            SetInt(waterObject, "_AnimatedWavesLod._MaximumQueryCount", Mathf.Max(1, dump.animatedWaves.maxQueryCount));
            waterObject.ApplyModifiedPropertiesWithoutUndo();
            ApplyLegacyVisualSettings(surfaceMaterial, volumeMaterial, dump.visuals);

            crest5Shape.OverrideGlobalWindSpeed = dump.shapeFft.overrideGlobalWindSpeed;
            crest5Shape.WindSpeed = dump.shapeFft.windSpeed;
            crest5Shape.WaveDirectionHeadingAngle = dump.shapeFft.waveDirectionHeadingAngle;
            crest5Shape.RespectShallowWaterAttenuation = dump.shapeFft.respectShallowWaterAttenuation;
            crest5Shape.Resolution = dump.shapeFft.resolution;
            crest5Shape.EvaluateSpectrumAtRunTimeEveryFrame = dump.shapeFft.evaluateSpectrumAtRuntimeEveryFrame;
            crest5Shape.WindTurbulence = dump.shapeFft.windTurbulence;
            crest5Shape.MaximumVerticalDisplacement = dump.shapeFft.maxVerticalDisplacement;
            crest5Shape.MaximumHorizontalDisplacement = dump.shapeFft.maxHorizontalDisplacement;
            crest5Shape.TimeLoopLength = dump.shapeFft.timeLoopLength;
            crest5Shape.Spectrum = CreateOrUpdateWaveSpectrumAsset(dump.waveSpectrum);

            NewCrest.FoamLodSettings foamSettings = CreateOrUpdateFoamSettingsAsset(dump.foam);
            water.FoamLod.Prewarm = dump.foam.prewarm;
            water.FoamLod.Settings = foamSettings;
            water.FoamLod.SimulationFrequency = Mathf.Max(1, Mathf.RoundToInt(dump.foam.simulationFrequency));
        }

        private static void PopulateVisualDump(VisualDump dump, string materialPath)
        {
            dump.legacyMaterialAssetPath = materialPath;

            Material legacyMaterial = string.IsNullOrWhiteSpace(materialPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<Material>(materialPath);

            if (legacyMaterial == null)
                return;

            dump.depthFogDensity = ReadMaterialColor(legacyMaterial, "_DepthFogDensity", new Color(0.12f, 0.12f, 0.085f, 0f));
            dump.diffuse = ReadMaterialColor(legacyMaterial, "_Diffuse", new Color(0.08f, 0.15f, 0.22f, 1f));
            dump.subSurface = ReadMaterialColor(legacyMaterial, "_SubSurface", new Color(0f, 0.48f, 0.36f, 1f));
            dump.subSurfaceColour = ReadMaterialColor(legacyMaterial, "_SubSurfaceColour", new Color(0.27f, 0.41f, 0.43f, 1f));
            dump.subSurfaceShallowColour = ReadMaterialColor(legacyMaterial, "_SubSurfaceShallowColour", new Color(0.34f, 0.78f, 0.74f, 1f));
            dump.transparency = ReadMaterialFloat(legacyMaterial, "_Transparency", 1f);
            dump.refractionStrength = ReadMaterialFloat(legacyMaterial, "_RefractionStrength", 1f);

            Vector3 derivedAbsorption = new Vector3(dump.depthFogDensity.r, dump.depthFogDensity.g, dump.depthFogDensity.b) * 0.5f;
            Vector3 derivedScattering =
                new Vector3(dump.diffuse.r, dump.diffuse.g, dump.diffuse.b) * 0.5f +
                new Vector3(dump.subSurface.r, dump.subSurface.g, dump.subSurface.b) * 0.2f;
            dump.crest5Absorption = new Color(derivedAbsorption.x, derivedAbsorption.y, derivedAbsorption.z, 0f);
            dump.crest5Scattering = new Color(derivedScattering.x, derivedScattering.y, derivedScattering.z, 1f);
            dump.crest5AbsorptionColor = new Color(
                dump.subSurfaceShallowColour.r,
                dump.subSurfaceShallowColour.g,
                dump.subSurfaceShallowColour.b,
                Mathf.Clamp01(dump.transparency) * 0.01f);
        }

        private static void ApplyLegacyVisualSettings(Material surfaceMaterial, Material volumeMaterial, VisualDump visuals)
        {
            ApplyLegacyVisualSettingsToMaterial(surfaceMaterial, visuals);
            ApplyLegacyVisualSettingsToMaterial(volumeMaterial, visuals);

            if (surfaceMaterial != null)
                EditorUtility.SetDirty(surfaceMaterial);

            if (volumeMaterial != null)
                EditorUtility.SetDirty(volumeMaterial);
        }

        private static void ApplyLegacyVisualSettingsToMaterial(Material material, VisualDump visuals)
        {
            if (material == null)
                return;

            SetMaterialColor(material, "_Crest_Absorption", visuals.crest5Absorption);
            SetMaterialColor(material, "_Crest_Scattering", visuals.crest5Scattering);
            SetMaterialColor(material, "_Crest_AbsorptionColor", visuals.crest5AbsorptionColor);
            SetMaterialFloat(material, "_Crest_RefractionStrength", visuals.refractionStrength);
        }

        private static NewCrest.WaveSpectrum CreateOrUpdateWaveSpectrumAsset(WaveSpectrumDump dump)
        {
            EnsureMigrationFolderExists();

            NewCrest.WaveSpectrum asset = AssetDatabase.LoadAssetAtPath<NewCrest.WaveSpectrum>(SpectrumAssetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<NewCrest.WaveSpectrum>();
                AssetDatabase.CreateAsset(asset, SpectrumAssetPath);
            }

            SerializedObject serializedObject = new SerializedObject(asset);
            SetFloat(serializedObject, "_WaveDirectionVariance", dump.waveDirectionVariance);
            SetFloat(serializedObject, "_GravityScale", dump.gravityScale);
            SetFloat(serializedObject, "_Multiplier", dump.multiplier);
            SetFloat(serializedObject, "_Chop", dump.chop);
            SetFloatArray(serializedObject, "_PowerLogarithmicScales", dump.powerLogarithmicScales);
            SetBoolArray(serializedObject, "_PowerDisabled", dump.powerDisabled);
            SetFloatArray(serializedObject, "_ChopScales", dump.chopScales);
            SetFloatArray(serializedObject, "_GravityScales", dump.gravityScales);
            SetInt(serializedObject, "_Model", dump.model);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static NewCrest.FoamLodSettings CreateOrUpdateFoamSettingsAsset(FoamDump dump)
        {
            EnsureMigrationFolderExists();

            NewCrest.FoamLodSettings asset = AssetDatabase.LoadAssetAtPath<NewCrest.FoamLodSettings>(FoamSettingsAssetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<NewCrest.FoamLodSettings>();
                AssetDatabase.CreateAsset(asset, FoamSettingsAssetPath);
            }

            asset.FoamFadeRate = dump.foamFadeRate;
            asset.WaveFoamStrength = dump.waveFoamStrength;
            asset.WaveFoamCoverage = dump.waveFoamCoverage;
            asset.FilterWaves = dump.filterWaves;
            asset.ShorelineFoamMaximumDepth = dump.shorelineFoamMaxDepth;
            asset.ShorelineFoamStrength = dump.shorelineFoamStrength;
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void EnsureParallelSceneExists()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetScenePath) != null)
                return;

            EnsureMigrationFolderExists();
            AssetDatabase.CopyAsset(SourceScenePath, TargetScenePath);
            AssetDatabase.Refresh();
        }

        private static void EnsureMigrationFolderExists()
        {
            string absolutePath = Path.GetFullPath(MigrationDataFolder);
            if (!Directory.Exists(absolutePath))
                Directory.CreateDirectory(absolutePath);
        }

        private static Crest4SettingsDump LoadDump()
        {
            if (!File.Exists(DumpAssetPath))
                return null;

            return JsonUtility.FromJson<Crest4SettingsDump>(File.ReadAllText(DumpAssetPath));
        }

        private static string BuildCollisionMigrationNote(int legacyCollisionSource)
        {
            return legacyCollisionSource switch
            {
                0 => "Legacy collision source NONE mapped to Crest 5 NONE.",
                1 => "Legacy collision source GERSTNER CPU has no direct Crest 5 equivalent. Migrated to Crest 5 GPU queries.",
                2 => "Legacy collision source COMPUTE SHADER QUERIES mapped to Crest 5 GPU queries.",
                3 => "Legacy collision source BAKED FFT was not migrated with baked data. Migrated to Crest 5 GPU queries.",
                _ => "Legacy collision source was unknown. Migrated to Crest 5 GPU queries."
            };
        }

        private static NewCrest.CollisionSource MapCollisionSource(int legacyCollisionSource)
        {
            return legacyCollisionSource switch
            {
                0 => NewCrest.CollisionSource.None,
                2 => NewCrest.CollisionSource.GPU,
                _ => NewCrest.CollisionSource.GPU
            };
        }

        private static T FindComponentInScene<T>(Scene scene) where T : Component
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                T component = roots[i].GetComponentInChildren<T>(true);
                if (component != null)
                    return component;
            }

            return null;
        }

        private static GameObject FindSceneObjectByName(Scene scene, string objectName)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root.name == objectName)
                    return root;

                Transform[] children = root.GetComponentsInChildren<Transform>(true);
                for (int childIndex = 0; childIndex < children.Length; childIndex++)
                {
                    Transform child = children[childIndex];
                    if (child.name == objectName)
                        return child.gameObject;
                }
            }

            return null;
        }

        private static T FindComponentAtPath<T>(Scene scene, string hierarchyPath) where T : Component
        {
            Transform transform = FindTransformByHierarchyPath(scene, hierarchyPath);
            return transform != null ? transform.GetComponent<T>() : null;
        }

        private static Transform FindTransformByHierarchyPath(Scene scene, string hierarchyPath)
        {
            if (string.IsNullOrWhiteSpace(hierarchyPath))
                return null;

            string[] pathParts = hierarchyPath.Split('/');
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (!string.Equals(roots[i].name, pathParts[0], StringComparison.Ordinal))
                    continue;

                Transform current = roots[i].transform;
                bool matched = true;
                for (int partIndex = 1; partIndex < pathParts.Length; partIndex++)
                {
                    Transform next = current.Find(pathParts[partIndex]);
                    if (next == null)
                    {
                        matched = false;
                        break;
                    }

                    current = next;
                }

                if (matched)
                    return current;
            }

            return null;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
                return string.Empty;

            string path = transform.name;
            Transform current = transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        private static T ReadObjectReference<T>(SerializedObject serializedObject, string propertyPath) where T : UnityEngine.Object
        {
            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            return property != null ? property.objectReferenceValue as T : null;
        }

        private static float ReadFloat(SerializedObject serializedObject, string propertyPath)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            return property != null ? property.floatValue : 0f;
        }

        private static int ReadInt(SerializedObject serializedObject, string propertyPath)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            return property != null ? property.intValue : 0;
        }

        private static bool ReadBool(SerializedObject serializedObject, string propertyPath)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            return property != null && property.boolValue;
        }

        private static Color ReadMaterialColor(Material material, string propertyName, Color fallback)
        {
            return material != null && material.HasProperty(propertyName) ? material.GetColor(propertyName) : fallback;
        }

        private static float ReadMaterialFloat(Material material, string propertyName, float fallback)
        {
            return material != null && material.HasProperty(propertyName) ? material.GetFloat(propertyName) : fallback;
        }

        private static float[] ReadFloatArray(SerializedObject serializedObject, string propertyPath)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            if (property == null || !property.isArray)
                return Array.Empty<float>();

            float[] values = new float[property.arraySize];
            for (int i = 0; i < values.Length; i++)
                values[i] = property.GetArrayElementAtIndex(i).floatValue;

            return values;
        }

        private static bool[] ReadBoolArray(SerializedObject serializedObject, string propertyPath)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            if (property == null || !property.isArray)
                return Array.Empty<bool>();

            bool[] values = new bool[property.arraySize];
            for (int i = 0; i < values.Length; i++)
                values[i] = property.GetArrayElementAtIndex(i).boolValue;

            return values;
        }

        private static void SetObjectReference(SerializedObject serializedObject, string propertyPath, UnityEngine.Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            if (property != null)
                property.objectReferenceValue = value;
        }

        private static void SetBool(SerializedObject serializedObject, string propertyPath, bool value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            if (property != null)
                property.boolValue = value;
        }

        private static void SetInt(SerializedObject serializedObject, string propertyPath, int value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            if (property != null)
                property.intValue = value;
        }

        private static void SetFloat(SerializedObject serializedObject, string propertyPath, float value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            if (property != null)
                property.floatValue = value;
        }

        private static void SetFloatArray(SerializedObject serializedObject, string propertyPath, float[] values)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            if (property == null || !property.isArray)
                return;

            property.arraySize = values != null ? values.Length : 0;
            for (int i = 0; i < property.arraySize; i++)
                property.GetArrayElementAtIndex(i).floatValue = values[i];
        }

        private static void SetBoolArray(SerializedObject serializedObject, string propertyPath, bool[] values)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            if (property == null || !property.isArray)
                return;

            property.arraySize = values != null ? values.Length : 0;
            for (int i = 0; i < property.arraySize; i++)
                property.GetArrayElementAtIndex(i).boolValue = values[i];
        }

        private static void SetMaterialColor(Material material, string propertyName, Color value)
        {
            if (material != null && material.HasProperty(propertyName))
                material.SetColor(propertyName, value);
        }

        private static void SetMaterialFloat(Material material, string propertyName, float value)
        {
            if (material != null && material.HasProperty(propertyName))
                material.SetFloat(propertyName, value);
        }
    }
}
