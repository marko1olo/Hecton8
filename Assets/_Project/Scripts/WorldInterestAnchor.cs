using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    public sealed class WorldInterestAnchor : MonoBehaviour
    {
        public enum InterestKind
        {
            ResourceField,
            Fabrication,
            ToolRange,
            Construction,
            Power,
            Service,
            ProgressionHub
        }

        [Header("Identity")]
        [SerializeField] private InterestKind interestKind = InterestKind.ProgressionHub;

        [Header("Influence")]
        [SerializeField] private float fullInfluenceRadius = 70f;
        [SerializeField] private float falloffRadius = 180f;

        [Header("Budget Lift")]
        [SerializeField] private float scavengeRadiusScale = 1.1f;
        [SerializeField] private float spawnScale = 1.08f;
        [SerializeField] private float colliderRadiusScale = 1.08f;
        [SerializeField] private float colliderOpsScale = 1.08f;

        [Header("Diagnostics")]
        [SerializeField] private float _debugLastInfluence;

        public InterestKind Kind => interestKind;
        public float FullInfluenceRadius => fullInfluenceRadius;
        public float FalloffRadius => falloffRadius;
        public float ScavengeRadiusScale => scavengeRadiusScale;
        public float SpawnScale => spawnScale;
        public float ColliderRadiusScale => colliderRadiusScale;
        public float ColliderOpsScale => colliderOpsScale;

        public float EvaluateInfluence(Vector3 playerPosition)
        {
            Vector3 delta = transform.position - playerPosition;
            delta.y = 0f;
            float distance = delta.magnitude;

            if (distance <= fullInfluenceRadius)
            {
                _debugLastInfluence = 1f;
                return 1f;
            }

            if (distance >= falloffRadius)
            {
                _debugLastInfluence = 0f;
                return 0f;
            }

            float t = Mathf.InverseLerp(falloffRadius, fullInfluenceRadius, distance);
            _debugLastInfluence = t;
            return t;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            fullInfluenceRadius = Mathf.Max(20f, fullInfluenceRadius);
            falloffRadius = Mathf.Max(fullInfluenceRadius + 20f, falloffRadius);
            scavengeRadiusScale = Mathf.Clamp(scavengeRadiusScale, 0.85f, 1.4f);
            spawnScale = Mathf.Clamp(spawnScale, 0.85f, 1.4f);
            colliderRadiusScale = Mathf.Clamp(colliderRadiusScale, 0.85f, 1.4f);
            colliderOpsScale = Mathf.Clamp(colliderOpsScale, 0.85f, 1.4f);
        }
#endif
    }
}
