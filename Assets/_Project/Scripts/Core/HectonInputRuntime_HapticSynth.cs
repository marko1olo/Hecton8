using System;
using System.Diagnostics;
using System.IO;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core
{
    public sealed unsafe partial class InputDispatcher
    {
        private static int s_x001HectonInputRuntimeHapticSynthSignalPushDropCount;
#if UNITY_EDITOR
        private const string HapticProfilesFileName = "haptic_response_profiles.csv";
#endif
        private const string HapticFaultDumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_353.bin";

        private VaultGenerationHandle<HapticPulseSignal> _hapticSynthesisPulsesHandle;
        private VaultGenerationHandle<HapticPulseSignal> _hapticSynthesisFinalPulseHandle;
        private VaultGenerationHandle<HapticPhysicalImpulseDTO> _hapticSynthesisMockImpulsesHandle;
        private VaultGenerationHandle<HapticTelemetryEntry> _hapticSynthesisTelemetryRingHandle;
        private VaultGenerationHandle<HapticProfileDTO> _hapticSynthesisProfilesHandle;
        private VaultGenerationHandle<HapticTuningDTO> _hapticSynthesisTuningHandle;
#if UNITY_EDITOR
        private VaultGenerationHandle<byte> _hapticSynthesisCsvScratchHandle;
#endif
        private float _hapticSynthesisAccumulator;
        private int _hapticSynthesisTelemetryCursor;
        private int _lastHapticSynthesisFaultDumpFrame = -1;
        private HapticSynthesisSimulationSystem _hapticSynthesisSimulationSystem;
        private HapticSynthesisPostSimulationSystem _hapticSynthesisPostSimulationSystem;
        private bool _hapticSynthesisSimulationRegistered;
        private bool _hapticSynthesisPostSimulationRegistered;
        private bool _hapticSynthesisInitialized;
        private bool _hapticSynthesisScheduledForPostSimulation;
        private int _hapticSynthesisScheduledTelemetryIndex = -1;
        private uint _hapticSynthesisScheduledFrame;
        private uint _hapticSynthesisScheduledSchemeHash;
        private long _hapticSynthesisScheduleTimestamp;

        private void TryRegisterHapticSynthesisPostSimulation()
        {
            if (IsHapticSynthesisDispatcherRouteRegistered() || !Application.isPlaying)
                return;

            if (_hapticSynthesisSimulationSystem == null)
                _hapticSynthesisSimulationSystem = new HapticSynthesisSimulationSystem(this);
            if (_hapticSynthesisPostSimulationSystem == null)
                _hapticSynthesisPostSimulationSystem = new HapticSynthesisPostSimulationSystem(this);

            EnsureDeterministicInputNativeBuffers();
            if (!EnsureHapticSynthesisNativeBuffers())
                return;

            SignalCorridorRuntime.EnsureHapticPulseSignalLaneInitialized();
            if (!SignalBus<HapticPulseSignal>.HasNativeStorage)
                return;

            _hapticSynthesisSimulationRegistered = GlobalRegistry.TryRegisterDispatcherSystem(_hapticSynthesisSimulationSystem);
            _hapticSynthesisPostSimulationRegistered = GlobalRegistry.TryRegisterDispatcherSystem(_hapticSynthesisPostSimulationSystem);
            if (!IsHapticSynthesisDispatcherRouteRegistered())
                TryUnregisterHapticSynthesisPostSimulation();
        }

        private void TryUnregisterHapticSynthesisPostSimulation()
        {
            if (_hapticSynthesisSimulationRegistered && _hapticSynthesisSimulationSystem != null)
                GlobalRegistry.UnregisterDispatcherSystem(_hapticSynthesisSimulationSystem);
            if (_hapticSynthesisPostSimulationRegistered && _hapticSynthesisPostSimulationSystem != null)
                GlobalRegistry.UnregisterDispatcherSystem(_hapticSynthesisPostSimulationSystem);
            _hapticSynthesisSimulationRegistered = false;
            _hapticSynthesisPostSimulationRegistered = false;
            _hapticSynthesisScheduledForPostSimulation = false;
            _hapticSynthesisScheduledTelemetryIndex = -1;
        }

        private bool IsHapticSynthesisDispatcherRouteRegistered()
        {
            return _hapticSynthesisSimulationRegistered && _hapticSynthesisPostSimulationRegistered;
        }

        private void RunHapticSynthesisPostSimulation(float deltaTime)
        {
            ConsumeScheduledHapticSynthesis(deltaTime);
        }

        private JobHandle ScheduleHapticSynthesisSimulation(in DispatcherTimingDTO timing, JobHandle dependsOn)
        {
            _hapticSynthesisScheduledForPostSimulation = false;
            _hapticSynthesisScheduledTelemetryIndex = -1;
            if (!_deterministicVaultBuffersReady)
                return dependsOn;

            uint schemeHash = _currentInputSchemeHash != 0u ? _currentInputSchemeHash : ResolveCurrentInputSchemeHash();
            if (schemeHash == InputSchemeHashKeyboardMouse)
                return dependsOn;

            InputProfileDTO profile = ReadInputProfile();
            return ScheduleHapticSynthesisTranslator(timing.FrameDelta, in profile, schemeHash, timing.FrameId, dependsOn);
        }

        private JobHandle ScheduleHapticSynthesisTranslator(
            float deltaTime,
            in InputProfileDTO inputProfile,
            uint schemeHash,
            uint frameId,
            JobHandle dependsOn)
        {
            if (schemeHash == InputSchemeHashKeyboardMouse)
                return dependsOn;

            if (!TryResolveHapticSynthesisRequiredBuffers(
                    out NativeArray<HapticTuningDTO> tuningBuffer,
                    out NativeArray<HapticPulseSignal> finalPulse,
                    out NativeArray<HapticTelemetryEntry> telemetryRing,
                    out NativeArray<HapticPulseSignal> pulses,
                    out NativeArray<HapticProfileDTO> profiles))
            {
                return dependsOn;
            }

            uint frame = frameId != 0u ? frameId : Hecton8.Core.SystemDispatcher.CurrentFrameId;
            if (!TryResolvePlayerHapticAup(out double3 playerAup))
            {
                RecordHapticSynthesisManagedTelemetry(HapticSynthesisFaultFlags.MissingPlayerAup, default, 0u, 0u, 0u);
                return dependsOn;
            }

            float homeostasisQuality = HomeostasisBrain.GlobalQualityWeight;
            float quality = math.saturate(math.isfinite(homeostasisQuality) ? homeostasisQuality : 1f);
            HapticTuningDTO tuning = tuningBuffer[0];
            tuning.GlobalQualityWeight = quality;
            tuning.TickIntervalSeconds = HapticSynthesisMath.ResolveTickInterval(quality);
            tuningBuffer[0] = tuning;

            _hapticSynthesisAccumulator += math.clamp(math.isfinite(deltaTime) ? deltaTime : (float)StandardInputTickIntervalSeconds, 0f, 0.25f);
            if (_hapticSynthesisAccumulator < tuning.TickIntervalSeconds)
            {
                RecordHapticSynthesisManagedTelemetry(HapticSynthesisFaultFlags.None, finalPulse[0], 0u, 0u, 0u);
                return dependsOn;
            }

            _hapticSynthesisAccumulator = 0f;
            int telemetryIndex = AdvanceHapticTelemetryCursor();
            int mockCount = 0;
            JobHandle chain = dependsOn;
            if ((inputProfile.Flags & InputProfileFlagEnableMockCollision) != 0u &&
                TryResolveInputBuffer(in _hapticSynthesisMockImpulsesHandle, HapticSynthesisMath.MockImpulseCapacity, out NativeArray<HapticPhysicalImpulseDTO> mockImpulses))
            {
                uint seed = math.hash(new uint2(InputMockSignalSourceHash, frame));
                GenerateMockHapticStormJob mockJob = default;
                mockJob.Impulses = mockImpulses;
                mockJob.PlayerAup = playerAup;
                mockJob.Frame = frame;
                mockJob.Seed = seed;
                chain = mockJob.Schedule(chain);
                mockCount = math.min(51, mockImpulses.Length);
            }

            EvaluateHapticSynthesisJob evaluateJob = default;
            evaluateJob.ImpactSignals = SignalBus<ImpactSignal>.GetFrameSnapshotArray();
            evaluateJob.HighSpeedImpactSignals = SignalBus<HighSpeedImpactSignal>.GetFrameSnapshotArray();
            evaluateJob.CombatDamageSignals = SignalBus<CombatDamageSignal>.GetFrameSnapshotArray();
            evaluateJob.ToolAcousticSignals = SignalBus<ToolAcousticSignal>.GetFrameSnapshotArray();
            evaluateJob.MockImpulses = mockCount > 0 && TryResolveInputBuffer(in _hapticSynthesisMockImpulsesHandle, HapticSynthesisMath.MockImpulseCapacity, out NativeArray<HapticPhysicalImpulseDTO> resolvedMockImpulses)
                ? resolvedMockImpulses
                : default;
            evaluateJob.Profiles = profiles;
            evaluateJob.Tuning = tuningBuffer;
            evaluateJob.Pulses = pulses;
            evaluateJob.TelemetryRing = telemetryRing;
            evaluateJob.PlayerAup = playerAup;
            evaluateJob.Frame = frame;
            evaluateJob.GlobalQualityWeight = quality;
            evaluateJob.MockImpulseCount = mockCount;
            evaluateJob.TelemetryCursor = telemetryIndex;
            JobHandle evaluateHandle = evaluateJob.Schedule(chain);

            CoalesceHapticPulsesJob coalesceJob = default;
            coalesceJob.Pulses = pulses;
            coalesceJob.Tuning = tuningBuffer;
            coalesceJob.FinalPulse = finalPulse;
            coalesceJob.TelemetryRing = telemetryRing;
            coalesceJob.TelemetryCursor = telemetryIndex;
            coalesceJob.GlobalQualityWeight = quality;
            JobHandle outputHandle = coalesceJob.Schedule(evaluateHandle);

            _hapticSynthesisScheduledForPostSimulation = true;
            _hapticSynthesisScheduledTelemetryIndex = telemetryIndex;
            _hapticSynthesisScheduledFrame = frame;
            _hapticSynthesisScheduledSchemeHash = schemeHash;
            _hapticSynthesisScheduleTimestamp = Stopwatch.GetTimestamp();
            return outputHandle;
        }

        private void ConsumeScheduledHapticSynthesis(float deltaTime)
        {
            if (!_hapticSynthesisScheduledForPostSimulation)
                return;

            _hapticSynthesisScheduledForPostSimulation = false;
            int telemetryIndex = _hapticSynthesisScheduledTelemetryIndex;
            _hapticSynthesisScheduledTelemetryIndex = -1;
            if (telemetryIndex < 0 ||
                _hapticSynthesisScheduledSchemeHash == InputSchemeHashKeyboardMouse ||
                !TryResolveInputBuffer(in _hapticSynthesisFinalPulseHandle, 1, out NativeArray<HapticPulseSignal> finalPulse) ||
                !TryResolveInputBuffer(in _hapticSynthesisTelemetryRingHandle, HapticSynthesisMath.TelemetryCapacity, out NativeArray<HapticTelemetryEntry> telemetryRing))
            {
                return;
            }

            uint elapsedMicros = ResolveElapsedHapticSynthesisMicros();
            PatchHapticSynthesisTelemetryTiming(telemetryRing, telemetryIndex, elapsedMicros);

            HapticPulseSignal pulse = finalPulse[0];
            HapticTelemetryEntry telemetry = telemetryRing[telemetryIndex];
            uint faultMask = HapticSynthesisFaultFlags.NanSanitized |
                             HapticSynthesisFaultFlags.BudgetExceeded |
                             HapticSynthesisFaultFlags.PulseOverflow;
            if ((telemetry.Flags & faultMask) != 0u)
            {
                pulse.PriorityFlags |= HapticPulseSignal.FlagFaultDumpRequested;
                if ((telemetry.Flags & HapticSynthesisFaultFlags.NanSanitized) != 0u)
                    pulse.PriorityFlags |= HapticPulseSignal.FlagNanSanitized;
                finalPulse[0] = pulse;
                DumpHapticTelemetryIfNeeded(telemetryRing, telemetry.Frame != 0u ? telemetry.Frame : _hapticSynthesisScheduledFrame);
            }

            if (pulse.DurationSeconds <= 0f ||
                (pulse.LowFrequencyMotor01 <= HapticMotorWriteEpsilon && pulse.HighFrequencyMotor01 <= HapticMotorWriteEpsilon))
            {
                return;
            }

            SignalBus<HapticPulseSignal>.TryPushTracked(in pulse, ref s_x001HectonInputRuntimeHapticSynthSignalPushDropCount);
            float safeDeltaTime = math.isfinite(deltaTime) && deltaTime > 0f
                ? math.min(deltaTime, 0.1f)
                : (float)StandardInputTickIntervalSeconds;
            float decayRate = 1f / math.max(pulse.DurationSeconds, math.max(safeDeltaTime, 0.02f));
            InsertHapticCommandDto(
                pulse.LowFrequencyMotor01,
                pulse.HighFrequencyMotor01,
                decayRate,
                HapticLowMotorMask | HapticHighMotorMask);
        }

        private void QueueSynthesizedHapticCommand(float deltaTime, in InputProfileDTO profile, uint schemeHash)
        {
            if (schemeHash == InputSchemeHashKeyboardMouse)
                return;

            float safeDeltaTime = math.isfinite(deltaTime) && deltaTime > 0f
                ? math.min(deltaTime, 0.1f)
                : (float)StandardInputTickIntervalSeconds;
            if (!TryRunHapticSynthesisTranslator(safeDeltaTime, in profile, out HapticPulseSignal synthesizedPulse))
                return;

            float decayRate = 1f / math.max(synthesizedPulse.DurationSeconds, 0.02f);
            InsertHapticCommandDto(
                synthesizedPulse.LowFrequencyMotor01,
                synthesizedPulse.HighFrequencyMotor01,
                decayRate,
                HapticLowMotorMask | HapticHighMotorMask);
        }

        private bool TryRunHapticSynthesisTranslator(float deltaTime, in InputProfileDTO inputProfile, out HapticPulseSignal pulse)
        {
            pulse = default;
            if (!TryResolveHapticSynthesisRequiredBuffers(
                    out NativeArray<HapticTuningDTO> tuningBuffer,
                    out NativeArray<HapticPulseSignal> finalPulse,
                    out NativeArray<HapticTelemetryEntry> telemetryRing,
                    out NativeArray<HapticPulseSignal> pulses,
                    out NativeArray<HapticProfileDTO> profiles))
            {
                return false;
            }

            if (!TryResolvePlayerHapticAup(out double3 playerAup))
            {
                RecordHapticSynthesisManagedTelemetry(HapticSynthesisFaultFlags.MissingPlayerAup, default, 0u, 0u, 0u);
                return false;
            }

            float homeostasisQuality = HomeostasisBrain.GlobalQualityWeight;
            float quality = math.saturate(math.isfinite(homeostasisQuality) ? homeostasisQuality : 1f);
            HapticTuningDTO tuning = tuningBuffer[0];
            tuning.GlobalQualityWeight = quality;
            tuning.TickIntervalSeconds = HapticSynthesisMath.ResolveTickInterval(quality);
            tuningBuffer[0] = tuning;

            _hapticSynthesisAccumulator += math.clamp(math.isfinite(deltaTime) ? deltaTime : (float)StandardInputTickIntervalSeconds, 0f, 0.25f);
            if (_hapticSynthesisAccumulator < tuning.TickIntervalSeconds)
            {
                RecordHapticSynthesisManagedTelemetry(HapticSynthesisFaultFlags.None, finalPulse[0], 0u, 0u, 0u);
                return false;
            }

            _hapticSynthesisAccumulator = 0f;
            int telemetryIndex = AdvanceHapticTelemetryCursor();
            int mockCount = 0;
            if ((inputProfile.Flags & InputProfileFlagEnableMockCollision) != 0u &&
                TryResolveInputBuffer(in _hapticSynthesisMockImpulsesHandle, HapticSynthesisMath.MockImpulseCapacity, out NativeArray<HapticPhysicalImpulseDTO> mockImpulses))
            {
                uint seed = math.hash(new uint2(InputMockSignalSourceHash, Hecton8.Core.SystemDispatcher.CurrentFrameId));
                GenerateMockHapticStormJob mockJob = default;
                mockJob.Impulses = mockImpulses;
                mockJob.PlayerAup = playerAup;
                mockJob.Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
                mockJob.Seed = seed;
                mockJob.Run();
                mockCount = math.min(51, mockImpulses.Length);
            }

            long startTicks = Stopwatch.GetTimestamp();
            EvaluateHapticSynthesisJob evaluateJob = default;
            evaluateJob.ImpactSignals = SignalBus<ImpactSignal>.GetFrameSnapshotArray();
            evaluateJob.HighSpeedImpactSignals = SignalBus<HighSpeedImpactSignal>.GetFrameSnapshotArray();
            evaluateJob.CombatDamageSignals = SignalBus<CombatDamageSignal>.GetFrameSnapshotArray();
            evaluateJob.ToolAcousticSignals = SignalBus<ToolAcousticSignal>.GetFrameSnapshotArray();
            evaluateJob.MockImpulses = mockCount > 0 && TryResolveInputBuffer(in _hapticSynthesisMockImpulsesHandle, HapticSynthesisMath.MockImpulseCapacity, out NativeArray<HapticPhysicalImpulseDTO> resolvedMockImpulses)
                ? resolvedMockImpulses
                : default;
            evaluateJob.Profiles = profiles;
            evaluateJob.Tuning = tuningBuffer;
            evaluateJob.Pulses = pulses;
            evaluateJob.TelemetryRing = telemetryRing;
            evaluateJob.PlayerAup = playerAup;
            evaluateJob.Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            evaluateJob.GlobalQualityWeight = quality;
            evaluateJob.MockImpulseCount = mockCount;
            evaluateJob.TelemetryCursor = telemetryIndex;
            evaluateJob.Run();

            CoalesceHapticPulsesJob coalesceJob = default;
            coalesceJob.Pulses = pulses;
            coalesceJob.Tuning = tuningBuffer;
            coalesceJob.FinalPulse = finalPulse;
            coalesceJob.TelemetryRing = telemetryRing;
            coalesceJob.TelemetryCursor = telemetryIndex;
            coalesceJob.GlobalQualityWeight = quality;
            coalesceJob.Run();

            ulong elapsedRawMicros = (ulong)((Stopwatch.GetTimestamp() - startTicks) * 1000000L / Stopwatch.Frequency);
            uint elapsedMicros = elapsedRawMicros > uint.MaxValue ? uint.MaxValue : (uint)elapsedRawMicros;
            RecordHapticSynthesisTimingJob timingJob = default;
            timingJob.TelemetryRing = telemetryRing;
            timingJob.TelemetryCursor = telemetryIndex;
            timingJob.BurstExecutionMicroseconds = elapsedMicros;
            timingJob.Run();

            pulse = finalPulse[0];
            HapticTelemetryEntry telemetry = telemetryRing[telemetryIndex];
            uint faultMask = HapticSynthesisFaultFlags.NanSanitized |
                             HapticSynthesisFaultFlags.BudgetExceeded |
                             HapticSynthesisFaultFlags.PulseOverflow;
            if ((telemetry.Flags & faultMask) != 0u)
            {
                pulse.PriorityFlags |= HapticPulseSignal.FlagFaultDumpRequested;
                if ((telemetry.Flags & HapticSynthesisFaultFlags.NanSanitized) != 0u)
                    pulse.PriorityFlags |= HapticPulseSignal.FlagNanSanitized;
                finalPulse[0] = pulse;
                DumpHapticTelemetryIfNeeded(telemetryRing, telemetry.Frame);
            }

            if (pulse.DurationSeconds <= 0f ||
                (pulse.LowFrequencyMotor01 <= HapticMotorWriteEpsilon && pulse.HighFrequencyMotor01 <= HapticMotorWriteEpsilon))
            {
                return false;
            }

            SignalBus<HapticPulseSignal>.TryPushTracked(in pulse, ref s_x001HectonInputRuntimeHapticSynthSignalPushDropCount);
            return true;
        }

        private bool TryResolveHapticSynthesisRequiredBuffers(
            out NativeArray<HapticTuningDTO> tuningBuffer,
            out NativeArray<HapticPulseSignal> finalPulse,
            out NativeArray<HapticTelemetryEntry> telemetryRing,
            out NativeArray<HapticPulseSignal> pulses,
            out NativeArray<HapticProfileDTO> profiles)
        {
            tuningBuffer = default;
            finalPulse = default;
            telemetryRing = default;
            pulses = default;
            profiles = default;
            return _hapticSynthesisInitialized &&
                   TryResolveInputBuffer(in _hapticSynthesisTuningHandle, 1, out tuningBuffer) &&
                   TryResolveInputBuffer(in _hapticSynthesisFinalPulseHandle, 1, out finalPulse) &&
                   TryResolveInputBuffer(in _hapticSynthesisTelemetryRingHandle, HapticSynthesisMath.TelemetryCapacity, out telemetryRing) &&
                   TryResolveInputBuffer(in _hapticSynthesisPulsesHandle, HapticSynthesisMath.PulseCapacity, out pulses) &&
                   TryResolveInputBuffer(in _hapticSynthesisProfilesHandle, HapticSynthesisMath.ProfileCapacity, out profiles);
        }

        private bool EnsureHapticSynthesisNativeBuffers()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (HapticSynthesisMath.ValidateLayoutSizes() != 0u)
                Hecton8.Core.H8Debug.LogError("[InputDispatcher] Haptic synthesis ABI violation.");
#endif
            bool ready =
                TryResolveOrAcquireInputBuffer(
                    ref _hapticSynthesisPulsesHandle,
                    BufferID.ShinobuHapticSynthesisPulses,
                    HapticSynthesisMath.PulseCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    out _) &&
                TryResolveOrAcquireInputBuffer(
                    ref _hapticSynthesisFinalPulseHandle,
                    BufferID.ShinobuHapticSynthesisFinalPulse,
                    1,
                    NativeArrayOptions.UninitializedMemory,
                    out _) &&
                TryResolveOrAcquireInputBuffer(
                    ref _hapticSynthesisMockImpulsesHandle,
                    BufferID.ShinobuHapticSynthesisMockImpulses,
                    HapticSynthesisMath.MockImpulseCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    out _) &&
                TryResolveOrAcquireInputBuffer(
                    ref _hapticSynthesisTelemetryRingHandle,
                    BufferID.ShinobuHapticSynthesisTelemetryRing,
                    HapticSynthesisMath.TelemetryCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    out _) &&
                TryResolveOrAcquireInputBuffer(
                    ref _hapticSynthesisProfilesHandle,
                    BufferID.ShinobuHapticSynthesisProfileTable,
                    HapticSynthesisMath.ProfileCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    out _) &&
                TryResolveOrAcquireInputBuffer(
                    ref _hapticSynthesisTuningHandle,
                    BufferID.ShinobuHapticSynthesisTuning,
                    1,
                    NativeArrayOptions.UninitializedMemory,
                    out _);
#if UNITY_EDITOR
            ready = ready &&
                TryResolveOrAcquireInputBuffer(
                    ref _hapticSynthesisCsvScratchHandle,
                    BufferID.ShinobuHapticSynthesisCsvScratch,
                    HapticSynthesisMath.ProfileCsvScratchBytes,
                    NativeArrayOptions.UninitializedMemory,
                    out _);
#endif

            if (!ready)
                return false;

            if (!_hapticSynthesisInitialized)
                InitializeHapticSynthesisBuffers();

            return true;
        }

        private uint ResolveElapsedHapticSynthesisMicros()
        {
            if (_hapticSynthesisScheduleTimestamp <= 0L)
                return 0u;

            long elapsedTicks = Stopwatch.GetTimestamp() - _hapticSynthesisScheduleTimestamp;
            if (elapsedTicks <= 0L)
                return 0u;

            ulong elapsedRawMicros = (ulong)(elapsedTicks * 1000000L / Stopwatch.Frequency);
            return elapsedRawMicros > uint.MaxValue ? uint.MaxValue : (uint)elapsedRawMicros;
        }

        private static void PatchHapticSynthesisTelemetryTiming(
            NativeArray<HapticTelemetryEntry> telemetryRing,
            int telemetryIndex,
            uint elapsedMicros)
        {
            if (!telemetryRing.IsCreated || telemetryRing.Length <= 0)
                return;

            int index = math.clamp(telemetryIndex, 0, telemetryRing.Length - 1);
            HapticTelemetryEntry* telemetryPtr = (HapticTelemetryEntry*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(telemetryRing);
            ref HapticTelemetryEntry entry = ref UnsafeUtility.AsRef<HapticTelemetryEntry>(telemetryPtr + index);
            entry.BurstExecutionMicroseconds = elapsedMicros;
            entry.StateHash ^= elapsedMicros * 16777619u;
        }

        private void InitializeHapticSynthesisBuffers()
        {
            if (!TryResolveInputBuffer(in _hapticSynthesisPulsesHandle, HapticSynthesisMath.PulseCapacity, out NativeArray<HapticPulseSignal> pulses) ||
                !TryResolveInputBuffer(in _hapticSynthesisFinalPulseHandle, 1, out NativeArray<HapticPulseSignal> finalPulse) ||
                !TryResolveInputBuffer(in _hapticSynthesisTelemetryRingHandle, HapticSynthesisMath.TelemetryCapacity, out NativeArray<HapticTelemetryEntry> telemetryRing) ||
                !TryResolveInputBuffer(in _hapticSynthesisProfilesHandle, HapticSynthesisMath.ProfileCapacity, out NativeArray<HapticProfileDTO> profiles) ||
                !TryResolveInputBuffer(in _hapticSynthesisTuningHandle, 1, out NativeArray<HapticTuningDTO> tuning))
            {
                return;
            }

            for (int i = 0; i < pulses.Length; i++)
                pulses[i] = default;
            finalPulse[0] = default;
            for (int i = 0; i < telemetryRing.Length; i++)
                telemetryRing[i] = default;

            int profileCount = HapticSynthesisMath.WriteDefaultProfiles(profiles);
#if UNITY_EDITOR
            int csvCount = TryLoadHapticProfilesFromCsv(profiles);
            if (csvCount > 0)
                profileCount = csvCount;
#endif

            float homeostasisQuality = HomeostasisBrain.GlobalQualityWeight;
            HapticTuningDTO defaultTuning = HapticSynthesisMath.DefaultTuning(math.isfinite(homeostasisQuality) ? homeostasisQuality : 1f);
            defaultTuning.ProfileCount = (uint)math.max(0, profileCount);
            tuning[0] = defaultTuning;
            _hapticSynthesisInitialized = true;
        }

#if UNITY_EDITOR
        private int TryLoadHapticProfilesFromCsv(NativeArray<HapticProfileDTO> profiles)
        {
            if (!TryResolveInputBuffer(in _hapticSynthesisCsvScratchHandle, HapticSynthesisMath.ProfileCsvScratchBytes, out NativeArray<byte> scratch))
                return 0;

            string path = Path.Combine(Application.dataPath, "_Project", "Data", "Haptics", HapticProfilesFileName);
            if (!File.Exists(path))
                return 0;

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                int byteCount = (int)math.min(stream.Length, scratch.Length);
                byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(scratch);
                Span<byte> destination = new Span<byte>(ptr, byteCount);
                int read = stream.Read(destination);
                return HapticProfileCsvParser.ParseProfiles(destination.Slice(0, read), profiles);
            }
        }
#endif

        private bool TryResolvePlayerHapticAup(out double3 playerAup)
        {
            playerAup = double3.zero;
            IPlayerRuntimeContext playerContext = _playerContext;
            if (playerContext == null)
                return false;

            if (playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                movementState.PredictedAup.IsFinite())
            {
                playerAup = movementState.PredictedAup.ToAbsoluteDouble3();
                return math.all(math.isfinite(playerAup));
            }

            if (playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot pose) &&
                pose.Aup.IsFinite())
            {
                playerAup = pose.Aup.ToAbsoluteDouble3();
                return math.all(math.isfinite(playerAup));
            }

            return false;
        }

        private int AdvanceHapticTelemetryCursor()
        {
            int cursor = _hapticSynthesisTelemetryCursor;
            _hapticSynthesisTelemetryCursor = (cursor + 1) % HapticSynthesisMath.TelemetryCapacity;
            return math.clamp(cursor, 0, HapticSynthesisMath.TelemetryCapacity - 1);
        }

        private void RecordHapticSynthesisManagedTelemetry(uint flags, HapticPulseSignal lastPulse, uint rawCount, uint droppedCount, uint burstMicros)
        {
            if (!TryResolveInputBuffer(in _hapticSynthesisTelemetryRingHandle, HapticSynthesisMath.TelemetryCapacity, out NativeArray<HapticTelemetryEntry> telemetryRing))
                return;

            int index = AdvanceHapticTelemetryCursor();
            HapticTelemetryEntry entry = default;
            entry.Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            entry.FinalLowFrequency01 = lastPulse.LowFrequencyMotor01;
            entry.FinalHighFrequency01 = lastPulse.HighFrequencyMotor01;
            entry.RawSignalCount = rawCount;
            entry.DroppedSignalCount = droppedCount;
            entry.BurstExecutionMicroseconds = burstMicros;
            entry.Flags = flags;
            float homeostasisQuality = HomeostasisBrain.GlobalQualityWeight;
            entry.GlobalQualityWeight = math.saturate(math.isfinite(homeostasisQuality) ? homeostasisQuality : 1f);
            entry.GeneratedPulseCount = 0u;
            entry.StateHash = math.hash(new uint4(entry.Frame, rawCount, droppedCount, flags));
            telemetryRing[index] = entry;
        }

        private void DumpHapticTelemetryIfNeeded(NativeArray<HapticTelemetryEntry> telemetryRing, uint frame)
        {
            int safeFrame = unchecked((int)frame);
            if (_lastHapticSynthesisFaultDumpFrame == safeFrame || !telemetryRing.IsCreated || telemetryRing.Length <= 0)
                return;

            _lastHapticSynthesisFaultDumpFrame = safeFrame;
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                return;

            string dumpPath = Path.Combine(projectRoot, HapticFaultDumpRelativePath);
            string directory = Path.GetDirectoryName(dumpPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            void* ptr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetryRing);
            int byteCount = telemetryRing.Length * UnsafeUtility.SizeOf<HapticTelemetryEntry>();
            ReadOnlySpan<byte> bytes = new ReadOnlySpan<byte>(ptr, byteCount);
            using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                stream.Write(bytes);
            }
        }

        private void ReleaseHapticSynthesisVaultHandles(IDataVault vault)
        {
            TryUnregisterHapticSynthesisPostSimulation();
            ReleaseVaultHandle(vault, ref _hapticSynthesisPulsesHandle);
            ReleaseVaultHandle(vault, ref _hapticSynthesisFinalPulseHandle);
            ReleaseVaultHandle(vault, ref _hapticSynthesisMockImpulsesHandle);
            ReleaseVaultHandle(vault, ref _hapticSynthesisTelemetryRingHandle);
            ReleaseVaultHandle(vault, ref _hapticSynthesisProfilesHandle);
            ReleaseVaultHandle(vault, ref _hapticSynthesisTuningHandle);
#if UNITY_EDITOR
            ReleaseVaultHandle(vault, ref _hapticSynthesisCsvScratchHandle);
#endif
            _hapticSynthesisInitialized = false;
            _hapticSynthesisAccumulator = 0f;
            _hapticSynthesisTelemetryCursor = 0;
            _hapticSynthesisScheduledForPostSimulation = false;
            _hapticSynthesisScheduledTelemetryIndex = -1;
            _hapticSynthesisScheduledFrame = 0u;
            _hapticSynthesisScheduledSchemeHash = 0u;
            _hapticSynthesisScheduleTimestamp = 0L;
        }

        private sealed class HapticSynthesisSimulationSystem : IDispatcherSystem
        {
            private const uint SystemHash = 0x53333532u;
            private readonly InputDispatcher _owner;

            public HapticSynthesisSimulationSystem(InputDispatcher owner)
            {
                _owner = owner;
            }

            public uint GetSystemIdHash() => SystemHash;
            public DispatcherPhase GetDispatcherPhase() => DispatcherPhase.Simulation;
            public byte GetBucketId() => 0;
            public int GetDependencyCount() => 0;
            public uint GetDependencyHash(int dependencyIndex) => 0u;
            public void PreSimulationTick(in DispatcherTimingDTO timing) { }
            public void PostSimulationTick(in DispatcherTimingDTO timing) { }
            public void VisualSyncTick(in DispatcherTimingDTO timing) { }

            public JobHandle ScheduleSimulation(
                in DispatcherTimingDTO timing,
                in DispatcherJobContext context,
                JobHandle dependsOn)
            {
                return _owner.ScheduleHapticSynthesisSimulation(in timing, dependsOn);
            }
        }

        private sealed class HapticSynthesisPostSimulationSystem : IDispatcherSystem
        {
            private const uint SystemHash = 0x53333533u;
            private readonly InputDispatcher _owner;

            public HapticSynthesisPostSimulationSystem(InputDispatcher owner)
            {
                _owner = owner;
            }

            public uint GetSystemIdHash() => SystemHash;
            public DispatcherPhase GetDispatcherPhase() => DispatcherPhase.PostSimulation;
            public byte GetBucketId() => 0;
            public int GetDependencyCount() => 0;
            public uint GetDependencyHash(int dependencyIndex) => 0u;
            public void PreSimulationTick(in DispatcherTimingDTO timing) { }
            public JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn) => dependsOn;
            public void VisualSyncTick(in DispatcherTimingDTO timing) { }

            public void PostSimulationTick(in DispatcherTimingDTO timing)
            {
                _owner.RunHapticSynthesisPostSimulation(timing.FrameDelta);
            }
        }
    }
}
