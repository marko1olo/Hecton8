using System;
using System.Runtime.CompilerServices;

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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool _indexErrorLogged;
        private bool _nullRegistrationLogged;
        private bool _capacityErrorLogged;
        private bool _unregisterMissLogged;
        private bool _destroyedEntryLogged;
#endif

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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T GetAt(int index)
        {
            if ((uint)index >= (uint)_count)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (!_indexErrorLogged)
                {
                    Hecton8.Core.H8Debug.LogError(
                        $"[RegistryBucket<{typeof(T).Name}>] Index {index} outside live count {_count}.");
                    _indexErrorLogged = true;
                }
#endif
                return null;
            }

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
            TryRegister(item);
        }

        /// <summary>
        /// Attempts to append a new item to the tail of the dense array.
        /// </summary>
        /// <param name="item">Item instance to register.</param>
        /// <returns>True when the item was registered.</returns>
        public bool TryRegister(T item)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (item == null)
            {
                if (!_nullRegistrationLogged)
                {
                    Hecton8.Core.H8Debug.LogError(
                        $"[RegistryBucket<{typeof(T).Name}>] Null registration is forbidden.");
                    _nullRegistrationLogged = true;
                }
                return false;
            }

            if (_count >= _capacity)
            {
                if (!_capacityErrorLogged)
                {
                    Hecton8.Core.H8Debug.LogError(
                        $"[GlobalRegistry] RegistryBucket<{typeof(T).Name}> capacity ({_capacity}) exceeded.");
                    _capacityErrorLogged = true;
                }
                return false;
            }

            if (Contains(item))
                return false;
#else
            if (item == null || _count >= _capacity || Contains(item))
                return false;
#endif
            _items[_count++] = item;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _indexErrorLogged = false;
            _destroyedEntryLogged = false;
#endif
            return true;
        }

        /// <summary>
        /// Removes an item using O(1) swap-with-last tail compaction.
        /// </summary>
        /// <param name="item">Item instance to remove.</param>
        /// <returns>True when the item was found and removed.</returns>
        public bool Unregister(T item)
        {
            if (TryUnregister(item))
                return true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_unregisterMissLogged)
            {
                Hecton8.Core.H8Debug.LogWarning(
                    $"[GlobalRegistry] Unregister called for non-registered {typeof(T).Name}.");
                _unregisterMissLogged = true;
            }
#endif
            return false;
        }

        /// <summary>
        /// Removes an item without emitting a miss warning; used by idempotent lifecycle teardown paths.
        /// </summary>
        /// <param name="item">Item instance to remove.</param>
        /// <returns>True when the item was found and removed.</returns>
        public bool TryUnregister(T item)
        {
            for (int i = 0; i < _count; i++)
            {
                if (!ReferenceEquals(_items[i], item))
                    continue;

                _count--;
                _items[i] = _items[_count];
                _items[_count] = null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                _indexErrorLogged = false;
                _unregisterMissLogged = false;
                _destroyedEntryLogged = false;
#endif
                return true;
            }

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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _indexErrorLogged = false;
            _nullRegistrationLogged = false;
            _capacityErrorLogged = false;
            _unregisterMissLogged = false;
            _destroyedEntryLogged = false;
#endif
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

                if (!_destroyedEntryLogged)
                {
                    Hecton8.Core.H8Debug.LogError(
                        $"[RegistryBucket<{typeof(T).Name}>] Destroyed object remained registered in {bucketName} at index {i}.");
                    _destroyedEntryLogged = true;
                }
                return;
            }
        }
#endif
    }
}
