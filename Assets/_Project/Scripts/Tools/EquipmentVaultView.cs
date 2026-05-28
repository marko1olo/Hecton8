namespace Hecton8.Tools
{
    using System.Runtime.CompilerServices;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;

    /// <summary>
    /// Stack-only wrapper for a current-phase DataVault view.
    /// It is not a persistent owner and must not be stored on a MonoBehaviour.
    /// </summary>
    internal ref struct EquipmentVaultView<T>
        where T : unmanaged
    {
        private NativeArray<T> _buffer;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EquipmentVaultView(NativeArray<T> buffer)
        {
            _buffer = buffer;
        }

        public bool IsCreated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _buffer.IsCreated;
        }

        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _buffer.Length;
        }

        public T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _buffer[index];
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => _buffer[index] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativeArray<T> AsNativeArray()
        {
            return _buffer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void* GetUnsafePtr()
        {
            return _buffer.GetUnsafePtr();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void* GetUnsafeReadOnlyPtr()
        {
            return _buffer.GetUnsafeReadOnlyPtr();
        }
    }
}
