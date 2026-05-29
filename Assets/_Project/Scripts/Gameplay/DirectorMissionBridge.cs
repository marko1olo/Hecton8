// ============================================================================
// HECTON-8 — DirectorMissionBridge.cs
// Most mezhdu HectonDirectorAI i MissionManager.
//
// ROL:
//   • Slushaet DirectorAIEvents mission trigger lane.
//   • Pri poluchenii sobytiya — aktiviruet sluchaynuyu dostupnuyu missiyu.
//   • Slushaet DirectorAIEvents rare discovery lane.
//   • Pri poluchenii — registriruet discovery cherez NarrativeEvents.
//
// ARHITEKTURA:
//   • Ne ITickable — tolko event subscriptions.
//   • Naznachit na tot zhe GameObject chto i HectonDirectorAI.
// ============================================================================

using System;
using Hecton8.Core;
using Hecton8.Systems.AI;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Director Mission Bridge")]
    public sealed class DirectorMissionBridge : MonoBehaviour, IDirectorAIEventListener, IGlobalRegistryHotSwapListener
    {
        [Header("── Profile ─────────────────────────────")]
        [Tooltip("Designer-authored mission weights, cooldowns, discovery id, and first-hour gates. Legacy fields below are fallback only.")]
        [SerializeField] private DirectorMissionBridgeProfile missionProfile;

        [Header("── Mission IDs ─────────────────────────────")]
        [Tooltip("ID missiy kotorye Director mozhet aktivirovat sluchayno.")]
        [SerializeField] private string[] directorMissionIds = new string[0];
        [SerializeField, HideInInspector] private int legacyValidationInvalidMissionCount;
        [SerializeField, HideInInspector] private int legacyValidationDuplicateMissionCount;
        [SerializeField, HideInInspector] private int legacyValidationFirstInvalidMissionIndex = -1;
        [SerializeField, HideInInspector] private int legacyValidationFirstDuplicateMissionIndex = -1;
        [SerializeField, HideInInspector] private int legacyValidationRuntimeMissionCount;

        [Tooltip("ID discovery dlya rare discovery sobytiya.")]
        [SerializeField] private string rareDiscoveryId = "director_rare_discovery";

        [Header("── Early-Game Gate ─────────────────────────")]
        [Tooltip("Do not let director-side missions compete with the early onboarding spine before this milestone is reached.")]
        [SerializeField] private FirstHourMilestone minimumMilestone = FirstHourMilestone.FirstCraft;

        private int _lastMissionIndex;
        private uint _rareDiscoveryHash;
        private int[] _profileWeightedMissionIndices;
        private float[] _profileMissionCooldownUntil;
        private DirectorMissionBridgeProfile _cachedProfile;
        private int _profileWeightedMissionCount;
        private int _profileRuntimeMissionCount;
        private bool _hotSwapRegistered;
        private IQuestSystem _missionManager;
        private IFirstHourReadModel _firstHourDirector;

        public int LegacyValidationInvalidMissionCount => legacyValidationInvalidMissionCount;
        public int LegacyValidationDuplicateMissionCount => legacyValidationDuplicateMissionCount;
        public int LegacyValidationFirstInvalidMissionIndex => legacyValidationFirstInvalidMissionIndex;
        public int LegacyValidationFirstDuplicateMissionIndex => legacyValidationFirstDuplicateMissionIndex;
        public int LegacyValidationRuntimeMissionCount => legacyValidationRuntimeMissionCount;

        private void OnEnable()
        {
            RebuildLegacyValidationStateCold();
            RebuildProfileRuntimeStateCold();
            RefreshRareDiscoveryHash();
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            DirectorAIEvents.Register(this);
        }

        private void OnDisable()
        {
            DirectorAIEvents.Unregister(this);
            TryUnregisterHotSwapListener();
        }

        private void OnDestroy()
        {
            DirectorAIEvents.Unregister(this);
            TryUnregisterHotSwapListener();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            RebuildLegacyValidationStateCold();
            RebuildProfileRuntimeStateCold();
            RefreshRareDiscoveryHash();
        }
#endif

        private void HandleMissionTrigger(Vector3 position)
        {
            if (!CanServeDirectorContent(ResolveMinimumMilestone()))
                return;

            if (missionProfile != null && missionProfile.MissionCount > 0)
            {
                HandleProfileMissionTrigger(position);
                return;
            }

            if (directorMissionIds == null || directorMissionIds.Length == 0)
                return;

            IQuestSystem mm = _missionManager;
            if (mm == null) return;

            // Cycle through configured missions.
            for (int i = 0; i < directorMissionIds.Length; i++)
            {
                int idx = (_lastMissionIndex + i) % directorMissionIds.Length;
                string missionId = directorMissionIds[idx];

                if (string.IsNullOrEmpty(missionId)) continue;
                if (HasLegacyDuplicateMissionBefore(missionId, idx)) continue;
                if (mm.IsCompleted(missionId)) continue;
                if (mm.IsActive(missionId)) continue;

                mm.ActivateQuest(missionId);
                if (!mm.IsActive(missionId))
                    continue;

                _lastMissionIndex = (idx + 1) % directorMissionIds.Length;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                H8Debug.Log($"[DirectorBridge] Mission triggered: {missionId} near {position}");
#endif
                return;
            }
        }

        private void HandleRareDiscovery(Vector3 position)
        {
            if (!CanServeDirectorContent(ResolveMinimumMilestone()))
                return;

            if (_rareDiscoveryHash != 0u)
                NarrativeEvents.TryRaiseDiscoveryMade(_rareDiscoveryHash);
        }

        private void RefreshRareDiscoveryHash()
        {
            string discoveryId = missionProfile != null ? missionProfile.RareDiscoveryId : rareDiscoveryId;
            _rareDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(discoveryId);
        }

        void IDirectorAIEventListener.OnDirectorSpawnHordeRequested(Vector3 position)
        {
        }

        void IDirectorAIEventListener.OnDirectorEquipmentGlitchRequested(float intensity)
        {
        }

        void IDirectorAIEventListener.OnDirectorRareDiscoveryRequested(Vector3 position)
        {
            HandleRareDiscovery(position);
        }

        void IDirectorAIEventListener.OnDirectorWeatherShiftRequested(float intensity)
        {
        }

        void IDirectorAIEventListener.OnDirectorMissionTriggerRequested(Vector3 position)
        {
            HandleMissionTrigger(position);
        }

        void IDirectorAIEventListener.OnDirectorPredatorPressureChanged(bool pressureEnabled)
        {
        }

        void IDirectorAIEventListener.OnDirectorThreatSpike(Vector3 position, float intensity)
        {
        }

        private void HandleProfileMissionTrigger(Vector3 position)
        {
            DirectorMissionBridgeProfile profile = missionProfile;
            if (profile == null)
                return;

            if (!HasProfileRuntimeState(profile))
                return;

            int totalWeight = _profileWeightedMissionCount;
            if (totalWeight <= 0)
                return;

            IQuestSystem mm = _missionManager;
            if (mm == null)
                return;

            float now = Time.time;
            for (int i = 0; i < totalWeight; i++)
            {
                int weightedIndex = (_lastMissionIndex + i) % totalWeight;
                int missionIndex = _profileWeightedMissionIndices[weightedIndex];
                if (!profile.TryGetRuntimeMission(missionIndex, out DirectorMissionBridgeProfile.MissionEntry entry))
                    continue;

                if (_profileMissionCooldownUntil != null &&
                    (uint)missionIndex < (uint)_profileMissionCooldownUntil.Length &&
                    now < _profileMissionCooldownUntil[missionIndex])
                {
                    continue;
                }

                if (!CanServeDirectorContent(entry.MinimumMilestone))
                    continue;

                string missionId = entry.MissionId;
                if (string.IsNullOrEmpty(missionId)) continue;
                if (mm.IsCompleted(missionId)) continue;
                if (mm.IsActive(missionId)) continue;

                mm.ActivateQuest(missionId);
                if (!mm.IsActive(missionId))
                    continue;

                _lastMissionIndex = (weightedIndex + 1) % totalWeight;
                if (_profileMissionCooldownUntil != null &&
                    (uint)missionIndex < (uint)_profileMissionCooldownUntil.Length)
                {
                    _profileMissionCooldownUntil[missionIndex] = now + entry.CooldownSeconds;
                }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                H8Debug.Log($"[DirectorBridge] Profile mission triggered: {missionId} near {position}");
#endif
                return;
            }
        }

        private void RebuildLegacyValidationStateCold()
        {
            legacyValidationInvalidMissionCount = 0;
            legacyValidationDuplicateMissionCount = 0;
            legacyValidationFirstInvalidMissionIndex = -1;
            legacyValidationFirstDuplicateMissionIndex = -1;
            legacyValidationRuntimeMissionCount = 0;

            if (directorMissionIds == null)
                return;

            for (int i = 0; i < directorMissionIds.Length; i++)
            {
                string missionId = directorMissionIds[i];
                if (string.IsNullOrWhiteSpace(missionId))
                {
                    legacyValidationInvalidMissionCount++;
                    if (legacyValidationFirstInvalidMissionIndex < 0)
                        legacyValidationFirstInvalidMissionIndex = i;

                    continue;
                }

                if (HasLegacyDuplicateMissionBefore(missionId, i))
                {
                    legacyValidationDuplicateMissionCount++;
                    if (legacyValidationFirstDuplicateMissionIndex < 0)
                        legacyValidationFirstDuplicateMissionIndex = i;

                    continue;
                }

                legacyValidationRuntimeMissionCount++;
            }
        }

        private bool HasLegacyDuplicateMissionBefore(string missionId, int index)
        {
            if (directorMissionIds == null || string.IsNullOrWhiteSpace(missionId))
                return false;

            for (int i = 0; i < index; i++)
            {
                if (string.Equals(directorMissionIds[i], missionId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private FirstHourMilestone ResolveMinimumMilestone()
        {
            return missionProfile != null ? missionProfile.MinimumMilestone : minimumMilestone;
        }

        private bool CanServeDirectorContent(FirstHourMilestone gate)
        {
            IFirstHourReadModel firstHourDirector = _firstHourDirector;
            if (firstHourDirector == null)
                return true;

            return firstHourDirector.IsFirstHourMilestoneComplete((int)gate);
        }

        private void RebuildProfileRuntimeStateCold()
        {
            DirectorMissionBridgeProfile profile = missionProfile;
            int missionCount = profile != null ? profile.RuntimeMissionCount : 0;
            if (profile == null || missionCount <= 0)
            {
                _cachedProfile = null;
                _profileRuntimeMissionCount = 0;
                _profileWeightedMissionCount = 0;
                _profileMissionCooldownUntil = null;
                return;
            }

            if (_profileMissionCooldownUntil == null ||
                _profileMissionCooldownUntil.Length < DirectorMissionBridgeProfile.MaxRuntimeMissions)
            {
                _profileMissionCooldownUntil = new float[DirectorMissionBridgeProfile.MaxRuntimeMissions]; // COLD ALLOC: fixed profile mission cooldown clock - owner: DirectorMissionBridge
            }
            else
            {
                Array.Clear(_profileMissionCooldownUntil, 0, _profileMissionCooldownUntil.Length);
            }

            if (_profileWeightedMissionIndices == null ||
                _profileWeightedMissionIndices.Length < DirectorMissionBridgeProfile.MaxRuntimeTotalWeight)
            {
                _profileWeightedMissionIndices = new int[DirectorMissionBridgeProfile.MaxRuntimeTotalWeight]; // COLD ALLOC: fixed weighted mission lookup table - owner: DirectorMissionBridge
            }

            _cachedProfile = profile;
            _profileRuntimeMissionCount = missionCount;
            _profileWeightedMissionCount = profile.TryBuildWeightedMissionIndexTable(
                _profileWeightedMissionIndices,
                out int weightedMissionCount)
                ? weightedMissionCount
                : 0;

            if (_profileWeightedMissionCount <= 0)
                _lastMissionIndex = 0;
            else if ((uint)_lastMissionIndex >= (uint)_profileWeightedMissionCount)
                _lastMissionIndex %= _profileWeightedMissionCount;
        }

        private bool HasProfileRuntimeState(DirectorMissionBridgeProfile profile)
        {
            return profile != null &&
                   ReferenceEquals(profile, _cachedProfile) &&
                   _profileRuntimeMissionCount == profile.RuntimeMissionCount &&
                   _profileWeightedMissionCount > 0 &&
                   _profileWeightedMissionIndices != null &&
                   _profileWeightedMissionCount <= _profileWeightedMissionIndices.Length &&
                   _profileMissionCooldownUntil != null &&
                   _profileMissionCooldownUntil.Length >= _profileRuntimeMissionCount;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.MissionRuntime:
                case GlobalRegistryServiceSlot.QuestSystem:
                case GlobalRegistryServiceSlot.QuestRuntime:
                    _missionManager = currentService as IQuestSystem;
                    break;
                case GlobalRegistryServiceSlot.FirstHourRuntime:
                    _firstHourDirector = currentService as IFirstHourReadModel;
                    break;
            }
        }

        private void CacheRegistryServicesCold()
        {
            _missionManager = GlobalRegistry.QuestSystem;
            _firstHourDirector = GlobalRegistry.FirstHourReadModel;
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
    }
}
