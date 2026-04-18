using UnityEngine;

namespace Hecton8.Gameplay
{
    #pragma warning disable CS0414
    /// <summary>
    /// Lightweight radiation exposure notifier that complements EnvironmentalHazard.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("Hecton8/Gameplay/Radiation Hazard")]
    public sealed class RadiationHazard : MonoBehaviour
    {
        [Header("Radiation Settings")]
        [SerializeField] private float radiationBuildupRate = 0.5f;
        [SerializeField] private float maxRadiationLevel = 100f;
        [SerializeField, TagSelector] private string playerTag = "Player";

        private int _activeExposureCount;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(playerTag))
                return;

            _activeExposureCount++;
            HazardExposureNotifier.Enter(HazardType.Radiation);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag(playerTag) || _activeExposureCount <= 0)
                return;

            _activeExposureCount--;
            HazardExposureNotifier.Exit(HazardType.Radiation);
        }

        private void OnDisable()
        {
            while (_activeExposureCount > 0)
            {
                _activeExposureCount--;
                HazardExposureNotifier.Exit(HazardType.Radiation);
            }
        }
    }
    #pragma warning restore CS0414
}
