#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Hecton8.Graphics.Culling.Editor
{
    public sealed class TBDRPipelineTunerWindow : EditorWindow
    {
        private const string WindowTitle = "TBDR Pipeline Tuner";
        private const string UberNoirPath = "Assets/_Project/Art/Shaders/Hecton8_UberNoir.hlsl";
        private const int DefaultMockRunCount = 150000;

        private TBDRPipelineSurgeonRuntime _target;
        private Vector2 _scroll;
        private string _csvPath = "Data/Rendering/gpu_budgets.csv";
        private bool _csvHotReload;
        private bool _showSorting;
        private uint _hardVertexCap = 800000u;
        private int _transparentQuadLimit = 5000;
        private float _frustumSqueezeAngle = 12f;
        private string _status = "No runtime selected.";
        private DateTime _lastCsvProbeUtc;

        [MenuItem("Hecton8/Rendering/TBDR Pipeline Tuner")]
        private static void Open()
        {
            GetWindow<TBDRPipelineTunerWindow>(WindowTitle);
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (_target != null && _csvHotReload)
            {
                DateTime now = DateTime.UtcNow;
                if ((now - _lastCsvProbeUtc).TotalSeconds > 0.25)
                {
                    _lastCsvProbeUtc = now;
                    _target.SetCsvPath(_csvPath);
                    if (_target.PollBudgetCsvOverride())
                        PullSnapshot();
                }
            }

            Repaint();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawTargetPicker();
            DrawLimits();
            DrawLiveChart();
            DrawCsvControls();
            DrawActions();
            DrawAudit();
            EditorGUILayout.HelpBox(_status, MessageType.Info);
            EditorGUILayout.EndScrollView();
        }

        private void DrawTargetPicker()
        {
            EditorGUILayout.BeginHorizontal();
            _target = (TBDRPipelineSurgeonRuntime)EditorGUILayout.ObjectField("Runtime", _target, typeof(TBDRPipelineSurgeonRuntime), true);
            if (GUILayout.Button("Find", GUILayout.Width(64f)))
            {
                _target = FindAnyObjectByType<TBDRPipelineSurgeonRuntime>();
                PullSnapshot();
            }

            if (GUILayout.Button("Init", GUILayout.Width(64f)) && _target != null)
            {
                _target.Initialize();
                PullSnapshot();
                _status = "Runtime initialized.";
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawLimits()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Hardware Limits", EditorStyles.boldLabel);
            _hardVertexCap = (uint)EditorGUILayout.IntSlider("Hard Vertex Cap", (int)Mathf.Min(_hardVertexCap, int.MaxValue), 100000, 4000000);
            _transparentQuadLimit = EditorGUILayout.IntSlider("Transparent Quad Limit", _transparentQuadLimit, 500, 20000);
            _frustumSqueezeAngle = EditorGUILayout.Slider("Frustum Squeeze Angle", _frustumSqueezeAngle, 0f, 15f);
            _showSorting = EditorGUILayout.Toggle("Show Sorting", _showSorting);

            if (_target != null && GUILayout.Button("Write To Vault"))
            {
                Undo.RecordObject(_target, "Tune TBDR Pipeline");
                _target.ApplyEditorLimits(_hardVertexCap, _transparentQuadLimit, _frustumSqueezeAngle);
                _target.EditorShowSorting = _showSorting;
                EditorUtility.SetDirty(_target);
                _status = "Limits written to unmanaged vault.";
            }
        }

        private void DrawLiveChart()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Live Budget", EditorStyles.boldLabel);
            TBDRTunerSnapshot snapshot = default;
            bool hasSnapshot = _target != null && _target.TryGetTunerSnapshot(out snapshot);
            if (!hasSnapshot)
            {
                Rect emptyRect = GUILayoutUtility.GetRect(220f, 70f);
                EditorGUI.DrawRect(emptyRect, new Color(0.08f, 0.08f, 0.08f, 1f));
                return;
            }

            Rect rect = GUILayoutUtility.GetRect(260f, 96f);
            EditorGUI.DrawRect(rect, new Color(0.07f, 0.08f, 0.09f, 1f));
            DrawBar(rect, 8f, "Vertices", snapshot.CurrentVisibleVertices, Mathf.Max(1f, snapshot.HardVertexCap), new Color(0.16f, 0.72f, 0.42f, 1f));
            DrawBar(rect, 34f, "VRAM est", snapshot.EstimatedVramMb, 512f, new Color(0.18f, 0.45f, 0.90f, 1f));
            DrawBar(rect, 60f, "Tile pressure", snapshot.TilePressure, 1f, new Color(0.92f, 0.52f, 0.12f, 1f));
            EditorGUILayout.LabelField("Sort ms", _target.LastSortComputeTimeMs().ToString("0.000"));
            EditorGUILayout.LabelField("Sorted visible", _target.LastSortedCount().ToString());
        }

        private static void DrawBar(Rect container, float yOffset, string label, float value, float max, Color color)
        {
            Rect labelRect = new Rect(container.x + 8f, container.y + yOffset, 92f, 18f);
            Rect barRect = new Rect(container.x + 104f, container.y + yOffset + 2f, container.width - 116f, 14f);
            float fill = Mathf.Clamp01(value / Mathf.Max(0.0001f, max));
            EditorGUI.LabelField(labelRect, label);
            EditorGUI.DrawRect(barRect, new Color(0.15f, 0.15f, 0.15f, 1f));
            EditorGUI.DrawRect(new Rect(barRect.x, barRect.y, barRect.width * fill, barRect.height), color);
        }

        private void DrawCsvControls()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("CSV Override", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            _csvPath = EditorGUILayout.TextField("gpu_budgets.csv", _csvPath);
            if (GUILayout.Button("Pick", GUILayout.Width(52f)))
            {
                string selected = EditorUtility.OpenFilePanel("Select gpu_budgets.csv", Application.dataPath, "csv");
                if (!string.IsNullOrEmpty(selected))
                    _csvPath = selected;
            }
            EditorGUILayout.EndHorizontal();
            _csvHotReload = EditorGUILayout.Toggle("Hot Reload CSV", _csvHotReload);
            if (_target != null && GUILayout.Button("Ingest Now"))
            {
                _target.SetCsvPath(_csvPath);
                _status = _target.PollBudgetCsvOverride() ? "CSV override applied." : "CSV override not applied.";
                PullSnapshot();
            }
        }

        private void DrawActions()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.BeginHorizontal();
            if (_target != null && GUILayout.Button("Run Mock 150K"))
            {
                _target.RunMockPipelineOnce(DefaultMockRunCount);
                PullSnapshot();
                _status = "Mock scatter throttled through radix + vertex cap.";
            }

            if (GUILayout.Button("Audit UberNoir Half"))
                _status = UberNoirHalfPrecisionValidator.AuditUberNoirShader();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawAudit()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("DTO Layout", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("VertexBudgetDTO", "0:uint Max, 4:uint Current, 8:float Pressure, 12:uint pad = 16B");
            EditorGUILayout.LabelField("TileSpillWarningDTO", "0:float Overdraw, 4:uint Culled, 8:ulong pad = 16B");
        }

        private void PullSnapshot()
        {
            if (_target == null)
                return;

            TBDRTunerSnapshot snapshot;
            if (!_target.TryGetTunerSnapshot(out snapshot))
                return;

            _hardVertexCap = snapshot.HardVertexCap;
            _transparentQuadLimit = (int)Mathf.Max(1, snapshot.TransparentQuadLimit);
            _frustumSqueezeAngle = snapshot.FrustumSqueezeDegrees;
            _showSorting = _target.EditorShowSorting;
            _csvPath = _target.GetCsvPath();
        }
    }

    internal sealed class UberNoirHalfPrecisionBuildGate : IPreprocessBuildWithReport
    {
        public int callbackOrder => -4520;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android &&
                report.summary.platform != BuildTarget.iOS &&
                report.summary.platform != BuildTarget.tvOS)
            {
                return;
            }

            string result = UberNoirHalfPrecisionValidator.AuditUberNoirShader();
            if (!result.StartsWith("PASS", StringComparison.Ordinal))
                throw new BuildFailedException(result);
        }
    }

    internal static class UberNoirHalfPrecisionValidator
    {
        private const string ShaderPath = "Assets/_Project/Art/Shaders/Hecton8_UberNoir.hlsl";

        internal static string AuditUberNoirShader()
        {
            if (!File.Exists(ShaderPath))
                return "BLOCKED: UberNoir shader not found at " + ShaderPath;

            string[] lines = File.ReadAllLines(ShaderPath);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int commentIndex = line.IndexOf("//", StringComparison.Ordinal);
                int scanLength = commentIndex >= 0 ? commentIndex : line.Length;
                if (!TouchesColorNormalOrUv(line, scanLength))
                    continue;

                if (ContainsFloatToken(line, scanLength))
                {
                    return "BLOCKED: UberNoir half precision violation at " +
                           ShaderPath +
                           ":" +
                           (i + 1).ToString() +
                           ". Color/normal/UV math must use half.";
                }
            }

            return "PASS: UberNoir color/normal/UV lanes avoid float tokens.";
        }

        private static bool TouchesColorNormalOrUv(string line, int scanLength)
        {
            return ContainsToken(line, scanLength, "color") ||
                   ContainsToken(line, scanLength, "Color") ||
                   ContainsToken(line, scanLength, "normal") ||
                   ContainsToken(line, scanLength, "Normal") ||
                   ContainsToken(line, scanLength, "uv") ||
                   ContainsToken(line, scanLength, "UV");
        }

        private static bool ContainsFloatToken(string line, int scanLength)
        {
            return ContainsToken(line, scanLength, "float") ||
                   ContainsToken(line, scanLength, "float2") ||
                   ContainsToken(line, scanLength, "float3") ||
                   ContainsToken(line, scanLength, "float4");
        }

        private static bool ContainsToken(string line, int scanLength, string token)
        {
            int cursor = 0;
            while (cursor < scanLength)
            {
                int index = line.IndexOf(token, cursor, scanLength - cursor, StringComparison.Ordinal);
                if (index < 0)
                    return false;

                int end = index + token.Length;
                bool left = index == 0 || !IsIdentifierChar(line[index - 1]);
                bool right = end >= scanLength || !IsIdentifierChar(line[end]);
                if (left && right)
                    return true;

                cursor = end;
            }

            return false;
        }

        private static bool IsIdentifierChar(char value)
        {
            return (value >= 'a' && value <= 'z') ||
                   (value >= 'A' && value <= 'Z') ||
                   (value >= '0' && value <= '9') ||
                   value == '_';
        }
    }
}
#endif
