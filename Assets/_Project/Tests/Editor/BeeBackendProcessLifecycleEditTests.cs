using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class BeeBackendProcessLifecycleEditTests
    {
        [Test]
        public void HectonBuildDaemon_UsesPerProcessNoThrowBeeLifecycle()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Editor/HectonBuildDaemon.cs");
            string probeBody = ExtractMethodBody(source, "private static bool TryGetBeeBackendCpuSeconds(out double cpuSeconds)");
            string killLoopBody = ExtractMethodBody(source, "private static int KillBeeBackends()");
            string cpuBody = ExtractMethodBody(source, "private static bool TryReadBeeBackendCpuSeconds(Process process, out double cpuSeconds)");
            string killBody = ExtractMethodBody(source, "private static bool TryKillBeeBackendNoThrow(Process process, string logPrefix)");
            string disposeBody = ExtractMethodBody(source, "private static void DisposeBeeBackendProcessNoThrow(Process process)");

            StringAssert.Contains("BeeBackendKillWaitMilliseconds = 2000", source);
            StringAssert.DoesNotContain("using (Process process = processes[i])", source);
            StringAssert.Contains("TryReadBeeBackendCpuSeconds(process, out double processCpuSeconds)", probeBody);
            StringAssert.Contains("cpuSeconds += processCpuSeconds;", probeBody);
            StringAssert.Contains("DisposeBeeBackendProcessNoThrow(process);", probeBody);
            StringAssert.DoesNotContain("process.TotalProcessorTime.TotalSeconds", probeBody);
            StringAssert.Contains("catch (Exception exception)", cpuBody);

            StringAssert.Contains("TryKillBeeBackendNoThrow(process, \"[HectonBuildDaemon] bee_backend kill failed: \")", killLoopBody);
            StringAssert.Contains("DisposeBeeBackendProcessNoThrow(process);", killLoopBody);
            StringAssert.DoesNotContain("process.Kill();", killLoopBody);
            StringAssert.DoesNotContain("process.WaitForExit(2000);", killLoopBody);
            StringAssert.Contains("if (process.HasExited)", killBody);
            StringAssert.Contains("process.Kill();", killBody);
            StringAssert.Contains("process.WaitForExit(BeeBackendKillWaitMilliseconds);", killBody);
            StringAssert.Contains("catch (Exception exception)", killBody);
            StringAssert.Contains("process.Dispose();", disposeBody);
            StringAssert.Contains("catch (Exception exception)", disposeBody);
        }

        [Test]
        public void H8PlayModeSentinel_UsesPerProcessNoThrowBeeLifecycle()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Editor/H8PlayModeSentinel.cs");
            string killLoopBody = ExtractMethodBody(source, "private static int KillBeeBackends()");
            string killBody = ExtractMethodBody(source, "private static bool TryKillBeeBackendNoThrow(Process process)");
            string disposeBody = ExtractMethodBody(source, "private static void DisposeBeeBackendProcessNoThrow(Process process)");

            StringAssert.Contains("BeeBackendKillWaitMilliseconds = 2000", source);
            StringAssert.DoesNotContain("using (Process process = processes[i])", source);
            StringAssert.Contains("TryKillBeeBackendNoThrow(process)", killLoopBody);
            StringAssert.Contains("DisposeBeeBackendProcessNoThrow(process);", killLoopBody);
            StringAssert.DoesNotContain("process.Kill();", killLoopBody);
            StringAssert.DoesNotContain("process.WaitForExit(2000);", killLoopBody);
            StringAssert.Contains("if (process.HasExited)", killBody);
            StringAssert.Contains("process.Kill();", killBody);
            StringAssert.Contains("process.WaitForExit(BeeBackendKillWaitMilliseconds);", killBody);
            StringAssert.Contains("catch (Exception exception)", killBody);
            StringAssert.Contains("process.Dispose();", disposeBody);
            StringAssert.Contains("catch (Exception exception)", disposeBody);
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

            Assert.Fail("Missing method close brace: " + signature);
            return string.Empty;
        }
    }
}
