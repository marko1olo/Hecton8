using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Hecton8.Core.Content;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Hecton8.Core.Content.Editor
{
    internal static class ContentAuthorityBuildValidators
    {
        private const string CoreGroupName = "Core";
        private const string HighResGroupName = "High_Res";
        private const string OverkillGroupName = "Overkill";
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
            ValidateAddressableGroups();
            ContentAssetHashMap[] maps = FindHashMaps();
            ValidateEconomyJsonMeshes(maps);
            ValidateNoCyclicRegistryDependencies(maps);
            ValidateTierGroups(maps);
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
            for (int i = 0; i < maps.Length; i++)
            {
                ContentAssetHashMap map = maps[i];
                if (map == null)
                    continue;

                for (int j = 0; j < map.Count; j++)
                {
                    ContentAssetEntry entry = map.GetEntryAt(j);
                    if (entry.Tier != ContentTier.Overkill)
                        continue;

                    if (string.IsNullOrEmpty(entry.Address) ||
                        entry.Address.IndexOf(OverkillGroupName, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        Fail("Overkill asset is not isolated behind the Overkill Addressables group/address: 0x" +
                             entry.Hash.ToString("X8"));
                    }
                }
            }
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
