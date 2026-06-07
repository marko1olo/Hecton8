using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.AI
{
    /// <summary>
    /// Publishes bounded leviathan wound data into shared shader globals without cloning materials.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CreatureDamageManager : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const int MaxWounds = 8;

        private static readonly int WoundCountId = Shader.PropertyToID("_HectonCreatureWoundCount");
        private static readonly int WoundsId = Shader.PropertyToID("_HectonCreatureWounds");
        private static readonly int WoundOwnerWorldToLocalId = Shader.PropertyToID("_HectonCreatureWoundOwnerWorldToLocal");
        private static readonly int WoundOwnerSphereId = Shader.PropertyToID("_HectonCreatureWoundOwnerSphere");
        private static readonly ShaderClearLateFrameProxy s_shaderClearProxy = new ShaderClearLateFrameProxy();

        [Header("Wound Projection")]
        [Tooltip("Maximum number of persistent wound stamps kept on the active leviathan shader owner.")]
        [SerializeField, Range(1, MaxWounds)] private int maxWounds = MaxWounds;
        [Tooltip("Minimum wound radius authored into the shared shader buffer.")]
        [SerializeField, Min(0.05f)] private float minWoundRadius = 0.22f;
        [Tooltip("Maximum wound radius authored into the shared shader buffer.")]
        [SerializeField, Min(0.05f)] private float maxWoundRadius = 1.1f;
        [Tooltip("Extra padding applied to the wound-owner rejection sphere to keep scars stable across skin deformation.")]
        [SerializeField, Min(0.1f)] private float ownerSpherePadding = 2.5f;

        // COLD ALLOC: Vector4[8] - shared leviathan wound upload cache bound into shader globals - owner: CreatureDamageManager
        private readonly Vector4[] _woundUpload = new Vector4[MaxWounds];
        private System.Collections.Generic.List<Renderer> _rendererScratch;

        private static CreatureDamageManager s_activeOwner;
        private static bool s_shaderClearPending;
        private static bool s_shaderClearProxyRegistered;

        private Transform _cachedTransform;
        private FaunaBrain _faunaBrain;
        private FaunaMetadata _faunaMetadata;
        private Bounds _localBounds = new Bounds(Vector3.zero, Vector3.one * 4f);
        private bool _registeredLateFrame;
        private bool _hotSwapRegistered;
        private bool _tickSleeping;
        private int _woundCount;
        private int _nextWoundIndex;
        private bool _woundsDirty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_activeOwner = null;
            s_shaderClearPending = false;
            s_shaderClearProxyRegistered = false;
        }

        private void Awake()
        {
            _cachedTransform = base.transform;
            TryGetComponent(out _faunaBrain);
            TryGetComponent(out _faunaMetadata);
            _faunaBrain?.BindCreatureDamageManagerOwner(this);
            RefreshBounds();
        }

        private void OnEnable()
        {
            if (_cachedTransform == null)
                _cachedTransform = base.transform;

            TryGetComponent(out _faunaBrain);
            TryGetComponent(out _faunaMetadata);
            _faunaBrain?.BindCreatureDamageManagerOwner(this);
            RefreshBounds();
            TryRegisterHotSwapListener();
            if (Application.isPlaying && _woundCount > 0 && IsLeviathanPresentationOwner())
            {
                s_activeOwner = this;
                _woundsDirty = true;
                _tickSleeping = false;
                TryRegisterLateFrame();
            }
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();

            if (ReferenceEquals(s_activeOwner, this))
            {
                s_activeOwner = null;
                QueueShaderClearGlobalsForLateFrame();
            }

            TryUnregisterLateFrame();
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            if (ReferenceEquals(s_activeOwner, this))
            {
                s_activeOwner = null;
                QueueShaderClearGlobalsForLateFrame();
            }

            TryUnregisterLateFrame();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            TryUnregisterShaderClearProxyFromDispatcher();
            TryUnregisterLateFrame();
            if (currentService == null || !isActiveAndEnabled)
                return;

            if (s_shaderClearPending)
                QueueShaderClearGlobalsForLateFrame();

            if (_woundCount > 0 && !_tickSleeping && ReferenceEquals(s_activeOwner, this))
                TryRegisterLateFrame();
        }

        /// <summary>
        /// Rebinds the fauna owner after procedural presentation ownership changes.
        /// </summary>
        /// <param name="faunaBrain">Owning fauna controller.</param>
        internal void BindFromFauna(FaunaBrain faunaBrain)
        {
            _faunaBrain = faunaBrain;
            if (_faunaMetadata == null)
                TryGetComponent(out _faunaMetadata);
            faunaBrain?.BindCreatureDamageManagerOwner(this);
            RefreshBounds();
        }

        /// <summary>
        /// Registers one authoritative runtime-space wound entry from the tool damage ingress.
        /// </summary>
        /// <param name="hitPointWS">Runtime-space impact point.</param>
        /// <param name="damageAmount">Damage amount used to scale wound radius.</param>
        internal void RegisterWoundWS(Vector3 hitPointWS, float damageAmount)
        {
            if (!IsLeviathanPresentationOwner())
                return;

            int safeCapacity = math.clamp(maxWounds, 1, MaxWounds);
            float normalizedDamage = _faunaBrain != null && _faunaBrain.MaxHealth > 0.001f
                ? math.saturate(math.max(0f, damageAmount) * math.rcp(_faunaBrain.MaxHealth))
                : math.saturate(math.max(0f, damageAmount) * 0.1f);

            Transform ownerTransform = ResolveCachedTransform();
            Vector3 localHitPoint = ownerTransform.InverseTransformPoint(hitPointWS);
            float woundRadius = minWoundRadius + ((maxWoundRadius - minWoundRadius) * normalizedDamage);

            if (_woundCount < safeCapacity)
                _woundCount++;

            _woundUpload[_nextWoundIndex] = new Vector4(localHitPoint.x, localHitPoint.y, localHitPoint.z, woundRadius);
            _nextWoundIndex++;
            if (_nextWoundIndex >= safeCapacity)
                _nextWoundIndex = 0;

            _woundsDirty = true;
            s_activeOwner = this;
            _tickSleeping = false;
            TryRegisterLateFrame();
        }

        public void LateFrameTick()
        {
            if (_tickSleeping)
                return;

            if (!ReferenceEquals(s_activeOwner, this) || _woundCount <= 0 || !IsLeviathanPresentationOwner())
            {
                _tickSleeping = true;
                return;
            }

            PublishShaderGlobals();
        }

        private bool IsLeviathanPresentationOwner()
        {
            return _faunaBrain != null &&
                   _faunaBrain.SpeciesProfile != null &&
                   _faunaBrain.SpeciesProfile.isLeviathan;
        }

        private void TryRegisterLateFrame()
        {
            if (_registeredLateFrame || !Application.isPlaying)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterLateFrame()
        {
            if (!_registeredLateFrame)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredLateFrame = false;
            _tickSleeping = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private void RefreshBounds()
        {
            if (_faunaMetadata != null && _faunaMetadata.TryGetLocalRenderBounds(out Bounds metadataBounds))
            {
                _localBounds = metadataBounds;
                return;
            }

            System.Collections.Generic.List<Renderer> scratch = _rendererScratch;
            if (scratch == null)
            {
                scratch = new System.Collections.Generic.List<Renderer>(8); // COLD ALLOC: List<Renderer>[8] - legacy no-metadata wound bounds discovery scratch - owner: CreatureDamageManager
                _rendererScratch = scratch;
            }

            scratch.Clear();
            GetComponentsInChildren(true, scratch);

            Transform ownerTransform = ResolveCachedTransform();
            bool hasBounds = false;
            Bounds combinedBounds = default;
            for (int i = 0; i < scratch.Count; i++)
            {
                Renderer renderer = scratch[i];
                if (renderer == null)
                    continue;

                Bounds worldBounds = renderer.bounds;
                if (!IsFinite(worldBounds.center) || !IsFinite(worldBounds.extents))
                    continue;

                AppendWorldBoundsAsOwnerLocal(ownerTransform, worldBounds, ref combinedBounds, ref hasBounds);
            }

            if (hasBounds)
                _localBounds = combinedBounds;
            scratch.Clear();
        }

        private void PublishShaderGlobals()
        {
            if (_woundCount <= 0)
            {
                ClearShaderGlobals();
                return;
            }

            Shader.SetGlobalFloat(WoundCountId, _woundCount);
            if (_woundsDirty)
            {
                Shader.SetGlobalVectorArray(WoundsId, _woundUpload);
                _woundsDirty = false;
            }

            Transform ownerTransform = ResolveCachedTransform();
            Shader.SetGlobalMatrix(WoundOwnerWorldToLocalId, ownerTransform.worldToLocalMatrix);

            Vector3 lossyScale = ownerTransform.lossyScale;
            Vector3 localExtents = _localBounds.extents;
            Vector3 scaledExtents = new Vector3(
                localExtents.x * math.abs(lossyScale.x),
                localExtents.y * math.abs(lossyScale.y),
                localExtents.z * math.abs(lossyScale.z));
            float ownerRadius = ResolveOwnerSphereRadiusCheat(scaledExtents) + math.max(0.1f, ownerSpherePadding);
            Vector3 ownerCenterWS = ownerTransform.TransformPoint(_localBounds.center);
            Shader.SetGlobalVector(WoundOwnerSphereId, new Vector4(ownerCenterWS.x, ownerCenterWS.y, ownerCenterWS.z, ownerRadius));
        }

        private Transform ResolveCachedTransform()
        {
            if (_cachedTransform == null)
                _cachedTransform = base.transform;

            return _cachedTransform;
        }

        private static float ResolveOwnerSphereRadiusCheat(Vector3 value)
        {
            float maxAxis = math.max(math.abs(value.x), math.max(math.abs(value.y), math.abs(value.z)));
            return maxAxis * 1.75f;
        }

        private static void AppendWorldBoundsAsOwnerLocal(
            Transform ownerTransform,
            Bounds worldBounds,
            ref Bounds combinedBounds,
            ref bool hasBounds)
        {
            Vector3 center = worldBounds.center;
            Vector3 extents = worldBounds.extents;
            AppendOwnerLocalBoundsPoint(ownerTransform, new Vector3(center.x - extents.x, center.y - extents.y, center.z - extents.z), ref combinedBounds, ref hasBounds);
            AppendOwnerLocalBoundsPoint(ownerTransform, new Vector3(center.x - extents.x, center.y - extents.y, center.z + extents.z), ref combinedBounds, ref hasBounds);
            AppendOwnerLocalBoundsPoint(ownerTransform, new Vector3(center.x - extents.x, center.y + extents.y, center.z - extents.z), ref combinedBounds, ref hasBounds);
            AppendOwnerLocalBoundsPoint(ownerTransform, new Vector3(center.x - extents.x, center.y + extents.y, center.z + extents.z), ref combinedBounds, ref hasBounds);
            AppendOwnerLocalBoundsPoint(ownerTransform, new Vector3(center.x + extents.x, center.y - extents.y, center.z - extents.z), ref combinedBounds, ref hasBounds);
            AppendOwnerLocalBoundsPoint(ownerTransform, new Vector3(center.x + extents.x, center.y - extents.y, center.z + extents.z), ref combinedBounds, ref hasBounds);
            AppendOwnerLocalBoundsPoint(ownerTransform, new Vector3(center.x + extents.x, center.y + extents.y, center.z - extents.z), ref combinedBounds, ref hasBounds);
            AppendOwnerLocalBoundsPoint(ownerTransform, new Vector3(center.x + extents.x, center.y + extents.y, center.z + extents.z), ref combinedBounds, ref hasBounds);
        }

        private static void AppendOwnerLocalBoundsPoint(
            Transform ownerTransform,
            Vector3 worldPoint,
            ref Bounds combinedBounds,
            ref bool hasBounds)
        {
            Vector3 localPoint = ownerTransform.InverseTransformPoint(worldPoint);
            if (!IsFinite(localPoint))
                return;

            if (!hasBounds)
            {
                combinedBounds = new Bounds(localPoint, Vector3.zero);
                hasBounds = true;
                return;
            }

            combinedBounds.Encapsulate(localPoint);
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z);
        }

        private static void ClearShaderGlobals()
        {
            Shader.SetGlobalFloat(WoundCountId, 0f);
            Shader.SetGlobalVector(WoundOwnerSphereId, Vector4.zero);
        }

        private static void QueueShaderClearGlobalsForLateFrame()
        {
            if (!Application.isPlaying)
            {
                s_shaderClearPending = false;
                return;
            }

            s_shaderClearPending = true;
            bool accepted = GlobalRegistry.TryRegisterLateFrameTickable(s_shaderClearProxy, PriorityLayer.Environment);
            s_shaderClearProxyRegistered = s_shaderClearProxyRegistered || accepted;
        }

        private static void TryUnregisterShaderClearProxyFromDispatcher()
        {
            if (!s_shaderClearProxyRegistered)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(s_shaderClearProxy, PriorityLayer.Environment);
            s_shaderClearProxyRegistered = false;
        }

        private sealed class ShaderClearLateFrameProxy : ILateFrameTickable
        {
            public void LateFrameTick()
            {
                if (!s_shaderClearPending)
                {
                    TryUnregisterShaderClearProxy();
                    return;
                }

                ClearShaderGlobals();
                s_shaderClearPending = false;
                TryUnregisterShaderClearProxy();
            }

            private static void TryUnregisterShaderClearProxy()
            {
                TryUnregisterShaderClearProxyFromDispatcher();
            }
        }
    }
}
