using System.Collections.Generic;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Power
{
    /// <summary>
    /// Lightweight authored relay that extends the existing PowerNode network with cable visuals and passive transmission loss.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PowerNode))]
    [AddComponentMenu("Hecton8/Power/Power Relay Node")]
    public sealed class PowerRelayNode : MonoBehaviour, IPowerComponent, IPoolable, ISlowTickable
    {
        private const float PositionRefreshEpsilonSqr = 0.0004f;

        [Header("── Cable Visualization ──────────────────")]
        [Tooltip("Optional LineRenderer used to visualize relay cable spokes to neighboring PowerNode links.")]
        [SerializeField] private LineRenderer cableRenderer;

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
        private bool _hasPower = true;
        private float _currentPassiveLoss;
        private Vector3 _lastPosition;
        private int _lastVisualPointCount = -1;
        private int _lastTopologyRevision = -1;
        private readonly List<long> _submittedLinkIds = new List<long>(8);
        private readonly List<long> _scratchLinkIds = new List<long>(8);

        /// <summary>Dynamic passive drain authored by this relay.</summary>
        public float PowerRating => -_currentPassiveLoss;

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

        private void Awake()
        {
            _cachedTransform = transform;
            TryGetComponent(out _powerNode);
        }

        private void OnEnable()
        {
            TryRegister();
            RefreshRelayLinks(true);
        }

        private void OnDisable()
        {
            TryUnregister();
            ClearCableVisuals();
        }

        public void OnSpawn()
        {
            _hasPower = true;
            _debugHasPower = true;
            ResolveReferences();
            TryRegister();
            RefreshRelayLinks(true);
        }

        public void OnDespawn()
        {
            TryUnregister();
            _currentPassiveLoss = 0f;
            _debugPassiveLoss = 0f;
            _debugCableLengthMeters = 0f;
            _debugNeighborCount = 0;
            _debugRelayNeighborCount = 0;
            ClearCableVisuals();
            _hasPower = true;
            _debugHasPower = true;
            _lastTopologyRevision = -1;
        }

        public void SlowTick()
        {
            RefreshRelayLinks(false);
        }

        private void ResolveReferences()
        {
            if (_cachedTransform == null)
                _cachedTransform = transform;

            if (_powerNode == null)
                TryGetComponent(out _powerNode);

            if (cableRenderer == null)
                TryGetComponent(out cableRenderer);
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            _registered = GlobalRegistry.SlowTickables.Contains(this);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registered = false;
        }

        private void RefreshRelayLinks(bool forceVisualRefresh)
        {
            ResolveReferences();

            if (_powerNode == null)
            {
                _currentPassiveLoss = 0f;
                ClearCableVisuals();
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

                totalHalfCableLength += Vector3.Distance(relayPosition, neighbor.transform.position) * 0.5f;

                if (neighbor.TryGetComponent(out PowerRelayNode relayNeighbor) && relayNeighbor != null)
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
                RefreshCableVisuals(relayPosition, neighbors, neighborCount);
        }

        private void RefreshCableVisuals(Vector3 relayPosition, List<PowerNode> neighbors, int neighborCount)
        {
            DisableLegacyCableRenderer();

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

        private void UpdateCableColor()
        {
            DisableLegacyCableRenderer();
        }

        private void ClearCableVisuals()
        {
            for (int linkIndex = _submittedLinkIds.Count - 1; linkIndex >= 0; linkIndex--)
                ConnectionSplineBatchRenderer.RemoveRelayLink(_submittedLinkIds[linkIndex]);

            _submittedLinkIds.Clear();
            DisableLegacyCableRenderer();

            _lastVisualPointCount = -1;
        }

        private void DisableLegacyCableRenderer()
        {
            if (cableRenderer == null)
                return;

            cableRenderer.positionCount = 0;
            cableRenderer.enabled = false;
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
    }
}
