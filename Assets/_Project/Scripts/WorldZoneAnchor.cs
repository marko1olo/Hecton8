using Hecton8.Environment;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    public sealed class WorldZoneAnchor : MonoBehaviour
    {
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
        [SerializeField] private int priority = 0;

        [Header("Future Use")]
        [SerializeField] private string gameplayIntent = "General world space.";
        [SerializeField] private bool routeCritical;

        [Header("Diagnostics")]
        [SerializeField] private float _debugLastDistance;
        [SerializeField] private bool _debugInsideActivation;
        [SerializeField] private bool _debugInsideHold;

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

        public bool IsInsideActivation(Vector3 playerPosition)
        {
            float distance = GetFlatDistance(playerPosition);
            _debugLastDistance = distance;
            _debugInsideActivation = distance <= activationRadius;
            _debugInsideHold = distance <= holdRadius;
            return _debugInsideActivation;
        }

        public bool IsInsideHold(Vector3 playerPosition)
        {
            float distance = GetFlatDistance(playerPosition);
            _debugLastDistance = distance;
            _debugInsideActivation = distance <= activationRadius;
            _debugInsideHold = distance <= holdRadius;
            return _debugInsideHold;
        }

        public float GetFlatDistance(Vector3 playerPosition)
        {
            Vector3 delta = transform.position - playerPosition;
            delta.y = 0f;
            return delta.magnitude;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            activationRadius = Mathf.Max(24f, activationRadius);
            holdRadius = Mathf.Max(activationRadius + 10f, holdRadius);
            priority = Mathf.Clamp(priority, -10, 20);

            if (string.IsNullOrWhiteSpace(zoneId))
                zoneId = "zone.generic";

            if (string.IsNullOrWhiteSpace(zoneLabel))
                zoneLabel = gameObject.name;

            if (dominantMatrixBiome != null && dominantBiomeFamily == null)
                dominantBiomeFamily = dominantMatrixBiome.familyProfile;
        }
#endif
    }
}
