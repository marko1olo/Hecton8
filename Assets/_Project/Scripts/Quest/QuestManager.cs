// ============================================================================
// HECTON-8 — QuestManager.cs
// Stateless quest hub — слушает события мира и продвигает квесты.
//
// АРХИТЕКТУРА:
//   • Все квесты — QuestData ScriptableObjects (data-driven).
//   • QuestManager слушает глобальные события (NarrativeEvents, AudioLogEvents,
//     HectonCelestialEngine.OnEclipseStart, AtlasSignalEvents).
//   • Состояние квестов: активные / завершённые HashSet<string>.
//   • ISaveable: сохраняет активные и завершённые questId.
//
// ZERO GC:
//   • HashSet<string> для O(1) проверки.
//   • ISlowTickable для depth-based триггеров.
//   • Никаких new/LINQ в hot path.
//
// ИНТЕГРАЦИЯ С ЛОРОМ:
//   • QuestTriggerType.OnEclipseStart → квест "Пережить Великое Затмение"
//   • QuestTriggerType.OnSignalDetected → квест "Найти источник сигнала"
//   • QuestTriggerType.OnAudioLogFound → квест "Собрать записи Chen_M"
// ============================================================================

using System.Collections.Generic;
using Hecton8.AtlasSignal;
using Hecton8.Celestial;
using Hecton8.Core;
using Hecton8.Interaction;
using Hecton8.Gameplay;
using Hecton8.Items;
using Hecton8.Narrative;
using Hecton8.SaveSystem;
using Hecton8.UI;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Quest
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-130)]
    public sealed class QuestManager : MonoBehaviour, ISaveable, ISlowTickable
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Quest Registry ──────────────────────────")]
        [Tooltip("Все квесты проекта. Назначить в инспекторе.")]
        [SerializeField] private QuestData[] allQuests = new QuestData[0];

        private const string kQuestFolder = "Assets/_Project/Data/Lore/Quests";

        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        public static QuestManager Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => Instance = null;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        // COLD ALLOC: 64 entries — max concurrent active quests
        private readonly HashSet<string> _activeQuests    = new HashSet<string>(64);
        private readonly HashSet<string> _completedQuests = new HashSet<string>(128);

        // Lookup: questId → QuestData (COLD ALLOC)
        private readonly Dictionary<string, QuestData> _questLookup =
            new Dictionary<string, QuestData>(64);

        private float _currentDepth;
        private bool _registered;
        private bool _biomeDiscoveryRegistered;

        // ══════════════════════════════════════════════════════════
        //  ISaveable
        // ══════════════════════════════════════════════════════════

        public int SavePriority => 7;
        public int LoadPriority => 7;

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
            BuildLookup();
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

            SubscribeToEvents();
            TrySubscribeToBiomeDiscovery();
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

            UnsubscribeFromEvents();
            UnsubscribeFromBiomeDiscovery();
        }

        private void Start()
        {
            // Активируем квесты с autoActivateOnStart
            for (int i = 0; i < allQuests.Length; i++)
            {
                QuestData q = allQuests[i];
                if (q != null && q.autoActivateOnStart)
                    ActivateQuest(q.questId);
            }

            TrySubscribeToBiomeDiscovery();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            TryAutoPopulateQuestRegistry();
            BuildLookup();
        }
#endif

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable — depth-based триггеры
        // ══════════════════════════════════════════════════════════

        public void SlowTick()
        {
            // Проверяем depth-based триггеры
            for (int i = 0; i < allQuests.Length; i++)
            {
                QuestData q = allQuests[i];
                if (q == null) continue;

                // Активация по глубине
                if (q.triggerType == QuestTriggerType.OnDepthReached &&
                    !_activeQuests.Contains(q.questId) &&
                    !_completedQuests.Contains(q.questId) &&
                    _currentDepth >= q.triggerValue)
                {
                    ActivateQuest(q.questId);
                }

                // Завершение по глубине
                if (q.completionType == QuestCompletionType.OnDepthReached &&
                    _activeQuests.Contains(q.questId) &&
                    _currentDepth >= q.completionValue)
                {
                    CompleteQuest(q.questId);
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>Активировать квест по ID.</summary>
        public void ActivateQuest(string questId)
        {
            if (string.IsNullOrEmpty(questId)) return;
            if (_activeQuests.Contains(questId)) return;
            if (_completedQuests.Contains(questId)) return;

            _activeQuests.Add(questId);
            QuestEvents.RaiseActivated(questId);

            // HUD notification — показываем название квеста если есть в lookup
            if (_questLookup.TryGetValue(questId, out QuestData q))
                NotificationEvents.PushInfo($"НОВАЯ ЦЕЛЬ: {q.displayTitle}");

        }

        /// <summary>Завершить квест по ID.</summary>
        public void CompleteQuest(string questId)
        {
            if (string.IsNullOrEmpty(questId)) return;
            if (!_activeQuests.Contains(questId)) return;

            _activeQuests.Remove(questId);
            _completedQuests.Add(questId);
            QuestEvents.RaiseCompleted(questId);

            if (_questLookup.TryGetValue(questId, out QuestData q))
                NotificationEvents.PushInfo($"ЦЕЛЬ ВЫПОЛНЕНА: {q.displayTitle}");

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Quest] Completed: {questId}");
#endif
        }

        public bool IsActive(string questId)    => _activeQuests.Contains(questId);
        public bool IsCompleted(string questId) => _completedQuests.Contains(questId);

        /// <summary>Обновить текущую глубину (вызывается из HectonSurvivalSystem или NarrativeDirector).</summary>
        public void UpdateDepth(float depthMeters) => _currentDepth = depthMeters;

        // ══════════════════════════════════════════════════════════
        //  EVENT HANDLERS
        // ══════════════════════════════════════════════════════════

        private void HandleDiscoveryMade(string discoveryId)
        {
            ProcessTrigger(QuestTriggerType.OnDiscoveryMade, discoveryId, 0f);
            ProcessCompletion(QuestCompletionType.OnDiscoveryMade, discoveryId, 0f);
        }

        private void HandleItemCollected(ItemData itemData, int quantity, Transform interactor)
        {
            string itemId = itemData != null ? itemData.name : string.Empty;
            ProcessTrigger(QuestTriggerType.OnItemCollected, itemId, quantity);
            ProcessCompletion(QuestCompletionType.OnItemCollected, itemId, quantity);
        }

        private void HandleAudioLogDiscovered(string logId)
        {
            ProcessTrigger(QuestTriggerType.OnAudioLogFound, logId, 0f);
            ProcessCompletion(QuestCompletionType.OnAudioLogFound, logId, 0f);
        }

        private void HandleEclipseStart()
        {
            ProcessTrigger(QuestTriggerType.OnEclipseStart, string.Empty, 0f);
        }

        private void HandleSignalDetected(UnityEngine.Vector3 sourcePos)
        {
            ProcessTrigger(QuestTriggerType.OnSignalDetected, string.Empty, 0f);
        }

        private void HandleBiomeDiscovered(int biomeId)
        {
            ProcessTrigger(QuestTriggerType.OnBiomeEntered, string.Empty, biomeId);
            ProcessCompletion(QuestCompletionType.OnBiomeEntered, string.Empty, biomeId);
        }

        private void HandleDepthTierReached(int tier)
        {
            // Конвертируем тир в примерную глубину для depth-based квестов
            float approxDepth = tier switch
            {
                1 => 0f,
                2 => 100f,
                3 => 300f,
                4 => 1000f,
                _ => 0f
            };
            _currentDepth = approxDepth;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE
        // ══════════════════════════════════════════════════════════

        private void BuildLookup()
        {
            _questLookup.Clear();
            for (int i = 0; i < allQuests.Length; i++)
            {
                QuestData q = allQuests[i];
                if (q != null && !string.IsNullOrEmpty(q.questId))
                    _questLookup[q.questId] = q;
            }
        }

        private void ProcessTrigger(QuestTriggerType type, string id, float value)
        {
            for (int i = 0; i < allQuests.Length; i++)
            {
                QuestData q = allQuests[i];
                if (q == null) continue;
                if (q.triggerType != type) continue;
                if (_activeQuests.Contains(q.questId)) continue;
                if (_completedQuests.Contains(q.questId)) continue;

                if (q.triggerType == QuestTriggerType.OnBiomeEntered)
                {
                    if (!Mathf.Approximately(q.triggerValue, value)) continue;
                }
                else if (q.triggerType == QuestTriggerType.OnItemCollected)
                {
                    if (!string.IsNullOrEmpty(q.triggerId) && q.triggerId != id) continue;
                    if (q.triggerValue > 0f && value < q.triggerValue) continue;
                }
                else if (!string.IsNullOrEmpty(q.triggerId) && q.triggerId != id)
                {
                    continue;
                }

                ActivateQuest(q.questId);
            }
        }

        private void ProcessCompletion(QuestCompletionType type, string id, float value)
        {
            for (int i = 0; i < allQuests.Length; i++)
            {
                QuestData q = allQuests[i];
                if (q == null) continue;
                if (q.completionType != type) continue;
                if (!_activeQuests.Contains(q.questId)) continue;

                if (q.completionType == QuestCompletionType.OnBiomeEntered)
                {
                    if (!Mathf.Approximately(q.completionValue, value)) continue;
                }
                else if (q.completionType == QuestCompletionType.OnItemCollected)
                {
                    if (!string.IsNullOrEmpty(q.completionId) && q.completionId != id) continue;
                    if (q.completionValue > 0f && value < q.completionValue) continue;
                }
                else if (!string.IsNullOrEmpty(q.completionId) && q.completionId != id)
                {
                    continue;
                }

                CompleteQuest(q.questId);
            }
        }

        private void SubscribeToEvents()
        {
            InteractionEvents.OnItemCollected    += HandleItemCollected;
            NarrativeEvents.OnDiscoveryMade      += HandleDiscoveryMade;
            NarrativeEvents.OnDepthTierReached   += HandleDepthTierReached;
            AudioLogEvents.OnLogDiscovered       += HandleAudioLogDiscovered;
            HectonCelestialEngine.OnEclipseStart += HandleEclipseStart;
            AtlasSignalEvents.OnSignalDetected   += HandleSignalDetected;
        }

        private void UnsubscribeFromEvents()
        {
            InteractionEvents.OnItemCollected    -= HandleItemCollected;
            NarrativeEvents.OnDiscoveryMade      -= HandleDiscoveryMade;
            NarrativeEvents.OnDepthTierReached   -= HandleDepthTierReached;
            AudioLogEvents.OnLogDiscovered       -= HandleAudioLogDiscovered;
            HectonCelestialEngine.OnEclipseStart -= HandleEclipseStart;
            AtlasSignalEvents.OnSignalDetected   -= HandleSignalDetected;
        }

        private void TrySubscribeToBiomeDiscovery()
        {
            if (_biomeDiscoveryRegistered)
                return;

            HectonDiscoveryManager discoveryManager = HectonDiscoveryManager.Instance;
            if (discoveryManager == null)
                return;

            discoveryManager.OnBiomeDiscovered += HandleBiomeDiscovered;
            _biomeDiscoveryRegistered = true;
        }

        private void UnsubscribeFromBiomeDiscovery()
        {
            if (!_biomeDiscoveryRegistered)
                return;

            HectonDiscoveryManager discoveryManager = HectonDiscoveryManager.Instance;
            if (discoveryManager != null)
                discoveryManager.OnBiomeDiscovered -= HandleBiomeDiscovered;

            _biomeDiscoveryRegistered = false;
        }

#if UNITY_EDITOR
        private void TryAutoPopulateQuestRegistry()
        {
            if (allQuests != null && allQuests.Length > 0)
                return;

            string[] guids = AssetDatabase.FindAssets("t:QuestData", new[] { kQuestFolder });
            if (guids == null || guids.Length == 0)
                return;

            QuestData[] loaded = new QuestData[guids.Length];
            int count = 0;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                QuestData quest = AssetDatabase.LoadAssetAtPath<QuestData>(path);
                if (quest == null)
                    continue;

                loaded[count++] = quest;
            }

            if (count <= 0)
                return;

            if (count != loaded.Length)
                System.Array.Resize(ref loaded, count);

            allQuests = loaded;
            EditorUtility.SetDirty(this);
        }
#endif

        // ══════════════════════════════════════════════════════════
        //  ISaveable
        // ══════════════════════════════════════════════════════════

        public void PopulateSaveData(SaveData data)
        {
            if (data == null) return;

            data.questActiveIds.Clear();
            data.questCompletedIds.Clear();

            foreach (string id in _activeQuests)
                data.questActiveIds.Add(id);

            foreach (string id in _completedQuests)
                data.questCompletedIds.Add(id);
        }

        public void LoadFromSaveData(SaveData data)
        {
            _activeQuests.Clear();
            _completedQuests.Clear();

            if (data == null) return;

            if (data.questActiveIds != null)
                foreach (string id in data.questActiveIds)
                    if (!string.IsNullOrEmpty(id)) _activeQuests.Add(id);

            if (data.questCompletedIds != null)
                foreach (string id in data.questCompletedIds)
                    if (!string.IsNullOrEmpty(id)) _completedQuests.Add(id);
        }
    }
}
