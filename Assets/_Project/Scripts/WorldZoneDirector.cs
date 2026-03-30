using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Environment;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4060)]
    public sealed class WorldZoneDirector : MonoBehaviour, ISlowTickable
    {
        [Header("References")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private ScatterBudgetController scatterBudgetController;
        [SerializeField] private WorldSliceDirector worldSliceDirector;

        [Header("Diagnostics")]
        [SerializeField] private string _debugCurrentZoneId = "zone.none";
        [SerializeField] private string _debugCurrentZoneLabel = "None";
        [SerializeField] private string _debugCurrentZoneKind = "Generic";
        [SerializeField] private string _debugCurrentZoneTier = "Starter";
        [SerializeField] private string _debugZonePlan = "None";
        [SerializeField] private string _debugHeroFamily = "None";
        [SerializeField] private string _debugNearFamily = "None";
        [SerializeField] private string _debugMidFamily = "None";
        [SerializeField] private string _debugFarFamily = "None";
        [SerializeField] private string _debugDominantBiome = "None";
        [SerializeField] private string _debugDominantBiomeFamily = "None";
        [SerializeField] private string _debugDominantVisitPurpose = "None";
        [SerializeField] private string _debugDominantLandmark = "None";
        [SerializeField] private string _debugDominantRisk = "None";
        [SerializeField] private string _debugNearestZone = "None";
        [SerializeField] private int _debugZoneCount;
        [SerializeField] private bool _debugApplied;

        private readonly List<WorldZoneAnchor> _anchors = new List<WorldZoneAnchor>(32);
        private bool _registeredToTickManager;
        private WorldZoneAnchor _currentZone;

        public WorldZoneAnchor CurrentZone => _currentZone;

        private void Awake()
        {
            ResolvePlayer();
            RefreshAnchors();
            UpdateDiagnostics();
        }

        private void OnEnable()
        {
            if (GameTickManager.Instance != null && !_registeredToTickManager)
            {
                GameTickManager.Instance.Register((ISlowTickable)this);
                _registeredToTickManager = true;
            }
        }

        private void Start()
        {
            if (!_registeredToTickManager && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Register((ISlowTickable)this);
                _registeredToTickManager = true;
            }

            EvaluateZones(forceRefresh: true);
        }

        private void OnDisable()
        {
            if (_registeredToTickManager && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Unregister((ISlowTickable)this);
                _registeredToTickManager = false;
            }
        }

        public void SlowTick()
        {
            EvaluateZones(forceRefresh: false);
        }

        public void RefreshAnchors()
        {
            _anchors.Clear();

            WorldZoneAnchor[] anchors = Resources.FindObjectsOfTypeAll<WorldZoneAnchor>();
            for (int i = 0; i < anchors.Length; i++)
            {
                WorldZoneAnchor anchor = anchors[i];
                if (anchor == null || anchor.gameObject == null || !anchor.gameObject.scene.IsValid())
                    continue;

                _anchors.Add(anchor);
            }

            _debugZoneCount = _anchors.Count;
        }

        private void EvaluateZones(bool forceRefresh)
        {
            ResolvePlayer();
            if (forceRefresh || _anchors.Count == 0)
                RefreshAnchors();

            if (playerTransform == null)
            {
                _debugApplied = false;
                UpdateDiagnostics();
                return;
            }

            Vector3 playerPosition = playerTransform.position;
            WorldZoneAnchor nearest = null;
            float nearestDistance = float.MaxValue;
            WorldZoneAnchor bestCandidate = null;

            for (int i = 0; i < _anchors.Count; i++)
            {
                WorldZoneAnchor anchor = _anchors[i];
                if (anchor == null)
                    continue;

                float distance = anchor.GetFlatDistance(playerPosition);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = anchor;
                }

                bool insideActivation = anchor.IsInsideActivation(playerPosition);
                bool insideHold = anchor.IsInsideHold(playerPosition);

                if (_currentZone == anchor && insideHold)
                {
                    bestCandidate = anchor;
                    continue;
                }

                if (!insideActivation)
                    continue;

                if (bestCandidate == null || Compare(anchor, bestCandidate, playerPosition) < 0)
                    bestCandidate = anchor;
            }

            _currentZone = bestCandidate ?? nearest;
            ApplyZoneProfile(_currentZone);
            _debugNearestZone = nearest != null ? nearest.ZoneLabel : "None";
            _debugApplied = _currentZone != null;
            UpdateDiagnostics();
        }

        private int Compare(WorldZoneAnchor a, WorldZoneAnchor b, Vector3 playerPosition)
        {
            int priorityCompare = b.Priority.CompareTo(a.Priority);
            if (priorityCompare != 0)
                return priorityCompare;

            return a.GetFlatDistance(playerPosition).CompareTo(b.GetFlatDistance(playerPosition));
        }

        private void ResolvePlayer()
        {
            if (playerTransform != null)
                return;

            GameObject player = GameObject.FindWithTag("Player");
            if (player == null)
                player = GameObject.Find("Player");

            if (player != null)
                playerTransform = player.transform;

            if (scatterBudgetController == null)
                scatterBudgetController = FindAnyObjectByType<ScatterBudgetController>();

            if (worldSliceDirector == null)
                worldSliceDirector = FindAnyObjectByType<WorldSliceDirector>();
        }

        private void ApplyZoneProfile(WorldZoneAnchor zone)
        {
            WorldZoneProfile profile = zone != null ? zone.Profile : null;

            float scavengeScale = profile != null ? profile.scavengeRadiusScale : 1f;
            float spawnScale = profile != null ? profile.spawnScale : 1f;
            float colliderRadiusScale = profile != null ? profile.colliderRadiusScale : 1f;
            float colliderOpsScale = profile != null ? profile.colliderOpsScale : 1f;
            float nearSliceScale = profile != null ? profile.sliceNearScale : 1f;
            float midSliceScale = profile != null ? profile.sliceMidScale : 1f;

            if (scatterBudgetController != null)
                scatterBudgetController.SetZoneScales(scavengeScale, spawnScale, colliderRadiusScale, colliderOpsScale);

            if (worldSliceDirector != null)
                worldSliceDirector.SetZoneScales(nearSliceScale, midSliceScale);
        }

        private void UpdateDiagnostics()
        {
            _debugZoneCount = _anchors.Count;

            if (_currentZone == null)
            {
                _debugCurrentZoneId = "zone.none";
                _debugCurrentZoneLabel = "None";
                _debugCurrentZoneKind = WorldZoneAnchor.ZoneKind.Generic.ToString();
                _debugCurrentZoneTier = WorldZoneAnchor.ZoneTier.Starter.ToString();
                _debugZonePlan = "None";
                _debugHeroFamily = "None";
                _debugNearFamily = "None";
                _debugMidFamily = "None";
                _debugFarFamily = "None";
                _debugDominantBiome = "None";
                _debugDominantBiomeFamily = "None";
                _debugDominantVisitPurpose = "None";
                _debugDominantLandmark = "None";
                _debugDominantRisk = "None";
                return;
            }

            HectonBiomeMatrixProfile biome = _currentZone.DominantMatrixBiome;
            HectonBiomeFamilyProfile biomeFamily = _currentZone.DominantBiomeFamily;
            _debugCurrentZoneId = _currentZone.ZoneId;
            _debugCurrentZoneLabel = _currentZone.ZoneLabel;
            _debugCurrentZoneKind = _currentZone.Kind.ToString();
            _debugCurrentZoneTier = _currentZone.Tier.ToString();
            _debugZonePlan = _currentZone.Profile != null && _currentZone.Profile.zonePlanProfile != null
                ? _currentZone.Profile.zonePlanProfile.planLabel
                : "None";
            _debugHeroFamily = _currentZone.Profile != null
                && _currentZone.Profile.zonePlanProfile != null
                && _currentZone.Profile.zonePlanProfile.heroFamily != null
                ? _currentZone.Profile.zonePlanProfile.heroFamily.familyLabel
                : "None";
            _debugNearFamily = _currentZone.Profile != null && !string.IsNullOrWhiteSpace(_currentZone.Profile.nearInteractiveFamily)
                ? _currentZone.Profile.nearInteractiveFamily
                : "None";
            _debugMidFamily = _currentZone.Profile != null && !string.IsNullOrWhiteSpace(_currentZone.Profile.midVisualFamily)
                ? _currentZone.Profile.midVisualFamily
                : "None";
            _debugFarFamily = _currentZone.Profile != null && !string.IsNullOrWhiteSpace(_currentZone.Profile.farSilhouetteFamily)
                ? _currentZone.Profile.farSilhouetteFamily
                : "None";
            _debugDominantBiome = biome != null ? biome.biomeName : "None";
            _debugDominantBiomeFamily = biomeFamily != null ? biomeFamily.familyLabel : "None";
            _debugDominantVisitPurpose = biome != null && !string.IsNullOrWhiteSpace(biome.visitPurpose)
                ? biome.visitPurpose
                : "None";
            _debugDominantLandmark = biome != null && !string.IsNullOrWhiteSpace(biome.landmarkIdentity)
                ? biome.landmarkIdentity
                : "None";
            _debugDominantRisk = biome != null && !string.IsNullOrWhiteSpace(biome.riskSummary)
                ? biome.riskSummary
                : "None";
        }
    }
}
