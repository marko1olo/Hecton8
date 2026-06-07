using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class BasePollutionRuntimeOwnerEditTests
    {
        [Test]
        public void BasePollutionManager_RuntimeOwnerGateClearsStaleMirrorAndRegistryBeforeEnvironmentalRouting()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "World", "BasePollutionManager.cs"));
            string noiseProperty = ExtractMethodBody(source, "public static float CurrentNoiseLevel");
            string microplasticProperty = ExtractMethodBody(source, "public static float CurrentMicroplasticLevel");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string register = ExtractMethodBody(source, "private void TryRegisterService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string resolve = ExtractMethodBody(source, "private static BasePollutionManager ResolveActiveRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsBasePollutionRuntimeUsable(");

            StringAssert.Contains("BasePollutionManager runtime = ResolveActiveRuntime();", noiseProperty);
            StringAssert.Contains("BasePollutionManager runtime = ResolveActiveRuntime();", microplasticProperty);
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", onEnable);
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "TryRegisterService();");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "CacheEnvironmentalStrain(GlobalRegistry.EnvironmentalStrainIndustrialSink);");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterBasePollutionRuntime(this);");
            StringAssert.Contains("BasePollutionManager active = s_activeRuntime", gate);
            StringAssert.Contains("BasePollutionManager registered = GlobalRegistry.BasePollution", gate);
            StringAssert.Contains("if (IsBasePollutionRuntimeUsable(active))", gate);
            StringAssert.Contains("if (IsBasePollutionRuntimeUsable(registered))", gate);
            StringAssert.Contains("Destroy(gameObject);", gate);
            StringAssert.Contains("s_activeRuntime = null", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterBasePollutionRuntime(active);", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterBasePollutionRuntime(registered);", gate);
            StringAssert.Contains("if (IsBasePollutionRuntimeUsable(active))", resolve);
            StringAssert.Contains("if (IsBasePollutionRuntimeUsable(registered))", resolve);
            StringAssert.Contains("s_activeRuntime = registered", resolve);
            StringAssert.Contains("GlobalRegistry.UnregisterBasePollutionRuntime(registered);", resolve);
            StringAssert.Contains("manager._serviceRegistered", usable);
            StringAssert.Contains("manager.isActiveAndEnabled", usable);
            StringAssert.DoesNotContain("registered != null && registered != this", source);
            StringAssert.DoesNotContain("BasePollutionManager runtime = s_activeRuntime", source);
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
