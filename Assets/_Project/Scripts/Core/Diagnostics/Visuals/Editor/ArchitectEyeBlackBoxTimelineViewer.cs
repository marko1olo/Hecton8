#if UNITY_EDITOR
using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Data;
using Hecton8.World;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Core.Diagnostics.Visuals.Editor
{
    public sealed class ArchitectEyeBlackBoxTimelineViewer : EditorWindow
    {
        private const string DefaultDumpPath = "Docs/AgentLogs/Dump_ARCHITECT_EYE_VISUALIZER.bin";
        private const string PoiPath = "Data/Balance/POIs.csv";
        private static bool _capturePois;
        private ArchitectEyeBlackBoxEntry[] _frames;
        private string _loadedPath = DefaultDumpPath;
        private int _selectedFrame;
        private Vector2 _scroll;

        [MenuItem("HECTON-8/Diagnostics/Architect Eye/BlackBox Timeline")]
        private static void Open()
        {
            GetWindow<ArchitectEyeBlackBoxTimelineViewer>("Architect Eye");
        }

        [MenuItem("HECTON-8/Diagnostics/Architect Eye/Toggle POI Breadcrumb Capture")]
        private static void ToggleBreadcrumbCapture()
        {
            _capturePois = !_capturePois;
            SceneView.RepaintAll();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui -= OnSceneGui;
            SceneView.duringSceneGui += OnSceneGui;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGui;
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Load Dump", EditorStyles.toolbarButton, GUILayout.Width(90f)))
                LoadDump(EditorUtility.OpenFilePanel("Architect Eye Dump", Directory.GetCurrentDirectory(), "bin"));
            if (GUILayout.Button("Load Default", EditorStyles.toolbarButton, GUILayout.Width(95f)))
                LoadDump(Path.Combine(Directory.GetCurrentDirectory(), DefaultDumpPath));
            GUILayout.FlexibleSpace();
            _capturePois = GUILayout.Toggle(_capturePois, "POI Capture", EditorStyles.toolbarButton, GUILayout.Width(100f));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("Path", _loadedPath);
            if (_frames == null || _frames.Length == 0)
            {
                EditorGUILayout.HelpBox("No Architect Eye dump loaded.", MessageType.Info);
                return;
            }

            _selectedFrame = EditorGUILayout.IntSlider("Frame", _selectedFrame, 0, _frames.Length - 1);
            DrawTimeline();
            DrawSelectedFrame();
        }

        private void DrawTimeline()
        {
            Rect rect = GUILayoutUtility.GetRect(10f, 90f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.02f, 0.02f, 0.025f, 1f));
            float width = math.max(1f, rect.width / _frames.Length);
            for (int i = 0; i < _frames.Length; i++)
            {
                ArchitectEyeBlackBoxEntry frame = _frames[i];
                float health = math.saturate(frame.SystemHealth01);
                float fault = math.saturate(frame.NonFiniteCount * 0.2f);
                Color color = Color.Lerp(new Color(0.1f, 0.5f, 1f, 0.8f), new Color(1f, 0.08f, 0.02f, 0.95f), fault);
                float barHeight = math.max(2f, rect.height * math.max(health, fault));
                Rect bar = new Rect(rect.x + i * width, rect.yMax - barHeight, math.max(1f, width - 1f), barHeight);
                EditorGUI.DrawRect(bar, color);
            }

            float markerX = rect.x + _selectedFrame * width;
            EditorGUI.DrawRect(new Rect(markerX, rect.y, 2f, rect.height), Color.white);
        }

        private void DrawSelectedFrame()
        {
            ArchitectEyeBlackBoxEntry frame = _frames[_selectedFrame];
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.LabelField("Frame", frame.Frame.ToString());
            EditorGUILayout.LabelField("Quads", frame.QuadCount.ToString());
            EditorGUILayout.LabelField("Signal Lanes", frame.SignalLaneCount.ToString());
            EditorGUILayout.LabelField("Signal Pressure", frame.SignalPressure01.ToString("0.000"));
            EditorGUILayout.LabelField("Vault Pressure", frame.VaultPressure01.ToString("0.000"));
            EditorGUILayout.LabelField("Memory Fragmentation", frame.MemoryFragmentation01.ToString("0.000"));
            EditorGUILayout.LabelField("Health", frame.SystemHealth01.ToString("0.000"));
            EditorGUILayout.LabelField("Frame Time Ms", frame.FrameTimeMs.ToString("0.000"));
            EditorGUILayout.LabelField("Non-Finite", frame.NonFiniteCount.ToString());
            EditorGUILayout.LabelField("Kill Switch Mask", "0x" + frame.KillSwitchMask.ToString("X8"));
            EditorGUILayout.EndScrollView();
        }

        private void LoadDump(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            byte[] bytes = File.ReadAllBytes(path);
            int stride = Marshal.SizeOf<ArchitectEyeBlackBoxEntry>();
            int count = bytes.Length / stride;
            if (count <= 0)
                return;

            ReadOnlySpan<byte> source = bytes.AsSpan(0, count * stride);
            ReadOnlySpan<ArchitectEyeBlackBoxEntry> frames = MemoryMarshal.Cast<byte, ArchitectEyeBlackBoxEntry>(source);
            _frames = frames.ToArray();
            _selectedFrame = math.clamp(_selectedFrame, 0, _frames.Length - 1);
            _loadedPath = path;
            Repaint();
            SceneView.RepaintAll();
        }

        private void OnSceneGui(SceneView sceneView)
        {
            if (_capturePois)
                HandleBreadcrumbClick();

            if (_frames == null || _frames.Length == 0 || _selectedFrame < 0 || _selectedFrame >= _frames.Length)
                return;

            ArchitectEyeBlackBoxEntry frame = _frames[_selectedFrame];
            if (frame.NonFiniteCount <= 0)
                return;

            Handles.color = new Color(1f, 0f, 0f, 0.85f);
            Vector3 position = new Vector3(
                frame.LastFaultPosition.x,
                frame.LastFaultPosition.y,
                frame.LastFaultPosition.z);
            Handles.DrawWireDisc(position, Vector3.up, 2f);
            Handles.DrawLine(position + Vector3.down * 2f, position + Vector3.up * 2f);
            Handles.Label(position + Vector3.up * 2.2f, "ARCHITECT EYE NON-FINITE");
        }

        private static void HandleBreadcrumbClick()
        {
            Event current = Event.current;
            if (current == null || current.type != EventType.MouseDown || current.button != 0 || !current.control)
                return;

            Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
            Vector3 point = UnityEngine.Physics.Raycast(ray, out RaycastHit hit, 10000f)
                ? hit.point
                : ray.origin + ray.direction * 20f;
            SavePoi(point);
            current.Use();
        }

        private static void SavePoi(Vector3 point)
        {
            AbsoluteUniversePosition aup = AbsoluteUniversePosition.FromRuntimePosition(point);
            string directory = Path.GetDirectoryName(PoiPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            bool writeHeader = !File.Exists(PoiPath);
            using StreamWriter writer = new StreamWriter(PoiPath, true);
            if (writeHeader)
                writer.WriteLine("id,hash32,grid_x,grid_y,grid_z,local_x,local_y,local_z");

            string id = "poi_" + DateTime.UtcNow.Ticks.ToString();
            uint hash = H8DataHash.ComputeFnv1A32(id.AsSpan());
            writer.Write(id);
            writer.Write(',');
            writer.Write(hash);
            writer.Write(',');
            writer.Write(aup.GridX);
            writer.Write(',');
            writer.Write(aup.GridY);
            writer.Write(',');
            writer.Write(aup.GridZ);
            writer.Write(',');
            writer.Write(aup.LocalX.ToString("R"));
            writer.Write(',');
            writer.Write(aup.LocalY.ToString("R"));
            writer.Write(',');
            writer.WriteLine(aup.LocalZ.ToString("R"));
        }
    }
}
#endif
