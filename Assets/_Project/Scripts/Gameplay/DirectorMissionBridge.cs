// ============================================================================
// HECTON-8 — DirectorMissionBridge.cs
// Мост между HectonDirectorAI и MissionManager.
//
// РОЛЬ:
//   • Слушает HectonDirectorAI.OnRequestMissionTrigger.
//   • При получении события — активирует случайную доступную миссию.
//   • Слушает HectonDirectorAI.OnRequestRareDiscovery.
//   • При получении — регистрирует discovery через NarrativeEvents.
//
// АРХИТЕКТУРА:
//   • Не ITickable — только event subscriptions.
//   • Назначить на тот же GameObject что и HectonDirectorAI.
// ============================================================================

using Hecton8.Core;
using Hecton8.Systems.AI;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Director Mission Bridge")]
    public sealed class DirectorMissionBridge : MonoBehaviour
    {
        [Header("── Mission IDs ─────────────────────────────")]
        [Tooltip("ID миссий которые Director может активировать случайно.")]
        [SerializeField] private string[] directorMissionIds = new string[0];

        [Tooltip("ID discovery для rare discovery события.")]
        [SerializeField] private string rareDiscoveryId = "director_rare_discovery";

        [Header("── Early-Game Gate ─────────────────────────")]
        [Tooltip("Do not let director-side missions compete with the early onboarding spine before this milestone is reached.")]
        [SerializeField] private FirstHourMilestone minimumMilestone = FirstHourMilestone.FirstCraft;

        private int _lastMissionIndex;

        private void OnEnable()
        {
            HectonDirectorAI.OnRequestMissionTrigger += HandleMissionTrigger;
            HectonDirectorAI.OnRequestRareDiscovery  += HandleRareDiscovery;
        }

        private void OnDisable()
        {
            HectonDirectorAI.OnRequestMissionTrigger -= HandleMissionTrigger;
            HectonDirectorAI.OnRequestRareDiscovery  -= HandleRareDiscovery;
        }

        private void HandleMissionTrigger(Vector3 position)
        {
            if (!CanServeDirectorContent())
                return;

            if (directorMissionIds == null || directorMissionIds.Length == 0)
                return;

            MissionManager mm = MissionManager.Instance;
            if (mm == null) return;

            // Циклически активируем миссии
            for (int i = 0; i < directorMissionIds.Length; i++)
            {
                int idx = (_lastMissionIndex + i) % directorMissionIds.Length;
                string missionId = directorMissionIds[idx];

                if (string.IsNullOrEmpty(missionId)) continue;
                if (mm.IsMissionCompleted(missionId)) continue;
                if (mm.GetActiveMission(missionId) != null) continue;

                mm.StartMission(missionId);
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

        private bool CanServeDirectorContent()
        {
            FirstHourDirector firstHourDirector = FirstHourDirector.Instance;
            if (firstHourDirector == null)
                return true;

            return firstHourDirector.IsMilestoneComplete(minimumMilestone);
        }
    }
}
