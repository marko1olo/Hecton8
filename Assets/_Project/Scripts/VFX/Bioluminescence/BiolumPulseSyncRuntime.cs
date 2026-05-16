using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Hecton8.VFX.Bioluminescence
{
    /// <summary>
    /// Global shader heartbeat for flora/fauna bioluminescence. Visual authority only.
    /// </summary>
    [DefaultExecutionOrder(-2520)]
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/VFX/Bioluminescence/Pulse Sync Runtime")]
    public sealed class BiolumPulseSyncRuntime : MonoBehaviour,
        IUpdatable,
        IScalabilityChangedEventListener,
        IGlobalRegistryHotSwapListener,
        IGlobalRegistryHotSwapRefListener,
        IDisposable
    {
        public const int MaxGlobalBiolumStates = 16;

        private const int ProfileFloatStride = 8;
        private const int ProfileFloatCount = MaxGlobalBiolumStates * ProfileFloatStride;
        private const int ProfileByteCount = ProfileFloatCount * 4;
        private const int BlackBoxFrameCount = 300;
        private const float StrobeDurationSeconds = 0.1f;
        private const float StrobeFadeSeconds = 0.16f;
        private const float OverloadUpdateIntervalSeconds = 1f / 15f;
        private const float NormalUpdateIntervalSeconds = 0f;
        private const float MaxHdrIntensity = 10f;
        private const float DefaultPingRadiusMeters = 80f;
        private const int JobOverrunDumpFrameThreshold = BlackBoxFrameCount;
        private const byte TelemetryFlagNonFinite = 1;
        private const byte TelemetryFlagJobOverrun = 2;
        private const byte TelemetryFlagAupInvalid = 4;
        private const ushort BlackBoxEntrySizeBytes = 32;
        private const uint BlackBoxMagic = 0x42505359u; // BPSY
        private const uint ProfileFallbackHash = 0x424C4642u; // BLFB
        private const uint ProfileBinaryHash = 0x424C554Du; // BLUM
        private const string ProfileFileName = "Biolum_Profiles.bin";
        private const string ProfileH8BinFileName = "Biolum_Profiles.h8bin";
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_BIOLUM_PULSE_SYNC.bin";

        private static readonly ProfilerMarker _tickMarker = new ProfilerMarker("H8.VFX.BiolumPulseSync.Tick");
        private static readonly int _GlobalBiolumStatesId = Shader.PropertyToID("_GlobalBiolumStates");
        private static readonly int _GlobalBiolumParamsId = Shader.PropertyToID("_GlobalBiolumParams");
        private static readonly int _GlobalBiolumClockId = Shader.PropertyToID("_GlobalBiolumClock");
        private static readonly int _GlobalBiolumAupOffsetId = Shader.PropertyToID("_GlobalBiolumAupOffset");
        private static readonly int _BiolumIntensityId = Shader.PropertyToID("_BiolumIntensity");

        // COLD ALLOC: Vector4[16] - fixed global shader pulse upload payload - owner: BIOLUM_PULSE_SYNC
        private readonly Vector4[] _managedStates = new Vector4[MaxGlobalBiolumStates];

        private IDataVault _dataVault;
        private VaultBufferHandle<float> _profileFloatsHandle;
        private VaultBufferHandle<float4> _jobStatesHandle;
        private VaultBufferHandle<BiolumPulseTelemetryEntry> _blackBoxHandle;
        private ITickDispatcher _tickDispatcher;
        private JobHandle _stateJobHandle;
        private HectonQualityTier _qualityTier = HectonQualityTier.Unknown;
        private float3 _aupOriginOffset;
        private double _localTimeSeconds;
        private float _updateAccumulatorSeconds;
        private float _overloadHoldSeconds;
        private float _strobeTimerSeconds;
        private float _strobePeak01;
        private uint _frameCounter;
        private uint _profileSourceHash = ProfileFallbackHash;
        private uint _vaultGenerationId;
        private int _activeStateCount = 1;
        private int _activeBiolumProfileId;
        private int _blackBoxCursor;
        private int _jobOverrunFrames;
        private byte _pendingTelemetryFlags;
        private bool _registeredUpdate;
        private bool _registeredScalability;
        private bool _registeredHotSwap;
        private bool _stateJobScheduled;
        private bool _jobLocksHeld;
        private bool _disposed;
        private bool _dumpedFault;
        private bool _forceSchedule = true;
        private bool _profilesLoaded;

        private void OnEnable()
        {
            _disposed = false;
            TryRegisterHotSwapListener();
            RefreshQualityTier(GlobalRegistry.ScalabilityTier);
            EnsureVaultBuffers();
            if (!_profilesLoaded)
                LoadProfilesFromDiskOrDefaults();
            TryRegisterScalabilityEvents();
            TryRegisterUpdate();
            EvaluateColdStartStates();
            UploadShaderGlobals(forceStateArray: true);
            _forceSchedule = true;
        }

        private void OnDisable()
        {
            if (_registeredUpdate)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredUpdate = false;
            }

            if (_registeredScalability)
            {
                ScalabilityEvents.Unregister(this);
                _registeredScalability = false;
            }

            TryUnregisterHotSwapListener();
            CompleteScheduledJob();
            ClearShaderGlobals();
            ReleaseVaultHandlesOnly(invalidateProfiles: false);
            _tickDispatcher = null;
        }

        private void OnDestroy()
        {
            if (!_disposed)
                Dispose();
        }

        public void Tick(float deltaTime)
        {
            using (_tickMarker.Auto())
            {
                if (!HasVaultBuffers() && !TryRefreshExistingVaultHandlesNoAllocate())
                    return;

                CompleteScheduledJobAndPublish();

                float dt = SanitizeDelta(deltaTime);
                AdvanceTime(dt);
                ConsumeAupShiftSignals();
                ConsumeFrameTimeSignals(dt);
                ConsumeAcousticPingSignals();
                AdvanceStrobe(dt);
                UploadShaderScalars();

                float cadence = _overloadHoldSeconds > 0f ? OverloadUpdateIntervalSeconds : NormalUpdateIntervalSeconds;
                _updateAccumulatorSeconds += dt;
                bool scheduleDue = !_stateJobScheduled && (_forceSchedule || cadence <= 0f || _updateAccumulatorSeconds >= cadence);
                if (scheduleDue)
                {
                    _forceSchedule = false;
                    _updateAccumulatorSeconds = 0f;
                    ScheduleStateJob(cadence);
                }

                RecordTelemetry(_pendingTelemetryFlags);
                _pendingTelemetryFlags = 0;
            }
        }

        public void OnScalabilityChanged(in ScalabilityChangedEvent payload)
        {
            RefreshQualityTier(payload.CurrentQualityTier);
            _forceSchedule = true;
        }

        public void Dispose()
        {
            CompleteScheduledJob();
            TryUnregisterHotSwapListener();
            ReleaseVaultHandlesOnly(invalidateProfiles: true);
            _dataVault = null;
            _tickDispatcher = null;
            _disposed = true;
        }

#if UNITY_EDITOR
        [ContextMenu("Reload Biolum Profiles")]
        private void ReloadProfilesFromDiskEditor()
        {
            CompleteScheduledJob();
            RefreshCachedRegistryServices();
            EnsureVaultBuffers();
            _profilesLoaded = false;
            LoadProfilesFromDiskOrDefaults();
            EvaluateColdStartStates();
            UploadShaderGlobals(forceStateArray: true);
            _forceSchedule = true;
        }
#endif

        private void TryRegisterUpdate()
        {
            if (_registeredUpdate)
                return;

            RefreshCachedRegistryServices();
            _registeredUpdate = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
        }

        private void TryRegisterScalabilityEvents()
        {
            if (_registeredScalability)
                return;

            ScalabilityEvents.Register(this);
            _registeredScalability = true;
        }

        private void TryRegisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);

            RefreshCachedRegistryServices();
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        void IGlobalRegistryHotSwapRefListener.OnGlobalRegistryServiceRebound(
            GlobalRegistryServiceSlot serviceSlot,
            ref object currentService)
        {
            ApplyRegistryServiceRebind(serviceSlot, currentService);
        }

        void IGlobalRegistryHotSwapListener.OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            ApplyRegistryServiceRebind(serviceSlot, currentService);
        }

        private void RefreshCachedRegistryServices()
        {
            ApplyRegistryServiceRebind(GlobalRegistryServiceSlot.Dispatcher, GlobalRegistry.TickDispatcher);
            ApplyRegistryServiceRebind(GlobalRegistryServiceSlot.DataVault, GlobalRegistry.DataVault);
        }

        private void ApplyRegistryServiceRebind(GlobalRegistryServiceSlot serviceSlot, object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher:
                    _tickDispatcher = currentService as ITickDispatcher;
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    BindDataVault(currentService as IDataVault);
                    break;
            }
        }

        private void RefreshQualityTier(HectonQualityTier tier)
        {
            _qualityTier = tier;
            _activeStateCount = ResolveStateCount(tier);
        }

        private void BindDataVault(IDataVault currentVault)
        {
            if (!ReferenceEquals(_dataVault, currentVault))
            {
                _dataVault = currentVault;
                ReleaseVaultHandlesOnly(invalidateProfiles: true);
            }
        }

        private bool EnsureVaultBuffers()
        {
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (_vaultGenerationId != 0u && _vaultGenerationId != vault.VaultGenerationID)
                ReleaseVaultHandlesOnly(invalidateProfiles: false);

            if (!_profileFloatsHandle.IsCreated ||
                _profileFloatsHandle.BufferId != BufferID.BiolumProfileFloats ||
                _profileFloatsHandle.Length < ProfileFloatCount)
            {
                if (vault.TryGetBufferHandle(BufferID.BiolumProfileFloats, out VaultBufferHandle<float> existingProfileHandle) &&
                    existingProfileHandle.Length >= ProfileFloatCount)
                {
                    _profileFloatsHandle = existingProfileHandle;
                }
                else
                {
                    _profilesLoaded = false;
                    _profileFloatsHandle = vault.GetBufferHandle<float>(
                        BufferID.BiolumProfileFloats,
                        ProfileFloatCount,
                        SystemID.Vfx,
                        NativeArrayOptions.ClearMemory);
                }
            }

            if (!_jobStatesHandle.IsCreated ||
                _jobStatesHandle.BufferId != BufferID.BiolumGlobalStates ||
                _jobStatesHandle.Length < MaxGlobalBiolumStates)
            {
                _jobStatesHandle = vault.GetBufferHandle<float4>(
                    BufferID.BiolumGlobalStates,
                    MaxGlobalBiolumStates,
                    SystemID.Vfx,
                    NativeArrayOptions.ClearMemory);
            }

            if (!_blackBoxHandle.IsCreated ||
                _blackBoxHandle.BufferId != BufferID.BiolumBlackBox ||
                _blackBoxHandle.Length < BlackBoxFrameCount)
            {
                _blackBoxHandle = vault.GetBufferHandle<BiolumPulseTelemetryEntry>(
                    BufferID.BiolumBlackBox,
                    BlackBoxFrameCount,
                    SystemID.Vfx,
                    NativeArrayOptions.ClearMemory);
            }

            bool ready = _profileFloatsHandle.IsCreated && _jobStatesHandle.IsCreated && _blackBoxHandle.IsCreated;
            if (ready)
                _vaultGenerationId = vault.VaultGenerationID;

            return ready;
        }

        private bool HasVaultBuffers()
        {
            IDataVault vault = _dataVault;
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   _vaultGenerationId != 0u &&
                   _vaultGenerationId == vault.VaultGenerationID &&
                   _profileFloatsHandle.IsCreated &&
                   _profileFloatsHandle.BufferId == BufferID.BiolumProfileFloats &&
                   _profileFloatsHandle.Length >= ProfileFloatCount &&
                   _jobStatesHandle.IsCreated &&
                   _jobStatesHandle.BufferId == BufferID.BiolumGlobalStates &&
                   _jobStatesHandle.Length >= MaxGlobalBiolumStates &&
                   _blackBoxHandle.IsCreated &&
                   _blackBoxHandle.BufferId == BufferID.BiolumBlackBox &&
                   _blackBoxHandle.Length >= BlackBoxFrameCount;
        }

        private bool TryRefreshExistingVaultHandlesNoAllocate()
        {
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (!vault.TryGetBufferHandle(BufferID.BiolumProfileFloats, out VaultBufferHandle<float> profileHandle) ||
                profileHandle.Length < ProfileFloatCount)
            {
                return false;
            }

            if (!vault.TryGetBufferHandle(BufferID.BiolumGlobalStates, out VaultBufferHandle<float4> statesHandle) ||
                statesHandle.Length < MaxGlobalBiolumStates)
            {
                return false;
            }

            if (!vault.TryGetBufferHandle(BufferID.BiolumBlackBox, out VaultBufferHandle<BiolumPulseTelemetryEntry> blackBoxHandle) ||
                blackBoxHandle.Length < BlackBoxFrameCount)
            {
                return false;
            }

            _profileFloatsHandle = profileHandle;
            _jobStatesHandle = statesHandle;
            _blackBoxHandle = blackBoxHandle;
            _vaultGenerationId = vault.VaultGenerationID;
            return true;
        }

        private void ReleaseVaultHandlesOnly(bool invalidateProfiles)
        {
            _profileFloatsHandle = default;
            _jobStatesHandle = default;
            _blackBoxHandle = default;
            _vaultGenerationId = 0u;
            if (invalidateProfiles)
                _profilesLoaded = false;
        }

        private void LoadProfilesFromDiskOrDefaults()
        {
            if (!TryLockProfileBuffer(out IDataVault vault, out NativeArray<float> profileFloats, allowEnsure: true))
                return;

            try
            {
                SeedDefaultProfiles(profileFloats);
                _profileSourceHash = ProfileFallbackHash;

                string path = ResolveProfilePath();
                if (string.IsNullOrEmpty(path))
                {
                    _profilesLoaded = true;
                    return;
                }

                try
                {
                    Span<byte> profileBytes = stackalloc byte[ProfileByteCount];
                    int totalBytesRead = 0;
                    using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, ProfileByteCount, FileOptions.SequentialScan))
                    {
                        while (totalBytesRead < ProfileByteCount)
                        {
                            int bytesRead = stream.Read(profileBytes.Slice(totalBytesRead));
                            if (bytesRead <= 0)
                                break;

                            totalBytesRead += bytesRead;
                        }
                    }

                    int readableFloats = math.min(ProfileFloatCount, totalBytesRead >> 2);
                    for (int i = 0; i < readableFloats; i++)
                        profileFloats[i] = SanitizeProfileFloat(MemoryMarshal.Read<float>(profileBytes.Slice(i << 2, 4)), i);

                    _profileSourceHash = ProfileBinaryHash;
                }
                catch (Exception)
                {
                    SeedDefaultProfiles(profileFloats);
                    _profileSourceHash = ProfileFallbackHash;
                }

                _profilesLoaded = true;
            }
            finally
            {
                vault.TryUnlockBuffer(BufferID.BiolumProfileFloats);
            }
        }

        private static string ResolveProfilePath()
        {
            if (TryResolveProfilePath(Application.streamingAssetsPath, out string profilePath))
                return profilePath;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            if (TryResolveProfilePath(Path.Combine(projectRoot, "Docs", "Generated"), out profilePath))
                return profilePath;

            return TryResolveProfilePath(Path.Combine(projectRoot, "Docs"), out profilePath) ? profilePath : null;
#else
            return null;
#endif
        }

        private static bool TryResolveProfilePath(string directory, out string path)
        {
            path = Path.Combine(directory, ProfileFileName);
            if (File.Exists(path))
                return true;

            path = Path.Combine(directory, ProfileH8BinFileName);
            if (File.Exists(path))
                return true;

            path = null;
            return false;
        }

        private bool TryLockProfileBuffer(out IDataVault vault, out NativeArray<float> profileFloats, bool allowEnsure = false)
        {
            profileFloats = default;
            vault = _dataVault;
            bool ready = allowEnsure ? EnsureVaultBuffers() : HasVaultBuffers();
            if (vault == null || !ready)
                return false;

            if (!vault.TryLockBuffer(BufferID.BiolumProfileFloats))
                return false;

            profileFloats = _profileFloatsHandle.Resolve(vault);
            if (profileFloats.IsCreated && profileFloats.Length >= ProfileFloatCount)
                return true;

            vault.TryUnlockBuffer(BufferID.BiolumProfileFloats);
            profileFloats = default;
            return false;
        }

        private bool TryLockBlackBoxBuffer(out IDataVault vault, out NativeArray<BiolumPulseTelemetryEntry> blackBox)
        {
            blackBox = default;
            vault = _dataVault;
            if (vault == null || !HasVaultBuffers())
                return false;

            if (!vault.TryLockBuffer(BufferID.BiolumBlackBox))
                return false;

            blackBox = _blackBoxHandle.Resolve(vault);
            if (blackBox.IsCreated && blackBox.Length >= BlackBoxFrameCount)
                return true;

            vault.TryUnlockBuffer(BufferID.BiolumBlackBox);
            blackBox = default;
            return false;
        }

        private void SeedDefaultProfiles(NativeArray<float> profileFloats)
        {
            for (int i = 0; i < MaxGlobalBiolumStates; i++)
            {
                int offset = i * ProfileFloatStride;
                float lane = i;
                profileFloats[offset] = math.frac(lane * 0.61803398875f);
                profileFloats[offset + 1] = 0.045f + lane * 0.0045f;
                profileFloats[offset + 2] = 0.42f + math.frac(lane * 0.37f) * 0.48f;
                profileFloats[offset + 3] = 0.18f + math.frac(lane * 0.23f) * 0.48f;
                profileFloats[offset + 4] = 0.12f + math.frac(lane * 0.19f) * 0.22f;
                profileFloats[offset + 5] = 0.22f + math.frac(lane * 0.31f) * 0.18f;
                profileFloats[offset + 6] = 0.76f + math.frac(lane * 0.17f) * 0.24f;
                profileFloats[offset + 7] = 0.86f + math.frac(lane * 0.11f) * 0.14f;
            }
        }

        private static float SanitizeProfileFloat(float value, int profileFloatIndex)
        {
            if (!math.isfinite(value))
                return 0f;

            int lane = profileFloatIndex % ProfileFloatStride;
            switch (lane)
            {
                case 1:
                    return math.clamp(value, 0.0025f, 1.5f);
                case 2:
                case 4:
                    return math.clamp(value, 0f, MaxHdrIntensity);
                case 3:
                case 5:
                case 6:
                case 7:
                    return math.saturate(value);
                default:
                    return math.frac(value);
            }
        }

        private void AdvanceTime(float dt)
        {
            ITickDispatcher dispatcher = _tickDispatcher;
            if (dispatcher != null)
            {
                H8TimeSnapshot snapshot = dispatcher.TimeSnapshot;
                if (snapshot.Time >= 0d && !double.IsNaN(snapshot.Time) && !double.IsInfinity(snapshot.Time))
                {
                    _localTimeSeconds = snapshot.Time;
                    return;
                }
            }

            _localTimeSeconds += dt;
        }

        private void ConsumeAupShiftSignals()
        {
            ReadOnlySpan<AupShiftSignal> shifts = SignalBus<AupShiftSignal>.GetFrameSnapshot();
            for (int i = 0; i < shifts.Length; i++)
            {
                float3 delta = shifts[i].ShiftMeters;
                if (math.all(math.isfinite(delta)))
                    _aupOriginOffset += delta;
            }

            if (!math.all(math.isfinite(_aupOriginOffset)))
            {
                _aupOriginOffset = float3.zero;
                _pendingTelemetryFlags |= TelemetryFlagAupInvalid;
                DumpBlackBox(TelemetryFlagAupInvalid);
            }
        }

        private void ConsumeFrameTimeSignals(float dt)
        {
            ReadOnlySpan<FrameTimeSignal> frameSignals = SignalBus<FrameTimeSignal>.GetFrameSnapshot();
            for (int i = 0; i < frameSignals.Length; i++)
            {
                FrameTimeSignal signal = frameSignals[i];
                float targetMs = math.max(signal.TargetFrameTimeMs, 0.001f);
                float currentMs = math.max(signal.CurrentFrameTimeMs, signal.FrameTimeEwmaMs);
                bool overloaded = signal.PressureLevel >= 2 || currentMs > targetMs * 1.25f;
                if (overloaded && math.isfinite(currentMs))
                    _overloadHoldSeconds = 0.45f;
            }

            if (_overloadHoldSeconds > 0f)
                _overloadHoldSeconds = math.max(0f, _overloadHoldSeconds - dt);
        }

        private void ConsumeAcousticPingSignals()
        {
            ReadOnlySpan<AcousticPingSignal> pings = SignalBus<AcousticPingSignal>.GetFrameSnapshot();
            float strongestPing01 = 0f;
            uint strongestSource = 0u;

            for (int i = 0; i < pings.Length; i++)
            {
                AcousticPingSignal signal = pings[i];
                float intensity = math.saturate(signal.Intensity01);
                float radius = math.isfinite(signal.RadiusMeters) ? math.max(signal.RadiusMeters, 0f) : DefaultPingRadiusMeters;
                if (intensity <= 0.0001f)
                    continue;

                float radiusBoost = math.saturate(radius / 180f);
                float contribution = math.saturate(intensity * (0.75f + radiusBoost * 0.25f));
                if (contribution > strongestPing01)
                {
                    strongestPing01 = contribution;
                    strongestSource = signal.SourceId;
                }
            }

            if (strongestPing01 <= 0f)
                return;

            _strobeTimerSeconds = StrobeDurationSeconds;
            _strobePeak01 = math.max(_strobePeak01, strongestPing01);
            _activeBiolumProfileId = (int)(strongestSource & (MaxGlobalBiolumStates - 1));
            _forceSchedule = true;
        }

        private void AdvanceStrobe(float dt)
        {
            if (_strobeTimerSeconds > 0f)
            {
                _strobeTimerSeconds = math.max(0f, _strobeTimerSeconds - dt);
                return;
            }

            if (_strobePeak01 <= 0f)
                return;

            float fadeStep = StrobeFadeSeconds > 0f ? dt / StrobeFadeSeconds : 1f;
            _strobePeak01 = math.max(0f, _strobePeak01 - fadeStep);
        }

        private float ResolveStrobe01()
        {
            if (_strobeTimerSeconds > 0f)
                return math.saturate(_strobePeak01);

            return math.saturate(_strobePeak01);
        }

        private void ScheduleStateJob(float cadenceSeconds)
        {
            IDataVault vault = _dataVault;
            if (vault == null || !HasVaultBuffers())
                return;

            if (!vault.TryLockBuffer(BufferID.BiolumProfileFloats))
                return;

            if (!vault.TryLockBuffer(BufferID.BiolumGlobalStates))
            {
                vault.TryUnlockBuffer(BufferID.BiolumProfileFloats);
                return;
            }

            NativeArray<float> profileFloats = _profileFloatsHandle.Resolve(vault);
            NativeArray<float4> jobStates = _jobStatesHandle.Resolve(vault);
            if (!profileFloats.IsCreated || !jobStates.IsCreated)
            {
                vault.TryUnlockBuffer(BufferID.BiolumGlobalStates);
                vault.TryUnlockBuffer(BufferID.BiolumProfileFloats);
                return;
            }

            BiolumVisualSyncJob job = new BiolumVisualSyncJob
            {
                ProfileFloats = profileFloats,
                States = jobStates,
                TimeSeconds = _localTimeSeconds,
                AupOriginOffset = _aupOriginOffset,
                StateCount = _activeStateCount,
                ProfileStride = ProfileFloatStride,
                Strobe01 = ResolveStrobe01(),
                QualityTier = (byte)_qualityTier,
                CadenceSeconds = cadenceSeconds
            };

            _stateJobHandle = job.Schedule(MaxGlobalBiolumStates, 8);
            H8Memory.RegisterActiveJob(SystemID.Vfx, _stateJobHandle);
            _stateJobScheduled = true;
            _jobLocksHeld = true;
        }

        private void CompleteScheduledJobAndPublish()
        {
            if (!_stateJobScheduled)
                return;

            if (!_stateJobHandle.IsCompleted)
            {
                _jobOverrunFrames = math.min(_jobOverrunFrames + 1, JobOverrunDumpFrameThreshold);
                _pendingTelemetryFlags |= TelemetryFlagJobOverrun;
                if (_jobOverrunFrames >= JobOverrunDumpFrameThreshold)
                    DumpBlackBox(TelemetryFlagJobOverrun);

                return;
            }

            _stateJobHandle.Complete();
            _stateJobScheduled = false;
            _jobOverrunFrames = 0;
            bool finite = CopyJobStatesToManagedBuffer();
            UnlockJobBuffers();
            if (!finite)
            {
                _pendingTelemetryFlags |= TelemetryFlagNonFinite;
                RecordTelemetry(_pendingTelemetryFlags);
                _pendingTelemetryFlags = 0;
                DumpBlackBox(TelemetryFlagNonFinite);
                EvaluateColdStartStates();
            }

            UploadShaderGlobals(forceStateArray: true);
            _pendingTelemetryFlags |= finite ? (byte)0 : TelemetryFlagNonFinite;
        }

        private void CompleteScheduledJob()
        {
            if (_stateJobScheduled)
            {
                _stateJobHandle.Complete();
                _stateJobScheduled = false;
            }

            UnlockJobBuffers();
        }

        private void UnlockJobBuffers()
        {
            if (!_jobLocksHeld)
                return;

            IDataVault vault = _dataVault;
            if (vault != null)
            {
                vault.TryUnlockBuffer(BufferID.BiolumGlobalStates);
                vault.TryUnlockBuffer(BufferID.BiolumProfileFloats);
            }

            _jobLocksHeld = false;
        }

        private bool CopyJobStatesToManagedBuffer()
        {
            bool finite = true;
            int activeCount = math.clamp(_activeStateCount, 1, MaxGlobalBiolumStates);
            float strongest = 0f;
            int strongestProfile = 0;
            IDataVault vault = _dataVault;
            NativeArray<float4> jobStates = vault != null ? _jobStatesHandle.Resolve(vault) : default;
            if (!jobStates.IsCreated)
                return false;

            for (int i = 0; i < MaxGlobalBiolumStates; i++)
            {
                float4 state = jobStates[i];
                if (!math.all(math.isfinite(state)))
                {
                    finite = false;
                    state = float4.zero;
                }

                state.xyz = math.clamp(state.xyz, float3.zero, new float3(MaxHdrIntensity));
                state.w = math.clamp(state.w, 0f, MaxHdrIntensity);
                _managedStates[i] = new Vector4(state.x, state.y, state.z, state.w);

                if (i < activeCount && state.w > strongest)
                {
                    strongest = state.w;
                    strongestProfile = i;
                }
            }

            _activeBiolumProfileId = strongestProfile;
            return finite;
        }

        private void EvaluateColdStartStates()
        {
            if (!TryLockProfileBuffer(out IDataVault vault, out NativeArray<float> profileFloats))
            {
                for (int i = 0; i < MaxGlobalBiolumStates; i++)
                    _managedStates[i] = Vector4.zero;

                return;
            }

            int activeCount = math.clamp(_activeStateCount, 1, MaxGlobalBiolumStates);
            try
            {
                for (int i = 0; i < MaxGlobalBiolumStates; i++)
                {
                    if (i >= activeCount)
                    {
                        _managedStates[i] = Vector4.zero;
                        continue;
                    }

                    int offset = i * ProfileFloatStride;
                    float intensity = math.clamp(profileFloats[offset + 4], 0f, MaxHdrIntensity);
                    _managedStates[i] = new Vector4(
                        math.saturate(profileFloats[offset + 5]),
                        math.saturate(profileFloats[offset + 6]),
                        math.saturate(profileFloats[offset + 7]),
                        intensity);
                }
            }
            finally
            {
                vault.TryUnlockBuffer(BufferID.BiolumProfileFloats);
            }
        }

        private void UploadShaderGlobals(bool forceStateArray)
        {
            if (forceStateArray)
                Shader.SetGlobalVectorArray(_GlobalBiolumStatesId, _managedStates);

            UploadShaderScalars();
        }

        private void UploadShaderScalars()
        {
            float strobe01 = ResolveStrobe01();
            float overloadFlag = _overloadHoldSeconds > 0f ? 1f : 0f;
            float cadence = overloadFlag > 0f ? OverloadUpdateIntervalSeconds : NormalUpdateIntervalSeconds;
            float timeFloat = (float)(_localTimeSeconds % 65536d);
            float masterPhase = math.frac(timeFloat * 0.045f);

            Shader.SetGlobalVector(_GlobalBiolumParamsId, new Vector4(_activeStateCount, (float)_qualityTier, strobe01, overloadFlag));
            Shader.SetGlobalVector(_GlobalBiolumClockId, new Vector4(timeFloat, cadence, _frameCounter, _activeBiolumProfileId));
            Shader.SetGlobalVector(_GlobalBiolumAupOffsetId, new Vector4(_aupOriginOffset.x, _aupOriginOffset.y, _aupOriginOffset.z, _profileSourceHash));
            Shader.SetGlobalVector(_BiolumIntensityId, new Vector4(math.clamp(_managedStates[0].w, 0f, MaxHdrIntensity), strobe01, _activeStateCount, overloadFlag));
            HectonShaderGlobalDataVaultBridge.PublishBiolumMasterPhase(new Vector4(masterPhase, ResolveTrianglePulse01(masterPhase), strobe01, overloadFlag));
        }

        private void ClearShaderGlobals()
        {
            for (int i = 0; i < MaxGlobalBiolumStates; i++)
                _managedStates[i] = Vector4.zero;

            Shader.SetGlobalVectorArray(_GlobalBiolumStatesId, _managedStates);
            Shader.SetGlobalVector(_GlobalBiolumParamsId, Vector4.zero);
            Shader.SetGlobalVector(_GlobalBiolumClockId, Vector4.zero);
            Shader.SetGlobalVector(_GlobalBiolumAupOffsetId, Vector4.zero);
            HectonShaderGlobalDataVaultBridge.PublishBiolumMasterPhase(new Vector4(0f, 0.5f, 0f, 0f));
        }

        private void RecordTelemetry(byte flags)
        {
            if (!TryLockBlackBoxBuffer(out IDataVault vault, out NativeArray<BiolumPulseTelemetryEntry> blackBox))
                return;

            try
            {
                Vector4 primaryState = _managedStates[0];
                blackBox[_blackBoxCursor] = new BiolumPulseTelemetryEntry
                {
                    Frame = _frameCounter++,
                    ActiveBiolumProfileId = (ushort)math.clamp(_activeBiolumProfileId, 0, MaxGlobalBiolumStates - 1),
                    ActiveStateCount = (byte)_activeStateCount,
                    QualityTier = (byte)_qualityTier,
                    Flags = flags,
                    Strobe01 = ResolveStrobe01(),
                    PrimaryIntensityHdr = math.clamp(primaryState.w, 0f, MaxHdrIntensity),
                    TimeSeconds = (float)(_localTimeSeconds % 65536d),
                    AupOffsetX = _aupOriginOffset.x,
                    AupOffsetZ = _aupOriginOffset.z
                };

                _blackBoxCursor = (_blackBoxCursor + 1) % blackBox.Length;
            }
            finally
            {
                vault.TryUnlockBuffer(BufferID.BiolumBlackBox);
            }
        }

        private void DumpBlackBox(byte reason)
        {
            if (_dumpedFault || !TryLockBlackBoxBuffer(out IDataVault vault, out NativeArray<BiolumPulseTelemetryEntry> blackBox))
                return;

            _dumpedFault = true;
            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string dumpPath = Path.Combine(projectRoot, DumpRelativePath);
                string directory = Path.GetDirectoryName(dumpPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    BiolumPulseDumpHeader header = new BiolumPulseDumpHeader
                    {
                        Magic = BlackBoxMagic,
                        Reason = reason,
                        EntrySizeBytes = BlackBoxEntrySizeBytes,
                        WriteCursor = _blackBoxCursor,
                        EntryCount = blackBox.Length
                    };

                    WriteUnmanaged(stream, ref header);
                    for (int i = 0; i < blackBox.Length; i++)
                    {
                        BiolumPulseTelemetryEntry entry = blackBox[(_blackBoxCursor + i) % blackBox.Length];
                        WriteUnmanaged(stream, ref entry);
                    }
                }
            }
            catch (Exception)
            {
                _dumpedFault = false;
            }
            finally
            {
                vault.TryUnlockBuffer(BufferID.BiolumBlackBox);
            }
        }

        private static int ResolveStateCount(HectonQualityTier tier)
        {
            switch (tier)
            {
                case HectonQualityTier.Ultra:
                    return 16;
                case HectonQualityTier.High:
                    return 16;
                case HectonQualityTier.Mid:
                    return 4;
                default:
                    return 1;
            }
        }

        private static float SanitizeDelta(float deltaTime)
        {
            if (!math.isfinite(deltaTime) || deltaTime <= 0f)
                return 0f;

            return math.min(deltaTime, 0.25f);
        }

        private static float ResolveTrianglePulse01(float phase01)
        {
            return 1f - math.abs(math.frac(phase01) * 2f - 1f);
        }

        private static void WriteUnmanaged<T>(FileStream stream, ref T value) where T : unmanaged
        {
            ReadOnlySpan<T> valueSpan = MemoryMarshal.CreateReadOnlySpan(ref value, 1);
            stream.Write(MemoryMarshal.AsBytes(valueSpan));
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
        private struct BiolumVisualSyncJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float> ProfileFloats;
            public NativeArray<float4> States;
            public double TimeSeconds;
            public float3 AupOriginOffset;
            public int StateCount;
            public int ProfileStride;
            public float Strobe01;
            public byte QualityTier;
            public float CadenceSeconds;

            public void Execute(int index)
            {
                if (index >= StateCount)
                {
                    States[index] = float4.zero;
                    return;
                }

                int offset = index * ProfileStride;
                float time = (float)(TimeSeconds % 4096d);
                float spatialPhase = (AupOriginOffset.x * 0.00013f + AupOriginOffset.z * 0.00017f) * (index + 1);
                float phaseOffset = ProfileFloats[offset];
                float frequencyHz = math.max(ProfileFloats[offset + 1], 0.0025f);
                float amplitudeHdr = math.clamp(ProfileFloats[offset + 2], 0f, MaxHdrIntensity);
                float sharpness01 = math.saturate(ProfileFloats[offset + 3]);
                float floorHdr = math.clamp(ProfileFloats[offset + 4], 0f, MaxHdrIntensity);
                float3 color = math.saturate(new float3(ProfileFloats[offset + 5], ProfileFloats[offset + 6], ProfileFloats[offset + 7]));

                float phase = math.frac(time * frequencyHz + phaseOffset + spatialPhase);
                float triangle = 1f - math.abs(phase * 2f - 1f);
                float smoothTriangle = triangle * triangle * (3f - 2f * triangle);
                float sharpened = math.lerp(triangle, smoothTriangle * smoothTriangle, sharpness01);
                float cadenceBoost = math.saturate(CadenceSeconds * 15f) * 0.04f;
                float tierGain = QualityTier >= (byte)HectonQualityTier.High ? 1.08f : 1f;
                float intensity = (floorHdr + amplitudeHdr * sharpened) * tierGain + cadenceBoost;

                float strobe = math.saturate(Strobe01);
                color = math.lerp(color, new float3(1f, 1f, 1f), strobe);
                intensity = math.max(intensity, strobe * MaxHdrIntensity);
                intensity = math.clamp(intensity, 0f, MaxHdrIntensity);

                float4 state = new float4(color, intensity);
                States[index] = math.all(math.isfinite(state)) ? state : float4.zero;
            }
        }

        [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]
        private struct BiolumPulseTelemetryEntry
        {
            [FieldOffset(0)]
            public uint Frame;
            [FieldOffset(4)]
            public ushort ActiveBiolumProfileId;
            [FieldOffset(6)]
            public byte ActiveStateCount;
            [FieldOffset(7)]
            public byte QualityTier;
            [FieldOffset(8)]
            public byte Flags;
            [FieldOffset(9)]
            public byte Reserved0;
            [FieldOffset(10)]
            public ushort Reserved1;
            [FieldOffset(12)]
            public float Strobe01;
            [FieldOffset(16)]
            public float PrimaryIntensityHdr;
            [FieldOffset(20)]
            public float TimeSeconds;
            [FieldOffset(24)]
            public float AupOffsetX;
            [FieldOffset(28)]
            public float AupOffsetZ;
        }

        [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 16)]
        private struct BiolumPulseDumpHeader
        {
            [FieldOffset(0)]
            public uint Magic;
            [FieldOffset(4)]
            public byte Reason;
            [FieldOffset(5)]
            public byte Reserved;
            [FieldOffset(6)]
            public ushort EntrySizeBytes;
            [FieldOffset(8)]
            public int WriteCursor;
            [FieldOffset(12)]
            public int EntryCount;
        }
    }
}
