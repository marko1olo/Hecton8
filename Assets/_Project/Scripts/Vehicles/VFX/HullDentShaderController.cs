using System;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using Hecton8.Core.Contracts;

namespace Hecton8.Vehicles.VFX
{
    /// <summary>
    /// Shader-only submarine hull dent presenter. Gameplay collision remains pristine.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton/Vehicles/VFX/Hull Dent Shader Controller")]
    public sealed class HullDentShaderController : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private static int s_x001HullDentShaderControllerSignalPushDropCount;
        private const int MaxHullDents = 16;
        private const int RadiusQuantizationStepsPerMeter = 16;
        private const float InvRadiusQuantizationStepsPerMeter = 1f / RadiusQuantizationStepsPerMeter;
        private const float InvDepthQuantizationSteps = 1f / 255f;
        private const float MinimumStoredDepthMeters = 0.001f;
        private const float RepairMatchPaddingMeters = 0.35f;
        private const float LocalTransformEpsilon = 0.000001f;
        private const uint HullDentTelemetryHash = 0x48444E54u; // HDNT

        private static readonly ProfilerMarker _lateFrameProfilerMarker = new ProfilerMarker("H8.VehicleVFX.HullDents.LateFrame");
        private static readonly int _HullDentsId = Shader.PropertyToID("_HectonHullDents");
        private static readonly int _HullDentParamsId = Shader.PropertyToID("_HectonHullDentParams");

        [Header("Authority")]
        [SerializeField] private Transform submarineRoot;
        [SerializeField] private MonoBehaviour breachReadModelSource;
        [SerializeField] private uint acceptedTargetHash;
        [SerializeField] private ushort acceptedTargetId;
        [SerializeField] private bool acceptUnfilteredSignals = true;
#pragma warning disable CS0414
        [SerializeField] private bool acceptLegacyLocalPoints = true;
#pragma warning restore CS0414
        [SerializeField, Min(1f)] private float maxLocalImpactDistanceMeters = 42f;

        [Header("Dent Shape")]
        [SerializeField, Range(0.25f, 8f)] private float baseDentRadiusMeters = 1.35f;
        [SerializeField, Range(0f, 0.08f)] private float radiusPerMagnitude = 0.012f;
        [SerializeField, Range(0.005f, 1f)] private float maxDentDepthMeters = 0.24f;
        [SerializeField, Range(0.0005f, 0.02f)] private float depthMetersPerMagnitude = 0.0035f;
        [SerializeField, Range(1f, 200f)] private float fullIntensityMagnitude = 80f;

        [Header("Repair")]
        [SerializeField, Range(0.01f, 1f)] private float repairFadeMetersPerSecond = 0.16f;

        // COLD ALLOC: Vector4[16] - fixed global shader dent buffer, xyz local point and w packed radius/depth - owner: HullDentShaderController
        private readonly Vector4[] _dentBuffer = new Vector4[MaxHullDents];

        private ISubmarineHullBreachReadModel _breachReadModel;
        private ITickDispatcher _tickDispatcher;
        private IDataVault _dataVault;
        private VaultGenerationHandle<float4> _hullDentsHandle;
        private Transform _cachedRoot;
        private int _writeHead;
        private int _activeDentCount;
        private int _lastProcessedFrame = int.MinValue;
        private byte _qualityWeightByte = byte.MaxValue;
        private float _qualityWeight01 = 1f;
        private bool _dirty;
        private bool _registeredLateFrame;
        private bool _registeredHotSwap;
        private bool _ownsHullDentsBuffer;

        private void OnEnable()
        {
            ResolveRoot();
            ResolveBreachReadModel();
            ResolveTickDispatcher();
            RefreshQualityPolicy();
            CacheDataVaultCold();
            EnsureHullDentsBuffer();
            SyncDentBufferFromVault();
            TryRegisterLateFrameTickable();
            TryRegisterHotSwapListener();
            UploadShaderGlobals();
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }

            ClearLocalDentBuffer();
            UploadShaderGlobalsFromLocal();
            ReleaseHullDentsBuffer();
            _tickDispatcher = null;
            _dataVault = null;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                _tickDispatcher = currentService as ITickDispatcher;
                _registeredLateFrame = false;
                if (isActiveAndEnabled)
                    TryRegisterLateFrameTickable();

                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            ReleaseVaultBuffer(previousService as IDataVault ?? _dataVault, ref _hullDentsHandle, ref _ownsHullDentsBuffer);
            _dataVault = currentService as IDataVault;
            ClearLocalDentBuffer();
            EnsureHullDentsBuffer();
            SyncDentBufferFromVault();
            UploadShaderGlobalsFromLocal();
        }

        public void LateFrameTick()
        {
            using (_lateFrameProfilerMarker.Auto())
            {
                ResolveRoot();
                if (_cachedRoot == null)
                    return;

                int acceptedSignals = ConsumeCombatDamageSignals();
                bool repairChanged = ApplyRepairCoupling(ResolveUnscaledDeltaTime());
                RefreshQualityPolicy();

                if (_dirty)
                    UploadShaderGlobals();

                if (acceptedSignals > 0 || repairChanged)
                    CrashTelemetryBuffer.ReportHullDentState(HullDentTelemetryHash, _activeDentCount, BuildTelemetryFlags());
            }
        }

        private void TryRegisterLateFrameTickable()
        {
            if (_registeredLateFrame)
                return;

            ResolveTickDispatcher();
            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
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

        private void ResolveRoot()
        {
            _cachedRoot = submarineRoot != null ? submarineRoot : transform;
        }

        private void ResolveBreachReadModel()
        {
            _breachReadModel = breachReadModelSource as ISubmarineHullBreachReadModel;
            if (_breachReadModel == null)
                _breachReadModel = GetComponent(typeof(ISubmarineHullBreachReadModel)) as ISubmarineHullBreachReadModel;
        }

        private void ResolveTickDispatcher()
        {
            _tickDispatcher = GlobalRegistry.TickDispatcher;
        }

        private void CacheDataVaultCold()
        {
            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;
        }

        private bool RefreshQualityPolicy()
        {
            float nextQualityWeight01 = ResolveCurrentQualityWeight(_qualityWeight01);
            byte nextQualityWeightByte = ResolveQualityWeightByte(nextQualityWeight01);
            if (math.abs(nextQualityWeight01 - _qualityWeight01) <= 0.001f &&
                nextQualityWeightByte == _qualityWeightByte)
            {
                _qualityWeight01 = nextQualityWeight01;
                return false;
            }

            _qualityWeight01 = nextQualityWeight01;
            _qualityWeightByte = nextQualityWeightByte;
            _dirty = true;
            return true;
        }

        private static float ResolveCurrentQualityWeight(float fallbackWeight01)
        {
            float qualityWeight01 = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(fallbackWeight01, qualityWeight01, math.isfinite(qualityWeight01)));
        }

        private static byte ResolveQualityWeightByte(float qualityWeight01)
        {
            return (byte)math.clamp((int)math.round(math.saturate(qualityWeight01) * 255f), 0, 255);
        }

        private static float ResolveDearLieScarProxyWeight(float qualityWeight01)
        {
            return math.saturate(1f - math.smoothstep(0.18f, 0.72f, math.saturate(qualityWeight01)));
        }

        private int ConsumeCombatDamageSignals()
        {
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_lastProcessedFrame == frame)
                return 0;

            _lastProcessedFrame = frame;
            ReadOnlySpan<CombatDamageSignal> signals = SignalBus<CombatDamageSignal>.GetFrameSnapshot();
            int accepted = 0;
            for (int i = 0; i < signals.Length; i++)
            {
                CombatDamageSignal signal = signals[i];
                if (!IsAcceptedSubmarineSignal(in signal))
                    continue;

                if (!TryResolveLocalImpact(in signal, out float3 localPoint))
                    continue;

                float radius = ResolveDentRadius(signal.Magnitude);
                float depth = ResolveDentDepth(signal.Magnitude);
                if (depth <= MinimumStoredDepthMeters)
                    continue;

                PushDent(localPoint, radius, depth);
                PublishHullDeformedSignal(in signal, localPoint, radius, depth);
                accepted++;
            }

            return accepted;
        }

        private bool IsAcceptedSubmarineSignal(in CombatDamageSignal signal)
        {
            if (acceptedTargetHash != 0u && signal.TargetHash == acceptedTargetHash)
                return true;

            if (acceptedTargetId != 0 && signal.TargetId == acceptedTargetId)
                return true;

            return acceptUnfilteredSignals;
        }

        private bool TryResolveLocalImpact(in CombatDamageSignal signal, out float3 localPoint)
        {
            localPoint = default;
            if (!CombatDamageSignalCodec.TryToRuntimePoint(in signal, out float3 runtimePoint))
                return false;

            Vector3 worldPoint = new Vector3(runtimePoint.x, runtimePoint.y, runtimePoint.z);
            if (!IsFiniteVector(worldPoint))
                return false;

            if (!TryResolveLocalImpactAup(worldPoint, out localPoint))
                return false;

            if (!math.all(math.isfinite(localPoint)))
                return false;

            float maxDistance = FiniteAtLeast(maxLocalImpactDistanceMeters, 1f);
            return math.lengthsq(localPoint) <= maxDistance * maxDistance;
        }

        private float ResolveDentRadius(float magnitude)
        {
            float safeMagnitude = FiniteNonNegativeOrZero(magnitude);
            float radius = FiniteNonNegativeOrZero(baseDentRadiusMeters) +
                           safeMagnitude * FiniteNonNegativeOrZero(radiusPerMagnitude);
            return math.clamp(radius, 0.25f, 15.9f);
        }

        private float ResolveDentDepth(float magnitude)
        {
            float safeMagnitude = FiniteNonNegativeOrZero(magnitude);
            float intensity01 = math.saturate(safeMagnitude * math.rcp(FiniteAtLeast(fullIntensityMagnitude, 1f)));
            float rawDepth = safeMagnitude * FiniteNonNegativeOrZero(depthMetersPerMagnitude) * math.max(0.25f, intensity01);
            return math.clamp(rawDepth, 0f, FiniteNonNegativeOrZero(maxDentDepthMeters));
        }

        private void PushDent(float3 localPoint, float radius, float depth)
        {
            SyncDentBufferFromVault();
            int existingIndex = FindMergeDentIndex(localPoint, radius);
            int writeIndex = existingIndex >= 0 ? existingIndex : _writeHead;
            float storedDepth = existingIndex >= 0 ? math.max(UnpackDepth(_dentBuffer[existingIndex].w), depth) : depth;

            _dentBuffer[writeIndex] = new Vector4(localPoint.x, localPoint.y, localPoint.z, PackRadiusDepth(radius, storedDepth));
            WriteDentToVault(writeIndex, _dentBuffer[writeIndex]);

            if (existingIndex < 0)
            {
                _writeHead = (_writeHead + 1) & (MaxHullDents - 1);
                _activeDentCount = math.min(MaxHullDents, _activeDentCount + 1);
            }

            _dirty = true;
        }

        private int FindMergeDentIndex(float3 localPoint, float radius)
        {
            float mergeRadius = math.max(0.15f, radius * 0.45f);
            float mergeRadiusSq = mergeRadius * mergeRadius;
            for (int i = 0; i < MaxHullDents; i++)
            {
                Vector4 dent = _dentBuffer[i];
                if (UnpackDepth(dent.w) <= MinimumStoredDepthMeters)
                    continue;

                float3 delta = new float3(dent.x, dent.y, dent.z) - localPoint;
                if (math.lengthsq(delta) <= mergeRadiusSq)
                    return i;
            }

            return -1;
        }

        private bool ApplyRepairCoupling(float deltaTime)
        {
            SyncDentBufferFromVault();
            if (_breachReadModel == null || !_breachReadModel.IsReady || _activeDentCount <= 0)
                return false;

            float safeDeltaTime = FiniteNonNegativeOrZero(deltaTime);
            float safeRepairFade = FiniteNonNegativeOrZero(repairFadeMetersPerSecond);
            float fadeDelta = safeDeltaTime * safeRepairFade;
            if (fadeDelta <= 0f)
                return false;

            int breachCount = _breachReadModel.ActiveBreachCount;
            bool changed = false;
            for (int dentIndex = 0; dentIndex < MaxHullDents; dentIndex++)
            {
                Vector4 dent = _dentBuffer[dentIndex];
                float depth = UnpackDepth(dent.w);
                if (depth <= MinimumStoredDepthMeters)
                    continue;

                float radius = UnpackRadius(dent.w);
                if (IsDentStillBackedByBreach(dent, radius + RepairMatchPaddingMeters, breachCount))
                    continue;

                float newDepth = math.max(0f, depth - fadeDelta);
                _dentBuffer[dentIndex].w = newDepth <= MinimumStoredDepthMeters ? 0f : PackRadiusDepth(radius, newDepth);
                changed = true;
            }

            if (changed)
            {
                _activeDentCount = CountActiveDents();
                FlushDentBufferToVault();
                _dirty = true;
            }

            return changed;
        }

        private float ResolveUnscaledDeltaTime()
        {
            ITickDispatcher dispatcher = _tickDispatcher;
            if (dispatcher != null)
            {
                double dispatcherDelta = dispatcher.TimeSnapshot.UnscaledDeltaTime;
                if (dispatcherDelta > 0d && double.IsFinite(dispatcherDelta))
                    return dispatcherDelta > 1d ? 1f : (float)dispatcherDelta;
            }

            float fallbackDelta = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            return math.isfinite(fallbackDelta) && fallbackDelta > 0f
                ? math.min(fallbackDelta, 1f)
                : 0f;
        }

        private bool IsDentStillBackedByBreach(Vector4 dent, float matchRadius, int breachCount)
        {
            if (breachCount <= 0)
                return false;

            float3 dentPoint = new float3(dent.x, dent.y, dent.z);
            float matchRadiusSq = matchRadius * matchRadius;
            int cappedCount = math.min(breachCount, 64);
            for (int i = 0; i < cappedCount; i++)
            {
                if (!_breachReadModel.TryGetActiveBreach(i, out Vector4 breach) || breach.w <= 0.0001f)
                    continue;

                float3 breachPoint = new float3(breach.x, breach.y, breach.z);
                float3 delta = breachPoint - dentPoint;
                if (math.lengthsq(delta) <= matchRadiusSq)
                    return true;
            }

            return false;
        }

        private int CountActiveDents()
        {
            int count = 0;
            for (int i = 0; i < MaxHullDents; i++)
            {
                if (UnpackDepth(_dentBuffer[i].w) > MinimumStoredDepthMeters)
                    count++;
            }

            return count;
        }

        private void UploadShaderGlobals()
        {
            SyncDentBufferFromVault();
            UploadShaderGlobalsFromLocal();
        }

        private void UploadShaderGlobalsFromLocal()
        {
            _activeDentCount = CountActiveDents();
            float scarScalar = ResolveHullScarScalar();
            float scarProxyWeight = ResolveDearLieScarProxyWeight(_qualityWeight01);
            Shader.SetGlobalVectorArray(_HullDentsId, _dentBuffer);
            Shader.SetGlobalVector(
                _HullDentParamsId,
                new Vector4(_activeDentCount, scarProxyWeight, scarScalar, _qualityWeightByte));
            _dirty = false;
        }

        private IDataVault ResolveDataVault()
        {
            CacheDataVaultCold();
            return _dataVault;
        }

        private bool EnsureHullDentsBuffer()
        {
            IDataVault vault = ResolveDataVault();
            return EnsureHullDentsHandle(vault);
        }

        private bool SyncDentBufferFromVault()
        {
            IDataVault vault = ResolveDataVault();
            if (vault == null)
                return false;

            if (!EnsureHullDentsHandle(vault) || !vault.TryLockBuffer(BufferID.HullDents, SystemID.Vfx))
                return false;

            try
            {
                if (!TryResolveHullDents(vault, out NativeArray<float4> dents, allowEnsure: false))
                    return false;

                bool changed = false;
                int count = math.min(MaxHullDents, dents.Length);
                for (int i = 0; i < count; i++)
                {
                    float4 dent = dents[i];
                    Vector4 next = math.all(math.isfinite(dent))
                        ? new Vector4(dent.x, dent.y, dent.z, SanitizePackedDentValue(dent.w))
                        : Vector4.zero;
                    if (_dentBuffer[i] != next)
                        changed = true;

                    _dentBuffer[i] = next;
                }

                for (int i = count; i < MaxHullDents; i++)
                {
                    if (_dentBuffer[i] != Vector4.zero)
                        changed = true;

                    _dentBuffer[i] = Vector4.zero;
                }

                RefreshDentWriteState();
                if (changed)
                    _dirty = true;

                return true;
            }
            finally
            {
                vault.TryUnlockBuffer(BufferID.HullDents, SystemID.Vfx);
            }
        }

        private bool FlushDentBufferToVault()
        {
            IDataVault vault = ResolveDataVault();
            if (vault == null)
                return false;

            if (!EnsureHullDentsHandle(vault) || !vault.TryLockBuffer(BufferID.HullDents, SystemID.Vfx))
                return false;

            try
            {
                if (!TryResolveHullDents(vault, out NativeArray<float4> dents, allowEnsure: false))
                    return false;

                int count = math.min(MaxHullDents, dents.Length);
                for (int i = 0; i < count; i++)
                {
                    Vector4 dent = _dentBuffer[i];
                    dents[i] = IsFiniteVector(dent)
                        ? new float4(dent.x, dent.y, dent.z, SanitizePackedDentValue(dent.w))
                        : float4.zero;
                }

                return true;
            }
            finally
            {
                vault.TryUnlockBuffer(BufferID.HullDents, SystemID.Vfx);
            }
        }

        private bool WriteDentToVault(int dentIndex, Vector4 dent)
        {
            if ((uint)dentIndex >= MaxHullDents || !IsFiniteVector(dent))
                return false;

            IDataVault vault = ResolveDataVault();
            if (vault == null)
                return false;

            if (!EnsureHullDentsHandle(vault) || !vault.TryLockBuffer(BufferID.HullDents, SystemID.Vfx))
                return false;

            try
            {
                if (!TryResolveHullDents(vault, out NativeArray<float4> dents, allowEnsure: false) ||
                    (uint)dentIndex >= (uint)dents.Length)
                {
                    return false;
                }

                dents[dentIndex] = new float4(dent.x, dent.y, dent.z, SanitizePackedDentValue(dent.w));
                return true;
            }
            finally
            {
                vault.TryUnlockBuffer(BufferID.HullDents, SystemID.Vfx);
            }
        }

        private bool EnsureHullDentsHandle(IDataVault vault)
        {
            if (vault == null)
                return false;

            if (vault.IsCompactionFenceActive)
            {
                return false;
            }

            if (IsVaultHandleCreated(in _hullDentsHandle) &&
                vault.TryResolveHandle(in _hullDentsHandle, out NativeArray<float4> currentDents) &&
                currentDents.IsCreated &&
                currentDents.Length >= MaxHullDents)
            {
                return true;
            }

            ClearHullDentsDescriptor();
            if (vault.TryGetGenerationHandle(BufferID.HullDents, out VaultGenerationHandle<float4> existing) &&
                vault.TryResolveHandle(in existing, out NativeArray<float4> existingDents) &&
                existingDents.IsCreated &&
                existingDents.Length >= MaxHullDents)
            {
                _hullDentsHandle = existing;
                _ownsHullDentsBuffer = false;
                return true;
            }

            if (vault.IsAllocationLocked)
                return false;

            VaultGenerationHandle<float4> acquired = vault.EnsureGenerationHandle<float4>(
                BufferID.HullDents,
                MaxHullDents,
                SystemID.Vfx,
                NativeArrayOptions.ClearMemory);
            if (!IsVaultHandleCreated(in acquired) ||
                !vault.TryResolveHandle(in acquired, out NativeArray<float4> acquiredDents) ||
                !acquiredDents.IsCreated ||
                acquiredDents.Length < MaxHullDents)
            {
                bool ownsAcquired = true;
                ReleaseVaultBuffer(vault, ref acquired, ref ownsAcquired);
                ClearHullDentsDescriptor();
                return false;
            }

            _hullDentsHandle = acquired;
            _ownsHullDentsBuffer = true;
            return true;
        }

        private bool TryResolveHullDents(IDataVault vault, out NativeArray<float4> dents, bool allowEnsure)
        {
            dents = default;
            if (vault == null)
                return false;

            if (allowEnsure && !EnsureHullDentsHandle(vault))
                return false;

            if (!IsVaultHandleCreated(in _hullDentsHandle))
                return false;

            if (!vault.TryResolveHandle(in _hullDentsHandle, out dents) ||
                !dents.IsCreated ||
                dents.Length < MaxHullDents)
            {
                if (allowEnsure)
                    ClearHullDentsDescriptor();

                return false;
            }

            return true;
        }

        private void ClearHullDentsDescriptor()
        {
            _hullDentsHandle = default;
            _ownsHullDentsBuffer = false;
        }

        private void ReleaseHullDentsBuffer()
        {
            ReleaseVaultBuffer(_dataVault, ref _hullDentsHandle, ref _ownsHullDentsBuffer);
        }

        private static bool IsVaultHandleCreated<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private static void ReleaseVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            ref bool ownsBuffer) where T : struct
        {
            if (ownsBuffer && vault != null && IsVaultHandleCreated(in handle))
                vault.ReleaseBuffer(in handle);

            handle = default;
            ownsBuffer = false;
        }

        private void RefreshDentWriteState()
        {
            _activeDentCount = CountActiveDents();
            if (_activeDentCount >= MaxHullDents)
            {
                _writeHead = 0;
                return;
            }

            for (int i = 0; i < MaxHullDents; i++)
            {
                if (UnpackDepth(_dentBuffer[i].w) <= MinimumStoredDepthMeters)
                {
                    _writeHead = i;
                    return;
                }
            }

            _writeHead = 0;
        }

        private float ResolveHullScarScalar()
        {
            float scar = 0f;
            for (int i = 0; i < MaxHullDents; i++)
                scar = math.max(scar, UnpackDepth(_dentBuffer[i].w));

            return math.saturate(scar * math.rcp(FiniteAtLeast(maxDentDepthMeters, 0.01f)));
        }

        private void ClearLocalDentBuffer()
        {
            for (int i = 0; i < MaxHullDents; i++)
                _dentBuffer[i] = Vector4.zero;

            _writeHead = 0;
            _activeDentCount = 0;
            _dirty = true;
        }

        private void PublishHullDeformedSignal(in CombatDamageSignal signal, float3 localPoint, float radius, float depth)
        {
            float intensity01 = math.saturate(depth * math.rcp(FiniteAtLeast(maxDentDepthMeters, 0.01f)));
            byte flags = 0;
            if ((signal.Flags & CombatDamageSignal.LegacyMirrorFlag) != 0)
                flags |= HullDeformedSignal.LegacyLocalPointFlag;

            HullDeformedSignal deformedSignal = new HullDeformedSignal
            {
                LocalPoint = localPoint,
                Radius = radius,
                Depth = depth,
                Intensity01 = intensity01,
                TargetHash = signal.TargetHash,
                SourceHash = signal.SourceHash,
                Frame = signal.Frame != 0u ? signal.Frame : Hecton8.Core.SystemDispatcher.CurrentFrameId,
                TargetId = signal.TargetId,
                SourceId = signal.SourceId,
                ActiveDentCount = (byte)math.min(MaxHullDents, _activeDentCount),
                Flags = flags,
                QualityTier = _qualityWeightByte,
                Channel = signal.Channel,
                DamageType = signal.DamageType
            };
            SignalBus<HullDeformedSignal>.TryPushTracked(in deformedSignal, ref s_x001HullDentShaderControllerSignalPushDropCount);
        }

        private uint BuildTelemetryFlags()
        {
            uint scarProxyByte = ResolveQualityWeightByte(ResolveDearLieScarProxyWeight(_qualityWeight01));
            return (uint)_qualityWeightByte | (scarProxyByte << 8);
        }

        private static float PackRadiusDepth(float radius, float depth)
        {
            float safeRadius = math.min(FiniteNonNegativeOrZero(radius), 15.9375f);
            float safeDepth = math.saturate(FiniteNonNegativeOrZero(depth));
            int radiusQ = Mathf.Clamp(Mathf.RoundToInt(safeRadius * RadiusQuantizationStepsPerMeter), 0, 255);
            int depthQ = Mathf.Clamp(Mathf.RoundToInt(safeDepth * 255f), 0, 255);
            return (depthQ << 8) | radiusQ;
        }

        private static float UnpackRadius(float packed)
        {
            int packedInt = Mathf.Max(0, Mathf.RoundToInt(SanitizePackedDentValue(packed)));
            return (packedInt & 255) * InvRadiusQuantizationStepsPerMeter;
        }

        private static float UnpackDepth(float packed)
        {
            int packedInt = Mathf.Max(0, Mathf.RoundToInt(SanitizePackedDentValue(packed)));
            return ((packedInt >> 8) & 255) * InvDepthQuantizationSteps;
        }

        private bool TryResolveLocalImpactAup(Vector3 worldPoint, out float3 localPoint)
        {
            localPoint = default;
            if (!IsFiniteVector(worldPoint))
                return false;

            Transform root = _cachedRoot != null ? _cachedRoot : submarineRoot != null ? submarineRoot : transform;
            _cachedRoot = root;
            if (root == null || !IsFiniteVector(root.position) || !IsFiniteQuaternion(root.rotation))
                return false;

            Vector3 relativeWorld = worldPoint - root.position;
            if (!IsFiniteVector(relativeWorld))
                return false;

            Quaternion inverseRotation = ConjugateUnitRotation(root.rotation);
            Vector3 local = inverseRotation * relativeWorld;
            Vector3 scale = root.lossyScale;
            local.x /= ResolveSafeScale(scale.x);
            local.y /= ResolveSafeScale(scale.y);
            local.z /= ResolveSafeScale(scale.z);
            if (!IsFiniteVector(local))
                return false;

            localPoint = new float3(local.x, local.y, local.z);
            return math.all(math.isfinite(localPoint));
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        private static Quaternion ConjugateUnitRotation(Quaternion rotation)
        {
            return new Quaternion(-rotation.x, -rotation.y, -rotation.z, rotation.w);
        }

        private static bool IsFiniteVector(Vector4 value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z) &&
                   float.IsFinite(value.w);
        }

        private static bool IsFiniteQuaternion(Quaternion value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z) &&
                   float.IsFinite(value.w);
        }

        private static float FiniteNonNegativeOrZero(float value)
        {
            return float.IsFinite(value) && value > 0f ? value : 0f;
        }

        private static float FiniteAtLeast(float value, float minimum)
        {
            return float.IsFinite(value) && value > minimum ? value : minimum;
        }

        private static float SanitizePackedDentValue(float value)
        {
            return float.IsFinite(value) && value > 0f ? value : 0f;
        }

        private static float ResolveSafeScale(float scale)
        {
            return float.IsFinite(scale) && math.abs(scale) > LocalTransformEpsilon ? scale : 1f;
        }
    }
}
