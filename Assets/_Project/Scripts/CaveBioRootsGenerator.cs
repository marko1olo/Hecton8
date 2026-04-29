using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Caves
{
    /// <summary>
    /// Runtime owner for hanging bioluminescent cave roots attached to voxel cave ceilings.
    /// Anchors are resolved with NonAlloc raycasts and refined later if the cave mesh was not ready yet.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CaveBioRootsGenerator : MonoBehaviour, ITickable, IUpdatable
    {
        private const string RootNamePrefix = "_BioRoot_";

        [Header("── Runtime Wiring ──────────────────")]
        [SerializeField]
        [Tooltip("Voxel volume that owns the cave mesh and local-space bounds.")]
        private HectonVoxelVolume volume;

        [SerializeField]
        [Tooltip("Optional player override used when bootstrap has not published the runtime player yet.")]
        private Transform playerTransformOverride;

        [Header("── Ceiling Sampling ──────────────────")]
        [SerializeField]
        [Tooltip("Layer mask sampled when probing cave ceilings with Physics.RaycastNonAlloc.")]
        private LayerMask ceilingMask = ~0;

        [SerializeField, Range(0.05f, 4f)]
        [Tooltip("Distance above the predicted ceiling plane where anchor-cast rays begin.")]
        private float anchorCastPadding = 1.25f;

        [SerializeField, Range(0.1f, 4f)]
        [Tooltip("How often roots retry anchor refinement while waiting for a generated cave mesh/collider.")]
        private float anchorRefineInterval = 0.75f;

        private Transform _volumeTransform;
        private Transform _playerTransform;
        private Rigidbody _playerRigidbody;
        private CavePreset _preset;
        private int _rootCount;
        private int _segmentsPerRoot;
        private float _minLength;
        private float _maxLength;
        private float _swayAmplitude;
        private float _swayFrequency;
        private float _propWashRadius;
        private float _propWashStrength;
        private float _topWidth;
        private float _tipWidth;
        private Color _glowColor;
        private float _anchorRefineTimer;
        private bool _registeredTick;
        private LineRenderer[] _rootRenderers;
        private Transform[] _rootTransforms;
        private Vector3[][] _rootPositions;
        private Vector3[] _rootAnchorsLocal;
        private float[] _rootLengths;
        private float[] _rootPhases;
        private bool[] _rootNeedsRefine;
        private readonly RaycastHit[] _ceilingHits = new RaycastHit[4]; // COLD ALLOC: RaycastHit[4] — bounded ceiling-anchor probe buffer — owner: CaveBioRootsGenerator

        /// <summary>
        /// Configures the generator from the cave dressing owner.
        /// </summary>
        internal void Configure(HectonVoxelVolume targetVolume, CavePreset preset, CaveBioRootConfig config, float globalIntensity)
        {
            volume = targetVolume;
            _volumeTransform = targetVolume != null ? targetVolume.transform : null;
            _preset = preset;

            if (config == null || _volumeTransform == null)
            {
                DisableAllRoots();
                TryUnregister();
                return;
            }

            _segmentsPerRoot = Mathf.Clamp(config.segmentsPerRoot, 3, 16);
            _minLength = Mathf.Max(0.5f, config.minLength);
            _maxLength = Mathf.Max(_minLength, config.maxLength);
            _swayAmplitude = Mathf.Max(0f, config.swayAmplitude) * Mathf.Max(0.1f, globalIntensity);
            _swayFrequency = Mathf.Max(0.05f, config.swayFrequency);
            _propWashRadius = Mathf.Max(0.5f, config.propWashRadius);
            _propWashStrength = Mathf.Max(0f, config.propWashStrength) * Mathf.Max(0.1f, globalIntensity);
            _topWidth = Mathf.Max(0.01f, config.topWidth);
            _tipWidth = Mathf.Clamp(config.tipWidth, 0.005f, _topWidth);
            _glowColor = config.glowColor;
            _rootCount = Mathf.Clamp(Mathf.RoundToInt(config.maxCount * Mathf.Clamp01(globalIntensity)), 0, config.maxCount);

            EnsureBuffers();
            EnsureRootRenderers();

            for (int i = 0; i < _rootCount; i++)
            {
                ConfigureRenderer(i);
                TryResolveAnchor(i, allowFallback: true);
                if (_rootTransforms[i] != null && !_rootTransforms[i].gameObject.activeSelf)
                    _rootTransforms[i].gameObject.SetActive(true);
            }

            DisableUnusedRoots();
            _anchorRefineTimer = 0f;

            if (_rootCount > 0)
                TryRegister();
            else
                TryUnregister();
        }

        /// <summary>
        /// Updates root sway in sync with the runtime tick loop.
        /// </summary>
        public void Tick(float dt)
        {
            if (_rootCount <= 0 || _volumeTransform == null)
                return;

            ResolvePlayerContext();
            TickAnchorRefinement(dt);

            Vector3 playerPosition = _playerTransform != null ? _playerTransform.position : Vector3.zero;
            Vector3 playerVelocity = _playerRigidbody != null ? _playerRigidbody.linearVelocity : Vector3.zero;
            float playerSpeed = playerVelocity.magnitude;
            float time = Time.time;

            for (int i = 0; i < _rootCount; i++)
            {
                LineRenderer renderer = _rootRenderers[i];
                Vector3[] positions = _rootPositions[i];
                if (renderer == null || positions == null)
                    continue;

                Vector3 anchorLocal = _rootAnchorsLocal[i];
                Vector3 anchorWS = _volumeTransform.TransformPoint(anchorLocal);
                Vector3 wakeOffsetLS = ResolvePropWashOffset(anchorWS, playerPosition, playerVelocity, playerSpeed, _rootLengths[i]);
                float oscillation = Mathf.Sin((time * _swayFrequency) + _rootPhases[i]);
                Vector3 harmonicOffsetLS = new Vector3(oscillation * _swayAmplitude, 0f, Mathf.Cos((time * _swayFrequency * 0.73f) + _rootPhases[i]) * (_swayAmplitude * 0.35f));

                int segmentCount = positions.Length;
                float length = _rootLengths[i];
                for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
                {
                    float t = segmentCount > 1 ? segmentIndex / (float)(segmentCount - 1) : 1f;
                    float bend = t * t;
                    Vector3 segmentOffset = (harmonicOffsetLS + wakeOffsetLS) * bend;
                    segmentOffset.y = 0f;
                    positions[segmentIndex] = anchorLocal + segmentOffset + (Vector3.down * (length * t));
                }

                renderer.SetPositions(positions);
            }
        }

        private void Awake()
        {
            if (volume != null)
                _volumeTransform = volume.transform;
        }

        private void OnEnable()
        {
            if (_rootCount > 0)
                TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
        }

        private void OnDestroy()
        {
            TryUnregister();
        }

        private void EnsureBuffers()
        {
            if (_rootRenderers == null || _rootRenderers.Length != _rootCount)
            {
                _rootRenderers = new LineRenderer[_rootCount]; // COLD ALLOC: LineRenderer[_rootCount] — cached per-root renderers — owner: CaveBioRootsGenerator
                _rootTransforms = new Transform[_rootCount]; // COLD ALLOC: Transform[_rootCount] — cached per-root transforms — owner: CaveBioRootsGenerator
                _rootPositions = new Vector3[_rootCount][]; // COLD ALLOC: Vector3[_rootCount][] — cached line position buffers — owner: CaveBioRootsGenerator
                _rootAnchorsLocal = new Vector3[_rootCount]; // COLD ALLOC: Vector3[_rootCount] — cached local-space root anchors — owner: CaveBioRootsGenerator
                _rootLengths = new float[_rootCount]; // COLD ALLOC: float[_rootCount] — cached root lengths — owner: CaveBioRootsGenerator
                _rootPhases = new float[_rootCount]; // COLD ALLOC: float[_rootCount] — cached root sway phase offsets — owner: CaveBioRootsGenerator
                _rootNeedsRefine = new bool[_rootCount]; // COLD ALLOC: bool[_rootCount] — deferred ceiling-anchor refinement flags — owner: CaveBioRootsGenerator
            }

            for (int i = 0; i < _rootCount; i++)
            {
                if (_rootPositions[i] == null || _rootPositions[i].Length != _segmentsPerRoot)
                    _rootPositions[i] = new Vector3[_segmentsPerRoot]; // COLD ALLOC: Vector3[_segmentsPerRoot] — per-root line positions — owner: CaveBioRootsGenerator
            }
        }

        private void EnsureRootRenderers()
        {
            for (int i = 0; i < _rootCount; i++)
            {
                if (_rootRenderers[i] != null)
                    continue;

                Transform child = transform.Find($"{RootNamePrefix}{i:00}");
                if (child == null)
                {
                    // COLD ALLOC: GameObject[1] — ceiling root visual child — owner: CaveBioRootsGenerator
                    GameObject childObject = new GameObject($"{RootNamePrefix}{i:00}");
                    child = childObject.transform;
                    child.SetParent(transform, false);
                }

                if (!child.TryGetComponent(out LineRenderer renderer))
                {
                    // COLD ALLOC: LineRenderer[1] — procedural cave-root visual — owner: CaveBioRootsGenerator
                    renderer = child.gameObject.AddComponent<LineRenderer>();
                }

                _rootTransforms[i] = child;
                _rootRenderers[i] = renderer;
            }
        }

        private void ConfigureRenderer(int rootIndex)
        {
            LineRenderer renderer = _rootRenderers[rootIndex];
            if (renderer == null)
                return;

            renderer.useWorldSpace = false;
            renderer.alignment = LineAlignment.View;
            renderer.textureMode = LineTextureMode.Stretch;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.positionCount = _segmentsPerRoot;
            renderer.widthMultiplier = 1f;
            renderer.startWidth = _topWidth;
            renderer.endWidth = _tipWidth;
            renderer.startColor = _glowColor;
            renderer.endColor = new Color(_glowColor.r, _glowColor.g, _glowColor.b, 0.18f);
        }

        private void DisableUnusedRoots()
        {
            int childCount = transform.childCount;
            for (int i = _rootCount; i < childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child != null && child.gameObject.activeSelf)
                    child.gameObject.SetActive(false);
            }
        }

        private void DisableAllRoots()
        {
            int childCount = transform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child != null && child.gameObject.activeSelf)
                    child.gameObject.SetActive(false);
            }
        }

        private void TickAnchorRefinement(float dt)
        {
            _anchorRefineTimer -= Mathf.Max(0f, dt);
            if (_anchorRefineTimer > 0f)
                return;

            _anchorRefineTimer = anchorRefineInterval;
            for (int i = 0; i < _rootCount; i++)
            {
                if (!_rootNeedsRefine[i])
                    continue;

                TryResolveAnchor(i, allowFallback: false);
            }
        }

        private bool TryResolveAnchor(int rootIndex, bool allowFallback)
        {
            if (_volumeTransform == null || !CaveRuntimeBoundsUtility.TryResolveLocalVolumeBounds(volume, _preset, out Bounds bounds))
                return false;

            float margin = 0.75f;
            float sampleX = Mathf.Lerp(bounds.min.x + margin, bounds.max.x - margin, Hash01(rootIndex + 1, 17));
            float sampleZ = Mathf.Lerp(bounds.min.z + margin, bounds.max.z - margin, Hash01(rootIndex + 1, 53));
            float rayDistance = bounds.size.y + (anchorCastPadding * 2f);
            Vector3 rayOriginLS = new Vector3(sampleX, bounds.max.y + anchorCastPadding, sampleZ);
            Vector3 rayOriginWS = _volumeTransform.TransformPoint(rayOriginLS);
            Ray ray = new Ray(rayOriginWS, Vector3.down);

            int hitCount = UnityEngine.Physics.RaycastNonAlloc(
                ray,
                _ceilingHits,
                rayDistance,
                ceilingMask,
                QueryTriggerInteraction.Ignore);

            float nearestDistance = float.PositiveInfinity;
            bool foundAnchor = false;
            Vector3 resolvedAnchorWS = rayOriginWS;
            Vector3 resolvedNormalWS = Vector3.up;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _ceilingHits[i];
                Collider hitCollider = hit.collider;
                if (hitCollider == null || !hitCollider.transform.IsChildOf(_volumeTransform) && hitCollider.transform != _volumeTransform)
                    continue;

                if (hit.distance >= nearestDistance)
                    continue;

                nearestDistance = hit.distance;
                resolvedAnchorWS = hit.point + (hit.normal.normalized * 0.03f);
                resolvedNormalWS = hit.normal.sqrMagnitude > 0.0001f ? hit.normal.normalized : Vector3.up;
                foundAnchor = true;
            }

            if (!foundAnchor)
            {
                if (!allowFallback)
                    return false;

                resolvedAnchorWS = _volumeTransform.TransformPoint(new Vector3(sampleX, bounds.max.y, sampleZ));
                resolvedNormalWS = Vector3.up;
                _rootNeedsRefine[rootIndex] = true;
            }
            else
            {
                _rootNeedsRefine[rootIndex] = false;
            }

            _rootAnchorsLocal[rootIndex] = _volumeTransform.InverseTransformPoint(resolvedAnchorWS);
            _rootLengths[rootIndex] = Mathf.Lerp(_minLength, _maxLength, Hash01(rootIndex + 1, 101));
            _rootPhases[rootIndex] = Hash01(rootIndex + 1, 149) * Mathf.PI * 2f;

            if (_rootTransforms[rootIndex] != null)
            {
                _rootTransforms[rootIndex].localPosition = Vector3.zero;
                _rootTransforms[rootIndex].localRotation = Quaternion.identity;
                _rootTransforms[rootIndex].up = resolvedNormalWS;
            }

            return true;
        }

        private void ResolvePlayerContext()
        {
            Transform runtimePlayer = BootstrapState.CurrentPlayerTransform;
            _playerTransform = runtimePlayer != null ? runtimePlayer : playerTransformOverride;
            if (_playerTransform != null && (_playerRigidbody == null || _playerRigidbody.transform != _playerTransform))
                _playerTransform.TryGetComponent(out _playerRigidbody);
        }

        private Vector3 ResolvePropWashOffset(Vector3 anchorWS, Vector3 playerPosition, Vector3 playerVelocity, float playerSpeed, float rootLength)
        {
            if (_playerTransform == null || playerSpeed <= 0.25f)
                return Vector3.zero;

            Vector3 toAnchor = anchorWS - playerPosition;
            if (toAnchor.y < 0f || toAnchor.y > (rootLength + 2f))
                return Vector3.zero;

            Vector3 horizontalDelta = new Vector3(toAnchor.x, 0f, toAnchor.z);
            float horizontalDistance = horizontalDelta.magnitude;
            if (horizontalDistance > _propWashRadius || horizontalDistance <= 0.001f)
                return Vector3.zero;

            float distanceT = 1f - Mathf.Clamp01(horizontalDistance / _propWashRadius);
            float speedT = Mathf.Clamp01(playerSpeed / 10f);
            Vector3 wakeDirectionWS = playerVelocity.sqrMagnitude > 0.0001f ? playerVelocity.normalized : horizontalDelta.normalized;
            wakeDirectionWS.y = 0f;
            if (wakeDirectionWS.sqrMagnitude <= 0.0001f)
                return Vector3.zero;

            Vector3 wakeDirectionLS = _volumeTransform.InverseTransformDirection(-wakeDirectionWS.normalized);
            return wakeDirectionLS * (_propWashStrength * distanceT * speedT);
        }

        private void TryRegister()
        {
            if (_registeredTick || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _registeredTick = true;
        }

        private void TryUnregister()
        {
            if (!_registeredTick)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _registeredTick = false;
        }

        private static float Hash01(int index, int salt)
        {
            float hash = Mathf.Sin((index * 12.9898f) + (salt * 78.233f)) * 43758.5453f;
            return hash - Mathf.Floor(hash);
        }
    }
}
