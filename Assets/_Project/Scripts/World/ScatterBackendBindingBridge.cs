using System.Collections.Generic;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.World
{
    public sealed partial class WorldProceduralScatterDirector
    {
        /// <summary>
        /// Owner-local bridge for scatter backend binding lookup.
        /// Keeps representative layer-family index binding out of the main backend integration partial.
        /// </summary>
        private sealed class ScatterBackendBindingBridge
        {
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

                    bindingState.TryRegisterRepresentativeFamilyIndex(
                        family,
                        ComputeFamilyIndex(family));
                }
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

                    hash = (hash * 31) + unchecked((int)EntityId.ToULong(family.GetEntityId()));
                    return hash;
                }
            }
        }
    }
}
