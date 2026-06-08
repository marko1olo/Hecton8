using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class EditorFileWatcherLifecycleEditTests
    {
        [Test]
        public void StaticDataHotReloadWatcher_UsesFailClosedNoThrowLifecycle()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Core/Data/H8DataBaker.cs");
            string bootstrapBody = ExtractMethodBody(source, "static H8StaticDataEditorHotReloadBootstrap()");
            string disposeBootstrapBody = ExtractMethodBody(source, "private static void DisposeWatcher()");
            string constructorBody = ExtractMethodBody(source, "public H8StaticDataHotReloadWatcher()");
            string createBody = ExtractMethodBody(source, "private FileSystemWatcher TryCreateHotReloadWatcher(string root)");
            string disposeBody = ExtractMethodBody(source, "public void Dispose()");
            string stopBody = ExtractMethodBody(source, "private void StopWatcherNoThrow(FileSystemWatcher watcher)");

            StringAssert.Contains("AssemblyReloadEvents.beforeAssemblyReload += DisposeWatcher;", bootstrapBody);
            StringAssert.Contains("EditorApplication.quitting += DisposeWatcher;", bootstrapBody);
            StringAssert.Contains("EditorApplication.update -= Tick;", disposeBootstrapBody);
            StringAssert.Contains("H8StaticDataHotReloadWatcher watcher = _watcher;", disposeBootstrapBody);
            StringAssert.Contains("_watcher = null;", disposeBootstrapBody);
            StringAssert.Contains("watcher.Dispose();", disposeBootstrapBody);

            StringAssert.DoesNotContain("_watcher = new FileSystemWatcher", source);
            StringAssert.Contains("_watcher = TryCreateHotReloadWatcher(root);", constructorBody);
            StringAssert.Contains("new FileSystemWatcher(root, \"*.csv\")", createBody);
            StringAssert.Contains("watcher.EnableRaisingEvents = true;", createBody);
            StringAssert.Contains("return watcher;", createBody);
            StringAssert.Contains("catch (IOException)", createBody);
            StringAssert.Contains("catch (UnauthorizedAccessException)", createBody);
            StringAssert.Contains("catch (ArgumentException)", createBody);
            StringAssert.Contains("catch (NotSupportedException)", createBody);
            StringAssert.Contains("catch (System.Security.SecurityException)", createBody);
            StringAssert.Contains("return null;", createBody);

            StringAssert.Contains("FileSystemWatcher watcher = _watcher;", disposeBody);
            StringAssert.Contains("_watcher = null;", disposeBody);
            StringAssert.Contains("StopWatcherNoThrow(watcher);", disposeBody);
            StringAssert.Contains("watcher.EnableRaisingEvents = false;", stopBody);
            StringAssert.Contains("watcher.Changed -= OnChanged;", stopBody);
            StringAssert.Contains("watcher.Dispose();", stopBody);
            StringAssert.Contains("catch (ObjectDisposedException)", stopBody);
            StringAssert.Contains("catch (InvalidOperationException)", stopBody);
            StringAssert.Contains("catch (IOException)", stopBody);
        }

        [Test]
        public void StaticDataBakerAtomicWrite_UsesCriticalFlushAndNoDeleteGap()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Core/Data/H8DataBaker.cs");
            string atomicWriteBody = ExtractMethodBody(source, "private static void AtomicWrite(string path, byte[] bytes)");
            string cleanupBody = ExtractMethodBody(source, "private static void TryDeleteAtomicWriteFile(string path)");

            StringAssert.Contains("using Hecton8.SaveSystem;", source);
            StringAssert.Contains("throw new ArgumentNullException(nameof(bytes));", atomicWriteBody);
            StringAssert.Contains("new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough)", atomicWriteBody);
            StringAssert.Contains("stream.Flush(true);", atomicWriteBody);
            StringAssert.Contains("AsyncWriteManager.TryGetFileLength(tempPath, out long tempBytes, out string lengthError)", atomicWriteBody);
            StringAssert.Contains("tempBytes != bytes.LongLength", atomicWriteBody);
            StringAssert.Contains("AsyncWriteManager.FlushCriticalSavePath(tempPath, tempBytes, out string flushError)", atomicWriteBody);
            StringAssert.Contains("AsyncWriteManager.InvalidateCachedReadWindows(tempPath);", atomicWriteBody);
            StringAssert.Contains("AsyncWriteManager.InvalidateCachedReadWindows(path);", atomicWriteBody);
            StringAssert.Contains("TryDeleteAtomicWriteFile(backupPath);", atomicWriteBody);
            StringAssert.Contains("AsyncWriteManager.InvalidateCachedReadWindows(backupPath);", atomicWriteBody);
            StringAssert.Contains("File.Replace(tempPath, path, backupPath, true);", atomicWriteBody);
            StringAssert.Contains("File.Move(tempPath, path);", atomicWriteBody);
            StringAssert.Contains("AsyncWriteManager.TryGetFileLength(path, out long promotedBytes, out lengthError)", atomicWriteBody);
            StringAssert.Contains("promotedBytes != bytes.LongLength", atomicWriteBody);
            StringAssert.Contains("AsyncWriteManager.FlushCriticalSavePath(path, promotedBytes, out flushError)", atomicWriteBody);
            StringAssert.Contains("TryDeleteAtomicWriteFile(tempPath);", atomicWriteBody);
            StringAssert.DoesNotContain("File.Delete(path)", atomicWriteBody);
            Assert.IsTrue(ContainsTokensInOrder(
                atomicWriteBody,
                "AsyncWriteManager.InvalidateCachedReadWindows(tempPath);",
                "FileOptions.WriteThrough",
                "stream.Flush(true);",
                "AsyncWriteManager.InvalidateCachedReadWindows(tempPath);",
                "AsyncWriteManager.TryGetFileLength(tempPath, out long tempBytes, out string lengthError)",
                "AsyncWriteManager.FlushCriticalSavePath(tempPath, tempBytes, out string flushError)",
                "AsyncWriteManager.InvalidateCachedReadWindows(tempPath);",
                "AsyncWriteManager.InvalidateCachedReadWindows(path);",
                "TryDeleteAtomicWriteFile(backupPath);",
                "File.Replace(tempPath, path, backupPath, true);",
                "AsyncWriteManager.InvalidateCachedReadWindows(backupPath);",
                "File.Move(tempPath, path);",
                "AsyncWriteManager.InvalidateCachedReadWindows(tempPath);",
                "AsyncWriteManager.InvalidateCachedReadWindows(path);",
                "AsyncWriteManager.TryGetFileLength(path, out long promotedBytes, out lengthError)",
                "AsyncWriteManager.FlushCriticalSavePath(path, promotedBytes, out flushError)",
                "TryDeleteAtomicWriteFile(tempPath);"));

            StringAssert.Contains("AsyncWriteManager.InvalidateCachedReadWindows(path);", cleanupBody);
            StringAssert.Contains("File.Delete(path);", cleanupBody);
            StringAssert.Contains("catch (IOException)", cleanupBody);
            StringAssert.Contains("catch (UnauthorizedAccessException)", cleanupBody);
            StringAssert.Contains("catch (System.Security.SecurityException)", cleanupBody);
            Assert.IsTrue(ContainsTokensInOrder(
                cleanupBody,
                "AsyncWriteManager.InvalidateCachedReadWindows(path);",
                "File.Delete(path);",
                "finally",
                "AsyncWriteManager.InvalidateCachedReadWindows(path);"));
        }

        [Test]
        public void AiTextureInboxWatcher_UsesFailClosedNoThrowLifecycle()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Editor/AITextureControlMapBaker/Shinobu269/AITextureIngestionWatcher.cs");
            string startBody = Normalize(ExtractMethodBody(source, "internal static void StartWatcher()"));
            string stopBody = ExtractMethodBody(source, "internal static void StopWatcher()");
            string ensureBody = ExtractMethodBody(source, "private static bool TryEnsureInboxDirectoryNoThrow(string absoluteInbox)");
            string startHelperBody = ExtractMethodBody(source, "private static FileSystemWatcher TryStartInboxWatcherNoThrow(string absoluteInbox)");
            string stopHelperBody = ExtractMethodBody(source, "private static void StopInboxWatcherNoThrow(FileSystemWatcher watcher)");

            StringAssert.DoesNotContain("_watcher = new FileSystemWatcher", source);
            StringAssert.Contains("if (!TryEnsureInboxDirectoryNoThrow(absoluteInbox))", startBody);
            StringAssert.Contains("FileSystemWatcher watcher = TryStartInboxWatcherNoThrow(absoluteInbox);", startBody);
            StringAssert.Contains("if (watcher == null)\n                return;", startBody);
            StringAssert.Contains("_watcher = watcher;", startBody);
            StringAssert.Contains("EnsureDrainRegistered();", startBody);

            StringAssert.Contains("FileSystemWatcher watcher = _watcher;", stopBody);
            StringAssert.Contains("_watcher = null;", stopBody);
            StringAssert.Contains("StopInboxWatcherNoThrow(watcher);", stopBody);

            StringAssert.Contains("Directory.CreateDirectory(absoluteInbox);", ensureBody);
            StringAssert.Contains("catch (IOException)", ensureBody);
            StringAssert.Contains("catch (UnauthorizedAccessException)", ensureBody);
            StringAssert.Contains("catch (ArgumentException)", ensureBody);
            StringAssert.Contains("catch (NotSupportedException)", ensureBody);
            StringAssert.Contains("catch (System.Security.SecurityException)", ensureBody);

            StringAssert.Contains("new FileSystemWatcher(absoluteInbox, \"*.png\")", startHelperBody);
            StringAssert.Contains("watcher.EnableRaisingEvents = true;", startHelperBody);
            StringAssert.Contains("return watcher;", startHelperBody);
            StringAssert.Contains("return null;", startHelperBody);

            StringAssert.Contains("watcher.EnableRaisingEvents = false;", stopHelperBody);
            StringAssert.Contains("watcher.Created -= OnFileChanged;", stopHelperBody);
            StringAssert.Contains("watcher.Dispose();", stopHelperBody);
            StringAssert.Contains("catch (ObjectDisposedException)", stopHelperBody);
            StringAssert.Contains("catch (InvalidOperationException)", stopHelperBody);
            StringAssert.Contains("catch (IOException)", stopHelperBody);
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

        private static string Normalize(string source)
        {
            return source.Replace("\r\n", "\n");
        }

        private static bool ContainsTokensInOrder(string text, params string[] tokens)
        {
            int index = 0;
            for (int i = 0; i < tokens.Length; i++)
            {
                int found = text.IndexOf(tokens[i], index, StringComparison.Ordinal);
                if (found < 0)
                    return false;

                index = found + tokens[i].Length;
            }

            return true;
        }
    }
}
