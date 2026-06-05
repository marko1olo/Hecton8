using System;
using System.IO;
using System.Runtime.CompilerServices;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Hecton8.Lighting
{
    /// <summary>
    /// Vault-owned dynamic point-light culling director. It submits mathematical light payloads, not Unity Light objects.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-2300)]
    public sealed unsafe class DynamicPointLightCullingDirector : MonoBehaviour, IUpdatable, ISlowTickable, ILateFrameTickable, IDisposable, IGlobalRegistryHotSwapListener
    {
        private const SystemID MemoryOwner = SystemID.GraphicsScalability;
        private const int DefaultCsvScratchBytes = 32 * 1024;
        private const int DefaultProfileCapacity = 64;
        private const int DefaultSdfResolution = 16;
        private const int MaxGizmoLightsHardCap = 512;
        private const float MinimumScheduleCadence = 1f / 60f;
        private const float MaximumScheduleCadence = 0.16f;
        private const float FaultTimeoutSeconds = 1.5f;
        private const uint DumpMagic = 0x4C445038u; // LDP8
        private const int DumpVersion = 1;
        private const string DefaultProfileCsvRelativePath = "Docs/Data/light_culling_profiles.csv";
        private const string BlackBoxDumpRelativePath = "Docs/AgentLogs/Dump_13KRA.bin";

        private static readonly int _DynamicLightBufferId = Shader.PropertyToID("_H8DynamicPointLightBuffer");
        private static readonly int _DynamicLightStateId = Shader.PropertyToID("_H8DynamicPointLightState");
        private static readonly int _DynamicLightCameraAupId = Shader.PropertyToID("_H8DynamicPointLightCameraAup");
        private const uint JobPinSources = 1u << 0;
        private const uint JobPinStates = 1u << 1;
        private const uint JobPinFrustumPlanes = 1u << 2;
        private const uint JobPinMockSdfSamples = 1u << 3;
        private const uint JobPinProfileRules = 1u << 4;
        private const uint JobPinImportanceKeys = 1u << 5;
        private const uint JobPinImportanceIndices = 1u << 6;
        private const uint JobPinSortScratchKeys = 1u << 7;
        private const uint JobPinSortScratchIndices = 1u << 8;
        private const uint JobPinGpuPayloadFront = 1u << 9;
        private const uint JobPinGpuPayloadBack = 1u << 10;
        private const uint JobPinDynamicProbeLights = 1u << 11;
        private const uint JobPinRuntimeCounters = 1u << 12;
        private static readonly ulong MockSeedMutationGuardMask =
            MutationGuardBit(DynamicPointLightCullingVaultIds.Sources) |
            MutationGuardBit(DynamicPointLightCullingVaultIds.States);
        private static readonly ulong MockSdfMutationGuardMask = MutationGuardBit(DynamicPointLightCullingVaultIds.MockSdfSamples);
        private static readonly ulong SourceManifestMutationGuardMask = MutationGuardBit(DynamicPointLightCullingVaultIds.SourceManifest);

        [Header("Source Capacity")]
        [Tooltip("Maximum mathematical light sources stored in the Vault.")]
        [SerializeField, Range(128, 16384)] private int sourceCapacity = DynamicPointLightCullingMath.DefaultMockLightCount;
        [Tooltip("Generate deterministic stress lights when source buffers are empty.")]
        [SerializeField] private bool generateMockDataOnEnable = true;

        [Header("Culling")]
        [Tooltip("Camera used for frustum plane extraction. If empty, the player runtime context camera is used.")]
        [SerializeField] private Camera renderCamera;
        [Tooltip("Base fade distance in meters before source profile multipliers.")]
        [SerializeField, Range(4f, 96f)] private float baseFadeDistanceMeters = 38f;
        [Tooltip("Importance multiplier applied before radix sorting.")]
        [SerializeField, Range(0.05f, 8f)] private float importanceWeight = 1f;
        [Tooltip("SDF threshold below which the light is considered occluded.")]
        [SerializeField, Range(-4f, 4f)] private float sdfOcclusionThreshold = -0.05f;
        [Tooltip("Maximum range accepted from a source record.")]
        [SerializeField, Range(8f, 256f)] private float maxRangeMeters = 48f;
        [Tooltip("Intensity below this value is excluded from GPU upload.")]
        [SerializeField, Range(0.000001f, 0.05f)] private float submitIntensityEpsilon = 0.0002f;

        [Header("Scalability")]
        [Tooltip("Secondary bounce gain applied to surviving lights before probe injection.")]
        [SerializeField, Range(0f, 2f)] private float bounceGain = 0.35f;
        [Tooltip("Near-field extra intensity bought on high quality weights.")]
        [SerializeField, Range(0f, 1f)] private float nearFieldOverkillBoost = 0.25f;
        [Tooltip("Thermal-pressure fade strength. Final behavior still remains continuous.")]
        [SerializeField, Range(0f, 1f)] private float thermalFadeStrength = 0.65f;
        [Tooltip("Editor/test override. Negative uses HomeostasisBrain.GlobalQualityWeight.")]
        [SerializeField, Range(-1f, 1f)] private float editorQualityOverride = -1f;

        [Header("Voxel SDF")]
        [Tooltip("Resolution of the mock CPU SDF grid used when no streamed voxel mirror is connected.")]
        [SerializeField, Range(4, 32)] private int mockSdfResolution = DefaultSdfResolution;
        [Tooltip("Cell size in meters for mock SDF line-of-sight samples.")]
        [SerializeField, Range(0.5f, 8f)] private float mockSdfCellSizeMeters = 4f;

        [Header("Profiles")]
        [Tooltip("Project-relative CSV path for profile priority/fade/intensity rules.")]
        [SerializeField] private string profileCsvRelativePath = DefaultProfileCsvRelativePath;

        [Header("Debug")]
        [Tooltip("Draw editor-only culling gizmos from Vault state.")]
        [SerializeField] private bool drawDebugGizmos;
        [Tooltip("Maximum debug gizmo boxes drawn in Scene view.")]
        [SerializeField, Range(0, MaxGizmoLightsHardCap)] private int debugGizmoMaxLights = 192;

        private IDataVault _vault;
        private IPlayerRuntimeContext _playerContext;
        private Transform _cachedTransform;
        private VaultGenerationHandle<DynamicPointLightSourceDTO> _sources;
        private VaultGenerationHandle<LightCullStateDTO> _states;
        private VaultGenerationHandle<DynamicPointLightSourceManifestDTO> _sourceManifest;
        private VaultGenerationHandle<DynamicPointLightCullingSettingsDTO> _settings;
        private VaultGenerationHandle<DynamicPointLightGpuDTO> _gpuPayloadFront;
        private VaultGenerationHandle<DynamicPointLightGpuDTO> _gpuPayloadBack;
        private VaultGenerationHandle<DynamicPointLightCullingTelemetryEntry> _telemetryRing;
        private VaultGenerationHandle<int> _telemetryCursor;
        private VaultGenerationHandle<uint> _importanceKeys;
        private VaultGenerationHandle<int> _importanceIndices;
        private VaultGenerationHandle<uint> _sortScratchKeys;
        private VaultGenerationHandle<int> _sortScratchIndices;
        private VaultGenerationHandle<byte> _csvScratch;
        private VaultGenerationHandle<DynamicPointLightProfileRuleDTO> _profileRules;
        private VaultGenerationHandle<float> _mockSdfSamples;
        private VaultGenerationHandle<CustomDynamicProbeLightDTO> _dynamicProbeLights;
        private VaultGenerationHandle<DynamicPointLightRuntimeCountersDTO> _runtimeCounters;
        private VaultGenerationHandle<float4> _frustumPlanes;
        private VaultGenerationHandle<DynamicPointLightSelfAuditDTO> _selfAudit;

        private GraphicsBuffer _gpuBufferA;
        private GraphicsBuffer _gpuBufferB;
        private JobHandle _pendingCullHandle;
        private long _pendingScheduleTicks;
        private int _payloadWriteIndex;
        private int _scheduledPayloadIndex;
        private int _gpuUploadWriteIndex;
        private int _profileRuleCount;
        private int _activeSourceCount;
        private int _telemetryWriteCursor;
        private uint _frameSequence;
        private ulong _lastGpuUploadBytes;
        private float _scheduleAccumulator;
        private bool _nativeStorageReady;
        private bool _registeredTick;
        private bool _registeredSlowTick;
        private bool _registeredLateFrame;
        private bool _registeredHotSwapListener;
        private bool _jobActive;
        private bool _csvReloadRequested;
        private bool _blackBoxDumped;
        private bool _sourceBufferSeeded;
        private bool _mockSdfSeeded;
        private bool _timeoutFaultPending;
        private IDataVault _jobPinVault;
        private uint _jobPinMask;
        private bool _jobPinsHeld;
        private bool _mockSeedGuardHeld;
        private bool _mockSdfGuardHeld;
        private bool _sourceManifestGuardHeld;

        /// <summary>True when Vault buffers and GPU buffers can be used.</summary>
        public bool IsInitialized => _nativeStorageReady;

        /// <summary>Current logical source count.</summary>
        public int ActiveSourceCount => _activeSourceCount;

        /// <summary>Current parsed profile rule count.</summary>
        public int ProfileRuleCount => _profileRuleCount;

        /// <summary>Dispatcher heartbeat count.</summary>
        public int TickCount => unchecked((int)_frameSequence);

        private static ulong MutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        private void Awake()
        {
            _cachedTransform = transform;
        }

        private void OnEnable()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                return;
#endif
            _cachedTransform = transform;
            CacheDependencies();
            TryRegisterHotSwapListener();
            EnsureNativeStorage();
            if (_nativeStorageReady)
                EnsureGpuBuffersCold(DynamicPointLightCullingMath.MaximumActiveLights);
            TryRegisterDispatch();
        }

        private void OnDisable()
        {
            ShutdownRuntime();
        }

        private void OnDestroy()
        {
            ShutdownRuntime();
            ReleaseGpuBuffers();
        }

        public void Dispose()
        {
            ShutdownRuntime();
            ReleaseGpuBuffers();
        }

        /// <summary>
        /// Simulation-phase scheduling. Jobs complete only in the late-frame visual/swap window.
        /// </summary>
        public void Tick(float deltaTime)
        {
            _frameSequence++;
            if (!_nativeStorageReady && !EnsureNativeStorage(allowAllocation: false, allowMockGeneration: false))
                return;

            if (_jobActive)
                return;

            float quality = ResolveQualityWeight();
            float cadence = ResolveScheduleCadence(quality, HomeostasisBrain.SystemHealthIndex01);
            _scheduleAccumulator += math.max(0f, math.isfinite(deltaTime) ? deltaTime : MinimumScheduleCadence);
            if (_scheduleAccumulator < cadence)
                return;

            _scheduleAccumulator = 0f;
            DynamicPointLightCullingSettingsDTO settings = BuildSettings(quality);
            WriteSettings(settings);
            WriteFrustumPlanes();
            ScheduleCullingPipeline(settings);
        }

        /// <summary>
        /// Slow path for profile CSV reload requests.
        /// </summary>
        public void SlowTick()
        {
            if (!_csvReloadRequested)
                return;

            _csvReloadRequested = false;
#if UNITY_EDITOR
            TryLoadProfilesFromCsv();
#endif
        }

        /// <summary>
        /// VISUAL_SYNC window. Reclaims completed culling jobs, uploads GPU payload, and writes telemetry.
        /// </summary>
        public void LateFrameTick()
        {
            if (!_nativeStorageReady)
                return;

            if (!_jobActive)
                return;

            double pendingSeconds = (Stopwatch.GetTimestamp() - _pendingScheduleTicks) / (double)Stopwatch.Frequency;
            if (!_pendingCullHandle.IsCompleted)
            {
                if (pendingSeconds > FaultTimeoutSeconds)
                {
                    _timeoutFaultPending = true;
                    if (!_blackBoxDumped)
                        DumpBlackBoxNow();
                }
                return;
            }

            // Non-blocking reclaim: the handle was proven completed above before release/upload.
            if (!DispatcherJobFence.TryFinalizeCompleted(ref _pendingCullHandle))
                return;

            UnlockJobBuffers();
            _jobActive = false;

            float elapsedUs = (float)math.max(0.0d, pendingSeconds * 1000000.0d);
            if (_timeoutFaultPending)
            {
                RecordTimeoutFault();
                _timeoutFaultPending = false;
            }

            UploadScheduledPayload();
            RecordTelemetry(elapsedUs);
            if (TryGetCountersCopy(out DynamicPointLightRuntimeCountersDTO counters) &&
                (counters.Flags & DynamicPointLightCullingFlags.NonFinite) != 0u)
            {
                if (!_blackBoxDumped)
                    DumpBlackBoxNow();
            }
        }

        /// <summary>Requests profile CSV reload on the next slow tick.</summary>
        public void RequestCsvReload()
        {
#if UNITY_EDITOR
            _csvReloadRequested = true;
#endif
        }

        /// <summary>Runs the CSV reload immediately from editor/cold tooling.</summary>
        public bool ReloadCsvNow()
        {
#if UNITY_EDITOR
            return TryLoadProfilesFromCsv();
#else
            return false;
#endif
        }

        /// <summary>Regenerates deterministic 5000-light mock data through Burst.</summary>
        public bool GenerateMockLightCullingData()
        {
            if (_jobActive)
                return false;

            if (!_nativeStorageReady && !EnsureNativeStorage(allowAllocation: false, allowMockGeneration: false))
                return false;

            DynamicPointLightCullingSettingsDTO settings = BuildSettings(ResolveQualityWeight());
            int targetCount = math.min(sourceCapacity, DynamicPointLightCullingMath.DefaultMockLightCount);
            if (targetCount <= 0)
                return false;

            settings.ActiveSourceCount = targetCount;

            if (!TryLockMockSeedBuffers())
                return false;

            int seededCapacity;
            try
            {
                NativeArray<DynamicPointLightSourceDTO> sources = ResolveArray(ref _sources);
                NativeArray<LightCullStateDTO> states = ResolveArray(ref _states);
                if (!sources.IsCreated || !states.IsCreated)
                    return false;

                seededCapacity = math.min(sources.Length, states.Length);
                JobHandle handle = new GenerateMockLightCullingDataJob
                {
                    Sources = sources,
                    States = states,
                    Settings = settings,
                    Count = targetCount
                }.Schedule(targetCount, 64);
                H8Memory.RegisterActiveJob(MemoryOwner, handle);
                // COLD SYNC JOB: mock seed fence; source manifest commits only after data is written.
                ForceCompleteJobInPostSimulationWindow(ref handle);
            }
            finally
            {
                UnlockMockSeedBuffers();
            }

            if (!TryLockSourceManifestBuffer())
                return false;

            try
            {
                CommitSourceManifest(
                    targetCount,
                    seededCapacity,
                    DynamicPointLightSourceManifestFlags.Committed | DynamicPointLightSourceManifestFlags.MockGenerated,
                    DynamicPointLightCullingMath.SourceHash);
            }
            finally
            {
                UnlockSourceManifestBuffer();
            }

            WriteSettings(settings);
            _sourceBufferSeeded = true;
            _mockSdfSeeded = GenerateMockSdfSamples(settings);
            return true;
        }

        /// <summary>Reads the telemetry ring without allocating.</summary>
        public bool TryGetTelemetryReadback(out NativeArray<DynamicPointLightCullingTelemetryEntry>.ReadOnly telemetry, out int cursor)
        {
            telemetry = default;
            cursor = _telemetryWriteCursor;
            if (_jobActive || !HasDynamicPointLightHandle(in _telemetryRing, DynamicPointLightCullingVaultIds.TelemetryRing))
                return false;

            NativeArray<DynamicPointLightCullingTelemetryEntry> mutableTelemetry = ResolveArray(ref _telemetryRing);
            if (!mutableTelemetry.IsCreated)
                return false;

            telemetry = mutableTelemetry.AsReadOnly();
            return telemetry.Length > 0;
        }

        /// <summary>Reads culling states for editor diagnostics.</summary>
        public bool TryGetStatesReadback(out NativeArray<LightCullStateDTO>.ReadOnly states, out NativeArray<DynamicPointLightSourceDTO>.ReadOnly sources, out int count)
        {
            states = default;
            sources = default;
            count = 0;
            if (_jobActive ||
                !HasDynamicPointLightHandle(in _states, DynamicPointLightCullingVaultIds.States) ||
                !HasDynamicPointLightHandle(in _sources, DynamicPointLightCullingVaultIds.Sources))
                return false;

            NativeArray<LightCullStateDTO> mutableStates = ResolveArray(ref _states);
            NativeArray<DynamicPointLightSourceDTO> mutableSources = ResolveArray(ref _sources);
            if (!mutableStates.IsCreated || !mutableSources.IsCreated)
                return false;

            states = mutableStates.AsReadOnly();
            sources = mutableSources.AsReadOnly();
            count = math.min(ReadCommittedSourceCount(), math.min(states.Length, sources.Length));
            return true;
        }

        /// <summary>Copies current settings for editor tooling.</summary>
        public bool TryGetSettingsCopy(out DynamicPointLightCullingSettingsDTO settings)
        {
            settings = default;
            if (!HasDynamicPointLightHandle(in _settings, DynamicPointLightCullingVaultIds.Settings))
                return false;

            NativeArray<DynamicPointLightCullingSettingsDTO> array = ResolveArray(ref _settings);
            if (!array.IsCreated || array.Length == 0)
                return false;

            settings = array[0];
            return true;
        }

        /// <summary>Copies current counters for editor tooling.</summary>
        public bool TryGetCountersCopy(out DynamicPointLightRuntimeCountersDTO counters)
        {
            counters = default;
            if (!HasDynamicPointLightHandle(in _runtimeCounters, DynamicPointLightCullingVaultIds.RuntimeCounters))
                return false;

            NativeArray<DynamicPointLightRuntimeCountersDTO> array = ResolveArray(ref _runtimeCounters);
            if (!array.IsCreated || array.Length == 0)
                return false;

            counters = array[0];
            return true;
        }

        /// <summary>Exposes the owner-local fake bounce stream for the probe-grid owner without scheduling cross-owner jobs.</summary>
        public bool TryGetProbeBounceReadback(out NativeArray<CustomDynamicProbeLightDTO>.ReadOnly lights, out int count)
        {
            lights = default;
            count = 0;
            if (_jobActive || !HasDynamicPointLightHandle(in _dynamicProbeLights, DynamicPointLightCullingVaultIds.DynamicProbeLights))
                return false;

            if (!TryGetCountersCopy(out DynamicPointLightRuntimeCountersDTO counters))
                return false;

            NativeArray<CustomDynamicProbeLightDTO> mutableLights = ResolveArray(ref _dynamicProbeLights);
            if (!mutableLights.IsCreated)
                return false;

            lights = mutableLights.AsReadOnly();
            count = math.clamp(counters.SubmittedLights, 0, math.min(lights.Length, DynamicPointLightCullingMath.MaximumActiveLights));
            return count > 0;
        }

        /// <summary>
        /// Commits an externally written source window after the writer has fully populated the Vault source/state buffers.
        /// </summary>
        /// <param name="count">Number of valid source records written from index zero.</param>
        /// <param name="writerHash">Stable writer hash for forensic ownership.</param>
        /// <remarks>
        /// This method does not allocate and does not touch Unity Light objects. It only publishes a Vault manifest.
        /// </remarks>
        public bool TryCommitExternalSourceCount(int count, uint writerHash)
        {
            if (_jobActive)
                return false;

            if (!_nativeStorageReady && !EnsureNativeStorage(allowAllocation: false, allowMockGeneration: false))
                return false;

            NativeArray<DynamicPointLightSourceDTO> sources = ResolveArray(ref _sources);
            NativeArray<LightCullStateDTO> states = ResolveArray(ref _states);
            if (!sources.IsCreated || !states.IsCreated)
                return false;

            int capacity = math.min(sourceCapacity, math.min(sources.Length, states.Length));
            int safeCount = math.clamp(count, 0, capacity);
            if (!TryLockSourceManifestBuffer())
                return false;

            try
            {
                CommitSourceManifest(
                    safeCount,
                    capacity,
                    DynamicPointLightSourceManifestFlags.Committed | DynamicPointLightSourceManifestFlags.ExternalWriter,
                    writerHash);
            }
            finally
            {
                UnlockSourceManifestBuffer();
            }

            DynamicPointLightCullingSettingsDTO settings = BuildSettings(ResolveQualityWeight());
            settings.ActiveSourceCount = safeCount;
            WriteSettings(settings);
            return safeCount == count;
        }

        /// <summary>Copies the current source manifest for editor tools and forensics.</summary>
        public bool TryGetSourceManifestCopy(out DynamicPointLightSourceManifestDTO manifest)
        {
            manifest = default;
            if (!HasDynamicPointLightHandle(in _sourceManifest, DynamicPointLightCullingVaultIds.SourceManifest))
                return false;

            NativeArray<DynamicPointLightSourceManifestDTO> array = ResolveArray(ref _sourceManifest);
            if (!array.IsCreated || array.Length == 0)
                return false;

            manifest = array[0];
            return true;
        }

        /// <summary>Editor-only quality override setter.</summary>
        public void SetEditorForceQuality(float value)
        {
            editorQualityOverride = math.clamp(value, -1f, 1f);
        }

        /// <summary>Editor-only base fade setter.</summary>
        public void SetEditorBaseFadeDistance(float meters)
        {
            baseFadeDistanceMeters = math.clamp(meters, 4f, 96f);
        }

        /// <summary>Editor-only importance setter.</summary>
        public void SetEditorImportanceWeight(float value)
        {
            importanceWeight = math.clamp(value, 0.05f, 8f);
        }

        /// <summary>Editor-only SDF threshold setter.</summary>
        public void SetEditorSdfOcclusionThreshold(float value)
        {
            sdfOcclusionThreshold = math.clamp(value, -4f, 4f);
        }

        /// <summary>Writes the 300-frame black box to Docs/AgentLogs/Dump_13KRA.bin.</summary>
        public bool DumpBlackBoxNow()
        {
            if (!HasDynamicPointLightHandle(in _telemetryRing, DynamicPointLightCullingVaultIds.TelemetryRing))
                return false;

            NativeArray<DynamicPointLightCullingTelemetryEntry> ring = ResolveArray(ref _telemetryRing);
            if (!ring.IsCreated || ring.Length == 0)
                return false;

            const string path = BlackBoxDumpRelativePath;
            NativeArray<byte> payload = default;
            try
            {
                int stride = UnsafeUtility.SizeOf<DynamicPointLightCullingTelemetryEntry>();
                int byteCount = 20 + ring.Length * stride;
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(DynamicPointLightCullingDirector),
                    "DynamicPointLightCullingBlackBoxDumpPayload");
                byte* target = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                WriteUInt32LittleEndian(target, 0, DumpMagic);
                WriteInt32LittleEndian(target, 4, DumpVersion);
                WriteInt32LittleEndian(target, 8, ring.Length);
                WriteInt32LittleEndian(target, 12, stride);
                WriteInt32LittleEndian(target, 16, _telemetryWriteCursor);
                int offset = 20;
                for (int i = 0; i < ring.Length; i++)
                {
                    int index = _telemetryWriteCursor + i;
                    if (index >= ring.Length)
                        index -= ring.Length;

                    DynamicPointLightCullingTelemetryEntry entry = ring[index];
                    UnsafeUtility.MemCpy(target + offset, UnsafeUtility.AddressOf(ref entry), stride);
                    offset += stride;
                }

                _blackBoxDumped = NativeFaultDumpWriter.TryWriteAll(path, payload, byteCount);
                return _blackBoxDumped;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(DynamicPointLightCullingDirector),
                    "DynamicPointLightCullingBlackBoxDumpPayload");
            }
        }

        private static unsafe void WriteInt32LittleEndian(byte* target, int offset, int value)
        {
            WriteUInt32LittleEndian(target, offset, unchecked((uint)value));
        }

        private static unsafe void WriteUInt32LittleEndian(byte* target, int offset, uint value)
        {
            target[offset] = (byte)value;
            target[offset + 1] = (byte)(value >> 8);
            target[offset + 2] = (byte)(value >> 16);
            target[offset + 3] = (byte)(value >> 24);
        }

        private void CacheDependencies()
        {
            RebindDataVaultForLifecycle(GlobalRegistry.DataVault);
            _playerContext = GlobalRegistry.Player;
        }

        private void RebindDataVaultForLifecycle(IDataVault vault)
        {
            if (ReferenceEquals(_vault, vault))
                return;

            if (_jobActive)
            {
                ForceCompleteJobInPostSimulationWindow(ref _pendingCullHandle);
                UnlockJobBuffers();
                _jobActive = false;
            }

            UnlockMockSeedBuffers();
            UnlockMockSdfBuffer();
            UnlockSourceManifestBuffer();
            ReleaseDynamicPointLightVaultHandles(_vault);

            _vault = vault;
            ResetNativeEpochState();
        }

        private static bool ForceCompleteJobInPostSimulationWindow(ref JobHandle handle)
        {
            DispatcherJobFence.BeginPostSimulationSwapWindow();
            try
            {
                return DispatcherJobFence.TryComplete(ref handle, forceComplete: true);
            }
            finally
            {
                DispatcherJobFence.EndPostSimulationSwapWindow();
            }
        }

        private void ResetNativeEpochState()
        {
            _pendingCullHandle = default;
            _pendingScheduleTicks = 0L;
            _nativeStorageReady = false;
            _sourceBufferSeeded = false;
            _mockSdfSeeded = false;
            _timeoutFaultPending = false;
            _blackBoxDumped = false;
            _profileRuleCount = 0;
            _activeSourceCount = 0;
            _telemetryWriteCursor = 0;
            _lastGpuUploadBytes = 0UL;
        }

        private bool EnsureNativeStorage(bool allowAllocation = true, bool allowMockGeneration = true)
        {
            IDataVault vault = _vault;
            if (vault == null || (allowAllocation && vault.IsAllocationLocked))
                return false;

            int safeSourceCapacity = math.clamp(sourceCapacity, 128, 16384);
            int gpuCapacity = DynamicPointLightCullingMath.MaximumActiveLights;
            int sdfResolution = math.clamp(mockSdfResolution, 4, 32);
            int sdfCapacity = sdfResolution * sdfResolution * sdfResolution;
            bool hadSourceHandles =
                HasDynamicPointLightHandle(in _sources, DynamicPointLightCullingVaultIds.Sources) ||
                HasDynamicPointLightHandle(in _states, DynamicPointLightCullingVaultIds.States);
            bool vaultHasSourceWindow =
                TryOpenExistingDynamicPointLightBuffer<DynamicPointLightSourceDTO>(DynamicPointLightCullingVaultIds.Sources, safeSourceCapacity, out _) &&
                TryOpenExistingDynamicPointLightBuffer<LightCullStateDTO>(DynamicPointLightCullingVaultIds.States, safeSourceCapacity, out _);
            bool currentSourceWindowValid =
                TryResolveDynamicPointLightBuffer(ref _sources, DynamicPointLightCullingVaultIds.Sources, safeSourceCapacity, out _) &&
                TryResolveDynamicPointLightBuffer(ref _states, DynamicPointLightCullingVaultIds.States, safeSourceCapacity, out _);
            bool sourceBuffersWillChange =
                !vaultHasSourceWindow ||
                (hadSourceHandles && !currentSourceWindowValid);
            bool sdfBufferWillChange = !TryOpenExistingDynamicPointLightBuffer<float>(DynamicPointLightCullingVaultIds.MockSdfSamples, sdfCapacity, out _);

            _sources = AcquireBuffer(ref _sources, DynamicPointLightCullingVaultIds.Sources, safeSourceCapacity, NativeArrayOptions.UninitializedMemory, allowAllocation);
            _states = AcquireBuffer(ref _states, DynamicPointLightCullingVaultIds.States, safeSourceCapacity, NativeArrayOptions.UninitializedMemory, allowAllocation);
            _sourceManifest = AcquireBuffer(ref _sourceManifest, DynamicPointLightCullingVaultIds.SourceManifest, 1, NativeArrayOptions.ClearMemory, allowAllocation);
            if (allowAllocation && sourceBuffersWillChange)
            {
                _sourceBufferSeeded = false;
                _activeSourceCount = 0;
                ClearSourceManifest(safeSourceCapacity);
            }

            _settings = AcquireBuffer(ref _settings, DynamicPointLightCullingVaultIds.Settings, 1, NativeArrayOptions.UninitializedMemory, allowAllocation);
            _gpuPayloadFront = AcquireBuffer(ref _gpuPayloadFront, DynamicPointLightCullingVaultIds.GpuPayloadFront, gpuCapacity, NativeArrayOptions.UninitializedMemory, allowAllocation);
            _gpuPayloadBack = AcquireBuffer(ref _gpuPayloadBack, DynamicPointLightCullingVaultIds.GpuPayloadBack, gpuCapacity, NativeArrayOptions.UninitializedMemory, allowAllocation);
            _telemetryRing = AcquireBuffer(ref _telemetryRing, DynamicPointLightCullingVaultIds.TelemetryRing, DynamicPointLightCullingMath.TelemetryCapacity, NativeArrayOptions.ClearMemory, allowAllocation);
            _telemetryCursor = AcquireBuffer(ref _telemetryCursor, DynamicPointLightCullingVaultIds.TelemetryCursor, 1, NativeArrayOptions.ClearMemory, allowAllocation);
            _importanceKeys = AcquireBuffer(ref _importanceKeys, DynamicPointLightCullingVaultIds.ImportanceKeys, safeSourceCapacity, NativeArrayOptions.UninitializedMemory, allowAllocation);
            _importanceIndices = AcquireBuffer(ref _importanceIndices, DynamicPointLightCullingVaultIds.ImportanceIndices, safeSourceCapacity, NativeArrayOptions.UninitializedMemory, allowAllocation);
            _sortScratchKeys = AcquireBuffer(ref _sortScratchKeys, DynamicPointLightCullingVaultIds.SortScratchKeys, safeSourceCapacity, NativeArrayOptions.UninitializedMemory, allowAllocation);
            _sortScratchIndices = AcquireBuffer(ref _sortScratchIndices, DynamicPointLightCullingVaultIds.SortScratchIndices, safeSourceCapacity, NativeArrayOptions.UninitializedMemory, allowAllocation);
            _csvScratch = AcquireBuffer(ref _csvScratch, DynamicPointLightCullingVaultIds.CsvScratch, DefaultCsvScratchBytes, NativeArrayOptions.UninitializedMemory, allowAllocation);
            _profileRules = AcquireBuffer(ref _profileRules, DynamicPointLightCullingVaultIds.ProfileRules, DefaultProfileCapacity, NativeArrayOptions.UninitializedMemory, allowAllocation);
            _mockSdfSamples = AcquireBuffer(ref _mockSdfSamples, DynamicPointLightCullingVaultIds.MockSdfSamples, sdfCapacity, NativeArrayOptions.UninitializedMemory, allowAllocation);
            if (allowAllocation && sdfBufferWillChange)
                _mockSdfSeeded = false;
            _dynamicProbeLights = AcquireBuffer(ref _dynamicProbeLights, DynamicPointLightCullingVaultIds.DynamicProbeLights, gpuCapacity, NativeArrayOptions.UninitializedMemory, allowAllocation);
            _runtimeCounters = AcquireBuffer(ref _runtimeCounters, DynamicPointLightCullingVaultIds.RuntimeCounters, 1, NativeArrayOptions.ClearMemory, allowAllocation);
            _frustumPlanes = AcquireBuffer(ref _frustumPlanes, DynamicPointLightCullingVaultIds.FrustumPlanes, 6, NativeArrayOptions.UninitializedMemory, allowAllocation);
            _selfAudit = AcquireBuffer(ref _selfAudit, DynamicPointLightCullingVaultIds.SelfAudit, 1, NativeArrayOptions.UninitializedMemory, allowAllocation);

            _nativeStorageReady =
                TryResolveDynamicPointLightBuffer(ref _sources, DynamicPointLightCullingVaultIds.Sources, safeSourceCapacity, out _) &&
                TryResolveDynamicPointLightBuffer(ref _states, DynamicPointLightCullingVaultIds.States, safeSourceCapacity, out _) &&
                TryResolveDynamicPointLightBuffer(ref _sourceManifest, DynamicPointLightCullingVaultIds.SourceManifest, 1, out _) &&
                TryResolveDynamicPointLightBuffer(ref _settings, DynamicPointLightCullingVaultIds.Settings, 1, out _) &&
                TryResolveDynamicPointLightBuffer(ref _gpuPayloadFront, DynamicPointLightCullingVaultIds.GpuPayloadFront, gpuCapacity, out _) &&
                TryResolveDynamicPointLightBuffer(ref _gpuPayloadBack, DynamicPointLightCullingVaultIds.GpuPayloadBack, gpuCapacity, out _) &&
                TryResolveDynamicPointLightBuffer(ref _telemetryRing, DynamicPointLightCullingVaultIds.TelemetryRing, DynamicPointLightCullingMath.TelemetryCapacity, out _) &&
                TryResolveDynamicPointLightBuffer(ref _telemetryCursor, DynamicPointLightCullingVaultIds.TelemetryCursor, 1, out _) &&
                TryResolveDynamicPointLightBuffer(ref _importanceKeys, DynamicPointLightCullingVaultIds.ImportanceKeys, safeSourceCapacity, out _) &&
                TryResolveDynamicPointLightBuffer(ref _importanceIndices, DynamicPointLightCullingVaultIds.ImportanceIndices, safeSourceCapacity, out _) &&
                TryResolveDynamicPointLightBuffer(ref _sortScratchKeys, DynamicPointLightCullingVaultIds.SortScratchKeys, safeSourceCapacity, out _) &&
                TryResolveDynamicPointLightBuffer(ref _sortScratchIndices, DynamicPointLightCullingVaultIds.SortScratchIndices, safeSourceCapacity, out _) &&
                TryResolveDynamicPointLightBuffer(ref _csvScratch, DynamicPointLightCullingVaultIds.CsvScratch, DefaultCsvScratchBytes, out _) &&
                TryResolveDynamicPointLightBuffer(ref _profileRules, DynamicPointLightCullingVaultIds.ProfileRules, DefaultProfileCapacity, out _) &&
                TryResolveDynamicPointLightBuffer(ref _mockSdfSamples, DynamicPointLightCullingVaultIds.MockSdfSamples, sdfCapacity, out _) &&
                TryResolveDynamicPointLightBuffer(ref _dynamicProbeLights, DynamicPointLightCullingVaultIds.DynamicProbeLights, gpuCapacity, out _) &&
                TryResolveDynamicPointLightBuffer(ref _runtimeCounters, DynamicPointLightCullingVaultIds.RuntimeCounters, 1, out _) &&
                TryResolveDynamicPointLightBuffer(ref _frustumPlanes, DynamicPointLightCullingVaultIds.FrustumPlanes, 6, out _) &&
                TryResolveDynamicPointLightBuffer(ref _selfAudit, DynamicPointLightCullingVaultIds.SelfAudit, 1, out _);

            if (!_nativeStorageReady)
                return false;

            if (allowAllocation)
                WriteSelfAudit();
            int committedSourceCount = ReadCommittedSourceCount();
            if (allowAllocation && allowMockGeneration && generateMockDataOnEnable && committedSourceCount <= 0)
                GenerateMockLightCullingData();

            return true;
        }

        private VaultGenerationHandle<T> AcquireBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int length,
            NativeArrayOptions options,
            bool allowAllocation) where T : struct
        {
            IDataVault vault = _vault;
            if (vault == null || vault.IsCompactionFenceActive)
                return default;

            if (TryResolveDynamicPointLightBuffer(ref handle, bufferId, length, out _))
                return handle;

            if (!allowAllocation || vault.IsAllocationLocked)
                return default;

            handle = vault.EnsureGenerationHandle<T>(bufferId, length, MemoryOwner, options);
            if (!TryResolveDynamicPointLightBuffer(ref handle, bufferId, length, out _))
                return default;

            return handle;
        }

        private NativeArray<T> ResolveArray<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength) where T : struct
        {
            return TryResolveDynamicPointLightBuffer(ref handle, bufferId, requiredLength, out NativeArray<T> buffer)
                ? buffer
                : default;
        }

        private NativeArray<T> ResolveArray<T>(ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (handle.BufferID == 0u)
                return default;

            BufferID bufferId = unchecked((BufferID)(int)handle.BufferID);
            return ResolveArray(ref handle, bufferId, 1);
        }

        private bool TryOpenExistingDynamicPointLightBuffer<T>(
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _vault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> handle) ||
                !HasDynamicPointLightHandle(in handle, bufferId) ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                return false;
            }

            return true;
        }

        private bool TryResolveDynamicPointLightBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _vault;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (HasDynamicPointLightHandle(in handle, bufferId) &&
                vault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                return true;
            }

            if (!vault.TryGetGenerationHandle<T>(bufferId, out handle) ||
                !HasDynamicPointLightHandle(in handle, bufferId) ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                handle = default;
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool HasDynamicPointLightHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.SystemID == (uint)MemoryOwner &&
                   handle.Generation != 0u;
        }

        private void ReleaseDynamicPointLightVaultHandles(IDataVault vault)
        {
            ReleaseDynamicPointLightVaultHandle(vault, ref _sources, DynamicPointLightCullingVaultIds.Sources);
            ReleaseDynamicPointLightVaultHandle(vault, ref _states, DynamicPointLightCullingVaultIds.States);
            ReleaseDynamicPointLightVaultHandle(vault, ref _sourceManifest, DynamicPointLightCullingVaultIds.SourceManifest);
            ReleaseDynamicPointLightVaultHandle(vault, ref _settings, DynamicPointLightCullingVaultIds.Settings);
            ReleaseDynamicPointLightVaultHandle(vault, ref _gpuPayloadFront, DynamicPointLightCullingVaultIds.GpuPayloadFront);
            ReleaseDynamicPointLightVaultHandle(vault, ref _gpuPayloadBack, DynamicPointLightCullingVaultIds.GpuPayloadBack);
            ReleaseDynamicPointLightVaultHandle(vault, ref _telemetryRing, DynamicPointLightCullingVaultIds.TelemetryRing);
            ReleaseDynamicPointLightVaultHandle(vault, ref _telemetryCursor, DynamicPointLightCullingVaultIds.TelemetryCursor);
            ReleaseDynamicPointLightVaultHandle(vault, ref _importanceKeys, DynamicPointLightCullingVaultIds.ImportanceKeys);
            ReleaseDynamicPointLightVaultHandle(vault, ref _importanceIndices, DynamicPointLightCullingVaultIds.ImportanceIndices);
            ReleaseDynamicPointLightVaultHandle(vault, ref _sortScratchKeys, DynamicPointLightCullingVaultIds.SortScratchKeys);
            ReleaseDynamicPointLightVaultHandle(vault, ref _sortScratchIndices, DynamicPointLightCullingVaultIds.SortScratchIndices);
            ReleaseDynamicPointLightVaultHandle(vault, ref _csvScratch, DynamicPointLightCullingVaultIds.CsvScratch);
            ReleaseDynamicPointLightVaultHandle(vault, ref _profileRules, DynamicPointLightCullingVaultIds.ProfileRules);
            ReleaseDynamicPointLightVaultHandle(vault, ref _mockSdfSamples, DynamicPointLightCullingVaultIds.MockSdfSamples);
            ReleaseDynamicPointLightVaultHandle(vault, ref _dynamicProbeLights, DynamicPointLightCullingVaultIds.DynamicProbeLights);
            ReleaseDynamicPointLightVaultHandle(vault, ref _runtimeCounters, DynamicPointLightCullingVaultIds.RuntimeCounters);
            ReleaseDynamicPointLightVaultHandle(vault, ref _frustumPlanes, DynamicPointLightCullingVaultIds.FrustumPlanes);
            ReleaseDynamicPointLightVaultHandle(vault, ref _selfAudit, DynamicPointLightCullingVaultIds.SelfAudit);
        }

        private static void ReleaseDynamicPointLightVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            if (vault != null && HasDynamicPointLightHandle(in handle, bufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private void TryRegisterDispatch()
        {
            if (!_registeredTick)
                _registeredTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            if (!_registeredSlowTick)
                _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void ShutdownRuntime()
        {
            if (_jobActive)
            {
                // Teardown drain: release Vault locks before unregistering this owner.
                ForceCompleteJobInPostSimulationWindow(ref _pendingCullHandle);
                UnlockJobBuffers();
                _jobActive = false;
            }

            UnlockMockSeedBuffers();
            UnlockMockSdfBuffer();
            UnlockSourceManifestBuffer();
            ReleaseDynamicPointLightVaultHandles(_vault);
            ResetNativeEpochState();

            if (_registeredTick)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredTick = false;
            }

            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlowTick = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }

            TryUnregisterHotSwapListener();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.DataVault:
                    RebindDataVaultForLifecycle(currentService as IDataVault);

                    if (_vault != null && isActiveAndEnabled)
                        EnsureNativeStorage(allowAllocation: true, allowMockGeneration: false);
                    break;

                case GlobalRegistryServiceSlot.Player:
                    _playerContext = currentService as IPlayerRuntimeContext;
                    break;

                case GlobalRegistryServiceSlot.Dispatcher:
                    _registeredTick = false;
                    _registeredSlowTick = false;
                    _registeredLateFrame = false;
                    if (currentService != null)
                        TryRegisterDispatch();
                    break;
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }

        private DynamicPointLightCullingSettingsDTO BuildSettings(float quality)
        {
            double3 cameraAup = ResolveCameraAup();
            float thermal = DynamicPointLightCullingMath.Sanitize01(HomeostasisBrain.SystemHealthIndex01, 0f);
            int maxActive = DynamicPointLightCullingMath.ResolveMaxActiveLights(quality, thermal);
            int activeSourceCount = ReadCommittedSourceCount();
            DynamicPointLightCullingSettingsDTO settings = default;
            settings.CameraAup = cameraAup;
            settings.GlobalQualityWeight = quality;
            settings.ThermalPressure01 = thermal;
            float safeFadeMeters = math.max(1f, DynamicPointLightCullingMath.SanitizeFinite(baseFadeDistanceMeters, 320f));
            settings.BaseFadeDistanceSq = math.max(1f, safeFadeMeters * safeFadeMeters);
            settings.ImportanceWeight = math.max(0.0001f, DynamicPointLightCullingMath.SanitizeFinite(importanceWeight, 1f));
            settings.SdfOcclusionThreshold = DynamicPointLightCullingMath.SanitizeFinite(sdfOcclusionThreshold, -0.05f);
            settings.ActiveSourceCount = math.clamp(activeSourceCount, 0, sourceCapacity);
            settings.MaxActiveLights = maxActive;
            settings.FrameIndex = _frameSequence;
            settings.SdfSampleCount = _mockSdfSeeded ? math.max(0, ResolveArray(ref _mockSdfSamples).Length) : 0;
            settings.SdfOriginAup = cameraAup;
            settings.SdfCellSizeMeters = math.max(0.01f, DynamicPointLightCullingMath.SanitizeFinite(mockSdfCellSizeMeters, 64f));
            settings.SdfGridResolution = math.clamp(mockSdfResolution, 4, 32);
            settings.BounceGain = math.max(0f, DynamicPointLightCullingMath.SanitizeFinite(bounceGain, 0.35f));
            settings.NearFieldOverkillBoost = math.max(0f, DynamicPointLightCullingMath.SanitizeFinite(nearFieldOverkillBoost, 0.35f));
            settings.ThermalFadeStrength = math.max(0f, DynamicPointLightCullingMath.SanitizeFinite(thermalFadeStrength, 0.65f));
            settings.MaxRangeMeters = math.max(1f, DynamicPointLightCullingMath.SanitizeFinite(maxRangeMeters, 4096f));
            settings.SubmitIntensityEpsilon = math.max(0.000001f, DynamicPointLightCullingMath.SanitizeFinite(submitIntensityEpsilon, 0.0005f));
            settings.FrustumPlaneCount = 6;
            settings.SettingsHash = HashSettings(in settings);
            return settings;
        }

        private float ResolveQualityWeight()
        {
            return editorQualityOverride >= 0f
                ? DynamicPointLightCullingMath.Sanitize01(editorQualityOverride, 1f)
                : DynamicPointLightCullingMath.Sanitize01(HomeostasisBrain.GlobalQualityWeight, 1f);
        }

        private static float ResolveScheduleCadence(float quality, float thermal)
        {
            float pressure = DynamicPointLightCullingMath.Sanitize01(thermal, 0f);
            float cadenceT = math.saturate(quality * (1f - pressure * 0.5f));
            float curved = cadenceT * cadenceT * (3f - 2f * cadenceT);
            return math.lerp(MaximumScheduleCadence, MinimumScheduleCadence, curved);
        }

        private double3 ResolveCameraAup()
        {
            IPlayerRuntimeContext player = _playerContext;
            if (player != null && player.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot))
                return snapshot.Aup.ToAbsoluteDouble3();

            var playerMovement = player != null ? player.PlayerMovement : null;
            if (playerMovement != null)
            {
                AbsoluteUniversePosition currentAup = playerMovement.CurrentAup;
                if (currentAup.IsFinite())
                    return currentAup.ToAbsoluteDouble3();
            }

            return HectonFloatingOrigin.CurrentTotalOffsetDouble;
        }

        private Camera ResolveRenderCamera()
        {
            if (renderCamera != null)
                return renderCamera;

            IPlayerRuntimeContext player = _playerContext;
            if (player != null && player.PlayerCamera != null)
            {
                renderCamera = player.PlayerCamera;
                return renderCamera;
            }

            return null;
        }

        private void WriteSettings(DynamicPointLightCullingSettingsDTO settings)
        {
            NativeArray<DynamicPointLightCullingSettingsDTO> array = ResolveArray(ref _settings);
            if (array.IsCreated && array.Length > 0)
                array[0] = settings;
        }

        private int ReadCommittedSourceCount()
        {
            NativeArray<DynamicPointLightSourceManifestDTO> array = ResolveArray(ref _sourceManifest);
            if (!array.IsCreated || array.Length == 0)
            {
                _activeSourceCount = 0;
                _sourceBufferSeeded = false;
                return 0;
            }

            DynamicPointLightSourceManifestDTO manifest = array[0];
            if ((manifest.Flags & DynamicPointLightSourceManifestFlags.Committed) == 0u)
            {
                _activeSourceCount = 0;
                _sourceBufferSeeded = false;
                return 0;
            }

            NativeArray<DynamicPointLightSourceDTO> sources = ResolveArray(ref _sources);
            NativeArray<LightCullStateDTO> states = ResolveArray(ref _states);
            int capacity = math.min(sourceCapacity, math.min(sources.IsCreated ? sources.Length : 0, states.IsCreated ? states.Length : 0));
            int count = math.clamp(manifest.ActiveSourceCount, 0, capacity);
            _activeSourceCount = count;
            _sourceBufferSeeded = count > 0;
            return count;
        }

        private void ClearSourceManifest(int capacity)
        {
            NativeArray<DynamicPointLightSourceManifestDTO> array = ResolveArray(ref _sourceManifest);
            if (!array.IsCreated || array.Length == 0)
                return;

            DynamicPointLightSourceManifestDTO manifest = default;
            manifest.SourceCapacity = math.max(0, capacity);
            manifest.VaultGeneration = _sourceManifest.Generation;
            array[0] = manifest;
        }

        private void CommitSourceManifest(int count, int capacity, uint flags, uint writerHash)
        {
            NativeArray<DynamicPointLightSourceManifestDTO> array = ResolveArray(ref _sourceManifest);
            if (!array.IsCreated || array.Length == 0)
                return;

            int safeCapacity = math.max(0, capacity);
            int safeCount = math.clamp(count, 0, safeCapacity);
            DynamicPointLightSourceManifestDTO previous = array[0];
            DynamicPointLightSourceManifestDTO manifest = default;
            manifest.ActiveSourceCount = safeCount;
            manifest.SourceCapacity = safeCapacity;
            manifest.WriterHash = writerHash;
            manifest.SourceRevision = previous.SourceRevision + 1u;
            manifest.Flags = flags;
            manifest.LastCommitFrame = _frameSequence;
            manifest.RejectedSourceCount = math.max(0, count - safeCount);
            manifest.VaultGeneration = _sourceManifest.Generation;
            array[0] = manifest;

            _activeSourceCount = safeCount;
            _sourceBufferSeeded = safeCount > 0;
        }

        private void WriteFrustumPlanes()
        {
            NativeArray<float4> planes = ResolveArray(ref _frustumPlanes);
            if (!planes.IsCreated || planes.Length < 6)
                return;

            Camera camera = ResolveRenderCamera();
            if (camera == null)
            {
                for (int i = 0; i < 6; i++)
                    planes[i] = default;
                return;
            }

            Matrix4x4 viewProjection = camera.projectionMatrix * camera.worldToCameraMatrix;
            Vector3 cameraPosition = camera.transform.position;
            planes[0] = BuildCameraLocalPlane(viewProjection.m30 + viewProjection.m00, viewProjection.m31 + viewProjection.m01, viewProjection.m32 + viewProjection.m02, viewProjection.m33 + viewProjection.m03, cameraPosition);
            planes[1] = BuildCameraLocalPlane(viewProjection.m30 - viewProjection.m00, viewProjection.m31 - viewProjection.m01, viewProjection.m32 - viewProjection.m02, viewProjection.m33 - viewProjection.m03, cameraPosition);
            planes[2] = BuildCameraLocalPlane(viewProjection.m30 + viewProjection.m10, viewProjection.m31 + viewProjection.m11, viewProjection.m32 + viewProjection.m12, viewProjection.m33 + viewProjection.m13, cameraPosition);
            planes[3] = BuildCameraLocalPlane(viewProjection.m30 - viewProjection.m10, viewProjection.m31 - viewProjection.m11, viewProjection.m32 - viewProjection.m12, viewProjection.m33 - viewProjection.m13, cameraPosition);
            planes[4] = BuildCameraLocalPlane(viewProjection.m30 + viewProjection.m20, viewProjection.m31 + viewProjection.m21, viewProjection.m32 + viewProjection.m22, viewProjection.m33 + viewProjection.m23, cameraPosition);
            planes[5] = BuildCameraLocalPlane(viewProjection.m30 - viewProjection.m20, viewProjection.m31 - viewProjection.m21, viewProjection.m32 - viewProjection.m22, viewProjection.m33 - viewProjection.m23, cameraPosition);
        }

        private static float4 BuildCameraLocalPlane(float x, float y, float z, float w, Vector3 cameraPosition)
        {
            float3 normal = new float3(x, y, z);
            float invLength = math.rsqrt(math.max(0.000001f, math.lengthsq(normal)));
            normal *= invLength;
            float distance = w * invLength;
            float localDistance = math.dot(normal, new float3(cameraPosition.x, cameraPosition.y, cameraPosition.z)) + distance;
            return math.all(math.isfinite(new float4(normal, localDistance))) ? new float4(normal, localDistance) : default;
        }

        private void ScheduleCullingPipeline(DynamicPointLightCullingSettingsDTO settings)
        {
            if (!TryLockJobBuffers())
                return;

            bool keepJobPins = false;
            try
            {
                NativeArray<DynamicPointLightSourceDTO> sources = ResolveArray(ref _sources);
                NativeArray<LightCullStateDTO> states = ResolveArray(ref _states);
                NativeArray<float4> planes = ResolveArray(ref _frustumPlanes);
                NativeArray<float> sdf = ResolveArray(ref _mockSdfSamples);
                NativeArray<DynamicPointLightProfileRuleDTO> rules = ResolveArray(ref _profileRules);
                NativeArray<uint> keys = ResolveArray(ref _importanceKeys);
                NativeArray<int> indices = ResolveArray(ref _importanceIndices);
                NativeArray<uint> scratchKeys = ResolveArray(ref _sortScratchKeys);
                NativeArray<int> scratchIndices = ResolveArray(ref _sortScratchIndices);
                NativeArray<DynamicPointLightGpuDTO> gpu = ResolveScheduledGpuPayload();
                NativeArray<CustomDynamicProbeLightDTO> probeLights = ResolveArray(ref _dynamicProbeLights);
                NativeArray<DynamicPointLightRuntimeCountersDTO> counters = ResolveArray(ref _runtimeCounters);

                if (!sources.IsCreated ||
                    !states.IsCreated ||
                    !planes.IsCreated ||
                    !sdf.IsCreated ||
                    !rules.IsCreated ||
                    !keys.IsCreated ||
                    !indices.IsCreated ||
                    !scratchKeys.IsCreated ||
                    !scratchIndices.IsCreated ||
                    !gpu.IsCreated ||
                    !probeLights.IsCreated ||
                    !counters.IsCreated)
                    return;

                int count = math.min(settings.ActiveSourceCount, math.min(sources.Length, states.Length));
                if (count <= 0)
                    return;

                _scheduledPayloadIndex = _payloadWriteIndex;
                _pendingScheduleTicks = Stopwatch.GetTimestamp();
                _blackBoxDumped = false;
                JobHandle eval = new EvaluateLightCullingJob
                {
                    Sources = sources,
                    FrustumPlanes = planes,
                    SdfSamples = sdf,
                    ProfileRules = rules,
                    States = states,
                    ImportanceKeys = keys,
                    ImportanceIndices = indices,
                    Settings = settings,
                    ProfileRuleCount = _profileRuleCount
                }.Schedule(count, 64);

                JobHandle sort = new SortLightImportanceJob
                {
                    Keys = keys,
                    Indices = indices,
                    ScratchKeys = scratchKeys,
                    ScratchIndices = scratchIndices,
                    Count = count
                }.Schedule(eval);

                _pendingCullHandle = new BuildLightGpuPayloadJob
                {
                    Sources = sources,
                    States = states,
                    SortedIndices = indices,
                    GpuPayload = gpu,
                    DynamicProbeLights = probeLights,
                    Counters = counters,
                    Settings = settings,
                    Count = count,
                    GpuCapacity = DynamicPointLightCullingMath.MaximumActiveLights
                }.Schedule(sort);

                H8Memory.RegisterActiveJob(MemoryOwner, _pendingCullHandle);
                _jobActive = true;
                keepJobPins = true;
            }
            finally
            {
                if (!keepJobPins)
                    UnlockJobBuffers();
            }
        }

        private NativeArray<DynamicPointLightGpuDTO> ResolveScheduledGpuPayload()
        {
            return _payloadWriteIndex == 0
                ? ResolveArray(ref _gpuPayloadFront)
                : ResolveArray(ref _gpuPayloadBack);
        }

        private NativeArray<DynamicPointLightGpuDTO> ResolveCompletedGpuPayload()
        {
            return _scheduledPayloadIndex == 0
                ? ResolveArray(ref _gpuPayloadFront)
                : ResolveArray(ref _gpuPayloadBack);
        }

        private bool TryLockJobBuffers()
        {
            IDataVault vault = _vault;
            if (vault == null)
                return false;

            if (_jobPinsHeld)
                return true;

            _jobPinVault = vault;
            try
            {
                if (!TryLockJobBuffer(vault, DynamicPointLightCullingVaultIds.Sources, JobPinSources) ||
                    !TryLockJobBuffer(vault, DynamicPointLightCullingVaultIds.States, JobPinStates) ||
                    !TryLockJobBuffer(vault, DynamicPointLightCullingVaultIds.FrustumPlanes, JobPinFrustumPlanes) ||
                    !TryLockJobBuffer(vault, DynamicPointLightCullingVaultIds.MockSdfSamples, JobPinMockSdfSamples) ||
                    !TryLockJobBuffer(vault, DynamicPointLightCullingVaultIds.ProfileRules, JobPinProfileRules) ||
                    !TryLockJobBuffer(vault, DynamicPointLightCullingVaultIds.ImportanceKeys, JobPinImportanceKeys) ||
                    !TryLockJobBuffer(vault, DynamicPointLightCullingVaultIds.ImportanceIndices, JobPinImportanceIndices) ||
                    !TryLockJobBuffer(vault, DynamicPointLightCullingVaultIds.SortScratchKeys, JobPinSortScratchKeys) ||
                    !TryLockJobBuffer(vault, DynamicPointLightCullingVaultIds.SortScratchIndices, JobPinSortScratchIndices) ||
                    !TryLockJobBuffer(vault, DynamicPointLightCullingVaultIds.GpuPayloadFront, JobPinGpuPayloadFront) ||
                    !TryLockJobBuffer(vault, DynamicPointLightCullingVaultIds.GpuPayloadBack, JobPinGpuPayloadBack) ||
                    !TryLockJobBuffer(vault, DynamicPointLightCullingVaultIds.DynamicProbeLights, JobPinDynamicProbeLights) ||
                    !TryLockJobBuffer(vault, DynamicPointLightCullingVaultIds.RuntimeCounters, JobPinRuntimeCounters))
                    return false;

                _jobPinsHeld = true;
                return true;
            }
            finally
            {
                if (!_jobPinsHeld)
                    UnlockJobBuffers();
            }
        }

        private void UnlockJobBuffers()
        {
            IDataVault vault = _jobPinVault;
            uint pinMask = _jobPinMask;
            if (vault != null && pinMask != 0u)
            {
                TryUnlockJobBuffer(vault, pinMask, JobPinRuntimeCounters, DynamicPointLightCullingVaultIds.RuntimeCounters);
                TryUnlockJobBuffer(vault, pinMask, JobPinDynamicProbeLights, DynamicPointLightCullingVaultIds.DynamicProbeLights);
                TryUnlockJobBuffer(vault, pinMask, JobPinGpuPayloadBack, DynamicPointLightCullingVaultIds.GpuPayloadBack);
                TryUnlockJobBuffer(vault, pinMask, JobPinGpuPayloadFront, DynamicPointLightCullingVaultIds.GpuPayloadFront);
                TryUnlockJobBuffer(vault, pinMask, JobPinSortScratchIndices, DynamicPointLightCullingVaultIds.SortScratchIndices);
                TryUnlockJobBuffer(vault, pinMask, JobPinSortScratchKeys, DynamicPointLightCullingVaultIds.SortScratchKeys);
                TryUnlockJobBuffer(vault, pinMask, JobPinImportanceIndices, DynamicPointLightCullingVaultIds.ImportanceIndices);
                TryUnlockJobBuffer(vault, pinMask, JobPinImportanceKeys, DynamicPointLightCullingVaultIds.ImportanceKeys);
                TryUnlockJobBuffer(vault, pinMask, JobPinProfileRules, DynamicPointLightCullingVaultIds.ProfileRules);
                TryUnlockJobBuffer(vault, pinMask, JobPinMockSdfSamples, DynamicPointLightCullingVaultIds.MockSdfSamples);
                TryUnlockJobBuffer(vault, pinMask, JobPinFrustumPlanes, DynamicPointLightCullingVaultIds.FrustumPlanes);
                TryUnlockJobBuffer(vault, pinMask, JobPinStates, DynamicPointLightCullingVaultIds.States);
                TryUnlockJobBuffer(vault, pinMask, JobPinSources, DynamicPointLightCullingVaultIds.Sources);
            }

            _jobPinMask = 0u;
            _jobPinVault = null;
            _jobPinsHeld = false;
        }

        private bool TryLockJobBuffer(IDataVault vault, BufferID bufferId, uint pinBit)
        {
            if ((_jobPinMask & pinBit) != 0u)
                return true;

            if (vault == null ||
                !vault.TryLockBuffer(bufferId, MemoryOwner))
                return false;

            _jobPinMask |= pinBit;
            return true;
        }

        private static void TryUnlockJobBuffer(IDataVault vault, uint pinMask, uint pinBit, BufferID bufferId)
        {
            if ((pinMask & pinBit) != 0u)
                vault.TryUnlockBuffer(bufferId, MemoryOwner);
        }

        private bool TryLockMockSeedBuffers()
        {
            IDataVault vault = _vault;
            if (vault == null)
                return false;

            if (_mockSeedGuardHeld)
                return true;
            if (!vault.TryAcquireMutationGuard(MockSeedMutationGuardMask))
                return false;

            _mockSeedGuardHeld = true;
            return true;
        }

        private void UnlockMockSeedBuffers()
        {
            IDataVault vault = _vault;
            if (vault != null && _mockSeedGuardHeld)
                vault.ReleaseMutationGuard(MockSeedMutationGuardMask);

            _mockSeedGuardHeld = false;
        }

        private bool TryLockMockSdfBuffer()
        {
            IDataVault vault = _vault;
            if (vault == null)
                return false;

            if (_mockSdfGuardHeld)
                return true;
            if (!vault.TryAcquireMutationGuard(MockSdfMutationGuardMask))
                return false;

            _mockSdfGuardHeld = true;
            return true;
        }

        private void UnlockMockSdfBuffer()
        {
            IDataVault vault = _vault;
            if (vault != null && _mockSdfGuardHeld)
                vault.ReleaseMutationGuard(MockSdfMutationGuardMask);

            _mockSdfGuardHeld = false;
        }

        private bool TryLockSourceManifestBuffer()
        {
            IDataVault vault = _vault;
            if (vault == null)
                return false;

            if (_sourceManifestGuardHeld)
                return true;
            if (!vault.TryAcquireMutationGuard(SourceManifestMutationGuardMask))
                return false;

            _sourceManifestGuardHeld = true;
            return true;
        }

        private void UnlockSourceManifestBuffer()
        {
            IDataVault vault = _vault;
            if (vault != null && _sourceManifestGuardHeld)
                vault.ReleaseMutationGuard(SourceManifestMutationGuardMask);

            _sourceManifestGuardHeld = false;
        }

        private void UploadScheduledPayload()
        {
            NativeArray<DynamicPointLightGpuDTO> payload = ResolveCompletedGpuPayload();
            if (!payload.IsCreated)
                return;

            if (!TryGetCountersCopy(out DynamicPointLightRuntimeCountersDTO counters))
                return;

            int submitted = math.clamp(counters.SubmittedLights, 0, math.min(payload.Length, DynamicPointLightCullingMath.MaximumActiveLights));
            if (!HasGpuBuffersReady(DynamicPointLightCullingMath.MaximumActiveLights))
            {
                _lastGpuUploadBytes = 0UL;
                return;
            }

            GraphicsBuffer target = _gpuUploadWriteIndex == 0 ? _gpuBufferA : _gpuBufferB;
            if (target == null)
                return;

            if (submitted > 0)
            {
                NativeArray<DynamicPointLightGpuDTO> mapped = target.LockBufferForWrite<DynamicPointLightGpuDTO>(0, submitted);
                try
                {
                    void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(payload);
                    void* targetPtr = NativeArrayUnsafeUtility.GetUnsafePtr(mapped);
                    long bytes = (long)UnsafeUtility.SizeOf<DynamicPointLightGpuDTO>() * submitted;
                    UnsafeUtility.MemCpy(targetPtr, sourcePtr, bytes);
                    _lastGpuUploadBytes = (ulong)bytes;
                }
                finally
                {
                    target.UnlockBufferAfterWrite<DynamicPointLightGpuDTO>(submitted);
                }
            }
            else
            {
                _lastGpuUploadBytes = 0UL;
            }

            Shader.SetGlobalBuffer(_DynamicLightBufferId, target);
            Vector4 state = default;
            state.x = submitted;
            state.y = counters.MaxActiveLights;
            state.z = counters.QualityWeight;
            state.w = counters.ThermalPressure01;
            Shader.SetGlobalVector(_DynamicLightStateId, state);
            Vector4 cameraResidue = ResolveShaderCameraAupResidue();
            Shader.SetGlobalVector(_DynamicLightCameraAupId, cameraResidue);

            _gpuUploadWriteIndex = 1 - _gpuUploadWriteIndex;
            _payloadWriteIndex = 1 - _payloadWriteIndex;
        }

        private bool HasGpuBuffersReady(int capacity)
        {
            int stride = UnsafeUtility.SizeOf<DynamicPointLightGpuDTO>();
            return _gpuBufferA != null && _gpuBufferA.count >= capacity && _gpuBufferA.stride == stride &&
                   _gpuBufferB != null && _gpuBufferB.count >= capacity && _gpuBufferB.stride == stride;
        }

        private void EnsureGpuBuffersCold(int capacity)
        {
            int stride = UnsafeUtility.SizeOf<DynamicPointLightGpuDTO>();
            if (_gpuBufferA == null || _gpuBufferA.count < capacity || _gpuBufferA.stride != stride)
            {
                _gpuBufferA?.Release();
                _gpuBufferA = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.LockBufferForWrite, capacity, stride);
            }

            if (_gpuBufferB == null || _gpuBufferB.count < capacity || _gpuBufferB.stride != stride)
            {
                _gpuBufferB?.Release();
                _gpuBufferB = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.LockBufferForWrite, capacity, stride);
            }
        }

        private Vector4 ResolveShaderCameraAupResidue()
        {
            if (!TryGetSettingsCopy(out DynamicPointLightCullingSettingsDTO settings))
                return Vector4.zero;

            double3 camera = settings.CameraAup;
            double3 residue = camera - math.floor(camera / 1024.0d) * 1024.0d;
            Vector4 value = default;
            value.x = (float)residue.x;
            value.y = (float)residue.y;
            value.z = (float)residue.z;
            value.w = settings.GlobalQualityWeight;
            return value;
        }

        private void ReleaseGpuBuffers()
        {
            if (_gpuBufferA != null)
            {
                _gpuBufferA.Release();
                _gpuBufferA = null;
            }

            if (_gpuBufferB != null)
            {
                _gpuBufferB.Release();
                _gpuBufferB = null;
            }
        }

        private void RecordTelemetry(float elapsedUs)
        {
            NativeArray<DynamicPointLightCullingTelemetryEntry> ring = ResolveArray(ref _telemetryRing);
            if (!ring.IsCreated || ring.Length == 0)
                return;

            if (!TryGetCountersCopy(out DynamicPointLightRuntimeCountersDTO counters))
                return;

            int cursor = _telemetryWriteCursor;
            if ((uint)cursor >= (uint)ring.Length)
                cursor = 0;

            DynamicPointLightCullingTelemetryEntry entry = default;
            entry.Frame = counters.Frame;
            entry.TotalLights = counters.TotalLights;
            entry.CulledLights = counters.CulledLights;
            entry.SubmittedLights = counters.SubmittedLights;
            entry.BurstCpuUs = math.max(0f, elapsedUs);
            entry.GlobalQualityWeight = counters.QualityWeight;
            entry.ThermalPressure01 = counters.ThermalPressure01;
            entry.Flags = counters.Flags;
            entry.StateHash = counters.StateHash;
            entry.MaxActiveLights = counters.MaxActiveLights;
            entry.MaxDistanceSq = counters.MaxDistanceSq;
            entry.AverageIntensity = counters.AverageSubmittedIntensity;
            entry.LastGpuUploadBytes = _lastGpuUploadBytes;
            entry.VaultGeneration = _telemetryRing.Generation;
            ring[cursor] = entry;

            cursor++;
            if (cursor >= ring.Length)
                cursor = 0;
            _telemetryWriteCursor = cursor;

            NativeArray<int> cursorArray = ResolveArray(ref _telemetryCursor);
            if (cursorArray.IsCreated && cursorArray.Length > 0)
                cursorArray[0] = cursor;
        }

        private void RecordTimeoutFault()
        {
            NativeArray<DynamicPointLightRuntimeCountersDTO> counters = ResolveArray(ref _runtimeCounters);
            if (!counters.IsCreated || counters.Length == 0)
                return;

            DynamicPointLightRuntimeCountersDTO entry = counters[0];
            entry.Flags |= DynamicPointLightCullingFlags.TimedOut;
            counters[0] = entry;
        }

        private bool GenerateMockSdfSamples(DynamicPointLightCullingSettingsDTO settings)
        {
            if (!TryLockMockSdfBuffer())
                return false;

            try
            {
                NativeArray<float> samples = ResolveArray(ref _mockSdfSamples);
                if (!samples.IsCreated || samples.Length == 0)
                    return false;

                JobHandle handle = new GenerateMockLightSdfSamplesJob
                {
                    Samples = samples,
                    Resolution = settings.SdfGridResolution,
                    CellSizeMeters = settings.SdfCellSizeMeters
                }.Schedule(samples.Length, 64);
                H8Memory.RegisterActiveJob(MemoryOwner, handle);
                // COLD SYNC JOB: editor SDF seed fence; gameplay culling treats unseeded SDF as absent.
                ForceCompleteJobInPostSimulationWindow(ref handle);
                return true;
            }
            finally
            {
                UnlockMockSdfBuffer();
            }
        }

#if UNITY_EDITOR
        private bool TryLoadProfilesFromCsv()
        {
            if (!_nativeStorageReady && !EnsureNativeStorage(allowAllocation: true, allowMockGeneration: false))
                return false;

            NativeArray<byte> csv = ResolveArray(ref _csvScratch);
            NativeArray<DynamicPointLightProfileRuleDTO> rules = ResolveArray(ref _profileRules);
            if (!csv.IsCreated || !rules.IsCreated)
                return false;

            string path = ResolveProjectPath(profileCsvRelativePath);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            int count = 0;
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(csv);
                    Span<byte> destination = new Span<byte>(ptr, csv.Length);
                    while (count < csv.Length)
                    {
                        int read = stream.Read(destination.Slice(count));
                        if (read <= 0)
                            break;

                        count += read;
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

            _profileRuleCount = DynamicPointLightProfileCsvParser.Parse(csv, count, rules, rules.Length, out _);
            return _profileRuleCount > 0;
        }
#endif

        private void WriteSelfAudit()
        {
            NativeArray<DynamicPointLightSelfAuditDTO> auditArray = ResolveArray(ref _selfAudit);
            if (!auditArray.IsCreated || auditArray.Length == 0)
                return;

            int lightCullStateSize = UnsafeUtility.SizeOf<LightCullStateDTO>();
            int sourceSize = UnsafeUtility.SizeOf<DynamicPointLightSourceDTO>();
            int gpuPayloadSize = UnsafeUtility.SizeOf<DynamicPointLightGpuDTO>();
            int telemetrySize = UnsafeUtility.SizeOf<DynamicPointLightCullingTelemetryEntry>();
            int settingsSize = UnsafeUtility.SizeOf<DynamicPointLightCullingSettingsDTO>();
            int profileRuleSize = UnsafeUtility.SizeOf<DynamicPointLightProfileRuleDTO>();
            int sourceManifestSize = UnsafeUtility.SizeOf<DynamicPointLightSourceManifestDTO>();
            int runtimeCountersSize = UnsafeUtility.SizeOf<DynamicPointLightRuntimeCountersDTO>();
            int selfAuditSize = UnsafeUtility.SizeOf<DynamicPointLightSelfAuditDTO>();

            DynamicPointLightSelfAuditDTO audit = default;
            audit.LightCullStateSize = lightCullStateSize;
            audit.SourceSize = sourceSize;
            audit.GpuPayloadSize = gpuPayloadSize;
            audit.TelemetrySize = telemetrySize;
            audit.SettingsSize = settingsSize;
            audit.ProfileRuleSize = profileRuleSize;
            audit.SourceBufferId = (int)DynamicPointLightCullingVaultIds.Sources;
            audit.StateBufferId = (int)DynamicPointLightCullingVaultIds.States;
            audit.GpuFrontBufferId = (int)DynamicPointLightCullingVaultIds.GpuPayloadFront;
            audit.GpuBackBufferId = (int)DynamicPointLightCullingVaultIds.GpuPayloadBack;
            audit.TelemetryBufferId = (int)DynamicPointLightCullingVaultIds.TelemetryRing;
            audit.MaxMockLights = DynamicPointLightCullingMath.DefaultMockLightCount;
            audit.Flags = DynamicPointLightCullingFlags.GpuDirty |
                          (IsSelfAuditLayoutValid(
                              lightCullStateSize,
                              sourceSize,
                              gpuPayloadSize,
                              telemetrySize,
                              settingsSize,
                              profileRuleSize,
                              sourceManifestSize,
                              runtimeCountersSize,
                              selfAuditSize)
                              ? DynamicPointLightCullingFlags.LayoutAligned
                              : DynamicPointLightCullingFlags.LayoutInvalid);
            audit.SourceHash = DynamicPointLightCullingMath.SourceHash;
            audit.SourceManifestBufferId = (int)DynamicPointLightCullingVaultIds.SourceManifest;
            audit.SourceManifestSize = sourceManifestSize;
            auditArray[0] = audit;
        }

        private static bool IsSelfAuditLayoutValid(
            int lightCullStateSize,
            int sourceSize,
            int gpuPayloadSize,
            int telemetrySize,
            int settingsSize,
            int profileRuleSize,
            int sourceManifestSize,
            int runtimeCountersSize,
            int selfAuditSize)
        {
            return lightCullStateSize == DynamicPointLightCullingLayout.LightCullStateStrideBytes &&
                   sourceSize == DynamicPointLightCullingLayout.SourceStrideBytes &&
                   gpuPayloadSize == DynamicPointLightCullingLayout.GpuPayloadStrideBytes &&
                   telemetrySize == DynamicPointLightCullingLayout.TelemetryEntryStrideBytes &&
                   settingsSize == DynamicPointLightCullingLayout.SettingsStrideBytes &&
                   profileRuleSize == DynamicPointLightCullingLayout.ProfileRuleStrideBytes &&
                   sourceManifestSize == DynamicPointLightCullingLayout.SourceManifestStrideBytes &&
                   runtimeCountersSize == DynamicPointLightCullingLayout.RuntimeCountersStrideBytes &&
                   selfAuditSize == DynamicPointLightCullingLayout.SelfAuditStrideBytes &&
                   IsAligned8(lightCullStateSize) &&
                   IsAligned8(sourceSize) &&
                   IsAligned8(gpuPayloadSize) &&
                   IsAligned8(telemetrySize) &&
                   IsAligned8(settingsSize) &&
                   IsAligned8(profileRuleSize) &&
                   IsAligned8(sourceManifestSize) &&
                   IsAligned8(runtimeCountersSize) &&
                   IsAligned8(selfAuditSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsAligned8(int sizeBytes)
        {
            return sizeBytes > 0 && (sizeBytes & 7) == 0;
        }

        private static uint HashSettings(in DynamicPointLightCullingSettingsDTO settings)
        {
            uint hash = 2166136261u;
            hash = DynamicPointLightCullingMath.FnvaByte(hash, (byte)settings.MaxActiveLights);
            hash = DynamicPointLightCullingMath.FnvaByte(hash, (byte)settings.ActiveSourceCount);
            hash = DynamicPointLightCullingMath.FnvaByte(hash, (byte)math.asuint(settings.GlobalQualityWeight));
            hash = DynamicPointLightCullingMath.FnvaByte(hash, (byte)math.asuint(settings.ThermalPressure01));
            hash = DynamicPointLightCullingMath.FnvaByte(hash, (byte)settings.FrameIndex);
            return hash;
        }

        private static string ResolveProjectPath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return null;

            string root = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.Combine(root, relativePath);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            sourceCapacity = math.clamp(sourceCapacity, 128, 16384);
            baseFadeDistanceMeters = math.clamp(baseFadeDistanceMeters, 4f, 96f);
            importanceWeight = math.clamp(importanceWeight, 0.05f, 8f);
            sdfOcclusionThreshold = math.clamp(sdfOcclusionThreshold, -4f, 4f);
            maxRangeMeters = math.clamp(maxRangeMeters, 8f, 256f);
            mockSdfResolution = math.clamp(mockSdfResolution, 4, 32);
            mockSdfCellSizeMeters = math.clamp(mockSdfCellSizeMeters, 0.5f, 8f);
            debugGizmoMaxLights = math.clamp(debugGizmoMaxLights, 0, MaxGizmoLightsHardCap);
            _mockSdfSeeded = false;
        }

        private void OnDrawGizmos()
        {
            if (!drawDebugGizmos || !Application.isPlaying || _jobActive)
                return;

            if (!TryGetStatesReadback(out NativeArray<LightCullStateDTO>.ReadOnly states, out NativeArray<DynamicPointLightSourceDTO>.ReadOnly sources, out int count))
                return;

            int limit = math.min(math.min(count, states.Length), math.min(sources.Length, debugGizmoMaxLights));
            for (int i = 0; i < limit; i++)
            {
                LightCullStateDTO state = states[i];
                DynamicPointLightSourceDTO source = sources[i];
                if (state.LightHash == 0u)
                    continue;

                if ((state.Flags & DynamicPointLightCullingFlags.Submitted) != 0u)
                    Gizmos.color = Color.green;
                else if ((state.Flags & DynamicPointLightCullingFlags.Active) != 0u)
                    Gizmos.color = Color.yellow;
                else
                    Gizmos.color = Color.red;

                Vector3 position = HectonFloatingOrigin.ToRuntimePosition(source.AUP);
                float size = math.max(0.15f, math.min(2.0f, source.RangeMeters * 0.08f));
                Gizmos.DrawWireCube(position, new Vector3(size, size, size));
            }
        }
#endif
    }
}
