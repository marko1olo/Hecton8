using CandiceAIforGames.AI.Pathfinding;
using System;
using System.Collections.Generic;
using UnityEngine;
namespace CandiceAIforGames.AI
{
    public class CandiceAIManager : MonoBehaviour
    {
        private const int MaxQueuedPathResults = 128;
        private const int MaxQueuedRegistrations = 128;
        private const int MaxRegisteredAgents = 128;

        [SerializeField]
        private bool drawGridGizmos = true;
        [SerializeField]
        private bool drawAllAgentPaths = true;
        //public static CandiceAIManager getInstance;
        private static CandiceAIManager instance;
        // COLD ALLOC: Queue<PathResult>(128) - bounded async path result handoff - owner: CandiceAIManager
        private Queue<PathResult> results = new Queue<PathResult>(MaxQueuedPathResults);//Data strucure containing a collection of all paths requested by all AI Agents/Controllers
        private CandicePathFinding pathFinding;//Pathfinding module that does the actual calculations to find a path.
        public CandiceGrid grid;//The grid that contains all the nodes


        // COLD ALLOC: Queue<RegistrationRequest>(128) - bounded controller registration handoff - owner: CandiceAIManager
        Queue<RegistrationRequest> registrationQueue = new Queue<RegistrationRequest>(MaxQueuedRegistrations);

        // COLD ALLOC: Dictionary<int,GameObject>(128) - bounded registered agent table - owner: CandiceAIManager
        public Dictionary<int, GameObject> agents = new Dictionary<int, GameObject>(MaxRegisteredAgents);
        public int agentCount = 0;

        public static string[] arrNodeTypes = { "Selector", "Sequence", "Inverter", "Action" };
        public static string[] arrFunctions = { "None", "MoveTo", "LookAt", "Attack", "EnemyDetected" };//This is managed by the Candice Behavior Designer.
        public const int NODE_TYPE_SELECTOR = 0;
        public const int NODE_TYPE_SEQUENCE = 1;
        public const int NODE_TYPE_INVERTER = 2;
        public const int NODE_TYPE_ACTION = 3;

        public bool DrawGridGizmos { get => drawGridGizmos; set => drawGridGizmos = value; }
        public bool DrawAllAgentPaths { get => drawAllAgentPaths; set => drawAllAgentPaths = value; }

        #region Events
        public event Action<GameObject, GameObject> OnPlayerDetected = delegate { };
        public event Action<GameObject> OnPlayerHealthLow = delegate { };
        public event Action<CandiceAIController> OnDestinationReached = delegate { };
        public event Action<GameObject> OnCharacterDead = delegate { };

        public void PlayerDetected(GameObject source, GameObject player)
        {
            OnPlayerDetected(source, player);
        }
        public void CharacterDead(GameObject go)
        {
            OnCharacterDead(go);
        }
        public void PlayerHealthLow(GameObject player)
        {
            OnPlayerHealthLow(player);
        }
        public void DestinationReached(CandiceAIController agent)
        {
            OnDestinationReached(agent);
        }
        #endregion

        // Start is called before the first frame update
        void Start()
        {
            //Process all agent pathfinding requests
            lock (results)
            {
                int itemsInQueue = results.Count;
                for (int i = 0; i < itemsInQueue; i++)
                {
                    PathResult result = results.Dequeue();
                    if (result.callbackWithLength != null)
                    {
                        result.callbackWithLength(result.path, result.pathLength, result.success);
                    }
                    else if (result.callback != null)
                    {
                        result.callback(result.path, result.success);
                    }
                }
            }
            //Process all agent registration requests
            lock (registrationQueue)
            {
                int itemsInQueue = registrationQueue.Count;
                for (int i = 0; i < itemsInQueue; i++)
                {
                    RegistrationRequest rr = registrationQueue.Dequeue();
                    bool isRegistered = false;
                    int assignedAgentId = agentCount;
                    if (agentCount < MaxRegisteredAgents && rr.agent != null)
                    {
                        agentCount++;
                        assignedAgentId = agentCount;
                        agents.Add(assignedAgentId, rr.agent);
                        isRegistered = true;
                    }
                    if (rr.callback != null)
                    {
                        rr.callback(isRegistered, assignedAgentId);
                    }
                }
            }
        }

        // Update is called once per frame
        void Update()
        {
            //Process all agent registration requests
            lock (registrationQueue)
            {
                int itemsInQueue = registrationQueue.Count;
                for (int i = 0; i < itemsInQueue; i++)
                {
                    RegistrationRequest rr = registrationQueue.Dequeue();
                    bool isRegistered = false;
                    int assignedAgentId = agentCount;
                    if (agentCount < MaxRegisteredAgents && rr.agent != null)
                    {
                        agentCount++;
                        assignedAgentId = agentCount;
                        agents.Add(assignedAgentId, rr.agent);
                        isRegistered = true;
                    }
                    if (rr.callback != null)
                    {
                        rr.callback(isRegistered, assignedAgentId);
                    }
                }
            }
            //Process all agent pathfinding requests
            lock (results)
            {
                int itemsInQueue = results.Count;
                for (int i = 0; i < itemsInQueue; i++)
                {
                    PathResult result = results.Dequeue();
                    if (result.callbackWithLength != null)
                    {
                        result.callbackWithLength(result.path, result.pathLength, result.success);
                    }
                    else if (result.callback != null)
                    {
                        result.callback(result.path, result.success);
                    }
                }
            }
            
        }
        private void Awake()
        {
            Initialise();
        }
        public static CandiceAIManager getInstance()
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<CandiceAIManager>();
            }
            return instance;
        }
        private void Initialise()
        {
            //CandiceConfig.enableDebug = enableDebug;
            instance = this;
            if (grid != null)
            {
                pathFinding = new CandicePathFinding(grid);
            }
            else
            {
                Debug.LogError("Cannot initialise Candice Pathfinding. Please make sure to set the Grid variable.");
            }

        }
        public bool IsPointWalkable(Vector3 point)
        {
            return grid != null && grid.isWalkable(point);
        }

        public void RegisterAgent(GameObject agent, Action<bool, int> callback)
        {
            if (registrationQueue.Count >= MaxQueuedRegistrations)
            {
                if (callback != null)
                {
                    callback(false, agentCount);
                }
                return;
            }

            registrationQueue.Enqueue(new RegistrationRequest(agent, callback));
        }

        #region A* Pathfinding
        //This method is called by the AI agents in order to receive a path to their goal, using the Pathfinding module.
        public void RequestASTARPath(PathRequest request)
        {
            if (pathFinding == null)
            {
                if (request.callback != null)
                {
                    request.callback(null, false);
                }
                else if (request.callbackWithLength != null)
                {
                    request.callbackWithLength(null, 0, false);
                }
                return;
            }

            pathFinding.FindASTARPath(request, null);
        }
        /*public static void RequestBFSPath(Tile tile, Action<Stack<Tile>> callback)
        {
            ThreadStart threadStart = delegate
            {
                getInstance().pathFinding.FindBFSPath(tile, callback);
            };
            threadStart.Invoke();
        }*/
        public void FinishedProcessingPath(PathResult result)
        {
            lock (results)
            {
                if (results.Count >= MaxQueuedPathResults)
                {
                    return;
                }

                //Add the result to the queue.
                results.Enqueue(result);
            }

        }
        #endregion

        private void OnDrawGizmos()
        {

            if (DrawGridGizmos)
                grid.DrawGrid();
        }
    }
    public struct PathResult
    {
        public Vector3[] path;
        public int pathLength;
        public bool success;
        public Action<Vector3[], bool> callback;
        public Action<Vector3[], int, bool> callbackWithLength;

        public PathResult(Vector3[] path, bool success, Action<Vector3[], bool> callback)
        {
            this.path = path;
            this.pathLength = path == null ? 0 : path.Length;
            this.success = success;
            this.callback = callback;
            this.callbackWithLength = null;
        }

        public PathResult(Vector3[] path, int pathLength, bool success, Action<Vector3[], bool> callback, Action<Vector3[], int, bool> callbackWithLength)
        {
            this.path = path;
            this.pathLength = pathLength;
            this.success = success;
            this.callback = callback;
            this.callbackWithLength = callbackWithLength;
        }
    }
    public struct PathRequest
    {
        public Vector3 pathStart;
        public Vector3 pathEnd;
        public Action<Vector3[], bool> callback;
        public Action<Vector3[], int, bool> callbackWithLength;

        public PathRequest(Vector3 _start, Vector3 _end, Action<Vector3[], bool> _callback)
        {
            pathStart = _start;
            pathEnd = _end;
            callback = _callback;
            callbackWithLength = null;
        }

        public PathRequest(Vector3 _start, Vector3 _end, Action<Vector3[], int, bool> _callback)
        {
            pathStart = _start;
            pathEnd = _end;
            callback = null;
            callbackWithLength = _callback;
        }
    }

    public struct RegistrationRequest
    {
        public GameObject agent;
        public Action<bool, int> callback;

        public RegistrationRequest(GameObject _agent, Action<bool, int> _callback)
        {
            agent = _agent;
            callback = _callback;
        }
    }
}

