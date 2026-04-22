using System.Collections.Generic;
using UnityEngine;

namespace Hecton8.Physics
{
    /// <summary>
    /// Crest 4-backed implementation of <see cref="IHectonOceanKinematics"/>.
    /// Keeps all Crest runtime calls and query-owner bookkeeping outside gameplay controllers.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Physics/Crest 4 Kinematics Adapter")]
    public sealed class Crest4KinematicsAdapter : MonoBehaviour, IHectonOceanKinematics
    {
        private const int MaxBatchSampleCount = 5;
        private const int ProviderPriority = 400;

        [Header("References")]
        [Tooltip("Optional explicit Crest ocean owner. Leave empty to resolve OceanRenderer.Instance or scan the scene.")]
        [SerializeField] private Crest.OceanRenderer crestOceanRenderer;

        [Header("Fallback / Resolve")]
        [Tooltip("Retry cadence for one-shot scene searches when OceanRenderer.Instance is not ready yet.")]
        [SerializeField, Range(0.1f, 5f)] private float sceneSearchRetryInterval = 1f;

        private int _heightQueryOwnerHash;
        private int _waveQueryOwnerHash;
        private int _displacementQueryOwnerHash;
        private int _flowQueryOwnerHash;
        private float _nextResolveTime = float.NegativeInfinity;
        private readonly List<GameObject> _sceneRootBuffer =
            new List<GameObject>(16); // COLD ALLOC: List<GameObject>(16) — reusable scene root scan buffer for delayed Crest owner recovery — owner: Crest4KinematicsAdapter
        private readonly float[] _heightScratch =
            new float[MaxBatchSampleCount]; // COLD ALLOC: float[5] — temporary Crest height scratch buffer for wave-only queries — owner: Crest4KinematicsAdapter

        /// <inheritdoc />
        public int Priority => ProviderPriority;

        /// <inheritdoc />
        public bool IsAvailable
        {
            get
            {
                Crest.OceanRenderer oceanRenderer = ResolveOceanRenderer(forceSceneSearch: false);
                return oceanRenderer != null && oceanRenderer.CollisionProvider != null;
            }
        }

        /// <inheritdoc />
        public float SeaLevel => ResolveSeaLevel(ResolveOceanRenderer(forceSceneSearch: false));

        private void Awake()
        {
            int ownerHash = GetHashCode();
            _heightQueryOwnerHash = ownerHash;
            _waveQueryOwnerHash = ownerHash ^ 0x2F31;
            _displacementQueryOwnerHash = ownerHash ^ 0x53C9;
            _flowQueryOwnerHash = ownerHash ^ 0x7A4D;
        }

        private void OnEnable()
        {
            HectonOceanRegistry.Register(this);
        }

        private void OnDisable()
        {
            HectonOceanRegistry.Unregister(this);
        }

        /// <inheritdoc />
        public bool GetWaterHeight(Vector3[] samplePositions, int sampleCount, float minSpatialLength, float[] waterHeights)
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
        public bool GetSurfaceFlow(Vector3[] samplePositions, int sampleCount, float minSpatialLength, Vector3[] surfaceFlows)
        {
            if (!ValidateVectorRequest(samplePositions, sampleCount, surfaceFlows))
                return false;

            Crest.OceanRenderer oceanRenderer = ResolveOceanRenderer(forceSceneSearch: true);
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
        public bool GetWaveNormal(
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

        private bool TryResolveCollisionProvider(out Crest.ICollProvider collisionProvider)
        {
            Crest.OceanRenderer oceanRenderer = ResolveOceanRenderer(forceSceneSearch: true);
            collisionProvider = oceanRenderer != null ? oceanRenderer.CollisionProvider : null;
            return collisionProvider != null;
        }

        private Crest.OceanRenderer ResolveOceanRenderer(bool forceSceneSearch)
        {
            if (crestOceanRenderer != null)
                return crestOceanRenderer;

            Crest.OceanRenderer instance = Crest.OceanRenderer.Instance;
            if (instance != null)
            {
                crestOceanRenderer = instance;
                return crestOceanRenderer;
            }

            if (!forceSceneSearch || !Application.isPlaying)
                return null;

            float now = Time.unscaledTime;
            if (now < _nextResolveTime)
                return null;

            _nextResolveTime = now + Mathf.Max(0.1f, sceneSearchRetryInterval);
            _sceneRootBuffer.Clear();
            gameObject.scene.GetRootGameObjects(_sceneRootBuffer);
            for (int i = 0; i < _sceneRootBuffer.Count; i++)
            {
                GameObject rootObject = _sceneRootBuffer[i];
                if (rootObject == null)
                    continue;

                Crest.OceanRenderer candidate = rootObject.GetComponentInChildren<Crest.OceanRenderer>(true);
                if (candidate == null)
                    continue;

                crestOceanRenderer = candidate;
                return crestOceanRenderer;
            }

            return null;
        }

        private static float ResolveSeaLevel(Crest.OceanRenderer oceanRenderer)
        {
            if (oceanRenderer != null && oceanRenderer.Root != null)
                return oceanRenderer.Root.position.y;

            HectonFluidEngine fluidEngine = HectonFluidEngine.Instance;
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

        private static bool ValidateVectorRequest(Vector3[] samplePositions, int sampleCount, Vector3[] vectors)
        {
            return samplePositions != null &&
                   vectors != null &&
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
    }
}
