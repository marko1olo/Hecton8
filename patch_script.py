with open("Assets/GPUInstancer/Scripts/Core/Contract/GPUInstancerManager.cs", "r") as f:
    content = f.read()

# Add treeProxyRenderers dictionary declaration
target1 = "public static Dictionary<GameObject, Transform> treeProxyList; // Dict[TreePrefab, TreeProxyGO]"
replacement1 = target1 + "\n        public static Dictionary<GameObject, MeshRenderer[]> treeProxyRenderers;"
content = content.replace(target1, replacement1)

# Clear treeProxyRenderers when treeProxyList is cleared
target2 = """                        if (treeProxyList != null)
                            treeProxyList.Clear();"""
replacement2 = """                        if (treeProxyList != null)
                            treeProxyList.Clear();
                        if (treeProxyRenderers != null)
                            treeProxyRenderers.Clear();"""
content = content.replace(target2, replacement2)

target3 = """                        if (treeProxyList[treePrototype.prefabObject] == null)
                            treeProxyList.Remove(treePrototype.prefabObject);"""
replacement3 = """                        if (treeProxyList[treePrototype.prefabObject] == null)
                        {
                            treeProxyList.Remove(treePrototype.prefabObject);
                            if (treeProxyRenderers != null) treeProxyRenderers.Remove(treePrototype.prefabObject);
                        }"""
content = content.replace(target3, replacement3)

target4 = """                    for (int lod = 0; lod < runtimeData.instanceLODs.Count; lod++)
                    {
                        if (proxyTransform.childCount <= lod)
                            continue;

                        rdLOD = runtimeData.instanceLODs[lod];
                        meshRenderer = proxyTransform.GetChild(lod).GetComponent<MeshRenderer>();"""
replacement4 = """                    if (treeProxyRenderers == null)
                        treeProxyRenderers = new Dictionary<GameObject, MeshRenderer[]>();
                    MeshRenderer[] renderersCache;
                    if (!treeProxyRenderers.TryGetValue(runtimeData.prototype.prefabObject, out renderersCache))
                    {
                        renderersCache = new MeshRenderer[runtimeData.instanceLODs.Count];
                        for (int lod = 0; lod < runtimeData.instanceLODs.Count; lod++)
                        {
                            if (proxyTransform.childCount > lod)
                                renderersCache[lod] = proxyTransform.GetChild(lod).GetComponent<MeshRenderer>();
                        }
                        treeProxyRenderers[runtimeData.prototype.prefabObject] = renderersCache;
                    }

                    for (int lod = 0; lod < runtimeData.instanceLODs.Count; lod++)
                    {
                        if (proxyTransform.childCount <= lod)
                            continue;

                        rdLOD = runtimeData.instanceLODs[lod];
                        meshRenderer = renderersCache[lod];
                        if (meshRenderer == null)
                            continue;"""
content = content.replace(target4, replacement4)

with open("Assets/GPUInstancer/Scripts/Core/Contract/GPUInstancerManager.cs", "w") as f:
    f.write(content)

print("Patch applied.")
