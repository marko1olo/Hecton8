using Hecton8.Building;
using Hecton8.Core;
using Hecton8.Interaction;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace Hecton8.Construction
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Construction/VR Construction Weld Target")]
    public sealed class VRConstructionWeldTarget : MonoBehaviour, IInteractionSignalConsumer, IOriginShiftListener, IUpdatable, IGlobalRegistryHotSwapListener
    {
        private const int CornerCount = 4;
        private const float DefaultInteractionStepSeconds = 0.02f;
        private const float MaxInteractionDeltaSeconds = 0.05f;
        private const float MaxSecondsPerCorner = 30f;
        private const float MaxWeldRadiusMeters = 2f;
        private const float MaxDeliveredPower = 4f;
        private const float MaxWeldHeatHoldAfterContactSeconds = 2f;
        private const float MaxWeldCooldownSecondsPerSecond = 10f;
        private const float MaxWeldGlowDurationSeconds = 2f;
        private const float MaxWeldGlowRangeMeters = 8f;
        private const float MaxWeldGlowProxyIntensity = 4f;

        [Header("Weld Corners")]
        [SerializeField] private Transform corner0;
        [SerializeField] private Transform corner1;
        [SerializeField] private Transform corner2;
        [SerializeField] private Transform corner3;

        [Header("Welding")]
        [SerializeField, Min(0.05f)] private float secondsPerCorner = 1.25f;
        [SerializeField, Min(0.01f)] private float weldRadiusMeters = 0.18f;
        [SerializeField, Min(0f)] private float requiredPower = 0.1f;
        [SerializeField, Min(0f)] private float weldHeatHoldAfterContactSeconds = 0.08f;
        [SerializeField, Min(0f)] private float weldCooldownSecondsPerSecond = 0.35f;

        [Header("Logistics Registration")]
        [SerializeField] private bool registerWithConstructionManagerOnComplete = true;
        [SerializeField, FormerlySerializedAs("constructionManager")] private MonoBehaviour constructionLogisticsProvider;
        [SerializeField] private BuildableData panelBuildableData;
        [SerializeField] private GameObject logisticsGraphEntryRoot;

        [Header("Shader Point Light Fake")]
        [SerializeField] private Transform weldGlowOrigin;
        [SerializeField] private Color incompleteGlowColor = new Color(1f, 0.42f, 0.08f, 1f);
        [SerializeField] private Color completeGlowColor = new Color(0.2f, 0.95f, 1f, 1f);
        [SerializeField, Min(0.01f)] private float weldGlowDurationSeconds = 0.1f;
        [SerializeField, Min(0.01f)] private float weldGlowRangeMeters = 1.25f;
        [SerializeField, Min(0f)] private float weldGlowProxyIntensity = 0.82f;

        private readonly Transform[] _corners = new Transform[CornerCount]; // COLD ALLOC: Transform[4] - weld corner cache - owner: VRConstructionWeldTarget
        private readonly float[] _cornerProgressSeconds = new float[CornerCount]; // COLD ALLOC: float[4] - weld progress state - owner: VRConstructionWeldTarget
        private readonly Vector3[] _cornerRuntimePositions = new Vector3[CornerCount]; // COLD ALLOC: Vector3[4] - local weld corner presentation positions - owner: VRConstructionWeldTarget
        private Vector3 _weldGlowRuntimePosition;
        private int _weldGlowProxyKey;
        private float _weldGlowClockSeconds;
        private float _weldGlowRemainingSeconds;
        private float _weldHeatHoldRemainingSeconds;
        private byte _validCornerMask;
        private byte _completedMask;
        private bool _complete;
        private bool _registeredOriginShift;
        private bool _registeredHotSwap;
        private bool _weldGlowProxyRegistered;
        private bool _weldGlowTickRegistered;
        private bool _weldGlowTickSleeping;
        private ILogisticsService _constructionLogistics;

        public bool IsComplete => _complete;
        public byte CompletedMask => _completedMask;

        private void Awake()
        {
            _weldGlowProxyKey = unchecked((int)EntityId.ToULong(GetEntityId()) ^ 0x56525744);
            BindCorners();
            CacheCornerRuntimePositions();
            CacheConstructionLogisticsCold();
        }

        private void OnEnable()
        {
            BindCorners();
            CacheCornerRuntimePositions();
            CacheConstructionLogisticsCold();
            TryRegisterHotSwapListener();
            TryRegisterOriginShiftListener();
            InteractableRegistry.RegisterTree(this);
        }

        private void OnDisable()
        {
            InteractableRegistry.InvalidateTree(this);
            TryUnregisterOriginShiftListener();
            TryUnregisterHotSwapListener();
            UnregisterWeldGlowProxy();
            TryUnregisterWeldGlowTick();
        }

        private void OnDestroy()
        {
            InteractableRegistry.InvalidateTree(this);
            TryUnregisterHotSwapListener();
            UnregisterWeldGlowProxy();
            TryUnregisterWeldGlowTick();
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftOffset = shiftData.ShiftOffset;
            float shiftSqrMagnitude = shiftOffset.sqrMagnitude;
            if (!IsFiniteVector(shiftOffset) || !math.isfinite(shiftSqrMagnitude) || shiftSqrMagnitude <= 0.000001f)
                return;

            CacheCornerRuntimePositions();
            if (_weldGlowProxyRegistered)
                UpdateWeldGlowProxyRegistration();
        }

        public float GetCornerProgress01(int index)
        {
            if ((uint)index >= CornerCount)
                return 0f;

            return math.saturate(_cornerProgressSeconds[index] / ResolveSafeSecondsPerCorner());
        }

        public bool TryGetCornerAup(int index, out AbsoluteUniversePosition aup)
        {
            aup = default;
            return false;
        }

        public bool ApplyWeldAtPoint(Vector3 runtimeHitPoint, float deliveredPower, float deltaSeconds)
        {
            float safeDeltaSeconds = ResolveSafeDeltaSeconds(deltaSeconds);
            float safeDeliveredPower = ResolveSafeDeliveredPower(deliveredPower);
            if (_complete || safeDeliveredPower < ResolveSafeRequiredPower() || safeDeltaSeconds <= 0f || !IsFiniteVector(runtimeHitPoint))
            {
                ArmWeldCooling();
                return false;
            }

            if (!TryFindCorner(runtimeHitPoint, out int cornerIndex))
            {
                ArmWeldCooling();
                return false;
            }

            float previousProgress = _cornerProgressSeconds[cornerIndex];
            float targetProgress = ResolveSafeSecondsPerCorner();
            if (previousProgress >= targetProgress)
                return true;

            _weldHeatHoldRemainingSeconds = math.max(_weldHeatHoldRemainingSeconds, ResolveSafeWeldHeatHoldAfterContactSeconds());
            _cornerProgressSeconds[cornerIndex] = math.min(targetProgress, previousProgress + safeDeltaSeconds * safeDeliveredPower);
            if (_cornerProgressSeconds[cornerIndex] >= targetProgress)
                _completedMask |= (byte)(1 << cornerIndex);

            TriggerWeldGlow(runtimeHitPoint);

            if (_completedMask == 0x0F)
                CompleteWeld();

            return true;
        }

        public void ApplyInteractionSignal(in InteractionSignal signal, Vector3 runtimeHitPoint)
        {
            if (signal.PowerDelivered <= 0f)
                return;

            byte effect = signal.EffectType;
            if (effect != (byte)InteractionEffectType.Weld &&
                effect != (byte)InteractionEffectType.Torch &&
                effect != (byte)InteractionEffectType.PlasmaCut &&
                effect != (byte)InteractionEffectType.Boil)
            {
                return;
            }

            ApplyWeldAtPoint(runtimeHitPoint, signal.PowerDelivered, DefaultInteractionStepSeconds);
        }

        public void ResetWeldProgress()
        {
            for (int i = 0; i < CornerCount; i++)
                _cornerProgressSeconds[i] = 0f;

            _completedMask = 0;
            _complete = false;
            _weldGlowRemainingSeconds = 0f;
            _weldHeatHoldRemainingSeconds = 0f;
            UnregisterWeldGlowProxy();
            TryUnregisterWeldGlowTick();
        }

        public void Tick(float deltaTime)
        {
            if (_weldGlowTickSleeping)
                return;

            float dt = ResolveSafeDeltaSeconds(deltaTime);
            _weldGlowClockSeconds = math.max(0f, _weldGlowClockSeconds + dt);
            UpdateWeldHeatCooling(dt);

            _weldGlowRemainingSeconds = math.max(0f, _weldGlowRemainingSeconds - dt);
            if (_weldGlowRemainingSeconds > 0f)
            {
                UpdateWeldGlowProxyRegistration();
                return;
            }

            UnregisterWeldGlowProxy();
            if (!HasWeldCoolingWork())
                _weldGlowTickSleeping = true;
        }

        private bool TryFindCorner(Vector3 runtimeHitPoint, out int cornerIndex)
        {
            cornerIndex = -1;
            if (!IsFiniteVector(runtimeHitPoint))
                return false;

            float safeWeldRadiusMeters = ResolveSafeWeldRadiusMeters();
            double bestDistanceSq = (double)safeWeldRadiusMeters * safeWeldRadiusMeters;
            for (int i = 0; i < CornerCount; i++)
            {
                Transform corner = _corners[i];
                if (corner == null || (_validCornerMask & (1 << i)) == 0)
                    continue;

                double distanceSq = RuntimeDistanceSq(runtimeHitPoint, _cornerRuntimePositions[i]);
                if (distanceSq > bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                cornerIndex = i;
            }

            return cornerIndex >= 0;
        }

        private void CompleteWeld()
        {
            if (_complete)
                return;

            _complete = true;
            _weldGlowRemainingSeconds = 0f;
            _weldHeatHoldRemainingSeconds = 0f;
            _weldGlowTickSleeping = false;
            UnregisterWeldGlowProxy();
            TryUnregisterWeldGlowTick();
            RegisterCompletedPanel();
        }

        private void ArmWeldCooling()
        {
            if (_complete || !HasWeldCoolingWork())
                return;

            _weldHeatHoldRemainingSeconds = 0f;
            _weldGlowTickSleeping = false;
            TryRegisterWeldGlowTick();
        }

        private void UpdateWeldHeatCooling(float deltaTime)
        {
            if (_complete || deltaTime <= 0f || !HasWeldCoolingWork())
                return;

            if (_weldHeatHoldRemainingSeconds > 0f)
            {
                _weldHeatHoldRemainingSeconds = math.max(0f, _weldHeatHoldRemainingSeconds - deltaTime);
                return;
            }

            float decay = ResolveSafeWeldCooldownSecondsPerSecond() * deltaTime;
            if (!(decay > 0f))
                return;

            for (int i = 0; i < CornerCount; i++)
            {
                if ((_completedMask & (1 << i)) != 0)
                    continue;

                _cornerProgressSeconds[i] = math.max(0f, _cornerProgressSeconds[i] - decay);
            }
        }

        private bool HasWeldCoolingWork()
        {
            if (_complete)
                return false;

            for (int i = 0; i < CornerCount; i++)
            {
                if ((_completedMask & (1 << i)) == 0 && _cornerProgressSeconds[i] > 0f)
                    return true;
            }

            return _weldHeatHoldRemainingSeconds > 0f;
        }

        private void RegisterCompletedPanel()
        {
            if (!registerWithConstructionManagerOnComplete)
                return;

            ILogisticsService logistics = ResolveConstructionLogistics();
            if (logistics == null)
                return;

            GameObject graphEntry = logisticsGraphEntryRoot != null ? logisticsGraphEntryRoot : gameObject;
            if (panelBuildableData != null)
                logistics.RegisterModule(graphEntry, panelBuildableData);
            else
                logistics.RegisterModule(graphEntry);
        }

        private void BindCorners()
        {
            _corners[0] = corner0;
            _corners[1] = corner1;
            _corners[2] = corner2;
            _corners[3] = corner3;
        }

        private void CacheCornerRuntimePositions()
        {
            _validCornerMask = 0;
            for (int i = 0; i < CornerCount; i++)
            {
                Transform corner = _corners[i];
                if (corner == null || !IsFiniteVector(corner.position))
                    continue;

                _cornerRuntimePositions[i] = corner.position;
                _validCornerMask |= (byte)(1 << i);
            }
        }

        private static double RuntimeDistanceSq(Vector3 a, Vector3 b)
        {
            double dx = (double)a.x - b.x;
            double dy = (double)a.y - b.y;
            double dz = (double)a.z - b.z;
            return (dx * dx) + (dy * dy) + (dz * dz);
        }

        private void TriggerWeldGlow(Vector3 runtimePosition)
        {
            if (!IsFiniteVector(runtimePosition))
                return;

            _weldGlowRuntimePosition = runtimePosition;
            _weldGlowRemainingSeconds = math.max(_weldGlowRemainingSeconds, ResolveSafeWeldGlowDurationSeconds());
            _weldGlowTickSleeping = false;
            UpdateWeldGlowProxyRegistration();
            TryRegisterWeldGlowTick();
        }

        private void UpdateWeldGlowProxyRegistration()
        {
            if (_weldGlowProxyKey == 0 || !(_weldGlowRemainingSeconds > 0f))
                return;

            float progress01 = ResolveAggregateProgress01();
            float4 incomplete = new float4(incompleteGlowColor.r, incompleteGlowColor.g, incompleteGlowColor.b, incompleteGlowColor.a);
            float4 complete = new float4(completeGlowColor.r, completeGlowColor.g, completeGlowColor.b, completeGlowColor.a);
            float4 mixed = math.lerp(incomplete, complete, progress01);
            Color glowLinear = new Color(mixed.x, mixed.y, mixed.z, 1f).linear;
            float lifetime01 = math.saturate(_weldGlowRemainingSeconds / ResolveSafeWeldGlowDurationSeconds());
            float intensity = math.saturate(ResolveSafeWeldGlowProxyIntensity() * lifetime01);
            Vector3 runtimePosition = weldGlowOrigin != null ? weldGlowOrigin.position : _weldGlowRuntimePosition;
            if (!IsFiniteVector(runtimePosition))
            {
                UnregisterWeldGlowProxy();
                return;
            }

            if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition positionAup))
            {
                UnregisterWeldGlowProxy();
                return;
            }

            ProxyLightData lightData = ProxyLightData.CreateTransientPoint(
                positionAup,
                runtimePosition,
                glowLinear,
                ResolveSafeWeldGlowRangeMeters(),
                intensity,
                _weldGlowClockSeconds);

            _weldGlowProxyRegistered = ProxyLightRegistry.RegisterOrUpdate(_weldGlowProxyKey, in lightData) || _weldGlowProxyRegistered;
        }

        private float ResolveAggregateProgress01()
        {
            float targetProgress = ResolveSafeSecondsPerCorner();
            float total = 0f;
            for (int i = 0; i < CornerCount; i++)
                total += math.saturate(_cornerProgressSeconds[i] / targetProgress);

            return total * 0.25f;
        }

        private void TryRegisterOriginShiftListener()
        {
            if (_registeredOriginShift)
                return;

            HectonFloatingOrigin.RegisterListener(this);
            _registeredOriginShift = HectonFloatingOrigin.IsListenerRegistered(this);
        }

        private void TryUnregisterOriginShiftListener()
        {
            if (!_registeredOriginShift)
                return;

            HectonFloatingOrigin.UnregisterListener(this);
            _registeredOriginShift = false;
        }

        private void UnregisterWeldGlowProxy()
        {
            if (!_weldGlowProxyRegistered || _weldGlowProxyKey == 0)
                return;

            ProxyLightRegistry.Unregister(_weldGlowProxyKey);
            _weldGlowProxyRegistered = false;
        }

        private void TryRegisterWeldGlowTick()
        {
            if (_weldGlowTickRegistered || !Application.isPlaying)
                return;

            _weldGlowTickRegistered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player);
        }

        private void TryUnregisterWeldGlowTick()
        {
            if (!_weldGlowTickRegistered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
            _weldGlowTickRegistered = false;
            _weldGlowTickSleeping = false;
        }

        private void CacheConstructionLogisticsCold()
        {
            ILogisticsService providerService = constructionLogisticsProvider as ILogisticsService;
            _constructionLogistics = providerService;
            if (_constructionLogistics == null)
                _constructionLogistics = GlobalRegistry.Logistics;
        }

        private ILogisticsService ResolveConstructionLogistics()
        {
            ILogisticsService providerService = constructionLogisticsProvider as ILogisticsService;
            ILogisticsService logistics = providerService ?? _constructionLogistics;
            return logistics;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Logistics)
            {
                _constructionLogistics = currentService as ILogisticsService;
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            bool shouldRestoreTick = !_complete && (_weldGlowRemainingSeconds > 0f || HasWeldCoolingWork());
            TryUnregisterWeldGlowTick();
            if (!shouldRestoreTick || currentService == null || !isActiveAndEnabled)
                return;

            _weldGlowTickSleeping = false;
            TryRegisterWeldGlowTick();
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return math.all(math.isfinite(new float3(value.x, value.y, value.z)));
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            float3 localRuntime = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (!math.all(math.isfinite(localRuntime)))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(localRuntime.x, localRuntime.y, localRuntime.z));
            return positionAup.IsFinite();
        }

        private static float ResolveSafeDeltaSeconds(float value)
        {
            return math.isfinite(value) ? math.clamp(value, 0f, MaxInteractionDeltaSeconds) : 0f;
        }

        private static float ResolveSafeDeliveredPower(float value)
        {
            return math.isfinite(value) ? math.clamp(value, 0f, MaxDeliveredPower) : 0f;
        }

        private float ResolveSafeSecondsPerCorner()
        {
            return math.isfinite(secondsPerCorner)
                ? math.clamp(secondsPerCorner, 0.001f, MaxSecondsPerCorner)
                : 1.25f;
        }

        private float ResolveSafeWeldRadiusMeters()
        {
            return math.isfinite(weldRadiusMeters)
                ? math.clamp(weldRadiusMeters, 0.01f, MaxWeldRadiusMeters)
                : 0.18f;
        }

        private float ResolveSafeRequiredPower()
        {
            return math.isfinite(requiredPower) ? math.clamp(requiredPower, 0f, MaxDeliveredPower) : 0.1f;
        }

        private float ResolveSafeWeldHeatHoldAfterContactSeconds()
        {
            return math.isfinite(weldHeatHoldAfterContactSeconds)
                ? math.clamp(weldHeatHoldAfterContactSeconds, 0f, MaxWeldHeatHoldAfterContactSeconds)
                : 0.08f;
        }

        private float ResolveSafeWeldCooldownSecondsPerSecond()
        {
            return math.isfinite(weldCooldownSecondsPerSecond)
                ? math.clamp(weldCooldownSecondsPerSecond, 0f, MaxWeldCooldownSecondsPerSecond)
                : 0.35f;
        }

        private float ResolveSafeWeldGlowDurationSeconds()
        {
            return math.isfinite(weldGlowDurationSeconds)
                ? math.clamp(weldGlowDurationSeconds, 0.01f, MaxWeldGlowDurationSeconds)
                : 0.1f;
        }

        private float ResolveSafeWeldGlowRangeMeters()
        {
            return math.isfinite(weldGlowRangeMeters)
                ? math.clamp(weldGlowRangeMeters, 0.01f, MaxWeldGlowRangeMeters)
                : 1.25f;
        }

        private float ResolveSafeWeldGlowProxyIntensity()
        {
            return math.isfinite(weldGlowProxyIntensity)
                ? math.clamp(weldGlowProxyIntensity, 0f, MaxWeldGlowProxyIntensity)
                : 0.82f;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!math.isfinite(secondsPerCorner) || secondsPerCorner < 0.05f)
                secondsPerCorner = 0.05f;
            secondsPerCorner = math.min(secondsPerCorner, MaxSecondsPerCorner);
            if (!math.isfinite(weldRadiusMeters) || weldRadiusMeters < 0.01f)
                weldRadiusMeters = 0.01f;
            weldRadiusMeters = math.min(weldRadiusMeters, MaxWeldRadiusMeters);
            if (!math.isfinite(requiredPower) || requiredPower < 0f)
                requiredPower = 0f;
            requiredPower = math.min(requiredPower, MaxDeliveredPower);
            if (!math.isfinite(weldHeatHoldAfterContactSeconds) || weldHeatHoldAfterContactSeconds < 0f)
                weldHeatHoldAfterContactSeconds = 0f;
            weldHeatHoldAfterContactSeconds = math.min(weldHeatHoldAfterContactSeconds, MaxWeldHeatHoldAfterContactSeconds);
            if (!math.isfinite(weldCooldownSecondsPerSecond) || weldCooldownSecondsPerSecond < 0f)
                weldCooldownSecondsPerSecond = 0f;
            weldCooldownSecondsPerSecond = math.min(weldCooldownSecondsPerSecond, MaxWeldCooldownSecondsPerSecond);
            if (!math.isfinite(weldGlowDurationSeconds) || weldGlowDurationSeconds < 0.01f)
                weldGlowDurationSeconds = 0.01f;
            weldGlowDurationSeconds = math.min(weldGlowDurationSeconds, MaxWeldGlowDurationSeconds);
            if (!math.isfinite(weldGlowRangeMeters) || weldGlowRangeMeters < 0.01f)
                weldGlowRangeMeters = 0.01f;
            weldGlowRangeMeters = math.min(weldGlowRangeMeters, MaxWeldGlowRangeMeters);
            if (!math.isfinite(weldGlowProxyIntensity) || weldGlowProxyIntensity < 0f)
                weldGlowProxyIntensity = 0f;
            weldGlowProxyIntensity = math.min(weldGlowProxyIntensity, MaxWeldGlowProxyIntensity);
        }
#endif
    }
}
