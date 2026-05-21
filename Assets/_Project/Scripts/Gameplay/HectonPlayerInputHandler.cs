using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Zero-allocation reader for frame-cached player input snapshots.
    /// </summary>
    internal struct HectonPlayerInputHandler
    {
        public bool TryReadFrame(
            IInputService inputService,
            float jumpBufferSeconds,
            out HectonPlayerInputFrame frame,
            out bool jumpBuffered)
        {
            return TryReadFrame(
                inputService,
                jumpBufferSeconds,
                consumeBufferedJump: true,
                out frame,
                out jumpBuffered);
        }

        public bool TryReadFrame(
            IInputService inputService,
            float jumpBufferSeconds,
            bool consumeBufferedJump,
            out HectonPlayerInputFrame frame,
            out bool jumpBuffered)
        {
            frame = default;
            jumpBuffered = false;

            if (inputService == null || !inputService.IsPlayerInputEnabled)
                return false;

            PlayerInputState state = inputService.GetState();
            Vector2 moveInput = state.MoveDelta;
            Vector2 lookInput = state.LookDelta;
            float verticalInput = state.VerticalDelta;
            if (!IsFinite(moveInput) || !IsFinite(lookInput) || !math.isfinite(verticalInput))
                return false;

            frame = new HectonPlayerInputFrame(
                state,
                ClampUnitInput(moveInput),
                lookInput,
                math.clamp(verticalInput, -1f, 1f),
                state.HasAction(PlayerInputAction.Sprint));
            jumpBuffered = consumeBufferedJump && inputService.TryConsumeBufferedAction(PlayerBufferedAction.Jump, jumpBufferSeconds);
            return true;
        }

        public static float ResolveVerticalInput(in PlayerInputState state)
        {
            return math.clamp(state.VerticalDelta, -1f, 1f);
        }

        private static bool IsFinite(Vector2 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y);
        }

        private static Vector2 ClampUnitInput(Vector2 value)
        {
            float lengthSq = (value.x * value.x) + (value.y * value.y);
            if (lengthSq <= 1f)
                return value;

            float inverseLength = math.rsqrt(lengthSq);
            return new Vector2(value.x * inverseLength, value.y * inverseLength);
        }
    }

    /// <summary>
    /// Value snapshot consumed by the locomotion orchestrator after input service reads.
    /// </summary>
    internal readonly struct HectonPlayerInputFrame
    {
        public HectonPlayerInputFrame(
            PlayerInputState state,
            Vector2 moveInput,
            Vector2 lookInput,
            float verticalInput,
            bool sprintHeld)
        {
            State = state;
            MoveInput = moveInput;
            LookInput = lookInput;
            VerticalInput = verticalInput;
            SprintHeld = sprintHeld ? (byte)1 : (byte)0;
        }

        public readonly PlayerInputState State;
        public readonly Vector2 MoveInput;
        public readonly Vector2 LookInput;
        public readonly float VerticalInput;
        public readonly byte SprintHeld;
    }
}
