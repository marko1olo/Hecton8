#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Hecton8.AI.Ambient;

namespace Hecton8.Tests.Editor
{
    public sealed class AmbientBiotaDirectorExceptionEditTests
    {
        private GameObject _go;
        private AmbientBiotaDirector _director;
        private MethodInfo _tryEnsureGraphicsResourcesColdMethod;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("AmbientBiotaDirectorTestObj");
            _director = _go.AddComponent<AmbientBiotaDirector>();
            _tryEnsureGraphicsResourcesColdMethod = typeof(AmbientBiotaDirector).GetMethod(
                "TryEnsureGraphicsResourcesCold",
                BindingFlags.NonPublic | BindingFlags.Instance);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
                UnityEngine.Object.DestroyImmediate(_go);
        }

        [Test]
        public void TryEnsureGraphicsResourcesCold_WhenExceptionThrown_CatchesAndReturnsFalse()
        {
            // Capacity <= 0 returns false immediately in EnsureGraphicsResources, bypassing GraphicsBuffer creation.
            // To force an exception in EnsureGraphicsResources, we need a capacity > 0 but invalid.
            // For AmbientBiotaGpuInstance, passing int.MaxValue will exceed buffer size limits
            // and Unity throws ArgumentException or InvalidOperationException from GraphicsBuffer constructor.

            bool result = false;

            // Just invoke with int.MaxValue. If it throws ArgumentException, it'll be caught.
            result = (bool)_tryEnsureGraphicsResourcesColdMethod.Invoke(_director, new object[] { int.MaxValue });

            Assert.IsFalse(result, "Exceeding buffer size limits should throw, be caught, and return false.");
        }
    }
}
#endif
