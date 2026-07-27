using Hecton8.World;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor.World
{
    /// <summary>
    /// Proves the structure accent floor produced by <see cref="WorldProceduralPatternProfile"/> is
    /// actually consumed, using the shipped authored numbers from
    /// Assets/_Project/Data/World/ProceduralPatternProfiles/*.asset.
    ///
    /// The defect under test: FertileShallows and ReefNavigation - the two patterns that own the
    /// First 20 Minutes shallow route - author techFragmentMin = 0, so
    /// WorldProceduralScatterDirector.cs:7091 skipped the TechFragment guarantee pass entirely and
    /// no technogenic structure was ever forced onto the photic route.
    /// </summary>
    public class WorldProceduralPatternStructureAccentFloorEditTests
    {
        private static WorldProceduralPatternProfile CreateProfile()
        {
            return ScriptableObject.CreateInstance<WorldProceduralPatternProfile>();
        }

        private static void Destroy(WorldProceduralPatternProfile profile)
        {
            if (profile != null)
                UnityEngine.Object.DestroyImmediate(profile);
        }

        private static void ApplyFertileShallows(WorldProceduralPatternProfile profile)
        {
            profile.pattern = WorldProceduralPattern.FertileShallows;
            profile.structureTargetMin = 4;
            profile.structureTargetMax = 6;
            profile.naturalLandmarkMin = 2;
            profile.naturalLandmarkMax = 3;
            profile.techFragmentMin = 0;
            profile.techFragmentMax = 1;
            profile.caveReadMin = 1;
            profile.caveReadMax = 2;
            profile.biologicalSilhouetteMin = 1;
            profile.biologicalSilhouetteMax = 2;
        }

        private static void ApplyReefNavigation(WorldProceduralPatternProfile profile)
        {
            profile.pattern = WorldProceduralPattern.ReefNavigation;
            profile.structureTargetMin = 6;
            profile.structureTargetMax = 8;
            profile.naturalLandmarkMin = 3;
            profile.naturalLandmarkMax = 4;
            profile.techFragmentMin = 0;
            profile.techFragmentMax = 1;
            profile.caveReadMin = 1;
            profile.caveReadMax = 2;
            profile.biologicalSilhouetteMin = 1;
            profile.biologicalSilhouetteMax = 2;
        }

        private static void ApplyIndustrialService(WorldProceduralPatternProfile profile)
        {
            profile.pattern = WorldProceduralPattern.IndustrialService;
            profile.structureTargetMin = 7;
            profile.structureTargetMax = 9;
            profile.naturalLandmarkMin = 1;
            profile.naturalLandmarkMax = 2;
            profile.techFragmentMin = 4;
            profile.techFragmentMax = 6;
            profile.caveReadMin = 1;
            profile.caveReadMax = 2;
            profile.biologicalSilhouetteMin = 0;
            profile.biologicalSilhouetteMax = 0;
        }

        private static int SumStructureAccentFloors(WorldProceduralPatternProfile profile)
        {
            int total = 0;
            for (int i = WorldProceduralPatternProfile.FirstStructureAccentRoleIndex;
                 i <= WorldProceduralPatternProfile.LastStructureAccentRoleIndex;
                 i++)
            {
                total += profile.GetStructureAccentMin((WorldPrefabFamilyProfile.StructureAccentRole)i);
            }

            return total;
        }

        [Test]
        public void FertileShallows_GuaranteesOneTechFragmentFloor()
        {
            WorldProceduralPatternProfile profile = CreateProfile();
            try
            {
                ApplyFertileShallows(profile);

                Assert.AreEqual(
                    1,
                    profile.GetStructureAccentMin(WorldPrefabFamilyProfile.StructureAccentRole.TechFragment),
                    "FertileShallows must guarantee one technogenic structure on the first-exit route.");
                Assert.AreEqual(
                    1,
                    profile.GetStructureAccentMax(WorldPrefabFamilyProfile.StructureAccentRole.TechFragment),
                    "The authored technogenic ceiling of one must survive the guaranteed floor.");
            }
            finally
            {
                Destroy(profile);
            }
        }

        [Test]
        public void ReefNavigation_GuaranteesOneTechFragmentFloor()
        {
            WorldProceduralPatternProfile profile = CreateProfile();
            try
            {
                ApplyReefNavigation(profile);

                Assert.AreEqual(
                    1,
                    profile.GetStructureAccentMin(WorldPrefabFamilyProfile.StructureAccentRole.TechFragment));
            }
            finally
            {
                Destroy(profile);
            }
        }

        [Test]
        public void AuthoredFloorsAreNeverLowered()
        {
            WorldProceduralPatternProfile profile = CreateProfile();
            try
            {
                ApplyFertileShallows(profile);

                Assert.AreEqual(
                    2,
                    profile.GetStructureAccentMin(WorldPrefabFamilyProfile.StructureAccentRole.NaturalLandmark));
                Assert.AreEqual(
                    1,
                    profile.GetStructureAccentMin(WorldPrefabFamilyProfile.StructureAccentRole.CaveRead));
                Assert.AreEqual(
                    1,
                    profile.GetStructureAccentMin(WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette));
            }
            finally
            {
                Destroy(profile);
            }
        }

        [Test]
        public void ExplicitBanStaysBanned()
        {
            WorldProceduralPatternProfile profile = CreateProfile();
            try
            {
                ApplyIndustrialService(profile);

                Assert.AreEqual(
                    0,
                    profile.GetStructureAccentMin(WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette),
                    "A role the pattern bans with a zero ceiling must not receive a guaranteed floor.");
                Assert.AreEqual(
                    0,
                    profile.GetStructureAccentMax(WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette),
                    "A banned role keeps a zero ceiling, which the acceptance pass reads as a rejection.");
                Assert.AreEqual(
                    4,
                    profile.GetStructureAccentMin(WorldPrefabFamilyProfile.StructureAccentRole.TechFragment),
                    "IndustrialService already demands four technogenic structures and must be unchanged.");
            }
            finally
            {
                Destroy(profile);
            }
        }

        [Test]
        public void SummedFloorsNeverExceedStructureLayerTarget()
        {
            WorldProceduralPatternProfile profile = CreateProfile();
            try
            {
                ApplyFertileShallows(profile);
                Assert.AreEqual(5, SumStructureAccentFloors(profile));
                Assert.LessOrEqual(
                    SumStructureAccentFloors(profile),
                    profile.GetTargetMax(WorldPrefabFamilyProfile.ScatterLayer.Structure));

                ApplyReefNavigation(profile);
                Assert.AreEqual(6, SumStructureAccentFloors(profile));
                Assert.LessOrEqual(
                    SumStructureAccentFloors(profile),
                    profile.GetTargetMax(WorldPrefabFamilyProfile.ScatterLayer.Structure));

                ApplyIndustrialService(profile);
                Assert.AreEqual(6, SumStructureAccentFloors(profile));
                Assert.LessOrEqual(
                    SumStructureAccentFloors(profile),
                    profile.GetTargetMax(WorldPrefabFamilyProfile.ScatterLayer.Structure));
            }
            finally
            {
                Destroy(profile);
            }
        }

        [Test]
        public void ExhaustedLayerBudgetGrantsNoGuaranteedFloor()
        {
            WorldProceduralPatternProfile profile = CreateProfile();
            try
            {
                profile.pattern = WorldProceduralPattern.AbyssSparse;
                profile.structureTargetMin = 3;
                profile.structureTargetMax = 3;
                profile.naturalLandmarkMin = 2;
                profile.naturalLandmarkMax = 2;
                profile.techFragmentMin = 0;
                profile.techFragmentMax = 2;
                profile.caveReadMin = 1;
                profile.caveReadMax = 1;
                profile.biologicalSilhouetteMin = 0;
                profile.biologicalSilhouetteMax = 1;

                Assert.AreEqual(
                    0,
                    profile.GetStructureAccentMin(WorldPrefabFamilyProfile.StructureAccentRole.TechFragment),
                    "Authored floors consume the whole layer target, so no slack remains to guarantee.");
                Assert.AreEqual(
                    0,
                    profile.GetStructureAccentMin(WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette));
                Assert.AreEqual(3, SumStructureAccentFloors(profile));
            }
            finally
            {
                Destroy(profile);
            }
        }
    }
}
