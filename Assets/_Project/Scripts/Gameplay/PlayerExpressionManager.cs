using System.Diagnostics;
using Hecton8.Bootstrap;
using Hecton8.SaveSystem;
using Hecton8.UI;
using UnityEngine;
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
#endif

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Static event bus for player expression changes.
    /// </summary>
    public static class PlayerExpressionEvents
    {
        /// <summary>Raised when the active player expression profile changes.</summary>
        public static event System.Action<PlayerExpressionProfile> OnProfileChanged;

        internal static void RaiseProfileChanged(PlayerExpressionProfile profile)
        {
            System.Action<PlayerExpressionProfile> handler = OnProfileChanged;
            handler?.Invoke(profile);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            OnProfileChanged = null;
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-105)]
    [AddComponentMenu("Hecton8/Gameplay/Player Expression Manager")]
    public sealed class PlayerExpressionManager : MonoBehaviour, ISaveable
    {
        private const string ProfileFolder = "Assets/_Project/Data/Customization/PlayerExpression";
        private const string DefaultIdentityName = "STANDARD";
        private const string DefaultIdentitySummary = "No authored player-expression profile is active.";

        private static PlayerExpressionManager _instance;
        private static PlayerExpressionProfile _activeProfile;
        private static SuitHUDProfile _activeHudProfileOverride;
        private static string _activeSuitLabelOverride;

        [Header("── References ─────────────────────────────────")]
        [Tooltip("Live tool manager used when syncing an identity to a recommended quick-slot kit.")]
        [SerializeField] private PlayerToolManager toolManager;

        [Tooltip("Live player movement owner used when syncing an identity to a recommended suit shell.")]
        [SerializeField] private HectonPlayerMovement playerMovement;

        [Tooltip("HUD notifier for user-facing profile switching messages.")]
        [SerializeField] private HUDNotification hudNotification;

        [Header("── Catalog ────────────────────────────────────")]
        [Tooltip("Default profile ID used for first boot or missing save data.")]
        [SerializeField] private string defaultProfileId = "expression.expedition.standard";

        [Tooltip("Authored player-expression catalog.")]
        [SerializeField] private PlayerExpressionProfile[] authoredProfiles = new PlayerExpressionProfile[0];

        [Header("── Behavior ───────────────────────────────────")]
        [Tooltip("Automatically apply the profile's recommended loadout when the identity changes.")]
        [SerializeField] private bool autoApplyRecommendedLoadoutOnSelection;

        [Tooltip("Development logging for profile apply/load behavior.")]
        [SerializeField] private bool verboseLogging;

        [Header("── Diagnostics ────────────────────────────────")]
        [SerializeField] private string _debugActiveProfileId;
        [SerializeField] private string _debugActiveProfileName;
        [SerializeField] private string _debugRecommendedSuitName;
        [SerializeField] private string _debugLiveSuitName;
        [SerializeField] private int _debugProfileCount;

        private bool _runtimeBindingsReady;
        private bool _pendingRecommendedSuitApply;

        /// <summary>Singleton instance for the active scene/runtime.</summary>
        public static PlayerExpressionManager Instance => _instance;

        /// <summary>The currently active expression profile.</summary>
        public static PlayerExpressionProfile ActiveProfile => _activeProfile;

        /// <summary>HUD profile override consumed by live HUD systems.</summary>
        public static SuitHUDProfile ActiveHudProfileOverride => _activeHudProfileOverride;

        /// <summary>HUD suit-label override consumed by live HUD systems.</summary>
        public static string ActiveSuitLabelOverride => _activeSuitLabelOverride;

        /// <summary>Catalog size available for PDA/UI readback.</summary>
        public int ProfileCount => authoredProfiles != null ? authoredProfiles.Length : 0;

        /// <summary>Save priority for the expression profile state.</summary>
        public int SavePriority => 60;

        /// <summary>Load priority for the expression profile state.</summary>
        public int LoadPriority => 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
            _activeProfile = null;
            _activeHudProfileOverride = null;
            _activeSuitLabelOverride = null;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            AutoResolveReferences();

#if UNITY_EDITOR
            AutoResolveCatalog();
#endif

            if (_activeProfile == null)
                ApplyProfileInternal(ResolveInitialProfileIndex(), false, false);

            SyncDiagnostics();
        }

        private void OnEnable()
        {
            AutoResolveReferences();

            if (Hecton8.Core.GlobalRegistry.SaveRuntime != null)
                Hecton8.Core.GlobalRegistry.SaveRuntime.Register(this);
        }

        private void Start()
        {
            AutoResolveReferences();
            _runtimeBindingsReady = true;
            ApplyPendingRuntimeBindings();
            SyncDiagnostics();
        }

        private void OnDisable()
        {
            if (Hecton8.Core.GlobalRegistry.SaveRuntime != null)
                Hecton8.Core.GlobalRegistry.SaveRuntime.Unregister(this);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
                _activeProfile = null;
                _activeHudProfileOverride = null;
                _activeSuitLabelOverride = null;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying)
                return;

            AutoResolveReferences();
            AutoResolveCatalog();
            SyncDiagnostics();
        }

        private void AutoResolveCatalog()
        {
            if (authoredProfiles != null && authoredProfiles.Length > 0)
            {
                bool hasNull = false;
                for (int i = 0; i < authoredProfiles.Length; i++)
                {
                    if (authoredProfiles[i] == null)
                    {
                        hasNull = true;
                        break;
                    }
                }

                if (!hasNull)
                    return;
            }

            string[] guids = AssetDatabase.FindAssets("t:PlayerExpressionProfile", new[] { ProfileFolder });
            if (guids == null || guids.Length == 0)
                return;

            List<PlayerExpressionProfile> profiles = new List<PlayerExpressionProfile>(guids.Length); // COLD ALLOC: List<PlayerExpressionProfile>[guids.Length] — editor catalog sync — owner: PlayerExpressionManager
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                PlayerExpressionProfile profile = AssetDatabase.LoadAssetAtPath<PlayerExpressionProfile>(path);
                if (profile != null)
                    profiles.Add(profile);
            }

            if (profiles.Count == 0)
                return;

            profiles.Sort(CompareProfiles);
            authoredProfiles = profiles.ToArray();
            EditorUtility.SetDirty(this);
        }

        private static int CompareProfiles(PlayerExpressionProfile left, PlayerExpressionProfile right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return 1;
            if (right == null)
                return -1;

            return string.CompareOrdinal(left.ProfileId, right.ProfileId);
        }
#endif

        /// <summary>Returns the currently active profile name.</summary>
        public string GetActiveProfileName()
        {
            return _activeProfile != null && !string.IsNullOrWhiteSpace(_activeProfile.DisplayName)
                ? _activeProfile.DisplayName
                : DefaultIdentityName;
        }

        /// <summary>Returns the currently active profile summary.</summary>
        public string GetActiveProfileSummary()
        {
            return _activeProfile != null && !string.IsNullOrWhiteSpace(_activeProfile.Summary)
                ? _activeProfile.Summary
                : DefaultIdentitySummary;
        }

        /// <summary>Returns the active profile's recommended loadout name, if any.</summary>
        public string GetActiveRecommendedLoadoutName()
        {
            return _activeProfile != null && _activeProfile.RecommendedLoadout != null
                ? _activeProfile.RecommendedLoadout.presetName
                : string.Empty;
        }

        /// <summary>Returns the active profile's recommended suit name, if any.</summary>
        public string GetActiveRecommendedSuitName()
        {
            if (_activeProfile == null || _activeProfile.RecommendedSuit == null)
                return string.Empty;

            return _activeProfile.RecommendedSuit.name.Replace('_', ' ');
        }

        /// <summary>Returns the currently applied live suit name, if any.</summary>
        public string GetLiveSuitName()
        {
            AutoResolveReferences();
            return playerMovement != null && playerMovement.CurrentSuit != null
                ? playerMovement.CurrentSuit.name.Replace('_', ' ')
                : string.Empty;
        }

        /// <summary>Returns true when the active profile's recommended suit is live on the player.</summary>
        public bool IsActiveRecommendedSuitApplied()
        {
            if (_activeProfile == null || _activeProfile.RecommendedSuit == null)
                return false;

            AutoResolveReferences();
            return playerMovement != null && ReferenceEquals(playerMovement.CurrentSuit, _activeProfile.RecommendedSuit);
        }

        /// <summary>Returns the index of the active profile in the authored catalog.</summary>
        public int GetActiveProfileIndex()
        {
            if (_activeProfile == null || authoredProfiles == null)
                return -1;

            for (int i = 0; i < authoredProfiles.Length; i++)
            {
                if (ReferenceEquals(authoredProfiles[i], _activeProfile))
                    return i;
            }

            return -1;
        }

        /// <summary>Returns the next valid profile index for PDA cycling.</summary>
        public int GetNextProfileIndex()
        {
            if (authoredProfiles == null || authoredProfiles.Length == 0)
                return -1;

            int activeIndex = GetActiveProfileIndex();
            if (activeIndex < 0)
                return FindFirstValidProfileIndex();

            for (int step = 1; step <= authoredProfiles.Length; step++)
            {
                int index = (activeIndex + step) % authoredProfiles.Length;
                if (authoredProfiles[index] != null)
                    return index;
            }

            return -1;
        }

        /// <summary>Returns the authored profile at the requested index.</summary>
        public PlayerExpressionProfile GetProfile(int index)
        {
            if (authoredProfiles == null || index < 0 || index >= authoredProfiles.Length)
                return null;

            return authoredProfiles[index];
        }

        /// <summary>Cycles to the next authored profile.</summary>
        public bool CycleNextProfile(bool applyRecommendedLoadout = false)
        {
            int nextIndex = GetNextProfileIndex();
            return ApplyProfileInternal(nextIndex, applyRecommendedLoadout, true);
        }

        /// <summary>Applies the requested profile from the authored catalog.</summary>
        public bool ApplyProfileAt(int index, bool applyRecommendedLoadout = false)
        {
            return ApplyProfileInternal(index, applyRecommendedLoadout, true);
        }

        /// <summary>Applies the active profile's recommended loadout, if authored.</summary>
        public bool ApplyRecommendedLoadoutForActiveProfile()
        {
            if (_activeProfile == null || _activeProfile.RecommendedLoadout == null || toolManager == null)
                return false;

            bool applied = toolManager.ApplyLoadoutPreset(_activeProfile.RecommendedLoadout, holsterFirst: true);
            if (!applied)
                return false;

            NotifyInfo($"IDENTITY KIT APPLIED - {ToUpperFast(_activeProfile.RecommendedLoadout.presetName)}");
            return true;
        }

        /// <summary>Applies the active profile's recommended suit shell, if authored.</summary>
        public bool ApplyRecommendedSuitForActiveProfile()
        {
            if (_activeProfile == null)
                return false;

            bool applied = TryApplyRecommendedSuit(_activeProfile);
            if (!applied)
                return false;

            string suitName = GetActiveRecommendedSuitName();
            if (!string.IsNullOrWhiteSpace(suitName))
                NotifyInfo($"SUIT SHELL APPLIED - {ToUpperFast(suitName)}");

            return true;
        }

        /// <summary>Writes the active identity selection into save data.</summary>
        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            data.playerExpressionProfileId = _activeProfile != null
                ? _activeProfile.ProfileId
                : string.Empty;
        }

        /// <summary>Restores the active identity selection from save data.</summary>
        public void LoadFromSaveData(SaveData data)
        {
            string requestedId = data != null ? data.playerExpressionProfileId : string.Empty;
            int profileIndex = ResolveProfileIndexForLoad(requestedId);
            ApplyProfileInternal(profileIndex, false, false);
        }

        private void AutoResolveReferences()
        {
            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                playerTransform != null)
            {
                if (toolManager == null)
                    toolManager = ((Hecton8.Core.GlobalRegistry.Player != null && Hecton8.Core.GlobalRegistry.Player.ToolManager != null) ? Hecton8.Core.GlobalRegistry.Player.ToolManager : playerTransform.GetComponent<PlayerToolManager>());

                if (playerMovement == null)
                {
                    playerMovement = playerTransform.GetComponent<HectonPlayerMovement>();
                    if (playerMovement == null)
                        playerMovement = ((Hecton8.Core.GlobalRegistry.Player != null && Hecton8.Core.GlobalRegistry.Player.PlayerMovement != null) ? Hecton8.Core.GlobalRegistry.Player.PlayerMovement : playerTransform.GetComponent<HectonPlayerMovement>());
                }
            }

            if (hudNotification == null)
                HUDNotification.TryGetActive(out hudNotification);
        }

        private int ResolveInitialProfileIndex()
        {
            int configuredIndex = FindProfileIndexById(defaultProfileId);
            if (configuredIndex >= 0)
                return configuredIndex;

            return FindFirstValidProfileIndex();
        }

        private int ResolveProfileIndexForLoad(string requestedId)
        {
            int requestedIndex = FindProfileIndexById(requestedId);
            if (requestedIndex >= 0)
                return requestedIndex;

            return ResolveInitialProfileIndex();
        }

        private int FindFirstValidProfileIndex()
        {
            if (authoredProfiles == null)
                return -1;

            for (int i = 0; i < authoredProfiles.Length; i++)
            {
                if (authoredProfiles[i] != null)
                    return i;
            }

            return -1;
        }

        private int FindProfileIndexById(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId) || authoredProfiles == null)
                return -1;

            for (int i = 0; i < authoredProfiles.Length; i++)
            {
                PlayerExpressionProfile profile = authoredProfiles[i];
                if (profile == null || string.IsNullOrWhiteSpace(profile.ProfileId))
                    continue;

                if (string.Equals(profile.ProfileId, profileId, System.StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        private bool ApplyProfileInternal(int index, bool applyRecommendedLoadout, bool userFacingNotification)
        {
            if (index < 0 || authoredProfiles == null || index >= authoredProfiles.Length)
                return false;

            PlayerExpressionProfile profile = authoredProfiles[index];
            if (profile == null)
                return false;

            _activeProfile = profile;
            _activeHudProfileOverride = profile.HudProfile;
            _activeSuitLabelOverride = string.IsNullOrWhiteSpace(profile.HudLabelOverride)
                ? string.Empty
                : profile.HudLabelOverride;

            if ((applyRecommendedLoadout || autoApplyRecommendedLoadoutOnSelection) &&
                profile.RecommendedLoadout != null &&
                toolManager != null)
            {
                toolManager.ApplyLoadoutPreset(profile.RecommendedLoadout, holsterFirst: true);
            }

            if (_runtimeBindingsReady)
                _pendingRecommendedSuitApply = !TryApplyRecommendedSuit(profile);
            else
                _pendingRecommendedSuitApply = profile.RecommendedSuit != null;

            SyncDiagnostics();
            PlayerExpressionEvents.RaiseProfileChanged(profile);

            if (userFacingNotification)
                NotifyInfo($"SUIT IDENTITY ACTIVE - {ToUpperFast(profile.DisplayName)}");

            LogProfileApplied(profile.ProfileId, profile.DisplayName);
            return true;
        }

        private void SyncDiagnostics()
        {
            _debugActiveProfileId = _activeProfile != null ? _activeProfile.ProfileId : string.Empty;
            _debugActiveProfileName = _activeProfile != null ? _activeProfile.DisplayName : string.Empty;
            _debugRecommendedSuitName = GetActiveRecommendedSuitName();
            _debugLiveSuitName = GetLiveSuitName();
            _debugProfileCount = ProfileCount;
        }

        private void ApplyPendingRuntimeBindings()
        {
            if (!_runtimeBindingsReady || _activeProfile == null)
                return;

            if (_activeProfile.RecommendedSuit == null)
            {
                _pendingRecommendedSuitApply = false;
                SyncDiagnostics();
                return;
            }

            _pendingRecommendedSuitApply = !TryApplyRecommendedSuit(_activeProfile);
            SyncDiagnostics();
        }

        private bool TryApplyRecommendedSuit(PlayerExpressionProfile profile)
        {
            if (profile == null || profile.RecommendedSuit == null)
            {
                _pendingRecommendedSuitApply = false;
                return false;
            }

            AutoResolveReferences();
            if (playerMovement == null)
            {
                _pendingRecommendedSuitApply = true;
                return false;
            }

            if (!ReferenceEquals(playerMovement.CurrentSuit, profile.RecommendedSuit))
            {
                playerMovement.SetSuit(profile.RecommendedSuit);
                LogSuitApplied(profile.ProfileId, profile.RecommendedSuit.name);
            }

            _pendingRecommendedSuitApply = false;
            return true;
        }

        private void NotifyInfo(string message)
        {
            if (hudNotification == null)
                HUDNotification.TryGetActive(out hudNotification);

            if (hudNotification != null)
                hudNotification.ShowInfo(message);
        }

        private static string ToUpperFast(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? DefaultIdentityName
                : value.ToUpperInvariant();
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private void LogProfileApplied(string profileId, string displayName)
        {
            if (!verboseLogging)
                return;

            UnityEngine.Debug.Log($"[PlayerExpression] Active profile: {profileId} ({displayName})", this);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private void LogSuitApplied(string profileId, string suitName)
        {
            if (!verboseLogging)
                return;

            UnityEngine.Debug.Log($"[PlayerExpression] Suit applied: {profileId} -> {suitName}", this);
        }
    }
}
