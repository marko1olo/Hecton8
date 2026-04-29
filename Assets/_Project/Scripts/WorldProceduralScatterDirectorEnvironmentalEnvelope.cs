using UnityEngine;

namespace Hecton8.World
{
    public sealed partial class WorldProceduralScatterDirector
    {
        private enum FloraBudgetClass : byte
        {
            None = 0,
            Micro = 1,
            Macro = 2
        }

        [Header("Environmental Envelope")]
        [SerializeField]
        [Tooltip("Optional environment bridge used to sample abyssal flow for deep-flora envelope gates.")]
        private HectonMapMagicVegetationBridge environmentalVegetationBridge;

        [SerializeField]
        [Tooltip("True when abyssal flora families are gated by depth and flow envelope checks instead of biome tags alone.")]
        private bool enableEnvironmentalEnvelopeGating = true;

        [SerializeField, Min(100f)]
        [Tooltip("Minimum depth in meters required before deep flora families are eligible for envelope evaluation.")]
        private float deepFloraMinDepthMeters = 800f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Maximum accepted depth-light proxy for brittle deep coral. Lower values push coral deeper into darker water.")]
        private float deepCoralMaxLightProxy = 0.1f;

        [SerializeField, Min(1f)]
        [Tooltip("Depth fade range used by the fallback depth-light proxy when no per-position light sampler exists.")]
        private float deepFloraLightProxyFadeRangeMeters = 240f;

        [SerializeField, Min(0f)]
        [Tooltip("Maximum accepted abyssal-flow magnitude for brittle deep coral placements.")]
        private float deepCoralMaxFlowMagnitude = 0.85f;

        [SerializeField, Min(0f)]
        [Tooltip("Minimum desired abyssal-flow magnitude for abyssal kelp placements.")]
        private float abyssalKelpMinFlowMagnitude = 0.06f;

        [SerializeField]
        [Tooltip("True when flora candidate acceptance counts nearby flora inside a 100 m-style envelope and silently drops over-budget requests.")]
        private bool enableFloraDensityClamp = true;

        [SerializeField, Min(1f)]
        [Tooltip("Planar radius in meters used when policing nearby flora density before candidate acceptance.")]
        private float floraDensityClampRadiusMeters = 100f;

        [SerializeField, Min(1)]
        [Tooltip("Hard cap for micro-flora placements inside the density-clamp radius.")]
        private int floraDensityClampMicroCap = 2000;

        [SerializeField, Min(1)]
        [Tooltip("Hard cap for macro-flora placements inside the density-clamp radius.")]
        private int floraDensityClampMacroCap = 50;

        private bool PassesEnvironmentalEnvelope(
            in WorldProceduralFieldSampler.FieldSample fieldSample,
            in ScatterRuntimeRuleEntry runtimeRule,
            in ScatterCandidatePreview candidatePreview)
        {
            if (runtimeRule.StrictEnvelopeMapping)
            {
                if (!PassesStrictSubstrateEnvelope(runtimeRule, candidatePreview.Position))
                    return false;

                if (!PassesClusterPatchEnvelope(runtimeRule, candidatePreview))
                    return false;
            }

            if (!enableEnvironmentalEnvelopeGating)
                return true;

            WorldPrefabFamilyProfile family = runtimeRule.Family;
            if (family == null)
                return true;

            int familyHash = family.FamilyHash;
            if (familyHash != _FamilyCoralBrittleHash && familyHash != _FamilyKelpAbyssalHash)
                return true;

            if (fieldSample.depthMeters < Mathf.Max(100f, deepFloraMinDepthMeters))
                return false;

            if (familyHash == _FamilyCoralBrittleHash)
            {
                float lightProxy = EvaluateDepthLightProxy01(fieldSample.depthMeters);
                if (lightProxy > Mathf.Clamp01(deepCoralMaxLightProxy))
                    return false;
            }

            if (!TrySampleEnvironmentalFlowMagnitude(candidatePreview.Position, out float flowMagnitude))
                return true;

            if (familyHash == _FamilyCoralBrittleHash)
                return flowMagnitude <= Mathf.Max(0f, deepCoralMaxFlowMagnitude);

            if (familyHash == _FamilyKelpAbyssalHash)
                return flowMagnitude >= Mathf.Max(0f, abyssalKelpMinFlowMagnitude);

            return true;
        }

        private bool PassesStrictSubstrateEnvelope(in ScatterRuntimeRuleEntry runtimeRule, Vector3 absolutePosition)
        {
            if (runtimeRule.RequiredSubstrate == WorldProceduralPlacementRule.FloraSubstrateMask.None ||
                runtimeRule.RequiredSubstrate == WorldProceduralPlacementRule.FloraSubstrateMask.Any)
            {
                return true;
            }

            if (!EnsureEnvironmentalVegetationBridgeResolved())
                return true;

            Vector3 runtimePosition = ToRuntimeScatterPosition(absolutePosition);
            if (!environmentalVegetationBridge.TrySampleFloraSubstrate(runtimePosition, out WorldProceduralPlacementRule.FloraSubstrateMask resolvedSubstrate))
                return true;

            return (runtimeRule.RequiredSubstrate & resolvedSubstrate) != 0;
        }

        private static bool PassesClusterPatchEnvelope(
            in ScatterRuntimeRuleEntry runtimeRule,
            in ScatterCandidatePreview candidatePreview)
        {
            if (runtimeRule.ClusterNoiseThreshold <= 0f)
                return true;

            float patchMask = EvaluateClusterPatchMask01(
                candidatePreview.Position.x,
                candidatePreview.Position.z,
                runtimeRule.ClusterNoiseScale,
                runtimeRule.RuleIdHash,
                runtimeRule.Family != null ? runtimeRule.Family.FamilyHash : 0);
            return patchMask >= runtimeRule.ClusterNoiseThreshold;
        }

        private bool TrySampleEnvironmentalFlowMagnitude(Vector3 absolutePosition, out float flowMagnitude)
        {
            flowMagnitude = 0f;
            if (!Application.isPlaying)
                return false;

            if (!EnsureEnvironmentalVegetationBridgeResolved())
                return false;

            Vector3 runtimePosition = ToRuntimeScatterPosition(absolutePosition);
            if (!environmentalVegetationBridge.TrySampleAbyssalFlow(runtimePosition, out Vector3 flowVector))
                return false;

            flowMagnitude = flowVector.magnitude;
            return true;
        }

        private bool EnsureEnvironmentalVegetationBridgeResolved()
        {
            if (environmentalVegetationBridge != null)
                return true;

            WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref environmentalVegetationBridge);
            return environmentalVegetationBridge != null;
        }

        private float EvaluateDepthLightProxy01(float depthMeters)
        {
            float minimumDepth = Mathf.Max(1f, deepFloraMinDepthMeters);
            float fadeRange = Mathf.Max(1f, deepFloraLightProxyFadeRangeMeters);
            float darkness01 = Mathf.Clamp01((depthMeters - minimumDepth) / fadeRange);
            return 1f - darkness01;
        }

        private static float EvaluateClusterPatchMask01(
            float worldX,
            float worldZ,
            float clusterNoiseScale,
            int ruleIdHash,
            int familyHash)
        {
            float safeScale = Mathf.Max(0.0001f, clusterNoiseScale);
            float baseX = (worldX * safeScale) + ((ruleIdHash & 255) * 0.03125f);
            float baseZ = (worldZ * safeScale) + ((familyHash & 255) * 0.03125f);
            float octaveA = Mathf.PerlinNoise(baseX, baseZ);
            float octaveB = Mathf.PerlinNoise((baseX * 1.93f) + 11.7f, (baseZ * 1.93f) - 7.1f);
            float octaveC = Mathf.PerlinNoise((baseX * 0.57f) - 19.4f, (baseZ * 0.57f) + 23.8f);
            return Mathf.Clamp01((octaveA * 0.58f) + (octaveB * 0.28f) + (octaveC * 0.14f));
        }

        private static FloraBudgetClass ResolveFloraBudgetClass(WorldPrefabFamilyProfile family)
        {
            if (family == null)
                return FloraBudgetClass.None;

            switch (family.proceduralDomain)
            {
                case WorldPrefabFamilyProfile.ProceduralDomain.Kelp:
                case WorldPrefabFamilyProfile.ProceduralDomain.Plant:
                case WorldPrefabFamilyProfile.ProceduralDomain.Coral:
                    return family.scatterLayer == WorldPrefabFamilyProfile.ScatterLayer.Ground
                        ? FloraBudgetClass.Micro
                        : FloraBudgetClass.Macro;
                default:
                    return FloraBudgetClass.None;
            }
        }
    }
}
