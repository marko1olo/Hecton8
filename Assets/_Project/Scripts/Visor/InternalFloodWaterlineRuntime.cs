using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Atmosphere;
using Hecton8.Core;
using Hecton8.Core.Signals;
using Hecton8.Gameplay;
using Hecton8.World;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Visor
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-6920)]
    public sealed class InternalFloodWaterlineRuntime : MonoBehaviour, IFastTickable, IOriginShiftListener, IServiceHeartbeat, IServiceShutdown
    {
        private const int TelemetryCapacity = 300;
        private const int TelemetryEntrySizeBytes = 40;
        private const uint DumpMagic = 0x4946574Cu; // IFWL
        private const int DumpVersion = 1;
        private const float FloodVisibleThreshold01 = 0.001f;
        private const float CrossingEpsilonMeters = 0.03f;
        private const float TransitionSmoothingSeconds = 0.22f;
        private const float DropletDurationSeconds = 2f;
        private const float LowTierRefractionStrength = 0f;
        private const float HighTierRefractionStrength = 0.0018f;
        private const float InternalWaterlineInvalidY = -100000f;
        private const float ShaderFloatEpsilon = 0.0001f;
        private const int DependencyRefreshTickInterval = 30;
        private const uint WaterSplashSourceHash = 0x49535753u; // ISWS
        private const uint ScreenBubbleSpeciesHash = 0x53434242u; // SCBB
        private const byte ScreenBubbleDebrisKind = 12;
        private const string DumpFileName = "Dump_INTERNAL_FLOOD_RENDERER.bin";
        private static readonly int InternalWaterlineYId = Shader.PropertyToID("_InternalWaterlineY");
        private static readonly int InternalWaterColorId = Shader.PropertyToID("_InternalWaterColor");
        private static readonly int InternalWaterlineRuntimeId = Shader.PropertyToID("_InternalWaterlineRuntime");
        private static readonly int InternalWaterlineDistortionId = Shader.PropertyToID("_InternalWaterlineDistortion");

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct WaterlineTelemetryEntry
        {
            public uint Frame;
            public uint Sequence;
            public int RoomId;
            public float Fill01;
            public float CurrentWaterlineY;
            public float TargetWaterlineY;
            public float CameraY;
            public float Droplets01;
            public byte Flags;
            public byte Reserved0;
            public ushort Reserved1;
            public uint StateHash;
        }

        internal static InternalFloodWaterlineRuntime ActiveRuntimeInstance { get; private set; }

        [SerializeField] private Color internalWaterColor = new Color(0.08f, 0.42f, 0.50f, 0.42f);
        [SerializeField, Range(0f, 1f)] private float tintStrength = 0.58f;
        [SerializeField, Range(0.001f, 0.1f)] private float edgeSoftness = 0.018f;

        private NativeArray<WaterlineTelemetryEntry> _telemetry;
        private HectonPlayerMovement _subscribedMovement;
        private IHabitatGraphService _habitatGraph;
        private IGasDynamicsSolver _gasDynamics;
        private AbsoluteUniversePosition _lastCameraAup;
        private int _telemetryCursor;
        private int _cachedRoomId = -1;
        private int _currentRoomId = -1;
        private int _pendingGasRoomId = -1;
        private int _lastProcessedExternalDropletFrame = int.MinValue;
        private int _nextDependencyRefreshTick;
        private float _currentWaterlineY = InternalWaterlineInvalidY;
        private float _targetWaterlineY = InternalWaterlineInvalidY;
        private float _currentFill01;
        private float _pendingGasFill01;
        private float _dropletSecondsRemaining;
        private float _lastPublishedWaterlineY = float.PositiveInfinity;
        private Color _lastPublishedWaterColor = Color.clear;
        private Vector4 _lastPublishedRuntime = Vector4.positiveInfinity;
        private Vector4 _lastPublishedDistortion = Vector4.positiveInfinity;
        private HectonQualityTier _cachedQualityTier = HectonQualityTier.Unknown;
        private bool _hasPendingGasSubmergedFraction;
        private bool _hasWaterline;
        private bool _cameraSubmerged;
        private bool _hasPreviousSubmergedState;
        private bool _registeredFastTick;
        private bool _registeredOriginShift;
        private bool _isInitialized;
        private bool _blackBoxDumped;
        private bool _shaderGlobalsDirty = true;
        private int _tickCount;

        public bool IsInitialized => _isInitialized && _telemetry.IsCreated;
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

            InternalFloodWaterlineRuntime existing = FindAnyObjectByType<InternalFloodWaterlineRuntime>();
            if (existing != null)
                return existing;

            GameObject root = new GameObject("[InternalFloodWaterlineRuntime]"); // COLD ALLOC: GameObject[1] - bootstrap-owned internal flood visor bridge - owner: InternalFloodWaterlineRuntime
            return root.AddComponent<InternalFloodWaterlineRuntime>();
        }

        public void InitializeService()
        {
            EnsureNativeTelemetry();
            _shaderGlobalsDirty = true;
            RefreshCachedDependencies(force: true);
            _isInitialized = true;
            ActiveRuntimeInstance = this;
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
            if (_isInitialized)
                RegisterRuntime();
        }

        private void OnDisable()
        {
            UnregisterRuntime();
            UnsubscribeMovement();
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        public void FastTick(float deltaTime)
        {
            _tickCount++;
            if (!_isInitialized || deltaTime <= 0f)
                return;

            ConsumeExternalDropletSignals();
            RefreshCachedDependencies(force: false);
            FlushPendingGasSubmergedFraction();
            if (_dropletSecondsRemaining > 0f)
                _dropletSecondsRemaining = math.max(0f, _dropletSecondsRemaining - deltaTime);

            if (!TryResolveRuntimeContext(out PlayerRuntimeContext runtimeContext))
            {
                ClearWaterlineState();
                return;
            }

            SubscribeMovement(runtimeContext.PlayerMovement);

            IHabitatGraphService habitatGraph = _habitatGraph;
            if (habitatGraph == null ||
                !TryResolvePlayerRuntimePosition(in runtimeContext, out Vector3 playerRuntimePosition) ||
                !habitatGraph.TryResolveRoomWaterline(playerRuntimePosition, _cachedRoomId, out HabitatRoomWaterlineSnapshot snapshot) ||
                !snapshot.IsValid)
            {
                ClearWaterlineState();
                return;
            }

            _cachedRoomId = snapshot.RoomId;
            _currentRoomId = snapshot.RoomId;
            _currentFill01 = snapshot.Fill01;
            QueueGasSubmergedFraction(snapshot.RoomId, snapshot.Fill01);

            if (snapshot.Fill01 <= FloodVisibleThreshold01)
            {
                ClearWaterlineState();
                return;
            }

            Vector3 cameraRuntimePosition = ResolveCameraRuntimePosition(in runtimeContext);
            _lastCameraAup = AbsoluteUniversePosition.FromRuntimePosition(cameraRuntimePosition);
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
            PublishShaderGlobals(snapshot.Fill01);
            WriteTelemetry(in snapshot, cameraRuntimePosition.y, 0);
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

            PublishShaderGlobals(_hasWaterline ? _currentFill01 : 0f);
        }

        private void RegisterRuntime()
        {
            if (!_registeredFastTick)
                _registeredFastTick = GlobalRegistry.TryRegisterFastTickable(this, PriorityLayer.Environment);

            if (!_registeredOriginShift)
            {
                HectonFloatingOrigin.RegisterListener(this);
                _registeredOriginShift = true;
            }
        }

        private void UnregisterRuntime()
        {
            if (_registeredFastTick)
            {
                GlobalRegistry.UnregisterFastTickable(this, PriorityLayer.Environment);
                _registeredFastTick = false;
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
            UnsubscribeMovement();
            _isInitialized = false;
            _habitatGraph = null;
            _gasDynamics = null;
            _hasPendingGasSubmergedFraction = false;
            _pendingGasRoomId = -1;
            if (_telemetry.IsCreated)
                _telemetry.Dispose();
            _telemetry = default;
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
            PublishInactiveGlobals();
        }

        private void EnsureNativeTelemetry()
        {
            if (_telemetry.IsCreated)
                return;

            _telemetry = new NativeArray<WaterlineTelemetryEntry>(TelemetryCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<WaterlineTelemetryEntry>[300] - fixed internal flood blackbox ring - owner: InternalFloodWaterlineRuntime
        }

        private static bool TryResolveRuntimeContext(out PlayerRuntimeContext runtimeContext)
        {
            return PlayerRuntimeContextService.TryGetActiveRuntimeContext(out runtimeContext) &&
                   runtimeContext != null &&
                   runtimeContext.IsBound;
        }

        private static bool TryResolvePlayerRuntimePosition(in PlayerRuntimeContext runtimeContext, out Vector3 runtimePosition)
        {
            float3 predicted = runtimeContext.MovementState.PredictedAup.ToRuntimeFloat3();
            if (math.all(math.isfinite(predicted)))
            {
                runtimePosition = new Vector3(predicted.x, predicted.y, predicted.z);
                return true;
            }

            float3 current = runtimeContext.MovementState.WorldPosition;
            if (math.all(math.isfinite(current)))
            {
                runtimePosition = new Vector3(current.x, current.y, current.z);
                return true;
            }

            runtimePosition = default;
            return false;
        }

        private static Vector3 ResolveCameraRuntimePosition(in PlayerRuntimeContext runtimeContext)
        {
            float3 eyePosition = runtimeContext.LookState.EyePosition;
            if (math.all(math.isfinite(eyePosition)))
                return new Vector3(eyePosition.x, eyePosition.y, eyePosition.z);

            Camera playerCamera = runtimeContext.PlayerCamera;
            if (playerCamera != null)
                return playerCamera.transform.position;

            float3 fallback = runtimeContext.MovementState.WorldPosition;
            return new Vector3(fallback.x, fallback.y, fallback.z);
        }

        private void SubscribeMovement(HectonPlayerMovement movement)
        {
            if (ReferenceEquals(_subscribedMovement, movement))
                return;

            UnsubscribeMovement();
            _subscribedMovement = movement;
            if (_subscribedMovement != null)
                _subscribedMovement.OnExhale += HandlePlayerExhale;
        }

        private void UnsubscribeMovement()
        {
            if (_subscribedMovement == null)
                return;

            _subscribedMovement.OnExhale -= HandlePlayerExhale;
            _subscribedMovement = null;
        }

        private void HandlePlayerExhale()
        {
            if (!_cameraSubmerged || !_hasWaterline)
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
            GlobalSignals.Publish(in signal);
        }

        private void ConsumeExternalDropletSignals()
        {
            int frame = Time.frameCount;
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
                _dropletSecondsRemaining = math.max(_dropletSecondsRemaining, duration * intensity01);
                PublishShaderGlobals(_hasWaterline ? _currentFill01 : 0f);
            }
        }

        private void QueueGasSubmergedFraction(int roomId, float fill01)
        {
            if (roomId < 0)
                return;

            float safeFill01 = math.isfinite(fill01) ? math.saturate(fill01) : 0f;
            _pendingGasRoomId = roomId;
            _pendingGasFill01 = safeFill01;
            _hasPendingGasSubmergedFraction = !TryPushGasSubmergedFraction(roomId, safeFill01);
        }

        private void FlushPendingGasSubmergedFraction()
        {
            if (!_hasPendingGasSubmergedFraction)
                return;

            if (TryPushGasSubmergedFraction(_pendingGasRoomId, _pendingGasFill01))
            {
                _hasPendingGasSubmergedFraction = false;
                _pendingGasRoomId = -1;
            }
        }

        private bool TryPushGasSubmergedFraction(int roomId, float fill01)
        {
            IGasDynamicsSolver gasDynamics = _gasDynamics;
            return gasDynamics != null && gasDynamics.TrySetRoomSubmergedFraction(roomId, fill01);
        }

        private void RefreshCachedDependencies(bool force)
        {
            if (!force && _tickCount < _nextDependencyRefreshTick)
                return;

            _habitatGraph = GlobalRegistry.HabitatGraph;
            _gasDynamics = GlobalRegistry.GasDynamics;
            _cachedQualityTier = GlobalRegistry.ScalabilityTier;
            _nextDependencyRefreshTick = _tickCount + DependencyRefreshTickInterval;
        }

        private void PublishCrossingFeedback(Vector3 cameraRuntimePosition, bool wasSubmerged)
        {
            if (wasSubmerged && !_cameraSubmerged)
                _dropletSecondsRemaining = DropletDurationSeconds;

            AcousticPingSignal ping = new AcousticPingSignal
            {
                PositionAup = AbsoluteUniversePosition.FromRuntimePosition(cameraRuntimePosition),
                RadiusMeters = 7.5f,
                Intensity01 = 0.42f,
                SourceId = WaterSplashSourceHash,
                Channel = 0,
                Flags = 0
            };
            GlobalSignals.Publish(in ping);
        }

        private void PublishShaderGlobals(float fill01)
        {
            float active01 = _hasWaterline ? 1f : 0f;
            float droplets01 = math.saturate(_dropletSecondsRemaining * math.rcp(DropletDurationSeconds));
            bool lowTier = IsLowTier(_cachedQualityTier);
            float refraction = lowTier ? LowTierRefractionStrength : HighTierRefractionStrength;
            Vector4 runtime = new Vector4(active01, math.saturate(fill01), droplets01, _currentRoomId);
            Vector4 distortion = new Vector4(refraction, math.saturate(tintStrength), math.max(0.001f, edgeSoftness), lowTier ? 1f : 0f);

            SetGlobalFloatIfChanged(InternalWaterlineYId, _hasWaterline ? _currentWaterlineY : InternalWaterlineInvalidY, ref _lastPublishedWaterlineY);
            SetGlobalColorIfChanged(InternalWaterColorId, internalWaterColor, ref _lastPublishedWaterColor);
            SetGlobalVectorIfChanged(InternalWaterlineRuntimeId, runtime, ref _lastPublishedRuntime);
            SetGlobalVectorIfChanged(InternalWaterlineDistortionId, distortion, ref _lastPublishedDistortion);
            _shaderGlobalsDirty = false;
        }

        private void PublishInactiveGlobals()
        {
            SetGlobalFloatIfChanged(InternalWaterlineYId, InternalWaterlineInvalidY, ref _lastPublishedWaterlineY);
            SetGlobalColorIfChanged(InternalWaterColorId, internalWaterColor, ref _lastPublishedWaterColor);
            SetGlobalVectorIfChanged(InternalWaterlineRuntimeId, Vector4.zero, ref _lastPublishedRuntime);
            SetGlobalVectorIfChanged(InternalWaterlineDistortionId, new Vector4(0f, math.saturate(tintStrength), math.max(0.001f, edgeSoftness), 1f), ref _lastPublishedDistortion);
            _shaderGlobalsDirty = false;
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
            _currentWaterlineY = InternalWaterlineInvalidY;
            _targetWaterlineY = InternalWaterlineInvalidY;
            if (hasDroplets)
                PublishShaderGlobals(0f);
            else
                PublishInactiveGlobals();
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
            if (!_telemetry.IsCreated)
                return;

            float droplets01 = math.saturate(_dropletSecondsRemaining * math.rcp(DropletDurationSeconds));
            byte telemetryFlags = flags;
            if (_cameraSubmerged)
                telemetryFlags |= 2;
            if (_hasPendingGasSubmergedFraction)
                telemetryFlags |= 4;
            if (IsLowTier(_cachedQualityTier))
                telemetryFlags |= 8;
            WaterlineTelemetryEntry entry = new WaterlineTelemetryEntry
            {
                Frame = (uint)math.max(0, Time.frameCount),
                Sequence = snapshot.Sequence,
                RoomId = snapshot.RoomId,
                Fill01 = snapshot.Fill01,
                CurrentWaterlineY = _currentWaterlineY,
                TargetWaterlineY = _targetWaterlineY,
                CameraY = cameraY,
                Droplets01 = droplets01,
                Flags = telemetryFlags,
                Reserved0 = 0,
                Reserved1 = 0,
                StateHash = ResolveTelemetryHash(snapshot.RoomId, snapshot.Fill01, _currentWaterlineY, cameraY, droplets01)
            };

            _telemetry[_telemetryCursor] = entry;
            _telemetryCursor = (_telemetryCursor + 1) % TelemetryCapacity;
            if ((flags & 1) != 0)
                DumpBlackBoxOnce();
        }

        private void DumpBlackBoxOnce()
        {
            if (_blackBoxDumped || !_telemetry.IsCreated)
                return;

            _blackBoxDumped = true;
            try
            {
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "AgentLogs", DumpFileName));
                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(DumpMagic);
                    writer.Write(DumpVersion);
                    writer.Write(TelemetryEntrySizeBytes);
                    writer.Write(TelemetryCapacity);
                    writer.Write(_telemetryCursor);
                    writer.Write(_tickCount);
                    for (int i = 0; i < _telemetry.Length; i++)
                    {
                        WaterlineTelemetryEntry entry = _telemetry[i];
                        writer.Write(entry.Frame);
                        writer.Write(entry.Sequence);
                        writer.Write(entry.RoomId);
                        writer.Write(entry.Fill01);
                        writer.Write(entry.CurrentWaterlineY);
                        writer.Write(entry.TargetWaterlineY);
                        writer.Write(entry.CameraY);
                        writer.Write(entry.Droplets01);
                        writer.Write(entry.Flags);
                        writer.Write(entry.Reserved0);
                        writer.Write(entry.Reserved1);
                        writer.Write(entry.StateHash);
                    }
                }
            }
            catch (System.Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[InternalFloodWaterlineRuntime] Black box dump failed: " + exception.Message);
#endif
            }
        }

        private static bool IsLowTier(HectonQualityTier tier)
        {
            return tier == HectonQualityTier.Low ||
                   tier == HectonQualityTier.Mx350 ||
                   tier == HectonQualityTier.Unknown;
        }

        private static uint ResolveTelemetryHash(int roomId, float fill01, float waterlineY, float cameraY, float droplets01)
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)roomId) * 16777619u;
            hash = (hash ^ math.asuint(fill01)) * 16777619u;
            hash = (hash ^ math.asuint(waterlineY)) * 16777619u;
            hash = (hash ^ math.asuint(cameraY)) * 16777619u;
            hash = (hash ^ math.asuint(droplets01)) * 16777619u;
            return hash;
        }
    }
}
