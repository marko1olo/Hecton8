using UnityEngine;

namespace Hecton8.World
{
    [CreateAssetMenu(fileName = "WorldGenerativeGeologyProfile", menuName = "Hecton8/World/Generative Geology Profile")]
    public sealed class WorldGenerativeGeologyProfile : ScriptableObject
    {
        public enum GeneratorMode
        {
            Disabled,
            NeuralPreferred,
            HeuristicSdfFallback
        }

        public enum ShapeArchetype
        {
            Arch,
            Canopy,
            ComplexRock,
            ArchCluster,
            ReefPack,
            CaveBridge
        }

        public enum CompositionMode
        {
            SingleFeature,
            PairedFeature,
            ContextPack
        }

        public enum TerrainSeamMode
        {
            None,
            HeightBlend,
            SdfBlend,
            DebrisBridge,
            CarveAndDebris
        }

        public enum CaveBlendMode
        {
            None,
            ProbeOnly,
            SdfBlend,
            CarvePortal
        }

        [Header("Generator")]
        public string profileId = "geology.generic";
        public string profileLabel = "Generic Geology";
        public GeneratorMode generatorMode = GeneratorMode.HeuristicSdfFallback;
        public ShapeArchetype shapeArchetype = ShapeArchetype.ComplexRock;
        public CompositionMode compositionMode = CompositionMode.SingleFeature;

        [Header("Placement Context")]
        public Vector2 idealSlopeRange = new Vector2(10f, 42f);
        public Vector2 idealCurvatureRange = new Vector2(-0.35f, 0.35f);
        public Vector2 idealCaveProximityRange = new Vector2(0.2f, 0.8f);
        public Vector2 idealRidgeSignalRange = new Vector2(0.2f, 1f);
        public Vector2 idealCanyonSignalRange = new Vector2(0f, 0.8f);
        [Range(0f, 2f)] public float placementWeight = 0.9f;
        [Range(0f, 2f)] public float compositionWeight = 1.1f;
        [Range(0f, 1f)] public float contextPackThreshold = 0.62f;

        [Header("Seam Integration")]
        public TerrainSeamMode terrainSeamMode = TerrainSeamMode.SdfBlend;
        public CaveBlendMode caveBlendMode = CaveBlendMode.SdfBlend;
        [Min(0.5f)] public float seamBlendRadius = 12f;
        [Min(0f)] public float terrainRaiseMeters = 2.5f;
        [Min(0f)] public float terrainCutMeters = 2f;
        [Min(0)] public int debrisCountMin = 3;
        [Min(0)] public int debrisCountMax = 8;

        [Header("LOD")]
        [Range(1, 3)] public int lodCount = 3;
        public Vector3 lodScreenHeights = new Vector3(0.65f, 0.28f, 0.08f);

        [Header("Future AI")]
        public string futureModelId = "geology.sdf.placeholder";
        [TextArea(2, 4)] public string neuralPromptHint = "Generate eroded underwater geology with layered strata, fracture shelves, and contextual seam continuity.";

        public bool IsEnabled => generatorMode != GeneratorMode.Disabled;

        public float EvaluatePlacementFitness(
            float slopeDegrees,
            float curvature,
            float caveProximity,
            float ridgeSignal,
            float canyonSignal,
            float compositionPotential)
        {
            float slopeFit = EvaluateRangeFit(idealSlopeRange, slopeDegrees);
            float curvatureFit = EvaluateRangeFit(idealCurvatureRange, curvature);
            float caveFit = EvaluateRangeFit(idealCaveProximityRange, caveProximity);
            float ridgeFit = EvaluateRangeFit(idealRidgeSignalRange, ridgeSignal);
            float canyonFit = EvaluateRangeFit(idealCanyonSignalRange, canyonSignal);

            float contextFit = (slopeFit * 0.24f)
                + (curvatureFit * 0.18f)
                + (caveFit * 0.20f)
                + (ridgeFit * 0.18f)
                + (canyonFit * 0.12f)
                + (Mathf.Clamp01(compositionPotential) * 0.08f);

            return Mathf.Clamp01(contextFit * Mathf.Max(0.05f, placementWeight));
        }

        public bool PreferContextPack(float compositionPotential)
        {
            return compositionMode == CompositionMode.ContextPack
                || (compositionMode == CompositionMode.PairedFeature && compositionPotential >= contextPackThreshold)
                || compositionPotential >= contextPackThreshold + 0.12f;
        }

        public int ResolveDebrisCount(int stableHash)
        {
            int min = Mathf.Max(0, debrisCountMin);
            int max = Mathf.Max(min, debrisCountMax);
            if (max <= min)
                return min;

            int range = (max - min) + 1;
            int safeHash = Mathf.Abs(stableHash);
            return min + (safeHash % range);
        }

        private static float EvaluateRangeFit(Vector2 range, float value)
        {
            if (range.y <= range.x)
                return value >= range.x ? 1f : 0f;

            if (value >= range.x && value <= range.y)
                return 1f;

            float falloff = Mathf.Max(0.0001f, (range.y - range.x) * 0.6f);
            if (value < range.x)
                return Mathf.Clamp01(1f - ((range.x - value) / falloff));

            return Mathf.Clamp01(1f - ((value - range.y) / falloff));
        }
    }
}
