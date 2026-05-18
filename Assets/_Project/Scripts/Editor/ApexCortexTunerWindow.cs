#if UNITY_EDITOR
using Hecton8.AI;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    public sealed class ApexCortexTunerWindow : EditorWindow
    {
        private const int GizmoCapacity = 128;
        private static readonly Vector3[] Origins = new Vector3[GizmoCapacity];
        private static readonly Vector3[] Targets = new Vector3[GizmoCapacity];
        private static readonly Vector3[] WallRepulsions = new Vector3[GizmoCapacity];
        private static readonly Vector3[] DesiredVelocities = new Vector3[GizmoCapacity];
        private static readonly Vector3[] AcousticMemory = new Vector3[GizmoCapacity];

        private ApexCortexTuningSnapshot _snapshot;
        private bool _drawAiIntent;
        private string _status = "Vault not sampled.";

        [MenuItem("Hecton8/AI/Apex Cortex Tuner")]
        private static void Open()
        {
            GetWindow<ApexCortexTunerWindow>("Apex Cortex Tuner");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui -= DrawSceneIntent;
            SceneView.duringSceneGui += DrawSceneIntent;
            RefreshFromVault();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawSceneIntent;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Apex Cortex Tuner", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
            {
                EditorGUI.BeginChangeCheck();
                _snapshot.HungerWeight = EditorGUILayout.Slider("HungerWeight", _snapshot.HungerWeight, 0.1f, 3f);
                _snapshot.FearWeight = EditorGUILayout.Slider("FearWeight", _snapshot.FearWeight, 0.1f, 3f);
                _snapshot.LightAversion = EditorGUILayout.Slider("LightAversion", _snapshot.LightAversion, 0f, 3f);
                _snapshot.AcousticMemoryDecay = EditorGUILayout.Slider("AcousticMemoryDecay", _snapshot.AcousticMemoryDecay, 0.01f, 3f);
                if (EditorGUI.EndChangeCheck())
                {
                    _status = PredatorCognitionDomain.TrySetApexCortexTuning(in _snapshot)
                        ? "Vault tuning updated."
                        : "Vault tuning unavailable.";
                    SceneView.RepaintAll();
                }

                EditorGUILayout.Space(6f);
                if (GUILayout.Button("Reload ai_behavior_overrides.csv"))
                {
                    _status = PredatorCognitionDomain.TryReloadApexCortexBehaviorOverrides()
                        ? "CSV overrides applied."
                        : "CSV overrides not found or unchanged.";
                    RefreshFromVault();
                }
            }

            _drawAiIntent = EditorGUILayout.Toggle("Draw AI Intent", _drawAiIntent);
            if (GUILayout.Button("Refresh From Vault"))
                RefreshFromVault();

            EditorGUILayout.HelpBox(_status, MessageType.None);
        }

        private void RefreshFromVault()
        {
            if (PredatorCognitionDomain.TryGetApexCortexTuning(out _snapshot))
                _status = "Vault tuning sampled.";
            else
                _status = "Vault tuning unavailable.";
        }

        private void DrawSceneIntent(SceneView sceneView)
        {
            if (!_drawAiIntent || !EditorApplication.isPlaying)
                return;

            int count = PredatorCognitionDomain.CopyApexCortexDebugGizmos(
                Origins,
                Targets,
                WallRepulsions,
                DesiredVelocities,
                AcousticMemory,
                GizmoCapacity);
            for (int i = 0; i < count; i++)
            {
                Vector3 origin = Origins[i];
                Handles.color = Color.red;
                Handles.DrawLine(origin, Targets[i]);
                Handles.color = Color.yellow;
                Handles.DrawLine(origin, origin + (WallRepulsions[i] * 6f));
                Handles.color = Color.blue;
                Handles.DrawLine(origin, origin + (DesiredVelocities[i] * 10f));
                Handles.color = new Color(0.2f, 0.8f, 1f, 0.75f);
                Vector3 memory = AcousticMemory[i];
                Handles.DrawWireDisc(memory, Vector3.up, 1.5f);
                Handles.DrawWireDisc(memory, Vector3.right, 1.5f);
                Handles.DrawWireDisc(memory, Vector3.forward, 1.5f);
            }
        }
    }
}
#endif
