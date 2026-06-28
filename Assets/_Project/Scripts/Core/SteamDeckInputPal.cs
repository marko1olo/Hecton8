using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;

namespace Hecton8.Core
{
    /// <summary>
    /// Steam Deck platform abstraction layer for gyro aim and trackpad-derived quick input.
    /// </summary>
    internal static class SteamDeckInputPal
    {
        private const float TrackpadDeadzone = 0.12f;
        private const float TrackpadDeadzoneSq = TrackpadDeadzone * TrackpadDeadzone;
        private const float GyroNoiseFloor = 0.018f;
        private const float GyroNoiseFloorSq = GyroNoiseFloor * GyroNoiseFloor;
        private const float GyroSensitivity = 3.2f;
        private const float MaxGyroDelta = 7.5f;
        private const float GyroLowPassCutoffHz = 12f;
        private const float GyroIdleDecayCutoffHz = 18f;
        private const float TwoPi = 6.28318530718f;

        private static Gamepad _boundGamepad;
        private static float2 _gyroEwma;
        private static bool _deckInputAvailable;
        private static bool _gyroEnableAttempted;

        public static bool IsDeckInputAvailable => _deckInputAvailable;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _boundGamepad = null;
            _gyroEwma = float2.zero;
            _deckInputAvailable = false;
            _gyroEnableAttempted = false;
        }

        /// <summary>
        /// Binds the current gamepad once per device change. No per-frame device search.
        /// </summary>
        public static void BindGamepad(Gamepad gamepad)
        {
            _boundGamepad = gamepad != null && gamepad.added ? gamepad : null;
            _deckInputAvailable = HardwareTierDetector.IsSteamDeckLike || IsSteamDeckDevice(_boundGamepad);
            if (!_deckInputAvailable)
                _gyroEwma = float2.zero;
            if (_deckInputAvailable)
                TryEnableGyro();
        }

        /// <summary>
        /// Captures Steam Deck PAL axes into the platform fields of the player input state.
        /// </summary>
        public static void Capture(ref PlayerInputState state, float deltaTime)
        {
            if (!_deckInputAvailable)
                return;

            CaptureTrackpadProxy(ref state);
            Vector2 gyroDelta = CaptureGyroAimDelta(deltaTime);
            if (LengthSq(gyroDelta) > 0f)
            {
                state.SteamDeckGyroAimDelta = gyroDelta;
                state.LookDelta += gyroDelta;
                state.PlatformInputFlags |= (uint)PlatformInputFlag.SteamDeckGyro;
            }
        }

        private static void CaptureTrackpadProxy(ref PlayerInputState state)
        {
            Gamepad gamepad = _boundGamepad;
            if (gamepad == null || !gamepad.added)
                return;

            Vector2 left = gamepad.leftStick.ReadValue();
            Vector2 right = gamepad.rightStick.ReadValue();
            if (TryApplyRadialDeadzone(left, out Vector2 filteredLeft))
            {
                state.SteamDeckLeftTrackpad = filteredLeft;
                state.PlatformInputFlags |= (uint)PlatformInputFlag.SteamDeckLeftTrackpad;
            }

            if (TryApplyRadialDeadzone(right, out Vector2 filteredRight))
            {
                state.SteamDeckRightTrackpad = filteredRight;
                state.PlatformInputFlags |= (uint)PlatformInputFlag.SteamDeckRightTrackpad;
            }

            if (state.SteamDeckLeftTrackpad != Vector2.zero || state.SteamDeckRightTrackpad != Vector2.zero)
                state.PlatformInputFlags |= (uint)PlatformInputFlag.SteamDeckEmulatedTrackpads;
        }

        private static Vector2 CaptureGyroAimDelta(float deltaTime)
        {
            TryEnableGyro();
            UnityEngine.InputSystem.Gyroscope gyro = UnityEngine.InputSystem.Gyroscope.current;
            if (gyro == null || !gyro.enabled)
            {
                _gyroEwma = float2.zero;
                return Vector2.zero;
            }

            float safeDeltaTime = math.isfinite(deltaTime) ? math.min(math.max(0f, deltaTime), 0.05f) : 0f;
            Vector3 angularVelocity = gyro.angularVelocity.ReadValue();
            float2 axis = new float2(angularVelocity.y, -angularVelocity.x);
            float sampleRateHz = safeDeltaTime > 0f ? 1f / safeDeltaTime : 0f;
            if (!math.all(math.isfinite(axis)) || math.lengthsq(axis) <= GyroNoiseFloorSq)
            {
                float decayX = Hecton8.PureLogic.Systems.GyroDriftFilterCalculator.Compute(
                    0f, _gyroEwma.x, GyroIdleDecayCutoffHz, sampleRateHz);
                float decayY = Hecton8.PureLogic.Systems.GyroDriftFilterCalculator.Compute(
                    0f, _gyroEwma.y, GyroIdleDecayCutoffHz, sampleRateHz);
                _gyroEwma = new float2(decayX, decayY);
                return Vector2.zero;
            }

            float2 delta = axis * (GyroSensitivity * safeDeltaTime);
            delta = math.clamp(delta, new float2(-MaxGyroDelta), new float2(MaxGyroDelta));
            float filteredX = Hecton8.PureLogic.Systems.GyroDriftFilterCalculator.Compute(
                delta.x, _gyroEwma.x, GyroLowPassCutoffHz, sampleRateHz);
            float filteredY = Hecton8.PureLogic.Systems.GyroDriftFilterCalculator.Compute(
                delta.y, _gyroEwma.y, GyroLowPassCutoffHz, sampleRateHz);
            _gyroEwma = new float2(filteredX, filteredY);
            return new Vector2(_gyroEwma.x, _gyroEwma.y);
        }

        private static void TryEnableGyro()
        {
            if (_gyroEnableAttempted)
                return;

            _gyroEnableAttempted = true;
            UnityEngine.InputSystem.Gyroscope gyro = UnityEngine.InputSystem.Gyroscope.current;
            if (gyro != null && !gyro.enabled)
                InputSystem.EnableDevice(gyro);
        }

        private static bool IsSteamDeckDevice(InputDevice device)
        {
            if (device == null)
                return false;

            InputDeviceDescription description = device.description;
            return ContainsIgnoreCase(device.name, "steam") ||
                   ContainsIgnoreCase(device.displayName, "steam") ||
                   ContainsIgnoreCase(description.manufacturer, "valve") ||
                   ContainsIgnoreCase(description.product, "steam");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float LengthSq(Vector2 value)
        {
            return (value.x * value.x) + (value.y * value.y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryApplyRadialDeadzone(Vector2 value, out Vector2 filtered)
        {
            float lengthSq = LengthSq(value);
            if (!math.isfinite(lengthSq) || lengthSq <= TrackpadDeadzoneSq)
            {
                filtered = Vector2.zero;
                return false;
            }

            float length = FastLengthFromSq(lengthSq, 0.00000001f);
            float normalized = math.saturate((length - TrackpadDeadzone) * math.rcp(math.max(1f - TrackpadDeadzone, 0.0001f)));
            float scale = normalized * math.rcp(math.max(length, 0.0001f));
            filtered = value * scale;
            return LengthSq(filtered) > 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FastLengthFromSq(float lengthSq, float minLengthSq)
        {
            if (!math.isfinite(lengthSq))
                return 0f;

            float safeLengthSq = math.max(lengthSq, minLengthSq);
            return safeLengthSq > 0f ? safeLengthSq * math.rsqrt(safeLengthSq) : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveLowPassAlpha(float deltaTime, float cutoffHz)
        {
            float safeDeltaTime = math.clamp(math.isfinite(deltaTime) ? deltaTime : 0f, 0f, 0.05f);
            float safeCutoff = math.max(math.isfinite(cutoffHz) ? cutoffHz : GyroLowPassCutoffHz, 0.01f);
            return math.saturate(1f - math.exp(-TwoPi * safeCutoff * safeDeltaTime));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ContainsIgnoreCase(string value, string token)
        {
            return !string.IsNullOrEmpty(value) &&
                   value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
