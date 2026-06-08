using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class NarrativeToolRuntimeOwnerEditTests
    {
        [Test]
        public void CorporateOrderSystem_RuntimeOwnerGateClearsStaleRegistryBeforeSaveAndTickRouting()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "Narrative", "CorporateOrderSystem.cs"));
            string awake = ExtractMethodBody(source, "private void Awake()");
            string register = ExtractMethodBody(source, "private bool TryRegisterRuntime()");
            string saveRegister = ExtractMethodBody(source, "private void TryRegisterSaveParticipant()");
            string saveUnregister = ExtractMethodBody(source, "private void TryUnregisterSaveParticipant()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsCorporateOrderRuntimeUsable(");
            string saveUsable = ExtractMethodBody(source, "private static bool IsSaveServiceUsable(");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterCorporateOrderRuntime(this);");
            StringAssert.Contains("CorporateOrderSystem registered = GlobalRegistry.CorporateOrders", gate);
            StringAssert.Contains("ReferenceEquals(registered, null)", gate);
            StringAssert.Contains("ReferenceEquals(registered, this)", gate);
            StringAssert.Contains("if (IsCorporateOrderRuntimeUsable(registered))", gate);
            StringAssert.Contains("Destroy(gameObject);", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterCorporateOrderRuntime(registered);", gate);
            StringAssert.Contains("system._runtimeRegistered", usable);
            StringAssert.Contains("system.isActiveAndEnabled", usable);
            AssertInitializedSaveOwnerRegistrationGate(
                saveRegister,
                saveUsable,
                "_saveService",
                "saveService.Register(this);",
                "_saveRegistered = true;");
            AssertRegisteredSaveOwnerUnregister(source, saveUnregister, "_saveService", "_saveRegistered");
            StringAssert.DoesNotContain("registered != null && registered != this", source);
        }

        [Test]
        public void NarrativeLogSaveableOwners_DelaySaveRegistrationUntilSaveOwnerInitialized()
        {
            string fieldOperations = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "FieldOperationLogSystem.cs"));
            string scanLog = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "ScanLogSystem.cs"));
            string proceduralLore = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "Narrative", "ProceduralLoreDirector.cs"));

            AssertInitializedSaveOwnerRegistrationGate(
                ExtractMethodBody(fieldOperations, "private void TryRegisterSaveParticipant()"),
                ExtractMethodBody(fieldOperations, "private static bool IsSaveServiceUsable("),
                "_saveService",
                "saveService.Register(this);",
                "_saveRegistered = true;");
            AssertRegisteredSaveOwnerUnregister(
                fieldOperations,
                ExtractMethodBody(fieldOperations, "private void TryUnregisterSaveParticipant()"),
                "_saveService",
                "_saveRegistered");

            AssertInitializedSaveOwnerRegistrationGate(
                ExtractMethodBody(scanLog, "private void TryRegisterSaveParticipant()"),
                ExtractMethodBody(scanLog, "private static bool IsSaveServiceUsable("),
                "_saveService",
                "saveService.Register(this);",
                "_saveRegistered = true;");
            AssertRegisteredSaveOwnerUnregister(
                scanLog,
                ExtractMethodBody(scanLog, "private void TryUnregisterSaveParticipant()"),
                "_saveService",
                "_saveRegistered");

            AssertInitializedSaveOwnerRegistrationGate(
                ExtractMethodBody(proceduralLore, "private void TryRegisterWithSaveManager()"),
                ExtractMethodBody(proceduralLore, "private static bool IsSaveServiceUsable("),
                "_saveService",
                "saveService.Register(this);",
                "_registeredToSave = true;");
            AssertRegisteredSaveOwnerUnregister(
                proceduralLore,
                ExtractMethodBody(proceduralLore, "private void UnregisterFromSaveManager()"),
                "_saveService",
                "_registeredToSave");
        }

        [Test]
        public void ToolDurabilitySystem_RuntimeOwnerGateClearsStaleRegistryBeforeNativeStateAndSaveRouting()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "Tools", "ToolDurabilitySystem.cs"));
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string start = ExtractMethodBody(source, "private void Start()");
            string register = ExtractMethodBody(source, "private void TryRegisterService()");
            string saveRegister = ExtractMethodBody(source, "private void TryRegisterSaveService()");
            string saveUnregister = ExtractMethodBody(source, "private void TryUnregisterSaveService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsToolDurabilityRuntimeUsable(");
            string saveUsable = ExtractMethodBody(source, "private static bool IsSaveServiceUsable(");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "CacheRegistryDependenciesCold();");
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "EnsureNativeStateCold();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", onEnable);
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "EnsureNativeStateCold();");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "TryRegisterSaveService();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", start);
            AssertTextBefore(start, "if (TryAbortForUsableExistingRuntime())", "EnsureNativeStateCold();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterToolDurabilityRuntime(this);");
            StringAssert.Contains("ToolDurabilitySystem registered = GlobalRegistry.ToolDurability", gate);
            StringAssert.Contains("ReferenceEquals(registered, null)", gate);
            StringAssert.Contains("ReferenceEquals(registered, this)", gate);
            StringAssert.Contains("if (IsToolDurabilityRuntimeUsable(registered))", gate);
            StringAssert.Contains("Destroy(gameObject);", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterToolDurabilityRuntime(registered);", gate);
            StringAssert.Contains("system._serviceRegistered", usable);
            StringAssert.Contains("system.isActiveAndEnabled", usable);
            StringAssert.Contains("if (_saveRegistered || !IsSaveServiceUsable(saveService))", saveRegister);
            Assert.IsTrue(ContainsTokensInOrder(
                saveRegister,
                "ISaveService saveService = _saveService;",
                "if (!IsSaveServiceUsable(saveService))",
                "saveService = GlobalRegistry.Save;",
                "_saveService = saveService;",
                "if (_saveRegistered || !IsSaveServiceUsable(saveService))",
                "return;",
                "saveService.Register(this);",
                "_registeredSaveService = saveService;",
                "_saveRegistered = true;"));
            AssertTextBefore(saveRegister, "if (_saveRegistered || !IsSaveServiceUsable(saveService))", "saveService.Register(this);");
            AssertRegisteredSaveOwnerUnregister(source, saveUnregister, "_saveService", "_saveRegistered");
            StringAssert.Contains("return saveService != null && saveService.IsInitialized;", saveUsable);
            StringAssert.DoesNotContain("if (_saveRegistered || saveService == null)", source);
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

        private static void AssertInitializedSaveOwnerRegistrationGate(
            string register,
            string usable,
            string saveServiceField,
            string registerCall,
            string registeredFlagAssignment)
        {
            Assert.IsTrue(ContainsTokensInOrder(
                register,
                "ISaveService saveService = " + saveServiceField + ";",
                "if (!IsSaveServiceUsable(saveService))",
                "saveService = GlobalRegistry.Save;",
                saveServiceField + " = saveService;",
                "if (!IsSaveServiceUsable(saveService))",
                "return;",
                registerCall,
                "_registeredSaveService = saveService;",
                registeredFlagAssignment));
            StringAssert.Contains("return saveService != null && saveService.IsInitialized;", usable);
            StringAssert.DoesNotContain("if (" + saveServiceField + " == null)", register);
            StringAssert.DoesNotContain("if (saveService == null)", register);
        }

        private static void AssertRegisteredSaveOwnerUnregister(
            string source,
            string unregister,
            string saveServiceField,
            string registeredFlagName)
        {
            StringAssert.Contains("private ISaveService _registeredSaveService;", source);
            Assert.IsTrue(ContainsTokensInOrder(
                unregister,
                "if (!" + registeredFlagName + " && _registeredSaveService == null)",
                "return;",
                "ISaveService saveService = _registeredSaveService != null ? _registeredSaveService : " + saveServiceField + ";",
                "if (saveService != null)",
                "saveService.Unregister(this);",
                "_registeredSaveService = null;",
                registeredFlagName + " = false;"));
            StringAssert.DoesNotContain("ISaveService saveService = " + saveServiceField + ";", unregister);
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
