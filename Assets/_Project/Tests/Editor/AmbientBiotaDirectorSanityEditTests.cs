#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Hecton8.AI.Ambient;

namespace Hecton8.AI.Tests.Editor
{
    public class AmbientBiotaDirectorSanityEditTests
    {
        private GameObject _directorGameObject;
        private AmbientBiotaDirector _director;

        [SetUp]
        public void SetUp()
        {
            _directorGameObject = new GameObject("AmbientBiotaDirectorTest");
            _director = _directorGameObject.AddComponent<AmbientBiotaDirector>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_directorGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_directorGameObject);
            }
        }

        [Test]
        public void AmbientBiotaDirector_IsInitialized_IsFalseByDefault()
        {
            Assert.IsFalse(_director.IsInitialized);
        }

        [Test]
        public void AmbientBiotaDirector_TickCount_IsZeroByDefault()
        {
            Assert.AreEqual(0, _director.TickCount);
        }

        [Test]
        public void AmbientBiotaDirector_Capacity_IsZeroByDefault()
        {
            Assert.AreEqual(0, _director.Capacity);
        }

        [Test]
        public void AmbientBiotaDirector_ActiveBiotaCount_IsZeroByDefault()
        {
            Assert.AreEqual(0, _director.ActiveBiotaCount);
        }
    }
}
#endif
