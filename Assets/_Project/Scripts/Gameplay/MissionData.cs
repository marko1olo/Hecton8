// ============================================================================
// HECTON-8 - MissionData.cs
// ScriptableObject for mission/quest definitions.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [Flags]
    public enum MissionDataValidationFlags : uint
    {
        None = 0u,
        MissingMissionId = 1u << 0,
        MissingTitle = 1u << 1,
        MissingDescription = 1u << 2,
        InvalidTimeLimit = 1u << 3,
        NullObjective = 1u << 4,
        MissingObjectiveId = 1u << 5,
        DuplicateObjectiveId = 1u << 6,
        InvalidObjectiveCount = 1u << 7,
        MissingObjectiveTarget = 1u << 8,
        NullReward = 1u << 9,
        MissingItemRewardId = 1u << 10,
        InvalidItemRewardCount = 1u << 11,
        InvalidRewardExperience = 1u << 12
    }

    [CreateAssetMenu(
        fileName = "MissionData",
        menuName = "Hecton8/Gameplay/Mission Data")]
    public sealed class MissionData : ScriptableObject
    {
        public const string DefaultMissionId = "mission.unknown";
        public const string DefaultMissionTitle = "UNKNOWN MISSION";
        public const string DefaultMissionDescription = "Mission description goes here.";
        public const string DefaultObjectiveId = "obj.unknown";
        public const string DefaultObjectiveDescription = "Objective description.";

        [Header("Identity")]
        public string missionId = DefaultMissionId;

        public string title = DefaultMissionTitle;

        [TextArea(2, 5)]
        public string description = DefaultMissionDescription;

        [Header("Objectives")]
        public List<ObjectiveData> objectives = new List<ObjectiveData>(4);

        [Header("Rewards")]
        public List<RewardData> rewards = new List<RewardData>(4);

        [Header("State")]
        public MissionState initialState = MissionState.Available;

        public bool isRepeatable = false;

        public float timeLimitSeconds = 0f;

        [NonSerialized] private MissionDataValidationFlags _validationFlags;
        [NonSerialized] private int _validObjectiveCount;
        [NonSerialized] private int _invalidObjectiveCount;
        [NonSerialized] private int _duplicateObjectiveCount;
        [NonSerialized] private int _firstInvalidObjectiveIndex = -1;
        [NonSerialized] private int _firstDuplicateObjectiveIndex = -1;
        [NonSerialized] private int _validRewardCount;
        [NonSerialized] private int _invalidRewardCount;
        [NonSerialized] private int _firstInvalidRewardIndex = -1;

        public enum MissionState
        {
            Available,
            Active,
            Completed,
            Failed,
            Expired
        }

        public string RuntimeMissionId => MissionDataRuntimeGuards.TextOrFallback(missionId, DefaultMissionId);

        public string RuntimeTitle => MissionDataRuntimeGuards.TextOrFallback(title, DefaultMissionTitle);

        public string RuntimeDescription => MissionDataRuntimeGuards.TextOrFallback(description, DefaultMissionDescription);

        public float RuntimeTimeLimitSeconds => MissionDataRuntimeGuards.NonNegativeFinite(timeLimitSeconds, 0f);

        public int ObjectiveSlotCount => objectives != null ? objectives.Count : 0;

        public int RewardSlotCount => rewards != null ? rewards.Count : 0;

        public int RuntimeObjectiveCount => _validObjectiveCount;

        public int RuntimeRewardCount => _validRewardCount;

        public MissionDataValidationFlags ValidationFlags => _validationFlags;

        public bool HasValidationErrors => _validationFlags != MissionDataValidationFlags.None;

        public int InvalidObjectiveCount => _invalidObjectiveCount;

        public int DuplicateObjectiveCount => _duplicateObjectiveCount;

        public int FirstInvalidObjectiveIndex => _firstInvalidObjectiveIndex;

        public int FirstDuplicateObjectiveIndex => _firstDuplicateObjectiveIndex;

        public int InvalidRewardCount => _invalidRewardCount;

        public int FirstInvalidRewardIndex => _firstInvalidRewardIndex;

        public bool TryGetObjective(int validIndex, out ObjectiveData objective)
        {
            objective = null;
            if (validIndex < 0 || objectives == null)
                return false;

            int validCursor = 0;
            for (int slot = 0; slot < objectives.Count; slot++)
            {
                ObjectiveData candidate = objectives[slot];
                if (!IsObjectiveRuntimeValid(slot, candidate))
                    continue;

                if (validCursor == validIndex)
                {
                    objective = candidate;
                    return true;
                }

                validCursor++;
            }

            return false;
        }

        public bool TryGetObjectiveBySlot(int slot, out ObjectiveData objective)
        {
            objective = null;
            if (objectives == null || slot < 0 || slot >= objectives.Count)
                return false;

            objective = objectives[slot];
            return objective != null;
        }

        public bool IsObjectiveRuntimeValidBySlot(int slot)
        {
            if (objectives == null || slot < 0 || slot >= objectives.Count)
                return false;

            return IsObjectiveRuntimeValid(slot, objectives[slot]);
        }

        public bool TryGetReward(int validIndex, out RewardData reward)
        {
            reward = null;
            if (validIndex < 0 || rewards == null)
                return false;

            int validCursor = 0;
            for (int slot = 0; slot < rewards.Count; slot++)
            {
                RewardData candidate = rewards[slot];
                if (!IsRewardRuntimeValid(candidate))
                    continue;

                if (validCursor == validIndex)
                {
                    reward = candidate;
                    return true;
                }

                validCursor++;
            }

            return false;
        }

        public bool TryGetRewardBySlot(int slot, out RewardData reward)
        {
            reward = null;
            if (rewards == null || slot < 0 || slot >= rewards.Count)
                return false;

            reward = rewards[slot];
            return reward != null;
        }

        public bool IsRewardRuntimeValidBySlot(int slot)
        {
            if (rewards == null || slot < 0 || slot >= rewards.Count)
                return false;

            return IsRewardRuntimeValid(rewards[slot]);
        }

        private void OnEnable()
        {
            RebuildValidationCache();
        }

        private void RebuildValidationCache()
        {
            _validationFlags = MissionDataValidationFlags.None;
            _validObjectiveCount = 0;
            _invalidObjectiveCount = 0;
            _duplicateObjectiveCount = 0;
            _firstInvalidObjectiveIndex = -1;
            _firstDuplicateObjectiveIndex = -1;
            _validRewardCount = 0;
            _invalidRewardCount = 0;
            _firstInvalidRewardIndex = -1;

            if (string.IsNullOrWhiteSpace(missionId) || string.Equals(missionId, DefaultMissionId, StringComparison.Ordinal))
                AddValidationFlag(MissionDataValidationFlags.MissingMissionId);

            if (string.IsNullOrWhiteSpace(title))
                AddValidationFlag(MissionDataValidationFlags.MissingTitle);

            if (string.IsNullOrWhiteSpace(description))
                AddValidationFlag(MissionDataValidationFlags.MissingDescription);

            if (MissionDataRuntimeGuards.IsInvalidFiniteNonNegative(timeLimitSeconds))
                AddValidationFlag(MissionDataValidationFlags.InvalidTimeLimit);

            RebuildObjectiveValidationCache();
            RebuildRewardValidationCache();
        }

        private void RebuildObjectiveValidationCache()
        {
            if (objectives == null)
                return;

            for (int slot = 0; slot < objectives.Count; slot++)
            {
                ObjectiveData objective = objectives[slot];
                MissionDataValidationFlags slotFlags = GetObjectiveValidationFlags(slot, objective);
                if (slotFlags == MissionDataValidationFlags.None)
                {
                    _validObjectiveCount++;
                    continue;
                }

                _invalidObjectiveCount++;
                AddValidationFlag(slotFlags);
                if (_firstInvalidObjectiveIndex < 0)
                    _firstInvalidObjectiveIndex = slot;

                if ((slotFlags & MissionDataValidationFlags.DuplicateObjectiveId) != 0)
                {
                    _duplicateObjectiveCount++;
                    if (_firstDuplicateObjectiveIndex < 0)
                        _firstDuplicateObjectiveIndex = slot;
                }
            }
        }

        private void RebuildRewardValidationCache()
        {
            if (rewards == null)
                return;

            for (int slot = 0; slot < rewards.Count; slot++)
            {
                RewardData reward = rewards[slot];
                MissionDataValidationFlags slotFlags = GetRewardValidationFlags(reward);
                if (slotFlags == MissionDataValidationFlags.None)
                {
                    _validRewardCount++;
                    continue;
                }

                _invalidRewardCount++;
                AddValidationFlag(slotFlags);
                if (_firstInvalidRewardIndex < 0)
                    _firstInvalidRewardIndex = slot;
            }
        }

        private void AddValidationFlag(MissionDataValidationFlags flag)
        {
            _validationFlags |= flag;
        }

        private MissionDataValidationFlags GetObjectiveValidationFlags(int slot, ObjectiveData objective)
        {
            if (objective == null)
                return MissionDataValidationFlags.NullObjective;

            MissionDataValidationFlags flags = MissionDataValidationFlags.None;
            if (string.IsNullOrWhiteSpace(objective.objectiveId) ||
                string.Equals(objective.objectiveId, DefaultObjectiveId, StringComparison.Ordinal))
            {
                flags |= MissionDataValidationFlags.MissingObjectiveId;
            }
            else if (HasDuplicateObjectiveIdBefore(slot, objective.objectiveId))
            {
                flags |= MissionDataValidationFlags.DuplicateObjectiveId;
            }

            if (objective.requiredCount <= 0)
                flags |= MissionDataValidationFlags.InvalidObjectiveCount;

            if (ObjectiveData.RequiresTarget(objective.type) && string.IsNullOrWhiteSpace(objective.targetId))
                flags |= MissionDataValidationFlags.MissingObjectiveTarget;

            return flags;
        }

        private MissionDataValidationFlags GetRewardValidationFlags(RewardData reward)
        {
            if (reward == null)
                return MissionDataValidationFlags.NullReward;

            MissionDataValidationFlags flags = MissionDataValidationFlags.None;
            if (reward.type == RewardData.RewardType.Item)
            {
                if (string.IsNullOrWhiteSpace(reward.itemId))
                    flags |= MissionDataValidationFlags.MissingItemRewardId;

                if (reward.count <= 0)
                    flags |= MissionDataValidationFlags.InvalidItemRewardCount;
            }

            if (MissionDataRuntimeGuards.IsInvalidFiniteNonNegative(reward.experience))
                flags |= MissionDataValidationFlags.InvalidRewardExperience;

            return flags;
        }

        private bool IsObjectiveRuntimeValid(int slot, ObjectiveData objective)
        {
            return GetObjectiveValidationFlags(slot, objective) == MissionDataValidationFlags.None;
        }

        private static bool IsRewardRuntimeValid(RewardData reward)
        {
            if (reward == null)
                return false;

            if (reward.type == RewardData.RewardType.Item &&
                (string.IsNullOrWhiteSpace(reward.itemId) || reward.count <= 0))
            {
                return false;
            }

            return !MissionDataRuntimeGuards.IsInvalidFiniteNonNegative(reward.experience);
        }

        private bool HasDuplicateObjectiveIdBefore(int slot, string objectiveId)
        {
            if (objectives == null || string.IsNullOrWhiteSpace(objectiveId))
                return false;

            for (int previousSlot = 0; previousSlot < slot; previousSlot++)
            {
                ObjectiveData previous = objectives[previousSlot];
                if (previous == null || string.IsNullOrWhiteSpace(previous.objectiveId))
                    continue;

                if (string.Equals(previous.objectiveId, objectiveId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(missionId) || string.Equals(missionId, DefaultMissionId, StringComparison.Ordinal))
                missionId = name.ToLowerInvariant().Replace(" ", "_");

            missionId = MissionDataRuntimeGuards.NormalizeAuthoringText(missionId, DefaultMissionId);
            title = MissionDataRuntimeGuards.NormalizeAuthoringText(title, DefaultMissionTitle);
            description = MissionDataRuntimeGuards.NormalizeAuthoringText(description, DefaultMissionDescription);
            timeLimitSeconds = RuntimeTimeLimitSeconds;

            if (objectives == null)
                objectives = new List<ObjectiveData>(4);

            NormalizeObjectiveRows();

            if (rewards == null)
                rewards = new List<RewardData>(4);

            NormalizeRewardRows();
            RebuildValidationCache();
        }

        private void NormalizeObjectiveRows()
        {
            for (int i = 0; i < objectives.Count; i++)
            {
                ObjectiveData objective = objectives[i];
                if (objective == null)
                    continue;

                if (string.IsNullOrWhiteSpace(objective.objectiveId) ||
                    string.Equals(objective.objectiveId, DefaultObjectiveId, StringComparison.Ordinal))
                {
                    objective.objectiveId = RuntimeMissionId + ".objective_" + (i + 1);
                }

                objective.objectiveId = MissionDataRuntimeGuards.NormalizeAuthoringText(objective.objectiveId, DefaultObjectiveId);
                objective.description = MissionDataRuntimeGuards.NormalizeAuthoringText(objective.description, DefaultObjectiveDescription);
                objective.requiredCount = objective.RuntimeRequiredCount;
                objective.targetId = MissionDataRuntimeGuards.NormalizeAuthoringText(objective.targetId, string.Empty);
            }
        }

        private void NormalizeRewardRows()
        {
            for (int i = 0; i < rewards.Count; i++)
            {
                RewardData reward = rewards[i];
                if (reward == null)
                    continue;

                reward.itemId = MissionDataRuntimeGuards.NormalizeAuthoringText(reward.itemId, string.Empty);
                if (reward.type == RewardData.RewardType.Item)
                    reward.count = reward.RuntimeCount;

                reward.experience = reward.RuntimeExperience;
            }
        }
#endif
    }

    [Serializable]
    public sealed class ObjectiveData
    {
        public string objectiveId = MissionData.DefaultObjectiveId;

        public string description = MissionData.DefaultObjectiveDescription;

        public ObjectiveType type = ObjectiveType.Manual;

        public int requiredCount = 1;

        public string targetId = string.Empty;

        public bool isOptional = false;

        public string RuntimeObjectiveId => MissionDataRuntimeGuards.TextOrFallback(objectiveId, MissionData.DefaultObjectiveId);

        public string RuntimeDescription => MissionDataRuntimeGuards.TextOrFallback(description, MissionData.DefaultObjectiveDescription);

        public int RuntimeRequiredCount => MissionDataRuntimeGuards.PositiveCount(requiredCount);

        public string RuntimeTargetId => MissionDataRuntimeGuards.TextOrFallback(targetId, string.Empty);

        public bool RequiresTargetId => RequiresTarget(type);

        public bool HasRuntimeTarget => !RequiresTargetId || !string.IsNullOrWhiteSpace(targetId);

        public enum ObjectiveType
        {
            Manual,
            CollectItem,
            ScanTarget,
            BuildModule,
            ReachLocation,
            DestroyTarget,
            SurviveTime
        }

        internal static bool RequiresTarget(ObjectiveType type)
        {
            switch (type)
            {
                case ObjectiveType.CollectItem:
                case ObjectiveType.ScanTarget:
                case ObjectiveType.BuildModule:
                case ObjectiveType.ReachLocation:
                case ObjectiveType.DestroyTarget:
                    return true;
                default:
                    return false;
            }
        }
    }

    [Serializable]
    public sealed class RewardData
    {
        public RewardType type = RewardType.Item;

        public string itemId = string.Empty;

        public int count = 1;

        public float experience = 0f;

        public string RuntimeItemId => MissionDataRuntimeGuards.TextOrFallback(itemId, string.Empty);

        public int RuntimeCount => MissionDataRuntimeGuards.PositiveCount(count);

        public float RuntimeExperience => MissionDataRuntimeGuards.NonNegativeFinite(experience, 0f);

        public enum RewardType
        {
            Item,
            Experience,
            Unlock
        }
    }

    internal static class MissionDataRuntimeGuards
    {
        public static string TextOrFallback(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        public static int PositiveCount(int value)
        {
            return value > 0 ? value : 1;
        }

        public static float NonNegativeFinite(float value, float fallback)
        {
            value = Finite(value, fallback);
            return value < 0f ? 0f : value;
        }

        public static bool IsInvalidFiniteNonNegative(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) || value < 0f;
        }

        private static float Finite(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }

#if UNITY_EDITOR
        public static string NormalizeAuthoringText(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            return value.Trim();
        }
#endif
    }
}
