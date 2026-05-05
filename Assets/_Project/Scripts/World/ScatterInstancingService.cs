using System;
using System.Buffers;
using System.Collections.Generic;
using GPUInstancer;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Encapsulates GPUI flora matrix aggregation and visibility-buffer pushes for scatter reconcile.
    /// </summary>
    internal sealed class ScatterInstancingService
    {
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

            if (!matricesByPrototype.TryGetValue(prototype, out Matrix4x4[] matrices))
            {
                matrices = ArrayPool<Matrix4x4>.Shared.Rent(64); // COLD ALLOC: Matrix4x4[64] - pooled GPUI flora prototype buffer - owner: ScatterInstancingService
                matricesByPrototype.Add(prototype, matrices);
                counts.Add(prototype, 0);
                bufferCapacities.Add(prototype, 0);
                knownPrototypes.Add(prototype);
            }

            int count = counts[prototype];
            if (count >= matrices.Length)
            {
                int newCapacity = Mathf.NextPowerOfTwo(count + 1);
                Matrix4x4[] expanded = ArrayPool<Matrix4x4>.Shared.Rent(newCapacity); // COLD ALLOC: Matrix4x4[newCapacity] - GPUI flora prototype buffer growth - owner: ScatterInstancingService
                Array.Copy(matrices, 0, expanded, 0, count);
                ArrayPool<Matrix4x4>.Shared.Return(matrices, clearArray: false);
                matrices = expanded;
                matricesByPrototype[prototype] = matrices;
            }

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

        private static bool ShouldUseFloraGpuiPath(
            GPUInstancerPrefabManager manager,
            WorldProceduralScatterDirector.ScatterPlacement placement,
            WorldPrefabFamilyProfile.VariantEntry runtimeVariant,
            out GPUInstancerPrefabPrototype prototype)
        {
            prototype = null;

            if (!Application.isPlaying || manager == null || placement == null)
                return false;

            WorldPrefabFamilyProfile family = placement.Family;
            if (family == null ||
                (family.proceduralDomain != WorldPrefabFamilyProfile.ProceduralDomain.Kelp &&
                 family.proceduralDomain != WorldPrefabFamilyProfile.ProceduralDomain.Coral))
            {
                return false;
            }

            if (family.expectsCollision || family.expectsInteraction)
                return false;

            GameObject prefab = runtimeVariant != null ? runtimeVariant.prefab : null;
            if (prefab == null || !prefab.TryGetComponent(out GPUInstancerPrefab gpuiPrefab))
                return false;

            prototype = gpuiPrefab.prefabPrototype;
            return prototype != null;
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

                float scaleX = new Vector3(matrix.m00, matrix.m10, matrix.m20).magnitude;
                float scaleY = new Vector3(matrix.m01, matrix.m11, matrix.m21).magnitude;
                float scaleZ = new Vector3(matrix.m02, matrix.m12, matrix.m22).magnitude;
                float radius = Mathf.Max(2f, Mathf.Max(scaleX, Mathf.Max(scaleY, scaleZ)) * 4f);
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

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
