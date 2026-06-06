using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;

namespace Hecton8.Core.Scheduling
{
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct JobSchedulingProfileDTO
    {
        [FieldOffset(0)] public uint JobHash;
        [FieldOffset(4)] public ushort MinBatch;
        [FieldOffset(6)] public ushort MaxBatch;
        [FieldOffset(8)] public uint Flags;
        [FieldOffset(12)] public uint Padding0;
    }

    /// <summary>
    /// Cold-boot CSV catalog for IJobParallelFor batch sizing.
    /// Format: jobTypeFullName,minBatch,maxBatch. Editor/development source data only; player runtime consumes baked/default Vault state.
    /// </summary>
    public static class JobSchedulingProfileCatalog
    {
        private const string DefaultProfilePath = "Assets/_SourceData/Core/Scheduling/job_scheduling_profiles.csv";
        private const int Capacity = 128;
        private const int CsvScratchBytes = 4096;
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;
        private const uint ProfileFlagActive = 1u;
        private static VaultGenerationHandle<JobSchedulingProfileDTO> _profilesHandle;
        private static IDataVault _vault;
        private static int _count;
        private static bool _loaded;

        public static void LoadColdBootProfiles(string path = DefaultProfilePath)
        {
            LoadColdBootProfiles(null, path);
        }

        public static void LoadColdBootProfiles(IDataVault vault, string path = DefaultProfilePath)
        {
            _vault = vault;
            _profilesHandle = default;
            _count = 0;
            _loaded = true;

            if (vault == null)
                return;

            if (vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                return;

            _profilesHandle = vault.EnsureGenerationHandle<JobSchedulingProfileDTO>(
                BufferID.SystemDispatcherJobSchedulingProfiles,
                Capacity,
                SystemID.SystemDispatcher,
                NativeArrayOptions.UninitializedMemory);

#if UNITY_EDITOR
            Span<byte> csvScratch = stackalloc byte[CsvScratchBytes];
            int byteCount = TryReadProfileCsvBytes(path, csvScratch);
            if (byteCount <= 0)
                return;

            if (!vault.TryAcquireWriteLock(in _profilesHandle, SystemID.SystemDispatcher, out NativeArray<JobSchedulingProfileDTO> profiles))
                return;

            try
            {
                if (profiles.IsCreated)
                    _count = ParseProfileCsv(csvScratch.Slice(0, byteCount), profiles);
            }
            finally
            {
                vault.ReleaseWriteLock(in _profilesHandle, SystemID.SystemDispatcher);
            }
#else
            _count = 0;
#endif
        }

#if UNITY_EDITOR
        private static int TryReadProfileCsvBytes(string path, Span<byte> scratch)
        {
            try
            {
                if (!File.Exists(path))
                    return 0;

                using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    int byteCount = 0;
                    while (byteCount < scratch.Length)
                    {
                        int read = stream.Read(scratch.Slice(byteCount));
                        if (read <= 0)
                            break;

                        byteCount += read;
                    }

                    return byteCount;
                }
            }
            catch (IOException)
            {
                return 0;
            }
            catch (UnauthorizedAccessException)
            {
                return 0;
            }
        }

        internal static int ParseProfileCsv(ReadOnlySpan<byte> csvBytes, NativeArray<JobSchedulingProfileDTO> profiles)
        {
            int count = 0;
            int capacity = math.min(Capacity, profiles.Length);
            const uint maxParsedBatch = ushort.MaxValue;
            uint nameHash = FnvOffset;
            uint value = 0u;
            int column = 0;
            ushort minBatch = 1;
            ushort maxBatch = 1;
            bool hasName = false;
            bool hasValue = false;
            bool comment = false;
            bool invalidRow = false;

            for (int i = 0; i <= csvBytes.Length; i++)
            {
                bool end = i == csvBytes.Length;
                byte c = end ? (byte)'\n' : csvBytes[i];

                if (i == 0 && csvBytes.Length >= 3 && c == 0xEF && csvBytes[1] == 0xBB && csvBytes[2] == 0xBF)
                {
                    i += 2;
                    continue;
                }

                if (comment)
                {
                    if (c == '\n' || c == '\r' || end)
                        comment = false;
                    else
                        continue;
                }

                if (c == '#')
                {
                    comment = true;
                    continue;
                }

                bool separator = c == ',' || c == ';' || c == '\t' || c == '\r' || c == '\n' || end;
                if (!separator)
                {
                    if (c == ' ')
                        continue;

                    if (column == 0)
                    {
                        nameHash ^= c;
                        nameHash *= FnvPrime;
                        hasName = true;
                    }
                    else if (c >= '0' && c <= '9')
                    {
                        uint digit = (uint)(c - '0');
                        value = value > maxParsedBatch
                            ? maxParsedBatch
                            : math.min(maxParsedBatch, value * 10u + digit);
                        hasValue = true;
                    }
                    else
                    {
                        invalidRow = true;
                    }

                    continue;
                }

                if (column == 1 && hasValue)
                    minBatch = (ushort)math.clamp((int)value, 1, ushort.MaxValue);
                else if (column == 2 && hasValue)
                    maxBatch = (ushort)math.clamp((int)value, 1, ushort.MaxValue);

                value = 0u;
                hasValue = false;

                if (c == ',' || c == ';' || c == '\t')
                {
                    column++;
                    continue;
                }

                if (!invalidRow && hasName && count < capacity)
                {
                    JobSchedulingProfileDTO profile = default;
                    profile.JobHash = nameHash;
                    profile.MinBatch = minBatch;
                    profile.MaxBatch = maxBatch < minBatch ? minBatch : maxBatch;
                    profile.Flags = ProfileFlagActive;
                    profiles[count] = profile;
                    count++;
                }

                nameHash = FnvOffset;
                minBatch = 1;
                maxBatch = 1;
                column = 0;
                hasName = false;
                invalidRow = false;
            }

            return count;
        }
#endif

        public static bool TryResolveBatchBounds(uint jobHash, out int minBatch, out int maxBatch)
        {
            minBatch = 1;
            maxBatch = 1;
            if (!_loaded)
                return false;

            if (_vault == null || _profilesHandle.BufferID == 0u)
                return false;

            if (!_vault.TryReadOnlyHandle(in _profilesHandle, out NativeArray<JobSchedulingProfileDTO>.ReadOnly profiles))
                return false;

            for (int i = 0; i < _count; i++)
            {
                JobSchedulingProfileDTO profile = profiles[i];
                if ((profile.Flags & ProfileFlagActive) == 0u || profile.JobHash != jobHash)
                    continue;

                minBatch = profile.MinBatch;
                maxBatch = profile.MaxBatch;
                return true;
            }

            return false;
        }
    }
}
