using Hecton8.World;
using UnityEngine;

namespace Hecton8.AI
{
    /// <summary>
    /// Global player-noise snapshot consumed by fauna and other awareness systems.
    /// </summary>
    public static class NoiseSystem
    {
        private const float MinimumMovementNoiseSqr = 0.25f;
        private const float MinimumMovementNoiseRadius = 12f;
        private const float MaximumMovementNoiseRadius = 42f;
        private const float FlashlightNoiseRadius = 30f;
        private const float MinimumToolNoiseRadius = 18f;
        private const float MaximumToolNoiseRadius = 48f;
        private const float MinimumTransportNoiseRadius = 28f;
        private const float MaximumTransportNoiseRadius = 96f;
        private const int MaxNoiseListenerCount = 256;

        /// <summary>
        /// Snapshot of player-generated noise state for the current frame window.
        /// </summary>
        public readonly struct PlayerNoiseSignal
        {
            public PlayerNoiseSignal(
                Vector3 position,
                float movementSpeedSqr,
                bool flashlightOn,
                float transportBoost01,
                float transportSignature,
                float toolUseNoise01,
                int reportedFrame)
            {
                Position = position;
                MovementSpeedSqr = movementSpeedSqr;
                FlashlightOn = flashlightOn;
                TransportBoost01 = transportBoost01;
                TransportSignature = transportSignature;
                ToolUseNoise01 = toolUseNoise01;
                ReportedFrame = reportedFrame;
            }

            /// <summary>World position of the player noise source.</summary>
            public Vector3 Position { get; }

            /// <summary>Squared player movement speed at the time of the report.</summary>
            public float MovementSpeedSqr { get; }

            /// <summary>True when the player flashlight is active.</summary>
            public bool FlashlightOn { get; }

            /// <summary>Normalized transport boost reported by the active locomotion owner.</summary>
            public float TransportBoost01 { get; }

            /// <summary>Species-facing transport detection signature multiplier.</summary>
            public float TransportSignature { get; }

            /// <summary>Short pulse emitted by active tool use.</summary>
            public float ToolUseNoise01 { get; }

            /// <summary>Frame index when the signal was last reported.</summary>
            public int ReportedFrame { get; }
        }

        private const int MaxPlayerSignalAgeFrames = 30;
        private static PlayerNoiseSignal _playerNoiseSignal;
        private static bool _hasPlayerNoiseSignal;
        // COLD ALLOC: SpatialQueryHit[256] — centralized fauna noise dispatch buffer — owner: NoiseSystem
        private static readonly SpatialQueryHit[] _playerNoiseListenerBuffer = new SpatialQueryHit[MaxNoiseListenerCount];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _playerNoiseSignal = default;
            _hasPlayerNoiseSignal = false;
        }

        /// <summary>
        /// Reports the latest player-noise snapshot for consumption by fauna sensors.
        /// </summary>
        public static void ReportPlayerSignal(
            Vector3 position,
            float movementSpeedSqr,
            bool flashlightOn,
            float transportBoost01,
            float transportSignature,
            float toolUseNoise01)
        {
            _playerNoiseSignal = new PlayerNoiseSignal(
                position,
                Mathf.Max(0f, movementSpeedSqr),
                flashlightOn,
                Mathf.Clamp01(transportBoost01),
                Mathf.Max(0f, transportSignature),
                Mathf.Clamp01(toolUseNoise01),
                Time.frameCount);
            _hasPlayerNoiseSignal = true;
            DispatchPlayerSignal(_playerNoiseSignal);
        }

        /// <summary>
        /// Clears the cached player-noise snapshot.
        /// </summary>
        public static void ClearPlayerSignal()
        {
            _playerNoiseSignal = default;
            _hasPlayerNoiseSignal = false;
        }

        /// <summary>
        /// Returns the latest player-noise snapshot when it is still fresh.
        /// </summary>
        public static bool TryGetPlayerSignal(out PlayerNoiseSignal signal)
        {
            if (_hasPlayerNoiseSignal &&
                Time.frameCount - _playerNoiseSignal.ReportedFrame <= MaxPlayerSignalAgeFrames)
            {
                signal = _playerNoiseSignal;
                return true;
            }

            signal = default;
            return false;
        }

        private static void DispatchPlayerSignal(PlayerNoiseSignal signal)
        {
            float dispatchRadius = ResolveDispatchRadius(signal);
            if (dispatchRadius <= 0f)
                return;

            int count = WorldSpatialHashGrid.CollectContactsNonAlloc(
                signal.Position,
                dispatchRadius,
                SpatialTargetKind.Bioform,
                _playerNoiseListenerBuffer);

            for (int i = 0; i < count; i++)
            {
                if (_playerNoiseListenerBuffer[i].Owner is FaunaBrain brain)
                    brain.ReceivePlayerNoiseSignal(signal);
            }
        }

        private static float ResolveDispatchRadius(PlayerNoiseSignal signal)
        {
            float dispatchRadius = 0f;

            if (signal.FlashlightOn)
                dispatchRadius = FlashlightNoiseRadius;

            if (signal.MovementSpeedSqr >= MinimumMovementNoiseSqr)
            {
                float movementSpeed = Mathf.Sqrt(signal.MovementSpeedSqr);
                float movementRadius = Mathf.Lerp(
                    MinimumMovementNoiseRadius,
                    MaximumMovementNoiseRadius,
                    Mathf.InverseLerp(0.5f, 8.5f, movementSpeed));
                dispatchRadius = Mathf.Max(dispatchRadius, movementRadius);
            }

            if (signal.ToolUseNoise01 > 0f)
            {
                float toolRadius = Mathf.Lerp(
                    MinimumToolNoiseRadius,
                    MaximumToolNoiseRadius,
                    signal.ToolUseNoise01);
                dispatchRadius = Mathf.Max(dispatchRadius, toolRadius);
            }

            if (signal.TransportBoost01 > 0f)
            {
                float transportSignature = Mathf.Max(1f, signal.TransportSignature);
                float transportRadius = Mathf.Lerp(
                    MinimumTransportNoiseRadius,
                    MaximumTransportNoiseRadius * transportSignature,
                    signal.TransportBoost01);
                dispatchRadius = Mathf.Max(dispatchRadius, transportRadius);
            }

            return dispatchRadius;
        }

        public static float EvaluatePlayerNoise01(
            Vector3 listenerPosition,
            Transform playerTransform,
            Rigidbody playerBody)
        {
            if (TryGetPlayerSignal(out PlayerNoiseSignal signal))
            {
                float distance = Vector3.Distance(listenerPosition, signal.Position);
                float speed = Mathf.Sqrt(signal.MovementSpeedSqr);
                float speed01 = Mathf.InverseLerp(0.75f, 8.5f, speed);
                float distance01 = 1f - Mathf.InverseLerp(6f, 42f, distance);
                float pulse01 = Mathf.Max(signal.TransportBoost01, signal.ToolUseNoise01);
                return Mathf.Clamp01(Mathf.Max(speed01 * distance01, pulse01 * distance01));
            }

            if (playerTransform == null || playerBody == null)
                return 0f;

            float playerSpeed = playerBody.linearVelocity.magnitude;
            if (playerSpeed <= 0.1f)
                return 0f;

            float playerDistance = Vector3.Distance(listenerPosition, playerTransform.position);
            float playerSpeed01 = Mathf.InverseLerp(0.75f, 8.5f, playerSpeed);
            float playerDistance01 = 1f - Mathf.InverseLerp(6f, 42f, playerDistance);
            return Mathf.Clamp01(playerSpeed01 * playerDistance01);
        }
    }
}
