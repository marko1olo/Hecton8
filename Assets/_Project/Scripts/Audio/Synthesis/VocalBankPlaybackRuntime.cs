using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

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
        public NativeArray<byte> CsvScratch;
#endif
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-3890)]
    [AddComponentMenu("Hecton8/Audio/Vocal Bank Playback Runtime")]
    public sealed unsafe class VocalBankPlaybackRuntime : MonoBehaviour, IColdTickable, IUpdatable, ISlowTickable, IGlobalRegistryHotSwapListener
    {
        private const SystemID VaultOwner = SystemID.AudioVocalSynthesis;
        private const int TelemetryCapacity = 300;
        private const int WaveformCapacity = 2048;
        private const int MockBankByteCapacity = 196608;
        private const int MockRecordCapacity = 1;
#if UNITY_EDITOR
        private const int CsvMetadataCapacity = 8192;
        private const int CsvScratchBytes = 1048576;
#endif
        private const int DefaultMockSamples = 32000;
        private const uint DefaultMockPhraseHash = 0x05203E88u; // FNV1a("VO_SHINOBU_MOCK").
        private const uint VocalCueLaneHash = 0xC001260u;
        private const uint VwsPreemptedFlag = 1u << 5;
        private const float DspDumpThresholdMicroseconds = 1000f;
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
        private const int LockCsvScratch = 1 << 8;
#endif
        private static readonly ulong VocalMutationGuardMask =
            VocalMutationGuardBit(BufferID.AudioVocalSynthesisState) |
            VocalMutationGuardBit(BufferID.AudioVocalSynthesisCodecState) |
            VocalMutationGuardBit(BufferID.AudioVocalSynthesisTelemetry) |
            VocalMutationGuardBit(BufferID.AudioVocalSynthesisTelemetryCursor) |
            VocalMutationGuardBit(BufferID.AudioVocalSynthesisWaveform) |
            VocalMutationGuardBit(BufferID.AudioVocalSynthesisMockBankBytes) |
            VocalMutationGuardBit(BufferID.AudioVocalSynthesisMockBankRecords)
#if UNITY_EDITOR
            | VocalMutationGuardBit(BufferID.AudioVocalSynthesisCsvMetadata)
            | VocalMutationGuardBit(BufferID.AudioVocalSynthesisCsvScratch)
#endif
            ;

        private static VocalBankPlaybackRuntime _activeInstance;
        private static FunctionPointer<VocalDecodeDelegate> _decodeFunctionPointer;
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
#if UNITY_EDITOR
        private VaultGenerationHandle<VocalDialogueMetadataDTO> _csvMetadataHandle;
        private VaultGenerationHandle<byte> _csvScratchHandle;
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
        private IDataVault _vocalMutationGuardVault;
        private ulong _vocalMutationGuardMask;
        private int _vocalMutationGuardDepth;
#if UNITY_EDITOR
        private int _csvMetadataCount;
#endif
        private uint _frameCounter;
        private float _cachedGlobalQualityWeight = 1f;

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

            _decodeFunctionPointer = BurstCompiler.CompileFunctionPointer<VocalDecodeDelegate>(VocalDecodeKernel.DecodeIntoAudioBuffer);
            Volatile.Write(ref _decodePointerReady, 1);
        }

        private static void EnsureVocalCueLaneCold()
        {
            SignalBus<VocalCueSignal>.ConfigureCacheLineCritical(64, 64, 16, VocalCueLaneHash);
            SignalBus<VocalCueSignal>.EnsureInitialized();
        }

        private void Awake()
        {
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

            DrainVocalCueSignals();
            if (Interlocked.Exchange(ref _dumpRequested, 0) != 0)
                DumpBlackboxCold();

            _ = deltaTime;
        }

        public void SlowTick()
        {
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

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (Volatile.Read(ref _bankReleaseInProgress) != 0 ||
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
                    _decodeFunctionPointer.Invoke(
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
                ReleaseVocalWriteLocks(lockedVault, lockMask);
                Interlocked.Decrement(ref _audioCallbackInFlight);
            }
        }

        private bool TryAcquireAudioCallbackViews(out VocalVaultViews views, out int lockMask, out IDataVault lockedVault)
        {
            views = default;
            lockMask = 0;
            lockedVault = _dataVault;
            if (lockedVault == null)
                return false;

            bool ownershipTransferred = false;
            try
            {
                if (!TryAcquireLockedView(lockedVault, in _stateHandle, BufferID.AudioVocalSynthesisState, LockState, ref lockMask, out views.State) ||
                    !TryAcquireLockedView(lockedVault, in _codecHandle, BufferID.AudioVocalSynthesisCodecState, LockCodec, ref lockMask, out views.Codec) ||
                    !TryAcquireLockedView(lockedVault, in _telemetryHandle, BufferID.AudioVocalSynthesisTelemetry, LockTelemetry, ref lockMask, out views.Telemetry) ||
                    !TryAcquireLockedView(lockedVault, in _countersHandle, BufferID.AudioVocalSynthesisTelemetryCursor, LockCounters, ref lockMask, out views.Counters) ||
                    !TryAcquireLockedView(lockedVault, in _waveformHandle, BufferID.AudioVocalSynthesisWaveform, LockWaveform, ref lockMask, out views.Waveform) ||
                    !TryAcquireLockedView(lockedVault, in _mockBankBytesHandle, BufferID.AudioVocalSynthesisMockBankBytes, LockMockBankBytes, ref lockMask, out views.MockBankBytes) ||
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
                    ReleaseVocalWriteLocks(lockedVault, lockMask);
                    views = default;
                    lockMask = 0;
                    lockedVault = null;
                }
            }
        }

        private bool TryAcquireControlViews(out VocalVaultViews views, out int lockMask, out IDataVault lockedVault)
        {
            views = default;
            lockMask = 0;
            lockedVault = _dataVault;
            if (lockedVault == null)
                return false;

            bool ownershipTransferred = false;
            try
            {
                if (!TryAcquireLockedView(lockedVault, in _stateHandle, BufferID.AudioVocalSynthesisState, LockState, ref lockMask, out views.State) ||
                    !TryAcquireLockedView(lockedVault, in _codecHandle, BufferID.AudioVocalSynthesisCodecState, LockCodec, ref lockMask, out views.Codec) ||
                    !TryAcquireLockedView(lockedVault, in _countersHandle, BufferID.AudioVocalSynthesisTelemetryCursor, LockCounters, ref lockMask, out views.Counters) ||
                    !TryAcquireLockedView(lockedVault, in _mockBankBytesHandle, BufferID.AudioVocalSynthesisMockBankBytes, LockMockBankBytes, ref lockMask, out views.MockBankBytes) ||
                    views.State.Length <= 0 ||
                    views.Codec.Length <= 0 ||
                    views.Counters.Length <= 0 ||
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
                    ReleaseVocalWriteLocks(lockedVault, lockMask);
                    views = default;
                    lockMask = 0;
                    lockedVault = null;
                }
            }
        }

        private bool TryAcquireBankBuildViews(out VocalVaultViews views, out int lockMask, out IDataVault lockedVault)
        {
            views = default;
            lockMask = 0;
            lockedVault = _dataVault;
            if (lockedVault == null)
                return false;

            bool ownershipTransferred = false;
            try
            {
                if (!TryAcquireLockedView(lockedVault, in _mockBankBytesHandle, BufferID.AudioVocalSynthesisMockBankBytes, LockMockBankBytes, ref lockMask, out views.MockBankBytes) ||
                    !TryAcquireLockedView(lockedVault, in _mockRecordsHandle, BufferID.AudioVocalSynthesisMockBankRecords, LockMockRecords, ref lockMask, out views.MockRecords) ||
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
                    ReleaseVocalWriteLocks(lockedVault, lockMask);
                    views = default;
                    lockMask = 0;
                    lockedVault = null;
                }
            }
        }

        private bool TryAcquireInitializeViews(out VocalVaultViews views, out int lockMask, out IDataVault lockedVault)
        {
            views = default;
            lockMask = 0;
            lockedVault = _dataVault;
            if (lockedVault == null)
                return false;

            bool ownershipTransferred = false;
            try
            {
                if (!TryAcquireLockedView(lockedVault, in _stateHandle, BufferID.AudioVocalSynthesisState, LockState, ref lockMask, out views.State) ||
                    !TryAcquireLockedView(lockedVault, in _codecHandle, BufferID.AudioVocalSynthesisCodecState, LockCodec, ref lockMask, out views.Codec) ||
                    !TryAcquireLockedView(lockedVault, in _telemetryHandle, BufferID.AudioVocalSynthesisTelemetry, LockTelemetry, ref lockMask, out views.Telemetry) ||
                    !TryAcquireLockedView(lockedVault, in _countersHandle, BufferID.AudioVocalSynthesisTelemetryCursor, LockCounters, ref lockMask, out views.Counters) ||
                    !TryAcquireLockedView(lockedVault, in _waveformHandle, BufferID.AudioVocalSynthesisWaveform, LockWaveform, ref lockMask, out views.Waveform) ||
                    !TryAcquireLockedView(lockedVault, in _mockRecordsHandle, BufferID.AudioVocalSynthesisMockBankRecords, LockMockRecords, ref lockMask, out views.MockRecords) ||
#if UNITY_EDITOR
                    !TryAcquireLockedView(lockedVault, in _csvMetadataHandle, BufferID.AudioVocalSynthesisCsvMetadata, LockCsvMetadata, ref lockMask, out views.CsvMetadata) ||
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
                    ReleaseVocalWriteLocks(lockedVault, lockMask);
                    views = default;
                    lockMask = 0;
                    lockedVault = null;
                }
            }
        }

#if UNITY_EDITOR
        private bool TryAcquireCsvViews(out VocalVaultViews views, out int lockMask, out IDataVault lockedVault)
        {
            views = default;
            lockMask = 0;
            lockedVault = _dataVault;
            if (lockedVault == null)
                return false;

            bool ownershipTransferred = false;
            try
            {
                if (!TryAcquireLockedView(lockedVault, in _csvMetadataHandle, BufferID.AudioVocalSynthesisCsvMetadata, LockCsvMetadata, ref lockMask, out views.CsvMetadata) ||
                    !TryAcquireLockedView(lockedVault, in _csvScratchHandle, BufferID.AudioVocalSynthesisCsvScratch, LockCsvScratch, ref lockMask, out views.CsvScratch) ||
                    views.CsvMetadata.Length <= 0 ||
                    views.CsvScratch.Length <= 0)
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
                    ReleaseVocalWriteLocks(lockedVault, lockMask);
                    views = default;
                    lockMask = 0;
                    lockedVault = null;
                }
            }
        }
#endif

        private static ulong VocalMutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        private bool TryAcquireVocalMutationGuard(IDataVault vault)
        {
            if (vault == null || VocalMutationGuardMask == 0UL)
                return false;

            if (_vocalMutationGuardDepth > 0)
            {
                if (!ReferenceEquals(_vocalMutationGuardVault, vault) ||
                    _vocalMutationGuardMask != VocalMutationGuardMask)
                {
                    return false;
                }

                _vocalMutationGuardDepth++;
                return true;
            }

            if (!vault.TryAcquireMutationGuard(VocalMutationGuardMask))
                return false;

            _vocalMutationGuardVault = vault;
            _vocalMutationGuardMask = VocalMutationGuardMask;
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
            ref int lockMask,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            bool acquiredGuard = false;
            if (lockMask == 0)
            {
                if (!TryAcquireVocalMutationGuard(vault))
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

        private void ReleaseVocalWriteLocks(int lockMask)
        {
            ReleaseVocalWriteLocks(_dataVault, lockMask);
        }

        private void ReleaseVocalWriteLocks(IDataVault vault, int lockMask)
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

        private void DrainVocalCueSignals()
        {
            if (!TryAcquireControlViews(out VocalVaultViews views, out int lockMask, out IDataVault lockedVault))
                return;

            try
            {
                long bankByteLength = Volatile.Read(ref _bankByteLength);
                if (bankByteLength <= 0L || bankByteLength > views.MockBankBytes.Length)
                    return;

                byte* bank = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(views.MockBankBytes);
                ReadOnlySpan<VocalCueSignal> signals = SignalBus<VocalCueSignal>.GetFrameSnapshot();
                for (int i = 0; i < signals.Length; i++)
                {
                    VocalCueSignal signal = signals[i];
                    if (signal.PhraseHashID == 0u)
                        continue;

                    VocalStateDTO current = views.State[0];
                    VocalCodecStateDTO currentCodec = views.Codec[0];
                    bool isPlaying = (current.Flags & VocalBankConstants.StateFlagPlaying) != 0u;
                    bool vwsPreempted = (signal.Flags & VwsPreemptedFlag) != 0u;
                    if (isPlaying && signal.Priority < currentCodec.Priority && !vwsPreempted)
                        continue;

                    if (!VocalBankReader.TryFindRecord(bank, bankByteLength, signal.PhraseHashID, out VocalBankIndexRecordDTO record))
                    {
                        VocalDecodeCounters64 counters = views.Counters[0];
                        counters.MissCount++;
                        counters.LastFaultFlags = VocalBankConstants.StateFlagBankMiss;
                        counters.LastPhraseHashID = signal.PhraseHashID;
                        views.Counters[0] = counters;
                        continue;
                    }

                    if (record.Codec == VocalBankConstants.CodecVorbis)
                    {
                        VocalDecodeCounters64 counters = views.Counters[0];
                        counters.FaultCount++;
                        counters.LastFaultFlags = VocalBankConstants.StateFlagVorbisUnsupported;
                        counters.LastPhraseHashID = signal.PhraseHashID;
                        views.Counters[0] = counters;
                        continue;
                    }

                    VocalDialogueMetadataDTO metadata = default;
                    bool hasMetadata = TryFindMetadata(signal.PhraseHashID, out metadata);
                    VocalStateDTO next = default;
                    next.PhraseHashID = signal.PhraseHashID;
                    next.CurrentSampleIndex = 0u;
                    next.TotalSamples = record.TotalSamples;
                    next.PlaybackSpeed = math.clamp(FiniteOrFallback(signal.PlaybackSpeed, 1f), 0.25f, 2f);
                    next.VolumeScalar = math.saturate(FiniteOrFallback(signal.VolumeScalar, 1f));
                    next.Flags = VocalBankConstants.StateFlagPlaying | (isPlaying ? VocalBankConstants.StateFlagInterrupted : 0u);
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
                    codec.FaultFlags = 0u;
                    views.Codec[0] = codec;
                }
            }
            finally
            {
                ReleaseVocalWriteLocks(lockedVault, lockMask);
            }
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

            float localX = signal.SourceAupLocalX;
            float localY = signal.SourceAupLocalY;
            float localZ = signal.SourceAupLocalZ;
            float distanceSq = math.max(1f, (localX * localX) + (localY * localY) + (localZ * localZ));
            float inverse = 1f / math.max(1f, distanceSq * 0.0008f);
            float attenuated = math.saturate(inverse);
            return math.lerp(1f, attenuated, blend);
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
        }

        private void EnsureVaultStorage()
        {
            IDataVault vault = _dataVault;
            if (Volatile.Read(ref _nativeAllocated) != 0 && AreVaultViewsResolvable())
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
            _csvScratchHandle = vault.EnsureGenerationHandle<byte>(BufferID.AudioVocalSynthesisCsvScratch, CsvScratchBytes, VaultOwner, NativeArrayOptions.UninitializedMemory);
#endif

            if (!TryAcquireInitializeViews(out VocalVaultViews views, out int lockMask, out IDataVault lockedVault))
            {
                DisposeVaultStorage();
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
                ReleaseVocalWriteLocks(lockedVault, lockMask);
            }
        }

        private bool AreVaultViewsResolvable()
        {
            return IsReadOnlyHandleResolvable(in _stateHandle, BufferID.AudioVocalSynthesisState, 1) &&
                   IsReadOnlyHandleResolvable(in _codecHandle, BufferID.AudioVocalSynthesisCodecState, 1) &&
                   IsReadOnlyHandleResolvable(in _telemetryHandle, BufferID.AudioVocalSynthesisTelemetry, TelemetryCapacity) &&
                   IsReadOnlyHandleResolvable(in _countersHandle, BufferID.AudioVocalSynthesisTelemetryCursor, 1) &&
                   IsReadOnlyHandleResolvable(in _waveformHandle, BufferID.AudioVocalSynthesisWaveform, WaveformCapacity) &&
                   IsReadOnlyHandleResolvable(in _mockBankBytesHandle, BufferID.AudioVocalSynthesisMockBankBytes, 1) &&
                   IsReadOnlyHandleResolvable(in _mockRecordsHandle, BufferID.AudioVocalSynthesisMockBankRecords, MockRecordCapacity)
#if UNITY_EDITOR
                   && IsReadOnlyHandleResolvable(in _csvMetadataHandle, BufferID.AudioVocalSynthesisCsvMetadata, CsvMetadataCapacity) &&
                   IsReadOnlyHandleResolvable(in _csvScratchHandle, BufferID.AudioVocalSynthesisCsvScratch, CsvScratchBytes)
#endif
                   ;
        }

        private bool IsReadOnlyHandleResolvable<T>(
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            int minimumLength)
            where T : struct
        {
            IDataVault vault = _dataVault;
            return vault != null &&
                   IsVocalSynthesisVaultHandle(in handle, expectedBufferId) &&
                   vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly view) &&
                   view.Length >= minimumLength;
        }

        private void DisposeVaultStorage()
        {
            BeginBankMutationCold();
            IDataVault vault = _dataVault;
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
                ReleaseVaultBuffer(vault, ref _csvScratchHandle, BufferID.AudioVocalSynthesisCsvScratch);
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

        private void OpenOrGenerateBankCold()
        {
            if (Volatile.Read(ref _nativeAllocated) == 0)
                return;

            BeginBankMutationCold();
            try
            {
                _bankByteLength = 0;
                Volatile.Write(ref _usingMockBank, 0);
                if (TryLoadBankIntoVaultCold())
                    return;

                if (_useMockBankWhenFileMissing)
                {
                    IDataVault vault = _dataVault;
                    if (vault != null)
                    {
                        _mockBankBytesHandle = vault.EnsureGenerationHandle<byte>(
                            BufferID.AudioVocalSynthesisMockBankBytes,
                            MockBankByteCapacity,
                            VaultOwner,
                            NativeArrayOptions.UninitializedMemory);
                    }

                    GenerateMockBankCold();
                }
            }
            finally
            {
                Interlocked.Exchange(ref _bankReleaseInProgress, 0);
            }
        }

        private bool TryLoadBankIntoVaultCold()
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

                    IDataVault vault = _dataVault;
                    if (vault == null)
                        return false;

                    int requiredBytes = (int)bankLength;
                    _mockBankBytesHandle = vault.EnsureGenerationHandle<byte>(
                        BufferID.AudioVocalSynthesisMockBankBytes,
                        requiredBytes,
                        VaultOwner,
                        NativeArrayOptions.UninitializedMemory);

                    int lockMask = 0;
                    if (!TryAcquireLockedView(vault, in _mockBankBytesHandle, BufferID.AudioVocalSynthesisMockBankBytes, LockMockBankBytes, ref lockMask, out NativeArray<byte> bankBytes))
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
                        ReleaseVocalWriteLocks(vault, lockMask);
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void BeginBankMutationCold()
        {
            Interlocked.Exchange(ref _bankReleaseInProgress, 1);
            SpinWait spin = default;
            while (Volatile.Read(ref _audioCallbackInFlight) != 0)
                spin.SpinOnce();
        }

        private void ClearBankStateCold()
        {
            BeginBankMutationCold();
            _bankByteLength = 0;
            Volatile.Write(ref _usingMockBank, 0);
            Interlocked.Exchange(ref _bankReleaseInProgress, 0);
        }

        private void GenerateMockBankCold()
        {
            if (!TryAcquireBankBuildViews(out VocalVaultViews views, out int lockMask, out IDataVault lockedVault))
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
                ReleaseVocalWriteLocks(lockedVault, lockMask);
            }
        }

        private bool TryFindMetadata(uint hash, out VocalDialogueMetadataDTO metadata)
        {
            metadata = default;
#if !UNITY_EDITOR
            return false;
#else
            IDataVault vault = _dataVault;
            if (vault == null ||
                !vault.TryReadOnlyHandle(in _csvMetadataHandle, out NativeArray<VocalDialogueMetadataDTO>.ReadOnly metadataView) ||
                !metadataView.IsCreated)
                return false;

            int count = math.clamp(_csvMetadataCount, 0, metadataView.Length);
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
            if (!TryAcquireCsvViews(out VocalVaultViews views, out int lockMask, out IDataVault lockedVault))
                return;

            try
            {
                string path = Path.Combine(Application.dataPath, "..", "Docs", "Audio", "dialogue_script.csv");
                if (!File.Exists(path))
                    return;

                int bytesRead = 0;
                int capacity = views.CsvScratch.Length;
                byte* scratch = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(views.CsvScratch);
                using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    while (bytesRead < capacity)
                    {
                        ref byte spanStart = ref UnsafeUtility.AsRef<byte>(scratch + bytesRead);
                        Span<byte> span = MemoryMarshal.CreateSpan(ref spanStart, capacity - bytesRead);
                        int read = stream.Read(span);
                        if (read <= 0)
                            break;
                        bytesRead += read;
                    }
                }

                ref byte csvStart = ref UnsafeUtility.AsRef<byte>(scratch);
                ReadOnlySpan<byte> csv = MemoryMarshal.CreateReadOnlySpan(ref csvStart, bytesRead);
                _csvMetadataCount = ParseDialogueCsv(csv, views.CsvMetadata);
            }
            catch (Exception)
            {
            }
            finally
            {
                ReleaseVocalWriteLocks(lockedVault, lockMask);
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
            if (Interlocked.Exchange(ref _registeredHotSwap, 0) != 0)
                GlobalRegistry.UnregisterHotSwapListener(this);
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
                string path = Path.Combine(Application.dataPath, "..", DumpRelativePath);
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    Span<byte> header = stackalloc byte[32];
                    WriteUInt32(header, 0, 0x44563848u); // H8VD.
                    WriteUInt32(header, 4, 1u);
                    int capacity = math.min(TelemetryCapacity, telemetryView.Length);
                    WriteUInt32(header, 8, (uint)capacity);
                    WriteUInt32(header, 12, (uint)UnsafeUtility.SizeOf<VocalTelemetryEntryDTO>());
                    WriteUInt32(header, 16, (uint)counters.TelemetryCursor);
                    WriteUInt32(header, 20, counters.LastFaultFlags);
                    WriteUInt32(header, 24, counters.LastPhraseHashID);
                    WriteUInt32(header, 28, _frameCounter);
                    for (int i = 0; i < header.Length; i++)
                        stream.WriteByte(header[i]);

                    int cursor = counters.TelemetryCursor;
                    for (int i = 0; i < capacity; i++)
                    {
                        int index = (cursor + i) % capacity;
                        VocalTelemetryEntryDTO entry = telemetryView[index];
                        byte* source = (byte*)UnsafeUtility.AddressOf(ref entry);
                        for (int b = 0; b < UnsafeUtility.SizeOf<VocalTelemetryEntryDTO>(); b++)
                            stream.WriteByte(source[b]);
                    }
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
