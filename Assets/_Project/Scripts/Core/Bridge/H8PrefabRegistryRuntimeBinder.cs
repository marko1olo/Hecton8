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

            VaultBufferHandle<H8PrefabMappingEntry> mapping = vault.GetBufferHandle<H8PrefabMappingEntry>(
                BufferID.BridgePrefabMapping,
                count,
                SystemID.CoreBridge,
                NativeArrayOptions.ClearMemory);
            VaultBufferHandle<H8PrefabLoreLinkEntry> loreLinks = vault.GetBufferHandle<H8PrefabLoreLinkEntry>(
                BufferID.BridgePrefabLoreLinks,
                count,
                SystemID.CoreBridge,
                NativeArrayOptions.ClearMemory);

            if (!mapping.IsCreated || mapping.Length < count || !loreLinks.IsCreated || loreLinks.Length < count)
                return false;

            Thread.MemoryBarrier();
            H8PrefabMappingEntry* mappingPtr = (H8PrefabMappingEntry*)mapping.ResolvePointer(vault);
            H8PrefabLoreLinkEntry* loreLinkPtr = (H8PrefabLoreLinkEntry*)loreLinks.ResolvePointer(vault);
            if (mappingPtr == null || loreLinkPtr == null)
                return false;

            ClearBuffer(mappingPtr, mapping.Length);
            ClearBuffer(loreLinkPtr, loreLinks.Length);

            long totalVramBytes = 0L;
            bool publishRuntimeSignals = Application.isPlaying;
            PrefabRegistry runtimeRegistry = publishRuntimeSignals ? GlobalRegistry.PrefabRegistryRuntime : null;
            uint frame = publishRuntimeSignals ? unchecked((uint)Time.frameCount) : 0u;
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

                mappingPtr[writeIndex] = entry.ToMappingEntry(runtimePrefabId);
                loreLinkPtr[writeIndex] = entry.ToLoreLinkEntry();
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
                SignalBus<PrefabAcousticSignatureSignal>.Push(in acoustic);

                PrefabLoreLinkSignal lore = new PrefabLoreLinkSignal
                {
                    PrefabHash = entry.HashID,
                    LoreHash = entry.LoreHash,
                    Frame = frame,
                    OneDimensionalLutHash = entry.OneDimensionalLutHash,
                    HighTierVisualHash = entry.HighTierVisualHash,
                    Flags = entry.Flags
                };
                SignalBus<PrefabLoreLinkSignal>.Push(in lore);
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

            if (vault.TryGetBufferHandle(BufferID.BridgePrefabMapping, out VaultBufferHandle<H8PrefabMappingEntry> mapping) &&
                mapping.IsCreated)
            {
                H8PrefabMappingEntry* mappingPtr = (H8PrefabMappingEntry*)mapping.ResolvePointer(vault);
                ClearBuffer(mappingPtr, mapping.Length);
            }

            if (vault.TryGetBufferHandle(BufferID.BridgePrefabLoreLinks, out VaultBufferHandle<H8PrefabLoreLinkEntry> loreLinks) &&
                loreLinks.IsCreated)
            {
                H8PrefabLoreLinkEntry* loreLinkPtr = (H8PrefabLoreLinkEntry*)loreLinks.ResolvePointer(vault);
                ClearBuffer(loreLinkPtr, loreLinks.Length);
            }
        }

        private static void ClearBuffer<T>(T* ptr, int length)
            where T : unmanaged
        {
            if (ptr == null || length <= 0)
                return;

            long byteCount = (long)length * UnsafeUtility.SizeOf<T>();
            Thread.MemoryBarrier();
            UnsafeUtility.MemClear(ptr, byteCount);
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
                Frame = unchecked((uint)Time.frameCount),
                BufferId = (ushort)bufferId,
                Flags = 0
            };
            SignalBus<DataVaultUpdateSignal>.Push(in signal);
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
