// ============================================================================
// HECTON-8 — PlayerTool.cs  v2.1 ENTERPRISE
// Абстрактный базовый класс для всех инструментов игрока.
//
// v2.1 ENTERPRISE:
//   [ADD] Zero-GC Operational Summaries via FixedCharBuffer
//   [FIX] Cleaned up architecture and removed legacy duplication
// ============================================================================

namespace Hecton8.Gameplay
{
    using System;
    using UnityEngine;
    using Hecton8.Bootstrap;
    using Hecton8.Core;
    using Hecton8.Interaction;
    using Hecton8.Items;
    using Hecton8.Tools;

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
        [Tooltip("Ссылка на ItemData этого инструмента.")]
        [SerializeField] private ItemData _toolData;

        [Tooltip("Метаданные инструмента (durability, upgrades, stats).")]
        [SerializeField] private ToolMetadata _toolMetadata;

        [Header("── Settings ──────────────────────────────────")]
        [Tooltip("Включить автоматический износ при использовании.")]
        [SerializeField] private bool enableDurabilityDrain = true;

        [Tooltip("Включить энергопотребление при использовании.")]
        [SerializeField] private bool enableEnergyConsumption = true;

        [Tooltip("Включить подробные lifecycle-логи для диагностики.")]
        [SerializeField] private bool lifecycleDebugLogging = false;

        [Tooltip("Optional swim-presentation contract for near-camera hand ownership.")]
        [SerializeField] private PlayerToolSwimContract _swimContract;

        [Tooltip("Optional transport feel contract for audio and presentation.")]
        [SerializeField] private PlayerTransportFeelContract _transportFeelContract;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        public ItemData ToolData => _toolData;
        public ToolMetadata Metadata => _toolMetadata;
        public bool IsEquipped { get; private set; }

        public PlayerToolSwimContract SwimContract
        {
            get
            {
                if (!_swimContractResolved) ResolveSwimContract();
                return _swimContract;
            }
        }

        internal PlayerTransportFeelContract TransportFeelContract
        {
            get
            {
                if (!_transportFeelContractResolved) ResolveTransportFeelContract();
                return _transportFeelContract;
            }
        }

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

        public float DurabilityNormalized
        {
            get
            {
                if (_toolMetadata == null) return 1f;
                return CurrentDurability / Mathf.Max(1f, _toolMetadata.maxDurability);
            }
        }

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
        //  EVENTS
        // ══════════════════════════════════════════════════════════

        public event Action<bool> OnToolUsed;
        public event Action OnDurabilityLow;
        public event Action OnToolBroken;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        [NonSerialized] private HectonSurvivalSystem _survivalSystem;
        private bool _lowDurabilityWarningFired;
        private bool _swimContractResolved;
        private bool _transportFeelContractResolved;
        private string _cachedOperationalToolName;
        private float _lastUseTime = float.NegativeInfinity;
        private ulong _queuedRaycastRequesterId;

        // ══════════════════════════════════════════════════════════
        //  IPoolable
        // ══════════════════════════════════════════════════════════

        public virtual void OnSpawn()
        {
            LogLifecycleDebug($"{GetType().Name}.OnSpawn");
            IsEquipped = false;
            _lowDurabilityWarningFired = false;
            _lastUseTime = float.NegativeInfinity;
            RefreshQueuedRaycastRequesterId();
            RefreshOperationalToolNameCache();
            ResolveSwimContract();
            ResolveTransportFeelContract();

            if (_survivalSystem == null && enableEnergyConsumption)
            {
                if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform))
                    _survivalSystem = playerTransform.GetComponent<HectonSurvivalSystem>();
            }
        }

        public virtual void OnDespawn()
        {
            LogLifecycleDebug($"{GetType().Name}.OnDespawn");
            if (IsEquipped) OnUnequip();
            IsEquipped = false;
            _lowDurabilityWarningFired = false;
            _lastUseTime = float.NegativeInfinity;
            _cachedOperationalToolName = null;
            _queuedRaycastRequesterId = 0UL;
        }

        protected void RefreshQueuedRaycastRequesterId()
        {
            _queuedRaycastRequesterId = EntityId.ToULong(gameObject.GetEntityId());
        }

        protected bool TryResolveQueuedRaycast(Vector3 origin, Vector3 direction, float range, int layerMask, QueryTriggerInteraction qti, out RaycastHit hit)
        {
            IInteractionSignalService interactionService = GlobalRegistry.InteractionSignals;
            if (interactionService != null && interactionService.IsInitialized)
            {
                if (_queuedRaycastRequesterId == 0UL) RefreshQueuedRaycastRequesterId();
                return interactionService.TryRaycastPrimary(_queuedRaycastRequesterId, origin, direction, range, layerMask, qti, out hit);
            }
            hit = default;
            return false;
        }

        private void ResolveSwimContract() { _swimContractResolved = true; if (_swimContract == null) TryGetComponent(out _swimContract); }
        private void ResolveTransportFeelContract() { _transportFeelContractResolved = true; if (_transportFeelContract == null) TryGetComponent(out _transportFeelContract); }

        // ══════════════════════════════════════════════════════════
        //  TOOL LIFECYCLE
        // ══════════════════════════════════════════════════════════

        public virtual void OnEquip()
        {
            IsEquipped = true;
            _lowDurabilityWarningFired = false;
            var system = ToolDurabilitySystem.Instance;
            if (system != null && _toolMetadata != null) system.OnToolBroken += HandleToolBroken;
        }

        public virtual void OnUnequip()
        {
            IsEquipped = false;
            var system = ToolDurabilitySystem.Instance;
            if (system != null && _toolMetadata != null) system.OnToolBroken -= HandleToolBroken;
        }

        public virtual void UsePrimary(float deltaTime)
        {
            if (IsBroken) { OnToolBrokenWhileUsing(); return; }
            if (enableDurabilityDrain && _toolMetadata != null) ApplyDurabilityDrain(deltaTime, true);
            if (enableEnergyConsumption && _toolMetadata != null && _survivalSystem != null) ApplyEnergyConsumption(deltaTime);
            _lastUseTime = Time.time;
            OnToolUsed?.Invoke(true);
            CheckLowDurability();
        }

        public virtual void UseSecondary(float deltaTime)
        {
            if (IsBroken) { OnToolBrokenWhileUsing(); return; }
            if (enableDurabilityDrain && _toolMetadata != null) ApplyDurabilityDrain(deltaTime, false);
            if (enableEnergyConsumption && _toolMetadata != null && _survivalSystem != null) ApplyEnergyConsumption(deltaTime * 0.5f);
            _lastUseTime = Time.time;
            OnToolUsed?.Invoke(false);
            CheckLowDurability();
        }

        public virtual void ToolTick(float deltaTime) { }

        // ══════════════════════════════════════════════════════════
        //  OPERATIONAL SUMMARIES (ZERO-GC)
        // ══════════════════════════════════════════════════════════

        public virtual string GetOperationalSummary()
        {
            string toolName = GetOperationalToolName();
            if (!IsEquipped) return $"{toolName} // STANDBY";
            if (IsBroken) return $"{toolName} // BROKEN";
            if (_toolMetadata != null) return $"{toolName} // DUR {(int)CurrentDurability}/{(int)_toolMetadata.maxDurability}";
            return $"{toolName} // READY";
        }

        public virtual void WriteOperationalSummary(FixedCharBuffer buffer)
        {
            buffer.Append(GetOperationalToolName());
            if (!IsEquipped) { buffer.Append(" // STANDBY"); return; }
            if (IsBroken) { buffer.Append(" // BROKEN"); return; }
            if (_toolMetadata != null)
            {
                buffer.Append(" // DUR ");
                buffer.AppendInt((int)CurrentDurability);
                buffer.Append("/");
                buffer.AppendInt((int)_toolMetadata.maxDurability);
                return;
            }
            buffer.Append(" // READY");
        }

        public virtual string GetOperationalDirective()
        {
            if (IsBroken) return "Repair or replace the active tool before the next field action.";
            if (_toolMetadata != null && DurabilityNormalized <= 0.2f) return "Durability is low. Finish the current action and service the tool.";
            return "Tool is ready for the current field role.";
        }

        private string GetOperationalToolName()
        {
            if (string.IsNullOrEmpty(_cachedOperationalToolName)) RefreshOperationalToolNameCache();
            return _cachedOperationalToolName;
        }

        private void RefreshOperationalToolNameCache()
        {
            _cachedOperationalToolName = _toolData != null && !string.IsNullOrWhiteSpace(_toolData.itemName)
                ? _toolData.itemName.ToUpperInvariant()
                : GetType().Name.ToUpperInvariant();
        }

        // ══════════════════════════════════════════════════════════
        //  PROTECTED — STAT MODIFIERS
        // ══════════════════════════════════════════════════════════

        protected float GetEfficiency() => _toolMetadata == null ? 1f : _toolMetadata.GetTotalEfficiency();
        protected float GetSpeed() => _toolMetadata == null ? 1f : _toolMetadata.GetTotalSpeed();
        protected float GetEnergyConsumption() => _toolMetadata == null ? 0f : _toolMetadata.GetTotalEnergyConsumption();

        protected float GetConditionPerformanceScale()
        {
            if (_toolMetadata == null || IsBroken) return 1f;
            float durability = DurabilityNormalized;
            if (durability >= 0.2f) return 1f;
            return Mathf.Lerp(0.65f, 1f, durability / 0.2f);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — DURABILITY & ENERGY
        // ══════════════════════════════════════════════════════════

        private void ApplyDurabilityDrain(float deltaTime, bool isPrimary)
        {
            var system = ToolDurabilitySystem.Instance;
            if (system == null || _toolMetadata == null) return;
            float drainRate = isPrimary ? _toolMetadata.durabilityDrainRate : _toolMetadata.durabilityDrainRateSecondary;
            float multiplier = 1f;
            for (int i = 0; i < _toolMetadata.maxUpgradeSlots && i < _toolMetadata.installedUpgrades.Length; i++)
            {
                var upgrade = _toolMetadata.installedUpgrades[i];
                if (upgrade != null) multiplier *= upgrade.durabilityDrainMultiplier;
            }
            system.DrainDurability(_toolMetadata.toolID, drainRate * multiplier * deltaTime, _toolMetadata.maxDurability);
        }

        private void ApplyEnergyConsumption(float deltaTime)
        {
            if (_survivalSystem == null || _toolMetadata == null) return;
            int drainAmount = Mathf.FloorToInt(GetEnergyConsumption() * deltaTime);
            if (drainAmount > 0) _survivalSystem.DrainEnergy(drainAmount);
        }

        private void CheckLowDurability()
        {
            if (_lowDurabilityWarningFired || _toolMetadata == null) return;
            if (DurabilityNormalized * 100f <= _toolMetadata.criticalDurabilityThreshold)
            {
                _lowDurabilityWarningFired = true;
                OnDurabilityLow?.Invoke();
            }
        }

        private void HandleToolBroken(string toolID)
        {
            if (_toolMetadata != null && _toolMetadata.toolID == toolID) OnToolBroken?.Invoke();
        }

        protected virtual void OnToolBrokenWhileUsing() { }

        internal ToolMetadata RuntimeMetadata => _toolMetadata;
        internal bool WasRecentlyUsed(float maxIdleSeconds) => IsEquipped && (Time.time - _lastUseTime <= Mathf.Max(0.05f, maxIdleSeconds));
        private void LogLifecycleDebug(string message) { if (lifecycleDebugLogging) Debug.Log("[ToolLifecycle] " + message); }
    }
}
