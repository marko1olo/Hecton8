using UnityEngine;

namespace Hecton8.Core.Contracts
{
    /// <summary>
    /// Read-only structural breach publication contract consumed by flooding, audio, and hull VFX systems.
    /// </summary>
    public interface ISubmarineHullBreachReadModel
    {
        /// <summary>True when the structural grid has initialized its native state and published buffers.</summary>
        bool IsReady { get; }

        /// <summary>Number of 64-bit words in the published hull breach mask.</summary>
        int BreachMaskWordCount { get; }

        /// <summary>Current active local-space breach count available for visual repair coupling.</summary>
        int ActiveBreachCount { get; }

        /// <summary>Current normalized structural fatigue peak for non-owning presentation consumers.</summary>
        float FatiguePeakNormalized { get; }

        /// <summary>Current normalized transient impact severity for non-owning presentation consumers.</summary>
        float RecentImpactSeverityNormalized { get; }

        /// <summary>Returns one published 64-bit word from the hull breach mask. Invalid indices return zero.</summary>
        ulong GetHullBreachMaskWord(int wordIndex);

        /// <summary>Returns the published breach area in square meters for a compartment. Invalid indices return zero.</summary>
        float GetCompartmentBreachAreaSquareMeters(int compartmentIndex);

        /// <summary>Returns one active local-space breach as xyz position and w severity. Invalid indices return false.</summary>
        bool TryGetActiveBreach(int index, out Vector4 localPointSeverity);
    }

    /// <summary>
    /// Repair-tool contract for submarine-local breach patching without exposing structural internals.
    /// </summary>
    public interface ISubmarineDamageControlTarget
    {
        /// <summary>Queues a repair hit resolved by the interaction probe lane.</summary>
        bool TryQueueRepairHit(Vector3 worldHitPoint, float deltaTime, float repairUnitsPerSecond, float intensity01);
    }

    /// <summary>
    /// Maps repair hits to gas-dynamics room indices without coupling tools to submarine internals.
    /// </summary>
    public interface ISubmarineRepairRoomResolver
    {
        /// <summary>Returns the nearest mapped compartment for a repair hit. Room ids match gas-dynamics room ids.</summary>
        bool TryResolveRepairRoom(Vector3 worldHitPoint, out int roomId);
    }
}
