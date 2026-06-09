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

        [Test]
        public void RunModifierController_PermadeathDeleteUsesInitializedSaveOwnerAndSafeSlot()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "Meta", "RunModifierController.cs"));
            string tryDelete = ExtractMethodBody(source, "private void TryDeleteCurrentSlot()");
            string resolveSlot = ExtractMethodBody(source, "private string ResolveCurrentSlotName()");
            string registerSaveOwner = ExtractMethodBody(source, "private void TryRegisterSaveOwner()");
            string unregisterSaveOwner = ExtractMethodBody(source, "private void TryUnregisterSaveOwner()");
            string hotSwap = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string saveServiceUsable = ExtractMethodBody(source, "private static bool IsSaveServiceUsable(");
            string saveManagerUsable = ExtractMethodBody(source, "private static bool IsSaveManagerUsable(");

            StringAssert.Contains("if (!IsSaveManagerUsable(saveManager))", tryDelete);
            StringAssert.Contains("if (!SaveManager.TryResolveSafeSlotName(slotName, out slotName))", tryDelete);
            Assert.Less(
                tryDelete.IndexOf("if (!IsSaveManagerUsable(saveManager))", StringComparison.Ordinal),
                tryDelete.IndexOf("saveManager.DeleteSave(slotName);", StringComparison.Ordinal));
            Assert.Less(
                tryDelete.IndexOf("if (!SaveManager.TryResolveSafeSlotName(slotName, out slotName))", StringComparison.Ordinal),
                tryDelete.IndexOf("saveManager.DeleteSave(slotName);", StringComparison.Ordinal));

            StringAssert.Contains("SaveManager.TryResolveSafeSlotName(context.TargetSaveSlot, out string safeContextSlotName)", resolveSlot);
            StringAssert.Contains("return safeContextSlotName;", resolveSlot);
            StringAssert.Contains("IsSaveManagerUsable(saveManager)", resolveSlot);
            StringAssert.Contains("SaveManager.TryResolveSafeSlotName(saveManager.LastOperationSlot, out string safeLastOperationSlot)", resolveSlot);
            StringAssert.Contains("return safeLastOperationSlot;", resolveSlot);
            StringAssert.DoesNotContain("return context.TargetSaveSlot;", resolveSlot);
            StringAssert.DoesNotContain("return saveManager.LastOperationSlot;", resolveSlot);

            StringAssert.Contains("if (!IsSaveServiceUsable(saveService))", registerSaveOwner);
            Assert.IsTrue(ContainsTokensInOrder(
                registerSaveOwner,
                "ISaveService saveService = _saveService;",
                "if (!IsSaveServiceUsable(saveService))",
                "saveService = GlobalRegistry.Save;",
                "_saveService = saveService;",
                "_saveManager = saveService as SaveManager;",
                "if (!IsSaveServiceUsable(saveService))",
                "return;",
                "saveService.Register(this);",
                "_registeredSaveService = saveService;",
                "_saveRegistered = true;"));
            StringAssert.Contains("ISaveService saveService = _registeredSaveService != null ? _registeredSaveService : _saveService;", unregisterSaveOwner);
            StringAssert.Contains("_registeredSaveService = null;", unregisterSaveOwner);
            StringAssert.DoesNotContain("_saveService?.Unregister(this);", unregisterSaveOwner);
            AssertTextBefore(hotSwap, "TryUnregisterSaveOwner();", "_saveService = currentService as ISaveService;");
            StringAssert.DoesNotContain("previousService is ISaveService previousSave", hotSwap);
            StringAssert.Contains("return saveService != null && saveService.IsInitialized;", saveServiceUsable);
            StringAssert.Contains("return saveManager != null && saveManager.IsInitialized;", saveManagerUsable);
        }

        [Test]
        public void RunModifierController_PermadeathDeathRouteUsesPlayerSurvivalSource()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "Meta", "RunModifierController.cs"));
            string hotSwap = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string refreshCold = ExtractMethodBody(source, "private void RefreshColdRegistryDependencies()");
            string registerUpdate = ExtractMethodBody(source, "private void TryRegisterWithUpdateDispatcher()");
            string consume = ExtractMethodBody(source, "private void ConsumeSurvivalDeathSignal()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string shutdown = ExtractMethodBody(source, "public void OnServiceShutdown()");
            string cachePlayer = ExtractMethodBody(source, "private void CachePlayerRuntimeContext(IPlayerRuntimeContext playerContext)");
            string clearPlayer = ExtractMethodBody(source, "private void ClearPlayerRuntimeContext()");
            string refreshSurvival = ExtractMethodBody(source, "private void RefreshSurvivalSignalBinding()");
            string resolveSource = ExtractMethodBody(source, "private static uint ResolveSurvivalSignalSourceId(HectonSurvivalSystem system)");

            StringAssert.Contains("using Hecton8.Gameplay;", source);
            StringAssert.Contains("private IPlayerRuntimeContext _playerRuntimeContext;", source);
            StringAssert.Contains("private HectonSurvivalSystem _survivalSystem;", source);
            StringAssert.Contains("private uint _survivalSignalSourceId;", source);

            StringAssert.Contains("if (serviceSlot == GlobalRegistryServiceSlot.Player)", hotSwap);
            StringAssert.Contains("CachePlayerRuntimeContext(currentService as IPlayerRuntimeContext);", hotSwap);
            StringAssert.Contains("RefreshSurvivalSignalBinding();", hotSwap);
            AssertTextBefore(
                hotSwap,
                "if (serviceSlot == GlobalRegistryServiceSlot.Player)",
                "if (serviceSlot != GlobalRegistryServiceSlot.Save)");

            StringAssert.Contains("CachePlayerRuntimeContext(GlobalRegistry.Player);", refreshCold);
            StringAssert.Contains("RefreshSurvivalSignalBinding();", refreshCold);
            AssertTextBefore(refreshCold, "CachePlayerRuntimeContext(GlobalRegistry.Player);", "RefreshSurvivalSignalBinding();");

            StringAssert.Contains("uint sourceId = _survivalSignalSourceId;", registerUpdate);
            StringAssert.Contains("sourceId != 0u &&", registerUpdate);
            StringAssert.Contains("SurvivalSignalRoute.TryGetLatestDeathForSource(sourceId, out _, out int sequence)", registerUpdate);
            StringAssert.DoesNotContain("SurvivalSignalRoute.TryGetLatestDeath(out _, out int sequence)", registerUpdate);

            StringAssert.Contains("uint sourceId = _survivalSignalSourceId;", consume);
            StringAssert.Contains("if (sourceId == 0u)", consume);
            StringAssert.Contains("SurvivalSignalRoute.TryGetLatestDeathForSource(sourceId, out SurvivalVitalsChangedSignal signal, out int sequence)", consume);
            StringAssert.Contains("signal.SourceId != sourceId", consume);
            StringAssert.Contains("SurvivalVitalsChangedSignalFlags.Death", consume);
            StringAssert.Contains("HandlePlayerDied();", consume);
            StringAssert.DoesNotContain("SurvivalSignalRoute.TryGetLatestDeath(out _, out int sequence)", consume);
            AssertTextBefore(consume, "_lastSurvivalDeathSignalSequence = sequence;", "if (signal.SourceId != sourceId");
            AssertTextBefore(consume, "if (signal.SourceId != sourceId", "HandlePlayerDied();");

            StringAssert.Contains("_playerRuntimeContext = playerContext != null && playerContext.IsInitialized ? playerContext : null;", cachePlayer);
            StringAssert.Contains("_survivalSystem = _playerRuntimeContext != null ? _playerRuntimeContext.SurvivalSystem : null;", cachePlayer);

            StringAssert.Contains("ClearPlayerRuntimeContext();", onDisable);
            StringAssert.Contains("ClearPlayerRuntimeContext();", shutdown);
            AssertTextBefore(onDisable, "ClearPlayerRuntimeContext();", "TryUnregisterService();");
            AssertTextBefore(shutdown, "ClearPlayerRuntimeContext();", "TryUnregisterService();");
            StringAssert.Contains("_playerRuntimeContext = null;", clearPlayer);
            StringAssert.Contains("_survivalSystem = null;", clearPlayer);
            StringAssert.Contains("_survivalSignalSourceId = 0u;", clearPlayer);
            StringAssert.Contains("_lastSurvivalDeathSignalSequence = 0;", clearPlayer);

            StringAssert.Contains("uint sourceId = ResolveSurvivalSignalSourceId(_survivalSystem);", refreshSurvival);
            StringAssert.Contains("_survivalSignalSourceId = sourceId;", refreshSurvival);
            StringAssert.Contains("sourceId != 0u &&", refreshSurvival);
            StringAssert.Contains("SurvivalSignalRoute.TryGetLatestDeathForSource(sourceId, out _, out int sequence)", refreshSurvival);

            StringAssert.Contains("RuntimeOriginRoute.FoldEntityIdToSourceId(EntityId.ToULong(system.GetEntityId()))", resolveSource);
            StringAssert.Contains(": 0u;", resolveSource);
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
    }
}
