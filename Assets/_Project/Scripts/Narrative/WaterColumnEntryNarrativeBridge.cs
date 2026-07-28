using System;
using Hecton8.Core;
using Hecton8.Core.Generated;
using UnityEngine;

namespace Hecton8.Narrative
{
    /// <summary>
    /// Raises the authored <c>first_hour_exit_lifepod</c> narrative discovery from observed player
    /// submersion, so the first-hour quest chain has a producer that does not require a base airlock.
    /// </summary>
    /// <remarks>
    /// Why this exists. Every entry edge of the mission spine hangs off one discovery hash:
    /// Quest_Arrival and Quest_FirstHour_ExitLifePod both complete on <c>completionType: 3</c>
    /// (OnDiscoveryMade) with <c>completionId: first_hour_exit_lifepod</c>, and Quest_StarterDrill,
    /// Quest_CopperSample and Quest_FirstHour_CollectTitanium all activate on the same id as their
    /// <c>triggerId</c>. The only producer of that discovery in the project is
    /// NarrativeProgressionBridge.TryIssueExitLifePodDiscoveryFromAup, and its only caller is that
    /// class's OnBaseAirlockEvent - so it needs a BaseAirlock to raise a
    /// BaseAirlockEventType.EnvironmentChanged event. BaseAirlock's script GUID
    /// (6617cbca100e19646bb6299390f3c6e0) is in zero scenes and zero prefabs, and no life-pod or
    /// drop-pod prefab exists either, so that edge cannot fire. The spine armed two quests at Start
    /// and then stopped forever.
    ///
    /// This bridge is not a substitute trigger for the pod. It observes the second half of the
    /// authored objective text ("enter the water column") against live player physics state and does
    /// nothing at all when the player is dry: the discovery is issued only from the Underwater flag
    /// plus a real depth reading, sustained across consecutive samples so a single surface-slap frame
    /// cannot arm it. When a pod airlock does land, that route wins - HasDiscovery short-circuits this
    /// one, and the discovery hash is identical either way.
    ///
    /// Installed by NarrativeRuntimeInstaller.EnsurePlayerSystems, which GameBootstrapper already
    /// calls inside its scene-runtime publication gate, so no new bootstrap lane is required.
    /// </remarks>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Narrative/Water Column Entry Narrative Bridge")]
    public sealed class WaterColumnEntryNarrativeBridge : MonoBehaviour, ISlowTickable, IGlobalRegistryHotSwapListener
    {
        private const string WaterColumnEntryLogPrefix = "H8QUESTSPINE WATERCOLUMN issued depthM=";
        private const int WaterColumnLogBufferCapacity = 96;
        private const int InstalledOwnerCapacity = 4;

        private static readonly uint _waterColumnEntryDiscoveryHash =
            NarrativeEvents.ComputeDiscoveryHash(H8Hashes.Signals.FirstHourExitLifepodId);
        private static readonly uint _waterColumnRaiseDropWarningHash =
            unchecked((uint)Hecton.Localization.LocHash.Compute("WaterColumnEntryNarrativeBridge.DiscoveryRaiseDrop"));
        private static readonly uint _waterColumnContextHash =
            unchecked((uint)Hecton.Localization.LocHash.Compute("WaterColumnEntryNarrativeBridge"));
        private static readonly uint _requiredMovementFlags = (uint)(PlayerRuntimeSnapshotFlags.HasPlayerRoot |
                                                                    PlayerRuntimeSnapshotFlags.PlayerAlive |
                                                                    PlayerRuntimeSnapshotFlags.Underwater);

        // COLD ALLOC: GameObject[4] - installed owner identity for the cold installer duplicate guard - owner: WaterColumnEntryNarrativeBridge
        private static readonly GameObject[] s_installedOwners = new GameObject[InstalledOwnerCapacity];
        // COLD ALLOC: WaterColumnEntryNarrativeBridge[4] - installed instances paired with s_installedOwners - owner: WaterColumnEntryNarrativeBridge
        private static readonly WaterColumnEntryNarrativeBridge[] s_installedInstances =
            new WaterColumnEntryNarrativeBridge[InstalledOwnerCapacity];
        private static int s_installedCount;
        private static bool s_waterColumnEntryIssued;
        private static int s_waterColumnSubmergedSampleTotal;
        private static int s_waterColumnRaiseDropCount;
        private static float s_waterColumnLastObservedDepthMeters;

        [Header("Water Column Entry")]
        [Tooltip("Depth below the surface, in metres, that counts as being inside the water column rather than bobbing on it.")]
        [SerializeField, Min(0f)] private float entryDepthMeters = 2f;

        [Tooltip("Consecutive submerged samples required before the discovery is issued. Rejects a single-frame surface reading.")]
        [SerializeField, Range(1, 32)] private int requiredConsecutiveSamples = 2;

        private IPlayerRuntimeContext _playerContext;
        private INarrativeDiscoveryReadModel _narrativeDiscovery;
        private int _consecutiveSubmergedSamples;
        // Instance-scoped on purpose. The static below is a session-wide diagnostic only. Gating on a
        // static latch would leave the spine unarmed after an in-session scene reload, because a fresh
        // QuestManager rebuilds its state graph from scratch while a static latch would stay closed.
        private bool _entryIssued;
        private bool _registeredToTick;
        private bool _registeredHotSwapListener;

        /// <summary>
        /// True once the water column discovery has been issued or observed from any producer this session.
        /// </summary>
        public static bool WaterColumnEntryIssued => s_waterColumnEntryIssued;

        /// <summary>
        /// Total submerged samples this session's bridges observed. Zero means the player was never in the water column.
        /// </summary>
        public static int WaterColumnSubmergedSampleTotal => s_waterColumnSubmergedSampleTotal;

        /// <summary>
        /// Times the narrative event lane refused the discovery raise because it was back-pressured.
        /// </summary>
        public static int WaterColumnRaiseDropCount => s_waterColumnRaiseDropCount;

        /// <summary>
        /// Last player depth this bridge read, in metres. Stays zero when no movement snapshot is published.
        /// </summary>
        public static float WaterColumnLastObservedDepthMeters => s_waterColumnLastObservedDepthMeters;

        /// <summary>
        /// Stable discovery hash this bridge publishes. Identical to the base-airlock route's hash.
        /// </summary>
        public static uint WaterColumnEntryDiscoveryHash => _waterColumnEntryDiscoveryHash;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            for (int i = 0; i < s_installedOwners.Length; i++)
            {
                s_installedOwners[i] = null;
                s_installedInstances[i] = null;
            }

            s_installedCount = 0;
            s_waterColumnEntryIssued = false;
            s_waterColumnSubmergedSampleTotal = 0;
            s_waterColumnRaiseDropCount = 0;
            s_waterColumnLastObservedDepthMeters = 0f;
        }

        /// <summary>
        /// True when this owner already carries a live bridge, without a component lookup.
        /// </summary>
        /// <param name="owner">Candidate owner object.</param>
        internal static bool IsInstalledOn(GameObject owner)
        {
            if (owner == null)
                return false;

            for (int i = 0; i < s_installedCount; i++)
            {
                if (ReferenceEquals(s_installedOwners[i], owner))
                    return true;
            }

            return false;
        }

        private void OnEnable()
        {
            RegisterInstalledOwner();
            TryRegisterHotSwapListener();
            RefreshCachedRuntimeServices();
            AdoptExistingDiscoveryState();
            TryRegisterWithTickManager();
        }

        // Repeated from OnEnable so an authored scene placement still reaches the tick lane. AddComponent
        // from NarrativeRuntimeInstaller runs OnEnable at the installer's call site, long after the
        // dispatcher exists, but a scene-authored copy would hit OnEnable before it and the
        // GlobalRegistry.Dispatcher guard would drop the registration with no retry. Same shape as
        // ProceduralLoreDirector.cs:108-116. Every call below is idempotent.
        private void Start()
        {
            RegisterInstalledOwner();
            TryRegisterHotSwapListener();
            RefreshCachedRuntimeServices();
            AdoptExistingDiscoveryState();
            TryRegisterWithTickManager();
        }

        private void OnDisable()
        {
            UnregisterInstalledOwner();
            UnregisterFromTickManager();
            TryUnregisterHotSwapListener();
            ClearCachedRuntimeServices();
            _consecutiveSubmergedSamples = 0;
        }

        private void OnDestroy()
        {
            UnregisterInstalledOwner();
            UnregisterFromTickManager();
            TryUnregisterHotSwapListener();
            ClearCachedRuntimeServices();
            _consecutiveSubmergedSamples = 0;
        }

        /// <inheritdoc />
        public void SlowTick()
        {
            if (_entryIssued || AdoptExistingDiscoveryState())
            {
                UnregisterFromTickManager();
                return;
            }

            if (!TryReadSubmergedDepth(out float depthMeters))
            {
                _consecutiveSubmergedSamples = 0;
                return;
            }

            s_waterColumnLastObservedDepthMeters = depthMeters;
            s_waterColumnSubmergedSampleTotal++;
            _consecutiveSubmergedSamples++;
            if (_consecutiveSubmergedSamples < requiredConsecutiveSamples)
                return;

            // A refused raise is a back-pressured lane, not a state change. The latch stays open so the
            // next slow tick retries; latching here would lose the keystone edge to one full queue.
            if (!NarrativeEvents.TryRaiseDiscoveryMade(_waterColumnEntryDiscoveryHash))
            {
                ReportDiscoveryRaiseDrop();
                return;
            }

            _entryIssued = true;
            s_waterColumnEntryIssued = true;
            UnregisterFromTickManager();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogWaterColumnEntryIssued(depthMeters);
#endif
        }

        /// <summary>
        /// Reads the live player movement snapshot and reports depth only while genuinely submerged.
        /// </summary>
        /// <param name="depthMeters">Finite depth below the surface when the read succeeds.</param>
        /// <returns>True when the player is alive, rooted, underwater and past the entry depth.</returns>
        private bool TryReadSubmergedDepth(out float depthMeters)
        {
            depthMeters = 0f;

            IPlayerRuntimeContext playerContext = ResolvePlayerContext();
            if (playerContext == null || !playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState state))
                return false;

            uint requiredFlags = _requiredMovementFlags;
            if ((state.Flags & requiredFlags) != requiredFlags)
                return false;

            float depth = state.DepthMeters;
            if (!float.IsFinite(depth) || depth < entryDepthMeters)
                return false;

            depthMeters = depth;
            return true;
        }

        /// <summary>
        /// Latches on a discovery that another producer already published so this bridge yields to it.
        /// </summary>
        /// <returns>True when the discovery is already recorded.</returns>
        private bool AdoptExistingDiscoveryState()
        {
            if (_entryIssued)
                return true;

            INarrativeDiscoveryReadModel narrativeDiscovery = ResolveNarrativeDiscoveryReadModel();
            if (narrativeDiscovery == null || !narrativeDiscovery.HasDiscovery(_waterColumnEntryDiscoveryHash))
                return false;

            _entryIssued = true;
            s_waterColumnEntryIssued = true;
            return true;
        }

        /// <inheritdoc />
        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Player:
                    _playerContext = currentService as IPlayerRuntimeContext;
                    break;
                case GlobalRegistryServiceSlot.NarrativeDirectorRuntime:
                    _narrativeDiscovery = currentService as INarrativeDiscoveryReadModel;
                    break;
            }
        }

        private IPlayerRuntimeContext ResolvePlayerContext()
        {
            IPlayerRuntimeContext playerContext = _playerContext;
            if (playerContext != null)
                return playerContext;

            playerContext = GlobalRegistry.Player;
            _playerContext = playerContext;
            return playerContext;
        }

        private INarrativeDiscoveryReadModel ResolveNarrativeDiscoveryReadModel()
        {
            INarrativeDiscoveryReadModel narrativeDiscovery = _narrativeDiscovery;
            if (narrativeDiscovery != null)
                return narrativeDiscovery;

            narrativeDiscovery = GlobalRegistry.NarrativeDiscoveryReadModel;
            _narrativeDiscovery = narrativeDiscovery;
            return narrativeDiscovery;
        }

        private void RefreshCachedRuntimeServices()
        {
            _playerContext = GlobalRegistry.Player;
            _narrativeDiscovery = GlobalRegistry.NarrativeDiscoveryReadModel;
        }

        private void ClearCachedRuntimeServices()
        {
            _playerContext = null;
            _narrativeDiscovery = null;
        }

        private void TryRegisterWithTickManager()
        {
            if (_registeredToTick || _entryIssued)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredToTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Player);
        }

        private void UnregisterFromTickManager()
        {
            if (!_registeredToTick)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Player);
            _registeredToTick = false;
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

        private void RegisterInstalledOwner()
        {
            GameObject owner = gameObject;
            for (int i = 0; i < s_installedCount; i++)
            {
                if (ReferenceEquals(s_installedInstances[i], this))
                {
                    s_installedOwners[i] = owner;
                    return;
                }

                if (ReferenceEquals(s_installedOwners[i], owner))
                {
                    s_installedInstances[i] = this;
                    return;
                }
            }

            if (s_installedCount >= InstalledOwnerCapacity)
                return;

            s_installedOwners[s_installedCount] = owner;
            s_installedInstances[s_installedCount] = this;
            s_installedCount++;
        }

        private void UnregisterInstalledOwner()
        {
            for (int i = 0; i < s_installedCount; i++)
            {
                if (!ReferenceEquals(s_installedInstances[i], this))
                    continue;

                s_installedCount--;
                s_installedOwners[i] = s_installedOwners[s_installedCount];
                s_installedInstances[i] = s_installedInstances[s_installedCount];
                s_installedOwners[s_installedCount] = null;
                s_installedInstances[s_installedCount] = null;
                return;
            }
        }

        private static void ReportDiscoveryRaiseDrop()
        {
            s_waterColumnRaiseDropCount++;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _waterColumnRaiseDropWarningHash,
                _waterColumnContextHash,
                s_waterColumnRaiseDropCount);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // COLD ALLOC: char[96] - development-only single-shot water column log line staging - owner: WaterColumnEntryNarrativeBridge
        private static readonly char[] s_waterColumnLogBuffer = new char[WaterColumnLogBufferCapacity];

        /// <summary>
        /// Emits the one development-only line proving which route armed the mission spine.
        /// </summary>
        /// <param name="depthMeters">Depth the discovery was issued at.</param>
        private void LogWaterColumnEntryIssued(float depthMeters)
        {
            int length = 0;
            ReadOnlySpan<char> prefix = WaterColumnEntryLogPrefix.AsSpan();
            if (length + prefix.Length > s_waterColumnLogBuffer.Length)
                return;

            prefix.CopyTo(s_waterColumnLogBuffer.AsSpan(length, prefix.Length));
            length += prefix.Length;

            int wholeMetres = (int)depthMeters;
            if (wholeMetres < 0)
                wholeMetres = 0;

            int digitStart = length;
            do
            {
                if (length >= s_waterColumnLogBuffer.Length)
                    return;

                s_waterColumnLogBuffer[length++] = (char)('0' + (wholeMetres % 10));
                wholeMetres /= 10;
            }
            while (wholeMetres != 0);

            for (int low = digitStart, high = length - 1; low < high; low++, high--)
            {
                char swap = s_waterColumnLogBuffer[low];
                s_waterColumnLogBuffer[low] = s_waterColumnLogBuffer[high];
                s_waterColumnLogBuffer[high] = swap;
            }

            // COLD ALLOC: string[1] - exactly one line per session, the bridge is one-shot - owner: WaterColumnEntryNarrativeBridge
            H8Debug.Log(new string(s_waterColumnLogBuffer, 0, length), this);
        }
#endif
    }
}
