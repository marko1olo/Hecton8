using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class RunModifierRuntimeOwnerEditTests
    {
        [Test]
        public void RunModifierController_RuntimeOwnerGateClearsStaleRegistryOwnerBeforeStaticAndSaveRouting()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "Meta", "RunModifierController.cs"));
            string nightmareProperty = ExtractMethodBody(source, "public static bool IsNightmareModeActive");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string register = ExtractMethodBody(source, "private void TryRegisterService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string resolve = ExtractMethodBody(source, "private static RunModifierController ResolveActiveRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsRunModifierRuntimeUsable(");

            StringAssert.Contains("RunModifierController runtime = ResolveActiveRuntime();", nightmareProperty);
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            Assert.Less(
                awake.IndexOf("if (TryAbortForUsableExistingRuntime())", StringComparison.Ordinal),
                awake.IndexOf("ResetForCurrentContext();", StringComparison.Ordinal));
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", onEnable);
            Assert.Less(
                onEnable.IndexOf("if (TryAbortForUsableExistingRuntime())", StringComparison.Ordinal),
                onEnable.IndexOf("TryRegisterService();", StringComparison.Ordinal));
            Assert.Less(
                onEnable.IndexOf("if (TryAbortForUsableExistingRuntime())", StringComparison.Ordinal),
                onEnable.IndexOf("TryRegisterSaveOwner();", StringComparison.Ordinal));
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            Assert.Less(
                register.IndexOf("if (TryAbortForUsableExistingRuntime())", StringComparison.Ordinal),
                register.IndexOf("GlobalRegistry.RegisterRunModifierRuntime(this);", StringComparison.Ordinal));
            StringAssert.Contains("RunModifierController registered = GlobalRegistry.RunModifiers", gate);
            StringAssert.Contains("ReferenceEquals(registered, null)", gate);
            StringAssert.Contains("ReferenceEquals(registered, this)", gate);
            StringAssert.Contains("if (IsRunModifierRuntimeUsable(registered))", gate);
            StringAssert.Contains("Destroy(gameObject);", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterRunModifierRuntime(registered);", gate);
            StringAssert.Contains("RunModifierController registered = GlobalRegistry.RunModifiers", resolve);
            StringAssert.Contains("if (IsRunModifierRuntimeUsable(registered))", resolve);
            StringAssert.Contains("GlobalRegistry.UnregisterRunModifierRuntime(registered);", resolve);
            StringAssert.Contains("controller._serviceRegistered", usable);
            StringAssert.Contains("controller.isActiveAndEnabled", usable);
            StringAssert.DoesNotContain("registered != null && registered != this", source);
            StringAssert.DoesNotContain("RunModifierController runtime = GlobalRegistry.RunModifiers", source);
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
