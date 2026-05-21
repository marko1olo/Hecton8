using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.UI;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Audio
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Audio/Vocal Warning System")]
    public sealed class VocalWarningSystem : MonoBehaviour, IVocalWarningSystem, IUpdatable, ISlowTickable, IGlobalRegistryHotSwapListener, IGlobalRegistryHotSwapRefListener
    {
        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct VwsTelemetryEntry
        {
            [FieldOffset(0)] public uint Frame;
            [FieldOffset(4)] public uint SourceId;
            [FieldOffset(8)] public float Severity01;
            [FieldOffset(12)] public float CooldownCrush;
            [FieldOffset(16)] public float CooldownHull;
            [FieldOffset(20)] public float CooldownOxygen;
            [FieldOffset(24)] public float CooldownRadiation;
            [FieldOffset(28)] public float CooldownPower;
            [FieldOffset(32)] public byte CurrentWarningId;
            [FieldOffset(33)] public byte QueueCount;
            [FieldOffset(34)] public byte PendingCount;
            [FieldOffset(35)] public byte Flags;
        }

        private struct VwsVaultViews
        {
            public NativeArray<byte> Queue;
            public NativeArray<byte> WarningFlags;
            public NativeArray<float> Cooldowns;
            public NativeArray<float> WarningSeverity;
            public NativeArray<uint> WarningSourceIds;
            public NativeArray<VwsTelemetryEntry> TelemetryRing;
        }

        private const int QueueCapacity = 16;
        private const int WarningStateLength = 6;
        private const int TelemetryCapacity = 300;
        private const float DefaultCooldownSeconds = 4f;
        private const float SlowTickDeltaSeconds = 0.1f;
        private const float DefaultGain = 0.85f;
        private const SystemID VaultOwner = SystemID.AudioVocalWarning;

        private static readonly string[] SubtitleFallbacks =
        {
            string.Empty,
            "Crush depth",
            "Hull breach",
            "Oxygen low",
            "Radiation",
            "Power low"
        };

        [Header("Mix")]
        [Tooltip("Voice gain applied before the procedural renderer safety limiter.")]
        [SerializeField, Range(0f, 1f)] private float voiceGain = DefaultGain;
        [Tooltip("Cooldown used when a producer does not provide a positive finite cooldown.")]
        [SerializeField, Min(0f)] private float fallbackCooldownSeconds = DefaultCooldownSeconds;

        private IDataVault _dataVault;
        private VaultGenerationHandle<byte> _vwsQueueHandle;
        private VaultGenerationHandle<byte> _warningFlagsHandle;
        private VaultGenerationHandle<float> _cooldownsHandle;
        private VaultGenerationHandle<float> _warningSeverityHandle;
        private VaultGenerationHandle<uint> _warningSourceIdsHandle;
        private VaultGenerationHandle<VwsTelemetryEntry> _telemetryRingHandle;
        private int _telemetryCursor;
        private int _queueCount;
        private int _registeredUpdate;
        private int _registeredSlowTick;
        private int _registeredHotSwap;
        private int _registeredRuntime;
        private int _nativeAllocated;
        private int _telemetryDumpRequested;
        private int _telemetryDumped;
        private SubtitleManager _subtitles;
        private float _globalQualityWeight01 = 1f;
        private float _warningPlaybackRemainingSeconds;
        private byte _currentWarningId;

        /// <inheritdoc />
        public bool IsInitialized => Volatile.Read(ref _nativeAllocated) != 0;

        /// <inheritdoc />
        public int PendingCount => math.max(0, _queueCount);

        /// <inheritdoc />
        public byte CurrentWarningId => _currentWarningId;

        /// <inheritdoc />
        public bool IsWarningActive
        {
            get
            {
                return _warningPlaybackRemainingSeconds > 0f;
            }
        }

        private void Awake()
        {
            EnsureNativeStorage();
            RefreshCachedServicesCold();
        }

        private void OnEnable()
        {
            EnsureNativeStorage();
            TryRegisterHotSwapListener();
            RefreshCachedServicesCold();
            GlobalRegistry.RegisterVocalWarningRuntime(this);
            Volatile.Write(ref _registeredRuntime, 1);
            if (GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment))
                _registeredUpdate = 1;
            if (GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment))
                _registeredSlowTick = 1;
        }

        private void OnDisable()
        {
            UnregisterRuntime();
        }

        private void OnDestroy()
        {
            UnregisterRuntime();
            DisposeNativeStorage();
        }

        private void UnregisterRuntime()
        {
            CancelRendererPlaybackAndClearQueues();
            if (Interlocked.Exchange(ref _registeredHotSwap, 0) != 0)
                GlobalRegistry.UnregisterHotSwapListener(this);
            if (Interlocked.Exchange(ref _registeredSlowTick, 0) != 0)
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            if (Interlocked.Exchange(ref _registeredUpdate, 0) != 0)
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            if (Interlocked.Exchange(ref _registeredRuntime, 0) != 0)
                GlobalRegistry.UnregisterVocalWarningRuntime(this);
            _subtitles = null;
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (Volatile.Read(ref _nativeAllocated) == 0)
                return;

            _globalQualityWeight01 = ResolveGlobalQualityWeight01();
            DrainSignals();
            _warningPlaybackRemainingSeconds = math.max(0f, _warningPlaybackRemainingSeconds - math.max(0f, deltaTime));
            PollRendererState();
            WriteTelemetry();
            FlushTelemetryDumpRequest();
            _ = deltaTime;
        }

        /// <inheritdoc />
        public void SlowTick()
        {
            if (Volatile.Read(ref _nativeAllocated) == 0)
                return;

            if (!TryResolveVwsViews(out VwsVaultViews views))
                return;

            DecayCooldownsInline(ref views, SlowTickDeltaSeconds);
            SortPriorityQueue(ref views);
            TryStartOrPreemptWarning(ref views);
            WriteTelemetry(ref views);
        }

        /// <inheritdoc />
        public bool TryQueueWarning(byte warningId, float severity01, float cooldownSeconds, byte flags, uint sourceId)
        {
            if (Volatile.Read(ref _nativeAllocated) == 0 || Volatile.Read(ref _registeredRuntime) == 0)
                return false;

            byte normalized = NormalizeWarningId(warningId);
            if (normalized == 0)
            {
                Interlocked.Exchange(ref _telemetryDumpRequested, 1);
                return false;
            }

            if (!TryResolveVwsViews(out VwsVaultViews views) ||
                normalized >= views.Cooldowns.Length ||
                normalized >= views.WarningFlags.Length ||
                normalized >= views.WarningSeverity.Length ||
                normalized >= views.WarningSourceIds.Length)
            {
                Interlocked.Exchange(ref _telemetryDumpRequested, 1);
                return false;
            }

            float cooldown = views.Cooldowns[normalized];
            if (cooldown > 0f)
                return false;

            views.Cooldowns[normalized] = ResolveCooldownSeconds(cooldownSeconds);
            views.WarningFlags[normalized] = flags;
            views.WarningSeverity[normalized] = ResolveSeverity01(severity01);
            views.WarningSourceIds[normalized] = sourceId;

            InsertOrPromote(ref views, normalized);
            return true;
        }

        /// <inheritdoc />
        public void CancelCurrentWarning()
        {
            CancelRendererPlaybackAndClearQueues();
        }

        private void CancelRendererPlaybackAndClearQueues()
        {
            _currentWarningId = 0;
            _warningPlaybackRemainingSeconds = 0f;
            if (Volatile.Read(ref _nativeAllocated) == 0)
                return;

            ClearQueuedWarnings();
        }

        /// <inheritdoc />
        public void OnGlobalRegistryServiceRebound(GlobalRegistryServiceSlot serviceSlot, ref object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                RebindDataVault(currentService as IDataVault);
                return;
            }

            if (Volatile.Read(ref _nativeAllocated) == 0)
                return;

            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.SubtitleRuntime:
                    _subtitles = currentService as SubtitleManager;
                    break;
            }
        }

        /// <inheritdoc />
        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault &&
                !ReferenceEquals(previousService, currentService))
                RebindDataVault(currentService as IDataVault);
        }

        private void EnsureNativeStorage()
        {
            if (Volatile.Read(ref _nativeAllocated) != 0)
                return;

            IDataVault vault = ResolveDataVaultCold();
            if (vault == null)
                return;

            BindVaultStorage(vault);
            if (!TryResolveVwsViews(out _))
            {
                ClearVaultDescriptors();
                return;
            }

            Volatile.Write(ref _nativeAllocated, 1);
        }

        private IDataVault ResolveDataVaultCold()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
            {
                vault = GlobalRegistry.DataVault;
                _dataVault = vault;
            }

            return vault;
        }

        private void BindVaultStorage(IDataVault vault)
        {
            _dataVault = vault;
            _vwsQueueHandle = vault.GetGenerationHandle<byte>(
                BufferID.AudioVocalWarningQueue,
                QueueCapacity,
                VaultOwner,
                NativeArrayOptions.ClearMemory);
            _warningFlagsHandle = vault.GetGenerationHandle<byte>(
                BufferID.AudioVocalWarningFlags,
                WarningStateLength,
                VaultOwner,
                NativeArrayOptions.ClearMemory);
            _cooldownsHandle = vault.GetGenerationHandle<float>(
                BufferID.AudioVocalWarningCooldowns,
                WarningStateLength,
                VaultOwner,
                NativeArrayOptions.ClearMemory);
            _warningSeverityHandle = vault.GetGenerationHandle<float>(
                BufferID.AudioVocalWarningSeverity,
                WarningStateLength,
                VaultOwner,
                NativeArrayOptions.ClearMemory);
            _warningSourceIdsHandle = vault.GetGenerationHandle<uint>(
                BufferID.AudioVocalWarningSourceIds,
                WarningStateLength,
                VaultOwner,
                NativeArrayOptions.ClearMemory);
            _telemetryRingHandle = vault.GetGenerationHandle<VwsTelemetryEntry>(
                BufferID.AudioVocalWarningTelemetry,
                TelemetryCapacity,
                VaultOwner,
                NativeArrayOptions.ClearMemory);
        }

        private void RebindDataVault(IDataVault vault)
        {
            if (ReferenceEquals(_dataVault, vault))
                return;

            ReleaseVaultBackedStorage();
            _dataVault = vault;
            Volatile.Write(ref _nativeAllocated, 0);
            _queueCount = 0;
            _currentWarningId = 0;
            if (vault != null)
                EnsureNativeStorage();
        }

        private void ReleaseVaultBackedStorage()
        {
            IDataVault vault = _dataVault;
            ReleaseVaultBuffer(vault, ref _vwsQueueHandle);
            ReleaseVaultBuffer(vault, ref _warningFlagsHandle);
            ReleaseVaultBuffer(vault, ref _cooldownsHandle);
            ReleaseVaultBuffer(vault, ref _warningSeverityHandle);
            ReleaseVaultBuffer(vault, ref _warningSourceIdsHandle);
            ReleaseVaultBuffer(vault, ref _telemetryRingHandle);

            ClearVaultDescriptors();
        }

        private static void ReleaseVaultBuffer<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (vault != null && handle.BufferID != 0u)
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private void ClearVaultDescriptors()
        {
            _vwsQueueHandle = default;
            _warningFlagsHandle = default;
            _cooldownsHandle = default;
            _warningSeverityHandle = default;
            _warningSourceIdsHandle = default;
            _telemetryRingHandle = default;
        }

        private bool TryResolveVwsViews(out VwsVaultViews views)
        {
            views = default;
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (!vault.TryResolveHandle(in _vwsQueueHandle, out views.Queue) ||
                !vault.TryResolveHandle(in _warningFlagsHandle, out views.WarningFlags) ||
                !vault.TryResolveHandle(in _cooldownsHandle, out views.Cooldowns) ||
                !vault.TryResolveHandle(in _warningSeverityHandle, out views.WarningSeverity) ||
                !vault.TryResolveHandle(in _warningSourceIdsHandle, out views.WarningSourceIds) ||
                !vault.TryResolveHandle(in _telemetryRingHandle, out views.TelemetryRing) ||
                !views.Queue.IsCreated ||
                !views.WarningFlags.IsCreated ||
                !views.Cooldowns.IsCreated ||
                !views.WarningSeverity.IsCreated ||
                !views.WarningSourceIds.IsCreated ||
                !views.TelemetryRing.IsCreated)
            {
                views = default;
                return false;
            }

            return true;
        }

        private void RefreshCachedServicesCold()
        {
            _subtitles = GlobalRegistry.Subtitles;
            _globalQualityWeight01 = ResolveGlobalQualityWeight01();
        }

        private void TryRegisterHotSwapListener()
        {
            if (Volatile.Read(ref _registeredHotSwap) != 0)
                return;

            if (GlobalRegistry.TryRegisterHotSwapListener(this))
                Volatile.Write(ref _registeredHotSwap, 1);
        }

        private void DisposeNativeStorage()
        {
            if (Interlocked.Exchange(ref _nativeAllocated, 0) == 0 &&
                _dataVault == null)
                return;

            ReleaseVaultBackedStorage();

            _dataVault = null;
            _queueCount = 0;
            _currentWarningId = 0;
        }

        private void DrainSignals()
        {
            while (GlobalSignals.TryDequeueVocalWarning(out VocalWarningSignal signal))
            {
                byte warningId = ResolveWarningId(signal.WarningHash, signal.Priority);
                TryQueueWarning(warningId, signal.Severity01, signal.CooldownSeconds, signal.Flags, signal.SourceId);
            }

            while (GlobalSignals.TryDequeueVitalWarning(out VitalWarningSignal signal))
            {
                byte warningId = ResolveWarningId(signal.WarningHash, signal.Priority);
                if (warningId == 0)
                    warningId = (byte)VocalWarningId.OxygenLow;
                TryQueueWarning(warningId, math.max(signal.Vital01, signal.Severity01), fallbackCooldownSeconds, signal.Flags, signal.SourceId);
            }

            while (GlobalSignals.TryDequeueCrushWarning(out CrushWarningSignal signal))
            {
                byte warningId = ResolveWarningId(signal.WarningHash, signal.Priority);
                if (warningId == 0)
                    warningId = (byte)VocalWarningId.CrushDepth;
                TryQueueWarning(warningId, signal.Severity01, fallbackCooldownSeconds, signal.Flags, signal.SourceId);
            }

            while (GlobalSignals.TryDequeueBrownout(out BrownoutSignal signal))
            {
                TryQueueWarning((byte)VocalWarningId.PowerLow, signal.Severity01, fallbackCooldownSeconds, signal.Flags, signal.NetworkId);
            }
        }

        private void InsertOrPromote(ref VwsVaultViews views, byte warningId)
        {
            byte normalized = NormalizeWarningId(warningId);
            if (normalized == 0)
            {
                Interlocked.Exchange(ref _telemetryDumpRequested, 1);
                return;
            }

            NativeArray<byte> queue = views.Queue;
            if (!queue.IsCreated || queue.Length <= 0)
                return;

            int capacity = math.min(QueueCapacity, queue.Length);
            int currentCount = math.clamp(_queueCount, 0, capacity);
            _queueCount = currentCount;
            for (int i = 0; i < currentCount; i++)
            {
                if (queue[i] == normalized)
                    return;
            }

            if (currentCount < capacity)
            {
                queue[currentCount] = normalized;
                _queueCount = currentCount + 1;
                return;
            }

            int worstIndex = 0;
            byte worstId = queue[0];
            for (int i = 1; i < capacity; i++)
            {
                byte candidate = queue[i];
                if (candidate > worstId)
                {
                    worstId = candidate;
                    worstIndex = i;
                }
            }

            if (normalized < worstId)
                queue[worstIndex] = normalized;
        }

        private void SortPriorityQueue(ref VwsVaultViews views)
        {
            SortPriorityQueueInline(ref views);
        }

        private void DecayCooldownsInline(ref VwsVaultViews views, float deltaSeconds)
        {
            NativeArray<float> cooldowns = views.Cooldowns;
            if (!cooldowns.IsCreated)
                return;

            float dt = math.max(0f, deltaSeconds);
            for (int i = 0; i < cooldowns.Length; i++)
                cooldowns[i] = math.max(0f, cooldowns[i] - dt);
        }

        private void SortPriorityQueueInline(ref VwsVaultViews views)
        {
            NativeArray<byte> queue = views.Queue;
            if (!queue.IsCreated)
                return;

            int count = math.clamp(_queueCount, 0, math.min(QueueCapacity, queue.Length));
            for (int i = 1; i < count; i++)
            {
                byte value = queue[i];
                int j = i - 1;
                while (j >= 0 && queue[j] > value)
                {
                    queue[j + 1] = queue[j];
                    j--;
                }

                queue[j + 1] = value;
            }
        }

        private void TryStartOrPreemptWarning(ref VwsVaultViews views)
        {
            NativeArray<byte> queue = views.Queue;
            if (_queueCount <= 0 || !queue.IsCreated || queue.Length <= 0)
                return;

            bool active = _warningPlaybackRemainingSeconds > 0f;
            byte activeId = active ? _currentWarningId : (byte)0;
            if (!active)
                _currentWarningId = 0;

            byte nextId = queue[0];
            bool preempt = active && activeId != 0 && nextId < activeId;
            if (active && !preempt)
                return;

            float radioDistortion01 = ResolveRadioDistortion01(ref views, nextId);
            float durationSeconds = EstimateWarningDurationSeconds(ref views, nextId);
            uint hash = VocalWarningHashes.FromWarningId(nextId);
            if (hash != 0u)
            {
                VocalCueSignal cue = default;
                cue.PhraseHashID = hash;
                cue.Priority = 255 - nextId;
                cue.VolumeScalar = voiceGain;
                cue.PlaybackSpeed = 1f;
                cue.RadioDistortion01 = radioDistortion01;
                cue.SpatialBlend01 = 0f;
                cue.Flags = preempt ? 1u : 0u;
                GlobalSignals.Publish(in cue);
                _currentWarningId = nextId;
                _warningPlaybackRemainingSeconds = durationSeconds;
                EmitSubtitle(nextId, durationSeconds);
            }

            RemoveQueueHead(ref views);
        }

        private void PollRendererState()
        {
            if (_warningPlaybackRemainingSeconds <= 0f)
                _currentWarningId = 0;
        }

        private void EmitSubtitle(byte warningId, float durationSeconds)
        {
            uint hash = VocalWarningHashes.FromWarningId(warningId);
            if (hash == 0u)
                return;

            float duration = math.max(0.25f, math.isfinite(durationSeconds) ? durationSeconds : 0.25f);
            SubtitleSignal subtitleSignal = new SubtitleSignal
            {
                SubtitleHash = hash,
                DurationSeconds = duration,
                Frame = (uint)math.max(0, Time.frameCount),
                Priority = warningId,
                Flags = 0
            };
            GlobalSignals.Publish(in subtitleSignal);

            SubtitleManager subtitles = _subtitles;
            if (subtitles == null)
                return;

            string fallback = warningId < SubtitleFallbacks.Length ? SubtitleFallbacks[warningId] : string.Empty;
            subtitles.DisplaySubtitle(unchecked((int)hash), fallback.AsSpan(), duration);
        }

        private float ResolveRadioDistortion01(ref VwsVaultViews views, byte warningId)
        {
            NativeArray<byte> warningFlags = views.WarningFlags;
            byte flags = warningFlags.IsCreated && warningId < warningFlags.Length ? warningFlags[warningId] : (byte)0;
            if ((flags & VocalWarningSignalFlags.HabitatIntegrityCompromised) == 0)
                return 0.38f;

            float qualityCurve = SmoothQuality01(_globalQualityWeight01);
            return math.lerp(0.38f, 0.72f, qualityCurve);
        }

        private static float ResolveGlobalQualityWeight01()
        {
            float value = Hecton8.Core.HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(1f, value, math.isfinite(value)));
        }

        private static float SmoothQuality01(float quality)
        {
            float t = math.saturate(math.select(1f, quality, math.isfinite(quality)));
            return t * t * (3f - 2f * t);
        }

        private float EstimateWarningDurationSeconds(ref VwsVaultViews views, byte warningId)
        {
            NativeArray<float> severities = views.WarningSeverity;
            float severity = severities.IsCreated && warningId < severities.Length ? severities[warningId] : 0.5f;
            return math.lerp(1.1f, 2.2f, math.saturate(severity));
        }

        private void RemoveQueueHead(ref VwsVaultViews views)
        {
            if (_queueCount <= 0)
                return;

            NativeArray<byte> queue = views.Queue;
            if (!queue.IsCreated || queue.Length <= 0)
                return;

            int count = math.clamp(_queueCount, 0, math.min(QueueCapacity, queue.Length));
            if (count <= 0)
                return;

            int last = count - 1;
            for (int i = 0; i < last; i++)
                queue[i] = queue[i + 1];

            queue[last] = 0;
            _queueCount = last;
        }

        private void ClearQueuedWarnings()
        {
            if (TryResolveVwsViews(out VwsVaultViews views))
            {
                NativeArray<byte> queue = views.Queue;
                for (int i = 0; i < queue.Length; i++)
                    queue[i] = 0;
            }

            _queueCount = 0;
        }

        private static byte ResolveWarningId(uint warningHash, byte priority)
        {
            byte fromHash = VocalWarningHashes.ToWarningId(warningHash);
            if (fromHash != 0)
                return fromHash;

            return NormalizeWarningId(priority);
        }

        private static byte NormalizeWarningId(byte warningId)
        {
            return warningId >= (byte)VocalWarningId.CrushDepth && warningId <= (byte)VocalWarningId.PowerLow
                ? warningId
                : (byte)0;
        }

        private float ResolveCooldownSeconds(float requestedCooldownSeconds)
        {
            float authoredFallback = math.isfinite(fallbackCooldownSeconds)
                ? fallbackCooldownSeconds
                : DefaultCooldownSeconds;
            float fallback = math.max(0f, authoredFallback);
            float value = requestedCooldownSeconds > 0f ? requestedCooldownSeconds : fallback;
            return math.isfinite(value) ? value : fallback;
        }

        private static float ResolveSeverity01(float severity01)
        {
            return math.isfinite(severity01) ? math.saturate(severity01) : 0f;
        }

        private void WriteTelemetry()
        {
            if (!TryResolveVwsViews(out VwsVaultViews views))
                return;

            WriteTelemetry(ref views);
        }

        private void WriteTelemetry(ref VwsVaultViews views)
        {
            NativeArray<VwsTelemetryEntry> telemetryRing = views.TelemetryRing;
            NativeArray<byte> queue = views.Queue;
            NativeArray<uint> sourceIds = views.WarningSourceIds;
            NativeArray<float> severity = views.WarningSeverity;
            NativeArray<float> cooldowns = views.Cooldowns;
            if (!telemetryRing.IsCreated || telemetryRing.Length <= 0 || !queue.IsCreated)
                return;

            int cursor = _telemetryCursor;
            if ((uint)cursor >= (uint)telemetryRing.Length)
                cursor = 0;

            byte flags = 0;
            int queueCount = math.clamp(_queueCount, 0, math.min(QueueCapacity, queue.Length));
            for (int i = 0; i < queueCount; i++)
            {
                if (NormalizeWarningId(queue[i]) == 0)
                    flags |= 1;
            }

            if (flags != 0)
                Interlocked.Exchange(ref _telemetryDumpRequested, 1);

            byte current = _currentWarningId;
            int crush = (int)VocalWarningId.CrushDepth;
            int hull = (int)VocalWarningId.HullBreach;
            int oxygen = (int)VocalWarningId.OxygenLow;
            int radiation = (int)VocalWarningId.Radiation;
            int power = (int)VocalWarningId.PowerLow;
            telemetryRing[cursor] = new VwsTelemetryEntry
            {
                Frame = (uint)math.max(0, Time.frameCount),
                SourceId = sourceIds.IsCreated && current < sourceIds.Length ? sourceIds[current] : 0u,
                Severity01 = severity.IsCreated && current < severity.Length ? severity[current] : 0f,
                CooldownCrush = cooldowns.IsCreated && crush < cooldowns.Length ? cooldowns[crush] : 0f,
                CooldownHull = cooldowns.IsCreated && hull < cooldowns.Length ? cooldowns[hull] : 0f,
                CooldownOxygen = cooldowns.IsCreated && oxygen < cooldowns.Length ? cooldowns[oxygen] : 0f,
                CooldownRadiation = cooldowns.IsCreated && radiation < cooldowns.Length ? cooldowns[radiation] : 0f,
                CooldownPower = cooldowns.IsCreated && power < cooldowns.Length ? cooldowns[power] : 0f,
                CurrentWarningId = current,
                QueueCount = (byte)queueCount,
                PendingCount = 0,
                Flags = flags
            };

            cursor++;
            if (cursor >= telemetryRing.Length)
                cursor = 0;
            _telemetryCursor = cursor;
        }

        private void FlushTelemetryDumpRequest()
        {
            if (Interlocked.Exchange(ref _telemetryDumpRequested, 0) == 0)
                return;

            DumpTelemetryCold();
        }

        private void DumpTelemetryCold()
        {
            if (!TryResolveVwsViews(out VwsVaultViews views))
                return;

            if (Interlocked.Exchange(ref _telemetryDumped, 1) != 0)
                return;

            try
            {
                string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string directory = Path.Combine(root, "Docs", "AgentLogs");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "Dump_AUDIO_VWS_SYSTEM.bin");
                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(TelemetryCapacity);
                    writer.Write(_telemetryCursor);
                    NativeArray<VwsTelemetryEntry> telemetryRing = views.TelemetryRing;
                    for (int i = 0; i < telemetryRing.Length; i++)
                    {
                        VwsTelemetryEntry entry = telemetryRing[i];
                        writer.Write(entry.Frame);
                        writer.Write(entry.SourceId);
                        writer.Write(entry.Severity01);
                        writer.Write(entry.CooldownCrush);
                        writer.Write(entry.CooldownHull);
                        writer.Write(entry.CooldownOxygen);
                        writer.Write(entry.CooldownRadiation);
                        writer.Write(entry.CooldownPower);
                        writer.Write(entry.CurrentWarningId);
                        writer.Write(entry.QueueCount);
                        writer.Write(entry.PendingCount);
                        writer.Write(entry.Flags);
                    }
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
