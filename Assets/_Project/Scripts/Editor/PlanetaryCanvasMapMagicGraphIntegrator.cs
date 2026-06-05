using System;
using System.IO;
using System.Text;
using Den.Tools.Matrices;
using MapMagic.Nodes;
using MapMagic.Nodes.MatrixGenerators;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    public static class PlanetaryCanvasMapMagicGraphIntegrator
    {
        private const string GraphPath = "Assets/MapMagic/Map_Graph/New Gen/ACTUAL TERRAIN.asset";
        private const string ArtifactPath = "CodexArtifacts/planetary-canvas-graph-integration-2026-05-05.json";
        private static readonly Encoding ArtifactEncoding = new UTF8Encoding(false);

        [MenuItem("HECTON-8/World/Integrate Planetary Canvas Graph")]
        public static void RunMenu()
        {
            RunBatchmode();
        }

        public static void RunBatchmode()
        {
            IntegrationReport report = IntegrateGraph(GraphPath);
            WriteReport(report);
            if (!report.Success)
                throw new InvalidOperationException(report.Message);
        }

        public static IntegrationReport IntegrateGraph(string graphPath)
        {
            Graph graph = AssetDatabase.LoadAssetAtPath<Graph>(graphPath);
            if (graph == null)
                return IntegrationReport.Failure(graphPath, "MapMagic graph asset not found.");

            HeightOutput200 heightOutput = FindFirst<HeightOutput200>(graph);
            TexturesOutput200 texturesOutput = FindFirst<TexturesOutput200>(graph);
            if (heightOutput == null)
                return IntegrationReport.Failure(graphPath, "HeightOutput200 node not found.");

            HectonBiomeMatrixMapMagicPostProcessNode tectonicNode = EnsureGenerator<HectonBiomeMatrixMapMagicPostProcessNode>(graph, -460f, -80f, out bool createdTectonic);
            HectonHydraulicErosionMapMagicNode erosionNode = EnsureGenerator<HectonHydraulicErosionMapMagicNode>(graph, -260f, -80f, out bool createdErosion);
            HectonTerrainSplatmapMapMagicNode splatNode = EnsureGenerator<HectonTerrainSplatmapMapMagicNode>(graph, -80f, 150f, out bool createdSplat);
            HectonAnomalyMapMagicNode anomalyNode = EnsureGenerator<HectonAnomalyMapMagicNode>(graph, 120f, 150f, out bool createdAnomaly);
            ConfigureRecoveryDefaults(erosionNode, anomalyNode);

            IOutlet<object> sourceOutlet = ResolveHeightSource(graph, heightOutput, tectonicNode, erosionNode, splatNode);
            if (sourceOutlet == null)
                return IntegrationReport.Failure(graphPath, "No valid upstream height source found.");

            graph.Link(sourceOutlet, tectonicNode);
            graph.Link(tectonicNode, erosionNode.heightIn);
            graph.Link(erosionNode.erodedHeightOut, splatNode.heightIn);
            graph.Link(erosionNode.sedimentMaskOut, splatNode.sedimentIn);
            graph.Link(erosionNode.erodedHeightOut, anomalyNode.heightIn);
            graph.Link(erosionNode.erodedHeightOut, heightOutput);

            int rockLinks = 0;
            int sandLinks = 0;
            int siltLinks = 0;
            int brineMudLinks = 0;
            if (texturesOutput != null && texturesOutput.layers != null)
            {
                for (int i = 0; i < texturesOutput.layers.Length; i++)
                {
                    TexturesOutput200.TextureLayer layer = texturesOutput.layers[i];
                    if (layer == null)
                        continue;

                    TextureSemantic semantic = ResolveTextureSemantic(layer);
                    if (semantic == TextureSemantic.Rock)
                    {
                        graph.Link(splatNode.rockOut, layer);
                        rockLinks++;
                    }
                    else if (semantic == TextureSemantic.Silt)
                    {
                        graph.Link(splatNode.siltOut, layer);
                        siltLinks++;
                    }
                    else if (semantic == TextureSemantic.Sand)
                    {
                        graph.Link(splatNode.sandOut, layer);
                        sandLinks++;
                    }
                    else if (semantic == TextureSemantic.Mud)
                    {
                        graph.Link(anomalyNode.brineMaskOut, layer);
                        brineMudLinks++;
                    }
                }
            }

            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();

            return new IntegrationReport
            {
                Success = true,
                GraphPath = graphPath,
                Message = "Planetary canvas graph integration applied.",
                CreatedTectonicNode = createdTectonic,
                CreatedErosionNode = createdErosion,
                CreatedSplatNode = createdSplat,
                CreatedAnomalyNode = createdAnomaly,
                HeightOutputLinked = true,
                RockTextureLinks = rockLinks,
                SandTextureLinks = sandLinks,
                SiltTextureLinks = siltLinks,
                BrineMudTextureLinks = brineMudLinks
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

        private static void ConfigureRecoveryDefaults(
            HectonHydraulicErosionMapMagicNode erosionNode,
            HectonAnomalyMapMagicNode anomalyNode)
        {
            if (erosionNode != null)
            {
                erosionNode.dropletCount = Math.Max(1, Math.Min(erosionNode.dropletCount, 32000));
                erosionNode.maxLifetime = Math.Max(1, Math.Min(erosionNode.maxLifetime, 32));
                erosionNode.maxOperationsPerSlice = Math.Max(128, Math.Min(erosionNode.maxOperationsPerSlice, 768));
                erosionNode.sedimentaryFlatSmoothingIterations = Math.Max(0, Math.Min(erosionNode.sedimentaryFlatSmoothingIterations, 1));
                erosionNode.thermalIterations = Math.Max(0, Math.Min(erosionNode.thermalIterations, 1));
                erosionNode.canyonWallStrength = Math.Min(erosionNode.canyonWallStrength, 2.0f);
                erosionNode.canyonWallMaxLift01 = Math.Min(erosionNode.canyonWallMaxLift01, 0.012f);
            }

            if (anomalyNode != null)
                anomalyNode.maxFloodCells = Math.Max(1024, Math.Min(anomalyNode.maxFloodCells, 8192));
        }

        private static IOutlet<object> ResolveHeightSource(
            Graph graph,
            HeightOutput200 heightOutput,
            HectonBiomeMatrixMapMagicPostProcessNode tectonicNode,
            HectonHydraulicErosionMapMagicNode erosionNode,
            HectonTerrainSplatmapMapMagicNode splatNode)
        {
            IOutlet<object> source = graph.GetLink(tectonicNode);
            if (IsUsableSource(source, tectonicNode, erosionNode, splatNode))
                return source;

            source = graph.GetLink(erosionNode.heightIn);
            if (IsUsableSource(source, tectonicNode, erosionNode, splatNode))
                return source;

            source = graph.GetLink(heightOutput);
            if (IsUsableSource(source, tectonicNode, erosionNode, splatNode))
                return source;

            Generator[] generators = graph != null ? graph.generators : null;
            if (generators != null)
            {
                for (int i = 0; i < generators.Length; i++)
                {
                    if (generators[i] is Import200 importNode)
                        return importNode;
                }
            }

            return null;
        }

        private static bool IsUsableSource(
            IOutlet<object> outlet,
            HectonBiomeMatrixMapMagicPostProcessNode tectonicNode,
            HectonHydraulicErosionMapMagicNode erosionNode,
            HectonTerrainSplatmapMapMagicNode splatNode)
        {
            if (outlet == null)
                return false;

            Generator generator = outlet.Gen;
            if (generator is Placeholders.GenericPlaceholder)
                return false;

            return generator != tectonicNode &&
                   generator != erosionNode &&
                   generator != splatNode;
        }

        private static TextureSemantic ResolveTextureSemantic(TexturesOutput200.TextureLayer layer)
        {
            if (ContainsToken(layer.name, "silt") ||
                ContainsToken(layer.name, "sediment") ||
                ContainsToken(layer.prototype != null ? layer.prototype.name : null, "silt") ||
                ContainsToken(layer.prototype != null ? layer.prototype.name : null, "sediment"))
            {
                return TextureSemantic.Silt;
            }

            if (ContainsToken(layer.name, "rock") ||
                ContainsToken(layer.name, "cliff") ||
                ContainsToken(layer.name, "brittle") ||
                ContainsToken(layer.prototype != null ? layer.prototype.name : null, "rock") ||
                ContainsToken(layer.prototype != null ? layer.prototype.name : null, "cliff") ||
                ContainsToken(layer.prototype != null ? layer.prototype.name : null, "brittle"))
            {
                return TextureSemantic.Rock;
            }

            if (ContainsToken(layer.name, "sand") ||
                ContainsToken(layer.prototype != null ? layer.prototype.name : null, "sand"))
            {
                return TextureSemantic.Sand;
            }

            if (ContainsToken(layer.name, "mud") ||
                ContainsToken(layer.name, "viscous") ||
                ContainsToken(layer.name, "brine") ||
                ContainsToken(layer.prototype != null ? layer.prototype.name : null, "mud") ||
                ContainsToken(layer.prototype != null ? layer.prototype.name : null, "viscous") ||
                ContainsToken(layer.prototype != null ? layer.prototype.name : null, "brine"))
            {
                return TextureSemantic.Mud;
            }

            return TextureSemantic.None;
        }

        private static bool ContainsToken(string value, string token)
        {
            return !string.IsNullOrEmpty(value) &&
                   value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void WriteReport(IntegrationReport report)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ArtifactPath) ?? ".");
            var builder = new StringBuilder(512);
            builder.AppendLine("{");
            builder.Append("  \"success\": ").Append(report.Success ? "true" : "false").AppendLine(",");
            builder.Append("  \"graphPath\": \"").Append(Escape(report.GraphPath)).AppendLine("\",");
            builder.Append("  \"message\": \"").Append(Escape(report.Message)).AppendLine("\",");
            builder.Append("  \"createdTectonicNode\": ").Append(report.CreatedTectonicNode ? "true" : "false").AppendLine(",");
            builder.Append("  \"createdErosionNode\": ").Append(report.CreatedErosionNode ? "true" : "false").AppendLine(",");
            builder.Append("  \"createdSplatNode\": ").Append(report.CreatedSplatNode ? "true" : "false").AppendLine(",");
            builder.Append("  \"createdAnomalyNode\": ").Append(report.CreatedAnomalyNode ? "true" : "false").AppendLine(",");
            builder.Append("  \"heightOutputLinked\": ").Append(report.HeightOutputLinked ? "true" : "false").AppendLine(",");
            builder.Append("  \"rockTextureLinks\": ").Append(report.RockTextureLinks).AppendLine(",");
            builder.Append("  \"sandTextureLinks\": ").Append(report.SandTextureLinks).AppendLine(",");
            builder.Append("  \"siltTextureLinks\": ").Append(report.SiltTextureLinks).AppendLine(",");
            builder.Append("  \"brineMudTextureLinks\": ").Append(report.BrineMudTextureLinks).AppendLine();
            builder.AppendLine("}");
            File.WriteAllText(ArtifactPath, builder.ToString(), ArtifactEncoding);
        }

        private static string Escape(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private enum TextureSemantic
        {
            None,
            Sand,
            Rock,
            Silt,
            Mud
        }

        public struct IntegrationReport
        {
            public bool Success;
            public string GraphPath;
            public string Message;
            public bool CreatedTectonicNode;
            public bool CreatedErosionNode;
            public bool CreatedSplatNode;
            public bool CreatedAnomalyNode;
            public bool HeightOutputLinked;
            public int RockTextureLinks;
            public int SandTextureLinks;
            public int SiltTextureLinks;
            public int BrineMudTextureLinks;

            public static IntegrationReport Failure(string graphPath, string message)
            {
                return new IntegrationReport
                {
                    Success = false,
                    GraphPath = graphPath,
                    Message = message
                };
            }
        }
    }
}
