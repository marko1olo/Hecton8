using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class DigitalVoiceForgeProcessLifecycleEditTests
    {
        [Test]
        public void VoiceForgeBakeProcess_UsesFailClosedNoThrowLifecycle()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Audio/Synthesis/Editor/DigitalVoiceForgeWindow.cs");
            string disableBody = ExtractMethodBody(source, "private void OnDisable()");
            string startBody = ExtractMethodBody(source, "private void StartBake()");
            string tickBody = ExtractMethodBody(source, "private void Tick()");
            string tryStartBody = ExtractMethodBody(source, "private Process TryStartBakeProcess(ProcessStartInfo psi)");
            string disposeRunningBody = ExtractMethodBody(source, "private void DisposeRunningBakeProcessNoThrow(Process process)");
            string isRunningBody = ExtractMethodBody(source, "private static bool IsProcessRunning(Process process)");
            string exitCodeBody = ExtractMethodBody(source, "private static int ReadExitCodeNoThrow(Process process)");
            string killBody = ExtractMethodBody(source, "private static void KillBakeProcessNoThrow(Process process)");
            string disposeBody = ExtractMethodBody(source, "private static void DisposeProcessNoThrow(Process process)");

            StringAssert.DoesNotContain("_process.HasExited", source);
            StringAssert.DoesNotContain("_process.Kill();", source);
            StringAssert.DoesNotContain("_process.Dispose();", source);
            StringAssert.DoesNotContain("_process.ExitCode", source);

            StringAssert.Contains("Process process = _process;", disableBody);
            StringAssert.Contains("KillBakeProcessNoThrow(process);", disableBody);
            StringAssert.Contains("DisposeRunningBakeProcessNoThrow(process);", disableBody);

            StringAssert.Contains("if (_process != null)", startBody);
            StringAssert.Contains("if (IsProcessRunning(_process))", startBody);
            StringAssert.Contains("DisposeRunningBakeProcessNoThrow(_process);", startBody);
            StringAssert.Contains("Process process = TryStartBakeProcess(psi);", startBody);
            StringAssert.Contains("_status.text = \"voice_baker.py failed to start.\";", startBody);
            StringAssert.Contains("_process = process;", startBody);

            StringAssert.Contains("Process process = _process;", tickBody);
            StringAssert.Contains("if (!IsProcessRunning(process))", tickBody);
            StringAssert.Contains("int code = ReadExitCodeNoThrow(process);", tickBody);
            StringAssert.Contains("DisposeRunningBakeProcessNoThrow(process);", tickBody);

            StringAssert.Contains("process = Process.Start(psi);", tryStartBody);
            StringAssert.Contains("process.OutputDataReceived += OnProcessOutputData;", tryStartBody);
            StringAssert.Contains("process.BeginOutputReadLine();", tryStartBody);
            StringAssert.Contains("catch (Exception exception)", tryStartBody);
            StringAssert.Contains("KillBakeProcessNoThrow(process);", tryStartBody);
            StringAssert.Contains("DisposeProcessNoThrow(process);", tryStartBody);

            StringAssert.Contains("if (ReferenceEquals(_process, process))", disposeRunningBody);
            StringAssert.Contains("_process = null;", disposeRunningBody);
            StringAssert.Contains("process.OutputDataReceived -= OnProcessOutputData;", disposeRunningBody);
            StringAssert.Contains("DisposeProcessNoThrow(process);", disposeRunningBody);

            StringAssert.Contains("return !process.HasExited;", isRunningBody);
            StringAssert.Contains("catch (Exception)", isRunningBody);
            StringAssert.Contains("return process.ExitCode;", exitCodeBody);
            StringAssert.Contains("catch (Exception)", exitCodeBody);
            StringAssert.Contains("process.Kill();", killBody);
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
