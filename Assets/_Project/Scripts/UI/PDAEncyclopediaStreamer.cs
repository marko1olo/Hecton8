using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Data;
using Hecton8.Core.Memory;
using TMPro;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.UI
{
    public enum PdaEncyclopediaStreamState : byte
    {
        Idle = 0,
        Loading = 1,
        Streaming = 2,
        Complete = 3,
        Locked = 4,
        Fault = 5
    }

    [StructLayout(LayoutKind.Explicit, Size = 48)]
    internal struct PdaAup48
    {
        [FieldOffset(0)] public long GridX;
        [FieldOffset(8)] public long GridY;
        [FieldOffset(16)] public long GridZ;
        [FieldOffset(24)] public float LocalX;
        [FieldOffset(28)] public float LocalY;
        [FieldOffset(32)] public float LocalZ;
        [FieldOffset(36)] public uint Reserved0;
        [FieldOffset(40)] public ulong Reserved1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct EncyclopediaStateDTO
    {
        [FieldOffset(0)] public ulong Mask0;
        [FieldOffset(8)] public ulong Mask1;
        [FieldOffset(16)] public ulong Mask2;
        [FieldOffset(24)] public ulong Mask3;
        [FieldOffset(32)] public long LastDiscoveryGridX;
        [FieldOffset(40)] public long LastDiscoveryGridY;
        [FieldOffset(48)] public long LastDiscoveryGridZ;
        [FieldOffset(56)] public float LastDiscoveryLocalX;
        [FieldOffset(60)] public float LastDiscoveryLocalY;
        [FieldOffset(64)] public float LastDiscoveryLocalZ;
        [FieldOffset(68)] public float GlobalQualityWeight;
        [FieldOffset(72)] public uint LastEntryHash;
        [FieldOffset(76)] public uint UnlockedCount;
        [FieldOffset(80)] public uint Revision;
        [FieldOffset(84)] public uint Magic;
        [FieldOffset(88)] public uint Flags;
        [FieldOffset(92)] public uint MetadataCount;
        [FieldOffset(96)] public uint MockEntryCount;
        [FieldOffset(100)] public uint LastFrame;
        [FieldOffset(104)] public uint LastSourceId;
        [FieldOffset(108)] public uint ActiveBitIndex;
        [FieldOffset(112)] public uint CursorByte;
        [FieldOffset(116)] public uint DecodedChars;
        [FieldOffset(120)] public uint VisibleChars;
        [FieldOffset(124)] public uint StreamState;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct PdaEncyclopediaRuntimeStateDTO
    {
        [FieldOffset(0)] public long LastDiscoveryGridX;
        [FieldOffset(8)] public long LastDiscoveryGridY;
        [FieldOffset(16)] public long LastDiscoveryGridZ;
        [FieldOffset(24)] public float LastDiscoveryLocalX;
        [FieldOffset(28)] public float LastDiscoveryLocalY;
        [FieldOffset(32)] public float LastDiscoveryLocalZ;
        [FieldOffset(36)] public float GlobalQualityWeight;
        [FieldOffset(40)] public uint LastEntryHash;
        [FieldOffset(44)] public uint UnlockedCount;
        [FieldOffset(48)] public uint Revision;
        [FieldOffset(52)] public uint Magic;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public uint MetadataCount;
        [FieldOffset(64)] public uint MockEntryCount;
        [FieldOffset(68)] public uint LastFrame;
        [FieldOffset(72)] public uint LastSourceId;
        [FieldOffset(76)] public uint ActiveBitIndex;
        [FieldOffset(80)] public uint CursorByte;
        [FieldOffset(84)] public uint DecodedChars;
        [FieldOffset(88)] public uint VisibleChars;
        [FieldOffset(92)] public uint SourceBytes;
        [FieldOffset(96)] public long DecodeTicks;
        [FieldOffset(104)] public long CanvasTicks;
        [FieldOffset(112)] public uint FaultHash;
        [FieldOffset(116)] public uint StateHash;
        [FieldOffset(120)] public uint StreamState;
        [FieldOffset(124)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct PdaEncyclopediaEntryMetaDTO
    {
        [FieldOffset(0)] public uint EntryHash;
        [FieldOffset(4)] public ushort BitIndex;
        [FieldOffset(6)] public ushort Flags;
        [FieldOffset(8)] public long DiscoveryGridX;
        [FieldOffset(16)] public long DiscoveryGridY;
        [FieldOffset(24)] public long DiscoveryGridZ;
        [FieldOffset(32)] public float DiscoveryLocalX;
        [FieldOffset(36)] public float DiscoveryLocalY;
        [FieldOffset(40)] public float DiscoveryLocalZ;
        [FieldOffset(44)] public uint SourceId;
        [FieldOffset(48)] public uint Revision;
        [FieldOffset(52)] public uint LastFrame;
        [FieldOffset(56)] public uint TitleHash;
        [FieldOffset(60)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct PdaEncyclopediaTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint StateHash;
        [FieldOffset(8)] public uint EntryHash;
        [FieldOffset(12)] public uint UnlockedCount;
        [FieldOffset(16)] public uint CharsRenderedThisFrame;
        [FieldOffset(20)] public uint VisibleChars;
        [FieldOffset(24)] public uint DecodedChars;
        [FieldOffset(28)] public uint SourceBytes;
        [FieldOffset(32)] public long DecodeTicks;
        [FieldOffset(40)] public long CanvasTicks;
        [FieldOffset(48)] public uint Flags;
        [FieldOffset(52)] public uint FaultHash;
        [FieldOffset(56)] public uint CursorByte;
        [FieldOffset(60)] public uint Capacity;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct PdaTypewriterStateDTO
    {
        [FieldOffset(0)] public float CharAccumulator;
        [FieldOffset(4)] public float GlobalQualityWeight;
        [FieldOffset(8)] public uint VisibleChars;
        [FieldOffset(12)] public uint DecodedChars;
        [FieldOffset(16)] public uint CharsRenderedThisFrame;
        [FieldOffset(20)] public uint LastFrame;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint StateHash;
        [FieldOffset(32)] public ulong Reserved0;
        [FieldOffset(40)] public ulong Reserved1;
        [FieldOffset(48)] public ulong Reserved2;
        [FieldOffset(56)] public ulong Reserved3;
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/PDA Encyclopedia Streamer")]
    public sealed unsafe class PDAEncyclopediaStreamer :
        MonoBehaviour,
        ILateFrameTickable,
        IPDAEventListener,
        IGlobalRegistryHotSwapListener
    {
        private const int UnlockBitCount = 256;
        private const int UnlockWordCount = 4;
        private const int MaxMetadataEntries = UnlockBitCount;
        private const int TelemetryFrameCount = 300;
        private const int MockUtf8Bytes = 64 * 1024;
        private const int MockEntryCapacity = 8;
        private const int CsvScratchBytes = 64 * 1024;
        private const int H8lrMirrorBytes = 8 * 1024 * 1024;
        private const int TitleBufferCapacity = 128;
        private const int MetaBufferCapacity = 256;
        private const long AupDeltaClampCells = 1000000L;
        private const double AupCellSizeMeters = HectonPhysicsContract.AupSectorSizeMetersInt;
        private const uint StateMagic = 0x50444145u;
        private const uint FaultMissingVault = 0x5641554Cu;
        private const uint FaultMissingText = 0x54455854u;
        private const uint FaultUtf8Invalid = 0x55544638u;
        private const uint DefaultEntryHash = 0xAEC57EACu;
        private const uint H8lrSourceId = PdaH8lrLoreStore.MagicH8lr;
        private const uint StateFlagEditorBulkUnlock = 1u;
        private const uint StateFlagPreciseAup = 2u;
        private const uint StateFlagCanvasSplit = 4u;
        private const int StateFlagSourceShift = 8;
        private const uint StateFlagSourceMask = 3u << StateFlagSourceShift;
        private const uint TelemetryFlagCanvasSplit = 1u << 16;
        private const uint TextSourceH8lr = 1u;
        private const uint TextSourceBabel = 2u;
        private const uint TextSourceVaultMock = 3u;
        private const BufferID UnlockMaskBufferId = (BufferID)70560;
        private const BufferID RuntimeStateBufferId = (BufferID)70561;
        private const BufferID MetadataBufferId = (BufferID)70562;
        private const BufferID TelemetryBufferId = (BufferID)70563;
        private const BufferID TelemetryCursorBufferId = (BufferID)70564;
        private const BufferID MockUtf8BufferId = (BufferID)70565;
        private const BufferID MockIndexBufferId = (BufferID)70566;
        private const BufferID CsvScratchBufferId = (BufferID)70567;
        private const BufferID TypewriterStateBufferId = (BufferID)70569;
        internal const BufferID H8lrMirrorBufferId = (BufferID)70570;
        private const SystemID VaultOwnerSystemId = SystemID.UI;

        private static readonly uint TokenDepthHash = ComputeStaticAsciiHash("DEPTH".AsSpan());
        private static readonly uint TokenEntryHashHash = ComputeStaticAsciiHash("ENTRY_HASH".AsSpan());
        private static readonly uint TokenQualityHash = ComputeStaticAsciiHash("QUALITY".AsSpan());
        private static readonly uint TokenDiscoveryGridHash = ComputeStaticAsciiHash("DISCOVERY_GRID".AsSpan());
        private static readonly uint TokenDiscoveryDistanceHash = ComputeStaticAsciiHash("DISCOVERY_DISTANCE".AsSpan());
        private static readonly uint TokenDistanceHash = ComputeStaticAsciiHash("DISTANCE".AsSpan());

        private static uint ResolvePdaFrame()
        {
            return TimeSliceScheduler.CurrentFrameId;
        }

        [Header("Bindings")]
        [SerializeField] private PlayerPDA playerPDA;
        [SerializeField] private int encyclopediaTabIndex = 3;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private TMP_Text metaText;
        [SerializeField] private Canvas staticShellCanvas;
        [SerializeField] private Canvas dynamicTextCanvas;

        [Header("Data")]
        [SerializeField] private bool openDefaultH8lrOnEnable = true;
        [SerializeField] private string h8lrPathOverride;
        [SerializeField] private bool openDefaultBabelOnEnable = true;
        [SerializeField] private string dictionaryPathOverride;
        [SerializeField] private string metadataCsvRelativePath = "Docs/PDA/lore_metadata.csv";
        [SerializeField] private uint initialEntryHash = DefaultEntryHash;

        [Header("Diagnostics")]
        [SerializeField] private bool drawDebugGizmo;
        [SerializeField] private float debugGizmoScale = 0.14f;

        private CharBufferPool.Lease _titleLease;
        private CharBufferPool.Lease _metaLease;
        private CharBufferPool.EncyclopediaLease _bodyLease;
        private IDataVault _vault;
        private VaultGenerationHandle<EncyclopediaStateDTO> _unlockMaskHandle;
        private VaultGenerationHandle<PdaEncyclopediaRuntimeStateDTO> _runtimeStateHandle;
        private VaultGenerationHandle<PdaEncyclopediaEntryMetaDTO> _metadataHandle;
        private VaultGenerationHandle<PdaEncyclopediaTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<int> _telemetryCursorHandle;
        private VaultGenerationHandle<byte> _mockUtf8Handle;
        private VaultGenerationHandle<BabelIndexDTO> _mockIndexHandle;
        private VaultGenerationHandle<byte> _csvScratchHandle;
        private VaultGenerationHandle<PdaTypewriterStateDTO> _typewriterStateHandle;
        private VaultGenerationHandle<byte> _h8lrMirrorHandle;
        private IPlayerRuntimeContext _playerContext;
        private PdaH8lrLoreStore _h8lrLoreStore;
        private BabelDictionaryStore _babelStore;
        private bool _ownsBabelStore;
        private bool _registeredLateFrame;
        private bool _registeredPdaEvents;
        private bool _registeredHotSwap;
        private bool _vaultReady;
        private bool _mockSeeded;
        private bool _h8lrMetadataSeeded;
#pragma warning disable CS0414
        private bool _coldBootstrapAttempted;
#pragma warning restore CS0414
        private bool _isPdaVisible;
        private bool _needsEntryReload = true;
        private bool _canvasSplitReady;
        private uint _activeEntryHash;
        private uint _pendingSelectHash;
        private PdaEncyclopediaStreamState _streamState;
        private int _sourceByteCursor;
        private int _decodedLength;
        private int _visibleLength;
        private int _lastSubmittedLength = -1;
        private int _activeSourceBytes;
        private float _charAccumulator;
        private uint _lastFaultHash;
        private uint _activeUtf8SourceFlags;

        private void Awake()
        {
            if (bodyText == null)
                bodyText = GetComponent<TMP_Text>();

            EnsureCanvasSplit();
        }

        private void OnEnable()
        {
            EnsureTextLeases();
            TryBindPlayerContextCold();
            TryColdBootstrap();
            SignalBus<ScanCompleteSignal>.EnsureInitialized();
            SignalBus<LoreFragmentScannedSignal>.EnsureInitialized();
            TryRegisterPdaEvents();
            TryRegisterHotSwapListener();
            TryRegisterDispatcherLanes();
            RefreshVisibility();
            _pendingSelectHash = initialEntryHash != 0u ? initialEntryHash : DefaultEntryHash;
            _needsEntryReload = true;
        }

        private void Start()
        {
            TryColdBootstrap();
            TryBindPlayerContextCold();
            TryRegisterPdaEvents();
            TryRegisterHotSwapListener();
            TryRegisterDispatcherLanes();
            RefreshVisibility();
        }

        private void OnDisable()
        {
            UnregisterDispatcherLanes();
            UnregisterPdaEvents();
            TryUnregisterHotSwapListener();
            _coldBootstrapAttempted = false;
            _vaultReady = false;
            _playerContext = null;
            ResetActiveSourceCache();

            if (_h8lrLoreStore != null)
            {
                _h8lrLoreStore.Dispose();
                _h8lrLoreStore = null;
                _h8lrMetadataSeeded = false;
            }

            if (_ownsBabelStore && _babelStore != null)
            {
                _babelStore.Dispose();
                _babelStore = null;
                _ownsBabelStore = false;
            }

            if (_bodyLease.IsValid)
            {
                CharBufferPool.Release(in _bodyLease);
                _bodyLease = default;
            }

            if (_titleLease.IsValid)
            {
                CharBufferPool.Release(in _titleLease);
                _titleLease = default;
            }

            if (_metaLease.IsValid)
            {
                CharBufferPool.Release(in _metaLease);
                _metaLease = default;
            }
        }

        private void OnDestroy()
        {
            OnDisable();
            ReleasePdaVaultHandles(_vault);
            _vault = null;
            _vaultReady = false;
            PDAEvents.AssertUnregistered(this, nameof(PDAEncyclopediaStreamer));
        }

        private void AdvanceEncyclopediaFrameState()
        {
            if (!_vaultReady)
            {
                TryColdBootstrap();
                if (!_vaultReady)
                    return;
            }

            ConsumeScanSignals();
            if (!_registeredPdaEvents)
                RefreshVisibility();

            // Entry selection rebuilds title/body/meta TMP buffers. Keep Tick as a
            // signal drain only; VISUAL_SYNC consumes the pending selection.
        }

        public void LateFrameTick()
        {
            AdvanceEncyclopediaFrameState();

            if (!_isPdaVisible || bodyText == null)
                return;

            if (!_bodyLease.IsValid)
            {
                SetFault(FaultMissingText);
                return;
            }

            if (!_vaultReady)
            {
                TryColdBootstrap();
                if (!_vaultReady)
                    return;
            }

            if (_pendingSelectHash != 0u)
            {
                uint hash = _pendingSelectHash;
                _pendingSelectHash = 0u;
                BeginEntry(hash);
            }

            if (_needsEntryReload)
                BeginEntry(_activeEntryHash != 0u ? _activeEntryHash : DefaultEntryHash);

            if (_streamState == PdaEncyclopediaStreamState.Locked ||
                _streamState == PdaEncyclopediaStreamState.Fault ||
                _streamState == PdaEncyclopediaStreamState.Complete)
            {
                RecordTelemetry(0u, 0L, 0L);
                return;
            }

            ReadOnlySpan<byte> source = SelectActiveUtf8Source();
            if (source.Length <= 0)
            {
                SetFault(FaultMissingText);
                return;
            }

            _activeSourceBytes = source.Length;
            TryAdvanceTextWindowForLongEntry(source.Length);
            float quality = math.saturate(HomeostasisBrain.GlobalQualityWeight);
            long decodeStart = Stopwatch.GetTimestamp();
            int decodeBudget = ResolveDecodeBudget(quality);
            int decodedThisFrame = DecodeUtf8Budgeted(source, _bodyLease.Span, decodeBudget);
            long decodeTicks = Stopwatch.GetTimestamp() - decodeStart;

            if (_sourceByteCursor >= source.Length)
                _streamState = _visibleLength >= _decodedLength ? PdaEncyclopediaStreamState.Complete : PdaEncyclopediaStreamState.Streaming;
            else
                _streamState = PdaEncyclopediaStreamState.Streaming;

            uint charsRenderedThisFrame = StepVisibleCharacters(quality);
            long canvasStart = Stopwatch.GetTimestamp();
            SubmitBodyIfChanged();
            long canvasTicks = Stopwatch.GetTimestamp() - canvasStart;

            if (_sourceByteCursor >= source.Length && _visibleLength >= _decodedLength)
                _streamState = PdaEncyclopediaStreamState.Complete;

            WriteRuntimeState(quality, decodeTicks, canvasTicks);
            RecordTelemetry(charsRenderedThisFrame, decodeTicks, canvasTicks);

            if (_lastFaultHash != 0u || HasInvalidNumbers(quality, decodeTicks, canvasTicks, decodedThisFrame))
                DumpBlackBox();
        }

        public void OnPDAEvent(in PDAEventPayload payload)
        {
            switch ((PDAEventType)payload.EventType)
            {
                case PDAEventType.Opened:
                    _isPdaVisible = payload.CurrentTab == encyclopediaTabIndex;
                    _needsEntryReload = _isPdaVisible;
                    return;
                case PDAEventType.TabChanged:
                    _isPdaVisible = payload.CurrentTab == encyclopediaTabIndex;
                    _needsEntryReload = _isPdaVisible;
                    return;
                case PDAEventType.Closed:
                    _isPdaVisible = false;
                    return;
                case PDAEventType.LogbookChanged:
                    if (payload.LogEventHashID != 0u)
                        _pendingSelectHash = payload.LogEventHashID;
                    return;
            }
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                _playerContext = currentService as IPlayerRuntimeContext;
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            IDataVault nextVault = currentService is IDataVault currentVault ? currentVault : null;
            IDataVault previousVault = previousService is IDataVault oldVault ? oldVault : null;
            BindDataVaultForLifecycle(nextVault, previousVault);

            if (_h8lrLoreStore != null)
            {
                _h8lrLoreStore.Dispose();
                _h8lrLoreStore = null;
            }

            if (_babelStore != null)
                _babelStore.BindDataVault(_vault);

            TryColdBootstrap();
            _needsEntryReload = true;
        }

#if UNITY_EDITOR
        public bool EditorTrySnapshot(
            out PdaEncyclopediaRuntimeStateDTO runtimeState,
            out EncyclopediaStateDTO unlockMask)
        {
            runtimeState = default;
            unlockMask = default;
            TryColdBootstrap();
            if (!EnsureVaultBuffers())
                return false;

            runtimeState = GetVaultElementRef(in _runtimeStateHandle, RuntimeStateBufferId, 0);
            unlockMask = GetVaultElementRef(in _unlockMaskHandle, UnlockMaskBufferId, 0);
            return true;
        }

        public void EditorUnlockAll()
        {
            TryColdBootstrap();
            if (!EnsureVaultBuffers())
                return;

            ref EncyclopediaStateDTO mask = ref GetVaultElementRef(in _unlockMaskHandle, UnlockMaskBufferId, 0);
            for (int word = 0; word < UnlockWordCount; word++)
                GetMaskWordRef(ref mask, word) = ulong.MaxValue;

            ref PdaEncyclopediaRuntimeStateDTO state = ref GetVaultElementRef(in _runtimeStateHandle, RuntimeStateBufferId, 0);
            state.UnlockedCount = UnlockBitCount;
            state.Revision++;
            state.Magic = StateMagic;
            state.Flags |= StateFlagEditorBulkUnlock;
            mask.UnlockedCount = UnlockBitCount;
            mask.Revision = state.Revision;
            mask.Magic = StateMagic;
            mask.Flags |= StateFlagEditorBulkUnlock;
            _needsEntryReload = true;
        }

        public void EditorLockAll()
        {
            TryColdBootstrap();
            if (!EnsureVaultBuffers())
                return;

            ref EncyclopediaStateDTO mask = ref GetVaultElementRef(in _unlockMaskHandle, UnlockMaskBufferId, 0);
            for (int word = 0; word < UnlockWordCount; word++)
                GetMaskWordRef(ref mask, word) = 0UL;

            ref PdaEncyclopediaRuntimeStateDTO state = ref GetVaultElementRef(in _runtimeStateHandle, RuntimeStateBufferId, 0);
            state.UnlockedCount = 0u;
            state.Revision++;
            state.Magic = StateMagic;
            mask.UnlockedCount = 0u;
            mask.Revision = state.Revision;
            mask.Magic = StateMagic;
            _needsEntryReload = true;
        }

        public void EditorSelectEntry(uint hash)
        {
            if (hash == 0u)
                return;

            _pendingSelectHash = hash;
            _needsEntryReload = true;
        }

        public bool EditorIngestCsv()
        {
            return TryIngestLoreMetadataCsvFromProject();
        }

        public bool TryIngestLoreMetadataCsvFromProject()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string path = Path.GetFullPath(Path.Combine(root, metadataCsvRelativePath));
            return TryIngestLoreMetadataCsv(path);
        }

        public bool TryIngestLoreMetadataCsv(string path)
        {
            TryColdBootstrap();
            if (!EnsureVaultBuffers() || string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            NativeArray<byte> scratch = ResolveVaultBuffer(in _csvScratchHandle, CsvScratchBufferId);
            if (!scratch.IsCreated || scratch.Length <= 0)
                return false;

            int totalRead = 0;
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan))
                {
                    byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(scratch);
                    Span<byte> span = MemoryMarshal.CreateSpan(ref UnsafeUtility.AsRef<byte>(ptr), scratch.Length);
                    while (totalRead < span.Length)
                    {
                        int read = stream.Read(span.Slice(totalRead));
                        if (read <= 0)
                            break;

                        totalRead += read;
                    }
                }
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }

            return ParseCsvMetadata(scratch, totalRead);
        }
#endif

        public static bool ValidateEncyclopediaStateLayout(out int sizeBytes, out int mask0Offset, out int mask3Offset)
        {
            sizeBytes = UnsafeUtility.SizeOf<EncyclopediaStateDTO>();
            mask0Offset = Marshal.OffsetOf<EncyclopediaStateDTO>(nameof(EncyclopediaStateDTO.Mask0)).ToInt32();
            mask3Offset = Marshal.OffsetOf<EncyclopediaStateDTO>(nameof(EncyclopediaStateDTO.Mask3)).ToInt32();
            return sizeBytes == 128 && mask0Offset == 0 && mask3Offset == 24;
        }

        public static bool ValidatePdaStreamerLayouts(
            out int encyclopediaSizeBytes,
            out int runtimeSizeBytes,
            out int entryMetaSizeBytes,
            out int telemetrySizeBytes,
            out int typewriterSizeBytes,
            out int aupSizeBytes,
            out int h8lrHeaderSizeBytes,
            out int h8lrRecordSizeBytes,
            out int runtimeSourceBytesOffset,
            out int telemetryFlagsOffset,
            out int typewriterReserved3Offset,
            out int aupReserved1Offset,
            out int h8lrRecordReserved0Offset)
        {
            encyclopediaSizeBytes = UnsafeUtility.SizeOf<EncyclopediaStateDTO>();
            runtimeSizeBytes = UnsafeUtility.SizeOf<PdaEncyclopediaRuntimeStateDTO>();
            entryMetaSizeBytes = UnsafeUtility.SizeOf<PdaEncyclopediaEntryMetaDTO>();
            telemetrySizeBytes = UnsafeUtility.SizeOf<PdaEncyclopediaTelemetryEntry>();
            typewriterSizeBytes = UnsafeUtility.SizeOf<PdaTypewriterStateDTO>();
            aupSizeBytes = UnsafeUtility.SizeOf<PdaAup48>();
            h8lrHeaderSizeBytes = UnsafeUtility.SizeOf<PdaH8lrHeaderDTO>();
            h8lrRecordSizeBytes = UnsafeUtility.SizeOf<PdaH8lrRecordDTO>();
            runtimeSourceBytesOffset = Marshal.OffsetOf<PdaEncyclopediaRuntimeStateDTO>(nameof(PdaEncyclopediaRuntimeStateDTO.SourceBytes)).ToInt32();
            telemetryFlagsOffset = Marshal.OffsetOf<PdaEncyclopediaTelemetryEntry>(nameof(PdaEncyclopediaTelemetryEntry.Flags)).ToInt32();
            typewriterReserved3Offset = Marshal.OffsetOf<PdaTypewriterStateDTO>(nameof(PdaTypewriterStateDTO.Reserved3)).ToInt32();
            aupReserved1Offset = Marshal.OffsetOf<PdaAup48>(nameof(PdaAup48.Reserved1)).ToInt32();
            h8lrRecordReserved0Offset = Marshal.OffsetOf<PdaH8lrRecordDTO>(nameof(PdaH8lrRecordDTO.Reserved0)).ToInt32();
            return encyclopediaSizeBytes == 128 &&
                   runtimeSizeBytes == 128 &&
                   entryMetaSizeBytes == 64 &&
                   telemetrySizeBytes == 64 &&
                   typewriterSizeBytes == 64 &&
                   aupSizeBytes == 48 &&
                   h8lrHeaderSizeBytes == 16 &&
                   h8lrRecordSizeBytes == 16 &&
                   runtimeSourceBytesOffset == 92 &&
                   telemetryFlagsOffset == 48 &&
                   typewriterReserved3Offset == 56 &&
                   aupReserved1Offset == 40 &&
                   h8lrRecordReserved0Offset == 12;
        }

#if UNITY_EDITOR
        public bool EditorTryWriteRawUtf8Hex(uint hash, Span<char> destination, out int written)
        {
            written = 0;
            if (hash == 0u)
                return false;

            TryColdBootstrap();
            if (!EnsureVaultBuffers())
                return false;

            ReadOnlySpan<byte> source = ReadOnlySpan<byte>.Empty;
            EnsureH8lrLoreStore();
            if (TryGetH8lrUtf8(hash, out ReadOnlySpan<byte> h8lrSource))
                source = h8lrSource;

            if (source.Length == 0)
            {
                EnsureBabelStore();
                if (_babelStore != null && _babelStore.IsOpen)
                {
                    source = _babelStore.FetchUtf8(hash);
                    if (IsBabelErrorSentinel(source))
                        source = ReadOnlySpan<byte>.Empty;
                }
            }

            if (source.Length == 0)
            {
                SeedMockLoreDatabase();
                TryGetMockUtf8(hash, out source);
            }

            if (source.Length == 0)
                return false;

            int byteCount = math.min(64, source.Length);
            if (!ZeroGCFormatter.AppendToSpan("UTF8 HEX ".AsSpan(), destination, ref written) ||
                !AppendHex8(destination, ref written, hash) ||
                !ZeroGCFormatter.AppendChar('\n', destination, ref written))
            {
                return false;
            }

            for (int i = 0; i < byteCount; i++)
            {
                if (!AppendHexByte(destination, ref written, source[i]) ||
                    !ZeroGCFormatter.AppendChar(' ', destination, ref written))
                {
                    return false;
                }
            }

            if (!ZeroGCFormatter.AppendChar('\n', destination, ref written))
                return false;

            for (int i = 0; i < byteCount; i++)
            {
                byte b = source[i];
                char c = b >= 32 && b <= 126 ? (char)b : '.';
                if (!ZeroGCFormatter.AppendChar(c, destination, ref written))
                    return false;
            }

            return true;
        }
#endif

        private void ConsumeScanSignals()
        {
            ReadOnlySpan<ScanCompleteSignal> scanSignals = SignalBus<ScanCompleteSignal>.GetFrameSnapshot();
            for (int i = 0; i < scanSignals.Length; i++)
            {
                ScanCompleteSignal signal = scanSignals[i];
                if (signal.EntryHash == 0u)
                    continue;

                PdaAup48 signalAup = CaptureSignalAup(in signal);
                UnlockEntry(signal.EntryHash, in signalAup, signal.SourceId, ResolvePdaFrame(), true);
                _pendingSelectHash = signal.EntryHash;
            }

            ReadOnlySpan<LoreFragmentScannedSignal> loreSignals = SignalBus<LoreFragmentScannedSignal>.GetFrameSnapshot();
            for (int i = 0; i < loreSignals.Length; i++)
            {
                LoreFragmentScannedSignal signal = loreSignals[i];
                if (signal.Hash == 0u)
                    continue;

                PdaAup48 aup = default;
                if (TryReadLastDiscoveryAup(out PdaAup48 lastAup))
                    aup = lastAup;

                UnlockEntry(signal.Hash, in aup, signal.SourceId, signal.Frame, false);
                _pendingSelectHash = signal.Hash;
            }
        }

        private void BeginEntry(uint hash)
        {
            if (hash == 0u)
                hash = DefaultEntryHash;

            if (!_bodyLease.IsValid)
            {
                SetFault(FaultMissingText);
                return;
            }

            _activeEntryHash = hash;
            _sourceByteCursor = 0;
            _decodedLength = 0;
            _visibleLength = 0;
            _lastSubmittedLength = -1;
            _activeSourceBytes = 0;
            _charAccumulator = 0f;
            _lastFaultHash = 0u;
            _needsEntryReload = false;
            ResetActiveSourceCache();
            ResetTypewriterState();

            if (!IsUnlocked(hash))
            {
                _streamState = PdaEncyclopediaStreamState.Locked;
                WriteLockedBody(hash);
                WriteTitle(hash);
                WriteMeta(hash);
                return;
            }

            _streamState = PdaEncyclopediaStreamState.Loading;
            WriteTitle(hash);
            WriteMeta(hash);
            SubmitBodyIfChanged();
        }

        private bool UnlockEntry(
            uint hash,
            in PdaAup48 aup,
            uint sourceId,
            uint frame,
            bool hasPreciseAup)
        {
            if (!EnsureVaultBuffers() || hash == 0u)
                return false;

            ushort bitIndex = EnsureBitIndex(hash);
            int wordIndex = bitIndex >> 6;
            int bitInWord = bitIndex & 63;
            ulong bit = 1UL << bitInWord;
            ref EncyclopediaStateDTO mask = ref GetVaultElementRef(in _unlockMaskHandle, UnlockMaskBufferId, 0);
            ref ulong word = ref GetMaskWordRef(ref mask, wordIndex);
            bool wasLocked = AtomicOr(ref word, bit);

            ref PdaEncyclopediaRuntimeStateDTO state = ref GetVaultElementRef(in _runtimeStateHandle, RuntimeStateBufferId, 0);
            state.Magic = StateMagic;
            state.LastEntryHash = hash;
            state.LastFrame = frame;
            state.LastSourceId = sourceId;
            state.ActiveBitIndex = bitIndex;
            state.GlobalQualityWeight = math.saturate(HomeostasisBrain.GlobalQualityWeight);
            mask.Magic = StateMagic;
            mask.LastEntryHash = hash;
            mask.LastFrame = frame;
            mask.LastSourceId = sourceId;
            mask.ActiveBitIndex = bitIndex;
            mask.GlobalQualityWeight = state.GlobalQualityWeight;
            mask.StreamState = (uint)_streamState;
            if (wasLocked)
            {
                state.UnlockedCount = math.min((uint)UnlockBitCount, state.UnlockedCount + 1u);
                state.Revision++;
                mask.UnlockedCount = state.UnlockedCount;
                mask.Revision = state.Revision;
            }
            else
            {
                mask.UnlockedCount = state.UnlockedCount;
                mask.Revision = state.Revision;
            }

            if (hasPreciseAup)
            {
                state.LastDiscoveryGridX = aup.GridX;
                state.LastDiscoveryGridY = aup.GridY;
                state.LastDiscoveryGridZ = aup.GridZ;
                state.LastDiscoveryLocalX = aup.LocalX;
                state.LastDiscoveryLocalY = aup.LocalY;
                state.LastDiscoveryLocalZ = aup.LocalZ;
                state.Flags |= StateFlagPreciseAup;
                mask.LastDiscoveryGridX = aup.GridX;
                mask.LastDiscoveryGridY = aup.GridY;
                mask.LastDiscoveryGridZ = aup.GridZ;
                mask.LastDiscoveryLocalX = aup.LocalX;
                mask.LastDiscoveryLocalY = aup.LocalY;
                mask.LastDiscoveryLocalZ = aup.LocalZ;
                mask.Flags |= StateFlagPreciseAup;
            }

            ref PdaEncyclopediaEntryMetaDTO meta = ref GetVaultElementRef(in _metadataHandle, MetadataBufferId, bitIndex);
            meta.EntryHash = hash;
            meta.BitIndex = bitIndex;
            meta.SourceId = sourceId;
            meta.Revision = state.Revision;
            meta.LastFrame = frame;
            if (hasPreciseAup)
            {
                meta.DiscoveryGridX = aup.GridX;
                meta.DiscoveryGridY = aup.GridY;
                meta.DiscoveryGridZ = aup.GridZ;
                meta.DiscoveryLocalX = aup.LocalX;
                meta.DiscoveryLocalY = aup.LocalY;
                meta.DiscoveryLocalZ = aup.LocalZ;
                meta.Flags |= 1;
            }

            return wasLocked;
        }

        private bool IsUnlocked(uint hash)
        {
            if (!EnsureVaultBuffers())
                return false;

            if (!TryFindBitIndex(hash, out ushort bitIndex))
                return false;

            ref EncyclopediaStateDTO mask = ref GetVaultElementRef(in _unlockMaskHandle, UnlockMaskBufferId, 0);
            ulong word = GetMaskWordRef(ref mask, bitIndex >> 6);
            ulong bit = 1UL << (bitIndex & 63);
            return (word & bit) != 0UL;
        }

        private ushort EnsureBitIndex(uint hash)
        {
            if (TryFindBitIndex(hash, out ushort existingBitIndex))
                return existingBitIndex;

            int start = (int)(hash & (UnlockBitCount - 1));
            for (int probe = 0; probe < UnlockBitCount; probe++)
            {
                int index = (start + probe) & (UnlockBitCount - 1);
                ref PdaEncyclopediaEntryMetaDTO meta = ref GetVaultElementRef(in _metadataHandle, MetadataBufferId, index);
                if (meta.EntryHash != 0u && meta.EntryHash != hash)
                    continue;

                if (meta.EntryHash == 0u)
                {
                    ref PdaEncyclopediaRuntimeStateDTO state = ref GetVaultElementRef(in _runtimeStateHandle, RuntimeStateBufferId, 0);
                    state.MetadataCount = math.min((uint)UnlockBitCount, state.MetadataCount + 1u);
                    ref EncyclopediaStateDTO encyclopedia = ref GetVaultElementRef(in _unlockMaskHandle, UnlockMaskBufferId, 0);
                    encyclopedia.MetadataCount = state.MetadataCount;
                    meta.EntryHash = hash;
                    meta.BitIndex = (ushort)index;
                    meta.TitleHash = hash;
                }

                return (ushort)index;
            }

            return (ushort)start;
        }

        private bool TryFindBitIndex(uint hash, out ushort bitIndex)
        {
            if (!_vaultReady || _vault == null || hash == 0u)
            {
                bitIndex = 0;
                return false;
            }

            int start = (int)(hash & (UnlockBitCount - 1));
            for (int probe = 0; probe < UnlockBitCount; probe++)
            {
                int index = (start + probe) & (UnlockBitCount - 1);
                ref PdaEncyclopediaEntryMetaDTO meta = ref GetVaultElementRef(in _metadataHandle, MetadataBufferId, index);
                if (meta.EntryHash == hash)
                {
                    bitIndex = (ushort)index;
                    return true;
                }

                if (meta.EntryHash == 0u)
                    break;
            }

            bitIndex = 0;
            return false;
        }

        private ReadOnlySpan<byte> SelectActiveUtf8Source()
        {
            if (TryGetH8lrUtf8(_activeEntryHash, out ReadOnlySpan<byte> h8lrUtf8))
            {
                CacheActiveSource(h8lrUtf8, TextSourceH8lr);
                return h8lrUtf8;
            }

            if (_babelStore != null && _babelStore.IsOpen)
            {
                ReadOnlySpan<byte> mappedUtf8 = _babelStore.FetchUtf8(_activeEntryHash);
                if (mappedUtf8.Length > 0 && !IsBabelErrorSentinel(mappedUtf8))
                {
                    CacheActiveSource(mappedUtf8, TextSourceBabel);
                    return mappedUtf8;
                }
            }

            if (TryGetMockUtf8(_activeEntryHash, out ReadOnlySpan<byte> mockUtf8))
            {
                CacheActiveSource(mockUtf8, TextSourceVaultMock);
                return mockUtf8;
            }

            return ReadOnlySpan<byte>.Empty;
        }

        private bool TryGetH8lrUtf8(uint hash, out ReadOnlySpan<byte> utf8)
        {
            utf8 = ReadOnlySpan<byte>.Empty;
            PdaH8lrLoreStore store = _h8lrLoreStore;
            return store != null && store.IsOpen && store.TryGetUtf8(hash, out utf8);
        }

        private void CacheActiveSource(ReadOnlySpan<byte> source, uint sourceFlags)
        {
            if (source.Length <= 0)
            {
                ResetActiveSourceCache();
                return;
            }

            _activeSourceBytes = source.Length;
            _activeUtf8SourceFlags = sourceFlags;
        }

        private void ResetActiveSourceCache()
        {
            _activeSourceBytes = 0;
            _activeUtf8SourceFlags = 0u;
        }

        private static bool IsBabelErrorSentinel(ReadOnlySpan<byte> source)
        {
            return source.Length == 5 &&
                   source[0] == (byte)'E' &&
                   source[1] == (byte)'R' &&
                   source[2] == (byte)'R' &&
                   source[3] == (byte)'O' &&
                   source[4] == (byte)'R';
        }

        private void TryAdvanceTextWindowForLongEntry(int sourceLength)
        {
            if (!_bodyLease.IsValid ||
                sourceLength <= 0 ||
                _sourceByteCursor >= sourceLength ||
                _decodedLength < _bodyLease.Span.Length ||
                _visibleLength < _decodedLength)
            {
                return;
            }

            _decodedLength = 0;
            _visibleLength = 0;
            _lastSubmittedLength = -1;
            _charAccumulator = 0f;
            ResetTypewriterState();
        }

        private int DecodeUtf8Budgeted(ReadOnlySpan<byte> source, Span<char> destination, int budgetChars)
        {
            int sourceCursor = _sourceByteCursor;
            int outputCursor = _decodedLength;
            int produced = 0;
            int budget = math.max(1, budgetChars);

            while (sourceCursor < source.Length && outputCursor < destination.Length && produced < budget)
            {
                byte b0 = source[sourceCursor];
                if (b0 < 0x80)
                {
                    if (b0 == (byte)'^' &&
                        TryAppendTokenReplacement(source, ref sourceCursor, destination, ref outputCursor, ref produced))
                    {
                        continue;
                    }

                    destination[outputCursor++] = (char)b0;
                    sourceCursor++;
                    produced++;
                    continue;
                }

                if ((b0 & 0xE0) == 0xC0 && sourceCursor + 1 < source.Length)
                {
                    byte b1 = source[sourceCursor + 1];
                    int scalar = ((b0 & 0x1F) << 6) | (b1 & 0x3F);
                    if ((b1 & 0xC0) == 0x80 && scalar >= 0x80)
                    {
                        destination[outputCursor++] = (char)scalar;
                        sourceCursor += 2;
                        produced++;
                        continue;
                    }
                }
                else if ((b0 & 0xF0) == 0xE0 && sourceCursor + 2 < source.Length)
                {
                    byte b1 = source[sourceCursor + 1];
                    byte b2 = source[sourceCursor + 2];
                    int scalar = ((b0 & 0x0F) << 12) | ((b1 & 0x3F) << 6) | (b2 & 0x3F);
                    if ((b1 & 0xC0) == 0x80 && (b2 & 0xC0) == 0x80 && scalar >= 0x800)
                    {
                        destination[outputCursor++] = (char)scalar;
                        sourceCursor += 3;
                        produced++;
                        continue;
                    }
                }
                else if ((b0 & 0xF8) == 0xF0 && sourceCursor + 3 < source.Length)
                {
                    if (outputCursor + 1 >= destination.Length || produced + 2 > budget)
                        break;

                    byte b1 = source[sourceCursor + 1];
                    byte b2 = source[sourceCursor + 2];
                    byte b3 = source[sourceCursor + 3];
                    int scalar = ((b0 & 0x07) << 18) | ((b1 & 0x3F) << 12) | ((b2 & 0x3F) << 6) | (b3 & 0x3F);
                    if ((b1 & 0xC0) == 0x80 &&
                        (b2 & 0xC0) == 0x80 &&
                        (b3 & 0xC0) == 0x80 &&
                        scalar >= 0x10000 &&
                        scalar <= 0x10FFFF)
                    {
                        int shifted = scalar - 0x10000;
                        destination[outputCursor++] = (char)(0xD800 + (shifted >> 10));
                        destination[outputCursor++] = (char)(0xDC00 + (shifted & 0x3FF));
                        sourceCursor += 4;
                        produced += 2;
                        continue;
                    }
                }

                destination[outputCursor++] = '\uFFFD';
                sourceCursor++;
                produced++;
                _lastFaultHash = FaultUtf8Invalid;
            }

            _sourceByteCursor = sourceCursor;
            _decodedLength = outputCursor;
            return produced;
        }

        private bool TryAppendTokenReplacement(
            ReadOnlySpan<byte> source,
            ref int sourceCursor,
            Span<char> destination,
            ref int outputCursor,
            ref int produced)
        {
            int tokenStart = sourceCursor + 1;
            int tokenEnd = tokenStart;
            int maxTokenEnd = math.min(source.Length, tokenStart + 32);
            while (tokenEnd < maxTokenEnd && source[tokenEnd] != (byte)'^')
                tokenEnd++;

            if (tokenEnd >= source.Length || tokenEnd >= maxTokenEnd || source[tokenEnd] != (byte)'^')
                return false;

            uint tokenHash = ComputeAsciiHash(source.Slice(tokenStart, tokenEnd - tokenStart));
            int before = outputCursor;
            if (!TryAppendTokenValue(tokenHash, destination, ref outputCursor))
                return false;

            produced += outputCursor - before;
            sourceCursor = tokenEnd + 1;
            return true;
        }

        private bool TryAppendTokenValue(uint tokenHash, Span<char> destination, ref int cursor)
        {
            if (tokenHash == TokenDepthHash)
            {
                if (!ZeroGCFormatter.AppendToSpan("DPT ".AsSpan(), destination, ref cursor))
                    return false;

                int meters = 0;
                if (EnsureVaultBuffers())
                {
                    ref PdaEncyclopediaRuntimeStateDTO state = ref GetVaultElementRef(in _runtimeStateHandle, RuntimeStateBufferId, 0);
                    meters = (int)math.round(math.abs(state.LastDiscoveryLocalY));
                }

                return ZeroGCFormatter.AppendInt(meters, destination, ref cursor) &&
                       ZeroGCFormatter.AppendChar('m', destination, ref cursor);
            }

            if (tokenHash == TokenEntryHashHash)
                return AppendHex8(destination, ref cursor, _activeEntryHash);

            if (tokenHash == TokenQualityHash)
            {
                int percent = (int)math.round(math.saturate(HomeostasisBrain.GlobalQualityWeight) * 100f);
                return ZeroGCFormatter.AppendInt(percent, destination, ref cursor) &&
                       ZeroGCFormatter.AppendChar('%', destination, ref cursor);
            }

            if (tokenHash == TokenDiscoveryGridHash)
            {
                if (!EnsureVaultBuffers())
                    return false;

                ref PdaEncyclopediaRuntimeStateDTO state = ref GetVaultElementRef(in _runtimeStateHandle, RuntimeStateBufferId, 0);
                return ZeroGCFormatter.AppendInt(ClampLongToInt(state.LastDiscoveryGridX), destination, ref cursor) &&
                       ZeroGCFormatter.AppendChar('/', destination, ref cursor) &&
                       ZeroGCFormatter.AppendInt(ClampLongToInt(state.LastDiscoveryGridY), destination, ref cursor) &&
                       ZeroGCFormatter.AppendChar('/', destination, ref cursor) &&
                       ZeroGCFormatter.AppendInt(ClampLongToInt(state.LastDiscoveryGridZ), destination, ref cursor);
            }

            if (tokenHash == TokenDiscoveryDistanceHash || tokenHash == TokenDistanceHash)
            {
                if (!TryResolveDiscoveryDistanceMeters(out int meters))
                    return false;

                return ZeroGCFormatter.AppendInt(meters, destination, ref cursor) &&
                       ZeroGCFormatter.AppendChar('m', destination, ref cursor);
            }

            return false;
        }

        private bool TryResolveDiscoveryDistanceMeters(out int meters)
        {
            meters = 0;
            if (!EnsureVaultBuffers())
                return false;

            IPlayerRuntimeContext player = _playerContext;
            if (player == null || !player.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot))
                return false;

            ref PdaEncyclopediaRuntimeStateDTO state = ref GetVaultElementRef(in _runtimeStateHandle, RuntimeStateBufferId, 0);
            PdaAup48 discovery = new PdaAup48
            {
                GridX = state.LastDiscoveryGridX,
                GridY = state.LastDiscoveryGridY,
                GridZ = state.LastDiscoveryGridZ,
                LocalX = state.LastDiscoveryLocalX,
                LocalY = state.LastDiscoveryLocalY,
                LocalZ = state.LastDiscoveryLocalZ
            };

            PdaAup48 playerAup = CaptureSnapshotAup(in snapshot);
            double3 deltaMeters = ResolveAupDeltaMetersClamped(in discovery, in playerAup);
            float3 localDelta = new float3((float)deltaMeters.x, (float)deltaMeters.y, (float)deltaMeters.z);
            float distanceSq = math.lengthsq(localDelta);
            if (!math.isfinite(distanceSq))
                return false;

            float distance = math.sqrt(math.max(0f, distanceSq));
            if (!math.isfinite(distance))
                return false;

            meters = distance >= int.MaxValue ? int.MaxValue : (int)math.round(math.max(0f, distance));
            return true;
        }

        private uint StepVisibleCharacters(float quality)
        {
            if (_visibleLength >= _decodedLength)
                return 0u;

            if (!EnsureVaultBuffers())
                return StepVisibleCharactersScalar(quality);

            NativeArray<PdaTypewriterStateDTO> typewriter = ResolveVaultBuffer(in _typewriterStateHandle, TypewriterStateBufferId);
            if (!typewriter.IsCreated)
                return StepVisibleCharactersScalar(quality);

            ref PdaTypewriterStateDTO stateRef = ref GetVaultElementRef(in _typewriterStateHandle, TypewriterStateBufferId, 0);
            StepTypewriterScalar(ref stateRef, quality, SystemDispatcher.CurrentFrameDeltaTime, _decodedLength, _visibleLength, ResolvePdaFrame());
            PdaTypewriterStateDTO state = stateRef;
            int previousVisible = _visibleLength;
            _visibleLength = math.clamp((int)state.VisibleChars, 0, _decodedLength);
            _charAccumulator = state.CharAccumulator;
            return (uint)math.max(0, _visibleLength - previousVisible);
        }

        private uint StepVisibleCharactersScalar(float quality)
        {
            if (_visibleLength >= _decodedLength)
                return 0u;

            float deltaTime = SystemDispatcher.CurrentFrameDeltaTime;
            if (!math.isfinite(deltaTime) || deltaTime <= 0f)
                deltaTime = 1f / 60f;

            float q = math.saturate(quality);
            float curve = q * q * (3f - (2f * q));
            float charsPerSecond = math.lerp(18f, 1600f, curve);
            _charAccumulator += charsPerSecond * deltaTime;
            int step = (int)math.floor(_charAccumulator);
            if (step <= 0)
                step = 1;

            int previousVisible = _visibleLength;
            _charAccumulator = math.max(0f, _charAccumulator - step);
            _visibleLength = math.min(_decodedLength, _visibleLength + step);
            return (uint)math.max(0, _visibleLength - previousVisible);
        }

        private static void StepTypewriterScalar(
            ref PdaTypewriterStateDTO state,
            float globalQualityWeight,
            float deltaTime,
            int decodedLength,
            int visibleLength,
            uint frame)
        {
            float q = math.saturate(globalQualityWeight);
            float dt = math.isfinite(deltaTime) && deltaTime > 0f ? deltaTime : 1f / 60f;
            int decoded = math.max(0, decodedLength);
            int visible = math.clamp(visibleLength, 0, decoded);
            if (visible >= decoded)
            {
                state.CharAccumulator = 0f;
                state.GlobalQualityWeight = q;
                state.VisibleChars = (uint)visible;
                state.DecodedChars = (uint)decoded;
                state.CharsRenderedThisFrame = 0u;
                state.LastFrame = frame;
                state.StateHash = HashTypewriterState(visible, decoded, q);
                return;
            }

            float curve = q * q * (3f - (2f * q));
            float charsPerSecond = math.lerp(18f, 1600f, curve);
            float accumulator = state.CharAccumulator + (charsPerSecond * dt);
            int step = math.max(1, (int)math.floor(accumulator));
            int nextVisible = math.min(decoded, visible + step);

            state.CharAccumulator = math.max(0f, accumulator - step);
            state.GlobalQualityWeight = q;
            state.VisibleChars = (uint)nextVisible;
            state.DecodedChars = (uint)decoded;
            state.CharsRenderedThisFrame = (uint)math.max(0, nextVisible - visible);
            state.LastFrame = frame;
            state.StateHash = HashTypewriterState(nextVisible, decoded, q);
        }

        private static uint HashTypewriterState(int visible, int decoded, float quality)
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)visible) * 16777619u;
            hash = (hash ^ (uint)decoded) * 16777619u;
            hash = (hash ^ math.asuint(quality)) * 16777619u;
            return hash;
        }

        private void ResetTypewriterState()
        {
            if (!EnsureVaultBuffers())
                return;

            ref PdaTypewriterStateDTO state = ref GetVaultElementRef(in _typewriterStateHandle, TypewriterStateBufferId, 0);
            state = default;
        }

        private int ResolveDecodeBudget(float quality)
        {
            float q = math.saturate(quality);
            return (int)math.round(math.lerp(32f, 2048f, q));
        }

        private void SubmitBodyIfChanged()
        {
            if (bodyText == null || !_bodyLease.IsValid)
                return;

            int safeLength = math.clamp(_visibleLength, 0, math.min(_decodedLength, _bodyLease.Buffer.Length));
            if (safeLength == _lastSubmittedLength)
                return;

            bodyText.SetCharArray(_bodyLease.Buffer, 0, safeLength);
            _lastSubmittedLength = safeLength;
        }

        private void WriteLockedBody(uint hash)
        {
            Span<char> span = _bodyLease.Span;
            int cursor = 0;
            ZeroGCFormatter.AppendToSpan("LOCKED // SCAN ENTRY ".AsSpan(), span, ref cursor);
            AppendHex8(span, ref cursor, hash);
            ZeroGCFormatter.AppendToSpan(" TO STREAM LORE.".AsSpan(), span, ref cursor);
            _decodedLength = cursor;
            _visibleLength = cursor;
            SubmitBodyIfChanged();
        }

        private void WriteTitle(uint hash)
        {
            if (titleText == null || !_titleLease.IsValid)
                return;

            Span<char> span = _titleLease.Buffer.AsSpan(0, math.min(TitleBufferCapacity, _titleLease.Buffer.Length));
            int cursor = 0;
            ZeroGCFormatter.AppendToSpan("ENCYCLOPEDIA // ".AsSpan(), span, ref cursor);
            AppendHex8(span, ref cursor, hash);
            titleText.SetCharArray(_titleLease.Buffer, 0, cursor);
        }

        private void WriteMeta(uint hash)
        {
            if (metaText == null || !_metaLease.IsValid || !EnsureVaultBuffers())
                return;

            Span<char> span = _metaLease.Buffer.AsSpan(0, math.min(MetaBufferCapacity, _metaLease.Buffer.Length));
            int cursor = 0;
            ref PdaEncyclopediaRuntimeStateDTO state = ref GetVaultElementRef(in _runtimeStateHandle, RuntimeStateBufferId, 0);
            ZeroGCFormatter.AppendToSpan("UNLOCKED ".AsSpan(), span, ref cursor);
            ZeroGCFormatter.AppendInt((int)state.UnlockedCount, span, ref cursor);
            ZeroGCFormatter.AppendToSpan("/256 | BIT ".AsSpan(), span, ref cursor);
            if (TryFindBitIndex(hash, out ushort bitIndex))
                ZeroGCFormatter.AppendInt(bitIndex, span, ref cursor);
            else
                ZeroGCFormatter.AppendChar('-', span, ref cursor);

            ZeroGCFormatter.AppendToSpan(" | GRID ".AsSpan(), span, ref cursor);
            ZeroGCFormatter.AppendInt(ClampLongToInt(state.LastDiscoveryGridX), span, ref cursor);
            ZeroGCFormatter.AppendChar('/', span, ref cursor);
            ZeroGCFormatter.AppendInt(ClampLongToInt(state.LastDiscoveryGridY), span, ref cursor);
            ZeroGCFormatter.AppendChar('/', span, ref cursor);
            ZeroGCFormatter.AppendInt(ClampLongToInt(state.LastDiscoveryGridZ), span, ref cursor);
            metaText.SetCharArray(_metaLease.Buffer, 0, cursor);
        }

        private void WriteRuntimeState(float quality, long decodeTicks, long canvasTicks)
        {
            if (!EnsureVaultBuffers())
                return;

            ref PdaEncyclopediaRuntimeStateDTO state = ref GetVaultElementRef(in _runtimeStateHandle, RuntimeStateBufferId, 0);
            state.Magic = StateMagic;
            state.GlobalQualityWeight = math.saturate(quality);
            state.LastEntryHash = _activeEntryHash;
            state.LastFrame = ResolvePdaFrame();
            state.CursorByte = (uint)math.max(0, _sourceByteCursor);
            state.DecodedChars = (uint)math.max(0, _decodedLength);
            state.VisibleChars = (uint)math.max(0, _visibleLength);
            state.SourceBytes = (uint)math.max(0, _activeSourceBytes);
            state.DecodeTicks = decodeTicks;
            state.CanvasTicks = canvasTicks;
            state.FaultHash = _lastFaultHash;
            state.StreamState = (uint)_streamState;
            state.Flags = ComposeStateFlags(state.Flags);
            state.StateHash = ComputeRuntimeStateHash(in state);

            ref EncyclopediaStateDTO encyclopedia = ref GetVaultElementRef(in _unlockMaskHandle, UnlockMaskBufferId, 0);
            encyclopedia.Magic = StateMagic;
            encyclopedia.LastEntryHash = _activeEntryHash;
            encyclopedia.GlobalQualityWeight = state.GlobalQualityWeight;
            encyclopedia.CursorByte = state.CursorByte;
            encyclopedia.DecodedChars = state.DecodedChars;
            encyclopedia.VisibleChars = state.VisibleChars;
            encyclopedia.StreamState = state.StreamState;
            encyclopedia.Flags = ComposeStateFlags(encyclopedia.Flags);
        }

        private void RecordTelemetry(uint charsRenderedThisFrame, long decodeTicks, long canvasTicks)
        {
            if (!EnsureVaultBuffers())
                return;

            ref int cursor = ref GetVaultElementRef(in _telemetryCursorHandle, TelemetryCursorBufferId, 0);
            int index = cursor;
            if ((uint)index >= TelemetryFrameCount)
                index = 0;

            ref PdaEncyclopediaTelemetryEntry entry = ref GetVaultElementRef(in _telemetryHandle, TelemetryBufferId, index);
            entry.Frame = ResolvePdaFrame();
            entry.EntryHash = _activeEntryHash;
            entry.UnlockedCount = GetVaultElementRef(in _runtimeStateHandle, RuntimeStateBufferId, 0).UnlockedCount;
            entry.CharsRenderedThisFrame = charsRenderedThisFrame;
            entry.VisibleChars = (uint)math.max(0, _visibleLength);
            entry.DecodedChars = (uint)math.max(0, _decodedLength);
            entry.SourceBytes = (uint)math.max(0, _activeSourceBytes);
            entry.DecodeTicks = decodeTicks;
            entry.CanvasTicks = canvasTicks;
            entry.Flags = ((uint)_streamState & 0xFFu) |
                          ((_activeUtf8SourceFlags & 3u) << StateFlagSourceShift) |
                          (_canvasSplitReady ? TelemetryFlagCanvasSplit : 0u);
            entry.FaultHash = _lastFaultHash;
            entry.CursorByte = (uint)math.max(0, _sourceByteCursor);
            entry.Capacity = CharBufferPool.EncyclopediaPageCapacity;
            entry.StateHash = ComputeTelemetryHash(in entry);

            cursor = index + 1;
            if (cursor >= TelemetryFrameCount)
                cursor = 0;
        }

        private bool EnsureVaultBuffers()
        {
            return _vaultReady &&
                   _vault != null &&
                   ArePdaHandlesCreated();
        }

        private NativeArray<T> ResolveVaultBuffer<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId)
            where T : unmanaged
        {
            return TryResolveVaultBuffer(in handle, expectedBufferId, out NativeArray<T> buffer) ? buffer : default;
        }

        private ref T GetVaultElementRef<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId, int index)
            where T : unmanaged
        {
            if (!TryResolveVaultBuffer(in handle, expectedBufferId, out NativeArray<T> buffer) ||
                (uint)index >= (uint)buffer.Length)
            {
                FatalMemoryException.ThrowStaleVaultHandle();
            }

            void* basePtr = NativeArrayUnsafeUtility.GetUnsafePtr(buffer);
            return ref UnsafeUtility.AsRef<T>((byte*)basePtr + (index * UnsafeUtility.SizeOf<T>()));
        }

        private bool TryResolveVaultBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            out NativeArray<T> buffer)
            where T : unmanaged
        {
            buffer = default;
            if (_vault == null ||
                _vault.IsCompactionFenceActive ||
                !IsPdaHandleCreated(in handle, expectedBufferId) ||
                !_vault.TryResolveHandle(in handle, out buffer) ||
                _vault.IsCompactionFenceActive ||
                !buffer.IsCreated ||
                buffer.Length <= 0)
            {
                InvalidateVaultDescriptors();
                return false;
            }

            return true;
        }

        private static bool IsPdaHandleCreated<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId)
            where T : unmanaged
        {
            return handle.BufferID == unchecked((uint)(int)expectedBufferId) &&
                   handle.SystemID == (uint)VaultOwnerSystemId &&
                   handle.Generation != 0u;
        }

        private bool ArePdaHandlesCreated()
        {
            return IsPdaHandleCreated(in _unlockMaskHandle, UnlockMaskBufferId) &&
                   IsPdaHandleCreated(in _runtimeStateHandle, RuntimeStateBufferId) &&
                   IsPdaHandleCreated(in _metadataHandle, MetadataBufferId) &&
                   IsPdaHandleCreated(in _telemetryHandle, TelemetryBufferId) &&
                   IsPdaHandleCreated(in _telemetryCursorHandle, TelemetryCursorBufferId) &&
                   IsPdaHandleCreated(in _mockUtf8Handle, MockUtf8BufferId) &&
                   IsPdaHandleCreated(in _mockIndexHandle, MockIndexBufferId) &&
                   IsPdaHandleCreated(in _csvScratchHandle, CsvScratchBufferId) &&
                   IsPdaHandleCreated(in _typewriterStateHandle, TypewriterStateBufferId) &&
                   IsPdaHandleCreated(in _h8lrMirrorHandle, H8lrMirrorBufferId);
        }

        private bool ArePdaBuffersResolvable()
        {
            return IsPdaBufferResolvable(in _unlockMaskHandle, UnlockMaskBufferId, 1) &&
                   IsPdaBufferResolvable(in _runtimeStateHandle, RuntimeStateBufferId, 1) &&
                   IsPdaBufferResolvable(in _metadataHandle, MetadataBufferId, MaxMetadataEntries) &&
                   IsPdaBufferResolvable(in _telemetryHandle, TelemetryBufferId, TelemetryFrameCount) &&
                   IsPdaBufferResolvable(in _telemetryCursorHandle, TelemetryCursorBufferId, 1) &&
                   IsPdaBufferResolvable(in _mockUtf8Handle, MockUtf8BufferId, MockUtf8Bytes) &&
                   IsPdaBufferResolvable(in _mockIndexHandle, MockIndexBufferId, MockEntryCapacity) &&
                   IsPdaBufferResolvable(in _csvScratchHandle, CsvScratchBufferId, CsvScratchBytes) &&
                   IsPdaBufferResolvable(in _typewriterStateHandle, TypewriterStateBufferId, 1) &&
                   IsPdaBufferResolvable(in _h8lrMirrorHandle, H8lrMirrorBufferId, H8lrMirrorBytes);
        }

        private bool IsPdaBufferResolvable<T>(
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            int requiredLength)
            where T : unmanaged
        {
            return TryResolveVaultBuffer(in handle, expectedBufferId, out NativeArray<T> buffer) &&
                   buffer.Length >= requiredLength;
        }

        private void InvalidateVaultDescriptors()
        {
            _vaultReady = false;
        }

        private void BindDataVaultForLifecycle(IDataVault nextVault, IDataVault fallbackReleaseVault = null)
        {
            if (ReferenceEquals(_vault, nextVault))
                return;

            ReleasePdaVaultHandles(_vault ?? fallbackReleaseVault);
            _vault = nextVault;
            _vaultReady = false;
            _mockSeeded = false;
            _h8lrMetadataSeeded = false;
            _coldBootstrapAttempted = false;
            ResetActiveSourceCache();
        }

        private void ReleasePdaVaultHandles(IDataVault vault)
        {
            ReleasePdaVaultHandle(vault, ref _unlockMaskHandle, UnlockMaskBufferId);
            ReleasePdaVaultHandle(vault, ref _runtimeStateHandle, RuntimeStateBufferId);
            ReleasePdaVaultHandle(vault, ref _metadataHandle, MetadataBufferId);
            ReleasePdaVaultHandle(vault, ref _telemetryHandle, TelemetryBufferId);
            ReleasePdaVaultHandle(vault, ref _telemetryCursorHandle, TelemetryCursorBufferId);
            ReleasePdaVaultHandle(vault, ref _mockUtf8Handle, MockUtf8BufferId);
            ReleasePdaVaultHandle(vault, ref _mockIndexHandle, MockIndexBufferId);
            ReleasePdaVaultHandle(vault, ref _csvScratchHandle, CsvScratchBufferId);
            ReleasePdaVaultHandle(vault, ref _typewriterStateHandle, TypewriterStateBufferId);
            ReleasePdaVaultHandle(vault, ref _h8lrMirrorHandle, H8lrMirrorBufferId);
        }

        private static void ReleasePdaVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID expectedBufferId)
            where T : unmanaged
        {
            if (vault != null && IsPdaHandleCreated(in handle, expectedBufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private void TryColdBootstrap()
        {
            if (_vaultReady)
                return;

            EnsureTextLeases();
            _coldBootstrapAttempted = true;
            if (!EnsureVaultBuffersCold())
                return;

            EnsureH8lrLoreStore();
            EnsureBabelStore();
            SeedMockLoreDatabase();
        }

        private bool EnsureVaultBuffersCold()
        {
            if (EnsureVaultBuffers())
                return true;

            if (!TryBindVaultCold())
            {
                _lastFaultHash = FaultMissingVault;
                _streamState = PdaEncyclopediaStreamState.Fault;
                return false;
            }

            _unlockMaskHandle = _vault.EnsureGenerationHandle<EncyclopediaStateDTO>(
                UnlockMaskBufferId,
                1,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            _runtimeStateHandle = _vault.EnsureGenerationHandle<PdaEncyclopediaRuntimeStateDTO>(
                RuntimeStateBufferId,
                1,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            _metadataHandle = _vault.EnsureGenerationHandle<PdaEncyclopediaEntryMetaDTO>(
                MetadataBufferId,
                MaxMetadataEntries,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            _telemetryHandle = _vault.EnsureGenerationHandle<PdaEncyclopediaTelemetryEntry>(
                TelemetryBufferId,
                TelemetryFrameCount,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            _telemetryCursorHandle = _vault.EnsureGenerationHandle<int>(
                TelemetryCursorBufferId,
                1,
                SystemID.UI,
                NativeArrayOptions.ClearMemory);
            _mockUtf8Handle = _vault.EnsureGenerationHandle<byte>(
                MockUtf8BufferId,
                MockUtf8Bytes,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            _mockIndexHandle = _vault.EnsureGenerationHandle<BabelIndexDTO>(
                MockIndexBufferId,
                MockEntryCapacity,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            _csvScratchHandle = _vault.EnsureGenerationHandle<byte>(
                CsvScratchBufferId,
                CsvScratchBytes,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            _typewriterStateHandle = _vault.EnsureGenerationHandle<PdaTypewriterStateDTO>(
                TypewriterStateBufferId,
                1,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);
            _h8lrMirrorHandle = _vault.EnsureGenerationHandle<byte>(
                H8lrMirrorBufferId,
                H8lrMirrorBytes,
                SystemID.UI,
                NativeArrayOptions.UninitializedMemory);

            if (!ArePdaBuffersResolvable())
                return false;

            NativeArray<PdaEncyclopediaRuntimeStateDTO> state = ResolveVaultBuffer(in _runtimeStateHandle, RuntimeStateBufferId);
            NativeArray<EncyclopediaStateDTO> mask = ResolveVaultBuffer(in _unlockMaskHandle, UnlockMaskBufferId);
            NativeArray<PdaTypewriterStateDTO> typewriter = ResolveVaultBuffer(in _typewriterStateHandle, TypewriterStateBufferId);
            if (!state.IsCreated || !mask.IsCreated || !typewriter.IsCreated)
                return false;

            if (state[0].Magic != StateMagic || mask[0].Magic != StateMagic)
            {
                ClearPdaRuntimeStateScalar(state, mask, typewriter);
                ref PdaEncyclopediaRuntimeStateDTO runtimeState = ref GetVaultElementRef(in _runtimeStateHandle, RuntimeStateBufferId, 0);
                runtimeState.Magic = StateMagic;
                runtimeState.StreamState = (uint)PdaEncyclopediaStreamState.Idle;
                ref EncyclopediaStateDTO encyclopedia = ref GetVaultElementRef(in _unlockMaskHandle, UnlockMaskBufferId, 0);
                encyclopedia.Magic = StateMagic;
                encyclopedia.StreamState = (uint)PdaEncyclopediaStreamState.Idle;
                ClearMetadataBuffer();
                ClearTelemetryBuffer();
            }

            _vaultReady = true;
            return true;
        }

        private static void ClearPdaRuntimeStateScalar(
            NativeArray<PdaEncyclopediaRuntimeStateDTO> runtimeState,
            NativeArray<EncyclopediaStateDTO> unlockMask,
            NativeArray<PdaTypewriterStateDTO> typewriterState)
        {
            runtimeState[0] = default;
            unlockMask[0] = default;
            typewriterState[0] = default;
        }

        private bool TryBindVaultCold()
        {
            IDataVault registryVault = GlobalRegistry.DataVault;
            if (!ReferenceEquals(_vault, registryVault))
                BindDataVaultForLifecycle(registryVault);

            return _vault != null;
        }

        private void TryBindPlayerContextCold()
        {
            if (_playerContext != null)
                return;

            _playerContext = GlobalRegistry.Player;
        }

        private void EnsureTextLeases()
        {
            if (!_bodyLease.IsValid)
                CharBufferPool.TryAcquireEncyclopedia(out _bodyLease);

            if (!_titleLease.IsValid)
                CharBufferPool.TryAcquire(out _titleLease);

            if (!_metaLease.IsValid)
                CharBufferPool.TryAcquire(out _metaLease);
        }

        private void EnsureH8lrLoreStore()
        {
            if (!openDefaultH8lrOnEnable || !EnsureVaultBuffers())
                return;

            if (_h8lrLoreStore != null && _h8lrLoreStore.IsOpen)
            {
                SeedH8lrMetadata();
                return;
            }

            if (_h8lrLoreStore == null)
                _h8lrLoreStore = new PdaH8lrLoreStore();

            bool opened = !string.IsNullOrEmpty(h8lrPathOverride)
                ? _h8lrLoreStore.Open(Path.GetFullPath(h8lrPathOverride), _vault, in _h8lrMirrorHandle)
                : _h8lrLoreStore.OpenDefault(_vault, in _h8lrMirrorHandle);

            if (opened)
                SeedH8lrMetadata();
        }

        private void EnsureBabelStore()
        {
            if (_babelStore != null)
            {
                _babelStore.BindDataVault(_vault);
                return;
            }

            _babelStore = new BabelDictionaryStore(_vault);
            _ownsBabelStore = true;

            if (!openDefaultBabelOnEnable)
                return;

            if (!string.IsNullOrEmpty(dictionaryPathOverride))
            {
                _babelStore.Open(Path.GetFullPath(dictionaryPathOverride));
                return;
            }

            _babelStore.OpenDefault();
        }

        private void SeedH8lrMetadata()
        {
            if (_h8lrMetadataSeeded || !EnsureVaultBuffers())
                return;

            PdaH8lrLoreStore store = _h8lrLoreStore;
            if (store == null || !store.IsOpen)
                return;

            int count = math.min(store.EntryCount, MaxMetadataEntries);
            int imported = 0;
            for (int i = 0; i < count; i++)
            {
                if (!store.TryGetRecord(i, out PdaH8lrRecordDTO record) || record.Hash == 0u)
                    continue;

                ushort bitIndex = EnsureBitIndex(record.Hash);
                ref PdaEncyclopediaEntryMetaDTO meta = ref GetVaultElementRef(in _metadataHandle, MetadataBufferId, bitIndex);
                if (meta.SourceId != H8lrSourceId)
                    imported++;

                meta.EntryHash = record.Hash;
                meta.BitIndex = bitIndex;
                meta.SourceId = H8lrSourceId;
                meta.TitleHash = record.Hash;
                meta.Flags = (ushort)(meta.Flags | 2u);
                meta.Revision++;
            }

            if (imported > 0)
            {
                ref PdaEncyclopediaRuntimeStateDTO state = ref GetVaultElementRef(in _runtimeStateHandle, RuntimeStateBufferId, 0);
                state.Revision++;
                ref EncyclopediaStateDTO encyclopedia = ref GetVaultElementRef(in _unlockMaskHandle, UnlockMaskBufferId, 0);
                encyclopedia.Revision = state.Revision;
                encyclopedia.MetadataCount = state.MetadataCount;
            }

            _h8lrMetadataSeeded = true;
        }

        private void SeedMockLoreDatabase()
        {
            if (_mockSeeded || !EnsureVaultBuffers())
                return;

            NativeArray<byte> bytes = ResolveVaultBuffer(in _mockUtf8Handle, MockUtf8BufferId);
            NativeArray<BabelIndexDTO> index = ResolveVaultBuffer(in _mockIndexHandle, MockIndexBufferId);
            if (!bytes.IsCreated || !index.IsCreated)
                return;

            for (int i = 0; i < index.Length; i++)
                index[i] = default;

            int offset = 0;
            for (int i = 0; i < MockEntryCapacity && i < index.Length; i++)
            {
                uint hash = ResolveMockHash(i);
                int start = offset;
                offset += WriteMockEntry(bytes, offset, hash, i);
                index[i] = new BabelIndexDTO
                {
                    StringHash = hash,
                    ByteOffset = (uint)start,
                    ByteLength = (uint)(offset - start),
                    _pad0 = 0u
                };

                PdaAup48 aup = default;
                uint sourceId = TryGetH8lrUtf8(hash, out _) ? H8lrSourceId : 0x5348494Eu;
                UnlockEntry(hash, in aup, sourceId, ResolvePdaFrame(), false);
            }

            ref PdaEncyclopediaRuntimeStateDTO state = ref GetVaultElementRef(in _runtimeStateHandle, RuntimeStateBufferId, 0);
            state.MockEntryCount = (uint)math.min(MockEntryCapacity, index.Length);
            ref EncyclopediaStateDTO encyclopedia = ref GetVaultElementRef(in _unlockMaskHandle, UnlockMaskBufferId, 0);
            encyclopedia.MockEntryCount = state.MockEntryCount;
            _mockSeeded = true;
        }

        private int WriteMockEntry(NativeArray<byte> bytes, int offset, uint hash, int ordinal)
        {
            int cursor = offset;
            WriteAscii(bytes, ref cursor, "HECTON-8 PDA ENCYCLOPEDIA FALLBACK\n".AsSpan());
            WriteAscii(bytes, ref cursor, "ENTRY ".AsSpan());
            WriteHexAscii(bytes, ref cursor, hash);
            WriteAscii(bytes, ref cursor, "\n\nBaked lore dictionary not present. This Vault-backed mock entry proves the streaming lane without JSON or managed string deserialization.\n\n".AsSpan());
            WriteAscii(bytes, ref cursor, "DISCOVERY GRID ^DISCOVERY_GRID^ // DIST ^DISCOVERY_DISTANCE^ // ENTRY ^ENTRY_HASH^ // QUALITY ^QUALITY^ // DEPTH ^DEPTH^\n\n".AsSpan());
            WriteAscii(bytes, ref cursor, "The PDA decodes UTF-8 bytes directly into a pooled TMP page. Typewriter reveal is a presentation lie; the lore payload stays byte-addressed and rollback-safe.\n\n".AsSpan());
            WriteAscii(bytes, ref cursor, "MOCK ORDINAL ".AsSpan());
            WriteDecimalAscii(bytes, ref cursor, ordinal);
            WriteAscii(bytes, ref cursor, "\n".AsSpan());
            return cursor - offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ResolveMockHash(int index)
        {
            return DefaultEntryHash + (uint)math.clamp(index, 0, MockEntryCapacity - 1);
        }

        private bool TryGetMockUtf8(uint hash, out ReadOnlySpan<byte> utf8)
        {
            utf8 = default;
            if (!EnsureVaultBuffers() || !_mockSeeded)
                return false;

            NativeArray<BabelIndexDTO> index = ResolveVaultBuffer(in _mockIndexHandle, MockIndexBufferId);
            NativeArray<byte> bytes = ResolveVaultBuffer(in _mockUtf8Handle, MockUtf8BufferId);
            if (!index.IsCreated || !bytes.IsCreated)
                return false;

            BabelLookupResultDTO row = ExtractMockLoreSpanScalar(
                index,
                hash,
                DefaultEntryHash,
                (uint)math.min(MockEntryCapacity, index.Length));
            if (row.Flags != 0u ||
                row.ByteLength == 0u ||
                (long)row.ByteOffset > (long)bytes.Length - row.ByteLength)
            {
                return false;
            }

            byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(bytes);
            utf8 = MemoryMarshal.CreateReadOnlySpan(
                ref UnsafeUtility.AsRef<byte>(ptr + row.ByteOffset),
                (int)row.ByteLength);
            return true;
        }

        private static BabelLookupResultDTO ExtractMockLoreSpanScalar(
            NativeArray<BabelIndexDTO> index,
            uint entryHash,
            uint mockBaseHash,
            uint mockEntryCount)
        {
            BabelLookupResultDTO result = default;
            result.TextHash = entryHash;
            result.Flags = 1u;

            if (entryHash >= mockBaseHash)
            {
                uint ordinal = entryHash - mockBaseHash;
                if (ordinal < mockEntryCount && ordinal < (uint)index.Length)
                {
                    BabelIndexDTO row = index[(int)ordinal];
                    if (row.StringHash == entryHash)
                    {
                        result.ByteOffset = row.ByteOffset;
                        result.ByteLength = row.ByteLength;
                        result.Flags = 0u;
                    }
                }
            }

            return result;
        }

#if UNITY_EDITOR
        private bool ParseCsvMetadata(NativeArray<byte> scratch, int byteLength)
        {
            if (!scratch.IsCreated || byteLength <= 0 || !EnsureVaultBuffers())
                return false;

            byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(scratch);
            ReadOnlySpan<byte> bytes = MemoryMarshal.CreateReadOnlySpan(
                ref UnsafeUtility.AsRef<byte>(ptr),
                math.min(byteLength, scratch.Length));
            int lineStart = 0;
            int imported = 0;
            for (int i = 0; i <= bytes.Length; i++)
            {
                if (i < bytes.Length && bytes[i] != (byte)'\n')
                    continue;

                ReadOnlySpan<byte> line = bytes.Slice(lineStart, i - lineStart);
                if (TryParseCsvLine(line, out uint hash, out ushort bitIndex))
                {
                    ref PdaEncyclopediaEntryMetaDTO meta = ref GetVaultElementRef(in _metadataHandle, MetadataBufferId, bitIndex);
                    if (meta.EntryHash == 0u)
                    {
                        ref PdaEncyclopediaRuntimeStateDTO state = ref GetVaultElementRef(in _runtimeStateHandle, RuntimeStateBufferId, 0);
                        state.MetadataCount = math.min((uint)UnlockBitCount, state.MetadataCount + 1u);
                        ref EncyclopediaStateDTO encyclopedia = ref GetVaultElementRef(in _unlockMaskHandle, UnlockMaskBufferId, 0);
                        encyclopedia.MetadataCount = state.MetadataCount;
                    }

                    meta.EntryHash = hash;
                    meta.BitIndex = bitIndex;
                    meta.TitleHash = hash;
                    imported++;
                }

                lineStart = i + 1;
            }

            if (imported > 0)
            {
                ref PdaEncyclopediaRuntimeStateDTO state = ref GetVaultElementRef(in _runtimeStateHandle, RuntimeStateBufferId, 0);
                state.Revision++;
                ref EncyclopediaStateDTO encyclopedia = ref GetVaultElementRef(in _unlockMaskHandle, UnlockMaskBufferId, 0);
                encyclopedia.Revision = state.Revision;
            }

            return imported > 0;
        }

        private bool TryParseCsvLine(ReadOnlySpan<byte> line, out uint hash, out ushort bitIndex)
        {
            hash = 0u;
            bitIndex = 0;
            line = TrimAscii(line);
            if (line.Length == 0 || line[0] == (byte)'#')
                return false;

            int firstSeparator = IndexOfSeparator(line);
            ReadOnlySpan<byte> first = firstSeparator >= 0 ? TrimAscii(line.Slice(0, firstSeparator)) : line;
            ReadOnlySpan<byte> rest = firstSeparator >= 0 ? line.Slice(firstSeparator + 1) : ReadOnlySpan<byte>.Empty;
            int secondSeparator = IndexOfSeparator(rest);
            ReadOnlySpan<byte> second = secondSeparator >= 0 ? TrimAscii(rest.Slice(0, secondSeparator)) : TrimAscii(rest);
            ReadOnlySpan<byte> third = secondSeparator >= 0 ? TrimAscii(rest.Slice(secondSeparator + 1)) : ReadOnlySpan<byte>.Empty;

            bool firstNumeric = TryParseUIntAscii(first, out uint firstNumber);
            bool secondNumeric = TryParseUIntAscii(second, out uint secondNumber);
            bool thirdNumeric = TryParseUIntAscii(third, out uint thirdNumber);

            if (third.Length > 0)
                hash = secondNumeric ? secondNumber : firstNumeric ? firstNumber : ComputeAsciiHash(first);
            else
                hash = firstNumeric ? firstNumber : ComputeAsciiHash(first);

            if (hash == 0u)
                return false;

            uint parsedBit = thirdNumeric
                ? thirdNumber
                : secondNumeric && third.Length == 0
                    ? secondNumber
                    : hash & (UnlockBitCount - 1);

            bitIndex = (ushort)(parsedBit & (UnlockBitCount - 1));
            return true;
        }
#endif

        private void ClearMetadataBuffer()
        {
            if (!IsPdaHandleCreated(in _metadataHandle, MetadataBufferId))
                return;

            for (int i = 0; i < MaxMetadataEntries; i++)
                GetVaultElementRef(in _metadataHandle, MetadataBufferId, i) = default;
        }

        private void ClearTelemetryBuffer()
        {
            if (!IsPdaHandleCreated(in _telemetryHandle, TelemetryBufferId))
                return;

            for (int i = 0; i < TelemetryFrameCount; i++)
                GetVaultElementRef(in _telemetryHandle, TelemetryBufferId, i) = default;
        }

        private void RefreshVisibility()
        {
            if (playerPDA == null)
            {
                _isPdaVisible = PlayerPDA.IsOpen;
                return;
            }

            _isPdaVisible = PlayerPDA.IsOpen && playerPDA.ActiveTab == encyclopediaTabIndex;
        }

        private void EnsureCanvasSplit()
        {
            _canvasSplitReady = dynamicTextCanvas != null &&
                                staticShellCanvas != null &&
                                !ReferenceEquals(dynamicTextCanvas, staticShellCanvas);

            if (dynamicTextCanvas != null && staticShellCanvas != null && !ReferenceEquals(dynamicTextCanvas, staticShellCanvas))
            {
                dynamicTextCanvas.overrideSorting = true;
                dynamicTextCanvas.sortingOrder = math.max(dynamicTextCanvas.sortingOrder, staticShellCanvas.sortingOrder + 1);
            }
        }

        private void TryRegisterDispatcherLanes()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void UnregisterDispatcherLanes()
        {
            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
                _registeredLateFrame = false;
            }
        }

        private void TryRegisterPdaEvents()
        {
            if (_registeredPdaEvents || !Application.isPlaying)
                return;

            PDAEvents.Register(this);
            _registeredPdaEvents = PDAEvents.IsRegistered(this);
        }

        private void UnregisterPdaEvents()
        {
            if (!_registeredPdaEvents)
                return;

            PDAEvents.Unregister(this);
            _registeredPdaEvents = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private bool TryReadLastDiscoveryAup(out PdaAup48 aup)
        {
            aup = default;
            if (!EnsureVaultBuffers())
                return false;

            ref PdaEncyclopediaRuntimeStateDTO state = ref GetVaultElementRef(in _runtimeStateHandle, RuntimeStateBufferId, 0);
            aup.GridX = state.LastDiscoveryGridX;
            aup.GridY = state.LastDiscoveryGridY;
            aup.GridZ = state.LastDiscoveryGridZ;
            aup.LocalX = state.LastDiscoveryLocalX;
            aup.LocalY = state.LastDiscoveryLocalY;
            aup.LocalZ = state.LastDiscoveryLocalZ;
            return (state.Flags & StateFlagPreciseAup) != 0u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static PdaAup48 CaptureSignalAup(in ScanCompleteSignal signal)
        {
            return new PdaAup48
            {
                GridX = signal.PositionAup.GridX,
                GridY = signal.PositionAup.GridY,
                GridZ = signal.PositionAup.GridZ,
                LocalX = signal.PositionAup.LocalX,
                LocalY = signal.PositionAup.LocalY,
                LocalZ = signal.PositionAup.LocalZ
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static PdaAup48 CaptureSnapshotAup(in PlayerRuntimePoseSnapshot snapshot)
        {
            return new PdaAup48
            {
                GridX = snapshot.Aup.GridX,
                GridY = snapshot.Aup.GridY,
                GridZ = snapshot.Aup.GridZ,
                LocalX = snapshot.Aup.LocalX,
                LocalY = snapshot.Aup.LocalY,
                LocalZ = snapshot.Aup.LocalZ
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double3 ResolveAupDeltaMetersClamped(in PdaAup48 target, in PdaAup48 origin)
        {
            return new double3(
                ResolveAupAxisDeltaMetersClamped(target.GridX, origin.GridX, target.LocalX, origin.LocalX),
                ResolveAupAxisDeltaMetersClamped(target.GridY, origin.GridY, target.LocalY, origin.LocalY),
                ResolveAupAxisDeltaMetersClamped(target.GridZ, origin.GridZ, target.LocalZ, origin.LocalZ));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double ResolveAupAxisDeltaMetersClamped(long targetGrid, long originGrid, float targetLocal, float originLocal)
        {
            long gridDelta = targetGrid - originGrid;
            if (gridDelta > AupDeltaClampCells)
                gridDelta = AupDeltaClampCells;
            else if (gridDelta < -AupDeltaClampCells)
                gridDelta = -AupDeltaClampCells;

            double meters = ((double)gridDelta * AupCellSizeMeters) + ((double)targetLocal - originLocal);
            return math.isfinite(meters) ? meters : 0d;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint ComposeStateFlags(uint existingFlags)
        {
            uint flags = existingFlags & ~StateFlagSourceMask;
            flags |= (_activeUtf8SourceFlags & 3u) << StateFlagSourceShift;
            if (_canvasSplitReady)
                flags |= StateFlagCanvasSplit;
            else
                flags &= ~StateFlagCanvasSplit;

            return flags;
        }

        private void SetFault(uint faultHash)
        {
            _lastFaultHash = faultHash;
            _streamState = PdaEncyclopediaStreamState.Fault;
            if (EnsureVaultBuffers())
            {
                ref PdaEncyclopediaRuntimeStateDTO state = ref GetVaultElementRef(in _runtimeStateHandle, RuntimeStateBufferId, 0);
                state.FaultHash = faultHash;
                state.StreamState = (uint)PdaEncyclopediaStreamState.Fault;
            }
        }

        private void DumpBlackBox()
        {
            if (!EnsureVaultBuffers())
                return;

            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string directory = Path.Combine(root, "Docs", "AgentLogs");
            try
            {
                Directory.CreateDirectory(directory);
                WriteBlackBoxDump(Path.Combine(directory, "Dump_1309_PDAEncyclopedia.bin"));
                WriteBlackBoxDump(Path.Combine(directory, "Dump_PDA_STREAMER.bin"));
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
            catch (ArgumentException)
            {
            }
            catch (NotSupportedException)
            {
            }
        }

        private void WriteBlackBoxDump(string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))
            {
                Span<byte> header = stackalloc byte[32];
                WriteUIntLittleEndian(header.Slice(0, 4), StateMagic);
                WriteUIntLittleEndian(header.Slice(4, 4), ResolvePdaFrame());
                WriteUIntLittleEndian(header.Slice(8, 4), _lastFaultHash);
                WriteUIntLittleEndian(header.Slice(12, 4), (uint)TelemetryFrameCount);
                WriteUIntLittleEndian(header.Slice(16, 4), (uint)UnsafeUtility.SizeOf<PdaEncyclopediaTelemetryEntry>());
                WriteUIntLittleEndian(header.Slice(20, 4), _activeEntryHash);
                stream.Write(header);

                Span<byte> row = stackalloc byte[64];
                for (int i = 0; i < TelemetryFrameCount; i++)
                {
                    if (!TryReadTelemetryDumpEntry(i, out PdaEncyclopediaTelemetryEntry entry))
                        entry = default;

                    WriteTelemetryDumpEntry(row, in entry);
                    stream.Write(row);
                }
            }
        }

        private bool TryReadTelemetryDumpEntry(int index, out PdaEncyclopediaTelemetryEntry entry)
        {
            entry = default;
            if (_vault == null ||
                _vault.IsCompactionFenceActive ||
                index < 0 ||
                index >= TelemetryFrameCount ||
                !TryResolveVaultBuffer(in _telemetryHandle, TelemetryBufferId, out NativeArray<PdaEncyclopediaTelemetryEntry> telemetry) ||
                _vault.IsCompactionFenceActive ||
                !telemetry.IsCreated ||
                index >= telemetry.Length)
            {
                return false;
            }

            entry = telemetry[index];
            return !_vault.IsCompactionFenceActive;
        }

        private static void WriteTelemetryDumpEntry(Span<byte> destination, in PdaEncyclopediaTelemetryEntry entry)
        {
            WriteUIntLittleEndian(destination.Slice(0, 4), entry.Frame);
            WriteUIntLittleEndian(destination.Slice(4, 4), entry.StateHash);
            WriteUIntLittleEndian(destination.Slice(8, 4), entry.EntryHash);
            WriteUIntLittleEndian(destination.Slice(12, 4), entry.UnlockedCount);
            WriteUIntLittleEndian(destination.Slice(16, 4), entry.CharsRenderedThisFrame);
            WriteUIntLittleEndian(destination.Slice(20, 4), entry.VisibleChars);
            WriteUIntLittleEndian(destination.Slice(24, 4), entry.DecodedChars);
            WriteUIntLittleEndian(destination.Slice(28, 4), entry.SourceBytes);
            WriteInt64LittleEndian(destination.Slice(32, 8), entry.DecodeTicks);
            WriteInt64LittleEndian(destination.Slice(40, 8), entry.CanvasTicks);
            WriteUIntLittleEndian(destination.Slice(48, 4), entry.Flags);
            WriteUIntLittleEndian(destination.Slice(52, 4), entry.FaultHash);
            WriteUIntLittleEndian(destination.Slice(56, 4), entry.CursorByte);
            WriteUIntLittleEndian(destination.Slice(60, 4), entry.Capacity);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawDebugGizmo)
                return;

            float visible01 = _decodedLength > 0 ? math.saturate(_visibleLength / (float)_decodedLength) : 0f;
            Gizmos.color = Color.Lerp(Color.red, Color.cyan, visible01);
            Vector3 size = new Vector3(debugGizmoScale, debugGizmoScale * math.max(0.05f, visible01), debugGizmoScale);
            Gizmos.DrawWireCube(transform.position + Vector3.up * (size.y * 0.5f), size);
        }

        private static bool HasInvalidNumbers(float quality, long decodeTicks, long canvasTicks, int decodedThisFrame)
        {
            return !math.isfinite(quality) || decodeTicks < 0L || canvasTicks < 0L || decodedThisFrame < 0;
        }

        private static uint ComputeRuntimeStateHash(in PdaEncyclopediaRuntimeStateDTO state)
        {
            uint hash = 2166136261u;
            hash = Mix(hash, state.LastEntryHash);
            hash = Mix(hash, state.UnlockedCount);
            hash = Mix(hash, state.Revision);
            hash = Mix(hash, state.VisibleChars);
            hash = Mix(hash, state.DecodedChars);
            hash = Mix(hash, state.SourceBytes);
            hash = Mix(hash, state.Flags);
            hash = Mix(hash, state.FaultHash);
            return hash;
        }

        private static uint ComputeTelemetryHash(in PdaEncyclopediaTelemetryEntry entry)
        {
            uint hash = 2166136261u;
            hash = Mix(hash, entry.Frame);
            hash = Mix(hash, entry.EntryHash);
            hash = Mix(hash, entry.VisibleChars);
            hash = Mix(hash, entry.DecodedChars);
            hash = Mix(hash, entry.SourceBytes);
            hash = Mix(hash, entry.Flags);
            hash = Mix(hash, entry.FaultHash);
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Mix(uint hash, uint value)
        {
            hash ^= value;
            return hash * 16777619u;
        }

        private static ref ulong GetMaskWordRef(ref EncyclopediaStateDTO mask, int wordIndex)
        {
            switch (wordIndex)
            {
                case 0: return ref mask.Mask0;
                case 1: return ref mask.Mask1;
                case 2: return ref mask.Mask2;
                default: return ref mask.Mask3;
            }
        }

        private static bool AtomicOr(ref ulong word, ulong bit)
        {
            ref long signedWord = ref UnsafeUtility.As<ulong, long>(ref word);
            long signedBit = unchecked((long)bit);
            while (true)
            {
                long before = Volatile.Read(ref signedWord);
                long after = before | signedBit;
                if (before == after)
                    return false;

                if (Interlocked.CompareExchange(ref signedWord, after, before) == before)
                    return true;
            }
        }

        private static int IndexOfSeparator(ReadOnlySpan<byte> line)
        {
            for (int i = 0; i < line.Length; i++)
            {
                byte b = line[i];
                if (b == (byte)',' || b == (byte)';' || b == (byte)'\t')
                    return i;
            }

            return -1;
        }

        private static ReadOnlySpan<byte> TrimAscii(ReadOnlySpan<byte> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start <= end && value[start] <= (byte)' ')
                start++;
            while (end >= start && value[end] <= (byte)' ')
                end--;

            return start <= end ? value.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        private static bool TryParseUIntAscii(ReadOnlySpan<byte> value, out uint parsed)
        {
            parsed = 0u;
            value = TrimAscii(value);
            if (value.Length == 0)
                return false;

            int cursor = 0;
            bool hex = value.Length > 2 && value[0] == (byte)'0' && (value[1] == (byte)'x' || value[1] == (byte)'X');
            if (hex)
                cursor = 2;

            uint result = 0u;
            for (; cursor < value.Length; cursor++)
            {
                byte b = value[cursor];
                uint digit;
                if (b >= (byte)'0' && b <= (byte)'9')
                    digit = (uint)(b - (byte)'0');
                else if (hex && b >= (byte)'A' && b <= (byte)'F')
                    digit = (uint)(b - (byte)'A' + 10);
                else if (hex && b >= (byte)'a' && b <= (byte)'f')
                    digit = (uint)(b - (byte)'a' + 10);
                else
                    return false;

                result = hex ? (result << 4) | digit : (result * 10u) + digit;
            }

            parsed = result;
            return true;
        }

        private static uint ComputeAsciiHash(ReadOnlySpan<byte> bytes)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte b = bytes[i];
                if (b >= (byte)'a' && b <= (byte)'z')
                    b = (byte)(b - 32);
                hash ^= b;
                hash *= 16777619u;
            }

            return hash;
        }

        private static uint ComputeStaticAsciiHash(ReadOnlySpan<char> chars)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (c >= 'a' && c <= 'z')
                    c = (char)(c - 32);
                hash ^= (byte)c;
                hash *= 16777619u;
            }

            return hash;
        }

        private static bool AppendHex8(Span<char> destination, ref int cursor, uint value)
        {
            if (destination.Length - cursor < 10)
                return false;

            destination[cursor++] = '0';
            destination[cursor++] = 'x';
            for (int shift = 28; shift >= 0; shift -= 4)
            {
                uint nibble = (value >> shift) & 0xFu;
                destination[cursor++] = (char)(nibble < 10u ? '0' + nibble : 'A' + (nibble - 10u));
            }

            return true;
        }

        private static int ClampLongToInt(long value)
        {
            if (value > int.MaxValue)
                return int.MaxValue;

            if (value < int.MinValue)
                return int.MinValue;

            return (int)value;
        }

        private static bool AppendHexByte(Span<char> destination, ref int cursor, byte value)
        {
            if (destination.Length - cursor < 2)
                return false;

            uint high = (uint)(value >> 4);
            uint low = (uint)(value & 0x0F);
            destination[cursor++] = (char)(high < 10u ? '0' + high : 'A' + (high - 10u));
            destination[cursor++] = (char)(low < 10u ? '0' + low : 'A' + (low - 10u));
            return true;
        }

        private static void WriteAscii(NativeArray<byte> bytes, ref int cursor, ReadOnlySpan<char> text)
        {
            for (int i = 0; i < text.Length && cursor < bytes.Length; i++)
            {
                char c = text[i];
                bytes[cursor++] = c <= 0x7F ? (byte)c : (byte)'?';
            }
        }

        private static void WriteHexAscii(NativeArray<byte> bytes, ref int cursor, uint value)
        {
            if (cursor + 10 > bytes.Length)
                return;

            bytes[cursor++] = (byte)'0';
            bytes[cursor++] = (byte)'x';
            for (int shift = 28; shift >= 0; shift -= 4)
            {
                uint nibble = (value >> shift) & 0xFu;
                bytes[cursor++] = (byte)(nibble < 10u ? '0' + nibble : 'A' + (nibble - 10u));
            }
        }

        private static void WriteDecimalAscii(NativeArray<byte> bytes, ref int cursor, int value)
        {
            Span<char> tmp = stackalloc char[16];
            if (!value.TryFormat(tmp, out int written))
                return;

            for (int i = 0; i < written && cursor < bytes.Length; i++)
                bytes[cursor++] = (byte)tmp[i];
        }

        private static void WriteUIntLittleEndian(Span<byte> destination, uint value)
        {
            destination[0] = (byte)value;
            destination[1] = (byte)(value >> 8);
            destination[2] = (byte)(value >> 16);
            destination[3] = (byte)(value >> 24);
        }

        private static void WriteInt64LittleEndian(Span<byte> destination, long value)
        {
            ulong raw = unchecked((ulong)value);
            destination[0] = (byte)raw;
            destination[1] = (byte)(raw >> 8);
            destination[2] = (byte)(raw >> 16);
            destination[3] = (byte)(raw >> 24);
            destination[4] = (byte)(raw >> 32);
            destination[5] = (byte)(raw >> 40);
            destination[6] = (byte)(raw >> 48);
            destination[7] = (byte)(raw >> 56);
        }
    }
}
