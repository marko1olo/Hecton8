using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Unity.Mathematics;

namespace Hecton8.Core
{
    public static class ExosuitKinematicAuthority
    {
        private static IDataVault s_vault;
        private static VaultGenerationHandle<ExosuitFrameInputDTO> s_inputHandle;
        private static ExosuitFrameInputDTO s_pendingInput;
        private static uint s_pendingSequence;
        private static bool s_bound;
        private static bool s_hasPendingInput;

        public static bool HasActiveAuthority()
        {
            return s_bound &&
                   s_vault != null &&
                   s_inputHandle.BufferID != 0u &&
                   s_inputHandle.SystemID == (uint)SystemID.Physics;
        }

        public static void Bind(IDataVault vault, in VaultGenerationHandle<ExosuitFrameInputDTO> inputHandle)
        {
            if (vault == null ||
                inputHandle.BufferID == 0u ||
                inputHandle.SystemID != (uint)SystemID.Physics)
            {
                s_vault = null;
                s_inputHandle = default;
                s_pendingInput = default;
                s_pendingSequence = 0u;
                s_bound = false;
                s_hasPendingInput = false;
                return;
            }

            s_vault = vault;
            s_inputHandle = inputHandle;
            s_pendingInput = default;
            s_pendingSequence = 0u;
            s_hasPendingInput = false;
            s_bound = true;
        }

        public static void Unbind(in VaultGenerationHandle<ExosuitFrameInputDTO> inputHandle)
        {
            if (s_inputHandle.BufferID != inputHandle.BufferID ||
                s_inputHandle.SystemID != inputHandle.SystemID)
                return;

            s_vault = null;
            s_inputHandle = default;
            s_pendingInput = default;
            s_pendingSequence = 0u;
            s_bound = false;
            s_hasPendingInput = false;
        }

        public static bool TryConsumePendingFrameInput(out ExosuitFrameInputDTO input)
        {
            input = default;
            if (!HasActiveAuthority() || !s_hasPendingInput)
                return false;

            input = s_pendingInput;
            s_pendingInput = default;
            s_hasPendingInput = false;
            return true;
        }

        public static bool TrySubmitFrameInput(
            float2 moveAxis,
            float verticalAxis,
            float desiredYawRadians,
            uint actionMask,
            float globalQualityWeight)
        {
            if (!HasActiveAuthority())
                return false;

            ExosuitFrameInputDTO input = default;
            input.MoveAxis = new float2(
                math.clamp(math.isfinite(moveAxis.x) ? moveAxis.x : 0.0f, -1.0f, 1.0f),
                math.clamp(math.isfinite(moveAxis.y) ? moveAxis.y : 0.0f, -1.0f, 1.0f));
            input.VerticalAxis = math.clamp(math.isfinite(verticalAxis) ? verticalAxis : 0.0f, -1.0f, 1.0f);
            input.DesiredYawRadians = WrapRadians(desiredYawRadians);
            input.ActionMask = actionMask | ExosuitInputActions.ExternalAuthority;
            input.GlobalQualityWeight = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1.0f);
            input.Frame = ++s_pendingSequence;
            if (s_pendingSequence == 0u)
            {
                s_pendingSequence = 1u;
                input.Frame = 1u;
            }

            s_pendingInput = input;
            s_hasPendingInput = true;
            return true;
        }

        private static float WrapRadians(float value)
        {
            const float TwoPi = 6.2831853071795864769f;
            const float InvTwoPi = 0.1591549430918953358f;
            if (!math.isfinite(value))
                return 0.0f;

            return value - math.round(value * InvTwoPi) * TwoPi;
        }
    }
}
