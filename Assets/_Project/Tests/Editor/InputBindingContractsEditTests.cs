using System;
using System.IO;
using System.Reflection;
using System.Text;
using Hecton8.Core;
using Hecton8.Input;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Hecton8.Tests.Editor
{
    public sealed class InputBindingContractsEditTests
    {
        [Test]
        public void InputBindingDtoLayoutsStayArm64Aligned()
        {
            Assert.AreEqual(0u, InputBindingLayoutGuard.Validate());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<InputActionStateDTO>());
            Assert.AreEqual(16, UnsafeUtility.SizeOf<AccessibilityConfigDTO>());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<InputBindingTelemetryEntry>());
            Assert.AreEqual(88, UnsafeUtility.SizeOf<ControlRemapIoResult>());
            Assert.AreEqual(0, UnsafeUtility.SizeOf<InputActionStateDTO>() & 7);
            Assert.AreEqual(0, UnsafeUtility.SizeOf<AccessibilityConfigDTO>() & 7);
            Assert.AreEqual(0, UnsafeUtility.SizeOf<InputBindingTelemetryEntry>() & 7);
            Assert.AreEqual(0, UnsafeUtility.SizeOf<ControlRemapIoResult>() & 7);
        }

        [Test]
        public void ScalabilityProfilesAreContinuousAndLegacySafe()
        {
            string contractsSource = File.ReadAllText(Path.Combine("Assets", "_Project", "Scripts", "Core", "BootstrapContracts", "InputBindingServiceContracts.cs"));
            string platformSource = File.ReadAllText(Path.Combine("Assets", "_Project", "Scripts", "Core", "IPlatformIntegration.cs"));

            Assert.AreEqual(ScalabilityTierProfiles.LowCompact, ScalabilityTierProfiles.Normalize(ScalabilityTierProfiles.LowCompact));
            Assert.AreEqual(ScalabilityTierProfiles.HighDiscrete, ScalabilityTierProfiles.Normalize(ScalabilityTierProfiles.LegacyHighDesktop));
            Assert.AreEqual(ScalabilityTierProfiles.Middle, ScalabilityTierProfiles.Normalize(ScalabilityTierProfiles.Middle));
            Assert.AreEqual(ScalabilityTierProfiles.HighDiscrete, ScalabilityTierProfiles.Normalize(ScalabilityTierProfiles.HighDiscrete));
            Assert.AreEqual(ScalabilityTierProfiles.Ultra, ScalabilityTierProfiles.Normalize(ScalabilityTierProfiles.Ultra));
            Assert.AreEqual(ScalabilityTierProfiles.Ultra, ScalabilityTierProfiles.Normalize(byte.MaxValue));
            Assert.Less(
                ScalabilityTierProfiles.ToGlobalQualityWeight01(ScalabilityTierProfiles.LowCompact),
                ScalabilityTierProfiles.ToGlobalQualityWeight01(ScalabilityTierProfiles.Middle));
            Assert.Less(
                ScalabilityTierProfiles.ToGlobalQualityWeight01(ScalabilityTierProfiles.Middle),
                ScalabilityTierProfiles.ToGlobalQualityWeight01(ScalabilityTierProfiles.HighDiscrete));
            Assert.Less(
                ScalabilityTierProfiles.ToGlobalQualityWeight01(ScalabilityTierProfiles.HighDiscrete),
                ScalabilityTierProfiles.ToGlobalQualityWeight01(ScalabilityTierProfiles.Ultra));
            StringAssert.DoesNotContain("case LegacyHighDesktop:", contractsSource);
            StringAssert.DoesNotContain("case ScalabilityTierProfiles.LegacyHighDesktop:", platformSource);
        }

        [Test]
        public void AccessibilityConstantBuffersUseLockBufferUsageFlag()
        {
            string sourcePath = Path.Combine("Assets", "_Project", "Scripts", "Input", "AccessibilitySettings.cs");
            string source = File.ReadAllText(sourcePath);

            StringAssert.Contains("GraphicsBuffer.UsageFlags.LockBufferForWrite", source);
            StringAssert.Contains("SystemInfo.supportsSetConstantBuffer", source);
            StringAssert.Contains("PublishDisabledConfig", source);
            StringAssert.Contains("Shader.SetGlobalVector(AccessibilityParamsId, Vector4.zero)", source);
            StringAssert.Contains("private void OnDestroy()", source);
            StringAssert.Contains("OnServiceShutdown();", source);
            StringAssert.Contains("internal static AccessibilitySettings ActiveRuntimeInstance", source);
            StringAssert.Contains("internal static bool TryResolveActiveRuntime(ref AccessibilitySettings target)", source);
            StringAssert.Contains("TryClaimRuntimeInstance", source);
            StringAssert.Contains("if (IsLiveRuntimeOwner(active))", source);
            StringAssert.Contains("ActiveRuntimeInstance = null;", source);
            StringAssert.Contains("_duplicateInstance", source);
            StringAssert.Contains("_serviceShutdownComplete = true;", source);
            Assert.Less(source.IndexOf("if (!TryClaimRuntimeInstance())", StringComparison.Ordinal), source.IndexOf("TryColdBootstrapBuffers();", StringComparison.Ordinal));
        }

        [Test]
        public void BootstrapCreatesAccessibilityRuntimeOwner()
        {
            string sourcePath = Path.Combine("Assets", "_Project", "Scripts", "Bootstrap", "GameBootstrapper.cs");
            string source = File.ReadAllText(sourcePath);

            StringAssert.Contains("AccessibilitySettings accessibilitySettings = null;", source);
            StringAssert.Contains("AccessibilitySettings.TryResolveActiveRuntime(ref accessibilitySettings)", source);
            StringAssert.Contains("GameObject accessibilityRoot = new GameObject(\"[AccessibilitySettings]\")", source);
            StringAssert.Contains("accessibilityRoot.AddComponent<AccessibilitySettings>()", source);
            StringAssert.Contains("PersistRuntimeService(accessibilitySettings);", source);
            Assert.Less(source.IndexOf("rebindingManager.BindNativeInputManager(inputManager);", StringComparison.Ordinal), source.IndexOf("AccessibilitySettings.TryResolveActiveRuntime", StringComparison.Ordinal));
            Assert.Less(source.IndexOf("PersistRuntimeService(accessibilitySettings);", StringComparison.Ordinal), source.IndexOf("ContextualPhysicalIkRuntime contextualIkRuntime", StringComparison.Ordinal));
        }

        [Test]
        public void BootstrapRebindingManagerUsesLiveRuntimeResolver()
        {
            string rebinding = File.ReadAllText(Path.Combine("Assets", "_Project", "Scripts", "Core", "RebindingManager.cs"));
            string bootstrap = File.ReadAllText(Path.Combine("Assets", "_Project", "Scripts", "Bootstrap", "GameBootstrapper.cs"));

            StringAssert.Contains("internal static bool TryResolveActiveRuntime(ref RebindingManager target)", rebinding);
            StringAssert.Contains("!active._registeredService", rebinding);
            StringAssert.Contains("RebindingManager rebindingManager = null;", bootstrap);
            StringAssert.Contains("RebindingManager.TryResolveActiveRuntime(ref rebindingManager)", bootstrap);
            StringAssert.DoesNotContain("RebindingManager rebindingManager = RebindingManager.ActiveRuntimeInstance;", bootstrap);
            Assert.Less(bootstrap.IndexOf("RebindingManager.TryResolveActiveRuntime(ref rebindingManager)", StringComparison.Ordinal), bootstrap.IndexOf("rebindingManager.BindNativeInputManager(inputManager);", StringComparison.Ordinal));
        }

        [Test]
        public void SettingsAccessibilityAppliesUseLiveRuntimeResolver()
        {
            string settingsPath = Path.Combine("Assets", "_Project", "Scripts", "UI", "SettingsManager.cs");
            string verifierPath = Path.Combine("Assets", "_Project", "Scripts", "Editor", "Narrative", "H8NarrativeApexVerifier.cs");
            string settings = File.ReadAllText(settingsPath);
            string verifier = File.ReadAllText(verifierPath);

            StringAssert.Contains("AccessibilitySettings.TryResolveActiveRuntime(ref accessibilitySettings)", settings);
            StringAssert.DoesNotContain("AccessibilitySettings accessibilitySettings = AccessibilitySettings.ActiveRuntimeInstance;", settings);
            StringAssert.Contains("TryApplyAccessibilityTextScale", settings);
            StringAssert.Contains("TryApplyAccessibilityUiMotionScale", settings);
            StringAssert.Contains("AccessibilitySettings.TryResolveActiveRuntime", verifier);
            StringAssert.DoesNotContain("AccessibilitySettings.ActiveRuntimeInstance", verifier);
        }

        [Test]
        public void InputDispatcherConsumersUseLiveRuntimeResolver()
        {
            string dispatcher = File.ReadAllText(Path.Combine("Assets", "_Project", "Scripts", "Core", "InputDispatcher.cs"));
            string bootstrap = File.ReadAllText(Path.Combine("Assets", "_Project", "Scripts", "Bootstrap", "GameBootstrapper.cs"));
            string vrSomatic = File.ReadAllText(Path.Combine("Assets", "_Project", "Scripts", "Gameplay", "VRSomaticProvider.cs"));
            string physicalHand = File.ReadAllText(Path.Combine("Assets", "_Project", "Scripts", "Interaction", "PhysicalHandController.cs"));
            string dropPodSeat = File.ReadAllText(Path.Combine("Assets", "_Project", "Scripts", "Vehicles", "DropPod", "DropPodSeatController.cs"));

            StringAssert.Contains("internal static bool TryResolveActiveRuntime(ref InputDispatcher target)", dispatcher);
            StringAssert.Contains("active == null || !active.isActiveAndEnabled", dispatcher);
            StringAssert.Contains("InputDispatcher.TryResolveActiveRuntime(ref dispatcher)", bootstrap);
            StringAssert.Contains("InputDispatcher.TryResolveActiveRuntime(ref dispatcher)", vrSomatic);
            StringAssert.Contains("InputDispatcher.TryResolveActiveRuntime(ref _inputDispatcher)", physicalHand);
            StringAssert.Contains("InputDispatcher.TryResolveActiveRuntime(ref dispatcher)", dropPodSeat);
            StringAssert.DoesNotContain("InputDispatcher dispatcher = InputDispatcher.ActiveRuntimeInstance;", bootstrap);
            StringAssert.DoesNotContain("InputDispatcher dispatcher = InputDispatcher.ActiveRuntimeInstance;", vrSomatic);
            StringAssert.DoesNotContain("_inputDispatcher = InputDispatcher.ActiveRuntimeInstance;", physicalHand);
            StringAssert.DoesNotContain("_inputBlockService = InputDispatcher.ActiveRuntimeInstance;", dropPodSeat);
        }

        [Test]
        public void UserOptionsPersistenceAvoidsDeleteGapAndBroadException()
        {
            string sourcePath = Path.Combine("Assets", "_Project", "Scripts", "Input", "UserOptionsPersistence.cs");
            string source = File.ReadAllText(sourcePath);

            StringAssert.DoesNotContain("catch (Exception)", source);
            StringAssert.DoesNotContain("throw new InvalidDataException", source);
            StringAssert.DoesNotContain("File.Delete(path)", source);
            StringAssert.DoesNotContain("JsonUtility", source);
            StringAssert.DoesNotContain("new FileInfo", source);
            StringAssert.Contains("new UTF8Encoding(false, true)", source);
            StringAssert.Contains("catch (DecoderFallbackException)", source);
            StringAssert.Contains("catch (EncoderFallbackException)", source);
            StringAssert.DoesNotContain("TryFindJsonProperty(", source);
            StringAssert.DoesNotContain("TryReadJsonStringProperty(", source);
            StringAssert.Contains("File.Replace(tempPath, path", source);
            StringAssert.Contains("FileOptions.WriteThrough", source);
            StringAssert.Contains("AsyncWriteManager.TryGetFileLength(tempPath, out long tempOptionsBytes, out string tempLengthError)", source);
            StringAssert.Contains("tempOptionsBytes != FixedOptionsFileBytes", source);
            StringAssert.Contains("AsyncWriteManager.FlushCriticalSavePath(tempPath, tempOptionsBytes, out string tempFlushError)", source);
            StringAssert.Contains("AsyncWriteManager.TryGetFileLength(path, out long promotedOptionsBytes, out string lengthError)", source);
            StringAssert.Contains("promotedOptionsBytes != FixedOptionsFileBytes", source);
            StringAssert.Contains("AsyncWriteManager.FlushCriticalSavePath(path, promotedOptionsBytes, out string flushError)", source);
            StringAssert.Contains("DeleteOptionsTempBestEffort(tempPath);", source);
            StringAssert.Contains("private static void DeleteOptionsTempBestEffort(string tempPath)", source);
            StringAssert.Contains("BinaryPayloadMagic", source);
            StringAssert.Contains("ApplyStagedOptionRecords", source);
            StringAssert.Contains("wasPortableContainer", source);
            StringAssert.Contains("bool loadedPortableOptions = TryReadPortableOptionsFile(path, out loadedScalabilityTier, out hasLoadedScalabilityTier, out bool wasPortableContainer);", source);
            StringAssert.Contains("if (!loadedPortableOptions)", source);
            StringAssert.Contains("if (wasPortableContainer ||", source);
            StringAssert.Contains("!TryApplyLegacyOptionsJson(ReadLegacyTextOptionsFile(path))", source);
            StringAssert.Contains("LogRejectedOptionsFile();", source);
            StringAssert.Contains("private static void LogRejectedOptionsFile()", source);
            StringAssert.Contains("Hecton8.Core.H8Debug.LogWarning(\"[UserOptionsPersistence] Rejected invalid options.h8cfg.\");", source);
            StringAssert.Contains("if (!payloadApplied)", source);
            StringAssert.Contains("if (index != payloadLength)", source);
            StringAssert.Contains("if (!TryReadLegacyOptionRecord(json, recordObjectStart, recordObjectEnd, out OptionRecord record))", source);
            StringAssert.Contains("if (tail != json.Length)", source);
            StringAssert.Contains("TryFindTopLevelJsonPropertyRange", source);
            StringAssert.Contains("if (afterArray != recordsValueEnd)", source);
            StringAssert.Contains("if (fileLength > FixedOptionsFileBytes)", source);
            StringAssert.Contains("if (version <= 0 || version > FileVersion)", source);
            StringAssert.Contains("TryFindJsonObjectEnd(json, recordObjectStart, recordsValueEnd, out int recordObjectEnd)", source);
            StringAssert.Contains("TryReadTopLevelJsonStringProperty(json, objectStart, objectEnd, \"Key\"", source);
            StringAssert.Contains("TryReadTopLevelJsonIntProperty(json, objectStart, objectEnd, \"Type\"", source);
            StringAssert.Contains("TryReadOptionalTopLevelJsonBoolProperty", source);
            StringAssert.Contains("out bool found", source);
            StringAssert.Contains("if (!found)", source);
            Assert.Less(source.IndexOf("if (!payloadApplied)", StringComparison.Ordinal), source.IndexOf("scalabilityTier = headerTier;", StringComparison.Ordinal));
            Assert.Less(source.IndexOf("if (index != payloadLength)", StringComparison.Ordinal), source.IndexOf("ApplyStagedOptionRecords(stagedRecords);", StringComparison.Ordinal));
            Assert.Less(source.IndexOf("if (tail != json.Length)", StringComparison.Ordinal), source.IndexOf("TryFindTopLevelJsonPropertyRange", StringComparison.Ordinal));
            int loadedPortableOptionsIndex = source.IndexOf("bool loadedPortableOptions = TryReadPortableOptionsFile(path, out loadedScalabilityTier, out hasLoadedScalabilityTier, out bool wasPortableContainer);", StringComparison.Ordinal);
            int loadedPortableFailureIndex = source.IndexOf("if (!loadedPortableOptions)", loadedPortableOptionsIndex, StringComparison.Ordinal);
            int rejectedPortableOrLegacyIndex = source.IndexOf("if (wasPortableContainer ||", loadedPortableFailureIndex, StringComparison.Ordinal);
            int rejectedOptionsLogIndex = source.IndexOf("LogRejectedOptionsFile();", rejectedPortableOrLegacyIndex, StringComparison.Ordinal);
            int applyLoadedTierIndex = source.IndexOf("ApplyLoadedScalabilityTier(loadedScalabilityTier, hasLoadedScalabilityTier);", rejectedOptionsLogIndex, StringComparison.Ordinal);
            Assert.GreaterOrEqual(loadedPortableOptionsIndex, 0, source);
            Assert.Greater(loadedPortableFailureIndex, loadedPortableOptionsIndex, source);
            Assert.Greater(rejectedPortableOrLegacyIndex, loadedPortableFailureIndex, source);
            Assert.Greater(rejectedOptionsLogIndex, rejectedPortableOrLegacyIndex, source);
            Assert.Greater(applyLoadedTierIndex, rejectedOptionsLogIndex, source);
            StringAssert.Contains("if (!TrySaveToDisk())", source);
            Assert.Less(source.IndexOf("if (!TrySaveToDisk())", StringComparison.Ordinal), source.IndexOf("PlatformIntegrationBridge.ApplyScalabilityTier(normalizedTier)", StringComparison.Ordinal));
            Assert.Less(source.IndexOf("PlatformIntegrationBridge.ApplyScalabilityTier(normalizedTier)", StringComparison.Ordinal), source.IndexOf("PlatformIntegrationBridge.PublishScalabilityChanged(previousTier, normalizedTier)", StringComparison.Ordinal));
            StringAssert.Contains("get => _scalabilityTier;", source);
            StringAssert.Contains("public string OptionsPath => _optionsPath ?? string.Empty;", source);
            StringAssert.Contains("public bool LastSaveSucceeded { get; private set; }", source);
            StringAssert.Contains("public void Save()", source);
            StringAssert.Contains("public bool TrySave()", source);
            StringAssert.Contains("TrySave();", source);
            StringAssert.Contains("LastSaveSucceeded = TrySaveToDisk();", source);
            StringAssert.Contains("return LastSaveSucceeded;", source);
            StringAssert.Contains("EnsureOptionsStoragePaths();", source);
            StringAssert.Contains("bool mayDeleteTemp = false;", source);
            StringAssert.Contains("if (!TryResolveAtomicOptionsPaths(path, tempPath, out string absolutePath, out string absoluteTempPath))", source);
            StringAssert.Contains("path = absolutePath;", source);
            StringAssert.Contains("tempPath = absoluteTempPath;", source);
            StringAssert.Contains("mayDeleteTemp = true;", source);
            StringAssert.Contains("if (mayDeleteTemp)", source);
            StringAssert.Contains("private static bool TryResolveAtomicOptionsPaths(string path, string tempPath, out string absolutePath, out string absoluteTempPath)", source);
            StringAssert.Contains("if (AreSameFullPath(absolutePath, absoluteTempPath))", source);
            StringAssert.Contains("if (!AreSameFullPath(directory ?? string.Empty, tempDirectory ?? string.Empty))", source);
            StringAssert.Contains("private static bool AreSameFullPath(string left, string right)", source);
            StringAssert.DoesNotContain("OptionsPath => ResolveOptionsPath", source);
            StringAssert.DoesNotContain("private string ResolveOptionsPath", source);
            StringAssert.DoesNotContain("private string ResolveOptionsTempPath", source);
            StringAssert.Contains("ReadOnlySpan<char> token = json.AsSpan", source);
            StringAssert.DoesNotContain("json.Substring(tokenStart", source);
            StringAssert.Contains("public bool HasKey(string key)", source);
            StringAssert.Contains("private bool TryGetRecord(string key, out OptionRecord record)", source);
            StringAssert.DoesNotContain("return _loaded && !string.IsNullOrWhiteSpace(key)", source);
            StringAssert.Contains("DestroyDuplicateInstance", source);
            StringAssert.Contains("_serviceShutdownComplete = true;", source);
            StringAssert.Contains("if (!TrySave())", source);
            StringAssert.Contains("Hecton8.Core.H8Debug.LogWarning(\"[UserOptionsPersistence] Failed to persist options.h8cfg during shutdown.\");", source);
            StringAssert.Contains("TryAbortForUsableExistingRuntime", source);
            StringAssert.Contains("IsUserOptionsRuntimeUsable", source);
            StringAssert.Contains("ReferenceEquals(registered, null) || ReferenceEquals(registered, this)", source);
            StringAssert.Contains("BootstrapRegistryBridge.Unregister(BootstrapRegistryBridgeSlot.UserOptionsRuntime, registered);", source);
            StringAssert.Contains("persistence._serviceRegistered", source);
            StringAssert.Contains("persistence.isActiveAndEnabled", source);
            StringAssert.Contains("!persistence._serviceShuttingDown", source);
            StringAssert.DoesNotContain("registered != null && registered != this", source);
            Assert.Less(source.IndexOf("BootstrapRegistryBridge.TryResolve(BootstrapRegistryBridgeSlot.UserOptionsRuntime", StringComparison.Ordinal), source.IndexOf("LoadFromDisk();", StringComparison.Ordinal));
            Assert.Less(source.IndexOf("if (TryAbortForUsableExistingRuntime())", StringComparison.Ordinal), source.IndexOf("LoadFromDisk();", StringComparison.Ordinal));
            int registerServiceIndex = source.IndexOf("private void RegisterService()", StringComparison.Ordinal);
            int registerGateIndex = source.IndexOf("if (TryAbortForUsableExistingRuntime())", registerServiceIndex, StringComparison.Ordinal);
            int registerCallIndex = source.IndexOf("BootstrapRegistryBridge.Register(BootstrapRegistryBridgeSlot.UserOptionsRuntime, this);", registerServiceIndex, StringComparison.Ordinal);
            Assert.GreaterOrEqual(registerGateIndex, registerServiceIndex);
            Assert.Less(registerGateIndex, registerCallIndex);

            int optionsSaveMethodIndex = source.IndexOf("private bool TrySaveToDisk()", StringComparison.Ordinal);
            Assert.GreaterOrEqual(optionsSaveMethodIndex, 0, source);
            int optionsResolveHelperIndex = source.IndexOf("private static bool TryResolveAtomicOptionsPaths", optionsSaveMethodIndex, StringComparison.Ordinal);
            Assert.Greater(optionsResolveHelperIndex, optionsSaveMethodIndex, source);
            string optionsSaveBody = source.Substring(optionsSaveMethodIndex, optionsResolveHelperIndex - optionsSaveMethodIndex);
            int mayDeleteDeclarationIndex = optionsSaveBody.IndexOf("bool mayDeleteTemp = false;", StringComparison.Ordinal);
            int resolveCallIndex = optionsSaveBody.IndexOf("if (!TryResolveAtomicOptionsPaths(path, tempPath, out string absolutePath, out string absoluteTempPath))", mayDeleteDeclarationIndex, StringComparison.Ordinal);
            int assignPathIndex = optionsSaveBody.IndexOf("path = absolutePath;", resolveCallIndex, StringComparison.Ordinal);
            int assignTempPathIndex = optionsSaveBody.IndexOf("tempPath = absoluteTempPath;", assignPathIndex, StringComparison.Ordinal);
            int createDirectoryIndex = optionsSaveBody.IndexOf("Directory.CreateDirectory(directory);", assignTempPathIndex, StringComparison.Ordinal);
            int mayDeleteTrueIndex = optionsSaveBody.IndexOf("mayDeleteTemp = true;", createDirectoryIndex, StringComparison.Ordinal);
            int optionsWriteIndex = optionsSaveBody.IndexOf("if (!WritePortableOptionsFile(tempPath, _writeRecords, recordCount))", mayDeleteTrueIndex, StringComparison.Ordinal);
            int optionsFinallyIndex = optionsSaveBody.IndexOf("finally", optionsWriteIndex, StringComparison.Ordinal);
            int cleanupGateIndex = optionsSaveBody.IndexOf("if (mayDeleteTemp)", optionsFinallyIndex, StringComparison.Ordinal);
            int cleanupCallIndex = optionsSaveBody.IndexOf("DeleteOptionsTempBestEffort(tempPath);", cleanupGateIndex, StringComparison.Ordinal);
            Assert.GreaterOrEqual(mayDeleteDeclarationIndex, 0, optionsSaveBody);
            Assert.Greater(resolveCallIndex, mayDeleteDeclarationIndex, optionsSaveBody);
            Assert.Greater(assignPathIndex, resolveCallIndex, optionsSaveBody);
            Assert.Greater(assignTempPathIndex, assignPathIndex, optionsSaveBody);
            Assert.Greater(createDirectoryIndex, assignTempPathIndex, optionsSaveBody);
            Assert.Greater(mayDeleteTrueIndex, createDirectoryIndex, optionsSaveBody);
            Assert.Greater(optionsWriteIndex, mayDeleteTrueIndex, optionsSaveBody);
            Assert.Greater(optionsFinallyIndex, optionsWriteIndex, optionsSaveBody);
            Assert.Greater(cleanupGateIndex, optionsFinallyIndex, optionsSaveBody);
            Assert.Greater(cleanupCallIndex, cleanupGateIndex, optionsSaveBody);

            int optionsReplaceMethodIndex = source.IndexOf("private static void ReplaceOptionsFile(string tempPath, string path)", optionsResolveHelperIndex, StringComparison.Ordinal);
            Assert.Greater(optionsReplaceMethodIndex, optionsResolveHelperIndex, source);
            string optionsResolveBody = source.Substring(optionsResolveHelperIndex, optionsReplaceMethodIndex - optionsResolveHelperIndex);
            int resolveAbsolutePathIndex = optionsResolveBody.IndexOf("absolutePath = Path.GetFullPath(path);", StringComparison.Ordinal);
            int resolveAbsoluteTempPathIndex = optionsResolveBody.IndexOf("absoluteTempPath = Path.GetFullPath(tempPath);", resolveAbsolutePathIndex, StringComparison.Ordinal);
            int samePathGuardIndex = optionsResolveBody.IndexOf("if (AreSameFullPath(absolutePath, absoluteTempPath))", resolveAbsoluteTempPathIndex, StringComparison.Ordinal);
            int samePathReturnIndex = optionsResolveBody.IndexOf("return false;", samePathGuardIndex, StringComparison.Ordinal);
            int resolveDirectoryIndex = optionsResolveBody.IndexOf("string directory = Path.GetDirectoryName(absolutePath);", samePathReturnIndex, StringComparison.Ordinal);
            int resolveTempDirectoryIndex = optionsResolveBody.IndexOf("string tempDirectory = Path.GetDirectoryName(absoluteTempPath);", resolveDirectoryIndex, StringComparison.Ordinal);
            int sameDirectoryGuardIndex = optionsResolveBody.IndexOf("if (!AreSameFullPath(directory ?? string.Empty, tempDirectory ?? string.Empty))", resolveTempDirectoryIndex, StringComparison.Ordinal);
            int sameDirectoryReturnIndex = optionsResolveBody.IndexOf("return false;", sameDirectoryGuardIndex, StringComparison.Ordinal);
            Assert.GreaterOrEqual(resolveAbsolutePathIndex, 0, optionsResolveBody);
            Assert.Greater(resolveAbsoluteTempPathIndex, resolveAbsolutePathIndex, optionsResolveBody);
            Assert.Greater(samePathGuardIndex, resolveAbsoluteTempPathIndex, optionsResolveBody);
            Assert.Greater(samePathReturnIndex, samePathGuardIndex, optionsResolveBody);
            Assert.Greater(resolveDirectoryIndex, samePathReturnIndex, optionsResolveBody);
            Assert.Greater(resolveTempDirectoryIndex, resolveDirectoryIndex, optionsResolveBody);
            Assert.Greater(sameDirectoryGuardIndex, resolveTempDirectoryIndex, optionsResolveBody);
            Assert.Greater(sameDirectoryReturnIndex, sameDirectoryGuardIndex, optionsResolveBody);

            int hasKeyIndex = source.IndexOf("public bool HasKey(string key)", StringComparison.Ordinal);
            int getIntIndex = source.IndexOf("public int GetInt(string key, int defaultValue = 0)", hasKeyIndex, StringComparison.Ordinal);
            Assert.GreaterOrEqual(hasKeyIndex, 0, source);
            Assert.Greater(getIntIndex, hasKeyIndex, source);
            string hasKeyBody = source.Substring(hasKeyIndex, getIntIndex - hasKeyIndex);
            StringAssert.Contains("if (string.IsNullOrWhiteSpace(key))", hasKeyBody);
            StringAssert.Contains("EnsureLoaded();", hasKeyBody);
            StringAssert.Contains("return _records.ContainsKey(key);", hasKeyBody);

            int tryGetRecordIndex = source.IndexOf("private bool TryGetRecord(string key, out OptionRecord record)", StringComparison.Ordinal);
            int ensureOptionsStoragePathsIndex = source.IndexOf("private void EnsureOptionsStoragePaths()", tryGetRecordIndex, StringComparison.Ordinal);
            Assert.GreaterOrEqual(tryGetRecordIndex, 0, source);
            Assert.Greater(ensureOptionsStoragePathsIndex, tryGetRecordIndex, source);
            string tryGetRecordBody = source.Substring(tryGetRecordIndex, ensureOptionsStoragePathsIndex - tryGetRecordIndex);
            StringAssert.Contains("if (string.IsNullOrWhiteSpace(key))", tryGetRecordBody);
            StringAssert.Contains("EnsureLoaded();", tryGetRecordBody);
            StringAssert.Contains("if (_records.TryGetValue(key, out record))", tryGetRecordBody);

            int writeMethodIndex = source.IndexOf("private bool WritePortableOptionsFile(", StringComparison.Ordinal);
            Assert.GreaterOrEqual(writeMethodIndex, 0, source);
            int preWriteInvalidationIndex = source.IndexOf("AsyncWriteManager.InvalidateCachedReadWindows(path);", writeMethodIndex, StringComparison.Ordinal);
            Assert.Greater(preWriteInvalidationIndex, writeMethodIndex, source);
            int writeStreamIndex = source.IndexOf("new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough)", preWriteInvalidationIndex, StringComparison.Ordinal);
            Assert.Greater(writeStreamIndex, preWriteInvalidationIndex, source);
            int streamFlushIndex = source.IndexOf("stream.Flush(true);", writeStreamIndex, StringComparison.Ordinal);
            Assert.Greater(streamFlushIndex, writeStreamIndex, source);
            int postWriteInvalidationIndex = source.IndexOf("AsyncWriteManager.InvalidateCachedReadWindows(path);", streamFlushIndex, StringComparison.Ordinal);
            Assert.Greater(postWriteInvalidationIndex, streamFlushIndex, source);

            int replaceMethodIndex = source.IndexOf("private static void ReplaceOptionsFile(string tempPath, string path)", StringComparison.Ordinal);
            Assert.GreaterOrEqual(replaceMethodIndex, 0, source);
            int tempFlushIndex = source.IndexOf("AsyncWriteManager.FlushCriticalSavePath(tempPath, tempOptionsBytes, out string tempFlushError)", replaceMethodIndex, StringComparison.Ordinal);
            Assert.Greater(tempFlushIndex, replaceMethodIndex, source);
            int prePromoteTempInvalidationIndex = source.IndexOf("AsyncWriteManager.InvalidateCachedReadWindows(tempPath);", tempFlushIndex, StringComparison.Ordinal);
            Assert.Greater(prePromoteTempInvalidationIndex, tempFlushIndex, source);
            int prePromoteFinalInvalidationIndex = source.IndexOf("AsyncWriteManager.InvalidateCachedReadWindows(path);", prePromoteTempInvalidationIndex, StringComparison.Ordinal);
            Assert.Greater(prePromoteFinalInvalidationIndex, prePromoteTempInvalidationIndex, source);
            int replaceIndex = source.IndexOf("File.Replace(tempPath, path, null, ignoreMetadataErrors: true);", prePromoteFinalInvalidationIndex, StringComparison.Ordinal);
            Assert.Greater(replaceIndex, prePromoteFinalInvalidationIndex, source);
            int moveIndex = source.IndexOf("File.Move(tempPath, path);", replaceIndex, StringComparison.Ordinal);
            Assert.Greater(moveIndex, replaceIndex, source);
            int postPromoteTempInvalidationIndex = source.IndexOf("AsyncWriteManager.InvalidateCachedReadWindows(tempPath);", moveIndex, StringComparison.Ordinal);
            Assert.Greater(postPromoteTempInvalidationIndex, moveIndex, source);
            int postPromoteFinalInvalidationIndex = source.IndexOf("AsyncWriteManager.InvalidateCachedReadWindows(path);", postPromoteTempInvalidationIndex, StringComparison.Ordinal);
            Assert.Greater(postPromoteFinalInvalidationIndex, postPromoteTempInvalidationIndex, source);
            int promotedFlushIndex = source.IndexOf("AsyncWriteManager.FlushCriticalSavePath(path, promotedOptionsBytes, out string flushError)", postPromoteFinalInvalidationIndex, StringComparison.Ordinal);
            Assert.Greater(promotedFlushIndex, postPromoteFinalInvalidationIndex, source);

            int cleanupHelperIndex = source.IndexOf("private static void DeleteOptionsTempBestEffort(string tempPath)", StringComparison.Ordinal);
            Assert.GreaterOrEqual(cleanupHelperIndex, 0, source);
            int cleanupInvalidationIndex = source.IndexOf("AsyncWriteManager.InvalidateCachedReadWindows(tempPath);", cleanupHelperIndex, StringComparison.Ordinal);
            Assert.Greater(cleanupInvalidationIndex, cleanupHelperIndex, source);
            int cleanupDeleteIndex = source.IndexOf("File.Delete(tempPath);", cleanupInvalidationIndex, StringComparison.Ordinal);
            Assert.Greater(cleanupDeleteIndex, cleanupInvalidationIndex, source);
            int cleanupFinallyIndex = source.IndexOf("finally", cleanupDeleteIndex, StringComparison.Ordinal);
            Assert.Greater(cleanupFinallyIndex, cleanupDeleteIndex, source);
            int cleanupPostInvalidationIndex = source.IndexOf("AsyncWriteManager.InvalidateCachedReadWindows(tempPath);", cleanupFinallyIndex, StringComparison.Ordinal);
            Assert.Greater(cleanupPostInvalidationIndex, cleanupFinallyIndex, source);
        }

        [Test]
        public void UserOptionsSaveRejectsTempPathCollisionWithoutTruncatingExistingFile()
        {
            string directory = CreateTempDirectory("h8_options_collision_");
            string path = Path.Combine(directory, UserOptionsPersistence.FileName);
            byte[] sentinel = Encoding.UTF8.GetBytes("existing-options-sentinel");
            File.WriteAllBytes(path, sentinel);
            GameObject root = null;

            try
            {
                UserOptionsPersistence persistence = CreateUserOptionsPersistenceForDiskTest(directory, path, path, out root);
                persistence.SetInt("Hecton_Test", 7);

                bool saved = persistence.TrySave();

                Assert.IsFalse(saved);
                Assert.IsFalse(persistence.LastSaveSucceeded);
                CollectionAssert.AreEqual(sentinel, File.ReadAllBytes(path));
            }
            finally
            {
                DestroyImmediateIfNotNull(root);
                DeleteDirectoryBestEffort(directory);
            }
        }

        [Test]
        public void UserOptionsSaveRejectsCrossDirectoryTempWithoutWritingFiles()
        {
            string finalDirectory = CreateTempDirectory("h8_options_final_");
            string tempDirectory = CreateTempDirectory("h8_options_temp_");
            string path = Path.Combine(finalDirectory, UserOptionsPersistence.FileName);
            string tempPath = Path.Combine(tempDirectory, UserOptionsPersistence.FileName + ".tmp");
            GameObject root = null;

            try
            {
                UserOptionsPersistence persistence = CreateUserOptionsPersistenceForDiskTest(finalDirectory, path, tempPath, out root);
                persistence.SetInt("Hecton_Test", 9);

                bool saved = persistence.TrySave();

                Assert.IsFalse(saved);
                Assert.IsFalse(persistence.LastSaveSucceeded);
                Assert.IsFalse(File.Exists(path));
                Assert.IsFalse(File.Exists(tempPath));
            }
            finally
            {
                DestroyImmediateIfNotNull(root);
                DeleteDirectoryBestEffort(finalDirectory);
                DeleteDirectoryBestEffort(tempDirectory);
            }
        }

        [Test]
        public void UserOptionsPublicReadApiLazilyLoadsSavedRecords()
        {
            string directory = CreateTempDirectory("h8_options_lazy_read_");
            string path = Path.Combine(directory, UserOptionsPersistence.FileName);
            string tempPath = path + ".tmp";
            GameObject root = null;

            try
            {
                UserOptionsPersistence persistence = CreateUserOptionsPersistenceForDiskTest(directory, path, tempPath, out root);
                persistence.SetInt("Hecton_Test", 123);
                Assert.IsTrue(persistence.TrySave());

                SetPrivateInstanceField(persistence, "_loaded", false);

                Assert.IsTrue(persistence.HasKey("Hecton_Test"));
                Assert.AreEqual(123, persistence.GetInt("Hecton_Test", -1));
            }
            finally
            {
                DestroyImmediateIfNotNull(root);
                DeleteDirectoryBestEffort(directory);
            }
        }

        [Test]
        public void UserOptionsLoadRejectsInvalidPortableFileWithoutKeepingStaleRecords()
        {
            string directory = CreateTempDirectory("h8_options_bad_portable_");
            string path = Path.Combine(directory, UserOptionsPersistence.FileName);
            string tempPath = path + ".tmp";
            File.WriteAllBytes(path, BuildInvalidPortableOptionsHeader());
            GameObject root = null;

            try
            {
                UserOptionsPersistence persistence = CreateUserOptionsPersistenceForDiskTest(directory, path, tempPath, out root);
                persistence.SetInt("Hecton_Test", 55);
                SetPrivateInstanceField(persistence, "_loaded", false);

                int loaded = persistence.GetInt("Hecton_Test", -1);

                Assert.AreEqual(-1, loaded);
                Assert.IsFalse(persistence.HasKey("Hecton_Test"));
            }
            finally
            {
                DestroyImmediateIfNotNull(root);
                DeleteDirectoryBestEffort(directory);
            }
        }

        [Test]
        public void UserOptionsLoadRejectsInvalidLegacyFileWithoutApplyingPartialRecords()
        {
            string directory = CreateTempDirectory("h8_options_bad_legacy_");
            string path = Path.Combine(directory, UserOptionsPersistence.FileName);
            string tempPath = path + ".tmp";
            const string legacyJson = "{\"Records\":[{\"Key\":\"Hecton_Test\",\"Type\":1,\"IntValue\":42,\"FloatValue\":0,\"StringValue\":\"\",\"BoolValue\":false},{\"Key\":\"Broken\",\"Type\":999,\"IntValue\":1}]}";
            File.WriteAllText(path, legacyJson, new UTF8Encoding(false, true));
            GameObject root = null;

            try
            {
                UserOptionsPersistence persistence = CreateUserOptionsPersistenceForDiskTest(directory, path, tempPath, out root);
                persistence.SetInt("Hecton_Test", 55);
                SetPrivateInstanceField(persistence, "_loaded", false);

                int loaded = persistence.GetInt("Hecton_Test", -1);

                Assert.AreEqual(-1, loaded);
                Assert.IsFalse(persistence.HasKey("Hecton_Test"));
            }
            finally
            {
                DestroyImmediateIfNotNull(root);
                DeleteDirectoryBestEffort(directory);
            }
        }

        [Test]
        public void ControlRemapperLoadUsesCanonicalPathAndInvalidatesReadCache()
        {
            string sourcePath = Path.Combine("Assets", "_Project", "Scripts", "Input", "ControlRemapper.cs");
            string source = File.ReadAllText(sourcePath);

            int loadIndex = source.IndexOf("public static bool TryLoadOverrides(", StringComparison.Ordinal);
            Assert.GreaterOrEqual(loadIndex, 0, source);
            int clearIndex = source.IndexOf("private static bool TryClearBindingOverrides(", loadIndex, StringComparison.Ordinal);
            Assert.Greater(clearIndex, loadIndex, source);
            string loadBody = source.Substring(loadIndex, clearIndex - loadIndex);

            StringAssert.Contains("string absolutePath = null;", loadBody);
            StringAssert.Contains("absolutePath = Path.GetFullPath(path);", loadBody);
            StringAssert.Contains("AsyncWriteManager.InvalidateCachedReadWindows(absolutePath);", loadBody);
            StringAssert.Contains("if (!File.Exists(absolutePath))", loadBody);
            StringAssert.Contains("int byteCount = TryReadAllCold(absolutePath, bytesPtr, fileBytes.Length, ref result);", loadBody);
            StringAssert.Contains("result.Telemetry = BuildTelemetry(InputBindingTelemetryOperation.Load, result.ResultCode, result.FaultFlags, result.ByteCount, 0, 0, 0, startTicks);", loadBody);
            StringAssert.Contains("catch (System.Security.SecurityException)", loadBody);
            StringAssert.Contains("if (!string.IsNullOrEmpty(absolutePath))", loadBody);

            int fullPathIndex = loadBody.IndexOf("absolutePath = Path.GetFullPath(path);", StringComparison.Ordinal);
            int invalidateIndex = loadBody.IndexOf("AsyncWriteManager.InvalidateCachedReadWindows(absolutePath);", fullPathIndex, StringComparison.Ordinal);
            int existsIndex = loadBody.IndexOf("if (!File.Exists(absolutePath))", invalidateIndex, StringComparison.Ordinal);
            int readIndex = loadBody.IndexOf("int byteCount = TryReadAllCold(absolutePath, bytesPtr, fileBytes.Length, ref result);", existsIndex, StringComparison.Ordinal);
            int finallyIndex = loadBody.IndexOf("finally", readIndex, StringComparison.Ordinal);
            int finalInvalidateIndex = loadBody.IndexOf("AsyncWriteManager.InvalidateCachedReadWindows(absolutePath);", finallyIndex, StringComparison.Ordinal);
            Assert.GreaterOrEqual(fullPathIndex, 0, loadBody);
            Assert.Greater(invalidateIndex, fullPathIndex, loadBody);
            Assert.Greater(existsIndex, invalidateIndex, loadBody);
            Assert.Greater(readIndex, existsIndex, loadBody);
            Assert.Greater(finallyIndex, readIndex, loadBody);
            Assert.Greater(finalInvalidateIndex, finallyIndex, loadBody);
            Assert.IsFalse(loadBody.Contains("File.Exists(path)", StringComparison.Ordinal));
            Assert.IsFalse(loadBody.Contains("TryReadAllCold(path", StringComparison.Ordinal));

            int readAllIndex = source.IndexOf("private static int TryReadAllCold(", StringComparison.Ordinal);
            Assert.GreaterOrEqual(readAllIndex, 0, source);
            int writeAtomicIndex = source.IndexOf("private static bool TryWriteAtomicCold(", readAllIndex, StringComparison.Ordinal);
            Assert.Greater(writeAtomicIndex, readAllIndex, source);
            string readAllBody = source.Substring(readAllIndex, writeAtomicIndex - readAllIndex);

            StringAssert.Contains("AsyncWriteManager.InvalidateCachedReadWindows(path);", readAllBody);
            StringAssert.Contains("AsyncWriteManager.TryGetFileLength(path, out long cachedLength, out _)", readAllBody);
            StringAssert.Contains("result.ResultCode = InputBindingTelemetryResult.IoFailure;", readAllBody);
            StringAssert.Contains("result.FaultFlags |= InputBindingFaultFlags.IoException;", readAllBody);
            StringAssert.Contains("result.ByteCount = cachedLength > int.MaxValue ? int.MaxValue : (int)Math.Max(0L, cachedLength);", readAllBody);
            StringAssert.Contains("if (cachedLength <= 0 ||", readAllBody);
            StringAssert.Contains("cachedLength > capacity", readAllBody);
            StringAssert.Contains("result.ByteCount = length > int.MaxValue ? int.MaxValue : (int)Math.Max(0L, length);", readAllBody);
            StringAssert.Contains("result.ByteCount = total;", readAllBody);
            StringAssert.Contains("length != cachedLength", readAllBody);
            StringAssert.Contains("finally", readAllBody);

            int preReadInvalidateIndex = readAllBody.IndexOf("AsyncWriteManager.InvalidateCachedReadWindows(path);", StringComparison.Ordinal);
            int lengthIndex = readAllBody.IndexOf("AsyncWriteManager.TryGetFileLength(path, out long cachedLength, out _)", preReadInvalidateIndex, StringComparison.Ordinal);
            int lengthFailureIndex = readAllBody.IndexOf("result.ResultCode = InputBindingTelemetryResult.IoFailure;", lengthIndex, StringComparison.Ordinal);
            int cachedByteCountIndex = readAllBody.IndexOf("result.ByteCount = cachedLength > int.MaxValue ? int.MaxValue : (int)Math.Max(0L, cachedLength);", lengthFailureIndex, StringComparison.Ordinal);
            int cachedBoundsIndex = readAllBody.IndexOf("if (cachedLength <= 0 ||", cachedByteCountIndex, StringComparison.Ordinal);
            int streamIndex = readAllBody.IndexOf("new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, FileStreamBufferBytes, FileOptions.SequentialScan)", cachedBoundsIndex, StringComparison.Ordinal);
            int mismatchIndex = readAllBody.IndexOf("if (length != cachedLength)", streamIndex, StringComparison.Ordinal);
            int mismatchByteCountIndex = readAllBody.IndexOf("result.ByteCount = length > int.MaxValue ? int.MaxValue : (int)Math.Max(0L, length);", mismatchIndex, StringComparison.Ordinal);
            int mismatchFailureIndex = readAllBody.IndexOf("result.ResultCode = InputBindingTelemetryResult.IoFailure;", mismatchByteCountIndex, StringComparison.Ordinal);
            int partialReadIndex = readAllBody.IndexOf("if (total != span.Length)", mismatchFailureIndex, StringComparison.Ordinal);
            int partialByteCountIndex = readAllBody.IndexOf("result.ByteCount = total;", partialReadIndex, StringComparison.Ordinal);
            int postReadFinallyIndex = readAllBody.IndexOf("finally", streamIndex, StringComparison.Ordinal);
            int postReadInvalidateIndex = readAllBody.IndexOf("AsyncWriteManager.InvalidateCachedReadWindows(path);", postReadFinallyIndex, StringComparison.Ordinal);
            Assert.GreaterOrEqual(preReadInvalidateIndex, 0, readAllBody);
            Assert.Greater(lengthIndex, preReadInvalidateIndex, readAllBody);
            Assert.Greater(lengthFailureIndex, lengthIndex, readAllBody);
            Assert.Greater(cachedByteCountIndex, lengthFailureIndex, readAllBody);
            Assert.Greater(cachedBoundsIndex, cachedByteCountIndex, readAllBody);
            Assert.Greater(streamIndex, cachedBoundsIndex, readAllBody);
            Assert.Greater(mismatchIndex, streamIndex, readAllBody);
            Assert.Greater(mismatchByteCountIndex, mismatchIndex, readAllBody);
            Assert.Greater(mismatchFailureIndex, mismatchByteCountIndex, readAllBody);
            Assert.Greater(partialReadIndex, mismatchFailureIndex, readAllBody);
            Assert.Greater(partialByteCountIndex, partialReadIndex, readAllBody);
            Assert.Greater(postReadFinallyIndex, streamIndex, readAllBody);
            Assert.Greater(postReadInvalidateIndex, postReadFinallyIndex, readAllBody);
        }

        [Test]
        public void ControlRemapperAtomicSaveInvalidatesReadCacheAroundPromotionAndCleanup()
        {
            string sourcePath = Path.Combine("Assets", "_Project", "Scripts", "Input", "ControlRemapper.cs");
            string source = File.ReadAllText(sourcePath);

            int methodIndex = source.IndexOf(
                "private static bool TryWriteAtomicCold(string path, string tempPath, byte* source, int byteCount, ref ControlRemapIoResult result)",
                StringComparison.Ordinal);
            Assert.GreaterOrEqual(methodIndex, 0, source);

            int helperIndex = source.IndexOf(
                "private static void TryDeleteTempAfterIoFailureCold(string tempPath, ref ControlRemapIoResult result)",
                methodIndex,
                StringComparison.Ordinal);
            Assert.Greater(helperIndex, methodIndex, source);

            string methodBody = source.Substring(methodIndex, helperIndex - methodIndex);
            StringAssert.Contains("string absolutePath = null;", methodBody);
            StringAssert.Contains("string absoluteTempPath = null;", methodBody);
            StringAssert.Contains("absolutePath = Path.GetFullPath(path);", methodBody);
            StringAssert.Contains("absoluteTempPath = Path.GetFullPath(tempPath);", methodBody);
            StringAssert.Contains("Path.GetDirectoryName(absolutePath)", methodBody);
            StringAssert.Contains("AsyncWriteManager.TryGetFileLength(absoluteTempPath, out long tempBytes, out _)", methodBody);
            StringAssert.Contains("AsyncWriteManager.FlushCriticalSavePath(absoluteTempPath, tempBytes, out _)", methodBody);
            StringAssert.Contains("AsyncWriteManager.TryGetFileLength(absolutePath, out long promotedBytes, out _)", methodBody);
            StringAssert.Contains("AsyncWriteManager.FlushCriticalSavePath(absolutePath, promotedBytes, out _)", methodBody);
            StringAssert.Contains("TryDeleteTempAfterIoFailureCold(absoluteTempPath, ref result);", methodBody);
            StringAssert.Contains("TryDeleteTempAfterIoFailureCold(absoluteTempPath ?? tempPath, ref result);", methodBody);
            StringAssert.Contains("if (AreSameFullPath(absolutePath, absoluteTempPath))", methodBody);
            StringAssert.Contains("string tempDirectory = Path.GetDirectoryName(absoluteTempPath);", methodBody);
            StringAssert.Contains("if (!AreSameFullPath(directory ?? string.Empty, tempDirectory ?? string.Empty))", methodBody);

            int absolutePathIndex = methodBody.IndexOf("absolutePath = Path.GetFullPath(path);", StringComparison.Ordinal);
            int absoluteTempPathIndex = methodBody.IndexOf("absoluteTempPath = Path.GetFullPath(tempPath);", absolutePathIndex, StringComparison.Ordinal);
            int collisionGuardIndex = methodBody.IndexOf("if (AreSameFullPath(absolutePath, absoluteTempPath))", absoluteTempPathIndex, StringComparison.Ordinal);
            int collisionFailureIndex = methodBody.IndexOf("MarkIoFailureNoTelemetry(ref result);", collisionGuardIndex, StringComparison.Ordinal);
            int collisionReturnIndex = methodBody.IndexOf("return false;", collisionFailureIndex, StringComparison.Ordinal);
            int directoryIndex = methodBody.IndexOf("string directory = Path.GetDirectoryName(absolutePath);", collisionReturnIndex, StringComparison.Ordinal);
            int tempDirectoryIndex = methodBody.IndexOf("string tempDirectory = Path.GetDirectoryName(absoluteTempPath);", directoryIndex, StringComparison.Ordinal);
            int directoryGuardIndex = methodBody.IndexOf("if (!AreSameFullPath(directory ?? string.Empty, tempDirectory ?? string.Empty))", tempDirectoryIndex, StringComparison.Ordinal);
            int directoryFailureIndex = methodBody.IndexOf("MarkIoFailureNoTelemetry(ref result);", directoryGuardIndex, StringComparison.Ordinal);
            int unsupportedPathIndex = methodBody.IndexOf("result.FaultFlags |= InputBindingFaultFlags.UnsupportedPath;", directoryFailureIndex, StringComparison.Ordinal);
            int directoryReturnIndex = methodBody.IndexOf("return false;", unsupportedPathIndex, StringComparison.Ordinal);
            int preWriteInvalidationIndex = methodBody.IndexOf("AsyncWriteManager.InvalidateCachedReadWindows(absoluteTempPath);", directoryReturnIndex, StringComparison.Ordinal);
            Assert.GreaterOrEqual(absolutePathIndex, 0, methodBody);
            Assert.Greater(absoluteTempPathIndex, absolutePathIndex, methodBody);
            Assert.Greater(collisionGuardIndex, absoluteTempPathIndex, methodBody);
            Assert.Greater(collisionFailureIndex, collisionGuardIndex, methodBody);
            Assert.Greater(collisionReturnIndex, collisionFailureIndex, methodBody);
            Assert.Greater(directoryIndex, collisionReturnIndex, methodBody);
            Assert.Greater(tempDirectoryIndex, directoryIndex, methodBody);
            Assert.Greater(directoryGuardIndex, tempDirectoryIndex, methodBody);
            Assert.Greater(directoryFailureIndex, directoryGuardIndex, methodBody);
            Assert.Greater(unsupportedPathIndex, directoryFailureIndex, methodBody);
            Assert.Greater(directoryReturnIndex, unsupportedPathIndex, methodBody);
            Assert.Greater(preWriteInvalidationIndex, directoryReturnIndex, methodBody);
            int writeStreamIndex = methodBody.IndexOf("new FileStream(absoluteTempPath, FileMode.Create, FileAccess.Write, FileShare.None, FileStreamBufferBytes, FileOptions.WriteThrough)", preWriteInvalidationIndex, StringComparison.Ordinal);
            Assert.Greater(writeStreamIndex, preWriteInvalidationIndex, methodBody);
            int streamFlushIndex = methodBody.IndexOf("stream.Flush(true);", writeStreamIndex, StringComparison.Ordinal);
            Assert.Greater(streamFlushIndex, writeStreamIndex, methodBody);
            int postWriteInvalidationIndex = methodBody.IndexOf("AsyncWriteManager.InvalidateCachedReadWindows(absoluteTempPath);", streamFlushIndex, StringComparison.Ordinal);
            Assert.Greater(postWriteInvalidationIndex, streamFlushIndex, methodBody);
            int tempFlushIndex = methodBody.IndexOf("AsyncWriteManager.FlushCriticalSavePath(absoluteTempPath, tempBytes, out _)", postWriteInvalidationIndex, StringComparison.Ordinal);
            Assert.Greater(tempFlushIndex, postWriteInvalidationIndex, methodBody);
            int cleanupAfterTempFailureIndex = methodBody.IndexOf("TryDeleteTempAfterIoFailureCold(absoluteTempPath, ref result);", tempFlushIndex, StringComparison.Ordinal);
            Assert.Greater(cleanupAfterTempFailureIndex, tempFlushIndex, methodBody);
            int prePromoteTempInvalidationIndex = methodBody.IndexOf("AsyncWriteManager.InvalidateCachedReadWindows(absoluteTempPath);", cleanupAfterTempFailureIndex, StringComparison.Ordinal);
            Assert.Greater(prePromoteTempInvalidationIndex, cleanupAfterTempFailureIndex, methodBody);
            int prePromoteFinalInvalidationIndex = methodBody.IndexOf("AsyncWriteManager.InvalidateCachedReadWindows(absolutePath);", prePromoteTempInvalidationIndex, StringComparison.Ordinal);
            Assert.Greater(prePromoteFinalInvalidationIndex, prePromoteTempInvalidationIndex, methodBody);
            int replaceIndex = methodBody.IndexOf("File.Replace(absoluteTempPath, absolutePath, null, true);", prePromoteFinalInvalidationIndex, StringComparison.Ordinal);
            Assert.Greater(replaceIndex, prePromoteFinalInvalidationIndex, methodBody);
            int moveIndex = methodBody.IndexOf("File.Move(absoluteTempPath, absolutePath);", replaceIndex, StringComparison.Ordinal);
            Assert.Greater(moveIndex, replaceIndex, methodBody);
            int postPromoteTempInvalidationIndex = methodBody.IndexOf("AsyncWriteManager.InvalidateCachedReadWindows(absoluteTempPath);", moveIndex, StringComparison.Ordinal);
            Assert.Greater(postPromoteTempInvalidationIndex, moveIndex, methodBody);
            int postPromoteFinalInvalidationIndex = methodBody.IndexOf("AsyncWriteManager.InvalidateCachedReadWindows(absolutePath);", postPromoteTempInvalidationIndex, StringComparison.Ordinal);
            Assert.Greater(postPromoteFinalInvalidationIndex, postPromoteTempInvalidationIndex, methodBody);
            int finalFlushIndex = methodBody.IndexOf("AsyncWriteManager.FlushCriticalSavePath(absolutePath, promotedBytes, out _)", postPromoteFinalInvalidationIndex, StringComparison.Ordinal);
            Assert.Greater(finalFlushIndex, postPromoteFinalInvalidationIndex, methodBody);
            int firstDeleteTempIndex = methodBody.IndexOf("TryDeleteTempAfterIoFailureCold", StringComparison.Ordinal);
            Assert.Greater(firstDeleteTempIndex, writeStreamIndex, methodBody);
            Assert.IsFalse(methodBody.Contains("File.Replace(tempPath, path", StringComparison.Ordinal));
            Assert.IsFalse(methodBody.Contains("AsyncWriteManager.TryGetFileLength(path, out long promotedBytes", StringComparison.Ordinal));

            string helperBody = source.Substring(helperIndex);
            int cleanupInvalidationIndex = helperBody.IndexOf("AsyncWriteManager.InvalidateCachedReadWindows(tempPath);", StringComparison.Ordinal);
            Assert.GreaterOrEqual(cleanupInvalidationIndex, 0, helperBody);
            int cleanupDeleteIndex = helperBody.IndexOf("File.Delete(tempPath);", cleanupInvalidationIndex, StringComparison.Ordinal);
            Assert.Greater(cleanupDeleteIndex, cleanupInvalidationIndex, helperBody);
            int cleanupFinallyIndex = helperBody.IndexOf("finally", cleanupDeleteIndex, StringComparison.Ordinal);
            Assert.Greater(cleanupFinallyIndex, cleanupDeleteIndex, helperBody);
            int cleanupPostInvalidationIndex = helperBody.IndexOf("AsyncWriteManager.InvalidateCachedReadWindows(tempPath);", cleanupFinallyIndex, StringComparison.Ordinal);
            Assert.Greater(cleanupPostInvalidationIndex, cleanupFinallyIndex, helperBody);
        }

        [Test]
        public void ControlRemapperSecurityExceptionsFailClosedAcrossSaveAndApply()
        {
            string sourcePath = Path.Combine("Assets", "_Project", "Scripts", "Input", "ControlRemapper.cs");
            string source = File.ReadAllText(sourcePath);

            int saveIndex = source.IndexOf("public static bool TrySaveOverrides(", StringComparison.Ordinal);
            Assert.GreaterOrEqual(saveIndex, 0, source);
            int loadIndex = source.IndexOf("public static bool TryLoadOverrides(", saveIndex, StringComparison.Ordinal);
            Assert.Greater(loadIndex, saveIndex, source);
            string saveBody = source.Substring(saveIndex, loadIndex - saveIndex);
            StringAssert.Contains("catch (System.Security.SecurityException)", saveBody);
            StringAssert.Contains("MarkIoFailure(ref result, InputBindingTelemetryOperation.Save, startTicks);", saveBody);

            int clearIndex = source.IndexOf("private static bool TryClearBindingOverrides(INativeInputManagerRuntime inputManager, ref ControlRemapIoResult result)", StringComparison.Ordinal);
            Assert.GreaterOrEqual(clearIndex, 0, source);
            int leaseIndex = source.IndexOf("private static bool TryAcquireLoadRollbackLease(", clearIndex, StringComparison.Ordinal);
            Assert.Greater(leaseIndex, clearIndex, source);
            string clearBody = source.Substring(clearIndex, leaseIndex - clearIndex);
            StringAssert.Contains("catch (System.Security.SecurityException)", clearBody);
            StringAssert.Contains("result.FaultFlags |= InputBindingFaultFlags.UnsupportedPath;", clearBody);

            int applyIndex = source.IndexOf("private static bool TryApplyOverridePath(InputAction action, int bindingIndex, string path, ref ControlRemapIoResult result)", StringComparison.Ordinal);
            Assert.GreaterOrEqual(applyIndex, 0, source);
            int canApplyIndex = source.IndexOf("private static bool CanApplyRecord(", applyIndex, StringComparison.Ordinal);
            Assert.Greater(canApplyIndex, applyIndex, source);
            string applyBody = source.Substring(applyIndex, canApplyIndex - applyIndex);
            StringAssert.Contains("catch (System.Security.SecurityException)", applyBody);
            StringAssert.Contains("result.FaultFlags |= InputBindingFaultFlags.UnsupportedPath;", applyBody);
        }

        [Test]
        public void ControlRemapperTelemetryRecordPublishesCursorAfterEntry()
        {
            string sourcePath = Path.Combine("Assets", "_Project", "Scripts", "Input", "ControlRemapper.cs");
            string source = File.ReadAllText(sourcePath);

            int methodIndex = source.IndexOf("public static void RecordTelemetry(", StringComparison.Ordinal);
            Assert.GreaterOrEqual(methodIndex, 0, source);
            int nextMethodIndex = source.IndexOf("private static ulong MutationGuardBit", methodIndex, StringComparison.Ordinal);
            Assert.Greater(nextMethodIndex, methodIndex, source);

            string methodBody = source.Substring(methodIndex, nextMethodIndex - methodIndex);
            StringAssert.Contains("!vault.TryAcquireMutationGuard(TelemetryMutationGuardMask)", methodBody);
            StringAssert.Contains("vault.TryResolveHandle(in cursorHandle, out NativeArray<int> cursor)", methodBody);
            StringAssert.Contains("vault.TryResolveHandle(in ringHandle, out NativeArray<InputBindingTelemetryEntry> ring)", methodBody);
            StringAssert.Contains("ring[index] = entry;", methodBody);
            StringAssert.Contains("cursor[0] = nextIndex;", methodBody);
            StringAssert.Contains("vault.ReleaseMutationGuard(TelemetryMutationGuardMask);", methodBody);

            int writeEntryIndex = methodBody.IndexOf("ring[index] = entry;", StringComparison.Ordinal);
            int publishCursorIndex = methodBody.IndexOf("cursor[0] = nextIndex;", writeEntryIndex, StringComparison.Ordinal);
            int releaseIndex = methodBody.IndexOf("vault.ReleaseMutationGuard(TelemetryMutationGuardMask);", publishCursorIndex, StringComparison.Ordinal);
            Assert.GreaterOrEqual(writeEntryIndex, 0, methodBody);
            Assert.Greater(publishCursorIndex, writeEntryIndex, methodBody);
            Assert.Greater(releaseIndex, publishCursorIndex, methodBody);
        }

        [Test]
        public void ControlRemapperTelemetryBootstrapFailsClosedOnPartialHandles()
        {
            string sourcePath = Path.Combine("Assets", "_Project", "Scripts", "Input", "ControlRemapper.cs");
            string source = File.ReadAllText(sourcePath);

            int methodIndex = source.IndexOf("public static bool TryBootstrapTelemetry(", StringComparison.Ordinal);
            Assert.GreaterOrEqual(methodIndex, 0, source);
            int nextMethodIndex = source.IndexOf("public static void RecordTelemetry(", methodIndex, StringComparison.Ordinal);
            Assert.Greater(nextMethodIndex, methodIndex, source);

            string methodBody = source.Substring(methodIndex, nextMethodIndex - methodIndex);
            StringAssert.Contains("bool hadRing = vault.TryGetGenerationHandle<InputBindingTelemetryEntry>", methodBody);
            StringAssert.Contains("bool hadCursor = vault.TryGetGenerationHandle<int>", methodBody);
            StringAssert.Contains("ringHandle = vault.EnsureGenerationHandle<InputBindingTelemetryEntry>", methodBody);
            StringAssert.Contains("cursorHandle = vault.EnsureGenerationHandle<int>", methodBody);
            StringAssert.Contains("if (ringHandle.BufferID != 0u && cursorHandle.BufferID != 0u)", methodBody);
            StringAssert.Contains("if (!hadRing && ringHandle.BufferID != 0u)", methodBody);
            StringAssert.Contains("vault.ReleaseBuffer(in ringHandle);", methodBody);
            StringAssert.Contains("if (!hadCursor && cursorHandle.BufferID != 0u)", methodBody);
            StringAssert.Contains("vault.ReleaseBuffer(in cursorHandle);", methodBody);
            StringAssert.Contains("ringHandle = default;", methodBody);
            StringAssert.Contains("cursorHandle = default;", methodBody);
            StringAssert.Contains("return false;", methodBody);

            int hadRingIndex = methodBody.IndexOf("bool hadRing = vault.TryGetGenerationHandle<InputBindingTelemetryEntry>", StringComparison.Ordinal);
            int ensureRingIndex = methodBody.IndexOf("ringHandle = vault.EnsureGenerationHandle<InputBindingTelemetryEntry>", hadRingIndex, StringComparison.Ordinal);
            int successIndex = methodBody.IndexOf("if (ringHandle.BufferID != 0u && cursorHandle.BufferID != 0u)", ensureRingIndex, StringComparison.Ordinal);
            int releaseRingIndex = methodBody.IndexOf("vault.ReleaseBuffer(in ringHandle);", successIndex, StringComparison.Ordinal);
            int defaultRingIndex = methodBody.IndexOf("ringHandle = default;", releaseRingIndex, StringComparison.Ordinal);
            Assert.Greater(ensureRingIndex, hadRingIndex, methodBody);
            Assert.Greater(successIndex, ensureRingIndex, methodBody);
            Assert.Greater(releaseRingIndex, successIndex, methodBody);
            Assert.Greater(defaultRingIndex, releaseRingIndex, methodBody);
        }

        [Test]
        public void ControlRemapperTelemetryDumpWritesTempFlushesAndPromotesAtomically()
        {
            string sourcePath = Path.Combine("Assets", "_Project", "Scripts", "Input", "ControlRemapper.cs");
            string source = File.ReadAllText(sourcePath);
            string rebindSource = File.ReadAllText(Path.Combine("Assets", "_Project", "Scripts", "Core", "RebindingManager.cs"));

            int methodIndex = source.IndexOf("public static bool TryDumpTelemetry(", StringComparison.Ordinal);
            Assert.GreaterOrEqual(methodIndex, 0, source);
            int helperIndex = source.IndexOf("private static void TryDeleteTelemetryDumpTempFile(string tempPath)", methodIndex, StringComparison.Ordinal);
            Assert.Greater(helperIndex, methodIndex, source);
            int nextMethodIndex = source.IndexOf("public static bool TrySaveOverrides(", helperIndex, StringComparison.Ordinal);
            Assert.Greater(nextMethodIndex, helperIndex, source);

            string methodBody = source.Substring(methodIndex, helperIndex - methodIndex);
            string helperBody = source.Substring(helperIndex, nextMethodIndex - helperIndex);

            StringAssert.Contains("in VaultGenerationHandle<int> cursorHandle", methodBody);
            StringAssert.Contains("cursorHandle.BufferID == 0u", methodBody);
            StringAssert.Contains("cursorHandle.BufferID != unchecked((uint)(int)InputBindingContractLayout.InputBindingTelemetryCursorBufferId)", methodBody);
            StringAssert.Contains("mutationGuardHeld = vault.TryAcquireMutationGuard(TelemetryMutationGuardMask);", methodBody);
            StringAssert.Contains("vault.TryResolveHandle(in cursorHandle, out NativeArray<int> cursor)", methodBody);
            StringAssert.Contains("vault.TryResolveHandle(in ringHandle, out NativeArray<InputBindingTelemetryEntry> ring)", methodBody);
            StringAssert.Contains("int cursorIndex = NormalizeTelemetryCursor(cursor[0], telemetryCapacity);", methodBody);
            StringAssert.Contains("for (int i = 0; i < telemetryCapacity; i++)", methodBody);
            StringAssert.Contains("int ringIndex = cursorIndex + i;", methodBody);
            StringAssert.Contains("if (ringIndex >= telemetryCapacity)", methodBody);
            StringAssert.Contains("UnsafeUtility.MemCpy(destination + (i * stride), source + (ringIndex * stride), stride);", methodBody);
            StringAssert.Contains("vault.ReleaseMutationGuard(TelemetryMutationGuardMask);", methodBody);
            StringAssert.Contains("string absolutePath = Path.GetFullPath(path);", methodBody);
            StringAssert.Contains("tempPath = absolutePath + \".tmp\";", methodBody);
            StringAssert.Contains("Path.GetDirectoryName(absolutePath)", methodBody);
            StringAssert.Contains("TryDeleteTelemetryDumpTempFile(tempPath);", methodBody);
            StringAssert.Contains("AsyncWriteManager.InvalidateCachedReadWindows(tempPath);", methodBody);
            StringAssert.Contains("new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.Read, FileStreamBufferBytes, FileOptions.WriteThrough)", methodBody);
            StringAssert.Contains("stream.Flush(true);", methodBody);
            StringAssert.Contains("stream.Length != byteCount", methodBody);
            StringAssert.Contains("AsyncWriteManager.TryGetFileLength(tempPath, out long tempBytes, out _)", methodBody);
            StringAssert.Contains("tempBytes != byteCount", methodBody);
            StringAssert.Contains("AsyncWriteManager.FlushCriticalSavePath(tempPath, tempBytes, out _)", methodBody);
            StringAssert.Contains("AsyncWriteManager.InvalidateCachedReadWindows(absolutePath);", methodBody);
            StringAssert.Contains("File.Replace(tempPath, absolutePath, null, true);", methodBody);
            StringAssert.Contains("File.Move(tempPath, absolutePath);", methodBody);
            StringAssert.Contains("AsyncWriteManager.TryGetFileLength(absolutePath, out long promotedBytes, out _)", methodBody);
            StringAssert.Contains("promotedBytes == byteCount", methodBody);
            StringAssert.Contains("AsyncWriteManager.FlushCriticalSavePath(absolutePath, promotedBytes, out _)", methodBody);
            Assert.IsFalse(methodBody.Contains("new FileStream(path, FileMode.Create", StringComparison.Ordinal));
            StringAssert.Contains("catch (System.Security.SecurityException)", methodBody);

            int absolutePathIndex = methodBody.IndexOf("string absolutePath = Path.GetFullPath(path);", StringComparison.Ordinal);
            int tempPathIndex = methodBody.IndexOf("tempPath = absolutePath + \".tmp\";", StringComparison.Ordinal);
            int deleteIndex = methodBody.IndexOf("TryDeleteTelemetryDumpTempFile(tempPath);", tempPathIndex, StringComparison.Ordinal);
            int writeIndex = methodBody.IndexOf("new FileStream(tempPath, FileMode.Create", deleteIndex, StringComparison.Ordinal);
            int streamFlushIndex = methodBody.IndexOf("stream.Flush(true);", writeIndex, StringComparison.Ordinal);
            int tempLengthIndex = methodBody.IndexOf("AsyncWriteManager.TryGetFileLength(tempPath, out long tempBytes, out _)", streamFlushIndex, StringComparison.Ordinal);
            int tempFlushIndex = methodBody.IndexOf("AsyncWriteManager.FlushCriticalSavePath(tempPath, tempBytes, out _)", tempLengthIndex, StringComparison.Ordinal);
            int replaceIndex = methodBody.IndexOf("File.Replace(tempPath, absolutePath, null, true);", tempFlushIndex, StringComparison.Ordinal);
            int promotedLengthIndex = methodBody.IndexOf("AsyncWriteManager.TryGetFileLength(absolutePath, out long promotedBytes, out _)", replaceIndex, StringComparison.Ordinal);
            int promotedFlushIndex = methodBody.IndexOf("AsyncWriteManager.FlushCriticalSavePath(absolutePath, promotedBytes, out _)", promotedLengthIndex, StringComparison.Ordinal);
            Assert.GreaterOrEqual(absolutePathIndex, 0, methodBody);
            Assert.Greater(tempPathIndex, absolutePathIndex, methodBody);
            Assert.Greater(deleteIndex, tempPathIndex, methodBody);
            Assert.Greater(writeIndex, deleteIndex, methodBody);
            Assert.Greater(streamFlushIndex, writeIndex, methodBody);
            Assert.Greater(tempLengthIndex, streamFlushIndex, methodBody);
            Assert.Greater(tempFlushIndex, tempLengthIndex, methodBody);
            Assert.Greater(replaceIndex, tempFlushIndex, methodBody);
            Assert.Greater(promotedLengthIndex, replaceIndex, methodBody);
            Assert.Greater(promotedFlushIndex, promotedLengthIndex, methodBody);

            StringAssert.Contains("AsyncWriteManager.InvalidateCachedReadWindows(tempPath);", helperBody);
            StringAssert.Contains("File.Delete(tempPath);", helperBody);
            StringAssert.Contains("catch (System.Security.SecurityException)", helperBody);
            StringAssert.Contains("finally", helperBody);

            StringAssert.Contains("private const string TelemetryDumpFileName = \"Diagnostics/InputBindingTelemetry.bin\";", rebindSource);
            StringAssert.Contains("string path = HectonPersistentPathPolicy.CombineFile(TelemetryDumpFileName);", rebindSource);
            StringAssert.Contains("ControlRemapper.TryDumpTelemetry(_dataVault, in _telemetryRingHandle, in _telemetryCursorHandle, path);", rebindSource);
            StringAssert.Contains("_telemetryRingHandle.BufferID == 0u", rebindSource);
            StringAssert.Contains("_telemetryCursorHandle.BufferID == 0u", rebindSource);
            StringAssert.DoesNotContain("AgentTelemetryDumpRelativePath", rebindSource);
            StringAssert.DoesNotContain("Docs/AgentLogs", rebindSource);
            StringAssert.DoesNotContain("Dump_1332", rebindSource);
            StringAssert.DoesNotContain("Directory.GetCurrentDirectory()", rebindSource);
        }

        [Test]
        public void UserOptionsConsumersIgnoreStaleRuntimeOwners()
        {
            string settings = File.ReadAllText(Path.Combine("Assets", "_Project", "Scripts", "UI", "SettingsManager.cs"));
            string localization = File.ReadAllText(Path.Combine("Assets", "_Project", "Scripts", "LocalizationManager.cs"));
            string modSettings = File.ReadAllText(Path.Combine("Assets", "_Project", "Scripts", "ModdingAPI", "ModSettingsRegistry.cs"));
            string pauseMenu = File.ReadAllText(Path.Combine("Assets", "_Project", "Scripts", "UI", "PauseMenuController.cs"));
            settings = settings.Replace("\r\n", "\n");
            localization = localization.Replace("\r\n", "\n");
            modSettings = modSettings.Replace("\r\n", "\n");

            StringAssert.Contains("IsUserOptionsPersistenceUsable", settings);
            StringAssert.Contains("persistence.IsServiceReady", settings);
            StringAssert.Contains("persistence.isActiveAndEnabled", settings);
            StringAssert.Contains("if (IsUserOptionsPersistenceUsable(_persistence))", settings);
            StringAssert.Contains("private bool TrySavePersistenceNow()", settings);
            StringAssert.Contains("if (_persistence.TrySave())", settings);
            StringAssert.Contains("H8Debug.LogWarning(\"[SettingsManager] Failed to persist options.h8cfg.\");", settings);
            StringAssert.Contains("if (!TryRefreshPersistenceReference(out _) || !TrySavePersistenceNow())", settings);
            StringAssert.Contains("private bool _persistenceNeedsFullStage;", settings);
            StringAssert.Contains("MarkPersistenceDirtyForMissingOwner();", settings);
            StringAssert.Contains("private void MarkPersistenceDirtyForMissingOwner()", settings);
            StringAssert.Contains("_persistenceNeedsFullStage = true;", settings);
            StringAssert.Contains("RefreshSettingsAfterPersistenceOwnerChanged();", settings);
            StringAssert.Contains("private void RefreshSettingsAfterPersistenceOwnerChanged()", settings);
            StringAssert.Contains("if (_persistenceDirty)\n                    _persistenceNeedsFullStage = true;", settings);
            StringAssert.Contains("if (_persistenceDirty)", settings);
            StringAssert.Contains("FlushPendingPersistenceSave();", settings);
            StringAssert.Contains("if (_persistenceDirty)\n                    return;", settings);
            StringAssert.Contains("private void StageCachedSettingsForPersistence()", settings);
            StringAssert.Contains("if (_persistenceNeedsFullStage)", settings);
            StringAssert.Contains("StageCachedSettingsForPersistence();", settings);
            StringAssert.Contains("_persistenceNeedsFullStage = false;", settings);
            StringAssert.Contains("_persistence.SetInt(QualityLevelKey, _cachedQualityLevel);", settings);
            StringAssert.Contains("_persistence.SetInt(QualityScaleVersionKey, CurrentQualityScaleVersion);", settings);
            StringAssert.Contains("_persistence.SetFloat(MasterVolumeKey, _cachedMasterVolume);", settings);
            StringAssert.Contains("_persistence.SetBool(FullscreenKey, _cachedFullscreen);", settings);
            StringAssert.Contains("_persistence.SetFloat(VrHeadRelativeSwimBiasKey, _cachedVrHeadRelativeSwimBias);", settings);
            StringAssert.DoesNotContain("_persistence.Save();", settings);
            StringAssert.DoesNotContain("return TryAssignPersistence(GlobalRegistry.UserOptions, out changed);", settings);
            int settingsSaveIntIndex = settings.IndexOf("private void SaveInt(string key, int value)", StringComparison.Ordinal);
            int tryAssignPersistenceIndex = settings.IndexOf("private bool TryAssignPersistence(UserOptionsPersistence persistence, out bool changed)", StringComparison.Ordinal);
            int settingsSaveFloatIndex = settings.IndexOf("private void SaveFloat(string key, float value)", settingsSaveIntIndex, StringComparison.Ordinal);
            int settingsSaveBoolIndex = settings.IndexOf("private void SaveBool(string key, bool value)", settingsSaveFloatIndex, StringComparison.Ordinal);
            int missingOwnerIndex = settings.IndexOf("private void MarkPersistenceDirtyForMissingOwner()", settingsSaveBoolIndex, StringComparison.Ordinal);
            int markDirtyIndex = settings.IndexOf("private void MarkPersistenceDirty()", missingOwnerIndex, StringComparison.Ordinal);
            int flushDirtyIndex = settings.IndexOf("private void FlushPendingPersistenceSave()", markDirtyIndex, StringComparison.Ordinal);
            int trySaveNowIndex = settings.IndexOf("private bool TrySavePersistenceNow()", flushDirtyIndex, StringComparison.Ordinal);
            int ownerChangedIndex = settings.IndexOf("private void RefreshSettingsAfterPersistenceOwnerChanged()", trySaveNowIndex, StringComparison.Ordinal);
            int stageCachedIndex = settings.IndexOf("private void StageCachedSettingsForPersistence()", ownerChangedIndex, StringComparison.Ordinal);
            Assert.GreaterOrEqual(settingsSaveIntIndex, 0, settings);
            Assert.GreaterOrEqual(tryAssignPersistenceIndex, 0, settings);
            Assert.Greater(settingsSaveFloatIndex, settingsSaveIntIndex, settings);
            Assert.Greater(settingsSaveBoolIndex, settingsSaveFloatIndex, settings);
            Assert.Greater(missingOwnerIndex, settingsSaveBoolIndex, settings);
            Assert.Greater(markDirtyIndex, missingOwnerIndex, settings);
            Assert.Greater(flushDirtyIndex, markDirtyIndex, settings);
            Assert.Greater(trySaveNowIndex, flushDirtyIndex, settings);
            Assert.Greater(ownerChangedIndex, trySaveNowIndex, settings);
            Assert.Greater(stageCachedIndex, ownerChangedIndex, settings);
            string saveIntBody = settings.Substring(settingsSaveIntIndex, settingsSaveFloatIndex - settingsSaveIntIndex);
            StringAssert.Contains("MarkPersistenceDirtyForMissingOwner();", saveIntBody);
            StringAssert.Contains("_persistence.SetInt(key, value);", saveIntBody);
            string saveFloatBody = settings.Substring(settingsSaveFloatIndex, settingsSaveBoolIndex - settingsSaveFloatIndex);
            StringAssert.Contains("MarkPersistenceDirtyForMissingOwner();", saveFloatBody);
            StringAssert.Contains("_persistence.SetFloat(key, value);", saveFloatBody);
            string saveBoolBody = settings.Substring(settingsSaveBoolIndex, missingOwnerIndex - settingsSaveBoolIndex);
            StringAssert.Contains("MarkPersistenceDirtyForMissingOwner();", saveBoolBody);
            StringAssert.Contains("_persistence.SetBool(key, value);", saveBoolBody);
            string tryAssignBody = settings.Substring(tryAssignPersistenceIndex, settings.IndexOf("[System.Diagnostics.Conditional(\"UNITY_EDITOR\")", tryAssignPersistenceIndex, StringComparison.Ordinal) - tryAssignPersistenceIndex);
            int assignChangedIndex = tryAssignBody.IndexOf("changed = !ReferenceEquals(_persistence, persistence);", StringComparison.Ordinal);
            int changedGateIndex = tryAssignBody.IndexOf("if (changed)", assignChangedIndex, StringComparison.Ordinal);
            int assignPersistenceIndex = tryAssignBody.IndexOf("_persistence = persistence;", changedGateIndex, StringComparison.Ordinal);
            int dirtyGateIndex = tryAssignBody.IndexOf("if (_persistenceDirty)", assignPersistenceIndex, StringComparison.Ordinal);
            int fullStageIndex = tryAssignBody.IndexOf("_persistenceNeedsFullStage = true;", dirtyGateIndex, StringComparison.Ordinal);
            Assert.GreaterOrEqual(assignChangedIndex, 0, tryAssignBody);
            Assert.Greater(changedGateIndex, assignChangedIndex, tryAssignBody);
            Assert.Greater(assignPersistenceIndex, changedGateIndex, tryAssignBody);
            Assert.Greater(dirtyGateIndex, assignPersistenceIndex, tryAssignBody);
            Assert.Greater(fullStageIndex, dirtyGateIndex, tryAssignBody);
            string ownerChangedBody = settings.Substring(ownerChangedIndex, stageCachedIndex - ownerChangedIndex);
            int ownerDirtyIndex = ownerChangedBody.IndexOf("if (_persistenceDirty)", StringComparison.Ordinal);
            int ownerFlushIndex = ownerChangedBody.IndexOf("FlushPendingPersistenceSave();", ownerDirtyIndex, StringComparison.Ordinal);
            int ownerReturnIfStillDirtyIndex = ownerChangedBody.IndexOf("if (_persistenceDirty)\n                    return;", ownerFlushIndex, StringComparison.Ordinal);
            int ownerApplyAfterFlushIndex = ownerChangedBody.IndexOf("ApplyAllSettings();", ownerReturnIfStillDirtyIndex, StringComparison.Ordinal);
            int ownerLoadCleanIndex = ownerChangedBody.IndexOf("LoadAllSettings();", ownerApplyAfterFlushIndex, StringComparison.Ordinal);
            Assert.GreaterOrEqual(ownerDirtyIndex, 0, ownerChangedBody);
            Assert.Greater(ownerFlushIndex, ownerDirtyIndex, ownerChangedBody);
            Assert.Greater(ownerReturnIfStillDirtyIndex, ownerFlushIndex, ownerChangedBody);
            Assert.Greater(ownerApplyAfterFlushIndex, ownerReturnIfStillDirtyIndex, ownerChangedBody);
            Assert.Greater(ownerLoadCleanIndex, ownerApplyAfterFlushIndex, ownerChangedBody);

            StringAssert.Contains("CacheUserOptions(GlobalRegistry.UserOptions, applyOwnerChange: false);", localization);
            StringAssert.Contains("ResolveUserOptionsPersistence", localization);
            StringAssert.Contains("IsUserOptionsRuntimeUsable", localization);
            StringAssert.Contains("options.IsServiceReady", localization);
            StringAssert.Contains("options.isActiveAndEnabled", localization);
            StringAssert.Contains("private bool _languagePersistenceDirty;", localization);
            StringAssert.Contains("if (options.TrySave())", localization);
            StringAssert.Contains("_languagePersistenceDirty = false;", localization);
            StringAssert.Contains("_languagePersistenceDirty = true;", localization);
            StringAssert.Contains("Hecton8.Core.H8Debug.LogWarning(\"[Localization] Failed to persist language preference.\");", localization);
            StringAssert.Contains("private void CacheUserOptions(UserOptionsPersistence options, bool applyOwnerChange)", localization);
            StringAssert.Contains("if (applyOwnerChange && _cachedUserOptions != null)", localization);
            StringAssert.Contains("RefreshLanguageAfterUserOptionsOwnerChanged();", localization);
            StringAssert.Contains("private void RefreshLanguageAfterUserOptionsOwnerChanged()", localization);
            StringAssert.Contains("SavePersistentLanguagePreference(_savedLanguage);", localization);
            StringAssert.Contains("options.HasKey(PrefsLanguageKey)", localization);
            StringAssert.Contains("GameLanguage loadedLanguage = (GameLanguage)saved;", localization);
            StringAssert.Contains("_transientLanguageOverrideActive || _currentLanguage == loadedLanguage", localization);
            StringAssert.Contains("PublishVisualLanguageState();", localization);
            StringAssert.Contains("IsLocalizationRuntimeUsable", localization);
            StringAssert.Contains("localization._registeredLocalizationRuntime", localization);
            StringAssert.Contains("localization.isActiveAndEnabled", localization);
            StringAssert.Contains("GlobalRegistry.UnregisterLocalizationRuntime(registered);", localization);
            StringAssert.Contains("GlobalRegistry.UnregisterBabelLocalizationRuntime(registered);", localization);
            StringAssert.Contains("ReferenceEquals(ActiveRuntimeInstance, registered)", localization);
            StringAssert.DoesNotContain("registered != null && registered != this", localization);
            int localizationAwakeIndex = localization.IndexOf("private void Awake()", StringComparison.Ordinal);
            int localizationGateIndex = localization.IndexOf("if (TryAbortForUsableExistingRuntime())", localizationAwakeIndex, StringComparison.Ordinal);
            int localizationRegisterIndex = localization.IndexOf("GlobalRegistry.RegisterLocalizationRuntime(this);", localizationAwakeIndex, StringComparison.Ordinal);
            Assert.GreaterOrEqual(localizationGateIndex, localizationAwakeIndex);
            Assert.Less(localizationGateIndex, localizationRegisterIndex);
            StringAssert.DoesNotContain("_cachedUserOptions = GlobalRegistry.UserOptions;", localization);
            StringAssert.DoesNotContain("_cachedUserOptions = currentService as UserOptionsPersistence;", localization);
            int localizationSavePreferenceIndex = localization.IndexOf("private void SavePersistentLanguagePreference(GameLanguage language)", StringComparison.Ordinal);
            int localizationResolveOptionsIndex = localization.IndexOf("private UserOptionsPersistence ResolveUserOptionsPersistence()", localizationSavePreferenceIndex, StringComparison.Ordinal);
            int localizationCacheOptionsIndex = localization.IndexOf("private void CacheUserOptions(UserOptionsPersistence options)", localizationResolveOptionsIndex, StringComparison.Ordinal);
            int localizationCacheOptionsApplyIndex = localization.IndexOf("private void CacheUserOptions(UserOptionsPersistence options, bool applyOwnerChange)", localizationCacheOptionsIndex, StringComparison.Ordinal);
            int localizationRefreshOwnerIndex = localization.IndexOf("private void RefreshLanguageAfterUserOptionsOwnerChanged()", localizationCacheOptionsApplyIndex, StringComparison.Ordinal);
            Assert.GreaterOrEqual(localizationSavePreferenceIndex, 0, localization);
            Assert.Greater(localizationResolveOptionsIndex, localizationSavePreferenceIndex, localization);
            Assert.Greater(localizationCacheOptionsIndex, localizationResolveOptionsIndex, localization);
            Assert.Greater(localizationCacheOptionsApplyIndex, localizationCacheOptionsIndex, localization);
            Assert.Greater(localizationRefreshOwnerIndex, localizationCacheOptionsApplyIndex, localization);
            string localizationSavePreferenceBody = localization.Substring(localizationSavePreferenceIndex, localizationResolveOptionsIndex - localizationSavePreferenceIndex);
            Assert.IsTrue(ContainsTokensInOrder(
                localizationSavePreferenceBody,
                "if (options == null)",
                "_languagePersistenceDirty = true;",
                "return;",
                "options.SetInt(PrefsLanguageKey, (int)language);",
                "if (options.TrySave())",
                "_languagePersistenceDirty = false;",
                "return;",
                "_languagePersistenceDirty = true;"));
            string localizationResolveBody = localization.Substring(localizationResolveOptionsIndex, localizationCacheOptionsIndex - localizationResolveOptionsIndex);
            StringAssert.Contains("CacheUserOptions(GlobalRegistry.UserOptions, applyOwnerChange: false);", localizationResolveBody);
            string localizationCacheApplyBody = localization.Substring(localizationCacheOptionsApplyIndex, localizationRefreshOwnerIndex - localizationCacheOptionsApplyIndex);
            Assert.IsTrue(ContainsTokensInOrder(
                localizationCacheApplyBody,
                "_cachedUserOptions = IsUserOptionsRuntimeUsable(options) ? options : null;",
                "if (applyOwnerChange && _cachedUserOptions != null)",
                "RefreshLanguageAfterUserOptionsOwnerChanged();"));
            string localizationRefreshOwnerBody = localization.Substring(localizationRefreshOwnerIndex, localization.IndexOf("private static bool IsUserOptionsRuntimeUsable", localizationRefreshOwnerIndex, StringComparison.Ordinal) - localizationRefreshOwnerIndex);
            Assert.IsTrue(ContainsTokensInOrder(
                localizationRefreshOwnerBody,
                "if (_languagePersistenceDirty)",
                "SavePersistentLanguagePreference(_savedLanguage);",
                "return;",
                "if (!IsUserOptionsRuntimeUsable(options) || !options.HasKey(PrefsLanguageKey))",
                "return;",
                "int saved = options.GetInt(PrefsLanguageKey, (int)defaultLanguage);",
                "if (!Enum.IsDefined(typeof(GameLanguage), saved))",
                "return;",
                "GameLanguage loadedLanguage = (GameLanguage)saved;",
                "_savedLanguage = loadedLanguage;",
                "if (_transientLanguageOverrideActive || _currentLanguage == loadedLanguage)",
                "return;",
                "_currentLanguage = loadedLanguage;",
                "PublishVisualLanguageState();"));

            StringAssert.Contains("CacheUserOptions(GlobalRegistry.UserOptions);", modSettings);
            StringAssert.Contains("ResolveUserOptions()", modSettings);
            StringAssert.Contains("IsUserOptionsRuntimeUsable", modSettings);
            StringAssert.Contains("options.IsServiceReady", modSettings);
            StringAssert.Contains("options.isActiveAndEnabled", modSettings);
            StringAssert.Contains("TrySaveUserOptions(options, entry.StorageKey);", modSettings);
            StringAssert.Contains("private static bool s_pendingFullStage;", modSettings);
            StringAssert.Contains("s_pendingFullStage = false;", modSettings);
            StringAssert.Contains("s_pendingFullStage = true;", modSettings);
            StringAssert.Contains("if (s_userOptions == null)", modSettings);
            StringAssert.Contains("TrySaveUserOptions(s_userOptions, \"pending\");", modSettings);
            StringAssert.Contains("HydrateEntriesFromUserOptions(s_userOptions);", modSettings);
            StringAssert.Contains("private static void HydrateEntriesFromUserOptions(UserOptionsPersistence options)", modSettings);
            StringAssert.Contains("private static bool TryHydrateEntryFromUserOptions(UserOptionsPersistence options, ref SettingEntry entry)", modSettings);
            StringAssert.Contains("while (index < _entries.Count)", modSettings);
            StringAssert.Contains("TryHydrateEntryFromUserOptions(options, ref entry)", modSettings);
            StringAssert.Contains("InvokeToggleCallback(entry.ModId, entry.ModHash, entry.BoolChanged, entry.BoolValue);", modSettings);
            StringAssert.Contains("InvokeSliderCallback(entry.ModId, entry.ModHash, entry.FloatChanged, entry.FloatValue);", modSettings);
            StringAssert.Contains("ModRegistryEvents.NotifySettingsRegistryChanged(entry.ModHash, entry.KeyHash);", modSettings);
            StringAssert.Contains("if (index < _entries.Count && _entries[index].KeyHash == entry.KeyHash)", modSettings);
            StringAssert.Contains("bool storedValue = options.GetBool(entry.StorageKey, entry.DefaultBoolValue);", modSettings);
            StringAssert.Contains("Mathf.Clamp(\n                    options.GetFloat(entry.StorageKey, entry.DefaultFloatValue),", modSettings);
            StringAssert.Contains("if (s_pendingFullStage)", modSettings);
            StringAssert.Contains("StageAllEntries(options);", modSettings);
            StringAssert.Contains("private static void StageAllEntries(UserOptionsPersistence options)", modSettings);
            StringAssert.Contains("private static void StageEntry(UserOptionsPersistence options, SettingEntry entry)", modSettings);
            StringAssert.Contains("options.SetBool(entry.StorageKey, entry.BoolValue);", modSettings);
            StringAssert.Contains("options.SetFloat(entry.StorageKey, entry.FloatValue);", modSettings);
            StringAssert.Contains("if (options.TrySave())", modSettings);
            StringAssert.Contains("s_pendingFullStage = false;", modSettings);
            StringAssert.Contains("Hecton8.Core.H8Debug.LogWarning(\"[ModSettingsRegistry] Failed to persist option '\" + storageKey + \"'.\");", modSettings);
            StringAssert.DoesNotContain("options.Save();", modSettings);
            StringAssert.DoesNotContain("s_userOptions = GlobalRegistry.UserOptions;", modSettings);
            StringAssert.DoesNotContain("s_userOptions = currentService as UserOptionsPersistence;", modSettings);
            StringAssert.DoesNotContain("UserOptionsPersistence options = s_userOptions;", modSettings);
            int modCacheIndex = modSettings.IndexOf("private static void CacheUserOptions(UserOptionsPersistence options)", StringComparison.Ordinal);
            int modHydrateIndex = modSettings.IndexOf("private static void HydrateEntriesFromUserOptions(UserOptionsPersistence options)", modCacheIndex, StringComparison.Ordinal);
            int modTryHydrateIndex = modSettings.IndexOf("private static bool TryHydrateEntryFromUserOptions(UserOptionsPersistence options, ref SettingEntry entry)", modHydrateIndex, StringComparison.Ordinal);
            int modTrySaveIndex = modSettings.IndexOf("private static bool TrySaveUserOptions(UserOptionsPersistence options, string storageKey)", modTryHydrateIndex, StringComparison.Ordinal);
            int modStageAllIndex = modSettings.IndexOf("private static void StageAllEntries(UserOptionsPersistence options)", modTrySaveIndex, StringComparison.Ordinal);
            int modStageEntryIndex = modSettings.IndexOf("private static void StageEntry(UserOptionsPersistence options, SettingEntry entry)", modStageAllIndex, StringComparison.Ordinal);
            Assert.GreaterOrEqual(modCacheIndex, 0, modSettings);
            Assert.Greater(modHydrateIndex, modCacheIndex, modSettings);
            Assert.Greater(modTryHydrateIndex, modHydrateIndex, modSettings);
            Assert.Greater(modTrySaveIndex, modTryHydrateIndex, modSettings);
            Assert.Greater(modStageAllIndex, modTrySaveIndex, modSettings);
            Assert.Greater(modStageEntryIndex, modStageAllIndex, modSettings);
            string modCacheBody = modSettings.Substring(modCacheIndex, modHydrateIndex - modCacheIndex);
            int modNullOwnerIndex = modCacheBody.IndexOf("if (s_userOptions == null)", StringComparison.Ordinal);
            int modNullReturnIndex = modCacheBody.IndexOf("return;", modNullOwnerIndex, StringComparison.Ordinal);
            int modPendingOwnerIndex = modCacheBody.IndexOf("if (s_pendingFullStage)", modNullReturnIndex, StringComparison.Ordinal);
            int modPendingSaveIndex = modCacheBody.IndexOf("TrySaveUserOptions(s_userOptions, \"pending\");", modPendingOwnerIndex, StringComparison.Ordinal);
            int modPendingReturnIndex = modCacheBody.IndexOf("return;", modPendingSaveIndex, StringComparison.Ordinal);
            int modHydrateCallIndex = modCacheBody.IndexOf("HydrateEntriesFromUserOptions(s_userOptions);", modPendingReturnIndex, StringComparison.Ordinal);
            Assert.GreaterOrEqual(modNullOwnerIndex, 0, modCacheBody);
            Assert.Greater(modNullReturnIndex, modNullOwnerIndex, modCacheBody);
            Assert.Greater(modPendingOwnerIndex, modNullReturnIndex, modCacheBody);
            Assert.Greater(modPendingSaveIndex, modPendingOwnerIndex, modCacheBody);
            Assert.Greater(modPendingReturnIndex, modPendingSaveIndex, modCacheBody);
            Assert.Greater(modHydrateCallIndex, modPendingReturnIndex, modCacheBody);
            StringAssert.Contains("TrySaveUserOptions(s_userOptions, \"pending\");", modCacheBody);
            StringAssert.Contains("HydrateEntriesFromUserOptions(s_userOptions);", modCacheBody);
            string modHydrateBody = modSettings.Substring(modHydrateIndex, modTryHydrateIndex - modHydrateIndex);
            StringAssert.Contains("while (index < _entries.Count)", modHydrateBody);
            StringAssert.Contains("TryHydrateEntryFromUserOptions(options, ref entry)", modHydrateBody);
            StringAssert.Contains("InvokeToggleCallback(entry.ModId, entry.ModHash, entry.BoolChanged, entry.BoolValue);", modHydrateBody);
            StringAssert.Contains("InvokeSliderCallback(entry.ModId, entry.ModHash, entry.FloatChanged, entry.FloatValue);", modHydrateBody);
            StringAssert.Contains("ModRegistryEvents.NotifySettingsRegistryChanged(entry.ModHash, entry.KeyHash);", modHydrateBody);
            StringAssert.Contains("if (index < _entries.Count && _entries[index].KeyHash == entry.KeyHash)", modHydrateBody);
            string modTryHydrateBody = modSettings.Substring(modTryHydrateIndex, modTrySaveIndex - modTryHydrateIndex);
            StringAssert.Contains("bool storedValue = options.GetBool(entry.StorageKey, entry.DefaultBoolValue);", modTryHydrateBody);
            StringAssert.Contains("entry.BoolValue = storedValue;", modTryHydrateBody);
            StringAssert.Contains("options.GetFloat(entry.StorageKey, entry.DefaultFloatValue)", modTryHydrateBody);
            StringAssert.Contains("entry.FloatValue = storedValue;", modTryHydrateBody);
            string modTrySaveBody = modSettings.Substring(modTrySaveIndex, modStageAllIndex - modTrySaveIndex);
            int modPendingStageIndex = modTrySaveBody.IndexOf("if (s_pendingFullStage)", StringComparison.Ordinal);
            int modStageAllCallIndex = modTrySaveBody.IndexOf("StageAllEntries(options);", modPendingStageIndex, StringComparison.Ordinal);
            int modSaveIndex = modTrySaveBody.IndexOf("if (options.TrySave())", modStageAllCallIndex, StringComparison.Ordinal);
            int modClearPendingIndex = modTrySaveBody.IndexOf("s_pendingFullStage = false;", modSaveIndex, StringComparison.Ordinal);
            int modSetPendingOnFailureIndex = modTrySaveBody.IndexOf("s_pendingFullStage = true;", modClearPendingIndex, StringComparison.Ordinal);
            Assert.GreaterOrEqual(modPendingStageIndex, 0, modTrySaveBody);
            Assert.Greater(modStageAllCallIndex, modPendingStageIndex, modTrySaveBody);
            Assert.Greater(modSaveIndex, modStageAllCallIndex, modTrySaveBody);
            Assert.Greater(modClearPendingIndex, modSaveIndex, modTrySaveBody);
            Assert.Greater(modSetPendingOnFailureIndex, modClearPendingIndex, modTrySaveBody);
            string modStageAllBody = modSettings.Substring(modStageAllIndex, modStageEntryIndex - modStageAllIndex);
            StringAssert.Contains("for (int i = 0; i < _entries.Count; i++)", modStageAllBody);
            StringAssert.Contains("StageEntry(options, _entries[i]);", modStageAllBody);

            StringAssert.Contains("Hecton8.Input.UserOptionsPersistence userOptions = Hecton8.Core.GlobalRegistry.UserOptions;", pauseMenu);
            StringAssert.Contains("userOptions.IsServiceReady", pauseMenu);
            StringAssert.Contains("userOptions.isActiveAndEnabled", pauseMenu);
            StringAssert.Contains("if (!userOptions.TrySave())", pauseMenu);
            StringAssert.Contains("Hecton8.Core.H8Debug.LogError(\"[PauseMenuController] User options save failed during quit.\");", pauseMenu);
            StringAssert.Contains("private const string DefaultMainMenuSceneName = \"01_MAIN_MENU\";", pauseMenu);
            StringAssert.Contains("string resolvedMainMenuSceneName = ResolveMainMenuSceneName(mainMenuSceneName);", pauseMenu);
            StringAssert.Contains("RegisterMainMenuCleanup(resolvedMainMenuSceneName);", pauseMenu);
            StringAssert.Contains("sceneService.LoadScene(resolvedMainMenuSceneName);", pauseMenu);
            StringAssert.Contains("_pendingMainMenuSceneName = ResolveMainMenuSceneName(sceneName);", pauseMenu);
            StringAssert.Contains("return string.IsNullOrWhiteSpace(sceneName) ? DefaultMainMenuSceneName : sceneName.Trim();", pauseMenu);
            StringAssert.DoesNotContain("Hecton8.Core.GlobalRegistry.UserOptions.Save();", pauseMenu);
            StringAssert.DoesNotContain("userOptions.Save();", pauseMenu);
            StringAssert.DoesNotContain("PlayerPrefs.Save() is called", pauseMenu);
            StringAssert.DoesNotContain("sceneService.LoadScene(mainMenuSceneName);", pauseMenu);
            StringAssert.DoesNotContain("RegisterMainMenuCleanup(mainMenuSceneName);", pauseMenu);
        }

        [Test]
        public void InputDomainDocsAndHelpersDoNotReintroduceLegacyRebindRoutes()
        {
            string inputManagerSource = File.ReadAllText(Path.Combine("Assets", "_Project", "Scripts", "Input", "InputManager.cs"));
            string inputContractsSource = File.ReadAllText(Path.Combine("Assets", "_Project", "Scripts", "Input", "InputBindingContracts.cs"));
            string inputServiceContractsSource = File.ReadAllText(Path.Combine("Assets", "_Project", "Scripts", "Core", "BootstrapContracts", "InputBindingServiceContracts.cs"));
            string rebindSource = File.ReadAllText(Path.Combine("Assets", "_Project", "Scripts", "Core", "RebindingManager.cs"));
            string controlRemapperSource = File.ReadAllText(Path.Combine("Assets", "_Project", "Scripts", "Input", "ControlRemapper.cs"));
            string pausePanelSource = File.ReadAllText(Path.Combine("Assets", "_Project", "Scripts", "UI", "PauseControlsPanel.cs"));
            string pdaControlsSource = File.ReadAllText(Path.Combine("Assets", "_Project", "Scripts", "UI", "PDAControlsRebindUI.cs"));
            string interactionUiSource = File.ReadAllText(Path.Combine("Assets", "_Project", "Scripts", "Interaction", "InteractionUI.cs"));
            string legacyInteractionUiSource = File.ReadAllText(Path.Combine("Assets", "_Project", "Scripts", "UI", "InteractionUI.cs"));
            string playerInteractionSource = File.ReadAllText(Path.Combine("Assets", "_Project", "Scripts", "Interaction", "PlayerInteraction.cs"));
            string guide = File.ReadAllText(Path.Combine("Assets", "_Project", "Scripts", "Input", "INPUT_MIGRATION_GUIDE.md"));
            string rebindNormalized = rebindSource.Replace("\r\n", "\n");
            string controlRemapperNormalized = controlRemapperSource.Replace("\r\n", "\n");
            string pausePanelNormalized = pausePanelSource.Replace("\r\n", "\n");
            string pdaControlsNormalized = pdaControlsSource.Replace("\r\n", "\n");

            Assert.IsFalse(inputManagerSource.Contains("catch\r\n            {") || inputManagerSource.Contains("catch\n            {"));
            StringAssert.DoesNotContain("+= Handle", inputManagerSource);
            StringAssert.DoesNotContain("-= Handle", inputManagerSource);
            StringAssert.Contains("Dictionary<int, InputDisplayStyle>(32)", inputManagerSource);
            StringAssert.Contains("TryGetActiveBindingPath", inputManagerSource);
            StringAssert.Contains("binding.overridePath != null", inputManagerSource);
            StringAssert.DoesNotContain("!string.IsNullOrWhiteSpace(binding.overridePath) ? binding.overridePath : binding.path", inputManagerSource);
            StringAssert.Contains("bool SaveOverrides();", inputServiceContractsSource);
            StringAssert.Contains("event Action<string, string, int> OnRebindSaveFailed;", inputServiceContractsSource);
            StringAssert.Contains("event Action OnOverridesSaveFailed;", inputServiceContractsSource);
            StringAssert.Contains("public const byte Middle = 2;", inputServiceContractsSource);
            StringAssert.Contains("public const byte Ultra = 4;", inputServiceContractsSource);
            StringAssert.Contains("if (tier == LegacyHighDesktop)", inputServiceContractsSource);
            StringAssert.Contains("ToGlobalQualityWeight01", inputServiceContractsSource);
            StringAssert.DoesNotContain("Fixed two-tier scalability profile", inputServiceContractsSource);
            StringAssert.Contains("bool LoadOverrides();", inputServiceContractsSource);
            StringAssert.Contains("bool ClearOverrides(bool clearSavedOverrides = true)", inputServiceContractsSource);
            StringAssert.DoesNotContain("clearPlayerPrefs", inputServiceContractsSource);
            StringAssert.Contains("ConcurrentOperation", inputContractsSource);
            StringAssert.Contains("ValidateInputActionStateOffsets", inputContractsSource);
            StringAssert.Contains("ValidateAccessibilityConfigOffsets", inputContractsSource);
            StringAssert.Contains("ValidateInputBindingTelemetryOffsets", inputContractsSource);
            StringAssert.Contains("ValidateControlRemapIoResultOffsets", inputContractsSource);
            StringAssert.Contains("nameof(InputActionStateDTO.CompositeDepth)", inputContractsSource);
            StringAssert.Contains("nameof(InputBindingTelemetryEntry.PathBytes)", inputContractsSource);
            StringAssert.Contains("TryDisableConflictingBinding", rebindSource);
            StringAssert.Contains("ApplyBindingOverride(bindingIndex, string.Empty)", rebindSource);
            StringAssert.Contains("_pendingConflictVictimAction", rebindSource);
            StringAssert.DoesNotContain("private string DetectConflict", rebindSource);
            StringAssert.Contains("TryDeleteOverridesFile", rebindSource);
            StringAssert.Contains("public bool SaveOverrides()", rebindSource);
            StringAssert.Contains("return SaveOverrides(emitFailureEvent: true);", rebindSource);
            StringAssert.Contains("private bool SaveOverrides(bool emitFailureEvent)", rebindSource);
            StringAssert.Contains("return FailSaveOverrides(emitFailureEvent);", rebindSource);
            StringAssert.Contains("private bool FailSaveOverrides(bool emitFailureEvent)", rebindSource);
            StringAssert.Contains("if (emitFailureEvent)\n                OnOverridesSaveFailed?.Invoke();", rebindNormalized);
            StringAssert.Contains("SaveOverrides(emitFailureEvent: false)", rebindSource);
            StringAssert.DoesNotContain("if (!SaveOverrides())\n                {\n                    TryRestoreBindingOverride(action", rebindNormalized);
            StringAssert.DoesNotContain("if (!SaveOverrides())\n                {\n                    TryRestoreBindingOverride(victimAction", rebindNormalized);
            StringAssert.Contains("public bool LoadOverrides()", rebindSource);
            StringAssert.Contains("public bool ClearOverrides(bool clearSavedOverrides = true)", rebindSource);
            StringAssert.Contains("private bool DeleteOverridesFileIfExistsCold(out InputBindingTelemetryEntry failureTelemetry)", rebindSource);
            StringAssert.Contains("private bool TryDeleteOverridesFileCold(string path, string warning, out InputBindingTelemetryEntry failureTelemetry)", rebindSource);
            StringAssert.Contains("BuildDeleteFailureTelemetry", rebindSource);
            StringAssert.Contains("ControlRemapper.BuildDeleteTelemetry", rebindSource);
            StringAssert.Contains("InputBindingTelemetryOperation.Delete", controlRemapperSource);
            StringAssert.Contains("public static InputBindingTelemetryEntry BuildDeleteTelemetry", controlRemapperSource);
            StringAssert.Contains("BuildApplyFailureTelemetry", rebindSource);
            StringAssert.Contains("ControlRemapper.BuildApplyTelemetry", rebindSource);
            StringAssert.Contains("InputBindingTelemetryOperation.Apply", controlRemapperSource);
            StringAssert.Contains("public static InputBindingTelemetryEntry BuildApplyTelemetry", controlRemapperSource);
            StringAssert.Contains("using Hecton8.SaveSystem;", rebindSource);
            StringAssert.Contains("using System.Diagnostics;", rebindSource);
            StringAssert.Contains("absolutePath = Path.GetFullPath(path);", rebindSource);
            StringAssert.Contains("AsyncWriteManager.InvalidateCachedReadWindows(absolutePath);", rebindSource);
            StringAssert.Contains("RuntimeOverrideRollbackRecord[] _clearRollbackRecords", rebindSource);
            StringAssert.Contains("TryCaptureRuntimeOverrideRollback(inputManager, out int rollbackCount, out InputBindingTelemetryEntry captureTelemetry)", rebindSource);
            StringAssert.Contains("RecordControlRemapTelemetry(in captureTelemetry);", rebindSource);
            StringAssert.Contains("DumpControlRemapTelemetryOnFault(in captureTelemetry);", rebindSource);
            StringAssert.Contains("inputManager.GetActionMap(PlayerActionMapName)", rebindSource);
            StringAssert.Contains("inputManager.GetActionMap(UiActionMapName)", rebindSource);
            StringAssert.Contains("if (!DeleteOverridesFileIfExistsCold(out InputBindingTelemetryEntry deleteTelemetry))", rebindSource);
            StringAssert.Contains("if (clearSavedOverrides && !DeleteOverridesFileIfExistsCold(out InputBindingTelemetryEntry deleteTelemetry))", rebindSource);
            StringAssert.Contains("RecordControlRemapTelemetry(in deleteTelemetry);", rebindSource);
            StringAssert.Contains("DumpControlRemapTelemetryOnFault(in deleteTelemetry);", rebindSource);
            StringAssert.Contains("TryRestoreRuntimeOverrideRollback(rollbackCount)", rebindSource);
            StringAssert.Contains("if (!TryDeleteOverridesFileCold(tempPath, \"Failed to delete temp binding overrides file.\", out failureTelemetry))\n                return false;\n\n            string path = GetOverridesFilePath();", rebindNormalized);
            int deleteOverridesIndex = rebindSource.IndexOf("private bool TryDeleteOverridesFileCold(string path, string warning, out InputBindingTelemetryEntry failureTelemetry)", StringComparison.Ordinal);
            Assert.GreaterOrEqual(deleteOverridesIndex, 0, rebindSource);
            int deleteFullPathIndex = rebindSource.IndexOf("absolutePath = Path.GetFullPath(path);", deleteOverridesIndex, StringComparison.Ordinal);
            Assert.Greater(deleteFullPathIndex, deleteOverridesIndex, rebindSource);
            int deletePathBytesIndex = rebindSource.IndexOf("pathBytes = absolutePath.Length;", deleteFullPathIndex, StringComparison.Ordinal);
            Assert.Greater(deletePathBytesIndex, deleteFullPathIndex, rebindSource);
            int deletePreInvalidationIndex = rebindSource.IndexOf("AsyncWriteManager.InvalidateCachedReadWindows(absolutePath);", deleteFullPathIndex, StringComparison.Ordinal);
            Assert.Greater(deletePreInvalidationIndex, deletePathBytesIndex, rebindSource);
            int deleteLengthIndex = rebindSource.IndexOf("AsyncWriteManager.TryGetFileLength(absolutePath, out long fileBytes, out _)", deletePreInvalidationIndex, StringComparison.Ordinal);
            Assert.Greater(deleteLengthIndex, deletePreInvalidationIndex, rebindSource);
            int deleteFileIndex = rebindSource.IndexOf("File.Delete(absolutePath);", deletePreInvalidationIndex, StringComparison.Ordinal);
            Assert.Greater(deleteFileIndex, deleteLengthIndex, rebindSource);
            int deleteFailureTelemetryIndex = rebindSource.IndexOf("failureTelemetry = BuildDeleteFailureTelemetry(byteCount, pathBytes, startTicks);", deleteFileIndex, StringComparison.Ordinal);
            Assert.Greater(deleteFailureTelemetryIndex, deleteFileIndex, rebindSource);
            int deleteFinallyIndex = rebindSource.IndexOf("finally", deleteFileIndex, StringComparison.Ordinal);
            Assert.Greater(deleteFinallyIndex, deleteFileIndex, rebindSource);
            int deletePostInvalidationIndex = rebindSource.IndexOf("AsyncWriteManager.InvalidateCachedReadWindows(absolutePath);", deleteFinallyIndex, StringComparison.Ordinal);
            Assert.Greater(deletePostInvalidationIndex, deleteFinallyIndex, rebindSource);
            StringAssert.Contains("bool TryClearBindingOverrides();", inputServiceContractsSource);
            StringAssert.Contains("public bool TryClearBindingOverrides()", inputManagerSource);
            StringAssert.Contains("if (_runtimeInputActionAsset == null && !EnsureInputActionsInitialized())", inputManagerSource);
            StringAssert.Contains("private static bool TryClearRuntimeBindingOverrides(INativeInputManagerRuntime inputManager)", rebindSource);
            StringAssert.Contains("if (!TryClearRuntimeBindingOverrides(inputManager))", rebindSource);
            StringAssert.DoesNotContain("inputManager.ClearBindingOverrides();", rebindSource);
            StringAssert.Contains("private static bool TryClearBindingOverrides(INativeInputManagerRuntime inputManager, ref ControlRemapIoResult result)", controlRemapperSource);
            StringAssert.Contains("if (!TryClearBindingOverrides(inputManager, ref result))", controlRemapperSource);
            StringAssert.Contains("if (HasRuntimeBindingPathConflict(inputManager))", controlRemapperSource);
            StringAssert.Contains("HasRuntimeOverrideDuplicate(map, actionIndex, bindingIndex, binding.overridePath)", controlRemapperSource);
            StringAssert.Contains("HasRuntimeDefaultPathConflict(map, actionIndex, bindingIndex, binding.overridePath)", controlRemapperSource);
            StringAssert.Contains("IsProtectedKeyboardEscapeOverride(map.name, action.name, binding.overridePath)", controlRemapperSource);
            StringAssert.Contains("IsProtectedKeyboardTabOverride(map.name, action.name, binding.overridePath)", controlRemapperSource);
            StringAssert.Contains("using Hecton8.SaveSystem;", controlRemapperSource);
            StringAssert.Contains("absolutePath = Path.GetFullPath(path);", controlRemapperSource);
            StringAssert.Contains("absoluteTempPath = Path.GetFullPath(tempPath);", controlRemapperSource);
            StringAssert.Contains("AsyncWriteManager.TryGetFileLength(absoluteTempPath, out long tempBytes, out _)", controlRemapperSource);
            StringAssert.Contains("tempBytes != byteCount", controlRemapperSource);
            StringAssert.Contains("AsyncWriteManager.FlushCriticalSavePath(absoluteTempPath, tempBytes, out _)", controlRemapperSource);
            StringAssert.Contains("AsyncWriteManager.TryGetFileLength(absolutePath, out long promotedBytes, out _)", controlRemapperSource);
            StringAssert.Contains("promotedBytes != byteCount", controlRemapperSource);
            StringAssert.Contains("AsyncWriteManager.FlushCriticalSavePath(absolutePath, promotedBytes, out _)", controlRemapperSource);
            StringAssert.Contains("catch (NotSupportedException)", rebindSource);
            StringAssert.Contains("TryDestroyDuplicateService", rebindSource);
            StringAssert.Contains("serviceSlot == GlobalRegistryServiceSlot.NativeInputManagerRuntime", rebindSource);
            StringAssert.Contains("BindNativeInputManager(currentService as INativeInputManagerRuntime);", rebindSource);
            StringAssert.Contains("if (IsRebinding)\n                CancelRebindOrPendingConflict();", rebindNormalized);
            StringAssert.Contains("_activeRebind != null || _pendingConflictAction != null", rebindSource);
            StringAssert.Contains("Cannot save binding overrides while rebinding.", rebindSource);
            StringAssert.Contains("CancelRebindOrPendingConflict();", rebindSource);
            StringAssert.Contains("private void CancelRebindOrPendingConflict()", rebindSource);
            StringAssert.Contains("CancelRebindOrPendingConflict();\n            INativeInputManagerRuntime inputManager = ResolveNativeInputManager()", rebindNormalized);
            StringAssert.Contains("TryReadActionEnabled", rebindSource);
            StringAssert.Contains("TryDisableActionForRebind", rebindSource);
            StringAssert.Contains("FailStartInteractiveRebind", rebindSource);
            StringAssert.Contains("if (_activeRebind == null)", rebindSource);
            StringAssert.Contains("TryRestoreActionEnabled", rebindSource);
            StringAssert.Contains("catch (NotSupportedException)", rebindSource);
            StringAssert.DoesNotContain("if (wasEnabled && action != null)\n                action.Enable();", rebindNormalized);
            StringAssert.Contains("loadResult.ResultCode == InputBindingTelemetryResult.FileMissing", rebindSource);
            StringAssert.Contains("InputBindingTelemetryEntry applyTelemetry = BuildApplyFailureTelemetry(0, clearStartTicks);", rebindSource);
            StringAssert.Contains("InputBindingTelemetryResult.IoFailure,\n                    InputBindingFaultFlags.BufferOverflow,", rebindNormalized);
            StringAssert.Contains("No saved binding overrides found; defaults applied.", rebindSource);
            StringAssert.Contains("if (_pendingConflictAction == null)", rebindSource);
            StringAssert.Contains("_activePreviousOverridePath", rebindSource);
            StringAssert.Contains("_pendingConflictPreviousOverridePath", rebindSource);
            StringAssert.Contains("_pendingConflictVictimPreviousOverridePath", rebindSource);
            StringAssert.Contains("TryRestoreBindingOverride", rebindSource);
            StringAssert.Contains("OnRebindSaveFailed?.Invoke(actionName, actionMap, bindingIndex);", rebindSource);
            StringAssert.Contains("OnOverridesSaveFailed?.Invoke();", rebindSource);
            StringAssert.Contains("Rebind auto-save failed; restored previous binding override.", rebindSource);
            StringAssert.Contains("Conflict rebind auto-save failed; restored previous binding overrides.", rebindSource);
            StringAssert.Contains("out bool multipleConflicts", rebindSource);
            StringAssert.Contains("Rebind rejected because binding path conflicts with multiple actions.", rebindSource);
            StringAssert.Contains("ShouldReserveKeyboardEscape(actionName, actionMap)", rebindSource);
            StringAssert.Contains("Rebind rejected because Keyboard Escape is reserved for Pause and Cancel.", rebindSource);
            StringAssert.Contains("ShouldReserveKeyboardTab(actionName, actionMap)", rebindSource);
            StringAssert.Contains("Rebind rejected because Keyboard Tab is reserved for UI tab navigation.", rebindSource);
            StringAssert.DoesNotContain("SaveOverrides();\n            }\n\n            OnRebindCompleted", rebindNormalized);
            StringAssert.DoesNotContain("action.RemoveBindingOverride(bindingIndex);\n                }\n\n                if (wasEnabled)", rebindNormalized);
            StringAssert.Contains("if (_registeredService)", rebindSource);
            StringAssert.Contains("if (!Application.isPlaying)", rebindSource);
            StringAssert.Contains("binding.overridePath == null", controlRemapperSource);
            StringAssert.Contains("record.PathByteLength == 0", controlRemapperSource);
            StringAssert.Contains("TryApplyOverridePath", controlRemapperSource);
            StringAssert.Contains("if (applied != recordCount)", controlRemapperSource);
            StringAssert.Contains("result.RecordCount = 0;", controlRemapperSource);
            StringAssert.Contains("state.BindingGuidHash64 == 0UL", controlRemapperSource);
            StringAssert.DoesNotContain("if (expectedHash == 0UL)\n                return true;", controlRemapperNormalized);
            StringAssert.Contains("SkipJsonObjectValue", controlRemapperSource);
            StringAssert.Contains("SkipJsonArrayValue", controlRemapperSource);
            StringAssert.Contains("SkipJsonNumberValue", controlRemapperSource);
            StringAssert.Contains("SkipJsonStringValue", controlRemapperSource);
            StringAssert.Contains("TryAdvanceJsonLiteral", controlRemapperSource);
            StringAssert.Contains("TryAdvanceUtf8Scalar", controlRemapperSource);
            StringAssert.Contains("IsUtf8Continuation", controlRemapperSource);
            StringAssert.Contains("LoadRollbackRecords", controlRemapperSource);
            StringAssert.Contains("int rollbackCount = 0;", controlRemapperSource);
            StringAssert.Contains("CaptureCurrentOverrides(inputManager, LoadRollbackRecords, out rollbackCount", controlRemapperSource);
            StringAssert.Contains("RestoreCapturedOverrides(LoadRollbackRecords, rollbackCount)", controlRemapperSource);
            StringAssert.Contains("ClearRollbackRecords(LoadRollbackRecords, rollbackCount);", controlRemapperSource);
            StringAssert.Contains("TryFinishRootObject(bytes, length, ref index, ref result)", controlRemapperSource);
            StringAssert.Contains("if (!hasMap || !hasAction || !hasBinding || !hasId || !hasPath)", controlRemapperSource);
            StringAssert.Contains("TokenEquals(bytes + tokenStart, tokenLength, \"bindings\") || !SkipValue", controlRemapperSource);
            StringAssert.Contains("private const int MaxUnknownJsonValueDepth = 16;", controlRemapperSource);
            StringAssert.Contains("private static bool SkipContainerValue", controlRemapperSource);
            StringAssert.Contains("return SkipJsonValue(bytes, length, ref index, 0);", controlRemapperSource);
            StringAssert.Contains("depth >= MaxUnknownJsonValueDepth", controlRemapperSource);
            StringAssert.Contains("IsHexDigit", controlRemapperSource);
            StringAssert.Contains("using System.Threading;", controlRemapperSource);
            StringAssert.Contains("private static int LoadRollbackLease;", controlRemapperSource);
            StringAssert.Contains("TryAcquireLoadRollbackLease", controlRemapperSource);
            StringAssert.Contains("Interlocked.CompareExchange(ref LoadRollbackLease, 1, 0)", controlRemapperSource);
            StringAssert.Contains("Interlocked.Exchange(ref LoadRollbackLease, 0)", controlRemapperSource);
            int rollbackLeaseIndex = controlRemapperNormalized.IndexOf("TryAcquireLoadRollbackLease(ref result", StringComparison.Ordinal);
            int rollbackCaptureIndex = controlRemapperNormalized.IndexOf("CaptureCurrentOverrides(inputManager", StringComparison.Ordinal);
            Assert.GreaterOrEqual(rollbackLeaseIndex, 0);
            Assert.GreaterOrEqual(rollbackCaptureIndex, 0);
            Assert.Less(rollbackLeaseIndex, rollbackCaptureIndex);
            StringAssert.Contains("catch (ArgumentException)", controlRemapperSource);
            StringAssert.Contains("catch (NotSupportedException)", controlRemapperSource);
            StringAssert.Contains("MarkIoFailure", controlRemapperSource);
            StringAssert.Contains("MarkFailure", controlRemapperSource);
            StringAssert.Contains("mutationGuardHeld = vault.TryAcquireMutationGuard(TelemetryMutationGuardMask);", controlRemapperSource);
            StringAssert.Contains("vault.TryResolveHandle(in cursorHandle, out NativeArray<int> cursor)", controlRemapperSource);
            StringAssert.Contains("vault.TryResolveHandle(in ringHandle, out NativeArray<InputBindingTelemetryEntry> ring)", controlRemapperSource);
            StringAssert.Contains("UnsafeUtility.MemCpy(destination + (i * stride), source + (ringIndex * stride), stride);", controlRemapperSource);
            StringAssert.Contains("vault.ReleaseMutationGuard(TelemetryMutationGuardMask);", controlRemapperSource);
            int telemetryReleaseIndex = controlRemapperNormalized.IndexOf("vault.ReleaseMutationGuard(TelemetryMutationGuardMask);", StringComparison.Ordinal);
            int telemetryDirectoryIndex = controlRemapperNormalized.IndexOf("Directory.CreateDirectory(directory);", StringComparison.Ordinal);
            Assert.GreaterOrEqual(telemetryReleaseIndex, 0);
            Assert.GreaterOrEqual(telemetryDirectoryIndex, 0);
            Assert.Less(telemetryReleaseIndex, telemetryDirectoryIndex);
            StringAssert.DoesNotContain("string.IsNullOrEmpty(binding.overridePath)", controlRemapperSource);
            StringAssert.DoesNotContain("RefreshAllBindingsNow();\n            if (!IsActive) return;", pausePanelNormalized);
            StringAssert.Contains("if (!IsActive) return;\n            RefreshAllBindingsNow();", pausePanelNormalized);
            StringAssert.Contains("StatusCannotSaveWhileRebinding", pausePanelSource);
            StringAssert.Contains("StatusBindingsSaveFailed", pausePanelSource);
            StringAssert.Contains("HandleRebindSaveFailed", pausePanelSource);
            StringAssert.Contains("rebinding.OnRebindSaveFailed += _rebindSaveFailedAction", pausePanelSource);
            StringAssert.DoesNotContain("OnOverridesSaveFailed += _overridesSaveFailedAction", pausePanelSource);
            StringAssert.Contains("_bindingOverridesChangedAction ??= HandleBindingOverridesChanged", pausePanelSource);
            StringAssert.Contains("rebinding.OnOverridesLoaded += _bindingOverridesChangedAction", pausePanelSource);
            StringAssert.Contains("rebinding.OnOverridesSaved += _bindingOverridesChangedAction", pausePanelSource);
            StringAssert.Contains("rebinding.OnOverridesCleared += _bindingOverridesChangedAction", pausePanelSource);
            StringAssert.Contains("rebinding.OnOverridesLoaded -= _bindingOverridesChangedAction", pausePanelSource);
            StringAssert.Contains("rebinding.OnOverridesSaved -= _bindingOverridesChangedAction", pausePanelSource);
            StringAssert.Contains("rebinding.OnOverridesCleared -= _bindingOverridesChangedAction", pausePanelSource);
            StringAssert.Contains("_displayStyleChangedAction ??= HandleInputDisplayStyleChanged", pausePanelSource);
            StringAssert.Contains("input.OnInputDisplayStyleCodeChanged += _displayStyleChangedAction", pausePanelSource);
            StringAssert.Contains("input.OnInputDisplayStyleCodeChanged -= _displayStyleChangedAction", pausePanelSource);
            StringAssert.Contains("private void HandleInputDisplayStyleChanged(byte styleCode)", pausePanelSource);
            StringAssert.Contains("if (!IsActive)\n                return;\n\n            RefreshAllBindings();\n            UpdateStatusForSelected();", pausePanelNormalized);
            StringAssert.Contains("private void HandleBindingOverridesChanged()", pausePanelSource);
            StringAssert.Contains("if (!IsActive || _ownsActiveRebind)\n                return;\n\n            RefreshAllBindings();\n            UpdateStatusForSelected();", pausePanelNormalized);
            StringAssert.Contains("private bool _ownsActiveRebind;", pausePanelSource);
            StringAssert.Contains("CancelOwnedRebindIfNeeded(rebinding);", pausePanelSource);
            StringAssert.Contains("if (!IsActive)\n            {\n                CancelOwnedRebindIfNeeded(_subscribedRebindingService);", pausePanelNormalized);
            StringAssert.Contains("serviceSlot != GlobalRegistryServiceSlot.NativeInputManagerRuntime &&\n                serviceSlot != GlobalRegistryServiceSlot.InputBinding)\n            {\n                return;\n            }\n\n            CancelOwnedRebindIfNeeded(_subscribedRebindingService);\n            Unsubscribe();", pausePanelNormalized);
            StringAssert.Contains("if (!IsActive) return false;\n            if (!_ownsActiveRebind) return false;", pausePanelNormalized);
            StringAssert.Contains("_ownsActiveRebind = true;\n            bool started = rebinding.StartInteractiveRebind(", pausePanelNormalized);
            StringAssert.Contains("if (!_ownsActiveRebind) return;\n            if (!IsActive) return;", pausePanelNormalized);
            StringAssert.Contains("StatusConflictModalUnavailable", pausePanelSource);
            StringAssert.Contains("IModalWindowService modalWindow = GlobalRegistry.ModalWindow;", pausePanelSource);
            StringAssert.Contains("onCancel?.Invoke();", pausePanelSource);
            StringAssert.Contains("modalWindow.ShowModal(", pausePanelSource);
            StringAssert.DoesNotContain("ModalWindow.Show(", pausePanelSource);
            StringAssert.Contains("rebinding.CancelRebind();\n            _ownsActiveRebind = false;", pausePanelNormalized);
            StringAssert.Contains("private void CancelOwnedRebindIfNeeded(IInputBindingService rebinding)", pausePanelSource);
            StringAssert.Contains("_ownsActiveRebind = true;", pausePanelSource);
            StringAssert.Contains("_ownsActiveRebind = false;", pausePanelSource);
            StringAssert.Contains("TryRestoreBindingOverride", pausePanelSource);
            StringAssert.Contains("if (rebinding == null || !rebinding.SaveOverrides())", pausePanelSource);
            StringAssert.Contains("INativeInputManagerRuntime input = ResolveInputManager();\n            for (int i = 0; i < _rows.Length; i++)\n                RefreshRowBinding(_rows[i], input);", pausePanelNormalized);
            StringAssert.Contains("private void RefreshRowBinding(RebindRow row, INativeInputManagerRuntime input)", pausePanelSource);
            StringAssert.Contains("private static int ResolveBindingIndex(\n            INativeInputManagerRuntime input,", pausePanelNormalized);
            StringAssert.Contains("int displayPreferredIndex = input.GetPreferredBindingIndex(actionName, actionMap);", pausePanelSource);
            StringAssert.Contains("bindingIndex = ResolveBindingIndex(input, action, row.actionName, row.actionMap, row.bindingIndex);", pausePanelSource);
            StringAssert.DoesNotContain("RefreshRowBinding(_rows[i]);", pausePanelSource);
            StringAssert.DoesNotContain("ResolveBindingIndex(action,", pausePanelSource);
            StringAssert.Contains("string previousOverridePath = action.bindings[bindingIndex].overridePath", pausePanelSource);
            StringAssert.DoesNotContain("rebinding.SaveOverrides();", pausePanelSource);
            StringAssert.Contains("if (rebinding.SaveOverrides())", pausePanelSource);
            StringAssert.Contains("if (rebinding.LoadOverrides())", pausePanelSource);
            StringAssert.Contains("if (rebinding.ClearOverrides())", pausePanelSource);
            StringAssert.Contains("!rebinding.IsRebinding &&", pausePanelSource);
            StringAssert.Contains("_subscribedInputBindingService.OnOverridesLoaded += HandleOverridesLoaded;", interactionUiSource);
            StringAssert.Contains("_subscribedInputBindingService.OnOverridesSaved += HandleOverridesSaved;", interactionUiSource);
            StringAssert.Contains("_subscribedInputBindingService.OnOverridesCleared += HandleOverridesCleared;", interactionUiSource);
            StringAssert.Contains("_subscribedInputBindingService.OnOverridesLoaded -= HandleOverridesLoaded;", interactionUiSource);
            StringAssert.Contains("_subscribedInputBindingService.OnOverridesSaved -= HandleOverridesSaved;", interactionUiSource);
            StringAssert.Contains("_subscribedInputBindingService.OnOverridesCleared -= HandleOverridesCleared;", interactionUiSource);
            StringAssert.Contains("private void HandleOverridesSaved()", interactionUiSource);
            StringAssert.Contains("private IInputBindingService _subscribedInputBindingService;", legacyInteractionUiSource);
            StringAssert.Contains("SubscribeInputBindingServiceIfAvailable(GlobalRegistry.InputBinding);", legacyInteractionUiSource);
            StringAssert.Contains("UnsubscribeInputBindingService();", legacyInteractionUiSource);
            StringAssert.Contains("serviceSlot != GlobalRegistryServiceSlot.NativeInputManagerRuntime", legacyInteractionUiSource);
            StringAssert.Contains("serviceSlot != GlobalRegistryServiceSlot.InputBinding", legacyInteractionUiSource);
            StringAssert.Contains("if (serviceSlot == GlobalRegistryServiceSlot.InputBinding)", legacyInteractionUiSource);
            StringAssert.Contains("serviceSlot == GlobalRegistryServiceSlot.Input ||\n                serviceSlot == GlobalRegistryServiceSlot.NativeInputManagerRuntime", legacyInteractionUiSource.Replace("\r\n", "\n"));
            StringAssert.Contains("SubscribeInputBindingServiceIfAvailable(currentService as IInputBindingService);", legacyInteractionUiSource);
            StringAssert.Contains("_subscribedInputBindingService.OnRebindCompleted += _rebindCompletedAction;", legacyInteractionUiSource);
            StringAssert.Contains("_subscribedInputBindingService.OnRebindCanceled += _rebindCanceledAction;", legacyInteractionUiSource);
            StringAssert.Contains("_subscribedInputBindingService.OnOverridesLoaded += _bindingOverridesChangedAction;", legacyInteractionUiSource);
            StringAssert.Contains("_subscribedInputBindingService.OnOverridesSaved += _bindingOverridesChangedAction;", legacyInteractionUiSource);
            StringAssert.Contains("_subscribedInputBindingService.OnOverridesCleared += _bindingOverridesChangedAction;", legacyInteractionUiSource);
            StringAssert.Contains("_subscribedInputBindingService.OnRebindCompleted -= _rebindCompletedAction;", legacyInteractionUiSource);
            StringAssert.Contains("_subscribedInputBindingService.OnRebindCanceled -= _rebindCanceledAction;", legacyInteractionUiSource);
            StringAssert.Contains("_subscribedInputBindingService.OnOverridesLoaded -= _bindingOverridesChangedAction;", legacyInteractionUiSource);
            StringAssert.Contains("_subscribedInputBindingService.OnOverridesSaved -= _bindingOverridesChangedAction;", legacyInteractionUiSource);
            StringAssert.Contains("_subscribedInputBindingService.OnOverridesCleared -= _bindingOverridesChangedAction;", legacyInteractionUiSource);
            StringAssert.Contains("private void HandleBindingOverridesChanged()", legacyInteractionUiSource);
            StringAssert.Contains("string.Equals(actionMap, PlayerActionMapName, StringComparison.OrdinalIgnoreCase)", legacyInteractionUiSource);
            StringAssert.Contains("string.Equals(actionName, InteractActionName, StringComparison.OrdinalIgnoreCase)", legacyInteractionUiSource);
            StringAssert.Contains("private IInputBindingService _subscribedInputBindingService;", playerInteractionSource);
            StringAssert.Contains("private const string PlayerActionMapName = \"Player\";", playerInteractionSource);
            StringAssert.Contains("private const string InteractActionName = \"Interact\";", playerInteractionSource);
            StringAssert.Contains("SubscribeInputBindingServiceIfAvailable(GlobalRegistry.InputBinding);", playerInteractionSource);
            StringAssert.Contains("UnsubscribeInputBindingService();", playerInteractionSource);
            StringAssert.Contains("case GlobalRegistryServiceSlot.InputBinding:", playerInteractionSource);
            StringAssert.Contains("SubscribeInputBindingServiceIfAvailable(currentService as IInputBindingService);", playerInteractionSource);
            StringAssert.Contains("private void OnDestroy()", playerInteractionSource);
            StringAssert.Contains("TryUnregisterLateFrameTickable();\n            UnsubscribeInputBindingService();\n            TryUnregisterHotSwapListener();", playerInteractionSource.Replace("\r\n", "\n"));
            StringAssert.Contains("_subscribedInputBindingService.OnRebindCompleted += _rebindCompletedAction;", playerInteractionSource);
            StringAssert.Contains("_subscribedInputBindingService.OnRebindCanceled += _rebindCanceledAction;", playerInteractionSource);
            StringAssert.Contains("_subscribedInputBindingService.OnOverridesLoaded += _bindingOverridesChangedAction;", playerInteractionSource);
            StringAssert.Contains("_subscribedInputBindingService.OnOverridesSaved += _bindingOverridesChangedAction;", playerInteractionSource);
            StringAssert.Contains("_subscribedInputBindingService.OnOverridesCleared += _bindingOverridesChangedAction;", playerInteractionSource);
            StringAssert.Contains("_subscribedInputBindingService.OnRebindCompleted -= _rebindCompletedAction;", playerInteractionSource);
            StringAssert.Contains("_subscribedInputBindingService.OnRebindCanceled -= _rebindCanceledAction;", playerInteractionSource);
            StringAssert.Contains("_subscribedInputBindingService.OnOverridesLoaded -= _bindingOverridesChangedAction;", playerInteractionSource);
            StringAssert.Contains("_subscribedInputBindingService.OnOverridesSaved -= _bindingOverridesChangedAction;", playerInteractionSource);
            StringAssert.Contains("_subscribedInputBindingService.OnOverridesCleared -= _bindingOverridesChangedAction;", playerInteractionSource);
            StringAssert.Contains("private static void HandleBindingOverridesChanged()", playerInteractionSource);
            StringAssert.Contains("string.Equals(actionMap, PlayerActionMapName, StringComparison.OrdinalIgnoreCase)", playerInteractionSource);
            StringAssert.Contains("string.Equals(actionName, InteractActionName, StringComparison.OrdinalIgnoreCase)", playerInteractionSource);
            StringAssert.Contains("inputManager.GetBindingDisplayString(InteractActionName, PlayerActionMapName)", playerInteractionSource);
            StringAssert.Contains("RefreshActiveInteractKeyCache();", playerInteractionSource);
            StringAssert.DoesNotContain("RefreshAllBindings();\n            if (!IsControlsTabActive) return;", pdaControlsNormalized);
            StringAssert.Contains("if (!IsControlsTabActive) return;\n            RefreshAllBindings();", pdaControlsNormalized);
            StringAssert.Contains("StatusBindingsSaveFailed", pdaControlsSource);
            StringAssert.Contains("HandleRebindSaveFailed", pdaControlsSource);
            StringAssert.Contains("rebinding.OnRebindSaveFailed += _rebindSaveFailedAction", pdaControlsSource);
            StringAssert.DoesNotContain("OnOverridesSaveFailed += _overridesSaveFailedAction", pdaControlsSource);
            StringAssert.Contains("_hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);", pausePanelSource);
            StringAssert.Contains("GlobalRegistry.TryUnregisterHotSwapListener(this);", pausePanelSource);
            StringAssert.DoesNotContain("GlobalRegistry.RegisterHotSwapListener(this);", pausePanelSource);
            StringAssert.DoesNotContain("GlobalRegistry.IsHotSwapListenerRegistered(this)", pausePanelSource);
            StringAssert.DoesNotContain("GlobalRegistry.UnregisterHotSwapListener(this);", pausePanelSource);
            StringAssert.Contains("_hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);", pdaControlsSource);
            StringAssert.Contains("GlobalRegistry.TryUnregisterHotSwapListener(this);", pdaControlsSource);
            StringAssert.DoesNotContain("GlobalRegistry.RegisterHotSwapListener(this);", pdaControlsSource);
            StringAssert.DoesNotContain("GlobalRegistry.IsHotSwapListenerRegistered(this)", pdaControlsSource);
            StringAssert.DoesNotContain("GlobalRegistry.UnregisterHotSwapListener(this);", pdaControlsSource);
            StringAssert.Contains("rebinding.OnConflictDetected += _conflictDetectedAction", pdaControlsSource);
            StringAssert.Contains("rebinding.OnConflictDetected -= _conflictDetectedAction", pdaControlsSource);
            StringAssert.Contains("private void HandleConflictDetected(string actionName, string conflictingAction, string newBinding, Action onConfirm, Action onCancel)", pdaControlsSource);
            StringAssert.Contains("StatusConflictModalUnavailable", pdaControlsSource);
            StringAssert.Contains("IModalWindowService modalWindow = GlobalRegistry.ModalWindow;", pdaControlsSource);
            StringAssert.Contains("onCancel?.Invoke();", pdaControlsSource);
            StringAssert.Contains("modalWindow.ShowModal(", pdaControlsSource);
            StringAssert.DoesNotContain("ModalWindow.Show(", pdaControlsSource);
            StringAssert.Contains("case PDAEventType.Closed:", pdaControlsSource);
            StringAssert.Contains("private bool _ownsActiveRebind;", pdaControlsSource);
            StringAssert.Contains("CancelOwnedRebindIfNeeded(_subscribedRebindingService);", pdaControlsSource);
            StringAssert.Contains("if (!IsControlsTabActive)\n            {\n                CancelOwnedRebindIfNeeded(_subscribedRebindingService);", pdaControlsNormalized);
            StringAssert.Contains("serviceSlot != GlobalRegistryServiceSlot.NativeInputManagerRuntime &&\n                serviceSlot != GlobalRegistryServiceSlot.InputBinding)\n            {\n                return;\n            }\n\n            CancelOwnedRebindIfNeeded(_subscribedRebindingService);\n            Unsubscribe();", pdaControlsNormalized);
            StringAssert.Contains("if (serviceSlot == GlobalRegistryServiceSlot.InputBinding)\n                _cachedRebindingService = currentService as IInputBindingService;\n            else\n                _cachedInput = currentService as INativeInputManagerRuntime ?? GlobalRegistry.NativeInputRuntime;", pdaControlsNormalized);
            StringAssert.Contains("if (!PlayerPDA.IsOpen) return false;\n            if (!_ownsActiveRebind) return false;", pdaControlsNormalized);
            StringAssert.Contains("_ownsActiveRebind = true;\n            bool started = rebinding.StartInteractiveRebind(", pdaControlsNormalized);
            StringAssert.Contains("if (!_ownsActiveRebind) return;\n            if (!IsControlsTabActive) return;", pdaControlsNormalized);
            StringAssert.Contains("rebinding.CancelRebind();\n            _ownsActiveRebind = false;", pdaControlsNormalized);
            StringAssert.Contains("private void CancelOwnedRebindIfNeeded(IInputBindingService rebinding)", pdaControlsSource);
            StringAssert.Contains("_ownsActiveRebind = true;", pdaControlsSource);
            StringAssert.Contains("_ownsActiveRebind = false;", pdaControlsSource);
            StringAssert.Contains("private void RefreshAllIfControlsTabActive()", pdaControlsSource);
            StringAssert.DoesNotContain("Subscribe();\n            RefreshAll();", pdaControlsNormalized);
            StringAssert.Contains("StatusBindingsClearFailed", pdaControlsSource);
            StringAssert.Contains("TryRestoreBindingOverride", pdaControlsSource);
            StringAssert.Contains("if (rebinding == null || !rebinding.SaveOverrides())", pdaControlsSource);
            StringAssert.Contains("INativeInputManagerRuntime inputManager = ResolveInputManager();\n            for (int i = 0; i < rows.Length; i++)", pdaControlsNormalized);
            StringAssert.Contains("private void RefreshRowBinding(RebindRow row, INativeInputManagerRuntime inputManager)", pdaControlsSource);
            StringAssert.Contains("private static int ResolveBindingIndex(\n            INativeInputManagerRuntime inputManager,", pdaControlsNormalized);
            StringAssert.DoesNotContain("ResolveBindingIndex(action,", pdaControlsSource);
            StringAssert.DoesNotContain("private uint _pad", inputContractsSource);
            StringAssert.DoesNotContain("private ulong _pad", inputContractsSource);
            StringAssert.DoesNotContain("PlayerPrefs", guide);
            StringAssert.Contains("controls.json", guide);
            StringAssert.Contains("TryClearBindingOverrides()", guide);
        }

        [Test]
        public void CorruptedControlsJsonFailsClosedWithoutClearingOverrides()
        {
            string path = Path.Combine(Path.GetTempPath(), "h8_controls_bad_" + Guid.NewGuid().ToString("N") + ".json");
            MockNativeInputRuntime runtime = new MockNativeInputRuntime();

            try
            {
                File.WriteAllText(path, "{\"v\":1,\"bindings\":[{\"map\":1");
                bool loaded = ControlRemapper.TryLoadOverrides(runtime, path, out ControlRemapIoResult result);

                Assert.IsFalse(loaded);
                Assert.AreEqual(0, runtime.ClearCount);
                Assert.AreEqual(InputBindingTelemetryResult.InvalidJson, result.ResultCode);
                Assert.AreNotEqual(0u, result.FaultFlags & InputBindingFaultFlags.InvalidSchema);
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [Test]
        public void ControlsJsonSerializerSurvivesBoundedInputSpam()
        {
            string path = Path.Combine(Path.GetTempPath(), "h8_controls_" + Guid.NewGuid().ToString("N") + ".json");
            string tempPath = path + ".tmp";
            MockNativeInputRuntime runtime = new MockNativeInputRuntime();
            InputAction action = runtime.Player.AddAction("Interact", binding: "<Keyboard>/e");

            try
            {
                for (int i = 0; i < 10000; i++)
                {
                    string pathOverride = (i & 1) == 0 ? "<Keyboard>/f" : "<Keyboard>/g";
                    action.ApplyBindingOverride(0, pathOverride);

                    bool saved = ControlRemapper.TrySaveOverrides(runtime, path, tempPath, out ControlRemapIoResult result);
                    Assert.IsTrue(saved);
                    Assert.AreEqual(InputBindingTelemetryResult.Success, result.ResultCode);
                    Assert.AreEqual(1, result.RecordCount);
                    Assert.Greater(result.ByteCount, 0);
                    Assert.AreEqual(0, runtime.LegacySaveCalls);
                }
            }
            finally
            {
                runtime.Dispose();
                if (File.Exists(path))
                    File.Delete(path);
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [Test]
        public void ControlsJsonAtomicSaveRejectsTempPathCollisionWithoutTruncatingExistingFile()
        {
            string path = Path.Combine(Path.GetTempPath(), "h8_controls_same_temp_" + Guid.NewGuid().ToString("N") + ".json");
            byte[] sentinel = Encoding.UTF8.GetBytes("existing-controls-sentinel");
            MockNativeInputRuntime runtime = new MockNativeInputRuntime();
            InputAction action = runtime.Player.AddAction("Interact", binding: "<Keyboard>/e");

            try
            {
                File.WriteAllBytes(path, sentinel);
                action.ApplyBindingOverride(0, "<Keyboard>/f");

                bool saved = ControlRemapper.TrySaveOverrides(runtime, path, path, out ControlRemapIoResult result);

                Assert.IsFalse(saved);
                Assert.AreEqual(InputBindingTelemetryResult.IoFailure, result.ResultCode);
                Assert.AreNotEqual(0u, result.FaultFlags & InputBindingFaultFlags.IoException);
                Assert.AreEqual(1, result.RecordCount);
                Assert.Greater(result.ByteCount, 0);
                Assert.AreEqual(InputBindingTelemetryOperation.Save, result.Telemetry.Operation);
                Assert.AreEqual(InputBindingTelemetryResult.IoFailure, result.Telemetry.Result);
                Assert.AreEqual(result.FaultFlags, result.Telemetry.FaultFlags);
                Assert.AreEqual((uint)result.ByteCount, result.Telemetry.Bytes);
                Assert.AreEqual((ushort)result.RecordCount, result.Telemetry.RecordCount);
                Assert.IsTrue(File.Exists(path));
                CollectionAssert.AreEqual(sentinel, File.ReadAllBytes(path));
            }
            finally
            {
                runtime.Dispose();
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [Test]
        public void ControlsJsonAtomicSaveRejectsCrossDirectoryTempWithoutWritingFiles()
        {
            string root = Path.Combine(Path.GetTempPath(), "h8_controls_cross_temp_" + Guid.NewGuid().ToString("N"));
            string finalDirectory = Path.Combine(root, "final");
            string tempDirectory = Path.Combine(root, "temp");
            string path = Path.Combine(finalDirectory, "controls.json");
            string tempPath = Path.Combine(tempDirectory, "controls.json.tmp");
            MockNativeInputRuntime runtime = new MockNativeInputRuntime();
            InputAction action = runtime.Player.AddAction("Interact", binding: "<Keyboard>/e");

            try
            {
                Directory.CreateDirectory(finalDirectory);
                Directory.CreateDirectory(tempDirectory);
                action.ApplyBindingOverride(0, "<Keyboard>/f");

                bool saved = ControlRemapper.TrySaveOverrides(runtime, path, tempPath, out ControlRemapIoResult result);

                Assert.IsFalse(saved);
                Assert.AreEqual(InputBindingTelemetryResult.IoFailure, result.ResultCode);
                Assert.AreNotEqual(0u, result.FaultFlags & InputBindingFaultFlags.IoException);
                Assert.AreNotEqual(0u, result.FaultFlags & InputBindingFaultFlags.UnsupportedPath);
                Assert.AreEqual(InputBindingTelemetryOperation.Save, result.Telemetry.Operation);
                Assert.AreEqual(InputBindingTelemetryResult.IoFailure, result.Telemetry.Result);
                Assert.AreEqual(result.FaultFlags, result.Telemetry.FaultFlags);
                Assert.IsFalse(File.Exists(path));
                Assert.IsFalse(File.Exists(tempPath));
            }
            finally
            {
                runtime.Dispose();
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public void ControlsJsonReloadsSavedOverrideAfterRuntimeRestart()
        {
            string path = Path.Combine(Path.GetTempPath(), "h8_controls_reload_" + Guid.NewGuid().ToString("N") + ".json");
            string tempPath = path + ".tmp";
            Guid bindingId = new Guid("13320000-0000-0000-0000-000000000001");
            MockNativeInputRuntime writerRuntime = new MockNativeInputRuntime();
            InputAction writerAction = AddActionWithStableBinding(writerRuntime.Player, "Interact", "<Keyboard>/e", bindingId);

            try
            {
                writerAction.ApplyBindingOverride(0, "<Keyboard>/f");
                bool saved = ControlRemapper.TrySaveOverrides(writerRuntime, path, tempPath, out ControlRemapIoResult saveResult);
                Assert.IsTrue(saved);
                Assert.AreEqual(InputBindingTelemetryResult.Success, saveResult.ResultCode);

                writerRuntime.Dispose();
                writerRuntime = null;

                MockNativeInputRuntime readerRuntime = new MockNativeInputRuntime();
                InputAction readerAction = AddActionWithStableBinding(readerRuntime.Player, "Interact", "<Keyboard>/e", bindingId);

                try
                {
                    bool loaded = ControlRemapper.TryLoadOverrides(readerRuntime, path, out ControlRemapIoResult loadResult);
                    Assert.IsTrue(loaded);
                    Assert.AreEqual(InputBindingTelemetryResult.Success, loadResult.ResultCode);
                    Assert.AreEqual(1, readerRuntime.ClearCount);
                    Assert.AreEqual(0, readerRuntime.LegacyLoadCalls);
                    Assert.AreEqual("<Keyboard>/f", readerAction.bindings[0].overridePath);
                }
                finally
                {
                    readerRuntime.Dispose();
                }
            }
            finally
            {
                writerRuntime?.Dispose();
                if (File.Exists(path))
                    File.Delete(path);
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [Test]
        public void ControlsJsonRejectsSavingOverrideThatCollidesWithDefaultBinding()
        {
            string path = Path.Combine(Path.GetTempPath(), "h8_controls_save_conflict_" + Guid.NewGuid().ToString("N") + ".json");
            string tempPath = path + ".tmp";
            Guid interactId = new Guid("13320000-0000-0000-0000-0000000000B1");
            Guid jumpId = new Guid("13320000-0000-0000-0000-0000000000B2");
            MockNativeInputRuntime runtime = new MockNativeInputRuntime();
            InputAction interact = AddActionWithStableBinding(runtime.Player, "Interact", "<Keyboard>/e", interactId);
            AddActionWithStableBinding(runtime.Player, "Jump", "<Keyboard>/space", jumpId);

            try
            {
                interact.ApplyBindingOverride(0, "<Keyboard>/space");

                bool saved = ControlRemapper.TrySaveOverrides(runtime, path, tempPath, out ControlRemapIoResult result);

                Assert.IsFalse(saved);
                Assert.AreEqual(InputBindingTelemetryResult.UnsupportedPath, result.ResultCode);
                Assert.AreNotEqual(0u, result.FaultFlags & InputBindingFaultFlags.UnsupportedPath);
                Assert.IsFalse(File.Exists(path));
                Assert.IsFalse(File.Exists(tempPath));
            }
            finally
            {
                runtime.Dispose();
                if (File.Exists(path))
                    File.Delete(path);
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [Test]
        public void ControlsJsonRejectsSavingDuplicateOverridePathsInSameMap()
        {
            string path = Path.Combine(Path.GetTempPath(), "h8_controls_save_duplicate_" + Guid.NewGuid().ToString("N") + ".json");
            string tempPath = path + ".tmp";
            Guid interactId = new Guid("13320000-0000-0000-0000-0000000000C1");
            Guid useId = new Guid("13320000-0000-0000-0000-0000000000C2");
            MockNativeInputRuntime runtime = new MockNativeInputRuntime();
            InputAction interact = AddActionWithStableBinding(runtime.Player, "Interact", "<Keyboard>/e", interactId);
            InputAction use = AddActionWithStableBinding(runtime.Player, "Use", "<Keyboard>/f", useId);

            try
            {
                interact.ApplyBindingOverride(0, "<Keyboard>/r");
                use.ApplyBindingOverride(0, "<Keyboard>/r");

                bool saved = ControlRemapper.TrySaveOverrides(runtime, path, tempPath, out ControlRemapIoResult result);

                Assert.IsFalse(saved);
                Assert.AreEqual(InputBindingTelemetryResult.UnsupportedPath, result.ResultCode);
                Assert.AreNotEqual(0u, result.FaultFlags & InputBindingFaultFlags.UnsupportedPath);
                Assert.IsFalse(File.Exists(path));
                Assert.IsFalse(File.Exists(tempPath));
            }
            finally
            {
                runtime.Dispose();
                if (File.Exists(path))
                    File.Delete(path);
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [Test]
        public void ControlsJsonRejectsSavingReservedEscapeForNonPauseAction()
        {
            string path = Path.Combine(Path.GetTempPath(), "h8_controls_save_escape_" + Guid.NewGuid().ToString("N") + ".json");
            string tempPath = path + ".tmp";
            MockNativeInputRuntime runtime = new MockNativeInputRuntime();
            InputAction interact = AddActionWithStableBinding(runtime.Player, "Interact", "<Keyboard>/e", new Guid("13320000-0000-0000-0000-0000000000D3"));

            try
            {
                interact.ApplyBindingOverride(0, "<Keyboard>/escape");

                bool saved = ControlRemapper.TrySaveOverrides(runtime, path, tempPath, out ControlRemapIoResult result);

                Assert.IsFalse(saved);
                Assert.AreEqual(InputBindingTelemetryResult.UnsupportedPath, result.ResultCode);
                Assert.AreNotEqual(0u, result.FaultFlags & InputBindingFaultFlags.UnsupportedPath);
                Assert.IsFalse(File.Exists(path));
                Assert.IsFalse(File.Exists(tempPath));
            }
            finally
            {
                runtime.Dispose();
                if (File.Exists(path))
                    File.Delete(path);
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [Test]
        public void ControlsJsonRejectsSavingReservedTabForNonUiTabAction()
        {
            string path = Path.Combine(Path.GetTempPath(), "h8_controls_save_tab_" + Guid.NewGuid().ToString("N") + ".json");
            string tempPath = path + ".tmp";
            MockNativeInputRuntime runtime = new MockNativeInputRuntime();
            InputAction pda = AddActionWithStableBinding(runtime.Player, "PDA", "<Keyboard>/p", new Guid("13320000-0000-0000-0000-0000000000D5"));
            AddActionWithStableBinding(runtime.UI, "TabNext", "<Keyboard>/tab", new Guid("13320000-0000-0000-0000-0000000000D6"));

            try
            {
                pda.ApplyBindingOverride(0, "<Keyboard>/tab");

                bool saved = ControlRemapper.TrySaveOverrides(runtime, path, tempPath, out ControlRemapIoResult result);

                Assert.IsFalse(saved);
                Assert.AreEqual(InputBindingTelemetryResult.UnsupportedPath, result.ResultCode);
                Assert.AreNotEqual(0u, result.FaultFlags & InputBindingFaultFlags.UnsupportedPath);
                Assert.IsFalse(File.Exists(path));
                Assert.IsFalse(File.Exists(tempPath));
            }
            finally
            {
                runtime.Dispose();
                if (File.Exists(path))
                    File.Delete(path);
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [Test]
        public void ControlsJsonAllowsSavingReservedTabForUiTabNext()
        {
            string path = Path.Combine(Path.GetTempPath(), "h8_controls_save_ui_tab_" + Guid.NewGuid().ToString("N") + ".json");
            string tempPath = path + ".tmp";
            Guid tabNextId = new Guid("13320000-0000-0000-0000-0000000000DA");
            MockNativeInputRuntime writerRuntime = new MockNativeInputRuntime();
            InputAction writerTabNext = AddActionWithStableBinding(writerRuntime.UI, "TabNext", "<Keyboard>/e", tabNextId);

            try
            {
                writerTabNext.ApplyBindingOverride(0, "<Keyboard>/tab");
                bool saved = ControlRemapper.TrySaveOverrides(writerRuntime, path, tempPath, out ControlRemapIoResult saveResult);
                Assert.IsTrue(saved);
                Assert.AreEqual(InputBindingTelemetryResult.Success, saveResult.ResultCode);

                MockNativeInputRuntime readerRuntime = new MockNativeInputRuntime();
                InputAction readerTabNext = AddActionWithStableBinding(readerRuntime.UI, "TabNext", "<Keyboard>/e", tabNextId);

                try
                {
                    bool loaded = ControlRemapper.TryLoadOverrides(readerRuntime, path, out ControlRemapIoResult loadResult);

                    Assert.IsTrue(loaded);
                    Assert.AreEqual(1, readerRuntime.ClearCount);
                    Assert.AreEqual("<Keyboard>/tab", readerTabNext.bindings[0].overridePath);
                    Assert.AreEqual(InputBindingTelemetryResult.Success, loadResult.ResultCode);
                }
                finally
                {
                    readerRuntime.Dispose();
                }
            }
            finally
            {
                writerRuntime.Dispose();
                if (File.Exists(path))
                    File.Delete(path);
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [Test]
        public void InteractiveRebindDetectsUnresolvableMultiConflict()
        {
            MockNativeInputRuntime runtime = new MockNativeInputRuntime();
            GameObject host = new GameObject("RebindingManager_MultiConflict_Test");
            RebindingManager rebinding = host.AddComponent<RebindingManager>();
            InputAction current = AddActionWithStableBinding(runtime.Player, "Interact", "<Keyboard>/e", new Guid("13320000-0000-0000-0000-0000000000E1"));
            AddActionWithStableBinding(runtime.Player, "Jump", "<Keyboard>/space", new Guid("13320000-0000-0000-0000-0000000000E2"));
            AddActionWithStableBinding(runtime.Player, "Use", "<Keyboard>/space", new Guid("13320000-0000-0000-0000-0000000000E3"));

            try
            {
                current.ApplyBindingOverride(0, "<Keyboard>/space");
                FieldInfo nativeInputManagerField = typeof(RebindingManager).GetField("_nativeInputManager", BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo detectConflictMethod = typeof(RebindingManager).GetMethod("TryDetectConflict", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(nativeInputManagerField);
                Assert.NotNull(detectConflictMethod);
                nativeInputManagerField.SetValue(rebinding, runtime);
                object[] args = { current, 0, "Player", null, null, -1, false };

                bool conflict = (bool)detectConflictMethod.Invoke(rebinding, args);

                Assert.IsTrue(conflict);
                Assert.AreEqual("Jump", args[3]);
                Assert.AreSame(runtime.Player.FindAction("Jump", false), args[4]);
                Assert.AreEqual(0, args[5]);
                Assert.IsTrue((bool)args[6]);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                runtime.Dispose();
            }
        }

        [Test]
        public void ControlsJsonRejectsLoadedOverrideThatCollidesWithDefaultBinding()
        {
            string path = Path.Combine(Path.GetTempPath(), "h8_controls_default_conflict_" + Guid.NewGuid().ToString("N") + ".json");
            string tempPath = path + ".tmp";
            Guid interactId = new Guid("13320000-0000-0000-0000-0000000000D1");
            Guid jumpId = new Guid("13320000-0000-0000-0000-0000000000D2");
            MockNativeInputRuntime writerRuntime = new MockNativeInputRuntime();
            InputAction writerInteract = AddActionWithStableBinding(writerRuntime.Player, "Interact", "<Keyboard>/e", interactId);
            AddActionWithStableBinding(writerRuntime.Player, "Jump", "<Keyboard>/space", jumpId);

            try
            {
                writerInteract.ApplyBindingOverride(0, "<Keyboard>/enter");
                bool saved = ControlRemapper.TrySaveOverrides(writerRuntime, path, tempPath, out ControlRemapIoResult saveResult);
                Assert.IsTrue(saved);
                Assert.AreEqual(InputBindingTelemetryResult.Success, saveResult.ResultCode);
                string payload = File.ReadAllText(path);
                StringAssert.Contains("<Keyboard>/enter", payload);
                File.WriteAllText(path, payload.Replace("<Keyboard>/enter", "<Keyboard>/space"));

                MockNativeInputRuntime readerRuntime = new MockNativeInputRuntime();
                InputAction readerInteract = AddActionWithStableBinding(readerRuntime.Player, "Interact", "<Keyboard>/e", interactId);
                AddActionWithStableBinding(readerRuntime.Player, "Jump", "<Keyboard>/space", jumpId);

                try
                {
                    readerInteract.ApplyBindingOverride(0, "<Keyboard>/h");

                    bool loaded = ControlRemapper.TryLoadOverrides(readerRuntime, path, out ControlRemapIoResult loadResult);

                    Assert.IsFalse(loaded);
                    Assert.AreEqual(0, readerRuntime.ClearCount);
                    Assert.AreEqual("<Keyboard>/h", readerInteract.bindings[0].overridePath);
                    Assert.AreEqual(InputBindingTelemetryResult.UnsupportedPath, loadResult.ResultCode);
                    Assert.AreNotEqual(0u, loadResult.FaultFlags & InputBindingFaultFlags.UnsupportedPath);
                }
                finally
                {
                    readerRuntime.Dispose();
                }
            }
            finally
            {
                writerRuntime.Dispose();
                if (File.Exists(path))
                    File.Delete(path);
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [Test]
        public void ControlsJsonRejectsLoadedReservedEscapeWithoutClearingOverrides()
        {
            string path = Path.Combine(Path.GetTempPath(), "h8_controls_load_escape_" + Guid.NewGuid().ToString("N") + ".json");
            string tempPath = path + ".tmp";
            Guid interactId = new Guid("13320000-0000-0000-0000-0000000000D4");
            MockNativeInputRuntime writerRuntime = new MockNativeInputRuntime();
            InputAction writerInteract = AddActionWithStableBinding(writerRuntime.Player, "Interact", "<Keyboard>/e", interactId);

            try
            {
                writerInteract.ApplyBindingOverride(0, "<Keyboard>/enter");
                bool saved = ControlRemapper.TrySaveOverrides(writerRuntime, path, tempPath, out ControlRemapIoResult saveResult);
                Assert.IsTrue(saved);
                Assert.AreEqual(InputBindingTelemetryResult.Success, saveResult.ResultCode);
                string payload = File.ReadAllText(path);
                StringAssert.Contains("<Keyboard>/enter", payload);
                File.WriteAllText(path, payload.Replace("<Keyboard>/enter", "<Keyboard>/escape"));

                MockNativeInputRuntime readerRuntime = new MockNativeInputRuntime();
                InputAction readerInteract = AddActionWithStableBinding(readerRuntime.Player, "Interact", "<Keyboard>/e", interactId);

                try
                {
                    readerInteract.ApplyBindingOverride(0, "<Keyboard>/h");

                    bool loaded = ControlRemapper.TryLoadOverrides(readerRuntime, path, out ControlRemapIoResult loadResult);

                    Assert.IsFalse(loaded);
                    Assert.AreEqual(0, readerRuntime.ClearCount);
                    Assert.AreEqual("<Keyboard>/h", readerInteract.bindings[0].overridePath);
                    Assert.AreEqual(InputBindingTelemetryResult.UnsupportedPath, loadResult.ResultCode);
                    Assert.AreNotEqual(0u, loadResult.FaultFlags & InputBindingFaultFlags.UnsupportedPath);
                }
                finally
                {
                    readerRuntime.Dispose();
                }
            }
            finally
            {
                writerRuntime.Dispose();
                if (File.Exists(path))
                    File.Delete(path);
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [Test]
        public void ControlsJsonRejectsLoadedReservedTabWithoutClearingOverrides()
        {
            string path = Path.Combine(Path.GetTempPath(), "h8_controls_load_tab_" + Guid.NewGuid().ToString("N") + ".json");
            string tempPath = path + ".tmp";
            Guid pdaId = new Guid("13320000-0000-0000-0000-0000000000D7");
            MockNativeInputRuntime writerRuntime = new MockNativeInputRuntime();
            InputAction writerPda = AddActionWithStableBinding(writerRuntime.Player, "PDA", "<Keyboard>/p", pdaId);
            AddActionWithStableBinding(writerRuntime.UI, "TabNext", "<Keyboard>/tab", new Guid("13320000-0000-0000-0000-0000000000D8"));

            try
            {
                writerPda.ApplyBindingOverride(0, "<Keyboard>/r");
                bool saved = ControlRemapper.TrySaveOverrides(writerRuntime, path, tempPath, out ControlRemapIoResult saveResult);
                Assert.IsTrue(saved);
                Assert.AreEqual(InputBindingTelemetryResult.Success, saveResult.ResultCode);
                string payload = File.ReadAllText(path);
                StringAssert.Contains("<Keyboard>/r", payload);
                File.WriteAllText(path, payload.Replace("<Keyboard>/r", "<Keyboard>/tab"));

                MockNativeInputRuntime readerRuntime = new MockNativeInputRuntime();
                InputAction readerPda = AddActionWithStableBinding(readerRuntime.Player, "PDA", "<Keyboard>/p", pdaId);
                AddActionWithStableBinding(readerRuntime.UI, "TabNext", "<Keyboard>/tab", new Guid("13320000-0000-0000-0000-0000000000D9"));

                try
                {
                    readerPda.ApplyBindingOverride(0, "<Keyboard>/h");

                    bool loaded = ControlRemapper.TryLoadOverrides(readerRuntime, path, out ControlRemapIoResult loadResult);

                    Assert.IsFalse(loaded);
                    Assert.AreEqual(0, readerRuntime.ClearCount);
                    Assert.AreEqual("<Keyboard>/h", readerPda.bindings[0].overridePath);
                    Assert.AreEqual(InputBindingTelemetryResult.UnsupportedPath, loadResult.ResultCode);
                    Assert.AreNotEqual(0u, loadResult.FaultFlags & InputBindingFaultFlags.UnsupportedPath);
                }
                finally
                {
                    readerRuntime.Dispose();
                }
            }
            finally
            {
                writerRuntime.Dispose();
                if (File.Exists(path))
                    File.Delete(path);
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [Test]
        public void ControlsJsonRejectsRuntimeClearFailureWithoutLosingCurrentOverrides()
        {
            string path = Path.Combine(Path.GetTempPath(), "h8_controls_clear_fail_" + Guid.NewGuid().ToString("N") + ".json");
            string tempPath = path + ".tmp";
            Guid bindingId = new Guid("13320000-0000-0000-0000-0000000000CF");
            MockNativeInputRuntime writerRuntime = new MockNativeInputRuntime();
            InputAction writerAction = AddActionWithStableBinding(writerRuntime.Player, "Interact", "<Keyboard>/e", bindingId);

            try
            {
                writerAction.ApplyBindingOverride(0, "<Keyboard>/f");
                bool saved = ControlRemapper.TrySaveOverrides(writerRuntime, path, tempPath, out ControlRemapIoResult saveResult);
                Assert.IsTrue(saved);
                Assert.AreEqual(InputBindingTelemetryResult.Success, saveResult.ResultCode);

                MockNativeInputRuntime readerRuntime = new MockNativeInputRuntime();
                InputAction readerAction = AddActionWithStableBinding(readerRuntime.Player, "Interact", "<Keyboard>/e", bindingId);

                try
                {
                    readerAction.ApplyBindingOverride(0, "<Keyboard>/g");
                    readerRuntime.RejectClear = true;

                    bool loaded = ControlRemapper.TryLoadOverrides(readerRuntime, path, out ControlRemapIoResult loadResult);

                    Assert.IsFalse(loaded);
                    Assert.AreEqual(0, readerRuntime.ClearCount);
                    Assert.AreEqual("<Keyboard>/g", readerAction.bindings[0].overridePath);
                    Assert.AreEqual(InputBindingTelemetryResult.UnsupportedPath, loadResult.ResultCode);
                    Assert.AreNotEqual(0u, loadResult.FaultFlags & InputBindingFaultFlags.UnsupportedPath);
                }
                finally
                {
                    readerRuntime.Dispose();
                }
            }
            finally
            {
                writerRuntime.Dispose();
                if (File.Exists(path))
                    File.Delete(path);
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [Test]
        public void ControlsJsonReloadsDisabledOverrideAfterRuntimeRestart()
        {
            string path = Path.Combine(Path.GetTempPath(), "h8_controls_disabled_reload_" + Guid.NewGuid().ToString("N") + ".json");
            string tempPath = path + ".tmp";
            Guid bindingId = new Guid("13320000-0000-0000-0000-000000000002");
            MockNativeInputRuntime writerRuntime = new MockNativeInputRuntime();
            InputAction writerAction = AddActionWithStableBinding(writerRuntime.Player, "Interact", "<Keyboard>/e", bindingId);

            try
            {
                writerAction.ApplyBindingOverride(0, string.Empty);
                bool saved = ControlRemapper.TrySaveOverrides(writerRuntime, path, tempPath, out ControlRemapIoResult saveResult);
                Assert.IsTrue(saved);
                Assert.AreEqual(InputBindingTelemetryResult.Success, saveResult.ResultCode);
                Assert.AreEqual(1, saveResult.RecordCount);
                Assert.AreEqual(0, saveResult.PathBytes);
                StringAssert.Contains("\"path\":\"\"", File.ReadAllText(path));

                writerRuntime.Dispose();
                writerRuntime = null;

                MockNativeInputRuntime readerRuntime = new MockNativeInputRuntime();
                InputAction readerAction = AddActionWithStableBinding(readerRuntime.Player, "Interact", "<Keyboard>/e", bindingId);

                try
                {
                    bool loaded = ControlRemapper.TryLoadOverrides(readerRuntime, path, out ControlRemapIoResult loadResult);
                    Assert.IsTrue(loaded);
                    Assert.AreEqual(InputBindingTelemetryResult.Success, loadResult.ResultCode);
                    Assert.AreEqual(1, readerRuntime.ClearCount);
                    Assert.AreEqual(string.Empty, readerAction.bindings[0].overridePath);
                    Assert.AreEqual(string.Empty, readerAction.bindings[0].effectivePath);
                }
                finally
                {
                    readerRuntime.Dispose();
                }
            }
            finally
            {
                writerRuntime?.Dispose();
                if (File.Exists(path))
                    File.Delete(path);
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [Test]
        public void DisabledOverrideDoesNotFallbackToDefaultDisplayPath()
        {
            InputActionMap map = new InputActionMap("Player");
            InputAction action = map.AddAction("Interact", binding: "<Keyboard>/e");

            try
            {
                action.ApplyBindingOverride(0, string.Empty);
                char[] buffer = new char[16];

                Assert.IsFalse(InputManager.TryGetBindingDisplayStringSafe(action, 0, out string display));
                Assert.AreEqual(string.Empty, display);
                Assert.IsFalse(InputManager.TryWriteBindingDisplayStringSafe(action, 0, buffer, 0, out int charsWritten));
                Assert.AreEqual(0, charsWritten);
            }
            finally
            {
                map.Dispose();
            }
        }

        [Test]
        public void ControlsJsonRejectsMalformedPathWithoutClearingOverrides()
        {
            string path = Path.Combine(Path.GetTempPath(), "h8_controls_bad_path_" + Guid.NewGuid().ToString("N") + ".json");
            string tempPath = path + ".tmp";
            MockNativeInputRuntime runtime = new MockNativeInputRuntime();
            InputAction action = runtime.Player.AddAction("Interact", binding: "<Keyboard>/e");

            try
            {
                action.ApplyBindingOverride(0, "<Keyboard>/f");
                bool saved = ControlRemapper.TrySaveOverrides(runtime, path, tempPath, out ControlRemapIoResult saveResult);
                Assert.IsTrue(saved);
                Assert.AreEqual(InputBindingTelemetryResult.Success, saveResult.ResultCode);

                string json = File.ReadAllText(path);
                File.WriteAllText(path, json.Replace("<Keyboard>/f", "Keyboard/f"));
                runtime.ClearCount = 0;

                bool loaded = ControlRemapper.TryLoadOverrides(runtime, path, out ControlRemapIoResult loadResult);
                Assert.IsFalse(loaded);
                Assert.AreEqual(0, runtime.ClearCount);
                Assert.AreEqual(InputBindingTelemetryResult.UnsupportedPath, loadResult.ResultCode);
                Assert.AreNotEqual(0u, loadResult.FaultFlags & InputBindingFaultFlags.UnsupportedPath);
            }
            finally
            {
                runtime.Dispose();
                if (File.Exists(path))
                    File.Delete(path);
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [Test]
        public void ControlsJsonRejectsRootTrailingGarbageWithoutClearingOverrides()
        {
            string path = Path.Combine(Path.GetTempPath(), "h8_controls_tail_" + Guid.NewGuid().ToString("N") + ".json");
            MockNativeInputRuntime runtime = new MockNativeInputRuntime();
            InputAction action = runtime.Player.AddAction("Interact", binding: "<Keyboard>/e");

            try
            {
                action.ApplyBindingOverride(0, "<Keyboard>/f");
                File.WriteAllText(path, "{\"v\":1,\"bindings\":[]}garbage");

                bool loaded = ControlRemapper.TryLoadOverrides(runtime, path, out ControlRemapIoResult result);

                Assert.IsFalse(loaded);
                Assert.AreEqual(0, runtime.ClearCount);
                Assert.AreEqual("<Keyboard>/f", action.bindings[0].overridePath);
                Assert.AreEqual(InputBindingTelemetryResult.InvalidJson, result.ResultCode);
                Assert.AreNotEqual(0u, result.FaultFlags & InputBindingFaultFlags.InvalidSchema);
            }
            finally
            {
                runtime.Dispose();
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [Test]
        public void ControlsJsonRejectsNestedBindingsWithoutClearingOverrides()
        {
            string path = Path.Combine(Path.GetTempPath(), "h8_controls_nested_" + Guid.NewGuid().ToString("N") + ".json");
            MockNativeInputRuntime runtime = new MockNativeInputRuntime();
            InputAction action = runtime.Player.AddAction("Interact", binding: "<Keyboard>/e");

            try
            {
                action.ApplyBindingOverride(0, "<Keyboard>/f");
                File.WriteAllText(path, "{\"v\":1,\"outer\":{\"bindings\":[]}}");

                bool loaded = ControlRemapper.TryLoadOverrides(runtime, path, out ControlRemapIoResult result);

                Assert.IsFalse(loaded);
                Assert.AreEqual(0, runtime.ClearCount);
                Assert.AreEqual("<Keyboard>/f", action.bindings[0].overridePath);
                Assert.AreEqual(InputBindingTelemetryResult.InvalidJson, result.ResultCode);
                Assert.AreNotEqual(0u, result.FaultFlags & InputBindingFaultFlags.InvalidSchema);
            }
            finally
            {
                runtime.Dispose();
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [Test]
        public void ControlsJsonRejectsDuplicateRecordFieldsWithoutClearingOverrides()
        {
            string path = Path.Combine(Path.GetTempPath(), "h8_controls_duplicate_" + Guid.NewGuid().ToString("N") + ".json");
            string tempPath = path + ".tmp";
            MockNativeInputRuntime runtime = new MockNativeInputRuntime();
            InputAction action = runtime.Player.AddAction("Interact", binding: "<Keyboard>/e");

            try
            {
                action.ApplyBindingOverride(0, "<Keyboard>/f");
                Assert.IsTrue(ControlRemapper.TrySaveOverrides(runtime, path, tempPath, out ControlRemapIoResult saveResult));
                Assert.AreEqual(InputBindingTelemetryResult.Success, saveResult.ResultCode);

                string json = File.ReadAllText(path);
                File.WriteAllText(path, json.Replace("\"path\":\"<Keyboard>/f\"", "\"path\":\"<Keyboard>/f\",\"path\":\"<Keyboard>/g\""));
                runtime.ClearCount = 0;

                bool loaded = ControlRemapper.TryLoadOverrides(runtime, path, out ControlRemapIoResult loadResult);

                Assert.IsFalse(loaded);
                Assert.AreEqual(0, runtime.ClearCount);
                Assert.AreEqual("<Keyboard>/f", action.bindings[0].overridePath);
                Assert.AreEqual(InputBindingTelemetryResult.InvalidJson, loadResult.ResultCode);
                Assert.AreNotEqual(0u, loadResult.FaultFlags & InputBindingFaultFlags.InvalidSchema);
            }
            finally
            {
                runtime.Dispose();
                if (File.Exists(path))
                    File.Delete(path);
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [Test]
        public void ControlsJsonRejectsMissingBindingIdWithoutClearingOverrides()
        {
            string path = Path.Combine(Path.GetTempPath(), "h8_controls_missing_id_" + Guid.NewGuid().ToString("N") + ".json");
            string tempPath = path + ".tmp";
            MockNativeInputRuntime runtime = new MockNativeInputRuntime();
            InputAction action = runtime.Player.AddAction("Interact", binding: "<Keyboard>/e");

            try
            {
                action.ApplyBindingOverride(0, "<Keyboard>/f");
                Assert.IsTrue(ControlRemapper.TrySaveOverrides(runtime, path, tempPath, out ControlRemapIoResult saveResult));
                Assert.AreEqual(InputBindingTelemetryResult.Success, saveResult.ResultCode);

                string json = File.ReadAllText(path);
                int idStart = json.IndexOf(",\"id\":", StringComparison.Ordinal);
                int pathStart = json.IndexOf(",\"path\":", StringComparison.Ordinal);
                Assert.GreaterOrEqual(idStart, 0);
                Assert.Greater(pathStart, idStart);
                File.WriteAllText(path, json.Remove(idStart, pathStart - idStart));
                runtime.ClearCount = 0;

                bool loaded = ControlRemapper.TryLoadOverrides(runtime, path, out ControlRemapIoResult loadResult);

                Assert.IsFalse(loaded);
                Assert.AreEqual(0, runtime.ClearCount);
                Assert.AreEqual("<Keyboard>/f", action.bindings[0].overridePath);
                Assert.AreEqual(InputBindingTelemetryResult.InvalidJson, loadResult.ResultCode);
                Assert.AreNotEqual(0u, loadResult.FaultFlags & InputBindingFaultFlags.InvalidSchema);
            }
            finally
            {
                runtime.Dispose();
                if (File.Exists(path))
                    File.Delete(path);
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [Test]
        public void ControlsJsonRejectsZeroBindingIdWithoutClearingOverrides()
        {
            string path = Path.Combine(Path.GetTempPath(), "h8_controls_zero_id_" + Guid.NewGuid().ToString("N") + ".json");
            string tempPath = path + ".tmp";
            MockNativeInputRuntime runtime = new MockNativeInputRuntime();
            InputAction action = runtime.Player.AddAction("Interact", binding: "<Keyboard>/e");

            try
            {
                action.ApplyBindingOverride(0, "<Keyboard>/f");
                Assert.IsTrue(ControlRemapper.TrySaveOverrides(runtime, path, tempPath, out ControlRemapIoResult saveResult));
                Assert.AreEqual(InputBindingTelemetryResult.Success, saveResult.ResultCode);

                string json = File.ReadAllText(path);
                int idStart = json.IndexOf("\"id\":", StringComparison.Ordinal);
                Assert.GreaterOrEqual(idStart, 0);
                int valueStart = idStart + "\"id\":".Length;
                int valueEnd = json.IndexOf(',', valueStart);
                Assert.Greater(valueEnd, valueStart);
                File.WriteAllText(path, json.Substring(0, valueStart) + "0" + json.Substring(valueEnd));
                runtime.ClearCount = 0;

                bool loaded = ControlRemapper.TryLoadOverrides(runtime, path, out ControlRemapIoResult loadResult);

                Assert.IsFalse(loaded);
                Assert.AreEqual(0, runtime.ClearCount);
                Assert.AreEqual("<Keyboard>/f", action.bindings[0].overridePath);
                Assert.AreEqual(InputBindingTelemetryResult.InvalidJson, loadResult.ResultCode);
                Assert.AreNotEqual(0u, loadResult.FaultFlags & InputBindingFaultFlags.InvalidSchema);
            }
            finally
            {
                runtime.Dispose();
                if (File.Exists(path))
                    File.Delete(path);
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [Test]
        public void ControlsJsonAllowsFutureRootFieldsAfterBindings()
        {
            string path = Path.Combine(Path.GetTempPath(), "h8_controls_future_root_" + Guid.NewGuid().ToString("N") + ".json");
            string tempPath = path + ".tmp";
            MockNativeInputRuntime runtime = new MockNativeInputRuntime();
            InputAction action = runtime.Player.AddAction("Interact", binding: "<Keyboard>/e");

            try
            {
                action.ApplyBindingOverride(0, "<Keyboard>/f");
                Assert.IsTrue(ControlRemapper.TrySaveOverrides(runtime, path, tempPath, out ControlRemapIoResult saveResult));
                Assert.AreEqual(InputBindingTelemetryResult.Success, saveResult.ResultCode);

                string json = File.ReadAllText(path);
                File.WriteAllText(path, json.Replace("]}", "],\"schema\":{\"version\":2,\"tags\":[\"future\",3]}}"));
                runtime.ClearCount = 0;

                bool loaded = ControlRemapper.TryLoadOverrides(runtime, path, out ControlRemapIoResult loadResult);

                Assert.IsTrue(loaded);
                Assert.AreEqual(1, runtime.ClearCount);
                Assert.AreEqual("<Keyboard>/f", action.bindings[0].overridePath);
                Assert.AreEqual(InputBindingTelemetryResult.Success, loadResult.ResultCode);
            }
            finally
            {
                runtime.Dispose();
                if (File.Exists(path))
                    File.Delete(path);
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [Test]
        public void ControlsJsonRejectsInvalidUnknownPrimitiveBeforeBindings()
        {
            string path = Path.Combine(Path.GetTempPath(), "h8_controls_bad_unknown_" + Guid.NewGuid().ToString("N") + ".json");
            MockNativeInputRuntime runtime = new MockNativeInputRuntime();
            InputAction action = runtime.Player.AddAction("Interact", binding: "<Keyboard>/e");

            try
            {
                action.ApplyBindingOverride(0, "<Keyboard>/f");
                File.WriteAllText(path, "{\"v\":?,\"bindings\":[]}");

                bool loaded = ControlRemapper.TryLoadOverrides(runtime, path, out ControlRemapIoResult result);

                Assert.IsFalse(loaded);
                Assert.AreEqual(0, runtime.ClearCount);
                Assert.AreEqual("<Keyboard>/f", action.bindings[0].overridePath);
                Assert.AreEqual(InputBindingTelemetryResult.InvalidJson, result.ResultCode);
                Assert.AreNotEqual(0u, result.FaultFlags & InputBindingFaultFlags.InvalidSchema);
            }
            finally
            {
                runtime.Dispose();
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [Test]
        public void ControlsJsonRejectsInvalidFutureRootObjectAfterBindings()
        {
            string path = Path.Combine(Path.GetTempPath(), "h8_controls_bad_future_" + Guid.NewGuid().ToString("N") + ".json");
            string tempPath = path + ".tmp";
            MockNativeInputRuntime runtime = new MockNativeInputRuntime();
            InputAction action = runtime.Player.AddAction("Interact", binding: "<Keyboard>/e");

            try
            {
                action.ApplyBindingOverride(0, "<Keyboard>/f");
                Assert.IsTrue(ControlRemapper.TrySaveOverrides(runtime, path, tempPath, out ControlRemapIoResult saveResult));
                Assert.AreEqual(InputBindingTelemetryResult.Success, saveResult.ResultCode);

                string json = File.ReadAllText(path);
                File.WriteAllText(path, json.Replace("]}", "],\"schema\":{bad}}"));
                runtime.ClearCount = 0;

                bool loaded = ControlRemapper.TryLoadOverrides(runtime, path, out ControlRemapIoResult loadResult);

                Assert.IsFalse(loaded);
                Assert.AreEqual(0, runtime.ClearCount);
                Assert.AreEqual("<Keyboard>/f", action.bindings[0].overridePath);
                Assert.AreEqual(InputBindingTelemetryResult.InvalidJson, loadResult.ResultCode);
                Assert.AreNotEqual(0u, loadResult.FaultFlags & InputBindingFaultFlags.InvalidSchema);
            }
            finally
            {
                runtime.Dispose();
                if (File.Exists(path))
                    File.Delete(path);
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [Test]
        public void ControlsJsonSkipsUtf8FutureRootStringAfterBindings()
        {
            string path = Path.Combine(Path.GetTempPath(), "h8_controls_utf8_future_" + Guid.NewGuid().ToString("N") + ".json");
            string tempPath = path + ".tmp";
            MockNativeInputRuntime runtime = new MockNativeInputRuntime();
            InputAction action = runtime.Player.AddAction("Interact", binding: "<Keyboard>/e");

            try
            {
                action.ApplyBindingOverride(0, "<Keyboard>/f");
                Assert.IsTrue(ControlRemapper.TrySaveOverrides(runtime, path, tempPath, out ControlRemapIoResult saveResult));
                Assert.AreEqual(InputBindingTelemetryResult.Success, saveResult.ResultCode);

                string json = File.ReadAllText(path);
                string futureLabel = new string(new[]
                {
                    (char)0x0440,
                    (char)0x0435,
                    (char)0x0436,
                    (char)0x0438,
                    (char)0x043C
                });
                File.WriteAllBytes(path, Encoding.UTF8.GetBytes(json.Replace("]}", "],\"label\":\"" + futureLabel + "\"}")));
                action.RemoveBindingOverride(0);
                runtime.ClearCount = 0;

                bool loaded = ControlRemapper.TryLoadOverrides(runtime, path, out ControlRemapIoResult loadResult);

                Assert.IsTrue(loaded);
                Assert.AreEqual(1, runtime.ClearCount);
                Assert.AreEqual("<Keyboard>/f", action.bindings[0].overridePath);
                Assert.AreEqual(InputBindingTelemetryResult.Success, loadResult.ResultCode);
            }
            finally
            {
                runtime.Dispose();
                if (File.Exists(path))
                    File.Delete(path);
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [Test]
        public void ControlsJsonConcurrentLoadLeaseFailsClosedWithoutClearingOverrides()
        {
            string path = Path.Combine(Path.GetTempPath(), "h8_controls_concurrent_" + Guid.NewGuid().ToString("N") + ".json");
            string tempPath = path + ".tmp";
            MockNativeInputRuntime runtime = new MockNativeInputRuntime();
            InputAction action = runtime.Player.AddAction("Interact", binding: "<Keyboard>/e");
            FieldInfo leaseField = typeof(ControlRemapper).GetField("LoadRollbackLease", BindingFlags.Static | BindingFlags.NonPublic);

            try
            {
                Assert.IsNotNull(leaseField);
                action.ApplyBindingOverride(0, "<Keyboard>/f");
                Assert.IsTrue(ControlRemapper.TrySaveOverrides(runtime, path, tempPath, out ControlRemapIoResult saveResult));
                Assert.AreEqual(InputBindingTelemetryResult.Success, saveResult.ResultCode);

                runtime.ClearCount = 0;
                leaseField.SetValue(null, 1);

                bool loaded = ControlRemapper.TryLoadOverrides(runtime, path, out ControlRemapIoResult loadResult);

                Assert.IsFalse(loaded);
                Assert.AreEqual(0, runtime.ClearCount);
                Assert.AreEqual("<Keyboard>/f", action.bindings[0].overridePath);
                Assert.AreEqual(InputBindingTelemetryResult.ConcurrentOperation, loadResult.ResultCode);
                Assert.AreNotEqual(0u, loadResult.FaultFlags & InputBindingFaultFlags.ConcurrentOperation);
            }
            finally
            {
                leaseField?.SetValue(null, 0);
                runtime.Dispose();
                if (File.Exists(path))
                    File.Delete(path);
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [Test]
        public void ControlsJsonInvalidFilePathFailsClosedWithoutThrowing()
        {
            const string invalidPath = "h8_invalid\0controls.json";
            MockNativeInputRuntime runtime = new MockNativeInputRuntime();
            InputAction action = runtime.Player.AddAction("Interact", binding: "<Keyboard>/e");
            action.ApplyBindingOverride(0, "<Keyboard>/f");

            try
            {
                bool saved = true;
                ControlRemapIoResult saveResult = default;
                Assert.DoesNotThrow(() => saved = ControlRemapper.TrySaveOverrides(runtime, invalidPath, invalidPath + ".tmp", out saveResult));
                Assert.IsFalse(saved);
                Assert.AreEqual(InputBindingTelemetryResult.IoFailure, saveResult.ResultCode);
                Assert.AreNotEqual(0u, saveResult.FaultFlags & InputBindingFaultFlags.IoException);

                bool loaded = true;
                runtime.ClearCount = 0;
                ControlRemapIoResult loadResult = default;
                Assert.DoesNotThrow(() => loaded = ControlRemapper.TryLoadOverrides(runtime, invalidPath, out loadResult));
                Assert.IsFalse(loaded);
                Assert.AreEqual(0, runtime.ClearCount);
                Assert.IsTrue(
                    loadResult.ResultCode == InputBindingTelemetryResult.IoFailure ||
                    loadResult.ResultCode == InputBindingTelemetryResult.FileMissing);
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static InputAction AddActionWithStableBinding(InputActionMap map, string actionName, string path, Guid bindingId)
        {
            InputAction action = map.AddAction(actionName);
            action.AddBinding(new InputBinding { path = path, id = bindingId });
            return action;
        }

        private static bool ContainsTokensInOrder(string text, params string[] tokens)
        {
            if (text == null || tokens == null)
                return false;

            int cursor = 0;
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (string.IsNullOrEmpty(token))
                    return false;

                int index = text.IndexOf(token, cursor, StringComparison.Ordinal);
                if (index < 0)
                    return false;

                cursor = index + token.Length;
            }

            return true;
        }

        private static UserOptionsPersistence CreateUserOptionsPersistenceForDiskTest(
            string directory,
            string path,
            string tempPath,
            out GameObject root)
        {
            root = new GameObject("UserOptionsPersistenceDiskTest");
            UserOptionsPersistence persistence = root.AddComponent<UserOptionsPersistence>();
            SetPrivateInstanceField(persistence, "_serviceShutdownComplete", true);
            SetPrivateInstanceField(persistence, "_optionsDirectory", directory);
            SetPrivateInstanceField(persistence, "_optionsPath", path);
            SetPrivateInstanceField(persistence, "_optionsTempPath", tempPath);
            SetPrivateInstanceField(persistence, "_loaded", true);
            return persistence;
        }

        private static string CreateTempDirectory(string prefix)
        {
            string directory = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static void DestroyImmediateIfNotNull(GameObject root)
        {
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
        }

        private static void DeleteDirectoryBestEffort(string directory)
        {
            if (string.IsNullOrEmpty(directory))
                return;

            try
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (IOException)
            {
            }
        }

        private static byte[] BuildInvalidPortableOptionsHeader()
        {
            byte[] bytes = new byte[16];
            bytes[0] = (byte)'H';
            bytes[1] = (byte)'8';
            bytes[2] = (byte)'C';
            bytes[3] = (byte)'F';
            bytes[4] = byte.MaxValue;
            bytes[5] = byte.MaxValue;
            return bytes;
        }

        private static void SetPrivateInstanceField<TValue>(object target, string fieldName, TValue value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            field.SetValue(target, value);
        }

        private sealed class MockNativeInputRuntime : INativeInputManagerRuntime, IDisposable
        {
            public readonly InputActionMap Player = new InputActionMap("Player");
            public readonly InputActionMap UI = new InputActionMap("UI");
            public int ClearCount;
            public bool RejectClear;
            public int LegacySaveCalls;
            public int LegacyLoadCalls;

            public event Action<Vector2> OnNavigate { add { } remove { } }
            public event Action OnSubmit { add { } remove { } }
            public event Action OnCancel { add { } remove { } }
            public event Action OnTabNext { add { } remove { } }
            public event Action OnTabPrevious { add { } remove { } }
            public event Action OnPause { add { } remove { } }
            public event Action<byte> OnInputDisplayStyleCodeChanged { add { } remove { } }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            public event Action OnDebugToggleBlackBoxDashboard { add { } remove { } }
            public event Action OnDebugToggleEngineHealthOverlay { add { } remove { } }
#endif

            public ServiceHeartbeatState HeartbeatState => ServiceHeartbeatState.Ready;
            public bool IsServiceReady => true;
            public bool IsPlayerInputEnabled => true;
            public bool IsUIInputEnabled => true;
            public bool CanSwitchActionMaps => true;
            public bool IsSprinting => false;
            public Vector2 MoveInput => Vector2.zero;
            public Vector2 LookInput => Vector2.zero;
            public byte CurrentDisplayStyleCode => NativeInputDisplayStyle.KeyboardMouse;

            public void EnablePlayerInput() { }
            public void DisablePlayerInput() { }
            public void EnableUIInput() { }
            public void DisableUIInput() { }
            public void SwitchToPlayerInput() { }
            public void SwitchToUIInput() { }
            public void OnServiceShutdown() { }

            public InputAction GetAction(string actionName, string actionMap = "Player")
            {
                InputActionMap map = GetActionMap(actionMap);
                return map != null ? map.FindAction(actionName, false) : null;
            }

            public InputActionMap GetActionMap(string actionMap = "Player")
            {
                if (string.Equals(actionMap, "Player", StringComparison.OrdinalIgnoreCase))
                    return Player;
                if (string.Equals(actionMap, "UI", StringComparison.OrdinalIgnoreCase))
                    return UI;
                return null;
            }

            public int GetPreferredBindingIndex(string actionName, string actionMap = "Player")
            {
                return 0;
            }

            public bool TryReadUiPoint(out Vector2 point)
            {
                point = Vector2.zero;
                return false;
            }

            public bool TryReadUiScrollWheel(out Vector2 scrollDelta)
            {
                scrollDelta = Vector2.zero;
                return false;
            }

            public string GetBindingDisplayString(string actionName, string actionMap = "Player", int bindingIndex = 0)
            {
                return string.Empty;
            }

            public bool TryGetBindingDisplayString(InputAction action, int bindingIndex, out string display)
            {
                display = string.Empty;
                if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count)
                    return false;

                display = action.bindings[bindingIndex].effectivePath;
                return !string.IsNullOrEmpty(display);
            }

            public bool TryWriteBindingDisplayString(
                string actionName,
                string actionMap,
                int bindingIndex,
                char[] buffer,
                int bufferOffset,
                out int charsWritten)
            {
                return TryWriteBindingDisplayString(GetAction(actionName, actionMap), bindingIndex, buffer, bufferOffset, out charsWritten);
            }

            public bool TryWriteBindingDisplayString(
                InputAction action,
                int bindingIndex,
                char[] buffer,
                int bufferOffset,
                out int charsWritten)
            {
                charsWritten = 0;
                if (!TryGetBindingDisplayString(action, bindingIndex, out string display) ||
                    buffer == null ||
                    bufferOffset < 0 ||
                    bufferOffset >= buffer.Length)
                {
                    return false;
                }

                int count = Math.Min(display.Length, buffer.Length - bufferOffset);
                for (int i = 0; i < count; i++)
                    buffer[bufferOffset + i] = display[i];
                charsWritten = count;
                return count > 0;
            }

            public bool TryGetBindingMarkupForToken(string token, out string markup)
            {
                markup = null;
                return false;
            }

            public bool TryConfigureUiInputModule(InputSystemUIInputModule inputModule)
            {
                return false;
            }

            public string SaveBindingOverridesAsJson()
            {
                LegacySaveCalls++;
                return string.Empty;
            }

            public void LoadBindingOverridesFromJson(string json)
            {
                LegacyLoadCalls++;
            }

            public bool TryClearBindingOverrides()
            {
                if (RejectClear)
                    return false;

                ClearCount++;
                Player.RemoveAllBindingOverrides();
                UI.RemoveAllBindingOverrides();
                return true;
            }

            public void ClearBindingOverrides()
            {
                TryClearBindingOverrides();
            }

            public void Dispose()
            {
                Player.Dispose();
                UI.Dispose();
            }
        }
    }
}
