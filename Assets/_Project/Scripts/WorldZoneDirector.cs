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

        [Header("Runtime Auto Resolve")]
        [SerializeField, Min(0f)] private float autoResolveRetryInterval = 1f;

        [Header("Diagnostics")]
        [SerializeField] private string _debugCurrentZoneId = "zone.none";
        [SerializeField] private string _debugCurrentZoneLabel = "None";
        [SerializeField] private string _debugCurrentZoneKind = "Generic";
        [SerializeField] private string _debugCurrentZoneTier = "Starter";
        [SerializeField] private string _debugZonePlan = "None";
        [SerializeField] private string _debugExpeditionLoop = "None";
        [SerializeField] private string _debugSandboxAttraction = "None";
        [SerializeField] private string _debugMotivationProfile = "None";
        [SerializeField] private string _debugSurvivalNeed = "None";
        [SerializeField] private string _debugResourceNeed = "None";
        [SerializeField] private string _debugEngineeringNeed = "None";
        [SerializeField] private string _debugCuriosityPull = "None";
        [SerializeField] private string _debugStoryPull = "None";
        [SerializeField] private string _debugRareValuePull = "None";
        [SerializeField] private string _debugHeroFamily = "None";
        [SerializeField] private string _debugNearFamily = "None";
        [SerializeField] private string _debugMidFamily = "None";
        [SerializeField] private string _debugFarFamily = "None";
        [SerializeField] private string _debugResourcePocketFamily = "None";
        [SerializeField] private string _debugNodeClusterFamily = "None";
        [SerializeField] private string _debugSafePocketFamily = "None";
        [SerializeField] private string _debugBuildSocketFamily = "None";
        [SerializeField] private string _debugPowerSpineFamily = "None";
        [SerializeField] private string _debugServiceChokeFamily = "None";
        [SerializeField] private string _debugRouteAnchorFamily = "None";
        [SerializeField] private string _debugHazardGateFamily = "None";
        [SerializeField] private string _debugRareObjectiveFamily = "None";
        [SerializeField] private string _debugDominantBiome = "None";
        [SerializeField] private string _debugDominantBiomeFamily = "None";
        [SerializeField] private string _debugSecondaryBiome = "None";
        [SerializeField] private string _debugSecondaryBiomeFamily = "None";
        [SerializeField] private string _debugDominantVisitPurpose = "None";
        [SerializeField] private string _debugDominantLandmark = "None";
        [SerializeField] private string _debugDominantRisk = "None";
        [SerializeField] private string _debugDominantEarlyFarm = "None";
        [SerializeField] private string _debugDominantLateReturn = "None";
        [SerializeField] private string _debugPocketResource = "None";
        [SerializeField] private string _debugNodeResource = "None";
        [SerializeField] private string _debugSafePocketResource = "None";
        [SerializeField] private string _debugRareObjectiveResource = "None";
        [SerializeField] private string _debugDominantLandmarkRole = "None";
        [SerializeField] private string _debugDominantExtraction = "None";
        [SerializeField] private string _debugDominantLandmarkGuidance = "None";
        [SerializeField] private int _debugLoosePickupBias;
        [SerializeField] private int _debugNodeBias;
        [SerializeField] private int _debugSalvageBias;
        [SerializeField] private int _debugCommonBias;
        [SerializeField] private int _debugUncommonBias;
        [SerializeField] private int _debugRareBias;
        [SerializeField] private int _debugBlendedLoosePickupBias;
        [SerializeField] private int _debugBlendedNodeBias;
        [SerializeField] private int _debugBlendedSalvageBias;
        [SerializeField] private int _debugBlendedCommonBias;
        [SerializeField] private int _debugBlendedUncommonBias;
        [SerializeField] private int _debugBlendedRareBias;
        [SerializeField] private float _debugEffectiveScavengeScale = 1f;
        [SerializeField] private float _debugEffectiveSpawnScale = 1f;
        [SerializeField] private float _debugEffectiveColliderRadiusScale = 1f;
        [SerializeField] private float _debugEffectiveColliderOpsScale = 1f;
        [SerializeField] private float _debugEffectiveNearSliceScale = 1f;
        [SerializeField] private float _debugEffectiveMidSliceScale = 1f;
        [SerializeField] private int _debugEffectiveNearDensity;
        [SerializeField] private int _debugEffectiveMidDensity;
        [SerializeField] private int _debugEffectiveFarDensity;
        [SerializeField] private string _debugZoneRewardRhythm = "None";
        [SerializeField] private string _debugZoneRouteRhythm = "None";
        [SerializeField] private string _debugZoneSafePocketRhythm = "None";
        [SerializeField] private string _debugLoopEntryBeat = "None";
        [SerializeField] private string _debugLoopRoutineBeat = "None";
        [SerializeField] private string _debugLoopReliefBeat = "None";
        [SerializeField] private string _debugLoopPressureBeat = "None";
        [SerializeField] private string _debugLoopPayoffBeat = "None";
        [SerializeField] private string _debugLoopExitBeat = "None";
        [SerializeField] private string _debugLoopFreedomRule = "None";
        [SerializeField] private string _debugLoopSoftPull = "None";
        [SerializeField] private string _debugLoopDetourRule = "None";
        [SerializeField] private string _debugLoopMasteryRule = "None";
        [SerializeField] private string _debugSandboxEntryRead = "None";
        [SerializeField] private string _debugSandboxAmbientValue = "None";
        [SerializeField] private string _debugSandboxDetourValue = "None";
        [SerializeField] private string _debugSandboxShelterRead = "None";
        [SerializeField] private string _debugSandboxPressureRead = "None";
        [SerializeField] private string _debugSandboxDeepLure = "None";
        [SerializeField] private string _debugSandboxReturnValue = "None";
        [SerializeField] private string _debugSandboxFreedomRule = "None";
        [SerializeField] private string _debugSandboxCrosslinkRule = "None";
        [SerializeField] private string _debugSandboxDangerRule = "None";
        [SerializeField] private string _debugBlendedRewardRhythm = "None";
        [SerializeField] private string _debugBlendedRouteRhythm = "None";
        [SerializeField] private string _debugBlendedSafePocketRhythm = "None";
        [SerializeField] private string _debugBlendedExtraction = "None";
        [SerializeField] private string _debugBlendedLandmarkGuidance = "None";
        [SerializeField] private string _debugNearestZone = "None";
        [SerializeField] private float _debugCurrentZoneWeight;
        [SerializeField] private float _debugNearestZoneWeight;
        [SerializeField] private string _debugSecondaryZone = "None";
        [SerializeField] private float _debugSecondaryZoneWeight;
        [SerializeField] private float _debugZoneBlendFactor;
        [SerializeField] private int _debugZoneCount;
        [SerializeField] private bool _debugApplied;

        private readonly List<WorldZoneAnchor> _anchors = new List<WorldZoneAnchor>(32);
        private bool _registeredToTickManager;
        private WorldZoneAnchor _currentZone;
        private WorldZoneAnchor _secondaryZone;
        private float _currentBlendFactor;
        private float _nextAutoResolveAttemptTime = float.NegativeInfinity;

        public WorldZoneAnchor CurrentZone => _currentZone;
        public WorldZoneAnchor SecondaryZone => _secondaryZone;
        public float CurrentBlendFactor => _currentBlendFactor;

        private void Awake()
        {
            ResolvePlayer(force: true);
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
            float nearestWeight = 0f;
            WorldZoneAnchor bestCandidate = null;
            float bestCandidateWeight = 0f;
            WorldZoneAnchor secondaryCandidate = null;
            float secondaryCandidateWeight = 0f;

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
                float activationWeight = anchor.EvaluateActivationWeight(playerPosition);
                float holdWeight = anchor.EvaluateHoldWeight(playerPosition);

                if (_currentZone == anchor && insideHold)
                {
                    float holdCandidateWeight = holdWeight + Mathf.Max(0f, anchor.Priority) * 0.05f;
                    PromoteCandidate(
                        anchor,
                        holdCandidateWeight,
                        ref bestCandidate,
                        ref bestCandidateWeight,
                        ref secondaryCandidate,
                        ref secondaryCandidateWeight,
                        playerPosition);
                    continue;
                }

                if (!insideActivation)
                    continue;

                float candidateWeight = activationWeight + Mathf.Max(0f, anchor.Priority) * 0.05f;
                PromoteCandidate(
                    anchor,
                    candidateWeight,
                    ref bestCandidate,
                    ref bestCandidateWeight,
                    ref secondaryCandidate,
                    ref secondaryCandidateWeight,
                    playerPosition);

                if (anchor == nearest)
                    nearestWeight = activationWeight;
            }

            _currentZone = bestCandidate ?? nearest;
            float blendFactor = EvaluateBlendFactor(bestCandidateWeight, secondaryCandidateWeight);
            _secondaryZone = secondaryCandidate;
            _currentBlendFactor = blendFactor;
            ApplyZoneProfile(_currentZone, secondaryCandidate, blendFactor);
            _debugNearestZone = nearest != null ? nearest.ZoneLabel : "None";
            _debugNearestZoneWeight = nearestWeight;
            _debugCurrentZoneWeight = _currentZone != null ? (_currentZone == bestCandidate ? bestCandidateWeight : _currentZone.EvaluateActivationWeight(playerPosition)) : 0f;
            _debugSecondaryZone = secondaryCandidate != null ? secondaryCandidate.ZoneLabel : "None";
            _debugSecondaryZoneWeight = secondaryCandidateWeight;
            _debugZoneBlendFactor = blendFactor;
            _debugApplied = _currentZone != null;
            UpdateDiagnostics();
        }

        private int Compare(WorldZoneAnchor a, float aWeight, WorldZoneAnchor b, float bWeight, Vector3 playerPosition)
        {
            int weightCompare = bWeight.CompareTo(aWeight);
            if (weightCompare != 0)
                return weightCompare;

            int priorityCompare = b.Priority.CompareTo(a.Priority);
            if (priorityCompare != 0)
                return priorityCompare;

            return a.GetFlatDistance(playerPosition).CompareTo(b.GetFlatDistance(playerPosition));
        }

        private bool NeedsAutoResolve()
        {
            return playerTransform == null ||
                   scatterBudgetController == null ||
                   worldSliceDirector == null;
        }

        private void ResolvePlayer(bool force = false)
        {
            if (!force && !NeedsAutoResolve())
                return;

            float now = Time.realtimeSinceStartup;
            if (!force && now < _nextAutoResolveAttemptTime)
                return;

            _nextAutoResolveAttemptTime = now + Mathf.Max(0f, autoResolveRetryInterval);

            if (playerTransform == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player == null)
                    player = GameObject.Find("Player");

                if (player != null)
                    playerTransform = player.transform;
            }

            if (scatterBudgetController == null)
                scatterBudgetController = FindAnyObjectByType<ScatterBudgetController>();

            if (worldSliceDirector == null)
                worldSliceDirector = FindAnyObjectByType<WorldSliceDirector>();
        }

        private void ApplyZoneProfile(WorldZoneAnchor primaryZone, WorldZoneAnchor secondaryZone, float blendFactor)
        {
            float scavengeScale = EvaluateZoneScavengeScale(primaryZone);
            float spawnScale = EvaluateZoneSpawnScale(primaryZone);
            float colliderRadiusScale = EvaluateZoneColliderRadiusScale(primaryZone);
            float colliderOpsScale = EvaluateZoneColliderOpsScale(primaryZone);
            float nearSliceScale = EvaluateZoneNearSliceScale(primaryZone);
            float midSliceScale = EvaluateZoneMidSliceScale(primaryZone);

            if (secondaryZone != null && blendFactor > 0.001f)
            {
                scavengeScale = Mathf.Lerp(scavengeScale, EvaluateZoneScavengeScale(secondaryZone), blendFactor);
                spawnScale = Mathf.Lerp(spawnScale, EvaluateZoneSpawnScale(secondaryZone), blendFactor);
                colliderRadiusScale = Mathf.Lerp(colliderRadiusScale, EvaluateZoneColliderRadiusScale(secondaryZone), blendFactor);
                colliderOpsScale = Mathf.Lerp(colliderOpsScale, EvaluateZoneColliderOpsScale(secondaryZone), blendFactor);
                nearSliceScale = Mathf.Lerp(nearSliceScale, EvaluateZoneNearSliceScale(secondaryZone), blendFactor);
                midSliceScale = Mathf.Lerp(midSliceScale, EvaluateZoneMidSliceScale(secondaryZone), blendFactor);
            }

            _debugEffectiveScavengeScale = scavengeScale;
            _debugEffectiveSpawnScale = spawnScale;
            _debugEffectiveColliderRadiusScale = colliderRadiusScale;
            _debugEffectiveColliderOpsScale = colliderOpsScale;
            _debugEffectiveNearSliceScale = nearSliceScale;
            _debugEffectiveMidSliceScale = midSliceScale;

            if (scatterBudgetController != null)
                scatterBudgetController.SetZoneScales(scavengeScale, spawnScale, colliderRadiusScale, colliderOpsScale);

            if (worldSliceDirector != null)
                worldSliceDirector.SetZoneScales(nearSliceScale, midSliceScale);
        }

        private float EvaluateZoneScavengeScale(WorldZoneAnchor zone)
        {
            WorldZoneProfile profile = zone != null ? zone.Profile : null;
            HectonBiomeMatrixProfile biome = zone != null ? zone.DominantMatrixBiome : null;
            float scale = profile != null ? profile.scavengeRadiusScale : 1f;
            return scale * EvaluateBiomeScavengeScale(zone, biome);
        }

        private float EvaluateZoneSpawnScale(WorldZoneAnchor zone)
        {
            WorldZoneProfile profile = zone != null ? zone.Profile : null;
            HectonBiomeMatrixProfile biome = zone != null ? zone.DominantMatrixBiome : null;
            float scale = profile != null ? profile.spawnScale : 1f;
            return scale * EvaluateBiomeSpawnScale(zone, biome);
        }

        private float EvaluateZoneColliderRadiusScale(WorldZoneAnchor zone)
        {
            WorldZoneProfile profile = zone != null ? zone.Profile : null;
            HectonBiomeMatrixProfile biome = zone != null ? zone.DominantMatrixBiome : null;
            float scale = profile != null ? profile.colliderRadiusScale : 1f;
            return scale * EvaluateBiomeColliderRadiusScale(zone, biome);
        }

        private float EvaluateZoneColliderOpsScale(WorldZoneAnchor zone)
        {
            WorldZoneProfile profile = zone != null ? zone.Profile : null;
            HectonBiomeMatrixProfile biome = zone != null ? zone.DominantMatrixBiome : null;
            float scale = profile != null ? profile.colliderOpsScale : 1f;
            return scale * EvaluateBiomeColliderOpsScale(zone, biome);
        }

        private float EvaluateZoneNearSliceScale(WorldZoneAnchor zone)
        {
            WorldZoneProfile profile = zone != null ? zone.Profile : null;
            HectonBiomeMatrixProfile biome = zone != null ? zone.DominantMatrixBiome : null;
            float scale = profile != null ? profile.sliceNearScale : 1f;
            return scale * EvaluateBiomeNearSliceScale(zone, biome);
        }

        private float EvaluateZoneMidSliceScale(WorldZoneAnchor zone)
        {
            WorldZoneProfile profile = zone != null ? zone.Profile : null;
            HectonBiomeMatrixProfile biome = zone != null ? zone.DominantMatrixBiome : null;
            float scale = profile != null ? profile.sliceMidScale : 1f;
            return scale * EvaluateBiomeMidSliceScale(zone, biome);
        }

        private void PromoteCandidate(
            WorldZoneAnchor candidate,
            float candidateWeight,
            ref WorldZoneAnchor bestCandidate,
            ref float bestCandidateWeight,
            ref WorldZoneAnchor secondaryCandidate,
            ref float secondaryCandidateWeight,
            Vector3 playerPosition)
        {
            if (candidate == null || candidateWeight <= 0f)
                return;

            if (bestCandidate == null || Compare(candidate, candidateWeight, bestCandidate, bestCandidateWeight, playerPosition) < 0)
            {
                secondaryCandidate = bestCandidate;
                secondaryCandidateWeight = bestCandidateWeight;
                bestCandidate = candidate;
                bestCandidateWeight = candidateWeight;
                return;
            }

            if (secondaryCandidate == null || Compare(candidate, candidateWeight, secondaryCandidate, secondaryCandidateWeight, playerPosition) < 0)
            {
                secondaryCandidate = candidate;
                secondaryCandidateWeight = candidateWeight;
            }
        }

        private static float EvaluateBlendFactor(float primaryWeight, float secondaryWeight)
        {
            if (primaryWeight <= 0f || secondaryWeight <= 0f)
                return 0f;

            float closeness = Mathf.Clamp01(1f - Mathf.Abs(primaryWeight - secondaryWeight));
            return Mathf.Clamp01(closeness * Mathf.InverseLerp(0.15f, 0.75f, secondaryWeight));
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
                _debugExpeditionLoop = "None";
                _debugSandboxAttraction = "None";
                _debugMotivationProfile = "None";
                _debugSurvivalNeed = "None";
                _debugResourceNeed = "None";
                _debugEngineeringNeed = "None";
                _debugCuriosityPull = "None";
                _debugStoryPull = "None";
                _debugRareValuePull = "None";
                _debugHeroFamily = "None";
                _debugNearFamily = "None";
                _debugMidFamily = "None";
                _debugFarFamily = "None";
                _debugResourcePocketFamily = "None";
                _debugNodeClusterFamily = "None";
                _debugSafePocketFamily = "None";
                _debugBuildSocketFamily = "None";
                _debugPowerSpineFamily = "None";
                _debugServiceChokeFamily = "None";
                _debugRouteAnchorFamily = "None";
                _debugHazardGateFamily = "None";
                _debugRareObjectiveFamily = "None";
                _debugDominantBiome = "None";
                _debugDominantBiomeFamily = "None";
                _debugSecondaryBiome = "None";
                _debugSecondaryBiomeFamily = "None";
                _debugDominantVisitPurpose = "None";
                _debugDominantLandmark = "None";
                _debugDominantRisk = "None";
                _debugDominantEarlyFarm = "None";
                _debugDominantLateReturn = "None";
                _debugPocketResource = "None";
                _debugNodeResource = "None";
                _debugSafePocketResource = "None";
                _debugRareObjectiveResource = "None";
                _debugDominantLandmarkRole = "None";
                _debugDominantExtraction = "None";
                _debugDominantLandmarkGuidance = "None";
                _debugLoosePickupBias = 0;
                _debugNodeBias = 0;
                _debugSalvageBias = 0;
                _debugCommonBias = 0;
                _debugUncommonBias = 0;
                _debugRareBias = 0;
                _debugBlendedLoosePickupBias = 0;
                _debugBlendedNodeBias = 0;
                _debugBlendedSalvageBias = 0;
                _debugBlendedCommonBias = 0;
                _debugBlendedUncommonBias = 0;
                _debugBlendedRareBias = 0;
                _debugEffectiveScavengeScale = 1f;
                _debugEffectiveSpawnScale = 1f;
                _debugEffectiveColliderRadiusScale = 1f;
                _debugEffectiveColliderOpsScale = 1f;
                _debugEffectiveNearSliceScale = 1f;
                _debugEffectiveMidSliceScale = 1f;
                _debugEffectiveNearDensity = 0;
                _debugEffectiveMidDensity = 0;
                _debugEffectiveFarDensity = 0;
                _debugZoneRewardRhythm = "None";
                _debugZoneRouteRhythm = "None";
                _debugZoneSafePocketRhythm = "None";
                _debugLoopEntryBeat = "None";
                _debugLoopRoutineBeat = "None";
                _debugLoopReliefBeat = "None";
                _debugLoopPressureBeat = "None";
                _debugLoopPayoffBeat = "None";
                _debugLoopExitBeat = "None";
                _debugLoopFreedomRule = "None";
                _debugLoopSoftPull = "None";
                _debugLoopDetourRule = "None";
                _debugLoopMasteryRule = "None";
                _debugSandboxEntryRead = "None";
                _debugSandboxAmbientValue = "None";
                _debugSandboxDetourValue = "None";
                _debugSandboxShelterRead = "None";
                _debugSandboxPressureRead = "None";
                _debugSandboxDeepLure = "None";
                _debugSandboxReturnValue = "None";
                _debugSandboxFreedomRule = "None";
                _debugSandboxCrosslinkRule = "None";
                _debugSandboxDangerRule = "None";
                _debugBlendedRewardRhythm = "None";
                _debugBlendedRouteRhythm = "None";
                _debugBlendedSafePocketRhythm = "None";
                _debugBlendedExtraction = "None";
                _debugBlendedLandmarkGuidance = "None";
                return;
            }

            HectonBiomeMatrixProfile biome = _currentZone.DominantMatrixBiome;
            HectonBiomeFamilyProfile biomeFamily = _currentZone.DominantBiomeFamily;
            HectonBiomeMatrixProfile secondaryBiome = _secondaryZone != null ? _secondaryZone.DominantMatrixBiome : null;
            HectonBiomeFamilyProfile secondaryBiomeFamily = _secondaryZone != null ? _secondaryZone.DominantBiomeFamily : null;
            WorldZonePlanProfile zonePlan = _currentZone.Profile != null ? _currentZone.Profile.zonePlanProfile : null;
            WorldExpeditionLoopProfile expeditionLoop = _currentZone.Profile != null ? _currentZone.Profile.expeditionLoopProfile : null;
            WorldSandboxAttractionProfile sandboxAttraction = _currentZone.Profile != null ? _currentZone.Profile.sandboxAttractionProfile : null;
            WorldMotivationProfile motivation = _currentZone.Profile != null ? _currentZone.Profile.motivationProfile : null;
            _debugCurrentZoneId = _currentZone.ZoneId;
            _debugCurrentZoneLabel = _currentZone.ZoneLabel;
            _debugCurrentZoneKind = _currentZone.Kind.ToString();
            _debugCurrentZoneTier = _currentZone.Tier.ToString();
            _debugZonePlan = zonePlan != null
                ? zonePlan.planLabel
                : "None";
            _debugExpeditionLoop = expeditionLoop != null ? expeditionLoop.profileLabel : "None";
            _debugSandboxAttraction = sandboxAttraction != null ? sandboxAttraction.profileLabel : "None";
            _debugMotivationProfile = motivation != null ? motivation.profileLabel : "None";
            _debugSurvivalNeed = motivation != null ? motivation.survivalNeed : "None";
            _debugResourceNeed = motivation != null ? motivation.resourceNeed : "None";
            _debugEngineeringNeed = motivation != null ? motivation.engineeringNeed : "None";
            _debugCuriosityPull = motivation != null ? motivation.curiosityPull : "None";
            _debugStoryPull = motivation != null ? motivation.storyPull : "None";
            _debugRareValuePull = motivation != null ? motivation.rareValuePull : "None";
            _debugHeroFamily = _currentZone.Profile != null
                && zonePlan != null
                && zonePlan.heroFamily != null
                ? zonePlan.heroFamily.familyLabel
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
            _debugResourcePocketFamily = zonePlan != null && zonePlan.resourcePocketPlan != null && zonePlan.resourcePocketPlan.family != null ? zonePlan.resourcePocketPlan.family.familyLabel : "None";
            _debugNodeClusterFamily = zonePlan != null && zonePlan.nodeClusterPlan != null && zonePlan.nodeClusterPlan.family != null ? zonePlan.nodeClusterPlan.family.familyLabel : "None";
            _debugSafePocketFamily = zonePlan != null && zonePlan.safePocketPlan != null && zonePlan.safePocketPlan.family != null ? zonePlan.safePocketPlan.family.familyLabel : "None";
            _debugBuildSocketFamily = zonePlan != null && zonePlan.buildSocketPlan != null && zonePlan.buildSocketPlan.family != null ? zonePlan.buildSocketPlan.family.familyLabel : "None";
            _debugPowerSpineFamily = zonePlan != null && zonePlan.powerSpinePlan != null && zonePlan.powerSpinePlan.family != null ? zonePlan.powerSpinePlan.family.familyLabel : "None";
            _debugServiceChokeFamily = zonePlan != null && zonePlan.serviceChokePlan != null && zonePlan.serviceChokePlan.family != null ? zonePlan.serviceChokePlan.family.familyLabel : "None";
            _debugRouteAnchorFamily = zonePlan != null && zonePlan.routeAnchorPlan != null && zonePlan.routeAnchorPlan.family != null ? zonePlan.routeAnchorPlan.family.familyLabel : "None";
            _debugHazardGateFamily = zonePlan != null && zonePlan.hazardGatePlan != null && zonePlan.hazardGatePlan.family != null ? zonePlan.hazardGatePlan.family.familyLabel : "None";
            _debugRareObjectiveFamily = zonePlan != null && zonePlan.rareObjectivePlan != null && zonePlan.rareObjectivePlan.family != null ? zonePlan.rareObjectivePlan.family.familyLabel : "None";
            _debugDominantBiome = biome != null ? biome.biomeName : "None";
            _debugDominantBiomeFamily = biomeFamily != null ? biomeFamily.familyLabel : "None";
            _debugSecondaryBiome = secondaryBiome != null ? secondaryBiome.biomeName : "None";
            _debugSecondaryBiomeFamily = secondaryBiomeFamily != null ? secondaryBiomeFamily.familyLabel : "None";
            _debugDominantVisitPurpose = biome != null && !string.IsNullOrWhiteSpace(biome.visitPurpose)
                ? biome.visitPurpose
                : "None";
            _debugDominantLandmark = biome != null && !string.IsNullOrWhiteSpace(biome.landmarkIdentity)
                ? biome.landmarkIdentity
                : "None";
            _debugDominantRisk = biome != null && !string.IsNullOrWhiteSpace(biome.riskSummary)
                ? biome.riskSummary
                : "None";
            _debugDominantEarlyFarm = biomeFamily != null && biomeFamily.resourcePlanProfile != null && !string.IsNullOrWhiteSpace(biomeFamily.resourcePlanProfile.earlyReasonToFarm)
                ? biomeFamily.resourcePlanProfile.earlyReasonToFarm
                : "None";
            _debugDominantLateReturn = biomeFamily != null && biomeFamily.resourcePlanProfile != null && !string.IsNullOrWhiteSpace(biomeFamily.resourcePlanProfile.lateReasonToReturn)
                ? biomeFamily.resourcePlanProfile.lateReasonToReturn
                : "None";
            _debugPocketResource = GetItemLabel(biomeFamily != null && biomeFamily.resourceChannelProfile != null ? biomeFamily.resourceChannelProfile.resourcePocketItem : null);
            _debugNodeResource = GetItemLabel(biomeFamily != null && biomeFamily.resourceChannelProfile != null ? biomeFamily.resourceChannelProfile.nodeClusterItem : null);
            _debugSafePocketResource = GetItemLabel(biomeFamily != null && biomeFamily.resourceChannelProfile != null ? biomeFamily.resourceChannelProfile.safePocketItem : null);
            _debugRareObjectiveResource = GetItemLabel(biomeFamily != null && biomeFamily.resourceChannelProfile != null ? biomeFamily.resourceChannelProfile.rareObjectiveRewardItem : null);
            _debugDominantLandmarkRole = biomeFamily != null && biomeFamily.landmarkPlanProfile != null && !string.IsNullOrWhiteSpace(biomeFamily.landmarkPlanProfile.dominantLandmarkRole)
                ? biomeFamily.landmarkPlanProfile.dominantLandmarkRole
                : "None";
            _debugDominantExtraction = biome != null && !string.IsNullOrWhiteSpace(biome.extractionFocus)
                ? biome.extractionFocus
                : "None";
            _debugDominantLandmarkGuidance = biome != null && !string.IsNullOrWhiteSpace(biome.landmarkGuidance)
                ? biome.landmarkGuidance
                : "None";
            _debugLoosePickupBias = biome != null ? biome.loosePickupBias : 0;
            _debugNodeBias = biome != null ? biome.nodeExtractionBias : 0;
            _debugSalvageBias = biome != null ? biome.salvageBias : 0;
            _debugCommonBias = biome != null ? biome.commonResourceBias : 0;
            _debugUncommonBias = biome != null ? biome.uncommonResourceBias : 0;
            _debugRareBias = biome != null ? biome.rareResourceBias : 0;
            _debugBlendedLoosePickupBias = BlendBias(biome != null ? biome.loosePickupBias : 0, secondaryBiome != null ? secondaryBiome.loosePickupBias : 0, _currentBlendFactor);
            _debugBlendedNodeBias = BlendBias(biome != null ? biome.nodeExtractionBias : 0, secondaryBiome != null ? secondaryBiome.nodeExtractionBias : 0, _currentBlendFactor);
            _debugBlendedSalvageBias = BlendBias(biome != null ? biome.salvageBias : 0, secondaryBiome != null ? secondaryBiome.salvageBias : 0, _currentBlendFactor);
            _debugBlendedCommonBias = BlendBias(biome != null ? biome.commonResourceBias : 0, secondaryBiome != null ? secondaryBiome.commonResourceBias : 0, _currentBlendFactor);
            _debugBlendedUncommonBias = BlendBias(biome != null ? biome.uncommonResourceBias : 0, secondaryBiome != null ? secondaryBiome.uncommonResourceBias : 0, _currentBlendFactor);
            _debugBlendedRareBias = BlendBias(biome != null ? biome.rareResourceBias : 0, secondaryBiome != null ? secondaryBiome.rareResourceBias : 0, _currentBlendFactor);
            _debugEffectiveNearDensity = EvaluateBlendedEffectiveDensity(zonePlan != null ? zonePlan.nearPlan.targetDensity : 0, biome, secondaryBiome, _currentBlendFactor, DensityBand.Near);
            _debugEffectiveMidDensity = EvaluateBlendedEffectiveDensity(zonePlan != null ? zonePlan.midPlan.targetDensity : 0, biome, secondaryBiome, _currentBlendFactor, DensityBand.Mid);
            _debugEffectiveFarDensity = EvaluateBlendedEffectiveDensity(zonePlan != null ? zonePlan.farPlan.targetDensity : 0, biome, secondaryBiome, _currentBlendFactor, DensityBand.Far);
            _debugZoneRewardRhythm = BuildRewardRhythm(biome, biomeFamily);
            _debugZoneRouteRhythm = BuildRouteRhythm(biome, biomeFamily);
            _debugZoneSafePocketRhythm = BuildSafePocketRhythm(biome, biomeFamily);
            _debugLoopEntryBeat = expeditionLoop != null ? expeditionLoop.entryBeat : "None";
            _debugLoopRoutineBeat = expeditionLoop != null ? expeditionLoop.routineBeat : "None";
            _debugLoopReliefBeat = expeditionLoop != null ? expeditionLoop.reliefBeat : "None";
            _debugLoopPressureBeat = expeditionLoop != null ? expeditionLoop.pressureBeat : "None";
            _debugLoopPayoffBeat = expeditionLoop != null ? expeditionLoop.payoffBeat : "None";
            _debugLoopExitBeat = expeditionLoop != null ? expeditionLoop.exitBeat : "None";
            _debugLoopFreedomRule = expeditionLoop != null ? expeditionLoop.playerFreedomRule : "None";
            _debugLoopSoftPull = expeditionLoop != null ? expeditionLoop.softProgressionPull : "None";
            _debugLoopDetourRule = expeditionLoop != null ? expeditionLoop.optionalDetourRule : "None";
            _debugLoopMasteryRule = expeditionLoop != null ? expeditionLoop.masteryLogic : "None";
            _debugSandboxEntryRead = sandboxAttraction != null ? sandboxAttraction.entryRead : "None";
            _debugSandboxAmbientValue = sandboxAttraction != null ? sandboxAttraction.ambientValue : "None";
            _debugSandboxDetourValue = sandboxAttraction != null ? sandboxAttraction.detourValue : "None";
            _debugSandboxShelterRead = sandboxAttraction != null ? sandboxAttraction.shelterRead : "None";
            _debugSandboxPressureRead = sandboxAttraction != null ? sandboxAttraction.pressureRead : "None";
            _debugSandboxDeepLure = sandboxAttraction != null ? sandboxAttraction.deepLure : "None";
            _debugSandboxReturnValue = sandboxAttraction != null ? sandboxAttraction.returnValue : "None";
            _debugSandboxFreedomRule = sandboxAttraction != null ? sandboxAttraction.freedomRule : "None";
            _debugSandboxCrosslinkRule = sandboxAttraction != null ? sandboxAttraction.crosslinkRule : "None";
            _debugSandboxDangerRule = sandboxAttraction != null ? sandboxAttraction.dangerRule : "None";
            _debugBlendedRewardRhythm = BuildBlendedRhythm(_debugZoneRewardRhythm, BuildRewardRhythm(secondaryBiome, secondaryBiomeFamily), _currentBlendFactor);
            _debugBlendedRouteRhythm = BuildBlendedRhythm(_debugZoneRouteRhythm, BuildRouteRhythm(secondaryBiome, secondaryBiomeFamily), _currentBlendFactor);
            _debugBlendedSafePocketRhythm = BuildBlendedRhythm(_debugZoneSafePocketRhythm, BuildSafePocketRhythm(secondaryBiome, secondaryBiomeFamily), _currentBlendFactor);
            _debugBlendedExtraction = BuildBlendedDescriptor(_debugDominantExtraction, secondaryBiome != null && !string.IsNullOrWhiteSpace(secondaryBiome.extractionFocus) ? secondaryBiome.extractionFocus : "None", _currentBlendFactor);
            _debugBlendedLandmarkGuidance = BuildBlendedDescriptor(_debugDominantLandmarkGuidance, secondaryBiome != null && !string.IsNullOrWhiteSpace(secondaryBiome.landmarkGuidance) ? secondaryBiome.landmarkGuidance : "None", _currentBlendFactor);
        }

        private float EvaluateBiomeScavengeScale(WorldZoneAnchor zone, HectonBiomeMatrixProfile biome)
        {
            if (zone == null || biome == null)
                return 1f;

            float extractionPressure = Average(biome.loosePickupBias, biome.nodeExtractionBias, biome.salvageBias);
            float rewardPressure = Average(biome.commonResourceBias, biome.uncommonResourceBias, biome.rareResourceBias);

            if (zone.Kind == WorldZoneAnchor.ZoneKind.Resources)
                return Mathf.Lerp(0.9f, 1.16f, Mathf.InverseLerp(1f, 5f, Mathf.Max(extractionPressure, rewardPressure)));

            if (zone.Kind == WorldZoneAnchor.ZoneKind.Service || zone.Kind == WorldZoneAnchor.ZoneKind.Power)
                return Mathf.Lerp(0.94f, 1.08f, Mathf.InverseLerp(1f, 5f, biome.uncommonResourceBias));

            return 1f;
        }

        private float EvaluateBiomeSpawnScale(WorldZoneAnchor zone, HectonBiomeMatrixProfile biome)
        {
            if (zone == null || biome == null)
                return 1f;

            float value = zone.Kind switch
            {
                WorldZoneAnchor.ZoneKind.Resources => Mathf.Max(biome.commonResourceBias, biome.uncommonResourceBias),
                WorldZoneAnchor.ZoneKind.Progression => Mathf.Max(biome.rareResourceBias, biome.rewardPull),
                WorldZoneAnchor.ZoneKind.Navigation => Mathf.Max(biome.landmarkStrength, 6 - biome.routePressure),
                WorldZoneAnchor.ZoneKind.Combat => Mathf.Max(biome.survivalPressure, biome.routePressure),
                _ => Average(biome.commonResourceBias, biome.rewardPull)
            };

            return Mathf.Lerp(0.9f, 1.14f, Mathf.InverseLerp(1f, 5f, value));
        }

        private float EvaluateBiomeColliderRadiusScale(WorldZoneAnchor zone, HectonBiomeMatrixProfile biome)
        {
            if (zone == null || biome == null)
                return 1f;

            if (zone.Kind == WorldZoneAnchor.ZoneKind.Combat || zone.Kind == WorldZoneAnchor.ZoneKind.Service)
                return Mathf.Lerp(0.96f, 1.12f, Mathf.InverseLerp(1f, 5f, biome.survivalPressure));

            return 1f;
        }

        private float EvaluateBiomeColliderOpsScale(WorldZoneAnchor zone, HectonBiomeMatrixProfile biome)
        {
            if (zone == null || biome == null)
                return 1f;

            if (zone.Kind == WorldZoneAnchor.ZoneKind.Power || zone.Kind == WorldZoneAnchor.ZoneKind.Service)
                return Mathf.Lerp(0.96f, 1.1f, Mathf.InverseLerp(1f, 5f, Mathf.Max(biome.uncommonResourceBias, biome.routePressure)));

            return 1f;
        }

        private float EvaluateBiomeNearSliceScale(WorldZoneAnchor zone, HectonBiomeMatrixProfile biome)
        {
            if (zone == null || biome == null)
                return 1f;

            float emphasis = zone.Kind switch
            {
                WorldZoneAnchor.ZoneKind.Resources => Mathf.Max(biome.loosePickupBias, biome.commonResourceBias),
                WorldZoneAnchor.ZoneKind.Navigation => Mathf.Max(biome.landmarkStrength, 6 - biome.routePressure),
                WorldZoneAnchor.ZoneKind.Progression => Mathf.Max(biome.rewardPull, biome.rareResourceBias),
                _ => Mathf.Max(biome.landmarkStrength, biome.rewardPull)
            };

            return Mathf.Lerp(0.94f, 1.14f, Mathf.InverseLerp(1f, 5f, emphasis));
        }

        private float EvaluateBiomeMidSliceScale(WorldZoneAnchor zone, HectonBiomeMatrixProfile biome)
        {
            if (zone == null || biome == null)
                return 1f;

            float emphasis = zone.Kind switch
            {
                WorldZoneAnchor.ZoneKind.Navigation => Mathf.Max(biome.landmarkStrength, biome.routePressure),
                WorldZoneAnchor.ZoneKind.Progression => Mathf.Max(biome.rareResourceBias, biome.landmarkStrength),
                WorldZoneAnchor.ZoneKind.Combat => Mathf.Max(biome.survivalPressure, biome.routePressure),
                _ => Mathf.Max(biome.landmarkStrength, biome.uncommonResourceBias)
            };

            return Mathf.Lerp(0.96f, 1.18f, Mathf.InverseLerp(1f, 5f, emphasis));
        }

        private int EvaluateEffectiveDensity(int baseDensity, HectonBiomeMatrixProfile biome, DensityBand band)
        {
            if (baseDensity <= 0 || biome == null)
                return Mathf.Max(0, baseDensity);

            int emphasis = band switch
            {
                DensityBand.Near => Mathf.Max(biome.loosePickupBias, biome.commonResourceBias),
                DensityBand.Mid => Mathf.Max(biome.nodeExtractionBias, biome.uncommonResourceBias, biome.landmarkStrength),
                _ => Mathf.Max(biome.rareResourceBias, biome.landmarkStrength, biome.rewardPull)
            };

            float multiplier = Mathf.Lerp(0.8f, 1.35f, Mathf.InverseLerp(1f, 5f, emphasis));
            return Mathf.Max(1, Mathf.RoundToInt(baseDensity * multiplier));
        }

        private int EvaluateBlendedEffectiveDensity(
            int baseDensity,
            HectonBiomeMatrixProfile primaryBiome,
            HectonBiomeMatrixProfile secondaryBiome,
            float blendFactor,
            DensityBand band)
        {
            int primaryDensity = EvaluateEffectiveDensity(baseDensity, primaryBiome, band);
            if (secondaryBiome == null || blendFactor <= 0.001f)
                return primaryDensity;

            int secondaryDensity = EvaluateEffectiveDensity(baseDensity, secondaryBiome, band);
            return Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(primaryDensity, secondaryDensity, blendFactor)));
        }

        private string BuildRewardRhythm(HectonBiomeMatrixProfile biome, HectonBiomeFamilyProfile biomeFamily)
        {
            if (biome == null)
                return "None";

            if (biomeFamily != null && biomeFamily.spatialPatternProfile != null && !string.IsNullOrWhiteSpace(biomeFamily.spatialPatternProfile.rareObjectivePattern))
                return biomeFamily.spatialPatternProfile.rareObjectivePattern;

            string familyLogic = biomeFamily != null && biomeFamily.resourcePlanProfile != null
                ? biomeFamily.resourcePlanProfile.routeRewardLogic
                : string.Empty;

            if (!string.IsNullOrWhiteSpace(familyLogic))
                return familyLogic;

            if (biome.rareResourceBias >= 4)
                return "Редкие, дорогие заходы за сильной находкой.";
            if (biome.commonResourceBias >= 4 || biome.loosePickupBias >= 4)
                return "Короткие понятные круги с частой окупаемостью.";

            return "Смешанный цикл: мелкая добыча, потом более редкая ценность.";
        }

        private string BuildRouteRhythm(HectonBiomeMatrixProfile biome, HectonBiomeFamilyProfile biomeFamily)
        {
            if (biome == null)
                return "None";

            if (biomeFamily != null && biomeFamily.spatialPatternProfile != null && !string.IsNullOrWhiteSpace(biomeFamily.spatialPatternProfile.routeAnchorPattern))
                return biomeFamily.spatialPatternProfile.routeAnchorPattern;

            string guidance = biome.landmarkGuidance;
            if (!string.IsNullOrWhiteSpace(guidance))
                return guidance;

            if (biomeFamily != null && biomeFamily.landmarkPlanProfile != null)
                return biomeFamily.landmarkPlanProfile.routeUse;

            return "Маршрут держится на самых читаемых формах рельефа.";
        }

        private string BuildSafePocketRhythm(HectonBiomeMatrixProfile biome, HectonBiomeFamilyProfile biomeFamily)
        {
            if (biome == null)
                return "None";

            if (biomeFamily != null && biomeFamily.spatialPatternProfile != null && !string.IsNullOrWhiteSpace(biomeFamily.spatialPatternProfile.safePocketPattern))
                return biomeFamily.spatialPatternProfile.safePocketPattern;

            if (!string.IsNullOrWhiteSpace(biome.safePocketIdentity))
                return biome.safePocketIdentity;

            if (biomeFamily != null && biomeFamily.landmarkPlanProfile != null)
                return biomeFamily.landmarkPlanProfile.safePocketUse;

            return "Передышка ищется в складках рельефа и за большими формами.";
        }

        private static int BlendBias(int primary, int secondary, float blendFactor)
        {
            if (secondary <= 0 || blendFactor <= 0.001f)
                return primary;

            return Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(primary, secondary, blendFactor)), 0, 5);
        }

        private static string BuildBlendedRhythm(string primary, string secondary, float blendFactor)
        {
            if (string.IsNullOrWhiteSpace(primary))
                primary = "None";

            if (string.IsNullOrWhiteSpace(secondary) || secondary == "None" || blendFactor <= 0.12f)
                return primary;

            if (blendFactor >= 0.68f)
                return secondary;

            if (primary == secondary)
                return primary;

            return $"{primary} | Подмешивается: {secondary}";
        }

        private static string BuildBlendedDescriptor(string primary, string secondary, float blendFactor)
        {
            if (string.IsNullOrWhiteSpace(primary))
                primary = "None";

            if (string.IsNullOrWhiteSpace(secondary) || secondary == "None" || blendFactor <= 0.12f)
                return primary;

            if (blendFactor >= 0.68f)
                return secondary;

            if (primary == secondary)
                return primary;

            return $"{primary} -> {secondary}";
        }

        private static string GetItemLabel(Hecton8.Items.ItemData item)
        {
            if (item == null)
                return "None";

            return string.IsNullOrWhiteSpace(item.itemName) ? item.name : item.itemName;
        }

        private static float Average(params int[] values)
        {
            if (values == null || values.Length == 0)
                return 0f;

            int total = 0;
            for (int i = 0; i < values.Length; i++)
                total += values[i];

            return total / (float)values.Length;
        }

        private enum DensityBand
        {
            Near,
            Mid,
            Far
        }
    }
}
