using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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
        private const int TelemetryEntrySizeBytes = 64;
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
        private const string DumpPayloadLabel = "internalFloodWaterlineDumpPayload";
        private static readonly int InternalWaterlineYId = Shader.PropertyToID("_InternalWaterlineY");
        private static readonly int InternalWaterColorId = Shader.PropertyToID("_InternalWaterColor");
        private static readonly int InternalWaterlineRuntimeId = Shader.PropertyToID("_InternalWaterlineRuntime");
        private static readonly int InternalWaterlineDistortionId = Shader.PropertyToID("_InternalWaterlineDistortion");

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 64)]
        private struct WaterlineTelemetryEntry
        {
            [System.Runtime.InteropServices.FieldOffset(0)]
            public uint Frame;
            [System.Runtime.InteropServices.FieldOffset(4)]
            public uint Sequence;
            [System.Runtime.InteropServices.FieldOffset(8)]
            public int RoomId;
            [System.Runtime.InteropServices.FieldOffset(12)]
            public float Fill01;
            [System.Runtime.InteropServices.FieldOffset(16)]
            public float CurrentWaterlineY;
            [System.Runtime.InteropServices.FieldOffset(20)]
            public float TargetWaterlineY;
            [System.Runtime.InteropServices.FieldOffset(24)]
            public float CameraY;
            [System.Runtime.InteropServices.FieldOffset(28)]
            public float Droplets01;
            [System.Runtime.InteropServices.FieldOffset(32)]
            public uint StateHash;
            [System.Runtime.InteropServices.FieldOffset(36)]
            public ushort Reserved1;
            [System.Runtime.InteropServices.FieldOffset(38)]
            public byte Flags;
            [System.Runtime.InteropServices.FieldOffset(39)]
            public byte Reserved0;
            [System.Runtime.InteropServices.FieldOffset(40)]
            private byte _pad0;
            [System.Runtime.InteropServices.FieldOffset(41)]
            private byte _pad1;
            [System.Runtime.InteropServices.FieldOffset(42)]
            private byte _pad2;
            [System.Runtime.InteropServices.FieldOffset(43)]
            private byte _pad3;
            [System.Runtime.InteropServices.FieldOffset(44)]
            private byte _pad4;
            [System.Runtime.InteropServices.FieldOffset(45)]
            private byte _pad5;
            [System.Runtime.InteropServices.FieldOffset(46)]
            private byte _pad6;
            [System.Runtime.InteropServices.FieldOffset(47)]
            private byte _pad7;
            [System.Runtime.InteropServices.FieldOffset(48)]
            private byte _pad8;
            [System.Runtime.InteropServices.FieldOffset(49)]
            private byte _pad9;
            [System.Runtime.InteropServices.FieldOffset(50)]
            private byte _pad10;
            [System.Runtime.InteropServices.FieldOffset(51)]
            private byte _pad11;
            [System.Runtime.InteropServices.FieldOffset(52)]
            private byte _pad12;
            [System.Runtime.InteropServices.FieldOffset(53)]
            private byte _pad13;
            [System.Runtime.InteropServices.FieldOffset(54)]
            private byte _pad14;
            [System.Runtime.InteropServices.FieldOffset(55)]
            private byte _pad15;
            [System.Runtime.InteropServices.FieldOffset(56)]
            private byte _pad16;
            [System.Runtime.InteropServices.FieldOffset(57)]
            private byte _pad17;
            [System.Runtime.InteropServices.FieldOffset(58)]
            private byte _pad18;
            [System.Runtime.InteropServices.FieldOffset(59)]
            private byte _pad19;
            [System.Runtime.InteropServices.FieldOffset(60)]
            private byte _pad20;
            [System.Runtime.InteropServices.FieldOffset(61)]
            private byte _pad21;
            [System.Runtime.InteropServices.FieldOffset(62)]
            private byte _pad22;
            [System.Runtime.InteropServices.FieldOffset(63)]
            private byte _pad23;
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

        public bool IsInitialized => _isInitialized && IsTelemetryHandleOwned(in _telemetryHandle);
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

            // Player-build construction path: no authored/bootstrap instance reachable.
            // Visor flood waterline bridge owns InternalFloodWaterline shader globals; without
            // create the suit HUD flood presentation stays permanently inactive.
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
            Vector3 shiftOffset = shiftData.ShiftOffset;
            float shiftSqrMagnitude = shiftOffset.sqrMagnitude;
            if (!IsFiniteVector(shiftOffset) || !math.isfinite(shiftSqrMagnitude))
                return;

            float shiftY = shiftOffset.y;
            if (shiftSqrMagnitude <= 0.000001f || math.abs(shiftY) <= 0.000001f)
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
            ReleaseTelemetryHandle(_dataVault);
            _dataVault = null;
            ResetVaultEpochState();
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
            PublishInactiveGlobals();
        }

        private void EnsureNativeTelemetry()
        {
            IDataVault vault = CacheDataVaultCold();
            if (vault == null)
                return;

            if (IsTelemetryHandleOwned(in _telemetryHandle) &&
                vault.TryReadOnlyHandle(in _telemetryHandle, out NativeArray<WaterlineTelemetryEntry>.ReadOnly telemetry) &&
                telemetry.IsCreated &&
                telemetry.Length >= TelemetryCapacity)
            {
                return;
            }

            if (IsTelemetryHandleOwned(in _telemetryHandle))
                ReleaseTelemetryHandle(vault);

            _telemetryHandle = vault.EnsureGenerationHandle<WaterlineTelemetryEntry>(
                TelemetryBufferId,
                TelemetryCapacity,
                VaultOwnerSystemId,
                NativeArrayOptions.ClearMemory);
        }

        private IDataVault CacheDataVaultCold()
        {
            if (_dataVault == null)
                BindDataVaultForLifecycle(GlobalRegistry.DataVault);

            return _dataVault;
        }

        private static bool IsTelemetryHandleOwned<T>(in VaultGenerationHandle<T> handle) where T : unmanaged
        {
            return handle.BufferID == unchecked((uint)(int)TelemetryBufferId) &&
                   handle.SystemID == (uint)VaultOwnerSystemId &&
                   handle.Generation != 0u;
        }

        private void BindDataVaultForLifecycle(IDataVault nextVault)
        {
            if (ReferenceEquals(_dataVault, nextVault))
                return;

            ReleaseTelemetryHandle(_dataVault);
            _dataVault = nextVault;
            ResetVaultEpochState();
        }

        private void ReleaseTelemetryHandle(IDataVault vault)
        {
            if (vault != null && IsTelemetryHandleOwned(in _telemetryHandle))
                vault.ReleaseBuffer(in _telemetryHandle);

            _telemetryHandle = default;
        }

        private void ResetVaultEpochState()
        {
            _telemetryCursor = 0;
            _blackBoxDumped = false;
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
                    UnregisterRuntime();
                    if (currentService != null && isActiveAndEnabled && _isInitialized)
                        RegisterRuntime();
                    return;

                case GlobalRegistryServiceSlot.DataVault:
                    IDataVault nextVault = currentService is IDataVault dataVault ? dataVault : null;
                    RebindDataVault(nextVault);
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

            BindDataVaultForLifecycle(nextVault);
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
                !IsTelemetryHandleOwned(in _telemetryHandle))
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
            WaterlineTelemetryEntry entry = default;
            entry.Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            entry.Sequence = snapshot.Sequence;
            entry.RoomId = snapshot.RoomId;
            entry.Fill01 = snapshot.Fill01;
            entry.CurrentWaterlineY = _currentWaterlineY;
            entry.TargetWaterlineY = _targetWaterlineY;
            entry.CameraY = cameraY;
            entry.Droplets01 = droplets01;
            entry.Flags = telemetryFlags;
            entry.Reserved0 = qualityByte;
            entry.Reserved1 = 0;
            entry.StateHash = ResolveTelemetryHash(snapshot.RoomId, snapshot.Fill01, _currentWaterlineY, cameraY, droplets01, qualityByte);

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

        private unsafe void DumpBlackBoxOnce()
        {
            IDataVault vault = _dataVault;
            if (_blackBoxDumped ||
                vault == null ||
                !IsTelemetryHandleOwned(in _telemetryHandle))
            {
                return;
            }

            NativeArray<byte> payload = default;
            try
            {
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "AgentLogs", DumpFileName));
                int totalBytes = 24 + TelemetryCapacity * TelemetryEntrySizeBytes;
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    totalBytes,
                    nameof(InternalFloodWaterlineRuntime),
                    DumpPayloadLabel,
                    NativeArrayOptions.UninitializedMemory);
                byte* payloadPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);

                Span<byte> header = new Span<byte>(payloadPtr, 24);
                WriteTelemetryDumpHeader(header, _telemetryCursor, _tickCount);

                int offset = 24;
                for (int i = 0; i < TelemetryCapacity; i++)
                {
                    if (!TryReadTelemetryEntry(vault, i, out WaterlineTelemetryEntry entry))
                        entry = default;

                    Span<byte> row = new Span<byte>(payloadPtr + offset, TelemetryEntrySizeBytes);
                    WriteTelemetryEntry(row, in entry);
                    offset += TelemetryEntrySizeBytes;
                }

                _blackBoxDumped = NativeFaultDumpWriter.TryWriteAll(path, payload, totalBytes);
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
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(InternalFloodWaterlineRuntime),
                    DumpPayloadLabel);
            }
        }

        private bool TryReadTelemetryEntry(IDataVault vault, int index, out WaterlineTelemetryEntry entry)
        {
            entry = default;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                index < 0 ||
                index >= TelemetryCapacity ||
                !IsTelemetryHandleOwned(in _telemetryHandle) ||
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
            destination.Clear();
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(0, 4), entry.Frame);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(4, 4), entry.Sequence);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(8, 4), unchecked((uint)entry.RoomId));
            WriteFloatLittleEndian(destination.Slice(12, 4), entry.Fill01);
            WriteFloatLittleEndian(destination.Slice(16, 4), entry.CurrentWaterlineY);
            WriteFloatLittleEndian(destination.Slice(20, 4), entry.TargetWaterlineY);
            WriteFloatLittleEndian(destination.Slice(24, 4), entry.CameraY);
            WriteFloatLittleEndian(destination.Slice(28, 4), entry.Droplets01);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(32, 4), entry.StateHash);
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(36, 2), entry.Reserved1);
            destination[38] = entry.Flags;
            destination[39] = entry.Reserved0;
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
