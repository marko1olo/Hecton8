using Hecton8.Core;
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
        private const uint ActiveLootMask = HectonScanRenderFlags.IsScanned | HectonScanRenderFlags.Loot;

        // COLD ALLOC: Renderer[512] - scanner render target registry - owner: HectonScanRenderRegistry
        private static readonly Renderer[] s_renderers = new Renderer[MaxTargets];
        // COLD ALLOC: uint[512] - scan render bit flags - owner: HectonScanRenderRegistry
        private static readonly uint[] s_flags = new uint[MaxTargets];
        // COLD ALLOC: AbsoluteUniversePosition[512] - cached scanner loot proxy centers - owner: HectonScanRenderRegistry
        private static readonly AbsoluteUniversePosition[] s_lootCenterAups = new AbsoluteUniversePosition[MaxTargets];
        // COLD ALLOC: float[512] - cached scanner loot proxy radii - owner: HectonScanRenderRegistry
        private static readonly float[] s_lootRadii = new float[MaxTargets];

        private static int s_count;
        private static uint s_registeredFlagMask;

        public static int Count => s_count;
        public static bool HasRegisteredFlags(uint requiredMask) => s_count > 0 && (s_registeredFlagMask & requiredMask) == requiredMask;

        public static bool Register(Renderer renderer, uint initialFlags)
        {
            if (renderer == null)
                return false;

            int existingIndex = IndexOf(renderer);
            if (existingIndex >= 0)
            {
                s_flags[existingIndex] |= initialFlags;
                s_registeredFlagMask |= s_flags[existingIndex];
                RefreshLootSphere(existingIndex, renderer);
                return true;
            }

            if (s_count >= MaxTargets)
                return false;

            int index = s_count++;
            s_renderers[index] = renderer;
            s_flags[index] = initialFlags;
            s_registeredFlagMask |= initialFlags;
            RefreshLootSphere(index, renderer);
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

            if (enabled)
            {
                s_flags[index] |= flags;
                s_registeredFlagMask |= s_flags[index];
            }
            else
            {
                s_flags[index] &= ~flags;
                RebuildRegisteredFlagMask();
            }

            RefreshLootSphere(index, renderer);
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

                if ((s_flags[i] & requiredMask) != requiredMask ||
                    !renderer.enabled ||
                    !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                cmd.DrawRenderer(renderer, material, 0, 0);
                drawCount++;
            }

            return drawCount;
        }

        public static bool TryFindNearestLootSphereAup(in AbsoluteUniversePosition observerAup, float maxDistance, float radiusPadding, out Vector4 lootSphereAup)
        {
            lootSphereAup = default;

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

                float cachedRadius = s_lootRadii[i];
                if (cachedRadius <= 0f)
                    continue;

                AbsoluteUniversePosition centerAup = s_lootCenterAups[i];
                double distanceSq = AbsoluteUniversePosition.DistanceSq(in observerAup, in centerAup);
                if (distanceSq > bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                float radius = cachedRadius + math.max(0f, radiusPadding);
                double3 absoluteCenter = centerAup.ToAbsoluteDouble3();
                lootSphereAup = new Vector4(
                    (float)absoluteCenter.x,
                    (float)absoluteCenter.y,
                    (float)absoluteCenter.z,
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

            s_renderers[index] = s_renderers[lastIndex];
            s_flags[index] = s_flags[lastIndex];
            s_lootCenterAups[index] = s_lootCenterAups[lastIndex];
            s_lootRadii[index] = s_lootRadii[lastIndex];
            s_renderers[lastIndex] = null;
            s_flags[lastIndex] = 0u;
            s_lootCenterAups[lastIndex] = default;
            s_lootRadii[lastIndex] = 0f;
            s_count = lastIndex;
            if (s_count == 0)
                s_registeredFlagMask = 0u;
            else
                RebuildRegisteredFlagMask();
        }

        private static void RefreshLootSphere(int index, Renderer renderer)
        {
            if ((uint)index >= MaxTargets || renderer == null)
                return;

            if ((s_flags[index] & ActiveLootMask) != ActiveLootMask)
            {
                s_lootCenterAups[index] = default;
                s_lootRadii[index] = 0f;
                return;
            }

            Bounds bounds = renderer.bounds;
            s_lootCenterAups[index] = AbsoluteUniversePosition.FromRuntimePosition(bounds.center);
            s_lootRadii[index] = math.max(0.1f, math.cmax((float3)bounds.extents));
        }

        private static void RebuildRegisteredFlagMask()
        {
            uint mask = 0u;
            for (int i = 0; i < s_count; i++)
                mask |= s_flags[i];

            s_registeredFlagMask = mask;
        }

    }
}
