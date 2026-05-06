#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Hecton.Localization;
using Hecton8.AI;
using Hecton8.Building;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Scavenging;
using Hecton8.World;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.Validation
{
    /// <summary>
    /// Performs cold-path content sanity validation for authored data assets and their referenced prefabs.
    /// Runtime gameplay code is not touched by this validator.
    /// </summary>
    internal static class ContentSanityValidator
    {
        private const string MenuPath = "Hecton-8/Validate Content";
        private const string DataRoot = "Assets/_Project/Data";
        private const string GeneratedRoot = DataRoot + "/Diagnostics/Generated/ContentSanity";
        private const string GeneratedMeshPath = GeneratedRoot + "/MESH_ContentSanityWireCube.asset";
        private const string GeneratedMaterialPath = GeneratedRoot + "/MAT_ContentSanityWireframe.mat";
        private const string FloraProxyFolder = GeneratedRoot + "/FloraGhostProxies";
        private const string InjectedProxyName = "__ContentSanityWireProxy";
        private static readonly string[] DataRoots = { DataRoot };

        private sealed class ValidationResult
        {
            public readonly Dictionary<uint, string> HashOwners = new Dictionary<uint, string>(256);
            public readonly List<string> Errors = new List<string>(128);
            public readonly List<string> Warnings = new List<string>(128);
            public readonly List<string> AutoFixes = new List<string>(128);
            public readonly HashSet<string> ProcessedPrefabPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            public int DataPrefabCount;
            public int ReferencedPrefabCount;
            public int ItemCount;
            public int FloraCount;
            public int FaunaCount;
            public int ResourceNodeCount;
            public int BaseModuleCount;
            public int InjectedProxyCount;
            public int GeneratedFloraProxyCount;
            public int MeshColliderViolationCount;
            public int HashCollisionCount;
            public int AudioMaterialViolationCount;
        }

        [MenuItem(MenuPath, priority = 141)]
        private static void ValidateContent()
        {
            ValidationResult result = new ValidationResult();
            EnsureFolder(DataRoot);
            EnsureFolder(GeneratedRoot);
            EnsureFolder(FloraProxyFolder);

            Mesh wireMesh = EnsureWireCubeMesh();
            Material wireMaterial = EnsureWireframeMaterial();

            ScanDataFolderPrefabs(result, wireMesh, wireMaterial);
            ValidateItemTemplates(result, wireMesh, wireMaterial);
            ValidateFloraTemplates(result, wireMesh, wireMaterial);
            ValidateFaunaTemplates(result, wireMesh, wireMaterial);
            ValidateResourceNodeTemplates(result);
            ValidateBaseModuleTemplates(result);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EmitSummary(result);
        }

        private static void ScanDataFolderPrefabs(ValidationResult result, Mesh wireMesh, Material wireMaterial)
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", DataRoots);
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (string.IsNullOrWhiteSpace(prefabPath))
                    continue;

                result.DataPrefabCount++;
                ValidatePrefabAsset(
                    prefabPath,
                    "data prefab",
                    result,
                    wireMesh,
                    wireMaterial,
                    allowMeshCollider: false);
            }
        }

        private static void ValidateItemTemplates(ValidationResult result, Mesh wireMesh, Material wireMaterial)
        {
            string[] itemGuids = AssetDatabase.FindAssets("t:ItemData", DataRoots);
            for (int i = 0; i < itemGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(itemGuids[i]);
                ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(assetPath);
                if (item == null)
                    continue;

                result.ItemCount++;
                string persistentId = item.PersistentId ?? string.Empty;
                int hashId = string.IsNullOrWhiteSpace(persistentId) ? 0 : LocHash.Compute(persistentId);
                RegisterHash(result, unchecked((uint)hashId), $"ItemData:{persistentId}", assetPath);

                ValidateItemAudioMaterial(result, item, assetPath);

                if (item.worldPrefab == null)
                    continue;

                string prefabPath = AssetDatabase.GetAssetPath(item.worldPrefab);
                if (string.IsNullOrWhiteSpace(prefabPath))
                {
                    result.Errors.Add($"{assetPath}: ItemData.worldPrefab has no valid asset path.");
                    continue;
                }

                result.ReferencedPrefabCount++;
                ValidatePrefabAsset(
                    prefabPath,
                    $"ItemData worldPrefab <- {assetPath}",
                    result,
                    wireMesh,
                    wireMaterial,
                    allowMeshCollider: false);
            }
        }

        private static void ValidateFloraTemplates(ValidationResult result, Mesh wireMesh, Material wireMaterial)
        {
            string[] floraGuids = AssetDatabase.FindAssets("t:FloraDataTemplate", DataRoots);
            for (int i = 0; i < floraGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(floraGuids[i]);
                FloraDataTemplate template = AssetDatabase.LoadAssetAtPath<FloraDataTemplate>(assetPath);
                if (template == null)
                    continue;

                result.FloraCount++;
                int hashId = string.IsNullOrWhiteSpace(template.StableId) ? 0 : LocHash.Compute(template.StableId);
                RegisterHash(result, unchecked((uint)hashId), $"FloraDataTemplate:{template.StableId}", assetPath);

                if (template.AudioMaterialID == (byte)FloraDataTemplate.AudioMaterialId.None)
                {
                    result.AudioMaterialViolationCount++;
                    result.Errors.Add($"{assetPath}: FloraDataTemplate.AudioMaterialID is None (0).");
                }

                if (template.Mesh == null)
                {
                    if (template.ProxyPrefab == null)
                    {
                        GameObject generatedProxy = CreateOrUpdateFloraGhostProxy(template, wireMesh, wireMaterial, assetPath);
                        if (generatedProxy != null)
                        {
                            SerializedObject serializedTemplate = new SerializedObject(template);
                            SerializedProperty proxyPrefabProperty = serializedTemplate.FindProperty("proxyPrefab");
                            if (proxyPrefabProperty != null && proxyPrefabProperty.objectReferenceValue != generatedProxy)
                            {
                                proxyPrefabProperty.objectReferenceValue = generatedProxy;
                                serializedTemplate.ApplyModifiedPropertiesWithoutUndo();
                                EditorUtility.SetDirty(template);
                                result.GeneratedFloraProxyCount++;
                                result.AutoFixes.Add($"{assetPath}: assigned generated flora ghost proxy '{AssetDatabase.GetAssetPath(generatedProxy)}'.");
                            }
                        }
                        else
                        {
                            result.Errors.Add($"{assetPath}: missing Mesh and failed to generate flora ghost proxy.");
                        }
                    }

                    if (template.ProxyPrefab != null)
                    {
                        string proxyPath = AssetDatabase.GetAssetPath(template.ProxyPrefab);
                        if (string.IsNullOrWhiteSpace(proxyPath))
                        {
                            result.Errors.Add($"{assetPath}: FloraDataTemplate.proxyPrefab has no valid asset path.");
                        }
                        else
                        {
                            result.ReferencedPrefabCount++;
                            ValidatePrefabAsset(
                                proxyPath,
                                $"FloraDataTemplate proxyPrefab <- {assetPath}",
                                result,
                                wireMesh,
                                wireMaterial,
                                allowMeshCollider: false);
                        }
                    }
                }
                else if (template.ProxyPrefab != null)
                {
                    string proxyPath = AssetDatabase.GetAssetPath(template.ProxyPrefab);
                    if (!string.IsNullOrWhiteSpace(proxyPath))
                    {
                        result.ReferencedPrefabCount++;
                        ValidatePrefabAsset(
                            proxyPath,
                            $"FloraDataTemplate proxyPrefab <- {assetPath}",
                            result,
                            wireMesh,
                            wireMaterial,
                            allowMeshCollider: false);
                    }
                }
            }
        }

        private static void ValidateFaunaTemplates(ValidationResult result, Mesh wireMesh, Material wireMaterial)
        {
            string[] faunaTemplateGuids = AssetDatabase.FindAssets("t:FaunaDataTemplate", DataRoots);
            for (int i = 0; i < faunaTemplateGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(faunaTemplateGuids[i]);
                FaunaDataTemplate template = AssetDatabase.LoadAssetAtPath<FaunaDataTemplate>(assetPath);
                if (template == null)
                    continue;

                result.FaunaCount++;
                RegisterHash(result, unchecked((uint)template.SpeciesId), $"FaunaDataTemplate:{template.SpeciesId}", assetPath);

                if (template.SpeciesId <= 0)
                    result.Errors.Add($"{assetPath}: FaunaDataTemplate.SpeciesId is not authored.");
            }

            string[] archetypeGuids = AssetDatabase.FindAssets("t:CreatureArchetypeData", DataRoots);
            for (int i = 0; i < archetypeGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(archetypeGuids[i]);
                CreatureArchetypeData archetype = AssetDatabase.LoadAssetAtPath<CreatureArchetypeData>(assetPath);
                if (archetype == null)
                    continue;

                if (archetype.prefab == null)
                {
                    result.Warnings.Add($"{assetPath}: CreatureArchetypeData.prefab is unassigned.");
                    continue;
                }

                string prefabPath = AssetDatabase.GetAssetPath(archetype.prefab);
                if (string.IsNullOrWhiteSpace(prefabPath))
                {
                    result.Errors.Add($"{assetPath}: CreatureArchetypeData.prefab has no valid asset path.");
                    continue;
                }

                result.ReferencedPrefabCount++;
                ValidatePrefabAsset(
                    prefabPath,
                    $"CreatureArchetypeData prefab <- {assetPath}",
                    result,
                    wireMesh,
                    wireMaterial,
                    allowMeshCollider: false);
            }
        }

        private static void ValidateResourceNodeTemplates(ValidationResult result)
        {
            string[] resourceGuids = AssetDatabase.FindAssets("t:ResourceNodeTemplate", DataRoots);
            for (int i = 0; i < resourceGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(resourceGuids[i]);
                ResourceNodeTemplate template = AssetDatabase.LoadAssetAtPath<ResourceNodeTemplate>(assetPath);
                if (template == null)
                    continue;

                result.ResourceNodeCount++;
                if (template.StableHashId == 0)
                    result.Errors.Add($"{assetPath}: ResourceNodeTemplate.StableHashId resolves to 0.");

                if (template.NodeMesh == null)
                    result.Warnings.Add($"{assetPath}: nodeMesh is null. Runtime ghost-box standard remains active.");
            }
        }

        private static void ValidateBaseModuleTemplates(ValidationResult result)
        {
            string[] moduleGuids = AssetDatabase.FindAssets("t:BaseModuleTemplate", DataRoots);
            for (int i = 0; i < moduleGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(moduleGuids[i]);
                BaseModuleTemplate template = AssetDatabase.LoadAssetAtPath<BaseModuleTemplate>(assetPath);
                if (template == null)
                    continue;

                result.BaseModuleCount++;
                if (template.TemplateHashId == 0)
                    result.Errors.Add($"{assetPath}: BaseModuleTemplate.TemplateHashId resolves to 0.");

                Vector3 proxyBoundsSize = template.ProxyBoundsSize;
                if (proxyBoundsSize.x <= 0.01f || proxyBoundsSize.y <= 0.01f || proxyBoundsSize.z <= 0.01f)
                    result.Errors.Add($"{assetPath}: BaseModuleTemplate.ProxyBoundsSize is degenerate.");
            }
        }

        private static void ValidateItemAudioMaterial(ValidationResult result, ItemData item, string assetPath)
        {
            SerializedObject serializedItem = new SerializedObject(item);
            SerializedProperty autoResolveProperty = serializedItem.FindProperty("autoResolvePhysicalMetadata");
            SerializedProperty audioMaterialProperty = serializedItem.FindProperty("audioMaterialId");
            bool autoResolve = autoResolveProperty != null && autoResolveProperty.boolValue;
            int serializedAudioMaterial = audioMaterialProperty != null ? audioMaterialProperty.intValue : -1;

            if (!autoResolve && !Enum.IsDefined(typeof(ItemAudioMaterialId), serializedAudioMaterial))
            {
                result.AudioMaterialViolationCount++;
                result.Errors.Add($"{assetPath}: serialized ItemData.audioMaterialId value '{serializedAudioMaterial}' is invalid.");
                return;
            }

            ItemAudioMaterialId resolvedDefault = ItemPhysicalMetadataUtility.ResolveDefaultAudioMaterialId(
                item.category,
                item.resourceFamily,
                item.PersistentId);

            if (!autoResolve &&
                audioMaterialProperty != null &&
                serializedAudioMaterial == (int)ItemAudioMaterialId.Organic &&
                resolvedDefault != ItemAudioMaterialId.Organic)
            {
                result.AudioMaterialViolationCount++;
                result.Errors.Add(
                    $"{assetPath}: AudioMaterialID is Organic while classification resolves to {resolvedDefault}. " +
                    "Likely stale or missing explicit audio-material authoring.");
            }
        }

        private static void RegisterHash(ValidationResult result, uint hash, string ownerLabel, string assetPath)
        {
            if (hash == 0u)
            {
                result.Errors.Add($"{assetPath}: authored hash resolves to 0 for '{ownerLabel}'.");
                return;
            }

            if (result.HashOwners.TryGetValue(hash, out string existingOwner))
            {
                result.HashCollisionCount++;
                result.Errors.Add(
                    $"{assetPath}: HASH COLLISION 0x{hash:X8} between '{existingOwner}' and '{ownerLabel}'.");
                return;
            }

            result.HashOwners.Add(hash, ownerLabel);
        }

        private static void ValidatePrefabAsset(
            string prefabPath,
            string context,
            ValidationResult result,
            Mesh wireMesh,
            Material wireMaterial,
            bool allowMeshCollider)
        {
            if (string.IsNullOrWhiteSpace(prefabPath) || !result.ProcessedPrefabPaths.Add(prefabPath))
                return;

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot == null)
            {
                result.Errors.Add($"{prefabPath}: failed to load prefab contents for {context}.");
                return;
            }

            bool changed = false;
            try
            {
                MeshCollider[] meshColliders = prefabRoot.GetComponentsInChildren<MeshCollider>(true);
                if (!allowMeshCollider && meshColliders != null && meshColliders.Length > 0)
                {
                    result.MeshColliderViolationCount += meshColliders.Length;
                    for (int i = 0; i < meshColliders.Length; i++)
                    {
                        MeshCollider meshCollider = meshColliders[i];
                        if (meshCollider == null)
                            continue;

                        result.Errors.Add(
                            $"{prefabPath}: MeshCollider is forbidden for {context} -> {BuildTransformPath(meshCollider.transform)}");
                    }
                }

                if (!HasRenderableMesh(prefabRoot))
                {
                    Vector3 center;
                    Vector3 size;
                    ResolveLocalBounds(prefabRoot, out center, out size);
                    if (EnsureWireframeProxy(prefabRoot, center, size, wireMesh, wireMaterial))
                    {
                        changed = true;
                        result.InjectedProxyCount++;
                        result.AutoFixes.Add($"{prefabPath}: injected wireframe proxy for {context}.");
                    }
                }

                if (changed)
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static bool HasRenderableMesh(GameObject root)
        {
            MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < meshFilters.Length; i++)
            {
                MeshFilter meshFilter = meshFilters[i];
                if (meshFilter != null && meshFilter.sharedMesh != null)
                    return true;
            }

            SkinnedMeshRenderer[] skinnedRenderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skinnedRenderers.Length; i++)
            {
                SkinnedMeshRenderer renderer = skinnedRenderers[i];
                if (renderer != null && renderer.sharedMesh != null)
                    return true;
            }

            return false;
        }

        private static bool EnsureWireframeProxy(GameObject root, Vector3 localCenter, Vector3 localSize, Mesh wireMesh, Material wireMaterial)
        {
            Transform existingProxy = FindChildRecursive(root.transform, InjectedProxyName);
            GameObject proxyObject = existingProxy != null
                ? existingProxy.gameObject
                : new GameObject(InjectedProxyName);

            bool changed = false;
            if (existingProxy == null)
            {
                proxyObject.transform.SetParent(root.transform, false);
                changed = true;
            }

            Vector3 sanitizedSize = SanitizeSize(localSize);
            if (proxyObject.transform.localPosition != localCenter)
            {
                proxyObject.transform.localPosition = localCenter;
                changed = true;
            }

            if (proxyObject.transform.localRotation != Quaternion.identity)
            {
                proxyObject.transform.localRotation = Quaternion.identity;
                changed = true;
            }

            if (proxyObject.transform.localScale != sanitizedSize)
            {
                proxyObject.transform.localScale = sanitizedSize;
                changed = true;
            }

            MeshFilter meshFilter = proxyObject.GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = proxyObject.AddComponent<MeshFilter>();
                changed = true;
            }

            if (meshFilter.sharedMesh != wireMesh)
            {
                meshFilter.sharedMesh = wireMesh;
                changed = true;
            }

            MeshRenderer meshRenderer = proxyObject.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = proxyObject.AddComponent<MeshRenderer>();
                changed = true;
            }

            if (meshRenderer.sharedMaterial != wireMaterial)
            {
                meshRenderer.sharedMaterial = wireMaterial;
                changed = true;
            }

            if (meshRenderer.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off)
            {
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                changed = true;
            }

            if (meshRenderer.receiveShadows)
            {
                meshRenderer.receiveShadows = false;
                changed = true;
            }

            return changed;
        }

        private static GameObject CreateOrUpdateFloraGhostProxy(
            FloraDataTemplate template,
            Mesh wireMesh,
            Material wireMaterial,
            string ownerPath)
        {
            EnsureFolder(FloraProxyFolder);
            string prefabName = $"PFB_{SanitizeToken(template.name)}_GhostProxy.prefab";
            string prefabPath = $"{FloraProxyFolder}/{prefabName}";

            GameObject root = new GameObject(Path.GetFileNameWithoutExtension(prefabName));
            try
            {
                ConfigureFloraGhostCapsuleCollider(root, template);

                EnsureWireframeProxy(root, template.BoundingBoxCenter, template.BoundingBoxSize, wireMesh, wireMaterial);

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                if (prefab == null)
                    Debug.LogError($"[ContentSanityValidator] Failed to save flora ghost proxy for '{ownerPath}'.");

                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Vector3 SanitizeSize(Vector3 value)
        {
            return new Vector3(
                Mathf.Max(0.1f, Mathf.Abs(value.x)),
                Mathf.Max(0.1f, Mathf.Abs(value.y)),
                Mathf.Max(0.1f, Mathf.Abs(value.z)));
        }

        private static void ConfigureFloraGhostCapsuleCollider(GameObject root, FloraDataTemplate template)
        {
            Vector3 size = SanitizeSize(template.BoundingBoxSize);
            Vector3 extents = size * 0.5f;
            int axis = ResolveFloraGhostCapsuleAxis(template.Category, template.ProxyShapeType, extents);
            int secondaryA = (axis + 1) % 3;
            int secondaryB = (axis + 2) % 3;
            float secondaryMin = Mathf.Min(GetAxis(size, secondaryA), GetAxis(size, secondaryB));
            float radius = Mathf.Max(0.05f, secondaryMin * 0.5f);

            CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
            collider.center = template.BoundingBoxCenter;
            collider.direction = axis;
            collider.radius = radius;
            collider.height = Mathf.Max(radius * 2f, GetAxis(size, axis));
        }

        private static int ResolveFloraGhostCapsuleAxis(
            FloraDataTemplate.FloraCategory category,
            FloraDataTemplate.ProxyShape proxyShape,
            Vector3 extents)
        {
            if (category == FloraDataTemplate.FloraCategory.HarvestableKelp ||
                category == FloraDataTemplate.FloraCategory.GiantSargassum)
            {
                return 1;
            }

            if (category == FloraDataTemplate.FloraCategory.HardCoral ||
                proxyShape == FloraDataTemplate.ProxyShape.Fan ||
                proxyShape == FloraDataTemplate.ProxyShape.SphereCluster)
            {
                return extents.x >= extents.z ? 0 : 2;
            }

            if (extents.y >= extents.x && extents.y >= extents.z)
                return 1;

            return extents.x >= extents.z ? 0 : 2;
        }

        private static float GetAxis(Vector3 value, int axis)
        {
            return axis == 0 ? value.x : (axis == 1 ? value.y : value.z);
        }

        private static void ResolveLocalBounds(GameObject root, out Vector3 localCenter, out Vector3 localSize)
        {
            bool hasBounds = false;
            Bounds combinedBounds = default;

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null)
                    continue;

                EncapsulateWorldBounds(root.transform, collider.bounds, ref hasBounds, ref combinedBounds);
            }

            if (!hasBounds)
            {
                Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer renderer = renderers[i];
                    if (renderer == null)
                        continue;

                    EncapsulateWorldBounds(root.transform, renderer.bounds, ref hasBounds, ref combinedBounds);
                }
            }

            if (!hasBounds)
            {
                localCenter = Vector3.zero;
                localSize = Vector3.one;
                return;
            }

            localCenter = combinedBounds.center;
            localSize = SanitizeSize(combinedBounds.size);
        }

        private static void EncapsulateWorldBounds(
            Transform root,
            Bounds worldBounds,
            ref bool hasBounds,
            ref Bounds combinedBounds)
        {
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;
            Vector3[] corners =
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z)
            };

            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 localPoint = root.InverseTransformPoint(corners[i]);
                if (!hasBounds)
                {
                    combinedBounds = new Bounds(localPoint, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(localPoint);
                }
            }
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root.name == childName)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = FindChildRecursive(root.GetChild(i), childName);
                if (child != null)
                    return child;
            }

            return null;
        }

        private static string BuildTransformPath(Transform target)
        {
            if (target == null)
                return "<null>";

            string path = target.name;
            Transform cursor = target.parent;
            while (cursor != null)
            {
                path = cursor.name + "/" + path;
                cursor = cursor.parent;
            }

            return path;
        }

        private static Mesh EnsureWireCubeMesh()
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(GeneratedMeshPath);
            if (mesh != null)
                return mesh;

            mesh = new Mesh
            {
                name = "MESH_ContentSanityWireCube"
            };

            Vector3[] vertices =
            {
                new Vector3(-0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, -0.5f, 0.5f),
                new Vector3(-0.5f, -0.5f, 0.5f),
                new Vector3(-0.5f, 0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, 0.5f),
                new Vector3(-0.5f, 0.5f, 0.5f)
            };

            int[] indices =
            {
                0, 1, 1, 2, 2, 3, 3, 0,
                4, 5, 5, 6, 6, 7, 7, 4,
                0, 4, 1, 5, 2, 6, 3, 7
            };

            mesh.SetVertices(vertices);
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one);
            mesh.UploadMeshData(false);
            AssetDatabase.CreateAsset(mesh, GeneratedMeshPath);
            return mesh;
        }

        private static Material EnsureWireframeMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(GeneratedMaterialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                    shader = Shader.Find("Standard");

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, GeneratedMaterialPath);
            }

            Color color = new Color(1f, 0.15f, 0.15f, 1f);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 0f);
            if (material.HasProperty("_Blend"))
                material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_ZWrite"))
                material.SetFloat("_ZWrite", 1f);

            EditorUtility.SetDirty(material);
            return material;
        }

        private static string SanitizeToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Unnamed";

            char[] chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char current = chars[i];
                if (char.IsLetterOrDigit(current) || current == '_')
                    continue;

                chars[i] = '_';
            }

            return new string(chars);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int slashIndex = path.LastIndexOf('/');
            if (slashIndex <= 0)
                return;

            string parent = path.Substring(0, slashIndex);
            string folderName = path.Substring(slashIndex + 1);
            EnsureFolder(parent);

            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, folderName);
        }

        private static void EmitSummary(ValidationResult result)
        {
            string summary =
                $"[ContentSanityValidator] DataPrefabs={result.DataPrefabCount}, " +
                $"ReferencedPrefabs={result.ReferencedPrefabCount}, " +
                $"Items={result.ItemCount}, Flora={result.FloraCount}, Fauna={result.FaunaCount}, " +
                $"ResourceNodes={result.ResourceNodeCount}, BaseModules={result.BaseModuleCount}, " +
                $"InjectedProxyCount={result.InjectedProxyCount}, GeneratedFloraProxyCount={result.GeneratedFloraProxyCount}, " +
                $"MeshColliderViolations={result.MeshColliderViolationCount}, HashCollisions={result.HashCollisionCount}, " +
                $"AudioMaterialViolations={result.AudioMaterialViolationCount}, Errors={result.Errors.Count}, Warnings={result.Warnings.Count}.";

            if (result.Errors.Count > 0)
            {
                Debug.LogError(summary);
                for (int i = 0; i < result.Errors.Count; i++)
                    Debug.LogError("[ContentSanityValidator] " + result.Errors[i]);
            }
            else
            {
                Debug.Log(summary);
            }

            for (int i = 0; i < result.Warnings.Count; i++)
                Debug.LogWarning("[ContentSanityValidator] " + result.Warnings[i]);

            for (int i = 0; i < result.AutoFixes.Count; i++)
                Debug.Log("[ContentSanityValidator] FIX " + result.AutoFixes[i]);
        }
    }
}
#endif
