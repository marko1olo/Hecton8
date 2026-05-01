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
    /// Voxel SDF operation mode accepted by the mod command dispatcher.
    /// Negative SDF means solid; subtract digs, add welds/builds solid material.
    /// </summary>
    public enum ModSdfMode : ushort
    {
        /// <summary>Subtracts material from the voxel field.</summary>
        Subtract = 0,

        /// <summary>Adds/welds material into the voxel field.</summary>
        Add = 1
    }

    /// <summary>
    /// AUP response lane kind for asynchronous mod callbacks.
    /// </summary>
    public enum ModAupResponseKind : uint
    {
        /// <summary>No response.</summary>
        None = 0,

        /// <summary>Flow vector response. Payload packs xyz meters per second.</summary>
        FlowVector = 1,

        /// <summary>Voxel modification accepted/rejected response.</summary>
        VoxelModify = 2,

        /// <summary>Acoustic ping accepted/rejected response.</summary>
        AcousticPing = 3
    }

    /// <summary>
    /// Generic status for asynchronous AUP mod responses.
    /// </summary>
    public enum ModAupResponseStatus : uint
    {
        /// <summary>The engine accepted and processed the request.</summary>
        Accepted = 0,

        /// <summary>The request was rejected before reaching the owning subsystem.</summary>
        Rejected = 1,

        /// <summary>The owning subsystem is unavailable this frame.</summary>
        Unavailable = 2
    }

    /// <summary>
    /// Fixed-size asynchronous response payload for AUP-backed mod queries.
    /// Bytes 52..63 pack opcode-specific data. Flow responses store x/y in Payload0
    /// and z in Payload1 using math.asuint(float).
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct ModAupResponse
    {
        /// <summary>Stable mod hash.</summary>
        public uint ModHash;

        /// <summary>Mod-local request identifier.</summary>
        public uint RequestId;

        /// <summary><see cref="ModAupResponseKind"/> value.</summary>
        public uint ResponseKind;

        /// <summary><see cref="ModAupResponseStatus"/> value.</summary>
        public uint Status;

        /// <summary>AUP grid echoed from the request.</summary>
        public long3 Grid;

        /// <summary>AUP local offset echoed from the request.</summary>
        public float3 Local;

        /// <summary>Opcode-specific packed payload. Flow: low32=x, high32=y.</summary>
        public ulong Payload0;

        /// <summary>Opcode-specific packed payload. Flow: low32=z, high32 reserved.</summary>
        public uint Payload1;
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
