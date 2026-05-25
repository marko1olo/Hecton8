#if UNITY_EDITOR
using System.Globalization;
using Hecton8.Core;
using Hecton8.Core.Memory;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    public sealed class MemorySentinelTunerWindow : EditorWindow
    {
        private float _validationFrequencyHz = 10f;
        private float _aupTeleportToleranceMeters = 50000f;
        private float _strictness01 = 1f;
        private bool _moddedGameEnabled;

        [MenuItem("Hecton8/Memory Sentinel Tuner")]
        private static void Open()
        {
            GetWindow<MemorySentinelTunerWindow>("Memory Sentinel Tuner");
        }

        private void OnGUI()
        {
            bool active = MemorySentinelRuntime.TryGetTunerSnapshot(out MemorySentinelTunerSnapshotDTO snapshot);
            using (new EditorGUI.DisabledScope(!active))
            {
                if (active && Event.current.type == EventType.Layout)
                {
                    _validationFrequencyHz = snapshot.ValidationFrequencyHz;
                    _aupTeleportToleranceMeters = snapshot.AupTeleportToleranceMeters;
                    _strictness01 = snapshot.Strictness01;
                    _moddedGameEnabled = snapshot.ModdedGameMask != 0u;
                }

                _validationFrequencyHz = EditorGUILayout.Slider("Validation Frequency", _validationFrequencyHz, 1f, 10f);
                _aupTeleportToleranceMeters = EditorGUILayout.Slider("AUP Teleport Tolerance", _aupTeleportToleranceMeters, 10f, 50000f);
                _strictness01 = EditorGUILayout.Slider("Strictness Level", _strictness01, 0f, 1f);
                _moddedGameEnabled = EditorGUILayout.Toggle("Modded Game Mask", _moddedGameEnabled);

                if (GUILayout.Button("Apply Vault Parameters"))
                    MemorySentinelRuntime.TrySetTunerParameters(_validationFrequencyHz, _aupTeleportToleranceMeters, _strictness01);

                if (GUILayout.Button("Apply Mod Quarantine Mask"))
                    MemorySentinelRuntime.TrySetModdedGameMask(_moddedGameEnabled ? 1u : 0u);

                if (GUILayout.Button("Load validation_rules.csv"))
                    MemorySentinelRuntime.TryLoadValidationRulesCsv();

                if (GUILayout.Button("Simulate Cheat Engine Write"))
                {
                    if (MemorySentinelRuntime.TrySimulateCheatEngineWrite())
                        Debug.LogWarning("SHINOBU_73 simulated 4-byte inventory tamper; rollback is evaluated on the next sentinel tick.");
                }

                if (GUILayout.Button("Dump Black Box"))
                    MemorySentinelRuntime.TryDumpBlackBox();
            }

            if (!active)
            {
                EditorGUILayout.HelpBox("Memory Sentinel runtime is not active in edit mode.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Global Quality Weight", snapshot.GlobalQualityWeight.ToString("0.000", CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Last Validation ms", snapshot.LastValidationMs.ToString("0.000", CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Targets", snapshot.TargetCount.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Last Bytes Hashed", snapshot.LastBytesHashed.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Last Corrected", snapshot.LastCorrectedCount.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Last Fatal", snapshot.LastFatalCount.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Modded Mask", "0x" + snapshot.ModdedGameMask.ToString("X8", CultureInfo.InvariantCulture));
        }
    }
}
#endif
