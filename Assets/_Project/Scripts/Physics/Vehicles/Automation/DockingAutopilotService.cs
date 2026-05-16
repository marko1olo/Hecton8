using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
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

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 144)]
    public struct ActiveSplineData
    {
        public double3 P0;
        public double3 P1;
        public double3 P2;
        public double3 P3;
        public float3 TargetForward;
        public float3 TargetUp;
        public uint OwnerHash;
        public uint RequestId;
        public float DurationSeconds;
        public float Progress01;
        public byte MathLod;
        public byte State;
        public byte Flags;
        public byte Reserved;
        public uint ReservedTail;

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

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 56)]
    public struct DockingSplineSample
    {
        public double3 AbsolutePosition;
        public float3 Tangent;
        public float3 Up;
        public float Progress01;
        public byte State;
        public byte Reserved0;
        public byte Reserved1;
        public byte Reserved2;
    }

    [BurstCompile]
    public struct CubicBezierJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<ActiveSplineData> Splines;
        [ReadOnly] public NativeArray<float> Progress01;
        public NativeArray<DockingSplineSample> Samples;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Splines.Length ||
                (uint)index >= (uint)Progress01.Length ||
                (uint)index >= (uint)Samples.Length)
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
            double3 startHandle = startAbsolute + (ToDouble3(safeStartForward) * controlDistance);
            double3 targetHandle = targetAbsolute - (ToDouble3(safeTargetForward) * controlDistance);

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

            return value * math.rsqrt(lengthSq);
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

        private static double3 ToDouble3(float3 value)
        {
            return new double3(value.x, value.y, value.z);
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return !(float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z) ||
                     float.IsInfinity(value.x) || float.IsInfinity(value.y) || float.IsInfinity(value.z));
        }
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Physics/Vehicles/Docking Autopilot Service")]
    public sealed class DockingAutopilotService : MonoBehaviour, IDockingAutopilotService, IServiceHeartbeat, IServiceShutdown
    {
        private const int DefaultActiveSplineCapacity = 64;
        private const int MaxActiveSplineCapacity = 256;

        [SerializeField, Min(1)] private int activeSplineCapacity = DefaultActiveSplineCapacity;

        private IDataVault _dataVault;
        private NativeArray<ActiveSplineData> _activeSplines;
        private bool _serviceRegistered;
        private ServiceHeartbeatState _heartbeatState = ServiceHeartbeatState.NotStarted;

        public bool IsReady => _activeSplines.IsCreated && _heartbeatState == ServiceHeartbeatState.Ready;
        public int ActiveSplineCapacity => _activeSplines.IsCreated ? _activeSplines.Length : 0;
        public ServiceHeartbeatState HeartbeatState => _heartbeatState;
        public bool IsServiceReady => IsReady;

        private void OnEnable()
        {
            InitializeService();
        }

        private void OnDisable()
        {
            UnregisterService();
        }

        private void OnDestroy()
        {
            OnServiceShutdown();
        }

        public void InitializeService()
        {
            activeSplineCapacity = math.clamp(activeSplineCapacity, 1, MaxActiveSplineCapacity);
            if (!_serviceRegistered)
            {
                GlobalRegistry.RegisterDockingAutopilotService(this);
                _serviceRegistered = ReferenceEquals(GlobalRegistry.DockingAutopilot, this);
            }

            _heartbeatState = EnsureSplineBufferAvailable()
                ? ServiceHeartbeatState.Ready
                : ServiceHeartbeatState.Degraded;
        }

        public bool TryAcquireSplineSlot(uint ownerHash, out int slot)
        {
            slot = -1;
            if (ownerHash == 0u || !EnsureSplineBufferAvailable())
                return false;

            for (int i = 0; i < _activeSplines.Length; i++)
            {
                ActiveSplineData existing = _activeSplines[i];
                if (existing.OwnerHash == ownerHash && existing.State != (byte)DockingSplineRuntimeState.Inactive)
                {
                    slot = i;
                    return true;
                }
            }

            for (int i = 0; i < _activeSplines.Length; i++)
            {
                ActiveSplineData existing = _activeSplines[i];
                if (existing.State != (byte)DockingSplineRuntimeState.Inactive)
                    continue;

                existing = default;
                existing.OwnerHash = ownerHash;
                existing.State = (byte)DockingSplineRuntimeState.Reserved;
                _activeSplines[i] = existing;
                slot = i;
                return true;
            }

            _heartbeatState = ServiceHeartbeatState.Degraded;
            return false;
        }

        public bool TryWriteActiveSpline(int slot, in ActiveSplineData spline)
        {
            if (!IsValidSlot(slot) || !spline.IsFinite())
                return false;

            ActiveSplineData writable = spline;
            if (writable.State == (byte)DockingSplineRuntimeState.Inactive)
                writable.State = (byte)DockingSplineRuntimeState.Active;

            _activeSplines[slot] = writable;
            return true;
        }

        public bool TryReadActiveSpline(int slot, out ActiveSplineData spline)
        {
            spline = default;
            if (!IsValidSlot(slot))
                return false;

            spline = _activeSplines[slot];
            return spline.State != (byte)DockingSplineRuntimeState.Inactive && spline.IsFinite();
        }

        public bool TryEvaluateActiveSpline(int slot, float progress01, out DockingSplineSample sample)
        {
            sample = default;
            return TryReadActiveSpline(slot, out ActiveSplineData spline) &&
                   DockingAutopilotMath.TryEvaluate(in spline, progress01, out sample);
        }

        public bool TryReleaseSplineSlot(int slot, uint ownerHash)
        {
            if (!IsValidSlot(slot) || ownerHash == 0u)
                return false;

            ActiveSplineData existing = _activeSplines[slot];
            if (existing.OwnerHash != ownerHash)
                return false;

            _activeSplines[slot] = default;
            return true;
        }

        public void OnServiceShutdown()
        {
            if (_activeSplines.IsCreated)
            {
                for (int i = 0; i < _activeSplines.Length; i++)
                    _activeSplines[i] = default;
            }

            UnregisterService();
            _activeSplines = default;
            _dataVault = null;
            _heartbeatState = ServiceHeartbeatState.Shutdown;
        }

        private bool EnsureSplineBufferAvailable()
        {
            if (_activeSplines.IsCreated)
                return true;

            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;
            if (_dataVault == null)
                return false;

            _activeSplines = _dataVault.GetBuffer<ActiveSplineData>(
                BufferID.VehicleDockingActiveSplines,
                activeSplineCapacity,
                SystemID.VehiclesPhysics,
                NativeArrayOptions.ClearMemory);
            return _activeSplines.IsCreated && _activeSplines.Length >= activeSplineCapacity;
        }

        private bool IsValidSlot(int slot)
        {
            return _activeSplines.IsCreated && (uint)slot < (uint)_activeSplines.Length;
        }

        private void UnregisterService()
        {
            if (_serviceRegistered && ReferenceEquals(GlobalRegistry.DockingAutopilot, this))
                GlobalRegistry.UnregisterDockingAutopilotService(this);

            _serviceRegistered = false;
            if (_heartbeatState != ServiceHeartbeatState.Shutdown)
                _heartbeatState = ServiceHeartbeatState.NotStarted;
        }
    }
}
