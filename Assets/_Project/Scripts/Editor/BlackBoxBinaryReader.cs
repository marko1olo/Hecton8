#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.EditorTools
{
    public sealed class BlackBoxBinaryReader : EditorWindow
    {
        private const ulong BinaryMagic = 0x00384E4F54434548ul;
        private const int TelemetryEntrySizeBytes = 64;
        private const int CrashExportHeaderSizeBytes = 16;
        private const float SpikeFrameTimeSeconds = 0.033f;
        private const uint NativeFragmentationRiskBit = 1u << 23;
        private const uint StaleBufferCrimeBit = 1u << 24;
        private const uint BlackBoxExportFaultBit = 1u << 25;
        private const uint NativeTransientLeakBit = 1u << 26;
        private const uint BlackBoxExportDroppedBit = 1u << 27;
        private const uint BlackBoxExportSuppressedBit = 1u << 28;
        private const uint MemoryFaultMask = NativeFragmentationRiskBit | StaleBufferCrimeBit | BlackBoxExportFaultBit | NativeTransientLeakBit | BlackBoxExportDroppedBit | BlackBoxExportSuppressedBit;
        private const long MaxPreviewEntries = 3600L;
        private const string FileName = "BLACKBOX_CRASH.bin";
        private const string MissingFileStatus = "BLACKBOX_CRASH.bin not found.";
        private const string InvalidHeaderStatus = "Invalid black box header.";
        private const string TruncatedFileStatus = "Truncated black box payload.";
        private const string CappedFramesPrefix = "Loaded capped frames: ";
        private const string LoadedFramesPrefix = "Loaded frames: ";

        private readonly List<TelemetryEntry> _entries = new List<TelemetryEntry>(4096);
        private string _path;
        private string _status;
        private TextField _pathField;
        private Label _statusLabel;
        private PreviewElement _previewElement;

        [StructLayout(LayoutKind.Explicit, Size = TelemetryEntrySizeBytes)]
        private struct TelemetryEntry
        {
            [FieldOffset(0)]
            public uint FrameIndex;
            [FieldOffset(4)]
            public uint SystemMask;
            [FieldOffset(8)]
            public float DeltaTime;
            [FieldOffset(12)]
            public float LatencyMs;
            [FieldOffset(16)]
            public float GpuFrameTime;
            [FieldOffset(20)]
            public float MemoryUsedMb;
            [FieldOffset(24)]
            public Vector3 PlayerAup;
            [FieldOffset(36)]
            public uint ActiveChunkCount;
            [FieldOffset(40)]
            public uint ErrorFlags;
            [FieldOffset(44)]
            public uint ExportReason;
            [FieldOffset(48)]
            public uint AupShiftSequence;
            [FieldOffset(52)]
            public uint Payload0;
            [FieldOffset(56)]
            public uint Payload1;
            [FieldOffset(60)]
            public uint LastOriginShiftFrame;

            public uint VelocityPacked => Payload0;
            public uint GcAllocBytes => Payload1;
            public uint AllocationHash => Payload0;
            public uint PackedMegabytes => Payload1;
        }

        [MenuItem("Hecton8/Forensics/Black Box Binary Reader")]
        private static void Open()
        {
            GetWindow<BlackBoxBinaryReader>("Black Box");
        }

        private void OnEnable()
        {
            _path = Path.Combine(Application.persistentDataPath, FileName);
            SyncVisualState();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 6f;
            rootVisualElement.style.paddingRight = 6f;
            rootVisualElement.style.paddingTop = 6f;

            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 4f;
            rootVisualElement.Add(row);

            _pathField = new TextField();
            _pathField.value = _path;
            _pathField.style.flexGrow = 1f;
            row.Add(_pathField);

            Button loadButton = new Button(LoadFromField);
            loadButton.text = "Load";
            loadButton.style.width = 64f;
            row.Add(loadButton);

            _statusLabel = new Label();
            _statusLabel.style.marginBottom = 4f;
            rootVisualElement.Add(_statusLabel);

            _previewElement = new PreviewElement(_entries);
            _previewElement.style.height = 360f;
            _previewElement.style.flexGrow = 1f;
            _previewElement.style.backgroundColor = Color.black;
            rootVisualElement.Add(_previewElement);

            SyncVisualState();
        }

        private void LoadFromField()
        {
            _path = _pathField != null ? _pathField.value : _path;
            LoadFile(_path);
        }

        private void SyncVisualState()
        {
            if (_pathField != null)
                _pathField.value = _path;
            if (_statusLabel != null)
                _statusLabel.text = _status;
            _previewElement?.MarkDirtyRepaint();
        }

        private void LoadFile(string path)
        {
            _entries.Clear();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                _status = MissingFileStatus;
                SyncVisualState();
                return;
            }

            using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (stream.Length < CrashExportHeaderSizeBytes)
                {
                    _status = InvalidHeaderStatus;
                    SyncVisualState();
                    return;
                }

                using (System.IO.BinaryReader reader = new System.IO.BinaryReader(stream))
                {
                    ulong magic = reader.ReadUInt64();
                    uint entryCount = reader.ReadUInt32();
                    uint structSize = reader.ReadUInt32();
                    if (magic != BinaryMagic || structSize != TelemetryEntrySizeBytes)
                    {
                        _status = InvalidHeaderStatus;
                        SyncVisualState();
                        return;
                    }

                    long remainingBytes = stream.Length - CrashExportHeaderSizeBytes;
                    long rawReadableEntryCount = remainingBytes > 0L
                        ? System.Math.Min((long)entryCount, remainingBytes / TelemetryEntrySizeBytes)
                        : 0L;
                    bool payloadTruncated = rawReadableEntryCount < entryCount;
                    long readableEntryCount = rawReadableEntryCount;
                    bool cappedPreview = readableEntryCount > MaxPreviewEntries;
                    if (readableEntryCount > MaxPreviewEntries)
                        readableEntryCount = MaxPreviewEntries;

                    for (long i = 0; i < readableEntryCount; i++)
                        _entries.Add(ReadEntry(reader));

                    _status = payloadTruncated
                        ? TruncatedFileStatus
                        : cappedPreview
                            ? CappedFramesPrefix + _entries.Count
                        : LoadedFramesPrefix + _entries.Count;
                }
            }

            SyncVisualState();
        }

        private static TelemetryEntry ReadEntry(System.IO.BinaryReader reader)
        {
            TelemetryEntry entry = default;
            entry.FrameIndex = reader.ReadUInt32();
            entry.SystemMask = reader.ReadUInt32();
            entry.DeltaTime = reader.ReadSingle();
            entry.LatencyMs = reader.ReadSingle();
            entry.GpuFrameTime = reader.ReadSingle();
            entry.MemoryUsedMb = reader.ReadSingle();
            entry.PlayerAup = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            entry.ActiveChunkCount = reader.ReadUInt32();
            entry.ErrorFlags = reader.ReadUInt32();
            entry.ExportReason = reader.ReadUInt32();
            entry.AupShiftSequence = reader.ReadUInt32();
            entry.Payload0 = reader.ReadUInt32();
            entry.Payload1 = reader.ReadUInt32();
            entry.LastOriginShiftFrame = reader.ReadUInt32();
            return entry;
        }

        private sealed class PreviewElement : VisualElement
        {
            private readonly List<TelemetryEntry> _entries;

            public PreviewElement(List<TelemetryEntry> entries)
            {
                _entries = entries;
                generateVisualContent += DrawPreview;
            }

            private void DrawPreview(MeshGenerationContext context)
            {
                if (_entries.Count <= 0)
                    return;

                ResolveBounds(out Vector2 min, out Vector2 max);
                Vector2 span = max - min;
                if (span.x < 0.001f)
                    span.x = 1f;
                if (span.y < 0.001f)
                    span.y = 1f;

                Painter2D painter = context.painter2D;
                Rect rect = contentRect;
                Vector2 previous = Vector2.zero;
                bool hasPrevious = false;
                for (int i = 0; i < _entries.Count; i++)
                {
                    TelemetryEntry entry = _entries[i];
                    if (!IsFinite(entry.PlayerAup))
                        continue;

                    Vector2 normalized = new Vector2(
                        (entry.PlayerAup.x - min.x) / span.x,
                        (entry.PlayerAup.z - min.y) / span.y);
                    Vector2 point = new Vector2(
                        rect.x + normalized.x * rect.width,
                        rect.yMax - normalized.y * rect.height);

                    if (hasPrevious)
                    {
                        bool memoryFault = (entry.ErrorFlags & MemoryFaultMask) != 0u;
                        painter.strokeColor = entry.DeltaTime > SpikeFrameTimeSeconds
                            ? Color.red
                            : memoryFault
                                ? Color.yellow
                                : Color.green;
                        painter.lineWidth = 2f;
                        painter.BeginPath();
                        painter.MoveTo(previous);
                        painter.LineTo(point);
                        painter.Stroke();
                    }

                    if (entry.DeltaTime > SpikeFrameTimeSeconds ||
                        (entry.ErrorFlags & MemoryFaultMask) != 0u)
                    {
                        painter.fillColor = entry.DeltaTime > SpikeFrameTimeSeconds ? Color.red : Color.yellow;
                        painter.BeginPath();
                        painter.Arc(point, 3f, 0f, 360f);
                        painter.Fill();
                    }

                    previous = point;
                    hasPrevious = true;
                }
            }

            private void ResolveBounds(out Vector2 min, out Vector2 max)
            {
                min = new Vector2(float.MaxValue, float.MaxValue);
                max = new Vector2(float.MinValue, float.MinValue);
                bool hasPoint = false;
                for (int i = 0; i < _entries.Count; i++)
                {
                    Vector3 aup = _entries[i].PlayerAup;
                    if (!IsFinite(aup))
                        continue;

                    min.x = Mathf.Min(min.x, aup.x);
                    min.y = Mathf.Min(min.y, aup.z);
                    max.x = Mathf.Max(max.x, aup.x);
                    max.y = Mathf.Max(max.y, aup.z);
                    hasPoint = true;
                }

                if (!hasPoint)
                {
                    min = Vector2.zero;
                    max = Vector2.one;
                }
            }

            private static bool IsFinite(Vector3 value)
            {
                return !float.IsNaN(value.x) &&
                    !float.IsInfinity(value.x) &&
                    !float.IsNaN(value.y) &&
                    !float.IsInfinity(value.y) &&
                    !float.IsNaN(value.z) &&
                    !float.IsInfinity(value.z);
            }
        }
    }
}
#endif
