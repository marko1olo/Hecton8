// H8BlackboxReflection.cs — Safe reflection helpers for probing project-specific types
// Uses reflection exclusively to avoid asmdef dependency issues.
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Hecton8.BlackboxDiagnostics
{
    public static class H8Reflect
    {
        private static readonly Dictionary<string, Type> _typeCache = new Dictionary<string, Type>();

        /// <summary>
        /// Find a type by full or short name across all loaded assemblies.
        /// Caches results. Returns null if not found.
        /// </summary>
        public static Type FindType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return null;
            if (_typeCache.TryGetValue(typeName, out var cached)) return cached;

            // Try direct Type.GetType first
            Type t = Type.GetType(typeName);
            if (t != null) { _typeCache[typeName] = t; return t; }

            // Search all loaded assemblies
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    t = asm.GetType(typeName);
                    if (t != null) { _typeCache[typeName] = t; return t; }

                    // Try short name match (class name only)
                    foreach (var at in asm.GetTypes())
                    {
                        if (at.Name == typeName || at.FullName == typeName)
                        {
                            _typeCache[typeName] = at;
                            return at;
                        }
                    }
                }
                catch { /* Some assemblies may throw on GetTypes() */ }
            }

            _typeCache[typeName] = null;
            return null;
        }

        /// <summary>
        /// Get a static field or property value by name from a type.
        /// Returns null on failure.
        /// </summary>
        public static object GetStatic(Type type, string name)
        {
            if (type == null || string.IsNullOrEmpty(name)) return null;
            try
            {
                const BindingFlags bf = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                var prop = type.GetProperty(name, bf);
                if (prop != null && prop.CanRead) return prop.GetValue(null);
                var field = type.GetField(name, bf);
                if (field != null) return field.GetValue(null);
            }
            catch (Exception e) { Debug.LogWarning($"[H8Blackbox] GetStatic({type.Name}.{name}) failed: {e.Message}"); }
            return null;
        }

        /// <summary>
        /// Get an instance field or property value by name.
        /// Returns null on failure.
        /// </summary>
        public static object GetField(object instance, string name)
        {
            if (instance == null || string.IsNullOrEmpty(name)) return null;
            try
            {
                var type = instance.GetType();
                const BindingFlags bf = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var prop = type.GetProperty(name, bf);
                if (prop != null && prop.CanRead) return prop.GetValue(instance);
                var field = type.GetField(name, bf);
                if (field != null) return field.GetValue(instance);
            }
            catch (Exception e) { Debug.LogWarning($"[H8Blackbox] GetField({name}) failed: {e.Message}"); }
            return null;
        }

        public static object GetFieldFallback(object instance, string[] names, out string foundName)
        {
            foundName = null;
            if (instance == null || names == null) return null;
            var type = instance.GetType();
            const BindingFlags bf = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var name in names)
            {
                var prop = type.GetProperty(name, bf);
                if (prop != null && prop.CanRead) { foundName = name; return prop.GetValue(instance); }
                var field = type.GetField(name, bf);
                if (field != null) { foundName = name; return field.GetValue(instance); }
            }
            return null;
        }

        public static bool HasMember(Type type, string name, out string kind)
        {
            kind = "";
            if (type == null || string.IsNullOrEmpty(name)) return false;
            const BindingFlags bf = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var prop = type.GetProperty(name, bf);
            if (prop != null) { kind = "Property"; return true; }
            var field = type.GetField(name, bf);
            if (field != null) { kind = "Field"; return true; }
            return false;
        }
        /// Dump all static fields and properties of a type that have simple/readable values.
        /// Includes both public and non-public members.
        /// </summary>
        public static List<H8KV> DumpStaticMembers(Type type, int maxValues = 100)
        {
            var result = new List<H8KV>();
            if (type == null) return result;
            const BindingFlags bf = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            int count = 0;

            try
            {
                foreach (var fi in type.GetFields(bf))
                {
                    if (count >= maxValues) break;
                    try
                    {
                        var val = fi.GetValue(null);
                        result.Add(new H8KV(fi.Name, SafeStr(val)));
                        count++;
                    }
                    catch { result.Add(new H8KV(fi.Name, "<read_error>")); count++; }
                }

                foreach (var pi in type.GetProperties(bf))
                {
                    if (count >= maxValues) break;
                    if (!pi.CanRead || pi.GetIndexParameters().Length > 0) continue;
                    try
                    {
                        var val = pi.GetValue(null);
                        result.Add(new H8KV(pi.Name, SafeStr(val)));
                        count++;
                    }
                    catch { result.Add(new H8KV(pi.Name, "<read_error>")); count++; }
                }
            }
            catch (Exception e) { result.Add(new H8KV("_dump_error", e.Message)); }

            return result;
        }

        /// <summary>
        /// Dump instance fields and properties with simple/readable values.
        /// </summary>
        public static List<H8KV> DumpInstanceMembers(object instance, int maxValues = 60)
        {
            var result = new List<H8KV>();
            if (instance == null) return result;
            var type = instance.GetType();
            const BindingFlags bf = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            int count = 0;

            try
            {
                foreach (var fi in type.GetFields(bf))
                {
                    if (count >= maxValues) break;
                    try
                    {
                        var val = fi.GetValue(instance);
                        result.Add(new H8KV(fi.Name, SafeStr(val)));
                        count++;
                    }
                    catch { result.Add(new H8KV(fi.Name, "<read_error>")); count++; }
                }

                foreach (var pi in type.GetProperties(bf))
                {
                    if (count >= maxValues) break;
                    if (!pi.CanRead || pi.GetIndexParameters().Length > 0) continue;
                    try
                    {
                        var val = pi.GetValue(instance);
                        result.Add(new H8KV(pi.Name, SafeStr(val)));
                        count++;
                    }
                    catch { result.Add(new H8KV(pi.Name, "<read_error>")); count++; }
                }
            }
            catch (Exception e) { result.Add(new H8KV("_dump_error", e.Message)); }

            return result;
        }

        /// <summary>
        /// Null-safe string representation. Handles Unity null, UnityEngine.Object naming, etc.
        /// </summary>
        public static string SafeStr(object value)
        {
            if (value == null) return "null";
            if (value is UnityEngine.Object uobj)
            {
                if (uobj == null) return "null (destroyed)";
                string name = uobj.name;
                if (uobj is GameObject go)
                    return $"GameObject(\"{name}\", active={go.activeSelf}, activeInHierarchy={go.activeInHierarchy})";
                if (uobj is Component comp)
                {
                    bool enabled = true;
                    if (comp is Behaviour beh) enabled = beh.enabled;
                    return $"{comp.GetType().Name}(\"{name}\", enabled={enabled})";
                }
                return $"{uobj.GetType().Name}(\"{name}\")";
            }
            if (value is bool b) return b ? "true" : "false";
            if (value is string s) return s.Length > 500 ? s.Substring(0, 500) + "..." : s;
            if (value is Enum e) return e.ToString();

            var vt = value.GetType();
            if (vt.IsPrimitive || vt == typeof(decimal)) return value.ToString();
            if (vt == typeof(Vector3) || vt == typeof(Vector2) || vt == typeof(Vector4) ||
                vt == typeof(Quaternion) || vt == typeof(Color) || vt == typeof(Rect))
                return value.ToString();

            // For complex types, just show type name and hash
            return $"<{vt.Name}>";
        }

        /// <summary>
        /// Check if an object is Unity-null (destroyed or actually null).
        /// </summary>
        public static bool IsUnityNull(object obj)
        {
            if (obj == null) return true;
            if (obj is UnityEngine.Object uobj) return uobj == null;
            return false;
        }

        /// <summary>
        /// Get a concise description of an object for slot reports.
        /// </summary>
        public static string GetObjectInfo(object obj)
        {
            if (obj == null) return "null";
            if (obj is UnityEngine.Object uobj)
            {
                if (uobj == null) return "null (destroyed)";
                return $"{uobj.GetType().Name}(\"{uobj.name}\")";
            }
            return $"<{obj.GetType().Name}>";
        }

        /// <summary>
        /// Find all components in loaded scenes by type name (including inactive).
        /// Works via reflection for project-specific types.
        /// </summary>
        public static List<Component> FindComponentsByTypeName(string typeName)
        {
            var result = new List<Component>();
            var type = FindType(typeName);
            if (type == null || !typeof(Component).IsAssignableFrom(type)) return result;

            try
            {
                var found = UnityEngine.Object.FindObjectsByType(type,
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var obj in found)
                {
                    if (obj is Component c) result.Add(c);
                }
            }
            catch
            {
                // Fallback: iterate scenes
                for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                {
                    var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                    if (!scene.isLoaded) continue;
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        foreach (var comp in root.GetComponentsInChildren(type, true))
                        {
                            result.Add(comp);
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Read a specific static field on GlobalRegistry to probe a service slot.
        /// Returns H8RegistrySlotInfo with measured null/type/name/active.
        /// </summary>
        public static H8RegistrySlotInfo ProbeRegistrySlot(Type registryType, string fieldName, string slotName)
        {
            var slot = new H8RegistrySlotInfo();
            slot.slotName = slotName ?? fieldName;

            if (registryType == null)
            {
                slot.isNull = true;
                slot.memberFound = false;
                slot.typeName = "<registry_type_not_found>";
                return slot;
            }

            if (!HasMember(registryType, fieldName, out string kind))
            {
                slot.memberFound = false;
                slot.isNull = false; // Treating as missing diagnostic, not null
                slot.typeName = "<member_not_found>";
                return slot;
            }

            slot.memberFound = true;
            slot.memberName = fieldName;
            slot.memberKind = kind;

            try
            {
                var val = GetStatic(registryType, fieldName);
                if (IsUnityNull(val))
                {
                    slot.isNull = true;
                    slot.typeName = "null";
                    return slot;
                }

                slot.isNull = false;
                slot.typeName = val.GetType().Name;

                if (val is UnityEngine.Object uobj)
                {
                    slot.objectName = uobj != null ? uobj.name : "";
                    if (uobj is Component comp)
                        slot.isActiveIfUnityObject = comp.gameObject != null && comp.gameObject.activeInHierarchy;
                    else if (uobj is GameObject go)
                        slot.isActiveIfUnityObject = go.activeInHierarchy;
                }
                else
                {
                    slot.objectName = val.ToString();
                }
            }
            catch (Exception e)
            {
                slot.isNull = true;
                slot.typeName = $"<error: {e.Message}>";
            }

            return slot;
        }
    }
}
