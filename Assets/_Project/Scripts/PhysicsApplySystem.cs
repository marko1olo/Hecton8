using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Gameplay;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Physics
{
    /// <summary>
    /// Fixed-step global physics frame counter used by staggered physics systems.
    /// </summary>
    public static class PhysicsFrame
    {
        /// <summary>
        /// Current fixed-step frame index.
        /// </summary>
        public static int Current { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Current = 0;
        }

        internal static void Tick()
        {
            Current++;
        }
    }

    /// <summary>
    /// Deferred main-thread force application payload.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ForcePacket
    {
        /// <summary>World-space force vector.</summary>
        public Vector3 Force;

        /// <summary>World-space torque vector.</summary>
        public Vector3 Torque;

        /// <summary>Local point offset placeholder for future AddForceAtPosition routing.</summary>
        public Vector3 PointOffset;

        /// <summary>Force application mode.</summary>
        public ForceMode Mode;

        /// <summary>Bitfield flags describing packet contents.</summary>
        public byte Flags;

        /// <summary>Dense rigidbody slot index owned by <see cref="PhysicsApplySystem"/>.</summary>
        public int RigidbodyIndex;
    }

    [System.Flags]
    internal enum ForcePacketFlags : byte
    {
        None = 0,
        HasForce = 1 << 0,
        HasTorque = 1 << 1,
        WakeBody = 1 << 2,
    }

    /// <summary>
    /// Authoritative main-thread owner for deferred Rigidbody force and torque application.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9000)]
    public sealed class PhysicsApplySystem : MonoBehaviour, IPhysicsService
    {
        private const int MaxTrackedBodies = 64;
        private const int MaxQueuedPackets = 512;
        private const float MinMagnitudeSq = 0.000001f;

        private static PhysicsApplySystem _instance;

        // COLD ALLOC: ForcePacket[512] — previous-step flush buffer — owner: PhysicsApplySystem
        private ForcePacket[] _frontPackets = new ForcePacket[MaxQueuedPackets];
        // COLD ALLOC: ForcePacket[512] — current-step gather buffer — owner: PhysicsApplySystem
        private ForcePacket[] _backPackets = new ForcePacket[MaxQueuedPackets];
        // COLD ALLOC: Rigidbody[64] — active rigidbody slot map for deferred packet application — owner: PhysicsApplySystem
        private readonly Rigidbody[] _bodySlots = new Rigidbody[MaxTrackedBodies];

        private int _frontCount;
        private int _backCount;
        private bool _isInitialized;

        /// <summary>
        /// True once the service is registered into <see cref="GlobalRegistry"/>.
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Ensures a live runtime instance exists.
        /// </summary>
        /// <returns>Live physics apply instance.</returns>
        public static PhysicsApplySystem EnsureRuntimeInstance()
        {
            if (_instance != null)
                return _instance;

            GameObject runtimeRoot = new GameObject("[PhysicsApplySystem]");
            PhysicsApplySystem applySystem = runtimeRoot.AddComponent<PhysicsApplySystem>();
            return applySystem;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        /// <summary>
        /// Explicitly initializes the service and registers it into <see cref="GlobalRegistry"/>.
        /// </summary>
        public void InitializeService()
        {
            if (_isInitialized)
                return;

            GlobalRegistry.RegisterPhysicsService(this);
            _isInitialized = true;
        }

        /// <summary>
        /// Static fallback clear path used before the service is resolved into <see cref="GlobalRegistry"/>.
        /// </summary>
        public static void ClearQueuedPacketsStatic()
        {
            if (_instance != null)
                _instance.ClearQueuedPackets();
        }

        /// <inheritdoc />
        public bool QueueForce(Rigidbody body, Vector3 force, ForceMode mode, bool wake = true)
        {
            if (!IsFiniteNonZero(force) || body == null || body.isKinematic)
                return false;

            int rigidbodyIndex = ResolveBodyIndex(body);
            if (rigidbodyIndex < 0 || _backCount >= _backPackets.Length)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[PhysicsApplySystem] Force packet queue saturated.");
#endif
                return false;
            }

            _backPackets[_backCount++] = new ForcePacket
            {
                Force = force,
                Torque = Vector3.zero,
                PointOffset = Vector3.zero,
                Mode = mode,
                Flags = (byte)(ForcePacketFlags.HasForce | (wake ? ForcePacketFlags.WakeBody : ForcePacketFlags.None)),
                RigidbodyIndex = rigidbodyIndex
            };
            return true;
        }

        /// <inheritdoc />
        public bool QueueTorque(Rigidbody body, Vector3 torque, ForceMode mode, bool wake = true)
        {
            if (!IsFiniteNonZero(torque) || body == null || body.isKinematic)
                return false;

            int rigidbodyIndex = ResolveBodyIndex(body);
            if (rigidbodyIndex < 0 || _backCount >= _backPackets.Length)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[PhysicsApplySystem] Torque packet queue saturated.");
#endif
                return false;
            }

            _backPackets[_backCount++] = new ForcePacket
            {
                Force = Vector3.zero,
                Torque = torque,
                PointOffset = Vector3.zero,
                Mode = mode,
                Flags = (byte)(ForcePacketFlags.HasTorque | (wake ? ForcePacketFlags.WakeBody : ForcePacketFlags.None)),
                RigidbodyIndex = rigidbodyIndex
            };
            return true;
        }

        /// <inheritdoc />
        public void ClearQueuedPackets()
        {
            System.Array.Clear(_frontPackets, 0, _frontCount);
            System.Array.Clear(_backPackets, 0, _backCount);
            System.Array.Clear(_bodySlots, 0, _bodySlots.Length);
            _frontCount = 0;
            _backCount = 0;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;

            if (Application.isPlaying)
            {
                if (transform.parent != null)
                    transform.SetParent(null, true);

                DontDestroyOnLoad(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (_isInitialized)
            {
                GlobalRegistry.UnregisterPhysicsService(this);
                _isInitialized = false;
            }

            if (_instance == this)
                _instance = null;
        }

        private void FixedUpdate()
        {
            PhysicsFrame.Tick();
            FlushFrontBuffer();
            SwapBuffers();
        }

        private void FlushFrontBuffer()
        {
            for (int i = 0; i < _frontCount; i++)
            {
                ForcePacket packet = _frontPackets[i];
                Rigidbody body = ResolveBody(packet.RigidbodyIndex);
                if (body == null || body.isKinematic)
                    continue;

                ForcePacketFlags flags = (ForcePacketFlags)packet.Flags;
                if ((flags & ForcePacketFlags.WakeBody) != 0 && body.IsSleeping())
                    body.WakeUp();

                if ((flags & ForcePacketFlags.HasForce) != 0)
                    body.AddForce(packet.Force, packet.Mode);

                if ((flags & ForcePacketFlags.HasTorque) != 0)
                    body.AddTorque(packet.Torque, packet.Mode);
            }

            System.Array.Clear(_frontPackets, 0, _frontCount);
            _frontCount = 0;
        }

        private void SwapBuffers()
        {
            ForcePacket[] swap = _frontPackets;
            _frontPackets = _backPackets;
            _backPackets = swap;
            _frontCount = _backCount;
            _backCount = 0;
        }

        private int ResolveBodyIndex(Rigidbody body)
        {
            for (int i = 0; i < _bodySlots.Length; i++)
            {
                Rigidbody slot = _bodySlots[i];
                if (slot == body)
                    return i;

                if (slot == null)
                {
                    _bodySlots[i] = body;
                    return i;
                }
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[PhysicsApplySystem] Rigidbody slot capacity exceeded.");
#endif
            return -1;
        }

        private Rigidbody ResolveBody(int rigidbodyIndex)
        {
            if ((uint)rigidbodyIndex >= (uint)_bodySlots.Length)
                return null;

            return _bodySlots[rigidbodyIndex];
        }

        private static bool IsFiniteNonZero(Vector3 value)
        {
            float3 value3 = new float3(value.x, value.y, value.z);
            return math.all(math.isfinite(value3)) && math.lengthsq(value3) > MinMagnitudeSq;
        }
    }

    /// <summary>
    /// Common physics routing facade that keeps player-body writes inside <see cref="IMotorForces"/>
    /// and routes all other rigidbody writes through <see cref="PhysicsApplySystem"/>.
    /// </summary>
    public static class PhysicsForceRouter
    {
        /// <summary>
        /// Routes a force request either into the player motor owner or the deferred packet system.
        /// </summary>
        /// <param name="body">Target rigidbody.</param>
        /// <param name="force">World-space force vector.</param>
        /// <param name="mode">Force application mode.</param>
        /// <param name="wake">True to wake sleeping bodies before application.</param>
        /// <returns>True when the request was accepted.</returns>
        public static bool QueueForce(Rigidbody body, Vector3 force, ForceMode mode, bool wake = true)
        {
            if (TryRouteToPlayerMotor(body, force, mode))
                return true;

            PhysicsApplySystem system = PhysicsApplySystem.EnsureRuntimeInstance();
            return system.QueueForce(body, force, mode, wake);
        }

        /// <summary>
        /// Routes a torque request into the deferred packet system.
        /// </summary>
        /// <param name="body">Target rigidbody.</param>
        /// <param name="torque">World-space torque vector.</param>
        /// <param name="mode">Force application mode.</param>
        /// <param name="wake">True to wake sleeping bodies before application.</param>
        /// <returns>True when the request was accepted.</returns>
        public static bool QueueTorque(Rigidbody body, Vector3 torque, ForceMode mode, bool wake = true)
        {
            PhysicsApplySystem system = PhysicsApplySystem.EnsureRuntimeInstance();
            return system.QueueTorque(body, torque, mode, wake);
        }

        private static bool TryRouteToPlayerMotor(Rigidbody body, Vector3 force, ForceMode mode)
        {
            if (body == null || !body.TryGetComponent(out HectonPlayerMotor playerMotor))
                return false;

            float mass = math.max(body.mass, 0.0001f);
            switch (mode)
            {
                case ForceMode.Force:
                    playerMotor.AddExternalAcceleration(force / mass);
                    return true;

                case ForceMode.Acceleration:
                    playerMotor.AddExternalAcceleration(force);
                    return true;

                case ForceMode.Impulse:
                    playerMotor.AddExternalVelocityChange(force / mass);
                    return true;

                case ForceMode.VelocityChange:
                    playerMotor.AddExternalVelocityChange(force);
                    return true;
            }

            return false;
        }
    }
}
