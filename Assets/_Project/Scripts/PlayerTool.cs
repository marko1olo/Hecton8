// ============================================================================
// HECTON-8 — PlayerTool.cs  v2.1 ENTERPRISE
// Abstraktnyy bazovyy klass dlya vseh instrumentov igroka.
//
// v2.1 ENTERPRISE:
//   [ADD] Zero-GC Operational Summaries via FixedCharBuffer
//   [FIX] Cleaned up architecture and removed legacy duplication
// ============================================================================

namespace Hecton8.Gameplay
{
    using System;
    using Hecton.Localization;
    using Unity.Mathematics;
    using UnityEngine;
    using Hecton8.Bootstrap;
    using Hecton8.Core;
    using Hecton8.Inventory;
    using Hecton8.Interaction;
    using Hecton8.Items;
    using Hecton8.Physics;
    using Hecton8.Tools;

    /// <summary>
    /// Bazovyy klass dlya vseh instrumentov, kotorye igrok
    /// mozhet derzhat v rukah. Upravlyaetsya cherez <see cref="PlayerToolManager"/>.
    /// </summary>
    public abstract class PlayerTool : MonoBehaviour, IPoolable
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Tool Identity ─────────────────────────────")]
        [Tooltip("Ssylka na ItemData etogo instrumenta.")]
        [SerializeField] private ItemData _toolData;

        [Tooltip("Metadannye instrumenta (durability, upgrades, stats).")]
        [SerializeField] private ToolMetadata _toolMetadata;

        [Header("── Settings ──────────────────────────────────")]
        [Tooltip("Vklyuchit avtomaticheskiy iznos pri ispolzovanii.")]
        [SerializeField] private bool enableDurabilityDrain = true;

        [Tooltip("Vklyuchit energopotreblenie pri ispolzovanii.")]
        [SerializeField] private bool enableEnergyConsumption = true;

        [Tooltip("Vklyuchit podrobnye lifecycle-logi dlya diagnostiki.")]
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
                var system = Hecton8.Core.GlobalRegistry.ToolDurability;
                if (system == null) return _toolMetadata.maxDurability;
                return system.GetDurability(_toolMetadata.toolID, _toolMetadata.maxDurability);
            }
        }

        public float DurabilityNormalized
        {
            get
            {
                if (_toolMetadata == null) return 1f;
                return CurrentDurability / math.max(1f, _toolMetadata.maxDurability);
            }
        }

        public bool IsBroken
        {
            get
            {
                if (_toolMetadata == null) return false;
                var system = Hecton8.Core.GlobalRegistry.ToolDurability;
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
        private uint _runtimeToolId;
        private uint _cachedToolItemHashId;
        private bool _runtimeToolRegistered;
        private Transform _cachedBaseTransform;
        private FixedCharBuffer _legacyOperationalBuffer = new FixedCharBuffer(256); // COLD ALLOC: char[256] - legacy string bridge for non-HUD callers - owner: PlayerTool

        // ══════════════════════════════════════════════════════════
        //  IPoolable
        // ══════════════════════════════════════════════════════════

        public virtual void OnSpawn()
        {
            if (lifecycleDebugLogging)
                LogLifecycleDebug(nameof(OnSpawn));
            IsEquipped = false;
            _lowDurabilityWarningFired = false;
            _lastUseTime = float.NegativeInfinity;
            RefreshQueuedRaycastRequesterId();
            RefreshOperationalToolNameCache();
            CacheToolItemHash();
            ResolveSwimContract();
            ResolveTransportFeelContract();

            if (_survivalSystem == null && enableEnergyConsumption)
            {
                if (GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform))
                    playerTransform.TryGetComponent(out _survivalSystem);
            }

            EnsureModularRuntimeRegistration();
            SyncModularBattery();
            SyncModularHeat(ResolveModularHeatNormalized());
            SyncModularDurability();
        }

        public virtual void OnDespawn()
        {
            if (lifecycleDebugLogging)
                LogLifecycleDebug(nameof(OnDespawn));
            if (IsEquipped) OnUnequip();
            UnregisterModularRuntime();
            IsEquipped = false;
            _lowDurabilityWarningFired = false;
            _lastUseTime = float.NegativeInfinity;
            _cachedOperationalToolName = null;
            _queuedRaycastRequesterId = 0UL;
            _runtimeToolId = 0u;
            _cachedToolItemHashId = 0u;
            _runtimeToolRegistered = false;
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
                Vector3 normalizedDirection = NormalizeOrCachedForward(direction);
                double3 absoluteOrigin = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(origin);
                InteractionPacket packet = new InteractionPacket(
                    ResolveRuntimeToolId(),
                    new Unity.Mathematics.float3((float)absoluteOrigin.x, (float)absoluteOrigin.y, (float)absoluteOrigin.z),
                    new Unity.Mathematics.float3(normalizedDirection.x, normalizedDirection.y, normalizedDirection.z),
                    GetRuntimePowerScalar(1f),
                    range,
                    (byte)ToolActionMode.Primary,
                    (byte)(IsEquipped ? ToolStateBits.Active : ToolStateBits.Idle),
                    unchecked((uint)Time.frameCount));
                return interactionService.TryRaycastPrimary(_queuedRaycastRequesterId, in packet, layerMask, qti, out hit);
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
            var system = Hecton8.Core.GlobalRegistry.ToolDurability;
            if (system != null && _toolMetadata != null) system.OnToolBroken += HandleToolBroken;
            EnsureModularRuntimeRegistration();
            SyncModularHeat(ResolveModularHeatNormalized());
            SyncModularDurability();
        }

        public virtual void OnUnequip()
        {
            IsEquipped = false;
            var system = Hecton8.Core.GlobalRegistry.ToolDurability;
            if (system != null && _toolMetadata != null) system.OnToolBroken -= HandleToolBroken;
        }

        public virtual void UsePrimary(float deltaTime)
        {
            TryBeginToolUse(deltaTime, true);
        }

        public virtual void UseSecondary(float deltaTime)
        {
            TryBeginToolUse(deltaTime, false);
        }

        public virtual void ToolTick(float deltaTime) { }

        // ══════════════════════════════════════════════════════════
        //  OPERATIONAL SUMMARIES (ZERO-GC)
        // ══════════════════════════════════════════════════════════

        public virtual string GetOperationalSummary()
        {
            _legacyOperationalBuffer.Clear();
            WriteOperationalSummary(ref _legacyOperationalBuffer);
            return _legacyOperationalBuffer.ToString();
        }

        public virtual void WriteOperationalSummary(ref FixedCharBuffer buffer)
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
            _legacyOperationalBuffer.Clear();
            WriteOperationalDirective(ref _legacyOperationalBuffer);
            return _legacyOperationalBuffer.ToString();
        }

        public virtual void WriteOperationalDirective(ref FixedCharBuffer buffer)
        {
            if (IsBroken)
            {
                buffer.Append("Repair or replace the active tool before the next field action.");
                return;
            }

            if (_toolMetadata != null && DurabilityNormalized <= 0.2f)
            {
                buffer.Append("Durability is low. Finish the current action and service the tool.");
                return;
            }

            buffer.Append("Tool is ready for the current field role.");
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

        protected float GetEfficiency()
        {
            float fallback = _toolMetadata == null ? 1f : _toolMetadata.GetTotalEfficiency();
            return TryGetModularEquipment(out IModularEquipmentService service) && _runtimeToolRegistered
                ? service.GetEfficiencyScalar(_runtimeToolId, fallback)
                : fallback;
        }

        protected float GetSpeed()
        {
            float fallback = _toolMetadata == null ? 1f : _toolMetadata.GetTotalSpeed();
            return TryGetModularEquipment(out IModularEquipmentService service) && _runtimeToolRegistered
                ? service.GetSpeedScalar(_runtimeToolId, fallback)
                : fallback;
        }

        protected float GetEnergyConsumption()
        {
            float fallback = _toolMetadata == null ? 0f : _toolMetadata.GetTotalEnergyConsumption();
            return TryGetModularEquipment(out IModularEquipmentService service) && _runtimeToolRegistered
                ? service.GetBatteryDrainPerSecond(_runtimeToolId, fallback)
                : fallback;
        }

        protected float GetRuntimeMaxRange(float fallback)
        {
            return TryGetModularEquipment(out IModularEquipmentService service) && _runtimeToolRegistered
                ? service.GetMaxRange(_runtimeToolId, fallback)
                : fallback;
        }

        protected float GetRuntimePowerScalar(float fallback)
        {
            return TryGetModularEquipment(out IModularEquipmentService service) && _runtimeToolRegistered
                ? service.GetPowerScalar(_runtimeToolId, fallback)
                : fallback;
        }

        protected float GetRuntimeHeatGenerationRate(float fallback)
        {
            return TryGetModularEquipment(out IModularEquipmentService service) && _runtimeToolRegistered
                ? service.GetHeatGenerationRate(_runtimeToolId, fallback)
                : fallback;
        }

        protected float GetRuntimeCooldownRate(float fallback)
        {
            return TryGetModularEquipment(out IModularEquipmentService service) && _runtimeToolRegistered
                ? service.GetCooldownRate(_runtimeToolId, fallback)
                : fallback;
        }

        protected float GetRuntimeRecoilImpulse(float fallback)
        {
            return TryGetModularEquipment(out IModularEquipmentService service) && _runtimeToolRegistered
                ? service.GetRecoilImpulse(_runtimeToolId, fallback)
                : fallback;
        }

        protected float GetRuntimeBatteryNormalized(float fallback)
        {
            return TryGetModularEquipment(out IModularEquipmentService service) && _runtimeToolRegistered
                ? service.GetBatteryNormalized(_runtimeToolId, fallback)
                : fallback;
        }

        protected bool HasModularUpgrade(ToolUpgradeBits flag)
        {
            return TryGetModularEquipment(out IModularEquipmentService service) &&
                   _runtimeToolRegistered &&
                   service.HasUpgrade(_runtimeToolId, flag);
        }

        protected bool TryGetWirelessBrownoutFlicker(out float flickerScalar)
        {
            flickerScalar = 0f;
            if (!_runtimeToolRegistered || !(GlobalRegistry.ModularEquipment is ModularEquipmentEngine runtime))
                return false;

            return runtime.TryGetWirelessBrownoutFeedback(_runtimeToolId, out flickerScalar);
        }

        protected bool TryGetToolBrownoutFlicker(out float flickerScalar)
        {
            flickerScalar = 0f;
            if (!_runtimeToolRegistered || !(GlobalRegistry.ModularEquipment is ModularEquipmentEngine runtime))
                return false;

            return runtime.TryGetToolBrownoutFeedback(_runtimeToolId, out flickerScalar);
        }

        protected bool HasToolEnergyOrWirelessPath()
        {
            if (GetRuntimeBatteryNormalized(0f) > 0.0001f)
                return true;

            return HasModularUpgrade(ToolUpgradeBits.WirelessCharging) &&
                   GlobalRegistry.Submarine != null &&
                   GlobalRegistry.Submarine.AtmosphereSystem != null &&
                   GlobalRegistry.PowerGrid != null;
        }

        protected float GetConditionPerformanceScale()
        {
            if (_toolMetadata == null || IsBroken) return 1f;
            float durability = DurabilityNormalized;
            if (durability >= 0.2f) return 1f;
            return math.lerp(0.65f, 1f, durability / 0.2f);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — DURABILITY & ENERGY
        // ══════════════════════════════════════════════════════════

        private void ApplyDurabilityDrain(float deltaTime, bool isPrimary)
        {
            var system = Hecton8.Core.GlobalRegistry.ToolDurability;
            if (system == null || _toolMetadata == null) return;
            float drainRate = isPrimary ? _toolMetadata.durabilityDrainRate : _toolMetadata.durabilityDrainRateSecondary;
            float multiplier = TryGetModularEquipment(out IModularEquipmentService service) && _runtimeToolRegistered
                ? service.GetDurabilityDrainMultiplier(_runtimeToolId, 1f)
                : 1f;
            system.DrainDurabilityByTime(_toolMetadata.toolID, ResolveToolItemHash(), drainRate * multiplier * deltaTime, _toolMetadata.maxDurability);
            SyncModularDurability();
        }

        private void ApplyEnergyConsumption(float deltaTime)
        {
            TryConsumeRuntimeEnergy(deltaTime);
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

        internal ToolRuntimeProfile BuildModularRuntimeProfile()
        {
            ToolRuntimeProfile profile = new ToolRuntimeProfile
            {
                ToolId = ResolveRuntimeToolId(),
                MaxRange = 1f,
                PowerScalar = 1f,
                EfficiencyScalar = _toolMetadata != null ? math.max(0.1f, _toolMetadata.efficiency) : 1f,
                SpeedScalar = _toolMetadata != null ? math.max(0.1f, _toolMetadata.speed) : 1f,
                HeatGenerationRate = _toolMetadata != null ? math.max(0f, _toolMetadata.authoredHeatGenerationRate) : 0f,
                CooldownRate = _toolMetadata != null ? math.max(0f, _toolMetadata.authoredCooldownRate) : 0f,
                BatteryCapacity = 1f,
                BatteryDrainPerSecond = _toolMetadata != null ? math.max(0f, _toolMetadata.energyConsumptionRate) : 0f,
                DurabilityDrainMultiplier = 1f,
                RecoilImpulse = _toolMetadata != null ? math.max(0f, _toolMetadata.authoredRecoilImpulse) : 0f,
                ModuleSlotCount = (byte)math.clamp(_toolMetadata != null ? _toolMetadata.maxUpgradeSlots : 0, 0, ToolUpgradeSystem.MaxModuleSlots)
            };

            ConfigureModularRuntimeProfile(ref profile);
            return profile;
        }

        internal int CopyAuthoredModules(ToolModuleData[] destination)
        {
            if (destination == null || destination.Length == 0 || _toolMetadata == null)
                return 0;

            for (int i = 0; i < destination.Length; i++)
                destination[i] = null;

            return _toolMetadata.CopyDefaultModules(destination);
        }

        internal virtual float ResolveModularBatteryNormalized()
        {
            return GetRuntimeBatteryNormalized(1f);
        }

        internal virtual float ResolveModularHeatNormalized()
        {
            return 0f;
        }

        protected virtual void ConfigureModularRuntimeProfile(ref ToolRuntimeProfile profile) { }

        protected void SyncModularHeat(float normalizedHeat)
        {
            if (TryGetModularEquipment(out IModularEquipmentService service) && _runtimeToolRegistered)
                service.SetHeat(_runtimeToolId, normalizedHeat);
        }

        protected void SyncModularBattery()
        {
            if (TryGetModularEquipment(out IModularEquipmentService service) && _runtimeToolRegistered)
                service.SetBattery(_runtimeToolId, ResolveModularBatteryNormalized());
        }

        protected void SetRuntimeBatteryNormalized(float normalizedBattery)
        {
            if (TryGetModularEquipment(out IModularEquipmentService service) && _runtimeToolRegistered)
                service.SetBattery(_runtimeToolId, normalizedBattery);
        }

        protected bool TryConsumeRuntimeEnergy(float deltaTime)
        {
            if (_toolMetadata == null || !TryGetModularEquipment(out IModularEquipmentService service) || !_runtimeToolRegistered)
                return false;

            if (service.TryGetToolState(_runtimeToolId, out ToolState runtimeState) &&
                (runtimeState.StatusMask & ToolRuntimeStatusMasks.Disabled) != 0u)
            {
                return false;
            }

            float safeDeltaTime = math.max(0f, deltaTime);
            float requestedDrain = GetEnergyConsumption() * safeDeltaTime;
            if (requestedDrain <= 0f)
                return true;

            float remainingDrain = requestedDrain;
            if (HasModularUpgrade(ToolUpgradeBits.WirelessCharging) &&
                GlobalRegistry.Submarine != null &&
                GlobalRegistry.Submarine.AtmosphereSystem != null &&
                GlobalRegistry.PowerGrid != null &&
                GlobalRegistry.PowerGrid.TryQueueWirelessToolDrain(requestedDrain, out float grantedDrain))
            {
                remainingDrain = math.max(0f, requestedDrain - grantedDrain);
            }

            if (remainingDrain <= 0f)
                return true;

            float batteryBefore = service.GetBatteryNormalized(_runtimeToolId, 0f);
            float remainingDrainRate = safeDeltaTime > 0f ? remainingDrain / safeDeltaTime : 0f;
            service.ConsumeBattery(_runtimeToolId, remainingDrainRate, safeDeltaTime);
            return batteryBefore + 0.0001f >= remainingDrain;
        }

        protected void SyncModularDurability()
        {
            if (TryGetModularEquipment(out IModularEquipmentService service) && _runtimeToolRegistered)
                service.SetDurability(_runtimeToolId, DurabilityNormalized);
        }

        protected bool TryQueuePlayerToolRecoil(Vector3 usageDirection, float impulseMagnitude)
        {
            if (impulseMagnitude <= 0.0001f)
                return false;

            Vector3 safeDirection = NormalizeOrCachedForward(usageDirection);
            if (ToolHitUtility.TryApplyRelativeCarrierImpulse(safeDirection, impulseMagnitude))
                return true;

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            Rigidbody playerBody = playerContext != null ? playerContext.PlayerRigidbody : null;
            if (playerBody == null)
                return false;

            return PhysicsForceRouter.QueueForce(playerBody, -safeDirection * impulseMagnitude, ForceMode.Impulse);
        }

        private Vector3 NormalizeOrCachedForward(Vector3 direction)
        {
            float sqrMagnitude = direction.sqrMagnitude;
            if (sqrMagnitude > 0.0001f)
            {
                if (math.abs(sqrMagnitude - 1f) <= 0.02f)
                    return direction;

                return direction * math.rsqrt(sqrMagnitude);
            }

            if (_cachedBaseTransform == null)
                _cachedBaseTransform = transform;

            return _cachedBaseTransform != null ? _cachedBaseTransform.forward : Vector3.forward;
        }

        protected void QueueToolHapticFeedback(float powerDelivered, float ratedPower, byte priority = 1)
        {
            ToolHapticsRuntime.EnqueueToolFeedback(powerDelivered, ratedPower, priority);
        }

        protected bool TryBeginToolUse(float deltaTime, bool isPrimary)
        {
            if (IsBroken)
            {
                OnToolBrokenWhileUsing();
                return false;
            }

            if (_runtimeToolRegistered &&
                TryGetModularEquipment(out IModularEquipmentService service) &&
                service.TryGetToolState(_runtimeToolId, out ToolState runtimeState) &&
                (runtimeState.StatusMask & ToolRuntimeStatusMasks.Disabled) != 0u)
            {
                return false;
            }

            if (enableEnergyConsumption && _toolMetadata != null)
            {
                float energyDeltaTime = isPrimary ? deltaTime : deltaTime * 0.5f;
                if (!HasToolEnergyOrWirelessPath() || !TryConsumeRuntimeEnergy(energyDeltaTime))
                    return false;
            }

            if (enableDurabilityDrain && _toolMetadata != null)
                ApplyDurabilityDrain(deltaTime, isPrimary);

            _lastUseTime = Time.time;
            OnToolUsed?.Invoke(isPrimary);
            CheckLowDurability();
            return true;
        }

        internal bool IsRuntimeOverchargeRequested()
        {
            IInputService inputService = GlobalRegistry.Input;
            if (inputService == null || !inputService.IsPlayerInputEnabled || !IsEquipped)
                return false;

            PlayerInputState inputState = inputService.GetState();
            return inputState.HasAction(PlayerInputAction.PrimaryFire) &&
                   inputState.HasAction(PlayerInputAction.Sprint);
        }

        internal void HandleRuntimeOverchargeFailure(float playerDamage)
        {
            if (_toolData != null)
            {
                int toolHashId = LocHash.Compute(_toolData.PersistentId);
                if (toolHashId != 0)
                    Hecton8.Core.GlobalRegistry.PlayerInventoryRuntime?.TryRemoveFirstMatchingItemByHash(toolHashId);
            }

            HectonPlayerHealth playerHealth = null;
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext != null && playerContext.PlayerTransform != null)
                playerContext.PlayerTransform.TryGetComponent(out playerHealth);

            if (playerHealth != null)
                playerHealth.TakeDamage(math.max(0f, playerDamage), true);
        }

        private uint ResolveRuntimeToolId()
        {
            if (_runtimeToolId != 0u)
                return _runtimeToolId;

            string toolIdSource = _toolMetadata != null && !string.IsNullOrWhiteSpace(_toolMetadata.toolID)
                ? _toolMetadata.toolID
                : (_toolData != null && !string.IsNullOrWhiteSpace(_toolData.itemName) ? _toolData.itemName : GetType().Name);
            _runtimeToolId = unchecked((uint)Animator.StringToHash(toolIdSource));
            return _runtimeToolId;
        }

        private void EnsureModularRuntimeRegistration()
        {
            if (_runtimeToolRegistered)
                return;

            ModularEquipmentEngine runtime = ModularEquipmentEngine.EnsureRuntimeInstance();
            if (runtime == null)
                return;

            runtime.InitializeService();

            if (!TryGetModularEquipment(out IModularEquipmentService service))
                return;

            _runtimeToolId = service.RegisterTool(this);
            _runtimeToolRegistered = _runtimeToolId != 0u;
        }

        private void UnregisterModularRuntime()
        {
            if (!_runtimeToolRegistered)
                return;

            if (TryGetModularEquipment(out IModularEquipmentService service))
                service.UnregisterTool(this, _runtimeToolId);

            _runtimeToolRegistered = false;
        }

        private bool TryGetModularEquipment(out IModularEquipmentService service)
        {
            service = GlobalRegistry.ModularEquipment;
            return service != null && service.IsInitialized;
        }

        internal ToolMetadata RuntimeMetadata => _toolMetadata;
        internal uint RuntimeToolId => _runtimeToolId;
        internal bool WasRecentlyUsed(float maxIdleSeconds) => IsEquipped && (Time.time - _lastUseTime <= math.max(0.05f, maxIdleSeconds));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void LogLifecycleDebug(string message) { if (lifecycleDebugLogging) Debug.Log("[ToolLifecycle] " + message); }
#else
        private void LogLifecycleDebug(string message) { }
#endif

        private void CacheToolItemHash()
        {
            _cachedToolItemHashId = _toolData != null
                ? unchecked((uint)LocHash.Compute(_toolData.PersistentId))
                : 0u;
        }

        private uint ResolveToolItemHash()
        {
            if (_cachedToolItemHashId != 0u)
                return _cachedToolItemHashId;

            CacheToolItemHash();
            return _cachedToolItemHashId != 0u
                ? _cachedToolItemHashId
                : unchecked((uint)Animator.StringToHash(_toolMetadata != null ? _toolMetadata.toolID : GetType().Name));
        }
    }
}
