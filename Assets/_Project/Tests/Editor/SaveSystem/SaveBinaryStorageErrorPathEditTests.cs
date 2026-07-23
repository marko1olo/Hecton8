using System;
using System.Reflection;
using Hecton8.SaveSystem;
using NUnit.Framework;

namespace Hecton8.Tests.Editor.SaveSystem
{
    public class SaveBinaryStorageErrorPathEditTests
    {
        [TearDown]
        public void TearDown()
        {
            var fieldCreate = typeof(SaveManager).GetField("s_testHookCreatePersistentNativeArrayException", BindingFlags.Static | BindingFlags.NonPublic);
            if (fieldCreate != null)
                fieldCreate.SetValue(null, null);

            var fieldDispose = typeof(SaveManager).GetField("s_testHookDisposeNativeArrayException", BindingFlags.Static | BindingFlags.NonPublic);
            if (fieldDispose != null)
                fieldDispose.SetValue(null, null);
        }

        [Test]
        public void CreatePersistentNativeArray_WhenCreationFailsAndCleanupFails_ThrowsAggregateException()
        {
            var fieldCreate = typeof(SaveManager).GetField("s_testHookCreatePersistentNativeArrayException", BindingFlags.Static | BindingFlags.NonPublic);
            var fieldDispose = typeof(SaveManager).GetField("s_testHookDisposeNativeArrayException", BindingFlags.Static | BindingFlags.NonPublic);

            fieldCreate.SetValue(null, new Action(() => throw new InvalidOperationException("Simulated creation failure")));
            fieldDispose.SetValue(null, new Action(() => throw new InvalidOperationException("Simulated cleanup failure")));

            var method = typeof(SaveManager).GetMethod("CreatePersistentNativeArray", BindingFlags.Static | BindingFlags.NonPublic);
            var methodGeneric = method.MakeGenericMethod(typeof(byte));

            Exception thrownException = null;
            try
            {
                methodGeneric.Invoke(null, new object[] { 10, Unity.Collections.NativeArrayOptions.UninitializedMemory, "TestLabel" });
            }
            catch (TargetInvocationException ex)
            {
                thrownException = ex.InnerException;
            }

            Assert.IsNotNull(thrownException);
            Assert.IsInstanceOf<AggregateException>(thrownException);
            var aggregateException = (AggregateException)thrownException;
            StringAssert.StartsWith("Persistent SaveManager NativeArray creation failed and cleanup also failed.", aggregateException.Message);
            Assert.AreEqual("Simulated creation failure", aggregateException.InnerExceptions[0].Message);
            Assert.AreEqual("Simulated cleanup failure", aggregateException.InnerExceptions[1].Message);
        }
    }
}
