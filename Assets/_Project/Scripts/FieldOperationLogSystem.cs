using System;
using Hecton8.Core;
using Hecton8.SaveSystem;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Field Operation Log System")]
    public sealed class FieldOperationLogSystem : MonoBehaviour, ISaveable, IServiceHeartbeat, IServiceShutdown, IGlobalRegistryHotSwapListener
    {
        public readonly struct FieldOperationSnapshot
        {
            public readonly string Source;
            public readonly string Title;
            public readonly string Summary;
            public readonly string Severity;

            public FieldOperationSnapshot(string source, string title, string summary, string severity)
            {
                Source = source;
                Title = title;
                Summary = summary;
                Severity = severity;
            }
        }

        private sealed class FieldOperationRecordSlot
        {
            public readonly char[] Source = new char[MaxSourceChars];
            public readonly char[] Title = new char[MaxTitleChars];
            public readonly char[] Summary = new char[MaxSummaryChars];
            public readonly char[] Severity = new char[MaxSeverityChars];
            public int SourceLength;
            public int TitleLength;
            public int SummaryLength;
            public int SeverityLength;

            public ReadOnlySpan<char> SourceSpan => Source.AsSpan(0, SourceLength);
            public ReadOnlySpan<char> TitleSpan => Title.AsSpan(0, TitleLength);
            public ReadOnlySpan<char> SummarySpan => Summary.AsSpan(0, SummaryLength);
            public ReadOnlySpan<char> SeveritySpan => Severity.AsSpan(0, SeverityLength);

            public void Write(
                ReadOnlySpan<char> source,
                ReadOnlySpan<char> title,
                ReadOnlySpan<char> summary,
                ReadOnlySpan<char> severity)
            {
                SourceLength = CopyNormalized(source, DefaultSource.AsSpan(), Source);
                TitleLength = CopyTruncated(title, Title);
                SummaryLength = CopyNormalized(summary, DefaultSummary.AsSpan(), Summary);
                SeverityLength = CopyNormalizedSeverity(severity, Severity);
            }

            public bool Matches(ReadOnlySpan<char> title, ReadOnlySpan<char> summary, ReadOnlySpan<char> severity)
            {
                return TitleSpan.SequenceEqual(title) &&
                       SummarySpan.SequenceEqual(ResolveNormalizedSpan(summary, DefaultSummary.AsSpan())) &&
                       SeveritySpan.SequenceEqual(ResolveSeveritySpan(severity));
            }

            public string CreateSourceString() => CreatePersistentString(Source, SourceLength);
            public string CreateTitleString() => CreatePersistentString(Title, TitleLength);
            public string CreateSummaryString() => CreatePersistentString(Summary, SummaryLength);
            public string CreateSeverityString() => CreatePersistentString(Severity, SeverityLength);
        }

        [SerializeField] private int maxRecentEntries = 10;
        [SerializeField] private bool verboseLogging;

        private const int MaxSourceChars = 48;
        private const int MaxTitleChars = 96;
        private const int MaxSummaryChars = 256;
        private const int MaxSeverityChars = 8;
        private const string DefaultSource = "FIELD";
        private const string DefaultSummary = "Field operation archived.";
        private const string DefaultSeverity = "INFO";
        private const string VerboseOperationRecordedMessage = "[FieldOps] Operation recorded.";
        private readonly FieldOperationRecordSlot[] _recent = new FieldOperationRecordSlot[FieldOperationLogDTO.MaxRecentEntries];
        private int _recentCount;
        private bool _runtimeRegistered;
        private bool _saveRegistered;
        private bool _hotSwapRegistered;
        private ISaveService _saveService;
        private ISaveService _registeredSaveService;
        private static FieldOperationLogSystem s_activeRuntime;

        public int SavePriority => 36;
        public int LoadPriority => 36;
        public int RecentCount => _recentCount;
        public ServiceHeartbeatState HeartbeatState => _runtimeRegistered ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;
        public bool IsServiceReady => _runtimeRegistered;

        public event Action LogChanged;

        private void Awake()
        {
            if (TryAbortForUsableExistingRuntime())
                return;

            EnsureSlots();
        }

        private void OnEnable()
        {
            if (!TryRegisterRuntime())
                return;

            TryRegisterHotSwapListener();
            TryRegisterSaveParticipant();
        }

        private void Start()
        {
            TryRegisterSaveParticipant();
        }

        private void OnDisable()
        {
            TryUnregisterSaveParticipant();
            TryUnregisterHotSwapListener();
            TryUnregisterRuntime();
        }

        private void OnDestroy()
        {
            TryUnregisterSaveParticipant();
            TryUnregisterHotSwapListener();
            TryUnregisterRuntime();
        }

        public static void RecordOperation(string source, string title, string summary, string severity = "INFO")
        {
            FieldOperationLogSystem instance = ResolveActiveRuntime();
            if (instance == null)
                return;

            instance.Push(
                AsSpanOrEmpty(source),
                AsSpanOrEmpty(title),
                AsSpanOrEmpty(summary),
                AsSpanOrEmpty(severity));
        }

        public static void RecordOperation(string source, string title, in FixedCharBuffer summaryBuffer, string severity = "INFO")
        {
            FieldOperationLogSystem instance = ResolveActiveRuntime();
            if (instance == null)
                return;

            instance.Push(
                AsSpanOrEmpty(source),
                AsSpanOrEmpty(title),
                summaryBuffer.AsSpan(),
                AsSpanOrEmpty(severity));
        }

        public static void RecordOperation(string source, in FixedCharBuffer titleBuffer, in FixedCharBuffer summaryBuffer, string severity = "INFO")
        {
            FieldOperationLogSystem instance = ResolveActiveRuntime();
            if (instance == null)
                return;

            instance.Push(
                AsSpanOrEmpty(source),
                titleBuffer.AsSpan(),
                summaryBuffer.AsSpan(),
                AsSpanOrEmpty(severity));
        }

        public void OnServiceShutdown()
        {
            TryUnregisterSaveParticipant();
            TryUnregisterHotSwapListener();
            TryUnregisterRuntime();
            _recentCount = 0;
            LogChanged = null;
            _saveService = null;
        }

        private bool TryRegisterRuntime()
        {
            if (_runtimeRegistered)
                return true;

            if (!Application.isPlaying)
                return false;

            if (TryAbortForUsableExistingRuntime())
                return false;

            GlobalRegistry.RegisterFieldOperationLogRuntime(this);
            _runtimeRegistered = ReferenceEquals(GlobalRegistry.FieldOperations, this);
            if (_runtimeRegistered)
                s_activeRuntime = this;
            return _runtimeRegistered;
        }

        /// <summary>
        /// Yields to an already-usable runtime by destroying THIS COMPONENT, never its host GameObject.
        /// </summary>
        /// <remarks>
        /// FieldOperationLogSystem is authored on the ROOT of Player.prefab, so Destroy(gameObject) here
        /// destroys the entire player. The identical line in
        /// BeaconNetworkSystem.TryAbortForUsableExistingRuntime did exactly that, silently, and made the
        /// world unenterable across three consecutive headless runs - the gate reported PLAYER_NULL, scene
        /// activation timed out, and ActivatePlayer never ran.
        ///
        /// This type has no bootstrap-created twin today, so it only fires against a genuine second player -
        /// a landmine rather than a live defect, and the difference is one double-spawn. Note this one is
        /// reachable from both Awake and TryRegisterRuntime, so it has two arming paths.
        ///
        /// Destroy(this) is the project's own precedent (PlayerActionController), asserted for that component
        /// at Audio/Editor/AdvancedAcousticsSmokeTester.cs:672. The duplicate is the COMPONENT, not its host.
        /// </remarks>
        private bool TryAbortForUsableExistingRuntime()
        {
            FieldOperationLogSystem active = s_activeRuntime;
            if (!ReferenceEquals(active, null) && !ReferenceEquals(active, this))
            {
                if (IsFieldOperationRuntimeUsable(active))
                {
                    Destroy(this);
                    return true;
                }

                if (ReferenceEquals(s_activeRuntime, active))
                    s_activeRuntime = null;
                if (ReferenceEquals(GlobalRegistry.FieldOperations, active))
                    GlobalRegistry.UnregisterFieldOperationLogRuntime(active);
            }

            FieldOperationLogSystem registered = GlobalRegistry.FieldOperations;
            if (ReferenceEquals(registered, null) || ReferenceEquals(registered, this))
                return false;

            if (IsFieldOperationRuntimeUsable(registered))
            {
                s_activeRuntime = registered;

                // Same reasoning as the branch above. See the remarks on this method.
                Destroy(this);
                return true;
            }

            GlobalRegistry.UnregisterFieldOperationLogRuntime(registered);
            if (ReferenceEquals(s_activeRuntime, registered))
                s_activeRuntime = null;
            return false;
        }

        private static FieldOperationLogSystem ResolveActiveRuntime()
        {
            FieldOperationLogSystem active = s_activeRuntime;
            if (IsFieldOperationRuntimeUsable(active))
                return active;

            if (!ReferenceEquals(active, null))
                s_activeRuntime = null;

            FieldOperationLogSystem registered = GlobalRegistry.FieldOperations;
            if (IsFieldOperationRuntimeUsable(registered))
            {
                s_activeRuntime = registered;
                return registered;
            }

            if (!ReferenceEquals(registered, null))
                GlobalRegistry.UnregisterFieldOperationLogRuntime(registered);

            return null;
        }

        private static bool IsFieldOperationRuntimeUsable(FieldOperationLogSystem system)
        {
            return system != null &&
                   system._runtimeRegistered &&
                   system.isActiveAndEnabled;
        }

        private void TryUnregisterRuntime()
        {
            if (!_runtimeRegistered)
                return;

            GlobalRegistry.UnregisterFieldOperationLogRuntime(this);
            _runtimeRegistered = false;
            if (ReferenceEquals(s_activeRuntime, this))
                s_activeRuntime = null;
        }

        private void TryRegisterSaveParticipant()
        {
            if (_saveRegistered)
                return;

            ISaveService saveService = _saveService;
            if (!IsSaveServiceUsable(saveService))
            {
                saveService = GlobalRegistry.Save;
                _saveService = saveService;
            }
            if (!IsSaveServiceUsable(saveService))
                return;

            saveService.Register(this);
            _registeredSaveService = saveService;
            _saveRegistered = true;
        }

        private void TryUnregisterSaveParticipant()
        {
            if (!_saveRegistered && _registeredSaveService == null)
                return;

            ISaveService saveService = _registeredSaveService != null ? _registeredSaveService : _saveService;
            if (saveService != null)
                saveService.Unregister(this);

            _registeredSaveService = null;
            _saveRegistered = false;
            _saveService = null;
        }

        private static bool IsSaveServiceUsable(ISaveService saveService)
        {
            return saveService != null && saveService.IsInitialized;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Save)
                return;

            TryUnregisterSaveParticipant();
            _saveService = currentService as ISaveService;
            TryRegisterSaveParticipant();
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        public int CopyRecentEntries(FieldOperationSnapshot[] buffer)
        {
            if (buffer == null || buffer.Length == 0 || _recentCount == 0)
                return 0;

            int count = Mathf.Min(buffer.Length, _recentCount);
            for (int i = 0; i < count; i++)
            {
                FieldOperationRecordSlot record = _recent[i];
                buffer[i] = new FieldOperationSnapshot(
                    record.CreateSourceString(),
                    record.CreateTitleString(),
                    record.CreateSummaryString(),
                    record.CreateSeverityString());
            }

            return count;
        }

        public bool TryCopyRecentEntry(
            int index,
            Span<char> source,
            out int sourceLength,
            Span<char> title,
            out int titleLength,
            Span<char> summary,
            out int summaryLength,
            Span<char> severity,
            out int severityLength)
        {
            sourceLength = 0;
            titleLength = 0;
            summaryLength = 0;
            severityLength = 0;

            if ((uint)index >= (uint)_recentCount)
                return false;

            FieldOperationRecordSlot record = _recent[index];
            if (record == null)
                return false;

            sourceLength = CopySpan(record.SourceSpan, source);
            titleLength = CopySpan(record.TitleSpan, title);
            summaryLength = CopySpan(record.SummarySpan, summary);
            severityLength = CopySpan(record.SeveritySpan, severity);
            return titleLength > 0;
        }

        public bool TryCopyLatestEntry(
            Span<char> source,
            out int sourceLength,
            Span<char> title,
            out int titleLength,
            Span<char> summary,
            out int summaryLength,
            Span<char> severity,
            out int severityLength)
        {
            return TryCopyRecentEntry(
                0,
                source,
                out sourceLength,
                title,
                out titleLength,
                summary,
                out summaryLength,
                severity,
                out severityLength);
        }

        public bool TryGetLatestEntry(out FieldOperationSnapshot snapshot)
        {
            if (_recentCount <= 0)
            {
                snapshot = default;
                return false;
            }

            FieldOperationRecordSlot record = _recent[0];
            snapshot = new FieldOperationSnapshot(
                record.CreateSourceString(),
                record.CreateTitleString(),
                record.CreateSummaryString(),
                record.CreateSeverityString());
            return true;
        }

        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            data.fieldOperations.EnsureCapacity();
            data.fieldOperations.recentCount = Mathf.Min(_recentCount, FieldOperationLogDTO.MaxRecentEntries);

            for (int i = 0; i < data.fieldOperations.recentCount; i++)
            {
                FieldOperationRecordSlot record = _recent[i];
                data.fieldOperations.recentEntries[i] = new FieldOperationEntryDTO
                {
                    source = record.CreateSourceString(),
                    title = record.CreateTitleString(),
                    summary = record.CreateSummaryString(),
                    severity = record.CreateSeverityString()
                };
            }

            for (int i = data.fieldOperations.recentCount; i < FieldOperationLogDTO.MaxRecentEntries; i++)
                data.fieldOperations.recentEntries[i] = default;
        }

        public void LoadFromSaveData(SaveData data)
        {
            EnsureSlots();
            _recentCount = 0;

            if (data == null)
                return;

            FieldOperationLogDTO dto = data.fieldOperations;
            int count = Mathf.Clamp(dto.recentCount, 0, dto.recentEntries != null ? dto.recentEntries.Length : 0);
            for (int i = 0; i < count; i++)
            {
                FieldOperationEntryDTO entry = dto.recentEntries[i];
                if (string.IsNullOrWhiteSpace(entry.title))
                    continue;

                if (_recentCount >= FieldOperationLogDTO.MaxRecentEntries)
                    break;

                _recent[_recentCount++].Write(
                    AsSpanOrEmpty(entry.source),
                    AsSpanOrEmpty(entry.title),
                    AsSpanOrEmpty(entry.summary),
                    AsSpanOrEmpty(entry.severity));
            }

            LogChanged?.Invoke();
        }

        private void Push(
            ReadOnlySpan<char> source,
            ReadOnlySpan<char> title,
            ReadOnlySpan<char> summary,
            ReadOnlySpan<char> severity)
        {
            EnsureSlots();

            if (IsWhiteSpace(title))
                return;

            ReadOnlySpan<char> normalizedSummary = ResolveNormalizedSpan(summary, DefaultSummary.AsSpan());
            ReadOnlySpan<char> normalizedSeverity = ResolveSeveritySpan(severity);

            if (_recentCount > 0)
            {
                FieldOperationRecordSlot latest = _recent[0];
                if (latest.TitleSpan.SequenceEqual(title) &&
                    latest.SummarySpan.SequenceEqual(normalizedSummary) &&
                    latest.SeveritySpan.SequenceEqual(normalizedSeverity))
                {
                    return;
                }
            }

            int capacity = Mathf.Clamp(maxRecentEntries, 1, FieldOperationLogDTO.MaxRecentEntries);
            int insertIndex = Mathf.Min(_recentCount, capacity - 1);
            FieldOperationRecordSlot target = _recent[insertIndex];
            for (int i = insertIndex; i > 0; i--)
                _recent[i] = _recent[i - 1];

            _recent[0] = target;
            _recent[0].Write(source, title, normalizedSummary, normalizedSeverity);
            if (_recentCount < capacity)
                _recentCount++;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (verboseLogging)
                Hecton8.Core.H8Debug.Log(VerboseOperationRecordedMessage);
#endif

            LogChanged?.Invoke();
        }

        private void EnsureSlots()
        {
            for (int i = 0; i < _recent.Length; i++)
            {
                if (_recent[i] == null)
                    _recent[i] = new FieldOperationRecordSlot();
            }
        }

        private static ReadOnlySpan<char> AsSpanOrEmpty(string value)
        {
            return string.IsNullOrEmpty(value) ? ReadOnlySpan<char>.Empty : value.AsSpan();
        }

        private static ReadOnlySpan<char> ResolveNormalizedSpan(ReadOnlySpan<char> value, ReadOnlySpan<char> fallback)
        {
            return IsWhiteSpace(value) ? fallback : value;
        }

        private static ReadOnlySpan<char> ResolveSeveritySpan(ReadOnlySpan<char> severity)
        {
            if (IsWhiteSpace(severity))
                return DefaultSeverity.AsSpan();
            if (EqualsTrimmedAsciiIgnoreCase(severity, "CRITICAL"))
                return "CRITICAL".AsSpan();
            if (EqualsTrimmedAsciiIgnoreCase(severity, "WARN") || EqualsTrimmedAsciiIgnoreCase(severity, "WARNING"))
                return "WARN".AsSpan();
            if (EqualsTrimmedAsciiIgnoreCase(severity, "INFO"))
                return DefaultSeverity.AsSpan();

            return DefaultSeverity.AsSpan();
        }

        private static int CopyNormalized(ReadOnlySpan<char> value, ReadOnlySpan<char> fallback, char[] destination)
        {
            return CopyTruncated(ResolveNormalizedSpan(value, fallback), destination);
        }

        private static int CopyNormalizedSeverity(ReadOnlySpan<char> severity, char[] destination)
        {
            return CopyTruncated(ResolveSeveritySpan(severity), destination);
        }

        private static int CopyTruncated(ReadOnlySpan<char> value, char[] destination)
        {
            if (destination == null || destination.Length == 0)
                return 0;

            int length = Mathf.Min(value.Length, destination.Length);
            value.Slice(0, length).CopyTo(destination.AsSpan(0, length));
            return length;
        }

        private static int CopySpan(ReadOnlySpan<char> source, Span<char> destination)
        {
            if (destination.Length == 0 || source.Length == 0)
                return 0;

            int length = Mathf.Min(source.Length, destination.Length);
            source.Slice(0, length).CopyTo(destination);
            return length;
        }

        private static bool IsWhiteSpace(ReadOnlySpan<char> value)
        {
            if (value.Length == 0)
                return true;

            for (int i = 0; i < value.Length; i++)
            {
                if (!char.IsWhiteSpace(value[i]))
                    return false;
            }

            return true;
        }

        private static string CreatePersistentString(char[] buffer, int length)
        {
            return buffer == null || length <= 0 ? string.Empty : new string(buffer, 0, length);
        }

        private static bool EqualsTrimmedAsciiIgnoreCase(ReadOnlySpan<char> value, string token)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start <= end && char.IsWhiteSpace(value[start]))
                start++;
            while (end >= start && char.IsWhiteSpace(value[end]))
                end--;

            int length = end - start + 1;
            if (length != token.Length)
                return false;

            for (int i = 0; i < length; i++)
            {
                if (ToUpperAscii(value[start + i]) != token[i])
                    return false;
            }

            return true;
        }

        private static char ToUpperAscii(char value)
        {
            return value >= 'a' && value <= 'z'
                ? (char)(value - ('a' - 'A'))
                : value;
        }
    }
}
