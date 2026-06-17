using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Hecton8.Audio.Editor;
using Hecton8.Core.Contracts;
using Hecton8.Core.Content;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine;

namespace Hecton8.Core.Content.Editor
{
    internal static class ContentAuthorityBuildValidators
    {
        private const string CoreGroupName = "Core";
        private const string HighResGroupName = "High_Res";
        private const string OverkillGroupName = "Overkill";
        private const long MaxSingleContentAssetBytes = 256L * 1024L * 1024L;
        private const string UnityEngineNamespace = "UnityEngine";
        private const string ResourcesTypeName = "Resources";
        private const string ResourcesLoadMethod = "Load";
        private const string ResourcesLoadAllMethod = "LoadAll";
        private const string ResourcesLoadAsyncMethod = "LoadAsync";
        private const string CrestOceanConstantsPath = "Assets/Crest/Crest/Shaders/OceanConstants.hlsl";
        private const string GPUInstancerPlatformDefinesPath = "Assets/GPUInstancer/Resources/Compute/Include/PlatformDefines.hlsl";
        private static readonly string[] _computeShaderValidationRootCandidates =
        {
            "Assets/_Project",
            "Assets/GPUInstancer",
            "Assets/Crest",
        };
        private static readonly Regex _hashRegex = new Regex(
            "\"(?:itemHash|meshHash|prefabHash|assetHash|hash)\"\\s*:\\s*\"?(?<hash>0x[0-9A-Fa-f]{1,8}|[0-9]{1,10})\"?",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex _computeKernelPragmaRegex = new Regex(
            "^\\s*#pragma\\s+kernel\\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)(?<defines>.*)$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline);
        private static readonly Regex _computeNumberDefineRegex = new Regex(
            "^\\s*#define\\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\\s+(?<value>[0-9]+)\\b",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline);
        private static readonly Regex _computePragmaNumberDefineRegex = new Regex(
            "(?<name>[A-Za-z_][A-Za-z0-9_]*)=(?<value>[0-9]+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex _computeThreadGroupDeclarationRegex = new Regex(
            "\\[\\s*numthreads\\s*\\(\\s*(?<x>[^,\\)]+)\\s*,\\s*(?<y>[^,\\)]+)\\s*,\\s*(?<z>[^\\)]+)\\s*\\)\\s*\\]",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex _computeKernelEntryRegex = new Regex(
            "\\[\\s*numthreads\\s*\\([^\\)]*\\)\\s*\\]\\s*(?:\\[[^\\]]+\\]\\s*)*void\\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\\s*\\([^\\)]*\\)\\s*\\{",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline);
        private static readonly Regex _voidReturnRegex = new Regex(
            "\\breturn\\s*;",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex _disabledIfZeroDirectiveRegex = new Regex(
            "^\\s*#\\s*if\\s+0(?:\\s|$)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex _ifDirectiveRegex = new Regex(
            "^\\s*#\\s*if\\b",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex _elseDirectiveRegex = new Regex(
            "^\\s*#\\s*else\\b",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex _elifDirectiveRegex = new Regex(
            "^\\s*#\\s*elif\\b",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex _endifDirectiveRegex = new Regex(
            "^\\s*#\\s*endif\\b",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        [MenuItem("Hecton8/Content/Validate Content Authority")]
        public static void ValidateFromMenu()
        {
            RunAllBuildValidators();
            Debug.Log("[ContentAuthority] Validation passed.");
        }

        public static void RunAllBuildValidators()
        {
            ValidateNoFirstPartyResourcesLoads();
            ValidateNoFirstPartyResourcesAssets();
            ValidateAddressableGroups();
            ContentAssetHashMap[] maps = FindHashMaps();
            ValidateHashMapIntegrity(maps);
            ValidateEconomyJsonMeshes(maps);
            ValidateNoCyclicRegistryDependencies(maps);
            ValidateTierGroups(maps);
            ValidateBinaryLayouts();
            ValidateSaveTopologyWriters();
            ValidateObjectBatchPayloads(maps);
            ValidateLoreBlockIoBudgets();
            ValidateRuntimePrefabBindings();
            ValidateAudioEventAuthoring();
            ValidateComputeShaderThreadGroups();
            ValidateVfxPrewarmManifests();
        }

        private static void ValidateNoFirstPartyResourcesLoads()
        {
            const string firstPartyRoot = "Assets/_Project";
            if (!Directory.Exists(firstPartyRoot))
                return;

            string[] files = Directory.GetFiles(firstPartyRoot, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i].Replace('\\', '/');
                string source = File.ReadAllText(path);
                if (!ContainsBannedResourcesLoad(source))
                    continue;

                Fail("First-party Resources API usage is banned: " + path);
            }
        }

        private static void ValidateNoFirstPartyResourcesAssets()
        {
            const string resourcesRoot = "Assets/_Project/Resources";
            if (!Directory.Exists(resourcesRoot))
                return;

            string[] files = Directory.GetFiles(resourcesRoot, "*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i].Replace('\\', '/');
                string extension = Path.GetExtension(path);
                if (string.Equals(extension, ".meta", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(Path.GetFileName(path), "README.md", StringComparison.OrdinalIgnoreCase))
                    continue;

                Fail("First-party runtime assets are banned under Assets/_Project/Resources: " + path);
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

                    ValidateEntryShape(in entry, mapName, j);
                    ValidateBinaryRecordExport(in entry, mapName, j);

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

        private static void ValidateEntryShape(in ContentAssetEntry entry, string mapName, int index)
        {
            if (entry.Kind == ContentAssetKind.Unknown || entry.Kind > ContentAssetKind.Compute)
            {
                Fail("Content entry has invalid kind: " +
                     mapName + " index=" + index + " hash=0x" + entry.Hash.ToString("X8"));
            }

            if (entry.Tier > ContentTier.Overkill)
            {
                Fail("Content entry has invalid tier: " +
                     mapName + " index=" + index + " hash=0x" + entry.Hash.ToString("X8"));
            }

            if (entry.EstimatedVramBytes < 0L)
            {
                Fail("Content entry has negative VRAM estimate: " +
                     mapName + " index=" + index + " hash=0x" + entry.Hash.ToString("X8"));
            }

            bool hasMeshPrefab = entry.MeshPrefab != null;
            bool hasMesh = entry.Mesh != null;
            bool hasAnyVisualBinding = hasMeshPrefab || hasMesh;
            if (hasAnyVisualBinding && !entry.IsVisual3DKind())
            {
                Fail("Content entry has 3D binding on non-visual kind: " +
                     mapName + " index=" + index + " kind=" + entry.Kind +
                     " hash=0x" + entry.Hash.ToString("X8"));
            }

            if (entry.Kind == ContentAssetKind.Mesh && !hasMesh)
            {
                Fail("Mesh content entry has no Mesh binding: " +
                     mapName + " index=" + index + " hash=0x" + entry.Hash.ToString("X8"));
            }

            if (entry.Kind == ContentAssetKind.Prefab && !hasAnyVisualBinding)
            {
                Fail("Prefab content entry has no 3D prefab/mesh binding: " +
                     mapName + " index=" + index + " hash=0x" + entry.Hash.ToString("X8"));
            }

            if (entry.LodLevel > 2)
            {
                Fail("Content entry has unsupported LOD level: " +
                     mapName + " index=" + index + " hash=0x" + entry.Hash.ToString("X8"));
            }

            uint[] dependencies = entry.DependencyHashes;
            if (dependencies == null)
                return;

            if (dependencies.Length > ushort.MaxValue)
            {
                Fail("Content entry dependency list exceeds binary record capacity: " +
                     mapName + " index=" + index + " hash=0x" + entry.Hash.ToString("X8"));
            }

            HashSet<uint> dependencySet = new HashSet<uint>(dependencies.Length);
            for (int dependencyIndex = 0; dependencyIndex < dependencies.Length; dependencyIndex++)
            {
                uint dependency = dependencies[dependencyIndex];
                if (dependency == 0u)
                {
                    Fail("Content entry has zero dependency hash: " +
                         mapName + " index=" + index + " dependencyIndex=" + dependencyIndex);
                }

                if (dependency == entry.Hash)
                {
                    Fail("Content entry depends on itself: " +
                         mapName + " hash=0x" + entry.Hash.ToString("X8"));
                }

                if (!dependencySet.Add(dependency))
                {
                    Fail("Content entry has duplicate dependency hash: " +
                         mapName + " hash=0x" + entry.Hash.ToString("X8") +
                         " dependency=0x" + dependency.ToString("X8"));
                }
            }
        }

        private static void ValidateBinaryRecordExport(in ContentAssetEntry entry, string mapName, int index)
        {
            ContentAssetBinaryRecord record;
            try
            {
                record = entry.ToBinaryRecord(0u);
            }
            catch (InvalidOperationException exception)
            {
                Fail("Content entry failed binary export: " +
                     mapName + " index=" + index + " hash=0x" + entry.Hash.ToString("X8") +
                     " reason=" + exception.Message);
                return;
            }

            if (record.Hash != entry.Hash)
                Fail("Content binary record hash drift: " + mapName + " index=" + index);
            if (record.EstimatedVramBytes != entry.EstimatedVramBytes)
                Fail("Content binary record VRAM drift: " + mapName + " index=" + index);
            if (record.DependencyOffset != 0u)
                Fail("Content binary record dependency offset drift: " + mapName + " index=" + index);

            int expectedDependencyCount = entry.DependencyHashes != null ? entry.DependencyHashes.Length : 0;
            if (record.DependencyCount != expectedDependencyCount)
                Fail("Content binary record dependency count drift: " + mapName + " index=" + index);
            if (record.Kind != entry.Kind)
                Fail("Content binary record kind drift: " + mapName + " index=" + index);
            if (record.Tier != entry.Tier)
                Fail("Content binary record tier drift: " + mapName + " index=" + index);
            if (record.BiomeId != entry.BiomeId)
                Fail("Content binary record biome drift: " + mapName + " index=" + index);
            if (record.LodLevel != entry.LodLevel)
                Fail("Content binary record LOD drift: " + mapName + " index=" + index);

            byte expectedFlags = 0;
            if (entry.RequiredInBuild)
                expectedFlags |= 1;
            if (entry.IsBiomeCache)
                expectedFlags |= 2;
            if (entry.HasVisual3D())
                expectedFlags |= 4;

            if (record.Flags != expectedFlags)
                Fail("Content binary record flag drift: " + mapName + " index=" + index);
            if (record.Reserved0 != 0 || record.Reserved1 != 0u)
                Fail("Content binary record reserved fields are dirty: " + mapName + " index=" + index);
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
                bool isCore = string.Equals(groupName, CoreGroupName, StringComparison.Ordinal);
                bool isHighRes = string.Equals(groupName, HighResGroupName, StringComparison.Ordinal);
                bool isOverkill = string.Equals(groupName, OverkillGroupName, StringComparison.Ordinal);
                hasCore |= isCore;
                hasHighRes |= isHighRes;
                hasOverkill |= isOverkill;
                ValidateAddressableGroupLoadMode(group, groupName, isCore || isHighRes || isOverkill);

                if (group.entries == null)
                    Fail("Addressable group has no entry set: " + groupName);

                using (IEnumerator<AddressableAssetEntry> enumerator = group.entries.GetEnumerator())
                {
                    while (enumerator.MoveNext())
                    {
                        AddressableAssetEntry entry = enumerator.Current;
                        if (entry == null)
                            Fail("Addressable group contains a null entry: " + groupName);

                        if (entry.parentGroup == null)
                        {
                            Fail("Addressable entry has no group: " + entry.address);
                            continue;
                        }

                        if (!ReferenceEquals(entry.parentGroup, group))
                        {
                            Fail("Addressable entry parent group mismatch: " +
                                 entry.address + " listedIn=" + groupName +
                                 " parent=" + entry.parentGroup.Name);
                        }
                    }
                }
            }

            if (!hasCore)
                Fail("Addressables tier group missing: Core.");
            if (!hasHighRes)
                Fail("Addressables tier group missing: High_Res.");
            if (!hasOverkill)
                Fail("Addressables tier group missing: Overkill.");
        }

        private static void ValidateAddressableGroupLoadMode(
            AddressableAssetGroup group,
            string groupName,
            bool isRequiredTierGroup)
        {
            BundledAssetGroupSchema bundledSchema = group.GetSchema<BundledAssetGroupSchema>();
            if (bundledSchema == null)
            {
                if (isRequiredTierGroup)
                    Fail("Addressables tier group missing bundled schema: " + groupName);
                return;
            }

            if (bundledSchema.AssetLoadMode != AssetLoadMode.RequestedAssetAndDependencies)
            {
                Fail("Addressable group uses unsupported AssetLoadMode: " +
                     groupName + " mode=" + bundledSchema.AssetLoadMode +
                     " expected=" + AssetLoadMode.RequestedAssetAndDependencies);
            }
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
                using (IEnumerator<AddressableAssetEntry> enumerator = group.entries.GetEnumerator())
                {
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
            AssertSize<ContentBundleRefState>(32, nameof(ContentBundleRefState));
            AssertSize<ContentAuthorityTelemetryEntry>(64, nameof(ContentAuthorityTelemetryEntry));
            AssertSize<ContentPendingLoadState>(16, nameof(ContentPendingLoadState));
            AssertSize<ContentVisualFeatureBudget>(16, nameof(ContentVisualFeatureBudget));
            AssertSize<ObjectBatchInstance>(80, nameof(ObjectBatchInstance));
            AssertSize<ObjectBatchChunk>(40, nameof(ObjectBatchChunk));
            AssertSize<ContentLoreBlockIndex>(16, nameof(ContentLoreBlockIndex));
        }

        private static void ValidateSaveTopologyWriters()
        {
            ValidateTopologyLengthConstants();
            Span<char> buffer = stackalloc char[ContentSaveSlotTopology.MaxSavePathChars];

            bool wrote = ContentSaveSlotTopology.TryWriteSaveSlotDirectory(0, buffer, out int written);
            ValidateTopologyWrite(wrote, buffer, written, "Saves/slot_0", "save slot directory");

            wrote = ContentSaveSlotTopology.TryWritePlayerDeltaFile(1, buffer, out written);
            ValidateTopologyWrite(wrote, buffer, written, "slot_1.sav", "player delta file");

            wrote = ContentSaveSlotTopology.TryWritePlayerDeltaBackupFile(2, buffer, out written);
            ValidateTopologyWrite(wrote, buffer, written, "slot_2.bak", "player delta backup file");

            wrote = ContentSaveSlotTopology.TryWritePlayerDeltaTempFile(0, buffer, out written);
            ValidateTopologyWrite(wrote, buffer, written, "slot_0.tmp", "player delta temp file");

            wrote = ContentSaveSlotTopology.TryWriteMacroDatabaseSectorFile(0x0123456789ABCDEFUL, buffer, out written);
            ValidateTopologyWrite(wrote, buffer, written, "sector_0123456789ABCDEF.h8page", "macro database sector file");

            if (ContentSaveSlotTopology.TryWriteSaveSlotDirectory(-1, buffer, out written) ||
                ContentSaveSlotTopology.TryWriteSaveSlotDirectory(3, buffer, out written))
            {
                Fail("Save topology accepted a slot outside slot_0..slot_2.");
            }

            ValidateTopologySmallBufferRejections();
        }

        private static void ValidateTopologyLengthConstants()
        {
            ValidateTopologyLengthConstant(
                ContentSaveSlotTopology.SaveSlotDirectoryChars,
                "Saves/slot_0",
                "save slot directory");
            ValidateTopologyLengthConstant(
                ContentSaveSlotTopology.PlayerDeltaFileChars,
                "slot_1.sav",
                "player delta file");
            ValidateTopologyLengthConstant(
                ContentSaveSlotTopology.PlayerDeltaBackupFileChars,
                "slot_2.bak",
                "player delta backup file");
            ValidateTopologyLengthConstant(
                ContentSaveSlotTopology.PlayerDeltaTempFileChars,
                "slot_0.tmp",
                "player delta temp file");
            ValidateTopologyLengthConstant(
                ContentSaveSlotTopology.MacroDatabaseSectorFileChars,
                "sector_0123456789ABCDEF.h8page",
                "macro database sector file");

            if (!IsSaveTopologyMaxPathOwnerCurrent())
                Fail("Save topology max path length no longer matches the macro database sector path.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool IsSaveTopologyMaxPathOwnerCurrent()
        {
            return ContentSaveSlotTopology.MaxSavePathChars == ContentSaveSlotTopology.MacroDatabaseSectorFileChars;
        }

        private static void ValidateTopologyLengthConstant(int actual, string expectedPath, string label)
        {
            if (actual != expectedPath.Length)
                Fail("Save topology length constant drift: " + label);
        }

        private static void ValidateTopologySmallBufferRejections()
        {
            int written;
            Span<char> smallBuffer = stackalloc char[ContentSaveSlotTopology.SaveSlotDirectoryChars - 1];
            bool wrote = ContentSaveSlotTopology.TryWriteSaveSlotDirectory(0, smallBuffer, out written);
            ValidateTopologyRejection(wrote, written, "undersized save slot directory buffer");

            smallBuffer = stackalloc char[ContentSaveSlotTopology.PlayerDeltaFileChars - 1];
            wrote = ContentSaveSlotTopology.TryWritePlayerDeltaFile(1, smallBuffer, out written);
            ValidateTopologyRejection(wrote, written, "undersized player delta file buffer");

            smallBuffer = stackalloc char[ContentSaveSlotTopology.PlayerDeltaBackupFileChars - 1];
            wrote = ContentSaveSlotTopology.TryWritePlayerDeltaBackupFile(2, smallBuffer, out written);
            ValidateTopologyRejection(wrote, written, "undersized player delta backup buffer");

            smallBuffer = stackalloc char[ContentSaveSlotTopology.PlayerDeltaTempFileChars - 1];
            wrote = ContentSaveSlotTopology.TryWritePlayerDeltaTempFile(0, smallBuffer, out written);
            ValidateTopologyRejection(wrote, written, "undersized player delta temp buffer");

            smallBuffer = stackalloc char[ContentSaveSlotTopology.MacroDatabaseSectorFileChars - 1];
            wrote = ContentSaveSlotTopology.TryWriteMacroDatabaseSectorFile(0x0123456789ABCDEFUL, smallBuffer, out written);
            ValidateTopologyRejection(wrote, written, "undersized macro database sector buffer");
        }

        private static void ValidateTopologyRejection(bool wrote, int written, string label)
        {
            if (wrote)
                Fail("Save topology writer accepted " + label);
            if (written != 0)
                Fail("Save topology writer reported chars for rejected write: " + label);
        }

        private static void ValidateTopologyWrite(
            bool wrote,
            Span<char> buffer,
            int written,
            string expected,
            string label)
        {
            if (!wrote)
                Fail("Save topology writer failed: " + label);
            if (written != expected.Length)
                Fail("Save topology writer length drift: " + label);

            for (int i = 0; i < expected.Length; i++)
            {
                if (buffer[i] != expected[i])
                    Fail("Save topology writer output drift: " + label);
            }
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
                    string dictionaryPath = provider.DictionaryRelativePath;
                    if (!ContentLoreBinaryProvider.IsPortableDictionaryRelativePath(dictionaryPath))
                    {
                        Fail("Lore dictionary path is not portable: " + path);
                    }

                    int blockCount = provider.BlockCount;
                    if (blockCount == 0)
                        Fail("Lore provider has no block index entries: " + path);

                    HashSet<uint> hashes = new HashSet<uint>(blockCount);
                    ContentLoreBlockIndex[] blocksByOffset = new ContentLoreBlockIndex[blockCount];
                    for (int blockIndex = 0; blockIndex < blockCount; blockIndex++)
                    {
                        ContentLoreBlockIndex block = provider.GetBlockAt(blockIndex);
                        if (block.Hash == 0u)
                            Fail("Lore block has zero hash: " + path + " blockIndex=" + blockIndex);
                        if (!hashes.Add(block.Hash))
                        {
                            Fail("Lore provider has duplicate block hash: " +
                                 path + " hash=0x" + block.Hash.ToString("X8"));
                        }

                        if (block.Offset < 0L)
                            Fail("Lore block has negative offset: " + path + " hash=0x" + block.Hash.ToString("X8"));
                        if (block.Length <= 0)
                            Fail("Lore block has invalid length: " + path + " hash=0x" + block.Hash.ToString("X8"));
                        if (block.Length > ContentLoreBinaryProvider.MaxSynchronousLoreReadBytes)
                        {
                            Fail("Lore block exceeds synchronous I/O budget: " +
                                 path + " hash=0x" + block.Hash.ToString("X8"));
                        }

                        long end = block.Offset + block.Length;
                        if (end < block.Offset)
                            Fail("Lore block range overflows: " + path + " hash=0x" + block.Hash.ToString("X8"));

                        blocksByOffset[blockIndex] = block;
                    }

                    SortLoreBlocksByOffset(blocksByOffset);
                    long previousEnd = 0L;
                    bool hasPrevious = false;
                    for (int blockIndex = 0; blockIndex < blocksByOffset.Length; blockIndex++)
                    {
                        ContentLoreBlockIndex block = blocksByOffset[blockIndex];
                        if (hasPrevious && block.Offset < previousEnd)
                        {
                            Fail("Lore block byte ranges overlap: " +
                                 path + " hash=0x" + block.Hash.ToString("X8"));
                        }

                        previousEnd = block.Offset + block.Length;
                        hasPrevious = true;
                    }
                }
            }
        }

        private static void SortLoreBlocksByOffset(ContentLoreBlockIndex[] blocks)
        {
            if (blocks == null)
                return;

            for (int i = 1; i < blocks.Length; i++)
            {
                ContentLoreBlockIndex current = blocks[i];
                int j = i - 1;
                while (j >= 0 && blocks[j].Offset > current.Offset)
                {
                    blocks[j + 1] = blocks[j];
                    j--;
                }

                blocks[j + 1] = current;
            }
        }

        private static void ValidateObjectBatchPayloads(ContentAssetHashMap[] maps)
        {
            HashSet<uint> registeredVisualHashes = BuildRegisteredVisualHashSet(maps);
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/_Project" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                ObjectBatchBase batch = AssetDatabase.LoadAssetAtPath<ObjectBatchBase>(path);
                if (batch == null)
                    continue;

                int meshCount = batch.MeshCount;
                int materialCount = batch.MaterialCount;
                int instanceCount = batch.InstanceCount;
                int chunkCount = batch.ChunkCount;
                if (meshCount == 0 && materialCount == 0 && instanceCount == 0 && chunkCount == 0)
                    continue;

                if (meshCount == 0)
                    Fail("Object batch has no mesh table: " + path);
                if (materialCount == 0)
                    Fail("Object batch has no material table: " + path);
                if (instanceCount == 0)
                    Fail("Object batch has no instances: " + path);
                if (chunkCount == 0)
                    Fail("Object batch has no chunks: " + path);

                byte[] chunkCoverage = new byte[instanceCount];
                for (int meshIndex = 0; meshIndex < meshCount; meshIndex++)
                {
                    if (batch.GetMesh(meshIndex) == null)
                        Fail("Object batch has null mesh binding: " + path + " meshIndex=" + meshIndex);
                }

                for (int materialIndex = 0; materialIndex < materialCount; materialIndex++)
                {
                    if (batch.GetMaterial(materialIndex) == null)
                        Fail("Object batch has null material binding: " + path + " materialIndex=" + materialIndex);
                }

                for (int instanceIndex = 0; instanceIndex < instanceCount; instanceIndex++)
                {
                    ObjectBatchInstance instance = batch.GetInstance(instanceIndex);
                    if (instance.AssetHash == 0u)
                        Fail("Object batch instance has zero asset hash: " + path + " instanceIndex=" + instanceIndex);
                    if (!registeredVisualHashes.Contains(instance.AssetHash))
                    {
                        Fail("Object batch instance hash has no registered 3D content binding: " +
                             path + " instanceIndex=" + instanceIndex + " hash=0x" + instance.AssetHash.ToString("X8"));
                    }

                    if (instance.MeshIndex < 0 || instance.MeshIndex >= meshCount)
                        Fail("Object batch instance has invalid mesh index: " + path + " instanceIndex=" + instanceIndex);
                    if (instance.MaterialIndex < 0 || instance.MaterialIndex >= materialCount)
                        Fail("Object batch instance has invalid material index: " + path + " instanceIndex=" + instanceIndex);
                    if (!IsFinite(instance.LocalToWorld))
                        Fail("Object batch instance has non-finite transform: " + path + " instanceIndex=" + instanceIndex);
                }

                for (int chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
                {
                    ObjectBatchChunk chunk = batch.GetChunk(chunkIndex);
                    if (chunk.ChunkHash == 0u)
                        Fail("Object batch chunk has zero chunk hash: " + path + " chunkIndex=" + chunkIndex);
                    if (chunk.Count <= 0)
                        Fail("Object batch chunk has empty range: " + path + " chunkIndex=" + chunkIndex);
                    if (chunk.StartIndex < 0 || chunk.Count > instanceCount || chunk.StartIndex > instanceCount - chunk.Count)
                        Fail("Object batch chunk range exceeds instance payload: " + path + " chunkIndex=" + chunkIndex);
                    if (chunk.LodLevel > 2)
                        Fail("Object batch chunk uses unsupported LOD level: " + path + " chunkIndex=" + chunkIndex);
                    if (!IsFinite(chunk.Bounds))
                        Fail("Object batch chunk has non-finite bounds: " + path + " chunkIndex=" + chunkIndex);

                    int endIndex = chunk.StartIndex + chunk.Count;
                    for (int instanceIndex = chunk.StartIndex; instanceIndex < endIndex; instanceIndex++)
                    {
                        if (chunkCoverage[instanceIndex] != 0)
                        {
                            Fail("Object batch instance is covered by multiple chunks: " +
                                 path + " instanceIndex=" + instanceIndex);
                        }

                        chunkCoverage[instanceIndex] = 1;
                    }
                }

                for (int instanceIndex = 0; instanceIndex < instanceCount; instanceIndex++)
                {
                    if (chunkCoverage[instanceIndex] == 0)
                    {
                        Fail("Object batch instance is not covered by any chunk: " +
                             path + " instanceIndex=" + instanceIndex);
                    }
                }
            }
        }

        private static HashSet<uint> BuildRegisteredVisualHashSet(ContentAssetHashMap[] maps)
        {
            HashSet<uint> hashes = new HashSet<uint>(512);
            int mapCount = maps != null ? maps.Length : 0;
            for (int mapIndex = 0; mapIndex < mapCount; mapIndex++)
            {
                ContentAssetHashMap map = maps[mapIndex];
                if (map == null)
                    continue;

                int entryCount = map.Count;
                for (int entryIndex = 0; entryIndex < entryCount; entryIndex++)
                {
                    ContentAssetEntry entry = map.GetEntryAt(entryIndex);
                    if (entry.Hash != 0u && entry.HasVisual3D())
                        hashes.Add(entry.Hash);
                }
            }

            return hashes;
        }

        private static void ValidateRuntimePrefabBindings()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                ContentAuthorityRuntime[] runtimes = prefab.GetComponentsInChildren<ContentAuthorityRuntime>(true);
                for (int runtimeIndex = 0; runtimeIndex < runtimes.Length; runtimeIndex++)
                {
                    ContentAuthorityRuntime runtime = runtimes[runtimeIndex];
                    if (runtime.AssetHashMap == null)
                        Fail("ContentAuthorityRuntime prefab missing asset hash map: " + path);
                    if (!runtime.HasHologramProxyBinding)
                        Fail("ContentAuthorityRuntime prefab missing hologram proxy mesh/material: " + path);
                    if (runtime.HologramPoolCapacity <= 0 ||
                        runtime.HologramPoolCapacity > ContentAuthorityRuntime.MaxPendingLoadCount)
                    {
                        Fail("ContentAuthorityRuntime prefab has invalid hologram pool capacity: " + path);
                    }
                }
            }
        }

        private static void ValidateAudioEventAuthoring()
        {
            if (!AudioEventAuthoringValidator.Run(out string report))
                Fail(report);
        }

        private static void ValidateComputeShaderThreadGroups()
        {
            int portableMaxThreads = Math.Min(
                Math.Min(
                    HectonPlatformContract.QuestSafeComputeThreadsPerGroup,
                    HectonPlatformContract.AndroidSafeComputeThreadsPerGroup),
                HectonPlatformContract.MetalSafeComputeThreadsPerGroup);
            int portableMaxThreadGroupZ = Math.Min(
                Math.Min(
                    HectonPlatformContract.QuestMaxThreadGroupZ,
                    HectonPlatformContract.AndroidMaxThreadGroupZ),
                HectonPlatformContract.MetalMaxThreadGroupZ);

            string[] roots = ResolveExistingAssetRoots(_computeShaderValidationRootCandidates);
            if (roots.Length == 0)
                return;

            Dictionary<string, int> sharedComputeDefines = CreateSharedComputeNumberDefines();
            ValidateComputeShaderSourceFiles(
                roots,
                portableMaxThreads,
                portableMaxThreadGroupZ,
                sharedComputeDefines);

            string[] guids = AssetDatabase.FindAssets("t:ComputeShader", roots);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                ComputeShader shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(path);
                if (shader == null)
                    continue;

                string source = ReadTextFileOrEmpty(path);
                ValidateComputeShaderSynchronization(path, source);

                int[] kernelIndices = ResolveComputeShaderKernelIndices(path, shader);
                for (int kernelIndex = 0; kernelIndex < kernelIndices.Length; kernelIndex++)
                {
                    int kernel = kernelIndices[kernelIndex];
                    if (!TryGetKernelThreadGroupSizes(shader, kernel, out uint x, out uint y, out uint z))
                        continue;

                    if (x == 0u || y == 0u || z == 0u)
                    {
                        Fail("Compute shader kernel has non-positive thread-group dimension: " +
                             path + " kernel=" + kernel + " x=" + x + " y=" + y + " z=" + z);
                    }

                    ulong total = (ulong)x * y * z;
                    if (total > (ulong)portableMaxThreads)
                    {
                        Fail("Compute shader kernel exceeds portable Quest/Android/Metal thread-group limit: " +
                             path + " kernel=" + kernel + " threads=" + total +
                             " max=" + portableMaxThreads);
                    }

                    if (z > (uint)portableMaxThreadGroupZ)
                    {
                        Fail("Compute shader kernel exceeds portable Z thread-group limit: " +
                             path + " kernel=" + kernel + " z=" + z +
                             " maxZ=" + portableMaxThreadGroupZ);
                    }
                }
            }
        }

        private static void ValidateComputeShaderSourceFiles(
            string[] roots,
            int portableMaxThreads,
            int portableMaxThreadGroupZ,
            Dictionary<string, int> sharedComputeDefines)
        {
            if (roots == null || roots.Length == 0)
                return;

            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                string root = roots[rootIndex];
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                    continue;

                string[] files = Directory.GetFiles(root, "*.compute", SearchOption.AllDirectories);
                for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
                {
                    string path = files[fileIndex].Replace('\\', '/');
                    ValidateComputeShaderSourceThreadGroups(
                        path,
                        ReadTextFileOrEmpty(path),
                        portableMaxThreads,
                        portableMaxThreadGroupZ,
                        sharedComputeDefines);
                }
            }
        }

        private static void ValidateComputeShaderSourceThreadGroups(
            string path,
            string source,
            int portableMaxThreads,
            int portableMaxThreadGroupZ,
            Dictionary<string, int> sharedComputeDefines)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(source))
                return;

            Dictionary<string, int> localDefines = sharedComputeDefines == null
                ? new Dictionary<string, int>()
                : new Dictionary<string, int>(sharedComputeDefines);
            MergeComputeNumberDefines(localDefines, source);

            string validationSource = StripCommentsAndDisabledZeroBlocks(source);
            List<Dictionary<string, int>> pragmaDefineSets = ResolveComputeShaderPragmaDefineSets(validationSource);
            MatchCollection declarations = _computeThreadGroupDeclarationRegex.Matches(validationSource);
            for (int declarationIndex = 0; declarationIndex < declarations.Count; declarationIndex++)
            {
                Match declaration = declarations[declarationIndex];
                for (int pragmaIndex = 0; pragmaIndex < pragmaDefineSets.Count; pragmaIndex++)
                {
                    Dictionary<string, int> pragmaDefines = pragmaDefineSets[pragmaIndex];
                    int x;
                    int y;
                    int z;
                    if (!TryResolveComputeThreadCount(declaration.Groups["x"].Value, localDefines, pragmaDefines, out x) ||
                        !TryResolveComputeThreadCount(declaration.Groups["y"].Value, localDefines, pragmaDefines, out y) ||
                        !TryResolveComputeThreadCount(declaration.Groups["z"].Value, localDefines, pragmaDefines, out z))
                    {
                        Fail("Compute shader source thread-group declaration uses unresolved numeric token: " +
                             path + " line=" + CountLineNumber(source, declaration.Index) +
                             " declaration=" + declaration.Value);
                        continue;
                    }

                    if (x <= 0 || y <= 0 || z <= 0)
                    {
                        Fail("Compute shader source thread-group has non-positive dimension: " +
                             path + " line=" + CountLineNumber(source, declaration.Index) +
                             " x=" + x + " y=" + y + " z=" + z +
                             " declaration=" + declaration.Value);
                    }

                    ulong total = (ulong)x * (ulong)y * (ulong)z;
                    if (total > (ulong)portableMaxThreads)
                    {
                        Fail("Compute shader source thread-group exceeds portable Quest/Android/Metal limit: " +
                             path + " line=" + CountLineNumber(source, declaration.Index) +
                             " threads=" + total + " max=" + portableMaxThreads +
                             " declaration=" + declaration.Value);
                    }

                    if (z > portableMaxThreadGroupZ)
                    {
                        Fail("Compute shader source thread-group exceeds portable Z limit: " +
                             path + " line=" + CountLineNumber(source, declaration.Index) +
                             " z=" + z + " maxZ=" + portableMaxThreadGroupZ +
                             " declaration=" + declaration.Value);
                    }
                }
            }
        }

        private static void ValidateComputeShaderSynchronization(string path, string source)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(source))
                return;

            string validationSource = StripCommentsAndDisabledZeroBlocks(source);
            MatchCollection entries = _computeKernelEntryRegex.Matches(validationSource);
            for (int i = 0; i < entries.Count; i++)
            {
                Match entry = entries[i];
                int bodyStart = entry.Index + entry.Length - 1;
                int bodyEnd = FindMatchingBrace(validationSource, bodyStart);
                if (bodyEnd <= bodyStart)
                    continue;

                int bodyLength = bodyEnd - bodyStart + 1;
                string body = validationSource.Substring(bodyStart, bodyLength);
                int lastBarrierIndex = body.LastIndexOf("GroupMemoryBarrierWithGroupSync", StringComparison.Ordinal);
                if (lastBarrierIndex < 0)
                    continue;

                Match voidReturn = _voidReturnRegex.Match(body);
                if (!voidReturn.Success)
                    continue;

                if (voidReturn.Index > lastBarrierIndex)
                    continue;

                string kernelName = entry.Groups["name"].Value;
                int lineNumber = CountLineNumber(source, bodyStart + voidReturn.Index);
                Fail("Compute shader synchronized kernel has early void return before/around group barrier: " +
                     path + " kernel=" + kernelName + " line=" + lineNumber);
            }
        }

        private static int FindMatchingBrace(string source, int openingBraceIndex)
        {
            if (string.IsNullOrEmpty(source) ||
                openingBraceIndex < 0 ||
                openingBraceIndex >= source.Length ||
                source[openingBraceIndex] != '{')
                return -1;

            int depth = 0;
            bool inLineComment = false;
            bool inBlockComment = false;
            for (int i = openingBraceIndex; i < source.Length; i++)
            {
                char c = source[i];
                char next = i + 1 < source.Length ? source[i + 1] : '\0';

                if (inLineComment)
                {
                    if (c == '\n' || c == '\r')
                        inLineComment = false;
                    continue;
                }

                if (inBlockComment)
                {
                    if (c == '*' && next == '/')
                    {
                        inBlockComment = false;
                        i++;
                    }
                    continue;
                }

                if (c == '/' && next == '/')
                {
                    inLineComment = true;
                    i++;
                    continue;
                }

                if (c == '/' && next == '*')
                {
                    inBlockComment = true;
                    i++;
                    continue;
                }

                if (c == '{')
                {
                    depth++;
                    continue;
                }

                if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }

            return -1;
        }

        private static string StripComments(string source)
        {
            if (string.IsNullOrEmpty(source))
                return string.Empty;

            char[] stripped = source.ToCharArray();
            bool inLineComment = false;
            bool inBlockComment = false;
            for (int i = 0; i < stripped.Length; i++)
            {
                char c = stripped[i];
                char next = i + 1 < stripped.Length ? stripped[i + 1] : '\0';

                if (inLineComment)
                {
                    if (c == '\n' || c == '\r')
                    {
                        inLineComment = false;
                    }
                    else
                    {
                        stripped[i] = ' ';
                    }
                    continue;
                }

                if (inBlockComment)
                {
                    if (c == '*' && next == '/')
                    {
                        stripped[i] = ' ';
                        stripped[i + 1] = ' ';
                        inBlockComment = false;
                        i++;
                    }
                    else if (c != '\n' && c != '\r')
                    {
                        stripped[i] = ' ';
                    }
                    continue;
                }

                if (c == '/' && next == '/')
                {
                    stripped[i] = ' ';
                    stripped[i + 1] = ' ';
                    inLineComment = true;
                    i++;
                    continue;
                }

                if (c == '/' && next == '*')
                {
                    stripped[i] = ' ';
                    stripped[i + 1] = ' ';
                    inBlockComment = true;
                    i++;
                }
            }

            return new string(stripped);
        }

        private static string StripCommentsAndDisabledZeroBlocks(string source)
        {
            string strippedSource = StripComments(source);
            if (string.IsNullOrEmpty(strippedSource))
                return string.Empty;

            char[] stripped = strippedSource.ToCharArray();
            int inactiveIfZeroDepth = 0;
            int lineStart = 0;
            while (lineStart < stripped.Length)
            {
                int lineEnd = lineStart;
                while (lineEnd < stripped.Length &&
                       stripped[lineEnd] != '\r' &&
                       stripped[lineEnd] != '\n')
                {
                    lineEnd++;
                }

                string line = new string(stripped, lineStart, lineEnd - lineStart);
                bool isIfZero = _disabledIfZeroDirectiveRegex.IsMatch(line);
                bool isIf = _ifDirectiveRegex.IsMatch(line);
                bool isElse = _elseDirectiveRegex.IsMatch(line);
                bool isElif = _elifDirectiveRegex.IsMatch(line);
                bool isEndIf = _endifDirectiveRegex.IsMatch(line);

                if (inactiveIfZeroDepth > 0)
                {
                    BlankNonNewlineCharacters(stripped, lineStart, lineEnd);

                    if (isIf)
                    {
                        inactiveIfZeroDepth++;
                    }
                    else if (isEndIf)
                    {
                        inactiveIfZeroDepth--;
                    }
                    else if (inactiveIfZeroDepth == 1 && (isElse || isElif))
                    {
                        inactiveIfZeroDepth = 0;
                    }
                }
                else if (isIfZero)
                {
                    inactiveIfZeroDepth = 1;
                    BlankNonNewlineCharacters(stripped, lineStart, lineEnd);
                }

                lineStart = lineEnd;
                if (lineStart < stripped.Length && stripped[lineStart] == '\r')
                    lineStart++;
                if (lineStart < stripped.Length && stripped[lineStart] == '\n')
                    lineStart++;
            }

            return new string(stripped);
        }

        private static void BlankNonNewlineCharacters(char[] source, int start, int end)
        {
            for (int i = start; i < end; i++)
                source[i] = ' ';
        }

        private static int CountLineNumber(string source, int index)
        {
            if (string.IsNullOrEmpty(source) || index <= 0)
                return 1;

            int clampedIndex = Math.Min(index, source.Length);
            int line = 1;
            for (int i = 0; i < clampedIndex; i++)
            {
                if (source[i] == '\n')
                    line++;
            }

            return line;
        }

        private static string[] ResolveExistingAssetRoots(string[] rootCandidates)
        {
            if (rootCandidates == null || rootCandidates.Length == 0)
                return Array.Empty<string>();

            string[] roots = new string[rootCandidates.Length];
            int count = 0;
            for (int i = 0; i < rootCandidates.Length; i++)
            {
                string root = rootCandidates[i];
                if (string.IsNullOrEmpty(root) || !AssetDatabase.IsValidFolder(root))
                    continue;

                roots[count] = root;
                count++;
            }

            if (count == roots.Length)
                return roots;

            string[] compact = new string[count];
            Array.Copy(roots, compact, count);
            return compact;
        }

        private static Dictionary<string, int> CreateSharedComputeNumberDefines()
        {
            Dictionary<string, int> defines = new Dictionary<string, int>();
            MergeMaxComputeNumberDefines(defines, CrestOceanConstantsPath);
            MergeMaxComputeNumberDefines(defines, GPUInstancerPlatformDefinesPath);
            return defines;
        }

        private static void MergeMaxComputeNumberDefines(Dictionary<string, int> defines, string path)
        {
            if (defines == null || string.IsNullOrEmpty(path))
                return;

            string source = ReadTextFileOrEmpty(path);
            if (string.IsNullOrEmpty(source))
                return;

            MatchCollection matches = _computeNumberDefineRegex.Matches(source);
            for (int i = 0; i < matches.Count; i++)
            {
                string name = matches[i].Groups["name"].Value;
                int value = int.Parse(matches[i].Groups["value"].Value, System.Globalization.CultureInfo.InvariantCulture);
                if (!defines.TryGetValue(name, out int existing) || value > existing)
                    defines[name] = value;
            }
        }

        private static void MergeComputeNumberDefines(Dictionary<string, int> defines, string source)
        {
            if (defines == null || string.IsNullOrEmpty(source))
                return;

            MatchCollection matches = _computeNumberDefineRegex.Matches(source);
            for (int i = 0; i < matches.Count; i++)
            {
                string name = matches[i].Groups["name"].Value;
                int value = int.Parse(matches[i].Groups["value"].Value, System.Globalization.CultureInfo.InvariantCulture);
                defines[name] = value;
            }
        }

        private static List<Dictionary<string, int>> ResolveComputeShaderPragmaDefineSets(string source)
        {
            List<Dictionary<string, int>> result = new List<Dictionary<string, int>>();
            if (!string.IsNullOrEmpty(source))
            {
                MatchCollection pragmas = _computeKernelPragmaRegex.Matches(source);
                for (int pragmaIndex = 0; pragmaIndex < pragmas.Count; pragmaIndex++)
                {
                    Dictionary<string, int> defines = new Dictionary<string, int>();
                    MatchCollection pairs = _computePragmaNumberDefineRegex.Matches(pragmas[pragmaIndex].Groups["defines"].Value);
                    for (int pairIndex = 0; pairIndex < pairs.Count; pairIndex++)
                    {
                        defines[pairs[pairIndex].Groups["name"].Value] = int.Parse(
                            pairs[pairIndex].Groups["value"].Value,
                            System.Globalization.CultureInfo.InvariantCulture);
                    }

                    if (defines.Count > 0)
                        result.Add(defines);
                }
            }

            if (result.Count == 0)
                result.Add(new Dictionary<string, int>());

            return result;
        }

        private static bool TryResolveComputeThreadCount(
            string token,
            Dictionary<string, int> localDefines,
            Dictionary<string, int> pragmaDefines,
            out int value)
        {
            token = token.Trim();
            if (int.TryParse(token, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out value))
                return true;

            if (pragmaDefines != null && pragmaDefines.TryGetValue(token, out value))
                return true;

            if (localDefines != null && localDefines.TryGetValue(token, out value))
                return true;

            value = 0;
            return false;
        }

        private static int[] ResolveComputeShaderKernelIndices(string path, ComputeShader shader)
        {
            if (shader == null || string.IsNullOrEmpty(path))
                return Array.Empty<int>();

            string source = ReadTextFileOrEmpty(path);
            if (string.IsNullOrEmpty(source))
                return Array.Empty<int>();

            MatchCollection matches = _computeKernelPragmaRegex.Matches(source);
            if (matches.Count == 0)
                return Array.Empty<int>();

            int[] kernels = new int[matches.Count * 2];
            int count = 0;
            for (int i = 0; i < matches.Count; i++)
            {
                string kernelName = matches[i].Groups["name"].Value;
                if (string.IsNullOrEmpty(kernelName))
                    continue;

                int kernel;
                try
                {
                    kernel = shader.FindKernel(kernelName);
                }
                catch (ArgumentException)
                {
                    continue;
                }
                catch (ObjectDisposedException)
                {
                    continue;
                }
                catch (InvalidOperationException)
                {
                    continue;
                }
                catch (MissingReferenceException)
                {
                    continue;
                }
                catch (UnityException)
                {
                    continue;
                }

                AddUniqueKernelIndex(kernels, ref count, kernel);
                if (HasDuplicateKernelPragmaName(matches, i, kernelName))
                    AddUniqueKernelIndex(kernels, ref count, i);
            }

            if (count == kernels.Length)
                return kernels;

            int[] compact = new int[count];
            Array.Copy(kernels, compact, count);
            return compact;
        }

        private static string ReadTextFileOrEmpty(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            try
            {
                return File.ReadAllText(path);
            }
            catch (IOException)
            {
                return string.Empty;
            }
            catch (UnauthorizedAccessException)
            {
                return string.Empty;
            }
        }

        private static bool TryGetKernelThreadGroupSizes(
            ComputeShader shader,
            int kernel,
            out uint x,
            out uint y,
            out uint z)
        {
            x = 0u;
            y = 0u;
            z = 0u;
            if (shader == null || kernel < 0)
                return false;

            try
            {
                shader.GetKernelThreadGroupSizes(kernel, out x, out y, out z);
                return true;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (IndexOutOfRangeException)
            {
                return false;
            }
            catch (MissingReferenceException)
            {
                return false;
            }
            catch (UnityException)
            {
                return false;
            }
        }

        private static bool HasDuplicateKernelPragmaName(MatchCollection matches, int index, string kernelName)
        {
            for (int i = 0; i < matches.Count; i++)
            {
                if (i == index)
                    continue;

                if (string.Equals(matches[i].Groups["name"].Value, kernelName, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static void AddUniqueKernelIndex(int[] kernels, ref int count, int kernel)
        {
            for (int i = 0; i < count; i++)
            {
                if (kernels[i] == kernel)
                    return;
            }

            if (count < kernels.Length)
                kernels[count++] = kernel;
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
                HashSet<object> prewarmRuntimeKeys = new HashSet<object>(manifest.TotalCount);
                for (int particleIndex = 0; particleIndex < manifest.ParticleSystemCount; particleIndex++)
                {
                    AssetReference particleReference = manifest.GetParticleSystem(particleIndex);
                    if (particleReference == null || !particleReference.RuntimeKeyIsValid())
                    {
                        Fail("VFX prewarm manifest has invalid particle Addressable reference: " +
                             path + " index=" + particleIndex);
                    }

                    ValidateUniqueVfxPrewarmReference(prewarmRuntimeKeys, particleReference, path, particleIndex, true);

                    if (!IsValidParticlePrewarmAsset(particleReference.editorAsset))
                    {
                        Fail("VFX prewarm manifest particle reference is not a ParticleSystem or prefab containing one: " +
                             path + " index=" + particleIndex);
                    }

                    if (!IsParticlePrewarmHierarchyWithinBudget(particleReference.editorAsset))
                    {
                        Fail("VFX prewarm manifest particle prefab exceeds hierarchy traversal budget: " +
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

                    ValidateUniqueVfxPrewarmReference(prewarmRuntimeKeys, computeReference, path, computeIndex, false);

                    if (!(computeReference.editorAsset is ComputeShader))
                    {
                        Fail("VFX prewarm manifest compute reference is not a ComputeShader: " +
                             path + " index=" + computeIndex);
                    }
                }
#endif
            }
        }

        private static void ValidateUniqueVfxPrewarmReference(
            HashSet<object> runtimeKeys,
            AssetReference reference,
            string path,
            int index,
            bool particle)
        {
            object runtimeKey = reference.RuntimeKey;
            if (runtimeKey == null)
                Fail("VFX prewarm manifest has null runtime key: " + path + " index=" + index);

            if (!runtimeKeys.Add(runtimeKey))
            {
                string kind = particle ? "particle" : "compute";
                Fail("VFX prewarm manifest has duplicate " + kind + " Addressable reference: " +
                     path + " index=" + index);
            }
        }

        private static bool IsValidParticlePrewarmAsset(UnityEngine.Object asset)
        {
            if (asset is ParticleSystem)
                return true;

            GameObject gameObject = asset as GameObject;
            return gameObject != null && ContainsParticleSystem(gameObject.transform);
        }

        private static bool ContainsParticleSystem(Transform root)
        {
            if (root == null)
                return false;

            if (root.TryGetComponent(out ParticleSystem _))
                return true;

            int childCount = root.childCount;
            for (int i = 0; i < childCount; i++)
            {
                if (ContainsParticleSystem(root.GetChild(i)))
                    return true;
            }

            return false;
        }

        private static bool IsParticlePrewarmHierarchyWithinBudget(UnityEngine.Object asset)
        {
            if (!(asset is GameObject gameObject))
                return true;

            int visitedNodes = 0;
            return ValidateParticlePrewarmHierarchyBudget(gameObject.transform, 0, ref visitedNodes);
        }

        private static bool ValidateParticlePrewarmHierarchyBudget(Transform root, int depth, ref int visitedNodes)
        {
            if (root == null)
                return true;
            if (depth > ContentVfxPrewarmManifest.MaxParticlePrefabDepth)
                return false;
            if (visitedNodes >= ContentVfxPrewarmManifest.MaxParticlePrefabNodes)
                return false;

            visitedNodes++;
            int childCount = root.childCount;
            for (int i = 0; i < childCount; i++)
            {
                if (!ValidateParticlePrewarmHierarchyBudget(root.GetChild(i), depth + 1, ref visitedNodes))
                    return false;
            }

            return true;
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

        private static bool ContainsBannedResourcesLoad(string source)
        {
            int length = source != null ? source.Length : 0;
            for (int i = 0; i < length; i++)
            {
                int afterResources;
                if (!MatchesIdentifier(source, i, ResourcesTypeName, out afterResources))
                {
                    if (!MatchesIdentifier(source, i, UnityEngineNamespace, out int afterUnityEngine) ||
                        !TryReadDotIdentifier(source, afterUnityEngine, ResourcesTypeName, out afterResources))
                    {
                        continue;
                    }
                }

                int afterMethod;
                if (!TryReadDotIdentifier(source, afterResources, ResourcesLoadMethod, out afterMethod) &&
                    !TryReadDotIdentifier(source, afterResources, ResourcesLoadAllMethod, out afterMethod) &&
                    !TryReadDotIdentifier(source, afterResources, ResourcesLoadAsyncMethod, out afterMethod))
                {
                    continue;
                }

                int afterWhitespace = SkipWhitespace(source, afterMethod);
                if (afterWhitespace < length && source[afterWhitespace] == '(')
                    return true;
            }

            return false;
        }

        private static bool TryReadDotIdentifier(string source, int index, string token, out int afterToken)
        {
            afterToken = index;
            int length = source != null ? source.Length : 0;
            int cursor = SkipWhitespace(source, index);
            if (cursor >= length || source[cursor] != '.')
                return false;

            cursor = SkipWhitespace(source, cursor + 1);
            return MatchesIdentifier(source, cursor, token, out afterToken);
        }

        private static bool MatchesIdentifier(string source, int index, string token, out int afterToken)
        {
            afterToken = index;
            int length = source != null ? source.Length : 0;
            int tokenLength = token != null ? token.Length : 0;
            if (tokenLength == 0 || index < 0 || index + tokenLength > length)
                return false;

            if (index > 0 && IsIdentifierPart(source[index - 1]))
                return false;

            for (int i = 0; i < tokenLength; i++)
            {
                if (source[index + i] != token[i])
                    return false;
            }

            afterToken = index + tokenLength;
            return afterToken >= length || !IsIdentifierPart(source[afterToken]);
        }

        private static int SkipWhitespace(string source, int index)
        {
            int length = source != null ? source.Length : 0;
            int cursor = index;
            while (cursor < length && char.IsWhiteSpace(source[cursor]))
                cursor++;

            return cursor;
        }

        private static bool IsIdentifierPart(char value)
        {
            return value == '_' || char.IsLetterOrDigit(value);
        }

        private static bool IsFinite(Matrix4x4 matrix)
        {
            return IsFinite(matrix.m00) && IsFinite(matrix.m01) &&
                   IsFinite(matrix.m02) && IsFinite(matrix.m03) &&
                   IsFinite(matrix.m10) && IsFinite(matrix.m11) &&
                   IsFinite(matrix.m12) && IsFinite(matrix.m13) &&
                   IsFinite(matrix.m20) && IsFinite(matrix.m21) &&
                   IsFinite(matrix.m22) && IsFinite(matrix.m23) &&
                   IsFinite(matrix.m30) && IsFinite(matrix.m31) &&
                   IsFinite(matrix.m32) && IsFinite(matrix.m33);
        }

        private static bool IsFinite(Bounds bounds)
        {
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            return IsFinite(center.x) && IsFinite(center.y) && IsFinite(center.z) &&
                   IsFinite(extents.x) && IsFinite(extents.y) && IsFinite(extents.z) &&
                   extents.x >= 0f && extents.y >= 0f && extents.z >= 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
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
