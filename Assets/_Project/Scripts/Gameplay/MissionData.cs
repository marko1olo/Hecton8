// ============================================================================
// HECTON-8 — MissionData.cs
// ScriptableObject for mission/quest definitions.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [CreateAssetMenu(
        fileName = "MissionData",
        menuName = "Hecton8/Gameplay/Mission Data")]
    public sealed class MissionData : ScriptableObject
    {
        /// <summary>Unique identifier for this mission.</summary>
        [Header("Identity")]
        public string missionId = "mission.unknown";

        /// <summary>Display title of the mission.</summary>
        public string title = "UNKNOWN MISSION";

        /// <summary>Detailed description of the mission.</summary>
        [TextArea(2, 5)]
        public string description = "Mission description goes here.";

        /// <summary>List of objectives that must be completed.</summary>
        [Header("Objectives")]
        public List<ObjectiveData> objectives = new List<ObjectiveData>();

        /// <summary>List of rewards granted upon completion.</summary>
        [Header("Rewards")]
        public List<RewardData> rewards = new List<RewardData>();

        /// <summary>Initial state of the mission.</summary>
        [Header("State")]
        public MissionState initialState = MissionState.Available;

        /// <summary>Whether this mission can be repeated.</summary>
        public bool isRepeatable = false;

        /// <summary>Time limit in seconds (0 = no limit).</summary>
        public float timeLimitSeconds = 0f;

        /// <summary>Possible states of a mission.</summary>
        public enum MissionState
        {
            Available,
            Active,
            Completed,
            Failed,
            Expired
        }
    }

    /// <summary>Data for a single mission objective.</summary>
    [System.Serializable]
    public sealed class ObjectiveData
    {
        /// <summary>Unique identifier for this objective.</summary>
        public string objectiveId = "obj.unknown";

        /// <summary>Display description of the objective.</summary>
        public string description = "Objective description.";

        /// <summary>Type of objective completion.</summary>
        public ObjectiveType type = ObjectiveType.Manual;

        /// <summary>Number of times this objective must be completed.</summary>
        public int requiredCount = 1;

        /// <summary>Target identifier (item id, location, etc.).</summary>
        public string targetId = "";

        /// <summary>Whether this objective is optional.</summary>
        public bool isOptional = false;

        /// <summary>Types of objectives.</summary>
        public enum ObjectiveType
        {
            Manual, // Completed manually
            CollectItem,
            ScanTarget,
            BuildModule,
            ReachLocation,
            DestroyTarget,
            SurviveTime
        }
    }

    /// <summary>Data for mission rewards.</summary>
    [System.Serializable]
    public sealed class RewardData
    {
        /// <summary>Type of reward.</summary>
        public RewardType type = RewardType.Item;

        /// <summary>Item identifier.</summary>
        public string itemId = "";

        /// <summary>Quantity of the reward.</summary>
        public int count = 1;

        /// <summary>Experience points granted.</summary>
        public float experience = 0f;

        /// <summary>Types of rewards.</summary>
        public enum RewardType
        {
            Item,
            Experience,
            Unlock
        }
    }
}