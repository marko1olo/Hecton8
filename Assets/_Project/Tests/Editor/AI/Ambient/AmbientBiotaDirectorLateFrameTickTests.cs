#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using Hecton8.AI.Ambient;
using Hecton8.Core.Contracts.Physics;

namespace Hecton8.Tests.Editor.AI.Ambient
{
    [TestFixture]
    public class AmbientBiotaDirectorLateFrameTickTests
    {
        private GameObject _go;
        private AmbientBiotaDirector _director;

        [SetUp]
        public void Setup()
        {
            _go = new GameObject("TestDirector");
            _director = _go.AddComponent<AmbientBiotaDirector>();
        }

        [TearDown]
        public void Teardown()
        {
            if (_go != null)
            {
                Object.DestroyImmediate(_go);
            }
        }

        [Test]
        public void LateFrameTick_JobPending_ReturnsEarly()
        {
            var fieldInfo = typeof(AmbientBiotaDirector).GetField("_jobPending", BindingFlags.NonPublic | BindingFlags.Instance);
            fieldInfo.SetValue(_director, true);
            Assert.DoesNotThrow(() => _director.LateFrameTick());
        }

        [Test]
        public void LateFrameTick_JobNotPending_RunsToCompletion()
        {
            var fieldInfo = typeof(AmbientBiotaDirector).GetField("_jobPending", BindingFlags.NonPublic | BindingFlags.Instance);
            fieldInfo.SetValue(_director, false);
            Assert.DoesNotThrow(() => _director.LateFrameTick());
        }

        [Test]
        public void LateFrameTick_JobPending_TryFinalizeActiveJobNoWaitTrue_RunsToCompletion()
        {
            // By default _activeJobHandle is uninitialized so DispatcherJobFence.TryFinalizeCompleted will return true
            // since IsCompleted is true on a default JobHandle.
            // This tests the `TryFinalizeActiveJobNoWait` condition in LateFrameTick where `completedJob = true` and `_jobPending = false` after method
            var fieldInfo = typeof(AmbientBiotaDirector).GetField("_jobPending", BindingFlags.NonPublic | BindingFlags.Instance);
            fieldInfo.SetValue(_director, true);

            Assert.DoesNotThrow(() => _director.LateFrameTick());

            // Verify job pending was cleared
            Assert.IsFalse((bool)fieldInfo.GetValue(_director));
        }
    }
}
#endif
