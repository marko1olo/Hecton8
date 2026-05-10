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

using Hecton8.Core;
using Hecton8.Systems.AI;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Director Mission Bridge")]
    public sealed class DirectorMissionBridge : MonoBehaviour, IDirectorAIEventListener
    {
        [Header("── Mission IDs ─────────────────────────────")]
        [Tooltip("ID missiy kotorye Director mozhet aktivirovat sluchayno.")]
        [SerializeField] private string[] directorMissionIds = new string[0];

        [Tooltip("ID discovery dlya rare discovery sobytiya.")]
        [SerializeField] private string rareDiscoveryId = "director_rare_discovery";

        [Header("── Early-Game Gate ─────────────────────────")]
        [Tooltip("Do not let director-side missions compete with the early onboarding spine before this milestone is reached.")]
        [SerializeField] private FirstHourMilestone minimumMilestone = FirstHourMilestone.FirstCraft;

        private int _lastMissionIndex;

        private void OnEnable()
        {
            DirectorAIEvents.Register(this);
        }

        private void OnDisable()
        {
            DirectorAIEvents.Unregister(this);
        }

        private void OnDestroy()
        {
            DirectorAIEvents.Unregister(this);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (directorMissionIds == null || directorMissionIds.Length <= 0)
                return;

            int writeIndex = 0;
            for (int i = 0; i < directorMissionIds.Length; i++)
            {
                string missionId = directorMissionIds[i];
                if (string.IsNullOrWhiteSpace(missionId))
                    continue;

                bool duplicate = false;
                for (int j = 0; j < writeIndex; j++)
                {
                    if (directorMissionIds[j] == missionId)
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (duplicate)
                    continue;

                directorMissionIds[writeIndex] = missionId;
                writeIndex++;
            }

            if (writeIndex == directorMissionIds.Length)
                return;

            string[] compact = new string[writeIndex];
            for (int i = 0; i < writeIndex; i++)
                compact[i] = directorMissionIds[i];

            directorMissionIds = compact;
        }
#endif

        private void HandleMissionTrigger(Vector3 position)
        {
            if (!CanServeDirectorContent())
                return;

            if (directorMissionIds == null || directorMissionIds.Length == 0)
                return;

            MissionManager mm = GlobalRegistry.Missions;
            if (mm == null) return;

            // Tsiklicheski aktiviruem missii
            for (int i = 0; i < directorMissionIds.Length; i++)
            {
                int idx = (_lastMissionIndex + i) % directorMissionIds.Length;
                string missionId = directorMissionIds[idx];

                if (string.IsNullOrEmpty(missionId)) continue;
                if (mm.IsMissionCompleted(missionId)) continue;
                if (mm.GetActiveMission(missionId) != null) continue;

                mm.StartMission(missionId);
                if (mm.GetActiveMission(missionId) == null)
                    continue;

                _lastMissionIndex = (idx + 1) % directorMissionIds.Length;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[DirectorBridge] Mission triggered: {missionId} near {position}");
#endif
                return;
            }
        }

        private void HandleRareDiscovery(Vector3 position)
        {
            if (!CanServeDirectorContent())
                return;

            if (!string.IsNullOrEmpty(rareDiscoveryId))
                NarrativeEvents.RaiseDiscoveryMade(rareDiscoveryId);
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

        private bool CanServeDirectorContent()
        {
            FirstHourDirector firstHourDirector = Hecton8.Core.GlobalRegistry.FirstHour;
            if (firstHourDirector == null)
                return true;

            return firstHourDirector.IsMilestoneComplete(minimumMilestone);
        }
    }
}
