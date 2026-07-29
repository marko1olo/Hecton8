using System.Collections.Generic;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.World;
using NASAPunk.Visor;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class HectonScanMarkerSystem : MonoBehaviour, ITickable, IUpdatable, ILateFrameTickable, IScanEventListener, IGlobalRegistryHotSwapListener
    {
        private const int MaxMarkers = 64;
        private const float FadeDurationSeconds = 1f;
        private const float ProjectionPaddingMeters = 0.05f;
        private const float DegreesToHalfRadians = 0.00872664626f;
        // COLD ALLOC: List<VisorHUDController>[2] — HUD camera resolve scratch — owner: HectonScanMarkerSystem
        private static readonly List<VisorHUDController> s_controllerResolveBuffer = new List<VisorHUDController>(2);
        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct ActiveMarker
        {
            [FieldOffset(0)]
            public AbsoluteUniversePosition aup;
            [FieldOffset(48)]
            public float timer;
            [FieldOffset(52)]
            private uint _pad0;
            [FieldOffset(56)]
            private ulong _pad1;
        }

        [Header("── HUD Camera ───────────────────────────────")]
        [SerializeField] private Camera hudCamera;

        [Header("── Appearance ───────────────────────────────")]
        [SerializeField] private Mesh markerMesh;
        [SerializeField] private Material markerMaterial;
        [SerializeField, Min(4f)] private float markerBaseSizePixels = 24f;
        [SerializeField, Min(2f)] private float markerMinSizePixels = 8f;
        [SerializeField, Min(4f)] private float markerMaxSizePixels = 40f;
        [SerializeField, Min(0.5f)] private float markerLifetime = 5f;
        [SerializeField, Min(0f)] private float edgeMarginPixels = 40f;

        private ActiveMarker[] _markers;
        private ulong _activeMarkerMask;
        private int _writeIndex;
        private Transform _playerTransform;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private Material _runtimeMarkerMaterial;
        private Mesh _runtimeMarkerMesh;
        // COLD ALLOC: Matrix4x4[64] — instanced marker draw mirror — owner: HectonScanMarkerSystem
        private readonly Matrix4x4[] _markerMatrixMirror = new Matrix4x4[MaxMarkers];
        private float _cachedProjectionDistance = -1f;
        private float _cachedFieldOfView = -1f;
        private float _cachedEdgeMarginPixels = -1f;
        private float _cachedWorldPerPixel;
        private float _cachedSafeHalfWidth = 0.5f;
        private float _cachedSafeHalfHeight = 0.5f;
        private int _cachedPixelWidth = -1;
        private int _cachedPixelHeight = -1;
        private bool _registered;
        private bool _lateFrameRegistered;
        private bool _registeredHotSwapListener;
        private bool _dispatcherAvailable;
        private bool _markerResourcesConfigured;
        private bool _invalidMarkerResourcesAnnounced;

        public void Initialize(Mesh meshOverride, Material materialOverride)
        {
            if (meshOverride != null)
                markerMesh = meshOverride;
            if (materialOverride != null)
                markerMaterial = materialOverride;

            _markerResourcesConfigured = true;
            EnsureRuntimeResources();
        }

        private void Awake()
        {
            // COLD ALLOC: ActiveMarker[64] — fixed scan marker slot buffer — owner: HectonScanMarkerSystem
            _markers = new ActiveMarker[MaxMarkers];
            CachePlayerContextCold();
            EnsureHudCamera();
            EnsurePlayerTransform();
            EnsureRuntimeResources();
        }

        private void OnEnable()
        {
            CachePlayerContextCold();
            EnsureHudCamera();
            EnsurePlayerTransform();
            EnsureRuntimeResources();
            TryRegisterHotSwapListener();
            ScanEvents.Register(this);
            RegisterTick();
            RegisterLateFrameTick();
        }

        private void OnDisable()
        {
            ScanEvents.Unregister(this);
            UnregisterTick();
            UnregisterLateFrameTick();
            TryUnregisterHotSwapListener();
            _cachedPlayerContext = null;
            _playerTransform = null;
        }

        private void OnDestroy()
        {
            UnregisterTick();
            UnregisterLateFrameTick();
            TryUnregisterHotSwapListener();

            _runtimeMarkerMaterial = null;
            _runtimeMarkerMesh = null;
            _cachedPlayerContext = null;
            _playerTransform = null;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Player)
            {
                if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
                {
                    UnregisterTick();
                    UnregisterLateFrameTick();
                    _dispatcherAvailable = currentService != null;
                    if (_dispatcherAvailable && isActiveAndEnabled)
                    {
                        RegisterTick();
                        RegisterLateFrameTick();
                    }
                }

                return;
            }

            IPlayerRuntimeContext previousContext = _cachedPlayerContext;
            Transform previousPlayerTransform = previousContext != null ? previousContext.PlayerTransform : null;
            _cachedPlayerContext = currentService as IPlayerRuntimeContext;
            if (previousPlayerTransform != null && ReferenceEquals(_playerTransform, previousPlayerTransform))
                _playerTransform = null;
            RefreshPresentationReferencesFromCachedContext();
        }

        public void Tick(float deltaTime)
        {
            if (_activeMarkerMask == 0UL)
                return;

            UpdateMarkerTimers(deltaTime);
            if (_activeMarkerMask == 0UL)
                return;

            RegisterLateFrameTick();
        }

        public void LateFrameTick()
        {
            if (_activeMarkerMask == 0UL)
                return;

            if (!RefreshPresentationReferencesFromCachedContext() ||
                !AreRuntimeResourcesReady())
            {
                return;
            }

            RenderMarkers();
        }

        public void OnScanEvent(in ScanEventPayload payload)
        {
            if ((ScanEventType)payload.EventType != ScanEventType.NodeFound)
                return;

            HandleNodeFound(payload.Position);
        }

        private void HandleNodeFound(float3 worldPos)
        {
            if (!TryResolveAupFromRuntimeOrigin(new Vector3(worldPos.x, worldPos.y, worldPos.z), out AbsoluteUniversePosition markerAup))
                return;

            ulong activeMask = _activeMarkerMask;
            while (activeMask != 0UL)
            {
                int i = (int)math.tzcnt(activeMask);
                activeMask &= activeMask - 1UL;

                if (AbsoluteUniversePosition.DistanceSq(in _markers[i].aup, in markerAup) < 1d)
                {
                    _markers[i].timer = markerLifetime;
                    return;
                }
            }

            int writeSlot = _writeIndex;
            _markers[writeSlot] = new ActiveMarker
            {
                aup = markerAup,
                timer = markerLifetime
            };

            _activeMarkerMask |= 1UL << writeSlot;
            _writeIndex = (_writeIndex + 1) & (MaxMarkers - 1);
        }

        private void UpdateMarkerTimers(float deltaTime)
        {
            ulong activeMask = _activeMarkerMask;
            while (activeMask != 0UL)
            {
                int i = (int)math.tzcnt(activeMask);
                ulong bit = 1UL << i;
                activeMask &= activeMask - 1UL;

                _markers[i].timer -= deltaTime;
                if (_markers[i].timer <= 0f)
                {
                    _activeMarkerMask &= ~bit;
                }
            }
        }

        private void RenderMarkers()
        {
            if (hudCamera == null || _playerTransform == null || _runtimeMarkerMaterial == null || _runtimeMarkerMesh == null)
                return;

            int visibleCount = BuildMarkerMatrices();
            if (visibleCount <= 0)
                return;

            UnityEngine.Graphics.DrawMeshInstanced(
                _runtimeMarkerMesh,
                0,
                _runtimeMarkerMaterial,
                _markerMatrixMirror,
                visibleCount,
                null,
                ShadowCastingMode.Off,
                false,
                0,
                hudCamera,
                LightProbeUsage.Off);
        }

        private int BuildMarkerMatrices()
        {
            ulong activeMask = _activeMarkerMask;
            if (activeMask == 0UL)
                return 0;

            Transform cameraTransform = hudCamera.transform;
            Vector3 playerPositionVector = _playerTransform.position;
            if (!TryResolvePlayerAup(playerPositionVector, out AbsoluteUniversePosition playerAup))
                return 0;

            UpdateProjectionCache();
            float projectionDistance = _cachedProjectionDistance;
            float worldPerPixel = _cachedWorldPerPixel;
            float safeHalfWidth = _cachedSafeHalfWidth;
            float safeHalfHeight = _cachedSafeHalfHeight;
            int visibleCount = 0;

            while (activeMask != 0UL)
            {
                int i = (int)math.tzcnt(activeMask);
                activeMask &= activeMask - 1UL;

                ActiveMarker marker = _markers[i];
                float3 markerRuntime = marker.aup.ToRuntimeFloat3();
                Vector3 markerRuntimePosition = new Vector3(markerRuntime.x, markerRuntime.y, markerRuntime.z);
                Vector3 viewport = hudCamera.WorldToViewportPoint(markerRuntimePosition);
                bool behindCamera = viewport.z <= 0.001f;
                float centeredX = viewport.x - 0.5f;
                float centeredY = viewport.y - 0.5f;
                if (behindCamera)
                {
                    centeredX = -centeredX;
                    centeredY = -centeredY;
                }

                if ((centeredX * centeredX) + (centeredY * centeredY) < 0.000001f)
                {
                    centeredX = 0f;
                    centeredY = 0.0001f;
                }

                bool clamped =
                    behindCamera ||
                    centeredX < -safeHalfWidth ||
                    centeredX > safeHalfWidth ||
                    centeredY < -safeHalfHeight ||
                    centeredY > safeHalfHeight;

                float finalViewportX = centeredX;
                float finalViewportY = centeredY;
                if (clamped)
                {
                    float tx = safeHalfWidth * math.rcp(math.max(math.abs(centeredX), 0.0001f));
                    float ty = safeHalfHeight * math.rcp(math.max(math.abs(centeredY), 0.0001f));
                    float clampScale = math.min(tx, ty);
                    finalViewportX *= clampScale;
                    finalViewportY *= clampScale;
                }

                float viewportX = finalViewportX + 0.5f;
                float viewportY = finalViewportY + 0.5f;
                Vector3 markerWorldPosition = hudCamera.ViewportToWorldPoint(new Vector3(viewportX, viewportY, projectionDistance));
                double distanceMeters = EstimateAupDistanceMeters(in marker.aup, in playerAup);
                double sizePixelsDouble = (double)markerBaseSizePixels * math.rcp(math.max(distanceMeters * 0.1d, 0.5d));
                float sizePixels = (float)math.clamp(sizePixelsDouble, markerMinSizePixels, markerMaxSizePixels);
                if (marker.timer < FadeDurationSeconds)
                {
                    sizePixels *= math.saturate(marker.timer * math.rcp(FadeDurationSeconds));
                }

                float markerScale = math.max(0.0001f, sizePixels * worldPerPixel);
                Matrix4x4 matrix = Matrix4x4.TRS(markerWorldPosition, cameraTransform.rotation, new Vector3(markerScale, markerScale, markerScale));
                _markerMatrixMirror[visibleCount] = matrix;
                visibleCount++;
            }

            return visibleCount;
        }

        private void UpdateProjectionCache()
        {
            int pixelHeight = math.max(1, hudCamera.pixelHeight);
            int pixelWidth = math.max(1, hudCamera.pixelWidth);
            float projectionDistance = hudCamera.nearClipPlane + ProjectionPaddingMeters;
            float fieldOfView = hudCamera.fieldOfView;
            float edgeMargin = edgeMarginPixels;

            if (_cachedPixelHeight == pixelHeight &&
                _cachedPixelWidth == pixelWidth &&
                _cachedProjectionDistance == projectionDistance &&
                _cachedFieldOfView == fieldOfView &&
                _cachedEdgeMarginPixels == edgeMargin)
            {
                return;
            }

            float frustumHeight = 2f * ApproximateTanPositive(fieldOfView * DegreesToHalfRadians) * projectionDistance;
            float invPixelWidth = math.rcp(pixelWidth);
            float invPixelHeight = math.rcp(pixelHeight);
            _cachedProjectionDistance = projectionDistance;
            _cachedFieldOfView = fieldOfView;
            _cachedEdgeMarginPixels = edgeMargin;
            _cachedPixelWidth = pixelWidth;
            _cachedPixelHeight = pixelHeight;
            _cachedWorldPerPixel = frustumHeight * invPixelHeight;
            _cachedSafeHalfWidth = math.max(0.001f, 0.5f - (edgeMargin * invPixelWidth));
            _cachedSafeHalfHeight = math.max(0.001f, 0.5f - (edgeMargin * invPixelHeight));
        }

        private void EnsureHudCamera()
        {
            if (hudCamera != null)
                return;

            VisorHUDController.CopyActiveControllersTo(s_controllerResolveBuffer);
            for (int i = 0; i < s_controllerResolveBuffer.Count; i++)
            {
                VisorHUDController controller = s_controllerResolveBuffer[i];
                if (controller != null && controller.HudCamera != null)
                {
                    hudCamera = controller.HudCamera;
                    break;
                }
            }

            s_controllerResolveBuffer.Clear();
        }

        private static float ApproximateTanPositive(float radians)
        {
            float x = math.clamp(radians, 0f, 1.4f);
            float x2 = x * x;
            float numerator = 15f - x2;
            float denominator = math.max(0.0001f, 15f - (6f * x2));
            return x * numerator * math.rcp(denominator);
        }

        private void EnsurePlayerTransform()
        {
            if (_playerTransform == null)
                GameBootstrapper.TryGetCurrentPlayerTransform(out _playerTransform);
        }

        private bool RefreshPresentationReferencesFromCachedContext()
        {
            IPlayerRuntimeContext context = _cachedPlayerContext;
            if (context != null && context.IsInitialized)
            {
                if (_playerTransform == null)
                    _playerTransform = context.PlayerTransform;

                if (hudCamera == null)
                {
                    VisorHUDController visor = context.VisorController;
                    if (visor != null)
                        hudCamera = visor.HudCamera;
                }
            }

            return hudCamera != null && _playerTransform != null;
        }

        /// <summary>
        /// Resolves the authored marker draw pair, or reports a half-authored one once without throwing.
        /// </summary>
        /// <remarks>
        /// The two <c>UnityEngine.Assertions.Assert.IsTrue</c> calls removed from the invalid-resources branch
        /// THREW - nothing under Assets sets <c>Assert.raiseExceptions = false</c>. The
        /// <c>shouldReportInvalidResources</c> gate limited WHEN they fired (a component with both slots empty and
        /// no <see cref="Initialize"/> call stayed silent) but not what they destroyed when they did fire:
        /// <see cref="OnEnable"/> reaches this method at :96, before <c>TryRegisterHotSwapListener()</c>,
        /// <c>ScanEvents.Register(this)</c>, <c>RegisterTick()</c> and <c>RegisterLateFrameTick()</c> (:97-100).
        /// A half-authored inspector pair - mesh assigned but material missing, or a material with GPU instancing
        /// off - therefore killed the whole scan marker system for the session: no scan event subscription, no
        /// update tick, no late-frame tick, and no way to recover once the asset was fixed.
        /// <see cref="Initialize"/> (:78) threw back into its caller for the same reason.
        ///
        /// The asserts guarded nothing: nulling the runtime pair on this branch is the designed idle state and
        /// <see cref="AreRuntimeResourcesReady"/> (:449) is the gate every draw path already consults.
        /// </remarks>
        private void EnsureRuntimeResources()
        {
            bool meshAssigned = markerMesh != null;
            bool materialAssigned = markerMaterial != null;
            bool authoredMeshValid = meshAssigned && markerMesh.subMeshCount > 0 && markerMesh.GetIndexCount(0) > 0u;
            bool authoredMaterialValid = materialAssigned &&
                                         markerMaterial.shader != null &&
                                         markerMaterial.enableInstancing;
            bool shouldReportInvalidResources = _markerResourcesConfigured || meshAssigned || materialAssigned;
            if (!authoredMeshValid || !authoredMaterialValid)
            {
                _runtimeMarkerMesh = null;
                _runtimeMarkerMaterial = null;

                // Report LAST and once. OnEnable continues to its four registration calls after this returns, so
                // a future re-introduced throw here can no longer unsubscribe the scan marker system.
                if (shouldReportInvalidResources && !_invalidMarkerResourcesAnnounced)
                {
                    _invalidMarkerResourcesAnnounced = true;
                    LogInvalidScanMarkerResources(
                        meshAssigned,
                        authoredMeshValid,
                        materialAssigned,
                        materialAssigned && markerMaterial.shader != null,
                        authoredMaterialValid);
                }

                return;
            }

            if (!ReferenceEquals(_runtimeMarkerMesh, markerMesh))
                _runtimeMarkerMesh = markerMesh;

            if (!ReferenceEquals(_runtimeMarkerMaterial, markerMaterial))
                _runtimeMarkerMaterial = markerMaterial;

        }

        /// <summary>
        /// One-shot report of a half-authored scan marker draw pair. The latch guarantees single emission and every
        /// parameter is a primitive, so no string work and no allocation reaches a tick cadence.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogInvalidScanMarkerResources(
            bool meshAssigned,
            bool authoredMeshValid,
            bool materialAssigned,
            bool materialHasShader,
            bool authoredMaterialValid)
        {
            if (!meshAssigned)
            {
                Hecton8.Core.H8Debug.LogError("HectonScanMarkerSystem: serialized field 'markerMesh' is unassigned while 'markerMaterial' or Initialize supplied the other half of the pair. No scan marker renders this session - AreRuntimeResourcesReady stays false. Scan events, the update tick and the late-frame tick all stay registered. Assign the authored marker quad mesh in the inspector or pass it to Initialize.");
            }
            else if (!authoredMeshValid)
            {
                Hecton8.Core.H8Debug.LogError("HectonScanMarkerSystem: the mesh assigned to 'markerMesh' has no indexed submesh 0 (subMeshCount is 0 or GetIndexCount(0) is 0), so the instanced marker draw would submit no triangles. No scan marker renders this session. Reimport or replace that mesh asset with one that carries an index buffer.");
            }

            if (!materialAssigned)
            {
                Hecton8.Core.H8Debug.LogError("HectonScanMarkerSystem: serialized field 'markerMaterial' is unassigned while 'markerMesh' or Initialize supplied the other half of the pair. No scan marker renders this session - AreRuntimeResourcesReady stays false. Every registration stays live. Runtime material generation is forbidden: assign the authored marker material in the inspector or pass it to Initialize.");
                return;
            }

            if (!materialHasShader)
            {
                Hecton8.Core.H8Debug.LogError("HectonScanMarkerSystem: the material assigned to 'markerMaterial' has a null shader - the shader asset it referenced is missing or failed to compile. No scan marker renders this session. Repair that material's shader reference.");
                return;
            }

            if (!authoredMaterialValid)
            {
                Hecton8.Core.H8Debug.LogError("HectonScanMarkerSystem: the material assigned to 'markerMaterial' has Enable GPU Instancing OFF, which the instanced marker draw requires. No scan marker renders this session. Tick 'Enable GPU Instancing' on that material asset.");
            }
        }

        private bool AreRuntimeResourcesReady()
        {
            return _runtimeMarkerMesh != null &&
                   _runtimeMarkerMaterial != null;
        }

        private void RegisterTick()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (!_dispatcherAvailable)
                return;

            _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.UI);
        }

        private void UnregisterTick()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registered = false;
        }

        private void RegisterLateFrameTick()
        {
            if (_lateFrameRegistered || !Application.isPlaying)
                return;

            if (!_dispatcherAvailable)
                return;

            _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void UnregisterLateFrameTick()
        {
            if (!_lateFrameRegistered)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
            _lateFrameRegistered = false;
        }

        private void CachePlayerContextCold()
        {
            _dispatcherAvailable = GlobalRegistry.Dispatcher != null;
            _cachedPlayerContext = Hecton8.Core.GlobalRegistry.Player;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }

        private bool TryResolvePlayerAup(Vector3 fallbackRuntimePosition, out AbsoluteUniversePosition playerAup)
        {
            playerAup = default;
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext != null &&
                playerContext.IsInitialized &&
                playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u)
            {
                playerAup = movementState.PredictedAup;
                if (playerAup.IsFinite())
                    return true;
            }

            return TryResolveAupFromRuntimeOrigin(fallbackRuntimePosition, out playerAup);
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition aup)
        {
            aup = default;
            if (!math.isfinite(runtimePosition.x) ||
                !math.isfinite(runtimePosition.y) ||
                !math.isfinite(runtimePosition.z))
            {
                return false;
            }

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            aup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return aup.IsFinite();
        }

        private static double EstimateAupDistanceMeters(in AbsoluteUniversePosition a, in AbsoluteUniversePosition b)
        {
            double3 delta = AbsoluteUniversePosition.DeltaMetersClamped(in a, in b);
            double ax = math.abs(delta.x);
            double ay = math.abs(delta.y);
            double az = math.abs(delta.z);
            double max = math.max(ax, math.max(ay, az));
            double min = math.min(ax, math.min(ay, az));
            double mid = ax + ay + az - max - min;
            return max + (mid * 0.375d) + (min * 0.25d);
        }
    }
}
