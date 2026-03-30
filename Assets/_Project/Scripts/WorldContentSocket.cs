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
        public WorldSliceAnchor.SliceState ResolvedPopulationFidelity => _debugPopulationFidelity;

        public void ApplyPopulationRecommendation(WorldPopulationRule rule)
        {
            if (rule == null)
            {
                ClearPopulationRecommendation();
                return;
            }

            _debugPopulationRule = string.IsNullOrWhiteSpace(rule.ruleLabel) ? "Unnamed Rule" : rule.ruleLabel;
            _debugPopulationFamily = string.IsNullOrWhiteSpace(rule.prefabFamily) ? "None" : rule.prefabFamily;
            _debugPopulationPurpose = string.IsNullOrWhiteSpace(rule.gameplayPurpose) ? "None" : rule.gameplayPurpose;
            _debugPopulationFidelity = ResolvePopulationFidelity();
            _debugPopulationClusterCount = Mathf.Max(0, rule.suggestedClusterCount);
            _debugPopulationMinCount = Mathf.Max(0, rule.suggestedMinCount);
            _debugPopulationMaxCount = Mathf.Max(_debugPopulationMinCount, rule.suggestedMaxCount);
            _debugPopulationDensityWeight = Mathf.Max(0f, rule.densityWeight);
        }

        public void ClearPopulationRecommendation()
        {
            _debugPopulationRule = "None";
            _debugPopulationFamily = "None";
            _debugPopulationPurpose = "None";
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
