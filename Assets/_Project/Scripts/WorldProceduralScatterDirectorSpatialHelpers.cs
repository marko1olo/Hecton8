using Hecton8.Core;
using UnityEngine;

namespace Hecton8.World
{
    public sealed partial class WorldProceduralScatterDirector
    {
        private static readonly int _FamilyCoralLowHash = ComputeStableStringHash("family.coral.low");
        private static readonly int _FamilyCoralMassiveHash = ComputeStableStringHash("family.coral.massive");
        private static readonly int _FamilyCoralBranchingHash = ComputeStableStringHash("family.coral.branching");
        private static readonly int _FamilyCoralPlateHash = ComputeStableStringHash("family.coral.plate");
        private static readonly int _FamilyCoralBrittleHash = ComputeStableStringHash("family.coral.brittle");
        private static readonly int _FamilyRockSmallFloorHash = ComputeStableStringHash("family.rock.small_floor");
        private static readonly int _FamilyRockClusterMediumHash = ComputeStableStringHash("family.rock.cluster.medium");
        private static readonly int _FamilyRockArchLargeHash = ComputeStableStringHash("family.rock.arch.large");
        private static readonly int _FamilyKelpTallHash = ComputeStableStringHash("family.kelp.tall");
        private static readonly int _FamilyKelpPatchDenseHash = ComputeStableStringHash("family.kelp.patch.dense");
        private static readonly int _FamilyKelpCanopyHash = ComputeStableStringHash("family.kelp.canopy");
        private static readonly int _FamilyKelpAbyssalHash = ComputeStableStringHash("family.kelp.abyssal");
        private static readonly int _FamilyCreatureSpawnPassiveHash = ComputeStableStringHash("family.creature.spawn.passive");
        private static readonly int _FamilyPocketSafeHash = ComputeStableStringHash("family.pocket.safe");
        private static readonly int _FamilyEggClusterHash = ComputeStableStringHash("family.egg.cluster");
        private static readonly int _FamilyLandmarkSpireHash = ComputeStableStringHash("family.landmark.spire");
        private static readonly int _FamilyCaveEntranceHash = ComputeStableStringHash("family.cave.entrance");
        private static readonly int _FamilyDebrisScatterHash = ComputeStableStringHash("family.debris.scatter");
        private static readonly int _FamilyDebrisFieldHash = ComputeStableStringHash("family.debris.field");

        private static Vector3 ToAbsoluteScatterPosition(Vector3 runtimePosition)
        {
            return HectonFloatingOrigin.ToAbsoluteUniversePosition(runtimePosition);
        }

        private static Vector3 ToRuntimeScatterPosition(Vector3 absolutePosition)
        {
            return HectonFloatingOrigin.ToRuntimePosition(absolutePosition);
        }

        private bool TryGetObserverAbsolutePosition(out Vector3 absolutePosition)
        {
            absolutePosition = default;
            if (playerTransform == null)
                return false;

            absolutePosition = ToAbsoluteScatterPosition(playerTransform.position);
            return true;
        }

        private static int GetFamilyHash(WorldPrefabFamilyProfile family)
        {
            return family != null ? family.FamilyHash : 0;
        }

        private static bool IsDeterministicClutterFamily(WorldPrefabFamilyProfile family)
        {
            int familyHash = GetFamilyHash(family);
            return familyHash == _FamilyDebrisScatterHash ||
                   familyHash == _FamilyDebrisFieldHash;
        }

        private static int GetVariantHash(WorldPrefabFamilyProfile.VariantEntry variant)
        {
            return variant != null ? variant.VariantHash : 0;
        }

        private static Transform FindDirectChildByName(Transform parent, string childName)
        {
            if (parent == null || string.IsNullOrEmpty(childName))
                return null;

            int childCount = parent.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child != null && string.Equals(child.name, childName, System.StringComparison.Ordinal))
                    return child;
            }

            return null;
        }
    }
}
