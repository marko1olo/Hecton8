#if UNITY_EDITOR
using System;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Core.Data.Editor
{
    public sealed unsafe class CacheBTreeTopologyXRayWindow : EditorWindow
    {
        private const int MaxNodes = 512;
        private const int MaxTraceNodes = H8CacheBTree.MaxTraversalDepth;
        private const int MaxWaterfallSamples = 64;
        private const float Padding = 6f;

        private readonly BTreeNodeSnapshot[] _nodes = new BTreeNodeSnapshot[MaxNodes];
        private readonly BTreeTelemetryEntry[] _telemetry = new BTreeTelemetryEntry[H8StaticDataFormat.TelemetryFrameCount];
        private readonly uint[] _traceOffsets = new uint[MaxTraceNodes];
        private readonly int[] _depthCounts = new int[H8CacheBTree.MaxTraversalDepth];
        private readonly int[] _depthCursor = new int[H8CacheBTree.MaxTraversalDepth];
        private byte[] _fileBytes;
        private string _loadedPath;
        private string _formatName;
        private uint _treeOffset;
        private uint _treeRootOffset;
        private uint _treeEndOffset;
        private uint _nodeCount;
        private int _nodeSnapshotCount;
        private int _telemetryCount;
        private int _traceCount;
        private uint _lastSearchHash;
        private uint _lastSearchResult;
        private bool _lastSearchFound;
        private string _tuningStatus = "tuning idle";

        private TextField _pathField;
        private TextField _searchField;
        private Label _summaryLabel;
        private Label _traceLabel;
        private Label _telemetryLabel;
        private BTreeTopologyElement _topology;
        private BTreeWaterfallElement _waterfall;

        [MenuItem("Hecton8/Core/Data/B-Tree Topology X-Ray")]
        public static void Open()
        {
            CacheBTreeTopologyXRayWindow window = GetWindow<CacheBTreeTopologyXRayWindow>("B-Tree X-Ray");
            window.minSize = new Vector2(680f, 420f);
            window.LoadDefaultCandidate();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = Padding;
            rootVisualElement.style.paddingRight = Padding;
            rootVisualElement.style.paddingTop = Padding;
            rootVisualElement.style.paddingBottom = Padding;

            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 4f;
            rootVisualElement.Add(row);

            _pathField = new TextField();
            _pathField.style.flexGrow = 1f;
            _pathField.SetValueWithoutNotify(ResolveDefaultPath());
            row.Add(_pathField);

            Button loadButton = new Button(LoadPathFromField) { text = "Load" };
            loadButton.style.width = 64f;
            row.Add(loadButton);

            Button browseButton = new Button(BrowsePath) { text = "Browse" };
            browseButton.style.width = 72f;
            row.Add(browseButton);

            Button tuningButton = new Button(LoadTuningCsv) { text = "Load Tuning CSV" };
            tuningButton.style.width = 126f;
            row.Add(tuningButton);

            VisualElement searchRow = new VisualElement();
            searchRow.style.flexDirection = FlexDirection.Row;
            searchRow.style.marginBottom = 4f;
            rootVisualElement.Add(searchRow);

            _searchField = new TextField();
            _searchField.style.flexGrow = 1f;
            _searchField.RegisterValueChangedCallback(_ => RunLiveSearch());
            searchRow.Add(_searchField);

            Button searchButton = new Button(RunLiveSearch) { text = "Trace Key" };
            searchButton.style.width = 96f;
            searchRow.Add(searchButton);

            _summaryLabel = new Label();
            _summaryLabel.style.marginBottom = 3f;
            rootVisualElement.Add(_summaryLabel);

            _traceLabel = new Label();
            _traceLabel.style.marginBottom = 3f;
            rootVisualElement.Add(_traceLabel);

            _telemetryLabel = new Label();
            _telemetryLabel.style.marginBottom = 4f;
            rootVisualElement.Add(_telemetryLabel);

            _waterfall = new BTreeWaterfallElement();
            _waterfall.style.height = 48f;
            _waterfall.style.marginBottom = 6f;
            _waterfall.style.backgroundColor = new Color(0.04f, 0.045f, 0.05f, 1f);
            rootVisualElement.Add(_waterfall);

            _topology = new BTreeTopologyElement();
            _topology.style.flexGrow = 1f;
            _topology.style.minHeight = 260f;
            _topology.style.backgroundColor = new Color(0.035f, 0.035f, 0.04f, 1f);
            rootVisualElement.Add(_topology);

            LoadPathFromField();
        }

        private void OnEnable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            RefreshTelemetrySnapshot();
            ApplyUi();
        }

        private void LoadDefaultCandidate()
        {
            if (_pathField != null)
                _pathField.SetValueWithoutNotify(ResolveDefaultPath());

            LoadPath(ResolveDefaultPath());
        }

        private void LoadPathFromField()
        {
            LoadPath(_pathField != null ? _pathField.value : ResolveDefaultPath());
        }

        private void BrowsePath()
        {
            string path = EditorUtility.OpenFilePanel("B-Tree MMF Payload", Directory.GetCurrentDirectory(), "bin,h8bin,h8loc");
            if (string.IsNullOrEmpty(path))
                return;

            if (_pathField != null)
                _pathField.SetValueWithoutNotify(path);
            LoadPath(path);
        }

        private void LoadTuningCsv()
        {
            string root = Directory.GetParent(Application.dataPath)?.FullName;
            string path = string.IsNullOrEmpty(root)
                ? "Data/Balance/btree_tuning_profiles.csv"
                : Path.Combine(root, "Data", "Balance", "btree_tuning_profiles.csv");
            if (!File.Exists(path))
            {
                _tuningStatus = "tuning CSV missing";
                ApplyUi();
                return;
            }

            IDataVault vault = GlobalRegistry.DataVault;
            if (!TryAcquireTuningProfiles(vault, out VaultGenerationHandle<BTreeTuningProfileDTO> profileHandle, out NativeArray<BTreeTuningProfileDTO> profiles))
            {
                _tuningStatus = "no active Vault for tuning profiles";
                ApplyUi();
                return;
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                _tuningStatus = BTreeTuningCsvParser.TryParse(bytes, profiles, out int count, out uint errorHash)
                    ? "tuning profiles " + count
                    : "tuning error 0x" + errorHash.ToString("X8");
            }
            finally
            {
                vault.ReleaseWriteLock(in profileHandle, SystemID.CoreDiagnostics);
            }
            ApplyUi();
        }

        private void LoadPath(string path)
        {
            _nodeSnapshotCount = 0;
            _traceCount = 0;
            _lastSearchFound = false;
            _lastSearchHash = 0u;
            _lastSearchResult = H8CacheBTree.NotFound;
            _loadedPath = path;
            _formatName = "unloaded";

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                _fileBytes = null;
                ApplyUi();
                return;
            }

            _fileBytes = File.ReadAllBytes(path);
            if (!TryResolveTreeFromBytes(_fileBytes))
            {
                _nodeSnapshotCount = 0;
                ApplyUi();
                return;
            }

            BuildNodeSnapshots();
            ApplyUi();
        }

        private bool TryResolveTreeFromBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length < H8StaticDataFormat.CacheLineBytes)
                return false;

            fixed (byte* ptr = bytes)
            {
                if (bytes.Length >= UnsafeUtility.SizeOf<H8StaticDataHeader>())
                {
                    H8StaticDataHeader header = UnsafeUtility.ReadArrayElement<H8StaticDataHeader>(ptr, 0);
                    if (header.Magic == H8StaticDataFormat.StaticDataMagic &&
                        H8CacheBTree.TryResolveTree(
                            header.Flags,
                            header.LookupOffset,
                            header.LookupCount,
                            (uint)UnsafeUtility.SizeOf<H8StaticDataLookupEntry>(),
                            header.RecordsOffset,
                            out _treeOffset,
                            out _treeRootOffset,
                            out _nodeCount))
                    {
                        _treeEndOffset = header.RecordsOffset;
                        _formatName = "H8StaticData";
                        return _treeEndOffset <= bytes.Length;
                    }
                }

                if (bytes.Length >= UnsafeUtility.SizeOf<H8BabelDictionaryHeader>())
                {
                    H8BabelDictionaryHeader header = UnsafeUtility.ReadArrayElement<H8BabelDictionaryHeader>(ptr, 0);
                    if (header.Magic == H8StaticDataFormat.BabelMagic &&
                        H8CacheBTree.TryResolveTree(
                            header.Flags,
                            header.IndexOffset,
                            header.EntryCount,
                            (uint)UnsafeUtility.SizeOf<BabelIndexDTO>(),
                            header.DataOffset,
                            out _treeOffset,
                            out _treeRootOffset,
                            out _nodeCount))
                    {
                        _treeEndOffset = header.DataOffset;
                        _formatName = "Babel";
                        return _treeEndOffset <= bytes.Length;
                    }
                }
            }

            return TryResolveH8lrTree(bytes);
        }

        private bool TryResolveH8lrTree(byte[] bytes)
        {
            if (bytes.Length < 16 || ReadUInt32(bytes, 0) != 0x524C3848u)
                return false;

            uint count = ReadUInt32(bytes, 8);
            if (count == 0u || count > 4096u)
                return false;

            int recordTableEnd = 16 + ((int)count * 16);
            if (recordTableEnd > bytes.Length)
                return false;

            uint firstPayload = uint.MaxValue;
            for (int i = 0; i < count; i++)
            {
                uint offset = ReadUInt32(bytes, 16 + (i * 16) + 4);
                if (offset < firstPayload)
                    firstPayload = offset;
            }

            uint treeOffset = (uint)H8StaticDataFormat.AlignUp64(recordTableEnd);
            if (firstPayload == uint.MaxValue ||
                firstPayload <= treeOffset ||
                firstPayload > bytes.Length ||
                ((treeOffset | firstPayload) & 63u) != 0u)
            {
                return false;
            }

            _treeOffset = treeOffset;
            _treeEndOffset = firstPayload;
            _treeRootOffset = firstPayload - H8StaticDataFormat.CacheLineBytes;
            _nodeCount = (_treeEndOffset - _treeOffset) / H8StaticDataFormat.CacheLineBytes;
            _formatName = "H8LR";
            return _nodeCount > 0u;
        }

        private void BuildNodeSnapshots()
        {
            _nodeSnapshotCount = 0;
            if (_fileBytes == null || _treeEndOffset <= _treeOffset)
                return;

            fixed (byte* ptr = _fileBytes)
            {
                for (uint offset = _treeOffset; offset < _treeEndOffset && _nodeSnapshotCount < MaxNodes; offset += H8StaticDataFormat.CacheLineBytes)
                {
                    BTreeNodeDTO node = UnsafeUtility.ReadArrayElement<BTreeNodeDTO>(ptr + offset, 0);
                    int keyCount = math.clamp(H8CacheBTree.GetKeyCount(in node), 0, H8StaticDataFormat.BTreeNodeKeyCapacity);
                    bool leaf = H8CacheBTree.IsLeaf(in node);
                    uint minChildDistance = uint.MaxValue;
                    if (!leaf)
                    {
                        int childCount = keyCount + 1;
                        for (int child = 0; child < childCount; child++)
                        {
                            uint childOffset = H8CacheBTree.GetChild(in node, child);
                            if (childOffset < _treeOffset || childOffset >= _treeEndOffset)
                                continue;

                            uint distance = childOffset > offset ? childOffset - offset : offset - childOffset;
                            if (distance < minChildDistance)
                                minChildDistance = distance;
                        }
                    }

                    _nodes[_nodeSnapshotCount++] = new BTreeNodeSnapshot
                    {
                        Offset = offset,
                        Meta = node.Meta,
                        KeyCount = keyCount,
                        IsLeaf = leaf,
                        MinChildDistance = minChildDistance == uint.MaxValue ? 0u : minChildDistance,
                        Child0 = H8CacheBTree.GetChild(in node, 0),
                        Child1 = H8CacheBTree.GetChild(in node, 1),
                        Child2 = H8CacheBTree.GetChild(in node, 2),
                        Child3 = H8CacheBTree.GetChild(in node, 3),
                        Child4 = H8CacheBTree.GetChild(in node, 4),
                        Child5 = H8CacheBTree.GetChild(in node, 5),
                        Child6 = H8CacheBTree.GetChild(in node, 6),
                        Child7 = H8CacheBTree.GetChild(in node, 7),
                        Depth = -1,
                        Rank = 0,
                        CountAtDepth = 1
                    };
                }
            }

            ComputeDepths();
        }

        private void ComputeDepths()
        {
            for (int i = 0; i < _depthCounts.Length; i++)
            {
                _depthCounts[i] = 0;
                _depthCursor[i] = 0;
            }

            for (int i = 0; i < _nodeSnapshotCount; i++)
            {
                _nodes[i].Depth = -1;
                _nodes[i].Rank = 0;
                _nodes[i].CountAtDepth = 1;
            }

            int rootIndex = FindNodeIndex(_treeRootOffset);
            if (rootIndex < 0)
                return;

            _nodes[rootIndex].Depth = 0;
            fixed (byte* ptr = _fileBytes)
            {
                for (int pass = 0; pass < H8CacheBTree.MaxTraversalDepth - 1; pass++)
                {
                    bool changed = false;
                    for (int i = 0; i < _nodeSnapshotCount; i++)
                    {
                        if (_nodes[i].Depth != pass || _nodes[i].IsLeaf)
                            continue;

                        BTreeNodeDTO node = UnsafeUtility.ReadArrayElement<BTreeNodeDTO>(ptr + _nodes[i].Offset, 0);
                        int childCount = math.clamp(H8CacheBTree.GetKeyCount(in node) + 1, 0, H8StaticDataFormat.BTreeNodeChildCapacity);
                        for (int child = 0; child < childCount; child++)
                        {
                            int childIndex = FindNodeIndex(H8CacheBTree.GetChild(in node, child));
                            if (childIndex >= 0 && _nodes[childIndex].Depth < 0)
                            {
                                _nodes[childIndex].Depth = pass + 1;
                                changed = true;
                            }
                        }
                    }

                    if (!changed)
                        break;
                }
            }

            for (int i = 0; i < _nodeSnapshotCount; i++)
            {
                int depth = _nodes[i].Depth;
                if ((uint)depth < (uint)_depthCounts.Length)
                    _depthCounts[depth]++;
            }

            for (int i = 0; i < _nodeSnapshotCount; i++)
            {
                int depth = _nodes[i].Depth;
                if ((uint)depth >= (uint)_depthCounts.Length)
                    continue;

                _nodes[i].Rank = _depthCursor[depth]++;
                _nodes[i].CountAtDepth = math.max(1, _depthCounts[depth]);
            }
        }

        private int FindNodeIndex(uint offset)
        {
            for (int i = 0; i < _nodeSnapshotCount; i++)
            {
                if (_nodes[i].Offset == offset)
                    return i;
            }

            return -1;
        }

        private void RunLiveSearch()
        {
            if (_fileBytes == null || _nodeSnapshotCount == 0 || _searchField == null)
                return;

            _lastSearchHash = H8DataHashTool.ComputeFnv1a32(_searchField.value.AsSpan());
            _lastSearchFound = false;
            _lastSearchResult = H8CacheBTree.NotFound;
            _traceCount = 0;
            for (int i = 0; i < _traceOffsets.Length; i++)
                _traceOffsets[i] = H8CacheBTree.NotFound;

            using (NativeArray<byte> bytes = new NativeArray<byte>(_fileBytes, Allocator.TempJob))
            using (NativeArray<DataOffsetLengthDTO> output = new NativeArray<DataOffsetLengthDTO>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory))
            using (NativeArray<uint> trace = new NativeArray<uint>(MaxTraceNodes, Allocator.TempJob, NativeArrayOptions.UninitializedMemory))
            {
                byte* basePointer = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(bytes);
                H8CacheBTree.TraceBTreeTraversalJob job = new H8CacheBTree.TraceBTreeTraversalJob
                {
                    BasePointer = basePointer,
                    Output = output,
                    TouchedNodeOffsets = trace,
                    TreeOffset = _treeOffset,
                    RootOffset = _treeRootOffset,
                    TreeEndOffset = _treeEndOffset,
                    TargetHash = _lastSearchHash,
                    GlobalQualityWeight = 1f
                };

                JobHandle handle = job.Schedule();
                handle.Complete();
                DataOffsetLengthDTO result = output[0];
                _lastSearchFound = (result.Flags & H8CacheBTree.ResultFoundFlag) != 0u;
                _lastSearchResult = result.ByteOffset;
                _traceCount = result.ByteLength > MaxTraceNodes ? MaxTraceNodes : (int)result.ByteLength;
                for (int i = 0; i < _traceCount; i++)
                    _traceOffsets[i] = trace[i];
            }

            ApplyUi();
        }

        private void RefreshTelemetrySnapshot()
        {
            _telemetryCount = 0;
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null ||
                !vault.TryGetGenerationHandle<BTreeTelemetryEntry>(
                    H8CacheBTree.BTreeTelemetryRingBufferId,
                    out VaultGenerationHandle<BTreeTelemetryEntry> ringHandle) ||
                ringHandle.BufferID != unchecked((uint)(int)H8CacheBTree.BTreeTelemetryRingBufferId) ||
                !vault.TryReadHandle(in ringHandle, out NativeArray<BTreeTelemetryEntry> ring) ||
                !ring.IsCreated)
            {
                return;
            }

            int count = math.min(ring.Length, _telemetry.Length);
            for (int i = 0; i < count; i++)
                _telemetry[_telemetryCount++] = ring[i];
        }

        private void ApplyUi()
        {
            if (_summaryLabel == null || _traceLabel == null || _telemetryLabel == null || _topology == null || _waterfall == null)
                return;

            _summaryLabel.text = _formatName + " | nodes " + _nodeSnapshotCount +
                                 " | tree [" + _treeOffset + ".." + _treeEndOffset + "] root " + _treeRootOffset +
                                 " | file " + (_fileBytes == null ? 0 : _fileBytes.Length) + " B | " + _tuningStatus;

            _traceLabel.text = "Live key hash 0x" + _lastSearchHash.ToString("X8") +
                               " | found " + _lastSearchFound +
                               " | value " + _lastSearchResult +
                               " | touched nodes " + _traceCount +
                               " | cache lines " + _traceCount;

            BTreeTelemetryEntry last = default;
            for (int i = _telemetryCount - 1; i >= 0; i--)
            {
                if (_telemetry[i].SearchCount != 0u)
                {
                    last = _telemetry[i];
                    break;
                }
            }

            _telemetryLabel.text = "Telemetry searches " + last.SearchCount +
                                   " | avg depth " + (last.AverageDepthQ8 / 256f).ToString("0.00") +
                                   " | keys " + last.KeysProcessed +
                                   " | slowest ns " + last.SlowestLookupNs;

            _waterfall.SetSamples(_telemetry, _telemetryCount);
            _topology.SetNodes(_nodes, _nodeSnapshotCount, _traceOffsets, _traceCount);
        }

        private static bool TryAcquireTuningProfiles(
            IDataVault vault,
            out VaultGenerationHandle<BTreeTuningProfileDTO> handle,
            out NativeArray<BTreeTuningProfileDTO> profiles)
        {
            handle = default;
            profiles = default;
            if (vault == null)
                return false;

            if (vault.TryGetGenerationHandle<BTreeTuningProfileDTO>(
                    H8CacheBTree.BTreeTuningProfilesBufferId,
                    out VaultGenerationHandle<BTreeTuningProfileDTO> existing) &&
                existing.BufferID == unchecked((uint)(int)H8CacheBTree.BTreeTuningProfilesBufferId) &&
                vault.TryReadHandle(in existing, out NativeArray<BTreeTuningProfileDTO> existingProfiles) &&
                existingProfiles.IsCreated &&
                existingProfiles.Length >= H8CacheBTree.BTreeTuningProfileCapacity)
            {
                handle = existing;
            }
            else
            {
                if (vault.IsAllocationLocked)
                    return false;

                handle = vault.GetGenerationHandle<BTreeTuningProfileDTO>(
                    H8CacheBTree.BTreeTuningProfilesBufferId,
                    H8CacheBTree.BTreeTuningProfileCapacity,
                    SystemID.CoreDataVault,
                    NativeArrayOptions.ClearMemory);
            }

            if (!vault.TryAcquireWriteLock(in handle, SystemID.CoreDiagnostics, out profiles))
                return false;

            if (profiles.IsCreated && profiles.Length >= H8CacheBTree.BTreeTuningProfileCapacity)
                return true;

            vault.ReleaseWriteLock(in handle, SystemID.CoreDiagnostics);
            profiles = default;
            return false;
        }

        private static string ResolveDefaultPath()
        {
            string root = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(root))
                return string.Empty;

            string staticPath = Path.Combine(root, "Data", "Balance", "Baked", H8StaticDataFormat.StaticDataFileName);
            if (File.Exists(staticPath))
                return staticPath;

            string babelPath = Path.Combine(root, "Data", "Balance", "Baked", H8StaticDataFormat.BabelDictionaryFileName);
            if (File.Exists(babelPath))
                return babelPath;

            return Path.Combine(root, "Data", "Lore", "Encyclopedia.h8bin");
        }

        private static uint ReadUInt32(byte[] bytes, int offset)
        {
            return (uint)(bytes[offset] |
                          (bytes[offset + 1] << 8) |
                          (bytes[offset + 2] << 16) |
                          (bytes[offset + 3] << 24));
        }

        private struct BTreeNodeSnapshot
        {
            public uint Offset;
            public uint Meta;
            public uint MinChildDistance;
            public uint Child0;
            public uint Child1;
            public uint Child2;
            public uint Child3;
            public uint Child4;
            public uint Child5;
            public uint Child6;
            public uint Child7;
            public int KeyCount;
            public int Depth;
            public int Rank;
            public int CountAtDepth;
            public bool IsLeaf;
        }

        private sealed class BTreeWaterfallElement : VisualElement
        {
            private readonly BTreeTelemetryEntry[] _samples = new BTreeTelemetryEntry[MaxWaterfallSamples];
            private int _count;

            public BTreeWaterfallElement()
            {
                generateVisualContent += Draw;
            }

            public void SetSamples(BTreeTelemetryEntry[] samples, int count)
            {
                _count = math.min(math.max(0, count), MaxWaterfallSamples);
                int start = math.max(0, count - _count);
                for (int i = 0; i < _count; i++)
                    _samples[i] = samples[start + i];
                MarkDirtyRepaint();
            }

            private void Draw(MeshGenerationContext context)
            {
                Rect rect = contentRect;
                Painter2D painter = context.painter2D;
                uint maxNs = 1u;
                for (int i = 0; i < _count; i++)
                {
                    if (_samples[i].SlowestLookupNs > maxNs)
                        maxNs = _samples[i].SlowestLookupNs;
                }

                float width = rect.width / math.max(1, MaxWaterfallSamples);
                for (int i = 0; i < _count; i++)
                {
                    BTreeTelemetryEntry sample = _samples[i];
                    float h = math.max(2f, rect.height * math.saturate(sample.SlowestLookupNs / (float)maxNs));
                    Rect bar = new Rect(rect.xMin + (i * width), rect.yMax - h, math.max(1f, width - 1f), h);
                    Color color = (sample.Flags & H8CacheBTree.BTreeTelemetrySlowBatchFlag) != 0u
                        ? new Color(0.95f, 0.12f, 0.05f, 1f)
                        : new Color(0.1f, 0.65f, 0.45f, 1f);
                    FillRect(painter, bar, color);
                }
            }
        }

        private sealed class BTreeTopologyElement : VisualElement
        {
            private readonly BTreeNodeSnapshot[] _nodes = new BTreeNodeSnapshot[MaxNodes];
            private readonly uint[] _trace = new uint[MaxTraceNodes];
            private int _nodeCount;
            private int _traceCount;

            public BTreeTopologyElement()
            {
                generateVisualContent += Draw;
            }

            public void SetNodes(BTreeNodeSnapshot[] nodes, int nodeCount, uint[] trace, int traceCount)
            {
                _nodeCount = math.min(math.max(0, nodeCount), MaxNodes);
                for (int i = 0; i < _nodeCount; i++)
                    _nodes[i] = nodes[i];

                _traceCount = math.min(math.max(0, traceCount), MaxTraceNodes);
                for (int i = 0; i < _traceCount; i++)
                    _trace[i] = trace[i];

                MarkDirtyRepaint();
            }

            private void Draw(MeshGenerationContext context)
            {
                Rect rect = contentRect;
                Painter2D painter = context.painter2D;
                int maxDepth = 0;
                for (int i = 0; i < _nodeCount; i++)
                    maxDepth = math.max(maxDepth, _nodes[i].Depth);

                float rowHeight = rect.height / math.max(1, maxDepth + 1);
                for (int i = 0; i < _nodeCount; i++)
                {
                    if (_nodes[i].Depth < 0)
                        continue;

                    Vector2 from = PositionOf(rect, rowHeight, in _nodes[i]);
                    int childCount = _nodes[i].IsLeaf ? 0 : math.clamp(_nodes[i].KeyCount + 1, 0, H8StaticDataFormat.BTreeNodeChildCapacity);
                    for (int child = 0; child < childCount; child++)
                    {
                        int childIndex = FindSnapshotIndex(GetChildOffset(in _nodes[i], child));
                        if (childIndex < 0)
                            continue;

                        Vector2 to = PositionOf(rect, rowHeight, in _nodes[childIndex]);
                        painter.strokeColor = new Color(0.16f, 0.18f, 0.2f, 0.7f);
                        painter.lineWidth = 1f;
                        painter.BeginPath();
                        painter.MoveTo(from);
                        painter.LineTo(to);
                        painter.Stroke();
                    }
                }

                for (int i = 0; i < _nodeCount; i++)
                {
                    if (_nodes[i].Depth < 0)
                        continue;

                    Vector2 center = PositionOf(rect, rowHeight, in _nodes[i]);
                    Rect nodeRect = new Rect(center.x - 18f, center.y - 8f, 36f, 16f);
                    float distance01 = math.saturate(_nodes[i].MinChildDistance / 4096f);
                    Color color = Color.Lerp(new Color(0.08f, 0.62f, 0.36f, 1f), new Color(0.9f, 0.32f, 0.08f, 1f), distance01);
                    if (_nodes[i].IsLeaf)
                        color = new Color(color.r * 0.75f, color.g * 0.75f, color.b + 0.15f, 1f);
                    if (IsTraced(_nodes[i].Offset))
                        color = Color.white;

                    FillRect(painter, nodeRect, color);
                }
            }

            private bool IsTraced(uint offset)
            {
                for (int i = 0; i < _traceCount; i++)
                {
                    if (_trace[i] == offset)
                        return true;
                }

                return false;
            }

            private int FindSnapshotIndex(uint offset)
            {
                for (int i = 0; i < _nodeCount; i++)
                {
                    if (_nodes[i].Offset == offset)
                        return i;
                }

                return -1;
            }

            private static uint GetChildOffset(in BTreeNodeSnapshot node, int index)
            {
                switch (index)
                {
                    case 0: return node.Child0;
                    case 1: return node.Child1;
                    case 2: return node.Child2;
                    case 3: return node.Child3;
                    case 4: return node.Child4;
                    case 5: return node.Child5;
                    case 6: return node.Child6;
                    default: return node.Child7;
                }
            }

            private static Vector2 PositionOf(Rect rect, float rowHeight, in BTreeNodeSnapshot node)
            {
                float x = rect.xMin + ((node.Rank + 1f) / (node.CountAtDepth + 1f)) * rect.width;
                float y = rect.yMin + (node.Depth + 0.5f) * rowHeight;
                return new Vector2(x, y);
            }
        }

        private static void FillRect(Painter2D painter, Rect rect, Color color)
        {
            painter.fillColor = color;
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMax));
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();
            painter.Fill();
        }
    }
}
#endif
