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
        private const float ActiveSonarDetectionRadius = 80f;
        private const int MaxNoiseListenerCount = 256;
        private const int MaxAcousticOcclusionHits = 8;

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
                int reportedFrame,
                bool isActiveSonarPing = false,
                float acousticTransmission01 = 1f,
                float acousticLowPassCutoffHz = AcousticOcclusionUtility.OpenLowPassCutoffHertz,
                float signalRadiusMeters = 0f)
            {
                Position = position;
                MovementSpeedSqr = movementSpeedSqr;
                FlashlightOn = flashlightOn;
                TransportBoost01 = transportBoost01;
                TransportSignature = transportSignature;
                ToolUseNoise01 = toolUseNoise01;
                ReportedFrame = reportedFrame;
                IsActiveSonarPing = isActiveSonarPing;
                AcousticTransmission01 = acousticTransmission01;
                AcousticLowPassCutoffHz = acousticLowPassCutoffHz;
                SignalRadiusMeters = signalRadiusMeters;
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

            /// <summary>True when the signal originates from an active sonar ping.</summary>
            public bool IsActiveSonarPing { get; }

            /// <summary>Acoustic transmission factor after path absorption is applied.</summary>
            public float AcousticTransmission01 { get; }

            /// <summary>Occlusion-derived low-pass cutoff for the path.</summary>
            public float AcousticLowPassCutoffHz { get; }

            /// <summary>Authored audible radius for downstream evaluators.</summary>
            public float SignalRadiusMeters { get; }
        }

        private const int MaxPlayerSignalAgeFrames = 30;
        private static readonly int SensoryOcclusionMask = AcousticOcclusionUtility.BuildSensoryMask();
        private static PlayerNoiseSignal _playerNoiseSignal;
        private static bool _hasPlayerNoiseSignal;
        // COLD ALLOC: SpatialQueryHit[256] — centralized fauna noise dispatch buffer — owner: NoiseSystem
        private static readonly SpatialQueryHit[] _playerNoiseListenerBuffer = new SpatialQueryHit[MaxNoiseListenerCount];
        // COLD ALLOC: RaycastHit[8] â€” active-sonar occlusion chain buffer â€” owner: NoiseSystem
        private static readonly RaycastHit[] _activeSonarOcclusionHits = new RaycastHit[MaxAcousticOcclusionHits];

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
        /// Reports an active sonar ping through the fauna hearing path with acoustic shadow validation.
        /// </summary>
        public static void ReportActiveSonarPing(Vector3 position, float intensity01)
        {
            float clampedIntensity = Mathf.Clamp01(intensity01);
            _playerNoiseSignal = new PlayerNoiseSignal(
                position,
                0f,
                false,
                0f,
                0f,
                clampedIntensity,
                Time.frameCount,
                true,
                1f,
                AcousticOcclusionUtility.OpenLowPassCutoffHertz,
                ActiveSonarDetectionRadius);
            _hasPlayerNoiseSignal = true;
            DispatchActiveSonarPing(_playerNoiseSignal);
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

        private static void DispatchActiveSonarPing(PlayerNoiseSignal signal)
        {
            int count = WorldSpatialHashGrid.CollectContactsNonAlloc(
                signal.Position,
                ActiveSonarDetectionRadius,
                SpatialTargetKind.Bioform,
                _playerNoiseListenerBuffer);

            for (int i = 0; i < count; i++)
            {
                SpatialQueryHit listener = _playerNoiseListenerBuffer[i];
                if (!(listener.Owner is FaunaBrain brain) || listener.Transform == null)
                    continue;

                AcousticOcclusionResult occlusion = AcousticOcclusionUtility.EvaluateOcclusionPath(
                    signal.Position,
                    listener.Position,
                    SensoryOcclusionMask,
                    _activeSonarOcclusionHits,
                    null,
                    listener.Transform.root);
                if (occlusion.Transmission01 < AcousticOcclusionUtility.DeepShadowTransmissionThreshold)
                    continue;

                PlayerNoiseSignal transmittedSignal = new PlayerNoiseSignal(
                    signal.Position,
                    0f,
                    false,
                    0f,
                    0f,
                    signal.ToolUseNoise01 * occlusion.Transmission01,
                    Time.frameCount,
                    true,
                    occlusion.Transmission01,
                    occlusion.LowPassCutoffHz,
                    ActiveSonarDetectionRadius);
                brain.ReceivePlayerNoiseSignal(transmittedSignal);
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

            if (signal.IsActiveSonarPing)
                dispatchRadius = Mathf.Max(dispatchRadius, signal.SignalRadiusMeters);

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
                float maxDistance = signal.SignalRadiusMeters > 0f ? signal.SignalRadiusMeters : 42f;
                float distance01 = 1f - Mathf.InverseLerp(6f, maxDistance, distance);
                float pulse01 = Mathf.Max(signal.TransportBoost01, signal.ToolUseNoise01);
                if (signal.IsActiveSonarPing)
                    pulse01 = Mathf.Max(pulse01, signal.ToolUseNoise01 * signal.AcousticTransmission01);
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
