using System;
using Hecton.Localization;
using Hecton8.Atmosphere;
using Hecton8.Audio;
using Hecton8.AI;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Physics;
using Hecton8.Visor;
using Hecton8.World;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    internal interface IAcousticEcholocationBarkListener
    {
        void OnStorageCapacityExceededBark();
    }

    internal static class AcousticEcholocationBarkEvents
    {
        private const int ListenerCapacity = 4;
        private static readonly IAcousticEcholocationBarkListener[] s_listeners = new IAcousticEcholocationBarkListener[ListenerCapacity]; // COLD ALLOC: IAcousticEcholocationBarkListener[4] - HUD bark listener registry - owner: AcousticEcholocationBarkEvents
        private static int s_listenerCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            for (int i = 0; i < ListenerCapacity; i++)
                s_listeners[i] = null;

            s_listenerCount = 0;
        }

        public static void Register(IAcousticEcholocationBarkListener listener)
        {
            if (listener == null)
                return;

            for (int i = 0; i < s_listenerCount; i++)
            {
                if (ReferenceEquals(s_listeners[i], listener))
                    return;
            }

            if (s_listenerCount >= ListenerCapacity)
                return;

            s_listeners[s_listenerCount++] = listener;
        }

        public static void Unregister(IAcousticEcholocationBarkListener listener)
        {
            if (listener == null)
                return;

            for (int i = 0; i < s_listenerCount; i++)
            {
                if (!ReferenceEquals(s_listeners[i], listener))
                    continue;

                int last = s_listenerCount - 1;
                s_listeners[i] = s_listeners[last];
                s_listeners[last] = null;
                s_listenerCount = last;
                return;
            }
        }

        public static void RaiseStorageCapacityExceeded()
        {
            for (int i = 0; i < s_listenerCount; i++)
            {
                IAcousticEcholocationBarkListener listener = s_listeners[i];
                if (listener != null)
                    listener.OnStorageCapacityExceededBark();
            }
        }
    }

    internal static class UiChildSpanUtility
    {
        private static readonly Transform[] s_childSnapshotBuffer = new Transform[128]; // COLD ALLOC: Transform[128] — shared UI child snapshot buffer — owner: UiChildSpanUtility

        public static RectTransform FindExistingChild(Transform parent, string childName)
        {
            ReadOnlySpan<Transform> children = SnapshotChildren(parent);
            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child != null && child.name == childName)
                    return child as RectTransform;
            }

            return null;
        }

        public static void DestroyChildren(Transform parent)
        {
            ReadOnlySpan<Transform> children = SnapshotChildren(parent);
            for (int i = children.Length - 1; i >= 0; i--)
            {
                Transform child = children[i];
                if (child == null)
                    continue;

                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(child.gameObject);
                else
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }

        private static ReadOnlySpan<Transform> SnapshotChildren(Transform parent)
        {
            if (parent == null)
                return ReadOnlySpan<Transform>.Empty;

            int childCount = Mathf.Min(parent.childCount, s_childSnapshotBuffer.Length);
            for (int i = 0; i < childCount; i++)
                s_childSnapshotBuffer[i] = parent.GetChild(i);

            return new ReadOnlySpan<Transform>(s_childSnapshotBuffer, 0, childCount);
        }
    }

    /// <summary>
    /// Player-owned diegetic sonar translator that converts active sonar contacts into terse PDA classification overlays.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Acoustic Echolocation Translator")]
    public sealed class AcousticEcholocationTranslator : MonoBehaviour, ITickable, IUpdatable, ISonarPingEventListener, ISonarSnapshotEventListener, ILocalizationLanguageChangedListener, IAcousticEcholocationBarkListener, IPhysicsAcousticImpulseEventListener
    {
        private enum ContactClassification : byte
        {
            None = 0,
            Leviathan = 1,
            Wreckage = 2
        }

        private const float OverlayWidth = 412f;
        private const float OverlayHeight = 92f;
        private const float VisibleDuration = 2.25f;
        private const float FadeDuration = 0.42f;
        private const float PulseDecaySharpness = 3.6f;
        private const float AnchorClassificationRadius = 112f;
        private const int MaxBioformContacts = 24;
        private const int HeaderTextCapacity = 64;
        private const int ClassificationTextCapacity = 192;
        private const string OverlayName = "AcousticEcholocationTranslatorOverlay";
        private const string DefaultContactHeader = "[SONAR CONTACT]";
        private const string DefaultClassificationPrefix = "CLASSIFICATION";
        private const string DefaultLeviathanClass = "UNKNOWN BIOMASS // LEVIATHAN";
        private const string DefaultWreckageClass = "WRECKAGE // ANCHOR RETURN";
        private const string DefaultSoundWaveHeader = "[ACOUSTIC WAVE]";
        private const string DefaultVisualSoundWaveText = "VISUAL SOUND WAVE // LEVIATHAN ROAR";
        private const string StorageCapacityHeader = "[FABRICATOR]";
        private const string StorageCapacityExceededText = "STORAGE CAPACITY EXCEEDED";
        private const float HeavyFogAttenuationDistanceMeters = 18f;
        private const float HeavyFogDensityThreshold = 0.035f;
        private const float MinimumVisualSoundWaveVolume01 = 0.12f;
        private static readonly int ContactHeaderKeyHash = LocHash.Compute(LocalizationKeys.SONAR_CONTACT_HEADER);
        private static readonly int ClassificationPrefixKeyHash = LocHash.Compute(LocalizationKeys.SONAR_CLASSIFICATION_PREFIX);
        private static readonly int LeviathanClassKeyHash = LocHash.Compute(LocalizationKeys.SONAR_CLASS_LEVIATHAN);
        private static readonly int WreckageClassKeyHash = LocHash.Compute(LocalizationKeys.SONAR_CLASS_WRECKAGE);

        private static readonly Color FrameColor = new Color(0.08f, 0.14f, 0.16f, 0.78f);
        private static readonly Color HeaderColor = new Color(0.72f, 0.96f, 0.88f, 0.96f);
        private static readonly Color ValueColor = new Color(0.86f, 0.98f, 0.92f, 0.96f);
        private static readonly Color AccentColor = new Color(0.38f, 0.92f, 0.88f, 0.18f);
        private static readonly Color StorageBarkFrameColor = new Color(0.34f, 0.02f, 0.015f, 0.9f);
        private static readonly Color StorageBarkHeaderColor = new Color(1f, 0.72f, 0.62f, 1f);
        private static readonly Color StorageBarkValueColor = new Color(1f, 0.08f, 0.04f, 1f);

        // COLD ALLOC: SpatialQueryHit[24] — active-sonar leviathan classification buffer — owner: AcousticEcholocationTranslator
        private readonly SpatialQueryHit[] _bioformContacts = new SpatialQueryHit[MaxBioformContacts];
        private readonly char[] _headerTextBuffer = new char[HeaderTextCapacity]; // COLD ALLOC: char[64] - sonar header TMP buffer - owner: AcousticEcholocationTranslator
        private readonly char[] _classificationTextBuffer = new char[ClassificationTextCapacity]; // COLD ALLOC: char[192] - sonar classification TMP buffer - owner: AcousticEcholocationTranslator
        private readonly char[] _classificationStressTextBuffer = new char[ClassificationTextCapacity]; // COLD ALLOC: char[192] - corrupted sonar classification TMP buffer - owner: AcousticEcholocationTranslator

        [Header("── Font ──────────────────")]
        [Tooltip("Optional readable font override for the acoustic translator overlay.")]
        [SerializeField] private TMP_FontAsset labelFont;
        [Tooltip("Optional numeric font override for distance readouts.")]
        [SerializeField] private TMP_FontAsset numericFont;

        private bool _uiBuilt;
        private bool _tickRegistered;
        private bool _pendingPing;
        private float _visibleTimer;
        private float _fadeTimer;
        private float _pulse01;
        private Canvas _targetCanvas;
        private HectonMapMagicVegetationBridge _vegetationBridge;
        private RectTransform _root;
        private CanvasGroup _group;
        private Image _background;
        private TextMeshProUGUI _headerLabel;
        private TextMeshProUGUI _classificationLabel;
        private bool _headerDirty = true;
        private bool _plainClassificationDirty = true;
        private bool _storageCapacityBarkActive;
        private ContactClassification _lastRenderedClassification = ContactClassification.None;
        private int _lastRenderedDistanceMeters = int.MinValue;

        private void OnEnable()
        {
            labelFont = LocalizedFontResolver.ResolveReadableFont(labelFont);
            numericFont = LocalizedFontResolver.ResolveNumericFont(numericFont, labelFont);
            ResolveAcousticOwners();
            EnsureUiBuilt();
            RefreshLocalizedCache();
            LocalizationEvents.RegisterLanguageListener(this);
            SpectrumEvents.RegisterSonarPingListener(this);
            SpectrumEvents.RegisterSonarSnapshotListener(this);
            AcousticEcholocationBarkEvents.Register(this);
            PhysicsEventBus.Register(this);
        }

        private void OnDisable()
        {
            LocalizationEvents.UnregisterLanguageListener(this);
            SpectrumEvents.UnregisterSonarPingListener(this);
            SpectrumEvents.UnregisterSonarSnapshotListener(this);
            AcousticEcholocationBarkEvents.Unregister(this);
            PhysicsEventBus.Unregister(this);
            UnregisterFromTickManager();
            ApplyRootAlpha(0f);
        }

        private void OnDestroy()
        {
            LocalizationEvents.UnregisterLanguageListener(this);
            SpectrumEvents.UnregisterSonarPingListener(this);
            SpectrumEvents.UnregisterSonarSnapshotListener(this);
            AcousticEcholocationBarkEvents.Unregister(this);
            PhysicsEventBus.Unregister(this);
            UnregisterFromTickManager();
        }

        /// <inheritdoc />
        public void Tick(float dt)
        {
            if (_group == null)
            {
                UnregisterFromTickManager();
                return;
            }

            if (_pulse01 > 0f)
                _pulse01 = Mathf.Max(0f, _pulse01 - (dt * PulseDecaySharpness));

            if (_visibleTimer > 0f)
            {
                _visibleTimer -= dt;
                ApplyVisualState(1f);
                return;
            }

            if (_fadeTimer > 0f)
            {
                _fadeTimer = Mathf.Max(0f, _fadeTimer - dt);
                float alpha = FadeDuration > 0.0001f
                    ? Mathf.Clamp01(_fadeTimer / FadeDuration)
                    : 0f;
                ApplyVisualState(alpha);
                if (_fadeTimer > 0f)
                    return;
            }

            ApplyRootAlpha(0f);
            _headerDirty = true;
            _plainClassificationDirty = true;
            _storageCapacityBarkActive = false;
            _lastRenderedClassification = ContactClassification.None;
            _lastRenderedDistanceMeters = int.MinValue;
            UnregisterFromTickManager();
        }

        private void HandleSonarPingSent(float intensity)
        {
            _pendingPing = true;
            _pulse01 = Mathf.Max(_pulse01, Mathf.Clamp01(intensity));
        }

        private void HandleSonarSnapshotUpdated(SpatialSonarSnapshot snapshot)
        {
            if (!_pendingPing)
                return;

            _pendingPing = false;
            ResolveAcousticOwners();
            EnsureUiBuilt();
            if (_classificationLabel == null || _headerLabel == null)
                return;

            if (!TryResolveContact(snapshot, out ContactClassification classification, out int distanceMeters))
                return;

            ShowClassification(classification, distanceMeters);
        }

        void ISonarPingEventListener.OnSonarPingSent(float intensity)
        {
            HandleSonarPingSent(intensity);
        }

        void ISonarSnapshotEventListener.OnSonarSnapshotUpdated(in SpatialSonarSnapshot snapshot)
        {
            HandleSonarSnapshotUpdated(snapshot);
        }

        void IPhysicsAcousticImpulseEventListener.OnAcousticImpulse(in AcousticImpulseEvent impulseEvent)
        {
            HandleAcousticImpulse(in impulseEvent);
        }

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            HandleLanguageChanged((GameLanguage)payload.Language);

        }


        private void HandleLanguageChanged(GameLanguage _)
        {
            RefreshLocalizedCache();
            _headerDirty = true;
            _plainClassificationDirty = true;
            _lastRenderedClassification = ContactClassification.None;
            _lastRenderedDistanceMeters = int.MinValue;
        }

        void IAcousticEcholocationBarkListener.OnStorageCapacityExceededBark()
        {
            ResolveAcousticOwners();
            EnsureUiBuilt();
            if (_classificationLabel == null || _headerLabel == null)
                return;

            int headerLength = CopySpanToBuffer(StorageCapacityHeader.AsSpan(), _headerTextBuffer);
            _headerLabel.SetCharArray(_headerTextBuffer, 0, headerLength);
            if (CharBufferPool.TryAcquire(out CharBufferPool.Lease lease))
            {
                try
                {
                    int messageLength = CopySpanToBuffer(StorageCapacityExceededText.AsSpan(), lease.Buffer);
                    _classificationLabel.SetCharArray(lease.Buffer, 0, messageLength);
                }
                finally
                {
                    CharBufferPool.Release(in lease);
                }
            }
            else
            {
                int messageLength = CopySpanToBuffer(StorageCapacityExceededText.AsSpan(), _classificationTextBuffer);
                _classificationLabel.SetCharArray(_classificationTextBuffer, 0, messageLength);
            }

            _visibleTimer = VisibleDuration;
            _fadeTimer = FadeDuration;
            _pulse01 = 1f;
            _headerDirty = true;
            _plainClassificationDirty = true;
            _storageCapacityBarkActive = true;
            _lastRenderedClassification = ContactClassification.None;
            _lastRenderedDistanceMeters = int.MinValue;
            ApplyVisualState(1f);
            RegisterToTickManager();
        }

        private void HandleAcousticImpulse(in AcousticImpulseEvent impulseEvent)
        {
            if ((impulseEvent.Flags & AcousticImpulseFlags.Leviathan) == 0 ||
                impulseEvent.Volume01 < MinimumVisualSoundWaveVolume01 ||
                !ShouldRenderVisualSoundWave())
            {
                return;
            }

            ResolveAcousticOwners();
            EnsureUiBuilt();
            if (_classificationLabel == null || _headerLabel == null)
                return;

            int headerLength = CopySpanToBuffer(DefaultSoundWaveHeader.AsSpan(), _headerTextBuffer);
            _headerLabel.SetCharArray(_headerTextBuffer, 0, headerLength);
            int distanceMeters = ResolveRuntimeDistanceMeters(impulseEvent.RuntimePosition);
            int waveTextLength = WriteVisualSoundWaveText(distanceMeters, impulseEvent.Volume01, _classificationTextBuffer);
            _classificationLabel.SetCharArray(_classificationTextBuffer, 0, waveTextLength);

            _storageCapacityBarkActive = false;
            _visibleTimer = VisibleDuration;
            _fadeTimer = FadeDuration;
            _pulse01 = Mathf.Max(_pulse01, Mathf.Clamp01(impulseEvent.Volume01));
            _headerDirty = true;
            _plainClassificationDirty = true;
            _lastRenderedClassification = ContactClassification.None;
            _lastRenderedDistanceMeters = int.MinValue;
            ApplyVisualState(1f);
            RegisterToTickManager();
        }

        private bool TryResolveContact(SpatialSonarSnapshot snapshot, out ContactClassification classification, out int distanceMeters)
        {
            classification = ContactClassification.None;
            distanceMeters = 0;

            if (TryResolveNearestLeviathan(snapshot, out distanceMeters))
            {
                classification = ContactClassification.Leviathan;
                return true;
            }

            if (TryResolveNearestAbyssalAnchor(out distanceMeters))
            {
                classification = ContactClassification.Wreckage;
                return true;
            }

            return false;
        }

        private bool TryResolveNearestLeviathan(SpatialSonarSnapshot snapshot, out int distanceMeters)
        {
            distanceMeters = 0;
            if (!snapshot.HasNearestBioform)
                return false;

            float searchRadius = Mathf.Clamp(snapshot.NearestBioformDistanceMeters + 12f, 18f, 180f);
            Vector3 origin = ResolveClassificationOriginRuntimePosition();
            AbsoluteUniversePosition originAup = AbsoluteUniversePosition.FromRuntimePosition(origin);
            int contactCount = FaunaSpatialHashRegistry.CollectContactsNonAlloc(
                in originAup,
                searchRadius,
                SpatialTargetKind.Bioform,
                _bioformContacts);

            float nearestDistanceSqr = float.MaxValue;
            for (int i = 0; i < contactCount; i++)
            {
                FaunaBrain brain = _bioformContacts[i].Owner as FaunaBrain;
                if (brain == null || brain.IsDead)
                    continue;

                FaunaSpeciesProfile speciesProfile = brain.SpeciesProfile;
                if (speciesProfile == null || !speciesProfile.isLeviathan)
                    continue;

                float candidateDistanceSqr = _bioformContacts[i].DistanceSqr;
                if (candidateDistanceSqr >= nearestDistanceSqr)
                    continue;

                nearestDistanceSqr = candidateDistanceSqr;
            }

            if (nearestDistanceSqr == float.MaxValue)
                return false;

            distanceMeters = RoundAupDistanceMeters(nearestDistanceSqr);
            return true;
        }

        private bool TryResolveNearestAbyssalAnchor(out int distanceMeters)
        {
            distanceMeters = 0;
            if (_vegetationBridge == null ||
                !_vegetationBridge.TryGetActiveAbyssalAnchorPayload(out NativeArray<Vector3> anchors, out int count) ||
                !anchors.IsCreated ||
                count <= 0)
            {
                return false;
            }

            double maxDistanceSqr = (double)AnchorClassificationRadius * AnchorClassificationRadius;
            double nearestDistanceSqr = double.PositiveInfinity;
            AbsoluteUniversePosition originAup = AbsoluteUniversePosition.FromRuntimePosition(ResolveClassificationOriginRuntimePosition());
            int limit = Mathf.Min(count, anchors.Length);
            for (int i = 0; i < limit; i++)
            {
                AbsoluteUniversePosition anchorAup = AbsoluteUniversePosition.FromRuntimePosition(anchors[i]);
                double candidateDistanceSqr = AbsoluteUniversePosition.DistanceSq(in anchorAup, in originAup);
                if (candidateDistanceSqr > maxDistanceSqr || candidateDistanceSqr >= nearestDistanceSqr)
                    continue;

                nearestDistanceSqr = candidateDistanceSqr;
            }

            if (double.IsPositiveInfinity(nearestDistanceSqr))
                return false;

            distanceMeters = RoundAupDistanceMeters(nearestDistanceSqr);
            return true;
        }

        private Vector3 ResolveClassificationOriginRuntimePosition()
        {
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext != null && playerContext.PlayerTransform != null)
                return playerContext.PlayerTransform.position;

            return transform != null ? transform.position : Vector3.zero;
        }

        private static int RoundAupDistanceMeters(double distanceSqr)
        {
            double distanceMeters = Math.Sqrt(Math.Max(0d, distanceSqr));
            return distanceMeters >= int.MaxValue ? int.MaxValue : Mathf.RoundToInt((float)distanceMeters);
        }

        private void ShowClassification(ContactClassification classification, int distanceMeters)
        {
            _storageCapacityBarkActive = false;
            ReadOnlySpan<char> classText = classification == ContactClassification.Leviathan
                ? ResolveLocalizedSpan(LeviathanClassKeyHash, DefaultLeviathanClass.AsSpan())
                : ResolveLocalizedSpan(WreckageClassKeyHash, DefaultWreckageClass.AsSpan());

            LocalizationManager localization = GlobalRegistry.Localization;
            bool useStressMutation = ShouldUseStressMutation(localization);
            if (useStressMutation)
            {
                ApplyStressMutatedClassification(localization, classText, distanceMeters);
            }
            else
            {
                ApplyPlainClassification(classification, classText, distanceMeters);
            }

            _visibleTimer = VisibleDuration;
            _fadeTimer = FadeDuration;
            _pulse01 = Mathf.Max(_pulse01, 1f);
            ApplyVisualState(1f);
            RegisterToTickManager();
        }

        private void ApplyPlainClassification(ContactClassification classification, ReadOnlySpan<char> classText, int distanceMeters)
        {
            if (_headerDirty)
            {
                int headerLength = CopySpanToBuffer(ResolveLocalizedSpan(ContactHeaderKeyHash, DefaultContactHeader.AsSpan()), _headerTextBuffer);
                _headerLabel.SetCharArray(_headerTextBuffer, 0, headerLength);
                _headerDirty = false;
            }

            if (_lastRenderedClassification != classification ||
                _lastRenderedDistanceMeters != distanceMeters ||
                _plainClassificationDirty)
            {
                int classificationLength = WriteClassificationText(classText, distanceMeters, _classificationTextBuffer);
                _classificationLabel.SetCharArray(_classificationTextBuffer, 0, classificationLength);
                _plainClassificationDirty = false;
                _lastRenderedClassification = classification;
                _lastRenderedDistanceMeters = distanceMeters;
            }
        }

        private void ApplyStressMutatedClassification(LocalizationManager localization, ReadOnlySpan<char> classText, int distanceMeters)
        {
            if (localization.TryApplyHullStressCorruptionIfNeeded(
                    ResolveLocalizedSpan(ContactHeaderKeyHash, DefaultContactHeader.AsSpan()),
                    _headerTextBuffer,
                    out int headerLength))
            {
                _headerLabel.SetCharArray(_headerTextBuffer, 0, headerLength);
                _headerDirty = true;
            }

            int sourceLength = WriteClassificationText(classText, distanceMeters, _classificationTextBuffer);
            if (localization.TryApplyHullStressCorruptionIfNeeded(
                    new ReadOnlySpan<char>(_classificationTextBuffer, 0, sourceLength),
                    _classificationStressTextBuffer,
                    out int classificationLength))
            {
                _classificationLabel.SetCharArray(_classificationStressTextBuffer, 0, classificationLength);
                _plainClassificationDirty = true;
                _lastRenderedClassification = ContactClassification.None;
                _lastRenderedDistanceMeters = int.MinValue;
            }
        }

        private static bool ShouldUseStressMutation(LocalizationManager localization)
        {
            return localization != null &&
                   (localization.GetHullStressCorruptionIntensity() > 0f ||
                    localization.IsMadnessWhisperVisualActive());
        }

        private static bool ShouldRenderVisualSoundWave()
        {
            LocalizationManager localization = GlobalRegistry.Localization;
            if (ShouldUseStressMutation(localization))
                return true;

            HectonAtmosphereManager atmosphere = GlobalRegistry.Atmosphere;
            if (atmosphere == null)
                return false;

            return atmosphere.CurrentFogAttenuationDistance <= HeavyFogAttenuationDistanceMeters ||
                   atmosphere.CurrentFogDensity >= HeavyFogDensityThreshold;
        }

        private int ResolveRuntimeDistanceMeters(Vector3 runtimePosition)
        {
            Vector3 origin = transform != null ? transform.position : Vector3.zero;
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext != null && playerContext.PlayerTransform != null)
                origin = playerContext.PlayerTransform.position;

            AbsoluteUniversePosition originAup = AbsoluteUniversePosition.FromRuntimePosition(origin);
            AbsoluteUniversePosition targetAup = AbsoluteUniversePosition.FromRuntimePosition(runtimePosition);
            double distanceSq = AbsoluteUniversePosition.DistanceSq(in originAup, in targetAup);
            double distanceMeters = Math.Sqrt(Math.Max(0d, distanceSq));
            return distanceMeters >= int.MaxValue ? int.MaxValue : Mathf.RoundToInt((float)distanceMeters);
        }

        private static int WriteVisualSoundWaveText(int distanceMeters, float volume01, char[] buffer)
        {
            int cursor = AppendSpanToBuffer(DefaultVisualSoundWaveText.AsSpan(), buffer, 0);
            cursor = AppendCharToBuffer(' ', buffer, cursor);
            cursor = AppendCharToBuffer('/', buffer, cursor);
            cursor = AppendCharToBuffer('/', buffer, cursor);
            cursor = AppendCharToBuffer(' ', buffer, cursor);
            if (cursor < buffer.Length &&
                distanceMeters.TryFormat(new Span<char>(buffer, cursor, buffer.Length - cursor), out int distanceWritten))
            {
                cursor += distanceWritten;
            }

            cursor = AppendCharToBuffer('M', buffer, cursor);
            cursor = AppendCharToBuffer(' ', buffer, cursor);
            cursor = AppendCharToBuffer('/', buffer, cursor);
            cursor = AppendCharToBuffer('/', buffer, cursor);
            cursor = AppendCharToBuffer(' ', buffer, cursor);
            int intensityPercent = Mathf.RoundToInt(Mathf.Clamp01(volume01) * 100f);
            if (cursor < buffer.Length &&
                intensityPercent.TryFormat(new Span<char>(buffer, cursor, buffer.Length - cursor), out int intensityWritten))
            {
                cursor += intensityWritten;
            }

            return AppendCharToBuffer('%', buffer, cursor);
        }

        private int WriteClassificationText(ReadOnlySpan<char> classText, int distanceMeters, char[] buffer)
        {
            int cursor = AppendSpanToBuffer(ResolveLocalizedSpan(ClassificationPrefixKeyHash, DefaultClassificationPrefix.AsSpan()), buffer, 0);
            cursor = AppendCharToBuffer(':', buffer, cursor);
            cursor = AppendCharToBuffer(' ', buffer, cursor);
            cursor = AppendSpanToBuffer(classText, buffer, cursor);
            cursor = AppendCharToBuffer(' ', buffer, cursor);
            cursor = AppendCharToBuffer('/', buffer, cursor);
            cursor = AppendCharToBuffer('/', buffer, cursor);
            cursor = AppendCharToBuffer(' ', buffer, cursor);

            if (cursor < buffer.Length &&
                distanceMeters.TryFormat(new Span<char>(buffer, cursor, buffer.Length - cursor), out int written))
            {
                cursor += written;
            }

            return AppendCharToBuffer('M', buffer, cursor);
        }

        private static int CopySpanToBuffer(ReadOnlySpan<char> source, char[] buffer)
        {
            return AppendSpanToBuffer(source, buffer, 0);
        }

        private static int AppendSpanToBuffer(ReadOnlySpan<char> source, char[] buffer, int offset)
        {
            if (source.Length == 0 || offset >= buffer.Length)
                return offset;

            int available = buffer.Length - offset;
            int length = source.Length <= available ? source.Length : available;
            for (int i = 0; i < length; i++)
                buffer[offset + i] = source[i];

            return offset + length;
        }

        private static int AppendCharToBuffer(char value, char[] buffer, int offset)
        {
            if (offset >= buffer.Length)
                return offset;

            buffer[offset] = value;
            return offset + 1;
        }

        private void ApplyVisualState(float alpha)
        {
            ApplyRootAlpha(alpha);
            if (_background != null)
            {
                Color frameColor = _storageCapacityBarkActive ? StorageBarkFrameColor : FrameColor;
                _background.color = new Color(frameColor.r, frameColor.g, frameColor.b, Mathf.Lerp(0f, frameColor.a, alpha));
            }

            if (_headerLabel != null)
                _headerLabel.color = _storageCapacityBarkActive ? StorageBarkHeaderColor : HeaderColor;

            if (_classificationLabel != null)
            {
                Color baseValue = _storageCapacityBarkActive ? StorageBarkValueColor : ValueColor;
                Color pulseValue = _storageCapacityBarkActive ? StorageBarkHeaderColor : HeaderColor;
                _classificationLabel.color = Color.Lerp(baseValue, pulseValue, _pulse01 * 0.45f);
            }
        }

        private void ResolveAcousticOwners()
        {
            if (_vegetationBridge == null)
                _vegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;

            if (_targetCanvas == null)
                _targetCanvas = ResolveTargetCanvas();
        }

        private void RefreshLocalizedCache()
        {
            _headerDirty = true;
            _plainClassificationDirty = true;
        }

        private void EnsureUiBuilt()
        {
            if (_uiBuilt || _targetCanvas == null)
                return;

            RectTransform canvasRoot = HectonUIScaler.ResolveContentRoot(_targetCanvas);
            if (canvasRoot == null)
                return;

            _root = FindExistingChild(canvasRoot, OverlayName);
            if (_root == null)
            {
                // COLD ALLOC: GameObject[1] — sonar translator HUD panel host — owner: AcousticEcholocationTranslator
                GameObject rootObject = new GameObject(OverlayName, typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
                rootObject.layer = canvasRoot.gameObject.layer;
                _root = rootObject.GetComponent<RectTransform>();
                _root.SetParent(canvasRoot, false);
            }

            _root.anchorMin = new Vector2(1f, 1f);
            _root.anchorMax = new Vector2(1f, 1f);
            _root.pivot = new Vector2(1f, 1f);
            _root.anchoredPosition = new Vector2(-42f, -86f);
            _root.sizeDelta = new Vector2(OverlayWidth, OverlayHeight);
            _root.localScale = Vector3.one;
            _root.SetAsLastSibling();

            _group = _root.GetComponent<CanvasGroup>();
            if (_group == null)
                _group = _root.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;

            _background = _root.GetComponent<Image>();
            _background.color = FrameColor;
            _background.raycastTarget = false;

            ClearChildren(_root);
            CreateRule(new Vector2(18f, -18f), new Vector2(-18f, -18f));
            CreateRule(new Vector2(18f, -74f), new Vector2(-18f, -74f));

            _headerLabel = CreateText("Header", labelFont, 12f, FontStyles.Bold, HeaderColor, TextAlignmentOptions.Left);
            Anchor(_headerLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -12f), new Vector2(-20f, -34f));

            _classificationLabel = CreateText("Classification", numericFont, 13f, FontStyles.Bold, ValueColor, TextAlignmentOptions.Left);
            Anchor(_classificationLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -34f), new Vector2(-20f, -72f));

            _uiBuilt = true;
        }

        private void RegisterToTickManager()
        {
            if (_tickRegistered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _tickRegistered = GlobalRegistry.Updatables.Contains(this);
        }

        private void UnregisterFromTickManager()
        {
            if (!_tickRegistered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _tickRegistered = false;
        }

        private void ApplyRootAlpha(float alpha)
        {
            if (_group != null && !Mathf.Approximately(_group.alpha, alpha))
                _group.alpha = alpha;
        }

        private static Canvas ResolveTargetCanvas()
        {
            SuitHUDV4CanvasOverlay overlay = SuitHUDV4CanvasOverlay.ActiveRuntimeInstance;
            if (overlay != null && overlay.TargetCanvas != null)
                return overlay.TargetCanvas;

            return (SuitHUDV4CanvasOverlay.ActiveRuntimeInstance != null ? SuitHUDV4CanvasOverlay.ActiveRuntimeInstance.GetComponent<Canvas>() : null);
        }

        private static ReadOnlySpan<char> ResolveLocalizedSpan(int keyHash, ReadOnlySpan<char> fallback)
        {
            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            return manager != null ? manager.GetRawSpanOrFallback(keyHash, fallback) : fallback;
        }

        private void CreateRule(Vector2 leftOffset, Vector2 rightOffset)
        {
            RectTransform rule = CreateRect(_root, "Rule");
            Image image = rule.gameObject.AddComponent<Image>();
            image.color = AccentColor;
            image.raycastTarget = false;
            Anchor(rule, new Vector2(0f, 1f), new Vector2(1f, 1f), leftOffset, rightOffset);
            rule.sizeDelta = new Vector2(0f, 1f);
        }

        private TextMeshProUGUI CreateText(string name, TMP_FontAsset fontAsset, float size, FontStyles style, Color color, TextAlignmentOptions alignment)
        {
            RectTransform rect = CreateRect(_root, name);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = fontAsset;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Truncate;
            LocalizedTMPAutoSizer.Configure(text, size * 0.7f, size, TextOverflowModes.Truncate, TextWrappingModes.NoWrap);
            TMP_TextRegistry.EnsureRegistered(text);
            return text;
        }

        private static RectTransform FindExistingChild(Transform parent, string childName)
        {
            return UiChildSpanUtility.FindExistingChild(parent, childName);
        }

        private static void ClearChildren(Transform parent)
        {
            UiChildSpanUtility.DestroyChildren(parent);
        }

        private static RectTransform CreateRect(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            return rect;
        }

        private static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }

    /// <summary>
    /// Fast sonar-driver boot log shown on active sonar pings.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Terminal Boot Sequence")]
    public sealed class TerminalBootSequence : MonoBehaviour, ITickable, IUpdatable, ISonarPingEventListener
    {
        private enum SequenceState : byte
        {
            Hidden = 0,
            Typing = 1,
            Hold = 2,
            Fade = 3
        }

        private const float CharacterRevealRate = 210f;
        private const float HoldDuration = 0.22f;
        private const float FadeSharpness = 7.5f;
        private const float HiddenAlphaCutoff = 0.01f;
        private const float OverlayWidth = 436f;
        private const float OverlayHeight = 148f;
        private const int SequenceTextCapacity = 256;
        private const string OverlayName = "TerminalBootSequenceOverlay";
        private const string DefaultStatusOk = "[OK]";
        private const string DefaultStatusDegraded = "[DEGRADED]";
        private const string DefaultStatusFailed = "[FAILED]";

        [Header("── Font ──────────────────")]
        [Tooltip("Optional readable font override for the sonar terminal boot feed.")]
        [SerializeField] private TMP_FontAsset font;

        private RectTransform _overlayRoot;
        private CanvasGroup _overlayGroup;
        private TextMeshProUGUI _consoleLabel;
        private bool _uiBuilt;
        private bool _tickRegistered;
        private SequenceState _state;
        private float _stateTimer;
        private float _visibleCharacterProgress;
        private int _visibleCharacterTarget;
        private HectonSurvivalSystem _survivalSystem;
        private readonly char[] _sequenceTextBuffer = new char[SequenceTextCapacity]; // COLD ALLOC: char[192] - sonar boot sequence TMP buffer - owner: TerminalBootSequence

        private void OnEnable()
        {
            font = LocalizedFontResolver.ResolveReadableFont(font);
            ResolveTerminalOwners();
            EnsureUiBuilt();
            SpectrumEvents.RegisterSonarPingListener(this);
        }

        private void OnDisable()
        {
            SpectrumEvents.UnregisterSonarPingListener(this);
            UnregisterFromTickManager();
            HideOverlay();
        }

        private void OnDestroy()
        {
            SpectrumEvents.UnregisterSonarPingListener(this);
            UnregisterFromTickManager();
        }

        /// <inheritdoc />
        public void Tick(float dt)
        {
            if (_consoleLabel == null || _overlayGroup == null || _state == SequenceState.Hidden)
                return;

            switch (_state)
            {
                case SequenceState.Typing:
                    _visibleCharacterProgress += dt * CharacterRevealRate;
                    int visibleCharacters = Mathf.Min(_visibleCharacterTarget, Mathf.FloorToInt(_visibleCharacterProgress));
                    if (_consoleLabel.maxVisibleCharacters != visibleCharacters)
                        _consoleLabel.maxVisibleCharacters = visibleCharacters;

                    if (visibleCharacters >= _visibleCharacterTarget)
                    {
                        _state = SequenceState.Hold;
                        _stateTimer = HoldDuration;
                    }
                    break;

                case SequenceState.Hold:
                    _stateTimer -= dt;
                    if (_stateTimer <= 0f)
                        _state = SequenceState.Fade;
                    break;

                case SequenceState.Fade:
                    _overlayGroup.alpha = Mathf.Lerp(_overlayGroup.alpha, 0f, 1f - Mathf.Exp(-FadeSharpness * dt));
                    if (_overlayGroup.alpha <= HiddenAlphaCutoff)
                    {
                        HideOverlay();
                        UnregisterFromTickManager();
                    }
                    break;
            }
        }

        private void HandleSonarPingSent(float intensity)
        {
            if (intensity <= 0.001f)
                return;

            ResolveTerminalOwners();
            EnsureUiBuilt();
            if (_consoleLabel == null || _overlayGroup == null)
                return;

            int sequenceTextLength = BuildSequenceText(_sequenceTextBuffer);
            _consoleLabel.SetCharArray(_sequenceTextBuffer, 0, sequenceTextLength);
            _consoleLabel.ForceMeshUpdate();
            _visibleCharacterTarget = _consoleLabel.textInfo.characterCount;
            _visibleCharacterProgress = 0f;
            _consoleLabel.maxVisibleCharacters = 0;
            _overlayGroup.alpha = 1f;
            _overlayGroup.blocksRaycasts = false;
            _overlayGroup.interactable = false;
            _state = SequenceState.Typing;
            _stateTimer = 0f;
            RegisterToTickManager();
        }

        void ISonarPingEventListener.OnSonarPingSent(float intensity)
        {
            HandleSonarPingSent(intensity);
        }

        private void ResolveTerminalOwners()
        {
            if (_survivalSystem == null)
                TryGetComponent(out _survivalSystem);
        }

        private int BuildSequenceText(char[] buffer)
        {
            float integrity01 = _survivalSystem != null ? _survivalSystem.IntegrityNormalized : 0f;
            float energy01 = _survivalSystem != null ? _survivalSystem.EnergyNormalized : 0f;
            float hullStress01 = _survivalSystem != null ? Mathf.Clamp01(1f - integrity01) : 1f;
            string hullStatus = ResolveIntegrityStatus(integrity01);
            string powerStatus = energy01 >= 0.25f ? DefaultStatusOk : DefaultStatusDegraded;
            string linkStatus = hullStress01 <= 0.18f ? DefaultStatusOk : DefaultStatusDegraded;

            int cursor = 0;
            cursor = AppendString(buffer, cursor, DefaultStatusOk);
            cursor = AppendLine(buffer, cursor, " MOUNTING SONAR_DRIVER...");
            cursor = AppendString(buffer, cursor, DefaultStatusOk);
            cursor = AppendString(buffer, cursor, " AUP SECTOR 0x");
            cursor = AppendHex8(buffer, cursor, ResolveFakeAupSectorHash());
            cursor = AppendChar(buffer, cursor, '\n');
            cursor = AppendString(buffer, cursor, DefaultStatusOk);
            cursor = AppendLine(buffer, cursor, " LOADING NEURAL INTERFACE...");
            cursor = AppendString(buffer, cursor, DefaultStatusOk);
            cursor = AppendLine(buffer, cursor, " CALIBRATING LIDAR ARRAY...");
            cursor = AppendString(buffer, cursor, linkStatus);
            cursor = AppendString(buffer, cursor, " ACOUSTIC BUS LINK... HULL ");
            cursor = AppendInt(buffer, cursor, _survivalSystem != null ? Mathf.RoundToInt(integrity01 * 100f) : 0);
            cursor = AppendChar(buffer, cursor, '%');
            cursor = AppendChar(buffer, cursor, '\n');
            cursor = AppendString(buffer, cursor, powerStatus);
            cursor = AppendString(buffer, cursor, " POWER FEED... ");
            cursor = AppendInt(buffer, cursor, _survivalSystem != null ? Mathf.RoundToInt(energy01 * 100f) : 0);
            cursor = AppendChar(buffer, cursor, '%');
            cursor = AppendChar(buffer, cursor, '\n');
            cursor = AppendString(buffer, cursor, hullStatus);
            cursor = AppendString(buffer, cursor, " NOISE FILTER... STRESS ");
            cursor = AppendInt(buffer, cursor, _survivalSystem != null ? Mathf.RoundToInt(hullStress01 * 100f) : 100);
            return AppendChar(buffer, cursor, '%');
        }

        private static int AppendLine(char[] buffer, int cursor, string value)
        {
            cursor = AppendString(buffer, cursor, value);
            return AppendChar(buffer, cursor, '\n');
        }

        private static int AppendString(char[] buffer, int cursor, string value)
        {
            if (buffer == null || string.IsNullOrEmpty(value) || cursor >= buffer.Length)
                return cursor;

            int available = buffer.Length - cursor;
            int length = value.Length <= available ? value.Length : available;
            for (int i = 0; i < length; i++)
                buffer[cursor + i] = value[i];

            return cursor + length;
        }

        private static int AppendInt(char[] buffer, int cursor, int value)
        {
            if (buffer == null || cursor >= buffer.Length)
                return cursor;

            return value.TryFormat(new Span<char>(buffer, cursor, buffer.Length - cursor), out int written)
                ? cursor + written
                : cursor;
        }

        private static int AppendHex8(char[] buffer, int cursor, uint value)
        {
            if (buffer == null || cursor >= buffer.Length)
                return cursor;

            Span<char> destination = new Span<char>(buffer, cursor, buffer.Length - cursor);
            return value.TryFormat(destination, out int written, "X8")
                ? cursor + written
                : cursor;
        }

        private static uint ResolveFakeAupSectorHash()
        {
            uint hash = 2166136261u;
            hash = (hash ^ HectonFloatingOrigin.LastShiftEvent.Sequence) * 16777619u;
            hash = (hash ^ unchecked((uint)Time.frameCount)) * 16777619u;
            return hash ^ 0xA8F1D3C5u;
        }

        private static int AppendChar(char[] buffer, int cursor, char value)
        {
            if (buffer == null || cursor >= buffer.Length)
                return cursor;

            buffer[cursor] = value;
            return cursor + 1;
        }

        private static string ResolveIntegrityStatus(float integrity01)
        {
            if (integrity01 < 0.55f)
                return DefaultStatusFailed;

            if (integrity01 < 0.82f)
                return DefaultStatusDegraded;

            return DefaultStatusOk;
        }

        private void EnsureUiBuilt()
        {
            if (_uiBuilt)
                return;

            Canvas targetCanvas = ResolveTargetCanvas();
            if (targetCanvas == null)
                return;

            RectTransform contentRoot = HectonUIScaler.ResolveContentRoot(targetCanvas);
            if (contentRoot == null)
                return;

            _overlayRoot = FindExistingChild(contentRoot, OverlayName);
            if (_overlayRoot == null)
            {
                // COLD ALLOC: GameObject[1] — sonar terminal boot overlay host — owner: TerminalBootSequence
                GameObject overlayObject = new GameObject(
                    OverlayName,
                    typeof(RectTransform),
                    typeof(CanvasGroup),
                    typeof(Image));
                overlayObject.layer = contentRoot.gameObject.layer;
                _overlayRoot = overlayObject.GetComponent<RectTransform>();
                _overlayRoot.SetParent(contentRoot, false);
            }

            _overlayRoot.anchorMin = new Vector2(0f, 1f);
            _overlayRoot.anchorMax = new Vector2(0f, 1f);
            _overlayRoot.pivot = new Vector2(0f, 1f);
            _overlayRoot.anchoredPosition = new Vector2(34f, -188f);
            _overlayRoot.sizeDelta = new Vector2(OverlayWidth, OverlayHeight);
            _overlayRoot.localScale = Vector3.one;
            _overlayRoot.SetAsLastSibling();

            _overlayGroup = _overlayRoot.GetComponent<CanvasGroup>();
            if (_overlayGroup == null)
                _overlayGroup = _overlayRoot.gameObject.AddComponent<CanvasGroup>();
            _overlayGroup.alpha = 0f;
            _overlayGroup.blocksRaycasts = false;
            _overlayGroup.interactable = false;

            Image background = _overlayRoot.GetComponent<Image>();
            if (background == null)
                background = _overlayRoot.gameObject.AddComponent<Image>();
            background.color = new Color(0.02f, 0.07f, 0.08f, 0.72f);
            background.raycastTarget = false;

            ClearChildren(_overlayRoot);

            GameObject textObject = new GameObject("ConsoleText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.layer = _overlayRoot.gameObject.layer;
            RectTransform textRoot = textObject.GetComponent<RectTransform>();
            textRoot.SetParent(_overlayRoot, false);
            textRoot.anchorMin = Vector2.zero;
            textRoot.anchorMax = Vector2.one;
            textRoot.offsetMin = new Vector2(16f, 12f);
            textRoot.offsetMax = new Vector2(-16f, -12f);

            _consoleLabel = textObject.GetComponent<TextMeshProUGUI>();
            if (font != null)
                _consoleLabel.font = font;

            _consoleLabel.fontSize = 16f;
            _consoleLabel.color = new Color(0.78f, 0.96f, 0.88f, 1f);
            _consoleLabel.alignment = TextAlignmentOptions.TopLeft;
            _consoleLabel.textWrappingMode = TextWrappingModes.NoWrap;
            _consoleLabel.overflowMode = TextOverflowModes.Overflow;
            _consoleLabel.maxVisibleCharacters = int.MaxValue;

            _uiBuilt = true;
        }

        private void RegisterToTickManager()
        {
            if (_tickRegistered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _tickRegistered = GlobalRegistry.Updatables.Contains(this);
        }

        private void UnregisterFromTickManager()
        {
            if (!_tickRegistered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _tickRegistered = false;
        }

        private void HideOverlay()
        {
            _state = SequenceState.Hidden;
            _stateTimer = 0f;
            _visibleCharacterProgress = 0f;
            _visibleCharacterTarget = 0;

            if (_overlayGroup != null)
            {
                _overlayGroup.alpha = 0f;
                _overlayGroup.blocksRaycasts = false;
                _overlayGroup.interactable = false;
            }

            if (_consoleLabel != null)
                _consoleLabel.maxVisibleCharacters = int.MaxValue;
        }

        private static Canvas ResolveTargetCanvas()
        {
            SuitHUDV4CanvasOverlay overlay = SuitHUDV4CanvasOverlay.ActiveRuntimeInstance;
            if (overlay != null && overlay.TargetCanvas != null)
                return overlay.TargetCanvas;

            return (SuitHUDV4CanvasOverlay.ActiveRuntimeInstance != null ? SuitHUDV4CanvasOverlay.ActiveRuntimeInstance.GetComponent<Canvas>() : null);
        }

        private static RectTransform FindExistingChild(Transform parent, string childName)
        {
            return UiChildSpanUtility.FindExistingChild(parent, childName);
        }

        private static void ClearChildren(Transform parent)
        {
            UiChildSpanUtility.DestroyChildren(parent);
        }
    }

    /// <summary>
    /// Player-owned overlay that renders pooled spatial-audio captions around the HUD center.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Audio Caption Overlay")]
    public sealed class AudioCaptionOverlay : MonoBehaviour, ITickable, IUpdatable, IAudioCaptionEventListener
    {
        private const int SlotCount = 8;
        private const float DefaultDuration = 1.65f;
        private const float MinDuration = 0.35f;
        private const float RadiusMin = 112f;
        private const float RadiusMax = 188f;
        private const float VerticalBias = -14f;
        private const float BehindFlipBias = 0.18f;
        private const int CaptionTextCapacity = 128;
        private const uint CaptionHashSeed = 2166136261u;
        private const uint CaptionHashPrime = 16777619u;
        private const string OverlayName = "AudioCaptionOverlay";

        private static readonly Color CaptionColor = new Color(0.86f, 0.97f, 0.92f, 0.94f);
        private static readonly Color CaptionShadowColor = new Color(0.06f, 0.11f, 0.12f, 0.84f);
        private static readonly Vector2 CaptionSize = new Vector2(240f, 44f);

        private struct CaptionSlot
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public TextMeshProUGUI Label;
            public bool Active;
            public float Age;
            public float Duration;
            public float Intensity;
            public Vector3 WorldPosition;
            public char[] TextBuffer;
            public int TextLength;
            public uint TextHash;
        }

        [Header("── Font ──────────────────")]
        [Tooltip("Readable font override for spatial audio captions.")]
        [SerializeField] private TMP_FontAsset labelFont;

        private Canvas _targetCanvas;
        private Camera _viewCamera;
        private RectTransform _overlayRoot;
        private bool _tickRegistered;
        private bool _uiBuilt;
        // COLD ALLOC: CaptionSlot[8] — pooled spatial audio caption slots — owner: AudioCaptionOverlay
        private readonly CaptionSlot[] _slots = new CaptionSlot[SlotCount];

        private void OnEnable()
        {
            labelFont = LocalizedFontResolver.ResolveReadableFont(labelFont);
            EnsureUiBuilt();
            AudioCaptionEvents.Register(this);
            RegisterToTickManager();
        }

        private void OnDisable()
        {
            AudioCaptionEvents.Unregister(this);
            UnregisterFromTickManager();
            HideAllSlots();
        }

        private void OnDestroy()
        {
            AudioCaptionEvents.Unregister(this);
            UnregisterFromTickManager();
        }

        public void Tick(float dt)
        {
            if (!_uiBuilt)
            {
                EnsureUiBuilt();
                if (!_uiBuilt)
                    return;
            }

            if (_viewCamera == null)
                ResolveViewCamera();

            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_slots[i].Active)
                    continue;

                CaptionSlot slot = _slots[i];
                slot.Age += dt;
                if (slot.Age >= slot.Duration)
                {
                    slot.Active = false;
                    ApplySlotHidden(ref slot);
                    _slots[i] = slot;
                    continue;
                }

                UpdateSlotPose(ref slot);
                _slots[i] = slot;
            }
        }

        /// <summary>
        /// Receives deferred spatial-audio caption requests.
        /// </summary>
        public void OnAudioCaptionRequested(AudioCaptionRequest request)
        {
            HandleCaptionRequested(request);
        }

        private void HandleCaptionRequested(AudioCaptionRequest request)
        {
            EnsureUiBuilt();
            if (!_uiBuilt)
                return;

            int slotIndex = AcquireSlotIndex();
            ref CaptionSlot slot = ref _slots[slotIndex];
            slot.Active = true;
            slot.Age = 0f;
            slot.Duration = Mathf.Max(MinDuration, request.DurationSeconds > 0f ? request.DurationSeconds : DefaultDuration);
            slot.Intensity = Mathf.Clamp01(request.Intensity);
            slot.WorldPosition = request.WorldPosition;
            string captionText = request.CaptionText;
            if (slot.Label != null &&
                slot.TextBuffer != null &&
                !string.IsNullOrWhiteSpace(captionText) &&
                !SlotTextMatches(ref slot, captionText, out int displayLength, out uint displayHash))
            {
                WriteCaptionToBuffer(captionText, slot.TextBuffer, displayLength);
                slot.Label.SetCharArray(slot.TextBuffer, 0, displayLength);
                slot.TextLength = displayLength;
                slot.TextHash = displayHash;
            }

            slot.Group.alpha = 1f;
            slot.Group.blocksRaycasts = false;
            slot.Group.interactable = false;
            UpdateSlotPose(ref slot);
        }

        private void EnsureUiBuilt()
        {
            if (_uiBuilt)
                return;

            _targetCanvas = ResolveTargetCanvas();
            if (_targetCanvas == null)
                return;

            RectTransform contentRoot = HectonUIScaler.ResolveContentRoot(_targetCanvas);
            if (contentRoot == null)
                return;

            _overlayRoot = FindExistingChild(contentRoot, OverlayName);
            if (_overlayRoot == null)
            {
                // COLD ALLOC: GameObject[1] — spatial audio caption host — owner: AudioCaptionOverlay
                GameObject overlayObject = new GameObject(OverlayName, typeof(RectTransform));
                overlayObject.layer = contentRoot.gameObject.layer;
                _overlayRoot = overlayObject.GetComponent<RectTransform>();
                _overlayRoot.SetParent(contentRoot, false);
            }

            _overlayRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _overlayRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _overlayRoot.pivot = new Vector2(0.5f, 0.5f);
            _overlayRoot.anchoredPosition = Vector2.zero;
            _overlayRoot.sizeDelta = Vector2.zero;
            _overlayRoot.localScale = Vector3.one;
            _overlayRoot.SetAsLastSibling();

            ClearChildren(_overlayRoot);

            for (int i = 0; i < _slots.Length; i++)
                BuildSlot(i);

            ResolveViewCamera();
            _uiBuilt = true;
        }

        private void BuildSlot(int index)
        {
            // COLD ALLOC: GameObject[1] — pooled caption slot root — owner: AudioCaptionOverlay
            GameObject slotObject = new GameObject(
                "CaptionSlot_" + index,
                typeof(RectTransform),
                typeof(CanvasGroup));
            slotObject.layer = _overlayRoot.gameObject.layer;
            RectTransform slotRoot = slotObject.GetComponent<RectTransform>();
            slotRoot.SetParent(_overlayRoot, false);
            slotRoot.anchorMin = new Vector2(0.5f, 0.5f);
            slotRoot.anchorMax = new Vector2(0.5f, 0.5f);
            slotRoot.pivot = new Vector2(0.5f, 0.5f);
            slotRoot.sizeDelta = CaptionSize;
            slotRoot.anchoredPosition = new Vector2(0f, VerticalBias);

            CanvasGroup group = slotObject.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            // COLD ALLOC: GameObject[1] — pooled caption text owner — owner: AudioCaptionOverlay
            GameObject textObject = new GameObject("Text", typeof(RectTransform));
            textObject.layer = slotObject.layer;
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.SetParent(slotRoot, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.font = labelFont != null ? labelFont : TMP_Settings.defaultFontAsset;
            text.fontSize = 13f;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.alignment = TextAlignmentOptions.Center;
            text.color = CaptionColor;
            text.outlineColor = CaptionShadowColor;
            text.outlineWidth = 0.18f;
            text.raycastTarget = false;

            char[] textBuffer = new char[CaptionTextCapacity]; // COLD ALLOC: char[128] - pooled spatial caption TMP buffer - owner: AudioCaptionOverlay
            text.SetCharArray(textBuffer, 0, 0);

            _slots[index] = new CaptionSlot
            {
                Root = slotRoot,
                Group = group,
                Label = text,
                Active = false,
                Age = 0f,
                Duration = DefaultDuration,
                Intensity = 0f,
                WorldPosition = Vector3.zero,
                TextBuffer = textBuffer,
                TextLength = 0,
                TextHash = CaptionHashSeed
            };
        }

        private void ResolveViewCamera()
        {
            if (_targetCanvas != null && _targetCanvas.worldCamera != null)
            {
                _viewCamera = _targetCanvas.worldCamera;
                return;
            }

            SuitHUDV4CanvasOverlay overlay = SuitHUDV4CanvasOverlay.ActiveRuntimeInstance;
            if (overlay != null && overlay.TargetCanvas != null && overlay.TargetCanvas.worldCamera != null)
            {
                _viewCamera = overlay.TargetCanvas.worldCamera;
                return;
            }

            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) && playerTransform != null)
                _viewCamera = ((Hecton8.Core.GlobalRegistry.Player != null && Hecton8.Core.GlobalRegistry.Player.PlayerCamera != null) ? Hecton8.Core.GlobalRegistry.Player.PlayerCamera : playerTransform.GetComponent<Camera>());
        }

        private int AcquireSlotIndex()
        {
            int oldestIndex = 0;
            float oldestAge = float.MinValue;

            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_slots[i].Active)
                    return i;

                if (_slots[i].Age > oldestAge)
                {
                    oldestAge = _slots[i].Age;
                    oldestIndex = i;
                }
            }

            return oldestIndex;
        }

        private void UpdateSlotPose(ref CaptionSlot slot)
        {
            if (slot.Root == null || slot.Group == null)
                return;

            Vector2 direction = ResolveScreenDirection(slot.WorldPosition);
            float radius = Mathf.Lerp(RadiusMin, RadiusMax, slot.Intensity);
            slot.Root.anchoredPosition = direction * radius + new Vector2(0f, VerticalBias);

            float remaining01 = 1f - Mathf.Clamp01(slot.Age / Mathf.Max(MinDuration, slot.Duration));
            slot.Group.alpha = Mathf.Sin(remaining01 * Mathf.PI * 0.5f);
        }

        private Vector2 ResolveScreenDirection(Vector3 worldPosition)
        {
            if (_viewCamera == null)
                return Vector2.up;

            Transform cameraTransform = _viewCamera.transform;
            Vector3 local = cameraTransform.InverseTransformPoint(worldPosition);
            Vector2 planar = new Vector2(local.x, local.y);
            if (planar.sqrMagnitude < 0.0001f)
                planar = new Vector2(local.x >= 0f ? BehindFlipBias : -BehindFlipBias, 1f);

            if (local.z < 0f)
                planar = -planar;

            float magnitude = planar.magnitude;
            if (magnitude <= 0.0001f)
                return Vector2.up;

            return planar / magnitude;
        }

        private void HideAllSlots()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_slots[i].Active)
                    continue;

                CaptionSlot slot = _slots[i];
                slot.Active = false;
                ApplySlotHidden(ref slot);
                _slots[i] = slot;
            }
        }

        private static void ApplySlotHidden(ref CaptionSlot slot)
        {
            if (slot.Group != null)
            {
                slot.Group.alpha = 0f;
                slot.Group.blocksRaycasts = false;
                slot.Group.interactable = false;
            }
        }

        private static bool SlotTextMatches(ref CaptionSlot slot, string captionText, out int displayLength, out uint displayHash)
        {
            displayLength = ResolveCaptionDisplayLength(captionText, slot.TextBuffer);
            bool truncated = IsCaptionTruncated(captionText, displayLength, slot.TextBuffer);
            displayHash = ComputeCaptionDisplayHash(captionText, displayLength, truncated);

            if (slot.TextLength != displayLength || slot.TextHash != displayHash)
                return false;

            for (int i = 0; i < displayLength; i++)
            {
                if (slot.TextBuffer[i] != ResolveCaptionDisplayChar(captionText, i, displayLength, truncated))
                    return false;
            }

            return true;
        }

        private static int ResolveCaptionDisplayLength(string captionText, char[] destination)
        {
            if (string.IsNullOrEmpty(captionText) || destination == null || destination.Length == 0)
                return 0;

            return Mathf.Min(captionText.Length, destination.Length);
        }

        private static bool IsCaptionTruncated(string captionText, int displayLength, char[] destination)
        {
            return captionText != null &&
                   destination != null &&
                   captionText.Length > destination.Length &&
                   displayLength >= 3;
        }

        private static void WriteCaptionToBuffer(string captionText, char[] destination, int displayLength)
        {
            bool truncated = IsCaptionTruncated(captionText, displayLength, destination);
            for (int i = 0; i < displayLength; i++)
                destination[i] = ResolveCaptionDisplayChar(captionText, i, displayLength, truncated);
        }

        private static uint ComputeCaptionDisplayHash(string captionText, int displayLength, bool truncated)
        {
            uint hash = CaptionHashSeed;
            for (int i = 0; i < displayLength; i++)
            {
                hash ^= ResolveCaptionDisplayChar(captionText, i, displayLength, truncated);
                hash *= CaptionHashPrime;
            }

            return hash;
        }

        private static char ResolveCaptionDisplayChar(string captionText, int index, int displayLength, bool truncated)
        {
            if (truncated && index >= displayLength - 3)
                return '.';

            return captionText[index];
        }

        private void RegisterToTickManager()
        {
            if (_tickRegistered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _tickRegistered = GlobalRegistry.Updatables.Contains(this);
        }

        private void UnregisterFromTickManager()
        {
            if (!_tickRegistered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _tickRegistered = false;
        }

        private static Canvas ResolveTargetCanvas()
        {
            SuitHUDV4CanvasOverlay overlay = SuitHUDV4CanvasOverlay.ActiveRuntimeInstance;
            if (overlay != null && overlay.TargetCanvas != null)
                return overlay.TargetCanvas;

            return (SuitHUDV4CanvasOverlay.ActiveRuntimeInstance != null ? SuitHUDV4CanvasOverlay.ActiveRuntimeInstance.GetComponent<Canvas>() : null);
        }

        private static RectTransform FindExistingChild(Transform parent, string childName)
        {
            return UiChildSpanUtility.FindExistingChild(parent, childName);
        }

        private static void ClearChildren(Transform parent)
        {
            UiChildSpanUtility.DestroyChildren(parent);
        }
    }
}
