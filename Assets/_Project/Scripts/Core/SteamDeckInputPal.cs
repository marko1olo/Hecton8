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

        private static Gamepad _boundGamepad;
        private static bool _deckInputAvailable;
        private static bool _gyroEnableAttempted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _boundGamepad = null;
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
            if (LengthSq(left) > TrackpadDeadzoneSq)
            {
                state.SteamDeckLeftTrackpad = left;
                state.PlatformInputFlags |= (uint)PlatformInputFlag.SteamDeckLeftTrackpad;
            }

            if (LengthSq(right) > TrackpadDeadzoneSq)
            {
                state.SteamDeckRightTrackpad = right;
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
                return Vector2.zero;

            Vector3 angularVelocity = gyro.angularVelocity.ReadValue();
            float2 axis = new float2(angularVelocity.y, -angularVelocity.x);
            if (!math.all(math.isfinite(axis)) || math.lengthsq(axis) <= GyroNoiseFloorSq)
                return Vector2.zero;

            float safeDeltaTime = math.isfinite(deltaTime) ? math.min(math.max(0f, deltaTime), 0.05f) : 0f;
            float2 delta = axis * (GyroSensitivity * safeDeltaTime);
            delta = math.clamp(delta, new float2(-MaxGyroDelta), new float2(MaxGyroDelta));
            return new Vector2(delta.x, delta.y);
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
        private static bool ContainsIgnoreCase(string value, string token)
        {
            return !string.IsNullOrEmpty(value) &&
                   value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
