using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Interaction
{
    /// <summary>
    /// Tool runtime state bits captured inside interaction packets.
    /// </summary>
    [System.Flags]
    public enum ToolStateBits : byte
    {
        Idle = 0x01,
        Active = 0x02,
        Busy = 0x04,
        Overheated = 0x08,
        LowPower = 0x10,
        TargetLock = 0x20,
        Cooldown = 0x40,
    }

    /// <summary>
    /// Logical tool action mode.
    /// </summary>
    public enum ToolActionMode : byte
    {
        Primary = 0,
        Secondary = 1,
        Alt = 2,
    }

    /// <summary>
    /// Supported queued interaction effect families.
    /// </summary>
    public enum InteractionEffectType : byte
    {
        Drill = 0,
        Harpoon = 1,
        Weld = 2,
        PlasmaCut = 3,
        Torch = 4,
        Boil = 5,
    }

    /// <summary>
    /// Stable bitmask capability ids for first-party tool contracts.
    /// </summary>
    public static class ToolCapabilityMasks
    {
        public const uint Cut = 1u << 0;
        public const uint Drill = 1u << 1;
        public const uint Grab = 1u << 2;
        public const uint Stun = 1u << 3;
        public const uint Burn = 1u << 4;
        public const uint Laser = 1u << 5;
        public const uint Bash = 1u << 6;
        public const uint PlasmaCut = Cut | Burn | Laser;

        public static uint ResolveCapabilityMask(InteractionEffectType effectType)
        {
            switch (effectType)
            {
                case InteractionEffectType.Drill:
                    return Drill;

                case InteractionEffectType.Harpoon:
                    return Grab | Bash;

                case InteractionEffectType.Weld:
                case InteractionEffectType.Torch:
                case InteractionEffectType.Boil:
                    return Burn | Laser;

                case InteractionEffectType.PlasmaCut:
                    return PlasmaCut;

                default:
                    return 0u;
            }
        }
    }

    /// <summary>
    /// Optional physical-vulnerability contract used by tool-routing owners before applying damage.
    /// </summary>
    public interface IInteractionVulnerabilitySource
    {
        /// <summary>
        /// Bitmask of supported tool interaction capabilities for this target.
        /// </summary>
        uint VulnerabilityMask { get; }
    }

    /// <summary>
    /// Immutable tool dispatch payload captured before routing into the interaction queue.
    /// All positions are absolute-universe coordinates.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 48)]
    public struct InteractionPacket
    {
        public InteractionPacket(
            uint toolId,
            float3 origin,
            float3 direction,
            float power,
            float range,
            byte mode,
            byte toolStateFlags,
            uint frameIndex)
        {
            ToolID = toolId;
            Origin = origin;
            Direction = direction;
            Power = power;
            Range = range;
            Mode = mode;
            ToolStateFlags = toolStateFlags;
            FrameIndex = frameIndex;
            _padding0 = (ushort)0;
        }

        [FieldOffset(0)]
        public uint ToolID;
        [FieldOffset(4)]
        public float3 Origin;
        [FieldOffset(16)]
        public float3 Direction;
        [FieldOffset(28)]
        public float Power;
        [FieldOffset(32)]
        public float Range;
        [FieldOffset(36)]
        public byte Mode;
        [FieldOffset(37)]
        public byte ToolStateFlags;
        [FieldOffset(40)]
        public uint FrameIndex;
        [FieldOffset(44)]
        private uint _padding0;
    }

    /// <summary>
    /// Queued interaction event consumed by the authoritative late-frame dispatch owner.
    /// All positions are absolute-universe coordinates.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 88)]
    public struct InteractionSignal
    {
        public InteractionSignal(
            InteractionPacket source,
            int targetInstanceId,
            float3 hitPoint,
            float3 hitNormal,
            float powerDelivered,
            byte effectType,
            byte penetrationOccurred)
        {
            Source = source;
            TargetInstanceID = targetInstanceId;
            HitPoint = hitPoint;
            HitNormal = hitNormal;
            PowerDelivered = powerDelivered;
            EffectType = effectType;
            PenetrationOccurred = penetrationOccurred;
            _padding0 = 0;
            _padding1 = 0u;
        }

        [FieldOffset(0)]
        public InteractionPacket Source;
        [FieldOffset(48)]
        public int TargetInstanceID;
        [FieldOffset(52)]
        public float3 HitPoint;
        [FieldOffset(64)]
        public float3 HitNormal;
        [FieldOffset(76)]
        public float PowerDelivered;
        [FieldOffset(80)]
        public byte EffectType;
        [FieldOffset(81)]
        public byte PenetrationOccurred;
        [FieldOffset(82)]
        private ushort _padding0;
        [FieldOffset(84)]
        private uint _padding1;
    }

    /// <summary>
    /// Minimal tool contract required by the interaction subsystem.
    /// </summary>
    public interface IToolModule
    {
        /// <summary>
        /// Transitions the tool into its active state.
        /// </summary>
        void Activate();

        /// <summary>
        /// Transitions the tool out of its active state.
        /// </summary>
        void Deactivate();

        /// <summary>
        /// Cancels the current tool action and resets runtime state.
        /// </summary>
        void CancelAction();

        /// <summary>
        /// Returns the stable capability mask published by this tool.
        /// </summary>
        /// <returns>Bitmask of authored capabilities.</returns>
        uint GetCapabilityMask();
    }

    /// <summary>
    /// Optional cut-target contract for systems that need the full queued interaction payload.
    /// </summary>
    public interface IInteractionSignalConsumer
    {
        /// <summary>
        /// Applies one deferred interaction signal after the queue owner resolves the final target.
        /// </summary>
        /// <param name="signal">Authoritative interaction payload.</param>
        /// <param name="runtimeHitPoint">Runtime-space hit point.</param>
        void ApplyInteractionSignal(in InteractionSignal signal, Vector3 runtimeHitPoint);
    }
}
