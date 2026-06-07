using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class BuildInfoPreprocessProcessLifecycleEditTests
    {
        [Test]
        public void GitMetadataProcess_UsesBoundedAsyncOutputDrain()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Editor/Build/BuildInfoPreprocess.cs");
            string runBody = ExtractMethodBody(source, "private static string RunGit(string arguments)");
            string startBody = ExtractMethodBody(source, "private static Process TryStartGitMetadataProcessNoThrow(ProcessStartInfo info)");
            string waitBody = ExtractMethodBody(source, "private static bool TryWaitForGitMetadataProcess(Process process)");
            string drainBody = ExtractMethodBody(source, "private static void WaitForGitMetadataOutputDrain(Task<string> outputTask, Task<string> errorTask)");
            string readOutputBody = ExtractMethodBody(source, "private static string ReadProcessOutputTaskNoThrow(Task<string> task)");
            string readExitBody = ExtractMethodBody(source, "private static int ReadProcessExitCodeNoThrow(Process process)");
            string killBody = ExtractMethodBody(source, "private static void KillGitMetadataProcessNoThrow(Process process)");

            StringAssert.Contains("using System.Threading.Tasks;", source);
            StringAssert.Contains("GitMetadataTimeoutMilliseconds = 2000", source);
            StringAssert.Contains("GitMetadataOutputDrainMilliseconds = 500", source);
            StringAssert.DoesNotContain("StandardOutput.ReadToEnd();", source);
            StringAssert.DoesNotContain("StandardError.ReadToEnd();", source);
            StringAssert.DoesNotContain("process.WaitForExit(2000)", source);
            StringAssert.DoesNotContain("process.ExitCode == 0", runBody);

            StringAssert.Contains("TryStartGitMetadataProcessNoThrow(info)", runBody);
            StringAssert.Contains("process.StandardOutput.ReadToEndAsync();", runBody);
            StringAssert.Contains("process.StandardError.ReadToEndAsync();", runBody);
            StringAssert.Contains("TryWaitForGitMetadataProcess(process)", runBody);
            StringAssert.Contains("WaitForGitMetadataOutputDrain(outputTask, errorTask);", runBody);
            StringAssert.Contains("ReadProcessOutputTaskNoThrow(outputTask)", runBody);
            StringAssert.Contains("ReadProcessExitCodeNoThrow(process) == 0", runBody);

            StringAssert.Contains("return Process.Start(info);", startBody);
            StringAssert.Contains("catch (Exception)", startBody);
            StringAssert.Contains("process.WaitForExit(GitMetadataTimeoutMilliseconds)", waitBody);
            StringAssert.Contains("Task.WaitAll(new Task[] { outputTask, errorTask }, GitMetadataOutputDrainMilliseconds);", drainBody);
            StringAssert.Contains("task.IsCompleted", readOutputBody);
            StringAssert.Contains("return task.Result ?? string.Empty;", readOutputBody);
            StringAssert.Contains("return process.ExitCode;", readExitBody);
            StringAssert.Contains("if (!process.HasExited)", killBody);
            StringAssert.Contains("process.Kill();", killBody);
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
