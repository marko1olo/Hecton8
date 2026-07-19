using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Graphing;
using UnityEditor.Graphing.Util;
using UnityEditor.Rendering;
using UnityEditor.ShaderGraph.Drawing;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace UnityEditor.ShaderGraph
{
    static class VirtualTexturingFeedbackUtils
    {
        // TODO: could get rid of this if we could run a codegen prepass (with proper keyword #ifdef)
        public static void GenerateVirtualTextureFeedback(
            List<AbstractMaterialNode> downstreamNodesIncludingRoot,
            List<int>[] keywordPermutationsPerNode,
            ShaderStringBuilder surfaceDescriptionFunction,
            KeywordCollector shaderKeywords)
        {
            // A note on how we handle vt feedback in combination with keywords:
            // We essentially generate a fully separate feedback path for each permutation of keywords
            // so per permutation we gather variables contribution to feedback and we generate
            // feedback gathering for each permutation individually.

            var feedbackVariablesPerPermutation = PooledList<PooledList<string>>.Get();
            try
            {
                if (shaderKeywords.permutations.Count >= 1)
                {
                    for (int i = 0; i < shaderKeywords.permutations.Count; i++)
                    {
                        feedbackVariablesPerPermutation.Add(PooledList<string>.Get());
                    }
                }
                else
                {
                    // Create a dummy single permutation
                    feedbackVariablesPerPermutation.Add(PooledList<string>.Get());
                }

                int index = 0; //for keywordPermutationsPerNode
                foreach (var node in downstreamNodesIncludingRoot)
                {
                    if (node is IGeneratesVirtualTextureFeedback vtFeedbackNode)
                    {
                        var vars = vtFeedbackNode.GetFeedbackVariables();
                        if (keywordPermutationsPerNode[index] == null)
                        {
                            feedbackVariablesPerPermutation[0].AddRange(vars);
                        }
                        else
                        {
                            foreach (var feedbackVar in vars)
                            {
                                foreach (int perm in keywordPermutationsPerNode[index])
                                {
                                    feedbackVariablesPerPermutation[perm].Add(feedbackVar);
                                }
                            }
                        }
                    }

                    index++;
                }

                index = 0;
                foreach (var feedbackVariables in feedbackVariablesPerPermutation)
                {
                    // If it's a dummy single always-on permutation don't put an ifdef around the code
                    if (shaderKeywords.permutations.Count >= 1)
                    {
                        surfaceDescriptionFunction.AppendLine(KeywordUtil.GetKeywordPermutationConditional(index));
                    }

                    using (surfaceDescriptionFunction.BlockScope())
                    {
                        if (feedbackVariables.Count == 0)
                        {
                            string feedBackCode = "surface.VTPackedFeedback = float4(1.0f,1.0f,1.0f,1.0f);";
                            surfaceDescriptionFunction.AppendLine(feedBackCode);
                        }
                        else if (feedbackVariables.Count == 1)
                        {
                            string feedBackCode = "surface.VTPackedFeedback = GetPackedVTFeedback(" + feedbackVariables[0] + ");";
                            surfaceDescriptionFunction.AppendLine(feedBackCode);
                        }
                        else if (feedbackVariables.Count > 1)
                        {
                            surfaceDescriptionFunction.AppendLine("float4 VTFeedback_array[" + feedbackVariables.Count + "];");

                            int arrayIndex = 0;
                            foreach (var variable in feedbackVariables)
                            {
                                surfaceDescriptionFunction.AppendLine("VTFeedback_array[" + arrayIndex + "] = " + variable + ";");
                                arrayIndex++;
                            }

                            // TODO: should read from NDCPosition instead...
                            surfaceDescriptionFunction.AppendLine("uint pixelColumn = (IN.ScreenPosition.x / IN.ScreenPosition.w) * _ScreenParams.x;");
                            surfaceDescriptionFunction.AppendLine(
                                "surface.VTPackedFeedback = GetPackedVTFeedback(VTFeedback_array[(pixelColumn + _FrameCount) % (uint)" + feedbackVariables.Count + "]);");
                        }
                    }

                    if (shaderKeywords.permutations.Count >= 1)
                    {
                        surfaceDescriptionFunction.AppendLine("#endif");
                    }

                    index++;
                }
            }
            finally
            {
                foreach (var list in feedbackVariablesPerPermutation)
                {
                    list.Dispose();
                }
                feedbackVariablesPerPermutation.Dispose();
            }
        }

        // Automatically add a  streaming feedback node and correctly connect it to stack samples are connected to it and it is connected to the master node output
        public static List<string> GetFeedbackVariables(SubGraphOutputNode masterNode)
        {
            var nodes = GraphUtil.FindDownStreamNodesOfType<IGeneratesVirtualTextureFeedback>(masterNode);

            List<string> result = new List<string>();

            foreach (var node in nodes)
            {
                result.AddRange(node.GetFeedbackVariables());
            }

            return result;
        }
    }
}
