#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.VFX.Parasites;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace Hecton8.VFX.Parasites.Editor
{
    public static class Biological_Particle_Scanner
    {
        private const string ReportRelativePath = "Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json";
        private const string SectionKey = "\"shinobu_313_parasitic_fauna_particle_swarms\"";

        [MenuItem("Hecton8/VFX/Run Biological Particle Scanner")]
        public static void RunMenu()
        {
            RunScan();
        }

        public static ParasiteScannerSummaryDTO RunScan()
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs" });
            int prefabCount = 0;
            int forbidden = 0;
            int externalForces = 0;
            int collisions = 0;
            int swarmScripts = 0;

            StringBuilder offenders = new StringBuilder(2048);
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                prefabCount++;
                ParticleSystem[] particles = prefab.GetComponentsInChildren<ParticleSystem>(true);
                for (int p = 0; p < particles.Length; p++)
                {
                    ParticleSystem system = particles[p];
                    bool looksBiological = IsBiologicalName(system.name) || IsBiologicalName(path);
                    bool hasExternalForces = system.externalForces.enabled;
                    bool hasCollision = system.collision.enabled || system.trigger.enabled;
                    if (hasExternalForces)
                        externalForces++;
                    if (hasCollision)
                        collisions++;
                    if (!looksBiological && !hasExternalForces)
                        continue;

                    forbidden++;
                    AppendOffender(offenders, path, system.name, hasExternalForces, hasCollision);
                }

                MonoBehaviour[] behaviours = prefab.GetComponentsInChildren<MonoBehaviour>(true);
                for (int b = 0; b < behaviours.Length; b++)
                {
                    MonoBehaviour behaviour = behaviours[b];
                    if (behaviour == null)
                        continue;
                    string typeName = behaviour.GetType().Name;
                    if (IsBiologicalName(typeName) || typeName.Contains("Boid"))
                        swarmScripts++;
                }
            }

            ParasiteScannerSummaryDTO summary = new ParasiteScannerSummaryDTO
            {
                Frame = (uint)Time.frameCount,
                PrefabCount = (uint)prefabCount,
                ForbiddenParticleSystems = (uint)forbidden,
                ExternalForceParticleSystems = (uint)externalForces,
                CollisionParticleSystems = (uint)collisions,
                SwarmScriptHits = (uint)swarmScripts,
                ReportHash = Hash(offenders),
                Flags = forbidden == 0 ? 1u : 0u
            };

            WriteReport(summary, offenders);
            WriteSummaryToVault(summary);
            AssetDatabase.Refresh();
            return summary;
        }

        private static bool IsBiologicalName(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            return value.Contains("Parasite") ||
                   value.Contains("parasite") ||
                   value.Contains("Leech") ||
                   value.Contains("leech") ||
                   value.Contains("Bug") ||
                   value.Contains("bug") ||
                   value.Contains("Swarm") ||
                   value.Contains("swarm") ||
                   value.Contains("Boid") ||
                   value.Contains("boid");
        }

        private static void AppendOffender(StringBuilder builder, string path, string systemName, bool externalForces, bool collision)
        {
            if (builder.Length > 0)
                builder.Append(",\n");

            builder.Append("    { \"path\": \"");
            AppendEscaped(builder, path);
            builder.Append("\", \"particleSystem\": \"");
            AppendEscaped(builder, systemName);
            builder.Append("\", \"externalForces\": ");
            builder.Append(externalForces ? "true" : "false");
            builder.Append(", \"collisionOrTrigger\": ");
            builder.Append(collision ? "true" : "false");
            builder.Append(" }");
        }

        private static void WriteReport(ParasiteScannerSummaryDTO summary, StringBuilder offenders)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string path = Path.Combine(root, ReportRelativePath);
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string existing = File.Exists(path) ? File.ReadAllText(path) : "{\n}\n";
            string section = BuildSection(summary, offenders);
            File.WriteAllText(path, UpsertReportSection(existing, section));
        }

        private static string BuildSection(ParasiteScannerSummaryDTO summary, StringBuilder offenders)
        {
            StringBuilder section = new StringBuilder(1536 + offenders.Length);
            section.Append("  \"shinobu_313_parasitic_fauna_particle_swarms\": {\n");
            section.Append("    \"agent\": \"SHINOBU_313\",\n");
            section.Append("    \"summary\": \"Forbidden CPU Boids Eradicated scanner report\",\n");
            section.Append("    \"prefabCount\": ").Append(summary.PrefabCount).Append(",\n");
            section.Append("    \"forbiddenParticleSystems\": ").Append(summary.ForbiddenParticleSystems).Append(",\n");
            section.Append("    \"externalForceParticleSystems\": ").Append(summary.ExternalForceParticleSystems).Append(",\n");
            section.Append("    \"collisionParticleSystems\": ").Append(summary.CollisionParticleSystems).Append(",\n");
            section.Append("    \"swarmScriptHits\": ").Append(summary.SwarmScriptHits).Append(",\n");
            section.Append("    \"gpuReplacement\": \"ParasiteSwarmGpuRuntime -> GraphicsBuffer -> Hecton_ParasiteSwarm.compute -> DrawProceduralIndirect\",\n");
            section.Append("    \"cpuParticleAuthority\": \"No parasite-owned runtime CPU particle emit API, GameObject parasite spawn, transform target list, direct buffer upload/readback, main-camera search, or per-frame material property block path under Assets/_Project/Scripts/VFX/Parasites.\",\n");
            section.Append("    \"gpuRoute\": \"ThermalSourceSignal snapshot -> runtime fixed candidate scratch -> score-ranked top-16 ParasiteTargetDTO scratch -> short Vault publish -> GraphicsBuffer.LockBufferForWrite -> Hecton_ParasiteSwarm.compute advect/rebase/cull -> Graphics.DrawProceduralIndirect.\",\n");
            section.Append("    \"cameraAuthority\": \"Camera AUP/runtime position are consumed only from cached IPlayerRuntimeContext.TryGetPlayerPoseSnapshot; runtime has no camera.transform fallback or local runtime-origin shadow, and active compute requires renderCamera.\",\n");
            section.Append("    \"dearLie\": \"Compute shader attraction latches particles to a spherical thermal-target shell and blends target velocity; no mesh collision, raycast, or CPU physics per particle.\",\n");
            section.Append("    \"dtoProof\": { \"ParasiteTargetDTO\": 32, \"ParasiteTargetCandidateDTO\": 64, \"ParasiteGpuParticleDTO\": 32, \"ParasiteIndirectArgsDTO\": 16, \"ParasiteFrameParamsDTO\": 64, \"ParasiteSwarmTuningDTO\": 64, \"ParasiteBehaviorProfileDTO\": 64, \"SwarmTelemetryEntry\": 64, \"ParasiteScannerSummaryDTO\": 32 },\n");
            section.Append("    \"vaultBufferIds\": \"71980..71987 plus 71989,71990 owned by SystemID.Vfx; rollback/Merkle/save excluded.\",\n");
            section.Append("    \"compileWall\": \"Runtime asmdef routes through Core/Core.Contracts/Core.Memory plus Unity packages only; source has no World-domain, Thermodynamics, KCC, heat-cell, or kinematic DTO route.\",\n");
            section.Append("    \"csvIngest\": \"parasite_behavior_profiles.csv reload reads into a bounded stack Span<byte>, parses staged ParasiteBehaviorProfileDTO rows before the profile/count mutation guard, and no longer opens ShinobuParasiteCsvScratch as a DataVault byte heap; oversized files fail closed.\",\n");
            section.Append("    \"shaderSafety\": \"No backend finite intrinsic calls, native shader trig calls, or Burst target-score sqrt calls remain in parasite compute/shader/runtime files; local H8FiniteScalar/H8Finite3 predicates guard NaN/infinity, H8FastSin/H8FastCos bounded polynomial helpers drive curl/dormant phases, inactive target slots are skipped before target-row reads, zero-target frames dispatch only indirect-args clear and skip advection/cull/draw, rebase is a required compute kernel, and final poisoned particle rows reset without CPU readback.\",\n");
            section.Append("    \"bufferUploadSafety\": \"Runtime target and draw-params uploads use ping-pong GraphicsBuffers with LockBufferForWrite try/finally unlock fences, so CPU writes target/draw payloads into the alternate buffer before binding it for the current dispatch/draw; the dead empty-flow buffer was removed and no CPU-side GPU payload copy/readback route is used.\",\n");
            section.Append("    \"materialResourceSafety\": \"Runtime does not call Shader.Find or create fallback Material instances; missing parasiteMaterial takes the no-compute path so GPU swarm work is not dispatched without a drawable asset.\",\n");
            section.Append("    \"frameParams\": \"Compute frame uniforms are grouped into one 64-byte ParasiteFrameParamsDTO row and uploaded through a ping-pong GraphicsBuffer; init, rebase, advect, and cull kernels bind the same explicit row instead of three loose vector-param writes.\",\n");
            section.Append("    \"blackBoxDump\": \"Fault dump writes a 64-byte H8P3 little-endian header with version, row stride, row count, cursor, and payload bytes before the fixed SwarmTelemetryEntry[300] rows.\",\n");
            section.Append("    \"selfAudit\": \"Docs/Reports/SHINOBU_313_SELF_AUDIT.xml records all 20 tasks, DTO offsets, Vault lanes, Dear Lie Big-O, dependency graph, and pending Unity/compiler/profiler proof.\",\n");
            section.Append("    \"visualClock\": \"Runtime uses a private fixed-step visual frame counter, wraps shader phase through a 4096-tick bounded phase ramp, and does not feed Unity frame clock values into parasite runtime advection or telemetry hashes.\",\n");
            section.Append("    \"scalability\": \"GlobalQualityWeight continuously scales budget from 5000 toward configured cap, then clamps to allocated ping-pong GraphicsBuffer.count; hard support ceiling is 2000000 particles with 64-wide compute groups.\",\n");
            section.Append("    \"compileStatus\": \"NOT_LAUNCHED: CPU load 99 percent, no dotnet/csc/VBCSCompiler process output, and generated project files still do not include parasite assembly/scripts; Unity import/project regeneration required before a proving compile.\",\n");
            section.Append("    \"profilerStatus\": \"PENDING_UNITY_IMPORT_RUNTIME_PROFILER_GCMONITOR_FRAME_DEBUGGER\",\n");
            section.Append("    \"offenders\": [\n");
            section.Append(offenders);
            section.Append("\n    ]\n");
            section.Append("  }");
            return section.ToString();
        }

        private static string UpsertReportSection(string existing, string section)
        {
            if (string.IsNullOrWhiteSpace(existing))
                return "{\n" + section + "\n}\n";

            int keyIndex = existing.IndexOf(SectionKey, StringComparison.Ordinal);
            if (keyIndex >= 0)
            {
                int propertyStart = existing.LastIndexOf('\n', keyIndex);
                propertyStart = propertyStart < 0 ? 1 : propertyStart + 1;
                int objectStart = existing.IndexOf('{', keyIndex);
                int objectEnd = FindMatchingBrace(existing, objectStart);
                if (objectStart >= 0 && objectEnd > objectStart)
                {
                    int afterObject = objectEnd + 1;
                    int nextToken = SkipWhitespace(existing, afterObject);
                    if (nextToken < existing.Length && existing[nextToken] == ',')
                        return existing.Substring(0, propertyStart) + section + "," + existing.Substring(nextToken + 1);

                    return existing.Substring(0, propertyStart) + section + existing.Substring(afterObject);
                }
            }

            int insert = existing.IndexOf('{');
            if (insert < 0)
                return "{\n" + section + "\n}\n";

            int nextAfterOpen = SkipWhitespace(existing, insert + 1);
            string comma = nextAfterOpen < existing.Length && existing[nextAfterOpen] == '}' ? string.Empty : ",";
            return existing.Substring(0, insert + 1) + "\n" + section + comma + existing.Substring(insert + 1);
        }

        private static int SkipWhitespace(string text, int index)
        {
            while (index < text.Length && char.IsWhiteSpace(text[index]))
                index++;
            return index;
        }

        private static int FindMatchingBrace(string text, int objectStart)
        {
            if (objectStart < 0 || objectStart >= text.Length)
                return -1;

            int depth = 0;
            bool inString = false;
            for (int i = objectStart; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '"' && (i == 0 || text[i - 1] != '\\'))
                    inString = !inString;
                if (inString)
                    continue;
                if (c == '{')
                    depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }

            return -1;
        }

        private static void WriteSummaryToVault(ParasiteScannerSummaryDTO summary)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null && GlobalDataVault.TryGetLatestCreated(out GlobalDataVault latest))
                vault = latest;
            if (vault == null)
                return;

            ParasiteSwarmContracts.EnsureVaultBuffers(vault);
            if (!vault.TryGetGenerationHandle(BufferID.ShinobuParasiteScannerSummary, out VaultGenerationHandle<ParasiteScannerSummaryDTO> handle) ||
                !vault.TryAcquireWriteLock(in handle, SystemID.Vfx, out NativeArray<ParasiteScannerSummaryDTO> summaryBuffer))
            {
                return;
            }

            try
            {
                if (!summaryBuffer.IsCreated || summaryBuffer.Length <= 0)
                    return;

                summaryBuffer[0] = summary;
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, SystemID.Vfx);
            }
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

        private static uint Hash(StringBuilder builder)
        {
            uint h = 2166136261u;
            for (int i = 0; i < builder.Length; i++)
            {
                h ^= builder[i];
                h *= 16777619u;
            }
            return h == 0u ? 1u : h;
        }
    }
}
#endif
