using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.Core.Memory
{
    /// <summary>
    /// Registry-facing data-vault contract. Systems request buffers here instead of owning persistent arrays.
    /// </summary>
    public interface IDataVault : IDisposable
    {
        /// <summary>Total allocated vault bytes.</summary>
        long AllocatedBytes { get; }

        /// <summary>True while allocations are blocked for an AUP shift.</summary>
        bool IsAllocationLocked { get; }

        /// <summary>Returns a persistent buffer view, growing the vault buffer when required.</summary>
        NativeArray<T> GetBuffer<T>(BufferID bufferId, int requiredLength, SystemID requester, NativeArrayOptions options = NativeArrayOptions.ClearMemory) where T : struct;

        /// <summary>Attempts to read an existing buffer without creating or growing it.</summary>
        bool TryGetBuffer<T>(BufferID bufferId, out NativeArray<T> buffer) where T : struct;

        /// <summary>Returns a read-only alias over an existing buffer.</summary>
        NativeArray<T>.ReadOnly CreateAlias<T>(BufferID bufferId, SystemID requester) where T : struct;

        /// <summary>Locks vault allocation while AUP positions are being rebased.</summary>
        void LockAllocationsForAupShift(uint shiftFrameId);

        /// <summary>Unlocks vault allocation after an AUP shift barrier resolves.</summary>
        void UnlockAllocationsAfterAupShift(uint shiftFrameId);

        /// <summary>Runs cold fragmentation maintenance.</summary>
        void FrostTickDefrag(float elapsedSeconds);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct VaultBufferMeta
    {
        public int Length;
        public int Stride;
        public int Alignment;
        public long Bytes;
        public SystemID Owner;
        public Allocator Allocator;
        public uint Version;
    }

    /// <summary>
    /// Persistent raw-memory authority for cross-system buffers.
    /// </summary>
    public sealed unsafe class GlobalDataVault : IDataVault
    {
        private const int DefaultBufferCapacity = 128;

        private UnsafeHashMap<int, IntPtr> _buffers;
        private UnsafeHashMap<int, VaultBufferMeta> _metadata;
        private NativeList<int> _keys;
        private int _allocationLock;
        private uint _lockedShiftFrameId;
        private long _allocatedBytes;
        private bool _initialized;

        /// <inheritdoc />
        public long AllocatedBytes => _allocatedBytes;

        /// <inheritdoc />
        public bool IsAllocationLocked => _allocationLock != 0;

        /// <summary>
        /// Creates and initializes the vault for bootstrap registration.
        /// </summary>
        public static GlobalDataVault Create(int capacity = DefaultBufferCapacity)
        {
            GlobalDataVault vault = new GlobalDataVault();
            vault.Initialize(capacity);
            return vault;
        }

        /// <summary>
        /// Initializes raw vault maps.
        /// </summary>
        public void Initialize(int capacity = DefaultBufferCapacity)
        {
            if (_initialized)
                return;

            int safeCapacity = capacity > 0 ? capacity : DefaultBufferCapacity;
            H8Memory.Initialize();
            _buffers = new UnsafeHashMap<int, IntPtr>(safeCapacity, Allocator.Persistent);
            _metadata = new UnsafeHashMap<int, VaultBufferMeta>(safeCapacity, Allocator.Persistent);
            _keys = new NativeList<int>(safeCapacity, Allocator.Persistent);
            _allocationLock = 0;
            _lockedShiftFrameId = 0u;
            _allocatedBytes = 0L;
            _initialized = true;
        }

        /// <inheritdoc />
        public NativeArray<T> GetBuffer<T>(
            BufferID bufferId,
            int requiredLength,
            SystemID requester,
            NativeArrayOptions options = NativeArrayOptions.ClearMemory) where T : struct
        {
            if (requiredLength <= 0)
                return default;

            EnsureInitialized();
            int key = (int)bufferId;
            if (key == 0)
                return default;

            int stride = UnsafeUtility.SizeOf<T>();
            int alignment = UnsafeUtility.AlignOf<T>();
            long requiredBytes = (long)requiredLength * stride;

            bool hasExistingPointer = _buffers.TryGetValue(key, out IntPtr existingPointer);
            bool hasExistingMeta = _metadata.TryGetValue(key, out VaultBufferMeta existingMeta);
            if (hasExistingPointer != hasExistingMeta)
                return default;

            if (hasExistingPointer)
            {
                ValidateType<T>(bufferId, existingMeta, stride, alignment);
                if (existingMeta.Length >= requiredLength)
                    return H8Memory.CreateNativeArrayView<T>(existingPointer.ToPointer(), existingMeta.Length);

                if (_allocationLock != 0)
                    return default;

                void* resized = H8Memory.ReallocateRaw(
                    existingPointer.ToPointer(),
                    existingMeta.Bytes,
                    requiredBytes,
                    alignment,
                    SystemID.CoreDataVault,
                    existingMeta.Allocator,
                    ShouldClear(options),
                    H8AllocationFlags.Vault);
                if (resized == null)
                    return default;

                _allocatedBytes += requiredBytes - existingMeta.Bytes;
                existingMeta.Length = requiredLength;
                existingMeta.Bytes = requiredBytes;
                existingMeta.Version++;
                _buffers[key] = (IntPtr)resized;
                _metadata[key] = existingMeta;
                return H8Memory.CreateNativeArrayView<T>(resized, requiredLength);
            }

            if (_allocationLock != 0)
                return default;

            if (_keys.Length >= _keys.Capacity)
                return default;

            void* pointer = H8Memory.AllocateRaw(
                requiredBytes,
                alignment,
                SystemID.CoreDataVault,
                Allocator.Persistent,
                ShouldClear(options),
                H8AllocationFlags.Vault);
            if (pointer == null)
                return default;

            VaultBufferMeta meta = new VaultBufferMeta
            {
                Length = requiredLength,
                Stride = stride,
                Alignment = alignment,
                Bytes = requiredBytes,
                Owner = requester == SystemID.Unknown ? SystemID.CoreDataVault : requester,
                Allocator = Allocator.Persistent,
                Version = 1u
            };

            bool bufferAdded = _buffers.TryAdd(key, (IntPtr)pointer);
            bool metadataAdded = bufferAdded && _metadata.TryAdd(key, meta);
            if (!bufferAdded || !metadataAdded)
            {
                if (bufferAdded)
                    _buffers.Remove(key);
                if (!metadataAdded)
                    _metadata.Remove(key);

                H8Memory.FreeRaw(pointer, Allocator.Persistent);
                return default;
            }

            _keys.AddNoResize(key);
            _allocatedBytes += requiredBytes;
            return H8Memory.CreateNativeArrayView<T>(pointer, requiredLength);
        }

        /// <inheritdoc />
        public bool TryGetBuffer<T>(BufferID bufferId, out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (!_initialized)
                return false;

            int key = (int)bufferId;
            if (key == 0)
                return false;

            if (!_buffers.TryGetValue(key, out IntPtr pointer) ||
                !_metadata.TryGetValue(key, out VaultBufferMeta meta))
            {
                return false;
            }

            int stride = UnsafeUtility.SizeOf<T>();
            int alignment = UnsafeUtility.AlignOf<T>();
            ValidateType<T>(bufferId, meta, stride, alignment);
            buffer = H8Memory.CreateNativeArrayView<T>(pointer.ToPointer(), meta.Length);
            return buffer.IsCreated;
        }

        /// <inheritdoc />
        public NativeArray<T>.ReadOnly CreateAlias<T>(BufferID bufferId, SystemID requester) where T : struct
        {
            if (!TryGetBuffer<T>(bufferId, out NativeArray<T> buffer))
                return default;

            return H8Memory.CreateAlias(buffer, requester);
        }

        /// <inheritdoc />
        public void LockAllocationsForAupShift(uint shiftFrameId)
        {
            _allocationLock = 1;
            _lockedShiftFrameId = shiftFrameId;
        }

        /// <inheritdoc />
        public void UnlockAllocationsAfterAupShift(uint shiftFrameId)
        {
            if (_allocationLock == 0)
                return;

            if (_lockedShiftFrameId != 0u && shiftFrameId != 0u && _lockedShiftFrameId != shiftFrameId)
                return;

            _lockedShiftFrameId = 0u;
            _allocationLock = 0;
        }

        /// <inheritdoc />
        public void FrostTickDefrag(float elapsedSeconds)
        {
            if (!_initialized || elapsedSeconds < 0f)
                return;

            // Fragmentation evaluation is cold and intentionally non-moving unless a future owner supplies relocation-safe handles.
            // Moving buffers blindly would invalidate outstanding NativeArray views and violate the AUP shift lock contract.
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (!_initialized)
                return;

            for (int i = 0; i < _keys.Length; i++)
            {
                int key = _keys[i];
                if (!_buffers.TryGetValue(key, out IntPtr pointer) ||
                    !_metadata.TryGetValue(key, out VaultBufferMeta meta))
                {
                    continue;
                }

                H8Memory.FreeRaw(pointer.ToPointer(), meta.Allocator);
            }

            _keys.Dispose();
            _buffers.Dispose();
            _metadata.Dispose();
            _allocatedBytes = 0L;
            _allocationLock = 0;
            _initialized = false;
        }

        private void EnsureInitialized()
        {
            if (!_initialized)
                Initialize();
        }

        private static bool ShouldClear(NativeArrayOptions options)
        {
            return options != NativeArrayOptions.UninitializedMemory;
        }

        private static void ValidateType<T>(BufferID bufferId, VaultBufferMeta meta, int stride, int alignment) where T : struct
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (meta.Stride != stride || meta.Alignment != alignment)
                throw new InvalidOperationException("GlobalDataVault buffer type mismatch: " + bufferId);
#endif
        }
    }
}
