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

            IOutlet<object> sourceOutlet = ResolveHeightSource(graph, heightOutput, tectonicNode, erosionNode, splatNode);
            if (sourceOutlet == null)
                return IntegrationReport.Failure(graphPath, "No valid upstream height source found.");

            graph.Link(sourceOutlet, tectonicNode);
            graph.Link(tectonicNode, erosionNode.heightIn);
            graph.Link(erosionNode.erodedHeightOut, splatNode.heightIn);
            graph.Link(erosionNode.sedimentMaskOut, splatNode.sedimentIn);
            graph.Link(erosionNode.erodedHeightOut, heightOutput);

            int rockLinks = 0;
            int sandLinks = 0;
            int siltLinks = 0;
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
                HeightOutputLinked = true,
                RockTextureLinks = rockLinks,
                SandTextureLinks = sandLinks,
                SiltTextureLinks = siltLinks
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
            foreach (T generator in graph.GeneratorsOfType<T>())
                return generator;

            return default;
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

            foreach (Import200 importNode in graph.GeneratorsOfType<Import200>())
                return importNode;

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
            builder.Append("  \"heightOutputLinked\": ").Append(report.HeightOutputLinked ? "true" : "false").AppendLine(",");
            builder.Append("  \"rockTextureLinks\": ").Append(report.RockTextureLinks).AppendLine(",");
            builder.Append("  \"sandTextureLinks\": ").Append(report.SandTextureLinks).AppendLine(",");
            builder.Append("  \"siltTextureLinks\": ").Append(report.SiltTextureLinks).AppendLine();
            builder.AppendLine("}");
            File.WriteAllText(ArtifactPath, builder.ToString());
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
            Silt
        }

        public struct IntegrationReport
        {
            public bool Success;
            public string GraphPath;
            public string Message;
            public bool CreatedTectonicNode;
            public bool CreatedErosionNode;
            public bool CreatedSplatNode;
            public bool HeightOutputLinked;
            public int RockTextureLinks;
            public int SandTextureLinks;
            public int SiltTextureLinks;

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
