using Hecton8.Audio;
using Hecton8.Bootstrap;
using Hecton8.Gameplay;
using TMPro;
using UnityEngine;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Suit Advisory Controller")]
    public sealed class SuitAdvisoryController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HectonSurvivalSystem survival;
        [SerializeField] private HUDNotification hudNotification;

        [Header("Thresholds")]
        [SerializeField, Range(0.05f, 0.8f)] private float oxygenWarningThreshold = 0.35f;
        [SerializeField, Range(0.05f, 0.5f)] private float oxygenCriticalThreshold = 0.15f;
        [SerializeField, Range(0.05f, 0.8f)] private float energyWarningThreshold = 0.25f;
        [SerializeField, Range(0.05f, 0.8f)] private float integrityWarningThreshold = 0.35f;
        [SerializeField, Range(0.05f, 0.5f)] private float integrityCriticalThreshold = 0.18f;
        [SerializeField] private float safeDepthWarningMargin = 20f;
        [SerializeField] private float safeDepthCriticalMargin = 6f;
        [SerializeField] private float resetHysteresis = 0.06f;

        [Header("Audio")]
        [SerializeField] private AudioClip warningClip;
        [SerializeField] private AudioClip criticalClip;
        [SerializeField, Range(0f, 1f)] private float uiVolume = 0.45f;

        private bool _oxygenWarned;
        private bool _oxygenCritical;
        private bool _energyWarned;
        private bool _integrityWarned;
        private bool _integrityCritical;
        private bool _depthWarned;
        private bool _depthCritical;
        private bool _deathTriggered;

        private const string MsgOxygenWarning = "OXYGEN RESERVES LOW";
        private const string MsgOxygenCritical = "CRITICAL OXYGEN";
        private const string MsgEnergyWarning = "SUIT POWER LOW";
        private const string MsgIntegrityWarning = "SUIT INTEGRITY DEGRADED";
        private const string MsgIntegrityCritical = "SUIT INTEGRITY CRITICAL";
        private const string MsgDepthWarning = "DEPTH LIMIT APPROACHING";
        private const string MsgDepthCritical = "DEPTH LIMIT EXCEEDED";
        private const string MsgDeath = "SUIT FAILURE";

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
            EvaluateAll();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void ResolveReferences()
        {
            if (survival == null)
            {
                survival = GetComponent<HectonSurvivalSystem>();

                if (survival == null &&
                    SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                    playerTransform != null)
                {
                    survival = playerTransform.GetComponent<HectonSurvivalSystem>();
                }
            }

            if (hudNotification == null)
                HUDNotification.TryGetActive(out hudNotification);
        }

        private void Subscribe()
        {
            if (survival == null)
                return;

            survival.OnOxygenChanged += HandleOxygenChanged;
            survival.OnEnergyChanged += HandleEnergyChanged;
            survival.OnIntegrityChanged += HandleIntegrityChanged;
            survival.OnDepthChanged += HandleDepthChanged;
            survival.OnDeath += HandleDeath;
        }

        private void Unsubscribe()
        {
            if (survival == null)
                return;

            survival.OnOxygenChanged -= HandleOxygenChanged;
            survival.OnEnergyChanged -= HandleEnergyChanged;
            survival.OnIntegrityChanged -= HandleIntegrityChanged;
            survival.OnDepthChanged -= HandleDepthChanged;
            survival.OnDeath -= HandleDeath;
        }

        private void EvaluateAll()
        {
            if (survival == null)
                return;

            HandleOxygenChanged(survival.Oxygen);
            HandleEnergyChanged(survival.Energy);
            HandleIntegrityChanged(survival.Integrity);
            HandleDepthChanged(survival.Depth);
        }

        private void HandleOxygenChanged(float _)
        {
            if (survival == null || survival.Stats == null)
                return;

            float normalized = survival.OxygenNormalized;

            if (!_oxygenCritical && normalized <= oxygenCriticalThreshold)
            {
                _oxygenCritical = true;
                NotifyCritical(MsgOxygenCritical);
            }
            else if (!_oxygenWarned && normalized <= oxygenWarningThreshold)
            {
                _oxygenWarned = true;
                NotifyWarning(MsgOxygenWarning);
            }

            if (normalized > oxygenWarningThreshold + resetHysteresis)
            {
                _oxygenWarned = false;
                _oxygenCritical = false;
            }
            else if (normalized > oxygenCriticalThreshold + resetHysteresis)
            {
                _oxygenCritical = false;
            }
        }

        private void HandleEnergyChanged(float _)
        {
            if (survival == null || survival.Stats == null)
                return;

            float normalized = survival.EnergyNormalized;
            if (!_energyWarned && normalized <= energyWarningThreshold)
            {
                _energyWarned = true;
                NotifyWarning(MsgEnergyWarning);
            }

            if (normalized > energyWarningThreshold + resetHysteresis)
                _energyWarned = false;
        }

        private void HandleIntegrityChanged(float _)
        {
            if (survival == null || survival.Stats == null)
                return;

            float normalized = survival.IntegrityNormalized;

            if (!_integrityCritical && normalized <= integrityCriticalThreshold)
            {
                _integrityCritical = true;
                NotifyCritical(MsgIntegrityCritical);
            }
            else if (!_integrityWarned && normalized <= integrityWarningThreshold)
            {
                _integrityWarned = true;
                NotifyWarning(MsgIntegrityWarning);
            }

            if (normalized > integrityWarningThreshold + resetHysteresis)
            {
                _integrityWarned = false;
                _integrityCritical = false;
            }
            else if (normalized > integrityCriticalThreshold + resetHysteresis)
            {
                _integrityCritical = false;
            }
        }

        private void HandleDepthChanged(float depth)
        {
            if (survival == null || survival.Stats == null)
                return;

            float remaining = survival.Stats.SafeDepth - depth;

            if (!_depthCritical && remaining <= safeDepthCriticalMargin)
            {
                _depthCritical = true;
                NotifyCritical(MsgDepthCritical);
            }
            else if (!_depthWarned && remaining <= safeDepthWarningMargin)
            {
                _depthWarned = true;
                NotifyWarning(MsgDepthWarning);
            }

            if (remaining > safeDepthWarningMargin + 5f)
            {
                _depthWarned = false;
                _depthCritical = false;
            }
            else if (remaining > safeDepthCriticalMargin + 3f)
            {
                _depthCritical = false;
            }
        }

        private void HandleDeath()
        {
            if (_deathTriggered)
                return;

            _deathTriggered = true;
            NotifyCritical(MsgDeath);
        }

        private void NotifyWarning(string message)
        {
            hudNotification?.ShowWarning(message);
            PlayUiClip(warningClip);
        }

        private void NotifyCritical(string message)
        {
            hudNotification?.ShowCritical(message);
            PlayUiClip(criticalClip != null ? criticalClip : warningClip);
        }

        private void PlayUiClip(AudioClip clip)
        {
            if (clip == null)
                return;

            SpatialAudioManager audio = SpatialAudioManager.Instance;
            if (audio != null)
                audio.PlayStatic2D(clip, uiVolume);
        }
    }
}
