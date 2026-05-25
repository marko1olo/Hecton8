using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Physics
{
    internal static class HectonOceanKinematicsLayout
    {
        public const int SurfaceWeatherStateStrideBytes = 32;
    }

    [System.Flags]
    public enum HectonOceanSurfaceWeatherStateFlags : uint
    {
        None = 0u,
        SupportsWindSpeed = 1u << 0,
        SupportsFoamStrength = 1u << 1,
        SupportsFoamCoverage = 1u << 2,
        SupportsFoamScale = 1u << 3
    }

    [StructLayout(LayoutKind.Explicit, Size = HectonOceanKinematicsLayout.SurfaceWeatherStateStrideBytes)]
    public struct HectonOceanSurfaceWeatherState
    {
        [FieldOffset(0)] public float WindSpeed;
        [FieldOffset(4)] public float FoamStrength;
        [FieldOffset(8)] public float FoamCoverage;
        [FieldOffset(12)] public float FoamScale;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] private uint _pad0;
        [FieldOffset(24)] private ulong _pad1;
    }

    /// <summary>
    /// Abstracts runtime ocean kinematics queries away from a specific ocean backend.
    /// Caller owns all batch buffers; implementations must write into those buffers without allocating.
    /// Supports up to the player controller's 5-point body sampling batch.
    /// </summary>
    public interface IHectonOceanKinematics
    {
        /// <summary>
        /// Provider-selection priority used by the global ocean registry. Higher wins.
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// True when the underlying ocean backend can answer collision/flow queries this frame.
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Current water level fallback used when fine sampling is unavailable.
        /// </summary>
        float SeaLevel { get; }

        /// <summary>
        /// Captures backend-owned surface-weather controls without exposing third-party renderer types.
        /// </summary>
        bool TryGetSurfaceWeatherState(out HectonOceanSurfaceWeatherState state);

        /// <summary>
        /// Applies backend-owned surface-weather controls without exposing third-party renderer types.
        /// </summary>
        bool ApplySurfaceWeatherState(in HectonOceanSurfaceWeatherState state);

        /// <summary>
        /// Applies the runtime primary light into the ocean backend when that backend owns sun-light binding.
        /// </summary>
        bool TryAssignPrimaryLight(Light primaryLight);

        /// <summary>
        /// Samples water height at one runtime position without exposing backend query ownership to gameplay.
        /// </summary>
        bool TrySampleWaveHeight(float3 position, float minSpatialLength, out float waterHeight);

        /// <summary>
        /// Samples one water-surface flow vector at the requested runtime position.
        /// </summary>
        bool TrySampleSurfaceFlow(float3 position, float minSpatialLength, out float3 surfaceFlow);

        /// <summary>
        /// Samples one surface-velocity vector at the requested runtime position.
        /// </summary>
        bool TrySampleWaterVelocity(float3 position, float minSpatialLength, out float3 waterVelocity);

        /// <summary>
        /// Samples one full wave-kinematics payload at the requested runtime position.
        /// </summary>
        bool TrySampleWaveKinematics(
            float3 position,
            float minSpatialLength,
            out float waterHeight,
            out float3 waveNormal,
            out float3 surfaceVelocity,
            out float3 displacement);

        /// <summary>
        /// Samples water height for a caller-owned native point batch.
        /// </summary>
        bool GetWaterHeight(NativeArray<Vector3> samplePositions, int sampleCount, float minSpatialLength, NativeArray<float> waterHeights);

        /// <summary>
        /// Samples water height for a caller-owned point batch.
        /// </summary>
        bool GetWaterHeight(Vector3[] samplePositions, int sampleCount, float minSpatialLength, float[] waterHeights);

        /// <summary>
        /// Samples horizontal/vertical authored surface flow for a caller-owned native point batch.
        /// </summary>
        bool GetSurfaceFlow(NativeArray<Vector3> samplePositions, int sampleCount, float minSpatialLength, NativeArray<Vector3> surfaceFlows);

        /// <summary>
        /// Samples horizontal/vertical authored surface flow for a caller-owned point batch.
        /// </summary>
        bool GetSurfaceFlow(Vector3[] samplePositions, int sampleCount, float minSpatialLength, Vector3[] surfaceFlows);

        /// <summary>
        /// Samples per-point wave normals plus surface velocity/displacement for a caller-owned native point batch.
        /// </summary>
        bool GetWaveNormal(
            NativeArray<Vector3> samplePositions,
            int sampleCount,
            float minSpatialLength,
            NativeArray<Vector3> waveNormals,
            NativeArray<Vector3> surfaceVelocities,
            NativeArray<Vector3> displacements);

        /// <summary>
        /// Samples per-point wave normals plus surface velocity/displacement for a caller-owned point batch.
        /// </summary>
        bool GetWaveNormal(
            Vector3[] samplePositions,
            int sampleCount,
            float minSpatialLength,
            Vector3[] waveNormals,
            Vector3[] surfaceVelocities,
            Vector3[] displacements);
    }

}
