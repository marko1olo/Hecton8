using System.Collections.Generic;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4050)]
    public sealed class WorldContentDirector : MonoBehaviour, ISlowTickable
    {
        private const string NoneLabel = "None";
        private const string UnresolvedZoneId = "zone.none";
        private const string UnresolvedZoneLabel = "Unresolved zone";

        internal static WorldContentDirector ActiveRuntimeInstance { get; private set; }

        [Header("References")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private WorldZoneDirector worldZoneDirector;

        [Header("Runtime Auto Resolve")]
        [SerializeField, Min(0f)] private float autoResolveRetryInterval = 1f;

        [Header("Diagnostics")]
        [SerializeField] private int _debugSocketCount;
        [SerializeField] private int _debugZoneSocketCount;
        [SerializeField] private string _debugNearestSocket = "None";
        [SerializeField] private string _debugNearestKind = "Generic";
        [SerializeField] private string _debugNearestProfile = "None";
        [SerializeField] private string _debugNearestPopulationFamily = "None";
        [SerializeField] private string _debugNearestPopulationRule = "None";
        [SerializeField] private string _debugNearestPopulationBiomeFit = "None";
        [SerializeField] private string _debugNearestPopulationExtraction = "None";
        [SerializeField] private string _debugNearestPopulationLandmark = "None";
        [SerializeField] private string _debugNearestPopulationSpatialRole = "None";
        [SerializeField] private string _debugNearestPopulationSpatialReason = "None";
        [SerializeField] private string _debugNearestPopulationBorderRole = "None";
        [SerializeField] private string _debugNearestPopulationBorderReason = "None";
        [SerializeField] private string _debugNearestPopulationResourceItem = "None";
        [SerializeField] private string _debugNearestPopulationResourceReason = "None";
        [SerializeField] private string _debugNearestPopulationMotivationPull = "None";
        [SerializeField] private string _debugNearestPopulationMotivationReason = "None";
        [SerializeField] private string _debugNearestPopulationSandboxAttractionRole = "None";
        [SerializeField] private string _debugNearestPopulationSandboxAttractionReason = "None";
        [SerializeField] private string _debugNearestZoneRoleFamily = "None";
        [SerializeField] private string _debugNearestZoneRoleLayout = "None";
        [SerializeField] private string _debugNearestZoneRolePriority = "None";
        [SerializeField] private string _debugNearestPopulationPurpose = "None";
        [SerializeField] private float _debugNearestPopulationDensity;
        [SerializeField] private string _debugNearestProceduralRule = "None";
        [SerializeField] private string _debugNearestProceduralFamily = "None";
        [SerializeField] private string _debugNearestProceduralVariant = "None";
        [SerializeField] private string _debugNearestProceduralSource = "None";
        [SerializeField] private string _debugNearestProceduralDomain = "Generic";
        [SerializeField] private string _debugNearestProceduralPlacementMode = "Scatter";
        [SerializeField] private string _debugNearestProceduralHeatmap = "None";
        [SerializeField] private string _debugNearestProceduralIntent = "None";
        [SerializeField] private string _debugNearestProceduralReason = "None";
        [SerializeField] private float _debugNearestProceduralScore;
        [SerializeField] private string _debugCurrentZone = "None";

        private readonly List<WorldContentSocket> _sockets = new List<WorldContentSocket>(128);
        private bool _registeredToTickManager;
        private float _nextAutoResolveAttemptTime = float.NegativeInfinity;

        public IReadOnlyList<WorldContentSocket> Sockets => _sockets;
        public bool HasResolvedContext => playerTransform != null && worldZoneDirector != null;

        private void Awake()
        {
            ActiveRuntimeInstance = this;
            ResolveReferences(force: true);
            RefreshSockets();
            UpdateDiagnostics(null, 0);
        }

        private void OnEnable()
        {
            TryRegister();
        }

        private void Start()
        {
            TryRegister();

            EvaluateSockets(forceRefresh: true);
        }

        private void OnDisable()
        {
            TryUnregister();
        }

        private void OnDestroy()
        {
            TryUnregister();

            if (ActiveRuntimeInstance == this)
                ActiveRuntimeInstance = null;
        }

        private void TryRegister()
        {
            if (_registeredToTickManager || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            _registeredToTickManager = GlobalRegistry.SlowTickables.Contains(this);
        }

        private void TryUnregister()
        {
            if (!_registeredToTickManager)
                return;

                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);

            _registeredToTickManager = false;
        }

        public void SlowTick()
        {
            EvaluateSockets(forceRefresh: false);
        }

        /// <summary>
        /// Forces immediate socket discovery and zone-local content evaluation.
        /// </summary>
        public void ForceRefresh()
        {
            ResolveReferences(force: true);
            RefreshSockets();
            EvaluateSockets(forceRefresh: true);
        }

        public void RefreshSockets()
        {
            WorldContentSocket.CopyActiveSocketsTo(_sockets);
            _debugSocketCount = _sockets.Count;
        }

        private void EvaluateSockets(bool forceRefresh)
        {
            ResolveReferences();
            if (forceRefresh || _sockets.Count == 0)
                RefreshSockets();

            if (playerTransform == null)
            {
                UpdateDiagnostics(null, 0);
                return;
            }

            WorldZoneAnchor currentZone = worldZoneDirector != null ? worldZoneDirector.CurrentZone : null;
            string currentZoneId = ResolveZoneId(currentZone);

            WorldContentSocket nearestSocket = null;
            float nearestDistanceSqr = float.MaxValue;
            int zoneSocketCount = 0;

            for (int i = 0; i < _sockets.Count; i++)
            {
                WorldContentSocket socket = _sockets[i];
                if (socket == null)
                    continue;

                WorldZoneAnchor zoneAnchor = socket.GetZoneAnchor();
                if (zoneAnchor == null || zoneAnchor.ZoneId != currentZoneId)
                    continue;

                zoneSocketCount++;
                float distanceSqr = socket.GetFlatDistanceSquared(playerTransform.position);
                if (distanceSqr < nearestDistanceSqr)
                {
                    nearestDistanceSqr = distanceSqr;
                    nearestSocket = socket;
                }
            }

            UpdateDiagnostics(nearestSocket, zoneSocketCount);
        }

        private bool NeedsAutoResolve()
        {
            return playerTransform == null || worldZoneDirector == null;
        }

        private void ResolveReferences(bool force = false)
        {
            if (!force && !NeedsAutoResolve())
                return;

            float now = Time.realtimeSinceStartup;
            if (!force && now < _nextAutoResolveAttemptTime)
                return;

            _nextAutoResolveAttemptTime = now + Mathf.Max(0f, autoResolveRetryInterval);

            WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);
            WorldRuntimeReferenceUtility.TryResolveWorldZoneDirector(ref worldZoneDirector);
        }

        private void UpdateDiagnostics(WorldContentSocket nearestSocket, int zoneSocketCount)
        {
            _debugSocketCount = _sockets.Count;
            _debugZoneSocketCount = zoneSocketCount;
            _debugNearestSocket = nearestSocket != null ? nearestSocket.SocketLabel : "None";
            _debugNearestKind = nearestSocket != null ? nearestSocket.Kind.ToString() : WorldContentSocket.ContentKind.Generic.ToString();
            _debugNearestProfile = nearestSocket != null && nearestSocket.Profile != null ? nearestSocket.Profile.profileLabel : "None";
            _debugNearestPopulationFamily = nearestSocket != null ? nearestSocket.ResolvedPopulationFamily : "None";
            _debugNearestPopulationRule = nearestSocket != null ? nearestSocket.ResolvedPopulationRule : "None";
            _debugNearestPopulationBiomeFit = nearestSocket != null ? nearestSocket.ResolvedPopulationBiomeFit : "None";
            _debugNearestPopulationExtraction = nearestSocket != null ? nearestSocket.ResolvedPopulationExtraction : "None";
            _debugNearestPopulationLandmark = nearestSocket != null ? nearestSocket.ResolvedPopulationLandmark : "None";
            _debugNearestPopulationSpatialRole = nearestSocket != null ? nearestSocket.ResolvedPopulationSpatialRole : "None";
            _debugNearestPopulationSpatialReason = nearestSocket != null ? nearestSocket.ResolvedPopulationSpatialReason : "None";
            _debugNearestPopulationBorderRole = nearestSocket != null ? nearestSocket.ResolvedPopulationBorderRole : "None";
            _debugNearestPopulationBorderReason = nearestSocket != null ? nearestSocket.ResolvedPopulationBorderReason : "None";
            _debugNearestPopulationResourceItem = nearestSocket != null ? nearestSocket.ResolvedPopulationResourceItem : "None";
            _debugNearestPopulationResourceReason = nearestSocket != null ? nearestSocket.ResolvedPopulationResourceReason : "None";
            _debugNearestPopulationMotivationPull = nearestSocket != null ? nearestSocket.ResolvedPopulationMotivationPull : "None";
            _debugNearestPopulationMotivationReason = nearestSocket != null ? nearestSocket.ResolvedPopulationMotivationReason : "None";
            _debugNearestPopulationSandboxAttractionRole = nearestSocket != null ? nearestSocket.ResolvedPopulationSandboxAttractionRole : "None";
            _debugNearestPopulationSandboxAttractionReason = nearestSocket != null ? nearestSocket.ResolvedPopulationSandboxAttractionReason : "None";
            _debugNearestZoneRoleFamily = nearestSocket != null ? nearestSocket.ResolvedZoneRoleFamily : "None";
            _debugNearestZoneRoleLayout = nearestSocket != null ? nearestSocket.ResolvedZoneRoleLayout : "None";
            _debugNearestZoneRolePriority = nearestSocket != null ? nearestSocket.ResolvedZoneRolePriority : "None";
            _debugNearestPopulationPurpose = nearestSocket != null ? nearestSocket.ResolvedPopulationPurpose : "None";
            _debugNearestPopulationDensity = nearestSocket != null ? nearestSocket.GetResolvedPopulationDensityWeight() : 0f;
            _debugNearestProceduralRule = nearestSocket != null ? nearestSocket.ResolvedProceduralRule : "None";
            _debugNearestProceduralFamily = nearestSocket != null ? nearestSocket.ResolvedProceduralFamily : "None";
            _debugNearestProceduralVariant = nearestSocket != null ? nearestSocket.ResolvedProceduralVariant : "None";
            _debugNearestProceduralSource = nearestSocket != null ? nearestSocket.ResolvedProceduralSource : "None";
            _debugNearestProceduralDomain = nearestSocket != null ? nearestSocket.ResolvedProceduralDomain : WorldPrefabFamilyProfile.ProceduralDomain.Generic.ToString();
            _debugNearestProceduralPlacementMode = nearestSocket != null ? nearestSocket.ResolvedProceduralPlacementMode : WorldPrefabFamilyProfile.PlacementMode.Scatter.ToString();
            _debugNearestProceduralHeatmap = nearestSocket != null ? nearestSocket.ResolvedProceduralHeatmap : "None";
            _debugNearestProceduralIntent = nearestSocket != null ? nearestSocket.ResolvedProceduralIntent : "None";
            _debugNearestProceduralReason = nearestSocket != null ? nearestSocket.ResolvedProceduralReason : "None";
            _debugNearestProceduralScore = nearestSocket != null ? nearestSocket.GetResolvedProceduralScore() : 0f;
            _debugCurrentZone = ResolveZoneLabel(worldZoneDirector != null ? worldZoneDirector.CurrentZone : null);
        }

        private static string ResolveZoneId(WorldZoneAnchor zone)
        {
            return zone != null && !string.IsNullOrWhiteSpace(zone.ZoneId)
                ? zone.ZoneId
                : UnresolvedZoneId;
        }

        private static string ResolveZoneLabel(WorldZoneAnchor zone)
        {
            return zone != null && !string.IsNullOrWhiteSpace(zone.ZoneLabel)
                ? zone.ZoneLabel
                : UnresolvedZoneLabel;
        }
    }
}
