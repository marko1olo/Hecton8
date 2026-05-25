using Hecton8.Core;
using Hecton8.Items;
using Hecton8.Physics;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Caves
{
    /// <summary>
    /// Cave-authored geyser marker. Physical lift is delegated to VolcanicUpdraftDirector;
    /// this component only supplies authored cadence and mineral-drop flavor.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CurrentVolume))]
    public sealed class ThermalGeyser : MonoBehaviour, ITickable, IUpdatable, IFixedTickable, IGlobalRegistryHotSwapListener
    {
        private const float MinimumCylinderHeightMeters = 1f;
        private const float EruptionCylinderHeightMultiplier = 2.25f;
        private const float DefaultMineralEjectionIntervalSeconds = 600f;
        private const int MinimumEjectedMineralCount = 3;
        private const int MaximumEjectedMineralCount = 5;

        [Header("Runtime Wiring")]
        [SerializeField]
        [Tooltip("Authoring-only current-volume marker. Runtime lift is handled by VolcanicUpdraftDirector.")]
        private CurrentVolume currentVolume;

        [Header("Mineral Ejection")]
        [SerializeField]
        [Tooltip("Low-tier mineral item emitted as loot during long-lived geyser activity.")]
        private ItemData ejectedMineralItem;

        [SerializeField, Range(60f, 1800f)]
        [Tooltip("Seconds between mineral ejection bursts while the geyser is erupting.")]
        private float mineralEjectionIntervalSeconds = DefaultMineralEjectionIntervalSeconds;

        [SerializeField, Range(0.1f, 30f)]
        [Tooltip("Impulse magnitude stored on ejected item records when loot hydrates.")]
        private float mineralEjectionImpulse = 8f;

        private float _quietDuration = 10f;
        private float _eruptionDuration = 3f;
        private float _eruptionRadius = 4f;
        private float _cavitationRadius = 6f;
        private float _updraftStrength = 500f;
        private float _phaseTimer;
        private bool _isErupting;
        private bool _registeredTick;
        private bool _registeredFixedTick;
        private bool _hotSwapRegistered;
        private float _mineralEjectionTimer = DefaultMineralEjectionIntervalSeconds;
        private uint _mineralEjectionSeed;
        private uint _volcanicVentSourceHash;
        private VolcanicUpdraftDirector _volcanicDirector;
        private PersistentWorldRegistry _persistentWorldRegistry;

        internal void Configure(ThermalGeyserConfig config, float globalIntensity)
        {
            if (config == null)
                return;

            _quietDuration = Mathf.Max(0.5f, config.quietDuration);
            _eruptionDuration = Mathf.Max(0.5f, config.eruptionDuration);
            _eruptionRadius = Mathf.Max(0.5f, config.eruptionRadius);
            _cavitationRadius = Mathf.Max(_eruptionRadius, config.cavitationRadius);
            _updraftStrength = Mathf.Max(0f, config.updraftStrength * Mathf.Max(0.1f, globalIntensity));

            ResolveRuntimeWiring();
            ConfigureCurrentVolume();
            _phaseTimer = _quietDuration;
            _isErupting = false;
            _mineralEjectionTimer = Mathf.Max(60f, mineralEjectionIntervalSeconds);
        }

        public void Tick(float dt)
        {
            float safeDt = Mathf.Max(0f, dt);
            TickMineralEjection(safeDt);

            _phaseTimer -= safeDt;
            if (_phaseTimer > 0f)
                return;

            _isErupting = !_isErupting;
            _phaseTimer = _isErupting ? _eruptionDuration : _quietDuration;
            ConfigureCurrentVolume();
        }

        public void FixedTick(float fdt)
        {
            if (fdt <= 0f)
                return;

            SubmitVolcanicDirectorVent();
        }

        private static uint Mix(uint hash, uint value)
        {
            hash ^= value + 0x9E3779B9u + (hash << 6) + (hash >> 2);
            return hash != 0u ? hash : 0xA341316Cu;
        }

        private static float Next01(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state & 0x00FFFFFFu) * (1f / 16777215f);
        }

        private void Awake()
        {
            ResolveRuntimeWiring();
            CacheRegistryServicesCold();
            _mineralEjectionSeed = unchecked((uint)EntityId.ToULong(GetEntityId())) ^ 0x9E3779B9u;
            _volcanicVentSourceHash = Mix(_mineralEjectionSeed, VolcanicUpdraftVault.SourceHash);
            _mineralEjectionTimer = Mathf.Max(60f, mineralEjectionIntervalSeconds);
        }

        private void OnEnable()
        {
            ResolveRuntimeWiring();
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            TryRegister();
        }

        private void Start()
        {
            ResolveRuntimeWiring();
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            TryUnregister();
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            TryUnregister();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.PersistentWorldRegistry:
                    _persistentWorldRegistry = currentService as PersistentWorldRegistry;
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    _registeredTick = false;
                    _registeredFixedTick = false;
                    if (currentService != null && isActiveAndEnabled)
                        TryRegister();
                    break;
            }
        }

        private void TickMineralEjection(float dt)
        {
            if (dt <= 0f || ejectedMineralItem == null)
                return;

            _mineralEjectionTimer -= dt;
            if (_mineralEjectionTimer > 0f || !_isErupting)
                return;

            _mineralEjectionTimer = Mathf.Max(60f, mineralEjectionIntervalSeconds);
            EjectMineralBurst();
        }

        private void EjectMineralBurst()
        {
            PersistentWorldRegistry registry = _persistentWorldRegistry;
            if (registry == null || ejectedMineralItem == null)
                return;

            uint state = _mineralEjectionSeed;
            _mineralEjectionSeed = Mix(_mineralEjectionSeed, 0xA511E9B3u);
            int count = MinimumEjectedMineralCount + (int)Mathf.Floor(Next01(ref state) * (MaximumEjectedMineralCount - MinimumEjectedMineralCount + 1));
            count = Mathf.Clamp(count, MinimumEjectedMineralCount, MaximumEjectedMineralCount);
            Vector3 origin = transform.position;
            for (int i = 0; i < count; i++)
            {
                Vector3 lateral = new Vector3((Next01(ref state) * 2f) - 1f, 0f, (Next01(ref state) * 2f) - 1f);
                if (lateral.sqrMagnitude <= 0.0001f)
                    lateral = Vector3.right;
                lateral.Normalize();

                Vector3 spawnPosition = origin + (Vector3.up * 0.25f) + (lateral * math.lerp(0.15f, 0.6f, Next01(ref state)));
                Vector3 impulse = (Vector3.up * math.lerp(0.85f, 1.25f, Next01(ref state)) * mineralEjectionImpulse) +
                                  (lateral * (mineralEjectionImpulse * 0.25f));
                registry.TryRegisterDroppedItem(ejectedMineralItem, 1, spawnPosition, impulse);
            }
        }

        private void ResolveRuntimeWiring()
        {
            if (currentVolume == null)
                TryGetComponent(out currentVolume);

            _volcanicDirector = VolcanicUpdraftDirector.ActiveRuntimeInstance;
        }

        private void ConfigureCurrentVolume()
        {
            if (currentVolume == null)
                return;

            currentVolume.ApplySemanticBoundsPreset(
                CurrentVolume.VolumeShape.Sphere,
                Vector3.one * (_cavitationRadius * 2f),
                _cavitationRadius);
            currentVolume.ApplySemanticFlowPreset(
                CurrentVolume.FlowPattern.Updraft,
                Vector3.up,
                0f,
                1f,
                0f);
        }

        private void SubmitVolcanicDirectorVent()
        {
            VolcanicUpdraftDirector director = _volcanicDirector;
            if (director == null)
                return;

            if (!TryResolveVentAup(out AbsoluteUniversePosition aup))
                return;

            float eruptionHeight = math.max(MinimumCylinderHeightMeters, _eruptionRadius * EruptionCylinderHeightMultiplier);
            float active01 = _isErupting ? 1f : 0f;
            float timer01 = _eruptionDuration > 0.0001f
                ? math.saturate(1f - (_phaseTimer / math.max(0.0001f, _eruptionDuration)))
                : active01;

            director.TryUpsertAuthoredVent(
                _volcanicVentSourceHash,
                aup.ToAbsoluteDouble3(),
                math.max(0.5f, _eruptionRadius),
                _updraftStrength * active01,
                eruptionHeight,
                active01,
                timer01);
        }

        private bool TryResolveVentAup(out AbsoluteUniversePosition aup)
        {
            aup = default;
            Vector3 runtimePosition = transform.position;
            if (!math.isfinite(runtimePosition.x) ||
                !math.isfinite(runtimePosition.y) ||
                !math.isfinite(runtimePosition.z))
            {
                return false;
            }

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            aup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return aup.IsFinite();
        }

        private void TryRegister()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredTick)
            {
                _registeredTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            }

            if (_registeredFixedTick)
                return;

            _registeredFixedTick = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (_registeredTick)
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);

            if (_registeredFixedTick)
                GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);

            _registeredTick = false;
            _registeredFixedTick = false;
        }

        private void CacheRegistryServicesCold()
        {
            _persistentWorldRegistry = GlobalRegistry.PersistentWorldRegistry;
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
    }
}
