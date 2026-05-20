using System;
using System.IO;
using Unity.Mathematics;

namespace Hecton8.Core.Scheduling
{
    /// <summary>
    /// Cold-boot CSV catalog for IJobParallelFor batch sizing.
    /// Format: subsystemName,minBatch,maxBatch. No string.Split, no hot-path parsing.
    /// </summary>
    public static class JobSchedulingProfileCatalog
    {
        private const string DefaultProfilePath = "job_scheduling_profiles.csv";
        private const int Capacity = 128;
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;
        private static readonly uint[] _hashes = new uint[Capacity];
        private static readonly ushort[] _minBatches = new ushort[Capacity];
        private static readonly ushort[] _maxBatches = new ushort[Capacity];
        private static int _count;
        private static bool _loaded;

        public static void LoadColdBootProfiles(string path = DefaultProfilePath)
        {
            _count = 0;
            _loaded = true;
            if (!File.Exists(path))
                return;

            try
            {
                using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    uint nameHash = FnvOffset;
                    uint value = 0u;
                    int column = 0;
                    ushort minBatch = 1;
                    ushort maxBatch = 1;
                    bool hasName = false;
                    bool hasValue = false;
                    bool comment = false;

                    while (true)
                    {
                        int read = stream.ReadByte();
                        bool end = read < 0;
                        byte c = end ? (byte)'\n' : (byte)read;

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
                            if (column == 0)
                            {
                                nameHash ^= c;
                                nameHash *= FnvPrime;
                                hasName = true;
                            }
                            else if (c >= '0' && c <= '9')
                            {
                                value = unchecked(value * 10u + (uint)(c - '0'));
                                hasValue = true;
                            }

                            if (!end)
                                continue;
                        }

                        if (separator)
                        {
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

                            if (hasName && _count < Capacity)
                            {
                                _hashes[_count] = nameHash;
                                _minBatches[_count] = minBatch;
                                _maxBatches[_count] = maxBatch < minBatch ? minBatch : maxBatch;
                                _count++;
                            }

                            nameHash = FnvOffset;
                            minBatch = 1;
                            maxBatch = 1;
                            column = 0;
                            hasName = false;
                        }

                        if (end)
                            break;
                    }
                }
            }
            catch (IOException)
            {
                _count = 0;
            }
            catch (UnauthorizedAccessException)
            {
                _count = 0;
            }
        }

        public static bool TryResolveBatchBounds(uint jobHash, out int minBatch, out int maxBatch)
        {
            minBatch = 1;
            maxBatch = 1;
            if (!_loaded)
                return false;

            for (int i = 0; i < _count; i++)
            {
                if (_hashes[i] != jobHash)
                    continue;

                minBatch = _minBatches[i];
                maxBatch = _maxBatches[i];
                return true;
            }

            return false;
        }
    }
}
