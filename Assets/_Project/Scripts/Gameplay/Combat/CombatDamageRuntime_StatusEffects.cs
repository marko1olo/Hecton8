using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Hecton8.Gameplay
{
    public static partial class CombatDamageRuntime
    {
        private static int s_x001DirectSignalPushDropCount_CombatDamageRuntime_StatusEffects;

        private static int s_x001CombatDamageRuntime_StatusEffectsSignalPushDropCount;
        private const int StatusEffectTelemetryCapacity = 300;
        private const int StatusEffectTelemetryCursorLength = 2;
        private const int StatusEffectTelemetryWriteCursor = 0;
        private const int StatusEffectTelemetryLastAnomaly = 1;
        private const int StatusEffectCounterLength = 9;
        private const int StatusEffectCounterActive = 0;
        private const int StatusEffectCounterResult = 1;
        private const int StatusEffectCounterRequests = 2;
        private const int StatusEffectCounterDroppedRequests = 3;
        private const int StatusEffectCounterDamageMilli = 4;
        private const int StatusEffectCounterVfxSignals = 5;
        private const int StatusEffectCounterSolveMicroseconds = 6;
        private const int StatusEffectCounterAnomaly = 7;
        private const int StatusEffectCounterDamageSignals = 8;
        private const int StatusEffectRequestBudget = MaxQueuedSignals;
        private const int StatusEffectVfxRequestBudget = MaxTargets;
        private const int StatusEffectDamageSignalBudget = MaxTargets;
        private const int StatusEffectStateSizeBytes = 64;
        private const int StatusEffectTelemetrySizeBytes = 64;
        private const ushort StatusEffectEnvironmentHazardSourceId = 4;
        private const float StatusEffectMinCadenceSeconds = 0.10f;
        private const float StatusEffectMaxCadenceSeconds = 1.00f;
        private const float StatusEffectDefaultStunMobilityScale = 0.35f;
        private const float StatusEffectToxicBubbleMinDamage = 0.01f;
        private const float StatusEffectToxicBubbleRadiusMeters = 0.45f;
        private const uint StatusEffectTelemetryMagicLow = 0x53544658u; // STFX
        private const uint StatusEffectTelemetryMagicHigh = 0x33313921u; // 319!
        private const uint StatusEffectTuningMagic = 0x53453139u; // SE19
        private const byte StatusFsmInactive = 0;
        private const byte StatusFsmActive = 1;
        private const byte StatusFsmExpiring = 2;

        private static VaultGenerationHandle<CombatStatusEffectRequest> _statusEffectRequestsHandle;
        private static VaultGenerationHandle<CombatStatusEffectState> _statusEffectStatesHandle;
        private static VaultGenerationHandle<CombatStatusEffectTuning> _statusEffectTuningHandle;
        private static VaultGenerationHandle<CombatStatusEffectTelemetryEntry> _statusEffectTelemetryHandle;
        private static VaultGenerationHandle<int> _statusEffectTelemetryCursorHandle;
        private static VaultGenerationHandle<CombatStatusEffectCounterLane> _statusEffectCountersHandle;
        private static VaultGenerationHandle<CombatStatusEffectVfxRequest> _statusEffectVfxRequestsHandle;
        private static VaultGenerationHandle<CombatDamageSignal> _statusEffectDamageSignalsHandle;
        private static IDataVault _statusEffectVault;
        private static IDataVault _pendingStatusEffectVault;
        private static int _queuedStatusEffectRequestCount;
        private static float _statusEvaluationAccumulatorSeconds;
        private static float _statusEffectLastQualityWeight01 = 1f;
        private static float _statusLastEvaluationDeltaSeconds;
        private static uint _statusEffectFrameIndex;
        private static long _statusScheduleTicks;
        private static int _statusLockedVaultBufferCount;
        private static bool _statusLockedArmorVaultBuffers;
        private static bool _statusScheduledSimulationWork;
        private static bool _statusEffectTelemetryDumpedThisSession;
        private static bool _statusEffectVaultRebindPending;

        private ref struct CombatStatusEffectVaultViews
        {
            public NativeArray<CombatStatusEffectRequest> Requests;
            public NativeArray<CombatStatusEffectState> States;
            public NativeArray<CombatStatusEffectTuning> Tuning;
            public NativeArray<CombatStatusEffectTelemetryEntry> TelemetryRing;
            public NativeArray<int> TelemetryCursor;
            public NativeArray<CombatStatusEffectCounterLane> Counters;
            public NativeArray<CombatStatusEffectVfxRequest> VfxRequests;
            public NativeArray<CombatDamageSignal> DamageSignals;
        }

        private ref struct CombatStatusEffectReadOnlyVaultViews
        {
            public NativeArray<CombatStatusEffectState>.ReadOnly States;
            public NativeArray<CombatStatusEffectTuning>.ReadOnly Tuning;
            public NativeArray<CombatStatusEffectTelemetryEntry>.ReadOnly TelemetryRing;
            public NativeArray<int>.ReadOnly TelemetryCursor;
            public NativeArray<CombatStatusEffectCounterLane>.ReadOnly Counters;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        public struct CombatStatusEffectRequest
        {
            [FieldOffset(0)] public int TargetId;
            [FieldOffset(4)] public int SourceId;
            [FieldOffset(8)] public ulong StatusEffectMask;
            [FieldOffset(16)] public float DurationSeconds;
            [FieldOffset(20)] public float Magnitude;
            [FieldOffset(24)] public double3 ImpactAup;
            [FieldOffset(48)] public uint Frame;
            [FieldOffset(52)] public uint DamageType;
            [FieldOffset(56)] public byte Flags;
            [FieldOffset(57)] private byte _pad0;
            [FieldOffset(58)] private ushort _pad1;
            [FieldOffset(60)] private uint _pad2;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        internal struct CombatStatusEffectState
        {
            [FieldOffset(0)] public ulong StatusEffectMask;
            [FieldOffset(8)] public float4 Durations0123;
            [FieldOffset(24)] public float4 Durations4567;
            [FieldOffset(40)] public uint LastAppliedFrame;
            [FieldOffset(44)] public uint LastChangedFrame;
            [FieldOffset(48)] public byte BleedFsm;
            [FieldOffset(49)] public byte CrushFsm;
            [FieldOffset(50)] public byte IrradiationFsm;
            [FieldOffset(51)] public byte HypoxiaFsm;
            [FieldOffset(52)] public byte PoisonFsm;
            [FieldOffset(53)] public byte BurnFsm;
            [FieldOffset(54)] public byte StunFsm;
            [FieldOffset(55)] public byte BrittleFsm;
            [FieldOffset(56)] public uint StateHash;
            [FieldOffset(60)] public float FractureSeconds;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        internal struct CombatStatusEffectTuning
        {
            [FieldOffset(0)] public uint Magic;
            [FieldOffset(4)] public float BleedingDamagePerSecond;
            [FieldOffset(8)] public float CrushedDamagePerSecond;
            [FieldOffset(12)] public float IrradiatedDamagePerSecond;
            [FieldOffset(16)] public float HypoxiaDamagePerSecond;
            [FieldOffset(20)] public float PoisonDamagePerSecond;
            [FieldOffset(24)] public float BurningDamagePerSecond;
            [FieldOffset(28)] public float StunMobilityScale;
            [FieldOffset(32)] public float MinCadenceSeconds;
            [FieldOffset(36)] public float MaxCadenceSeconds;
            [FieldOffset(40)] public float GlobalQualityWeight01;
            [FieldOffset(44)] public float ToxicBubbleScale;
            [FieldOffset(48)] public uint Flags;
            [FieldOffset(52)] public uint ProfileHash;
            [FieldOffset(56)] public ulong Reserved;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        internal struct CombatStatusEffectTelemetryEntry
        {
            [FieldOffset(0)] public uint FrameIndex;
            [FieldOffset(4)] public uint TargetHash;
            [FieldOffset(8)] public ulong StatusEffectMask;
            [FieldOffset(16)] public uint StateHash;
            [FieldOffset(20)] public uint AnomalyHash;
            [FieldOffset(24)] public float PreviousHealth;
            [FieldOffset(28)] public float NextHealth;
            [FieldOffset(32)] public float AppliedDamage;
            [FieldOffset(36)] public float DeltaTime;
            [FieldOffset(40)] public float GlobalQualityWeight01;
            [FieldOffset(44)] public ushort Flags;
            [FieldOffset(46)] public byte FsmPacked0;
            [FieldOffset(47)] public byte FsmPacked1;
            [FieldOffset(48)] public uint ActiveCount;
            [FieldOffset(52)] public uint RequestCount;
            [FieldOffset(56)] public uint EstimatedMicroseconds;
            [FieldOffset(60)] public uint Reserved;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        internal struct CombatStatusEffectCounterLane
        {
            [FieldOffset(0)] public int Value;
            [FieldOffset(4)] public uint Reserved0;
            [FieldOffset(8)] public ulong Reserved1;
            [FieldOffset(16)] public ulong Reserved2;
            [FieldOffset(24)] public ulong Reserved3;
            [FieldOffset(32)] public ulong Reserved4;
            [FieldOffset(40)] public ulong Reserved5;
            [FieldOffset(48)] public ulong Reserved6;
            [FieldOffset(56)] public ulong Reserved7;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        internal struct CombatStatusEffectVfxRequest
        {
            [FieldOffset(0)] public double3 PositionAup;
            [FieldOffset(24)] public float Intensity01;
            [FieldOffset(28)] public float RadiusMeters;
            [FieldOffset(32)] public uint Frame;
            [FieldOffset(36)] public uint SourceHash;
            [FieldOffset(40)] public uint EffectHash;
            [FieldOffset(44)] public uint Flags;
            [FieldOffset(48)] public ulong Reserved0;
            [FieldOffset(56)] public ulong Reserved1;
        }

        public static bool TryQueueStatusEffect(
            int targetId,
            ulong statusEffectMask,
            float durationSeconds,
            int sourceId = StatusEffectEnvironmentHazardSourceId,
            float magnitude = 0f)
        {
            if (targetId == 0 || statusEffectMask == 0UL)
                return false;

            if (_statusJobScheduled || _queuedStatusEffectRequestCount >= StatusEffectRequestBudget)
                return false;

            if (!TryResolveStatusEffectVaultViews(out CombatStatusEffectVaultViews statusViews, ensure: false) ||
                !statusViews.Requests.IsCreated)
                return false;

            CombatStatusEffectRequest request = new CombatStatusEffectRequest
            {
                TargetId = targetId,
                SourceId = sourceId,
                StatusEffectMask = statusEffectMask & CombatStatusBits.KnownRuntimeMask64,
                DurationSeconds = SanitizeStatusDuration(durationSeconds, statusEffectMask),
                Magnitude = math.max(0f, math.select(0f, magnitude, math.isfinite(magnitude))),
                ImpactAup = double3.zero,
                Frame = ResolveStatusEffectFrameIndex(),
                DamageType = ResolveStatusDamageType(statusEffectMask),
                Flags = 0
            };

            if (request.StatusEffectMask == 0UL)
                return false;

            int writeIndex = _queuedStatusEffectRequestCount;
            if ((uint)writeIndex >= (uint)StatusEffectRequestBudget ||
                (uint)writeIndex >= (uint)statusViews.Requests.Length ||
                _statusEffectVault == null ||
                !_statusEffectVault.TryAcquireWriteLock(in _statusEffectRequestsHandle, SystemID.GameplayCombat, out NativeArray<CombatStatusEffectRequest> requests))
            {
                return false;
            }

            try
            {
                if (!requests.IsCreated || (uint)writeIndex >= (uint)requests.Length)
                    return false;

                requests[writeIndex] = request;
                _queuedStatusEffectRequestCount = writeIndex + 1;
                return true;
            }
            finally
            {
                _statusEffectVault.ReleaseWriteLock(in _statusEffectRequestsHandle, SystemID.GameplayCombat);
            }
        }

        public static int QueueMockStatusPlague(int firstTargetId, int count, uint seed)
        {
            if (count <= 0)
                return 0;

            EnsureInitialized();
            if (!TryResolveCombatDamageVaultViews(out CombatDamageVaultViews views, ensure: false))
                return 0;

            int queued = 0;
            int targetCount = math.max(0, _targetCount);
            int maxCount = math.min(count, targetCount);
            for (int i = 0; i < maxCount; i++)
            {
                int slot = (int)(math.hash(new uint2(seed, unchecked((uint)i))) % (uint)math.max(1, targetCount));
                int targetId = firstTargetId != 0 ? firstTargetId + i : views.InstanceIds[slot];
                ulong bit = ((i & 1) == 0) ? CombatStatusBits.Poisoned64 : CombatStatusBits.Bleeding64;
                if (TryQueueStatusEffect(targetId, bit, ResolveDefaultStatusDuration64(bit), StatusEffectEnvironmentHazardSourceId, 1f))
                    queued++;
            }

            return queued;
        }

        public static bool GenerateMockStatusEffects(int count, uint seed)
        {
            EnsureInitialized();
            if (!TryResolveStatusEffectVaultViews(out CombatStatusEffectVaultViews statusViews, ensure: false) ||
                !statusViews.States.IsCreated ||
                _damageJobScheduled ||
                _statusJobScheduled ||
                _targetCount <= 0 ||
                !TryResolveCombatDamageVaultViews(out CombatDamageVaultViews views, ensure: false))
            {
                return false;
            }

            int writeCount = math.min(math.max(0, count), _targetCount);
            if (writeCount <= 0)
                return false;

            if (!TryLockStatusEffectVaultBuffersForJobs(includeSimulationBuffers: false))
                return false;

            if (!TryLockCombatDamageVaultBuffersForJobs(out int damageLockedCount))
            {
                UnlockStatusEffectVaultBuffersForJobs();
                return false;
            }

            try
            {
                if (!TryResolveStatusEffectVaultViews(out statusViews, ensure: false) ||
                    !TryResolveCombatDamageVaultViews(out views, ensure: false) ||
                    !statusViews.States.IsCreated ||
                    !views.StatusMasks.IsCreated ||
                    !views.StatusDurations0123.IsCreated ||
                    !views.LegacyStatusDurations4567.IsCreated ||
                    !views.BrittleDurations.IsCreated ||
                    (uint)writeCount > (uint)statusViews.States.Length ||
                    (uint)writeCount > (uint)views.StatusMasks.Length ||
                    (uint)writeCount > (uint)views.StatusDurations0123.Length ||
                    (uint)writeCount > (uint)views.LegacyStatusDurations4567.Length ||
                    (uint)writeCount > (uint)views.BrittleDurations.Length)
                {
                    return false;
                }

                GenerateMockStatusEffectsJob job = new GenerateMockStatusEffectsJob
                {
                    Seed = seed,
                    Count = writeCount,
                    FrameIndex = ResolveStatusEffectFrameIndex(),
                    StatusEffectStates = statusViews.States,
                    StatusMasks = views.StatusMasks,
                    StatusDurations0123 = views.StatusDurations0123,
                    LegacyStatusDurations4567 = views.LegacyStatusDurations4567,
                    BrittleDurations = views.BrittleDurations
                };
                job.Run(writeCount);
                return true;
            }
            finally
            {
                UnlockCombatDamageVaultBuffersForJobs(damageLockedCount);
                UnlockStatusEffectVaultBuffersForJobs();
            }
        }

        public static bool TryGetStatusEffectMask(int targetId, out ulong statusEffectMask)
        {
            statusEffectMask = 0UL;
            if (_statusJobScheduled ||
                !TryResolveStatusEffectReadOnlyVaultViews(out CombatStatusEffectReadOnlyVaultViews statusViews) ||
                !TryResolveCombatDamageReadOnlyViews(out CombatDamageReadOnlyVaultViews views))
                return false;

            if (!TryFindTargetSlotInLookup(views.TargetLookupKeys, views.TargetLookupSlots, targetId, out int slot))
                return false;

            if ((uint)slot >= (uint)statusViews.States.Length)
                return false;

            statusEffectMask = statusViews.States[slot].StatusEffectMask;
            return true;
        }

        public static bool TryGetStatusMobilityScale(int targetId, out float mobilityScale)
        {
            mobilityScale = 1f;
            if (_statusJobScheduled ||
                !TryResolveStatusEffectReadOnlyVaultViews(out CombatStatusEffectReadOnlyVaultViews statusViews) ||
                !TryResolveCombatDamageReadOnlyViews(out CombatDamageReadOnlyVaultViews views))
                return false;

            if (!TryFindTargetSlotInLookup(views.TargetLookupKeys, views.TargetLookupSlots, targetId, out int slot))
                return false;

            if ((uint)slot >= (uint)statusViews.States.Length)
                return false;

            ulong mask = statusViews.States[slot].StatusEffectMask;
            mobilityScale = ResolveStatusMobilityScale(mask, ReadStatusEffectTuning(in statusViews));
            return true;
        }

        internal static bool TryGetStatusEffectTuning(out CombatStatusEffectTuning tuning)
        {
            tuning = default;
            if (_statusJobScheduled ||
                !TryResolveStatusEffectReadOnlyVaultViews(out CombatStatusEffectReadOnlyVaultViews statusViews) ||
                statusViews.Tuning.Length == 0)
                return false;

            tuning = ReadStatusEffectTuning(in statusViews);
            return true;
        }

        internal static bool WriteStatusEffectTuning(in CombatStatusEffectTuning tuning)
        {
            return EnsureStatusEffectStorage() && TryWriteStatusEffectTuningLocked(in tuning);
        }

        internal static bool TryGetLastStatusEffectTelemetry(out CombatStatusEffectTelemetryEntry entry)
        {
            entry = default;
            if (_statusJobScheduled ||
                !TryResolveStatusEffectReadOnlyVaultViews(out CombatStatusEffectReadOnlyVaultViews statusViews) ||
                statusViews.TelemetryRing.Length <= 0 ||
                (uint)StatusEffectTelemetryWriteCursor >= (uint)statusViews.TelemetryCursor.Length)
            {
                return false;
            }

            int cursor = statusViews.TelemetryCursor[StatusEffectTelemetryWriteCursor];
            if (cursor <= 0)
                return false;

            int ringLength = math.min(StatusEffectTelemetryCapacity, statusViews.TelemetryRing.Length);
            int index = (int)((uint)(cursor - 1) % (uint)ringLength);
            entry = statusViews.TelemetryRing[index];
            return true;
        }

        internal static bool TryGetStatusEffectDebugSnapshot(int slot, out Vector3 worldPoint, out ulong statusEffectMask)
        {
            worldPoint = default;
            statusEffectMask = 0UL;
            if (_statusJobScheduled ||
                !TryResolveStatusEffectReadOnlyVaultViews(out CombatStatusEffectReadOnlyVaultViews statusViews) ||
                _receiverTransforms == null ||
                (uint)slot >= (uint)_targetCount ||
                (uint)slot >= (uint)_receiverTransforms.Length ||
                (uint)slot >= (uint)statusViews.States.Length)
            {
                return false;
            }

            statusEffectMask = statusViews.States[slot].StatusEffectMask;
            Transform receiverTransform = _receiverTransforms[slot];
            if (receiverTransform == null || statusEffectMask == 0UL)
                return false;

            worldPoint = receiverTransform.position + Vector3.up;
            return math.all(math.isfinite(new float3(worldPoint.x, worldPoint.y, worldPoint.z)));
        }

        internal static int ReadStatusEffectDebugTargetCount()
        {
            if (_statusJobScheduled ||
                !TryResolveStatusEffectReadOnlyVaultViews(out CombatStatusEffectReadOnlyVaultViews statusViews) ||
                _receiverTransforms == null)
                return 0;

            return math.min(
                math.max(0, _targetCount),
                math.min(statusViews.States.Length, _receiverTransforms.Length));
        }

#if UNITY_EDITOR
        internal static bool TryLoadStatusEffectProfilesCsv(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path) || !EnsureStatusEffectStorage())
                return false;

            byte[] bytes = File.ReadAllBytes(path);
            ReadOnlySpan<byte> span = bytes;
            CombatStatusEffectTuning tuning = ReadStatusEffectTuning();
            int rows = 0;
            int cursor = 0;
            while (cursor < span.Length)
            {
                ReadOnlySpan<byte> line = ReadLine(span, ref cursor);
                if (line.Length == 0 || line[0] == (byte)'#')
                    continue;

                int tokenCursor = 0;
                ReadOnlySpan<byte> name = ReadToken(line, ref tokenCursor);
                ReadOnlySpan<byte> dpsToken = ReadToken(line, ref tokenCursor);
                ReadOnlySpan<byte> durationToken = ReadToken(line, ref tokenCursor);
                ReadOnlySpan<byte> stunScaleToken = ReadToken(line, ref tokenCursor);
                if (AsciiEquals(name, "name") || !TryParsePositiveFloat(dpsToken, out float dps))
                    continue;

                TryParsePositiveFloat(durationToken, out float duration);
                if (AsciiEquals(name, "poison") || AsciiEquals(name, "poisoned"))
                    tuning.PoisonDamagePerSecond = dps;
                else if (AsciiEquals(name, "bleed") || AsciiEquals(name, "bleeding"))
                    tuning.BleedingDamagePerSecond = dps;
                else if (AsciiEquals(name, "burn") || AsciiEquals(name, "burning"))
                    tuning.BurningDamagePerSecond = dps;
                else if (AsciiEquals(name, "crush") || AsciiEquals(name, "crushed"))
                    tuning.CrushedDamagePerSecond = dps;
                else if (AsciiEquals(name, "irradiated") || AsciiEquals(name, "radiation"))
                    tuning.IrradiatedDamagePerSecond = dps;
                else if (AsciiEquals(name, "hypoxia"))
                    tuning.HypoxiaDamagePerSecond = dps;

                if (AsciiEquals(name, "stun") || AsciiEquals(name, "stunned"))
                {
                    if (TryParsePositiveFloat(stunScaleToken, out float stunScale))
                        tuning.StunMobilityScale = math.saturate(stunScale);
                    else
                        tuning.StunMobilityScale = math.saturate(duration);
                }

                rows++;
            }

            return rows > 0 && WriteStatusEffectTuning(in tuning);
        }
#endif

        private static void RequestStatusEffectVaultRebind(IDataVault previousVault, IDataVault currentVault)
        {
            if (ReferenceEquals(_statusEffectVault, currentVault))
            {
                _pendingStatusEffectVault = null;
                _statusEffectVaultRebindPending = false;
                return;
            }

            if (_statusJobScheduled)
            {
                if (!_statusJobHandle.IsCompleted)
                {
                    _pendingStatusEffectVault = currentVault;
                    _statusEffectVaultRebindPending = true;
                    return;
                }

                DispatcherJobSwap.TryFinalizeCompleted(ref _statusJobHandle);
                _statusJobScheduled = false;
                CompleteStatusEffectFrame();
            }

            ApplyStatusEffectVaultRebind(previousVault ?? _statusEffectVault, currentVault);
        }

        private static void TryApplyPendingStatusEffectVaultRebind()
        {
            if (!_statusEffectVaultRebindPending || (_statusJobScheduled && !_statusJobHandle.IsCompleted))
                return;

            if (_statusJobScheduled)
            {
                DispatcherJobSwap.TryFinalizeCompleted(ref _statusJobHandle);
                _statusJobScheduled = false;
                CompleteStatusEffectFrame();
            }

            ApplyStatusEffectVaultRebind(_statusEffectVault, _pendingStatusEffectVault);
        }

        private static void ApplyStatusEffectVaultRebind(IDataVault previousVault, IDataVault currentVault)
        {
            _pendingStatusEffectVault = null;
            _statusEffectVaultRebindPending = false;

            if (ReferenceEquals(previousVault, currentVault))
            {
                _statusEffectVault = currentVault;
                return;
            }

            ReleaseStatusEffectVaultHandles(previousVault);
            ClearStatusEffectVaultViews();
            _queuedStatusEffectRequestCount = 0;
            _statusEvaluationAccumulatorSeconds = 0f;
            _statusEffectLastQualityWeight01 = 1f;
            _statusLastEvaluationDeltaSeconds = 0f;
            _statusEffectFrameIndex = 0u;
            _statusScheduleTicks = 0L;
            _statusLockedVaultBufferCount = 0;
            _statusLockedArmorVaultBuffers = false;
            _statusScheduledSimulationWork = false;
            _statusEffectTelemetryDumpedThisSession = false;
            _statusEffectVault = currentVault;

            if (IsCombatDamageVaultInitialized() &&
                currentVault != null &&
                !currentVault.IsAllocationLocked &&
                !currentVault.IsCompactionFenceActive)
            {
                EnsureStatusEffectStorage();
            }
        }

        private static bool EnsureStatusEffectStorage()
        {
            SignalBus<CombatDamageSignal>.EnsureInitialized();
            SignalBus<BubbleSpawnSignal>.EnsureInitialized();

            TryApplyPendingStatusEffectVaultRebind();
            if (_statusEffectVault == null)
                _statusEffectVault = _combatDataVault;
            if (_statusEffectVault == null)
                return false;

            if (TryResolveStatusEffectVaultViews(out CombatStatusEffectVaultViews existingViews, ensure: false))
            {
                if (existingViews.Tuning.IsCreated &&
                    existingViews.Tuning.Length > 0 &&
                    existingViews.Tuning[0].Magic == StatusEffectTuningMagic)
                {
                    return true;
                }

                return TryWriteStatusEffectTuningLocked(CreateDefaultStatusEffectTuning(ResolveStatusEffectQualityWeight01()));
            }

            if (!TryResolveStatusEffectVaultViews(out CombatStatusEffectVaultViews views, ensure: true))
                return false;

            if (!TryLockStatusEffectVaultBuffersForJobs(includeSimulationBuffers: false))
                return false;

            try
            {
                if (!TryResolveStatusEffectVaultViews(out views, ensure: false))
                    return false;

                ClearStatusEffectTelemetryImmediate(ref views);
                ClearStatusEffectCountersImmediate(ref views);
                CombatStatusEffectTuning tuning = views.Tuning[0];
                if (tuning.Magic != StatusEffectTuningMagic)
                    views.Tuning[0] = CreateDefaultStatusEffectTuning(ResolveStatusEffectQualityWeight01());

                return true;
            }
            finally
            {
                UnlockStatusEffectVaultBuffersForJobs();
            }
        }

        private static bool TryWriteStatusEffectTuningLocked(in CombatStatusEffectTuning tuning)
        {
            if (_statusEffectVault == null ||
                !_statusEffectVault.TryAcquireWriteLock(in _statusEffectTuningHandle, SystemID.GameplayCombat, out NativeArray<CombatStatusEffectTuning> tuningArray))
            {
                return false;
            }

            try
            {
                if (!tuningArray.IsCreated || tuningArray.Length == 0)
                    return false;

                tuningArray[0] = SanitizeStatusEffectTuning(tuning);
                return true;
            }
            finally
            {
                _statusEffectVault.ReleaseWriteLock(in _statusEffectTuningHandle, SystemID.GameplayCombat);
            }
        }

        private static bool TryResolveStatusEffectVaultViews(out CombatStatusEffectVaultViews views, bool ensure)
        {
            views = default;
            if (_statusEffectVault == null || _statusEffectVault.IsCompactionFenceActive)
                return false;

            return TryResolveStatusEffectBuffer(
                       BufferID.Shinobu319StatusEffectRequests,
                       StatusEffectRequestBudget,
                       NativeArrayOptions.UninitializedMemory,
                       ensure,
                       ref _statusEffectRequestsHandle,
                       out views.Requests) &&
                   TryResolveStatusEffectBuffer(
                       BufferID.Shinobu319StatusEffectStates,
                       MaxTargets,
                       NativeArrayOptions.ClearMemory,
                       ensure,
                       ref _statusEffectStatesHandle,
                       out views.States) &&
                   TryResolveStatusEffectBuffer(
                       BufferID.Shinobu319StatusEffectTuning,
                       1,
                       NativeArrayOptions.ClearMemory,
                       ensure,
                       ref _statusEffectTuningHandle,
                       out views.Tuning) &&
                   TryResolveStatusEffectBuffer(
                       BufferID.Shinobu319StatusEffectTelemetryRing,
                       StatusEffectTelemetryCapacity,
                       NativeArrayOptions.UninitializedMemory,
                       ensure,
                       ref _statusEffectTelemetryHandle,
                       out views.TelemetryRing) &&
                   TryResolveStatusEffectBuffer(
                       BufferID.Shinobu319StatusEffectTelemetryCursor,
                       StatusEffectTelemetryCursorLength,
                       NativeArrayOptions.UninitializedMemory,
                       ensure,
                       ref _statusEffectTelemetryCursorHandle,
                       out views.TelemetryCursor) &&
                   TryResolveStatusEffectBuffer(
                       BufferID.Shinobu319StatusEffectCounters,
                       StatusEffectCounterLength,
                       NativeArrayOptions.UninitializedMemory,
                       ensure,
                       ref _statusEffectCountersHandle,
                       out views.Counters) &&
                   TryResolveStatusEffectBuffer(
                       BufferID.Shinobu319StatusEffectVfxRequests,
                       StatusEffectVfxRequestBudget,
                       NativeArrayOptions.UninitializedMemory,
                       ensure,
                       ref _statusEffectVfxRequestsHandle,
                       out views.VfxRequests) &&
                   TryResolveStatusEffectBuffer(
                       BufferID.Shinobu319StatusEffectDamageSignals,
                       StatusEffectDamageSignalBudget,
                       NativeArrayOptions.UninitializedMemory,
                       ensure,
                       ref _statusEffectDamageSignalsHandle,
                       out views.DamageSignals);
        }

        private static bool TryResolveStatusEffectReadOnlyVaultViews(out CombatStatusEffectReadOnlyVaultViews views)
        {
            views = default;
            if (_statusEffectVault == null || _statusEffectVault.IsCompactionFenceActive)
                return false;

            return TryResolveStatusEffectReadOnlyBuffer(
                       BufferID.Shinobu319StatusEffectStates,
                       MaxTargets,
                       in _statusEffectStatesHandle,
                       out views.States) &&
                   TryResolveStatusEffectReadOnlyBuffer(
                       BufferID.Shinobu319StatusEffectTuning,
                       1,
                       in _statusEffectTuningHandle,
                       out views.Tuning) &&
                   TryResolveStatusEffectReadOnlyBuffer(
                       BufferID.Shinobu319StatusEffectTelemetryRing,
                       StatusEffectTelemetryCapacity,
                       in _statusEffectTelemetryHandle,
                       out views.TelemetryRing) &&
                   TryResolveStatusEffectReadOnlyBuffer(
                       BufferID.Shinobu319StatusEffectTelemetryCursor,
                       StatusEffectTelemetryCursorLength,
                       in _statusEffectTelemetryCursorHandle,
                       out views.TelemetryCursor) &&
                   TryResolveStatusEffectReadOnlyBuffer(
                       BufferID.Shinobu319StatusEffectCounters,
                       StatusEffectCounterLength,
                       in _statusEffectCountersHandle,
                       out views.Counters);
        }

        private static bool TryResolveStatusEffectBuffer<T>(
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            bool ensure,
            ref VaultGenerationHandle<T> handle,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (_statusEffectVault == null || requiredLength <= 0 || _statusEffectVault.IsCompactionFenceActive)
                return false;

            if (IsStatusEffectVaultHandleCreated(in handle, bufferId))
            {
                VaultGenerationHandle<T> readHandle = handle;
                if ((ensure ? _statusEffectVault.TryResolveHandle(in readHandle, out buffer) : _statusEffectVault.TryReadHandle(in readHandle, out buffer)) &&
                    buffer.IsCreated &&
                    (uint)requiredLength <= (uint)buffer.Length)
                {
                    return true;
                }
            }

            if (!ensure)
                return false;

            handle = _statusEffectVault.EnsureGenerationHandle<T>(bufferId, requiredLength, SystemID.GameplayCombat, options);
            return _statusEffectVault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool TryResolveStatusEffectReadOnlyBuffer<T>(
            BufferID bufferId,
            int requiredLength,
            in VaultGenerationHandle<T> handle,
            out NativeArray<T>.ReadOnly buffer)
            where T : struct
        {
            buffer = default;
            if (_statusEffectVault == null ||
                requiredLength <= 0 ||
                _statusEffectVault.IsCompactionFenceActive ||
                !IsStatusEffectVaultHandleCreated(in handle, bufferId))
            {
                return false;
            }

            return _statusEffectVault.TryReadOnlyHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   (uint)requiredLength <= (uint)buffer.Length;
        }

        private static bool IsStatusEffectVaultHandleCreated<T>(in VaultGenerationHandle<T> handle, BufferID bufferId)
            where T : struct
        {
            return handle.BufferID == (uint)bufferId &&
                   handle.Generation != 0u &&
                   handle.SystemID == (uint)SystemID.GameplayCombat;
        }

        private static void ShutdownStatusEffectStorage()
        {
            ReleaseStatusEffectVaultHandles(_statusEffectVault);
            ClearStatusEffectVaultViews();

            _statusEffectVault = null;
            _pendingStatusEffectVault = null;
            _statusEffectVaultRebindPending = false;
            _queuedStatusEffectRequestCount = 0;
            _statusEvaluationAccumulatorSeconds = 0f;
            _statusEffectLastQualityWeight01 = 1f;
            _statusLastEvaluationDeltaSeconds = 0f;
            _statusEffectFrameIndex = 0u;
            _statusScheduleTicks = 0L;
            _statusLockedVaultBufferCount = 0;
            _statusLockedArmorVaultBuffers = false;
            _statusScheduledSimulationWork = false;
            _statusEffectTelemetryDumpedThisSession = false;
        }

        private static void ReleaseStatusEffectVaultHandles(IDataVault vault)
        {
            if (vault == null)
                return;

            if (_statusEffectRequestsHandle.BufferID != 0u)
                vault.ReleaseBuffer(in _statusEffectRequestsHandle);
            if (_statusEffectStatesHandle.BufferID != 0u)
                vault.ReleaseBuffer(in _statusEffectStatesHandle);
            if (_statusEffectTuningHandle.BufferID != 0u)
                vault.ReleaseBuffer(in _statusEffectTuningHandle);
            if (_statusEffectTelemetryHandle.BufferID != 0u)
                vault.ReleaseBuffer(in _statusEffectTelemetryHandle);
            if (_statusEffectTelemetryCursorHandle.BufferID != 0u)
                vault.ReleaseBuffer(in _statusEffectTelemetryCursorHandle);
            if (_statusEffectCountersHandle.BufferID != 0u)
                vault.ReleaseBuffer(in _statusEffectCountersHandle);
            if (_statusEffectVfxRequestsHandle.BufferID != 0u)
                vault.ReleaseBuffer(in _statusEffectVfxRequestsHandle);
            if (_statusEffectDamageSignalsHandle.BufferID != 0u)
                vault.ReleaseBuffer(in _statusEffectDamageSignalsHandle);
        }

        private static void ClearStatusEffectVaultViews()
        {
            _statusEffectRequestsHandle = default;
            _statusEffectStatesHandle = default;
            _statusEffectTuningHandle = default;
            _statusEffectTelemetryHandle = default;
            _statusEffectTelemetryCursorHandle = default;
            _statusEffectCountersHandle = default;
            _statusEffectVfxRequestsHandle = default;
            _statusEffectDamageSignalsHandle = default;
        }

        private static bool TryScheduleStatusEffectJobs(float deltaTime)
        {
            if (_damageJobScheduled)
                return false;

            if (!EnsureStatusEffectStorage())
                return false;
            if (!TryResolveStatusEffectVaultViews(out CombatStatusEffectVaultViews statusViews, ensure: false))
                return false;

            RefreshRuntimePolicy();
            float statusQualityWeight01 = ResolveStatusEffectQualityWeight01();
            _statusEffectLastQualityWeight01 = statusQualityWeight01;
            _statusEffectFrameIndex = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            float safeDelta = math.max(0f, math.select(0f, deltaTime, math.isfinite(deltaTime)));
            _statusEvaluationAccumulatorSeconds = math.min(_statusEvaluationAccumulatorSeconds + safeDelta, 4f);
            CombatStatusEffectTuning tuning = ReadStatusEffectTuning(ref statusViews);
            tuning.GlobalQualityWeight01 = statusQualityWeight01;
            tuning.MinCadenceSeconds = StatusEffectMinCadenceSeconds;
            tuning.MaxCadenceSeconds = StatusEffectMaxCadenceSeconds;

            bool hasRequestWork = _queuedStatusEffectRequestCount > 0;
            float cadenceSeconds = ResolveStatusEffectCadenceSeconds(statusQualityWeight01, in tuning);
            bool hasSimulationWork = _statusEvaluationAccumulatorSeconds >= cadenceSeconds;
            if (!hasRequestWork && !hasSimulationWork)
                return false;

            if (hasSimulationWork && !SignalBus<CombatDamageSignal>.HasNativeStorage)
                return false;

            if (!TryResolveCombatDamageVaultViews(out CombatDamageVaultViews damageViews, ensure: false))
                return false;

            float evaluationDelta = hasSimulationWork ? _statusEvaluationAccumulatorSeconds : 0f;
            ArmorPenetrationVaultViews armorViews = default;
            bool lockedArmorVaultBuffers = false;
            if (hasSimulationWork)
            {
                if (!TryResolveArmorPenetrationVaultViews(out armorViews, ensure: true) ||
                    !armorViews.TargetRootAups.IsCreated)
                {
                    return false;
                }

                if (!TryLockArmorVaultBuffersForJobs())
                    return false;

                lockedArmorVaultBuffers = true;
            }

            if (!TryLockStatusEffectVaultBuffersForJobs(hasSimulationWork))
            {
                if (lockedArmorVaultBuffers)
                    UnlockArmorVaultBuffersForJobs();

                return false;
            }

            if (!TryLockCombatDamageVaultBuffersForJobs(out int damageLockedCount))
            {
                UnlockStatusEffectVaultBuffersForJobs();
                if (lockedArmorVaultBuffers)
                    UnlockArmorVaultBuffersForJobs();

                return false;
            }

            if (!TryResolveStatusEffectVaultViews(out statusViews, ensure: false) ||
                !TryResolveCombatDamageVaultViews(out damageViews, ensure: false) ||
                (hasSimulationWork && !TryResolveArmorPenetrationVaultViews(out armorViews, ensure: false)))
            {
                UnlockCombatDamageVaultBuffersForJobs(damageLockedCount);
                UnlockStatusEffectVaultBuffersForJobs();
                if (lockedArmorVaultBuffers)
                    UnlockArmorVaultBuffersForJobs();

                return false;
            }

            if (hasSimulationWork)
                RefreshArmorTargetSnapshotsLocked(ref armorViews);
            if (!CanUseStatusEffectJobBuffers(hasSimulationWork, ref statusViews, ref damageViews, in armorViews))
            {
                UnlockCombatDamageVaultBuffersForJobs(damageLockedCount);
                UnlockStatusEffectVaultBuffersForJobs();
                if (lockedArmorVaultBuffers)
                    UnlockArmorVaultBuffersForJobs();

                return false;
            }

            statusViews.Tuning[0] = SanitizeStatusEffectTuning(tuning);
            _statusLockedArmorVaultBuffers = lockedArmorVaultBuffers;
            _statusScheduledSimulationWork = hasSimulationWork;
            ClearStatusEffectCountersImmediate(ref statusViews);
            _statusLastEvaluationDeltaSeconds = evaluationDelta;
            if (hasSimulationWork)
                _statusEvaluationAccumulatorSeconds = 0f;

            ApplyStatusEffectRequestsJob applyJob = new ApplyStatusEffectRequestsJob
            {
                Requests = statusViews.Requests,
                RequestCount = _queuedStatusEffectRequestCount,
                TargetLookupKeys = damageViews.TargetLookupKeys,
                TargetLookupSlots = damageViews.TargetLookupSlots,
                StatusEffectStates = statusViews.States,
                StatusMasks = damageViews.StatusMasks,
                StatusDurations0123 = damageViews.StatusDurations0123,
                LegacyStatusDurations4567 = damageViews.LegacyStatusDurations4567,
                BrittleDurations = damageViews.BrittleDurations,
                Counters = statusViews.Counters,
                RequestBudget = StatusEffectRequestBudget
            };
            JobHandle applyHandle = applyJob.Schedule();

            if (!hasSimulationWork)
            {
                _statusScheduleTicks = Stopwatch.GetTimestamp();
                _statusJobHandle = applyHandle;
                _statusJobScheduled = true;
                H8Memory.RegisterActiveJob(CombatDamageMemoryOwner, _statusJobHandle);
                H8Memory.RegisterActiveJob(SystemID.GameplayCombat, _statusJobHandle);
                JobHandle.ScheduleBatchedJobs();
                return true;
            }

            EvaluateStatusEffectsJob evaluateJob = new EvaluateStatusEffectsJob
            {
                DeltaTime = evaluationDelta,
                FrameIndex = _statusEffectFrameIndex,
                InstanceIds = damageViews.InstanceIds,
                Health = damageViews.Health,
                MaxHealth = damageViews.MaxHealth,
                InvMaxHealth = damageViews.InvMaxHealth,
                TargetRootAups = armorViews.TargetRootAups,
                StatusEffectStates = statusViews.States,
                StatusMasks = damageViews.StatusMasks,
                StatusDurations0123 = damageViews.StatusDurations0123,
                LegacyStatusDurations4567 = damageViews.LegacyStatusDurations4567,
                BrittleDurations = damageViews.BrittleDurations,
                ResultsBySlot = damageViews.StatusResults,
                ResultActiveBySlot = damageViews.StatusResultActive,
                Tuning = statusViews.Tuning,
                TelemetryCursor = statusViews.TelemetryCursor,
                Counters = statusViews.Counters,
                VfxRequests = statusViews.VfxRequests,
                DamageSignals = statusViews.DamageSignals,
                GlobalQualityWeight01 = statusQualityWeight01
            };
            _statusScheduleTicks = Stopwatch.GetTimestamp();
            _statusJobHandle = evaluateJob.Schedule(_targetCount, ResolveStatusEffectBatchSize(statusQualityWeight01), applyHandle);
            _statusJobScheduled = true;
            H8Memory.RegisterActiveJob(CombatDamageMemoryOwner, _statusJobHandle);
            H8Memory.RegisterActiveJob(SystemID.GameplayCombat, _statusJobHandle);
            JobHandle.ScheduleBatchedJobs();
            return true;
        }

        private static void CompleteStatusEffectFrame()
        {
            _queuedStatusEffectRequestCount = 0;
            if (!TryResolveStatusEffectVaultViews(out CombatStatusEffectVaultViews statusViews, ensure: false) ||
                !statusViews.Counters.IsCreated)
            {
                UnlockCombatDamageVaultBuffersForJobs(CombatDamageVaultJobLockCount);
                UnlockStatusEffectVaultBuffersForJobs();
                UnlockStatusEffectBorrowedArmorBuffers();
                _statusScheduledSimulationWork = false;
                return;
            }

            uint elapsedMicroseconds = ResolveStatusElapsedMicroseconds();
            WriteStatusCounter(StatusEffectCounterSolveMicroseconds, unchecked((int)elapsedMicroseconds), ref statusViews);
            PublishStatusEffectDamageSignals(ReadStatusCounter(StatusEffectCounterDamageSignals, ref statusViews), ref statusViews);
            PublishStatusEffectVfxRequests(ReadStatusCounter(StatusEffectCounterVfxSignals, ref statusViews), ref statusViews);
            if (_statusScheduledSimulationWork)
                WriteStatusResultTelemetryRows(ref statusViews);
            WriteStatusCompletionTelemetry(elapsedMicroseconds, ref statusViews);
            uint anomalyHash = unchecked((uint)ReadStatusCounter(StatusEffectCounterAnomaly, ref statusViews));
            if (elapsedMicroseconds > 200u)
            {
                anomalyHash = anomalyHash != 0u ? anomalyHash : 0x53190200u;
                WriteStatusCounter(StatusEffectCounterAnomaly, unchecked((int)anomalyHash), ref statusViews);
            }

            if (anomalyHash != 0u)
                TryDumpStatusEffectTelemetry(anomalyHash, ref statusViews);

            UnlockCombatDamageVaultBuffersForJobs(CombatDamageVaultJobLockCount);
            UnlockStatusEffectVaultBuffersForJobs();
            UnlockStatusEffectBorrowedArmorBuffers();
            _statusScheduledSimulationWork = false;
        }

        private static void PublishStatusEffectDamageSignals(int requestedCount, ref CombatStatusEffectVaultViews statusViews)
        {
            if (requestedCount <= 0 ||
                !statusViews.DamageSignals.IsCreated)
            {
                return;
            }

            if (!SignalBus<CombatDamageSignal>.HasNativeStorage)
            {
                WriteStatusCounter(StatusEffectCounterAnomaly, unchecked((int)0x5319D002u), ref statusViews);
                return;
            }

            int count = math.min(requestedCount, statusViews.DamageSignals.Length);
            for (int i = 0; i < count; i++)
            {
                CombatDamageSignal signal = statusViews.DamageSignals[i];
                if (signal.Magnitude <= 0f || !math.isfinite(signal.Magnitude) || !math.all(math.isfinite(signal.ImpactAup)))
                    continue;

                if (!SignalBus<CombatDamageSignal>.TryPushTracked(in signal, ref s_x001DirectSignalPushDropCount_CombatDamageRuntime_StatusEffects))
                    WriteStatusCounter(StatusEffectCounterAnomaly, unchecked((int)0x5319D001u), ref statusViews);
            }
        }

        private static void PublishStatusEffectVfxRequests(int requestedCount, ref CombatStatusEffectVaultViews statusViews)
        {
            if (requestedCount <= 0 ||
                !statusViews.VfxRequests.IsCreated ||
                !SignalBus<BubbleSpawnSignal>.HasNativeStorage)
            {
                return;
            }

            int count = math.min(requestedCount, statusViews.VfxRequests.Length);
            for (int i = 0; i < count; i++)
            {
                CombatStatusEffectVfxRequest request = statusViews.VfxRequests[i];
                if (!math.all(math.isfinite(request.PositionAup)))
                    continue;

                BubbleSpawnSignal signal = new BubbleSpawnSignal
                {
                    PositionAup = AbsoluteUniversePosition.FromAbsolutePosition(request.PositionAup),
                    Direction = new float3(0f, 1f, 0f),
                    Intensity01 = math.saturate(request.Intensity01),
                    RadiusMeters = math.max(0f, request.RadiusMeters),
                    Frame = request.Frame,
                    SourceHash = request.SourceHash,
                    Flags = request.Flags,
                    Reserved0 = request.EffectHash,
                    Reserved1 = request.Reserved0,
                    Reserved2 = request.Reserved1,
                    Reserved3 = 0UL,
                    Reserved4 = 0UL,
                    Reserved5 = 0UL
                };
                SignalBus<BubbleSpawnSignal>.TryPushTracked(in signal, ref s_x001CombatDamageRuntime_StatusEffectsSignalPushDropCount);
            }
        }

        private static void UnlockStatusEffectBorrowedArmorBuffers()
        {
            if (!_statusLockedArmorVaultBuffers)
                return;

            UnlockArmorVaultBuffersForJobs();
            _statusLockedArmorVaultBuffers = false;
        }

        private static bool TryLockStatusEffectVaultBuffersForJobs(bool includeSimulationBuffers)
        {
            if (_statusEffectVault == null)
                return false;

            int locked = 0;
            if (!_statusEffectVault.TryLockBuffer(BufferID.Shinobu319StatusEffectRequests, SystemID.GameplayCombat)) return false;
            locked++;
            if (!_statusEffectVault.TryLockBuffer(BufferID.Shinobu319StatusEffectStates, SystemID.GameplayCombat)) { UnlockStatusEffectVaultBuffersForJobs(locked); return false; }
            locked++;
            if (!_statusEffectVault.TryLockBuffer(BufferID.Shinobu319StatusEffectTuning, SystemID.GameplayCombat)) { UnlockStatusEffectVaultBuffersForJobs(locked); return false; }
            locked++;
            if (!_statusEffectVault.TryLockBuffer(BufferID.Shinobu319StatusEffectTelemetryRing, SystemID.GameplayCombat)) { UnlockStatusEffectVaultBuffersForJobs(locked); return false; }
            locked++;
            if (!_statusEffectVault.TryLockBuffer(BufferID.Shinobu319StatusEffectTelemetryCursor, SystemID.GameplayCombat)) { UnlockStatusEffectVaultBuffersForJobs(locked); return false; }
            locked++;
            if (!_statusEffectVault.TryLockBuffer(BufferID.Shinobu319StatusEffectCounters, SystemID.GameplayCombat)) { UnlockStatusEffectVaultBuffersForJobs(locked); return false; }
            locked++;

            if (!includeSimulationBuffers)
            {
                _statusLockedVaultBufferCount = locked;
                return true;
            }

            if (!_statusEffectVault.TryLockBuffer(BufferID.Shinobu319StatusEffectVfxRequests, SystemID.GameplayCombat)) { UnlockStatusEffectVaultBuffersForJobs(locked); return false; }
            locked++;
            if (!_statusEffectVault.TryLockBuffer(BufferID.Shinobu319StatusEffectDamageSignals, SystemID.GameplayCombat)) { UnlockStatusEffectVaultBuffersForJobs(locked); return false; }
            locked++;
            _statusLockedVaultBufferCount = locked;
            return true;
        }

        private static bool CanUseStatusEffectJobBuffers(
            bool includeSimulationBuffers,
            ref CombatStatusEffectVaultViews statusViews,
            ref CombatDamageVaultViews damageViews,
            in ArmorPenetrationVaultViews armorViews)
        {
            int targetCount = math.max(0, _targetCount);
            if (!statusViews.Requests.IsCreated ||
                statusViews.Requests.Length < StatusEffectRequestBudget ||
                !damageViews.TargetLookupKeys.IsCreated ||
                damageViews.TargetLookupKeys.Length < CombatTargetLookupCapacity ||
                !damageViews.TargetLookupSlots.IsCreated ||
                damageViews.TargetLookupSlots.Length < CombatTargetLookupCapacity ||
                !statusViews.States.IsCreated ||
                (uint)targetCount > (uint)statusViews.States.Length ||
                !damageViews.StatusMasks.IsCreated ||
                (uint)targetCount > (uint)damageViews.StatusMasks.Length ||
                !damageViews.StatusDurations0123.IsCreated ||
                (uint)targetCount > (uint)damageViews.StatusDurations0123.Length ||
                !damageViews.LegacyStatusDurations4567.IsCreated ||
                (uint)targetCount > (uint)damageViews.LegacyStatusDurations4567.Length ||
                !damageViews.BrittleDurations.IsCreated ||
                (uint)targetCount > (uint)damageViews.BrittleDurations.Length ||
                !statusViews.Tuning.IsCreated ||
                statusViews.Tuning.Length <= 0 ||
                !statusViews.TelemetryRing.IsCreated ||
                statusViews.TelemetryRing.Length <= 0 ||
                !statusViews.TelemetryCursor.IsCreated ||
                statusViews.TelemetryCursor.Length < StatusEffectTelemetryCursorLength ||
                !statusViews.Counters.IsCreated ||
                statusViews.Counters.Length < StatusEffectCounterLength)
            {
                return false;
            }

            if (!includeSimulationBuffers)
                return true;

            return armorViews.TargetRootAups.IsCreated &&
                   (uint)targetCount <= (uint)armorViews.TargetRootAups.Length &&
                   damageViews.InstanceIds.IsCreated &&
                   (uint)targetCount <= (uint)damageViews.InstanceIds.Length &&
                   damageViews.Health.IsCreated &&
                   (uint)targetCount <= (uint)damageViews.Health.Length &&
                   damageViews.MaxHealth.IsCreated &&
                   (uint)targetCount <= (uint)damageViews.MaxHealth.Length &&
                   damageViews.InvMaxHealth.IsCreated &&
                   (uint)targetCount <= (uint)damageViews.InvMaxHealth.Length &&
                   damageViews.StatusResults.IsCreated &&
                   (uint)targetCount <= (uint)damageViews.StatusResults.Length &&
                   damageViews.StatusResultActive.IsCreated &&
                   (uint)targetCount <= (uint)damageViews.StatusResultActive.Length &&
                   statusViews.VfxRequests.IsCreated &&
                   (uint)targetCount <= (uint)statusViews.VfxRequests.Length &&
                   statusViews.DamageSignals.IsCreated &&
                   (uint)targetCount <= (uint)statusViews.DamageSignals.Length;
        }

        private static void UnlockStatusEffectVaultBuffersForJobs()
        {
            int lockedCount = _statusLockedVaultBufferCount;
            _statusLockedVaultBufferCount = 0;
            UnlockStatusEffectVaultBuffersForJobs(lockedCount);
        }

        private static void UnlockStatusEffectVaultBuffersForJobs(int lockedCount)
        {
            _statusLockedVaultBufferCount = 0;
            if (_statusEffectVault == null)
                return;

            if (lockedCount >= 8) _statusEffectVault.TryUnlockBuffer(BufferID.Shinobu319StatusEffectDamageSignals, SystemID.GameplayCombat);
            if (lockedCount >= 7) _statusEffectVault.TryUnlockBuffer(BufferID.Shinobu319StatusEffectVfxRequests, SystemID.GameplayCombat);
            if (lockedCount >= 6) _statusEffectVault.TryUnlockBuffer(BufferID.Shinobu319StatusEffectCounters, SystemID.GameplayCombat);
            if (lockedCount >= 5) _statusEffectVault.TryUnlockBuffer(BufferID.Shinobu319StatusEffectTelemetryCursor, SystemID.GameplayCombat);
            if (lockedCount >= 4) _statusEffectVault.TryUnlockBuffer(BufferID.Shinobu319StatusEffectTelemetryRing, SystemID.GameplayCombat);
            if (lockedCount >= 3) _statusEffectVault.TryUnlockBuffer(BufferID.Shinobu319StatusEffectTuning, SystemID.GameplayCombat);
            if (lockedCount >= 2) _statusEffectVault.TryUnlockBuffer(BufferID.Shinobu319StatusEffectStates, SystemID.GameplayCombat);
            if (lockedCount >= 1) _statusEffectVault.TryUnlockBuffer(BufferID.Shinobu319StatusEffectRequests, SystemID.GameplayCombat);
        }

        private static bool MoveTargetSideState(int sourceSlot, int destinationSlot)
        {
            bool armorLocked = false;
            bool statusLocked = false;
            int armorLockCount = 0;
            IDataVault statusVault = null;
            NativeArray<CombatStatusEffectState> states = default;

            if (!TryAcquireArmorTargetWriteLocks(out ArmorPenetrationVaultViews armorViews, out armorLockCount))
                return false;

            armorLocked = true;
            try
            {
                if (!TryAcquireStatusEffectStatesWriteLock(out statusVault, out states, out statusLocked))
                    return false;

                return MoveTargetSideStateLocked(sourceSlot, destinationSlot, statusLocked, states, ref armorViews);
            }
            finally
            {
                if (statusLocked)
                    ReleaseStatusEffectStatesWriteLock(statusVault, statusLocked);
                if (armorLocked)
                    ReleaseArmorTargetWriteLocks(armorLockCount);
            }
        }

        private static bool ClearTargetSideState(int slot)
        {
            bool armorLocked = false;
            bool statusLocked = false;
            int armorLockCount = 0;
            IDataVault statusVault = null;
            NativeArray<CombatStatusEffectState> states = default;

            if (!TryAcquireArmorTargetWriteLocks(out ArmorPenetrationVaultViews armorViews, out armorLockCount))
                return false;

            armorLocked = true;
            try
            {
                if (!TryAcquireStatusEffectStatesWriteLock(out statusVault, out states, out statusLocked))
                    return false;

                return ClearTargetSideStateLocked(slot, statusLocked, states, ref armorViews);
            }
            finally
            {
                if (statusLocked)
                    ReleaseStatusEffectStatesWriteLock(statusVault, statusLocked);
                if (armorLocked)
                    ReleaseArmorTargetWriteLocks(armorLockCount);
            }
        }

        private static bool ResetStatusEffectSlot(int slot)
        {
            if (!TryAcquireStatusEffectStatesWriteLock(
                    out IDataVault statusVault,
                    out NativeArray<CombatStatusEffectState> states,
                    out bool statusLocked))
                return false;

            try
            {
                return ResetStatusEffectSlotLocked(slot, statusLocked, states);
            }
            finally
            {
                ReleaseStatusEffectStatesWriteLock(statusVault, statusLocked);
            }
        }

        private static bool TryAcquireStatusEffectStatesWriteLock(
            out IDataVault statusVault,
            out NativeArray<CombatStatusEffectState> states,
            out bool statusLocked)
        {
            statusVault = _statusEffectVault;
            states = default;
            statusLocked = false;
            if (statusVault == null || _statusEffectStatesHandle.BufferID == 0u)
                return true;

            if (!statusVault.TryAcquireWriteLock(in _statusEffectStatesHandle, SystemID.GameplayCombat, out states))
                return false;

            statusLocked = true;
            return true;
        }

        private static void ReleaseStatusEffectStatesWriteLock(IDataVault statusVault, bool statusLocked)
        {
            if (statusLocked && statusVault != null)
                statusVault.ReleaseWriteLock(in _statusEffectStatesHandle, SystemID.GameplayCombat);
        }

        private static bool ResetStatusEffectSlotLocked(int slot, bool hasStatusStorage, NativeArray<CombatStatusEffectState> states)
        {
            if (!hasStatusStorage)
                return true;

            if (!states.IsCreated || (uint)slot >= (uint)states.Length)
                return false;

            states[slot] = default;
            return true;
        }

        private static bool MoveTargetSideStateLocked(
            int sourceSlot,
            int destinationSlot,
            bool hasStatusStorage,
            NativeArray<CombatStatusEffectState> states,
            ref ArmorPenetrationVaultViews armorViews)
        {
            if (hasStatusStorage &&
                (!states.IsCreated ||
                 (uint)destinationSlot >= (uint)states.Length ||
                 (uint)sourceSlot >= (uint)states.Length))
            {
                return false;
            }

            if (!CanUseArmorTargetSlot(in armorViews, sourceSlot) ||
                !CanUseArmorTargetSlot(in armorViews, destinationSlot))
            {
                return false;
            }

            if (hasStatusStorage)
            {
                states[destinationSlot] = states[sourceSlot];
                states[sourceSlot] = default;
            }

            armorViews.TargetArmorProfiles[destinationSlot] = armorViews.TargetArmorProfiles[sourceSlot];
            armorViews.TargetRootAups[destinationSlot] = armorViews.TargetRootAups[sourceSlot];
            armorViews.TargetRotations[destinationSlot] = armorViews.TargetRotations[sourceSlot];
            armorViews.TargetHalfExtents[destinationSlot] = armorViews.TargetHalfExtents[sourceSlot];
            armorViews.TargetArmorProfiles[sourceSlot] = default;
            armorViews.TargetRootAups[sourceSlot] = double3.zero;
            armorViews.TargetRotations[sourceSlot] = quaternion.identity;
            armorViews.TargetHalfExtents[sourceSlot] = float3.zero;
            return true;
        }

        private static bool ClearTargetSideStateLocked(
            int slot,
            bool hasStatusStorage,
            NativeArray<CombatStatusEffectState> states,
            ref ArmorPenetrationVaultViews armorViews)
        {
            if (hasStatusStorage &&
                (!states.IsCreated || (uint)slot >= (uint)states.Length))
            {
                return false;
            }

            if (!CanUseArmorTargetSlot(in armorViews, slot))
                return false;

            if (hasStatusStorage)
                states[slot] = default;

            armorViews.TargetArmorProfiles[slot] = default;
            armorViews.TargetRootAups[slot] = double3.zero;
            armorViews.TargetRotations[slot] = quaternion.identity;
            armorViews.TargetHalfExtents[slot] = float3.zero;
            return true;
        }

        private static ReadOnlySpan<byte> ReadLine(ReadOnlySpan<byte> span, ref int cursor)
        {
            int start = cursor;
            while (cursor < span.Length && span[cursor] != (byte)'\n' && span[cursor] != (byte)'\r')
                cursor++;

            int end = cursor;
            while (cursor < span.Length && (span[cursor] == (byte)'\n' || span[cursor] == (byte)'\r'))
                cursor++;

            return TrimAscii(span.Slice(start, end - start));
        }

        private static ReadOnlySpan<byte> ReadToken(ReadOnlySpan<byte> line, ref int cursor)
        {
            int start = cursor;
            while (cursor < line.Length && line[cursor] != (byte)',')
                cursor++;

            int end = cursor;
            if (cursor < line.Length && line[cursor] == (byte)',')
                cursor++;

            return TrimAscii(line.Slice(start, end - start));
        }

        private static ReadOnlySpan<byte> TrimAscii(ReadOnlySpan<byte> span)
        {
            int start = 0;
            int end = span.Length;
            while (start < end && span[start] <= 32)
                start++;
            while (end > start && span[end - 1] <= 32)
                end--;
            return span.Slice(start, end - start);
        }

        private static bool AsciiEquals(ReadOnlySpan<byte> token, string literal)
        {
            if (token.Length != literal.Length)
                return false;

            for (int i = 0; i < token.Length; i++)
            {
                byte value = token[i];
                if (value >= (byte)'A' && value <= (byte)'Z')
                    value = (byte)(value + 32);
                if (value != (byte)literal[i])
                    return false;
            }

            return true;
        }

        private static bool TryParsePositiveFloat(ReadOnlySpan<byte> token, out float value)
        {
            value = 0f;
            if (token.Length == 0)
                return false;

            float integer = 0f;
            float fraction = 0f;
            float divisor = 1f;
            bool afterDecimal = false;
            bool any = false;
            for (int i = 0; i < token.Length; i++)
            {
                byte c = token[i];
                if (c == (byte)'.')
                {
                    afterDecimal = true;
                    continue;
                }

                if (c < (byte)'0' || c > (byte)'9')
                    return false;

                any = true;
                float digit = c - (byte)'0';
                if (afterDecimal)
                {
                    divisor *= 10f;
                    fraction += digit / divisor;
                }
                else
                {
                    integer = (integer * 10f) + digit;
                }
            }

            value = integer + fraction;
            return any && math.isfinite(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static CombatStatusEffectTuning SanitizeStatusEffectTuning(in CombatStatusEffectTuning input)
        {
            CombatStatusEffectTuning tuning = input;
            tuning.Magic = StatusEffectTuningMagic;
            tuning.BleedingDamagePerSecond = SanitizeNonNegativeFinite(tuning.BleedingDamagePerSecond, 0f);
            tuning.CrushedDamagePerSecond = SanitizeNonNegativeFinite(tuning.CrushedDamagePerSecond, 0f);
            tuning.IrradiatedDamagePerSecond = SanitizeNonNegativeFinite(tuning.IrradiatedDamagePerSecond, 0f);
            tuning.HypoxiaDamagePerSecond = SanitizeNonNegativeFinite(tuning.HypoxiaDamagePerSecond, 0f);
            tuning.PoisonDamagePerSecond = SanitizeNonNegativeFinite(tuning.PoisonDamagePerSecond, 0f);
            tuning.BurningDamagePerSecond = SanitizeNonNegativeFinite(tuning.BurningDamagePerSecond, 0f);
            tuning.StunMobilityScale = math.saturate(math.select(StatusEffectDefaultStunMobilityScale, tuning.StunMobilityScale, math.isfinite(tuning.StunMobilityScale)));
            tuning.MinCadenceSeconds = math.clamp(SanitizeNonNegativeFinite(tuning.MinCadenceSeconds, StatusEffectMinCadenceSeconds), 0.02f, 2f);
            tuning.MaxCadenceSeconds = math.max(tuning.MinCadenceSeconds, SanitizeNonNegativeFinite(tuning.MaxCadenceSeconds, StatusEffectMaxCadenceSeconds));
            tuning.GlobalQualityWeight01 = SanitizeQualityWeight01(tuning.GlobalQualityWeight01);
            tuning.ToxicBubbleScale = SanitizeNonNegativeFinite(tuning.ToxicBubbleScale, 1f);
            return tuning;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeNonNegativeFinite(float value, float fallback)
        {
            return math.max(0f, math.select(fallback, value, math.isfinite(value)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static CombatStatusEffectTuning ReadStatusEffectTuning()
        {
            return TryResolveStatusEffectReadOnlyVaultViews(out CombatStatusEffectReadOnlyVaultViews statusViews)
                ? ReadStatusEffectTuning(in statusViews)
                : CreateDefaultStatusEffectTuning(_statusEffectLastQualityWeight01);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static CombatStatusEffectTuning ReadStatusEffectTuning(in CombatStatusEffectReadOnlyVaultViews statusViews)
        {
            if (statusViews.Tuning.Length > 0 && statusViews.Tuning[0].Magic == StatusEffectTuningMagic)
                return statusViews.Tuning[0];

            return CreateDefaultStatusEffectTuning(_statusEffectLastQualityWeight01);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static CombatStatusEffectTuning ReadStatusEffectTuning(ref CombatStatusEffectVaultViews statusViews)
        {
            if (statusViews.Tuning.IsCreated && statusViews.Tuning.Length > 0 && statusViews.Tuning[0].Magic == StatusEffectTuningMagic)
                return statusViews.Tuning[0];

            return CreateDefaultStatusEffectTuning(_statusEffectLastQualityWeight01);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static CombatStatusEffectTuning CreateDefaultStatusEffectTuning(float qualityWeight01)
        {
            return new CombatStatusEffectTuning
            {
                Magic = StatusEffectTuningMagic,
                BleedingDamagePerSecond = BleedingDamagePerSlowTick,
                CrushedDamagePerSecond = CrushedDamagePerSlowTick,
                IrradiatedDamagePerSecond = IrradiatedDamagePerSlowTick,
                HypoxiaDamagePerSecond = HypoxiaDamagePerSlowTick,
                PoisonDamagePerSecond = PoisonDamagePerSlowTick,
                BurningDamagePerSecond = BurningDamagePerSlowTick,
                StunMobilityScale = StatusEffectDefaultStunMobilityScale,
                MinCadenceSeconds = StatusEffectMinCadenceSeconds,
                MaxCadenceSeconds = StatusEffectMaxCadenceSeconds,
                GlobalQualityWeight01 = SanitizeQualityWeight01(qualityWeight01),
                ToxicBubbleScale = 1f,
                Flags = 0u,
                ProfileHash = StatusEffectTuningMagic,
                Reserved = 0UL
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeStatusDuration(float durationSeconds, ulong statusMask)
        {
            float sanitized = math.max(0f, math.select(0f, durationSeconds, math.isfinite(durationSeconds)));
            return sanitized > 0f ? sanitized : ResolveDefaultStatusDuration64(statusMask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ResolveStatusDamageType(ulong statusMask)
        {
            uint toxic = (uint)math.select(0, (int)CombatDamageTypes.Toxic, (statusMask & CombatStatusBits.Poisoned64) != 0UL);
            uint thermal = (uint)math.select(0, (int)CombatDamageTypes.Thermal, (statusMask & CombatStatusBits.Burning64) != 0UL);
            uint pressure = (uint)math.select(0, (int)CombatDamageTypes.Pressure, (statusMask & (CombatStatusBits.Crushed64 | CombatStatusBits.Hypoxia64)) != 0UL);
            uint radioactive = (uint)math.select(0, (int)CombatDamageTypes.Radioactive, (statusMask & CombatStatusBits.Irradiated64) != 0UL);
            return toxic | thermal | pressure | radioactive;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveDefaultStatusDuration64(ulong statusMask)
        {
            float duration = DefaultThermalStatusDurationSeconds;
            duration = math.select(duration, DefaultBleedStatusDurationSeconds, (statusMask & CombatStatusBits.Bleeding64) != 0UL);
            duration = math.select(duration, DefaultPoisonStatusDurationSeconds, (statusMask & CombatStatusBits.Poisoned64) != 0UL);
            duration = math.select(duration, DefaultStunStatusDurationSeconds, (statusMask & CombatStatusBits.Stunned64) != 0UL);
            duration = math.select(duration, CrippledMobilityDurationSeconds, (statusMask & CombatStatusBits.Fractured64) != 0UL);
            return duration;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveStatusEffectCadenceSeconds(float qualityWeight01, in CombatStatusEffectTuning tuning)
        {
            float q = SmoothStep01(qualityWeight01);
            float minCadence = math.max(0.02f, tuning.MinCadenceSeconds);
            float maxCadence = math.max(minCadence, tuning.MaxCadenceSeconds);
            return math.lerp(maxCadence, minCadence, q);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ResolveStatusEffectBatchSize(float qualityWeight01)
        {
            float q = SmoothStep01(qualityWeight01);
            return math.clamp((int)math.round(math.lerp(128f, 32f, q)), 16, 128);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveStatusMobilityScale(ulong statusMask, in CombatStatusEffectTuning tuning)
        {
            float stunned = Bit01(statusMask, 6);
            float crippled = Bit01(statusMask, 8);
            float fractured = Bit01(statusMask, 9);
            float stunScale = math.lerp(1f, math.saturate(tuning.StunMobilityScale), stunned);
            float crippleScale = math.lerp(1f, CrippledMobilitySpeedScale, crippled);
            float fractureScale = math.lerp(1f, CrippledMobilitySpeedScale, fractured);
            return math.min(math.min(stunScale, crippleScale), fractureScale);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static CombatStatusEffectState ApplyStatusBitsToState(
            CombatStatusEffectState state,
            ulong statusBits,
            float durationSeconds,
            uint frameIndex)
        {
            ulong bits = statusBits & CombatStatusBits.KnownRuntimeMask64;
            float duration = SanitizeStatusDuration(durationSeconds, bits);
            state.StatusEffectMask |= bits;
            state.Durations0123 = new float4(
                math.max(state.Durations0123.x, duration * Bit01(bits, 0)),
                math.max(state.Durations0123.y, duration * Bit01(bits, 1)),
                math.max(state.Durations0123.z, duration * Bit01(bits, 2)),
                math.max(state.Durations0123.w, duration * Bit01(bits, 3)));
            state.Durations4567 = new float4(
                math.max(state.Durations4567.x, duration * Bit01(bits, 4)),
                math.max(state.Durations4567.y, duration * Bit01(bits, 5)),
                math.max(state.Durations4567.z, duration * Bit01(bits, 6)),
                math.max(state.Durations4567.w, duration * Bit01(bits, 7)));
            state.FractureSeconds = math.max(state.FractureSeconds, duration * Bit01(bits, 9));
            state.LastAppliedFrame = frameIndex;
            state.LastChangedFrame = frameIndex;
            RefreshStatusFsmBytes(ref state);
            return state;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void RefreshStatusFsmBytes(ref CombatStatusEffectState state)
        {
            state.BleedFsm = ResolveFsmByte(state.Durations0123.x, Bit01(state.StatusEffectMask, 0));
            state.CrushFsm = ResolveFsmByte(state.Durations0123.y, Bit01(state.StatusEffectMask, 1));
            state.IrradiationFsm = ResolveFsmByte(state.Durations0123.z, Bit01(state.StatusEffectMask, 2));
            state.HypoxiaFsm = ResolveFsmByte(state.Durations0123.w, Bit01(state.StatusEffectMask, 3));
            state.PoisonFsm = ResolveFsmByte(state.Durations4567.x, Bit01(state.StatusEffectMask, 4));
            state.BurnFsm = ResolveFsmByte(state.Durations4567.y, Bit01(state.StatusEffectMask, 5));
            state.StunFsm = ResolveFsmByte(state.Durations4567.z, Bit01(state.StatusEffectMask, 6));
            state.BrittleFsm = ResolveFsmByte(state.Durations4567.w, Bit01(state.StatusEffectMask, 7));
            state.StateHash = math.hash(new uint4(
                unchecked((uint)state.StatusEffectMask),
                unchecked((uint)(state.StatusEffectMask >> 32)),
                PackFsmBytes(state.BleedFsm, state.CrushFsm, state.IrradiationFsm, state.HypoxiaFsm),
                PackFsmBytes(state.PoisonFsm, state.BurnFsm, state.StunFsm, state.BrittleFsm) ^ math.asuint(state.FractureSeconds)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte ResolveFsmByte(float secondsRemaining, float active01)
        {
            float safeSeconds = math.max(0f, math.select(0f, secondsRemaining, math.isfinite(secondsRemaining)));
            int active = math.select(0, 1, active01 > 0f);
            int expiring = math.select(0, 1, safeSeconds <= 0.25f);
            int liveState = math.select((int)StatusFsmActive, (int)StatusFsmExpiring, expiring != 0);
            return (byte)(liveState * active);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ResolveStatusEffectFrameIndex()
        {
            uint frame = _statusEffectFrameIndex;
            return frame != 0u ? frame : Hecton8.Core.SystemDispatcher.CurrentFrameId;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveStatusEffectQualityWeight01()
        {
            return SanitizeQualityWeight01(SignalBusRegistry.GlobalQualityWeight01);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint PackFsmBytes(byte a, byte b, byte c, byte d)
        {
            return (uint)(a | (b << 8) | (c << 16) | (d << 24));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Bit01(ulong mask, int shift)
        {
            return (float)((mask >> shift) & 1UL);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong SelectStatusBit(float secondsRemaining, ulong bit)
        {
            return (ulong)math.select(0L, unchecked((long)bit), secondsRemaining > 0f);
        }

        private static void ClearStatusEffectTelemetryImmediate(ref CombatStatusEffectVaultViews statusViews)
        {
            if (statusViews.TelemetryRing.IsCreated)
            {
                int ringLength = math.min(StatusEffectTelemetryCapacity, statusViews.TelemetryRing.Length);
                for (int i = 0; i < ringLength; i++)
                    statusViews.TelemetryRing[i] = default;
            }

            if (statusViews.TelemetryCursor.IsCreated)
            {
                for (int i = 0; i < math.min(StatusEffectTelemetryCursorLength, statusViews.TelemetryCursor.Length); i++)
                    statusViews.TelemetryCursor[i] = 0;
            }
        }

        private static void ClearStatusEffectCountersImmediate(ref CombatStatusEffectVaultViews statusViews)
        {
            if (!statusViews.Counters.IsCreated)
                return;

            for (int i = 0; i < math.min(StatusEffectCounterLength, statusViews.Counters.Length); i++)
                statusViews.Counters[i] = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ReadStatusCounter(int index)
        {
            return TryResolveStatusEffectReadOnlyVaultViews(out CombatStatusEffectReadOnlyVaultViews statusViews) &&
                   statusViews.Counters.IsCreated &&
                   (uint)index < (uint)statusViews.Counters.Length
                ? statusViews.Counters[index].Value
                : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ReadStatusCounter(int index, ref CombatStatusEffectVaultViews statusViews)
        {
            return statusViews.Counters.IsCreated && (uint)index < (uint)statusViews.Counters.Length
                ? statusViews.Counters[index].Value
                : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteStatusCounter(int index, int value)
        {
            if (_statusEffectVault == null ||
                !_statusEffectVault.TryAcquireWriteLock(in _statusEffectCountersHandle, SystemID.GameplayCombat, out NativeArray<CombatStatusEffectCounterLane> counters))
            {
                return;
            }

            try
            {
                if (!counters.IsCreated || (uint)index >= (uint)counters.Length)
                    return;

                CombatStatusEffectCounterLane lane = counters[index];
                lane.Value = value;
                counters[index] = lane;
            }
            finally
            {
                _statusEffectVault.ReleaseWriteLock(in _statusEffectCountersHandle, SystemID.GameplayCombat);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteStatusCounter(int index, int value, ref CombatStatusEffectVaultViews statusViews)
        {
            if (statusViews.Counters.IsCreated && (uint)index < (uint)statusViews.Counters.Length)
            {
                CombatStatusEffectCounterLane lane = statusViews.Counters[index];
                lane.Value = value;
                statusViews.Counters[index] = lane;
            }
        }

        private static uint ResolveStatusElapsedMicroseconds()
        {
            long startTicks = _statusScheduleTicks;
            _statusScheduleTicks = 0L;
            if (startTicks <= 0L)
                return 0u;

            long elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            if (elapsedTicks <= 0L)
                return 0u;

            double microseconds = (elapsedTicks * 1000000.0d) / Stopwatch.Frequency;
            if (!double.IsFinite(microseconds) || microseconds <= 0d)
                return 0u;

            return microseconds >= uint.MaxValue ? uint.MaxValue : (uint)math.round((float)microseconds);
        }

        private static void WriteStatusCompletionTelemetry(uint elapsedMicroseconds, ref CombatStatusEffectVaultViews statusViews)
        {
            if (!statusViews.TelemetryRing.IsCreated ||
                !statusViews.TelemetryCursor.IsCreated ||
                statusViews.TelemetryRing.Length <= 0 ||
                (uint)StatusEffectTelemetryWriteCursor >= (uint)statusViews.TelemetryCursor.Length)
            {
                return;
            }

            int ringLength = math.min(StatusEffectTelemetryCapacity, statusViews.TelemetryRing.Length);
            int cursor = statusViews.TelemetryCursor[StatusEffectTelemetryWriteCursor];
            int writeIndex = (int)((uint)cursor % (uint)ringLength);
            statusViews.TelemetryCursor[StatusEffectTelemetryWriteCursor] = unchecked(cursor + 1);
            uint active = unchecked((uint)math.max(0, ReadStatusCounter(StatusEffectCounterActive, ref statusViews)));
            uint requests = unchecked((uint)math.max(0, ReadStatusCounter(StatusEffectCounterRequests, ref statusViews)));
            uint damageMilli = unchecked((uint)math.max(0, ReadStatusCounter(StatusEffectCounterDamageMilli, ref statusViews)));
            uint bitExtractions = unchecked((uint)math.max(0, _targetCount * 8));
            uint anomalyHash = unchecked((uint)ReadStatusCounter(StatusEffectCounterAnomaly, ref statusViews));
            if (anomalyHash == 0u && elapsedMicroseconds > 200u)
                anomalyHash = 0x53190200u;

            statusViews.TelemetryRing[writeIndex] = new CombatStatusEffectTelemetryEntry
            {
                FrameIndex = ResolveStatusEffectFrameIndex(),
                TargetHash = 0u,
                StatusEffectMask = CombatStatusBits.KnownRuntimeMask64,
                StateHash = math.hash(new uint4(active, requests, damageMilli, bitExtractions)),
                AnomalyHash = anomalyHash,
                PreviousHealth = 0f,
                NextHealth = 0f,
                AppliedDamage = damageMilli * 0.001f,
                DeltaTime = _statusLastEvaluationDeltaSeconds,
                GlobalQualityWeight01 = _statusEffectLastQualityWeight01,
                Flags = (ushort)(anomalyHash != 0u ? TelemetryFlagResultAnomaly : CombatDamageResultFlags.None),
                FsmPacked0 = 0,
                FsmPacked1 = 0,
                ActiveCount = active,
                RequestCount = requests,
                EstimatedMicroseconds = elapsedMicroseconds,
                Reserved = bitExtractions
            };
        }

        private static void WriteStatusResultTelemetryRows(ref CombatStatusEffectVaultViews statusViews)
        {
            if (!statusViews.TelemetryRing.IsCreated ||
                !statusViews.TelemetryCursor.IsCreated ||
                !TryResolveCombatDamageVaultViews(out CombatDamageVaultViews views, ensure: false) ||
                !views.StatusResults.IsCreated ||
                !views.StatusResultActive.IsCreated ||
                !statusViews.States.IsCreated)
            {
                return;
            }

            int count = math.min(_targetCount, math.min(views.StatusResults.Length, math.min(views.StatusResultActive.Length, statusViews.States.Length)));
            uint active = unchecked((uint)math.max(0, ReadStatusCounter(StatusEffectCounterActive, ref statusViews)));
            uint requests = unchecked((uint)math.max(0, ReadStatusCounter(StatusEffectCounterRequests, ref statusViews)));
            uint elapsed = unchecked((uint)math.max(0, ReadStatusCounter(StatusEffectCounterSolveMicroseconds, ref statusViews)));
            uint vfxCount = unchecked((uint)math.max(0, ReadStatusCounter(StatusEffectCounterVfxSignals, ref statusViews)));
            for (int slot = 0; slot < count; slot++)
            {
                if (views.StatusResultActive[slot] == 0)
                    continue;

                CombatDamageResult result = views.StatusResults[slot];
                CombatStatusEffectState state = statusViews.States[slot];
                AppendStatusTelemetryEntry(new CombatStatusEffectTelemetryEntry
                {
                    FrameIndex = ResolveStatusEffectFrameIndex(),
                    TargetHash = unchecked((uint)result.TargetId),
                    StatusEffectMask = state.StatusEffectMask,
                    StateHash = state.StateHash,
                    AnomalyHash = 0u,
                    PreviousHealth = result.PreviousHealth,
                    NextHealth = result.NextHealth,
                    AppliedDamage = result.AppliedDamage,
                    DeltaTime = _statusLastEvaluationDeltaSeconds,
                    GlobalQualityWeight01 = _statusEffectLastQualityWeight01,
                    Flags = result.Flags,
                    FsmPacked0 = (byte)(state.BleedFsm | (state.CrushFsm << 2) | (state.IrradiationFsm << 4) | (state.HypoxiaFsm << 6)),
                    FsmPacked1 = (byte)(state.PoisonFsm | (state.BurnFsm << 2) | (state.StunFsm << 4) | (state.BrittleFsm << 6)),
                    ActiveCount = active,
                    RequestCount = requests,
                    EstimatedMicroseconds = elapsed,
                    Reserved = vfxCount
                }, ref statusViews);
            }
        }

        private static void AppendStatusTelemetryEntry(in CombatStatusEffectTelemetryEntry entry, ref CombatStatusEffectVaultViews statusViews)
        {
            if (!statusViews.TelemetryRing.IsCreated ||
                !statusViews.TelemetryCursor.IsCreated ||
                statusViews.TelemetryRing.Length <= 0 ||
                (uint)StatusEffectTelemetryWriteCursor >= (uint)statusViews.TelemetryCursor.Length)
            {
                return;
            }

            int ringLength = math.min(StatusEffectTelemetryCapacity, statusViews.TelemetryRing.Length);
            int cursor = statusViews.TelemetryCursor[StatusEffectTelemetryWriteCursor];
            int writeIndex = (int)((uint)cursor % (uint)ringLength);
            statusViews.TelemetryRing[writeIndex] = entry;
            statusViews.TelemetryCursor[StatusEffectTelemetryWriteCursor] = unchecked(cursor + 1);
        }

        private static void TryDumpStatusEffectTelemetry(uint anomalyHash, ref CombatStatusEffectVaultViews statusViews)
        {
            if (_statusEffectTelemetryDumpedThisSession ||
                !statusViews.TelemetryRing.IsCreated ||
                statusViews.TelemetryRing.Length <= 0)
            {
                return;
            }

            int ringLength = math.min(StatusEffectTelemetryCapacity, statusViews.TelemetryRing.Length);
            bool cursorReadable = statusViews.TelemetryCursor.IsCreated &&
                (uint)StatusEffectTelemetryWriteCursor < (uint)statusViews.TelemetryCursor.Length;
            uint cursor = cursorReadable
                ? unchecked((uint)statusViews.TelemetryCursor[StatusEffectTelemetryWriteCursor])
                : 0u;
            try
            {
                string dumpPath = Path.Combine(
                    Application.dataPath,
                    "..",
                    "Docs",
                    "AgentLogs",
                    "Dump_1417_CombatStatusEffects.bin");
                string directory = Path.GetDirectoryName(dumpPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(StatusEffectTelemetryMagicLow);
                    writer.Write(StatusEffectTelemetryMagicHigh);
                    writer.Write((uint)ringLength);
                    writer.Write((uint)StatusEffectTelemetrySizeBytes);
                    writer.Write(cursor);
                    writer.Write(anomalyHash);
                    int start = cursor >= (uint)ringLength
                        ? (int)(cursor % (uint)ringLength)
                        : 0;

                    for (int i = 0; i < ringLength; i++)
                    {
                        int index = (start + i) % ringLength;
                        WriteStatusEffectTelemetryEntry(writer, statusViews.TelemetryRing[index]);
                    }
                }

                _statusEffectTelemetryDumpedThisSession = true;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static void WriteStatusEffectTelemetryEntry(BinaryWriter writer, in CombatStatusEffectTelemetryEntry entry)
        {
            writer.Write(entry.FrameIndex);
            writer.Write(entry.TargetHash);
            writer.Write(entry.StatusEffectMask);
            writer.Write(entry.StateHash);
            writer.Write(entry.AnomalyHash);
            writer.Write(entry.PreviousHealth);
            writer.Write(entry.NextHealth);
            writer.Write(entry.AppliedDamage);
            writer.Write(entry.DeltaTime);
            writer.Write(entry.GlobalQualityWeight01);
            writer.Write(entry.Flags);
            writer.Write(entry.FsmPacked0);
            writer.Write(entry.FsmPacked1);
            writer.Write(entry.ActiveCount);
            writer.Write(entry.RequestCount);
            writer.Write(entry.EstimatedMicroseconds);
            writer.Write(entry.Reserved);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct GenerateMockStatusEffectsJob : IJobParallelFor
        {
            public uint Seed;
            public int Count;
            public uint FrameIndex;
            [NoAlias] public NativeArray<CombatStatusEffectState> StatusEffectStates;
            [NoAlias] public NativeArray<uint> StatusMasks;
            [NoAlias] public NativeArray<float4> StatusDurations0123;
            [NoAlias] public NativeArray<float4> LegacyStatusDurations4567;
            [NoAlias] public NativeArray<float> BrittleDurations;

            public void Execute(int index)
            {
                if (index >= Count)
                    return;

                uint hash = math.hash(new uint3(Seed, unchecked((uint)index), 0x5319BEEFu));
                ulong mask = CombatStatusBits.Poisoned64 |
                             CombatStatusBits.Bleeding64 |
                             (((hash & 1u) != 0u) ? CombatStatusBits.Stunned64 : 0UL) |
                             (((hash & 2u) != 0u) ? CombatStatusBits.Burning64 : 0UL);
                float duration = 2f + ((hash >> 8) & 1023u) * (1f / 128f);
                CombatStatusEffectState state = StatusEffectStates[index];
                state = ApplyStatusBitsToState(state, mask, duration, FrameIndex);
                StatusEffectStates[index] = state;
                StatusMasks[index] = (uint)(state.StatusEffectMask & uint.MaxValue);
                StatusDurations0123[index] = state.Durations0123;
                LegacyStatusDurations4567[index] = state.Durations4567;
                BrittleDurations[index] = state.Durations4567.w;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct ApplyStatusEffectRequestsJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<CombatStatusEffectRequest> Requests;
            [ReadOnly, NoAlias] public NativeArray<int> TargetLookupKeys;
            [ReadOnly, NoAlias] public NativeArray<int> TargetLookupSlots;
            [NoAlias] public NativeArray<CombatStatusEffectState> StatusEffectStates;
            [NoAlias] public NativeArray<uint> StatusMasks;
            [NoAlias] public NativeArray<float4> StatusDurations0123;
            [NoAlias] public NativeArray<float4> LegacyStatusDurations4567;
            [NoAlias] public NativeArray<float> BrittleDurations;
            [NoAlias] public NativeArray<CombatStatusEffectCounterLane> Counters;
            public int RequestCount;
            public int RequestBudget;

            public unsafe void Execute()
            {
                int processed = 0;
                int requestLimit = math.min(math.min(RequestBudget, RequestCount), Requests.Length);
                for (int requestIndex = 0; requestIndex < requestLimit; requestIndex++)
                {
                    CombatStatusEffectRequest request = Requests[requestIndex];
                    processed++;
                    if (!TryFindTargetSlotInLookup(TargetLookupKeys, TargetLookupSlots, request.TargetId, out int slot))
                    {
                        AddCounter(StatusEffectCounterDroppedRequests, 1);
                        continue;
                    }

                    if (!IsValidSlot(slot))
                    {
                        AddCounter(StatusEffectCounterDroppedRequests, 1);
                        continue;
                    }

                    ulong bits = request.StatusEffectMask & CombatStatusBits.KnownRuntimeMask64;
                    if (bits == 0UL)
                        continue;

                    AtomicOrStatusMask(slot, bits);
                    CombatStatusEffectState state = StatusEffectStates[slot];
                    state = ApplyStatusBitsToState(state, bits, request.DurationSeconds, request.Frame);
                    StatusEffectStates[slot] = state;
                    StatusMasks[slot] = (uint)(state.StatusEffectMask & uint.MaxValue);
                    MirrorLegacyStatusDurations(slot, in state);
                    AddCounter(StatusEffectCounterRequests, 1);
                }
            }

            private unsafe void AddCounter(int index, int delta)
            {
                byte* counters = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(Counters);
                ref int value = ref UnsafeUtility.AsRef<int>((void*)(counters + (index * 64)));
                Interlocked.Add(ref value, delta);
            }

            private bool IsValidSlot(int slot)
            {
                return (uint)slot < (uint)StatusEffectStates.Length &&
                       (uint)slot < (uint)StatusMasks.Length &&
                       (uint)slot < (uint)StatusDurations0123.Length &&
                       (uint)slot < (uint)LegacyStatusDurations4567.Length &&
                       (uint)slot < (uint)BrittleDurations.Length;
            }

            private unsafe bool AtomicOrStatusMask(int slot, ulong bitMask)
            {
                byte* states = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(StatusEffectStates);
                ref long signedWord = ref UnsafeUtility.AsRef<long>((void*)(states + (slot * StatusEffectStateSizeBytes)));
                long signedBit = unchecked((long)bitMask);
                while (true)
                {
                    long before = Interlocked.CompareExchange(ref signedWord, 0L, 0L);
                    long after = before | signedBit;
                    if (before == after)
                        return false;

                    if (Interlocked.CompareExchange(ref signedWord, after, before) == before)
                        return true;
                }
            }

            private void MirrorLegacyStatusDurations(int slot, in CombatStatusEffectState state)
            {
                StatusDurations0123[slot] = state.Durations0123;
                LegacyStatusDurations4567[slot] = state.Durations4567;
                BrittleDurations[slot] = state.Durations4567.w;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct EvaluateStatusEffectsJob : IJobParallelFor
        {
            public float DeltaTime;
            public uint FrameIndex;
            [ReadOnly, NoAlias] public NativeArray<int> InstanceIds;
            [ReadOnly, NoAlias] public NativeArray<float> Health;
            [ReadOnly, NoAlias] public NativeArray<float> MaxHealth;
            [ReadOnly, NoAlias] public NativeArray<float> InvMaxHealth;
            [ReadOnly, NoAlias] public NativeArray<double3> TargetRootAups;
            [NoAlias] public NativeArray<CombatStatusEffectState> StatusEffectStates;
            [NoAlias] public NativeArray<uint> StatusMasks;
            [NoAlias] public NativeArray<float4> StatusDurations0123;
            [NoAlias] public NativeArray<float4> LegacyStatusDurations4567;
            [NoAlias] public NativeArray<float> BrittleDurations;
            [WriteOnly, NoAlias] public NativeArray<CombatDamageResult> ResultsBySlot;
            [WriteOnly, NoAlias] public NativeArray<byte> ResultActiveBySlot;
            [ReadOnly, NoAlias] public NativeArray<CombatStatusEffectTuning> Tuning;
            // Parallel-safe: cursor/counter lanes are mutated only through Interlocked; VFX writes reserve unique slots.
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<int> TelemetryCursor;
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<CombatStatusEffectCounterLane> Counters;
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<CombatStatusEffectVfxRequest> VfxRequests;
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<CombatDamageSignal> DamageSignals;
            public float GlobalQualityWeight01;

            public unsafe void Execute(int index)
            {
                if ((uint)index < (uint)ResultActiveBySlot.Length)
                    ResultActiveBySlot[index] = 0;

                if (!IsValidEvaluationIndex(index))
                    return;

                CombatStatusEffectTuning tuning = Tuning[0];
                CombatStatusEffectState state = StatusEffectStates[index];
                ulong previousMask = state.StatusEffectMask & CombatStatusBits.KnownRuntimeMask64;
                float active01 = math.select(0f, 1f, previousMask != 0UL);
                float dt = math.max(0f, DeltaTime);
                float4 active0123 = new float4(
                    Bit01(previousMask, 0),
                    Bit01(previousMask, 1),
                    Bit01(previousMask, 2),
                    Bit01(previousMask, 3));
                float4 active4567 = new float4(
                    Bit01(previousMask, 4),
                    Bit01(previousMask, 5),
                    Bit01(previousMask, 6),
                    Bit01(previousMask, 7));

                float4 previousDurations0123 = math.select(float4.zero, state.Durations0123, math.isfinite(state.Durations0123));
                float4 previousDurations4567 = math.select(float4.zero, state.Durations4567, math.isfinite(state.Durations4567));
                float4 durations0123 = math.max(float4.zero, previousDurations0123 - (new float4(dt) * active0123));
                float4 durations4567 = math.max(float4.zero, previousDurations4567 - (new float4(dt) * active4567));
                float fractureSeconds = math.max(0f, math.select(0f, state.FractureSeconds, math.isfinite(state.FractureSeconds)) - (dt * Bit01(previousMask, 9)));
                ulong liveMask = (previousMask & CombatStatusBits.Crippled64) |
                                 SelectStatusBit(durations0123.x, CombatStatusBits.Bleeding64) |
                                 SelectStatusBit(durations0123.y, CombatStatusBits.Crushed64) |
                                 SelectStatusBit(durations0123.z, CombatStatusBits.Irradiated64) |
                                 SelectStatusBit(durations0123.w, CombatStatusBits.Hypoxia64) |
                                 SelectStatusBit(durations4567.x, CombatStatusBits.Poisoned64) |
                                 SelectStatusBit(durations4567.y, CombatStatusBits.Burning64) |
                                 SelectStatusBit(durations4567.z, CombatStatusBits.Stunned64) |
                                 SelectStatusBit(durations4567.w, CombatStatusBits.Brittle64) |
                                 SelectStatusBit(fractureSeconds, CombatStatusBits.Fractured64);

                float damagePerSecond =
                    active0123.x * tuning.BleedingDamagePerSecond +
                    active0123.y * tuning.CrushedDamagePerSecond +
                    active0123.z * tuning.IrradiatedDamagePerSecond +
                    active0123.w * tuning.HypoxiaDamagePerSecond +
                    active4567.x * tuning.PoisonDamagePerSecond +
                    active4567.y * tuning.BurningDamagePerSecond;
                damagePerSecond = math.max(0f, math.select(0f, damagePerSecond, math.isfinite(damagePerSecond)));
                float previousHealth = Health[index];
                float maxHealth = math.max(0.0001f, MaxHealth[index]);
                float rawDamage = damagePerSecond * dt;
                float damage = math.max(0f, math.select(0f, rawDamage, math.isfinite(rawDamage))) * math.select(0f, 1f, previousHealth > 0f);
                float nextHealth = previousHealth;
                if (damage > 0f)
                    EnqueueDamageSignal(index, previousMask, damage);
                TryEnqueueToxicBubbleSignal(index, previousMask, damage, maxHealth, state.StateHash);

                state.StatusEffectMask = liveMask;
                state.Durations0123 = durations0123;
                state.Durations4567 = durations4567;
                state.FractureSeconds = fractureSeconds;
                state.LastAppliedFrame = math.select(state.LastAppliedFrame, FrameIndex, active01 > 0f);
                state.LastChangedFrame = math.select(state.LastChangedFrame, FrameIndex, liveMask != previousMask);
                RefreshStatusFsmBytes(ref state);
                StatusEffectStates[index] = state;
                StatusMasks[index] = (uint)(liveMask & uint.MaxValue);
                StatusDurations0123[index] = durations0123;
                LegacyStatusDurations4567[index] = durations4567;
                BrittleDurations[index] = durations4567.w;

                ushort flags = liveMask == previousMask
                    ? CombatDamageResultFlags.None
                    : CombatDamageResultFlags.StatusChanged;
                uint anomalyHash = ResolveStatusAnomalyHash(previousHealth, nextHealth, damage, liveMask);
                if (anomalyHash != 0u)
                    SetAnomaly(anomalyHash);

                if (liveMask != 0UL)
                    AddCounter(StatusEffectCounterActive, 1);
                bool shouldWriteResult = damage > 0f || liveMask != previousMask || anomalyHash != 0u;
                if (!shouldWriteResult)
                    return;

                ResultsBySlot[index] = new CombatDamageResult
                {
                    TargetId = InstanceIds[index],
                    SourceId = StatusEffectEnvironmentHazardSourceId,
                    DamageType = ResolveResultDamageType(previousMask),
                    StatusBits = (uint)(liveMask & uint.MaxValue),
                    PreviousHealth = previousHealth,
                    NextHealth = nextHealth,
                    AppliedDamage = damage,
                    MaxHealth = maxHealth,
                    Direction = float3.zero,
                    TraumaLevel = ResolveTraumaLevelFromInvMax(damage, InvMaxHealth[index]),
                    Flags = flags,
                    Channel = (byte)DamageChannel.Integrity,
                    DirectionOctant = 0,
                    LocalPoint = float3.zero,
                    SurfaceNormal = float3.zero,
                    Depth = 0f
                };
                ResultActiveBySlot[index] = 1;
                AddCounter(StatusEffectCounterResult, 1);
                AddCounter(StatusEffectCounterDamageMilli, (int)math.round(damage * 1000f));
            }

            private void EnqueueDamageSignal(int index, ulong statusMask, float damage)
            {
                int writeIndex = ReserveDamageSignalSlot();
                if ((uint)writeIndex >= (uint)DamageSignals.Length)
                    return;

                uint targetHash = unchecked((uint)InstanceIds[index]);
                double3 targetAup = ResolveTargetAup(index);
                DamageSignals[writeIndex] = new CombatDamageSignal
                {
                    ImpactAup = targetAup,
                    Direction = float3.zero,
                    Magnitude = damage,
                    DamageType = ResolveResultDamageType(statusMask),
                    TargetHash = targetHash,
                    SourceHash = StatusEffectEnvironmentHazardSourceId,
                    Frame = FrameIndex,
                    SourceId = StatusEffectEnvironmentHazardSourceId,
                    TargetId = targetHash <= (uint)ushort.MaxValue ? (ushort)targetHash : (ushort)0,
                    Channel = (byte)DamageChannel.Integrity,
                    Flags = CombatDamageSignal.DirectRuntimeFlag,
                    IntegrityDelta = 0,
                    Reserved0 = 0
                };
            }

            private bool IsValidEvaluationIndex(int index)
            {
                return (uint)index < (uint)InstanceIds.Length &&
                       (uint)index < (uint)Health.Length &&
                       (uint)index < (uint)MaxHealth.Length &&
                       (uint)index < (uint)InvMaxHealth.Length &&
                       (uint)index < (uint)TargetRootAups.Length &&
                       (uint)index < (uint)StatusEffectStates.Length &&
                       (uint)index < (uint)StatusMasks.Length &&
                       (uint)index < (uint)StatusDurations0123.Length &&
                       (uint)index < (uint)LegacyStatusDurations4567.Length &&
                       (uint)index < (uint)BrittleDurations.Length &&
                       (uint)index < (uint)ResultsBySlot.Length &&
                       (uint)index < (uint)ResultActiveBySlot.Length &&
                       (uint)index < (uint)DamageSignals.Length &&
                       Tuning.Length > 0;
            }

            private unsafe int ReserveDamageSignalSlot()
            {
                byte* counters = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(Counters);
                ref int value = ref UnsafeUtility.AsRef<int>((void*)(counters + (StatusEffectCounterDamageSignals * 64)));
                return Interlocked.Increment(ref value) - 1;
            }

            private void TryEnqueueToxicBubbleSignal(int index, ulong statusMask, float damage, float maxHealth, uint stateHash)
            {
                uint poisonActive = (uint)Bit01(statusMask, 4);
                uint damageActive = (uint)math.select(0, 1, damage > StatusEffectToxicBubbleMinDamage);
                uint cadence = ResolveToxicBubbleCadenceFrames(GlobalQualityWeight01);
                uint frameMod = FrameIndex % cadence;
                uint laneOffset = math.hash(new uint3(unchecked((uint)index), stateHash, 0xB0B5319u)) % cadence;
                if ((poisonActive & damageActive) == 0u || frameMod != laneOffset)
                    return;

                double3 targetAup = ResolveTargetAup(index);
                if (!math.all(math.isfinite(targetAup)))
                    return;

                float intensity01 = math.saturate(damage * math.rcp(math.max(0.0001f, maxHealth)));
                int writeIndex = ReserveVfxRequestSlot();
                if ((uint)writeIndex >= (uint)VfxRequests.Length)
                    return;

                VfxRequests[writeIndex] = new CombatStatusEffectVfxRequest
                {
                    PositionAup = targetAup,
                    Intensity01 = intensity01 * math.max(0f, Tuning[0].ToxicBubbleScale),
                    RadiusMeters = StatusEffectToxicBubbleRadiusMeters,
                    Frame = FrameIndex,
                    SourceHash = unchecked((uint)InstanceIds[index]),
                    EffectHash = CombatDamageTypes.Toxic,
                    Flags = 0u,
                    Reserved0 = 0UL,
                    Reserved1 = 0UL
                };
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private double3 ResolveTargetAup(int index)
            {
                double3 targetAup = TargetRootAups[index];
                return math.select(double3.zero, targetAup, math.isfinite(targetAup));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static uint ResolveToxicBubbleCadenceFrames(float qualityWeight01)
            {
                float q = SmoothStep01(qualityWeight01);
                return (uint)math.clamp((int)math.round(math.lerp(48f, 8f, q)), 8, 48);
            }

            private unsafe void AddCounter(int index, int delta)
            {
                byte* counters = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(Counters);
                ref int value = ref UnsafeUtility.AsRef<int>((void*)(counters + (index * 64)));
                Interlocked.Add(ref value, delta);
            }

            private unsafe int ReadCounter(int index)
            {
                byte* counters = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(Counters);
                ref int value = ref UnsafeUtility.AsRef<int>((void*)(counters + (index * 64)));
                return Interlocked.CompareExchange(ref value, 0, 0);
            }

            private unsafe int ReserveVfxRequestSlot()
            {
                byte* counters = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(Counters);
                ref int value = ref UnsafeUtility.AsRef<int>((void*)(counters + (StatusEffectCounterVfxSignals * 64)));
                return Interlocked.Increment(ref value) - 1;
            }

            private unsafe void SetAnomaly(uint anomalyHash)
            {
                byte* counters = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(Counters);
                ref int anomaly = ref UnsafeUtility.AsRef<int>((void*)(counters + (StatusEffectCounterAnomaly * 64)));
                Interlocked.CompareExchange(ref anomaly, unchecked((int)anomalyHash), 0);
                int* cursor = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(TelemetryCursor);
                Interlocked.CompareExchange(ref cursor[StatusEffectTelemetryLastAnomaly], unchecked((int)anomalyHash), 0);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static uint ResolveResultDamageType(ulong statusMask)
            {
                uint damageType = ResolveStatusDamageType(statusMask);
                return damageType == 0u ? CombatDamageTypes.Toxic : damageType;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static uint ResolveStatusAnomalyHash(float previousHealth, float nextHealth, float damage, ulong liveMask)
            {
                if (!math.isfinite(previousHealth))
                    return 0x53190001u;
                if (!math.isfinite(nextHealth))
                    return 0x53190002u;
                if (!math.isfinite(damage))
                    return 0x53190003u;
                if ((liveMask & ~CombatStatusBits.KnownRuntimeMask64) != 0UL)
                    return 0x53190004u;

                return 0u;
            }
        }
    }
}
