using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class NativePointerlessSentinelLifecycleEditTests
    {
        private static readonly HelperSpec[] HelperSpecs =
        {
            new HelperSpec("private static void ReleaseNativeQueue<T>", "queue.Dispose();"),
            new HelperSpec("private static void DisposeNativeQueue<T>", "queue.Dispose();"),
            new HelperSpec("private static void DisposeQueue<T>", "queue.Dispose();"),
            new HelperSpec("private static void DisposeQueue<TPayload>", "queue.Dispose();"),
            new HelperSpec("private static void DisposeNativeArray<T>", "array.Dispose();"),
            new HelperSpec("private static void ReleaseNativeHashSet<T>", "hashSet.Dispose();"),
            new HelperSpec("private static void ReleaseNativeList<T>", "list.Dispose();"),
            new HelperSpec("private static void DisposeNativeList<T>", "list.Dispose();"),
            new HelperSpec("private static void DisposeTrackedTempJobList<T>", "list.Dispose();"),
            new HelperSpec("private static void DisposeTrackedTempJobArray<T>", "array.Dispose();"),
            new HelperSpec("private static void DisposeNativeParallelHashMap<TKey, TValue>", "map.Dispose();"),
            new HelperSpec("private static void DisposeNativeParallelMultiHashMap<TKey, TValue>", "map.Dispose();"),
            new HelperSpec("private static void DisposeHashMap<TValue>", "map.Dispose();"),
            new HelperSpec("private static void DisposeTrackedPersistentQueue<T>", "queue.Dispose();"),
            new HelperSpec("private static void DisposeServiceReboundQueue", "queue.Dispose();")
        };

        private static readonly string[] ForbiddenPrefixedFragments =
        {
            "queueException",
            "queueif",
            "queue{",
            "queue}",
            "queueVolatile",
            "hashSetException",
            "hashSetif",
            "hashSet{",
            "hashSet}",
            "arrayException",
            "arrayif",
            "array{",
            "array}",
            "listException",
            "listif",
            "list{",
            "list}"
        };

        private static readonly string[] ForbiddenOwnerLabelApis =
        {
            "NativeMemorySentinel.RegisterNativeList(",
            "NativeMemorySentinel.RegisterNativeQueue(",
            "NativeMemorySentinel.RegisterNativeHashMap(",
            "NativeMemorySentinel.RegisterNativeUnsafeHashMap(",
            "NativeMemorySentinel.RegisterNativeParallelHashMap(",
            "NativeMemorySentinel.RegisterNativeParallelHashSet(",
            "NativeMemorySentinel.RegisterNativeParallelMultiHashMap(",
            "NativeMemorySentinel.UnregisterNativeList(",
            "NativeMemorySentinel.UnregisterNativeQueue(",
            "NativeMemorySentinel.UnregisterNativeHashMap(",
            "NativeMemorySentinel.UnregisterNativeUnsafeHashMap(",
            "NativeMemorySentinel.UnregisterNativeParallelHashMap(",
            "NativeMemorySentinel.UnregisterNativeParallelHashSet(",
            "NativeMemorySentinel.UnregisterNativeParallelMultiHashMap(",
            "NativeMemorySentinel.RefreshNativeList(",
            "NativeMemorySentinel.RefreshNativeQueue(",
            "NativeMemorySentinel.RefreshNativeHashMap(",
            "NativeMemorySentinel.RefreshNativeUnsafeHashMap(",
            "NativeMemorySentinel.RefreshNativeParallelHashMap(",
            "NativeMemorySentinel.RefreshNativeParallelHashSet(",
            "NativeMemorySentinel.RefreshNativeParallelMultiHashMap("
        };

        [Test]
        public void RuntimeSource_HasNoMechanicalPointerlessSentinelPrefixJunk()
        {
            var failures = new List<string>();
            foreach (string path in EnumerateRuntimeSourceFiles())
            {
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string trimmed = lines[i].TrimStart();
                    foreach (string fragment in ForbiddenPrefixedFragments)
                    {
                        if (trimmed.StartsWith(fragment, StringComparison.Ordinal))
                            failures.Add(RelativePath(path) + ":" + (i + 1) + " starts with " + fragment);
                    }
                }
            }

            AssertNoFailures(failures);
        }

        [Test]
        public void RuntimeSource_UsesStoredIdsForPointerlessNativeSentinelApis()
        {
            var failures = new List<string>();
            foreach (string path in EnumerateRuntimeSourceFiles())
            {
                string source = File.ReadAllText(path);
                foreach (string api in ForbiddenOwnerLabelApis)
                {
                    if (source.IndexOf(api, StringComparison.Ordinal) >= 0)
                        failures.Add(RelativePath(path) + " still calls " + api);
                }
            }

            AssertNoFailures(failures);
        }

        [Test]
        public void RuntimeSource_PointerlessStoredIdHelpersDisposeBeforeUnregister()
        {
            var failures = new List<string>();
            foreach (string path in EnumerateRuntimeSourceFiles())
            {
                string source = File.ReadAllText(path);
                foreach (HelperSpec spec in HelperSpecs)
                {
                    int start = 0;
                    while ((start = source.IndexOf(spec.Signature, start, StringComparison.Ordinal)) >= 0)
                    {
                        string declaration = ExtractDeclarationAt(source, start);
                        string body = ExtractMethodAt(source, start);
                        string label = RelativePath(path) + "::" + spec.Signature;
                        if (declaration.IndexOf("sentinelId", StringComparison.Ordinal) >= 0)
                        {
                            AssertHelperBody(label, body, spec.DisposeToken, failures);
                        }
                        else if (body.IndexOf("sentinelId", StringComparison.Ordinal) >= 0)
                        {
                            failures.Add(label + " references sentinelId without storing the registration id");
                        }

                        start += spec.Signature.Length;
                    }
                }
            }

            AssertNoFailures(failures);
        }

        [Test]
        public void ThreadSafeCommandQueue_CreationRollbackUsesTrackedDisposePath()
        {
            string path = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Core/ThreadSafeCommandQueue.cs");
            string source = File.ReadAllText(path);
            int methodStart = source.IndexOf("private static NativeQueue<T> CreateTrackedPersistentQueue<T>", StringComparison.Ordinal);
            Assert.GreaterOrEqual(methodStart, 0, "Missing ThreadSafeCommandQueue.CreateTrackedPersistentQueue<T>.");

            string body = ExtractMethodAt(source, methodStart);
            int catchIndex = body.IndexOf("catch (Exception exception)", StringComparison.Ordinal);
            int releaseCallIndex = body.IndexOf("DisposeTrackedPersistentQueue(ref queue, ref sentinelId, ref cleanupReadyFlag);", StringComparison.Ordinal);
            Assert.GreaterOrEqual(catchIndex, 0, "CreateTrackedPersistentQueue<T> must have a rollback catch path.");
            Assert.GreaterOrEqual(releaseCallIndex, 0, "CreateTrackedPersistentQueue<T> rollback must use DisposeTrackedPersistentQueue.");
            Assert.Less(catchIndex, releaseCallIndex, "CreateTrackedPersistentQueue<T> rollback must release after entering the catch path.");

            string rollbackBody = body.Substring(catchIndex);
            Assert.AreEqual(
                -1,
                rollbackBody.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                "CreateTrackedPersistentQueue<T> rollback must not manually unregister before native dispose.");
            Assert.AreEqual(
                -1,
                rollbackBody.IndexOf("queue.Dispose();", StringComparison.Ordinal),
                "CreateTrackedPersistentQueue<T> rollback must not bypass the tracked dispose helper.");
        }

        private static string ExtractDeclarationAt(string source, int methodStart)
        {
            int open = source.IndexOf('{', methodStart);
            Assert.GreaterOrEqual(open, 0, "Missing method open brace near " + methodStart);
            return source.Substring(methodStart, open - methodStart);
        }

        private static void AssertHelperBody(string label, string body, string disposeToken, List<string> failures)
        {
            int disposeIndex = body.IndexOf(disposeToken, StringComparison.Ordinal);
            int unregisterIndex = body.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal);
            int resetIndex = body.IndexOf("sentinelId = 0;", StringComparison.Ordinal);

            if (disposeIndex < 0)
                failures.Add(label + " is missing " + disposeToken);
            if (unregisterIndex < 0)
                failures.Add(label + " is missing NativeMemorySentinel.Unregister(sentinelId);");
            if (resetIndex < 0)
                failures.Add(label + " is missing sentinelId reset");
            if (disposeIndex >= 0 && unregisterIndex >= 0 && disposeIndex < unregisterIndex)
                failures.Add(label + " disposes native container before sentinel unregister");
            if (unregisterIndex >= 0 && resetIndex >= 0 && resetIndex < unregisterIndex)
                failures.Add(label + " clears sentinelId before unregister succeeds");
            if (body.IndexOf("bool disposed = !", StringComparison.Ordinal) >= 0)
                failures.Add(label + " preserves stale disposed gate");
            if (body.IndexOf("if (" + "disposed &&", StringComparison.Ordinal) >= 0)
                failures.Add(label + " gates sentinel unregister on native dispose");
            if (body.IndexOf("if (sentinelId > 0)", StringComparison.Ordinal) < 0)
                failures.Add(label + " does not guard stored sentinel id");
            if (body.IndexOf("finally", StringComparison.Ordinal) < 0)
                failures.Add(label + " does not reset native owner state in finally");
        }

        private static string ExtractMethodAt(string source, int methodStart)
        {
            int open = source.IndexOf('{', methodStart);
            Assert.GreaterOrEqual(open, 0, "Missing method open brace near " + methodStart);

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

            Assert.Fail("Missing method close brace near " + methodStart);
            return string.Empty;
        }

        private static IEnumerable<string> EnumerateRuntimeSourceFiles()
        {
            string scriptsRoot = Path.Combine(Directory.GetCurrentDirectory(), "Assets/_Project/Scripts");
            foreach (string path in Directory.EnumerateFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories))
            {
                string normalized = path.Replace('\\', '/');
                if (normalized.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                if (normalized.EndsWith("/Core/NativeMemorySentinel.cs", StringComparison.OrdinalIgnoreCase))
                    continue;

                yield return path;
            }
        }

        private static string RelativePath(string path)
        {
            string root = Directory.GetCurrentDirectory();
            if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return path.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return path;
        }

        private static void AssertNoFailures(List<string> failures)
        {
            if (failures.Count == 0)
                return;

            Assert.Fail(string.Join(System.Environment.NewLine, failures));
        }

        private readonly struct HelperSpec
        {
            public HelperSpec(string signature, string disposeToken)
            {
                Signature = signature;
                DisposeToken = disposeToken;
            }

            public string Signature { get; }

            public string DisposeToken { get; }
        }
    }
}
