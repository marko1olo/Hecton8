using Hecton8.Core;
using TMPro;
using UnityEngine;

namespace Hecton8.UI
{
    /// <summary>
    /// Keeps world-space TMP labels bound to static shared font assets.
    /// Runtime SDF sharpness must come from offline-baked atlases, not per-label material clones.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldSpaceTMPSharpnessController : MonoBehaviour, ISlowTickable, IGlobalRegistryHotSwapListener
    {
        private TMP_Text _target;
        private Camera _camera;
        private bool _registeredSlowTick;
        private bool _hotSwapListenerRegistered;

        /// <summary>
        /// Binds the sharpness owner to a world-space TMP label and optional camera.
        /// The camera parameter is retained for scene/prefab compatibility; the runtime no longer mutates SDF material state per distance.
        /// </summary>
        public void Bind(TMP_Text target, Camera camera)
        {
            if (ReferenceEquals(_target, target) && ReferenceEquals(_camera, camera))
                return;

            _target = target;
            _camera = camera;

            if (_target == null)
            {
                UnregisterFromTickManager();
                TryUnregisterHotSwapListener();
                return;
            }

            TryRegisterHotSwapListener();
            RegisterToTickManager();
            BindStaticSharedMaterial();
        }

        private void OnEnable()
        {
            if (_target == null)
                return;

            TryRegisterHotSwapListener();
            RegisterToTickManager();
            BindStaticSharedMaterial();
        }

        private void OnDisable()
        {
            UnregisterFromTickManager();
            TryUnregisterHotSwapListener();
        }

        private void OnDestroy()
        {
            UnregisterFromTickManager();
            TryUnregisterHotSwapListener();
        }

        /// <inheritdoc />
        public void SlowTick()
        {
            BindStaticSharedMaterial();
        }

        private void BindStaticSharedMaterial()
        {
            if (_target == null)
                return;

            TMP_FontAsset fontAsset = _target.font;
            Material staticMaterial = fontAsset != null ? fontAsset.material : _target.fontSharedMaterial;
            if (staticMaterial == null || ReferenceEquals(_target.fontSharedMaterial, staticMaterial))
                return;

            _target.fontSharedMaterial = staticMaterial;
            _target.SetMaterialDirty();
        }

        private void RegisterToTickManager()
        {
            if (_registeredSlowTick || _target == null || !Application.isPlaying)
                return;

            _registeredSlowTick = SystemDispatcher.Register((ISlowTickable)this, PriorityLayer.UI);
        }

        private void UnregisterFromTickManager()
        {
            if (!_registeredSlowTick)
                return;

            SystemDispatcher.Unregister((ISlowTickable)this, PriorityLayer.UI);
            _registeredSlowTick = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            UnregisterFromTickManager();
            if (currentService != null && isActiveAndEnabled)
                RegisterToTickManager();
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }
    }
}
