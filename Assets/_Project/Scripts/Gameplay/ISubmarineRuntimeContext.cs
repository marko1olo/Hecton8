using Hecton8.Atmosphere;
using Hecton8.Physics;
using UnityEngine;

namespace Hecton8.Gameplay
{
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

        /// <summary>Optional fixed-step atmosphere owner attached to this submarine root.</summary>
        SubmarineAtmosphereSystem AtmosphereSystem { get; }

        /// <summary>Optional structural breach read/write owner attached to this submarine root.</summary>
        SubmarineStructuralGrid StructuralGrid { get; }
    }
}
