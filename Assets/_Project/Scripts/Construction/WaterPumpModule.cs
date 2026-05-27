using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Logistics;
using Hecton8.Power;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Construction
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PowerNode))]
    [AddComponentMenu("Hecton8/Construction/Water Pump Module")]
    public sealed class WaterPumpModule : MonoBehaviour, IPowerComponent, IPoolable
    {
        private const int MaxPumpCapacity = 32;

        [Header("Pump")]
        [SerializeField, Min(0f)] private float pumpRateM3PerSecond = 1.8f;
        [SerializeField, Min(0f)] private float powerDrawWatts = 2400f;
        [SerializeField, Range(0, 100)] private int powerPriority = 8;

        [Header("Pipe Graph")]
        [SerializeField, Min(0.001f)] private float pipeCapacityM3 = 2f;
        [SerializeField, Min(0.1f)] private float pipeMaxPressureKPa = 160f;

        [Header("Diagnostics")]
        [SerializeField] private bool _debugHasPower = true;
        [SerializeField] private float _debugLastDrainBudgetM3;

        // COLD ALLOC: List<WaterPumpModule>[16] - active pump registry for CSR flood drainage - owner: WaterPumpModule
        private static readonly List<WaterPumpModule> s_activePumps = new List<WaterPumpModule>(MaxPumpCapacity);

        private BaseModule _hostModule;
        private ISubmarineAtmosphereRoomReadModel _atmosphereSystem;
        private IFluidPipeGraphService _pipeGraphService;
        private bool _hasPower = true;
        private bool _registered;
        private int _waterPipeNodeIndex = -1;
        private int _waterPipeOutletNodeIndex = -1;
        private bool _waterPipeOutletConnected;

        public float PowerRating => -math.max(0f, powerDrawWatts);
        public int PowerPriority => powerPriority;
        public bool HasPower => _hasPower;
        internal BaseModule HostModule => _hostModule;
        internal bool CanPump => isActiveAndEnabled && _hasPower && pumpRateM3PerSecond > 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_activePumps.Clear();
        }

        private void Awake()
        {
            CacheColdReferences();
        }

        private void OnEnable()
        {
            Register();
        }

        private void OnDisable()
        {
            DisableWaterPipeNode(forgetNode: false);
            Unregister();
        }

        private void OnDestroy()
        {
            DisableWaterPipeNode(forgetNode: true);
            Unregister();
        }

        public void OnSpawn()
        {
            _hasPower = true;
            _debugHasPower = true;
            Register();
        }

        public void OnDespawn()
        {
            DisableWaterPipeNode(forgetNode: true);
            Unregister();
            _hasPower = true;
            _debugHasPower = true;
            _debugLastDrainBudgetM3 = 0f;
        }

        public void OnPowerStatusChanged(bool hasPower)
        {
            _hasPower = hasPower;
            _debugHasPower = hasPower;
        }

        internal static int ActivePumpCount => s_activePumps.Count;

        internal static WaterPumpModule GetActivePump(int index)
        {
            return index >= 0 && index < s_activePumps.Count ? s_activePumps[index] : null;
        }

        internal float ResolveDrainBudgetM3(float deltaTime)
        {
            float budget = CalculatePumpDrainVolumeM3(pumpRateM3PerSecond, _hasPower ? 1f : 0f, deltaTime);
            _debugLastDrainBudgetM3 = budget;
            return budget;
        }

        internal bool TryEnsureWaterPipeNode(IFluidPipeGraphService graph, out int nodeIndex)
        {
            nodeIndex = -1;
            if (graph == null || !graph.IsInitialized)
                return false;

            if (!ReferenceEquals(_pipeGraphService, graph))
            {
                _pipeGraphService = graph;
                _waterPipeNodeIndex = -1;
                _waterPipeOutletNodeIndex = -1;
                _waterPipeOutletConnected = false;
            }

            int networkId = ResolvePipeNetworkId();
            if (_waterPipeNodeIndex >= 0 &&
                graph.TryReadPipeNode(_waterPipeNodeIndex, out _, out _, out byte cachedFlags))
            {
                byte requiredFlags = (byte)(FluidPipeFlags.Active | FluidPipeFlags.PumpIngress | FluidPipeFlags.RoomCoupled);
                if ((cachedFlags & (byte)FluidPipeFlags.Disabled) == 0 &&
                    (cachedFlags & (byte)FluidPipeFlags.Ruptured) == 0 &&
                    (cachedFlags & requiredFlags) == requiredFlags)
                {
                    if (TryConnectDefaultOutlet(graph, networkId, _waterPipeNodeIndex))
                    {
                        nodeIndex = _waterPipeNodeIndex;
                        return true;
                    }

                    DisableWaterPipeNode(forgetNode: false);
                    return false;
                }

                if ((cachedFlags & (byte)FluidPipeFlags.Ruptured) == 0 &&
                    graph.TrySetPipeNodeFlags(
                        _waterPipeNodeIndex,
                        requiredFlags,
                        (byte)FluidPipeFlags.Disabled))
                {
                    if (TryConnectDefaultOutlet(graph, networkId, _waterPipeNodeIndex))
                    {
                        nodeIndex = _waterPipeNodeIndex;
                        return true;
                    }

                    DisableWaterPipeNode(forgetNode: false);
                    return false;
                }
            }

            if (!TryResolveAupFromRuntimeOrigin(ResolvePipeRuntimePosition(), out AbsoluteUniversePosition nodeAup))
                return false;

            if (!graph.TryRegisterPipeNode(
                    networkId,
                    ResolvePipeRoomIndex(),
                    (byte)FluidPipeContentKind.Water,
                    nodeAup,
                    math.max(0.001f, pipeCapacityM3),
                    math.max(0.1f, pipeMaxPressureKPa),
                    out nodeIndex))
            {
                return false;
            }

            _waterPipeNodeIndex = nodeIndex;
            graph.TrySetPipeNodeFlags(
                nodeIndex,
                (byte)(FluidPipeFlags.Active | FluidPipeFlags.PumpIngress | FluidPipeFlags.RoomCoupled),
                (byte)FluidPipeFlags.Disabled);
            if (TryConnectDefaultOutlet(graph, networkId, nodeIndex))
                return true;

            DisableWaterPipeNode(forgetNode: false);
            nodeIndex = -1;
            return false;
        }

        internal static float CalculatePumpDrainVolumeM3(float rateM3PerSecond, float powerSupplyRatio, float deltaTime)
        {
            if (rateM3PerSecond <= 0f || powerSupplyRatio <= 0f || deltaTime <= 0f)
                return 0f;

            float volume = rateM3PerSecond * math.saturate(powerSupplyRatio) * deltaTime;
            return math.isfinite(volume) ? math.max(0f, volume) : 0f;
        }

        private void Register()
        {
            if (_registered)
                return;

            CacheColdReferences();

            if (s_activePumps.Count >= MaxPumpCapacity)
                return;

            s_activePumps.Add(this);
            _registered = true;
        }

        private void Unregister()
        {
            if (!_registered)
                return;

            for (int i = s_activePumps.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(s_activePumps[i], this))
                    s_activePumps.RemoveAt(i);
            }

            _registered = false;
        }

        private void DisableWaterPipeNode(bool forgetNode)
        {
            _waterPipeOutletConnected = false;
            if (_pipeGraphService != null && _waterPipeNodeIndex >= 0)
            {
                _pipeGraphService.TrySetPipeDemandRate(_waterPipeNodeIndex, 0f);
                _pipeGraphService.TrySetPipeSourceRate(_waterPipeNodeIndex, 0f);
                _pipeGraphService.TrySetPipeNodeFlags(
                    _waterPipeNodeIndex,
                    (byte)FluidPipeFlags.Disabled,
                    (byte)(FluidPipeFlags.PumpIngress | FluidPipeFlags.RoomCoupled));
            }

            if (_pipeGraphService != null && _waterPipeOutletNodeIndex >= 0)
            {
                _pipeGraphService.TrySetPipeDemandRate(_waterPipeOutletNodeIndex, 0f);
                _pipeGraphService.TrySetPipeSourceRate(_waterPipeOutletNodeIndex, 0f);
                _pipeGraphService.TrySetPipeNodeFlags(
                    _waterPipeOutletNodeIndex,
                    (byte)FluidPipeFlags.Disabled,
                    (byte)FluidPipeFlags.Outside);
            }

            if (forgetNode)
            {
                _pipeGraphService = null;
                _waterPipeNodeIndex = -1;
                _waterPipeOutletNodeIndex = -1;
                _waterPipeOutletConnected = false;
            }
        }

        private void CacheColdReferences()
        {
            if (_hostModule == null)
                TryGetComponent(out _hostModule);
            if (_hostModule == null)
                ConstructionParentLookup.TryCaptureSelfOrParent(this, out _hostModule);
            if (_atmosphereSystem == null || !_atmosphereSystem.IsAtmosphereRuntimeActive)
                _atmosphereSystem = ComponentReferenceUtility.ResolveParentService<ISubmarineAtmosphereRoomReadModel>(this);
        }

        private bool TryConnectDefaultOutlet(IFluidPipeGraphService graph, int networkId, int ingressNodeIndex)
        {
            if (graph == null || ingressNodeIndex < 0 || _waterPipeOutletConnected)
                return _waterPipeOutletConnected;

            if (!TryEnsureWaterOutletPipeNode(graph, networkId, out int outletNodeIndex))
                return false;

            _waterPipeOutletConnected = graph.TryConnectPipeNodes(ingressNodeIndex, outletNodeIndex);
            return _waterPipeOutletConnected;
        }

        private bool TryEnsureWaterOutletPipeNode(IFluidPipeGraphService graph, int networkId, out int nodeIndex)
        {
            nodeIndex = -1;
            if (graph == null || !graph.IsInitialized)
                return false;

            if (_waterPipeOutletNodeIndex >= 0 &&
                graph.TryReadPipeNode(_waterPipeOutletNodeIndex, out _, out _, out byte cachedFlags))
            {
                byte requiredFlags = (byte)(FluidPipeFlags.Active | FluidPipeFlags.Outside);
                if ((cachedFlags & (byte)FluidPipeFlags.Disabled) == 0 &&
                    (cachedFlags & (byte)FluidPipeFlags.Ruptured) == 0 &&
                    (cachedFlags & requiredFlags) == requiredFlags)
                {
                    nodeIndex = _waterPipeOutletNodeIndex;
                    return true;
                }

                if ((cachedFlags & (byte)FluidPipeFlags.Ruptured) == 0 &&
                    graph.TrySetPipeNodeFlags(
                        _waterPipeOutletNodeIndex,
                        requiredFlags,
                        (byte)FluidPipeFlags.Disabled))
                {
                    nodeIndex = _waterPipeOutletNodeIndex;
                    return true;
                }
            }

            if (!TryResolveAupFromRuntimeOrigin(ResolvePipeOutletRuntimePosition(), out AbsoluteUniversePosition nodeAup))
                return false;

            if (!graph.TryRegisterPipeNode(
                    networkId,
                    -1,
                    (byte)FluidPipeContentKind.Water,
                    nodeAup,
                    math.max(0.001f, pipeCapacityM3),
                    math.max(0.1f, pipeMaxPressureKPa),
                    out nodeIndex))
            {
                return false;
            }

            _waterPipeOutletNodeIndex = nodeIndex;
            _waterPipeOutletConnected = false;
            graph.TrySetPipeNodeFlags(
                nodeIndex,
                (byte)(FluidPipeFlags.Active | FluidPipeFlags.Outside),
                (byte)FluidPipeFlags.Disabled);
            return true;
        }

        private Vector3 ResolvePipeRuntimePosition()
        {
            if (_hostModule != null &&
                _hostModule.TryGetInteriorAabbBounds(out Vector3 center, out Vector3 halfExtents) &&
                halfExtents.sqrMagnitude > 0.0001f)
            {
                return center;
            }

            return transform.position;
        }

        private int ResolvePipeRoomIndex()
        {
            return _atmosphereSystem != null && _atmosphereSystem.IsAtmosphereRuntimeActive
                ? _atmosphereSystem.ResolveNearestRoomIndexForWorldPosition(ResolvePipeRuntimePosition())
                : -1;
        }

        private Vector3 ResolvePipeOutletRuntimePosition()
        {
            Vector3 origin = ResolvePipeRuntimePosition();
            Transform pumpTransform = transform;
            return pumpTransform != null ? origin + (pumpTransform.up * 0.5f) : origin;
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

        private int ResolvePipeNetworkId()
        {
            if (_atmosphereSystem != null && _atmosphereSystem.IsAtmosphereRuntimeActive)
                return _atmosphereSystem.RuntimeEntityIdHash;
            if (_hostModule != null)
                return unchecked((int)EntityId.ToULong(_hostModule.GetEntityId()));

            return unchecked((int)EntityId.ToULong(GetEntityId()));
        }
    }
}
