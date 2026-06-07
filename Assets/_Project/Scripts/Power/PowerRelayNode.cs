using System.Collections.Generic;
using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Power
{
    /// <summary>
    /// Lightweight authored relay that extends the existing PowerNode network with cable visuals and passive transmission loss.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PowerNode))]
    [AddComponentMenu("Hecton8/Power/Power Relay Node")]
    public sealed class PowerRelayNode : MonoBehaviour, IPowerComponent, IPoolable, ISlowTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener, IPowerActivationTarget
    {
        private const float PositionRefreshEpsilonSqr = 0.0004f;

        [Header("── Cable Visualization ──────────────────")]
        [Tooltip("Cable color while the relay still has power.")]
        [SerializeField] private Color poweredCableColor = new Color(0.25f, 0.95f, 1f, 0.95f);

        [Tooltip("Cable color while the relay has been depowered by grid deficit.")]
        [SerializeField] private Color unpoweredCableColor = new Color(0.35f, 0.42f, 0.48f, 0.55f);

        [Header("── Resistance Loss ──────────────────────")]
        [Tooltip("Baseline relay standby draw once at least one cable path is connected.")]
        [SerializeField, Range(0f, 20f)] private float standbyDrain = 1.5f;

        [Tooltip("Extra passive draw for each relay-to-relay handoff in the local path graph.")]
        [SerializeField, Range(0f, 10f)] private float relayHandoffLoss = 0.35f;

        [Tooltip("Passive line loss applied per meter of connected cable path.")]
        [SerializeField, Range(0f, 1f)] private float resistanceLossPerMeter = 0.06f;

        [Tooltip("Priority used when the grid starts shedding non-critical loads. Lower is more critical.")]
        [SerializeField, Range(0, 100)] private int powerPriority = 15;

        [Header("── Diagnostics ─────────────────────────")]
        [SerializeField] private bool _debugHasPower = true;
        [SerializeField] private int _debugNeighborCount;
        [SerializeField] private int _debugRelayNeighborCount;
        [SerializeField] private float _debugCableLengthMeters;
        [SerializeField] private float _debugPassiveLoss;

        private PowerNode _powerNode;
        private Transform _cachedTransform;
        private bool _registered;
        private bool _registeredLateFrame;
        private bool _hotSwapRegistered;
        private bool _localReferenceProbeCompleted;
        private bool _hasPower = true;
        private bool _cableVisualRefreshPending;
        private bool _cableVisualClearPending;
        private float _runtimeActivation01 = 1f;
        private float _currentPassiveLoss;
        private Vector3 _lastPosition;
        private int _lastVisualPointCount = -1;
        private int _lastTopologyRevision = -1;
        // COLD ALLOC: List<long>[8] - submitted relay cable link ids retained between SlowTick refreshes - owner: PowerRelayNode
        private readonly List<long> _submittedLinkIds = new List<long>(8);
        // COLD ALLOC: List<long>[8] - scratch relay cable link ids for zero-GC diffing during SlowTick - owner: PowerRelayNode
        private readonly List<long> _scratchLinkIds = new List<long>(8);

        /// <summary>Dynamic passive drain authored by this relay.</summary>
        public float PowerRating => -_currentPassiveLoss * _runtimeActivation01;

        /// <summary>Power deficit shedding priority. Lower values stay online longer.</summary>
        public int PowerPriority => powerPriority;

        /// <summary>Cached power availability propagated by the active grid.</summary>
        public bool HasPower => _hasPower;

        public void OnPowerStatusChanged(bool hasPower)
        {
            _hasPower = hasPower;
            _debugHasPower = hasPower;
            RefreshRelayLinks(true);
        }

        public bool SetRuntimeActivation01(float activation01)
        {
            float sanitized = math.saturate(math.select(1f, activation01, math.isfinite(activation01)));
            if (math.abs(_runtimeActivation01 - sanitized) <= 0.0001f)
                return false;

            _runtimeActivation01 = sanitized;
            PowerGrid grid = _powerNode != null ? _powerNode.Grid : null;
            grid?.MarkDirty();
            RefreshRelayLinks(true);
            return true;
        }

        private void Awake()
        {
            _cachedTransform = transform;
            ResolveReferencesCold();
        }

        private void OnEnable()
        {
            TryRegisterHotSwapListener();
            TryRegister();
            ResolveReferencesCold();
            RefreshRelayLinks(true);
        }

        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterHotSwapListener();
            ClearCableVisuals();
        }

        public void OnSpawn()
        {
            _hasPower = true;
            _debugHasPower = true;
            _localReferenceProbeCompleted = false;
            ResolveReferencesCold();
            TryRegisterHotSwapListener();
            TryRegister();
            RefreshRelayLinks(true);
        }

        public void OnDespawn()
        {
            TryUnregister();
            TryUnregisterHotSwapListener();
            _runtimeActivation01 = 1f;
            _currentPassiveLoss = 0f;
            _debugPassiveLoss = 0f;
            _debugCableLengthMeters = 0f;
            _debugNeighborCount = 0;
            _debugRelayNeighborCount = 0;
            ClearCableVisuals();
            _hasPower = true;
            _debugHasPower = true;
            _lastTopologyRevision = -1;
            _localReferenceProbeCompleted = false;
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterHotSwapListener();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            TryUnregister();
            if (currentService != null && isActiveAndEnabled)
                TryRegister();
        }

        public void SlowTick()
        {
            RefreshRelayLinks(false);
        }

        public void LateFrameTick()
        {
            if (_cableVisualClearPending)
            {
                _cableVisualClearPending = false;
                _cableVisualRefreshPending = false;
                ClearCableVisuals();
                return;
            }

            if (!_cableVisualRefreshPending)
                return;

            _cableVisualRefreshPending = false;
            ResolveReferencesCached();
            if (_powerNode == null)
            {
                ClearCableVisuals();
                return;
            }

            List<PowerNode> neighbors = _powerNode.Neighbors;
            int neighborCount = neighbors != null ? neighbors.Count : 0;
            Vector3 relayPosition = _cachedTransform != null ? _cachedTransform.position : transform.position;
            RefreshCableVisuals(relayPosition, neighbors, neighborCount);
        }

        private void ResolveReferencesCached()
        {
            if (_cachedTransform == null)
                _cachedTransform = transform;
        }

        private void ResolveReferencesCold()
        {
            ResolveReferencesCached();

            if (_powerNode == null && !_localReferenceProbeCompleted)
                TryGetComponent(out _powerNode);

            _localReferenceProbeCompleted = true;
        }

        private void TryRegister()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registered)
                _registered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }

            if (_registered)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registered = false;
            }

            _cableVisualRefreshPending = false;
            _cableVisualClearPending = false;
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

        private void RefreshRelayLinks(bool forceVisualRefresh)
        {
            ResolveReferencesCached();

            if (_powerNode == null)
            {
                _currentPassiveLoss = 0f;
                QueueCableVisualClear();
                return;
            }

            List<PowerNode> neighbors = _powerNode.Neighbors;
            int neighborCount = neighbors != null ? neighbors.Count : 0;
            Vector3 relayPosition = _cachedTransform.position;
            bool moved = (relayPosition - _lastPosition).sqrMagnitude > PositionRefreshEpsilonSqr;
            int topologyRevision = _powerNode.TopologyRevision;
            if (!forceVisualRefresh && !moved && topologyRevision == _lastTopologyRevision)
                return;

            _lastPosition = relayPosition;
            _lastTopologyRevision = topologyRevision;
            float totalHalfCableLength = 0f;
            int relayNeighborCount = 0;

            for (int i = 0; i < neighborCount; i++)
            {
                PowerNode neighbor = neighbors[i];
                if (neighbor == null)
                    continue;

                totalHalfCableLength += ResolvePresentationCableLengthApproxMeters(relayPosition, neighbor.transform) * 0.5f;

                if (HasRelayPowerComponent(neighbor))
                    relayNeighborCount++;
            }

            _currentPassiveLoss = neighborCount > 0
                ? standbyDrain + relayNeighborCount * relayHandoffLoss + totalHalfCableLength * resistanceLossPerMeter
                : 0f;

            _debugNeighborCount = neighborCount;
            _debugRelayNeighborCount = relayNeighborCount;
            _debugCableLengthMeters = totalHalfCableLength * 2f;
            _debugPassiveLoss = _currentPassiveLoss;

            if (forceVisualRefresh || moved || neighborCount != _submittedLinkIds.Count)
                QueueCableVisualRefresh();
        }

        private void QueueCableVisualRefresh()
        {
            _cableVisualRefreshPending = true;
            _cableVisualClearPending = false;
        }

        private void QueueCableVisualClear()
        {
            _cableVisualRefreshPending = false;
            _cableVisualClearPending = true;
        }

        private void RefreshCableVisuals(Vector3 relayPosition, List<PowerNode> neighbors, int neighborCount)
        {
            if (neighborCount <= 0)
            {
                ClearCableVisuals();
                return;
            }

            _scratchLinkIds.Clear();

            for (int i = 0; i < neighborCount; i++)
            {
                PowerNode neighbor = neighbors[i];
                if (neighbor == null)
                    continue;

                long linkId = ComposeRelayLinkId(_powerNode, neighbor);
                _scratchLinkIds.Add(linkId);
                ConnectionSplineBatchRenderer.SubmitRelayLink(
                    linkId,
                    relayPosition,
                    neighbor.transform.position,
                    _hasPower,
                    poweredCableColor,
                    unpoweredCableColor);
            }

            for (int submittedIndex = _submittedLinkIds.Count - 1; submittedIndex >= 0; submittedIndex--)
            {
                long submittedLinkId = _submittedLinkIds[submittedIndex];
                if (ContainsLinkId(_scratchLinkIds, submittedLinkId))
                    continue;

                ConnectionSplineBatchRenderer.RemoveRelayLink(submittedLinkId);
                _submittedLinkIds.RemoveAt(submittedIndex);
            }

            _submittedLinkIds.Clear();
            int submittedCount = _scratchLinkIds.Count;
            for (int linkIndex = 0; linkIndex < submittedCount; linkIndex++)
                _submittedLinkIds.Add(_scratchLinkIds[linkIndex]);

            _lastVisualPointCount = submittedCount;
        }

        private void ClearCableVisuals()
        {
            for (int linkIndex = _submittedLinkIds.Count - 1; linkIndex >= 0; linkIndex--)
                ConnectionSplineBatchRenderer.RemoveRelayLink(_submittedLinkIds[linkIndex]);

            _submittedLinkIds.Clear();

            _lastVisualPointCount = -1;
        }

        private static long ComposeRelayLinkId(PowerNode sourceNode, PowerNode destinationNode)
        {
            if (sourceNode == null || destinationNode == null)
                return 0L;

            uint sourceId = unchecked((uint)EntityId.ToULong(sourceNode.GetEntityId()));
            uint destinationId = unchecked((uint)EntityId.ToULong(destinationNode.GetEntityId()));
            uint minId = sourceId < destinationId ? sourceId : destinationId;
            uint maxId = sourceId < destinationId ? destinationId : sourceId;
            return ((long)minId << 32) | maxId;
        }

        private static bool ContainsLinkId(List<long> linkIds, long linkId)
        {
            int count = linkIds.Count;
            for (int index = 0; index < count; index++)
            {
                if (linkIds[index] == linkId)
                    return true;
            }

            return false;
        }

        private static bool HasRelayPowerComponent(PowerNode node)
        {
            if (node == null || node.Components == null)
                return false;

            List<IPowerComponent> components = node.Components;
            int count = components.Count;
            for (int i = 0; i < count; i++)
            {
                if (components[i] is PowerRelayNode)
                    return true;
            }

            return false;
        }

        private static float ResolvePresentationCableLengthApproxMeters(Vector3 sourcePosition, Transform destinationTransform)
        {
            if (destinationTransform == null)
                return 0f;

            Vector3 destinationPosition = destinationTransform.position;
            double3 delta = math.abs(new double3(
                (double)destinationPosition.x - sourcePosition.x,
                (double)destinationPosition.y - sourcePosition.y,
                (double)destinationPosition.z - sourcePosition.z));
            double maxAxis = math.max(delta.x, math.max(delta.y, delta.z));
            double minAxis = math.min(delta.x, math.min(delta.y, delta.z));
            double midAxis = delta.x + delta.y + delta.z - maxAxis - minAxis;
            double approximateLength = maxAxis + (midAxis * 0.5d) + (minAxis * 0.25d);
            return (float)math.min(approximateLength, (double)float.MaxValue);
        }
    }
}
