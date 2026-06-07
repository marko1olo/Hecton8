using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Data;
using Hecton8.Core.Memory;
using Hecton8.Data;
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
        ISlowTickable,
        ILateFrameTickable,
        IPDAEventListener,
        ILocalizationLanguageChangedListener,
        IGlobalRegistryHotSwapListener
    {
        private static int s_x001PdaEncyclopediaStreamerSignalPushDropCount;
        private const int UnlockBitCount = 256;
        private const int UnlockWordCount = 4;
        private const int MaxMetadataEntries = UnlockBitCount;
        private const int TelemetryFrameCount = 300;
        private const int MockUtf8Bytes = 64 * 1024;
        private const int MockEntryCapacity = 8;
#if UNITY_EDITOR
        private const int CsvScratchBytes = 64 * 1024;
#endif
        private const int H8lrMirrorBytes = 8 * 1024 * 1024;
        private const int TitleBufferCapacity = 128;
        private const int MetaBufferCapacity = 256;
        private const long AupDeltaClampCells = 1000000L;
        private const double AupCellSizeMeters = HectonPhysicsContract.AupSectorSizeMetersInt;
        private const uint StateMagic = 0x50444145u;
        private const uint FaultMissingVault = 0x5641554Cu;
        private const uint FaultMissingText = 0x54455854u;
        private const uint FaultUtf8Invalid = 0x55544638u;
        private const uint FaultMetadataFull = 0x4D455441u;
        private const uint FaultMetadataCollision = 0x434F4C4Cu;
        private const uint FaultInvalidHash = 0x494E5648u;
        private const uint FaultH8lrOpenFailed = 0x48384C52u; // H8LR
        private const string BlackBoxDumpFileName = "Dump_PDAEncyclopediaStreamer_BlackBox.bin";
        private const string BlackBoxDumpRelativePath = "Docs/AgentLogs/" + BlackBoxDumpFileName;
        private const uint DefaultEntryHash = 0xAEC57EACu;
        private const uint H8lrSourceId = PdaH8lrLoreStore.MagicH8lr;
        private const uint StateFlagEditorBulkUnlock = 1u;
        private const uint StateFlagPreciseAup = 2u;
        private const uint StateFlagCanvasSplit = 4u;
        private const int StateFlagSourceShift = 8;
        private const uint StateFlagSourceMask = 7u << StateFlagSourceShift;
        private const ushort MetaFlagPreciseAup = 1;
        private const ushort MetaFlagH8lrSource = 2;
        private const ushort MetaFlagDataMonolithSource = 4;
        private const ushort MetaFlagEncryptedPrerequisite = 8;
        private const float LoreUnlockHapticLow01 = 0.12f;
        private const float LoreUnlockHapticHigh01 = 0.92f;
        private const float LoreUnlockHapticSeconds = 0.075f;
        private const uint TelemetryFlagCanvasSplit = 1u << 16;
        private const uint TextSourceH8lr = 1u;
        private const uint TextSourceBabel = 2u;
        private const uint TextSourceVaultMock = 3u;
        private const uint TextSourceDataMonolith = 4u;
        private const uint DataMonolithSourceId = H8DataLayoutConstants.BlobMagic;
        private const int DataMonolithSeedMinRecordsPerFrame = 16;
        private const int DataMonolithSeedMaxRecordsPerFrame = 96;
        private const BufferID UnlockMaskBufferId = (BufferID)70560;
        private const BufferID RuntimeStateBufferId = (BufferID)70561;
        private const BufferID MetadataBufferId = (BufferID)70562;
        private const BufferID TelemetryBufferId = (BufferID)70563;
        private const BufferID TelemetryCursorBufferId = (BufferID)70564;
        private const BufferID MockUtf8BufferId = (BufferID)70565;
        private const BufferID MockIndexBufferId = (BufferID)70566;
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
        [SerializeField] private bool openDataMonolithAppliedLoreOnEnable = true;
        [SerializeField] private bool followActiveLocalizationLanguage = true;
        [SerializeField] private uint dataMonolithLocaleHash = H8AppliedLoreRuntime.DefaultLocaleHash;
        [SerializeField] private bool openDefaultH8lrOnEnable = true;
        [SerializeField] private string h8lrPathOverride;
        [SerializeField] private bool openDefaultBabelOnEnable = true;
        [SerializeField] private string dictionaryPathOverride;
#if UNITY_EDITOR
        [SerializeField] private string metadataCsvRelativePath = "Docs/PDA/lore_metadata.csv";
#endif
        [SerializeField] private uint initialEntryHash = DefaultEntryHash;

        [Header("Accessibility")]
        [SerializeField] private bool consumeUiRescaleRequests = true;
        [SerializeField] private float minimumTextScale = 0.78f;
        [SerializeField] private float maximumTextScale = 1.35f;
        [SerializeField] private bool allowInstantRevealRequests = true;

        [Header("Diagnostics")]
        [SerializeField] private bool drawDebugGizmo;
        [SerializeField] private float debugGizmoScale = 0.14f;
#if UNITY_EDITOR
        [SerializeField] private bool allowMockLoreFallbackInEditor;
#endif

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
#if UNITY_EDITOR
        private byte[] _editorCsvScratch;
#endif
        private VaultGenerationHandle<PdaTypewriterStateDTO> _typewriterStateHandle;
        private VaultGenerationHandle<byte> _h8lrMirrorHandle;
        private IPlayerRuntimeContext _playerContext;
        private PdaH8lrLoreStore _h8lrLoreStore;
        private BabelDictionaryStore _babelStore;
        private bool _ownsBabelStore;
        private bool _registeredLateFrame;
        private bool _registeredSlowTick;
        private bool _registeredPdaEvents;
        private bool _registeredHotSwap;
        private bool _vaultReady;
        private bool _mockSeeded;
        private bool _h8lrMetadataSeeded;
        private bool _h8lrOpenAttempted;
        private bool _h8lrOpenFailed;
        private bool _dataMonolithMetadataSeeded;
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
        private H8AppliedLorePacketRecord _activeAppliedLoreRecord;
        private uint _activeAppliedLoreRecordHash;
        private uint _activeAppliedLoreLocaleHash;
        private uint _resolvedDataMonolithLocaleHash = H8AppliedLoreRuntime.DefaultLocaleHash;
        private uint _dataMonolithMetadataSeedLocaleHash = H8AppliedLoreRuntime.DefaultLocaleHash;
        private int _dataMonolithMetadataSeedCursor;
        private int _dataMonolithMetadataSeedStage;
        private uint _lastFaultHash;
        private uint _activeUtf8SourceFlags;
        private bool _hasActiveAppliedLoreRecord;
        private bool _blackBoxDumpQueued;
        private uint _queuedBlackBoxFaultHash;
        private bool _forceRevealDecodedTextNextVisualSync;
        private bool _titleBaseFontSizeCaptured;
        private bool _bodyBaseFontSizeCaptured;
        private bool _metaBaseFontSizeCaptured;
        private float _titleBaseFontSize;
        private float _bodyBaseFontSize;
        private float _metaBaseFontSize;
        private float _appliedTextScale = 1f;
        private uint _lastUiRescaleFrame;
        private uint _lastUiRescaleSourceHash;
        private uint _lastUiRescaleFontScaleBits;
        private ushort _lastUiRescaleReason;

        private void Awake()
        {
            if (bodyText == null)
                TryGetComponent(out bodyText);

            EnsureCanvasSplit();
        }

        private void OnEnable()
        {
            EnsureTextLeases();
            TryBindPlayerContextCold();
            RefreshAppliedLoreLocaleHash(LocRegistry.ActiveLanguage);
            TryColdBootstrap();
            SignalBus<ScanCompleteSignal>.EnsureInitialized();
            SignalBus<LoreFragmentScannedSignal>.EnsureInitialized();
            SignalBus<UIRescaleRequestSignal>.EnsureInitialized();
            SignalCorridorRuntime.EnsureHapticPulseSignalLaneInitialized();
            CapturePdaTextFontBaselinesCold();
            TryRegisterPdaEvents();
            LocalizationEvents.RegisterLanguageListener(this);
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
            LocalizationEvents.UnregisterLanguageListener(this);
            UnregisterPdaEvents();
            TryUnregisterHotSwapListener();
            FlushQueuedBlackBoxDump();
            _coldBootstrapAttempted = false;
            _vaultReady = false;
            _dataMonolithMetadataSeeded = false;
            _playerContext = null;
            ResetActiveSourceCache();
            _forceRevealDecodedTextNextVisualSync = false;

            if (_h8lrLoreStore != null)
            {
                _h8lrLoreStore.Dispose();
                _h8lrLoreStore = null;
                _h8lrMetadataSeeded = false;
            }
            _h8lrOpenAttempted = false;
            _h8lrOpenFailed = false;

            if (_ownsBabelStore && _babelStore != null)
            {
                _babelStore.Dispose();
                _babelStore = null;
                _ownsBabelStore = false;
            }

#if UNITY_EDITOR
            _editorCsvScratch = null;
#endif

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
                return;

            ConsumeScanSignals();
            if (!_registeredPdaEvents)
                RefreshVisibility();

            // Entry selection rebuilds title/body/meta TMP buffers. Keep Tick as a
            // signal drain only; VISUAL_SYNC consumes the pending selection.
        }

        public void LateFrameTick()
        {
            AdvanceEncyclopediaFrameState();
            ConsumeUiRescaleRequestsVisualSync();

            if (!_isPdaVisible || bodyText == null)
                return;

            if (!_bodyLease.IsValid)
            {
                SetFault(FaultMissingText);
                return;
            }

            if (!_vaultReady)
                return;

            if (!_dataMonolithMetadataSeeded && openDataMonolithAppliedLoreOnEnable)
                SeedDataMonolithAppliedLoreMetadata();

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
                WriteCorruptedBody(_activeEntryHash != 0u ? _activeEntryHash : DefaultEntryHash);
                SetFault(FaultMissingText);
                QueueBlackBoxDump();
                return;
            }

            _activeSourceBytes = source.Length;
            TryAdvanceTextWindowForLongEntry(source);
            float quality = ResolveGlobalQualityWeight01();
            long decodeStart = Stopwatch.GetTimestamp();
            int decodeBudget = ResolveDecodeBudget(quality);
            int decodedThisFrame = DecodeUtf8Budgeted(source, _bodyLease.Span, decodeBudget);
            long decodeTicks = Stopwatch.GetTimestamp() - decodeStart;

            if (_sourceByteCursor >= source.Length)
                _streamState = _visibleLength >= _decodedLength ? PdaEncyclopediaStreamState.Complete : PdaEncyclopediaStreamState.Streaming;
            else
                _streamState = PdaEncyclopediaStreamState.Streaming;

            uint charsRenderedThisFrame = StepVisibleCharacters(quality);
            charsRenderedThisFrame += ForceRevealDecodedTextIfRequested();
            long canvasStart = Stopwatch.GetTimestamp();
            SubmitBodyIfChanged();
            long canvasTicks = Stopwatch.GetTimestamp() - canvasStart;

            if (_sourceByteCursor >= source.Length && _visibleLength >= _decodedLength)
                _streamState = PdaEncyclopediaStreamState.Complete;

            bool hasRuntimeStateSnapshot = WriteRuntimeState(quality, decodeTicks, canvasTicks, out uint unlockedCountSnapshot);
            RecordTelemetry(charsRenderedThisFrame, decodeTicks, canvasTicks, unlockedCountSnapshot, hasRuntimeStateSnapshot);

            if (_lastFaultHash != 0u || HasInvalidNumbers(quality, decodeTicks, canvasTicks, decodedThisFrame))
                QueueBlackBoxDump();
        }

        public void SlowTick()
        {
            FlushQueuedBlackBoxDump();
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

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)
        {
            RefreshAppliedLoreLocaleHash((GameLanguage)payload.Language);
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

            return TryReadRuntimeAndMask(out runtimeState, out unlockMask);
        }

        public void EditorUnlockAll()
        {
            TryColdBootstrap();
            if (!EnsureVaultBuffers())
                return;

            if (!TryReadRuntimeAndMask(out PdaEncyclopediaRuntimeStateDTO state, out EncyclopediaStateDTO mask))
                return;

            for (int word = 0; word < UnlockWordCount; word++)
                GetMaskWordRef(ref mask, word) = ulong.MaxValue;

            state.UnlockedCount = UnlockBitCount;
            state.Revision++;
            state.Magic = StateMagic;
            state.Flags |= StateFlagEditorBulkUnlock;
            mask.UnlockedCount = UnlockBitCount;
            mask.Revision = state.Revision;
            mask.Magic = StateMagic;
            mask.Flags |= StateFlagEditorBulkUnlock;
            if (!TryWriteRuntimeState(in state))
                return;

            TryWriteUnlockMask(in mask);
            _needsEntryReload = true;
        }

        public void EditorLockAll()
        {
            TryColdBootstrap();
            if (!EnsureVaultBuffers())
                return;

            if (!TryReadRuntimeAndMask(out PdaEncyclopediaRuntimeStateDTO state, out EncyclopediaStateDTO mask))
                return;

            for (int word = 0; word < UnlockWordCount; word++)
                GetMaskWordRef(ref mask, word) = 0UL;

            state.UnlockedCount = 0u;
            state.Revision++;
            state.Magic = StateMagic;
            mask.UnlockedCount = 0u;
            mask.Revision = state.Revision;
            mask.Magic = StateMagic;
            if (!TryWriteRuntimeState(in state))
                return;

            TryWriteUnlockMask(in mask);
            _needsEntryReload = true;
        }

        public void EditorSelectEntry(uint hash)
        {
            if (hash == 0u)
                return;

            _pendingSelectHash = hash;
            _needsEntryReload = true;
        }

        public void RequestInstantReveal()
        {
            if (allowInstantRevealRequests)
                _forceRevealDecodedTextNextVisualSync = true;
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

            byte[] scratch = EnsureEditorCsvScratchCold();
            int totalRead = 0;
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan))
                {
                    while (totalRead < scratch.Length)
                    {
                        int readCapacity = scratch.Length - totalRead;
                        int read = stream.Read(scratch, totalRead, readCapacity);
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

            return ParseCsvMetadata(new ReadOnlySpan<byte>(scratch, 0, totalRead));
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
                if (CanUseMockLoreFallback())
                {
                    SeedMockLoreDatabase();
                    TryGetMockUtf8(hash, out source);
                }
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

                if (!TryResolveLorePayloadForUnlock(signal.EntryHash))
                {
                    RejectLoreHash(signal.EntryHash);
                    continue;
                }

                PdaAup48 signalAup = default;
                bool hasSignalAup = TryCaptureSignalAup(in signal, out signalAup);
                if (!hasSignalAup && TryReadLastDiscoveryAup(out PdaAup48 lastAup))
                    signalAup = lastAup;

                if (UnlockEntry(signal.EntryHash, in signalAup, signal.SourceId, ResolvePdaFrame(), hasSignalAup, validatePayload: false, wasNewUnlock: out bool scanUnlocked))
                {
                    if (scanUnlocked)
                        PublishLoreUnlockHaptic();

                    _pendingSelectHash = signal.EntryHash;
                }
            }

            ReadOnlySpan<LoreFragmentScannedSignal> loreSignals = SignalBus<LoreFragmentScannedSignal>.GetFrameSnapshot();
            for (int i = 0; i < loreSignals.Length; i++)
            {
                LoreFragmentScannedSignal signal = loreSignals[i];
                if (signal.Hash == 0u)
                    continue;

                if ((signal.Flags & LoreFragmentScannedSignal.FlagPairedScanComplete) != 0 &&
                    HasPairedScanComplete(scanSignals, in signal))
                    continue;

                PdaAup48 aup = default;
                bool hasSignalAup = false;
                if ((signal.Flags & LoreFragmentScannedSignal.FlagHasAup) != 0 &&
                    TryCaptureSignalAup(in signal, out PdaAup48 signalAup))
                {
                    aup = signalAup;
                    hasSignalAup = true;
                }
                else if (TryReadLastDiscoveryAup(out PdaAup48 lastAup))
                {
                    aup = lastAup;
                }

                if (!TryResolveLorePayloadForUnlock(signal.Hash))
                {
                    RejectLoreHash(signal.Hash);
                    continue;
                }

                if (UnlockEntry(signal.Hash, in aup, signal.SourceId, signal.Frame, hasSignalAup, validatePayload: false, wasNewUnlock: out bool loreUnlocked))
                {
                    if (loreUnlocked)
                        PublishLoreUnlockHaptic();

                    _pendingSelectHash = signal.Hash;
                }
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
            _forceRevealDecodedTextNextVisualSync = false;
            _needsEntryReload = false;
            ResetActiveSourceCache();
            ResetTypewriterState();

            if (!IsUnlocked(hash))
            {
                _streamState = PdaEncyclopediaStreamState.Locked;
                if (IsEncrypted(hash))
                    WriteEncryptedBody(hash);
                else
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
            return UnlockEntry(hash, in aup, sourceId, frame, hasPreciseAup, out _);
        }

        private bool UnlockEntry(
            uint hash,
            in PdaAup48 aup,
            uint sourceId,
            uint frame,
            bool hasPreciseAup,
            out bool wasNewUnlock)
        {
            return UnlockEntry(hash, in aup, sourceId, frame, hasPreciseAup, true, out wasNewUnlock);
        }

        private bool UnlockEntry(
            uint hash,
            in PdaAup48 aup,
            uint sourceId,
            uint frame,
            bool hasPreciseAup,
            bool validatePayload,
            out bool wasNewUnlock)
        {
            wasNewUnlock = false;
            if (!EnsureVaultBuffers() || hash == 0u)
                return false;

            if (hash == uint.MaxValue || (validatePayload && !TryResolveLorePayloadForUnlock(hash)))
            {
                RejectLoreHash(hash);
                return false;
            }

            NativeArray<EncyclopediaStateDTO>.ReadOnly masks;
            NativeArray<PdaEncyclopediaRuntimeStateDTO>.ReadOnly states;
            NativeArray<PdaEncyclopediaEntryMetaDTO>.ReadOnly metadata;
            bool hasMaskSnapshot = TryReadVaultBuffer(in _unlockMaskHandle, UnlockMaskBufferId, out masks);
            bool hasStateSnapshot = TryReadVaultBuffer(in _runtimeStateHandle, RuntimeStateBufferId, out states);
            bool hasMetadataSnapshot = TryReadVaultBuffer(in _metadataHandle, MetadataBufferId, out metadata);
            if (!hasMaskSnapshot ||
                !hasStateSnapshot ||
                !hasMetadataSnapshot ||
                masks.Length < 1 ||
                states.Length < 1 ||
                metadata.Length < MaxMetadataEntries)
            {
                return false;
            }

            EncyclopediaStateDTO maskSnapshot = masks[0];
            bool prerequisitesSatisfied = AreAppliedLorePrerequisitesSatisfiedSnapshot(hash, metadata, in maskSnapshot);
            if (!TryPlanUnlockEntry(
                    hash,
                    in aup,
                    sourceId,
                    frame,
                    hasPreciseAup,
                    prerequisitesSatisfied,
                    masks,
                    states,
                    metadata,
                    out EncyclopediaStateDTO plannedMask,
                    out PdaEncyclopediaRuntimeStateDTO plannedState,
                    out PdaEncyclopediaEntryMetaDTO plannedMeta,
                    out ushort bitIndex,
                    out wasNewUnlock))
            {
                return false;
            }

            if (!TryWriteMetadataEntry(hash, bitIndex, in plannedMeta, out bool metadataCollision))
            {
                if (metadataCollision)
                    SetFault(FaultMetadataCollision);
                return false;
            }

            if (!TryWriteRuntimeState(in plannedState) ||
                !TryWriteUnlockMask(in plannedMask))
            {
                return false;
            }

            if (wasNewUnlock)
                PromoteEncryptedDependents(frame);

            return true;
        }

        private bool TryPlanUnlockEntry(
            uint hash,
            in PdaAup48 aup,
            uint sourceId,
            uint frame,
            bool hasPreciseAup,
            bool routePrerequisitesSatisfied,
            NativeArray<EncyclopediaStateDTO>.ReadOnly masks,
            NativeArray<PdaEncyclopediaRuntimeStateDTO>.ReadOnly states,
            NativeArray<PdaEncyclopediaEntryMetaDTO>.ReadOnly metadata,
            out EncyclopediaStateDTO plannedMask,
            out PdaEncyclopediaRuntimeStateDTO plannedState,
            out PdaEncyclopediaEntryMetaDTO plannedMeta,
            out ushort bitIndex,
            out bool wasNewUnlock)
        {
            plannedMask = default;
            plannedState = default;
            plannedMeta = default;
            bitIndex = 0;
            wasNewUnlock = false;
            if (!TryFindOrReserveBitIndexSnapshot(hash, metadata, out bitIndex, out bool reservedNewMetadata))
            {
                SetFault(FaultMetadataFull);
                return false;
            }

            plannedMask = masks[0];
            plannedState = states[0];
            plannedMeta = metadata[bitIndex];
            if (reservedNewMetadata)
            {
                plannedMeta.EntryHash = hash;
                plannedMeta.BitIndex = bitIndex;
                plannedMeta.TitleHash = hash;
                plannedState.MetadataCount = math.min((uint)UnlockBitCount, plannedState.MetadataCount + 1u);
                plannedMask.MetadataCount = plannedState.MetadataCount;
            }

            int wordIndex = bitIndex >> 6;
            int bitInWord = bitIndex & 63;
            ulong bit = 1UL << bitInWord;
            ref ulong word = ref GetMaskWordRef(ref plannedMask, wordIndex);
            bool alreadyUnlocked = (word & bit) != 0UL;
            bool prerequisitesSatisfied = alreadyUnlocked || routePrerequisitesSatisfied;
            wasNewUnlock = prerequisitesSatisfied && !alreadyUnlocked;
            if (wasNewUnlock)
                word |= bit;

            plannedState.Magic = StateMagic;
            plannedState.LastEntryHash = hash;
            plannedState.LastFrame = frame;
            plannedState.LastSourceId = sourceId;
            plannedState.ActiveBitIndex = bitIndex;
            plannedState.GlobalQualityWeight = ResolveGlobalQualityWeight01();
            plannedMask.Magic = StateMagic;
            plannedMask.LastEntryHash = hash;
            plannedMask.LastFrame = frame;
            plannedMask.LastSourceId = sourceId;
            plannedMask.ActiveBitIndex = bitIndex;
            plannedMask.GlobalQualityWeight = plannedState.GlobalQualityWeight;
            plannedMask.StreamState = (uint)_streamState;
            if (wasNewUnlock)
            {
                plannedState.UnlockedCount = math.min((uint)UnlockBitCount, plannedState.UnlockedCount + 1u);
                plannedState.Revision++;
            }
            else if (!prerequisitesSatisfied)
            {
                plannedState.Revision++;
            }

            plannedMask.UnlockedCount = plannedState.UnlockedCount;
            plannedMask.Revision = plannedState.Revision;

            if (hasPreciseAup)
            {
                plannedState.LastDiscoveryGridX = aup.GridX;
                plannedState.LastDiscoveryGridY = aup.GridY;
                plannedState.LastDiscoveryGridZ = aup.GridZ;
                plannedState.LastDiscoveryLocalX = aup.LocalX;
                plannedState.LastDiscoveryLocalY = aup.LocalY;
                plannedState.LastDiscoveryLocalZ = aup.LocalZ;
                plannedState.Flags |= StateFlagPreciseAup;
                plannedMask.LastDiscoveryGridX = aup.GridX;
                plannedMask.LastDiscoveryGridY = aup.GridY;
                plannedMask.LastDiscoveryGridZ = aup.GridZ;
                plannedMask.LastDiscoveryLocalX = aup.LocalX;
                plannedMask.LastDiscoveryLocalY = aup.LocalY;
                plannedMask.LastDiscoveryLocalZ = aup.LocalZ;
                plannedMask.Flags |= StateFlagPreciseAup;
            }

            plannedMeta.EntryHash = hash;
            plannedMeta.BitIndex = bitIndex;
            if (plannedMeta.TitleHash == 0u)
                plannedMeta.TitleHash = hash;
            plannedMeta.SourceId = sourceId;
            plannedMeta.Revision = plannedState.Revision;
            plannedMeta.LastFrame = frame;
            if (hasPreciseAup)
            {
                plannedMeta.DiscoveryGridX = aup.GridX;
                plannedMeta.DiscoveryGridY = aup.GridY;
                plannedMeta.DiscoveryGridZ = aup.GridZ;
                plannedMeta.DiscoveryLocalX = aup.LocalX;
                plannedMeta.DiscoveryLocalY = aup.LocalY;
                plannedMeta.DiscoveryLocalZ = aup.LocalZ;
                plannedMeta.Flags |= MetaFlagPreciseAup;
            }

            plannedMeta.Flags = prerequisitesSatisfied
                ? (ushort)(plannedMeta.Flags & ~MetaFlagEncryptedPrerequisite)
                : (ushort)(plannedMeta.Flags | MetaFlagEncryptedPrerequisite);
            return true;
        }

        private static bool TryFindOrReserveBitIndexSnapshot(
            uint hash,
            NativeArray<PdaEncyclopediaEntryMetaDTO>.ReadOnly metadata,
            out ushort bitIndex,
            out bool reservedNewMetadata)
        {
            bitIndex = 0;
            reservedNewMetadata = false;
            if (metadata.Length <= 0 || hash == 0u)
                return false;

            int start = (int)(hash & (UnlockBitCount - 1));
            for (int probe = 0; probe < UnlockBitCount; probe++)
            {
                int index = (start + probe) & (UnlockBitCount - 1);
                PdaEncyclopediaEntryMetaDTO meta = metadata[index];
                if (meta.EntryHash != 0u && meta.EntryHash != hash)
                    continue;

                bitIndex = (ushort)index;
                reservedNewMetadata = meta.EntryHash == 0u;
                return true;
            }

            return false;
        }

        private static bool TryFindBitIndexSnapshot(
            NativeArray<PdaEncyclopediaEntryMetaDTO> metadata,
            uint hash,
            out ushort bitIndex)
        {
            bitIndex = 0;
            if (!metadata.IsCreated || hash == 0u)
                return false;

            int start = (int)(hash & (UnlockBitCount - 1));
            for (int probe = 0; probe < UnlockBitCount; probe++)
            {
                int index = (start + probe) & (UnlockBitCount - 1);
                PdaEncyclopediaEntryMetaDTO meta = metadata[index];
                if (meta.EntryHash == hash)
                {
                    bitIndex = (ushort)index;
                    return true;
                }

                if (meta.EntryHash == 0u)
                    break;
            }

            return false;
        }

        private static bool TryFindBitIndexSnapshot(
            NativeArray<PdaEncyclopediaEntryMetaDTO>.ReadOnly metadata,
            uint hash,
            out ushort bitIndex)
        {
            bitIndex = 0;
            if (metadata.Length <= 0 || hash == 0u)
                return false;

            int start = (int)(hash & (UnlockBitCount - 1));
            for (int probe = 0; probe < UnlockBitCount; probe++)
            {
                int index = (start + probe) & (UnlockBitCount - 1);
                PdaEncyclopediaEntryMetaDTO meta = metadata[index];
                if (meta.EntryHash == hash)
                {
                    bitIndex = (ushort)index;
                    return true;
                }

                if (meta.EntryHash == 0u)
                    break;
            }

            return false;
        }

        private void PromoteEncryptedDependents(uint frame)
        {
            if (!EnsureVaultBuffers())
                return;

            for (int pass = 0; pass <= H8DataLayoutConstants.AppliedLoreRoutePrerequisiteCapacity; pass++)
            {
                if (!TryReadVaultBuffer(in _unlockMaskHandle, UnlockMaskBufferId, out NativeArray<EncyclopediaStateDTO>.ReadOnly masks) ||
                    !TryReadVaultBuffer(in _runtimeStateHandle, RuntimeStateBufferId, out NativeArray<PdaEncyclopediaRuntimeStateDTO>.ReadOnly states) ||
                    !TryReadVaultBuffer(in _metadataHandle, MetadataBufferId, out NativeArray<PdaEncyclopediaEntryMetaDTO>.ReadOnly metadata) ||
                    masks.Length < 1 ||
                    states.Length < 1 ||
                    metadata.Length < MaxMetadataEntries)
                {
                    return;
                }

                bool changedThisPass = false;
                uint promotedThisPass = 0u;
                ulong clearMask0 = 0UL;
                ulong clearMask1 = 0UL;
                ulong clearMask2 = 0UL;
                ulong clearMask3 = 0UL;
                PdaEncyclopediaRuntimeStateDTO state = states[0];
                EncyclopediaStateDTO mask = masks[0];

                for (ushort bitIndex = 0; bitIndex < MaxMetadataEntries; bitIndex++)
                {
                    PdaEncyclopediaEntryMetaDTO meta = metadata[bitIndex];
                    uint dependentHash = meta.EntryHash;
                    if (dependentHash == 0u || (meta.Flags & MetaFlagEncryptedPrerequisite) == 0)
                        continue;

                    if (!AreAppliedLorePrerequisitesSatisfiedSnapshot(dependentHash, metadata, in mask))
                        continue;

                    int wordIndex = bitIndex >> 6;
                    ulong bit = 1UL << (bitIndex & 63);
                    OrMaskWord(ref clearMask0, ref clearMask1, ref clearMask2, ref clearMask3, wordIndex, bit);
                    ref ulong word = ref GetMaskWordRef(ref mask, wordIndex);
                    if ((word & bit) == 0UL)
                    {
                        word |= bit;
                        promotedThisPass++;
                        state.UnlockedCount = math.min((uint)UnlockBitCount, state.UnlockedCount + 1u);
                    }

                    changedThisPass = true;
                }

                if (!changedThisPass)
                    break;

                state.Revision++;
                state.LastFrame = frame;
                mask.UnlockedCount = state.UnlockedCount;
                mask.Revision = state.Revision;
                mask.LastFrame = frame;

                if (!TryWriteUnlockMask(in mask) ||
                    !TryWriteRuntimeState(in state) ||
                    !TryClearPromotedMetadataFlags(frame, clearMask0, clearMask1, clearMask2, clearMask3))
                {
                    return;
                }

                if (promotedThisPass == 0u)
                    break;
            }
        }

        private bool AreAppliedLorePrerequisitesSatisfiedSnapshot(
            uint hash,
            NativeArray<PdaEncyclopediaEntryMetaDTO>.ReadOnly metadata,
            in EncyclopediaStateDTO mask)
        {
            if (hash == 0u || !H8AppliedLoreRuntime.TryFindRouteForPacket(hash, out H8AppliedLoreRouteRecord route))
                return true;

            uint requiredCount = math.min(route.RequiredPacketCount, (uint)H8DataLayoutConstants.AppliedLoreRoutePrerequisiteCapacity);
            for (uint i = 0u; i < requiredCount; i++)
            {
                uint requiredHash = H8AppliedLoreRuntime.GetRouteRequiredPacketHash(in route, i);
                if (requiredHash == 0u || requiredHash == hash)
                    return false;

                if (!TryFindBitIndexSnapshot(metadata, requiredHash, out ushort bitIndex))
                    return false;

                ulong word = ReadMaskWord(in mask, bitIndex >> 6);
                ulong bit = 1UL << (bitIndex & 63);
                if ((word & bit) == 0UL)
                    return false;
            }

            return true;
        }

        private bool IsUnlocked(uint hash)
        {
            if (!EnsureVaultBuffers() ||
                !TryFindBitIndex(hash, out ushort bitIndex) ||
                !TryReadVaultBuffer(in _unlockMaskHandle, UnlockMaskBufferId, out NativeArray<EncyclopediaStateDTO>.ReadOnly masks) ||
                masks.Length < 1)
            {
                return false;
            }

            EncyclopediaStateDTO mask = masks[0];
            ulong word = ReadMaskWord(in mask, bitIndex >> 6);
            ulong bit = 1UL << (bitIndex & 63);
            return (word & bit) != 0UL;
        }

        private bool IsEncrypted(uint hash)
        {
            if (!EnsureVaultBuffers() ||
                !TryFindBitIndex(hash, out ushort bitIndex) ||
                !TryReadVaultBuffer(in _metadataHandle, MetadataBufferId, out NativeArray<PdaEncyclopediaEntryMetaDTO>.ReadOnly metadata) ||
                (uint)bitIndex >= (uint)metadata.Length)
            {
                return false;
            }

            PdaEncyclopediaEntryMetaDTO meta = metadata[bitIndex];
            return (meta.Flags & MetaFlagEncryptedPrerequisite) != 0;
        }

        private bool TryEnsureBitIndex(uint hash, out ushort bitIndex)
        {
            if (TryFindBitIndex(hash, out ushort existingBitIndex))
            {
                bitIndex = existingBitIndex;
                return true;
            }

            if (!_vaultReady || _vault == null || hash == 0u)
            {
                bitIndex = 0;
                return false;
            }

            if (!TryReadVaultBuffer(in _unlockMaskHandle, UnlockMaskBufferId, out NativeArray<EncyclopediaStateDTO>.ReadOnly masks) ||
                !TryReadVaultBuffer(in _runtimeStateHandle, RuntimeStateBufferId, out NativeArray<PdaEncyclopediaRuntimeStateDTO>.ReadOnly states) ||
                !TryReadVaultBuffer(in _metadataHandle, MetadataBufferId, out NativeArray<PdaEncyclopediaEntryMetaDTO>.ReadOnly metadata) ||
                masks.Length < 1 ||
                states.Length < 1 ||
                metadata.Length < MaxMetadataEntries ||
                !TryFindOrReserveBitIndexSnapshot(hash, metadata, out bitIndex, out bool reservedNewMetadata))
            {
                bitIndex = 0;
                return false;
            }

            if (!reservedNewMetadata)
                return true;

            PdaEncyclopediaEntryMetaDTO plannedMeta = metadata[bitIndex];
            plannedMeta.EntryHash = hash;
            plannedMeta.BitIndex = bitIndex;
            plannedMeta.TitleHash = hash;

            PdaEncyclopediaRuntimeStateDTO plannedState = states[0];
            plannedState.MetadataCount = math.min((uint)UnlockBitCount, plannedState.MetadataCount + 1u);

            EncyclopediaStateDTO plannedMask = masks[0];
            plannedMask.MetadataCount = plannedState.MetadataCount;

            if (!TryWriteMetadataEntry(hash, bitIndex, in plannedMeta, out bool metadataCollision))
            {
                if (metadataCollision)
                    SetFault(FaultMetadataCollision);
                return false;
            }

            return TryWriteRuntimeState(in plannedState) &&
                   TryWriteUnlockMask(in plannedMask);
        }

        private bool TryFindBitIndex(uint hash, out ushort bitIndex)
        {
            if (!_vaultReady ||
                _vault == null ||
                hash == 0u ||
                !TryReadVaultBuffer(in _metadataHandle, MetadataBufferId, out NativeArray<PdaEncyclopediaEntryMetaDTO>.ReadOnly metadata))
            {
                bitIndex = 0;
                return false;
            }

            return TryFindBitIndexSnapshot(metadata, hash, out bitIndex);
        }

        private bool TryResolveLorePayloadForUnlock(uint hash)
        {
            if (hash == 0u || hash == uint.MaxValue)
                return false;

            if (TryGetAppliedLoreUtf8(hash, H8AppliedLoreSurface.InGameWiki, out ReadOnlySpan<byte> dataMonolithUtf8) &&
                dataMonolithUtf8.Length > 0)
            {
                return true;
            }

            if (TryGetH8lrUtf8(hash, out ReadOnlySpan<byte> h8lrUtf8) && h8lrUtf8.Length > 0)
                return true;

            if (_babelStore != null && _babelStore.IsOpen)
            {
                ReadOnlySpan<byte> mappedUtf8 = _babelStore.FetchUtf8(hash);
                if (mappedUtf8.Length > 0 && !IsBabelErrorSentinel(mappedUtf8))
                    return true;
            }

            return CanUseMockLoreFallback() &&
                   TryGetMockUtf8(hash, out ReadOnlySpan<byte> mockUtf8) &&
                   mockUtf8.Length > 0;
        }

        private void RejectLoreHash(uint hash)
        {
            _lastFaultHash = hash == uint.MaxValue ? FaultInvalidHash : FaultMissingText;
            if (hash == uint.MaxValue)
                _streamState = PdaEncyclopediaStreamState.Fault;
            QueueBlackBoxDump();
        }

        private void PublishLoreUnlockHaptic()
        {
            HapticPulseSignal pulse = new HapticPulseSignal
            {
                LowFrequencyMotor01 = LoreUnlockHapticLow01,
                HighFrequencyMotor01 = LoreUnlockHapticHigh01,
                DurationSeconds = LoreUnlockHapticSeconds,
                PriorityFlags = HapticPulseSignal.PriorityTool
            };
            SignalBus<HapticPulseSignal>.TryPushTracked(in pulse, ref s_x001PdaEncyclopediaStreamerSignalPushDropCount);
        }

        private ReadOnlySpan<byte> SelectActiveUtf8Source()
        {
            if (TryGetAppliedLoreUtf8(_activeEntryHash, H8AppliedLoreSurface.InGameWiki, out ReadOnlySpan<byte> dataMonolithUtf8))
            {
                CacheActiveSource(dataMonolithUtf8, TextSourceDataMonolith);
                return dataMonolithUtf8;
            }

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

            if (CanUseMockLoreFallback() && TryGetMockUtf8(_activeEntryHash, out ReadOnlySpan<byte> mockUtf8))
            {
                CacheActiveSource(mockUtf8, TextSourceVaultMock);
                return mockUtf8;
            }

            return ReadOnlySpan<byte>.Empty;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool CanUseMockLoreFallback()
        {
            if (!_h8lrOpenFailed || !openDefaultH8lrOnEnable)
                return true;

#if UNITY_EDITOR
            return allowMockLoreFallbackInEditor;
#else
            return false;
#endif
        }

        private bool TryGetH8lrUtf8(uint hash, out ReadOnlySpan<byte> utf8)
        {
            utf8 = ReadOnlySpan<byte>.Empty;
            PdaH8lrLoreStore store = _h8lrLoreStore;
            return store != null && store.IsOpen && store.TryGetUtf8(hash, out utf8);
        }

        private bool TryGetAppliedLoreUtf8(uint hash, H8AppliedLoreSurface surface, out ReadOnlySpan<byte> utf8)
        {
            utf8 = ReadOnlySpan<byte>.Empty;
            if (!openDataMonolithAppliedLoreOnEnable || hash == 0u)
                return false;

            if (!TryGetAppliedLoreRecord(hash, out H8AppliedLorePacketRecord record))
                return false;

            return H8AppliedLoreRuntime.TryGetUtf8(in record, surface, out utf8);
        }

        private bool TryGetAppliedLoreRecord(uint hash, out H8AppliedLorePacketRecord record)
        {
            uint localeHash = ResolveActiveDataMonolithLocaleHash();
            if (_hasActiveAppliedLoreRecord &&
                _activeAppliedLoreRecordHash == hash &&
                _activeAppliedLoreLocaleHash == localeHash)
            {
                record = _activeAppliedLoreRecord;
                return record.PacketHash == hash && record.LocaleHash != 0u;
            }

            if (H8AppliedLoreRuntime.TryFindPacket(hash, localeHash, out record))
            {
                CacheAppliedLoreRecord(hash, localeHash, in record);
                return true;
            }

            record = default;
            return false;
        }

        private bool TryWriteAppliedLoreSurfaceUtf16(
            uint hash,
            H8AppliedLoreSurface surface,
            Span<char> destination,
            out int written)
        {
            written = 0;
            return openDataMonolithAppliedLoreOnEnable &&
                   hash != 0u &&
                   TryGetAppliedLoreRecord(hash, out H8AppliedLorePacketRecord record) &&
                   H8AppliedLoreRuntime.TryWriteSurfaceUtf16(in record, surface, destination, out written);
        }

        private void CacheAppliedLoreRecord(uint requestedHash, uint requestedLocaleHash, in H8AppliedLorePacketRecord record)
        {
            _activeAppliedLoreRecord = record;
            _activeAppliedLoreRecordHash = requestedHash;
            _activeAppliedLoreLocaleHash = requestedLocaleHash;
            _hasActiveAppliedLoreRecord = record.PacketHash == requestedHash && record.LocaleHash != 0u;
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
            _activeAppliedLoreRecord = default;
            _activeAppliedLoreRecordHash = 0u;
            _activeAppliedLoreLocaleHash = 0u;
            _hasActiveAppliedLoreRecord = false;
        }

        private void RefreshAppliedLoreLocaleHash(GameLanguage language)
        {
            uint localeHash = ResolveConfiguredDataMonolithLocaleHash(language);
            if (_resolvedDataMonolithLocaleHash == localeHash)
                return;

            _resolvedDataMonolithLocaleHash = localeHash;
            ResetDataMonolithMetadataSeed(localeHash);
            _needsEntryReload = true;
            ResetActiveSourceCache();
        }

        private uint ResolveActiveDataMonolithLocaleHash()
        {
            uint localeHash = _resolvedDataMonolithLocaleHash;
            return localeHash != 0u ? localeHash : H8AppliedLoreRuntime.DefaultLocaleHash;
        }

        private uint ResolveConfiguredDataMonolithLocaleHash(GameLanguage language)
        {
            if (!followActiveLocalizationLanguage)
                return dataMonolithLocaleHash != 0u ? dataMonolithLocaleHash : H8AppliedLoreRuntime.DefaultLocaleHash;

            return H8AppliedLoreRuntime.ResolveLocaleHash(language);
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

        private void TryAdvanceTextWindowForLongEntry(ReadOnlySpan<byte> source)
        {
            int sourceLength = source.Length;
            if (!_bodyLease.IsValid ||
                sourceLength <= 0 ||
                _sourceByteCursor >= sourceLength)
            {
                return;
            }

            int capacity = _bodyLease.Span.Length;
            if (capacity <= 0)
                return;

            bool pendingSurrogateNeedsFreshWindow =
                _visibleLength >= _decodedLength &&
                _decodedLength >= capacity - 1 &&
                HasPendingUtf8SurrogatePairAtCursor(source);

            if (!pendingSurrogateNeedsFreshWindow &&
                (_decodedLength < capacity ||
                 _visibleLength < _decodedLength))
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
                    if (IsUtf8Continuation(b1) && scalar >= 0x80)
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
                    if (IsUtf8Continuation(b1) &&
                        IsUtf8Continuation(b2) &&
                        scalar >= 0x800 &&
                        !IsUtf16SurrogateScalar(scalar))
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
                    if (IsUtf8Continuation(b1) &&
                        IsUtf8Continuation(b2) &&
                        IsUtf8Continuation(b3) &&
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

        private bool HasPendingUtf8SurrogatePairAtCursor(ReadOnlySpan<byte> source)
        {
            int sourceCursor = _sourceByteCursor;
            if (sourceCursor < 0 || sourceCursor + 3 >= source.Length)
                return false;

            byte b0 = source[sourceCursor];
            if ((b0 & 0xF8) != 0xF0)
                return false;

            byte b1 = source[sourceCursor + 1];
            byte b2 = source[sourceCursor + 2];
            byte b3 = source[sourceCursor + 3];
            if (!IsUtf8Continuation(b1) ||
                !IsUtf8Continuation(b2) ||
                !IsUtf8Continuation(b3))
            {
                return false;
            }

            int scalar = ((b0 & 0x07) << 18) | ((b1 & 0x3F) << 12) | ((b2 & 0x3F) << 6) | (b3 & 0x3F);
            return scalar >= 0x10000 && scalar <= 0x10FFFF;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsUtf8Continuation(byte value)
        {
            return (value & 0xC0) == 0x80;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsUtf16SurrogateScalar(int scalar)
        {
            return scalar >= 0xD800 && scalar <= 0xDFFF;
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

                if (!TryReadPreciseDiscoveryState(out PdaEncyclopediaRuntimeStateDTO state))
                    return false;

                int meters = (int)math.round(math.abs(state.LastDiscoveryLocalY));

                return ZeroGCFormatter.AppendInt(meters, destination, ref cursor) &&
                       ZeroGCFormatter.AppendChar('m', destination, ref cursor);
            }

            if (tokenHash == TokenEntryHashHash)
                return AppendHex8(destination, ref cursor, _activeEntryHash);

            if (tokenHash == TokenQualityHash)
            {
                int percent = (int)math.round(ResolveGlobalQualityWeight01() * 100f);
                return ZeroGCFormatter.AppendInt(percent, destination, ref cursor) &&
                       ZeroGCFormatter.AppendChar('%', destination, ref cursor);
            }

            if (tokenHash == TokenDiscoveryGridHash)
            {
                if (!TryReadPreciseDiscoveryState(out PdaEncyclopediaRuntimeStateDTO state))
                    return false;

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

            if (!TryReadPreciseDiscoveryState(out PdaEncyclopediaRuntimeStateDTO state))
                return false;

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

        private bool TryReadPreciseDiscoveryState(out PdaEncyclopediaRuntimeStateDTO state)
        {
            state = default;
            if (!EnsureVaultBuffers() || _activeEntryHash == 0u)
                return false;

            if (!TryFindBitIndex(_activeEntryHash, out ushort bitIndex))
                return false;

            if (!TryReadVaultBuffer(in _metadataHandle, MetadataBufferId, out NativeArray<PdaEncyclopediaEntryMetaDTO>.ReadOnly metadata) ||
                (uint)bitIndex >= (uint)metadata.Length)
            {
                return false;
            }

            PdaEncyclopediaEntryMetaDTO meta = metadata[bitIndex];
            if (meta.EntryHash != _activeEntryHash || (meta.Flags & MetaFlagPreciseAup) == 0)
                return false;

            state.LastEntryHash = meta.EntryHash;
            state.LastFrame = meta.LastFrame;
            state.LastSourceId = meta.SourceId;
            state.ActiveBitIndex = bitIndex;
            state.LastDiscoveryGridX = meta.DiscoveryGridX;
            state.LastDiscoveryGridY = meta.DiscoveryGridY;
            state.LastDiscoveryGridZ = meta.DiscoveryGridZ;
            state.LastDiscoveryLocalX = meta.DiscoveryLocalX;
            state.LastDiscoveryLocalY = meta.DiscoveryLocalY;
            state.LastDiscoveryLocalZ = meta.DiscoveryLocalZ;
            state.Flags = StateFlagPreciseAup;
            return true;
        }

        private uint StepVisibleCharacters(float quality)
        {
            if (_visibleLength >= _decodedLength)
                return 0u;

            if (!EnsureVaultBuffers())
                return StepVisibleCharactersScalar(quality);

            if (!TryReadVaultBuffer(in _typewriterStateHandle, TypewriterStateBufferId, out NativeArray<PdaTypewriterStateDTO>.ReadOnly typewriter) ||
                typewriter.Length < 1)
            {
                return StepVisibleCharactersScalar(quality);
            }

            PdaTypewriterStateDTO state = typewriter[0];
            StepTypewriterScalar(ref state, quality, SystemDispatcher.CurrentFrameDeltaTime, _decodedLength, _visibleLength, ResolvePdaFrame());
            if (!TryWriteTypewriterState(in state))
                return StepVisibleCharactersScalar(quality);

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

        private uint ForceRevealDecodedTextIfRequested()
        {
            if (!_forceRevealDecodedTextNextVisualSync)
                return 0u;

            _forceRevealDecodedTextNextVisualSync = false;
            int capacity = _bodyLease.IsValid ? _bodyLease.Buffer.Length : 0;
            int decoded = math.clamp(_decodedLength, 0, capacity);
            if (decoded <= _visibleLength)
                return 0u;

            int previousVisible = _visibleLength;
            _visibleLength = decoded;
            _charAccumulator = 0f;
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

            PdaTypewriterStateDTO state = default;
            TryWriteTypewriterState(in state);
        }

        private int ResolveDecodeBudget(float quality)
        {
            float q = math.saturate(quality);
            return (int)math.round(math.lerp(32f, 2048f, q));
        }

        private int ResolveDataMonolithSeedBudget(float quality)
        {
            float q = math.saturate(quality);
            return (int)math.round(math.lerp(DataMonolithSeedMinRecordsPerFrame, DataMonolithSeedMaxRecordsPerFrame, q));
        }

        private float ResolveGlobalQualityWeight01()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            if (math.isfinite(quality))
                return math.saturate(quality);

            return 0.5f;
        }

        private void ConsumeUiRescaleRequestsVisualSync()
        {
            if (!consumeUiRescaleRequests)
                return;

            ReadOnlySpan<UIRescaleRequestSignal> signals = SignalBus<UIRescaleRequestSignal>.GetFrameSnapshot();
            if (signals.Length == 0)
                return;

            float scale = _appliedTextScale;
            bool hasRequest = false;
            for (int i = 0; i < signals.Length; i++)
            {
                UIRescaleRequestSignal signal = signals[i];
                uint fontScaleBits = math.asuint(signal.FontScale);
                if (signal.Frame == _lastUiRescaleFrame &&
                    signal.SourceHash == _lastUiRescaleSourceHash &&
                    fontScaleBits == _lastUiRescaleFontScaleBits &&
                    signal.Reason == _lastUiRescaleReason)
                {
                    continue;
                }

                _lastUiRescaleFrame = signal.Frame;
                _lastUiRescaleSourceHash = signal.SourceHash;
                _lastUiRescaleFontScaleBits = fontScaleBits;
                _lastUiRescaleReason = signal.Reason;
                scale = ResolvePdaTextScale(signal.FontScale);
                hasRequest = true;
            }

            if (hasRequest)
                ApplyPdaTextScaleVisualSync(scale);
        }

        private float ResolvePdaTextScale(float requestedScale)
        {
            float scale = math.isfinite(requestedScale) && requestedScale > 0f ? requestedScale : 1f;
            float minScale = math.isfinite(minimumTextScale) && minimumTextScale > 0f ? minimumTextScale : 0.78f;
            float maxScale = math.isfinite(maximumTextScale) && maximumTextScale > 0f ? maximumTextScale : 1.35f;
            if (maxScale < minScale)
                maxScale = minScale;

            return math.clamp(scale, minScale, maxScale);
        }

        private void ApplyPdaTextScaleVisualSync(float scale)
        {
            CapturePdaTextFontBaselinesCold();
            if (math.abs(scale - _appliedTextScale) < 0.001f)
                return;

            _appliedTextScale = scale;
            ApplyFontScale(titleText, _titleBaseFontSizeCaptured, _titleBaseFontSize, scale);
            ApplyFontScale(bodyText, _bodyBaseFontSizeCaptured, _bodyBaseFontSize, scale);
            ApplyFontScale(metaText, _metaBaseFontSizeCaptured, _metaBaseFontSize, scale);
        }

        private void CapturePdaTextFontBaselinesCold()
        {
            if (!_titleBaseFontSizeCaptured && titleText != null)
            {
                _titleBaseFontSize = titleText.fontSize;
                _titleBaseFontSizeCaptured = math.isfinite(_titleBaseFontSize) && _titleBaseFontSize > 0f;
            }

            if (!_bodyBaseFontSizeCaptured && bodyText != null)
            {
                _bodyBaseFontSize = bodyText.fontSize;
                _bodyBaseFontSizeCaptured = math.isfinite(_bodyBaseFontSize) && _bodyBaseFontSize > 0f;
            }

            if (!_metaBaseFontSizeCaptured && metaText != null)
            {
                _metaBaseFontSize = metaText.fontSize;
                _metaBaseFontSizeCaptured = math.isfinite(_metaBaseFontSize) && _metaBaseFontSize > 0f;
            }
        }

        private static void ApplyFontScale(TMP_Text text, bool hasBaseline, float baseline, float scale)
        {
            if (text == null || !hasBaseline || !math.isfinite(baseline) || baseline <= 0f)
                return;

            text.fontSize = math.max(1f, baseline * scale);
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
            if (!ZeroGCFormatter.AppendToSpan("LOCKED // SCAN ENTRY ".AsSpan(), span, ref cursor) ||
                !AppendHex8(span, ref cursor, hash) ||
                !ZeroGCFormatter.AppendToSpan(" TO STREAM LORE.".AsSpan(), span, ref cursor))
            {
                SetFault(FaultMissingText);
                return;
            }

            _decodedLength = cursor;
            _visibleLength = cursor;
            SubmitBodyIfChanged();
        }

        private void WriteEncryptedBody(uint hash)
        {
            Span<char> span = _bodyLease.Span;
            int cursor = 0;
            if (!ZeroGCFormatter.AppendToSpan("ENCRYPTED // PRIOR EVIDENCE REQUIRED ".AsSpan(), span, ref cursor) ||
                !AppendHex8(span, ref cursor, hash))
            {
                SetFault(FaultMissingText);
                return;
            }

            _decodedLength = cursor;
            _visibleLength = cursor;
            SubmitBodyIfChanged();
        }

        private void WriteCorruptedBody(uint hash)
        {
            Span<char> span = _bodyLease.Span;
            int cursor = 0;
            if (!ZeroGCFormatter.AppendToSpan("[CORRUPTED DATA RECORD] ".AsSpan(), span, ref cursor) ||
                !AppendHex8(span, ref cursor, hash))
            {
                SetFault(FaultMissingText);
                return;
            }

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
            if (TryWriteAppliedLoreSurfaceUtf16(hash, H8AppliedLoreSurface.Title, span, out cursor))
            {
                titleText.SetCharArray(_titleLease.Buffer, 0, cursor);
                return;
            }

            if (!ZeroGCFormatter.AppendToSpan("ENCYCLOPEDIA // ".AsSpan(), span, ref cursor) ||
                !AppendHex8(span, ref cursor, hash))
            {
                SetFault(FaultMissingText);
                return;
            }

            titleText.SetCharArray(_titleLease.Buffer, 0, cursor);
        }

        private void WriteMeta(uint hash)
        {
            if (metaText == null || !_metaLease.IsValid || !EnsureVaultBuffers())
                return;

            Span<char> span = _metaLease.Buffer.AsSpan(0, math.min(MetaBufferCapacity, _metaLease.Buffer.Length));
            int cursor = 0;
            if (!TryReadVaultBuffer(in _runtimeStateHandle, RuntimeStateBufferId, out NativeArray<PdaEncyclopediaRuntimeStateDTO>.ReadOnly states) ||
                states.Length < 1)
            {
                return;
            }

            PdaEncyclopediaRuntimeStateDTO state = states[0];
            if (!ZeroGCFormatter.AppendToSpan("UNLOCKED ".AsSpan(), span, ref cursor) ||
                !ZeroGCFormatter.AppendInt((int)state.UnlockedCount, span, ref cursor) ||
                !ZeroGCFormatter.AppendToSpan("/256 | BIT ".AsSpan(), span, ref cursor))
            {
                FailMetaWrite();
                return;
            }

            if (TryFindBitIndex(hash, out ushort bitIndex))
            {
                if (!ZeroGCFormatter.AppendInt(bitIndex, span, ref cursor))
                {
                    FailMetaWrite();
                    return;
                }
            }
            else if (!ZeroGCFormatter.AppendChar('-', span, ref cursor))
            {
                FailMetaWrite();
                return;
            }

            if (!ZeroGCFormatter.AppendToSpan(" | GRID ".AsSpan(), span, ref cursor))
            {
                FailMetaWrite();
                return;
            }

            if (TryReadPreciseDiscoveryState(out PdaEncyclopediaRuntimeStateDTO discoveryState))
            {
                if (!ZeroGCFormatter.AppendInt(ClampLongToInt(discoveryState.LastDiscoveryGridX), span, ref cursor) ||
                    !ZeroGCFormatter.AppendChar('/', span, ref cursor) ||
                    !ZeroGCFormatter.AppendInt(ClampLongToInt(discoveryState.LastDiscoveryGridY), span, ref cursor) ||
                    !ZeroGCFormatter.AppendChar('/', span, ref cursor) ||
                    !ZeroGCFormatter.AppendInt(ClampLongToInt(discoveryState.LastDiscoveryGridZ), span, ref cursor))
                {
                    FailMetaWrite();
                    return;
                }
            }
            else if (!ZeroGCFormatter.AppendToSpan("-/-/-".AsSpan(), span, ref cursor))
            {
                FailMetaWrite();
                return;
            }

            int routeMetaCursor = cursor;
            if (!TryAppendAppliedLoreRouteMeta(hash, span, ref cursor))
                cursor = routeMetaCursor;

            metaText.SetCharArray(_metaLease.Buffer, 0, cursor);
        }

        private void FailMetaWrite()
        {
            SetFault(FaultMissingText);
            if (metaText != null && _metaLease.IsValid)
                metaText.SetCharArray(_metaLease.Buffer, 0, 0);
        }

        private static bool TryAppendAppliedLoreRouteMeta(uint hash, Span<char> destination, ref int cursor)
        {
            if (hash == 0u || !H8AppliedLoreRuntime.TryFindRouteForPacket(hash, out H8AppliedLoreRouteRecord route))
                return true;

            uint routePacketCount = math.min(route.PacketCount, (uint)H8DataLayoutConstants.AppliedLoreRoutePacketCapacity);
            if (routePacketCount == 0u)
                return true;

            if (!ZeroGCFormatter.AppendToSpan(" | ROUTE ".AsSpan(), destination, ref cursor))
                return false;

            if (H8AppliedLoreRuntime.TryResolveRoutePacketOrdinal(in route, hash, out uint ordinal))
            {
                if (!ZeroGCFormatter.AppendInt((int)ordinal, destination, ref cursor))
                    return false;
            }
            else if (!ZeroGCFormatter.AppendChar('-', destination, ref cursor))
            {
                return false;
            }

            if (!ZeroGCFormatter.AppendChar('/', destination, ref cursor) ||
                !ZeroGCFormatter.AppendInt((int)routePacketCount, destination, ref cursor))
            {
                return false;
            }

            uint requiredCount = math.min(route.RequiredPacketCount, (uint)H8DataLayoutConstants.AppliedLoreRoutePrerequisiteCapacity);
            if (requiredCount > 0u)
            {
                if (!ZeroGCFormatter.AppendToSpan(" REQ ".AsSpan(), destination, ref cursor) ||
                    !ZeroGCFormatter.AppendInt((int)requiredCount, destination, ref cursor))
                {
                    return false;
                }
            }

            if (route.DepthMaxMeters > route.DepthMinMeters)
            {
                if (!ZeroGCFormatter.AppendToSpan(" DPT ".AsSpan(), destination, ref cursor) ||
                    !ZeroGCFormatter.AppendInt((int)math.round(route.DepthMinMeters), destination, ref cursor) ||
                    !ZeroGCFormatter.AppendChar('-', destination, ref cursor) ||
                    !ZeroGCFormatter.AppendInt((int)math.round(route.DepthMaxMeters), destination, ref cursor) ||
                    !ZeroGCFormatter.AppendChar('m', destination, ref cursor))
                {
                    return false;
                }
            }

            return true;
        }

        private bool WriteRuntimeState(float quality, long decodeTicks, long canvasTicks, out uint unlockedCountSnapshot)
        {
            unlockedCountSnapshot = 0u;
            if (!EnsureVaultBuffers())
                return false;

            if (!TryReadVaultBuffer(in _runtimeStateHandle, RuntimeStateBufferId, out NativeArray<PdaEncyclopediaRuntimeStateDTO>.ReadOnly states) ||
                !TryReadVaultBuffer(in _unlockMaskHandle, UnlockMaskBufferId, out NativeArray<EncyclopediaStateDTO>.ReadOnly masks) ||
                states.Length < 1 ||
                masks.Length < 1)
            {
                return false;
            }

            PdaEncyclopediaRuntimeStateDTO state = states[0];
            unlockedCountSnapshot = state.UnlockedCount;
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

            EncyclopediaStateDTO encyclopedia = masks[0];
            encyclopedia.Magic = StateMagic;
            encyclopedia.LastEntryHash = _activeEntryHash;
            encyclopedia.GlobalQualityWeight = state.GlobalQualityWeight;
            encyclopedia.CursorByte = state.CursorByte;
            encyclopedia.DecodedChars = state.DecodedChars;
            encyclopedia.VisibleChars = state.VisibleChars;
            encyclopedia.StreamState = state.StreamState;
            encyclopedia.Flags = ComposeStateFlags(encyclopedia.Flags);

            if (!TryWriteRuntimeState(in state))
                return false;

            TryWriteUnlockMask(in encyclopedia);
            return true;
        }

        private void RecordTelemetry(
            uint charsRenderedThisFrame,
            long decodeTicks,
            long canvasTicks,
            uint unlockedCountSnapshot = 0u,
            bool hasUnlockedCountSnapshot = false)
        {
            if (!EnsureVaultBuffers() ||
                !TryReadVaultBuffer(in _telemetryCursorHandle, TelemetryCursorBufferId, out NativeArray<int>.ReadOnly cursorSnapshot) ||
                cursorSnapshot.Length < 1)
            {
                return;
            }

            if (!hasUnlockedCountSnapshot)
            {
                if (!TryReadVaultBuffer(in _runtimeStateHandle, RuntimeStateBufferId, out NativeArray<PdaEncyclopediaRuntimeStateDTO>.ReadOnly runtimeStateSnapshot) ||
                    runtimeStateSnapshot.Length < 1)
                {
                    return;
                }

                unlockedCountSnapshot = runtimeStateSnapshot[0].UnlockedCount;
            }

            int index = cursorSnapshot[0];
            if ((uint)index >= TelemetryFrameCount)
                index = 0;

            PdaEncyclopediaTelemetryEntry entry = default;
            entry.Frame = ResolvePdaFrame();
            entry.EntryHash = _activeEntryHash;
            entry.UnlockedCount = unlockedCountSnapshot;
            entry.CharsRenderedThisFrame = charsRenderedThisFrame;
            entry.VisibleChars = (uint)math.max(0, _visibleLength);
            entry.DecodedChars = (uint)math.max(0, _decodedLength);
            entry.SourceBytes = (uint)math.max(0, _activeSourceBytes);
            entry.DecodeTicks = decodeTicks;
            entry.CanvasTicks = canvasTicks;
            entry.Flags = ((uint)_streamState & 0xFFu) |
                          ((_activeUtf8SourceFlags & 7u) << StateFlagSourceShift) |
                          (_canvasSplitReady ? TelemetryFlagCanvasSplit : 0u);
            entry.FaultHash = _lastFaultHash;
            entry.CursorByte = (uint)math.max(0, _sourceByteCursor);
            entry.Capacity = CharBufferPool.EncyclopediaPageCapacity;
            entry.StateHash = ComputeTelemetryHash(in entry);

            int nextCursor = index + 1;
            if (nextCursor >= TelemetryFrameCount)
                nextCursor = 0;

            IDataVault vault = _vault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireWriteLock(in _telemetryHandle, VaultOwnerSystemId, out NativeArray<PdaEncyclopediaTelemetryEntry> telemetry))
            {
                return;
            }

            try
            {
                if (!telemetry.IsCreated ||
                    telemetry.Length < TelemetryFrameCount ||
                    (uint)index >= (uint)telemetry.Length)
                {
                    return;
                }

                telemetry[index] = entry;
            }
            finally
            {
                vault.ReleaseWriteLock(in _telemetryHandle, VaultOwnerSystemId);
            }

            TryWriteTelemetryCursor(vault, nextCursor);
        }

        private bool EnsureVaultBuffers()
        {
            return _vaultReady &&
                   _vault != null &&
                   ArePdaHandlesCreated();
        }

        private bool TryReadVaultBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            out NativeArray<T>.ReadOnly buffer)
            where T : unmanaged
        {
            buffer = default;
            if (_vault == null ||
                _vault.IsCompactionFenceActive ||
                !IsPdaHandleCreated(in handle, expectedBufferId) ||
                !_vault.TryReadOnlyHandle(in handle, out buffer) ||
                _vault.IsCompactionFenceActive ||
                buffer.Length <= 0)
            {
                return false;
            }

            return true;
        }

        private bool CanUsePdaVaultHandles()
        {
            return _vault != null && ArePdaHandlesCreated();
        }

        private bool TryReadRuntimeAndMask(
            out PdaEncyclopediaRuntimeStateDTO runtimeState,
            out EncyclopediaStateDTO unlockMask)
        {
            runtimeState = default;
            unlockMask = default;
            if (!TryReadVaultBuffer(in _runtimeStateHandle, RuntimeStateBufferId, out NativeArray<PdaEncyclopediaRuntimeStateDTO>.ReadOnly states) ||
                !TryReadVaultBuffer(in _unlockMaskHandle, UnlockMaskBufferId, out NativeArray<EncyclopediaStateDTO>.ReadOnly masks) ||
                states.Length < 1 ||
                masks.Length < 1)
            {
                return false;
            }

            runtimeState = states[0];
            unlockMask = masks[0];
            return true;
        }

        private bool TryCommitRuntimeAndMask(in PdaEncyclopediaRuntimeStateDTO runtimeState, in EncyclopediaStateDTO unlockMask)
        {
            return TryWriteRuntimeState(in runtimeState) && TryWriteUnlockMask(in unlockMask);
        }

        private bool TryCommitMetadataRevision(bool incrementRevision)
        {
            if (!TryReadRuntimeAndMask(out PdaEncyclopediaRuntimeStateDTO state, out EncyclopediaStateDTO encyclopedia))
                return false;

            state.Magic = StateMagic;
            encyclopedia.Magic = StateMagic;
            if (incrementRevision)
                state.Revision++;

            encyclopedia.Revision = state.Revision;
            encyclopedia.MetadataCount = state.MetadataCount;
            return TryCommitRuntimeAndMask(in state, in encyclopedia);
        }

        private bool TryWriteMetadataEntry(uint hash, ushort bitIndex, in PdaEncyclopediaEntryMetaDTO plannedMeta)
        {
            return TryWriteMetadataEntry(hash, bitIndex, in plannedMeta, out _);
        }

        private bool TryWriteMetadataEntry(
            uint hash,
            ushort bitIndex,
            in PdaEncyclopediaEntryMetaDTO plannedMeta,
            out bool collision)
        {
            collision = false;
            IDataVault vault = _vault;
            if (!CanUsePdaVaultHandles() ||
                vault == null ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireWriteLock(in _metadataHandle, VaultOwnerSystemId, out NativeArray<PdaEncyclopediaEntryMetaDTO> metadata))
            {
                return false;
            }

            try
            {
                if (!metadata.IsCreated || (uint)bitIndex >= (uint)metadata.Length)
                    return false;

                PdaEncyclopediaEntryMetaDTO current = metadata[bitIndex];
                if (current.EntryHash != 0u && current.EntryHash != hash)
                {
                    collision = true;
                    return false;
                }

                metadata[bitIndex] = plannedMeta;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _metadataHandle, VaultOwnerSystemId);
            }
        }

        private bool TryWriteRuntimeState(in PdaEncyclopediaRuntimeStateDTO plannedState)
        {
            IDataVault vault = _vault;
            if (!CanUsePdaVaultHandles() ||
                vault == null ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireWriteLock(in _runtimeStateHandle, VaultOwnerSystemId, out NativeArray<PdaEncyclopediaRuntimeStateDTO> states))
            {
                return false;
            }

            try
            {
                if (!states.IsCreated || states.Length < 1)
                    return false;

                states[0] = plannedState;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _runtimeStateHandle, VaultOwnerSystemId);
            }
        }

        private bool TryWriteTelemetryCursor(IDataVault vault, int nextCursor)
        {
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireWriteLock(in _telemetryCursorHandle, VaultOwnerSystemId, out NativeArray<int> cursorBuffer))
            {
                return false;
            }

            try
            {
                if (!cursorBuffer.IsCreated || cursorBuffer.Length < 1)
                    return false;

                cursorBuffer[0] = nextCursor;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _telemetryCursorHandle, VaultOwnerSystemId);
            }
        }

        private bool TryWriteUnlockMask(in EncyclopediaStateDTO plannedMask)
        {
            IDataVault vault = _vault;
            if (!CanUsePdaVaultHandles() ||
                vault == null ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireWriteLock(in _unlockMaskHandle, VaultOwnerSystemId, out NativeArray<EncyclopediaStateDTO> masks))
            {
                return false;
            }

            try
            {
                if (!masks.IsCreated || masks.Length < 1)
                    return false;

                masks[0] = plannedMask;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _unlockMaskHandle, VaultOwnerSystemId);
            }
        }

        private bool TryWriteTypewriterState(in PdaTypewriterStateDTO plannedState)
        {
            IDataVault vault = _vault;
            if (!CanUsePdaVaultHandles() ||
                vault == null ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireWriteLock(in _typewriterStateHandle, VaultOwnerSystemId, out NativeArray<PdaTypewriterStateDTO> states))
            {
                return false;
            }

            try
            {
                if (!states.IsCreated || states.Length < 1)
                    return false;

                states[0] = plannedState;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _typewriterStateHandle, VaultOwnerSystemId);
            }
        }

        private bool TryClearPromotedMetadataFlags(uint frame, ulong clearMask0, ulong clearMask1, ulong clearMask2, ulong clearMask3)
        {
            IDataVault vault = _vault;
            if (!CanUsePdaVaultHandles() ||
                vault == null ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireWriteLock(in _metadataHandle, VaultOwnerSystemId, out NativeArray<PdaEncyclopediaEntryMetaDTO> metadata))
            {
                return false;
            }

            try
            {
                if (!metadata.IsCreated || metadata.Length < MaxMetadataEntries)
                    return false;

                for (ushort bitIndex = 0; bitIndex < MaxMetadataEntries; bitIndex++)
                {
                    ulong bit = 1UL << (bitIndex & 63);
                    if ((ReadPromoteMaskWord(clearMask0, clearMask1, clearMask2, clearMask3, bitIndex >> 6) & bit) == 0UL)
                        continue;

                    PdaEncyclopediaEntryMetaDTO meta = metadata[bitIndex];
                    meta.Flags = (ushort)(meta.Flags & ~MetaFlagEncryptedPrerequisite);
                    meta.LastFrame = frame;
                    meta.Revision++;
                    metadata[bitIndex] = meta;
                }

                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _metadataHandle, VaultOwnerSystemId);
            }
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
                   IsPdaBufferResolvable(in _typewriterStateHandle, TypewriterStateBufferId, 1) &&
                   IsPdaBufferResolvable(in _h8lrMirrorHandle, H8lrMirrorBufferId, H8lrMirrorBytes);
        }

        private bool IsPdaBufferResolvable<T>(
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            int requiredLength)
            where T : unmanaged
        {
            return TryReadVaultBuffer(in handle, expectedBufferId, out NativeArray<T>.ReadOnly buffer) &&
                   buffer.Length >= requiredLength;
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
            _h8lrOpenAttempted = false;
            _h8lrOpenFailed = false;
            ResetDataMonolithMetadataSeed(ResolveActiveDataMonolithLocaleHash());
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
            SeedDataMonolithAppliedLoreMetadata();
            if (CanUseMockLoreFallback())
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

            if (!TryReadVaultBuffer(in _runtimeStateHandle, RuntimeStateBufferId, out NativeArray<PdaEncyclopediaRuntimeStateDTO>.ReadOnly state) ||
                !TryReadVaultBuffer(in _unlockMaskHandle, UnlockMaskBufferId, out NativeArray<EncyclopediaStateDTO>.ReadOnly mask) ||
                !TryReadVaultBuffer(in _typewriterStateHandle, TypewriterStateBufferId, out NativeArray<PdaTypewriterStateDTO>.ReadOnly typewriter))
            {
                return false;
            }

            if (state[0].Magic != StateMagic || mask[0].Magic != StateMagic)
            {
                PdaEncyclopediaRuntimeStateDTO runtimeState = default;
                runtimeState.Magic = StateMagic;
                runtimeState.StreamState = (uint)PdaEncyclopediaStreamState.Idle;
                EncyclopediaStateDTO encyclopedia = default;
                encyclopedia.Magic = StateMagic;
                encyclopedia.StreamState = (uint)PdaEncyclopediaStreamState.Idle;
                PdaTypewriterStateDTO typewriterState = default;
                if (!TryWriteRuntimeState(in runtimeState) ||
                    !TryWriteUnlockMask(in encyclopedia) ||
                    !TryWriteTypewriterState(in typewriterState))
                {
                    return false;
                }

                ClearMetadataBuffer();
                ClearTelemetryBuffer();
            }

            _vaultReady = true;
            return true;
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
                _h8lrOpenAttempted = true;
                _h8lrOpenFailed = false;
                SeedH8lrMetadata();
                return;
            }

            if (_h8lrOpenAttempted && _h8lrOpenFailed)
                return;

            if (_h8lrLoreStore == null)
                _h8lrLoreStore = new PdaH8lrLoreStore();

            bool opened = TryOpenConfiguredH8lrStore();

            _h8lrOpenAttempted = true;
            _h8lrOpenFailed = !opened;
            if (opened)
            {
                SeedH8lrMetadata();
                return;
            }

            _h8lrLoreStore.Dispose();
            _h8lrLoreStore = null;
            _h8lrMetadataSeeded = false;
            _lastFaultHash = FaultH8lrOpenFailed;
            QueueBlackBoxDump(FaultH8lrOpenFailed);
        }

        private bool TryOpenConfiguredH8lrStore()
        {
            if (_h8lrLoreStore == null)
                return false;

            if (string.IsNullOrEmpty(h8lrPathOverride))
                return _h8lrLoreStore.OpenDefault(_vault, in _h8lrMirrorHandle);

            return TryResolveConfiguredPath(h8lrPathOverride, out string resolvedPath) &&
                _h8lrLoreStore.Open(resolvedPath, _vault, in _h8lrMirrorHandle);
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
                if (TryResolveConfiguredPath(dictionaryPathOverride, out string resolvedPath))
                    _babelStore.Open(resolvedPath);

                return;
            }

            _babelStore.OpenDefault();
        }

        private static bool TryResolveConfiguredPath(string configuredPath, out string resolvedPath)
        {
            resolvedPath = null;
            if (string.IsNullOrEmpty(configuredPath))
                return false;

            try
            {
                resolvedPath = Path.GetFullPath(configuredPath);
                return !string.IsNullOrEmpty(resolvedPath);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private void SeedDataMonolithAppliedLoreMetadata()
        {
            if (_dataMonolithMetadataSeeded ||
                !openDataMonolithAppliedLoreOnEnable ||
                !EnsureVaultBuffers())
            {
                return;
            }

            ReadOnlySpan<H8AppliedLorePacketRecord> records = H8AppliedLoreRuntime.GetPacketRecords();
            if (records.Length <= 0)
            {
                _dataMonolithMetadataSeeded = true;
                return;
            }

            uint localeHash = ResolveActiveDataMonolithLocaleHash();
            if (_dataMonolithMetadataSeedLocaleHash != localeHash)
                ResetDataMonolithMetadataSeed(localeHash);

            int remainingBudget = ResolveDataMonolithSeedBudget(ResolveGlobalQualityWeight01());
            int metadataWrites = 0;
            while (remainingBudget > 0 && !_dataMonolithMetadataSeeded)
            {
                uint stageLocaleHash = _dataMonolithMetadataSeedStage == 0
                    ? localeHash
                    : H8AppliedLoreRuntime.DefaultLocaleHash;
                bool fillOnlyMissing = _dataMonolithMetadataSeedStage != 0;

                int scanned = SeedAppliedLoreMetadataForLocale(
                    records,
                    stageLocaleHash,
                    fillOnlyMissing,
                    ref _dataMonolithMetadataSeedCursor,
                    remainingBudget,
                    out int metadataWritesThisSlice);

                metadataWrites += metadataWritesThisSlice;
                remainingBudget -= scanned > 0 ? scanned : remainingBudget;
                if (_dataMonolithMetadataSeedCursor < records.Length)
                    break;

                if (_dataMonolithMetadataSeedStage == 0 &&
                    localeHash != H8AppliedLoreRuntime.DefaultLocaleHash)
                {
                    _dataMonolithMetadataSeedStage = 1;
                    _dataMonolithMetadataSeedCursor = 0;
                    continue;
                }

                _dataMonolithMetadataSeeded = true;
            }

            if (metadataWrites > 0)
                TryCommitMetadataRevision(true);
        }

        private int SeedAppliedLoreMetadataForLocale(
            ReadOnlySpan<H8AppliedLorePacketRecord> records,
            uint localeHash,
            bool fillOnlyMissingDataMonolithRows,
            ref int cursor,
            int recordBudget,
            out int metadataWrites)
        {
            metadataWrites = 0;
            if (recordBudget <= 0)
                return 0;

            int scanned = 0;
            int start = math.clamp(cursor, 0, records.Length);
            for (int i = start; i < records.Length && scanned < recordBudget && metadataWrites < MaxMetadataEntries; i++)
            {
                scanned++;
                cursor = i + 1;
                H8AppliedLorePacketRecord record = records[i];
                if (record.PacketHash == 0u || record.LocaleHash != localeHash)
                    continue;

                if (!TryEnsureBitIndex(record.PacketHash, out ushort bitIndex))
                {
                    SetFault(FaultMetadataFull);
                    break;
                }

                if (!TryReadVaultBuffer(in _metadataHandle, MetadataBufferId, out NativeArray<PdaEncyclopediaEntryMetaDTO>.ReadOnly metadata) ||
                    (uint)bitIndex >= (uint)metadata.Length)
                {
                    break;
                }

                PdaEncyclopediaEntryMetaDTO meta = metadata[bitIndex];
                bool hasDataMonolithMetadata = (meta.Flags & MetaFlagDataMonolithSource) != 0;
                if (fillOnlyMissingDataMonolithRows && hasDataMonolithMetadata)
                    continue;

                meta.EntryHash = record.PacketHash;
                meta.BitIndex = bitIndex;
                meta.SourceId = DataMonolithSourceId;
                meta.TitleHash = record.PacketHash;
                meta.Flags = (ushort)(meta.Flags | MetaFlagDataMonolithSource);
                meta.Revision++;
                if (!TryWriteMetadataEntry(record.PacketHash, bitIndex, in meta, out bool metadataCollision))
                {
                    if (metadataCollision)
                        SetFault(FaultMetadataCollision);
                    break;
                }

                metadataWrites++;
            }

            return scanned;
        }

        private void ResetDataMonolithMetadataSeed(uint localeHash)
        {
            _dataMonolithMetadataSeeded = false;
            _dataMonolithMetadataSeedCursor = 0;
            _dataMonolithMetadataSeedStage = 0;
            _dataMonolithMetadataSeedLocaleHash = localeHash != 0u
                ? localeHash
                : H8AppliedLoreRuntime.DefaultLocaleHash;
        }

        private void SeedH8lrMetadata()
        {
            if (_h8lrMetadataSeeded || !EnsureVaultBuffers())
                return;

            PdaH8lrLoreStore store = _h8lrLoreStore;
            if (store == null || !store.IsOpen)
                return;

            int count = math.min(store.EntryCount, MaxMetadataEntries);
            int metadataWrites = 0;
            for (int i = 0; i < count; i++)
            {
                if (!store.TryGetRecord(i, out PdaH8lrRecordDTO record) || record.Hash == 0u)
                    continue;

                if (!TryEnsureBitIndex(record.Hash, out ushort bitIndex))
                {
                    SetFault(FaultMetadataFull);
                    break;
                }

                if (!TryReadVaultBuffer(in _metadataHandle, MetadataBufferId, out NativeArray<PdaEncyclopediaEntryMetaDTO>.ReadOnly metadata) ||
                    (uint)bitIndex >= (uint)metadata.Length)
                {
                    break;
                }

                PdaEncyclopediaEntryMetaDTO meta = metadata[bitIndex];

                meta.EntryHash = record.Hash;
                meta.BitIndex = bitIndex;
                meta.SourceId = H8lrSourceId;
                meta.TitleHash = record.Hash;
                meta.Flags = (ushort)(meta.Flags | MetaFlagH8lrSource);
                meta.Revision++;
                if (!TryWriteMetadataEntry(record.Hash, bitIndex, in meta, out bool metadataCollision))
                {
                    if (metadataCollision)
                        SetFault(FaultMetadataCollision);
                    break;
                }

                metadataWrites++;
            }

            if (metadataWrites > 0)
                TryCommitMetadataRevision(true);

            _h8lrMetadataSeeded = true;
        }

        private void SeedMockLoreDatabase()
        {
            if (_mockSeeded || !CanUseMockLoreFallback() || !EnsureVaultBuffers())
                return;

            if (!TryReadVaultBuffer(in _mockIndexHandle, MockIndexBufferId, out NativeArray<BabelIndexDTO>.ReadOnly index) ||
                !TryClearMockIndexBuffer())
            {
                return;
            }

            int offset = 0;
            int seededCount = 0;
            for (int i = 0; i < MockEntryCapacity && i < index.Length; i++)
            {
                uint hash = ResolveMockHash(i);
                int start = offset;
                if (!TryWriteMockEntryToVault(offset, hash, i, out int byteLength))
                {
                    SetFault(FaultMissingText);
                    break;
                }

                offset += byteLength;
                BabelIndexDTO row = new BabelIndexDTO
                {
                    StringHash = hash,
                    ByteOffset = (uint)start,
                    ByteLength = (uint)byteLength,
                    _pad0 = 0u
                };
                if (!TryWriteMockIndexRow(i, in row))
                    break;

                seededCount++;

                PdaAup48 aup = default;
                uint sourceId = TryGetH8lrUtf8(hash, out _) ? H8lrSourceId : 0x5348494Eu;
                UnlockEntry(hash, in aup, sourceId, ResolvePdaFrame(), false, false, out _);
            }

            if (TryReadRuntimeAndMask(out PdaEncyclopediaRuntimeStateDTO state, out EncyclopediaStateDTO encyclopedia))
            {
                state.MockEntryCount = (uint)seededCount;
                encyclopedia.MockEntryCount = state.MockEntryCount;
                TryCommitRuntimeAndMask(in state, in encyclopedia);
            }

            _mockSeeded = true;
        }

        private bool TryClearMockIndexBuffer()
        {
            IDataVault vault = _vault;
            if (!CanUsePdaVaultHandles() ||
                vault == null ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireWriteLock(in _mockIndexHandle, VaultOwnerSystemId, out NativeArray<BabelIndexDTO> index))
            {
                return false;
            }

            try
            {
                if (!index.IsCreated)
                    return false;

                for (int i = 0; i < index.Length; i++)
                    index[i] = default;

                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _mockIndexHandle, VaultOwnerSystemId);
            }
        }

        private bool TryWriteMockIndexRow(int indexPosition, in BabelIndexDTO row)
        {
            IDataVault vault = _vault;
            if (!CanUsePdaVaultHandles() ||
                vault == null ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireWriteLock(in _mockIndexHandle, VaultOwnerSystemId, out NativeArray<BabelIndexDTO> index))
            {
                return false;
            }

            try
            {
                if (!index.IsCreated || (uint)indexPosition >= (uint)index.Length)
                    return false;

                index[indexPosition] = row;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _mockIndexHandle, VaultOwnerSystemId);
            }
        }

        private bool TryWriteMockEntryToVault(
            int offset,
            uint hash,
            int ordinal,
            out int byteLength)
        {
            byteLength = 0;
            IDataVault vault = _vault;
            if (!CanUsePdaVaultHandles() ||
                vault == null ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireWriteLock(in _mockUtf8Handle, VaultOwnerSystemId, out NativeArray<byte> bytes))
            {
                return false;
            }

            try
            {
                return TryWriteMockEntry(bytes, offset, hash, ordinal, out byteLength);
            }
            finally
            {
                vault.ReleaseWriteLock(in _mockUtf8Handle, VaultOwnerSystemId);
            }
        }

        private bool TryWriteMockEntry(
            NativeArray<byte> bytes,
            int offset,
            uint hash,
            int ordinal,
            out int byteLength)
        {
            byteLength = 0;
            if (!bytes.IsCreated || offset < 0 || offset > bytes.Length)
                return false;

            int cursor = offset;
            if (!TryWriteAscii(bytes, ref cursor, "HECTON-8 PDA ENCYCLOPEDIA FALLBACK\n".AsSpan()) ||
                !TryWriteAscii(bytes, ref cursor, "ENTRY ".AsSpan()) ||
                !TryWriteHexAscii(bytes, ref cursor, hash) ||
                !TryWriteAscii(bytes, ref cursor, "\n\nBaked lore dictionary not present. This Vault-backed mock entry proves the streaming lane without JSON or managed string deserialization.\n\n".AsSpan()) ||
                !TryWriteAscii(bytes, ref cursor, "DISCOVERY GRID ^DISCOVERY_GRID^ // DIST ^DISCOVERY_DISTANCE^ // ENTRY ^ENTRY_HASH^ // QUALITY ^QUALITY^ // DEPTH ^DEPTH^\n\n".AsSpan()) ||
                !TryWriteAscii(bytes, ref cursor, "The PDA decodes UTF-8 bytes directly into a pooled TMP page. Typewriter reveal is a presentation lie; the lore payload stays byte-addressed and rollback-safe.\n\n".AsSpan()) ||
                !TryWriteAscii(bytes, ref cursor, "MOCK ORDINAL ".AsSpan()) ||
                !TryWriteDecimalAscii(bytes, ref cursor, ordinal) ||
                !TryWriteAscii(bytes, ref cursor, "\n".AsSpan()))
            {
                return false;
            }

            byteLength = cursor - offset;
            return byteLength > 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ResolveMockHash(int index)
        {
            return DefaultEntryHash + (uint)math.clamp(index, 0, MockEntryCapacity - 1);
        }

        private bool TryGetMockUtf8(uint hash, out ReadOnlySpan<byte> utf8)
        {
            utf8 = default;
            if (!CanUseMockLoreFallback() || !EnsureVaultBuffers() || !_mockSeeded)
                return false;

            if (!TryReadVaultBuffer(in _mockIndexHandle, MockIndexBufferId, out NativeArray<BabelIndexDTO>.ReadOnly index))
                return false;

            if (!TryReadVaultBuffer(in _mockUtf8Handle, MockUtf8BufferId, out NativeArray<byte>.ReadOnly bytes))
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

            byte* ptr = (byte*)bytes.GetUnsafeReadOnlyPtr();
            utf8 = MemoryMarshal.CreateReadOnlySpan(
                ref UnsafeUtility.AsRef<byte>(ptr + row.ByteOffset),
                (int)row.ByteLength);
            return true;
        }

        private static BabelLookupResultDTO ExtractMockLoreSpanScalar(
            NativeArray<BabelIndexDTO>.ReadOnly index,
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
        private bool ParseCsvMetadata(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length <= 0 || !EnsureVaultBuffers())
            {
                return false;
            }

            int lineStart = 0;
            int imported = 0;
            for (int i = 0; i <= bytes.Length; i++)
            {
                if (i < bytes.Length && bytes[i] != (byte)'\n')
                    continue;

                ReadOnlySpan<byte> line = bytes.Slice(lineStart, i - lineStart);
                if (TryParseCsvLine(line, out uint hash, out ushort bitIndex, out bool explicitBitIndex))
                {
                    if (!explicitBitIndex)
                    {
                        if (!TryEnsureBitIndex(hash, out bitIndex))
                        {
                            SetFault(FaultMetadataFull);
                            lineStart = i + 1;
                            continue;
                        }
                    }

                    if (!TryReadVaultBuffer(in _metadataHandle, MetadataBufferId, out NativeArray<PdaEncyclopediaEntryMetaDTO>.ReadOnly metadata) ||
                        (uint)bitIndex >= (uint)metadata.Length)
                    {
                        lineStart = i + 1;
                        continue;
                    }

                    PdaEncyclopediaEntryMetaDTO meta = metadata[bitIndex];
                    if (meta.EntryHash != 0u && meta.EntryHash != hash)
                    {
                        SetFault(FaultMetadataCollision);
                        lineStart = i + 1;
                        continue;
                    }

                    bool newMetadata = meta.EntryHash == 0u;

                    meta.EntryHash = hash;
                    meta.BitIndex = bitIndex;
                    meta.TitleHash = hash;
                    if (!TryWriteMetadataEntry(hash, bitIndex, in meta, out bool metadataCollision))
                    {
                        if (metadataCollision)
                            SetFault(FaultMetadataCollision);
                        lineStart = i + 1;
                        continue;
                    }

                    if (newMetadata &&
                        TryReadRuntimeAndMask(out PdaEncyclopediaRuntimeStateDTO state, out EncyclopediaStateDTO encyclopedia))
                    {
                        state.MetadataCount = math.min((uint)UnlockBitCount, state.MetadataCount + 1u);
                        encyclopedia.MetadataCount = state.MetadataCount;
                        TryCommitRuntimeAndMask(in state, in encyclopedia);
                    }

                    imported++;
                }

                lineStart = i + 1;
            }

            if (imported > 0)
                TryCommitMetadataRevision(true);

            return imported > 0;
        }

        private byte[] EnsureEditorCsvScratchCold()
        {
            byte[] scratch = _editorCsvScratch;
            if (scratch == null || scratch.Length != CsvScratchBytes)
            {
                scratch = new byte[CsvScratchBytes]; // EDITOR COLD ALLOC: local metadata CSV scratch, not GlobalDataVault ownership.
                _editorCsvScratch = scratch;
            }

            return scratch;
        }

        private bool TryParseCsvLine(ReadOnlySpan<byte> line, out uint hash, out ushort bitIndex, out bool explicitBitIndex)
        {
            hash = 0u;
            bitIndex = 0;
            explicitBitIndex = false;
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

            uint parsedBit;
            if (thirdNumeric)
            {
                parsedBit = thirdNumber;
                explicitBitIndex = true;
            }
            else if (secondNumeric && third.Length == 0)
            {
                parsedBit = secondNumber;
                explicitBitIndex = true;
            }
            else
            {
                parsedBit = hash & (UnlockBitCount - 1);
            }

            if (explicitBitIndex && parsedBit >= UnlockBitCount)
                return false;

            bitIndex = (ushort)(parsedBit & (UnlockBitCount - 1));
            return true;
        }
#endif

        private void ClearMetadataBuffer()
        {
            IDataVault vault = _vault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !IsPdaHandleCreated(in _metadataHandle, MetadataBufferId) ||
                !vault.TryAcquireWriteLock(in _metadataHandle, VaultOwnerSystemId, out NativeArray<PdaEncyclopediaEntryMetaDTO> metadata))
            {
                return;
            }

            try
            {
                if (!metadata.IsCreated)
                    return;

                int count = math.min(MaxMetadataEntries, metadata.Length);
                for (int i = 0; i < count; i++)
                    metadata[i] = default;
            }
            finally
            {
                vault.ReleaseWriteLock(in _metadataHandle, VaultOwnerSystemId);
            }
        }

        private void ClearTelemetryBuffer()
        {
            IDataVault vault = _vault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !IsPdaHandleCreated(in _telemetryHandle, TelemetryBufferId) ||
                !vault.TryAcquireWriteLock(in _telemetryHandle, VaultOwnerSystemId, out NativeArray<PdaEncyclopediaTelemetryEntry> telemetry))
            {
                return;
            }

            try
            {
                if (!telemetry.IsCreated)
                    return;

                int count = math.min(TelemetryFrameCount, telemetry.Length);
                for (int i = 0; i < count; i++)
                    telemetry[i] = default;
            }
            finally
            {
                vault.ReleaseWriteLock(in _telemetryHandle, VaultOwnerSystemId);
            }
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
            if (!Application.isPlaying)
                return;

            if (!_registeredLateFrame)
                _registeredLateFrame = SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI);

            if (!_registeredSlowTick)
                _registeredSlowTick = SystemDispatcher.Register((ISlowTickable)this, PriorityLayer.UI);
        }

        private void UnregisterDispatcherLanes()
        {
            if (_registeredLateFrame)
            {
                SystemDispatcher.UnregisterLateFrameTickableDirect(this, PriorityLayer.UI);
                _registeredLateFrame = false;
            }

            if (_registeredSlowTick)
            {
                SystemDispatcher.Unregister((ISlowTickable)this, PriorityLayer.UI);
                _registeredSlowTick = false;
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
            if (!EnsureVaultBuffers() ||
                !TryReadVaultBuffer(in _runtimeStateHandle, RuntimeStateBufferId, out NativeArray<PdaEncyclopediaRuntimeStateDTO>.ReadOnly states) ||
                states.Length < 1)
            {
                return false;
            }

            PdaEncyclopediaRuntimeStateDTO state = states[0];
            aup.GridX = state.LastDiscoveryGridX;
            aup.GridY = state.LastDiscoveryGridY;
            aup.GridZ = state.LastDiscoveryGridZ;
            aup.LocalX = state.LastDiscoveryLocalX;
            aup.LocalY = state.LastDiscoveryLocalY;
            aup.LocalZ = state.LastDiscoveryLocalZ;
            return (state.Flags & StateFlagPreciseAup) != 0u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryCaptureSignalAup(in ScanCompleteSignal signal, out PdaAup48 aup)
        {
            ScanCompleteSignal copy = signal;
            aup = UnsafeUtility.As<ScanCompleteSignal, PdaAup48>(ref copy);
            return IsFiniteAup(in aup);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryCaptureSignalAup(in LoreFragmentScannedSignal signal, out PdaAup48 aup)
        {
            LoreFragmentScannedSignal copy = signal;
            aup = UnsafeUtility.As<LoreFragmentScannedSignal, PdaAup48>(ref copy);
            return IsFiniteAup(in aup);
        }

        private static bool HasPairedScanComplete(
            ReadOnlySpan<ScanCompleteSignal> scanSignals,
            in LoreFragmentScannedSignal loreSignal)
        {
            for (int i = 0; i < scanSignals.Length; i++)
            {
                ScanCompleteSignal scan = scanSignals[i];
                if (scan.EntryHash == loreSignal.Hash &&
                    scan.SourceId == loreSignal.SourceId)
                    return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFiniteAup(in PdaAup48 aup)
        {
            return math.isfinite(aup.LocalX) &&
                math.isfinite(aup.LocalY) &&
                math.isfinite(aup.LocalZ);
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
            flags |= (_activeUtf8SourceFlags & 7u) << StateFlagSourceShift;
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
            if (EnsureVaultBuffers() &&
                TryReadVaultBuffer(in _runtimeStateHandle, RuntimeStateBufferId, out NativeArray<PdaEncyclopediaRuntimeStateDTO>.ReadOnly states) &&
                states.Length >= 1)
            {
                PdaEncyclopediaRuntimeStateDTO state = states[0];
                state.FaultHash = faultHash;
                state.StreamState = (uint)PdaEncyclopediaStreamState.Fault;
                TryWriteRuntimeState(in state);
            }
        }

        private void QueueBlackBoxDump()
        {
            if (_lastFaultHash != 0u)
                _queuedBlackBoxFaultHash = _lastFaultHash;

            _blackBoxDumpQueued = true;
        }

        private void QueueBlackBoxDump(uint faultHash)
        {
            if (faultHash != 0u)
                _queuedBlackBoxFaultHash = faultHash;

            _blackBoxDumpQueued = true;
        }

        private void FlushQueuedBlackBoxDump()
        {
            if (!_blackBoxDumpQueued)
                return;

            if (DumpBlackBox())
            {
                _blackBoxDumpQueued = false;
                _queuedBlackBoxFaultHash = 0u;
            }
        }

        private bool DumpBlackBox()
        {
            if (!EnsureVaultBuffers())
            {
                return false;
            }

            try
            {
                return WriteBlackBoxDump(BlackBoxDumpRelativePath);
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

            return false;
        }

        private bool WriteBlackBoxDump(string path)
        {
            const int headerBytes = 32;
            const int telemetryEntryBytes = 64;
            int byteCount = headerBytes + TelemetryFrameCount * telemetryEntryBytes;
            NativeArray<PdaEncyclopediaTelemetryEntry>.ReadOnly telemetrySnapshot = default;
            bool hasTelemetrySnapshot =
                _vault != null &&
                !_vault.IsCompactionFenceActive &&
                TryReadVaultBuffer(in _telemetryHandle, TelemetryBufferId, out telemetrySnapshot) &&
                !_vault.IsCompactionFenceActive;
            NativeArray<byte> payload = default;
            try
            {
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(PDAEncyclopediaStreamer),
                    "pdaBlackBoxPayload",
                    NativeArrayOptions.ClearMemory);
                byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(payload);
                Span<byte> buffer = new Span<byte>(destination, byteCount);
                Span<byte> header = buffer.Slice(0, headerBytes);
                WriteUIntLittleEndian(header.Slice(0, 4), StateMagic);
                WriteUIntLittleEndian(header.Slice(4, 4), ResolvePdaFrame());
                uint faultHash = _queuedBlackBoxFaultHash != 0u ? _queuedBlackBoxFaultHash : _lastFaultHash;
                WriteUIntLittleEndian(header.Slice(8, 4), faultHash);
                WriteUIntLittleEndian(header.Slice(12, 4), (uint)TelemetryFrameCount);
                WriteUIntLittleEndian(header.Slice(16, 4), (uint)UnsafeUtility.SizeOf<PdaEncyclopediaTelemetryEntry>());
                WriteUIntLittleEndian(header.Slice(20, 4), _activeEntryHash);

                for (int i = 0; i < TelemetryFrameCount; i++)
                {
                    PdaEncyclopediaTelemetryEntry entry = default;
                    if (hasTelemetrySnapshot && (uint)i < (uint)telemetrySnapshot.Length)
                        entry = telemetrySnapshot[i];

                    Span<byte> row = buffer.Slice(headerBytes + i * telemetryEntryBytes, telemetryEntryBytes);
                    WriteTelemetryDumpEntry(row, in entry);
                }

                return NativeFaultDumpWriter.TryWriteAll(path, payload, byteCount);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(PDAEncyclopediaStreamer),
                    "pdaBlackBoxPayload");
            }
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ReadMaskWord(in EncyclopediaStateDTO mask, int wordIndex)
        {
            switch (wordIndex)
            {
                case 0: return mask.Mask0;
                case 1: return mask.Mask1;
                case 2: return mask.Mask2;
                default: return mask.Mask3;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void OrMaskWord(ref ulong mask0, ref ulong mask1, ref ulong mask2, ref ulong mask3, int wordIndex, ulong bit)
        {
            switch (wordIndex)
            {
                case 0:
                    mask0 |= bit;
                    break;
                case 1:
                    mask1 |= bit;
                    break;
                case 2:
                    mask2 |= bit;
                    break;
                default:
                    mask3 |= bit;
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ReadPromoteMaskWord(ulong mask0, ulong mask1, ulong mask2, ulong mask3, int wordIndex)
        {
            switch (wordIndex)
            {
                case 0: return mask0;
                case 1: return mask1;
                case 2: return mask2;
                default: return mask3;
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

        private static bool TryWriteAscii(NativeArray<byte> bytes, ref int cursor, ReadOnlySpan<char> text)
        {
            if (!bytes.IsCreated || cursor < 0 || cursor > bytes.Length || text.Length > bytes.Length - cursor)
                return false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                bytes[cursor++] = c <= 0x7F ? (byte)c : (byte)'?';
            }

            return true;
        }

        private static bool TryWriteHexAscii(NativeArray<byte> bytes, ref int cursor, uint value)
        {
            if (!bytes.IsCreated || cursor < 0 || cursor > bytes.Length || bytes.Length - cursor < 10)
                return false;

            bytes[cursor++] = (byte)'0';
            bytes[cursor++] = (byte)'x';
            for (int shift = 28; shift >= 0; shift -= 4)
            {
                uint nibble = (value >> shift) & 0xFu;
                bytes[cursor++] = (byte)(nibble < 10u ? '0' + nibble : 'A' + (nibble - 10u));
            }

            return true;
        }

        private static bool TryWriteDecimalAscii(NativeArray<byte> bytes, ref int cursor, int value)
        {
            Span<char> tmp = stackalloc char[16];
            if (!value.TryFormat(tmp, out int written))
                return false;

            if (!bytes.IsCreated || cursor < 0 || cursor > bytes.Length || written > bytes.Length - cursor)
                return false;

            for (int i = 0; i < written; i++)
                bytes[cursor++] = (byte)tmp[i];

            return true;
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
