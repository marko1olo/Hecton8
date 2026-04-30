using Hecton8.Atmosphere;
using Hecton8.Bootstrap;
using Hecton8.Core;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Celestial
{
    /// <summary>
    /// Observer-relative celestial placement for far bodies rendered by the space camera.
    /// </summary>
    /// <remarks>
    /// The body stays locked to the observer-facing sky rig so it does not exhibit
    /// kilometer-scale world parallax against the horizon.
    /// Runtime cadence follows project tick ownership instead of LateUpdate.
    /// </remarks>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(2000)]
    public sealed class ObserverRelativeCelestialBody : MonoBehaviour, ITickable
    {
        /// <summary>
        /// Placement solve mode for this body.
        /// </summary>
        public enum CelestialPlacementMode
        {
            FixedDirection = 0,
            OrbitAroundParent = 1
        }

        /// <summary>
        /// Time owner used to advance apparent orbit and axial spin.
        /// </summary>
        public enum CelestialTimeSourceMode
        {
            AtmosphereCycle = 0,
            RealtimeSeconds = 1
        }

        private const float MinAnchorDistance = 100f;
        private const float MinAngularDiameter = 0.05f;
        private const float MaxAngularDiameter = 179f;
        private const float MinMeshRadius = 0.0001f;
        private const float MinOrbitPeriodSeconds = 0.01f;
        private const float DirectionEpsilon = 0.0001f;
        private const float FixedPlacementBackgroundDistance = 50000f;
        private const float DefaultFixedVerticalOffset = 0.0564f;

        [Header("── Placement ──────────────────")]
        [Tooltip("How the body resolves its apparent direction in the sky.")]
        [SerializeField] private CelestialPlacementMode placementMode = CelestialPlacementMode.FixedDirection;
        [Tooltip("Time owner for orbit and spin. AtmosphereCycle keeps celestial motion in the same cadence as the day-night system.")]
        [SerializeField] private CelestialTimeSourceMode timeSourceMode = CelestialTimeSourceMode.AtmosphereCycle;
        [Tooltip("Optional atmosphere owner override. Falls back to HectonAtmosphereManager.Instance when left empty.")]
        [SerializeField] private HectonAtmosphereManager atmosphereManager;
        [Tooltip("Observer transform used to capture the initial sky direction from the current scene layout.")]
        [SerializeField] private Transform observerTransform;
        [Tooltip("Parent celestial body used when this body orbits another body on the sky sphere.")]
        [SerializeField] private Transform parentBodyTransform;
        [Tooltip("If enabled, the current world-space direction is captured on enable and used as the fixed sky direction.")]
        [SerializeField] private bool captureInitialDirectionOnEnable = true;
        [Tooltip("Fallback fixed sky direction when no capture data exists.")]
        [SerializeField] private Vector3 fixedDirection = new Vector3(0.9f, 0.2f, 0.35f);
        [Tooltip("Constant vertical offset applied to fixed-direction bodies after observer-relative X/Z lock. Default value keeps roughly 40% of Aegir's disc below the horizon at sea level.")]
        [SerializeField, Range(-0.5f, 0.5f)] private float fixedVerticalOffset = DefaultFixedVerticalOffset;
        [Tooltip("Anchor distance from the sky rig origin. This is not physical distance. It only defines stable render placement.")]
        [SerializeField, Min(MinAnchorDistance)] private float anchorDistance = 20000f;
        [Tooltip("Target apparent angular diameter in degrees as seen by the observer.")]
        [SerializeField, Range(MinAngularDiameter, 40f)] private float angularDiameterDegrees = 12f;

        [Header("── Orbit Around Parent ──────────────────")]
        [Tooltip("Apparent orbit radius around the parent body in degrees.")]
        [SerializeField, Range(0f, 45f)] private float apparentOrbitRadiusDegrees = 6f;
        [Tooltip("Orbital period in seconds of shared game-cycle time. This is not a physical orbital distance.")]
        [SerializeField, Min(MinOrbitPeriodSeconds)] private float orbitalPeriodSeconds = 1200f;
        [Tooltip("Inclination flattens the orbit ellipse on the sky sphere.")]
        [SerializeField, Range(-89f, 89f)] private float orbitalInclinationDegrees = 12f;
        [Tooltip("Rotates the orbit plane around the parent body's view direction.")]
        [SerializeField, Range(0f, 360f)] private float orbitPlaneLongitudeDegrees = 0f;
        [Tooltip("Phase offset applied to the orbit angle.")]
        [SerializeField, Range(0f, 360f)] private float orbitPhaseOffsetDegrees = 0f;
        [Tooltip("Reference vector used to construct the orbit tangent basis around the parent body.")]
        [SerializeField] private Vector3 orbitPoleReference = Vector3.up;
        [Tooltip("Keeps orbiting bodies in an observer-relative equatorial panorama band instead of letting them climb into the zenith.")]
        [SerializeField] private bool keepOrbitInEquatorialBand = true;
        [Tooltip("Lifts or lowers the equatorial panorama band relative to the sea horizon.")]
        [SerializeField, Range(-0.25f, 0.25f)] private float equatorialBandVerticalBias = 0.06f;
        [Tooltip("Scales how much orbital inclination is allowed to pull bodies above or below the equatorial panorama band.")]
        [SerializeField, Range(0f, 1f)] private float equatorialBandInclinationScale = 0.35f;

        [Header("── Rotation ──────────────────")]
        [Tooltip("Static axial tilt applied before spin.")]
        [SerializeField] private Vector3 axialTiltEuler = Vector3.zero;
        [Tooltip("Axial spin period in seconds of shared game-cycle time. Set to zero to disable rotation.")]
        [SerializeField, Min(0f)] private float axialRotationPeriodSeconds = 0f;
        [Tooltip("Additional axial spin phase in degrees.")]
        [SerializeField, Range(0f, 360f)] private float axialRotationOffsetDegrees = 0f;

        [Header("── Renderer ──────────────────")]
        [Tooltip("Optional renderer reference used for mesh-radius auto-resolution.")]
        [SerializeField] private Renderer bodyRenderer;
        [Tooltip("Optional mesh filter reference used for mesh-radius auto-resolution.")]
        [SerializeField] private MeshFilter bodyMeshFilter;

        private float _meshRadius = 0.5f;
        private Vector3 _capturedDirection = Vector3.forward;
        private bool _hasCapturedDirection;
        private bool _registeredToTickManager;
        private ObserverRelativeCelestialBody _parentObserverRelativeBody;
        private bool _editorPreviewDirty = true;
        private Vector3 _editorLastObserverPosition;
        private Vector3 _editorLastParentPosition;
        private Quaternion _editorLastParentRotation = Quaternion.identity;
        private bool _editorPreviewCached;

        /// <summary>
        /// Current normalized sky direction solved by this body.
        /// </summary>
        public Vector3 CurrentDirection => ResolvePlacementDirection(ResolveTimeSeconds());

        /// <summary>
        /// Current apparent angular diameter in degrees.
        /// </summary>
        public float AngularDiameterDegrees => angularDiameterDegrees;

        private void Awake()
        {
            CacheAuthoringReferences();
        }

        private void OnEnable()
        {
#if UNITY_EDITOR
            if (EditorApplication.isCompiling || !Application.isPlaying)
                return;
#endif

            CacheAuthoringReferences();
            TryCaptureInitialDirection();
            ApplyPlacement();

            if (Application.isPlaying)
            {
                TryRegister();
            }
#if UNITY_EDITOR
            else
            {
                _editorPreviewDirty = true;
                _editorPreviewCached = false;
                EditorApplication.update -= HandleEditorUpdate;
                EditorApplication.update += HandleEditorUpdate;
            }
#endif
        }

        private void OnDisable()
        {
            if (Application.isPlaying)
            {
                TryUnregister();
            }
#if UNITY_EDITOR
            else
            {
                EditorApplication.update -= HandleEditorUpdate;
            }
#endif
        }

        /// <summary>
        /// Applies observer-relative placement on the project tick cadence.
        /// </summary>
        /// <param name="dt">Unused tick delta. The body resolves against the shared owner time.</param>
        public void Tick(float dt)
        {
            ApplyPlacement();
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying)
                return;

            if (placementMode != CelestialPlacementMode.FixedDirection)
                return;

            ApplyFixedObserverPlacement();
        }

        /// <summary>
        /// Overrides the captured fixed direction from external code.
        /// </summary>
        /// <param name="direction">Normalized sky direction.</param>
        public void SetFixedDirection(Vector3 direction)
        {
            if (direction.sqrMagnitude <= DirectionEpsilon)
                return;

            fixedDirection = direction.normalized;
            _capturedDirection = fixedDirection;
            _hasCapturedDirection = true;
            ApplyPlacement();
        }

        private void TryRegister()
        {
            if (_registeredToTickManager || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _registeredToTickManager = true;
        }

        private void TryUnregister()
        {
            if (!_registeredToTickManager)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);

            _registeredToTickManager = false;
        }

        private void ApplyPlacement()
        {
            if (placementMode == CelestialPlacementMode.FixedDirection)
            {
                ApplyFixedObserverPlacement();
                return;
            }

            float timeSeconds = ResolveTimeSeconds();
            Vector3 direction = ResolvePlacementDirection(timeSeconds);
            if (direction.sqrMagnitude <= DirectionEpsilon)
                return;

            float safeAnchorDistance = Mathf.Max(MinAnchorDistance, anchorDistance);
            float uniformScale = ResolveUniformScale(safeAnchorDistance);

            transform.localPosition = direction * safeAnchorDistance;
            transform.localScale = new Vector3(uniformScale, uniformScale, uniformScale);
            transform.localRotation = ResolveLocalRotation(timeSeconds);
        }

        private void ApplyFixedObserverPlacement()
        {
            Vector3 direction = ResolveFixedDirection();
            if (direction.sqrMagnitude <= DirectionEpsilon)
                return;

            Vector3 observerPosition = ResolveObserverWorldPosition();
            Vector3 horizontalDirection = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (horizontalDirection.sqrMagnitude <= DirectionEpsilon)
                horizontalDirection = Vector3.forward;

            horizontalDirection.Normalize();

            Vector3 screenLockedDirection = horizontalDirection + (Vector3.up * fixedVerticalOffset);
            if (screenLockedDirection.sqrMagnitude <= DirectionEpsilon)
                screenLockedDirection = horizontalDirection;

            screenLockedDirection.Normalize();

            float safeAnchorDistance = Mathf.Max(
                Mathf.Max(MinAnchorDistance, anchorDistance),
                FixedPlacementBackgroundDistance);
            float uniformScale = ResolveUniformScale(safeAnchorDistance);

            transform.position = observerPosition + (screenLockedDirection * safeAnchorDistance);
            transform.localScale = new Vector3(uniformScale, uniformScale, uniformScale);
            transform.localRotation = ResolveLocalRotation(ResolveTimeSeconds());
        }

        private Vector3 ResolvePlacementDirection(float timeSeconds)
        {
            switch (placementMode)
            {
                case CelestialPlacementMode.OrbitAroundParent:
                    return ResolveOrbitDirection(timeSeconds);

                default:
                    return ResolveFixedDirection();
            }
        }

        private Vector3 ResolveFixedDirection()
        {
            if (!captureInitialDirectionOnEnable)
            {
                if (fixedDirection.sqrMagnitude <= DirectionEpsilon)
                    return Vector3.forward;

                return fixedDirection.normalized;
            }

            if (_hasCapturedDirection)
                return _capturedDirection;

            if (fixedDirection.sqrMagnitude <= DirectionEpsilon)
                return Vector3.forward;

            return fixedDirection.normalized;
        }

        private Vector3 ResolveOrbitDirection(float timeSeconds)
        {
            Vector3 parentDirection = ResolveParentDirection();
            if (parentDirection.sqrMagnitude <= DirectionEpsilon)
                return ResolveFixedDirection();

            parentDirection.Normalize();

            if (keepOrbitInEquatorialBand &&
                TryResolveEquatorialParentDirection(parentDirection, out Vector3 equatorialParentDirection))
            {
                return ResolveEquatorialOrbitDirection(timeSeconds, equatorialParentDirection);
            }

            Vector3 basisReference = orbitPoleReference.sqrMagnitude > DirectionEpsilon
                ? orbitPoleReference.normalized
                : Vector3.up;

            Vector3 tangent = Vector3.Cross(basisReference, parentDirection);
            if (tangent.sqrMagnitude <= DirectionEpsilon)
            {
                basisReference = Mathf.Abs(Vector3.Dot(parentDirection, Vector3.up)) > 0.98f
                    ? Vector3.right
                    : Vector3.up;
                tangent = Vector3.Cross(basisReference, parentDirection);
                if (tangent.sqrMagnitude <= DirectionEpsilon)
                    return parentDirection;
            }

            tangent.Normalize();
            Vector3 bitangent = Vector3.Cross(parentDirection, tangent).normalized;

            if (orbitPlaneLongitudeDegrees != 0f)
            {
                Quaternion orbitPlaneRotation = Quaternion.AngleAxis(orbitPlaneLongitudeDegrees, parentDirection);
                tangent = orbitPlaneRotation * tangent;
                bitangent = orbitPlaneRotation * bitangent;
            }

            float orbitAngleDegrees = orbitPhaseOffsetDegrees;
            if (orbitalPeriodSeconds > MinOrbitPeriodSeconds)
                orbitAngleDegrees += (timeSeconds / orbitalPeriodSeconds) * 360f;

            float orbitAngleRad = orbitAngleDegrees * Mathf.Deg2Rad;
            float inclinationCos = Mathf.Cos(orbitalInclinationDegrees * Mathf.Deg2Rad);
            float orbitRadiusTan = Mathf.Tan(apparentOrbitRadiusDegrees * Mathf.Deg2Rad);

            Vector3 offset =
                tangent * (Mathf.Cos(orbitAngleRad) * orbitRadiusTan) +
                bitangent * (Mathf.Sin(orbitAngleRad) * orbitRadiusTan * inclinationCos);

            Vector3 orbitDirection = parentDirection + offset;
            if (orbitDirection.sqrMagnitude <= DirectionEpsilon)
                return parentDirection;

            return orbitDirection.normalized;
        }

        private Vector3 ResolveEquatorialOrbitDirection(float timeSeconds, Vector3 equatorialParentDirection)
        {
            Vector3 horizonParentDirection = equatorialParentDirection;
            if (orbitPlaneLongitudeDegrees != 0f)
            {
                Quaternion longitudeRotation = Quaternion.AngleAxis(orbitPlaneLongitudeDegrees, Vector3.up);
                horizonParentDirection = longitudeRotation * horizonParentDirection;
            }

            Vector3 horizonTangent = Vector3.Cross(Vector3.up, horizonParentDirection);
            if (horizonTangent.sqrMagnitude <= DirectionEpsilon)
                return horizonParentDirection;

            horizonTangent.Normalize();

            float orbitAngleDegrees = orbitPhaseOffsetDegrees;
            if (orbitalPeriodSeconds > MinOrbitPeriodSeconds)
                orbitAngleDegrees += (timeSeconds / orbitalPeriodSeconds) * 360f;

            float orbitAngleRad = orbitAngleDegrees * Mathf.Deg2Rad;
            float orbitRadiusTan = Mathf.Tan(apparentOrbitRadiusDegrees * Mathf.Deg2Rad);
            float inclinationAmplitude = Mathf.Sin(orbitalInclinationDegrees * Mathf.Deg2Rad) * equatorialBandInclinationScale;

            Vector3 orbitDirection =
                horizonParentDirection +
                horizonTangent * (Mathf.Cos(orbitAngleRad) * orbitRadiusTan) +
                Vector3.up * ((Mathf.Sin(orbitAngleRad) * orbitRadiusTan * inclinationAmplitude) + (equatorialBandVerticalBias * orbitRadiusTan));

            if (orbitDirection.sqrMagnitude <= DirectionEpsilon)
                return horizonParentDirection;

            return orbitDirection.normalized;
        }

        private static bool TryResolveEquatorialParentDirection(Vector3 parentDirection, out Vector3 equatorialParentDirection)
        {
            equatorialParentDirection = Vector3.ProjectOnPlane(parentDirection, Vector3.up);
            if (equatorialParentDirection.sqrMagnitude <= DirectionEpsilon)
            {
                equatorialParentDirection = Vector3.zero;
                return false;
            }

            equatorialParentDirection.Normalize();
            return true;
        }

        private Vector3 ResolveParentDirection()
        {
            if (parentBodyTransform == null)
                return Vector3.zero;

            if (_parentObserverRelativeBody == null)
                parentBodyTransform.TryGetComponent(out _parentObserverRelativeBody);

            if (_parentObserverRelativeBody != null && _parentObserverRelativeBody != this)
            {
                Vector3 parentSkyDirection = _parentObserverRelativeBody.CurrentDirection;
                if (parentSkyDirection.sqrMagnitude > DirectionEpsilon)
                    return parentSkyDirection.normalized;
            }

            if (transform.parent != null && parentBodyTransform.parent == transform.parent)
            {
                Vector3 localDirection = parentBodyTransform.localPosition;
                if (localDirection.sqrMagnitude > DirectionEpsilon)
                    return localDirection.normalized;
            }

            Vector3 observerPosition = ResolveObserverWorldPosition();
            if (observerPosition.sqrMagnitude > DirectionEpsilon || observerTransform != null)
            {
                Vector3 worldDirection = parentBodyTransform.position - observerPosition;
                if (worldDirection.sqrMagnitude > DirectionEpsilon)
                    return worldDirection.normalized;
            }

            Vector3 fallbackDirection = parentBodyTransform.position - transform.position;
            if (fallbackDirection.sqrMagnitude > DirectionEpsilon)
                return fallbackDirection.normalized;

            return Vector3.zero;
        }

        private float ResolveUniformScale(float safeAnchorDistance)
        {
            float safeAngularDiameter = Mathf.Clamp(
                angularDiameterDegrees,
                MinAngularDiameter,
                MaxAngularDiameter);

            float radiusWorld = Mathf.Tan(safeAngularDiameter * Mathf.Deg2Rad * 0.5f) * safeAnchorDistance;
            return radiusWorld / Mathf.Max(_meshRadius, MinMeshRadius);
        }

        private Quaternion ResolveLocalRotation(float timeSeconds)
        {
            Quaternion tiltRotation = Quaternion.Euler(axialTiltEuler);
            if (axialRotationPeriodSeconds <= MinOrbitPeriodSeconds)
                return tiltRotation;

            float spinAngleDegrees =
                axialRotationOffsetDegrees +
                (timeSeconds / axialRotationPeriodSeconds) * 360f;

            Quaternion spinRotation = Quaternion.AngleAxis(spinAngleDegrees, Vector3.up);
            return tiltRotation * spinRotation;
        }

        private void TryCaptureInitialDirection()
        {
            if (!captureInitialDirectionOnEnable || _hasCapturedDirection)
                return;

            if (transform.parent != null)
            {
                Vector3 localDirection = transform.localPosition;
                if (localDirection.sqrMagnitude > DirectionEpsilon)
                {
                    _capturedDirection = localDirection.normalized;
                    fixedDirection = _capturedDirection;
                    _hasCapturedDirection = true;
                    return;
                }
            }

            Vector3 observerPosition = ResolveObserverWorldPosition();
            Vector3 worldDirection = transform.position - observerPosition;
            if (worldDirection.sqrMagnitude <= DirectionEpsilon)
                return;

            _capturedDirection = worldDirection.normalized;
            fixedDirection = _capturedDirection;
            _hasCapturedDirection = true;
        }

        private void CacheAuthoringReferences()
        {
            ResolveAtmosphereManager();
            CacheMeshRadius();

            if (parentBodyTransform != null && _parentObserverRelativeBody == null)
                parentBodyTransform.TryGetComponent(out _parentObserverRelativeBody);
        }

        private void ResolveAtmosphereManager()
        {
            if (atmosphereManager == null)
                atmosphereManager = HectonAtmosphereManager.Instance;
        }

        private Vector3 ResolveObserverWorldPosition()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                SceneView sceneView = SceneView.lastActiveSceneView;
                if (sceneView != null && sceneView.camera != null)
                    return sceneView.camera.transform.position;
            }
#endif

            if (observerTransform == null)
            {
                if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) && playerTransform != null)
                {
                    Camera playerCamera = ((Hecton8.Core.GlobalRegistry.Player != null && Hecton8.Core.GlobalRegistry.Player.PlayerCamera != null) ? Hecton8.Core.GlobalRegistry.Player.PlayerCamera : playerTransform.GetComponent<Camera>());
                    if (playerCamera != null)
                        observerTransform = playerCamera.transform;
                }
            }

            return observerTransform != null
                ? observerTransform.position
                : Vector3.zero;
        }

        private void CacheMeshRadius()
        {
            if (bodyRenderer == null)
                bodyRenderer = GetComponent<Renderer>();

            if (bodyMeshFilter == null)
                bodyMeshFilter = GetComponent<MeshFilter>();

            Mesh sharedMesh = bodyMeshFilter != null ? bodyMeshFilter.sharedMesh : null;
            if (sharedMesh == null)
            {
                _meshRadius = 0.5f;
                return;
            }

            Vector3 extents = sharedMesh.bounds.extents;
            _meshRadius = Mathf.Max(
                Mathf.Max(Mathf.Abs(extents.x), Mathf.Abs(extents.y)),
                Mathf.Abs(extents.z));

            if (_meshRadius < MinMeshRadius)
                _meshRadius = 0.5f;
        }

        private float ResolveTimeSeconds()
        {
            ResolveAtmosphereManager();

            if (timeSourceMode == CelestialTimeSourceMode.AtmosphereCycle && atmosphereManager != null)
                return (float)atmosphereManager.ElapsedCycleTimeSeconds;

            if (Application.isPlaying)
                return Time.time;

#if UNITY_EDITOR
            return (float)EditorApplication.timeSinceStartup;
#else
            return 0f;
#endif
        }

#if UNITY_EDITOR
        private void HandleEditorUpdate()
        {
            if (EditorApplication.isCompiling || !Application.isPlaying)
            {
                EditorApplication.update -= HandleEditorUpdate;
                return;
            }

            if (Application.isPlaying || this == null)
                return;

            if (!UnityEditorInternal.InternalEditorUtility.isApplicationActive)
                return;

            if (!ShouldRefreshEditorPreview())
                return;

            ApplyPlacement();
            CacheEditorPreviewState();
            _editorPreviewDirty = false;
        }

        private void OnValidate()
        {
            if (EditorApplication.isCompiling || !Application.isPlaying)
                return;

            _registeredToTickManager = false;
            _parentObserverRelativeBody = null;
            _editorPreviewDirty = true;
            _editorPreviewCached = false;
            CacheAuthoringReferences();
            if (!_hasCapturedDirection && fixedDirection.sqrMagnitude > DirectionEpsilon)
                fixedDirection = fixedDirection.normalized;
            ApplyPlacement();
        }

        private bool ShouldRefreshEditorPreview()
        {
            if (_editorPreviewDirty || !_editorPreviewCached)
                return true;

            Vector3 observerPosition = ResolveObserverWorldPosition();
            if ((observerPosition - _editorLastObserverPosition).sqrMagnitude > DirectionEpsilon)
                return true;

            if (parentBodyTransform == null)
                return false;

            if ((parentBodyTransform.position - _editorLastParentPosition).sqrMagnitude > DirectionEpsilon)
                return true;

            return parentBodyTransform.rotation != _editorLastParentRotation;
        }

        private void CacheEditorPreviewState()
        {
            _editorLastObserverPosition = ResolveObserverWorldPosition();
            if (parentBodyTransform != null)
            {
                _editorLastParentPosition = parentBodyTransform.position;
                _editorLastParentRotation = parentBodyTransform.rotation;
            }
            else
            {
                _editorLastParentPosition = Vector3.zero;
                _editorLastParentRotation = Quaternion.identity;
            }

            _editorPreviewCached = true;
        }
#endif
    }
}
