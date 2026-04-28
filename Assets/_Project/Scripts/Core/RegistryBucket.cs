using System;

namespace Hecton8.Core
{
    /// <summary>
    /// Dense fixed-capacity registry bucket with O(1) swap-with-last removal.
    /// </summary>
    /// <typeparam name="T">Reference type stored in the bucket.</typeparam>
    public sealed class RegistryBucket<T> where T : class
    {
        private readonly T[] _items;
        private readonly int _capacity;
        private int _count;

        /// <summary>
        /// Active item count.
        /// </summary>
        public int Count => _count;

        /// <summary>
        /// Raw contiguous backing array for zero-allocation linear scans.
        /// </summary>
        public T[] RawArray => _items;

        /// <summary>
        /// Returns the live item at the given dense-array index.
        /// </summary>
        /// <param name="index">Dense registry index.</param>
        /// <returns>Registered item at the requested index.</returns>
        public T GetAt(int index)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if ((uint)index >= (uint)_count)
                throw new ArgumentOutOfRangeException(nameof(index));
#endif
            return _items[index];
        }

        /// <summary>
        /// Creates a fixed-capacity registry bucket.
        /// </summary>
        /// <param name="capacity">Maximum number of live entries allowed in the bucket.</param>
        public RegistryBucket(int capacity)
        {
            _capacity = Math.Max(1, capacity);
            _items = new T[_capacity]; // COLD ALLOC: T[_capacity] — dense registry backing storage — owner: RegistryBucket<T>
            _count = 0;
        }

        /// <summary>
        /// Appends a new item to the tail of the dense array.
        /// </summary>
        /// <param name="item">Item instance to register.</param>
        public void Register(T item)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (item == null)
                throw new ArgumentNullException(nameof(item), $"[RegistryBucket<{typeof(T).Name}>] Null registration is forbidden.");

            if (_count >= _capacity)
            {
                throw new InvalidOperationException(
                    $"[GlobalRegistry] RegistryBucket<{typeof(T).Name}> capacity ({_capacity}) exceeded.");
            }

            if (Contains(item))
            {
                throw new InvalidOperationException(
                    $"[GlobalRegistry] Double-registration detected for {typeof(T).Name}.");
            }
#endif
            _items[_count++] = item;
        }

        /// <summary>
        /// Removes an item using O(1) swap-with-last tail compaction.
        /// </summary>
        /// <param name="item">Item instance to remove.</param>
        /// <returns>True when the item was found and removed.</returns>
        public bool Unregister(T item)
        {
            for (int i = 0; i < _count; i++)
            {
                if (!ReferenceEquals(_items[i], item))
                    continue;

                _count--;
                _items[i] = _items[_count];
                _items[_count] = null;
                return true;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UnityEngine.Debug.LogWarning(
                $"[GlobalRegistry] Unregister called for non-registered {typeof(T).Name}.");
#endif
            return false;
        }

        /// <summary>
        /// Checks whether a given instance is already present in the bucket.
        /// </summary>
        /// <param name="item">Item instance to test.</param>
        /// <returns>True when the instance is present.</returns>
        public bool Contains(T item)
        {
            for (int i = 0; i < _count; i++)
            {
                if (ReferenceEquals(_items[i], item))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Clears all live entries and nulls the dense storage window.
        /// </summary>
        public void Clear()
        {
            Array.Clear(_items, 0, _count);
            _count = 0;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Debug-only validation that detects Unity objects destroyed without unregistering first.
        /// </summary>
        /// <param name="bucketName">Human-readable bucket label for diagnostics.</param>
        public void ValidateNoDestroyedEntriesDebug(string bucketName)
        {
            for (int i = 0; i < _count; i++)
            {
                if (!(_items[i] is UnityEngine.Object unityObject))
                    continue;

                if (unityObject != null)
                    continue;

                UnityEngine.Debug.LogError(
                    $"[RegistryBucket<{typeof(T).Name}>] Destroyed object remained registered in {bucketName} at index {i}.");
                return;
            }
        }
#endif
    }
}
