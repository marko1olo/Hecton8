// ============================================================================
// HECTON-8 — MissionManager.cs
// Singleton manager for mission/quest system.
// ============================================================================

using System.Collections.Generic;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>Singleton manager for handling missions and quests.</summary>
    public sealed class MissionManager : MonoBehaviour
    {
        /// <summary>Singleton instance of the mission manager.</summary>
        public static MissionManager Instance { get; private set; }

        /// <summary>List of available mission data assets.</summary>
        [Header("References")]
        [SerializeField] private List<MissionData> availableMissions = new List<MissionData>();

        private readonly Dictionary<string, MissionInstance> _activeMissions = new Dictionary<string, MissionInstance>();
        private readonly HashSet<string> _completedMissions = new HashSet<string>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>Starts a mission by its ID.</summary>
        /// <param name="missionId">The unique identifier of the mission.</param>
        public void StartMission(string missionId)
        {
            if (_activeMissions.ContainsKey(missionId) || _completedMissions.Contains(missionId))
                return;

            MissionData data = availableMissions.Find(m => m.missionId == missionId);
            if (data == null)
                return;

            MissionInstance instance = new MissionInstance(data);
            _activeMissions[missionId] = instance;

            // TODO: Notify PDA, HUD, etc.
            Debug.Log($"Mission started: {data.title}");
        }

        /// <summary>Completes an objective for a mission.</summary>
        /// <param name="missionId">The mission identifier.</param>
        /// <param name="objectiveId">The objective identifier.</param>
        public void CompleteObjective(string missionId, string objectiveId)
        {
            if (!_activeMissions.TryGetValue(missionId, out MissionInstance mission))
                return;

            mission.CompleteObjective(objectiveId);

            if (mission.IsCompleted)
            {
                _completedMissions.Add(missionId);
                _activeMissions.Remove(missionId);

                // TODO: Grant rewards
                Debug.Log($"Mission completed: {mission.Data.title}");
            }
        }

        /// <summary>Gets an active mission instance.</summary>
        /// <param name="missionId">The mission identifier.</param>
        /// <returns>The mission instance, or null if not active.</returns>
        public MissionInstance GetActiveMission(string missionId)
        {
            _activeMissions.TryGetValue(missionId, out MissionInstance mission);
            return mission;
        }

        /// <summary>Gets all active mission instances.</summary>
        /// <returns>Enumerable of active missions.</returns>
        public IEnumerable<MissionInstance> GetActiveMissions()
        {
            return _activeMissions.Values;
        }

        /// <summary>Checks if a mission is completed.</summary>
        /// <param name="missionId">The mission identifier.</param>
        /// <returns>True if completed.</returns>
        public bool IsMissionCompleted(string missionId)
        {
            return _completedMissions.Contains(missionId);
        }

        /// <summary>Represents an active instance of a mission.</summary>
        public sealed class MissionInstance
        {
            /// <summary>The mission data.</summary>
            public MissionData Data { get; }

            /// <summary>Current state of the mission.</summary>
            public MissionData.MissionState State { get; private set; }

            /// <summary>Completed objectives.</summary>
            public Dictionary<string, bool> CompletedObjectives { get; } = new Dictionary<string, bool>();

            /// <summary>Whether the mission is completed.</summary>
            public bool IsCompleted => State == MissionData.MissionState.Completed;

            /// <summary>Creates a new mission instance.</summary>
            /// <param name="data">The mission data.</param>
            public MissionInstance(MissionData data)
            {
                Data = data;
                State = MissionData.MissionState.Active;
            }

            /// <summary>Completes an objective.</summary>
            /// <param name="objectiveId">The objective identifier.</param>
            public void CompleteObjective(string objectiveId)
            {
                CompletedObjectives[objectiveId] = true;

                // Check if all required objectives are complete
                bool allComplete = true;
                foreach (var obj in Data.objectives)
                {
                    if (!obj.isOptional && !CompletedObjectives.ContainsKey(obj.objectiveId))
                    {
                        allComplete = false;
                        break;
                    }
                }

                if (allComplete)
                {
                    State = MissionData.MissionState.Completed;
                }
            }
        }
    }
}