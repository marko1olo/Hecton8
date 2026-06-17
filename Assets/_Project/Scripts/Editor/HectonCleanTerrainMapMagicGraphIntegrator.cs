using System;
using System.Collections.Generic;
using MapMagic.Nodes;
using MapMagic.Nodes.MatrixGenerators;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    public static class HectonCleanTerrainMapMagicGraphIntegrator
    {
        public const string ActiveSandboxGraphPath =
            "Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset";

        private const string Batch34TerrainLayerRoot =
            "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialAtlases/Batch20260608_TextureExpansion/TerrainLayers";

        private static readonly CanonicalTerrainLayerSpec[] CanonicalTextureLayers =
        {
            new CanonicalTerrainLayerSpec("L_B34_3408_ClaySiltTurbiditySlope.terrainlayer", true),
            new CanonicalTerrainLayerSpec("L_B34_3401_PhoticLimestoneRubbleShelf.terrainlayer", false),
            new CanonicalTerrainLayerSpec("L_B34_3402_ShallowSeagrassRootMat.terrainlayer", false),
            new CanonicalTerrainLayerSpec("L_B34_3406_SerpentiniteFaultRock.terrainlayer", false),
            new CanonicalTerrainLayerSpec("L_B34_3403_BrineCanyonSaltCrustSilt.terrainlayer", false),
            new CanonicalTerrainLayerSpec("L_B34_3404_AbyssalManganeseNodulePlain.terrainlayer", false),
            new CanonicalTerrainLayerSpec("L_B34_3405_MethaneHydrateCrackVein.terrainlayer", false),
            new CanonicalTerrainLayerSpec("L_B34_3409_LimestoneCaveCeilingMineralDrip.terrainlayer", false),
        };

        [MenuItem("Hecton8/World/MapMagic/Apply Clean Terrain Material Route")]
        public static void ApplyActiveSandboxGraphMenu()
        {
            IntegrationResult result = ApplyMaterialRoute(ActiveSandboxGraphPath);
            if (!result.Success)
                throw new InvalidOperationException(result.Message);

            Debug.Log(
                "[HectonCleanTerrainMapMagicGraphIntegrator] " +
                $"graph={result.GraphPath}, createdMaterialNode={result.CreatedMaterialNode}, " +
                $"syncedTextureLayers={result.SyncedTextureLayers}, baseTextureLayers={result.BaseTextureLayers}, " +
                $"explicitLinks={result.ExplicitMaterialLinks}, packedLinks={result.PackedCompatibilityLinks}, " +
                $"unmatchedLayers={result.UnmatchedTextureLayers}, staleTextureLinksRemoved={result.StaleTextureLinksRemoved}");
        }

        [MenuItem("Hecton8/World/MapMagic/Validate Clean Terrain Material Route")]
        public static void ValidateActiveSandboxGraphMenu()
        {
            ValidationResult result = ValidateActiveSandboxGraph();
            if (!result.Success)
                throw new InvalidOperationException(result.Message);

            Debug.Log(
                "[HectonCleanTerrainMapMagicGraphIntegrator] " +
                $"validation graph={result.GraphPath}, hasMacroBase={result.HasMacroBaseNode}, " +
                $"hasSurfaceMaterials={result.HasSurfaceMaterialNode}, hasTexturesOutput={result.HasTexturesOutput}, " +
                $"hasLegacySplatOnly={result.HasLegacySplatOnly}, matchedTextureLayers={result.MatchedTextureLayers}, " +
                $"missingTextureLayerLinks={result.MissingTextureLayerLinks}, mismatchedTextureLayerLinks={result.MismatchedTextureLayerLinks}, " +
                $"wrongSourceTextureLayerLinks={result.WrongSourceTextureLayerLinks}, unexpectedBaseTextureLayerLinks={result.UnexpectedBaseTextureLayerLinks}, " +
                $"canonicalTextureLayerMismatches={result.CanonicalTextureLayerMismatches}, unmatchedTextureLayers={result.UnmatchedTextureLayers}, " +
                $"staleTextureLayerLinks={result.StaleTextureLayerLinks}");
        }

        public static IntegrationResult ApplyMaterialRoute(string graphPath)
        {
            Graph graph = AssetDatabase.LoadAssetAtPath<Graph>(graphPath);
            if (graph == null)
                return IntegrationResult.Failure(graphPath, "MapMagic active terrain graph asset not found.");

            TexturesOutput200 texturesOutput = FindFirst<TexturesOutput200>(graph);
            if (texturesOutput == null)
                return IntegrationResult.Failure(graphPath, "TexturesOutput200 node not found.");

            HectonTerrainSurfaceMaterialMapMagicNode materialNode =
                EnsureGenerator<HectonTerrainSurfaceMaterialMapMagicNode>(
                    graph,
                    180f,
                    360f,
                    out bool createdMaterialNode);
            TextureLayerSyncResult syncResult = SyncCanonicalTextureLayers(texturesOutput);
            if (!syncResult.Success)
                return IntegrationResult.Failure(graphPath, syncResult.Message);

            int explicitLinks = 0;
            int packedLinks = 0;
            int unmatchedLayers = 0;
            int baseTextureLayers = 0;
            int staleTextureLinksRemoved = RemoveStaleTextureOutputLinks(graph, texturesOutput);

            TexturesOutput200.TextureLayer[] layers = texturesOutput.layers;
            if (layers != null)
            {
                for (int i = 0; i < layers.Length; i++)
                {
                    TexturesOutput200.TextureLayer layer = layers[i];
                    if (layer == null)
                        continue;

                    if (i == 0)
                    {
                        if (graph.IsLinked(layer))
                            graph.UnlinkInlet(layer);
                        baseTextureLayers++;
                        continue;
                    }

                    IOutlet<object> outlet = ResolveExplicitMaterialOutlet(materialNode, layer);
                    if (outlet != null)
                    {
                        graph.Link(outlet, layer);
                        explicitLinks++;
                        continue;
                    }

                    outlet = ResolvePackedCompatibilityOutlet(materialNode, layer);
                    if (outlet != null)
                    {
                        graph.Link(outlet, layer);
                        packedLinks++;
                        continue;
                    }

                    unmatchedLayers++;
                }
            }

            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();

            return new IntegrationResult
            {
                Success = true,
                GraphPath = graphPath,
                Message = "Clean terrain material route applied to active MapMagic graph.",
                CreatedMaterialNode = createdMaterialNode,
                SyncedTextureLayers = syncResult.SyncedTextureLayers,
                BaseTextureLayers = baseTextureLayers,
                ExplicitMaterialLinks = explicitLinks,
                PackedCompatibilityLinks = packedLinks,
                UnmatchedTextureLayers = unmatchedLayers,
                StaleTextureLinksRemoved = staleTextureLinksRemoved
            };
        }

        public static ValidationResult ValidateActiveSandboxGraph()
        {
            return ValidateGraph(ActiveSandboxGraphPath);
        }

        public static ValidationResult ValidateGraph(string graphPath)
        {
            Graph graph = AssetDatabase.LoadAssetAtPath<Graph>(graphPath);
            if (graph == null)
                return ValidationResult.Failure(graphPath, "MapMagic active terrain graph asset not found.");

            bool hasMacroBase = FindFirst<HectonSandboxAbyssalShelfMapMagicNode>(graph) != null;
            HectonTerrainSurfaceMaterialMapMagicNode materialNode =
                FindFirst<HectonTerrainSurfaceMaterialMapMagicNode>(graph);
            TexturesOutput200 texturesOutput = FindFirst<TexturesOutput200>(graph);
            bool hasMaterialNode = materialNode != null;
            bool hasTexturesOutput = texturesOutput != null;
            bool hasLegacySplatOnly =
                !hasMaterialNode &&
                FindFirst<HectonTerrainSplatmapMapMagicNode>(graph) != null;

            if (!hasTexturesOutput)
                return ValidationResult.Failure(graphPath, "TexturesOutput200 node not found.");

            if (!hasMaterialNode)
            {
                return ValidationResult.Failure(
                    graphPath,
                    hasLegacySplatOnly
                        ? "Active graph still depends on legacy packed splat route only; run clean material route integration."
                        : "Active graph does not contain Macro Surface Materials node.");
            }

            CanonicalTextureLayerValidationResult canonicalResult =
                ValidateCanonicalTextureLayers(texturesOutput);
            if (!canonicalResult.Success)
            {
                return new ValidationResult
                {
                    Success = false,
                    GraphPath = graphPath,
                    Message =
                        "Active graph TexturesOutput stack is not the canonical Batch34 geology stack. " +
                        canonicalResult.FirstFailure,
                    HasMacroBaseNode = hasMacroBase,
                    HasSurfaceMaterialNode = hasMaterialNode,
                    HasTexturesOutput = hasTexturesOutput,
                    HasLegacySplatOnly = hasLegacySplatOnly,
                    CanonicalTextureLayerMismatches = canonicalResult.Mismatches
                };
            }

            StrictTextureRouteResult routeResult =
                ValidateTextureLayerRoutes(graph, materialNode, texturesOutput);
            bool strictRouteValid =
                routeResult.MissingTextureLayerLinks == 0 &&
                routeResult.MismatchedTextureLayerLinks == 0 &&
                routeResult.WrongSourceTextureLayerLinks == 0 &&
                routeResult.UnexpectedBaseTextureLayerLinks == 0 &&
                routeResult.UnmatchedTextureLayers == 0 &&
                routeResult.StaleTextureLayerLinks == 0;

            if (!strictRouteValid)
            {
                return new ValidationResult
                {
                    Success = false,
                    GraphPath = graphPath,
                    Message =
                        "Active graph texture output is not strictly routed to Macro Surface Materials. " +
                        routeResult.FirstFailure,
                    HasMacroBaseNode = hasMacroBase,
                    HasSurfaceMaterialNode = hasMaterialNode,
                    HasTexturesOutput = hasTexturesOutput,
                    HasLegacySplatOnly = hasLegacySplatOnly,
                    MatchedTextureLayers = routeResult.MatchedTextureLayers,
                    MissingTextureLayerLinks = routeResult.MissingTextureLayerLinks,
                    MismatchedTextureLayerLinks = routeResult.MismatchedTextureLayerLinks,
                    WrongSourceTextureLayerLinks = routeResult.WrongSourceTextureLayerLinks,
                    UnexpectedBaseTextureLayerLinks = routeResult.UnexpectedBaseTextureLayerLinks,
                    CanonicalTextureLayerMismatches = canonicalResult.Mismatches,
                    UnmatchedTextureLayers = routeResult.UnmatchedTextureLayers,
                    StaleTextureLayerLinks = routeResult.StaleTextureLayerLinks
                };
            }

            return new ValidationResult
            {
                Success = true,
                GraphPath = graphPath,
                Message = hasMacroBase
                    ? "Active graph has macro base and macro surface materials."
                    : "Active graph has macro surface materials; height source is still external or legacy graph chain.",
                HasMacroBaseNode = hasMacroBase,
                HasSurfaceMaterialNode = hasMaterialNode,
                HasTexturesOutput = hasTexturesOutput,
                HasLegacySplatOnly = hasLegacySplatOnly,
                MatchedTextureLayers = routeResult.MatchedTextureLayers,
                MissingTextureLayerLinks = routeResult.MissingTextureLayerLinks,
                MismatchedTextureLayerLinks = routeResult.MismatchedTextureLayerLinks,
                WrongSourceTextureLayerLinks = routeResult.WrongSourceTextureLayerLinks,
                UnexpectedBaseTextureLayerLinks = routeResult.UnexpectedBaseTextureLayerLinks,
                CanonicalTextureLayerMismatches = canonicalResult.Mismatches,
                UnmatchedTextureLayers = routeResult.UnmatchedTextureLayers,
                StaleTextureLayerLinks = routeResult.StaleTextureLayerLinks
            };
        }

        private static T EnsureGenerator<T>(Graph graph, float x, float y, out bool created)
            where T : Generator
        {
            T existing = FindFirst<T>(graph);
            if (existing != null)
            {
                created = false;
                return existing;
            }

            T generator = (T)Generator.Create(typeof(T));
            generator.guiPosition = new Vector2(x, y);
            graph.Add(generator);
            created = true;
            return generator;
        }

        private static T FindFirst<T>(Graph graph)
        {
            Generator[] generators = graph != null ? graph.generators : null;
            if (generators == null)
                return default;

            for (int i = 0; i < generators.Length; i++)
            {
                if (generators[i] is T generator)
                    return generator;
            }

            return default;
        }

        private static IOutlet<object> ResolveExplicitMaterialOutlet(
            HectonTerrainSurfaceMaterialMapMagicNode materialNode,
            TexturesOutput200.TextureLayer layer)
        {
            if (HasAnyToken(layer, "seagrass", "rootmat", "root mat"))
                return materialNode.reefRubbleOut;

            if (HasAnyToken(layer, "shell", "sand", "shallow", "beach", "grit"))
                return materialNode.shellSandOut;

            if (HasAnyToken(layer, "limestone", "calcium", "shelf", "carbonate"))
                return materialNode.limestoneShelfOut;

            if (HasAnyToken(layer, "clay", "silt", "sediment", "turbidity"))
                return materialNode.claySiltOut;

            if (HasAnyToken(layer, "hardrock", "hard rock", "basalt", "serpentinite", "cliff", "fault", "ridge"))
                return materialNode.hardRockOut;

            if (HasAnyToken(layer, "brine", "salt", "crust", "hydrate"))
                return materialNode.brineSaltCrustOut;

            if (HasAnyToken(layer, "manganese", "nodule"))
                return materialNode.manganeseNodulePlainOut;

            if (HasAnyToken(layer, "reef", "coral", "rubble"))
                return materialNode.reefRubbleOut;

            if (HasAnyToken(layer, "seep", "oxide", "iron", "bacteria"))
                return materialNode.seepCrustOut;

            return null;
        }

        private static IOutlet<object> ResolveExpectedMaterialOutlet(
            HectonTerrainSurfaceMaterialMapMagicNode materialNode,
            TexturesOutput200.TextureLayer layer,
            out string routeName)
        {
            IOutlet<object> outlet = ResolveExplicitMaterialOutlet(materialNode, layer);
            if (outlet != null)
            {
                routeName = GetMaterialRouteName(materialNode, outlet);
                return outlet;
            }

            outlet = ResolvePackedCompatibilityOutlet(materialNode, layer);
            routeName = outlet != null ? GetMaterialRouteName(materialNode, outlet) : null;
            return outlet;
        }

        private static string GetMaterialRouteName(
            HectonTerrainSurfaceMaterialMapMagicNode materialNode,
            IOutlet<object> outlet)
        {
            if (ReferenceEquals(outlet, materialNode.shellSandOut))
                return nameof(materialNode.shellSandOut);
            if (ReferenceEquals(outlet, materialNode.limestoneShelfOut))
                return nameof(materialNode.limestoneShelfOut);
            if (ReferenceEquals(outlet, materialNode.claySiltOut))
                return nameof(materialNode.claySiltOut);
            if (ReferenceEquals(outlet, materialNode.hardRockOut))
                return nameof(materialNode.hardRockOut);
            if (ReferenceEquals(outlet, materialNode.brineSaltCrustOut))
                return nameof(materialNode.brineSaltCrustOut);
            if (ReferenceEquals(outlet, materialNode.manganeseNodulePlainOut))
                return nameof(materialNode.manganeseNodulePlainOut);
            if (ReferenceEquals(outlet, materialNode.reefRubbleOut))
                return nameof(materialNode.reefRubbleOut);
            if (ReferenceEquals(outlet, materialNode.seepCrustOut))
                return nameof(materialNode.seepCrustOut);
            if (ReferenceEquals(outlet, materialNode.control1XOut))
                return nameof(materialNode.control1XOut);
            if (ReferenceEquals(outlet, materialNode.control1YOut))
                return nameof(materialNode.control1YOut);
            if (ReferenceEquals(outlet, materialNode.control1ZOut))
                return nameof(materialNode.control1ZOut);
            if (ReferenceEquals(outlet, materialNode.control1WOut))
                return nameof(materialNode.control1WOut);

            return "unknownMaterialOutlet";
        }

        private static IOutlet<object> ResolvePackedCompatibilityOutlet(
            HectonTerrainSurfaceMaterialMapMagicNode materialNode,
            TexturesOutput200.TextureLayer layer)
        {
            if (HasAnyToken(layer, "rock", "cliff", "brittle", "hard"))
                return materialNode.control1XOut;

            if (HasAnyToken(layer, "sand", "gravel", "shell"))
                return materialNode.control1YOut;

            if (HasAnyToken(layer, "silt", "mud", "sediment"))
                return materialNode.control1ZOut;

            if (HasAnyToken(layer, "deposition", "cavity", "brine", "salt"))
                return materialNode.control1WOut;

            return null;
        }

        private static bool HasAnyToken(TexturesOutput200.TextureLayer layer, params string[] tokens)
        {
            if (layer == null || tokens == null)
                return false;

            string layerName = layer.name;
            string prototypeName = layer.prototype != null ? layer.prototype.name : null;
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (ContainsToken(layerName, token) || ContainsToken(prototypeName, token))
                    return true;
            }

            return false;
        }

        private static bool ContainsToken(string value, string token)
        {
            return !string.IsNullOrEmpty(value) &&
                   !string.IsNullOrEmpty(token) &&
                   value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static StrictTextureRouteResult ValidateTextureLayerRoutes(
            Graph graph,
            HectonTerrainSurfaceMaterialMapMagicNode materialNode,
            TexturesOutput200 texturesOutput)
        {
            StrictTextureRouteResult result = new StrictTextureRouteResult();
            HashSet<IInlet<object>> activeTextureLayerInlets = new HashSet<IInlet<object>>();
            if (texturesOutput?.layers == null)
                return result;

            for (int i = 0; i < texturesOutput.layers.Length; i++)
            {
                TexturesOutput200.TextureLayer layer = texturesOutput.layers[i];
                if (layer == null)
                    continue;

                activeTextureLayerInlets.Add(layer);
                IOutlet<object> expected = ResolveExpectedMaterialOutlet(materialNode, layer, out string routeName);
                IOutlet<object> actual = graph.GetLink(layer);
                string layerName = GetTextureLayerName(layer);

                if (i == 0)
                {
                    if (actual == null)
                    {
                        result.BaseTextureLayers++;
                    }
                    else
                    {
                        result.UnexpectedBaseTextureLayerLinks++;
                        result.CaptureFirstFailure(
                            $"base layer '{layerName}' must be unlinked because MapMagic TexturesOutput forces layer 0 to full weight.");
                    }

                    continue;
                }

                if (expected == null)
                {
                    result.UnmatchedTextureLayers++;
                    result.CaptureFirstFailure($"unmatched layer '{layerName}' has no macro material route.");
                    continue;
                }

                if (actual == null)
                {
                    result.MissingTextureLayerLinks++;
                    result.CaptureFirstFailure($"layer '{layerName}' is not linked; expected {routeName}.");
                    continue;
                }

                bool correctOutlet = ReferenceEquals(actual, expected);
                bool correctSource = actual.Gen == materialNode;
                if (!correctOutlet || !correctSource)
                {
                    result.MismatchedTextureLayerLinks++;
                    if (!correctSource)
                        result.WrongSourceTextureLayerLinks++;

                    string actualSource = actual.Gen != null ? actual.Gen.GetType().Name : "null";
                    result.CaptureFirstFailure(
                        $"layer '{layerName}' is linked to {actualSource}; expected {routeName}.");
                    continue;
                }

                result.MatchedTextureLayers++;
            }

            if (graph.links != null)
            {
                foreach (KeyValuePair<IInlet<object>, IOutlet<object>> link in graph.links)
                {
                    IInlet<object> inlet = link.Key;
                    if (inlet == null || inlet.Gen != texturesOutput)
                        continue;

                    if (!activeTextureLayerInlets.Contains(inlet))
                    {
                        result.StaleTextureLayerLinks++;
                        result.CaptureFirstFailure("graph contains stale TexturesOutput layer inlet links.");
                    }
                }
            }

            return result;
        }

        private static TextureLayerSyncResult SyncCanonicalTextureLayers(TexturesOutput200 texturesOutput)
        {
            if (texturesOutput == null)
                return TextureLayerSyncResult.Failure("TexturesOutput200 node not found.");

            TexturesOutput200.TextureLayer[] previousLayers =
                texturesOutput.layers ?? Array.Empty<TexturesOutput200.TextureLayer>();
            TexturesOutput200.TextureLayer[] canonicalLayers =
                new TexturesOutput200.TextureLayer[CanonicalTextureLayers.Length];

            for (int i = 0; i < CanonicalTextureLayers.Length; i++)
            {
                CanonicalTerrainLayerSpec spec = CanonicalTextureLayers[i];
                TerrainLayer terrainLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(spec.AssetPath);
                if (terrainLayer == null)
                    return TextureLayerSyncResult.Failure(
                        $"Missing canonical Batch34 TerrainLayer asset: {spec.AssetPath}. Run Batch34TerrainLayerAssetBuilder first.");

                TexturesOutput200.TextureLayer layer =
                    FindPreviousTextureLayer(previousLayers, spec.AssetPath) ??
                    new TexturesOutput200.TextureLayer();
                layer.SetGen(texturesOutput);
                if (layer.Id == 0)
                    layer.Id = Den.Tools.Id.Generate();
                layer.name = terrainLayer.name;
                layer.prototype = terrainLayer;
                layer.Opacity = 1f;
                canonicalLayers[i] = layer;
            }

            texturesOutput.layers = canonicalLayers;
            texturesOutput.version++;
            return new TextureLayerSyncResult
            {
                Success = true,
                Message = "Canonical Batch34 texture layer stack synchronized.",
                SyncedTextureLayers = canonicalLayers.Length
            };
        }

        private static TexturesOutput200.TextureLayer FindPreviousTextureLayer(
            TexturesOutput200.TextureLayer[] previousLayers,
            string expectedAssetPath)
        {
            if (previousLayers == null)
                return null;

            for (int i = 0; i < previousLayers.Length; i++)
            {
                TexturesOutput200.TextureLayer layer = previousLayers[i];
                if (layer == null || layer.prototype == null)
                    continue;

                string path = AssetDatabase.GetAssetPath(layer.prototype);
                if (string.Equals(path, expectedAssetPath, StringComparison.Ordinal))
                    return layer;
            }

            return null;
        }

        private static CanonicalTextureLayerValidationResult ValidateCanonicalTextureLayers(TexturesOutput200 texturesOutput)
        {
            CanonicalTextureLayerValidationResult result = new CanonicalTextureLayerValidationResult { Success = true };
            TexturesOutput200.TextureLayer[] layers = texturesOutput.layers;
            if (layers == null)
            {
                result.Success = false;
                result.Mismatches = CanonicalTextureLayers.Length;
                result.CaptureFirstFailure("TexturesOutput layer array is null.");
                return result;
            }

            if (layers.Length != CanonicalTextureLayers.Length)
            {
                result.Success = false;
                result.Mismatches += Math.Abs(layers.Length - CanonicalTextureLayers.Length);
                result.CaptureFirstFailure(
                    $"expected {CanonicalTextureLayers.Length} canonical layers, found {layers.Length}.");
            }

            int compareCount = Math.Min(layers.Length, CanonicalTextureLayers.Length);
            for (int i = 0; i < compareCount; i++)
            {
                TerrainLayer expected = AssetDatabase.LoadAssetAtPath<TerrainLayer>(CanonicalTextureLayers[i].AssetPath);
                TexturesOutput200.TextureLayer actualLayer = layers[i];
                string actualPath = actualLayer?.prototype != null
                    ? AssetDatabase.GetAssetPath(actualLayer.prototype)
                    : string.Empty;

                if (expected == null ||
                    actualLayer == null ||
                    !string.Equals(actualPath, CanonicalTextureLayers[i].AssetPath, StringComparison.Ordinal))
                {
                    result.Success = false;
                    result.Mismatches++;
                    result.CaptureFirstFailure(
                        $"layer index {i} expected '{CanonicalTextureLayers[i].AssetPath}', actual '{actualPath}'.");
                }

                if (actualLayer != null && actualLayer.Gen != texturesOutput)
                {
                    result.Success = false;
                    result.Mismatches++;
                    result.CaptureFirstFailure($"layer index {i} has stale owner generator.");
                }
            }

            return result;
        }

        private static int RemoveStaleTextureOutputLinks(Graph graph, TexturesOutput200 texturesOutput)
        {
            if (graph.links == null || texturesOutput?.layers == null)
                return 0;

            HashSet<IInlet<object>> activeTextureLayerInlets = new HashSet<IInlet<object>>();
            for (int i = 0; i < texturesOutput.layers.Length; i++)
            {
                TexturesOutput200.TextureLayer layer = texturesOutput.layers[i];
                if (layer != null)
                    activeTextureLayerInlets.Add(layer);
            }

            List<IInlet<object>> staleInlets = new List<IInlet<object>>();
            foreach (KeyValuePair<IInlet<object>, IOutlet<object>> link in graph.links)
            {
                IInlet<object> inlet = link.Key;
                if (inlet != null &&
                    inlet.Gen == texturesOutput &&
                    !activeTextureLayerInlets.Contains(inlet))
                {
                    staleInlets.Add(inlet);
                }
            }

            for (int i = 0; i < staleInlets.Count; i++)
                graph.links.Remove(staleInlets[i]);

            if (staleInlets.Count > 0)
                texturesOutput.version++;

            return staleInlets.Count;
        }

        private static string GetTextureLayerName(TexturesOutput200.TextureLayer layer)
        {
            if (layer == null)
                return "null";

            if (!string.IsNullOrEmpty(layer.name))
                return layer.name;

            return layer.prototype != null ? layer.prototype.name : "unnamed";
        }

        public struct IntegrationResult
        {
            public bool Success;
            public string GraphPath;
            public string Message;
            public bool CreatedMaterialNode;
            public int SyncedTextureLayers;
            public int BaseTextureLayers;
            public int ExplicitMaterialLinks;
            public int PackedCompatibilityLinks;
            public int UnmatchedTextureLayers;
            public int StaleTextureLinksRemoved;

            public static IntegrationResult Failure(string graphPath, string message)
            {
                return new IntegrationResult
                {
                    Success = false,
                    GraphPath = graphPath,
                    Message = message
                };
            }
        }

        public struct ValidationResult
        {
            public bool Success;
            public string GraphPath;
            public string Message;
            public bool HasMacroBaseNode;
            public bool HasSurfaceMaterialNode;
            public bool HasTexturesOutput;
            public bool HasLegacySplatOnly;
            public int MatchedTextureLayers;
            public int MissingTextureLayerLinks;
            public int MismatchedTextureLayerLinks;
            public int WrongSourceTextureLayerLinks;
            public int UnexpectedBaseTextureLayerLinks;
            public int CanonicalTextureLayerMismatches;
            public int UnmatchedTextureLayers;
            public int StaleTextureLayerLinks;

            public static ValidationResult Failure(string graphPath, string message)
            {
                return new ValidationResult
                {
                    Success = false,
                    GraphPath = graphPath,
                    Message = message
                };
            }
        }

        private struct StrictTextureRouteResult
        {
            public int BaseTextureLayers;
            public int MatchedTextureLayers;
            public int MissingTextureLayerLinks;
            public int MismatchedTextureLayerLinks;
            public int WrongSourceTextureLayerLinks;
            public int UnexpectedBaseTextureLayerLinks;
            public int UnmatchedTextureLayers;
            public int StaleTextureLayerLinks;
            public string FirstFailure;

            public void CaptureFirstFailure(string message)
            {
                if (string.IsNullOrEmpty(FirstFailure))
                    FirstFailure = message;
            }
        }

        private readonly struct CanonicalTerrainLayerSpec
        {
            public readonly string AssetPath;
            public readonly bool IsBaseLayer;

            public CanonicalTerrainLayerSpec(string fileName, bool isBaseLayer)
            {
                AssetPath = Batch34TerrainLayerRoot + "/" + fileName;
                IsBaseLayer = isBaseLayer;
            }
        }

        private struct TextureLayerSyncResult
        {
            public bool Success;
            public string Message;
            public int SyncedTextureLayers;

            public static TextureLayerSyncResult Failure(string message)
            {
                return new TextureLayerSyncResult
                {
                    Success = false,
                    Message = message
                };
            }
        }

        private struct CanonicalTextureLayerValidationResult
        {
            public bool Success;
            public int Mismatches;
            public string FirstFailure;

            public void CaptureFirstFailure(string message)
            {
                if (string.IsNullOrEmpty(FirstFailure))
                    FirstFailure = message;
            }
        }
    }
}
