#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Hecton8.Core;
using Hecton8.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Audio.Editor
{
    /// <summary>
    /// Editor-only bridge check for authored one-based AudioEvent IDs before runtime queue rejection.
    /// </summary>
    public static class AudioEventAuthoringValidator
    {
        private const string ProjectRoot = "Assets/_Project";
        private const string SpatialAudioClipTableProperty = "_audioEventClipTable";

        private static readonly List<Component> ComponentBuffer = new List<Component>(256);
        private static readonly Regex SerializedAudioEventIdYamlRegex = new Regex(
            "^\\s*(?<name>[A-Za-z_][A-Za-z0-9_]*(?:AudioEventId|AudioEventID))\\s*:\\s*(?<value>-?[0-9]+)\\s*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline);

        [MenuItem("Hecton8/Audio/Validate Audio Event Authoring")]
        public static void RunMenuItem()
        {
            bool passed = Run(out string report);
            if (passed)
                H8Debug.Log(report);
            else
                H8Debug.LogError(report);
        }

        public static bool Run(out string report)
        {
            int failureCount = 0;
            int validatedBindingCount = 0;
            StringBuilder builder = new StringBuilder(4096);
            builder.AppendLine("[AudioEventAuthoringValidator]");

            AudioEventTableSnapshot table = BuildAudioEventTableSnapshot(builder, ref failureCount);
            if (table.HasTable)
            {
                ValidateActiveScenes(table, builder, ref failureCount, ref validatedBindingCount);
                ValidateSceneAssetFiles(table, builder, ref failureCount, ref validatedBindingCount);
                ValidatePrefabAssets(table, builder, ref failureCount, ref validatedBindingCount);
                ValidateItemDataAssets(table, builder, ref failureCount, ref validatedBindingCount);
            }

            builder.Append("Audio event table owners: ").Append(table.OwnerCount).AppendLine();
            builder.Append("Resolvable audio event IDs: ").Append(table.ResolvableIdCount).AppendLine();
            builder.Append("Validated authored bindings: ").Append(validatedBindingCount).AppendLine();
            builder.Append("Failures: ").Append(failureCount).AppendLine();

            report = builder.ToString();
            return failureCount == 0;
        }

        private static AudioEventTableSnapshot BuildAudioEventTableSnapshot(StringBuilder builder, ref int failureCount)
        {
            AudioEventTableSnapshot table = default;
            int activeSceneOwnerCount = 0;

            int sceneCount = EditorSceneManager.sceneCount;
            for (int sceneIndex = 0; sceneIndex < sceneCount; sceneIndex++)
            {
                Scene scene = EditorSceneManager.GetSceneAt(sceneIndex);
                if (!scene.isLoaded)
                    continue;

                GameObject[] roots = scene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                    AddSpatialAudioManagersFromHierarchy(roots[i], ResolveScenePath(scene), true, ref activeSceneOwnerCount, ref table, builder, ref failureCount);
            }

            if (activeSceneOwnerCount > 1)
                AppendFailure(builder, ref failureCount, "Duplicate SpatialAudioManager owners in open scenes: " + activeSceneOwnerCount);

            if (!table.HasTable)
            {
                int prefabOwnerCount = 0;
                string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { ProjectRoot });
                for (int i = 0; i < prefabGuids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null)
                        continue;

                    AddSpatialAudioManagersFromHierarchy(prefab, path, true, ref prefabOwnerCount, ref table, builder, ref failureCount);
                }

                if (prefabOwnerCount > 1)
                    AppendFailure(builder, ref failureCount, "Duplicate SpatialAudioManager owners in project prefabs: " + prefabOwnerCount);
            }

            if (!table.HasTable)
                AppendFailure(builder, ref failureCount, "No SpatialAudioManager audio event table found in open scenes or project prefabs.");

            return table;
        }

        private static void AddSpatialAudioManagersFromHierarchy(
            GameObject root,
            string ownerPath,
            bool countOwner,
            ref int ownerCount,
            ref AudioEventTableSnapshot table,
            StringBuilder builder,
            ref int failureCount)
        {
            if (root == null)
                return;

            ComponentBuffer.Clear();
            root.GetComponentsInChildren(true, ComponentBuffer);
            for (int i = 0; i < ComponentBuffer.Count; i++)
            {
                SpatialAudioManager manager = ComponentBuffer[i] as SpatialAudioManager;
                if (manager == null)
                    continue;

                if (countOwner)
                    ownerCount++;

                AddSpatialAudioTable(manager, ownerPath + "/" + manager.name, ref table, builder, ref failureCount);
            }

            ComponentBuffer.Clear();
        }

        private static void AddSpatialAudioTable(
            SpatialAudioManager manager,
            string ownerPath,
            ref AudioEventTableSnapshot table,
            StringBuilder builder,
            ref int failureCount)
        {
            SerializedObject serialized = new SerializedObject(manager);
            SerializedProperty clipTable = serialized.FindProperty(SpatialAudioClipTableProperty);
            if (clipTable == null || !clipTable.isArray)
            {
                AppendFailure(builder, ref failureCount, ownerPath + " has no serialized " + SpatialAudioClipTableProperty + " array.");
                return;
            }

            table.RegisterOwner();
            if (clipTable.arraySize <= 0)
            {
                AppendFailure(builder, ref failureCount, ownerPath + " has an empty audio event table.");
                return;
            }

            HashSet<string> clipGuidsInTable = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < clipTable.arraySize; i++)
            {
                SerializedProperty element = clipTable.GetArrayElementAtIndex(i);
                AudioClip clip = element != null ? element.objectReferenceValue as AudioClip : null;
                int eventId = i + 1;
                table.RegisterSlot(eventId, clip != null);
                if (clip == null)
                {
                    AppendFailure(builder, ref failureCount, ownerPath + " has a null clip at AudioEventId " + eventId + ".");
                    continue;
                }

                string assetPath = AssetDatabase.GetAssetPath(clip);
                string guid = string.IsNullOrEmpty(assetPath) ? string.Empty : AssetDatabase.AssetPathToGUID(assetPath);
                if (!string.IsNullOrEmpty(guid) && !clipGuidsInTable.Add(guid))
                    AppendFailure(builder, ref failureCount, ownerPath + " reuses clip " + assetPath + "; ClipHash fallback would be ambiguous.");
            }
        }

        private static void ValidateActiveScenes(
            in AudioEventTableSnapshot table,
            StringBuilder builder,
            ref int failureCount,
            ref int validatedBindingCount)
        {
            int sceneCount = EditorSceneManager.sceneCount;
            for (int sceneIndex = 0; sceneIndex < sceneCount; sceneIndex++)
            {
                Scene scene = EditorSceneManager.GetSceneAt(sceneIndex);
                if (!scene.isLoaded)
                    continue;

                GameObject[] roots = scene.GetRootGameObjects();
                string scenePath = ResolveScenePath(scene);
                for (int i = 0; i < roots.Length; i++)
                    ValidateGameObjectHierarchy(roots[i], scenePath, table, builder, ref failureCount, ref validatedBindingCount);
            }
        }

        private static void ValidatePrefabAssets(
            in AudioEventTableSnapshot table,
            StringBuilder builder,
            ref int failureCount,
            ref int validatedBindingCount)
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { ProjectRoot });
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                ValidateGameObjectHierarchy(prefab, path, table, builder, ref failureCount, ref validatedBindingCount);
            }
        }

        private static void ValidateSceneAssetFiles(
            in AudioEventTableSnapshot table,
            StringBuilder builder,
            ref int failureCount,
            ref int validatedBindingCount)
        {
            HashSet<string> loadedScenePaths = BuildLoadedScenePathSet();
            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { ProjectRoot });
            for (int i = 0; i < sceneGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(sceneGuids[i]).Replace('\\', '/');
                if (loadedScenePaths.Contains(path))
                    continue;

                string serializedScene = ReadTextAsset(path);
                if (string.IsNullOrEmpty(serializedScene))
                    continue;

                ValidateSerializedAudioEventIdsInText(path, serializedScene, table, builder, ref failureCount, ref validatedBindingCount);
            }
        }

        private static void ValidateItemDataAssets(
            in AudioEventTableSnapshot table,
            StringBuilder builder,
            ref int failureCount,
            ref int validatedBindingCount)
        {
            string[] itemDataGuids = AssetDatabase.FindAssets("t:ItemData", new[] { ProjectRoot });
            for (int i = 0; i < itemDataGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(itemDataGuids[i]);
                ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
                if (item == null)
                    continue;

                ValidateSerializedAudioEventIds(item, path, table, builder, ref failureCount, ref validatedBindingCount);
            }
        }

        private static void ValidateGameObjectHierarchy(
            GameObject root,
            string ownerPath,
            in AudioEventTableSnapshot table,
            StringBuilder builder,
            ref int failureCount,
            ref int validatedBindingCount)
        {
            if (root == null)
                return;

            ComponentBuffer.Clear();
            root.GetComponentsInChildren(true, ComponentBuffer);
            for (int i = 0; i < ComponentBuffer.Count; i++)
            {
                Component component = ComponentBuffer[i];
                if (component == null)
                    continue;

                ValidateSerializedAudioEventIds(component, ownerPath + "/" + component.name + ":" + component.GetType().Name, table, builder, ref failureCount, ref validatedBindingCount);
            }

            ComponentBuffer.Clear();
        }

        private static void ValidateSerializedAudioEventIds(
            UnityEngine.Object owner,
            string ownerPath,
            in AudioEventTableSnapshot table,
            StringBuilder builder,
            ref int failureCount,
            ref int validatedBindingCount)
        {
            SerializedObject serialized = new SerializedObject(owner);
            SerializedProperty property = serialized.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (!IsAudioEventIdProperty(property))
                    continue;

                if (property.propertyType != SerializedPropertyType.Integer)
                    continue;

                long rawEventId = property.longValue;
                if (rawEventId <= 0L)
                    continue;

                ValidateAudioEventIdValue(ownerPath + "." + property.name, rawEventId, table, builder, ref failureCount, ref validatedBindingCount);
            }
        }

        private static void ValidateSerializedAudioEventIdsInText(
            string ownerPath,
            string serializedText,
            in AudioEventTableSnapshot table,
            StringBuilder builder,
            ref int failureCount,
            ref int validatedBindingCount)
        {
            MatchCollection matches = SerializedAudioEventIdYamlRegex.Matches(serializedText);
            for (int i = 0; i < matches.Count; i++)
            {
                Match match = matches[i];
                string propertyName = match.Groups["name"].Value;
                string valueText = match.Groups["value"].Value;
                if (!long.TryParse(valueText, NumberStyles.Integer, CultureInfo.InvariantCulture, out long rawEventId))
                {
                    AppendFailure(builder, ref failureCount, ownerPath + "." + propertyName + " has an unreadable AudioEventId value: " + valueText + ".");
                    continue;
                }

                if (rawEventId <= 0L)
                    continue;

                ValidateAudioEventIdValue(ownerPath + "." + propertyName, rawEventId, table, builder, ref failureCount, ref validatedBindingCount);
            }
        }

        private static void ValidateAudioEventIdValue(
            string bindingPath,
            long rawEventId,
            in AudioEventTableSnapshot table,
            StringBuilder builder,
            ref int failureCount,
            ref int validatedBindingCount)
        {
            if (rawEventId > int.MaxValue)
            {
                AppendFailure(builder, ref failureCount, bindingPath + " is too large for the authored audio event table: " + rawEventId + ".");
                return;
            }

            int eventId = (int)rawEventId;
            validatedBindingCount++;
            if (!table.CanResolve(eventId))
                AppendFailure(builder, ref failureCount, bindingPath + " points to missing AudioEventId " + eventId + ".");
        }

        private static bool IsAudioEventIdProperty(SerializedProperty property)
        {
            string name = property.name;
            return name.EndsWith("AudioEventId", StringComparison.Ordinal) ||
                   name.EndsWith("AudioEventID", StringComparison.Ordinal);
        }

        private static HashSet<string> BuildLoadedScenePathSet()
        {
            HashSet<string> loadedScenePaths = new HashSet<string>(StringComparer.Ordinal);
            int sceneCount = EditorSceneManager.sceneCount;
            for (int sceneIndex = 0; sceneIndex < sceneCount; sceneIndex++)
            {
                Scene scene = EditorSceneManager.GetSceneAt(sceneIndex);
                if (!scene.isLoaded || string.IsNullOrEmpty(scene.path))
                    continue;

                loadedScenePaths.Add(scene.path.Replace('\\', '/'));
            }

            return loadedScenePaths;
        }

        private static string ResolveScenePath(Scene scene)
        {
            return string.IsNullOrEmpty(scene.path) ? scene.name : scene.path;
        }

        private static string ReadTextAsset(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return string.Empty;

            return File.ReadAllText(path);
        }

        private static void AppendFailure(StringBuilder builder, ref int failureCount, string message)
        {
            failureCount++;
            builder.Append("FAIL: ").AppendLine(message);
        }

        private struct AudioEventTableSnapshot
        {
            private bool[] _resolvableIds;

            public bool HasTable { get; private set; }
            public int OwnerCount { get; private set; }
            public int ResolvableIdCount { get; private set; }

            public void RegisterOwner()
            {
                HasTable = true;
                OwnerCount++;
            }

            public void RegisterSlot(int eventId, bool hasClip)
            {
                EnsureCapacity(eventId);
                if (hasClip && !_resolvableIds[eventId])
                {
                    _resolvableIds[eventId] = true;
                    ResolvableIdCount++;
                }
            }

            public bool CanResolve(int eventId)
            {
                return eventId > 0 &&
                       _resolvableIds != null &&
                       eventId < _resolvableIds.Length &&
                       _resolvableIds[eventId];
            }

            private void EnsureCapacity(int eventId)
            {
                int requiredLength = eventId + 1;
                if (_resolvableIds == null)
                {
                    _resolvableIds = new bool[Math.Max(requiredLength, 8)];
                    return;
                }

                if (_resolvableIds.Length >= requiredLength)
                    return;

                int capacity = _resolvableIds.Length;
                while (capacity < requiredLength)
                    capacity *= 2;

                Array.Resize(ref _resolvableIds, capacity);
            }
        }
    }
}
#endif
