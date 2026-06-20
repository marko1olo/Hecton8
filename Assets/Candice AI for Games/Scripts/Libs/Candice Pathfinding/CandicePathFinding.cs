using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using CandiceAIforGames.data;

namespace CandiceAIforGames.AI.Pathfinding
{
    public class CandicePathFinding
    {
        CandiceGrid grid;
        private readonly CandiceHeap<Node> openSet;
        private readonly HashSet<Node> closedSet;
        // COLD ALLOC: GameObject[0] - empty Candice tile fallback for legacy BFS setup - owner: CandicePathFinding
        private static readonly GameObject[] EmptyTiles = new GameObject[0];
        // COLD ALLOC: Node[8] - fixed 8-neighbour A* scratch buffer - owner: CandicePathFinding
        private readonly Node[] neighbourScratch = new Node[8];
        private readonly List<Node> pathScratch;
        private readonly List<Vector3> waypointScratch;
        private readonly Vector3[] waypointBuffer;

        public CandicePathFinding(CandiceGrid _grid)
        {
            grid = _grid;
            // COLD ALLOC: CandiceHeap<Node>[grid.MaxSize] - reusable A* open-set storage - owner: CandicePathFinding
            openSet = new CandiceHeap<Node>(grid.MaxSize);
            // COLD ALLOC: HashSet<Node>[grid.MaxSize] - reusable A* closed-set storage - owner: CandicePathFinding
            closedSet = new HashSet<Node>(grid.MaxSize);
            // COLD ALLOC: List<Node>[grid.MaxSize] - reusable A* backtrack scratch - owner: CandicePathFinding
            pathScratch = new List<Node>(grid.MaxSize);
            // COLD ALLOC: List<Vector3>[grid.MaxSize] - reusable A* waypoint scratch - owner: CandicePathFinding
            waypointScratch = new List<Vector3>(grid.MaxSize);
            // COLD ALLOC: Vector3[grid.MaxSize] - reusable A* waypoint output buffer - owner: CandicePathFinding
            waypointBuffer = new Vector3[grid.MaxSize];
        }
        /// <summary>
        /// Computes the adjacency list for each CandiceTile in the scene, based on the specified jump height and target tile.
        /// </summary>
        /// <param name="jumpHeight">The maximum height that the player can jump when traversing between tiles.</param>
        /// <param name="target">The target tile that the player is trying to reach.</param>
        /// <remarks>
        /// This function finds all CandiceTile game objects in the scene that have the "CandiceTile" tag, and calls the FindNeighbors method on each CandiceTile component to compute its adjacency list. The adjacency list is a list of other CandiceTile components that are adjacent to the current tile, meaning that they can be reached by jumping from the current tile with a height of no more than jumpHeight. The adjacency list is stored as a public property of the CandiceTile component, and can be accessed by other scripts to implement pathfinding or other AI behaviors. If no CandiceTile game objects are found in the scene, an empty array is created and used instead. 
        /// </remarks>
        public void ComputeAdjacencyList(float jumpHeight, CandiceTile target)
        {
            var tiles = CandiceTile.AllTiles;
            int tileCount = tiles.Count;
            // Compute the adjacency list for each CandiceTile in the scene
            for (int i = 0; i < tileCount; i++)
            {
                CandiceTile t = tiles[i];
                if (t == null)
                {
                    continue;
                }

                // Compute the adjacency list for the current CandiceTile component
                t.FindNeighbors(jumpHeight, target);
            }
        }

        /// <summary>
        /// Finds all selectable tiles within a given maximum move distance, starting from a given tile. Make sure to call ComputeAdjacencyList() first.
        /// </summary>
        /// <param name="currentTile">The tile from which to start the search for selectable tiles.</param>
        /// <param name="maxMovePoints">The maximum distance that can be traveled from the starting tile.</param>
        /// <param name="callback">The function to call when the selectable tiles have been found.</param>
        /// <remarks>
        /// This function uses the Breadth-First Search algorithm to find all tiles that can be reached
        /// from the starting tile within a given maximum move distance. The resulting list of selectable
        /// tiles is returned to the specified callback function.
        /// </remarks>
        public void FindSelectableTiles(CandiceTile currentTile, float maxMovePoints, Action<List<CandiceTile>> callback)
        {
            // Create an empty list of selectable tiles
            List<CandiceTile> selectableTiles = new List<CandiceTile>();

            // Create a queue for BFS traversal
            Queue<CandiceTile> process = new Queue<CandiceTile>();

            // Enqueue the current tile and mark it as visited
            process.Enqueue(currentTile);
            currentTile.visited = true;

            // BFS traversal to find all selectable tiles
            while (process.Count > 0)
            {
                CandiceTile t = process.Dequeue();

                // Add the tile to the list of selectable tiles and mark it as selectable
                selectableTiles.Add(t);
                t.selectable = true;

                // If the distance from the current tile is less than maxMovePoints, add all its adjacent tiles to the queue
                if (t.distance < maxMovePoints)
                {
                    foreach (CandiceTile adjacentTile in t.adjacencyList)
                    {
                        if (!adjacentTile.visited)
                        {
                            adjacentTile.parent = t;
                            adjacentTile.visited = true;
                            adjacentTile.distance = 1 + t.distance;
                            process.Enqueue(adjacentTile);
                        }
                    }
                }
            }

            // Call the callback function with the list of selectable tiles
            callback(selectableTiles);
        }
        /// <summary>
        /// Finds the path to a target tile using a Breadth-First Search algorithm.
        /// </summary>
        /// <param name="targetTile">The target tile to find the path to.</param>
        /// <param name="callback">The method to call with the resulting path.</param>
        public void FindBFSPath(CandiceTile targetTile, Action<Stack<CandiceTile>> callback)
        {
            // Create a new stack to hold the path.
            Stack<CandiceTile> path = new Stack<CandiceTile>();

            // Mark the target tile as the destination of the path.
            targetTile.target = true;

            // Traverse the tree from the target tile to the starting tile, adding each tile to the path stack.
            CandiceTile nextTile = targetTile;
            while (nextTile != null)
            {
                path.Push(nextTile);
                nextTile = nextTile.parent;
            }

            // Invoke the callback method with the resulting path stack.
            callback(path);
        }
        /// <summary>
        /// Calculates the shortest path between two points using the A* algorithm. Calls the specified callback function with the result.
        /// </summary>
        /// <param name="request">The pathfinding request, which contains information about the start point, end point, and callback function.</param>
        /// <param name="callback">The callback function to call with the result of the pathfinding operation.</param>
        /// <remarks>
        /// The A* algorithm uses a heuristic function to estimate the cost of the remaining path from a node to the target node. 
        /// The algorithm maintains two sets of nodes: the open set, which contains nodes that have been discovered but not yet explored, 
        /// and the closed set, which contains nodes that have already been explored. 
        /// The algorithm selects the node with the lowest fCost (the sum of the gCost and hCost) from the open set, and adds it to the closed set. 
        /// It then checks the neighbours of the current node, and for each neighbour that is walkable and not in the closed set, 
        /// it calculates the cost of moving from the current node to the neighbour node, and updates the neighbour node's gCost, hCost, 
        /// and parent attributes if it's cheaper to get to the neighbour node through the current node. 
        /// If the neighbour node is not in the open set, it is added to the open set. 
        /// If the neighbour node is already in the open set, its fCost is updated to reflect the new cost. 
        /// If the target node is found, the algorithm stops, and the function retraces the path by following the parent links back to the start node.
        /// </remarks>
        public void FindASTARPath(PathRequest request, Action<PathResult> callback)
        {
            //Initializes variables
            Vector3[] waypoints = waypointBuffer;
            int waypointCount = 0;
            bool pathSuccess = false;
            //Finds the start and end node from the given world points
            Node startNode = grid.NodeFromWorldPoint(request.pathStart);
            Node targetNode = grid.NodeFromWorldPoint(request.pathEnd);

            //Checks if start and end nodes are walkable
            if (startNode.walkable && targetNode.walkable)
            {
                //Initializes open and closed sets
                openSet.Clear();
                closedSet.Clear();
                openSet.Add(startNode);

                //Starts A* pathfinding algorithm
                while (openSet.Count > 0)
                {
                    //Takes the node with the lowest fCost from the open set
                    Node currentNode = openSet.RemoveFirst();
                    //Adds current node to the closed set
                    closedSet.Add(currentNode);
                    //If the path has been found
                    if (currentNode == targetNode)
                    {
                        //Sets path success flag to true and breaks the loop
                        pathSuccess = true;
                        break;
                    }
                    //Checks the neighbours of the current node
                    int neighbourCount = grid.GetNeighboursNonAlloc(currentNode, neighbourScratch);
                    for (int i = 0; i < neighbourCount; i++)
                    {
                        Node neighbour = neighbourScratch[i];
                        //Ignores non-walkable nodes or nodes already in the closed set
                        if (!neighbour.walkable || closedSet.Contains(neighbour))
                        {
                            continue;
                        }
                        //Calculates the new cost to get to the neighbour node
                        int newMovementCostToNeighbour = currentNode.gCost + GetDistance(currentNode, neighbour) + neighbour.movementPenalty;
                        //If it's cheaper to get to the neighbour node through the current node, or if the neighbour node is not in the open set
                        if (newMovementCostToNeighbour < neighbour.gCost || !openSet.Contains(neighbour))
                        {
                            //Updates the neighbour node
                            neighbour.gCost = newMovementCostToNeighbour;
                            neighbour.hCost = GetDistance(neighbour, targetNode);
                            neighbour.parent = currentNode;
                            //If the neighbour node is not in the open set, adds it
                            if (!openSet.Contains(neighbour))
                            {
                                openSet.Add(neighbour);
                            }
                            //Otherwise, updates the neighbour node in the open set
                            else
                            {
                                openSet.UpdateItem(neighbour);
                            }
                        }
                    }
                }
            }
            //Retraces the path and sets path success flag to true if path has been found
            if (pathSuccess)
            {
                waypointCount = RetracePath(startNode, targetNode);
                pathSuccess = waypointCount > 0;
            }
            // The default manager path dispatches immediately so the agent can copy from the shared waypoint buffer before the next request.
            if (callback == null)
            {
                if (request.callbackWithLength != null)
                {
                    request.callbackWithLength(waypoints, waypointCount, pathSuccess);
                }
                else if (request.callback != null)
                {
                    request.callback(waypoints, pathSuccess);
                }
                return;
            }

            PathResult result;
            result.path = waypoints;
            result.pathLength = waypointCount;
            result.success = pathSuccess;
            result.callback = request.callback;
            result.callbackWithLength = request.callbackWithLength;
            callback(result);
        }

        /// <summary>
        /// Backtracks through the path from endNode to startNode and returns an array of Vector3 waypoints
        /// </summary>
        /// <param name="startNode">The starting node of the path</param>
        /// <param name="endNode">The ending node of the path</param>
        /// <returns>An array of Vector3 waypoints from the start to end of the path</returns>
        int RetracePath(Node startNode, Node endNode)
        {
            // Initialize an empty list to store the nodes in the path
            pathScratch.Clear();

            // Set the current node to the end node
            Node currentNode = endNode;

            // Loop through the nodes from the end node to the start node
            while (currentNode != startNode)
            {
                // Add the current node to the path list
                pathScratch.Add(currentNode);

                // Set the current node to its parent node
                currentNode = currentNode.parent;
            }

            // Convert and simplify the path to an array of Vector3 waypoints
            int waypointCount = ConvertAndSimplifyPath(pathScratch);

            // Reverse the order of the waypoints to create a path from the start to end
            Array.Reverse(waypointBuffer, 0, waypointCount);

            // Return the array of waypoints
            return waypointCount;
        }
        int ConvertPath(List<Node> path)
        {
            waypointScratch.Clear();

            for (int i = 1; i < path.Count; i++)
            {
                waypointScratch.Add(path[i].worldPosition);
            }
            return CopyWaypointScratchToBuffer();
        }


        /// <summary>
        /// Converts a path of nodes to an array of waypoints (as Vector3).
        /// The path is simplified by removing waypoints that are on the same line (and same direction) as the previous waypoint.
        /// </summary>
        /// <param name="path">A list of nodes representing a path.</param>
        /// <returns>An array of Vector3 representing a simplified version of the path.</returns>
        int ConvertAndSimplifyPath(List<Node> path)
        {
            // Create an empty list to hold the waypoints
            waypointScratch.Clear();

            // Keep track of the previous direction to determine when to add a new waypoint
            Vector2 directionOld = Vector2.zero;

            // Loop over the nodes in the path (starting from the second node)
            for (int i = 1; i < path.Count; i++)
            {
                // Calculate the direction between the current node and the previous node
                Vector2 directionNew = new Vector2(path[i - 1].gridX - path[i].gridX, path[i - 1].gridY - path[i].gridY);

                // If the direction has changed since the last node, add the current node's position as a new waypoint
                if (directionNew != directionOld)
                {
                    waypointScratch.Add(path[i].worldPosition);
                }

                // Update the previous direction
                directionOld = directionNew;
            }

            // Copy the simplified waypoint list to the reusable output buffer.
            return CopyWaypointScratchToBuffer();
        }

        private int CopyWaypointScratchToBuffer()
        {
            int count = Mathf.Min(waypointScratch.Count, waypointBuffer.Length);
            for (int i = 0; i < count; i++)
            {
                waypointBuffer[i] = waypointScratch[i];
            }

            return count;
        }
        /// <summary>
        /// Calculates the heuristic cost between two nodes using Manhattan distance.
        /// </summary>
        /// <param name="nodeA">The starting node.</param>
        /// <param name="nodeB">The ending node.</param>
        /// <returns>The heuristic cost between the two nodes.</returns>
        int GetDistance(Node nodeA, Node nodeB)
        {
            int distX = Mathf.Abs(nodeA.gridX - nodeB.gridX);
            int distY = Mathf.Abs(nodeA.gridY - nodeB.gridY);

            // The heuristic cost is based on the difference between the nodes' x and y positions on a two-dimensional grid.
            // The cost is calculated as 10 for every horizontal or vertical step and 14 for every diagonal step.
            if (distX > distY)
            {
                return 14 * distY + 10 * (distX - distY);
            }
            else
            {
                return 14 * distX + 10 * (distY - distX);
            }
        }

    }

}
