using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Physics;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Registry-facing immutable submarine motion and ballast snapshot.
    /// </summary>
    public struct SubmarineStateSnapshot
    {
        public float3 RuntimePosition;
        public quaternion RuntimeRotation;
        public float3 LinearVelocity;
        public float3 AngularVelocity;
        public float3 CenterOfMassLocal;
        public float BaseMassKg;
        public float BallastWaterMassKg;
        public float TotalCargoMassKg;
        public float PidIntegralWindup;
        public byte MathLod;
        public byte PumpPowered;
        public byte AutoLevelActive;
        public uint Frame;
    }

    /// <summary>
    /// Narrow read model for submarine stabilizer, ballast, and HUD telemetry consumers.
    /// </summary>
    public interface ISubmarineState : Hecton8.Core.ISystem
    {
        /// <summary>SOA ballast fill state. Four entries on normal tiers; low tier mirrors one master scalar.</summary>
        NativeArray<float>.ReadOnly BallastFill01 { get; }

        /// <summary>Latest fixed-step state snapshot owned by the submarine controller.</summary>
        SubmarineStateSnapshot StateSnapshot { get; }
    }

    /// <summary>
    /// Authoritative runtime contract for the active submarine root.
    /// </summary>
    /// <remarks>
    /// Keeps registry consumers on a narrow interface while the root stays a thin coordinator.
    /// Subsystems such as flooding, atmosphere, and hull integrity remain owned by their dedicated components.
    /// </remarks>
    public interface ISubmarineRuntimeContext : ITransportPlatform
    {
        /// <summary>Authoritative rigidbody driving hull motion and point velocity.</summary>
        Rigidbody HullRigidbody { get; }

        /// <summary>Optional fixed-step flooding owner attached to this submarine root.</summary>
        SubmarineFluidDynamics FluidDynamics { get; }

        /// <summary>Optional localized water-heat command facade owned by the fluid runtime.</summary>
        IWaterHeatInjectionService WaterHeatInjectionService { get; }

        /// <summary>Optional fixed-step atmosphere owner attached to this submarine root.</summary>
        ISubmarineAtmosphereRoomReadModel AtmosphereSystem { get; }

        /// <summary>Optional structural breach read/write owner attached to this submarine root.</summary>
        SubmarineStructuralGrid StructuralGrid { get; }

        /// <summary>Thermodynamics-owned thrust/top-speed scalar. One is neutral.</summary>
        float ThermalSpeedMultiplier { get; }

        /// <summary>Resolved certified operating depth in meters after upgrades.</summary>
        float MaxDepthMeters { get; }

        /// <summary>Applies a bounded thermodynamics slowdown without coupling thermodynamics to a concrete submarine class.</summary>
        void SetThermalSpeedMultiplier(float multiplier);
    }
}
