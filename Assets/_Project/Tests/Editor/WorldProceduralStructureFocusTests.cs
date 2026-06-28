using NUnit.Framework;
using Hecton8.World;
using System;

namespace Hecton8.Tests.Editor.World
{
    public class WorldProceduralStructureFocusTests
    {
        [Test]
        public void Enum_WorldProceduralStructureFocus_ContainsExpectedValues()
        {
            Assert.That(Enum.IsDefined(typeof(WorldProceduralStructureFocus), "None"), Is.True);
            Assert.That(Enum.IsDefined(typeof(WorldProceduralStructureFocus), "NaturalLandmark"), Is.True);
            Assert.That(Enum.IsDefined(typeof(WorldProceduralStructureFocus), "TechFragment"), Is.True);
            Assert.That(Enum.IsDefined(typeof(WorldProceduralStructureFocus), "CaveRead"), Is.True);
            Assert.That(Enum.IsDefined(typeof(WorldProceduralStructureFocus), "BiologicalSilhouette"), Is.True);

            var names = Enum.GetNames(typeof(WorldProceduralStructureFocus));
            Assert.That(names.Length, Is.EqualTo(5));
        }

        [Test]
        public void Enum_WorldProceduralStructureFocus_ValuesAreCorrect()
        {
            Assert.That((int)WorldProceduralStructureFocus.None, Is.EqualTo(0));
            Assert.That((int)WorldProceduralStructureFocus.NaturalLandmark, Is.EqualTo(1));
            Assert.That((int)WorldProceduralStructureFocus.TechFragment, Is.EqualTo(2));
            Assert.That((int)WorldProceduralStructureFocus.CaveRead, Is.EqualTo(3));
            Assert.That((int)WorldProceduralStructureFocus.BiologicalSilhouette, Is.EqualTo(4));
        }
    }
}
