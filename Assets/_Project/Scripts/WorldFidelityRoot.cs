using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    public sealed class WorldFidelityRoot : MonoBehaviour
    {
        [Header("Thresholds")]
        [SerializeField] private WorldSliceAnchor.SliceState visibleFromState = WorldSliceAnchor.SliceState.Far;
        [SerializeField] private WorldSliceAnchor.SliceState collidersFromState = WorldSliceAnchor.SliceState.Near;
        [SerializeField] private WorldSliceAnchor.SliceState behavioursFromState = WorldSliceAnchor.SliceState.Near;
        [SerializeField] private WorldSliceAnchor.SliceState physicsFromState = WorldSliceAnchor.SliceState.Near;
        [SerializeField] private WorldSliceAnchor.SliceState fullShadowsFromState = WorldSliceAnchor.SliceState.Mid;

        [Header("Collection")]
        [SerializeField] private bool autoCollectChildren = true;
        [SerializeField] private Renderer[] renderers;
        [SerializeField] private Collider[] colliders;
        [SerializeField] private Behaviour[] behaviours;
        [SerializeField] private Rigidbody[] rigidbodies;

        [Header("Diagnostics")]
        [SerializeField] private string _debugState = "Far";
        [SerializeField] private int _debugRendererCount;
        [SerializeField] private int _debugColliderCount;
        [SerializeField] private int _debugBehaviourCount;
        [SerializeField] private int _debugRigidbodyCount;

        private static readonly List<Renderer> _RendererScratch = new List<Renderer>(32);
        private static readonly List<Collider> _ColliderScratch = new List<Collider>(32);
        private static readonly List<Rigidbody> _RigidbodyScratch = new List<Rigidbody>(16);
        private static readonly List<Behaviour> _BehaviourScratch = new List<Behaviour>(32);
        private static readonly List<Behaviour> _FilteredBehaviourScratch = new List<Behaviour>(32);

        private ShadowCastingMode[] _originalShadowModes;
        private bool[] _originalReceiveShadows;
        private bool[] _originalRigidbodyKinematic;
        private bool[] _originalRigidbodyDetectCollisions;

        private void Awake()
        {
            RefreshTrackedComponents();
            CacheRuntimeState();
        }

        private void OnEnable()
        {
            if (_originalShadowModes == null || _originalShadowModes.Length != Count(renderers))
                CacheRuntimeState();
        }

        public void RefreshTrackedComponents()
        {
            if (!autoCollectChildren)
            {
                UpdateDiagnostics();
                return;
            }

            _RendererScratch.Clear();
            GetComponentsInChildren<Renderer>(true, _RendererScratch);
            renderers = CopyScratchToArray(_RendererScratch, renderers);
            _RendererScratch.Clear();

            _ColliderScratch.Clear();
            GetComponentsInChildren<Collider>(true, _ColliderScratch);
            colliders = CopyScratchToArray(_ColliderScratch, colliders);
            _ColliderScratch.Clear();

            _RigidbodyScratch.Clear();
            GetComponentsInChildren<Rigidbody>(true, _RigidbodyScratch);
            rigidbodies = CopyScratchToArray(_RigidbodyScratch, rigidbodies);
            _RigidbodyScratch.Clear();

            behaviours = CollectBehaviours();
            UpdateDiagnostics();
        }

        public void ApplySliceState(WorldSliceAnchor.SliceState state)
        {
            _debugState = WorldSliceAnchor.ResolveStateName(state);

            bool renderVisible = Meets(state, visibleFromState);
            bool collidersEnabled = Meets(state, collidersFromState);
            bool behavioursEnabled = Meets(state, behavioursFromState);
            bool physicsEnabled = Meets(state, physicsFromState);
            bool fullShadows = Meets(state, fullShadowsFromState);

            ApplyRenderers(renderVisible, fullShadows);
            ApplyColliders(collidersEnabled);
            ApplyBehaviours(behavioursEnabled);
            ApplyRigidbodies(physicsEnabled);
        }

        private void CacheRuntimeState()
        {
            int rendererCount = Count(renderers);
            EnsureArrayLength(ref _originalShadowModes, rendererCount);
            EnsureArrayLength(ref _originalReceiveShadows, rendererCount);
            Array.Clear(_originalShadowModes, 0, _originalShadowModes.Length);
            Array.Clear(_originalReceiveShadows, 0, _originalReceiveShadows.Length);
            for (int i = 0; i < _originalShadowModes.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                _originalShadowModes[i] = renderer.shadowCastingMode;
                _originalReceiveShadows[i] = renderer.receiveShadows;
            }

            int rigidbodyCount = Count(rigidbodies);
            EnsureArrayLength(ref _originalRigidbodyKinematic, rigidbodyCount);
            EnsureArrayLength(ref _originalRigidbodyDetectCollisions, rigidbodyCount);
            Array.Clear(_originalRigidbodyKinematic, 0, _originalRigidbodyKinematic.Length);
            Array.Clear(_originalRigidbodyDetectCollisions, 0, _originalRigidbodyDetectCollisions.Length);
            for (int i = 0; i < _originalRigidbodyKinematic.Length; i++)
            {
                Rigidbody body = rigidbodies[i];
                if (body == null)
                    continue;

                _originalRigidbodyKinematic[i] = body.isKinematic;
                _originalRigidbodyDetectCollisions[i] = body.detectCollisions;
            }
        }

        private void ApplyRenderers(bool visible, bool fullShadows)
        {
            if (renderers == null)
                return;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                renderer.enabled = visible;
                if (!visible)
                    continue;

                if (fullShadows)
                {
                    renderer.shadowCastingMode = i < _originalShadowModes.Length
                        ? _originalShadowModes[i]
                        : ShadowCastingMode.On;
                    renderer.receiveShadows = i < _originalReceiveShadows.Length
                        ? _originalReceiveShadows[i]
                        : true;
                }
                else
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
            }
        }

        private void ApplyColliders(bool enabled)
        {
            if (colliders == null)
                return;

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null)
                    continue;

                collider.enabled = enabled;
            }
        }

        private void ApplyBehaviours(bool enabled)
        {
            if (behaviours == null)
                return;

            for (int i = 0; i < behaviours.Length; i++)
            {
                Behaviour behaviour = behaviours[i];
                if (behaviour == null)
                    continue;

                behaviour.enabled = enabled;
            }
        }

        private void ApplyRigidbodies(bool physicsEnabled)
        {
            if (rigidbodies == null)
                return;

            for (int i = 0; i < rigidbodies.Length; i++)
            {
                Rigidbody body = rigidbodies[i];
                if (body == null)
                    continue;

                if (physicsEnabled)
                {
                    body.isKinematic = i < _originalRigidbodyKinematic.Length && _originalRigidbodyKinematic[i];
                    body.detectCollisions = i < _originalRigidbodyDetectCollisions.Length
                        ? _originalRigidbodyDetectCollisions[i]
                        : true;
                }
                else
                {
                    body.isKinematic = true;
                    body.detectCollisions = false;
                }
            }
        }

        private Behaviour[] CollectBehaviours()
        {
            _BehaviourScratch.Clear();
            _FilteredBehaviourScratch.Clear();
            GetComponentsInChildren<Behaviour>(true, _BehaviourScratch);
            for (int i = 0; i < _BehaviourScratch.Count; i++)
            {
                Behaviour behaviour = _BehaviourScratch[i];
                if (behaviour == null || behaviour == this || behaviour is WorldSliceAnchor || behaviour is WorldFidelityRoot)
                    continue;

                _FilteredBehaviourScratch.Add(behaviour);
            }

            Behaviour[] result = CopyScratchToArray(_FilteredBehaviourScratch, behaviours);
            _BehaviourScratch.Clear();
            _FilteredBehaviourScratch.Clear();
            return result;
        }

        private void UpdateDiagnostics()
        {
            _debugRendererCount = renderers != null ? renderers.Length : 0;
            _debugColliderCount = colliders != null ? colliders.Length : 0;
            _debugBehaviourCount = behaviours != null ? behaviours.Length : 0;
            _debugRigidbodyCount = rigidbodies != null ? rigidbodies.Length : 0;
        }

        private static bool Meets(WorldSliceAnchor.SliceState state, WorldSliceAnchor.SliceState threshold)
        {
            return (int)state >= (int)threshold;
        }

        private static int Count<T>(T[] values)
        {
            return values != null ? values.Length : 0;
        }

        private static void EnsureArrayLength<T>(ref T[] values, int length)
        {
            if (length <= 0)
            {
                values = Array.Empty<T>();
                return;
            }

            if (values == null || values.Length != length)
                values = new T[length];
        }

        private static T[] CopyScratchToArray<T>(List<T> source, T[] target)
        {
            int count = source != null ? source.Count : 0;
            EnsureArrayLength(ref target, count);
            for (int i = 0; i < count; i++)
                target[i] = source[i];

            return target;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            RefreshTrackedComponents();
            CacheRuntimeState();
        }
#endif
    }
}
