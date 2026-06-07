#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using Hecton8.World;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    public sealed class ScatterDiagnosticsWindow : EditorWindow
    {
        private const int DebugBoundsCapacity = 128;
        private const int ScatterBinaryProfileVersion = 1;
        private const int ScatterBinaryProfileMagic = 0x53425247;
        private const string ScatterProfileCsvHeader = "lod0,lod1,maxDensity,minimumDensityStep";
        private static readonly Bounds[] _visibleBounds = new Bounds[DebugBoundsCapacity];
        private static readonly Bounds[] _culledBounds = new Bounds[DebugBoundsCapacity];

        private HectonIndirectVegetationRenderer _target;
        private Vector2 _scroll;
        private bool _drawFrustumGizmos = true;
        private bool _csvHotReload;
        private string _csvPath = string.Empty;
        private string _profileStatus = "No scatter profile loaded.";
        private DateTime _csvLastWriteUtc = DateTime.MinValue;
        private int _cachedTelemetryFrame = int.MinValue;
        private int _cachedTotal = int.MinValue;
        private int _cachedVisible = int.MinValue;
        private int _cachedFrustum = int.MinValue;
        private int _cachedOcclusion = int.MinValue;
        private int _cachedDensityStep = int.MinValue;
        private float _cachedSystemStress = -1f;
        private bool _cachedOverdraw;
        private string _instancesText = "0";
        private string _visibleText = "0";
        private string _frustumText = "0";
        private string _occlusionText = "0";
        private string _densityStepText = "1";
        private string _systemStressText = "0.00";
        private string _overdrawText = "NO";

        [MenuItem("Hecton8/Rendering/Scatter Diagnostics")]
        private static void Open()
        {
            GetWindow<ScatterDiagnosticsWindow>("Scatter Diagnostics");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGui;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGui;
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            TryHotReloadCsvProfile();
            Repaint();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawTargetPicker();

            if (_target == null)
            {
                EditorGUILayout.HelpBox("Select an active HectonIndirectVegetationRenderer.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }

            DrawTelemetry();
            DrawControls();
            DrawProfileBridge();
            DrawActions();
            EditorGUILayout.EndScrollView();
        }

        private void DrawTargetPicker()
        {
            EditorGUILayout.BeginHorizontal();
            _target = (HectonIndirectVegetationRenderer)EditorGUILayout.ObjectField("Renderer", _target, typeof(HectonIndirectVegetationRenderer), true);
            if (GUILayout.Button("Find", GUILayout.Width(64f)))
                _target = UnityEngine.Object.FindAnyObjectByType<HectonIndirectVegetationRenderer>();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTelemetry()
        {
            HectonIndirectVegetationRenderer.VegetationCullTelemetrySnapshot telemetry;
            bool hasTelemetry = _target.TryGetLatestCullTelemetry(out telemetry);
            int total = hasTelemetry ? telemetry.TotalInstances : _target.BoundInstanceCount;
            int visible = hasTelemetry ? telemetry.VisibleCount : 0;
            int frustum = hasTelemetry ? telemetry.FrustumCulledCount : 0;
            int occlusion = hasTelemetry ? telemetry.OcclusionCulledCount : 0;
            int densityStep = _target.ResolvedDensityDecimationStep;
            float systemStress = _target.SystemStress01;
            bool overdraw = _target.CullOverdrawWarning;
            RefreshTelemetryText(hasTelemetry ? telemetry.FrameIndex : -1, total, visible, frustum, occlusion, densityStep, systemStress, overdraw);

            Rect chartRect = GUILayoutUtility.GetRect(180f, 120f);
            DrawCullChart(chartRect, total, visible, frustum, occlusion);

            EditorGUILayout.LabelField("Instances", _instancesText);
            EditorGUILayout.LabelField("Visible", _visibleText);
            EditorGUILayout.LabelField("Frustum/Distance Culled", _frustumText);
            EditorGUILayout.LabelField("HZB Occluded", _occlusionText);
            EditorGUILayout.LabelField("Density Step", _densityStepText);
            EditorGUILayout.LabelField("System Stress", _systemStressText);
            EditorGUILayout.LabelField("Overdraw Warning", _overdrawText);
        }

        private void RefreshTelemetryText(
            int frame,
            int total,
            int visible,
            int frustum,
            int occlusion,
            int densityStep,
            float systemStress,
            bool overdraw)
        {
            if (_cachedTelemetryFrame == frame &&
                _cachedTotal == total &&
                _cachedVisible == visible &&
                _cachedFrustum == frustum &&
                _cachedOcclusion == occlusion &&
                _cachedDensityStep == densityStep &&
                Mathf.Approximately(_cachedSystemStress, systemStress) &&
                _cachedOverdraw == overdraw)
            {
                return;
            }

            _cachedTelemetryFrame = frame;
            _cachedTotal = total;
            _cachedVisible = visible;
            _cachedFrustum = frustum;
            _cachedOcclusion = occlusion;
            _cachedDensityStep = densityStep;
            _cachedSystemStress = systemStress;
            _cachedOverdraw = overdraw;
            _instancesText = total.ToString();
            _visibleText = visible.ToString();
            _frustumText = frustum.ToString();
            _occlusionText = occlusion.ToString();
            _densityStepText = densityStep.ToString();
            _systemStressText = systemStress.ToString("0.00");
            _overdrawText = overdraw ? "YES" : "NO";
        }

        private static void DrawCullChart(Rect rect, int total, int visible, int frustum, int occlusion)
        {
            EditorGUI.DrawRect(rect, new Color(0.08f, 0.09f, 0.1f, 1f));
            int safeTotal = Mathf.Max(1, total);
            Vector3 center = new Vector3(rect.center.x, rect.center.y, 0f);
            float radius = Mathf.Min(rect.width, rect.height) * 0.42f;
            float startAngle = 0f;
            Handles.BeginGUI();
            DrawPieSegment(center, radius, ref startAngle, visible / (float)safeTotal, new Color(0.18f, 0.72f, 0.42f, 1f));
            DrawPieSegment(center, radius, ref startAngle, frustum / (float)safeTotal, new Color(0.74f, 0.18f, 0.16f, 1f));
            DrawPieSegment(center, radius, ref startAngle, occlusion / (float)safeTotal, new Color(0.20f, 0.36f, 0.84f, 1f));
            if (startAngle < 359.5f)
                DrawPieSegment(center, radius, ref startAngle, (360f - startAngle) / 360f, new Color(0.18f, 0.18f, 0.18f, 1f));
            Handles.EndGUI();
        }

        private static void DrawPieSegment(Vector3 center, float radius, ref float startAngle, float fraction, Color color)
        {
            float angle = Mathf.Clamp01(fraction) * 360f;
            if (angle <= 0.1f)
                return;

            Handles.color = color;
            Vector3 from = Quaternion.Euler(0f, 0f, startAngle) * Vector3.up;
            Handles.DrawSolidArc(center, Vector3.forward, from, angle, radius);
            startAngle += angle;
        }

        private void DrawControls()
        {
            float lod0 = EditorGUILayout.Slider("LOD0 Distance", _target.NearLodDistance, 1f, 120f);
            float lod1 = EditorGUILayout.Slider("LOD1 Distance", _target.FarLodDistance, lod0, 300f);
            float maxDensity = EditorGUILayout.Slider("Max Density", _target.MaxDensity01, 0.05f, 1f);
            int minimumDensityStep = EditorGUILayout.IntSlider("Min Density Step", _target.MinimumDensityDecimationStep, 1, 4);
            if (!Mathf.Approximately(lod0, _target.NearLodDistance) ||
                !Mathf.Approximately(lod1, _target.FarLodDistance) ||
                !Mathf.Approximately(maxDensity, _target.MaxDensity01) ||
                minimumDensityStep != _target.MinimumDensityDecimationStep)
            {
                Undo.RecordObject(_target, "Tune Scatter LOD");
                _target.SetDiagnosticScatterTuning(lod0, lod1, maxDensity, minimumDensityStep);
                EditorUtility.SetDirty(_target);
            }

            bool drawGizmos = EditorGUILayout.Toggle("Draw Debug Bounds", _target.EditorScatterDebugGizmosEnabled);
            if (drawGizmos != _target.EditorScatterDebugGizmosEnabled)
            {
                Undo.RecordObject(_target, "Toggle Scatter Debug Bounds");
                _target.SetEditorScatterDebugGizmosEnabled(drawGizmos);
                EditorUtility.SetDirty(_target);
            }

            _drawFrustumGizmos = drawGizmos;
        }

        private void DrawProfileBridge()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("CSV / Binary Tuning", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Profile", _profileStatus);
            EditorGUILayout.BeginHorizontal();
            _csvPath = EditorGUILayout.TextField("CSV Path", _csvPath);
            if (GUILayout.Button("Pick", GUILayout.Width(52f)))
            {
                string selectedPath = EditorUtility.OpenFilePanel("Select Scatter Tuning CSV", Application.dataPath, "csv");
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    _csvPath = selectedPath;
                    ResetCsvHotReloadClock();
                }
            }
            EditorGUILayout.EndHorizontal();

            bool hotReload = EditorGUILayout.Toggle("Hot Reload CSV", _csvHotReload);
            if (hotReload != _csvHotReload)
            {
                _csvHotReload = hotReload;
                ResetCsvHotReloadClock();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Import CSV"))
                ImportCsvProfile();

            if (GUILayout.Button("Export CSV"))
                ExportCsvProfile();

            if (GUILayout.Button("Bake .h8bin"))
                BakeBinaryProfile();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawActions()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Generate 100x100 Mock"))
            {
                Undo.RecordObject(_target, "Generate Mock Scatter");
                _target.GenerateMockScatterForDiagnostics();
                EditorUtility.SetDirty(_target);
            }

            if (GUILayout.Button("Frame Selected"))
                Selection.activeObject = _target;
            EditorGUILayout.EndHorizontal();
        }

        private void ImportCsvProfile()
        {
            string path = _csvPath;
            if (string.IsNullOrEmpty(path))
                path = EditorUtility.OpenFilePanel("Import Scatter Tuning CSV", Application.dataPath, "csv");

            if (string.IsNullOrEmpty(path))
                return;

            _csvPath = path;
            TryApplyCsvProfile(path, true);
        }

        private void ExportCsvProfile()
        {
            string path = EditorUtility.SaveFilePanel("Export Scatter Tuning CSV", Application.dataPath, "ScatterLodProfile_SHINOBU_09", "csv");
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                using (StreamWriter writer = new StreamWriter(path, false))
                {
                    writer.WriteLine(ScatterProfileCsvHeader);
                    writer.Write(_target.NearLodDistance.ToString("0.###", CultureInfo.InvariantCulture));
                    writer.Write(',');
                    writer.Write(_target.FarLodDistance.ToString("0.###", CultureInfo.InvariantCulture));
                    writer.Write(',');
                    writer.Write(_target.MaxDensity01.ToString("0.###", CultureInfo.InvariantCulture));
                    writer.Write(',');
                    writer.WriteLine(_target.MinimumDensityDecimationStep.ToString(CultureInfo.InvariantCulture));
                }

                _csvPath = path;
                ResetCsvHotReloadClock();
                _profileStatus = "CSV exported.";
                AssetDatabase.Refresh();
            }
            catch (IOException exception)
            {
                _profileStatus = exception.Message;
            }
            catch (UnauthorizedAccessException exception)
            {
                _profileStatus = exception.Message;
            }
        }

        private void BakeBinaryProfile()
        {
            string path = EditorUtility.SaveFilePanel("Bake Scatter Binary Profile", Application.dataPath, "ScatterLodProfile_SHINOBU_09", "h8bin");
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(ScatterBinaryProfileMagic);
                    writer.Write(ScatterBinaryProfileVersion);
                    writer.Write(_target.NearLodDistance);
                    writer.Write(_target.FarLodDistance);
                    writer.Write(_target.MaxDensity01);
                    writer.Write(_target.MinimumDensityDecimationStep);
                }

                _profileStatus = ".h8bin baked.";
                AssetDatabase.Refresh();
            }
            catch (IOException exception)
            {
                _profileStatus = exception.Message;
            }
            catch (UnauthorizedAccessException exception)
            {
                _profileStatus = exception.Message;
            }
        }

        private void TryHotReloadCsvProfile()
        {
            if (!_csvHotReload || _target == null || string.IsNullOrEmpty(_csvPath) || !File.Exists(_csvPath))
                return;

            DateTime writeUtc = File.GetLastWriteTimeUtc(_csvPath);
            if (writeUtc <= _csvLastWriteUtc)
                return;

            _csvLastWriteUtc = writeUtc;
            TryApplyCsvProfile(_csvPath, false);
        }

        private void ResetCsvHotReloadClock()
        {
            _csvLastWriteUtc = File.Exists(_csvPath) ? File.GetLastWriteTimeUtc(_csvPath) : DateTime.MinValue;
        }

        private void TryApplyCsvProfile(string path, bool resetClock)
        {
            float lod0;
            float lod1;
            float maxDensity;
            int minimumDensityStep;
            if (!TryReadCsvProfile(path, out lod0, out lod1, out maxDensity, out minimumDensityStep))
                return;

            Undo.RecordObject(_target, "Import Scatter Tuning CSV");
            _target.SetDiagnosticScatterTuning(lod0, lod1, maxDensity, minimumDensityStep);
            EditorUtility.SetDirty(_target);
            _profileStatus = "CSV applied.";
            if (resetClock)
                ResetCsvHotReloadClock();
        }

        private bool TryReadCsvProfile(
            string path,
            out float lod0,
            out float lod1,
            out float maxDensity,
            out int minimumDensityStep)
        {
            lod0 = 20f;
            lod1 = 50f;
            maxDensity = 1f;
            minimumDensityStep = 1;

            try
            {
                string[] lines = File.ReadAllLines(path);
                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    string line = lines[lineIndex].Trim();
                    if (line.Length == 0 || line[0] == '#')
                        continue;

                    if (line.StartsWith("lod0", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string[] tokens = line.Split(',');
                    if (tokens.Length < 4)
                    {
                        _profileStatus = "CSV requires lod0,lod1,maxDensity,minimumDensityStep.";
                        return false;
                    }

                    if (!float.TryParse(tokens[0], NumberStyles.Float, CultureInfo.InvariantCulture, out lod0) ||
                        !float.TryParse(tokens[1], NumberStyles.Float, CultureInfo.InvariantCulture, out lod1) ||
                        !float.TryParse(tokens[2], NumberStyles.Float, CultureInfo.InvariantCulture, out maxDensity) ||
                        !int.TryParse(tokens[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out minimumDensityStep))
                    {
                        _profileStatus = "CSV parse failed.";
                        return false;
                    }

                    return true;
                }

                _profileStatus = "CSV contains no profile row.";
                return false;
            }
            catch (IOException exception)
            {
                _profileStatus = exception.Message;
                return false;
            }
            catch (UnauthorizedAccessException exception)
            {
                _profileStatus = exception.Message;
                return false;
            }
        }

        private void OnSceneGui(SceneView sceneView)
        {
            if (!_drawFrustumGizmos || _target == null)
                return;

            int count = _target.CopyDebugBoundsNonAlloc(_visibleBounds, _culledBounds);
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
            for (int i = 0; i < count; i++)
            {
                if (_visibleBounds[i].size.sqrMagnitude > 0.0001f)
                {
                    Handles.color = new Color(1f, 0.9f, 0.12f, 0.85f);
                    Handles.DrawWireCube(_visibleBounds[i].center, _visibleBounds[i].size);
                }

                if (_culledBounds[i].size.sqrMagnitude > 0.0001f)
                {
                    Handles.color = new Color(1f, 0.08f, 0.05f, 0.65f);
                    Handles.DrawWireCube(_culledBounds[i].center, _culledBounds[i].size);
                }
            }
        }
    }
}
#endif
