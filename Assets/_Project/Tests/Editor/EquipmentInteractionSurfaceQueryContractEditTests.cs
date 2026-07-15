using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class EquipmentInteractionSurfaceQueryContractEditTests
    {
        [Test]
        public void PrimarySurfaceHitAttemptsCurrentFrameResolveBeforeFrameLatentFallback()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Interaction/EquipmentInteractionHandler.cs");
            string requestBody = ExtractMethodBody(source, "public bool RequestPrimarySurfaceHit(ulong requesterId, Vector3 origin");

            StringAssert.Contains("InteractionSurfaceQueryDTO request = CreateSurfaceQueryRequest(origin, normalizedDirection, range, layerMask, queryTriggerInteraction);", requestBody);
            StringAssert.Contains("bool hasCurrentHit = TryResolveKinematicSurfaceHit(in request, out InteractionSurfaceHit currentHit);", requestBody);
            StringAssert.Contains("QueuePrimarySurfaceQuery(requesterId, in request);", requestBody);
            StringAssert.Contains("if (hasCurrentHit)", requestBody);
            StringAssert.Contains("if (hasCompletedHit)", requestBody);
            Assert.IsTrue(ContainsTokensInOrder(
                requestBody,
                "bool hasCompletedHit = TryGetCompletedSurfaceHit",
                "InteractionSurfaceQueryDTO request = CreateSurfaceQueryRequest",
                "bool hasCurrentHit = TryResolveKinematicSurfaceHit(in request",
                "QueuePrimarySurfaceQuery(requesterId, in request);",
                "if (hasCurrentHit)",
                "hit = currentHit;",
                "if (hasCompletedHit)",
                "hit = completedHit;"));
        }

        [Test]
        public void KinematicSurfaceResolveMergesColliderSdfAndTerrainCandidates()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Interaction/EquipmentInteractionHandler.cs");
            string resolveBody = ExtractMethodBody(source, "private bool TryResolveKinematicSurfaceHit(in InteractionSurfaceQueryDTO request");
            string sdfBody = ExtractMethodBody(source, "private bool TryResolveSdfSurfaceHit(");
            string terrainBody = ExtractMethodBody(source, "private bool TryResolveTerrainSurfaceHit(");
            string selectBody = ExtractMethodBody(source, "private static void TrySelectNearestSurfaceHit(");

            StringAssert.Contains("InteractableRegistry.TryResolveSpatialTarget", resolveBody);
            StringAssert.Contains("ResolveQueryTriggerInteraction(request.TriggerMode)", resolveBody);
            StringAssert.Contains("InteractionSurfaceHit.FromSurface(", resolveBody);
            StringAssert.Contains("spatialHit.Collider", resolveBody);
            StringAssert.Contains("TryResolveSdfSurfaceHit(request.Origin, normalizedDirection, request.Range, request.LayerMask", resolveBody);
            StringAssert.Contains("TryResolveTerrainSurfaceHit(request.Origin, normalizedDirection, request.Range, request.LayerMask", resolveBody);
            StringAssert.Contains("TrySelectNearestSurfaceHit", resolveBody);
            Assert.IsFalse(resolveBody.Contains("return TryResolveTerrainSurfaceHit(request.Origin", StringComparison.Ordinal));

            StringAssert.Contains("HectonLayerMasks.VoxelCave", sdfBody);
            StringAssert.Contains("HectonLayerMasks.VoxelProxy", sdfBody);
            StringAssert.Contains("hit = InteractionSurfaceHit.FromSurface(", sdfBody);
            Assert.IsFalse(sdfBody.Contains("hit.point =", StringComparison.Ordinal));
            Assert.IsFalse(sdfBody.Contains("hit.normal =", StringComparison.Ordinal));
            Assert.IsFalse(sdfBody.Contains("hit.distance =", StringComparison.Ordinal));

            StringAssert.Contains("hit = InteractionSurfaceHit.FromSurface(point, normal, distance, null, HectonLayerMasks.Terrain);", terrainBody);
            Assert.IsFalse(terrainBody.Contains("hit.point =", StringComparison.Ordinal));
            Assert.IsFalse(terrainBody.Contains("hit.normal =", StringComparison.Ordinal));
            Assert.IsFalse(terrainBody.Contains("hit.distance =", StringComparison.Ordinal));

            StringAssert.Contains("candidate.distance > bestDistance", selectBody);
            StringAssert.Contains("bestHit = candidate;", selectBody);
            StringAssert.Contains("bestDistance = candidate.distance;", selectBody);
            StringAssert.Contains("hasBestHit = true;", selectBody);
        }

        [Test]
        public void InteractionSignalServiceSurfaceHitContractRemainsStable()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs");
            string serviceBody = ExtractInterfaceBody(source, "public interface IInteractionSignalService");

            StringAssert.Contains("bool RequestPrimarySurfaceHit(ulong requesterId, in Hecton8.Interaction.InteractionPacket packet, int layerMask, QueryTriggerInteraction queryTriggerInteraction, out InteractionSurfaceHit hit);", serviceBody);
            StringAssert.Contains("bool RequestPrimarySurfaceHit(ulong requesterId, Vector3 origin, Vector3 direction, float range, int layerMask, QueryTriggerInteraction queryTriggerInteraction, out InteractionSurfaceHit hit);", serviceBody);
            Assert.AreEqual(2, CountToken(serviceBody, "RequestPrimarySurfaceHit("));
            Assert.AreEqual(0, CountToken(serviceBody, "SurfaceHitStatus"));
        }

        private static string ReadProjectFile(string relativePath)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return File.ReadAllText(Path.Combine(root, relativePath));
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(signatureIndex, 0, "Missing method signature: " + signature);
            int open = source.IndexOf('{', signatureIndex);
            Assert.GreaterOrEqual(open, 0, "Missing method open brace: " + signature);
            return ExtractBraceBody(source, open, signature);
        }

        private static string ExtractInterfaceBody(string source, string signature)
        {
            int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(signatureIndex, 0, "Missing interface signature: " + signature);
            int open = source.IndexOf('{', signatureIndex);
            Assert.GreaterOrEqual(open, 0, "Missing interface open brace: " + signature);
            return ExtractBraceBody(source, open, signature);
        }

        private static string ExtractBraceBody(string source, int open, string signature)
        {
            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '{')
                    depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(open, i - open + 1);
                }
            }

            Assert.Fail("Missing close brace: " + signature);
            return string.Empty;
        }

        private static int CountToken(string source, string token)
        {
            int count = 0;
            int index = 0;
            while (true)
            {
                index = source.IndexOf(token, index, StringComparison.Ordinal);
                if (index < 0)
                    return count;

                count++;
                index += token.Length;
            }
        }

        private static bool ContainsTokensInOrder(string source, params string[] tokens)
        {
            int index = 0;
            foreach (string token in tokens)
            {
                int next = source.IndexOf(token, index, StringComparison.Ordinal);
                if (next < 0)
                    return false;

                index = next + token.Length;
            }

            return true;
        }
    }
}
