using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Branch-only finite-value guard for physics and runtime-state ingress.
    /// </summary>
    public static class MathGuard
    {
        private const int InvalidNumberQueuePrewarmCapacity = 256;
        private const int MaxMainThreadDrainPerLateFrame = 32;
        private const float MinDirectionLengthSq = 0.000001f;
        private const float UnitDirectionLengthSqTolerance = 0.0625f;
        private const int NaNErrorHash = unchecked((int)0x4E414E21); // "NAN!"
        private const BufferID InvalidNumberCodesBufferId = (BufferID)70883;
        private const BufferID InvalidNumberCounterBufferId = (BufferID)70884;
        private const SystemID VaultOwner = SystemID.CoreDiagnostics;

        private static IDataVault _dataVault;
        private static VaultGenerationHandle<int> _invalidNumberCodesHandle;
        private static VaultGenerationHandle<InvalidNumberCounter64> _invalidNumberCounterHandle;
        private static NativeArray<int> _invalidNumberCodes;
        private static NativeArray<InvalidNumberCounter64> _invalidNumberCounters;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Dispose();
        }

        /// <summary>Resolves the vault-owned invalid-number ring before Burst jobs can request a writer.</summary>
        public static void Initialize()
        {
            if (!EnsureInvalidNumberBuffers(allowAllocate: true))
                return;

            ref InvalidNumberCounter64 counter = ref ResolveCounterRef();
            ResetInvalidNumberCounters(ref counter);
        }

        /// <summary>Releases the vault-owned invalid-number ring handles.</summary>
        public static void Dispose()
        {
            IDataVault vault = _dataVault;
            if (vault != null)
            {
                if (IsVaultHandleCreated(in _invalidNumberCodesHandle))
                    vault.ReleaseBuffer(in _invalidNumberCodesHandle);
                if (IsVaultHandleCreated(in _invalidNumberCounterHandle))
                    vault.ReleaseBuffer(in _invalidNumberCounterHandle);
            }

            _invalidNumberCodes = default;
            _invalidNumberCounters = default;
            _invalidNumberCodesHandle = default;
            _invalidNumberCounterHandle = default;
            _dataVault = null;
        }

        /// <summary>Returns a Burst-safe writer for invalid-number error codes.</summary>
        public static InvalidNumberWriter AsParallelWriter()
        {
            return EnsureInvalidNumberBuffers(allowAllocate: false)
                ? new InvalidNumberWriter(_invalidNumberCodes, _invalidNumberCounters)
                : default;
        }

        /// <summary>
        /// Checks a vector and enqueues the supplied error code if any component is NaN or infinity.
        /// </summary>
        /// <param name="value">Value to validate.</param>
        /// <param name="errorCode">Deterministic caller-owned error code.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Check(float3 value, int errorCode)
        {
            if (math.all(math.isfinite(value)))
                return;

            InvalidNumberWriter writer = AsParallelWriter();
            writer.TryEnqueue(errorCode);
        }

        /// <summary>
        /// Burst-callable invalid-number check using a caller-supplied native queue writer.
        /// </summary>
        /// <param name="value">Value to validate.</param>
        /// <param name="errorCode">Deterministic caller-owned error code.</param>
        /// <param name="writer">Writer obtained from <see cref="AsParallelWriter"/> outside the job.</param>
        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Check(float3 value, int errorCode, InvalidNumberWriter writer)
        {
            if (!math.all(math.isfinite(value)))
                writer.TryEnqueue(errorCode);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 SanitizeFiniteOrZero(float3 value, int errorCode, InvalidNumberWriter writer)
        {
            if (math.all(math.isfinite(value)))
                return value;

            writer.TryEnqueue(errorCode);
            return float3.zero;
        }

        /// <summary>
        /// Drains invalid-number error codes into the telemetry bus.
        /// </summary>
        /// <param name="maxDrainCount">Maximum codes to consume this frame.</param>
        public static int DrainInvalidNumberErrors(int maxDrainCount = MaxMainThreadDrainPerLateFrame)
        {
            if (!EnsureInvalidNumberBuffers(allowAllocate: false) || maxDrainCount <= 0)
                return 0;

            ref InvalidNumberCounter64 counter = ref ResolveCounterRef();
            int writeCursor = Volatile.Read(ref counter.WriteCursor);
            int readCursor = counter.ReadCursor;
            int readable = math.min(writeCursor, InvalidNumberQueuePrewarmCapacity) - readCursor;
            if (readable <= 0)
            {
                if (readCursor >= InvalidNumberQueuePrewarmCapacity || writeCursor <= 0)
                    ResetInvalidNumberCounters(ref counter);
                return 0;
            }

            int drainedCount = 0;
            int drainTarget = math.min(maxDrainCount, readable);
            while (drainedCount < drainTarget)
            {
                int errorCode = _invalidNumberCodes[readCursor];
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(errorCode);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                DodReplayRecorder.RequestFullStateDump(
                    DeterministicReplaySeed.MathGuardSubjectHash,
                    unchecked((uint)errorCode));
#endif
                CrashTelemetryBuffer.ReportNanPhysicsRecovery();
                readCursor++;
                drainedCount++;
            }

            counter.ReadCursor = readCursor;
            if (readCursor >= writeCursor || readCursor >= InvalidNumberQueuePrewarmCapacity)
                ResetInvalidNumberCounters(ref counter);

            return drainedCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFinite(float value)
        {
            return math.isfinite(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFinite(float3 value)
        {
            return math.all(math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFinite(Vector3 value)
        {
            return IsFinite(new float3(value.x, value.y, value.z));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryAcceptFinite(float value, out float finite)
        {
            if (!math.isfinite(value))
            {
                finite = 0f;
                return false;
            }

            finite = value;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryAcceptFinite(float3 value, out float3 finite)
        {
            if (math.all(math.isfinite(value)))
            {
                finite = value;
                return true;
            }

            finite = DominantAxisPayload(value);
            Check(value, NaNErrorHash);
            return false;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryAcceptFinite(float3 value, out float3 finite, InvalidNumberWriter writer)
        {
            if (math.all(math.isfinite(value)))
            {
                finite = value;
                return true;
            }

            finite = DominantAxisPayload(value);
            writer.TryEnqueue(NaNErrorHash);
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryAcceptFinite(Vector3 value, out Vector3 finite)
        {
            bool accepted = TryAcceptFinite(new float3(value.x, value.y, value.z), out float3 finite3);
            finite = new Vector3(finite3.x, finite3.y, finite3.z);
            return accepted;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 DominantAxisPayload(float3 value)
        {
            bool3 finiteMask = math.isfinite(value);
            float3 finiteValue = math.select(float3.zero, value, finiteMask);
            float ax = math.abs(finiteValue.x);
            float ay = math.abs(finiteValue.y);
            float az = math.abs(finiteValue.z);
            if (ax >= ay && ax >= az)
            {
                return new float3(finiteValue.x, 0f, 0f);
            }

            if (ay >= az)
                return new float3(0f, finiteValue.y, 0f);

            return new float3(0f, 0f, finiteValue.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SanitizeFinite(float value, float fallback = 0f)
        {
            return math.isfinite(value) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 SanitizeFinite(float3 value, float3 fallback = default)
        {
            return IsFinite(value) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 SanitizeFinite(Vector3 value, Vector3 fallback = default)
        {
            return IsFinite(value) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SanitizeNonNegative(float value, float fallback = 0f)
        {
            float finite = SanitizeFinite(value, fallback);
            return math.max(0f, finite);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Sanitize01(float value, float fallback = 0f)
        {
            float finite = SanitizeFinite(value, fallback);
            return math.saturate(finite);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 SanitizeDirection(float3 value, float3 fallback)
        {
            if (IsFinite(value))
            {
                float lengthSq = math.lengthsq(value);
                if (lengthSq > MinDirectionLengthSq)
                    return math.abs(lengthSq - 1f) <= UnitDirectionLengthSqTolerance
                        ? value
                        : DominantAxisDirection(value);
            }

            if (IsFinite(fallback))
            {
                float fallbackLengthSq = math.lengthsq(fallback);
                if (fallbackLengthSq > MinDirectionLengthSq)
                    return math.abs(fallbackLengthSq - 1f) <= UnitDirectionLengthSqTolerance
                        ? fallback
                        : DominantAxisDirection(fallback);
            }

            return new float3(0f, 0f, 1f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 DominantAxisDirection(float3 value)
        {
            float ax = math.abs(value.x);
            float ay = math.abs(value.y);
            float az = math.abs(value.z);
            if (ax >= ay && ax >= az)
                return new float3(value.x < 0f ? -1f : 1f, 0f, 0f);
            if (ay >= az)
                return new float3(0f, value.y < 0f ? -1f : 1f, 0f);
            return new float3(0f, 0f, value.z < 0f ? -1f : 1f);
        }

        private static bool EnsureInvalidNumberBuffers(bool allowAllocate)
        {
            if (_invalidNumberCodes.IsCreated &&
                _invalidNumberCounters.IsCreated &&
                _invalidNumberCodes.Length >= InvalidNumberQueuePrewarmCapacity &&
                _invalidNumberCounters.Length > 0)
                return true;

            IDataVault vault = _dataVault ?? GlobalRegistry.DataVault;
            if (vault == null)
                return false;

            _dataVault = vault;
            if (!IsVaultHandleCreated(in _invalidNumberCodesHandle))
            {
                if (!allowAllocate || vault.IsAllocationLocked)
                {
                    if (!vault.TryGetGenerationHandle<int>(
                            InvalidNumberCodesBufferId,
                            out _invalidNumberCodesHandle))
                    {
                        return false;
                    }
                }
                else
                {
                    _invalidNumberCodesHandle = vault.GetGenerationHandle<int>(
                        InvalidNumberCodesBufferId,
                        InvalidNumberQueuePrewarmCapacity,
                        VaultOwner,
                        NativeArrayOptions.ClearMemory);
                }
            }

            if (!IsVaultHandleCreated(in _invalidNumberCounterHandle))
            {
                if (!allowAllocate || vault.IsAllocationLocked)
                {
                    if (!vault.TryGetGenerationHandle<InvalidNumberCounter64>(
                            InvalidNumberCounterBufferId,
                            out _invalidNumberCounterHandle))
                    {
                        return false;
                    }
                }
                else
                {
                    _invalidNumberCounterHandle = vault.GetGenerationHandle<InvalidNumberCounter64>(
                        InvalidNumberCounterBufferId,
                        1,
                        VaultOwner,
                        NativeArrayOptions.ClearMemory);
                }
            }

            bool resolved =
                vault.TryResolveHandle(in _invalidNumberCodesHandle, out _invalidNumberCodes) &&
                vault.TryResolveHandle(in _invalidNumberCounterHandle, out _invalidNumberCounters) &&
                _invalidNumberCodes.IsCreated &&
                _invalidNumberCounters.IsCreated &&
                _invalidNumberCodes.Length >= InvalidNumberQueuePrewarmCapacity &&
                _invalidNumberCounters.Length > 0;

            if (!resolved)
            {
                _invalidNumberCodes = default;
                _invalidNumberCounters = default;
                return false;
            }

            return true;
        }

        private static bool IsVaultHandleCreated<T>(in VaultGenerationHandle<T> handle)
            where T : struct
        {
            return handle.BufferID != 0u;
        }

        private static unsafe ref InvalidNumberCounter64 ResolveCounterRef()
        {
            void* ptr = NativeArrayUnsafeUtility.GetUnsafePtr(_invalidNumberCounters);
            return ref UnsafeUtility.AsRef<InvalidNumberCounter64>(ptr);
        }

        private static void ResetInvalidNumberCounters(ref InvalidNumberCounter64 counter)
        {
            counter.WriteCursor = 0;
            counter.ReadCursor = 0;
            counter.DroppedCount = 0;
            counter.OverflowFlag = 0;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        public struct InvalidNumberCounter64
        {
            [FieldOffset(0)] public int WriteCursor;
            [FieldOffset(4)] public int ReadCursor;
            [FieldOffset(8)] public int DroppedCount;
            [FieldOffset(12)] public int OverflowFlag;
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        public unsafe struct InvalidNumberWriter
        {
            [FieldOffset(0)]
            [NativeDisableUnsafePtrRestriction]
            private int* _codes;

            [FieldOffset(8)]
            [NativeDisableUnsafePtrRestriction]
            private InvalidNumberCounter64* _counter;

            [FieldOffset(16)] private int _capacity;
            [FieldOffset(20)] private uint _pad0;
            [FieldOffset(24)] private ulong _pad1;

            internal InvalidNumberWriter(
                NativeArray<int> codes,
                NativeArray<InvalidNumberCounter64> counters)
            {
                _codes = codes.IsCreated ? (int*)NativeArrayUnsafeUtility.GetUnsafePtr(codes) : null;
                _counter = counters.IsCreated ? (InvalidNumberCounter64*)NativeArrayUnsafeUtility.GetUnsafePtr(counters) : null;
                _capacity = codes.IsCreated ? codes.Length : 0;
                _pad0 = 0u;
                _pad1 = 0UL;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool TryEnqueue(int errorCode)
            {
                if (_codes == null || _counter == null || _capacity <= 0)
                    return false;

                ref int writeCursor = ref UnsafeUtility.AsRef<int>(&_counter->WriteCursor);
                int index = Interlocked.Increment(ref writeCursor) - 1;
                if ((uint)index >= (uint)_capacity)
                {
                    Interlocked.Increment(ref _counter->DroppedCount);
                    Interlocked.Exchange(ref _counter->OverflowFlag, 1);
                    return false;
                }

                _codes[index] = errorCode;
                return true;
            }
        }
    }
}
