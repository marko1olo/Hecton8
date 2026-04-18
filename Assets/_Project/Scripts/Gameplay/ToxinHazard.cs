using UnityEngine;

namespace Hecton8.Gameplay
{
    #pragma warning disable CS0414
    /// <summary>
    /// Lightweight toxin exposure notifier that complements EnvironmentalHazard.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("Hecton8/Gameplay/Toxin Hazard")]
    public sealed class ToxinHazard : MonoBehaviour
    {
        [Header("Toxin Settings")]
        [SerializeField] private float toxinBuildupRate = 0.3f;
        [SerializeField] private float maxToxinLevel = 50f;
        [SerializeField, TagSelector] private string playerTag = "Player";

        private int _activeExposureCount;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(playerTag))
                return;

            _activeExposureCount++;
            HazardExposureNotifier.Enter(HazardType.Toxicity);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag(playerTag) || _activeExposureCount <= 0)
                return;

            _activeExposureCount--;
            HazardExposureNotifier.Exit(HazardType.Toxicity);
        }

        private void OnDisable()
        {
            while (_activeExposureCount > 0)
            {
                _activeExposureCount--;
                HazardExposureNotifier.Exit(HazardType.Toxicity);
            }
        }
    }
    #pragma warning restore CS0414
}
