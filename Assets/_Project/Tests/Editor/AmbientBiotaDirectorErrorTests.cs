#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Hecton8.AI.Ambient;
using Hecton8.Core.Contracts;

namespace Hecton8.AI.Ambient.Tests
{
    [TestFixture]
    public class AmbientBiotaDirectorErrorTests
    {
        private GameObject _gameObject;
        private AmbientBiotaDirector _director;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("AmbientBiotaDirectorTest");
            _director = _gameObject.AddComponent<AmbientBiotaDirector>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_gameObject);
            }
        }

        [Test]
        public void LateFrameTick_WhenExceptionInTryBlock_FinallyBlockExecutesAndReleasesPins()
        {
            // Setup director to have a job pending and job buffers pinned
            SetPrivateField(_director, "_jobPending", true);
            SetPrivateField(_director, "_jobBuffersPinned", true);
            SetPrivateField(_director, "_jobBufferPinMask", 7u); // non-zero mask
            SetPrivateField(_director, "_activeJobHandle", new JobHandle());

            // To test the finally block executing and properly freeing resources
            // when an exception is thrown in the Try block, we invoke LateFrameTick
            // on a background thread. This forces Unity methods (like Graphics.DrawMeshInstancedIndirect
            // used inside LateFrameTick -> RenderIndirectBiota) to throw an InvalidOperationException
            // since they can only be called from the main thread.

            Assert.Throws<AggregateException>(() =>
            {
                Task.Run(() => _director.LateFrameTick()).Wait();
            });

            // Even though an exception was thrown during the try block, the finally block
            // should still run. Because completedJob evaluated to true (since _jobPending was true
            // and the empty JobHandle was completed), ReleaseBiotaJobBufferPins() should have been called.

            bool jobBuffersPinned = GetPrivateField<bool>(_director, "_jobBuffersPinned");
            Assert.IsFalse(jobBuffersPinned, "Finally block should have cleared _jobBuffersPinned");
        }

        private void SetPrivateField(object instance, string fieldName, object value)
        {
            var field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
                field.SetValue(instance, value);
        }

        private T GetPrivateField<T>(object instance, string fieldName)
        {
            var field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            return (T)field.GetValue(instance);
        }
    }
}
#endif
