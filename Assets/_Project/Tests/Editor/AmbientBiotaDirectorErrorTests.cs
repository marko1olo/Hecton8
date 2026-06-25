#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Unity.Jobs;
using Hecton8.AI.Ambient;
using Hecton8.Core.Contracts;

namespace Hecton8.Tests.Editor
{
    public class AmbientBiotaDirectorErrorTests
    {
        private struct DummyJob : IJob
        {
            public void Execute() { }
        }

        private T GetPrivateField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return (T)field.GetValue(target);
        }

        private void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(target, value);
        }

        [Test]
        public void CompleteActiveJobForTeardown_WhenJobHandleThrows_EnsuresSwapWindowClosed()
        {
            var go = new GameObject("TestDirector");
            var director = go.AddComponent<AmbientBiotaDirector>();

            try
            {
                // Schedule a dummy job
                var handle = new DummyJob().Schedule();

                SetPrivateField(director, "_jobPending", true);
                SetPrivateField(director, "_activeJobHandle", handle);

                int initialDepth = (int)typeof(DispatcherJobFence).GetField("_activeSwapWindowDepth", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

                var method = typeof(AmbientBiotaDirector).GetMethod("CompleteActiveJobForTeardown", BindingFlags.Instance | BindingFlags.NonPublic);

                // Call CompleteActiveJobForTeardown from a background thread to force Unity to throw an InvalidOperationException
                // ("JobHandle.Complete() can only be called from the main thread")
                // This ensures the try-catch block is exercised and the finally block executes.
                var t = Task.Run(() =>
                {
                    try
                    {
                        method.Invoke(director, null);
                    }
                    catch (TargetInvocationException ex)
                    {
                        if (ex.InnerException is InvalidOperationException)
                            return; // expected
                        throw ex.InnerException;
                    }
                });

                t.Wait();

                int finalDepth = (int)typeof(DispatcherJobFence).GetField("_activeSwapWindowDepth", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

                Assert.AreEqual(initialDepth, finalDepth, "Swap window depth must be unchanged (closed) after exception.");

                bool isJobPending = GetPrivateField<bool>(director, "_jobPending");
                Assert.IsTrue(isJobPending, "Job pending should remain true because the completion was aborted by an exception.");

                // Cleanup the job handle on the main thread so Unity doesn't complain about uncompleted jobs
                handle.Complete();
            }
            finally
            {
                GameObject.DestroyImmediate(go);
            }
        }
    }
}
#endif
