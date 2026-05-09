#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Editor-only dashboard that reports public first-party interfaces with no concrete implementors.
    /// </summary>
    public sealed class InterfaceComplianceDashboard : EditorWindow
    {
        private const string WindowTitle = "Interface Compliance";
        private const string MenuPath = "Hecton8/Diagnostics/Interface Compliance Dashboard";

        private readonly List<InterfaceRow> _rows = new List<InterfaceRow>(128);
        private readonly List<Type> _typeScratch = new List<Type>(1024);
        private Vector2 _scroll;
        private int _ghostCount;

        [MenuItem(MenuPath)]
        private static void Open()
        {
            InterfaceComplianceDashboard window = GetWindow<InterfaceComplianceDashboard>(WindowTitle);
            window.Rebuild();
            window.Show();
        }

        private void OnEnable()
        {
            Rebuild();
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(72f)))
                    Rebuild();

                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("Interfaces: " + _rows.Count, GUILayout.Width(110f));
                EditorGUILayout.LabelField("Ghosts: " + _ghostCount, GUILayout.Width(90f));
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < _rows.Count; i++)
            {
                InterfaceRow row = _rows[i];
                GUIStyle style = row.ImplementorCount == 0 ? EditorStyles.helpBox : EditorStyles.label;
                Rect rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight + 4f);
                if (row.ImplementorCount == 0)
                    EditorGUI.DrawRect(rect, new Color(0.35f, 0.05f, 0.05f, 0.35f));

                rect.x += 4f;
                rect.width -= 8f;
                EditorGUI.LabelField(rect, row.DisplayName, style);
            }

            EditorGUILayout.EndScrollView();
        }

        private void Rebuild()
        {
            _rows.Clear();
            _typeScratch.Clear();
            _ghostCount = 0;
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Assembly assembly = assemblies[i];
                if (!IsFirstPartyAssembly(assembly))
                    continue;

                Type[] assemblyTypes;
                try
                {
                    assemblyTypes = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    assemblyTypes = exception.Types;
                }

                if (assemblyTypes == null)
                    continue;

                for (int typeIndex = 0; typeIndex < assemblyTypes.Length; typeIndex++)
                {
                    Type type = assemblyTypes[typeIndex];
                    if (type != null)
                        _typeScratch.Add(type);
                }
            }

            for (int typeIndex = 0; typeIndex < _typeScratch.Count; typeIndex++)
            {
                Type interfaceType = _typeScratch[typeIndex];
                if (!IsPublicFirstPartyInterface(interfaceType))
                    continue;

                int implementorCount = CountConcreteImplementors(interfaceType, _typeScratch);
                if (implementorCount == 0)
                    _ghostCount++;
                _rows.Add(new InterfaceRow(interfaceType.FullName, implementorCount));
            }

            _rows.Sort(InterfaceRow.Compare);
            Repaint();
        }

        private static bool IsFirstPartyAssembly(Assembly assembly)
        {
            string assemblyName = assembly.GetName().Name;
            return assemblyName == "Assembly-CSharp" ||
                   assemblyName == "Assembly-CSharp-Editor" ||
                   assemblyName.StartsWith("Hecton8", StringComparison.Ordinal);
        }

        private static bool IsPublicFirstPartyInterface(Type type)
        {
            if (type == null || !type.IsInterface || !type.IsPublic)
                return false;

            string namespaceName = type.Namespace;
            return namespaceName != null &&
                   (namespaceName.StartsWith("Hecton8", StringComparison.Ordinal) ||
                    namespaceName.StartsWith("Hecton", StringComparison.Ordinal) ||
                    namespaceName.StartsWith("NASAPunk", StringComparison.Ordinal));
        }

        private static int CountConcreteImplementors(Type interfaceType, List<Type> allTypes)
        {
            int count = 0;
            for (int i = 0; i < allTypes.Count; i++)
            {
                Type candidate = allTypes[i];
                if (candidate == null ||
                    candidate.IsInterface ||
                    candidate.IsAbstract ||
                    candidate.ContainsGenericParameters)
                {
                    continue;
                }

                if (interfaceType.IsAssignableFrom(candidate))
                    count++;
            }

            return count;
        }

        private readonly struct InterfaceRow
        {
            public readonly string InterfaceName;
            public readonly int ImplementorCount;

            public InterfaceRow(string interfaceName, int implementorCount)
            {
                InterfaceName = interfaceName;
                ImplementorCount = implementorCount;
            }

            public string DisplayName => ImplementorCount == 0
                ? InterfaceName + "    GHOST"
                : InterfaceName + "    " + ImplementorCount;

            public static int Compare(InterfaceRow left, InterfaceRow right)
            {
                int leftGhost = left.ImplementorCount == 0 ? 0 : 1;
                int rightGhost = right.ImplementorCount == 0 ? 0 : 1;
                int ghostCompare = leftGhost.CompareTo(rightGhost);
                return ghostCompare != 0
                    ? ghostCompare
                    : string.CompareOrdinal(left.InterfaceName, right.InterfaceName);
            }
        }
    }
}
#endif
