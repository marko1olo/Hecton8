using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor.SaveSystem
{
    public sealed class SaveEventsUiBridgeEditTests
    {
        [Test]
        public void HudNotificationBridgeSurfacesLoadFailureAsCritical()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/UI/HUDSaveNotificationLink.cs");
            string onSaveEvent = ExtractMethodBody(source, "public void OnSaveEvent(in SaveEventPayload payload)");
            string tryBuildMessage = ExtractMethodBody(source, "private bool TryBuildMessage(in SaveEventPayload payload, string failureMessageOverride = null)");
            string appendFailureDetail = ExtractMethodBody(source, "private static void AppendFailureDetail(");
            string resolveFailureMessageOverride = ExtractMethodBody(source, "private string ResolveFailureMessageOverride(");
            string appendTruncated = ExtractMethodBody(source, "private static void AppendTruncated(");

            StringAssert.Contains("LoadFailedKeyHash", source);
            StringAssert.Contains("case SaveEventType.LoadFailed:", onSaveEvent);
            StringAssert.Contains("if (IsDuplicateFailureNotification(in payload))", onSaveEvent);
            StringAssert.Contains("string failureMessageOverride = ResolveFailureMessageOverride(in payload);", onSaveEvent);
            StringAssert.Contains("TryBuildMessage(in payload, failureMessageOverride)", onSaveEvent);
            StringAssert.Contains("notificationSystem.ShowCritical(in _messageBuffer);", onSaveEvent);
            StringAssert.Contains("RememberFailureNotification(in payload);", onSaveEvent);
            StringAssert.Contains("payload.Type == SaveEventType.LoadFailed", tryBuildMessage);
            StringAssert.Contains("AppendLocalized(ref _messageBuffer, LoadFailedKeyHash, \"LOAD FAILED\".AsSpan())", tryBuildMessage);
            StringAssert.Contains("AppendFailureDetail(ref _messageBuffer, in payload, failureMessageOverride);", tryBuildMessage);
            StringAssert.Contains("string message = failureMessageOverride;", appendFailureDetail);
            StringAssert.Contains("SaveEvents.TryResolveMessage(in payload, out message)", appendFailureDetail);
            StringAssert.Contains("if (!IsFailurePayload(in payload))", resolveFailureMessageOverride);
            StringAssert.Contains("SaveEvents.TryResolveMessage(in payload, out _)", resolveFailureMessageOverride);
            StringAssert.Contains("SaveEvents.TryConsumeMatchingFailureSnapshotForUi(", resolveFailureMessageOverride);
            StringAssert.Contains("ref _lastConsumedFailureSnapshotSequence", resolveFailureMessageOverride);
            StringAssert.Contains("if (ResolveRemainingCapacity(in buffer) <= 2)", appendFailureDetail);
            StringAssert.Contains("TryAppendLiteral(ref buffer, \": \".AsSpan())", appendFailureDetail);
            StringAssert.Contains("AppendTruncated(ref buffer, message.AsSpan());", appendFailureDetail);
            StringAssert.Contains("ResolveRemainingCapacity(in buffer)", appendTruncated);
            StringAssert.Contains("buffer.Append(\"...\".AsSpan());", appendTruncated);
        }

        [Test]
        public void HudNotificationBridgeConsumesLateFailureSnapshotWithoutListenerReplay()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/UI/HUDSaveNotificationLink.cs");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string showSnapshot = ExtractMethodBody(source, "private void TryShowLatestFailureSnapshot()");
            string duplicate = ExtractMethodBody(source, "private bool IsDuplicateFailureNotification(");
            string remember = ExtractMethodBody(source, "private void RememberFailureNotification(");
            string signature = ExtractMethodBody(source, "private static ulong BuildFailureNotificationSignature(");

            StringAssert.Contains("private uint _lastConsumedFailureSnapshotSequence;", source);
            StringAssert.Contains("private ulong _lastFailureNotificationSignature;", source);
            Assert.Less(
                onEnable.IndexOf("SaveEvents.Register(this);", StringComparison.Ordinal),
                onEnable.IndexOf("TryShowLatestFailureSnapshot();", StringComparison.Ordinal));
            StringAssert.Contains("TryUnregisterHotSwapListener();", onDestroy);
            StringAssert.Contains("LocalizationEvents.UnregisterCorruptionVisualStateListener(this);", onDestroy);
            StringAssert.Contains("LocalizationEvents.UnregisterLanguageListener(this);", onDestroy);
            StringAssert.Contains("SaveEvents.Unregister(this);", onDestroy);
            StringAssert.Contains("ClearMessageCache();", onDestroy);
            StringAssert.Contains("SaveEvents.TryConsumeLatestFailureSnapshotForUi(", showSnapshot);
            StringAssert.Contains("ref _lastConsumedFailureSnapshotSequence", showSnapshot);
            StringAssert.Contains("out SaveEventPayload payload", showSnapshot);
            StringAssert.Contains("out string failureMessage", showSnapshot);
            StringAssert.Contains("if (IsDuplicateFailureNotification(in payload))", showSnapshot);
            StringAssert.Contains("TryBuildMessage(in payload, failureMessage)", showSnapshot);
            StringAssert.Contains("notificationSystem.ShowCritical(in _messageBuffer);", showSnapshot);
            StringAssert.Contains("RememberFailureNotification(in payload);", showSnapshot);
            StringAssert.Contains("signature == _lastFailureNotificationSignature", duplicate);
            StringAssert.Contains("_lastFailureNotificationSignature = signature;", remember);
            StringAssert.Contains("IsFailurePayload(in payload)", signature);
            StringAssert.Contains("payload.TimestampTicks", signature);
            StringAssert.Contains("payload.MessageHash", signature);
            StringAssert.Contains("payload.SlotHash", signature);
        }

        [Test]
        public void PauseSaveStationClearsBlockedStateOnLoadFailure()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/UI/PauseMenuController.cs");
            string onSaveEvent = ExtractMethodBody(source, "public void OnSaveEvent(in SaveEventPayload payload)");
            string saveSlot = ExtractMethodBody(source, "private void SaveSlot(string slotName)");
            string handleSaveFailed = ExtractMethodBody(source, "private void HandleSaveFailed(string slotName, string error)");
            string handleLoadStarted = ExtractMethodBody(source, "private void HandleLoadStarted(string slotName)");
            string handleLoadCompleted = ExtractMethodBody(source, "private void HandleLoadCompleted(string slotName)");
            string showLoadRecoveryModal = ExtractMethodBody(source, "private void ShowLoadRecoveryModal(string slotName)");
            string handleLoadFailed = ExtractMethodBody(source, "private void HandleLoadFailed(string slotName, string error)");
            string buildLocalizedModalTitle = ExtractMethodBody(source, "private string BuildLocalizedModalTitle(");
            string resolveFailureMessage = ExtractMethodBody(source, "private string ResolveSaveFailureMessage(");
            string cacheRetrySaveSlot = ExtractMethodBody(source, "private Action CacheRetrySaveSlot(string slotName)");
            string buildModalWithRetryGate = ExtractMethodBodyContaining(
                source,
                "private int BuildSaveModalMessage(",
                "bool appendRetryPrompt");

            StringAssert.Contains("case SaveEventType.LoadStarted:", onSaveEvent);
            StringAssert.Contains("case SaveEventType.LoadCompleted:", onSaveEvent);
            StringAssert.Contains("case SaveEventType.LoadFailed:", onSaveEvent);
            StringAssert.Contains("HandleSaveFailed(SaveEvents.ResolveSlotName(payload.SlotHash), ResolveSaveFailureMessage(in payload));", onSaveEvent);
            StringAssert.Contains("HandleLoadFailed(SaveEvents.ResolveSlotName(payload.SlotHash), ResolveSaveFailureMessage(in payload));", onSaveEvent);
            StringAssert.DoesNotContain("SaveEvents.ResolveMessage(in payload)", onSaveEvent);

            Assert.Less(
                saveSlot.IndexOf("if (!SaveEvents.IsKnownManualSlotName(slotName))", StringComparison.Ordinal),
                saveSlot.IndexOf("_ = SaveSlotAsync(slotName);", StringComparison.Ordinal));
            StringAssert.Contains("const string reason = \"Invalid save slot.\";", saveSlot);
            StringAssert.Contains("_pendingRetrySaveSlotName = string.Empty;", saveSlot);
            StringAssert.Contains("ApplySaveFailedStatusText(slotName, reason);", saveSlot);
            StringAssert.Contains("\"OK\"", saveSlot);
            StringAssert.Contains("bool canRetry = SaveEvents.IsKnownManualSlotName(slotName);", handleSaveFailed);
            StringAssert.Contains("canRetry ? CacheRetrySaveSlot(slotName) : null", handleSaveFailed);
            StringAssert.Contains("canRetry ? \"Retry\" : \"OK\"", handleSaveFailed);
            StringAssert.Contains("canRetry ? \"Cancel\" : null", handleSaveFailed);
            StringAssert.Contains("_saveOperationInFlight = true;", handleLoadStarted);
            StringAssert.Contains("SetSaveButtonsInteractable(false);", handleLoadStarted);
            StringAssert.Contains("_saveOperationInFlight = false;", handleLoadCompleted);
            StringAssert.Contains("SetSaveButtonsInteractable(true);", handleLoadCompleted);
            StringAssert.Contains("ShowLoadRecoveryModal(slotName);", handleLoadCompleted);
            StringAssert.Contains("_cachedSaveService is SaveManager saveManager", showLoadRecoveryModal);
            StringAssert.Contains("saveManager.LastLoadUsedBackup", showLoadRecoveryModal);
            StringAssert.Contains("saveManager.LastLoadSelfRepaired", showLoadRecoveryModal);
            StringAssert.Contains("WarningBackupUsedMessageKeyHash", showLoadRecoveryModal);
            StringAssert.Contains("WarningSaveRepairedMessageKeyHash", showLoadRecoveryModal);
            StringAssert.Contains("WarningBackupUsedTitleKeyHash", showLoadRecoveryModal);
            StringAssert.Contains("WarningSaveRepairedTitleKeyHash", showLoadRecoveryModal);
            StringAssert.Contains("appendRetryPrompt: false", showLoadRecoveryModal);
            StringAssert.Contains("ModalWindow.ShowWithCustomLabels(", showLoadRecoveryModal);
            StringAssert.Contains("_modalTitleBuffer", source);
            StringAssert.Contains("new string(_modalTitleBuffer, 0, length)", buildLocalizedModalTitle);
            StringAssert.Contains("_saveOperationInFlight = false;", handleLoadFailed);
            StringAssert.Contains("SetSaveButtonsInteractable(true);", handleLoadFailed);
            StringAssert.Contains("ApplyLoadFailedStatusText(slotName, error);", handleLoadFailed);
            StringAssert.Contains("ErrorLoadFailedMessageKeyHash", handleLoadFailed);
            StringAssert.Contains("\"Load Failed\"", handleLoadFailed);
            StringAssert.Contains("false);", handleLoadFailed);
            StringAssert.Contains("private uint _lastConsumedFailureSnapshotSequence;", source);
            StringAssert.Contains("SaveEvents.ResolveMessage(in payload)", resolveFailureMessage);
            StringAssert.Contains("SaveEvents.TryConsumeMatchingFailureSnapshotForUi(", resolveFailureMessage);
            StringAssert.Contains("ref _lastConsumedFailureSnapshotSequence", resolveFailureMessage);
            Assert.Less(
                cacheRetrySaveSlot.IndexOf("if (!SaveEvents.IsKnownManualSlotName(slotName))", StringComparison.Ordinal),
                cacheRetrySaveSlot.IndexOf("_pendingRetrySaveSlotName = slotName ?? string.Empty;", StringComparison.Ordinal));
            StringAssert.Contains("_pendingRetrySaveSlotName = string.Empty;", cacheRetrySaveSlot);
            StringAssert.Contains("return null;", cacheRetrySaveSlot);

            StringAssert.Contains("if (appendRetryPrompt)", buildModalWithRetryGate);
            StringAssert.Contains("\"\\n\\nRetry?\".AsSpan()", buildModalWithRetryGate);
        }

        [Test]
        public void PauseSaveButtonDoesNotEnterWritingStateWhenSaveServiceUnavailable()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/UI/PauseMenuController.cs");
            string saveSlotAsync = ExtractMethodBody(source, "private async Awaitable SaveSlotAsync(string slotName)");
            string refreshSaveSection = ExtractMethodBody(source, "private void RefreshSaveSectionState()");
            string usable = ExtractMethodBody(source, "private static bool IsSaveServiceUsable(");

            int serviceIndex = saveSlotAsync.IndexOf("if (!IsSaveServiceUsable(saveService))", StringComparison.Ordinal);
            int busyIndex = saveSlotAsync.IndexOf("if (saveService.IsBusy)", StringComparison.Ordinal);
            int writingIndex = saveSlotAsync.IndexOf("ApplySaveStatusText(_cachedWriting, upperSlotName, \"...\")", StringComparison.Ordinal);
            int saveCallIndex = saveSlotAsync.IndexOf("await saveService.SaveGameAsync(slotName);", StringComparison.Ordinal);

            StringAssert.Contains("return saveService != null && saveService.IsInitialized;", usable);
            Assert.GreaterOrEqual(serviceIndex, 0);
            Assert.Greater(busyIndex, serviceIndex);
            Assert.Greater(writingIndex, busyIndex);
            Assert.Greater(saveCallIndex, writingIndex);
            StringAssert.Contains("ApplySaveStatusLiteral(_cachedSaveServiceUnavailable);", saveSlotAsync);
            StringAssert.Contains("ErrorSaveManagerUnavailableKeyHash", saveSlotAsync);
            StringAssert.Contains("if (!IsSaveServiceUsable(saveService))", refreshSaveSection);
            Assert.Less(
                refreshSaveSection.IndexOf("if (!IsSaveServiceUsable(saveService))", StringComparison.Ordinal),
                refreshSaveSection.IndexOf("bool isBusy = _saveOperationInFlight || saveService.IsBusy;", StringComparison.Ordinal));
            StringAssert.Contains("SetSaveButtonsInteractable(false);", refreshSaveSection);
            StringAssert.Contains("ApplySaveStatusLiteral(_cachedSaveServiceUnavailable);", refreshSaveSection);
        }

        [Test]
        public void MainMenuSaveLoadFailuresUseUiSnapshotFallbackBeforeUnknownError()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/MainMenuController.cs");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string serviceReplaced = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string showSnapshot = ExtractMethodBody(source, "private void TryShowLatestFailureSnapshot()");
            string onSaveEvent = ExtractMethodBody(source, "public void OnSaveEvent(in SaveEventPayload payload)");
            string resolveFailureMessage = ExtractMethodBody(source, "private string ResolveSaveFailureMessage(");
            string duplicateFailureNotification = ExtractMethodBody(source, "private bool IsDuplicateFailureNotification(");
            string rememberFailureNotification = ExtractMethodBody(source, "private void RememberFailureNotification(");
            string failureNotificationSignature = ExtractMethodBody(source, "private static ulong BuildFailureNotificationSignature(");
            string resolveError = ExtractMethodBody(source, "private static string ResolveSaveEventError(string error)");

            StringAssert.Contains("private uint _lastConsumedFailureSnapshotSequence;", source);
            StringAssert.Contains("private ulong _lastFailureNotificationSignature;", source);
            Assert.Less(
                onEnable.IndexOf("SaveEvents.Register(this);", StringComparison.Ordinal),
                onEnable.IndexOf("TryShowLatestFailureSnapshot();", StringComparison.Ordinal));
            StringAssert.Contains("GlobalRegistryServiceSlot.ModalWindowRuntime", serviceReplaced);
            StringAssert.Contains("TryShowLatestFailureSnapshot();", serviceReplaced);
            StringAssert.Contains("if (GlobalRegistry.ModalWindow == null)", showSnapshot);
            Assert.Less(
                showSnapshot.IndexOf("if (GlobalRegistry.ModalWindow == null)", StringComparison.Ordinal),
                showSnapshot.IndexOf("SaveEvents.TryConsumeLatestFailureSnapshotForUi(", StringComparison.Ordinal));
            StringAssert.Contains("SaveEvents.TryConsumeLatestFailureSnapshotForUi(", showSnapshot);
            StringAssert.Contains("ref _lastConsumedFailureSnapshotSequence", showSnapshot);
            StringAssert.Contains("out SaveEventPayload payload", showSnapshot);
            StringAssert.Contains("out string failureMessage", showSnapshot);
            StringAssert.Contains("if (IsDuplicateFailureNotification(in payload))", showSnapshot);
            StringAssert.Contains("case SaveEventType.SaveFailed:", showSnapshot);
            StringAssert.Contains("OnSaveFailed(slotName, failureMessage);", showSnapshot);
            StringAssert.Contains("RememberFailureNotification(in payload);", showSnapshot);
            StringAssert.Contains("case SaveEventType.LoadFailed:", showSnapshot);
            StringAssert.Contains("OnLoadFailed(slotName, failureMessage);", showSnapshot);
            StringAssert.Contains("if (IsDuplicateFailureNotification(in payload))", onSaveEvent);
            StringAssert.Contains("OnSaveFailed(SaveEvents.ResolveSlotName(payload.SlotHash), ResolveSaveFailureMessage(in payload));", onSaveEvent);
            StringAssert.Contains("OnLoadFailed(SaveEvents.ResolveSlotName(payload.SlotHash), ResolveSaveFailureMessage(in payload));", onSaveEvent);
            StringAssert.Contains("RememberFailureNotification(in payload);", onSaveEvent);
            StringAssert.DoesNotContain("SaveEvents.ResolveMessage(in payload)", onSaveEvent);
            StringAssert.Contains("SaveEvents.ResolveMessage(in payload)", resolveFailureMessage);
            StringAssert.Contains("SaveEvents.TryConsumeMatchingFailureSnapshotForUi(", resolveFailureMessage);
            StringAssert.Contains("ref _lastConsumedFailureSnapshotSequence", resolveFailureMessage);
            StringAssert.Contains("signature == _lastFailureNotificationSignature", duplicateFailureNotification);
            StringAssert.Contains("_lastFailureNotificationSignature = signature;", rememberFailureNotification);
            StringAssert.Contains("payload.TimestampTicks", failureNotificationSignature);
            StringAssert.Contains("string.IsNullOrWhiteSpace(error) ? UnknownSaveEventError : error", resolveError);
        }

        [Test]
        public void MainMenuSaveLoadUsesInitializedSaveManagerSnapshot()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/MainMenuController.cs");
            string openSaveLoad = ExtractMethodBody(source, "public void OpenSaveLoadMenu()");
            string serviceReplaced = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string refreshSaveLoadSlots = ExtractMethodBody(source, "private void RefreshSaveLoadSlotViewsFromCachedManager()");
            string unavailableSaveSlots = ExtractMethodBody(source, "private void ApplyUnavailableSaveSlotViews()");
            string cacheStartAction = ExtractMethodBody(source, "private Action CacheStartGameAction(string slotName)");
            string onLoadFailed = ExtractMethodBody(source, "private void OnLoadFailed(string slotName, string error)");
            string startGameWithScene = ExtractMethodBody(source, "private void StartGameWithScene(");
            string onSlotClicked = ExtractMethodBody(source, "private void OnSlotClicked(string slotName)");
            string readableSlotInfo = ExtractMethodBody(source, "private bool TryResolveReadableSaveSlotInfo(");
            string onSaveCompleted = ExtractMethodBody(source, "private void OnSaveCompleted(in SaveEventPayload payload)");
            string onLoadCompleted = ExtractMethodBody(source, "private void OnLoadCompleted(in SaveEventPayload payload)");
            string showLoadRecoveryModal = ExtractMethodBody(source, "private void ShowLoadRecoveryModal(");
            string cacheSaveManager = ExtractMethodBody(source, "private void CacheSaveManagerCold(SaveManager saveManager)");
            string usable = ExtractMethodBody(source, "private static bool IsSaveManagerUsable(SaveManager saveManager)");
            string englishLocalization = ReadProjectFile("Assets/_Project/Scripts/English.json");
            string locKeysGenerated = ReadProjectFile("Assets/_Project/Scripts/LocKeys.Generated.cs");
            string h8Hashes = ReadProjectFile("Assets/_Project/Scripts/Core/Generated/H8Hashes.cs");

            StringAssert.Contains("return saveManager != null && saveManager.IsInitialized;", usable);
            StringAssert.Contains("_saveManager = IsSaveManagerUsable(saveManager) ? saveManager : null;", cacheSaveManager);
            StringAssert.DoesNotContain("_saveManager == null", source);
            StringAssert.DoesNotContain("_saveManager != null", source);

            StringAssert.Contains("RefreshSaveLoadSlotViewsFromCachedManager();", openSaveLoad);
            StringAssert.Contains("RefreshSaveLoadSlotViewsFromCachedManager();", serviceReplaced);
            StringAssert.DoesNotContain("_saveManager.TryGetSaveSlotInfo", openSaveLoad);
            Assert.Less(
                refreshSaveLoadSlots.IndexOf("SaveManager saveManager = _saveManager;", StringComparison.Ordinal),
                refreshSaveLoadSlots.IndexOf("if (!IsSaveManagerUsable(saveManager))", StringComparison.Ordinal));
            Assert.Less(
                refreshSaveLoadSlots.IndexOf("if (!IsSaveManagerUsable(saveManager))", StringComparison.Ordinal),
                refreshSaveLoadSlots.IndexOf("saveManager.TryGetSaveSlotInfo(slotName, out SaveSlotInfo slotInfo)", StringComparison.Ordinal));
            StringAssert.Contains("ApplyUnavailableSaveSlotViews();", refreshSaveLoadSlots);
            StringAssert.Contains("slotUI.Init(slotInfo, OnSlotClicked);", refreshSaveLoadSlots);
            StringAssert.Contains("slotUI.Init(slotName, false, string.Empty, 0f, OnSlotClicked);", refreshSaveLoadSlots);
            StringAssert.Contains("SetSaveLoadButtonsInteractable(!_isSaveLoadBusy && !_isSceneLoadInFlight);", refreshSaveLoadSlots);
            StringAssert.Contains("for (int i = 0; i < _slotButtonAvailability.Length; i++)", unavailableSaveSlots);
            StringAssert.Contains("_slotButtonAvailability[i] = false;", unavailableSaveSlots);
            StringAssert.Contains("slotUI.Init(slotName, false, string.Empty, 0f, OnSlotClicked);", unavailableSaveSlots);
            StringAssert.Contains("SetSaveLoadButtonsInteractable(!_isSaveLoadBusy && !_isSceneLoadInFlight);", unavailableSaveSlots);

            Assert.Less(
                startGameWithScene.IndexOf("SaveManager saveManager = _saveManager;", StringComparison.Ordinal),
                startGameWithScene.IndexOf("if (!IsSaveManagerUsable(saveManager))", StringComparison.Ordinal));
            Assert.Less(
                startGameWithScene.IndexOf("if (!IsSaveManagerUsable(saveManager))", StringComparison.Ordinal),
                startGameWithScene.IndexOf("if (!SaveManager.TryResolveSafeSlotName(slotName, out safeSlotName))", StringComparison.Ordinal));
            Assert.Less(
                startGameWithScene.IndexOf("if (!SaveManager.TryResolveSafeSlotName(slotName, out safeSlotName))", StringComparison.Ordinal),
                startGameWithScene.IndexOf("if (!saveManager.SaveExists(safeSlotName))", StringComparison.Ordinal));
            Assert.Less(
                startGameWithScene.IndexOf("if (!saveManager.SaveExists(safeSlotName))", StringComparison.Ordinal),
                startGameWithScene.IndexOf("slotName = safeSlotName;", StringComparison.Ordinal));
            Assert.Less(
                startGameWithScene.IndexOf("slotName = safeSlotName;", StringComparison.Ordinal),
                startGameWithScene.IndexOf("GameStartContext.CreateLoadGame(slotName)", StringComparison.Ordinal));
            StringAssert.Contains("string safeSlotName = string.Empty;", startGameWithScene);
            StringAssert.Contains("\"Invalid save slot.\"", startGameWithScene);
            StringAssert.DoesNotContain("saveManager.SaveExists(slotName)", startGameWithScene);
            StringAssert.DoesNotContain("_saveManager.SaveExists(slotName)", startGameWithScene);

            StringAssert.Contains("if (!SaveEvents.IsKnownManualSlotName(slotName))", onSlotClicked);
            StringAssert.Contains("PublishPrimaryMenuActionFeedback(ResolveSlotButtonByName(slotName));", onSlotClicked);
            StringAssert.Contains("CacheStartGameAction(slotName)", onSlotClicked);
            Assert.Less(
                onSlotClicked.IndexOf("if (!SaveEvents.IsKnownManualSlotName(slotName))", StringComparison.Ordinal),
                onSlotClicked.IndexOf("PublishPrimaryMenuActionFeedback(ResolveSlotButtonByName(slotName));", StringComparison.Ordinal));
            Assert.Less(
                onSlotClicked.IndexOf("PublishPrimaryMenuActionFeedback(ResolveSlotButtonByName(slotName));", StringComparison.Ordinal),
                onSlotClicked.IndexOf("CacheStartGameAction(slotName)", StringComparison.Ordinal));

            Assert.Less(
                cacheStartAction.IndexOf("!SaveEvents.IsKnownManualSlotName(slotName)", StringComparison.Ordinal),
                cacheStartAction.IndexOf("_pendingStartSlotName = slotName ?? string.Empty;", StringComparison.Ordinal));
            StringAssert.Contains("_pendingStartSlotName = string.Empty;", cacheStartAction);
            StringAssert.Contains("return null;", cacheStartAction);
            StringAssert.Contains("bool canRetry = SaveEvents.IsKnownManualSlotName(slotName);", onLoadFailed);
            StringAssert.Contains("canRetry ? CacheStartGameAction(slotName) : null", onLoadFailed);
            StringAssert.Contains("canRetry ? _returnSaveLoadToMainMenuAction : null", onLoadFailed);
            StringAssert.Contains("canRetry ? \"Retry\" : \"OK\"", onLoadFailed);
            StringAssert.Contains("canRetry ? \"Return to Menu\" : null", onLoadFailed);

            StringAssert.Contains("if (!IsSaveManagerUsable(saveManager))", readableSlotInfo);
            StringAssert.Contains("IsSaveManagerUsable(saveManager) && _slotUIs != null", onSaveCompleted);
            StringAssert.Contains("saveManager.TryGetSaveSlotInfo(slotNameToRefresh, out SaveSlotInfo slotInfo)", onSaveCompleted);
            StringAssert.DoesNotContain("_saveManager.TryGetSaveSlotInfo", onSaveCompleted);
            StringAssert.Contains("ShowLoadRecoveryModal(in payload, saveManager);", onLoadCompleted);
            StringAssert.Contains("bool usedBackup = saveManager.LastLoadUsedBackup;", showLoadRecoveryModal);
            StringAssert.Contains("bool selfRepaired = saveManager.LastLoadSelfRepaired;", showLoadRecoveryModal);
            StringAssert.Contains("if (!usedBackup && !selfRepaired)", showLoadRecoveryModal);
            StringAssert.Contains("LocalizationKeys.WARNING_BACKUP_USED_TITLE", showLoadRecoveryModal);
            StringAssert.Contains("LocalizationKeys.WARNING_BACKUP_USED_MESSAGE", showLoadRecoveryModal);
            StringAssert.Contains("LocalizationKeys.WARNING_SAVE_REPAIRED_MESSAGE", showLoadRecoveryModal);
            StringAssert.Contains("LocalizationKeys.WARNING_SAVE_REPAIRED_TITLE", showLoadRecoveryModal);
            StringAssert.DoesNotContain("usedBackup ? \"Backup Loaded\" : \"Save Repaired\"", showLoadRecoveryModal);
            StringAssert.Contains("\"WARNING_BACKUP_USED_MESSAGE\": \"Primary save file was corrupt. Loaded from backup.\"", englishLocalization);
            StringAssert.Contains("\"WARNING_BACKUP_USED_TITLE\": \"BACKUP LOADED\"", englishLocalization);
            StringAssert.Contains("\"WARNING_SAVE_REPAIRED_MESSAGE\": \"Primary save file was repaired before loading.\"", englishLocalization);
            StringAssert.Contains("\"WARNING_SAVE_REPAIRED_TITLE\": \"SAVE REPAIRED\"", englishLocalization);
            AssertRootLocalizationJsonContains("\"WARNING_SAVE_REPAIRED_MESSAGE\": \"Primary save file was repaired before loading.\"");
            AssertRootLocalizationJsonContains("\"WARNING_SAVE_REPAIRED_TITLE\": \"SAVE REPAIRED\"");
            StringAssert.Contains("WARNING_SAVE_REPAIRED_MESSAGE = LocHash.Compute(\"WARNING_SAVE_REPAIRED_MESSAGE\")", locKeysGenerated);
            StringAssert.Contains("WARNING_SAVE_REPAIRED_TITLE = LocHash.Compute(\"WARNING_SAVE_REPAIRED_TITLE\")", locKeysGenerated);
            StringAssert.Contains("WARNINGSAVEREPAIREDMESSAGEId = \"WARNING_SAVE_REPAIRED_MESSAGE\"", h8Hashes);
            StringAssert.Contains("WARNINGSAVEREPAIREDMESSAGEHash = 2818600358u", h8Hashes);
            StringAssert.DoesNotContain("_saveManager.LastLoadUsedBackup", onLoadCompleted);
            StringAssert.DoesNotContain("_saveManager.LastLoadSelfRepaired", onLoadCompleted);
            StringAssert.DoesNotContain("_saveManager.LastLoadUsedBackup", showLoadRecoveryModal);
            StringAssert.DoesNotContain("_saveManager.LastLoadSelfRepaired", showLoadRecoveryModal);
        }

        [Test]
        public void SaveSlotHoverPreviewGatesThumbnailLoadOnUsableSaveManagerMetadata()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/UI/SaveSlotHoverPreview.cs");
            string showPreview = ExtractMethodBody(source, "private void ShowPreview()");
            string populateWrapper = ExtractMethodBody(source, "private void PopulatePreviewMetadata()");
            string tryPopulate = ExtractMethodBody(source, "private bool TryPopulatePreviewMetadata()");
            string refreshPreview = ExtractMethodBody(source, "private void RefreshVisiblePreviewFromCachedSaveManager()");
            string usable = ExtractMethodBody(source, "private static bool IsSaveManagerUsable(SaveManager saveManager)");
            string serviceReplaced = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");

            StringAssert.Contains("RefreshVisiblePreviewFromCachedSaveManager();", showPreview);
            StringAssert.DoesNotContain("previewThumbnail.LoadThumbnail(_currentSlotId);", showPreview);
            StringAssert.Contains("_ = TryPopulatePreviewMetadata();", populateWrapper);
            StringAssert.Contains("return saveManager != null && saveManager.IsInitialized;", usable);
            StringAssert.Contains("!IsSaveManagerUsable(saveManager)", tryPopulate);
            Assert.Less(
                tryPopulate.IndexOf("!IsSaveManagerUsable(saveManager)", StringComparison.Ordinal),
                tryPopulate.IndexOf("!saveManager.TryGetSaveSlotInfo(_currentSlotId, out SaveSlotInfo slotInfo)", StringComparison.Ordinal));
            StringAssert.Contains("!slotInfo.HasAnySaveData", tryPopulate);
            StringAssert.Contains("return false;", tryPopulate);
            StringAssert.Contains("return true;", tryPopulate);
            StringAssert.Contains("bool hasPreviewData = TryPopulatePreviewMetadata();", refreshPreview);
            Assert.Less(
                refreshPreview.IndexOf("if (hasPreviewData)", StringComparison.Ordinal),
                refreshPreview.IndexOf("previewThumbnail.LoadThumbnail(_currentSlotId);", StringComparison.Ordinal));
            StringAssert.Contains("previewThumbnail.ClearThumbnail();", refreshPreview);
            StringAssert.Contains("case GlobalRegistryServiceSlot.Save:", serviceReplaced);
            StringAssert.Contains("RefreshVisiblePreviewFromCachedSaveManager();", serviceReplaced);
        }

        [Test]
        public void TerminalOsDialogueChoiceBridgeUsesInitializedSaveManager()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs");
            string updateSync = ExtractMethodBody(source, "private void UpdateDialogueChoiceVisualSync()");
            string recordSolved = ExtractMethodBody(source, "private void RecordSolvedDecryptionDialogueChoice()");
            string usable = ExtractMethodBody(source, "private static bool IsSaveManagerUsable(SaveManager saveManager)");
            string serviceReplaced = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");

            StringAssert.Contains("return saveManager != null && saveManager.IsInitialized;", usable);
            StringAssert.Contains(
                "IsSaveManagerUsable(saveManager) ? saveManager.PlayerDialogueChoiceFlags : (ushort)0",
                updateSync);
            StringAssert.DoesNotContain("saveManager != null ? saveManager.PlayerDialogueChoiceFlags", updateSync);
            StringAssert.Contains("if (!IsSaveManagerUsable(saveManager) ||", recordSolved);
            Assert.Less(
                recordSolved.IndexOf("if (!IsSaveManagerUsable(saveManager) ||", StringComparison.Ordinal),
                recordSolved.IndexOf("saveManager.RecordPlayerDialogueChoiceFlag(DialogueDecisionSaveFacilityMask);", StringComparison.Ordinal));
            StringAssert.DoesNotContain("if (saveManager == null ||", recordSolved);
            StringAssert.Contains("case GlobalRegistryServiceSlot.Save:", serviceReplaced);
            StringAssert.Contains("_cachedSaveManager = currentService as SaveManager;", serviceReplaced);
            StringAssert.Contains("_lastUploadedDialogueChoiceFlags = ushort.MaxValue;", serviceReplaced);
        }

        [Test]
        public void BootstrapGameStartContextUsesInitializedSaveManagerBeforeLoadingSlot()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs");
            string verifySingletons = ExtractMethodBody(source, "private bool VerifySingletons()");
            string loadOrNewGame = ExtractMethodBody(source, "private async Awaitable LoadOrNewGameAsync()");
            string rejectedLoadContext = ExtractMethodBody(source, "private void StartNewGameFromRejectedLoadContext()");
            string usable = ExtractMethodBody(source, "private static bool IsSaveManagerUsable(SaveManager saveManager)");

            StringAssert.Contains("return saveManager != null && saveManager.IsInitialized;", usable);
            StringAssert.Contains("if (!IsSaveManagerUsable(GlobalRegistry.Save as SaveManager))", verifySingletons);
            StringAssert.Contains("SaveManager not found or not initialized.", verifySingletons);
            Assert.Less(
                loadOrNewGame.IndexOf("SaveManager save = GlobalRegistry.Save as SaveManager;", StringComparison.Ordinal),
                loadOrNewGame.IndexOf("if (!IsSaveManagerUsable(save))", StringComparison.Ordinal));
            Assert.Less(
                loadOrNewGame.IndexOf("if (!IsSaveManagerUsable(save))", StringComparison.Ordinal),
                loadOrNewGame.IndexOf("if (!SaveManager.TryResolveSafeSlotName(context.TargetSaveSlot, out string targetSaveSlot))", StringComparison.Ordinal));
            Assert.Less(
                loadOrNewGame.IndexOf("if (!SaveManager.TryResolveSafeSlotName(context.TargetSaveSlot, out string targetSaveSlot))", StringComparison.Ordinal),
                loadOrNewGame.IndexOf("if (!save.SaveExists(targetSaveSlot))", StringComparison.Ordinal));
            Assert.Less(
                loadOrNewGame.IndexOf("if (!save.SaveExists(targetSaveSlot))", StringComparison.Ordinal),
                loadOrNewGame.IndexOf("await save.LoadGameAsync(targetSaveSlot);", StringComparison.Ordinal));
            StringAssert.Contains("StartNewGameFromRejectedLoadContext();", loadOrNewGame);
            StringAssert.Contains("GameStartContextHolder.Current = GameStartContext.CreateNewGame();", rejectedLoadContext);
            StringAssert.Contains("_isLoadingSave = false;", rejectedLoadContext);
            StringAssert.Contains("InitNewGame();", rejectedLoadContext);
            StringAssert.DoesNotContain("save.SaveExists(context.TargetSaveSlot)", loadOrNewGame);
            StringAssert.DoesNotContain("save.LoadGameAsync(context.TargetSaveSlot)", loadOrNewGame);
            StringAssert.DoesNotContain("if (save == null)", loadOrNewGame);
            StringAssert.DoesNotContain("(GlobalRegistry.Save as SaveManager) == null", verifySingletons);
        }

        [Test]
        public void SaveStationManualSlotUsesAsyncPersistenceRequestBridge()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Interaction/SaveStation.cs");
            string interact = ExtractMethodBody(source, "public void Interact(Transform interactor)");
            string requestManual = ExtractMethodBody(source, "private bool TryRequestManualSlotSave(");
            string resolveLocalized = ExtractMethodBody(source, "private ReadOnlySpan<char> ResolveLocalizedSpan(");

            int serviceUnavailableIndex = interact.IndexOf("if (saveService == null || !saveService.IsInitialized)", StringComparison.Ordinal);
            int bridgeIndex = interact.IndexOf("if (TryRequestManualSlotSave(saveService, interactor))", StringComparison.Ordinal);
            int fallbackSoundIndex = interact.IndexOf("PlayInteractionSound();", bridgeIndex, StringComparison.Ordinal);
            int requestedIndex = interact.IndexOf("ShowHudInfo(LocalizationKeys.SAVE_STATION_REQUESTED, \"SAVE REQUESTED\");", fallbackSoundIndex, StringComparison.Ordinal);
            int fallbackIndex = interact.IndexOf("_ = saveService.SaveGameAsync(_saveSlot);", bridgeIndex, StringComparison.Ordinal);
            int fallbackInteractionIndex = interact.IndexOf("InteractionEvents.TryRaiseInteractionStarted(this, interactor);", fallbackIndex, StringComparison.Ordinal);
            int acceptedIndex = requestManual.IndexOf("if (accepted)", StringComparison.Ordinal);
            int rejectedIndex = requestManual.IndexOf("if (!accepted)", StringComparison.Ordinal);
            int rejectedReturnIndex = requestManual.IndexOf("return true;", rejectedIndex, StringComparison.Ordinal);
            int manualSoundIndex = requestManual.IndexOf("PlayInteractionSound();", rejectedReturnIndex, StringComparison.Ordinal);
            int manualRequestedIndex = requestManual.IndexOf("ShowHudInfo(LocalizationKeys.SAVE_STATION_REQUESTED, \"SAVE REQUESTED\");", manualSoundIndex, StringComparison.Ordinal);
            int manualInteractionIndex = requestManual.IndexOf("InteractionEvents.TryRaiseInteractionStarted(this, interactor);", manualRequestedIndex, StringComparison.Ordinal);

            Assert.GreaterOrEqual(serviceUnavailableIndex, 0);
            Assert.Greater(bridgeIndex, serviceUnavailableIndex);
            Assert.Greater(fallbackSoundIndex, bridgeIndex);
            Assert.Greater(requestedIndex, fallbackSoundIndex);
            Assert.Greater(fallbackIndex, requestedIndex);
            Assert.Greater(fallbackInteractionIndex, fallbackIndex);
            Assert.Less(acceptedIndex, 0);
            Assert.GreaterOrEqual(rejectedIndex, 0);
            Assert.Greater(rejectedReturnIndex, rejectedIndex);
            Assert.Greater(manualSoundIndex, rejectedReturnIndex);
            Assert.Greater(manualRequestedIndex, manualSoundIndex);
            Assert.Greater(manualInteractionIndex, manualRequestedIndex);
            StringAssert.Contains("private const uint SaveStationSourceHash", source);
            StringAssert.Contains("IInteractionStartedEventOwner", source);
            StringAssert.Contains("saveService as IAsyncPersistenceService", requestManual);
            StringAssert.Contains("SaveEvents.ResolveKnownSlotIndex(_saveSlot)", requestManual);
            StringAssert.Contains("slotIndex < 0 || slotIndex >= SaveEvents.ManualSlotCount", requestManual);
            StringAssert.Contains("bool accepted = asyncPersistence.TryRequestSave((byte)slotIndex, SaveStationSourceHash);", requestManual);
            StringAssert.Contains("if (!accepted)", requestManual);
            StringAssert.Contains("ShowHudInfo(LocalizationKeys.SAVE_STATION_REQUESTED, \"SAVE REQUESTED\");", requestManual);
            StringAssert.Contains("InteractionEvents.TryRaiseInteractionStarted(this, interactor);", requestManual);
            StringAssert.Contains("return true;", requestManual);
            StringAssert.DoesNotContain("ShowManualSaveRequestRejected", source);
            StringAssert.Contains("if (string.IsNullOrEmpty(key))", resolveLocalized);
            StringAssert.Contains("return fallback.AsSpan();", resolveLocalized);
            StringAssert.DoesNotContain("ShowHudWarning", requestManual);
            StringAssert.DoesNotContain("SaveEvents.Register(this)", source);
        }

        [Test]
        public void PlayerInteractionSuppressesDefaultStartedEventForSelfPublishingTargets()
        {
            string interactable = ReadProjectFile("Assets/_Project/Scripts/Interaction/IInteractable.cs");
            string playerInteraction = ReadProjectFile("Assets/_Project/Scripts/Interaction/PlayerInteraction.cs");
            string saveStation = ReadProjectFile("Assets/_Project/Scripts/Interaction/SaveStation.cs");
            string fabricator = ReadProjectFile("Assets/_Project/Scripts/Fabricator.cs");
            string emergencyServiceRelay = ReadProjectFile("Assets/_Project/Scripts/World/EmergencyServiceRelay.cs");
            string execute = ExtractMethodBody(playerInteraction, "private void ExecuteInteraction()");
            string defaultPublisher = ExtractMethodBody(playerInteraction, "private static bool TryRaiseDefaultInteractionStarted(");
            string defaultFeedback = ExtractMethodBody(playerInteraction, "private void QueueDefaultInteractionFeedback(");
            string ownerCheck = ExtractMethodBody(playerInteraction, "private static bool IsInteractionStartedEventOwner(");
            string normalizedExecute = execute.Replace("\r\n", "\n");

            StringAssert.Contains("public interface IInteractionStartedEventOwner", interactable);
            StringAssert.Contains("generic confirm feedback", interactable);
            StringAssert.Contains("IInteractable target = _currentHovered;", execute);
            StringAssert.Contains("TryRaiseDefaultInteractionStarted(target, transform);", execute);
            StringAssert.Contains("QueueDefaultInteractionFeedback(target);", execute);
            StringAssert.DoesNotContain("QueueStaticAudio(interactSound", execute);
            StringAssert.DoesNotContain("target.Interact(transform);\n            InteractionEvents.TryRaiseInteractionStarted", normalizedExecute);
            StringAssert.Contains("if (IsInteractionStartedEventOwner(target))", defaultPublisher);
            StringAssert.Contains("return false;", defaultPublisher);
            StringAssert.Contains("return InteractionEvents.TryRaiseInteractionStarted(target, interactor);", defaultPublisher);
            StringAssert.Contains("if (IsInteractionStartedEventOwner(target))", defaultFeedback);
            StringAssert.Contains("QueueStaticAudio(interactSound, 0.6f);", defaultFeedback);
            StringAssert.Contains("return target is IInteractionStartedEventOwner;", ownerCheck);
            StringAssert.Contains("IInteractionStartedEventOwner", saveStation);
            StringAssert.Contains("IInteractionStartedEventOwner", fabricator);
            StringAssert.Contains("IInteractionStartedEventOwner", emergencyServiceRelay);
            AssertRuntimeSelfPublishingInteractablesDeclareOwnerMarker();
        }

        [Test]
        public void SaveVerifierToolsUseCurrentManualSlotContract()
        {
            string stateRecovery = ReadProjectFile("Assets/_Project/Scripts/Tools/StateRecoveryVerifier.cs");
            string shellSmoke = ReadProjectFile("Assets/_Project/Scripts/Dev/ShellVerificationRuntimeSmokeTester.cs");
            string stateAwake = ExtractMethodBody(stateRecovery, "private void Awake()");
            string normalizeProbeOrder = ExtractMethodBody(stateRecovery, "private void NormalizeSaveSlotProbeOrder()");
            string stateResolveExisting = ExtractMethodBody(stateRecovery, "private string ResolveExistingSaveSlot()");
            string stateUsable = ExtractMethodBody(stateRecovery, "private static bool IsSaveManagerUsable(SaveManager saveManager)");
            string shellResolveExisting = ExtractMethodBody(shellSmoke, "private string ResolveExistingSaveSlot()");

            StringAssert.Contains("SaveEvents.ResolveManualSlotName(0)", stateRecovery);
            StringAssert.Contains("SaveEvents.ResolveManualSlotName(1)", stateRecovery);
            StringAssert.Contains("SaveEvents.ResolveManualSlotName(2)", stateRecovery);
            StringAssert.Contains("NormalizeSaveSlotProbeOrder();", stateAwake);
            StringAssert.Contains("int slotCount = SaveEvents.ManualSlotCount;", normalizeProbeOrder);
            StringAssert.Contains("string configured = current != null && i < current.Length ? current[i] : string.Empty;", normalizeProbeOrder);
            StringAssert.Contains("SaveManager.TryResolveSafeSlotName(configured, out string safeSlotName)", normalizeProbeOrder);
            StringAssert.Contains("safeSlotName = SaveEvents.ResolveManualSlotName(i);", normalizeProbeOrder);
            StringAssert.Contains("_saveSlotProbeOrder = normalized;", normalizeProbeOrder);
            StringAssert.Contains("NormalizeSaveSlotProbeOrder();", stateResolveExisting);
            StringAssert.Contains("if (!IsSaveManagerUsable(saveManager) || _saveSlotProbeOrder == null)", stateResolveExisting);
            StringAssert.Contains("SaveManager.TryResolveSafeSlotName(slotName, out slotName)", stateResolveExisting);
            StringAssert.Contains("saveManager.SaveExists(slotName)", stateResolveExisting);
            StringAssert.Contains("return saveManager != null && saveManager.IsInitialized;", stateUsable);
            StringAssert.Contains("if (saveManager == null || !saveManager.IsInitialized)", shellResolveExisting);
            StringAssert.Contains("for (int i = 0; i < SaveEvents.ManualSlotCount; i++)", shellResolveExisting);
            StringAssert.Contains("string slotName = SaveEvents.ResolveManualSlotName(i);", shellResolveExisting);
            StringAssert.Contains("saveManager.SaveExists(slotName)", shellResolveExisting);
            StringAssert.DoesNotContain("\"slot_3\"", stateRecovery);
            StringAssert.DoesNotContain("\"slot_3\"", shellSmoke);
        }

        private static string ReadProjectFile(string relativePath)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return File.ReadAllText(Path.Combine(root, relativePath));
        }

        private static void AssertRootLocalizationJsonContains(string value)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string sourceRoot = Path.Combine(root, "Assets", "_Project", "Scripts");
            Assert.IsTrue(Directory.Exists(sourceRoot), "Expected localization source root.");

            int checkedFileCount = 0;
            foreach (string file in Directory.EnumerateFiles(sourceRoot, "*.json", SearchOption.TopDirectoryOnly))
            {
                checkedFileCount++;
                string source = File.ReadAllText(file);
                StringAssert.Contains(value, source, "Missing recovery localization in " + Path.GetFileName(file));
            }

            Assert.GreaterOrEqual(checkedFileCount, 17, "Expected all root localization tables.");
        }

        private static void AssertRuntimeSelfPublishingInteractablesDeclareOwnerMarker()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string scriptsRoot = Path.Combine(root, "Assets", "_Project", "Scripts");
            string[] sourceFiles = Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < sourceFiles.Length; i++)
            {
                string path = sourceFiles[i];
                string normalizedPath = path.Replace('\\', '/');
                if (normalizedPath.Contains("/Editor/", StringComparison.Ordinal))
                    continue;

                string source = File.ReadAllText(path);
                if (!source.Contains("InteractionEvents.TryRaiseInteractionStarted(this, interactor);", StringComparison.Ordinal))
                    continue;

                if (!source.Contains("IInteractable", StringComparison.Ordinal))
                    continue;

                Assert.IsTrue(
                    source.Contains("IInteractionStartedEventOwner", StringComparison.Ordinal),
                    "Self-publishing interactable must suppress PlayerInteraction default event: " + normalizedPath);
            }
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(signatureIndex, 0, "Missing method signature: " + signature);
            int open = source.IndexOf('{', signatureIndex);
            Assert.GreaterOrEqual(open, 0, "Missing method open brace: " + signature);

            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '{')
                    depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(open, i - open + 1);
                }
            }

            Assert.Fail("Missing method close brace: " + signature);
            return string.Empty;
        }

        private static string ExtractMethodBodyContaining(string source, string signature, string requiredBodyText)
        {
            int searchIndex = 0;
            while (searchIndex < source.Length)
            {
                int signatureIndex = source.IndexOf(signature, searchIndex, StringComparison.Ordinal);
                Assert.GreaterOrEqual(signatureIndex, 0, "Missing method signature containing: " + requiredBodyText);
                string body = ExtractMethodBodyAt(source, signatureIndex, signature);
                if (body.IndexOf(requiredBodyText, StringComparison.Ordinal) >= 0)
                    return body;

                searchIndex = signatureIndex + signature.Length;
            }

            Assert.Fail("Missing method body containing: " + requiredBodyText);
            return string.Empty;
        }

        private static string ExtractMethodBodyAt(string source, int signatureIndex, string signature)
        {
            int open = source.IndexOf('{', signatureIndex);
            Assert.GreaterOrEqual(open, 0, "Missing method open brace: " + signature);

            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '{')
                    depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(open, i - open + 1);
                }
            }

            Assert.Fail("Missing method close brace: " + signature);
            return string.Empty;
        }
    }
}
