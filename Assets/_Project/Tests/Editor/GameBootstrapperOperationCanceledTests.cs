using System;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using Hecton8.Bootstrap;

namespace Hecton8.Tests.Editor
{
    public class GameBootstrapperOperationCanceledTests
    {
        [Test]
        public void RunBootstrapRunStartWatchdogAsync_OperationCanceledException_IsCaught()
        {
            var go = new GameObject("BootstrapperTest");
            var bootstrapper = go.AddComponent<GameBootstrapper>();

            var methodInfo = typeof(GameBootstrapper).GetMethod("RunBootstrapRunStartWatchdogAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(methodInfo, "RunBootstrapRunStartWatchdogAsync method not found.");

            using var cts = new CancellationTokenSource();
            cts.Cancel(); // Pre-cancel to trigger OperationCanceledException immediately

            Assert.DoesNotThrow(() =>
            {
                var result = methodInfo.Invoke(bootstrapper, new object[] { cts.Token });
            }, "OperationCanceledException was not caught by the bootstrapper's exception handler.");

            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
