using Hecton8.Gameplay;
using UnityEngine;

namespace Hecton8.AI
{
    /// <summary>
    /// Простой расчёт заметности игрока по фонарю.
    /// Смотрим: включён ли фонарь, в каком он режиме, насколько игрок светит в сторону существа и как далеко оно стоит.
    /// </summary>
    public static class LightDetectionSystem
    {
        public static float EvaluatePlayerLight01(
            Vector3 listenerPosition,
            Transform playerTransform,
            PlayerFlashlight flashlight)
        {
            if (playerTransform == null || flashlight == null || !flashlight.IsOn)
                return 0f;

            Vector3 toListener = listenerPosition - playerTransform.position;
            float distance = toListener.magnitude;
            if (distance <= 0.01f)
                return 1f;

            Vector3 direction = toListener / distance;
            float facing01 = Mathf.Clamp01(Vector3.Dot(playerTransform.forward, direction));
            float heatBoost = Mathf.Lerp(0f, 0.08f, Mathf.Clamp01(flashlight.HeatLevel));

            float beamRange;
            float beamWeight;
            switch (flashlight.CurrentBeamMode)
            {
                case PlayerFlashlight.BeamMode.Flood:
                    beamRange = 14f;
                    beamWeight = 0.72f;
                    break;
                case PlayerFlashlight.BeamMode.Focus:
                    beamRange = 26f;
                    beamWeight = 1f;
                    break;
                default:
                    beamRange = 18f;
                    beamWeight = 0.84f;
                    break;
            }

            float distance01 = 1f - Mathf.InverseLerp(beamRange * 0.35f, beamRange, distance);
            float beam01 = (0.3f + (facing01 * 0.7f) + heatBoost) * beamWeight * distance01;
            return Mathf.Clamp01(beam01);
        }
    }
}
