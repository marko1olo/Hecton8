using System;
using System.Reflection;
using Hecton8.SaveSystem;
using NUnit.Framework;

namespace Hecton8.Tests.Editor.SaveSystem
{
    public sealed class SaveManagerErrorPathTests
    {
        [TearDown]
        public void TearDown()
        {
            // Clean up the test hook
            typeof(SaveManager).GetField("TestHook_PublishSaveStatus_SimulateException", BindingFlags.NonPublic | BindingFlags.Static)
                ?.SetValue(null, null);
        }

        [Test]
        public void PublishSaveStatus_CatchesExceptionAndHandlesGracefully()
        {
            // Arrange
            bool testHookCalled = false;
            Action simulateException = () =>
            {
                testHookCalled = true;
                throw new InvalidOperationException("Simulated exception during PublishSaveStatus");
            };

            var testHookField = typeof(SaveManager).GetField("TestHook_PublishSaveStatus_SimulateException", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(testHookField, "Could not find TestHook_PublishSaveStatus_SimulateException field.");
            testHookField.SetValue(null, simulateException);

            var publishSaveStatusMethod = typeof(SaveManager).GetMethod("PublishSaveStatus", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(byte), typeof(uint), typeof(byte), typeof(float), typeof(uint) }, null);
            Assert.IsNotNull(publishSaveStatusMethod, "Could not find PublishSaveStatus method.");

            // Act
            Exception thrownException = null;
            try
            {
                // Invoke method through reflection, which wraps target exceptions in TargetInvocationException
                publishSaveStatusMethod.Invoke(null, new object[] { (byte)0, (uint)1, (byte)0, 0f, 0u });
            }
            catch (Exception ex)
            {
                thrownException = ex;
            }

            // Assert
            Assert.IsTrue(testHookCalled, "Test hook was not invoked.");
            Assert.IsNull(thrownException, "PublishSaveStatus did not catch the exception as expected.");
        }
    }
}
