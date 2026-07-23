using System;
using System.Reflection;
using Hecton8.SaveSystem;
using NUnit.Framework;
using Unity.Collections;

namespace Hecton8.Tests.Editor.SaveSystem
{
    public class SaveManagerCreateTransientNativeArrayTests
    {
        private Type _saveManagerType;
        private MethodInfo _createTransientMethod;
        private FieldInfo _hookField;

        [SetUp]
        public void Setup()
        {
            _saveManagerType = typeof(SaveManager);
            _createTransientMethod = _saveManagerType.GetMethod("CreateTransientNativeArray", BindingFlags.NonPublic | BindingFlags.Static);
            _createTransientMethod = _createTransientMethod?.MakeGenericMethod(typeof(byte));

            _hookField = _saveManagerType.GetField("s_TestDisposeNativeArrayBestEffortHook", BindingFlags.NonPublic | BindingFlags.Static);
        }

        [TearDown]
        public void TearDown()
        {
            if (_hookField != null)
            {
                _hookField.SetValue(null, null);
            }
        }

        [Test]
        public void CreateTransientNativeArray_WhenRegistrationFailsAndDisposeThrows_ThrowsAggregateException()
        {
            Assert.IsNotNull(_createTransientMethod, "Could not find CreateTransientNativeArray method.");
            Assert.IsNotNull(_hookField, "Could not find test hook field.");

            // Set up the hook to throw an exception to simulate cleanup failure
            Action<Exception> hook = (ex) => throw new InvalidOperationException("Simulated cleanup failure");
            _hookField.SetValue(null, hook);

            int length = -1; // Force new NativeArray to throw ArgumentException
            Allocator allocator = Allocator.Temp;
            NativeArrayOptions options = NativeArrayOptions.ClearMemory;
            string sentinelLabel = "TestSentinel";

            Exception caughtException = null;
            try
            {
                _createTransientMethod.Invoke(null, new object[] { length, allocator, options, sentinelLabel });
            }
            catch (TargetInvocationException ex)
            {
                caughtException = ex.InnerException;
            }

            Assert.IsNotNull(caughtException, "Expected an exception to be thrown.");
            Assert.IsTrue(caughtException is AggregateException, $"Expected AggregateException, but got {caughtException.GetType()}");

            var aggregate = caughtException as AggregateException;
            Assert.AreEqual(2, aggregate.InnerExceptions.Count);
            Assert.IsTrue(aggregate.InnerExceptions[0] is ArgumentException, $"First inner exception should be ArgumentException, got {aggregate.InnerExceptions[0].GetType()}");
            Assert.IsTrue(aggregate.InnerExceptions[1] is InvalidOperationException, $"Second inner exception should be InvalidOperationException, got {aggregate.InnerExceptions[1].GetType()}");
        }
    }
}
