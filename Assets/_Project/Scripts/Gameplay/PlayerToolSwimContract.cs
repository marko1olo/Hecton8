using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Data-only contract declaring how a held tool should rebalance near-camera swim presentation.
    /// </summary>
    /// <remarks>
    /// This owner does not move the tool and does not own locomotion.
    /// It only informs swim presentation how much of the root/support/tool-hand read should remain visible.
    /// </remarks>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Player Tool Swim Contract")]
    public sealed class PlayerToolSwimContract : MonoBehaviour
    {
        [System.Serializable]
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct PoseBiasData
        {
            [Tooltip("Local position bias applied by this pose contract.")]
            public Vector3 localPositionOffset;

            [Tooltip("Local euler bias applied by this pose contract.")]
            public Vector3 localEulerOffset;

            public PoseBiasData(Vector3 localPositionOffset, Vector3 localEulerOffset)
            {
                this.localPositionOffset = localPositionOffset;
                this.localEulerOffset = localEulerOffset;
            }
        }

        [Header("── Ownership ──────────────────────────")]
        [Tooltip("Which hand is primarily occupied by this held tool.")]
        [SerializeField] private PlayerToolSwimHandedness toolHand = PlayerToolSwimHandedness.Right;

        [Header("── Swim Presentation Blend ───────────")]
        [Tooltip("How much root-level swim presentation remains visible while this tool is equipped.")]
        [SerializeField, Range(0f, 1f)] private float swimRootPresentationWeight = 0.42f;

        [Tooltip("How much of the non-tool support hand remains visible while this tool is equipped.")]
        [SerializeField, Range(0f, 1f)] private float swimSupportHandWeight = 0.68f;

        [Tooltip("How much of the tool-owning hand remains visible while this tool is equipped but idle.")]
        [SerializeField, Range(0f, 1f)] private float swimToolHandWeight = 0.12f;

        [Tooltip("Extra support-hand visibility while the tool is actively being used.")]
        [SerializeField, Range(0f, 0.5f)] private float activeUseSupportHandBoost = 0.16f;

        [Header("── Swim Pose Bias ─────────────────────")]
        [Tooltip("Root-level swim pose bias while this tool is equipped.")]
        [SerializeField] private PoseBiasData swimRootPoseBias;

        [Tooltip("Support-hand swim pose bias while this tool is equipped.")]
        [SerializeField] private PoseBiasData swimSupportHandPoseBias;

        [Tooltip("Tool-hand swim pose bias while this tool is equipped.")]
        [SerializeField] private PoseBiasData swimToolHandPoseBias;

        [Tooltip("Extra root-level swim pose bias while the tool is actively being used.")]
        [SerializeField] private PoseBiasData activeUseRootPoseBias;

        [Tooltip("Extra support-hand swim pose bias while the tool is actively being used.")]
        [SerializeField] private PoseBiasData activeUseSupportHandPoseBias;

        /// <summary>Primary near-camera hand owned by the tool.</summary>
        public PlayerToolSwimHandedness ToolHand => toolHand;

        /// <summary>Root-level swim presentation weight while this tool is equipped.</summary>
        public float SwimRootPresentationWeight => swimRootPresentationWeight;

        /// <summary>Support-hand swim presentation weight while this tool is equipped.</summary>
        public float SwimSupportHandWeight => swimSupportHandWeight;

        /// <summary>Tool-hand swim presentation weight while this tool is equipped and idle.</summary>
        public float SwimToolHandWeight => swimToolHandWeight;

        /// <summary>Additional support-hand weight while this tool is actively being used.</summary>
        public float ActiveUseSupportHandBoost => activeUseSupportHandBoost;

        /// <summary>Root-level swim local position bias while this tool is equipped.</summary>
        public Vector3 SwimRootLocalPositionOffset => swimRootPoseBias.localPositionOffset;

        /// <summary>Root-level swim local euler bias while this tool is equipped.</summary>
        public Vector3 SwimRootLocalEulerOffset => swimRootPoseBias.localEulerOffset;

        /// <summary>Support-hand swim local position bias while this tool is equipped.</summary>
        public Vector3 SwimSupportHandLocalPositionOffset => swimSupportHandPoseBias.localPositionOffset;

        /// <summary>Support-hand swim local euler bias while this tool is equipped.</summary>
        public Vector3 SwimSupportHandLocalEulerOffset => swimSupportHandPoseBias.localEulerOffset;

        /// <summary>Tool-hand swim local position bias while this tool is equipped.</summary>
        public Vector3 SwimToolHandLocalPositionOffset => swimToolHandPoseBias.localPositionOffset;

        /// <summary>Tool-hand swim local euler bias while this tool is equipped.</summary>
        public Vector3 SwimToolHandLocalEulerOffset => swimToolHandPoseBias.localEulerOffset;

        /// <summary>Additional root swim local position bias while this tool is actively being used.</summary>
        public Vector3 ActiveUseRootLocalPositionOffset => activeUseRootPoseBias.localPositionOffset;

        /// <summary>Additional root swim local euler bias while this tool is actively being used.</summary>
        public Vector3 ActiveUseRootLocalEulerOffset => activeUseRootPoseBias.localEulerOffset;

        /// <summary>Additional support-hand swim local position bias while this tool is actively being used.</summary>
        public Vector3 ActiveUseSupportHandLocalPositionOffset => activeUseSupportHandPoseBias.localPositionOffset;

        /// <summary>Additional support-hand swim local euler bias while this tool is actively being used.</summary>
        public Vector3 ActiveUseSupportHandLocalEulerOffset => activeUseSupportHandPoseBias.localEulerOffset;
    }
}
