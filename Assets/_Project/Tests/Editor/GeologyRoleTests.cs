using NUnit.Framework;
using System.Collections.Generic;
using Hecton8;

namespace Hecton8.Tests.Editor
{
    [TestFixture]
    public class GeologyRoleTests
    {
        [Test]
        public void DefaultInstantiation_IsValid()
        {
            var role = new GeologyRole();

            Assert.That(role, Is.Not.Null);
            Assert.That(role.primaryMaterial, Is.Null);
            Assert.That(role.secondaryFeatures, Is.Null);
        }

        [Test]
        public void Roughness_CanBeSetToBoundaries()
        {
            var role = new GeologyRole();

            role.roughness = 0f;
            Assert.That(role.roughness, Is.EqualTo(0f).Within(0.0001f));

            role.roughness = 1f;
            Assert.That(role.roughness, Is.EqualTo(1f).Within(0.0001f));
        }
    }
}
