using System.Collections.Generic;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Optimization
{
    /// <summary>
    /// Main-thread dispatcher that throttles asset load grants by tier and active pressure.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-8011)]
    public sealed class AssetLoadDispatcher : MonoBehaviour, ITickable
    {
        private const int Tier01Slots = 8;
        private const int Tier2Slots = 6;
        private const int Tier34Slots = 4;
        private const int Tier34CriticalSlots = 1;
        private const int Tier56Slots = 2;
        private const int Tier56WarningSlots = 0;
        private const int StarvationFrameThreshold = 60;

        private static AssetLoadDispatcher _instance;

        [Header("Dispatch Budget")]
        [Tooltip("Main-thread dispatch budget in milliseconds per frame.")]
        [SerializeField] private float dispatchBudgetMilliseconds = 2f;

        [Tooltip("Maximum ready tickets retained for backend consumers.")]
        [SerializeField] private int maxReadyTicketCount = 32;

        private bool _registeredTick;
        private int _nextRequestId = 1;

        // COLD ALLOC: List<AssetDispatchRequest>[128] - queued load requests - owner: AssetLoadDispatcher
        private readonly List<AssetDispatchRequest> _queuedRequests = new List<AssetDispatchRequest>(128);
        // COLD ALLOC: List<AssetDispatchTicket>[32] - ready-to-dispatch tickets - owner: AssetLoadDispatcher
        private readonly List<AssetDispatchTicket> _readyTickets = new List<AssetDispatchTicket>(32);
        // COLD ALLOC: List<AssetDispatchRequest>[64] - active in-flight requests - owner: AssetLoadDispatcher
        private readonly List<AssetDispatchRequest> _inflightRequests = new List<AssetDispatchRequest>(64);
        // COLD ALLOC: int[4] - tier-band inflight counters - owner: AssetLoadDispatcher
        private readonly int[] _inflightCounts = new int[4];

        internal static AssetLoadDispatcher Instance => _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        private void OnEnable()
        {
            TryRegister();
        }

        private void Start()
        {
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
        }

        private void OnDestroy()
        {
            TryUnregister();
            _queuedRequests.Clear();
            _readyTickets.Clear();
            _inflightRequests.Clear();

            for (int i = 0; i < _inflightCounts.Length; i++)
                _inflightCounts[i] = 0;

            if (_instance == this)
                _instance = null;
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            AgeQueuedRequests();
            DispatchWithinBudget();
        }

        internal bool Enqueue(uint assetKey, AssetPriorityTier priority, bool isDistantHlod, out int requestId)
        {
            requestId = 0;

            for (int i = 0; i < _queuedRequests.Count; i++)
            {
                if (_queuedRequests[i].AssetKey != assetKey)
                    continue;

                requestId = _queuedRequests[i].RequestId;
                return true;
            }

            for (int i = 0; i < _inflightRequests.Count; i++)
            {
                if (_inflightRequests[i].AssetKey != assetKey)
                    continue;

                requestId = _inflightRequests[i].RequestId;
                return true;
            }

            if (_readyTickets.Count >= maxReadyTicketCount)
                return false;

            requestId = _nextRequestId++;
            _queuedRequests.Add(new AssetDispatchRequest
            {
                RequestId = requestId,
                AssetKey = assetKey,
                Priority = priority,
                IsDistantHlod = isDistantHlod,
                AgeFrames = 0
            });
            return true;
        }

        internal bool TryDequeueReadyTicket(out AssetDispatchTicket ticket)
        {
            if (_readyTickets.Count == 0)
            {
                ticket = default;
                return false;
            }

            int lastIndex = _readyTickets.Count - 1;
            ticket = _readyTickets[lastIndex];
            _readyTickets.RemoveAt(lastIndex);
            return true;
        }

        internal bool Complete(int requestId, bool success)
        {
            for (int i = 0; i < _inflightRequests.Count; i++)
            {
                AssetDispatchRequest request = _inflightRequests[i];
                if (request.RequestId != requestId)
                    continue;

                int band = ResolveBand(request.Priority);
                if (_inflightCounts[band] > 0)
                    _inflightCounts[band]--;

                RemoveAtSwapBack(_inflightRequests, i);
                return true;
            }

            return false;
        }

        internal void CancelByAssetKey(uint assetKey)
        {
            for (int i = _queuedRequests.Count - 1; i >= 0; i--)
            {
                if (_queuedRequests[i].AssetKey == assetKey)
                    RemoveAtSwapBack(_queuedRequests, i);
            }

            for (int i = _readyTickets.Count - 1; i >= 0; i--)
            {
                if (_readyTickets[i].AssetKey == assetKey)
                    RemoveAtSwapBack(_readyTickets, i);
            }

            for (int i = _inflightRequests.Count - 1; i >= 0; i--)
            {
                if (_inflightRequests[i].AssetKey != assetKey)
                    continue;

                int band = ResolveBand(_inflightRequests[i].Priority);
                if (_inflightCounts[band] > 0)
                    _inflightCounts[band]--;

                RemoveAtSwapBack(_inflightRequests, i);
            }
        }

        private void TryRegister()
        {
            if (_registeredTick)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            tickManager.Register((ITickable)this);
            _registeredTick = true;
        }

        private void TryUnregister()
        {
            if (!_registeredTick)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager != null)
                tickManager.Unregister((ITickable)this);

            _registeredTick = false;
        }

        private void AgeQueuedRequests()
        {
            for (int i = 0; i < _queuedRequests.Count; i++)
            {
                AssetDispatchRequest request = _queuedRequests[i];
                request.AgeFrames++;
                _queuedRequests[i] = request;
            }
        }

        private void DispatchWithinBudget()
        {
            if (_queuedRequests.Count == 0 || _readyTickets.Count >= maxReadyTicketCount)
                return;

            float dispatchStart = Time.realtimeSinceStartup;
            while (_queuedRequests.Count > 0 && _readyTickets.Count < maxReadyTicketCount)
            {
                float elapsedMilliseconds = (Time.realtimeSinceStartup - dispatchStart) * 1000f;
                if (elapsedMilliseconds >= dispatchBudgetMilliseconds)
                    break;

                int requestIndex = FindNextDispatchableRequestIndex();
                if (requestIndex < 0)
                    break;

                AssetDispatchRequest request = _queuedRequests[requestIndex];
                RemoveAtSwapBack(_queuedRequests, requestIndex);

                _readyTickets.Add(new AssetDispatchTicket
                {
                    RequestId = request.RequestId,
                    AssetKey = request.AssetKey,
                    Priority = request.Priority,
                    IsDistantHlod = request.IsDistantHlod
                });

                _inflightRequests.Add(request);
                _inflightCounts[ResolveBand(request.Priority)]++;
            }
        }

        private int FindNextDispatchableRequestIndex()
        {
            int bestIndex = -1;
            int bestPriority = int.MaxValue;
            int bestAge = -1;

            for (int i = 0; i < _queuedRequests.Count; i++)
            {
                AssetDispatchRequest request = _queuedRequests[i];
                int band = ResolveBand(request.Priority);
                int allowedLoads = ResolveAllowedConcurrentLoads(request.Priority);
                if (_inflightCounts[band] >= allowedLoads)
                    continue;

                int effectivePriority = (int)(byte)request.Priority;
                if (request.AgeFrames > StarvationFrameThreshold && effectivePriority > 0)
                    effectivePriority--;

                if (bestIndex < 0 || effectivePriority < bestPriority ||
                    (effectivePriority == bestPriority && request.AgeFrames > bestAge))
                {
                    bestIndex = i;
                    bestPriority = effectivePriority;
                    bestAge = request.AgeFrames;
                }
            }

            return bestIndex;
        }

        private static int ResolveBand(AssetPriorityTier priority)
        {
            byte priorityByte = (byte)priority;
            if (priorityByte <= (byte)AssetPriorityTier.Tier1Equipped)
                return 0;
            if (priority == AssetPriorityTier.Tier2Proximity)
                return 1;
            if (priorityByte <= (byte)AssetPriorityTier.Tier4MidRange)
                return 2;

            return 3;
        }

        private static void RemoveAtSwapBack<T>(List<T> list, int index)
        {
            int lastIndex = list.Count - 1;
            list[index] = list[lastIndex];
            list.RemoveAt(lastIndex);
        }

        private int ResolveAllowedConcurrentLoads(AssetPriorityTier priority)
        {
            int band = ResolveBand(priority);
            VRAMPressureMonitor pressureMonitor = VRAMPressureMonitor.Instance;
            float ramPressure = pressureMonitor != null ? pressureMonitor.RamPressureFactor : 0f;

            switch (band)
            {
                case 0:
                    return Tier01Slots;

                case 1:
                    return Tier2Slots;

                case 2:
                    return ramPressure > 0.85f ? Tier34CriticalSlots : Tier34Slots;

                default:
                    return ramPressure > 0.75f ? Tier56WarningSlots : Tier56Slots;
            }
        }
    }
}
