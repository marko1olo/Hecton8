#if UNITY_EDITOR
using Hecton8.Gameplay;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    public sealed class DataMiningTunerWindow : EditorWindow
    {
        private const string WindowTitle = "Data Mining Tuner";
        private static readonly Color ConeColor = new Color(1f, 0.88f, 0.12f, 0.9f);
        private static readonly Color LineColor = new Color(1f, 0.1f, 0.05f, 0.95f);
        private static readonly Color HitColor = new Color(0.15f, 0.45f, 1f, 0.9f);

        [MenuItem("Hecton-8/Scanner/Data Mining Tuner")]
        public static void Open()
        {
            GetWindow<DataMiningTunerWindow>(WindowTitle);
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui -= DrawScannerGizmos;
            SceneView.duringSceneGui += DrawScannerGizmos;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawScannerGizmos;
        }

        private void OnGUI()
        {
            ScannerSettingsDTO settings = ScannerDataMiningRouter.TryReadVaultSettings(out ScannerSettingsDTO vaultSettings)
                ? vaultSettings
                : ScannerDataMiningTuning.Settings;
            EditorGUI.BeginChangeCheck();
            settings.MaxDistanceMeters = EditorGUILayout.Slider("Max Scan Distance", settings.MaxDistanceMeters, 4f, 160f);
            settings.BeamRadiusMeters = EditorGUILayout.Slider("Beam Radius", settings.BeamRadiusMeters, 0.05f, 4f);
            settings.BeamMagnetism = EditorGUILayout.Slider("Beam Magnetism", settings.BeamMagnetism, 0f, 32f);
            settings.BeamMinDot = EditorGUILayout.Slider("Min Forward Dot", settings.BeamMinDot, 0.8f, 0.999f);
            settings.ProgressDecayRate = EditorGUILayout.Slider("Progress Decay Rate", settings.ProgressDecayRate, 0f, 3f);
            settings.ScanDurationFallback = EditorGUILayout.Slider("Fallback Scan Seconds", settings.ScanDurationFallback, 0.1f, 8f);
            settings.SdfMidpointClearance = EditorGUILayout.Slider("SDF Clearance", settings.SdfMidpointClearance, -0.5f, 1f);
            settings.LowTierCadenceFrames = EditorGUILayout.IntSlider("Low Cadence Frames", settings.LowTierCadenceFrames, 1, 12);
            settings.MidTierCadenceFrames = EditorGUILayout.IntSlider("Mid Cadence Frames", settings.MidTierCadenceFrames, 1, 8);
            settings.HighTierCadenceFrames = EditorGUILayout.IntSlider("High Cadence Frames", settings.HighTierCadenceFrames, 1, 4);
            settings.UltraTierCadenceFrames = EditorGUILayout.IntSlider("Ultra Cadence Frames", settings.UltraTierCadenceFrames, 1, 4);
            settings.MaxCandidateCells = EditorGUILayout.IntSlider("Candidate Cells", settings.MaxCandidateCells, 1, 81);
            settings.MaxCandidatesPerCell = EditorGUILayout.IntSlider("Candidates Per Cell", settings.MaxCandidatesPerCell, 1, 64);
            if (EditorGUI.EndChangeCheck())
            {
                ScannerDataMiningTuning.Settings = settings;
                ScannerDataMiningRouter.TryWriteVaultSettings(in settings);
                SceneView.RepaintAll();
            }
        }

        private static void DrawScannerGizmos(SceneView sceneView)
        {
            ScannerDataMiningRouter router = UnityEngine.Object.FindFirstObjectByType<ScannerDataMiningRouter>();
            if (router == null)
                return;

            ScannerSettingsDTO settings = ScannerDataMiningRouter.TryReadVaultSettings(out ScannerSettingsDTO vaultSettings)
                ? vaultSettings
                : ScannerDataMiningTuning.Settings;
            Transform routerTransform = router.transform;
            Vector3 origin = routerTransform.position;
            Vector3 forward = routerTransform.forward;
            float radius = math.max(0.2f, settings.BeamRadiusMeters);
            Vector3 end = origin + forward * settings.MaxDistanceMeters;
            Vector3 right = routerTransform.right * radius;
            Vector3 up = routerTransform.up * radius;

            Handles.color = ConeColor;
            Handles.DrawWireDisc(end, forward, radius);
            Handles.DrawLine(origin, end + right);
            Handles.DrawLine(origin, end - right);
            Handles.DrawLine(origin, end + up);
            Handles.DrawLine(origin, end - up);

            if (!ScannerDataMiningRouter.TryGetLastVfxTarget(out ScannerVfxDTO target, out _))
                return;

            float3 local = target.HitAUP;
            Vector3 hit = new Vector3(local.x, local.y, local.z);
            Handles.color = LineColor;
            Handles.DrawLine(origin, hit, 2f);
            Handles.color = HitColor;
            Handles.SphereHandleCap(0, hit, Quaternion.identity, radius, EventType.Repaint);
        }
    }
}
#endif
