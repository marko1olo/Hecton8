#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Hecton8.Audio.Editor
{
    public sealed class AdaptiveAudioTunerWindow : EditorWindow
    {
        private const int TelemetryCapacity = 300;
        private readonly Vector3[] _graphPoints = new Vector3[TelemetryCapacity];

        [MenuItem("Hecton8/Audio/Adaptive Audio Tuner")]
        public static void Open()
        {
            GetWindow<AdaptiveAudioTunerWindow>("Adaptive Audio Tuner");
        }

        private void OnGUI()
        {
            if (!AdaptiveStemAudioMixer.TryGetActive(out AdaptiveStemAudioMixer mixer))
            {
                EditorGUILayout.HelpBox("No active AdaptiveStemAudioMixer in Play Mode.", MessageType.Warning);
                Repaint();
                return;
            }

            if (!mixer.TryGetEditorRule(out AudioStemRuleDTO rule))
            {
                EditorGUILayout.HelpBox("Adaptive stem vault is not ready.", MessageType.Warning);
                Repaint();
                return;
            }

            EditorGUI.BeginChangeCheck();
            rule.AttackSeconds = EditorGUILayout.Slider("Tension Attack", rule.AttackSeconds, 0.01f, 5f);
            rule.ReleaseSeconds = EditorGUILayout.Slider("Tension Release", rule.ReleaseSeconds, 0.1f, 60f);
            rule.DepthFilterMaxHz = EditorGUILayout.Slider("Depth Filter Max", rule.DepthFilterMaxHz, 2000f, 22000f);
            rule.DepthFilterMinHz = EditorGUILayout.Slider("Depth Filter Min", rule.DepthFilterMinHz, 10f, rule.DepthFilterMaxHz);
            rule.CombatEnterThreshold = EditorGUILayout.Slider("Combat Enter", rule.CombatEnterThreshold, 0f, 1f);
            rule.CombatExitThreshold = EditorGUILayout.Slider("Combat Exit", rule.CombatExitThreshold, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
                mixer.TryWriteEditorRule(in rule);

            if (mixer.TryGetEditorMixFrame(out StemMixFrameDTO frame))
            {
                EditorGUILayout.Space(8f);
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.FloatField("Base Volume", frame.BaseVolume);
                    EditorGUILayout.FloatField("Action Volume", frame.ActionVolume);
                    EditorGUILayout.FloatField("Depth Volume", frame.DepthVolume);
                    EditorGUILayout.FloatField("Boss Volume", frame.BossVolume);
                    EditorGUILayout.FloatField("Cutoff Hz", frame.CutoffHz);
                    EditorGUILayout.FloatField("Quality Weight", frame.QualityWeight);
                }
            }

            if (mixer.TryGetEditorTelemetry(0, out AudioStemTelemetryEntry latestTelemetry))
            {
                EditorGUILayout.Space(6f);
                DrawCelestialLightTelemetry(latestTelemetry.Flags);
            }

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("Repair Stem Clip Imports"))
                RepairStemClipImports(mixer);

            EditorGUILayout.Space(8f);
            Rect rect = GUILayoutUtility.GetRect(10f, 160f, GUILayout.ExpandWidth(true));
            DrawOscilloscope(rect, mixer, rule);
            Repaint();
        }

        private static void RepairStemClipImports(AdaptiveStemAudioMixer mixer)
        {
            int repairedCount = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < 4; i++)
                {
                    if (!mixer.TryGetEditorStemClip(i, out AudioClip clip))
                        continue;

                    string path = AssetDatabase.GetAssetPath(clip);
                    if (string.IsNullOrEmpty(path))
                        continue;

                    AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
                    if (importer == null)
                        continue;

                    bool changed = false;
                    AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                    if (settings.loadType != AudioClipLoadType.Streaming)
                    {
                        settings.loadType = AudioClipLoadType.Streaming;
                        changed = true;
                    }

                    if (settings.compressionFormat != AudioCompressionFormat.Vorbis)
                    {
                        settings.compressionFormat = AudioCompressionFormat.Vorbis;
                        changed = true;
                    }

                    if (settings.sampleRateSetting != AudioSampleRateSetting.OverrideSampleRate ||
                        settings.sampleRateOverride != 44100u)
                    {
                        settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
                        settings.sampleRateOverride = 44100u;
                        changed = true;
                    }

                    if (settings.quality > 0.71f || settings.quality < 0.69f)
                    {
                        settings.quality = 0.7f;
                        changed = true;
                    }

                    if (settings.preloadAudioData)
                    {
                        settings.preloadAudioData = false;
                        changed = true;
                    }

                    if (!importer.loadInBackground)
                    {
                        importer.loadInBackground = true;
                        changed = true;
                    }

                    if (!changed)
                        continue;

                    importer.defaultSampleSettings = settings;
                    importer.SaveAndReimport();
                    repairedCount++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            if (repairedCount > 0)
                AssetDatabase.SaveAssets();
        }

        private void DrawOscilloscope(Rect rect, AdaptiveStemAudioMixer mixer, AudioStemRuleDTO rule)
        {
            EditorGUI.DrawRect(rect, new Color(0.04f, 0.05f, 0.055f, 1f));
            Handles.BeginGUI();
            DrawThresholdLine(rect, rule.CombatExitThreshold, new Color(0.45f, 0.55f, 0.7f, 0.65f));
            DrawThresholdLine(rect, rule.CombatEnterThreshold, new Color(0.95f, 0.42f, 0.24f, 0.8f));

            int pointCount = 0;
            for (int i = 0; i < TelemetryCapacity; i++)
            {
                if (!mixer.TryGetEditorTelemetry(TelemetryCapacity - 1 - i, out AudioStemTelemetryEntry entry))
                    continue;

                float x = rect.xMin + rect.width * (i / (float)(TelemetryCapacity - 1));
                float y = rect.yMax - Mathf.Clamp01(entry.TensionIndex) * rect.height;
                _graphPoints[pointCount++] = new Vector3(x, y, 0f);
            }

            if (pointCount > 1)
            {
                Handles.color = new Color(0.2f, 0.95f, 0.82f, 1f);
                for (int i = 1; i < pointCount; i++)
                    Handles.DrawLine(_graphPoints[i - 1], _graphPoints[i]);
            }

            Handles.EndGUI();
        }

        private static void DrawCelestialLightTelemetry(uint flags)
        {
            bool missing = (flags & AdaptiveStemAudioMixer.TelemetryFlagCelestialLightMissing) != 0u;
            bool fallback = (flags & AdaptiveStemAudioMixer.TelemetryFlagCelestialLightFallback) != 0u;
            bool abyssCritical = (flags & AdaptiveStemAudioMixer.TelemetryFlagCelestialLightAbyssCritical) != 0u;
            bool qualityReduced = (flags & AdaptiveStemAudioMixer.TelemetryFlagCelestialLightQualityReduced) != 0u;
            bool twilight = (flags & AdaptiveStemAudioMixer.TelemetryFlagCelestialLightTwilight) != 0u;
            bool night = (flags & AdaptiveStemAudioMixer.TelemetryFlagCelestialLightNight) != 0u;
            bool bound = (flags & AdaptiveStemAudioMixer.TelemetryFlagCelestialLightBound) != 0u;

            string state = missing
                ? "Missing"
                : fallback
                    ? "Fallback"
                    : abyssCritical
                        ? "Abyss critical"
                        : night
                            ? "Night phase"
                            : twilight
                                ? "Twilight phase"
                                : qualityReduced
                                    ? "Quality reduced"
                                    : bound
                                        ? "Bound"
                                        : "Idle";
            MessageType messageType = missing || fallback ? MessageType.Warning : MessageType.Info;
            EditorGUILayout.HelpBox("Celestial light bridge: " + state, messageType);
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.TextField("Telemetry Flags", "0x" + flags.ToString("X8"));
        }

        private static void DrawThresholdLine(Rect rect, float value, Color color)
        {
            float y = rect.yMax - Mathf.Clamp01(value) * rect.height;
            Handles.color = color;
            Handles.DrawLine(new Vector3(rect.xMin, y, 0f), new Vector3(rect.xMax, y, 0f));
        }
    }
}
#endif
