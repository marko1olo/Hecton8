using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Vehicles.Automation
{
    public enum DockingSplineRuntimeState : byte
    {
        Inactive = 0,
        Reserved = 1,
        Active = 2,
        Completed = 3,
        Aborted = 4
    }

    [StructLayout(LayoutKind.Explicit, Size = 144)]
    public struct ActiveSplineData
    {
        [FieldOffset(0)] public double3 P0;
        [FieldOffset(24)] public double3 P1;
        [FieldOffset(48)] public double3 P2;
        [FieldOffset(72)] public double3 P3;
        [FieldOffset(96)] public float3 TargetForward;
        [FieldOffset(108)] public float3 TargetUp;
        [FieldOffset(120)] public uint OwnerHash;
        [FieldOffset(124)] public uint RequestId;
        [FieldOffset(128)] public float DurationSeconds;
        [FieldOffset(132)] public float Progress01;
        [FieldOffset(136)] public byte MathLod;
        [FieldOffset(137)] public byte State;
        [FieldOffset(138)] public byte Flags;
        [FieldOffset(139)] public byte Reserved;
        [FieldOffset(140)] public uint ReservedTail;

        public readonly bool IsFinite()
        {
            return OwnerHash != 0u &&
                   DurationSeconds > 0f &&
                   math.isfinite(DurationSeconds) &&
                   math.isfinite(Progress01) &&
                   math.all(math.isfinite(P0)) &&
                   math.all(math.isfinite(P1)) &&
                   math.all(math.isfinite(P2)) &&
                   math.all(math.isfinite(P3)) &&
                   math.all(math.isfinite(TargetForward)) &&
                   math.all(math.isfinite(TargetUp));
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 56)]
    public struct DockingSplineSample
    {
        [FieldOffset(0)] public double3 AbsolutePosition;
        [FieldOffset(24)] public float3 Tangent;
        [FieldOffset(36)] public float3 Up;
        [FieldOffset(48)] public float Progress01;
        [FieldOffset(52)] public byte State;
        [FieldOffset(53)] public byte Reserved0;
        [FieldOffset(54)] public byte Reserved1;
        [FieldOffset(55)] public byte Reserved2;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct CubicBezierJob : IJobParallelFor
    {
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // These pointers are Vault/owner slices passed as raw lanes so the spline sampler can run without per-sample
        // NativeArray view construction. Length fields gate every index before dereference, and read-only spline/progress
        // lanes are never written by this job.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Copying spline rows into temporary NativeArrays was rejected because it adds a staging pass and TempJob lifetime
        // pressure. Using managed spline objects was rejected because it would destroy Burst compatibility and introduce
        // virtual/GC state in the parallel path.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // The invariant is one output sample per Execute index: Samples[index] is exclusively written by that worker,
        // while Splines[index] and Progress01[index] are immutable for the job's input dependency window.
        [NoAlias, NativeDisableUnsafePtrRestriction, ReadOnly] public ActiveSplineData* Splines;
        [NoAlias, NativeDisableUnsafePtrRestriction, ReadOnly] public float* Progress01;
        [NoAlias, NativeDisableUnsafePtrRestriction, WriteOnly] public DockingSplineSample* Samples;
        public int SplineLength;
        public int ProgressLength;
        public int SampleLength;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)SplineLength ||
                (uint)index >= (uint)ProgressLength ||
                (uint)index >= (uint)SampleLength ||
                Splines == null ||
                Progress01 == null ||
                Samples == null)
            {
                return;
            }

            ActiveSplineData spline = Splines[index];
            if (!DockingAutopilotMath.TryEvaluate(in spline, Progress01[index], out DockingSplineSample sample))
                sample = default;

            Samples[index] = sample;
        }
    }

    /// <summary>
    /// Registry-owned docking spline authority. Hot-path consumers cache this service once and read/write blittable spline slots.
    /// </summary>
    public interface IDockingAutopilotService
    {
        /// <summary>True when the GlobalDataVault spline buffer is available.</summary>
        bool IsReady { get; }

        /// <summary>Current active spline capacity exposed for telemetry and bootstrap checks.</summary>
        int ActiveSplineCapacity { get; }

        /// <summary>Reserves a stable spline slot for an owner. Existing owner slots are reused.</summary>
        bool TryAcquireSplineSlot(uint ownerHash, out int slot);

        /// <summary>Writes an active spline into a previously reserved slot.</summary>
        bool TryWriteActiveSpline(int slot, in ActiveSplineData spline);

        /// <summary>Reads an active spline from the vault buffer without allocating.</summary>
        bool TryReadActiveSpline(int slot, out ActiveSplineData spline);

        /// <summary>Evaluates a reserved spline slot with pure Bernstein math.</summary>
        bool TryEvaluateActiveSpline(int slot, float progress01, out DockingSplineSample sample);

        /// <summary>Releases a spline slot when the docking sequence completes or aborts.</summary>
        bool TryReleaseSplineSlot(int slot, uint ownerHash);
    }

    public static class DockingAutopilotMath
    {
        public const byte AuthoritativeMathLod = 1;
        private const float TangentEpsilonSq = 0.000001f;
        private const double ControlDistanceScale = 0.35;
        private const double MinControlDistanceMeters = 1.5;
        private const double MaxControlDistanceMeters = 48.0;

        public static bool TryBuildActiveSpline(
            double3 startAbsolute,
            float3 startForward,
            double3 targetAbsolute,
            float3 targetForward,
            float3 targetUp,
            uint ownerHash,
            uint requestId,
            float durationSeconds,
            byte mathLod,
            out ActiveSplineData spline)
        {
            spline = default;
            if (ownerHash == 0u ||
                !math.isfinite(durationSeconds) ||
                durationSeconds <= 0f ||
                !math.all(math.isfinite(startAbsolute)) ||
                !math.all(math.isfinite(targetAbsolute)))
            {
                return false;
            }

            float3 safeTargetForward = NormalizeOrFallback(targetForward, new float3(0f, 0f, 1f));
            float3 safeStartForward = NormalizeOrFallback(startForward, safeTargetForward);
            float3 safeTargetUp = NormalizeOrFallback(targetUp, new float3(0f, 1f, 0f));
            double3 delta = targetAbsolute - startAbsolute;
            double distanceSq = math.lengthsq(delta);
            if (!math.isfinite(distanceSq) || distanceSq < 0.0)
                return false;

            double distance = distanceSq * math.rsqrt(math.max(distanceSq, 0.0001d));
            double controlDistance = math.clamp(
                distance * ControlDistanceScale,
                MinControlDistanceMeters,
                MaxControlDistanceMeters);
            double3 startHandle = startAbsolute + (global::Hecton8.World.AUPMath.ToDouble3(safeStartForward) * controlDistance);
            double3 targetHandle = targetAbsolute - (global::Hecton8.World.AUPMath.ToDouble3(safeTargetForward) * controlDistance);

            spline = new ActiveSplineData
            {
                P0 = startAbsolute,
                P1 = startHandle,
                P2 = targetHandle,
                P3 = targetAbsolute,
                TargetForward = safeTargetForward,
                TargetUp = safeTargetUp,
                OwnerHash = ownerHash,
                RequestId = requestId,
                DurationSeconds = durationSeconds,
                Progress01 = 0f,
                MathLod = mathLod,
                State = (byte)DockingSplineRuntimeState.Active,
                Flags = 0,
                Reserved = 0
            };

            return spline.IsFinite();
        }

        public static float ResolveInertialProgress01(float normalizedTime)
        {
            float t = math.saturate(normalizedTime);
            return t * t * (3f - (2f * t));
        }

        public static float ResolveDockingProgress01(float normalizedTime)
        {
            return ResolveInertialProgress01(normalizedTime);
        }

        public static float ResolveZeroJerkHermiteProgress01(float normalizedTime)
        {
            float t = math.saturate(normalizedTime);
            float t2 = t * t;
            float t4 = t2 * t2;
            float t5 = t4 * t;
            float t6 = t5 * t;
            float t7 = t6 * t;
            return math.saturate((35f * t4) - (84f * t5) + (70f * t6) - (20f * t7));
        }

        public static bool TryEvaluate(in ActiveSplineData spline, float progress01, out DockingSplineSample sample)
        {
            sample = default;
            if (!spline.IsFinite() || spline.State == (byte)DockingSplineRuntimeState.Inactive)
                return false;

            float t = math.saturate(progress01);
            double td = t;
            double u = 1.0 - td;
            double tt = td * td;
            double uu = u * u;
            double ttt = tt * td;
            double uuu = uu * u;
            double3 position =
                (spline.P0 * uuu) +
                (spline.P1 * (3.0 * uu * td)) +
                (spline.P2 * (3.0 * u * tt)) +
                (spline.P3 * ttt);
            double3 derivative =
                ((spline.P1 - spline.P0) * (3.0 * uu)) +
                ((spline.P2 - spline.P1) * (6.0 * u * td)) +
                ((spline.P3 - spline.P2) * (3.0 * tt));

            if (!math.all(math.isfinite(position)) || !math.all(math.isfinite(derivative)))
                return false;

            sample = new DockingSplineSample
            {
                AbsolutePosition = position,
                Tangent = NormalizeTangent(derivative, spline.TargetForward),
                Up = NormalizeOrFallback(spline.TargetUp, new float3(0f, 1f, 0f)),
                Progress01 = t,
                State = spline.State,
                Reserved0 = 0,
                Reserved1 = 0,
                Reserved2 = 0
            };
            return math.all(math.isfinite(sample.Tangent)) && math.all(math.isfinite(sample.Up));
        }

        public static float3 NormalizeOrFallback(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            if (!math.all(math.isfinite(value)) || !math.isfinite(lengthSq) || lengthSq <= TangentEpsilonSq)
                value = fallback;

            lengthSq = math.lengthsq(value);
            if (!math.all(math.isfinite(value)) || !math.isfinite(lengthSq) || lengthSq <= TangentEpsilonSq)
                return new float3(0f, 0f, 1f);

            return value * math.rsqrt(math.max(lengthSq, TangentEpsilonSq));
        }

        public static Vector3 ResolveRuntimePosition(double3 absolutePosition, Vector3 fallbackPosition)
        {
            if (!math.all(math.isfinite(absolutePosition)))
                return fallbackPosition;

            float3 runtime = AbsoluteUniversePosition.FromAbsolutePosition(absolutePosition).ToRuntimeFloat3();
            Vector3 result = new Vector3(runtime.x, runtime.y, runtime.z);
            return IsFiniteVector(result) ? result : fallbackPosition;
        }

        private static float3 NormalizeTangent(double3 derivative, float3 fallback)
        {
            float3 tangent = new float3((float)derivative.x, (float)derivative.y, (float)derivative.z);
            return NormalizeOrFallback(tangent, fallback);
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return !(float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z) ||
                     float.IsInfinity(value.x) || float.IsInfinity(value.y) || float.IsInfinity(value.z));
        }
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Physics/Vehicles/Docking Autopilot Service")]
    public sealed class DockingAutopilotService :
        MonoBehaviour,
        IDockingAutopilotService,
        IServiceHeartbeat,
        IServiceShutdown,
        IGlobalRegistryHotSwapListener
    {
        private const int DefaultActiveSplineCapacity = 64;
        private const int MaxActiveSplineCapacity = 256;

        [SerializeField, Min(1)] private int activeSplineCapacity = DefaultActiveSplineCapacity;

        private IDataVault _dataVault;
        private VaultGenerationHandle<ActiveSplineData> _activeSplineHandle;
        private int _activeSplineLength;
        private bool _serviceRegistered;
        private bool _hotSwapRegistered;
        private ServiceHeartbeatState _heartbeatState = ServiceHeartbeatState.NotStarted;

        public bool IsReady => IsVaultHandleCreated(in _activeSplineHandle) && _activeSplineLength > 0 && _heartbeatState == ServiceHeartbeatState.Ready;
        public int ActiveSplineCapacity => IsVaultHandleCreated(in _activeSplineHandle) ? _activeSplineLength : 0;
        public ServiceHeartbeatState HeartbeatState => _heartbeatState;
        public bool IsServiceReady => IsReady;

        // DockingAutopilotService is the sole IDockingAutopilotService owner and had no construction
        // site of any kind. No AddComponent, no scene/prefab GUID hit for
        // 3d6fecc0d76140547a5275b902b63c4b. Live consumer VehicleDockingModule.cs:1845 caches
        // GlobalRegistry.DockingAutopilot permanently null. Same shape as DebrisManager: ship
        // EnsureRuntimeInstance and call it from bootstrap.

        private const string RuntimeRootName = "__HECTON_DOCKING_AUTOPILOT_RUNTIME";

        /// <summary>
        /// Cold-path resolve-or-create for the docking autopilot owner. Idempotent.
        /// </summary>
        public static DockingAutopilotService EnsureRuntimeInstance()
        {
            if (GlobalRegistry.DockingAutopilot is DockingAutopilotService registered && registered != null)
                return registered;

            DockingAutopilotService existing = FindFirstObjectByType<DockingAutopilotService>(FindObjectsInactive.Include);
            if (existing != null)
            {
                if (!existing.gameObject.activeSelf)
                    existing.gameObject.SetActive(true);
                if (!existing.enabled)
                    existing.enabled = true;
                return existing;
            }

            // Player-build construction path: no authored/bootstrap instance reachable.
            // VehicleDockingModule caches GlobalRegistry.DockingAutopilot; without this create
            // path the slot stays null forever when no scene/prefab instance exists.
            GameObject root = GameObject.Find(RuntimeRootName);
            if (root == null)
                root = new GameObject(RuntimeRootName); // COLD ALLOC: GameObject[1] - docking autopilot runtime root - owner: DockingAutopilotService

            root.hideFlags = HideFlags.None;
            if (!root.activeSelf)
                root.SetActive(true);

            if (!root.TryGetComponent(out DockingAutopilotService service))
                service = root.AddComponent<DockingAutopilotService>();

            return service;
        }


        private void OnEnable()
        {
            InitializeService();
        }


        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            UnregisterService();
            ReleaseActiveSplineBuffer();
            _dataVault = null;
        }

        private void OnDestroy()
        {
            OnServiceShutdown();
        }

        public void InitializeService()
        {
            activeSplineCapacity = math.clamp(activeSplineCapacity, 1, MaxActiveSplineCapacity);
            RefreshDataVaultReferenceCold();
            if (!_serviceRegistered)
            {
                GlobalRegistry.RegisterDockingAutopilotService(this);
                _serviceRegistered = ReferenceEquals(GlobalRegistry.DockingAutopilot, this);
            }

            TryRegisterHotSwapListener();
            _heartbeatState = EnsureSplineBufferAvailable()
                ? ServiceHeartbeatState.Ready
                : ServiceHeartbeatState.Degraded;
        }

        /// <inheritdoc />
        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            if (ReferenceEquals(_dataVault, currentService))
                return;

            ReleaseVaultBuffer(previousService as IDataVault ?? _dataVault, ref _activeSplineHandle);
            _activeSplineLength = 0;
            _dataVault = currentService as IDataVault;
            _heartbeatState = EnsureSplineBufferAvailable()
                ? ServiceHeartbeatState.Ready
                : ServiceHeartbeatState.Degraded;
        }

        public bool TryAcquireSplineSlot(uint ownerHash, out int slot)
        {
            slot = -1;
            IDataVault vault = _dataVault;
            if (ownerHash == 0u || !IsReady || !TryAcquireActiveSplineWriteView(vault, out NativeArray<ActiveSplineData> activeSplines))
                return false;

            try
            {
                int length = activeSplines.Length;
                for (int i = 0; i < length; i++)
                {
                    ActiveSplineData existing = activeSplines[i];
                    if (existing.OwnerHash == ownerHash && existing.State != (byte)DockingSplineRuntimeState.Inactive)
                    {
                        slot = i;
                        return true;
                    }
                }

                for (int i = 0; i < length; i++)
                {
                    ActiveSplineData existing = activeSplines[i];
                    if (existing.State != (byte)DockingSplineRuntimeState.Inactive)
                        continue;

                    existing = default;
                    existing.OwnerHash = ownerHash;
                    existing.State = (byte)DockingSplineRuntimeState.Reserved;
                    activeSplines[i] = existing;
                    slot = i;
                    return true;
                }

                _heartbeatState = ServiceHeartbeatState.Degraded;
                return false;
            }
            finally
            {
                vault.ReleaseWriteLock(in _activeSplineHandle, SystemID.VehiclesPhysics);
            }
        }

        public bool TryWriteActiveSpline(int slot, in ActiveSplineData spline)
        {
            IDataVault vault = _dataVault;
            if (!spline.IsFinite() ||
                !IsReady ||
                !TryAcquireActiveSplineWriteView(vault, out NativeArray<ActiveSplineData> activeSplines))
            {
                return false;
            }

            try
            {
                if ((uint)slot >= (uint)activeSplines.Length)
                {
                    return false;
                }

                ActiveSplineData existing = activeSplines[slot];
                if (existing.State != (byte)DockingSplineRuntimeState.Inactive &&
                    existing.OwnerHash != spline.OwnerHash)
                {
                    return false;
                }

                ActiveSplineData writable = spline;
                if (writable.State == (byte)DockingSplineRuntimeState.Inactive)
                    writable.State = (byte)DockingSplineRuntimeState.Active;

                activeSplines[slot] = writable;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _activeSplineHandle, SystemID.VehiclesPhysics);
            }
        }

        public bool TryReadActiveSpline(int slot, out ActiveSplineData spline)
        {
            spline = default;
            if (!TryReadOnlyActiveSplineView(out NativeArray<ActiveSplineData>.ReadOnly activeSplines) ||
                (uint)slot >= (uint)activeSplines.Length)
            {
                return false;
            }

            spline = activeSplines[slot];
            return spline.State != (byte)DockingSplineRuntimeState.Inactive && spline.IsFinite();
        }

        public bool TryEvaluateActiveSpline(int slot, float progress01, out DockingSplineSample sample)
        {
            sample = default;
            if (!TryReadOnlyActiveSplineView(out NativeArray<ActiveSplineData>.ReadOnly activeSplines) ||
                (uint)slot >= (uint)activeSplines.Length)
            {
                return false;
            }

            ActiveSplineData spline = activeSplines[slot];
            if (spline.State == (byte)DockingSplineRuntimeState.Inactive || !spline.IsFinite())
                return false;

            return DockingAutopilotMath.TryEvaluate(in spline, math.saturate(progress01), out sample);
        }

        public bool TryReleaseSplineSlot(int slot, uint ownerHash)
        {
            IDataVault vault = _dataVault;
            if (ownerHash == 0u ||
                !IsReady ||
                !TryAcquireActiveSplineWriteView(vault, out NativeArray<ActiveSplineData> activeSplines))
            {
                return false;
            }

            try
            {
                if ((uint)slot >= (uint)activeSplines.Length)
                {
                    return false;
                }

                ActiveSplineData existing = activeSplines[slot];
                if (existing.OwnerHash != ownerHash)
                    return false;

                activeSplines[slot] = default;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _activeSplineHandle, SystemID.VehiclesPhysics);
            }
        }

        public void OnServiceShutdown()
        {
            IDataVault vault = _dataVault;
            if (TryAcquireActiveSplineWriteView(vault, out NativeArray<ActiveSplineData> activeSplines))
            {
                try
                {
                    for (int i = 0; i < activeSplines.Length; i++)
                        activeSplines[i] = default;
                }
                finally
                {
                    vault.ReleaseWriteLock(in _activeSplineHandle, SystemID.VehiclesPhysics);
                }
            }

            UnregisterService();
            TryUnregisterHotSwapListener();
            ReleaseActiveSplineBuffer();
            _dataVault = null;
            _heartbeatState = ServiceHeartbeatState.Shutdown;
        }

        private bool EnsureSplineBufferAvailable()
        {
            if (_dataVault == null)
                return false;

            activeSplineCapacity = math.clamp(activeSplineCapacity, 1, MaxActiveSplineCapacity);
            if (IsVaultHandleCreated(in _activeSplineHandle) &&
                _dataVault.TryResolveHandle(in _activeSplineHandle, out NativeArray<ActiveSplineData> activeSplines) &&
                activeSplines.IsCreated &&
                activeSplines.Length >= activeSplineCapacity)
            {
                _activeSplineLength = activeSplines.Length;
                return true;
            }

            _activeSplineHandle = default;
            _activeSplineLength = 0;
            if (_dataVault.TryGetGenerationHandle(
                    BufferID.VehicleDockingActiveSplines,
                    out VaultGenerationHandle<ActiveSplineData> refreshed) &&
                _dataVault.TryResolveHandle(in refreshed, out NativeArray<ActiveSplineData> refreshedSplines) &&
                refreshedSplines.IsCreated &&
                refreshedSplines.Length >= activeSplineCapacity)
            {
                _activeSplineHandle = refreshed;
                _activeSplineLength = refreshedSplines.Length;
                return true;
            }

            if (_dataVault.IsAllocationLocked || _dataVault.IsCompactionFenceActive)
                return false;

            VaultGenerationHandle<ActiveSplineData> acquired = _dataVault.EnsureGenerationHandle<ActiveSplineData>(
                BufferID.VehicleDockingActiveSplines,
                activeSplineCapacity,
                SystemID.VehiclesPhysics,
                NativeArrayOptions.ClearMemory);
            if (!IsVaultHandleCreated(in acquired) ||
                !_dataVault.TryResolveHandle(in acquired, out NativeArray<ActiveSplineData> acquiredSplines) ||
                !acquiredSplines.IsCreated ||
                acquiredSplines.Length < activeSplineCapacity)
            {
                _activeSplineHandle = default;
                _activeSplineLength = 0;
                return false;
            }

            _activeSplineHandle = acquired;
            _activeSplineLength = acquiredSplines.Length;
            return true;
        }

        private void RefreshDataVaultReferenceCold()
        {
            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;
        }

        private bool TryAcquireActiveSplineWriteView(IDataVault vault, out NativeArray<ActiveSplineData> activeSplines)
        {
            activeSplines = default;
            if (!IsVaultHandleCreated(in _activeSplineHandle) || vault == null || vault.IsCompactionFenceActive)
                return false;

            VaultGenerationHandle<ActiveSplineData> activeSplineHandle = _activeSplineHandle;
            if (!vault.TryAcquireWriteLock(in activeSplineHandle, SystemID.VehiclesPhysics, out activeSplines))
                return false;

            bool releaseOnFailure = true;
            try
            {
                if (!activeSplines.IsCreated || activeSplines.Length <= 0)
                {
                    _activeSplineHandle = default;
                    _activeSplineLength = 0;
                    _heartbeatState = ServiceHeartbeatState.Degraded;
                    return false;
                }

                _activeSplineLength = activeSplines.Length;
                releaseOnFailure = false;
                return true;
            }
            finally
            {
                if (releaseOnFailure)
                {
                    vault.ReleaseWriteLock(in activeSplineHandle, SystemID.VehiclesPhysics);
                    activeSplines = default;
                }
            }
        }

        private bool TryReadOnlyActiveSplineView(out NativeArray<ActiveSplineData>.ReadOnly activeSplines)
        {
            activeSplines = default;
            if (!IsVaultHandleCreated(in _activeSplineHandle) || _dataVault == null || _dataVault.IsCompactionFenceActive)
                return false;

            if (!_dataVault.TryReadOnlyHandle(in _activeSplineHandle, out activeSplines) ||
                !activeSplines.IsCreated ||
                activeSplines.Length <= 0)
            {
                return false;
            }

            return true;
        }

        private void ReleaseActiveSplineBuffer()
        {
            ReleaseVaultBuffer(_dataVault, ref _activeSplineHandle);
            _activeSplineLength = 0;
        }

        private static bool IsVaultHandleCreated<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private static void ReleaseVaultBuffer<T>(IDataVault vault, ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (vault != null && IsVaultHandleCreated(in handle))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private void UnregisterService()
        {
            if (_serviceRegistered && ReferenceEquals(GlobalRegistry.DockingAutopilot, this))
                GlobalRegistry.UnregisterDockingAutopilotService(this);

            _serviceRegistered = false;
            if (_heartbeatState != ServiceHeartbeatState.Shutdown)
                _heartbeatState = ServiceHeartbeatState.NotStarted;
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
    }
}
