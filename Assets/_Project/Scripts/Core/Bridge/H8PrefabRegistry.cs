using System;
using System.Collections.Generic;
using Hecton8.Core.Contracts.Signals;
using Unity.Mathematics;
using UnityEngine;

#if UNITY_ADDRESSABLES_EXIST
using UnityEngine.AddressableAssets;
#endif

namespace Hecton8.Core.Bridge
{
    [CreateAssetMenu(fileName = "H8PrefabRegistry", menuName = "Hecton-8/Bridge/Prefab Registry")]
    public sealed class H8PrefabRegistry : ScriptableObject
    {
        private static int s_x001H8PrefabRegistrySignalPushDropCount;
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private uint hashID;
            [SerializeField] private GameObject prefab;
#if UNITY_ADDRESSABLES_EXIST
            [SerializeField] private AssetReferenceGameObject addressablePrefab;
#endif
            [SerializeField] private uint addressHash;
            [SerializeField] private uint loreHash;
            [SerializeField] private uint acousticSignatureHash;
            [SerializeField] private uint oneDimensionalLutHash;
            [SerializeField] private uint highTierVisualHash;
            [SerializeField] private long estimatedVramBytes;
            [SerializeField] private ushort flags;

            public uint HashID => hashID;
            public GameObject Prefab => prefab;
#if UNITY_ADDRESSABLES_EXIST
            public AssetReferenceGameObject AddressablePrefab => addressablePrefab;
#endif
            public uint AddressHash => addressHash;
            public uint LoreHash => loreHash;
            public uint AcousticSignatureHash => acousticSignatureHash;
            public uint OneDimensionalLutHash => oneDimensionalLutHash;
            public uint HighTierVisualHash => highTierVisualHash;
            public long EstimatedVramBytes => estimatedVramBytes;
            public ushort Flags => flags;
            public bool IsRuntimeBindable
            {
                get
                {
                    if (prefab != null)
                        return true;
#if UNITY_ADDRESSABLES_EXIST
                    return HasAddressableReference();
#else
                    return false;
#endif
                }
            }

            public void AssignPrefab(GameObject value)
            {
                prefab = value;
            }

#if UNITY_ADDRESSABLES_EXIST
            public void AssignAddressable(AssetReferenceGameObject value)
            {
                addressablePrefab = value;
            }
#endif

            public void AssignLoreHash(uint value)
            {
                loreHash = value;
            }

            public void AssignLutHash(uint value)
            {
                oneDimensionalLutHash = value;
            }

            public void AssignHighTierVisualHash(uint value)
            {
                highTierVisualHash = value;
            }

            public void AssignEstimatedVramBytes(long value)
            {
                estimatedVramBytes = value > 0L ? value : 0L;
            }

            public void RebuildHashes()
            {
                if (prefab == null && !HasAddressableReference())
                {
                    ClearRuntimeBinding();
                    return;
                }

                string sourceName = ResolveSourceName();
                hashID = H8BridgeHashes.ComputeFnv1A(sourceName);
                addressHash = H8BridgeHashes.ComputeFnv1A(sourceName, H8BridgeHashes.AddressSeed);
                if (loreHash == 0u)
                    loreHash = H8BridgeHashes.ComputeFnv1A(sourceName, H8BridgeHashes.LoreSeed);

                if (acousticSignatureHash == 0u)
                    acousticSignatureHash = H8BridgeHashes.ComputeFnv1A(sourceName, H8BridgeHashes.AcousticSeed);

                if (oneDimensionalLutHash == 0u)
                    oneDimensionalLutHash = H8BridgeHashes.ComputeFnv1A(sourceName, H8BridgeHashes.LutSeed);

                if (highTierVisualHash == 0u)
                    highTierVisualHash = H8BridgeHashes.ComputeFnv1A(sourceName, H8BridgeHashes.VisualOverkillSeed);

                H8PrefabMappingFlags nextFlags = H8PrefabMappingFlags.None;
#if UNITY_ADDRESSABLES_EXIST
                if (addressablePrefab != null && !string.IsNullOrEmpty(addressablePrefab.AssetGUID))
                    nextFlags |= H8PrefabMappingFlags.Addressable;
#endif
                if (loreHash != 0u)
                    nextFlags |= H8PrefabMappingFlags.HasLore;
                if (acousticSignatureHash != 0u)
                    nextFlags |= H8PrefabMappingFlags.HasAcousticSignature;
                if (oneDimensionalLutHash != 0u)
                    nextFlags |= H8PrefabMappingFlags.UsesOneDimensionalLut;
                if (highTierVisualHash != 0u)
                    nextFlags |= H8PrefabMappingFlags.HighTierVisualOverkill;
                flags = (ushort)nextFlags;
            }

            private string ResolveSourceName()
            {
                if (prefab != null)
                    return prefab.name;

#if UNITY_ADDRESSABLES_EXIST
                if (HasAddressableReference())
                    return addressablePrefab.AssetGUID;
#endif

                return string.Empty;
            }

            private bool HasAddressableReference()
            {
#if UNITY_ADDRESSABLES_EXIST
                return addressablePrefab != null && !string.IsNullOrEmpty(addressablePrefab.AssetGUID);
#else
                return false;
#endif
            }

            private void ClearRuntimeBinding()
            {
                hashID = 0u;
                addressHash = 0u;
                loreHash = 0u;
                acousticSignatureHash = 0u;
                oneDimensionalLutHash = 0u;
                highTierVisualHash = 0u;
                estimatedVramBytes = 0L;
                flags = (ushort)H8PrefabMappingFlags.None;
            }

            public H8PrefabMappingEntry ToMappingEntry(uint runtimePrefabId)
            {
                return new H8PrefabMappingEntry
                {
                    HashID = hashID,
                    AddressHash = addressHash,
                    LoreHash = loreHash,
                    AcousticSignatureHash = acousticSignatureHash,
                    EstimatedVramBytes = estimatedVramBytes,
                    RuntimePrefabId = runtimePrefabId,
                    Flags = flags,
                    OneDimensionalLutHash = oneDimensionalLutHash,
                    HighTierVisualHash = highTierVisualHash
                };
            }

            public H8PrefabLoreLinkEntry ToLoreLinkEntry()
            {
                return new H8PrefabLoreLinkEntry
                {
                    PrefabHash = hashID,
                    LoreHash = loreHash,
                    AcousticSignatureHash = acousticSignatureHash,
                    OneDimensionalLutHash = oneDimensionalLutHash,
                    HighTierVisualHash = highTierVisualHash,
                    Flags = flags
                };
            }
        }

        [SerializeField] private List<Entry> entries = new List<Entry>(128);
        [SerializeField] private bool bindOnValidateInPlayMode = true;
        [SerializeField] private uint registryHash = H8BridgeHashes.PrefabRegistry;

        public int EntryCount => entries != null ? entries.Count : 0;
        public uint RegistryHash => registryHash == 0u ? H8BridgeHashes.PrefabRegistry : registryHash;
        public bool BindOnValidateInPlayMode => bindOnValidateInPlayMode;

        public Entry GetEntry(int index)
        {
            return entries != null && index >= 0 && index < entries.Count ? entries[index] : null;
        }

        public bool TryFind(uint hashId, out Entry entry)
        {
            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    Entry candidate = entries[i];
                    if (candidate != null && candidate.HashID == hashId)
                    {
                        entry = candidate;
                        return true;
                    }
                }
            }

            entry = null;
            return false;
        }

        public Entry AddOrUpdatePrefab(GameObject prefab)
        {
            if (prefab == null)
                return null;

            if (entries == null)
                entries = new List<Entry>(128);

            uint prefabHash = H8BridgeHashes.ComputeFnv1A(prefab.name);
            for (int i = 0; i < entries.Count; i++)
            {
                Entry existing = entries[i];
                if (existing == null)
                    continue;

                if (existing.HashID != prefabHash)
                    continue;

                existing.AssignPrefab(prefab);
                existing.RebuildHashes();
                PublishPrefabSignals(existing);
                return existing;
            }

            Entry entry = new Entry();
            entry.AssignPrefab(prefab);
            entry.RebuildHashes();
            entries.Add(entry);
            PublishPrefabSignals(entry);
            return entry;
        }

#if UNITY_ADDRESSABLES_EXIST
        public Entry AddOrUpdateAddressablePrefab(GameObject prefab, AssetReferenceGameObject addressablePrefab)
        {
            Entry entry = AddOrUpdatePrefab(prefab);
            if (entry == null)
                return null;

            entry.AssignAddressable(addressablePrefab);
            entry.RebuildHashes();
            return entry;
        }
#endif

        public void RebuildAllHashes()
        {
            ValidateEntries();
        }

        public long EstimateTotalVramBytes()
        {
            long total = 0L;
            if (entries == null)
                return total;

            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (entry != null)
                    total += entry.EstimatedVramBytes > 0L ? entry.EstimatedVramBytes : 0L;
            }

            return total;
        }

        private void OnValidate()
        {
            ValidateEntries();
            if (bindOnValidateInPlayMode && Application.isPlaying)
                H8PrefabRegistryRuntimeBinder.Bind(this, GlobalRegistry.DataVault);
        }

        private void ValidateEntries()
        {
            if (entries == null)
                entries = new List<Entry>(128);

            if (registryHash == 0u)
                registryHash = H8BridgeHashes.PrefabRegistry;

            for (int i = entries.Count - 1; i >= 0; i--)
            {
                Entry entry = entries[i];
                if (entry == null)
                {
                    entries.RemoveAt(i);
                    continue;
                }

#if UNITY_EDITOR
                if (entry.Prefab != null)
                    entry.AssignEstimatedVramBytes(H8PrefabRegistryVramEstimator.EstimatePrefabBytes(entry.Prefab));
#endif
                entry.RebuildHashes();
            }
        }

        private static void PublishPrefabSignals(Entry entry)
        {
            if (entry == null)
                return;

            if (!entry.IsRuntimeBindable)
                return;

            if (!Application.isPlaying)
                return;

            uint frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
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
            SignalBus<PrefabAcousticSignatureSignal>.TryPushTracked(in acoustic, ref s_x001H8PrefabRegistrySignalPushDropCount);

            PrefabLoreLinkSignal lore = new PrefabLoreLinkSignal
            {
                PrefabHash = entry.HashID,
                LoreHash = entry.LoreHash,
                Frame = frame,
                OneDimensionalLutHash = entry.OneDimensionalLutHash,
                HighTierVisualHash = entry.HighTierVisualHash,
                Flags = entry.Flags
            };
            SignalBus<PrefabLoreLinkSignal>.TryPushTracked(in lore, ref s_x001H8PrefabRegistrySignalPushDropCount);
        }
    }

#if UNITY_EDITOR
    internal static class H8PrefabRegistryVramEstimator
    {
        private static readonly List<Renderer> s_RendererScratch = new List<Renderer>(32);
        private static readonly List<Material> s_MaterialScratch = new List<Material>(8);

        public static long EstimatePrefabBytes(GameObject prefab)
        {
            if (prefab == null)
                return 0L;

            long total = 0L;
            HashSet<int> countedTextures = new HashSet<int>();
            s_RendererScratch.Clear();
            prefab.GetComponentsInChildren(true, s_RendererScratch);
            for (int i = 0; i < s_RendererScratch.Count; i++)
            {
                Renderer renderer = s_RendererScratch[i];
                if (renderer == null)
                    continue;

                s_MaterialScratch.Clear();
                renderer.GetSharedMaterials(s_MaterialScratch);
                for (int j = 0; j < s_MaterialScratch.Count; j++)
                {
                    Material material = s_MaterialScratch[j];
                    if (material == null)
                        continue;

                    string[] textureNames = material.GetTexturePropertyNames();
                    for (int k = 0; k < textureNames.Length; k++)
                    {
                        Texture texture = material.GetTexture(textureNames[k]);
                        if (texture == null)
                            continue;

                        int textureId = texture.GetEntityId().GetHashCode();
                        if (!countedTextures.Add(textureId))
                            continue;

                        total += EstimateTextureBytes(texture);
                    }
                }
            }

            s_MaterialScratch.Clear();
            s_RendererScratch.Clear();
            return total;
        }

        private static long EstimateTextureBytes(Texture texture)
        {
            if (texture == null)
                return 0L;

            long pixels = math.max(1, texture.width) * (long)math.max(1, texture.height);
            const long BytesPerPixelRgba = 4L;
            const long MipOverheadNumerator = 4L;
            const long MipOverheadDenominator = 3L;
            return (pixels * BytesPerPixelRgba * MipOverheadNumerator) / MipOverheadDenominator;
        }
    }
#endif
}
