using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class SaveOwnerRegistrationLifecycleEditTests
    {
        [Test]
        public void DiscoveryArchaeologyAndScarcitySaveOwnersWaitForInitializedSaveService()
        {
            string archaeology = ReadProjectFile("Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs");
            string scarcity = ReadProjectFile("Assets/_Project/Scripts/Economy/ResourceScarcityDirector.cs");
            string discovery = ReadProjectFile("Assets/_Project/Scripts/HectonDiscoveryManager.cs");
            string discoveryRegister = ExtractMethodBody(discovery, "private void TryRegisterWithSaveManager()");
            string discoveryUnregister = ExtractMethodBody(discovery, "private void UnregisterFromSaveManager()");
            string archaeologyRegister = ExtractMethodBody(archaeology, "private void TryRegisterRuntime()");
            string archaeologyUnregister = ExtractMethodBody(archaeology, "private void UnregisterRuntime()");
            string archaeologyHotSwap = ExtractMethodBody(archaeology, "public void OnGlobalRegistryServiceReplaced(");

            AssertSaveRegistrationGate(
                discovery,
                "private void TryRegisterWithSaveManager()",
                "_saveService",
                "_registeredWithSaveManager = true;");
            StringAssert.Contains("private ISaveService _registeredSaveService;", discovery);
            StringAssert.Contains("_registeredSaveService = saveService;", discoveryRegister);
            AssertRegisteredSaveOwnerUnregister(
                discoveryUnregister,
                "_saveService",
                "_registeredWithSaveManager");

            AssertSaveRegistrationGate(
                archaeology,
                "private void TryRegisterRuntime()",
                "_saveService",
                "_registeredSave = true;");
            StringAssert.Contains("private ISaveService _registeredSaveService;", archaeology);
            StringAssert.Contains("_registeredSaveService = saveService;", archaeologyRegister);
            Assert.IsTrue(ContainsTokensInOrder(
                archaeologyUnregister,
                "if (_registeredSave || _registeredSaveService != null)",
                "ISaveService saveService = _registeredSaveService != null ? _registeredSaveService : _saveService;",
                "if (saveService != null)",
                "saveService.Unregister(this);",
                "_registeredSaveService = null;",
                "_registeredSave = false;"));
            Assert.IsTrue(ContainsTokensInOrder(
                archaeologyHotSwap,
                "if (_registeredSave || _registeredSaveService != null)",
                "ISaveService previousSave = _registeredSaveService != null ? _registeredSaveService : previousService as ISaveService ?? _saveService;",
                "previousSave.Unregister(this);",
                "_registeredSaveService = null;",
                "_registeredSave = false;",
                "_saveService = currentService as ISaveService;",
                "TryRegisterRuntime();"));

            AssertSaveRegistrationGate(
                scarcity,
                "private void TryRegisterWithSaveManager()",
                "_cachedSaveService",
                "_saveServiceRegistered = true;");
            StringAssert.Contains("_registeredSaveService = saveService;", ExtractMethodBody(scarcity, "private void TryRegisterWithSaveManager()"));
            AssertRegisteredSaveOwnerUnregister(
                ExtractMethodBody(scarcity, "private void TryUnregisterFromSaveManager()"),
                "_cachedSaveService",
                "_saveServiceRegistered");
        }

        [Test]
        public void HazardAndFaunaSaveOwnersWaitForInitializedSaveService()
        {
            string hazard = ReadProjectFile("Assets/_Project/Scripts/Gameplay/HazardZoneManager.cs");
            string fauna = ReadProjectFile("Assets/_Project/Scripts/FaunaDirector.cs");
            string hazardUnregister = ExtractMethodBody(hazard, "private void TryUnregisterSaveParticipant()");
            string faunaUnregister = ExtractMethodBody(fauna, "private void TryUnregisterSaveParticipant()");

            AssertSaveRegistrationGate(
                hazard,
                "private void TryRegisterSaveParticipant()",
                "_saveService",
                "_saveRegistered = true;");
            StringAssert.Contains("private ISaveService _registeredSaveService;", hazard);
            StringAssert.Contains("_registeredSaveService = saveService;", ExtractMethodBody(hazard, "private void TryRegisterSaveParticipant()"));
            AssertRegisteredSaveOwnerUnregister(hazardUnregister, "_saveService", "_saveRegistered");

            AssertSaveRegistrationGate(
                fauna,
                "private void TryRegisterSaveParticipant()",
                "_saveService",
                "_saveRegistered = true;");
            StringAssert.Contains("private ISaveService _registeredSaveService;", fauna);
            StringAssert.Contains("_registeredSaveService = saveService;", ExtractMethodBody(fauna, "private void TryRegisterSaveParticipant()"));
            AssertRegisteredSaveOwnerUnregister(faunaUnregister, "_saveService", "_saveRegistered");

            string faunaHotSwap = ExtractMethodBody(fauna, "public void OnGlobalRegistryServiceReplaced(");
            string faunaRefresh = ExtractMethodBody(fauna, "private void RefreshColdRegistryDependencies()");
            StringAssert.Contains("TryRegisterSaveParticipant();", faunaHotSwap);
            StringAssert.DoesNotContain("_saveService.Register(this);", faunaHotSwap);
            StringAssert.Contains("if (!IsSaveServiceUsable(_saveService))", faunaRefresh);
            StringAssert.DoesNotContain("if (_saveService == null)", faunaRefresh);
        }

        [Test]
        public void RemainingPersistenceOwnersWaitForInitializedSaveService()
        {
            string construction = ReadProjectFile("Assets/_Project/Scripts/ConstructionManager.cs");
            string constructionRegister = ExtractMethodBody(construction, "private void TryRegisterSaveParticipant(ISaveService saveService)");
            string constructionUsable = ExtractMethodBody(construction, "private static bool IsSaveServiceUsable(");
            string constructionHotSwapRegister = ExtractMethodBody(construction, "private void TryRegisterHotSwapListener()");
            string constructionHotSwapUnregister = ExtractMethodBody(construction, "private void TryUnregisterHotSwapListener()");

            Assert.IsTrue(ContainsTokensInOrder(
                constructionRegister,
                "if (!_isInitialized || !Application.isPlaying || _registeredSaveService != null)",
                "if (!IsSaveServiceUsable(saveService))",
                "saveService = GlobalRegistry.Save;",
                "_cachedSaveService = saveService;",
                "if (!IsSaveServiceUsable(saveService))",
                "return;",
                "saveService.Register(this);",
                "_registeredSaveService = saveService;"));
            StringAssert.Contains("return saveService != null && saveService.IsInitialized;", constructionUsable);
            StringAssert.DoesNotContain("saveService == null", constructionRegister);
            StringAssert.Contains("_hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);", constructionHotSwapRegister);
            StringAssert.Contains("GlobalRegistry.TryUnregisterHotSwapListener(this);", constructionHotSwapUnregister);
            StringAssert.DoesNotContain("GlobalRegistry.RegisterHotSwapListener(this);", constructionHotSwapRegister);
            StringAssert.DoesNotContain("GlobalRegistry.IsHotSwapListenerRegistered(this)", constructionHotSwapRegister);
            StringAssert.DoesNotContain("GlobalRegistry.UnregisterHotSwapListener(this);", constructionHotSwapUnregister);

            string radiation = ReadProjectFile("Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs");
            string radiationRegister = ExtractMethodBody(radiation, "private void TryRegisterRuntimeLanes()");
            string radiationSaveRegister = ExtractMethodBody(radiation, "private void TryRegisterSaveParticipant()");
            string radiationSaveUnregister = ExtractMethodBody(radiation, "private void TryUnregisterSaveParticipant()");
            string radiationUsable = ExtractMethodBody(radiation, "private static bool IsSaveServiceUsable(");

            Assert.IsTrue(ContainsTokensInOrder(
                radiationRegister,
                "if (_registeredSave &&",
                "(!ReferenceEquals(_registeredSaveService, GlobalRegistry.Save) || !IsSaveServiceUsable(_registeredSaveService))",
                "TryUnregisterSaveParticipant();",
                "TryRegisterSaveParticipant();"));
            AssertSaveRegistrationGate(
                radiation,
                "private void TryRegisterSaveParticipant()",
                "_saveService",
                "_registeredSave = true;");
            StringAssert.Contains("private ISaveService _registeredSaveService;", radiation);
            StringAssert.Contains("_registeredSaveService = saveService;", radiationSaveRegister);
            AssertRegisteredSaveOwnerUnregister(radiationSaveUnregister, "_saveService", "_registeredSave");
            StringAssert.Contains("return saveService != null && saveService.IsInitialized;", radiationUsable);
            StringAssert.DoesNotContain("if (!_registeredSave && saveService != null)", radiationRegister);

            string lore = ReadProjectFile("Assets/_Project/Scripts/Narrative/LoreDatabaseManager.cs");
            string loreRegister = ExtractMethodBody(lore, "private void TryRegisterSaveParticipant(ISaveService saveService)");
            string loreUsable = ExtractMethodBody(lore, "private static bool IsSaveServiceUsable(");

            Assert.IsTrue(ContainsTokensInOrder(
                loreRegister,
                "if (_runtimeOwnerAborted || !Application.isPlaying || _registeredSaveService != null)",
                "if (!IsSaveServiceUsable(saveService))",
                "saveService = GlobalRegistry.Save;",
                "_saveService = saveService;",
                "if (!IsSaveServiceUsable(saveService))",
                "return;",
                "saveService.Register(this);",
                "_registeredSaveService = saveService;"));
            StringAssert.Contains("return saveService != null && saveService.IsInitialized;", loreUsable);
            StringAssert.DoesNotContain("saveService == null", loreRegister);

            string metaCampaign = ReadProjectFile("Assets/_Project/Scripts/Narrative/Campaign/MetaCampaignService.cs");
            string metaCampaignRegister = ExtractMethodBody(metaCampaign, "private void TryRegisterSaveService()");
            string metaCampaignUnregister = ExtractMethodBody(metaCampaign, "private void TryUnregisterSaveService()");
            string metaCampaignHotSwap = ExtractMethodBody(metaCampaign, "public void OnGlobalRegistryServiceReplaced(");

            AssertSaveRegistrationGate(
                metaCampaign,
                "private void TryRegisterSaveService()",
                "_saveService",
                "_saveServiceRegistered = true;");
            StringAssert.Contains("private ISaveService _registeredSaveService;", metaCampaign);
            StringAssert.Contains("_registeredSaveService = saveService;", metaCampaignRegister);
            AssertRegisteredSaveOwnerUnregister(metaCampaignUnregister, "_saveService", "_saveServiceRegistered");
            AssertTextBefore(metaCampaignHotSwap, "TryUnregisterSaveService();", "_saveService = currentService as ISaveService;");
            AssertTextBefore(metaCampaignHotSwap, "_saveService = currentService as ISaveService;", "TryRegisterSaveService();");

            string voxel = ReadProjectFile("Assets/_Project/Scripts/VoxelDeltaProcessor.cs");
            string voxelRegister = ExtractMethodBody(voxel, "private void TryRegisterSaveService()");
            string voxelUnregister = ExtractMethodBody(voxel, "private void TryUnregisterSaveService()");
            string voxelReplace = ExtractMethodBody(voxel, "private void ReplaceSaveService(");

            AssertSaveRegistrationGate(
                voxel,
                "private void TryRegisterSaveService()",
                "_saveService",
                "_saveRegistered = true;");
            StringAssert.Contains("private ISaveService _registeredSaveService;", voxel);
            StringAssert.Contains("_registeredSaveService = saveService;", voxelRegister);
            AssertRegisteredSaveOwnerUnregister(voxelUnregister, "_saveService", "_saveRegistered");
            AssertTextBefore(voxelReplace, "TryUnregisterSaveService();", "_saveService = nextService;");
            AssertTextBefore(voxelReplace, "_saveService = nextService;", "TryRegisterSaveService();");
        }

        [Test]
        public void FirstHourSaveOwnerTracksRegisteredSaveServiceAcrossHotSwapAndDuplicateAbort()
        {
            string firstHour = ReadProjectFile("Assets/_Project/Scripts/Gameplay/FirstHourDirector.cs");
            string firstHourRegister = ExtractMethodBody(firstHour, "private void TryRegisterSaveParticipant()");
            string firstHourUnregister = ExtractMethodBody(firstHour, "private void TryUnregisterSaveParticipant()");
            string firstHourAbort = ExtractMethodBody(firstHour, "private void AbortDuplicateRuntimeOwner()");

            Assert.IsTrue(ContainsTokensInOrder(
                firstHourRegister,
                "ISaveService saveService = _saveService;",
                "if (!IsSaveServiceUsable(saveService))",
                "saveService = Hecton8.Core.GlobalRegistry.Save;",
                "_saveService = saveService;",
                "if (!IsSaveServiceUsable(saveService))",
                "return;",
                "saveService.Register(this);",
                "_registeredSaveService = saveService;",
                "_saveRegistered = true;"));
            StringAssert.Contains("private ISaveService _registeredSaveService;", firstHour);
            AssertRegisteredSaveOwnerUnregister(firstHourUnregister, "_saveService", "_saveRegistered");
            StringAssert.Contains("_registeredSaveService = null;", firstHourAbort);
        }

        [Test]
        public void QuestAndPdaClockRejectUninitializedSaveService()
        {
            string quest = ReadProjectFile("Assets/_Project/Scripts/Quest/QuestManager.cs");
            string start = ExtractMethodBody(quest, "private void Start()");
            string bindSave = ExtractMethodBody(quest, "private void BindSaveService(ISaveService saveService)");
            string questUsable = ExtractMethodBody(quest, "private static bool IsSaveServiceUsable(");

            AssertTextBefore(start, "if (!_runtimeOwnerAborted)", "BindSaveService(GlobalRegistry.Save);");
            Assert.IsTrue(ContainsTokensInOrder(
                bindSave,
                "if (!IsSaveServiceUsable(saveService))",
                "saveService = null;",
                "if (ReferenceEquals(_registeredSaveService, saveService))",
                "_registeredSaveService?.Unregister(this);",
                "_registeredSaveService = null;",
                "if (!IsSaveServiceUsable(saveService))",
                "return;",
                "_registeredSaveService = saveService;",
                "_registeredSaveService.Register(this);"));
            StringAssert.Contains("return saveService != null && saveService.IsInitialized;", questUsable);
            StringAssert.DoesNotContain("_registeredSaveService?.Register(this);", bindSave);

            string pdaUtility = ReadProjectFile("Assets/_Project/Scripts/PDA/PDAUtility.cs");
            string pdaUsable = ExtractMethodBody(pdaUtility, "private static bool IsSaveServiceUsable(");

            Assert.IsTrue(ContainsTokensInOrder(
                pdaUtility,
                "playTimeSeconds = IsSaveServiceUsable(saveService)",
                "? math.max(0f, saveService.CurrentPlayTimeSeconds)",
                ": 0f;"));
            StringAssert.Contains("return saveService != null && saveService.IsInitialized;", pdaUsable);
            StringAssert.DoesNotContain("saveService != null ? saveService.CurrentPlayTimeSeconds", pdaUtility);
        }

        [Test]
        public void QuestManagerNotificationQueueRefusalStaysDiagnosticAndDoesNotGateQuestTransition()
        {
            string quest = ReadProjectFile("Assets/_Project/Scripts/Quest/QuestManager.cs");
            string emit = ExtractMethodBody(quest, "private void EmitQuestTransition(");
            string push = ExtractMethodBody(quest, "private void TryPushQuestNotification(");
            string report = ExtractMethodBody(quest, "private void ReportQuestNotificationMiss(");
            string clear = ExtractMethodBody(quest, "private void ClearQuestNotificationDiagnostics()");
            string onDisable = ExtractMethodBody(quest, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(quest, "private void OnDestroy()");
            string abort = ExtractMethodBody(quest, "private void AbortDuplicateRuntimeOwner()");
            string populate = ExtractMethodBody(quest, "public void PopulateSaveData(SaveData data)");
            string load = ExtractMethodBody(quest, "public void LoadFromSaveData(SaveData data)");

            StringAssert.Contains("private static readonly uint _QuestNotificationMissWarningHash", quest);
            StringAssert.Contains("private static readonly uint _QuestNotificationContextHash", quest);
            StringAssert.Contains("public int QuestNotificationMissCount =>", quest);
            StringAssert.Contains("TryPushQuestNotification(notificationHash);", emit);
            StringAssert.DoesNotContain("if (notificationHash != 0u)", emit);
            StringAssert.DoesNotContain("NotificationEvents.TryPushRegisteredInfo(notificationHash);", emit);
            AssertTextBefore(emit, "QuestEvents.TryRaiseCompleted(questHash);", "TryPushQuestNotification(notificationHash);");
            AssertTextBefore(emit, "QuestEvents.TryRaiseActivated(questHash);", "TryPushQuestNotification(notificationHash);");
            StringAssert.Contains("if (NotificationEvents.TryPushRegisteredInfo(notificationHash))", push);
            StringAssert.Contains("ReportQuestNotificationMiss(notificationHash);", push);
            StringAssert.Contains("_questNotificationMissCount++;", report);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", report);
            StringAssert.Contains("_QuestNotificationMissWarningHash", report);
            StringAssert.Contains("_QuestNotificationContextHash ^ notificationHash", report);
            StringAssert.Contains("math.max(1, _questNotificationMissCount)", report);
            StringAssert.Contains("_questNotificationMissCount = 0;", clear);
            StringAssert.Contains("ClearQuestNotificationDiagnostics();", onDisable);
            StringAssert.Contains("ClearQuestNotificationDiagnostics();", onDestroy);
            StringAssert.Contains("ClearQuestNotificationDiagnostics();", abort);
            StringAssert.Contains("ClearQuestNotificationDiagnostics();", load);
            StringAssert.DoesNotContain("_questNotificationMissCount", populate);
            StringAssert.DoesNotContain("_questNotificationMissCount", load);
        }

        private static void AssertSaveRegistrationGate(
            string source,
            string registerSignature,
            string saveServiceField,
            string registeredFlagAssignment)
        {
            string register = ExtractMethodBody(source, registerSignature);
            string usable = ExtractMethodBody(source, "private static bool IsSaveServiceUsable(");

            Assert.IsTrue(ContainsTokensInOrder(
                register,
                "ISaveService saveService = " + saveServiceField + ";",
                "if (!IsSaveServiceUsable(saveService))",
                "saveService = GlobalRegistry.Save;",
                saveServiceField + " = saveService;",
                "if (!IsSaveServiceUsable(saveService))",
                "return;",
                "saveService.Register(this);",
                registeredFlagAssignment));
            StringAssert.Contains("return saveService != null && saveService.IsInitialized;", usable);
            StringAssert.DoesNotContain("if (" + saveServiceField + " == null)", register);
            StringAssert.DoesNotContain("if (saveService == null)", register);
        }

        private static void AssertRegisteredSaveOwnerUnregister(
            string unregister,
            string saveServiceField,
            string registeredFlagName)
        {
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

        private static void AssertTextBefore(string text, string before, string after)
        {
            int beforeIndex = text.IndexOf(before, StringComparison.Ordinal);
            int afterIndex = text.IndexOf(after, StringComparison.Ordinal);
            Assert.GreaterOrEqual(beforeIndex, 0, "Missing token: " + before);
            Assert.GreaterOrEqual(afterIndex, 0, "Missing token: " + after);
            Assert.Less(beforeIndex, afterIndex, before + " should appear before " + after);
        }

        private static string ReadProjectFile(string relativePath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return File.ReadAllText(Path.Combine(projectRoot, relativePath));
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
