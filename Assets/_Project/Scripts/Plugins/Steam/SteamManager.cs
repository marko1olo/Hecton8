using System;
using System.Threading;
using Hecton8.Core;
using UnityEngine;

#if HECTON8_STEAMWORKS
using Steamworks;
#endif

namespace Hecton8.Plugins.Steam
{
    /// <summary>
    /// Steamworks border component. Gameplay never references Steamworks types.
    /// Initialization is background-owned; callbacks are drained only from the slow tick.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-30970)]
    public sealed class SteamManager : MonoBehaviour, IFrostTickable, IServiceHeartbeat, IServiceShutdown, IGlobalRegistryHotSwapListener
    {
        private const int StateNotStarted = 0;
        private const int StateBooting = 1;
        private const int StateReady = 2;
        private const int StateUnavailable = 3;
        private const int StateFailed = 4;
        private const int StateShutdown = 5;

        private Thread _initThread;
        private volatile bool _shutdownRequested;
        private int _state;
        private bool _registeredFrostTick;
        private bool _registeredHotSwap;

        public ServiceHeartbeatState HeartbeatState
        {
            get
            {
                int state = Volatile.Read(ref _state);
                switch (state)
                {
                    case StateBooting:
                        return ServiceHeartbeatState.Booting;
                    case StateReady:
                        return ServiceHeartbeatState.Ready;
                    case StateUnavailable:
                        return ServiceHeartbeatState.Degraded;
                    case StateFailed:
                        return ServiceHeartbeatState.Failed;
                    case StateShutdown:
                        return ServiceHeartbeatState.Shutdown;
                    default:
                        return ServiceHeartbeatState.NotStarted;
                }
            }
        }

        public bool IsServiceReady => Volatile.Read(ref _state) == StateReady;

        private void OnEnable()
        {
            RegisterFrostTick();
            TryRegisterHotSwapListener();
            StartBackgroundInit();
        }

        private void OnDisable()
        {
            UnregisterFrostTick();
            TryUnregisterHotSwapListener();
        }

        private void OnDestroy()
        {
            OnServiceShutdown();
        }

        public void FrostTick()
        {
            if (_shutdownRequested || Volatile.Read(ref _state) != StateReady)
                return;

#if HECTON8_STEAMWORKS
            SteamAPI.RunCallbacks();
#endif
        }

        public void OnServiceShutdown()
        {
            if (Volatile.Read(ref _state) == StateShutdown)
                return;

            _shutdownRequested = true;
            UnregisterFrostTick();
            TryUnregisterHotSwapListener();

#if HECTON8_STEAMWORKS
            if (Volatile.Read(ref _state) == StateReady)
                SteamAPI.Shutdown();
#endif

            Volatile.Write(ref _state, StateShutdown);
        }

        private void RegisterFrostTick()
        {
            if (_registeredFrostTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredFrostTick = GlobalRegistry.TryRegisterFrostTickable(this, PriorityLayer.Core);
        }

        private void UnregisterFrostTick()
        {
            if (!_registeredFrostTick)
                return;

            GlobalRegistry.UnregisterFrostTickable(this, PriorityLayer.Core);
            _registeredFrostTick = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
            {
                return;
            }

            bool wasRegistered = _registeredFrostTick;
            UnregisterFrostTick();
            if (currentService == null ||
                _shutdownRequested ||
                !isActiveAndEnabled)
            {
                return;
            }

            if (wasRegistered)
                RegisterFrostTick();
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private void StartBackgroundInit()
        {
            if (!Application.isPlaying || _shutdownRequested)
                return;

            if (Interlocked.CompareExchange(ref _state, StateBooting, StateNotStarted) != StateNotStarted)
                return;

            try
            {
                Thread thread = new Thread(InitializeSteamworksBackground)
                {
                    IsBackground = true,
                    Name = "Hecton8 Steam Init"
                }; // COLD ALLOC: Thread[1] - Steamworks border initialization lane - owner: SteamManager
                _initThread = thread;
                thread.Start();
            }
            catch (Exception)
            {
                _initThread = null;
                Volatile.Write(ref _state, _shutdownRequested ? StateShutdown : StateFailed);
            }
        }

        private void InitializeSteamworksBackground()
        {
            try
            {
#if HECTON8_STEAMWORKS
                bool initialized = SteamAPI.Init();
                if (_shutdownRequested)
                {
                    if (initialized)
                        SteamAPI.Shutdown();

                    Volatile.Write(ref _state, StateShutdown);
                    return;
                }

                Volatile.Write(ref _state, initialized ? StateReady : StateUnavailable);
#else
                if (_shutdownRequested)
                {
                    Volatile.Write(ref _state, StateShutdown);
                    return;
                }

                Volatile.Write(ref _state, StateUnavailable);
#endif
            }
            catch (Exception)
            {
                Volatile.Write(ref _state, StateFailed);
            }
        }
    }
}
