using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Hecton8.Power;
using Hecton8.Power.Generators.Contracts;
using Hecton8.SaveSystem;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Power.Generators
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PowerNode))]
    [AddComponentMenu("Hecton8/Power/Radioisotope Thermal Generator")]
    public sealed class RadioisotopeThermalGenerator : MonoBehaviour,
        IPowerComponent,
        IColdTickable,
        ILateFrameTickable,
        IPoolable,
        IPowerActivationTarget,
        ISaveable,
        IRtgDecayOutputReader,
        IRadioisotopeThermalReprocessable,
        IGlobalRegistryHotSwapListener
    {
        private static int s_x001RadioisotopeThermalGeneratorSignalPushDropCount;
        private const int MaxRtgs = 128;
        private const int TelemetryCapacity = 300;
        private const int DecayBatchSize = 32;
        private const float SecondsPerHour = 3600f;
        private const float MinimumHalfLifeSeconds = 1f;
        private const float DeadOutputThreshold01 = 0.05f;
        private const float WarningOutputThreshold01 = 0.2f;
        private const float DecayCadenceSeconds = 1f;
        private const float PowerDirtyDeltaWatts = 0.01f;
        private const float MinimumRadiationRadiusMeters = 0.5f;
        private const string BlackBoxDumpRelativePath = "Docs/AgentLogs/Dump_VAULT_SOVEREIGNTY_ENFORCER_RTG_DECAY.bin";
        private const uint BlackBoxMagic = 0x52475444u; // RGTD
        private const uint BlackBoxVersion = 2u;
        private const int BlackBoxHeaderBytes = 24;
        private const int BlackBoxTelemetryRowBytes = 23;
        private const int RtgTelemetryEntrySizeBytes = 64;
        private const uint RtgTelemetryHash = 0x52544721u; // RTG!
        private const uint ActiveRtgsHash = 0x41525447u; // ARTG
        private const uint AverageRtgHealthHash = 0x41564821u; // AVH!
        private static readonly uint RtgLowOutputMessageHash =
            unchecked((uint)LocHash.Compute("Power.RTG.OutputBelowTwentyPercent"));
        private static readonly uint RtgLowOutputContextHash =
            unchecked((uint)LocHash.Compute(nameof(RadioisotopeThermalGenerator)));

        private const byte FlagActive = RtgDecayMath.FlagActive;
        private const byte FlagDead = RtgDecayMath.FlagDead;
        private const byte FlagWarned20 = RtgDecayMath.FlagWarned20;
        private const byte FlagReprocessed = RtgDecayMath.FlagReprocessed;

        [SerializeField] private string stableRtgId = "rtg.core.00";
        [SerializeField] private uint sourceIdOverride;
        [SerializeField] private float baseOutputWatts = 180f;
        [SerializeField] private float halfLifeHours = 180f;
        [SerializeField] private float thermalRadiusMeters = 5f;
        [SerializeField] private float thermalDeltaCelsiusAtFullOutput = 6f;
        [SerializeField] private float radiationRadiusMeters = 6f;
        [SerializeField] private float radiationIntensityAtFullOutput = 0.55f;
        [SerializeField] private float deadRadiationIntensity = 0.35f;
        [SerializeField] private string depletedIsotopeItemId = "item.depleted_rtg_isotope";
        private static VaultGenerationHandle<float> s_rtgStartTimesHandle;
        private static VaultGenerationHandle<float> s_rtgHalfLivesHandle;
        private static VaultGenerationHandle<float> s_rtgBaseOutputHandle;
        private static VaultGenerationHandle<float> s_rtgCurrentOutputHandle;
        private static VaultGenerationHandle<float> s_rtgOutputNormalizedHandle;
        private static VaultGenerationHandle<byte> s_rtgFlagsHandle;
        private static VaultGenerationHandle<RtgTelemetryEntry> s_telemetryRingHandle;
        private static IDataVault s_dataVault;
        private static RadioisotopeThermalGenerator[] s_instances;
        private static JobHandle s_decayJobHandle;
        private static bool s_decayJobPending;
        private static bool s_blackBoxDumped;
        private static int s_activeCount;
        private static int s_leaderSlot = -1;
        private static int s_telemetryCursor;
        private static float s_averageRtgHealth01 = 1f;
        private static float s_lastDecayEvaluationSeconds = float.NegativeInfinity;
        private static IThermodynamicsService s_thermodynamics;

        private PowerNode _powerNode;
        private int _slot = -1;
        private int _sourceId;
        private uint _depletedIsotopeHash;
        private float _startTimeSeconds = -1f;
        private float _currentOutputWatts;
        private float _outputNormalized01 = 1f;
        private float _runtimeActivation01 = 1f;
        private bool _isDead;
        private bool _reprocessed;
        private bool _registeredCold;
        private bool _registeredLate;
        private bool _registeredSave;
        private bool _registeredHotSwapListener;
        private ISaveService _saveService;

        public int SavePriority => 53;
        public int LoadPriority => 53;
        public float PowerRating => _isDead || _reprocessed ? 0f : math.max(0f, _currentOutputWatts) * _runtimeActivation01;
        public int PowerPriority => 0;
        public bool HasPower => !_isDead && !_reprocessed && _runtimeActivation01 > 0.0001f;
        public float OutputNormalized01 => _outputNormalized01;
        public float CurrentOutputWatts => _currentOutputWatts;
        public int ActiveRtgCount => s_activeCount;
        public float AverageRtgHealth01 => s_averageRtgHealth01;
        public bool IsDeadRtg => _isDead;
        public uint DepletedIsotopeHash => _depletedIsotopeHash;

        public void OnPowerStatusChanged(bool hasPower)
        {
        }

        public void OnSpawn()
        {
            CacheThermodynamicsServiceCold();
            TryRegisterHotSwapListener();
            TryRegisterRuntime();
            TryRegisterSaveParticipant();
        }

        public void OnDespawn()
        {
            TryUnregisterRuntime();
            TryUnregisterSaveParticipant();
            TryUnregisterHotSwapListener();
            _runtimeActivation01 = 1f;
        }

        public bool SetRuntimeActivation01(float activation01)
        {
            float sanitized = math.saturate(math.select(1f, activation01, math.isfinite(activation01)));
            if (math.abs(_runtimeActivation01 - sanitized) <= 0.0001f)
                return false;

            _runtimeActivation01 = sanitized;
            MarkPowerGridDirty();
            return true;
        }

        public void ColdTick()
        {
            if (_slot != s_leaderSlot)
                return;

            TryRegisterSaveParticipant();
            TryRunDecayCadence(DecayCadenceSeconds);
        }

        public void LateFrameTick()
        {
            if (_slot != s_leaderSlot)
                return;

            TryFinalizeDecayJobNoWait();
        }

        public void PopulateSaveData(SaveData data)
        {
            if (data == null || _slot != s_leaderSlot)
                return;

            CompleteDecayJobForTeardown();
            EnsureRtgSaveArrays(data);
            if (!TryResolveRtgFlags(out NativeArray<byte> rtgFlags))
                return;

            int writeCount = 0;
            for (int i = 0; i < MaxRtgs; i++)
            {
                if ((rtgFlags[i] & FlagActive) == 0)
                    continue;

                RadioisotopeThermalGenerator instance = s_instances != null ? s_instances[i] : null;
                instance?.WriteRtgSaveRecord(data, ref writeCount);
            }

            data.rtgDecayCount = writeCount;
        }

        public void LoadFromSaveData(SaveData data)
        {
            ResolveIdentity();
            if (data == null || _sourceId == 0)
                return;

            int safeCount = math.clamp(data.rtgDecayCount, 0, SaveData.MaxRtgDecayRecords);
            int sourceLength = data.rtgDecaySourceIds != null ? data.rtgDecaySourceIds.Length : 0;
            int startLength = data.rtgStartTimesSeconds != null ? data.rtgStartTimesSeconds.Length : 0;
            int flagLength = data.rtgDecayFlags != null ? data.rtgDecayFlags.Length : 0;
            safeCount = math.min(safeCount, math.min(sourceLength, math.min(startLength, flagLength)));

            for (int i = 0; i < safeCount; i++)
            {
                if (data.rtgDecaySourceIds[i] != _sourceId)
                    continue;

                double start = data.rtgStartTimesSeconds[i];
                _startTimeSeconds = (!double.IsNaN(start) && !double.IsInfinity(start) && start > 0d)
                    ? (float)math.min(start, (double)float.MaxValue)
                    : ResolveCurrentTimeSeconds();
                byte flags = data.rtgDecayFlags[i];
                _reprocessed = (flags & FlagReprocessed) != 0;
                _isDead = (flags & FlagDead) != 0;
                ResolveLocalDecaySnapshot(ResolveCurrentTimeSeconds());
                if ((flags & FlagWarned20) != 0 && _slot >= 0 && TryResolveRtgFlags(out NativeArray<byte> rtgFlags))
                    rtgFlags[_slot] = (byte)(rtgFlags[_slot] | FlagWarned20);

                WriteSlotStateFromInstance();
                return;
            }
        }

        public bool TryGetRtgCurrentOutput(uint sourceId, out float watts, out float normalized01)
        {
            return TryGetCurrentOutput(sourceId, out watts, out normalized01);
        }

        public bool TryMarkReprocessed()
        {
            if (!_isDead || _reprocessed)
                return false;

            _reprocessed = true;
            _currentOutputWatts = 0f;
            _outputNormalized01 = 0f;
            if (_slot >= 0 &&
                TryResolveRtgBuffers(
                    out _,
                    out _,
                    out _,
                    out NativeArray<float> currentOutput,
                    out NativeArray<float> outputNormalized,
                    out NativeArray<byte> rtgFlags,
                    out _))
            {
                rtgFlags[_slot] = (byte)(rtgFlags[_slot] | FlagDead | FlagReprocessed);
                currentOutput[_slot] = 0f;
                outputNormalized[_slot] = 0f;
            }

            RadiationHazardGrid.UnregisterSource(_sourceId);
            MarkPowerGridDirty();
            RecordTelemetry(_slot, ComposeRuntimeFlags());
            return true;
        }

        public static bool TryGetCurrentOutput(uint sourceId, out float watts, out float normalized01)
        {
            watts = 0f;
            normalized01 = 0f;
            if (sourceId == 0u ||
                s_instances == null ||
                !TryResolveRtgBuffers(
                    out _,
                    out _,
                    out _,
                    out NativeArray<float> currentOutput,
                    out NativeArray<float> outputNormalized,
                    out _,
                    out _))
            {
                return false;
            }

            int sourceInt = NormalizeSourceId(sourceId);
            for (int i = 0; i < MaxRtgs; i++)
            {
                RadioisotopeThermalGenerator instance = s_instances[i];
                if (instance == null || instance._sourceId != sourceInt)
                    continue;

                watts = currentOutput[i];
                normalized01 = outputNormalized[i];
                return true;
            }

            return false;
        }

        public static bool TryReprocessForFabricator(Component candidate, out uint depletedIsotopeHash)
        {
            depletedIsotopeHash = 0u;
            if (candidate == null)
                return false;

            if (!candidate.TryGetComponent(out IRadioisotopeThermalReprocessable reprocessable) ||
                !reprocessable.IsDeadRtg)
            {
                return false;
            }

            depletedIsotopeHash = reprocessable.DepletedIsotopeHash;
            return depletedIsotopeHash != 0u && reprocessable.TryMarkReprocessed();
        }

        public static bool TryGetTelemetry(int newestFirstIndex, out uint sourceId, out float outputWatts, out float normalized01)
        {
            sourceId = 0u;
            outputWatts = 0f;
            normalized01 = 0f;
            if (newestFirstIndex < 0 ||
                newestFirstIndex >= TelemetryCapacity ||
                s_telemetryCursor <= 0 ||
                !TryResolveTelemetryRing(out NativeArray<RtgTelemetryEntry> telemetryRing))
            {
                return false;
            }

            int index = (s_telemetryCursor - 1 - newestFirstIndex + TelemetryCapacity) % TelemetryCapacity;
            RtgTelemetryEntry entry = telemetryRing[index];
            sourceId = entry.SourceId;
            outputWatts = entry.OutputWatts;
            normalized01 = entry.NormalizedOutput01;
            return sourceId != 0u;
        }

        private void Awake()
        {
            TryGetComponent(out _powerNode);
            ResolveIdentity();
            SanitizeInspectorValues();
            _currentOutputWatts = math.max(0f, baseOutputWatts);
            _outputNormalized01 = 1f;
            EnsureNativeBuffers();
        }

        private void Start()
        {
            CacheThermodynamicsServiceCold();
            TryRegisterHotSwapListener();
            TryRegisterRuntime();
            TryRegisterSaveParticipant();
        }

        private void OnEnable()
        {
            CacheThermodynamicsServiceCold();
            TryRegisterHotSwapListener();
            TryRegisterRuntime();
            TryRegisterSaveParticipant();
        }

        private void OnDisable()
        {
            TryUnregisterRuntime();
            TryUnregisterSaveParticipant();
            TryUnregisterHotSwapListener();
        }

        private void OnDestroy()
        {
            TryUnregisterRuntime();
            TryUnregisterSaveParticipant();
            TryUnregisterHotSwapListener();
        }

        private void OnValidate()
        {
            SanitizeInspectorValues();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            CompleteDecayJobForTeardown();
            DisposeNativeBuffers();
            s_instances = null;
            s_decayJobHandle = default;
            s_decayJobPending = false;
            s_blackBoxDumped = false;
            s_dataVault = null;
            s_activeCount = 0;
            s_leaderSlot = -1;
            s_telemetryCursor = 0;
            s_averageRtgHealth01 = 1f;
            s_lastDecayEvaluationSeconds = float.NegativeInfinity;
            s_thermodynamics = null;
        }

        private static void EnsureNativeBuffers()
        {
            _ = TryResolveRtgBuffers(out _, out _, out _, out _, out _, out _, out _);
            if (s_instances == null || s_instances.Length != MaxRtgs)
                s_instances = new RadioisotopeThermalGenerator[MaxRtgs];
        }

        private static void DisposeNativeBuffers()
        {
            if (TryResolveRtgBuffers(
                    out NativeArray<float> startTimes,
                    out NativeArray<float> halfLives,
                    out NativeArray<float> baseOutput,
                    out NativeArray<float> currentOutput,
                    out NativeArray<float> outputNormalized,
                    out NativeArray<byte> flags,
                    out NativeArray<RtgTelemetryEntry> telemetryRing))
            {
                ClearNativeArray(startTimes);
                ClearNativeArray(halfLives);
                ClearNativeArray(baseOutput);
                ClearNativeArray(currentOutput);
                ClearNativeArray(outputNormalized);
                ClearNativeArray(flags);
                ClearNativeArray(telemetryRing);
            }

            s_rtgStartTimesHandle = default;
            s_rtgHalfLivesHandle = default;
            s_rtgBaseOutputHandle = default;
            s_rtgCurrentOutputHandle = default;
            s_rtgOutputNormalizedHandle = default;
            s_rtgFlagsHandle = default;
            s_telemetryRingHandle = default;
        }

        private static bool TryResolveRtgBuffers(
            out NativeArray<float> startTimes,
            out NativeArray<float> halfLives,
            out NativeArray<float> baseOutput,
            out NativeArray<float> currentOutput,
            out NativeArray<float> outputNormalized,
            out NativeArray<byte> flags,
            out NativeArray<RtgTelemetryEntry> telemetryRing)
        {
            startTimes = default;
            halfLives = default;
            baseOutput = default;
            currentOutput = default;
            outputNormalized = default;
            flags = default;
            telemetryRing = default;

            return TryResolveVaultBuffer(
                    ref s_rtgStartTimesHandle,
                    BufferID.RtgStartTimes,
                    MaxRtgs,
                    out startTimes) &&
                TryResolveVaultBuffer(
                    ref s_rtgHalfLivesHandle,
                    BufferID.RtgHalfLives,
                    MaxRtgs,
                    out halfLives) &&
                TryResolveVaultBuffer(
                    ref s_rtgBaseOutputHandle,
                    BufferID.RtgBaseOutput,
                    MaxRtgs,
                    out baseOutput) &&
                TryResolveVaultBuffer(
                    ref s_rtgCurrentOutputHandle,
                    BufferID.RtgCurrentOutput,
                    MaxRtgs,
                    out currentOutput) &&
                TryResolveVaultBuffer(
                    ref s_rtgOutputNormalizedHandle,
                    BufferID.RtgOutputNormalized,
                    MaxRtgs,
                    out outputNormalized) &&
                TryResolveVaultBuffer(
                    ref s_rtgFlagsHandle,
                    BufferID.RtgFlags,
                    MaxRtgs,
                    out flags) &&
                TryResolveVaultBuffer(
                    ref s_telemetryRingHandle,
                    BufferID.RtgTelemetryRing,
                    TelemetryCapacity,
                    out telemetryRing);
        }

        private static bool TryResolveRtgFlags(out NativeArray<byte> flags)
        {
            return TryResolveVaultBuffer(ref s_rtgFlagsHandle, BufferID.RtgFlags, MaxRtgs, out flags);
        }

        private static bool TryResolveTelemetryRing(out NativeArray<RtgTelemetryEntry> telemetryRing)
        {
            return TryResolveVaultBuffer(
                ref s_telemetryRingHandle,
                BufferID.RtgTelemetryRing,
                TelemetryCapacity,
                out telemetryRing);
        }

        private static bool TryResolveVaultBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = ResolveDataVault();
            if (vault == null || requiredLength <= 0)
                return false;

            if (IsHandleValid(in handle) &&
                vault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                return true;
            }

            if (vault.IsAllocationLocked)
            {
                if (!vault.TryGetGenerationHandle(bufferId, out handle))
                    return false;
            }
            else
            {
                handle = vault.EnsureGenerationHandle<T>(bufferId, requiredLength, SystemID.Power, NativeArrayOptions.ClearMemory);
            }

            return vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static IDataVault ResolveDataVault()
        {
            IDataVault vault = s_dataVault;
            if (vault != null)
                return vault;

            vault = GlobalRegistry.DataVault;
            if (vault != null)
                s_dataVault = vault;

            return vault;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsHandleValid<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u;
        }

        private static void ClearNativeArray<T>(NativeArray<T> buffer) where T : struct
        {
            if (!buffer.IsCreated)
                return;

            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = default;
        }

        private void TryRegisterRuntime()
        {
            if (!Application.isPlaying || _slot >= 0)
                return;

            EnsureNativeBuffers();
            ResolveIdentity();
            SanitizeInspectorValues();
            if (!TryResolveRtgFlags(out NativeArray<byte> rtgFlags))
            {
                DumpBlackBoxOnce(1u);
                return;
            }

            int slot = AllocateSlot(this, rtgFlags);
            if (slot < 0)
            {
                DumpBlackBoxOnce(1u);
                return;
            }

            _slot = slot;
            if (_startTimeSeconds <= 0f || !math.isfinite(_startTimeSeconds))
                _startTimeSeconds = ResolveCurrentTimeSeconds();

            ResolveLocalDecaySnapshot(ResolveCurrentTimeSeconds());
            WriteSlotStateFromInstance();
            s_activeCount++;
            PublishRadiationAndHeat();
            MarkPowerGridDirty();
            RefreshLeader();
        }

        private void TryUnregisterRuntime()
        {
            if (_slot < 0)
                return;

            CompleteDecayJobForTeardown();
            int slot = _slot;
            if (!TryResolveRtgBuffers(
                    out NativeArray<float> startTimes,
                    out NativeArray<float> halfLives,
                    out NativeArray<float> baseOutput,
                    out NativeArray<float> currentOutput,
                    out NativeArray<float> outputNormalized,
                    out NativeArray<byte> rtgFlags,
                    out _))
            {
                _slot = -1;
                return;
            }

            if (slot == s_leaderSlot)
                SetLeaderSlot(-1);

            RadiationHazardGrid.UnregisterSource(_sourceId);
            s_instances[slot] = null;
            rtgFlags[slot] = 0;
            startTimes[slot] = 0f;
            halfLives[slot] = 0f;
            baseOutput[slot] = 0f;
            currentOutput[slot] = 0f;
            outputNormalized[slot] = 0f;
            _slot = -1;
            s_activeCount = math.max(0, s_activeCount - 1);
            MarkPowerGridDirty();
            RefreshLeader();
        }

        private static int AllocateSlot(RadioisotopeThermalGenerator instance, NativeArray<byte> rtgFlags)
        {
            if (!rtgFlags.IsCreated)
                return -1;

            for (int i = 0; i < MaxRtgs; i++)
            {
                if ((rtgFlags[i] & FlagActive) != 0)
                    continue;

                s_instances[i] = instance;
                return i;
            }

            return -1;
        }

        private void WriteSlotStateFromInstance()
        {
            if (_slot < 0 ||
                !TryResolveRtgBuffers(
                    out NativeArray<float> startTimes,
                    out NativeArray<float> halfLives,
                    out NativeArray<float> baseOutput,
                    out NativeArray<float> currentOutput,
                    out NativeArray<float> outputNormalized,
                    out NativeArray<byte> rtgFlags,
                    out _))
            {
                return;
            }

            startTimes[_slot] = math.max(0f, _startTimeSeconds);
            halfLives[_slot] = math.max(MinimumHalfLifeSeconds, halfLifeHours * SecondsPerHour);
            baseOutput[_slot] = math.max(0f, baseOutputWatts);
            currentOutput[_slot] = _reprocessed || _isDead ? 0f : math.max(0f, _currentOutputWatts);
            outputNormalized[_slot] = math.saturate(_outputNormalized01);
            rtgFlags[_slot] = ComposeRuntimeFlags();
        }

        private void WriteRtgSaveRecord(SaveData data, ref int writeCount)
        {
            if (_sourceId == 0 || writeCount >= SaveData.MaxRtgDecayRecords)
                return;

            ResolveLocalDecaySnapshot(ResolveCurrentTimeSeconds());
            data.rtgDecaySourceIds[writeCount] = _sourceId;
            data.rtgStartTimesSeconds[writeCount] = math.max(0f, _startTimeSeconds);
            data.rtgDecayFlags[writeCount] = ComposeRuntimeFlags();
            writeCount++;
        }

        private void ResolveLocalDecaySnapshot(float currentTimeSeconds)
        {
            float halfLifeSeconds = math.max(MinimumHalfLifeSeconds, halfLifeHours * SecondsPerHour);
            float factor = RtgDecayMath.ResolveDecayFactor(
                currentTimeSeconds,
                math.max(0f, _startTimeSeconds),
                halfLifeSeconds);

            _outputNormalized01 = math.saturate(factor);
            _isDead |= _outputNormalized01 < DeadOutputThreshold01;
            if (_reprocessed)
                _isDead = true;

            _currentOutputWatts = _isDead || _reprocessed
                ? 0f
                : math.max(0f, baseOutputWatts) * _outputNormalized01;
        }

        private byte ComposeRuntimeFlags()
        {
            byte flags = FlagActive;
            if (_isDead)
                flags |= FlagDead;
            if (_outputNormalized01 <= WarningOutputThreshold01)
                flags |= FlagWarned20;
            if (_reprocessed)
                flags |= FlagReprocessed;
            return flags;
        }

        private void TryRunDecayCadence(float cadenceSeconds)
        {
            TryFinalizeDecayJobNoWait();
            if (s_decayJobPending || s_activeCount <= 0)
                return;

            float now = ResolveCurrentTimeSeconds();
            float safeCadence = math.max(0.1f, cadenceSeconds);
            if (math.isfinite(s_lastDecayEvaluationSeconds) &&
                now - s_lastDecayEvaluationSeconds < safeCadence)
            {
                return;
            }

            if (!TryResolveRtgBuffers(
                    out NativeArray<float> startTimes,
                    out NativeArray<float> halfLives,
                    out NativeArray<float> baseOutput,
                    out NativeArray<float> currentOutput,
                    out NativeArray<float> outputNormalized,
                    out NativeArray<byte> rtgFlags,
                    out _))
            {
                return;
            }

            s_lastDecayEvaluationSeconds = now;
            s_decayJobHandle = new RtgDecayJob
            {
                CurrentTimeSeconds = now,
                DeadThreshold01 = DeadOutputThreshold01,
                RtgStartTimes = new NativeSlice<float>(startTimes),
                RtgHalfLifeSeconds = new NativeSlice<float>(halfLives),
                RtgBaseOutputWatts = new NativeSlice<float>(baseOutput),
                RtgCurrentOutputWatts = new NativeSlice<float>(currentOutput),
                RtgOutputNormalized = new NativeSlice<float>(outputNormalized),
                RtgFlags = new NativeSlice<byte>(rtgFlags)
            }.Schedule(MaxRtgs, DecayBatchSize);
            s_decayJobPending = true;
        }

        private static void TryFinalizeDecayJobNoWait()
        {
            if (!s_decayJobPending)
                return;

            if (!s_decayJobHandle.IsCompleted)
                return;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref s_decayJobHandle))
                return;

            FinishDecayJobCompletion();
        }

        private static void CompleteDecayJobForTeardown()
        {
            if (!s_decayJobPending)
                return;

            if (!DispatcherJobFence.TryComplete(ref s_decayJobHandle, forceComplete: true))
                return;

            FinishDecayJobCompletion();
        }

        private static void FinishDecayJobCompletion()
        {
            s_decayJobPending = false;
            ApplyDecayResults();
        }

        private static void ApplyDecayResults()
        {
            if (s_instances == null ||
                !TryResolveRtgBuffers(
                    out _,
                    out _,
                    out _,
                    out NativeArray<float> currentOutput,
                    out NativeArray<float> outputNormalized,
                    out NativeArray<byte> rtgFlags,
                    out _))
            {
                return;
            }

            float healthSum = 0f;
            int healthCount = 0;
            for (int i = 0; i < MaxRtgs; i++)
            {
                if ((rtgFlags[i] & FlagActive) == 0)
                    continue;

                RadioisotopeThermalGenerator instance = s_instances[i];
                if (instance == null)
                    continue;

                float normalized = math.saturate(outputNormalized[i]);
                if (!math.isfinite(normalized))
                {
                    DumpBlackBoxOnce(2u);
                    normalized = 0f;
                }

                healthSum += normalized;
                healthCount++;
            }

            s_averageRtgHealth01 = healthCount > 0 ? math.saturate(healthSum * math.rcp((float)healthCount)) : 1f;

            for (int i = 0; i < MaxRtgs; i++)
            {
                if ((rtgFlags[i] & FlagActive) == 0)
                    continue;

                RadioisotopeThermalGenerator instance = s_instances[i];
                if (instance == null)
                    continue;

                float outputWatts = currentOutput[i];
                float normalized = math.saturate(outputNormalized[i]);
                byte flags = rtgFlags[i];
                if (!math.isfinite(outputWatts) || !math.isfinite(normalized))
                {
                    DumpBlackBoxOnce(2u);
                    outputWatts = 0f;
                    normalized = 0f;
                    flags |= FlagDead;
                }

                instance.ApplyDecayResult(outputWatts, normalized, flags);
            }

            GlobalTelemetryBus.PublishModTelemetry(RtgTelemetryHash, ActiveRtgsHash, healthCount);
            GlobalTelemetryBus.PublishModTelemetry(RtgTelemetryHash, AverageRtgHealthHash, s_averageRtgHealth01);
        }

        private void ApplyDecayResult(float outputWatts, float normalized01, byte flags)
        {
            bool wasDead = _isDead;
            float previousWatts = _currentOutputWatts;
            _isDead = (flags & FlagDead) != 0;
            _reprocessed |= (flags & FlagReprocessed) != 0;
            _outputNormalized01 = math.saturate(normalized01);
            _currentOutputWatts = _isDead || _reprocessed ? 0f : math.max(0f, outputWatts);
            if (_slot >= 0 && TryResolveRtgFlags(out NativeArray<byte> rtgFlags))
                rtgFlags[_slot] = ComposeRuntimeFlags();

            if (!_reprocessed && _outputNormalized01 <= WarningOutputThreshold01 && (flags & FlagWarned20) == 0)
                PublishLowOutputHudWarning();

            if (math.abs(previousWatts - _currentOutputWatts) > PowerDirtyDeltaWatts || wasDead != _isDead)
                MarkPowerGridDirty();

            PublishRadiationAndHeat();
            RecordTelemetry(_slot, ComposeRuntimeFlags());
        }

        private void PublishRadiationAndHeat()
        {
            if (!Application.isPlaying || _sourceId == 0 || _reprocessed)
                return;

            Vector3 position = transform.position;
            float normalized = math.saturate(_outputNormalized01);
            float radiationIntensity = _isDead
                ? math.max(0f, deadRadiationIntensity)
                : math.max(0f, radiationIntensityAtFullOutput * math.max(DeadOutputThreshold01, normalized));
            RadiationHazardGrid.RegisterSource(
                _sourceId,
                position,
                radiationIntensity,
                math.max(MinimumRadiationRadiusMeters, radiationRadiusMeters));

            float heatDelta = math.max(0f, thermalDeltaCelsiusAtFullOutput * math.max(DeadOutputThreshold01, normalized));
            if (heatDelta <= 0f)
                return;

            bool injected = s_thermodynamics != null &&
                            s_thermodynamics.TryInjectTransientHeatSource(
                                position,
                                math.max(0.25f, thermalRadiusMeters),
                                heatDelta,
                                unchecked((uint)_sourceId));
            if (injected)
                return;

            if (!TryResolveAupFromRuntimeOrigin(position, out AbsoluteUniversePosition positionAup))
                return;

            TemperatureChangedSignal signal = default;
            signal.PositionAup = positionAup;
            signal.TemperatureCelsius = heatDelta;
            signal.DeltaCelsius = heatDelta;
            signal.Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            signal.SourceId = (ushort)math.min(_sourceId, ushort.MaxValue);
            signal.Flags = TemperatureChangedSignal.FlagSubmarineAmbient;
            SignalBus<TemperatureChangedSignal>.TryPushTracked(in signal, ref s_x001RadioisotopeThermalGeneratorSignalPushDropCount);
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition absoluteAup)
        {
            absoluteAup = default;
            if (!float.IsFinite(runtimePosition.x) ||
                !float.IsFinite(runtimePosition.y) ||
                !float.IsFinite(runtimePosition.z))
            {
                return false;
            }

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            absoluteAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return absoluteAup.IsFinite();
        }

        private void PublishLowOutputHudWarning()
        {
            if (_slot >= 0 && TryResolveRtgFlags(out NativeArray<byte> rtgFlags))
                rtgFlags[_slot] = (byte)(rtgFlags[_slot] | FlagWarned20);

            HUDNotificationSignal signal = default;
            signal.MessageHash = RtgLowOutputMessageHash;
            signal.ContextHash = RtgLowOutputContextHash;
            signal.SourceId = unchecked((uint)_sourceId);
            signal.Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            signal.Severity = 2;
            signal.Flags = 0;
            SignalBus<HUDNotificationSignal>.TryPushTracked(in signal, ref s_x001RadioisotopeThermalGeneratorSignalPushDropCount);
        }

        private void MarkPowerGridDirty()
        {
            PowerGrid grid = _powerNode != null ? _powerNode.Grid : null;
            grid?.MarkDirty();
        }

        private void TryRegisterSaveParticipant()
        {
            if (_registeredSave || !Application.isPlaying || !isActiveAndEnabled)
                return;

            if (_saveService == null)
                _saveService = GlobalRegistry.Save;

            if (_saveService == null)
                return;

            _saveService.Register(this);
            _registeredSave = true;
        }

        private void TryUnregisterSaveParticipant()
        {
            if (!_registeredSave)
                return;

            ISaveService saveService = _saveService;
            if (saveService != null)
                saveService.Unregister(this);
            _registeredSave = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.ThermodynamicsRuntime ||
                serviceSlot == GlobalRegistryServiceSlot.ThermodynamicsService)
            {
                s_thermodynamics = currentService as IThermodynamicsService;
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.Save)
                return;

            TryUnregisterSaveParticipant();
            _saveService = currentService as ISaveService;
            TryRegisterSaveParticipant();
        }

        private static void CacheThermodynamicsServiceCold()
        {
            if (s_thermodynamics == null || !s_thermodynamics.IsInitialized)
                s_thermodynamics = GlobalRegistry.ThermodynamicsService;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }

        private static void RefreshLeader()
        {
            if (!TryResolveRtgFlags(out NativeArray<byte> rtgFlags))
                return;

            int next = -1;
            for (int i = 0; i < MaxRtgs; i++)
            {
                if ((rtgFlags[i] & FlagActive) != 0 && s_instances[i] != null)
                {
                    next = i;
                    break;
                }
            }

            SetLeaderSlot(next);
        }

        private static void SetLeaderSlot(int slot)
        {
            if (s_leaderSlot == slot)
                return;

            if (s_leaderSlot >= 0 && s_instances != null)
                s_instances[s_leaderSlot]?.UnregisterLeaderLanes();

            s_leaderSlot = slot;
            if (s_leaderSlot >= 0 && s_instances != null)
                s_instances[s_leaderSlot]?.RegisterLeaderLanes();
        }

        private void RegisterLeaderLanes()
        {
            if (!Application.isPlaying)
                return;

            if (!_registeredCold)
                _registeredCold = GlobalRegistry.TryRegisterColdTickable(this, PriorityLayer.Environment);
            if (!_registeredLate)
                _registeredLate = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void UnregisterLeaderLanes()
        {
            if (_registeredCold)
            {
                GlobalRegistry.UnregisterColdTickable(this, PriorityLayer.Environment);
                _registeredCold = false;
            }

            if (_registeredLate)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLate = false;
            }
        }

        private void ResolveIdentity()
        {
            _sourceId = NormalizeSourceId(sourceIdOverride);
            if (_sourceId == 0)
            {
                uint idHash = !string.IsNullOrWhiteSpace(stableRtgId)
                    ? unchecked((uint)LocHash.Compute(stableRtgId))
                    : 0u;
                ulong entityId = EntityId.ToULong(gameObject.GetEntityId());
                uint entityHash = unchecked((uint)(entityId ^ (entityId >> 32)));
                if (entityHash == 0u)
                    entityHash = 1u;
                _sourceId = NormalizeSourceId(idHash != 0u ? idHash ^ entityHash : entityHash);
            }

            _depletedIsotopeHash = string.IsNullOrWhiteSpace(depletedIsotopeItemId)
                ? 0u
                : unchecked((uint)LocHash.Compute(depletedIsotopeItemId));
        }

        private static int NormalizeSourceId(uint sourceId)
        {
            int normalized = unchecked((int)(sourceId & 0x7FFFFFFFu));
            return normalized == 0 ? 0 : normalized;
        }

        private void SanitizeInspectorValues()
        {
            baseOutputWatts = math.max(0f, baseOutputWatts);
            halfLifeHours = math.max(MinimumHalfLifeSeconds * math.rcp(SecondsPerHour), halfLifeHours);
            thermalRadiusMeters = math.max(0.25f, thermalRadiusMeters);
            radiationRadiusMeters = math.max(MinimumRadiationRadiusMeters, radiationRadiusMeters);
            radiationIntensityAtFullOutput = math.max(0f, radiationIntensityAtFullOutput);
            deadRadiationIntensity = math.max(0f, deadRadiationIntensity);
            thermalDeltaCelsiusAtFullOutput = math.max(0f, thermalDeltaCelsiusAtFullOutput);
        }

        private static float ResolveCurrentTimeSeconds()
        {
            double now = SystemDispatcher.CurrentUnscaledTimeSeconds;
            if (double.IsNaN(now) || double.IsInfinity(now) || now < 0d)
                return 0f;

            return (float)math.min(now, (double)float.MaxValue);
        }

        private static void EnsureRtgSaveArrays(SaveData data)
        {
            data.EnsureRtgDecayCapacity();
        }

        private static void RecordTelemetry(int slot, byte flags)
        {
            if (slot < 0 ||
                !TryResolveRtgBuffers(
                    out _,
                    out _,
                    out _,
                    out NativeArray<float> currentOutput,
                    out NativeArray<float> outputNormalized,
                    out _,
                    out NativeArray<RtgTelemetryEntry> telemetryRing))
            {
                return;
            }

            RadioisotopeThermalGenerator instance = s_instances != null ? s_instances[slot] : null;
            if (instance == null)
                return;

            int index = s_telemetryCursor % TelemetryCapacity;
            telemetryRing[index] = new RtgTelemetryEntry
            {
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                SourceId = unchecked((uint)instance._sourceId),
                OutputWatts = currentOutput[slot],
                NormalizedOutput01 = outputNormalized[slot],
                AverageHealth01 = s_averageRtgHealth01,
                ActiveRtgs = (ushort)math.clamp(s_activeCount, 0, ushort.MaxValue),
                Flags = flags
            };
            s_telemetryCursor = (s_telemetryCursor + 1) % TelemetryCapacity;
        }

        private static void DumpBlackBoxOnce(uint reasonFlags)
        {
            if (s_blackBoxDumped)
                return;

            if (!TryResolveTelemetryRing(out NativeArray<RtgTelemetryEntry> telemetryRing))
                return;

            try
            {
                long totalBytes = BlackBoxHeaderBytes + ((long)TelemetryCapacity * BlackBoxTelemetryRowBytes);
                if (totalBytes < BlackBoxHeaderBytes || totalBytes > int.MaxValue)
                    return;

                NativeArray<byte> payload = NativeFaultDumpWriter.CreateTransientPayload(
                    (int)totalBytes,
                    nameof(RadioisotopeThermalGenerator),
                    "rtgBlackBoxPayload");
                try
                {
                    WriteUInt32LittleEndian(payload, 0, BlackBoxMagic);
                    WriteUInt32LittleEndian(payload, 4, BlackBoxVersion);
                    WriteUInt32LittleEndian(payload, 8, reasonFlags);
                    WriteInt32LittleEndian(payload, 12, TelemetryCapacity);
                    WriteInt32LittleEndian(payload, 16, RtgTelemetryEntrySizeBytes);
                    WriteInt32LittleEndian(payload, 20, s_telemetryCursor);

                    int cursor = BlackBoxHeaderBytes;
                    for (int i = 0; i < TelemetryCapacity; i++)
                    {
                        int index = (s_telemetryCursor + i) % TelemetryCapacity;
                        WriteRtgTelemetryEntry(payload, cursor, telemetryRing[index]);
                        cursor += BlackBoxTelemetryRowBytes;
                    }

                    s_blackBoxDumped = NativeFaultDumpWriter.TryWriteAll(BlackBoxDumpRelativePath, payload, (int)totalBytes);
                }
                finally
                {
                    NativeFaultDumpWriter.DisposeTransientPayload(
                        ref payload,
                        nameof(RadioisotopeThermalGenerator),
                        "rtgBlackBoxPayload");
                }
            }
            catch (Exception)
            {
            }
        }

        private static void WriteRtgTelemetryEntry(
            NativeArray<byte> destination,
            int offset,
            RtgTelemetryEntry entry)
        {
            WriteUInt32LittleEndian(destination, offset, entry.Frame);
            WriteUInt32LittleEndian(destination, offset + 4, entry.SourceId);
            WriteFloat32LittleEndian(destination, offset + 8, entry.OutputWatts);
            WriteFloat32LittleEndian(destination, offset + 12, entry.NormalizedOutput01);
            WriteFloat32LittleEndian(destination, offset + 16, entry.AverageHealth01);
            WriteUInt16LittleEndian(destination, offset + 20, entry.ActiveRtgs);
            destination[offset + 22] = entry.Flags;
        }

        private static void WriteFloat32LittleEndian(NativeArray<byte> destination, int offset, float value)
        {
            WriteUInt32LittleEndian(destination, offset, math.asuint(value));
        }

        private static void WriteInt32LittleEndian(NativeArray<byte> destination, int offset, int value)
        {
            WriteUInt32LittleEndian(destination, offset, unchecked((uint)value));
        }

        private static void WriteUInt16LittleEndian(NativeArray<byte> destination, int offset, ushort value)
        {
            destination[offset] = (byte)value;
            destination[offset + 1] = (byte)(value >> 8);
        }

        private static void WriteUInt32LittleEndian(NativeArray<byte> destination, int offset, uint value)
        {
            destination[offset] = (byte)value;
            destination[offset + 1] = (byte)(value >> 8);
            destination[offset + 2] = (byte)(value >> 16);
            destination[offset + 3] = (byte)(value >> 24);
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 64)]
        private struct RtgTelemetryEntry
        {
            [System.Runtime.InteropServices.FieldOffset(0)]
            public uint Frame;
            [System.Runtime.InteropServices.FieldOffset(4)]
            public uint SourceId;
            [System.Runtime.InteropServices.FieldOffset(8)]
            public float OutputWatts;
            [System.Runtime.InteropServices.FieldOffset(12)]
            public float NormalizedOutput01;
            [System.Runtime.InteropServices.FieldOffset(16)]
            public float AverageHealth01;
            [System.Runtime.InteropServices.FieldOffset(20)]
            public ushort ActiveRtgs;
            [System.Runtime.InteropServices.FieldOffset(22)]
            public byte Flags;
            [System.Runtime.InteropServices.FieldOffset(23)]
            private byte _pad0;
            [System.Runtime.InteropServices.FieldOffset(24)]
            private byte _pad1;
            [System.Runtime.InteropServices.FieldOffset(25)]
            private byte _pad2;
            [System.Runtime.InteropServices.FieldOffset(26)]
            private byte _pad3;
            [System.Runtime.InteropServices.FieldOffset(27)]
            private byte _pad4;
            [System.Runtime.InteropServices.FieldOffset(28)]
            private byte _pad5;
            [System.Runtime.InteropServices.FieldOffset(29)]
            private byte _pad6;
            [System.Runtime.InteropServices.FieldOffset(30)]
            private byte _pad7;
            [System.Runtime.InteropServices.FieldOffset(31)]
            private byte _pad8;
            [System.Runtime.InteropServices.FieldOffset(32)]
            private byte _pad9;
            [System.Runtime.InteropServices.FieldOffset(33)]
            private byte _pad10;
            [System.Runtime.InteropServices.FieldOffset(34)]
            private byte _pad11;
            [System.Runtime.InteropServices.FieldOffset(35)]
            private byte _pad12;
            [System.Runtime.InteropServices.FieldOffset(36)]
            private byte _pad13;
            [System.Runtime.InteropServices.FieldOffset(37)]
            private byte _pad14;
            [System.Runtime.InteropServices.FieldOffset(38)]
            private byte _pad15;
            [System.Runtime.InteropServices.FieldOffset(39)]
            private byte _pad16;
            [System.Runtime.InteropServices.FieldOffset(40)]
            private byte _pad17;
            [System.Runtime.InteropServices.FieldOffset(41)]
            private byte _pad18;
            [System.Runtime.InteropServices.FieldOffset(42)]
            private byte _pad19;
            [System.Runtime.InteropServices.FieldOffset(43)]
            private byte _pad20;
            [System.Runtime.InteropServices.FieldOffset(44)]
            private byte _pad21;
            [System.Runtime.InteropServices.FieldOffset(45)]
            private byte _pad22;
            [System.Runtime.InteropServices.FieldOffset(46)]
            private byte _pad23;
            [System.Runtime.InteropServices.FieldOffset(47)]
            private byte _pad24;
            [System.Runtime.InteropServices.FieldOffset(48)]
            private byte _pad25;
            [System.Runtime.InteropServices.FieldOffset(49)]
            private byte _pad26;
            [System.Runtime.InteropServices.FieldOffset(50)]
            private byte _pad27;
            [System.Runtime.InteropServices.FieldOffset(51)]
            private byte _pad28;
            [System.Runtime.InteropServices.FieldOffset(52)]
            private byte _pad29;
            [System.Runtime.InteropServices.FieldOffset(53)]
            private byte _pad30;
            [System.Runtime.InteropServices.FieldOffset(54)]
            private byte _pad31;
            [System.Runtime.InteropServices.FieldOffset(55)]
            private byte _pad32;
            [System.Runtime.InteropServices.FieldOffset(56)]
            private byte _pad33;
            [System.Runtime.InteropServices.FieldOffset(57)]
            private byte _pad34;
            [System.Runtime.InteropServices.FieldOffset(58)]
            private byte _pad35;
            [System.Runtime.InteropServices.FieldOffset(59)]
            private byte _pad36;
            [System.Runtime.InteropServices.FieldOffset(60)]
            private byte _pad37;
            [System.Runtime.InteropServices.FieldOffset(61)]
            private byte _pad38;
            [System.Runtime.InteropServices.FieldOffset(62)]
            private byte _pad39;
            [System.Runtime.InteropServices.FieldOffset(63)]
            private byte _pad40;
        }
    }

    public static class RtgDecayMath
    {
        internal const byte FlagActive = 1 << 0;
        internal const byte FlagDead = 1 << 1;
        internal const byte FlagWarned20 = 1 << 2;
        internal const byte FlagReprocessed = 1 << 3;
        private const float HalfLifeLambda = 0.6931471805599453f;
        private const float PadeEpsilon = 0.000001f;
        private const float PadeInputScale = 0.125f;
        private const float PadeMaxInput = 80f;

        public static float ResolvePadeExpNegative(float x)
        {
            float safeX = math.max(0f, math.isfinite(x) ? math.min(x, PadeMaxInput) : 0f);
            float reducedX = safeX * PadeInputScale;
            float denominator = 1f + reducedX + 0.5f * reducedX * reducedX;
            float pade = math.saturate(math.rcp(math.max(PadeEpsilon, denominator)));
            float pade2 = pade * pade;
            float pade4 = pade2 * pade2;
            return pade4 * pade4;
        }

        public static float ResolveDecayFactor(float currentTimeSeconds, float startTimeSeconds, float halfLifeSeconds)
        {
            float safeHalfLife = math.max(1f, math.isfinite(halfLifeSeconds) ? halfLifeSeconds : 1f);
            float safeCurrentTime = math.max(0f, math.isfinite(currentTimeSeconds) ? currentTimeSeconds : 0f);
            float safeStartTime = math.max(0f, math.isfinite(startTimeSeconds) ? startTimeSeconds : 0f);
            float age = math.max(0f, safeCurrentTime - safeStartTime);
            float lambda = HalfLifeLambda * math.rcp(safeHalfLife);
            return ResolvePadeExpNegative(lambda * age);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct RtgDecayJob : IJobParallelFor
    {
        public float CurrentTimeSeconds;
        public float DeadThreshold01;

        [ReadOnly, NoAlias] public NativeSlice<float> RtgStartTimes;
        [ReadOnly, NoAlias] public NativeSlice<float> RtgHalfLifeSeconds;
        [ReadOnly, NoAlias] public NativeSlice<float> RtgBaseOutputWatts;
        [WriteOnly, NoAlias] public NativeSlice<float> RtgCurrentOutputWatts;
        [WriteOnly, NoAlias] public NativeSlice<float> RtgOutputNormalized;
        [NoAlias] public NativeSlice<byte> RtgFlags;

        public void Execute(int index)
        {
            byte flags = RtgFlags[index];
            if ((flags & RtgDecayMath.FlagActive) == 0)
                return;

            if ((flags & RtgDecayMath.FlagReprocessed) != 0)
            {
                RtgCurrentOutputWatts[index] = 0f;
                RtgOutputNormalized[index] = 0f;
                RtgFlags[index] = (byte)(flags | RtgDecayMath.FlagDead);
                return;
            }

            float baseOutput = math.max(0f, RtgBaseOutputWatts[index]);
            float factor = RtgDecayMath.ResolveDecayFactor(
                CurrentTimeSeconds,
                RtgStartTimes[index],
                RtgHalfLifeSeconds[index]);
            float rawOutput = baseOutput * factor;
            bool dead = (flags & RtgDecayMath.FlagDead) != 0 ||
                        factor < math.max(0f, DeadThreshold01);
            RtgOutputNormalized[index] = math.saturate(factor);
            RtgCurrentOutputWatts[index] = dead ? 0f : rawOutput;
            RtgFlags[index] = dead
                ? (byte)(flags | RtgDecayMath.FlagDead)
                : (byte)(flags & unchecked((byte)~RtgDecayMath.FlagDead));
        }
    }
}
