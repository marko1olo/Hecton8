using System;
using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Environment;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4038)]
    public sealed class WorldProceduralFillDirector : MonoBehaviour, ISlowTickable, IGlobalRegistryHotSwapListener
    {
        [Header("References")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private WorldZoneDirector worldZoneDirector;
        [SerializeField] private WorldContentDirector worldContentDirector;
        [SerializeField] private BiomeMatrixDirector biomeMatrixDirector;
        [SerializeField] private List<WorldProceduralPlacementRule> rules = new List<WorldProceduralPlacementRule>(16);
        [SerializeField] private List<WorldPrefabFamilyProfile> families = new List<WorldPrefabFamilyProfile>(16);

        [Header("Runtime Auto Resolve")]
        [SerializeField, Min(0f)] private float autoResolveRetryInterval = 1f;

        [Header("Diagnostics")]
        [SerializeField] private int _debugRuleCount;
        [SerializeField] private int _debugFamilyCount;
        [SerializeField] private int _debugResolvedSocketCount;
        [SerializeField] private string _debugCurrentZone = "None";
        [SerializeField] private string _debugCurrentBiome = "None";
        [SerializeField] private string _debugCurrentSocket = "None";
        [SerializeField] private string _debugPrimaryRule = "None";
        [SerializeField] private string _debugPrimaryFamily = "None";
        [SerializeField] private string _debugPrimarySource = "None";
        [SerializeField] private string _debugPrimaryVariant = "None";
        [SerializeField] private string _debugPrimaryDomain = "Generic";
        [SerializeField] private string _debugPrimaryPlacementMode = "Scatter";
        [SerializeField] private string _debugPrimaryHeatmap = "None";
        [SerializeField] private string _debugPrimaryIntent = "None";
        [SerializeField] private string _debugPrimaryReason = "None";
        [SerializeField] private float _debugPrimaryScore;
        [SerializeField] private int _debugPrimaryMinCount;
        [SerializeField] private int _debugPrimaryMaxCount;
        [SerializeField] private float _debugPrimarySpacingMeters;
        [SerializeField] private float _debugPrimaryClusterRadiusMeters;

        private bool _registeredToTickManager;
        private float _nextAutoResolveAttemptTime = float.NegativeInfinity;

        internal static WorldProceduralFillDirector ActiveRuntimeInstance { get; private set; }

        public IReadOnlyList<WorldProceduralPlacementRule> Rules => rules;
        public IReadOnlyList<WorldPrefabFamilyProfile> Families => families;

        private void Awake()
        {
            ActiveRuntimeInstance = this;
            ResolveReferences(force: true);
            UpdateDiagnostics(null, null, default, 0);
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
                GlobalRegistry.TryRegisterHotSwapListener(this);

            TryRegisterToTickManager();
        }

        private void Start()
        {
            TryRegisterToTickManager();
            EvaluateFill();
        }

        private void OnDisable()
        {
            TryUnregisterFromTickManager();
            GlobalRegistry.TryUnregisterHotSwapListener(this);
        }

        private void OnDestroy()
        {
            TryUnregisterFromTickManager();
            GlobalRegistry.TryUnregisterHotSwapListener(this);

            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            if (currentService == null)
            {
                _registeredToTickManager = false;
                return;
            }

            if (isActiveAndEnabled)
            {
                TryUnregisterFromTickManager();
                TryRegisterToTickManager();
            }
        }

        public void SlowTick()
        {
            EvaluateFill();
        }

        /// <summary>
        /// Forces immediate procedural fill evaluation using current zone and content data.
        /// </summary>
        public void ForceRefresh()
        {
            ResolveReferences(force: true);
            EvaluateFill();
        }

        public void SetRules(IReadOnlyList<WorldProceduralPlacementRule> sourceRules)
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

        public void SetFamilies(IReadOnlyList<WorldPrefabFamilyProfile> sourceFamilies)
        {
            families.Clear();
            if (sourceFamilies == null)
                return;

            for (int i = 0; i < sourceFamilies.Count; i++)
            {
                if (sourceFamilies[i] != null)
                    families.Add(sourceFamilies[i]);
            }
        }

        private void TryRegisterToTickManager()
        {
            if (_registeredToTickManager || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredToTickManager = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterFromTickManager()
        {
            if (!_registeredToTickManager)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);

            _registeredToTickManager = false;
        }

        private void EvaluateFill()
        {
            ResolveReferences();

            WorldZoneAnchor zone = worldZoneDirector != null ? worldZoneDirector.CurrentZone : null;
            WorldContentSocket socket = FindNearestSocketInZone(zone);
            ProceduralSelection primarySelection = default;
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
                    ProceduralSelection candidateSelection = ResolveSelection(candidateZone, candidateSocket);
                    if (candidateSelection.HasValue)
                    {
                        candidateSocket.ApplyProceduralRecommendation(
                            candidateSelection.Rule,
                            candidateSelection.Family,
                            candidateSelection.VariantId,
                            candidateSelection.Source,
                            candidateSelection.Reason,
                            candidateSelection.Intent,
                            candidateSelection.HeatmapChannel,
                            candidateSelection.MinCount,
                            candidateSelection.MaxCount,
                            candidateSelection.MinSpacingMeters,
                            candidateSelection.ClusterRadiusMeters,
                            candidateSelection.Score);
                        resolvedSocketCount++;
                    }
                    else
                    {
                        candidateSocket.ClearProceduralRecommendation();
                    }

                    if (candidateSocket == socket)
                        primarySelection = candidateSelection;
                }
            }

            UpdateDiagnostics(zone, socket, primarySelection, resolvedSocketCount);
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
                    best = socket;
                    bestDistanceSqr = distanceSqr;
                }
            }

            return best;
        }

        private ProceduralSelection ResolveSelection(WorldZoneAnchor zone, WorldContentSocket socket)
        {
            if (socket == null)
                return default;

            HectonBiomeFamilyProfile biomeFamily = zone != null ? zone.DominantBiomeFamily : biomeMatrixDirector != null ? biomeMatrixDirector.CurrentFamilyProfile : null;
            WorldProceduralPlacementRule bestRule = null;
            WorldPrefabFamilyProfile bestFamily = null;
            float bestScore = float.MinValue;

            WorldPrefabFamilyProfile profileDrivenFamily = ResolveFamilyFromSocketProfile(socket);
            WorldPrefabFamilyProfile zonePlanFamily = ResolveFamilyFromZonePlan(zone, socket);
            WorldPrefabFamilyProfile zoneProfileFamily = ResolveFamilyFromZoneProfile(zone, socket);

            for (int i = 0; i < rules.Count; i++)
            {
                WorldProceduralPlacementRule rule = rules[i];
                if (rule == null || !rule.Matches(biomeFamily, zone, socket))
                    continue;

                WorldPrefabFamilyProfile family = rule.familyProfile;
                if (family == null)
                    continue;

                float score = Mathf.Max(0.01f, rule.densityScale);
                if (profileDrivenFamily != null && profileDrivenFamily == family)
                    score += 2f;

                if (zonePlanFamily != null && zonePlanFamily == family)
                    score += 1.5f;

                if (zoneProfileFamily != null && zoneProfileFamily == family)
                    score += 0.75f;

                if (family.defaultFidelity == socket.PreferredFidelity)
                    score += 0.3f;

                if (rule.preferredFidelity == socket.PreferredFidelity)
                    score += 0.2f;

                if (!string.IsNullOrWhiteSpace(rule.requiredHeatmapChannel))
                    score += 0.15f;

                if (bestRule != null && score <= bestScore)
                    continue;

                bestRule = rule;
                bestFamily = family;
                bestScore = score;
            }

            string source;
            string intent;
            WorldPrefabFamilyProfile resolvedFamily = bestFamily;
            if (resolvedFamily != null)
            {
                source = "Rule";
                intent = bestRule != null && !string.IsNullOrWhiteSpace(bestRule.gameplayIntent)
                    ? bestRule.gameplayIntent
                    : resolvedFamily.gameplayRole;
            }
            else if (profileDrivenFamily != null)
            {
                resolvedFamily = profileDrivenFamily;
                source = "SocketProfile";
                intent = socket.Profile != null && !string.IsNullOrWhiteSpace(socket.Profile.gameplayPurpose)
                    ? socket.Profile.gameplayPurpose
                    : resolvedFamily.gameplayRole;
                bestScore = 1.25f;
            }
            else if (zonePlanFamily != null)
            {
                resolvedFamily = zonePlanFamily;
                source = "ZonePlan";
                intent = resolvedFamily.gameplayRole;
                bestScore = 1f;
            }
            else if (zoneProfileFamily != null)
            {
                resolvedFamily = zoneProfileFamily;
                source = "ZoneProfile";
                intent = resolvedFamily.gameplayRole;
                bestScore = 0.75f;
            }
            else
            {
                resolvedFamily = MapSocketKindToDefaultFamily(socket);
                source = resolvedFamily != null ? "Fallback" : "None";
                intent = resolvedFamily != null ? resolvedFamily.gameplayRole : "None";
                bestScore = resolvedFamily != null ? 0.5f : 0f;
            }

            if (resolvedFamily == null)
                return default;

            WorldPrefabFamilyProfile.VariantEntry variant = ResolveVariant(resolvedFamily, socket.SocketId);
            string variantId = variant != null && !string.IsNullOrWhiteSpace(variant.variantId)
                ? variant.variantId
                : $"{resolvedFamily.familyId}.generated";
            string heatmap = bestRule != null && !string.IsNullOrWhiteSpace(bestRule.requiredHeatmapChannel)
                ? bestRule.requiredHeatmapChannel
                : !string.IsNullOrWhiteSpace(resolvedFamily.heatmapChannel)
                    ? resolvedFamily.heatmapChannel
                    : "None";

            int minCount = bestRule != null ? Mathf.Max(0, bestRule.minInstances) : Mathf.Max(0, resolvedFamily.clusterCountMin);
            int maxCount = bestRule != null ? Mathf.Max(minCount, bestRule.maxInstances) : Mathf.Max(minCount, resolvedFamily.clusterCountMax);
            float minSpacing = bestRule != null && bestRule.minSpacingOverrideMeters > 0f
                ? bestRule.minSpacingOverrideMeters
                : resolvedFamily.minSpacingMeters;
            float clusterRadius = bestRule != null && bestRule.clusterRadiusOverrideMeters > 0f
                ? bestRule.clusterRadiusOverrideMeters
                : resolvedFamily.clusterRadiusMeters;

            return new ProceduralSelection(
                bestRule,
                resolvedFamily,
                variantId,
                source,
                BuildReason(zone, socket, biomeFamily, bestRule, resolvedFamily, source),
                string.IsNullOrWhiteSpace(intent) ? "Generic procedural world fill." : intent,
                heatmap,
                Mathf.Max(0f, bestScore),
                minCount,
                maxCount,
                minSpacing,
                clusterRadius);
        }

        private WorldPrefabFamilyProfile ResolveFamilyFromSocketProfile(WorldContentSocket socket)
        {
            if (socket == null || socket.Profile == null || string.IsNullOrWhiteSpace(socket.Profile.futurePrefabFamily))
                return null;

            return FindFamilyByKey(socket.Profile.futurePrefabFamily);
        }

        private WorldPrefabFamilyProfile ResolveFamilyFromZonePlan(WorldZoneAnchor zone, WorldContentSocket socket)
        {
            if (zone == null || zone.Profile == null || zone.Profile.zonePlanProfile == null || socket == null)
                return null;

            WorldZonePlanProfile plan = zone.Profile.zonePlanProfile;
            return socket.Kind switch
            {
                WorldContentSocket.ContentKind.ResourcePickup => plan.resourcePocketPlan != null ? plan.resourcePocketPlan.family : null,
                WorldContentSocket.ContentKind.ResourceNode => plan.nodeClusterPlan != null ? plan.nodeClusterPlan.family : null,
                WorldContentSocket.ContentKind.FabricationStation => plan.safePocketPlan != null ? plan.safePocketPlan.family : null,
                WorldContentSocket.ContentKind.ConstructionPoint => plan.buildSocketPlan != null ? plan.buildSocketPlan.family : null,
                WorldContentSocket.ContentKind.PowerPoint => plan.powerSpinePlan != null ? plan.powerSpinePlan.family : null,
                WorldContentSocket.ContentKind.ServiceTarget => plan.serviceChokePlan != null ? plan.serviceChokePlan.family : null,
                WorldContentSocket.ContentKind.NavigationMarker => plan.routeAnchorPlan != null ? plan.routeAnchorPlan.family : null,
                WorldContentSocket.ContentKind.HazardPoint => plan.hazardGatePlan != null ? plan.hazardGatePlan.family : null,
                WorldContentSocket.ContentKind.CombatPoint => plan.hazardGatePlan != null && plan.hazardGatePlan.family != null ? plan.hazardGatePlan.family : plan.rareObjectivePlan != null ? plan.rareObjectivePlan.family : null,
                WorldContentSocket.ContentKind.Landmark => plan.heroFamily != null ? plan.heroFamily : plan.rareObjectivePlan != null ? plan.rareObjectivePlan.family : null,
                _ => plan.nearPlan != null && plan.nearPlan.primaryFamily != null ? plan.nearPlan.primaryFamily : null
            };
        }

        private WorldPrefabFamilyProfile ResolveFamilyFromZoneProfile(WorldZoneAnchor zone, WorldContentSocket socket)
        {
            if (zone == null || zone.Profile == null)
                return null;

            if (socket == null)
                return zone.Profile.midVisualProfile;

            return socket.Kind switch
            {
                WorldContentSocket.ContentKind.ResourcePickup => zone.Profile.nearInteractiveProfile,
                WorldContentSocket.ContentKind.ResourceNode => zone.Profile.nearInteractiveProfile,
                WorldContentSocket.ContentKind.FabricationStation => zone.Profile.nearInteractiveProfile,
                WorldContentSocket.ContentKind.ConstructionPoint => zone.Profile.nearInteractiveProfile,
                WorldContentSocket.ContentKind.PowerPoint => zone.Profile.midVisualProfile,
                WorldContentSocket.ContentKind.ServiceTarget => zone.Profile.midVisualProfile,
                WorldContentSocket.ContentKind.NavigationMarker => zone.Profile.midVisualProfile,
                WorldContentSocket.ContentKind.HazardPoint => zone.Profile.midVisualProfile,
                WorldContentSocket.ContentKind.CombatPoint => zone.Profile.midVisualProfile,
                WorldContentSocket.ContentKind.Landmark => zone.Profile.farSilhouetteProfile,
                _ => zone.Profile.midVisualProfile
            };
        }

        private WorldPrefabFamilyProfile MapSocketKindToDefaultFamily(WorldContentSocket socket)
        {
            string familyId = socket != null ? socket.Kind switch
            {
                WorldContentSocket.ContentKind.ResourcePickup => "family.pocket.resource",
                WorldContentSocket.ContentKind.ResourceNode => "family.pocket.resource",
                WorldContentSocket.ContentKind.FabricationStation => "family.pocket.safe",
                WorldContentSocket.ContentKind.ConstructionPoint => "family.ruin.module.single",
                WorldContentSocket.ContentKind.PowerPoint => "family.route.power",
                WorldContentSocket.ContentKind.ServiceTarget => "family.service.scar",
                WorldContentSocket.ContentKind.NavigationMarker => "family.cave.entrance",
                WorldContentSocket.ContentKind.HazardPoint => "family.pocket.hazard",
                WorldContentSocket.ContentKind.CombatPoint => "family.creature.spawn.predator",
                WorldContentSocket.ContentKind.Landmark => "family.landmark.spire",
                _ => "family.rock.cluster.medium"
            } : "family.rock.cluster.medium";

            return FindFamilyByKey(familyId);
        }

        private WorldPrefabFamilyProfile FindFamilyByKey(string familyKey)
        {
            if (string.IsNullOrWhiteSpace(familyKey))
                return null;

            for (int i = 0; i < families.Count; i++)
            {
                WorldPrefabFamilyProfile family = families[i];
                if (family == null)
                    continue;

                if (string.Equals(family.familyId, familyKey, StringComparison.Ordinal) ||
                    string.Equals(family.familyLabel, familyKey, StringComparison.Ordinal))
                    return family;
            }

            return null;
        }

        private static WorldPrefabFamilyProfile.VariantEntry ResolveVariant(WorldPrefabFamilyProfile family, string seedKey)
        {
            if (family == null || family.variants == null || family.variants.Length == 0)
                return null;

            int totalWeight = 0;
            for (int i = 0; i < family.variants.Length; i++)
            {
                WorldPrefabFamilyProfile.VariantEntry entry = family.variants[i];
                if (entry == null)
                    continue;

                totalWeight += Mathf.Max(1, entry.weight);
            }

            if (totalWeight <= 0)
                return family.variants[0];

            int pick = Mathf.Abs(ComputeStableHash(seedKey)) % totalWeight;
            for (int i = 0; i < family.variants.Length; i++)
            {
                WorldPrefabFamilyProfile.VariantEntry entry = family.variants[i];
                if (entry == null)
                    continue;

                pick -= Mathf.Max(1, entry.weight);
                if (pick < 0)
                    return entry;
            }

            return family.variants[family.variants.Length - 1];
        }

        private static string BuildReason(
            WorldZoneAnchor zone,
            WorldContentSocket socket,
            HectonBiomeFamilyProfile biomeFamily,
            WorldProceduralPlacementRule rule,
            WorldPrefabFamilyProfile family,
            string source)
        {
            string biomeLabel = biomeFamily != null ? biomeFamily.familyLabel : "generic biome";
            string zoneLabel = zone != null ? zone.ZoneLabel : "unscoped zone";
            string socketLabel = socket != null ? socket.SocketLabel : "generic socket";
            string familyLabel = family != null ? family.familyLabel : "generic family";

            if (rule != null)
                return $"{source}: {rule.ruleLabel} matched {socketLabel} in {zoneLabel} for {biomeLabel} and resolved {familyLabel}.";

            return $"{source}: {socketLabel} in {zoneLabel} falls back to {familyLabel} for {biomeLabel}.";
        }

        private bool NeedsAutoResolve()
        {
            return playerTransform == null ||
                   worldZoneDirector == null ||
                   worldContentDirector == null ||
                   biomeMatrixDirector == null;
        }

        private void ResolveReferences(bool force = false)
        {
            if (!force && !NeedsAutoResolve())
                return;

            float now = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
            if (!force && now < _nextAutoResolveAttemptTime)
                return;

            _nextAutoResolveAttemptTime = now + Mathf.Max(0f, autoResolveRetryInterval);

            WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);
            WorldRuntimeReferenceUtility.TryResolveWorldZoneDirector(ref worldZoneDirector);
            WorldRuntimeReferenceUtility.TryResolveWorldContentDirector(ref worldContentDirector);
            WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref biomeMatrixDirector);
        }

        private void UpdateDiagnostics(WorldZoneAnchor zone, WorldContentSocket socket, ProceduralSelection selection, int resolvedSocketCount)
        {
            _debugRuleCount = rules.Count;
            _debugFamilyCount = families.Count;
            _debugResolvedSocketCount = resolvedSocketCount;
            _debugCurrentZone = zone != null ? zone.ZoneLabel : "None";
            _debugCurrentBiome = zone != null && zone.DominantBiomeFamily != null
                ? zone.DominantBiomeFamily.familyLabel
                : biomeMatrixDirector != null && biomeMatrixDirector.CurrentFamilyProfile != null
                    ? biomeMatrixDirector.CurrentFamilyProfile.familyLabel
                    : "None";
            _debugCurrentSocket = socket != null ? socket.SocketLabel : "None";
            _debugPrimaryRule = selection.Rule != null ? selection.Rule.ruleLabel : "None";
            _debugPrimaryFamily = selection.Family != null ? selection.Family.familyLabel : "None";
            _debugPrimarySource = string.IsNullOrWhiteSpace(selection.Source) ? "None" : selection.Source;
            _debugPrimaryVariant = string.IsNullOrWhiteSpace(selection.VariantId) ? "None" : selection.VariantId;
            _debugPrimaryDomain = selection.Family != null ? selection.Family.proceduralDomain.ToString() : WorldPrefabFamilyProfile.ProceduralDomain.Generic.ToString();
            _debugPrimaryPlacementMode = selection.Family != null ? selection.Family.placementMode.ToString() : WorldPrefabFamilyProfile.PlacementMode.Scatter.ToString();
            _debugPrimaryHeatmap = string.IsNullOrWhiteSpace(selection.HeatmapChannel) ? "None" : selection.HeatmapChannel;
            _debugPrimaryIntent = string.IsNullOrWhiteSpace(selection.Intent) ? "None" : selection.Intent;
            _debugPrimaryReason = string.IsNullOrWhiteSpace(selection.Reason) ? "None" : selection.Reason;
            _debugPrimaryScore = Mathf.Max(0f, selection.Score);
            _debugPrimaryMinCount = selection.MinCount;
            _debugPrimaryMaxCount = selection.MaxCount;
            _debugPrimarySpacingMeters = Mathf.Max(0f, selection.MinSpacingMeters);
            _debugPrimaryClusterRadiusMeters = Mathf.Max(0f, selection.ClusterRadiusMeters);
        }

        private static int ComputeStableHash(string value)
        {
            if (string.IsNullOrEmpty(value))
                return 17;

            unchecked
            {
                int hash = 23;
                for (int i = 0; i < value.Length; i++)
                    hash = (hash * 31) + value[i];

                return hash;
            }
        }

        private readonly struct ProceduralSelection
        {
            public ProceduralSelection(
                WorldProceduralPlacementRule rule,
                WorldPrefabFamilyProfile family,
                string variantId,
                string source,
                string reason,
                string intent,
                string heatmapChannel,
                float score,
                int minCount,
                int maxCount,
                float minSpacingMeters,
                float clusterRadiusMeters)
            {
                Rule = rule;
                Family = family;
                VariantId = variantId;
                Source = source;
                Reason = reason;
                Intent = intent;
                HeatmapChannel = heatmapChannel;
                Score = score;
                MinCount = minCount;
                MaxCount = maxCount;
                MinSpacingMeters = minSpacingMeters;
                ClusterRadiusMeters = clusterRadiusMeters;
            }

            public bool HasValue => Family != null;
            public WorldProceduralPlacementRule Rule { get; }
            public WorldPrefabFamilyProfile Family { get; }
            public string VariantId { get; }
            public string Source { get; }
            public string Reason { get; }
            public string Intent { get; }
            public string HeatmapChannel { get; }
            public float Score { get; }
            public int MinCount { get; }
            public int MaxCount { get; }
            public float MinSpacingMeters { get; }
            public float ClusterRadiusMeters { get; }
        }
    }
}
