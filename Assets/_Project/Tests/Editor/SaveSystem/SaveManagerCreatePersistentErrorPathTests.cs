using System;
using System.Reflection;
using Hecton8.SaveSystem;
using NUnit.Framework;
using Unity.Collections;

namespace Hecton8.Tests.Editor.SaveSystem
{
    public sealed class SaveManagerCreatePersistentErrorPathTests
    {
        private MethodInfo _createPersistentNativeArrayMethod;
        private FieldInfo _testHookField;

        [SetUp]
        public void Setup()
        {
            _createPersistentNativeArrayMethod = typeof(SaveManager).GetMethod(
                "CreatePersistentNativeArray",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(_createPersistentNativeArrayMethod, "Could not find CreatePersistentNativeArray method.");

            _testHookField = typeof(SaveManager).GetField(
                "TestHookSimulateCleanupFailure",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(_testHookField, "Could not find TestHookSimulateCleanupFailure field.");
        }

        [TearDown]
        public void TearDown()
        {
            _testHookField?.SetValue(null, null);
        }

        [Test]
        public void CreatePersistentNativeArray_ThrowsAggregateException_WhenAllocationAndCleanupBothFail()
        {
            var genericMethod = _createPersistentNativeArrayMethod.MakeGenericMethod(typeof(byte));

            // Set the hook to throw an exception during cleanup
            Action failingHook = () => throw new InvalidOperationException("Simulated cleanup failure");
            _testHookField.SetValue(null, failingHook);

            // Trigger allocation failure by passing a negative length
            var ex = Assert.Throws<TargetInvocationException>(() =>
            {
                genericMethod.Invoke(null, new object[] { -1, NativeArrayOptions.UninitializedMemory, "TestLabel" });
            });

            Assert.IsNotNull(ex.InnerException, "Expected an InnerException.");
            Assert.IsInstanceOf<AggregateException>(ex.InnerException, "Expected an AggregateException.");

            var aggregateEx = (AggregateException)ex.InnerException;
            Assert.IsTrue(aggregateEx.Message.Contains("Persistent SaveManager NativeArray creation failed"), "Unexpected AggregateException message.");
            Assert.AreEqual(2, aggregateEx.InnerExceptions.Count, "Expected exactly 2 inner exceptions.");
            Assert.IsInstanceOf<ArgumentException>(aggregateEx.InnerExceptions[0], "First exception should be ArgumentException from allocation failure.");
            Assert.IsInstanceOf<InvalidOperationException>(aggregateEx.InnerExceptions[1], "Second exception should be InvalidOperationException from cleanup failure.");
        }
    }
}
