using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    public sealed class WorldInterestAnchor : MonoBehaviour
    {
        private const int ActiveAnchorCopyBudget = 32;
        private static readonly WorldInterestAnchor[] _ActiveAnchors = new WorldInterestAnchor[ActiveAnchorCopyBudget];
        private static int _ActiveAnchorCount;
        private static int _ActiveAnchorVersion;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            System.Array.Clear(_ActiveAnchors, 0, _ActiveAnchors.Length);
            _ActiveAnchorCount = 0;
            _ActiveAnchorVersion = 0;
        }

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
        [SerializeField, Tooltip("Stable debug/readability label used by runtime systems instead of hierarchy Object.name.")]
        private string interestLabel = "Progression Hub";
        [SerializeField] private InterestKind interestKind = InterestKind.ProgressionHub;

        [Header("Influence")]
        [SerializeField] private float fullInfluenceRadius = 70f;
        [SerializeField] private float falloffRadius = 180f;

        [Header("Budget Lift")]
        [SerializeField] private float scavengeRadiusScale = 1.1f;
        [SerializeField] private float spawnScale = 1.08f;
        [SerializeField] private float colliderRadiusScale = 1.08f;
        [SerializeField] private float colliderOpsScale = 1.08f;

        [Header("Slice Lift")]
        [SerializeField] private float sliceNearScale = 1.05f;
        [SerializeField] private float sliceMidScale = 1.1f;

        [Header("Diagnostics")]
        [SerializeField] private float _debugLastInfluence;

        public InterestKind Kind => interestKind;
        public string InterestLabel => string.IsNullOrWhiteSpace(interestLabel)
            ? BuildDefaultInterestLabel(interestKind)
            : interestLabel;
        public float FullInfluenceRadius => fullInfluenceRadius;
        public float FalloffRadius => falloffRadius;
        public float ScavengeRadiusScale => scavengeRadiusScale;
        public float SpawnScale => spawnScale;
        public float ColliderRadiusScale => colliderRadiusScale;
        public float ColliderOpsScale => colliderOpsScale;
        public float SliceNearScale => sliceNearScale;
        public float SliceMidScale => sliceMidScale;

        private void OnEnable()
        {
            RegisterActiveAnchor(this);
        }

        private void OnDisable()
        {
            UnregisterActiveAnchor(this);
        }

        private void OnDestroy()
        {
            UnregisterActiveAnchor(this);
        }

        public static int CopyActiveAnchorsTo(WorldInterestAnchor[] destination)
        {
            if (destination == null || destination.Length <= 0)
                return 0;

            int writeCount = 0;
            int safeCount = math.min(_ActiveAnchorCount, ActiveAnchorCopyBudget);
            for (int i = 0; i < safeCount && writeCount < destination.Length; i++)
            {
                WorldInterestAnchor anchor = _ActiveAnchors[i];
                if (anchor == null)
                    continue;

                GameObject go = anchor.gameObject;
                if (go == null || !go.scene.IsValid())
                    continue;

                destination[writeCount] = anchor;
                writeCount++;
            }

            for (int i = writeCount; i < destination.Length; i++)
                destination[i] = null;

            return writeCount;
        }

        public static int ActiveAnchorVersion => _ActiveAnchorVersion;

        public float EvaluateInfluence(Vector3 playerPosition)
        {
            Vector3 anchorPosition = transform.position;
            double deltaX = (double)anchorPosition.x - playerPosition.x;
            double deltaZ = (double)anchorPosition.z - playerPosition.z;
            return EvaluateInfluenceFromDistanceSq((deltaX * deltaX) + (deltaZ * deltaZ));
        }

        public float EvaluateInfluence(in AbsoluteUniversePosition playerAup)
        {
            double distanceSq = PlanarDistanceSqToRuntimeAnchor(transform.position, in playerAup);
            return EvaluateInfluenceFromDistanceSq(distanceSq);
        }

        private float EvaluateInfluenceFromDistanceSq(double distanceSq)
        {
            if (double.IsNaN(distanceSq) || double.IsInfinity(distanceSq))
            {
                _debugLastInfluence = 0f;
                return 0f;
            }

            float fullRadius = math.max(0f, fullInfluenceRadius);
            float falloff = math.max(fullRadius + 0.0001f, falloffRadius);
            double fullRadiusSq = (double)fullRadius * fullRadius;
            double falloffRadiusSq = (double)falloff * falloff;

            if (distanceSq <= fullRadiusSq)
            {
                _debugLastInfluence = 1f;
                return 1f;
            }

            if (distanceSq >= falloffRadiusSq)
            {
                _debugLastInfluence = 0f;
                return 0f;
            }

            double falloffBandSq = math.max(0.0001f, falloffRadiusSq - fullRadiusSq);
            float t = math.saturate((float)((falloffRadiusSq - distanceSq) / falloffBandSq));
            _debugLastInfluence = t;
            return t;
        }

        private static double PlanarDistanceSqToRuntimeAnchor(Vector3 anchorPosition, in AbsoluteUniversePosition playerAup)
        {
            float3 playerRuntime = playerAup.ToRuntimeFloat3();
            double deltaX = (double)anchorPosition.x - playerRuntime.x;
            double deltaZ = (double)anchorPosition.z - playerRuntime.z;
            return (deltaX * deltaX) + (deltaZ * deltaZ);
        }

        private static void RegisterActiveAnchor(WorldInterestAnchor anchor)
        {
            if (anchor == null || FindActiveAnchorIndex(anchor) >= 0 || _ActiveAnchorCount >= _ActiveAnchors.Length)
                return;

            _ActiveAnchors[_ActiveAnchorCount] = anchor;
            _ActiveAnchorCount++;
            _ActiveAnchorVersion++;
        }

        private static void UnregisterActiveAnchor(WorldInterestAnchor anchor)
        {
            if (anchor == null)
                return;

            int index = FindActiveAnchorIndex(anchor);
            if (index < 0)
                return;

            int lastIndex = _ActiveAnchorCount - 1;
            if (index != lastIndex)
                _ActiveAnchors[index] = _ActiveAnchors[lastIndex];

            _ActiveAnchors[lastIndex] = null;
            _ActiveAnchorCount--;
            _ActiveAnchorVersion++;
        }

        private static int FindActiveAnchorIndex(WorldInterestAnchor anchor)
        {
            for (int i = 0; i < _ActiveAnchorCount; i++)
            {
                if (ReferenceEquals(_ActiveAnchors[i], anchor))
                    return i;
            }

            return -1;
        }

        private static string BuildDefaultInterestLabel(InterestKind kind)
        {
            return kind switch
            {
                InterestKind.ResourceField => "Resource Field",
                InterestKind.Fabrication => "Fabrication",
                InterestKind.ToolRange => "Tool Range",
                InterestKind.Construction => "Construction",
                InterestKind.Power => "Power",
                InterestKind.Service => "Service",
                _ => "Progression Hub"
            };
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            fullInfluenceRadius = Mathf.Max(20f, fullInfluenceRadius);
            falloffRadius = Mathf.Max(fullInfluenceRadius + 20f, falloffRadius);
            scavengeRadiusScale = Mathf.Clamp(scavengeRadiusScale, 0.85f, 1.4f);
            spawnScale = Mathf.Clamp(spawnScale, 0.85f, 1.4f);
            colliderRadiusScale = Mathf.Clamp(colliderRadiusScale, 0.85f, 1.4f);
            colliderOpsScale = Mathf.Clamp(colliderOpsScale, 0.85f, 1.4f);
            sliceNearScale = Mathf.Clamp(sliceNearScale, 0.85f, 1.35f);
            sliceMidScale = Mathf.Clamp(sliceMidScale, 0.9f, 1.45f);

            if (string.IsNullOrWhiteSpace(interestLabel))
                interestLabel = BuildDefaultInterestLabel(interestKind);
        }
#endif
    }
}
