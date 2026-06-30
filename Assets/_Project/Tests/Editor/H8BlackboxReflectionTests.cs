#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System;
using NUnit.Framework;
using Hecton8.BlackboxDiagnostics;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    [TestFixture]
    public sealed class H8BlackboxReflectionTests
    {
        private class DummyClass
        {
            public static string PropertyThatThrows
            {
                get { throw new InvalidOperationException("Simulated exception on static property."); }
            }
        }

        [Test]
        public void GetStatic_WhenPropertyThrows_CatchesExceptionAndReturnsNull()
        {
            // Act
            object result = H8Reflect.GetStatic(typeof(DummyClass), "PropertyThatThrows");

            // Assert
            Assert.That(result, Is.Null, "Expected GetStatic to return null when the property getter throws an exception.");
        }
    }
}
#endif
