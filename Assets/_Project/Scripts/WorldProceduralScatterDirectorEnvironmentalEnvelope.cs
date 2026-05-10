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

            WorldPrefabFamilyProfile family = runtimeRule.Family;
            if (family == null)
                return true;

            if (ShouldRejectForMigratorySargassumShade(family, candidatePreview))
                return false;

            if (!enableEnvironmentalEnvelopeGating)
                return true;

            int familyHash = family.FamilyHash;
            if (familyHash != _FamilyCoralBrittleHash && familyHash != _FamilyKelpAbyssalHash)
                return true;

            if (fieldSample.depthMeters < Mathf.Max(100f, deepFloraMinDepthMeters))
                return false;

            if (familyHash == _FamilyCoralBrittleHash)
            {
                float lightProxy = ScatterMath.EvaluateDepthLightProxy01(
                    fieldSample.depthMeters,
                    deepFloraMinDepthMeters,
                    deepFloraLightProxyFadeRangeMeters);
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
            {
                LogStrictSubstrateMissingOnce(absolutePosition);
                return false;
            }

            Vector3 runtimePosition = ToRuntimeScatterPosition(absolutePosition);
            if (!environmentalVegetationBridge.TrySampleFloraSubstrate(runtimePosition, out WorldProceduralPlacementRule.FloraSubstrateMask resolvedSubstrate))
            {
                LogStrictSubstrateMissingOnce(absolutePosition);
                return false;
            }

            return ScatterCandidateEvaluator.PassesStrictSubstrateEnvelope(runtimeRule.RequiredSubstrate, resolvedSubstrate);
        }

        private bool PassesClusterPatchEnvelope(
            in ScatterRuntimeRuleEntry runtimeRule,
            in ScatterCandidatePreview candidatePreview)
        {
            return ScatterCandidateEvaluator.PassesClusterPatchEnvelope(
                candidatePreview.Position.x,
                candidatePreview.Position.z,
                _runtimeStreamingState.ChunkSize,
                runtimeRule.ClusterNoiseThreshold,
                runtimeRule.ClusterNoiseScale,
                runtimeRule.RuleIdHash,
                runtimeRule.Family != null ? runtimeRule.Family.FamilyHash : 0);
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

            flowMagnitude = FastMagnitudeApprox(flowVector);
            return true;
        }

        private static float FastMagnitudeApprox(Vector3 value)
        {
            float ax = Mathf.Abs(value.x);
            float ay = Mathf.Abs(value.y);
            float az = Mathf.Abs(value.z);
            float max = Mathf.Max(ax, Mathf.Max(ay, az));
            float min = Mathf.Min(ax, Mathf.Min(ay, az));
            float mid = ax + ay + az - max - min;
            return max + (mid * 0.41421356f) + (min * 0.29289322f);
        }

        private bool EnsureEnvironmentalVegetationBridgeResolved()
        {
            if (environmentalVegetationBridge != null)
                return true;

            WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref environmentalVegetationBridge);
            return environmentalVegetationBridge != null;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogStrictSubstrateMissingOnce(Vector3 absolutePosition)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            float chunkSize = Mathf.Max(1f, _runtimeStreamingState.ChunkSize);
            int chunkX = Mathf.FloorToInt(absolutePosition.x / chunkSize);
            int chunkZ = Mathf.FloorToInt(absolutePosition.z / chunkSize);
            long chunkKey = (((long)chunkX) << 32) ^ (uint)chunkZ;
            EnsureWorkingMemory();
            if (_memory == null || !_memory.StrictSubstrateMissingLoggedChunks.Add(chunkKey))
                return;

            Debug.LogWarning(
                "[WorldProceduralScatterDirector] Strict flora substrate unavailable; rejecting strict-envelope flora for chunk " +
                chunkX +
                "," +
                chunkZ +
                ".",
                this);
#endif
        }

        private static FloraBudgetClass ResolveFloraBudgetClass(WorldPrefabFamilyProfile family)
        {
            return (FloraBudgetClass)ScatterMath.ResolveFloraBudgetClassId(family);
        }
    }
}
