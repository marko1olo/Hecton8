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
    public sealed class SteamManager : MonoBehaviour, IFrostTickable, IServiceHeartbeat, IServiceShutdown
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
            StartBackgroundInit();
        }

        private void OnDisable()
        {
            UnregisterFrostTick();
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

            GlobalRegistry.RegisterFrostTickable(this, PriorityLayer.Core);
            _registeredFrostTick = GlobalRegistry.FrostTickables.Contains(this);
        }

        private void UnregisterFrostTick()
        {
            if (!_registeredFrostTick)
                return;

            GlobalRegistry.UnregisterFrostTickable(this, PriorityLayer.Core);
            _registeredFrostTick = false;
        }

        private void StartBackgroundInit()
        {
            if (!Application.isPlaying || _shutdownRequested)
                return;

            if (Interlocked.CompareExchange(ref _state, StateBooting, StateNotStarted) != StateNotStarted)
                return;

            _initThread = new Thread(InitializeSteamworksBackground)
            {
                IsBackground = true,
                Name = "Hecton8 Steam Init"
            }; // COLD ALLOC: Thread[1] - Steamworks border initialization lane - owner: SteamManager
            _initThread.Start();
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
