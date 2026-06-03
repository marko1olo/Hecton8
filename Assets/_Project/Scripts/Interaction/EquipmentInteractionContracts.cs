using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Interaction
{
    internal static class EquipmentInteractionContractLayout
    {
        public const int ModuleRepairSnapshotStrideBytes = 32;
        public const int WfcDoorLaserCutSnapshotStrideBytes = 16;
        public const int InteractionPacketStrideBytes = 64;
        public const int InteractionSignalStrideBytes = 128;
        public const int InteractionAnchorDataStrideBytes = 64;
    }

    [Serializable]
    [StructLayout(LayoutKind.Explicit, Size = EquipmentInteractionContractLayout.InteractionAnchorDataStrideBytes)]
    public struct InteractionAnchorData
    {
        public const uint FlagActive = 1u << 0;
        public const uint FlagTwoHanded = 1u << 1;
        public const byte HandMaskLeft = 1;
        public const byte HandMaskRight = 2;
        public const byte HandMaskBoth = HandMaskLeft | HandMaskRight;
        public const byte SurfaceKindLever = 1;
        public const byte SurfaceKindValve = 2;
        public const byte SurfaceKindToggle = 3;

        [FieldOffset(0)] public float3 LocalPosition;
        [FieldOffset(12)] public float3 LocalForward;
        [FieldOffset(24)] public float3 LocalUp;
        [FieldOffset(36)] public float SnapRadiusMeters;
        [FieldOffset(40)] public uint AnchorId;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public byte HandMask;
        [FieldOffset(49)] public byte SurfaceKind;
        [FieldOffset(50)] private ushort _pad0;
        [FieldOffset(52)] private uint _pad1;
        [FieldOffset(56)] private ulong _pad2;
    }

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
    /// Marker for habitat module owners that can host attached flora interaction routing.
    /// </summary>
    public interface IBaseModuleInteractionHost
    {
    }

    [StructLayout(LayoutKind.Explicit, Size = EquipmentInteractionContractLayout.ModuleRepairSnapshotStrideBytes)]
    public struct ModuleRepairReadSnapshot
    {
        public const uint FlagFlooded = 1u << 0;
        public const uint FlagDraining = 1u << 1;
        public const uint FlagHasPower = 1u << 2;

        [FieldOffset(0)] public float CurrentIntegrity;
        [FieldOffset(4)] public float MaxIntegrity;
        [FieldOffset(8)] public uint Flags;
        [FieldOffset(12)] public uint Pad0;
        [FieldOffset(16)] public ulong Pad1;
        [FieldOffset(24)] public ulong Pad2;
    }

    /// <summary>
    /// Repair-tool command/read surface for habitat module integrity. Implementations own mutable module state.
    /// </summary>
    public interface IRepairableModuleTarget : IBaseModuleInteractionHost
    {
        bool TryReadRepairState(out ModuleRepairReadSnapshot snapshot);
        void ApplyRepair(float amount);
    }

    [StructLayout(LayoutKind.Explicit, Size = EquipmentInteractionContractLayout.WfcDoorLaserCutSnapshotStrideBytes)]
    public struct WfcDoorLaserCutReadSnapshot
    {
        [FieldOffset(0)] public ulong SectorHash;
        [FieldOffset(8)] public ushort CellIndex;
        [FieldOffset(10)] public byte CurrentFlags;
        [FieldOffset(11)] public byte Pad0;
        [FieldOffset(12)] public uint Pad1;
    }

    /// <summary>
    /// Laser-cut command/read surface for WFC-backed sealed doors. Implementations own mutable door state.
    /// </summary>
    public interface IWfcDoorLaserCutTarget
    {
        bool TryReadWfcDoorLaserCutState(out WfcDoorLaserCutReadSnapshot snapshot);
        void ApplyWfcDoorLaserCutProgress(float progress01, uint frame);
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
    [StructLayout(LayoutKind.Explicit, Size = EquipmentInteractionContractLayout.InteractionPacketStrideBytes)]
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
            _padding0 = 0;
            _padding1 = 0u;
            _padding2 = 0UL;
            _padding3 = 0UL;
        }

        [FieldOffset(0)]
        public uint ToolID;
        [FieldOffset(4)]
        public uint FrameIndex;
        [FieldOffset(8)]
        public float3 Origin;
        [FieldOffset(20)]
        public float3 Direction;
        [FieldOffset(32)]
        public float Power;
        [FieldOffset(36)]
        public float Range;
        [FieldOffset(40)]
        public byte Mode;
        [FieldOffset(41)]
        public byte ToolStateFlags;
        [FieldOffset(42)]
        private ushort _padding0;
        [FieldOffset(44)]
        private uint _padding1;
        [FieldOffset(48)]
        private ulong _padding2;
        [FieldOffset(56)]
        private ulong _padding3;
    }

    /// <summary>
    /// Queued interaction event consumed by the authoritative late-frame dispatch owner.
    /// All positions are absolute-universe coordinates.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = EquipmentInteractionContractLayout.InteractionSignalStrideBytes)]
    public struct InteractionSignal
    {
        public const byte HitPointAupDoubleValid = 1;

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
            CoordinateFlags = 0;
            _padding0 = 0;
            _padding1 = 0u;
            HitPointAupDouble = default;
        }

        public InteractionSignal(
            InteractionPacket source,
            int targetInstanceId,
            float3 hitPoint,
            float3 hitNormal,
            float powerDelivered,
            byte effectType,
            byte penetrationOccurred,
            double3 hitPointAupDouble,
            byte coordinateFlags)
        {
            Source = source;
            TargetInstanceID = targetInstanceId;
            HitPoint = hitPoint;
            HitNormal = hitNormal;
            PowerDelivered = powerDelivered;
            EffectType = effectType;
            PenetrationOccurred = penetrationOccurred;
            CoordinateFlags = math.all(math.isfinite(hitPointAupDouble))
                ? coordinateFlags
                : (byte)(coordinateFlags & ~HitPointAupDoubleValid);
            _padding0 = 0;
            _padding1 = 0u;
            HitPointAupDouble = hitPointAupDouble;
        }

        public bool TryGetHitPointAupDouble(out double3 hitPointAupDouble)
        {
            hitPointAupDouble = HitPointAupDouble;
            return (CoordinateFlags & HitPointAupDoubleValid) != 0 &&
                   math.all(math.isfinite(hitPointAupDouble));
        }

        public void SetHitPointAupDouble(double3 hitPointAupDouble)
        {
            HitPointAupDouble = hitPointAupDouble;
            if (math.all(math.isfinite(hitPointAupDouble)))
                CoordinateFlags = (byte)(CoordinateFlags | HitPointAupDoubleValid);
            else
                CoordinateFlags = (byte)(CoordinateFlags & ~HitPointAupDoubleValid);
        }

        [FieldOffset(0)]
        public InteractionPacket Source;
        [FieldOffset(64)]
        public int TargetInstanceID;
        [FieldOffset(68)]
        public float3 HitPoint;
        [FieldOffset(80)]
        public float3 HitNormal;
        [FieldOffset(92)]
        public float PowerDelivered;
        [FieldOffset(96)]
        public byte EffectType;
        [FieldOffset(97)]
        public byte PenetrationOccurred;
        [FieldOffset(98)]
        public byte CoordinateFlags;
        [FieldOffset(99)]
        private byte _padding0;
        [FieldOffset(100)]
        private uint _padding1;
        [FieldOffset(104)]
        public double3 HitPointAupDouble;
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
