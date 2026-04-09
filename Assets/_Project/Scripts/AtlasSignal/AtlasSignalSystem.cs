// ============================================================================
// HECTON-8 — AtlasSignalSystem.cs
// Система пульса сигнала Атлас-6.
//
// ЛОР (лор3 Блок З):
//   Слух среди скавенджеров: "На Гектоне-8 есть сигнал, который повторяется
//   каждые 11:23". Ритм 11:23 — время перебора всех вариантов "спасения колонии".
//   Чем ближе к ядру — тем яснее "содержание" сигнала:
//   не слова, а эмоциональный паттерн: отчаяние, надежда, безумие.
//
// МЕХАНИКА:
//   • Пульс каждые 683 секунды (11 мин 23 сек).
//   • Сила сигнала = 1 - (dist / maxSignalRange).
//   • Сканер может "настроиться" → показывает направление к ядру.
//   • Интегрируется с QuestManager (QuestTriggerType.OnSignalDetected).
//   • Интегрируется с HectonDirectorAI (narrative beat).
//
// ZERO GC:
//   • ISlowTickable — таймер без Update().
//   • Никаких new/LINQ в hot path.
//   • Shader.SetGlobalFloat для визуального отклика биолюминесценции.
// ============================================================================

using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.SaveSystem;
using Hecton8.UI;
using UnityEngine;

namespace Hecton8.AtlasSignal
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-120)]
    public sealed class AtlasSignalSystem : MonoBehaviour, ISaveable, ISlowTickable
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Signal Parameters ──────────────────────")]
        [Tooltip("Период пульса в секундах (683 = 11 мин 23 сек).")]
        [SerializeField] private float pulsePeriodSeconds = 683f;

        [Tooltip("Максимальная дальность обнаружения сигнала (метры).")]
        [SerializeField] private float maxSignalRange = 8000f;

        [Tooltip("Позиция ядра Атлас-6 в мировых координатах.")]
        [SerializeField] private Vector3 atlasCorePosWorld = new Vector3(0f, -5000f, 0f);

        [Tooltip("Минимальная сила сигнала для обнаружения сканером.")]
        [SerializeField, Range(0f, 1f)] private float detectionThreshold = 0.05f;

        [Header("── Shader Integration ────────────────────")]
        [Tooltip("Публиковать силу сигнала в шейдер для биолюминесцентного отклика.")]
        [SerializeField] private bool publishToShader = true;

        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        public static AtlasSignalSystem Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => Instance = null;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private Transform _playerTransform;
        private float _pulseTimer;
        private float _currentStrength;
        private float _lastPublishedStrength;
        private bool _signalEverDetected;
        private bool _registered;

        private static readonly int _ShaderSignalStrength =
            Shader.PropertyToID("_AtlasSignalStrength");

        // Throttle log — static field, не в hot path
        private static float _nextSignalLogTime;

        private const float StrengthEpsilon = 0.01f;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        public float CurrentStrength => _currentStrength;
        public bool IsDetected => _currentStrength >= detectionThreshold;
        public Vector3 AtlasCorePosition => atlasCorePosWorld;

        /// <summary>
        /// Направление к ядру Атлас-6 от текущей позиции игрока.
        /// Используется сканером для навигации.
        /// </summary>
        public Vector3 DirectionToCore
        {
            get
            {
                if (_playerTransform == null) return Vector3.down;
                Vector3 toCore = atlasCorePosWorld - _playerTransform.position;
                float mag = toCore.magnitude;
                return mag > 0.001f ? toCore / mag : Vector3.down;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  ISaveable
        // ══════════════════════════════════════════════════════════

        public int SavePriority => 8;
        public int LoadPriority => 8;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            if (GameTickManager.Instance != null && !_registered)
            {
                GameTickManager.Instance.Register(this);
                _registered = true;
            }

            if (SaveManager.Instance != null)
                SaveManager.Instance.Register(this);

            ResolvePlayer();
        }

        private void OnDisable()
        {
            if (GameTickManager.Instance != null && _registered)
            {
                GameTickManager.Instance.Unregister(this);
                _registered = false;
            }

            if (SaveManager.Instance != null)
                SaveManager.Instance.Unregister(this);
        }

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable
        // ══════════════════════════════════════════════════════════

        public void SlowTick()
        {
            if (_playerTransform == null)
            {
                ResolvePlayer();
                if (_playerTransform == null) return;
            }

            _pulseTimer += 0.5f; // SlowTick ~0.5s

            // Обновляем силу сигнала
            float dist = Vector3.Distance(_playerTransform.position, atlasCorePosWorld);
            float newStrength = 0f;

            if (dist < maxSignalRange)
                newStrength = 1f - (dist / maxSignalRange);

            // Публикуем изменение силы
            if (Mathf.Abs(newStrength - _lastPublishedStrength) > StrengthEpsilon)
            {
                _currentStrength = newStrength;
                _lastPublishedStrength = newStrength;
                AtlasSignalEvents.RaiseStrengthChanged(newStrength);

                // Первое обнаружение
                if (!_signalEverDetected && newStrength >= detectionThreshold)
                {
                    _signalEverDetected = true;
                    AtlasSignalEvents.RaiseDetected(atlasCorePosWorld);
                    NotificationEvents.PushWarning("НЕИЗВЕСТНЫЙ СИГНАЛ ОБНАРУЖЕН — ИСТОЧНИК: НЕИЗВЕСТЕН");

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[AtlasSignal] Signal first detected! Strength: {newStrength:F2}");
#endif
                }

                // Шейдер
                if (publishToShader)
                    Shader.SetGlobalFloat(_ShaderSignalStrength, newStrength);
            }

            // Пульс
            if (_pulseTimer < pulsePeriodSeconds)
                return;

            _pulseTimer = 0f;
            float pulseIntensity = _currentStrength;
            AtlasSignalEvents.RaisePulse(pulseIntensity);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Time.time >= _nextSignalLogTime)
            {
                _nextSignalLogTime = Time.time + 5f;
                Debug.Log($"[AtlasSignal] PULSE — intensity: {pulseIntensity:F2} " +
                          $"(dist to core: {Vector3.Distance(_playerTransform.position, atlasCorePosWorld):F0}m)");
            }
#endif
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Вызывается когда игрок достигает ядра и расшифровывает сигнал.
        /// </summary>
        public void DecodeSignal(string messageId)
        {
            AtlasSignalEvents.RaiseDecoded(messageId);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[AtlasSignal] Signal decoded: {messageId}");
#endif
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE
        // ══════════════════════════════════════════════════════════

        private void ResolvePlayer()
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
                _playerTransform = player.transform;
        }

        // ══════════════════════════════════════════════════════════
        //  ISaveable
        // ══════════════════════════════════════════════════════════

        public void PopulateSaveData(SaveData data)
        {
            if (data == null) return;
            data.atlasSignalDetected = _signalEverDetected;
            data.atlasSignalPulseTimer = _pulseTimer;
        }

        public void LoadFromSaveData(SaveData data)
        {
            if (data == null) return;
            _signalEverDetected = data.atlasSignalDetected;
            _pulseTimer = data.atlasSignalPulseTimer;
        }
    }
}
