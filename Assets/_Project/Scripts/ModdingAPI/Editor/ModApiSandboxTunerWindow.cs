#if UNITY_EDITOR
using Hecton8.Modding;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    public sealed class ModApiSandboxTunerWindow : EditorWindow
    {
        private readonly uint[] _opcodeHashes =
        {
            FutureCommandOpcodes.SpawnItem,
            FutureCommandOpcodes.AlterHealth,
            FutureCommandOpcodes.AlterGravity,
            FutureCommandOpcodes.AssetReference,
            FutureCommandOpcodes.ModMemoryRead,
            FutureCommandOpcodes.ModMemoryWrite,
            FutureCommandOpcodes.FaunaAcousticStimulus,
            FutureCommandOpcodes.FaunaDamageStimulus,
            FutureCommandOpcodes.TriggerSubtitleCue
        };

        private readonly string[] _opcodeLabels =
        {
            "SPAWN_ITEM_OP",
            "ALTER_HEALTH_OP",
            "ALTER_GRAVITY_OP",
            "ASSET_REFERENCE_OP",
            "MOD_MEMORY_READ_OP",
            "MOD_MEMORY_WRITE_OP",
            "FAUNA_ACOUSTIC_STIMULUS_OP",
            "FAUNA_DAMAGE_STIMULUS_OP",
            "TRIGGER_SUBTITLE_CUE_OP"
        };

        private Vector2 _scroll;

        [MenuItem("HECTON-8/Mod API Sandbox Tuner")]
        public static void Open()
        {
            GetWindow<ModApiSandboxTunerWindow>("Mod API Sandbox Tuner");
        }

        private void OnEnable()
        {
            FutureCommandSandboxValidator.Initialize();
            EditorApplication.update += Repaint;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Repaint;
        }

        private void OnGUI()
        {
            FutureCommandSandboxValidator.Initialize();
            FutureCommandSandboxTuning tuning = FutureCommandSandboxValidator.GetTuningSnapshot();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUI.BeginChangeCheck();
            tuning.MaxCommandsPerFrame = EditorGUILayout.IntSlider("Max Commands Per Frame", tuning.MaxCommandsPerFrame, 10, 10000);
            tuning.MaxModMemoryMb = EditorGUILayout.IntSlider("Max Mod Memory MB", tuning.MaxModMemoryMb, 1, 256);
            int maxAssetMb = EditorGUILayout.IntSlider("Max Asset MB", (int)(tuning.MaxAssetBytes / (1024u * 1024u)), 1, 256);
            tuning.MaxAssetBytes = (uint)maxAssetMb * 1024u * 1024u;
            float qualityOverride = tuning.GlobalQualityWeightOverride;
            bool qualityForced = qualityOverride >= 0f;
            qualityForced = EditorGUILayout.Toggle("Force Quality Weight", qualityForced);
            tuning.GlobalQualityWeightOverride = qualityForced
                ? EditorGUILayout.Slider("Quality Weight", Mathf.Clamp01(qualityOverride < 0f ? 1f : qualityOverride), 0f, 1f)
                : -1f;
            tuning.CpuThermalPressure01 = EditorGUILayout.Slider("CPU Thermal Pressure", Mathf.Clamp01(tuning.CpuThermalPressure01), 0f, 1f);
            if (EditorGUI.EndChangeCheck())
                FutureCommandSandboxValidator.ApplyTuning(in tuning);

            EditorGUILayout.Space(8f);
            for (int i = 0; i < _opcodeHashes.Length; i++)
            {
                bool enabled = FutureCommandSandboxValidator.IsOpcodeEnabled(_opcodeHashes[i]);
                bool next = EditorGUILayout.ToggleLeft(_opcodeLabels[i], enabled);
                if (next != enabled)
                    FutureCommandSandboxValidator.SetOpcodeEnabled(_opcodeHashes[i], next);
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reload allowed_opcodes.csv"))
                FutureCommandSandboxValidator.TryReloadAllowedOpcodesCsvFromDisk();
            if (GUILayout.Button("Run Self Audit"))
                FutureCommandSandboxValidator.RunSelfAudit();
            if (GUILayout.Button("Dump Blackbox"))
                FutureCommandSandboxValidator.DumpBlackbox(0x4544554Du);
            EditorGUILayout.EndHorizontal();

            DrawTrafficHistogram();
            EditorGUILayout.EndScrollView();
        }

        private void DrawTrafficHistogram()
        {
            Rect rect = GUILayoutUtility.GetRect(10f, 120f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.08f, 0.08f, 0.08f, 1f));

            uint peak = 1u;
            int count = FutureCommandSandboxConstants.TelemetryCapacity;
            for (int i = 0; i < count; i++)
            {
                if (!FutureCommandSandboxValidator.TryGetTelemetryEntry(i, out ModSandboxTelemetryEntry entry))
                    continue;

                peak = entry.Incoming > peak ? entry.Incoming : peak;
                peak = entry.CommandsRejected > peak ? entry.CommandsRejected : peak;
            }

            float barWidth = Mathf.Max(1f, rect.width / count);
            for (int i = 0; i < count; i++)
            {
                if (!FutureCommandSandboxValidator.TryGetTelemetryEntry(i, out ModSandboxTelemetryEntry entry))
                    continue;

                float x = rect.x + i * barWidth;
                float incomingHeight = rect.height * (entry.Incoming / (float)peak);
                float rejectedHeight = rect.height * (entry.CommandsRejected / (float)peak);
                EditorGUI.DrawRect(new Rect(x, rect.yMax - incomingHeight, barWidth * 0.45f, incomingHeight), new Color(0.2f, 0.65f, 0.9f, 1f));
                EditorGUI.DrawRect(new Rect(x + barWidth * 0.5f, rect.yMax - rejectedHeight, barWidth * 0.45f, rejectedHeight), new Color(0.9f, 0.25f, 0.18f, 1f));
            }
        }
    }
}
#endif
