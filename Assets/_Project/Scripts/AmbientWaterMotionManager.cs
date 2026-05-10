// ============================================================================
// HECTON-8 — AmbientWaterMotionManager.cs
// Centralized visual bob/sway updater. One tick for many decorative props.
//
// v1.1 OPTIMIZATIONS:
//   [FIX] TryResolveObserver: dobavlen _observerResolveCooldown — ne dergaem
//         GameBootstrapper/player resolve kazhdyy kadr esli observer esche ne gotov.
//   [FIX] Register: zamena Contains (O(n)) na HashSet dlya O(1) deduplikatsii.
//   [FIX] ApplyMotion: keshiruem worldPos iz CachedTransform.position odin raz,
//         peredaem v ShouldUpdate chtoby ne chitat position dvazhdy cherez bridge.
//   [FIX] ShouldUpdate: prinimaet worldPos kak parametr, ubran povtornyy .position.
// ============================================================================

using System.Collections.Generic;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Environment;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Physics
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4900)]
    [AddComponentMenu("Hecton/Physics/Ambient Water Motion Manager")]
    public sealed class AmbientWaterMotionManager : MonoBehaviour, ITickable, IUpdatable, IBiomeMatrixEventListener
    {
        private const float BiomeFlowBlendSeconds = 5f;
        private const int MotionCapacity = 128;

        [Header("Observer / LOD")]
        [SerializeField] private Transform lodObserver;
        [SerializeField] private float nearDistance    = 20f;
        [SerializeField] private float mediumDistance  = 45f;
        [SerializeField] private float farDistance     = 90f;
        [SerializeField] private float cullDistance    = 150f;
        [SerializeField, Range(1, 8)]  private int mediumDivisor = 2;
        [SerializeField, Range(1, 16)] private int farDivisor    = 4;
        [SerializeField, Range(1, 32)] private int cullDivisor   = 8;

        [Header("Global")]
        [SerializeField] private float globalAmplitude = 1f;
        [SerializeField] private float globalFrequency = 1f;

        [Header("Diagnostics")]
        [SerializeField] private int _debugActiveObjects;
        [SerializeField] private int _debugNearCount;
        [SerializeField] private int _debugMediumCount;
        [SerializeField] private int _debugFarCount;
        [SerializeField] private int _debugCulledCount;
        [SerializeField] private int _debugBiomeCurrentBiomeId = -1;
        [SerializeField] private Vector3 _debugBiomeCurrentVector;

        // ── Registered objects ───────────────────────────────────────────────
        // List dlya iteratsii (cache-friendly), HashSet dlya O(1) deduplikatsii v Register.
        private readonly List<AmbientWaterMotion> _objects =
            new List<AmbientWaterMotion>(MotionCapacity); // COLD ALLOC: List<AmbientWaterMotion>[128] � active ambient-water motion registry � owner: AmbientWaterMotionManager
        private readonly HashSet<AmbientWaterMotion> _objectsSet =
            new HashSet<AmbientWaterMotion>(MotionCapacity); // COLD ALLOC: HashSet<AmbientWaterMotion>[128] � duplicate guard for ambient-water motion registry � owner: AmbientWaterMotionManager

        private float _time;
        private int   _frameCounter;
        private float _nearDistanceSqr;
        private float _mediumDistanceSqr;
        private float _farDistanceSqr;
        private float _cullDistanceSqr;
        private bool _tickRegistered;
        private bool _serviceRegistered;
        private Vector3 _biomeCurrentVector;
        private Vector3 _biomeCurrentStartVector;
        private Vector3 _biomeCurrentTargetVector;
        private float _biomeCurrentBlendElapsed;
        private bool _hasBiomeCurrentTarget;

        // ── Observer resolve cooldown ────────────────────────────────────────
        // Esli observer ne naznachen i ne nayden — ne dergaem bootstrap kazhdyy kadr.
        private float _observerResolveTimer;
        private const float ObserverResolveCooldown = 2f;

        public static AmbientWaterMotionManager Instance => GlobalRegistry.AmbientWaterMotion;

        // ════════════════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ════════════════════════════════════════════════════════════════════

        private void Awake()
        {
            AmbientWaterMotionManager registered = GlobalRegistry.AmbientWaterMotion;
            if (registered != null && registered != this)
            {
                Destroy(gameObject);
                return;
            }

            RefreshDistanceThresholds();
            // Probuem srazu pri starte
            TryResolveObserver(force: true);
        }

        private void OnEnable()
        {
            TryRegister();
            TryRegisterService();
            if (Application.isPlaying)
                BiomeMatrixEvents.Register(this);
        }

        private void OnDisable()
        {
            BiomeMatrixEvents.Unregister(this);
            TryUnregister();
            TryUnregisterService();
        }

        private void OnDestroy()
        {
            BiomeMatrixEvents.Unregister(this);
            TryUnregister();
            TryUnregisterService();

        }

        // ════════════════════════════════════════════════════════════════════
        //  REGISTRATION — O(1) deduplikatsiya cherez HashSet
        // ════════════════════════════════════════════════════════════════════

        public void Register(AmbientWaterMotion motion)
        {
            if (motion == null) return;

            // HashSet.Add vozvraschaet false esli uzhe est — O(1) vs O(n) Contains
            if (_objectsSet.Add(motion))
                _objects.Add(motion);

            _debugActiveObjects = _objects.Count;
        }

        public void Unregister(AmbientWaterMotion motion)
        {
            if (motion == null) return;

            if (_objectsSet.Remove(motion))
                _objects.Remove(motion);

            _debugActiveObjects = _objects.Count;
        }

        // ════════════════════════════════════════════════════════════════════
        //  TICK
        // ════════════════════════════════════════════════════════════════════

        public void Tick(float deltaTime)
        {
            if (HectonFloatingOrigin.IsShiftInProgress)
                return;
            UpdateBiomeCurrentBlend(deltaTime);

            if (_objects.Count == 0) return;

            _frameCounter++;
            _time += deltaTime;
            if (_time > 100000f) _time -= 100000f;

            // Cooldown na poisk observer — ne kazhdyy kadr
            _observerResolveTimer -= deltaTime;
            if (_observerResolveTimer <= 0f)
            {
                TryResolveObserver(force: false);
            }

            _debugNearCount   = 0;
            _debugMediumCount = 0;
            _debugFarCount    = 0;
            _debugCulledCount = 0;

            // Keshiruem pozitsiyu nablyudatelya odin raz za tik
            // Izbegaem povtornyh bridge calls v ShouldUpdate dlya kazhdogo obekta
            AbsoluteUniversePosition observerAup = lodObserver != null
                ? AbsoluteUniversePosition.FromRuntimePosition(lodObserver.position)
                : default;

            // Kvadraty distantsiy — schitaem odin raz za tik
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                AmbientWaterMotion motion = _objects[i];

                // Null-check: obekt mog byt unichtozhen bez OnDisable
                if (motion == null || motion.CachedTransform == null)
                {
                    // Swap-and-pop: O(1) udalenie iz serediny spiska
                    _objectsSet.Remove(motion);
                    int last = _objects.Count - 1;
                    _objects[i] = _objects[last];
                    _objects.RemoveAt(last);
                    continue;
                }

                // Chitaem position ODIN RAZ — keshiruem dlya ShouldUpdate i ApplyMotion
                // Bylo: position chitalsya dvazhdy (v ShouldUpdate i v ApplyMotion)
                AbsoluteUniversePosition motionAup = motion.RestAup;
                float3 runtimeRestPosition = motionAup.ToRuntimeFloat3();
                Vector3 worldPos = new Vector3(runtimeRestPosition.x, runtimeRestPosition.y, runtimeRestPosition.z);

                if (!ShouldUpdateAup(motion, i, motionAup, observerAup,
                                  _nearDistanceSqr, _mediumDistanceSqr, _farDistanceSqr, _cullDistanceSqr))
                    continue;

                ApplyMotion(motion, worldPos);
            }

            _debugActiveObjects = _objects.Count;
        }

        // ════════════════════════════════════════════════════════════════════
        //  SHOULD UPDATE — prinimaet predvychislennye dannye, net bridge calls
        // ════════════════════════════════════════════════════════════════════

        private bool ShouldUpdateAup(
            AmbientWaterMotion motion,
            int index,
            in AbsoluteUniversePosition motionAup,
            in AbsoluteUniversePosition observerAup,
            float nearSq,
            float mediumSq,
            float farSq,
            float cullSq)
        {
            if (!motion.AllowDistanceLod || lodObserver == null)
            {
                _debugNearCount++;
                return true;
            }

            float bias = math.max(0.1f, motion.LodBias);
            double biasSq = (double)bias * bias;
            double distanceSq = AbsoluteUniversePosition.DistanceSq(in motionAup, in observerAup);

            if (distanceSq <= (double)nearSq * biasSq)
            {
                _debugNearCount++;
                return true;
            }

            if (distanceSq <= (double)mediumSq * biasSq)
            {
                _debugMediumCount++;
                return ((_frameCounter + index) % math.max(1, mediumDivisor)) == 0;
            }

            if (distanceSq <= (double)farSq * biasSq)
            {
                _debugFarCount++;
                return ((_frameCounter + index) % math.max(1, farDivisor)) == 0;
            }

            _debugCulledCount++;
            return distanceSq <= (double)cullSq * biasSq
                && ((_frameCounter + index) % math.max(1, cullDivisor)) == 0;
        }

        private void ApplyMotion(AmbientWaterMotion motion, Vector3 worldPos)
        {
            Transform tr = motion.CachedTransform;

            float coupling = math.max(0f, motion.CurrentCoupling);
            Vector3 current = Vector3.zero;
            if (coupling > 0.0001f)
            {
                Vector3 volumeCurrent = CurrentVolume.SampleAt(worldPos);
                float3 phantomCurrent = CurrentManager.SampleHorizontal(
                    new float3(worldPos.x, worldPos.y, worldPos.z),
                    _time,
                    0.018f,
                    0.12f,
                    1f);

                current = (volumeCurrent
                    + new Vector3(phantomCurrent.x, phantomCurrent.y, phantomCurrent.z)
                    + _biomeCurrentVector) * coupling;
            }

            float currentSqrMagnitude = current.x * current.x + current.y * current.y + current.z * current.z;
            float currentMagnitude = ApproximateVectorMagnitude(current);
            Vector3 currentDir = currentSqrMagnitude > 0.0001f
                ? current * math.rsqrt(currentSqrMagnitude)
                : Vector3.forward;

            float t = (_time + motion.Phase)
                    * math.max(0f, motion.BaseFrequency * globalFrequency);

            float bobY = math.sin(t * 1.13f) * motion.VerticalAmplitude;
            float bobX = math.sin(t * 0.91f) * motion.PositionalAmplitude.x;
            float bobZ = math.cos(t * 1.07f) * motion.PositionalAmplitude.z;

            Vector3 offset = new Vector3(
                bobX + currentDir.x * currentMagnitude * 0.03f,
                bobY,
                bobZ + currentDir.z * currentMagnitude * 0.03f)
                * globalAmplitude;

            float pitch = math.sin(t * 0.87f) * motion.AngularAmplitude.x
                        + currentDir.z * currentMagnitude * 2f;
            float yaw   = math.sin(t * 0.43f) * motion.AngularAmplitude.y;
            float roll  = math.cos(t * 0.79f) * motion.AngularAmplitude.z
                        - currentDir.x * currentMagnitude * 3f;

            tr.localPosition = motion.RestLocalPosition + offset;
            tr.localRotation = motion.RestLocalRotation * Quaternion.Euler(pitch, yaw, roll);
        }

        private static float ApproximateVectorMagnitude(Vector3 value)
        {
            float ax = math.abs(value.x);
            float ay = math.abs(value.y);
            float az = math.abs(value.z);
            float max = math.max(ax, math.max(ay, az));
            float min = math.min(ax, math.min(ay, az));
            float mid = ax + ay + az - max - min;
            return max + (mid * 0.375f) + (min * 0.125f);
        }

        private void UpdateBiomeCurrentBlend(float deltaTime)
        {
            if (!_hasBiomeCurrentTarget)
                return;

            _biomeCurrentBlendElapsed += math.max(0f, deltaTime);
            float t = math.saturate(_biomeCurrentBlendElapsed / BiomeFlowBlendSeconds);
            float smooth = t * t * (3f - 2f * t);
            float3 biomeCurrent = math.lerp(
                new float3(_biomeCurrentStartVector.x, _biomeCurrentStartVector.y, _biomeCurrentStartVector.z),
                new float3(_biomeCurrentTargetVector.x, _biomeCurrentTargetVector.y, _biomeCurrentTargetVector.z),
                smooth);
            _biomeCurrentVector = new Vector3(biomeCurrent.x, biomeCurrent.y, biomeCurrent.z);
            if (t >= 1f)
            {
                _biomeCurrentVector = _biomeCurrentTargetVector;
                _hasBiomeCurrentTarget = false;
            }

            _debugBiomeCurrentVector = _biomeCurrentVector;
        }

        private void SetBiomeCurrentTarget(HectonBiomeMatrixProfile profile)
        {
            Vector3 target = Vector3.zero;
            if (profile != null && profile.hasAmbientFlowOverride)
                target = profile.ambientFlowOverride * math.saturate(profile.ambientFlowOverrideWeight);

            _debugBiomeCurrentBiomeId = profile != null ? profile.matrixIndex : -1;
            if ((target - _biomeCurrentTargetVector).sqrMagnitude <= 0.000001f)
                return;

            _biomeCurrentStartVector = _biomeCurrentVector;
            _biomeCurrentTargetVector = target;
            _biomeCurrentBlendElapsed = 0f;
            _hasBiomeCurrentTarget = true;
            _debugBiomeCurrentVector = _biomeCurrentVector;
        }

        void IBiomeMatrixEventListener.OnMatrixBiomeChanged(HectonBiomeMatrixProfile profile)
        {
            SetBiomeCurrentTarget(profile);
        }

        void IBiomeMatrixEventListener.OnDepthTierChanged(int depthTier, float depthMeters)
        {
        }

        // ════════════════════════════════════════════════════════════════════
        //  OBSERVER RESOLVE — s cooldown, ne kazhdyy kadr
        // ════════════════════════════════════════════════════════════════════

        /// <param name="force">true = ignorirovat cooldown (Awake, OnEnable).</param>
        private void TryResolveObserver(bool force = false)
        {
            // Esli uzhe est — ne ischem
            if (lodObserver != null) return;

            // Esli cooldown ne istek i ne forsim — propuskaem
            if (!force && _observerResolveTimer > 0f) return;

            // Sbrasyvaem taymer nezavisimo ot rezultata poiska
            // Ne nashli seychas — podozhdem esche ObserverResolveCooldown sekund
            _observerResolveTimer = ObserverResolveCooldown;

            if (GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform))
                lodObserver = playerTransform;
        }

        private void RefreshDistanceThresholds()
        {
            _nearDistanceSqr = nearDistance * nearDistance;
            _mediumDistanceSqr = mediumDistance * mediumDistance;
            _farDistanceSqr = farDistance * farDistance;
            _cullDistanceSqr = cullDistance * cullDistance;
        }

        private void TryRegister()
        {
            if (_tickRegistered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _tickRegistered = GlobalRegistry.Updatables.Contains(this);
        }

        private void TryUnregister()
        {
            if (!_tickRegistered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _tickRegistered = false;
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            AmbientWaterMotionManager registered = GlobalRegistry.AmbientWaterMotion;
            if (registered != null && registered != this)
            {
                Destroy(gameObject);
                return;
            }

            GlobalRegistry.RegisterAmbientWaterMotionRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.AmbientWaterMotion, this);
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterAmbientWaterMotionRuntime(this);
            _serviceRegistered = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (nearDistance   < 1f)              nearDistance   = 1f;
            if (mediumDistance < nearDistance)    mediumDistance = nearDistance;
            if (farDistance    < mediumDistance)  farDistance    = mediumDistance;
            if (cullDistance   < farDistance)     cullDistance   = farDistance;
            if (globalAmplitude < 0f)             globalAmplitude = 0f;
            if (globalFrequency < 0f)             globalFrequency = 0f;
            RefreshDistanceThresholds();
        }
#endif
    }
}
