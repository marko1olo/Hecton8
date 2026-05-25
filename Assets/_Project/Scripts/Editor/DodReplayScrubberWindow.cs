#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Editor-only time scrubber for deterministic replay snapshot headers.
    /// </summary>
    public sealed class DodReplayScrubberWindow : EditorWindow
    {
        private const ulong ReplayMagic = 0x48385245504C4159ul;
        private const int HeaderSizeBytes = 128;
        private const int SegmentHeaderSizeBytes = 64;
        private const int MaxIndexedHeaders = 4096;
        private const int MaxCompareSegments = 1024;
        private const string ReplayFileName = "replay.bin";
        private const string MissingFileStatus = "replay.bin not found.";
        private const string EmptyReplayStatus = "No replay headers found.";
        private const string LoadedPrefix = "Indexed snapshots: ";
        private const string CompareNoNextStatus = "Compare: no next snapshot.";
        private const string CompareNoPayloadDeltaStatus = "Compare: no payload byte delta found.";

        private readonly List<ReplayHeaderPreview> _headers = new List<ReplayHeaderPreview>(512);
        private readonly List<ReplaySegmentPreview> _compareA = new List<ReplaySegmentPreview>(128);
        private readonly List<ReplaySegmentPreview> _compareB = new List<ReplaySegmentPreview>(128);
        private string _path;
        private string _status;
        private TextField _pathField;
        private SliderInt _scrubber;
        private Label _statusLabel;
        private Label _frameLabel;
        private Label _payloadLabel;
        private Label _faultLabel;
        private Label _compareLabel;

        [MenuItem("Hecton8/Forensics/DOD Replay Scrubber")]
        private static void Open()
        {
            GetWindow<DodReplayScrubberWindow>("DOD Replay");
        }

        private void OnEnable()
        {
            _path = Path.Combine(Application.persistentDataPath, ReplayFileName);
            SyncVisualState();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 6f;
            rootVisualElement.style.paddingRight = 6f;
            rootVisualElement.style.paddingTop = 6f;

            VisualElement pathRow = new VisualElement();
            pathRow.style.flexDirection = FlexDirection.Row;
            pathRow.style.marginBottom = 4f;
            rootVisualElement.Add(pathRow);

            _pathField = new TextField();
            _pathField.value = _path;
            _pathField.style.flexGrow = 1f;
            pathRow.Add(_pathField);

            Button refreshButton = new Button(LoadFromField);
            refreshButton.text = "Refresh";
            refreshButton.style.width = 82f;
            pathRow.Add(refreshButton);

            Button compareButton = new Button(CompareSelectedWithNext);
            compareButton.text = "Compare Next";
            compareButton.style.width = 112f;
            pathRow.Add(compareButton);

            _scrubber = new SliderInt(0, 0);
            _scrubber.label = "Frame";
            _scrubber.RegisterValueChangedCallback(evt => ShowHeader(evt.newValue));
            rootVisualElement.Add(_scrubber);

            _statusLabel = new Label();
            _frameLabel = new Label();
            _payloadLabel = new Label();
            _faultLabel = new Label();
            _compareLabel = new Label();
            rootVisualElement.Add(_statusLabel);
            rootVisualElement.Add(_frameLabel);
            rootVisualElement.Add(_payloadLabel);
            rootVisualElement.Add(_faultLabel);
            rootVisualElement.Add(_compareLabel);

            SyncVisualState();
        }

        private void LoadFromField()
        {
            _path = _pathField != null ? _pathField.value : _path;
            LoadFile(_path);
        }

        private void LoadFile(string path)
        {
            _headers.Clear();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                _status = MissingFileStatus;
                SyncVisualState();
                return;
            }

            using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                while (stream.Position <= stream.Length - HeaderSizeBytes && _headers.Count < MaxIndexedHeaders)
                {
                    long headerOffset = stream.Position;
                    ulong magic = reader.ReadUInt64();
                    if (magic != ReplayMagic)
                    {
                        stream.Position = headerOffset + 1L;
                        continue;
                    }

                    ReplayHeaderPreview header = ReadHeader(reader, headerOffset);
                    if (!IsHeaderPlausible(header))
                    {
                        stream.Position = headerOffset + 1L;
                        continue;
                    }

                    _headers.Add(header);
                    long nextOffset = headerOffset + HeaderSizeBytes + header.PayloadBytes;
                    stream.Position = nextOffset > headerOffset && nextOffset <= stream.Length
                        ? nextOffset
                        : headerOffset + 1L;
                }
            }

            _status = _headers.Count > 0
                ? LoadedPrefix + _headers.Count
                : EmptyReplayStatus;
            SyncVisualState();
        }

        private void SyncVisualState()
        {
            if (_pathField != null)
                _pathField.value = _path;

            if (_statusLabel != null)
                _statusLabel.text = _status;

            if (_scrubber != null)
            {
                int max = _headers.Count > 0 ? _headers.Count - 1 : 0;
                _scrubber.lowValue = 0;
                _scrubber.highValue = max;
                _scrubber.SetValueWithoutNotify(Mathf.Clamp(_scrubber.value, 0, max));
                _scrubber.SetEnabled(_headers.Count > 0);
            }

            ShowHeader(_scrubber != null ? _scrubber.value : 0);
        }

        private void ShowHeader(int index)
        {
            if (_frameLabel == null || _payloadLabel == null || _faultLabel == null)
                return;

            if (_headers.Count <= 0 || index < 0 || index >= _headers.Count)
            {
                _frameLabel.text = "Frame: -";
                _payloadLabel.text = "Payload: -";
                _faultLabel.text = "Fault: -";
                if (_compareLabel != null)
                    _compareLabel.text = "Compare: -";
                return;
            }

            ReplayHeaderPreview header = _headers[index];
            _frameLabel.text = "Frame: " + header.FrameIndex.ToString(CultureInfo.InvariantCulture) +
                               " | Sequence: " + header.SnapshotSequence.ToString(CultureInfo.InvariantCulture) +
                               " | Segments: " + header.SegmentCount.ToString(CultureInfo.InvariantCulture);
            _payloadLabel.text = "Payload: " + header.PayloadBytes.ToString(CultureInfo.InvariantCulture) +
                                 " B | Source: " + header.TotalSourceBytes.ToString(CultureInfo.InvariantCulture) +
                                 " B | Dropped: " + header.DroppedBytes.ToString(CultureInfo.InvariantCulture) + " B";
            _faultLabel.text = "SubjectHash: 0x" + header.SubjectHash.ToString("X8", CultureInfo.InvariantCulture) +
                               " | ErrorCode: 0x" + header.ErrorCode.ToString("X8", CultureInfo.InvariantCulture) +
                               " | Seed: 0x" + header.ReplaySeed.ToString("X8", CultureInfo.InvariantCulture);
        }

        private void CompareSelectedWithNext()
        {
            if (_compareLabel == null)
                return;

            int index = _scrubber != null ? _scrubber.value : 0;
            if (_headers.Count <= 1 || index < 0 || index >= _headers.Count - 1)
            {
                _compareLabel.text = CompareNoNextStatus;
                return;
            }

            if (!LoadSnapshotSegments(index, _compareA) || !LoadSnapshotSegments(index + 1, _compareB))
            {
                _compareLabel.text = "Compare: unable to read snapshot payload.";
                return;
            }

            for (int i = 0; i < _compareA.Count; i++)
            {
                ReplaySegmentPreview left = _compareA[i];
                if (left.Payload == null || left.Payload.Length == 0)
                    continue;

                int rightIndex = FindMatchingSegment(_compareB, left.OwnerHash, left.LabelHash);
                if (rightIndex < 0)
                    continue;

                ReplaySegmentPreview right = _compareB[rightIndex];
                if (right.Payload == null || right.Payload.Length == 0)
                    continue;

                int diffOffset = FindFirstByteDiff(left.Payload, right.Payload);
                if (diffOffset >= 0)
                {
                    _compareLabel.text =
                        "Compare: Frame " + _headers[index].FrameIndex.ToString(CultureInfo.InvariantCulture) +
                        " -> " + _headers[index + 1].FrameIndex.ToString(CultureInfo.InvariantCulture) +
                        " | Owner 0x" + left.OwnerHash.ToString("X8", CultureInfo.InvariantCulture) +
                        " Label 0x" + left.LabelHash.ToString("X8", CultureInfo.InvariantCulture) +
                        " Byte " + diffOffset.ToString(CultureInfo.InvariantCulture);
                    return;
                }
            }

            _compareLabel.text = CompareNoPayloadDeltaStatus;
        }

        private bool LoadSnapshotSegments(int headerIndex, List<ReplaySegmentPreview> target)
        {
            target.Clear();
            if (headerIndex < 0 || headerIndex >= _headers.Count)
                return false;

            ReplayHeaderPreview header = _headers[headerIndex];
            if (header.SegmentCount > (uint)MaxCompareSegments)
                return false;

            using (FileStream stream = File.Open(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                long segmentOffset = header.FileOffset + HeaderSizeBytes;
                if (segmentOffset < 0L || segmentOffset >= stream.Length)
                    return false;

                stream.Position = segmentOffset;
                int segmentCount = (int)header.SegmentCount;
                for (int i = 0; i < segmentCount; i++)
                {
                    if (stream.Position + SegmentHeaderSizeBytes > stream.Length)
                        return false;

                    ReplaySegmentPreview segment = ReadSegment(reader);
                    if (segment.PayloadBytes < 0)
                        return false;

                    int readableBytes = segment.PayloadBytes;
                    long remainingBytes = stream.Length - stream.Position;
                    if (readableBytes > remainingBytes)
                        readableBytes = (int)remainingBytes;

                    segment.Payload = readableBytes > 0
                        ? reader.ReadBytes(readableBytes)
                        : System.Array.Empty<byte>();
                    target.Add(segment);
                }
            }

            return true;
        }

        private static ReplayHeaderPreview ReadHeader(BinaryReader reader, long offset)
        {
            ReplayHeaderPreview header = default;
            header.FileOffset = offset;
            header.Magic = ReplayMagic;
            header.Version = reader.ReadUInt32();
            header.HeaderSize = reader.ReadUInt16();
            header.SegmentHeaderSize = reader.ReadUInt16();
            header.FrameIndex = reader.ReadUInt32();
            header.SnapshotSequence = reader.ReadUInt32();
            header.SegmentCount = reader.ReadUInt32();
            header.Flags = reader.ReadUInt32();
            header.PrecisionTimestamp = reader.ReadDouble();
            header.PayloadBytes = reader.ReadInt64();
            header.TotalSourceBytes = reader.ReadInt64();
            header.DroppedBytes = reader.ReadInt64();
            header.WriteOffset = reader.ReadInt64();
            header.SubjectHash = reader.ReadUInt32();
            header.ErrorCode = reader.ReadUInt32();
            header.ReplaySeed = reader.ReadUInt32();
            header.SourceCount = reader.ReadUInt32();
            return header;
        }

        private static ReplaySegmentPreview ReadSegment(BinaryReader reader)
        {
            ReplaySegmentPreview segment = default;
            segment.OwnerHash = reader.ReadUInt32();
            segment.LabelHash = reader.ReadUInt32();
            segment.SourceBytes = reader.ReadInt64();
            segment.PayloadBytes = reader.ReadInt32();
            segment.AllocationFrame = reader.ReadInt32();
            segment.PreviousHash = reader.ReadUInt64();
            segment.CurrentHash = reader.ReadUInt64();
            segment.Flags = reader.ReadUInt32();
            segment.SegmentIndex = reader.ReadUInt32();
            segment.PayloadOffset = reader.ReadInt64();
            reader.ReadInt64();
            return segment;
        }

        private static int FindMatchingSegment(List<ReplaySegmentPreview> segments, uint ownerHash, uint labelHash)
        {
            for (int i = 0; i < segments.Count; i++)
            {
                ReplaySegmentPreview segment = segments[i];
                if (segment.OwnerHash == ownerHash && segment.LabelHash == labelHash)
                    return i;
            }

            return -1;
        }

        private static int FindFirstByteDiff(byte[] left, byte[] right)
        {
            int limit = left.Length < right.Length ? left.Length : right.Length;
            for (int i = 0; i < limit; i++)
            {
                if (left[i] != right[i])
                    return i;
            }

            return left.Length == right.Length ? -1 : limit;
        }

        private static bool IsHeaderPlausible(ReplayHeaderPreview header)
        {
            return header.Magic == ReplayMagic &&
                   header.Version > 0u &&
                   header.HeaderSize == HeaderSizeBytes &&
                   header.SegmentHeaderSize == SegmentHeaderSizeBytes &&
                   header.PayloadBytes >= 0L &&
                   header.TotalSourceBytes >= 0L &&
                   header.DroppedBytes >= 0L;
        }

        private struct ReplayHeaderPreview
        {
            public long FileOffset;
            public ulong Magic;
            public uint Version;
            public ushort HeaderSize;
            public ushort SegmentHeaderSize;
            public uint FrameIndex;
            public uint SnapshotSequence;
            public uint SegmentCount;
            public uint Flags;
            public double PrecisionTimestamp;
            public long PayloadBytes;
            public long TotalSourceBytes;
            public long DroppedBytes;
            public long WriteOffset;
            public uint SubjectHash;
            public uint ErrorCode;
            public uint ReplaySeed;
            public uint SourceCount;
        }

        private struct ReplaySegmentPreview
        {
            public uint OwnerHash;
            public uint LabelHash;
            public long SourceBytes;
            public int PayloadBytes;
            public int AllocationFrame;
            public ulong PreviousHash;
            public ulong CurrentHash;
            public uint Flags;
            public uint SegmentIndex;
            public long PayloadOffset;
            public byte[] Payload;
        }
    }
}
#endif
