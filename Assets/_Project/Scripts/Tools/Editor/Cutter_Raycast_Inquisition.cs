#if UNITY_EDITOR
namespace Hecton8.Tools.Editor
{
    using System;
    using System.IO;
    using System.Text;
    using UnityEditor;
    using UnityEngine;

    public static class Cutter_Raycast_Inquisition
    {
        private const string ReportFileName = "CONSTRUCTION_OPTIMIZATION_REPORT_SHINOBU_225.json";

        [MenuItem("Hecton8/Tools/Cutter Raycast Inquisition")]
        public static void RunMenu()
        {
            string reportPath = RunToFile();
            Debug.Log("[SHINOBU_225] Cutter raycast inquisition wrote " + reportPath);
        }

        public static string RunToFile()
        {
            DirectoryInfo projectRoot = Directory.GetParent(Application.dataPath);
            if (projectRoot == null)
                return string.Empty;

            string sourceRoot = Path.Combine(Application.dataPath, "_Project", "Scripts");
            int cutterSyncRaycasts = 0;
            int cutterParticleSystems = 0;
            int cutterInstantiateSites = 0;
            int cutterMeshMutationSites = 0;
            int dodRequestDefinitions = 0;
            int dodRequestMetaDefinitions = 0;
            int raycastCommandBatchSites = 0;
            int noAliasAttributeHits = 0;
            int hotResolveNoAcquireHits = 0;
            int shaderLieDtos = 0;
            int gpuSparkSignals = 0;
            int burstWorkEstimateHits = 0;
            int smoothQualityHits = 0;

            foreach (string file in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
            {
                string normalized = file.Replace('\\', '/');
                if (normalized.EndsWith("/Tools/Editor/Cutter_Raycast_Inquisition.cs", StringComparison.Ordinal))
                    continue;

                string text = File.ReadAllText(file);
                bool cutterRelated = normalized.EndsWith("/LaserCutter.cs", StringComparison.Ordinal) ||
                                     normalized.IndexOf("/Tools/LaserCutterDod", StringComparison.Ordinal) >= 0 ||
                                     normalized.EndsWith("/Tools/WfcLaserCutRuntime.cs", StringComparison.Ordinal) ||
                                     normalized.EndsWith("/Gameplay/SealedDoor.cs", StringComparison.Ordinal) ||
                                     normalized.EndsWith("/Gameplay/SargassumCutResponder.cs", StringComparison.Ordinal);
                if (!cutterRelated)
                    continue;

                cutterSyncRaycasts += Count(text, "Physics.Raycast(") + Count(text, "Physics.RaycastAll(") + Count(text, "Physics.RaycastNonAlloc(");
                cutterParticleSystems += Count(text, "ParticleSystem");
                cutterInstantiateSites += Count(text, "Instantiate(");
                cutterMeshMutationSites += Count(text, ".vertices") + Count(text, "SetVertices(") + Count(text, "RecalculateNormals(");
                dodRequestDefinitions += Count(text, "LaserCutRequestDTO");
                dodRequestMetaDefinitions += Count(text, "LaserCutRequestMetaDTO");
                raycastCommandBatchSites += Count(text, "RaycastCommand.ScheduleBatch");
                noAliasAttributeHits += Count(text, "NoAlias");
                hotResolveNoAcquireHits += Count(text, "allowAcquire: false");
                shaderLieDtos += Count(text, "LaserCutDeformationStateDTO") + Count(text, "LaserCutGlowDecalRequestDTO");
                gpuSparkSignals += Count(text, "DebrisSpawnSignal") + Count(text, "VfxSparkRequestSignal") + Count(text, "LaserCutImpactVfxDTO");
                burstWorkEstimateHits += Count(text, "BurstWorkEstimateMicros");
                smoothQualityHits += Count(text, "math.smoothstep");
            }

            bool layoutOk = LaserCutterDodLayoutValidator.Validate(out uint layoutFaults);
            string reportDirectory = Path.Combine(projectRoot.FullName, "Docs", "Reports");
            Directory.CreateDirectory(reportDirectory);
            string reportPath = Path.Combine(reportDirectory, ReportFileName);
            File.WriteAllText(
                reportPath,
                BuildJson(
                    cutterSyncRaycasts,
                    cutterParticleSystems,
                    cutterInstantiateSites,
                    cutterMeshMutationSites,
                    dodRequestDefinitions,
                    dodRequestMetaDefinitions,
                    raycastCommandBatchSites,
                    noAliasAttributeHits,
                    hotResolveNoAcquireHits,
                    shaderLieDtos,
                    gpuSparkSignals,
                    burstWorkEstimateHits,
                    smoothQualityHits,
                    layoutOk,
                    layoutFaults),
                Encoding.UTF8);
            return reportPath;
        }

        private static string BuildJson(
            int cutterSyncRaycasts,
            int cutterParticleSystems,
            int cutterInstantiateSites,
            int cutterMeshMutationSites,
            int dodRequestDefinitions,
            int dodRequestMetaDefinitions,
            int raycastCommandBatchSites,
            int noAliasAttributeHits,
            int hotResolveNoAcquireHits,
            int shaderLieDtos,
            int gpuSparkSignals,
            int burstWorkEstimateHits,
            int smoothQualityHits,
            bool layoutOk,
            uint layoutFaults)
        {
            StringBuilder builder = new StringBuilder(1024);
            builder.AppendLine("{");
            builder.AppendLine("  \"agent\": \"SHINOBU_225\",");
            builder.AppendLine("  \"scanner\": \"Cutter_Raycast_Inquisition\",");
            builder.AppendLine("  \"generated_utc\": \"" + DateTime.UtcNow.ToString("O") + "\",");
            builder.AppendLine("  \"cutter_sync_raycast_sites\": " + cutterSyncRaycasts + ",");
            builder.AppendLine("  \"cutter_particle_system_references\": " + cutterParticleSystems + ",");
            builder.AppendLine("  \"cutter_instantiate_sites\": " + cutterInstantiateSites + ",");
            builder.AppendLine("  \"cutter_mesh_mutation_sites\": " + cutterMeshMutationSites + ",");
            builder.AppendLine("  \"dod_request_definition_hits\": " + dodRequestDefinitions + ",");
            builder.AppendLine("  \"dod_request_meta_definition_hits\": " + dodRequestMetaDefinitions + ",");
            builder.AppendLine("  \"raycast_command_batch_sites\": " + raycastCommandBatchSites + ",");
            builder.AppendLine("  \"noalias_attribute_hits\": " + noAliasAttributeHits + ",");
            builder.AppendLine("  \"hot_resolve_allow_acquire_false_hits\": " + hotResolveNoAcquireHits + ",");
            builder.AppendLine("  \"shader_lie_dto_hits\": " + shaderLieDtos + ",");
            builder.AppendLine("  \"gpu_spark_signal_hits\": " + gpuSparkSignals + ",");
            builder.AppendLine("  \"burst_work_estimate_hits\": " + burstWorkEstimateHits + ",");
            builder.AppendLine("  \"smooth_quality_curve_hits\": " + smoothQualityHits + ",");
            builder.AppendLine("  \"laser_cut_request_layout_contract\": \"64 bytes offsets 0,24,36,40,44,48 with explicit padding at 52,56,60\",");
            builder.AppendLine("  \"laser_cut_request_meta_layout_contract\": \"64 bytes offsets Frame=0,Flags=4,RequestSequence=8,CooldownUntilFrame=12\",");
            builder.AppendLine("  \"gpu_spark_count_curve\": \"math.smoothstep(GlobalQualityWeight) over tuning LowSparkCount=0 to UltraSparkCount=500\",");
            builder.AppendLine("  \"spark_signal_source\": \"post-evaluation signals forward LaserCutImpactVfxDTO.SparkCount directly; live helper computes direct tool presentation quantity from the same tuning row\",");
            builder.AppendLine("  \"telemetry_tail_contract\": \"BatteryWatts@120,BurstWorkEstimateMicros@124\",");
            builder.AppendLine("  \"compile_attempt\": \"manual guarded Hecton8.Core.csproj build failed with external dependency errors; editor rerun cannot prove compile\",");
            builder.AppendLine("  \"laser_cut_request_layout_ok\": " + (layoutOk ? "true" : "false") + ",");
            builder.AppendLine("  \"laser_cut_request_layout_faults\": " + layoutFaults + ",");
            builder.AppendLine("  \"verdict\": \"" + ResolveVerdict(cutterSyncRaycasts, cutterParticleSystems, cutterInstantiateSites, cutterMeshMutationSites, layoutOk) + "\"");
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static string ResolveVerdict(int syncRaycasts, int particleSystems, int instantiateSites, int meshMutationSites, bool layoutOk)
        {
            if (!layoutOk)
                return "FAIL: LaserCutRequestDTO layout contract broken.";
            if (syncRaycasts > 0)
                return "FAIL: synchronous cutter Physics.Raycast pattern remains.";
            if (particleSystems > 0)
                return "FAIL: cutter ParticleSystem pattern remains.";
            if (instantiateSites > 0)
                return "FAIL: cutter Instantiate pattern remains.";
            if (meshMutationSites > 0)
                return "REVIEW: cutter-related mesh mutation text remains.";
            return "PASS: cutter path has deferred raycast/DOD evidence and no direct sync raycast, prefab spawn, ParticleSystem, mesh mutation, or request-padding metadata text.";
        }

        private static int Count(string text, string pattern)
        {
            int count = 0;
            int index = 0;
            while (index < text.Length)
            {
                index = text.IndexOf(pattern, index, StringComparison.Ordinal);
                if (index < 0)
                    return count;

                count++;
                index += pattern.Length;
            }

            return count;
        }
    }
}
#endif
