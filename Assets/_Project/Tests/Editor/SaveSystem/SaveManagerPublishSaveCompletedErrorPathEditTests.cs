using NUnit.Framework;
using UnityEngine;
using Hecton8.SaveSystem;
using System.Reflection;
using System;
using Hecton8.Core.Contracts;

namespace Hecton8.Tests.SaveSystem
{
    [TestFixture]
    public class SaveManagerPublishSaveCompletedErrorPathEditTests
    {
        private class TestException : Exception { }

        [TearDown]
        public void TearDown()
        {
            var hookField = typeof(SaveManager).GetField("TestHook_PublishSaveCompleted_BeforePush", BindingFlags.NonPublic | BindingFlags.Static);
            if (hookField != null)
            {
                hookField.SetValue(null, null);
            }
        }

        [Test]
        public void PublishSaveCompleted_ThrowsException_HandledGracefully()
        {
            var publishMethod = typeof(SaveManager).GetMethod("PublishSaveCompleted", BindingFlags.NonPublic | BindingFlags.Static);
            var hookField = typeof(SaveManager).GetField("TestHook_PublishSaveCompleted_BeforePush", BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(publishMethod, "PublishSaveCompleted method not found. Check parameters.");
            Assert.IsNotNull(hookField, "TestHook_PublishSaveCompleted_BeforePush not found. Ensure the test hook is injected.");

            bool threwInHook = false;
            Action throwAction = () => {
                threwInHook = true;
                throw new TestException();
            };
            hookField.SetValue(null, throwAction);

            // Reflection creation of inner struct: PublishSaveCompletedArgs
            object args = Activator.CreateInstance(
                typeof(SaveManager).GetNestedType("PublishSaveCompletedArgs", BindingFlags.Public | BindingFlags.NonPublic),
                new object[] { 1u, 100L, 50L, true }
            );

            // This should not throw an Unhandled Exception due to the catch block handling TestException
            Assert.DoesNotThrow(() => {
                try
                {
                    publishMethod.Invoke(null, new object[] { 123u, args });
                }
                catch (TargetInvocationException ex)
                {
                    // TargetInvocationException wraps the exception thrown by the invoked method.
                    // If the exception escaped PublishSaveCompleted, it would be caught here.
                    // But our goal is to verify it is caught WITHIN PublishSaveCompleted, so it shouldn't bubble up.
                    throw ex.InnerException ?? ex;
                }
            });

            Assert.IsTrue(threwInHook, "The test hook was never executed.");
        }
    }
}
