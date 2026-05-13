using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Logistics
{
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
    public struct FluidPipePressureSolveJob : IJob
    {
        public int NodeCount;
        public int FrameIndex;
        public int TelemetryIndex;
        public float DeltaTime;
        public float DefaultFlowRate;

        [ReadOnly] public NativeParallelMultiHashMap<int, int> Connections;
        [ReadOnly] public NativeArray<byte> PipeContentKinds;
        [ReadOnly] public NativeArray<int> PipeNetworkIds;
        [ReadOnly] public NativeArray<int> PipeRoomIndices;
        [ReadOnly] public NativeArray<float> PipeCapacities;
        [ReadOnly] public NativeArray<float> PipeMaxPressure;
        [ReadOnly] public NativeArray<float> PipeFlowRates;
        [ReadOnly] public NativeArray<float> PipeSourceRates;
        [ReadOnly] public NativeArray<float> PipeDemandRates;

        public NativeArray<float> PipePressure;
        public NativeArray<float> PipeContents;
        public NativeArray<byte> PipeFlags;
        public NativeArray<float3> PipeFlowVectors;
        public NativeArray<float> PipeRoomExchangeContents;
        public NativeArray<FluidPipeTelemetryEntry> TelemetryRing;
        public NativeArray<FluidPipeRuptureRecord> RuptureTelemetryRing;
        public NativeQueue<FluidPipeRuptureRecord>.ParallelWriter Ruptures;

        public void Execute()
        {
            if (!HasRequiredArrays())
                return;

            int count = ResolveSafeNodeCount();
            float dt = math.max(0f, DeltaTime);
            float defaultRate = math.max(0f, DefaultFlowRate);
            int ruptureCount = 0;
            int nanCount = 0;
            uint stateHash = FluidPipeGraphConstants.FnvOffset;

            for (int i = 0; i < count; i++)
            {
                byte flags = PipeFlags[i];
                float pressure = PipePressure[i];
                float content = PipeContents[i];
                if (!math.isfinite(pressure) || !math.isfinite(content))
                {
                    flags |= (byte)FluidPipeFlags.Disabled;
                    nanCount++;
                }

                content = FluidPipeGraphConstants.SanitizeFiniteNonNegative(content);
                if ((flags & (byte)FluidPipeFlags.Active) != 0 &&
                    (flags & (byte)FluidPipeFlags.Disabled) == 0 &&
                    (flags & (byte)FluidPipeFlags.Ruptured) == 0)
                {
                    float source = FluidPipeGraphConstants.SanitizeFiniteNonNegative(ReadFloat(PipeSourceRates, i, 0f));
                    content += source * dt;
                    if (!math.isfinite(content))
                    {
                        flags |= (byte)FluidPipeFlags.Disabled;
                        content = 0f;
                        nanCount++;
                    }
                }

                PipeFlags[i] = flags;
                PipeContents[i] = FluidPipeGraphConstants.SanitizeFiniteNonNegative(content);
                PipePressure[i] = ResolveInitialPressure(i, PipeContents[i], flags, ReadByte(PipeContentKinds, i, (byte)FluidPipeContentKind.Empty));
                PipeFlowVectors[i] = default;
                PipeRoomExchangeContents[i] = 0f;
            }

            for (int nodeIndex = 0; nodeIndex < count; nodeIndex++)
            {
                byte flags = PipeFlags[nodeIndex];
                if ((flags & (byte)FluidPipeFlags.Active) == 0 ||
                    (flags & (byte)FluidPipeFlags.Disabled) != 0 ||
                    (flags & (byte)FluidPipeFlags.Ruptured) != 0)
                {
                    continue;
                }

                NativeParallelMultiHashMapIterator<int> iterator;
                int neighborIndex;
                if (!Connections.TryGetFirstValue(nodeIndex, out neighborIndex, out iterator))
                    continue;

                do
                {
                    if (neighborIndex <= nodeIndex || neighborIndex < 0 || neighborIndex >= count)
                        continue;

                    TransferAcrossEdge(nodeIndex, neighborIndex, dt, defaultRate);
                }
                while (Connections.TryGetNextValue(out neighborIndex, ref iterator));
            }

            float totalWater = 0f;
            float totalOxygen = 0f;
            float maxPressure = 0f;

            for (int nodeIndex = 0; nodeIndex < count; nodeIndex++)
            {
                byte flags = PipeFlags[nodeIndex];
                byte contentKind = ReadByte(PipeContentKinds, nodeIndex, (byte)FluidPipeContentKind.Empty);
                float content = FluidPipeGraphConstants.SanitizeFiniteNonNegative(PipeContents[nodeIndex]);
                float pressure = FluidPipeGraphConstants.SanitizeFiniteNonNegative(PipePressure[nodeIndex]);

                if (!math.isfinite(content) || !math.isfinite(pressure))
                {
                    flags |= (byte)FluidPipeFlags.Disabled;
                    content = 0f;
                    pressure = 0f;
                    nanCount++;
                }

                if ((flags & (byte)FluidPipeFlags.Active) == 0 ||
                    (flags & (byte)FluidPipeFlags.Disabled) != 0)
                {
                    PipeFlags[nodeIndex] = flags;
                    PipeContents[nodeIndex] = content;
                    PipePressure[nodeIndex] = pressure;
                    continue;
                }

                float demand = FluidPipeGraphConstants.SanitizeFiniteNonNegative(ReadFloat(PipeDemandRates, nodeIndex, 0f));

                float delivered = math.min(content, demand * dt);
                content -= delivered;
                if (delivered > 0f)
                    PipeRoomExchangeContents[nodeIndex] += delivered;

                if ((flags & (byte)FluidPipeFlags.Outside) != 0 && contentKind == (byte)FluidPipeContentKind.Water)
                {
                    content = 0f;
                    pressure = 0f;
                }
                else
                {
                    pressure = ResolvePressure(nodeIndex, content);
                }

                float nodeMaxPressure = ResolveMaxPressure(nodeIndex);
                bool newlyRuptured = pressure > nodeMaxPressure && (flags & (byte)FluidPipeFlags.Ruptured) == 0;
                if (newlyRuptured)
                {
                    flags |= (byte)FluidPipeFlags.Ruptured;
                    EnqueueRupture(nodeIndex, content, pressure, nodeMaxPressure, contentKind, flags, ruptureCount);
                    ruptureCount++;
                }

                if ((flags & (byte)FluidPipeFlags.Ruptured) != 0)
                {
                    content = 0f;
                    pressure = 0f;
                }

                PipeFlags[nodeIndex] = flags;
                PipeContents[nodeIndex] = content;
                PipePressure[nodeIndex] = pressure;

                if (contentKind == (byte)FluidPipeContentKind.Water)
                    totalWater += content;
                else if (contentKind == (byte)FluidPipeContentKind.Oxygen)
                    totalOxygen += content;

                maxPressure = math.max(maxPressure, pressure);
                stateHash = FluidPipeGraphConstants.MixHash(stateHash, (uint)nodeIndex);
                stateHash = FluidPipeGraphConstants.MixHash(stateHash, math.asuint(content));
                stateHash = FluidPipeGraphConstants.MixHash(stateHash, math.asuint(pressure));
                stateHash = FluidPipeGraphConstants.MixHash(stateHash, flags);
            }

            WriteTelemetry(count, ruptureCount, nanCount, totalWater, totalOxygen, maxPressure, stateHash);
        }

        private bool HasRequiredArrays()
        {
            return PipePressure.IsCreated &&
                   PipeContents.IsCreated &&
                   PipeFlags.IsCreated &&
                   PipeFlowVectors.IsCreated &&
                   PipeRoomExchangeContents.IsCreated &&
                   Connections.IsCreated;
        }

        private int ResolveSafeNodeCount()
        {
            int count = math.max(0, NodeCount);
            count = math.min(count, PipePressure.Length);
            count = math.min(count, PipeContents.Length);
            count = math.min(count, PipeFlags.Length);
            count = math.min(count, PipeFlowVectors.Length);
            count = math.min(count, PipeRoomExchangeContents.Length);
            return count;
        }

        private void TransferAcrossEdge(int a, int b, float dt, float defaultRate)
        {
            byte aFlags = PipeFlags[a];
            byte bFlags = PipeFlags[b];
            if ((aFlags & (byte)FluidPipeFlags.Active) == 0 ||
                (bFlags & (byte)FluidPipeFlags.Active) == 0 ||
                (aFlags & (byte)FluidPipeFlags.Ruptured) != 0 ||
                (bFlags & (byte)FluidPipeFlags.Ruptured) != 0 ||
                (aFlags & (byte)FluidPipeFlags.Disabled) != 0 ||
                (bFlags & (byte)FluidPipeFlags.Disabled) != 0)
            {
                return;
            }

            byte aKind = ReadByte(PipeContentKinds, a, 0);
            byte bKind = ReadByte(PipeContentKinds, b, 0);
            if (aKind != bKind)
                return;

            int aNetwork = ReadInt(PipeNetworkIds, a, 0);
            int bNetwork = ReadInt(PipeNetworkIds, b, 0);
            if (aNetwork != bNetwork)
                return;

            float pressureDelta = PipePressure[a] - PipePressure[b];
            if (math.abs(pressureDelta) <= 0.0001f)
                return;

            float flowRate = math.max(0f, ResolveEdgeRate(a, b, defaultRate));
            float requestedTransfer = pressureDelta * flowRate * dt;
            if (!math.isfinite(requestedTransfer) || math.abs(requestedTransfer) <= 0.000001f)
                return;

            if (requestedTransfer > 0f)
            {
                float moved = math.min(PipeContents[a], requestedTransfer);
                if (moved <= 0f)
                    return;

                PipeContents[a] -= moved;
                PipeContents[b] += moved;
                PipeFlowVectors[a] += new float3(-moved, math.abs(moved), pressureDelta);
                PipeFlowVectors[b] += new float3(moved, math.abs(moved), -pressureDelta);
            }
            else
            {
                float moved = math.min(PipeContents[b], -requestedTransfer);
                if (moved <= 0f)
                    return;

                PipeContents[b] -= moved;
                PipeContents[a] += moved;
                PipeFlowVectors[b] += new float3(-moved, math.abs(moved), -pressureDelta);
                PipeFlowVectors[a] += new float3(moved, math.abs(moved), pressureDelta);
            }
        }

        private float ResolvePressure(int nodeIndex, float content)
        {
            float capacity = math.max(
                FluidPipeGraphConstants.MinCapacity,
                FluidPipeGraphConstants.SanitizeFiniteNonNegative(ReadFloat(PipeCapacities, nodeIndex, 1f)));
            float maxPressure = ResolveMaxPressure(nodeIndex);
            return content * math.rcp(capacity) * maxPressure;
        }

        private float ResolveInitialPressure(int nodeIndex, float content, byte flags, byte contentKind)
        {
            if ((flags & (byte)FluidPipeFlags.Disabled) != 0 ||
                (flags & (byte)FluidPipeFlags.Active) == 0 ||
                (flags & (byte)FluidPipeFlags.Ruptured) != 0 ||
                ((flags & (byte)FluidPipeFlags.Outside) != 0 && contentKind == (byte)FluidPipeContentKind.Water))
            {
                return 0f;
            }

            return ResolvePressure(nodeIndex, content);
        }

        private float ResolveMaxPressure(int nodeIndex)
        {
            return math.max(
                FluidPipeGraphConstants.MinMaxPressureKPa,
                FluidPipeGraphConstants.SanitizeFiniteNonNegative(ReadFloat(PipeMaxPressure, nodeIndex, 100f)));
        }

        private float ResolveEdgeRate(int a, int b, float defaultRate)
        {
            float aRate = ReadFloat(PipeFlowRates, a, defaultRate);
            float bRate = ReadFloat(PipeFlowRates, b, defaultRate);
            return math.max(0f, math.min(aRate, bRate));
        }

        private void EnqueueRupture(
            int nodeIndex,
            float content,
            float pressure,
            float maxPressure,
            byte contentKind,
            byte flags,
            int ruptureSequence)
        {
            float flow01 = math.saturate(pressure * math.rcp(math.max(FluidPipeGraphConstants.MinMaxPressureKPa, maxPressure)));
            int networkId = ReadInt(PipeNetworkIds, nodeIndex, 0);
            FluidPipeRuptureRecord record = new FluidPipeRuptureRecord
            {
                NodeIndex = nodeIndex,
                NetworkId = networkId,
                RoomIndex = ReadInt(PipeRoomIndices, nodeIndex, -1),
                FrameIndex = FrameIndex,
                PressureKPa = pressure,
                Contents = content,
                Flow01 = flow01,
                NodeHash = FluidPipeGraphConstants.HashNode(nodeIndex, networkId, contentKind),
                ContentKind = contentKind,
                Flags = flags
            };

            Ruptures.Enqueue(record);
            if (RuptureTelemetryRing.IsCreated && RuptureTelemetryRing.Length > 0)
            {
                int index = (TelemetryIndex + ruptureSequence) % RuptureTelemetryRing.Length;
                if (index < 0)
                    index += RuptureTelemetryRing.Length;
                RuptureTelemetryRing[index] = record;
            }
        }

        private void WriteTelemetry(
            int count,
            int ruptureCount,
            int nanCount,
            float totalWater,
            float totalOxygen,
            float maxPressure,
            uint stateHash)
        {
            if (!TelemetryRing.IsCreated || TelemetryRing.Length <= 0)
                return;

            int index = TelemetryIndex % TelemetryRing.Length;
            if (index < 0)
                index += TelemetryRing.Length;

            TelemetryRing[index] = new FluidPipeTelemetryEntry
            {
                FrameIndex = FrameIndex,
                NodeCount = count,
                RuptureCount = ruptureCount,
                NanCount = nanCount,
                TotalWater = totalWater,
                TotalOxygen = totalOxygen,
                MaxPressureKPa = maxPressure,
                StateHash = stateHash
            };
        }

        private static float ReadFloat(NativeArray<float> array, int index, float fallback)
        {
            return array.IsCreated && index >= 0 && index < array.Length ? array[index] : fallback;
        }

        private static int ReadInt(NativeArray<int> array, int index, int fallback)
        {
            return array.IsCreated && index >= 0 && index < array.Length ? array[index] : fallback;
        }

        private static byte ReadByte(NativeArray<byte> array, int index, byte fallback)
        {
            return array.IsCreated && index >= 0 && index < array.Length ? array[index] : fallback;
        }
    }
}
