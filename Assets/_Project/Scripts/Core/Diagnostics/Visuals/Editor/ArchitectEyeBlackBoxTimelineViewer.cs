#if UNITY_EDITOR
using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Data;
using Hecton8.World;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Core.Diagnostics.Visuals.Editor
{
    public sealed class ArchitectEyeBlackBoxTimelineViewer : EditorWindow
    {
        private const string DefaultDumpPath = ArchitectEyeVisualizer.BlackBoxDumpRelativePath;
        private const string PoiPath = "Data/Balance/POIs.csv";
        private const float SectorSizeMeters = HectonPhysicsContract.AupSectorSizeMetersFloat;
        private const int MaxTimelineFrames = 300;
        private static bool _capturePois;
        private static bool _teleportAup;
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
            _teleportAup = GUILayout.Toggle(_teleportAup, "Click to Teleport AUP", EditorStyles.toolbarButton, GUILayout.Width(150f));
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

            int stride = UnsafeUtility.SizeOf<ArchitectEyeBlackBoxEntry>();
            int maxBytes = stride * MaxTimelineFrames;
            long fileLength = new FileInfo(path).Length;
            long cappedLength = math.min((long)maxBytes, math.max(0L, fileLength));
            int readableBytes = (int)cappedLength;
            byte[] bytes = new byte[readableBytes];
            int read = 0;
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan))
            {
                while (read < readableBytes)
                {
                    int chunk = stream.Read(bytes, read, readableBytes - read);
                    if (chunk <= 0)
                        break;

                    read += chunk;
                }
            }

            int count = read / stride;
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
            DrawAupSectorGrid(sceneView);
            if (_teleportAup)
                HandleAupTeleportClick();

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

        private static void DrawAupSectorGrid(SceneView sceneView)
        {
            Camera camera = sceneView != null ? sceneView.camera : null;
            Vector3 anchor = camera != null ? camera.transform.position : Vector3.zero;
            float baseX = Mathf.Floor(anchor.x / SectorSizeMeters) * SectorSizeMeters;
            float baseZ = Mathf.Floor(anchor.z / SectorSizeMeters) * SectorSizeMeters;
            float y = anchor.y;

            Handles.color = new Color(0.1f, 0.45f, 0.7f, 0.18f);
            for (int x = -2; x <= 2; x++)
            {
                float lineX = baseX + x * SectorSizeMeters;
                Handles.DrawLine(new Vector3(lineX, y, baseZ - SectorSizeMeters * 2f), new Vector3(lineX, y, baseZ + SectorSizeMeters * 2f));
            }

            for (int z = -2; z <= 2; z++)
            {
                float lineZ = baseZ + z * SectorSizeMeters;
                Handles.DrawLine(new Vector3(baseX - SectorSizeMeters * 2f, y, lineZ), new Vector3(baseX + SectorSizeMeters * 2f, y, lineZ));
            }

            Handles.color = new Color(0.0f, 0.9f, 1f, 0.25f);
            for (int sx = -1; sx <= 1; sx++)
            {
                for (int sz = -1; sz <= 1; sz++)
                {
                    Vector3 center = new Vector3(baseX + sx * SectorSizeMeters, y, baseZ + sz * SectorSizeMeters);
                    Vector3 a = center + new Vector3(-SectorSizeMeters * 0.5f, 0f, -SectorSizeMeters * 0.5f);
                    Vector3 b = center + new Vector3(SectorSizeMeters * 0.5f, 0f, -SectorSizeMeters * 0.5f);
                    Vector3 c = center + new Vector3(SectorSizeMeters * 0.5f, 0f, SectorSizeMeters * 0.5f);
                    Vector3 d = center + new Vector3(-SectorSizeMeters * 0.5f, 0f, SectorSizeMeters * 0.5f);
                    Handles.DrawLine(a, b);
                    Handles.DrawLine(b, c);
                    Handles.DrawLine(c, d);
                    Handles.DrawLine(d, a);
                }
            }
        }

        private static void HandleAupTeleportClick()
        {
            Event current = Event.current;
            if (current == null || current.type != EventType.MouseDown || current.button != 0 || !current.control)
                return;

            Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
            Vector3 point = UnityEngine.Physics.Raycast(ray, out RaycastHit hit, 10000f)
                ? hit.point
                : ray.origin + ray.direction * 20f;
            TryTeleportPlayer(point);
            PublishTeleportPreview(point);
            current.Use();
        }

        private static void TryTeleportPlayer(Vector3 point)
        {
            if (!Application.isPlaying)
                return;

            IPlayerRuntimeContext player = GlobalRegistry.Player;
            Transform playerTransform = player != null ? player.PlayerTransform : null;
            if (playerTransform == null)
                return;

            Rigidbody body = player.PlayerRigidbody;
            if (body != null)
            {
                body.position = point;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            playerTransform.position = point;
        }

        private static void PublishTeleportPreview(Vector3 point)
        {
            if (!Application.isPlaying)
                return;

            DebugSignal signal = default;
            signal.Kind = (uint)DebugSignalKind.AupTeleportPreview;
            signal.Position = (float3)point;
            signal.Frame = unchecked((uint)Mathf.Max(0, Time.frameCount));
            ArchitectEyeDebugBus.Push(in signal);
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
