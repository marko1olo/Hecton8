using Hecton8.Core;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    public sealed class HectonWorldShellController1428 : MonoBehaviour, IBootstrapLegacyWorldShellOwner
    {
        // Legacy scene shell marker only. Production player movement, input, and camera
        // authority belong to Player.prefab owner components, not this scene-local shell.
#pragma warning disable 0414, 0649
        [SerializeField] private Transform cameraRig;
        [SerializeField] private float moveSpeed = 7.5f;
        [SerializeField] private float verticalSpeed = 4.0f;
        [SerializeField] private float lookSpeed = 0.11f;
        [SerializeField] private float idleDriftMeters = 0.18f;
#pragma warning restore 0414, 0649
    }
}
