using System;
using UnityEngine;

namespace Hecton8.Core.Contracts
{
    /// <summary>
    /// Three coarse AI simulation bands resolved by the central foveated director.
    /// </summary>
    public enum FoveatedSimulationTier : byte
    {
        Active = 0,
        Peripheral = 1,
        Frozen = 2
    }

    /// <summary>
    /// Registry-published foveated simulation read model. Implementations must keep frame paths allocation-free.
    /// </summary>
    public interface IFoveatedSimulationDirector : IDisposable
    {
        /// <summary>Current number of frozen registered entities.</summary>
        int FrozenEntityCount { get; }

        /// <summary>Reads the last resolved tier for a registered entity slot.</summary>
        bool TryGetEntityTier(int targetIndex, out FoveatedSimulationTier tier);

        /// <summary>Resolves the current foveated tier for an arbitrary runtime position.</summary>
        FoveatedSimulationTier ResolveTierForPosition(Vector3 runtimePosition);

        /// <summary>Forces one entity into Tier0 for a finite duration after authoritative combat contact.</summary>
        void LockTier0(uint entityHash, ushort entityId, float seconds);

        /// <summary>Applies a thermal override for the Tier2 freeze threshold. Call with inactive to restore scalability defaults.</summary>
        void SetThermalFreezeDistanceOverride(bool active, float frozenDistanceMeters);
    }
}
