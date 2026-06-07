#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using Hecton8.Audio.Virtualization;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Audio.Editor
{
    /// <summary>
    /// Editor-only facade for vault-backed virtual acoustic DSP constants.
    /// </summary>
    public sealed class SabineReverbDspTunerWindow : EditorWindow
    {
        private const string ProfileAssetPath = "Assets/_Project/Data/Audio/audio_profiles.csv";
        private const float StatsRefreshSeconds = 0.25f;

        private VirtualVoiceTuningSnapshot _tuning = VirtualVoiceTuningSnapshot.CreateDefault();
        private VirtualVoiceStatistics _stats;
        private DateTime _profileLastWriteUtc;
        private string _profileAbsolutePath;
        private double _nextStatsRefreshTime;
        private bool _drawGizmos = true;
        private bool _hasRuntimeTuning;

        [MenuItem("Hecton8/Audio/Sabine Reverb & DSP Tuner")]
        public static void Open()
        {
            GetWindow<SabineReverbDspTunerWindow>("Sabine DSP Tuner");
        }

        private void OnEnable()
        {
            _profileAbsolutePath = ResolveProfileAbsolutePath();
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            SceneView.duringSceneGui -= OnSceneGui;
            SceneView.duringSceneGui += OnSceneGui;
            PullFromRuntime();
            TryReloadCsvProfile(force: true);
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGui;
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (TryReloadCsvProfile(force: false))
                PublishToRuntime();

            if (EditorApplication.isPlaying && EditorApplication.timeSinceStartup >= _nextStatsRefreshTime)
            {
                _nextStatsRefreshTime = EditorApplication.timeSinceStartup + StatsRefreshSeconds;
                PullFromRuntime();
                Repaint();
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Vault", _hasRuntimeTuning ? "Live" : "Unavailable");
            EditorGUI.BeginChangeCheck();
            _tuning.SoundSpeedMetersPerSecond = EditorGUILayout.Slider(
                "Speed of Sound",
                _tuning.SoundSpeedMetersPerSecond,
                250f,
                2000f);
            _tuning.GlobalOcclusionPenalty = EditorGUILayout.Slider(
                "Global Occlusion Penalty",
                _tuning.GlobalOcclusionPenalty,
                0.03162278f,
                1f);
            _tuning.OccludedLowPassHertz = EditorGUILayout.Slider(
                "Occlusion Low-Pass Hz",
                _tuning.OccludedLowPassHertz,
                80f,
                VirtualVoiceUtility.OpenLowPassHertz);
            _tuning.SabineDecayScale = EditorGUILayout.Slider(
                "Sabine Decay Times",
                _tuning.SabineDecayScale,
                0.1f,
                4f);
            _tuning.MaxHydratedVoices = EditorGUILayout.IntSlider(
                "Max Hydrated Voices",
                _tuning.MaxHydratedVoices,
                1,
                VirtualVoiceUtility.MaxPhysicalVoiceCount);
            _tuning.DisableSdfOcclusion = EditorGUILayout.Toggle(
                "Disable SDF Occlusion",
                _tuning.DisableSdfOcclusion != 0) ? (byte)1 : (byte)0;
            _drawGizmos = EditorGUILayout.Toggle("Draw Scene Gizmos", _drawGizmos);
            if (EditorGUI.EndChangeCheck())
            {
                _tuning = VirtualVoiceTuningSnapshot.Sanitize(in _tuning);
                PublishToRuntime();
            }

            EditorGUILayout.Space(8f);
            DrawStats();

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reload CSV"))
                {
                    if (TryReloadCsvProfile(force: true))
                        PublishToRuntime();
                }

                if (GUILayout.Button("Write Defaults"))
                    WriteDefaultCsvProfile();
            }
        }

        private void DrawStats()
        {
            EditorGUILayout.LabelField("Total Virtual Voices", _stats.TotalVoices.ToString());
            EditorGUILayout.LabelField("Audible Virtual Voices", _stats.AudibleVoices.ToString());
            EditorGUILayout.LabelField("Hydrated Voices", _stats.ActivePhysicalVoices.ToString());
            EditorGUILayout.LabelField("Culled Voices", _stats.CulledVoices.ToString());
            EditorGUILayout.LabelField("Average Sort Time ms", _stats.SortTimeMs.ToString("0.000"));
            EditorGUILayout.LabelField("SDF Occlusion Time ms", _stats.AcousticOcclusionTimeMs.ToString("0.000"));
            EditorGUILayout.LabelField("Average RT60 s", _stats.AverageRt60Seconds.ToString("0.000"));
            EditorGUILayout.LabelField("Average LPF Hz", _stats.AverageLowPassHertz.ToString("0"));
            EditorGUILayout.LabelField("Max Delay s", _stats.MaximumDelaySeconds.ToString("0.000"));
        }

        private void OnSceneGui(SceneView sceneView)
        {
            if (!_drawGizmos || !EditorApplication.isPlaying)
                return;

            SpatialAudioManager manager = ResolveSpatialAudioManager();
            if (manager == null)
                return;

            manager.DrawVirtualVoiceEditorGizmos();
        }

        private void PullFromRuntime()
        {
            SpatialAudioManager manager = ResolveSpatialAudioManager();
            if (manager == null)
            {
                _hasRuntimeTuning = false;
                return;
            }

            _hasRuntimeTuning = manager.TryGetVirtualVoiceRuntimeTuning(out _tuning);
            manager.TryGetVirtualizationStats(out _stats);
        }

        private void PublishToRuntime()
        {
            if (!EditorApplication.isPlaying)
                return;

            SpatialAudioManager manager = ResolveSpatialAudioManager();
            if (manager == null)
                return;

            _tuning = VirtualVoiceTuningSnapshot.Sanitize(in _tuning);
            manager.ApplyVirtualVoiceRuntimeTuning(in _tuning);
            _hasRuntimeTuning = true;
        }

        private bool TryReloadCsvProfile(bool force)
        {
            if (string.IsNullOrEmpty(_profileAbsolutePath) || !File.Exists(_profileAbsolutePath))
                return false;

            DateTime lastWriteUtc = File.GetLastWriteTimeUtc(_profileAbsolutePath);
            if (!force && lastWriteUtc == _profileLastWriteUtc)
                return false;

            _profileLastWriteUtc = lastWriteUtc;
            byte[] csv = File.ReadAllBytes(_profileAbsolutePath); // COLD ALLOC: editor-only byte reload, runtime parser stays span-based.
            VirtualVoiceTuningSnapshot parsed = _tuning;
            if (!VirtualVoiceProfileCsvParser.TryReadTuning(csv.AsSpan(), ref parsed))
                return false;

            _tuning = parsed;
            return true;
        }

        private void WriteDefaultCsvProfile()
        {
            if (string.IsNullOrEmpty(_profileAbsolutePath))
                _profileAbsolutePath = ResolveProfileAbsolutePath();

            string directory = Path.GetDirectoryName(_profileAbsolutePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            VirtualVoiceTuningSnapshot defaults = VirtualVoiceTuningSnapshot.CreateDefault();
            File.WriteAllText(
                _profileAbsolutePath,
                "speed_of_sound," + defaults.SoundSpeedMetersPerSecond.ToString("0.###", CultureInfo.InvariantCulture) + System.Environment.NewLine +
                "global_occlusion_penalty," + defaults.GlobalOcclusionPenalty.ToString("0.########", CultureInfo.InvariantCulture) + System.Environment.NewLine +
                "occluded_lowpass_hz," + defaults.OccludedLowPassHertz.ToString("0.###", CultureInfo.InvariantCulture) + System.Environment.NewLine +
                "sabine_decay_scale," + defaults.SabineDecayScale.ToString("0.###", CultureInfo.InvariantCulture) + System.Environment.NewLine +
                "max_hydrated_voices," + defaults.MaxHydratedVoices + System.Environment.NewLine +
                "disable_sdf_occlusion," + defaults.DisableSdfOcclusion + System.Environment.NewLine);
            _profileLastWriteUtc = DateTime.MinValue;
            TryReloadCsvProfile(force: true);
        }

        private static SpatialAudioManager ResolveSpatialAudioManager()
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindAnyObjectByType<SpatialAudioManager>(FindObjectsInactive.Include);
#else
            return UnityEngine.Object.FindObjectOfType<SpatialAudioManager>();
#endif
        }

        private static string ResolveProfileAbsolutePath()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, ProfileAssetPath));
        }
    }
}
#endif
