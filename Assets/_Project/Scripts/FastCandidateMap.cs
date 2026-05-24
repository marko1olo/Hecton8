using Unity.Collections;
using UnityEngine;

namespace Hecton8.World
{
    public sealed partial class WorldProceduralScatterDirector
    {
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        internal struct FastCandidateMap
        {
            private const ulong FibonacciHashMultiplier = 11400714819323198485UL;

            private ScatterCandidate[] values;
            private long[] keys;
            private int[] orderedSlots;
            public int count;

            private bool[] _occupied;
            private int _capacity;
            private int _mask;
            private int _log2Capacity;
            private bool _disposed;

            public bool IsInitialized => values != null &&
                                         keys != null &&
                                         orderedSlots != null &&
                                         _occupied != null &&
                                         _capacity > 0;

            public void Init(int capacity, Allocator allocator)
            {
                _ = allocator;
                int resolvedCapacity = Mathf.NextPowerOfTwo(Mathf.Max(4, capacity));

                // COLD ALLOC: ScatterCandidate[capacity] — hashed rescue candidate storage — owner: WorldProceduralScatterDirector.FastCandidateMap
                values = new ScatterCandidate[resolvedCapacity];
                // COLD ALLOC: long[capacity] — hashed rescue candidate keys — owner: WorldProceduralScatterDirector.FastCandidateMap
                keys = new long[resolvedCapacity];
                // COLD ALLOC: bool[capacity] — hashed slot occupancy flags — owner: WorldProceduralScatterDirector.FastCandidateMap
                _occupied = new bool[resolvedCapacity];
                // COLD ALLOC: int[capacity] — dense used-slot order for zero-GC iteration — owner: WorldProceduralScatterDirector.FastCandidateMap
                orderedSlots = new int[resolvedCapacity];

                count = 0;
                _capacity = resolvedCapacity;
                _mask = resolvedCapacity - 1;
                _log2Capacity = ComputeLog2(resolvedCapacity);
                _disposed = false;
            }

            public void Dispose()
            {
                if (_disposed)
                    return;

                values = null;
                keys = null;
                orderedSlots = null;
                _occupied = null;
                count = 0;
                _capacity = 0;
                _mask = 0;
                _log2Capacity = 0;
                _disposed = true;
            }

            internal bool TryAdd(long key, ScatterCandidate value)
            {
                if (TryGetIndex(key, out int existingIndex))
                {
                    values[existingIndex] = value;
                    return true;
                }

                return TryAppendKnownUnique(key, value);
            }

            internal bool TryAppendKnownUnique(long key, ScatterCandidate value)
            {
                if (!IsInitialized)
                    return false;

                if (count >= _capacity)
                {
                    LogCandidateMapCapacityExceeded(_capacity, key);
                    return false;
                }

                if (count >= ((_capacity * 3) / 4))
                    LogCandidateMapNearCapacity(count, _capacity);

                int bucket = GetBucket(key);
                for (int probe = 0; probe < _capacity; probe++)
                {
                    int slot = (bucket + probe) & _mask;
                    if (_occupied[slot])
                        continue;

                    _occupied[slot] = true;
                    keys[slot] = key;
                    values[slot] = value;
                    orderedSlots[count] = slot;
                    count++;
                    return true;
                }

                LogCandidateMapCapacityExceeded(_capacity, key);
                return false;
            }

            internal bool TryGetValue(long key, out ScatterCandidate value)
            {
                if (TryGetIndex(key, out int index))
                {
                    value = values[index];
                    return true;
                }

                value = default;
                return false;
            }

            public bool TryGetIndex(long key, out int index)
            {
                if (!IsInitialized)
                {
                    index = -1;
                    return false;
                }

                int bucket = GetBucket(key);
                for (int probe = 0; probe < _capacity; probe++)
                {
                    int slot = (bucket + probe) & _mask;
                    if (!_occupied[slot])
                        break;

                    if (keys[slot] == key)
                    {
                        index = slot;
                        return true;
                    }
                }

                index = -1;
                return false;
            }

            public bool Contains(long key)
            {
                return TryGetIndex(key, out _);
            }

            internal ScatterCandidate GetValueAtOrderedIndex(int orderedIndex)
            {
                return values[orderedSlots[orderedIndex]];
            }

            internal ScatterCandidate GetValueAtIndex(int index)
            {
                return values[index];
            }

            internal void SetValueAtIndex(int index, ScatterCandidate value)
            {
                values[index] = value;
            }

            public void Clear()
            {
                if (!IsInitialized || count <= 0)
                {
                    count = 0;
                    return;
                }

                for (int i = 0; i < count; i++)
                {
                    int slot = orderedSlots[i];
                    _occupied[slot] = false;
                    keys[slot] = 0L;
                    values[slot] = default;
                    orderedSlots[i] = 0;
                }

                count = 0;
            }

            private int GetBucket(long key)
            {
                ulong hash = unchecked((ulong)key * FibonacciHashMultiplier);
                return (int)((hash >> (64 - _log2Capacity)) & (ulong)_mask);
            }

            private static int ComputeLog2(int value)
            {
                int result = 0;
                while ((1 << result) < value)
                    result++;

                return result;
            }
        }
    }
}
