#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Hecton8.Physics;
using Hecton8.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Editor
{
    internal static class CrestWorldWaterLevelCalibrationInstaller
    {
        private const string CrestPrefabPath = "Assets/_Project/Prefabs/Ocean_Crest.prefab";
        private const string WorldScenePath = "Assets/_Project/Scenes/02_HECTON_WORLD.unity";
        private const string CalibrationArtifactPath =
            "Docs/GeneratedAssets/Terrain/MacroGeology/WorldWaterLevelCalibration_Extent30000m_Res192.json";
        private const string WaterLevelRegexSuffix = "\\s*:\\s*\\[\\s*\\{[\\s\\S]*?\"waterLevelMeters\"\\s*:\\s*(-?[0-9]+(?:\\.[0-9]+)?)";

        [MenuItem("HECTON-8/Water/Install Crest Water-Level Calibration Prefab")]
        internal static void InstallPrefabCalibration()
        {
            if (!CanMutateEditorAssets(out string failure))
                throw new InvalidOperationException(failure);

            float waterLevelY = ResolveCalibratedWaterLevelY();
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(CrestPrefabPath);
            try
            {
                EnsureCrestWaterLevelCalibration(prefabRoot, waterLevelY);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, CrestPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            AssetDatabase.SaveAssets();
        }

        [MenuItem("HECTON-8/Water/Install Crest Ocean In World Scene")]
        internal static void InstallWorldSceneCalibration()
        {
            if (!CanMutateEditorAssets(out string failure))
                throw new InvalidOperationException(failure);

            InstallPrefabCalibration();
            Scene scene = EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Single);
            global::Crest.OceanRenderer ocean = FindOceanInScene(scene);
            if (ocean == null)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CrestPrefabPath);
                if (prefab == null)
                    throw new FileNotFoundException(CrestPrefabPath);

                GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
                if (instance == null)
                    throw new InvalidOperationException("Failed to instantiate " + CrestPrefabPath);

                instance.name = "Ocean_Crest";
                ocean = instance.GetComponentInChildren<global::Crest.OceanRenderer>(true);
            }

            if (ocean == null)
                throw new InvalidOperationException("World scene has no Crest OceanRenderer after install.");

            EnsureCrestWaterLevelCalibration(ocean.gameObject, ResolveCalibratedWaterLevelY());
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("HECTON-8/Water/Validate Crest Water-Level Calibration")]
        internal static void ValidatePrefabCalibrationMenu()
        {
            if (!VerifyPrefabCalibration(out string failure))
                throw new InvalidOperationException(failure);

            Debug.Log("[HECTON-8 Water] Crest water-level calibration route is valid.");
        }

        internal static bool VerifyPrefabCalibration(out string failure)
        {
            failure = null;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CrestPrefabPath);
            if (prefab == null)
            {
                failure = "Missing Crest ocean prefab: " + CrestPrefabPath;
                return false;
            }

            global::Crest.OceanRenderer ocean = prefab.GetComponentInChildren<global::Crest.OceanRenderer>(true);
            if (ocean == null)
            {
                failure = CrestPrefabPath + " has no Crest OceanRenderer.";
                return false;
            }

            Crest4KinematicsAdapter kinematics = prefab.GetComponentInChildren<Crest4KinematicsAdapter>(true);
            if (kinematics == null)
            {
                failure = CrestPrefabPath + " has no Crest4KinematicsAdapter.";
                return false;
            }

            WorldWaterLevelCalibrationAuthoring calibration =
                prefab.GetComponentInChildren<WorldWaterLevelCalibrationAuthoring>(true);
            if (calibration == null)
            {
                failure = CrestPrefabPath + " has no WorldWaterLevelCalibrationAuthoring.";
                return false;
            }

            Transform root = ocean.Root != null ? ocean.Root : ocean.transform;
            float resolvedWaterY = calibration.ResolvedWaterLevelY;
            if (!Mathf.Approximately(root.position.y, resolvedWaterY))
            {
                failure = CrestPrefabPath + " root Y " + root.position.y.ToString(CultureInfo.InvariantCulture) +
                          " does not match calibration waterY " + resolvedWaterY.ToString(CultureInfo.InvariantCulture);
                return false;
            }

            return true;
        }

        private static void EnsureCrestWaterLevelCalibration(GameObject root, float waterLevelY)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));

            global::Crest.OceanRenderer ocean = root.GetComponentInChildren<global::Crest.OceanRenderer>(true);
            if (ocean == null)
                throw new InvalidOperationException(root.name + " has no Crest OceanRenderer.");

            WorldWaterLevelCalibrationAuthoring calibration =
                ocean.GetComponent<WorldWaterLevelCalibrationAuthoring>() ??
                ocean.gameObject.AddComponent<WorldWaterLevelCalibrationAuthoring>();

            SerializedObject serialized = new SerializedObject(calibration);
            SetInt(serialized, "authoringSeed", WorldWaterLevelCalibrationMath.DefaultAuthoringSeed);
            SetInt(serialized, "runtimeSeed", 0);
            SetString(serialized, "calibrationArtifactRelativePath", CalibrationArtifactPath);
            SetFloat(serialized, "calibratedWaterLevelY", waterLevelY);
            SetFloat(serialized, "fallbackWaterLevelY", WorldWaterLevelCalibrationMath.DefaultWaterLevelY);
            SetFloat(serialized, "calibrationTravelMeters", WorldWaterLevelCalibrationMath.DefaultCalibrationTravelMeters);
            SetObject(serialized, "oceanRenderer", ocean);
            SetObject(serialized, "crestRootOverride", ocean.Root != null ? ocean.Root : ocean.transform);
            SetBool(serialized, "applyOnEnable", true);
            SetBool(serialized, "applyInEditMode", true);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            calibration.TryApplyWaterLevel();
            EditorUtility.SetDirty(calibration);
            EditorUtility.SetDirty(ocean.gameObject);
        }

        private static global::Crest.OceanRenderer FindOceanInScene(Scene scene)
        {
            global::Crest.OceanRenderer[] oceans =
                UnityEngine.Object.FindObjectsByType<global::Crest.OceanRenderer>(FindObjectsInactive.Include);
            for (int i = 0; i < oceans.Length; i++)
            {
                global::Crest.OceanRenderer ocean = oceans[i];
                if (ocean != null && ocean.gameObject.scene == scene)
                    return ocean;
            }

            return null;
        }

        private static float ResolveCalibratedWaterLevelY()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                return WorldWaterLevelCalibrationMath.DefaultWaterLevelY;

            string path = Path.Combine(projectRoot, CalibrationArtifactPath);
            if (!File.Exists(path))
                return WorldWaterLevelCalibrationMath.DefaultWaterLevelY;

            string json = File.ReadAllText(path);
            if (!TryReadWaterLevelFromLane(json, "strictCandidateLevels", out float waterLevelY) &&
                !TryReadWaterLevelFromLane(json, "bestLevels", out waterLevelY) &&
                !TryReadWaterLevelFromLane(json, "allLevels", out waterLevelY))
            {
                return WorldWaterLevelCalibrationMath.DefaultWaterLevelY;
            }

            return WorldWaterLevelCalibrationMath.TryResolveWaterLevelY(
                waterLevelY,
                WorldWaterLevelCalibrationMath.DefaultWaterLevelY,
                WorldWaterLevelCalibrationMath.DefaultCalibrationTravelMeters,
                out float resolvedWaterLevelY)
                ? resolvedWaterLevelY
                : WorldWaterLevelCalibrationMath.DefaultWaterLevelY;
        }

        private static bool TryReadWaterLevelFromLane(string json, string laneName, out float waterLevelY)
        {
            waterLevelY = 0f;
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(laneName))
                return false;

            string pattern = "\"" + Regex.Escape(laneName) + "\"" + WaterLevelRegexSuffix;
            Match match = Regex.Match(json, pattern, RegexOptions.CultureInvariant);
            return match.Success &&
                   float.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out waterLevelY);
        }

        private static bool CanMutateEditorAssets(out string failure)
        {
            failure = null;
            if (Application.isPlaying)
            {
                failure = "Cannot install Crest water-level calibration during Play Mode.";
                return false;
            }

            if (EditorApplication.isCompiling)
            {
                failure = "Cannot install Crest water-level calibration while Unity is compiling.";
                return false;
            }

            if (EditorApplication.isUpdating)
            {
                failure = "Cannot install Crest water-level calibration while Unity is importing/updating.";
                return false;
            }

            return true;
        }

        private static void SetInt(SerializedObject serialized, string path, int value)
        {
            SerializedProperty property = serialized.FindProperty(path);
            if (property != null)
                property.intValue = value;
        }

        private static void SetString(SerializedObject serialized, string path, string value)
        {
            SerializedProperty property = serialized.FindProperty(path);
            if (property != null)
                property.stringValue = value;
        }

        private static void SetFloat(SerializedObject serialized, string path, float value)
        {
            SerializedProperty property = serialized.FindProperty(path);
            if (property != null)
                property.floatValue = value;
        }

        private static void SetBool(SerializedObject serialized, string path, bool value)
        {
            SerializedProperty property = serialized.FindProperty(path);
            if (property != null)
                property.boolValue = value;
        }

        private static void SetObject(SerializedObject serialized, string path, UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(path);
            if (property != null)
                property.objectReferenceValue = value;
        }
    }
}
#endif
