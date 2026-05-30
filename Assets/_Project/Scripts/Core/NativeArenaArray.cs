using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.Core
{
    /// <summary>
    /// Frame-lifetime NativeContainer view over memory owned by <see cref="HectonArenaAllocator"/>.
    /// </summary>
    [NativeContainer]
    [NativeContainerSupportsMinMaxWriteRestriction]
    [NoAlias]
    public unsafe struct NativeArenaArray<T> where T : unmanaged
    {
        [NativeDisableUnsafePtrRestriction]
        [NoAlias]
        private void* _buffer;
        internal int m_Length;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal int m_MinIndex;
        internal int m_MaxIndex;
#endif
        private int _byteCount;
        private int _arenaIndex;
        private int _slabIndex;
        private int _frameSequence;
#pragma warning disable CS0414
        private int _pad0;
#pragma warning restore CS0414

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle m_Safety;
#endif

        public int Length => m_Length;
        public bool IsCreated => _buffer != null;
        public int ByteCount => _byteCount;
        public int ArenaIndex => _arenaIndex;
        public int SlabIndex => _slabIndex;
        public int FrameSequence => _frameSequence;

        public T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                CheckReadIndex(index);
                return UnsafeUtility.ReadArrayElement<T>(_buffer, index);
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                CheckWriteIndex(index);
                UnsafeUtility.WriteArrayElement(_buffer, index, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T ElementAt(int index)
        {
            CheckWriteIndex(index);
            return ref UnsafeUtility.ArrayElementAsRef<T>(_buffer, index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T GetElementAsRef(int index)
        {
            return ref ElementAt(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref readonly T GetElementAsReadOnlyRef(int index)
        {
            CheckReadIndex(index);
            return ref UnsafeUtility.ArrayElementAsRef<T>(_buffer, index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void* GetUnsafePtr()
        {
            CheckWrite();
            return _buffer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void* GetUnsafeReadOnlyPtr()
        {
            CheckRead();
            return _buffer;
        }

        public NativeArray<T> AsNativeArray()
        {
            if (_buffer == null || m_Length <= 0)
                return default;

            CheckWrite();
            NativeArray<T> array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(_buffer, m_Length, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, m_Safety);
#endif
            return array;
        }

        public NativeArray<T>.ReadOnly AsReadOnlyNativeArray()
        {
            if (_buffer == null || m_Length <= 0)
                return default;

            CheckRead();
            NativeArray<T> array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(_buffer, m_Length, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, m_Safety);
#endif
            return array.AsReadOnly();
        }

        public void Clear()
        {
            if (_buffer == null || _byteCount <= 0)
                return;

            CheckWrite();
            UnsafeUtility.MemClear(_buffer, _byteCount);
        }

        internal static NativeArenaArray<T> Create(
            void* ptr,
            int length,
            int byteCount,
            int arenaIndex,
            int slabIndex,
            int frameSequence)
        {
            NativeArenaArray<T> array = new NativeArenaArray<T>
            {
                _buffer = ptr,
                m_Length = length,
                _byteCount = byteCount,
                _arenaIndex = arenaIndex,
                _slabIndex = slabIndex,
                _frameSequence = frameSequence,
                _pad0 = 0
            };
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            array.m_Safety = HectonArenaAllocator.GetSafetyHandle(arenaIndex);
            array.m_MinIndex = 0;
            array.m_MaxIndex = length - 1;
#endif
            return array;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckReadIndex(int index)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
            if ((uint)index >= (uint)m_Length || index < m_MinIndex || index > m_MaxIndex)
                ThrowIndexOutOfRange(index);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckWriteIndex(int index)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
            if ((uint)index >= (uint)m_Length || index < m_MinIndex || index > m_MaxIndex)
                ThrowIndexOutOfRange(index);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckRead()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckWrite()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
        }

        private static void ThrowIndexOutOfRange(int index)
        {
            throw new IndexOutOfRangeException("NativeArenaArray index out of range.");
        }
    }
}
