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
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string register = ExtractMethodBody(source, "private void TryRegisterService()");
            string saveRegister = ExtractMethodBody(source, "private void TryRegisterSaveService()");
            string saveUnregister = ExtractMethodBody(source, "private void TryUnregisterSaveService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string resolve = ExtractMethodBody(source, "private static EnvironmentalStrainManager ResolveActiveRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsEnvironmentalStrainRuntimeUsable(");
            string saveUsable = ExtractMethodBody(source, "private static bool IsSaveServiceUsable(");
            string suppressed = ExtractMethodBody(source, "private void SuppressDuplicateService()");
            string replaced = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");

            StringAssert.Contains("EnvironmentalStrainManager registered = ResolveActiveRuntime();", aggressionProperty);
            StringAssert.Contains("EnvironmentalStrainManager registered = ResolveActiveRuntime();", sectorQuery);
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", onEnable);
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "TryRegisterService();");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "CacheSaveServiceCold();");
            StringAssert.Contains("TryRegisterSaveService();", onEnable);
            StringAssert.Contains("TryUnregisterSaveService();", onDisable);
            StringAssert.Contains("TryUnregisterSaveService();", onDestroy);
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

            StringAssert.Contains("TryUnregisterSaveService();", suppressed);
            Assert.IsTrue(ContainsTokensInOrder(
                saveRegister,
                "if (_saveRegistered || _duplicateServiceSuppressed)",
                "ISaveService saveService = _saveService;",
                "if (!IsSaveServiceUsable(saveService))",
                "saveService = GlobalRegistry.Save;",
                "_saveService = saveService;",
                "if (!IsSaveServiceUsable(saveService))",
                "return;",
                "saveService.Register(this);",
                "_registeredSaveService = saveService;",
                "_saveRegistered = true;"));
            Assert.IsTrue(ContainsTokensInOrder(
                saveUnregister,
                "if (!_saveRegistered && _registeredSaveService == null)",
                "_saveService = null;",
                "return;",
                "ISaveService saveService = _registeredSaveService != null ? _registeredSaveService : _saveService;",
                "if (saveService != null)",
                "saveService.Unregister(this);",
                "_registeredSaveService = null;",
                "_saveRegistered = false;",
                "_saveService = null;"));
            StringAssert.Contains("private ISaveService _registeredSaveService;", source);
            StringAssert.Contains("return saveService != null && saveService.IsInitialized;", saveUsable);
            StringAssert.DoesNotContain("ISaveService saveService = _saveService;", saveUnregister);
            AssertTextBefore(replaced, "TryUnregisterSaveService();", "_saveService = currentService as ISaveService;");
            AssertTextBefore(replaced, "_saveService = currentService as ISaveService;", "TryRegisterSaveService();");
            StringAssert.DoesNotContain("_saveService?.Register(this)", source);
            StringAssert.DoesNotContain("if (Application.isPlaying && previousService is ISaveService previousSave)", source);
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

        private static bool ContainsTokensInOrder(string text, params string[] tokens)
        {
            int index = 0;
            for (int i = 0; i < tokens.Length; i++)
            {
                int found = text.IndexOf(tokens[i], index, StringComparison.Ordinal);
                if (found < 0)
                    return false;

                index = found + tokens[i].Length;
            }

            return true;
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
