using System.Threading;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.Core.Bridge
{
    public static class H8PrefabRegistryRuntimeBinder
    {
        public static bool Bind(H8PrefabRegistry registry, IDataVault vault)
        {
            if (registry == null || vault == null)
                return false;

            int count = registry.EntryCount;
            if (count <= 0)
            {
                VRAMBudgetTracker.Unregister(registry.RegistryHash);
                return true;
            }

            NativeArray<H8PrefabMappingEntry> mapping = vault.GetBuffer<H8PrefabMappingEntry>(
                BufferID.BridgePrefabMapping,
                count,
                SystemID.CoreBridge,
                NativeArrayOptions.ClearMemory);
            NativeArray<H8PrefabLoreLinkEntry> loreLinks = vault.GetBuffer<H8PrefabLoreLinkEntry>(
                BufferID.BridgePrefabLoreLinks,
                count,
                SystemID.CoreBridge,
                NativeArrayOptions.ClearMemory);

            if (!mapping.IsCreated || mapping.Length < count || !loreLinks.IsCreated || loreLinks.Length < count)
                return false;

            Thread.MemoryBarrier();
            long totalVramBytes = 0L;
            PrefabRegistry runtimeRegistry = GlobalRegistry.PrefabRegistryRuntime;
            uint frame = unchecked((uint)Time.frameCount);

            for (int i = 0; i < count; i++)
            {
                H8PrefabRegistry.Entry entry = registry.GetEntry(i);
                if (entry == null)
                    continue;

                uint runtimePrefabId = 0u;
                if (runtimeRegistry != null && entry.Prefab != null)
                    runtimePrefabId = unchecked((uint)runtimeRegistry.GetOrRegisterPrefab(entry.Prefab));

                mapping[i] = entry.ToMappingEntry(runtimePrefabId);
                loreLinks[i] = entry.ToLoreLinkEntry();
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
                SignalBus<PrefabAcousticSignatureSignal>.Push(acoustic);

                PrefabLoreLinkSignal lore = new PrefabLoreLinkSignal
                {
                    PrefabHash = entry.HashID,
                    LoreHash = entry.LoreHash,
                    Frame = frame,
                    OneDimensionalLutHash = entry.OneDimensionalLutHash,
                    HighTierVisualHash = entry.HighTierVisualHash,
                    Flags = entry.Flags
                };
                SignalBus<PrefabLoreLinkSignal>.Push(lore);
            }

            Thread.MemoryBarrier();
            VRAMBudgetTracker.RegisterOrUpdate(registry.RegistryHash, totalVramBytes);
            GlobalTelemetryBus.PublishModTelemetry(H8BridgeHashes.PrefabRegistry, registry.RegistryHash, count);
            return true;
        }
    }

    public sealed class H8PrefabRegistryBootBinder : MonoBehaviour
    {
        [SerializeField] private H8PrefabRegistry registry;
        [SerializeField] private bool bindOnAwake = true;

        public H8PrefabRegistry Registry => registry;

        private void Awake()
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
