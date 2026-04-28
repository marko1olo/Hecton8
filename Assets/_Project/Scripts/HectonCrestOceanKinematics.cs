using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Physics
{
    /// <summary>
    /// Shared anti-corruption base for Crest-backed ocean providers.
    /// Gameplay talks only to <see cref="IHectonOceanKinematics"/> while Crest-specific query ownership stays here.
    /// </summary>
    public abstract class HectonCrestOceanKinematics : MonoBehaviour, IHectonOceanKinematics
    {
        // COLD ALLOC: Vector3[1] - single-sample ocean query position scratch for interface convenience methods - owner: HectonCrestOceanKinematics
        private readonly Vector3[] _singleSamplePosition = new Vector3[1];
        // COLD ALLOC: float[1] - single-sample ocean height scratch for interface convenience methods - owner: HectonCrestOceanKinematics
        private readonly float[] _singleSampleHeight = new float[1];
        // COLD ALLOC: Vector3[1] - single-sample ocean flow scratch for interface convenience methods - owner: HectonCrestOceanKinematics
        private readonly Vector3[] _singleSampleFlow = new Vector3[1];
        // COLD ALLOC: Vector3[1] - single-sample ocean normal scratch for interface convenience methods - owner: HectonCrestOceanKinematics
        private readonly Vector3[] _singleSampleNormal = new Vector3[1];
        // COLD ALLOC: Vector3[1] - single-sample surface velocity scratch for interface convenience methods - owner: HectonCrestOceanKinematics
        private readonly Vector3[] _singleSampleVelocity = new Vector3[1];
        // COLD ALLOC: Vector3[1] - single-sample displacement scratch for interface convenience methods - owner: HectonCrestOceanKinematics
        private readonly Vector3[] _singleSampleDisplacement = new Vector3[1];

        /// <inheritdoc />
        public abstract int Priority { get; }

        /// <inheritdoc />
        public abstract bool IsAvailable { get; }

        /// <inheritdoc />
        public abstract float SeaLevel { get; }

        /// <inheritdoc />
        public virtual bool TryGetSurfaceWeatherState(out HectonOceanSurfaceWeatherState state)
        {
            state = default;
            return false;
        }

        /// <inheritdoc />
        public virtual bool ApplySurfaceWeatherState(in HectonOceanSurfaceWeatherState state)
        {
            return false;
        }

        /// <inheritdoc />
        public virtual bool TryAssignPrimaryLight(Light primaryLight)
        {
            return false;
        }

        /// <inheritdoc />
        public abstract bool GetWaterHeight(NativeArray<Vector3> samplePositions, int sampleCount, float minSpatialLength, NativeArray<float> waterHeights);

        /// <inheritdoc />
        public abstract bool GetWaterHeight(Vector3[] samplePositions, int sampleCount, float minSpatialLength, float[] waterHeights);

        /// <inheritdoc />
        public abstract bool GetSurfaceFlow(NativeArray<Vector3> samplePositions, int sampleCount, float minSpatialLength, NativeArray<Vector3> surfaceFlows);

        /// <inheritdoc />
        public abstract bool GetSurfaceFlow(Vector3[] samplePositions, int sampleCount, float minSpatialLength, Vector3[] surfaceFlows);

        /// <inheritdoc />
        public abstract bool GetWaveNormal(
            NativeArray<Vector3> samplePositions,
            int sampleCount,
            float minSpatialLength,
            NativeArray<Vector3> waveNormals,
            NativeArray<Vector3> surfaceVelocities,
            NativeArray<Vector3> displacements);

        /// <inheritdoc />
        public abstract bool GetWaveNormal(
            Vector3[] samplePositions,
            int sampleCount,
            float minSpatialLength,
            Vector3[] waveNormals,
            Vector3[] surfaceVelocities,
            Vector3[] displacements);

        /// <inheritdoc />
        public bool TrySampleWaveHeight(float3 position, float minSpatialLength, out float waterHeight)
        {
            _singleSamplePosition[0] = new Vector3(position.x, position.y, position.z);
            bool succeeded = GetWaterHeight(_singleSamplePosition, 1, minSpatialLength, _singleSampleHeight);
            waterHeight = succeeded ? _singleSampleHeight[0] : SeaLevel;
            return succeeded;
        }

        /// <inheritdoc />
        public bool TrySampleSurfaceFlow(float3 position, float minSpatialLength, out float3 surfaceFlow)
        {
            _singleSamplePosition[0] = new Vector3(position.x, position.y, position.z);
            bool succeeded = GetSurfaceFlow(_singleSamplePosition, 1, minSpatialLength, _singleSampleFlow);
            Vector3 fallback = succeeded ? _singleSampleFlow[0] : Vector3.zero;
            surfaceFlow = new float3(fallback.x, fallback.y, fallback.z);
            return succeeded;
        }

        /// <inheritdoc />
        public bool TrySampleWaterVelocity(float3 position, float minSpatialLength, out float3 waterVelocity)
        {
            _singleSamplePosition[0] = new Vector3(position.x, position.y, position.z);
            bool succeeded = GetWaveNormal(
                _singleSamplePosition,
                1,
                minSpatialLength,
                _singleSampleNormal,
                _singleSampleVelocity,
                _singleSampleDisplacement);
            Vector3 fallback = succeeded ? _singleSampleVelocity[0] : Vector3.zero;
            waterVelocity = new float3(fallback.x, fallback.y, fallback.z);
            return succeeded;
        }

        /// <inheritdoc />
        public bool TrySampleWaveKinematics(
            float3 position,
            float minSpatialLength,
            out float waterHeight,
            out float3 waveNormal,
            out float3 surfaceVelocity,
            out float3 displacement)
        {
            _singleSamplePosition[0] = new Vector3(position.x, position.y, position.z);

            bool heightSucceeded = GetWaterHeight(_singleSamplePosition, 1, minSpatialLength, _singleSampleHeight);
            bool waveSucceeded = GetWaveNormal(
                _singleSamplePosition,
                1,
                minSpatialLength,
                _singleSampleNormal,
                _singleSampleVelocity,
                _singleSampleDisplacement);

            waterHeight = heightSucceeded ? _singleSampleHeight[0] : SeaLevel;

            Vector3 sampledNormal = waveSucceeded ? _singleSampleNormal[0] : Vector3.up;
            Vector3 sampledVelocity = waveSucceeded ? _singleSampleVelocity[0] : Vector3.zero;
            Vector3 sampledDisplacement = waveSucceeded ? _singleSampleDisplacement[0] : Vector3.zero;
            waveNormal = new float3(sampledNormal.x, sampledNormal.y, sampledNormal.z);
            surfaceVelocity = new float3(sampledVelocity.x, sampledVelocity.y, sampledVelocity.z);
            displacement = new float3(sampledDisplacement.x, sampledDisplacement.y, sampledDisplacement.z);
            return heightSucceeded & waveSucceeded;
        }
    }
}
