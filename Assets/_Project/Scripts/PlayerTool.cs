// ============================================================================
// HECTON-8 — PlayerTool.cs  v2.0 ENTERPRISE
// Абстрактный базовый класс для всех инструментов игрока.
//
// v2.0 ENTERPRISE ADDITIONS:
//   [ADD] ToolMetadata integration — durability, upgrades, stats
//   [ADD] Automatic durability drain — при UsePrimary/UseSecondary
//   [ADD] Tool events — OnToolUsed, OnDurabilityLow, OnToolBroken
//   [ADD] Energy consumption — интеграция с HectonSurvivalSystem
//   [ADD] Stat modifiers — efficiency, speed применяются автоматически
//   [ADD] Broken tool handling — блокировка использования при durability=0
//
// Ответственности:
//   1. Определяет жизненный цикл инструмента: Equip → Use → Unequip.
//   2. Реализует IPoolable для совместимости с ObjectPoolManager.
//   3. Хранит ссылку на ItemData для связи с инвентарной системой.
//   4. Автоматически управляет износом через ToolDurabilitySystem.
//   5. Применяет stat modifiers из ToolMetadata и upgrades.
//
// НЕ содержит Update/FixedUpdate — логика вызывается через
// PlayerToolManager.Tick() → UsePrimary()/UseSecondary().
//
// НАСЛЕДНИКИ: LaserCutter, Scanner, Builder и т.д.
// ============================================================================

namespace Hecton8.Gameplay
{
    using Hecton8.Bootstrap;
    using Hecton8.Core;
    using Hecton8.Interaction;
    using Hecton8.Items;
    using Hecton8.Tools;
    using System;
    using UnityEngine;

    /// <summary>
    /// Базовый класс для всех инструментов, которые игрок
    /// может держать в руках. Управляется через <see cref="PlayerToolManager"/>.
    /// </summary>
    public abstract class PlayerTool : MonoBehaviour, IPoolable
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Tool Identity ─────────────────────────────")]
        [Tooltip("Ссылка на ItemData этого инструмента. " +
                 "Используется для проверки наличия в инвентаре.")]
        [SerializeField] private ItemData _toolData;

        [Tooltip("Метаданные инструмента (durability, upgrades, stats). v2.0 ENTERPRISE")]
        [SerializeField] private ToolMetadata _toolMetadata;

        [Header("── v2.0 ENTERPRISE Settings ──────────────────")]
        [Tooltip("Включить автоматический износ при использовании.")]
        [SerializeField] private bool enableDurabilityDrain = true;

        [Tooltip("Включить энергопотребление при использовании.")]
        [SerializeField] private bool enableEnergyConsumption = true;

        [Tooltip("Включить подробные lifecycle-логи для диагностики.")]
        [SerializeField] private bool lifecycleDebugLogging = false;

        [Tooltip("Optional swim-presentation contract for near-camera hand ownership while this tool is equipped.")]
        [SerializeField] private PlayerToolSwimContract _swimContract;

        [Tooltip("Optional transport feel contract consumed by audio and presentation while this tool drives the player.")]
        [SerializeField] private PlayerTransportFeelContract _transportFeelContract;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Данные предмета инструмента (ScriptableObject).
        /// Используется <see cref="PlayerToolManager"/> для проверки
        /// наличия инструмента в <see cref="Hecton8.Inventory.PlayerInventory"/>.
        /// </summary>
        public ItemData ToolData => _toolData;

        /// <summary>
        /// Метаданные инструмента (durability, upgrades, stats). v2.0 ENTERPRISE
        /// </summary>
        public ToolMetadata Metadata => _toolMetadata;

        /// <summary>
        /// Экипирован ли инструмент в данный момент.
        /// Устанавливается <see cref="PlayerToolManager"/> при Equip/Unequip.
        /// </summary>
        public bool IsEquipped { get; private set; }

        /// <summary>
        /// Optional near-camera swim-presentation contract for this tool.
        /// </summary>
        public PlayerToolSwimContract SwimContract
        {
            get
            {
                if (!_swimContractResolved)
                    ResolveSwimContract();

                return _swimContract;
            }
        }

        /// <summary>
        /// Optional transport feel contract for this tool.
        /// </summary>
        internal PlayerTransportFeelContract TransportFeelContract
        {
            get
            {
                if (!_transportFeelContractResolved)
                    ResolveTransportFeelContract();

                return _transportFeelContract;
            }
        }

        /// <summary>
        /// Текущая прочность инструмента (0-maxDurability). v2.0 ENTERPRISE
        /// </summary>
        public float CurrentDurability
        {
            get
            {
                if (_toolMetadata == null) return 0f;
                var system = ToolDurabilitySystem.Instance;
                if (system == null) return _toolMetadata.maxDurability;
                return system.GetDurability(_toolMetadata.toolID, _toolMetadata.maxDurability);
            }
        }

        /// <summary>
        /// Нормализованная прочность (0-1). v2.0 ENTERPRISE
        /// </summary>
        public float DurabilityNormalized
        {
            get
            {
                if (_toolMetadata == null) return 1f;
                return CurrentDurability / Mathf.Max(1f, _toolMetadata.maxDurability);
            }
        }

        /// <summary>
        /// Сломан ли инструмент. v2.0 ENTERPRISE
        /// </summary>
        public bool IsBroken
        {
            get
            {
                if (_toolMetadata == null) return false;
                var system = ToolDurabilitySystem.Instance;
                if (system == null) return false;
                return system.IsBroken(_toolMetadata.toolID);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  EVENTS — v2.0 ENTERPRISE
        // ══════════════════════════════════════════════════════════

        /// <summary>Fired when tool is used (Primary or Secondary). Parameter: isPrimary.</summary>
        public event Action<bool> OnToolUsed;

        /// <summary>Fired when durability drops below critical threshold.</summary>
        public event Action OnDurabilityLow;

        /// <summary>Fired when tool breaks (durability reaches 0).</summary>
        public event Action OnToolBroken;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE — v2.0 ENTERPRISE
        // ══════════════════════════════════════════════════════════

        [NonSerialized] private HectonSurvivalSystem _survivalSystem;
        private bool _lowDurabilityWarningFired;
        private bool _swimContractResolved;
        private bool _transportFeelContractResolved;
        private string _cachedOperationalToolName;
        private float _lastUseTime = float.NegativeInfinity;
        private ulong _queuedRaycastRequesterId;

        // ══════════════════════════════════════════════════════════
        //  IPoolable — POOL LIFECYCLE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Вызывается ObjectPoolManager при извлечении из пула.
        /// Сбрасывает состояние инструмента к начальному.
        /// Наследники могут переопределить для своей инициализации,
        /// но ОБЯЗАНЫ вызвать base.OnSpawn().
        /// </summary>
        public virtual void OnSpawn()
        {
            LogLifecycleDebug(
                $"{GetType().Name}.OnSpawn data={(_toolData != null ? _toolData.name : "null")} " +
                $"meta={(_toolMetadata != null ? _toolMetadata.name : "null")}");
            IsEquipped = false;
            _lowDurabilityWarningFired = false;
            _lastUseTime = float.NegativeInfinity;
            RefreshQueuedRaycastRequesterId();
            RefreshOperationalToolNameCache();
            ResolveSwimContract();
            ResolveTransportFeelContract();

            // Auto-resolve SurvivalSystem
            if (_survivalSystem == null && enableEnergyConsumption)
            {
                if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform))
                    _survivalSystem = playerTransform.GetComponent<HectonSurvivalSystem>();
            }
        }

        /// <summary>
        /// Вызывается ObjectPoolManager при возврате в пул.
        /// Гарантирует, что инструмент корректно отключён.
        /// Наследники могут переопределить для очистки,
        /// но ОБЯЗАНЫ вызвать base.OnDespawn().
        /// </summary>
        public virtual void OnDespawn()
        {
            LogLifecycleDebug($"{GetType().Name}.OnDespawn equipped={IsEquipped}");
            // Если инструмент ещё экипирован — корректно снимаем
            if (IsEquipped)
            {
                OnUnequip();
            }

            IsEquipped = false;
            _lowDurabilityWarningFired = false;
            _lastUseTime = float.NegativeInfinity;
            _cachedOperationalToolName = null;
            _queuedRaycastRequesterId = 0UL;
        }

        /// <summary>
        /// Refreshes the stable requester identifier used by the shared batched tool-ray lane.
        /// </summary>
        protected void RefreshQueuedRaycastRequesterId()
        {
            _queuedRaycastRequesterId = EntityId.ToULong(gameObject.GetEntityId());
        }

        /// <summary>
        /// Resolves a frame-latent shared batched raycast result for this tool instance.
        /// </summary>
        protected bool TryResolveQueuedRaycast(Vector3 origin, Vector3 direction, float range, int layerMask, QueryTriggerInteraction queryTriggerInteraction, out RaycastHit hit)
        {
            IInteractionSignalService interactionService = GlobalRegistry.InteractionSignals;
            if (interactionService != null && interactionService.IsInitialized)
            {
                if (_queuedRaycastRequesterId == 0UL)
                    RefreshQueuedRaycastRequesterId();

                return interactionService.TryRaycastPrimary(_queuedRaycastRequesterId, origin, direction, range, layerMask, queryTriggerInteraction, out hit);
            }

            hit = default;
            return false;
        }

        private void ResolveSwimContract()
        {
            _swimContractResolved = true;
            if (_swimContract == null)
                TryGetComponent(out _swimContract);
        }

        private void ResolveTransportFeelContract()
        {
            _transportFeelContractResolved = true;
            if (_transportFeelContract == null)
                TryGetComponent(out _transportFeelContract);
        }

        // ══════════════════════════════════════════════════════════
        //  TOOL LIFECYCLE — вызывается PlayerToolManager
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Вызывается при экипировке инструмента в руки игрока.
        /// Используй для: включения визуальных элементов,
        /// активации компонентов, запуска idle-анимации.
        /// </summary>
        public virtual void OnEquip()
        {
            LogLifecycleDebug(
                $"{GetType().Name}.OnEquip data={(_toolData != null ? _toolData.name : "null")} " +
                $"meta={(_toolMetadata != null ? _toolMetadata.name : "null")}");
            IsEquipped = true;
            _lowDurabilityWarningFired = false;

            // Subscribe to durability events
            var system = ToolDurabilitySystem.Instance;
            if (system != null && _toolMetadata != null)
            {
                system.OnToolBroken += HandleToolBroken;
            }
        }

        /// <summary>
        /// Вызывается при снятии инструмента из рук.
        /// Используй для: отключения VFX, остановки звуков,
        /// сброса состояния использования.
        /// </summary>
        public virtual void OnUnequip()
        {
            LogLifecycleDebug($"{GetType().Name}.OnUnequip");
            IsEquipped = false;

            // Unsubscribe from durability events
            var system = ToolDurabilitySystem.Instance;
            if (system != null && _toolMetadata != null)
            {
                system.OnToolBroken -= HandleToolBroken;
            }
        }

        /// <summary>
        /// Основное действие инструмента (ЛКМ / Fire1).
        /// v2.0 ENTERPRISE: автоматически применяет износ и энергопотребление.
        /// Вызывается каждый кадр, пока зажата кнопка.
        /// Пример: стрельба лазером, сканирование, размещение блока.
        /// </summary>
        /// <param name="deltaTime">Time.deltaTime — для frame-independent логики.</param>
        public virtual void UsePrimary(float deltaTime)
        {
            // v2.0 ENTERPRISE: Check if tool is broken
            if (IsBroken)
            {
                OnToolBrokenWhileUsing();
                return;
            }

            // v2.0 ENTERPRISE: Apply durability drain
            if (enableDurabilityDrain && _toolMetadata != null)
            {
                ApplyDurabilityDrain(deltaTime, true);
            }

            // v2.0 ENTERPRISE: Apply energy consumption
            if (enableEnergyConsumption && _toolMetadata != null && _survivalSystem != null)
            {
                ApplyEnergyConsumption(deltaTime);
            }

            _lastUseTime = Time.time;

            // v2.0 ENTERPRISE: Fire event
            OnToolUsed?.Invoke(true);

            // v2.0 ENTERPRISE: Check low durability
            CheckLowDurability();
        }

        /// <summary>
        /// Альтернативное действие инструмента (ПКМ / Fire2).
        /// v2.0 ENTERPRISE: автоматически применяет износ и энергопотребление.
        /// Вызывается каждый кадр, пока зажата кнопка.
        /// Пример: альт-режим резака, зум сканера, поворот блока.
        /// </summary>
        /// <param name="deltaTime">Time.deltaTime — для frame-independent логики.</param>
        public virtual void UseSecondary(float deltaTime)
        {
            // v2.0 ENTERPRISE: Check if tool is broken
            if (IsBroken)
            {
                OnToolBrokenWhileUsing();
                return;
            }

            // v2.0 ENTERPRISE: Apply durability drain (secondary rate)
            if (enableDurabilityDrain && _toolMetadata != null)
            {
                ApplyDurabilityDrain(deltaTime, false);
            }

            // v2.0 ENTERPRISE: Apply energy consumption (50% of primary)
            if (enableEnergyConsumption && _toolMetadata != null && _survivalSystem != null)
            {
                ApplyEnergyConsumption(deltaTime * 0.5f);
            }

            _lastUseTime = Time.time;

            // v2.0 ENTERPRISE: Fire event
            OnToolUsed?.Invoke(false);

            // v2.0 ENTERPRISE: Check low durability
            CheckLowDurability();
        }

        /// <summary>
        /// Вызывается каждый кадр через PlayerToolManager.Tick(),
        /// независимо от нажатия кнопок. Используй для: idle-анимации,
        /// покачивания, обновления UI индикаторов инструмента.
        /// </summary>
        /// <param name="deltaTime">Time.deltaTime.</param>
        public virtual void ToolTick(float deltaTime) { }

        /// <summary>
        /// Короткая сводка состояния активного инструмента для HUD/PDA.
        /// </summary>
        public virtual string GetOperationalSummary()
        {
            string toolName = GetOperationalToolName();

            if (!IsEquipped)
                return $"{toolName} // STANDBY";

            if (IsBroken)
                return $"{toolName} // BROKEN";

            if (_toolMetadata != null)
                return $"{toolName} // DUR {CurrentDurability:0}/{_toolMetadata.maxDurability:0}";

            return $"{toolName} // READY";
        }

        /// <summary>
        /// Что игроку сейчас делать с активным инструментом.
        /// </summary>
        public virtual string GetOperationalDirective()
        {
            if (IsBroken)
                return "Repair or replace the active tool before the next field action.";

            if (_toolMetadata != null && DurabilityNormalized <= 0.2f)
                return "Durability is low. Finish the current action and service the tool.";

            return "Tool is ready for the current field role.";
        }

        private string GetOperationalToolName()
        {
            if (string.IsNullOrEmpty(_cachedOperationalToolName))
                RefreshOperationalToolNameCache();

            return _cachedOperationalToolName;
        }

        private void RefreshOperationalToolNameCache()
        {
            _cachedOperationalToolName = _toolData != null && !string.IsNullOrWhiteSpace(_toolData.itemName)
                ? _toolData.itemName.ToUpperInvariant()
                : GetType().Name.ToUpperInvariant();
        }

        // ══════════════════════════════════════════════════════════
        //  PROTECTED — STAT MODIFIERS (v2.0 ENTERPRISE)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Возвращает итоговую эффективность инструмента с учётом upgrades.
        /// Используй в наследниках для масштабирования урона/скорости работы.
        /// </summary>
        protected float GetEfficiency()
        {
            if (_toolMetadata == null) return 1f;
            return _toolMetadata.GetTotalEfficiency();
        }

        /// <summary>
        /// Возвращает итоговую скорость инструмента с учётом upgrades.
        /// Используй в наследниках для масштабирования скорости анимации/действий.
        /// </summary>
        protected float GetSpeed()
        {
            if (_toolMetadata == null) return 1f;
            return _toolMetadata.GetTotalSpeed();
        }

        /// <summary>
        /// Возвращает итоговое энергопотребление с учётом upgrades.
        /// </summary>
        protected float GetEnergyConsumption()
        {
            if (_toolMetadata == null) return 0f;
            return _toolMetadata.GetTotalEnergyConsumption();
        }

        /// <summary>
        /// Returns a non-broken condition performance scale. Tools below 20% condition become less reliable before they fully fail.
        /// </summary>
        protected float GetConditionPerformanceScale()
        {
            if (_toolMetadata == null || IsBroken)
                return 1f;

            float durability = DurabilityNormalized;
            if (durability >= 0.2f)
                return 1f;

            return Mathf.Lerp(0.65f, 1f, durability / 0.2f);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — DURABILITY & ENERGY (v2.0 ENTERPRISE)
        // ══════════════════════════════════════════════════════════

        private void ApplyDurabilityDrain(float deltaTime, bool isPrimary)
        {
            var system = ToolDurabilitySystem.Instance;
            if (system == null || _toolMetadata == null) return;

            float drainRate = isPrimary
                ? _toolMetadata.durabilityDrainRate
                : _toolMetadata.durabilityDrainRateSecondary;

            // Apply upgrade modifiers
            float multiplier = 1f;
            for (int i = 0; i < _toolMetadata.maxUpgradeSlots && i < _toolMetadata.installedUpgrades.Length; i++)
            {
                var upgrade = _toolMetadata.installedUpgrades[i];
                if (upgrade != null)
                    multiplier *= upgrade.durabilityDrainMultiplier;
            }

            float drain = drainRate * multiplier * deltaTime;
            system.DrainDurability(_toolMetadata.toolID, drain, _toolMetadata.maxDurability);
        }

        private void ApplyEnergyConsumption(float deltaTime)
        {
            if (_survivalSystem == null || _toolMetadata == null) return;

            float consumption = GetEnergyConsumption() * deltaTime;
            int drainAmount = Mathf.FloorToInt(consumption);

            if (drainAmount > 0)
            {
                _survivalSystem.DrainEnergy(drainAmount);
            }
        }

        private void CheckLowDurability()
        {
            if (_lowDurabilityWarningFired) return;
            if (_toolMetadata == null) return;

            float percent = DurabilityNormalized * 100f;
            if (percent <= _toolMetadata.criticalDurabilityThreshold)
            {
                _lowDurabilityWarningFired = true;
                OnDurabilityLow?.Invoke();
            }
        }

        private void HandleToolBroken(string toolID)
        {
            if (_toolMetadata == null) return;
            if (_toolMetadata.toolID != toolID) return;

            OnToolBroken?.Invoke();
        }

        /// <summary>
        /// Вызывается когда игрок пытается использовать сломанный инструмент.
        /// Наследники могут переопределить для кастомной реакции (звук, VFX).
        /// </summary>
        protected virtual void OnToolBrokenWhileUsing()
        {
            // Default: do nothing (tool is blocked)
        }

        internal ToolMetadata RuntimeMetadata => _toolMetadata;

        internal bool WasRecentlyUsed(float maxIdleSeconds)
        {
            if (!IsEquipped)
                return false;

            return Time.time - _lastUseTime <= Mathf.Max(0.05f, maxIdleSeconds);
        }

        private void LogLifecycleDebug(string message)
        {
            if (!lifecycleDebugLogging)
                return;

            Debug.Log("[ToolLifecycle] " + message);
        }
    }
}
