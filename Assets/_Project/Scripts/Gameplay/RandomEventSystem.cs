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
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.UI;
using UnityEngine;

namespace Hecton8.Gameplay
{
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
        }

        /// <summary>Случайное событие началось.</summary>
        public static event Action<RandomEventType, float> OnEventStarted;

        /// <summary>Случайное событие завершилось.</summary>
        public static event Action<RandomEventType> OnEventEnded;

        public static void RaiseStarted(RandomEventType type, float intensity)
            => OnEventStarted?.Invoke(type, intensity);

        public static void RaiseEnded(RandomEventType type)
            => OnEventEnded?.Invoke(type);
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
            if (GameTickManager.Instance != null && !_registered)
            {
                GameTickManager.Instance.Register(this);
                _registered = true;
            }

            ResolveSurvivalSystem();
        }

        private void OnDisable()
        {
            if (GameTickManager.Instance != null && _registered)
            {
                GameTickManager.Instance.Unregister(this);
                _registered = false;
            }

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
            NotificationEvents.PushInfo("БИОЛЮМИНЕСЦЕНТНЫЙ ШТОРМ — ВИДИМОСТЬ +30%. ФАУНА АКТИВИЗИРУЕТСЯ.");
        }

        private void TryTriggerThermalEruption(float depth)
        {
            if (IsEventActive(RandomEventType.ThermalEruption)) return;
            if (depth < 3000f) return; // Только в рифтовых зонах
            if (UnityEngine.Random.value > thermalEruptionChance) return;

            StartEvent(RandomEventType.ThermalEruption, thermalEruptionDuration, 1f);
            NotificationEvents.PushWarning("ТЕРМАЛЬНЫЙ ВЫБРОС — ОПАСНОСТЬ ОЖОГА. РЕДКИЕ МИНЕРАЛЫ ДОСТУПНЫ.");

            // Урон оборудованию
            if (survivalSystem != null)
                survivalSystem.TakeDamage(5f);
        }

        private void TryTriggerFaunaMigration()
        {
            if (IsEventActive(RandomEventType.FaunaMigration)) return;
            if (UnityEngine.Random.value > faunaMigrationChance) return;

            StartEvent(RandomEventType.FaunaMigration, faunaMigrationDuration, 0.5f);
            NotificationEvents.PushInfo("МИГРАЦИЯ СТАИ — ПОВЕДЕНИЕ ФАУНЫ ИЗМЕНИЛОСЬ.");
        }

        private void TryTriggerGlitch(float depth)
        {
            if (IsEventActive(RandomEventType.HectonOSGlitch)) return;
            if (depth < 500f) return;
            if (UnityEngine.Random.value > glitchChance) return;

            StartEvent(RandomEventType.HectonOSGlitch, glitchDuration, 0.6f);
            Shader.SetGlobalFloat(_ShaderGlitchActive, 1f);
            NotificationEvents.PushWarning("HECTON-OS: СБОЙ — РАДИАЦИОННЫЕ ПОМЕХИ. ПОКАЗАНИЯ МОГУТ БЫТЬ НЕТОЧНЫМИ.");
        }

        private void TryTriggerCaveCollapse(float depth)
        {
            if (IsEventActive(RandomEventType.CaveCollapse)) return;
            if (depth < 200f) return;
            if (UnityEngine.Random.value > caveCollapseChance) return;

            StartEvent(RandomEventType.CaveCollapse, caveCollapseDuration, 1f);
            NotificationEvents.PushWarning("ОБРУШЕНИЕ ПЕЩЕРЫ — ПУТЬ ЗАБЛОКИРОВАН. ВОЗМОЖЕН НОВЫЙ ПРОХОД.");
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
    }
}
