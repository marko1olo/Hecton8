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
using Hecton8.AtlasSignal;
using Hecton8.Bootstrap;
using Hecton8.Crafting;
using Hecton8.Core;
using Hecton8.Environment;
using Hecton8.Interaction;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Narrative;
using Hecton8.Quest;
using Hecton8.SaveSystem;
using Hecton8.UI;
using Hecton8.World;
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
        [SerializeField] private float shadowTime        = 2400f;  // 40 мин
        [SerializeField] private float firstModuleTime   = 4200f;  // 70 мин

        [Header("── Shadow Trigger ──────────────────────────")]
        [Tooltip("Минимальная глубина для появления тени (метры).")]
        [SerializeField] private float shadowMinDepth = 30f;

        [Header("── Early Goal Hooks ─────────────────────────")]
        [Tooltip("Quest that represents successful arrival/orientation.")]
        [SerializeField] private string arrivalQuestId = "quest_arrival";

        [Tooltip("Quest that should become the next clear early-game material goal.")]
        [SerializeField] private string firstResourceQuestId = "quest_copper_sample";

        [Tooltip("Item ID that proves the first resource goal was already solved in an older save.")]
        [SerializeField] private string firstResourceItemId = "Data_Copper";

        [Tooltip("Quest that should take over once the player secures the first core material.")]
        [SerializeField] private string firstDepthQuestId = "quest_first_breath";

        [Tooltip("Narrative discovery that counts as a real ruined-colony/module contact.")]
        [SerializeField] private string firstModuleZoneDiscoveryId = "zone_drowned_factories";

        [Header("── Retention Nudges ─────────────────────────")]
        [Tooltip("When to remind the player about the first core resource if they are still drifting.")]
        [SerializeField] private float firstResourceReminderTime = 480f;

        [Tooltip("When to remind the player that the next real step is to go deeper.")]
        [SerializeField] private float firstDepthReminderTime = 1080f;

        [Tooltip("When to remind the player that shallow safety is no longer the real progression route and the next meaningful contact is a module or ruin.")]
        [SerializeField] private float firstModuleReminderTime = 2100f;

        [Header("── Soft Guidance ───────────────────────────")]
        [Tooltip("Minimum delay between contextual onboarding nudges.")]
        [SerializeField] private float contextualGuidanceCooldown = 24f;

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
        private bool  _firstModuleHintIssued;
        private bool  _firstResourceReminderIssued;
        private bool  _firstDepthReminderIssued;
        private bool  _firstModuleReminderIssued;
        private bool  _starterResourcesZoneHintIssued;
        private bool  _starterFabricationFallbackHintIssued;
        private bool  _starterBackslideGuidanceIssued;
        private bool  _firstReturnLoreHintIssued;
        private bool  _deeperRouteZoneHintIssued;
        private bool  _moduleRouteHintIssued;
        private bool  _hasLoreRouteContact;
        private float _nextContextualGuidanceTime;
        private WorldZoneDirector _worldZoneDirector;
        private BiomeMatrixDirector _biomeMatrixDirector;
        private WorldZoneAnchor _lastObservedZone;
        private bool _lastContextResourceCompleted;
        private bool _lastContextDepthCompleted;
        private bool _lastContextLoreContact;
        private HectonSurvivalSystem _survivalSystem;

        private const float MinEarnedOrientationTime = 75f;
        private const string MsgResourceShelfRead =
            "ДЕРЖИСЬ ЧИТАЕМОЙ КРОМКИ. ПЕРВАЯ МЕДЬ ЧАЩЕ СИДИТ НИЖЕ И СБОКУ ОТ САМОЙ БЕЗОПАСНОЙ ПОЛКИ.";
        private const string MsgFabricationFallback =
            "УЗЕЛ ДАЁТ ПЕРЕДЫШКУ, НЕ ОТВЕТ. ВЫХОДИ СНОВА — СИЛЬНЫЙ РАННИЙ ВЫХОД ЛЕЖИТ ЧУТЬ ГЛУБЖЕ.";
        private const string MsgReturnLoreRelay =
            "В СЛУЖЕБНЫХ УЗЛАХ МОГЛИ ОСТАТЬСЯ ЗАПИСИ И МЕТКИ. ПРОВЕРЯЙ ТЕРМИНАЛЫ И БОКОВЫЕ СТОЙКИ, НЕ ТОЛЬКО РЕСУРСЫ.";
        private const string MsgDeeperRouteRead =
            "НИЖЕ ВАЖНЕЕ НЕ ЖАДНОСТЬ, А МАРШРУТ. ДЕРЖИ В ПАМЯТИ СИЛУЭТ ВЫХОДА И СПОКОЙНЫЙ КАРМАН.";
        private const string MsgModuleRouteRead =
            "ТЕПЕРЬ ИЩИ НЕ РОССЫПЬ, А СЛЕД. РУИНЫ, МОДУЛИ И СЛУЖЕБНЫЕ ОСТАНОВКИ ДАДУТ НАСТОЯЩИЙ ВЕКТОР.";
        private const string MsgStarterBackslideRead =
            "МЕЛКОВОДЬЕ ТЕПЕРЬ ДАЁТ ТЕБЕ ПЕРЕДЫШКУ, НЕ ПРОГРЕСС. СОБЕРИСЬ И ВЕРНИСЬ К ГЛУБИННОМУ МАРШРУТУ.";

        // ══════════════════════════════════════════════════════════
        //  ISaveable
        // ══════════════════════════════════════════════════════════

        public int SavePriority => 13;
        public int LoadPriority => 13;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        public float SessionTime => _sessionTime;
        public bool IsFirstHourComplete => IsMilestoneComplete(FirstHourMilestone.HumCloser);

        public bool IsMilestoneComplete(FirstHourMilestone m)
            => (_completedMilestones & (1 << (int)m)) != 0;

        /// <summary>
        /// Registers a confirmed service-relay route contact for first-hour pacing.
        /// </summary>
        public void RegisterServiceRelayRouteContact()
        {
            _hasLoreRouteContact = true;
        }

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

            if (SaveManager.Instance != null)
                SaveManager.Instance.Register(this);

            ResolveSurvivalSystem();
            ResolveWorldContext(force: true);
            SynchronizeContextFromRuntimeSystems();

            CraftingEvents.OnCraftCompleted += HandleCraftCompleted;
            NarrativeEvents.OnDiscoveryMade += HandleDiscovery;
            QuestEvents.OnQuestCompleted += HandleQuestCompleted;
            ScanEvents.OnEntryDiscovered += HandleScanEntryDiscovered;
            InteractionEvents.OnItemCollected += HandleItemCollected;
            AudioLogEvents.OnLogDiscovered += HandleAudioLogDiscovered;
        }

        private void OnDisable()
        {
            TryUnregister();

            if (SaveManager.Instance != null)
                SaveManager.Instance.Unregister(this);

            CraftingEvents.OnCraftCompleted -= HandleCraftCompleted;
            NarrativeEvents.OnDiscoveryMade -= HandleDiscovery;
            QuestEvents.OnQuestCompleted -= HandleQuestCompleted;
            ScanEvents.OnEntryDiscovered -= HandleScanEntryDiscovered;
            InteractionEvents.OnItemCollected -= HandleItemCollected;
            AudioLogEvents.OnLogDiscovered -= HandleAudioLogDiscovered;

            _lastObservedZone = null;
            _nextContextualGuidanceTime = 0f;
            _lastContextResourceCompleted = false;
            _lastContextDepthCompleted = false;
            _lastContextLoreContact = false;
        }

        private void Start()
        {
            TryRegister();
            SaveManager.Instance?.Register(this);
            ResolveSurvivalSystem();
            ResolveWorldContext(force: true);
            SynchronizeContextFromRuntimeSystems();
        }

        private void TryRegister()
        {
            if (_registered)
                return;

            GameTickManager gameTickManager = GameTickManager.Instance;
            if (gameTickManager == null)
                return;

            gameTickManager.Register(this);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GameTickManager gameTickManager = GameTickManager.Instance;
            if (gameTickManager != null)
                gameTickManager.Unregister(this);

            _registered = false;
        }

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable
        // ══════════════════════════════════════════════════════════

        public void SlowTick()
        {
            if (IsFirstHourComplete) return;

            _sessionTime += 0.5f;
            ResolveSurvivalSystem();
            ResolveWorldContext();

            float depth = _survivalSystem != null ? _survivalSystem.Depth : 0f;
            WorldZoneAnchor currentZone = _worldZoneDirector != null ? _worldZoneDirector.CurrentZone : null;
            int currentDepthTier = _biomeMatrixDirector != null ? _biomeMatrixDirector.CurrentDepthTier : 1;
            int atlasRevealStage = GetCurrentAtlasRevealStage();

            CheckMilestone(FirstHourMilestone.Orientation,
                _sessionTime >= orientationTime || IsOrientationEarned(currentZone));
            CheckMilestone(
                FirstHourMilestone.FirstAnxiety,
                ShouldTriggerFirstAnxiety(atlasRevealStage));

            // Тень — только если игрок под водой на нужной глубине
            CheckMilestone(FirstHourMilestone.TheShadow,
                _sessionTime >= shadowTime && depth >= shadowMinDepth);

            if (!_firstModuleHintIssued &&
                !_moduleRouteHintIssued &&
                !_firstModuleReminderIssued &&
                !IsMilestoneComplete(FirstHourMilestone.FirstModule) &&
                _sessionTime >= firstModuleTime &&
                currentDepthTier > 1 &&
                QuestManager.Instance != null &&
                QuestManager.Instance.IsCompleted(firstDepthQuestId))
            {
                _firstModuleHintIssued = true;
                _firstModuleReminderIssued = true;
                NotificationEvents.PushInfo("СКАНИРУЙ РУИНЫ И МОДУЛИ. ЗДЕСЬ ЕСТЬ ЧТО-ТО ЦЕЛОЕ.");
            }

            TryIssueRetentionNudges();
            TryIssueContextualGuidance();

            CheckMilestone(
                FirstHourMilestone.HumCloser,
                ShouldTriggerHumCloser(atlasRevealStage));
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
                    CompleteQuest(arrivalQuestId);
                    ActivateQuest(firstResourceQuestId);
                    TryAdvanceFirstResourceGoalFromRuntimeInventory();
                    break;

                case FirstHourMilestone.TheShadow:
                    // ТЕНЬ — большая, быстрая, слева
                    // Director AI получает narrative bonus (снижение tension после страха)
                    NarrativeEvents.RaiseDiscoveryMade("first_hour_shadow_event");
                    break;

                case FirstHourMilestone.FirstModule:
                    NarrativeEvents.RaiseDiscoveryMade("first_colony_module_spotted");
                    break;

            }

            LogMilestoneTriggered(milestone, _sessionTime);
        }

        private void HandleCraftCompleted(ItemData resultItem)
        {
            if (resultItem == null)
                return;

            CheckMilestone(FirstHourMilestone.FirstCraft,
                !IsMilestoneComplete(FirstHourMilestone.FirstCraft));
        }

        private void HandleDiscovery(string discoveryId)
        {
            if (string.IsNullOrEmpty(discoveryId))
                return;

            EmergencyServiceRelayDirector relayDirector = EmergencyServiceRelayDirector.Instance;
            if (relayDirector != null && relayDirector.IsRelayDiscoveryId(discoveryId))
                _hasLoreRouteContact = true;

            if (!IsMilestoneComplete(FirstHourMilestone.FirstModule) &&
                string.Equals(discoveryId, firstModuleZoneDiscoveryId, StringComparison.Ordinal))
            {
                CheckMilestone(FirstHourMilestone.FirstModule, true);
            }
        }

        private void HandleScanEntryDiscovered(string entryId, string title, string category, string summary)
        {
            if (IsMilestoneComplete(FirstHourMilestone.FirstModule) ||
                string.IsNullOrEmpty(entryId))
            {
                return;
            }

            if (entryId.StartsWith("module.", StringComparison.Ordinal))
                CheckMilestone(FirstHourMilestone.FirstModule, true);
        }

        private void HandleQuestCompleted(string questId)
        {
            if (string.Equals(questId, firstDepthQuestId, StringComparison.Ordinal))
            {
                _firstDepthReminderIssued = true;
                return;
            }

            if (!string.Equals(questId, firstResourceQuestId, StringComparison.Ordinal))
                return;

            _firstResourceReminderIssued = true;
            ActivateQuest(firstDepthQuestId);
        }

        private void HandleAudioLogDiscovered(string logId)
        {
            if (!string.IsNullOrEmpty(logId))
                _hasLoreRouteContact = true;
        }

        private void HandleItemCollected(ItemData item, int quantity, Transform interactor)
        {
            if (!IsMilestoneComplete(FirstHourMilestone.Orientation) ||
                item == null ||
                !string.Equals(item.name, firstResourceItemId, StringComparison.Ordinal))
            {
                return;
            }

            CompleteQuest(firstResourceQuestId);
            _firstResourceReminderIssued = true;
            ActivateQuest(firstDepthQuestId);
            _firstDepthReminderIssued = false;
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

        private void ResolveWorldContext(bool force = false)
        {
            if (force || _worldZoneDirector == null)
                _worldZoneDirector = WorldZoneDirector.ActiveRuntimeInstance;

            if (force || _biomeMatrixDirector == null)
                _biomeMatrixDirector = BiomeMatrixDirector.ActiveRuntimeInstance;
        }

        private void SynchronizeContextFromRuntimeSystems()
        {
            AudioLogSystem audioLogSystem = AudioLogSystem.Instance;
            if (audioLogSystem != null && audioLogSystem.DiscoveredCount > 0)
                _hasLoreRouteContact = true;

            EmergencyServiceRelayDirector relayDirector = EmergencyServiceRelayDirector.Instance;
            if (relayDirector != null && relayDirector.HasDiscoveredRelayInDrivenChain())
                _hasLoreRouteContact = true;
        }

        private bool IsOrientationEarned(WorldZoneAnchor currentZone)
        {
            if (_sessionTime < MinEarnedOrientationTime || currentZone == null)
                return false;

            if (currentZone.RouteCritical)
                return true;

            switch (currentZone.Kind)
            {
                case WorldZoneAnchor.ZoneKind.Resources:
                case WorldZoneAnchor.ZoneKind.Fabrication:
                case WorldZoneAnchor.ZoneKind.Navigation:
                case WorldZoneAnchor.ZoneKind.Progression:
                case WorldZoneAnchor.ZoneKind.Service:
                    return true;
                default:
                    return false;
            }
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
            _firstModuleHintIssued = _sessionTime >= firstModuleTime ||
                                     IsMilestoneComplete(FirstHourMilestone.FirstModule);
            _firstResourceReminderIssued = false;
            _firstDepthReminderIssued = false;
            _firstModuleReminderIssued = false;
            _starterResourcesZoneHintIssued = false;
            _starterFabricationFallbackHintIssued = false;
            _starterBackslideGuidanceIssued = false;
            _firstReturnLoreHintIssued = false;
            _deeperRouteZoneHintIssued = false;
            _moduleRouteHintIssued = false;
            _hasLoreRouteContact = false;
            _nextContextualGuidanceTime = 0f;
            _lastObservedZone = null;
            _lastContextResourceCompleted = false;
            _lastContextDepthCompleted = false;
            _lastContextLoreContact = false;
            SynchronizeContextFromRuntimeSystems();
            SynchronizeAtlasMilestonesFromRuntime();
            SynchronizeEarlyQuestState();
            SynchronizeFirstResourceQuestFromSaveData(data);
        }

        private void ActivateQuest(string questId)
        {
            if (string.IsNullOrEmpty(questId))
                return;

            QuestManager questManager = QuestManager.Instance;
            if (questManager == null)
                return;

            if (!questManager.IsActive(questId) && !questManager.IsCompleted(questId))
                questManager.ActivateQuest(questId);
        }

        private void CompleteQuest(string questId)
        {
            if (string.IsNullOrEmpty(questId))
                return;

            QuestManager questManager = QuestManager.Instance;
            if (questManager == null)
                return;

            if (!questManager.IsActive(questId) && !questManager.IsCompleted(questId))
                questManager.ActivateQuest(questId);

            if (questManager.IsActive(questId))
                questManager.CompleteQuest(questId);
        }

        private void SynchronizeEarlyQuestState()
        {
            if (!IsMilestoneComplete(FirstHourMilestone.Orientation))
                return;

            CompleteQuest(arrivalQuestId);
            ActivateQuest(firstResourceQuestId);
            TryAdvanceFirstResourceGoalFromRuntimeInventory();

            QuestManager questManager = QuestManager.Instance;
            if (questManager != null && questManager.IsCompleted(firstResourceQuestId))
            {
                _firstResourceReminderIssued = true;
                ActivateQuest(firstDepthQuestId);
            }

            if (questManager != null && questManager.IsCompleted(firstDepthQuestId))
                _firstDepthReminderIssued = true;

            if (_hasLoreRouteContact)
                _firstReturnLoreHintIssued = true;

            if (IsMilestoneComplete(FirstHourMilestone.FirstModule))
                _firstModuleReminderIssued = true;
        }

        private void SynchronizeAtlasMilestonesFromRuntime()
        {
            int atlasRevealStage = GetCurrentAtlasRevealStage();
            if (atlasRevealStage >= 1)
                _completedMilestones |= 1 << (int)FirstHourMilestone.FirstAnxiety;

            if (atlasRevealStage >= 2)
                _completedMilestones |= 1 << (int)FirstHourMilestone.HumCloser;
        }

        private int GetCurrentAtlasRevealStage()
        {
            AtlasSignalSystem atlasSignalSystem = AtlasSignalSystem.Instance;
            return atlasSignalSystem != null ? atlasSignalSystem.CurrentRevealStage : 0;
        }

        private static bool ShouldTriggerFirstAnxiety(int atlasRevealStage)
        {
            return atlasRevealStage >= 1;
        }

        private static bool ShouldTriggerHumCloser(int atlasRevealStage)
        {
            return atlasRevealStage >= 2;
        }

        private void SynchronizeFirstResourceQuestFromSaveData(SaveData data)
        {
            if (data == null ||
                !IsMilestoneComplete(FirstHourMilestone.Orientation) ||
                !SaveInventoryContainsItem(data.inventory, firstResourceItemId))
            {
                return;
            }

            CompleteQuest(firstResourceQuestId);
            _firstResourceReminderIssued = true;
            ActivateQuest(firstDepthQuestId);
            _firstDepthReminderIssued = false;
        }

        private static bool SaveInventoryContainsItem(InventoryDTO inventory, string itemId)
        {
            if (string.IsNullOrEmpty(itemId) ||
                inventory.cells == null ||
                inventory.cellCount <= 0)
            {
                return false;
            }

            int cellCount = Mathf.Min(inventory.cellCount, inventory.cells.Length);
            for (int i = 0; i < cellCount; i++)
            {
                if (string.Equals(inventory.cells[i].itemId, itemId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private void TryAdvanceFirstResourceGoalFromRuntimeInventory()
        {
            if (string.IsNullOrEmpty(firstResourceItemId) ||
                !TryGetRuntimeInventory(out PlayerInventory inventory) ||
                inventory == null)
            {
                return;
            }

            InventoryGrid grid = inventory.Grid;
            if (grid == null)
                return;

            int columns = grid.Columns;
            int rows = grid.Rows;
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    ItemData item = grid.GetCell(x, y);
                    if (item == null)
                        continue;

                    if (!string.Equals(item.name, firstResourceItemId, StringComparison.Ordinal))
                        continue;

                    CompleteQuest(firstResourceQuestId);
                    _firstResourceReminderIssued = true;
                    ActivateQuest(firstDepthQuestId);
                    _firstDepthReminderIssued = false;
                    return;
                }
            }
        }

        private void TryIssueRetentionNudges()
        {
            if (!IsMilestoneComplete(FirstHourMilestone.Orientation))
                return;

            QuestManager questManager = QuestManager.Instance;
            if (questManager == null)
                return;

            if (!_firstResourceReminderIssued &&
                !questManager.IsCompleted(firstResourceQuestId) &&
                _sessionTime >= firstResourceReminderTime)
            {
                _firstResourceReminderIssued = true;
                string reminderMessage = string.Equals(firstResourceItemId, "Data_Copper", StringComparison.Ordinal)
                    ? "ИЩИ МЕДЬ В ОБЛОМКАХ И У СКАЛ. БЕЗ НЕЁ ТЫ НЕ СДВИНЕШЬСЯ ДАЛЬШЕ."
                    : "ИЩИ ПЕРВЫЙ ОПОРНЫЙ МАТЕРИАЛ В ОБЛОМКАХ И У ЧИТАЕМЫХ СКАЛ. БЕЗ НЕГО ЦЕПЬ НЕ ПОЙДЁТ ДАЛЬШЕ.";
                NotificationEvents.PushInfo(reminderMessage);
            }

            if (!_firstDepthReminderIssued &&
                questManager.IsCompleted(firstResourceQuestId) &&
                questManager.IsActive(firstDepthQuestId) &&
                !questManager.IsCompleted(firstDepthQuestId) &&
                _sessionTime >= firstDepthReminderTime)
            {
                _firstDepthReminderIssued = true;
                NotificationEvents.PushInfo("ПЕРВАЯ НАСТОЯЩАЯ НАХОДКА НИЖЕ. ИДИ ГЛУБЖЕ, НО НЕ ТЕРЯЙ ВЫХОД.");
            }

            if (!_firstModuleReminderIssued &&
                questManager.IsCompleted(firstDepthQuestId) &&
                !IsMilestoneComplete(FirstHourMilestone.FirstModule) &&
                _sessionTime >= firstModuleReminderTime)
            {
                _firstModuleReminderIssued = true;

                WorldZoneAnchor currentZone = _worldZoneDirector != null ? _worldZoneDirector.CurrentZone : null;
                HectonBiomeMatrixProfile currentBiome = ResolveCurrentBiomeProfile(currentZone);
                NotificationEvents.PushInfo(ResolveModuleRouteGuidanceMessage(currentZone, currentBiome));
            }
        }

        private void TryIssueContextualGuidance()
        {
            if (!IsMilestoneComplete(FirstHourMilestone.Orientation))
                return;

            if (Time.unscaledTime < _nextContextualGuidanceTime)
                return;

            QuestManager questManager = QuestManager.Instance;
            if (questManager == null)
                return;

            SynchronizeContextFromRuntimeSystems();

            if (TryIssueServiceRelayGuidance())
                return;

            WorldZoneAnchor currentZone = _worldZoneDirector != null ? _worldZoneDirector.CurrentZone : null;
            if (currentZone == null)
                return;

            int currentDepthTier = _biomeMatrixDirector != null ? _biomeMatrixDirector.CurrentDepthTier : 1;
            HectonBiomeMatrixProfile currentBiome = ResolveCurrentBiomeProfile(currentZone);
            bool resourceCompleted = questManager.IsCompleted(firstResourceQuestId);
            bool depthCompleted = questManager.IsCompleted(firstDepthQuestId);
            bool loreRouteContact = _hasLoreRouteContact;

            bool zoneChanged = !ReferenceEquals(currentZone, _lastObservedZone);
            bool stageChanged =
                resourceCompleted != _lastContextResourceCompleted ||
                depthCompleted != _lastContextDepthCompleted ||
                loreRouteContact != _lastContextLoreContact;

            _lastObservedZone = currentZone;
            _lastContextResourceCompleted = resourceCompleted;
            _lastContextDepthCompleted = depthCompleted;
            _lastContextLoreContact = loreRouteContact;

            if (!zoneChanged && !stageChanged)
                return;

            if (TryIssueEarlyResourceZoneGuidance(questManager, currentZone, currentBiome))
                return;

            if (TryIssueFabricationReturnGuidance(questManager, currentZone, currentBiome))
                return;

            if (TryIssueStarterBackslideGuidance(questManager, currentZone, currentBiome, currentDepthTier))
                return;

            if (TryIssueDeeperRouteGuidance(questManager, currentZone, currentBiome, currentDepthTier))
                return;

            TryIssueModuleRouteGuidance(questManager, currentZone, currentBiome, currentDepthTier);
        }

        private bool TryIssueServiceRelayGuidance()
        {
            EmergencyServiceRelayDirector relayDirector = EmergencyServiceRelayDirector.Instance;
            if (relayDirector == null ||
                !relayDirector.TryBuildContextualGuidanceMessage(out string relayMessage))
            {
                return false;
            }

            PublishContextualInfo(relayMessage);
            return true;
        }

        private bool TryIssueEarlyResourceZoneGuidance(
            QuestManager questManager,
            WorldZoneAnchor currentZone,
            HectonBiomeMatrixProfile currentBiome)
        {
            if (_starterResourcesZoneHintIssued ||
                questManager.IsCompleted(firstResourceQuestId) ||
                currentZone.Kind != WorldZoneAnchor.ZoneKind.Resources)
            {
                return false;
            }

            _starterResourcesZoneHintIssued = true;
            PublishContextualInfo(ResolveResourceZoneGuidanceMessage(currentZone, currentBiome));
            return true;
        }

        private bool TryIssueFabricationReturnGuidance(
            QuestManager questManager,
            WorldZoneAnchor currentZone,
            HectonBiomeMatrixProfile currentBiome)
        {
            if (currentZone.Kind != WorldZoneAnchor.ZoneKind.Fabrication)
                return false;

            if (!_starterFabricationFallbackHintIssued &&
                !questManager.IsCompleted(firstResourceQuestId))
            {
                _starterFabricationFallbackHintIssued = true;
                PublishContextualInfo(ResolveFabricationFallbackMessage(currentZone, currentBiome));
                return true;
            }

            if (!_firstReturnLoreHintIssued &&
                questManager.IsCompleted(firstResourceQuestId) &&
                !questManager.IsCompleted(firstDepthQuestId) &&
                !_hasLoreRouteContact)
            {
                _firstReturnLoreHintIssued = true;
                PublishContextualInfo(ResolveReturnLoreGuidanceMessage(currentZone, currentBiome));
                return true;
            }

            return false;
        }

        private bool TryIssueDeeperRouteGuidance(
            QuestManager questManager,
            WorldZoneAnchor currentZone,
            HectonBiomeMatrixProfile currentBiome,
            int currentDepthTier)
        {
            if (_deeperRouteZoneHintIssued ||
                !questManager.IsCompleted(firstResourceQuestId) ||
                questManager.IsCompleted(firstDepthQuestId) ||
                currentDepthTier > 1)
            {
                return false;
            }

            if (currentZone.Kind != WorldZoneAnchor.ZoneKind.Navigation &&
                currentZone.Kind != WorldZoneAnchor.ZoneKind.Progression &&
                currentZone.Kind != WorldZoneAnchor.ZoneKind.Service)
            {
                return false;
            }

            _deeperRouteZoneHintIssued = true;
            PublishContextualInfo(ResolveDeeperRouteGuidanceMessage(currentZone, currentBiome));
            return true;
        }

        private bool TryIssueStarterBackslideGuidance(
            QuestManager questManager,
            WorldZoneAnchor currentZone,
            HectonBiomeMatrixProfile currentBiome,
            int currentDepthTier)
        {
            if (_starterBackslideGuidanceIssued ||
                !questManager.IsCompleted(firstDepthQuestId) ||
                IsMilestoneComplete(FirstHourMilestone.FirstModule) ||
                currentZone == null)
            {
                return false;
            }

            bool inStarterSafetyPocket =
                currentZone.Tier == WorldZoneAnchor.ZoneTier.Starter &&
                (currentZone.Kind == WorldZoneAnchor.ZoneKind.Resources ||
                 currentZone.Kind == WorldZoneAnchor.ZoneKind.Fabrication ||
                 currentZone.Kind == WorldZoneAnchor.ZoneKind.Service);

            if (!inStarterSafetyPocket && currentDepthTier > 1)
                return false;

            _starterBackslideGuidanceIssued = true;
            PublishContextualInfo(ResolveStarterBackslideMessage(currentZone, currentBiome));
            return true;
        }

        private bool TryIssueModuleRouteGuidance(
            QuestManager questManager,
            WorldZoneAnchor currentZone,
            HectonBiomeMatrixProfile currentBiome,
            int currentDepthTier)
        {
            if (_moduleRouteHintIssued ||
                !questManager.IsCompleted(firstDepthQuestId) ||
                IsMilestoneComplete(FirstHourMilestone.FirstModule) ||
                currentDepthTier <= 1)
            {
                return false;
            }

            if (currentZone.Kind != WorldZoneAnchor.ZoneKind.Navigation &&
                currentZone.Kind != WorldZoneAnchor.ZoneKind.Service &&
                currentZone.Kind != WorldZoneAnchor.ZoneKind.Progression &&
                currentZone.Kind != WorldZoneAnchor.ZoneKind.Combat)
            {
                return false;
            }

            _moduleRouteHintIssued = true;
            _firstModuleHintIssued = true;
            PublishContextualInfo(ResolveModuleRouteGuidanceMessage(currentZone, currentBiome));
            return true;
        }

        private void PublishContextualInfo(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            NotificationEvents.PushInfo(message);
            _nextContextualGuidanceTime = Time.unscaledTime + Mathf.Max(0f, contextualGuidanceCooldown);
        }

        private HectonBiomeMatrixProfile ResolveCurrentBiomeProfile(WorldZoneAnchor currentZone)
        {
            if (currentZone != null && currentZone.DominantMatrixBiome != null)
                return currentZone.DominantMatrixBiome;

            return _biomeMatrixDirector != null ? _biomeMatrixDirector.CurrentProfile : null;
        }

        private string ResolveResourceZoneGuidanceMessage(
            WorldZoneAnchor currentZone,
            HectonBiomeMatrixProfile currentBiome)
        {
            WorldZoneProfile zoneProfile = currentZone != null ? currentZone.Profile : null;
            WorldMotivationProfile motivation = zoneProfile != null ? zoneProfile.motivationProfile : null;
            WorldSandboxAttractionProfile sandbox = zoneProfile != null ? zoneProfile.sandboxAttractionProfile : null;
            WorldExpeditionLoopProfile expedition = zoneProfile != null ? zoneProfile.expeditionLoopProfile : null;

            return SelectFirstNonEmpty(
                motivation != null ? motivation.resourceNeed : null,
                sandbox != null ? sandbox.ambientValue : null,
                expedition != null ? expedition.softProgressionPull : null,
                currentBiome != null ? currentBiome.commonRewardHook : null,
                currentBiome != null ? currentBiome.landmarkGuidance : null,
                MsgResourceShelfRead);
        }

        private string ResolveFabricationFallbackMessage(
            WorldZoneAnchor currentZone,
            HectonBiomeMatrixProfile currentBiome)
        {
            WorldZoneProfile zoneProfile = currentZone != null ? currentZone.Profile : null;
            WorldSandboxAttractionProfile sandbox = zoneProfile != null ? zoneProfile.sandboxAttractionProfile : null;
            WorldExpeditionLoopProfile expedition = zoneProfile != null ? zoneProfile.expeditionLoopProfile : null;

            return SelectFirstNonEmpty(
                expedition != null ? expedition.reliefBeat : null,
                expedition != null ? expedition.playerPromise : null,
                sandbox != null ? sandbox.shelterRead : null,
                currentBiome != null ? currentBiome.safePocketIdentity : null,
                null,
                MsgFabricationFallback);
        }

        private string ResolveReturnLoreGuidanceMessage(
            WorldZoneAnchor currentZone,
            HectonBiomeMatrixProfile currentBiome)
        {
            WorldZoneProfile zoneProfile = currentZone != null ? currentZone.Profile : null;
            WorldMotivationProfile motivation = zoneProfile != null ? zoneProfile.motivationProfile : null;
            WorldSandboxAttractionProfile sandbox = zoneProfile != null ? zoneProfile.sandboxAttractionProfile : null;

            return SelectFirstNonEmpty(
                motivation != null ? motivation.storyPull : null,
                sandbox != null ? sandbox.storyLure : null,
                currentBiome != null ? currentBiome.rareRewardHook : null,
                null,
                null,
                MsgReturnLoreRelay);
        }

        private string ResolveDeeperRouteGuidanceMessage(
            WorldZoneAnchor currentZone,
            HectonBiomeMatrixProfile currentBiome)
        {
            WorldZoneProfile zoneProfile = currentZone != null ? currentZone.Profile : null;
            WorldSandboxAttractionProfile sandbox = zoneProfile != null ? zoneProfile.sandboxAttractionProfile : null;
            WorldExpeditionLoopProfile expedition = zoneProfile != null ? zoneProfile.expeditionLoopProfile : null;

            return SelectFirstNonEmpty(
                sandbox != null ? sandbox.deepLure : null,
                expedition != null ? expedition.softProgressionPull : null,
                currentBiome != null ? currentBiome.landmarkGuidance : null,
                currentBiome != null ? currentBiome.rareRewardHook : null,
                null,
                MsgDeeperRouteRead);
        }

        private string ResolveModuleRouteGuidanceMessage(
            WorldZoneAnchor currentZone,
            HectonBiomeMatrixProfile currentBiome)
        {
            WorldZoneProfile zoneProfile = currentZone != null ? currentZone.Profile : null;
            WorldMotivationProfile motivation = zoneProfile != null ? zoneProfile.motivationProfile : null;
            WorldSandboxAttractionProfile sandbox = zoneProfile != null ? zoneProfile.sandboxAttractionProfile : null;

            return SelectFirstNonEmpty(
                motivation != null ? motivation.storyPull : null,
                motivation != null ? motivation.curiosityPull : null,
                sandbox != null ? sandbox.storyLure : null,
                currentBiome != null ? currentBiome.landmarkGuidance : null,
                null,
                MsgModuleRouteRead);
        }

        private string ResolveStarterBackslideMessage(
            WorldZoneAnchor currentZone,
            HectonBiomeMatrixProfile currentBiome)
        {
            WorldZoneProfile zoneProfile = currentZone != null ? currentZone.Profile : null;
            WorldExpeditionLoopProfile expedition = zoneProfile != null ? zoneProfile.expeditionLoopProfile : null;
            WorldMotivationProfile motivation = zoneProfile != null ? zoneProfile.motivationProfile : null;
            WorldSandboxAttractionProfile sandbox = zoneProfile != null ? zoneProfile.sandboxAttractionProfile : null;

            return SelectFirstNonEmpty(
                expedition != null ? expedition.playerPromise : null,
                expedition != null ? expedition.softProgressionPull : null,
                motivation != null ? motivation.storyPull : null,
                sandbox != null ? sandbox.deepLure : null,
                currentBiome != null ? currentBiome.landmarkGuidance : null,
                MsgStarterBackslideRead);
        }

        private static string SelectFirstNonEmpty(
            string optionA,
            string optionB,
            string optionC,
            string optionD,
            string optionE,
            string fallback)
        {
            if (!string.IsNullOrWhiteSpace(optionA))
                return optionA;

            if (!string.IsNullOrWhiteSpace(optionB))
                return optionB;

            if (!string.IsNullOrWhiteSpace(optionC))
                return optionC;

            if (!string.IsNullOrWhiteSpace(optionD))
                return optionD;

            if (!string.IsNullOrWhiteSpace(optionE))
                return optionE;

            return fallback;
        }

        private static bool TryGetRuntimeInventory(out PlayerInventory inventory)
        {
            inventory = null;

            if (!SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) ||
                playerTransform == null)
            {
                return false;
            }

            inventory = playerTransform.GetComponent<PlayerInventory>();
            if (inventory != null)
                return true;

            inventory = playerTransform.GetComponentInChildren<PlayerInventory>(true);
            return inventory != null;
        }
    }
}
