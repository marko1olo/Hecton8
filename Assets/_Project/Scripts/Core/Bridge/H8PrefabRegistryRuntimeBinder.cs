using System.Threading;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Serialization;

namespace Hecton8.Core.Bridge
{
    public static unsafe class H8PrefabRegistryRuntimeBinder
    {
        private const int RuntimePrefabIdScratchCapacity = 1024;
        private const ulong PrefabRegistryBindMutationGuardMask =
            (1UL << (unchecked((int)(uint)(int)BufferID.BridgePrefabMapping) & 31)) |
            (1UL << (unchecked((int)(uint)(int)BufferID.BridgePrefabLoreLinks) & 31));
        private static int s_x001H8PrefabRegistryRuntimeBinderSignalPushDropCount;
        public static bool Bind(H8PrefabRegistry registry, IDataVault vault, PrefabRegistry runtimeRegistry)
        {
            return Bind(
                registry,
                vault,
                runtimeRegistry,
                allowAuthoringRepair: true,
                allowBufferGrowth: true);
        }

        internal static bool BindExistingBuffers(H8PrefabRegistry registry, IDataVault vault, PrefabRegistry runtimeRegistry)
        {
            return Bind(
                registry,
                vault,
                runtimeRegistry,
                allowAuthoringRepair: false,
                allowBufferGrowth: false);
        }

        private static bool Bind(
            H8PrefabRegistry registry,
            IDataVault vault,
            PrefabRegistry runtimeRegistry,
            bool allowAuthoringRepair,
            bool allowBufferGrowth)
        {
            if (registry == null || vault == null)
                return false;

            int runtimeBindableCount = registry.RefreshRuntimeBindingStateForSync(allowAuthoringRepair);
            if (runtimeBindableCount < 0)
                return false;

            int rawCount = registry.EntryCount;
            if (registry.ValidationDuplicateHashCount > 0)
                return false;

            if (runtimeBindableCount <= 0)
            {
                if (!ClearExistingBuffers(vault))
                    return false;

                VRAMBudgetTracker.Unregister(registry.RegistryHash);
                PublishRegistryUpdateSignal(registry.RegistryHash, BufferID.BridgePrefabMapping, 0);
                PublishRegistryUpdateSignal(registry.RegistryHash, BufferID.BridgePrefabLoreLinks, 0);
                return true;
            }

            if (runtimeBindableCount > RuntimePrefabIdScratchCapacity)
                return false;

            if (!TryValidateRuntimeBindableCount(registry, rawCount, runtimeBindableCount))
                return false;

            bool publishRuntimeSignals = Application.isPlaying;
            if (!publishRuntimeSignals)
                runtimeRegistry = null;

            uint frame = publishRuntimeSignals ? Hecton8.Core.SystemDispatcher.CurrentFrameId : 0u;

            uint* runtimePrefabIds = stackalloc uint[RuntimePrefabIdScratchCapacity];
            if (!TryWritePrefabBuffers(
                    vault,
                    registry,
                    runtimeRegistry,
                    runtimePrefabIds,
                    rawCount,
                    runtimeBindableCount,
                    allowBufferGrowth,
                    allowRuntimeRegistration: allowBufferGrowth,
                    out int activeCount,
                    out long totalVramBytes))
            {
                return false;
            }

            if (publishRuntimeSignals)
                PublishPrefabSignals(registry, rawCount, frame);

            PublishRegistryUpdateSignal(registry.RegistryHash, BufferID.BridgePrefabMapping, activeCount);
            PublishRegistryUpdateSignal(registry.RegistryHash, BufferID.BridgePrefabLoreLinks, activeCount);
            if (activeCount > 0)
                VRAMBudgetTracker.RegisterOrUpdate(registry.RegistryHash, totalVramBytes);
            else
                VRAMBudgetTracker.Unregister(registry.RegistryHash);

            GlobalTelemetryBus.PublishModTelemetry(H8BridgeHashes.PrefabRegistry, registry.RegistryHash, activeCount);
            return true;
        }

        public static bool PrepareBuffers(H8PrefabRegistry registry, IDataVault vault)
        {
            return PrepareBuffers(registry, vault, null);
        }

        public static bool PrepareBuffers(H8PrefabRegistry registry, IDataVault vault, PrefabRegistry runtimeRegistry)
        {
            if (registry == null || vault == null)
                return false;

            int runtimeBindableCount = registry.RefreshRuntimeBindingStateForSync(allowAuthoringRepair: true);
            if (runtimeBindableCount < 0 ||
                registry.ValidationDuplicateHashCount > 0 ||
                runtimeBindableCount > RuntimePrefabIdScratchCapacity)
            {
                return false;
            }

            if (runtimeBindableCount <= 0)
                return true;

            int rawCount = registry.EntryCount;
            if (!TryValidateRuntimeBindableCount(registry, rawCount, runtimeBindableCount))
                return false;

            if (vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                return false;

            if (!TryEnsureBuffer<H8PrefabMappingEntry>(
                    vault,
                    BufferID.BridgePrefabMapping,
                    runtimeBindableCount) ||
                !TryEnsureBuffer<H8PrefabLoreLinkEntry>(
                    vault,
                    BufferID.BridgePrefabLoreLinks,
                    runtimeBindableCount))
            {
                return false;
            }

            uint* runtimePrefabIds = stackalloc uint[RuntimePrefabIdScratchCapacity];
            return TryPopulateRuntimePrefabIds(
                registry,
                runtimeRegistry,
                runtimePrefabIds,
                rawCount,
                runtimeBindableCount,
                allowRuntimeRegistration: runtimeRegistry != null);
        }

        private static bool TryValidateRuntimeBindableCount(H8PrefabRegistry registry, int rawCount, int runtimeBindableCount)
        {
            if (registry == null || rawCount < 0 || runtimeBindableCount <= 0)
                return false;

            int counted = 0;
            for (int i = 0; i < rawCount; i++)
            {
                H8PrefabRegistry.Entry entry = registry.GetEntry(i);
                if (entry != null && entry.IsRuntimeBindable)
                    counted++;
            }

            return counted == runtimeBindableCount;
        }

        private static bool TryWritePrefabBuffers(
            IDataVault vault,
            H8PrefabRegistry registry,
            PrefabRegistry runtimeRegistry,
            uint* runtimePrefabIds,
            int rawCount,
            int runtimeBindableCount,
            bool allowBufferGrowth,
            bool allowRuntimeRegistration,
            out int activeCount,
            out long totalVramBytes)
        {
            activeCount = 0;
            totalVramBytes = 0L;
            if (vault == null ||
                registry == null ||
                runtimeBindableCount <= 0 ||
                vault.IsAllocationLocked ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireMutationGuard(PrefabRegistryBindMutationGuardMask))
            {
                return false;
            }

            bool written = false;
            try
            {
                if (!TryAcquireGuardedBuffer(
                        vault,
                        BufferID.BridgePrefabMapping,
                        runtimeBindableCount,
                        NativeArrayOptions.ClearMemory,
                        allowBufferGrowth,
                        out NativeArray<H8PrefabMappingEntry> mapping) ||
                    !TryAcquireGuardedBuffer(
                        vault,
                        BufferID.BridgePrefabLoreLinks,
                        runtimeBindableCount,
                        NativeArrayOptions.ClearMemory,
                        allowBufferGrowth,
                        out NativeArray<H8PrefabLoreLinkEntry> loreLinks))
                {
                    return false;
                }

                if (!TryPopulateRuntimePrefabIds(
                        registry,
                        runtimeRegistry,
                        runtimePrefabIds,
                        rawCount,
                        runtimeBindableCount,
                        allowRuntimeRegistration))
                {
                    return false;
                }

                ClearBuffer(mapping);
                ClearBuffer(loreLinks);

                int writeIndex = 0;
                long vramBytes = 0L;
                for (int i = 0; i < rawCount; i++)
                {
                    H8PrefabRegistry.Entry entry = registry.GetEntry(i);
                    if (entry == null || !entry.IsRuntimeBindable)
                        continue;

                    if (writeIndex >= runtimeBindableCount)
                        return false;

                    mapping[writeIndex] = entry.ToMappingEntry(runtimePrefabIds[writeIndex]);
                    loreLinks[writeIndex] = entry.ToLoreLinkEntry();
                    vramBytes += entry.EstimatedVramBytes > 0L ? entry.EstimatedVramBytes : 0L;
                    writeIndex++;
                }

                if (writeIndex != runtimeBindableCount)
                    return false;

                Thread.MemoryBarrier();
                activeCount = writeIndex;
                totalVramBytes = vramBytes;
                written = true;
            }
            finally
            {
                vault.ReleaseMutationGuard(PrefabRegistryBindMutationGuardMask);
            }

            return written;
        }

        private static bool TryPopulateRuntimePrefabIds(
            H8PrefabRegistry registry,
            PrefabRegistry runtimeRegistry,
            uint* runtimePrefabIds,
            int rawCount,
            int runtimeBindableCount,
            bool allowRuntimeRegistration)
        {
            if (registry == null || runtimePrefabIds == null || rawCount < 0 || runtimeBindableCount <= 0)
                return false;

            for (int i = 0; i < runtimeBindableCount; i++)
                runtimePrefabIds[i] = 0u;

            int runtimeIndex = 0;
            for (int i = 0; i < rawCount; i++)
            {
                H8PrefabRegistry.Entry entry = registry.GetEntry(i);
                if (entry == null || !entry.IsRuntimeBindable)
                    continue;

                if (runtimeIndex >= runtimeBindableCount)
                    return false;

                if (runtimeRegistry != null && entry.Prefab != null)
                {
                    int runtimePrefabId = allowRuntimeRegistration
                        ? runtimeRegistry.GetOrRegisterPrefab(entry.Prefab)
                        : runtimeRegistry.GetPrefabId(entry.Prefab);

                    if (runtimePrefabId <= 0)
                        return false;

                    runtimePrefabIds[runtimeIndex] = unchecked((uint)runtimePrefabId);
                }

                runtimeIndex++;
            }

            return runtimeIndex == runtimeBindableCount;
        }

        private static bool TryAcquireGuardedBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            bool allowBufferGrowth,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength <= 0 || vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                return false;

            VaultGenerationHandle<T> handle;
            if (allowBufferGrowth)
            {
                handle = vault.EnsureGenerationHandle<T>(
                    bufferId,
                    requiredLength,
                    SystemID.CoreBridge,
                    options);
            }
            else if (!vault.TryGetGenerationHandle<T>(bufferId, out handle))
            {
                return false;
            }

            return handle.BufferID != 0u &&
                   vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool TryEnsureBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength) where T : struct
        {
            if (vault == null || requiredLength <= 0 || vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                return false;

            VaultGenerationHandle<T> handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.CoreBridge,
                NativeArrayOptions.ClearMemory);

            return handle.BufferID != 0u &&
                   vault.TryReadHandle(in handle, out NativeArray<T> buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static void PublishPrefabSignals(H8PrefabRegistry registry, int count, uint frame)
        {
            for (int i = 0; i < count; i++)
            {
                H8PrefabRegistry.Entry entry = registry.GetEntry(i);
                if (entry == null || !entry.IsRuntimeBindable)
                    continue;

                PrefabAcousticSignatureSignal acoustic = default;
                acoustic.PrefabHash = entry.HashID;
                acoustic.AcousticSignatureHash = entry.AcousticSignatureHash;
                acoustic.LoreHash = entry.LoreHash;
                acoustic.Frame = frame;
                acoustic.Resonance01 = 1f;
                acoustic.OneDimensionalLutHash = entry.OneDimensionalLutHash;
                acoustic.Flags = entry.Flags;
                SignalBus<PrefabAcousticSignatureSignal>.TryPushTracked(in acoustic, ref s_x001H8PrefabRegistryRuntimeBinderSignalPushDropCount);

                PrefabLoreLinkSignal lore = default;
                lore.PrefabHash = entry.HashID;
                lore.LoreHash = entry.LoreHash;
                lore.Frame = frame;
                lore.OneDimensionalLutHash = entry.OneDimensionalLutHash;
                lore.HighTierVisualHash = entry.HighTierVisualHash;
                lore.Flags = entry.Flags;
                SignalBus<PrefabLoreLinkSignal>.TryPushTracked(in lore, ref s_x001H8PrefabRegistryRuntimeBinderSignalPushDropCount);
            }
        }

        private static bool ClearExistingBuffers(IDataVault vault)
        {
            if (vault == null)
                return false;

            if (vault.IsAllocationLocked ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireMutationGuard(PrefabRegistryBindMutationGuardMask))
            {
                return false;
            }

            bool cleared = false;
            try
            {
                if (!TryReadExistingGuardedBuffer(
                        vault,
                        BufferID.BridgePrefabMapping,
                        out NativeArray<H8PrefabMappingEntry> mapping,
                        out bool hasMapping) ||
                    !TryReadExistingGuardedBuffer(
                        vault,
                        BufferID.BridgePrefabLoreLinks,
                        out NativeArray<H8PrefabLoreLinkEntry> loreLinks,
                        out bool hasLoreLinks))
                {
                    return false;
                }

                if (hasMapping)
                    ClearBuffer(mapping);

                if (hasLoreLinks)
                    ClearBuffer(loreLinks);

                cleared = true;
            }
            finally
            {
                vault.ReleaseMutationGuard(PrefabRegistryBindMutationGuardMask);
            }

            return cleared;
        }

        private static bool TryReadExistingGuardedBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            out NativeArray<T> buffer,
            out bool exists) where T : struct
        {
            buffer = default;
            exists = false;
            if (vault == null ||
                !vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> handle) ||
                handle.BufferID == 0u)
            {
                return true;
            }

            if (!vault.TryReadHandle(in handle, out buffer))
                return false;

            if (!buffer.IsCreated)
                return false;

            exists = true;
            return true;
        }

        private static void ClearBuffer<T>(NativeArray<T> buffer)
            where T : unmanaged
        {
            if (!buffer.IsCreated || buffer.Length <= 0)
                return;

            long byteCount = (long)buffer.Length * UnsafeUtility.SizeOf<T>();
            Thread.MemoryBarrier();
            UnsafeUtility.MemClear(buffer.GetUnsafePtr(), byteCount);
            Thread.MemoryBarrier();
        }

        private static void PublishRegistryUpdateSignal(uint registryHash, BufferID bufferId, int entryCount)
        {
            if (!Application.isPlaying)
                return;

            DataVaultUpdateSignal signal = new DataVaultUpdateSignal
            {
                SourceHash = registryHash,
                FieldHash = H8BridgeHashes.PrefabRegistry,
                OffsetBytes = -1,
                OldValue = 0f,
                NewValue = entryCount > 0 ? entryCount : 0f,
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                BufferId = (ushort)bufferId,
                Flags = 0
            };
            SignalBus<DataVaultUpdateSignal>.TryPushTracked(in signal, ref s_x001H8PrefabRegistryRuntimeBinderSignalPushDropCount);
        }
    }

    public sealed class H8PrefabRegistryBootBinder : MonoBehaviour
    {
        [SerializeField] private H8PrefabRegistry registry;
        [FormerlySerializedAs("bindOnAwake")]
        [SerializeField] private bool bindOnStart = true;

        public H8PrefabRegistry Registry => registry;

        private void Start()
        {
            if (bindOnStart)
                BindNow();
        }

        [ContextMenu("Bind Prefab Registry Now")]
        public void BindNow()
        {
            if (Application.isPlaying)
            {
                IDataVault vault = GlobalRegistry.DataVault;
                PrefabRegistry runtimeRegistry = GlobalRegistry.PrefabRegistryRuntime;
                if (!H8PrefabRegistryRuntimeBinder.PrepareBuffers(registry, vault, runtimeRegistry))
                    return;

                H8BridgeLiveSyncScheduler.RequestPrefabBind(registry, vault, runtimeRegistry);
                return;
            }

            H8PrefabRegistryRuntimeBinder.Bind(registry, GlobalRegistry.DataVault, GlobalRegistry.PrefabRegistryRuntime);
        }
    }
}
