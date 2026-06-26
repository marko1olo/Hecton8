#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.Jobs;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class AmbientBiotaDirectorExceptionTests
    {
        private AmbientBiotaDirector _director;

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("TestDirector");
            _director = go.AddComponent<AmbientBiotaDirector>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_director != null && _director.gameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_director.gameObject);
            }
        }

        private struct DummyJob : IJob
        {
            public void Execute() { }
        }

        [Test]
        public void CompleteActiveJobForTeardown_WhenJobHandleThrows_ReleasesSwapWindow()
        {
            SetPrivateField(_director, "_jobPending", true);

            var handle = new DummyJob().Schedule();
            SetPrivateField(_director, "_activeJobHandle", handle);

            var fieldInfo = typeof(DispatcherJobFence).GetField("_activeSwapWindowDepth", BindingFlags.Static | BindingFlags.NonPublic);
            int initialDepth = (int)fieldInfo.GetValue(null);

            var method = typeof(AmbientBiotaDirector).GetMethod("CompleteActiveJobForTeardown", BindingFlags.Instance | BindingFlags.NonPublic);

            bool exceptionThrown = false;
            try
            {
                Task.Run(() =>
                {
                    try
                    {
                        method.Invoke(_director, null);
                    }
                    catch (TargetInvocationException ex)
                    {
                        throw ex.InnerException;
                    }
                }).GetAwaiter().GetResult();
            }
            catch (InvalidOperationException)
            {
                exceptionThrown = true;
            }

            Assert.IsTrue(exceptionThrown, "Expected InvalidOperationException was not thrown from background thread job completion.");

            int finalDepth = (int)fieldInfo.GetValue(null);
            Assert.AreEqual(initialDepth, finalDepth, "Swap window depth was not restored after exception in CompleteActiveJobForTeardown.");

            // Clean up the handle natively if the exception aborted Complete
            handle.Complete();
        }

        private void SetPrivateField<T>(object target, string fieldName, T value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(target, value);
        }
    }
}
#endif
