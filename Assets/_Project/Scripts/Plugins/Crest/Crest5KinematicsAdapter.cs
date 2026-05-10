using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using WaveHarmonic.Crest;
using Hecton8.Core;

namespace Hecton8.Physics
{
    /// <summary>
    /// Crest 5-backed implementation of <see cref="IHectonOceanKinematics"/>.
    /// Keeps Crest 5 query ownership and batch sampling outside gameplay controllers.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Physics/Crest 5 Kinematics Adapter")]
    public sealed class Crest5KinematicsAdapter : CrestBridge
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
        public override int Priority => ProviderPriority;

        /// <inheritdoc />
        public override bool IsAvailable
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
        public override float SeaLevel => ResolveSeaLevel(ResolveWaterRenderer(forceSceneSearch: false));

        /// <inheritdoc />
        public override bool TryGetSurfaceWeatherState(out HectonOceanSurfaceWeatherState state)
        {
            state = default;
            WaterRenderer waterRenderer = ResolveWaterRenderer(forceSceneSearch: true);
            if (waterRenderer == null)
                return false;

            state.WindSpeed = Mathf.Max(0f, waterRenderer.WindSpeed);
            state.Flags = (uint)HectonOceanSurfaceWeatherStateFlags.SupportsWindSpeed;
            return true;
        }

        /// <inheritdoc />
        public override bool ApplySurfaceWeatherState(in HectonOceanSurfaceWeatherState state)
        {
            WaterRenderer waterRenderer = ResolveWaterRenderer(forceSceneSearch: true);
            if (waterRenderer == null)
                return false;

            if ((state.Flags & (uint)HectonOceanSurfaceWeatherStateFlags.SupportsWindSpeed) != 0u)
                waterRenderer.WindSpeed = Mathf.Max(0f, state.WindSpeed);

            return true;
        }

        private void Awake()
        {
            EnsureHelpersInitialized();
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
        public override bool GetWaterHeight(NativeArray<Vector3> samplePositions, int sampleCount, float minSpatialLength, NativeArray<float> waterHeights)
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
                if (_heightSampleHelpers[i].SampleHeight(samplePositions[i], out float sampledHeight, resolvedMinSpatialLength))
                {
                    waterHeights[i] = sampledHeight;
                    continue;
                }

                waterHeights[i] = seaLevel;
                succeeded = false;
            }

            return succeeded;
        }

        /// <inheritdoc />
        public override bool GetSurfaceFlow(Vector3[] samplePositions, int sampleCount, float minSpatialLength, Vector3[] surfaceFlows)
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
        public override bool GetSurfaceFlow(NativeArray<Vector3> samplePositions, int sampleCount, float minSpatialLength, NativeArray<Vector3> surfaceFlows)
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
        public override bool GetWaveNormal(
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

        /// <inheritdoc />
        public override bool GetWaveNormal(
            NativeArray<Vector3> samplePositions,
            int sampleCount,
            float minSpatialLength,
            NativeArray<Vector3> waveNormals,
            NativeArray<Vector3> surfaceVelocities,
            NativeArray<Vector3> displacements)
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
                        out Vector3 displacement,
                        out Vector3 surfaceVelocity,
                        out Vector3 waveNormal,
                        resolvedMinSpatialLength))
                {
                    displacements[i] = displacement;
                    surfaceVelocities[i] = surfaceVelocity;
                    waveNormals[i] = waveNormal;
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

                WaterRenderer candidate = rootObject.GetComponent<WaterRenderer>();
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

            HectonFluidEngine fluidEngine = GlobalRegistry.Fluid;
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

        private static void FillHeightFallback(NativeArray<float> heights, int sampleCount, float value)
        {
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

        private static void FillVectorFallback(NativeArray<Vector3> values, int sampleCount, Vector3 fallbackValue)
        {
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
    }
}
