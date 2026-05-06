using Hecton8.Core;
using Hecton8.Environment;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Cold-path runtime smoke probe for the MapMagic sandbox biome matrix handoff.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BiomeMatrixSmokeTester : MonoBehaviour
    {
        private const int TerrainScratchCapacity = 32;

        [Header("References")]
        [Tooltip("MapMagic terrain bridge under test. Resolved from runtime references when omitted.")]
        [SerializeField] private MapMagicBridge mapMagicBridge;
        [Tooltip("Procedural field sampler used to resolve packed biome influence at the probe position.")]
        [SerializeField] private WorldProceduralFieldSampler fieldSampler;
        [Tooltip("Scatter director expected to publish the packed biome influence GraphicsBuffer.")]
        [SerializeField] private WorldProceduralScatterDirector scatterDirector;
        [Tooltip("Biome matrix director expected to expose the 108-slot matrix catalog.")]
        [SerializeField] private BiomeMatrixDirector biomeMatrixDirector;
        [Tooltip("Optional sample point. Uses this transform when omitted.")]
        [SerializeField] private Transform sampleTransform;

        [Header("Execution")]
        [Tooltip("Runs the smoke test once from Start. Leave off for manual context-menu validation.")]
        [SerializeField] private bool runOnStart;
        [Tooltip("Requires MapMagicBridge sandbox procedural terrain mode to be enabled.")]
        [SerializeField] private bool requireSandboxProceduralTerrainOnly = true;
        [Tooltip("Requires the scatter director to have published a non-empty biome influence GPU buffer.")]
        [SerializeField] private bool requireGpuInfluenceGrid = true;

        [Header("Diagnostics")]
        [SerializeField] private bool _debugPassed;
        [SerializeField] private string _debugStatus = "NotRun";
        [SerializeField] private int _debugCatalogSlots;
        [SerializeField] private int _debugCatalogFilledSlots;
        [SerializeField] private int _debugCatalogDuplicateIds;
        [SerializeField] private int _debugCatalogInvalidIds;
        [SerializeField] private int _debugResolvedTerrainCount;
        [SerializeField] private bool _debugSandboxMode;
        [SerializeField] private bool _debugSampledBiomeInfluence;
        [SerializeField] private int _debugPrimaryBiomeId;
        [SerializeField] private int _debugSecondaryBiomeId;
        [SerializeField] private int _debugBlend255;
        [SerializeField] private int _debugInfluenceFlags;
        [SerializeField] private int _debugGpuInfluenceGridCells;
        [SerializeField] private int _debugGpuInfluenceBufferCapacity;

        // COLD ALLOC: Terrain[32] - smoke-test MapMagic tile cache resolver buffer - owner: BiomeMatrixSmokeTester
        private readonly Terrain[] _terrainScratch = new Terrain[TerrainScratchCapacity];

        /// <summary>
        /// True when the latest smoke run passed every enabled gate.
        /// </summary>
        public bool LastRunPassed => _debugPassed;

        /// <summary>
        /// Failure code or PASS state from the latest smoke run.
        /// </summary>
        public string LastRunStatus => _debugStatus;

        private void Start()
        {
            if (runOnStart)
                RunSmokeTest();
        }

        [ContextMenu("Run Biome Matrix Smoke Test")]
        private void RunSmokeTestFromContextMenu()
        {
            RunSmokeTest();
        }

        /// <summary>
        /// Executes the cold-path biome matrix smoke test and records inspector diagnostics.
        /// </summary>
        /// <returns>True when all enabled biome matrix, terrain, influence, and GPU upload gates pass.</returns>
        public bool RunSmokeTest()
        {
            ResolveReferences();
            ClearTerrainScratch();

            bool catalogOk = ValidateCatalog(
                biomeMatrixDirector != null ? biomeMatrixDirector.MatrixCatalog : null,
                out _debugCatalogSlots,
                out _debugCatalogFilledSlots,
                out _debugCatalogDuplicateIds,
                out _debugCatalogInvalidIds);

            _debugSandboxMode = mapMagicBridge != null && mapMagicBridge.SandboxProceduralTerrainOnly;
            _debugResolvedTerrainCount = mapMagicBridge != null
                ? mapMagicBridge.CopyResolvedTerrainsTo(_terrainScratch)
                : 0;

            Vector3 samplePosition = sampleTransform != null ? sampleTransform.position : transform.position;
            WorldProceduralFieldSampler.BiomeInfluenceCell influence = default;
            HectonBiomeMatrixProfile primaryProfile = null;
            HectonBiomeMatrixProfile secondaryProfile = null;
            _debugSampledBiomeInfluence = fieldSampler != null &&
                fieldSampler.TrySampleBiomeInfluence(
                    samplePosition,
                    out influence,
                    out primaryProfile,
                    out secondaryProfile);

            _debugPrimaryBiomeId = influence.PrimaryVisualFamilyId;
            _debugSecondaryBiomeId = influence.SecondaryVisualFamilyId;
            _debugBlend255 = influence.Blend255;
            _debugInfluenceFlags = influence.Flags;

            _debugGpuInfluenceGridCells = scatterDirector != null ? scatterDirector.DebugBiomeInfluenceGridCells : 0;
            _debugGpuInfluenceBufferCapacity = scatterDirector != null ? scatterDirector.DebugBiomeInfluenceGpuBufferCapacity : 0;

            bool sandboxOk = !requireSandboxProceduralTerrainOnly || _debugSandboxMode;
            bool terrainOk = _debugResolvedTerrainCount > 0;
            bool influenceIdOk = !_debugSampledBiomeInfluence ||
                (uint)_debugPrimaryBiomeId < HectonBiomeVisualFamilyUtility.VisualFamilyCount;
            bool secondaryIdOk = !_debugSampledBiomeInfluence ||
                _debugBlend255 == 0 ||
                (uint)_debugSecondaryBiomeId < HectonBiomeVisualFamilyUtility.VisualFamilyCount;
            bool gpuGridOk = !requireGpuInfluenceGrid ||
                (_debugGpuInfluenceGridCells > 0 &&
                 _debugGpuInfluenceBufferCapacity >= _debugGpuInfluenceGridCells);
            bool scatterDirectorOk = !requireGpuInfluenceGrid || scatterDirector != null;

            _debugPassed = catalogOk &&
                sandboxOk &&
                terrainOk &&
                fieldSampler != null &&
                influenceIdOk &&
                secondaryIdOk &&
                scatterDirectorOk &&
                gpuGridOk;

            _debugStatus = _debugPassed
                ? "PASS"
                : BuildFailureStatus(catalogOk, sandboxOk, terrainOk, influenceIdOk, secondaryIdOk, scatterDirectorOk, gpuGridOk);

            return _debugPassed;
        }

        private void ResolveReferences()
        {
            WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref mapMagicBridge);
            WorldRuntimeReferenceUtility.TryResolveWorldProceduralFieldSampler(ref fieldSampler);
            WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref biomeMatrixDirector);

            if (scatterDirector == null)
                scatterDirector = WorldProceduralScatterDirector.ActiveRuntimeInstance;
        }

        private void ClearTerrainScratch()
        {
            for (int i = 0; i < _terrainScratch.Length; i++)
                _terrainScratch[i] = null;
        }

        private static bool ValidateCatalog(
            HectonBiomeMatrixCatalog catalog,
            out int slots,
            out int filledSlots,
            out int duplicateIds,
            out int invalidIds)
        {
            slots = catalog != null ? catalog.Count : 0;
            filledSlots = 0;
            duplicateIds = 0;
            invalidIds = 0;

            HectonBiomeMatrixProfile[] profiles = catalog != null ? catalog.Profiles : null;
            if (profiles == null)
                return false;

            ulong seenLow = 0UL;
            ulong seenHigh = 0UL;
            for (int i = 0; i < profiles.Length; i++)
            {
                HectonBiomeMatrixProfile profile = profiles[i];
                if (profile == null)
                    continue;

                filledSlots++;
                int matrixIndex = profile.matrixIndex;
                if ((uint)(matrixIndex - 1) >= 108u)
                {
                    invalidIds++;
                    continue;
                }

                if (!TryMarkMatrixIndex(matrixIndex, ref seenLow, ref seenHigh))
                    duplicateIds++;
            }

            return slots == 108 &&
                filledSlots == 108 &&
                duplicateIds == 0 &&
                invalidIds == 0;
        }

        private static bool TryMarkMatrixIndex(int matrixIndex, ref ulong seenLow, ref ulong seenHigh)
        {
            if (matrixIndex < 64)
            {
                ulong mask = 1UL << matrixIndex;
                bool isNew = (seenLow & mask) == 0UL;
                seenLow |= mask;
                return isNew;
            }

            int highBit = matrixIndex - 64;
            ulong highMask = 1UL << highBit;
            bool isHighNew = (seenHigh & highMask) == 0UL;
            seenHigh |= highMask;
            return isHighNew;
        }

        private string BuildFailureStatus(
            bool catalogOk,
            bool sandboxOk,
            bool terrainOk,
            bool influenceIdOk,
            bool secondaryIdOk,
            bool scatterDirectorOk,
            bool gpuGridOk)
        {
            if (!catalogOk)
                return "FAIL:Catalog108";
            if (!sandboxOk)
                return "FAIL:SandboxMode";
            if (!terrainOk)
                return "FAIL:NoResolvedMapMagicTerrain";
            if (fieldSampler == null)
                return "FAIL:NoFieldSampler";
            if (!influenceIdOk)
                return "FAIL:PrimaryVisualFamilyId";
            if (!secondaryIdOk)
                return "FAIL:SecondaryVisualFamilyId";
            if (!scatterDirectorOk)
                return "FAIL:NoScatterDirector";
            if (!gpuGridOk)
                return "FAIL:NoGpuInfluenceGrid";

            return "FAIL:Unknown";
        }
    }
}
