using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class ScrapRuntimeOwnerEditTests
    {
        [Test]
        public void ScrapManager_RuntimeOwnerGateClearsStaleMirrorAndRegistryOwnersBeforeEconomyRouting()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "Economy", "ScrapManager.cs"));
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string register = ExtractMethodBody(source, "private void TryRegisterToGlobalRegistry()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsScrapRuntimeUsable(");

            StringAssert.Contains("TryAbortForUsableExistingRuntime();", awake);
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", onEnable);
            Assert.Less(
                onEnable.IndexOf("if (TryAbortForUsableExistingRuntime())", StringComparison.Ordinal),
                onEnable.IndexOf("CacheRegistryServicesCold();", StringComparison.Ordinal));
            Assert.Less(
                onEnable.IndexOf("if (TryAbortForUsableExistingRuntime())", StringComparison.Ordinal),
                onEnable.IndexOf("TryRegisterHotSwapListener();", StringComparison.Ordinal));
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            Assert.Less(
                register.IndexOf("if (TryAbortForUsableExistingRuntime())", StringComparison.Ordinal),
                register.IndexOf("GlobalRegistry.RegisterScrapRuntime(this);", StringComparison.Ordinal));
            StringAssert.Contains("ScrapManager active = s_activeRuntimeInstance", gate);
            StringAssert.Contains("ScrapManager registered = GlobalRegistry.Scrap", gate);
            StringAssert.Contains("if (IsScrapRuntimeUsable(active))", gate);
            StringAssert.Contains("if (IsScrapRuntimeUsable(registered))", gate);
            StringAssert.Contains("s_activeRuntimeInstance = null", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterScrapRuntime(active);", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterScrapRuntime(registered);", gate);
            StringAssert.Contains("manager._serviceRegistered", usable);
            StringAssert.Contains("manager.isActiveAndEnabled", usable);
            StringAssert.DoesNotContain("registered != null && registered != this", source);
            StringAssert.DoesNotContain("s_activeRuntimeInstance ?? GlobalRegistry.Scrap", source);
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
