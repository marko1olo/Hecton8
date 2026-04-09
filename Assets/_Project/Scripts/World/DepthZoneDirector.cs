// ============================================================================
// HECTON-8 — DepthZoneDirector.cs
// Определяет текущую зону игрока по глубине и публикует события.
//
// РОЛЬ:
//   • Отслеживает глубину игрока через HectonSurvivalSystem.
//   • При смене зоны: публикует событие, регистрирует discovery,
//     обновляет QuestManager, уведомляет HUD.
//   • Проверяет требования к тиру корпуса — предупреждает если
//     игрок ныряет глубже допустимого.
//
// ZERO GC:
//   • ISlowTickable — проверка зоны раз в 0.5с.
//   • Никаких new/LINQ в SlowTick.
// ============================================================================

using System;
using System.Diagnostics;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Quest;
using Hecton8.UI;
using UnityEngine;

namespace Hecton8.World
{
    public static class DepthZoneEvents
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            OnZoneEntered = null;
            OnZoneExited = null;
        }

        /// <summary>Вход в новую зону. DepthZoneProfile: новая зона.</summary>
        public static event Action<DepthZoneProfile> OnZoneEntered;

        /// <summary>Выход из зоны. DepthZoneProfile: покинутая зона.</summary>
        public static event Action<DepthZoneProfile> OnZoneExited;

        public static void RaiseZoneEntered(DepthZoneProfile zone) => OnZoneEntered?.Invoke(zone);
        public static void RaiseZoneExited(DepthZoneProfile zone)  => OnZoneExited?.Invoke(zone);
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-105)]
    public sealed class DepthZoneDirector : MonoBehaviour, ISlowTickable
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Zones ───────────────────────────────────")]
        [Tooltip("Все зоны глубины. Порядок не важен — сортируются по minDepth.")]
        [SerializeField] private DepthZoneProfile[] zones = new DepthZoneProfile[0];

        [Header("── References ──────────────────────────────")]
        [Tooltip("Система выживания для чтения глубины.")]
        [SerializeField] private HectonSurvivalSystem survivalSystem;

        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        public static DepthZoneDirector Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => Instance = null;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private DepthZoneProfile _currentZone;
        private bool _registered;
        private bool _hullWarningShown;
        // COLD ALLOC: small per-zone message caches avoid string formatting in SlowTick transition path.
        private readonly DepthZoneProfile[] _cachedMessageZones = new DepthZoneProfile[32];
        private readonly string[] _cachedZoneEnterMessages = new string[32];
        private readonly string[] _cachedHullWarningMessages = new string[32];
        private int _cachedMessageCount;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        public DepthZoneProfile CurrentZone => _currentZone;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            RebuildZoneMessageCache();
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
        }

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable
        // ══════════════════════════════════════════════════════════

        public void SlowTick()
        {
            if (survivalSystem == null && !ResolveSurvivalSystem())
                return;

            if (survivalSystem == null || zones == null || zones.Length == 0)
                return;

            float depth = survivalSystem.Depth;

            // Обновляем QuestManager
            QuestManager questManager = QuestManager.Instance;
            if (questManager != null)
                questManager.UpdateDepth(depth);

            // Находим текущую зону
            DepthZoneProfile newZone = FindZoneForDepth(depth);

            if (newZone == _currentZone)
            {
                // Проверяем предупреждение о корпусе
                CheckHullWarning(newZone);
                return;
            }

            // Смена зоны
            DepthZoneProfile oldZone = _currentZone;
            _currentZone = newZone;
            _hullWarningShown = false;

            if (oldZone != null)
                DepthZoneEvents.RaiseZoneExited(oldZone);

            if (newZone != null)
            {
                DepthZoneEvents.RaiseZoneEntered(newZone);

                // Регистрируем discovery
                if (!string.IsNullOrEmpty(newZone.discoveryId))
                    NarrativeEvents.RaiseDiscoveryMade(newZone.discoveryId);

                // HUD уведомление
                NotificationEvents.PushInfo(GetZoneEnterMessage(newZone));

                LogZoneEntered(newZone.displayName, depth);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE
        // ══════════════════════════════════════════════════════════

        private DepthZoneProfile FindZoneForDepth(float depth)
        {
            DepthZoneProfile best = null;
            float bestMin = -1f;

            for (int i = 0; i < zones.Length; i++)
            {
                DepthZoneProfile z = zones[i];
                if (z == null) continue;
                if (!z.ContainsDepth(depth)) continue;

                // Берём зону с наибольшим minDepth (наиболее специфичную)
                if (z.minDepth > bestMin)
                {
                    bestMin = z.minDepth;
                    best = z;
                }
            }

            return best;
        }

        private void CheckHullWarning(DepthZoneProfile zone)
        {
            if (zone == null || _hullWarningShown) return;

            SuitUpgradeManager upgradeManager = SuitUpgradeManager.Instance;
            if (upgradeManager == null) return;

            if (upgradeManager.CurrentHullTier < zone.requiredHullTier)
            {
                _hullWarningShown = true;
                NotificationEvents.PushWarning(GetHullWarningMessage(zone));
            }
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

        private void RebuildZoneMessageCache()
        {
            _cachedMessageCount = 0;

            if (zones == null || zones.Length == 0)
                return;

            int maxCacheCount = Mathf.Min(zones.Length, _cachedMessageZones.Length);
            for (int i = 0; i < maxCacheCount; i++)
            {
                DepthZoneProfile zone = zones[i];
                if (zone == null)
                    continue;

                string displayName = string.IsNullOrEmpty(zone.displayName) ? "НЕИЗВЕСТНАЯ ЗОНА" : zone.displayName.ToUpperInvariant();
                _cachedMessageZones[_cachedMessageCount] = zone;
                _cachedZoneEnterMessages[_cachedMessageCount] = "ЗОНА: " + displayName;
                _cachedHullWarningMessages[_cachedMessageCount] =
                    "ПРЕДУПРЕЖДЕНИЕ: КОРПУС СКАФАНДРА НЕ РАССЧИТАН НА ЭТУ ГЛУБИНУ. ТРЕБУЕТСЯ ТИР " +
                    zone.requiredHullTier + ".";
                _cachedMessageCount++;
            }
        }

        private string GetZoneEnterMessage(DepthZoneProfile zone)
        {
            for (int i = 0; i < _cachedMessageCount; i++)
            {
                if (_cachedMessageZones[i] == zone)
                    return _cachedZoneEnterMessages[i];
            }

            return "ЗОНА: НЕИЗВЕСТНАЯ ЗОНА";
        }

        private string GetHullWarningMessage(DepthZoneProfile zone)
        {
            for (int i = 0; i < _cachedMessageCount; i++)
            {
                if (_cachedMessageZones[i] == zone)
                    return _cachedHullWarningMessages[i];
            }

            return "ПРЕДУПРЕЖДЕНИЕ: КОРПУС СКАФАНДРА НЕ РАССЧИТАН НА ЭТУ ГЛУБИНУ.";
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogZoneEntered(string zoneDisplayName, float depth)
        {
            UnityEngine.Debug.Log($"[DepthZone] Entered: {zoneDisplayName} (depth: {depth:F0}m)");
        }
    }
}
