using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Data;
using Hecton8.Core.Memory;
using Hecton8.Physics.Vehicles;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using AbsoluteUniversePosition = Hecton8.World.AbsoluteUniversePosition;

namespace Hecton8.Audio.Synthesis
{
    internal ref struct VocalVaultViews
    {
        public NativeArray<VocalStateDTO> State;
        public NativeArray<VocalCodecStateDTO> Codec;
        public NativeArray<VocalTelemetryEntryDTO> Telemetry;
        public NativeArray<VocalDecodeCounters64> Counters;
        public NativeArray<float> Waveform;
        public NativeArray<byte> MockBankBytes;
        public NativeArray<VocalBankIndexRecordDTO> MockRecords;
#if UNITY_EDITOR
        public NativeArray<VocalDialogueMetadataDTO> CsvMetadata;
#endif
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-3890)]
    [AddComponentMenu("Hecton8/Audio/Vocal Bank Playback Runtime")]
    public sealed unsafe class VocalBankPlaybackRuntime : MonoBehaviour, IColdTickable, IUpdatable, IGlobalRegistryHotSwapListener
    {
        private const SystemID VaultOwner = SystemID.AudioVocalSynthesis;
        private const int TelemetryCapacity = 300;
        private const int WaveformCapacity = 2048;
        private const int MockBankByteCapacity = 196608;
        private const int MockRecordCapacity = 1;
#if UNITY_EDITOR
        private const int CsvMetadataCapacity = 8192;
        private const int EditorCsvScratchBytes = 1048576;
        private const string EditorCsvScratchLabel = "editorCsvScratch";
#endif
        private const int DefaultMockSamples = 32000;
        private const uint DefaultMockPhraseHash = 0x05203E88u; // FNV1a("VO_SHINOBU_MOCK").
        private const uint VocalCueLaneHash = 0xC001260u;
        private const uint VwsPreemptedFlag = 1u << 5;
        private const int DefaultPlayVoiceOverPriority = 128;
        private const uint PlayVoiceOverSignalMissWarningHash = 0x50564F4Du; // PVOM.
        private const uint PlayVoiceOverSignalContextHash = 0x50564F43u; // PVOC.
        private const uint VocalBankMissWarningHash = 0x56424D53u; // VBMS.
        private const uint VocalBankMissContextHash = 0x56424B43u; // VBKC.
        private const uint PlayVoiceOverSubtitleDropWarningHash = 0x50565344u; // PVSD.
        private const uint PlayVoiceOverSubtitleContextHash = 0x50565343u; // PVSC.
        private const uint PlayVoiceOverSubtitleSourceHash = 0x50565352u; // PVSR.
        private const ushort DefaultPlayVoiceOverSubtitleDurationMilliseconds = 3250;
        private const uint VesselTelemetryHandleRetryMask = 63u;
        private const float DspDumpThresholdMicroseconds = 1000f;
        private const float VesselCareColdPlaybackSpeed = 0.985f;
        private const float VesselCareWarmPlaybackSpeed = 1.015f;
        private const int BankMutationSpinLimit = 4096;
        private const string BankRelativePath = "Hecton8/Audio/vocal_banks.h8bin";
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_1308_Synthesis.bin";
        private const int LockState = 1 << 0;
        private const int LockCodec = 1 << 1;
        private const int LockTelemetry = 1 << 2;
        private const int LockCounters = 1 << 3;
        private const int LockWaveform = 1 << 4;
        private const int LockMockBankBytes = 1 << 5;
        private const int LockMockRecords = 1 << 6;
#if UNITY_EDITOR
        private const int LockCsvMetadata = 1 << 7;
#endif
        private const ulong GuardBitState = 1UL << ((int)BufferID.AudioVocalSynthesisState & 31);
        private const ulong GuardBitCodec = 1UL << ((int)BufferID.AudioVocalSynthesisCodecState & 31);
        private const ulong GuardBitTelemetry = 1UL << ((int)BufferID.AudioVocalSynthesisTelemetry & 31);
        private const ulong GuardBitCounters = 1UL << ((int)BufferID.AudioVocalSynthesisTelemetryCursor & 31);
        private const ulong GuardBitWaveform = 1UL << ((int)BufferID.AudioVocalSynthesisWaveform & 31);
        private const ulong GuardBitMockBankBytes = 1UL << ((int)BufferID.AudioVocalSynthesisMockBankBytes & 31);
        private const ulong GuardBitMockRecords = 1UL << ((int)BufferID.AudioVocalSynthesisMockBankRecords & 31);
#if UNITY_EDITOR
        private const ulong GuardBitCsvMetadata = 1UL << ((int)BufferID.AudioVocalSynthesisCsvMetadata & 31);
#endif
        private static readonly ulong VocalAudioCallbackMutationGuardMask =
            GuardBitState |
            GuardBitCodec |
            GuardBitTelemetry |
            GuardBitCounters |
            GuardBitWaveform |
            GuardBitMockBankBytes;
        private static readonly ulong VocalControlMutationGuardMask =
            GuardBitState |
            GuardBitCodec |
            GuardBitCounters |
            GuardBitMockBankBytes
#if UNITY_EDITOR
            | GuardBitCsvMetadata
#endif
            ;
        private static readonly ulong VocalBankBytesMutationGuardMask =
            GuardBitMockBankBytes;
        private static readonly ulong VocalBankBuildMutationGuardMask =
            GuardBitMockBankBytes |
            GuardBitMockRecords;
        private static readonly ulong VocalInitializeMutationGuardMask =
            GuardBitState |
            GuardBitCodec |
            GuardBitTelemetry |
            GuardBitCounters |
            GuardBitWaveform |
            GuardBitMockRecords
#if UNITY_EDITOR
            | GuardBitCsvMetadata
#endif
            ;
#if UNITY_EDITOR
        private static readonly ulong VocalCsvMutationGuardMask =
            GuardBitCsvMetadata;
#endif

        private static VocalBankPlaybackRuntime _activeInstance;
        private static int _decodePointerReady;

        [Header("Cold Bank")]
        [SerializeField] private bool _autoBindToSceneAudioListener = true;
        [SerializeField] private bool _mixIntoExistingAudioGraph = true;
        [SerializeField, Range(0f, 1f)] private float _mockQualityBias01 = 1f;
        [SerializeField] private bool _useMockBankWhenFileMissing = true;

        private IDataVault _dataVault;
        private VaultGenerationHandle<VocalStateDTO> _stateHandle;
        private VaultGenerationHandle<VocalCodecStateDTO> _codecHandle;
        private VaultGenerationHandle<VocalTelemetryEntryDTO> _telemetryHandle;
        private VaultGenerationHandle<VocalDecodeCounters64> _countersHandle;
        private VaultGenerationHandle<float> _waveformHandle;
        private VaultGenerationHandle<byte> _mockBankBytesHandle;
        private VaultGenerationHandle<VocalBankIndexRecordDTO> _mockRecordsHandle;
        private VaultGenerationHandle<VesselTelemetryEntry> _vesselTelemetryHandle;
#if UNITY_EDITOR
        private VaultGenerationHandle<VocalDialogueMetadataDTO> _csvMetadataHandle;
#endif

        private long _bankByteLength;

        private int _nativeAllocated;
        private int _registeredUpdate;
        private int _registeredColdTick;
        private int _registeredHotSwap;
        private int _usingMockBank;
        private int _dumpRequested;
        private int _audioCallbackInFlight;
        private int _bankReleaseInProgress;
        private int _invalidAudioFilterHost;
        private IDataVault _vocalMutationGuardVault;
        private ulong _vocalMutationGuardMask;
        private int _vocalMutationGuardDepth;
#if UNITY_EDITOR
        private int _csvMetadataCount;
        private byte* _editorCsvScratch;
        private int _editorCsvScratchCapacity;
        private int _editorCsvScratchSentinelId;
#endif
        private uint _frameCounter;
        private float _cachedGlobalQualityWeight = 1f;
        private float _vesselCareTone01;
        private float _lastAppliedVesselCareTone01;
        private uint _lastPlayVoiceOverTextHash;
        private uint _lastPlayVoiceOverVoiceHash;
        private int _playVoiceOverSignalConsumedCount;
        private int _playVoiceOverSignalMissCount;
        private int _lastPlayVoiceOverSignalMissTelemetryFrame = -1;
        private int _vocalBankMissTelemetryCount;
        private int _lastVocalBankMissTelemetryFrame = -1;
        private int _playVoiceOverSubtitleCuePublishedCount;
        private int _playVoiceOverSubtitleCueDropCount;
        private int _lastPlayVoiceOverSubtitleDropTelemetryFrame = -1;

        public int PlayVoiceOverSignalConsumedCount => _playVoiceOverSignalConsumedCount;
        public int PlayVoiceOverSignalMissCount => _playVoiceOverSignalMissCount;
        public uint LastPlayVoiceOverTextHash => _lastPlayVoiceOverTextHash;
        public uint LastPlayVoiceOverVoiceHash => _lastPlayVoiceOverVoiceHash;
        public int VocalBankMissTelemetryCount => _vocalBankMissTelemetryCount;
        public int PlayVoiceOverSubtitleCuePublishedCount => _playVoiceOverSubtitleCuePublishedCount;
        public int PlayVoiceOverSubtitleCueDropCount => _playVoiceOverSubtitleCueDropCount;

        public static bool TryGetActive(out VocalBankPlaybackRuntime runtime)
        {
            runtime = _activeInstance;
            return runtime != null && Volatile.Read(ref runtime._nativeAllocated) != 0;
        }

        public static bool TryGetEditorState(out VocalStateDTO state, out VocalCodecStateDTO codec)
        {
            state = default;
            codec = default;
            if (!TryGetActive(out VocalBankPlaybackRuntime runtime))
                return false;

            IDataVault vault = runtime._dataVault;
            if (vault == null ||
                !vault.TryReadOnlyHandle(in runtime._stateHandle, out NativeArray<VocalStateDTO>.ReadOnly stateView) ||
                !vault.TryReadOnlyHandle(in runtime._codecHandle, out NativeArray<VocalCodecStateDTO>.ReadOnly codecView) ||
                stateView.Length <= 0 ||
                codecView.Length <= 0)
                return false;

            state = stateView[0];
            codec = codecView[0];
            return true;
        }

        public static bool TryGetEditorTelemetry(int offsetFromNewest, out VocalTelemetryEntryDTO entry)
        {
            entry = default;
            if (!TryGetActive(out VocalBankPlaybackRuntime runtime))
                return false;

            IDataVault vault = runtime._dataVault;
            if (vault == null ||
                !vault.TryReadOnlyHandle(in runtime._countersHandle, out NativeArray<VocalDecodeCounters64>.ReadOnly countersView) ||
                !vault.TryReadOnlyHandle(in runtime._telemetryHandle, out NativeArray<VocalTelemetryEntryDTO>.ReadOnly telemetryView) ||
                countersView.Length <= 0 ||
                telemetryView.Length <= 0)
                return false;

            int capacity = math.min(TelemetryCapacity, telemetryView.Length);
            int cursor = math.max(0, countersView[0].TelemetryCursor);
            int offset = math.clamp(offsetFromNewest, 0, capacity - 1);
            int index = cursor - 1 - offset;
            while (index < 0)
                index += capacity;

            entry = telemetryView[index % capacity];
            return true;
        }

        public static bool TryGetEditorWaveformSample(int newestOffset, out float sample)
        {
            sample = 0f;
            if (!TryGetActive(out VocalBankPlaybackRuntime runtime))
                return false;

            IDataVault vault = runtime._dataVault;
            if (vault == null ||
                !vault.TryReadOnlyHandle(in runtime._countersHandle, out NativeArray<VocalDecodeCounters64>.ReadOnly countersView) ||
                !vault.TryReadOnlyHandle(in runtime._waveformHandle, out NativeArray<float>.ReadOnly waveformView) ||
                countersView.Length <= 0 ||
                waveformView.Length <= 0)
                return false;

            int capacity = math.min(WaveformCapacity, waveformView.Length);
            int cursor = math.max(0, countersView[0].WaveformCursor);
            int offset = math.clamp(newestOffset, 0, capacity - 1);
            int index = cursor - 1 - offset;
            while (index < 0)
                index += capacity;

            sample = waveformView[index % capacity];
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeInstanceAfterSceneLoad()
        {
            if (!Application.isPlaying)
                return;

            EnsureDecodeFunctionPointerCold();
            EnsureVocalCueLaneCold();
            if (_activeInstance != null)
                return;

            AudioListener listener = ResolvePlayerAudioListenerCold();
            if (listener == null)
                return;

            GameObject listenerObject = listener.gameObject;
            if (listenerObject.TryGetComponent(out VocalBankPlaybackRuntime existing))
            {
                _activeInstance = existing;
                return;
            }

            return;
        }

        private static AudioListener ResolvePlayerAudioListenerCold()
        {
            Camera playerCamera = null;
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext != null)
                playerCamera = playerContext.PlayerCamera;

            if (playerCamera == null)
            {
                IPlayerSensoryService playerSensory = GlobalRegistry.PlayerSensory;
                if (playerSensory != null)
                    playerCamera = playerSensory.PlayerCamera;
            }

            if (playerCamera == null)
                return null;

            return playerCamera.TryGetComponent(out AudioListener listener) ? listener : null;
        }

        private static void EnsureDecodeFunctionPointerCold()
        {
            if (Volatile.Read(ref _decodePointerReady) != 0)
                return;

            Volatile.Write(ref _decodePointerReady, 1);
        }

        private static void EnsureVocalCueLaneCold()
        {
            SignalBus<VocalCueSignal>.ConfigureCacheLineCritical(64, 64, 16, VocalCueLaneHash);
            SignalBus<VocalCueSignal>.EnsureInitialized();
            SignalBus<PlayVoiceOverSignal>.Configure(expectedCapacity: 32, maxFrameSignals: 32, lowTierFrameSignals: 8);
            SignalBus<PlayVoiceOverSignal>.EnsureInitialized();
            SignalBus<SubtitleCueSignal>.Configure(
                SubtitleCueSignal.ExpectedCapacity,
                maxFrameSignals: SubtitleCueSignal.MaxFrameSignals,
                lowTierFrameSignals: SubtitleCueSignal.LowTierFrameSignals,
                laneHash: SubtitleCueSignal.LaneHash);
            SignalBus<SubtitleCueSignal>.EnsureInitialized();
        }

        private void Awake()
        {
            if (RejectInvalidAudioFilterHostCold())
                return;

            EnsureDecodeFunctionPointerCold();
            EnsureVocalCueLaneCold();
            CacheDataVaultCold();
            EnsureVaultStorage();
            RefreshGlobalQualitySnapshotCold();
            OpenOrGenerateBankCold();
#if UNITY_EDITOR
            ReloadDialogueCsvMetadataCold();
#endif
        }

        private void OnEnable()
        {
            if (RejectInvalidAudioFilterHostCold())
                return;

            EnsureDecodeFunctionPointerCold();
            EnsureVocalCueLaneCold();
            CacheDataVaultCold();
            EnsureVaultStorage();
            if (_autoBindToSceneAudioListener || _activeInstance == null)
                _activeInstance = this;

            if (GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Core))
                _registeredUpdate = 1;
            if (GlobalRegistry.TryRegisterColdTickable(this, PriorityLayer.Core))
                _registeredColdTick = 1;
            if (GlobalRegistry.TryRegisterHotSwapListener(this))
                _registeredHotSwap = 1;
        }

        private void OnDisable()
        {
            UnregisterRuntime();
        }

        private void OnDestroy()
        {
            UnregisterRuntime();
            ClearBankStateCold();
            DisposeVaultStorage();
        }

        private bool RejectInvalidAudioFilterHostCold()
        {
            bool hasListener = TryGetComponent<AudioListener>(out _);
            bool hasSource = TryGetComponent(out AudioSource source);
            if (!hasListener && !hasSource)
            {
                Volatile.Write(ref _invalidAudioFilterHost, 1);
                if (ReferenceEquals(_activeInstance, this))
                    _activeInstance = null;
                enabled = false;
                return true;
            }

            if (hasSource && source.clip == null)
            {
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                if (source.isPlaying)
                    source.Stop();
            }

            Volatile.Write(ref _invalidAudioFilterHost, 0);
            return false;
        }

        public void Tick(float deltaTime)
        {
            if (Volatile.Read(ref _nativeAllocated) == 0)
                return;

            unchecked
            {
                _frameCounter++;
                if (_frameCounter == 0u)
                    _frameCounter = 1u;
            }

            RefreshVesselTelemetryHandleIfMissing(_frameCounter);
            _vesselCareTone01 = ReadVesselCareTone01();
            DrainVocalCueSignals(_vesselCareTone01);
            if (Interlocked.Exchange(ref _dumpRequested, 0) != 0)
                DumpBlackboxCold();

            _ = deltaTime;
        }

        public void ColdTick()
        {
            if (Volatile.Read(ref _nativeAllocated) == 0)
                EnsureVaultStorage();

            RefreshGlobalQualitySnapshotCold();
            if (Volatile.Read(ref _bankByteLength) <= 0)
                OpenOrGenerateBankCold();
#if UNITY_EDITOR
            ReloadDialogueCsvMetadataCold();
#endif
        }

        public void OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            IDataVault nextVault = currentService is IDataVault vault ? vault : null;
            RebindDataVaultCold(nextVault);
            EnsureVaultStorage();
            OpenOrGenerateBankCold();
            _ = previousService;
        }

#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        private void OnAudioFilterRead(float[] data, int channels)
        {
            // L19 hop2 LIVE: VocalBank OnAudioFilterRead AtomicSafety AV under batch
            if (Application.isBatchMode)
                return;

            if (data != null && data.Length > 0)
                ZeroManagedAudioBuffer(data, 0, data.Length);

            _ = channels;
        }
#else
        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (Volatile.Read(ref _bankReleaseInProgress) != 0 ||
                Volatile.Read(ref _invalidAudioFilterHost) != 0 ||
                data == null ||
                data.Length <= 0 ||
                Volatile.Read(ref _nativeAllocated) == 0 ||
                Volatile.Read(ref _decodePointerReady) == 0)
            {
                if (data != null && data.Length > 0)
                    ZeroManagedAudioBuffer(data, 0, data.Length);
                return;
            }

            Interlocked.Increment(ref _audioCallbackInFlight);
            int lockMask = 0;
            IDataVault lockedVault = null;
            try
            {
                if (Volatile.Read(ref _bankReleaseInProgress) != 0 ||
                    !TryAcquireAudioCallbackViews(out VocalVaultViews views, out lockMask, out lockedVault))
                {
                    ZeroManagedAudioBuffer(data, 0, data.Length);
                    return;
                }

                int safeChannels = math.clamp(channels, 1, 8);
                int sampleCount = data.Length / safeChannels;
                if (sampleCount <= 0)
                {
                    ZeroManagedAudioBuffer(data, 0, data.Length);
                    return;
                }

                long bankByteLength = Volatile.Read(ref _bankByteLength);
                if (bankByteLength <= 0L || bankByteLength > views.MockBankBytes.Length)
                {
                    ZeroManagedAudioBuffer(data, 0, data.Length);
                    WriteVocalFault(ref views, VocalBankConstants.StateFlagBankMiss, 0u);
                    return;
                }

                long startTicks = Stopwatch.GetTimestamp();
                fixed (float* output = data)
                {
                    byte* bank = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(views.MockBankBytes);
                    VocalStateDTO* state = (VocalStateDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(views.State);
                    VocalCodecStateDTO* codec = (VocalCodecStateDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(views.Codec);
                    VocalTelemetryEntryDTO* telemetry = (VocalTelemetryEntryDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(views.Telemetry);
                    VocalDecodeCounters64* counters = (VocalDecodeCounters64*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(views.Counters);
                    float* waveform = (float*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(views.Waveform);
                    VocalDecodeKernel.DecodeIntoAudioBuffer(
                        output,
                        sampleCount,
                        safeChannels,
                        (_autoBindToSceneAudioListener || _mixIntoExistingAudioGraph) ? 1 : 0,
                        bank,
                        bankByteLength,
                        state,
                        codec,
                        telemetry,
                        counters,
                        waveform,
                        WaveformCapacity,
                        _frameCounter);
                }

                long elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
                float elapsedMicroseconds = elapsedTicks * (1000000f / Stopwatch.Frequency);
                if (views.Counters.Length > 0)
                {
                    VocalDecodeCounters64 counters = views.Counters[0];
                    counters.LastDspMicroseconds = elapsedMicroseconds;
                    views.Counters[0] = counters;
                    if (views.Telemetry.Length > 0)
                    {
                        int index = counters.TelemetryCursor - 1;
                        while (index < 0)
                            index += TelemetryCapacity;
                        VocalTelemetryEntryDTO entry = views.Telemetry[index % TelemetryCapacity];
                        entry.DspMicroseconds = elapsedMicroseconds;
                        views.Telemetry[index % TelemetryCapacity] = entry;
                    }
                    if (elapsedMicroseconds > DspDumpThresholdMicroseconds)
                        Interlocked.Exchange(ref _dumpRequested, 1);
                }
            }
            finally
            {
                ReleaseVocalMutationGuardScope(lockedVault, lockMask);
                Interlocked.Decrement(ref _audioCallbackInFlight);
            }
        }
#endif

        private bool TryAcquireAudioCallbackViews(out VocalVaultViews views, out int lockMask, out IDataVault lockedVault)
        {
            return TryAcquireAudioCallbackViews(_dataVault, out views, out lockMask, out lockedVault);
        }

        private bool TryAcquireAudioCallbackViews(IDataVault vault, out VocalVaultViews views, out int lockMask, out IDataVault lockedVault)
        {
            views = default;
            lockMask = 0;
            lockedVault = vault;
            if (lockedVault == null)
                return false;

            bool ownershipTransferred = false;
            try
            {
                if (!TryAcquireLockedView(lockedVault, in _stateHandle, BufferID.AudioVocalSynthesisState, LockState, VocalAudioCallbackMutationGuardMask, ref lockMask, out views.State) ||
                    !TryAcquireLockedView(lockedVault, in _codecHandle, BufferID.AudioVocalSynthesisCodecState, LockCodec, VocalAudioCallbackMutationGuardMask, ref lockMask, out views.Codec) ||
                    !TryAcquireLockedView(lockedVault, in _telemetryHandle, BufferID.AudioVocalSynthesisTelemetry, LockTelemetry, VocalAudioCallbackMutationGuardMask, ref lockMask, out views.Telemetry) ||
                    !TryAcquireLockedView(lockedVault, in _countersHandle, BufferID.AudioVocalSynthesisTelemetryCursor, LockCounters, VocalAudioCallbackMutationGuardMask, ref lockMask, out views.Counters) ||
                    !TryAcquireLockedView(lockedVault, in _waveformHandle, BufferID.AudioVocalSynthesisWaveform, LockWaveform, VocalAudioCallbackMutationGuardMask, ref lockMask, out views.Waveform) ||
                    !TryAcquireLockedView(lockedVault, in _mockBankBytesHandle, BufferID.AudioVocalSynthesisMockBankBytes, LockMockBankBytes, VocalAudioCallbackMutationGuardMask, ref lockMask, out views.MockBankBytes) ||
                    views.State.Length <= 0 ||
                    views.Codec.Length <= 0 ||
                    views.Telemetry.Length < TelemetryCapacity ||
                    views.Counters.Length <= 0 ||
                    views.Waveform.Length < WaveformCapacity ||
                    views.MockBankBytes.Length <= 0)
                {
                    return false;
                }

                ownershipTransferred = true;
                return true;
            }
            finally
            {
                if (!ownershipTransferred)
                {
                    ReleaseVocalMutationGuardScope(lockedVault, lockMask);
                    views = default;
                    lockMask = 0;
                    lockedVault = null;
                }
            }
        }

        private bool TryAcquireControlViews(out VocalVaultViews views, out int lockMask, out IDataVault lockedVault)
        {
            return TryAcquireControlViews(_dataVault, out views, out lockMask, out lockedVault);
        }

        private bool TryAcquireControlViews(IDataVault vault, out VocalVaultViews views, out int lockMask, out IDataVault lockedVault)
        {
            views = default;
            lockMask = 0;
            lockedVault = vault;
            if (lockedVault == null)
                return false;

            bool ownershipTransferred = false;
            try
            {
                if (!TryAcquireLockedView(lockedVault, in _stateHandle, BufferID.AudioVocalSynthesisState, LockState, VocalControlMutationGuardMask, ref lockMask, out views.State) ||
                    !TryAcquireLockedView(lockedVault, in _codecHandle, BufferID.AudioVocalSynthesisCodecState, LockCodec, VocalControlMutationGuardMask, ref lockMask, out views.Codec) ||
                    !TryAcquireLockedView(lockedVault, in _countersHandle, BufferID.AudioVocalSynthesisTelemetryCursor, LockCounters, VocalControlMutationGuardMask, ref lockMask, out views.Counters) ||
                    !TryAcquireLockedView(lockedVault, in _mockBankBytesHandle, BufferID.AudioVocalSynthesisMockBankBytes, LockMockBankBytes, VocalControlMutationGuardMask, ref lockMask, out views.MockBankBytes) ||
                    views.State.Length <= 0 ||
                    views.Codec.Length <= 0 ||
                    views.Counters.Length <= 0 ||
                    views.MockBankBytes.Length <= 0)
                {
                    return false;
                }

#if UNITY_EDITOR
                if (!TryAcquireLockedView(lockedVault, in _csvMetadataHandle, BufferID.AudioVocalSynthesisCsvMetadata, LockCsvMetadata, VocalControlMutationGuardMask, ref lockMask, out views.CsvMetadata) ||
                    views.CsvMetadata.Length <= 0)
                {
                    return false;
                }
#endif

                ownershipTransferred = true;
                return true;
            }
            finally
            {
                if (!ownershipTransferred)
                {
                    ReleaseVocalMutationGuardScope(lockedVault, lockMask);
                    views = default;
                    lockMask = 0;
                    lockedVault = null;
                }
            }
        }

        private bool TryAcquireBankBuildViews(IDataVault vault, out VocalVaultViews views, out int lockMask, out IDataVault lockedVault)
        {
            views = default;
            lockMask = 0;
            lockedVault = vault;
            if (lockedVault == null)
                return false;

            bool ownershipTransferred = false;
            try
            {
                if (!TryAcquireLockedView(lockedVault, in _mockBankBytesHandle, BufferID.AudioVocalSynthesisMockBankBytes, LockMockBankBytes, VocalBankBuildMutationGuardMask, ref lockMask, out views.MockBankBytes) ||
                    !TryAcquireLockedView(lockedVault, in _mockRecordsHandle, BufferID.AudioVocalSynthesisMockBankRecords, LockMockRecords, VocalBankBuildMutationGuardMask, ref lockMask, out views.MockRecords) ||
                    views.MockBankBytes.Length <= 0 ||
                    views.MockRecords.Length <= 0)
                {
                    return false;
                }

                ownershipTransferred = true;
                return true;
            }
            finally
            {
                if (!ownershipTransferred)
                {
                    ReleaseVocalMutationGuardScope(lockedVault, lockMask);
                    views = default;
                    lockMask = 0;
                    lockedVault = null;
                }
            }
        }

        private bool TryAcquireInitializeViews(IDataVault vault, out VocalVaultViews views, out int lockMask, out IDataVault lockedVault)
        {
            views = default;
            lockMask = 0;
            lockedVault = vault;
            if (lockedVault == null)
                return false;

            bool ownershipTransferred = false;
            try
            {
                if (!TryAcquireLockedView(lockedVault, in _stateHandle, BufferID.AudioVocalSynthesisState, LockState, VocalInitializeMutationGuardMask, ref lockMask, out views.State) ||
                    !TryAcquireLockedView(lockedVault, in _codecHandle, BufferID.AudioVocalSynthesisCodecState, LockCodec, VocalInitializeMutationGuardMask, ref lockMask, out views.Codec) ||
                    !TryAcquireLockedView(lockedVault, in _telemetryHandle, BufferID.AudioVocalSynthesisTelemetry, LockTelemetry, VocalInitializeMutationGuardMask, ref lockMask, out views.Telemetry) ||
                    !TryAcquireLockedView(lockedVault, in _countersHandle, BufferID.AudioVocalSynthesisTelemetryCursor, LockCounters, VocalInitializeMutationGuardMask, ref lockMask, out views.Counters) ||
                    !TryAcquireLockedView(lockedVault, in _waveformHandle, BufferID.AudioVocalSynthesisWaveform, LockWaveform, VocalInitializeMutationGuardMask, ref lockMask, out views.Waveform) ||
                    !TryAcquireLockedView(lockedVault, in _mockRecordsHandle, BufferID.AudioVocalSynthesisMockBankRecords, LockMockRecords, VocalInitializeMutationGuardMask, ref lockMask, out views.MockRecords) ||
#if UNITY_EDITOR
                    !TryAcquireLockedView(lockedVault, in _csvMetadataHandle, BufferID.AudioVocalSynthesisCsvMetadata, LockCsvMetadata, VocalInitializeMutationGuardMask, ref lockMask, out views.CsvMetadata) ||
#endif
                    views.State.Length <= 0 ||
                    views.Codec.Length <= 0 ||
                    views.Telemetry.Length <= 0 ||
                    views.Counters.Length <= 0 ||
                    views.Waveform.Length <= 0 ||
                    views.MockRecords.Length <= 0
#if UNITY_EDITOR
                    || views.CsvMetadata.Length <= 0
#endif
                    )
                {
                    return false;
                }

                ownershipTransferred = true;
                return true;
            }
            finally
            {
                if (!ownershipTransferred)
                {
                    ReleaseVocalMutationGuardScope(lockedVault, lockMask);
                    views = default;
                    lockMask = 0;
                    lockedVault = null;
                }
            }
        }

#if UNITY_EDITOR
        private bool TryAcquireCsvViews(IDataVault vault, out VocalVaultViews views, out int lockMask, out IDataVault lockedVault)
        {
            views = default;
            lockMask = 0;
            lockedVault = vault;
            if (lockedVault == null)
                return false;

            bool ownershipTransferred = false;
            try
            {
                if (!TryAcquireLockedView(lockedVault, in _csvMetadataHandle, BufferID.AudioVocalSynthesisCsvMetadata, LockCsvMetadata, VocalCsvMutationGuardMask, ref lockMask, out views.CsvMetadata) ||
                    views.CsvMetadata.Length <= 0)
                {
                    return false;
                }

                ownershipTransferred = true;
                return true;
            }
            finally
            {
                if (!ownershipTransferred)
                {
                    ReleaseVocalMutationGuardScope(lockedVault, lockMask);
                    views = default;
                    lockMask = 0;
                    lockedVault = null;
                }
            }
        }
#endif

        private static ulong ResolveVocalMutationGuardBit(BufferID bufferId)
        {
            switch (bufferId)
            {
                case BufferID.AudioVocalSynthesisState:
                    return GuardBitState;
                case BufferID.AudioVocalSynthesisCodecState:
                    return GuardBitCodec;
                case BufferID.AudioVocalSynthesisTelemetry:
                    return GuardBitTelemetry;
                case BufferID.AudioVocalSynthesisTelemetryCursor:
                    return GuardBitCounters;
                case BufferID.AudioVocalSynthesisWaveform:
                    return GuardBitWaveform;
                case BufferID.AudioVocalSynthesisMockBankBytes:
                    return GuardBitMockBankBytes;
                case BufferID.AudioVocalSynthesisMockBankRecords:
                    return GuardBitMockRecords;
#if UNITY_EDITOR
                case BufferID.AudioVocalSynthesisCsvMetadata:
                    return GuardBitCsvMetadata;
#endif
                default:
                    return 0UL;
            }
        }

        private bool TryAcquireVocalMutationGuard(IDataVault vault, ulong mutationGuardMask)
        {
            if (vault == null || mutationGuardMask == 0UL)
                return false;

            if (_vocalMutationGuardDepth > 0)
            {
                if (!ReferenceEquals(_vocalMutationGuardVault, vault) ||
                    _vocalMutationGuardMask != mutationGuardMask)
                {
                    return false;
                }

                _vocalMutationGuardDepth++;
                return true;
            }

            if (!vault.TryAcquireMutationGuard(mutationGuardMask))
                return false;

            _vocalMutationGuardVault = vault;
            _vocalMutationGuardMask = mutationGuardMask;
            _vocalMutationGuardDepth = 1;
            return true;
        }

        private void ReleaseVocalMutationGuard(IDataVault vault)
        {
            if (_vocalMutationGuardDepth <= 0)
            {
                _vocalMutationGuardDepth = 0;
                _vocalMutationGuardMask = 0UL;
                _vocalMutationGuardVault = null;
                return;
            }

            if (_vocalMutationGuardDepth > 1)
            {
                _vocalMutationGuardDepth--;
                return;
            }

            IDataVault guardedVault = _vocalMutationGuardVault;
            ulong guardMask = _vocalMutationGuardMask;
            _vocalMutationGuardDepth = 0;
            _vocalMutationGuardMask = 0UL;
            _vocalMutationGuardVault = null;
            if (guardedVault != null && guardMask != 0UL && (vault == null || ReferenceEquals(vault, guardedVault)))
                guardedVault.ReleaseMutationGuard(guardMask);
        }

        private void ForceReleaseVocalMutationGuard()
        {
            IDataVault guardedVault = _vocalMutationGuardVault;
            ulong guardMask = _vocalMutationGuardMask;
            _vocalMutationGuardDepth = 0;
            _vocalMutationGuardMask = 0UL;
            _vocalMutationGuardVault = null;
            if (guardedVault != null && guardMask != 0UL)
                guardedVault.ReleaseMutationGuard(guardMask);
        }

        private bool TryAcquireLockedView<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            int lockBit,
            ulong mutationGuardMask,
            ref int lockMask,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            bool acquiredGuard = false;
            if ((mutationGuardMask & ResolveVocalMutationGuardBit(expectedBufferId)) == 0UL)
                return false;

            if (lockMask == 0)
            {
                if (!TryAcquireVocalMutationGuard(vault, mutationGuardMask))
                    return false;

                acquiredGuard = true;
            }

            if (!IsVocalSynthesisVaultHandle(in handle, expectedBufferId) ||
                !vault.TryReadHandle(in handle, out buffer))
            {
                if (acquiredGuard)
                    ReleaseVocalMutationGuard(vault);
                return false;
            }

            bool ownershipTransferred = false;
            try
            {
                if (!buffer.IsCreated)
                {
                    buffer = default;
                    return false;
                }

                lockMask |= lockBit;
                ownershipTransferred = true;
                return true;
            }
            finally
            {
                if (!ownershipTransferred && acquiredGuard)
                    ReleaseVocalMutationGuard(vault);
            }
        }

        private void ReleaseVocalMutationGuardScope(int lockMask)
        {
            ReleaseVocalMutationGuardScope(_dataVault, lockMask);
        }

        private void ReleaseVocalMutationGuardScope(IDataVault vault, int lockMask)
        {
            if (lockMask == 0)
                return;

            ReleaseVocalMutationGuard(vault);
        }

        private void WriteVocalFault(ref VocalVaultViews views, uint flags, uint phraseHash)
        {
            if (!views.Counters.IsCreated || views.Counters.Length <= 0)
                return;

            VocalDecodeCounters64 counters = views.Counters[0];
            counters.FaultCount++;
            counters.LastFaultFlags = flags;
            counters.LastPhraseHashID = phraseHash;

            if (views.Telemetry.IsCreated && views.Telemetry.Length > 0)
            {
                int capacity = math.min(TelemetryCapacity, views.Telemetry.Length);
                int cursor = counters.TelemetryCursor;
                int index = cursor % capacity;
                VocalTelemetryEntryDTO entry = default;
                entry.Frame = _frameCounter;
                entry.PhraseHashID = phraseHash;
                entry.Flags = flags;
                entry.UnderrunCount = (uint)math.max(0, counters.FaultCount);
                views.Telemetry[index] = entry;
                counters.TelemetryCursor = (cursor + 1) % capacity;
            }

            views.Counters[0] = counters;
        }

        private void DrainVocalCueSignals(float vesselCareTone01)
        {
            if (!TryAcquireControlViews(out VocalVaultViews views, out int lockMask, out IDataVault lockedVault))
                return;

            try
            {
                long bankByteLength = Volatile.Read(ref _bankByteLength);
                if (bankByteLength <= 0L || bankByteLength > views.MockBankBytes.Length)
                    return;

                byte* bank = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(views.MockBankBytes);
                bool startedCue = false;
                float safeVesselCareTone01 = ResolveSafeVesselCareTone01(vesselCareTone01);
                float vesselCarePlaybackScalar = ResolveVesselCarePlaybackScalar(safeVesselCareTone01);

                ReadOnlySpan<PlayVoiceOverSignal> playVoiceOverSignals = SignalBus<PlayVoiceOverSignal>.GetFrameSnapshot();
                for (int i = 0; i < playVoiceOverSignals.Length; i++)
                {
                    PlayVoiceOverSignal signal = playVoiceOverSignals[i];
                    if (!TryBuildVocalCueFromPlayVoiceOver(in signal, out VocalCueSignal cue))
                        continue;

                    bool startedPlayVoiceOverCue = TryStartVocalCue(
                        ref views,
                        bank,
                        bankByteLength,
                        in cue,
                        vesselCarePlaybackScalar);
                    if (startedPlayVoiceOverCue)
                        TryPublishPlayVoiceOverSubtitleCue(in signal, ref views);

                    startedCue |= startedPlayVoiceOverCue;
                }

                ReadOnlySpan<VocalCueSignal> signals = SignalBus<VocalCueSignal>.GetFrameSnapshot();
                for (int i = 0; i < signals.Length; i++)
                {
                    startedCue |= TryStartVocalCue(
                        ref views,
                        bank,
                        bankByteLength,
                        in signals[i],
                        vesselCarePlaybackScalar);
                }

                if (startedCue)
                {
                    _lastAppliedVesselCareTone01 = safeVesselCareTone01;
                }
                else
                {
                    ApplyVesselCareToneToActivePlayback(ref views, _lastAppliedVesselCareTone01, safeVesselCareTone01);
                    _lastAppliedVesselCareTone01 = safeVesselCareTone01;
                }
            }
            finally
            {
                ReleaseVocalMutationGuardScope(lockedVault, lockMask);
            }
        }

        private bool TryStartVocalCue(
            ref VocalVaultViews views,
            byte* bank,
            long bankByteLength,
            in VocalCueSignal signal,
            float vesselCarePlaybackScalar)
        {
            if (signal.PhraseHashID == 0u)
                return false;

            VocalStateDTO current = views.State[0];
            VocalCodecStateDTO currentCodec = views.Codec[0];
            bool isPlaying = (current.Flags & VocalBankConstants.StateFlagPlaying) != 0u;
            bool vwsPreempted = (signal.Flags & VwsPreemptedFlag) != 0u;
            if (isPlaying && signal.Priority < currentCodec.Priority && !vwsPreempted)
                return false;

            uint playbackPhraseHash = signal.PhraseHashID;
            bool usedCanonicalFallback = false;
            if (!VocalBankReader.TryFindRecord(bank, bankByteLength, signal.PhraseHashID, out VocalBankIndexRecordDTO record))
            {
                RecordVocalBankMiss(ref views, signal.PhraseHashID);
                if (!TryResolveCanonicalVocalWarningFallbackRecord(
                        bank,
                        bankByteLength,
                        signal.PhraseHashID,
                        out record,
                        out playbackPhraseHash))
                {
                    return false;
                }

                usedCanonicalFallback = true;
            }

            if (record.Codec == VocalBankConstants.CodecVorbis)
            {
                VocalDecodeCounters64 counters = views.Counters[0];
                counters.FaultCount++;
                counters.LastFaultFlags = VocalBankConstants.StateFlagVorbisUnsupported;
                counters.LastPhraseHashID = signal.PhraseHashID;
                views.Counters[0] = counters;
                return false;
            }

            VocalDialogueMetadataDTO metadata = default;
#if UNITY_EDITOR
            bool hasMetadata = TryFindMetadata(playbackPhraseHash, views.CsvMetadata, _csvMetadataCount, out metadata);
#else
            bool hasMetadata = false;
#endif
            VocalStateDTO next = default;
            next.PhraseHashID = signal.PhraseHashID;
            next.CurrentSampleIndex = 0u;
            next.TotalSamples = record.TotalSamples;
            next.PlaybackSpeed = math.clamp(FiniteOrFallback(signal.PlaybackSpeed, 1f) * vesselCarePlaybackScalar, 0.25f, 2f);
            next.VolumeScalar = math.saturate(FiniteOrFallback(signal.VolumeScalar, 1f));
            uint nextFlags = VocalBankConstants.StateFlagPlaying | (isPlaying ? VocalBankConstants.StateFlagInterrupted : 0u);
            if (usedCanonicalFallback)
                nextFlags |= VocalBankConstants.StateFlagBankMiss;
            next.Flags = nextFlags;
            next.DuckingEnvelope01 = math.saturate(FiniteOrFallback(current.DuckingEnvelope01, 0f));
            next.SpeakerFloodDistortion01 = math.saturate(FiniteOrFallback(signal.RadioDistortion01, 0f));
            views.State[0] = next;

            VocalCodecStateDTO codec = views.Codec[0];
            codec.PayloadOffset = record.ByteOffset;
            codec.PayloadByteLength = record.ByteLength;
            codec.SampleRate = record.SampleRate;
            codec.Priority = signal.Priority != 0 ? signal.Priority : (hasMetadata ? metadata.Priority : record.Priority);
            float csvRadio = hasMetadata ? metadata.RadioDistortion01 : 0f;
            codec.RadioDistortion01 = math.saturate(math.max(math.max(record.RadioDistortionByte / 255f, csvRadio), FiniteOrFallback(signal.RadioDistortion01, 0f)));
            codec.QualityWeight01 = ResolveEffectiveQualityWeight();
            codec.SpatialGain = ResolveSpatialGain(in signal);
            codec.Codec = record.Codec;
            codec.ActivePhraseHashID = 0u;
            codec.FaultFlags = usedCanonicalFallback ? VocalBankConstants.StateFlagBankMiss : 0u;
            views.Codec[0] = codec;
            return true;
        }

        private bool TryBuildVocalCueFromPlayVoiceOver(in PlayVoiceOverSignal signal, out VocalCueSignal cue)
        {
            cue = default;
            if (signal.VoiceHash == 0u)
            {
                ReportPlayVoiceOverSignalMiss(in signal);
                return false;
            }

            cue.PhraseHashID = signal.VoiceHash;
            cue.Priority = DefaultPlayVoiceOverPriority;
            cue.VolumeScalar = 1f;
            cue.PlaybackSpeed = 1f;
            cue.RadioDistortion01 = 0f;
            cue.SpatialBlend01 = 0f;
            cue.Flags = 0u;
            _lastPlayVoiceOverTextHash = signal.TextHash;
            _lastPlayVoiceOverVoiceHash = signal.VoiceHash;
            _playVoiceOverSignalConsumedCount++;
            return true;
        }

        private void ReportPlayVoiceOverSignalMiss(in PlayVoiceOverSignal signal)
        {
            _playVoiceOverSignalMissCount++;
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_lastPlayVoiceOverSignalMissTelemetryFrame == frame)
                return;

            _lastPlayVoiceOverSignalMissTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                PlayVoiceOverSignalMissWarningHash,
                PlayVoiceOverSignalContextHash ^ signal.TextHash ^ signal.VoiceHash,
                math.max(1, _playVoiceOverSignalMissCount));
        }

        private void ClearPlayVoiceOverSignalDiagnostics()
        {
            _lastPlayVoiceOverTextHash = 0u;
            _lastPlayVoiceOverVoiceHash = 0u;
            _playVoiceOverSignalConsumedCount = 0;
            _playVoiceOverSignalMissCount = 0;
            _lastPlayVoiceOverSignalMissTelemetryFrame = -1;
            _vocalBankMissTelemetryCount = 0;
            _lastVocalBankMissTelemetryFrame = -1;
            _playVoiceOverSubtitleCuePublishedCount = 0;
            _playVoiceOverSubtitleCueDropCount = 0;
            _lastPlayVoiceOverSubtitleDropTelemetryFrame = -1;
        }

        private bool TryPublishPlayVoiceOverSubtitleCue(in PlayVoiceOverSignal signal, ref VocalVaultViews views)
        {
            if (signal.TextHash == 0u)
                return false;

            SubtitleCueSignal subtitle = default;
            subtitle.TokenHash = signal.TextHash;
            subtitle.SourceHash = PlayVoiceOverSubtitleSourceHash;
            subtitle.StartAudioFrame = 0u;
            subtitle.DurationMilliseconds = ResolveActiveVocalSubtitleDurationMilliseconds(ref views);
            subtitle.Priority = DefaultPlayVoiceOverPriority;
            subtitle.Flags = 0;
            if (SignalBus<SubtitleCueSignal>.TryPushTracked(in subtitle, ref _playVoiceOverSubtitleCueDropCount))
            {
                _playVoiceOverSubtitleCuePublishedCount++;
                return true;
            }

            ReportPlayVoiceOverSubtitleDrop(in signal);
            return false;
        }

        private void ReportPlayVoiceOverSubtitleDrop(in PlayVoiceOverSignal signal)
        {
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_lastPlayVoiceOverSubtitleDropTelemetryFrame == frame)
                return;

            _lastPlayVoiceOverSubtitleDropTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                PlayVoiceOverSubtitleDropWarningHash,
                PlayVoiceOverSubtitleContextHash ^ signal.TextHash ^ signal.VoiceHash,
                math.max(1, _playVoiceOverSubtitleCueDropCount));
        }

        private static ushort ResolveActiveVocalSubtitleDurationMilliseconds(ref VocalVaultViews views)
        {
            if (!views.State.IsCreated ||
                !views.Codec.IsCreated ||
                views.State.Length <= 0 ||
                views.Codec.Length <= 0)
            {
                return DefaultPlayVoiceOverSubtitleDurationMilliseconds;
            }

            VocalStateDTO state = views.State[0];
            VocalCodecStateDTO codec = views.Codec[0];
            if (state.TotalSamples == 0u || codec.SampleRate == 0u)
                return DefaultPlayVoiceOverSubtitleDurationMilliseconds;

            float durationSeconds = state.TotalSamples * math.rcp((float)math.max(1u, codec.SampleRate));
            return ResolveSubtitleDurationMilliseconds(durationSeconds);
        }

        private static ushort ResolveSubtitleDurationMilliseconds(float durationSeconds)
        {
            float safeSeconds = math.max(0.5f, math.select(0.5f, durationSeconds, math.isfinite(durationSeconds)));
            float milliseconds = math.clamp(safeSeconds * 1000f, 1f, ushort.MaxValue);
            return (ushort)math.round(milliseconds);
        }

        private void BindVesselTelemetryHandleCold()
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                !vault.TryGetGenerationHandle<VesselTelemetryEntry>(
                    SubmarineBallastBufferIds.VesselTelemetry,
                    out _vesselTelemetryHandle))
            {
                _vesselTelemetryHandle = default;
            }
        }

        private static bool TryResolveCanonicalVocalWarningFallbackRecord(
            byte* bank,
            long bankByteLength,
            uint requestedPhraseHash,
            out VocalBankIndexRecordDTO record,
            out uint playbackPhraseHash)
        {
            playbackPhraseHash = requestedPhraseHash;
            if (!IsCanonicalVocalWarningPhraseHash(requestedPhraseHash) ||
                requestedPhraseHash == DefaultMockPhraseHash ||
                !VocalBankReader.TryFindRecord(bank, bankByteLength, DefaultMockPhraseHash, out record))
            {
                record = default;
                return false;
            }

            playbackPhraseHash = DefaultMockPhraseHash;
            return true;
        }

        private static bool IsCanonicalVocalWarningPhraseHash(uint phraseHash)
        {
            switch (phraseHash)
            {
                case VocalWarningHashes.CrushDepth:
                case VocalWarningHashes.HullBreach:
                case VocalWarningHashes.HullTempCritical:
                case VocalWarningHashes.OxygenLow:
                case VocalWarningHashes.Radiation:
                case VocalWarningHashes.PowerLow:
                case VocalWarningHashes.Toxicity:
                    return true;
                default:
                    return false;
            }
        }

        private void RecordVocalBankMiss(ref VocalVaultViews views, uint requestedPhraseHash)
        {
            ReportVocalBankMiss(requestedPhraseHash);
            if (!views.Counters.IsCreated || views.Counters.Length <= 0)
                return;

            VocalDecodeCounters64 counters = views.Counters[0];
            counters.MissCount++;
            counters.LastFaultFlags = VocalBankConstants.StateFlagBankMiss;
            counters.LastPhraseHashID = requestedPhraseHash;
            views.Counters[0] = counters;
        }

        private void ReportVocalBankMiss(uint requestedPhraseHash)
        {
            _vocalBankMissTelemetryCount++;
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_lastVocalBankMissTelemetryFrame == frame)
                return;

            _lastVocalBankMissTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                VocalBankMissWarningHash,
                VocalBankMissContextHash ^ requestedPhraseHash,
                math.max(1, _vocalBankMissTelemetryCount));
        }

        private void RefreshVesselTelemetryHandleIfMissing(uint frame)
        {
            if (IsVehiclesPhysicsVaultHandle(in _vesselTelemetryHandle, SubmarineBallastBufferIds.VesselTelemetry) ||
                (frame & VesselTelemetryHandleRetryMask) != 0u)
            {
                return;
            }

            BindVesselTelemetryHandleCold();
        }

        private float ReadVesselCareTone01()
        {
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return ResolveSafeVesselCareTone01(_vesselCareTone01);

            if (!IsVehiclesPhysicsVaultHandle(in _vesselTelemetryHandle, SubmarineBallastBufferIds.VesselTelemetry) ||
                !vault.TryReadOnlyHandle(in _vesselTelemetryHandle, out NativeArray<VesselTelemetryEntry>.ReadOnly vesselTelemetry) ||
                !vesselTelemetry.IsCreated ||
                vesselTelemetry.Length <= 0)
            {
                return ResolveSafeVesselCareTone01(_vesselCareTone01);
            }

            VesselTelemetryEntry entry = vesselTelemetry[0];
            return VesselTelemetryEntry.ResolveToneWeight01(entry.TotalCareActionsCount);
        }

        private static void ApplyVesselCareToneToActivePlayback(ref VocalVaultViews views, float previousTone01, float nextTone01)
        {
            if (!views.State.IsCreated || views.State.Length <= 0)
                return;

            VocalStateDTO state = views.State[0];
            if ((state.Flags & VocalBankConstants.StateFlagPlaying) == 0u)
                return;

            float previousScalar = ResolveVesselCarePlaybackScalar(previousTone01);
            float nextScalar = ResolveVesselCarePlaybackScalar(nextTone01);
            if (math.abs(previousScalar - nextScalar) <= 0.000001f)
                return;

            float basePlaybackSpeed = FiniteOrFallback(state.PlaybackSpeed, 1f) / previousScalar;
            state.PlaybackSpeed = math.clamp(basePlaybackSpeed * nextScalar, 0.25f, 2f);
            views.State[0] = state;
        }

        private static float ResolveSafeVesselCareTone01(float vesselCareTone01)
        {
            return math.saturate(math.select(0f, vesselCareTone01, math.isfinite(vesselCareTone01)));
        }

        private static float ResolveVesselCarePlaybackScalar(float vesselCareTone01)
        {
            return math.lerp(VesselCareColdPlaybackSpeed, VesselCareWarmPlaybackSpeed, ResolveSafeVesselCareTone01(vesselCareTone01));
        }

        private float ResolveEffectiveQualityWeight()
        {
            float quality = math.saturate(FiniteOrFallback(_cachedGlobalQualityWeight, 1f));
            float bias = math.saturate(FiniteOrFallback(_mockQualityBias01, 1f));
            return math.saturate(quality * math.lerp(0.35f, 1f, bias));
        }

        private static float ResolveSpatialGain(in VocalCueSignal signal)
        {
            float blend = math.saturate(FiniteOrFallback(signal.SpatialBlend01, 0f));
            if (blend <= 0.0001f)
                return 1f;

            if (!TryResolveSourceDistanceSq(in signal, out float distanceSq))
                return 1f;

            float inverse = 1f / math.max(1f, distanceSq * 0.0008f);
            float attenuated = math.saturate(inverse);
            return math.lerp(1f, attenuated, blend);
        }

        private static bool TryResolveSourceDistanceSq(in VocalCueSignal signal, out float distanceSq)
        {
            distanceSq = 1f;
            float3 local = new float3(signal.SourceAupLocalX, signal.SourceAupLocalY, signal.SourceAupLocalZ);
            if (!math.all(math.isfinite(local)))
                return false;

            bool hasGrid = signal.SourceAupGridX != 0L || signal.SourceAupGridY != 0L || signal.SourceAupGridZ != 0L;
            bool hasLocal = math.lengthsq(local) > 0.000001f;
            if (!hasGrid && !hasLocal)
                return false;

            AbsoluteUniversePosition sourceAup = new AbsoluteUniversePosition
            {
                GridX = signal.SourceAupGridX,
                GridY = signal.SourceAupGridY,
                GridZ = signal.SourceAupGridZ,
                LocalX = signal.SourceAupLocalX,
                LocalY = signal.SourceAupLocalY,
                LocalZ = signal.SourceAupLocalZ
            };

            AbsoluteUniversePosition listenerAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!sourceAup.IsFinite() || !listenerAup.IsFinite())
                return false;

            double3 delta = AbsoluteUniversePosition.DeltaMetersClamped(in sourceAup, in listenerAup);
            if (!math.all(math.isfinite(delta)))
                return false;

            distanceSq = math.max(1f, (float)math.min(1000000000.0, math.lengthsq(delta)));
            return math.isfinite(distanceSq);
        }

        private void CacheDataVaultCold()
        {
            RebindDataVaultCold(GlobalRegistry.DataVault);
        }

        private void RebindDataVaultCold(IDataVault nextVault)
        {
            if (ReferenceEquals(_dataVault, nextVault))
                return;

            if (_dataVault != null)
                DisposeVaultStorage();

            _dataVault = nextVault;
            _vesselTelemetryHandle = default;
            _lastAppliedVesselCareTone01 = 0f;
            BindVesselTelemetryHandleCold();
        }

        private void EnsureVaultStorage()
        {
            IDataVault vault = _dataVault;
            if (Volatile.Read(ref _nativeAllocated) != 0 && AreVaultViewsResolvable(vault))
                return;

            if (vault == null)
                return;

            _stateHandle = vault.EnsureGenerationHandle<VocalStateDTO>(BufferID.AudioVocalSynthesisState, 1, VaultOwner, NativeArrayOptions.UninitializedMemory);
            _codecHandle = vault.EnsureGenerationHandle<VocalCodecStateDTO>(BufferID.AudioVocalSynthesisCodecState, 1, VaultOwner, NativeArrayOptions.UninitializedMemory);
            _telemetryHandle = vault.EnsureGenerationHandle<VocalTelemetryEntryDTO>(BufferID.AudioVocalSynthesisTelemetry, TelemetryCapacity, VaultOwner, NativeArrayOptions.UninitializedMemory);
            _countersHandle = vault.EnsureGenerationHandle<VocalDecodeCounters64>(BufferID.AudioVocalSynthesisTelemetryCursor, 1, VaultOwner, NativeArrayOptions.UninitializedMemory);
            _waveformHandle = vault.EnsureGenerationHandle<float>(BufferID.AudioVocalSynthesisWaveform, WaveformCapacity, VaultOwner, NativeArrayOptions.UninitializedMemory);
            _mockBankBytesHandle = vault.EnsureGenerationHandle<byte>(BufferID.AudioVocalSynthesisMockBankBytes, MockBankByteCapacity, VaultOwner, NativeArrayOptions.UninitializedMemory);
            _mockRecordsHandle = vault.EnsureGenerationHandle<VocalBankIndexRecordDTO>(BufferID.AudioVocalSynthesisMockBankRecords, MockRecordCapacity, VaultOwner, NativeArrayOptions.UninitializedMemory);
#if UNITY_EDITOR
            _csvMetadataHandle = vault.EnsureGenerationHandle<VocalDialogueMetadataDTO>(BufferID.AudioVocalSynthesisCsvMetadata, CsvMetadataCapacity, VaultOwner, NativeArrayOptions.UninitializedMemory);
#endif

            if (!TryAcquireInitializeViews(vault, out VocalVaultViews views, out int lockMask, out IDataVault lockedVault))
            {
                DisposeVaultStorage(vault);
                return;
            }

            try
            {
                views.State[0] = default;
                views.Codec[0] = default;
                views.Counters[0] = default;
                if (views.Waveform.Length > 0)
                    views.Waveform[0] = 0f;
                if (views.MockRecords.Length > 0)
                    views.MockRecords[0] = default;
#if UNITY_EDITOR
                if (views.CsvMetadata.Length > 0)
                    views.CsvMetadata[0] = default;
#endif
                for (int i = 0; i < views.Telemetry.Length; i++)
                    views.Telemetry[i] = default;
#if UNITY_EDITOR
                _csvMetadataCount = 0;
#endif
                Volatile.Write(ref _nativeAllocated, 1);
            }
            finally
            {
                ReleaseVocalMutationGuardScope(lockedVault, lockMask);
            }
        }

        private bool AreVaultViewsResolvable(IDataVault vault)
        {
            return IsReadOnlyHandleResolvable(vault, in _stateHandle, BufferID.AudioVocalSynthesisState, 1) &&
                   IsReadOnlyHandleResolvable(vault, in _codecHandle, BufferID.AudioVocalSynthesisCodecState, 1) &&
                   IsReadOnlyHandleResolvable(vault, in _telemetryHandle, BufferID.AudioVocalSynthesisTelemetry, TelemetryCapacity) &&
                   IsReadOnlyHandleResolvable(vault, in _countersHandle, BufferID.AudioVocalSynthesisTelemetryCursor, 1) &&
                   IsReadOnlyHandleResolvable(vault, in _waveformHandle, BufferID.AudioVocalSynthesisWaveform, WaveformCapacity) &&
                   IsReadOnlyHandleResolvable(vault, in _mockBankBytesHandle, BufferID.AudioVocalSynthesisMockBankBytes, 1) &&
                   IsReadOnlyHandleResolvable(vault, in _mockRecordsHandle, BufferID.AudioVocalSynthesisMockBankRecords, MockRecordCapacity)
#if UNITY_EDITOR
                   && IsReadOnlyHandleResolvable(vault, in _csvMetadataHandle, BufferID.AudioVocalSynthesisCsvMetadata, CsvMetadataCapacity)
#endif
                   ;
        }

        private bool IsReadOnlyHandleResolvable<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            int minimumLength)
            where T : struct
        {
            return vault != null &&
                   IsVocalSynthesisVaultHandle(in handle, expectedBufferId) &&
                   vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly view) &&
                   view.Length >= minimumLength;
        }

        private void DisposeVaultStorage()
        {
            DisposeVaultStorage(_dataVault);
        }

        private void DisposeVaultStorage(IDataVault vault)
        {
            if (!TryBeginBankMutationCold())
                return;

            try
            {
                ForceReleaseVocalMutationGuard();
                ReleaseVaultBuffer(vault, ref _stateHandle, BufferID.AudioVocalSynthesisState);
                ReleaseVaultBuffer(vault, ref _codecHandle, BufferID.AudioVocalSynthesisCodecState);
                ReleaseVaultBuffer(vault, ref _telemetryHandle, BufferID.AudioVocalSynthesisTelemetry);
                ReleaseVaultBuffer(vault, ref _countersHandle, BufferID.AudioVocalSynthesisTelemetryCursor);
                ReleaseVaultBuffer(vault, ref _waveformHandle, BufferID.AudioVocalSynthesisWaveform);
                ReleaseVaultBuffer(vault, ref _mockBankBytesHandle, BufferID.AudioVocalSynthesisMockBankBytes);
                ReleaseVaultBuffer(vault, ref _mockRecordsHandle, BufferID.AudioVocalSynthesisMockBankRecords);
#if UNITY_EDITOR
                ReleaseVaultBuffer(vault, ref _csvMetadataHandle, BufferID.AudioVocalSynthesisCsvMetadata);
                ReleaseEditorCsvScratch();
#endif
                _bankByteLength = 0;
                Volatile.Write(ref _usingMockBank, 0);
                Volatile.Write(ref _nativeAllocated, 0);
            }
            finally
            {
                Interlocked.Exchange(ref _bankReleaseInProgress, 0);
            }
        }

        private static void ReleaseVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID expectedBufferId)
            where T : struct
        {
            if (vault != null && IsVocalSynthesisVaultHandle(in handle, expectedBufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static bool IsVocalSynthesisVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId)
            where T : struct
        {
            return handle.BufferID == (uint)expectedBufferId &&
                   handle.SystemID == (uint)VaultOwner &&
                   handle.Generation != 0u;
        }

        private static bool IsVehiclesPhysicsVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId)
            where T : struct
        {
            return handle.BufferID == (uint)expectedBufferId &&
                   handle.SystemID == (uint)SystemID.VehiclesPhysics &&
                   handle.Generation != 0u;
        }

        private void OpenOrGenerateBankCold()
        {
            if (Volatile.Read(ref _nativeAllocated) == 0)
                return;

            if (!TryBeginBankMutationCold())
                return;

            IDataVault vault = _dataVault;
            try
            {
                _bankByteLength = 0;
                Volatile.Write(ref _usingMockBank, 0);
                if (vault == null)
                    return;

                if (TryLoadBankIntoVaultCold(vault))
                    return;

                if (_useMockBankWhenFileMissing)
                {
                    _mockBankBytesHandle = vault.EnsureGenerationHandle<byte>(
                        BufferID.AudioVocalSynthesisMockBankBytes,
                        MockBankByteCapacity,
                        VaultOwner,
                        NativeArrayOptions.UninitializedMemory);

                    GenerateMockBankCold(vault);
                }
            }
            finally
            {
                Interlocked.Exchange(ref _bankReleaseInProgress, 0);
            }
        }

        private bool TryLoadBankIntoVaultCold(IDataVault vault)
        {
            string path = Path.Combine(Application.streamingAssetsPath, BankRelativePath);
            if (!File.Exists(path))
                return false;

            try
            {
                using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    long bankLength = stream.Length;
                    if (bankLength <= 0 || bankLength > int.MaxValue)
                        return false;

                    if (vault == null)
                        return false;

                    int requiredBytes = (int)bankLength;
                    _mockBankBytesHandle = vault.EnsureGenerationHandle<byte>(
                        BufferID.AudioVocalSynthesisMockBankBytes,
                        requiredBytes,
                        VaultOwner,
                        NativeArrayOptions.UninitializedMemory);

                    int lockMask = 0;
                    if (!TryAcquireLockedView(vault, in _mockBankBytesHandle, BufferID.AudioVocalSynthesisMockBankBytes, LockMockBankBytes, VocalBankBytesMutationGuardMask, ref lockMask, out NativeArray<byte> bankBytes))
                        return false;

                    try
                    {
                        if (bankBytes.Length < requiredBytes)
                            return false;

                        byte* target = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(bankBytes);
                        int bytesRead = 0;
                        while (bytesRead < requiredBytes)
                        {
                            ref byte spanStart = ref UnsafeUtility.AsRef<byte>(target + bytesRead);
                            Span<byte> span = MemoryMarshal.CreateSpan(ref spanStart, requiredBytes - bytesRead);
                            int read = stream.Read(span);
                            if (read <= 0)
                                break;
                            bytesRead += read;
                        }

                        if (bytesRead != requiredBytes ||
                            !VocalBankReader.TryReadHeader(target, requiredBytes, out _))
                        {
                            return false;
                        }

                        _bankByteLength = requiredBytes;
                        Volatile.Write(ref _usingMockBank, 0);
                        return true;
                    }
                    finally
                    {
                        ReleaseVocalMutationGuardScope(vault, lockMask);
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool TryBeginBankMutationCold()
        {
            if (Interlocked.CompareExchange(ref _bankReleaseInProgress, 1, 0) != 0)
            {
                Interlocked.Exchange(ref _dumpRequested, 1);
                return false;
            }

            SpinWait spin = default;
            int spinCount = 0;
            while (Volatile.Read(ref _audioCallbackInFlight) != 0 && spinCount < BankMutationSpinLimit)
            {
                spin.SpinOnce();
                spinCount++;
            }

            if (Volatile.Read(ref _audioCallbackInFlight) == 0)
                return true;

            Interlocked.Exchange(ref _bankReleaseInProgress, 0);
            Interlocked.Exchange(ref _dumpRequested, 1);
            return false;
        }

        private void ClearBankStateCold()
        {
            if (!TryBeginBankMutationCold())
                return;

            try
            {
                _bankByteLength = 0;
                Volatile.Write(ref _usingMockBank, 0);
            }
            finally
            {
                Interlocked.Exchange(ref _bankReleaseInProgress, 0);
            }
        }

        private void GenerateMockBankCold(IDataVault vault)
        {
            if (!TryAcquireBankBuildViews(vault, out VocalVaultViews views, out int lockMask, out IDataVault lockedVault))
                return;

            try
            {
                uint sampleRate = (uint)math.max(8000, AudioSettings.outputSampleRate);
                GenerateMockVocalBankJob job = default;
                job.BankBytes = views.MockBankBytes;
                job.Records = views.MockRecords;
                job.PhraseHashID = DefaultMockPhraseHash;
                job.SampleRate = sampleRate;
                job.TotalSamples = DefaultMockSamples;
                job.Execute();
                _bankByteLength = views.MockBankBytes.Length;
                Volatile.Write(ref _usingMockBank, 1);
            }
            finally
            {
                ReleaseVocalMutationGuardScope(lockedVault, lockMask);
            }
        }

        private static bool TryFindMetadata(
            uint hash,
            NativeArray<VocalDialogueMetadataDTO> metadataView,
            int metadataCount,
            out VocalDialogueMetadataDTO metadata)
        {
            metadata = default;
#if !UNITY_EDITOR
            return false;
#else
            if (!metadataView.IsCreated)
                return false;

            int count = math.clamp(metadataCount, 0, metadataView.Length);
            int lo = 0;
            int hi = count - 1;
            while (lo <= hi)
            {
                int mid = lo + ((hi - lo) >> 1);
                VocalDialogueMetadataDTO candidate = metadataView[mid];
                if (candidate.HashID == hash)
                {
                    metadata = candidate;
                    return true;
                }

                if (candidate.HashID < hash)
                    lo = mid + 1;
                else
                    hi = mid - 1;
            }

            return false;
#endif
        }

#if UNITY_EDITOR
        private void ReloadDialogueCsvMetadataCold()
        {
            IDataVault vault = _dataVault;
            if (!TryAcquireCsvViews(vault, out VocalVaultViews views, out int lockMask, out IDataVault lockedVault))
                return;

            try
            {
                string path = Path.Combine(Application.dataPath, "..", "Docs", "Audio", "dialogue_script.csv");
                if (!File.Exists(path))
                    return;

                if (!TryGetEditorCsvScratch(out byte* csvScratch, out int csvScratchCapacity) ||
                    !TryReadColdCsvExact(path, csvScratch, csvScratchCapacity, out int bytesRead))
                    return;

                ref byte csvStart = ref UnsafeUtility.AsRef<byte>(csvScratch);
                _csvMetadataCount = ParseDialogueCsv(MemoryMarshal.CreateReadOnlySpan(ref csvStart, bytesRead), views.CsvMetadata);
            }
            catch (Exception)
            {
            }
            finally
            {
                ReleaseVocalMutationGuardScope(lockedVault, lockMask);
            }
        }

        private bool TryGetEditorCsvScratch(out byte* scratch, out int capacity)
        {
            if (_editorCsvScratch == null || _editorCsvScratchCapacity != EditorCsvScratchBytes)
            {
                ReleaseEditorCsvScratch();
                _editorCsvScratch = (byte*)UnsafeUtility.Malloc(EditorCsvScratchBytes, 16, Allocator.Persistent);
                try
                {
                    if (_editorCsvScratch == null)
                        throw new InvalidOperationException("Vocal bank editor CSV scratch allocation failed.");

                    _editorCsvScratchSentinelId = NativeMemorySentinel.RegisterPointer(
                        _editorCsvScratch,
                        EditorCsvScratchBytes,
                        nameof(VocalBankPlaybackRuntime),
                        EditorCsvScratchLabel,
                        NativeAllocationLifetime.Session);
                    if (_editorCsvScratchSentinelId <= 0)
                        throw new InvalidOperationException("NativeMemorySentinel rejected vocal bank editor CSV scratch registration.");

                    _editorCsvScratchCapacity = EditorCsvScratchBytes;
                }
                catch (Exception exception)
                {
                    try
                    {
                        ReleaseEditorCsvScratch();
                    }
                    catch (Exception releaseException)
                    {
                        throw new AggregateException("Vocal bank editor CSV scratch allocation cleanup failed.", exception, releaseException);
                    }

                    throw;
                }
            }

            scratch = _editorCsvScratch;
            capacity = _editorCsvScratchCapacity;
            return scratch != null && capacity > 0;
        }

        private void ReleaseEditorCsvScratch()
        {
            Exception firstException = null;
            bool released = _editorCsvScratch == null;

            if (_editorCsvScratch != null)
            {
                try
                {
                    UnsafeUtility.Free(_editorCsvScratch, Allocator.Persistent);
                    _editorCsvScratch = null;
                    _editorCsvScratchCapacity = 0;
                    released = true;
                }
                catch (Exception exception)
                {
                    firstException = exception;
                }
            }

            if (released)
            {
                _editorCsvScratchCapacity = 0;
                if (_editorCsvScratchSentinelId > 0)
                {
                    try
                    {
                        NativeMemorySentinel.Unregister(_editorCsvScratchSentinelId);
                        _editorCsvScratchSentinelId = 0;
                    }
                    catch (Exception exception)
                    {
                        if (firstException == null)
                            firstException = exception;
                    }
                }
            }

            if (firstException != null)
                throw firstException;
        }

        private static bool TryReadColdCsvExact(string path, byte* destination, int capacity, out int bytesRead)
        {
            bytesRead = 0;
            if (string.IsNullOrEmpty(path) || destination == null || capacity <= 0)
                return false;

            using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                long length = stream.Length;
                if (length <= 0L || length > capacity || length > int.MaxValue)
                    return false;

                int requiredBytes = (int)length;
                while (bytesRead < requiredBytes)
                {
                    ref byte spanStart = ref UnsafeUtility.AsRef<byte>(destination + bytesRead);
                    Span<byte> span = MemoryMarshal.CreateSpan(ref spanStart, requiredBytes - bytesRead);
                    int read = stream.Read(span);
                    if (read <= 0)
                        break;

                    bytesRead += read;
                }

                return bytesRead == requiredBytes;
            }
        }

        private static int ParseDialogueCsv(ReadOnlySpan<byte> csv, NativeArray<VocalDialogueMetadataDTO> metadata)
        {
            if (csv.Length <= 0 || !metadata.IsCreated || metadata.Length <= 0)
                return 0;

            int count = 0;
            int lineStart = 0;
            int lineIndex = 0;
            for (int i = 0; i <= csv.Length; i++)
            {
                bool atEnd = i == csv.Length;
                if (!atEnd && csv[i] != (byte)'\n')
                    continue;

                int lineEnd = i;
                if (lineEnd > lineStart && csv[lineEnd - 1] == (byte)'\r')
                    lineEnd--;

                if (lineEnd > lineStart && lineIndex > 0)
                    TryParseMetadataLine(csv.Slice(lineStart, lineEnd - lineStart), metadata, ref count);

                lineStart = i + 1;
                lineIndex++;
            }

            return count;
        }

        private static void TryParseMetadataLine(ReadOnlySpan<byte> line, NativeArray<VocalDialogueMetadataDTO> metadata, ref int count)
        {
            Span<int> starts = stackalloc int[4];
            Span<int> lengths = stackalloc int[4];
            int column = 0;
            int start = 0;
            bool quoted = false;
            for (int i = 0; i <= line.Length && column < 4; i++)
            {
                bool atEnd = i == line.Length;
                byte b = atEnd ? (byte)',' : line[i];
                if (!atEnd && b == (byte)'"')
                {
                    quoted = !quoted;
                    continue;
                }

                if (atEnd || (b == (byte)',' && !quoted))
                {
                    starts[column] = TrimStart(line, start, i);
                    lengths[column] = TrimLength(line, starts[column], i);
                    column++;
                    start = i + 1;
                }
            }

            if (column < 4 || count >= metadata.Length)
                return;

            ReadOnlySpan<byte> id = line.Slice(starts[0], lengths[0]);
            if (id.Length <= 0)
                return;

            VocalDialogueMetadataDTO row = default;
            row.HashID = VocalBankReader.Fnv1A(id);
            row.Priority = ParseInt(line.Slice(starts[2], lengths[2]));
            row.RadioDistortion01 = math.saturate(ParseFloat01(line.Slice(starts[3], lengths[3])));
            InsertMetadataSorted(metadata, ref count, in row);
        }

        private static void InsertMetadataSorted(NativeArray<VocalDialogueMetadataDTO> metadata, ref int count, in VocalDialogueMetadataDTO row)
        {
            int capacity = metadata.Length;
            if (capacity <= 0)
                return;

            int insert = math.clamp(count, 0, capacity - 1);
            for (int i = 0; i < count; i++)
            {
                if (metadata[i].HashID == row.HashID)
                {
                    metadata[i] = row;
                    return;
                }

                if (metadata[i].HashID > row.HashID)
                {
                    insert = i;
                    break;
                }
            }

            int safeCount = math.min(count, capacity - 1);
            for (int i = safeCount; i > insert; i--)
                metadata[i] = metadata[i - 1];

            metadata[insert] = row;
            count = math.min(count + 1, capacity);
        }

        private static int TrimStart(ReadOnlySpan<byte> line, int start, int end)
        {
            int s = start;
            while (s < end && (line[s] == (byte)' ' || line[s] == (byte)'\t' || line[s] == (byte)'"'))
                s++;
            return s;
        }

        private static int TrimLength(ReadOnlySpan<byte> line, int start, int end)
        {
            int e = end;
            while (e > start && (line[e - 1] == (byte)' ' || line[e - 1] == (byte)'\t' || line[e - 1] == (byte)'"'))
                e--;
            return math.max(0, e - start);
        }

        private static int ParseInt(ReadOnlySpan<byte> bytes)
        {
            int sign = 1;
            int value = 0;
            int i = 0;
            if (bytes.Length > 0 && bytes[0] == (byte)'-')
            {
                sign = -1;
                i = 1;
            }

            for (; i < bytes.Length; i++)
            {
                byte b = bytes[i];
                if (b < (byte)'0' || b > (byte)'9')
                    break;
                value = value * 10 + (b - (byte)'0');
            }

            return value * sign;
        }

        private static float ParseFloat01(ReadOnlySpan<byte> bytes)
        {
            float value = 0f;
            float scale = 0.1f;
            bool fraction = false;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte b = bytes[i];
                if (b == (byte)'.')
                {
                    fraction = true;
                    continue;
                }

                if (b < (byte)'0' || b > (byte)'9')
                    break;

                int digit = b - (byte)'0';
                if (fraction)
                {
                    value += digit * scale;
                    scale *= 0.1f;
                }
                else
                {
                    value = value * 10f + digit;
                }
            }

            return value;
        }
#endif

        private void RefreshGlobalQualitySnapshotCold()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            _cachedGlobalQualityWeight = math.saturate(math.select(1f, quality, math.isfinite(quality)));
        }

        private void UnregisterRuntime()
        {
            if (ReferenceEquals(_activeInstance, this))
                _activeInstance = null;
            ClearPlayVoiceOverSignalDiagnostics();
            if (Interlocked.Exchange(ref _registeredHotSwap, 0) != 0)
                GlobalRegistry.TryUnregisterHotSwapListener(this);
            if (Interlocked.Exchange(ref _registeredColdTick, 0) != 0)
                GlobalRegistry.UnregisterColdTickable(this, PriorityLayer.Core);
            if (Interlocked.Exchange(ref _registeredUpdate, 0) != 0)
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
        }

        private void DumpBlackboxCold()
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                !vault.TryReadOnlyHandle(in _telemetryHandle, out NativeArray<VocalTelemetryEntryDTO>.ReadOnly telemetryView) ||
                !vault.TryReadOnlyHandle(in _countersHandle, out NativeArray<VocalDecodeCounters64>.ReadOnly countersView) ||
                !telemetryView.IsCreated ||
                !countersView.IsCreated ||
                telemetryView.Length <= 0 ||
                countersView.Length <= 0)
                return;

            try
            {
                VocalDecodeCounters64 counters = countersView[0];
                int capacity = math.min(TelemetryCapacity, telemetryView.Length);
                int stride = UnsafeUtility.SizeOf<VocalTelemetryEntryDTO>();
                int byteCount = 32 + capacity * stride;
                NativeArray<byte> payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(VocalBankPlaybackRuntime),
                    "vocalBankBlackBoxPayload");
                try
                {
                    byte* target = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                    Span<byte> header = new Span<byte>(target, 32);
                    WriteUInt32(header, 0, 0x44563848u); // H8VD.
                    WriteUInt32(header, 4, 1u);
                    WriteUInt32(header, 8, (uint)capacity);
                    WriteUInt32(header, 12, (uint)stride);
                    WriteUInt32(header, 16, (uint)counters.TelemetryCursor);
                    WriteUInt32(header, 20, counters.LastFaultFlags);
                    WriteUInt32(header, 24, counters.LastPhraseHashID);
                    WriteUInt32(header, 28, _frameCounter);

                    int cursor = counters.TelemetryCursor;
                    int offset = 32;
                    for (int i = 0; i < capacity; i++)
                    {
                        int index = (cursor + i) % capacity;
                        VocalTelemetryEntryDTO entry = telemetryView[index];
                        byte* source = (byte*)UnsafeUtility.AddressOf(ref entry);
                        UnsafeUtility.MemCpy(target + offset, source, stride);
                        offset += stride;
                    }

                    NativeFaultDumpWriter.TryWriteAll(DumpRelativePath, payload, byteCount);
                }
                finally
                {
                    NativeFaultDumpWriter.DisposeTransientPayload(
                        ref payload,
                        nameof(VocalBankPlaybackRuntime),
                        "vocalBankBlackBoxPayload");
                }
            }
            catch (Exception)
            {
            }
        }

        private static float FiniteOrFallback(float value, float fallback)
        {
            return math.select(fallback, value, math.isfinite(value));
        }

        private static void ZeroManagedAudioBuffer(float[] data, int start, int count)
        {
            if (data == null || count <= 0)
                return;

            int end = math.min(data.Length, start + count);
            for (int i = math.max(0, start); i < end; i++)
                data[i] = 0f;
        }

        private static void WriteUInt32(Span<byte> target, int offset, uint value)
        {
            target[offset] = (byte)value;
            target[offset + 1] = (byte)(value >> 8);
            target[offset + 2] = (byte)(value >> 16);
            target[offset + 3] = (byte)(value >> 24);
        }
    }
}
