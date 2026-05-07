using Hecton8.Building;
using Hecton8.Core;
using Hecton8.Interaction;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Construction
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Construction/VR Construction Weld Target")]
    public sealed class VRConstructionWeldTarget : MonoBehaviour, IInteractionSignalConsumer, IOriginShiftListener, IUpdatable
    {
        private const int CornerCount = 4;
        private const float DefaultInteractionStepSeconds = 0.02f;

        [Header("AUP Corners")]
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
        [SerializeField] private ConstructionManager constructionManager;
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
        private readonly AbsoluteUniversePosition[] _cornerAups = new AbsoluteUniversePosition[CornerCount]; // COLD ALLOC: AUP[4] - stable weld corner coordinates - owner: VRConstructionWeldTarget
        private Vector3 _weldGlowRuntimePosition;
        private int _weldGlowProxyKey;
        private float _weldGlowRemainingSeconds;
        private float _weldHeatHoldRemainingSeconds;
        private byte _completedMask;
        private bool _complete;
        private bool _registeredOriginShift;
        private bool _weldGlowProxyRegistered;
        private bool _weldGlowTickRegistered;

        public bool IsComplete => _complete;
        public byte CompletedMask => _completedMask;

        private void Awake()
        {
            _weldGlowProxyKey = unchecked((int)EntityId.ToULong(GetEntityId()) ^ 0x56525744);
            BindCorners();
            CacheCornerAups();
        }

        private void OnEnable()
        {
            BindCorners();
            CacheCornerAups();
            TryRegisterOriginShiftListener();
        }

        private void OnDisable()
        {
            TryUnregisterOriginShiftListener();
            UnregisterWeldGlowProxy();
            TryUnregisterWeldGlowTick();
        }

        private void OnDestroy()
        {
            UnregisterWeldGlowProxy();
            TryUnregisterWeldGlowTick();
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            CacheCornerAups();
            if (_weldGlowProxyRegistered)
                UpdateWeldGlowProxyRegistration();
        }

        public float GetCornerProgress01(int index)
        {
            if ((uint)index >= CornerCount)
                return 0f;

            return math.saturate(_cornerProgressSeconds[index] / math.max(0.001f, secondsPerCorner));
        }

        public bool TryGetCornerAup(int index, out AbsoluteUniversePosition aup)
        {
            if ((uint)index >= CornerCount)
            {
                aup = default;
                return false;
            }

            aup = _cornerAups[index];
            return _corners[index] != null;
        }

        public bool ApplyWeldAtPoint(Vector3 runtimeHitPoint, float deliveredPower, float deltaSeconds)
        {
            if (_complete || deliveredPower < requiredPower || deltaSeconds <= 0f)
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
            float targetProgress = math.max(0.001f, secondsPerCorner);
            if (previousProgress >= targetProgress)
                return true;

            _weldHeatHoldRemainingSeconds = math.max(_weldHeatHoldRemainingSeconds, weldHeatHoldAfterContactSeconds);
            _cornerProgressSeconds[cornerIndex] = math.min(targetProgress, previousProgress + deltaSeconds * deliveredPower);
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
            float dt = math.max(0f, deltaTime);
            UpdateWeldHeatCooling(dt);

            _weldGlowRemainingSeconds = math.max(0f, _weldGlowRemainingSeconds - dt);
            if (_weldGlowRemainingSeconds > 0f)
            {
                UpdateWeldGlowProxyRegistration();
                return;
            }

            UnregisterWeldGlowProxy();
            if (!HasWeldCoolingWork())
                TryUnregisterWeldGlowTick();
        }

        private bool TryFindCorner(Vector3 runtimeHitPoint, out int cornerIndex)
        {
            cornerIndex = -1;
            double bestDistanceSq = (double)weldRadiusMeters * weldRadiusMeters;
            AbsoluteUniversePosition hitAup = AbsoluteUniversePosition.FromRuntimePosition(runtimeHitPoint);
            for (int i = 0; i < CornerCount; i++)
            {
                Transform corner = _corners[i];
                if (corner == null)
                    continue;

                double distanceSq = AbsoluteUniversePosition.DistanceSq(in hitAup, in _cornerAups[i]);
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
            UnregisterWeldGlowProxy();
            TryUnregisterWeldGlowTick();
            RegisterCompletedPanel();
        }

        private void ArmWeldCooling()
        {
            if (_complete || !HasWeldCoolingWork())
                return;

            _weldHeatHoldRemainingSeconds = 0f;
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

            float decay = weldCooldownSecondsPerSecond * deltaTime;
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

            ConstructionManager manager = constructionManager != null
                ? constructionManager
                : GlobalRegistry.ConstructionRuntime;
            if (manager == null)
                return;

            GameObject graphEntry = logisticsGraphEntryRoot != null ? logisticsGraphEntryRoot : gameObject;
            if (panelBuildableData != null)
                manager.RegisterModule(graphEntry, panelBuildableData);
            else
                manager.RegisterModule(graphEntry);
        }

        private void BindCorners()
        {
            _corners[0] = corner0;
            _corners[1] = corner1;
            _corners[2] = corner2;
            _corners[3] = corner3;
        }

        private void CacheCornerAups()
        {
            for (int i = 0; i < CornerCount; i++)
            {
                Transform corner = _corners[i];
                if (corner != null)
                    _cornerAups[i] = AbsoluteUniversePosition.FromRuntimePosition(corner.position);
            }
        }

        private void TriggerWeldGlow(Vector3 runtimePosition)
        {
            _weldGlowRuntimePosition = runtimePosition;
            _weldGlowRemainingSeconds = math.max(_weldGlowRemainingSeconds, math.max(0.01f, weldGlowDurationSeconds));
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
            float lifetime01 = math.saturate(_weldGlowRemainingSeconds / math.max(0.01f, weldGlowDurationSeconds));
            float intensity = math.saturate(weldGlowProxyIntensity * lifetime01);
            Vector3 runtimePosition = weldGlowOrigin != null ? weldGlowOrigin.position : _weldGlowRuntimePosition;
            AbsoluteUniversePosition positionAup = AbsoluteUniversePosition.FromRuntimePosition(runtimePosition);
            ProxyLightData lightData = ProxyLightData.CreateTransientPoint(
                positionAup,
                runtimePosition,
                glowLinear,
                weldGlowRangeMeters,
                intensity,
                Time.unscaledTime);

            _weldGlowProxyRegistered = ProxyLightRegistry.RegisterOrUpdate(_weldGlowProxyKey, in lightData) || _weldGlowProxyRegistered;
        }

        private float ResolveAggregateProgress01()
        {
            float targetProgress = math.max(0.001f, secondsPerCorner);
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
            _registeredOriginShift = true;
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
            if (_weldGlowTickRegistered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Player);
            _weldGlowTickRegistered = GlobalRegistry.Updatables.Contains(this);
        }

        private void TryUnregisterWeldGlowTick()
        {
            if (!_weldGlowTickRegistered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
            _weldGlowTickRegistered = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (secondsPerCorner < 0.05f)
                secondsPerCorner = 0.05f;
            if (weldRadiusMeters < 0.01f)
                weldRadiusMeters = 0.01f;
            if (requiredPower < 0f)
                requiredPower = 0f;
            if (weldHeatHoldAfterContactSeconds < 0f)
                weldHeatHoldAfterContactSeconds = 0f;
            if (weldCooldownSecondsPerSecond < 0f)
                weldCooldownSecondsPerSecond = 0f;
            if (weldGlowDurationSeconds < 0.01f)
                weldGlowDurationSeconds = 0.01f;
            if (weldGlowRangeMeters < 0.01f)
                weldGlowRangeMeters = 0.01f;
            if (weldGlowProxyIntensity < 0f)
                weldGlowProxyIntensity = 0f;
        }
#endif
    }
}
