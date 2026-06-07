using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class LogRuntimeOwnerEditTests
    {
        [Test]
        public void FieldOperationLogSystem_RuntimeOwnerGateClearsStaleOwnersAndStaticWritesResolveUsableRuntime()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "FieldOperationLogSystem.cs"));
            string awake = ExtractMethodBody(source, "private void Awake()");
            string register = ExtractMethodBody(source, "private bool TryRegisterRuntime()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string resolve = ExtractMethodBody(source, "private static FieldOperationLogSystem ResolveActiveRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsFieldOperationRuntimeUsable(");
            string recordText = ExtractMethodBody(source, "public static void RecordOperation(string source, string title, string summary, string severity = \"INFO\")");
            string recordFixedSummary = ExtractMethodBody(source, "public static void RecordOperation(string source, string title, in FixedCharBuffer summaryBuffer, string severity = \"INFO\")");
            string recordFixedTitle = ExtractMethodBody(source, "public static void RecordOperation(string source, in FixedCharBuffer titleBuffer, in FixedCharBuffer summaryBuffer, string severity = \"INFO\")");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            Assert.Less(
                awake.IndexOf("if (TryAbortForUsableExistingRuntime())", StringComparison.Ordinal),
                awake.IndexOf("EnsureSlots();", StringComparison.Ordinal));
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            Assert.Less(
                register.IndexOf("if (TryAbortForUsableExistingRuntime())", StringComparison.Ordinal),
                register.IndexOf("GlobalRegistry.RegisterFieldOperationLogRuntime(this);", StringComparison.Ordinal));
            StringAssert.Contains("FieldOperationLogSystem active = s_activeRuntime", gate);
            StringAssert.Contains("FieldOperationLogSystem registered = GlobalRegistry.FieldOperations", gate);
            StringAssert.Contains("if (IsFieldOperationRuntimeUsable(active))", gate);
            StringAssert.Contains("if (IsFieldOperationRuntimeUsable(registered))", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterFieldOperationLogRuntime(active);", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterFieldOperationLogRuntime(registered);", gate);
            StringAssert.Contains("s_activeRuntime = null", gate);
            StringAssert.Contains("if (IsFieldOperationRuntimeUsable(active))", resolve);
            StringAssert.Contains("if (!ReferenceEquals(active, null))", resolve);
            StringAssert.Contains("if (IsFieldOperationRuntimeUsable(registered))", resolve);
            StringAssert.Contains("GlobalRegistry.UnregisterFieldOperationLogRuntime(registered);", resolve);
            StringAssert.Contains("system._runtimeRegistered", usable);
            StringAssert.Contains("system.isActiveAndEnabled", usable);
            StringAssert.Contains("FieldOperationLogSystem instance = ResolveActiveRuntime();", recordText);
            StringAssert.Contains("FieldOperationLogSystem instance = ResolveActiveRuntime();", recordFixedSummary);
            StringAssert.Contains("FieldOperationLogSystem instance = ResolveActiveRuntime();", recordFixedTitle);
            StringAssert.DoesNotContain("s_activeRuntime?.Push", source);
            StringAssert.DoesNotContain("FieldOperationLogSystem instance = s_activeRuntime", source);
            StringAssert.DoesNotContain("registered != null && registered != this", awake);
            StringAssert.DoesNotContain("registered != null && registered != this", register);
        }

        [Test]
        public void ScanLogSystem_RuntimeOwnerGateClearsStaleRegistryOwnerBeforeEventSubscription()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "ScanLogSystem.cs"));
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string register = ExtractMethodBody(source, "private void TryRegisterService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsScanLogRuntimeUsable(");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            Assert.Less(
                awake.IndexOf("if (TryAbortForUsableExistingRuntime())", StringComparison.Ordinal),
                awake.IndexOf("NotificationEvents.RegisterMessage", StringComparison.Ordinal));
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            Assert.Less(
                register.IndexOf("if (TryAbortForUsableExistingRuntime())", StringComparison.Ordinal),
                register.IndexOf("GlobalRegistry.RegisterScanLogRuntime(this);", StringComparison.Ordinal));
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", onEnable);
            Assert.Less(
                onEnable.IndexOf("if (TryAbortForUsableExistingRuntime())", StringComparison.Ordinal),
                onEnable.IndexOf("TryRegisterHotSwapListener();", StringComparison.Ordinal));
            Assert.Less(
                onEnable.IndexOf("if (TryAbortForUsableExistingRuntime())", StringComparison.Ordinal),
                onEnable.IndexOf("ScanEvents.Register(this);", StringComparison.Ordinal));
            Assert.Less(
                onEnable.IndexOf("TryRegisterService();", StringComparison.Ordinal),
                onEnable.IndexOf("ScanEvents.Register(this);", StringComparison.Ordinal));
            StringAssert.Contains("ScanLogSystem active = s_activeRuntimeInstance", gate);
            StringAssert.Contains("ScanLogSystem registered = GlobalRegistry.ScanLog", gate);
            StringAssert.Contains("if (IsScanLogRuntimeUsable(active))", gate);
            StringAssert.Contains("if (IsScanLogRuntimeUsable(registered))", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterScanLogRuntime(active);", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterScanLogRuntime(registered);", gate);
            StringAssert.Contains("s_activeRuntimeInstance = null", gate);
            StringAssert.Contains("system._serviceRegistered", usable);
            StringAssert.Contains("system.isActiveAndEnabled", usable);
            StringAssert.DoesNotContain("registered != null && registered != this", awake);
            StringAssert.DoesNotContain("registered != null && registered != this", register);
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
