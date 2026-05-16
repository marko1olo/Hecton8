using System.Runtime.InteropServices;
using Hecton8.Core.Contracts.Signals;
using Unity.Mathematics;
using UnityEngine.Scripting;

namespace Hecton8.Core.Contracts.Signals
{
    /// <summary>
    /// Diagnostic visual payload routed through an isolated <see cref="SignalBus{T}"/> lane.
    /// </summary>
    [Preserve]
    public enum DebugSignalKind : uint
    {
        PointerLink = 1u,
        GenerationId = 2u,
        CollisionNormal = 3u,
        BreadcrumbSegment = 4u,
        GasRoom = 5u,
        PressureVector = 6u,
        FluidVelocity = 7u,
        AcousticRay = 8u,
        SignalEvent = 9u,
        LaneSaturation = 10u,
        EventResonance = 11u,
        NanGeyser = 12u,
        Homeostasis = 13u,
        GhostPose = 14u,
        VramBudgetSlice = 15u,
        AupTeleportPreview = 16u
    }

    /// <summary>
    /// Blittable diagnostics payload. Position is start/center; Vector is end/direction by kind.
    /// </summary>
    [Preserve]
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]
    public struct DebugSignal : ISignal
    {
        public uint Kind;
        public uint EntityId;
        public uint ProducerId;
        public uint ConsumerId;
        public float3 Position;
        public float3 Vector;
        public float Value0;
        public float Value1;
        public uint Flags;
        public uint Frame;
        public uint Aux0;
        public uint Aux1;
    }
}

namespace Hecton8.Core.Diagnostics.Visuals
{
    /// <summary>
    /// Zero-allocation helper for publishing Architect Eye diagnostic visuals.
    /// </summary>
    [Preserve]
    public static class ArchitectEyeDebugBus
    {
        /// <summary>Ensures the isolated diagnostics lane exists before first use.</summary>
        public static void EnsureInitialized()
        {
            GlobalSignals.InitializeAllQueues();
            SignalBus<DebugSignal>.EnsureInitialized();
        }

        /// <summary>Publishes one diagnostic visual payload.</summary>
        public static void Push(in DebugSignal signal)
        {
            SignalBus<DebugSignal>.Push(in signal);
        }
    }
}
