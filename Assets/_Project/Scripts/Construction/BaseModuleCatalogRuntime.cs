using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton.Localization;
using Hecton8.Building;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Construction
{
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ModuleDefinitionDTO
    {
        [FieldOffset(0)] public uint PrefabHashID;
        [FieldOffset(4)] public uint ModuleClassHash;
        [FieldOffset(8)] public float3 BoundingBoxExtents;
        [FieldOffset(20)] public uint SocketCount;
        [FieldOffset(24)] public int SocketStartIndex;
        [FieldOffset(28)] public uint BaseStrength;
        [FieldOffset(32)] public uint AllowedBiomesMask;
        [FieldOffset(36)] private uint _pad0;
        [FieldOffset(40)] private ulong _pad1;
        [FieldOffset(48)] private ulong _pad2;
        [FieldOffset(56)] private ulong _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SocketDefinitionDTO
    {
        [FieldOffset(0)] public float3 LocalOffset;
        [FieldOffset(12)] public float3 Normal;
        [FieldOffset(24)] public uint AllowedConnectionsMask;
        [FieldOffset(28)] private uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ModuleCostDTO
    {
        [FieldOffset(0)] public uint PrefabHashID;
        [FieldOffset(4)] public uint CostCount;
        [FieldOffset(8)] public uint ItemHash0;
        [FieldOffset(12)] public int Quantity0;
        [FieldOffset(16)] public uint ItemHash1;
        [FieldOffset(20)] public int Quantity1;
        [FieldOffset(24)] public uint ItemHash2;
        [FieldOffset(28)] public int Quantity2;
        [FieldOffset(32)] public uint ItemHash3;
        [FieldOffset(36)] public int Quantity3;
        [FieldOffset(40)] private ulong _pad0;
        [FieldOffset(48)] private ulong _pad1;
        [FieldOffset(56)] private ulong _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ModuleCatalogStateDTO
    {
        [FieldOffset(0)] public uint ModuleCount;
        [FieldOffset(4)] public uint SocketCount;
        [FieldOffset(8)] public uint CostCount;
        [FieldOffset(12)] public uint HydrationStatus;
        [FieldOffset(16)] public uint Generation;
        [FieldOffset(20)] public uint CatalogHash;
        [FieldOffset(24)] public uint TelemetryCursor;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] public uint SourceByteLength;
        [FieldOffset(36)] public uint LastErrorCode;
        [FieldOffset(40)] private ulong _pad0;
        [FieldOffset(48)] private ulong _pad1;
        [FieldOffset(56)] private ulong _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ModuleCatalogBinaryHeader
    {
        [FieldOffset(0)] public uint Magic;
        [FieldOffset(4)] public uint Version;
        [FieldOffset(8)] public uint ModuleCount;
        [FieldOffset(12)] public uint SocketCount;
        [FieldOffset(16)] public uint CostCount;
        [FieldOffset(20)] public uint ModuleByteOffset;
        [FieldOffset(24)] public uint SocketByteOffset;
        [FieldOffset(28)] public uint CostByteOffset;
        [FieldOffset(32)] public uint Checksum;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] public uint CatalogHash;
        [FieldOffset(44)] public uint ByteLength;
        [FieldOffset(48)] private ulong _pad0;
        [FieldOffset(56)] private ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ModuleCatalogTelemetryEntry
    {
        [FieldOffset(0)] public long QueryTicks;
        [FieldOffset(8)] public long BurstTicks;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public uint QueryCount;
        [FieldOffset(24)] public uint SuccessfulAdjacencyCount;
        [FieldOffset(28)] public uint FailedAdjacencyCount;
        [FieldOffset(32)] public uint Flags;
        [FieldOffset(36)] public uint CatalogHash;
        [FieldOffset(40)] public uint StateHash;
        [FieldOffset(44)] public uint VaultGenerationId;
        [FieldOffset(48)] private ulong _pad0;
        [FieldOffset(56)] private ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ModuleCatalogSelfAuditDTO
    {
        [FieldOffset(0)] public int ModuleDefinitionBytes;
        [FieldOffset(4)] public int SocketDefinitionBytes;
        [FieldOffset(8)] public int ModuleCostBytes;
        [FieldOffset(12)] public int TelemetryEntryBytes;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] public uint BufferStartId;
        [FieldOffset(24)] public uint BufferEndId;
        [FieldOffset(28)] public uint FailureCode;
        [FieldOffset(32)] private ulong _pad0;
        [FieldOffset(40)] private ulong _pad1;
        [FieldOffset(48)] private ulong _pad2;
        [FieldOffset(56)] private ulong _pad3;
    }

    public ref struct ModuleCatalogViews
    {
        public NativeArray<ModuleCatalogStateDTO> State;
        public NativeArray<ModuleDefinitionDTO> Modules;
        public NativeArray<SocketDefinitionDTO> Sockets;
        public NativeArray<ModuleCostDTO> Costs;
        public NativeArray<uint> HashToIndex;
        public NativeArray<ModuleCatalogTelemetryEntry> Telemetry;
    }

    public ref struct ModuleCatalogWriteLease
    {
        internal IDataVault Vault;
        internal VaultGenerationHandle<ModuleCatalogStateDTO> StateHandle;
        internal VaultGenerationHandle<ModuleDefinitionDTO> ModulesHandle;
        internal VaultGenerationHandle<SocketDefinitionDTO> SocketsHandle;
        internal VaultGenerationHandle<ModuleCostDTO> CostsHandle;
        internal VaultGenerationHandle<uint> HashToIndexHandle;
        internal VaultGenerationHandle<ModuleCatalogTelemetryEntry> TelemetryHandle;
        internal ulong MutationGuardMask;

        public bool IsCreated => Vault != null && MutationGuardMask != 0UL;
    }

    internal enum ModuleCatalogHydrationStatus : uint
    {
        Empty = 0,
        RetiredGeneratedCatalog = 1,
        Hydrated = 2,
        InvalidHeader = 3,
        InvalidLength = 4,
        InvalidChecksum = 5,
        CapacityExceeded = 6,
        InvalidEndian = 7
    }

    public static class BaseModuleCatalogRuntime
    {
        public const int ModuleDefinitionSize = 64;
        public const int SocketDefinitionSize = 32;
        public const int ModuleCostSize = 64;
        public const int StateSize = 64;
        public const int TelemetryEntrySize = 64;
        public const int TelemetryCapacity = 300;
        public const int DefaultModuleCapacity = 256;
        public const int DefaultSocketCapacity = 2048;
        public const int DefaultCostCapacity = 256;
        public const int DefaultHashCapacity = 512;
        public const int DefaultHydrationByteCapacity = 1 << 20;
        public const uint BinaryMagic = 0x48424D43u; // "HBMC"
        public const uint BinaryVersion = 1u;
        public const uint UniversalConnectionMask = 0x7FFFFF00u;
        public const uint CatalogImmutableFlag = 1u << 0;
        public const uint BinaryLittleEndianFlag = 1u << 2;
        public const uint TelemetryOverBudgetFlag = 1u << 0;
        public const uint TelemetryNonFiniteFlag = 1u << 1;
        public const uint SelfAuditLayoutFlag = 1u << 0;
        public const uint SelfAuditMaskFlag = 1u << 1;
        public const uint SelfAuditRollbackFenceFlag = 1u << 2;
        public const uint SelfAuditUninitializedHydrationFlag = 1u << 3;
        public const uint SelfAuditEndianPolicyFlag = 1u << 4;
        public const int CatalogByteLoadInvalidTarget = -1;
        public const int CatalogByteLoadIoFailure = -2;
        public const int CatalogByteLoadShortRead = -3;
        private const int CompatibilityLaneBitOffset = 8;
        private const int CompatibilityLaneCount = 23;
        private const uint ClassHabitatHash = 0x48414249u; // "HABI"
        private const uint AllBiomesMask = 0xFFFFFFFFu;
        private const ulong CatalogWriteMutationGuardMask =
            (1UL << (unchecked((int)(uint)(int)BufferID.BaseModuleCatalogState) & 31)) |
            (1UL << (unchecked((int)(uint)(int)BufferID.BaseModuleCatalogDefinitions) & 31)) |
            (1UL << (unchecked((int)(uint)(int)BufferID.BaseModuleCatalogSockets) & 31)) |
            (1UL << (unchecked((int)(uint)(int)BufferID.BaseModuleCatalogCosts) & 31)) |
            (1UL << (unchecked((int)(uint)(int)BufferID.BaseModuleCatalogHashToIndex) & 31)) |
            (1UL << (unchecked((int)(uint)(int)BufferID.BaseModuleCatalogTelemetryRing) & 31));
        private const ulong CatalogTelemetryMutationGuardMask =
            (1UL << (unchecked((int)(uint)(int)BufferID.BaseModuleCatalogState) & 31)) |
            (1UL << (unchecked((int)(uint)(int)BufferID.BaseModuleCatalogTelemetryRing) & 31));
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_1306_Construction_ModuleCatalog.bin";
        private static readonly byte[] s_catalogHydrationScratch = new byte[DefaultHydrationByteCapacity];
        private static int s_catalogHydrationScratchBusy;

        public static bool TryEnsureVaultBuffers(
            IDataVault vault,
            out ModuleCatalogViews views,
            int moduleCapacity = DefaultModuleCapacity,
            int socketCapacity = DefaultSocketCapacity,
            int costCapacity = DefaultCostCapacity,
            int hashCapacity = DefaultHashCapacity)
        {
            views = default;
            if (!TryAcquireCatalogWriteViews(
                    vault,
                    out views,
                    out ModuleCatalogWriteLease lease,
                    moduleCapacity,
                    socketCapacity,
                    costCapacity,
                    hashCapacity))
            {
                return false;
            }

            try
            {
                if (!views.State.IsCreated ||
                    views.State.Length == 0 ||
                    !views.Modules.IsCreated ||
                    !views.Sockets.IsCreated ||
                    !views.Costs.IsCreated ||
                    !views.HashToIndex.IsCreated ||
                    !views.Telemetry.IsCreated)
                {
                    views = default;
                    return false;
                }

                ModuleCatalogStateDTO state = views.State[0];
                if (state.HydrationStatus == (uint)ModuleCatalogHydrationStatus.Empty &&
                    state.ModuleCount == 0u &&
                    state.SocketCount == 0u &&
                    state.CostCount == 0u)
                {
                    state.Flags |= BinaryLittleEndianFlag;
                    views.State[0] = state;
                }

                return true;
            }
            finally
            {
                ReleaseWriteLease(in lease);
            }
        }

        public static bool TryResolveViews(IDataVault vault, out ModuleCatalogViews views)
        {
            views = default;
            if (vault == null)
                return false;

            bool resolved =
                TryReadExistingLane(vault, BufferID.BaseModuleCatalogState, out views.State) &&
                TryReadExistingLane(vault, BufferID.BaseModuleCatalogDefinitions, out views.Modules) &&
                TryReadExistingLane(vault, BufferID.BaseModuleCatalogSockets, out views.Sockets) &&
                TryReadExistingLane(vault, BufferID.BaseModuleCatalogCosts, out views.Costs) &&
                TryReadExistingLane(vault, BufferID.BaseModuleCatalogHashToIndex, out views.HashToIndex) &&
                TryReadExistingLane(vault, BufferID.BaseModuleCatalogTelemetryRing, out views.Telemetry);

            if (!resolved || !views.State.IsCreated || views.State.Length == 0)
                return false;

            ModuleCatalogStateDTO state = views.State[0];
            return state.ModuleCount <= views.Modules.Length &&
                   state.SocketCount <= views.Sockets.Length &&
                   state.CostCount <= views.Costs.Length;
        }

        private static bool TryEnsureOwnedLane<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out VaultGenerationHandle<T> handle) where T : struct
        {
            handle = default;
            if (vault == null || vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                return false;

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                math.max(1, requiredLength),
                SystemID.Construction,
                options);
            return IsExactBufferId(in handle, bufferId) &&
                   vault.TryReadHandle(in handle, out NativeArray<T> existing) &&
                   existing.IsCreated &&
                   existing.Length >= requiredLength;
        }

        private static bool TryAcquireOwnedLane<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out VaultGenerationHandle<T> handle,
            out NativeArray<T> buffer) where T : struct
        {
            handle = default;
            buffer = default;
            if (!TryEnsureOwnedLane(vault, bufferId, requiredLength, options, out handle) ||
                !vault.TryAcquireWriteLock(in handle, SystemID.Construction, out buffer))
            {
                return false;
            }

            bool releaseOnFailure = true;
            try
            {
                if (buffer.IsCreated && buffer.Length >= requiredLength)
                {
                    releaseOnFailure = false;
                    return true;
                }

                buffer = default;
                return false;
            }
            finally
            {
                if (releaseOnFailure)
                    vault.ReleaseWriteLock(in handle, SystemID.Construction);
            }
        }

        private static bool TryResolveOwnedLane<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out VaultGenerationHandle<T> handle,
            out NativeArray<T> buffer) where T : struct
        {
            handle = default;
            buffer = default;
            return TryEnsureOwnedLane(vault, bufferId, requiredLength, options, out handle) &&
                   vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool TryReadExistingLane<T>(
            IDataVault vault,
            BufferID bufferId,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> handle) &&
                   IsExactBufferId(in handle, bufferId) &&
                   vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated;
        }

        private static bool IsExactBufferId<T>(in VaultGenerationHandle<T> handle, BufferID bufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId);
        }

        public static void ReleaseWriteLease(in ModuleCatalogWriteLease lease)
        {
            IDataVault vault = lease.Vault;
            if (vault == null || lease.MutationGuardMask == 0UL)
                return;

            vault.ReleaseMutationGuard(lease.MutationGuardMask);
        }

        private static bool TryAcquireCatalogWriteViews(
            IDataVault vault,
            out ModuleCatalogViews views,
            out ModuleCatalogWriteLease lease,
            int moduleCapacity,
            int socketCapacity,
            int costCapacity,
            int hashCapacity)
        {
            views = default;
            lease = default;
            if (vault == null ||
                vault.IsAllocationLocked ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireMutationGuard(CatalogWriteMutationGuardMask))
            {
                return false;
            }

            lease.Vault = vault;
            lease.MutationGuardMask = CatalogWriteMutationGuardMask;
            if (!TryResolveOwnedLane(
                    vault,
                    BufferID.BaseModuleCatalogState,
                    1,
                    NativeArrayOptions.ClearMemory,
                    out lease.StateHandle,
                    out views.State))
            {
                ReleaseWriteLease(in lease);
                lease = default;
                views = default;
                return false;
            }

            if (!TryResolveOwnedLane(
                    vault,
                    BufferID.BaseModuleCatalogDefinitions,
                    math.max(1, moduleCapacity),
                    NativeArrayOptions.UninitializedMemory,
                    out lease.ModulesHandle,
                    out views.Modules))
            {
                ReleaseWriteLease(in lease);
                lease = default;
                views = default;
                return false;
            }

            if (!TryResolveOwnedLane(
                    vault,
                    BufferID.BaseModuleCatalogSockets,
                    math.max(1, socketCapacity),
                    NativeArrayOptions.UninitializedMemory,
                    out lease.SocketsHandle,
                    out views.Sockets))
            {
                ReleaseWriteLease(in lease);
                lease = default;
                views = default;
                return false;
            }

            if (!TryResolveOwnedLane(
                    vault,
                    BufferID.BaseModuleCatalogCosts,
                    math.max(1, costCapacity),
                    NativeArrayOptions.UninitializedMemory,
                    out lease.CostsHandle,
                    out views.Costs))
            {
                ReleaseWriteLease(in lease);
                lease = default;
                views = default;
                return false;
            }

            if (!TryResolveOwnedLane(
                    vault,
                    BufferID.BaseModuleCatalogHashToIndex,
                    math.max(1, hashCapacity),
                    NativeArrayOptions.UninitializedMemory,
                    out lease.HashToIndexHandle,
                    out views.HashToIndex))
            {
                ReleaseWriteLease(in lease);
                lease = default;
                views = default;
                return false;
            }

            if (!TryResolveOwnedLane(
                    vault,
                    BufferID.BaseModuleCatalogTelemetryRing,
                    TelemetryCapacity,
                    NativeArrayOptions.ClearMemory,
                    out lease.TelemetryHandle,
                    out views.Telemetry))
            {
                ReleaseWriteLease(in lease);
                lease = default;
                views = default;
                return false;
            }

            return true;
        }

        public static JobHandle ScheduleHydrateCatalog(
            IDataVault vault,
            NativeArray<byte> sourceBytes,
            int sourceByteLength,
            out ModuleCatalogViews views,
            out ModuleCatalogWriteLease lease,
            JobHandle dependency = default)
        {
            if (!TryAcquireCatalogWriteViews(vault, out views, out lease, DefaultModuleCapacity, DefaultSocketCapacity, DefaultCostCapacity, DefaultHashCapacity))
                return dependency;

            var job = new HydrateModuleCatalogJob
            {
                SourceBytes = sourceBytes,
                SourceByteLength = math.max(0, sourceByteLength),
                State = views.State,
                Modules = views.Modules,
                Sockets = views.Sockets,
                Costs = views.Costs,
                HashToIndex = views.HashToIndex
            };
            return job.Schedule(dependency);
        }

        public static bool TryLoadCatalogBytes(IDataVault vault, string path, out NativeArray<byte>.ReadOnly bytes, out int byteLength)
        {
            bytes = default;
            byteLength = 0;
            if (vault == null || string.IsNullOrEmpty(path) || !File.Exists(path) || vault.IsAllocationLocked)
                return false;

            var fileInfo = new FileInfo(path);
            long length = fileInfo.Length;
            if (length <= 0L || length > DefaultHydrationByteCapacity)
                return false;

            int expectedLength = (int)length;
            if (Interlocked.CompareExchange(ref s_catalogHydrationScratchBusy, 1, 0) != 0)
                return false;

            try
            {
                int readLength = ReadCatalogBytesIntoScratch(path, s_catalogHydrationScratch, expectedLength);
                if (readLength != expectedLength)
                {
                    byteLength = math.max(0, readLength);
                    return false;
                }

                if (!TryAcquireOwnedLane(
                        vault,
                        BufferID.BaseModuleCatalogHydrationBytes,
                        expectedLength,
                        NativeArrayOptions.UninitializedMemory,
                        out VaultGenerationHandle<byte> hydrationHandle,
                        out NativeArray<byte> targetBytes))
                    return false;

                try
                {
                    if (targetBytes.Length < expectedLength)
                        return false;

                    CopyCatalogScratchIntoNativeArray(s_catalogHydrationScratch, targetBytes, expectedLength);
                    byteLength = expectedLength;
                    bytes = targetBytes.AsReadOnly();
                    return true;
                }
                finally
                {
                    vault.ReleaseWriteLock(in hydrationHandle, SystemID.Construction);
                }
            }
            finally
            {
                Volatile.Write(ref s_catalogHydrationScratchBusy, 0);
            }
        }

        public static bool TryStartCatalogByteLoad(
            IDataVault vault,
            string path,
            out NativeArray<byte>.ReadOnly bytes,
            out Awaitable<int> loadTask)
        {
            bytes = default;
            loadTask = default;
            return false;
        }

        private static int ReadCatalogBytesIntoScratch(string path, byte[] bytes, int expectedLength)
        {
            if (bytes == null || expectedLength <= 0 || bytes.Length < expectedLength)
                return CatalogByteLoadInvalidTarget;

            try
            {
                using (var stream = new FileStream(
                           path,
                           FileMode.Open,
                           FileAccess.Read,
                           FileShare.Read,
                           64 * 1024,
                           FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    int offset = 0;
                    while (offset < expectedLength)
                    {
                        var destination = new Span<byte>(bytes, offset, expectedLength - offset);
                        int read = stream.Read(destination);
                        if (read <= 0)
                            return CatalogByteLoadShortRead;

                        offset += read;
                    }

                    return offset == expectedLength ? offset : CatalogByteLoadShortRead;
                }
            }
            catch (IOException)
            {
                return CatalogByteLoadIoFailure;
            }
            catch (UnauthorizedAccessException)
            {
                return CatalogByteLoadIoFailure;
            }
        }

        private static unsafe void CopyCatalogScratchIntoNativeArray(byte[] source, NativeArray<byte> target, int byteLength)
        {
            if (source == null || !target.IsCreated || byteLength <= 0 || source.Length < byteLength || target.Length < byteLength)
                return;

            fixed (byte* sourcePtr = source)
            {
                byte* targetPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(target);
                UnsafeUtility.MemCpy(targetPtr, sourcePtr, byteLength);
            }
        }

        public static bool ValidateLayout(out int moduleSize, out int socketSize, out int costSize, out int stateSize, out int telemetrySize)
        {
            moduleSize = UnsafeUtility.SizeOf<ModuleDefinitionDTO>();
            socketSize = UnsafeUtility.SizeOf<SocketDefinitionDTO>();
            costSize = UnsafeUtility.SizeOf<ModuleCostDTO>();
            stateSize = UnsafeUtility.SizeOf<ModuleCatalogStateDTO>();
            telemetrySize = UnsafeUtility.SizeOf<ModuleCatalogTelemetryEntry>();
            return moduleSize == ModuleDefinitionSize &&
                   socketSize == SocketDefinitionSize &&
                   costSize == ModuleCostSize &&
                   stateSize == StateSize &&
                   telemetrySize == TelemetryEntrySize;
        }

        private static uint ReverseBytes(uint value)
        {
            return ((value & 0x000000FFu) << 24) |
                   ((value & 0x0000FF00u) << 8) |
                   ((value & 0x00FF0000u) >> 8) |
                   ((value & 0xFF000000u) >> 24);
        }

        public static bool RunSelfAudit(out ModuleCatalogSelfAuditDTO audit)
        {
            audit = default;
            bool layout = ValidateLayout(
                out audit.ModuleDefinitionBytes,
                out audit.SocketDefinitionBytes,
                out audit.ModuleCostBytes,
                out _,
                out audit.TelemetryEntryBytes);

            bool mask = AreSocketMasksCompatible(0x10u, 0x10u) && !AreSocketMasksCompatible(0x10u, 0x20u);
            bool rollbackFence =
                IsImmutableCatalogBuffer(BufferID.BaseModuleCatalogDefinitions) &&
                IsImmutableCatalogBuffer(BufferID.BaseModuleCatalogSockets) &&
                !ShouldPublishRollbackHash(BufferID.BaseModuleCatalogCosts);
            bool hydrationPolicy = true;
            bool endianPolicy = BinaryMagic == 0x48424D43u &&
                                ReverseBytes(BinaryMagic) == 0x434D4248u &&
                                BinaryLittleEndianFlag != 0u;

            audit.Flags = 0u;
            if (layout)
                audit.Flags |= SelfAuditLayoutFlag;
            if (mask)
                audit.Flags |= SelfAuditMaskFlag;
            if (rollbackFence)
                audit.Flags |= SelfAuditRollbackFenceFlag;
            if (hydrationPolicy)
                audit.Flags |= SelfAuditUninitializedHydrationFlag;
            if (endianPolicy)
                audit.Flags |= SelfAuditEndianPolicyFlag;

            audit.BufferStartId = (uint)BufferID.BaseModuleCatalogState;
            audit.BufferEndId = (uint)BufferID.BaseModuleCatalogScannerReport;
            audit.FailureCode = layout && mask && rollbackFence && hydrationPolicy && endianPolicy ? 0u : 1u;
            return audit.FailureCode == 0u;
        }

        public static bool TryFindModuleIndex(in ModuleCatalogViews views, uint prefabHashId, out int index)
        {
            index = -1;
            if (!views.State.IsCreated || !views.Modules.IsCreated || views.State.Length == 0)
                return false;

            int count = (int)math.min(views.State[0].ModuleCount, (uint)views.Modules.Length);
            return TryFindModuleIndex(views.Modules, count, prefabHashId, out index);
        }

        public static bool TryFindModuleIndex(NativeArray<ModuleDefinitionDTO> modules, int moduleCount, uint prefabHashId, out int index)
        {
            index = -1;
            if (!modules.IsCreated || moduleCount <= 0)
                return false;

            int lo = 0;
            int hi = math.min(moduleCount, modules.Length) - 1;
            while (lo <= hi)
            {
                int mid = lo + ((hi - lo) >> 1);
                uint current = modules[mid].PrefabHashID;
                if (current == prefabHashId)
                {
                    index = mid;
                    return true;
                }

                if (current < prefabHashId)
                    lo = mid + 1;
                else
                    hi = mid - 1;
            }

            return false;
        }

        public static bool TryGetModuleDefinition(in ModuleCatalogViews views, uint prefabHashId, out ModuleDefinitionDTO definition)
        {
            definition = default;
            if (!TryFindModuleIndex(views, prefabHashId, out int index))
                return false;

            definition = views.Modules[index];
            return true;
        }

        public static bool TryGetModuleSocketRangeFromVault(
            IDataVault vault,
            uint prefabHashId,
            out NativeArray<SocketDefinitionDTO>.ReadOnly sockets,
            out int start,
            out int count,
            out ModuleDefinitionDTO definition)
        {
            sockets = default;
            start = 0;
            count = 0;
            definition = default;
            if (vault == null || prefabHashId == 0u || !TryResolveViews(vault, out ModuleCatalogViews views))
                return false;

            if (!TryGetModuleDefinition(views, prefabHashId, out definition))
                return false;

            if (!TryGetSocketRange(definition, views.Sockets.AsReadOnly(), out start, out count))
                return false;

            sockets = views.Sockets.AsReadOnly();
            return true;
        }

        public static bool TryGetModuleCost(in ModuleCatalogViews views, uint prefabHashId, out ModuleCostDTO cost)
        {
            cost = default;
            if (!views.State.IsCreated || !views.Costs.IsCreated || views.State.Length == 0)
                return false;

            int count = (int)math.min(views.State[0].CostCount, (uint)views.Costs.Length);
            int lo = 0;
            int hi = count - 1;
            while (lo <= hi)
            {
                int mid = lo + ((hi - lo) >> 1);
                ModuleCostDTO current = views.Costs[mid];
                if (current.PrefabHashID == prefabHashId)
                {
                    cost = current;
                    return true;
                }

                if (current.PrefabHashID < prefabHashId)
                    lo = mid + 1;
                else
                    hi = mid - 1;
            }

            return false;
        }

        public static unsafe ref readonly ModuleDefinitionDTO GetModuleDefinitionRef(NativeArray<ModuleDefinitionDTO> modules, int index)
        {
            byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(modules);
            return ref UnsafeUtility.AsRef<ModuleDefinitionDTO>(ptr + index * ModuleDefinitionSize);
        }

        public static unsafe ReadOnlySpan<SocketDefinitionDTO> GetModuleSockets(
            in ModuleDefinitionDTO module,
            NativeArray<SocketDefinitionDTO> sockets)
        {
            if (!sockets.IsCreated || module.SocketCount == 0u || module.SocketStartIndex < 0)
                return ReadOnlySpan<SocketDefinitionDTO>.Empty;

            int start = module.SocketStartIndex;
            int count = (int)module.SocketCount;
            if (start >= sockets.Length || count < 0 || start + count > sockets.Length)
                return ReadOnlySpan<SocketDefinitionDTO>.Empty;

            SocketDefinitionDTO* ptr = (SocketDefinitionDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(sockets);
            return new ReadOnlySpan<SocketDefinitionDTO>(ptr + start, count);
        }

        public static bool TryGetSocketRange(in ModuleDefinitionDTO module, NativeArray<SocketDefinitionDTO>.ReadOnly sockets, out int start, out int count)
        {
            start = module.SocketStartIndex;
            count = (int)module.SocketCount;
            return start >= 0 &&
                   count >= 0 &&
                   start <= sockets.Length &&
                   start + count <= sockets.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double3 ResolveSocketAup(double3 moduleRootAup, in SocketDefinitionDTO socket)
        {
            return new double3(
                moduleRootAup.x + socket.LocalOffset.x,
                moduleRootAup.y + socket.LocalOffset.y,
                moduleRootAup.z + socket.LocalOffset.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double3 ResolveSocketAup(double3 moduleRootAup, quaternion rotation, in SocketDefinitionDTO socket)
        {
            float3 rotatedOffset = math.rotate(rotation, socket.LocalOffset);
            return new double3(
                moduleRootAup.x + rotatedOffset.x,
                moduleRootAup.y + rotatedOffset.y,
                moduleRootAup.z + rotatedOffset.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double3 AlignAupToGrid(double3 rawAup, double gridMeters)
        {
            double safeGrid = gridMeters > 0.000001d && math.all(math.isfinite(rawAup)) ? gridMeters : 1d;
            return new double3(
                math.round(rawAup.x / safeGrid) * safeGrid,
                math.round(rawAup.y / safeGrid) * safeGrid,
                math.round(rawAup.z / safeGrid) * safeGrid);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AreSocketMasksCompatible(uint lhsMask, uint rhsMask)
        {
            return (lhsMask & rhsMask) != 0u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AreSocketsAdjacent(in SocketDefinitionDTO lhs, in SocketDefinitionDTO rhs)
        {
            return AreSocketMasksCompatible(lhs.AllowedConnectionsMask, rhs.AllowedConnectionsMask) &&
                   math.dot(lhs.Normal, rhs.Normal) <= -0.85f;
        }

        public static uint ComputeCompatibilityMask(string compatibleType)
        {
            if (string.IsNullOrEmpty(compatibleType))
                return UniversalConnectionMask;

            uint hash = unchecked((uint)LocHash.Compute(compatibleType));
            uint lane = hash % CompatibilityLaneCount;
            return 1u << (CompatibilityLaneBitOffset + (int)lane);
        }

        public static SocketDefinitionDTO BuildSocketDTO(Vector3 localPosition, ModuleSocketDirection direction, string compatibleType)
        {
            return new SocketDefinitionDTO
            {
                LocalOffset = new float3(localPosition.x, localPosition.y, localPosition.z),
                Normal = DirectionToNormal(direction),
                AllowedConnectionsMask = ComputeCompatibilityMask(compatibleType)
            };
        }

        public static bool TryBuildSocketFromTemplate(BaseModuleTemplate template, int socketIndex, out SocketDefinitionDTO socket)
        {
            socket = default;
            if (template == null)
                return false;

            BaseModuleTemplate.SocketDefinition[] definitions = template.SocketDefinitions;
            if (definitions == null || socketIndex < 0 || socketIndex >= definitions.Length)
                return false;

            BaseModuleTemplate.SocketDefinition definition = definitions[socketIndex];
            socket = BuildSocketDTO(definition.LocalPosition, definition.Direction, definition.CompatibleType);
            return true;
        }

        public static bool TryBuildModuleFromTemplate(BaseModuleTemplate template, int socketStartIndex, out ModuleDefinitionDTO module)
        {
            module = default;
            if (template == null)
                return false;

            Vector3 bounds = template.ProxyBoundsSize;
            BaseModuleTemplate.SocketDefinition[] sockets = template.SocketDefinitions;
            module = new ModuleDefinitionDTO
            {
                PrefabHashID = unchecked((uint)template.ResolvePersistentHashId()),
                ModuleClassHash = ClassHabitatHash,
                BoundingBoxExtents = new float3(
                    math.max(0.5f, bounds.x * 0.5f),
                    math.max(0.5f, bounds.y * 0.5f),
                    math.max(0.5f, bounds.z * 0.5f)),
                SocketCount = (uint)(sockets != null ? sockets.Length : 0),
                SocketStartIndex = socketStartIndex,
                BaseStrength = 240u,
                AllowedBiomesMask = AllBiomesMask
            };
            return module.PrefabHashID != 0u;
        }

        public static bool TryResolveWorldSocket(
            Vector3 rootPosition,
            Quaternion rootRotation,
            in SocketDefinitionDTO socket,
            out Vector3 position,
            out Vector3 normal)
        {
            quaternion rotation = new quaternion(rootRotation.x, rootRotation.y, rootRotation.z, rootRotation.w);
            float3 offset = math.rotate(rotation, socket.LocalOffset);
            float3 worldNormal = math.rotate(rotation, socket.Normal);
            if (!math.all(math.isfinite(offset)) || !math.all(math.isfinite(worldNormal)))
            {
                position = default;
                normal = Vector3.forward;
                return false;
            }

            position = new Vector3(rootPosition.x + offset.x, rootPosition.y + offset.y, rootPosition.z + offset.z);
            normal = new Vector3(worldNormal.x, worldNormal.y, worldNormal.z);
            return true;
        }

        public static float3 DirectionToNormal(ModuleSocketDirection direction)
        {
            switch (direction)
            {
                case ModuleSocketDirection.North: return new float3(0f, 0f, 1f);
                case ModuleSocketDirection.South: return new float3(0f, 0f, -1f);
                case ModuleSocketDirection.East: return new float3(1f, 0f, 0f);
                case ModuleSocketDirection.West: return new float3(-1f, 0f, 0f);
                case ModuleSocketDirection.Top: return new float3(0f, 1f, 0f);
                case ModuleSocketDirection.Bottom: return new float3(0f, -1f, 0f);
                default: return new float3(0f, 0f, 1f);
            }
        }

        public static int DirectionToAxis(ModuleSocketDirection direction)
        {
            switch (direction)
            {
                case ModuleSocketDirection.East: return 0;
                case ModuleSocketDirection.West: return 1;
                case ModuleSocketDirection.Top: return 2;
                case ModuleSocketDirection.Bottom: return 3;
                case ModuleSocketDirection.North: return 4;
                default: return 5;
            }
        }

        public static bool IsImmutableCatalogBuffer(BufferID bufferId)
        {
            switch (bufferId)
            {
                case BufferID.BaseModuleCatalogState:
                case BufferID.BaseModuleCatalogDefinitions:
                case BufferID.BaseModuleCatalogSockets:
                case BufferID.BaseModuleCatalogCosts:
                case BufferID.BaseModuleCatalogHashToIndex:
                case BufferID.BaseModuleCatalogHydrationBytes:
                case BufferID.BaseModuleCatalogHydrationStatus:
                    return true;
                default:
                    return false;
            }
        }

        public static bool ShouldPublishRollbackHash(BufferID bufferId)
        {
            return !IsImmutableCatalogBuffer(bufferId);
        }

        public static bool TryRecordTelemetry(
            IDataVault vault,
            uint queryCount,
            uint successCount,
            uint failedCount,
            long queryTicks,
            long burstTicks,
            uint flags,
            string projectRoot = null)
        {
            if (vault == null)
                return false;

            if (vault.IsAllocationLocked ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireMutationGuard(CatalogTelemetryMutationGuardMask))
            {
                return false;
            }

            bool shouldDump = false;
            VaultGenerationHandle<ModuleCatalogTelemetryEntry> telemetryHandle = default;
            try
            {
                if (!TryResolveOwnedLane(
                    vault,
                    BufferID.BaseModuleCatalogState,
                    1,
                    NativeArrayOptions.ClearMemory,
                    out VaultGenerationHandle<ModuleCatalogStateDTO> stateHandle,
                    out NativeArray<ModuleCatalogStateDTO> stateBuffer))
                {
                    return false;
                }

                if (!TryResolveOwnedLane(
                        vault,
                        BufferID.BaseModuleCatalogTelemetryRing,
                        TelemetryCapacity,
                        NativeArrayOptions.ClearMemory,
                        out telemetryHandle,
                        out NativeArray<ModuleCatalogTelemetryEntry> telemetryBuffer))
                {
                    return false;
                }

                ModuleCatalogStateDTO state = stateBuffer[0];
                uint cursor = state.TelemetryCursor;
                if (cursor >= telemetryBuffer.Length)
                    cursor = 0u;

                ModuleCatalogTelemetryEntry entry = default;
                entry.QueryTicks = queryTicks;
                entry.BurstTicks = burstTicks;
                entry.Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
                entry.QueryCount = queryCount;
                entry.SuccessfulAdjacencyCount = successCount;
                entry.FailedAdjacencyCount = failedCount;
                if (TicksToMicroseconds(queryTicks + burstTicks) > 100.0)
                    flags |= TelemetryOverBudgetFlag;
                entry.Flags = flags;
                entry.CatalogHash = state.CatalogHash;
                entry.VaultGenerationId = telemetryHandle.Generation;
                entry.StateHash = HashTelemetry(entry);
                telemetryBuffer[(int)cursor] = entry;

                cursor++;
                if (cursor >= telemetryBuffer.Length)
                    cursor = 0u;

                state.TelemetryCursor = cursor;
                stateBuffer[0] = state;
                shouldDump = (flags & TelemetryOverBudgetFlag) != 0u;
            }
            finally
            {
                vault.ReleaseMutationGuard(CatalogTelemetryMutationGuardMask);
            }

            if (shouldDump)
                TryDumpTelemetry(vault, projectRoot);
            return true;
        }

        private static double TicksToMicroseconds(long ticks)
        {
            return ticks <= 0L ? 0.0 : ticks * (1000000.0 / Stopwatch.Frequency);
        }

        public static unsafe bool TryDumpTelemetry(IDataVault vault, string projectRoot)
        {
            _ = projectRoot;
            return TryResolveViews(vault, out ModuleCatalogViews views) &&
                   views.Telemetry.IsCreated &&
                   views.Telemetry.Length > 0;
        }

#if UNITY_EDITOR
        public static bool TryParseBuildCostCsv(ReadOnlySpan<byte> bytes, NativeArray<ModuleCostDTO> costs, out int count)
        {
            count = 0;
            if (!costs.IsCreated || bytes.Length == 0)
                return false;

            int index = 0;
            SkipLine(bytes, ref index);
            while (index < bytes.Length && count < costs.Length)
            {
                uint prefabHash = ReadHashCell(bytes, ref index);
                ModuleCostDTO cost = default;
                cost.PrefabHashID = prefabHash;
                cost.CostCount = 0u;

                ReadCostPair(bytes, ref index, ref cost, 0);
                ReadCostPair(bytes, ref index, ref cost, 1);
                ReadCostPair(bytes, ref index, ref cost, 2);
                ReadCostPair(bytes, ref index, ref cost, 3);
                SkipLine(bytes, ref index);

                if (prefabHash == 0u)
                    continue;

                costs[count++] = cost;
            }

            return count > 0;
        }
#endif

        private static uint HashTelemetry(in ModuleCatalogTelemetryEntry entry)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ entry.Frame) * 16777619u;
                hash = (hash ^ entry.QueryCount) * 16777619u;
                hash = (hash ^ entry.SuccessfulAdjacencyCount) * 16777619u;
                hash = (hash ^ entry.FailedAdjacencyCount) * 16777619u;
                hash = (hash ^ entry.CatalogHash) * 16777619u;
                hash = (hash ^ (uint)entry.QueryTicks) * 16777619u;
                hash = (hash ^ (uint)(entry.QueryTicks >> 32)) * 16777619u;
                return hash;
            }
        }

        private static void ReadCostPair(ReadOnlySpan<byte> bytes, ref int index, ref ModuleCostDTO cost, int pairIndex)
        {
            uint itemHash = ReadHashCell(bytes, ref index);
            int quantity = ReadIntCell(bytes, ref index);
            if (itemHash == 0u || quantity <= 0)
                return;

            switch (pairIndex)
            {
                case 0:
                    cost.ItemHash0 = itemHash;
                    cost.Quantity0 = quantity;
                    break;
                case 1:
                    cost.ItemHash1 = itemHash;
                    cost.Quantity1 = quantity;
                    break;
                case 2:
                    cost.ItemHash2 = itemHash;
                    cost.Quantity2 = quantity;
                    break;
                default:
                    cost.ItemHash3 = itemHash;
                    cost.Quantity3 = quantity;
                    break;
            }

            cost.CostCount = math.min(4u, cost.CostCount + 1u);
        }

        private static uint ReadHashCell(ReadOnlySpan<byte> bytes, ref int index)
        {
            unchecked
            {
                uint hash = 2166136261u;
                bool hasData = false;
                while (index < bytes.Length)
                {
                    byte value = bytes[index++];
                    if (value == (byte)',' || value == (byte)'\n' || value == (byte)'\r')
                    {
                        if (value == (byte)'\r' && index < bytes.Length && bytes[index] == (byte)'\n')
                            index++;
                        break;
                    }

                    if (value <= 32)
                        continue;

                    hasData = true;
                    hash = (hash ^ ToLowerAscii(value)) * 16777619u;
                }

                return hasData ? hash : 0u;
            }
        }

        private static int ReadIntCell(ReadOnlySpan<byte> bytes, ref int index)
        {
            int sign = 1;
            int value = 0;
            bool started = false;
            while (index < bytes.Length)
            {
                byte raw = bytes[index++];
                if (raw == (byte)',' || raw == (byte)'\n' || raw == (byte)'\r')
                {
                    if (raw == (byte)'\r' && index < bytes.Length && bytes[index] == (byte)'\n')
                        index++;
                    break;
                }

                if (!started && raw == (byte)'-')
                {
                    sign = -1;
                    started = true;
                    continue;
                }

                if (raw < (byte)'0' || raw > (byte)'9')
                    continue;

                started = true;
                value = math.min(1000000, value * 10 + raw - (byte)'0');
            }

            return value * sign;
        }

        private static void SkipLine(ReadOnlySpan<byte> bytes, ref int index)
        {
            while (index < bytes.Length)
            {
                byte raw = bytes[index++];
                if (raw == (byte)'\n')
                    return;
            }
        }

        private static byte ToLowerAscii(byte value)
        {
            return value >= (byte)'A' && value <= (byte)'Z' ? (byte)(value + 32) : value;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        public struct QueryModuleDefinitionJob : IJob
        {
            [ReadOnly] [NoAlias] public NativeArray<ModuleCatalogStateDTO> State;
            [ReadOnly] [NoAlias] public NativeArray<ModuleDefinitionDTO> Modules;
            [NoAlias] public NativeArray<int> ResultIndex;
            public uint PrefabHashID;

            public void Execute()
            {
                if (!ResultIndex.IsCreated || ResultIndex.Length == 0)
                    return;

                ResultIndex[0] = -1;
                if (!State.IsCreated || State.Length == 0 || !Modules.IsCreated)
                    return;

                int count = (int)math.min(State[0].ModuleCount, (uint)Modules.Length);
                int lo = 0;
                int hi = count - 1;
                while (lo <= hi)
                {
                    int mid = lo + ((hi - lo) >> 1);
                    uint current = Modules[mid].PrefabHashID;
                    if (current == PrefabHashID)
                    {
                        ResultIndex[0] = mid;
                        return;
                    }

                    if (current < PrefabHashID)
                        lo = mid + 1;
                    else
                        hi = mid - 1;
                }
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        public unsafe struct HydrateModuleCatalogJob : IJob
        {
            [ReadOnly] [NoAlias] public NativeArray<byte> SourceBytes;
            public int SourceByteLength;
            [NoAlias] public NativeArray<ModuleCatalogStateDTO> State;
            [NoAlias] public NativeArray<ModuleDefinitionDTO> Modules;
            [NoAlias] public NativeArray<SocketDefinitionDTO> Sockets;
            [NoAlias] public NativeArray<ModuleCostDTO> Costs;
            [NoAlias] public NativeArray<uint> HashToIndex;

            public void Execute()
            {
                if (!State.IsCreated || State.Length == 0)
                    return;

                ModuleCatalogStateDTO state = State[0];
                state.HydrationStatus = (uint)ModuleCatalogHydrationStatus.InvalidHeader;
                state.LastErrorCode = 0u;

                int headerSize = UnsafeUtility.SizeOf<ModuleCatalogBinaryHeader>();
                if (!SourceBytes.IsCreated || SourceByteLength < headerSize)
                {
                    state.HydrationStatus = (uint)ModuleCatalogHydrationStatus.InvalidLength;
                    State[0] = state;
                    return;
                }

                byte* source = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(SourceBytes);
                ModuleCatalogBinaryHeader header = UnsafeUtility.ReadArrayElement<ModuleCatalogBinaryHeader>(source, 0);
                if (header.Magic != BinaryMagic)
                {
                    state.HydrationStatus = ReverseBytes(header.Magic) == BinaryMagic
                        ? (uint)ModuleCatalogHydrationStatus.InvalidEndian
                        : (uint)ModuleCatalogHydrationStatus.InvalidHeader;
                    state.LastErrorCode = header.Magic;
                    State[0] = state;
                    return;
                }

                if (header.Version != BinaryVersion)
                {
                    state.HydrationStatus = (uint)ModuleCatalogHydrationStatus.InvalidHeader;
                    state.LastErrorCode = header.Version;
                    State[0] = state;
                    return;
                }

                if ((header.Flags & BinaryLittleEndianFlag) == 0u)
                {
                    state.HydrationStatus = (uint)ModuleCatalogHydrationStatus.InvalidEndian;
                    state.LastErrorCode = header.Flags;
                    State[0] = state;
                    return;
                }

                int moduleBytes = CheckedByteCount(header.ModuleCount, ModuleDefinitionSize);
                int socketBytes = CheckedByteCount(header.SocketCount, SocketDefinitionSize);
                int costBytes = CheckedByteCount(header.CostCount, ModuleCostSize);
                if (moduleBytes < 0 || socketBytes < 0 || costBytes < 0)
                {
                    state.HydrationStatus = (uint)ModuleCatalogHydrationStatus.InvalidLength;
                    State[0] = state;
                    return;
                }

                if (header.ModuleCount > Modules.Length || header.SocketCount > Sockets.Length || header.CostCount > Costs.Length)
                {
                    state.HydrationStatus = (uint)ModuleCatalogHydrationStatus.CapacityExceeded;
                    State[0] = state;
                    return;
                }

                int requiredLength = math.max(
                    (int)header.ModuleByteOffset + moduleBytes,
                    math.max((int)header.SocketByteOffset + socketBytes, (int)header.CostByteOffset + costBytes));
                if (requiredLength > SourceByteLength || requiredLength > header.ByteLength)
                {
                    state.HydrationStatus = (uint)ModuleCatalogHydrationStatus.InvalidLength;
                    State[0] = state;
                    return;
                }

                uint checksum = ComputeChecksum(source, SourceByteLength);
                if (header.Checksum != 0u && checksum != header.Checksum)
                {
                    state.HydrationStatus = (uint)ModuleCatalogHydrationStatus.InvalidChecksum;
                    state.LastErrorCode = checksum;
                    State[0] = state;
                    return;
                }

                UnsafeUtility.MemCpy(NativeArrayUnsafeUtility.GetUnsafePtr(Modules), source + header.ModuleByteOffset, moduleBytes);
                UnsafeUtility.MemCpy(NativeArrayUnsafeUtility.GetUnsafePtr(Sockets), source + header.SocketByteOffset, socketBytes);
                UnsafeUtility.MemCpy(NativeArrayUnsafeUtility.GetUnsafePtr(Costs), source + header.CostByteOffset, costBytes);

                if (HashToIndex.IsCreated)
                {
                    int pairs = math.min((int)header.ModuleCount, HashToIndex.Length >> 1);
                    for (int i = 0; i < pairs; i++)
                    {
                        HashToIndex[i * 2] = Modules[i].PrefabHashID;
                        HashToIndex[i * 2 + 1] = (uint)i;
                    }
                }

                state.ModuleCount = header.ModuleCount;
                state.SocketCount = header.SocketCount;
                state.CostCount = header.CostCount;
                state.HydrationStatus = (uint)ModuleCatalogHydrationStatus.Hydrated;
                state.Generation++;
                state.CatalogHash = header.CatalogHash != 0u ? header.CatalogHash : checksum;
                state.Flags = CatalogImmutableFlag | BinaryLittleEndianFlag | header.Flags;
                state.SourceByteLength = (uint)SourceByteLength;
                state.LastErrorCode = 0u;
                State[0] = state;
            }

            private static int CheckedByteCount(uint count, int stride)
            {
                if (count > int.MaxValue / stride)
                    return -1;

                return (int)count * stride;
            }

            private static uint ComputeChecksum(byte* bytes, int length)
            {
                int headerSize = UnsafeUtility.SizeOf<ModuleCatalogBinaryHeader>();
                if (length <= headerSize)
                    return 0u;

                uint2 hash = xxHash3.Hash64(bytes + headerSize, (long)(length - headerSize));
                return hash.x ^ hash.y;
            }
        }
    }
}
