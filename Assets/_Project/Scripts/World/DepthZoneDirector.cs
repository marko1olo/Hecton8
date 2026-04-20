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
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Quest;
using Hecton8.UI;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
        private const string DepthZoneDataRoot = "Assets/_Project/Data/Lore/DepthZones";

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Zones ───────────────────────────────────")]
        [Tooltip("Все зоны глубины. Порядок не важен — сортируются по minDepth.")]
        [SerializeField] private DepthZoneProfile[] zones = new DepthZoneProfile[0];

        [Header("── References ──────────────────────────────")]
        [Tooltip("Система выживания для чтения глубины.")]
        [SerializeField] private HectonSurvivalSystem survivalSystem;

        [Header("â”€â”€ Notification Cadence â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Minimum delay between depth-zone enter HUD messages. Prevents boundary spam and early-route noise.")]
        [SerializeField, Min(0f)] private float zoneNotificationCooldown = 18f;

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
        private float _nextZoneNotificationTime;
        // COLD ALLOC: small per-zone message caches avoid string formatting in SlowTick transition path.
        private readonly DepthZoneProfile[] _cachedMessageZones = new DepthZoneProfile[32];
        private readonly string[] _cachedZoneEnterMessages = new string[32];
        private readonly string[] _cachedHullWarningMessages = new string[32];
        private readonly string[] _cachedZoneRouteCueMessages = new string[32];
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
            TryRegister();

            LocalizationManager.OnLanguageChanged += HandleLanguageChanged;
            ResolveSurvivalSystem();
        }

        private void OnDisable()
        {
            TryUnregister();

            LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
            _nextZoneNotificationTime = 0f;
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
                if (ShouldPublishZoneEnterNotification())
                {
                    NotificationEvents.PushInfo(GetZoneEnterMessage(newZone));
                    _nextZoneNotificationTime = Time.unscaledTime + Mathf.Max(0f, zoneNotificationCooldown);
                }

                LogZoneEntered(newZone.DisplayNameOrFallback, depth);
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

        private bool ShouldPublishZoneEnterNotification()
        {
            if (Time.unscaledTime < _nextZoneNotificationTime)
                return false;

            FirstHourDirector firstHourDirector = FirstHourDirector.Instance;
            if (firstHourDirector == null)
                return true;

            return firstHourDirector.IsMilestoneComplete(FirstHourMilestone.Orientation);
        }

        private bool ResolveSurvivalSystem()
        {
            if (survivalSystem != null)
                return true;

            if (!BootstrapState.TryGetCurrentPlayerTransform(out Transform playerTransform) ||
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
            LocalizationManager manager = LocalizationManager.Instance;
            for (int i = 0; i < maxCacheCount; i++)
            {
                DepthZoneProfile zone = zones[i];
                if (zone == null)
                    continue;

                zone.RebuildCache();
                string fallbackUnknown = manager != null
                    ? manager.GetOrFallback(manager.CurrentLanguage, LocalizationKeys.DEPTH_ZONE_UNKNOWN, "UNKNOWN ZONE")
                    : "UNKNOWN ZONE";
                string resolvedDisplayName = string.IsNullOrWhiteSpace(zone.DisplayNameOrFallback)
                    ? fallbackUnknown
                    : zone.DisplayNameOrFallback;
                string uppercaseZoneLabel = resolvedDisplayName.ToUpperInvariant();
                string zoneEnterLabel = manager != null
                    ? manager.GetFormatted(LocalizationKeys.DEPTH_ZONE_ENTER, uppercaseZoneLabel)
                    : "ZONE: " + uppercaseZoneLabel;
                string zoneRouteCue = ResolveZoneRouteCue(zone);
                _cachedMessageZones[_cachedMessageCount] = zone;
                _cachedZoneEnterMessages[_cachedMessageCount] = string.IsNullOrWhiteSpace(zone.cachedHudLabel)
                    ? zoneEnterLabel
                    : zone.cachedHudLabel;
                _cachedZoneRouteCueMessages[_cachedMessageCount] = string.IsNullOrWhiteSpace(zoneRouteCue)
                    ? _cachedZoneEnterMessages[_cachedMessageCount]
                    : _cachedZoneEnterMessages[_cachedMessageCount] + " — " + zoneRouteCue;
                _cachedHullWarningMessages[_cachedMessageCount] = manager != null
                    ? manager.GetFormatted(LocalizationKeys.DEPTH_ZONE_HULL_WARNING, zone.requiredHullTier)
                    : "WARNING: SUIT HULL IS NOT RATED FOR THIS DEPTH. TIER " + zone.requiredHullTier + ".";
                _cachedMessageCount++;
            }
        }

        private string GetZoneEnterMessage(DepthZoneProfile zone)
        {
            for (int i = 0; i < _cachedMessageCount; i++)
            {
                if (_cachedMessageZones[i] == zone)
                    return _cachedZoneRouteCueMessages[i];
            }

            return ResolveZoneEnterFallback(ResolveUnknownZoneLabel());
        }

        private string GetHullWarningMessage(DepthZoneProfile zone)
        {
            for (int i = 0; i < _cachedMessageCount; i++)
            {
                if (_cachedMessageZones[i] == zone)
                    return _cachedHullWarningMessages[i];
            }

            LocalizationManager manager = LocalizationManager.Instance;
            return manager != null
                ? manager.GetFormatted(LocalizationKeys.DEPTH_ZONE_HULL_WARNING, zone != null ? zone.requiredHullTier : 0)
                : "WARNING: SUIT HULL IS NOT RATED FOR THIS DEPTH.";
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogZoneEntered(string zoneDisplayName, float depth)
        {
            UnityEngine.Debug.Log($"[DepthZone] Entered: {zoneDisplayName} (depth: {depth:F0}m)");
        }

        private void HandleLanguageChanged(GameLanguage language)
        {
            RebuildZoneMessageCache();
        }

        private static string ResolveUnknownZoneLabel()
        {
            LocalizationManager manager = LocalizationManager.Instance;
            return manager != null
                ? manager.GetOrFallback(manager.CurrentLanguage, LocalizationKeys.DEPTH_ZONE_UNKNOWN, "UNKNOWN ZONE")
                : "UNKNOWN ZONE";
        }

        private static string ResolveZoneEnterFallback(string zoneLabel)
        {
            LocalizationManager manager = LocalizationManager.Instance;
            return manager != null
                ? manager.GetFormatted(LocalizationKeys.DEPTH_ZONE_ENTER, zoneLabel)
                : "ZONE: " + zoneLabel;
        }

        private static string ResolveZoneRouteCue(DepthZoneProfile zone)
        {
            if (zone == null)
                return null;

            if (!string.IsNullOrWhiteSpace(zone.DescriptionOrFallback))
                return zone.DescriptionOrFallback.ToUpperInvariant();

            if (zone.isThermal)
                return "THERMAL WATER DISTORTS COLOR AND RANGE. TRUST YOUR RETURN LINE, NOT THE GLOW.";

            if (zone.hasCaves)
                return "CAVES CUT READABILITY. HOLD A CLEAN EXIT VECTOR BEFORE YOU COMMIT.";

            if (zone.dangerLevel >= 0.75f)
                return "HIGH-PRESSURE WATER. ROUTE MEMORY MATTERS MORE THAN GREED HERE.";

            if (zone.dangerLevel >= 0.45f)
                return "VISIBILITY FALLS FAST. KEEP THE SAFER SILHOUETTE IN MEMORY.";

            return "READ THE SHELVES, NOT THE NOISE. SAFE WATER IS FOR RESET, NOT FORWARD PROGRESS.";
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

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying)
                return;

            TryAutoPopulateZones();
            RebuildZoneMessageCache();
        }

        private void TryAutoPopulateZones()
        {
            if (zones != null && zones.Length > 0)
                return;

            string[] guids = AssetDatabase.FindAssets("t:DepthZoneProfile", new[] { DepthZoneDataRoot });
            if (guids == null || guids.Length <= 0)
                return;

            DepthZoneProfile[] loadedZones = new DepthZoneProfile[guids.Length];
            int loadedCount = 0;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                DepthZoneProfile zone = AssetDatabase.LoadAssetAtPath<DepthZoneProfile>(path);
                if (zone == null)
                    continue;

                loadedZones[loadedCount] = zone;
                loadedCount++;
            }

            if (loadedCount <= 0)
                return;

            if (loadedCount != loadedZones.Length)
            {
                DepthZoneProfile[] compactZones = new DepthZoneProfile[loadedCount];
                System.Array.Copy(loadedZones, compactZones, loadedCount);
                loadedZones = compactZones;
            }

            SortZonesByMinDepth(loadedZones);
            zones = loadedZones;
            EditorUtility.SetDirty(this);
        }

        private static void SortZonesByMinDepth(DepthZoneProfile[] authoredZones)
        {
            if (authoredZones == null || authoredZones.Length <= 1)
                return;

            for (int i = 0; i < authoredZones.Length - 1; i++)
            {
                int bestIndex = i;
                float bestDepth = authoredZones[i] != null ? authoredZones[i].minDepth : float.MaxValue;
                for (int j = i + 1; j < authoredZones.Length; j++)
                {
                    float candidateDepth = authoredZones[j] != null ? authoredZones[j].minDepth : float.MaxValue;
                    if (candidateDepth < bestDepth)
                    {
                        bestIndex = j;
                        bestDepth = candidateDepth;
                    }
                }

                if (bestIndex == i)
                    continue;

                DepthZoneProfile swap = authoredZones[i];
                authoredZones[i] = authoredZones[bestIndex];
                authoredZones[bestIndex] = swap;
            }
        }
#endif
    }
}
