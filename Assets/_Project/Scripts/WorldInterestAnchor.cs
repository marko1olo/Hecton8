using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    public sealed class WorldInterestAnchor : MonoBehaviour
    {
        private static readonly List<WorldInterestAnchor> _ActiveAnchors = new List<WorldInterestAnchor>(24);

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

        public static void CopyActiveAnchorsTo(List<WorldInterestAnchor> destination)
        {
            if (destination == null)
                return;

            destination.Clear();
            for (int i = 0; i < _ActiveAnchors.Count; i++)
            {
                WorldInterestAnchor anchor = _ActiveAnchors[i];
                if (anchor == null)
                    continue;

                GameObject go = anchor.gameObject;
                if (go == null || !go.scene.IsValid())
                    continue;

                destination.Add(anchor);
            }
        }

        public float EvaluateInfluence(Vector3 playerPosition)
        {
            AbsoluteUniversePosition playerAup = AbsoluteUniversePosition.FromRuntimePosition(playerPosition);
            return EvaluateInfluence(in playerAup);
        }

        public float EvaluateInfluence(in AbsoluteUniversePosition playerAup)
        {
            AbsoluteUniversePosition anchorAup = AbsoluteUniversePosition.FromRuntimePosition(transform.position);
            double distanceSq = PlanarDistanceSq(in anchorAup, in playerAup);
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

            float distance = math.sqrt((float)distanceSq);
            float t = math.saturate((falloff - distance) / math.max(0.0001f, falloff - fullRadius));
            _debugLastInfluence = t;
            return t;
        }

        private static double PlanarDistanceSq(in AbsoluteUniversePosition a, in AbsoluteUniversePosition b)
        {
            const double cellSize = AbsoluteUniversePosition.CellSizeMeters;
            double deltaX = ((a.GridX - b.GridX) * cellSize) + (a.LocalX - b.LocalX);
            double deltaZ = ((a.GridZ - b.GridZ) * cellSize) + (a.LocalZ - b.LocalZ);
            return (deltaX * deltaX) + (deltaZ * deltaZ);
        }

        private static void RegisterActiveAnchor(WorldInterestAnchor anchor)
        {
            if (anchor == null || _ActiveAnchors.Contains(anchor))
                return;

            _ActiveAnchors.Add(anchor);
        }

        private static void UnregisterActiveAnchor(WorldInterestAnchor anchor)
        {
            if (anchor == null)
                return;

            _ActiveAnchors.Remove(anchor);
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
