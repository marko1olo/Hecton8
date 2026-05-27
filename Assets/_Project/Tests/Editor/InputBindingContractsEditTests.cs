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

            Assert.AreEqual(ScalabilityTierProfiles.LowMx350, ScalabilityTierProfiles.Normalize(ScalabilityTierProfiles.LowMx350));
            Assert.AreEqual(ScalabilityTierProfiles.HighRtx, ScalabilityTierProfiles.Normalize(ScalabilityTierProfiles.LegacyHighRtx));
            Assert.AreEqual(ScalabilityTierProfiles.Middle, ScalabilityTierProfiles.Normalize(ScalabilityTierProfiles.Middle));
            Assert.AreEqual(ScalabilityTierProfiles.HighRtx, ScalabilityTierProfiles.Normalize(ScalabilityTierProfiles.HighRtx));
            Assert.AreEqual(ScalabilityTierProfiles.Ultra, ScalabilityTierProfiles.Normalize(ScalabilityTierProfiles.Ultra));
            Assert.AreEqual(ScalabilityTierProfiles.Ultra, ScalabilityTierProfiles.Normalize(byte.MaxValue));
            Assert.Less(
                ScalabilityTierProfiles.ToGlobalQualityWeight01(ScalabilityTierProfiles.LowMx350),
                ScalabilityTierProfiles.ToGlobalQualityWeight01(ScalabilityTierProfiles.Middle));
            Assert.Less(
                ScalabilityTierProfiles.ToGlobalQualityWeight01(ScalabilityTierProfiles.Middle),
                ScalabilityTierProfiles.ToGlobalQualityWeight01(ScalabilityTierProfiles.HighRtx));
            Assert.Less(
                ScalabilityTierProfiles.ToGlobalQualityWeight01(ScalabilityTierProfiles.HighRtx),
                ScalabilityTierProfiles.ToGlobalQualityWeight01(ScalabilityTierProfiles.Ultra));
            StringAssert.DoesNotContain("case LegacyHighRtx:", contractsSource);
            StringAssert.DoesNotContain("case ScalabilityTierProfiles.LegacyHighRtx:", platformSource);
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
            StringAssert.Contains("TryClaimRuntimeInstance", source);
            StringAssert.Contains("_duplicateInstance", source);
            StringAssert.Contains("_serviceShutdownComplete = true;", source);
            Assert.Less(source.IndexOf("if (!TryClaimRuntimeInstance())", StringComparison.Ordinal), source.IndexOf("TryColdBootstrapBuffers();", StringComparison.Ordinal));
        }

        [Test]
        public void BootstrapCreatesAccessibilityRuntimeOwner()
        {
            string sourcePath = Path.Combine("Assets", "_Project", "Scripts", "Bootstrap", "GameBootstrapper.cs");
            string source = File.ReadAllText(sourcePath);

            StringAssert.Contains("AccessibilitySettings accessibilitySettings = AccessibilitySettings.ActiveRuntimeInstance;", source);
            StringAssert.Contains("GameObject accessibilityRoot = new GameObject(\"[AccessibilitySettings]\")", source);
            StringAssert.Contains("accessibilityRoot.AddComponent<AccessibilitySettings>()", source);
            StringAssert.Contains("PersistRuntimeService(accessibilitySettings);", source);
            Assert.Less(source.IndexOf("rebindingManager.BindNativeInputManager(inputManager);", StringComparison.Ordinal), source.IndexOf("AccessibilitySettings accessibilitySettings", StringComparison.Ordinal));
            Assert.Less(source.IndexOf("PersistRuntimeService(accessibilitySettings);", StringComparison.Ordinal), source.IndexOf("ContextualPhysicalIkRuntime contextualIkRuntime", StringComparison.Ordinal));
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
            StringAssert.Contains("BinaryPayloadMagic", source);
            StringAssert.Contains("ApplyStagedOptionRecords", source);
            StringAssert.Contains("wasPortableContainer", source);
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
            StringAssert.Contains("if (!TrySaveToDisk())", source);
            Assert.Less(source.IndexOf("if (!TrySaveToDisk())", StringComparison.Ordinal), source.IndexOf("PlatformIntegrationBridge.ApplyScalabilityTier(normalizedTier)", StringComparison.Ordinal));
            Assert.Less(source.IndexOf("PlatformIntegrationBridge.ApplyScalabilityTier(normalizedTier)", StringComparison.Ordinal), source.IndexOf("PlatformIntegrationBridge.PublishScalabilityChanged(previousTier, normalizedTier)", StringComparison.Ordinal));
            StringAssert.Contains("get => _scalabilityTier;", source);
            StringAssert.Contains("public string OptionsPath => _optionsPath ?? string.Empty;", source);
            StringAssert.Contains("public bool LastSaveSucceeded { get; private set; }", source);
            StringAssert.Contains("public bool TrySave()", source);
            StringAssert.Contains("LastSaveSucceeded = TrySaveToDisk();", source);
            StringAssert.Contains("return LastSaveSucceeded;", source);
            StringAssert.Contains("EnsureOptionsStoragePaths();", source);
            StringAssert.DoesNotContain("OptionsPath => ResolveOptionsPath", source);
            StringAssert.DoesNotContain("private string ResolveOptionsPath", source);
            StringAssert.DoesNotContain("private string ResolveOptionsTempPath", source);
            StringAssert.Contains("ReadOnlySpan<char> token = json.AsSpan", source);
            StringAssert.DoesNotContain("json.Substring(tokenStart", source);
            StringAssert.Contains("return _loaded && !string.IsNullOrWhiteSpace(key)", source);
            StringAssert.Contains("DestroyDuplicateInstance", source);
            StringAssert.Contains("_serviceShutdownComplete = true;", source);
            Assert.Less(source.IndexOf("BootstrapRegistryBridge.TryResolve(BootstrapRegistryBridgeSlot.UserOptionsRuntime", StringComparison.Ordinal), source.IndexOf("LoadFromDisk();", StringComparison.Ordinal));
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
            StringAssert.Contains("if (tier == LegacyHighRtx)", inputServiceContractsSource);
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
            StringAssert.Contains("public bool LoadOverrides()", rebindSource);
            StringAssert.Contains("public bool ClearOverrides(bool clearSavedOverrides = true)", rebindSource);
            StringAssert.Contains("private bool DeleteOverridesFileIfExists()", rebindSource);
            StringAssert.Contains("private bool TryDeleteOverridesFile", rebindSource);
            StringAssert.Contains("RuntimeOverrideRollbackRecord[] _clearRollbackRecords", rebindSource);
            StringAssert.Contains("TryCaptureRuntimeOverrideRollback(inputManager, out int rollbackCount)", rebindSource);
            StringAssert.Contains("if (clearSavedOverrides && !DeleteOverridesFileIfExists())", rebindSource);
            StringAssert.Contains("TryRestoreRuntimeOverrideRollback(rollbackCount)", rebindSource);
            StringAssert.Contains("if (!TryDeleteOverridesFile(tempPath, \"Failed to delete temp binding overrides file.\"))\n                return false;\n\n            string path = GetOverridesFilePath();", rebindNormalized);
            StringAssert.Contains("bool TryClearBindingOverrides();", inputServiceContractsSource);
            StringAssert.Contains("public bool TryClearBindingOverrides()", inputManagerSource);
            StringAssert.Contains("if (_runtimeInputActionAsset == null && !EnsureInputActionsInitialized())", inputManagerSource);
            StringAssert.Contains("private static bool TryClearRuntimeBindingOverrides(INativeInputManagerRuntime inputManager)", rebindSource);
            StringAssert.Contains("if (!TryClearRuntimeBindingOverrides(inputManager))", rebindSource);
            StringAssert.DoesNotContain("inputManager.ClearBindingOverrides();", rebindSource);
            StringAssert.Contains("private static bool TryClearBindingOverrides(INativeInputManagerRuntime inputManager, ref ControlRemapIoResult result)", controlRemapperSource);
            StringAssert.Contains("if (!TryClearBindingOverrides(inputManager, ref result))", controlRemapperSource);
            StringAssert.Contains("catch (NotSupportedException)", rebindSource);
            StringAssert.Contains("TryDestroyDuplicateService", rebindSource);
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
            StringAssert.Contains("TryAcquireWriteLock(in ringHandle, SystemID.UI, out NativeArray<InputBindingTelemetryEntry> ring)", controlRemapperSource);
            StringAssert.Contains("UnsafeUtility.MemCpy(destination, source, byteCount);", controlRemapperSource);
            StringAssert.Contains("vault.ReleaseWriteLock(in ringHandle, SystemID.UI);", controlRemapperSource);
            int telemetryReleaseIndex = controlRemapperNormalized.IndexOf("vault.ReleaseWriteLock(in ringHandle, SystemID.UI);", StringComparison.Ordinal);
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
            StringAssert.Contains("private bool _ownsActiveRebind;", pausePanelSource);
            StringAssert.Contains("CancelOwnedRebindIfNeeded(rebinding);", pausePanelSource);
            StringAssert.Contains("if (!IsActive)\n            {\n                CancelOwnedRebindIfNeeded(_subscribedRebindingService);", pausePanelNormalized);
            StringAssert.Contains("serviceSlot != GlobalRegistryServiceSlot.InputBinding)\n            {\n                return;\n            }\n\n            CancelOwnedRebindIfNeeded(_subscribedRebindingService);\n            Unsubscribe();", pausePanelNormalized);
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
            StringAssert.DoesNotContain("RefreshRowBinding(_rows[i]);", pausePanelSource);
            StringAssert.Contains("string previousOverridePath = action.bindings[bindingIndex].overridePath", pausePanelSource);
            StringAssert.DoesNotContain("rebinding.SaveOverrides();", pausePanelSource);
            StringAssert.Contains("if (rebinding.SaveOverrides())", pausePanelSource);
            StringAssert.Contains("if (rebinding.LoadOverrides())", pausePanelSource);
            StringAssert.Contains("if (rebinding.ClearOverrides())", pausePanelSource);
            StringAssert.Contains("!rebinding.IsRebinding &&", pausePanelSource);
            StringAssert.DoesNotContain("RefreshAllBindings();\n            if (!IsControlsTabActive) return;", pdaControlsNormalized);
            StringAssert.Contains("if (!IsControlsTabActive) return;\n            RefreshAllBindings();", pdaControlsNormalized);
            StringAssert.Contains("StatusBindingsSaveFailed", pdaControlsSource);
            StringAssert.Contains("HandleRebindSaveFailed", pdaControlsSource);
            StringAssert.Contains("rebinding.OnRebindSaveFailed += _rebindSaveFailedAction", pdaControlsSource);
            StringAssert.DoesNotContain("OnOverridesSaveFailed += _overridesSaveFailedAction", pdaControlsSource);
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
            StringAssert.Contains("serviceSlot != GlobalRegistryServiceSlot.InputBinding)\n            {\n                return;\n            }\n\n            CancelOwnedRebindIfNeeded(_subscribedRebindingService);\n            Unsubscribe();", pdaControlsNormalized);
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
