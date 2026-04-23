#if UNITY_EDITOR
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.World;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.DebugTools
{
    /// <summary>
    /// Draws a comparative ghost for player platform-local motion against absolute universe motion.
    /// </summary>
    public sealed class KinematicGhostDebugger : EditorWindow
    {
        private const int MaxHistorySamples = 96;
        private const float DefaultSampleInterval = 0.05f;
        private static readonly GUIContent _SampleIntervalLabel = new GUIContent("Sample Interval", "Seconds between ghost samples.");
        private static readonly GUIContent _GhostSizeLabel = new GUIContent("Ghost Size", "Wireframe cube size used for local and universe ghosts.");
        private static readonly GUIContent _DrawTrailsLabel = new GUIContent("Draw Trails", "Draw per-sample history trails in SceneView.");
        private static readonly GUIContent _DrawErrorVectorLabel = new GUIContent("Draw Error Vector", "Draw the delta between platform-local and absolute-universe ghosts.");
        private static readonly GUIContent _ClearHistoryLabel = new GUIContent("Clear History");

        [Header("── Sampling ──────────────────")]
        [Tooltip("Seconds between ghost samples.")]
        [SerializeField, Min(0.01f)] private float sampleInterval = DefaultSampleInterval;

        [Header("── Visuals ──────────────────")]
        [Tooltip("Wireframe cube size used for ghost markers.")]
        [SerializeField, Min(0.1f)] private float ghostSize = 0.75f;

        [Tooltip("Color for the current platform-local ghost.")]
        [SerializeField] private Color localColor = new Color(0.12f, 0.95f, 1f, 1f);

        [Tooltip("Color for the current absolute-universe ghost.")]
        [SerializeField] private Color universeColor = new Color(1f, 0.62f, 0.12f, 1f);

        [Tooltip("Color for error vectors between the two ghosts.")]
        [SerializeField] private Color errorColor = new Color(1f, 0.12f, 0.12f, 1f);

        [Tooltip("Draw trail history for both ghost streams.")]
        [SerializeField] private bool drawTrails = true;

        [Tooltip("Draw the vector between the newest local and universe ghost positions.")]
        [SerializeField] private bool drawErrorVector = true;

        // COLD ALLOC: Vector3[96] - platform-local position history ring buffer - owner: KinematicGhostDebugger
        private readonly Vector3[] _localHistory = new Vector3[MaxHistorySamples];

        // COLD ALLOC: Quaternion[96] - platform-local rotation history ring buffer - owner: KinematicGhostDebugger
        private readonly Quaternion[] _localRotationHistory = new Quaternion[MaxHistorySamples];

        // COLD ALLOC: Vector3[96] - absolute universe position history ring buffer - owner: KinematicGhostDebugger
        private readonly Vector3[] _universeHistory = new Vector3[MaxHistorySamples];

        // COLD ALLOC: Quaternion[96] - world rotation history ring buffer - owner: KinematicGhostDebugger
        private readonly Quaternion[] _worldRotationHistory = new Quaternion[MaxHistorySamples];

        private Transform _playerTransform;
        private PlayerTransportCoordinator _transportCoordinator;
        private ITransportPlatform _transportPlatform;
        private Transform _platformTransform;
        private bool _subscribed;
        private int _historyCount;
        private int _historyHead;
        private double _nextSampleTime;

        [MenuItem("Hecton8/Debug/Kinematic Ghost")]
        private static void Open()
        {
            GetWindow<KinematicGhostDebugger>("Kinematic Ghost");
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Play Mode only. Player reference resolves from SceneBootstrap.");
            sampleInterval = EditorGUILayout.Slider(_SampleIntervalLabel, sampleInterval, 0.01f, 0.25f);
            ghostSize = EditorGUILayout.Slider(_GhostSizeLabel, ghostSize, 0.1f, 2f);
            drawTrails = EditorGUILayout.Toggle(_DrawTrailsLabel, drawTrails);
            drawErrorVector = EditorGUILayout.Toggle(_DrawErrorVectorLabel, drawErrorVector);

            using (new EditorGUI.DisabledScope(_historyCount == 0))
            {
                if (GUILayout.Button(_ClearHistoryLabel))
                    ClearHistory();
            }
        }

        private void Subscribe()
        {
            if (_subscribed)
                return;

            _subscribed = true;
            EditorApplication.update += HandleEditorUpdate;
            SceneView.duringSceneGui += HandleSceneGui;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
                return;

            _subscribed = false;
            EditorApplication.update -= HandleEditorUpdate;
            SceneView.duringSceneGui -= HandleSceneGui;
        }

        private void HandleEditorUpdate()
        {
            if (!EditorApplication.isPlaying)
                return;

            if (EditorApplication.timeSinceStartup < _nextSampleTime)
                return;

            _nextSampleTime = EditorApplication.timeSinceStartup + sampleInterval;
            RefreshTargets();
            if (_playerTransform == null)
                return;

            SampleGhostState();
            SceneView.RepaintAll();
            Repaint();
        }

        private void HandleSceneGui(SceneView sceneView)
        {
            if (!EditorApplication.isPlaying || _historyCount == 0 || _playerTransform == null)
                return;

            DrawGhostTrails();
            DrawCurrentGhosts();
        }

        private void RefreshTargets()
        {
            Transform previousPlatformTransform = _platformTransform;
            if (!SceneBootstrap.TryGetCurrentPlayerTransform(out Transform resolvedPlayer))
            {
                _playerTransform = null;
                _transportCoordinator = null;
                _transportPlatform = null;
                _platformTransform = null;
                return;
            }

            if (_playerTransform != resolvedPlayer)
            {
                _playerTransform = resolvedPlayer;
                _transportCoordinator = null;
                _transportPlatform = null;
                _platformTransform = null;
                ClearHistory();
            }

            if (_transportCoordinator == null && _playerTransform != null)
                _playerTransform.TryGetComponent(out _transportCoordinator);

            if (_transportCoordinator != null &&
                _transportCoordinator.TryResolveTransportLifecycleOwner(out IPlayerTransportLifecycleOwner lifecycleOwner))
            {
                _transportPlatform = lifecycleOwner as ITransportPlatform;
                _platformTransform = _transportPlatform != null && _transportPlatform.IsTransportPlatformActive
                    ? _transportPlatform.PlatformTransform
                    : null;
            }
            else
            {
                _transportPlatform = null;
                _platformTransform = null;
            }

            if (!object.ReferenceEquals(previousPlatformTransform, _platformTransform))
                ClearHistory();
        }

        private void SampleGhostState()
        {
            Vector3 playerWorldPosition = _playerTransform.position;
            Quaternion playerWorldRotation = _playerTransform.rotation;
            Vector3 universePosition = ToAbsoluteUniversePosition(playerWorldPosition);

            Vector3 localPosition = playerWorldPosition;
            Quaternion localRotation = playerWorldRotation;

            if (_platformTransform != null)
            {
                localPosition = _platformTransform.InverseTransformPoint(playerWorldPosition);
                localRotation = Quaternion.Inverse(_platformTransform.rotation) * playerWorldRotation;
            }

            _localHistory[_historyHead] = localPosition;
            _localRotationHistory[_historyHead] = localRotation;
            _universeHistory[_historyHead] = universePosition;
            _worldRotationHistory[_historyHead] = playerWorldRotation;

            _historyHead = (_historyHead + 1) % MaxHistorySamples;
            if (_historyCount < MaxHistorySamples)
                _historyCount++;
        }

        private void DrawGhostTrails()
        {
            if (!drawTrails)
                return;

            Handles.color = localColor;
            for (int i = 1; i < _historyCount; i++)
            {
                Vector3 previous = ResolveLocalPreviewPosition(GetHistoryIndex(i - 1));
                Vector3 current = ResolveLocalPreviewPosition(GetHistoryIndex(i));
                Handles.DrawLine(previous, current);
            }

            Handles.color = universeColor;
            for (int i = 1; i < _historyCount; i++)
            {
                Vector3 previous = ResolveUniversePreviewPosition(GetHistoryIndex(i - 1));
                Vector3 current = ResolveUniversePreviewPosition(GetHistoryIndex(i));
                Handles.DrawLine(previous, current);
            }
        }

        private void DrawCurrentGhosts()
        {
            int newestIndex = GetHistoryIndex(_historyCount - 1);
            Vector3 localPosition = ResolveLocalPreviewPosition(newestIndex);
            Quaternion localRotation = ResolveLocalPreviewRotation(newestIndex);
            Vector3 universePosition = ResolveUniversePreviewPosition(newestIndex);
            Quaternion universeRotation = _worldRotationHistory[newestIndex];

            Handles.color = localColor;
            Handles.matrix = Matrix4x4.TRS(localPosition, localRotation, Vector3.one * ghostSize);
            Handles.DrawWireCube(Vector3.zero, Vector3.one);

            Handles.color = universeColor;
            Handles.matrix = Matrix4x4.TRS(universePosition, universeRotation, Vector3.one * ghostSize);
            Handles.DrawWireCube(Vector3.zero, Vector3.one);

            Handles.matrix = Matrix4x4.identity;

            if (!drawErrorVector)
                return;

            Handles.color = errorColor;
            Handles.DrawLine(localPosition, universePosition);
        }

        private Vector3 ResolveLocalPreviewPosition(int historyIndex)
        {
            if (_platformTransform == null)
                return _localHistory[historyIndex];

            return _platformTransform.TransformPoint(_localHistory[historyIndex]);
        }

        private Quaternion ResolveLocalPreviewRotation(int historyIndex)
        {
            if (_platformTransform == null)
                return _localRotationHistory[historyIndex];

            return _platformTransform.rotation * _localRotationHistory[historyIndex];
        }

        private Vector3 ResolveUniversePreviewPosition(int historyIndex)
        {
            Vector3 currentWorldPosition = _playerTransform.position;
            Vector3 currentUniversePosition = ToAbsoluteUniversePosition(currentWorldPosition);
            Vector3 absoluteDelta = _universeHistory[historyIndex] - currentUniversePosition;
            return currentWorldPosition + absoluteDelta;
        }

        private static Vector3 ToAbsoluteUniversePosition(Vector3 runtimePosition)
        {
            Vector3 bridgeUniversePosition = HectonMapMagicVegetationBridge.ToUniverseSpace(runtimePosition);
            if (bridgeUniversePosition != runtimePosition)
                return bridgeUniversePosition;

            HectonFloatingOrigin floatingOrigin = HectonFloatingOrigin.Instance;
            return floatingOrigin != null
                ? runtimePosition + floatingOrigin.TotalUniverseOffset
                : runtimePosition;
        }

        private int GetHistoryIndex(int orderedIndex)
        {
            int oldestIndex = _historyCount == MaxHistorySamples ? _historyHead : 0;
            return (oldestIndex + orderedIndex) % MaxHistorySamples;
        }

        private void ClearHistory()
        {
            _historyCount = 0;
            _historyHead = 0;
            _nextSampleTime = 0d;
        }
    }
}
#endif
