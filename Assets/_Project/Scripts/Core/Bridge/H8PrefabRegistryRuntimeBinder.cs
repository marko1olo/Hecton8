using System.Threading;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

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
            PrefabRegistry runtimeRegistry = GlobalRegistry.PrefabRegistryRuntime;
            uint frame = unchecked((uint)Time.frameCount);

            for (int i = 0; i < count; i++)
            {
                H8PrefabRegistry.Entry entry = registry.GetEntry(i);
                if (entry == null || !entry.IsRuntimeBindable)
                    continue;

                uint runtimePrefabId = 0u;
                if (runtimeRegistry != null && entry.Prefab != null)
                    runtimePrefabId = unchecked((uint)runtimeRegistry.GetOrRegisterPrefab(entry.Prefab));

                mappingPtr[i] = entry.ToMappingEntry(runtimePrefabId);
                loreLinkPtr[i] = entry.ToLoreLinkEntry();
                totalVramBytes += entry.EstimatedVramBytes > 0L ? entry.EstimatedVramBytes : 0L;

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
            VRAMBudgetTracker.RegisterOrUpdate(registry.RegistryHash, totalVramBytes);
            GlobalTelemetryBus.PublishModTelemetry(H8BridgeHashes.PrefabRegistry, registry.RegistryHash, count);
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

            UnsafeUtility.MemClear(ptr, length * UnsafeUtility.SizeOf<T>());
        }
    }

    public sealed class H8PrefabRegistryBootBinder : MonoBehaviour
    {
        [SerializeField] private H8PrefabRegistry registry;
        [SerializeField] private bool bindOnAwake = true;

        public H8PrefabRegistry Registry => registry;

        private void Start()
        {
            if (bindOnAwake)
                BindNow();
        }

        [ContextMenu("Bind Prefab Registry Now")]
        public void BindNow()
        {
            H8PrefabRegistryRuntimeBinder.Bind(registry, GlobalRegistry.DataVault);
        }
    }
}
