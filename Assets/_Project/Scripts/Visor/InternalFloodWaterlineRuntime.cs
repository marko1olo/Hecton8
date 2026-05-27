using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Visor
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-6920)]
    public sealed class InternalFloodWaterlineRuntime : MonoBehaviour, ILateFrameTickable, IOriginShiftListener, IServiceHeartbeat, IServiceShutdown, IGlobalRegistryHotSwapListener
    {
        private static int s_x001InternalFloodWaterlineRuntimeSignalPushDropCount;
        private const int TelemetryCapacity = 300;
        private const int TelemetryEntrySizeBytes = 40;
        private const SystemID VaultOwnerSystemId = SystemID.UI;
        private const BufferID TelemetryBufferId = BufferID.InternalFloodWaterlineTelemetryRing;
        private const uint DumpMagic = 0x4946574Cu; // IFWL
        private const int DumpVersion = 1;
        private const float FloodVisibleThreshold01 = 0.001f;
        private const float CrossingEpsilonMeters = 0.03f;
        private const float TransitionSmoothingSeconds = 0.22f;
        private const float DropletDurationSeconds = 2f;
        private const float MinQualityRefractionStrength = 0f;
        private const float MaxQualityRefractionStrength = 0.0018f;
        private const float InternalWaterlineInvalidY = -100000f;
        private const float ShaderFloatEpsilon = 0.0001f;
        private const uint WaterSplashSourceHash = 0x49535753u; // ISWS
        private const uint ScreenBubbleSpeciesHash = 0x53434242u; // SCBB
        private const byte ScreenBubbleDebrisKind = 12;
        private const string DumpFileName = "Dump_INTERNAL_FLOOD_RENDERER.bin";
        private static readonly int InternalWaterlineYId = Shader.PropertyToID("_InternalWaterlineY");
        private static readonly int InternalWaterColorId = Shader.PropertyToID("_InternalWaterColor");
        private static readonly int InternalWaterlineRuntimeId = Shader.PropertyToID("_InternalWaterlineRuntime");
        private static readonly int InternalWaterlineDistortionId = Shader.PropertyToID("_InternalWaterlineDistortion");

        [StructLayout(LayoutKind.Explicit, Size = TelemetryEntrySizeBytes)]
        private struct WaterlineTelemetryEntry
        {
            [FieldOffset(0)]
            public uint Frame;
            [FieldOffset(4)]
            public uint Sequence;
            [FieldOffset(8)]
            public int RoomId;
            [FieldOffset(12)]
            public float Fill01;
            [FieldOffset(16)]
            public float CurrentWaterlineY;
            [FieldOffset(20)]
            public float TargetWaterlineY;
            [FieldOffset(24)]
            public float CameraY;
            [FieldOffset(28)]
            public float Droplets01;
            [FieldOffset(32)]
            public byte Flags;
            [FieldOffset(33)]
            public byte Reserved0;
            [FieldOffset(34)]
            public ushort Reserved1;
            [FieldOffset(36)]
            public uint StateHash;
        }

        internal static InternalFloodWaterlineRuntime ActiveRuntimeInstance { get; private set; }

        [SerializeField] private Color internalWaterColor = new Color(0.08f, 0.42f, 0.50f, 0.42f);
        [SerializeField, Range(0f, 1f)] private float tintStrength = 0.58f;
        [SerializeField, Range(0.001f, 0.1f)] private float edgeSoftness = 0.018f;

        private VaultGenerationHandle<WaterlineTelemetryEntry> _telemetryHandle;
        private IDataVault _dataVault;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private IHabitatGraphService _habitatGraph;
        private AbsoluteUniversePosition _lastCameraAup;
        private int _telemetryCursor;
        private int _cachedRoomId = -1;
        private int _currentRoomId = -1;
        private int _lastProcessedExternalDropletFrame = int.MinValue;
        private float _currentWaterlineY = InternalWaterlineInvalidY;
        private float _targetWaterlineY = InternalWaterlineInvalidY;
        private float _currentFill01;
        private float _dropletSecondsRemaining;
        private float _lastPublishedWaterlineY = float.PositiveInfinity;
        private Color _lastPublishedWaterColor = Color.clear;
        private Vector4 _lastPublishedRuntime = Vector4.positiveInfinity;
        private Vector4 _lastPublishedDistortion = Vector4.positiveInfinity;
        private float _cachedGlobalQualityWeight01 = 1f;
        private float _cachedQualityPressure01;
        private bool _hasWaterline;
        private bool _cameraSubmerged;
        private bool _hasPreviousSubmergedState;
        private bool _registeredLateFrameTick;
        private bool _registeredOriginShift;
        private bool _hotSwapListenerRegistered;
        private bool _isInitialized;
        private bool _blackBoxDumped;
        private bool _shaderGlobalsDirty = true;
        private bool _pendingInactiveShaderGlobals = true;
        private float _pendingShaderFill01;
        private bool _lastCameraAupValid;
        private int _tickCount;

        public bool IsInitialized => _isInitialized && IsVaultHandleCreated(in _telemetryHandle);
        public ServiceHeartbeatState HeartbeatState => IsInitialized ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;
        public bool IsServiceReady => IsInitialized;
        int IServiceHeartbeat.TickCount => _tickCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ActiveRuntimeInstance = null;
        }

        public static InternalFloodWaterlineRuntime EnsureRuntimeInstance()
        {
            if (ActiveRuntimeInstance != null)
                return ActiveRuntimeInstance;

            GameObject root = new GameObject("[InternalFloodWaterlineRuntime]"); // COLD ALLOC: GameObject[1] - bootstrap-owned internal flood visor bridge - owner: InternalFloodWaterlineRuntime
            return root.AddComponent<InternalFloodWaterlineRuntime>();
        }

        public void InitializeService()
        {
            EnsureNativeTelemetry();
            _shaderGlobalsDirty = true;
            CacheRuntimeDependenciesCold();
            RefreshQualityPolicy();
            _isInitialized = true;
            ActiveRuntimeInstance = this;
            TryRegisterHotSwapListener();
            RegisterRuntime();
            PublishInactiveGlobals();
        }

        public void OnServiceShutdown()
        {
            Shutdown();
        }

        private void OnEnable()
        {
            ActiveRuntimeInstance = this;
            _shaderGlobalsDirty = true;
            TryRegisterHotSwapListener();
            if (_isInitialized)
                RegisterRuntime();
        }

        private void OnDisable()
        {
            UnregisterRuntime();
            TryUnregisterHotSwapListener();
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        private void AdvanceWaterlinePresentation(float deltaTime)
        {
            _tickCount++;
            if (!_isInitialized || deltaTime <= 0f)
                return;

            RefreshQualityPolicy();
            ConsumeExternalDropletSignals();
            ConsumePlayerExhaleSignals();
            if (_dropletSecondsRemaining > 0f)
                _dropletSecondsRemaining = math.max(0f, _dropletSecondsRemaining - deltaTime);

            if (!TryResolveRuntimeContext(out IPlayerRuntimeContext runtimeContext, out PlayerRuntimePoseSnapshot poseSnapshot))
            {
                ClearWaterlineState();
                return;
            }

            IHabitatGraphService habitatGraph = _habitatGraph;
            if (habitatGraph == null ||
                !TryResolvePlayerRuntimePosition(in poseSnapshot, out Vector3 playerRuntimePosition) ||
                !habitatGraph.TryResolveRoomWaterline(playerRuntimePosition, _cachedRoomId, out HabitatRoomWaterlineSnapshot snapshot) ||
                !snapshot.IsValid)
            {
                ClearWaterlineState();
                return;
            }

            _cachedRoomId = snapshot.RoomId;
            _currentRoomId = snapshot.RoomId;
            _currentFill01 = snapshot.Fill01;

            if (snapshot.Fill01 <= FloodVisibleThreshold01)
            {
                ClearWaterlineState();
                return;
            }

            Vector3 cameraRuntimePosition = ResolveCameraRuntimePosition(runtimeContext, in poseSnapshot);
            if (IsFiniteAup(in poseSnapshot.Aup))
            {
                _lastCameraAup = poseSnapshot.Aup;
                _lastCameraAupValid = true;
            }
            else
            {
                _lastCameraAupValid = TryResolveAupFromRuntimeOrigin(cameraRuntimePosition, out _lastCameraAup);
            }

            float targetY = snapshot.SurfaceY;
            if (!math.isfinite(targetY))
            {
                WriteTelemetry(in snapshot, cameraRuntimePosition.y, 1);
                DumpBlackBoxOnce();
                ClearWaterlineState();
                return;
            }

            _targetWaterlineY = targetY;
            if (!_hasWaterline || !math.isfinite(_currentWaterlineY))
                _currentWaterlineY = targetY;
            else
                _currentWaterlineY = math.lerp(_currentWaterlineY, targetY, math.saturate(deltaTime * math.rcp(TransitionSmoothingSeconds)));

            bool wasSubmerged = _cameraSubmerged;
            _cameraSubmerged = cameraRuntimePosition.y < _currentWaterlineY - CrossingEpsilonMeters;
            if (_hasPreviousSubmergedState && wasSubmerged != _cameraSubmerged)
                PublishCrossingFeedback(cameraRuntimePosition, wasSubmerged);
            _hasPreviousSubmergedState = true;

            _hasWaterline = true;
            QueueShaderGlobals(snapshot.Fill01);
            WriteTelemetry(in snapshot, cameraRuntimePosition.y, 0);
        }

        public void LateFrameTick()
        {
            AdvanceWaterlinePresentation(SystemDispatcher.CurrentFrameDeltaTime);

            if (!_shaderGlobalsDirty)
                return;

            if (_pendingInactiveShaderGlobals)
                PublishInactiveGlobals();
            else
                PublishShaderGlobals(_pendingShaderFill01);
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            float shiftY = shiftData.ShiftOffset.y;
            if (!math.isfinite(shiftY))
                return;

            if (math.isfinite(_currentWaterlineY))
                _currentWaterlineY -= shiftY;
            if (math.isfinite(_targetWaterlineY))
                _targetWaterlineY -= shiftY;

            QueueShaderGlobals(_hasWaterline ? _currentFill01 : 0f);
        }

        private void RegisterRuntime()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredLateFrameTick)
                _registeredLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);

            if (!_registeredOriginShift)
            {
                HectonFloatingOrigin.RegisterListener(this);
                _registeredOriginShift = true;
            }
        }

        private void UnregisterRuntime()
        {
            if (_registeredLateFrameTick)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrameTick = false;
            }

            if (_registeredOriginShift)
            {
                HectonFloatingOrigin.UnregisterListener(this);
                _registeredOriginShift = false;
            }
        }

        private void Shutdown()
        {
            UnregisterRuntime();
            TryUnregisterHotSwapListener();
            _isInitialized = false;
            _playerRuntimeContext = null;
            _habitatGraph = null;
            IDataVault vault = _dataVault;
            if (vault != null && IsVaultHandleCreated(in _telemetryHandle))
                vault.ReleaseBuffer(in _telemetryHandle);
            _telemetryHandle = default;
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
            PublishInactiveGlobals();
        }

        private void EnsureNativeTelemetry()
        {
            IDataVault vault = CacheDataVaultCold();
            if (vault == null)
                return;

            if (IsVaultHandleCreated(in _telemetryHandle) &&
                vault.TryReadOnlyHandle(in _telemetryHandle, out NativeArray<WaterlineTelemetryEntry>.ReadOnly telemetry) &&
                telemetry.IsCreated &&
                telemetry.Length >= TelemetryCapacity)
            {
                return;
            }

            if (IsVaultHandleCreated(in _telemetryHandle))
                vault.ReleaseBuffer(in _telemetryHandle);

            _telemetryHandle = vault.EnsureGenerationHandle<WaterlineTelemetryEntry>(
                TelemetryBufferId,
                TelemetryCapacity,
                VaultOwnerSystemId,
                NativeArrayOptions.ClearMemory);
        }

        private IDataVault CacheDataVaultCold()
        {
            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;

            return _dataVault;
        }

        private static bool IsVaultHandleCreated<T>(in VaultGenerationHandle<T> handle) where T : unmanaged
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private bool TryResolveRuntimeContext(
            out IPlayerRuntimeContext runtimeContext,
            out PlayerRuntimePoseSnapshot poseSnapshot)
        {
            runtimeContext = _playerRuntimeContext;
            poseSnapshot = default;
            return runtimeContext != null &&
                   runtimeContext.IsInitialized &&
                   runtimeContext.TryGetPlayerPoseSnapshot(out poseSnapshot) &&
                   math.all(math.isfinite(poseSnapshot.RuntimePosition));
        }

        private static bool TryResolvePlayerRuntimePosition(in PlayerRuntimePoseSnapshot poseSnapshot, out Vector3 runtimePosition)
        {
            float3 current = poseSnapshot.RuntimePosition;
            runtimePosition = new Vector3(current.x, current.y, current.z);
            return math.all(math.isfinite(current));
        }

        private static Vector3 ResolveCameraRuntimePosition(
            IPlayerRuntimeContext runtimeContext,
            in PlayerRuntimePoseSnapshot poseSnapshot)
        {
            Camera playerCamera = runtimeContext.PlayerCamera;
            if (playerCamera != null && IsFiniteVector(playerCamera.transform.position))
                return playerCamera.transform.position;

            float3 fallback = poseSnapshot.RuntimePosition;
            return new Vector3(fallback.x, fallback.y, fallback.z);
        }

        private void ConsumePlayerExhaleSignals()
        {
            ReadOnlySpan<PlayerExhaleSignal> signals = SignalBus<PlayerExhaleSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
                HandlePlayerExhale();
        }

        private void HandlePlayerExhale()
        {
            if (!_cameraSubmerged || !_hasWaterline || !_lastCameraAupValid || !IsFiniteAup(in _lastCameraAup))
                return;

            DebrisSpawnSignal signal = new DebrisSpawnSignal
            {
                PositionAup = _lastCameraAup,
                SpeciesHash = ScreenBubbleSpeciesHash,
                SourceEntityId = WaterSplashSourceHash,
                Intensity01 = 1f,
                DebrisKind = ScreenBubbleDebrisKind,
                Flags = 1,
                Quantity = 6
            };
            SignalBus<DebrisSpawnSignal>.TryPushTracked(in signal, ref s_x001InternalFloodWaterlineRuntimeSignalPushDropCount);
        }

        private void ConsumeExternalDropletSignals()
        {
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_lastProcessedExternalDropletFrame == frame)
                return;

            _lastProcessedExternalDropletFrame = frame;
            ReadOnlySpan<VisorDropletSignal> signals = SignalBus<VisorDropletSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                VisorDropletSignal signal = signals[i];
                if (!math.isfinite(signal.Intensity01) || !math.isfinite(signal.DurationSeconds))
                    continue;

                float intensity01 = math.saturate(signal.Intensity01);
                if (intensity01 <= 0.001f)
                    continue;

                float duration = math.max(0.05f, signal.DurationSeconds);
                _lastCameraAup = signal.PositionAup;
                _lastCameraAupValid = IsFiniteAup(in _lastCameraAup);
                _dropletSecondsRemaining = math.max(_dropletSecondsRemaining, duration * intensity01);
                QueueShaderGlobals(_hasWaterline ? _currentFill01 : 0f);
            }
        }

        private void CacheRuntimeDependenciesCold()
        {
            if (_playerRuntimeContext == null)
                _playerRuntimeContext = GlobalRegistry.Player;

            if (_habitatGraph == null)
                _habitatGraph = GlobalRegistry.HabitatGraph;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher:
                    _registeredLateFrameTick = false;
                    if (currentService != null && isActiveAndEnabled && _isInitialized)
                        RegisterRuntime();
                    return;

                case GlobalRegistryServiceSlot.DataVault:
                    RebindDataVault(currentService as IDataVault);
                    return;

                case GlobalRegistryServiceSlot.Player:
                    _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                    return;

                case GlobalRegistryServiceSlot.Logistics:
                    if (currentService is IHabitatGraphService || previousService is IHabitatGraphService)
                        _habitatGraph = currentService as IHabitatGraphService;
                    return;
            }
        }

        private void RebindDataVault(IDataVault nextVault)
        {
            if (ReferenceEquals(_dataVault, nextVault))
                return;

            IDataVault currentVault = _dataVault;
            if (currentVault != null && IsVaultHandleCreated(in _telemetryHandle))
                currentVault.ReleaseBuffer(in _telemetryHandle);

            _telemetryHandle = default;
            _dataVault = nextVault;
            if (_isInitialized)
                EnsureNativeTelemetry();
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        private void RefreshQualityPolicy()
        {
            float quality = ResolveGlobalQualityWeight01();
            float pressure = 1f - SmoothQuality01(quality);
            if (math.abs(_cachedGlobalQualityWeight01 - quality) > ShaderFloatEpsilon ||
                math.abs(_cachedQualityPressure01 - pressure) > ShaderFloatEpsilon)
            {
                _shaderGlobalsDirty = true;
            }

            _cachedGlobalQualityWeight01 = quality;
            _cachedQualityPressure01 = pressure;
        }

        private void PublishCrossingFeedback(Vector3 cameraRuntimePosition, bool wasSubmerged)
        {
            if (wasSubmerged && !_cameraSubmerged)
                _dropletSecondsRemaining = DropletDurationSeconds;

            if (!TryResolveAupFromRuntimeOrigin(cameraRuntimePosition, out AbsoluteUniversePosition cameraAup))
                return;

            AcousticPingSignal ping = new AcousticPingSignal
            {
                PositionAup = cameraAup,
                RadiusMeters = 7.5f,
                Intensity01 = 0.42f,
                SourceId = WaterSplashSourceHash,
                Channel = 0,
                Flags = 0
            };
            SignalBus<AcousticPingSignal>.TryPushTracked(in ping, ref s_x001InternalFloodWaterlineRuntimeSignalPushDropCount);
        }

        private void QueueShaderGlobals(float fill01)
        {
            _pendingShaderFill01 = math.isfinite(fill01) ? math.saturate(fill01) : 0f;
            _pendingInactiveShaderGlobals = false;
            _shaderGlobalsDirty = true;
        }

        private void QueueInactiveGlobals()
        {
            _pendingShaderFill01 = 0f;
            _pendingInactiveShaderGlobals = true;
            _shaderGlobalsDirty = true;
        }

        private void PublishShaderGlobals(float fill01)
        {
            float active01 = _hasWaterline ? 1f : 0f;
            float droplets01 = math.saturate(_dropletSecondsRemaining * math.rcp(DropletDurationSeconds));
            float qualityPressure01 = math.saturate(_cachedQualityPressure01);
            float refraction = math.lerp(MinQualityRefractionStrength, MaxQualityRefractionStrength, 1f - qualityPressure01);
            Vector4 runtime = new Vector4(active01, math.saturate(fill01), droplets01, _currentRoomId);
            Vector4 distortion = new Vector4(refraction, math.saturate(tintStrength), math.max(0.001f, edgeSoftness), qualityPressure01);

            SetGlobalFloatIfChanged(InternalWaterlineYId, _hasWaterline ? _currentWaterlineY : InternalWaterlineInvalidY, ref _lastPublishedWaterlineY);
            SetGlobalColorIfChanged(InternalWaterColorId, internalWaterColor, ref _lastPublishedWaterColor);
            SetGlobalVectorIfChanged(InternalWaterlineRuntimeId, runtime, ref _lastPublishedRuntime);
            SetGlobalVectorIfChanged(InternalWaterlineDistortionId, distortion, ref _lastPublishedDistortion);
            _shaderGlobalsDirty = false;
            _pendingInactiveShaderGlobals = false;
        }

        private void PublishInactiveGlobals()
        {
            SetGlobalFloatIfChanged(InternalWaterlineYId, InternalWaterlineInvalidY, ref _lastPublishedWaterlineY);
            SetGlobalColorIfChanged(InternalWaterColorId, internalWaterColor, ref _lastPublishedWaterColor);
            SetGlobalVectorIfChanged(InternalWaterlineRuntimeId, Vector4.zero, ref _lastPublishedRuntime);
            SetGlobalVectorIfChanged(InternalWaterlineDistortionId, new Vector4(0f, math.saturate(tintStrength), math.max(0.001f, edgeSoftness), 1f), ref _lastPublishedDistortion);
            _shaderGlobalsDirty = false;
            _pendingInactiveShaderGlobals = true;
        }

        private void ClearWaterlineState()
        {
            if (!_hasWaterline &&
                !_cameraSubmerged &&
                !_hasPreviousSubmergedState &&
                _cachedRoomId < 0 &&
                _currentRoomId < 0 &&
                _dropletSecondsRemaining <= 0f)
            {
                return;
            }

            bool hasDroplets = _dropletSecondsRemaining > 0f;
            _hasWaterline = false;
            _cameraSubmerged = false;
            _hasPreviousSubmergedState = false;
            _cachedRoomId = -1;
            _currentRoomId = -1;
            _currentFill01 = 0f;
            _lastCameraAupValid = false;
            _currentWaterlineY = InternalWaterlineInvalidY;
            _targetWaterlineY = InternalWaterlineInvalidY;
            if (hasDroplets)
                QueueShaderGlobals(0f);
            else
                QueueInactiveGlobals();
        }

        private void SetGlobalFloatIfChanged(int shaderId, float value, ref float cachedValue)
        {
            if (!_shaderGlobalsDirty && math.abs(cachedValue - value) <= ShaderFloatEpsilon)
                return;

            Shader.SetGlobalFloat(shaderId, value);
            cachedValue = value;
        }

        private void SetGlobalColorIfChanged(int shaderId, Color value, ref Color cachedValue)
        {
            if (!_shaderGlobalsDirty &&
                math.abs(cachedValue.r - value.r) <= ShaderFloatEpsilon &&
                math.abs(cachedValue.g - value.g) <= ShaderFloatEpsilon &&
                math.abs(cachedValue.b - value.b) <= ShaderFloatEpsilon &&
                math.abs(cachedValue.a - value.a) <= ShaderFloatEpsilon)
            {
                return;
            }

            Shader.SetGlobalColor(shaderId, value);
            cachedValue = value;
        }

        private void SetGlobalVectorIfChanged(int shaderId, Vector4 value, ref Vector4 cachedValue)
        {
            if (!_shaderGlobalsDirty &&
                math.abs(cachedValue.x - value.x) <= ShaderFloatEpsilon &&
                math.abs(cachedValue.y - value.y) <= ShaderFloatEpsilon &&
                math.abs(cachedValue.z - value.z) <= ShaderFloatEpsilon &&
                math.abs(cachedValue.w - value.w) <= ShaderFloatEpsilon)
            {
                return;
            }

            Shader.SetGlobalVector(shaderId, value);
            cachedValue = value;
        }

        private void WriteTelemetry(in HabitatRoomWaterlineSnapshot snapshot, float cameraY, byte flags)
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !IsVaultHandleCreated(in _telemetryHandle))
            {
                return;
            }

            float droplets01 = math.saturate(_dropletSecondsRemaining * math.rcp(DropletDurationSeconds));
            byte telemetryFlags = flags;
            if (_cameraSubmerged)
                telemetryFlags |= 2;
            if (_cachedQualityPressure01 > 0.5f)
                telemetryFlags |= 8;
            byte qualityByte = EncodeQualityWeightByte(_cachedGlobalQualityWeight01);
            WaterlineTelemetryEntry entry = new WaterlineTelemetryEntry
            {
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                Sequence = snapshot.Sequence,
                RoomId = snapshot.RoomId,
                Fill01 = snapshot.Fill01,
                CurrentWaterlineY = _currentWaterlineY,
                TargetWaterlineY = _targetWaterlineY,
                CameraY = cameraY,
                Droplets01 = droplets01,
                Flags = telemetryFlags,
                Reserved0 = qualityByte,
                Reserved1 = 0,
                StateHash = ResolveTelemetryHash(snapshot.RoomId, snapshot.Fill01, _currentWaterlineY, cameraY, droplets01, qualityByte)
            };

            if (!vault.TryAcquireWriteLock(in _telemetryHandle, VaultOwnerSystemId, out NativeArray<WaterlineTelemetryEntry> telemetry))
                return;

            try
            {
                if (vault.IsCompactionFenceActive ||
                    !telemetry.IsCreated ||
                    telemetry.Length < TelemetryCapacity)
                {
                    return;
                }

                telemetry[_telemetryCursor] = entry;
                _telemetryCursor = (_telemetryCursor + 1) % TelemetryCapacity;
            }
            finally
            {
                vault.ReleaseWriteLock(in _telemetryHandle, VaultOwnerSystemId);
            }

            if ((flags & 1) != 0)
                DumpBlackBoxOnce();
        }

        private void DumpBlackBoxOnce()
        {
            IDataVault vault = _dataVault;
            if (_blackBoxDumped ||
                vault == null ||
                !IsVaultHandleCreated(in _telemetryHandle))
            {
                return;
            }

            _blackBoxDumped = true;
            try
            {
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "AgentLogs", DumpFileName));
                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    Span<byte> header = stackalloc byte[24];
                    WriteTelemetryDumpHeader(header, _telemetryCursor, _tickCount);
                    stream.Write(header);

                    Span<byte> row = stackalloc byte[TelemetryEntrySizeBytes];
                    for (int i = 0; i < TelemetryCapacity; i++)
                    {
                        if (!TryReadTelemetryEntry(vault, i, out WaterlineTelemetryEntry entry))
                            entry = default;

                        WriteTelemetryEntry(row, in entry);
                        stream.Write(row);
                    }
                }
            }
            catch (IOException)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError("[InternalFloodWaterlineRuntime] Black box dump IO failed.");
#endif
            }
            catch (UnauthorizedAccessException)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError("[InternalFloodWaterlineRuntime] Black box dump access denied.");
#endif
            }
            catch (ObjectDisposedException)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError("[InternalFloodWaterlineRuntime] Black box dump stream disposed.");
#endif
            }
            catch (InvalidOperationException)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError("[InternalFloodWaterlineRuntime] Black box dump invalid operation.");
#endif
            }
            catch (ArgumentException)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError("[InternalFloodWaterlineRuntime] Black box dump argument invalid.");
#endif
            }
            catch (NotSupportedException)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError("[InternalFloodWaterlineRuntime] Black box dump path unsupported.");
#endif
            }
        }

        private bool TryReadTelemetryEntry(IDataVault vault, int index, out WaterlineTelemetryEntry entry)
        {
            entry = default;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                index < 0 ||
                index >= TelemetryCapacity ||
                !IsVaultHandleCreated(in _telemetryHandle) ||
                !vault.TryReadOnlyHandle(in _telemetryHandle, out NativeArray<WaterlineTelemetryEntry>.ReadOnly telemetry) ||
                vault.IsCompactionFenceActive ||
                !telemetry.IsCreated ||
                index >= telemetry.Length)
            {
                return false;
            }

            entry = telemetry[index];
            return true;
        }

        private static void WriteTelemetryDumpHeader(Span<byte> destination, int telemetryCursor, int tickCount)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(0, 4), DumpMagic);
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(4, 4), DumpVersion);
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(8, 4), TelemetryEntrySizeBytes);
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(12, 4), TelemetryCapacity);
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(16, 4), telemetryCursor);
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(20, 4), tickCount);
        }

        private static void WriteTelemetryEntry(Span<byte> destination, in WaterlineTelemetryEntry entry)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(0, 4), entry.Frame);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(4, 4), entry.Sequence);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(8, 4), unchecked((uint)entry.RoomId));
            WriteFloatLittleEndian(destination.Slice(12, 4), entry.Fill01);
            WriteFloatLittleEndian(destination.Slice(16, 4), entry.CurrentWaterlineY);
            WriteFloatLittleEndian(destination.Slice(20, 4), entry.TargetWaterlineY);
            WriteFloatLittleEndian(destination.Slice(24, 4), entry.CameraY);
            WriteFloatLittleEndian(destination.Slice(28, 4), entry.Droplets01);
            destination[32] = entry.Flags;
            destination[33] = entry.Reserved0;
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(34, 2), entry.Reserved1);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(36, 4), entry.StateHash);
        }

        private static void WriteFloatLittleEndian(Span<byte> destination, float value)
        {
            BinaryPrimitives.WriteInt32LittleEndian(destination, BitConverter.SingleToInt32Bits(value));
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return math.isfinite(value.x) &&
                   math.isfinite(value.y) &&
                   math.isfinite(value.z);
        }

        private static bool IsFiniteAup(in AbsoluteUniversePosition position)
        {
            return math.isfinite(position.LocalX) &&
                   math.isfinite(position.LocalY) &&
                   math.isfinite(position.LocalZ);
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition absoluteAup)
        {
            absoluteAup = default;
            if (!IsFiniteVector(runtimePosition))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!IsFiniteAup(in originAup))
                return false;

            absoluteAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return IsFiniteAup(in absoluteAup);
        }

        private static float ResolveGlobalQualityWeight01()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.isfinite(quality) ? math.saturate(quality) : 1f;
        }

        private static float SmoothQuality01(float quality)
        {
            quality = math.isfinite(quality) ? math.saturate(quality) : 1f;
            return quality * quality * (3f - (2f * quality));
        }

        private static byte EncodeQualityWeightByte(float quality)
        {
            return (byte)math.clamp((int)math.round(math.saturate(quality) * 255f), 0, 255);
        }

        private static uint ResolveTelemetryHash(int roomId, float fill01, float waterlineY, float cameraY, float droplets01, byte qualityByte)
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)roomId) * 16777619u;
            hash = (hash ^ math.asuint(fill01)) * 16777619u;
            hash = (hash ^ math.asuint(waterlineY)) * 16777619u;
            hash = (hash ^ math.asuint(cameraY)) * 16777619u;
            hash = (hash ^ math.asuint(droplets01)) * 16777619u;
            hash = (hash ^ qualityByte) * 16777619u;
            return hash;
        }
    }
}
