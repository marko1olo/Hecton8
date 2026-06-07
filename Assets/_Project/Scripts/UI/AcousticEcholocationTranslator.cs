using System;
using Hecton.Localization;
using Hecton8.Atmosphere;
using Hecton8.Audio;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay;
using Hecton8.Visor;
using Hecton8.World;
using TMPro;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    internal static class AcousticEcholocationBarkEvents
    {
        private const int PendingStorageBarkCapacity = 8;
        private static int s_pendingStorageCapacityExceeded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_pendingStorageCapacityExceeded = 0;
        }

        public static void RaiseStorageCapacityExceeded()
        {
            if (s_pendingStorageCapacityExceeded >= PendingStorageBarkCapacity)
                return;

            s_pendingStorageCapacityExceeded++;
        }

        public static bool ConsumeStorageCapacityExceeded()
        {
            if (s_pendingStorageCapacityExceeded <= 0)
                return false;

            s_pendingStorageCapacityExceeded--;
            return true;
        }
    }

    internal static class UiChildSpanUtility
    {
        private static readonly Transform[] s_childSnapshotBuffer = new Transform[128]; // COLD ALLOC: Transform[128] — shared UI child snapshot buffer — owner: UiChildSpanUtility

        public static RectTransform FindExistingChild(Transform parent, string childName)
        {
            return FindExistingChild(parent, childName, 0);
        }

        public static RectTransform FindExistingChild(Transform parent, string childName, int occurrenceIndex)
        {
            ReadOnlySpan<Transform> children = SnapshotChildren(parent);
            int matchingIndex = 0;
            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child == null || child.name != childName)
                    continue;

                if (matchingIndex == occurrenceIndex)
                    return child as RectTransform;

                matchingIndex++;
            }

            return null;
        }

        private static ReadOnlySpan<Transform> SnapshotChildren(Transform parent)
        {
            if (parent == null)
                return ReadOnlySpan<Transform>.Empty;

            int childCount = math.min(parent.childCount, s_childSnapshotBuffer.Length);
            for (int i = 0; i < childCount; i++)
                s_childSnapshotBuffer[i] = parent.GetChild(i);

            return s_childSnapshotBuffer.AsSpan(0, childCount);
        }
    }

    /// <summary>
    /// Player-owned diegetic sonar translator that converts active sonar contacts into terse PDA classification overlays.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Acoustic Echolocation Translator")]
    public sealed class AcousticEcholocationTranslator : MonoBehaviour, ILateFrameTickable, ISonarPulseEventListener, ISonarPingEventListener, ISonarSnapshotEventListener, ILocalizationLanguageChangedListener, IGlobalRegistryHotSwapListener
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
        private const float LeviathanClassificationRadius = 64f;
        private const float LeviathanClassificationRadiusSq = LeviathanClassificationRadius * LeviathanClassificationRadius;
        private const float LeviathanClassificationSearchPaddingMeters = 10f;
        private const int MaxBioformContacts = 24;
        private const int MaxAbyssalAnchorClassificationScan = 64;
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
        private const string StorageCapacityExceededText = "STORAGE FULL";
        private const int DynamicOverlaySortingOffset = 6;
        private const float HeavyFogAttenuationDistanceMeters = 18f;
        private const float HeavyFogDensityThreshold = 0.035f;
        private const float MinimumVisualSoundWaveVolume01 = 0.12f;
        private const ushort PhysicsEventTypeAcousticImpulse = 4;
        private const uint AcousticImpulseFlagLeviathan = 1u << 1;
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

        // COLD ALLOC: SpatialQueryHit[24] - active-sonar leviathan classification buffer - owner: AcousticEcholocationTranslator
        private readonly SpatialQueryHit[] _bioformContacts = new SpatialQueryHit[MaxBioformContacts];
        private readonly char[] _headerTextBuffer = new char[HeaderTextCapacity]; // COLD ALLOC: char[64] - sonar header TMP buffer - owner: AcousticEcholocationTranslator
        private readonly char[] _classificationTextBuffer = new char[ClassificationTextCapacity]; // COLD ALLOC: char[192] - sonar classification TMP buffer - owner: AcousticEcholocationTranslator
        private readonly char[] _classificationStressTextBuffer = new char[ClassificationTextCapacity]; // COLD ALLOC: char[192] - corrupted sonar classification TMP buffer - owner: AcousticEcholocationTranslator

        [Header("-- Font ------------------")]
        [Tooltip("Optional readable font override for the acoustic translator overlay.")]
        [SerializeField] private TMP_FontAsset labelFont;
        [Tooltip("Optional numeric font override for distance readouts.")]
        [SerializeField] private TMP_FontAsset numericFont;

        private bool _uiBuilt;
        private bool _tickRegistered;
        private int _pendingSnapshotPulseCount;
        private float _visibleTimer;
        private float _fadeTimer;
        private float _pulse01;
        private Canvas _targetCanvas;
        private Canvas _dynamicCanvas;
        private HectonMapMagicVegetationBridge _vegetationBridge;
        private RectTransform _root;
        private CanvasGroup _group;
        private Image _background;
        private TextMeshProUGUI _headerLabel;
        private TextMeshProUGUI _classificationLabel;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private ILocalizationStressPresentationReadModel _cachedLocalization;
        private IAtmosphereReadModel _cachedAtmosphere;
        private bool _headerDirty = true;
        private bool _plainClassificationDirty = true;
        private bool _localizedPresentationDirty;
        private bool _storageCapacityBarkActive;
        private bool _hotSwapListenerRegistered;
        private ContactClassification _lastRenderedClassification = ContactClassification.None;
        private int _lastRenderedDistanceMeters = int.MinValue;
        private int _lastPhysicsEventSnapshotGeneration;

        private void OnEnable()
        {
            labelFont = LocalizedFontResolver.ResolveReadableFont(labelFont);
            numericFont = LocalizedFontResolver.ResolveNumericFont(numericFont, labelFont);
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            ResolveAcousticOwners();
            EnsureUiBuilt();
            RegisterToTickManager();
            RefreshLocalizedCache();
            LocalizationEvents.RegisterLanguageListener(this);
            SpectrumEvents.RegisterSonarPulseListener(this);
            SpectrumEvents.RegisterSonarPingListener(this);
            SpectrumEvents.RegisterSonarSnapshotListener(this);
        }

        private void OnDisable()
        {
            LocalizationEvents.UnregisterLanguageListener(this);
            SpectrumEvents.UnregisterSonarPulseListener(this);
            SpectrumEvents.UnregisterSonarPingListener(this);
            SpectrumEvents.UnregisterSonarSnapshotListener(this);
            TryUnregisterHotSwapListener();
            _pendingSnapshotPulseCount = 0;
            _lastPhysicsEventSnapshotGeneration = 0;
            _cachedPlayerContext = null;
            _cachedLocalization = null;
            _cachedAtmosphere = null;
            UnregisterFromTickManager();
            ApplyRootAlpha(0f);
        }

        private void OnDestroy()
        {
            LocalizationEvents.UnregisterLanguageListener(this);
            SpectrumEvents.UnregisterSonarPulseListener(this);
            SpectrumEvents.UnregisterSonarPingListener(this);
            SpectrumEvents.UnregisterSonarSnapshotListener(this);
            TryUnregisterHotSwapListener();
            _pendingSnapshotPulseCount = 0;
            _lastPhysicsEventSnapshotGeneration = 0;
            UnregisterFromTickManager();
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            ApplyPendingLocalizationRefresh();
            DrainPhysicsEventPayloads();
            DrainStorageCapacityExceededBarks();

            float dt = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            if (_group == null)
            {
                return;
            }

            if (_pulse01 > 0f)
                _pulse01 = math.max(0f, _pulse01 - (dt * PulseDecaySharpness));

            if (_visibleTimer > 0f)
            {
                _visibleTimer -= dt;
                ApplyVisualState(1f);
                return;
            }

            if (_fadeTimer > 0f)
            {
                _fadeTimer = math.max(0f, _fadeTimer - dt);
                float alpha = FadeDuration > 0.0001f
                    ? math.saturate(_fadeTimer / FadeDuration)
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
        }

        private void HandleSonarPulse(float radius)
        {
            if (radius <= 0f)
                return;

            _pendingSnapshotPulseCount = math.min(_pendingSnapshotPulseCount + 1, 4);
        }

        private void HandleSonarPingSent(float intensity)
        {
            if (_pendingSnapshotPulseCount > 0)
                _pendingSnapshotPulseCount--;

            _pulse01 = math.max(_pulse01, math.saturate(intensity));
        }

        private void HandleSonarSnapshotUpdated(SpatialSonarSnapshot snapshot)
        {
            if (_pendingSnapshotPulseCount <= 0)
                return;

            _pendingSnapshotPulseCount--;
            if (_classificationLabel == null || _headerLabel == null)
                return;

            if (!TryResolveContact(snapshot, out ContactClassification classification, out int distanceMeters))
                return;

            ShowClassification(classification, distanceMeters);
        }

        void ISonarPulseEventListener.OnSonarPulse(float radius)
        {
            HandleSonarPulse(radius);
        }

        void ISonarPingEventListener.OnSonarPingSent(float intensity)
        {
            HandleSonarPingSent(intensity);
        }

        void ISonarSnapshotEventListener.OnSonarSnapshotUpdated(in SpatialSonarSnapshot snapshot)
        {
            HandleSonarSnapshotUpdated(snapshot);
        }

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            HandleLanguageChanged((GameLanguage)payload.Language);

        }

        /// <inheritdoc />
        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                _cachedPlayerContext = currentService as IPlayerRuntimeContext;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.LocalizationRuntime)
            {
                _cachedLocalization = currentService as ILocalizationStressPresentationReadModel;
                QueueLocalizationPresentationRefresh();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.AtmosphereRuntime)
            {
                _cachedAtmosphere = currentService as IAtmosphereReadModel;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                UnregisterFromTickManager();
                if (isActiveAndEnabled)
                {
                    if (currentService != null)
                        RegisterToTickManager();
                }
            }
        }


        private void HandleLanguageChanged(GameLanguage _)
        {
            QueueLocalizationPresentationRefresh();
        }

        private void QueueLocalizationPresentationRefresh()
        {
            _localizedPresentationDirty = true;
            _headerDirty = true;
            _plainClassificationDirty = true;
            _lastRenderedClassification = ContactClassification.None;
            _lastRenderedDistanceMeters = int.MinValue;
            if (isActiveAndEnabled)
                RegisterToTickManager();
        }

        private void ApplyPendingLocalizationRefresh()
        {
            if (!_localizedPresentationDirty)
                return;

            _localizedPresentationDirty = false;
            RefreshLocalizedCache();
        }

        private void DrainStorageCapacityExceededBarks()
        {
            if (!AcousticEcholocationBarkEvents.ConsumeStorageCapacityExceeded())
                return;

            while (AcousticEcholocationBarkEvents.ConsumeStorageCapacityExceeded())
            {
            }

            ShowStorageCapacityExceededBark();
        }

        private void ShowStorageCapacityExceededBark()
        {
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
        }

        private void DrainPhysicsEventPayloads()
        {
            int snapshotGeneration = SignalBus<PhysicsEventPayload>.SnapshotGeneration;
            if (snapshotGeneration == _lastPhysicsEventSnapshotGeneration)
                return;

            _lastPhysicsEventSnapshotGeneration = snapshotGeneration;
            ReadOnlySpan<PhysicsEventPayload> signals = SignalBus<PhysicsEventPayload>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                PhysicsEventPayload payload = signals[i];
                if (payload.EventType != PhysicsEventTypeAcousticImpulse)
                    continue;

                HandleAcousticImpulsePayload(in payload);
            }
        }

        private void HandleAcousticImpulsePayload(in PhysicsEventPayload impulseEvent)
        {
            if ((impulseEvent.StatusBits & AcousticImpulseFlagLeviathan) == 0u ||
                impulseEvent.Scalar1 < MinimumVisualSoundWaveVolume01 ||
                !ShouldRenderVisualSoundWave())
            {
                return;
            }

            if (_classificationLabel == null || _headerLabel == null)
                return;

            int headerLength = CopySpanToBuffer(DefaultSoundWaveHeader.AsSpan(), _headerTextBuffer);
            _headerLabel.SetCharArray(_headerTextBuffer, 0, headerLength);
            int distanceMeters = ResolveRuntimeDistanceMeters(impulseEvent.RuntimePosition);
            int waveTextLength = WriteVisualSoundWaveText(distanceMeters, impulseEvent.Scalar1, _classificationTextBuffer);
            _classificationLabel.SetCharArray(_classificationTextBuffer, 0, waveTextLength);

            _storageCapacityBarkActive = false;
            _visibleTimer = VisibleDuration;
            _fadeTimer = FadeDuration;
            _pulse01 = math.max(_pulse01, math.saturate(impulseEvent.Scalar1));
            _headerDirty = true;
            _plainClassificationDirty = true;
            _lastRenderedClassification = ContactClassification.None;
            _lastRenderedDistanceMeters = int.MinValue;
            ApplyVisualState(1f);
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
            if (!SpatialSonarSnapshot.HasNearestBioform(in snapshot))
                return false;

            float snapshotDistance = math.select(
                float.MaxValue,
                (float)snapshot.NearestBioformDistanceMeters,
                snapshot.NearestBioformDistanceMeters > 0);
            if (snapshotDistance > LeviathanClassificationRadius)
                return false;

            float searchRadius = math.clamp(
                snapshotDistance + LeviathanClassificationSearchPaddingMeters,
                18f,
                LeviathanClassificationRadius);
            if (!TryResolveClassificationOriginAup(out AbsoluteUniversePosition originAup))
                return false;

            int contactCount = FaunaSpatialHashRegistry.CollectContactsNonAlloc(
                in originAup,
                searchRadius,
                SpatialTargetKind.Bioform,
                _bioformContacts);

            float nearestDistanceSqr = float.MaxValue;
            AbsoluteUniversePosition nearestAup = default;
            for (int i = 0; i < contactCount; i++)
            {
                SpatialQueryHit contact = _bioformContacts[i];
                IFaunaSpatialContact faunaContact = contact.Owner as IFaunaSpatialContact;
                if (faunaContact == null || faunaContact.IsDead)
                    continue;

                if (!faunaContact.IsLeviathanContact)
                    continue;

                float candidateDistanceSqr = contact.DistanceSqr;
                if (!math.isfinite(candidateDistanceSqr) ||
                    candidateDistanceSqr > LeviathanClassificationRadiusSq ||
                    candidateDistanceSqr >= nearestDistanceSqr)
                {
                    continue;
                }

                AbsoluteUniversePosition candidateAup;
                if (contact.HasAbsolutePosition)
                {
                    candidateAup = contact.AbsolutePosition;
                    if (!AbsoluteUniversePosition.IsFinite(in candidateAup))
                        continue;
                }
                else if (!TryResolveAupFromRuntimeOrigin(contact.Position, out candidateAup))
                {
                    continue;
                }

                nearestDistanceSqr = candidateDistanceSqr;
                nearestAup = candidateAup;
            }

            if (nearestDistanceSqr == float.MaxValue)
                return false;

            distanceMeters = RoundApproximateAupDistanceMeters(in originAup, in nearestAup);
            return true;
        }

        private bool TryResolveNearestAbyssalAnchor(out int distanceMeters)
        {
            distanceMeters = 0;
            if (_vegetationBridge == null)
            {
                return false;
            }

            if (!TryResolveClassificationOriginAup(out AbsoluteUniversePosition originAup))
                return false;

            if (_vegetationBridge.TryGetActiveAbyssalAnchorAupPayload(out NativeArray<AbsoluteUniversePosition>.ReadOnly anchorAups, out int aupCount) &&
                anchorAups.Length > 0 &&
                aupCount > 0)
            {
                return TryResolveNearestAbyssalAnchorDistance(anchorAups, aupCount, in originAup, out distanceMeters);
            }

            if (!_vegetationBridge.TryGetActiveAbyssalAnchorPayload(out NativeArray<Vector3>.ReadOnly anchors, out int count) ||
                anchors.Length <= 0 ||
                count <= 0)
            {
                return false;
            }

            int limit = math.min(MaxAbyssalAnchorClassificationScan, math.min(count, anchors.Length));
            double nearestDistanceMeters = double.MaxValue;
            for (int i = 0; i < limit; i++)
            {
                if (!TryResolveAupFromRuntimeOrigin(anchors[i], out AbsoluteUniversePosition anchorAup))
                    continue;

                double candidateDistanceMeters = ApproximateAupDistanceMeters(in anchorAup, in originAup);
                if (!IsFiniteNonNegativeDistanceMeters(candidateDistanceMeters) ||
                    candidateDistanceMeters > AnchorClassificationRadius ||
                    candidateDistanceMeters >= nearestDistanceMeters)
                {
                    continue;
                }

                nearestDistanceMeters = candidateDistanceMeters;
            }

            if (nearestDistanceMeters == double.MaxValue)
                return false;

            distanceMeters = nearestDistanceMeters >= int.MaxValue ? int.MaxValue : (int)math.round(nearestDistanceMeters);
            return true;
        }

        private static bool TryResolveNearestAbyssalAnchorDistance(
            NativeArray<AbsoluteUniversePosition>.ReadOnly anchorAups,
            int count,
            in AbsoluteUniversePosition originAup,
            out int distanceMeters)
        {
            distanceMeters = 0;
            int limit = math.min(MaxAbyssalAnchorClassificationScan, math.min(count, anchorAups.Length));
            double nearestDistanceMeters = double.MaxValue;
            for (int i = 0; i < limit; i++)
            {
                AbsoluteUniversePosition anchorAup = anchorAups[i];
                double candidateDistanceMeters = ApproximateAupDistanceMeters(in anchorAup, in originAup);
                if (!IsFiniteNonNegativeDistanceMeters(candidateDistanceMeters) ||
                    candidateDistanceMeters > AnchorClassificationRadius ||
                    candidateDistanceMeters >= nearestDistanceMeters)
                {
                    continue;
                }

                nearestDistanceMeters = candidateDistanceMeters;
            }

            if (nearestDistanceMeters == double.MaxValue)
                return false;

            distanceMeters = nearestDistanceMeters >= int.MaxValue ? int.MaxValue : (int)math.round(nearestDistanceMeters);
            return true;
        }

        private bool TryResolveClassificationOriginAup(out AbsoluteUniversePosition originAup)
        {
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext != null &&
                playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u)
            {
                originAup = movementState.PredictedAup;
                return AbsoluteUniversePosition.IsFinite(in originAup);
            }

            HectonPlayerMovement movement = playerContext != null ? playerContext.PlayerMovement : null;
            if (movement != null)
            {
                originAup = movement.CurrentAup;
                return AbsoluteUniversePosition.IsFinite(in originAup);
            }

            originAup = default;
            return false;
        }

        private static int RoundApproximateAupDistanceMeters(in AbsoluteUniversePosition a, in AbsoluteUniversePosition b)
        {
            double distanceMeters = ApproximateAupDistanceMeters(in a, in b);
            if (!IsFiniteNonNegativeDistanceMeters(distanceMeters))
                return int.MaxValue;
            return distanceMeters >= int.MaxValue ? int.MaxValue : (int)math.round(distanceMeters);
        }

        private static double ApproximateAupDistanceMeters(in AbsoluteUniversePosition a, in AbsoluteUniversePosition b)
        {
            double approximateDistance = AbsoluteUniversePosition.ApproximateDistanceMetersClamped(in a, in b);
            if (!IsFiniteNonNegativeDistanceMeters(approximateDistance))
                return double.PositiveInfinity;
            return approximateDistance >= int.MaxValue ? int.MaxValue : approximateDistance;
        }

        private static bool IsFiniteNonNegativeDistanceMeters(double distanceMeters)
        {
            return !double.IsNaN(distanceMeters) &&
                   !double.IsInfinity(distanceMeters) &&
                   distanceMeters >= 0d;
        }

        private void ShowClassification(ContactClassification classification, int distanceMeters)
        {
            _storageCapacityBarkActive = false;
            ReadOnlySpan<char> classText = classification == ContactClassification.Leviathan
                ? ResolveLocalizedSpan(LeviathanClassKeyHash, DefaultLeviathanClass.AsSpan())
                : ResolveLocalizedSpan(WreckageClassKeyHash, DefaultWreckageClass.AsSpan());

            ILocalizationStressPresentationReadModel localization = _cachedLocalization;
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
            _pulse01 = math.max(_pulse01, 1f);
            ApplyVisualState(1f);
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

        private void ApplyStressMutatedClassification(ILocalizationStressPresentationReadModel localization, ReadOnlySpan<char> classText, int distanceMeters)
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
                    _classificationTextBuffer.AsSpan(0, sourceLength),
                    _classificationStressTextBuffer,
                    out int classificationLength))
            {
                _classificationLabel.SetCharArray(_classificationStressTextBuffer, 0, classificationLength);
                _plainClassificationDirty = true;
                _lastRenderedClassification = ContactClassification.None;
                _lastRenderedDistanceMeters = int.MinValue;
            }
        }

        private static bool ShouldUseStressMutation(ILocalizationStressPresentationReadModel localization)
        {
            return localization != null &&
                   (localization.GetHullStressCorruptionIntensity() > 0f ||
                    localization.IsMadnessWhisperVisualActive());
        }

        private bool ShouldRenderVisualSoundWave()
        {
            ILocalizationStressPresentationReadModel localization = _cachedLocalization;
            if (ShouldUseStressMutation(localization))
                return true;

            IAtmosphereReadModel atmosphere = _cachedAtmosphere;
            if (atmosphere == null)
                return false;

            return atmosphere.CurrentFogAttenuationDistance <= HeavyFogAttenuationDistanceMeters ||
                   atmosphere.CurrentFogDensity >= HeavyFogDensityThreshold;
        }

        private int ResolveRuntimeDistanceMeters(Vector3 runtimePosition)
        {
            if (!TryResolveClassificationOriginAup(out AbsoluteUniversePosition originAup))
                return 0;

            if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition targetAup))
                return 0;

            return RoundApproximateAupDistanceMeters(in originAup, in targetAup);
        }

        private static int WriteVisualSoundWaveText(int distanceMeters, float volume01, char[] buffer)
        {
            int cursor = AppendSpanToBuffer(DefaultVisualSoundWaveText.AsSpan(), buffer, 0);
            cursor = AppendCharToBuffer(' ', buffer, cursor);
            cursor = AppendCharToBuffer('/', buffer, cursor);
            cursor = AppendCharToBuffer('/', buffer, cursor);
            cursor = AppendCharToBuffer(' ', buffer, cursor);
            if (cursor < buffer.Length &&
                distanceMeters.TryFormat(buffer.AsSpan(cursor, buffer.Length - cursor), out int distanceWritten))
            {
                cursor += distanceWritten;
            }

            cursor = AppendCharToBuffer('M', buffer, cursor);
            cursor = AppendCharToBuffer(' ', buffer, cursor);
            cursor = AppendCharToBuffer('/', buffer, cursor);
            cursor = AppendCharToBuffer('/', buffer, cursor);
            cursor = AppendCharToBuffer(' ', buffer, cursor);
            int intensityPercent = (int)math.round(math.saturate(volume01) * 100f);
            if (cursor < buffer.Length &&
                intensityPercent.TryFormat(buffer.AsSpan(cursor, buffer.Length - cursor), out int intensityWritten))
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
                distanceMeters.TryFormat(buffer.AsSpan(cursor, buffer.Length - cursor), out int written))
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
                _background.color = new Color(frameColor.r, frameColor.g, frameColor.b, math.lerp(0f, frameColor.a, alpha));
            }

            if (_headerLabel != null)
                _headerLabel.color = _storageCapacityBarkActive ? StorageBarkHeaderColor : HeaderColor;

            if (_classificationLabel != null)
            {
                Color baseValue = _storageCapacityBarkActive ? StorageBarkValueColor : ValueColor;
                Color pulseValue = _storageCapacityBarkActive ? StorageBarkHeaderColor : HeaderColor;
                float pulseBlend = math.saturate(_pulse01 * 0.45f);
                _classificationLabel.color = new Color(
                    math.lerp(baseValue.r, pulseValue.r, pulseBlend),
                    math.lerp(baseValue.g, pulseValue.g, pulseBlend),
                    math.lerp(baseValue.b, pulseValue.b, pulseBlend),
                    math.lerp(baseValue.a, pulseValue.a, pulseBlend));
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
                return;

            _root.anchorMin = new Vector2(1f, 1f);
            _root.anchorMax = new Vector2(1f, 1f);
            _root.pivot = new Vector2(1f, 1f);
            _root.anchoredPosition = new Vector2(-42f, -86f);
            _root.sizeDelta = new Vector2(OverlayWidth, OverlayHeight);
            _root.localScale = Vector3.one;
            _root.SetAsLastSibling();
            _dynamicCanvas = EnsureDynamicOverlayCanvas(_root, _targetCanvas, _dynamicCanvas);

            if (!_root.TryGetComponent(out _group) || _group == null)
                return;
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;

            if (!_root.TryGetComponent(out _background) || _background == null)
                return;
            _background.color = FrameColor;
            _background.raycastTarget = false;

            ConfigureExistingRule(0, new Vector2(18f, -18f), new Vector2(-18f, -18f));
            ConfigureExistingRule(1, new Vector2(18f, -74f), new Vector2(-18f, -74f));

            _headerLabel = BindExistingText("Header", labelFont, 12f, FontStyles.Bold, HeaderColor, TextAlignmentOptions.Left);
            if (_headerLabel == null)
                return;

            Anchor(_headerLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -12f), new Vector2(-20f, -34f));

            _classificationLabel = BindExistingText("Classification", numericFont, 13f, FontStyles.Bold, ValueColor, TextAlignmentOptions.Left);
            if (_classificationLabel == null)
                return;

            Anchor(_classificationLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -34f), new Vector2(-20f, -72f));

            _uiBuilt = true;
        }

        private static Canvas EnsureDynamicOverlayCanvas(RectTransform root, Canvas targetCanvas, Canvas existingCanvas)
        {
            if (root == null || targetCanvas == null)
                return existingCanvas;

            Canvas canvas = existingCanvas;
            if (canvas == null && !root.TryGetComponent(out canvas))
                return existingCanvas;

            canvas.renderMode = targetCanvas.renderMode;
            canvas.worldCamera = targetCanvas.worldCamera;
            canvas.planeDistance = targetCanvas.planeDistance;
            canvas.pixelPerfect = false;
            canvas.overrideSorting = true;
            canvas.sortingOrder = targetCanvas.sortingOrder + DynamicOverlaySortingOffset;
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.None;

            if (canvas.TryGetComponent(out GraphicRaycaster raycaster))
                raycaster.enabled = false;

            return canvas;
        }

        private void ConfigureExistingRule(int occurrenceIndex, Vector2 leftOffset, Vector2 rightOffset)
        {
            RectTransform rule = UiChildSpanUtility.FindExistingChild(_root, "Rule", occurrenceIndex);
            if (rule == null || !rule.TryGetComponent(out Image image) || image == null)
                return;

            image.color = AccentColor;
            image.raycastTarget = false;
            Anchor(rule, new Vector2(0f, 1f), new Vector2(1f, 1f), leftOffset, rightOffset);
            rule.sizeDelta = new Vector2(0f, 1f);
        }

        private void CacheRegistryServicesCold()
        {
            _cachedPlayerContext = GlobalRegistry.Player;
            _cachedLocalization = GlobalRegistry.LocalizationStressPresentation;
            _cachedAtmosphere = GlobalRegistry.AtmosphereReadModel;
        }

        private void RegisterToTickManager()
        {
            if (_tickRegistered || !Application.isPlaying)
                return;

            _tickRegistered = SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI);
        }

        private void UnregisterFromTickManager()
        {
            if (!_tickRegistered)
                return;

            SystemDispatcher.UnregisterLateFrameTickableDirect(this, PriorityLayer.UI);
            _tickRegistered = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        private void ApplyRootAlpha(float alpha)
        {
            if (_group != null && math.abs(_group.alpha - alpha) > 0.0001f)
                _group.alpha = alpha;
        }

        private static Canvas ResolveTargetCanvas()
        {
            SuitHUDV4CanvasOverlay overlay = SuitHUDV4CanvasOverlay.ActiveRuntimeInstance;
            if (overlay != null && overlay.TargetCanvas != null)
                return overlay.TargetCanvas;

            if (overlay == null)
                return null;

            overlay.TryGetComponent(out Canvas canvas);
            return canvas;
        }

        private ReadOnlySpan<char> ResolveLocalizedSpan(int keyHash, ReadOnlySpan<char> fallback)
        {
            ILocalizationStressPresentationReadModel manager = _cachedLocalization;
            return manager != null ? manager.GetRawSpanOrFallback(keyHash, fallback) : fallback;
        }

        private TextMeshProUGUI BindExistingText(string name, TMP_FontAsset fontAsset, float size, FontStyles style, Color color, TextAlignmentOptions alignment)
        {
            RectTransform rect = FindExistingChild(_root, name);
            if (rect == null || !rect.TryGetComponent(out TextMeshProUGUI text) || text == null)
                return null;

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

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!math.isfinite(runtimePosition.x) || !math.isfinite(runtimePosition.y) || !math.isfinite(runtimePosition.z))
                return false;

            double3 origin = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            if (!math.all(math.isfinite(origin)))
                return false;

            AbsoluteUniversePosition originAup = AbsoluteUniversePosition.FromAbsolutePosition(origin);
            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return AbsoluteUniversePosition.IsFinite(in positionAup);
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
    public sealed class TerminalBootSequence : MonoBehaviour, ILateFrameTickable, ISonarPingEventListener, IGlobalRegistryHotSwapListener
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
        private static ReadOnlySpan<char> StatusOkChars => "[OK]".AsSpan();
        private static ReadOnlySpan<char> StatusDegradedChars => "[DEGRADED]".AsSpan();
        private static ReadOnlySpan<char> StatusFailedChars => "[FAILED]".AsSpan();

        [Header("-- Font ------------------")]
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
        private IPlayerRuntimeContext _cachedPlayerContext;
        private bool _hotSwapListenerRegistered;
        private readonly char[] _sequenceTextBuffer = new char[SequenceTextCapacity]; // COLD ALLOC: char[256] - sonar boot sequence TMP buffer - owner: TerminalBootSequence

        private void OnEnable()
        {
            font = LocalizedFontResolver.ResolveReadableFont(font);
            CachePlayerRuntimeContextCold();
            TryRegisterHotSwapListener();
            EnsureUiBuilt();
            RegisterToTickManager();
            SpectrumEvents.RegisterSonarPingListener(this);
        }

        private void OnDisable()
        {
            SpectrumEvents.UnregisterSonarPingListener(this);
            TryUnregisterHotSwapListener();
            UnregisterFromTickManager();
            HideOverlay();
        }

        private void OnDestroy()
        {
            SpectrumEvents.UnregisterSonarPingListener(this);
            TryUnregisterHotSwapListener();
            UnregisterFromTickManager();
        }

        /// <inheritdoc />
        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                _cachedPlayerContext = currentService as IPlayerRuntimeContext;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                UnregisterFromTickManager();
                if (isActiveAndEnabled)
                {
                    if (currentService != null)
                        RegisterToTickManager();
                }
            }
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            float dt = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            if (_consoleLabel == null || _overlayGroup == null || _state == SequenceState.Hidden)
                return;

            switch (_state)
            {
                case SequenceState.Typing:
                    _visibleCharacterProgress += dt * CharacterRevealRate;
                    int visibleCharacters = math.min(_visibleCharacterTarget, (int)math.floor(_visibleCharacterProgress));
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
                    _overlayGroup.alpha = math.lerp(_overlayGroup.alpha, 0f, math.saturate(FadeSharpness * dt));
                    if (_overlayGroup.alpha <= HiddenAlphaCutoff)
                    {
                        HideOverlay();
                    }
                    break;
            }
        }

        private void HandleSonarPingSent(float intensity)
        {
            if (intensity <= 0.001f)
                return;

            EnsureUiBuilt();
            if (_consoleLabel == null || _overlayGroup == null)
                return;

            int sequenceTextLength = BuildSequenceText(_sequenceTextBuffer);
            _consoleLabel.SetCharArray(_sequenceTextBuffer, 0, sequenceTextLength);
            _visibleCharacterTarget = sequenceTextLength;
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

        private int BuildSequenceText(char[] buffer)
        {
            bool hasSurvivalState = TryReadSurvivalRuntimeState(out PlayerSurvivalRuntimeState survivalState);
            float integrity01 = hasSurvivalState ? Sanitize01(survivalState.IntegrityNormalized) : 0f;
            float energy01 = hasSurvivalState ? Sanitize01(survivalState.EnergyNormalized) : 0f;
            float hullStress01 = hasSurvivalState ? math.saturate(1f - integrity01) : 1f;
            ReadOnlySpan<char> hullStatus = ResolveIntegrityStatusChars(integrity01);
            ReadOnlySpan<char> powerStatus = energy01 >= 0.25f ? StatusOkChars : StatusDegradedChars;
            ReadOnlySpan<char> linkStatus = hullStress01 <= 0.18f ? StatusOkChars : StatusDegradedChars;

            int cursor = 0;
            cursor = AppendSpan(buffer, cursor, StatusOkChars);
            cursor = AppendLine(buffer, cursor, " MOUNTING SONAR_DRIVER...".AsSpan());
            cursor = AppendSpan(buffer, cursor, StatusOkChars);
            cursor = AppendSpan(buffer, cursor, " AUP SECTOR 0x".AsSpan());
            cursor = AppendHex8(buffer, cursor, ResolveFakeAupSectorHash());
            cursor = AppendChar(buffer, cursor, '\n');
            cursor = AppendSpan(buffer, cursor, StatusOkChars);
            cursor = AppendLine(buffer, cursor, " LOADING NEURAL INTERFACE...".AsSpan());
            cursor = AppendSpan(buffer, cursor, StatusOkChars);
            cursor = AppendLine(buffer, cursor, " CALIBRATING LIDAR ARRAY...".AsSpan());
            cursor = AppendSpan(buffer, cursor, linkStatus);
            cursor = AppendSpan(buffer, cursor, " ACOUSTIC BUS LINK... HULL ".AsSpan());
            cursor = AppendInt(buffer, cursor, hasSurvivalState ? (int)math.round(integrity01 * 100f) : 0);
            cursor = AppendChar(buffer, cursor, '%');
            cursor = AppendChar(buffer, cursor, '\n');
            cursor = AppendSpan(buffer, cursor, powerStatus);
            cursor = AppendSpan(buffer, cursor, " POWER FEED... ".AsSpan());
            cursor = AppendInt(buffer, cursor, hasSurvivalState ? (int)math.round(energy01 * 100f) : 0);
            cursor = AppendChar(buffer, cursor, '%');
            cursor = AppendChar(buffer, cursor, '\n');
            cursor = AppendSpan(buffer, cursor, hullStatus);
            cursor = AppendSpan(buffer, cursor, " NOISE FILTER... STRESS ".AsSpan());
            cursor = AppendInt(buffer, cursor, hasSurvivalState ? (int)math.round(hullStress01 * 100f) : 100);
            return AppendChar(buffer, cursor, '%');
        }

        private bool TryReadSurvivalRuntimeState(out PlayerSurvivalRuntimeState survivalState)
        {
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext != null && playerContext.TryGetSurvivalRuntimeState(out survivalState))
                return true;

            survivalState = default;
            return false;
        }

        private void CachePlayerRuntimeContextCold()
        {
            _cachedPlayerContext = GlobalRegistry.Player;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        private static int AppendLine(char[] buffer, int cursor, ReadOnlySpan<char> value)
        {
            cursor = AppendSpan(buffer, cursor, value);
            return AppendChar(buffer, cursor, '\n');
        }

        private static int AppendSpan(char[] buffer, int cursor, ReadOnlySpan<char> value)
        {
            if (buffer == null || value.IsEmpty || cursor >= buffer.Length)
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

            return value.TryFormat(buffer.AsSpan(cursor, buffer.Length - cursor), out int written)
                ? cursor + written
                : cursor;
        }

        private static int AppendHex8(char[] buffer, int cursor, uint value)
        {
            if (buffer == null || cursor >= buffer.Length)
                return cursor;

            Span<char> destination = buffer.AsSpan(cursor, buffer.Length - cursor);
            return value.TryFormat(destination, out int written, "X8")
                ? cursor + written
                : cursor;
        }

        private static uint ResolveFakeAupSectorHash()
        {
            uint hash = 2166136261u;
            hash = (hash ^ HectonFloatingOrigin.LastShiftEvent.Sequence) * 16777619u;
            hash = (hash ^ Hecton8.Core.SystemDispatcher.CurrentFrameId) * 16777619u;
            return hash ^ 0xA8F1D3C5u;
        }

        private static int AppendChar(char[] buffer, int cursor, char value)
        {
            if (buffer == null || cursor >= buffer.Length)
                return cursor;

            buffer[cursor] = value;
            return cursor + 1;
        }

        private static ReadOnlySpan<char> ResolveIntegrityStatusChars(float integrity01)
        {
            if (integrity01 < 0.55f)
                return StatusFailedChars;

            if (integrity01 < 0.82f)
                return StatusDegradedChars;

            return StatusOkChars;
        }

        private static float Sanitize01(float value)
        {
            return math.select(0f, math.saturate(value), math.isfinite(value));
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
                return;

            _overlayRoot.anchorMin = new Vector2(0f, 1f);
            _overlayRoot.anchorMax = new Vector2(0f, 1f);
            _overlayRoot.pivot = new Vector2(0f, 1f);
            _overlayRoot.anchoredPosition = new Vector2(34f, -188f);
            _overlayRoot.sizeDelta = new Vector2(OverlayWidth, OverlayHeight);
            _overlayRoot.localScale = Vector3.one;
            _overlayRoot.SetAsLastSibling();

            if (!_overlayRoot.TryGetComponent(out _overlayGroup) || _overlayGroup == null)
                return;
            _overlayGroup.alpha = 0f;
            _overlayGroup.blocksRaycasts = false;
            _overlayGroup.interactable = false;

            if (!_overlayRoot.TryGetComponent(out Image background) || background == null)
                return;
            background.color = new Color(0.02f, 0.07f, 0.08f, 0.72f);
            background.raycastTarget = false;

            RectTransform textRoot = FindExistingChild(_overlayRoot, "ConsoleText");
            if (textRoot == null || !textRoot.TryGetComponent(out _consoleLabel) || _consoleLabel == null)
                return;

            textRoot.anchorMin = Vector2.zero;
            textRoot.anchorMax = Vector2.one;
            textRoot.offsetMin = new Vector2(16f, 12f);
            textRoot.offsetMax = new Vector2(-16f, -12f);

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

            _tickRegistered = SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI);
        }

        private void UnregisterFromTickManager()
        {
            if (!_tickRegistered)
                return;

            SystemDispatcher.UnregisterLateFrameTickableDirect(this, PriorityLayer.UI);
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

            if (overlay == null)
                return null;

            overlay.TryGetComponent(out Canvas canvas);
            return canvas;
        }

        private static RectTransform FindExistingChild(Transform parent, string childName)
        {
            return UiChildSpanUtility.FindExistingChild(parent, childName);
        }

    }

    /// <summary>
    /// Player-owned overlay that renders pooled spatial-audio captions around the HUD center.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Audio Caption Overlay")]
    public sealed class AudioCaptionOverlay : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener
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
        private const float CaptionPositionEpsilonSq = 0.0004f;
        private const float CaptionAlphaEpsilon = 0.001f;
        private const string OverlayName = "AudioCaptionOverlay";
        private const string SlotName = "CaptionSlot";
        private const string TextName = "Text";

        private static readonly Color CaptionColor = new Color(0.86f, 0.97f, 0.92f, 0.94f);
        private static readonly Color CaptionShadowColor = new Color(0.06f, 0.11f, 0.12f, 0.84f);
        private static readonly Vector2 CaptionSize = new Vector2(240f, 44f);
        private static readonly Vector2 CaptionHiddenPosition = new Vector2(float.MaxValue, float.MaxValue);
        private static readonly Vector2 CaptionVerticalOffset = new Vector2(0f, VerticalBias);

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
            public AbsoluteUniversePosition WorldAup;
            public bool HasWorldAup;
            public char[] TextBuffer;
            public int TextLength;
            public uint TextHash;
            public Vector2 LastAnchoredPosition;
            public float LastAlpha;
        }

        private struct CaptionViewFrame
        {
            public Vector3 Origin;
            public AbsoluteUniversePosition OriginAup;
            public float3 Right;
            public float3 Up;
            public float3 Forward;
            public byte HasView;
            public byte HasOriginAup;
        }

        [Header("-- Font ------------------")]
        [Tooltip("Readable font override for spatial audio captions.")]
        [SerializeField] private TMP_FontAsset labelFont;

        private Canvas _targetCanvas;
        private Camera _viewCamera;
        private Transform _viewTransform;
        private RectTransform _overlayRoot;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private bool _tickRegistered;
        private bool _uiBuilt;
        private bool _hotSwapListenerRegistered;
        private bool _captionConsumerRegistered;
        // COLD ALLOC: CaptionSlot[8] - pooled spatial audio caption slots - owner: AudioCaptionOverlay
        private readonly CaptionSlot[] _slots = new CaptionSlot[SlotCount];

        private void OnEnable()
        {
            labelFont = LocalizedFontResolver.ResolveReadableFont(labelFont);
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            EnsureUiBuilt(allowComponentFallback: true);
            RegisterToTickManager();
            TryRegisterCaptionConsumer();
        }

        private void OnDisable()
        {
            TryUnregisterCaptionConsumer();
            TryUnregisterHotSwapListener();
            UnregisterFromTickManager();
            HideAllSlots();
            _cachedPlayerContext = null;
            _viewCamera = null;
            _viewTransform = null;
        }

        private void OnDestroy()
        {
            TryUnregisterCaptionConsumer();
            TryUnregisterHotSwapListener();
            UnregisterFromTickManager();
        }

        /// <inheritdoc />
        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Player)
            {
                if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
                {
                    TryUnregisterCaptionConsumer();
                    UnregisterFromTickManager();
                    if (isActiveAndEnabled)
                    {
                        if (currentService != null)
                        {
                            RegisterToTickManager();
                            TryRegisterCaptionConsumer();
                        }
                    }
                }
                return;
            }

            _cachedPlayerContext = currentService as IPlayerRuntimeContext;
            _viewCamera = null;
            _viewTransform = null;
        }

        public void LateFrameTick()
        {
            float dt = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            if (!_uiBuilt)
                return;

            if (_viewTransform == null)
                ResolveViewCamera();

            CaptionViewFrame viewFrame = ResolveCaptionViewFrame();
            int activeCount = 0;
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

                UpdateSlotPose(ref slot, in viewFrame);
                _slots[i] = slot;
                activeCount++;
            }

            DrainPendingCaptionRequests();

            if (activeCount <= 0)
                return;
        }

        private void DrainPendingCaptionRequests()
        {
            int scanBudget = AudioCaptionEvents.PendingCount;
            while (scanBudget-- > 0 && AudioCaptionEvents.PendingCount > 0)
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (AudioCaptionEvents.ConsumeNextPendingCaption(out AudioCaptionRequest request))
                    HandleCaptionRequested(request);
            }
        }

        private void HandleCaptionRequested(AudioCaptionRequest request)
        {
            if (!_uiBuilt)
                return;

            int slotIndex = AcquireSlotIndex();
            ref CaptionSlot slot = ref _slots[slotIndex];
            slot.Active = true;
            slot.Age = 0f;
            slot.Duration = math.max(MinDuration, request.DurationSeconds > 0f ? request.DurationSeconds : DefaultDuration);
            slot.Intensity = math.saturate(request.Intensity);
            slot.WorldPosition = request.WorldPosition;
            slot.WorldAup = request.ResolveWorldAup();
            slot.HasWorldAup = true;
            slot.LastAnchoredPosition = CaptionHiddenPosition;
            slot.LastAlpha = -1f;
            if (slot.Label != null &&
                slot.TextBuffer != null &&
                AudioCaptionEvents.TryWriteCaptionText(
                    request.CaptionHashId,
                    slot.TextBuffer.AsSpan(),
                    out int displayLength,
                    out int sourceLength,
                    out bool localized))
            {
                uint displayHash = ComputeCaptionDisplayHash(slot.TextBuffer, displayLength);
                if (slot.TextLength != displayLength || slot.TextHash != displayHash)
                {
                    slot.Label.SetCharArray(slot.TextBuffer, 0, displayLength);
                    slot.TextLength = displayLength;
                    slot.TextHash = displayHash;
                }

                if (!localized && sourceLength > displayLength)
                {
                    BabelSubtitleSyncRuntime.RecordUIOptimizationFailure(
                        request.CaptionHashId,
                        UIOptimizationFailureCode.TextBufferOverflow,
                        sourceLength,
                        displayLength,
                        slot.TextBuffer.Length,
                        0u);
                }
            }

            if (slot.Group != null)
            {
                slot.Group.alpha = 1f;
                slot.Group.blocksRaycasts = false;
                slot.Group.interactable = false;
                slot.LastAlpha = 1f;
            }
            CaptionViewFrame viewFrame = ResolveCaptionViewFrame();
            UpdateSlotPose(ref slot, in viewFrame);
        }

        private void EnsureUiBuilt(bool allowComponentFallback)
        {
            if (_uiBuilt)
                return;

            _targetCanvas = ResolveTargetCanvas(allowComponentFallback);
            if (_targetCanvas == null)
                return;

            RectTransform contentRoot = HectonUIScaler.ResolveContentRoot(_targetCanvas);
            if (contentRoot == null)
                return;

            _overlayRoot = FindExistingChild(contentRoot, OverlayName);
            if (_overlayRoot == null)
                return;

            _overlayRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _overlayRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _overlayRoot.pivot = new Vector2(0.5f, 0.5f);
            _overlayRoot.anchoredPosition = Vector2.zero;
            _overlayRoot.sizeDelta = Vector2.zero;
            _overlayRoot.localScale = Vector3.one;
            _overlayRoot.SetAsLastSibling();

            for (int i = 0; i < _slots.Length; i++)
            {
                if (!TryResolveSlotView(i, out _, out _, out _))
                    return;
            }

            for (int i = 0; i < _slots.Length; i++)
                BindExistingSlot(i);

            ResolveViewCamera();
            _uiBuilt = true;
        }

        private bool TryResolveSlotView(
            int index,
            out RectTransform slotRoot,
            out CanvasGroup group,
            out TextMeshProUGUI text)
        {
            slotRoot = UiChildSpanUtility.FindExistingChild(_overlayRoot, SlotName, index);
            group = null;
            text = null;
            if (slotRoot == null || !slotRoot.TryGetComponent(out group) || group == null)
                return false;

            RectTransform textRoot = FindExistingChild(slotRoot, TextName);
            if (textRoot == null || !textRoot.TryGetComponent(out text) || text == null)
                return false;

            return true;
        }

        private void BindExistingSlot(int index)
        {
            if (!TryResolveSlotView(index, out RectTransform slotRoot, out CanvasGroup group, out TextMeshProUGUI text))
                return;

            slotRoot.anchorMin = new Vector2(0.5f, 0.5f);
            slotRoot.anchorMax = new Vector2(0.5f, 0.5f);
            slotRoot.pivot = new Vector2(0.5f, 0.5f);
            slotRoot.sizeDelta = CaptionSize;
            slotRoot.anchoredPosition = CaptionVerticalOffset;

            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            text.font = labelFont != null ? labelFont : TMP_Settings.defaultFontAsset;
            text.fontSize = 13f;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.alignment = TextAlignmentOptions.Center;
            text.color = CaptionColor;
            text.outlineColor = CaptionShadowColor;
            text.outlineWidth = 0.18f;
            text.raycastTarget = false;

            char[] textBuffer = _slots[index].TextBuffer;
            if (textBuffer == null || textBuffer.Length < CaptionTextCapacity)
                textBuffer = new char[CaptionTextCapacity]; // COLD ALLOC: char[128] - pooled spatial caption TMP buffer - owner: AudioCaptionOverlay

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
                WorldAup = default,
                HasWorldAup = false,
                TextBuffer = textBuffer,
                TextLength = 0,
                TextHash = CaptionHashSeed,
                LastAnchoredPosition = CaptionHiddenPosition,
                LastAlpha = 0f
            };
        }

        private void ResolveViewCamera()
        {
            if (_targetCanvas != null && _targetCanvas.worldCamera != null)
            {
                _viewCamera = _targetCanvas.worldCamera;
                _viewTransform = _viewCamera.transform;
                return;
            }

            SuitHUDV4CanvasOverlay overlay = SuitHUDV4CanvasOverlay.ActiveRuntimeInstance;
            if (overlay != null && overlay.TargetCanvas != null && overlay.TargetCanvas.worldCamera != null)
            {
                _viewCamera = overlay.TargetCanvas.worldCamera;
                _viewTransform = _viewCamera.transform;
                return;
            }

            if (_cachedPlayerContext != null && _cachedPlayerContext.PlayerCamera != null)
            {
                _viewCamera = _cachedPlayerContext.PlayerCamera;
                _viewTransform = _viewCamera.transform;
                return;
            }

            Transform playerTransform = _cachedPlayerContext != null ? _cachedPlayerContext.PlayerTransform : null;
            _viewCamera = null;
            _viewTransform = playerTransform;
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

        private void UpdateSlotPose(ref CaptionSlot slot, in CaptionViewFrame viewFrame)
        {
            if (slot.Root == null || slot.Group == null)
                return;

            Vector2 direction = ResolveScreenDirection(in slot, in viewFrame);
            float radius = math.lerp(RadiusMin, RadiusMax, slot.Intensity);
            Vector2 anchoredPosition = direction * radius + CaptionVerticalOffset;
            if (Vector2DistanceSq(slot.LastAnchoredPosition, anchoredPosition) > CaptionPositionEpsilonSq)
            {
                slot.Root.anchoredPosition = anchoredPosition;
                slot.LastAnchoredPosition = anchoredPosition;
            }

            float remaining01 = 1f - math.saturate(slot.Age / math.max(MinDuration, slot.Duration));
            float alpha = EvaluateCheapQuarterSine01(remaining01);
            if (math.abs(slot.LastAlpha - alpha) > CaptionAlphaEpsilon)
            {
                slot.Group.alpha = alpha;
                slot.LastAlpha = alpha;
            }
        }

        private static float EvaluateCheapQuarterSine01(float value)
        {
            float t = math.saturate(value);
            return t * (2f - t);
        }

        private static float Vector2DistanceSq(Vector2 a, Vector2 b)
        {
            float dx = a.x - b.x;
            float dy = a.y - b.y;
            return (dx * dx) + (dy * dy);
        }

        private CaptionViewFrame ResolveCaptionViewFrame()
        {
            if (_viewTransform == null)
                return default;

            Vector3 viewPosition = _viewTransform.position;
            Quaternion viewRotation = _viewTransform.rotation;
            return new CaptionViewFrame
            {
                Origin = viewPosition,
                OriginAup = ResolveCaptionOriginAup(viewPosition, out bool hasOriginAup),
                Right = (float3)(viewRotation * Vector3.right),
                Up = (float3)(viewRotation * Vector3.up),
                Forward = (float3)(viewRotation * Vector3.forward),
                HasView = 1,
                HasOriginAup = hasOriginAup ? (byte)1 : (byte)0
            };
        }

        private static Vector2 ResolveScreenDirection(in CaptionSlot slot, in CaptionViewFrame viewFrame)
        {
            if (viewFrame.HasView == 0)
                return Vector2.up;

            float3 delta;
            if (viewFrame.HasOriginAup != 0 && slot.HasWorldAup)
            {
                delta = AupPrecisionMath.LocalDeltaFloat3(
                    slot.WorldAup.ToAbsoluteDouble3(),
                    viewFrame.OriginAup.ToAbsoluteDouble3(),
                    float3.zero);
            }
            else
            {
                delta = (float3)(slot.WorldPosition - viewFrame.Origin);
            }
            float localX = math.dot(delta, viewFrame.Right);
            float localY = math.dot(delta, viewFrame.Up);
            float localZ = math.dot(delta, viewFrame.Forward);
            Vector2 planar = new Vector2(localX, localY);
            if (planar.sqrMagnitude < 0.0001f)
                planar = new Vector2(localX >= 0f ? BehindFlipBias : -BehindFlipBias, 1f);

            if (localZ < 0f)
                planar = -planar;

            float magnitudeSq = planar.sqrMagnitude;
            if (magnitudeSq <= 0.0001f)
                return Vector2.up;

            return NormalizePlanarDirectionFast(planar);
        }

        private static Vector2 NormalizePlanarDirectionFast(Vector2 planar)
        {
            float ax = math.abs(planar.x);
            float ay = math.abs(planar.y);
            float maxAxis = math.max(ax, ay);
            if (maxAxis <= 0.0001f)
                return Vector2.up;

            float minAxis = math.min(ax, ay);
            float approximateMagnitude = maxAxis + minAxis * 0.375f;
            return planar / math.max(approximateMagnitude, 0.0001f);
        }

        private AbsoluteUniversePosition ResolveCaptionOriginAup(Vector3 viewPosition, out bool hasOriginAup)
        {
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext != null &&
                playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u)
            {
                hasOriginAup = true;
                return OffsetAupLocal(
                    in movementState.PredictedAup,
                    (Vector3)((float3)viewPosition - movementState.PredictedWorldPosition));
            }

            HectonPlayerMovement movement = playerContext != null ? playerContext.PlayerMovement : null;
            if (movement != null)
            {
                hasOriginAup = true;
                return movement.CurrentAup;
            }

            hasOriginAup = false;
            return default;
        }

        private static AbsoluteUniversePosition OffsetAupLocal(in AbsoluteUniversePosition anchorAup, Vector3 runtimeOffset)
        {
            AbsoluteUniversePosition result = anchorAup;
            result.LocalX += runtimeOffset.x;
            result.LocalY += runtimeOffset.y;
            result.LocalZ += runtimeOffset.z;
            NormalizeAupLocalAxis(ref result.GridX, ref result.LocalX);
            NormalizeAupLocalAxis(ref result.GridY, ref result.LocalY);
            NormalizeAupLocalAxis(ref result.GridZ, ref result.LocalZ);
            return result;
        }

        private static void NormalizeAupLocalAxis(ref long grid, ref float local)
        {
            const float cellSize = AbsoluteUniversePosition.CellSizeMeters;
            if (local >= 0f && local < cellSize)
                return;

            long gridDelta = (long)math.floor(local / cellSize);
            grid += gridDelta;
            local -= gridDelta * cellSize;
            if (local < 0f)
            {
                local += cellSize;
                grid--;
                return;
            }

            if (local >= cellSize)
            {
                local -= cellSize;
                grid++;
            }
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!math.isfinite(runtimePosition.x) || !math.isfinite(runtimePosition.y) || !math.isfinite(runtimePosition.z))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!AbsoluteUniversePosition.IsFinite(in originAup))
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return AbsoluteUniversePosition.IsFinite(in positionAup);
        }

        private void HideAllSlots()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                CaptionSlot slot = _slots[i];
                slot.Active = false;
                ApplySlotHidden(ref slot);
                _slots[i] = slot;
            }

            UnregisterFromTickManager();
        }

        private static void ApplySlotHidden(ref CaptionSlot slot)
        {
            if (slot.Group != null)
            {
                if (slot.Group.alpha > CaptionAlphaEpsilon)
                    slot.Group.alpha = 0f;

                slot.Group.blocksRaycasts = false;
                slot.Group.interactable = false;
            }

            slot.LastAlpha = 0f;
            slot.LastAnchoredPosition = CaptionHiddenPosition;
            slot.WorldPosition = Vector3.zero;
            slot.WorldAup = default;
            slot.HasWorldAup = false;
        }

        private static uint ComputeCaptionDisplayHash(char[] captionText, int displayLength)
        {
            uint hash = CaptionHashSeed;
            for (int i = 0; i < displayLength; i++)
            {
                hash ^= captionText[i];
                hash *= CaptionHashPrime;
            }

            return hash;
        }

        private void CacheRegistryServicesCold()
        {
            _cachedPlayerContext = GlobalRegistry.Player;
        }

        private void RegisterToTickManager()
        {
            if (_tickRegistered || !Application.isPlaying)
                return;

            _tickRegistered = SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI);
        }

        private void TryRegisterCaptionConsumer()
        {
            if (_captionConsumerRegistered || !_uiBuilt || !_tickRegistered || !Application.isPlaying)
                return;

            AudioCaptionEvents.RegisterConsumer();
            _captionConsumerRegistered = true;
        }

        private void TryUnregisterCaptionConsumer()
        {
            if (!_captionConsumerRegistered)
                return;

            AudioCaptionEvents.UnregisterConsumer();
            _captionConsumerRegistered = false;
        }

        private void UnregisterFromTickManager()
        {
            if (!_tickRegistered)
                return;

            SystemDispatcher.UnregisterLateFrameTickableDirect(this, PriorityLayer.UI);
            _tickRegistered = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        private static Canvas ResolveTargetCanvas(bool allowComponentFallback)
        {
            SuitHUDV4CanvasOverlay overlay = SuitHUDV4CanvasOverlay.ActiveRuntimeInstance;
            if (overlay != null && overlay.TargetCanvas != null)
                return overlay.TargetCanvas;

            if (!allowComponentFallback || overlay == null)
                return null;

            overlay.TryGetComponent(out Canvas canvas);
            return canvas;
        }

        private static RectTransform FindExistingChild(Transform parent, string childName)
        {
            return UiChildSpanUtility.FindExistingChild(parent, childName);
        }
    }
}
