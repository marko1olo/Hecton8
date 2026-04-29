// ============================================================================
// HECTON-8 — RandomEventSystem.cs
// Система случайных событий мира.
//
// ЛОР (лор3 Блок 16 — Random Event Table):
//   • Биолюминесцентный шторм: глубина > 1000м, видимость +30%, привлечение фауны
//   • Термальный выброс: рифтовая зона, урон оборудованию, редкие минералы
//   • Миграция стаи: любой биом, изменение поведения фауны
//   • Сбой Hecton-OS: радиация/глубина, глитчи интерфейса
//   • Обрушение пещеры: воксельная зона, блокировка пути, новый лут
//
// АРХИТЕКТУРА:
//   • ISlowTickable — проверка условий раз в 0.5с.
//   • Каждое событие: условия, частота, эффект.
//   • Публикует события через RandomEventEvents.
//   • Интегрируется с HectonDirectorAI (tension modifier).
//
// ZERO GC:
//   • Pre-allocated массив состояний событий.
//   • Никаких new/LINQ в SlowTick.
// ============================================================================

using System;
using Hecton.Localization;
using Hecton8.Bootstrap;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Physics;
using Hecton8.UI;
using Hecton8.World;
using UnityEngine;

namespace Hecton8.Gameplay
{
    public readonly struct SeismicShockwaveEvent
    {
        public readonly Vector3 EpicenterWS;
        public readonly float ImpulseRadiusMeters;
        public readonly float ImpulseMagnitude;
        public readonly int AppliedStampCount;

        public SeismicShockwaveEvent(
            Vector3 epicenterWS,
            float impulseRadiusMeters,
            float impulseMagnitude,
            int appliedStampCount)
        {
            EpicenterWS = epicenterWS;
            ImpulseRadiusMeters = impulseRadiusMeters;
            ImpulseMagnitude = impulseMagnitude;
            AppliedStampCount = appliedStampCount;
        }
    }

    public enum RandomEventType
    {
        BiolumStorm     = 0,   // Биолюминесцентный шторм
        ThermalEruption = 1,   // Термальный выброс
        FaunaMigration  = 2,   // Миграция стаи
        HectonOSGlitch  = 3,   // Сбой Hecton-OS
        CaveCollapse    = 4    // Обрушение пещеры
    }

    public static class RandomEventEvents
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            OnEventStarted = null;
            OnEventEnded = null;
            OnSeismicShockwave = null;
        }

        /// <summary>Случайное событие началось.</summary>
        public static event Action<RandomEventType, float> OnEventStarted;

        /// <summary>Случайное событие завершилось.</summary>
        public static event Action<RandomEventType> OnEventEnded;

        /// <summary>Воксельное сейсмическое событие применило реальные разрушения.</summary>
        public static event Action<SeismicShockwaveEvent> OnSeismicShockwave;

        public static void RaiseStarted(RandomEventType type, float intensity)
            => OnEventStarted?.Invoke(type, intensity);

        public static void RaiseEnded(RandomEventType type)
            => OnEventEnded?.Invoke(type);

        public static void RaiseSeismicShockwave(in SeismicShockwaveEvent payload)
            => OnSeismicShockwave?.Invoke(payload);
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-70)]
    public sealed class RandomEventSystem : MonoBehaviour, ISlowTickable
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── References ──────────────────────────────")]
        [SerializeField] private HectonSurvivalSystem survivalSystem;
        [SerializeField] private HectonVoxelEngine voxelEngine;
        [SerializeField] private TectonicActivityProfile tectonicActivityProfile;

        [Header("── Event Probabilities (per SlowTick) ──────")]
        [SerializeField, Range(0f, 0.01f)] private float biolumStormChance    = 0.001f;
        [SerializeField, Range(0f, 0.01f)] private float thermalEruptionChance = 0.0005f;
        [SerializeField, Range(0f, 0.02f)] private float faunaMigrationChance  = 0.002f;
        [SerializeField, Range(0f, 0.01f)] private float glitchChance          = 0.0008f;
        [SerializeField, Range(0f, 0.005f)] private float caveCollapseChance   = 0.0003f;

        [Header("── Event Durations (seconds) ───────────────")]
        [SerializeField] private float biolumStormDuration    = 120f;
        [SerializeField] private float thermalEruptionDuration = 30f;
        [SerializeField] private float faunaMigrationDuration  = 180f;
        [SerializeField] private float glitchDuration          = 15f;
        [SerializeField] private float caveCollapseDuration    = 5f;

        [Header("── Seismic Collapse ───────────────────────")]
        [SerializeField, Min(4f)] private float seismicTargetRadius = 72f;
        [SerializeField, Range(16, 256)] private int seismicOverlapCapacity = 96;
        [SerializeField, Range(16, 128)] private int seismicUniqueBodyCapacity = 48;

        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        public static RandomEventSystem Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => Instance = null;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        // Таймеры активных событий (0 = неактивно)
        private readonly float[] _eventTimers = new float[5];
        // COLD ALLOC: Collider[96] - reusable shockwave overlap buffer for cave-collapse rigidbody routing - owner: RandomEventSystem
        private readonly Collider[] _seismicOverlapBuffer = new Collider[96];
        // COLD ALLOC: Rigidbody[48] - reusable unique rigidbody buffer for cave-collapse impulse routing - owner: RandomEventSystem
        private readonly Rigidbody[] _seismicBodyBuffer = new Rigidbody[48];
        private bool _registered;

        // Shader IDs
        private static readonly int _ShaderBiolumStorm  = Shader.PropertyToID("_BiolumStormActive");
        private static readonly int _ShaderGlitchActive = Shader.PropertyToID("_HUDGlitchActive");

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            TryRegister();

            ResolveSurvivalSystem();
        }

        private void OnDisable()
        {
            TryUnregister();

            // Сбрасываем все активные события
            for (int i = 0; i < _eventTimers.Length; i++)
            {
                if (_eventTimers[i] > 0f)
                {
                    _eventTimers[i] = 0f;
                    RandomEventEvents.RaiseEnded((RandomEventType)i);
                }
            }

            Shader.SetGlobalFloat(_ShaderBiolumStorm, 0f);
            Shader.SetGlobalFloat(_ShaderGlitchActive, 0f);
        }

        private void OnDestroy()
        {
            TryUnregister();

            if (Instance == this)
                Instance = null;
        }

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable
        // ══════════════════════════════════════════════════════════

        public void SlowTick()
        {
            if (survivalSystem == null && !ResolveSurvivalSystem())
                return;

            const float dt = 0.5f;
            float depth = survivalSystem != null ? survivalSystem.Depth : 0f;

            // Обновляем таймеры активных событий
            for (int i = 0; i < _eventTimers.Length; i++)
            {
                if (_eventTimers[i] <= 0f) continue;

                _eventTimers[i] -= dt;
                if (_eventTimers[i] <= 0f)
                {
                    _eventTimers[i] = 0f;
                    OnEventEnd((RandomEventType)i);
                }
            }

            // Проверяем условия для новых событий
            TryTriggerBiolumStorm(depth);
            TryTriggerThermalEruption(depth);
            TryTriggerFaunaMigration();
            TryTriggerGlitch(depth);
            TryTriggerCaveCollapse(depth);
        }

        private void TryRegister()
        {
            if (_registered)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registered = false;
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        public bool IsEventActive(RandomEventType type)
            => _eventTimers[(int)type] > 0f;

        public float GetEventTimeRemaining(RandomEventType type)
            => Mathf.Max(0f, _eventTimers[(int)type]);

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — EVENT TRIGGERS
        // ══════════════════════════════════════════════════════════

        private void TryTriggerBiolumStorm(float depth)
        {
            if (IsEventActive(RandomEventType.BiolumStorm)) return;
            if (depth < 1000f) return;
            if (UnityEngine.Random.value > biolumStormChance) return;

            StartEvent(RandomEventType.BiolumStorm, biolumStormDuration, 0.8f);
            Shader.SetGlobalFloat(_ShaderBiolumStorm, 1f);
            NotificationEvents.PushInfo(ResolveLocalized(
                LocalizationKeys.RANDOM_EVENT_BIOLUM_STORM,
                "BIOLUMINESCENT STORM - VISIBILITY +30%. FAUNA AGITATED."));
        }

        private void TryTriggerThermalEruption(float depth)
        {
            if (IsEventActive(RandomEventType.ThermalEruption)) return;
            if (depth < 3000f) return; // Только в рифтовых зонах
            if (UnityEngine.Random.value > thermalEruptionChance) return;

            StartEvent(RandomEventType.ThermalEruption, thermalEruptionDuration, 1f);
            NotificationEvents.PushWarning(ResolveLocalized(
                LocalizationKeys.RANDOM_EVENT_THERMAL_ERUPTION,
                "THERMAL ERUPTION - BURN HAZARD. RARE MINERALS EXPOSED."));

            // Урон оборудованию
            if (survivalSystem != null)
                survivalSystem.TakeDamage(5f);
        }

        private void TryTriggerFaunaMigration()
        {
            if (IsEventActive(RandomEventType.FaunaMigration)) return;
            if (UnityEngine.Random.value > faunaMigrationChance) return;

            StartEvent(RandomEventType.FaunaMigration, faunaMigrationDuration, 0.5f);
            NotificationEvents.PushInfo(ResolveLocalized(
                LocalizationKeys.RANDOM_EVENT_FAUNA_MIGRATION,
                "PACK MIGRATION - FAUNA BEHAVIOR SHIFT DETECTED."));
        }

        private void TryTriggerGlitch(float depth)
        {
            if (IsEventActive(RandomEventType.HectonOSGlitch)) return;
            if (depth < 500f) return;
            if (UnityEngine.Random.value > glitchChance) return;

            StartEvent(RandomEventType.HectonOSGlitch, glitchDuration, 0.6f);
            Shader.SetGlobalFloat(_ShaderGlitchActive, 1f);
            NotificationEvents.PushWarning(ResolveLocalized(
                LocalizationKeys.RANDOM_EVENT_HECTON_OS_GLITCH,
                "HECTON-OS GLITCH - RADIATION INTERFERENCE. READINGS MAY BE INACCURATE."));
        }

        private void TryTriggerCaveCollapse(float depth)
        {
            if (IsEventActive(RandomEventType.CaveCollapse)) return;
            if (depth < 200f) return;
            if (!TryResolveSeismicContext(
                    out Vector3 playerPosition,
                    out HectonVoxelVolume targetVolume,
                    out TectonicActivityProfile.SeismicEventSettings settings))
            {
                return;
            }

            float resolvedChance = caveCollapseChance * settings.collapseChanceMultiplier;
            if (UnityEngine.Random.value > Mathf.Clamp(resolvedChance, 0f, 1f)) return;
            if (!TryExecuteSeismicShockwave(playerPosition, targetVolume, settings, out SeismicShockwaveEvent seismicEvent))
                return;

            StartEvent(RandomEventType.CaveCollapse, caveCollapseDuration, 1f);
            RandomEventEvents.RaiseSeismicShockwave(in seismicEvent);
            NotificationEvents.PushWarning(ResolveLocalized(
                LocalizationKeys.RANDOM_EVENT_CAVE_COLLAPSE,
                "CAVE COLLAPSE - ROUTE BLOCKED. POSSIBLE NEW OPENING."));
        }

        private void StartEvent(RandomEventType type, float duration, float intensity)
        {
            _eventTimers[(int)type] = duration;
            RandomEventEvents.RaiseStarted(type, intensity);

            LogEventStarted(type, duration, intensity);
        }

        private void OnEventEnd(RandomEventType type)
        {
            RandomEventEvents.RaiseEnded(type);

            // Сбрасываем шейдерные эффекты
            switch (type)
            {
                case RandomEventType.BiolumStorm:
                    Shader.SetGlobalFloat(_ShaderBiolumStorm, 0f);
                    break;
                case RandomEventType.HectonOSGlitch:
                    Shader.SetGlobalFloat(_ShaderGlitchActive, 0f);
                    break;
            }

            LogEventEnded(type);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogEventStarted(RandomEventType type, float duration, float intensity)
        {
            Debug.Log($"[RandomEvent] Started: {type} (duration: {duration}s, intensity: {intensity:F2})");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogEventEnded(RandomEventType type)
        {
            Debug.Log($"[RandomEvent] Ended: {type}");
        }

        private bool ResolveSurvivalSystem()
        {
            if (survivalSystem != null)
                return true;

            if (!SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) ||
                playerTransform == null)
            {
                return false;
            }

            return playerTransform.TryGetComponent(out survivalSystem);
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = LocalizationManager.Instance;
            return manager != null
                ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback)
                : fallback;
        }

        private bool TryResolveSeismicContext(
            out Vector3 playerPosition,
            out HectonVoxelVolume targetVolume,
            out TectonicActivityProfile.SeismicEventSettings settings)
        {
            playerPosition = default;
            targetVolume = null;
            settings = tectonicActivityProfile != null
                ? tectonicActivityProfile.ResolveSeismicSettings(null, null)
                : default;

            if (!SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) || playerTransform == null)
                return false;

            playerPosition = playerTransform.position;
            if (voxelEngine == null)
                voxelEngine = HectonVoxelEngine.ActiveRuntimeInstance;

            if (voxelEngine == null || !voxelEngine.TryGetNearestActiveVolume(playerPosition, out targetVolume) || targetVolume == null)
                return false;

            float maxTargetRadius = Mathf.Max(4f, seismicTargetRadius);
            if ((targetVolume.generationPosition - playerPosition).sqrMagnitude > maxTargetRadius * maxTargetRadius)
                return false;

            string familyId = null;
            string geologyProfileId = null;
            if (targetVolume.TryGetComponent(out WorldGenerativeGeologyVoxelRuntime runtime))
            {
                familyId = runtime.FamilyId;
                geologyProfileId = runtime.GeologyProfileId;
            }

            settings = tectonicActivityProfile != null
                ? tectonicActivityProfile.ResolveSeismicSettings(familyId, geologyProfileId)
                : new TectonicActivityProfile.SeismicEventSettings
                {
                    collapseChanceMultiplier = 1f,
                    stampCountMin = 2,
                    stampCountMax = 4,
                    stampScatterRadius = 18f,
                    ceilingSearchDepth = 18f,
                    craterRadiusMin = 2.5f,
                    craterRadiusMax = 6f,
                    impulseRadius = 100f,
                    impulseMagnitude = 14f
                }.Sanitize();
            return true;
        }

        private bool TryExecuteSeismicShockwave(
            Vector3 playerPosition,
            HectonVoxelVolume targetVolume,
            TectonicActivityProfile.SeismicEventSettings settings,
            out SeismicShockwaveEvent seismicEvent)
        {
            seismicEvent = default;
            if (targetVolume == null)
                return false;

            int stampCount = UnityEngine.Random.Range(settings.stampCountMin, settings.stampCountMax + 1);
            uint stableSeed = unchecked(((uint)Time.frameCount * 2654435761u) ^ (uint)targetVolume.RuntimeStamp);
            if (!targetVolume.TryApplySeismicShockwave(
                    playerPosition,
                    stampCount,
                    settings.stampScatterRadius,
                    settings.ceilingSearchDepth,
                    settings.craterRadiusMin,
                    settings.craterRadiusMax,
                    stableSeed,
                    out int appliedStampCount))
            {
                return false;
            }

            ApplySeismicImpulse(playerPosition, settings.impulseRadius, settings.impulseMagnitude);
            seismicEvent = new SeismicShockwaveEvent(
                playerPosition,
                settings.impulseRadius,
                settings.impulseMagnitude,
                appliedStampCount);
            return true;
        }

        private void ApplySeismicImpulse(Vector3 epicenter, float radius, float impulseMagnitude)
        {
            int overlapCapacity = Mathf.Clamp(seismicOverlapCapacity, 16, _seismicOverlapBuffer.Length);
            int hitCount = UnityEngine.Physics.OverlapSphereNonAlloc(
                epicenter,
                Mathf.Max(1f, radius),
                _seismicOverlapBuffer,
                ~0,
                QueryTriggerInteraction.Ignore);
            if (hitCount <= 0)
                return;

            int uniqueCapacity = Mathf.Clamp(seismicUniqueBodyCapacity, 16, _seismicBodyBuffer.Length);
            int uniqueBodyCount = 0;
            for (int hitIndex = 0; hitIndex < hitCount && hitIndex < overlapCapacity; hitIndex++)
            {
                Collider collider = _seismicOverlapBuffer[hitIndex];
                _seismicOverlapBuffer[hitIndex] = null;
                if (collider == null)
                    continue;

                Rigidbody body = collider.attachedRigidbody;
                if (body == null || body.isKinematic)
                    continue;

                bool duplicate = false;
                for (int bodyIndex = 0; bodyIndex < uniqueBodyCount; bodyIndex++)
                {
                    if (_seismicBodyBuffer[bodyIndex] != body)
                        continue;

                    duplicate = true;
                    break;
                }

                if (duplicate)
                    continue;

                _seismicBodyBuffer[uniqueBodyCount++] = body;
                if (uniqueBodyCount >= uniqueCapacity)
                    break;
            }

            float safeRadius = Mathf.Max(1f, radius);
            for (int bodyIndex = 0; bodyIndex < uniqueBodyCount; bodyIndex++)
            {
                Rigidbody body = _seismicBodyBuffer[bodyIndex];
                _seismicBodyBuffer[bodyIndex] = null;
                if (body == null)
                    continue;

                Vector3 away = body.worldCenterOfMass - epicenter;
                float distance = away.magnitude;
                if (distance > safeRadius)
                    continue;

                Vector3 direction = distance > 0.0001f ? away / distance : Vector3.up;
                direction.y = Mathf.Max(direction.y, 0.25f);
                direction.Normalize();

                float distance01 = 1f - Mathf.Clamp01(distance / safeRadius);
                float resolvedImpulse = impulseMagnitude * Mathf.Pow(distance01, 0.65f);
                PhysicsForceRouter.QueueForce(body, direction * resolvedImpulse, ForceMode.Impulse);
            }
        }
    }
}
