using System.Collections.Generic;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4040)]
    public sealed class WorldPopulationDirector : MonoBehaviour, ISlowTickable
    {
        private const string NoneLabel = "None";
        private const string NoMatchingRuleLabel = "No matching rule";

        [Header("References")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private WorldZoneDirector worldZoneDirector;
        [SerializeField] private WorldContentDirector worldContentDirector;
        [SerializeField] private List<WorldPopulationRule> rules = new List<WorldPopulationRule>();

        [Header("Runtime Auto Resolve")]
        [SerializeField, Min(0f)] private float autoResolveRetryInterval = 1f;

        [Header("Diagnostics")]
        [SerializeField] private int _debugRuleCount;
        [SerializeField] private int _debugMatchedRuleCount;
        [SerializeField] private int _debugResolvedSocketCount;
        [SerializeField] private string _debugCurrentZone = "None";
        [SerializeField] private string _debugCurrentZoneBiome = "None";
        [SerializeField] private string _debugSecondaryZone = "None";
        [SerializeField] private string _debugSecondaryZoneBiome = "None";
        [SerializeField] private float _debugZoneBlendFactor;
        [SerializeField] private string _debugCurrentSocket = "None";
        [SerializeField] private string _debugPrimaryRule = "None";
        [SerializeField] private string _debugPrimaryPrefabFamily = "None";
        [SerializeField] private string _debugPrimaryBiomeFit = "None";
        [SerializeField] private string _debugPrimaryExtraction = "None";
        [SerializeField] private string _debugPrimaryLandmark = "None";
        [SerializeField] private string _debugPrimarySpatialRole = "None";
        [SerializeField] private string _debugPrimarySpatialReason = "None";
        [SerializeField] private string _debugPrimaryBorderRole = "None";
        [SerializeField] private string _debugPrimaryBorderReason = "None";
        [SerializeField] private string _debugPrimaryResourceItem = "None";
        [SerializeField] private string _debugPrimaryResourceReason = "None";
        [SerializeField] private string _debugPrimaryMotivationPull = "None";
        [SerializeField] private string _debugPrimaryMotivationReason = "None";
        [SerializeField] private string _debugPrimarySandboxAttractionRole = "None";
        [SerializeField] private string _debugPrimarySandboxAttractionReason = "None";
        [SerializeField] private string _debugPrimaryZoneRoleFamily = "None";
        [SerializeField] private string _debugPrimaryZoneRoleLayout = "None";
        [SerializeField] private string _debugPrimaryZoneRolePriority = "None";
        [SerializeField] private string _debugPrimaryPurpose = "None";
        [SerializeField] private float _debugPrimaryEffectiveDensity;

        private bool _registeredToTickManager;
        private float _nextAutoResolveAttemptTime = float.NegativeInfinity;

        private void Awake()
        {
            ResolveReferences(force: true);
            UpdateDiagnostics(null, null, 0f, null, null, 0f, "None", "None", "None", "None", "None", "None", "None", "None", "None", "None", "None", "None", "None", "None", "None", "None", "None", 0, 0);
        }

        private void OnEnable()
        {
            TryRegister();
        }

        private void Start()
        {
            TryRegister();

            EvaluateRules();
        }

        private void OnDisable()
        {
            TryUnregister();
        }

        private void TryRegister()
        {
            if (_registeredToTickManager || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            _registeredToTickManager = GlobalRegistry.SlowTickables.Contains(this);
        }

        private void TryUnregister()
        {
            if (!_registeredToTickManager)
                return;

                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);

            _registeredToTickManager = false;
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
            WorldZoneAnchor secondaryZone = worldZoneDirector != null ? worldZoneDirector.SecondaryZone : null;
            float zoneBlendFactor = worldZoneDirector != null ? worldZoneDirector.CurrentBlendFactor : 0f;
            WorldContentSocket socket = FindNearestSocketInZone(zone);

            if (worldContentDirector == null || worldContentDirector.Sockets == null || worldContentDirector.Sockets.Count == 0)
            {
                UpdateDiagnostics(zone, secondaryZone, zoneBlendFactor, socket, null, 0f, NoneLabel, NoneLabel, NoneLabel, NoneLabel, NoneLabel, NoneLabel, NoneLabel, NoneLabel, NoneLabel, NoneLabel, NoneLabel, NoneLabel, NoneLabel, NoneLabel, NoneLabel, NoneLabel, NoneLabel, 0, 0);
                return;
            }

            WorldPopulationRule primaryRule = null;
            float primaryDensityWeight = 0f;
            string primaryBiomeFit = "None";
            string primaryExtraction = "None";
            string primaryLandmark = "None";
            string primarySpatialRole = "None";
            string primarySpatialReason = "None";
            string primaryBorderRole = "None";
            string primaryBorderReason = "None";
            string primaryResourceItem = "None";
            string primaryResourceReason = "None";
            string primaryMotivationPull = "None";
            string primaryMotivationReason = "None";
            string primarySandboxAttractionRole = "None";
            string primarySandboxAttractionReason = "None";
            string primaryZoneRoleFamily = "None";
            string primaryZoneRoleLayout = "None";
            string primaryZoneRolePriority = "None";
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

                    WorldZoneAnchor candidateZone = candidateSocket.GetZoneAnchor();
                    bool captureSocketDiagnostics = candidateSocket == socket;
                    PopulationSelection candidateSelection = FindPrimaryRule(candidateZone, candidateSocket, out int candidateMatchCount, captureSocketDiagnostics);
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
                            candidateSelection.BorderBlendRole,
                            candidateSelection.BorderBlendReason,
                            candidateSelection.ResourceChannelItem,
                            candidateSelection.ResourceChannelReason,
                            candidateSelection.MotivationPull,
                            candidateSelection.MotivationReason,
                            candidateSelection.SandboxAttractionRole,
                            candidateSelection.SandboxAttractionReason,
                            candidateSelection.ZoneRoleFamily,
                            candidateSelection.ZoneRoleLayout,
                            candidateSelection.ZoneRolePriority);
                        resolvedSocketCount++;
                    }
                    else
                    {
                        candidateSocket.ClearPopulationRecommendation();
                    }

                    if (candidateSocket == socket)
                    {
                        PopulationSelection blendedSelection = FindPrimaryRule(zone, secondaryZone, zoneBlendFactor, candidateSocket, out int blendedMatchCount, true);
                        primaryRule = blendedSelection.Rule;
                        primaryDensityWeight = blendedSelection.EffectiveDensityWeight;
                        primaryBiomeFit = blendedSelection.BiomeFitReason;
                        primaryExtraction = blendedSelection.ExtractionFocus;
                        primaryLandmark = blendedSelection.LandmarkGuidance;
                        primarySpatialRole = blendedSelection.SpatialRole;
                        primarySpatialReason = blendedSelection.SpatialReason;
                        primaryBorderRole = blendedSelection.BorderBlendRole;
                        primaryBorderReason = blendedSelection.BorderBlendReason;
                        primaryResourceItem = blendedSelection.ResourceChannelItem;
                        primaryResourceReason = blendedSelection.ResourceChannelReason;
                        primaryMotivationPull = blendedSelection.MotivationPull;
                        primaryMotivationReason = blendedSelection.MotivationReason;
                        primarySandboxAttractionRole = blendedSelection.SandboxAttractionRole;
                        primarySandboxAttractionReason = blendedSelection.SandboxAttractionReason;
                        primaryZoneRoleFamily = blendedSelection.ZoneRoleFamily;
                        primaryZoneRoleLayout = blendedSelection.ZoneRoleLayout;
                        primaryZoneRolePriority = blendedSelection.ZoneRolePriority;
                        primaryPurpose = blendedSelection.ResolvedPurpose;
                        matchedCount = Mathf.Max(candidateMatchCount, blendedMatchCount);
                    }
                }
            }

            UpdateDiagnostics(zone, secondaryZone, zoneBlendFactor, socket, primaryRule, primaryDensityWeight, primaryBiomeFit, primaryExtraction, primaryLandmark, primarySpatialRole, primarySpatialReason, primaryBorderRole, primaryBorderReason, primaryResourceItem, primaryResourceReason, primaryMotivationPull, primaryMotivationReason, primarySandboxAttractionRole, primarySandboxAttractionReason, primaryZoneRoleFamily, primaryZoneRoleLayout, primaryZoneRolePriority, primaryPurpose, matchedCount, resolvedSocketCount);
        }

        private WorldContentSocket FindNearestSocketInZone(WorldZoneAnchor zone)
        {
            if (zone == null || playerTransform == null || worldContentDirector == null)
                return null;

            IReadOnlyList<WorldContentSocket> sockets = worldContentDirector.Sockets;
            WorldContentSocket best = null;
            float bestDistanceSqr = float.MaxValue;

            for (int i = 0; i < sockets.Count; i++)
            {
                WorldContentSocket socket = sockets[i];
                if (socket == null)
                    continue;

                WorldZoneAnchor socketZone = socket.GetZoneAnchor();
                if (socketZone == null || socketZone.ZoneId != zone.ZoneId)
                    continue;

                float distanceSqr = socket.GetFlatDistanceSquared(playerTransform.position);
                if (distanceSqr < bestDistanceSqr)
                {
                    bestDistanceSqr = distanceSqr;
                    best = socket;
                }
            }

            return best;
        }

        private bool NeedsAutoResolve()
        {
            return playerTransform == null ||
                   worldZoneDirector == null ||
                   worldContentDirector == null;
        }

        private void ResolveReferences(bool force = false)
        {
            if (!force && !NeedsAutoResolve())
                return;

            float now = Time.realtimeSinceStartup;
            if (!force && now < _nextAutoResolveAttemptTime)
                return;

            _nextAutoResolveAttemptTime = now + Mathf.Max(0f, autoResolveRetryInterval);

            WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);
            WorldRuntimeReferenceUtility.TryResolveWorldZoneDirector(ref worldZoneDirector);
            WorldRuntimeReferenceUtility.TryResolveWorldContentDirector(ref worldContentDirector);
        }

        private PopulationSelection FindPrimaryRule(WorldZoneAnchor zone, WorldContentSocket socket, out int matchedCount, bool captureDiagnostics)
        {
            return FindPrimaryRule(zone, null, 0f, socket, out matchedCount, captureDiagnostics);
        }

        private PopulationSelection FindPrimaryRule(WorldZoneAnchor zone, WorldZoneAnchor secondaryZone, float blendFactor, WorldContentSocket socket, out int matchedCount, bool captureDiagnostics)
        {
            WorldPopulationRule primaryRule = null;
            float bestDensity = float.MinValue;
            bool bestPrimaryMatched = false;
            bool bestSecondaryMatched = false;
            WorldZoneAnchor bestResolvedZone = null;
            matchedCount = 0;

            for (int i = 0; i < rules.Count; i++)
            {
                WorldPopulationRule rule = rules[i];
                if (rule == null)
                    continue;

                float primaryDensity = rule.GetEffectiveDensityWeight(zone, socket);
                float secondaryDensity = secondaryZone != null ? rule.GetEffectiveDensityWeight(secondaryZone, socket) : 0f;
                bool primaryMatched = primaryDensity > 0f;
                bool secondaryMatched = secondaryDensity > 0f;
                if (!primaryMatched && !secondaryMatched)
                    continue;

                matchedCount++;
                float candidateDensity = primaryMatched && secondaryMatched && blendFactor > 0.001f
                    ? Mathf.Lerp(primaryDensity, secondaryDensity, blendFactor)
                    : Mathf.Max(primaryDensity, secondaryDensity);
                candidateDensity *= rule.GetBorderBlendMultiplier(zone, secondaryZone, socket, blendFactor);
                if (candidateDensity <= bestDensity && primaryRule != null)
                    continue;

                WorldZoneAnchor resolvedZone = secondaryMatched && secondaryDensity > primaryDensity
                    ? secondaryZone
                    : zone;
                primaryRule = rule;
                bestDensity = candidateDensity;
                bestPrimaryMatched = primaryMatched;
                bestSecondaryMatched = secondaryMatched;
                bestResolvedZone = resolvedZone;
            }

            if (primaryRule == null)
            {
                if (captureDiagnostics)
                {
                    UpdateDiagnostics(zone, secondaryZone, blendFactor, socket, null, 0f, NoneLabel, NoneLabel, NoneLabel, NoneLabel, NoneLabel, NoneLabel, NoneLabel, NoneLabel, NoneLabel, NoneLabel, NoneLabel, NoneLabel, NoneLabel, NoneLabel, NoneLabel, NoneLabel, NoneLabel, 0, 0);
                }

                return default;
            }

            return BuildPopulationSelection(
                primaryRule,
                bestDensity,
                zone,
                secondaryZone,
                blendFactor,
                socket,
                bestResolvedZone,
                bestPrimaryMatched,
                bestSecondaryMatched,
                captureDiagnostics);
        }

        private static PopulationSelection BuildPopulationSelection(
            WorldPopulationRule rule,
            float effectiveDensityWeight,
            WorldZoneAnchor primaryZone,
            WorldZoneAnchor secondaryZone,
            float blendFactor,
            WorldContentSocket socket,
            WorldZoneAnchor resolvedZone,
            bool primaryMatched,
            bool secondaryMatched,
            bool captureDiagnostics)
        {
            if (!captureDiagnostics)
            {
                return new PopulationSelection(
                    rule,
                    effectiveDensityWeight,
                    NoneLabel,
                    NoneLabel,
                    NoneLabel,
                    NoneLabel,
                    NoneLabel,
                    NoneLabel,
                    NoneLabel,
                    NoneLabel,
                    NoneLabel,
                    NoneLabel,
                    NoneLabel,
                    NoneLabel,
                    NoneLabel,
                    NoneLabel,
                    NoneLabel,
                    NoneLabel,
                    NoneLabel);
            }

            return new PopulationSelection(
                rule,
                effectiveDensityWeight,
                BuildBlendedString(rule.BuildBiomeFitReason(primaryZone, socket), rule.BuildBiomeFitReason(secondaryZone, socket), primaryMatched, secondaryMatched, blendFactor),
                BuildBlendedString(rule.BuildExtractionFocus(primaryZone), rule.BuildExtractionFocus(secondaryZone), primaryMatched, secondaryMatched, blendFactor),
                BuildBlendedString(rule.BuildLandmarkGuidance(primaryZone), rule.BuildLandmarkGuidance(secondaryZone), primaryMatched, secondaryMatched, blendFactor),
                BuildBlendedString(rule.BuildResolvedPurpose(primaryZone), rule.BuildResolvedPurpose(secondaryZone), primaryMatched, secondaryMatched, blendFactor),
                rule.BuildSpatialRole(resolvedZone, socket),
                BuildBlendedString(rule.BuildSpatialRoleReason(primaryZone, socket), rule.BuildSpatialRoleReason(secondaryZone, socket), primaryMatched, secondaryMatched, blendFactor),
                rule.BuildBorderBlendRole(primaryZone, secondaryZone, socket, blendFactor),
                rule.BuildBorderBlendReason(primaryZone, secondaryZone, socket, blendFactor),
                rule.BuildResourceChannelItem(resolvedZone, socket),
                rule.BuildResourceChannelReason(resolvedZone, socket),
                rule.BuildMotivationPull(resolvedZone, socket),
                rule.BuildMotivationReason(resolvedZone, socket),
                rule.BuildSandboxAttractionRole(resolvedZone, socket),
                rule.BuildSandboxAttractionReason(resolvedZone, socket),
                rule.BuildZoneRoleFamily(resolvedZone, socket),
                rule.BuildZoneRoleLayout(resolvedZone, socket),
                rule.BuildZoneRolePriority(resolvedZone, socket));
        }

        private void UpdateDiagnostics(
            WorldZoneAnchor zone,
            WorldZoneAnchor secondaryZone,
            float zoneBlendFactor,
            WorldContentSocket socket,
            WorldPopulationRule primaryRule,
            float primaryDensityWeight,
            string primaryBiomeFit,
            string primaryExtraction,
            string primaryLandmark,
            string primarySpatialRole,
            string primarySpatialReason,
            string primaryBorderRole,
            string primaryBorderReason,
            string primaryResourceItem,
            string primaryResourceReason,
            string primaryMotivationPull,
            string primaryMotivationReason,
            string primarySandboxAttractionRole,
            string primarySandboxAttractionReason,
            string primaryZoneRoleFamily,
            string primaryZoneRoleLayout,
            string primaryZoneRolePriority,
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
            _debugSecondaryZone = secondaryZone != null ? secondaryZone.ZoneLabel : "None";
            _debugSecondaryZoneBiome = secondaryZone != null && secondaryZone.DominantBiomeFamily != null
                ? secondaryZone.DominantBiomeFamily.familyLabel
                : "None";
            _debugZoneBlendFactor = Mathf.Clamp01(zoneBlendFactor);
            _debugCurrentSocket = socket != null ? socket.SocketLabel : "None";
            _debugPrimaryRule = primaryRule != null ? primaryRule.ruleLabel : NoMatchingRuleLabel;
            _debugPrimaryPrefabFamily = primaryRule != null && !string.IsNullOrWhiteSpace(primaryRule.prefabFamily)
                ? primaryRule.prefabFamily
                : "None";
            _debugPrimaryBiomeFit = string.IsNullOrWhiteSpace(primaryBiomeFit) ? "None" : primaryBiomeFit;
            _debugPrimaryExtraction = string.IsNullOrWhiteSpace(primaryExtraction) ? "None" : primaryExtraction;
            _debugPrimaryLandmark = string.IsNullOrWhiteSpace(primaryLandmark) ? "None" : primaryLandmark;
            _debugPrimarySpatialRole = string.IsNullOrWhiteSpace(primarySpatialRole) ? "None" : primarySpatialRole;
            _debugPrimarySpatialReason = string.IsNullOrWhiteSpace(primarySpatialReason) ? "None" : primarySpatialReason;
            _debugPrimaryBorderRole = string.IsNullOrWhiteSpace(primaryBorderRole) ? "None" : primaryBorderRole;
            _debugPrimaryBorderReason = string.IsNullOrWhiteSpace(primaryBorderReason) ? "None" : primaryBorderReason;
            _debugPrimaryResourceItem = string.IsNullOrWhiteSpace(primaryResourceItem) ? "None" : primaryResourceItem;
            _debugPrimaryResourceReason = string.IsNullOrWhiteSpace(primaryResourceReason) ? "None" : primaryResourceReason;
            _debugPrimaryMotivationPull = string.IsNullOrWhiteSpace(primaryMotivationPull) ? "None" : primaryMotivationPull;
            _debugPrimaryMotivationReason = string.IsNullOrWhiteSpace(primaryMotivationReason) ? "None" : primaryMotivationReason;
            _debugPrimarySandboxAttractionRole = string.IsNullOrWhiteSpace(primarySandboxAttractionRole) ? "None" : primarySandboxAttractionRole;
            _debugPrimarySandboxAttractionReason = string.IsNullOrWhiteSpace(primarySandboxAttractionReason) ? "None" : primarySandboxAttractionReason;
            _debugPrimaryZoneRoleFamily = string.IsNullOrWhiteSpace(primaryZoneRoleFamily) ? "None" : primaryZoneRoleFamily;
            _debugPrimaryZoneRoleLayout = string.IsNullOrWhiteSpace(primaryZoneRoleLayout) ? "None" : primaryZoneRoleLayout;
            _debugPrimaryZoneRolePriority = string.IsNullOrWhiteSpace(primaryZoneRolePriority) ? "None" : primaryZoneRolePriority;
            _debugPrimaryPurpose = string.IsNullOrWhiteSpace(primaryPurpose) ? "None" : primaryPurpose;
            _debugPrimaryEffectiveDensity = Mathf.Max(0f, primaryDensityWeight);
        }

        private static string BuildBlendedString(string primary, string secondary, bool primaryMatched, bool secondaryMatched, float blendFactor)
        {
            if (primaryMatched && !secondaryMatched)
                return string.IsNullOrWhiteSpace(primary) ? NoneLabel : primary;

            if (!primaryMatched && secondaryMatched)
                return string.IsNullOrWhiteSpace(secondary) ? NoneLabel : secondary;

            if (!primaryMatched && !secondaryMatched)
                return NoneLabel;

            string cleanPrimary = string.IsNullOrWhiteSpace(primary) ? NoneLabel : primary;
            string cleanSecondary = string.IsNullOrWhiteSpace(secondary) ? NoneLabel : secondary;
            if (cleanPrimary == cleanSecondary || blendFactor <= 0.12f)
                return cleanPrimary;

            if (blendFactor >= 0.68f)
                return cleanSecondary;

            return $"{cleanPrimary} | blending with {cleanSecondary}";
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
                string borderBlendRole,
                string borderBlendReason,
                string resourceChannelItem,
                string resourceChannelReason,
                string motivationPull,
                string motivationReason,
                string sandboxAttractionRole,
                string sandboxAttractionReason,
                string zoneRoleFamily,
                string zoneRoleLayout,
                string zoneRolePriority)
            {
                Rule = rule;
                EffectiveDensityWeight = effectiveDensityWeight;
                BiomeFitReason = biomeFitReason;
                ExtractionFocus = extractionFocus;
                LandmarkGuidance = landmarkGuidance;
                ResolvedPurpose = resolvedPurpose;
                SpatialRole = spatialRole;
                SpatialReason = spatialReason;
                BorderBlendRole = borderBlendRole;
                BorderBlendReason = borderBlendReason;
                ResourceChannelItem = resourceChannelItem;
                ResourceChannelReason = resourceChannelReason;
                MotivationPull = motivationPull;
                MotivationReason = motivationReason;
                SandboxAttractionRole = sandboxAttractionRole;
                SandboxAttractionReason = sandboxAttractionReason;
                ZoneRoleFamily = zoneRoleFamily;
                ZoneRoleLayout = zoneRoleLayout;
                ZoneRolePriority = zoneRolePriority;
            }

            public WorldPopulationRule Rule { get; }
            public float EffectiveDensityWeight { get; }
            public string BiomeFitReason { get; }
            public string ExtractionFocus { get; }
            public string LandmarkGuidance { get; }
            public string ResolvedPurpose { get; }
            public string SpatialRole { get; }
            public string SpatialReason { get; }
            public string BorderBlendRole { get; }
            public string BorderBlendReason { get; }
            public string ResourceChannelItem { get; }
            public string ResourceChannelReason { get; }
            public string MotivationPull { get; }
            public string MotivationReason { get; }
            public string SandboxAttractionRole { get; }
            public string SandboxAttractionReason { get; }
            public string ZoneRoleFamily { get; }
            public string ZoneRoleLayout { get; }
            public string ZoneRolePriority { get; }
        }
    }
}
