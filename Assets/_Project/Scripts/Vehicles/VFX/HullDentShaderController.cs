using System;
using Hecton8.Core;
using Hecton8.Core.Signals;
using Hecton8.Physics;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Hecton8.Vehicles.VFX
{
    /// <summary>
    /// Shader-only submarine hull dent presenter. Gameplay collision remains pristine.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton/Vehicles/VFX/Hull Dent Shader Controller")]
    public sealed class HullDentShaderController : MonoBehaviour, ILateFrameTickable
    {
        private const int MaxHullDents = 16;
        private const int RadiusQuantizationStepsPerMeter = 16;
        private const float InvRadiusQuantizationStepsPerMeter = 1f / RadiusQuantizationStepsPerMeter;
        private const float InvDepthQuantizationSteps = 1f / 255f;
        private const float MinimumStoredDepthMeters = 0.001f;
        private const float RepairMatchPaddingMeters = 0.35f;
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
        [SerializeField] private bool acceptLegacyLocalPoints = true;
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
        private Transform _cachedRoot;
        private int _writeHead;
        private int _activeDentCount;
        private int _lastProcessedFrame = int.MinValue;
        private int _qualityRefreshFrame = int.MinValue;
        private byte _qualityTier;
        private bool _lowTier;
        private bool _dirty;
        private bool _registeredLateFrame;

        private void OnEnable()
        {
            ResolveRoot();
            ResolveBreachReadModel();
            ResolveTickDispatcher();
            RefreshQualityTier(force: true);
            ClearDentBuffer();
            TryRegisterLateFrameTickable();
            UploadShaderGlobals();
        }

        private void OnDisable()
        {
            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }

            ClearDentBuffer();
            UploadShaderGlobals();
            _tickDispatcher = null;
        }

        public void LateFrameTick()
        {
            using (_lateFrameProfilerMarker.Auto())
            {
                if (!_registeredLateFrame)
                    TryRegisterLateFrameTickable();

                ResolveRoot();
                if (_cachedRoot == null)
                    return;

                RefreshQualityTier(force: false);
                int acceptedSignals = ConsumeCombatDamageSignals();
                bool repairChanged = ApplyRepairCoupling(ResolveUnscaledDeltaTime());

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

        private void RefreshQualityTier(bool force)
        {
            int frame = Time.frameCount;
            if (!force && frame - _qualityRefreshFrame < 60)
                return;

            _qualityRefreshFrame = frame;
            byte newQualityTier = GlobalRegistry.ScalabilityTierProfileByte;
            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            bool newLowTier = tier == HectonQualityTier.Unknown ||
                              tier == HectonQualityTier.Low ||
                              tier == HectonQualityTier.Mx350;

            if (!force && newQualityTier == _qualityTier && newLowTier == _lowTier)
                return;

            _qualityTier = newQualityTier;
            _lowTier = newLowTier;
            _dirty = true;
        }

        private int ConsumeCombatDamageSignals()
        {
            int frame = Time.frameCount;
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
            bool legacyLocal = acceptLegacyLocalPoints && (signal.Flags & CombatDamageSignal.LegacyMirrorFlag) != 0;
            if (legacyLocal)
            {
                localPoint = signal.WorldPoint;
            }
            else
            {
                Vector3 worldPoint = new Vector3(signal.WorldPoint.x, signal.WorldPoint.y, signal.WorldPoint.z);
                if (!IsFiniteVector(worldPoint))
                    return false;

                Vector3 local = _cachedRoot.InverseTransformPoint(worldPoint);
                localPoint = new float3(local.x, local.y, local.z);
            }

            if (!math.all(math.isfinite(localPoint)))
                return false;

            float maxDistance = math.max(1f, maxLocalImpactDistanceMeters);
            return math.lengthsq(localPoint) <= maxDistance * maxDistance;
        }

        private float ResolveDentRadius(float magnitude)
        {
            float radius = baseDentRadiusMeters + math.max(0f, magnitude) * radiusPerMagnitude;
            return math.clamp(radius, 0.25f, 15.9f);
        }

        private float ResolveDentDepth(float magnitude)
        {
            float intensity01 = math.saturate(math.max(0f, magnitude) * math.rcp(math.max(1f, fullIntensityMagnitude)));
            float rawDepth = math.max(0f, magnitude) * depthMetersPerMagnitude * math.max(0.25f, intensity01);
            return math.clamp(rawDepth, 0f, maxDentDepthMeters);
        }

        private void PushDent(float3 localPoint, float radius, float depth)
        {
            int existingIndex = FindMergeDentIndex(localPoint, radius);
            int writeIndex = existingIndex >= 0 ? existingIndex : _writeHead;
            float storedDepth = existingIndex >= 0 ? math.max(UnpackDepth(_dentBuffer[existingIndex].w), depth) : depth;

            _dentBuffer[writeIndex] = new Vector4(localPoint.x, localPoint.y, localPoint.z, PackRadiusDepth(radius, storedDepth));

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
            if (_breachReadModel == null || !_breachReadModel.IsReady || _activeDentCount <= 0)
                return false;

            float fadeDelta = math.max(0f, deltaTime) * math.max(0f, repairFadeMetersPerSecond);
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

            float fallbackDelta = Time.unscaledDeltaTime;
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
            _activeDentCount = CountActiveDents();
            float scarScalar = ResolveLowTierScarScalar();
            Shader.SetGlobalVectorArray(_HullDentsId, _dentBuffer);
            Shader.SetGlobalVector(
                _HullDentParamsId,
                new Vector4(_activeDentCount, _lowTier ? 1f : 0f, scarScalar, _qualityTier));
            _dirty = false;
        }

        private float ResolveLowTierScarScalar()
        {
            float scar = 0f;
            for (int i = 0; i < MaxHullDents; i++)
                scar = math.max(scar, UnpackDepth(_dentBuffer[i].w));

            return math.saturate(scar * math.rcp(math.max(0.01f, maxDentDepthMeters)));
        }

        private void ClearDentBuffer()
        {
            for (int i = 0; i < MaxHullDents; i++)
                _dentBuffer[i] = Vector4.zero;

            _writeHead = 0;
            _activeDentCount = 0;
            _dirty = true;
        }

        private void PublishHullDeformedSignal(in CombatDamageSignal signal, float3 localPoint, float radius, float depth)
        {
            float intensity01 = math.saturate(depth * math.rcp(math.max(0.01f, maxDentDepthMeters)));
            byte flags = 0;
            if (_lowTier)
                flags |= HullDeformedSignal.LowTierVisualOnlyFlag;
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
                Frame = signal.Frame != 0u ? signal.Frame : unchecked((uint)Time.frameCount),
                TargetId = signal.TargetId,
                SourceId = signal.SourceId,
                ActiveDentCount = (byte)math.min(MaxHullDents, _activeDentCount),
                Flags = flags,
                QualityTier = _qualityTier,
                Channel = signal.Channel,
                DamageType = signal.DamageType
            };
            GlobalSignals.Publish(in deformedSignal);
        }

        private uint BuildTelemetryFlags()
        {
            uint flags = _lowTier ? 1u : 0u;
            return flags | ((uint)_qualityTier << 8);
        }

        private static float PackRadiusDepth(float radius, float depth)
        {
            int radiusQ = Mathf.Clamp(Mathf.RoundToInt(math.clamp(radius, 0f, 15.9375f) * RadiusQuantizationStepsPerMeter), 0, 255);
            int depthQ = Mathf.Clamp(Mathf.RoundToInt(math.saturate(depth) * 255f), 0, 255);
            return (depthQ << 8) | radiusQ;
        }

        private static float UnpackRadius(float packed)
        {
            int packedInt = Mathf.Max(0, Mathf.RoundToInt(packed));
            return (packedInt & 255) * InvRadiusQuantizationStepsPerMeter;
        }

        private static float UnpackDepth(float packed)
        {
            int packedInt = Mathf.Max(0, Mathf.RoundToInt(packed));
            return ((packedInt >> 8) & 255) * InvDepthQuantizationSteps;
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }
    }
}
