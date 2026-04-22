using System.Collections.Generic;
using UnityEngine;
using WaveHarmonic.Crest;

namespace Hecton8.Physics
{
    /// <summary>
    /// Crest 5-backed implementation of <see cref="IHectonOceanKinematics"/>.
    /// Keeps Crest 5 query ownership and batch sampling outside gameplay controllers.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Physics/Crest 5 Kinematics Adapter")]
    public sealed class Crest5KinematicsAdapter : MonoBehaviour, IHectonOceanKinematics
    {
        private const int MaxBatchSampleCount = 5;
        private const float DefaultSeaLevel = 4900f;
        private const int ProviderPriority = 500;

        [Header("References")]
        [Tooltip("Optional explicit Crest 5 water owner. Leave empty to resolve WaterRenderer.Instance or scan the scene.")]
        [SerializeField] private WaterRenderer crestWaterRenderer;

        [Header("Fallback / Resolve")]
        [Tooltip("Retry cadence for one-shot scene searches when WaterRenderer.Instance is not ready yet.")]
        [SerializeField, Range(0.1f, 5f)] private float sceneSearchRetryInterval = 1f;
        [Tooltip("Retry cadence for forcing a Crest 5 heartbeat when the water renderer exists but query providers are still null.")]
        [SerializeField, Range(0.1f, 5f)] private float providerHeartbeatRetryInterval = 0.5f;

        private float _nextResolveTime = float.NegativeInfinity;
        private float _nextProviderHeartbeatTime = float.NegativeInfinity;
        private readonly List<GameObject> _sceneRootBuffer =
            new List<GameObject>(16); // COLD ALLOC: List<GameObject>(16) — reusable scene root scan buffer for delayed Crest 5 owner recovery — owner: Crest5KinematicsAdapter
        private readonly SampleCollisionHelper[] _heightSampleHelpers =
            new SampleCollisionHelper[MaxBatchSampleCount]; // COLD ALLOC: SampleCollisionHelper[5] — per-point Crest 5 height query lanes for zero-GC batch sampling — owner: Crest5KinematicsAdapter
        private readonly SampleCollisionHelper[] _waveSampleHelpers =
            new SampleCollisionHelper[MaxBatchSampleCount]; // COLD ALLOC: SampleCollisionHelper[5] — per-point Crest 5 displacement/normal query lanes for zero-GC batch sampling — owner: Crest5KinematicsAdapter
        private readonly SampleFlowHelper[] _flowSampleHelpers =
            new SampleFlowHelper[MaxBatchSampleCount]; // COLD ALLOC: SampleFlowHelper[5] — per-point Crest 5 flow query lanes for zero-GC batch sampling — owner: Crest5KinematicsAdapter

        /// <inheritdoc />
        public int Priority => ProviderPriority;

        /// <inheritdoc />
        public bool IsAvailable
        {
            get
            {
                WaterRenderer waterRenderer = ResolveWaterRenderer(forceSceneSearch: false);
                if (waterRenderer == null)
                    return false;

                PrimeProvidersIfNeeded(waterRenderer, ResolveHeartbeatSamplePosition(waterRenderer), 1f, requireFlow: false);
                return waterRenderer != null &&
                       waterRenderer.AnimatedWavesLod != null &&
                       waterRenderer.AnimatedWavesLod.Enabled &&
                       waterRenderer.AnimatedWavesLod.CollisionSource != CollisionSource.None &&
                       waterRenderer.CollisionProvider != null;
            }
        }

        /// <inheritdoc />
        public float SeaLevel => ResolveSeaLevel(ResolveWaterRenderer(forceSceneSearch: false));

        private void Awake()
        {
            EnsureHelpersInitialized();
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
            EnsureHelpersInitialized();

            if (!ValidateHeightRequest(samplePositions, sampleCount, waterHeights))
                return false;

            WaterRenderer waterRenderer = ResolveWaterRenderer(forceSceneSearch: true);
            if (waterRenderer == null)
            {
                FillHeightFallback(waterHeights, sampleCount, DefaultSeaLevel);
                return false;
            }

            float resolvedMinSpatialLength = Mathf.Max(0.01f, minSpatialLength);
            PrimeProvidersIfNeeded(waterRenderer, samplePositions[0], resolvedMinSpatialLength, requireFlow: false);
            bool succeeded = true;
            float seaLevel = ResolveSeaLevel(waterRenderer);
            for (int i = 0; i < sampleCount; i++)
            {
                if (_heightSampleHelpers[i].SampleHeight(samplePositions[i], out waterHeights[i], resolvedMinSpatialLength))
                    continue;

                waterHeights[i] = seaLevel;
                succeeded = false;
            }

            return succeeded;
        }

        /// <inheritdoc />
        public bool GetSurfaceFlow(Vector3[] samplePositions, int sampleCount, float minSpatialLength, Vector3[] surfaceFlows)
        {
            EnsureHelpersInitialized();

            if (!ValidateVectorRequest(samplePositions, sampleCount, surfaceFlows))
                return false;

            WaterRenderer waterRenderer = ResolveWaterRenderer(forceSceneSearch: true);
            if (waterRenderer == null || waterRenderer.FlowLod == null || !waterRenderer.FlowLod.Enabled)
            {
                FillVectorFallback(surfaceFlows, sampleCount, Vector3.zero);
                return false;
            }

            float resolvedMinSpatialLength = Mathf.Max(0.01f, minSpatialLength);
            PrimeProvidersIfNeeded(waterRenderer, samplePositions[0], resolvedMinSpatialLength, requireFlow: true);
            bool succeeded = true;
            for (int i = 0; i < sampleCount; i++)
            {
                if (_flowSampleHelpers[i].Sample(samplePositions[i], out Vector2 flow, resolvedMinSpatialLength))
                {
                    surfaceFlows[i] = new Vector3(flow.x, 0f, flow.y);
                    continue;
                }

                surfaceFlows[i] = Vector3.zero;
                succeeded = false;
            }

            return succeeded;
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
            EnsureHelpersInitialized();

            if (!ValidateWaveRequest(samplePositions, sampleCount, waveNormals, surfaceVelocities, displacements))
                return false;

            WaterRenderer waterRenderer = ResolveWaterRenderer(forceSceneSearch: true);
            if (waterRenderer == null)
            {
                FillVectorFallback(waveNormals, sampleCount, Vector3.up);
                FillVectorFallback(surfaceVelocities, sampleCount, Vector3.zero);
                FillVectorFallback(displacements, sampleCount, Vector3.zero);
                return false;
            }

            float resolvedMinSpatialLength = Mathf.Max(0.01f, minSpatialLength);
            PrimeProvidersIfNeeded(waterRenderer, samplePositions[0], resolvedMinSpatialLength, requireFlow: false);
            bool succeeded = true;
            for (int i = 0; i < sampleCount; i++)
            {
                if (_waveSampleHelpers[i].SampleDisplacement(
                        samplePositions[i],
                        out displacements[i],
                        out surfaceVelocities[i],
                        out waveNormals[i],
                        resolvedMinSpatialLength))
                {
                    continue;
                }

                displacements[i] = Vector3.zero;
                surfaceVelocities[i] = Vector3.zero;
                waveNormals[i] = Vector3.up;
                succeeded = false;
            }

            return succeeded;
        }

        private void PrimeProvidersIfNeeded(WaterRenderer waterRenderer, Vector3 samplePosition, float minSpatialLength, bool requireFlow)
        {
            if (waterRenderer == null)
                return;

            bool collisionReady = waterRenderer.CollisionProvider != null;
            bool flowExpected = requireFlow && waterRenderer.FlowLod != null && waterRenderer.FlowLod.Enabled;
            bool flowReady = !flowExpected || waterRenderer.FlowProvider != null;
            if (collisionReady && flowReady)
                return;

            TryPrimeQueryHelpers(samplePosition, minSpatialLength, flowExpected);

            collisionReady = waterRenderer.CollisionProvider != null;
            flowReady = !flowExpected || waterRenderer.FlowProvider != null;
            if (collisionReady && flowReady)
                return;

            if (!Application.isPlaying)
                return;

            float now = Time.unscaledTime;
            if (now < _nextProviderHeartbeatTime)
                return;

            _nextProviderHeartbeatTime = now + Mathf.Max(0.1f, providerHeartbeatRetryInterval);
            bool wasEnabled = waterRenderer.enabled;
            waterRenderer.enabled = false;
            waterRenderer.enabled = wasEnabled;
            TryPrimeQueryHelpers(samplePosition, minSpatialLength, flowExpected);
        }

        private void EnsureHelpersInitialized()
        {
            for (int i = 0; i < MaxBatchSampleCount; i++)
            {
                _heightSampleHelpers[i] ??= new SampleCollisionHelper();
                _waveSampleHelpers[i] ??= new SampleCollisionHelper();
                _flowSampleHelpers[i] ??= new SampleFlowHelper();
            }
        }

        private void TryPrimeQueryHelpers(Vector3 samplePosition, float minSpatialLength, bool requireFlow)
        {
            float resolvedMinSpatialLength = Mathf.Max(0.01f, minSpatialLength);
            _heightSampleHelpers[0].SampleHeight(samplePosition, out _, resolvedMinSpatialLength);
            _waveSampleHelpers[0].SampleDisplacement(samplePosition, out _, out _, out _, resolvedMinSpatialLength);
            if (requireFlow)
                _flowSampleHelpers[0].Sample(samplePosition, out _, resolvedMinSpatialLength);
        }

        private Vector3 ResolveHeartbeatSamplePosition(WaterRenderer waterRenderer)
        {
            Vector3 samplePosition = waterRenderer != null ? waterRenderer.transform.position : transform.position;
            samplePosition.y = ResolveSeaLevel(waterRenderer);
            return samplePosition;
        }

        private WaterRenderer ResolveWaterRenderer(bool forceSceneSearch)
        {
            if (crestWaterRenderer != null)
                return crestWaterRenderer;

            WaterRenderer instance = WaterRenderer.Instance;
            if (instance != null)
            {
                crestWaterRenderer = instance;
                return crestWaterRenderer;
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

                WaterRenderer candidate = rootObject.GetComponentInChildren<WaterRenderer>(true);
                if (candidate == null)
                    continue;

                crestWaterRenderer = candidate;
                return crestWaterRenderer;
            }

            return null;
        }

        private static float ResolveSeaLevel(WaterRenderer waterRenderer)
        {
            if (waterRenderer != null)
                return waterRenderer.SeaLevel;

            HectonFluidEngine fluidEngine = HectonFluidEngine.Instance;
            return fluidEngine != null ? fluidEngine.WaterLevel : DefaultSeaLevel;
        }

        private static void FillHeightFallback(float[] heights, int sampleCount, float value)
        {
            if (heights == null)
                return;

            int resolvedCount = Mathf.Min(sampleCount, heights.Length);
            for (int i = 0; i < resolvedCount; i++)
                heights[i] = value;
        }

        private static void FillVectorFallback(Vector3[] values, int sampleCount, Vector3 fallbackValue)
        {
            if (values == null)
                return;

            int resolvedCount = Mathf.Min(sampleCount, values.Length);
            for (int i = 0; i < resolvedCount; i++)
                values[i] = fallbackValue;
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
