using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.Modding
{
    /// <summary>
    /// Signed 64-bit integer grid coordinate for AUP payloads.
    /// Unity.Mathematics in this project does not provide long3.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct long3
    {
        /// <summary>X cell coordinate.</summary>
        public long x;

        /// <summary>Y cell coordinate.</summary>
        public long y;

        /// <summary>Z cell coordinate.</summary>
        public long z;
    }

    /// <summary>
    /// Absolute Universe Position payload accepted from sandboxed mods.
    /// Grid is measured in 5000 m cells; local is the float precision offset inside the cell.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ModAup
    {
        /// <summary>Signed 64-bit AUP cell coordinates.</summary>
        public long3 Grid;

        /// <summary>Float local offset inside <see cref="Grid"/>.</summary>
        public float3 Local;
    }

    /// <summary>
    /// AUP-backed command wrapper for every mod request that touches position.
    /// The dispatcher rebases this payload against the current floating-origin offset at drain time.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ModAupCommand
    {
        /// <summary>Base 64-byte command header/payload.</summary>
        public ModCommand Command;

        /// <summary>AUP anchor for spawn, move, effect, or ray origin.</summary>
        public ModAup Position;

        /// <summary>Optional normalized direction for ray/effect commands.</summary>
        public float3 Direction;

        /// <summary>Opcode-specific scalar, normally range, radius, or scale.</summary>
        public float Scalar;
    }

    /// <summary>
    /// Matrix submission packet for the reserved mod instancing layer.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ModRenderInstanceCommand
    {
        /// <summary>Engine-assigned mod hash. User input is overwritten at enqueue time.</summary>
        public uint ModHash;

        /// <summary>Mod-local request identifier.</summary>
        public uint RequestId;

        /// <summary>Resource hash previously returned by <see cref="IModResourceProxy"/>.</summary>
        public uint ResourceHash;

        /// <summary>Reserved flags for future material/kernel routing.</summary>
        public uint Flags;

        /// <summary>World matrix in current frame-space coordinates.</summary>
        public float4x4 Matrix;
    }

    /// <summary>
    /// Result status for sandboxed mod raycasts.
    /// </summary>
    public enum ModRaycastResultStatus : uint
    {
        /// <summary>The raycast did not hit a valid collider.</summary>
        Miss = 0,

        /// <summary>The raycast hit a collider accepted by the engine lane.</summary>
        Hit = 1,

        /// <summary>The raycast request was rejected before scheduling.</summary>
        Rejected = 2
    }

    /// <summary>
    /// Unmanaged next-frame result payload for proxied mod raycasts.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ModRaycastResultPayload
    {
        /// <summary>Stable mod hash.</summary>
        public uint ModHash;

        /// <summary>Mod-local request identifier.</summary>
        public uint RequestId;

        /// <summary><see cref="ModRaycastResultStatus"/> value.</summary>
        public uint Status;

        /// <summary>Unity instance id of the hit collider, or zero. This is diagnostic only.</summary>
        public int ColliderInstanceId;

        /// <summary>Hit collider layer, or -1 when absent.</summary>
        public int Layer;

        /// <summary>Hit distance in meters.</summary>
        public float Distance;

        /// <summary>Frame-space hit point.</summary>
        public float3 Point;

        /// <summary>Frame-space hit normal.</summary>
        public float3 Normal;
    }

    /// <summary>
    /// Unmanaged rejection event emitted when the security gate arbitrates a mod command out.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ModInteractionRejectedPayload
    {
        /// <summary>Stable mod hash.</summary>
        public uint ModHash;

        /// <summary>Mod-local request identifier.</summary>
        public uint RequestId;

        /// <summary>Rejected opcode.</summary>
        public ushort Opcode;

        /// <summary>Rejected target system.</summary>
        public ushort TargetSystem;

        /// <summary>Numeric <see cref="ModCommandRejectReason"/> code.</summary>
        public uint Reason;
    }

    /// <summary>
    /// Unmanaged event emitted before a mod is disabled by heap quota enforcement.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ModCriticalMemoryEvictionPayload
    {
        /// <summary>Stable mod hash.</summary>
        public uint ModHash;

        /// <summary>Tracked managed allocation bytes charged to this mod.</summary>
        public ulong TrackedHeapBytes;

        /// <summary>Configured quota in bytes.</summary>
        public uint LimitBytes;

        /// <summary>Reserved reason code.</summary>
        public uint Reason;
    }
}
