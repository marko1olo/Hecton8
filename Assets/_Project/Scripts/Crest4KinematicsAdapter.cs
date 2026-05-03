using Hecton8.Core;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.Physics
{
    /// <summary>
    /// Crest 4-backed implementation of <see cref="IHectonOceanKinematics"/>.
    /// Keeps all Crest runtime calls and query-owner bookkeeping outside gameplay controllers.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Physics/Crest 4 Kinematics Adapter")]
    public sealed class Crest4KinematicsAdapter : HectonCrestOceanKinematics
    {
        private const int MaxBatchSampleCount = 5;
        private const int ProviderPriority = 400;
        private static readonly int _waveFoamStrengthId = Shader.PropertyToID("_WaveFoamStrength");
        private static readonly int _waveFoamCoverageId = Shader.PropertyToID("_WaveFoamCoverage");
        private static readonly int _foamScaleId = Shader.PropertyToID("_FoamScale");

        [Header("References")]
        [Tooltip("Explicit Crest ocean owner. Assign this directly or colocate the OceanRenderer on the same GameObject.")]
        [SerializeField] private Crest.OceanRenderer crestOceanRenderer;

        private int _heightQueryOwnerHash;
        private int _waveQueryOwnerHash;
        private int _displacementQueryOwnerHash;
        private int _flowQueryOwnerHash;
        private bool _loggedMissingOceanRenderer;
        private bool _loggedMissingCollisionProvider;
        // COLD ALLOC: Vector3[5] - native-to-managed Crest position bridge scratch for Crest 4 runtime fallback - owner: Crest4KinematicsAdapter
        private readonly Vector3[] _samplePositionScratch = new Vector3[MaxBatchSampleCount];
        // COLD ALLOC: Vector3[5] - native-to-managed Crest flow bridge scratch for Crest 4 runtime fallback - owner: Crest4KinematicsAdapter
        private readonly Vector3[] _flowScratch = new Vector3[MaxBatchSampleCount];
        // COLD ALLOC: Vector3[5] - native-to-managed Crest wave-normal bridge scratch for Crest 4 runtime fallback - owner: Crest4KinematicsAdapter
        private readonly Vector3[] _waveNormalScratch = new Vector3[MaxBatchSampleCount];
        // COLD ALLOC: Vector3[5] - native-to-managed Crest velocity bridge scratch for Crest 4 runtime fallback - owner: Crest4KinematicsAdapter
        private readonly Vector3[] _surfaceVelocityScratch = new Vector3[MaxBatchSampleCount];
        // COLD ALLOC: Vector3[5] - native-to-managed Crest displacement bridge scratch for Crest 4 runtime fallback - owner: Crest4KinematicsAdapter
        private readonly Vector3[] _displacementScratch = new Vector3[MaxBatchSampleCount];
        private readonly float[] _heightScratch =
            new float[MaxBatchSampleCount]; // COLD ALLOC: float[5] - temporary Crest height scratch buffer for wave-only queries - owner: Crest4KinematicsAdapter

        /// <inheritdoc />
        public override int Priority => ProviderPriority;

        /// <inheritdoc />
        public override bool IsAvailable
        {
            get
            {
                Crest.OceanRenderer oceanRenderer = ResolveOceanRenderer();
                return oceanRenderer != null && oceanRenderer.CollisionProvider != null;
            }
        }

        /// <inheritdoc />
        public override float SeaLevel => ResolveSeaLevel(ResolveOceanRenderer());

        /// <inheritdoc />
        public override bool TryGetSurfaceWeatherState(out HectonOceanSurfaceWeatherState state)
        {
            state = default;
            Crest.OceanRenderer oceanRenderer = ResolveOceanRenderer();
            if (oceanRenderer == null)
                return false;

            uint flags = (uint)HectonOceanSurfaceWeatherStateFlags.SupportsWindSpeed;
            state.WindSpeed = Mathf.Max(0f, oceanRenderer._globalWindSpeed);

            Material oceanMaterial = oceanRenderer.OceanMaterial;
            if (oceanMaterial != null)
            {
                if (oceanMaterial.HasProperty(_waveFoamStrengthId))
                {
                    flags |= (uint)HectonOceanSurfaceWeatherStateFlags.SupportsFoamStrength;
                    state.FoamStrength = oceanMaterial.GetFloat(_waveFoamStrengthId);
                }

                if (oceanMaterial.HasProperty(_waveFoamCoverageId))
                {
                    flags |= (uint)HectonOceanSurfaceWeatherStateFlags.SupportsFoamCoverage;
                    state.FoamCoverage = oceanMaterial.GetFloat(_waveFoamCoverageId);
                }

                if (oceanMaterial.HasProperty(_foamScaleId))
                {
                    flags |= (uint)HectonOceanSurfaceWeatherStateFlags.SupportsFoamScale;
                    state.FoamScale = oceanMaterial.GetFloat(_foamScaleId);
                }
            }

            state.Flags = flags;
            return true;
        }

        /// <inheritdoc />
        public override bool ApplySurfaceWeatherState(in HectonOceanSurfaceWeatherState state)
        {
            Crest.OceanRenderer oceanRenderer = ResolveOceanRenderer();
            if (oceanRenderer == null)
                return false;

            uint flags = state.Flags;
            if ((flags & (uint)HectonOceanSurfaceWeatherStateFlags.SupportsWindSpeed) != 0u)
                oceanRenderer._globalWindSpeed = Mathf.Max(0f, state.WindSpeed);

            Material oceanMaterial = oceanRenderer.OceanMaterial;
            if (oceanMaterial == null)
                return true;

            if ((flags & (uint)HectonOceanSurfaceWeatherStateFlags.SupportsFoamStrength) != 0u &&
                oceanMaterial.HasProperty(_waveFoamStrengthId))
            {
                oceanMaterial.SetFloat(_waveFoamStrengthId, state.FoamStrength);
            }

            if ((flags & (uint)HectonOceanSurfaceWeatherStateFlags.SupportsFoamCoverage) != 0u &&
                oceanMaterial.HasProperty(_waveFoamCoverageId))
            {
                oceanMaterial.SetFloat(_waveFoamCoverageId, state.FoamCoverage);
            }

            if ((flags & (uint)HectonOceanSurfaceWeatherStateFlags.SupportsFoamScale) != 0u &&
                oceanMaterial.HasProperty(_foamScaleId))
            {
                oceanMaterial.SetFloat(_foamScaleId, state.FoamScale);
            }

            return true;
        }

        /// <inheritdoc />
        public override bool TryAssignPrimaryLight(Light primaryLight)
        {
            Crest.OceanRenderer oceanRenderer = ResolveOceanRenderer();
            if (oceanRenderer == null || primaryLight == null)
                return false;

            if (!ReferenceEquals(oceanRenderer._primaryLight, primaryLight))
                oceanRenderer._primaryLight = primaryLight;

            return true;
        }

        private void Awake()
        {
            TryResolveLocalOceanRendererBinding();
            int ownerHash = unchecked((int)EntityId.ToULong(GetEntityId()));
            _heightQueryOwnerHash = ownerHash;
            _waveQueryOwnerHash = ownerHash ^ 0x2F31;
            _displacementQueryOwnerHash = ownerHash ^ 0x53C9;
            _flowQueryOwnerHash = ownerHash ^ 0x7A4D;
        }

        private void OnEnable()
        {
            OceanKinematicsRuntimeService.RegisterProvider(this);
        }

        private void OnDisable()
        {
            OceanKinematicsRuntimeService.UnregisterProvider(this);
        }

        /// <inheritdoc />
        public override bool GetWaterHeight(Vector3[] samplePositions, int sampleCount, float minSpatialLength, float[] waterHeights)
        {
            if (!ValidateHeightRequest(samplePositions, sampleCount, waterHeights))
                return false;

            if (!TryResolveCollisionProvider(out Crest.ICollProvider collisionProvider))
                return false;

            int queryStatus = collisionProvider.Query(
                _heightQueryOwnerHash,
                Mathf.Max(0.01f, minSpatialLength),
                samplePositions,
                waterHeights,
                null,
                null);
            return collisionProvider.RetrieveSucceeded(queryStatus);
        }

        /// <inheritdoc />
        public override bool GetWaterHeight(NativeArray<Vector3> samplePositions, int sampleCount, float minSpatialLength, NativeArray<float> waterHeights)
        {
            if (!ValidateHeightRequest(samplePositions, sampleCount, waterHeights))
                return false;

            CopyNativePositions(samplePositions, sampleCount);
            bool succeeded = GetWaterHeight(_samplePositionScratch, sampleCount, minSpatialLength, _heightScratch);
            CopyManagedHeightsToNative(_heightScratch, waterHeights, sampleCount);
            return succeeded;
        }

        /// <inheritdoc />
        public override bool GetSurfaceFlow(Vector3[] samplePositions, int sampleCount, float minSpatialLength, Vector3[] surfaceFlows)
        {
            if (!ValidateVectorRequest(samplePositions, sampleCount, surfaceFlows))
                return false;

            Crest.OceanRenderer oceanRenderer = ResolveOceanRenderer();
            if (oceanRenderer == null || oceanRenderer.FlowProvider == null)
                return false;

            Crest.IFlowProvider flowProvider = oceanRenderer.FlowProvider;
            int queryStatus = flowProvider.Query(
                _flowQueryOwnerHash,
                Mathf.Max(0.01f, minSpatialLength),
                samplePositions,
                surfaceFlows);
            return flowProvider.RetrieveSucceeded(queryStatus);
        }

        /// <inheritdoc />
        public override bool GetSurfaceFlow(NativeArray<Vector3> samplePositions, int sampleCount, float minSpatialLength, NativeArray<Vector3> surfaceFlows)
        {
            if (!ValidateVectorRequest(samplePositions, sampleCount, surfaceFlows))
                return false;

            CopyNativePositions(samplePositions, sampleCount);
            bool succeeded = GetSurfaceFlow(_samplePositionScratch, sampleCount, minSpatialLength, _flowScratch);
            CopyManagedVectorsToNative(_flowScratch, surfaceFlows, sampleCount);
            return succeeded;
        }

        /// <inheritdoc />
        public override bool GetWaveNormal(
            Vector3[] samplePositions,
            int sampleCount,
            float minSpatialLength,
            Vector3[] waveNormals,
            Vector3[] surfaceVelocities,
            Vector3[] displacements)
        {
            if (!ValidateWaveRequest(samplePositions, sampleCount, waveNormals, surfaceVelocities, displacements))
                return false;

            if (!TryResolveCollisionProvider(out Crest.ICollProvider collisionProvider))
                return false;

            float resolvedMinSpatialLength = Mathf.Max(0.01f, minSpatialLength);
            int waveStatus = collisionProvider.Query(
                _waveQueryOwnerHash,
                resolvedMinSpatialLength,
                samplePositions,
                _heightScratch,
                waveNormals,
                surfaceVelocities);
            bool waveSucceeded = collisionProvider.RetrieveSucceeded(waveStatus);
            if (!waveSucceeded)
                return false;

            int displacementStatus = collisionProvider.Query(
                _displacementQueryOwnerHash,
                resolvedMinSpatialLength,
                samplePositions,
                displacements,
                null,
                null);
            if (!collisionProvider.RetrieveSucceeded(displacementStatus))
            {
                for (int i = 0; i < sampleCount; i++)
                    displacements[i] = Vector3.zero;
            }

            return true;
        }

        /// <inheritdoc />
        public override bool GetWaveNormal(
            NativeArray<Vector3> samplePositions,
            int sampleCount,
            float minSpatialLength,
            NativeArray<Vector3> waveNormals,
            NativeArray<Vector3> surfaceVelocities,
            NativeArray<Vector3> displacements)
        {
            if (!ValidateWaveRequest(samplePositions, sampleCount, waveNormals, surfaceVelocities, displacements))
                return false;

            CopyNativePositions(samplePositions, sampleCount);
            bool succeeded = GetWaveNormal(
                _samplePositionScratch,
                sampleCount,
                minSpatialLength,
                _waveNormalScratch,
                _surfaceVelocityScratch,
                _displacementScratch);
            CopyManagedVectorsToNative(_waveNormalScratch, waveNormals, sampleCount);
            CopyManagedVectorsToNative(_surfaceVelocityScratch, surfaceVelocities, sampleCount);
            CopyManagedVectorsToNative(_displacementScratch, displacements, sampleCount);
            return succeeded;
        }

        private bool TryResolveCollisionProvider(out Crest.ICollProvider collisionProvider)
        {
            Crest.OceanRenderer oceanRenderer = ResolveOceanRenderer();
            collisionProvider = oceanRenderer != null ? oceanRenderer.CollisionProvider : null;
            if (collisionProvider == null && oceanRenderer != null && !_loggedMissingCollisionProvider)
            {
                _loggedMissingCollisionProvider = true;
                Debug.LogError("[Crest4KinematicsAdapter] Crest OceanRenderer is bound but CollisionProvider is unavailable. Ocean sampling disabled.");
            }

            return collisionProvider != null;
        }

        private void TryResolveLocalOceanRendererBinding()
        {
            if (crestOceanRenderer == null)
                TryGetComponent(out crestOceanRenderer);
        }

        private Crest.OceanRenderer ResolveOceanRenderer()
        {
            if (crestOceanRenderer != null)
                return crestOceanRenderer;

            TryResolveLocalOceanRendererBinding();
            if (crestOceanRenderer != null)
                return crestOceanRenderer;

            if (!_loggedMissingOceanRenderer)
            {
                _loggedMissingOceanRenderer = true;
                Debug.LogError("[Crest4KinematicsAdapter] Missing Crest OceanRenderer binding. Assign crestOceanRenderer explicitly or colocate the OceanRenderer component.");
            }

            return null;
        }

        private static float ResolveSeaLevel(Crest.OceanRenderer oceanRenderer)
        {
            if (oceanRenderer != null && oceanRenderer.Root != null)
                return oceanRenderer.Root.position.y;

            HectonFluidEngine fluidEngine = GlobalRegistry.Fluid;
            return fluidEngine != null ? fluidEngine.WaterLevel : 0f;
        }

        private static bool ValidateHeightRequest(Vector3[] samplePositions, int sampleCount, float[] heights)
        {
            return samplePositions != null &&
                   heights != null &&
                   sampleCount > 0 &&
                   sampleCount <= MaxBatchSampleCount &&
                   samplePositions.Length >= sampleCount &&
                   heights.Length >= sampleCount;
        }

        private static bool ValidateHeightRequest(NativeArray<Vector3> samplePositions, int sampleCount, NativeArray<float> heights)
        {
            return samplePositions.IsCreated &&
                   heights.IsCreated &&
                   sampleCount > 0 &&
                   sampleCount <= MaxBatchSampleCount &&
                   samplePositions.Length >= sampleCount &&
                   heights.Length >= sampleCount;
        }

        private static bool ValidateVectorRequest(Vector3[] samplePositions, int sampleCount, Vector3[] vectors)
        {
            return samplePositions != null &&
                   vectors != null &&
                   sampleCount > 0 &&
                   sampleCount <= MaxBatchSampleCount &&
                   samplePositions.Length >= sampleCount &&
                   vectors.Length >= sampleCount;
        }

        private static bool ValidateVectorRequest(NativeArray<Vector3> samplePositions, int sampleCount, NativeArray<Vector3> vectors)
        {
            return samplePositions.IsCreated &&
                   vectors.IsCreated &&
                   sampleCount > 0 &&
                   sampleCount <= MaxBatchSampleCount &&
                   samplePositions.Length >= sampleCount &&
                   vectors.Length >= sampleCount;
        }

        private static bool ValidateWaveRequest(
            Vector3[] samplePositions,
            int sampleCount,
            Vector3[] waveNormals,
            Vector3[] surfaceVelocities,
            Vector3[] displacements)
        {
            return ValidateVectorRequest(samplePositions, sampleCount, waveNormals) &&
                   surfaceVelocities != null &&
                   displacements != null &&
                   surfaceVelocities.Length >= sampleCount &&
                   displacements.Length >= sampleCount;
        }

        private static bool ValidateWaveRequest(
            NativeArray<Vector3> samplePositions,
            int sampleCount,
            NativeArray<Vector3> waveNormals,
            NativeArray<Vector3> surfaceVelocities,
            NativeArray<Vector3> displacements)
        {
            return ValidateVectorRequest(samplePositions, sampleCount, waveNormals) &&
                   surfaceVelocities.IsCreated &&
                   displacements.IsCreated &&
                   surfaceVelocities.Length >= sampleCount &&
                   displacements.Length >= sampleCount;
        }

        private void CopyNativePositions(NativeArray<Vector3> samplePositions, int sampleCount)
        {
            for (int i = 0; i < sampleCount; i++)
                _samplePositionScratch[i] = samplePositions[i];
        }

        private static void CopyManagedHeightsToNative(float[] source, NativeArray<float> destination, int sampleCount)
        {
            for (int i = 0; i < sampleCount; i++)
                destination[i] = source[i];
        }

        private static void CopyManagedVectorsToNative(Vector3[] source, NativeArray<Vector3> destination, int sampleCount)
        {
            for (int i = 0; i < sampleCount; i++)
                destination[i] = source[i];
        }
    }
}
