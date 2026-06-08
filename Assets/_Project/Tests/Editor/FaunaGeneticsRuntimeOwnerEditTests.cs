using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class FaunaGeneticsRuntimeOwnerEditTests
    {
        [Test]
        public void FaunaGeneticsManager_RuntimeOwnerGateClearsStaleRegistryOwnerBeforeSeedAndSaveRouting()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "Ecosystem", "FaunaGeneticsManager.cs"));
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string register = ExtractMethodBody(source, "private void TryRegisterService()");
            string saveRegister = ExtractMethodBody(source, "private void TryRegisterSaveParticipant()");
            string saveUnregister = ExtractMethodBody(source, "private void TryUnregisterSaveParticipant()");
            string saveUsable = ExtractMethodBody(source, "private static bool IsSaveServiceUsable(");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsFaunaGeneticsRuntimeUsable(");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            Assert.Less(
                awake.IndexOf("if (TryAbortForUsableExistingRuntime())", StringComparison.Ordinal),
                awake.IndexOf("CacheRunModifiersCold();", StringComparison.Ordinal));
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", onEnable);
            Assert.Less(
                onEnable.IndexOf("if (TryAbortForUsableExistingRuntime())", StringComparison.Ordinal),
                onEnable.IndexOf("TryRegisterService();", StringComparison.Ordinal));
            Assert.Less(
                onEnable.IndexOf("if (TryAbortForUsableExistingRuntime())", StringComparison.Ordinal),
                onEnable.IndexOf("TryRegisterSaveParticipant();", StringComparison.Ordinal));
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            Assert.Less(
                register.IndexOf("if (TryAbortForUsableExistingRuntime())", StringComparison.Ordinal),
                register.IndexOf("GlobalRegistry.RegisterFaunaGeneticsRuntime(this);", StringComparison.Ordinal));
            StringAssert.Contains("FaunaGeneticsManager registered = GlobalRegistry.FaunaGenetics", gate);
            StringAssert.Contains("ReferenceEquals(registered, null)", gate);
            StringAssert.Contains("ReferenceEquals(registered, this)", gate);
            StringAssert.Contains("if (IsFaunaGeneticsRuntimeUsable(registered))", gate);
            StringAssert.Contains("SuppressDuplicateService();", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterFaunaGeneticsRuntime(registered);", gate);
            StringAssert.Contains("manager._serviceRegistered", usable);
            StringAssert.Contains("!manager._duplicateServiceSuppressed", usable);
            StringAssert.Contains("manager.isActiveAndEnabled", usable);
            AssertInitializedSaveOwnerRegistrationGate(saveRegister, saveUsable);
            AssertRegisteredSaveOwnerUnregister(source, saveUnregister);
            StringAssert.DoesNotContain("_saveService?.Register(this)", source);
            StringAssert.DoesNotContain("registered != null && registered != this", source);
        }

        private static void AssertInitializedSaveOwnerRegistrationGate(string register, string usable)
        {
            Assert.IsTrue(ContainsTokensInOrder(
                register,
                "ISaveService saveService = _saveService;",
                "if (!IsSaveServiceUsable(saveService))",
                "saveService = GlobalRegistry.Save;",
                "_saveService = saveService;",
                "if (!IsSaveServiceUsable(saveService))",
                "return;",
                "saveService.Register(this);",
                "_registeredSaveService = saveService;",
                "_saveRegistered = true;"));
            StringAssert.Contains("return saveService != null && saveService.IsInitialized;", usable);
            StringAssert.DoesNotContain("if (_saveService == null)", register);
            StringAssert.DoesNotContain("if (saveService == null)", register);
        }

        private static void AssertRegisteredSaveOwnerUnregister(string source, string unregister)
        {
            StringAssert.Contains("private ISaveService _registeredSaveService;", source);
            Assert.IsTrue(ContainsTokensInOrder(
                unregister,
                "if (!_saveRegistered && _registeredSaveService == null)",
                "return;",
                "ISaveService saveService = _registeredSaveService != null ? _registeredSaveService : _saveService;",
                "if (saveService != null)",
                "saveService.Unregister(this);",
                "_registeredSaveService = null;",
                "_saveRegistered = false;"));
            StringAssert.DoesNotContain("ISaveService saveService = _saveService;", unregister);
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
