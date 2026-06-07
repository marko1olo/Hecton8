#if UNITY_EDITOR
using System.Globalization;
using System.IO;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    public sealed class WfcBuilderTunerWindow : EditorWindow
    {
        private float _gridSnapSize;
        private float _maxBaseBounds;
        private float _terrainClearanceMargin;
        private bool _loaded;

        [MenuItem("Hecton8/Construction/WFC Builder Tuner")]
        public static void Open()
        {
            GetWindow<WfcBuilderTunerWindow>("WFC Builder Tuner");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui -= DrawSceneGizmos;
            SceneView.duringSceneGui += DrawSceneGizmos;
            LoadSettings();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawSceneGizmos;
        }

        private void OnGUI()
        {
            if (!_loaded)
                LoadSettings();

            EditorGUI.BeginChangeCheck();
            _gridSnapSize = EditorGUILayout.Slider("Grid Snap Size", _gridSnapSize, 1f, 20f);
            _maxBaseBounds = EditorGUILayout.Slider("Max Base Bounds", _maxBaseBounds, 25f, 5000f);
            _terrainClearanceMargin = EditorGUILayout.Slider("Terrain Clearance Margin", _terrainClearanceMargin, 0f, 2f);

            if (EditorGUI.EndChangeCheck())
            {
                ModularBaseConstructionValidator.SetTunerSettings(
                    _gridSnapSize,
                    _maxBaseBounds,
                    _terrainClearanceMargin);
                ModularBaseConstructionValidator.WriteTunerSettingsToVault(GlobalRegistry.DataVault);
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("Reload Vault Settings"))
                LoadSettings();

            if (GUILayout.Button("Load module_bounds.csv"))
                LoadBoundsCsv();

            if (ModularBaseConstructionValidator.TryGetLastValidation(
                    out ConstructionRequestDTO request,
                    out StructuralBoundsDTO _,
                    out ConstructionTerrainSampler _,
                    out ConstructionValidationResultDTO result))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Last Grid", $"{request.GridPos.x}, {request.GridPos.y}, {request.GridPos.z}");
                EditorGUILayout.LabelField("Last Flags", ((ConstructionValidationFlags)result.FailureFlags).ToString());
                EditorGUILayout.LabelField("Min SDF", result.MinSdfDistance.ToString("F3", CultureInfo.InvariantCulture));
                EditorGUILayout.LabelField("Probes", result.ProbeCount.ToString());
            }
        }

        private void LoadSettings()
        {
            ConstructionValidationSettingsDTO settings;
            if (!ModularBaseConstructionValidator.TryReadTunerSettingsFromVault(GlobalRegistry.DataVault, out settings))
                settings = ModularBaseConstructionValidator.GetTunerSettings();

            _gridSnapSize = settings.GridSizeMeters;
            _maxBaseBounds = settings.MaxBaseBoundsMeters;
            _terrainClearanceMargin = settings.TerrainClearanceMargin;
            _loaded = true;
        }

        private static void LoadBoundsCsv()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string path = Path.Combine(projectRoot, "Data", "Construction", "module_bounds.csv");
            if (!File.Exists(path))
            {
                Debug.LogWarning($"WFC Builder Tuner: missing {path}");
                return;
            }

            byte[] csv = File.ReadAllBytes(path);
            bool parsed = ModularBaseConstructionValidator.TryParseModuleBoundsCsvToVault(
                csv,
                GlobalRegistry.DataVault,
                out int written);
            if (!parsed)
            {
                Debug.LogWarning("WFC Builder Tuner: module_bounds.csv parsed zero rows.");
                return;
            }

            Debug.Log($"WFC Builder Tuner: loaded {written} module bounds rows.");
        }

        private static void DrawSceneGizmos(SceneView sceneView)
        {
            if (!ModularBaseConstructionValidator.TryGetLastValidation(
                    out ConstructionRequestDTO request,
                    out StructuralBoundsDTO bounds,
                    out ConstructionTerrainSampler _,
                    out ConstructionValidationResultDTO result))
                return;

            ConstructionValidationSettingsDTO settings = ModularBaseConstructionValidator.GetTunerSettings();
            float gridSize = math.max(settings.GridSizeMeters, 0.001f);
            float3 localCenter = ModularBaseConstructionValidator.GridToLocal(in request, gridSize);
            double3 centerAup = request.RootAUP + new double3(localCenter.x, localCenter.y, localCenter.z);
            Vector3 centerRuntime = HectonFloatingOrigin.ToRuntimePosition(centerAup);

            Handles.color = new Color(1f, 1f, 1f, 0.18f);
            const int gridRadius = 4;
            for (int i = -gridRadius; i <= gridRadius; i++)
            {
                float offset = i * gridSize;
                Handles.DrawLine(
                    centerRuntime + new Vector3(-gridRadius * gridSize, 0f, offset),
                    centerRuntime + new Vector3(gridRadius * gridSize, 0f, offset));
                Handles.DrawLine(
                    centerRuntime + new Vector3(offset, 0f, -gridRadius * gridSize),
                    centerRuntime + new Vector3(offset, 0f, gridRadius * gridSize));
            }

            bool terrainBlocked = (result.FailureFlags & (uint)ConstructionValidationFlags.TerrainIntersection) != 0u;
            if (!terrainBlocked)
                return;

            Handles.color = new Color(1f, 0.05f, 0.02f, 0.85f);
            Vector3 boundsCenter = centerRuntime + (Vector3)bounds.CenterOffset;
            Vector3 boundsSize = (Vector3)(bounds.Extents * 2f);
            Handles.DrawWireCube(boundsCenter, boundsSize);
        }
    }
}
#endif
