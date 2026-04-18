// ============================================================================
// HECTON-8 — EclipseGameplaySystem.cs
// Геймплейные последствия Великого Затмения.
//
// ЛОР (лор1):
//   • Температура падает на 8°C за минуту
//   • Ночные хищники поднимаются из глубины
//   • Биолюминесценция усиливается
//   • Бездонник поднимается до 200-300м
//   • Planet-shine — единственное освещение
//
// АРХИТЕКТУРА:
//   • Слушает HectonCelestialEngine.OnEclipseStart/End.
//   • Публикует события для HUD, атмосферы, фауны.
//   • ISlowTickable — температурный дрейф во время затмения.
//   • Интегрируется с HectonAtmosphereManager через событие.
//
// ZERO GC:
//   • Никаких new/LINQ в SlowTick.
//   • Static events для decoupled уведомлений.
// ============================================================================

using System;
using Conditional = System.Diagnostics.ConditionalAttribute;
using Hecton.Localization;
using Hecton8.Celestial;
using Hecton8.Core;
using Hecton8.UI;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Статическая шина событий затмения для геймплейных систем.
    /// </summary>
    public static class EclipseGameplayEvents
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            OnEclipsePhaseChanged = null;
            OnNightPredatorsRising = null;
            OnEclipseTemperatureDelta = null;
        }

        /// <summary>Фаза затмения изменилась. bool: true = активно.</summary>
        public static event Action<bool> OnEclipsePhaseChanged;

        /// <summary>Ночные хищники поднимаются. float: интенсивность [0..1].</summary>
        public static event Action<float> OnNightPredatorsRising;

        /// <summary>Температурная дельта от затмения. float: дельта °C (отрицательная).</summary>
        public static event Action<float> OnEclipseTemperatureDelta;

        public static void RaisePhaseChanged(bool active) => OnEclipsePhaseChanged?.Invoke(active);
        public static void RaiseNightPredatorsRising(float intensity) => OnNightPredatorsRising?.Invoke(intensity);
        public static void RaiseTemperatureDelta(float delta) => OnEclipseTemperatureDelta?.Invoke(delta);
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class EclipseGameplaySystem : MonoBehaviour, ISlowTickable
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Temperature ─────────────────────────────")]
        [Tooltip("Скорость падения температуры во время затмения (°C/сек).")]
        [SerializeField] private float temperatureCoolRate = 0.133f; // 8°C/мин = 0.133°C/сек

        [Tooltip("Максимальное падение температуры за одно затмение (°C).")]
        [SerializeField] private float maxTemperatureDrop = 8f;

        [Tooltip("Скорость восстановления температуры после затмения (°C/сек).")]
        [SerializeField] private float temperatureRecoveryRate = 0.05f;

        [Header("── Night Predators ──────────────────────────")]
        [Tooltip("Задержка перед подъёмом ночных хищников (сек после начала затмения).")]
        [SerializeField] private float predatorRiseDelay = 60f;

        [Tooltip("Интенсивность подъёма хищников [0..1].")]
        [SerializeField, Range(0f, 1f)] private float predatorRiseIntensity = 0.7f;

        [Header("── Bioluminescence ────────────────────────")]
        [Tooltip("Множитель биолюминесценции во время затмения.")]
        [SerializeField] private float biolumMultiplier = 1.5f;

        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        public static EclipseGameplaySystem Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => Instance = null;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private bool  _eclipseActive;
        private float _eclipseTimer;
        private float _currentTempDrop;
        private bool  _predatorsRisen;
        private bool  _registered;

        private static readonly int _ShaderBiolumMultiplier =
            Shader.PropertyToID("_EclipseBiolumMultiplier");

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        public bool IsEclipseActive => _eclipseActive;
        public float CurrentTempDrop => _currentTempDrop;
        public float EclipseProgress => _eclipseActive && maxTemperatureDrop > 0f
            ? _currentTempDrop / maxTemperatureDrop
            : 0f;

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

            HectonCelestialEngine.OnEclipseStart += HandleEclipseStart;
            HectonCelestialEngine.OnEclipseEnd   += HandleEclipseEnd;
        }

        private void OnDisable()
        {
            TryUnregister();

            HectonCelestialEngine.OnEclipseStart -= HandleEclipseStart;
            HectonCelestialEngine.OnEclipseEnd   -= HandleEclipseEnd;
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
            const float dt = 0.5f; // SlowTick interval

            if (_eclipseActive)
            {
                _eclipseTimer += dt;

                // Температурный дрейф
                if (_currentTempDrop < maxTemperatureDrop)
                {
                    float newDrop = Mathf.Min(maxTemperatureDrop,
                        _currentTempDrop + temperatureCoolRate * dt);

                    if (newDrop > _currentTempDrop)
                    {
                        _currentTempDrop = newDrop;
                        EclipseGameplayEvents.RaiseTemperatureDelta(-_currentTempDrop);
                    }
                }

                // Ночные хищники поднимаются после задержки
                if (!_predatorsRisen && _eclipseTimer >= predatorRiseDelay)
                {
                    _predatorsRisen = true;
                    EclipseGameplayEvents.RaiseNightPredatorsRising(predatorRiseIntensity);

                    LogNightPredatorsRising(predatorRiseIntensity);
                }
            }
            else
            {
                // Восстановление температуры
                if (_currentTempDrop > 0f)
                {
                    _currentTempDrop = Mathf.Max(0f,
                        _currentTempDrop - temperatureRecoveryRate * dt);
                    EclipseGameplayEvents.RaiseTemperatureDelta(-_currentTempDrop);
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        //  EVENT HANDLERS
        // ══════════════════════════════════════════════════════════

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogNightPredatorsRising(float intensity)
        {
            Debug.Log($"[Eclipse] Night predators rising! Intensity: {intensity:F2}");
        }

        private void HandleEclipseStart()
        {
            _eclipseActive = true;
            _eclipseTimer  = 0f;
            _predatorsRisen = false;

            EclipseGameplayEvents.RaisePhaseChanged(true);
            NotificationEvents.PushWarning(ResolveLocalized(
                LocalizationKeys.ECLIPSE_EVENT_STARTED,
                "GREAT ECLIPSE - TEMPERATURE FALLING. NIGHT PREDATORS ASCENDING."));

            // Биолюминесценция усиливается
            Shader.SetGlobalFloat(_ShaderBiolumMultiplier, biolumMultiplier);

            LogEclipseStarted();
        }

        private void HandleEclipseEnd()
        {
            _eclipseActive = false;

            EclipseGameplayEvents.RaisePhaseChanged(false);
            NotificationEvents.PushInfo(ResolveLocalized(
                LocalizationKeys.ECLIPSE_EVENT_ENDED,
                "ECLIPSE ENDED - TEMPERATURE RECOVERING."));

            // Биолюминесценция возвращается к норме
            Shader.SetGlobalFloat(_ShaderBiolumMultiplier, 1f);

            LogEclipseEnded();
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogEclipseStarted()
        {
            Debug.Log("[Eclipse] Eclipse started — gameplay consequences active.");
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogEclipseEnded()
        {
            Debug.Log("[Eclipse] Eclipse ended — temperature recovering.");
        }

        private void TryRegister()
        {
            if (_registered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            tickManager.Register(this);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager != null)
                tickManager.Unregister(this);

            _registered = false;
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = LocalizationManager.Instance;
            return manager != null
                ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback)
                : fallback;
        }
    }
}
