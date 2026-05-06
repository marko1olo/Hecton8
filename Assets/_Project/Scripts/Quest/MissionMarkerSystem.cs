using System.Collections.Generic;
using Hecton8.AtlasSignal;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.World;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Quest
{
    /// <summary>
    /// Zero-allocation instanced world marker renderer for active quest objectives.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MissionMarkerSystem : MonoBehaviour, IUpdatable, IRenderable
    {
        private const string MarkerShaderPath = "Assets/_Project/Art/Shaders/Hecton_ScannerMarkerInstanced.shader";
        private const int MaxMarkers = 32;
        private const float MinimumDistanceMeters = 3f;
        private static readonly uint _atlasCoreMarkerTargetHash = QuestFlagHashKernel.ComputeStableHash("atlas6_core");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int FlickerFrequencyId = Shader.PropertyToID("_FlickerFrequency");
        private static readonly int FlickerIntensityId = Shader.PropertyToID("_FlickerIntensity");

        private struct QuestMarkerCache
        {
            public uint TargetHash;
            public Vector3 FallbackPosition;
            public AbsoluteUniversePosition FallbackAup;
            public float HeightOffset;
            public bool HasFallbackPosition;
        }

        [Header("── Appearance ───────────────────────")]
        [Tooltip("Instanced shader used for quest markers.")]
        [SerializeField] private Shader markerShader;

        [Tooltip("Base marker tint.")]
        [SerializeField] private Color markerColor = new Color(1f, 0.62f, 0.08f, 0.9f);

        [Tooltip("Uniform marker scale in world meters.")]
        [SerializeField, Min(0.25f)] private float markerScaleMeters = 5f;

        [Tooltip("Maximum world-space marker range from the player.")]
        [SerializeField, Min(5f)] private float maxVisibleDistanceMeters = 6000f;

        [Tooltip("Per-frame flicker frequency fed into the instanced marker shader.")]
        [SerializeField, Min(0f)] private float flickerFrequency = 18f;

        [Tooltip("Per-frame flicker intensity fed into the instanced marker shader.")]
        [SerializeField, Range(0f, 0.4f)] private float flickerIntensity = 0.08f;

        // COLD ALLOC: Dictionary<uint,QuestMarkerCache>[32] - quest hash to authored marker cache - owner: MissionMarkerSystem
        private readonly Dictionary<uint, QuestMarkerCache> _markerCacheByQuestHash = new Dictionary<uint, QuestMarkerCache>(MaxMarkers);
        // COLD ALLOC: uint[32] - active quest hash scan buffer - owner: MissionMarkerSystem
        private readonly uint[] _activeQuestHashes = new uint[MaxMarkers];
        // COLD ALLOC: Vector3[32] - resolved marker world positions - owner: MissionMarkerSystem
        private readonly Vector3[] _markerWorldPositions = new Vector3[MaxMarkers];
        // COLD ALLOC: Matrix4x4[32] - instanced quest marker matrices - owner: MissionMarkerSystem
        private readonly Matrix4x4[] _markerMatrices = new Matrix4x4[MaxMarkers];

        private Transform _playerTransform;
        private Material _runtimeMarkerMaterial;
        private Mesh _runtimeMarkerMesh;
        private int _visibleMarkerCount;
        private bool _registeredUpdatable;
        private bool _registeredRenderable;

        private void Awake()
        {
            EnsureRuntimeResources();
            ResolvePlayerTransform();
        }

        private void OnEnable()
        {
            RegisterRuntime();
        }

        private void OnDisable()
        {
            UnregisterRuntime();
        }

        private void OnDestroy()
        {
            UnregisterRuntime();

            if (_runtimeMarkerMaterial != null)
            {
                Destroy(_runtimeMarkerMaterial);
                _runtimeMarkerMaterial = null;
            }

            if (_runtimeMarkerMesh != null)
            {
                Destroy(_runtimeMarkerMesh);
                _runtimeMarkerMesh = null;
            }
        }

        /// <summary>
        /// Rebuilds the active quest marker cache for the current frame.
        /// </summary>
        /// <param name="deltaTime">Scaled frame delta supplied by the dispatcher.</param>
        public void Tick(float deltaTime)
        {
            ResolvePlayerTransform();
            EnsureRuntimeResources();
            RebuildMarkerCache();
        }

        /// <summary>
        /// Draws the active quest marker batch for the current SRP camera callback.
        /// </summary>
        /// <param name="deltaTime">Scaled frame delta supplied by the render dispatcher.</param>
        public void Render(float deltaTime)
        {
            if (_visibleMarkerCount <= 0 || _runtimeMarkerMaterial == null || _runtimeMarkerMesh == null)
                return;

            Camera camera = GlobalRenderContext.CurrentCamera;
            if (camera == null ||
                camera.cameraType == CameraType.Preview ||
                camera.cameraType == CameraType.Reflection)
            {
                return;
            }

            Transform cameraTransform = camera.transform;
            Quaternion rotation = cameraTransform.rotation;
            Vector3 uniformScale = new Vector3(markerScaleMeters, markerScaleMeters, markerScaleMeters);
            for (int i = 0; i < _visibleMarkerCount; i++)
                _markerMatrices[i] = Matrix4x4.TRS(_markerWorldPositions[i], rotation, uniformScale);

            _runtimeMarkerMaterial.SetColor(BaseColorId, markerColor);
            _runtimeMarkerMaterial.SetFloat(FlickerFrequencyId, flickerFrequency);
            _runtimeMarkerMaterial.SetFloat(FlickerIntensityId, flickerIntensity);

            Graphics.DrawMeshInstanced(
                _runtimeMarkerMesh,
                0,
                _runtimeMarkerMaterial,
                _markerMatrices,
                _visibleMarkerCount,
                null,
                ShadowCastingMode.Off,
                false,
                0,
                camera,
                LightProbeUsage.Off,
                null);
        }

        private void RegisterRuntime()
        {
            if (Application.isPlaying && GlobalRegistry.Dispatcher != null && !_registeredUpdatable)
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
                _registeredUpdatable = GlobalRegistry.Updatables.Contains(this);
            }

            if (!_registeredRenderable)
            {
                GlobalRegistry.Renderables.Register(this);
                _registeredRenderable = GlobalRegistry.Renderables.Contains(this);
            }
        }

        private void UnregisterRuntime()
        {
            if (_registeredUpdatable)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
                _registeredUpdatable = false;
            }

            if (_registeredRenderable)
            {
                GlobalRegistry.Renderables.Unregister(this);
                _registeredRenderable = false;
            }
        }

        private void ResolvePlayerTransform()
        {
            if (_playerTransform == null)
                SceneBootstrap.TryGetCurrentPlayerTransform(out _playerTransform);
        }

        private void EnsureRuntimeResources()
        {
            if (_runtimeMarkerMesh == null)
                _runtimeMarkerMesh = CreateMarkerMesh();

            if (_runtimeMarkerMaterial != null)
                return;

#if UNITY_EDITOR
            if (markerShader == null)
                markerShader = AssetDatabase.LoadAssetAtPath<Shader>(MarkerShaderPath);
#endif

            if (markerShader == null)
                return;

            _runtimeMarkerMaterial = new Material(markerShader)
            {
                enableInstancing = true,
                hideFlags = HideFlags.DontSave
            };
        }

        private void RebuildMarkerCache()
        {
            _visibleMarkerCount = 0;

            QuestManager questManager = GlobalRegistry.Quest;
            if (questManager == null)
                return;

            int activeQuestCount = questManager.CopyActiveQuestHashes(_activeQuestHashes);
            bool hasPlayer = _playerTransform != null;
            AbsoluteUniversePosition playerAup = hasPlayer
                ? AbsoluteUniversePosition.FromRuntimePosition(_playerTransform.position)
                : default;
            double maxDistanceSq = (double)maxVisibleDistanceMeters * maxVisibleDistanceMeters;
            double minDistanceSq = (double)MinimumDistanceMeters * MinimumDistanceMeters;

            for (int i = 0; i < activeQuestCount && _visibleMarkerCount < MaxMarkers; i++)
            {
                uint questHash = _activeQuestHashes[i];
                if (questHash == 0u)
                    continue;

                if (!TryResolveMarkerPosition(
                        questManager,
                        questHash,
                        out Vector3 markerWorldPosition,
                        out AbsoluteUniversePosition markerAup))
                {
                    continue;
                }

                if (hasPlayer)
                {
                    double distanceSq = AbsoluteUniversePosition.DistanceSq(in markerAup, in playerAup);
                    if (distanceSq < minDistanceSq || distanceSq > maxDistanceSq)
                        continue;
                }

                _markerWorldPositions[_visibleMarkerCount++] = markerWorldPosition;
            }
        }

        private bool TryResolveMarkerPosition(
            QuestManager questManager,
            uint questHash,
            out Vector3 markerWorldPosition,
            out AbsoluteUniversePosition markerAup)
        {
            markerWorldPosition = default;
            markerAup = default;
            if (!TryResolveMarkerCache(questManager, questHash, out QuestMarkerCache cache))
                return false;

            Vector3 resolvedPosition;
            if (cache.TargetHash == _atlasCoreMarkerTargetHash)
            {
                AtlasSignalSystem atlasSignalSystem = Hecton8.Core.GlobalRegistry.AtlasSignal;
                if (atlasSignalSystem == null)
                    return false;

                resolvedPosition = atlasSignalSystem.AtlasCorePosition + (Vector3.up * cache.HeightOffset);
                markerAup = AbsoluteUniversePosition.FromRuntimePosition(resolvedPosition);
            }
            else if (cache.HasFallbackPosition)
            {
                resolvedPosition = cache.FallbackPosition;
                markerAup = cache.FallbackAup;
            }
            else
            {
                return false;
            }

            markerWorldPosition = resolvedPosition;
            return true;
        }

        private bool TryResolveMarkerCache(QuestManager questManager, uint questHash, out QuestMarkerCache cache)
        {
            if (_markerCacheByQuestHash.TryGetValue(questHash, out cache))
                return true;

            if (!questManager.TryGetQuestPresentation(
                    questHash,
                    out _,
                    out _,
                    out uint markerTargetHash,
                    out Vector3 markerWorldPosition,
                    out float markerHeightOffset))
            {
                return false;
            }

            float heightOffset = Mathf.Max(0f, markerHeightOffset);
            Vector3 resolvedFallbackPosition = markerWorldPosition + (Vector3.up * heightOffset);
            bool hasFallbackPosition = markerWorldPosition.sqrMagnitude > 0.0001f;
            cache = new QuestMarkerCache
            {
                TargetHash = markerTargetHash,
                FallbackPosition = resolvedFallbackPosition,
                FallbackAup = hasFallbackPosition
                    ? AbsoluteUniversePosition.FromRuntimePosition(resolvedFallbackPosition)
                    : default,
                HeightOffset = heightOffset,
                HasFallbackPosition = hasFallbackPosition
            };

            _markerCacheByQuestHash[questHash] = cache;
            return true;
        }

        private static Mesh CreateMarkerMesh()
        {
            Mesh mesh = new Mesh
            {
                name = "QuestMarkerDiamond"
            };

            Vector3[] vertices =
            {
                new Vector3(0f, 0.75f, 0f),
                new Vector3(0.6f, 0f, 0f),
                new Vector3(0f, 0f, 0.6f),
                new Vector3(-0.6f, 0f, 0f),
                new Vector3(0f, 0f, -0.6f),
                new Vector3(0f, -0.9f, 0f)
            };

            int[] triangles =
            {
                0, 1, 2,
                0, 2, 3,
                0, 3, 4,
                0, 4, 1,
                5, 2, 1,
                5, 3, 2,
                5, 4, 3,
                5, 1, 4
            };

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            return mesh;
        }
    }
}
