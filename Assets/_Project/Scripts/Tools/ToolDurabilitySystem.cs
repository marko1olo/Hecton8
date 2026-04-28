// ============================================================================
// HECTON-8 — ToolDurabilitySystem.cs  v1.0 ENTERPRISE
// Система износа и ремонта инструментов.
// Singleton — управляет durability всех инструментов в игре.
//
// v1.0 ENTERPRISE FEATURES:
//   [ADD] Runtime durability tracking — словарь toolID → current durability
//   [ADD] Durability drain — автоматический износ при использовании
//   [ADD] Repair system — ремонт за ресурсы
//   [ADD] Durability events — OnDurabilityChanged, OnToolBroken
//   [ADD] Save/Load integration — сохранение состояния инструментов
//   [ADD] Zero GC — pre-allocated dictionaries, cached references
//
// АРХИТЕКТУРА:
//   • Singleton pattern (Instance)
//   • ISaveable — сохраняет durability в SaveData
//   • Читается PlayerTool при UsePrimary/UseSecondary
//   • Отображается в HUD и PDA
//
// ZERO GC:
//   • Dictionary<string, float> — pre-allocated capacity
//   • Events — cached delegates, no boxing
//   • No string allocations in hot paths
// ============================================================================

using System;
using System.Collections.Generic;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.SaveSystem;
using UnityEngine;

namespace Hecton8.Tools
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Tools/Tool Durability System")]
    public sealed class ToolDurabilitySystem : MonoBehaviour, ISaveable, ISlowTickable
    {
        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        public static ToolDurabilitySystem Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
        }

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — SETTINGS
        // ══════════════════════════════════════════════════════════

        [Header("── Settings ────────────────────────────────")]
        [Tooltip("Включить автоматический износ инструментов.")]
        [SerializeField] private bool enableDurabilityDrain = true;

        [Tooltip("Глобальный множитель износа (1.0 = 100%).")]
        [Range(0.1f, 2f)]
        [SerializeField] private float globalDurabilityMultiplier = 1f;

        [Tooltip("Автоматически ломать инструмент при durability = 0.")]
        [SerializeField] private bool autoBreakOnZero = true;
        [Tooltip("Passive corrosion on the currently held tool while the player stays underwater.")]
        [SerializeField] private bool enableEnvironmentalCorrosion = true;
        [Tooltip("Base corrosion per second for a held underwater tool.")]
        [Range(0f, 1f)]
        [SerializeField] private float heldUnderwaterCorrosionPerSecond = 0.04f;
        [Tooltip("Extra corrosion per second when the held underwater tool was used recently.")]
        [Range(0f, 2f)]
        [SerializeField] private float activeUseCorrosionPerSecond = 0.12f;
        [Tooltip("Extra corrosion multiplier applied during cold stress.")]
        [Range(0f, 2f)]
        [SerializeField] private float coldStressCorrosionMultiplier = 0.55f;
        [Tooltip("Extra corrosion multiplier applied during heat stress.")]
        [Range(0f, 2f)]
        [SerializeField] private float heatStressCorrosionMultiplier = 0.35f;

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Словарь: toolID → текущая прочность.
        /// Pre-allocated capacity 32 (типичное количество инструментов).
        /// </summary>
        private readonly Dictionary<string, float> _durabilityMap = new Dictionary<string, float>(32);

        /// <summary>
        /// Словарь: toolID → сломан ли инструмент.
        /// </summary>
        private readonly Dictionary<string, bool> _brokenMap = new Dictionary<string, bool>(32);
        private HectonSurvivalSystem _playerSurvivalSystem;
        private PlayerToolManager _playerToolManager;
        private Transform _playerRoot;
        private bool _registeredToTick;
        private const float SlowTickDeltaTime = 0.5f;
        private const float UnderwaterDepthThreshold = 0.5f;
        private const float ActiveUseWindowSeconds = 0.7f;

        // ══════════════════════════════════════════════════════════
        //  EVENTS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Fired when tool durability changes.
        /// Parameters: (toolID, currentDurability, maxDurability).
        /// </summary>
        public event Action<string, float, float> OnDurabilityChanged;

        /// <summary>
        /// Fired when tool breaks (durability reaches 0).
        /// Parameter: toolID.
        /// </summary>
        public event Action<string> OnToolBroken;

        /// <summary>
        /// Fired when tool is repaired.
        /// Parameters: (toolID, newDurability).
        /// </summary>
        public event Action<string, float> OnToolRepaired;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[ToolDurabilitySystem] Duplicate instance detected. Destroying.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnEnable()
        {
            TryRegisterWithTickManager();
            SaveManager.Instance?.Register(this);
        }

        private void Start()
        {
            TryRegisterWithTickManager();
        }

        private void OnDisable()
        {
            UnregisterFromTickManager();
            SaveManager.Instance?.Unregister(this);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — DURABILITY
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Возвращает текущую прочность инструмента.
        /// Если инструмент не зарегистрирован — возвращает maxDurability.
        /// </summary>
        public float GetDurability(string toolID, float maxDurability)
        {
            if (string.IsNullOrEmpty(toolID))
                return maxDurability;

            if (_durabilityMap.TryGetValue(toolID, out float current))
                return current;

            // Первое обращение — инициализируем полной прочностью
            _durabilityMap[toolID] = maxDurability;
            return maxDurability;
        }

        /// <summary>
        /// Возвращает нормализованную прочность (0-1).
        /// </summary>
        public float GetDurabilityNormalized(string toolID, float maxDurability)
        {
            float current = GetDurability(toolID, maxDurability);
            return Mathf.Clamp01(current / Mathf.Max(1f, maxDurability));
        }

        /// <summary>
        /// Проверяет, сломан ли инструмент.
        /// </summary>
        public bool IsBroken(string toolID)
        {
            if (string.IsNullOrEmpty(toolID))
                return false;

            return _brokenMap.TryGetValue(toolID, out bool broken) && broken;
        }

        /// <summary>
        /// Уменьшает прочность инструмента.
        /// Вызывается из PlayerTool.UsePrimary/UseSecondary.
        /// </summary>
        /// <param name="toolID">ID инструмента.</param>
        /// <param name="amount">Количество износа.</param>
        /// <param name="maxDurability">Максимальная прочность.</param>
        public void DrainDurability(string toolID, float amount, float maxDurability)
        {
            if (!enableDurabilityDrain)
                return;

            if (string.IsNullOrEmpty(toolID))
                return;

            if (IsBroken(toolID))
                return; // сломанный инструмент не изнашивается дальше

            float current = GetDurability(toolID, maxDurability);
            float drain = amount * globalDurabilityMultiplier;

            current = Mathf.Max(0f, current - drain);
            _durabilityMap[toolID] = current;

            OnDurabilityChanged?.Invoke(toolID, current, maxDurability);

            // Проверка на поломку
            if (current <= 0f && autoBreakOnZero)
            {
                BreakTool(toolID);
            }
        }

        /// <summary>
        /// Ремонтирует инструмент на указанное количество.
        /// </summary>
        /// <param name="toolID">ID инструмента.</param>
        /// <param name="amount">Количество восстановления.</param>
        /// <param name="maxDurability">Максимальная прочность.</param>
        public void RepairTool(string toolID, float amount, float maxDurability)
        {
            if (string.IsNullOrEmpty(toolID))
                return;

            float current = GetDurability(toolID, maxDurability);
            current = Mathf.Min(maxDurability, current + amount);
            _durabilityMap[toolID] = current;

            // Если был сломан — чиним
            if (_brokenMap.ContainsKey(toolID))
                _brokenMap[toolID] = false;

            OnToolRepaired?.Invoke(toolID, current);
            OnDurabilityChanged?.Invoke(toolID, current, maxDurability);
        }

        /// <summary>
        /// Полностью ремонтирует инструмент.
        /// </summary>
        public void RepairToolFull(string toolID, float maxDurability)
        {
            RepairTool(toolID, maxDurability, maxDurability);
        }

        /// <summary>
        /// Ломает инструмент (устанавливает broken flag).
        /// </summary>
        public void BreakTool(string toolID)
        {
            if (string.IsNullOrEmpty(toolID))
                return;

            if (IsBroken(toolID))
                return; // уже сломан

            _brokenMap[toolID] = true;
            OnToolBroken?.Invoke(toolID);
        }

        /// <summary>
        /// Сбрасывает прочность инструмента к максимальной.
        /// Используется при создании нового инструмента.
        /// </summary>
        public void ResetDurability(string toolID, float maxDurability)
        {
            if (string.IsNullOrEmpty(toolID))
                return;

            _durabilityMap[toolID] = maxDurability;

            if (_brokenMap.ContainsKey(toolID))
                _brokenMap[toolID] = false;

            OnDurabilityChanged?.Invoke(toolID, maxDurability, maxDurability);
        }

        // ══════════════════════════════════════════════════════════
        //  ISaveable — SAVE / LOAD
        // ══════════════════════════════════════════════════════════

        public int SavePriority => 20; // после игрока (10)
        public int LoadPriority => 20;

        public void PopulateSaveData(SaveData data)
        {
            // Сохраняем durability map
            data.toolDurabilityMap.Clear();
            foreach (var kvp in _durabilityMap)
            {
                data.toolDurabilityMap[kvp.Key] = kvp.Value;
            }

            // Сохраняем broken map
            data.toolBrokenMap.Clear();
            foreach (var kvp in _brokenMap)
            {
                if (kvp.Value) // сохраняем только сломанные
                    data.toolBrokenMap[kvp.Key] = true;
            }
        }

        public void LoadFromSaveData(SaveData data)
        {
            // Загружаем durability map
            _durabilityMap.Clear();
            foreach (var kvp in data.toolDurabilityMap)
            {
                _durabilityMap[kvp.Key] = kvp.Value;
            }

            // Загружаем broken map
            _brokenMap.Clear();
            foreach (var kvp in data.toolBrokenMap)
            {
                _brokenMap[kvp.Key] = kvp.Value;
            }
        }
        public void SlowTick()
        {
            ApplyEnvironmentalCorrosion();
        }

        private void TryRegisterWithTickManager()
        {
            if (_registeredToTick)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Player);
            _registeredToTick = true;
        }

        private void UnregisterFromTickManager()
        {
            if (!_registeredToTick)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Player);
            _registeredToTick = false;
        }

        private void ApplyEnvironmentalCorrosion()
        {
            if (!enableEnvironmentalCorrosion)
                return;

            if (!ResolvePlayerOwners())
                return;

            if (_playerSurvivalSystem == null || _playerToolManager == null)
                return;

            if (_playerSurvivalSystem.Depth <= UnderwaterDepthThreshold)
                return;

            PlayerTool currentTool = _playerToolManager.CurrentTool;
            if (currentTool == null || !currentTool.IsEquipped || currentTool.IsBroken)
                return;

            ToolMetadata metadata = currentTool.RuntimeMetadata;
            if (metadata == null || string.IsNullOrEmpty(metadata.toolID))
                return;

            float drain = heldUnderwaterCorrosionPerSecond * SlowTickDeltaTime;
            if (currentTool.WasRecentlyUsed(ActiveUseWindowSeconds))
                drain += activeUseCorrosionPerSecond * SlowTickDeltaTime;

            if (_playerSurvivalSystem.IsInColdStress)
                drain *= 1f + (_playerSurvivalSystem.ColdStressSeverity01 * coldStressCorrosionMultiplier);

            if (_playerSurvivalSystem.IsInHeatStress)
                drain *= 1f + (_playerSurvivalSystem.HeatStressSeverity01 * heatStressCorrosionMultiplier);

            if (drain <= 0.0001f)
                return;

            DrainDurability(metadata.toolID, drain, metadata.maxDurability);
        }

        private bool ResolvePlayerOwners()
        {
            if (_playerRoot == null)
            {
                if (!SceneBootstrap.TryGetCurrentPlayerTransform(out _playerRoot) || _playerRoot == null)
                    return false;
            }

            if (_playerSurvivalSystem == null)
                _playerRoot.TryGetComponent(out _playerSurvivalSystem);

            if (_playerToolManager == null)
                _playerToolManager = ((Hecton8.Core.GlobalRegistry.Player != null && Hecton8.Core.GlobalRegistry.Player.ToolManager != null) ? Hecton8.Core.GlobalRegistry.Player.ToolManager : _playerRoot.GetComponent<PlayerToolManager>());

            return _playerSurvivalSystem != null && _playerToolManager != null;
        }
    }
}
