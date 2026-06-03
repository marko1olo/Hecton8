using System;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Logistics;
using Hecton8.Power;
using Hecton8.World;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Construction
{
    public enum LogisticsNetworkTypeID : byte
    {
        PowerDc = 0,
        OxygenPressure = 1,
        FluidPressure = 2,
        ThermalCoolant = 3,
        DataSignal = 4,
        FuelLiquid = 5
    }

    public enum LogisticsNetworkNodeTypeID : byte
    {
        Pipe = 1,
        Junction = 2,
        Valve = 3,
        Pump = 4,
        Relay = 5,
        Producer = 6,
        Consumer = 7
    }

    public enum LogisticsNetworkPortTypeID : byte
    {
        Bidirectional = 1,
        Inlet = 2,
        Outlet = 3,
        Control = 4,
        Power = 5,
        Data = 6
    }

    [Serializable]
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    public struct NetworkPortDescriptor
    {
        [FieldOffset(0)]
        public int PortID;
        [FieldOffset(4)]
        public LogisticsNetworkPortTypeID PortTypeID;
        [FieldOffset(8)]
        public Vector3 LocalPosition;
        [FieldOffset(20)]
        public Vector3 LocalDirection;
        [FieldOffset(32)]
        public float CapacityScale;
        [FieldOffset(36)]
        private uint _pad0;
    }

    [Serializable]
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct NetworkNodeBakeDTO
    {
        [FieldOffset(0)]
        public uint StableNodeHash;
        [FieldOffset(4)]
        public float BaseCapacity;
        [FieldOffset(8)]
        public float BaseResistance;
        [FieldOffset(12)]
        public float MaxPressureKPa;
        [FieldOffset(16)]
        public LogisticsNetworkTypeID NetworkTypeID;
        [FieldOffset(17)]
        public LogisticsNetworkNodeTypeID NodeTypeID;
        [FieldOffset(18)]
        public byte Priority;
        [FieldOffset(19)]
        public byte Flags;
        [FieldOffset(20)]
        public ushort PortCount;
        [FieldOffset(22)]
        public ushort StateRendererCount;
        [FieldOffset(24)]
        public float BaseWattage;
        [FieldOffset(28)]
        private uint _pad0;
    }

    [Serializable]
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct FluidPipeNodeBakeDTO
    {
        [FieldOffset(0)]
        public uint StableNodeHash;
        [FieldOffset(4)]
        public float Capacity;
        [FieldOffset(8)]
        public float MaxPressureKPa;
        [FieldOffset(12)]
        public byte ContentKind;
        [FieldOffset(13)]
        public byte InitialFlags;
        [FieldOffset(14)]
        public ushort PortCount;
        [FieldOffset(16)]
        public LogisticsNetworkNodeTypeID NodeTypeID;
        [FieldOffset(17)]
        public byte Priority;
        [FieldOffset(18)]
        private ushort _pad0;
        [FieldOffset(20)]
        private uint _pad1;
        [FieldOffset(24)]
        private ulong _pad2;
    }

    [Serializable]
    [StructLayout(LayoutKind.Explicit, Size = 72)]
    public struct FluidPipeRegistrationDTO
    {
        [FieldOffset(0)]
        public uint StableNodeHash;
        [FieldOffset(4)]
        public int NetworkId;
        [FieldOffset(8)]
        public int RoomIndex;
        [FieldOffset(12)]
        public byte ContentKind;
        [FieldOffset(13)]
        public byte InitialFlags;
        [FieldOffset(14)]
        public ushort PortCount;
        [FieldOffset(16)]
        public AbsoluteUniversePosition NodeAup;
        [FieldOffset(64)]
        public float Capacity;
        [FieldOffset(68)]
        public float MaxPressureKPa;
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Construction/Network Node Data")]
    public sealed class NetworkNodeData : MonoBehaviour
    {
        private const float MinPowerCapacity = 0.001f;
        private const float MinPowerResistance = 0.0001f;
        private const byte AllowedFluidPipeInitialFlags =
            (byte)FluidPipeFlags.Active |
            (byte)FluidPipeFlags.Outside |
            (byte)FluidPipeFlags.PumpIngress |
            (byte)FluidPipeFlags.OxygenSource |
            (byte)FluidPipeFlags.RoomCoupled;

        private static readonly NetworkPortDescriptor[] s_emptyPorts = Array.Empty<NetworkPortDescriptor>();
        private static readonly Renderer[] s_emptyRenderers = Array.Empty<Renderer>();

        [Header("Graph Identity")]
        [SerializeField] private LogisticsNetworkTypeID networkTypeID = LogisticsNetworkTypeID.FluidPressure;
        [SerializeField] private LogisticsNetworkNodeTypeID nodeTypeID = LogisticsNetworkNodeTypeID.Pipe;
        [SerializeField] private uint stableNodeHash;
        [SerializeField] private byte priority;
        [SerializeField] private byte flags;

        [Header("Jacobi Integration")]
        [SerializeField, Min(0.001f)] private float baseCapacity = 1f;
        [SerializeField, Min(0f)] private float baseResistance = 1f;
        [SerializeField, Min(0.1f)] private float maxPressureKPa = 160f;
        [SerializeField] private NetworkPortDescriptor[] ports = s_emptyPorts;

        [Header("Power Integration")]
        [SerializeField, Min(0f)] private float powerInitialStorageWattSeconds;
        [SerializeField] private float powerBaseWattage;
        [SerializeField] private uint powerNodeFlagsOverride;

        [Header("Visual Sync")]
        [SerializeField] private Renderer[] stateRenderers = s_emptyRenderers;

        public LogisticsNetworkTypeID NetworkTypeID => networkTypeID;
        public LogisticsNetworkNodeTypeID NodeTypeID => nodeTypeID;
        public uint StableNodeHash => stableNodeHash;
        public byte Priority => priority;
        public byte Flags => flags;
        public float BaseCapacity => baseCapacity;
        public float BaseResistance => baseResistance;
        public float MaxPressureKPa => maxPressureKPa;
        public float PowerInitialStorageWattSeconds => powerInitialStorageWattSeconds;
        public float PowerBaseWattage => powerBaseWattage;
        public uint PowerNodeFlagsOverride => powerNodeFlagsOverride;
        public int PortCount => ports != null ? ports.Length : 0;
        public int StateRendererCount => stateRenderers != null ? stateRenderers.Length : 0;

        public bool TryGetPort(int index, out NetworkPortDescriptor descriptor)
        {
            NetworkPortDescriptor[] source = ports;
            if (source == null || (uint)index >= (uint)source.Length)
            {
                descriptor = default;
                return false;
            }

            descriptor = source[index];
            return true;
        }

        public Renderer GetStateRenderer(int index)
        {
            Renderer[] source = stateRenderers;
            if (source == null || (uint)index >= (uint)source.Length)
                return null;

            return source[index];
        }

        public bool TryBuildNodeBakeDTO(out NetworkNodeBakeDTO dto)
        {
            dto = default;
            int portCount = PortCount;
            if (stableNodeHash == 0u ||
                !math.isfinite(baseCapacity) ||
                !math.isfinite(baseResistance) ||
                !math.isfinite(maxPressureKPa) ||
                !math.isfinite(powerBaseWattage) ||
                baseCapacity <= 0f ||
                baseResistance < 0f ||
                maxPressureKPa <= 0f ||
                portCount <= 0 ||
                portCount > ushort.MaxValue ||
                !ValidatePortsForBake(ports))
            {
                return false;
            }

            dto = new NetworkNodeBakeDTO
            {
                StableNodeHash = stableNodeHash,
                BaseCapacity = baseCapacity,
                BaseResistance = baseResistance,
                MaxPressureKPa = maxPressureKPa,
                NetworkTypeID = networkTypeID,
                NodeTypeID = nodeTypeID,
                Priority = priority,
                Flags = flags,
                PortCount = (ushort)portCount,
                StateRendererCount = (ushort)math.min(StateRendererCount, ushort.MaxValue),
                BaseWattage = powerBaseWattage
            };
            return true;
        }

        public bool TryGetPowerGraphNetworkType(out LogisticsNetworkType graphNetworkType)
        {
            if (networkTypeID == LogisticsNetworkTypeID.PowerDc)
            {
                graphNetworkType = LogisticsNetworkType.PowerDc;
                return true;
            }

            if (networkTypeID == LogisticsNetworkTypeID.OxygenPressure)
            {
                graphNetworkType = LogisticsNetworkType.OxygenPressure;
                return true;
            }

            graphNetworkType = default;
            return false;
        }

        public bool TryBuildFluidPipeBakeDTO(out FluidPipeNodeBakeDTO dto)
        {
            dto = default;
            if (!TryBuildNodeBakeDTO(out NetworkNodeBakeDTO nodeDto) ||
                !TryResolveFluidContentKind(out FluidPipeContentKind contentKind))
            {
                return false;
            }

            dto = new FluidPipeNodeBakeDTO
            {
                StableNodeHash = nodeDto.StableNodeHash,
                Capacity = math.max(FluidPipeGraphConstants.MinCapacity, nodeDto.BaseCapacity),
                MaxPressureKPa = math.max(FluidPipeGraphConstants.MinMaxPressureKPa, nodeDto.MaxPressureKPa),
                ContentKind = (byte)contentKind,
                InitialFlags = SanitizeFluidPipeInitialFlags(flags),
                PortCount = nodeDto.PortCount,
                NodeTypeID = nodeDto.NodeTypeID,
                Priority = nodeDto.Priority
            };
            return true;
        }

        public bool TryBuildFluidPipeRegistrationDTO(
            int networkId,
            int roomIndex,
            in AbsoluteUniversePosition nodeAup,
            out FluidPipeRegistrationDTO dto)
        {
            dto = default;
            if (networkId < 0 ||
                !AbsoluteUniversePosition.IsFinite(in nodeAup) ||
                !TryBuildFluidPipeBakeDTO(out FluidPipeNodeBakeDTO bakeDto))
            {
                return false;
            }

            dto = new FluidPipeRegistrationDTO
            {
                StableNodeHash = bakeDto.StableNodeHash,
                NetworkId = networkId,
                RoomIndex = roomIndex,
                ContentKind = bakeDto.ContentKind,
                InitialFlags = bakeDto.InitialFlags,
                PortCount = bakeDto.PortCount,
                NodeAup = nodeAup,
                Capacity = bakeDto.Capacity,
                MaxPressureKPa = bakeDto.MaxPressureKPa
            };
            return true;
        }

        public static bool TryRegisterFluidPipeNode(
            IFluidPipeGraphService graph,
            in FluidPipeRegistrationDTO registration,
            out int nodeIndex)
        {
            nodeIndex = -1;
            if (graph == null ||
                !graph.IsInitialized ||
                registration.StableNodeHash == 0u ||
                registration.NetworkId < 0 ||
                registration.PortCount == 0 ||
                !AbsoluteUniversePosition.IsFinite(in registration.NodeAup) ||
                !math.isfinite(registration.Capacity) ||
                !math.isfinite(registration.MaxPressureKPa) ||
                registration.Capacity <= 0f ||
                registration.MaxPressureKPa <= 0f)
            {
                return false;
            }

            if (!graph.TryRegisterPipeNode(
                    registration.NetworkId,
                    registration.RoomIndex,
                    registration.ContentKind,
                    registration.NodeAup,
                    registration.Capacity,
                    registration.MaxPressureKPa,
                    out nodeIndex))
            {
                return false;
            }

            byte setMask = SanitizeFluidPipeInitialFlags(registration.InitialFlags);
            if (setMask == (byte)FluidPipeFlags.Active)
                return true;

            graph.TrySetPipeNodeFlags(
                nodeIndex,
                setMask,
                (byte)FluidPipeFlags.Disabled);
            return true;
        }

        public bool TryBuildPowerNodeDTO(out PowerNodeDTO dto)
        {
            dto = default;
            if (!TryBuildNodeBakeDTO(out NetworkNodeBakeDTO nodeDto) ||
                networkTypeID != LogisticsNetworkTypeID.PowerDc)
            {
                return false;
            }

            float initialStorage = math.isfinite(powerInitialStorageWattSeconds)
                ? math.max(0f, powerInitialStorageWattSeconds)
                : 0f;

            uint powerFlags = PowerGridJacobiConstants.NodeFlagActive;
            if (nodeTypeID == LogisticsNetworkNodeTypeID.Producer)
                powerFlags |= PowerGridJacobiConstants.NodeFlagSource;
            powerFlags |= powerNodeFlagsOverride;

            dto = new PowerNodeDTO
            {
                NodeHash = nodeDto.StableNodeHash,
                Potential = nodeTypeID == LogisticsNetworkNodeTypeID.Producer ? 1f : 0f,
                MaxCapacity = math.max(MinPowerCapacity, nodeDto.BaseCapacity),
                CurrentStorage = initialStorage,
                Flags = powerFlags,
                InternalResistance = math.max(MinPowerResistance, nodeDto.BaseResistance)
            };
            return true;
        }

        private void OnValidate()
        {
            SanitizeSerializedState();
        }

        private void SanitizeSerializedState()
        {
            baseCapacity = SanitizePositiveFinite(baseCapacity, 0.001f);
            baseResistance = SanitizeNonNegativeFinite(baseResistance, 1f);
            maxPressureKPa = SanitizePositiveFinite(maxPressureKPa, 0.1f);
            powerInitialStorageWattSeconds = SanitizeNonNegativeFinite(powerInitialStorageWattSeconds, 0f);
            powerBaseWattage = SanitizeFinite(powerBaseWattage, 0f);
            SanitizePorts(ports);
        }

        private static void SanitizePorts(NetworkPortDescriptor[] source)
        {
            if (source == null)
                return;

            for (int i = 0; i < source.Length; i++)
            {
                NetworkPortDescriptor port = source[i];
                if (port.PortID < 0)
                    port.PortID = i;

                if (port.CapacityScale <= 0f || !math.isfinite(port.CapacityScale))
                    port.CapacityScale = 1f;

                if (!IsFinite(port.LocalPosition))
                    port.LocalPosition = Vector3.zero;
                port.LocalDirection = NormalizeDirection(port.LocalDirection, Vector3.forward);

                source[i] = port;
            }
        }

        private static bool ValidatePortsForBake(NetworkPortDescriptor[] source)
        {
            if (source == null || source.Length == 0 || source.Length > ushort.MaxValue)
                return false;

            for (int i = 0; i < source.Length; i++)
            {
                NetworkPortDescriptor port = source[i];
                if (port.PortID < 0 ||
                    port.CapacityScale <= 0f ||
                    !math.isfinite(port.CapacityScale) ||
                    !IsFinite(port.LocalPosition) ||
                    !IsFinite(port.LocalDirection))
                {
                    return false;
                }

                float3 direction = new float3(port.LocalDirection.x, port.LocalDirection.y, port.LocalDirection.z);
                float lengthSq = math.lengthsq(direction);
                if (lengthSq < 0.999f || lengthSq > 1.001f)
                    return false;

                for (int j = i + 1; j < source.Length; j++)
                {
                    if (source[j].PortID == port.PortID)
                        return false;
                }
            }

            return true;
        }

        private static bool IsFinite(Vector3 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
        }

        private static Vector3 NormalizeDirection(Vector3 value, Vector3 fallback)
        {
            float3 direction = new float3(value.x, value.y, value.z);
            if (!math.all(math.isfinite(direction)) || math.lengthsq(direction) <= 0.000001f)
                return fallback;

            float3 normalized = math.normalize(direction);
            return new Vector3(normalized.x, normalized.y, normalized.z);
        }

        private static float SanitizePositiveFinite(float value, float fallback)
        {
            if (!math.isfinite(value) || value < fallback)
                return fallback;

            return value;
        }

        private static float SanitizeNonNegativeFinite(float value, float fallback)
        {
            if (!math.isfinite(value) || value < 0f)
                return fallback;

            return value;
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            if (!math.isfinite(value))
                return fallback;

            return value;
        }

        private bool TryResolveFluidContentKind(out FluidPipeContentKind contentKind)
        {
            if (networkTypeID == LogisticsNetworkTypeID.OxygenPressure)
            {
                contentKind = FluidPipeContentKind.Oxygen;
                return true;
            }

            if (networkTypeID == LogisticsNetworkTypeID.FluidPressure)
            {
                contentKind = FluidPipeContentKind.Water;
                return true;
            }

            contentKind = default;
            return false;
        }

        private static byte SanitizeFluidPipeInitialFlags(byte value)
        {
            return (byte)(((byte)FluidPipeFlags.Active | value) & AllowedFluidPipeInitialFlags);
        }

        public static bool ValidateUnmanagedLayout(out int nodeBytes, out int portBytes, out int fluidPipeBytes, out int fluidRegistrationBytes, out int powerNodeBytes)
        {
            nodeBytes = UnsafeUtility.SizeOf<NetworkNodeBakeDTO>();
            portBytes = UnsafeUtility.SizeOf<NetworkPortDescriptor>();
            fluidPipeBytes = UnsafeUtility.SizeOf<FluidPipeNodeBakeDTO>();
            fluidRegistrationBytes = UnsafeUtility.SizeOf<FluidPipeRegistrationDTO>();
            powerNodeBytes = UnsafeUtility.SizeOf<PowerNodeDTO>();
            return nodeBytes == 32 &&
                   portBytes == 40 &&
                   fluidPipeBytes == 32 &&
                   fluidRegistrationBytes == 72 &&
                   powerNodeBytes == PowerGridJacobiConstants.PowerNodeDtoSizeBytes &&
                   (nodeBytes & 7) == 0 &&
                   (portBytes & 7) == 0 &&
                   (fluidPipeBytes & 7) == 0 &&
                   (fluidRegistrationBytes & 7) == 0 &&
                   (powerNodeBytes & 7) == 0 &&
                   OffsetOf<NetworkNodeBakeDTO>(nameof(NetworkNodeBakeDTO.StableNodeHash)) == 0 &&
                   OffsetOf<NetworkNodeBakeDTO>(nameof(NetworkNodeBakeDTO.BaseCapacity)) == 4 &&
                   OffsetOf<NetworkNodeBakeDTO>(nameof(NetworkNodeBakeDTO.BaseResistance)) == 8 &&
                   OffsetOf<NetworkNodeBakeDTO>(nameof(NetworkNodeBakeDTO.MaxPressureKPa)) == 12 &&
                   OffsetOf<NetworkNodeBakeDTO>(nameof(NetworkNodeBakeDTO.NetworkTypeID)) == 16 &&
                   OffsetOf<NetworkNodeBakeDTO>(nameof(NetworkNodeBakeDTO.NodeTypeID)) == 17 &&
                   OffsetOf<NetworkNodeBakeDTO>(nameof(NetworkNodeBakeDTO.Priority)) == 18 &&
                   OffsetOf<NetworkNodeBakeDTO>(nameof(NetworkNodeBakeDTO.Flags)) == 19 &&
                   OffsetOf<NetworkNodeBakeDTO>(nameof(NetworkNodeBakeDTO.PortCount)) == 20 &&
                   OffsetOf<NetworkNodeBakeDTO>(nameof(NetworkNodeBakeDTO.StateRendererCount)) == 22 &&
                   OffsetOf<NetworkNodeBakeDTO>(nameof(NetworkNodeBakeDTO.BaseWattage)) == 24 &&
                   OffsetOf<NetworkPortDescriptor>(nameof(NetworkPortDescriptor.PortID)) == 0 &&
                   OffsetOf<NetworkPortDescriptor>(nameof(NetworkPortDescriptor.PortTypeID)) == 4 &&
                   OffsetOf<NetworkPortDescriptor>(nameof(NetworkPortDescriptor.LocalPosition)) == 8 &&
                   OffsetOf<NetworkPortDescriptor>(nameof(NetworkPortDescriptor.LocalDirection)) == 20 &&
                   OffsetOf<NetworkPortDescriptor>(nameof(NetworkPortDescriptor.CapacityScale)) == 32 &&
                   OffsetOf<FluidPipeNodeBakeDTO>(nameof(FluidPipeNodeBakeDTO.StableNodeHash)) == 0 &&
                   OffsetOf<FluidPipeNodeBakeDTO>(nameof(FluidPipeNodeBakeDTO.Capacity)) == 4 &&
                   OffsetOf<FluidPipeNodeBakeDTO>(nameof(FluidPipeNodeBakeDTO.MaxPressureKPa)) == 8 &&
                   OffsetOf<FluidPipeNodeBakeDTO>(nameof(FluidPipeNodeBakeDTO.ContentKind)) == 12 &&
                   OffsetOf<FluidPipeNodeBakeDTO>(nameof(FluidPipeNodeBakeDTO.InitialFlags)) == 13 &&
                   OffsetOf<FluidPipeNodeBakeDTO>(nameof(FluidPipeNodeBakeDTO.PortCount)) == 14 &&
                   OffsetOf<FluidPipeNodeBakeDTO>(nameof(FluidPipeNodeBakeDTO.NodeTypeID)) == 16 &&
                   OffsetOf<FluidPipeNodeBakeDTO>(nameof(FluidPipeNodeBakeDTO.Priority)) == 17 &&
                   OffsetOf<FluidPipeRegistrationDTO>(nameof(FluidPipeRegistrationDTO.StableNodeHash)) == 0 &&
                   OffsetOf<FluidPipeRegistrationDTO>(nameof(FluidPipeRegistrationDTO.NetworkId)) == 4 &&
                   OffsetOf<FluidPipeRegistrationDTO>(nameof(FluidPipeRegistrationDTO.RoomIndex)) == 8 &&
                   OffsetOf<FluidPipeRegistrationDTO>(nameof(FluidPipeRegistrationDTO.ContentKind)) == 12 &&
                   OffsetOf<FluidPipeRegistrationDTO>(nameof(FluidPipeRegistrationDTO.InitialFlags)) == 13 &&
                   OffsetOf<FluidPipeRegistrationDTO>(nameof(FluidPipeRegistrationDTO.PortCount)) == 14 &&
                   OffsetOf<FluidPipeRegistrationDTO>(nameof(FluidPipeRegistrationDTO.NodeAup)) == 16 &&
                   OffsetOf<FluidPipeRegistrationDTO>(nameof(FluidPipeRegistrationDTO.Capacity)) == 64 &&
                   OffsetOf<FluidPipeRegistrationDTO>(nameof(FluidPipeRegistrationDTO.MaxPressureKPa)) == 68 &&
                   OffsetOf<PowerNodeDTO>(nameof(PowerNodeDTO.NodeHash)) == 0 &&
                   OffsetOf<PowerNodeDTO>(nameof(PowerNodeDTO.Potential)) == 4 &&
                   OffsetOf<PowerNodeDTO>(nameof(PowerNodeDTO.MaxCapacity)) == 8 &&
                   OffsetOf<PowerNodeDTO>(nameof(PowerNodeDTO.CurrentStorage)) == 12 &&
                   OffsetOf<PowerNodeDTO>(nameof(PowerNodeDTO.Flags)) == 16 &&
                   OffsetOf<PowerNodeDTO>(nameof(PowerNodeDTO.InternalResistance)) == 20;
        }

        public static bool ValidateUnmanagedLayout(out int nodeBytes, out int portBytes, out int fluidPipeBytes, out int powerNodeBytes)
        {
            return ValidateUnmanagedLayout(out nodeBytes, out portBytes, out fluidPipeBytes, out _, out powerNodeBytes);
        }

        private static int OffsetOf<T>(string fieldName) where T : struct
        {
            return Marshal.OffsetOf<T>(fieldName).ToInt32();
        }

#if UNITY_EDITOR
        public void ConfigureEditorBake(
            LogisticsNetworkTypeID bakedNetworkTypeID,
            LogisticsNetworkNodeTypeID bakedNodeTypeID,
            float bakedBaseCapacity,
            float bakedBaseResistance,
            float bakedMaxPressureKPa,
            byte bakedPriority,
            byte bakedFlags,
            uint bakedStableNodeHash,
            NetworkPortDescriptor[] bakedPorts,
            Renderer[] bakedStateRenderers,
            float bakedPowerInitialStorageWattSeconds = 0f,
            uint bakedPowerNodeFlagsOverride = 0u,
            float bakedPowerBaseWattage = 0f)
        {
            networkTypeID = bakedNetworkTypeID;
            nodeTypeID = bakedNodeTypeID;
            baseCapacity = bakedBaseCapacity;
            baseResistance = bakedBaseResistance;
            maxPressureKPa = bakedMaxPressureKPa;
            priority = bakedPriority;
            flags = bakedFlags;
            stableNodeHash = bakedStableNodeHash;
            ports = bakedPorts != null ? bakedPorts : s_emptyPorts;
            stateRenderers = bakedStateRenderers != null ? bakedStateRenderers : s_emptyRenderers;
            powerInitialStorageWattSeconds = bakedPowerInitialStorageWattSeconds;
            powerNodeFlagsOverride = bakedPowerNodeFlagsOverride;
            powerBaseWattage = bakedPowerBaseWattage;
            SanitizeSerializedState();
        }
#endif
    }
}
