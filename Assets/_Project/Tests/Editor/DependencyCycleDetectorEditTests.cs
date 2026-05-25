using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Text;
using Hecton8.Core;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class DependencyCycleDetectorEditTests
    {
        [Test]
        public void GlobalRegistryDependencyAttributes_DoNotContainCycles()
        {
            Dictionary<string, List<string>> graph = new Dictionary<string, List<string>>(256, StringComparer.Ordinal);
            List<string> nodes = new List<string>(256);
            Assembly coreAssembly = typeof(GlobalRegistry).Assembly;
            Type[] types = GetLoadableTypes(coreAssembly);

            for (int i = 0; i < types.Length; i++)
            {
                Type type = types[i];
                if (type == null)
                    continue;

                ScanOwner(type.FullName, CustomAttributeData.GetCustomAttributes(type), graph, nodes);

                BindingFlags flags = BindingFlags.Instance |
                                     BindingFlags.Static |
                                     BindingFlags.Public |
                                     BindingFlags.NonPublic |
                                     BindingFlags.DeclaredOnly;
                MemberInfo[] members = type.GetMembers(flags);
                for (int memberIndex = 0; memberIndex < members.Length; memberIndex++)
                {
                    MemberInfo member = members[memberIndex];
                    string owner = type.FullName + "." + member.Name;
                    ScanOwner(owner, CustomAttributeData.GetCustomAttributes(member), graph, nodes);
                }
            }

            Dictionary<string, byte> state = new Dictionary<string, byte>(nodes.Count, StringComparer.Ordinal);
            List<string> stack = new List<string>(64);
            for (int i = 0; i < nodes.Count; i++)
            {
                string node = nodes[i];
                if (state.TryGetValue(node, out byte value) && value != 0)
                    continue;

                if (TryFindCycle(node, graph, state, stack, out string cycle))
                    Assert.Fail("Dependency cycle detected: " + cycle);
            }
        }

        private static void ScanOwner(
            string owner,
            IList<CustomAttributeData> attributes,
            Dictionary<string, List<string>> graph,
            List<string> nodes)
        {
            for (int i = 0; i < attributes.Count; i++)
            {
                CustomAttributeData attribute = attributes[i];
                if (attribute == null || attribute.AttributeType == null)
                    continue;

                if (attribute.AttributeType.Name.IndexOf("Dependency", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                List<string> targets = new List<string>(4);
                for (int argIndex = 0; argIndex < attribute.ConstructorArguments.Count; argIndex++)
                    ExtractTargets(attribute.ConstructorArguments[argIndex], targets);

                for (int namedIndex = 0; namedIndex < attribute.NamedArguments.Count; namedIndex++)
                    ExtractTargets(attribute.NamedArguments[namedIndex].TypedValue, targets);

                for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
                    AddEdge(owner, targets[targetIndex], graph, nodes);
            }
        }

        private static void ExtractTargets(CustomAttributeTypedArgument argument, List<string> targets)
        {
            object value = argument.Value;
            if (value == null)
                return;

            if (value is Type typeValue)
            {
                targets.Add(typeValue.FullName);
                return;
            }

            if (value is string stringValue)
            {
                if (!string.IsNullOrEmpty(stringValue))
                    targets.Add(stringValue);
                return;
            }

            ReadOnlyCollection<CustomAttributeTypedArgument> arrayValue =
                value as ReadOnlyCollection<CustomAttributeTypedArgument>;
            if (arrayValue == null)
                return;

            for (int i = 0; i < arrayValue.Count; i++)
                ExtractTargets(arrayValue[i], targets);
        }

        private static void AddEdge(
            string source,
            string target,
            Dictionary<string, List<string>> graph,
            List<string> nodes)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
                return;

            if (!graph.TryGetValue(source, out List<string> edges))
            {
                edges = new List<string>(4);
                graph.Add(source, edges);
                AddNode(source, nodes);
            }

            edges.Add(target);
            AddNode(target, nodes);
        }

        private static void AddNode(string node, List<string> nodes)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                if (string.Equals(nodes[i], node, StringComparison.Ordinal))
                    return;
            }

            nodes.Add(node);
        }

        private static bool TryFindCycle(
            string node,
            Dictionary<string, List<string>> graph,
            Dictionary<string, byte> state,
            List<string> stack,
            out string cycle)
        {
            state[node] = 1;
            stack.Add(node);

            if (graph.TryGetValue(node, out List<string> edges))
            {
                for (int i = 0; i < edges.Count; i++)
                {
                    string target = edges[i];
                    if (!state.TryGetValue(target, out byte targetState))
                        targetState = 0;

                    if (targetState == 1)
                    {
                        cycle = BuildCycle(stack, target);
                        return true;
                    }

                    if (targetState == 0 && TryFindCycle(target, graph, state, stack, out cycle))
                        return true;
                }
            }

            stack.RemoveAt(stack.Count - 1);
            state[node] = 2;
            cycle = string.Empty;
            return false;
        }

        private static string BuildCycle(List<string> stack, string repeatedNode)
        {
            int start = 0;
            for (int i = 0; i < stack.Count; i++)
            {
                if (stack[i] == repeatedNode)
                {
                    start = i;
                    break;
                }
            }

            StringBuilder builder = new StringBuilder(256);
            for (int i = start; i < stack.Count; i++)
            {
                if (builder.Length > 0)
                    builder.Append(" -> ");
                builder.Append(stack[i]);
            }

            builder.Append(" -> ");
            builder.Append(repeatedNode);
            return builder.ToString();
        }

        private static Type[] GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types;
            }
        }
    }
}
