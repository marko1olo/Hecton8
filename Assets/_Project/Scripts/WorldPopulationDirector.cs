using System.Collections.Generic;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4040)]
    public sealed class WorldPopulationDirector : MonoBehaviour, ISlowTickable
    {
        [Header("References")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private WorldZoneDirector worldZoneDirector;
        [SerializeField] private WorldContentDirector worldContentDirector;
        [SerializeField] private List<WorldPopulationRule> rules = new List<WorldPopulationRule>();

        [Header("Diagnostics")]
        [SerializeField] private int _debugRuleCount;
        [SerializeField] private int _debugMatchedRuleCount;
        [SerializeField] private int _debugResolvedSocketCount;
        [SerializeField] private string _debugCurrentZone = "None";
        [SerializeField] private string _debugCurrentZoneBiome = "None";
        [SerializeField] private string _debugCurrentSocket = "None";
        [SerializeField] private string _debugPrimaryRule = "None";
        [SerializeField] private string _debugPrimaryPrefabFamily = "None";
        [SerializeField] private string _debugPrimaryBiomeFit = "None";
        [SerializeField] private string _debugPrimaryExtraction = "None";
        [SerializeField] private string _debugPrimaryLandmark = "None";
        [SerializeField] private string _debugPrimarySpatialRole = "None";
        [SerializeField] private string _debugPrimarySpatialReason = "None";
        [SerializeField] private string _debugPrimaryZoneRoleFamily = "None";
        [SerializeField] private string _debugPrimaryZoneRoleLayout = "None";
        [SerializeField] private string _debugPrimaryPurpose = "None";
        [SerializeField] private float _debugPrimaryEffectiveDensity;

        private bool _registeredToTickManager;

        private void Awake()
        {
            ResolveReferences();
            UpdateDiagnostics(null, null, null, 0f, "None", "None", "None", "None", "None", "None", "None", "None", 0, 0);
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

            EvaluateRules();
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
            EvaluateRules();
        }

        public void SetRules(IReadOnlyList<WorldPopulationRule> sourceRules)
        {
            rules.Clear();
            if (sourceRules == null)
                return;

            for (int i = 0; i < sourceRules.Count; i++)
            {
                if (sourceRules[i] != null)
                    rules.Add(sourceRules[i]);
            }
        }

        private void EvaluateRules()
        {
            ResolveReferences();

            WorldZoneAnchor zone = worldZoneDirector != null ? worldZoneDirector.CurrentZone : null;
            WorldContentSocket socket = FindNearestSocketInZone(zone);

            WorldPopulationRule primaryRule = null;
            float primaryDensityWeight = 0f;
            string primaryBiomeFit = "None";
            string primaryExtraction = "None";
            string primaryLandmark = "None";
            string primarySpatialRole = "None";
            string primarySpatialReason = "None";
            string primaryZoneRoleFamily = "None";
            string primaryZoneRoleLayout = "None";
            string primaryPurpose = "None";
            int matchedCount = 0;
            int resolvedSocketCount = 0;

            IReadOnlyList<WorldContentSocket> sockets = worldContentDirector != null ? worldContentDirector.Sockets : null;
            if (sockets != null)
            {
                for (int i = 0; i < sockets.Count; i++)
                {
                    WorldContentSocket candidateSocket = sockets[i];
                    if (candidateSocket == null)
                        continue;

                    WorldZoneAnchor candidateZone = candidateSocket.GetComponentInParent<WorldZoneAnchor>();
                    PopulationSelection candidateSelection = FindPrimaryRule(candidateZone, candidateSocket, out int candidateMatchCount);
                    WorldPopulationRule candidateRule = candidateSelection.Rule;
                    if (candidateRule != null)
                    {
                        candidateSocket.ApplyPopulationRecommendation(
                            candidateRule,
                            candidateSelection.EffectiveDensityWeight,
                            candidateSelection.BiomeFitReason,
                            candidateSelection.ExtractionFocus,
                            candidateSelection.LandmarkGuidance,
                            candidateSelection.ResolvedPurpose,
                            candidateSelection.SpatialRole,
                            candidateSelection.SpatialReason,
                            candidateSelection.ZoneRoleFamily,
                            candidateSelection.ZoneRoleLayout);
                        resolvedSocketCount++;
                    }
                    else
                    {
                        candidateSocket.ClearPopulationRecommendation();
                    }

                    if (candidateSocket == socket)
                    {
                        primaryRule = candidateSelection.Rule;
                        primaryDensityWeight = candidateSelection.EffectiveDensityWeight;
                        primaryBiomeFit = candidateSelection.BiomeFitReason;
                        primaryExtraction = candidateSelection.ExtractionFocus;
                        primaryLandmark = candidateSelection.LandmarkGuidance;
                        primarySpatialRole = candidateSelection.SpatialRole;
                        primarySpatialReason = candidateSelection.SpatialReason;
                        primaryZoneRoleFamily = candidateSelection.ZoneRoleFamily;
                        primaryZoneRoleLayout = candidateSelection.ZoneRoleLayout;
                        primaryPurpose = candidateSelection.ResolvedPurpose;
                        matchedCount = candidateMatchCount;
                    }
                }
            }

            UpdateDiagnostics(zone, socket, primaryRule, primaryDensityWeight, primaryBiomeFit, primaryExtraction, primaryLandmark, primarySpatialRole, primarySpatialReason, primaryZoneRoleFamily, primaryZoneRoleLayout, primaryPurpose, matchedCount, resolvedSocketCount);
        }

        private WorldContentSocket FindNearestSocketInZone(WorldZoneAnchor zone)
        {
            if (zone == null || playerTransform == null || worldContentDirector == null)
                return null;

            IReadOnlyList<WorldContentSocket> sockets = worldContentDirector.Sockets;
            WorldContentSocket best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < sockets.Count; i++)
            {
                WorldContentSocket socket = sockets[i];
                if (socket == null)
                    continue;

                WorldZoneAnchor socketZone = socket.GetComponentInParent<WorldZoneAnchor>();
                if (socketZone == null || socketZone.ZoneId != zone.ZoneId)
                    continue;

                float distance = socket.GetFlatDistance(playerTransform.position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = socket;
                }
            }

            return best;
        }

        private void ResolveReferences()
        {
            if (playerTransform == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player == null)
                    player = GameObject.Find("Player");

                if (player != null)
                    playerTransform = player.transform;
            }

            if (worldZoneDirector == null)
                worldZoneDirector = FindAnyObjectByType<WorldZoneDirector>();

            if (worldContentDirector == null)
                worldContentDirector = FindAnyObjectByType<WorldContentDirector>();
        }

        private PopulationSelection FindPrimaryRule(WorldZoneAnchor zone, WorldContentSocket socket, out int matchedCount)
        {
            PopulationSelection bestSelection = default;
            WorldPopulationRule primaryRule = null;
            float bestDensity = float.MinValue;
            matchedCount = 0;

            for (int i = 0; i < rules.Count; i++)
            {
                WorldPopulationRule rule = rules[i];
                if (rule == null || !rule.Matches(zone, socket))
                    continue;

                matchedCount++;
                float candidateDensity = rule.GetEffectiveDensityWeight(zone, socket);
                if (candidateDensity <= bestDensity && primaryRule != null)
                    continue;

                primaryRule = rule;
                bestDensity = candidateDensity;
                bestSelection = new PopulationSelection(
                    rule,
                    candidateDensity,
                    rule.BuildBiomeFitReason(zone, socket),
                    rule.BuildExtractionFocus(zone),
                    rule.BuildLandmarkGuidance(zone),
                    rule.BuildResolvedPurpose(zone),
                    rule.BuildSpatialRole(zone, socket),
                    rule.BuildSpatialRoleReason(zone, socket),
                    rule.BuildZoneRoleFamily(zone, socket),
                    rule.BuildZoneRoleLayout(zone, socket));
            }

            return bestSelection;
        }

        private void UpdateDiagnostics(
            WorldZoneAnchor zone,
            WorldContentSocket socket,
            WorldPopulationRule primaryRule,
            float primaryDensityWeight,
            string primaryBiomeFit,
            string primaryExtraction,
            string primaryLandmark,
            string primarySpatialRole,
            string primarySpatialReason,
            string primaryZoneRoleFamily,
            string primaryZoneRoleLayout,
            string primaryPurpose,
            int matchedCount,
            int resolvedSocketCount)
        {
            _debugRuleCount = rules.Count;
            _debugMatchedRuleCount = matchedCount;
            _debugResolvedSocketCount = resolvedSocketCount;
            _debugCurrentZone = zone != null ? zone.ZoneLabel : "None";
            _debugCurrentZoneBiome = zone != null && zone.DominantBiomeFamily != null
                ? zone.DominantBiomeFamily.familyLabel
                : "None";
            _debugCurrentSocket = socket != null ? socket.SocketLabel : "None";
            _debugPrimaryRule = primaryRule != null ? primaryRule.ruleLabel : "None";
            _debugPrimaryPrefabFamily = primaryRule != null && !string.IsNullOrWhiteSpace(primaryRule.prefabFamily)
                ? primaryRule.prefabFamily
                : "None";
            _debugPrimaryBiomeFit = string.IsNullOrWhiteSpace(primaryBiomeFit) ? "None" : primaryBiomeFit;
            _debugPrimaryExtraction = string.IsNullOrWhiteSpace(primaryExtraction) ? "None" : primaryExtraction;
            _debugPrimaryLandmark = string.IsNullOrWhiteSpace(primaryLandmark) ? "None" : primaryLandmark;
            _debugPrimarySpatialRole = string.IsNullOrWhiteSpace(primarySpatialRole) ? "None" : primarySpatialRole;
            _debugPrimarySpatialReason = string.IsNullOrWhiteSpace(primarySpatialReason) ? "None" : primarySpatialReason;
            _debugPrimaryZoneRoleFamily = string.IsNullOrWhiteSpace(primaryZoneRoleFamily) ? "None" : primaryZoneRoleFamily;
            _debugPrimaryZoneRoleLayout = string.IsNullOrWhiteSpace(primaryZoneRoleLayout) ? "None" : primaryZoneRoleLayout;
            _debugPrimaryPurpose = string.IsNullOrWhiteSpace(primaryPurpose) ? "None" : primaryPurpose;
            _debugPrimaryEffectiveDensity = Mathf.Max(0f, primaryDensityWeight);
        }

        private readonly struct PopulationSelection
        {
            public PopulationSelection(
                WorldPopulationRule rule,
                float effectiveDensityWeight,
                string biomeFitReason,
                string extractionFocus,
                string landmarkGuidance,
                string resolvedPurpose,
                string spatialRole,
                string spatialReason,
                string zoneRoleFamily,
                string zoneRoleLayout)
            {
                Rule = rule;
                EffectiveDensityWeight = effectiveDensityWeight;
                BiomeFitReason = biomeFitReason;
                ExtractionFocus = extractionFocus;
                LandmarkGuidance = landmarkGuidance;
                ResolvedPurpose = resolvedPurpose;
                SpatialRole = spatialRole;
                SpatialReason = spatialReason;
                ZoneRoleFamily = zoneRoleFamily;
                ZoneRoleLayout = zoneRoleLayout;
            }

            public WorldPopulationRule Rule { get; }
            public float EffectiveDensityWeight { get; }
            public string BiomeFitReason { get; }
            public string ExtractionFocus { get; }
            public string LandmarkGuidance { get; }
            public string ResolvedPurpose { get; }
            public string SpatialRole { get; }
            public string SpatialReason { get; }
            public string ZoneRoleFamily { get; }
            public string ZoneRoleLayout { get; }
        }
    }
}
