using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class DataMonolithHotReloadSocketLifecycleEditTests
    {
        [Test]
        public void HotReloadSocketUsesBoundedNoThrowThreadLifecycle()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Editor/DataMonolith/H8DataMonolithCompiler.cs");
            string startBody = ExtractMethodBody(source, "private static void Start()");
            string stopBody = ExtractMethodBody(source, "private static void Stop()");
            string listenBody = ExtractMethodBody(source, "private static void ListenLoop()");
            string stopListenerBody = ExtractMethodBody(source, "private static void StopListenerNoThrow(TcpListener listener)");
            string joinBody = ExtractMethodBody(source, "private static bool TryJoinHotReloadThreadNoThrow(Thread thread)");
            string cleanupBody = ExtractMethodBody(source, "private static void CleanupExitedHotReloadThread(Thread thread)");

            StringAssert.Contains("HotReloadThreadJoinMilliseconds = 1000", source);
            StringAssert.Contains("private static readonly object LifecycleLock", source);
            StringAssert.Contains("if (existingThread != null && !existingThread.IsAlive)", startBody);
            StringAssert.Contains("if (_thread != null && _thread.IsAlive)", startBody);
            StringAssert.Contains("_thread = thread;", startBody);
            StringAssert.Contains("thread.Start();", startBody);
            StringAssert.Contains("HandleStartFailure(ex);", startBody);

            StringAssert.Contains("StopListenerNoThrow(listener);", stopBody);
            StringAssert.Contains("TryJoinHotReloadThreadNoThrow(thread)", stopBody);
            StringAssert.Contains("ReferenceEquals(_thread, thread)", stopBody);
            StringAssert.Contains("_thread = null;", stopBody);

            StringAssert.Contains("listener.Stop();", stopListenerBody);
            StringAssert.Contains("catch (SocketException)", stopListenerBody);
            StringAssert.Contains("catch (ObjectDisposedException)", stopListenerBody);
            StringAssert.Contains("catch (InvalidOperationException)", stopListenerBody);

            StringAssert.Contains("ReferenceEquals(Thread.CurrentThread, thread)", joinBody);
            StringAssert.Contains("thread.Join(HotReloadThreadJoinMilliseconds);", joinBody);
            StringAssert.Contains("return !thread.IsAlive;", joinBody);
            StringAssert.Contains("catch (Exception)", joinBody);

            StringAssert.Contains("finally", listenBody);
            StringAssert.Contains("CleanupExitedHotReloadThread(Thread.CurrentThread);", listenBody);
            StringAssert.Contains("ReferenceEquals(_thread, thread)", cleanupBody);
            StringAssert.Contains("Interlocked.Exchange(ref _running, 0);", cleanupBody);
            StringAssert.Contains("StopListenerNoThrow(listener);", cleanupBody);
        }

        [Test]
        public void FileSystemWatcherUsesNoThrowStartStopLifecycle()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Editor/DataMonolith/H8DataMonolithCompiler.cs");
            string staticBody = ExtractMethodBody(source, "static H8DataMonolithFileSystemWatcher()");
            string startBody = ExtractMethodBody(source, "private static void StartWatcher()");
            string startForBody = ExtractMethodBody(source, "private static FileSystemWatcher TryStartWatcherFor(string absoluteSourceFolder)");
            string stopBody = ExtractMethodBody(source, "private static void StopWatcher(ref FileSystemWatcher watcher)");

            StringAssert.Contains("AssemblyReloadEvents.beforeAssemblyReload += StopWatcher;", staticBody);
            StringAssert.Contains("EditorApplication.quitting += StopWatcher;", staticBody);
            StringAssert.Contains("TryStartWatcherFor(Path.GetFullPath(H8DataMonolithCompiler.SourceFolder))", startBody);
            StringAssert.Contains("TryStartWatcherFor(Path.GetFullPath(\"Data/Balance\"))", startBody);

            StringAssert.Contains("Directory.CreateDirectory(absoluteSourceFolder);", startForBody);
            StringAssert.Contains("watcher.EnableRaisingEvents = true;", startForBody);
            StringAssert.Contains("catch (IOException ex)", startForBody);
            StringAssert.Contains("catch (UnauthorizedAccessException ex)", startForBody);
            StringAssert.Contains("return null;", startForBody);

            StringAssert.Contains("FileSystemWatcher activeWatcher = watcher;", stopBody);
            StringAssert.Contains("watcher = null;", stopBody);
            StringAssert.Contains("activeWatcher.EnableRaisingEvents = false;", stopBody);
            StringAssert.Contains("activeWatcher.Changed -= HandleSourceChanged;", stopBody);
            StringAssert.Contains("activeWatcher.Dispose();", stopBody);
            StringAssert.Contains("catch (ObjectDisposedException)", stopBody);
            StringAssert.Contains("catch (InvalidOperationException)", stopBody);
        }

        [Test]
        public void PythonProjectToolsUseBoundedAsyncOutputDrain()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Editor/DataMonolith/H8DataMonolithCompiler.cs");
            string runBody = ExtractMethodBody(source, "private static bool TryRunPythonProjectTool(string relativeToolPath, string label, out string summary)");
            string startBody = ExtractMethodBody(source, "private static Process TryStartPythonProjectToolNoThrow(ProcessStartInfo startInfo)");
            string waitBody = ExtractMethodBody(source, "private static bool TryWaitForPythonProjectTool(Process process, string label, out string summary)");
            string drainBody = ExtractMethodBody(source, "private static void WaitForPythonProjectToolOutputDrain(Task<string> outputTask, Task<string> errorTask)");
            string readOutputBody = ExtractMethodBody(source, "private static string ReadProcessOutputTaskNoThrow(Task<string> task)");
            string readExitBody = ExtractMethodBody(source, "private static int ReadProcessExitCodeNoThrow(Process process)");
            string killBody = ExtractMethodBody(source, "private static void KillPythonProjectToolNoThrow(Process process)");

            StringAssert.Contains("PythonToolTimeoutMilliseconds = 30000", source);
            StringAssert.Contains("PythonToolOutputDrainMilliseconds = 1000", source);
            StringAssert.DoesNotContain("process.StandardOutput.ReadToEnd();", source);
            StringAssert.DoesNotContain("process.StandardError.ReadToEnd();", source);
            StringAssert.DoesNotContain("process.WaitForExit();", runBody);
            StringAssert.DoesNotContain("process.ExitCode == 0", runBody);

            StringAssert.Contains("TryStartPythonProjectToolNoThrow(startInfo)", runBody);
            StringAssert.Contains("process.StandardOutput.ReadToEndAsync();", runBody);
            StringAssert.Contains("process.StandardError.ReadToEndAsync();", runBody);
            StringAssert.Contains("TryWaitForPythonProjectTool(process, label, out summary)", runBody);
            StringAssert.Contains("WaitForPythonProjectToolOutputDrain(outputTask, errorTask);", runBody);
            StringAssert.Contains("ReadProcessOutputTaskNoThrow(outputTask)", runBody);
            StringAssert.Contains("ReadProcessExitCodeNoThrow(process)", runBody);

            StringAssert.Contains("return Process.Start(startInfo);", startBody);
            StringAssert.Contains("catch (Exception)", startBody);
            StringAssert.Contains("process.WaitForExit(PythonToolTimeoutMilliseconds)", waitBody);
            StringAssert.Contains("KillPythonProjectToolNoThrow(process);", waitBody);
            Assert.AreEqual(2, CountToken(waitBody, "KillPythonProjectToolNoThrow(process);"));
            StringAssert.Contains("Task.WaitAll(new Task[] { outputTask, errorTask }, PythonToolOutputDrainMilliseconds);", drainBody);
            StringAssert.Contains("task.IsCompleted", readOutputBody);
            StringAssert.Contains("return task.Result ?? string.Empty;", readOutputBody);
            StringAssert.Contains("return process.ExitCode;", readExitBody);
            StringAssert.Contains("if (!process.HasExited)", killBody);
            StringAssert.Contains("process.Kill();", killBody);
        }

        [Test]
        public void CompilerOutputPromotionAvoidsDeleteMoveFallback()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Editor/DataMonolith/H8DataMonolithCompiler.cs");
            string promoteBody = ExtractMethodBody(source, "private static bool TryPromoteAfterReplaceFailure(");
            string copyBody = ExtractMethodBody(source, "private static bool TryPromoteWithValidatedCopy(");

            StringAssert.DoesNotContain("private static bool TryPromoteWithRecoverableMove", source);
            StringAssert.DoesNotContain("TryPromoteWithRecoverableMove(", source);
            StringAssert.DoesNotContain("File.Delete(outputPath);", source);
            StringAssert.Contains("TryPromoteWithValidatedCopy(outputPath, tempPath, backupPath, out string copyError)", promoteBody);
            StringAssert.Contains("File.Copy(tempPath, outputPath, true);", copyBody);
            StringAssert.Contains("TryValidateBlobFile(outputPath, out string validationError)", copyBody);
            StringAssert.Contains("TryRestoreBackup(outputPath, backupPath, out string restoreError)", copyBody);
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

        private static int CountToken(string source, string token)
        {
            int count = 0;
            int index = 0;
            while (index < source.Length)
            {
                int found = source.IndexOf(token, index, StringComparison.Ordinal);
                if (found < 0)
                    return count;

                count++;
                index = found + token.Length;
            }

            return count;
        }
    }
}
