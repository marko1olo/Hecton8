using System.Diagnostics;
using System.Reflection;
using System.Collections.Generic;
using Hecton8.Core.Memory;
using Hecton8.Power;
using NUnit.Framework;
using UnityEngine;
using Unity.Collections;

namespace Hecton8.Tests.Power
{
    [TestFixture]
    public class PowerGridSplitPerformanceTests
    {
        [Test]
        public void BenchmarkSplitGridIfDisconnected()
        {
            int numNodes = 10000;
            int numIslands = 50;

            var grid = new PowerGrid(numNodes, null);
            var goList = new List<GameObject>();

            // Setup Nodes
            for (int i = 0; i < numNodes; i++)
            {
                var go = new GameObject($"Node{i}");
                goList.Add(go);
                var node = go.AddComponent<PowerNode>();
                grid.AddNode(node);
            }

            // We need to manipulate the logistics graph to simulate islands
            var logisticsGraphField = typeof(PowerGrid).GetField("_logisticsGraph", BindingFlags.NonPublic | BindingFlags.Instance);
            var logisticsGraph = logisticsGraphField.GetValue(grid);

            // Set island count using reflection
            var islandCountField = logisticsGraph.GetType().GetField("_islandCount", BindingFlags.NonPublic | BindingFlags.Instance);
            islandCountField.SetValue(logisticsGraph, numIslands);

            // Mock GetNodeComponentId
            var nodeComponentIdsField = logisticsGraph.GetType().GetField("_nodeComponentIds", BindingFlags.NonPublic | BindingFlags.Instance);
            NativeArray<int> nodeComponentIds = (NativeArray<int>)nodeComponentIdsField.GetValue(logisticsGraph);
            for (int i = 0; i < numNodes; i++)
            {
                nodeComponentIds[i] = i % numIslands;
            }
            nodeComponentIdsField.SetValue(logisticsGraph, nodeComponentIds);

            // Mock GetComponentSize
            var componentSizesField = logisticsGraph.GetType().GetField("_componentSizes", BindingFlags.NonPublic | BindingFlags.Instance);
            NativeArray<int> componentSizes = (NativeArray<int>)componentSizesField.GetValue(logisticsGraph);
            for (int i = 0; i < numIslands; i++)
            {
                componentSizes[i] = numNodes / numIslands;
            }
            componentSizesField.SetValue(logisticsGraph, componentSizes);

            // Topology Summary
            var summary = new LogisticsNetworkGraph.TopologySummary
            {
                NodeCount = numNodes,
                EdgeCount = numNodes - numIslands,
                BfsVisitedCount = numNodes,
                IslandCount = numIslands,
                HasCycles = false
            };

            // Call SplitGridIfDisconnected via reflection
            var method = typeof(PowerGridManager).GetMethod("SplitGridIfDisconnected", BindingFlags.NonPublic | BindingFlags.Static);

            // Initialize PowerGridManager _allGrids list
            typeof(PowerGridManager).GetMethod("EnsureStorage", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);

            var sw = Stopwatch.StartNew();
            method.Invoke(null, new object[] { grid, summary });
            sw.Stop();

            UnityEngine.Debug.Log($"SplitGridIfDisconnected took {sw.ElapsedMilliseconds} ms for {numNodes} nodes and {numIslands} islands.");

            // Cleanup
            var disposeAll = typeof(PowerGridManager).GetMethod("DisposeAllGrids", BindingFlags.NonPublic | BindingFlags.Static);
            disposeAll.Invoke(null, null);
            grid.Dispose();

            foreach (var go in goList)
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
