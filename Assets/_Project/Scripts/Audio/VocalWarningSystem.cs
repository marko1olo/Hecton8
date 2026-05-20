using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using ScalabilityChangedEvent = Hecton8.Core.Contracts.Signals.ScalabilityChangedEvent;
using Hecton8.Core.Memory;
using Hecton8.UI;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Audio
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Audio/Vocal Warning System")]
    public sealed class VocalWarningSystem : MonoBehaviour, IVocalWarningSystem, IUpdatable, ISlowTickable, ILocalizationLanguageChangedListener, IGlobalRegistryHotSwapListener, IGlobalRegistryHotSwapRefListener
    {
        [Serializable]
        private struct VocalWarningLocalizedClipBundle
        {
            [Tooltip("Language served by this flat warning clip table.")]
            [SerializeField] private GameLanguage language;
            [Tooltip("Flat clip table indexed by VocalWarningId byte. Element 0 is unused.")]
            [SerializeField] private AudioClip[] clips;

            public GameLanguage Language => language;
            public AudioClip[] Clips => clips;
        }

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

        private const int QueueCapacity = 16;
        private const int WarningStateLength = 6;
        private const int TelemetryCapacity = 300;
        private const float DefaultCooldownSeconds = 4f;
        private const float SlowTickDeltaSeconds = 0.1f;
        private const float DefaultGain = 0.85f;
        private const string NativeMemoryOwner = nameof(VocalWarningSystem);
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

        [Header("Warning Clips")]
        [Tooltip("Flat clip table indexed by VocalWarningId byte. Element 0 is unused.")]
        [SerializeField] private AudioClip[] defaultWarningClips;
        [Tooltip("Optional flat clip tables for localized Bitchin' Betty voices. Each table uses the same VocalWarningId indexing as defaultWarningClips.")]
        [SerializeField] private VocalWarningLocalizedClipBundle[] localizedBundles;

        [Header("Mix")]
        [Tooltip("Voice gain applied before the procedural renderer safety limiter.")]
        [SerializeField, Range(0f, 1f)] private float voiceGain = DefaultGain;
        [Tooltip("Cooldown used when a producer does not provide a positive finite cooldown.")]
        [SerializeField, Min(0f)] private float fallbackCooldownSeconds = DefaultCooldownSeconds;

        private NativeQueue<byte> _pendingWarningIds;
        private NativeArray<byte> _vwsQueue; // Vault alias; GlobalDataVault owns backing memory.
        private NativeArray<byte> _warningFlags; // Vault alias; GlobalDataVault owns backing memory.
        private NativeArray<float> _cooldowns; // Vault alias; GlobalDataVault owns backing memory.
        private NativeArray<float> _warningSeverity; // Vault alias; GlobalDataVault owns backing memory.
        private NativeArray<uint> _warningSourceIds; // Vault alias; GlobalDataVault owns backing memory.
        private NativeArray<VwsTelemetryEntry> _telemetryRing; // Vault alias; GlobalDataVault owns backing memory.
        private IDataVault _dataVault;
        private VaultBufferHandle<byte> _vwsQueueHandle;
        private VaultBufferHandle<byte> _warningFlagsHandle;
        private VaultBufferHandle<float> _cooldownsHandle;
        private VaultBufferHandle<float> _warningSeverityHandle;
        private VaultBufferHandle<uint> _warningSourceIdsHandle;
        private VaultBufferHandle<VwsTelemetryEntry> _telemetryRingHandle;
        private AudioClip[] _activeWarningClips;
        private int _telemetryCursor;
        private int _pendingNativeCount;
        private int _queueCount;
        private int _registeredUpdate;
        private int _registeredSlowTick;
        private int _registeredLocalization;
        private int _registeredHotSwap;
        private int _registeredRuntime;
        private int _nativeAllocated;
        private int _telemetryDumpRequested;
        private int _telemetryDumped;
        private int _lastScalabilitySignalFrame = -4096;
        private PlayerCriticalProceduralAudioRenderer _renderer;
        private SubtitleManager _subtitles;
        private LocalizationManager _localization;
        private HectonQualityTier _qualityTier = HectonQualityTier.Unknown;
        private byte _currentWarningId;

        /// <inheritdoc />
        public bool IsInitialized => Volatile.Read(ref _nativeAllocated) != 0;

        /// <inheritdoc />
        public int PendingCount => math.max(0, _queueCount) + math.max(0, Volatile.Read(ref _pendingNativeCount));

        /// <inheritdoc />
        public byte CurrentWarningId => _currentWarningId;

        /// <inheritdoc />
        public bool IsWarningActive
        {
            get
            {
                PlayerCriticalProceduralAudioRenderer renderer = _renderer;
                return renderer != null && renderer.IsVocalWarningPlaying;
            }
        }

        private void Awake()
        {
            EnsureNativeStorage();
            RefreshCachedServicesCold();
            SelectActiveClipBundle(ResolveCurrentLanguage());
        }

        private void OnEnable()
        {
            EnsureNativeStorage();
            TryRegisterHotSwapListener();
            RefreshCachedServicesCold();
            SelectActiveClipBundle(ResolveCurrentLanguage());
            GlobalRegistry.RegisterVocalWarningRuntime(this);
            Volatile.Write(ref _registeredRuntime, 1);
            if (GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment))
                _registeredUpdate = 1;
            if (GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment))
                _registeredSlowTick = 1;
            LocalizationEvents.RegisterLanguageListener(this);
            _registeredLocalization = 1;
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
            if (Interlocked.Exchange(ref _registeredLocalization, 0) != 0)
                LocalizationEvents.UnregisterLanguageListener(this);
            if (Interlocked.Exchange(ref _registeredHotSwap, 0) != 0)
                GlobalRegistry.UnregisterHotSwapListener(this);
            if (Interlocked.Exchange(ref _registeredSlowTick, 0) != 0)
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            if (Interlocked.Exchange(ref _registeredUpdate, 0) != 0)
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            if (Interlocked.Exchange(ref _registeredRuntime, 0) != 0)
                GlobalRegistry.UnregisterVocalWarningRuntime(this);
            _renderer = null;
            _subtitles = null;
            _localization = null;
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (Volatile.Read(ref _nativeAllocated) == 0)
                return;

            ConsumeScalabilitySignals();
            DrainSignals();
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

            DecayCooldownsInline(SlowTickDeltaSeconds);

            DrainPendingIdsIntoQueue();
            SortPriorityQueue();
            TryStartOrPreemptWarning();
            WriteTelemetry();
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

            float cooldown = _cooldowns[normalized];
            if (cooldown > 0f)
                return false;

            _cooldowns[normalized] = ResolveCooldownSeconds(cooldownSeconds);
            _warningFlags[normalized] = flags;
            _warningSeverity[normalized] = ResolveSeverity01(severity01);
            _warningSourceIds[normalized] = sourceId;

            if (Volatile.Read(ref _pendingNativeCount) >= QueueCapacity)
            {
                InsertOrPromote(normalized);
                return true;
            }

            _pendingWarningIds.Enqueue(normalized);
            Interlocked.Increment(ref _pendingNativeCount);
            return true;
        }

        /// <inheritdoc />
        public void CancelCurrentWarning()
        {
            CancelRendererPlaybackAndClearQueues();
        }

        private void CancelRendererPlaybackAndClearQueues()
        {
            PlayerCriticalProceduralAudioRenderer renderer = _renderer;
            if (renderer != null)
                renderer.CancelVocalWarningPlayback();
            _currentWarningId = 0;
            if (Volatile.Read(ref _nativeAllocated) == 0)
                return;

            ClearQueuedWarnings();
        }

        /// <inheritdoc />
        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)
        {
            int languageIndex = (int)payload.Language;
            GameLanguage language = languageIndex >= (int)GameLanguage.English && languageIndex <= (int)GameLanguage.Arabic
                ? (GameLanguage)languageIndex
                : GameLanguage.English;
            SelectActiveClipBundle(language);
        }

        private void HandleScalabilityChanged(in ScalabilityChangedEvent payload)
        {
            _qualityTier = payload.CurrentQualityTier;
        }

        private void ConsumeScalabilitySignals()
        {
            int frame = Time.frameCount;
            if (_lastScalabilitySignalFrame == frame)
                return;

            _lastScalabilitySignalFrame = frame;
            ReadOnlySpan<ScalabilityChangedEvent> signals = SignalBus<ScalabilityChangedEvent>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                ScalabilityChangedEvent payload = signals[i];
                HandleScalabilityChanged(in payload);
            }
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
                case GlobalRegistryServiceSlot.PlayerCriticalAudioRuntime:
                    _renderer = currentService as PlayerCriticalProceduralAudioRenderer;
                    break;
                case GlobalRegistryServiceSlot.SubtitleRuntime:
                    _subtitles = currentService as SubtitleManager;
                    break;
                case GlobalRegistryServiceSlot.LocalizationRuntime:
                    _localization = currentService as LocalizationManager;
                    SelectActiveClipBundle(ResolveCurrentLanguage());
                    break;
            }
        }

        /// <inheritdoc />
        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.PlayerCriticalAudioRuntime:
                    if (ReferenceEquals(previousService, currentService))
                        return;

                    PlayerCriticalProceduralAudioRenderer previousRenderer = previousService as PlayerCriticalProceduralAudioRenderer;
                    if (previousRenderer != null)
                        previousRenderer.CancelVocalWarningPlayback();
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    if (!ReferenceEquals(previousService, currentService))
                        RebindDataVault(currentService as IDataVault);
                    break;
            }
        }

        private void EnsureNativeStorage()
        {
            if (Volatile.Read(ref _nativeAllocated) != 0)
                return;

            IDataVault vault = ResolveDataVaultCold();
            if (vault == null)
                return;

            if (!_pendingWarningIds.IsCreated)
            {
                _pendingWarningIds = new NativeQueue<byte>(Allocator.Persistent); // COLD ALLOC: NativeQueue<byte>[16] - VWS pending byte IDs; GlobalDataVault has no queue primitive - owner: VocalWarningSystem
                NativeMemorySentinel.RegisterNativeQueue(_pendingWarningIds, QueueCapacity, NativeMemoryOwner, nameof(_pendingWarningIds), NativeAllocationLifetime.Session);
            }

            BindVaultStorage(vault);
            if (!_vwsQueue.IsCreated ||
                !_warningFlags.IsCreated ||
                !_cooldowns.IsCreated ||
                !_warningSeverity.IsCreated ||
                !_warningSourceIds.IsCreated ||
                !_telemetryRing.IsCreated)
            {
                ClearVaultBackedStorageAliases();
                return;
            }

            PrewarmPendingQueue();
            _activeWarningClips = defaultWarningClips;
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
            _vwsQueueHandle = vault.GetBufferHandle<byte>(
                BufferID.AudioVocalWarningQueue,
                QueueCapacity,
                VaultOwner,
                NativeArrayOptions.ClearMemory);
            _warningFlagsHandle = vault.GetBufferHandle<byte>(
                BufferID.AudioVocalWarningFlags,
                WarningStateLength,
                VaultOwner,
                NativeArrayOptions.ClearMemory);
            _cooldownsHandle = vault.GetBufferHandle<float>(
                BufferID.AudioVocalWarningCooldowns,
                WarningStateLength,
                VaultOwner,
                NativeArrayOptions.ClearMemory);
            _warningSeverityHandle = vault.GetBufferHandle<float>(
                BufferID.AudioVocalWarningSeverity,
                WarningStateLength,
                VaultOwner,
                NativeArrayOptions.ClearMemory);
            _warningSourceIdsHandle = vault.GetBufferHandle<uint>(
                BufferID.AudioVocalWarningSourceIds,
                WarningStateLength,
                VaultOwner,
                NativeArrayOptions.ClearMemory);
            _telemetryRingHandle = vault.GetBufferHandle<VwsTelemetryEntry>(
                BufferID.AudioVocalWarningTelemetry,
                TelemetryCapacity,
                VaultOwner,
                NativeArrayOptions.ClearMemory);

            _vwsQueue = _vwsQueueHandle.Resolve(vault);
            _warningFlags = _warningFlagsHandle.Resolve(vault);
            _cooldowns = _cooldownsHandle.Resolve(vault);
            _warningSeverity = _warningSeverityHandle.Resolve(vault);
            _warningSourceIds = _warningSourceIdsHandle.Resolve(vault);
            _telemetryRing = _telemetryRingHandle.Resolve(vault);
        }

        private void RebindDataVault(IDataVault vault)
        {
            if (ReferenceEquals(_dataVault, vault))
                return;

            ReleaseVaultBackedStorage();
            _dataVault = vault;
            Volatile.Write(ref _nativeAllocated, 0);
            _queueCount = 0;
            _pendingNativeCount = 0;
            _currentWarningId = 0;
            if (vault != null)
                EnsureNativeStorage();
        }

        private void ReleaseVaultBackedStorage()
        {
            IDataVault vault = _dataVault;
            if (vault != null)
                vault.ReleaseOwnerBuffers(VaultOwner, out _);

            ClearVaultBackedStorageAliases();
        }

        private void ClearVaultBackedStorageAliases()
        {
            _vwsQueue = default;
            _warningFlags = default;
            _cooldowns = default;
            _warningSeverity = default;
            _warningSourceIds = default;
            _telemetryRing = default;
            _vwsQueueHandle = default;
            _warningFlagsHandle = default;
            _cooldownsHandle = default;
            _warningSeverityHandle = default;
            _warningSourceIdsHandle = default;
            _telemetryRingHandle = default;
        }

        private void PrewarmPendingQueue()
        {
            if (!_pendingWarningIds.IsCreated)
                return;

            for (int i = 0; i < QueueCapacity; i++)
                _pendingWarningIds.Enqueue(0);

            while (_pendingWarningIds.TryDequeue(out _))
            {
            }
        }

        private void RefreshCachedServicesCold()
        {
            _renderer = GlobalRegistry.PlayerCriticalAudio;
            _subtitles = GlobalRegistry.Subtitles;
            _localization = GlobalRegistry.Localization;
            _qualityTier = GlobalRegistry.ScalabilityTier;
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
                !_pendingWarningIds.IsCreated &&
                _dataVault == null)
                return;

            if (_pendingWarningIds.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(NativeMemoryOwner, nameof(_pendingWarningIds));
                _pendingWarningIds.Dispose();
            }

            ReleaseVaultBackedStorage();

            _pendingWarningIds = default;
            _queueCount = 0;
            _pendingNativeCount = 0;
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

        private void DrainPendingIdsIntoQueue()
        {
            while (_pendingWarningIds.TryDequeue(out byte warningId))
            {
                Interlocked.Decrement(ref _pendingNativeCount);
                InsertOrPromote(warningId);
            }
        }

        private void InsertOrPromote(byte warningId)
        {
            byte normalized = NormalizeWarningId(warningId);
            if (normalized == 0)
            {
                Interlocked.Exchange(ref _telemetryDumpRequested, 1);
                return;
            }

            for (int i = 0; i < _queueCount; i++)
            {
                if (_vwsQueue[i] == normalized)
                    return;
            }

            if (_queueCount < QueueCapacity)
            {
                _vwsQueue[_queueCount] = normalized;
                _queueCount++;
                return;
            }

            int worstIndex = 0;
            byte worstId = _vwsQueue[0];
            for (int i = 1; i < QueueCapacity; i++)
            {
                byte candidate = _vwsQueue[i];
                if (candidate > worstId)
                {
                    worstId = candidate;
                    worstIndex = i;
                }
            }

            if (normalized < worstId)
                _vwsQueue[worstIndex] = normalized;
        }

        private void SortPriorityQueue()
        {
            SortPriorityQueueInline();
        }

        private void DecayCooldownsInline(float deltaSeconds)
        {
            if (!_cooldowns.IsCreated)
                return;

            float dt = math.max(0f, deltaSeconds);
            for (int i = 0; i < _cooldowns.Length; i++)
                _cooldowns[i] = math.max(0f, _cooldowns[i] - dt);
        }

        private void SortPriorityQueueInline()
        {
            if (!_vwsQueue.IsCreated)
                return;

            int count = math.clamp(_queueCount, 0, math.min(QueueCapacity, _vwsQueue.Length));
            for (int i = 1; i < count; i++)
            {
                byte value = _vwsQueue[i];
                int j = i - 1;
                while (j >= 0 && _vwsQueue[j] > value)
                {
                    _vwsQueue[j + 1] = _vwsQueue[j];
                    j--;
                }

                _vwsQueue[j + 1] = value;
            }
        }

        private void TryStartOrPreemptWarning()
        {
            PlayerCriticalProceduralAudioRenderer renderer = _renderer;
            if (renderer == null || _queueCount <= 0)
                return;

            bool active = renderer.IsVocalWarningPlaying;
            byte activeId = active ? renderer.CurrentVocalWarningId : (byte)0;
            if (!active)
                _currentWarningId = 0;

            byte nextId = _vwsQueue[0];
            bool preempt = active && activeId != 0 && nextId < activeId;
            if (active && !preempt)
                return;

            if (!TryResolveClip(nextId, out AudioClip clip))
            {
                RemoveQueueHead();
                return;
            }

            bool radioDegrade = ShouldUseRadioDegradation(nextId);
            if (renderer.TrySubmitVocalWarningClip(nextId, clip, voiceGain, preempt, radioDegrade, out float durationSeconds))
            {
                _currentWarningId = nextId;
                EmitSubtitle(nextId, durationSeconds);
                RemoveQueueHead();
            }
        }

        private void PollRendererState()
        {
            PlayerCriticalProceduralAudioRenderer renderer = _renderer;
            if (renderer == null || !renderer.IsVocalWarningPlaying)
                _currentWarningId = 0;
            else
                _currentWarningId = renderer.CurrentVocalWarningId;
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

        private bool TryResolveClip(byte warningId, out AudioClip clip)
        {
            clip = null;
            AudioClip[] clips = _activeWarningClips;
            if (clips == null || warningId >= clips.Length)
                return false;

            clip = clips[warningId];
            return clip != null;
        }

        private void SelectActiveClipBundle(GameLanguage language)
        {
            _activeWarningClips = defaultWarningClips;
            if (localizedBundles == null)
                return;

            for (int i = 0; i < localizedBundles.Length; i++)
            {
                if (localizedBundles[i].Language != language || localizedBundles[i].Clips == null)
                    continue;

                _activeWarningClips = localizedBundles[i].Clips;
                return;
            }
        }

        private GameLanguage ResolveCurrentLanguage()
        {
            LocalizationManager localization = _localization;
            return localization != null ? localization.CurrentLanguage : GameLanguage.English;
        }

        private bool ShouldUseRadioDegradation(byte warningId)
        {
            byte flags = warningId < _warningFlags.Length ? _warningFlags[warningId] : (byte)0;
            if ((flags & VocalWarningSignalFlags.HabitatIntegrityCompromised) == 0)
                return false;

            HectonQualityTier tier = _qualityTier;
            return tier != HectonQualityTier.Low &&
                   tier != HectonQualityTier.Mx350 &&
                   tier != HectonQualityTier.Unknown;
        }

        private void RemoveQueueHead()
        {
            if (_queueCount <= 0)
                return;

            int last = _queueCount - 1;
            for (int i = 0; i < last; i++)
                _vwsQueue[i] = _vwsQueue[i + 1];

            _vwsQueue[last] = 0;
            _queueCount = last;
        }

        private void ClearQueuedWarnings()
        {
            if (_pendingWarningIds.IsCreated)
            {
                while (_pendingWarningIds.TryDequeue(out _))
                {
                }
            }

            if (_vwsQueue.IsCreated)
            {
                for (int i = 0; i < _vwsQueue.Length; i++)
                    _vwsQueue[i] = 0;
            }

            Interlocked.Exchange(ref _pendingNativeCount, 0);
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
            if (!_telemetryRing.IsCreated)
                return;

            int cursor = _telemetryCursor;
            byte flags = 0;
            for (int i = 0; i < _queueCount; i++)
            {
                if (NormalizeWarningId(_vwsQueue[i]) == 0)
                    flags |= 1;
            }

            if (flags != 0)
                Interlocked.Exchange(ref _telemetryDumpRequested, 1);

            _telemetryRing[cursor] = new VwsTelemetryEntry
            {
                Frame = (uint)math.max(0, Time.frameCount),
                SourceId = _currentWarningId < _warningSourceIds.Length ? _warningSourceIds[_currentWarningId] : 0u,
                Severity01 = _currentWarningId < _warningSeverity.Length ? _warningSeverity[_currentWarningId] : 0f,
                CooldownCrush = _cooldowns[(int)VocalWarningId.CrushDepth],
                CooldownHull = _cooldowns[(int)VocalWarningId.HullBreach],
                CooldownOxygen = _cooldowns[(int)VocalWarningId.OxygenLow],
                CooldownRadiation = _cooldowns[(int)VocalWarningId.Radiation],
                CooldownPower = _cooldowns[(int)VocalWarningId.PowerLow],
                CurrentWarningId = _currentWarningId,
                QueueCount = (byte)math.clamp(_queueCount, 0, QueueCapacity),
                PendingCount = (byte)math.clamp(Volatile.Read(ref _pendingNativeCount), 0, 255),
                Flags = flags
            };

            cursor++;
            if (cursor >= TelemetryCapacity)
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
            if (!_telemetryRing.IsCreated)
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
                    for (int i = 0; i < _telemetryRing.Length; i++)
                    {
                        VwsTelemetryEntry entry = _telemetryRing[i];
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
