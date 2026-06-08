using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Gameplay
{
    public static class HectonScanRenderFlags
    {
        public const uint None = 0u;
        public const uint IsScanned = 1u << 0;
        public const uint Loot = 1u << 1;
        public const uint Environment = 1u << 2;
        public const uint AiEntity = 1u << 3;
    }

    /// <summary>
    /// Fixed scan-visual registry used by renderer features. It stores only renderer references and bit flags.
    /// </summary>
    public static class HectonScanRenderRegistry
    {
        public const int MaxTargets = 512;
        private const int MaxSubMeshesPerTarget = 8;
        private const uint ActiveLootMask = HectonScanRenderFlags.IsScanned | HectonScanRenderFlags.Loot;

        // COLD ALLOC: Renderer[512] - scanner render target registry - owner: HectonScanRenderRegistry
        private static readonly Renderer[] s_renderers = new Renderer[MaxTargets];
        // COLD ALLOC: uint[512] - scan render bit flags - owner: HectonScanRenderRegistry
        private static readonly uint[] s_flags = new uint[MaxTargets];
        // COLD ALLOC: AbsoluteUniversePosition[512] - cached scanner loot proxy centers - owner: HectonScanRenderRegistry
        private static readonly AbsoluteUniversePosition[] s_lootCenterAups = new AbsoluteUniversePosition[MaxTargets];
        // COLD ALLOC: float[512] - cached scanner loot proxy radii - owner: HectonScanRenderRegistry
        private static readonly float[] s_lootRadii = new float[MaxTargets];
        // COLD ALLOC: int[512] - cached renderer submesh draw counts - owner: HectonScanRenderRegistry
        private static readonly int[] s_subMeshCounts = new int[MaxTargets];
        // COLD ALLOC: int[512] - renderer bounds refresh frame cache for multi-camera visor passes - owner: HectonScanRenderRegistry
        private static readonly int[] s_lootSphereRefreshFrames = new int[MaxTargets];

        private static int s_count;
        private static uint s_registeredFlagMask;
        private static int s_activeLootCount;

        public static int Count => s_count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            for (int i = 0; i < s_count; i++)
            {
                s_renderers[i] = null;
                s_flags[i] = 0u;
                s_lootCenterAups[i] = default;
                s_lootRadii[i] = 0f;
                s_subMeshCounts[i] = 0;
                s_lootSphereRefreshFrames[i] = 0;
            }

            s_count = 0;
            s_registeredFlagMask = 0u;
            s_activeLootCount = 0;
        }

        public static bool TryGetFlags(Renderer renderer, out uint flags)
        {
            int index = IndexOf(renderer);
            if (index < 0)
            {
                flags = HectonScanRenderFlags.None;
                return false;
            }

            flags = s_flags[index];
            return true;
        }

        public static bool HasAnyTargetWithFlags(uint requiredMask)
        {
            if (requiredMask == HectonScanRenderFlags.None || s_count <= 0 || (s_registeredFlagMask & requiredMask) != requiredMask)
                return false;

            for (int i = 0; i < s_count; i++)
            {
                Renderer renderer = s_renderers[i];
                if (renderer == null)
                {
                    RemoveAtSwapBack(i);
                    i--;
                    continue;
                }

                if ((s_flags[i] & requiredMask) == requiredMask && IsRendererDrawable(renderer))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool Register(Renderer renderer, uint initialFlags)
        {
            if (renderer == null)
                return false;

            int existingIndex = IndexOf(renderer);
            if (existingIndex >= 0)
            {
                bool wasActiveLoot = IsActiveLoot(s_flags[existingIndex]);
                s_flags[existingIndex] |= initialFlags;
                if (!wasActiveLoot && IsActiveLoot(s_flags[existingIndex]))
                    s_activeLootCount++;
                s_registeredFlagMask |= s_flags[existingIndex];
                RefreshRendererMetadata(existingIndex, renderer);
                return true;
            }

            if (s_count >= MaxTargets)
                return false;

            int index = s_count++;
            s_renderers[index] = renderer;
            s_flags[index] = initialFlags;
            if (IsActiveLoot(initialFlags))
                s_activeLootCount++;
            s_registeredFlagMask |= initialFlags;
            RefreshRendererMetadata(index, renderer);
            return true;
        }

        public static void Unregister(Renderer renderer)
        {
            int index = IndexOf(renderer);
            if (index >= 0)
                RemoveAtSwapBack(index);
        }

        public static bool SetFlags(Renderer renderer, uint flags, bool enabled)
        {
            if (renderer == null)
                return false;

            int index = IndexOf(renderer);
            if (index < 0)
            {
                if (!enabled)
                    return false;

                return Register(renderer, flags);
            }

            bool wasActiveLoot = IsActiveLoot(s_flags[index]);
            if (enabled)
            {
                s_flags[index] |= flags;
                s_registeredFlagMask |= s_flags[index];
            }
            else
            {
                s_flags[index] &= ~flags;
            }

            bool isActiveLoot = IsActiveLoot(s_flags[index]);
            if (isActiveLoot != wasActiveLoot)
                s_activeLootCount += isActiveLoot ? 1 : -1;
            if (!enabled && s_flags[index] == HectonScanRenderFlags.None)
            {
                RemoveAtSwapBack(index);
                return true;
            }

            if (!enabled)
                RebuildRegisteredState();

            RefreshRendererMetadata(index, renderer);
            return true;
        }

        public static bool MarkScanned(Transform root, uint additionalFlags)
        {
            if (!TryResolveRenderer(root, out Renderer renderer))
                return false;

            return SetFlags(renderer, HectonScanRenderFlags.IsScanned | additionalFlags, true);
        }

        public static int DrawRenderers(CommandBuffer cmd, Material material, uint requiredMask, int maxDraws)
        {
            if (cmd == null || material == null || maxDraws <= 0)
                return 0;

            return DrawRenderersCore(cmd, null, material, requiredMask, maxDraws);
        }

        public static int DrawRenderers(IRasterCommandBuffer cmd, Material material, uint requiredMask, int maxDraws)
        {
            if (cmd == null || material == null || maxDraws <= 0)
                return 0;

            return DrawRenderersCore(null, cmd, material, requiredMask, maxDraws);
        }

        private static int DrawRenderersCore(
            CommandBuffer cmd,
            IRasterCommandBuffer rasterCmd,
            Material material,
            uint requiredMask,
            int maxDraws)
        {
            int drawCount = 0;
            for (int i = 0; i < s_count && drawCount < maxDraws; i++)
            {
                Renderer renderer = s_renderers[i];
                if (renderer == null)
                {
                    RemoveAtSwapBack(i);
                    i--;
                    continue;
                }

                if ((s_flags[i] & requiredMask) != requiredMask || !IsRendererDrawable(renderer))
                {
                    continue;
                }

                int subMeshCount = math.max(1, s_subMeshCounts[i]);
                for (int subMeshIndex = 0; subMeshIndex < subMeshCount && drawCount < maxDraws; subMeshIndex++)
                {
                    if (rasterCmd != null)
                        rasterCmd.DrawRenderer(renderer, material, subMeshIndex, 0);
                    else
                        cmd.DrawRenderer(renderer, material, subMeshIndex, 0);
                    drawCount++;
                }
            }

            return drawCount;
        }

        public static bool TryFindNearestLootSphereAup(in AbsoluteUniversePosition observerAup, float maxDistance, float radiusPadding, out Vector4 lootSphereAup)
        {
            lootSphereAup = default;
            if (s_activeLootCount <= 0 || !observerAup.IsFinite())
                return false;

            double maxDistanceSq = maxDistance > 0f ? (double)maxDistance * maxDistance : double.MaxValue;
            double bestDistanceSq = maxDistanceSq;
            bool found = false;
            for (int i = 0; i < s_count; i++)
            {
                Renderer renderer = s_renderers[i];
                if (renderer == null)
                {
                    RemoveAtSwapBack(i);
                    i--;
                    continue;
                }

                if ((s_flags[i] & ActiveLootMask) != ActiveLootMask)
                {
                    continue;
                }

                if (!IsRendererActive(renderer))
                {
                    continue;
                }

                RefreshLootSphere(i, renderer);
                float cachedRadius = s_lootRadii[i];
                if (cachedRadius <= 0f)
                    continue;

                AbsoluteUniversePosition centerAup = s_lootCenterAups[i];
                if (!centerAup.IsFinite())
                    continue;

                double distanceSq = AbsoluteUniversePosition.DistanceSq(in observerAup, in centerAup);
                if (distanceSq > bestDistanceSq)
                    continue;

                AbsoluteUniversePosition runtimeOriginAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
                if (!runtimeOriginAup.IsFinite())
                    continue;

                double3 localCenterDelta = AupPrecisionMath.LocalDeltaDouble(
                    centerAup.ToAbsoluteDouble3(),
                    runtimeOriginAup.ToAbsoluteDouble3());
                float radius = cachedRadius + math.max(0f, radiusPadding);
                float3 shaderCenter = AupPrecisionMath.DowncastLocalDelta(localCenterDelta, float3.zero);
                if (!math.all(math.isfinite(shaderCenter)))
                    continue;

                bestDistanceSq = distanceSq;
                lootSphereAup = new Vector4(
                    shaderCenter.x,
                    shaderCenter.y,
                    shaderCenter.z,
                    math.max(0.1f, radius));
                found = true;
            }

            return found;
        }

        private static bool TryResolveRenderer(Transform root, out Renderer renderer)
        {
            renderer = null;
            if (root == null)
                return false;

            renderer = ComponentReferenceUtility.ResolveOwnedComponent<Renderer>(root);
            return renderer != null;
        }

        private static int IndexOf(Renderer renderer)
        {
            if (renderer == null)
                return -1;

            for (int i = 0; i < s_count; i++)
            {
                if (ReferenceEquals(s_renderers[i], renderer))
                    return i;
            }

            return -1;
        }

        private static void RemoveAtSwapBack(int index)
        {
            int lastIndex = s_count - 1;
            if ((uint)index >= (uint)s_count)
                return;

            if (IsActiveLoot(s_flags[index]))
                s_activeLootCount--;

            s_renderers[index] = s_renderers[lastIndex];
            s_flags[index] = s_flags[lastIndex];
            s_lootCenterAups[index] = s_lootCenterAups[lastIndex];
            s_lootRadii[index] = s_lootRadii[lastIndex];
            s_subMeshCounts[index] = s_subMeshCounts[lastIndex];
            s_lootSphereRefreshFrames[index] = s_lootSphereRefreshFrames[lastIndex];
            s_renderers[lastIndex] = null;
            s_flags[lastIndex] = 0u;
            s_lootCenterAups[lastIndex] = default;
            s_lootRadii[lastIndex] = 0f;
            s_subMeshCounts[lastIndex] = 0;
            s_lootSphereRefreshFrames[lastIndex] = 0;
            s_count = lastIndex;
            if (s_count == 0)
            {
                s_registeredFlagMask = 0u;
                s_activeLootCount = 0;
            }
            else
            {
                RebuildRegisteredState();
            }
        }

        private static void RefreshRendererMetadata(int index, Renderer renderer)
        {
            if ((uint)index >= MaxTargets || renderer == null)
                return;

            s_subMeshCounts[index] = ResolveSubMeshCount(renderer);
            RefreshLootSphere(index, renderer);
        }

        private static int ResolveSubMeshCount(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinnedRenderer && skinnedRenderer.sharedMesh != null)
                return math.clamp(skinnedRenderer.sharedMesh.subMeshCount, 1, MaxSubMeshesPerTarget);

            if (renderer.TryGetComponent(out MeshFilter meshFilter) && meshFilter.sharedMesh != null)
                return math.clamp(meshFilter.sharedMesh.subMeshCount, 1, MaxSubMeshesPerTarget);

            return 1;
        }

        private static void RefreshLootSphere(int index, Renderer renderer)
        {
            if ((uint)index >= MaxTargets || renderer == null)
                return;

            if ((s_flags[index] & ActiveLootMask) != ActiveLootMask)
            {
                s_lootCenterAups[index] = default;
                s_lootRadii[index] = 0f;
                s_lootSphereRefreshFrames[index] = 0;
                return;
            }

            int frame = SystemDispatcher.CurrentFrameIndex;
            if (s_lootSphereRefreshFrames[index] == frame && s_lootRadii[index] > 0f)
                return;

            Bounds bounds = renderer.bounds;
            Vector3 boundsCenter = bounds.center;
            Vector3 boundsExtents = bounds.extents;
            float3 center = new float3(boundsCenter.x, boundsCenter.y, boundsCenter.z);
            float3 extents = new float3(boundsExtents.x, boundsExtents.y, boundsExtents.z);
            if (!math.all(math.isfinite(center)) || !math.all(math.isfinite(extents)))
            {
                s_lootCenterAups[index] = default;
                s_lootRadii[index] = 0f;
                s_lootSphereRefreshFrames[index] = frame;
                return;
            }

            if (!TryResolveRuntimeAup(boundsCenter, out s_lootCenterAups[index]))
            {
                s_lootCenterAups[index] = default;
                s_lootRadii[index] = 0f;
                s_lootSphereRefreshFrames[index] = frame;
                return;
            }

            s_lootRadii[index] = math.max(0.1f, math.cmax(extents));
            s_lootSphereRefreshFrames[index] = frame;
        }

        private static bool TryResolveRuntimeAup(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!math.isfinite(runtimePosition.x) ||
                !math.isfinite(runtimePosition.y) ||
                !math.isfinite(runtimePosition.z))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return positionAup.IsFinite();
        }

        private static bool IsActiveLoot(uint flags)
        {
            return (flags & ActiveLootMask) == ActiveLootMask;
        }

        private static bool IsRendererActive(Renderer renderer)
        {
            return renderer != null &&
                   renderer.enabled &&
                   !renderer.forceRenderingOff &&
                   renderer.gameObject.activeInHierarchy;
        }

        private static bool IsRendererDrawable(Renderer renderer)
        {
            return IsRendererActive(renderer) && renderer.isVisible;
        }

        private static void RebuildRegisteredState()
        {
            uint mask = 0u;
            int activeLootCount = 0;
            for (int i = 0; i < s_count; i++)
            {
                mask |= s_flags[i];
                if (IsActiveLoot(s_flags[i]))
                    activeLootCount++;
            }

            s_registeredFlagMask = mask;
            s_activeLootCount = activeLootCount;
        }

    }
}
