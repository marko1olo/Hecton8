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
    public abstract class PlayerTool : MonoBehaviour, IPoolable, IGlobalRegistryHotSwapListener, IGlobalRegistryHotSwapRefListener
    {
        private const uint ToolLifecycleTelemetryHash = 0x544C4946u; // TLIF
        private const uint ToolLifecycleSpawnHash = 0x544C5350u; // TLSP
        private const uint ToolLifecycleDespawnHash = 0x544C4453u; // TLDS
        private const float RuntimeActiveIntentHoldSeconds = 0.075f;

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
                ToolDurabilitySystem system = _toolDurabilityService;
                if (system == null) return _toolMetadata.maxDurability;
                return system.GetDurability(_toolMetadata.toolID, _toolMetadata.maxDurability);
            }
        }

        public float DurabilityNormalized
        {
            get
            {
                if (_toolMetadata == null) return 1f;
                return math.saturate(FiniteNonNegativeOrZero(CurrentDurability) / FiniteAtLeast(_toolMetadata.maxDurability, 1f));
            }
        }

        public bool IsBroken
        {
            get
            {
                if (_toolMetadata == null) return false;
                ToolDurabilitySystem system = _toolDurabilityService;
                if (system == null) return false;
                return system.IsBroken(_toolMetadata.toolID);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  EVENTS
        // ══════════════════════════════════════════════════════════

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private bool _lowDurabilityWarningFired;
        private bool _swimContractResolved;
        private bool _transportFeelContractResolved;
        private string _cachedOperationalToolName;
        private float _lastUseTime = float.NegativeInfinity;
        private float _runtimeActiveIntentSeconds;
        private ulong _queuedRaycastRequesterId;
        private uint _runtimeToolId;
        private uint _runtimeToolSpecHashId;
        private uint _cachedToolItemHashId;
        private bool _runtimeToolRegistered;
        private bool _modularHotSwapRegistered;
        private bool _lastUseWasPrimary;
        private Transform _cachedBaseTransform;
        private IModularEquipmentService _modularEquipmentService;
        private IPowerGridService _powerGridService;
        private ISubmarineRuntimeContext _submarineRuntimeContext;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private IPlayerInventoryService _playerInventoryService;
        private IInputService _inputService;
        private IInteractionSignalService _interactionSignalService;
        private ToolDurabilitySystem _toolDurabilityService;
        private FixedCharBuffer _legacyOperationalBuffer = new FixedCharBuffer(256); // COLD ALLOC: char[256] - legacy string bridge for non-HUD callers - owner: PlayerTool

        // ══════════════════════════════════════════════════════════
        //  IPoolable
        // ══════════════════════════════════════════════════════════

        public virtual void OnSpawn()
        {
            if (lifecycleDebugLogging)
                PublishLifecycleDebug(ToolLifecycleSpawnHash);
            IsEquipped = false;
            _lowDurabilityWarningFired = false;
            _lastUseTime = float.NegativeInfinity;
            _runtimeActiveIntentSeconds = 0f;
            _lastUseWasPrimary = false;
            RefreshQueuedRaycastRequesterId();
            RefreshOperationalToolNameCache();
            CacheToolItemHash();
            ResolveSwimContract();
            ResolveTransportFeelContract();
            CacheToolRegistryDependenciesCold();
            TryRegisterModularHotSwap();

            EnsureModularRuntimeRegistration();
            SyncModularBattery();
            SyncModularHeat(ResolveModularHeatNormalized());
            SyncModularDurability();
        }

        public virtual void OnDespawn()
        {
            if (lifecycleDebugLogging)
                PublishLifecycleDebug(ToolLifecycleDespawnHash);
            if (IsEquipped) OnUnequip();
            UnregisterModularRuntime();
            IsEquipped = false;
            _lowDurabilityWarningFired = false;
            _lastUseTime = float.NegativeInfinity;
            _runtimeActiveIntentSeconds = 0f;
            _lastUseWasPrimary = false;
            _cachedOperationalToolName = null;
            _queuedRaycastRequesterId = 0UL;
            _runtimeToolId = 0u;
            _runtimeToolSpecHashId = 0u;
            _cachedToolItemHashId = 0u;
            _runtimeToolRegistered = false;
            TryUnregisterModularHotSwap();
            _modularEquipmentService = null;
            _powerGridService = null;
            _submarineRuntimeContext = null;
            _playerRuntimeContext = null;
            _playerInventoryService = null;
            _inputService = null;
            _interactionSignalService = null;
            _toolDurabilityService = null;
        }

        protected void RefreshQueuedRaycastRequesterId()
        {
            _queuedRaycastRequesterId = EntityId.ToULong(gameObject.GetEntityId());
        }

        protected bool TryResolveQueuedRaycast(Vector3 origin, Vector3 direction, float range, int layerMask, QueryTriggerInteraction qti, out RaycastHit hit)
        {
            IInteractionSignalService interactionService = _interactionSignalService;
            if (interactionService != null && interactionService.IsInitialized)
            {
                if (_queuedRaycastRequesterId == 0UL) RefreshQueuedRaycastRequesterId();
                float safeRange = FiniteNonNegativeOrZero(range);
                if (!IsFiniteVector(origin) || safeRange <= 0f)
                {
                    hit = default;
                    return false;
                }

                Vector3 normalizedDirection = NormalizeOrCachedForward(direction);
                double3 absoluteOrigin = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(origin);
                if (!IsFiniteVector(normalizedDirection) || !math.all(math.isfinite(absoluteOrigin)))
                {
                    hit = default;
                    return false;
                }

                float safePower = math.saturate(FiniteNonNegativeOrZero(GetRuntimePowerScalar(1f)));
                InteractionPacket packet = new InteractionPacket(
                    ResolveRuntimeToolId(),
                    new Unity.Mathematics.float3((float)absoluteOrigin.x, (float)absoluteOrigin.y, (float)absoluteOrigin.z),
                    new Unity.Mathematics.float3(normalizedDirection.x, normalizedDirection.y, normalizedDirection.z),
                    safePower,
                    safeRange,
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
            EnsureModularRuntimeRegistration();
            SyncModularHeat(ResolveModularHeatNormalized());
            SyncModularDurability();
        }

        public virtual void OnUnequip()
        {
            IsEquipped = false;
            _runtimeActiveIntentSeconds = 0f;
            if (TryGetModularEquipment(out IModularEquipmentService service) && _runtimeToolRegistered)
                service.SetToolActive(_runtimeToolId, false);
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

        internal void AdvanceRuntimeActiveIntent(float deltaTime)
        {
            if (_runtimeActiveIntentSeconds <= 0f)
                return;

            _runtimeActiveIntentSeconds = math.max(0f, _runtimeActiveIntentSeconds - FiniteNonNegativeOrZero(deltaTime));
        }

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
            float value = TryGetModularEquipment(out IModularEquipmentService service) && _runtimeToolRegistered
                ? service.GetEfficiencyScalar(_runtimeToolId, fallback)
                : fallback;
            return FiniteAtLeast(value, 0.1f);
        }

        protected float GetSpeed()
        {
            float fallback = _toolMetadata == null ? 1f : _toolMetadata.GetTotalSpeed();
            float value = TryGetModularEquipment(out IModularEquipmentService service) && _runtimeToolRegistered
                ? service.GetSpeedScalar(_runtimeToolId, fallback)
                : fallback;
            return FiniteAtLeast(value, 0.1f);
        }

        protected float GetEnergyConsumption()
        {
            float fallback = _toolMetadata == null ? 0f : _toolMetadata.GetTotalEnergyConsumption();
            float value = TryGetModularEquipment(out IModularEquipmentService service) && _runtimeToolRegistered
                ? service.GetBatteryDrainPerSecond(_runtimeToolId, fallback)
                : fallback;
            return FiniteNonNegativeOrZero(value);
        }

        protected float GetRuntimeMaxRange(float fallback)
        {
            float value = TryGetModularEquipment(out IModularEquipmentService service) && _runtimeToolRegistered
                ? service.GetMaxRange(_runtimeToolId, fallback)
                : fallback;
            return FiniteNonNegativeOrZero(value);
        }

        protected float GetRuntimePowerScalar(float fallback)
        {
            float value = TryGetModularEquipment(out IModularEquipmentService service) && _runtimeToolRegistered
                ? service.GetPowerScalar(_runtimeToolId, fallback)
                : fallback;
            return math.isfinite(value) ? value : (math.isfinite(fallback) ? fallback : 1f);
        }

        protected float GetRuntimeHeatGenerationRate(float fallback)
        {
            float value = TryGetModularEquipment(out IModularEquipmentService service) && _runtimeToolRegistered
                ? service.GetHeatGenerationRate(_runtimeToolId, fallback)
                : fallback;
            return FiniteNonNegativeOrZero(value);
        }

        protected float GetRuntimeCooldownRate(float fallback)
        {
            float value = TryGetModularEquipment(out IModularEquipmentService service) && _runtimeToolRegistered
                ? service.GetCooldownRate(_runtimeToolId, fallback)
                : fallback;
            return FiniteNonNegativeOrZero(value);
        }

        protected float GetRuntimeRecoilImpulse(float fallback)
        {
            float value = TryGetModularEquipment(out IModularEquipmentService service) && _runtimeToolRegistered
                ? service.GetRecoilImpulse(_runtimeToolId, fallback)
                : fallback;
            return FiniteNonNegativeOrZero(value);
        }

        protected float GetRuntimeBatteryNormalized(float fallback)
        {
            float value = TryGetModularEquipment(out IModularEquipmentService service) && _runtimeToolRegistered
                ? service.GetBatteryNormalized(_runtimeToolId, fallback)
                : fallback;
            return math.isfinite(value) ? math.saturate(value) : math.saturate(fallback);
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
            if (!_runtimeToolRegistered ||
                !TryGetModularEquipment(out IModularEquipmentService service))
                return false;

            if (!service.TryGetWirelessBrownoutFeedback(_runtimeToolId, out flickerScalar) || !math.isfinite(flickerScalar))
            {
                flickerScalar = 0f;
                return false;
            }

            flickerScalar = math.saturate(flickerScalar);
            return true;
        }

        protected bool TryGetToolBrownoutFlicker(out float flickerScalar)
        {
            flickerScalar = 0f;
            if (!_runtimeToolRegistered ||
                !TryGetModularEquipment(out IModularEquipmentService service))
                return false;

            if (!service.TryGetToolBrownoutFeedback(_runtimeToolId, out flickerScalar) || !math.isfinite(flickerScalar))
            {
                flickerScalar = 0f;
                return false;
            }

            flickerScalar = math.saturate(flickerScalar);
            return true;
        }

        protected bool HasToolEnergyOrWirelessPath()
        {
            if (GetRuntimeBatteryNormalized(0f) > 0.0001f)
                return true;

            ISubmarineRuntimeContext submarine = _submarineRuntimeContext;
            return HasModularUpgrade(ToolUpgradeBits.WirelessCharging) &&
                   submarine != null &&
                   submarine.AtmosphereSystem != null &&
                   _powerGridService != null;
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
            ToolDurabilitySystem system = _toolDurabilityService;
            if (system == null || _toolMetadata == null) return;
            float safeDeltaTime = FiniteNonNegativeOrZero(deltaTime);
            float drainRate = FiniteNonNegativeOrZero(isPrimary ? _toolMetadata.durabilityDrainRate : _toolMetadata.durabilityDrainRateSecondary);
            float safeMaxDurability = FiniteAtLeast(_toolMetadata.maxDurability, 1f);
            float multiplier = TryGetModularEquipment(out IModularEquipmentService service) && _runtimeToolRegistered
                ? service.GetDurabilityDrainMultiplier(_runtimeToolId, 1f)
                : 1f;
            float safeMultiplier = FiniteNonNegativeOrZero(multiplier);
            system.DrainDurabilityByTime(
                _toolMetadata.toolID,
                ResolveToolItemHash(),
                drainRate * math.rcp(safeMaxDurability) * safeMultiplier * safeDeltaTime,
                safeMaxDurability);
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
            }
        }

        protected virtual void OnToolBrokenWhileUsing() { }

        internal ToolRuntimeProfile BuildModularRuntimeProfile()
        {
            ToolRuntimeProfile profile = new ToolRuntimeProfile
            {
                ToolId = ResolveRuntimeToolId(),
                MaxRange = 1f,
                PowerScalar = 1f,
                EfficiencyScalar = _toolMetadata != null ? FiniteAtLeast(_toolMetadata.efficiency, 0.1f) : 1f,
                SpeedScalar = _toolMetadata != null ? FiniteAtLeast(_toolMetadata.speed, 0.1f) : 1f,
                HeatGenerationRate = _toolMetadata != null ? FiniteNonNegativeOrZero(_toolMetadata.authoredHeatGenerationRate) : 0f,
                CooldownRate = _toolMetadata != null ? FiniteNonNegativeOrZero(_toolMetadata.authoredCooldownRate) : 0f,
                BatteryCapacity = 1f,
                BatteryDrainPerSecond = _toolMetadata != null ? FiniteNonNegativeOrZero(_toolMetadata.energyConsumptionRate) : 0f,
                DurabilityDrainMultiplier = 1f,
                RecoilImpulse = _toolMetadata != null ? FiniteNonNegativeOrZero(_toolMetadata.authoredRecoilImpulse) : 0f,
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

            float safeDeltaTime = FiniteNonNegativeOrZero(deltaTime);
            float requestedDrain = GetEnergyConsumption() * safeDeltaTime;
            if (!math.isfinite(requestedDrain))
                requestedDrain = 0f;
            if (requestedDrain <= 0f)
                return true;

            return HasToolEnergyOrWirelessPath();
        }

        protected void SyncModularDurability()
        {
            if (TryGetModularEquipment(out IModularEquipmentService service) && _runtimeToolRegistered)
                service.SetDurability(_runtimeToolId, DurabilityNormalized);
        }

        protected bool TryQueuePlayerToolRecoil(Vector3 usageDirection, float impulseMagnitude)
        {
            if (!math.isfinite(impulseMagnitude) || impulseMagnitude <= 0.0001f)
                return false;

            Vector3 safeDirection = NormalizeOrCachedForward(usageDirection);
            if (ToolHitUtility.TryApplyRelativeCarrierImpulse(safeDirection, impulseMagnitude))
                return true;

            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            Rigidbody playerBody = playerContext != null ? playerContext.PlayerRigidbody : null;
            if (playerBody == null)
                return false;

            return PhysicsForceRouter.QueueForce(playerBody, -safeDirection * impulseMagnitude, ForceMode.Impulse);
        }

        private Vector3 NormalizeOrCachedForward(Vector3 direction)
        {
            if (!IsFiniteVector(direction))
                return ResolveCachedForward();

            float sqrMagnitude = direction.sqrMagnitude;
            if (math.isfinite(sqrMagnitude) && sqrMagnitude > 0.0001f)
            {
                if (math.abs(sqrMagnitude - 1f) <= 0.02f)
                    return direction;

                return direction * math.rsqrt(sqrMagnitude);
            }

            return ResolveCachedForward();
        }

        private Vector3 ResolveCachedForward()
        {
            if (_cachedBaseTransform == null)
                _cachedBaseTransform = transform;

            if (_cachedBaseTransform == null)
                return Vector3.forward;

            Vector3 forward = _cachedBaseTransform.forward;
            return IsFiniteVector(forward) ? forward : Vector3.forward;
        }

        protected void QueueToolHapticFeedback(float powerDelivered, float ratedPower, byte priority = 1)
        {
            ToolHapticsRuntime.EnqueueToolFeedback(powerDelivered, ratedPower, priority);
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
        }

        private static float FiniteNonNegativeOrZero(float value)
        {
            return math.isfinite(value) && value > 0f ? value : 0f;
        }

        private static float FiniteAtLeast(float value, float minimum)
        {
            return math.isfinite(value) ? math.max(minimum, value) : minimum;
        }

        protected bool TryBeginToolUse(float deltaTime, bool isPrimary)
        {
            float safeDeltaTime = FiniteNonNegativeOrZero(deltaTime);
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
                float energyDeltaTime = isPrimary ? safeDeltaTime : safeDeltaTime * 0.5f;
                if (!HasToolEnergyOrWirelessPath() || !TryConsumeRuntimeEnergy(energyDeltaTime))
                    return false;
            }

            _lastUseWasPrimary = isPrimary;
            if (enableDurabilityDrain && _toolMetadata != null && !UsesCentralizedRuntimeWear())
                ApplyDurabilityDrain(safeDeltaTime, isPrimary);

            _runtimeActiveIntentSeconds = math.max(_runtimeActiveIntentSeconds, RuntimeActiveIntentHoldSeconds);
            _lastUseTime = Time.time;
            CheckLowDurability();
            return true;
        }

        private bool UsesCentralizedRuntimeWear()
        {
            return _runtimeToolRegistered &&
                   TryGetModularEquipment(out IModularEquipmentService service) &&
                   service.IsInitialized;
        }

        internal bool IsRuntimeOverchargeRequested()
        {
            IInputService inputService = _inputService;
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
                {
                    PlayerInventory inventory = _playerInventoryService != null
                        ? _playerInventoryService.Inventory
                        : null;
                    inventory?.TryRemoveFirstMatchingItemByHash(toolHashId);
                }
            }

            HectonPlayerHealth playerHealth = null;
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext != null && playerContext.PlayerTransform != null)
                playerContext.PlayerTransform.TryGetComponent(out playerHealth);

            if (playerHealth != null)
                playerHealth.TakeDamage(FiniteNonNegativeOrZero(playerDamage), true);
        }

        private uint ResolveRuntimeToolId()
        {
            if (_runtimeToolId != 0u)
                return _runtimeToolId;

            string toolIdSource = ResolveRuntimeToolIdSource();
            _runtimeToolId = unchecked((uint)Animator.StringToHash(toolIdSource));
            _runtimeToolSpecHashId = HashRuntimeToolSpecId(toolIdSource);
            return _runtimeToolId;
        }

        private uint ResolveRuntimeToolSpecHashId()
        {
            if (_runtimeToolSpecHashId != 0u)
                return _runtimeToolSpecHashId;

            string toolIdSource = ResolveRuntimeToolIdSource();
            _runtimeToolSpecHashId = HashRuntimeToolSpecId(toolIdSource);
            return _runtimeToolSpecHashId;
        }

        private string ResolveRuntimeToolIdSource()
        {
            return _toolMetadata != null && !string.IsNullOrWhiteSpace(_toolMetadata.toolID)
                ? _toolMetadata.toolID
                : (_toolData != null && !string.IsNullOrWhiteSpace(_toolData.itemName) ? _toolData.itemName : GetType().Name);
        }

        private static uint HashRuntimeToolSpecId(string value)
        {
            if (string.IsNullOrEmpty(value))
                return 0u;

            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c >= 'A' && c <= 'Z')
                    c = (char)(c + 32);

                hash ^= c <= 0x7F ? (byte)c : (byte)'?';
                hash *= 16777619u;
            }

            return hash;
        }

        private void EnsureModularRuntimeRegistration()
        {
            if (_runtimeToolRegistered)
                return;

            ModularEquipmentEngine runtime = ModularEquipmentEngine.EnsureRuntimeInstance();
            if (runtime == null)
                return;

            runtime.InitializeService();
            _modularEquipmentService = runtime.IsInitialized ? runtime : _modularEquipmentService;

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

        protected bool TryGetModularEquipment(out IModularEquipmentService service)
        {
            service = _modularEquipmentService;
            return service != null && service.IsInitialized;
        }

        protected bool TryGetSubmarineRuntimeContext(out ISubmarineRuntimeContext submarine)
        {
            submarine = _submarineRuntimeContext;
            return submarine != null;
        }

        protected bool TryGetPlayerRuntimeContext(out IPlayerRuntimeContext playerContext)
        {
            playerContext = _playerRuntimeContext;
            return playerContext != null && playerContext.IsInitialized;
        }

        private void CacheToolRegistryDependenciesCold()
        {
            _modularEquipmentService = GlobalRegistry.ModularEquipment;
            _powerGridService = GlobalRegistry.PowerGrid;
            _submarineRuntimeContext = GlobalRegistry.Submarine;
            _playerRuntimeContext = GlobalRegistry.Player;
            _playerInventoryService = GlobalRegistry.PlayerInventory;
            _inputService = GlobalRegistry.Input;
            _interactionSignalService = GlobalRegistry.InteractionSignals;
            _toolDurabilityService = GlobalRegistry.ToolDurability;
        }

        private void TryRegisterModularHotSwap()
        {
            if (_modularHotSwapRegistered)
                return;

            _modularHotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterModularHotSwap()
        {
            if (!_modularHotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _modularHotSwapRegistered = false;
        }

        private void ApplyRegistryServiceRebind(GlobalRegistryServiceSlot serviceSlot, object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.ModularEquipment:
                    _modularEquipmentService = currentService as IModularEquipmentService;
                    break;
                case GlobalRegistryServiceSlot.PowerGrid:
                    _powerGridService = currentService as IPowerGridService;
                    break;
                case GlobalRegistryServiceSlot.Submarine:
                    _submarineRuntimeContext = currentService as ISubmarineRuntimeContext;
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                    break;
                case GlobalRegistryServiceSlot.PlayerInventory:
                    _playerInventoryService = currentService as IPlayerInventoryService;
                    break;
                case GlobalRegistryServiceSlot.Input:
                    _inputService = currentService as IInputService;
                    break;
                case GlobalRegistryServiceSlot.InteractionSignals:
                    _interactionSignalService = currentService as IInteractionSignalService;
                    break;
                case GlobalRegistryServiceSlot.ToolDurabilityRuntime:
                    _toolDurabilityService = currentService as ToolDurabilitySystem;
                    break;
            }
        }

        void IGlobalRegistryHotSwapRefListener.OnGlobalRegistryServiceRebound(GlobalRegistryServiceSlot serviceSlot, ref object currentService)
        {
            ApplyRegistryServiceRebind(serviceSlot, currentService);
        }

        void IGlobalRegistryHotSwapListener.OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
        {
            ApplyRegistryServiceRebind(serviceSlot, currentService);
        }

        internal ToolMetadata RuntimeMetadata => _toolMetadata;
        internal uint RuntimeToolId => _runtimeToolId;
        internal uint RuntimeToolSpecHashId => ResolveRuntimeToolSpecHashId();
        internal float LastUseTime => _lastUseTime;
        internal bool LastUseWasPrimary => _lastUseWasPrimary;
        internal bool HasRuntimeActiveIntent => IsEquipped && _runtimeActiveIntentSeconds > 0f;
        internal bool WasRecentlyUsed(float maxIdleSeconds) => IsEquipped && (Time.time - _lastUseTime <= FiniteAtLeast(maxIdleSeconds, 0.05f));

        internal bool TryResolveCachedRuntimePosition(out float3 runtimePosition)
        {
            runtimePosition = default;
            if (_cachedBaseTransform == null)
                _cachedBaseTransform = transform;

            if (_cachedBaseTransform == null)
                return false;

            Vector3 position = _cachedBaseTransform.position;
            if (!IsFiniteVector(position))
                return false;

            runtimePosition = new float3(position.x, position.y, position.z);
            return true;
        }

        internal bool TryResolveCachedAup(out double3 absoluteAup)
        {
            absoluteAup = default;
            if (!TryResolveCachedRuntimePosition(out float3 runtimePosition))
                return false;

            absoluteAup = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(new Vector3(
                runtimePosition.x,
                runtimePosition.y,
                runtimePosition.z));
            return math.all(math.isfinite(absoluteAup));
        }

        internal bool TryGetDurabilityMirror(out string toolId, out uint itemHashId, out float maxDurability)
        {
            toolId = null;
            itemHashId = 0u;
            maxDurability = 1f;
            if (_toolMetadata == null || string.IsNullOrEmpty(_toolMetadata.toolID))
                return false;

            toolId = _toolMetadata.toolID;
            itemHashId = ResolveToolItemHash();
            maxDurability = FiniteAtLeast(_toolMetadata.maxDurability, 1f);
            return true;
        }

        internal float ResolveActiveDurabilityDrainRateNormalized()
        {
            if (!enableDurabilityDrain || _toolMetadata == null)
                return 0f;

            float drainRate = _lastUseWasPrimary
                ? _toolMetadata.durabilityDrainRate
                : _toolMetadata.durabilityDrainRateSecondary;
            float safeMaxDurability = FiniteAtLeast(_toolMetadata.maxDurability, 1f);
            return FiniteNonNegativeOrZero(drainRate) * math.rcp(safeMaxDurability);
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void PublishLifecycleDebug(uint markerHash)
        {
            if (lifecycleDebugLogging)
                GlobalTelemetryBus.PublishModTelemetry(ToolLifecycleTelemetryHash, markerHash, 1f);
        }
#else
        private void PublishLifecycleDebug(uint markerHash) { }
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
