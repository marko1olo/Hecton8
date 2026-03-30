using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    public sealed class WorldContentSocket : MonoBehaviour
    {
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
        [SerializeField] private string _debugZoneRoleFamily = "None";
        [SerializeField] private string _debugZoneRoleLayout = "None";
        [SerializeField] private string _debugZoneRolePriority = "None";
        [SerializeField] private WorldSliceAnchor.SliceState _debugPopulationFidelity = WorldSliceAnchor.SliceState.Near;
        [SerializeField] private int _debugPopulationClusterCount;
        [SerializeField] private int _debugPopulationMinCount;
        [SerializeField] private int _debugPopulationMaxCount;
        [SerializeField] private float _debugPopulationDensityWeight;

        public string SocketId => socketId;
        public string SocketLabel => socketLabel;
        public ContentKind Kind => contentKind;
        public WorldContentProfile Profile => contentProfile;
        public WorldSliceAnchor.SliceState PreferredFidelity => preferredFidelity;
        public float InteractionRadius => interactionRadius;
        public int Weight => weight;
        public string FuturePrefabKey => futurePrefabKey;
        public string ContentIntent => contentIntent;

        public float GetFlatDistance(Vector3 position)
        {
            Vector3 delta = transform.position - position;
            delta.y = 0f;
            _debugLastDistance = delta.magnitude;
            return _debugLastDistance;
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
        public string ResolvedZoneRoleFamily => _debugZoneRoleFamily;
        public string ResolvedZoneRoleLayout => _debugZoneRoleLayout;
        public string ResolvedZoneRolePriority => _debugZoneRolePriority;
        public WorldSliceAnchor.SliceState ResolvedPopulationFidelity => _debugPopulationFidelity;
        public float GetResolvedPopulationDensityWeight() => _debugPopulationDensityWeight;

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
            _debugZoneRoleFamily = "None";
            _debugZoneRoleLayout = "None";
            _debugZoneRolePriority = "None";
            _debugPopulationFidelity = ResolvePopulationFidelity();
            _debugPopulationClusterCount = 0;
            _debugPopulationMinCount = 0;
            _debugPopulationMaxCount = 0;
            _debugPopulationDensityWeight = 0f;
        }

        private WorldSliceAnchor.SliceState ResolvePopulationFidelity()
        {
            if (contentProfile != null)
                return contentProfile.preferredFidelity;

            return preferredFidelity;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(socketId))
                socketId = "socket.generic";

            if (string.IsNullOrWhiteSpace(socketLabel))
                socketLabel = gameObject.name;

            interactionRadius = Mathf.Max(1f, interactionRadius);
            weight = Mathf.Clamp(weight, 1, 20);
            _debugPopulationFidelity = ResolvePopulationFidelity();
        }
#endif
    }
}
