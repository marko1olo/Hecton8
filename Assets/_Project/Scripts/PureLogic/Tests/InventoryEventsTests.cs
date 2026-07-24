using NUnit.Framework;
using System;
using System.Reflection;
using Hecton8.Inventory;
using Hecton8.Core;
using Unity.Collections;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class InventoryEventsTests
    {
        [Test]
        public void ReleaseNativeHashSet_NativeMemorySentinelUnregisterThrows_ExceptionCaughtAndRethrown()
        {
            var methodInfo = typeof(InventoryEvents).GetMethod(
                "ReleaseNativeHashSet",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(methodInfo, "ReleaseNativeHashSet method not found");
            var genericMethod = methodInfo.MakeGenericMethod(typeof(int));

            var hashSet = new NativeHashSet<int>();
            int sentinelId = 1;

            var countField = typeof(NativeMemorySentinel).GetField("_count", BindingFlags.NonPublic | BindingFlags.Static);
            int originalCount = (int)countField.GetValue(null);

            try
            {
                countField.SetValue(null, 9999);

                var args = new object[] { hashSet, sentinelId };

                var ex = Assert.Throws<TargetInvocationException>(() => genericMethod.Invoke(null, args));
                Assert.IsInstanceOf<IndexOutOfRangeException>(ex.InnerException);

                Assert.AreEqual(0, args[1]);
            }
            finally
            {
                countField.SetValue(null, originalCount);
            }
        }

        [Test]
        public void ReleaseNativeQueue_NativeMemorySentinelUnregisterThrows_ExceptionCaughtAndRethrown()
        {
            var methodInfo = typeof(InventoryEvents).GetMethod(
                "ReleaseNativeQueue",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(methodInfo, "ReleaseNativeQueue method not found");
            var genericMethod = methodInfo.MakeGenericMethod(typeof(int));

            var queue = new NativeQueue<int>();
            int sentinelId = 1;

            var countField = typeof(NativeMemorySentinel).GetField("_count", BindingFlags.NonPublic | BindingFlags.Static);
            int originalCount = (int)countField.GetValue(null);

            try
            {
                countField.SetValue(null, 9999);

                var args = new object[] { queue, sentinelId };

                var ex = Assert.Throws<TargetInvocationException>(() => genericMethod.Invoke(null, args));
                Assert.IsInstanceOf<IndexOutOfRangeException>(ex.InnerException);

                Assert.AreEqual(0, args[1]);
            }
            finally
            {
                countField.SetValue(null, originalCount);
            }
        }
    }
}
