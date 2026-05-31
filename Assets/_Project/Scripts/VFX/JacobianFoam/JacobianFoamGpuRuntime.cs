using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Hecton8.VFX
{
    public sealed class JacobianFoamGpuRuntime : MonoBehaviour, ILateFrameTickable, IColdTickable, IGlobalRegistryHotSwapListener
    {
        private const uint PortableMaxThreadsPerThreadGroup = 256u;
        private const float GpuDumpThresholdMicroseconds = 1500f;
        private const int ResolutionRebuildFrameCadence = 30;
        private const int ResolutionHysteresisPixels = 128;
        private const int MaxSingleDispatchResolution = 1024;
        private const float LockedVisualTickDeltaSeconds = 1f / 60f;
        private const SystemID OwnerSystemId = SystemID.Vfx;

        public struct FoamRenderGraphPayload
        {
            public int OwnerId;
            public ComputeShader Compute;
            public int CalculateKernel;
            public int AdvectKernel;
            public int ClearKernel;
            public int DispatchGroups;
            public int DispatchGroupsX;
            public int DispatchGroupsY;
            public int CalculateDispatchGroupsX;
            public int CalculateDispatchGroupsY;
            public int AdvectDispatchGroupsX;
            public int AdvectDispatchGroupsY;
            public int ClearDispatchGroupsX;
            public int ClearDispatchGroupsY;
            public int Resolution;
            public int WakeCount;
            public uint Sequence;
            public byte HistoryWriteIndex;
            public byte ClearHistory;
            public GraphicsBuffer ParamsBuffer;
            public GraphicsBuffer WakeBuffer;
            public RTHandle HistoryReadTexture;
            public RTHandle HistoryWriteTexture;
            public GraphicsFormat FoamTextureFormat;
            public Vector4 GridParams;
            public Vector4 WorldParams;
            public Vector4 WakeParams;
            public Vector4 Wave0;
            public Vector4 Wave1;
            public Vector4 Wave2;
            public Vector4 Wave3;
            public Vector4 WaveSpeed;
        }

        [SerializeField] private ComputeShader _computeShader;
        [SerializeField] private int _minResolution = 512;
        [SerializeField] private int _maxResolution = 2048;
        [SerializeField] private float _textureWorldSizeMeters = 512f;
        [SerializeField] private bool _generateMockStormState;
        [SerializeField] private bool _dumpOnBudgetSpike = true;
        [SerializeField] private Camera _primaryCamera;

        private IDataVault _vault;
        private IDataVault _tuningReadPinVault;
        private IDataVault _wakeReadPinVault;
        private IDataVault _telemetryReadPinVault;
        private IDataVault _paramsWriteVault;
        private IDataVault _tuningWriteVault;
        private IDataVault _wakeWriteVault;
        private IDataVault _telemetryWriteVault;
        private VaultGenerationHandle<FoamComputeParamsDTO> _paramsHandle;
        private VaultGenerationHandle<FoamTuningDTO> _tuningHandle;
        private VaultGenerationHandle<FoamWakeImpactDTO> _wakeHandle;
        private VaultGenerationHandle<FoamRenderTelemetryEntry> _telemetryHandle;
        private GraphicsBuffer _paramsBufferA;
        private GraphicsBuffer _paramsBufferB;
        private GraphicsBuffer _activeParamsBuffer;
        private GraphicsBuffer _wakeBufferA;
        private GraphicsBuffer _wakeBufferB;
        private GraphicsBuffer _activeWakeBuffer;
        private RTHandle _foamHistoryA;
        private RTHandle _foamHistoryB;
        private GraphicsFormat _foamTextureFormat = GraphicsFormat.None;
        private int _readHistoryIndex;
        private int _resolution;
        private int _telemetryCursor;
        private int _telemetryWritten;
        private int _frame;
        private uint _payloadSequence;
        private uint _lastConsumedAckSequence;
        private int _calculateKernel = -1;
        private int _advectKernel = -1;
        private int _clearKernel = -1;
        private int _calculateThreadGroupSizeX;
        private int _calculateThreadGroupSizeY;
        private int _advectThreadGroupSizeX;
        private int _advectThreadGroupSizeY;
        private int _clearThreadGroupSizeX;
        private int _clearThreadGroupSizeY;
        private float2 _previousScrollOffset;
        private float2 _currentScrollOffset;
        private float _qualityWeight;
        private float _lastDeltaTime;
        private float _visualClockSeconds;
        private float _lastEstimatedGpuMicroseconds;
        private float _shorelineDepthFade;
        private int _lastWakeCount;
        private int _lastResolutionRebuildFrame;
        private uint _lastVisualClockFrameId = uint.MaxValue;
        private bool _clearHistoryNextDispatch;
        private bool _registeredLateFrame;
        private bool _registeredColdTick;
        private bool _registeredHotSwap;
        private bool _vaultReady;
        private bool _hasPreparedPayload;
        private bool _coldSupportsComputeShaders;
        private GraphicsFormat _coldFoamTextureFormat = GraphicsFormat.None;
        private RenderTexture _activeFoamTexture;
        private FoamRenderGraphPayload _preparedPayload;
        private int _instanceId;
        private bool _deferredTelemetryDumpRequested;
        private int _deferredTelemetryDumpCursor;
        private int _deferredTelemetryDumpWritten;
        private int _pendingGpuResolution;
        private bool _gpuStateRebuildRequested;
        private readonly FoamWakeImpactDTO[] _wakeUploadSnapshot = new FoamWakeImpactDTO[JacobianFoamContracts.WakeImpactCapacity];

        private static FoamRenderGraphPayload s_publishedPayload;
        private static bool s_hasPublishedPayload;
        private static int s_publishedOwnerId;
        private static RenderTexture s_publishedFoamTexture;
        private static uint s_renderGraphAckSequence;
        private static int s_renderGraphAckOwnerId;
        private static byte s_renderGraphAckHistoryWriteIndex;

        public void ColdBindDataVault(IDataVault vault)
        {
            RebindDataVaultForLifecycle(vault);
            EnsureVaultState(true);
        }

        private void OnEnable()
        {
            _instanceId = GetEntityId().GetHashCode();
            CacheDataVaultCold();
            CacheRenderContextCameraIfMissing();
            CacheGraphicsCapabilitySnapshotCold();
            ResolveKernels();
            EnsureVaultState(true);
            if (!EnsureGpuStateCold(JacobianFoamContracts.ResolveFoamResolution(0.5f, _minResolution, _maxResolution)))
                RequestGpuStateRebuild(JacobianFoamContracts.ResolveFoamResolution(0.5f, _minResolution, _maxResolution));
            _visualClockSeconds = 0f;
            _lastVisualClockFrameId = uint.MaxValue;
            TryRegisterHotSwapListener();
            TryRegisterColdTickable();
            TryRegisterLateFrameTickable();
        }

        private void OnDisable()
        {
            FlushDeferredTelemetryDump();
            TryUnregisterLateFrameTickable();
            TryUnregisterColdTickable();
            TryUnregisterHotSwapListener();
            ReleaseVaultHandles(_vault);
            ClearVaultDescriptors();
            _vault = null;
            ResetVaultEpochState();
            ClearPreparedPayload();
            DisposeGpuState();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                if (currentService == null || !isActiveAndEnabled)
                    return;

                TryUnregisterLateFrameTickable();
                TryUnregisterColdTickable();
                TryRegisterColdTickable();
                TryRegisterLateFrameTickable();
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            RebindDataVaultForLifecycle(currentService is IDataVault currentVault ? currentVault : null);

            if (isActiveAndEnabled && _vault != null)
                EnsureVaultState(true);
        }

        public void ColdTick()
        {
            if (!_gpuStateRebuildRequested)
                return;

            if (_computeShader == null || !_coldSupportsComputeShaders)
            {
                ClearPreparedPayload();
                return;
            }

            if (EnsureGpuStateCold(_pendingGpuResolution))
                _gpuStateRebuildRequested = false;
        }

        public void LateFrameTick()
        {
            ConsumeRenderGraphAcknowledgement();

            if (_computeShader == null || !_coldSupportsComputeShaders)
            {
                ClearPreparedPayload();
                return;
            }

            if (!HasVaultStateReady())
            {
                ClearPreparedPayload();
                return;
            }

            FoamTuningDTO tuning = ResolveTuning();
            float overrideWeight = tuning.GlobalQualityWeightOverride;
            _qualityWeight = math.saturate(overrideWeight >= 0f ? overrideWeight : HomeostasisBrain.GlobalQualityWeight);
            _textureWorldSizeMeters = math.max(1f, tuning.TextureWorldSizeMeters > 0f ? tuning.TextureWorldSizeMeters : _textureWorldSizeMeters);
            _shorelineDepthFade = tuning.ShorelineDepthFade;
            _lastDeltaTime = AdvanceVisualClock(ref _lastVisualClockFrameId, ref _visualClockSeconds);
            float phaseTime = _visualClockSeconds;

            int minResolution = tuning.MinResolution > 0f ? (int)tuning.MinResolution : _minResolution;
            int maxResolution = tuning.MaxResolution > 0f ? (int)tuning.MaxResolution : _maxResolution;
            int targetResolution = ResolveRuntimeResolution(JacobianFoamContracts.ResolveFoamResolution(_qualityWeight, minResolution, maxResolution));
            if (!HasGpuStateReady(targetResolution))
            {
                RequestGpuStateRebuild(targetResolution);
                ClearPreparedPayload();
                return;
            }

            _previousScrollOffset = _currentScrollOffset;
            CacheRenderContextCameraIfMissing();
            _currentScrollOffset = ResolveCameraScrollOffset(_primaryCamera, _textureWorldSizeMeters);

            if (_generateMockStormState)
            {
                tuning = JacobianFoamContracts.ResolveMockStormTuning(in tuning, _qualityWeight);
                if (!TryWriteTuning(in tuning))
                {
                    ClearPreparedPayload();
                    return;
                }
            }

            FoamComputeParamsDTO parameters = JacobianFoamContracts.BuildParams(
                in tuning,
                _qualityWeight,
                _lastDeltaTime,
                _currentScrollOffset);
            if (!TryWriteAndUploadParams(in parameters))
            {
                ClearPreparedPayload();
                return;
            }

            if (_generateMockStormState)
            {
                if (!TryWriteAndUploadMockWakes(in tuning, phaseTime))
                {
                    ClearPreparedPayload();
                    return;
                }
            }
            else if (!TryUploadReadOnlyWakes())
            {
                ClearPreparedPayload();
                return;
            }

            RecordTelemetry(tuning);
            PublishRenderGraphPayload();
            _frame++;
        }

        public bool TryReadRenderGraphPayload(out FoamRenderGraphPayload payload)
        {
            payload = _preparedPayload;
            return IsPayloadValid(in payload, _hasPreparedPayload);
        }

        private IDataVault CacheDataVaultCold()
        {
            if (_vault != null)
                return _vault;

            RebindDataVaultForLifecycle(GlobalRegistry.DataVault);
            return _vault;
        }

        private void RebindDataVaultForLifecycle(IDataVault vault)
        {
            if (ReferenceEquals(_vault, vault))
                return;

            FlushDeferredTelemetryDump();
            ReleaseVaultHandles(_vault);
            ClearVaultDescriptors();
            _vault = vault;
            ResetVaultEpochState();
        }

        private void ClearVaultDescriptors()
        {
            _paramsHandle = default;
            _tuningHandle = default;
            _wakeHandle = default;
            _telemetryHandle = default;
        }

        private void ResetVaultEpochState()
        {
            _vaultReady = false;
            _telemetryCursor = 0;
            _telemetryWritten = 0;
            _deferredTelemetryDumpRequested = false;
            _deferredTelemetryDumpCursor = 0;
            _deferredTelemetryDumpWritten = 0;
            _lastEstimatedGpuMicroseconds = 0f;
            _lastWakeCount = 0;
        }

        private void CacheGraphicsCapabilitySnapshotCold()
        {
            _coldSupportsComputeShaders = SystemInfo.supportsComputeShaders;
            _coldFoamTextureFormat = ResolveFoamTextureFormatCold();
        }

        private static void ReleaseVaultHandles(IDataVault vault)
        {
            ReleaseVaultHandle<FoamComputeParamsDTO>(vault, BufferID.JacobianFoamParams);
            ReleaseVaultHandle<FoamTuningDTO>(vault, BufferID.JacobianFoamTuning);
            ReleaseVaultHandle<FoamWakeImpactDTO>(vault, BufferID.JacobianFoamWakeImpacts);
            ReleaseVaultHandle<FoamRenderTelemetryEntry>(vault, BufferID.JacobianFoamTelemetryRing);
            ReleaseVaultHandle<FoamAestheticProfileDTO>(vault, BufferID.JacobianFoamProfiles);
            ReleaseVaultHandle<byte>(vault, BufferID.JacobianFoamCsvScratch);
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, BufferID bufferId) where T : struct
        {
            if (vault == null)
                return;

            if (vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> handle) &&
                IsOwnedHandle(in handle, bufferId))
            {
                vault.ReleaseBuffer(in handle);
            }
        }

        private void TryRegisterLateFrameTickable()
        {
            if (_registeredLateFrame || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryRegisterColdTickable()
        {
            if (_registeredColdTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredColdTick = GlobalRegistry.TryRegisterColdTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterLateFrameTickable()
        {
            if (!_registeredLateFrame)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredLateFrame = false;
        }

        private void TryUnregisterColdTickable()
        {
            if (!_registeredColdTick)
                return;

            GlobalRegistry.UnregisterColdTickable(this, PriorityLayer.Environment);
            _registeredColdTick = false;
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

        public static bool TryReadPublishedRenderGraphPayload(out FoamRenderGraphPayload payload)
        {
            payload = s_publishedPayload;
            return IsPayloadValid(in payload, s_hasPublishedPayload);
        }

        private static bool IsPayloadValid(in FoamRenderGraphPayload payload, bool hasPayload)
        {
            return hasPayload &&
                payload.Compute != null &&
                payload.DispatchGroups > 0 &&
                payload.DispatchGroupsX > 0 &&
                payload.DispatchGroupsY > 0 &&
                payload.CalculateDispatchGroupsX > 0 &&
                payload.CalculateDispatchGroupsY > 0 &&
                payload.AdvectDispatchGroupsX > 0 &&
                payload.AdvectDispatchGroupsY > 0 &&
                payload.ClearDispatchGroupsX > 0 &&
                payload.ClearDispatchGroupsY > 0 &&
                payload.ParamsBuffer != null &&
                payload.ParamsBuffer.IsValid() &&
                payload.WakeBuffer != null &&
                payload.WakeBuffer.IsValid() &&
                payload.HistoryReadTexture != null &&
                payload.HistoryWriteTexture != null &&
                payload.OwnerId != 0 &&
                payload.Sequence != 0u &&
                payload.FoamTextureFormat != GraphicsFormat.None;
        }

        public static bool TryReadFoamPreviewTexture(out RenderTexture texture)
        {
            texture = s_publishedFoamTexture;
            return texture != null;
        }

        internal static void AcknowledgePublishedRenderGraphPayload(int ownerId, uint sequence, byte historyWriteIndex, RenderTexture foamTexture)
        {
            if (ownerId == 0 || sequence == 0u || foamTexture == null)
                return;

            s_renderGraphAckOwnerId = ownerId;
            s_renderGraphAckSequence = sequence;
            s_renderGraphAckHistoryWriteIndex = (byte)(historyWriteIndex & 1);
            s_publishedFoamTexture = foamTexture;
        }

        internal static void AcknowledgeFallbackFoamTexture()
        {
            s_publishedFoamTexture = null;
        }

        private void PublishRenderGraphPayload()
        {
            if (_computeShader == null ||
                _calculateKernel < 0 ||
                _advectKernel < 0 ||
                _clearKernel < 0 ||
                _activeParamsBuffer == null ||
                !_activeParamsBuffer.IsValid() ||
                _activeWakeBuffer == null ||
                !_activeWakeBuffer.IsValid() ||
                _foamHistoryA == null ||
                _foamHistoryB == null ||
                _resolution <= 0)
            {
                ClearPreparedPayload();
                return;
            }

            RTHandle read = _readHistoryIndex == 0 ? _foamHistoryA : _foamHistoryB;
            RTHandle write = _readHistoryIndex == 0 ? _foamHistoryB : _foamHistoryA;
            byte writeIndex = (byte)(_readHistoryIndex == 0 ? 1 : 0);
            if (read == null || write == null)
            {
                ClearPreparedPayload();
                return;
            }

            uint sequence = unchecked(_payloadSequence + 1u);
            if (sequence == 0u)
                sequence = 1u;
            _payloadSequence = sequence;

            FoamRenderGraphPayload payload;
            payload.OwnerId = _instanceId;
            payload.Compute = _computeShader;
            payload.CalculateKernel = _calculateKernel;
            payload.AdvectKernel = _advectKernel;
            payload.ClearKernel = _clearKernel;
            payload.CalculateDispatchGroupsX = JacobianFoamContracts.ResolveDispatchGroups(_resolution, _calculateThreadGroupSizeX);
            payload.CalculateDispatchGroupsY = JacobianFoamContracts.ResolveDispatchGroups(_resolution, _calculateThreadGroupSizeY);
            payload.AdvectDispatchGroupsX = JacobianFoamContracts.ResolveDispatchGroups(_resolution, _advectThreadGroupSizeX);
            payload.AdvectDispatchGroupsY = JacobianFoamContracts.ResolveDispatchGroups(_resolution, _advectThreadGroupSizeY);
            payload.ClearDispatchGroupsX = JacobianFoamContracts.ResolveDispatchGroups(_resolution, _clearThreadGroupSizeX);
            payload.ClearDispatchGroupsY = JacobianFoamContracts.ResolveDispatchGroups(_resolution, _clearThreadGroupSizeY);
            if (payload.CalculateDispatchGroupsX <= 0 ||
                payload.CalculateDispatchGroupsY <= 0 ||
                payload.AdvectDispatchGroupsX <= 0 ||
                payload.AdvectDispatchGroupsY <= 0 ||
                payload.ClearDispatchGroupsX <= 0 ||
                payload.ClearDispatchGroupsY <= 0)
            {
                ClearPreparedPayload();
                return;
            }

            payload.DispatchGroupsX = math.max(
                math.max(payload.CalculateDispatchGroupsX, payload.AdvectDispatchGroupsX),
                payload.ClearDispatchGroupsX);
            payload.DispatchGroupsY = math.max(
                math.max(payload.CalculateDispatchGroupsY, payload.AdvectDispatchGroupsY),
                payload.ClearDispatchGroupsY);
            payload.DispatchGroups = math.max(payload.DispatchGroupsX, payload.DispatchGroupsY);
            payload.Resolution = _resolution;
            payload.WakeCount = _lastWakeCount;
            payload.Sequence = sequence;
            payload.HistoryWriteIndex = writeIndex;
            payload.ClearHistory = _clearHistoryNextDispatch ? (byte)1 : (byte)0;
            payload.ParamsBuffer = _activeParamsBuffer;
            payload.WakeBuffer = _activeWakeBuffer;
            payload.HistoryReadTexture = read;
            payload.HistoryWriteTexture = write;
            payload.FoamTextureFormat = _foamTextureFormat;
            float inv = 1f / math.max(1, _resolution);
            payload.GridParams = new Vector4(_resolution, _resolution, inv, inv);
            payload.WorldParams = new Vector4(_previousScrollOffset.x, _previousScrollOffset.y, _textureWorldSizeMeters, _visualClockSeconds);
            payload.WakeParams = new Vector4(_lastWakeCount, 1f, _shorelineDepthFade, _qualityWeight);
            float waveWeight1 = math.smoothstep(0.12f, 0.45f, _qualityWeight);
            float waveWeight2 = math.smoothstep(0.38f, 0.72f, _qualityWeight);
            float waveWeight3 = math.smoothstep(0.62f, 0.96f, _qualityWeight);
            payload.Wave0 = new Vector4(0.94f, 0.34f, 0.29f, 31f);
            payload.Wave1 = new Vector4(-0.22f, 0.98f, 0.18f * waveWeight1, 57f);
            payload.Wave2 = new Vector4(0.67f, -0.74f, 0.12f * waveWeight2, 103f);
            payload.Wave3 = new Vector4(-0.86f, -0.51f, 0.08f * waveWeight3, 181f);
            payload.WaveSpeed = new Vector4(1.35f, 0.91f, 0.55f, 0.33f);

            _preparedPayload = payload;
            _hasPreparedPayload = true;
            s_publishedPayload = payload;
            s_hasPublishedPayload = true;
            s_publishedOwnerId = _instanceId;
        }

        private void ResolveKernels()
        {
            if (_computeShader == null)
            {
                InvalidateKernels();
                return;
            }

            _calculateKernel = TryFindSupportedKernel("CS_CalculateFoam");
            _advectKernel = TryFindSupportedKernel("CS_AdvectFoam");
            _clearKernel = TryFindSupportedKernel("CS_ClearFoam");
            if (_calculateKernel < 0 || _advectKernel < 0 || _clearKernel < 0)
            {
                InvalidateKernels();
                return;
            }

            if (!TryResolveKernelThreadGroupSize2D(_calculateKernel, out _calculateThreadGroupSizeX, out _calculateThreadGroupSizeY) ||
                !TryResolveKernelThreadGroupSize2D(_advectKernel, out _advectThreadGroupSizeX, out _advectThreadGroupSizeY) ||
                !TryResolveKernelThreadGroupSize2D(_clearKernel, out _clearThreadGroupSizeX, out _clearThreadGroupSizeY))
            {
                InvalidateKernels();
            }
        }

        private bool TryResolveKernelThreadGroupSize2D(int kernel, out int groupSizeX, out int groupSizeY)
        {
            groupSizeX = 0;
            groupSizeY = 0;
            if (_computeShader == null || kernel < 0)
                return false;

            uint x;
            uint y;
            uint z;
            try
            {
                if (!_computeShader.IsSupported(kernel))
                    return false;

                _computeShader.GetKernelThreadGroupSizes(kernel, out x, out y, out z);
            }
            catch (System.ObjectDisposedException)
            {
                return false;
            }
            catch (System.InvalidOperationException)
            {
                return false;
            }
            catch (System.ArgumentException)
            {
                return false;
            }
            catch (MissingReferenceException)
            {
                return false;
            }
            catch (UnityException)
            {
                return false;
            }
            ulong totalThreads = (ulong)x * y * z;
            if (x == 0u ||
                y == 0u ||
                z == 0u ||
                totalThreads > PortableMaxThreadsPerThreadGroup ||
                x > int.MaxValue ||
                y > int.MaxValue)
            {
                return false;
            }

            groupSizeX = (int)x;
            groupSizeY = (int)y;
            return true;
        }

        private int TryFindSupportedKernel(string kernelName)
        {
            if (_computeShader == null)
                return -1;

            try
            {
                if (!_computeShader.HasKernel(kernelName))
                    return -1;

                int kernel = _computeShader.FindKernel(kernelName);
                if (kernel < 0)
                    return -1;

                return _computeShader.IsSupported(kernel) ? kernel : -1;
            }
            catch (System.ObjectDisposedException)
            {
                return -1;
            }
            catch (System.InvalidOperationException)
            {
                return -1;
            }
            catch (System.ArgumentException)
            {
                return -1;
            }
            catch (MissingReferenceException)
            {
                return -1;
            }
            catch (UnityException)
            {
                return -1;
            }
        }

        private void InvalidateKernels()
        {
            _calculateKernel = -1;
            _advectKernel = -1;
            _clearKernel = -1;
            _calculateThreadGroupSizeX = 0;
            _calculateThreadGroupSizeY = 0;
            _advectThreadGroupSizeX = 0;
            _advectThreadGroupSizeY = 0;
            _clearThreadGroupSizeX = 0;
            _clearThreadGroupSizeY = 0;
            ClearPreparedPayload();
        }

        private bool EnsureVaultState(bool allowCreate)
        {
            if (_vault == null || _vault.IsCompactionFenceActive)
            {
                _vaultReady = false;
                return false;
            }

            if (_vaultReady &&
                IsOwnedHandle(in _paramsHandle, BufferID.JacobianFoamParams) &&
                IsOwnedHandle(in _tuningHandle, BufferID.JacobianFoamTuning) &&
                IsOwnedHandle(in _wakeHandle, BufferID.JacobianFoamWakeImpacts) &&
                IsOwnedHandle(in _telemetryHandle, BufferID.JacobianFoamTelemetryRing))
            {
                return true;
            }

            if (allowCreate && !JacobianFoamContracts.EnsureVaultBuffers(_vault))
                return false;

            if (!TryBindVaultDescriptor(_vault, BufferID.JacobianFoamParams, out _paramsHandle) ||
                !TryBindVaultDescriptor(_vault, BufferID.JacobianFoamTuning, out _tuningHandle) ||
                !TryBindVaultDescriptor(_vault, BufferID.JacobianFoamWakeImpacts, out _wakeHandle) ||
                !TryBindVaultDescriptor(_vault, BufferID.JacobianFoamTelemetryRing, out _telemetryHandle))
            {
                _vaultReady = false;
                return false;
            }

            if (allowCreate && !TrySeedDefaultTuning())
                return false;

            _vaultReady =
                IsOwnedHandle(in _paramsHandle, BufferID.JacobianFoamParams) &&
                IsOwnedHandle(in _tuningHandle, BufferID.JacobianFoamTuning) &&
                IsOwnedHandle(in _wakeHandle, BufferID.JacobianFoamWakeImpacts) &&
                IsOwnedHandle(in _telemetryHandle, BufferID.JacobianFoamTelemetryRing);
            return _vaultReady;
        }

        private bool HasVaultStateReady()
        {
            return _vault != null &&
                !_vault.IsCompactionFenceActive &&
                _vaultReady &&
                IsOwnedHandle(in _paramsHandle, BufferID.JacobianFoamParams) &&
                IsOwnedHandle(in _tuningHandle, BufferID.JacobianFoamTuning) &&
                IsOwnedHandle(in _wakeHandle, BufferID.JacobianFoamWakeImpacts) &&
                IsOwnedHandle(in _telemetryHandle, BufferID.JacobianFoamTelemetryRing);
        }

        private bool HasGpuStateReady(int targetResolution)
        {
            targetResolution = ClampSingleDispatchResolution(targetResolution);
            GraphicsFormat targetFormat = _coldFoamTextureFormat;
            return targetFormat != GraphicsFormat.None &&
                _paramsBufferA != null &&
                _paramsBufferA.IsValid() &&
                _paramsBufferB != null &&
                _paramsBufferB.IsValid() &&
                _wakeBufferA != null &&
                _wakeBufferA.IsValid() &&
                _wakeBufferB != null &&
                _wakeBufferB.IsValid() &&
                _resolution == targetResolution &&
                _foamTextureFormat == targetFormat &&
                _foamHistoryA != null &&
                _foamHistoryB != null;
        }

        private void RequestGpuStateRebuild(int targetResolution)
        {
            _pendingGpuResolution = ClampSingleDispatchResolution(targetResolution);
            _gpuStateRebuildRequested = true;
        }

        private bool EnsureGpuStateCold(int targetResolution)
        {
            targetResolution = ClampSingleDispatchResolution(targetResolution);

            if (_paramsBufferA == null || !_paramsBufferA.IsValid() ||
                _paramsBufferB == null || !_paramsBufferB.IsValid())
            {
                _paramsBufferA?.Release();
                _paramsBufferB?.Release();
                _paramsBufferA = new GraphicsBuffer(GraphicsBuffer.Target.Constant, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, JacobianFoamContracts.ParamsStrideBytes);
                _paramsBufferB = new GraphicsBuffer(GraphicsBuffer.Target.Constant, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, JacobianFoamContracts.ParamsStrideBytes);
            }

            if (_wakeBufferA == null || !_wakeBufferA.IsValid() ||
                _wakeBufferB == null || !_wakeBufferB.IsValid())
            {
                _wakeBufferA?.Release();
                _wakeBufferB?.Release();
                _wakeBufferA = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.LockBufferForWrite, JacobianFoamContracts.WakeImpactCapacity, JacobianFoamContracts.WakeImpactStrideBytes);
                _wakeBufferB = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.LockBufferForWrite, JacobianFoamContracts.WakeImpactCapacity, JacobianFoamContracts.WakeImpactStrideBytes);
            }

            GraphicsFormat targetFormat = _coldFoamTextureFormat;
            if (targetFormat == GraphicsFormat.None)
            {
                ReleaseTextures();
                _resolution = 0;
                _foamTextureFormat = GraphicsFormat.None;
                return false;
            }

            if (_resolution == targetResolution &&
                _foamTextureFormat == targetFormat &&
                _foamHistoryA != null &&
                _foamHistoryB != null)
            {
                return true;
            }

            ReleaseTextures();
            _resolution = targetResolution;
            _foamTextureFormat = targetFormat;
            _foamHistoryA = AllocateFoamTexture(targetResolution, targetFormat, "_HectonJacobianFoamHistoryA");
            _foamHistoryB = AllocateFoamTexture(targetResolution, targetFormat, "_HectonJacobianFoamHistoryB");
            _readHistoryIndex = 0;
            _lastResolutionRebuildFrame = _frame;
            _clearHistoryNextDispatch = true;
            return _foamHistoryA != null && _foamHistoryB != null;
        }

        private int ResolveRuntimeResolution(int targetResolution)
        {
            targetResolution = ClampSingleDispatchResolution(targetResolution);
            if (_resolution <= 0)
                return targetResolution;

            int delta = math.abs(targetResolution - _resolution);
            if (delta < ResolutionHysteresisPixels)
                return _resolution;

            if (_frame - _lastResolutionRebuildFrame < ResolutionRebuildFrameCadence)
                return _resolution;

            return targetResolution;
        }

        private static int ClampSingleDispatchResolution(int targetResolution)
        {
            return math.clamp(targetResolution, 256, MaxSingleDispatchResolution);
        }

        private static RTHandle AllocateFoamTexture(int resolution, GraphicsFormat format, string name)
        {
            return RTHandles.Alloc(
                resolution,
                resolution,
                1,
                DepthBits.None,
                format,
                FilterMode.Bilinear,
                TextureWrapMode.Repeat,
                dimension: TextureDimension.Tex2D,
                enableRandomWrite: true,
                name: name);
        }

        private static GraphicsFormat ResolveFoamTextureFormatCold()
        {
            if (IsFoamTextureFormatSupportedCold(GraphicsFormat.R16_SFloat))
                return GraphicsFormat.R16_SFloat;

            if (IsFoamTextureFormatSupportedCold(GraphicsFormat.R32_SFloat))
                return GraphicsFormat.R32_SFloat;

            return IsFoamTextureFormatSupportedCold(GraphicsFormat.R8_UNorm)
                ? GraphicsFormat.R8_UNorm
                : GraphicsFormat.None;
        }

        private static bool IsFoamTextureFormatSupportedCold(GraphicsFormat format)
        {
            return SystemInfo.IsFormatSupported(format, GraphicsFormatUsage.LoadStore) &&
                SystemInfo.IsFormatSupported(format, GraphicsFormatUsage.Sample);
        }

        private void DisposeGpuState()
        {
            _paramsBufferA?.Release();
            _paramsBufferB?.Release();
            _wakeBufferA?.Release();
            _wakeBufferB?.Release();
            _paramsBufferA = null;
            _paramsBufferB = null;
            _wakeBufferA = null;
            _wakeBufferB = null;
            _activeParamsBuffer = null;
            _activeWakeBuffer = null;
            ReleaseTextures();
        }

        private void ReleaseTextures()
        {
            _foamHistoryA?.Release();
            _foamHistoryB?.Release();
            _foamHistoryA = null;
            _foamHistoryB = null;
            _foamTextureFormat = GraphicsFormat.None;
            ClearPreparedPayload();
        }

        private FoamTuningDTO ResolveTuning()
        {
            if (!TryAcquireReadPin(
                    in _tuningHandle,
                    BufferID.JacobianFoamTuning,
                    1,
                    out NativeArray<FoamTuningDTO> tuning))
            {
                return JacobianFoamContracts.CreateDefaultTuning();
            }

            try
            {
                return tuning.IsCreated && tuning.Length > 0 && tuning[0].Version != 0u
                    ? tuning[0]
                    : JacobianFoamContracts.CreateDefaultTuning();
            }
            finally
            {
                ReleaseReadPin(BufferID.JacobianFoamTuning);
            }
        }

        private bool TryWriteTuning(in FoamTuningDTO tuning)
        {
            if (!TryAcquireWriteBuffer(
                    in _tuningHandle,
                    BufferID.JacobianFoamTuning,
                    1,
                    out IDataVault tuningWriteVault,
                    out NativeArray<FoamTuningDTO> tuningArray))
            {
                return false;
            }

            try
            {
                if (!tuningArray.IsCreated || tuningArray.Length <= 0)
                    return false;

                tuningArray[0] = tuning;
                return true;
            }
            finally
            {
                ReleaseWriteBuffer(tuningWriteVault, in _tuningHandle, BufferID.JacobianFoamTuning);
            }
        }

        private bool TryWriteAndUploadParams(in FoamComputeParamsDTO parameters)
        {
            bool wroteParams = false;
            if (!TryAcquireWriteBuffer(
                    in _paramsHandle,
                    BufferID.JacobianFoamParams,
                    1,
                    out IDataVault paramsWriteVault,
                    out NativeArray<FoamComputeParamsDTO> paramsArray))
            {
                return false;
            }

            try
            {
                if (!paramsArray.IsCreated || paramsArray.Length <= 0)
                    return false;

                paramsArray[0] = parameters;
                wroteParams = true;
            }
            finally
            {
                ReleaseWriteBuffer(paramsWriteVault, in _paramsHandle, BufferID.JacobianFoamParams);
            }

            return wroteParams && UploadParams(in parameters);
        }

        private bool TryWriteAndUploadMockWakes(in FoamTuningDTO tuning, float phaseTime)
        {
            int wakeCount = 0;
            bool capturedSnapshot = false;
            if (!TryAcquireWriteBuffer(
                    in _wakeHandle,
                    BufferID.JacobianFoamWakeImpacts,
                    JacobianFoamContracts.WakeImpactCapacity,
                    out IDataVault wakeWriteVault,
                    out NativeArray<FoamWakeImpactDTO> wakeArray))
            {
                return false;
            }

            try
            {
                if (!wakeArray.IsCreated || wakeArray.Length <= 0)
                    return false;

                int count = math.min(wakeArray.Length, JacobianFoamContracts.WakeImpactCapacity);
                for (int i = 0; i < count; i++)
                {
                    wakeArray[i] = JacobianFoamContracts.BuildMockWakeImpact(
                        i,
                        count,
                        phaseTime,
                        _qualityWeight,
                        in tuning);
                }

                wakeCount = ResolveWakeCount(wakeArray, _qualityWeight);
                wakeCount = CopyWakesToUploadSnapshot(wakeArray, wakeCount);
                capturedSnapshot = true;
            }
            finally
            {
                ReleaseWriteBuffer(wakeWriteVault, in _wakeHandle, BufferID.JacobianFoamWakeImpacts);
            }

            if (!capturedSnapshot)
                return false;

            _lastWakeCount = wakeCount;
            return UploadWakesFromSnapshot(wakeCount);
        }

        private bool TryUploadReadOnlyWakes()
        {
            int wakeCount = 0;
            bool capturedSnapshot = false;
            if (!TryAcquireReadPin(
                    in _wakeHandle,
                    BufferID.JacobianFoamWakeImpacts,
                    JacobianFoamContracts.WakeImpactCapacity,
                    out NativeArray<FoamWakeImpactDTO> wakeArray))
            {
                return false;
            }

            try
            {
                wakeCount = ResolveWakeCount(wakeArray, _qualityWeight);
                wakeCount = CopyWakesToUploadSnapshot(wakeArray, wakeCount);
                capturedSnapshot = true;
            }
            finally
            {
                ReleaseReadPin(BufferID.JacobianFoamWakeImpacts);
            }

            if (!capturedSnapshot)
                return false;

            _lastWakeCount = wakeCount;
            return UploadWakesFromSnapshot(wakeCount);
        }

        private bool UploadParams(in FoamComputeParamsDTO parameters)
        {
            GraphicsBuffer writeBuffer = _activeParamsBuffer == _paramsBufferA ? _paramsBufferB : _paramsBufferA;
            if (writeBuffer == null || !writeBuffer.IsValid())
            {
                _activeParamsBuffer = null;
                return false;
            }

            NativeArray<FoamComputeParamsDTO> mapped = writeBuffer.LockBufferForWrite<FoamComputeParamsDTO>(0, 1);
            try
            {
                if (mapped.IsCreated && mapped.Length > 0)
                    mapped[0] = parameters;
            }
            finally
            {
                writeBuffer.UnlockBufferAfterWrite<FoamComputeParamsDTO>(1);
            }

            _activeParamsBuffer = writeBuffer;
            return true;
        }

        private int CopyWakesToUploadSnapshot(NativeArray<FoamWakeImpactDTO> wakeArray, int wakeCount)
        {
            int capacity = _wakeUploadSnapshot.Length;
            int sourceCount = wakeArray.IsCreated ? math.min(wakeArray.Length, capacity) : 0;
            int copyCount = math.clamp(wakeCount, 0, sourceCount);
            for (int i = 0; i < copyCount; i++)
                _wakeUploadSnapshot[i] = wakeArray[i];

            for (int i = copyCount; i < capacity; i++)
                _wakeUploadSnapshot[i] = default;

            return copyCount;
        }

        private bool UploadWakesFromSnapshot(int wakeCount)
        {
            GraphicsBuffer writeBuffer = _activeWakeBuffer == _wakeBufferA ? _wakeBufferB : _wakeBufferA;
            if (writeBuffer == null || !writeBuffer.IsValid())
            {
                _activeWakeBuffer = null;
                return false;
            }

            NativeArray<FoamWakeImpactDTO> mapped = writeBuffer.LockBufferForWrite<FoamWakeImpactDTO>(0, JacobianFoamContracts.WakeImpactCapacity);
            try
            {
                if (mapped.IsCreated && mapped.Length > 0)
                {
                    int destinationCount = math.min(mapped.Length, JacobianFoamContracts.WakeImpactCapacity);
                    int copyCount = math.clamp(wakeCount, 0, math.min(destinationCount, _wakeUploadSnapshot.Length));
                    for (int i = 0; i < copyCount; i++)
                        mapped[i] = _wakeUploadSnapshot[i];

                    for (int i = copyCount; i < destinationCount; i++)
                        mapped[i] = default;
                }
            }
            finally
            {
                writeBuffer.UnlockBufferAfterWrite<FoamWakeImpactDTO>(JacobianFoamContracts.WakeImpactCapacity);
            }

            _activeWakeBuffer = writeBuffer;
            return true;
        }

        private int ResolveWakeCount(NativeArray<FoamWakeImpactDTO> wakeArray, float quality)
        {
            if (!wakeArray.IsCreated)
                return 0;

            int highCount = math.min(wakeArray.Length, JacobianFoamContracts.WakeImpactCapacity);
            float curved = math.saturate(quality);
            int target = (int)math.round(math.lerp(8f, highCount, curved * curved * (3f - 2f * curved)));
            return math.clamp(target, 0, highCount);
        }

        private void RecordTelemetry(FoamTuningDTO tuning)
        {
            if (!IsOwnedHandle(in _telemetryHandle, BufferID.JacobianFoamTelemetryRing) || _vault == null || _vault.IsCompactionFenceActive)
                return;

            if (!TryAcquireWriteBuffer(
                    in _telemetryHandle,
                    BufferID.JacobianFoamTelemetryRing,
                    JacobianFoamContracts.TelemetryCapacity,
                    out IDataVault telemetryWriteVault,
                    out NativeArray<FoamRenderTelemetryEntry> telemetry))
                return;

            try
            {
                if (!telemetry.IsCreated || telemetry.Length <= 0)
                    return;

                int slot = _telemetryCursor % telemetry.Length;
                float resolutionScale = _maxResolution <= 0 ? 1f : math.saturate(_resolution / (float)_maxResolution);
                _lastEstimatedGpuMicroseconds = JacobianFoamContracts.EstimateGpuMicroseconds(_resolution, _lastWakeCount, _qualityWeight);
                bool budgetSpike = _dumpOnBudgetSpike && _lastEstimatedGpuMicroseconds > GpuDumpThresholdMicroseconds;
                uint flags = _clearHistoryNextDispatch ? 2u : 1u;
                if (budgetSpike)
                    flags |= 4u;
                telemetry[slot] = new FoamRenderTelemetryEntry
                {
                    Frame = _frame,
                    Resolution = _resolution,
                    WakeCount = _lastWakeCount,
                    DispatchGroups = ResolveMaxDispatchGroups(_resolution),
                    GlobalQualityWeight = _qualityWeight,
                    ResolutionScale = resolutionScale,
                    EstimatedGpuMicroseconds = _lastEstimatedGpuMicroseconds,
                    ShorelineContribution = tuning.ShorelineDepthFade,
                    ScrollOffset = _currentScrollOffset,
                    StateHash = JacobianFoamContracts.HashState(_frame, _resolution, _lastWakeCount, _qualityWeight, _currentScrollOffset, JacobianFoamContracts.DefaultProfileHash),
                    Flags = flags,
                    Cursor = (uint)_telemetryCursor,
                    ProfileHash = JacobianFoamContracts.DefaultProfileHash,
                    DecayRate = tuning.DecayRate,
                    Pad0 = 0u
                };

                _telemetryCursor = (_telemetryCursor + 1) % telemetry.Length;
                _telemetryWritten = math.min(_telemetryWritten + 1, telemetry.Length);
                if (budgetSpike)
                {
                    _deferredTelemetryDumpRequested = true;
                    _deferredTelemetryDumpCursor = _telemetryCursor;
                    _deferredTelemetryDumpWritten = _telemetryWritten;
                }
            }
            finally
            {
                ReleaseWriteBuffer(telemetryWriteVault, in _telemetryHandle, BufferID.JacobianFoamTelemetryRing);
            }
        }

        private int ResolveMaxDispatchGroups(int resolution)
        {
            int calculateGroupsX = JacobianFoamContracts.ResolveDispatchGroups(resolution, _calculateThreadGroupSizeX);
            int calculateGroupsY = JacobianFoamContracts.ResolveDispatchGroups(resolution, _calculateThreadGroupSizeY);
            int advectGroupsX = JacobianFoamContracts.ResolveDispatchGroups(resolution, _advectThreadGroupSizeX);
            int advectGroupsY = JacobianFoamContracts.ResolveDispatchGroups(resolution, _advectThreadGroupSizeY);
            int clearGroupsX = JacobianFoamContracts.ResolveDispatchGroups(resolution, _clearThreadGroupSizeX);
            int clearGroupsY = JacobianFoamContracts.ResolveDispatchGroups(resolution, _clearThreadGroupSizeY);
            if (calculateGroupsX <= 0 ||
                calculateGroupsY <= 0 ||
                advectGroupsX <= 0 ||
                advectGroupsY <= 0 ||
                clearGroupsX <= 0 ||
                clearGroupsY <= 0)
                return 0;

            return math.max(
                math.max(math.max(calculateGroupsX, calculateGroupsY), math.max(advectGroupsX, advectGroupsY)),
                math.max(clearGroupsX, clearGroupsY));
        }

        private static string ProjectRoot()
        {
            return System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, ".."));
        }

        public bool FlushDeferredTelemetryDump()
        {
            if (!_deferredTelemetryDumpRequested ||
                _vault == null ||
                _vault.IsCompactionFenceActive ||
                !TryAcquireReadPin(
                    in _telemetryHandle,
                    BufferID.JacobianFoamTelemetryRing,
                    JacobianFoamContracts.TelemetryCapacity,
                    out NativeArray<FoamRenderTelemetryEntry> telemetry))
            {
                return false;
            }

            try
            {
                bool wrote = FoamTelemetryDump.TryWrite(ProjectRoot(), telemetry, _deferredTelemetryDumpCursor, _deferredTelemetryDumpWritten);
                if (wrote)
                    _deferredTelemetryDumpRequested = false;
                return wrote;
            }
            finally
            {
                ReleaseReadPin(BufferID.JacobianFoamTelemetryRing);
            }
        }

        private void ClearPreparedPayload()
        {
            _hasPreparedPayload = false;
            _preparedPayload = default;
            _activeFoamTexture = null;
            if (s_publishedOwnerId == _instanceId)
            {
                s_hasPublishedPayload = false;
                s_publishedPayload = default;
                s_publishedOwnerId = 0;
                s_publishedFoamTexture = null;
            }

            if (s_renderGraphAckOwnerId == _instanceId)
            {
                s_renderGraphAckOwnerId = 0;
                s_renderGraphAckSequence = 0u;
                s_renderGraphAckHistoryWriteIndex = 0;
            }
        }

        private void ConsumeRenderGraphAcknowledgement()
        {
            if (s_renderGraphAckOwnerId != _instanceId ||
                s_renderGraphAckSequence == 0u ||
                s_renderGraphAckSequence == _lastConsumedAckSequence)
            {
                return;
            }

            _readHistoryIndex = s_renderGraphAckHistoryWriteIndex == 0 ? 0 : 1;
            RTHandle read = _readHistoryIndex == 0 ? _foamHistoryA : _foamHistoryB;
            _activeFoamTexture = read != null ? read.rt : null;
            _clearHistoryNextDispatch = false;
            _lastConsumedAckSequence = s_renderGraphAckSequence;
        }

        private static float ResolveWrappedTime(float timeSeconds)
        {
            float safeTime = math.isfinite(timeSeconds) ? math.max(0f, timeSeconds) : 0f;
            const float WrapSeconds = 4096f;
            return safeTime - math.floor(safeTime / WrapSeconds) * WrapSeconds;
        }

        private static float AdvanceVisualClock(ref uint lastFrameId, ref float clockSeconds)
        {
            uint frameId = TimeSliceScheduler.CurrentFrameId;
            float deltaSeconds = frameId != lastFrameId ? LockedVisualTickDeltaSeconds : 0f;
            lastFrameId = frameId;
            clockSeconds = ResolveWrappedTime(clockSeconds + deltaSeconds);
            return deltaSeconds;
        }

        private void CacheRenderContextCameraIfMissing()
        {
            if (_primaryCamera == null)
                _primaryCamera = GlobalRenderContext.CurrentCamera;
        }

        private static bool TryBindVaultDescriptor<T>(
            IDataVault vault,
            BufferID bufferId,
            out VaultGenerationHandle<T> handle) where T : struct
        {
            handle = default;
            if (vault == null)
                return false;

            if (!vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> candidate) ||
                !IsOwnedHandle(in candidate, bufferId))
            {
                return false;
            }

            handle = candidate;
            return true;
        }

        private static bool IsOwnedHandle<T>(in VaultGenerationHandle<T> handle, BufferID bufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                handle.SystemID == (uint)OwnerSystemId &&
                handle.Generation != 0u;
        }

        private static ulong VaultMutationGuardBit(BufferID bufferId)
        {
            int bitIndex = unchecked((int)((uint)(int)bufferId & 63u));
            return 1UL << bitIndex;
        }

        private bool TryResolveHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return _vault != null &&
                requiredLength > 0 &&
                IsOwnedHandle(in handle, bufferId) &&
                _vault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength;
        }

        private bool TryAcquireWriteBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out IDataVault writeVault,
            out NativeArray<T> buffer) where T : struct
        {
            writeVault = null;
            buffer = default;
            IDataVault vault = _vault;
            if (vault == null ||
                requiredLength <= 0 ||
                !IsOwnedHandle(in handle, bufferId) ||
                HasWriteBufferVault(bufferId) ||
                !vault.TryAcquireWriteLock(in handle, OwnerSystemId, out buffer))
            {
                return false;
            }

            bool releaseOnFailure = true;
            try
            {
                if (buffer.IsCreated && buffer.Length >= requiredLength)
                {
                    StoreWriteBufferVault(bufferId, vault);
                    writeVault = vault;
                    releaseOnFailure = false;
                    return true;
                }

                buffer = default;
                return false;
            }
            finally
            {
                if (releaseOnFailure)
                    vault.ReleaseWriteLock(in handle, OwnerSystemId);
            }
        }

        private void ReleaseWriteBuffer<T>(IDataVault writeVault, in VaultGenerationHandle<T> handle, BufferID bufferId) where T : struct
        {
            IDataVault storedVault = TakeWriteBufferVault(bufferId);
            IDataVault vault = storedVault ?? writeVault;
            if (vault != null && IsOwnedHandle(in handle, bufferId))
                vault.ReleaseWriteLock(in handle, OwnerSystemId);
        }

        private bool TryAcquireReadPin<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _vault;
            ulong guardMask = VaultMutationGuardBit(bufferId);
            if (vault == null ||
                requiredLength <= 0 ||
                !IsOwnedHandle(in handle, bufferId) ||
                !vault.TryAcquireMutationGuard(guardMask))
            {
                return false;
            }

            if (vault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                StoreReadPinVault(bufferId, vault);
                return true;
            }

            vault.ReleaseMutationGuard(guardMask);
            buffer = default;
            return false;
        }

        private void ReleaseReadPin(BufferID bufferId)
        {
            IDataVault vault = TakeReadPinVault(bufferId);
            vault?.ReleaseMutationGuard(VaultMutationGuardBit(bufferId));
        }

        private void StoreWriteBufferVault(BufferID bufferId, IDataVault vault)
        {
            if (bufferId == BufferID.JacobianFoamParams)
                _paramsWriteVault = vault;
            else if (bufferId == BufferID.JacobianFoamTuning)
                _tuningWriteVault = vault;
            else if (bufferId == BufferID.JacobianFoamWakeImpacts)
                _wakeWriteVault = vault;
            else if (bufferId == BufferID.JacobianFoamTelemetryRing)
                _telemetryWriteVault = vault;
        }

        private IDataVault TakeWriteBufferVault(BufferID bufferId)
        {
            if (bufferId == BufferID.JacobianFoamParams)
            {
                IDataVault vault = _paramsWriteVault;
                _paramsWriteVault = null;
                return vault;
            }

            if (bufferId == BufferID.JacobianFoamTuning)
            {
                IDataVault vault = _tuningWriteVault;
                _tuningWriteVault = null;
                return vault;
            }

            if (bufferId == BufferID.JacobianFoamWakeImpacts)
            {
                IDataVault vault = _wakeWriteVault;
                _wakeWriteVault = null;
                return vault;
            }

            if (bufferId == BufferID.JacobianFoamTelemetryRing)
            {
                IDataVault vault = _telemetryWriteVault;
                _telemetryWriteVault = null;
                return vault;
            }

            return null;
        }

        private bool HasWriteBufferVault(BufferID bufferId)
        {
            if (bufferId == BufferID.JacobianFoamParams)
                return _paramsWriteVault != null;
            if (bufferId == BufferID.JacobianFoamTuning)
                return _tuningWriteVault != null;
            if (bufferId == BufferID.JacobianFoamWakeImpacts)
                return _wakeWriteVault != null;
            if (bufferId == BufferID.JacobianFoamTelemetryRing)
                return _telemetryWriteVault != null;
            return false;
        }

        private void StoreReadPinVault(BufferID bufferId, IDataVault vault)
        {
            if (bufferId == BufferID.JacobianFoamTuning)
                _tuningReadPinVault = vault;
            else if (bufferId == BufferID.JacobianFoamWakeImpacts)
                _wakeReadPinVault = vault;
            else if (bufferId == BufferID.JacobianFoamTelemetryRing)
                _telemetryReadPinVault = vault;
        }

        private IDataVault TakeReadPinVault(BufferID bufferId)
        {
            if (bufferId == BufferID.JacobianFoamTuning)
            {
                IDataVault vault = _tuningReadPinVault;
                _tuningReadPinVault = null;
                return vault;
            }

            if (bufferId == BufferID.JacobianFoamWakeImpacts)
            {
                IDataVault vault = _wakeReadPinVault;
                _wakeReadPinVault = null;
                return vault;
            }

            if (bufferId == BufferID.JacobianFoamTelemetryRing)
            {
                IDataVault vault = _telemetryReadPinVault;
                _telemetryReadPinVault = null;
                return vault;
            }

            return null;
        }

        private bool TrySeedDefaultTuning()
        {
            if (!TryAcquireWriteBuffer(
                    in _tuningHandle,
                    BufferID.JacobianFoamTuning,
                    1,
                    out IDataVault tuningWriteVault,
                    out NativeArray<FoamTuningDTO> tuning))
            {
                return false;
            }

            try
            {
                if (tuning.IsCreated && tuning.Length > 0 && tuning[0].Version == 0u)
                    tuning[0] = JacobianFoamContracts.CreateDefaultTuning();
                return true;
            }
            finally
            {
                ReleaseWriteBuffer(tuningWriteVault, in _tuningHandle, BufferID.JacobianFoamTuning);
            }
        }

        private static float2 ResolveCameraScrollOffset(Camera camera, float textureWorldSizeMeters)
        {
            Vector3 runtimePosition = camera != null ? camera.transform.position : Vector3.zero;
            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!AbsoluteUniversePosition.IsFinite(in originAup))
                return JacobianFoamContracts.ResolveWrappedScrollOffset(new double2(runtimePosition.x, runtimePosition.z), textureWorldSizeMeters);

            AbsoluteUniversePosition cameraAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            double3 absolute = cameraAup.ToAbsoluteDouble3();
            return JacobianFoamContracts.ResolveWrappedScrollOffset(new double2(absolute.x, absolute.z), textureWorldSizeMeters);
        }
    }
}
