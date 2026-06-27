#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Hecton8.AI.Ambient;
using Hecton8.Core.Contracts;

namespace Hecton8.Tests.Editor.AI.Ambient
{
    [TestFixture]
    public sealed class AmbientBiotaDirectorTeardownErrorEditTests
    {
        [Test]
        public void CompleteActiveJobForTeardown_WhenJobHandleThrows_ReleasesSwapWindowAndJobPending()
        {
            var go = new GameObject("TestBiotaDirector");
            var director = go.AddComponent<AmbientBiotaDirector>();

            var jobPendingField = typeof(AmbientBiotaDirector).GetField("_jobPending", BindingFlags.Instance | BindingFlags.NonPublic);
            jobPendingField.SetValue(director, true);

            var method = typeof(AmbientBiotaDirector).GetMethod("CompleteActiveJobForTeardown", BindingFlags.Instance | BindingFlags.NonPublic);

            var depthField = typeof(DispatcherJobFence).GetField("_activeSwapWindowDepth", BindingFlags.Static | BindingFlags.NonPublic);
            int initialDepth = (int)depthField.GetValue(null);

            InvalidOperationException caughtEx = null;

            var task = Task.Run(() =>
            {
                try
                {
                    method.Invoke(director, null);
                }
                catch (TargetInvocationException ex)
                {
                    if (ex.InnerException is InvalidOperationException ioe)
                    {
                        caughtEx = ioe;
                    }
                    else
                    {
                        throw ex.InnerException;
                    }
                }
            });

            task.Wait();

            Assert.IsNotNull(caughtEx, "Expected JobHandle.Complete to throw an InvalidOperationException from the background thread");
            Assert.That(caughtEx.Message, Does.Contain("JobHandle.Complete can only be called from the main thread"));

            int finalDepth = (int)depthField.GetValue(null);
            Assert.AreEqual(initialDepth, finalDepth, "The swap window depth should be fully restored by the finally block even if JobHandle.Complete throws.");

            bool isPendingNow = (bool)jobPendingField.GetValue(director);
            Assert.IsTrue(isPendingNow, "Job pending should remain true if job completion threw an exception, preventing unsafe release.");

            GameObject.DestroyImmediate(go);
        }
    }
}
#endif
