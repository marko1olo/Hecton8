using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Authored baseline submarine stats consumed by root submarine runtime owners.
    /// </summary>
    [CreateAssetMenu(fileName = "SubmarineProfile_", menuName = "Hecton8/Gameplay/Submarine Profile")]
    public sealed class SubmarineProfile : ScriptableObject
    {
        [Header("── Hull Baseline ─────────────────")]
        [Tooltip("Baseline rigidbody mass in kilograms applied to the submarine hull.")]
        [SerializeField, Min(1f)] private float baseMass = 1200f;

        [Tooltip("Baseline propulsion thrust ceiling in Newtons.")]
        [SerializeField, Min(0f)] private float maxThrust = 16000f;

        [Tooltip("Baseline yaw-turn speed in degrees per second.")]
        [SerializeField, Min(0f)] private float turnSpeed = 35f;

        [Tooltip("Maximum certified operating depth in meters before upgrades.")]
        [SerializeField, Min(0f)] private float maxDepth = 400f;

        [Tooltip("Baseline structural integrity before installed upgrades.")]
        [SerializeField, Min(1f)] private float baseIntegrity = 250f;

        /// <summary>Baseline rigidbody mass in kilograms.</summary>
        public float BaseMass => Mathf.Max(1f, baseMass);

        /// <summary>Baseline propulsion thrust ceiling in Newtons.</summary>
        public float MaxThrust => Mathf.Max(0f, maxThrust);

        /// <summary>Baseline yaw-turn speed in degrees per second.</summary>
        public float TurnSpeed => Mathf.Max(0f, turnSpeed);

        /// <summary>Maximum certified operating depth in meters.</summary>
        public float MaxDepth => Mathf.Max(0f, maxDepth);

        /// <summary>Baseline structural integrity before upgrades.</summary>
        public float BaseIntegrity => Mathf.Max(1f, baseIntegrity);
    }
}
