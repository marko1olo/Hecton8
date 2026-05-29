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
        private static int s_x001H8PrefabRegistryRuntimeBinderSignalPushDropCount;
        public static bool Bind(H8PrefabRegistry registry, IDataVault vault)
        {
            if (registry == null || vault == null)
                return false;

            int count = registry.EntryCount;
            if (count <= 0)
            {
                ClearExistingBuffers(vault);
                VRAMBudgetTracker.Unregister(registry.RegistryHash);
                PublishRegistryUpdateSignal(registry.RegistryHash, BufferID.BridgePrefabMapping, 0);
                PublishRegistryUpdateSignal(registry.RegistryHash, BufferID.BridgePrefabLoreLinks, 0);
                return true;
            }

            if (count > RuntimePrefabIdScratchCapacity)
            {
                ClearExistingBuffers(vault);
                return false;
            }

            bool publishRuntimeSignals = Application.isPlaying;
            PrefabRegistry runtimeRegistry = publishRuntimeSignals ? GlobalRegistry.PrefabRegistryRuntime : null;
            uint frame = publishRuntimeSignals ? Hecton8.Core.SystemDispatcher.CurrentFrameId : 0u;

            uint* runtimePrefabIds = stackalloc uint[RuntimePrefabIdScratchCapacity];
            for (int i = 0; i < count; i++)
                runtimePrefabIds[i] = 0u;

            if (runtimeRegistry != null)
            {
                for (int i = 0; i < count; i++)
                {
                    H8PrefabRegistry.Entry entry = registry.GetEntry(i);
                    if (entry != null && entry.IsRuntimeBindable && entry.Prefab != null)
                        runtimePrefabIds[i] = unchecked((uint)runtimeRegistry.GetOrRegisterPrefab(entry.Prefab));
                }
            }

            VaultGenerationHandle<H8PrefabMappingEntry> mappingHandle = vault.EnsureGenerationHandle<H8PrefabMappingEntry>(
                BufferID.BridgePrefabMapping,
                count,
                SystemID.CoreBridge,
                NativeArrayOptions.ClearMemory);
            VaultGenerationHandle<H8PrefabLoreLinkEntry> loreLinksHandle = vault.EnsureGenerationHandle<H8PrefabLoreLinkEntry>(
                BufferID.BridgePrefabLoreLinks,
                count,
                SystemID.CoreBridge,
                NativeArrayOptions.ClearMemory);

            if (mappingHandle.BufferID == 0u ||
                loreLinksHandle.BufferID == 0u)
            {
                return false;
            }

            if (!TryWriteMappingBuffer(vault, in mappingHandle, registry, runtimePrefabIds, count, out int activeCount, out long totalVramBytes))
                return false;

            if (!TryWriteLoreLinksBuffer(vault, in loreLinksHandle, registry, count))
            {
                ClearMappingBuffer(vault);
                return false;
            }

            if (publishRuntimeSignals)
                PublishPrefabSignals(registry, count, frame);

            PublishRegistryUpdateSignal(registry.RegistryHash, BufferID.BridgePrefabMapping, activeCount);
            PublishRegistryUpdateSignal(registry.RegistryHash, BufferID.BridgePrefabLoreLinks, activeCount);
            if (activeCount > 0)
                VRAMBudgetTracker.RegisterOrUpdate(registry.RegistryHash, totalVramBytes);
            else
                VRAMBudgetTracker.Unregister(registry.RegistryHash);

            GlobalTelemetryBus.PublishModTelemetry(H8BridgeHashes.PrefabRegistry, registry.RegistryHash, activeCount);
            return true;
        }

        private static bool TryWriteMappingBuffer(
            IDataVault vault,
            in VaultGenerationHandle<H8PrefabMappingEntry> mappingHandle,
            H8PrefabRegistry registry,
            uint* runtimePrefabIds,
            int count,
            out int activeCount,
            out long totalVramBytes)
        {
            activeCount = 0;
            totalVramBytes = 0L;
            if (!vault.TryAcquireWriteLock(in mappingHandle, SystemID.CoreBridge, out NativeArray<H8PrefabMappingEntry> mapping))
                return false;

            try
            {
                if (!mapping.IsCreated || mapping.Length < count)
                    return false;

                ClearBuffer(mapping);

                for (int i = 0; i < count; i++)
                {
                    H8PrefabRegistry.Entry entry = registry.GetEntry(i);
                    if (entry == null || !entry.IsRuntimeBindable)
                        continue;

                    int writeIndex = activeCount++;
                    mapping[writeIndex] = entry.ToMappingEntry(runtimePrefabIds[i]);
                    totalVramBytes += entry.EstimatedVramBytes > 0L ? entry.EstimatedVramBytes : 0L;
                }

                Thread.MemoryBarrier();
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in mappingHandle, SystemID.CoreBridge);
            }
        }

        private static bool TryWriteLoreLinksBuffer(
            IDataVault vault,
            in VaultGenerationHandle<H8PrefabLoreLinkEntry> loreLinksHandle,
            H8PrefabRegistry registry,
            int count)
        {
            if (!vault.TryAcquireWriteLock(in loreLinksHandle, SystemID.CoreBridge, out NativeArray<H8PrefabLoreLinkEntry> loreLinks))
                return false;

            try
            {
                if (!loreLinks.IsCreated || loreLinks.Length < count)
                    return false;

                ClearBuffer(loreLinks);

                int loreWriteIndex = 0;
                for (int i = 0; i < count; i++)
                {
                    H8PrefabRegistry.Entry entry = registry.GetEntry(i);
                    if (entry == null || !entry.IsRuntimeBindable)
                        continue;

                    loreLinks[loreWriteIndex++] = entry.ToLoreLinkEntry();
                }

                Thread.MemoryBarrier();
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in loreLinksHandle, SystemID.CoreBridge);
            }
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

        private static void ClearMappingBuffer(IDataVault vault)
        {
            if (vault == null ||
                !vault.TryGetGenerationHandle<H8PrefabMappingEntry>(BufferID.BridgePrefabMapping, out VaultGenerationHandle<H8PrefabMappingEntry> mappingHandle) ||
                mappingHandle.BufferID == 0u ||
                !vault.TryAcquireWriteLock(in mappingHandle, SystemID.CoreBridge, out NativeArray<H8PrefabMappingEntry> mapping))
            {
                return;
            }

            try
            {
                if (mapping.IsCreated)
                    ClearBuffer(mapping);
            }
            finally
            {
                vault.ReleaseWriteLock(in mappingHandle, SystemID.CoreBridge);
            }
        }

        private static void ClearExistingBuffers(IDataVault vault)
        {
            if (vault == null)
                return;

            ClearMappingBuffer(vault);
            ClearLoreLinksBuffer(vault);
        }

        private static void ClearLoreLinksBuffer(IDataVault vault)
        {
            if (vault == null ||
                !vault.TryGetGenerationHandle<H8PrefabLoreLinkEntry>(BufferID.BridgePrefabLoreLinks, out VaultGenerationHandle<H8PrefabLoreLinkEntry> loreLinksHandle) ||
                loreLinksHandle.BufferID == 0u ||
                !vault.TryAcquireWriteLock(in loreLinksHandle, SystemID.CoreBridge, out NativeArray<H8PrefabLoreLinkEntry> loreLinks))
            {
                return;
            }

            try
            {
                if (loreLinks.IsCreated)
                    ClearBuffer(loreLinks);
            }
            finally
            {
                vault.ReleaseWriteLock(in loreLinksHandle, SystemID.CoreBridge);
            }
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
            H8PrefabRegistryRuntimeBinder.Bind(registry, GlobalRegistry.DataVault);
        }
    }
}
