using Hecton8.Gameplay;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.AI
{
    /// <summary>
    /// Prostoy raschet zametnosti igroka po fonaryu.
    /// Smotrim: vklyuchen li fonar, v kakom on rezhime, naskolko igrok svetit v storonu suschestva i kak daleko ono stoit.
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
            float distanceSqr = toListener.sqrMagnitude;
            if (distanceSqr <= 0.0001f)
                return 1f;

            float inverseDistance = math.rsqrt(math.max(distanceSqr, 0.0001f));
            float3 direction = (float3)toListener * inverseDistance;
            float3 forward = math.normalizesafe((float3)playerTransform.forward, new float3(0f, 0f, 1f));
            float facing01 = math.saturate(math.dot(forward, direction));
            float heatBoost = math.lerp(0f, 0.08f, math.saturate(flashlight.HeatLevel));

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

            float nearDistance = beamRange * 0.35f;
            float nearDistanceSqr = nearDistance * nearDistance;
            float beamRangeSqr = beamRange * beamRange;
            float distance01 = 1f - math.saturate((distanceSqr - nearDistanceSqr) / math.max(0.0001f, beamRangeSqr - nearDistanceSqr));
            float beam01 = (0.3f + (facing01 * 0.7f) + heatBoost) * beamWeight * distance01;
            return math.saturate(beam01);
        }
    }
}
