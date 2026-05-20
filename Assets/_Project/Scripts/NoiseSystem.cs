using Hecton8.World;
using Unity.Mathematics;
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
        private const float PlayerNoiseMemorySeconds = 10f;
        private const float ActiveSonarMemorySeconds = 8f;
        private const int MaxNoiseListenerCount = 64;

        /// <summary>
        /// Snapshot of player-generated noise state for the current frame window.
        /// </summary>
        public readonly struct PlayerNoiseSignal
        {
            public PlayerNoiseSignal(
                Vector3 position,
                in AbsoluteUniversePosition positionAup,
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
                PositionAup = positionAup;
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

            /// <summary>AUP of the player noise source for long-range sensory math.</summary>
            public AbsoluteUniversePosition PositionAup { get; }

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
        // COLD ALLOC: SpatialQueryHit[64] — centralized fauna noise dispatch buffer — owner: NoiseSystem
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
            AbsoluteUniversePosition positionAup = AbsoluteUniversePosition.FromRuntimePosition(position);
            ReportPlayerSignal(
                position,
                in positionAup,
                movementSpeedSqr,
                flashlightOn,
                transportBoost01,
                transportSignature,
                toolUseNoise01);
        }

        /// <summary>
        /// Reports the latest player-noise snapshot with caller-owned AUP to avoid repeat runtime conversion.
        /// </summary>
        public static void ReportPlayerSignal(
            Vector3 position,
            in AbsoluteUniversePosition positionAup,
            float movementSpeedSqr,
            bool flashlightOn,
            float transportBoost01,
            float transportSignature,
            float toolUseNoise01)
        {
            _playerNoiseSignal = new PlayerNoiseSignal(
                position,
                in positionAup,
                math.max(0f, movementSpeedSqr),
                flashlightOn,
                math.saturate(transportBoost01),
                math.max(0f, transportSignature),
                math.saturate(toolUseNoise01),
                Time.frameCount);
            _hasPlayerNoiseSignal = true;
            float transientRadius = ResolveDispatchRadius(_playerNoiseSignal);
            if (transientRadius > 0f)
            {
                WorldSpatialHashGrid.RegisterTransientEvent(
                    position,
                    in positionAup,
                    transientRadius,
                    ResolveSignalIntensity01(_playerNoiseSignal),
                    PlayerNoiseMemorySeconds,
                    SpatialTransientEventType.AcousticImpulse,
                    SpatialInteractionFlags.AcousticReceiver);
            }

            DispatchPlayerSignal(_playerNoiseSignal);
        }

        /// <summary>
        /// Reports an active sonar ping through the fauna hearing path with acoustic shadow validation.
        /// </summary>
        public static void ReportActiveSonarPing(Vector3 position, float intensity01)
        {
            float clampedIntensity = math.saturate(intensity01);
            AbsoluteUniversePosition positionAup = AbsoluteUniversePosition.FromRuntimePosition(position);
            _playerNoiseSignal = new PlayerNoiseSignal(
                position,
                in positionAup,
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
            WorldSpatialHashGrid.RegisterTransientEvent(
                position,
                in positionAup,
                ActiveSonarDetectionRadius,
                clampedIntensity,
                ActiveSonarMemorySeconds,
                SpatialTransientEventType.AcousticImpulse,
                SpatialInteractionFlags.AcousticReceiver);
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

            Vector3 position = signal.Position;
            AbsoluteUniversePosition positionAup = signal.PositionAup;
            int count = WorldSpatialHashGrid.CollectContactsNonAlloc(
                position,
                in positionAup,
                dispatchRadius,
                SpatialTargetKind.Bioform,
                SpatialInteractionFlags.AcousticReceiver,
                _playerNoiseListenerBuffer);

            for (int i = 0; i < count; i++)
            {
                if (_playerNoiseListenerBuffer[i].Owner is FaunaBrain brain)
                    brain.ReceivePlayerNoiseSignal(signal);
            }
        }

        private static void DispatchActiveSonarPing(PlayerNoiseSignal signal)
        {
            Vector3 position = signal.Position;
            AbsoluteUniversePosition positionAup = signal.PositionAup;
            int count = WorldSpatialHashGrid.CollectContactsNonAlloc(
                position,
                in positionAup,
                ActiveSonarDetectionRadius,
                SpatialTargetKind.Bioform,
                SpatialInteractionFlags.AcousticReceiver,
                _playerNoiseListenerBuffer);

            for (int i = 0; i < count; i++)
            {
                SpatialQueryHit listener = _playerNoiseListenerBuffer[i];
                if (!(listener.Owner is FaunaBrain brain) || listener.Transform == null)
                    continue;

                AcousticOcclusionResult occlusion = AcousticOcclusionUtility.EvaluateOcclusionPath(
                    position,
                    listener.Position,
                    SensoryOcclusionMask,
                    null,
                    listener.Transform.root);
                if (occlusion.Transmission01 < AcousticOcclusionUtility.DeepShadowTransmissionThreshold)
                    continue;

                PlayerNoiseSignal transmittedSignal = new PlayerNoiseSignal(
                    signal.Position,
                    in positionAup,
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
                float movementRadius = LerpClamped(
                    MinimumMovementNoiseRadius,
                    MaximumMovementNoiseRadius,
                    InverseLerpClamped(MinimumMovementNoiseSqr, 72.25f, signal.MovementSpeedSqr));
                dispatchRadius = math.max(dispatchRadius, movementRadius);
            }

            if (signal.ToolUseNoise01 > 0f)
            {
                float toolRadius = LerpClamped(
                    MinimumToolNoiseRadius,
                    MaximumToolNoiseRadius,
                    signal.ToolUseNoise01);
                dispatchRadius = math.max(dispatchRadius, toolRadius);
            }

            if (signal.TransportBoost01 > 0f)
            {
                float transportSignature = math.max(1f, signal.TransportSignature);
                float transportRadius = LerpClamped(
                    MinimumTransportNoiseRadius,
                    MaximumTransportNoiseRadius * transportSignature,
                    signal.TransportBoost01);
                dispatchRadius = math.max(dispatchRadius, transportRadius);
            }

            if (signal.IsActiveSonarPing)
                dispatchRadius = math.max(dispatchRadius, signal.SignalRadiusMeters);

            return dispatchRadius;
        }

        private static float LerpClamped(float from, float to, float t)
        {
            return from + (to - from) * math.saturate(t);
        }

        private static float InverseLerpClamped(float from, float to, float value)
        {
            float range = to - from;
            return range > 0.000001f
                ? math.saturate((value - from) / range)
                : 0f;
        }

        private static float ResolveSignalIntensity01(PlayerNoiseSignal signal)
        {
            float movementIntensity = signal.MovementSpeedSqr > 0f
                ? InverseLerpClamped(MinimumMovementNoiseSqr, 72.25f, signal.MovementSpeedSqr)
                : 0f;
            float utilityIntensity = math.max(signal.ToolUseNoise01, signal.TransportBoost01);
            if (signal.FlashlightOn)
                utilityIntensity = math.max(utilityIntensity, 0.25f);
            if (signal.IsActiveSonarPing)
                utilityIntensity = math.max(utilityIntensity, signal.ToolUseNoise01 * signal.AcousticTransmission01);
            return math.saturate(math.max(movementIntensity, utilityIntensity));
        }

        public static float EvaluatePlayerNoise01(
            Vector3 listenerPosition,
            Transform playerTransform,
            Rigidbody playerBody)
        {
            if (TryGetPlayerSignal(out PlayerNoiseSignal signal))
            {
                float distanceSqr = (listenerPosition - signal.Position).sqrMagnitude;
                float speed01 = InverseLerpClamped(0.5625f, 72.25f, signal.MovementSpeedSqr);
                float maxDistance = signal.SignalRadiusMeters > 0f ? signal.SignalRadiusMeters : 42f;
                float distance01 = 1f - InverseLerpClamped(36f, maxDistance * maxDistance, distanceSqr);
                float pulse01 = math.max(signal.TransportBoost01, signal.ToolUseNoise01);
                if (signal.IsActiveSonarPing)
                    pulse01 = math.max(pulse01, signal.ToolUseNoise01 * signal.AcousticTransmission01);
                return math.saturate(math.max(speed01 * distance01, pulse01 * distance01));
            }

            return 0f;
        }
    }
}
