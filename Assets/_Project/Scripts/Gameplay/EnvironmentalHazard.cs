// ============================================================================
// HECTON-8 — EnvironmentalHazard.cs
// Environmental hazard zone that damages the player.
//
// ARCHITECTURE:
//   • ISlowTickable for periodic damage logic (no Update)
//   • Trigger-based or radius-based detection
//   • Distance-scaled damage intensity
//   • MaterialPropertyBlock for visual feedback (zero GC)
//
// HAZARD TYPES:
//   • Radiation — ionizing radiation damage
//   • Heat — high temperature damage
//   • Toxic — poisonous gas/liquid damage
//
// INTEGRATION:
//   • UnityEvent OnIntensityChanged for post-process effects
//   • Player injury authority remains status/signal based, not UnityEvent based.
// ============================================================================

using Hecton8.Atmosphere;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Events;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Environmental hazard zone that damages the player.
    /// Implements ISlowTickable for periodic damage logic.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Environmental Hazard")]
    public sealed class EnvironmentalHazard : MonoBehaviour, ISlowTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private static int s_x001EnvironmentalHazardSignalPushDropCount;
        private const float HazardSlowTickDeltaSeconds = 0.1f;
        private const float ToxicityExposureToxemiaScale = 0.08f;
        private const float ToxicityPoisonStatusDurationSeconds = 6f;
        private const uint EnvironmentalToxicityChemicalHash = 0x45544F58u; // ETOX
        private static int PlayerLayerIndex = -1;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Hazard Settings ────────────────────────────")]
        [Tooltip("Type of hazard.")]
        [SerializeField] private HazardType hazardType = HazardType.Radiation;

        [Tooltip("Base damage per second at center.")]
        [SerializeField, Range(0.1f, 50f)] private float baseDamagePerSecond = 5f;

        [Tooltip("Radius of the hazard zone (used if no trigger).")]
        [SerializeField, Range(1f, 100f)] private float hazardRadius = 10f;

        [Tooltip("Use trigger collider instead of radius.")]
        [SerializeField] private bool useTriggerCollider = true;

        [Tooltip("Minimum distance for full damage (closer = max damage).")]
        [SerializeField, Range(0f, 5f)] private float fullDamageRadius = 1f;

        [Tooltip("Damage interval (seconds).")]
        [SerializeField, Range(0.1f, 2f)] private float damageInterval = 0.5f;

        [Header("── Detection ──────────────────────────────────")]
        [Tooltip("Layer mask for player detection.")]
        [SerializeField] private LayerMask playerLayer;

        [Tooltip("Tag to compare for player (zero GC).")]
        [SerializeField, TagSelector] private string playerTag = "Player";

        [Header("── Visuals ────────────────────────────────────")]
        [Tooltip("Renderer for hazard indicator.")]
        [SerializeField] private Renderer hazardIndicator;

        [Tooltip("Material property for indicator color.")]
        [SerializeField] private string emissionProperty = "_EmissionColor";

        [Tooltip("Color when player is in hazard.")]
        [SerializeField] private Color activeColor = new Color(1f, 0.3f, 0.1f);

        [Tooltip("Color when no player in hazard.")]
        [SerializeField] private Color inactiveColor = new Color(0.3f, 0.1f, 0.1f);

        [Header("── Events ─────────────────────────────────────")]
        [Tooltip("Fired when player enters hazard zone.")]
        [SerializeField] private UnityEvent OnPlayerEnter;

        [Tooltip("Fired when player exits hazard zone.")]
        [SerializeField] private UnityEvent OnPlayerExit;

        [Tooltip("Fired when intensity changes. Parameter: normalized intensity (0-1).")]
        [SerializeField] private UnityEvent<float> OnIntensityChanged;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private Transform _playerTransform;
        private bool _playerInHazard;
        private float _damageTimer;
        private float _currentIntensity;
        private bool _registered;
        private bool _lateFrameRegistered;
        private bool _hotSwapRegistered;
        private bool _indicatorDirty;
        private bool _radiationSourceRegistered;
        private int _radiationSourceId;
        private int _emissionPropertyId;
        private IThermodynamicsService _thermodynamicsService;
        private IPlayerRuntimeContext _playerRuntime;
        private IPlayerActionInterruptSink _playerActionInterrupts;
        private HectonPlayerHealth _playerHealth;
        private Collider _triggerCollider;
        private CachedTriggerVolume _triggerVolume;

        // Cached references
        private Transform _cachedTransform;
        private MaterialPropertyBlock _mpb;
        private static readonly int _EmissionColorID = Shader.PropertyToID("_EmissionColor");

        // Pre-cached strings for zero GC
        private static readonly string _radiationText = "Radiation Hazard";
        private static readonly string _heatText = "Heat Hazard";
        private static readonly string _toxicText = "Toxic Hazard";

        // COLD ALLOC: Collider[8] — player radius overlap buffer — owner: EnvironmentalHazard
        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        /// <summary>Type of hazard.</summary>
        public HazardType HazardTypeValue => hazardType;

        /// <summary>True if player is currently in hazard zone.</summary>
        public bool PlayerInHazard => _playerInHazard;

        /// <summary>Current damage intensity (0-1).</summary>
        public float CurrentIntensity => _currentIntensity;

        /// <summary>Hazard radius.</summary>
        public float HazardRadius => hazardRadius;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            PlayerLayerIndex = -1;
        }

        private static void EnsureLayerCache()
        {
            if (PlayerLayerIndex >= 0)
                return;

            PlayerLayerIndex = Hecton8.Core.HectonLayerMasks.Player;
        }

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            EnsureLayerCache();
            _cachedTransform = transform;
            TryGetComponent(out _triggerCollider);
            _triggerVolume = CachedTriggerVolume.FromCollider(_triggerCollider, hazardRadius);
            RefreshColdRegistryReferences();
            _radiationSourceId = unchecked((int)EntityId.ToULong(GetEntityId()));
            _emissionPropertyId = Shader.PropertyToID(string.IsNullOrEmpty(emissionProperty) ? "_EmissionColor" : emissionProperty);
            _mpb = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] — per-renderer props — owner: EnvironmentalHazard

            if (hazardIndicator == null)
                TryGetComponent(out hazardIndicator);

            // Cache player layer if not set
            if (playerLayer == 0 && PlayerLayerIndex >= 0)
            {
                playerLayer = 1 << PlayerLayerIndex;
            }
        }

        private void OnEnable()
        {
            RefreshColdRegistryReferences();
            TryRegister();
            TryRegisterHotSwapListener();
            TryRegisterRadiationSource();
            QueueIndicatorUpdate();
        }

        private void OnDisable()
        {
            TryUnregisterRadiationSource();
            TryUnregisterHotSwapListener();
            TryUnregisterLateFrame();
            TryUnregister();
            ClearExposureState();
        }

        private void OnDestroy()
        {
            TryUnregisterRadiationSource();
            TryUnregisterHotSwapListener();
            TryUnregisterLateFrame();
            TryUnregister();
            ClearExposureState();
        }

        public void LateFrameTick()
        {
            if (!_indicatorDirty)
            {
                TryUnregisterLateFrameWhenDormant();
                return;
            }

            _indicatorDirty = false;
            UpdateIndicator();
            TryUnregisterLateFrameWhenDormant();
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (hazardType == HazardType.Radiation)
                return;

            _registered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registered = false;
        }

        private void TryRegisterLateFrame()
        {
            if (_lateFrameRegistered || !Application.isPlaying)
                return;

            _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterLateFrame()
        {
            if (!_lateFrameRegistered)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _lateFrameRegistered = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private void RefreshColdRegistryReferences()
        {
            _thermodynamicsService = GlobalRegistry.ThermodynamicsService;
            _playerRuntime = GlobalRegistry.Player;
            _playerActionInterrupts = GlobalRegistry.PlayerActionInterrupts;
            _playerHealth = _playerRuntime != null ? _playerRuntime.PlayerHealth : null;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.ThermodynamicsService)
            {
                _thermodynamicsService = currentService as IThermodynamicsService;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                _playerRuntime = currentService as IPlayerRuntimeContext;
                _playerHealth = _playerRuntime != null ? _playerRuntime.PlayerHealth : null;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.PlayerActionRuntime)
                _playerActionInterrupts = currentService as IPlayerActionInterruptSink;
        }

        private void ClearExposureState()
        {
            if (_playerInHazard)
                HazardExposureNotifier.Exit(hazardType);

            _playerInHazard = false;
            _playerTransform = null;
            _currentIntensity = 0f;
            _damageTimer = 0f;
        }

        // ══════════════════════════════════════════════════════════
        //  TRIGGER DETECTION (Optional)
        // ══════════════════════════════════════════════════════════

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable — DAMAGE LOGIC
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// ISlowTickable implementation. Handles damage logic.
        /// Zero GC: no allocations, uses CompareTag.
        /// </summary>
        public void SlowTick()
        {
            if (hazardType == HazardType.Radiation)
                return;

            if (hazardType == HazardType.Heat)
            {
                TryPublishThermalFieldSource();
                return;
            }

            if (useTriggerCollider)
                CheckPlayerInTriggerVolume();
            else
                CheckPlayerInRadius();

            if (!_playerInHazard || _playerTransform == null)
                return;

            double distanceSq = ResolvePlayerDistanceSq();
            float newIntensity = CalculateIntensityFromDistanceSq(distanceSq);

            // Fire intensity changed event
            if (Mathf.Abs(newIntensity - _currentIntensity) > 0.01f)
            {
                _currentIntensity = newIntensity;
                OnIntensityChanged?.Invoke(_currentIntensity);
                QueueIndicatorUpdate();
            }

            // Apply damage at intervals
            _damageTimer += HazardSlowTickDeltaSeconds;
            if (_damageTimer >= damageInterval)
            {
                _damageTimer = 0f;
                ApplyDamage();
            }
        }

        // ══════════════════════════════════════════════════════════
        //  DETECTION LOGIC
        // ══════════════════════════════════════════════════════════

        private void CheckPlayerInRadius()
        {
            if (hazardType == HazardType.Radiation)
                return;

            Transform playerTransform = ResolveRuntimePlayerTransform();
            bool foundPlayer = playerTransform != null && IsPlayerInsideHazardRadius(playerTransform);
            SetPlayerHazardState(foundPlayer, playerTransform);
        }

        private void CheckPlayerInTriggerVolume()
        {
            if (hazardType == HazardType.Radiation)
                return;

            Transform playerTransform = ResolveRuntimePlayerTransform();
            bool foundPlayer = playerTransform != null && IsPlayerInsideTriggerVolume(playerTransform);
            SetPlayerHazardState(foundPlayer, playerTransform);
        }

        private void SetPlayerHazardState(bool foundPlayer, Transform playerTransform)
        {
            if (foundPlayer)
                _playerTransform = playerTransform;

            if (foundPlayer && !_playerInHazard)
            {
                _playerInHazard = true;
                HazardExposureNotifier.Enter(hazardType);
                OnPlayerEnter?.Invoke();
            }
            else if (!foundPlayer && _playerInHazard)
            {
                HazardExposureNotifier.Exit(hazardType);
                _playerInHazard = false;
                _playerTransform = null;
                _currentIntensity = 0f;
                OnPlayerExit?.Invoke();
                OnIntensityChanged?.Invoke(0f);
                QueueIndicatorUpdate();
            }
        }

        private bool IsPlayerInsideTriggerVolume(Transform playerTransform)
        {
            if (playerTransform == null)
                return false;

            if (!string.IsNullOrEmpty(playerTag) && !playerTransform.CompareTag(playerTag))
                return false;

            int layer = playerTransform.gameObject.layer;
            if (layer >= 0 && layer < 32 && (playerLayer.value & (1 << layer)) == 0)
                return false;

            return _triggerVolume.Contains(_cachedTransform, playerTransform.position);
        }

        private Transform ResolveRuntimePlayerTransform()
        {
            IPlayerRuntimeContext playerContext = _playerRuntime;
            if (playerContext != null && playerContext.PlayerTransform != null)
                return playerContext.PlayerTransform;

            return _playerTransform;
        }

        private bool IsPlayerInsideHazardRadius(Transform playerTransform)
        {
            if (playerTransform == null)
                return false;

            if (!string.IsNullOrEmpty(playerTag) && !playerTransform.CompareTag(playerTag))
                return false;

            int layer = playerTransform.gameObject.layer;
            if (layer >= 0 && layer < 32 && (playerLayer.value & (1 << layer)) == 0)
                return false;

            double radius = math.max(0d, hazardRadius);
            double radiusSq = radius * radius;
            Vector3 hazardPosition = _cachedTransform.position;
            Vector3 playerPosition = playerTransform.position;
            if (hazardRadius <= 50f)
                return (playerPosition - hazardPosition).sqrMagnitude <= radiusSq;

            if (!TryResolveAupFromRuntimeOrigin(hazardPosition, out AbsoluteUniversePosition hazardAup) ||
                !TryResolveAupFromRuntimeOrigin(playerPosition, out AbsoluteUniversePosition playerAup))
            {
                return false;
            }

            return AbsoluteUniversePosition.DistanceSq(in hazardAup, in playerAup) <= radiusSq;
        }

        /// <summary>
        /// Calculates damage intensity from squared distance.
        /// 1.0 at center, 0.0 at edge.
        /// </summary>
        private float CalculateIntensityFromDistanceSq(double distanceSq)
        {
            double fullDamageRadiusSq = (double)fullDamageRadius * fullDamageRadius;
            if (distanceSq <= fullDamageRadiusSq)
                return 1f;

            double hazardRadiusSq = (double)hazardRadius * hazardRadius;
            if (distanceSq >= hazardRadiusSq)
                return 0f;

            double rangeSq = hazardRadiusSq - fullDamageRadiusSq;
            if (rangeSq <= double.Epsilon)
                return 0f;

            float normalizedSq = Mathf.Clamp01((float)((distanceSq - fullDamageRadiusSq) / rangeSq));
            return 1f - normalizedSq;
        }

        private double ResolvePlayerDistanceSq()
        {
            Vector3 hazardPosition = _cachedTransform.position;
            Vector3 playerPosition = _playerTransform.position;

            if (hazardRadius <= 50f)
            {
                Vector3 offset = playerPosition - hazardPosition;
                return offset.sqrMagnitude;
            }

            if (!TryResolveAupFromRuntimeOrigin(hazardPosition, out AbsoluteUniversePosition hazardAup) ||
                !TryResolveAupFromRuntimeOrigin(playerPosition, out AbsoluteUniversePosition playerAup))
            {
                double radius = math.max((double)hazardRadius, 0d);
                return radius * radius;
            }

            return AbsoluteUniversePosition.DistanceSq(in hazardAup, in playerAup);
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!math.isfinite(runtimePosition.x) ||
                !math.isfinite(runtimePosition.y) ||
                !math.isfinite(runtimePosition.z))
            {
                return false;
            }

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return positionAup.IsFinite();
        }

        // ══════════════════════════════════════════════════════════
        //  DAMAGE APPLICATION
        // ══════════════════════════════════════════════════════════

        private void ApplyDamage()
        {
            if (hazardType == HazardType.Radiation)
                return;

            if (_playerTransform == null || _currentIntensity <= 0f)
                return;

            float damage = baseDamagePerSecond * _currentIntensity * damageInterval;
            if (!(damage > 0f) || !math.isfinite(damage))
                return;

            // ── Interrupt player action (eating, healing) ──
            _playerActionInterrupts?.OnDamageTaken();
            HectonPlayerHealth playerHealth = ResolvePlayerHealth();
            if (playerHealth == null)
                return;

            if (IsToxicHazardType())
            {
                ApplyToxicityExposure(playerHealth, damage);
                return;
            }

            if (!TryQueueCentralHazardDamage(playerHealth, damage))
                ApplyOwnerHazardDamageFallback(playerHealth, damage);
        }

        // ══════════════════════════════════════════════════════════
        //  VISUALS
        // ══════════════════════════════════════════════════════════

        private HectonPlayerHealth ResolvePlayerHealth()
        {
            HectonPlayerHealth playerHealth = _playerHealth;
            if (playerHealth != null)
                return playerHealth;

            IPlayerRuntimeContext playerContext = _playerRuntime;
            playerHealth = playerContext != null ? playerContext.PlayerHealth : null;
            _playerHealth = playerHealth;
            return playerHealth;
        }

        private void ApplyToxicityExposure(HectonPlayerHealth playerHealth, float damageMagnitude)
        {
            if (playerHealth == null)
                return;

            int targetId = CombatDamageRuntime.ResolveTargetId(playerHealth.gameObject);
            float exposure01 = math.saturate(_currentIntensity);
            if (targetId != 0 && CombatDamageRuntime.IsTargetRegistered(targetId))
            {
                float severity01 = math.saturate(math.max(exposure01, damageMagnitude * 0.05f));
                if (severity01 > 0.0001f)
                {
                    CombatDamageRuntime.TryQueueStatusEffect(
                        targetId,
                        CombatStatusBits.Poisoned64,
                        ToxicityPoisonStatusDurationSeconds * math.max(0.25f, severity01),
                        DamageSourceIds.EnvironmentHazard,
                        severity01);
                }
            }

            float toxemiaDelta = math.saturate(exposure01 * math.max(0.1f, damageMagnitude) * ToxicityExposureToxemiaScale);
            if (targetId == 0 || (exposure01 <= 0.0001f && toxemiaDelta <= 0f))
                return;

            if (!TryResolveAupFromRuntimeOrigin(playerHealth.transform.position, out AbsoluteUniversePosition playerAup))
                return;

            ToxicityExposureSignal signal = default;
            signal.AUP = playerAup.ToAbsoluteDouble3();
            signal.Exposure01 = exposure01;
            signal.ToxemiaDelta = toxemiaDelta;
            signal.EntityId = unchecked((uint)targetId);
            signal.ChemicalHash = EnvironmentalToxicityChemicalHash;
            signal.Frame = TimeSliceScheduler.CurrentFrameId;
            signal.Flags = 1;
            SignalBus<ToxicityExposureSignal>.TryPushTracked(in signal, ref s_x001EnvironmentalHazardSignalPushDropCount);
        }

        private bool TryQueueCentralHazardDamage(HectonPlayerHealth playerHealth, float damage)
        {
            if (hazardType != HazardType.Heat)
                return false;

            if (playerHealth == null)
                return false;

            Transform playerTransform = playerHealth.transform;
            if (playerTransform == null)
                return false;

            int targetId = CombatDamageRuntime.ResolveTargetId(playerHealth.gameObject);
            if (targetId == 0 || !CombatDamageRuntime.IsTargetRegistered(targetId))
                return false;

            Vector3 impactPoint = playerTransform.position;
            float3 localPoint = ResolveTargetLocalPoint(playerTransform, impactPoint);
            float3 direction = ResolveHazardDirection(playerTransform);
            CombatDamageRequest signal = new CombatDamageRequest
            {
                TargetId = targetId,
                SourceId = DamageSourceIds.EnvironmentHazard,
                Amount = damage,
                ImpulseMagnitude = 0f,
                Direction = direction,
                PackedMeta = CombatDamageRuntime.PackSignalMeta(
                    ResolveHazardDamageType(),
                    ResolveHazardStatusBits(),
                    CombatWeakspotTier.None)
            };
            CombatDamageSignalDetail detail = new CombatDamageSignalDetail
            {
                LocalPoint = localPoint,
                ArmorNormal = -direction,
                LocalTemperatureCelsius = 0f,
                StatusDurationSeconds = math.max(damageInterval, HazardSlowTickDeltaSeconds)
            };

            double3 impactAup = double3.zero;
            if (TryResolveAupFromRuntimeOrigin(impactPoint, out AbsoluteUniversePosition playerAup) &&
                playerAup.IsFinite())
            {
                double3 resolvedAup = playerAup.ToAbsoluteDouble3();
                if (math.all(math.isfinite(resolvedAup)))
                    impactAup = resolvedAup;
            }

            CombatDamageRuntime.TryQueueDamage(in signal, in detail, impactAup);
            return true;
        }

        private void ApplyOwnerHazardDamageFallback(HectonPlayerHealth playerHealth, float damage)
        {
            if (hazardType != HazardType.Heat || playerHealth == null || !(damage > 0f) || !math.isfinite(damage))
                return;

            Transform playerTransform = playerHealth.transform;
            Vector3 impactPoint = playerTransform != null &&
                                  math.isfinite(playerTransform.position.x) &&
                                  math.isfinite(playerTransform.position.y) &&
                                  math.isfinite(playerTransform.position.z)
                ? playerTransform.position
                : Vector3.zero;

            DamagePacket packet = new DamagePacket
            {
                Channel = DamageChannel.Integrity,
                PreviousValue = 0f,
                NextValue = 0f,
                Magnitude = damage,
                LocalPoint = ResolveTargetLocalPoint(playerTransform, impactPoint),
                DamageType = ResolveHazardDamageType(),
                IntegrityDelta = 0,
                Depth = 0f,
                SourceId = DamageSourceIds.EnvironmentHazard,
                TraumaLevel = 0
            };
            playerHealth.ReceiveDamage(in packet);
        }

        private static float3 ResolveTargetLocalPoint(Transform targetTransform, Vector3 impactPoint)
        {
            if (targetTransform == null ||
                !math.isfinite(impactPoint.x) ||
                !math.isfinite(impactPoint.y) ||
                !math.isfinite(impactPoint.z))
            {
                return float3.zero;
            }

            Vector3 localPoint = targetTransform.InverseTransformPoint(impactPoint);
            float3 localPoint3 = new float3(localPoint.x, localPoint.y, localPoint.z);
            return math.all(math.isfinite(localPoint3)) ? localPoint3 : float3.zero;
        }

        private float3 ResolveHazardDirection(Transform playerTransform)
        {
            if (playerTransform == null || _cachedTransform == null)
                return new float3(0f, 1f, 0f);

            Vector3 offset = playerTransform.position - _cachedTransform.position;
            float3 direction = new float3(offset.x, offset.y, offset.z);
            return math.normalizesafe(direction, new float3(0f, 1f, 0f));
        }

        private uint ResolveHazardDamageType()
        {
            return CombatDamageTypes.Thermal;
        }

        private uint ResolveHazardStatusBits()
        {
            return hazardType == HazardType.Heat ? CombatStatusBits.Burning : 0u;
        }

        private bool IsToxicHazardType()
        {
            return hazardType == HazardType.Toxicity || hazardType == HazardType.Biohazard;
        }

        /// <summary>
        /// Updates the hazard indicator using MaterialPropertyBlock.
        /// Zero GC: uses cached MaterialPropertyBlock.
        /// </summary>
        private void UpdateIndicator()
        {
            if (hazardIndicator == null)
                return;

            Color indicatorColor = _playerInHazard
                ? Color.Lerp(inactiveColor, activeColor, _currentIntensity)
                : inactiveColor;

            hazardIndicator.GetPropertyBlock(_mpb);
            _mpb.SetColor(_emissionPropertyId != 0 ? _emissionPropertyId : _EmissionColorID, indicatorColor);
            hazardIndicator.SetPropertyBlock(_mpb);
        }

        private void QueueIndicatorUpdate()
        {
            _indicatorDirty = true;
            TryRegisterLateFrame();
        }

        private void TryUnregisterLateFrameWhenDormant()
        {
            if (_indicatorDirty)
                return;

            TryUnregisterLateFrame();
        }

        private void TryRegisterRadiationSource()
        {
            if (hazardType != HazardType.Radiation || _cachedTransform == null)
                return;

            float intensity = baseDamagePerSecond * 10f;
            if (!TryResolveValidHazardSourcePayload(
                    _cachedTransform.position,
                    intensity,
                    hazardRadius,
                    out Vector3 position,
                    out float safeIntensity,
                    out float safeRadius))
            {
                TryUnregisterRadiationSource();
                return;
            }

            if (!Application.isPlaying ||
                _radiationSourceId == 0 ||
                !TryResolveAupFromRuntimeOrigin(position, out AbsoluteUniversePosition sourceAup))
            {
                TryUnregisterRadiationSource();
                return;
            }

            RadiationHazardGrid.RegisterSource(_radiationSourceId, in sourceAup, safeIntensity, safeRadius);
            _radiationSourceRegistered = true;
        }

        private void TryUnregisterRadiationSource()
        {
            if (!_radiationSourceRegistered)
                return;

            RadiationHazardGrid.UnregisterSource(_radiationSourceId);
            _radiationSourceRegistered = false;
        }

        private void TryPublishThermalFieldSource()
        {
            if (hazardType != HazardType.Heat || _cachedTransform == null)
                return;

            if (!TryResolveValidHazardSourcePayload(
                    _cachedTransform.position,
                    baseDamagePerSecond,
                    hazardRadius,
                    out Vector3 position,
                    out float safeIntensity,
                    out float safeRadius))
            {
                return;
            }

            IThermodynamicsService thermodynamics = _thermodynamicsService;
            if (thermodynamics != null && thermodynamics.IsInitialized)
                thermodynamics.TryInjectTransientHeatSource(position, safeRadius, safeIntensity, unchecked((uint)_radiationSourceId));
        }

        private static bool TryResolveValidHazardSourcePayload(
            Vector3 position,
            float intensity,
            float radius,
            out Vector3 safePosition,
            out float safeIntensity,
            out float safeRadius)
        {
            safePosition = default;
            safeIntensity = 0f;
            safeRadius = 0f;

            if (!math.isfinite(position.x) ||
                !math.isfinite(position.y) ||
                !math.isfinite(position.z) ||
                !math.isfinite(intensity) ||
                !(intensity > 0f) ||
                !math.isfinite(radius) ||
                !(radius > 0f))
            {
                return false;
            }

            safePosition = position;
            safeIntensity = intensity;
            safeRadius = radius;
            return true;
        }

        /// <summary>
        /// Gets the hazard name for UI display.
        /// </summary>
        public string GetHazardName()
        {
            switch (hazardType)
            {
                case HazardType.Radiation: return _radiationText;
                case HazardType.Heat: return _heatText;
                case HazardType.Toxicity: return _toxicText;
                case HazardType.Biohazard: return "Biohazard";
                default: return string.Empty;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!math.isfinite(baseDamagePerSecond) || baseDamagePerSecond < 0.1f) baseDamagePerSecond = 0.1f;
            if (!math.isfinite(fullDamageRadius) || fullDamageRadius < 0f) fullDamageRadius = 0f;
            if (!math.isfinite(hazardRadius) || hazardRadius < fullDamageRadius) hazardRadius = fullDamageRadius + 1f;
            if (!math.isfinite(damageInterval) || damageInterval < 0.1f) damageInterval = 0.1f;
        }

        private void OnDrawGizmosSelected()
        {
            // Draw hazard radius
            Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, hazardRadius);

            // Draw full damage radius
            Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, fullDamageRadius);

            // Draw hazard type indicator
            if (Application.isPlaying && _playerInHazard)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, _playerTransform.position);
            }
        }
#endif
    }

    /// <summary>
    /// Attribute for tag selector in inspector.
    /// </summary>
    public class TagSelectorAttribute : PropertyAttribute { }
}
