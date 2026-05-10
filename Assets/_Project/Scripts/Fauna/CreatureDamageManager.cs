using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.AI
{
    /// <summary>
    /// Publishes bounded leviathan wound data into shared shader globals without cloning materials.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CreatureDamageManager : MonoBehaviour, ITickable
    {
        private const int MaxWounds = 8;

        private static readonly int WoundCountId = Shader.PropertyToID("_HectonCreatureWoundCount");
        private static readonly int WoundsId = Shader.PropertyToID("_HectonCreatureWounds");
        private static readonly int WoundOwnerWorldToLocalId = Shader.PropertyToID("_HectonCreatureWoundOwnerWorldToLocal");
        private static readonly int WoundOwnerSphereId = Shader.PropertyToID("_HectonCreatureWoundOwnerSphere");

        [Header("── Wound Projection ─────────────────")]
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
        // COLD ALLOC: List<SkinnedMeshRenderer>[8] - wound-owner skeletal renderer bounds discovery scratch - owner: CreatureDamageManager
        private readonly System.Collections.Generic.List<SkinnedMeshRenderer> _rendererScratch = new System.Collections.Generic.List<SkinnedMeshRenderer>(8);

        private static CreatureDamageManager s_activeOwner;

        private Transform _cachedTransform;
        private FaunaBrain _faunaBrain;
        private Bounds _localBounds = new Bounds(Vector3.zero, Vector3.one * 4f);
        private bool _registeredTick;
        private int _woundCount;
        private int _nextWoundIndex;
        private bool _woundsDirty;

        private void Awake()
        {
            _cachedTransform = base.transform;
            TryGetComponent(out _faunaBrain);
            RefreshBounds();
        }

        private void OnEnable()
        {
            if (_cachedTransform == null)
                _cachedTransform = base.transform;

            TryGetComponent(out _faunaBrain);
            RefreshBounds();
            if (Application.isPlaying && _woundCount > 0 && IsLeviathanPresentationOwner())
            {
                s_activeOwner = this;
                _woundsDirty = true;
                PublishShaderGlobals();
                TryRegisterTick();
            }
        }

        private void OnDisable()
        {
            if (_registeredTick)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredTick = false;
            }

            if (ReferenceEquals(s_activeOwner, this))
            {
                s_activeOwner = null;
                ClearShaderGlobals();
            }
        }

        /// <summary>
        /// Rebinds the fauna owner after procedural presentation ownership changes.
        /// </summary>
        /// <param name="faunaBrain">Owning fauna controller.</param>
        internal void BindFromFauna(FaunaBrain faunaBrain)
        {
            _faunaBrain = faunaBrain;
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
                ? math.saturate(math.max(0f, damageAmount) / _faunaBrain.MaxHealth)
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
            PublishShaderGlobals();
            TryRegisterTick();
        }

        public void Tick(float deltaTime)
        {
            if (!ReferenceEquals(s_activeOwner, this) || _woundCount <= 0 || !IsLeviathanPresentationOwner())
            {
                TryUnregisterTick();
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

        private void TryRegisterTick()
        {
            if (_registeredTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterTick()
        {
            if (!_registeredTick)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _registeredTick = false;
        }

        private void RefreshBounds()
        {
            _rendererScratch.Clear();
            GetComponentsInChildren(true, _rendererScratch);

            bool hasBounds = false;
            Bounds combinedBounds = default;
            for (int i = 0; i < _rendererScratch.Count; i++)
            {
                SkinnedMeshRenderer renderer = _rendererScratch[i];
                if (renderer == null)
                    continue;

                Bounds localBounds = renderer.localBounds;
                if (!hasBounds)
                {
                    combinedBounds = localBounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(localBounds.min);
                    combinedBounds.Encapsulate(localBounds.max);
                }
            }

            if (hasBounds)
                _localBounds = combinedBounds;
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

        private static void ClearShaderGlobals()
        {
            Shader.SetGlobalFloat(WoundCountId, 0f);
            Shader.SetGlobalVector(WoundOwnerSphereId, Vector4.zero);
        }
    }
}
