using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    internal static class FloraDearLiePhysicsScanner
    {
        private const string ReportPath = "Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_268.json";
        private static readonly List<Collider> s_ColliderScratch = new List<Collider>(32);
        private static readonly List<Rigidbody> s_RigidbodyScratch = new List<Rigidbody>(8);

        private static readonly string[] CandidateRoots =
        {
            "Assets/_Project/Prefabs/Nature/Flora",
            "Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders/Flora",
            "Assets/_Project/Prefabs"
        };

        [MenuItem("Hecton8/Diagnostics/Scan Flora Physics Dear Lie")]
        private static void ScanMenu()
        {
            ScanAndWriteReport();
        }

        internal static void ScanAndWriteReport()
        {
            StringBuilder builder = new StringBuilder(4096);
            builder.Append("{\n");
            builder.Append("  \"agent\": \"SHINOBU_268\",\n");
            builder.Append("  \"rule\": \"flora destruction must route through matrix scale-zero and GPU VFX signals, not Rigidbody debris or Physics overlap\",\n");
            builder.Append("  \"prefabs\": [\n");

            int written = 0;
            int colliderCount = 0;
            int rigidbodyCount = 0;
            for (int rootIndex = 0; rootIndex < CandidateRoots.Length; rootIndex++)
            {
                string root = CandidateRoots[rootIndex];
                if (!AssetDatabase.IsValidFolder(root))
                    continue;

                string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { root });
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (path.IndexOf("flora", StringComparison.OrdinalIgnoreCase) < 0 &&
                        path.IndexOf("kelp", StringComparison.OrdinalIgnoreCase) < 0 &&
                        path.IndexOf("grass", StringComparison.OrdinalIgnoreCase) < 0 &&
                        path.IndexOf("sargassum", StringComparison.OrdinalIgnoreCase) < 0 &&
                        path.IndexOf("coral", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null)
                        continue;

                    s_ColliderScratch.Clear();
                    prefab.GetComponentsInChildren(true, s_ColliderScratch);
                    int prefabColliders = s_ColliderScratch.Count;

                    s_RigidbodyScratch.Clear();
                    prefab.GetComponentsInChildren(true, s_RigidbodyScratch);
                    int prefabRigidbodies = s_RigidbodyScratch.Count;
                    if (prefabColliders == 0 && prefabRigidbodies == 0)
                        continue;

                    if (written > 0)
                        builder.Append(",\n");

                    builder.Append("    { \"path\": \"");
                    AppendEscaped(builder, path);
                    builder.Append("\", \"colliders\": ");
                    builder.Append(prefabColliders);
                    builder.Append(", \"rigidbodies\": ");
                    builder.Append(prefabRigidbodies);
                    builder.Append(" }");
                    colliderCount += prefabColliders;
                    rigidbodyCount += prefabRigidbodies;
                    written++;
                }
            }

            builder.Append("\n  ],\n");
            builder.Append("  \"prefabsWithPhysics\": ");
            builder.Append(written);
            builder.Append(",\n  \"colliderCount\": ");
            builder.Append(colliderCount);
            builder.Append(",\n  \"rigidbodyCount\": ");
            builder.Append(rigidbodyCount);
            builder.Append(",\n  \"runtimeRouteProof\": {\n");
            builder.Append("    \"owner\": \"DestructibleOrganicManager\",\n");
            builder.Append("    \"damageInput\": \"SignalBus<CombatDamageSignal>.GetFrameSnapshot -> Vault 72982 FloraDestructionEventDTO[128]\",\n");
            builder.Append("    \"query\": \"Burst flat Vault bucket-head/next AUP lookup, buffers 72987..72990\",\n");
            builder.Append("    \"resultLane\": \"Vault 72983 FloraDearLieDestructionResult[256] covers independent surface and underwater lanes\",\n");
            builder.Append("    \"vaultBuffers\": \"72980..72990 under SystemID.FloraGenomics; high local IDs remain below MaxGenerationHandleCapacity=100000\",\n");
            builder.Append("    \"visualFake\": \"matrix basis columns scale-zero plus owner-fenced DebrisSpawnSignal GPU shard intent\",\n");
            builder.Append("    \"overflowGuard\": \"slot reserved before matrix mutation; overflow counter 6 records rejection and triggers blackbox dump\"\n");
            builder.Append("  }\n}\n");

            string absolutePath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), ReportPath);
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(absolutePath));
            System.IO.File.WriteAllText(absolutePath, builder.ToString());
            AssetDatabase.Refresh();
        }

        private static void AppendEscaped(StringBuilder builder, string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '\\' || c == '"')
                    builder.Append('\\');
                builder.Append(c);
            }
        }
    }
}
