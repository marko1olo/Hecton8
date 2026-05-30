using System;
using System.Buffers;
using System.Collections.Generic;
using GPUInstancer;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Encapsulates GPUI flora matrix aggregation and visibility-buffer pushes for scatter reconcile.
    /// </summary>
    internal sealed class ScatterInstancingService
    {
        private readonly Dictionary<GameObject, GPUInstancerPrefabPrototype> _prototypeByPrefab =
            new Dictionary<GameObject, GPUInstancerPrefabPrototype>(128);
        private readonly HashSet<GameObject> _nonGpuiPrefabs = new HashSet<GameObject>();
        private bool _supportsVendorGpuInstancerCompute;

        public ScatterInstancingService()
        {
            RefreshPlatformCapabilitiesCold();
        }

        public void RefreshPlatformCapabilitiesCold()
        {
            _supportsVendorGpuInstancerCompute = SystemInfo.supportsComputeShaders;
        }

        public void PrewarmFamilyPrototypeCacheCold(WorldPrefabFamilyProfile family)
        {
            if (family == null ||
                family.variants == null ||
                family.variants.Length == 0 ||
                !IsFloraGpuiEligibleFamily(family))
            {
                return;
            }

            for (int i = 0; i < family.variants.Length; i++)
                PrewarmVariantPrototypeCacheCold(family.variants[i]);
        }

        public void PrewarmVariantPrototypeCacheCold(WorldPrefabFamilyProfile.VariantEntry runtimeVariant)
        {
            GameObject prefab = runtimeVariant != null ? runtimeVariant.prefab : null;
            if (prefab == null ||
                _prototypeByPrefab.ContainsKey(prefab) ||
                _nonGpuiPrefabs.Contains(prefab))
            {
                return;
            }

            if (prefab.TryGetComponent(out GPUInstancerPrefab gpuiPrefab) &&
                gpuiPrefab != null &&
                gpuiPrefab.prefabPrototype != null)
            {
                _prototypeByPrefab[prefab] = gpuiPrefab.prefabPrototype;
                return;
            }

            _nonGpuiPrefabs.Add(prefab);
        }

        public void PrewarmFamilyAggregationStorageCold(
            WorldPrefabFamilyProfile family,
            List<GPUInstancerPrefabPrototype> knownPrototypes,
            Dictionary<GPUInstancerPrefabPrototype, Matrix4x4[]> matricesByPrototype,
            Dictionary<GPUInstancerPrefabPrototype, int> counts,
            Dictionary<GPUInstancerPrefabPrototype, int> bufferCapacities,
            int requiredCapacity)
        {
            if (family == null ||
                family.variants == null ||
                family.variants.Length == 0 ||
                !IsFloraGpuiEligibleFamily(family))
            {
                return;
            }

            for (int i = 0; i < family.variants.Length; i++)
            {
                PrewarmVariantAggregationStorageCold(
                    family.variants[i],
                    knownPrototypes,
                    matricesByPrototype,
                    counts,
                    bufferCapacities,
                    requiredCapacity);
            }
        }

        public void PrewarmVariantAggregationStorageCold(
            WorldPrefabFamilyProfile.VariantEntry runtimeVariant,
            List<GPUInstancerPrefabPrototype> knownPrototypes,
            Dictionary<GPUInstancerPrefabPrototype, Matrix4x4[]> matricesByPrototype,
            Dictionary<GPUInstancerPrefabPrototype, int> counts,
            Dictionary<GPUInstancerPrefabPrototype, int> bufferCapacities,
            int requiredCapacity)
        {
            if (!TryResolveCachedPrototype(runtimeVariant, out GPUInstancerPrefabPrototype prototype))
                return;

            EnsurePrototypeAggregationStorageCold(
                prototype,
                knownPrototypes,
                matricesByPrototype,
                counts,
                bufferCapacities,
                requiredCapacity);
        }

        public void ResetAggregation(
            List<GPUInstancerPrefabPrototype> knownPrototypes,
            Dictionary<GPUInstancerPrefabPrototype, int> counts,
            ref int activeGpuiPlacements)
        {
            activeGpuiPlacements = 0;
            if (knownPrototypes == null || counts == null)
                return;

            for (int i = 0; i < knownPrototypes.Count; i++)
            {
                GPUInstancerPrefabPrototype prototype = knownPrototypes[i];
                if (prototype == null)
                    continue;

                counts[prototype] = 0;
            }
        }

        public bool TryRegisterPlacement(
            GPUInstancerPrefabManager manager,
            WorldProceduralScatterDirector.ScatterPlacement placement,
            WorldPrefabFamilyProfile.VariantEntry runtimeVariant,
            List<GPUInstancerPrefabPrototype> knownPrototypes,
            Dictionary<GPUInstancerPrefabPrototype, Matrix4x4[]> matricesByPrototype,
            Dictionary<GPUInstancerPrefabPrototype, int> counts,
            Dictionary<GPUInstancerPrefabPrototype, int> bufferCapacities,
            ref int activeGpuiPlacements,
            out GPUInstancerPrefabPrototype prototype)
        {
            if (!ShouldUseFloraGpuiPath(manager, placement, runtimeVariant, out prototype))
                return false;

            if (knownPrototypes == null ||
                matricesByPrototype == null ||
                counts == null ||
                bufferCapacities == null)
            {
                return false;
            }

            if (!matricesByPrototype.TryGetValue(prototype, out Matrix4x4[] matrices) ||
                matrices == null ||
                matrices.Length <= 0)
            {
                return true;
            }

            if (!counts.TryGetValue(prototype, out int count))
                return true;
            if (count >= matrices.Length)
                return true;

            matrices[count] = ScatterGPUIBackend.BuildOriginRelativeMatrix(
                placement.Position,
                placement.Rotation,
                placement.Scale);
            counts[prototype] = count + 1;
            activeGpuiPlacements++;
            return true;
        }

        public bool CanUseFloraGpuiPath(
            GPUInstancerPrefabManager manager,
            WorldProceduralScatterDirector.ScatterPlacement placement,
            WorldPrefabFamilyProfile.VariantEntry runtimeVariant,
            out GPUInstancerPrefabPrototype prototype)
        {
            return ShouldUseFloraGpuiPath(manager, placement, runtimeVariant, out prototype);
        }

        public void FlushBuffers(
            GPUInstancerPrefabManager manager,
            List<GPUInstancerPrefabPrototype> knownPrototypes,
            Dictionary<GPUInstancerPrefabPrototype, Matrix4x4[]> matricesByPrototype,
            Dictionary<GPUInstancerPrefabPrototype, int> counts,
            Dictionary<GPUInstancerPrefabPrototype, int> bufferCapacities,
            HashSet<GPUInstancerPrefabPrototype> initializedPrototypes)
        {
            if (manager == null ||
                knownPrototypes == null ||
                matricesByPrototype == null ||
                counts == null ||
                bufferCapacities == null ||
                initializedPrototypes == null ||
                !CanUseVendorGpuInstancerCompute() ||
                !Application.isPlaying)
            {
                return;
            }

            Bounds aggregateBounds = default;
            bool hasAggregateBounds = false;

            for (int i = 0; i < knownPrototypes.Count; i++)
            {
                GPUInstancerPrefabPrototype prototype = knownPrototypes[i];
                if (prototype == null)
                    continue;

                counts.TryGetValue(prototype, out int count);
                Matrix4x4[] matrices = matricesByPrototype[prototype];
                int requiredCapacity = matrices != null ? matrices.Length : 0;

                bool needsInitialize = !initializedPrototypes.Contains(prototype);
                if (!needsInitialize &&
                    bufferCapacities.TryGetValue(prototype, out int currentCapacity) &&
                    currentCapacity < requiredCapacity)
                {
                    needsInitialize = true;
                }

                if (needsInitialize)
                {
                    GPUInstancerAPI.InitializePrototype(
                        manager,
                        prototype,
                        requiredCapacity,
                        count);
                    initializedPrototypes.Add(prototype);
                    bufferCapacities[prototype] = requiredCapacity;
                }

                if (count <= 0)
                {
                    GPUInstancerAPI.UpdateVisibilityBufferWithMatrix4x4Array(
                        manager,
                        prototype,
                        Array.Empty<Matrix4x4>());
                    continue;
                }

                GPUInstancerAPI.UpdateVisibilityBufferWithMatrix4x4Array(
                    manager,
                    prototype,
                    matrices,
                    0,
                    0,
                    count);

                AccumulateInstancingBounds(matrices, count, ref aggregateBounds, ref hasAggregateBounds);
            }

            if (hasAggregateBounds)
            {
                aggregateBounds.Expand(8f);
                manager.instancingBounds = aggregateBounds;
            }
        }

        public void ClearVisibility(
            GPUInstancerPrefabManager manager,
            List<GPUInstancerPrefabPrototype> knownPrototypes,
            HashSet<GPUInstancerPrefabPrototype> initializedPrototypes,
            ref int activeGpuiPlacements)
        {
            activeGpuiPlacements = 0;
            if (manager == null ||
                knownPrototypes == null ||
                initializedPrototypes == null ||
                !CanUseVendorGpuInstancerCompute() ||
                !Application.isPlaying)
            {
                return;
            }

            for (int i = 0; i < knownPrototypes.Count; i++)
            {
                GPUInstancerPrefabPrototype prototype = knownPrototypes[i];
                if (prototype == null || !initializedPrototypes.Contains(prototype))
                    continue;

                GPUInstancerAPI.UpdateVisibilityBufferWithMatrix4x4Array(
                    manager,
                    prototype,
                    Array.Empty<Matrix4x4>());
            }
        }

        private bool ShouldUseFloraGpuiPath(
            GPUInstancerPrefabManager manager,
            WorldProceduralScatterDirector.ScatterPlacement placement,
            WorldPrefabFamilyProfile.VariantEntry runtimeVariant,
            out GPUInstancerPrefabPrototype prototype)
        {
            prototype = null;

            if (!Application.isPlaying ||
                !CanUseVendorGpuInstancerCompute() ||
                manager == null ||
                placement == null)
            {
                return false;
            }

            WorldPrefabFamilyProfile family = placement.Family;
            if (!IsFloraGpuiEligibleFamily(family))
            {
                return false;
            }

            if (family.expectsCollision || family.expectsInteraction)
                return false;

            return TryResolveCachedPrototype(runtimeVariant, out prototype);
        }

        private static bool IsFloraGpuiEligibleFamily(WorldPrefabFamilyProfile family)
        {
            return family != null &&
                   (family.proceduralDomain == WorldPrefabFamilyProfile.ProceduralDomain.Kelp ||
                    family.proceduralDomain == WorldPrefabFamilyProfile.ProceduralDomain.Coral);
        }

        private bool TryResolveCachedPrototype(
            WorldPrefabFamilyProfile.VariantEntry runtimeVariant,
            out GPUInstancerPrefabPrototype prototype)
        {
            GameObject prefab = runtimeVariant != null ? runtimeVariant.prefab : null;
            if (prefab != null && _prototypeByPrefab.TryGetValue(prefab, out prototype))
                return prototype != null;

            prototype = null;
            return false;
        }

        private bool CanUseVendorGpuInstancerCompute()
        {
            return _supportsVendorGpuInstancerCompute;
        }

        private static void EnsurePrototypeAggregationStorageCold(
            GPUInstancerPrefabPrototype prototype,
            List<GPUInstancerPrefabPrototype> knownPrototypes,
            Dictionary<GPUInstancerPrefabPrototype, Matrix4x4[]> matricesByPrototype,
            Dictionary<GPUInstancerPrefabPrototype, int> counts,
            Dictionary<GPUInstancerPrefabPrototype, int> bufferCapacities,
            int requiredCapacity)
        {
            if (prototype == null ||
                knownPrototypes == null ||
                matricesByPrototype == null ||
                counts == null ||
                bufferCapacities == null)
            {
                return;
            }

            int safeCapacity = Mathf.NextPowerOfTwo(Mathf.Clamp(requiredCapacity, 1, 4096));
            if (matricesByPrototype.TryGetValue(prototype, out Matrix4x4[] matrices) &&
                matrices != null &&
                matrices.Length >= safeCapacity)
            {
                if (!knownPrototypes.Contains(prototype))
                    knownPrototypes.Add(prototype);
                if (!counts.ContainsKey(prototype))
                    counts.Add(prototype, 0);
                if (!bufferCapacities.ContainsKey(prototype))
                    bufferCapacities.Add(prototype, 0);
                return;
            }

            Matrix4x4[] expanded = ArrayPool<Matrix4x4>.Shared.Rent(safeCapacity); // COLD ALLOC: Matrix4x4[safeCapacity] - prewarmed GPUI flora prototype matrix buffer - owner: ScatterInstancingService
            if (matrices != null)
            {
                int existingCount = counts.TryGetValue(prototype, out int count) ? Mathf.Min(count, matrices.Length) : 0;
                Array.Copy(matrices, 0, expanded, 0, Mathf.Min(existingCount, expanded.Length));
                ArrayPool<Matrix4x4>.Shared.Return(matrices, clearArray: false);
            }

            matricesByPrototype[prototype] = expanded;
            if (!knownPrototypes.Contains(prototype))
                knownPrototypes.Add(prototype);
            if (!counts.ContainsKey(prototype))
                counts.Add(prototype, 0);
            if (!bufferCapacities.ContainsKey(prototype))
                bufferCapacities.Add(prototype, 0);
        }

        private static void AccumulateInstancingBounds(
            Matrix4x4[] matrices,
            int count,
            ref Bounds aggregateBounds,
            ref bool hasAggregateBounds)
        {
            if (matrices == null || count <= 0)
                return;

            int safeCount = Mathf.Min(count, matrices.Length);
            for (int i = 0; i < safeCount; i++)
            {
                Matrix4x4 matrix = matrices[i];
                Vector3 position = new Vector3(matrix.m03, matrix.m13, matrix.m23);
                if (!IsFinite(position))
                    continue;

                float radius = ResolveConservativeInstanceRadius(matrix);
                Bounds instanceBounds = new Bounds(position, Vector3.one * radius);
                if (!hasAggregateBounds)
                {
                    aggregateBounds = instanceBounds;
                    hasAggregateBounds = true;
                    continue;
                }

                aggregateBounds.Encapsulate(instanceBounds);
            }
        }

        private static float ResolveConservativeInstanceRadius(Matrix4x4 matrix)
        {
            float xAxis = Mathf.Max(Mathf.Max(Mathf.Abs(matrix.m00), Mathf.Abs(matrix.m10)), Mathf.Abs(matrix.m20));
            float yAxis = Mathf.Max(Mathf.Max(Mathf.Abs(matrix.m01), Mathf.Abs(matrix.m11)), Mathf.Abs(matrix.m21));
            float zAxis = Mathf.Max(Mathf.Max(Mathf.Abs(matrix.m02), Mathf.Abs(matrix.m12)), Mathf.Abs(matrix.m22));
            float maxAxisComponent = Mathf.Max(Mathf.Max(xAxis, yAxis), zAxis);

            return Mathf.Max(2f, maxAxisComponent * 7f);
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
