using UnityEngine;

namespace Hecton8.Physics
{
    /// <summary>
    /// Authored tether tuning profile shared by runtime tether solvers.
    /// </summary>
    [CreateAssetMenu(
        fileName = "TetherProfile_TowCable",
        menuName = "Hecton8/Physics/Tether Profile",
        order = 120)]
    public sealed class TetherProfileSO : ScriptableObject
    {
        [Header("── Constraint ──────────────────")]
        [Tooltip("Primary spring stiffness used by the tether acceleration solver in N/m-equivalent units.")]
        [SerializeField, Min(0f)] private float springStiffness_k = 1200f;

        [Tooltip("Multiplier applied on top of the critically damped coefficient c = 2 * sqrt(k * reducedMass).")]
        [SerializeField, Min(1f)] private float overDampingMultiplier = 1.2f;

        [Tooltip("Peak tension threshold that must be exceeded before the tether begins accumulating snap stress.")]
        [SerializeField, Min(1f)] private float snapTensionThreshold = 1800f;

        /// <summary>Primary spring stiffness used by the tether solver.</summary>
        public float SpringStiffness => springStiffness_k;

        /// <summary>Multiplier applied to the critically damped coefficient.</summary>
        public float OverDampingMultiplier => overDampingMultiplier;

        /// <summary>Tension threshold required to accumulate snap stress.</summary>
        public float SnapTensionThreshold => snapTensionThreshold;
    }
}
