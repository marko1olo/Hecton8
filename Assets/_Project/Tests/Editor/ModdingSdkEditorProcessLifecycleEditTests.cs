using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class ModdingSdkEditorProcessLifecycleEditTests
    {
        [Test]
        public void SdkHubValidatorProcess_UsesNoThrowCleanupLifecycle()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Editor/ModdingSDK/ModdingSdkHubWindow.cs");
            string disableBody = ExtractMethodBody(source, "private void OnDisable()");
            string launchBody = ExtractMethodBody(source, "private void RunStaticValidator()");
            string disposeBody = ExtractMethodBody(source, "private void DisposeRunningValidator()");
            string killBody = ExtractMethodBody(source, "private static void KillValidatorProcessNoThrow(DiagnosticsProcess process)");
            string disposeProcessBody = ExtractMethodBody(source, "private static void DisposeValidatorProcessNoThrow(DiagnosticsProcess process)");

            StringAssert.Contains("DiagnosticsProcess process = _runningValidatorProcess;", disableBody);
            StringAssert.Contains("KillValidatorProcessNoThrow(process);", disableBody);
            StringAssert.Contains("DisposeRunningValidator();", disableBody);
            StringAssert.DoesNotContain("_runningValidatorProcess.Kill();", disableBody);
            StringAssert.Contains("KillValidatorProcessNoThrow(_runningValidatorProcess);", launchBody);

            StringAssert.Contains("DiagnosticsProcess process = _runningValidatorProcess;", disposeBody);
            StringAssert.Contains("_runningValidatorProcess = null;", disposeBody);
            StringAssert.Contains("DisposeValidatorProcessNoThrow(process);", disposeBody);
            AssertOrder(disposeBody, "_runningValidatorProcess = null;", "DisposeValidatorProcessNoThrow(process);");
            StringAssert.DoesNotContain("_runningValidatorProcess.Dispose();", disposeBody);

            StringAssert.Contains("if (process == null)", killBody);
            StringAssert.Contains("if (!process.HasExited)", killBody);
            StringAssert.Contains("process.Kill();", killBody);
            StringAssert.Contains("catch (Exception exception)", killBody);
            StringAssert.Contains("process.Dispose();", disposeProcessBody);
            StringAssert.Contains("catch (Exception exception)", disposeProcessBody);
        }

        [Test]
        public void StarterWorkbenchToolProcess_UsesNoThrowCleanupLifecycle()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Editor/ModdingSDK/ExternalStarterKitWorkbenchWindow.cs");
            string disableBody = ExtractMethodBody(source, "private void OnDisable()");
            string launchBody = ExtractMethodBody(source, "private void RunStarterTool(string scriptRelativePath, string extraArguments, bool reloadAfterSuccess)");
            string disposeBody = ExtractMethodBody(source, "private void DisposeRunningTool()");
            string killBody = ExtractMethodBody(source, "private static void KillToolProcessNoThrow(DiagnosticsProcess process)");
            string disposeProcessBody = ExtractMethodBody(source, "private static void DisposeToolProcessNoThrow(DiagnosticsProcess process)");

            StringAssert.Contains("DiagnosticsProcess process = _runningToolProcess;", disableBody);
            StringAssert.Contains("KillToolProcessNoThrow(process);", disableBody);
            StringAssert.Contains("DisposeRunningTool();", disableBody);
            StringAssert.DoesNotContain("_runningToolProcess.Kill();", disableBody);
            StringAssert.Contains("KillToolProcessNoThrow(_runningToolProcess);", launchBody);

            StringAssert.Contains("DiagnosticsProcess process = _runningToolProcess;", disposeBody);
            StringAssert.Contains("_runningToolProcess = null;", disposeBody);
            StringAssert.Contains("DisposeToolProcessNoThrow(process);", disposeBody);
            AssertOrder(disposeBody, "_runningToolProcess = null;", "DisposeToolProcessNoThrow(process);");
            StringAssert.DoesNotContain("_runningToolProcess.Dispose();", disposeBody);

            StringAssert.Contains("if (process == null)", killBody);
            StringAssert.Contains("if (!process.HasExited)", killBody);
            StringAssert.Contains("process.Kill();", killBody);
            StringAssert.Contains("catch (Exception exception)", killBody);
            StringAssert.Contains("process.Dispose();", disposeProcessBody);
            StringAssert.Contains("catch (Exception exception)", disposeProcessBody);
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

        private static void AssertOrder(string source, string first, string second)
        {
            int firstIndex = source.IndexOf(first, StringComparison.Ordinal);
            int secondIndex = source.IndexOf(second, StringComparison.Ordinal);
            Assert.GreaterOrEqual(firstIndex, 0, "Missing first token: " + first);
            Assert.GreaterOrEqual(secondIndex, 0, "Missing second token: " + second);
            Assert.Less(firstIndex, secondIndex, first + " must appear before " + second);
        }
    }
}
