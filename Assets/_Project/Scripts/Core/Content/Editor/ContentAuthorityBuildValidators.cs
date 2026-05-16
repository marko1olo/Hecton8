using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Hecton8.Core.Content;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine.AddressableAssets;
using UnityEngine;

namespace Hecton8.Core.Content.Editor
{
    internal static class ContentAuthorityBuildValidators
    {
        private const string CoreGroupName = "Core";
        private const string HighResGroupName = "High_Res";
        private const string OverkillGroupName = "Overkill";
        private const long MaxSingleContentAssetBytes = 256L * 1024L * 1024L;
        private const string ResourcesApiPrefix = "Resources.";
        private const string ResourcesLoadMethod = "Load";
        private const string ResourcesLoadAllMethod = "LoadAll";
        private static readonly Regex _hashRegex = new Regex(
            "\"(?:itemHash|meshHash|prefabHash|assetHash|hash)\"\\s*:\\s*\"?(?<hash>0x[0-9A-Fa-f]{1,8}|[0-9]{1,10})\"?",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        [MenuItem("HECTON-8/Content/Validate Content Authority")]
        public static void ValidateFromMenu()
        {
            RunAllBuildValidators();
            Debug.Log("[ContentAuthority] Validation passed.");
        }

        public static void RunAllBuildValidators()
        {
            ValidateNoFirstPartyResourcesLoads();
            ValidateAddressableGroups();
            ContentAssetHashMap[] maps = FindHashMaps();
            ValidateHashMapIntegrity(maps);
            ValidateEconomyJsonMeshes(maps);
            ValidateNoCyclicRegistryDependencies(maps);
            ValidateTierGroups(maps);
            ValidateBinaryLayouts();
            ValidateLoreBlockIoBudgets();
            ValidateComputeShaderThreadGroups();
            ValidateVfxPrewarmManifests();
        }

        private static void ValidateNoFirstPartyResourcesLoads()
        {
            const string firstPartyRoot = "Assets/_Project";
            if (!Directory.Exists(firstPartyRoot))
                return;

            string loadCall = ResourcesApiPrefix + ResourcesLoadMethod + "(";
            string loadAllCall = ResourcesApiPrefix + ResourcesLoadAllMethod + "(";
            string[] files = Directory.GetFiles(firstPartyRoot, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i].Replace('\\', '/');
                string source = File.ReadAllText(path);
                if (source.IndexOf(loadCall, StringComparison.Ordinal) < 0 &&
                    source.IndexOf(loadAllCall, StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                Fail("First-party Resources API usage is banned: " + path);
            }
        }

        private static ContentAssetHashMap[] FindHashMaps()
        {
            string[] guids = AssetDatabase.FindAssets("t:ContentAssetHashMap");
            ContentAssetHashMap[] maps = new ContentAssetHashMap[guids.Length];
            int count = 0;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                ContentAssetHashMap map = AssetDatabase.LoadAssetAtPath<ContentAssetHashMap>(path);
                if (map == null)
                    continue;

                map.ForceSort();
                maps[count] = map;
                count++;
            }

            if (count == maps.Length)
                return maps;

            ContentAssetHashMap[] compact = new ContentAssetHashMap[count];
            for (int i = 0; i < count; i++)
                compact[i] = maps[i];
            return compact;
        }

        private static void ValidateHashMapIntegrity(ContentAssetHashMap[] maps)
        {
            Dictionary<uint, string> ownersByHash = new Dictionary<uint, string>(512);
            for (int i = 0; i < maps.Length; i++)
            {
                ContentAssetHashMap map = maps[i];
                if (map == null)
                    continue;

                string mapName = map.name;
                for (int j = 0; j < map.Count; j++)
                {
                    ContentAssetEntry entry = map.GetEntryAt(j);
                    if (entry.Hash == 0u)
                        Fail("ContentAssetHashMap contains zero hash: " + mapName + " index=" + j);

                    if (ownersByHash.TryGetValue(entry.Hash, out string existingOwner))
                    {
                        Fail("Duplicate content hash detected: 0x" + entry.Hash.ToString("X8") +
                             " first=" + existingOwner + " second=" + mapName);
                    }

                    ownersByHash.Add(entry.Hash, mapName);

                    if (entry.EstimatedVramBytes > MaxSingleContentAssetBytes)
                    {
                        Fail("Content entry exceeds single-asset VRAM budget: " +
                             mapName + " hash=0x" + entry.Hash.ToString("X8") +
                             " bytes=" + entry.EstimatedVramBytes);
                    }

                    if (entry.Kind != ContentAssetKind.LoreText && !HasAddressableBinding(in entry))
                    {
                        Fail("Content entry has no Addressables binding: " +
                             mapName + " hash=0x" + entry.Hash.ToString("X8"));
                    }
                }
            }

            for (int i = 0; i < maps.Length; i++)
            {
                ContentAssetHashMap map = maps[i];
                if (map == null)
                    continue;

                string mapName = map.name;
                for (int j = 0; j < map.Count; j++)
                {
                    ContentAssetEntry entry = map.GetEntryAt(j);
                    uint[] dependencies = entry.DependencyHashes;
                    if (dependencies == null)
                        continue;

                    for (int dependencyIndex = 0; dependencyIndex < dependencies.Length; dependencyIndex++)
                    {
                        uint dependency = dependencies[dependencyIndex];
                        if (dependency == 0u || ownersByHash.ContainsKey(dependency))
                            continue;

                        Fail("Content entry references missing dependency hash: " +
                             mapName + " hash=0x" + entry.Hash.ToString("X8") +
                             " dependency=0x" + dependency.ToString("X8"));
                    }
                }
            }
        }

        private static bool HasAddressableBinding(in ContentAssetEntry entry)
        {
            if (!string.IsNullOrEmpty(entry.Address))
                return true;

#if UNITY_ADDRESSABLES_EXIST
            return entry.Asset != null && entry.Asset.RuntimeKeyIsValid();
#else
            return false;
#endif
        }

        private static void ValidateAddressableGroups()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                Fail("Addressables settings asset missing.");

            List<AddressableAssetGroup> groups = settings.groups;
            if (groups == null || groups.Count == 0)
                Fail("Addressables has no groups.");

            bool hasCore = false;
            bool hasHighRes = false;
            bool hasOverkill = false;

            for (int i = 0; i < groups.Count; i++)
            {
                AddressableAssetGroup group = groups[i];
                if (group == null)
                    continue;

                string groupName = group.Name;
                hasCore |= string.Equals(groupName, CoreGroupName, StringComparison.Ordinal);
                hasHighRes |= string.Equals(groupName, HighResGroupName, StringComparison.Ordinal);
                hasOverkill |= string.Equals(groupName, OverkillGroupName, StringComparison.Ordinal);

                HashSet<AddressableAssetEntry>.Enumerator enumerator = group.entries.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    AddressableAssetEntry entry = enumerator.Current;
                    if (entry == null || entry.parentGroup != null)
                        continue;

                    Fail("Addressable entry has no group: " + entry.address);
                }
            }

            if (!hasCore)
                Fail("Addressables tier group missing: Core.");
            if (!hasHighRes)
                Fail("Addressables tier group missing: High_Res.");
            if (!hasOverkill)
                Fail("Addressables tier group missing: Overkill.");
        }

        private static void ValidateEconomyJsonMeshes(ContentAssetHashMap[] maps)
        {
            HashSet<uint> visualHashes = new HashSet<uint>();
            for (int i = 0; i < maps.Length; i++)
            {
                ContentAssetHashMap map = maps[i];
                if (map == null)
                    continue;

                for (int j = 0; j < map.Count; j++)
                {
                    ContentAssetEntry entry = map.GetEntryAt(j);
                    if (entry.Hash != 0u && entry.HasVisual3D())
                        visualHashes.Add(entry.Hash);
                }
            }

            string[] files = Directory.GetFiles("Assets/_Project", "*.json", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i].Replace('\\', '/');
                if (path.IndexOf("Economy", StringComparison.OrdinalIgnoreCase) < 0 &&
                    path.IndexOf("Item", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                string json = File.ReadAllText(path);
                MatchCollection matches = _hashRegex.Matches(json);
                for (int matchIndex = 0; matchIndex < matches.Count; matchIndex++)
                {
                    Match match = matches[matchIndex];
                    uint hash = ParseHash(match.Groups["hash"].Value);
                    if (hash == 0u || visualHashes.Contains(hash))
                        continue;

                    Fail("Economy JSON references item hash with no 3D mesh in ContentAssetHashMap: " +
                         path + " hash=0x" + hash.ToString("X8"));
                }
            }
        }

        private static void ValidateNoCyclicRegistryDependencies(ContentAssetHashMap[] maps)
        {
            Dictionary<uint, ContentAssetEntry> entries = new Dictionary<uint, ContentAssetEntry>(512);
            for (int i = 0; i < maps.Length; i++)
            {
                ContentAssetHashMap map = maps[i];
                if (map == null)
                    continue;

                for (int j = 0; j < map.Count; j++)
                {
                    ContentAssetEntry entry = map.GetEntryAt(j);
                    if (entry.Hash == 0u || entries.ContainsKey(entry.Hash))
                        continue;

                    entries.Add(entry.Hash, entry);
                }
            }

            Dictionary<uint, byte> visitState = new Dictionary<uint, byte>(entries.Count);
            Dictionary<uint, ContentAssetEntry>.Enumerator enumerator = entries.GetEnumerator();
            while (enumerator.MoveNext())
            {
                uint hash = enumerator.Current.Key;
                if (DetectCycle(hash, entries, visitState))
                    Fail("Cyclic bundle dependency detected at hash=0x" + hash.ToString("X8"));
            }
        }

        private static void ValidateTierGroups(ContentAssetHashMap[] maps)
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                Fail("Addressables settings asset missing.");

            Dictionary<string, string> groupByAddress = new Dictionary<string, string>(512, StringComparer.Ordinal);
            Dictionary<string, string> groupByGuid = new Dictionary<string, string>(512, StringComparer.Ordinal);
            BuildAddressableGroupLookup(settings, groupByAddress, groupByGuid);

            for (int i = 0; i < maps.Length; i++)
            {
                ContentAssetHashMap map = maps[i];
                if (map == null)
                    continue;

                for (int j = 0; j < map.Count; j++)
                {
                    ContentAssetEntry entry = map.GetEntryAt(j);
                    if (entry.Kind == ContentAssetKind.LoreText)
                        continue;

                    string groupName = ResolveAddressableGroupName(in entry, groupByAddress, groupByGuid);
                    if (string.IsNullOrEmpty(groupName))
                    {
                        Fail("Content entry is not present in Addressables settings: 0x" +
                             entry.Hash.ToString("X8"));
                    }

                    string expectedGroup = ResolveExpectedGroupName(entry.Tier);
                    if (!string.Equals(groupName, expectedGroup, StringComparison.Ordinal))
                    {
                        Fail("Content entry assigned to wrong Addressables tier group: 0x" +
                             entry.Hash.ToString("X8") + " expected=" + expectedGroup + " actual=" + groupName);
                    }
                }
            }
        }

        private static void BuildAddressableGroupLookup(
            AddressableAssetSettings settings,
            Dictionary<string, string> groupByAddress,
            Dictionary<string, string> groupByGuid)
        {
            List<AddressableAssetGroup> groups = settings.groups;
            if (groups == null)
                return;

            for (int i = 0; i < groups.Count; i++)
            {
                AddressableAssetGroup group = groups[i];
                if (group == null || group.entries == null)
                    continue;

                string groupName = group.Name;
                HashSet<AddressableAssetEntry>.Enumerator enumerator = group.entries.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    AddressableAssetEntry entry = enumerator.Current;
                    if (entry == null)
                        continue;

                    if (!string.IsNullOrEmpty(entry.address) && !groupByAddress.ContainsKey(entry.address))
                        groupByAddress.Add(entry.address, groupName);

                    if (!string.IsNullOrEmpty(entry.guid) && !groupByGuid.ContainsKey(entry.guid))
                        groupByGuid.Add(entry.guid, groupName);
                }
            }
        }

        private static string ResolveAddressableGroupName(
            in ContentAssetEntry entry,
            Dictionary<string, string> groupByAddress,
            Dictionary<string, string> groupByGuid)
        {
            if (!string.IsNullOrEmpty(entry.Address) &&
                groupByAddress.TryGetValue(entry.Address, out string groupName))
            {
                return groupName;
            }

#if UNITY_ADDRESSABLES_EXIST
            if (entry.Asset != null)
            {
                string assetGuid = entry.Asset.AssetGUID;
                if (!string.IsNullOrEmpty(assetGuid) &&
                    groupByGuid.TryGetValue(assetGuid, out groupName))
                {
                    return groupName;
                }
            }
#endif
            return null;
        }

        private static string ResolveExpectedGroupName(ContentTier tier)
        {
            switch (tier)
            {
                case ContentTier.Overkill:
                    return OverkillGroupName;

                case ContentTier.HighRes:
                    return HighResGroupName;

                default:
                    return CoreGroupName;
            }
        }

        private static void ValidateBinaryLayouts()
        {
            AssertSize<ContentAssetBinaryRecord>(32, nameof(ContentAssetBinaryRecord));
            AssertSize<ContentBundleRefState>(24, nameof(ContentBundleRefState));
            AssertSize<ContentAuthorityTelemetryEntry>(64, nameof(ContentAuthorityTelemetryEntry));
            AssertSize<ContentPendingLoadState>(16, nameof(ContentPendingLoadState));
            AssertSize<ContentVisualFeatureBudget>(16, nameof(ContentVisualFeatureBudget));
            AssertSize<ObjectBatchInstance>(80, nameof(ObjectBatchInstance));
            AssertSize<ObjectBatchChunk>(40, nameof(ObjectBatchChunk));
            AssertSize<ContentLoreBlockIndex>(16, nameof(ContentLoreBlockIndex));
        }

        private static void ValidateLoreBlockIoBudgets()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                ContentLoreBinaryProvider[] providers = prefab.GetComponentsInChildren<ContentLoreBinaryProvider>(true);
                for (int providerIndex = 0; providerIndex < providers.Length; providerIndex++)
                {
                    ContentLoreBinaryProvider provider = providers[providerIndex];
                    for (int blockIndex = 0; blockIndex < provider.BlockCount; blockIndex++)
                    {
                        ContentLoreBlockIndex block = provider.GetBlockAt(blockIndex);
                        if (block.Length > ContentLoreBinaryProvider.MaxSynchronousLoreReadBytes)
                        {
                            Fail("Lore block exceeds synchronous I/O budget: " +
                                 path + " hash=0x" + block.Hash.ToString("X8"));
                        }
                    }
                }
            }
        }

        private static void ValidateComputeShaderThreadGroups()
        {
            string[] guids = AssetDatabase.FindAssets("t:ComputeShader", new[] { "Assets/_Project" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                ComputeShader shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(path);
                if (shader == null)
                    continue;

                for (int kernel = 0; kernel < shader.kernelCount; kernel++)
                {
                    shader.GetKernelThreadGroupSizes(kernel, out uint x, out uint y, out uint z);
                    ulong total = (ulong)x * y * z;
                    if (total > 1024UL)
                    {
                        Fail("Compute shader kernel exceeds Metal/Quest thread-group limit: " +
                             path + " kernel=" + kernel + " threads=" + total);
                    }
                }
            }
        }

        private static void ValidateVfxPrewarmManifests()
        {
            string[] guids = AssetDatabase.FindAssets("t:ContentVfxPrewarmManifest", new[] { "Assets/_Project" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                ContentVfxPrewarmManifest manifest = AssetDatabase.LoadAssetAtPath<ContentVfxPrewarmManifest>(path);
                if (manifest == null)
                    continue;

                if (manifest.TotalCount > ContentVfxPrewarmManifest.MaxEntries)
                {
                    Fail("VFX prewarm manifest exceeds fixed handle ledger capacity: " +
                         path + " count=" + manifest.TotalCount +
                         " max=" + ContentVfxPrewarmManifest.MaxEntries);
                }

#if UNITY_ADDRESSABLES_EXIST
                for (int particleIndex = 0; particleIndex < manifest.ParticleSystemCount; particleIndex++)
                {
                    AssetReference particleReference = manifest.GetParticleSystem(particleIndex);
                    if (particleReference == null || !particleReference.RuntimeKeyIsValid())
                    {
                        Fail("VFX prewarm manifest has invalid particle Addressable reference: " +
                             path + " index=" + particleIndex);
                    }
                }

                for (int computeIndex = 0; computeIndex < manifest.ComputeShaderCount; computeIndex++)
                {
                    AssetReference computeReference = manifest.GetComputeShader(computeIndex);
                    if (computeReference == null || !computeReference.RuntimeKeyIsValid())
                    {
                        Fail("VFX prewarm manifest has invalid compute Addressable reference: " +
                             path + " index=" + computeIndex);
                    }
                }
#endif
            }
        }

        private static void AssertSize<T>(int expectedBytes, string typeName) where T : struct
        {
            int actualBytes = Marshal.SizeOf<T>();
            if (actualBytes != expectedBytes)
                Fail(typeName + " binary layout drift. Expected " + expectedBytes + " bytes, got " + actualBytes + ".");
        }

        private static bool DetectCycle(
            uint hash,
            Dictionary<uint, ContentAssetEntry> entries,
            Dictionary<uint, byte> visitState)
        {
            if (visitState.TryGetValue(hash, out byte state))
                return state == 1;

            if (!entries.TryGetValue(hash, out ContentAssetEntry entry))
                return false;

            visitState[hash] = 1;
            uint[] dependencies = entry.DependencyHashes;
            if (dependencies != null)
            {
                for (int i = 0; i < dependencies.Length; i++)
                {
                    uint dependency = dependencies[i];
                    if (dependency == 0u)
                        continue;

                    if (DetectCycle(dependency, entries, visitState))
                        return true;
                }
            }

            visitState[hash] = 2;
            return false;
        }

        private static uint ParseHash(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return 0u;

            if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return Convert.ToUInt32(raw.Substring(2), 16);

            return uint.TryParse(raw, out uint value) ? value : 0u;
        }

        private static void Fail(string message)
        {
            throw new BuildFailedException("[ContentAuthority] " + message);
        }
    }

    public sealed class ContentAuthorityBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => -9000;

        public void OnPreprocessBuild(BuildReport report)
        {
            ContentAuthorityBuildValidators.RunAllBuildValidators();
        }
    }
}
