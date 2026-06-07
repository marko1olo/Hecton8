using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class EnvironmentalStrainRuntimeOwnerEditTests
    {
        [Test]
        public void EnvironmentalStrainManager_RuntimeOwnerGateClearsStaleMirrorAndRegistryBeforeSaveAndSinkRouting()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "World", "EnvironmentalStrainManager.cs"));
            string aggressionProperty = ExtractMethodBody(source, "public static float CurrentPredatorAggressionScale");
            string sectorQuery = ExtractMethodBody(source, "public static bool TryGetSectorStrain01(");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string register = ExtractMethodBody(source, "private void TryRegisterService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string resolve = ExtractMethodBody(source, "private static EnvironmentalStrainManager ResolveActiveRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsEnvironmentalStrainRuntimeUsable(");

            StringAssert.Contains("EnvironmentalStrainManager registered = ResolveActiveRuntime();", aggressionProperty);
            StringAssert.Contains("EnvironmentalStrainManager registered = ResolveActiveRuntime();", sectorQuery);
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", onEnable);
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "TryRegisterService();");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "CacheSaveServiceCold();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterEnvironmentalStrainRuntime(this);");
            StringAssert.Contains("EnvironmentalStrainManager registered = s_activeRuntimeInstance", gate);
            StringAssert.Contains("EnvironmentalStrainManager globalRegistered = GlobalRegistry.EnvironmentalStrain", gate);
            StringAssert.Contains("if (IsEnvironmentalStrainRuntimeUsable(registered))", gate);
            StringAssert.Contains("if (IsEnvironmentalStrainRuntimeUsable(globalRegistered))", gate);
            StringAssert.Contains("SuppressDuplicateService();", gate);
            StringAssert.Contains("s_activeRuntimeInstance = null", gate);
            StringAssert.Contains("s_activeRuntimeInstance = globalRegistered", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterEnvironmentalStrainRuntime(registered);", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterEnvironmentalStrainRuntime(globalRegistered);", gate);
            StringAssert.Contains("EnvironmentalStrainManager registered = s_activeRuntimeInstance", resolve);
            StringAssert.Contains("EnvironmentalStrainManager globalRegistered = GlobalRegistry.EnvironmentalStrain", resolve);
            StringAssert.Contains("if (IsEnvironmentalStrainRuntimeUsable(registered))", resolve);
            StringAssert.Contains("if (IsEnvironmentalStrainRuntimeUsable(globalRegistered))", resolve);
            StringAssert.Contains("s_activeRuntimeInstance = globalRegistered", resolve);
            StringAssert.Contains("GlobalRegistry.UnregisterEnvironmentalStrainRuntime(globalRegistered);", resolve);
            StringAssert.Contains("manager._serviceRegistered", usable);
            StringAssert.Contains("!manager._duplicateServiceSuppressed", usable);
            StringAssert.Contains("manager.isActiveAndEnabled", usable);
            StringAssert.DoesNotContain("registered != null && registered != this", source);
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
