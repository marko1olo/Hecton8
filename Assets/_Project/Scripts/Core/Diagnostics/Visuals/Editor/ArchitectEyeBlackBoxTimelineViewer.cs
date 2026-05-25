#if UNITY_EDITOR
using System;
using System.Globalization;
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
using UnityEngine.UIElements;

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
        private VisualElement _timelineRoot;
        private VisualElement[] _timelineBars;
        private Label _pathLabel;
        private Label _emptyLabel;
        private Label[] _detailLabels;
        private SliderInt _frameSlider;
        private Toggle _captureToggle;
        private Toggle _teleportToggle;
        private string _loadedPath = DefaultDumpPath;
        private int _selectedFrame;

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

        private void CreateGUI()
        {
            rootVisualElement.Clear();

            VisualElement toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.height = 24f;
            toolbar.style.flexShrink = 0f;

            Button loadDump = new Button(HandleLoadDumpPressed) { text = "Load Dump" };
            Button loadDefault = new Button(HandleLoadDefaultPressed) { text = "Load Default" };
            _captureToggle = new Toggle("POI Capture") { value = _capturePois };
            _teleportToggle = new Toggle("Click to Teleport AUP") { value = _teleportAup };
            _captureToggle.RegisterValueChangedCallback(OnPoiCaptureChanged);
            _teleportToggle.RegisterValueChangedCallback(OnTeleportToggleChanged);

            toolbar.Add(loadDump);
            toolbar.Add(loadDefault);
            toolbar.Add(_captureToggle);
            toolbar.Add(_teleportToggle);
            rootVisualElement.Add(toolbar);

            _pathLabel = new Label();
            _pathLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            _pathLabel.style.marginLeft = 4f;
            rootVisualElement.Add(_pathLabel);

            _emptyLabel = new Label("No Architect Eye dump loaded.");
            _emptyLabel.style.marginLeft = 4f;
            _emptyLabel.style.marginTop = 6f;
            rootVisualElement.Add(_emptyLabel);

            _frameSlider = new SliderInt("Frame", 0, 0);
            _frameSlider.RegisterValueChangedCallback(OnFrameSliderChanged);
            rootVisualElement.Add(_frameSlider);

            _timelineRoot = new VisualElement();
            _timelineRoot.style.flexDirection = FlexDirection.Row;
            _timelineRoot.style.alignItems = Align.FlexEnd;
            _timelineRoot.style.height = 90f;
            _timelineRoot.style.marginLeft = 4f;
            _timelineRoot.style.marginRight = 4f;
            _timelineRoot.style.backgroundColor = new Color(0.02f, 0.02f, 0.025f, 1f);
            rootVisualElement.Add(_timelineRoot);

            ScrollView details = new ScrollView(ScrollViewMode.Vertical);
            details.style.flexGrow = 1f;
            _detailLabels = new Label[10]; // COLD ALLOC: Label[10] - editor blackbox detail rows - owner: ArchitectEyeBlackBoxTimelineViewer
            for (int i = 0; i < _detailLabels.Length; i++)
            {
                Label row = new Label();
                row.style.marginLeft = 4f;
                row.style.unityTextAlign = TextAnchor.MiddleLeft;
                _detailLabels[i] = row;
                details.Add(row);
            }

            rootVisualElement.Add(details);
            RefreshWindow();
        }

        private void HandleLoadDumpPressed()
        {
            LoadDump(EditorUtility.OpenFilePanel("Architect Eye Dump", Directory.GetCurrentDirectory(), "bin"));
        }

        private void HandleLoadDefaultPressed()
        {
            LoadDump(Path.Combine(Directory.GetCurrentDirectory(), DefaultDumpPath));
        }

        private void OnPoiCaptureChanged(ChangeEvent<bool> evt)
        {
            _capturePois = evt.newValue;
            SceneView.RepaintAll();
        }

        private void OnTeleportToggleChanged(ChangeEvent<bool> evt)
        {
            _teleportAup = evt.newValue;
            SceneView.RepaintAll();
        }

        private void OnFrameSliderChanged(ChangeEvent<int> evt)
        {
            _selectedFrame = evt.newValue;
            RefreshSelectedFrame();
            RefreshTimelineSelection();
            SceneView.RepaintAll();
        }

        private void RefreshWindow()
        {
            if (_pathLabel != null)
                _pathLabel.text = $"Path: {_loadedPath}";

            bool hasFrames = _frames != null && _frames.Length > 0;
            if (_emptyLabel != null)
                _emptyLabel.style.display = hasFrames ? DisplayStyle.None : DisplayStyle.Flex;
            if (_frameSlider != null)
            {
                _frameSlider.style.display = hasFrames ? DisplayStyle.Flex : DisplayStyle.None;
                _frameSlider.lowValue = 0;
                _frameSlider.highValue = hasFrames ? _frames.Length - 1 : 0;
                _frameSlider.SetValueWithoutNotify(math.clamp(_selectedFrame, 0, hasFrames ? _frames.Length - 1 : 0));
            }

            RefreshTimeline();
            RefreshSelectedFrame();
        }

        private void RefreshTimeline()
        {
            if (_timelineRoot == null)
                return;

            _timelineRoot.Clear();
            if (_frames == null || _frames.Length == 0)
            {
                _timelineBars = null;
                _timelineRoot.style.display = DisplayStyle.None;
                return;
            }

            _timelineRoot.style.display = DisplayStyle.Flex;
            _timelineBars = new VisualElement[_frames.Length]; // COLD ALLOC: VisualElement[<=300] - editor timeline bars - owner: ArchitectEyeBlackBoxTimelineViewer
            for (int i = 0; i < _frames.Length; i++)
            {
                ArchitectEyeBlackBoxEntry frame = _frames[i];
                float health = math.saturate(frame.SystemHealth01);
                float fault = math.saturate(frame.NonFiniteCount * 0.2f);
                float barPercent = math.max(3f, 100f * math.max(health, fault));
                Color color = Color.Lerp(new Color(0.1f, 0.5f, 1f, 0.8f), new Color(1f, 0.08f, 0.02f, 0.95f), fault);
                VisualElement bar = new VisualElement();
                bar.style.flexGrow = 1f;
                bar.style.alignSelf = Align.FlexEnd;
                bar.style.marginRight = 1f;
                bar.style.height = Length.Percent(barPercent);
                bar.style.backgroundColor = color;
                _timelineBars[i] = bar;
                _timelineRoot.Add(bar);
            }

            RefreshTimelineSelection();
        }

        private void RefreshTimelineSelection()
        {
            if (_timelineBars == null)
                return;

            for (int i = 0; i < _timelineBars.Length; i++)
            {
                VisualElement bar = _timelineBars[i];
                bool selected = i == _selectedFrame;
                bar.style.borderTopWidth = selected ? 2f : 0f;
                bar.style.borderBottomWidth = selected ? 2f : 0f;
                bar.style.borderLeftWidth = selected ? 1f : 0f;
                bar.style.borderRightWidth = selected ? 1f : 0f;
                bar.style.borderTopColor = Color.white;
                bar.style.borderBottomColor = Color.white;
                bar.style.borderLeftColor = Color.white;
                bar.style.borderRightColor = Color.white;
            }
        }

        private void RefreshSelectedFrame()
        {
            if (_detailLabels == null)
                return;

            if (_frames == null || _frames.Length == 0)
            {
                for (int i = 0; i < _detailLabels.Length; i++)
                    _detailLabels[i].text = string.Empty;
                return;
            }

            _selectedFrame = math.clamp(_selectedFrame, 0, _frames.Length - 1);
            ArchitectEyeBlackBoxEntry frame = _frames[_selectedFrame];
            _detailLabels[0].text = $"Frame: {frame.Frame}";
            _detailLabels[1].text = $"Quads: {frame.QuadCount}";
            _detailLabels[2].text = $"Signal Lanes: {frame.SignalLaneCount}";
            _detailLabels[3].text = $"Signal Pressure: {frame.SignalPressure01:0.000}";
            _detailLabels[4].text = $"Vault Pressure: {frame.VaultPressure01:0.000}";
            _detailLabels[5].text = $"Memory Fragmentation: {frame.MemoryFragmentation01:0.000}";
            _detailLabels[6].text = $"Health: {frame.SystemHealth01:0.000}";
            _detailLabels[7].text = $"Frame Time Ms: {frame.FrameTimeMs:0.000}";
            _detailLabels[8].text = $"Non-Finite: {frame.NonFiniteCount}";
            _detailLabels[9].text = $"Kill Switch Mask: 0x{frame.KillSwitchMask:X8}";
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
            if (_frames == null || _frames.Length != frames.Length)
                _frames = new ArchitectEyeBlackBoxEntry[frames.Length];

            frames.CopyTo(_frames);
            _selectedFrame = math.clamp(_selectedFrame, 0, _frames.Length - 1);
            _loadedPath = path;
            RefreshWindow();
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

            Span<char> id = stackalloc char[32];
            int idLength = FormatPoiId(id, DateTime.UtcNow.Ticks);
            ReadOnlySpan<char> idSpan = id.Slice(0, idLength);
            uint hash = H8DataHash.ComputeFnv1A32(idSpan);
            writer.Write(idSpan);
            writer.Write(',');
            writer.Write(hash);
            writer.Write(',');
            writer.Write(aup.GridX);
            writer.Write(',');
            writer.Write(aup.GridY);
            writer.Write(',');
            writer.Write(aup.GridZ);
            writer.Write(',');
            WriteFloatInvariant(writer, aup.LocalX);
            writer.Write(',');
            WriteFloatInvariant(writer, aup.LocalY);
            writer.Write(',');
            WriteFloatInvariant(writer, aup.LocalZ);
            writer.WriteLine();
        }

        private static void WriteFloatInvariant(TextWriter writer, float value)
        {
            Span<char> buffer = stackalloc char[32];
            if (!value.TryFormat(buffer, out int charsWritten, "R", CultureInfo.InvariantCulture))
            {
                writer.Write(0);
                return;
            }

            writer.Write(buffer.Slice(0, charsWritten));
        }

        private static int FormatPoiId(Span<char> buffer, long ticks)
        {
            const string Prefix = "poi_";
            Prefix.AsSpan().CopyTo(buffer);
            int length = Prefix.Length;
            if (ticks.TryFormat(buffer.Slice(length), out int charsWritten, ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture))
                return length + charsWritten;

            buffer[length++] = '0';
            return length;
        }
    }
}
#endif
