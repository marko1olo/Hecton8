// ============================================================================
// HECTON-8 â€” AmbientWaterMotionManager.cs
// Centralized visual bob/sway updater. One tick for many decorative props.
//
// v1.1 OPTIMIZATIONS:
//   [FIX] TryResolveObserver: Ð´Ð¾Ð±Ð°Ð²Ð»ÐµÐ½ _observerResolveCooldown â€” Ð½Ðµ Ð´Ñ‘Ñ€Ð³Ð°ÐµÐ¼
//         SceneBootstrap/player resolve ÐºÐ°Ð¶Ð´Ñ‹Ð¹ ÐºÐ°Ð´Ñ€ ÐµÑÐ»Ð¸ observer ÐµÑ‰Ñ‘ Ð½Ðµ Ð³Ð¾Ñ‚Ð¾Ð².
//   [FIX] Register: Ð·Ð°Ð¼ÐµÐ½Ð° Contains (O(n)) Ð½Ð° HashSet Ð´Ð»Ñ O(1) Ð´ÐµÐ´ÑƒÐ¿Ð»Ð¸ÐºÐ°Ñ†Ð¸Ð¸.
//   [FIX] ApplyMotion: ÐºÑÑˆÐ¸Ñ€ÑƒÐµÐ¼ worldPos Ð¸Ð· CachedTransform.position Ð¾Ð´Ð¸Ð½ Ñ€Ð°Ð·,
//         Ð¿ÐµÑ€ÐµÐ´Ð°Ñ‘Ð¼ Ð² ShouldUpdate Ñ‡Ñ‚Ð¾Ð±Ñ‹ Ð½Ðµ Ñ‡Ð¸Ñ‚Ð°Ñ‚ÑŒ position Ð´Ð²Ð°Ð¶Ð´Ñ‹ Ñ‡ÐµÑ€ÐµÐ· bridge.
//   [FIX] ShouldUpdate: Ð¿Ñ€Ð¸Ð½Ð¸Ð¼Ð°ÐµÑ‚ worldPos ÐºÐ°Ðº Ð¿Ð°Ñ€Ð°Ð¼ÐµÑ‚Ñ€, ÑƒÐ±Ñ€Ð°Ð½ Ð¿Ð¾Ð²Ñ‚Ð¾Ñ€Ð½Ñ‹Ð¹ .position.
// ============================================================================

using System.Collections.Generic;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Physics
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4900)]
    [AddComponentMenu("Hecton/Physics/Ambient Water Motion Manager")]
    public sealed class AmbientWaterMotionManager : MonoBehaviour, ITickable, IUpdatable
    {
        private static AmbientWaterMotionManager _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

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

        // â”€â”€ Registered objects â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // List Ð´Ð»Ñ Ð¸Ñ‚ÐµÑ€Ð°Ñ†Ð¸Ð¸ (cache-friendly), HashSet Ð´Ð»Ñ O(1) Ð´ÐµÐ´ÑƒÐ¿Ð»Ð¸ÐºÐ°Ñ†Ð¸Ð¸ Ð² Register.
        private readonly List<AmbientWaterMotion>     _objects    = new List<AmbientWaterMotion>(128);
        private readonly HashSet<AmbientWaterMotion>  _objectsSet = new HashSet<AmbientWaterMotion>();

        private float _time;
        private int   _frameCounter;
        private float _nearDistanceSqr;
        private float _mediumDistanceSqr;
        private float _farDistanceSqr;
        private float _cullDistanceSqr;
        private bool _tickRegistered;
        private bool _serviceRegistered;

        // â”€â”€ Observer resolve cooldown â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Ð•ÑÐ»Ð¸ observer Ð½Ðµ Ð½Ð°Ð·Ð½Ð°Ñ‡ÐµÐ½ Ð¸ Ð½Ðµ Ð½Ð°Ð¹Ð´ÐµÐ½ â€” Ð½Ðµ Ð´Ñ‘Ñ€Ð³Ð°ÐµÐ¼ bootstrap ÐºÐ°Ð¶Ð´Ñ‹Ð¹ ÐºÐ°Ð´Ñ€.
        private float _observerResolveTimer;
        private const float ObserverResolveCooldown = 2f;

        public static AmbientWaterMotionManager Instance => _instance;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  LIFECYCLE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            RefreshDistanceThresholds();
            // ÐŸÑ€Ð¾Ð±ÑƒÐµÐ¼ ÑÑ€Ð°Ð·Ñƒ Ð¿Ñ€Ð¸ ÑÑ‚Ð°Ñ€Ñ‚Ðµ
            TryResolveObserver(force: true);
        }

        private void OnEnable()
        {
            TryRegister();
            TryRegisterService();
        }

        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterService();
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterService();

            if (_instance == this)
                _instance = null;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  REGISTRATION â€” O(1) Ð´ÐµÐ´ÑƒÐ¿Ð»Ð¸ÐºÐ°Ñ†Ð¸Ñ Ñ‡ÐµÑ€ÐµÐ· HashSet
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        public void Register(AmbientWaterMotion motion)
        {
            if (motion == null) return;

            // HashSet.Add Ð²Ð¾Ð·Ð²Ñ€Ð°Ñ‰Ð°ÐµÑ‚ false ÐµÑÐ»Ð¸ ÑƒÐ¶Ðµ ÐµÑÑ‚ÑŒ â€” O(1) vs O(n) Contains
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

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  TICK
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        public void Tick(float deltaTime)
        {
            if (_objects.Count == 0) return;

            _frameCounter++;
            _time += deltaTime;
            if (_time > 100000f) _time -= 100000f;

            // Cooldown Ð½Ð° Ð¿Ð¾Ð¸ÑÐº observer â€” Ð½Ðµ ÐºÐ°Ð¶Ð´Ñ‹Ð¹ ÐºÐ°Ð´Ñ€
            _observerResolveTimer -= deltaTime;
            if (_observerResolveTimer <= 0f)
            {
                TryResolveObserver(force: false);
            }

            _debugNearCount   = 0;
            _debugMediumCount = 0;
            _debugFarCount    = 0;
            _debugCulledCount = 0;

            // ÐšÑÑˆÐ¸Ñ€ÑƒÐµÐ¼ Ð¿Ð¾Ð·Ð¸Ñ†Ð¸ÑŽ Ð½Ð°Ð±Ð»ÑŽÐ´Ð°Ñ‚ÐµÐ»Ñ Ð¾Ð´Ð¸Ð½ Ñ€Ð°Ð· Ð·Ð° Ñ‚Ð¸Ðº
            // Ð˜Ð·Ð±ÐµÐ³Ð°ÐµÐ¼ Ð¿Ð¾Ð²Ñ‚Ð¾Ñ€Ð½Ñ‹Ñ… bridge calls Ð² ShouldUpdate Ð´Ð»Ñ ÐºÐ°Ð¶Ð´Ð¾Ð³Ð¾ Ð¾Ð±ÑŠÐµÐºÑ‚Ð°
            Vector3 observerPos = lodObserver != null
                ? lodObserver.position
                : Vector3.zero;

            // ÐšÐ²Ð°Ð´Ñ€Ð°Ñ‚Ñ‹ Ð´Ð¸ÑÑ‚Ð°Ð½Ñ†Ð¸Ð¹ â€” ÑÑ‡Ð¸Ñ‚Ð°ÐµÐ¼ Ð¾Ð´Ð¸Ð½ Ñ€Ð°Ð· Ð·Ð° Ñ‚Ð¸Ðº
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                AmbientWaterMotion motion = _objects[i];

                // Null-check: Ð¾Ð±ÑŠÐµÐºÑ‚ Ð¼Ð¾Ð³ Ð±Ñ‹Ñ‚ÑŒ ÑƒÐ½Ð¸Ñ‡Ñ‚Ð¾Ð¶ÐµÐ½ Ð±ÐµÐ· OnDisable
                if (motion == null || motion.CachedTransform == null)
                {
                    // Swap-and-pop: O(1) ÑƒÐ´Ð°Ð»ÐµÐ½Ð¸Ðµ Ð¸Ð· ÑÐµÑ€ÐµÐ´Ð¸Ð½Ñ‹ ÑÐ¿Ð¸ÑÐºÐ°
                    _objectsSet.Remove(motion);
                    int last = _objects.Count - 1;
                    _objects[i] = _objects[last];
                    _objects.RemoveAt(last);
                    continue;
                }

                // Ð§Ð¸Ñ‚Ð°ÐµÐ¼ position ÐžÐ”Ð˜Ð Ð ÐÐ— â€” ÐºÑÑˆÐ¸Ñ€ÑƒÐµÐ¼ Ð´Ð»Ñ ShouldUpdate Ð¸ ApplyMotion
                // Ð‘Ñ‹Ð»Ð¾: position Ñ‡Ð¸Ñ‚Ð°Ð»ÑÑ Ð´Ð²Ð°Ð¶Ð´Ñ‹ (Ð² ShouldUpdate Ð¸ Ð² ApplyMotion)
                Vector3 worldPos = motion.CachedTransform.position;

                if (!ShouldUpdate(motion, i, worldPos, observerPos,
                                  _nearDistanceSqr, _mediumDistanceSqr, _farDistanceSqr, _cullDistanceSqr))
                    continue;

                ApplyMotion(motion, worldPos);
            }

            _debugActiveObjects = _objects.Count;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  SHOULD UPDATE â€” Ð¿Ñ€Ð¸Ð½Ð¸Ð¼Ð°ÐµÑ‚ Ð¿Ñ€ÐµÐ´Ð²Ñ‹Ñ‡Ð¸ÑÐ»ÐµÐ½Ð½Ñ‹Ðµ Ð´Ð°Ð½Ð½Ñ‹Ðµ, Ð½ÐµÑ‚ bridge calls
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private bool ShouldUpdate(
            AmbientWaterMotion motion,
            int index,
            Vector3 worldPos,      // Ð¿Ñ€ÐµÐ´Ð²Ñ‹Ñ‡Ð¸ÑÐ»ÐµÐ½ Ð² Tick
            Vector3 observerPos,   // Ð¿Ñ€ÐµÐ´Ð²Ñ‹Ñ‡Ð¸ÑÐ»ÐµÐ½ Ð² Tick
            float nearSq, float mediumSq, float farSq, float cullSq)
        {
            if (!motion.AllowDistanceLod || lodObserver == null)
            {
                _debugNearCount++;
                return true;
            }

            float bias = Mathf.Max(0.1f, motion.LodBias);
            float dx = worldPos.x - observerPos.x;
            float dy = worldPos.y - observerPos.y;
            float dz = worldPos.z - observerPos.z;
            float distanceSq = dx * dx + dy * dy + dz * dz;

            // ÐŸÑ€Ð¸Ð¼ÐµÐ½ÑÐµÐ¼ bias ÐºÐ°Ðº Ð¼Ð½Ð¾Ð¶Ð¸Ñ‚ÐµÐ»ÑŒ Ðº Ð¿Ð¾Ñ€Ð¾Ð³Ð°Ð¼ (Ð½Ðµ Ðº distanceSq â€”
            // Ñ‚Ð°Ðº bias Ñ€Ð°Ð±Ð¾Ñ‚Ð°ÐµÑ‚ Ð¸Ð½Ñ‚ÑƒÐ¸Ñ‚Ð¸Ð²Ð½Ð¾: bias>1 = Ð¾Ð±ÑŠÐµÐºÑ‚ "Ð´Ð°Ð»ÑŒÑˆÐµ" Ñ‡ÐµÐ¼ ÐµÑÑ‚ÑŒ)
            float biasSq = bias * bias;
            if (distanceSq <= nearSq * biasSq)
            {
                _debugNearCount++;
                return true;
            }

            if (distanceSq <= mediumSq * biasSq)
            {
                _debugMediumCount++;
                return ((_frameCounter + index) % Mathf.Max(1, mediumDivisor)) == 0;
            }

            if (distanceSq <= farSq * biasSq)
            {
                _debugFarCount++;
                return ((_frameCounter + index) % Mathf.Max(1, farDivisor)) == 0;
            }

            _debugCulledCount++;
            return distanceSq <= cullSq * biasSq
                && ((_frameCounter + index) % Mathf.Max(1, cullDivisor)) == 0;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  APPLY MOTION â€” worldPos Ð¿ÐµÑ€ÐµÐ´Ð°Ñ‘Ñ‚ÑÑ Ð¸Ð·Ð²Ð½Ðµ, Ð½Ðµ Ñ‡Ð¸Ñ‚Ð°ÐµÑ‚ÑÑ Ð¿Ð¾Ð²Ñ‚Ð¾Ñ€Ð½Ð¾
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void ApplyMotion(AmbientWaterMotion motion, Vector3 worldPos)
        {
            Transform tr = motion.CachedTransform;

            // CurrentVolume: managed, main thread only. ÐÐ¾Ñ€Ð¼Ð°Ð»ÑŒÐ½Ð¾.
            Vector3 volumeCurrent = CurrentVolume.SampleAt(worldPos);

            // CurrentManager: static, pure math, no allocations.
            float3 phantomCurrent = CurrentManager.SampleHorizontal(
                new float3(worldPos.x, worldPos.y, worldPos.z),
                _time,
                0.018f,
                0.12f,
                motion.CurrentCoupling);

            Vector3 current = volumeCurrent
                + new Vector3(phantomCurrent.x, phantomCurrent.y, phantomCurrent.z);

            float currentMagnitude = current.magnitude;
            Vector3 currentDir = currentMagnitude > 0.0001f
                ? current / currentMagnitude
                : Vector3.forward;

            float t = (_time + motion.Phase)
                    * Mathf.Max(0f, motion.BaseFrequency * globalFrequency);

            float bobY = Mathf.Sin(t * 1.13f) * motion.VerticalAmplitude;
            float bobX = Mathf.Sin(t * 0.91f) * motion.PositionalAmplitude.x;
            float bobZ = Mathf.Cos(t * 1.07f) * motion.PositionalAmplitude.z;

            float coupling = motion.CurrentCoupling;
            Vector3 offset = new Vector3(
                bobX + currentDir.x * currentMagnitude * 0.03f * coupling,
                bobY,
                bobZ + currentDir.z * currentMagnitude * 0.03f * coupling)
                * globalAmplitude;

            float pitch = Mathf.Sin(t * 0.87f) * motion.AngularAmplitude.x
                        + currentDir.z * currentMagnitude * 2f;
            float yaw   = Mathf.Sin(t * 0.43f) * motion.AngularAmplitude.y;
            float roll  = Mathf.Cos(t * 0.79f) * motion.AngularAmplitude.z
                        - currentDir.x * currentMagnitude * 3f;

            tr.localPosition = motion.RestLocalPosition + offset;
            tr.localRotation = motion.RestLocalRotation * Quaternion.Euler(pitch, yaw, roll);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  OBSERVER RESOLVE â€” Ñ cooldown, Ð½Ðµ ÐºÐ°Ð¶Ð´Ñ‹Ð¹ ÐºÐ°Ð´Ñ€
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <param name="force">true = Ð¸Ð³Ð½Ð¾Ñ€Ð¸Ñ€Ð¾Ð²Ð°Ñ‚ÑŒ cooldown (Awake, OnEnable).</param>
        private void TryResolveObserver(bool force = false)
        {
            // Ð•ÑÐ»Ð¸ ÑƒÐ¶Ðµ ÐµÑÑ‚ÑŒ â€” Ð½Ðµ Ð¸Ñ‰ÐµÐ¼
            if (lodObserver != null) return;

            // Ð•ÑÐ»Ð¸ cooldown Ð½Ðµ Ð¸ÑÑ‚Ñ‘Ðº Ð¸ Ð½Ðµ Ñ„Ð¾Ñ€ÑÐ¸Ð¼ â€” Ð¿Ñ€Ð¾Ð¿ÑƒÑÐºÐ°ÐµÐ¼
            if (!force && _observerResolveTimer > 0f) return;

            // Ð¡Ð±Ñ€Ð°ÑÑ‹Ð²Ð°ÐµÐ¼ Ñ‚Ð°Ð¹Ð¼ÐµÑ€ Ð½ÐµÐ·Ð°Ð²Ð¸ÑÐ¸Ð¼Ð¾ Ð¾Ñ‚ Ñ€ÐµÐ·ÑƒÐ»ÑŒÑ‚Ð°Ñ‚Ð° Ð¿Ð¾Ð¸ÑÐºÐ°
            // ÐÐµ Ð½Ð°ÑˆÐ»Ð¸ ÑÐµÐ¹Ñ‡Ð°Ñ â€” Ð¿Ð¾Ð´Ð¾Ð¶Ð´Ñ‘Ð¼ ÐµÑ‰Ñ‘ ObserverResolveCooldown ÑÐµÐºÑƒÐ½Ð´
            _observerResolveTimer = ObserverResolveCooldown;

            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform))
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
            _tickRegistered = true;
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
            if (_serviceRegistered || !Application.isPlaying || _instance != this)
                return;

            GlobalRegistry.RegisterAmbientWaterMotionRuntime(this);
            _serviceRegistered = true;
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
