using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.AI
{
    /// <summary>
    /// Authoring template for fauna spawn/runtime descriptors.
    /// Builds a blittable payload that can be copied into SOA lanes without pulling managed authoring state into hot paths.
    /// </summary>
    [CreateAssetMenu(fileName = "FaunaDataTemplate_", menuName = "Hecton8/Fauna/Data Template")]
    public sealed class FaunaDataTemplate : ScriptableObject
    {
        [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 64)]
        public struct RuntimeDescriptor
        {
            public int SpeciesId;
            public float MassKg;
            public float BodyRadiusMeters;
            public float CruiseSpeedMetersPerSecond;
            public float MaxSpeedMetersPerSecond;
            public float SteeringResponse;
            public float4 VatPositionScaleBias;
            public float4 VatNormalScaleBias;
            public float4 VatPhaseOffsetScale;
            public uint DefaultBoidStateMask;
            public uint SpawnFlags;
            public int MaxSchoolCount;
            public int Reserved0;
        }

        [Header("Identity")]
        [SerializeField, Tooltip("Stable species identifier mirrored into runtime SOA descriptors.")]
        private int speciesId;

        [SerializeField, Tooltip("Optional high-level behavior profile linked to this spawn template.")]
        private FaunaSpeciesProfile speciesProfile;

        [Header("Physics")]
        [SerializeField, Min(0.01f), Tooltip("Baseline body mass used by custom fauna kinematics and pushback lanes.")]
        private float massKg = 12f;

        [SerializeField, Min(0.01f), Tooltip("Broadphase radius used by fauna separation, steering and avoidance.")]
        private float bodyRadiusMeters = 0.65f;

        [SerializeField, Min(0.01f), Tooltip("Nominal cruise speed written into the runtime descriptor.")]
        private float cruiseSpeedMetersPerSecond = 2.5f;

        [SerializeField, Min(0.01f), Tooltip("Maximum chase/flee speed written into the runtime descriptor.")]
        private float maxSpeedMetersPerSecond = 4.5f;

        [SerializeField, Min(0.01f), Tooltip("Scalar used by steering jobs to tune turn response without reading authoring assets.")]
        private float steeringResponse = 1.25f;

        [Header("VAT")]
        [SerializeField, Tooltip("Scale/bias payload for VAT position sampling: xy = scale, zw = bias.")]
        private Vector4 vatPositionScaleBias = new Vector4(1f, 1f, 0f, 0f);

        [SerializeField, Tooltip("Scale/bias payload for VAT normal sampling: xy = scale, zw = bias.")]
        private Vector4 vatNormalScaleBias = new Vector4(1f, 1f, 0f, 0f);

        [SerializeField, Tooltip("Per-spawn VAT phase and offset payload: x = phase scale, y = frame offset, z = playback bias, w = reserved.")]
        private Vector4 vatPhaseOffsetScale = new Vector4(1f, 0f, 0f, 0f);

        [Header("Boid Defaults")]
        [SerializeField, Tooltip("Default boid-state bitmask copied into runtime descriptors for GPU or Burst flock lanes.")]
        private uint defaultBoidStateMask = 0x00000003u;

        [SerializeField, Tooltip("Template-level spawn flags packed into the runtime descriptor for zero-branch initialization.")]
        private uint spawnFlags;

        [SerializeField, Min(1), Tooltip("Upper bound for local school size spawned from this template.")]
        private int maxSchoolCount = 12;

        /// <summary>
        /// Stable species identifier for gameplay-side lookups.
        /// </summary>
        public int SpeciesId => speciesId;

        /// <summary>
        /// Optional high-level species profile linked to this template.
        /// </summary>
        public FaunaSpeciesProfile SpeciesProfile => speciesProfile;

        /// <summary>
        /// Builds the blittable runtime descriptor consumed by SOA-friendly fauna systems.
        /// </summary>
        public RuntimeDescriptor BuildRuntimeDescriptor()
        {
            return new RuntimeDescriptor
            {
                SpeciesId = speciesId,
                MassKg = math.max(0.01f, massKg),
                BodyRadiusMeters = math.max(0.01f, bodyRadiusMeters),
                CruiseSpeedMetersPerSecond = math.max(0.01f, cruiseSpeedMetersPerSecond),
                MaxSpeedMetersPerSecond = math.max(cruiseSpeedMetersPerSecond, maxSpeedMetersPerSecond),
                SteeringResponse = math.max(0.01f, steeringResponse),
                VatPositionScaleBias = new float4(vatPositionScaleBias.x, vatPositionScaleBias.y, vatPositionScaleBias.z, vatPositionScaleBias.w),
                VatNormalScaleBias = new float4(vatNormalScaleBias.x, vatNormalScaleBias.y, vatNormalScaleBias.z, vatNormalScaleBias.w),
                VatPhaseOffsetScale = new float4(vatPhaseOffsetScale.x, vatPhaseOffsetScale.y, vatPhaseOffsetScale.z, vatPhaseOffsetScale.w),
                DefaultBoidStateMask = defaultBoidStateMask,
                SpawnFlags = spawnFlags,
                MaxSchoolCount = math.max(1, maxSchoolCount),
                Reserved0 = 0
            };
        }
    }
}
