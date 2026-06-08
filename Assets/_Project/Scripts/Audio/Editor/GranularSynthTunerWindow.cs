#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using Hecton8.Core;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Audio.Editor
{
    /// <summary>
    /// Editor-only live tuning facade for the player-critical structural granular synth.
    /// </summary>
    public sealed class GranularSynthTunerWindow : EditorWindow
    {
        private const int ScopeSampleCount = 128;
        private const string ProfileAssetPath = "Assets/_Project/Data/Audio/audio_synth_profiles.csv";
        private const float ScopeHeight = 96f;
        private const float ScopePadding = 6f;

        // COLD ALLOC: editor-only oscilloscope sample cache - owner: GranularSynthTunerWindow.
        private readonly float[] _scopeSamples = new float[ScopeSampleCount];
        // COLD ALLOC: editor-only Handles polyline cache - owner: GranularSynthTunerWindow.
        private readonly Vector3[] _scopePoints = new Vector3[ScopeSampleCount];

        private PlayerCriticalProceduralAudioRenderer.GranularSynthTuningSnapshot _tuning = new PlayerCriticalProceduralAudioRenderer.GranularSynthTuningSnapshot
        {
            BasePitchScale = 1f,
            GrainLengthScale = 1f,
            OverlapDensityScale = 1f,
            FmModulationIndex = 1f
        };

        private PlayerCriticalProceduralAudioRenderer.GranularSynthTuningSnapshot _lastPublishedTuning;
        private bool _hasPublishedTuning;
        private DateTime _profileLastWriteUtc;
        private string _profileAbsolutePath;

        /// <summary>Opens the Granular Synth Tuner editor window.</summary>
        [MenuItem("Hecton8/Audio/Granular Synth Tuner")]
        public static void Open()
        {
            GetWindow<GranularSynthTunerWindow>("Granular Synth Tuner");
        }

        private void OnEnable()
        {
            _profileAbsolutePath = ResolveProfileAbsolutePath();
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            bool profileChanged = TryReloadCsvProfile();
            if (profileChanged)
                PublishToRenderer(force: true);

            if (EditorApplication.isPlaying)
                Repaint();
        }

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            _tuning.BasePitchScale = EditorGUILayout.Slider("Base Pitch", _tuning.BasePitchScale, 0.35f, 2.4f);
            _tuning.GrainLengthScale = EditorGUILayout.Slider("Grain Length", _tuning.GrainLengthScale, 0.25f, 4f);
            _tuning.OverlapDensityScale = EditorGUILayout.Slider("Overlap Density", _tuning.OverlapDensityScale, 0f, 4f);
            _tuning.FmModulationIndex = EditorGUILayout.Slider("FM Modulation Index", _tuning.FmModulationIndex, 0f, 4f);
            if (EditorGUI.EndChangeCheck())
                PublishToRenderer(force: true);

            EditorGUILayout.Space(8f);
            DrawOscilloscope();
        }

        private void PublishToRenderer(bool force)
        {
            if (!EditorApplication.isPlaying)
                return;

            if (!force && _hasPublishedTuning && AreEqual(_tuning, _lastPublishedTuning))
                return;

            PlayerCriticalProceduralAudioRenderer renderer = ResolveRenderer();
            if (renderer == null)
                return;

            renderer.ApplyGranularSynthTuning(
                _tuning.BasePitchScale,
                _tuning.GrainLengthScale,
                _tuning.OverlapDensityScale,
                _tuning.FmModulationIndex);
            _lastPublishedTuning = _tuning;
            _hasPublishedTuning = true;
        }

        private void DrawOscilloscope()
        {
            Rect rect = GUILayoutUtility.GetRect(1f, ScopeHeight, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.035f, 0.04f, 0.045f, 1f));

            PlayerCriticalProceduralAudioRenderer renderer = EditorApplication.isPlaying ? ResolveRenderer() : null;
            bool hasSamples = renderer != null &&
                              renderer.TryCopyLatestGranularOscilloscope(_scopeSamples, 0, _scopeSamples.Length);

            float centerY = rect.y + rect.height * 0.5f;
            float width = Mathf.Max(1f, rect.width - ScopePadding * 2f);
            float height = Mathf.Max(1f, rect.height - ScopePadding * 2f);
            for (int i = 0; i < _scopePoints.Length; i++)
            {
                float x = rect.x + ScopePadding + width * (i / (float)(_scopePoints.Length - 1));
                float sample = hasSamples ? Mathf.Clamp(_scopeSamples[i], -1f, 1f) : 0f;
                float y = centerY - sample * height * 0.45f;
                _scopePoints[i] = new Vector3(x, y, 0f);
            }

            Handles.BeginGUI();
            Handles.color = new Color(0.23f, 0.95f, 0.78f, 1f);
            Handles.DrawPolyLine(_scopePoints);
            Handles.color = new Color(0.23f, 0.95f, 0.78f, 0.22f);
            Handles.DrawLine(
                new Vector3(rect.x + ScopePadding, centerY, 0f),
                new Vector3(rect.xMax - ScopePadding, centerY, 0f));
            Handles.EndGUI();
        }

        private bool TryReloadCsvProfile()
        {
            if (string.IsNullOrEmpty(_profileAbsolutePath) || !File.Exists(_profileAbsolutePath))
                return false;

            DateTime lastWriteUtc = File.GetLastWriteTimeUtc(_profileAbsolutePath);
            if (lastWriteUtc == _profileLastWriteUtc)
                return false;

            _profileLastWriteUtc = lastWriteUtc;
            string text = File.ReadAllText(_profileAbsolutePath);
            PlayerCriticalProceduralAudioRenderer.GranularSynthTuningSnapshot parsed = _tuning;
            if (!TryParseCsvProfile(text.AsSpan(), ref parsed))
                return false;

            _tuning = parsed;
            return true;
        }

        private static bool TryParseCsvProfile(
            ReadOnlySpan<char> csv,
            ref PlayerCriticalProceduralAudioRenderer.GranularSynthTuningSnapshot tuning)
        {
            bool any = false;
            int cursor = 0;
            while (TryReadLine(csv, ref cursor, out ReadOnlySpan<char> line))
            {
                line = Trim(line);
                if (line.Length <= 0 || line[0] == '#')
                    continue;

                int delimiter = IndexOfDelimiter(line);
                if (delimiter <= 0 || delimiter >= line.Length - 1)
                    continue;

                ReadOnlySpan<char> key = Trim(line.Slice(0, delimiter));
                ReadOnlySpan<char> valueSpan = Trim(line.Slice(delimiter + 1));
                if (!float.TryParse(valueSpan, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                    continue;

                uint keyHash = HashKey(key);
                if (keyHash == HashKey("BasePitch".AsSpan()) || keyHash == HashKey("BasePitchScale".AsSpan()))
                {
                    tuning.BasePitchScale = Mathf.Clamp(value, 0.35f, 2.4f);
                    any = true;
                }
                else if (keyHash == HashKey("GrainLength".AsSpan()) || keyHash == HashKey("GrainLengthScale".AsSpan()))
                {
                    tuning.GrainLengthScale = Mathf.Clamp(value, 0.25f, 4f);
                    any = true;
                }
                else if (keyHash == HashKey("OverlapDensity".AsSpan()) || keyHash == HashKey("OverlapDensityScale".AsSpan()))
                {
                    tuning.OverlapDensityScale = Mathf.Clamp(value, 0f, 4f);
                    any = true;
                }
                else if (keyHash == HashKey("FmModulationIndex".AsSpan()) || keyHash == HashKey("FMModulationIndex".AsSpan()))
                {
                    tuning.FmModulationIndex = Mathf.Clamp(value, 0f, 4f);
                    any = true;
                }
            }

            return any;
        }

        private static PlayerCriticalProceduralAudioRenderer ResolveRenderer(bool requireReady = true)
        {
            PlayerCriticalProceduralAudioRenderer registeredRenderer = GlobalRegistry.PlayerCriticalAudio;
            if (IsRendererUsable(registeredRenderer, requireReady))
                return registeredRenderer;

#if UNITY_2023_1_OR_NEWER
            PlayerCriticalProceduralAudioRenderer sceneRenderer = UnityEngine.Object.FindAnyObjectByType<PlayerCriticalProceduralAudioRenderer>(FindObjectsInactive.Include);
#else
            PlayerCriticalProceduralAudioRenderer sceneRenderer = UnityEngine.Object.FindObjectOfType<PlayerCriticalProceduralAudioRenderer>();
#endif
            return IsRendererUsable(sceneRenderer, requireReady) ? sceneRenderer : null;
        }

        private static bool IsRendererUsable(PlayerCriticalProceduralAudioRenderer renderer, bool requireReady)
        {
            if (renderer == null)
                return false;

            return requireReady ? renderer.IsPlayerCriticalAudioRuntimeReady : renderer.isActiveAndEnabled;
        }

        private static string ResolveProfileAbsolutePath()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, ProfileAssetPath));
        }

        private static bool AreEqual(
            PlayerCriticalProceduralAudioRenderer.GranularSynthTuningSnapshot left,
            PlayerCriticalProceduralAudioRenderer.GranularSynthTuningSnapshot right)
        {
            return Mathf.Approximately(left.BasePitchScale, right.BasePitchScale) &&
                   Mathf.Approximately(left.GrainLengthScale, right.GrainLengthScale) &&
                   Mathf.Approximately(left.OverlapDensityScale, right.OverlapDensityScale) &&
                   Mathf.Approximately(left.FmModulationIndex, right.FmModulationIndex);
        }

        private static bool TryReadLine(ReadOnlySpan<char> text, ref int cursor, out ReadOnlySpan<char> line)
        {
            if (cursor >= text.Length)
            {
                line = default;
                return false;
            }

            int start = cursor;
            while (cursor < text.Length && text[cursor] != '\n' && text[cursor] != '\r')
                cursor++;

            line = text.Slice(start, cursor - start);
            if (cursor < text.Length && text[cursor] == '\r')
                cursor++;
            if (cursor < text.Length && text[cursor] == '\n')
                cursor++;
            return true;
        }

        private static ReadOnlySpan<char> Trim(ReadOnlySpan<char> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start <= end && char.IsWhiteSpace(value[start]))
                start++;
            while (end >= start && char.IsWhiteSpace(value[end]))
                end--;
            return start > end ? ReadOnlySpan<char>.Empty : value.Slice(start, end - start + 1);
        }

        private static int IndexOfDelimiter(ReadOnlySpan<char> value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == ',' || c == '=' || c == ';')
                    return i;
            }

            return -1;
        }

        private static uint HashKey(ReadOnlySpan<char> key)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < key.Length; i++)
            {
                char c = key[i];
                if (c >= 'A' && c <= 'Z')
                    c = (char)(c + 32);
                if (c == '_' || c == '-' || char.IsWhiteSpace(c))
                    continue;

                hash ^= c;
                hash *= 16777619u;
            }

            return hash;
        }
    }
}
#endif
