using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class InputHapticPriority17DEditTests
    {
        [Test]
        public void HapticRequests_DoNotPromoteMicroVibrationAboveHullImpacts()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Core/InputDispatcher.cs");
            string normalized = Normalize(source);

            StringAssert.DoesNotContain("byte priority = request.Channel", source);
            StringAssert.DoesNotContain("request.Flags & HapticBlendAdditive", source);
            StringAssert.Contains("private const byte HapticPriorityMicro = 0;", source);
            StringAssert.Contains("private const byte HapticPriorityTool = 1;", source);
            StringAssert.Contains("private const byte HapticPriorityCollision = 2;", source);
            StringAssert.Contains("private const byte HapticPriorityCritical = 3;", source);
            StringAssert.Contains("ResolveHapticRequestPriority(in request)", source);
            StringAssert.Contains("request.Channel == HapticRequest.ChannelMicroVibration", source);
            StringAssert.Contains("request.Channel == HapticRequest.ChannelVehicleCritical", source);
            StringAssert.Contains("request.Channel == HapticRequest.ChannelLightThud", source);
            StringAssert.Contains("if (priority < weakestPriority)\n                    return;", normalized);
            StringAssert.Contains("if (priority == weakestPriority && commandMagnitude <= weakestMagnitude)\n                    return;", normalized);
        }

        [Test]
        public void HapticCommandDto_KeepsSixteenByteAbiWhilePackingPriority()
        {
            string dispatcher = ReadProjectFile("Assets/_Project/Scripts/Core/InputDispatcher.cs");
            string coreDto = ReadProjectFile("Assets/_Project/Scripts/Core/InputDeterminismDtos.cs");
            string deterministicDto = ReadProjectFile("Assets/_Project/Scripts/Input/Determinism/DeterministicInputContracts.cs");

            StringAssert.Contains("PackHapticCommandMotorMask(motorMask, priority, blendMode)", dispatcher);
            StringAssert.Contains("ExtractHapticCommandMotorMask(command.MotorMask)", dispatcher);
            StringAssert.Contains("ExtractHapticCommandPriority(command.MotorMask)", dispatcher);
            StringAssert.Contains("ExtractHapticCommandBlendMode(command.MotorMask)", dispatcher);
            StringAssert.Contains("encoded == 0u ? HapticPriorityTool : encoded - 1u", dispatcher);
            StringAssert.Contains("encoded == 0u ? HapticBlendAdditive : encoded - 1u", dispatcher);
            AssertHapticCommandDtoLayout(coreDto);
            AssertHapticCommandDtoLayout(deterministicDto);
        }

        [Test]
        public void SynthesizedHapticPulses_PreservePriorityFlagsWhenQueued()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Core/HectonInputRuntime_HapticSynth.cs");

            Assert.AreEqual(2, CountToken(source, "ResolveHapticPulsePriority("));
            Assert.AreEqual(2, CountToken(source, "ResolveHapticPulseBlendMode("));
            StringAssert.Contains("ResolveHapticPulsePriority(pulse.PriorityFlags)", source);
            StringAssert.Contains("ResolveHapticPulsePriority(synthesizedPulse.PriorityFlags)", source);
        }

        [Test]
        public void HapticSynthesisWriteLocks_ReleaseThroughAcquiredVault()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Core/HectonInputRuntime_HapticSynth.cs");

            StringAssert.Contains("out IDataVault lockVault", source);
            StringAssert.Contains("IDataVault vault = lockVault;", source);
            StringAssert.Contains("private static void ReleaseInputWriteBuffer<T>(IDataVault vault", source);
            StringAssert.Contains("out IDataVault telemetryVault", source);
            StringAssert.Contains("ReleaseInputWriteBuffer(telemetryVault,", source);
            StringAssert.Contains("out IDataVault profilesVault", source);
            StringAssert.Contains("ReleaseInputWriteBuffer(profilesVault,", source);
            StringAssert.Contains("out IDataVault tuningVault", source);
            StringAssert.Contains("ReleaseInputWriteBuffer(tuningVault,", source);
            StringAssert.Contains("out IDataVault finalPulseVault", source);
            StringAssert.Contains("ReleaseInputWriteBuffer(finalPulseVault,", source);
            StringAssert.Contains("out IDataVault writeVault", source);
            StringAssert.Contains("ReleaseInputWriteBuffer(writeVault,", source);
            StringAssert.DoesNotContain("ReleaseInputWriteBuffer(BufferID.ShinobuHapticSynthesis", source);
        }

        [Test]
        public void HapticPulsePriorityBits_DoNotBleedFromSourceHashes()
        {
            string signal = ReadProjectFile("Assets/_Project/Scripts/Core/Contracts/Signals/HapticPulseSignal.cs");
            string synthesis = ReadProjectFile("Assets/_Project/Scripts/Core/HapticSynthesisContracts.cs");
            string dispatcher = ReadProjectFile("Assets/_Project/Scripts/Core/InputDispatcher.cs");
            string questManager = ReadProjectFile("Assets/_Project/Scripts/Quest/QuestManager.cs");
            string questDagResolver = ReadProjectFile("Assets/_Project/Scripts/Quest/QuestDagResolverRuntime.cs");
            string toolKinematics = ReadProjectFile("Assets/_Project/Scripts/Tools/ToolKinematics/ToolKinematicsRuntime.cs");

            StringAssert.Contains("public const uint PriorityMask = PriorityCollision | PriorityExplosion | PriorityTool;", signal);
            StringAssert.Contains("public const int SourceHashShift = 3;", signal);
            StringAssert.Contains("public const uint SourceHashPayloadMask = 0x01FFFFFFu;", signal);
            StringAssert.Contains("public const uint FlagMask = FlagNanSanitized | FlagFaultDumpRequested;", signal);
            StringAssert.Contains("PackPriorityAndSourceHash(uint priorityFlags, uint sourceHash)", signal);
            StringAssert.Contains("ExtractPriorityFlags(uint priorityFlags)", signal);
            StringAssert.Contains("priorityFlags & (PriorityMask | FlagMask)", signal);
            StringAssert.Contains("HapticPulseSignal.PackPriorityAndSourceHash(HapticPulseSignal.PriorityTool, signal.ToolHash)", synthesis);
            StringAssert.Contains("HapticPulseSignal.PackPriorityAndSourceHash(priorityFlags, sourceHash)", synthesis);
            StringAssert.DoesNotContain("| (sourceHash & 0x00FFFFFFu)", synthesis);
            StringAssert.DoesNotContain("| (signal.ToolHash & 0x00FFFFFFu)", synthesis);
            StringAssert.Contains("HapticPulseSignal.ExtractPriorityFlags(priorityFlags)", dispatcher);
            StringAssert.Contains("HapticPulseSignal.PackPriorityAndSourceHash(\n                    HapticPulseSignal.PriorityTool,\n                    QuestDagRuntimeConstants.SignalSourceHash)", Normalize(questManager));
            StringAssert.Contains("HapticPulseSignal.PackPriorityAndSourceHash(\n                    HapticPulseSignal.PriorityTool,\n                    QuestDagRuntimeConstants.SignalSourceHash)", Normalize(questDagResolver));
            StringAssert.Contains("HapticPulseSignal.PackPriorityAndSourceHash(\n                    HapticPulseSignal.PriorityTool,\n                    heat.ToolHash)", Normalize(toolKinematics));
            StringAssert.DoesNotContain("PriorityTool | (QuestDagRuntimeConstants.SignalSourceHash & 0x00FFFFFFu)", questManager);
            StringAssert.DoesNotContain("PriorityTool | (QuestDagRuntimeConstants.SignalSourceHash & 0x00FFFFFFu)", questDagResolver);
            StringAssert.DoesNotContain("PriorityTool | (heat.ToolHash & 0x00FFFFFFu)", toolKinematics);
        }

        [Test]
        public void RootInputProfile_ProvidesStickAndHapticTuningForDeviceSweep()
        {
            string profile = ReadProjectFile("input_profiles.csv");
            string dispatcher = ReadProjectFile("Assets/_Project/Scripts/Core/InputDispatcher.cs");

            StringAssert.Contains("_inputProfileCsvPath = Path.Combine(projectRoot, \"input_profiles.csv\")", dispatcher);
            StringAssert.Contains("inner_deadzone,0.14", profile);
            StringAssert.Contains("outer_deadzone,0.96", profile);
            StringAssert.Contains("move_exponent,1.55", profile);
            StringAssert.Contains("mouse_acceleration,0.06", profile);
            StringAssert.Contains("haptic_thermal_amplitude_scale,0.62", profile);
            StringAssert.Contains("haptic_dispatch_interval_seconds,0.0333333", profile);
            StringAssert.Contains("mock_collision,0", profile);
        }

        [Test]
        public void RootInputProfile_StagesOutsideDataVaultMutationGuard()
        {
            string dispatcher = Normalize(ReadProjectFile("Assets/_Project/Scripts/Core/InputDispatcher.cs"));
            int methodIndex = dispatcher.IndexOf("private bool ApplyStagedInputProfileCsvToVault()", StringComparison.Ordinal);
            Assert.GreaterOrEqual(methodIndex, 0, "ApplyStagedInputProfileCsvToVault");

            int firstStageLockIndex = dispatcher.IndexOf("lock (_inputProfileCsvStageGate)", methodIndex, StringComparison.Ordinal);
            int guardIndex = dispatcher.IndexOf("if (!TryAcquireInputMutationGuard())", methodIndex, StringComparison.Ordinal);
            int releaseIndex = dispatcher.IndexOf("ReleaseInputMutationGuard();", guardIndex, StringComparison.Ordinal);
            int secondStageLockIndex = dispatcher.IndexOf("lock (_inputProfileCsvStageGate)", releaseIndex, StringComparison.Ordinal);

            Assert.Greater(firstStageLockIndex, methodIndex);
            Assert.Greater(guardIndex, firstStageLockIndex);
            Assert.Greater(releaseIndex, guardIndex);
            Assert.Greater(secondStageLockIndex, releaseIndex);
            string guardedWindow = dispatcher.Substring(guardIndex, releaseIndex - guardIndex);
            StringAssert.DoesNotContain("lock (_inputProfileCsvStageGate)", guardedWindow);
            StringAssert.Contains("profiles[0] = stagedProfile;", guardedWindow);
        }

        [Test]
        public void RootInputProfileCsvWatcher_UsesFailClosedNoThrowLifecycle()
        {
            string dispatcher = Normalize(ReadProjectFile("Assets/_Project/Scripts/Core/InputDispatcher.cs"));

            StringAssert.Contains("FileSystemWatcher watcher = TryCreateInputProfileCsvWatcher(projectRoot);", dispatcher);
            StringAssert.Contains("if (watcher == null)\n            {\n                CrashTelemetryBuffer.ReportBlackBoxExportFailure();\n                return;\n            }", dispatcher);
            StringAssert.Contains("_inputProfileCsvWatcher = null;\n            StopInputProfileCsvWatcherNoThrow(watcher);", dispatcher);
            StringAssert.Contains("private FileSystemWatcher TryCreateInputProfileCsvWatcher(string projectRoot)", dispatcher);
            StringAssert.Contains("watcher.EnableRaisingEvents = true;\n                return watcher;", dispatcher);
            StringAssert.Contains("catch (Exception)\n            {\n                return null;\n            }", dispatcher);
            StringAssert.Contains("private void StopInputProfileCsvWatcherNoThrow(FileSystemWatcher watcher)", dispatcher);
            StringAssert.Contains("watcher.EnableRaisingEvents = false;", dispatcher);
            StringAssert.Contains("watcher.Changed -= HandleInputProfileCsvChanged;", dispatcher);
            StringAssert.Contains("watcher.Dispose();", dispatcher);
        }

        [Test]
        public void InputPublishAndBlackBoxFaultDump_RunAfterMutationGuardRelease()
        {
            string dispatcher = Normalize(ReadProjectFile("Assets/_Project/Scripts/Core/InputDispatcher.cs"));
            int publishIndex = dispatcher.IndexOf("private void PublishDeterministicInputState(uint currentFrame)", StringComparison.Ordinal);
            Assert.GreaterOrEqual(publishIndex, 0, "PublishDeterministicInputState");

            int guardIndex = dispatcher.IndexOf("if (!TryAcquireInputMutationGuard())", publishIndex, StringComparison.Ordinal);
            int releaseIndex = dispatcher.IndexOf("ReleaseInputMutationGuard();", guardIndex, StringComparison.Ordinal);
            int afterReleaseSignalIndex = dispatcher.IndexOf("SignalBus<InputStateSignal>.TryPushTracked(in signal", releaseIndex, StringComparison.Ordinal);
            int afterReleaseDiscreteIndex = dispatcher.IndexOf("PublishDiscreteInputSignals(discreteCurrentButtonMask, discretePreviousButtonMask);", releaseIndex, StringComparison.Ordinal);
            int afterReleaseCrashIndex = dispatcher.IndexOf("CrashTelemetryBuffer.ReportDeterministicInputFrame(", releaseIndex, StringComparison.Ordinal);
            int afterReleaseDumpIndex = dispatcher.IndexOf("if (dumpDeterministicBlackBox)\n                DumpDeterministicInputBlackBox();", releaseIndex, StringComparison.Ordinal);
            Assert.Greater(guardIndex, publishIndex);
            Assert.Greater(releaseIndex, guardIndex);
            Assert.Greater(afterReleaseSignalIndex, releaseIndex);
            Assert.Greater(afterReleaseDiscreteIndex, releaseIndex);
            Assert.Greater(afterReleaseCrashIndex, releaseIndex);
            Assert.Greater(afterReleaseDumpIndex, releaseIndex);

            string guardedWindow = dispatcher.Substring(guardIndex, releaseIndex - guardIndex);
            StringAssert.DoesNotContain("SignalBus<InputStateSignal>.TryPushTracked", guardedWindow);
            StringAssert.DoesNotContain("PublishDiscreteInputSignals(", guardedWindow);
            StringAssert.DoesNotContain("CrashTelemetryBuffer.ReportDeterministicInputFrame", guardedWindow);
            StringAssert.DoesNotContain("DumpDeterministicInputBlackBox();", guardedWindow);
            StringAssert.Contains("dumpDeterministicBlackBox = WriteDeterministicInputBlackBox", guardedWindow);
            StringAssert.Contains("out uint packedAxes", dispatcher);
            StringAssert.Contains("out bool recordedFrame", dispatcher);
            StringAssert.Contains("TryReadInputBuffer(in _inputTelemetryHandle, InputBlackBoxCapacity, out NativeArray<InputTelemetryEntryDTO>.ReadOnly telemetry)", dispatcher);
            StringAssert.Contains("telemetry.GetUnsafeReadOnlyPtr()", dispatcher);
        }

        [Test]
        public void PowerSaveMute_SuppressesHapticIngressBeforeDeviceDispatch()
        {
            string dispatcher = Normalize(ReadProjectFile("Assets/_Project/Scripts/Core/InputDispatcher.cs"));
            string synthesis = Normalize(ReadProjectFile("Assets/_Project/Scripts/Core/HectonInputRuntime_HapticSynth.cs"));

            int drainIndex = dispatcher.IndexOf("private void DrainToolHaptics(float deltaTime)", StringComparison.Ordinal);
            Assert.GreaterOrEqual(drainIndex, 0, "DrainToolHaptics");
            int muteIndex = dispatcher.IndexOf("if (ToolHapticsRuntime.PowerSaveMuteActive)", drainIndex, StringComparison.Ordinal);
            int synthIndex = dispatcher.IndexOf("if (!IsHapticSynthesisDispatcherRouteRegistered())", drainIndex, StringComparison.Ordinal);
            int requestIndex = dispatcher.IndexOf("while (SignalBus<HapticRequest>.TryConsumeFrame", drainIndex, StringComparison.Ordinal);
            Assert.Greater(muteIndex, drainIndex, "power-save mute gate must be inside DrainToolHaptics.");
            Assert.Greater(synthIndex, muteIndex, "power-save mute must stop fallback synthesis before it queues DTO commands.");
            Assert.Greater(requestIndex, muteIndex, "power-save mute must drain haptic requests before normal request insertion.");

            string muteWindow = dispatcher.Substring(muteIndex, Math.Min(512, dispatcher.Length - muteIndex));
            StringAssert.Contains("DrainSuppressedHapticRequests();", muteWindow);
            StringAssert.Contains("ClearVaultBuffer(ref _hapticCommandDtoHandle);", muteWindow);
            StringAssert.Contains("_lastHapticCommandsActive = 0;", muteWindow);
            StringAssert.Contains("_hapticDispatchAccumulator = 0f;", muteWindow);
            StringAssert.Contains("QueueHapticOutput(schemeHash, 0f, 0f);", muteWindow);

            StringAssert.Contains("using Hecton8.Tools;", synthesis);
            Assert.AreEqual(3, CountToken(synthesis, "ToolHapticsRuntime.PowerSaveMuteActive"));
            StringAssert.Contains("if (ToolHapticsRuntime.PowerSaveMuteActive)\n            {\n                _hapticSynthesisAccumulator = 0f;\n                return dependsOn;\n            }", synthesis);
            StringAssert.Contains("if (ToolHapticsRuntime.PowerSaveMuteActive)\n                return;", synthesis);
            StringAssert.Contains("if (ToolHapticsRuntime.PowerSaveMuteActive)\n            {\n                _hapticSynthesisAccumulator = 0f;\n                return;\n            }", synthesis);
        }

        [Test]
        public void UiNavigation_HasKeyboardAndGamepadRoutesWithoutMouseDependency()
        {
            string actions = ReadProjectFile("Assets/InputSystem_Actions.inputactions");
            string runtimeActions = ReadProjectFile("Assets/Resources/HectonRuntimeInputActions.inputactions");
            string manager = ReadProjectFile("Assets/_Project/Scripts/Input/InputManager.cs");

            StringAssert.Contains("\"name\": \"Navigate\"", actions);
            StringAssert.Contains("\"path\": \"<Gamepad>/dpad\"", actions);
            StringAssert.Contains("\"path\": \"<Gamepad>/leftStick/up\"", actions);
            StringAssert.Contains("\"path\": \"<Gamepad>/rightStick/up\"", actions);
            StringAssert.Contains("\"processors\": \"stickDeadzone(min=0.14,max=0.96)\"", actions);
            StringAssert.Contains("\"processors\": \"axisDeadzone(min=0.14,max=0.96)\"", actions);
            StringAssert.Contains("\"path\": \"<Gamepad>/leftStick\", \"interactions\": \"\", \"processors\": \"stickDeadzone(min=0.14,max=0.96)\"", runtimeActions);
            StringAssert.Contains("\"path\": \"<Gamepad>/rightStick\", \"interactions\": \"\", \"processors\": \"stickDeadzone(min=0.14,max=0.96)\"", runtimeActions);
            StringAssert.Contains("\"path\": \"<Gamepad>/leftStick\", \"interactions\": \"\", \"processors\": \"stickDeadzone(min=0.14,max=0.96)\", \"groups\": \"Gamepad\", \"action\": \"Navigate\"", runtimeActions);
            StringAssert.Contains("\"path\": \"<Gamepad>/dpad/up\", \"interactions\": \"\", \"processors\": \"\", \"groups\": \"Gamepad\", \"action\": \"PDA\"", runtimeActions);
            StringAssert.DoesNotContain("\"path\": \"<Gamepad>/start\", \"interactions\": \"\", \"processors\": \"\", \"groups\": \"Gamepad\", \"action\": \"PDA\"", runtimeActions);
            StringAssert.Contains("\"path\": \"<Keyboard>/p\", \"interactions\": \"\", \"processors\": \"\", \"groups\": \"Keyboard&Mouse\", \"action\": \"PDA\"", runtimeActions);
            StringAssert.DoesNotContain("\"path\": \"<Keyboard>/tab\", \"interactions\": \"\", \"processors\": \"\", \"groups\": \"Keyboard&Mouse\", \"action\": \"PDA\"", runtimeActions);
            StringAssert.Contains("\"path\": \"<XRController>{LeftHand}/secondaryButton\", \"interactions\": \"\", \"processors\": \"\", \"groups\": \"XR_Touch\", \"action\": \"PDA\"", runtimeActions);
            StringAssert.Contains("\"path\": \"<XRController>{LeftHand}/menuButton\", \"interactions\": \"\", \"processors\": \"\", \"groups\": \"XR_Touch\", \"action\": \"Pause\"", runtimeActions);
            StringAssert.DoesNotContain("\"path\": \"<XRController>{LeftHand}/menuButton\", \"interactions\": \"\", \"processors\": \"\", \"groups\": \"XR_Touch\", \"action\": \"PDA\"", runtimeActions);
            StringAssert.Contains("\"path\": \"<Keyboard>/tab\", \"interactions\": \"\", \"processors\": \"\", \"groups\": \"Keyboard&Mouse\", \"action\": \"TabNext\"", runtimeActions);
            StringAssert.DoesNotContain("\"path\": \"<Keyboard>/tab\", \"interactions\": \"\", \"processors\": \"\", \"groups\": \"Keyboard&Mouse\", \"action\": \"Cancel\"", runtimeActions);
            StringAssert.Contains("\"path\": \"<Keyboard>/upArrow\"", actions);
            StringAssert.Contains("\"path\": \"*/{Submit}\"", actions);
            StringAssert.Contains("\"path\": \"*/{Cancel}\"", actions);
            StringAssert.Contains("private const string GamepadStickDeadzoneProcessor = \"stickDeadzone(min=0.14,max=0.96)\";", manager);
            StringAssert.Contains("private const string PlayerPdaKeyboardBindingPath = \"<Keyboard>/p\";", manager);
            StringAssert.Contains("private const string LegacyPlayerPdaKeyboardBindingPath = \"<Keyboard>/tab\";", manager);
            StringAssert.Contains("EnsurePlayerKeyboardBindings(playerActionMap)", manager);
            StringAssert.Contains("EnsurePlayerGamepadBindings(playerActionMap)", manager);
            StringAssert.Contains("ReplaceBindingPathIfPresent(pdaAction, LegacyPlayerPdaKeyboardBindingPath, PlayerPdaKeyboardBindingPath)", manager);
            StringAssert.Contains("AddBindingIfMissing(pdaAction, PlayerPdaKeyboardBindingPath)", manager);
            StringAssert.Contains("AddBindingIfMissing(playerActionMap.FindAction(\"Movement\"), \"<Gamepad>/leftStick\", GamepadStickDeadzoneProcessor)", manager);
            StringAssert.Contains("AddBindingIfMissing(playerActionMap.FindAction(\"Look\"), \"<Gamepad>/rightStick\", GamepadStickDeadzoneProcessor)", manager);
            StringAssert.Contains("AddBindingIfMissing(playerActionMap.FindAction(\"PDA\"), \"<Gamepad>/dpad/up\")", manager);
            StringAssert.DoesNotContain("AddBindingIfMissing(playerActionMap.FindAction(\"PDA\"), \"<Gamepad>/start\")", manager);
            StringAssert.Contains("AddBindingIfMissing(playerActionMap.FindAction(\"PrimaryAction\"), \"<Gamepad>/rightTrigger\")", manager);
            StringAssert.Contains("AddBindingIfMissing(playerActionMap.FindAction(\"SecondaryAction\"), \"<Gamepad>/leftTrigger\")", manager);
            StringAssert.Contains("AddBindingIfMissing(navigateAction, \"<Gamepad>/dpad\")", manager);
            StringAssert.Contains("AddBindingIfMissing(navigateAction, \"<Gamepad>/leftStick\", GamepadStickDeadzoneProcessor)", manager);
            StringAssert.Contains("binding.WithProcessor(processor)", manager);
            StringAssert.Contains("EnsureBindingProcessor(action, bindingIndex, processor)", manager);
            StringAssert.Contains("InputBinding binding = action.bindings[bindingIndex]", manager);
            StringAssert.Contains("binding.processors = processor", manager);
            StringAssert.Contains("action.ChangeBinding(bindingIndex).To(binding)", manager);
            StringAssert.Contains("AddBindingIfMissing(submitAction, \"<Gamepad>/buttonSouth\")", manager);
            StringAssert.Contains("AddBindingIfMissing(cancelAction, \"<Gamepad>/buttonEast\")", manager);
            StringAssert.Contains("AddBindingIfMissing(cancelAction, \"<Gamepad>/start\")", manager);
            StringAssert.DoesNotContain("AddBindingIfMissing(cancelAction, \"<Keyboard>/tab\")", manager);
            StringAssert.Contains("AddBindingIfMissing(tabNextAction, \"<Keyboard>/tab\")", manager);
            StringAssert.Contains("AddBindingIfMissing(tabNextAction, \"<Gamepad>/rightShoulder\")", manager);
            StringAssert.Contains("AddBindingIfMissing(tabPreviousAction, \"<Gamepad>/leftShoulder\")", manager);
        }

        [Test]
        public void RebindingPersistence_CannotStealGamepadStartFromPause()
        {
            string rebinding = ReadProjectFile("Assets/_Project/Scripts/Core/RebindingManager.cs");
            string remapper = ReadProjectFile("Assets/_Project/Scripts/Input/ControlRemapper.cs");

            StringAssert.Contains("private const string DefaultGamepadCancelPath = \"<Gamepad>/start\";", rebinding);
            StringAssert.Contains("ShouldReserveGamepadStart(actionName, actionMap)", rebinding);
            StringAssert.Contains("_activeRebind.WithControlsExcluding(DefaultGamepadCancelPath)", rebinding);
            StringAssert.Contains("IsProtectedGamepadStartOverride(actionName, actionMap, action, bindingIndex)", rebinding);
            StringAssert.Contains("Rebind rejected because Gamepad Start is reserved for Pause.", rebinding);
            StringAssert.Contains("private const string GamepadStartPath = \"<Gamepad>/start\";", remapper);
            StringAssert.Contains("private const string XInputStartPath = \"<XInputController>/start\";", remapper);
            StringAssert.Contains("private const string DualShockStartPath = \"<DualShockGamepad>/start\";", remapper);
            StringAssert.Contains("private const string DualSenseStartPath = \"<DualSenseGamepadHID>/start\";", remapper);
            StringAssert.Contains("private const string SteamDeckStartPath = \"<SteamDeckGamepad>/start\";", remapper);
            StringAssert.Contains("IsProtectedPlayerStartOverride(in record, pathUtf8)", remapper);
            StringAssert.Contains("HasLoadBindingPathConflict(inputManager, records, recordCount, bytesPtr)", remapper);
            StringAssert.Contains("record.ActionNameHash != HashString32(PauseActionName)", remapper);
            StringAssert.Contains("IsGamepadStartPath(pathUtf8)", remapper);
            StringAssert.Contains("StringMatchesAscii(DualSenseStartPath, pathUtf8)", remapper);
            StringAssert.Contains("string.Equals(path, DualSenseStartPath, StringComparison.OrdinalIgnoreCase)", rebinding);
        }

        [Test]
        public void PhysicalHandInteraction_UsesBoundedShellOverlapAndClosestGrabTarget()
        {
            string controller = ReadProjectFile("Assets/_Project/Scripts/Interaction/PhysicalHandController.cs");
            string registry = ReadProjectFile("Assets/_Project/Scripts/Interaction/PhysicalHandReceiverRegistry.cs");

            StringAssert.Contains("Physics.OverlapSphereNonAlloc(", controller);
            StringAssert.Contains("_suitOverlapResults", controller);
            StringAssert.Contains("TryEnqueueSuitCollisionHaptic(strongestPenetration)", controller);
            StringAssert.Contains("ResolveSuitCollisionHapticScale(pressure01)", controller);
            StringAssert.Contains("ToolHapticsRuntime.TryEnqueueCommand(", controller);
            StringAssert.Contains("CriticalHapticBlendMode", controller);
            StringAssert.Contains("activeCollider.ClosestPoint(_runtimeGripPoint.position)", controller);
            StringAssert.Contains("BendAngle = bendAngle", controller);
            StringAssert.DoesNotContain("BendAngle = 1f", controller);
            StringAssert.Contains("for (int i = 0; i < MaxReceivers; i++)", registry);
            StringAssert.Contains("farthestIndex", registry);
            StringAssert.Contains("results[farthestIndex] = collider", registry);
        }

        [Test]
        public void SteamDeckPal_UsesDeltaTimeLowPassAndRadialTrackpadDeadzone()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Core/SteamDeckInputPal.cs");

            StringAssert.DoesNotContain("GyroEwmaAlpha", source);
            StringAssert.Contains("private const float GyroLowPassCutoffHz = 12f;", source);
            StringAssert.Contains("private const float GyroIdleDecayCutoffHz = 18f;", source);
            StringAssert.Contains("ResolveLowPassAlpha(safeDeltaTime, GyroLowPassCutoffHz)", source);
            StringAssert.Contains("ResolveLowPassAlpha(safeDeltaTime, GyroIdleDecayCutoffHz)", source);
            StringAssert.Contains("1f - math.exp(-TwoPi * safeCutoff * safeDeltaTime)", source);
            StringAssert.Contains("TryApplyRadialDeadzone(left, out Vector2 filteredLeft)", source);
            StringAssert.Contains("TryApplyRadialDeadzone(right, out Vector2 filteredRight)", source);
            StringAssert.Contains("normalized = math.saturate((length - TrackpadDeadzone)", source);
        }

        private static void AssertHapticCommandDtoLayout(string source)
        {
            int structIndex = source.IndexOf("public struct HapticCommandDTO", StringComparison.Ordinal);
            Assert.GreaterOrEqual(structIndex, 0, "HapticCommandDTO struct");
            int layoutIndex = source.LastIndexOf("[StructLayout", structIndex, StringComparison.Ordinal);
            Assert.GreaterOrEqual(layoutIndex, 0, "HapticCommandDTO layout attribute");
            string window = source.Substring(layoutIndex, Math.Min(512, source.Length - layoutIndex));

            bool literalSize = window.Contains("Size = 16", StringComparison.Ordinal);
            bool contractSize = window.Contains("Size = DeterministicInputContractLayout.HapticCommandStrideBytes", StringComparison.Ordinal) &&
                source.Contains("public const int HapticCommandStrideBytes = 16;", StringComparison.Ordinal);
            Assert.IsTrue(literalSize || contractSize, "HapticCommandDTO must remain 16 bytes.");
            StringAssert.Contains("[FieldOffset(0)] public float LowFreqIntensity;", window);
            StringAssert.Contains("[FieldOffset(4)] public float HighFreqIntensity;", window);
            StringAssert.Contains("[FieldOffset(8)] public float DecayRate;", window);
            StringAssert.Contains("[FieldOffset(12)] public uint MotorMask;", window);
        }

        private static string ProjectRoot
        {
            get
            {
                string root = Directory.GetParent(Application.dataPath)?.FullName;
                if (string.IsNullOrEmpty(root))
                    throw new InvalidOperationException("Project root could not be resolved.");

                return root;
            }
        }

        private static string ReadProjectFile(string relativePath)
        {
            return File.ReadAllText(Path.Combine(ProjectRoot, relativePath));
        }

        private static string Normalize(string source)
        {
            return source.Replace("\r\n", "\n");
        }

        private static int CountToken(string source, string token)
        {
            int count = 0;
            int index = 0;
            while (index < source.Length)
            {
                int found = source.IndexOf(token, index, StringComparison.Ordinal);
                if (found < 0)
                    return count;

                count++;
                index = found + token.Length;
            }

            return count;
        }
    }
}
