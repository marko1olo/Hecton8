using System;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World
{
    public static class ChunkResidencyStateFlags
    {
        public const byte Dehydrated = 0;
        public const byte Hydrated = 1 << 0;
        public const byte HydrationPending = 1 << 1;
        public const byte DehydrationPending = 1 << 2;
        public const byte LOD2Impostor = 1 << 3;
        public const byte ThreatOverride = 1 << 4;
        public const byte Loading = 1 << 5;
        public const byte Staged = 1 << 6;
        public const byte Pinned = 1 << 7;
    }

    [StructLayout(LayoutKind.Explicit, Size = 40)]
    public struct ChunkResidencyDTO
    {
        [FieldOffset(0)]
        public double3 AUP_Center;
        [FieldOffset(24)]
        public uint SectorHash;
        [FieldOffset(28)]
        public float DistanceSq;
        [FieldOffset(32)]
        public byte StateFlags;
        [FieldOffset(33)]
        public byte Priority;
        [FieldOffset(34)]
        public ushort _pad0;
        [FieldOffset(36)]
        public uint _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct AddressablesRequestDTO
    {
        [FieldOffset(0)]
        public uint AssetHash;
        [FieldOffset(4)]
        public int TargetChunkIndex;
        [FieldOffset(8)]
        public ulong HandlePtr;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct HLOD_ImpostorDTO
    {
        [FieldOffset(0)]
        public uint SectorHash;
        [FieldOffset(4)]
        public float2 CenterXZ;
        [FieldOffset(12)]
        public ushort RadiusMetersQ;
        [FieldOffset(14)]
        public byte ImpostorType;
        [FieldOffset(15)]
        public byte Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ChunkHydrationApplyRecord
    {
        [FieldOffset(0)]
        public long ChunkId;
        [FieldOffset(8)]
        public ulong PrefabStableHash;
        [FieldOffset(16)]
        public double TimeSeconds;
        [FieldOffset(24)]
        public int ChunkIndex;
        [FieldOffset(28)]
        public int PrefabIndex;
        [FieldOffset(32)]
        public int EstimatedBytes;
        [FieldOffset(36)]
        public uint Frame;
        [FieldOffset(40)]
        public byte Flags;
        [FieldOffset(41)]
        public byte _pad0;
        [FieldOffset(42)]
        public ushort _pad1;
        [FieldOffset(44)]
        public uint _pad2;
        [FieldOffset(48)]
        public ulong _pad3;
        [FieldOffset(56)]
        public ulong _pad4;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct MockAssetHandle
    {
        [FieldOffset(0)]
        public uint AssetHash;
        [FieldOffset(4)]
        public int TargetChunkIndex;
        [FieldOffset(8)]
        public uint StartFrame;
        [FieldOffset(12)]
        public byte Status;
        [FieldOffset(13)]
        public byte Priority;
        [FieldOffset(14)]
        public ushort PayloadPages;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public partial struct MockAupShiftSignal
    {
        [FieldOffset(0)]
        public double3 ShiftDeltaMeters;
        [FieldOffset(24)]
        public uint FrameId;
        [FieldOffset(28)]
        public byte Fired;
        [FieldOffset(29)]
        public byte _pad0;
        [FieldOffset(30)]
        public ushort _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct WorldStreamingRuntimeTuning
    {
        [FieldOffset(0)]
        public float PredictiveVelocityStretch;
        [FieldOffset(4)]
        public float Lod1RadiusMeters;
        [FieldOffset(8)]
        public float PhysicalHydrationRadiusMeters;
        [FieldOffset(12)]
        public float VisualResidencyRadiusMeters;
        [FieldOffset(16)]
        public float DataResidencyRadiusMeters;
        [FieldOffset(20)]
        public float DehydrationHysteresisMeters;
        [FieldOffset(24)]
        public int MaxConcurrentLoads;
        [FieldOffset(28)]
        public int HydrationCopyBudgetBytes;
        [FieldOffset(32)]
        public uint ProfileHash;
        [FieldOffset(36)]
        public byte Flags;
        [FieldOffset(37)]
        public byte _pad0;
        [FieldOffset(38)]
        public byte _pad1;
        [FieldOffset(39)]
        public byte _pad2;
        [FieldOffset(40)]
        public float LoadRadiusMeters;
        [FieldOffset(44)]
        public float UnloadRadiusMeters;

        public static WorldStreamingRuntimeTuning CreateDefault()
        {
            return new WorldStreamingRuntimeTuning
            {
                PredictiveVelocityStretch = 1f,
                Lod1RadiusMeters = 420f,
                PhysicalHydrationRadiusMeters = 180f,
                VisualResidencyRadiusMeters = 900f,
                DataResidencyRadiusMeters = 1800f,
                DehydrationHysteresisMeters = 50f,
                MaxConcurrentLoads = 4,
                HydrationCopyBudgetBytes = 512 * 1024,
                ProfileHash = 0x53333550u,
                Flags = 0,
                LoadRadiusMeters = 500f,
                UnloadRadiusMeters = 600f
            };
        }
    }

    public static class MockAddressables
    {
        public const byte StatusPending = 0;
        public const byte StatusSucceeded = 1;

        public static MockAssetHandle LoadAsync(uint assetHash, int targetChunkIndex, uint frame, byte priority)
        {
            uint pages = 1u + ((assetHash ^ (uint)targetChunkIndex) & 3u);
            return new MockAssetHandle
            {
                AssetHash = assetHash != 0u ? assetHash : 0x4D4F434Bu,
                TargetChunkIndex = targetChunkIndex,
                StartFrame = frame,
                Status = StatusPending,
                Priority = priority,
                PayloadPages = (ushort)pages
            };
        }

        public static bool IsDone(in MockAssetHandle handle, uint frame)
        {
            return unchecked(frame - handle.StartFrame) >= math.max(1u, handle.PayloadPages);
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct ChunkResidencyDtoInitJob : IJobParallelFor
    {
        public NativeArray<ChunkResidencyDTO> Chunks;

        public void Execute(int index)
        {
            Chunks[index] = new ChunkResidencyDTO
            {
                AUP_Center = default,
                SectorHash = 0u,
                DistanceSq = float.MaxValue,
                StateFlags = ChunkResidencyStateFlags.Dehydrated,
                Priority = 0,
                _pad0 = 0,
                _pad1 = 0u
            };
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct MockAupShiftSignalJob : IJob
    {
        public NativeArray<MockAupShiftSignal> Signal;
        public uint Frame;
        public uint Seed;

        public void Execute()
        {
            if (!Signal.IsCreated || Signal.Length == 0)
                return;

            uint mixed = Seed ^ (Frame * 747796405u) ^ 0x9E3779B9u;
            mixed ^= mixed >> 16;
            byte fired = (byte)((mixed & 31u) == 0u ? 1 : 0);
            Signal[0] = new MockAupShiftSignal
            {
                ShiftDeltaMeters = fired != 0 ? new double3(4000d, 0d, 0d) : default,
                FrameId = Frame,
                Fired = fired,
                _pad0 = 0,
                _pad1 = 0
            };
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct ChunkResidencyAupShiftReconcileJob : IJobParallelFor
    {
        public NativeArray<ChunkResidencyDTO> Chunks;
        [ReadOnly] public NativeArray<MockAupShiftSignal> Signal;

        public void Execute(int index)
        {
            if (!Signal.IsCreated || Signal.Length == 0 || Signal[0].Fired == 0)
                return;

            ChunkResidencyDTO chunk = Chunks[index];
            chunk.DistanceSq = float.MaxValue;
            chunk.StateFlags = (byte)(chunk.StateFlags & unchecked((byte)~(ChunkResidencyStateFlags.HydrationPending | ChunkResidencyStateFlags.DehydrationPending)));
            Chunks[index] = chunk;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct PredictiveChunkResidencyJob : IJobParallelFor
    {
        public NativeArray<ChunkResidencyDTO> Chunks;
        public NativeList<int>.ParallelWriter HydrationRequests;
        public NativeList<int>.ParallelWriter DehydrationRequests;
        public double3 CameraAup;
        public float3 CameraVelocity;
        public float LoadRadiusMeters;
        public float UnloadRadiusMeters;
        public float PredictiveVelocityStretch;
        public float HysteresisMeters;

        public void Execute(int index)
        {
            ChunkResidencyDTO chunk = Chunks[index];
            double3 deltaD = chunk.AUP_Center - CameraAup;
            float3 delta = new float3((float)deltaD.x, (float)deltaD.y, (float)deltaD.z);
            float distSq = math.lengthsq(delta);
            if (!math.isfinite(distSq))
                distSq = float.MaxValue;

            float loadRadius = math.max(1f, LoadRadiusMeters);
            float unloadRadius = math.max(loadRadius + math.max(0f, HysteresisMeters), UnloadRadiusMeters);
            float loadSq = loadRadius * loadRadius;
            float unloadSq = unloadRadius * unloadRadius;

            bool insideLoadZone = distSq <= loadSq;
            float speedSq = math.lengthsq(CameraVelocity);
            if (!insideLoadZone && speedSq > 0.0001f && PredictiveVelocityStretch > 0f)
            {
                float invSpeed = math.rsqrt(speedSq);
                float3 direction = CameraVelocity * invSpeed;
                float ahead = math.dot(delta, direction);
                if (ahead > 0f)
                {
                    float predictionMeters = math.min(200f, speedSq * invSpeed * PredictiveVelocityStretch);
                    float clampedAhead = math.min(ahead, predictionMeters);
                    float3 nearestDelta = delta - (direction * clampedAhead);
                    insideLoadZone = math.lengthsq(nearestDelta) <= loadSq;
                }
            }

            byte flags = chunk.StateFlags;
            bool hydrated = (flags & ChunkResidencyStateFlags.Hydrated) != 0;
            bool loading = (flags & ChunkResidencyStateFlags.Loading) != 0;
            bool threat = (flags & ChunkResidencyStateFlags.ThreatOverride) != 0;
            bool pinned = (flags & ChunkResidencyStateFlags.Pinned) != 0;

            if (!hydrated && !loading && insideLoadZone)
            {
                flags = (byte)((flags | ChunkResidencyStateFlags.HydrationPending | ChunkResidencyStateFlags.Loading) & unchecked((byte)~ChunkResidencyStateFlags.DehydrationPending));
                chunk.Priority = (byte)(distSq <= loadSq * 0.25f ? 3 : 2);
                HydrationRequests.AddNoResize(index);
            }
            else if (hydrated && !threat && !pinned && distSq > unloadSq)
            {
                flags = (byte)((flags | ChunkResidencyStateFlags.DehydrationPending) & unchecked((byte)~ChunkResidencyStateFlags.HydrationPending));
                chunk.Priority = 1;
                DehydrationRequests.AddNoResize(index);
            }
            else
            {
                flags = (byte)(flags & unchecked((byte)~(ChunkResidencyStateFlags.HydrationPending | ChunkResidencyStateFlags.DehydrationPending)));
            }

            chunk.DistanceSq = distSq;
            chunk.StateFlags = flags;
            chunk._pad0 = 0;
            chunk._pad1 = 0u;
            Chunks[index] = chunk;
        }
    }

    public static class WorldStreamingProfileCsvParser
    {
        public static bool TryParse(ReadOnlySpan<char> csv, ref WorldStreamingRuntimeTuning tuning)
        {
            bool changed = false;
            int cursor = 0;
            while (cursor < csv.Length)
            {
                int lineStart = cursor;
                while (cursor < csv.Length && csv[cursor] != '\n' && csv[cursor] != '\r')
                    cursor++;

                ReadOnlySpan<char> line = Trim(csv.Slice(lineStart, cursor - lineStart));
                while (cursor < csv.Length && (csv[cursor] == '\n' || csv[cursor] == '\r'))
                    cursor++;

                if (line.Length == 0 || line[0] == '#')
                    continue;

                int separator = IndexOfSeparator(line);
                if (separator <= 0)
                    continue;

                ReadOnlySpan<char> key = Trim(line.Slice(0, separator));
                ReadOnlySpan<char> value = Trim(line.Slice(separator + 1));
                if (EqualsKey(key, "key") || value.Length == 0)
                    continue;

                if (EqualsKey(key, "max_concurrent_loads"))
                {
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue))
                    {
                        tuning.MaxConcurrentLoads = math.clamp(intValue, 1, 16);
                        changed = true;
                    }
                    continue;
                }

                if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatValue) || !math.isfinite(floatValue))
                    continue;

                if (EqualsKey(key, "predictive_velocity_stretch"))
                {
                    tuning.PredictiveVelocityStretch = math.clamp(floatValue, 0f, 10f);
                    changed = true;
                }
                else if (EqualsKey(key, "lod1_radius"))
                {
                    tuning.Lod1RadiusMeters = math.max(1f, floatValue);
                    changed = true;
                }
                else if (EqualsKey(key, "physical_hydration_radius") || EqualsKey(key, "full_simulation_radius"))
                {
                    tuning.PhysicalHydrationRadiusMeters = math.max(1f, floatValue);
                    changed = true;
                }
                else if (EqualsKey(key, "visual_residency_radius"))
                {
                    tuning.VisualResidencyRadiusMeters = math.max(1f, floatValue);
                    changed = true;
                }
                else if (EqualsKey(key, "data_residency_radius"))
                {
                    tuning.DataResidencyRadiusMeters = math.max(1f, floatValue);
                    changed = true;
                }
                else if (EqualsKey(key, "dehydration_hysteresis"))
                {
                    tuning.DehydrationHysteresisMeters = math.max(0f, floatValue);
                    changed = true;
                }
                else if (EqualsKey(key, "load_radius"))
                {
                    tuning.LoadRadiusMeters = math.max(1f, floatValue);
                    changed = true;
                }
                else if (EqualsKey(key, "unload_radius"))
                {
                    tuning.UnloadRadiusMeters = math.max(1f, floatValue);
                    changed = true;
                }
            }

            if (changed)
            {
                tuning.UnloadRadiusMeters = math.max(tuning.LoadRadiusMeters + math.max(1f, tuning.DehydrationHysteresisMeters), tuning.UnloadRadiusMeters);
                tuning.ProfileHash = HashSpan(csv);
            }

            return changed;
        }

        private static int IndexOfSeparator(ReadOnlySpan<char> line)
        {
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == ',' || c == '=' || c == ':')
                    return i;
            }

            return -1;
        }

        private static ReadOnlySpan<char> Trim(ReadOnlySpan<char> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start <= end && char.IsWhiteSpace(value[start]))
                start++;
            while (end >= start && char.IsWhiteSpace(value[end]))
                end--;
            return start > end ? ReadOnlySpan<char>.Empty : value.Slice(start, end - start + 1);
        }

        private static bool EqualsKey(ReadOnlySpan<char> lhs, string rhs)
        {
            if (lhs.Length != rhs.Length)
                return false;

            for (int i = 0; i < lhs.Length; i++)
            {
                char a = lhs[i];
                char b = rhs[i];
                if (a >= 'A' && a <= 'Z')
                    a = (char)(a + 32);
                if (a != b)
                    return false;
            }

            return true;
        }

        private static uint HashSpan(ReadOnlySpan<char> value)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= 16777619u;
            }

            return hash != 0u ? hash : 1u;
        }
    }

    public static class WorldStreamingLegacyProfileArchaeology
    {
        private const int MaxRationaleFiles = 96;
        private const int MaxReadChars = 8192;

        public static WorldStreamingRuntimeTuning GenerateEmergencyMockProfile()
        {
            return WorldStreamingRuntimeTuning.CreateDefault();
        }

        public static WorldStreamingRuntimeTuning ScanOrEmergency(string projectRoot)
        {
            WorldStreamingRuntimeTuning tuning = GenerateEmergencyMockProfile();
            try
            {
                if (string.IsNullOrEmpty(projectRoot))
                    return tuning;

                string archive = Path.Combine(projectRoot, "Docs", "Archive");
                if (!Directory.Exists(archive))
                    return tuning;

                ScanLegacyBinaryNames(archive, ref tuning);
                ScanRationaleLogs(archive, ref tuning);
            }
            catch (Exception)
            {
                tuning = GenerateEmergencyMockProfile();
            }

            return tuning;
        }

        private static void ScanLegacyBinaryNames(string archive, ref WorldStreamingRuntimeTuning tuning)
        {
            foreach (string file in Directory.EnumerateFiles(archive, "world_chunk_streaming_profile.h8bin", SearchOption.AllDirectories))
            {
                if (!string.IsNullOrEmpty(file))
                {
                    tuning.Flags |= 1;
                    tuning.ProfileHash ^= 0x48384249u;
                    return;
                }
            }
        }

        private static void ScanRationaleLogs(string archive, ref WorldStreamingRuntimeTuning tuning)
        {
            int scanned = 0;
            foreach (string file in Directory.EnumerateFiles(archive, "Rationale_*.md", SearchOption.AllDirectories))
            {
                if (scanned++ >= MaxRationaleFiles)
                    break;

                string text = File.ReadAllText(file);
                if (text.Length > MaxReadChars)
                    text = text.Substring(0, MaxReadChars);

                ReadOnlySpan<char> span = text.AsSpan();
                TryExtractRadius(span, "Visual", ref tuning.VisualResidencyRadiusMeters);
                TryExtractRadius(span, "Data", ref tuning.DataResidencyRadiusMeters);
                TryExtractRadius(span, "Full", ref tuning.PhysicalHydrationRadiusMeters);
                TryExtractRadius(span, "LOD1", ref tuning.Lod1RadiusMeters);
            }
        }

        private static void TryExtractRadius(ReadOnlySpan<char> text, string label, ref float target)
        {
            int index = IndexOfOrdinalIgnoreCase(text, label);
            if (index < 0)
                return;

            int end = math.min(text.Length, index + 96);
            ReadOnlySpan<char> slice = text.Slice(index, end - index);
            for (int i = 0; i < slice.Length; i++)
            {
                if ((slice[i] < '0' || slice[i] > '9') && slice[i] != '.')
                    continue;

                int start = i;
                while (i < slice.Length && ((slice[i] >= '0' && slice[i] <= '9') || slice[i] == '.'))
                    i++;

                if (float.TryParse(slice.Slice(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out float value) &&
                    math.isfinite(value) &&
                    value >= 1f)
                {
                    target = value;
                    return;
                }
            }
        }

        private static int IndexOfOrdinalIgnoreCase(ReadOnlySpan<char> text, string needle)
        {
            if (needle.Length == 0 || text.Length < needle.Length)
                return -1;

            for (int i = 0; i <= text.Length - needle.Length; i++)
            {
                bool match = true;
                for (int n = 0; n < needle.Length; n++)
                {
                    char a = text[i + n];
                    char b = needle[n];
                    if (a >= 'A' && a <= 'Z')
                        a = (char)(a + 32);
                    if (b >= 'A' && b <= 'Z')
                        b = (char)(b + 32);
                    if (a != b)
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                    return i;
            }

            return -1;
        }
    }
}
