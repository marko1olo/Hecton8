// ============================================================================
// HECTON-8 — AmbientWaterMotionManager.cs
// Centralized visual bob/sway updater. One tick for many decorative props.
//
// v1.1 OPTIMIZATIONS:
//   [FIX] TryResolveObserver: добавлен _observerResolveCooldown — не ищем
//         Camera.main/Player каждый кадр если не нашли. Пробуем раз в 2 сек.
//   [FIX] Register: замена Contains (O(n)) на HashSet для O(1) дедупликации.
//   [FIX] ApplyMotion: кэшируем worldPos из CachedTransform.position один раз,
//         передаём в ShouldUpdate чтобы не читать position дважды через bridge.
//   [FIX] ShouldUpdate: принимает worldPos как параметр, убран повторный .position.
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
    public sealed class AmbientWaterMotionManager : MonoBehaviour, ITickable
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

        // ── Registered objects ───────────────────────────────────────────────
        // List для итерации (cache-friendly), HashSet для O(1) дедупликации в Register.
        private readonly List<AmbientWaterMotion>     _objects    = new List<AmbientWaterMotion>(128);
        private readonly HashSet<AmbientWaterMotion>  _objectsSet = new HashSet<AmbientWaterMotion>();

        private float _time;
        private int   _frameCounter;
        private float _nearDistanceSqr;
        private float _mediumDistanceSqr;
        private float _farDistanceSqr;
        private float _cullDistanceSqr;

        // ── Observer resolve cooldown ────────────────────────────────────────
        // Если observer не назначен и не найден — не ищем каждый кадр.
        // Camera.main внутри — это FindObjectWithTag, дорого.
        private float _observerResolveTimer;
        private const float ObserverResolveCooldown = 2f;

        public static AmbientWaterMotionManager Instance => _instance;

        // ════════════════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ════════════════════════════════════════════════════════════════════

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            RefreshDistanceThresholds();
            // Пробуем сразу при старте
            TryResolveObserver(force: true);
        }

        private void OnEnable()
        {
            GameTickManager.Instance?.Register((ITickable)this);
        }

        private void OnDisable()
        {
            GameTickManager.Instance?.Unregister((ITickable)this);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        // ════════════════════════════════════════════════════════════════════
        //  REGISTRATION — O(1) дедупликация через HashSet
        // ════════════════════════════════════════════════════════════════════

        public void Register(AmbientWaterMotion motion)
        {
            if (motion == null) return;

            // HashSet.Add возвращает false если уже есть — O(1) vs O(n) Contains
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
            if (_objects.Count == 0) return;

            _frameCounter++;
            _time += deltaTime;
            if (_time > 100000f) _time -= 100000f;

            // Cooldown на поиск observer — не каждый кадр
            _observerResolveTimer -= deltaTime;
            if (_observerResolveTimer <= 0f)
            {
                TryResolveObserver(force: false);
            }

            _debugNearCount   = 0;
            _debugMediumCount = 0;
            _debugFarCount    = 0;
            _debugCulledCount = 0;

            // Кэшируем позицию наблюдателя один раз за тик
            // Избегаем повторных bridge calls в ShouldUpdate для каждого объекта
            Vector3 observerPos = lodObserver != null
                ? lodObserver.position
                : Vector3.zero;

            // Квадраты дистанций — считаем один раз за тик
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                AmbientWaterMotion motion = _objects[i];

                // Null-check: объект мог быть уничтожен без OnDisable
                if (motion == null || motion.CachedTransform == null)
                {
                    // Swap-and-pop: O(1) удаление из середины списка
                    _objectsSet.Remove(motion);
                    int last = _objects.Count - 1;
                    _objects[i] = _objects[last];
                    _objects.RemoveAt(last);
                    continue;
                }

                // Читаем position ОДИН РАЗ — кэшируем для ShouldUpdate и ApplyMotion
                // Было: position читался дважды (в ShouldUpdate и в ApplyMotion)
                Vector3 worldPos = motion.CachedTransform.position;

                if (!ShouldUpdate(motion, i, worldPos, observerPos,
                                  _nearDistanceSqr, _mediumDistanceSqr, _farDistanceSqr, _cullDistanceSqr))
                    continue;

                ApplyMotion(motion, worldPos);
            }

            _debugActiveObjects = _objects.Count;
        }

        // ════════════════════════════════════════════════════════════════════
        //  SHOULD UPDATE — принимает предвычисленные данные, нет bridge calls
        // ════════════════════════════════════════════════════════════════════

        private bool ShouldUpdate(
            AmbientWaterMotion motion,
            int index,
            Vector3 worldPos,      // предвычислен в Tick
            Vector3 observerPos,   // предвычислен в Tick
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

            // Применяем bias как множитель к порогам (не к distanceSq —
            // так bias работает интуитивно: bias>1 = объект "дальше" чем есть)
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

        // ════════════════════════════════════════════════════════════════════
        //  APPLY MOTION — worldPos передаётся извне, не читается повторно
        // ════════════════════════════════════════════════════════════════════

        private void ApplyMotion(AmbientWaterMotion motion, Vector3 worldPos)
        {
            Transform tr = motion.CachedTransform;

            // CurrentVolume: managed, main thread only. Нормально.
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

        // ════════════════════════════════════════════════════════════════════
        //  OBSERVER RESOLVE — с cooldown, не каждый кадр
        // ════════════════════════════════════════════════════════════════════

        /// <param name="force">true = игнорировать cooldown (Awake, OnEnable).</param>
        private void TryResolveObserver(bool force = false)
        {
            // Если уже есть — не ищем
            if (lodObserver != null) return;

            // Если cooldown не истёк и не форсим — пропускаем
            if (!force && _observerResolveTimer > 0f) return;

            // Сбрасываем таймер независимо от результата поиска
            // Не нашли сейчас — подождём ещё ObserverResolveCooldown секунд
            _observerResolveTimer = ObserverResolveCooldown;

            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                lodObserver = mainCam.transform;
                return;
            }

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
