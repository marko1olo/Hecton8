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

        private Transform _cachedTransform;
        private float _requestAccumulator;

        private void Awake()
        {
            _cachedTransform = transform;
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            _cachedTransform = transform;
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
            Transform owner = _cachedTransform != null ? _cachedTransform : transform;
            Transform anchor = chestTarget != null ? chestTarget : owner;
            float lifetime = math.max(0.01f, requestLifetimeSeconds);
            AuxiliaryEquipmentRouterRuntime.TryDeployGravityTether(owner.position, anchor.position, lifetime);
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
    }
}
