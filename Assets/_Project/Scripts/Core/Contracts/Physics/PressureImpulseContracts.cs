using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core.Contracts.Physics
{
    /// <summary>
    /// Pressure blowout payload emitted when a bulkhead opens across a large pressure differential.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct PressureImpulseEvent
    {
        /// <summary>
        /// Creates a pressure blowout payload.
        /// </summary>
        public PressureImpulseEvent(
            int doorIndex,
            Vector3 runtimePosition,
            Vector3 direction,
            float doorAreaSquareMeters,
            float highPressureKPa,
            float lowPressureKPa,
            Vector3 forceVectorNewtons,
            Vector3 impulseVectorNewtonSeconds,
            float influenceRadiusMeters)
        {
            this = default;
            DoorIndex = doorIndex;
            RuntimePosition = runtimePosition;
            Direction = direction;
            DoorAreaSquareMeters = doorAreaSquareMeters;
            HighPressureKPa = highPressureKPa;
            LowPressureKPa = lowPressureKPa;
            PressureDeltaKPa = math.abs(highPressureKPa - lowPressureKPa);
            ForceVectorNewtons = forceVectorNewtons;
            ImpulseVectorNewtonSeconds = impulseVectorNewtonSeconds;
            InfluenceRadiusMeters = influenceRadiusMeters;
        }

        /// <summary>Bulkhead edge index inside the submarine compartment graph.</summary>
        [FieldOffset(68)]
        public int DoorIndex;

        /// <summary>Runtime-space midpoint of the opened bulkhead.</summary>
        [FieldOffset(0)]
        public Vector3 RuntimePosition;

        /// <summary>Normalized airflow direction from the high-pressure room toward the low-pressure room.</summary>
        [FieldOffset(12)]
        public Vector3 Direction;

        /// <summary>Cross-sectional doorway area used by the blowout force calculation.</summary>
        [FieldOffset(48)]
        public float DoorAreaSquareMeters;

        /// <summary>Pressure of the source room at the moment of opening.</summary>
        [FieldOffset(52)]
        public float HighPressureKPa;

        /// <summary>Pressure of the destination room at the moment of opening.</summary>
        [FieldOffset(56)]
        public float LowPressureKPa;

        /// <summary>Absolute pressure delta across the opened bulkhead.</summary>
        [FieldOffset(60)]
        public float PressureDeltaKPa;

        /// <summary>Raw force vector in newtons derived from the pressure differential.</summary>
        [FieldOffset(24)]
        public Vector3 ForceVectorNewtons;

        /// <summary>One-shot impulse vector in newton-seconds routed into the deferred physics system.</summary>
        [FieldOffset(36)]
        public Vector3 ImpulseVectorNewtonSeconds;

        /// <summary>World-space influence radius used by the local overlap dispatch.</summary>
        [FieldOffset(64)]
        public float InfluenceRadiusMeters;

        [FieldOffset(72)] private ulong _pad0;
        [FieldOffset(80)] private ulong _pad1;
        [FieldOffset(88)] private ulong _pad2;
        [FieldOffset(96)] private ulong _pad3;
        [FieldOffset(104)] private ulong _pad4;
        [FieldOffset(112)] private ulong _pad5;
        [FieldOffset(120)] private ulong _pad6;
    }

    /// <summary>
    /// Listener for deferred pressure impulse events.
    /// </summary>
    public interface IPressureImpulseEventListener
    {
        void OnPressureImpulse(in PressureImpulseEvent pressureEvent);
    }

    /// <summary>
    /// Electromagnetic pulse payload emitted by fauna or environmental hazards.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ElectromagneticPulseEvent
    {
        public ElectromagneticPulseEvent(
            Vector3 runtimePosition,
            float radiusMeters,
            float durationSeconds,
            float claritySuppression01,
            uint damageType,
            ushort sourceId)
        {
            this = default;
            RuntimePosition = runtimePosition;
            RadiusMeters = radiusMeters;
            DurationSeconds = durationSeconds;
            ClaritySuppression01 = claritySuppression01;
            DamageType = damageType;
            SourceId = sourceId;
        }

        [FieldOffset(0)]
        public Vector3 RuntimePosition;
        [FieldOffset(12)]
        public float RadiusMeters;
        [FieldOffset(16)]
        public float DurationSeconds;
        [FieldOffset(20)]
        public float ClaritySuppression01;
        [FieldOffset(24)]
        public uint DamageType;
        [FieldOffset(28)]
        public ushort SourceId;
        [FieldOffset(30)]
        private ushort _pad0;
    }

    /// <summary>
    /// Listener for deferred electromagnetic pulse events.
    /// </summary>
    public interface IElectromagneticPulseEventListener
    {
        void OnElectromagneticPulse(in ElectromagneticPulseEvent pulseEvent);
    }
}
