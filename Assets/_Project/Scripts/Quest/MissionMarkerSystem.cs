using System.Runtime.InteropServices;
using Hecton8.AtlasSignal;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.World;
using Unity.Mathematics;
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
    public sealed class MissionMarkerSystem : MonoBehaviour, IUpdatable, IRenderable, IQuestEventListener
    {
        private const string MarkerShaderPath = "Assets/_Project/Art/Shaders/Hecton_ScannerMarkerInstanced.shader";
        private const int MaxMarkers = 32;
        private const float MinimumDistanceMeters = 3f;
        private const double MarkerRebuildMoveThresholdMetersSq = 64d;
        private const uint ActiveMarkerOverflowWarningHash = 0x4D4D4151u; // MMAQ
        private const uint MarkerCacheOverflowWarningHash = 0x4D4D4351u; // MMCQ
        private const uint MarkerContextHash = 0x4D4D4354u; // MMCT
        private static readonly uint _atlasCoreMarkerTargetHash = QuestFlagHashKernel.ComputeStableHash("atlas6_core");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int FlickerFrequencyId = Shader.PropertyToID("_FlickerFrequency");
        private static readonly int FlickerIntensityId = Shader.PropertyToID("_FlickerIntensity");

        [StructLayout(LayoutKind.Sequential)]
        private struct QuestMarkerCache
        {
            public uint TargetHash;
            public AbsoluteUniversePosition FallbackAup;
            public Vector3 FallbackPosition;
            public float HeightOffset;
            public byte HasFallbackPosition;
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

        // COLD ALLOC: uint[32] - quest hash marker cache keys - owner: MissionMarkerSystem
        private readonly uint[] _markerCacheQuestHashes = new uint[MaxMarkers];
        // COLD ALLOC: QuestMarkerCache[32] - quest hash marker cache values - owner: MissionMarkerSystem
        private readonly QuestMarkerCache[] _markerCaches = new QuestMarkerCache[MaxMarkers];
        // COLD ALLOC: uint[32] - active quest hash scan buffer - owner: MissionMarkerSystem
        private readonly uint[] _activeQuestHashes = new uint[MaxMarkers];
        // COLD ALLOC: Matrix4x4[32] - instanced quest marker matrices - owner: MissionMarkerSystem
        private readonly Matrix4x4[] _markerMatrices = new Matrix4x4[MaxMarkers];

        private HectonPlayerMovement _playerMovement;
        private Material _runtimeMarkerMaterial;
        private Mesh _runtimeMarkerMesh;
        private int _visibleMarkerCount;
        private int _markerCacheCount;
        private int _activeQuestCount;
        private int _droppedActiveMarkerCount;
        private int _droppedMarkerCacheCount;
        private int _lastActiveMarkerOverflowTelemetryFrame = -1;
        private int _lastMarkerCacheOverflowTelemetryFrame = -1;
        private bool _activeQuestSetPrimed;
        private bool _registeredUpdatable;
        private bool _registeredRenderable;
        private bool _markerCacheDirty = true;
        private bool _hasMarkerRebuildPlayerAup;
        private float _cachedMarkerScaleMeters = -1f;
        private AbsoluteUniversePosition _lastMarkerRebuildPlayerAup;

        /// <summary>
        /// Number of active quest marker hashes rejected by the fixed marker budget.
        /// </summary>
        public int DroppedActiveMarkerCount => _droppedActiveMarkerCount;

        /// <summary>
        /// Number of quest marker presentation cache entries rejected by the fixed cache budget.
        /// </summary>
        public int DroppedMarkerCacheCount => _droppedMarkerCacheCount;

        private void Awake()
        {
            EnsureRuntimeResources();
            ResolvePlayerContextCold();
        }

        private void OnEnable()
        {
            EnsureRuntimeResources();
            ResolvePlayerContextCold();
            PrimeActiveQuestSet();
            QuestEvents.Register(this);
            RegisterRuntime();
        }

        private void OnDisable()
        {
            QuestEvents.Unregister(this);
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
            if (!_activeQuestSetPrimed)
                PrimeActiveQuestSet();

            if (_activeQuestCount <= 0)
            {
                _visibleMarkerCount = 0;
                return;
            }

            ResolvePlayerContext();
            if (_cachedMarkerScaleMeters != markerScaleMeters)
                _markerCacheDirty = true;

            if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
            {
                _visibleMarkerCount = 0;
                _hasMarkerRebuildPlayerAup = false;
                return;
            }

            if (!_markerCacheDirty && _hasMarkerRebuildPlayerAup)
            {
                double movedSq = AbsoluteUniversePosition.DistanceSq(in playerAup, in _lastMarkerRebuildPlayerAup);
                if (movedSq < MarkerRebuildMoveThresholdMetersSq)
                    return;
            }

            RebuildMarkerCache(in playerAup);
            _lastMarkerRebuildPlayerAup = playerAup;
            _hasMarkerRebuildPlayerAup = true;
            _markerCacheDirty = false;
        }

        public void OnQuestEvent(in QuestEventPayload payload)
        {
            uint questHash = payload.QuestHashID;
            if (questHash == 0u)
                return;

            switch ((QuestEventType)payload.EventType)
            {
                case QuestEventType.Activated:
                case QuestEventType.RevertRequested:
                    AddActiveQuestHash(questHash);
                    break;
                case QuestEventType.Completed:
                case QuestEventType.Failed:
                    RemoveActiveQuestHash(questHash);
                    break;
            }
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

            _runtimeMarkerMaterial.SetColor(BaseColorId, markerColor);
            _runtimeMarkerMaterial.SetFloat(FlickerFrequencyId, flickerFrequency);
            _runtimeMarkerMaterial.SetFloat(FlickerIntensityId, flickerIntensity);

            UnityEngine.Graphics.DrawMeshInstanced(
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

        private void ResolvePlayerContext()
        {
            if (_playerMovement != null)
                return;

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext != null)
            {
                _playerMovement = playerContext.PlayerMovement;
            }
        }

        private void ResolvePlayerContextCold()
        {
            ResolvePlayerContext();
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

        private void RebuildMarkerCache(in AbsoluteUniversePosition playerAup)
        {
            _visibleMarkerCount = 0;

            QuestManager questManager = GlobalRegistry.Quest;
            if (questManager == null)
                return;

            double maxDistanceSq = (double)maxVisibleDistanceMeters * maxVisibleDistanceMeters;
            double minDistanceSq = (double)MinimumDistanceMeters * MinimumDistanceMeters;
            float safeScale = math.max(0.25f, markerScaleMeters);
            Vector3 uniformScale = new Vector3(safeScale, safeScale, safeScale);
            _cachedMarkerScaleMeters = markerScaleMeters;

            for (int i = 0; i < _activeQuestCount && _visibleMarkerCount < MaxMarkers; i++)
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

                double distanceSq = AbsoluteUniversePosition.DistanceSq(in markerAup, in playerAup);
                if (distanceSq < minDistanceSq || distanceSq > maxDistanceSq)
                    continue;

                int markerIndex = _visibleMarkerCount++;
                _markerMatrices[markerIndex] = Matrix4x4.TRS(markerWorldPosition, Quaternion.identity, uniformScale);
            }
        }

        private void PrimeActiveQuestSet()
        {
            _activeQuestCount = 0;
            QuestManager questManager = GlobalRegistry.Quest;
            if (questManager == null)
            {
                _activeQuestSetPrimed = false;
                return;
            }

            _activeQuestCount = questManager.CopyActiveQuestHashes(_activeQuestHashes);
            _activeQuestSetPrimed = true;
            _markerCacheDirty = true;
        }

        private void AddActiveQuestHash(uint questHash)
        {
            if (!_activeQuestSetPrimed)
                PrimeActiveQuestSet();

            for (int i = 0; i < _activeQuestCount; i++)
            {
                if (_activeQuestHashes[i] == questHash)
                {
                    _markerCacheDirty = true;
                    return;
                }
            }

            if (_activeQuestCount >= MaxMarkers)
            {
                ReportActiveMarkerOverflow(questHash);
                return;
            }

            _activeQuestHashes[_activeQuestCount++] = questHash;
            _markerCacheDirty = true;
        }

        private void RemoveActiveQuestHash(uint questHash)
        {
            for (int i = 0; i < _activeQuestCount; i++)
            {
                if (_activeQuestHashes[i] != questHash)
                    continue;

                int lastIndex = --_activeQuestCount;
                _activeQuestHashes[i] = _activeQuestHashes[lastIndex];
                _activeQuestHashes[lastIndex] = 0u;
                _markerCacheDirty = true;
                if (_activeQuestCount == 0)
                    _visibleMarkerCount = 0;
                return;
            }
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            if (_playerMovement == null)
                ResolvePlayerContext();

            if (_playerMovement != null)
            {
                playerAup = _playerMovement.CurrentAup;
                return true;
            }

            playerAup = default;
            return false;
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

                AbsoluteUniversePosition atlasCoreAup = atlasSignalSystem.AtlasCoreAup;
                markerAup = ResolveOffsetAup(in atlasCoreAup, cache.HeightOffset);
                float3 runtimePosition = markerAup.ToRuntimeFloat3();
                resolvedPosition = new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            }
            else if (cache.HasFallbackPosition != 0)
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

        private static AbsoluteUniversePosition ResolveOffsetAup(in AbsoluteUniversePosition sourceAup, float heightOffsetMeters)
        {
            if (heightOffsetMeters == 0f)
                return sourceAup;

            double3 absolute = sourceAup.ToAbsoluteDouble3();
            absolute.y += heightOffsetMeters;
            return AbsoluteUniversePosition.FromAbsolutePosition(absolute);
        }

        private bool TryResolveMarkerCache(QuestManager questManager, uint questHash, out QuestMarkerCache cache)
        {
            for (int i = 0; i < _markerCacheCount; i++)
            {
                if (_markerCacheQuestHashes[i] == questHash)
                {
                    cache = _markerCaches[i];
                    return true;
                }
            }

            if (_markerCacheCount >= MaxMarkers)
            {
                ReportMarkerCacheOverflow(questHash);
                cache = default;
                return false;
            }

            if (!questManager.TryGetQuestPresentation(
                    questHash,
                    out _,
                    out _,
                    out uint markerTargetHash,
                    out Vector3 markerWorldPosition,
                    out float markerHeightOffset))
            {
                cache = default;
                return false;
            }

            float heightOffset = math.max(0f, markerHeightOffset);
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
                HasFallbackPosition = hasFallbackPosition ? (byte)1 : (byte)0
            };

            int cacheIndex = _markerCacheCount++;
            _markerCacheQuestHashes[cacheIndex] = questHash;
            _markerCaches[cacheIndex] = cache;
            return true;
        }

        private void ReportActiveMarkerOverflow(uint questHash)
        {
            _droppedActiveMarkerCount++;
            int frame = Time.frameCount;
            if (_lastActiveMarkerOverflowTelemetryFrame == frame)
                return;

            _lastActiveMarkerOverflowTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                ActiveMarkerOverflowWarningHash,
                MarkerContextHash ^ questHash,
                math.max(1, _droppedActiveMarkerCount));
        }

        private void ReportMarkerCacheOverflow(uint questHash)
        {
            _droppedMarkerCacheCount++;
            int frame = Time.frameCount;
            if (_lastMarkerCacheOverflowTelemetryFrame == frame)
                return;

            _lastMarkerCacheOverflowTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                MarkerCacheOverflowWarningHash,
                MarkerContextHash ^ questHash,
                math.max(1, _droppedMarkerCacheCount));
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
