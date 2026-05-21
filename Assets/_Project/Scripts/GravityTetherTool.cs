namespace Hecton8.Gameplay
{
    using Hecton8.Core;
    using Hecton8.Equipment.Auxiliary;
    using Hecton8.Tools;
    using Unity.Mathematics;
    using UnityEngine;

    [DisallowMultipleComponent]
    public sealed class GravityTetherTool : PlayerTool
    {
        [Header("Tether Request")]
        [SerializeField, Min(0.1f)] private float rangeMeters = 8f;
        [SerializeField, Min(0.01f)] private float requestLifetimeSeconds = 0.2f;
        [SerializeField, Min(0.01f)] private float requestIntervalSeconds = 0.1f;
        [SerializeField] private Transform chestTarget;

        private float _requestAccumulator;

        public override void OnSpawn()
        {
            base.OnSpawn();
            _requestAccumulator = 0f;
        }

        public override void UsePrimary(float deltaTime)
        {
            base.UsePrimary(deltaTime);
            if (!IsEquipped)
                return;

            float safeDelta = math.select(0f, deltaTime, math.isfinite(deltaTime) & (deltaTime > 0f));
            _requestAccumulator += safeDelta;
            float interval = math.max(0.01f, requestIntervalSeconds);
            if (_requestAccumulator < interval)
                return;

            _requestAccumulator = 0f;
            if (!TryResolveTetherPose(out Vector3 origin, out Vector3 forward))
                return;

            float lifetime = math.max(0.01f, requestLifetimeSeconds);
            float range = math.max(0.1f, GetRuntimeMaxRange(rangeMeters));
            Vector3 anchorPosition = chestTarget != null ? chestTarget.position : origin;
            Vector3 projectilePosition = chestTarget != null ? origin : origin + (forward * range);
            AuxiliaryEquipmentRouterRuntime.TryDeployGravityTether(projectilePosition, anchorPosition, lifetime);
        }

        public override void WriteOperationalSummary(ref FixedCharBuffer buffer)
        {
            AppendText(ref buffer, "GRAV TETHER ROUTER // RNG ");
            buffer.AppendFloat(math.max(0.1f, GetRuntimeMaxRange(rangeMeters)), 1);
            AppendText(ref buffer, "M");
        }

        public override void WriteOperationalDirective(ref FixedCharBuffer buffer)
        {
            AppendText(ref buffer, IsEquipped ? "Primary routes AUP tether packets." : "Tether stowed.");
        }

        protected override void ConfigureModularRuntimeProfile(ref ToolRuntimeProfile profile)
        {
            profile.MaxRange = math.max(0.1f, rangeMeters);
            profile.PowerScalar = 1f;
        }

        private static bool AppendText(ref FixedCharBuffer buffer, string value)
        {
            return buffer.Append(value);
        }

        private bool TryResolveTetherPose(out Vector3 origin, out Vector3 forward)
        {
            origin = default;
            forward = default;
            if (!TryGetPlayerRuntimeContext(out IPlayerRuntimeContext playerContext) ||
                !playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot))
            {
                return false;
            }

            float3 runtimePosition = snapshot.RuntimePosition;
            float3 runtimeForward = snapshot.Forward;
            float forwardLengthSq = math.lengthsq(runtimeForward);
            if (!math.all(math.isfinite(runtimePosition)) ||
                !math.all(math.isfinite(runtimeForward)) ||
                !math.isfinite(forwardLengthSq) ||
                forwardLengthSq <= 0.0001f)
            {
                return false;
            }

            float invForwardLength = math.rsqrt(math.max(forwardLengthSq, 0.0001f));
            origin = new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            forward = new Vector3(
                runtimeForward.x * invForwardLength,
                runtimeForward.y * invForwardLength,
                runtimeForward.z * invForwardLength);
            return true;
        }
    }
}
