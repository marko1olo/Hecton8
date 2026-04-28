using Hecton8.Core;
using System.Collections.Generic;
using Hecton8.Gameplay;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Physics
{
    /// <summary>
    /// Player-owned tether runtime host.
    /// Physics executes in <see cref="FixedTick(float)"/> and visuals render in <see cref="LateUpdate"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Physics/Tether Manager")]
    public sealed class TetherManager : MonoBehaviour, IFixedTickable, IOriginShiftListener
    {
        [BurstCompile(FloatMode = FloatMode.Fast)]
        private struct TranslateVisualPointsJob : IJobParallelFor
        {
            public float3 ShiftOffset;
            public NativeArray<float3> Points;

            public void Execute(int index)
            {
                Points[index] -= ShiftOffset;
            }
        }

        private const string RuntimeShaderName = "Hecton8/Physics/TetherLineStrip";
        private static readonly int _TetherPositionsId = Shader.PropertyToID("_TetherPositions");
        private static readonly int _TetherColorId = Shader.PropertyToID("_TetherColor");
        private static readonly int _TetherPointCountId = Shader.PropertyToID("_TetherPointCount");

        [Header("Tether Rendering")]
        [Tooltip("Optional explicit material for tether line rendering. When omitted the manager creates a runtime material from the built-in tether shader.")]
        [SerializeField] private Material tetherRenderMaterial;

        [Tooltip("Fallback tether line tint used by the procedural line-strip renderer.")]
        [SerializeField] private Color tetherRenderColor = new Color(0.22f, 0.92f, 0.96f, 0.92f);

        [Tooltip("Padding applied around per-tether bounds before the procedural draw is submitted.")]
        [SerializeField, Range(0f, 4f)] private float tetherBoundsPadding = 1.2f;

        [Tooltip("Optional explicit camera used for tether rendering. Null renders to all cameras.")]
        [SerializeField] private Camera renderCamera;

        [Tooltip("Maximum tether count allowed to use virtual bend detection and catenary rendering simultaneously.")]
        [SerializeField, Range(1, 8)] private int maxVisualizedTethers = 4;

        [Header("Tether Profiles")]
        [Tooltip("Optional authored tow-cable profile. When omitted the runtime falls back to HeavyTowWinch tuning.")]
        [SerializeField] private TetherProfileSO towCableProfile;

        [Header("Diagnostics")]
#pragma warning disable CS0414
        [SerializeField] private int _debugActiveTetherCount;
#pragma warning restore CS0414

        // COLD ALLOC: List<TetherInstance>[4] — active tether registry owned by the player-local tether manager — owner: TetherManager
        private readonly List<TetherInstance> _activeInstances = new List<TetherInstance>(4);
        // COLD ALLOC: List<TetherInstance>[4] — pooled tether instances reused across attach/release cycles — owner: TetherManager
        private readonly List<TetherInstance> _pooledInstances = new List<TetherInstance>(4);
        private MaterialPropertyBlock _renderPropertyBlock;
        private Material _runtimeRenderMaterial;
        private bool _ownsRuntimeMaterial;
        private bool _registeredFixedTick;
        private bool _registeredOriginShiftListener;

        private void Awake()
        {
            if (renderCamera == null)
            {
                Camera childCamera = Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<Camera>(transform);
                if (childCamera != null)
                    renderCamera = childCamera;
            }

            _renderPropertyBlock ??= new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] — procedural tether render binding payload — owner: TetherManager
        }

        private void OnEnable()
        {
            if (!_registeredFixedTick)
            {
                GlobalRegistry.RegisterFixedTickable(this, PriorityLayer.Player);
                _registeredFixedTick = true;
            }

            if (!_registeredOriginShiftListener)
            {
                HectonFloatingOrigin.RegisterListener(this);
                _registeredOriginShiftListener = true;
            }
        }

        private void OnDisable()
        {
            if (_registeredFixedTick)
            {
                GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Player);
                _registeredFixedTick = false;
            }

            if (_registeredOriginShiftListener)
            {
                HectonFloatingOrigin.UnregisterListener(this);
                _registeredOriginShiftListener = false;
            }

            for (int i = _activeInstances.Count - 1; i >= 0; i--)
                DetachTether(_activeInstances[i], false, true);
        }

        private void OnDestroy()
        {
            if (_registeredOriginShiftListener)
            {
                HectonFloatingOrigin.UnregisterListener(this);
                _registeredOriginShiftListener = false;
            }

            for (int i = 0; i < _pooledInstances.Count; i++)
            {
                if (_pooledInstances[i] != null)
                    _pooledInstances[i].DisposeRuntimeResources();
            }

            for (int i = 0; i < _activeInstances.Count; i++)
            {
                if (_activeInstances[i] != null)
                    _activeInstances[i].DisposeRuntimeResources();
            }

            if (_ownsRuntimeMaterial && _runtimeRenderMaterial != null)
            {
                Destroy(_runtimeRenderMaterial);
                _runtimeRenderMaterial = null;
                _ownsRuntimeMaterial = false;
            }
        }

        /// <summary>
        /// Creates or reuses a tow-cable runtime instance.
        /// </summary>
        public TetherInstance AttachTowCable(
            HeavyTowWinch owner,
            HectonPlayerMotor playerMotor,
            Rigidbody playerBody,
            Rigidbody payloadBody,
            Collider payloadCollider,
            float initialDistance)
        {
            if (owner == null || playerBody == null || payloadBody == null || payloadCollider == null)
                return null;

            TetherInstance instance = RentInstance();
            if (instance == null)
                return null;

            instance.Configure(owner, playerMotor, playerBody, payloadBody, payloadCollider, initialDistance);
            if (!_activeInstances.Contains(instance))
                _activeInstances.Add(instance);

            _debugActiveTetherCount = _activeInstances.Count;
            return instance;
        }

        /// <summary>
        /// Releases an active tether and returns it to the local pool.
        /// </summary>
        public void DetachTether(TetherInstance instance, bool snapped, bool notifyOwner)
        {
            if (instance == null)
                return;

            int index = _activeInstances.IndexOf(instance);
            if (index >= 0)
            {
                int lastIndex = _activeInstances.Count - 1;
                _activeInstances[index] = _activeInstances[lastIndex];
                _activeInstances.RemoveAt(lastIndex);
            }

            HeavyTowWinch owner = notifyOwner ? instance.Owner : null;
            instance.Deactivate();
            if (!_pooledInstances.Contains(instance))
                _pooledInstances.Add(instance);

            if (notifyOwner && owner != null)
                owner.OnTetherDetached(instance, snapped);

            _debugActiveTetherCount = _activeInstances.Count;
        }

        /// <inheritdoc />
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftOffset = shiftData.ShiftOffset;
            if (shiftOffset.sqrMagnitude <= 0.000001f)
                return;

            float3 shiftOffsetF3 = new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z);
            for (int i = 0; i < _activeInstances.Count; i++)
            {
                TetherInstance instance = _activeInstances[i];
                if (instance == null || !instance.IsActive)
                    continue;

                instance.RebaseManagedRuntimeState(shiftOffset);
                NativeArray<float3> visualPoints = instance.VisualSegmentPositions;
                if (!visualPoints.IsCreated || visualPoints.Length == 0)
                    continue;

                TranslateVisualPointsJob translateJob = new TranslateVisualPointsJob
                {
                    ShiftOffset = shiftOffsetF3,
                    Points = visualPoints
                };

                JobHandle handle = translateJob.Schedule(visualPoints.Length, 32);
                handle.Complete();
                instance.CommitVisualRebaseUpload();
            }
        }

        /// <inheritdoc />
        public void FixedTick(float fixedDeltaTime)
        {
            int activeCount = _activeInstances.Count;
            for (int i = activeCount - 1; i >= 0; i--)
            {
                TetherInstance instance = _activeInstances[i];
                if (instance == null)
                {
                    _activeInstances.RemoveAt(i);
                    continue;
                }

                TetherLifecycleState state = instance.Simulate(fixedDeltaTime, activeCount, maxVisualizedTethers);
                if (state == TetherLifecycleState.Alive)
                    continue;

                bool snapped = state == TetherLifecycleState.Snapped;
                DetachTether(instance, snapped, true);
                activeCount = _activeInstances.Count;
            }

            _debugActiveTetherCount = _activeInstances.Count;
        }

        private void LateUpdate()
        {
            Material renderMaterial = ResolveRenderMaterial();
            if (renderMaterial == null || _activeInstances.Count == 0)
                return;

            _renderPropertyBlock.Clear();
            RenderParams renderParams = new RenderParams(renderMaterial)
            {
                matProps = _renderPropertyBlock,
                camera = renderCamera,
                layer = gameObject.layer,
                receiveShadows = false,
                shadowCastingMode = ShadowCastingMode.Off,
                renderingLayerMask = 1u
            };

            float deltaTime = Time.deltaTime;
            for (int i = 0; i < _activeInstances.Count; i++)
            {
                TetherInstance instance = _activeInstances[i];
                if (instance == null || !instance.IsActive)
                    continue;

                instance.UpdateVisuals(deltaTime);
                if (!instance.IsVisualReady)
                    continue;

                _renderPropertyBlock.Clear();
                _renderPropertyBlock.SetBuffer(_TetherPositionsId, instance.VisualSegmentBuffer);
                _renderPropertyBlock.SetColor(_TetherColorId, tetherRenderColor);
                _renderPropertyBlock.SetInt(_TetherPointCountId, instance.VisualPointCount);
                renderParams.worldBounds = instance.GetVisualBounds(tetherBoundsPadding);
                Graphics.RenderPrimitives(renderParams, MeshTopology.LineStrip, instance.VisualPointCount, 1);
            }
        }

        private TetherInstance RentInstance()
        {
            int pooledCount = _pooledInstances.Count;
            if (pooledCount > 0)
            {
                int lastIndex = pooledCount - 1;
                TetherInstance pooled = _pooledInstances[lastIndex];
                _pooledInstances.RemoveAt(lastIndex);
                if (pooled != null)
                {
                    pooled.InitializeManager(this);
                    pooled.gameObject.SetActive(true);
                    return pooled;
                }
            }

            GameObject tetherObject = new GameObject($"TetherInstance_{_activeInstances.Count + _pooledInstances.Count:D2}");
            tetherObject.transform.SetParent(transform, false);
            tetherObject.transform.localPosition = Vector3.zero;
            tetherObject.transform.localRotation = Quaternion.identity;
            tetherObject.transform.localScale = Vector3.one;
            // COLD ALLOC: TetherInstance[1] — pooled tether runtime child created on first demand — owner: TetherManager
            TetherInstance instance = tetherObject.AddComponent<TetherInstance>();
            instance.InitializeManager(this);
            return instance;
        }

        private Material ResolveRenderMaterial()
        {
            if (tetherRenderMaterial != null)
            {
                _ownsRuntimeMaterial = false;
                return tetherRenderMaterial;
            }

            if (_runtimeRenderMaterial != null)
                return _runtimeRenderMaterial;

            Shader shader = Shader.Find(RuntimeShaderName);
            if (shader == null)
                return null;

            // COLD ALLOC: Material[1] — runtime tether line-strip material fallback built from first-party shader — owner: TetherManager
            _runtimeRenderMaterial = new Material(shader)
            {
                name = "MAT_Runtime_TetherLineStrip",
                hideFlags = HideFlags.DontSave
            };
            _ownsRuntimeMaterial = true;
            return _runtimeRenderMaterial;
        }

        internal float ResolveTowSpringStiffness(HeavyTowWinch owner)
        {
            if (towCableProfile != null)
                return math.max(0f, towCableProfile.SpringStiffness);

            return owner != null ? owner.ResolveTowSpringStiffness() : 0f;
        }

        internal float ResolveTowOverDampingMultiplier(HeavyTowWinch owner)
        {
            if (towCableProfile != null)
                return math.max(1f, towCableProfile.OverDampingMultiplier);

            return owner != null ? owner.ResolveTowOverDampingMultiplier() : 1f;
        }

        internal float ResolveTowSnapTensionThreshold(HeavyTowWinch owner)
        {
            if (towCableProfile != null)
                return math.max(1f, towCableProfile.SnapTensionThreshold);

            return owner != null ? owner.ResolveSnapTensionThreshold() : 1f;
        }
    }
}
