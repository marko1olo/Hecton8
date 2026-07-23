using System.Collections.Generic;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    public sealed class WorldContentSocket : MonoBehaviour
    {
        private static readonly List<WorldContentSocket> _ActiveSockets = new List<WorldContentSocket>(128);

        public enum ContentKind
        {
            Generic,
            ResourcePickup,
            ResourceNode,
            FabricationStation,
            ConstructionPoint,
            PowerPoint,
            ServiceTarget,
            NavigationMarker,
            HazardPoint,
            CombatPoint,
            Landmark
        }

        [Header("Identity")]
        [SerializeField] private string socketId = "socket.generic";
        [SerializeField] private string socketLabel = "Generic Socket";
        [SerializeField] private ContentKind contentKind = ContentKind.Generic;
        [SerializeField] private WorldContentProfile contentProfile;

        [Header("Placement")]
        [SerializeField] private WorldSliceAnchor.SliceState preferredFidelity = WorldSliceAnchor.SliceState.Near;
        [SerializeField] private float interactionRadius = 6f;
        [SerializeField] private int weight = 1;

        [Header("Future Spawn")]
        [SerializeField] private string futurePrefabKey = string.Empty;
        [SerializeField] [TextArea(2, 4)] private string contentIntent = "Generic content socket.";

        [Header("Diagnostics")]
        [SerializeField] private float _debugLastDistance;
        [SerializeField] private string _debugPopulationRule = "None";
        [SerializeField] private string _debugPopulationFamily = "None";
        [SerializeField] private string _debugPopulationPurpose = "None";
        [SerializeField] private string _debugPopulationBiomeFit = "None";
        [SerializeField] private string _debugPopulationExtraction = "None";
        [SerializeField] private string _debugPopulationLandmark = "None";
        [SerializeField] private string _debugPopulationSpatialRole = "None";
        [SerializeField] private string _debugPopulationSpatialReason = "None";
        [SerializeField] private string _debugPopulationBorderRole = "None";
        [SerializeField] private string _debugPopulationBorderReason = "None";
        [SerializeField] private string _debugPopulationResourceItem = "None";
        [SerializeField] private string _debugPopulationResourceReason = "None";
        [SerializeField] private string _debugPopulationMotivationPull = "None";
        [SerializeField] private string _debugPopulationMotivationReason = "None";
        [SerializeField] private string _debugPopulationSandboxAttractionRole = "None";
        [SerializeField] private string _debugPopulationSandboxAttractionReason = "None";
        [SerializeField] private string _debugZoneRoleFamily = "None";
        [SerializeField] private string _debugZoneRoleLayout = "None";
        [SerializeField] private string _debugZoneRolePriority = "None";
        [SerializeField] private string _debugProceduralRule = "None";
        [SerializeField] private string _debugProceduralFamily = "None";
        [SerializeField] private string _debugProceduralVariant = "None";
        [SerializeField] private string _debugProceduralSource = "None";
        [SerializeField] private string _debugProceduralDomain = "Generic";
        [SerializeField] private string _debugProceduralPlacementMode = "Scatter";
        [SerializeField] private string _debugProceduralHeatmap = "None";
        [SerializeField] private string _debugProceduralIntent = "None";
        [SerializeField] private string _debugProceduralReason = "None";
        [SerializeField] private WorldSliceAnchor.SliceState _debugPopulationFidelity = WorldSliceAnchor.SliceState.Near;
        [SerializeField] private int _debugPopulationClusterCount;
        [SerializeField] private int _debugPopulationMinCount;
        [SerializeField] private int _debugPopulationMaxCount;
        [SerializeField] private float _debugPopulationDensityWeight;
        [SerializeField] private int _debugProceduralMinCount;
        [SerializeField] private int _debugProceduralMaxCount;
        [SerializeField] private float _debugProceduralScore;
        [SerializeField] private float _debugProceduralMinSpacingMeters;
        [SerializeField] private float _debugProceduralClusterRadiusMeters;

        private WorldZoneAnchor _cachedZoneAnchor;
        private bool _hasCachedZoneAnchor;

        public string SocketId => socketId;
        public string SocketLabel => socketLabel;
        public ContentKind Kind => contentKind;
        public WorldContentProfile Profile => contentProfile;
        public WorldSliceAnchor.SliceState PreferredFidelity => preferredFidelity;
        public float InteractionRadius => interactionRadius;
        public int Weight => weight;
        public string FuturePrefabKey => futurePrefabKey;
        public string ContentIntent => contentIntent;

        private void OnEnable()
        {
            RefreshZoneAnchorCold();
            RegisterActiveSocket(this);
        }

        private void OnDisable()
        {
            UnregisterActiveSocket(this);
        }

        private void OnDestroy()
        {
            UnregisterActiveSocket(this);
        }

        public static void CopyActiveSocketsTo(List<WorldContentSocket> destination)
        {
            if (destination == null)
                return;

            destination.Clear();
            for (int i = 0; i < _ActiveSockets.Count; i++)
            {
                WorldContentSocket socket = _ActiveSockets[i];
                if (socket == null)
                    continue;

                GameObject go = socket.gameObject;
                if (go == null || !go.scene.IsValid())
                    continue;

                if (WorldShippingContentFilter.IsSuppressedSocket(socket))
                    continue;

                destination.Add(socket);
            }
        }

        public WorldZoneAnchor GetZoneAnchor()
        {
            if (!_hasCachedZoneAnchor)
                RefreshZoneAnchorCold();
            return _cachedZoneAnchor;
        }

        public void RefreshZoneAnchorCold()
        {
            if (!TryGetComponent(out _cachedZoneAnchor))
                TryResolveComponentInParents(transform.parent, out _cachedZoneAnchor);
            _hasCachedZoneAnchor = true;
        }

        private static bool TryResolveComponentInParents<T>(Transform current, out T component) where T : Component
        {
            if (current == null)
            {
                component = null;
                return false;
            }

            component = current.GetComponentInParent<T>(true);
            return component != null;
        }

        public float GetFlatDistance(Vector3 position)
        {
            Vector3 delta = transform.position - position;
            delta.y = 0f;
            _debugLastDistance = delta.magnitude;
            return _debugLastDistance;
        }

        public float GetFlatDistanceSquared(Vector3 position)
        {
            Vector3 delta = transform.position - position;
            delta.y = 0f;
            return delta.sqrMagnitude;
        }

        public string ResolvedPopulationRule => _debugPopulationRule;
        public string ResolvedPopulationFamily => _debugPopulationFamily;
        public string ResolvedPopulationPurpose => _debugPopulationPurpose;
        public string ResolvedPopulationBiomeFit => _debugPopulationBiomeFit;
        public string ResolvedPopulationExtraction => _debugPopulationExtraction;
        public string ResolvedPopulationLandmark => _debugPopulationLandmark;
        public string ResolvedPopulationSpatialRole => _debugPopulationSpatialRole;
        public string ResolvedPopulationSpatialReason => _debugPopulationSpatialReason;
        public string ResolvedPopulationBorderRole => _debugPopulationBorderRole;
        public string ResolvedPopulationBorderReason => _debugPopulationBorderReason;
        public string ResolvedPopulationResourceItem => _debugPopulationResourceItem;
        public string ResolvedPopulationResourceReason => _debugPopulationResourceReason;
        public string ResolvedPopulationMotivationPull => _debugPopulationMotivationPull;
        public string ResolvedPopulationMotivationReason => _debugPopulationMotivationReason;
        public string ResolvedPopulationSandboxAttractionRole => _debugPopulationSandboxAttractionRole;
        public string ResolvedPopulationSandboxAttractionReason => _debugPopulationSandboxAttractionReason;
        public string ResolvedZoneRoleFamily => _debugZoneRoleFamily;
        public string ResolvedZoneRoleLayout => _debugZoneRoleLayout;
        public string ResolvedZoneRolePriority => _debugZoneRolePriority;
        public string ResolvedProceduralRule => _debugProceduralRule;
        public string ResolvedProceduralFamily => _debugProceduralFamily;
        public string ResolvedProceduralVariant => _debugProceduralVariant;
        public string ResolvedProceduralSource => _debugProceduralSource;
        public string ResolvedProceduralDomain => _debugProceduralDomain;
        public string ResolvedProceduralPlacementMode => _debugProceduralPlacementMode;
        public string ResolvedProceduralHeatmap => _debugProceduralHeatmap;
        public string ResolvedProceduralIntent => _debugProceduralIntent;
        public string ResolvedProceduralReason => _debugProceduralReason;
        public WorldSliceAnchor.SliceState ResolvedPopulationFidelity => _debugPopulationFidelity;
        public float GetResolvedPopulationDensityWeight() => _debugPopulationDensityWeight;
        public float GetResolvedProceduralScore() => _debugProceduralScore;

        public void ApplyPopulationRecommendation(
            WorldPopulationRule rule,
            float effectiveDensityWeight,
            string biomeFit,
            string extractionFocus,
            string landmarkGuidance,
            string resolvedPurpose,
            string spatialRole,
            string spatialReason,
            string borderRole,
            string borderReason,
            string resourceItem,
            string resourceReason,
            string motivationPull,
            string motivationReason,
            string sandboxAttractionRole,
            string sandboxAttractionReason,
            string zoneRoleFamily,
            string zoneRoleLayout,
            string zoneRolePriority)
        {
            if (rule == null)
            {
                ClearPopulationRecommendation();
                return;
            }

            _debugPopulationRule = string.IsNullOrWhiteSpace(rule.ruleLabel) ? "Unnamed Rule" : rule.ruleLabel;
            _debugPopulationFamily = string.IsNullOrWhiteSpace(rule.prefabFamily) ? "None" : rule.prefabFamily;
            _debugPopulationPurpose = string.IsNullOrWhiteSpace(resolvedPurpose) ? "None" : resolvedPurpose;
            _debugPopulationBiomeFit = string.IsNullOrWhiteSpace(biomeFit) ? "None" : biomeFit;
            _debugPopulationExtraction = string.IsNullOrWhiteSpace(extractionFocus) ? "None" : extractionFocus;
            _debugPopulationLandmark = string.IsNullOrWhiteSpace(landmarkGuidance) ? "None" : landmarkGuidance;
            _debugPopulationSpatialRole = string.IsNullOrWhiteSpace(spatialRole) ? "None" : spatialRole;
            _debugPopulationSpatialReason = string.IsNullOrWhiteSpace(spatialReason) ? "None" : spatialReason;
            _debugPopulationBorderRole = string.IsNullOrWhiteSpace(borderRole) ? "None" : borderRole;
            _debugPopulationBorderReason = string.IsNullOrWhiteSpace(borderReason) ? "None" : borderReason;
            _debugPopulationResourceItem = string.IsNullOrWhiteSpace(resourceItem) ? "None" : resourceItem;
            _debugPopulationResourceReason = string.IsNullOrWhiteSpace(resourceReason) ? "None" : resourceReason;
            _debugPopulationMotivationPull = string.IsNullOrWhiteSpace(motivationPull) ? "None" : motivationPull;
            _debugPopulationMotivationReason = string.IsNullOrWhiteSpace(motivationReason) ? "None" : motivationReason;
            _debugPopulationSandboxAttractionRole = string.IsNullOrWhiteSpace(sandboxAttractionRole) ? "None" : sandboxAttractionRole;
            _debugPopulationSandboxAttractionReason = string.IsNullOrWhiteSpace(sandboxAttractionReason) ? "None" : sandboxAttractionReason;
            _debugZoneRoleFamily = string.IsNullOrWhiteSpace(zoneRoleFamily) ? "None" : zoneRoleFamily;
            _debugZoneRoleLayout = string.IsNullOrWhiteSpace(zoneRoleLayout) ? "None" : zoneRoleLayout;
            _debugZoneRolePriority = string.IsNullOrWhiteSpace(zoneRolePriority) ? "None" : zoneRolePriority;
            _debugPopulationFidelity = ResolvePopulationFidelity();
            _debugPopulationClusterCount = Mathf.Max(0, rule.suggestedClusterCount);
            _debugPopulationMinCount = Mathf.Max(0, rule.suggestedMinCount);
            _debugPopulationMaxCount = Mathf.Max(_debugPopulationMinCount, rule.suggestedMaxCount);
            _debugPopulationDensityWeight = Mathf.Max(0f, effectiveDensityWeight);
        }

        public void ClearPopulationRecommendation()
        {
            _debugPopulationRule = "None";
            _debugPopulationFamily = "None";
            _debugPopulationPurpose = "None";
            _debugPopulationBiomeFit = "None";
            _debugPopulationExtraction = "None";
            _debugPopulationLandmark = "None";
            _debugPopulationSpatialRole = "None";
            _debugPopulationSpatialReason = "None";
            _debugPopulationBorderRole = "None";
            _debugPopulationBorderReason = "None";
            _debugPopulationResourceItem = "None";
            _debugPopulationResourceReason = "None";
            _debugPopulationMotivationPull = "None";
            _debugPopulationMotivationReason = "None";
            _debugPopulationSandboxAttractionRole = "None";
            _debugPopulationSandboxAttractionReason = "None";
            _debugZoneRoleFamily = "None";
            _debugZoneRoleLayout = "None";
            _debugZoneRolePriority = "None";
            _debugPopulationFidelity = ResolvePopulationFidelity();
            _debugPopulationClusterCount = 0;
            _debugPopulationMinCount = 0;
            _debugPopulationMaxCount = 0;
            _debugPopulationDensityWeight = 0f;
        }

        public void ApplyProceduralRecommendation(
            WorldProceduralPlacementRule rule,
            WorldPrefabFamilyProfile family,
            string variantId,
            string source,
            string reason,
            string intent,
            string heatmapChannel,
            int minCount,
            int maxCount,
            float minSpacingMeters,
            float clusterRadiusMeters,
            float score)
        {
            _debugProceduralRule = rule != null && !string.IsNullOrWhiteSpace(rule.ruleLabel) ? rule.ruleLabel : "None";
            _debugProceduralFamily = family != null && !string.IsNullOrWhiteSpace(family.familyLabel) ? family.familyLabel : "None";
            _debugProceduralVariant = string.IsNullOrWhiteSpace(variantId) ? "None" : variantId;
            _debugProceduralSource = string.IsNullOrWhiteSpace(source) ? "None" : source;
            _debugProceduralDomain = family != null ? ResolveProceduralDomainLabel(family.proceduralDomain) : "Generic";
            _debugProceduralPlacementMode = family != null ? ResolvePlacementModeLabel(family.placementMode) : "Scatter";
            _debugProceduralHeatmap = string.IsNullOrWhiteSpace(heatmapChannel) ? "None" : heatmapChannel;
            _debugProceduralIntent = string.IsNullOrWhiteSpace(intent) ? "None" : intent;
            _debugProceduralReason = string.IsNullOrWhiteSpace(reason) ? "None" : reason;
            _debugProceduralMinCount = Mathf.Max(0, minCount);
            _debugProceduralMaxCount = Mathf.Max(_debugProceduralMinCount, maxCount);
            _debugProceduralScore = Mathf.Max(0f, score);
            _debugProceduralMinSpacingMeters = Mathf.Max(0f, minSpacingMeters);
            _debugProceduralClusterRadiusMeters = Mathf.Max(0f, clusterRadiusMeters);
        }

        public void ClearProceduralRecommendation()
        {
            _debugProceduralRule = "None";
            _debugProceduralFamily = "None";
            _debugProceduralVariant = "None";
            _debugProceduralSource = "None";
            _debugProceduralDomain = "Generic";
            _debugProceduralPlacementMode = "Scatter";
            _debugProceduralHeatmap = "None";
            _debugProceduralIntent = "None";
            _debugProceduralReason = "None";
            _debugProceduralMinCount = 0;
            _debugProceduralMaxCount = 0;
            _debugProceduralScore = 0f;
            _debugProceduralMinSpacingMeters = 0f;
            _debugProceduralClusterRadiusMeters = 0f;
        }

        private void OnTransformParentChanged()
        {
            _cachedZoneAnchor = null;
            _hasCachedZoneAnchor = false;
        }

        private WorldSliceAnchor.SliceState ResolvePopulationFidelity()
        {
            if (contentProfile != null)
                return contentProfile.preferredFidelity;

            return preferredFidelity;
        }

        private static string ResolveProceduralDomainLabel(WorldPrefabFamilyProfile.ProceduralDomain domain)
        {
            switch (domain)
            {
                case WorldPrefabFamilyProfile.ProceduralDomain.Rock:
                    return "Rock";
                case WorldPrefabFamilyProfile.ProceduralDomain.RockCluster:
                    return "RockCluster";
                case WorldPrefabFamilyProfile.ProceduralDomain.RockArch:
                    return "RockArch";
                case WorldPrefabFamilyProfile.ProceduralDomain.RockShelf:
                    return "RockShelf";
                case WorldPrefabFamilyProfile.ProceduralDomain.Kelp:
                    return "Kelp";
                case WorldPrefabFamilyProfile.ProceduralDomain.Plant:
                    return "Plant";
                case WorldPrefabFamilyProfile.ProceduralDomain.Coral:
                    return "Coral";
                case WorldPrefabFamilyProfile.ProceduralDomain.Egg:
                    return "Egg";
                case WorldPrefabFamilyProfile.ProceduralDomain.Debris:
                    return "Debris";
                case WorldPrefabFamilyProfile.ProceduralDomain.RuinModule:
                    return "RuinModule";
                case WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance:
                    return "CaveEntrance";
                case WorldPrefabFamilyProfile.ProceduralDomain.Landmark:
                    return "Landmark";
                case WorldPrefabFamilyProfile.ProceduralDomain.CreatureSpawn:
                    return "CreatureSpawn";
                case WorldPrefabFamilyProfile.ProceduralDomain.ResourcePocket:
                    return "ResourcePocket";
                case WorldPrefabFamilyProfile.ProceduralDomain.HazardPocket:
                    return "HazardPocket";
                case WorldPrefabFamilyProfile.ProceduralDomain.SafePocket:
                    return "SafePocket";
                case WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute:
                    return "PowerRoute";
                case WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar:
                    return "ServiceScar";
                default:
                    return "Generic";
            }
        }

        private static string ResolvePlacementModeLabel(WorldPrefabFamilyProfile.PlacementMode placementMode)
        {
            switch (placementMode)
            {
                case WorldPrefabFamilyProfile.PlacementMode.Cluster:
                    return "Cluster";
                case WorldPrefabFamilyProfile.PlacementMode.Patch:
                    return "Patch";
                case WorldPrefabFamilyProfile.PlacementMode.Solitary:
                    return "Solitary";
                case WorldPrefabFamilyProfile.PlacementMode.Landmark:
                    return "Landmark";
                case WorldPrefabFamilyProfile.PlacementMode.SpawnAnchor:
                    return "SpawnAnchor";
                case WorldPrefabFamilyProfile.PlacementMode.SocketDriven:
                    return "SocketDriven";
                default:
                    return "Scatter";
            }
        }

        private static void RegisterActiveSocket(WorldContentSocket socket)
        {
            if (socket == null || _ActiveSockets.Contains(socket))
                return;

            _ActiveSockets.Add(socket);
        }

        private static void UnregisterActiveSocket(WorldContentSocket socket)
        {
            if (socket == null)
                return;

            _ActiveSockets.Remove(socket);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (string.IsNullOrWhiteSpace(socketId))
                socketId = "socket.generic";

            if (string.IsNullOrWhiteSpace(socketLabel))
                socketLabel = "Generic Socket";

            interactionRadius = Mathf.Max(1f, interactionRadius);
            weight = Mathf.Clamp(weight, 1, 20);
            _debugPopulationFidelity = ResolvePopulationFidelity();
        }
#endif
    }
}
