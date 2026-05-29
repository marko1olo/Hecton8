using System;
using Hecton8.Quest;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [CreateAssetMenu(
        fileName = "DirectorMissionBridgeProfile",
        menuName = "Hecton8/Gameplay/Director Mission Bridge Profile",
        order = 130)]
    public sealed class DirectorMissionBridgeProfile : ScriptableObject
    {
        public const uint SchemaVersion = 1u;
        public const int MaxRuntimeMissions = 64;
        public const int MaxRuntimeTotalWeight = MaxRuntimeMissions * MaxMissionWeight;
        private const int MaxMissionWeight = 16;

        [SerializeField] private string rareDiscoveryId = "director_rare_discovery";
        [SerializeField] private FirstHourMilestone minimumMilestone = FirstHourMilestone.FirstCraft;
        [SerializeField] private MissionEntry[] missions = Array.Empty<MissionEntry>();
        [SerializeField] private QuestData[] validationQuests = Array.Empty<QuestData>();
        [SerializeField, HideInInspector] private int validationErrorCount;
        [SerializeField, HideInInspector] private int validationWarningCount;
        [SerializeField, HideInInspector] private int validationFirstInvalidIndex = -1;
        [SerializeField, HideInInspector] private int validationRuntimeMissionCount;
        [SerializeField, HideInInspector] private int validationTotalWeight;
        [SerializeField, HideInInspector] private ProfileValidationFlags validationFlags;

        public string RareDiscoveryId => string.IsNullOrWhiteSpace(rareDiscoveryId) ? "director_rare_discovery" : rareDiscoveryId;
        public FirstHourMilestone MinimumMilestone => minimumMilestone;
        public int MissionCount => missions != null ? missions.Length : 0;
        public int RuntimeMissionCount => ResolveRuntimeMissionCount();
        public int ValidationErrorCount => validationErrorCount;
        public int ValidationWarningCount => validationWarningCount;
        public int ValidationFirstInvalidIndex => validationFirstInvalidIndex;
        public int ValidationRuntimeMissionCount => validationRuntimeMissionCount;
        public int ValidationTotalWeight => validationTotalWeight;
        public ProfileValidationFlags ValidationFlags => validationFlags;

        public MissionEntry GetMission(int index)
        {
            return missions[index];
        }

        public bool TryGetMission(int index, out MissionEntry mission)
        {
            mission = default;
            if (missions == null || (uint)index >= (uint)missions.Length)
                return false;

            mission = missions[index];
            return mission.IsValid;
        }

        public bool TryGetRuntimeMission(int index, out MissionEntry mission)
        {
            mission = default;
            if (missions == null || (uint)index >= (uint)ResolveRuntimeMissionCount())
                return false;

            MissionEntry candidate = missions[index];
            if (!candidate.IsValid || HasDuplicateMissionBefore(candidate.MissionId, index))
                return false;

            mission = candidate;
            return true;
        }

        public bool TryGetMissionById(string missionId, out MissionEntry mission, out int missionIndex)
        {
            mission = default;
            missionIndex = -1;
            if (string.IsNullOrWhiteSpace(missionId) || missions == null)
                return false;

            int count = ResolveRuntimeMissionCount();
            for (int i = 0; i < count; i++)
            {
                MissionEntry candidate = missions[i];
                if (!candidate.IsValid)
                    continue;

                if (!string.Equals(candidate.MissionId, missionId, StringComparison.Ordinal))
                    continue;

                mission = candidate;
                missionIndex = i;
                return true;
            }

            return false;
        }

        public int GetTotalWeight()
        {
            int totalWeight = 0;
            int count = ResolveRuntimeMissionCount();
            for (int i = 0; i < count; i++)
            {
                if (!TryGetRuntimeMission(i, out MissionEntry mission))
                    continue;

                totalWeight += mission.ClampedWeight;
            }

            return totalWeight;
        }

        public bool TryBuildWeightedMissionIndexTable(int[] destination, out int weightedCount)
        {
            weightedCount = 0;
            if (destination == null || destination.Length <= 0)
                return false;

            bool truncated = false;
            int count = ResolveRuntimeMissionCount();
            for (int i = 0; i < count; i++)
            {
                if (!TryGetRuntimeMission(i, out MissionEntry mission))
                    continue;

                int weight = mission.ClampedWeight;
                for (int j = 0; j < weight; j++)
                {
                    if (weightedCount >= destination.Length)
                    {
                        truncated = true;
                        break;
                    }

                    destination[weightedCount] = i;
                    weightedCount++;
                }
            }

            return weightedCount > 0 && !truncated;
        }

        public bool TryResolveWeightedIndex(int weightedIndex, out int missionIndex)
        {
            missionIndex = -1;
            int cursor = weightedIndex;
            int count = ResolveRuntimeMissionCount();
            for (int i = 0; i < count; i++)
            {
                if (!TryGetRuntimeMission(i, out MissionEntry mission))
                    continue;

                int weight = mission.ClampedWeight;
                if (cursor < weight)
                {
                    missionIndex = i;
                    return true;
                }

                cursor -= weight;
            }

            return false;
        }

        private int ResolveRuntimeMissionCount()
        {
            int count = missions != null ? missions.Length : 0;
            return count > MaxRuntimeMissions ? MaxRuntimeMissions : count;
        }

        private bool HasDuplicateMissionBefore(string missionId, int index)
        {
            if (missions == null || string.IsNullOrWhiteSpace(missionId))
                return false;

            for (int i = 0; i < index; i++)
            {
                if (string.Equals(missions[i].MissionId, missionId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            validationErrorCount = 0;
            validationWarningCount = 0;
            validationFirstInvalidIndex = -1;
            validationRuntimeMissionCount = 0;
            validationTotalWeight = 0;
            validationFlags = ProfileValidationFlags.None;

            if (string.IsNullOrWhiteSpace(rareDiscoveryId))
                MarkValidationWarning(ProfileValidationFlags.MissingRareDiscoveryId);

            if (missions == null)
            {
                missions = Array.Empty<MissionEntry>();
                return;
            }

            validationRuntimeMissionCount = ResolveRuntimeMissionCount();
            if (missions.Length > MaxRuntimeMissions)
                MarkValidationWarning(ProfileValidationFlags.OverRuntimeMissionLimit);

            for (int i = 0; i < missions.Length; i++)
            {
                MissionEntry mission = missions[i].Sanitized();
                missions[i] = mission;
                if (!mission.IsValid)
                {
                    MarkValidationError(ProfileValidationFlags.MissingMissionId, i);
                    continue;
                }

                if (HasDuplicateMissionBefore(mission.MissionId, i))
                {
                    MarkValidationError(ProfileValidationFlags.DuplicateMissionId, i);
                    continue;
                }

                if (validationQuests != null && validationQuests.Length > 0 && !HasValidationQuest(mission.MissionId))
                    MarkValidationError(ProfileValidationFlags.MissingQuestAsset, i);

                if (i < MaxRuntimeMissions)
                    validationTotalWeight += mission.ClampedWeight;
            }
        }

        private bool HasValidationQuest(string missionId)
        {
            for (int i = 0; i < validationQuests.Length; i++)
            {
                QuestData questData = validationQuests[i];
                if (questData != null && string.Equals(questData.questId, missionId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private void MarkValidationError(ProfileValidationFlags flag, int index)
        {
            validationErrorCount++;
            validationFlags |= flag;
            if (validationFirstInvalidIndex < 0)
                validationFirstInvalidIndex = index;
        }

        private void MarkValidationWarning(ProfileValidationFlags flag)
        {
            validationWarningCount++;
            validationFlags |= flag;
        }
#endif

        [Flags]
        public enum ProfileValidationFlags
        {
            None = 0,
            MissingMissionId = 1 << 0,
            DuplicateMissionId = 1 << 1,
            MissingQuestAsset = 1 << 2,
            OverRuntimeMissionLimit = 1 << 3,
            MissingRareDiscoveryId = 1 << 4,
        }

        [Serializable]
        public struct MissionEntry
        {
            [SerializeField] private string missionId;
            [SerializeField, Range(1, MaxMissionWeight)] private int weight;
            [SerializeField, Min(0f)] private float cooldownSeconds;
            [SerializeField] private FirstHourMilestone minimumMilestone;

            public string MissionId => missionId;
            public int ClampedWeight => Mathf.Clamp(weight <= 0 ? 1 : weight, 1, MaxMissionWeight);
            public float CooldownSeconds => Mathf.Max(0f, cooldownSeconds);
            public FirstHourMilestone MinimumMilestone => minimumMilestone;
            public bool IsValid => !string.IsNullOrWhiteSpace(missionId);

            public MissionEntry Sanitized()
            {
                MissionEntry sanitized = this;
                sanitized.weight = Mathf.Clamp(weight <= 0 ? 1 : weight, 1, MaxMissionWeight);
                sanitized.cooldownSeconds = Mathf.Max(0f, cooldownSeconds);
                return sanitized;
            }
        }
    }
}
