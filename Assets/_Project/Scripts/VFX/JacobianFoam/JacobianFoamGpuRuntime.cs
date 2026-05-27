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
    public sealed class JacobianFoamGpuRuntime : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const uint PortableMaxThreadsPerThreadGroup = 256u;
        private const float GpuDumpThresholdMicroseconds = 1500f;
        private const int ResolutionRebuildFrameCadence = 30;
        private const int ResolutionHysteresisPixels = 128;
        private const int MaxSingleDispatchResolution = 1024;
        private const float LockedVisualTickDeltaSeconds = 1f / 60f;

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
        private bool _registeredHotSwap;
        private bool _vaultReady;
        private bool _hasPreparedPayload;
        private RenderTexture _activeFoamTexture;
        private FoamRenderGraphPayload _preparedPayload;
        private int _instanceId;
        private bool _deferredTelemetryDumpRequested;
        private int _deferredTelemetryDumpCursor;
        private int _deferredTelemetryDumpWritten;

        private static FoamRenderGraphPayload s_publishedPayload;
        private static bool s_hasPublishedPayload;
        private static int s_publishedOwnerId;
        private static RenderTexture s_publishedFoamTexture;
        private static uint s_renderGraphAckSequence;
        private static int s_renderGraphAckOwnerId;
        private static byte s_renderGraphAckHistoryWriteIndex;

        public void ColdBindDataVault(IDataVault vault)
        {
            _vault = vault;
            EnsureVaultState(true);
        }

        private void OnEnable()
        {
            _instanceId = GetEntityId().GetHashCode();
            CacheDataVaultCold();
            CacheRenderContextCameraIfMissing();
            ResolveKernels();
            EnsureVaultState(true);
            EnsureGpuState(JacobianFoamContracts.ResolveFoamResolution(0.5f, _minResolution, _maxResolution));
            _visualClockSeconds = 0f;
            _lastVisualClockFrameId = uint.MaxValue;
            TryRegisterHotSwapListener();
            TryRegisterLateFrameTickable();
        }

        private void OnDisable()
        {
            FlushDeferredTelemetryDump();
            TryUnregisterLateFrameTickable();
            TryUnregisterHotSwapListener();
            _vaultReady = false;
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
                TryRegisterLateFrameTickable();
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            _vault = currentService as IDataVault;
            _vaultReady = false;
            _paramsHandle = default;
            _tuningHandle = default;
            _wakeHandle = default;
            _telemetryHandle = default;

            if (isActiveAndEnabled && _vault != null)
                EnsureVaultState(true);
        }

        public void LateFrameTick()
        {
            ConsumeRenderGraphAcknowledgement();

            if (_computeShader == null || !HardwareTierDetector.AllowHighResourceComputeShaders)
            {
                ClearPreparedPayload();
                return;
            }

            if (!EnsureVaultState(false))
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
            if (!EnsureGpuState(targetResolution))
            {
                ClearPreparedPayload();
                return;
            }

            _previousScrollOffset = _currentScrollOffset;
            CacheRenderContextCameraIfMissing();
            _currentScrollOffset = ResolveCameraScrollOffset(_primaryCamera, _textureWorldSizeMeters);

            if (!TryAcquireWriteBuffer(
                    in _paramsHandle,
                    1,
                    out NativeArray<FoamComputeParamsDTO> paramsArray))
            {
                ClearPreparedPayload();
                return;
            }

            bool wakeReadPinned = false;
            bool wakeWriteLocked = false;
            bool tuningWriteLocked = false;
            NativeArray<FoamWakeImpactDTO> wakeArray = default;
            NativeArray<FoamTuningDTO> tuningArray = default;
            try
            {
                if (_generateMockStormState)
                {
                    if (!TryAcquireWriteBuffer(
                            in _tuningHandle,
                            1,
                            out tuningArray))
                    {
                        ClearPreparedPayload();
                        return;
                    }

                    tuningWriteLocked = true;
                    if (!TryAcquireWriteBuffer(
                            in _wakeHandle,
                            JacobianFoamContracts.WakeImpactCapacity,
                            out wakeArray))
                    {
                        ClearPreparedPayload();
                        return;
                    }

                    wakeWriteLocked = true;
                    GenerateMockStormStateJob job = new GenerateMockStormStateJob
                    {
                        Params = paramsArray,
                        Tuning = tuningArray,
                        WakeImpacts = wakeArray,
                        TimeSeconds = phaseTime,
                        GlobalQualityWeight = _qualityWeight,
                        DeltaTime = _lastDeltaTime,
                        ScrollOffset = _currentScrollOffset
                    };
                    job.Run();
                    if (tuningArray.IsCreated && tuningArray.Length > 0 && tuningArray[0].Version != 0u)
                        tuning = tuningArray[0];
                }
                else
                {
                    if (!TryAcquireReadPin(
                            in _wakeHandle,
                            BufferID.JacobianFoamWakeImpacts,
                            JacobianFoamContracts.WakeImpactCapacity,
                            out wakeArray))
                    {
                        ClearPreparedPayload();
                        return;
                    }

                    wakeReadPinned = true;
                    ref FoamComputeParamsDTO paramRef = ref JacobianFoamContracts.MutableParamsRef(paramsArray);
                    paramRef = JacobianFoamContracts.BuildParams(in tuning, _qualityWeight, _lastDeltaTime, _currentScrollOffset);
                }

                _lastWakeCount = ResolveWakeCount(wakeArray, _qualityWeight);
                UploadParams(paramsArray);
                UploadWakes(wakeArray, _lastWakeCount);
                RecordTelemetry(tuning);
                PublishRenderGraphPayload();
                _frame++;
            }
            finally
            {
                if (wakeReadPinned)
                    ReleaseReadPin(BufferID.JacobianFoamWakeImpacts);
                if (wakeWriteLocked)
                    ReleaseWriteBuffer(in _wakeHandle);
                if (tuningWriteLocked)
                    ReleaseWriteBuffer(in _tuningHandle);
                ReleaseWriteBuffer(in _paramsHandle);
            }
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

            _vault = GlobalRegistry.DataVault;
            return _vault;
        }

        private void TryRegisterLateFrameTickable()
        {
            if (_registeredLateFrame || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterLateFrameTickable()
        {
            if (!_registeredLateFrame)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredLateFrame = false;
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
            if (_computeShader == null || kernel < 0 || !_computeShader.IsSupported(kernel))
                return false;

            _computeShader.GetKernelThreadGroupSizes(kernel, out uint x, out uint y, out uint z);
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
            if (_computeShader == null || !_computeShader.HasKernel(kernelName))
                return -1;

            int kernel = _computeShader.FindKernel(kernelName);
            return kernel >= 0 && _computeShader.IsSupported(kernel) ? kernel : -1;
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
                IsHandleCreated(in _paramsHandle, BufferID.JacobianFoamParams) &&
                IsHandleCreated(in _tuningHandle, BufferID.JacobianFoamTuning) &&
                IsHandleCreated(in _wakeHandle, BufferID.JacobianFoamWakeImpacts) &&
                IsHandleCreated(in _telemetryHandle, BufferID.JacobianFoamTelemetryRing))
            {
                return true;
            }

            if (allowCreate && !JacobianFoamContracts.EnsureVaultBuffers(_vault))
                return false;

            if (!_vault.TryGetGenerationHandle(BufferID.JacobianFoamParams, out _paramsHandle) ||
                !_vault.TryGetGenerationHandle(BufferID.JacobianFoamTuning, out _tuningHandle) ||
                !_vault.TryGetGenerationHandle(BufferID.JacobianFoamWakeImpacts, out _wakeHandle) ||
                !_vault.TryGetGenerationHandle(BufferID.JacobianFoamTelemetryRing, out _telemetryHandle))
            {
                _vaultReady = false;
                return false;
            }

            if (allowCreate && !TrySeedDefaultTuning())
                return false;

            _vaultReady =
                IsHandleCreated(in _paramsHandle, BufferID.JacobianFoamParams) &&
                IsHandleCreated(in _tuningHandle, BufferID.JacobianFoamTuning) &&
                IsHandleCreated(in _wakeHandle, BufferID.JacobianFoamWakeImpacts) &&
                IsHandleCreated(in _telemetryHandle, BufferID.JacobianFoamTelemetryRing);
            return _vaultReady;
        }

        private bool EnsureGpuState(int targetResolution)
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

            GraphicsFormat targetFormat = ResolveFoamTextureFormat();
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

        private static GraphicsFormat ResolveFoamTextureFormat()
        {
            if (IsFoamTextureFormatSupported(GraphicsFormat.R16_SFloat))
                return GraphicsFormat.R16_SFloat;

            if (IsFoamTextureFormatSupported(GraphicsFormat.R32_SFloat))
                return GraphicsFormat.R32_SFloat;

            return IsFoamTextureFormatSupported(GraphicsFormat.R8_UNorm)
                ? GraphicsFormat.R8_UNorm
                : GraphicsFormat.None;
        }

        private static bool IsFoamTextureFormatSupported(GraphicsFormat format)
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

        private void UploadParams(NativeArray<FoamComputeParamsDTO> paramsArray)
        {
            if (!paramsArray.IsCreated || paramsArray.Length <= 0)
                return;

            GraphicsBuffer writeBuffer = _activeParamsBuffer == _paramsBufferA ? _paramsBufferB : _paramsBufferA;
            if (writeBuffer == null || !writeBuffer.IsValid())
            {
                _activeParamsBuffer = null;
                return;
            }

            NativeArray<FoamComputeParamsDTO> mapped = writeBuffer.LockBufferForWrite<FoamComputeParamsDTO>(0, 1);
            CopyFoamParamsToMappedBufferJob copyJob = new CopyFoamParamsToMappedBufferJob
            {
                Source = paramsArray,
                Destination = mapped
            };
            try
            {
                copyJob.Run();
            }
            finally
            {
                writeBuffer.UnlockBufferAfterWrite<FoamComputeParamsDTO>(1);
            }
            _activeParamsBuffer = writeBuffer;
        }

        private void UploadWakes(NativeArray<FoamWakeImpactDTO> wakeArray, int wakeCount)
        {
            GraphicsBuffer writeBuffer = _activeWakeBuffer == _wakeBufferA ? _wakeBufferB : _wakeBufferA;
            if (writeBuffer == null || !writeBuffer.IsValid())
            {
                _activeWakeBuffer = null;
                return;
            }

            NativeArray<FoamWakeImpactDTO> mapped = writeBuffer.LockBufferForWrite<FoamWakeImpactDTO>(0, JacobianFoamContracts.WakeImpactCapacity);
            CopyFoamWakesToMappedBufferJob copyJob = new CopyFoamWakesToMappedBufferJob
            {
                Source = wakeArray,
                Destination = mapped,
                Count = wakeCount
            };
            try
            {
                copyJob.Run();
            }
            finally
            {
                writeBuffer.UnlockBufferAfterWrite<FoamWakeImpactDTO>(JacobianFoamContracts.WakeImpactCapacity);
            }
            _activeWakeBuffer = writeBuffer;
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
            if (!IsHandleCreated(in _telemetryHandle, BufferID.JacobianFoamTelemetryRing) || _vault == null || _vault.IsCompactionFenceActive)
                return;

            if (!TryAcquireWriteBuffer(
                    in _telemetryHandle,
                    JacobianFoamContracts.TelemetryCapacity,
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
                ReleaseWriteBuffer(in _telemetryHandle);
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

        private static bool IsHandleCreated<T>(in VaultGenerationHandle<T> handle, BufferID bufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) && handle.Generation != 0u;
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
                IsHandleCreated(in handle, bufferId) &&
                _vault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength;
        }

        private bool TryAcquireWriteBuffer<T>(
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (_vault == null ||
                requiredLength <= 0 ||
                !_vault.TryAcquireWriteLock(in handle, SystemID.Vfx, out buffer))
            {
                return false;
            }

            if (buffer.IsCreated && buffer.Length >= requiredLength)
                return true;

            _vault.ReleaseWriteLock(in handle, SystemID.Vfx);
            buffer = default;
            return false;
        }

        private void ReleaseWriteBuffer<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            if (_vault != null)
                _vault.ReleaseWriteLock(in handle, SystemID.Vfx);
        }

        private bool TryAcquireReadPin<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (_vault == null ||
                requiredLength <= 0 ||
                !IsHandleCreated(in handle, bufferId) ||
                !_vault.TryLockBuffer(bufferId, SystemID.Vfx))
            {
                return false;
            }

            if (TryResolveHandle(in handle, bufferId, requiredLength, out buffer))
                return true;

            _vault.TryUnlockBuffer(bufferId, SystemID.Vfx);
            buffer = default;
            return false;
        }

        private void ReleaseReadPin(BufferID bufferId)
        {
            if (_vault != null)
                _vault.TryUnlockBuffer(bufferId, SystemID.Vfx);
        }

        private bool TrySeedDefaultTuning()
        {
            if (!TryAcquireWriteBuffer(
                    in _tuningHandle,
                    1,
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
                ReleaseWriteBuffer(in _tuningHandle);
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
