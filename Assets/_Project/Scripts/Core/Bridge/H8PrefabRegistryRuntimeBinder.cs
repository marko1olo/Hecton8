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
                loreLinksHandle.BufferID == 0u ||
                !vault.TryResolveHandle(in mappingHandle, out NativeArray<H8PrefabMappingEntry> mapping) ||
                !vault.TryResolveHandle(in loreLinksHandle, out NativeArray<H8PrefabLoreLinkEntry> loreLinks) ||
                !mapping.IsCreated ||
                !loreLinks.IsCreated ||
                mapping.Length < count ||
                loreLinks.Length < count)
            {
                return false;
            }

            Thread.MemoryBarrier();
            ClearBuffer(mapping);
            ClearBuffer(loreLinks);

            long totalVramBytes = 0L;
            bool publishRuntimeSignals = Application.isPlaying;
            PrefabRegistry runtimeRegistry = publishRuntimeSignals ? GlobalRegistry.PrefabRegistryRuntime : null;
            uint frame = publishRuntimeSignals ? Hecton8.Core.SystemDispatcher.CurrentFrameId : 0u;
            int activeCount = 0;

            for (int i = 0; i < count; i++)
            {
                H8PrefabRegistry.Entry entry = registry.GetEntry(i);
                if (entry == null || !entry.IsRuntimeBindable)
                    continue;

                int writeIndex = activeCount++;
                uint runtimePrefabId = 0u;
                if (runtimeRegistry != null && entry.Prefab != null)
                    runtimePrefabId = unchecked((uint)runtimeRegistry.GetOrRegisterPrefab(entry.Prefab));

                mapping[writeIndex] = entry.ToMappingEntry(runtimePrefabId);
                loreLinks[writeIndex] = entry.ToLoreLinkEntry();
                totalVramBytes += entry.EstimatedVramBytes > 0L ? entry.EstimatedVramBytes : 0L;

                if (!publishRuntimeSignals)
                    continue;

                PrefabAcousticSignatureSignal acoustic = new PrefabAcousticSignatureSignal
                {
                    PrefabHash = entry.HashID,
                    AcousticSignatureHash = entry.AcousticSignatureHash,
                    LoreHash = entry.LoreHash,
                    Frame = frame,
                    Resonance01 = 1f,
                    OneDimensionalLutHash = entry.OneDimensionalLutHash,
                    Flags = entry.Flags
                };
                SignalBus<PrefabAcousticSignatureSignal>.TryPushTracked(in acoustic, ref s_x001H8PrefabRegistryRuntimeBinderSignalPushDropCount);

                PrefabLoreLinkSignal lore = new PrefabLoreLinkSignal
                {
                    PrefabHash = entry.HashID,
                    LoreHash = entry.LoreHash,
                    Frame = frame,
                    OneDimensionalLutHash = entry.OneDimensionalLutHash,
                    HighTierVisualHash = entry.HighTierVisualHash,
                    Flags = entry.Flags
                };
                SignalBus<PrefabLoreLinkSignal>.TryPushTracked(in lore, ref s_x001H8PrefabRegistryRuntimeBinderSignalPushDropCount);
            }

            Thread.MemoryBarrier();
            PublishRegistryUpdateSignal(registry.RegistryHash, BufferID.BridgePrefabMapping, activeCount);
            PublishRegistryUpdateSignal(registry.RegistryHash, BufferID.BridgePrefabLoreLinks, activeCount);
            if (activeCount > 0)
                VRAMBudgetTracker.RegisterOrUpdate(registry.RegistryHash, totalVramBytes);
            else
                VRAMBudgetTracker.Unregister(registry.RegistryHash);

            GlobalTelemetryBus.PublishModTelemetry(H8BridgeHashes.PrefabRegistry, registry.RegistryHash, activeCount);
            return true;
        }

        private static void ClearExistingBuffers(IDataVault vault)
        {
            if (vault == null)
                return;

            if (vault.TryGetGenerationHandle<H8PrefabMappingEntry>(BufferID.BridgePrefabMapping, out VaultGenerationHandle<H8PrefabMappingEntry> mappingHandle) &&
                mappingHandle.BufferID != 0u &&
                vault.TryResolveHandle(in mappingHandle, out NativeArray<H8PrefabMappingEntry> mapping) &&
                mapping.IsCreated)
            {
                ClearBuffer(mapping);
            }

            if (vault.TryGetGenerationHandle<H8PrefabLoreLinkEntry>(BufferID.BridgePrefabLoreLinks, out VaultGenerationHandle<H8PrefabLoreLinkEntry> loreLinksHandle) &&
                loreLinksHandle.BufferID != 0u &&
                vault.TryResolveHandle(in loreLinksHandle, out NativeArray<H8PrefabLoreLinkEntry> loreLinks) &&
                loreLinks.IsCreated)
            {
                ClearBuffer(loreLinks);
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
