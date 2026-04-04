using Hecton8.Environment;
using System.Collections.Generic;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    public sealed class WorldZoneAnchor : MonoBehaviour
    {
        private static readonly List<WorldZoneAnchor> _ActiveAnchors = new List<WorldZoneAnchor>(32);
        private static int _ActiveAnchorVersion;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _ActiveAnchors.Clear();
            _ActiveAnchorVersion = 0;
        }

        public enum ZoneKind
        {
            Generic,
            Resources,
            Fabrication,
            Trial,
            Construction,
            Power,
            Service,
            Progression,
            Combat,
            Navigation
        }

        public enum ZoneTier
        {
            Starter,
            Early,
            Mid,
            Late,
            Endgame
        }

        [Header("Identity")]
        [SerializeField] private string zoneId = "zone.generic";
        [SerializeField] private string zoneLabel = "Generic Zone";
        [SerializeField] private ZoneKind zoneKind = ZoneKind.Generic;
        [SerializeField] private ZoneTier zoneTier = ZoneTier.Starter;
        [SerializeField] private WorldZoneProfile zoneProfile;

        [Header("Biome Identity")]
        [SerializeField] private HectonBiomeMatrixProfile dominantMatrixBiome;
        [SerializeField] private HectonBiomeFamilyProfile dominantBiomeFamily;

        [Header("Presence")]
        [SerializeField] private float activationRadius = 90f;
        [SerializeField] private float holdRadius = 140f;
        [SerializeField] private float edgeBlendDistance = 22f;
        [SerializeField] private float edgeNoiseScale = 0.018f;
        [SerializeField] private float edgeNoiseStrength = 0.16f;
        [SerializeField] private Vector2 edgeNoiseOffset = new Vector2(13.7f, 41.3f);
        [SerializeField] private int priority = 0;

        [Header("Future Use")]
        [SerializeField] private string gameplayIntent = "General world space.";
        [SerializeField] private bool routeCritical;

        [Header("Diagnostics")]
        [SerializeField] private float _debugLastDistance;
        [SerializeField] private bool _debugInsideActivation;
        [SerializeField] private bool _debugInsideHold;
        [SerializeField] private float _debugActivationWeight;
        [SerializeField] private float _debugHoldWeight;

        public string ZoneId => zoneId;
        public string ZoneLabel => zoneLabel;
        public ZoneKind Kind => zoneKind;
        public ZoneTier Tier => zoneTier;
        public WorldZoneProfile Profile => zoneProfile;
        public HectonBiomeMatrixProfile DominantMatrixBiome => dominantMatrixBiome;
        public HectonBiomeFamilyProfile DominantBiomeFamily => dominantBiomeFamily;
        public int Priority => priority;
        public string GameplayIntent => gameplayIntent;
        public bool RouteCritical => routeCritical;
        public float ActivationRadius => activationRadius;
        public float HoldRadius => holdRadius;
        public float EdgeBlendDistance => edgeBlendDistance;
        public float EdgeNoiseScale => edgeNoiseScale;
        public float EdgeNoiseStrength => edgeNoiseStrength;
        public Vector2 EdgeNoiseOffset => edgeNoiseOffset;

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

        public static void CopyActiveAnchorsTo(List<WorldZoneAnchor> destination)
        {
            if (destination == null)
                return;

            destination.Clear();
            for (int i = 0; i < _ActiveAnchors.Count; i++)
            {
                WorldZoneAnchor anchor = _ActiveAnchors[i];
                if (anchor == null)
                    continue;

                GameObject go = anchor.gameObject;
                if (go == null || !go.scene.IsValid())
                    continue;

                destination.Add(anchor);
            }
        }

        public static int ActiveAnchorVersion => _ActiveAnchorVersion;

        public bool IsInsideActivation(Vector3 playerPosition)
        {
            EvaluatePlayerState(
                playerPosition,
                out _,
                out float activationWeight,
                out _,
                out bool insideActivation,
                out _);
            _debugActivationWeight = activationWeight;
            _debugInsideActivation = insideActivation;
            return insideActivation;
        }

        public bool IsInsideHold(Vector3 playerPosition)
        {
            EvaluatePlayerState(
                playerPosition,
                out _,
                out _,
                out float holdWeight,
                out _,
                out bool insideHold);
            _debugHoldWeight = holdWeight;
            _debugInsideHold = insideHold;
            return insideHold;
        }

        public float GetFlatDistance(Vector3 playerPosition)
        {
            Vector3 delta = transform.position - playerPosition;
            delta.y = 0f;
            return delta.magnitude;
        }

        public float GetFlatDistanceSquared(Vector3 playerPosition)
        {
            Vector3 delta = transform.position - playerPosition;
            delta.y = 0f;
            return delta.sqrMagnitude;
        }

        public float EvaluateActivationWeight(Vector3 playerPosition)
        {
            float distance = GetFlatDistance(playerPosition);
            float noisyRadius = activationRadius * EvaluateNoiseRadiusMultiplier(playerPosition);
            float blend = Mathf.Max(4f, edgeBlendDistance);
            return EvaluateRadiusWeightFromDistance(distance, noisyRadius, blend);
        }

        public float EvaluateHoldWeight(Vector3 playerPosition)
        {
            float distance = GetFlatDistance(playerPosition);
            float noisyRadius = holdRadius * EvaluateNoiseRadiusMultiplier(playerPosition);
            float blend = Mathf.Max(4f, edgeBlendDistance);
            return EvaluateRadiusWeightFromDistance(distance, noisyRadius, blend);
        }

        public void EvaluatePlayerState(
            Vector3 playerPosition,
            out float flatDistanceSqr,
            out float activationWeight,
            out float holdWeight,
            out bool insideActivation,
            out bool insideHold)
        {
            Vector3 delta = transform.position - playerPosition;
            delta.y = 0f;

            flatDistanceSqr = delta.sqrMagnitude;
            float distance = Mathf.Sqrt(flatDistanceSqr);
            float blend = Mathf.Max(4f, edgeBlendDistance);
            float noiseRadiusMultiplier = EvaluateNoiseRadiusMultiplier(playerPosition);

            activationWeight = EvaluateRadiusWeightFromDistance(distance, activationRadius * noiseRadiusMultiplier, blend);
            holdWeight = EvaluateRadiusWeightFromDistance(distance, holdRadius * noiseRadiusMultiplier, blend);
            insideActivation = activationWeight > 0.01f;
            insideHold = holdWeight > 0.01f;

            _debugLastDistance = distance;
            _debugActivationWeight = activationWeight;
            _debugHoldWeight = holdWeight;
            _debugInsideActivation = insideActivation;
            _debugInsideHold = insideHold;
        }

        private float EvaluateRadiusWeight(Vector3 playerPosition, float radius)
        {
            float distance = GetFlatDistance(playerPosition);
            float noisyRadius = radius * EvaluateNoiseRadiusMultiplier(playerPosition);
            float blend = Mathf.Max(4f, edgeBlendDistance);
            return EvaluateRadiusWeightFromDistance(distance, noisyRadius, blend);
        }

        private static float EvaluateRadiusWeightFromDistance(float distance, float noisyRadius, float blend)
        {
            float innerRadius = Mathf.Max(0f, noisyRadius - blend);

            if (distance <= innerRadius)
                return 1f;

            if (distance >= noisyRadius)
                return 0f;

            return 1f - Mathf.InverseLerp(innerRadius, noisyRadius, distance);
        }

        private float EvaluateNoiseRadiusMultiplier(Vector3 playerPosition)
        {
            float scale = Mathf.Max(0.0001f, edgeNoiseScale);
            Vector2 sample = new Vector2(playerPosition.x, playerPosition.z) * scale + edgeNoiseOffset;
            float noise = Mathf.PerlinNoise(sample.x, sample.y);
            float centered = (noise - 0.5f) * 2f;
            return Mathf.Clamp(1f + centered * edgeNoiseStrength, 0.75f, 1.35f);
        }

        private static void RegisterActiveAnchor(WorldZoneAnchor anchor)
        {
            if (anchor == null || _ActiveAnchors.Contains(anchor))
                return;

            _ActiveAnchors.Add(anchor);
            _ActiveAnchorVersion++;
        }

        private static void UnregisterActiveAnchor(WorldZoneAnchor anchor)
        {
            if (anchor == null)
                return;

            if (_ActiveAnchors.Remove(anchor))
                _ActiveAnchorVersion++;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;
#endif
            activationRadius = Mathf.Max(24f, activationRadius);
            holdRadius = Mathf.Max(activationRadius + 10f, holdRadius);
            edgeBlendDistance = Mathf.Clamp(edgeBlendDistance, 4f, holdRadius * 0.45f);
            edgeNoiseScale = Mathf.Clamp(edgeNoiseScale, 0.001f, 0.2f);
            edgeNoiseStrength = Mathf.Clamp(edgeNoiseStrength, 0f, 0.35f);
            priority = Mathf.Clamp(priority, -10, 20);

            if (string.IsNullOrWhiteSpace(zoneId))
                zoneId = "zone.generic";

            if (string.IsNullOrWhiteSpace(zoneLabel))
                zoneLabel = gameObject.name;

            if (dominantMatrixBiome != null && dominantBiomeFamily == null)
                dominantBiomeFamily = dominantMatrixBiome.familyProfile;

            _ActiveAnchorVersion++;
        }
#endif
    }
}
