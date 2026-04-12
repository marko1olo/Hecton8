using System.Collections.Generic;
using UnityEngine;

namespace Hecton8.World
{
    public sealed partial class WorldProceduralScatterDirector
    {
        /// <summary>
        /// Owner-local bridge for scatter backend binding lookup and prefab resolution.
        /// Keeps binding orchestration out of the main backend integration partial.
        /// </summary>
        private sealed class ScatterBackendBindingBridge
        {
            private readonly WorldProceduralScatterDirector _owner;

            public ScatterBackendBindingBridge(WorldProceduralScatterDirector owner)
            {
                _owner = owner;
            }

            public void RebuildLookup(
                ScatterBackendRuntimeHost host,
                List<ScatterRuntimeRuleEntry> runtimeRuleBuffer)
            {
                if (host == null)
                    return;

                ScatterBackendBindingState bindingState = host.EnsureBindingState();
                bindingState.ResetLookup();

                if (runtimeRuleBuffer == null || runtimeRuleBuffer.Count == 0)
                    return;

                for (int i = 0; i < runtimeRuleBuffer.Count; i++)
                {
                    ScatterRuntimeRuleEntry runtimeRule = runtimeRuleBuffer[i];
                    WorldPrefabFamilyProfile family = runtimeRule.Family;
                    if (family == null)
                        continue;

                    bindingState.TryRegisterFamily(
                        family,
                        ComputeFamilyIndex(family),
                        ResolveRepresentativePrefab(family));
                }
            }

            public bool TryResolvePrefab(
                ScatterBackendRuntimeHost host,
                int familyIndex,
                int layerIndex,
                out GameObject prefab)
            {
                prefab = null;
                ScatterBackendBindingState bindingState = host != null ? host.BindingState : null;
                if (bindingState == null)
                    return false;

                if (bindingState.TryResolveCachedPrefab(familyIndex, layerIndex, out prefab))
                    return true;

                if (!bindingState.TryGetFamily(familyIndex, out WorldPrefabFamilyProfile family) || family == null)
                    return false;

                if ((int)family.scatterLayer != layerIndex)
                    return false;

                prefab = ResolveRepresentativePrefab(family);
                bindingState.CacheRepresentativePrefab(familyIndex, prefab);
                return prefab != null;
            }

            private GameObject ResolveRepresentativePrefab(WorldPrefabFamilyProfile family)
            {
                if (_owner == null || family == null)
                    return null;

                WorldPrefabFamilyProfile.VariantEntry variant = _owner.ResolveRuntimeVariant(
                    family,
                    stableHash: 0,
                    preferFinalVariant: false);

                if (variant != null && variant.prefab != null)
                    return variant.prefab;

                if (family.variants == null)
                    return null;

                for (int i = 0; i < family.variants.Length; i++)
                {
                    WorldPrefabFamilyProfile.VariantEntry fallbackVariant = family.variants[i];
                    if (fallbackVariant != null && fallbackVariant.prefab != null)
                        return fallbackVariant.prefab;
                }

                return null;
            }

            private static int ComputeFamilyIndex(WorldPrefabFamilyProfile family)
            {
                if (family == null)
                    return 0;

                unchecked
                {
                    int hash = 17;
                    string familyId = family.familyId;
                    if (!string.IsNullOrWhiteSpace(familyId))
                    {
                        for (int i = 0; i < familyId.Length; i++)
                            hash = (hash * 31) + familyId[i];
                    }

#pragma warning disable CS0618
                    hash = (hash * 31) + family.GetInstanceID();
#pragma warning restore CS0618
                    return hash;
                }
            }
        }
    }
}
