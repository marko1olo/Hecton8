using System;
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
        private const BufferID InvalidNumberCodesBufferId = BufferID.MathGuard_InvalidNumberCodesBufferId;
        private const BufferID InvalidNumberCounterBufferId = BufferID.MathGuard_InvalidNumberCounterBufferId;
        private const SystemID VaultOwner = SystemID.CoreDiagnostics;

        private static IDataVault _dataVault;
        private static VaultGenerationHandle<int> _invalidNumberCodesHandle;
        private static VaultGenerationHandle<InvalidNumberCounter64> _invalidNumberCounterHandle;
        private static bool _invalidNumberMutationGuardHeld;
        private static readonly ulong InvalidNumberMutationGuardMask =
            MutationGuardBit(InvalidNumberCodesBufferId) |
            MutationGuardBit(InvalidNumberCounterBufferId);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Dispose();
        }

        /// <summary>Resolves the vault-owned invalid-number ring before Burst jobs can request a writer.</summary>
        public static void Initialize()
        {
            IDataVault vault = _dataVault;
            if (!OpenOrAcquireInvalidNumberBuffersForOwnerRoute(vault))
                return;

            if (!TryAcquireInvalidNumberCounterWriteBuffer(vault, out NativeArray<InvalidNumberCounter64> invalidNumberCounters))
                return;

            try
            {
                ref InvalidNumberCounter64 counter = ref ResolveCounterRef(invalidNumberCounters);
                ResetInvalidNumberCounters(ref counter);
            }
            finally
            {
                ReleaseInvalidNumberCounterWriteLock(vault);
            }

            TryAcquireInvalidNumberMutationGuard(vault);
        }

        /// <summary>Binds the bootstrap-owned DataVault used for invalid-number telemetry.</summary>
        internal static void BindDataVaultCold(IDataVault vault)
        {
            if (ReferenceEquals(_dataVault, vault))
            {
                if (vault != null)
                    Initialize();
                return;
            }

            ReleaseInvalidNumberBuffers(_dataVault);
            _dataVault = vault;
            if (vault != null)
                Initialize();
        }

        /// <summary>Releases the vault-owned invalid-number ring handles.</summary>
        public static void Dispose()
        {
            ReleaseInvalidNumberBuffers(_dataVault);
            _dataVault = null;
        }

        private static void ReleaseInvalidNumberBuffers(IDataVault vault)
        {
            if (vault != null)
            {
                if (_invalidNumberMutationGuardHeld)
                    ReleaseInvalidNumberMutationGuardNoThrow(vault);

                if (IsVaultHandleCreated(in _invalidNumberCodesHandle))
                    vault.ReleaseBuffer(in _invalidNumberCodesHandle);
                if (IsVaultHandleCreated(in _invalidNumberCounterHandle))
                    vault.ReleaseBuffer(in _invalidNumberCounterHandle);
            }

            _invalidNumberCodesHandle = default;
            _invalidNumberCounterHandle = default;
            _invalidNumberMutationGuardHeld = false;
        }

        /// <summary>Returns a Burst-safe writer for invalid-number error codes.</summary>
        public static InvalidNumberWriter AsParallelWriter()
        {
            IDataVault vault = _dataVault;
            return _invalidNumberMutationGuardHeld &&
                TryOpenExistingInvalidNumberBuffersForOwnerRoute(
                    vault,
                    out NativeArray<int> invalidNumberCodes,
                    out NativeArray<InvalidNumberCounter64> invalidNumberCounters)
                ? new InvalidNumberWriter(invalidNumberCodes, invalidNumberCounters)
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
            int maxCodesToDrain = math.clamp(maxDrainCount, 0, MaxMainThreadDrainPerLateFrame);
            if (maxCodesToDrain <= 0)
                return 0;

            IDataVault vault = _dataVault;
            ReleaseInvalidNumberMutationGuardNoThrow(vault);
            if (!TryReadInvalidNumberCodes(vault, out NativeArray<int>.ReadOnly invalidNumberCodes) ||
                !TryAcquireInvalidNumberCounterWriteBuffer(vault, out NativeArray<InvalidNumberCounter64> invalidNumberCounters))
            {
                TryAcquireInvalidNumberMutationGuard(vault);
                return 0;
            }

            Span<int> drainedCodes = stackalloc int[MaxMainThreadDrainPerLateFrame];
            int drainedCount = 0;

            try
            {
                ref InvalidNumberCounter64 counter = ref ResolveCounterRef(invalidNumberCounters);
                int writeCursor = Volatile.Read(ref counter.WriteCursor);
                int readCursor = counter.ReadCursor;
                int readable = math.min(writeCursor, InvalidNumberQueuePrewarmCapacity) - readCursor;
                if (readable <= 0)
                {
                    if (readCursor >= InvalidNumberQueuePrewarmCapacity || writeCursor <= 0)
                        ResetInvalidNumberCounters(ref counter);
                    return 0;
                }

                int drainTarget = math.min(maxCodesToDrain, readable);
                while (drainedCount < drainTarget)
                {
                    drainedCodes[drainedCount] = invalidNumberCodes[readCursor];
                    readCursor++;
                    drainedCount++;
                }

                counter.ReadCursor = readCursor;
                if (readCursor >= writeCursor || readCursor >= InvalidNumberQueuePrewarmCapacity)
                    ResetInvalidNumberCounters(ref counter);
            }
            finally
            {
                ReleaseInvalidNumberCounterWriteLock(vault);
                TryAcquireInvalidNumberMutationGuard(vault);
            }

            for (int i = 0; i < drainedCount; i++)
            {
                int errorCode = drainedCodes[i];
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(errorCode);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                DodReplayRecorder.RequestFullStateDump(
                    DeterministicReplaySeed.MathGuardSubjectHash,
                    unchecked((uint)errorCode));
#endif
                CrashTelemetryBuffer.ReportNanPhysicsRecovery();
            }

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

        private static bool OpenOrAcquireInvalidNumberBuffersForOwnerRoute(IDataVault vault)
        {
            if (vault == null)
                return false;

            if (!IsVaultHandleCreated(in _invalidNumberCodesHandle) &&
                !OpenOrAcquireInvalidNumberHandleForOwnerRoute(
                    vault,
                    InvalidNumberCodesBufferId,
                    InvalidNumberQueuePrewarmCapacity,
                    out _invalidNumberCodesHandle))
            {
                return false;
            }

            if (!IsVaultHandleCreated(in _invalidNumberCounterHandle) &&
                !OpenOrAcquireInvalidNumberHandleForOwnerRoute(
                    vault,
                    InvalidNumberCounterBufferId,
                    1,
                    out _invalidNumberCounterHandle))
            {
                return false;
            }

            return TryOpenExistingInvalidNumberBuffersForOwnerRoute(vault, out _, out _);
        }

        private static bool OpenOrAcquireInvalidNumberHandleForOwnerRoute<T>(
            IDataVault vault,
            BufferID bufferId,
            int capacity,
            out VaultGenerationHandle<T> handle)
            where T : struct
        {
            handle = default;
            if (vault == null || capacity <= 0)
                return false;

            if (vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                return vault.TryGetGenerationHandle<T>(bufferId, out handle);

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                capacity,
                VaultOwner,
                NativeArrayOptions.ClearMemory);
            return IsVaultHandleCreated(in handle);
        }

        private static bool TryOpenExistingInvalidNumberBuffersForOwnerRoute(
            out NativeArray<int> invalidNumberCodes,
            out NativeArray<InvalidNumberCounter64> invalidNumberCounters)
        {
            return TryOpenExistingInvalidNumberBuffersForOwnerRoute(
                _dataVault,
                out invalidNumberCodes,
                out invalidNumberCounters);
        }

        private static bool TryOpenExistingInvalidNumberBuffersForOwnerRoute(
            IDataVault vault,
            out NativeArray<int> invalidNumberCodes,
            out NativeArray<InvalidNumberCounter64> invalidNumberCounters)
        {
            invalidNumberCodes = default;
            invalidNumberCounters = default;

            if (vault == null)
                return false;

            return
                IsVaultHandleCreated(in _invalidNumberCodesHandle) &&
                IsVaultHandleCreated(in _invalidNumberCounterHandle) &&
                vault.TryResolveHandle(in _invalidNumberCodesHandle, out invalidNumberCodes) &&
                vault.TryResolveHandle(in _invalidNumberCounterHandle, out invalidNumberCounters) &&
                invalidNumberCodes.IsCreated &&
                invalidNumberCounters.IsCreated &&
                invalidNumberCodes.Length >= InvalidNumberQueuePrewarmCapacity &&
                invalidNumberCounters.Length > 0;
        }

        private static bool TryAcquireInvalidNumberMutationGuard(IDataVault vault)
        {
            if (_invalidNumberMutationGuardHeld)
                return true;

            if (vault == null || !vault.TryAcquireMutationGuard(InvalidNumberMutationGuardMask))
                return false;

            _invalidNumberMutationGuardHeld = true;
            return true;
        }

        private static void ReleaseInvalidNumberMutationGuardNoThrow(IDataVault vault)
        {
            if (!_invalidNumberMutationGuardHeld)
                return;

            try
            {
                vault?.ReleaseMutationGuard(InvalidNumberMutationGuardMask);
            }
            catch (Exception)
            {
            }
            finally
            {
                _invalidNumberMutationGuardHeld = false;
            }
        }

        private static bool TryReadInvalidNumberCodes(out NativeArray<int>.ReadOnly invalidNumberCodes)
        {
            return TryReadInvalidNumberCodes(_dataVault, out invalidNumberCodes);
        }

        private static bool TryReadInvalidNumberCodes(IDataVault vault, out NativeArray<int>.ReadOnly invalidNumberCodes)
        {
            invalidNumberCodes = default;
            return vault != null &&
                IsVaultHandleCreated(in _invalidNumberCodesHandle) &&
                vault.TryReadOnlyHandle(in _invalidNumberCodesHandle, out invalidNumberCodes) &&
                invalidNumberCodes.Length >= InvalidNumberQueuePrewarmCapacity;
        }

        private static bool TryAcquireInvalidNumberCounterWriteBuffer(
            out NativeArray<InvalidNumberCounter64> invalidNumberCounters)
        {
            return TryAcquireInvalidNumberCounterWriteBuffer(_dataVault, out invalidNumberCounters);
        }

        private static bool TryAcquireInvalidNumberCounterWriteBuffer(
            IDataVault vault,
            out NativeArray<InvalidNumberCounter64> invalidNumberCounters)
        {
            invalidNumberCounters = default;
            if (vault == null ||
                !IsVaultHandleCreated(in _invalidNumberCounterHandle) ||
                !vault.TryAcquireWriteLock(in _invalidNumberCounterHandle, VaultOwner, out invalidNumberCounters))
            {
                return false;
            }

            bool handedOff = false;
            try
            {
                if (invalidNumberCounters.IsCreated && invalidNumberCounters.Length > 0)
                {
                    handedOff = true;
                    return true;
                }

                invalidNumberCounters = default;
                return false;
            }
            finally
            {
                if (!handedOff)
                    vault.ReleaseWriteLock(in _invalidNumberCounterHandle, VaultOwner);
            }
        }

        private static void ReleaseInvalidNumberCounterWriteLock()
        {
            ReleaseInvalidNumberCounterWriteLock(_dataVault);
        }

        private static void ReleaseInvalidNumberCounterWriteLock(IDataVault vault)
        {
            if (vault != null && IsVaultHandleCreated(in _invalidNumberCounterHandle))
                vault.ReleaseWriteLock(in _invalidNumberCounterHandle, VaultOwner);
        }

        private static bool IsVaultHandleCreated<T>(in VaultGenerationHandle<T> handle)
            where T : struct
        {
            return handle.BufferID != 0u;
        }

        private static ulong MutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 63);
        }

        private static unsafe ref InvalidNumberCounter64 ResolveCounterRef(NativeArray<InvalidNumberCounter64> invalidNumberCounters)
        {
            void* ptr = NativeArrayUnsafeUtility.GetUnsafePtr(invalidNumberCounters);
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
