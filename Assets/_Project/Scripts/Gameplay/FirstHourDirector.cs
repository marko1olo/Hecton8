// ============================================================================
// HECTON-8 — FirstHourDirector.cs
// Режиссура первого часа игры.
//
// ЛОР (лор1 — Психологический arc первых двух часов):
//   Минута 0-5:    Дезориентация → Ориентация
//   Минута 5-15:   Любопытство без страха (мелководье безопасно)
//   Минута 15-25:  Первая тревога (рука из-под обломка, гул снизу)
//   Минута 25-40:  Компетентность (первый крафт)
//   Минута 40-50:  Удар по уверенности (ТЕНЬ — большая, быстрая, слева)
//   Минута 50-70:  Осторожность (игрок двигается иначе)
//   Минута 70-90:  Маленькая победа (нашёл модуль)
//   Минута 90-120: Предвкушение (гул приближается)
//
// МЕХАНИКА:
//   • Отслеживает время сессии и прогресс.
//   • Публикует события для Director AI и нарративных систем.
//   • Одноразовые события (не повторяются после первого раза).
//   • ISaveable: сохраняет прогресс первого часа.
//
// ZERO GC:
//   • Битовая маска для отслеживания выполненных событий.
//   • ISlowTickable.
// ============================================================================

using System;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.SaveSystem;
using Hecton8.UI;
using UnityEngine;

namespace Hecton8.Gameplay
{
    public enum FirstHourMilestone
    {
        Orientation     = 0,   // Мин 0-5: ориентация
        FirstAnxiety    = 1,   // Мин 15-25: первая тревога (гул)
        FirstCraft      = 2,   // Мин 25-40: первый крафт
        TheShadow       = 3,   // Мин 40-50: ТЕНЬ
        FirstModule     = 4,   // Мин 70-90: первый модуль колонии
        HumCloser       = 5    // Мин 90-120: гул приближается
    }

    public static class FirstHourEvents
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => OnMilestoneReached = null;

        /// <summary>Достигнут milestone первого часа.</summary>
        public static event Action<FirstHourMilestone> OnMilestoneReached;

        public static void RaiseMilestone(FirstHourMilestone milestone)
            => OnMilestoneReached?.Invoke(milestone);
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-65)]
    public sealed class FirstHourDirector : MonoBehaviour, ISaveable, ISlowTickable
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Timing (seconds) ────────────────────────")]
        [SerializeField] private float orientationTime   = 300f;   // 5 мин
        [SerializeField] private float firstAnxietyTime  = 900f;   // 15 мин
        [SerializeField] private float shadowTime        = 2400f;  // 40 мин
        [SerializeField] private float firstModuleTime   = 4200f;  // 70 мин
        [SerializeField] private float humCloserTime     = 5400f;  // 90 мин

        [Header("── Shadow Trigger ──────────────────────────")]
        [Tooltip("Минимальная глубина для появления тени (метры).")]
        [SerializeField] private float shadowMinDepth = 30f;

        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        public static FirstHourDirector Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => Instance = null;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private float _sessionTime;
        private int   _completedMilestones; // битовая маска
        private bool  _registered;
        private HectonSurvivalSystem _survivalSystem;

        // ══════════════════════════════════════════════════════════
        //  ISaveable
        // ══════════════════════════════════════════════════════════

        public int SavePriority => 13;
        public int LoadPriority => 13;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        public float SessionTime => _sessionTime;
        public bool IsFirstHourComplete => _sessionTime >= humCloserTime;

        public bool IsMilestoneComplete(FirstHourMilestone m)
            => (_completedMilestones & (1 << (int)m)) != 0;

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

            if (SaveManager.Instance != null)
                SaveManager.Instance.Register(this);

            ResolveSurvivalSystem();

            // Слушаем первый крафт
            NarrativeEvents.OnDiscoveryMade += HandleDiscovery;
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

            NarrativeEvents.OnDiscoveryMade -= HandleDiscovery;
        }

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable
        // ══════════════════════════════════════════════════════════

        public void SlowTick()
        {
            if (IsFirstHourComplete) return;

            _sessionTime += 0.5f;
            ResolveSurvivalSystem();

            float depth = _survivalSystem != null ? _survivalSystem.Depth : 0f;

            CheckMilestone(FirstHourMilestone.Orientation,  _sessionTime >= orientationTime);
            CheckMilestone(FirstHourMilestone.FirstAnxiety, _sessionTime >= firstAnxietyTime);

            // Тень — только если игрок под водой на нужной глубине
            CheckMilestone(FirstHourMilestone.TheShadow,
                _sessionTime >= shadowTime && depth >= shadowMinDepth);

            CheckMilestone(FirstHourMilestone.FirstModule,  _sessionTime >= firstModuleTime);
            CheckMilestone(FirstHourMilestone.HumCloser,    _sessionTime >= humCloserTime);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE
        // ══════════════════════════════════════════════════════════

        private void CheckMilestone(FirstHourMilestone milestone, bool condition)
        {
            if (!condition) return;
            if (IsMilestoneComplete(milestone)) return;

            _completedMilestones |= (1 << (int)milestone);
            TriggerMilestone(milestone);
        }

        private void TriggerMilestone(FirstHourMilestone milestone)
        {
            FirstHourEvents.RaiseMilestone(milestone);

            switch (milestone)
            {
                case FirstHourMilestone.Orientation:
                    // Ориентация завершена — мир понятен
                    break;

                case FirstHourMilestone.FirstAnxiety:
                    // Первая тревога — гул снизу
                    NotificationEvents.PushInfo("ГУЛ — ИСТОЧНИК: НЕИЗВЕСТЕН. ГЛУБИНА: НЕИЗВЕСТНА.");
                    break;

                case FirstHourMilestone.TheShadow:
                    // ТЕНЬ — большая, быстрая, слева
                    // Director AI получает narrative bonus (снижение tension после страха)
                    NarrativeEvents.RaiseDiscoveryMade("first_hour_shadow_event");
                    break;

                case FirstHourMilestone.FirstModule:
                    // Первый модуль колонии обнаружен
                    NarrativeEvents.RaiseDiscoveryMade("first_colony_module_spotted");
                    break;

                case FirstHourMilestone.HumCloser:
                    // Гул приближается
                    NotificationEvents.PushWarning("ГУЛ УСИЛИВАЕТСЯ. ИСТОЧНИК ПРИБЛИЖАЕТСЯ.");
                    break;
            }

            LogMilestoneTriggered(milestone, _sessionTime);
        }

        private void HandleDiscovery(string discoveryId)
        {
            // Первый крафт — любое discovery в первые 40 минут
            if (!IsMilestoneComplete(FirstHourMilestone.FirstCraft) &&
                _sessionTime >= 1200f && _sessionTime <= 2400f)
            {
                CheckMilestone(FirstHourMilestone.FirstCraft, true);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  ISaveable
        // ══════════════════════════════════════════════════════════

        private bool ResolveSurvivalSystem()
        {
            if (_survivalSystem != null)
                return true;

            if (!SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) ||
                playerTransform == null)
            {
                return false;
            }

            return playerTransform.TryGetComponent(out _survivalSystem);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogMilestoneTriggered(FirstHourMilestone milestone, float sessionTime)
        {
            Debug.Log($"[FirstHour] Milestone: {milestone} (t={sessionTime:F0}s)");
        }

        public void PopulateSaveData(SaveData data)
        {
            if (data == null) return;
            data.firstHourSessionTime = _sessionTime;
            data.firstHourMilestones  = _completedMilestones;
        }

        public void LoadFromSaveData(SaveData data)
        {
            if (data == null) return;
            _sessionTime          = data.firstHourSessionTime;
            _completedMilestones  = data.firstHourMilestones;
        }
    }
}
