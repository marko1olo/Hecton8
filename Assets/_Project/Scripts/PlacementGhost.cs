using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Building
{
    [DisallowMultipleComponent]
    public sealed class PlacementGhost : MonoBehaviour, IFixedTickable, IPoolable
    {
        [Header("Materials")]
        [Tooltip("Green hologram material shown when placement is valid.")]
        [SerializeField] private Material validMaterial;

        [Tooltip("Red hologram material shown when placement is blocked.")]
        [SerializeField] private Material invalidMaterial;

        [Header("Collision Check")]
        [Tooltip("Half extents of the collision test volume.")]
        [SerializeField] private Vector3 checkHalfExtents = new Vector3(1f, 0.5f, 1f);

        [Tooltip("Center offset of the collision test volume in local space.")]
        [SerializeField] private Vector3 checkCenterOffset = Vector3.zero;

        [Tooltip("Layers that block module placement. Exclude the ghost layer.")]
        [SerializeField] private LayerMask blockingMask = ~0;

        [Tooltip("Small shrink factor to allow flush wall-to-wall socket placement.")]
        [SerializeField] private float checkShrink = 0.02f;

        private Renderer[] _renderers;
        private Collider[] _ownColliders;
        private bool _canBuild = true;
        private bool _collisionValid = true;
        private bool _externalValid = true;
        private bool _lastVisualState = true;
        private bool _registeredFixedTick;

        // COLD ALLOC: Collider[32] — shared non-alloc overlap buffer for placement blocking checks — owner: PlacementGhost
        private static readonly Collider[] OverlapBuffer = new Collider[32];

        /// <summary>
        /// True when both collision and external placement gates are valid.
        /// </summary>
        public bool CanBuild => _canBuild;

        /// <summary>
        /// Applies a semantic or structural validity gate from the builder owner.
        /// </summary>
        public void SetExternalValidity(bool isValid)
        {
            if (_externalValid == isValid)
                return;

            _externalValid = isValid;
            RefreshBuildState();
        }

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _ownColliders = GetComponentsInChildren<Collider>(true);
        }

        private void OnEnable()
        {
            TryRegisterFixedTickable();
        }

        private void OnDisable()
        {
            TryUnregisterFixedTickable();
        }

        public void OnSpawn()
        {
            _canBuild = true;
            _collisionValid = true;
            _externalValid = true;
            _lastVisualState = true;
            ApplyMaterial(validMaterial);
        }

        public void OnDespawn()
        {
            _canBuild = false;
            _collisionValid = false;
            _externalValid = false;
            _lastVisualState = false;
        }

        public void FixedTick(float fixedDeltaTime)
        {
            Vector3 center = transform.TransformPoint(checkCenterOffset);
            Vector3 halfExtents = checkHalfExtents - Vector3.one * checkShrink;
            Quaternion rotation = transform.rotation;

            int overlapCount = UnityEngine.Physics.OverlapBoxNonAlloc(
                center,
                halfExtents,
                OverlapBuffer,
                rotation,
                blockingMask,
                QueryTriggerInteraction.Ignore);

            bool blocked = false;
            for (int i = 0; i < overlapCount; i++)
            {
                if (IsOwnCollider(OverlapBuffer[i]))
                    continue;

                blocked = true;
                break;
            }

            _collisionValid = !blocked;
            RefreshBuildState();
        }

        private bool IsOwnCollider(Collider collider)
        {
            for (int i = 0, length = _ownColliders.Length; i < length; i++)
            {
                if (ReferenceEquals(_ownColliders[i], collider))
                    return true;
            }

            return false;
        }

        private void RefreshBuildState()
        {
            _canBuild = _collisionValid && _externalValid;

            if (_canBuild == _lastVisualState)
                return;

            _lastVisualState = _canBuild;
            ApplyMaterial(_canBuild ? validMaterial : invalidMaterial);
        }

        private void ApplyMaterial(Material material)
        {
            if (material == null)
                return;

            for (int i = 0, length = _renderers.Length; i < length; i++)
            {
                Renderer renderer = _renderers[i];
                if (renderer != null)
                    renderer.sharedMaterial = material;
            }
        }

        private void TryRegisterFixedTickable()
        {
            if (_registeredFixedTick || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterFixedTickable(this, PriorityLayer.Player);
            _registeredFixedTick = true;
        }

        private void TryUnregisterFixedTickable()
        {
            if (!_registeredFixedTick)
                return;

            GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Player);
            _registeredFixedTick = false;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector3 center = transform.TransformPoint(checkCenterOffset);
            Vector3 halfExtents = checkHalfExtents - Vector3.one * checkShrink;

            Gizmos.matrix = Matrix4x4.TRS(center, transform.rotation, Vector3.one);
            Gizmos.color = _canBuild
                ? new Color(0f, 1f, 0f, 0.25f)
                : new Color(1f, 0f, 0f, 0.25f);
            Gizmos.DrawCube(Vector3.zero, halfExtents * 2f);
            Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2f);
        }
#endif
    }
}
