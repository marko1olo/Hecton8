#if UNITY_EDITOR
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.AI.Cognition.Editor
{
    /// <summary>
    /// Editor facade for SHINOBU_61 unmanaged apex tuning.
    /// </summary>
    public sealed class LeviathanCortexTunerWindow : EditorWindow
    {
        private ApexBrainVaultHandles _handles;
        private ApexBrainTuning _tuning;
        private bool _drawGizmos = true;
        private string _status = "Vault unavailable.";
        private double _nextCsvPollTime;

        [MenuItem("Hecton8/AI/Leviathan Cortex Tuner")]
        private static void Open()
        {
            GetWindow<LeviathanCortexTunerWindow>("Leviathan Cortex Tuner");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui -= OnDrawGizmosSceneView;
            SceneView.duringSceneGui += OnDrawGizmosSceneView;
            RefreshFromVault();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnDrawGizmosSceneView;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Leviathan Cortex Tuner", EditorStyles.boldLabel);
            bool vaultReady = TryEnsureVault();
            PollCsvIfDue(vaultReady);
            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || !vaultReady))
            {
                EditorGUI.BeginChangeCheck();
                _tuning.AggressionMultiplier = EditorGUILayout.Slider("Aggression Multiplier", _tuning.AggressionMultiplier, 0.05f, 4f);
                _tuning.AcousticSensitivity = EditorGUILayout.Slider("Acoustic Sensitivity", _tuning.AcousticSensitivity, 0.05f, 4f);
                _tuning.TurnRate = EditorGUILayout.Slider("Turn Rate", _tuning.TurnRate, 0.01f, 1f);
                _tuning.StalkingDistance = EditorGUILayout.Slider("Stalking Distance", _tuning.StalkingDistance, 8f, 260f);
                _tuning.GlobalQualityWeight = EditorGUILayout.Slider("Global Quality Weight", _tuning.GlobalQualityWeight, 0f, 1f);
                if (EditorGUI.EndChangeCheck())
                    WriteTuning();

                EditorGUILayout.Space(4f);
                if (GUILayout.Button("Reload apex_predator_stats.csv"))
                    ReloadCsv();
                if (GUILayout.Button("Generate Emergency Mock Apex Stats"))
                    GenerateEmergencyMock();
                if (GUILayout.Button("Dump Leviathan Cortex Black Box"))
                    DumpBlackBox();
            }

            _drawGizmos = EditorGUILayout.Toggle("Draw Intercept Gizmos", _drawGizmos);
            if (GUILayout.Button("Refresh From Vault"))
                RefreshFromVault();

            EditorGUILayout.HelpBox(_status, MessageType.None);
        }

        private void PollCsvIfDue(bool vaultReady)
        {
            if (!EditorApplication.isPlaying || !vaultReady)
                return;

            double now = EditorApplication.timeSinceStartup;
            if (now < _nextCsvPollTime)
                return;

            _nextCsvPollTime = now + 0.5d;
            if (ApexBrainVault.TryPollCsvOverrides(GlobalRegistry.DataVault, ref _handles, Application.dataPath + "/.."))
            {
                RefreshFromVault();
                _status = "CSV overrides auto-applied.";
                SceneView.RepaintAll();
            }
        }

        private bool TryEnsureVault()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
            {
                _status = "GlobalRegistry.DataVault unavailable.";
                return false;
            }

            if (!_handles.IsCreated() && !ApexBrainVault.TryAcquireHandles(vault, out _handles))
            {
                _status = "ApexBrain vault handles unavailable.";
                return false;
            }

            return true;
        }

        private void RefreshFromVault()
        {
            if (!TryEnsureVault())
                return;

            if (ApexBrainVault.TryGetTuning(GlobalRegistry.DataVault, ref _handles, out _tuning))
                _status = "Vault tuning sampled.";
            else
                _status = "Vault tuning unreadable.";
        }

        private void WriteTuning()
        {
            if (!TryEnsureVault())
                return;

            _status = ApexBrainVault.TrySetTuning(GlobalRegistry.DataVault, ref _handles, in _tuning)
                ? "Vault tuning updated."
                : "Vault tuning write failed.";
            SceneView.RepaintAll();
        }

        private void ReloadCsv()
        {
            if (!TryEnsureVault())
                return;

            _status = ApexBrainVault.TryLoadCsvOverrides(GlobalRegistry.DataVault, ref _handles, Application.dataPath + "/..")
                ? "CSV overrides applied."
                : "CSV overrides missing or unchanged.";
            RefreshFromVault();
            SceneView.RepaintAll();
        }

        private void GenerateEmergencyMock()
        {
            _tuning = ApexBrainVault.BuildEmergencyMockTuning();
            WriteTuning();
        }

        private void DumpBlackBox()
        {
            if (!TryEnsureVault())
                return;

            if (!ApexBrainVault.TryResolveViews(GlobalRegistry.DataVault, ref _handles, out ApexBrainVaultBuffers buffers))
            {
                _status = "Telemetry buffers unavailable.";
                return;
            }

            _status = ApexBrainVault.TryDumpBlackBox(in buffers, Application.dataPath + "/..")
                ? "Black box dumped."
                : "Black box dump failed.";
        }

        private void OnDrawGizmosSceneView(SceneView sceneView)
        {
            if (!_drawGizmos || !EditorApplication.isPlaying || !TryEnsureVault())
                return;

            if (!ApexBrainVault.TryResolveViews(GlobalRegistry.DataVault, ref _handles, out ApexBrainVaultBuffers buffers) ||
                !buffers.Outputs.IsCreated)
            {
                return;
            }

            double pulse = EditorApplication.timeSinceStartup;
            float pulsePhase = (float)pulse * 4f;
            float pulseFraction = pulsePhase - math.floor(pulsePhase);
            float pulseTriangle = (1f - math.abs(pulseFraction * 2f - 1f)) * 2f - 1f;
            float ringRadius = 2f + (pulseTriangle * 0.5f);
            int count = math.min(buffers.Outputs.Length, ApexBrainConstants.MaxLeviathans);
            for (int i = 0; i < count; i++)
            {
                ApexBrainOutputDTO output = buffers.Outputs[i];
                if ((output.Flags & ApexBrainFlags.Active) == 0)
                    continue;

                Vector3 origin = Vector3.zero;
                Vector3 intercept = ToVector3(output.InterceptLocal);
                Vector3 acoustic = ToVector3(output.AcousticMemoryLocal);
                Vector3 desired = ToVector3(output.DesiredDirection) * 10f;

                Handles.color = Color.red;
                Handles.SphereHandleCap(0, intercept, Quaternion.identity, 2.25f, EventType.Repaint);
                Handles.DrawLine(origin, intercept);

                Handles.color = Color.yellow;
                Handles.DrawWireDisc(acoustic, Vector3.up, ringRadius);
                Handles.DrawWireDisc(acoustic, Vector3.right, ringRadius);
                Handles.DrawWireDisc(acoustic, Vector3.forward, ringRadius);

                Handles.color = Color.cyan;
                Handles.DrawLine(origin, origin + desired);
            }
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }
    }
}
#endif
