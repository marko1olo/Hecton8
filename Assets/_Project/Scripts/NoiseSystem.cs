using UnityEngine;

namespace Hecton8.AI
{
    /// <summary>
    /// Простой расчёт шума игрока для существ.
    /// Пока без глобальной шины событий: шум считается по скорости игрока и расстоянию.
    /// </summary>
    public static class NoiseSystem
    {
        public static float EvaluatePlayerNoise01(
            Vector3 listenerPosition,
            Transform playerTransform,
            Rigidbody playerBody)
        {
            if (playerTransform == null || playerBody == null)
                return 0f;

            float speed = playerBody.linearVelocity.magnitude;
            if (speed <= 0.1f)
                return 0f;

            float distance = Vector3.Distance(listenerPosition, playerTransform.position);
            float speed01 = Mathf.InverseLerp(0.75f, 8.5f, speed);
            float distance01 = 1f - Mathf.InverseLerp(6f, 42f, distance);
            return Mathf.Clamp01(speed01 * distance01);
        }
    }
}
