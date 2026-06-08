using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class StateRuntimeOwnerEditTests
    {
        [Test]
        public void WorldStateManager_RuntimeOwnerGateClearsStaleRegistryOwnerBeforePersistenceInit()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "WorldStateManager.cs"));
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string register = ExtractMethodBody(source, "private void TryRegisterService()");
            string saveRegister = ExtractMethodBody(source, "private void TryRegisterSaveParticipant()");
            string saveUnregister = ExtractMethodBody(source, "private void TryUnregisterSaveParticipant()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsWorldStateRuntimeUsable(");
            string saveUsable = ExtractMethodBody(source, "private static bool IsSaveServiceUsable(");
            string replaced = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            Assert.Less(
                awake.IndexOf("if (TryAbortForUsableExistingRuntime())", StringComparison.Ordinal),
                awake.IndexOf("TryRegisterService();", StringComparison.Ordinal));
            Assert.Less(
                awake.IndexOf("if (TryAbortForUsableExistingRuntime())", StringComparison.Ordinal),
                awake.IndexOf("GameBootstrapper.PersistRuntimeService(this);", StringComparison.Ordinal));
            Assert.Less(
                awake.IndexOf("if (TryAbortForUsableExistingRuntime())", StringComparison.Ordinal),
                awake.IndexOf("_depletedNodeIds = new HashSet<string>(initialCapacity);", StringComparison.Ordinal));
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            Assert.Less(
                register.IndexOf("if (TryAbortForUsableExistingRuntime())", StringComparison.Ordinal),
                register.IndexOf("GlobalRegistry.RegisterWorldStateRuntime(this);", StringComparison.Ordinal));
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", onEnable);
            Assert.Less(
                onEnable.IndexOf("if (TryAbortForUsableExistingRuntime())", StringComparison.Ordinal),
                onEnable.IndexOf("TryRegisterService();", StringComparison.Ordinal));
            Assert.Less(
                onEnable.IndexOf("if (TryAbortForUsableExistingRuntime())", StringComparison.Ordinal),
                onEnable.IndexOf("TryRegisterSaveParticipant();", StringComparison.Ordinal));
            StringAssert.Contains("WorldStateManager registered = GlobalRegistry.WorldState", gate);
            StringAssert.Contains("ReferenceEquals(registered, null)", gate);
            StringAssert.Contains("ReferenceEquals(registered, this)", gate);
            StringAssert.Contains("if (IsWorldStateRuntimeUsable(registered))", gate);
            StringAssert.Contains("Destroy(gameObject);", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterWorldStateRuntime(registered);", gate);
            StringAssert.Contains("manager._serviceRegistered", usable);
            StringAssert.Contains("manager.isActiveAndEnabled", usable);
            AssertTextBefore(replaced, "TryUnregisterSaveParticipant();", "_saveService = currentService as ISaveService;");
            AssertTextBefore(replaced, "_saveService = currentService as ISaveService;", "TryRegisterSaveParticipant();");
            Assert.IsTrue(ContainsTokensInOrder(
                saveRegister,
                "ISaveService saveService = _saveService;",
                "if (!IsSaveServiceUsable(saveService))",
                "saveService = GlobalRegistry.Save;",
                "_saveService = saveService;",
                "if (!IsSaveServiceUsable(saveService))",
                "return;",
                "saveService.Register(this);",
                "_registeredSaveService = saveService;",
                "_saveRegistered = true;"));
            AssertRegisteredSaveOwnerUnregister(source, saveUnregister);
            StringAssert.Contains("return saveService != null && saveService.IsInitialized;", saveUsable);
            StringAssert.DoesNotContain("if (_saveService == null)", saveRegister);
            StringAssert.DoesNotContain("_saveService.Register(this)", source);
            StringAssert.DoesNotContain("registered != null && registered != this", awake);
            StringAssert.DoesNotContain("registered != null && registered != this", register);
        }

        [Test]
        public void SeamRegistry_SaveBridgeRequiresInitializedOwnerBeforeGeologyStateRegistration()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "SeamRegistry.cs"));
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string replaced = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string saveRegister = ExtractMethodBody(source, "private void TryRegisterSaveParticipant()");
            string saveUnregister = ExtractMethodBody(source, "private void TryUnregisterSaveParticipant()");
            string saveUsable = ExtractMethodBody(source, "private static bool IsSaveServiceUsable(");

            AssertTextBefore(onEnable, "CacheRegistryServicesCold();", "TryRegisterSaveParticipant();");
            AssertTextBefore(replaced, "TryUnregisterSaveParticipant();", "_saveService = currentService as ISaveService;");
            AssertTextBefore(replaced, "_saveService = currentService as ISaveService;", "TryRegisterSaveParticipant();");
            Assert.IsTrue(ContainsTokensInOrder(
                saveRegister,
                "ISaveService saveService = _saveService;",
                "if (!IsSaveServiceUsable(saveService))",
                "saveService = GlobalRegistry.Save;",
                "_saveService = saveService;",
                "if (!IsSaveServiceUsable(saveService))",
                "return;",
                "saveService.Register(this);",
                "_registeredSaveService = saveService;",
                "_saveRegistered = true;"));
            AssertRegisteredSaveOwnerUnregister(source, saveUnregister);
            StringAssert.Contains("return saveService != null && saveService.IsInitialized;", saveUsable);
            StringAssert.DoesNotContain("if (saveService == null)", saveRegister);
        }

        [Test]
        public void WorldProceduralStateRegistry_SaveBridgeTracksRegisteredOwnerAcrossSaveReplacement()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "WorldProceduralStateRegistry.cs"));
            string replaced = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string saveRegister = ExtractMethodBody(source, "private void TryRegisterSaveParticipant()");
            string saveUnregister = ExtractMethodBody(source, "private void TryUnregisterSaveParticipant()");
            string saveUsable = ExtractMethodBody(source, "private static bool IsSaveServiceUsable(");

            AssertTextBefore(replaced, "TryUnregisterSaveParticipant();", "_saveService = currentService as ISaveService;");
            AssertTextBefore(replaced, "_saveService = currentService as ISaveService;", "TryRegisterSaveParticipant();");
            Assert.IsTrue(ContainsTokensInOrder(
                saveRegister,
                "ISaveService saveService = _saveService;",
                "if (!IsSaveServiceUsable(saveService))",
                "saveService = GlobalRegistry.Save;",
                "_saveService = saveService;",
                "if (!IsSaveServiceUsable(saveService))",
                "return;",
                "saveService.Register(this);",
                "_registeredSaveService = saveService;",
                "_saveRegistered = true;"));
            AssertRegisteredSaveOwnerUnregister(source, saveUnregister);
            StringAssert.Contains("return saveService != null && saveService.IsInitialized;", saveUsable);
            StringAssert.DoesNotContain("if (saveService == null)", saveRegister);
        }

        [Test]
        public void EntityChangeManager_RuntimeOwnerGateClearsStaleRegistryOwnerBeforeClaimingService()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "EntityChangeDetector.cs"));
            string awake = ExtractMethodBody(source, "private void Awake()");
            string register = ExtractMethodBody(source, "private void TryRegisterService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsEntityChangeRuntimeUsable(");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            Assert.Less(
                register.IndexOf("if (TryAbortForUsableExistingRuntime())", StringComparison.Ordinal),
                register.IndexOf("GlobalRegistry.RegisterEntityChangeManagerRuntime(this);", StringComparison.Ordinal));
            StringAssert.Contains("EntityChangeManager registered = GlobalRegistry.EntityChanges", gate);
            StringAssert.Contains("ReferenceEquals(registered, null)", gate);
            StringAssert.Contains("ReferenceEquals(registered, this)", gate);
            StringAssert.Contains("if (IsEntityChangeRuntimeUsable(registered))", gate);
            StringAssert.Contains("Destroy(gameObject);", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterEntityChangeManagerRuntime(registered);", gate);
            StringAssert.Contains("manager._serviceRegistered", usable);
            StringAssert.Contains("manager.isActiveAndEnabled", usable);
            StringAssert.DoesNotContain("registered != null && registered != this", awake);
            StringAssert.DoesNotContain("registered != null && registered != this", register);
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
