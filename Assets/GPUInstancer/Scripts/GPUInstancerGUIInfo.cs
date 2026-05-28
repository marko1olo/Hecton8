using System.Collections.Generic;
using UnityEngine;

namespace GPUInstancer
{
    public class GPUInstancerGUIInfo : MonoBehaviour
    {
        public bool showRenderedAmount;
        public bool showPrototypesSeparate;
        public bool showPrefabManagers = true;
        public bool showDetailManagers = true;
        public bool showTreeManagers = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // COLD ALLOC: List<GPUInstancerRuntimeData>[1] - debug OnGUI single-row scratch - owner: GPUInstancerGUIInfo
        private static readonly List<GPUInstancerRuntimeData> singlesList = new List<GPUInstancerRuntimeData>(1) { null };
        // COLD ALLOC: int[8] - debug OnGUI rendered LOD count scratch - owner: GPUInstancerGUIInfo
        private static readonly int[] lodCountScratch = new int[8];

        private void OnGUI()
        {
            if (GPUInstancerManager.activeManagerList != null)
            {
                if (GPUInstancerManager.showRenderedAmount != showRenderedAmount)
                    GPUInstancerManager.showRenderedAmount = showRenderedAmount;

                int startPos = 0;
                int enabledCount = 0;
                string name = "";

                Color oldColor = GUI.color;
                GUI.color = Color.red;

                if (showRenderedAmount)
                {
                    GUI.Label(new Rect(10, Screen.height - 30, 700, 30),
                    "SHOWING RENDERED AMOUNTS! FRAME RATE WILL DROP!");
                    startPos += 30;
                }

                for (int i = 0; i < GPUInstancerManager.activeManagerList.Count; i++)
                {
                    GPUInstancerManager manager = GPUInstancerManager.activeManagerList[i];
                    enabledCount = 0;
                    if (manager is GPUInstancerPrefabManager)
                    {
                        if (!showPrefabManagers)
                            continue;
                        name = "prefab";
                        enabledCount = ((GPUInstancerPrefabManager)manager).GetEnabledPrefabCount();
                    }
                    else if (manager is GPUInstancerTreeManager)
                    {
                        if (!showTreeManagers)
                            continue;
                        name = "tree";
                    }
                    else if (manager is GPUInstancerDetailManager)
                    {
                        if (!showDetailManagers)
                            continue;
                        name = "detail";
                    }
                if (showPrototypesSeparate)
                {
                    for (int rdIndex = 0; manager.runtimeDataList != null && rdIndex < manager.runtimeDataList.Count; rdIndex++)
                    {
                        GPUInstancerRuntimeData rd = manager.runtimeDataList[rdIndex];
                        singlesList[0] = rd;
                        DebugOnManagerGUI(rd.prototype != null ? rd.prototype.name : "null", singlesList, showRenderedAmount, startPos, enabledCount);
                        startPos += GPUInstancerConstants.DEBUG_INFO_SIZE;
                    }
                    }
                    else
                    {
                        DebugOnManagerGUI(name, manager.runtimeDataList, showRenderedAmount, startPos, enabledCount);
                        startPos += GPUInstancerConstants.DEBUG_INFO_SIZE;
                    }
                }

                GUI.color = oldColor;
            }
        }

        private void OnDisable()
        {
            if (showRenderedAmount)
                GPUInstancerManager.showRenderedAmount = false;
        }

        private static void DebugOnManagerGUI(string name, List<GPUInstancerRuntimeData> runtimeDataList, bool showRenderedAmount, int startPos, int enabledCount)
        {
            if (runtimeDataList == null || runtimeDataList.Count == 0)
            {
                GUI.Label(new Rect(10, Screen.height - startPos - 25, 700, 30),
                    "There are no " + name + " instance prototypes to render!");
                return;
            }

            int totalInstanceCount = 0;
            for (int i = 0; i < runtimeDataList.Count; i++)
            {
                totalInstanceCount += runtimeDataList[i].instanceCount;
            }

            // show instance counts
            GUI.Label(new Rect(10, Screen.height - startPos - 45, 700, 30),
                "Total " + name + " prototype count: " + runtimeDataList.Count);
            GUI.Label(new Rect(10, Screen.height - startPos - 25, 700, 30),
                "Total " + name + " instance count: " + totalInstanceCount);

            if (showRenderedAmount)
            {
                GUI.Label(new Rect(10, Screen.height - startPos - 65, 700, 30),
                    "Rendered " + name + " instance count: " + GetRenderedAmountsGUITextFromArgs(runtimeDataList));
                GUI.Label(new Rect(10, Screen.height - startPos - 85, 700, 30),
                    "Rendered Shadow " + name + " instance count: " + GetRenderedAmountsGUITextFromArgs(runtimeDataList, true));
            }

            if (enabledCount > 0)
                GUI.Label(new Rect(10, Screen.height - startPos - 105, 700, 30),
                    "Instancing disabled " + name + " instance count: " + enabledCount);
        }

        private static string GetRenderedAmountsGUITextFromArgs<T>(List<T> runtimeData, bool isShadow = false) where T : GPUInstancerRuntimeData
        {
            int totalRendered = 0;
            int maxLodCount = 1;
            for (int i = 0; i < runtimeData.Count; i++)
            {
                if (maxLodCount < runtimeData[i].instanceLODs.Count)
                    maxLodCount = runtimeData[i].instanceLODs.Count;
            }

            if (maxLodCount > lodCountScratch.Length)
                maxLodCount = lodCountScratch.Length;

            for (int lod = 0; lod < maxLodCount; lod++)
                lodCountScratch[lod] = 0;

            for (int i = 0; i < runtimeData.Count; i++)
            {
                if (isShadow)
                {
                    if (runtimeData[i].shadowArgs != null && runtimeData[i].shadowArgs.Length > 0)
                        for (int lod = 0; lod < runtimeData[i].instanceLODs.Count && lod < maxLodCount; lod++)
                            lodCountScratch[lod] += (int)runtimeData[i].shadowArgs[runtimeData[i].instanceLODs[lod].argsBufferOffset + 1];
                }
                else
                {
                    if (runtimeData[i].args != null && runtimeData[i].args.Length > 0)
                        for (int lod = 0; lod < runtimeData[i].instanceLODs.Count && lod < maxLodCount; lod++)
                            lodCountScratch[lod] += (int)runtimeData[i].args[runtimeData[i].instanceLODs[lod].argsBufferOffset + 1];
                }
            }
            string lodstr = "";
            for (int lod = 0; lod < maxLodCount; lod++)
            {
                totalRendered += lodCountScratch[lod];
                lodstr += "LOD" + lod + ": " + lodCountScratch[lod] + (lod == maxLodCount - 1 ? "" : ", ");
            }

            return totalRendered + " (" + lodstr + ")";
        }
#endif
    }
}
