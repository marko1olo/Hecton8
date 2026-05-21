using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
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
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace Hecton8.Audio.Synthesis
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-3890)]
    [AddComponentMenu("Hecton8/Audio/Vocal Bank Playback Runtime")]
    public sealed unsafe class VocalBankPlaybackRuntime : MonoBehaviour, IUpdatable, ISlowTickable, IGlobalRegistryHotSwapListener
    {
        private const SystemID VaultOwner = SystemID.AudioVocalSynthesis;
        private const int TelemetryCapacity = 300;
        private const int WaveformCapacity = 2048;
        private const int MockBankByteCapacity = 196608;
        private const int MockRecordCapacity = 1;
        private const int CsvMetadataCapacity = 8192;
        private const int CsvScratchBytes = 1048576;
        private const int DefaultMockSamples = 32000;
        private const uint DefaultMockPhraseHash = 0x05203E88u; // FNV1a("VO_SHINOBU_MOCK").
        private const uint VocalCueLaneHash = 0xC001260u;
        private const float DspDumpThresholdMicroseconds = 1000f;
        private const string BankRelativePath = "Hecton8/Audio/vocal_banks.h8bin";
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_260.bin";

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
        private VaultGenerationHandle<VocalDialogueMetadataDTO> _csvMetadataHandle;
        private VaultGenerationHandle<byte> _csvScratchHandle;

        private VocalStateDTO* _statePtr;
        private VocalCodecStateDTO* _codecPtr;
        private VocalTelemetryEntryDTO* _telemetryPtr;
        private VocalDecodeCounters64* _countersPtr;
        private float* _waveformPtr;
        private byte* _mockBankPtr;
        private byte* _bankPtr;
        private long _bankByteLength;

        private MemoryMappedFile _mmf;
        private MemoryMappedViewAccessor _mmfAccessor;
        private byte* _mmfPointer;

        private int _nativeAllocated;
        private int _registeredUpdate;
        private int _registeredSlowTick;
        private int _registeredHotSwap;
        private int _usingMockBank;
        private int _dumpRequested;
        private int _audioCallbackInFlight;
        private int _bankReleaseInProgress;
        private int _csvMetadataCount;
        private uint _frameCounter;
        private float _cachedGlobalQualityWeight = 1f;

        private struct VocalVaultViews
        {
            public NativeArray<VocalStateDTO> State;
            public NativeArray<VocalCodecStateDTO> Codec;
            public NativeArray<VocalTelemetryEntryDTO> Telemetry;
            public NativeArray<VocalDecodeCounters64> Counters;
            public NativeArray<float> Waveform;
            public NativeArray<byte> MockBankBytes;
            public NativeArray<VocalBankIndexRecordDTO> MockRecords;
            public NativeArray<VocalDialogueMetadataDTO> CsvMetadata;
            public NativeArray<byte> CsvScratch;
        }

        public static bool TryGetActive(out VocalBankPlaybackRuntime runtime)
        {
            runtime = _activeInstance;
            return runtime != null && Volatile.Read(ref runtime._nativeAllocated) != 0;
        }

        public static bool TryGetEditorState(out VocalStateDTO state, out VocalCodecStateDTO codec)
        {
            state = default;
            codec = default;
            if (!TryGetActive(out VocalBankPlaybackRuntime runtime) ||
                runtime._statePtr == null ||
                runtime._codecPtr == null)
                return false;

            state = *runtime._statePtr;
            codec = *runtime._codecPtr;
            return true;
        }

        public static bool TryGetEditorTelemetry(int offsetFromNewest, out VocalTelemetryEntryDTO entry)
        {
            entry = default;
            if (!TryGetActive(out VocalBankPlaybackRuntime runtime) ||
                runtime._countersPtr == null ||
                runtime._telemetryPtr == null)
                return false;

            int capacity = TelemetryCapacity;
            int cursor = math.max(0, runtime._countersPtr->TelemetryCursor);
            int offset = math.clamp(offsetFromNewest, 0, capacity - 1);
            int index = cursor - 1 - offset;
            while (index < 0)
                index += capacity;

            entry = runtime._telemetryPtr[index % capacity];
            return true;
        }

        public static bool TryGetEditorWaveformSample(int newestOffset, out float sample)
        {
            sample = 0f;
            if (!TryGetActive(out VocalBankPlaybackRuntime runtime) ||
                runtime._countersPtr == null ||
                runtime._waveformPtr == null)
                return false;

            int cursor = math.max(0, runtime._countersPtr->WaveformCursor);
            int offset = math.clamp(newestOffset, 0, WaveformCapacity - 1);
            int index = cursor - 1 - offset;
            while (index < 0)
                index += WaveformCapacity;

            sample = runtime._waveformPtr[index % WaveformCapacity];
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

#if UNITY_2023_1_OR_NEWER
            VocalBankPlaybackRuntime existing = UnityEngine.Object.FindAnyObjectByType<VocalBankPlaybackRuntime>();
#else
            VocalBankPlaybackRuntime existing = UnityEngine.Object.FindObjectOfType<VocalBankPlaybackRuntime>();
#endif
            if (existing != null)
            {
                _activeInstance = existing;
                return;
            }

#if UNITY_2023_1_OR_NEWER
            AudioListener listener = UnityEngine.Object.FindAnyObjectByType<AudioListener>();
#else
            AudioListener listener = UnityEngine.Object.FindObjectOfType<AudioListener>();
#endif
            if (listener != null)
                listener.gameObject.AddComponent<VocalBankPlaybackRuntime>();
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
            ReloadDialogueCsvMetadataCold();
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
            if (GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Core))
                _registeredSlowTick = 1;
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
            ReleaseMmfCold();
            DisposeVaultStorage();
        }

        public void Tick(float deltaTime)
        {
            if (Volatile.Read(ref _nativeAllocated) == 0)
            {
                EnsureVaultStorage();
                if (Volatile.Read(ref _nativeAllocated) == 0)
                    return;
            }

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
            if (Volatile.Read(ref _nativeAllocated) == 0)
                EnsureVaultStorage();

            RefreshGlobalQualitySnapshotCold();
            if (_bankPtr == null || _bankByteLength <= 0)
                OpenOrGenerateBankCold();
#if UNITY_EDITOR
            ReloadDialogueCsvMetadataCold();
#endif
        }

        public void OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            IDataVault replacement = currentService as IDataVault;
            if (!ReferenceEquals(_dataVault, replacement))
            {
                DisposeVaultStorage();
                _dataVault = replacement;
            }

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
                return;

            Interlocked.Increment(ref _audioCallbackInFlight);
            try
            {
                if (Volatile.Read(ref _bankReleaseInProgress) != 0 ||
                    _bankPtr == null ||
                    _statePtr == null ||
                    _codecPtr == null ||
                    _telemetryPtr == null ||
                    _countersPtr == null)
                    return;

                int safeChannels = math.clamp(channels, 1, 8);
                int sampleCount = data.Length / safeChannels;
                if (sampleCount <= 0)
                    return;

                long startTicks = Stopwatch.GetTimestamp();
                fixed (float* output = data)
                {
                    _decodeFunctionPointer.Invoke(
                        output,
                        sampleCount,
                        safeChannels,
                        (_autoBindToSceneAudioListener || _mixIntoExistingAudioGraph) ? 1 : 0,
                        _bankPtr,
                        _bankByteLength,
                        _statePtr,
                        _codecPtr,
                        _telemetryPtr,
                        _countersPtr,
                        _waveformPtr,
                        WaveformCapacity,
                        _frameCounter);
                }

                long elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
                float elapsedMicroseconds = elapsedTicks * (1000000f / Stopwatch.Frequency);
                if (_countersPtr != null)
                {
                    _countersPtr->LastDspMicroseconds = elapsedMicroseconds;
                    if (_telemetryPtr != null)
                    {
                        int index = _countersPtr->TelemetryCursor - 1;
                        while (index < 0)
                            index += TelemetryCapacity;
                        _telemetryPtr[index % TelemetryCapacity].DspMicroseconds = elapsedMicroseconds;
                    }
                    if (elapsedMicroseconds > DspDumpThresholdMicroseconds)
                        Interlocked.Exchange(ref _dumpRequested, 1);
                }
            }
            finally
            {
                Interlocked.Decrement(ref _audioCallbackInFlight);
            }
        }

        private void DrainVocalCueSignals()
        {
            if (_statePtr == null || _codecPtr == null || _bankPtr == null)
                return;

            ReadOnlySpan<VocalCueSignal> signals = SignalBus<VocalCueSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                VocalCueSignal signal = signals[i];
                if (signal.PhraseHashID == 0u)
                    continue;

                VocalStateDTO current = *_statePtr;
                VocalCodecStateDTO currentCodec = *_codecPtr;
                bool isPlaying = (current.Flags & VocalBankConstants.StateFlagPlaying) != 0u;
                if (isPlaying && signal.Priority < currentCodec.Priority)
                    continue;

                if (!VocalBankReader.TryFindRecord(_bankPtr, _bankByteLength, signal.PhraseHashID, out VocalBankIndexRecordDTO record))
                {
                    if (_countersPtr != null)
                    {
                        _countersPtr->MissCount++;
                        _countersPtr->LastFaultFlags = VocalBankConstants.StateFlagBankMiss;
                        _countersPtr->LastPhraseHashID = signal.PhraseHashID;
                    }
                    continue;
                }

                if (record.Codec == VocalBankConstants.CodecVorbis)
                {
                    if (_countersPtr != null)
                    {
                        _countersPtr->FaultCount++;
                        _countersPtr->LastFaultFlags = VocalBankConstants.StateFlagVorbisUnsupported;
                        _countersPtr->LastPhraseHashID = signal.PhraseHashID;
                    }
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
                *_statePtr = next;

                VocalCodecStateDTO codec = *_codecPtr;
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
                *_codecPtr = codec;
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

            float3 local = new float3(signal.SourceAupLocalX, signal.SourceAupLocalY, signal.SourceAupLocalZ);
            float distanceSq = math.max(1f, math.lengthsq(local));
            float inverse = 1f / math.max(1f, distanceSq * 0.0008f);
            float attenuated = math.saturate(inverse);
            return math.lerp(1f, attenuated, blend);
        }

        private void CacheDataVaultCold()
        {
            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;
        }

        private void EnsureVaultStorage()
        {
            IDataVault vault = _dataVault;
            if (Volatile.Read(ref _nativeAllocated) != 0 && TryResolveViews(out _))
                return;

            if (vault == null)
                return;

            _stateHandle = vault.GetGenerationHandle<VocalStateDTO>(BufferID.AudioVocalSynthesisState, 1, VaultOwner, NativeArrayOptions.UninitializedMemory);
            _codecHandle = vault.GetGenerationHandle<VocalCodecStateDTO>(BufferID.AudioVocalSynthesisCodecState, 1, VaultOwner, NativeArrayOptions.UninitializedMemory);
            _telemetryHandle = vault.GetGenerationHandle<VocalTelemetryEntryDTO>(BufferID.AudioVocalSynthesisTelemetry, TelemetryCapacity, VaultOwner, NativeArrayOptions.UninitializedMemory);
            _countersHandle = vault.GetGenerationHandle<VocalDecodeCounters64>(BufferID.AudioVocalSynthesisTelemetryCursor, 1, VaultOwner, NativeArrayOptions.UninitializedMemory);
            _waveformHandle = vault.GetGenerationHandle<float>(BufferID.AudioVocalSynthesisWaveform, WaveformCapacity, VaultOwner, NativeArrayOptions.UninitializedMemory);
            _mockBankBytesHandle = vault.GetGenerationHandle<byte>(BufferID.AudioVocalSynthesisMockBankBytes, MockBankByteCapacity, VaultOwner, NativeArrayOptions.UninitializedMemory);
            _mockRecordsHandle = vault.GetGenerationHandle<VocalBankIndexRecordDTO>(BufferID.AudioVocalSynthesisMockBankRecords, MockRecordCapacity, VaultOwner, NativeArrayOptions.UninitializedMemory);
            _csvMetadataHandle = vault.GetGenerationHandle<VocalDialogueMetadataDTO>(BufferID.AudioVocalSynthesisCsvMetadata, CsvMetadataCapacity, VaultOwner, NativeArrayOptions.UninitializedMemory);
            _csvScratchHandle = vault.GetGenerationHandle<byte>(BufferID.AudioVocalSynthesisCsvScratch, CsvScratchBytes, VaultOwner, NativeArrayOptions.UninitializedMemory);

            if (!TryResolveViews(out VocalVaultViews views))
            {
                DisposeVaultStorage();
                return;
            }

            views.State[0] = default;
            views.Codec[0] = default;
            views.Counters[0] = default;
            if (views.Waveform.Length > 0)
                views.Waveform[0] = 0f;
            if (views.MockRecords.Length > 0)
                views.MockRecords[0] = default;
            if (views.CsvMetadata.Length > 0)
                views.CsvMetadata[0] = default;
            for (int i = 0; i < views.Telemetry.Length; i++)
                views.Telemetry[i] = default;
            _csvMetadataCount = 0;
            RefreshUnsafePointers(in views);
            Volatile.Write(ref _nativeAllocated, 1);
        }

        private bool TryResolveViews(out VocalVaultViews views)
        {
            views = default;
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (!vault.TryResolveHandle(in _stateHandle, out views.State) ||
                !vault.TryResolveHandle(in _codecHandle, out views.Codec) ||
                !vault.TryResolveHandle(in _telemetryHandle, out views.Telemetry) ||
                !vault.TryResolveHandle(in _countersHandle, out views.Counters) ||
                !vault.TryResolveHandle(in _waveformHandle, out views.Waveform) ||
                !vault.TryResolveHandle(in _mockBankBytesHandle, out views.MockBankBytes) ||
                !vault.TryResolveHandle(in _mockRecordsHandle, out views.MockRecords) ||
                !vault.TryResolveHandle(in _csvMetadataHandle, out views.CsvMetadata) ||
                !vault.TryResolveHandle(in _csvScratchHandle, out views.CsvScratch) ||
                !views.State.IsCreated ||
                !views.Codec.IsCreated ||
                !views.Telemetry.IsCreated ||
                !views.Counters.IsCreated ||
                !views.Waveform.IsCreated ||
                !views.MockBankBytes.IsCreated ||
                !views.MockRecords.IsCreated ||
                !views.CsvMetadata.IsCreated ||
                !views.CsvScratch.IsCreated)
            {
                views = default;
                return false;
            }

            return true;
        }

        private void RefreshUnsafePointers(in VocalVaultViews views)
        {
            _statePtr = (VocalStateDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(views.State);
            _codecPtr = (VocalCodecStateDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(views.Codec);
            _telemetryPtr = (VocalTelemetryEntryDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(views.Telemetry);
            _countersPtr = (VocalDecodeCounters64*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(views.Counters);
            _waveformPtr = (float*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(views.Waveform);
            _mockBankPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(views.MockBankBytes);
        }

        private void DisposeVaultStorage()
        {
            IDataVault vault = _dataVault;
            ReleaseVaultBuffer(vault, ref _stateHandle);
            ReleaseVaultBuffer(vault, ref _codecHandle);
            ReleaseVaultBuffer(vault, ref _telemetryHandle);
            ReleaseVaultBuffer(vault, ref _countersHandle);
            ReleaseVaultBuffer(vault, ref _waveformHandle);
            ReleaseVaultBuffer(vault, ref _mockBankBytesHandle);
            ReleaseVaultBuffer(vault, ref _mockRecordsHandle);
            ReleaseVaultBuffer(vault, ref _csvMetadataHandle);
            ReleaseVaultBuffer(vault, ref _csvScratchHandle);
            _statePtr = null;
            _codecPtr = null;
            _telemetryPtr = null;
            _countersPtr = null;
            _waveformPtr = null;
            _mockBankPtr = null;
            if (Volatile.Read(ref _usingMockBank) != 0)
            {
                _bankPtr = null;
                _bankByteLength = 0;
            }
            Volatile.Write(ref _nativeAllocated, 0);
        }

        private static void ReleaseVaultBuffer<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (vault != null && handle.BufferID != 0u)
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private void OpenOrGenerateBankCold()
        {
            if (Volatile.Read(ref _nativeAllocated) == 0 || !TryResolveViews(out VocalVaultViews views))
                return;

            ReleaseMmfCold();
            RefreshUnsafePointers(in views);
            if (TryOpenMmfBankCold())
                return;

            if (_useMockBankWhenFileMissing)
                GenerateMockBankCold(in views);
        }

        private bool TryOpenMmfBankCold()
        {
            string path = Path.Combine(Application.streamingAssetsPath, BankRelativePath);
            if (!File.Exists(path))
                return false;

            try
            {
                FileInfo info = new FileInfo(path);
                if (info.Length <= 0)
                    return false;

                _mmf = MemoryMappedFile.CreateFromFile(path, FileMode.Open, null, 0L, MemoryMappedFileAccess.Read);
                _mmfAccessor = _mmf.CreateViewAccessor(0L, 0L, MemoryMappedFileAccess.Read);
                byte* ptr = null;
                _mmfAccessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
                _mmfPointer = ptr + _mmfAccessor.PointerOffset;
                if (!VocalBankReader.TryReadHeader(_mmfPointer, info.Length, out _))
                {
                    ReleaseMmfCold();
                    return false;
                }

                _bankPtr = _mmfPointer;
                _bankByteLength = info.Length;
                Volatile.Write(ref _usingMockBank, 0);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[SHINOBU_260] vocal_banks.h8bin MMF open failed: " + ex.Message, this);
                ReleaseMmfCold();
                return false;
            }
        }

        private void ReleaseMmfCold()
        {
            Interlocked.Exchange(ref _bankReleaseInProgress, 1);
            _bankPtr = null;
            _bankByteLength = 0;
            SpinWait spin = default;
            while (Volatile.Read(ref _audioCallbackInFlight) != 0)
                spin.SpinOnce();

            try
            {
                if (_mmfAccessor != null)
                {
                    if (_mmfPointer != null)
                    {
                        _mmfAccessor.SafeMemoryMappedViewHandle.ReleasePointer();
                        _mmfPointer = null;
                    }
                    _mmfAccessor.Dispose();
                    _mmfAccessor = null;
                }

                if (_mmf != null)
                {
                    _mmf.Dispose();
                    _mmf = null;
                }

                if (Volatile.Read(ref _usingMockBank) == 0)
                {
                    _bankPtr = null;
                    _bankByteLength = 0;
                }
            }
            finally
            {
                Interlocked.Exchange(ref _bankReleaseInProgress, 0);
            }
        }

        private void GenerateMockBankCold(in VocalVaultViews views)
        {
            if (!views.MockBankBytes.IsCreated || !views.MockRecords.IsCreated)
                return;

            uint sampleRate = (uint)math.max(8000, AudioSettings.outputSampleRate);
            GenerateMockVocalBankJob job = new GenerateMockVocalBankJob
            {
                BankBytes = views.MockBankBytes,
                Records = views.MockRecords,
                PhraseHashID = DefaultMockPhraseHash,
                SampleRate = sampleRate,
                TotalSamples = DefaultMockSamples
            };
            JobHandle handle = job.Schedule();
            DispatcherJobFence.TryComplete(ref handle, forceComplete: true);
            _bankPtr = _mockBankPtr;
            _bankByteLength = views.MockBankBytes.Length;
            Volatile.Write(ref _usingMockBank, 1);
        }

        private bool TryFindMetadata(uint hash, out VocalDialogueMetadataDTO metadata)
        {
            metadata = default;
            if (!TryResolveViews(out VocalVaultViews views) || !views.CsvMetadata.IsCreated)
                return false;

            int count = math.clamp(_csvMetadataCount, 0, views.CsvMetadata.Length);
            int lo = 0;
            int hi = count - 1;
            while (lo <= hi)
            {
                int mid = lo + ((hi - lo) >> 1);
                VocalDialogueMetadataDTO candidate = views.CsvMetadata[mid];
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
        }

        private void ReloadDialogueCsvMetadataCold()
        {
            if (!TryResolveViews(out VocalVaultViews views) ||
                !views.CsvScratch.IsCreated ||
                !views.CsvMetadata.IsCreated)
                return;

            string path = Path.Combine(Application.dataPath, "..", "Docs", "Audio", "dialogue_script.csv");
            if (!File.Exists(path))
                return;

            try
            {
                int bytesRead = 0;
                int capacity = views.CsvScratch.Length;
                byte* scratch = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(views.CsvScratch);
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    while (bytesRead < capacity)
                    {
                        Span<byte> span = new Span<byte>(scratch + bytesRead, capacity - bytesRead);
                        int read = stream.Read(span);
                        if (read <= 0)
                            break;
                        bytesRead += read;
                    }
                }

                ReadOnlySpan<byte> csv = new ReadOnlySpan<byte>(scratch, bytesRead);
                _csvMetadataCount = ParseDialogueCsv(csv, views.CsvMetadata);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[SHINOBU_260] dialogue_script.csv metadata parse failed: " + ex.Message, this);
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
            if (Interlocked.Exchange(ref _registeredSlowTick, 0) != 0)
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Core);
            if (Interlocked.Exchange(ref _registeredUpdate, 0) != 0)
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
        }

        private void DumpBlackboxCold()
        {
            if (_telemetryPtr == null || _countersPtr == null)
                return;

            try
            {
                string path = Path.Combine(Application.dataPath, "..", DumpRelativePath);
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    Span<byte> header = stackalloc byte[32];
                    WriteUInt32(header, 0, 0x44563848u); // H8VD.
                    WriteUInt32(header, 4, 1u);
                    WriteUInt32(header, 8, (uint)TelemetryCapacity);
                    WriteUInt32(header, 12, (uint)UnsafeUtility.SizeOf<VocalTelemetryEntryDTO>());
                    WriteUInt32(header, 16, (uint)_countersPtr->TelemetryCursor);
                    WriteUInt32(header, 20, _countersPtr->LastFaultFlags);
                    WriteUInt32(header, 24, _countersPtr->LastPhraseHashID);
                    WriteUInt32(header, 28, _frameCounter);
                    for (int i = 0; i < header.Length; i++)
                        stream.WriteByte(header[i]);

                    int cursor = _countersPtr->TelemetryCursor;
                    for (int i = 0; i < TelemetryCapacity; i++)
                    {
                        int index = (cursor + i) % TelemetryCapacity;
                        byte* source = (byte*)(_telemetryPtr + index);
                        for (int b = 0; b < UnsafeUtility.SizeOf<VocalTelemetryEntryDTO>(); b++)
                            stream.WriteByte(source[b]);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[SHINOBU_260] black-box dump failed: " + ex.Message, this);
            }
        }

        private static float FiniteOrFallback(float value, float fallback)
        {
            return math.select(fallback, value, math.isfinite(value));
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
