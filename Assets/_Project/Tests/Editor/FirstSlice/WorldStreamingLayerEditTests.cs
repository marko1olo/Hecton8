using System;
using NUnit.Framework;
using Hecton8.World;

namespace Hecton8.Tests.Editor
{
    public sealed class WorldStreamingLayerEditTests
    {
        [Test]
        public void WorldStreamingLayer_HasExpectedIntegerBindings()
        {
            Assert.That((int)WorldStreamingLayer.TerrainLod, Is.EqualTo(0));
            Assert.That((int)WorldStreamingLayer.Flora, Is.EqualTo(1));
            Assert.That((int)WorldStreamingLayer.Debris, Is.EqualTo(2));
            Assert.That((int)WorldStreamingLayer.Resources, Is.EqualTo(3));
            Assert.That((int)WorldStreamingLayer.Fauna, Is.EqualTo(4));
            Assert.That((int)WorldStreamingLayer.Construction, Is.EqualTo(5));
            Assert.That((int)WorldStreamingLayer.LargeThreats, Is.EqualTo(6));
        }

        [Test]
        public void WorldStreamingLayer_HasExactExpectedCount()
        {
            var values = Enum.GetValues(typeof(WorldStreamingLayer));
            Assert.That(values.Length, Is.EqualTo(7));
        }
    }
}
