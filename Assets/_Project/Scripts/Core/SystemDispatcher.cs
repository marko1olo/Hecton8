using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Priority-lane dispatcher for registry-managed <see cref="IUpdatable"/> systems.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9950)]
    public sealed class SystemDispatcher : MonoBehaviour
    {
        private const int LaneCount = 4;

        // COLD ALLOC: RegistryBucket<IUpdatable>[4] — fixed dispatcher lanes ordered by bootstrap layer — owner: SystemDispatcher
        private static readonly RegistryBucket<IUpdatable>[] _priorityLanes =
        {
            new RegistryBucket<IUpdatable>(16),
            new RegistryBucket<IUpdatable>(32),
            new RegistryBucket<IUpdatable>(32),
            new RegistryBucket<IUpdatable>(16),
        };

        private static SystemDispatcher _instance;

        /// <summary>
        /// Live dispatcher instance.
        /// </summary>
        public static SystemDispatcher Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
            ClearAllLanes();
        }

        /// <summary>
        /// Returns the registry lane for a fixed priority layer.
        /// </summary>
        /// <param name="layer">Priority lane.</param>
        /// <returns>Dense lane bucket.</returns>
        public static RegistryBucket<IUpdatable> GetLane(PriorityLayer layer)
        {
            return _priorityLanes[GetLaneIndex(layer)];
        }

        /// <summary>
        /// Ensures a live runtime dispatcher exists.
        /// </summary>
        /// <returns>Live dispatcher instance.</returns>
        public static SystemDispatcher EnsureRuntimeInstance()
        {
            if (_instance != null)
                return _instance;

            GameObject runtimeRoot = new GameObject("[SystemDispatcher]");
            SystemDispatcher dispatcher = runtimeRoot.AddComponent<SystemDispatcher>();
            return dispatcher;
        }

        /// <summary>
        /// Registers an update owner into a fixed priority lane.
        /// </summary>
        /// <param name="item">Update owner.</param>
        /// <param name="layer">Priority lane.</param>
        internal static void Register(IUpdatable item, PriorityLayer layer)
        {
            if (item == null)
                return;

            GetLane(layer).Register(item);
        }

        /// <summary>
        /// Unregisters an update owner from a fixed priority lane.
        /// </summary>
        /// <param name="item">Update owner.</param>
        /// <param name="layer">Priority lane.</param>
        internal static void Unregister(IUpdatable item, PriorityLayer layer)
        {
            if (item == null)
                return;

            GetLane(layer).Unregister(item);
        }

        /// <summary>
        /// Clears every dispatcher lane.
        /// </summary>
        public static void ClearAllLanes()
        {
            for (int i = 0; i < LaneCount; i++)
                _priorityLanes[i].Clear();
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
            if (_instance == this)
                _instance = null;
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;

            for (int laneIndex = 0; laneIndex < LaneCount; laneIndex++)
            {
                RegistryBucket<IUpdatable> lane = _priorityLanes[laneIndex];
                IUpdatable[] rawArray = lane.RawArray;
                int count = lane.Count;

                for (int itemIndex = 0; itemIndex < count; itemIndex++)
                {
                    rawArray[itemIndex].Tick(deltaTime);
                }
            }
        }

        private static int GetLaneIndex(PriorityLayer layer)
        {
            switch (layer)
            {
                case PriorityLayer.Core:
                    return 0;
                case PriorityLayer.Environment:
                    return 1;
                case PriorityLayer.Player:
                    return 2;
                case PriorityLayer.UI:
                    return 3;
                default:
                    return 0;
            }
        }
    }
}
