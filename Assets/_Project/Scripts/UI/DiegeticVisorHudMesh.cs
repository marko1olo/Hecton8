using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Atmosphere;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
namespace Hecton8.UI
{
    /// <summary>
    /// Physical visor HUD projection mesh. No screen-space canvas, no physics raycasts.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class DiegeticVisorHudMesh : MonoBehaviour, ISlowTickable, ILateFrameTickable, IPlayerSignalEventListener, IDamageReceiver, IGlobalRegistryHotSwapListener
    {
        private const int BlackBoxCapacity = 300;
        private const SystemID VaultOwnerSystemId = SystemID.UI;
        private const BufferID BlackBoxBufferId = BufferID.DiegeticVisorHudBlackBox;
        private const float DefaultDistanceMeters = 0.48f;
        private const float DefaultHorizontalDegrees = 78f;
        private const float DefaultVerticalDegrees = 48f;
        private const float DegreesToHalfRadians = 0.008726646f;
        private const float Epsilon = 0.0001f;

        private static readonly int PanelPowerLevelId = Shader.PropertyToID("_PanelPowerLevel");
        private static readonly int DamageGlitchId = Shader.PropertyToID("_DamageGlitch");
        private static readonly int Humidity01Id = Shader.PropertyToID("_Humidity01");
        private static readonly int StencilRefId = Shader.PropertyToID("_StencilRef");

        [Header("Projection")]
        [SerializeField] private Camera visorCamera;
        [SerializeField] private bool parentToCamera = true;
        [SerializeField, Min(0.05f)] private float distanceMeters = DefaultDistanceMeters;
        [SerializeField, Range(16f, 130f)] private float horizontalDegrees = DefaultHorizontalDegrees;
        [SerializeField, Range(12f, 90f)] private float verticalDegrees = DefaultVerticalDegrees;
        [SerializeField, Tooltip("Authored curved visor projection mesh. Runtime mesh synthesis is forbidden; leave null only when MeshFilter.sharedMesh is already authored.")]
        private Mesh authoredProjectionMesh;

        [Header("Render State")]
        [SerializeField] private Material sourceMaterial;
        [SerializeField] private bool releaseRuntimeObjectsOnDisable;
        [SerializeField] private bool releaseBlackBoxOnDisable;
        [SerializeField] private int stencilReference = 17;
        [SerializeField, Range(0f, 1f)] private float panelPower01 = 1f;
        [SerializeField, Range(0.05f, 4f)] private float glitchRecoveryPerSecond = 1.8f;

        [Header("Signals")]
        [SerializeField] private BaseAtmosphereEngine atmosphereEngine;
        [SerializeField, Min(0.05f)] private float humiditySampleIntervalSeconds = 0.5f;

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _runtimeMesh;
        private Material _runtimeMaterial;
        private MaterialPropertyBlock _materialProperties;
        private Transform _cameraTransform;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private VaultGenerationHandle<DiegeticHudTelemetryEntry> _blackBoxHandle;
        private IDataVault _dataVault;
        private int _blackBoxCursor;
        private bool _registeredLateFrame;
        private bool _registeredSlowTick;
        private bool _hotSwapListenerRegistered;
        private bool _playerSignalRegistered;
        private bool _blackBoxDumpQueued;
        private bool _blackBoxDumped;
        private bool _materialStateDirty;
        private bool _meshRebuildDirty;
        private float _brownout01;
        private float _damageGlitch01;
        private float _humidity01;
        private float _humiditySampleTimer;
        private float _lastPanelPower = -1f;
        private float _lastDamageGlitch = -1f;
        private float _lastHumidity = -1f;
        private int _lastStencilReference = int.MinValue;
        private float _cachedQualityWeight01 = 1f;
        private float _meshDistanceMeters = -1f;
        private float _meshHorizontalDegrees = -1f;
        private float _meshVerticalDegrees = -1f;
        public float PanelPower01 => panelPower01;
        public float Brownout01 => _brownout01;
        public float DamageGlitch01 => _damageGlitch01;
        public float Humidity01 => _humidity01;

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            ResolveComponents();
            ResolveCamera();
            RebuildMesh();
            EnsureRuntimeMaterial();
            EnsureBlackBox();
            TryRegisterTick();
            PlayerSignalEvents.Register(this);
            _playerSignalRegistered = true;
        }

        private void OnDisable()
        {
            FlushQueuedBlackBoxDump();
            TryUnregisterHotSwapListener();
            TryUnregisterTick();
            if (_playerSignalRegistered)
            {
                PlayerSignalEvents.Unregister(this);
                _playerSignalRegistered = false;
            }

            if (releaseBlackBoxOnDisable)
                DisposeBlackBox();
            if (releaseRuntimeObjectsOnDisable)
                ReleaseRuntimeObjects();
        }

        private void OnDestroy()
        {
            FlushQueuedBlackBoxDump();
            TryUnregisterHotSwapListener();
            TryUnregisterTick();
            DisposeBlackBox();
            ReleaseRuntimeObjects();
        }

        private void AdvanceVisualHudState(float deltaTime)
        {
            float dt = math.max(0f, deltaTime);
            if (_damageGlitch01 > 0f)
                _damageGlitch01 = math.max(0f, _damageGlitch01 - dt * glitchRecoveryPerSecond);
            if (_brownout01 > 0f)
                _brownout01 = math.max(0f, _brownout01 - dt);

            if (RefreshQualityPolicy())
                _meshRebuildDirty = true;

            SampleHumidity(dt);
            _materialStateDirty = true;
            RecordTelemetry();
        }

        public void LateFrameTick()
        {
            AdvanceVisualHudState(SystemDispatcher.CurrentFrameDeltaTime);

            if (_meshRebuildDirty)
            {
                _meshRebuildDirty = false;
                RebuildMesh();
                _materialStateDirty = true;
            }

            if (!_materialStateDirty)
                return;

            _materialStateDirty = false;
            ApplyMaterialState();
        }

        public void SlowTick()
        {
            FlushQueuedBlackBoxDump();
        }

        public void OnTraumaHudSignal(in TraumaHudSignal signal)
        {
            panelPower01 = math.saturate(signal.TransportPower01);
            float hullDamage = signal.HullIntegrity01 < 0.3f ? 1f - math.saturate(signal.HullIntegrity01 * 3.3333333f) : 0f;
            _damageGlitch01 = math.saturate(math.max(_damageGlitch01, math.max(signal.GlitchIntensity, hullDamage)));
        }

        public void OnInteractionSignal(in PlayerInteractionStressSignal signal)
        {
        }

        public void OnToolDepletedSignal(in PlayerToolDepletedSignal signal)
        {
        }

        public void ReceiveDamage(in DamagePacket packet)
        {
            if (packet.Channel != DamageChannel.Integrity &&
                packet.Channel != DamageChannel.Clarity &&
                packet.Channel != DamageChannel.Trauma)
            {
                return;
            }

            float health01 = packet.NextValue > 0f ? math.saturate(packet.NextValue) : 1f - math.saturate(packet.Magnitude);
            if (health01 >= 0.3f)
                return;

            float missingLowHealth = 1f - math.saturate(health01 * 3.3333333f);
            float impact = math.saturate(packet.Magnitude);
            _damageGlitch01 = math.saturate(math.max(_damageGlitch01, math.max(missingLowHealth, impact)));
        }

        public void ApplyBrownoutSignal(in BrownoutSignal signal)
        {
            panelPower01 = math.saturate(signal.SupplyRatio);
            _brownout01 = math.saturate(math.max(_brownout01, signal.Severity01));
        }

        public void ApplyDamageSignal(in Hecton8.Core.Contracts.Signals.CombatDamageSignal signal, float health01)
        {
            if (health01 >= 0.3f)
                return;

            float missingLowHealth = 1f - math.saturate(health01 * 3.3333333f);
            float impact = math.saturate(signal.Magnitude * 0.05f);
            _damageGlitch01 = math.saturate(math.max(_damageGlitch01, math.max(missingLowHealth, impact)));
        }

        public void ApplyAtmosphereHumidity(byte humidityPercent)
        {
            _humidity01 = math.saturate(humidityPercent * 0.01f);
        }

        public bool TryProjectViewportPoint(Vector2 viewportPoint, out Vector2 visorUv, out Vector3 localHit)
        {
            visorUv = default;
            localHit = default;
            if (visorCamera == null)
                return false;

            Ray ray = visorCamera.ViewportPointToRay(new Vector3(viewportPoint.x, viewportPoint.y, 0f));
            return TryProjectRayToVisor(ray, out visorUv, out localHit);
        }

        public bool TryProjectRayToVisor(Ray worldRay, out Vector2 visorUv, out Vector3 localHit)
        {
            visorUv = default;
            localHit = default;

            Transform self = transform;
            Vector3 localOrigin = self.InverseTransformPoint(worldRay.origin);
            Vector3 localDirection = self.InverseTransformDirection(worldRay.direction);
            if (math.abs(localDirection.z) < Epsilon)
                return false;

            float t = (distanceMeters - localOrigin.z) * math.rcp(localDirection.z);
            if (t < 0f)
                return false;

            localHit = localOrigin + localDirection * t;
            float halfWidth = ResolveHalfWidth();
            float halfHeight = ResolveHalfHeight();
            if (halfWidth <= Epsilon || halfHeight <= Epsilon)
                return false;

            float u = (localHit.x * math.rcp(halfWidth * 2f)) + 0.5f;
            float v = (localHit.y * math.rcp(halfHeight * 2f)) + 0.5f;
            if (u < 0f || u > 1f || v < 0f || v > 1f)
                return false;

            visorUv = new Vector2(u, v);
            return true;
        }

        public static float RationalTan(float radians)
        {
            float x = math.clamp(radians, -1.2f, 1.2f);
            float x2 = x * x;
            float denominator = 27f - (9f * x2);
            if (math.abs(denominator) < Epsilon)
                denominator = denominator < 0f ? -Epsilon : Epsilon;

            return x * (27f - x2) * math.rcp(denominator);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                IDataVault previousVault = previousService is IDataVault oldVault ? oldVault : null;
                IDataVault nextVault = currentService is IDataVault vault ? vault : null;
                RebindDataVaultForLifecycle(nextVault, previousVault);
                if (_dataVault != null)
                    EnsureBlackBox();
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.Player)
                return;

            _cachedPlayerContext = currentService as IPlayerRuntimeContext;
            if (visorCamera == null)
                ResolveCamera();
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        private void CacheRegistryServicesCold()
        {
            _cachedPlayerContext = GlobalRegistry.Player;
            _cachedQualityWeight01 = ResolveCurrentQualityWeight(_cachedQualityWeight01);
        }

        private bool RefreshQualityPolicy()
        {
            float nextQualityWeight01 = ResolveCurrentQualityWeight(_cachedQualityWeight01);
            bool changed = math.abs(nextQualityWeight01 - _cachedQualityWeight01) > Epsilon;
            _cachedQualityWeight01 = nextQualityWeight01;
            return changed && _runtimeMesh == null;
        }

        private static float ResolveCurrentQualityWeight(float fallbackWeight01)
        {
            float qualityWeight01 = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(fallbackWeight01, qualityWeight01, math.isfinite(qualityWeight01)));
        }

        private void ResolveComponents()
        {
            if (_meshFilter == null)
                TryGetComponent(out _meshFilter);
            if (_meshRenderer == null)
                TryGetComponent(out _meshRenderer);
        }

        private void ResolveCamera()
        {
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (visorCamera == null && playerContext != null)
                visorCamera = playerContext.PlayerCamera;
            if (visorCamera == null)
                visorCamera = ResolveNearestParentCamera(transform);
            if (visorCamera == null)
                return;

            _cameraTransform = visorCamera.transform;
            if (!parentToCamera)
                return;

            transform.SetParent(_cameraTransform, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        private static Camera ResolveNearestParentCamera(Transform start)
        {
            for (Transform current = start; current != null; current = current.parent)
            {
                if (current.TryGetComponent(out Camera camera))
                    return camera;
            }

            return null;
        }

        private void RebuildMesh()
        {
            ResolveComponents();
            Mesh authoredMesh = authoredProjectionMesh != null
                ? authoredProjectionMesh
                : _meshFilter != null
                    ? _meshFilter.sharedMesh
                    : null;
            bool meshValid = authoredMesh != null &&
                             authoredMesh.subMeshCount > 0 &&
                             authoredMesh.GetIndexCount(0) > 0u;
            if (!meshValid)
            {
                if (_meshFilter != null && ReferenceEquals(_meshFilter.sharedMesh, _runtimeMesh))
                    _meshFilter.sharedMesh = null;
                _runtimeMesh = null;
                return;
            }

            _runtimeMesh = authoredMesh;
            _meshFilter.sharedMesh = _runtimeMesh;
            _meshDistanceMeters = distanceMeters;
            _meshHorizontalDegrees = horizontalDegrees;
            _meshVerticalDegrees = verticalDegrees;
        }

        private void EnsureRuntimeMaterial()
        {
            ResolveComponents();
            if (_runtimeMaterial != null)
            {
                _meshRenderer.sharedMaterial = _runtimeMaterial;
                return;
            }

            if (sourceMaterial == null || sourceMaterial.shader == null)
            {
                _runtimeMaterial = null;
                if (_meshRenderer != null)
                    _meshRenderer.sharedMaterial = null;
                return;
            }

            _runtimeMaterial = sourceMaterial;

            if (_runtimeMaterial == null)
                return;

            _meshRenderer.sharedMaterial = _runtimeMaterial;
            _lastPanelPower = -1f;
            _lastDamageGlitch = -1f;
            _lastHumidity = -1f;
            _lastStencilReference = int.MinValue;
            ApplyMaterialState();
        }

        private void ApplyMaterialState()
        {
            if (_runtimeMaterial == null || _meshRenderer == null)
                return;

            EnsureMaterialPropertiesCold();
            float resolvedPanelPower = math.saturate(panelPower01) * (1f - (_brownout01 * 0.65f));
            if (!math.isfinite(resolvedPanelPower) ||
                !math.isfinite(_damageGlitch01) ||
                !math.isfinite(_humidity01))
            {
                QueueBlackBoxDump();
                return;
            }

            bool changed = false;
            if (math.abs(resolvedPanelPower - _lastPanelPower) > 0.001f)
            {
                _materialProperties.SetFloat(PanelPowerLevelId, resolvedPanelPower);
                _lastPanelPower = resolvedPanelPower;
                changed = true;
            }

            if (math.abs(_damageGlitch01 - _lastDamageGlitch) > 0.001f)
            {
                _materialProperties.SetFloat(DamageGlitchId, _damageGlitch01);
                _lastDamageGlitch = _damageGlitch01;
                changed = true;
            }

            if (math.abs(_humidity01 - _lastHumidity) > 0.001f)
            {
                _materialProperties.SetFloat(Humidity01Id, _humidity01);
                _lastHumidity = _humidity01;
                changed = true;
            }

            if (stencilReference != _lastStencilReference)
            {
                _materialProperties.SetInt(StencilRefId, stencilReference);
                _lastStencilReference = stencilReference;
                changed = true;
            }

            if (changed)
                _meshRenderer.SetPropertyBlock(_materialProperties);
        }

        private void EnsureMaterialPropertiesCold()
        {
            if (_materialProperties != null)
                return;

            // COLD ALLOC: MaterialPropertyBlock[1] - visor per-renderer shader state - owner: DiegeticVisorHudMesh.
            _materialProperties = new MaterialPropertyBlock();
            _lastPanelPower = -1f;
            _lastDamageGlitch = -1f;
            _lastHumidity = -1f;
            _lastStencilReference = int.MinValue;
        }

        private void SampleHumidity(float deltaTime)
        {
            if (atmosphereEngine == null)
                return;

            _humiditySampleTimer += deltaTime;
            if (_humiditySampleTimer < humiditySampleIntervalSeconds)
                return;

            _humiditySampleTimer = 0f;
            if (atmosphereEngine.TryGetCompartmentState(atmosphereEngine.ActiveCompartmentIndex, out CompartmentState state))
                _humidity01 = math.saturate(state.HumidityPercent * 0.01f);
        }

        private void TryRegisterTick()
        {
            if (!_registeredLateFrame)
                _registeredLateFrame = SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI);

            if (!_registeredSlowTick)
                _registeredSlowTick = SystemDispatcher.Register((ISlowTickable)this, PriorityLayer.UI);
        }

        private void TryUnregisterTick()
        {
            if (_registeredLateFrame)
            {
                SystemDispatcher.UnregisterLateFrameTickableDirect(this, PriorityLayer.UI);
                _registeredLateFrame = false;
            }

            if (_registeredSlowTick)
            {
                SystemDispatcher.Unregister((ISlowTickable)this, PriorityLayer.UI);
                _registeredSlowTick = false;
            }

            _materialStateDirty = false;
            _meshRebuildDirty = false;
        }

        private void EnsureBlackBox()
        {
            IDataVault vault = CacheDataVaultCold();
            if (vault == null)
                return;

            if (!vault.IsCompactionFenceActive &&
                IsBlackBoxHandle(in _blackBoxHandle) &&
                vault.TryReadOnlyHandle(in _blackBoxHandle, out NativeArray<DiegeticHudTelemetryEntry>.ReadOnly blackBox) &&
                !vault.IsCompactionFenceActive &&
                blackBox.IsCreated &&
                blackBox.Length >= BlackBoxCapacity)
                return;

            if (vault.IsCompactionFenceActive)
                return;

            ReleaseBlackBoxHandle(vault);

            _blackBoxHandle = vault.EnsureGenerationHandle<DiegeticHudTelemetryEntry>(
                BlackBoxBufferId,
                BlackBoxCapacity,
                VaultOwnerSystemId,
                NativeArrayOptions.ClearMemory);
            if (!IsBlackBoxHandle(in _blackBoxHandle) ||
                vault.IsCompactionFenceActive ||
                !vault.TryReadOnlyHandle(in _blackBoxHandle, out blackBox) ||
                vault.IsCompactionFenceActive ||
                !blackBox.IsCreated ||
                blackBox.Length < BlackBoxCapacity)
            {
                ResetBlackBoxNativeEpochState();
                return;
            }

            _blackBoxCursor = 0;
            _blackBoxDumped = false;
        }

        private void DisposeBlackBox()
        {
            ReleaseBlackBoxHandle(_dataVault);
            ResetBlackBoxNativeEpochState();
        }

        private void ReleaseBlackBoxHandle(IDataVault vault)
        {
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !IsBlackBoxHandle(in _blackBoxHandle) ||
                !vault.TryGetGenerationHandle(BlackBoxBufferId, out VaultGenerationHandle<DiegeticHudTelemetryEntry> currentHandle) ||
                !IsBlackBoxHandle(in currentHandle) ||
                currentHandle.Generation != _blackBoxHandle.Generation)
            {
                return;
            }

            vault.ReleaseBuffer(in _blackBoxHandle);
        }

        private void ResetBlackBoxNativeEpochState()
        {
            _blackBoxHandle = default;
            _blackBoxCursor = 0;
            _blackBoxDumped = false;
        }

        private void RebindDataVaultForLifecycle(IDataVault nextVault, IDataVault fallbackReleaseVault = null)
        {
            if (ReferenceEquals(_dataVault, nextVault))
                return;

            ReleaseBlackBoxHandle(_dataVault ?? fallbackReleaseVault);
            _dataVault = nextVault;
            ResetBlackBoxNativeEpochState();
        }

        private IDataVault CacheDataVaultCold()
        {
            IDataVault registryVault = GlobalRegistry.DataVault;
            if (!ReferenceEquals(_dataVault, registryVault))
                RebindDataVaultForLifecycle(registryVault);

            return _dataVault;
        }

        private static bool IsBlackBoxHandle(in VaultGenerationHandle<DiegeticHudTelemetryEntry> handle)
        {
            return handle.BufferID == (uint)BlackBoxBufferId &&
                   handle.SystemID == (uint)VaultOwnerSystemId &&
                   handle.Generation != 0u;
        }

        private void RecordTelemetry()
        {
            IDataVault vault = _dataVault;
            if (vault == null || !IsBlackBoxHandle(in _blackBoxHandle))
                return;

            Vector3 localPosition = transform.localPosition;
            if (!math.isfinite(localPosition.x) || !math.isfinite(localPosition.y) || !math.isfinite(localPosition.z))
            {
                QueueBlackBoxDump();
                return;
            }

            if (vault.IsCompactionFenceActive ||
                !vault.TryAcquireWriteLock(in _blackBoxHandle, VaultOwnerSystemId, out NativeArray<DiegeticHudTelemetryEntry> blackBox))
            {
                return;
            }

            try
            {
                if (vault.IsCompactionFenceActive ||
                    !blackBox.IsCreated ||
                    blackBox.Length < BlackBoxCapacity)
                {
                    return;
                }

                blackBox[_blackBoxCursor] = new DiegeticHudTelemetryEntry
                {
                    Frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex,
                    Power01 = math.saturate(panelPower01),
                    Brownout01 = math.saturate(_brownout01),
                    DamageGlitch01 = math.saturate(_damageGlitch01),
                    Humidity01 = math.saturate(_humidity01),
                    LocalX = localPosition.x,
                    LocalY = localPosition.y,
                    LocalZ = localPosition.z,
                    Flags = (uint)((_registeredLateFrame ? 1 : 0) | (_playerSignalRegistered ? 2 : 0) | (_runtimeMaterial != null ? 4 : 0))
                };
                _blackBoxCursor++;
                if (_blackBoxCursor >= BlackBoxCapacity)
                    _blackBoxCursor = 0;
            }
            finally
            {
                vault.ReleaseWriteLock(in _blackBoxHandle, VaultOwnerSystemId);
            }
        }

        private void QueueBlackBoxDump()
        {
            if (!_blackBoxDumped)
                _blackBoxDumpQueued = true;
        }

        private void FlushQueuedBlackBoxDump()
        {
            if (!_blackBoxDumpQueued)
                return;

            _blackBoxDumpQueued = false;
            DumpBlackBox();
        }

        private unsafe void DumpBlackBox()
        {
            IDataVault vault = _dataVault;
            if (_blackBoxDumped ||
                vault == null ||
                !IsBlackBoxHandle(in _blackBoxHandle))
            {
                _blackBoxDumpQueued = !_blackBoxDumped;
                return;
            }

            NativeArray<byte> payload = default;
            try
            {
                string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string path = Path.Combine(root, "Docs", "AgentLogs", "Dump_UI_DIEGETIC_HUD.bin");
                const int headerBytes = 8;
                const int rowBytes = 40;
                int byteCount = headerBytes + (BlackBoxCapacity * rowBytes);
                payload = H8Memory.Allocate<byte>(
                    byteCount,
                    VaultOwnerSystemId,
                    Allocator.Temp,
                    NativeArrayOptions.UninitializedMemory);
                if (!payload.IsCreated)
                {
                    _blackBoxDumpQueued = true;
                    return;
                }

                byte* payloadPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                Span<byte> header = new Span<byte>(payloadPtr, headerBytes);
                BinaryPrimitives.WriteInt32LittleEndian(header.Slice(0, 4), BlackBoxCapacity);
                BinaryPrimitives.WriteInt32LittleEndian(header.Slice(4, 4), _blackBoxCursor);

                for (int i = 0; i < BlackBoxCapacity; i++)
                {
                    if (!TryReadBlackBoxEntry(vault, i, out DiegeticHudTelemetryEntry entry))
                    {
                        _blackBoxDumpQueued = true;
                        return;
                    }

                    Span<byte> row = new Span<byte>(payloadPtr + headerBytes + (i * rowBytes), rowBytes);
                    WriteDiegeticHudTelemetryEntry(row, in entry);
                }

                _blackBoxDumped = NativeFaultDumpWriter.TryWriteAll(path, payload, byteCount);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
            catch (ArgumentException)
            {
            }
            catch (NotSupportedException)
            {
            }
            finally
            {
                if (payload.IsCreated)
                    H8Memory.Release(ref payload, VaultOwnerSystemId);
            }
        }

        private bool TryReadBlackBoxEntry(
            IDataVault vault,
            int index,
            out DiegeticHudTelemetryEntry entry)
        {
            entry = default;
            if (vault == null ||
                index < 0 ||
                index >= BlackBoxCapacity ||
                !IsBlackBoxHandle(in _blackBoxHandle) ||
                vault.IsCompactionFenceActive ||
                !vault.TryReadOnlyHandle(in _blackBoxHandle, out NativeArray<DiegeticHudTelemetryEntry>.ReadOnly blackBox) ||
                vault.IsCompactionFenceActive ||
                !blackBox.IsCreated ||
                blackBox.Length <= index)
            {
                return false;
            }

            entry = blackBox[index];
            return true;
        }

        private static void WriteDiegeticHudTelemetryEntry(Span<byte> destination, in DiegeticHudTelemetryEntry entry)
        {
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(0, 4), entry.Frame);
            WriteFloatLittleEndian(destination.Slice(4, 4), entry.Power01);
            WriteFloatLittleEndian(destination.Slice(8, 4), entry.Brownout01);
            WriteFloatLittleEndian(destination.Slice(12, 4), entry.DamageGlitch01);
            WriteFloatLittleEndian(destination.Slice(16, 4), entry.Humidity01);
            WriteFloatLittleEndian(destination.Slice(20, 4), entry.LocalX);
            WriteFloatLittleEndian(destination.Slice(24, 4), entry.LocalY);
            WriteFloatLittleEndian(destination.Slice(28, 4), entry.LocalZ);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(32, 4), entry.Flags);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(36, 4), 0u);
        }

        private static void WriteFloatLittleEndian(Span<byte> destination, float value)
        {
            BinaryPrimitives.WriteInt32LittleEndian(destination, BitConverter.SingleToInt32Bits(value));
        }

        private void ReleaseRuntimeObjects()
        {
            if (_meshFilter != null)
                _meshFilter.sharedMesh = null;
            if (_meshRenderer != null && _meshRenderer.sharedMaterial == _runtimeMaterial)
                _meshRenderer.sharedMaterial = null;

            _runtimeMesh = null;
            _runtimeMaterial = null;

            _meshDistanceMeters = -1f;
            _meshHorizontalDegrees = -1f;
            _meshVerticalDegrees = -1f;
        }

        private float ResolveHalfWidth()
        {
            return RationalTan(horizontalDegrees * DegreesToHalfRadians) * distanceMeters;
        }

        private float ResolveHalfHeight()
        {
            return RationalTan(verticalDegrees * DegreesToHalfRadians) * distanceMeters;
        }

    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 64)]
    public struct DiegeticHudTelemetryEntry
    {
        [System.Runtime.InteropServices.FieldOffset(0)]
        public int Frame;
        [System.Runtime.InteropServices.FieldOffset(4)]
        public float Power01;
        [System.Runtime.InteropServices.FieldOffset(8)]
        public float Brownout01;
        [System.Runtime.InteropServices.FieldOffset(12)]
        public float DamageGlitch01;
        [System.Runtime.InteropServices.FieldOffset(16)]
        public float Humidity01;
        [System.Runtime.InteropServices.FieldOffset(20)]
        public float LocalX;
        [System.Runtime.InteropServices.FieldOffset(24)]
        public float LocalY;
        [System.Runtime.InteropServices.FieldOffset(28)]
        public float LocalZ;
        [System.Runtime.InteropServices.FieldOffset(32)]
        public uint Flags;
        [System.Runtime.InteropServices.FieldOffset(36)]
        private byte _pad0;
        [System.Runtime.InteropServices.FieldOffset(37)]
        private byte _pad1;
        [System.Runtime.InteropServices.FieldOffset(38)]
        private byte _pad2;
        [System.Runtime.InteropServices.FieldOffset(39)]
        private byte _pad3;
        [System.Runtime.InteropServices.FieldOffset(40)]
        private byte _pad4;
        [System.Runtime.InteropServices.FieldOffset(41)]
        private byte _pad5;
        [System.Runtime.InteropServices.FieldOffset(42)]
        private byte _pad6;
        [System.Runtime.InteropServices.FieldOffset(43)]
        private byte _pad7;
        [System.Runtime.InteropServices.FieldOffset(44)]
        private byte _pad8;
        [System.Runtime.InteropServices.FieldOffset(45)]
        private byte _pad9;
        [System.Runtime.InteropServices.FieldOffset(46)]
        private byte _pad10;
        [System.Runtime.InteropServices.FieldOffset(47)]
        private byte _pad11;
        [System.Runtime.InteropServices.FieldOffset(48)]
        private byte _pad12;
        [System.Runtime.InteropServices.FieldOffset(49)]
        private byte _pad13;
        [System.Runtime.InteropServices.FieldOffset(50)]
        private byte _pad14;
        [System.Runtime.InteropServices.FieldOffset(51)]
        private byte _pad15;
        [System.Runtime.InteropServices.FieldOffset(52)]
        private byte _pad16;
        [System.Runtime.InteropServices.FieldOffset(53)]
        private byte _pad17;
        [System.Runtime.InteropServices.FieldOffset(54)]
        private byte _pad18;
        [System.Runtime.InteropServices.FieldOffset(55)]
        private byte _pad19;
        [System.Runtime.InteropServices.FieldOffset(56)]
        private byte _pad20;
        [System.Runtime.InteropServices.FieldOffset(57)]
        private byte _pad21;
        [System.Runtime.InteropServices.FieldOffset(58)]
        private byte _pad22;
        [System.Runtime.InteropServices.FieldOffset(59)]
        private byte _pad23;
        [System.Runtime.InteropServices.FieldOffset(60)]
        private byte _pad24;
        [System.Runtime.InteropServices.FieldOffset(61)]
        private byte _pad25;
        [System.Runtime.InteropServices.FieldOffset(62)]
        private byte _pad26;
        [System.Runtime.InteropServices.FieldOffset(63)]
        private byte _pad27;
    }
}
