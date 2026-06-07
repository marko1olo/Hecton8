using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class PerformanceMonitorRuntimeOwnerEditTests
    {
        [Test]
        public void PerformanceMonitor_RuntimeOwnerGateClearsStaleMirrorAndRegistryBeforeSamplingAndRouting()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "PerformanceMonitor.cs"));
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string register = ExtractMethodBody(source, "private void TryRegisterService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string resolver = ExtractMethodBody(source, "private static PerformanceMonitor ResolveActiveRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsPerformanceMonitorRuntimeUsable(");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "_frameStopwatch = new System.Diagnostics.Stopwatch();");
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "_frameTimeHistory = new float[historyLength];");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", onEnable);
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "TryRegisterService();");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "TryRegisterToDispatcher();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterPerformanceMonitorRuntime(this);");
            StringAssert.Contains("if (_serviceRegistered)", register);
            StringAssert.Contains("s_currentRuntime = this;", register);

            StringAssert.Contains("PerformanceMonitor active = s_currentRuntime", gate);
            StringAssert.Contains("PerformanceMonitor registered = GlobalRegistry.PerformanceMonitor", gate);
            StringAssert.Contains("if (IsPerformanceMonitorRuntimeUsable(active))", gate);
            StringAssert.Contains("if (IsPerformanceMonitorRuntimeUsable(registered))", gate);
            StringAssert.Contains("Destroy(gameObject);", gate);
            StringAssert.Contains("s_currentRuntime = null", gate);
            StringAssert.Contains("s_currentRuntime = registered", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterPerformanceMonitorRuntime(active);", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterPerformanceMonitorRuntime(registered);", gate);

            StringAssert.Contains("PerformanceMonitor active = s_currentRuntime", resolver);
            StringAssert.Contains("PerformanceMonitor registered = GlobalRegistry.PerformanceMonitor", resolver);
            StringAssert.Contains("if (IsPerformanceMonitorRuntimeUsable(active))", resolver);
            StringAssert.Contains("if (IsPerformanceMonitorRuntimeUsable(registered))", resolver);
            StringAssert.Contains("s_currentRuntime = registered", resolver);
            StringAssert.Contains("GlobalRegistry.UnregisterPerformanceMonitorRuntime(registered);", resolver);
            StringAssert.Contains("return null;", resolver);

            StringAssert.Contains("monitor._serviceRegistered", usable);
            StringAssert.Contains("monitor.isActiveAndEnabled", usable);
            StringAssert.Contains("PerformanceMonitor runtime = ResolveActiveRuntime();", source);
            StringAssert.DoesNotContain("registered != null && registered != this", source);
            StringAssert.DoesNotContain("PerformanceMonitor runtime = s_currentRuntime", source);
        }

        private static void AssertTextBefore(string body, string expectedEarlier, string expectedLater)
        {
            int earlierIndex = body.IndexOf(expectedEarlier, StringComparison.Ordinal);
            int laterIndex = body.IndexOf(expectedLater, StringComparison.Ordinal);
            Assert.GreaterOrEqual(earlierIndex, 0, "Missing earlier text: " + expectedEarlier);
            Assert.GreaterOrEqual(laterIndex, 0, "Missing later text: " + expectedLater);
            Assert.Less(earlierIndex, laterIndex, expectedEarlier + " should appear before " + expectedLater);
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(signatureIndex, 0, "Missing method signature: " + signature);

            int bodyStart = source.IndexOf('{', signatureIndex);
            Assert.GreaterOrEqual(bodyStart, 0, "Missing method body: " + signature);

            int depth = 0;
            for (int i = bodyStart; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '{')
                {
                    depth++;
                    continue;
                }

                if (c != '}')
                    continue;

                depth--;
                if (depth == 0)
                    return source.Substring(bodyStart, i - bodyStart + 1);
            }

            Assert.Fail("Unclosed method body: " + signature);
            return string.Empty;
        }
    }
}
