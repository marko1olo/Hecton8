using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Caves
{
    /// <summary>
    /// Runtime owner for hanging bioluminescent cave roots attached to voxel cave ceilings.
    /// Anchors use deterministic local-bounds sampling; root motion stays on the tick path.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CaveBioRootsGenerator : MonoBehaviour, ITickable, IUpdatable
    {
        private const string RootNamePrefix = "_BioRoot_";
        private const int RootNameCacheCapacity = 32;
        private const float CeilingAnchorInset = 0.12f;
        private static readonly string[] _RootNames = CreateTwoDigitNameCache(RootNamePrefix, RootNameCacheCapacity); // COLD ALLOC: string[32] — bounded bio-root child names — owner: CaveBioRootsGenerator

        [Header("── Runtime Wiring ──────────────────")]
        [SerializeField]
        [Tooltip("Voxel volume that owns the cave mesh and local-space bounds.")]
        private HectonVoxelVolume volume;

        [SerializeField]
        [Tooltip("Optional player override used when bootstrap has not published the runtime player yet.")]
        private Transform playerTransformOverride;

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
        private float _swayTime;
        private bool _registeredTick;
        private LineRenderer[] _rootRenderers;
        private Transform[] _rootTransforms;
        private Vector3[][] _rootPositions;
        private Vector3[] _rootAnchorsLocal;
        private float[] _rootLengths;
        private float[] _rootPhases;

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
            _rootCount = Mathf.Clamp(
                Mathf.RoundToInt(config.maxCount * Mathf.Clamp01(globalIntensity)),
                0,
                Mathf.Min(config.maxCount, RootNameCacheCapacity));

            EnsureBuffers();
            EnsureRootRenderers();

            for (int i = 0; i < _rootCount; i++)
            {
                ConfigureRenderer(i);
                ResolveAnchor(i);
                if (_rootTransforms[i] != null && !_rootTransforms[i].gameObject.activeSelf)
                    _rootTransforms[i].gameObject.SetActive(true);
            }

            DisableUnusedRoots();
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
            _swayTime += math.max(0f, dt);

            Vector3 playerPosition = _playerTransform != null ? _playerTransform.position : Vector3.zero;
            Vector3 playerVelocity = _playerRigidbody != null ? _playerRigidbody.linearVelocity : Vector3.zero;
            float playerSpeedSq = playerVelocity.sqrMagnitude;
            float playerSpeed = playerSpeedSq > 0.0625f ? EstimateLength3D(playerVelocity) : 0f;
            float time = _swayTime;

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

                string rootName = GetCachedRootName(i);
                Transform child = transform.Find(rootName);
                if (child == null)
                {
                    // COLD ALLOC: GameObject[1] — ceiling root visual child — owner: CaveBioRootsGenerator
                    GameObject childObject = new GameObject(rootName);
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

        private bool ResolveAnchor(int rootIndex)
        {
            if (_volumeTransform == null || !CaveRuntimeBoundsUtility.TryResolveLocalVolumeBounds(volume, _preset, out Bounds bounds))
                return false;

            float margin = 0.75f;
            float sampleX = math.lerp(bounds.min.x + margin, bounds.max.x - margin, Hash01(rootIndex + 1, 17));
            float sampleZ = math.lerp(bounds.min.z + margin, bounds.max.z - margin, Hash01(rootIndex + 1, 53));
            _rootAnchorsLocal[rootIndex] = new Vector3(sampleX, bounds.max.y - CeilingAnchorInset, sampleZ);
            _rootLengths[rootIndex] = math.lerp(_minLength, _maxLength, Hash01(rootIndex + 1, 101));
            _rootPhases[rootIndex] = Hash01(rootIndex + 1, 149) * Mathf.PI * 2f;

            if (_rootTransforms[rootIndex] != null)
            {
                _rootTransforms[rootIndex].localPosition = Vector3.zero;
                _rootTransforms[rootIndex].localRotation = Quaternion.identity;
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
            float horizontalDistanceSq = (horizontalDelta.x * horizontalDelta.x) + (horizontalDelta.z * horizontalDelta.z);
            float propWashRadiusSq = _propWashRadius * _propWashRadius;
            if (horizontalDistanceSq > propWashRadiusSq || horizontalDistanceSq <= 0.000001f)
                return Vector3.zero;

            float distanceT = 1f - math.saturate(horizontalDistanceSq / math.max(0.0001f, propWashRadiusSq));
            float speedT = math.saturate(playerSpeed * 0.1f);
            Vector3 wakeDirectionWS = playerVelocity.sqrMagnitude > 0.0001f ? playerVelocity : horizontalDelta;
            wakeDirectionWS.y = 0f;
            float wakeDirectionSq = (wakeDirectionWS.x * wakeDirectionWS.x) + (wakeDirectionWS.z * wakeDirectionWS.z);
            if (wakeDirectionSq <= 0.0001f)
                return Vector3.zero;

            wakeDirectionWS *= ApproximateInvLength2D(wakeDirectionWS);
            Vector3 wakeDirectionLS = _volumeTransform.InverseTransformDirection(-wakeDirectionWS);
            return wakeDirectionLS * (_propWashStrength * distanceT * speedT);
        }

        private static float EstimateLength3D(Vector3 value)
        {
            float ax = math.abs(value.x);
            float ay = math.abs(value.y);
            float az = math.abs(value.z);
            float maxAxis = math.max(ax, math.max(ay, az));
            float minAxis = math.min(ax, math.min(ay, az));
            float midAxis = ax + ay + az - maxAxis - minAxis;
            return maxAxis + (midAxis * 0.375f) + (minAxis * 0.25f);
        }

        private static float ApproximateInvLength2D(Vector3 value)
        {
            float ax = math.abs(value.x);
            float az = math.abs(value.z);
            float length = math.max(ax, az) + (math.min(ax, az) * 0.375f);
            return length > 0.0001f ? 1f / length : 0f;
        }

        private void TryRegister()
        {
            if (_registeredTick || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _registeredTick = GlobalRegistry.Updatables.Contains(this);
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

        private static string GetCachedRootName(int index)
        {
            return (uint)index < (uint)_RootNames.Length
                ? _RootNames[index]
                : RootNamePrefix;
        }

        private static string[] CreateTwoDigitNameCache(string prefix, int count)
        {
            string[] names = new string[count];
            for (int i = 0; i < count; i++)
                names[i] = i < 10 ? prefix + "0" + i : prefix + i;

            return names;
        }
    }
}
