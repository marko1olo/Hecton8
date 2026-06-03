#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Hecton8.Building;
using Hecton8.Construction;
using Hecton8.Gameplay;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.Structures
{
    public struct StationModuleAnalysis
    {
        public string Name;
        public string Path;
        public int VertexCount;
        public int TriangleCount;
        public int SocketCount;
        public uint SocketMask;
        public Bounds Bounds;
    }

    public sealed class StationModuleLibrary : IDisposable
    {
        public NativeArray<StationModuleRuleDTO> Rules;
        public NativeArray<StationSocketDTO> Sockets;
        public NativeArray<StationMeshSliceDTO> MeshSlices;
        public NativeArray<StationMeshVertexDTO> Vertices;
        public NativeArray<StationTriangleDTO> Triangles;
        public StationModuleAnalysis[] Analyses;
        public string[] ModuleNames;
        public string[] ModulePaths;
        public Material[] Materials;
        public Material PrimaryMaterial;
        public Bounds CombinedBounds;
        public int MaxVerticesPerModule;
        public int MaxTrianglesPerModule;

        public int ModuleCount => Rules.IsCreated ? Rules.Length : 0;

        public void Dispose()
        {
            if (Rules.IsCreated)
                Rules.Dispose();
            if (Sockets.IsCreated)
                Sockets.Dispose();
            if (MeshSlices.IsCreated)
                MeshSlices.Dispose();
            if (Vertices.IsCreated)
                Vertices.Dispose();
            if (Triangles.IsCreated)
                Triangles.Dispose();
        }
    }

    public static class DeepReachStationModuleLibraryBuilder
    {
        public const string DefaultPrefabFolder = "Assets/_Project/Prefabs/Construction/Final";
        private const float SocketCapNormalDotThreshold = 0.72f;
        private const float SocketFaceToleranceMeters = 0.03f;
        private const float SocketFaceToleranceExtentScale = 0.035f;
        private const float SocketWindowExtentScale = 0.38f;
        private const float SocketWindowMinRadius = 0.75f;
        private const float SocketWindowMaxRadius = 3f;

        private static readonly int[] s_directionOrder =
        {
            DeepReachStationDirections.North,
            DeepReachStationDirections.East,
            DeepReachStationDirections.South,
            DeepReachStationDirections.West,
            DeepReachStationDirections.Top,
            DeepReachStationDirections.Bottom
        };

        public static StationModuleLibrary BuildFromConstructionPrefabs(
            string prefabFolder,
            Allocator allocator)
        {
            string folder = string.IsNullOrWhiteSpace(prefabFolder) ? DefaultPrefabFolder : prefabFolder;
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
            Array.Sort(guids, StringComparer.Ordinal);

            var rules = new List<StationModuleRuleDTO>(DeepReachStationConstants.MaxModuleRules);
            var socketContracts = new List<StationSocketDTO>(64);
            var slices = new List<StationMeshSliceDTO>(DeepReachStationConstants.MaxModuleRules);
            var vertices = new List<StationMeshVertexDTO>(4096);
            var triangles = new List<StationTriangleDTO>(4096);
            var analyses = new List<StationModuleAnalysis>(DeepReachStationConstants.MaxModuleRules);
            var names = new List<string>(DeepReachStationConstants.MaxModuleRules);
            var paths = new List<string>(DeepReachStationConstants.MaxModuleRules);
            var materials = new List<Material>(DeepReachStationConstants.MaxMaterialSlots);
            var connectorMasks = new Dictionary<string, ushort>(15, StringComparer.OrdinalIgnoreCase);
            ReserveFallbackMaterialSlot(materials);

            AppendEmptyModule(rules, slices, analyses, names, paths);

            bool hasCombinedBounds = false;
            Bounds combinedBounds = default;
            int maxVertices = 0;
            int maxTriangles = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!IsStructuralModulePath(path))
                    continue;

                if (rules.Count >= DeepReachStationConstants.MaxModuleRules)
                    throw new InvalidOperationException($"Deep Reach station structural vocabulary exceeds {DeepReachStationConstants.MaxModuleRules - 1} modules. Refusing to silently drop prefab: {path}");

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                int moduleId = rules.Count;
                StationModuleAnalysis analysis = AppendModule(prefab, path, moduleId, rules, socketContracts, slices, vertices, triangles, materials, connectorMasks);
                analyses.Add(analysis);
                names.Add(prefab.name);
                paths.Add(path);

                if (analysis.VertexCount > 0)
                {
                    if (!hasCombinedBounds)
                    {
                        combinedBounds = analysis.Bounds;
                        hasCombinedBounds = true;
                    }
                    else
                    {
                        combinedBounds.Encapsulate(analysis.Bounds);
                    }
                }

                maxVertices = Math.Max(maxVertices, analysis.VertexCount);
                maxTriangles = Math.Max(maxTriangles, analysis.TriangleCount);
            }

            if (rules.Count <= 1)
                throw new InvalidOperationException("Deep Reach station generation found no structural construction prefabs.");

            var library = new StationModuleLibrary
            {
                Rules = ToNative(rules, allocator),
                Sockets = ToNative(socketContracts, allocator),
                MeshSlices = ToNative(slices, allocator),
                Vertices = ToNative(vertices, allocator),
                Triangles = ToNative(triangles, allocator),
                Analyses = analyses.ToArray(),
                ModuleNames = names.ToArray(),
                ModulePaths = paths.ToArray(),
                Materials = materials.ToArray(),
                PrimaryMaterial = ResolvePrimaryMaterial(materials),
                CombinedBounds = hasCombinedBounds ? combinedBounds : new Bounds(Vector3.zero, Vector3.one),
                MaxVerticesPerModule = maxVertices,
                MaxTrianglesPerModule = maxTriangles
            };

            return library;
        }

        public static void AppendBoxSurrogate(
            Bounds bounds,
            ushort[] socketMasks,
            int vertexStart,
            int triangleStart,
            List<StationMeshVertexDTO> vertices,
            List<StationTriangleDTO> triangles)
        {
            AppendBoxSurrogate(bounds, socketMasks, vertexStart, triangleStart, vertices, triangles, 0);
        }

        public static void AppendBoxSurrogate(
            Bounds bounds,
            ushort[] socketMasks,
            int vertexStart,
            int triangleStart,
            List<StationMeshVertexDTO> vertices,
            List<StationTriangleDTO> triangles,
            ushort materialSlot)
        {
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            extents.x = Mathf.Max(extents.x, 0.5f);
            extents.y = Mathf.Max(extents.y, 0.5f);
            extents.z = Mathf.Max(extents.z, 0.5f);

            AppendBoxSurrogateVertex(vertices, center + new Vector3(-extents.x, -extents.y, -extents.z), center, 0);
            AppendBoxSurrogateVertex(vertices, center + new Vector3(extents.x, -extents.y, -extents.z), center, 1);
            AppendBoxSurrogateVertex(vertices, center + new Vector3(extents.x, -extents.y, extents.z), center, 2);
            AppendBoxSurrogateVertex(vertices, center + new Vector3(-extents.x, -extents.y, extents.z), center, 3);
            AppendBoxSurrogateVertex(vertices, center + new Vector3(-extents.x, extents.y, -extents.z), center, 4);
            AppendBoxSurrogateVertex(vertices, center + new Vector3(extents.x, extents.y, -extents.z), center, 5);
            AppendBoxSurrogateVertex(vertices, center + new Vector3(extents.x, extents.y, extents.z), center, 6);
            AppendBoxSurrogateVertex(vertices, center + new Vector3(-extents.x, extents.y, extents.z), center, 7);

            AppendQuad(triangles, vertexStart, 3, 2, 6, 7, DirectionCullMask(socketMasks, DeepReachStationDirections.North), triangleStart, materialSlot);
            AppendQuad(triangles, vertexStart, 1, 0, 4, 5, DirectionCullMask(socketMasks, DeepReachStationDirections.South), triangleStart + 2, materialSlot);
            AppendQuad(triangles, vertexStart, 2, 1, 5, 6, DirectionCullMask(socketMasks, DeepReachStationDirections.East), triangleStart + 4, materialSlot);
            AppendQuad(triangles, vertexStart, 0, 3, 7, 4, DirectionCullMask(socketMasks, DeepReachStationDirections.West), triangleStart + 6, materialSlot);
            AppendQuad(triangles, vertexStart, 7, 6, 5, 4, DirectionCullMask(socketMasks, DeepReachStationDirections.Top), triangleStart + 8, materialSlot);
            AppendQuad(triangles, vertexStart, 0, 1, 2, 3, DirectionCullMask(socketMasks, DeepReachStationDirections.Bottom), triangleStart + 10, materialSlot);
        }

        private static void AppendBoxSurrogateVertex(
            List<StationMeshVertexDTO> vertices,
            Vector3 point,
            Vector3 center,
            int index)
        {
            float3 position = new float3(point.x, point.y, point.z);
            float3 center3 = new float3(center.x, center.y, center.z);
            vertices.Add(new StationMeshVertexDTO
            {
                Position = position,
                Normal = math.normalizesafe(position - center3, new float3(0f, 1f, 0f)),
                Uv0 = new float2((index & 1) == 0 ? 0f : 1f, (index & 2) == 0 ? 0f : 1f),
                ColorRgba = DeepReachStationMath.EncodeColor(255, 180, 120, 255),
                Flags = 1u
            });
        }

        private static StationModuleAnalysis AppendModule(
            GameObject prefab,
            string path,
            int moduleId,
            List<StationModuleRuleDTO> rules,
            List<StationSocketDTO> socketContracts,
            List<StationMeshSliceDTO> slices,
            List<StationMeshVertexDTO> vertices,
            List<StationTriangleDTO> triangles,
            List<Material> materials,
            Dictionary<string, ushort> connectorMasks)
        {
            int vertexStart = vertices.Count;
            int triangleStart = triangles.Count;
            int socketStart = socketContracts.Count;
            Bounds localBounds = ResolveRendererBounds(prefab);
            EnsureFiniteBounds(localBounds, prefab.name);
            ushort[] sockets = ResolveSocketMasks(prefab, moduleId, localBounds, connectorMasks, socketContracts, out int socketCount, out uint socketLedgerMask);
            ExtractReadableMeshes(prefab, vertexStart, triangleStart, vertices, triangles, materials, ref localBounds, out uint materialHash);
            EnsureFiniteBounds(localBounds, prefab.name);

            int vertexCount = vertices.Count - vertexStart;
            int triangleCount = triangles.Count - triangleStart;
            if (vertexCount == 0 || triangleCount == 0)
            {
                ushort surrogateMaterialSlot = ResolveSurrogateMaterialSlot(prefab, materials, out materialHash);
                AppendBoxSurrogate(localBounds, sockets, vertexStart, triangleStart, vertices, triangles, surrogateMaterialSlot);
                vertexCount = vertices.Count - vertexStart;
                triangleCount = triangles.Count - triangleStart;
            }
            else
            {
                RefreshCullMasksForModule(triangles, triangleStart, triangleCount, vertices, vertexStart, localBounds, sockets, socketContracts, socketStart, socketCount);
            }

            StationModuleRuleDTO rule = default;
            rule.ModuleHash = HashString(prefab.name);
            rule.SocketNorth = sockets[DeepReachStationDirections.North];
            rule.SocketEast = sockets[DeepReachStationDirections.East];
            rule.SocketSouth = sockets[DeepReachStationDirections.South];
            rule.SocketWest = sockets[DeepReachStationDirections.West];
            rule.SocketTop = sockets[DeepReachStationDirections.Top];
            rule.SocketBottom = sockets[DeepReachStationDirections.Bottom];
            Vector3 extents = localBounds.extents;
            rule.BoundsExtents = new float3(extents.x, extents.y, extents.z);
            rule.Weight = ResolveModuleWeight(prefab.name, socketCount, vertexCount);
            rule.PrefabHash = HashString(path);
            rule.Flags = 1u;
            rule.ModuleId = (byte)moduleId;
            rule.DrawPriority = ResolveDrawPriority(prefab.name);
            rule.SourceSocketCount = (ushort)Mathf.Clamp(socketCount, 0, ushort.MaxValue);
            rule.SourceVertexCount = (uint)vertexCount;
            rule.SourceTriangleCount = (uint)triangleCount;
            rule.SourceSocketStart = (uint)socketStart;

            StationMeshSliceDTO slice = default;
            slice.VertexStart = vertexStart;
            slice.VertexCount = vertexCount;
            slice.TriangleStart = triangleStart;
            slice.TriangleCount = triangleCount;
            slice.MaterialHash = materialHash;
            slice.Flags = 1u;

            rules.Add(rule);
            slices.Add(slice);

            return new StationModuleAnalysis
            {
                Name = prefab.name,
                Path = path,
                VertexCount = vertexCount,
                TriangleCount = triangleCount,
                SocketCount = socketCount,
                SocketMask = socketLedgerMask,
                Bounds = localBounds
            };
        }

        private static void ExtractReadableMeshes(
            GameObject prefab,
            int moduleVertexStart,
            int moduleTriangleStart,
            List<StationMeshVertexDTO> vertices,
            List<StationTriangleDTO> triangles,
            List<Material> materials,
            ref Bounds localBounds,
            out uint primaryMaterialHash)
        {
            primaryMaterialHash = 0u;
            var filters = new List<MeshFilter>(32);
            prefab.GetComponentsInChildren(true, filters);
            var rendererMap = BuildRendererMap(prefab);
            var meshVertices = new List<Vector3>(1024);
            var meshNormals = new List<Vector3>(1024);
            var meshUvs = new List<Vector2>(1024);
            var meshIndices = new List<int>(4096);
            Matrix4x4 rootInverse = prefab.transform.worldToLocalMatrix;
            bool hasBounds = localBounds.size.sqrMagnitude > 0.0001f;

            for (int i = 0; i < filters.Count; i++)
            {
                MeshFilter filter = filters[i];
                Mesh mesh = filter.sharedMesh;
                if (mesh == null || !mesh.isReadable || IsRejectedRenderChild(filter.transform))
                    continue;

                Material[] sharedMaterials = null;
                if (rendererMap.TryGetValue(filter.transform, out MeshRenderer renderer) && renderer != null)
                    sharedMaterials = renderer.sharedMaterials;

                Matrix4x4 localToModule = rootInverse * filter.transform.localToWorldMatrix;
                int filterVertexStart = vertices.Count;
                meshVertices.Clear();
                meshNormals.Clear();
                meshUvs.Clear();
                mesh.GetVertices(meshVertices);
                mesh.GetNormals(meshNormals);
                mesh.GetUVs(0, meshUvs);
                for (int v = 0; v < meshVertices.Count; v++)
                {
                    Vector3 position = localToModule.MultiplyPoint3x4(meshVertices[v]);
                    Vector3 sourceNormal = v < meshNormals.Count ? meshNormals[v] : Vector3.up;
                    Vector3 normal = localToModule.MultiplyVector(sourceNormal).normalized;
                    Vector2 uv = v < meshUvs.Count ? meshUvs[v] : Vector2.zero;
                    vertices.Add(new StationMeshVertexDTO
                    {
                        Position = new float3(position.x, position.y, position.z),
                        Normal = normal.sqrMagnitude > 0.0001f ? new float3(normal.x, normal.y, normal.z) : new float3(0f, 1f, 0f),
                        Uv0 = new float2(uv.x, uv.y),
                        ColorRgba = DeepReachStationMath.EncodeColor(255, 128, 64, 255),
                        Flags = 1u
                    });

                    if (!hasBounds)
                    {
                        localBounds = new Bounds(position, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        localBounds.Encapsulate(position);
                    }
                }

                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    ushort materialSlot = ResolveMaterialSlot(materials, sharedMaterials, sub);
                    if (primaryMaterialHash == 0u && materialSlot > 0 && materialSlot < materials.Count)
                    {
                        Material structuralMaterial = materials[materialSlot];
                        if (structuralMaterial != null)
                            primaryMaterialHash = HashString(structuralMaterial.name);
                    }

                    meshIndices.Clear();
                    mesh.GetTriangles(meshIndices, sub, true);
                    for (int t = 0; t + 2 < meshIndices.Count; t += 3)
                    {
                        triangles.Add(new StationTriangleDTO
                        {
                            Index0 = filterVertexStart + meshIndices[t] - moduleVertexStart,
                            Index1 = filterVertexStart + meshIndices[t + 1] - moduleVertexStart,
                            Index2 = filterVertexStart + meshIndices[t + 2] - moduleVertexStart,
                            CullDirectionMask = 0,
                            SubMesh = materialSlot,
                            SourceHash = DeepReachStationMath.Hash((uint)(moduleTriangleStart + triangles.Count + t)),
                            Flags = 1u
                        });
                    }
                }
            }
        }

        private static ushort ResolveMaterialSlot(List<Material> materials, Material[] rendererMaterials, int subMesh)
        {
            ReserveFallbackMaterialSlot(materials);

            Material material = null;
            if (rendererMaterials != null && rendererMaterials.Length > 0)
                material = rendererMaterials[Mathf.Min(subMesh, rendererMaterials.Length - 1)];

            if (IsRejectedPrimaryMaterial(material))
                return 0;

            int existing = materials.IndexOf(material);
            if (existing >= 0)
                return (ushort)Mathf.Clamp(existing, 0, ushort.MaxValue);

            if (materials.Count >= DeepReachStationConstants.MaxMaterialSlots)
                throw new InvalidOperationException($"Station structural material vocabulary exceeds {DeepReachStationConstants.MaxMaterialSlots} slots.");

            materials.Add(material);
            return (ushort)(materials.Count - 1);
        }

        private static ushort ResolveSurrogateMaterialSlot(GameObject prefab, List<Material> materials, out uint primaryMaterialHash)
        {
            primaryMaterialHash = 0u;
            var renderers = new List<Renderer>(16);
            prefab.GetComponentsInChildren(true, renderers);
            for (int r = 0; r < renderers.Count; r++)
            {
                Renderer renderer = renderers[r];
                if (renderer == null || IsRejectedRenderChild(renderer.transform))
                    continue;

                Material[] sharedMaterials = renderer.sharedMaterials;
                for (int sub = 0; sub < sharedMaterials.Length; sub++)
                {
                    ushort slot = ResolveMaterialSlot(materials, sharedMaterials, sub);
                    if (slot == 0 || slot >= materials.Count)
                        continue;

                    Material structuralMaterial = materials[slot];
                    if (structuralMaterial == null)
                        continue;

                    primaryMaterialHash = HashString(structuralMaterial.name);
                    return slot;
                }
            }

            return 0;
        }

        private static void ReserveFallbackMaterialSlot(List<Material> materials)
        {
            if (materials.Count == 0)
                materials.Add(null);
        }

        private static ushort[] ResolveSocketMasks(
            GameObject prefab,
            int moduleId,
            Bounds localBounds,
            Dictionary<string, ushort> connectorMasks,
            List<StationSocketDTO> socketContracts,
            out int socketCount,
            out uint ledgerMask)
        {
            var result = new ushort[DeepReachStationConstants.DirectionCount];
            socketCount = 0;
            ledgerMask = 0u;

            if (TryResolveModuleTemplate(prefab, out BaseModuleTemplate template))
            {
                BaseModuleTemplate.SocketDefinition[] definitions = template.SocketDefinitions;
                if (definitions != null && definitions.Length > 0)
                {
                    for (int i = 0; i < definitions.Length; i++)
                    {
                        BaseModuleTemplate.SocketDefinition definition = definitions[i];
                        int direction = ConvertDirection(definition.Direction);
                        ushort connector = ConnectorMaskFromType(definition.CompatibleType, connectorMasks);
                        result[direction] |= connector;
                        ledgerMask |= connector;
                        socketContracts.Add(BuildSocketDTO(prefab, definition, moduleId, direction, connector, socketCount));
                        socketCount++;
                    }

                    return result;
                }
            }

            ModuleSocket[] sockets = prefab.GetComponentsInChildren<ModuleSocket>(true);
            for (int i = 0; i < sockets.Length; i++)
            {
                ModuleSocket socket = sockets[i];
                if (socket == null)
                    continue;

                float3 localPosition = ResolveSocketLocalPosition(prefab, socket);
                int direction = ResolveSocketDirection(socket, localPosition, localBounds);
                ushort connector = ConnectorMaskFromType(socket.CompatibleType, connectorMasks);
                result[direction] |= connector;
                ledgerMask |= connector;
                socketContracts.Add(BuildSocketDTO(prefab, socket, moduleId, direction, connector, socketCount, localPosition));
                socketCount++;
            }

            return result;
        }

        private static bool TryResolveModuleTemplate(GameObject prefab, out BaseModuleTemplate template)
        {
            template = null;
            if (prefab == null)
                return false;

            if (prefab.TryGetComponent(out ModuleMarker marker) &&
                marker.Data != null &&
                marker.Data.ModuleTemplate != null)
            {
                template = marker.Data.ModuleTemplate;
                return true;
            }

            if (prefab.TryGetComponent(out BaseModule baseModule) && baseModule.ModuleTemplate != null)
            {
                template = baseModule.ModuleTemplate;
                return true;
            }

            return false;
        }

        private static StationSocketDTO BuildSocketDTO(
            GameObject prefab,
            ModuleSocket socket,
            int moduleId,
            int direction,
            ushort connector,
            int ordinal,
            float3 position)
        {
            Quaternion localRotation = Quaternion.Inverse(prefab.transform.rotation) * socket.transform.rotation;
            quaternion rotation = new quaternion(localRotation.x, localRotation.y, localRotation.z, localRotation.w);
            if (!DeepReachStationMath.IsFinite(position) || !DeepReachStationMath.IsFinite(rotation) || math.lengthsq(rotation.value) <= 0.000001f)
                throw new InvalidOperationException($"Station socket transform is invalid on {prefab.name}/{socket.name}.");

            StationSocketDTO dto = default;
            dto.LocalPosition = position;
            dto.LocalRotation = math.normalize(rotation);
            dto.ConnectorMask = connector;
            dto.StableHash = DeepReachStationMath.Hash(HashString(prefab.name) ^ HashString(socket.name) ^ (uint)(ordinal * 16777619));
            dto.ModuleId = (ushort)moduleId;
            dto.Direction = (byte)direction;
            dto.Flags = 1;
            return dto;
        }

        private static StationSocketDTO BuildSocketDTO(
            GameObject prefab,
            BaseModuleTemplate.SocketDefinition definition,
            int moduleId,
            int direction,
            ushort connector,
            int ordinal)
        {
            Vector3 localPositionVector = definition.LocalPosition;
            float3 position = new float3(localPositionVector.x, localPositionVector.y, localPositionVector.z);
            quaternion rotation = ResolveSocketRotation(definition.Direction);
            if (!DeepReachStationMath.IsFinite(position) || !DeepReachStationMath.IsFinite(rotation) || math.lengthsq(rotation.value) <= 0.000001f)
                throw new InvalidOperationException($"Station template socket is invalid on {prefab.name}/{definition.Direction}.");

            StationSocketDTO dto = default;
            dto.LocalPosition = position;
            dto.LocalRotation = math.normalize(rotation);
            dto.ConnectorMask = connector;
            uint directionHash = unchecked((uint)(direction + 1) * 2166136261u);
            uint ordinalHash = unchecked((uint)ordinal * 16777619u);
            dto.StableHash = DeepReachStationMath.Hash(HashString(prefab.name) ^ HashString(definition.CompatibleType) ^ directionHash ^ ordinalHash);
            dto.ModuleId = (ushort)moduleId;
            dto.Direction = (byte)direction;
            dto.Flags = 1;
            return dto;
        }

        private static float3 ResolveSocketLocalPosition(GameObject prefab, ModuleSocket socket)
        {
            Matrix4x4 rootInverse = prefab.transform.worldToLocalMatrix;
            Vector3 localPosition = rootInverse.MultiplyPoint3x4(socket.transform.position);
            return new float3(localPosition.x, localPosition.y, localPosition.z);
        }

        private static int ResolveSocketDirection(ModuleSocket socket, float3 localPosition, Bounds bounds)
        {
            Vector3 centerVector = bounds.center;
            Vector3 extentsVector = bounds.extents;
            float3 center = new float3(centerVector.x, centerVector.y, centerVector.z);
            float3 extents = math.max(new float3(extentsVector.x, extentsVector.y, extentsVector.z), new float3(0.001f));
            float3 relative = localPosition - center;
            float3 normalized = math.abs(relative) / extents;
            float strongest = math.cmax(normalized);
            if (strongest < 0.45f)
                return ConvertDirection(socket.Direction);

            if (normalized.x >= normalized.y && normalized.x >= normalized.z)
                return relative.x >= 0f ? DeepReachStationDirections.East : DeepReachStationDirections.West;
            if (normalized.y >= normalized.x && normalized.y >= normalized.z)
                return relative.y >= 0f ? DeepReachStationDirections.Top : DeepReachStationDirections.Bottom;
            return relative.z >= 0f ? DeepReachStationDirections.North : DeepReachStationDirections.South;
        }

        private static quaternion ResolveSocketRotation(ModuleSocketDirection direction)
        {
            Vector3 forward;
            Vector3 up = Vector3.up;
            switch (direction)
            {
                case ModuleSocketDirection.South:
                    forward = Vector3.back;
                    break;
                case ModuleSocketDirection.East:
                    forward = Vector3.right;
                    break;
                case ModuleSocketDirection.West:
                    forward = Vector3.left;
                    break;
                case ModuleSocketDirection.Top:
                    forward = Vector3.up;
                    up = Vector3.forward;
                    break;
                case ModuleSocketDirection.Bottom:
                    forward = Vector3.down;
                    up = Vector3.forward;
                    break;
                default:
                    forward = Vector3.forward;
                    break;
            }

            Quaternion rotation = Quaternion.LookRotation(forward, up);
            return new quaternion(rotation.x, rotation.y, rotation.z, rotation.w);
        }

        private static Dictionary<Transform, MeshRenderer> BuildRendererMap(GameObject prefab)
        {
            var renderers = new List<MeshRenderer>(32);
            prefab.GetComponentsInChildren(true, renderers);
            var rendererMap = new Dictionary<Transform, MeshRenderer>(renderers.Count);
            for (int i = 0; i < renderers.Count; i++)
            {
                MeshRenderer renderer = renderers[i];
                if (renderer != null && renderer.transform != null && !rendererMap.ContainsKey(renderer.transform))
                    rendererMap.Add(renderer.transform, renderer);
            }

            return rendererMap;
        }

        private static void RefreshCullMasksForModule(
            List<StationTriangleDTO> triangles,
            int triangleStart,
            int triangleCount,
            List<StationMeshVertexDTO> vertices,
            int vertexStart,
            Bounds bounds,
            ushort[] sockets,
            List<StationSocketDTO> socketContracts,
            int socketStart,
            int socketCount)
        {
            Vector3 boundsMin = bounds.min;
            Vector3 boundsMax = bounds.max;
            float3 min = new float3(boundsMin.x, boundsMin.y, boundsMin.z);
            float3 max = new float3(boundsMax.x, boundsMax.y, boundsMax.z);
            float maxExtent = Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z));
            float tolerance = Mathf.Max(SocketFaceToleranceMeters, maxExtent * SocketFaceToleranceExtentScale);
            float socketWindowRadius = Mathf.Clamp(maxExtent * SocketWindowExtentScale, SocketWindowMinRadius, SocketWindowMaxRadius);

            for (int i = 0; i < triangleCount; i++)
            {
                int triangleIndex = triangleStart + i;
                StationTriangleDTO tri = triangles[triangleIndex];
                StationMeshVertexDTO a = vertices[vertexStart + tri.Index0];
                StationMeshVertexDTO b = vertices[vertexStart + tri.Index1];
                StationMeshVertexDTO c = vertices[vertexStart + tri.Index2];
                float3 center = (a.Position + b.Position + c.Position) * (1f / 3f);
                float3 normal = math.normalizesafe(math.cross(b.Position - a.Position, c.Position - a.Position), new float3(0f, 1f, 0f));
                ushort cullMask = 0;

                for (int d = 0; d < s_directionOrder.Length; d++)
                {
                    int direction = s_directionOrder[d];
                    if (sockets[direction] == 0)
                        continue;

                    float3 axis = DirectionAxis(direction);
                    bool nearFace = IsNearDirectionalFace(center, min, max, direction, tolerance);
                    bool nearSocket = IsNearSocketWindow(center, direction, socketContracts, socketStart, socketCount, socketWindowRadius);
                    if (nearFace && nearSocket && math.abs(math.dot(normal, axis)) >= SocketCapNormalDotThreshold)
                        cullMask |= (ushort)(1 << direction);
                }

                tri.CullDirectionMask = cullMask;
                triangles[triangleIndex] = tri;
            }
        }

        private static bool IsNearSocketWindow(
            float3 center,
            int direction,
            List<StationSocketDTO> socketContracts,
            int socketStart,
            int socketCount,
            float radius)
        {
            float radiusSq = radius * radius;
            int end = Math.Min(socketContracts.Count, socketStart + socketCount);
            for (int i = socketStart; i < end; i++)
            {
                StationSocketDTO socket = socketContracts[i];
                if (socket.Direction != (byte)direction)
                    continue;

                float distanceSq = TangentialDistanceSq(center, socket.LocalPosition, direction);
                if (distanceSq <= radiusSq)
                    return true;
            }

            return false;
        }

        private static float TangentialDistanceSq(float3 lhs, float3 rhs, int direction)
        {
            switch (direction)
            {
                case DeepReachStationDirections.North:
                case DeepReachStationDirections.South:
                    return math.lengthsq(new float2(lhs.x - rhs.x, lhs.y - rhs.y));
                case DeepReachStationDirections.East:
                case DeepReachStationDirections.West:
                    return math.lengthsq(new float2(lhs.z - rhs.z, lhs.y - rhs.y));
                default:
                    return math.lengthsq(new float2(lhs.x - rhs.x, lhs.z - rhs.z));
            }
        }

        private static Bounds ResolveRendererBounds(GameObject prefab)
        {
            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
            Matrix4x4 rootInverse = prefab.transform.worldToLocalMatrix;
            bool hasBounds = false;
            Bounds bounds = default;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || IsRejectedRenderChild(renderer.transform) || !IsFiniteBounds(renderer.bounds))
                    continue;

                Bounds local = TransformBounds(rootInverse, renderer.bounds);
                if (!IsFiniteBounds(local))
                    continue;

                if (!hasBounds)
                {
                    bounds = local;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(local);
                }
            }

            if (!hasBounds || bounds.size.sqrMagnitude < 0.0001f)
                bounds = new Bounds(Vector3.zero, new Vector3(4f, 3f, 4f));

            return bounds;
        }

        private static Bounds TransformBounds(Matrix4x4 matrix, Bounds bounds)
        {
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            Bounds result = new Bounds(
                matrix.MultiplyPoint3x4(center + new Vector3(-extents.x, -extents.y, -extents.z)),
                Vector3.zero);
            result.Encapsulate(matrix.MultiplyPoint3x4(center + new Vector3(extents.x, -extents.y, -extents.z)));
            result.Encapsulate(matrix.MultiplyPoint3x4(center + new Vector3(extents.x, extents.y, -extents.z)));
            result.Encapsulate(matrix.MultiplyPoint3x4(center + new Vector3(-extents.x, extents.y, -extents.z)));
            result.Encapsulate(matrix.MultiplyPoint3x4(center + new Vector3(-extents.x, -extents.y, extents.z)));
            result.Encapsulate(matrix.MultiplyPoint3x4(center + new Vector3(extents.x, -extents.y, extents.z)));
            result.Encapsulate(matrix.MultiplyPoint3x4(center + new Vector3(extents.x, extents.y, extents.z)));
            result.Encapsulate(matrix.MultiplyPoint3x4(center + new Vector3(-extents.x, extents.y, extents.z)));

            return result;
        }

        private static void EnsureFiniteBounds(Bounds bounds, string owner)
        {
            if (!IsFiniteBounds(bounds))
                throw new InvalidOperationException($"Station module bounds are non-finite: {owner}.");
        }

        private static bool IsFiniteBounds(Bounds bounds)
        {
            return IsFiniteVector(bounds.center) &&
                   IsFiniteVector(bounds.size);
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return DeepReachStationMath.IsFinite(new float3(value.x, value.y, value.z));
        }

        private static void AppendEmptyModule(
            List<StationModuleRuleDTO> rules,
            List<StationMeshSliceDTO> slices,
            List<StationModuleAnalysis> analyses,
            List<string> names,
            List<string> paths)
        {
            StationModuleRuleDTO empty = default;
            empty.ModuleHash = HashString("EMPTY");
            empty.Weight = 1f;
            empty.ModuleId = DeepReachStationConstants.EmptyModuleId;
            rules.Add(empty);
            slices.Add(default);
            analyses.Add(new StationModuleAnalysis
            {
                Name = "EMPTY",
                Path = string.Empty,
                Bounds = new Bounds(Vector3.zero, Vector3.zero)
            });
            names.Add("EMPTY");
            paths.Add(string.Empty);
        }

        private static NativeArray<T> ToNative<T>(List<T> source, Allocator allocator)
            where T : struct
        {
            var result = new NativeArray<T>(source.Count, allocator, NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < source.Count; i++)
                result[i] = source[i];
            return result;
        }

        private static void AppendQuad(
            List<StationTriangleDTO> triangles,
            int vertexStart,
            int i0,
            int i1,
            int i2,
            int i3,
            ushort cullMask,
            int seed,
            ushort materialSlot)
        {
            triangles.Add(new StationTriangleDTO
            {
                Index0 = i0,
                Index1 = i1,
                Index2 = i2,
                CullDirectionMask = cullMask,
                SubMesh = materialSlot,
                SourceHash = DeepReachStationMath.Hash((uint)(vertexStart + seed)),
                Flags = 1u
            });
            triangles.Add(new StationTriangleDTO
            {
                Index0 = i0,
                Index1 = i2,
                Index2 = i3,
                CullDirectionMask = cullMask,
                SubMesh = materialSlot,
                SourceHash = DeepReachStationMath.Hash((uint)(vertexStart + seed + 1)),
                Flags = 1u
            });
        }

        private static ushort DirectionCullMask(ushort[] socketMasks, int direction)
        {
            if (socketMasks == null || (uint)direction >= (uint)socketMasks.Length || socketMasks[direction] == 0)
                return 0;

            return (ushort)(1 << direction);
        }

        private static bool IsStructuralModulePath(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            return name.StartsWith("PFB_Module_", StringComparison.OrdinalIgnoreCase) ||
                   name.StartsWith("PFB_Ruin_", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRejectedRenderChild(Transform transform)
        {
            for (Transform t = transform; t != null; t = t.parent)
            {
                string name = t.name;
                if (name.IndexOf("Collider", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Trigger", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("LOD1", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("LOD2", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("LOD3", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static float ResolveModuleWeight(string name, int socketCount, int vertexCount)
        {
            float socketBias = Mathf.Clamp(socketCount, 1, 6);
            float costPenalty = Mathf.Clamp01(vertexCount / 24000f);
            if (name.IndexOf("Corridor", StringComparison.OrdinalIgnoreCase) >= 0)
                return 2.25f + socketBias * 0.15f - costPenalty;
            if (name.IndexOf("Foundation", StringComparison.OrdinalIgnoreCase) >= 0)
                return 1.75f + socketBias * 0.12f - costPenalty;
            return 1f + socketBias * 0.08f - costPenalty * 0.5f;
        }

        private static byte ResolveDrawPriority(string name)
        {
            if (name.IndexOf("Foundation", StringComparison.OrdinalIgnoreCase) >= 0)
                return 3;
            if (name.IndexOf("Corridor", StringComparison.OrdinalIgnoreCase) >= 0)
                return 2;
            return 1;
        }

        private static int ConvertDirection(ModuleSocketDirection direction)
        {
            switch (direction)
            {
                case ModuleSocketDirection.North:
                    return DeepReachStationDirections.North;
                case ModuleSocketDirection.South:
                    return DeepReachStationDirections.South;
                case ModuleSocketDirection.East:
                    return DeepReachStationDirections.East;
                case ModuleSocketDirection.West:
                    return DeepReachStationDirections.West;
                case ModuleSocketDirection.Top:
                    return DeepReachStationDirections.Top;
                case ModuleSocketDirection.Bottom:
                    return DeepReachStationDirections.Bottom;
                default:
                    return DeepReachStationDirections.North;
            }
        }

        private static ushort ConnectorMaskFromType(string compatibleType, Dictionary<string, ushort> connectorMasks)
        {
            if (string.IsNullOrWhiteSpace(compatibleType))
                return (ushort)DeepReachStationConstants.GenericConnectorMask;

            string key = compatibleType.Trim();
            if (connectorMasks.TryGetValue(key, out ushort mask))
                return mask;

            int bit = connectorMasks.Count + 1;
            if (bit >= 16)
                throw new InvalidOperationException("Station socket vocabulary exceeded the 15 explicit connector bits available in StationModuleRuleDTO.");

            mask = (ushort)(1u << bit);
            connectorMasks.Add(key, mask);
            return mask;
        }

        private static float3 DirectionAxis(int direction)
        {
            switch (direction)
            {
                case DeepReachStationDirections.North:
                    return new float3(0f, 0f, 1f);
                case DeepReachStationDirections.East:
                    return new float3(1f, 0f, 0f);
                case DeepReachStationDirections.South:
                    return new float3(0f, 0f, -1f);
                case DeepReachStationDirections.West:
                    return new float3(-1f, 0f, 0f);
                case DeepReachStationDirections.Top:
                    return new float3(0f, 1f, 0f);
                default:
                    return new float3(0f, -1f, 0f);
            }
        }

        private static bool IsNearDirectionalFace(float3 center, float3 min, float3 max, int direction, float tolerance)
        {
            switch (direction)
            {
                case DeepReachStationDirections.North:
                    return math.abs(center.z - max.z) <= tolerance;
                case DeepReachStationDirections.East:
                    return math.abs(center.x - max.x) <= tolerance;
                case DeepReachStationDirections.South:
                    return math.abs(center.z - min.z) <= tolerance;
                case DeepReachStationDirections.West:
                    return math.abs(center.x - min.x) <= tolerance;
                case DeepReachStationDirections.Top:
                    return math.abs(center.y - max.y) <= tolerance;
                default:
                    return math.abs(center.y - min.y) <= tolerance;
            }
        }

        private static Material ResolvePrimaryMaterial(List<Material> materials)
        {
            for (int i = 0; i < materials.Count; i++)
            {
                Material material = materials[i];
                if (IsPreferredStructuralMaterial(material))
                    return material;
            }

            for (int i = 0; i < materials.Count; i++)
            {
                Material material = materials[i];
                if (material != null && !IsRejectedPrimaryMaterial(material))
                    return material;
            }

            return null;
        }

        private static bool IsPreferredStructuralMaterial(Material material)
        {
            if (material == null || IsRejectedPrimaryMaterial(material))
                return false;

            string name = material.name;
            return name.IndexOf("Mat_Module_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("MAT_family_ruin", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("RuinSeep", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsRejectedPrimaryMaterial(Material material)
        {
            if (material == null)
                return true;

            string name = material.name;
            if (name.IndexOf("Leak", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Glass", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Ghost", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Scan", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            if (material.IsKeywordEnabled("_ALPHABLEND_ON") ||
                material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT") ||
                material.renderQueue >= 3000)
                return true;

            string renderType = material.GetTag("RenderType", true, string.Empty);
            return renderType.IndexOf("Transparent", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static uint HashString(string text)
        {
            uint hash = 2166136261u;
            if (string.IsNullOrEmpty(text))
                return DeepReachStationMath.Hash(hash);

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c >= 'A' && c <= 'Z')
                    c = (char)(c + 32);
                if (c == ' ' || c == '\t')
                    continue;

                hash ^= c;
                hash *= 16777619u;
            }

            return DeepReachStationMath.Hash(hash);
        }

    }
}
#endif
