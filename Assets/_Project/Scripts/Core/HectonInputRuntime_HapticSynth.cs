using System;
using System.Diagnostics;
using System.IO;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Tools;
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
        private const uint HapticSynthesisPinPulses = 1u << 0;
        private const uint HapticSynthesisPinFinalPulse = 1u << 1;
        private const uint HapticSynthesisPinMockImpulses = 1u << 2;
        private const uint HapticSynthesisPinTelemetry = 1u << 3;
        private const uint HapticSynthesisPinProfiles = 1u << 4;
        private const uint HapticSynthesisPinTuning = 1u << 5;
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
        private uint _hapticSynthesisPinnedBufferMask;
        private IDataVault _hapticSynthesisPinnedBufferVault;
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
            ReleaseHapticSynthesisSchedulePins();
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
            ReleaseHapticSynthesisSchedulePins();
            _hapticSynthesisScheduledForPostSimulation = false;
            _hapticSynthesisScheduledTelemetryIndex = -1;
            if (!_deterministicVaultBuffersReady)
                return dependsOn;

            uint schemeHash = _currentInputSchemeHash != 0u ? _currentInputSchemeHash : ResolveCurrentInputSchemeHash();
            if (schemeHash == InputSchemeHashKeyboardMouse)
                return dependsOn;
            if (ToolHapticsRuntime.PowerSaveMuteActive)
            {
                _hapticSynthesisAccumulator = 0f;
                return dependsOn;
            }

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

            if (!TryReadHapticSynthesisRequiredBuffers(
                    out NativeArray<HapticTuningDTO>.ReadOnly tuningReadBuffer,
                    out NativeArray<HapticPulseSignal>.ReadOnly finalPulseRead,
                    out _,
                    out _,
                    out _))
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
            HapticTuningDTO tuning = tuningReadBuffer[0];
            tuning.GlobalQualityWeight = quality;
            tuning.TickIntervalSeconds = HapticSynthesisMath.ResolveTickInterval(quality);
            if (!TryWriteHapticSynthesisTuning(in tuning))
                return dependsOn;

            _hapticSynthesisAccumulator += math.clamp(math.isfinite(deltaTime) ? deltaTime : (float)StandardInputTickIntervalSeconds, 0f, 0.25f);
            if (_hapticSynthesisAccumulator < tuning.TickIntervalSeconds)
            {
                RecordHapticSynthesisManagedTelemetry(HapticSynthesisFaultFlags.None, finalPulseRead[0], 0u, 0u, 0u);
                return dependsOn;
            }

            _hapticSynthesisAccumulator = 0f;
            int telemetryIndex = AdvanceHapticTelemetryCursor();
            int mockCount = 0;
            JobHandle chain = dependsOn;
            bool includeMockImpulses = (inputProfile.Flags & InputProfileFlagEnableMockCollision) != 0u;
            if (!TryPinHapticSynthesisScheduleBuffers(includeMockImpulses) ||
                !TryOpenHapticSynthesisJobBuffersForOwner(
                    out NativeArray<HapticTuningDTO> tuningBuffer,
                    out NativeArray<HapticPulseSignal> finalPulse,
                    out NativeArray<HapticTelemetryEntry> telemetryRing,
                    out NativeArray<HapticPulseSignal> pulses,
                    out NativeArray<HapticProfileDTO> profiles))
            {
                ReleaseHapticSynthesisSchedulePins();
                return dependsOn;
            }

            bool scheduled = false;
            try
            {
                NativeArray<HapticPhysicalImpulseDTO> mockImpulses = default;
                bool hasMockImpulses = includeMockImpulses &&
                                       TryOpenHapticInputBufferForOwner(
                                           BufferID.ShinobuHapticSynthesisMockImpulses,
                                           in _hapticSynthesisMockImpulsesHandle,
                                           HapticSynthesisMath.MockImpulseCapacity,
                                           out mockImpulses);
                if (hasMockImpulses)
                {
                    uint2 seedInput = default;
                    seedInput.x = InputMockSignalSourceHash;
                    seedInput.y = frame;
                    uint seed = math.hash(seedInput);
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
                evaluateJob.MockImpulses = hasMockImpulses
                    ? mockImpulses
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
                scheduled = true;
                return outputHandle;
            }
            finally
            {
                if (!scheduled)
                    ReleaseHapticSynthesisSchedulePins();
            }
        }

        private void ConsumeScheduledHapticSynthesis(float deltaTime)
        {
            if (!_hapticSynthesisScheduledForPostSimulation)
                return;

            _hapticSynthesisScheduledForPostSimulation = false;
            int telemetryIndex = _hapticSynthesisScheduledTelemetryIndex;
            _hapticSynthesisScheduledTelemetryIndex = -1;
            ReleaseHapticSynthesisSchedulePins();
            if (telemetryIndex < 0 ||
                _hapticSynthesisScheduledSchemeHash == InputSchemeHashKeyboardMouse)
            {
                return;
            }
            if (ToolHapticsRuntime.PowerSaveMuteActive)
                return;

            uint elapsedMicros = ResolveElapsedHapticSynthesisMicros();
            HapticTelemetryEntry telemetry = default;
            if (!TryAcquireInputWriteBuffer(
                    BufferID.ShinobuHapticSynthesisTelemetryRing,
                    in _hapticSynthesisTelemetryRingHandle,
                    HapticSynthesisMath.TelemetryCapacity,
                    out NativeArray<HapticTelemetryEntry> telemetryRing,
                    out IDataVault telemetryVault))
            {
                return;
            }

            try
            {
                PatchHapticSynthesisTelemetryTiming(telemetryRing, telemetryIndex, elapsedMicros);
                telemetry = telemetryRing[math.clamp(telemetryIndex, 0, telemetryRing.Length - 1)];
            }
            finally
            {
                ReleaseInputWriteBuffer(telemetryVault, BufferID.ShinobuHapticSynthesisTelemetryRing, in _hapticSynthesisTelemetryRingHandle);
            }

            if (!TryReadHapticInputBuffer(BufferID.ShinobuHapticSynthesisFinalPulse, in _hapticSynthesisFinalPulseHandle, 1, out NativeArray<HapticPulseSignal>.ReadOnly finalPulse))
                return;

            HapticPulseSignal pulse = finalPulse[0];
            uint faultMask = HapticSynthesisFaultFlags.NanSanitized |
                             HapticSynthesisFaultFlags.BudgetExceeded |
                             HapticSynthesisFaultFlags.PulseOverflow;
            if ((telemetry.Flags & faultMask) != 0u)
            {
                pulse.PriorityFlags |= HapticPulseSignal.FlagFaultDumpRequested;
                if ((telemetry.Flags & HapticSynthesisFaultFlags.NanSanitized) != 0u)
                    pulse.PriorityFlags |= HapticPulseSignal.FlagNanSanitized;
                TryWriteHapticSynthesisFinalPulse(in pulse);

                if (TryReadHapticInputBuffer(BufferID.ShinobuHapticSynthesisTelemetryRing, in _hapticSynthesisTelemetryRingHandle, HapticSynthesisMath.TelemetryCapacity, out NativeArray<HapticTelemetryEntry>.ReadOnly telemetryDumpRing))
                    DumpHapticTelemetryIfNeeded(telemetryDumpRing, telemetry.Frame != 0u ? telemetry.Frame : _hapticSynthesisScheduledFrame);
            }

            if (pulse.DurationSeconds <= 0f ||
                (pulse.LowFrequencyMotor01 <= HapticMotorWriteEpsilon && pulse.HighFrequencyMotor01 <= HapticMotorWriteEpsilon))
            {
                return;
            }

            SignalBus<HapticPulseSignal>.TryPushTracked(in pulse, ref s_x001HectonInputRuntimeHapticSynthSignalPushDropCount);
        }

        private void QueueSynthesizedHapticCommand(float deltaTime, in InputProfileDTO profile, uint schemeHash)
        {
            if (schemeHash == InputSchemeHashKeyboardMouse)
                return;
            if (ToolHapticsRuntime.PowerSaveMuteActive)
            {
                _hapticSynthesisAccumulator = 0f;
                return;
            }

            float safeDeltaTime = math.isfinite(deltaTime) && deltaTime > 0f
                ? math.min(deltaTime, 0.1f)
                : (float)StandardInputTickIntervalSeconds;
            TryRunHapticSynthesisTranslator(safeDeltaTime, in profile, out _);
        }

        private bool TryRunHapticSynthesisTranslator(float deltaTime, in InputProfileDTO inputProfile, out HapticPulseSignal pulse)
        {
            pulse = default;
            if (!TryReadHapticSynthesisRequiredBuffers(
                    out NativeArray<HapticTuningDTO>.ReadOnly tuningReadBuffer,
                    out NativeArray<HapticPulseSignal>.ReadOnly finalPulseRead,
                    out _,
                    out _,
                    out _))
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
            HapticTuningDTO tuning = tuningReadBuffer[0];
            tuning.GlobalQualityWeight = quality;
            tuning.TickIntervalSeconds = HapticSynthesisMath.ResolveTickInterval(quality);
            if (!TryWriteHapticSynthesisTuning(in tuning))
                return false;

            _hapticSynthesisAccumulator += math.clamp(math.isfinite(deltaTime) ? deltaTime : (float)StandardInputTickIntervalSeconds, 0f, 0.25f);
            if (_hapticSynthesisAccumulator < tuning.TickIntervalSeconds)
            {
                RecordHapticSynthesisManagedTelemetry(HapticSynthesisFaultFlags.None, finalPulseRead[0], 0u, 0u, 0u);
                return false;
            }

            _hapticSynthesisAccumulator = 0f;
            int telemetryIndex = AdvanceHapticTelemetryCursor();
            int mockCount = 0;
            bool includeMockImpulses = (inputProfile.Flags & InputProfileFlagEnableMockCollision) != 0u;
            if (!TryPinHapticSynthesisScheduleBuffers(includeMockImpulses))
                return false;

            bool shouldPushPulse = false;
            bool shouldWriteFaultPulse = false;
            HapticPulseSignal faultPulse = default;
            uint faultDumpFrame = 0u;
            try
            {
                if (!TryOpenHapticSynthesisJobBuffersForOwner(
                        out NativeArray<HapticTuningDTO> tuningBuffer,
                        out NativeArray<HapticPulseSignal> finalPulse,
                        out NativeArray<HapticTelemetryEntry> telemetryRing,
                        out NativeArray<HapticPulseSignal> pulses,
                        out NativeArray<HapticProfileDTO> profiles))
                {
                    return false;
                }

                NativeArray<HapticPhysicalImpulseDTO> mockImpulses = default;
                bool hasMockImpulses = includeMockImpulses &&
                                       TryOpenHapticInputBufferForOwner(
                                           BufferID.ShinobuHapticSynthesisMockImpulses,
                                           in _hapticSynthesisMockImpulsesHandle,
                                           HapticSynthesisMath.MockImpulseCapacity,
                                           out mockImpulses);
                if (hasMockImpulses)
                {
                    uint2 seedInput = default;
                    seedInput.x = InputMockSignalSourceHash;
                    seedInput.y = Hecton8.Core.SystemDispatcher.CurrentFrameId;
                    uint seed = math.hash(seedInput);
                    GenerateMockHapticStormJob mockJob = default;
                    mockJob.Impulses = mockImpulses;
                    mockJob.PlayerAup = playerAup;
                    mockJob.Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
                    mockJob.Seed = seed;
                    mockJob.Execute();
                    mockCount = math.min(51, mockImpulses.Length);
                }

                long startTicks = Stopwatch.GetTimestamp();
                EvaluateHapticSynthesisJob evaluateJob = default;
                evaluateJob.ImpactSignals = SignalBus<ImpactSignal>.GetFrameSnapshotArray();
                evaluateJob.HighSpeedImpactSignals = SignalBus<HighSpeedImpactSignal>.GetFrameSnapshotArray();
                evaluateJob.CombatDamageSignals = SignalBus<CombatDamageSignal>.GetFrameSnapshotArray();
                evaluateJob.ToolAcousticSignals = SignalBus<ToolAcousticSignal>.GetFrameSnapshotArray();
                evaluateJob.MockImpulses = hasMockImpulses
                    ? mockImpulses
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
                evaluateJob.Execute();

                CoalesceHapticPulsesJob coalesceJob = default;
                coalesceJob.Pulses = pulses;
                coalesceJob.Tuning = tuningBuffer;
                coalesceJob.FinalPulse = finalPulse;
                coalesceJob.TelemetryRing = telemetryRing;
                coalesceJob.TelemetryCursor = telemetryIndex;
                coalesceJob.GlobalQualityWeight = quality;
                coalesceJob.Execute();

                ulong elapsedRawMicros = (ulong)((Stopwatch.GetTimestamp() - startTicks) * 1000000L / Stopwatch.Frequency);
                uint elapsedMicros = elapsedRawMicros > uint.MaxValue ? uint.MaxValue : (uint)elapsedRawMicros;
                RecordHapticSynthesisTimingJob timingJob = default;
                timingJob.TelemetryRing = telemetryRing;
                timingJob.TelemetryCursor = telemetryIndex;
                timingJob.BurstExecutionMicroseconds = elapsedMicros;
                timingJob.Execute();

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
                    shouldWriteFaultPulse = true;
                    faultPulse = pulse;
                    faultDumpFrame = telemetry.Frame;
                }

                shouldPushPulse = pulse.DurationSeconds > 0f &&
                                  (pulse.LowFrequencyMotor01 > HapticMotorWriteEpsilon ||
                                   pulse.HighFrequencyMotor01 > HapticMotorWriteEpsilon);
            }
            finally
            {
                ReleaseHapticSynthesisSchedulePins();
            }

            if (shouldWriteFaultPulse)
            {
                TryWriteHapticSynthesisFinalPulse(in faultPulse);
                if (TryReadHapticInputBuffer(BufferID.ShinobuHapticSynthesisTelemetryRing, in _hapticSynthesisTelemetryRingHandle, HapticSynthesisMath.TelemetryCapacity, out NativeArray<HapticTelemetryEntry>.ReadOnly telemetryDumpRing))
                    DumpHapticTelemetryIfNeeded(telemetryDumpRing, faultDumpFrame);
            }

            if (!shouldPushPulse)
                return false;

            SignalBus<HapticPulseSignal>.TryPushTracked(in pulse, ref s_x001HectonInputRuntimeHapticSynthSignalPushDropCount);
            return true;
        }

        private bool TryOpenHapticSynthesisJobBuffersForOwner(
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
                   TryOpenHapticInputBufferForOwner(BufferID.ShinobuHapticSynthesisTuning, in _hapticSynthesisTuningHandle, 1, out tuningBuffer) &&
                   TryOpenHapticInputBufferForOwner(BufferID.ShinobuHapticSynthesisFinalPulse, in _hapticSynthesisFinalPulseHandle, 1, out finalPulse) &&
                   TryOpenHapticInputBufferForOwner(BufferID.ShinobuHapticSynthesisTelemetryRing, in _hapticSynthesisTelemetryRingHandle, HapticSynthesisMath.TelemetryCapacity, out telemetryRing) &&
                   TryOpenHapticInputBufferForOwner(BufferID.ShinobuHapticSynthesisPulses, in _hapticSynthesisPulsesHandle, HapticSynthesisMath.PulseCapacity, out pulses) &&
                   TryOpenHapticInputBufferForOwner(BufferID.ShinobuHapticSynthesisProfileTable, in _hapticSynthesisProfilesHandle, HapticSynthesisMath.ProfileCapacity, out profiles);
        }

        private bool TryReadHapticSynthesisRequiredBuffers(
            out NativeArray<HapticTuningDTO>.ReadOnly tuningBuffer,
            out NativeArray<HapticPulseSignal>.ReadOnly finalPulse,
            out NativeArray<HapticTelemetryEntry>.ReadOnly telemetryRing,
            out NativeArray<HapticPulseSignal>.ReadOnly pulses,
            out NativeArray<HapticProfileDTO>.ReadOnly profiles)
        {
            tuningBuffer = default;
            finalPulse = default;
            telemetryRing = default;
            pulses = default;
            profiles = default;
            return _hapticSynthesisInitialized &&
                   TryReadHapticInputBuffer(BufferID.ShinobuHapticSynthesisTuning, in _hapticSynthesisTuningHandle, 1, out tuningBuffer) &&
                   TryReadHapticInputBuffer(BufferID.ShinobuHapticSynthesisFinalPulse, in _hapticSynthesisFinalPulseHandle, 1, out finalPulse) &&
                   TryReadHapticInputBuffer(BufferID.ShinobuHapticSynthesisTelemetryRing, in _hapticSynthesisTelemetryRingHandle, HapticSynthesisMath.TelemetryCapacity, out telemetryRing) &&
                   TryReadHapticInputBuffer(BufferID.ShinobuHapticSynthesisPulses, in _hapticSynthesisPulsesHandle, HapticSynthesisMath.PulseCapacity, out pulses) &&
                   TryReadHapticInputBuffer(BufferID.ShinobuHapticSynthesisProfileTable, in _hapticSynthesisProfilesHandle, HapticSynthesisMath.ProfileCapacity, out profiles);
        }

        private bool TryOpenHapticInputBufferForOwner<T>(
            BufferID expectedBufferId,
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            if (vault == null ||
                !IsHapticSynthesisHandle(in handle, expectedBufferId) ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private bool TryReadHapticInputBuffer<T>(
            BufferID expectedBufferId,
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            if (vault == null ||
                !IsHapticSynthesisHandle(in handle, expectedBufferId) ||
                !vault.TryReadOnlyHandle(in handle, out buffer) ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private bool OpenOrAcquireHapticSynthesisBufferForOwnerRoute<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer) where T : struct
        {
            if (TryOpenHapticInputBufferForOwner(bufferId, in handle, requiredLength, out buffer))
                return true;

            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (vault.IsAllocationLocked || vault.IsCompactionFenceActive)
            {
                buffer = default;
                return false;
            }

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.CoreDeterminism,
                options);

            if (TryOpenHapticInputBufferForOwner(bufferId, in handle, requiredLength, out buffer))
                return true;

            ReleaseHapticSynthesisVaultHandle(vault, bufferId, ref handle);
            buffer = default;
            return false;
        }

        private bool EnsureHapticSynthesisNativeBuffers()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (HapticSynthesisMath.ValidateLayoutSizes() != 0u)
                Hecton8.Core.H8Debug.LogError("[InputDispatcher] Haptic synthesis ABI violation.");
#endif
            bool ready =
                OpenOrAcquireHapticSynthesisBufferForOwnerRoute(
                    ref _hapticSynthesisPulsesHandle,
                    BufferID.ShinobuHapticSynthesisPulses,
                    HapticSynthesisMath.PulseCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    out _) &&
                OpenOrAcquireHapticSynthesisBufferForOwnerRoute(
                    ref _hapticSynthesisFinalPulseHandle,
                    BufferID.ShinobuHapticSynthesisFinalPulse,
                    1,
                    NativeArrayOptions.UninitializedMemory,
                    out _) &&
                OpenOrAcquireHapticSynthesisBufferForOwnerRoute(
                    ref _hapticSynthesisMockImpulsesHandle,
                    BufferID.ShinobuHapticSynthesisMockImpulses,
                    HapticSynthesisMath.MockImpulseCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    out _) &&
                OpenOrAcquireHapticSynthesisBufferForOwnerRoute(
                    ref _hapticSynthesisTelemetryRingHandle,
                    BufferID.ShinobuHapticSynthesisTelemetryRing,
                    HapticSynthesisMath.TelemetryCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    out _) &&
                OpenOrAcquireHapticSynthesisBufferForOwnerRoute(
                    ref _hapticSynthesisProfilesHandle,
                    BufferID.ShinobuHapticSynthesisProfileTable,
                    HapticSynthesisMath.ProfileCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    out _) &&
                OpenOrAcquireHapticSynthesisBufferForOwnerRoute(
                    ref _hapticSynthesisTuningHandle,
                    BufferID.ShinobuHapticSynthesisTuning,
                    1,
                    NativeArrayOptions.UninitializedMemory,
                    out _);
#if UNITY_EDITOR
            ready = ready &&
                OpenOrAcquireHapticSynthesisBufferForOwnerRoute(
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
            if (!ClearHapticSynthesisBuffer(BufferID.ShinobuHapticSynthesisPulses, in _hapticSynthesisPulsesHandle, HapticSynthesisMath.PulseCapacity) ||
                !ClearHapticSynthesisBuffer(BufferID.ShinobuHapticSynthesisFinalPulse, in _hapticSynthesisFinalPulseHandle, 1) ||
                !ClearHapticSynthesisBuffer(BufferID.ShinobuHapticSynthesisTelemetryRing, in _hapticSynthesisTelemetryRingHandle, HapticSynthesisMath.TelemetryCapacity))
            {
                return;
            }

            int profileCount = WriteDefaultHapticProfiles();
#if UNITY_EDITOR
            int csvCount = TryLoadHapticProfilesFromCsv();
            if (csvCount > 0)
                profileCount = csvCount;
#endif

            float homeostasisQuality = HomeostasisBrain.GlobalQualityWeight;
            HapticTuningDTO defaultTuning = HapticSynthesisMath.DefaultTuning(math.isfinite(homeostasisQuality) ? homeostasisQuality : 1f);
            defaultTuning.ProfileCount = (uint)math.max(0, profileCount);
            if (!TryWriteHapticSynthesisTuning(in defaultTuning))
                return;
            _hapticSynthesisInitialized = true;
        }

#if UNITY_EDITOR
        private int TryLoadHapticProfilesFromCsv()
        {
            Span<byte> csvScratch = stackalloc byte[HapticSynthesisMath.ProfileCsvScratchBytes];
            int read = TryReadHapticProfilesCsv(csvScratch);
            if (read <= 0)
                return 0;

            if (!TryAcquireInputWriteBuffer(
                    BufferID.ShinobuHapticSynthesisProfileTable,
                    in _hapticSynthesisProfilesHandle,
                    HapticSynthesisMath.ProfileCapacity,
                    out NativeArray<HapticProfileDTO> profiles,
                    out IDataVault profilesVault))
            {
                return 0;
            }

            try
            {
                return HapticProfileCsvParser.ParseProfiles(csvScratch.Slice(0, read), profiles);
            }
            finally
            {
                ReleaseInputWriteBuffer(profilesVault, BufferID.ShinobuHapticSynthesisProfileTable, in _hapticSynthesisProfilesHandle);
            }
        }

        private static int TryReadHapticProfilesCsv(Span<byte> destination)
        {
            string path = Path.Combine(Application.dataPath, "_Project", "Data", "Haptics", HapticProfilesFileName);
            if (destination.Length <= 0 || !File.Exists(path))
                return 0;

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                int byteCount = (int)math.min(stream.Length, destination.Length);
                return stream.Read(destination.Slice(0, byteCount));
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
            if (!TryAcquireInputWriteBuffer(
                    BufferID.ShinobuHapticSynthesisTelemetryRing,
                    in _hapticSynthesisTelemetryRingHandle,
                    HapticSynthesisMath.TelemetryCapacity,
                    out NativeArray<HapticTelemetryEntry> telemetryRing,
                    out IDataVault telemetryVault))
            {
                return;
            }

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
            try
            {
                telemetryRing[index] = entry;
            }
            finally
            {
                ReleaseInputWriteBuffer(telemetryVault, BufferID.ShinobuHapticSynthesisTelemetryRing, in _hapticSynthesisTelemetryRingHandle);
            }
        }

        private void DumpHapticTelemetryIfNeeded(NativeArray<HapticTelemetryEntry>.ReadOnly telemetryRing, uint frame)
        {
            int safeFrame = unchecked((int)frame);
            if (_lastHapticSynthesisFaultDumpFrame == safeFrame || !telemetryRing.IsCreated || telemetryRing.Length <= 0)
                return;

            _lastHapticSynthesisFaultDumpFrame = safeFrame;
            int byteCount = telemetryRing.Length * UnsafeUtility.SizeOf<HapticTelemetryEntry>();
            NativeArray<byte> payload = default;
            const string dumpPayloadLabel = "hapticSynthesisTelemetryDumpPayload";
            try
            {
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(InputDispatcher),
                    dumpPayloadLabel,
                    NativeArrayOptions.UninitializedMemory);
                void* source = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetryRing);
                void* destination = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(payload);
                if (UnsafeMemoryCopyGuard.SafeCopy(destination, byteCount, source, byteCount))
                    NativeFaultDumpWriter.TryWriteAll(HapticFaultDumpRelativePath, payload, byteCount);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(ref payload, nameof(InputDispatcher), dumpPayloadLabel);
            }
        }

        private void ReleaseHapticSynthesisVaultHandles(IDataVault vault)
        {
            TryUnregisterHapticSynthesisPostSimulation();
            ReleaseHapticSynthesisVaultHandle(vault, BufferID.ShinobuHapticSynthesisPulses, ref _hapticSynthesisPulsesHandle);
            ReleaseHapticSynthesisVaultHandle(vault, BufferID.ShinobuHapticSynthesisFinalPulse, ref _hapticSynthesisFinalPulseHandle);
            ReleaseHapticSynthesisVaultHandle(vault, BufferID.ShinobuHapticSynthesisMockImpulses, ref _hapticSynthesisMockImpulsesHandle);
            ReleaseHapticSynthesisVaultHandle(vault, BufferID.ShinobuHapticSynthesisTelemetryRing, ref _hapticSynthesisTelemetryRingHandle);
            ReleaseHapticSynthesisVaultHandle(vault, BufferID.ShinobuHapticSynthesisProfileTable, ref _hapticSynthesisProfilesHandle);
            ReleaseHapticSynthesisVaultHandle(vault, BufferID.ShinobuHapticSynthesisTuning, ref _hapticSynthesisTuningHandle);
#if UNITY_EDITOR
            ReleaseHapticSynthesisVaultHandle(vault, BufferID.ShinobuHapticSynthesisCsvScratch, ref _hapticSynthesisCsvScratchHandle);
#endif
            _hapticSynthesisInitialized = false;
            _hapticSynthesisAccumulator = 0f;
            _hapticSynthesisTelemetryCursor = 0;
            _hapticSynthesisScheduledForPostSimulation = false;
            _hapticSynthesisScheduledTelemetryIndex = -1;
            _hapticSynthesisScheduledFrame = 0u;
            _hapticSynthesisScheduledSchemeHash = 0u;
            _hapticSynthesisPinnedBufferMask = 0u;
            _hapticSynthesisPinnedBufferVault = null;
            _hapticSynthesisScheduleTimestamp = 0L;
        }

        private static void ReleaseHapticSynthesisVaultHandle<T>(
            IDataVault vault,
            BufferID expectedBufferId,
            ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (vault != null && IsHapticSynthesisHandle(in handle, expectedBufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private bool TryAcquireInputWriteBuffer<T>(
            BufferID expectedBufferId,
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            return TryAcquireInputWriteBuffer(expectedBufferId, in handle, requiredLength, out buffer, out _);
        }

        private bool TryAcquireInputWriteBuffer<T>(
            BufferID expectedBufferId,
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T> buffer,
            out IDataVault lockVault) where T : struct
        {
            buffer = default;
            lockVault = _dataVault;
            IDataVault vault = lockVault;
            if (vault == null || !IsHapticSynthesisHandle(in handle, expectedBufferId))
            {
                lockVault = null;
                return false;
            }

            if (!vault.TryAcquireWriteLock(in handle, SystemID.CoreDeterminism, out buffer))
            {
                lockVault = null;
                return false;
            }

            bool handedOff = false;
            try
            {
                if (!buffer.IsCreated || buffer.Length < requiredLength)
                    return false;

                handedOff = true;
                return true;
            }
            finally
            {
                if (!handedOff)
                {
                    vault.ReleaseWriteLock(in handle, SystemID.CoreDeterminism);
                    buffer = default;
                    lockVault = null;
                }
            }
        }

        private void ReleaseInputWriteBuffer<T>(BufferID expectedBufferId, in VaultGenerationHandle<T> handle) where T : struct
        {
            ReleaseInputWriteBuffer(_dataVault, expectedBufferId, in handle);
        }

        private static void ReleaseInputWriteBuffer<T>(IDataVault vault, BufferID expectedBufferId, in VaultGenerationHandle<T> handle) where T : struct
        {
            if (vault != null && IsHapticSynthesisHandle(in handle, expectedBufferId))
                vault.ReleaseWriteLock(in handle, SystemID.CoreDeterminism);
        }

        private bool ClearHapticSynthesisBuffer<T>(
            BufferID expectedBufferId,
            in VaultGenerationHandle<T> handle,
            int requiredLength) where T : struct
        {
            if (!TryAcquireInputWriteBuffer(expectedBufferId, in handle, requiredLength, out NativeArray<T> buffer, out IDataVault writeVault))
                return false;

            try
            {
                UnsafeUtility.MemClear(
                    NativeArrayUnsafeUtility.GetUnsafePtr(buffer),
                    (long)buffer.Length * UnsafeUtility.SizeOf<T>());
            }
            finally
            {
                ReleaseInputWriteBuffer(writeVault, expectedBufferId, in handle);
            }

            return true;
        }

        private int WriteDefaultHapticProfiles()
        {
            if (!TryAcquireInputWriteBuffer(
                    BufferID.ShinobuHapticSynthesisProfileTable,
                    in _hapticSynthesisProfilesHandle,
                    HapticSynthesisMath.ProfileCapacity,
                    out NativeArray<HapticProfileDTO> profiles,
                    out IDataVault profilesVault))
            {
                return 0;
            }

            try
            {
                return HapticSynthesisMath.WriteDefaultProfiles(profiles);
            }
            finally
            {
                ReleaseInputWriteBuffer(profilesVault, BufferID.ShinobuHapticSynthesisProfileTable, in _hapticSynthesisProfilesHandle);
            }
        }

        private bool TryWriteHapticSynthesisTuning(in HapticTuningDTO tuning)
        {
            if (!TryAcquireInputWriteBuffer(
                    BufferID.ShinobuHapticSynthesisTuning,
                    in _hapticSynthesisTuningHandle,
                    1,
                    out NativeArray<HapticTuningDTO> tuningBuffer,
                    out IDataVault tuningVault))
            {
                return false;
            }

            try
            {
                tuningBuffer[0] = tuning;
            }
            finally
            {
                ReleaseInputWriteBuffer(tuningVault, BufferID.ShinobuHapticSynthesisTuning, in _hapticSynthesisTuningHandle);
            }

            return true;
        }

        private bool TryWriteHapticSynthesisFinalPulse(in HapticPulseSignal pulse)
        {
            if (!TryAcquireInputWriteBuffer(
                    BufferID.ShinobuHapticSynthesisFinalPulse,
                    in _hapticSynthesisFinalPulseHandle,
                    1,
                    out NativeArray<HapticPulseSignal> finalPulse,
                    out IDataVault finalPulseVault))
            {
                return false;
            }

            try
            {
                finalPulse[0] = pulse;
            }
            finally
            {
                ReleaseInputWriteBuffer(finalPulseVault, BufferID.ShinobuHapticSynthesisFinalPulse, in _hapticSynthesisFinalPulseHandle);
            }

            return true;
        }

        private bool TryPinHapticSynthesisScheduleBuffers(bool includeMockImpulses)
        {
            ReleaseHapticSynthesisSchedulePins();
            IDataVault vault = _dataVault;
            if (vault == null)
            {
                return false;
            }

            _hapticSynthesisPinnedBufferVault = vault;
            bool pinned = false;
            try
            {
                if (!TryLockHapticSynthesisScheduleBuffer(vault, BufferID.ShinobuHapticSynthesisPulses, HapticSynthesisPinPulses) ||
                    !TryLockHapticSynthesisScheduleBuffer(vault, BufferID.ShinobuHapticSynthesisFinalPulse, HapticSynthesisPinFinalPulse) ||
                    !TryLockHapticSynthesisScheduleBuffer(vault, BufferID.ShinobuHapticSynthesisTelemetryRing, HapticSynthesisPinTelemetry) ||
                    !TryLockHapticSynthesisScheduleBuffer(vault, BufferID.ShinobuHapticSynthesisProfileTable, HapticSynthesisPinProfiles) ||
                    !TryLockHapticSynthesisScheduleBuffer(vault, BufferID.ShinobuHapticSynthesisTuning, HapticSynthesisPinTuning) ||
                    (includeMockImpulses &&
                     !TryLockHapticSynthesisScheduleBuffer(vault, BufferID.ShinobuHapticSynthesisMockImpulses, HapticSynthesisPinMockImpulses)))
                {
                    return false;
                }

                if (!TryValidateHapticSynthesisScheduleBuffers(includeMockImpulses))
                    return false;

                pinned = true;
                return true;
            }
            finally
            {
                if (!pinned)
                    ReleaseHapticSynthesisSchedulePins();
            }
        }

        private void ReleaseHapticSynthesisSchedulePins()
        {
            IDataVault vault = _hapticSynthesisPinnedBufferVault;
            uint mask = _hapticSynthesisPinnedBufferMask;
            _hapticSynthesisPinnedBufferMask = 0u;
            _hapticSynthesisPinnedBufferVault = null;
            if (vault != null && mask != 0u)
            {
                TryUnlockHapticSynthesisScheduleBuffer(vault, mask, HapticSynthesisPinMockImpulses, BufferID.ShinobuHapticSynthesisMockImpulses);
                TryUnlockHapticSynthesisScheduleBuffer(vault, mask, HapticSynthesisPinTuning, BufferID.ShinobuHapticSynthesisTuning);
                TryUnlockHapticSynthesisScheduleBuffer(vault, mask, HapticSynthesisPinProfiles, BufferID.ShinobuHapticSynthesisProfileTable);
                TryUnlockHapticSynthesisScheduleBuffer(vault, mask, HapticSynthesisPinTelemetry, BufferID.ShinobuHapticSynthesisTelemetryRing);
                TryUnlockHapticSynthesisScheduleBuffer(vault, mask, HapticSynthesisPinFinalPulse, BufferID.ShinobuHapticSynthesisFinalPulse);
                TryUnlockHapticSynthesisScheduleBuffer(vault, mask, HapticSynthesisPinPulses, BufferID.ShinobuHapticSynthesisPulses);
            }
        }

        private bool TryValidateHapticSynthesisScheduleBuffers(bool includeMockImpulses)
        {
            return TryReadHapticSynthesisRequiredBuffers(
                       out _,
                       out _,
                       out _,
                       out _,
                       out _) &&
                   (!includeMockImpulses ||
                    TryReadHapticInputBuffer(
                        BufferID.ShinobuHapticSynthesisMockImpulses,
                        in _hapticSynthesisMockImpulsesHandle,
                        HapticSynthesisMath.MockImpulseCapacity,
                        out NativeArray<HapticPhysicalImpulseDTO>.ReadOnly mockImpulses) &&
                    mockImpulses.IsCreated);
        }

        private bool TryLockHapticSynthesisScheduleBuffer(IDataVault vault, BufferID bufferId, uint pinBit)
        {
            if ((_hapticSynthesisPinnedBufferMask & pinBit) != 0u)
                return true;

            if (vault == null || !vault.TryLockBuffer(bufferId, SystemID.CoreDeterminism))
                return false;

            _hapticSynthesisPinnedBufferMask |= pinBit;
            return true;
        }

        private static void TryUnlockHapticSynthesisScheduleBuffer(IDataVault vault, uint pinMask, uint pinBit, BufferID bufferId)
        {
            if ((pinMask & pinBit) != 0u)
                vault.TryUnlockBuffer(bufferId, SystemID.CoreDeterminism);
        }

        private static bool IsHapticSynthesisHandle<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId) where T : struct
        {
            return handle.BufferID == (uint)expectedBufferId &&
                   handle.SystemID == (uint)SystemID.CoreDeterminism &&
                   handle.Generation != 0u;
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
