#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using MapMagic.Nodes;
using MapMagic.Nodes.MatrixGenerators;
using Den.Tools;
using Den.Tools.Matrices;
using System.Reflection;
using System;
using System.Collections.Generic;

/// <summary>
/// Builds the "Asymmetric Continental Divide" MapMagic 2 graph architecture.
/// Select your MapMagic Graph asset in the Project window, then run Tools > Generate MapMagic Graph.
/// 
/// Uses heavy reflection to bypass read-only properties and API restrictions.
/// </summary>
public static class AsymmetricContinentalDivideGraphBuilder
{
    // ════════════════════════════════════════════════════════════════════
    //  ID GENERATION (ulong)
    // ════════════════════════════════════════════════════════════════════

    private static ulong _idCounter = 1000UL;
    private static readonly List<Generator> s_clearGraphGenerators = new List<Generator>(256);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        _idCounter = 1000UL;
    }

    private static ulong GenerateId()
    {
        return System.Threading.Interlocked.Increment(ref _idCounter);
    }

    // We need a thread-safe Interlocked.Increment for ulong.
    // System.Threading.Interlocked doesn't have an overload for ulong directly,
    // so we wrap it with a long-based approach that is safe for our range.
    private static class System
    {
        public static class Threading
        {
            public static class Interlocked
            {
                public static ulong Increment(ref ulong location)
                {
                    // Safe for values well below long.MaxValue
                    long asLong = (long)location;
                    long result = global::System.Threading.Interlocked.Increment(ref asLong);
                    location = (ulong)result;
                    return (ulong)result;
                }
            }
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //  BLEND ALGORITHM ENUM (mirrors Blend200.BlendAlgorithm)
    // ════════════════════════════════════════════════════════════════════

    private enum BlendAlgorithm
    {
        Mix = 0,
        Add = 1,
        Subtract = 2,
        Multiply = 3,
        Min = 4,
        Max = 5
    }

    /// <summary>Describes one layer to be added to a Blend200 node.</summary>
    private struct BlendLayerDef
    {
        public Generator source;
        public BlendAlgorithm algorithm;
        public float opacity;

        public BlendLayerDef(Generator source, BlendAlgorithm algorithm, float opacity)
        {
            this.source = source;
            this.algorithm = algorithm;
            this.opacity = opacity;
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //  DEEP REFLECTION HELPERS
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Forcibly sets a field on target, searching through the entire type hierarchy
    /// including private, public, and backing fields. This is the nuclear option
    /// for bypassing read-only properties.
    /// </summary>
    private static bool SetPrivateField(object target, string fieldName, object value)
    {
        if (target == null) return false;

        Type type = target.GetType();

        // Walk up the entire inheritance chain
        while (type != null)
        {
            // Try exact field name first
            FieldInfo field = type.GetField(fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

            if (field != null)
            {
                try
                {
                    field.SetValue(target, value);
                    return true;
                }
                catch (global::System.Exception e)
                {
                    Debug.LogWarning($"[GraphBuilder] Failed to set field '{fieldName}' on {type.Name}: {e.Message}");
                }
            }

            type = type.BaseType;
        }

        // If exact name didn't work, try common backing field patterns
        string[] backingPatterns = new string[]
        {
            $"<{fieldName}>k__BackingField",
            $"_{fieldName}",
            $"m_{fieldName}",
            fieldName.Substring(0, 1).ToLower() + fieldName.Substring(1), // PascalCase -> camelCase
        };

        type = target.GetType();
        while (type != null)
        {
            foreach (string pattern in backingPatterns)
            {
                FieldInfo field = type.GetField(pattern,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

                if (field != null)
                {
                    try
                    {
                        field.SetValue(target, value);
                        return true;
                    }
                    catch (global::System.Exception e)
                    {
                        Debug.LogWarning($"[GraphBuilder] Failed to set backing field '{pattern}' on {type.Name}: {e.Message}");
                    }
                }
            }
            type = type.BaseType;
        }

        Debug.LogWarning($"[GraphBuilder] Could not find any field matching '{fieldName}' on {target.GetType().Name}");
        return false;
    }

    /// <summary>
    /// Gets a field value via reflection, searching the full hierarchy.
    /// </summary>
    private static object GetPrivateField(object target, string fieldName)
    {
        if (target == null) return null;

        Type type = target.GetType();

        while (type != null)
        {
            FieldInfo field = type.GetField(fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

            if (field != null)
            {
                return field.GetValue(target);
            }

            type = type.BaseType;
        }

        // Try backing field patterns
        string[] backingPatterns = new string[]
        {
            $"<{fieldName}>k__BackingField",
            $"_{fieldName}",
            $"m_{fieldName}",
            fieldName.Substring(0, 1).ToLower() + fieldName.Substring(1),
        };

        type = target.GetType();
        while (type != null)
        {
            foreach (string pattern in backingPatterns)
            {
                FieldInfo field = type.GetField(pattern,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

                if (field != null)
                {
                    return field.GetValue(target);
                }
            }
            type = type.BaseType;
        }

        return null;
    }

    /// <summary>
    /// Sets a field value using the general-purpose approach. Tries direct field access
    /// first, then falls back to property, then to SetPrivateField.
    /// </summary>
    private static void SetFieldReflection(object target, string fieldName, object value)
    {
        if (target == null) return;

        Type type = target.GetType();

        // Try public field first
        FieldInfo field = type.GetField(fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (field != null)
        {
            try
            {
                field.SetValue(target, value);
                return;
            }
            catch { }
        }

        // Try property
        PropertyInfo prop = type.GetProperty(fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (prop != null && prop.CanWrite)
        {
            try
            {
                prop.SetValue(target, value);
                return;
            }
            catch { }
        }

        // Nuclear fallback
        if (!SetPrivateField(target, fieldName, value))
        {
            Debug.LogWarning($"[GraphBuilder] SetFieldReflection: Could not set '{fieldName}' on {type.Name}");
        }
    }

    /// <summary>Sets an enum field by its integer value, handling type conversion.</summary>
    private static void SetEnumField(object target, string fieldName, int value)
    {
        if (target == null) return;

        Type type = target.GetType();

        // Search the full hierarchy for the field
        FieldInfo field = null;
        Type searchType = type;
        while (searchType != null && field == null)
        {
            field = searchType.GetField(fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            searchType = searchType.BaseType;
        }

        if (field != null && field.FieldType.IsEnum)
        {
            object enumVal = Enum.ToObject(field.FieldType, value);
            field.SetValue(target, enumVal);
        }
        else if (field != null)
        {
            field.SetValue(target, value);
        }
        else
        {
            Debug.LogWarning($"[GraphBuilder] Enum field '{fieldName}' not found on {type.Name}");
        }
    }

    /// <summary>Sets a Vector2 field via reflection.</summary>
    private static void SetVector2Field(object target, string fieldName, Vector2 value)
    {
        SetFieldReflection(target, fieldName, value);
    }

    // ════════════════════════════════════════════════════════════════════
    //  INLET / OUTLET DISCOVERY (Fixed Generic Handling)
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Checks if a given Type implements IOutlet{MatrixWorld} at any level.
    /// </summary>
    private static bool ImplementsOutletMatrixWorld(Type type)
    {
        if (type == null) return false;

        Type targetInterface = typeof(IOutlet<MatrixWorld>);

        // Direct check
        if (targetInterface.IsAssignableFrom(type))
            return true;

        // Check all interfaces
        foreach (Type iface in type.GetInterfaces())
        {
            if (iface == targetInterface)
                return true;

            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IOutlet<>))
            {
                Type[] genericArgs = iface.GetGenericArguments();
                if (genericArgs.Length == 1 && genericArgs[0] == typeof(MatrixWorld))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a given Type implements IInlet{MatrixWorld} at any level.
    /// Handles both concrete generic types and open generic definitions.
    /// </summary>
    private static bool ImplementsInletMatrixWorld(Type type)
    {
        if (type == null) return false;

        Type targetInterface = typeof(IInlet<MatrixWorld>);

        // Direct check
        if (targetInterface.IsAssignableFrom(type))
            return true;

        // Check all interfaces
        foreach (Type iface in type.GetInterfaces())
        {
            if (iface == targetInterface)
                return true;

            if (iface.IsGenericType)
            {
                try
                {
                    Type genDef = iface.GetGenericTypeDefinition();
                    if (genDef == typeof(IInlet<>))
                    {
                        Type[] genericArgs = iface.GetGenericArguments();
                        if (genericArgs.Length == 1 && genericArgs[0] == typeof(MatrixWorld))
                            return true;
                    }
                }
                catch { }
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the first IOutlet{MatrixWorld} from a generator.
    /// Most MapMagic matrix generators implement IOutlet{MatrixWorld} directly.
    /// </summary>
    private static IOutlet<MatrixWorld> GetDefaultOutlet(Generator gen)
    {
        if (gen == null) return null;

        // Most generators implement IOutlet<MatrixWorld> directly
        if (gen is IOutlet<MatrixWorld> directOutlet)
            return directOutlet;

        // Search all fields for outlet types
        Type type = gen.GetType();
        while (type != null)
        {
            FieldInfo[] fields = type.GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

            foreach (FieldInfo f in fields)
            {
                if (ImplementsOutletMatrixWorld(f.FieldType))
                {
                    object val = f.GetValue(gen);
                    if (val is IOutlet<MatrixWorld> outlet)
                        return outlet;
                }
            }

            type = type.BaseType;
        }

        return null;
    }

    /// <summary>
    /// Gets the first IInlet{MatrixWorld} from a generator.
    /// Uses the corrected generic type checking that avoids CS0305.
    /// </summary>
    private static IInlet<MatrixWorld> GetDefaultInlet(Generator gen)
    {
        if (gen == null) return null;

        // Check if generator itself is an inlet
        if (gen is IInlet<MatrixWorld> directInlet)
            return directInlet;

        // Search all fields across the entire type hierarchy
        Type type = gen.GetType();
        while (type != null)
        {
            FieldInfo[] fields = type.GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

            foreach (FieldInfo f in fields)
            {
                // Use our safe generic check instead of typeof(IInlet<>) which causes CS0305
                if (ImplementsInletMatrixWorld(f.FieldType))
                {
                    object val = f.GetValue(gen);
                    if (val is IInlet<MatrixWorld> inlet)
                    {
                        // Ensure the inlet has its Gen reference set
                        EnsureInletGen(inlet, gen);
                        return inlet;
                    }
                }
            }

            type = type.BaseType;
        }

        // Also search properties
        type = gen.GetType();
        while (type != null)
        {
            PropertyInfo[] props = type.GetProperties(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

            foreach (PropertyInfo p in props)
            {
                if (p.CanRead && ImplementsInletMatrixWorld(p.PropertyType))
                {
                    try
                    {
                        object val = p.GetValue(gen);
                        if (val is IInlet<MatrixWorld> inlet)
                        {
                            EnsureInletGen(inlet, gen);
                            return inlet;
                        }
                    }
                    catch { }
                }
            }

            type = type.BaseType;
        }

        return null;
    }

    /// <summary>
    /// Ensures an inlet's Gen (generator) reference is set via reflection.
    /// Cannot assign directly because Gen is read-only.
    /// </summary>
    private static void EnsureInletGen(object inlet, Generator gen)
    {
        if (inlet == null || gen == null) return;

        // Try to read the current Gen value
        object currentGen = GetPrivateField(inlet, "gen");
        if (currentGen == null)
        {
            // Force-set the gen field
            SetPrivateField(inlet, "gen", gen);
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //  INLET CREATION VIA REFLECTION
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates a new Inlet{MatrixWorld} instance and forcibly sets its
    /// Gen and Id fields via reflection, bypassing read-only restrictions.
    /// </summary>
    private static object CreateInletForGenerator(Generator gen)
    {
        // Create a new Inlet<MatrixWorld>
        Type inletType = typeof(Inlet<MatrixWorld>);
        object inlet = Activator.CreateInstance(inletType);

        // Force-set the gen field (backing field for Gen property)
        SetPrivateField(inlet, "gen", gen);
        SetPrivateField(inlet, "Gen", gen);

        // Force-set the id field (backing field for Id property)
        ulong newId = GenerateId();
        SetPrivateField(inlet, "id", newId);
        SetPrivateField(inlet, "Id", newId);

        return inlet;
    }

    // ════════════════════════════════════════════════════════════════════
    //  GRAPH OPERATIONS
    // ════════════════════════════════════════════════════════════════════

    /// <summary>Remove all generators and links from the graph.</summary>
    private static void ClearGraph(Graph graph)
    {
        s_clearGraphGenerators.Clear();

        try
        {
            if (graph.generators != null)
            {
                foreach (Generator generator in graph.generators)
                {
                    if (generator != null)
                        s_clearGraphGenerators.Add(generator);
                }
            }
        }
        catch
        {
            s_clearGraphGenerators.Clear();
        }

        for (int i = 0; i < s_clearGraphGenerators.Count; i++)
        {
            Generator gen = s_clearGraphGenerators[i];
            if (gen != null)
            {
                try { graph.Remove(gen); }
                catch { }
            }
        }

        s_clearGraphGenerators.Clear();

        // Clear links via reflection if direct access fails
        try
        {
            object links = GetPrivateField(graph, "links");
            if (links != null)
            {
                MethodInfo clearMethod = links.GetType().GetMethod("Clear",
                    BindingFlags.Instance | BindingFlags.Public);
                if (clearMethod != null)
                    clearMethod.Invoke(links, null);
            }
        }
        catch { }
    }

    /// <summary>Count generators in the graph.</summary>
    private static int CountGenerators(Graph graph)
    {
        try
        {
            if (graph.generators != null)
                return graph.generators.Length;
        }
        catch { }
        return 0;
    }

    // ════════════════════════════════════════════════════════════════════
    //  GENERATOR FACTORY
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates a generator of type T, assigns a unique ulong ID,
    /// sets its GUI position, enables it, and adds it to the graph.
    /// </summary>
    private static T CreateGenerator<T>(Graph graph, Vector2 guiPos) where T : Generator, new()
    {
        T gen = new T();

        // Set guiPosition
        SetFieldReflection(gen, "guiPosition", guiPos);

        // Set enabled
        SetFieldReflection(gen, "enabled", true);

        // Assign a unique ulong Id via reflection (id property is likely read-only)
        ulong newId = GenerateId();
        SetPrivateField(gen, "id", newId);
        SetPrivateField(gen, "Id", newId);

        // Add to graph
        graph.Add(gen);

        return gen;
    }

    /// <summary>
    /// Attempts to create a generator by type name using reflection.
    /// Used as fallback for nodes like Contrast200 or Terrace200 that
    /// may have different class names across MapMagic versions.
    /// </summary>
    private static Generator CreateGeneratorByName(Graph graph, string typeName, Vector2 guiPos)
    {
        // Search all loaded assemblies for the type
        Type genType = null;

        foreach (global::System.Reflection.Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                Type[] types = asm.GetTypes();
                foreach (Type t in types)
                {
                    if (t.Name == typeName && typeof(Generator).IsAssignableFrom(t))
                    {
                        genType = t;
                        break;
                    }
                }
                if (genType != null) break;
            }
            catch { }
        }

        if (genType == null)
        {
            Debug.LogError($"[GraphBuilder] Could not find generator type: {typeName}");
            return null;
        }

        Generator gen = (Generator)Activator.CreateInstance(genType);

        SetFieldReflection(gen, "guiPosition", guiPos);
        SetFieldReflection(gen, "enabled", true);

        ulong newId = GenerateId();
        SetPrivateField(gen, "id", newId);
        SetPrivateField(gen, "Id", newId);

        graph.Add(gen);

        return gen;
    }

    // ════════════════════════════════════════════════════════════════════
    //  NODE LINKING
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Links the default outlet of 'from' to the default inlet of 'to'.
    /// Uses Graph.Link() which is the safe API method.
    /// </summary>
    private static void LinkNodes(Graph graph, Generator from, Generator to)
    {
        IOutlet<MatrixWorld> outlet = GetDefaultOutlet(from);
        IInlet<MatrixWorld> inlet = GetDefaultInlet(to);

        if (outlet == null)
        {
            Debug.LogError($"[GraphBuilder] No outlet found on {from.GetType().Name}");
            return;
        }
        if (inlet == null)
        {
            Debug.LogError($"[GraphBuilder] No inlet found on {to.GetType().Name}");
            return;
        }

        try
        {
            graph.Link(outlet, inlet);
        }
        catch (global::System.Exception e)
        {
            Debug.LogError($"[GraphBuilder] Failed to link {from.GetType().Name} -> {to.GetType().Name}: {e.Message}");

            // Fallback: try to link via reflection on the graph's internal link storage
            TryLinkViaReflection(graph, outlet, inlet);
        }
    }

    /// <summary>
    /// Fallback link method that directly manipulates the graph's internal link dictionary.
    /// </summary>
    private static void TryLinkViaReflection(Graph graph, object outlet, object inlet)
    {
        try
        {
            // MapMagic Graph stores links in a dictionary-like structure
            // Try to find and invoke an internal link method
            MethodInfo[] methods = typeof(Graph).GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            foreach (MethodInfo m in methods)
            {
                if (m.Name == "Link" || m.Name == "link" || m.Name == "AddLink")
                {
                    ParameterInfo[] parms = m.GetParameters();
                    if (parms.Length == 2)
                    {
                        try
                        {
                            m.Invoke(graph, new object[] { outlet, inlet });
                            Debug.Log("[GraphBuilder] Linked via reflection fallback.");
                            return;
                        }
                        catch { }
                    }
                }
            }

            // Last resort: set LinkedOutletId and LinkedGenId on the inlet
            if (outlet is Generator outGen)
            {
                ulong outletId = 0;
                object idVal = GetPrivateField(outlet, "id");
                if (idVal == null) idVal = GetPrivateField(outlet, "Id");
                if (idVal != null) outletId = Convert.ToUInt64(idVal);

                ulong genId = 0;
                object genIdVal = GetPrivateField(outGen, "id");
                if (genIdVal == null) genIdVal = GetPrivateField(outGen, "Id");
                if (genIdVal != null) genId = Convert.ToUInt64(genIdVal);

                SetPrivateField(inlet, "LinkedOutletId", outletId);
                SetPrivateField(inlet, "LinkedGenId", genId);
            }
        }
        catch (global::System.Exception e)
        {
            Debug.LogError($"[GraphBuilder] Reflection link fallback also failed: {e.Message}");
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //  BLEND NODE CONSTRUCTION
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates a Blend200 node with the specified layers.
    /// The first BlendLayerDef is always the "base" layer.
    /// Creates inlets via reflection to bypass read-only restrictions.
    /// </summary>
    private static Blend200 CreateBlend(Graph graph, Vector2 guiPos, params BlendLayerDef[] layerDefs)
    {
        Blend200 blend = CreateGenerator<Blend200>(graph, guiPos);

        // Get the Layer type
        Type layerType = typeof(Blend200).GetNestedType("Layer",
            BindingFlags.Public | BindingFlags.NonPublic);

        if (layerType == null)
        {
            Debug.LogError("[GraphBuilder] Could not find Blend200.Layer type!");
            return blend;
        }

        // Create the layers array
        Array layersArray = Array.CreateInstance(layerType, layerDefs.Length);

        for (int i = 0; i < layerDefs.Length; i++)
        {
            // Create a new Layer instance
            object layer = Activator.CreateInstance(layerType);

            // Set the algorithm via reflection
            SetBlendLayerAlgorithm(layer, layerType, (int)layerDefs[i].algorithm);

            // Set opacity
            SetFieldReflection(layer, "opacity", layerDefs[i].opacity);

            // Create and configure the inlet
            object inlet = CreateInletForGenerator(blend);

            // Set the inlet on the layer
            SetFieldReflection(layer, "inlet", inlet);

            layersArray.SetValue(layer, i);
        }

        // Set the layers array on the blend node
        SetFieldReflection(blend, "layers", layersArray);

        // Now wire the inlets to their source generators' outlets
        // We need to re-read the layers after assignment to get the actual references
        object assignedLayers = GetPrivateField(blend, "layers");
        if (assignedLayers is Array assignedArray)
        {
            for (int i = 0; i < layerDefs.Length; i++)
            {
                if (layerDefs[i].source == null) continue;

                object layer = assignedArray.GetValue(i);
                if (layer == null) continue;

                // Get the inlet from the layer
                object inletObj = GetPrivateField(layer, "inlet");
                if (inletObj == null) continue;

                // Get the outlet from the source
                IOutlet<MatrixWorld> outlet = GetDefaultOutlet(layerDefs[i].source);
                if (outlet == null)
                {
                    Debug.LogWarning($"[GraphBuilder] No outlet on {layerDefs[i].source.GetType().Name} for blend layer {i}");
                    continue;
                }

                // Link them
                if (inletObj is IInlet<MatrixWorld> typedInlet)
                {
                    try
                    {
                        graph.Link(outlet, typedInlet);
                    }
                    catch
                    {
                        // Fallback: set the linked IDs directly on the inlet
                        TryLinkViaReflection(graph, outlet, inletObj);
                    }
                }
                else
                {
                    // The inlet may not be castable directly; try reflection link
                    TryLinkViaReflection(graph, outlet, inletObj);
                }
            }
        }

        return blend;
    }

    /// <summary>
    /// Sets the algorithm enum on a Blend200.Layer via reflection.
    /// </summary>
    private static void SetBlendLayerAlgorithm(object layer, Type layerType, int algorithmValue)
    {
        FieldInfo field = layerType.GetField("algorithm",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (field != null && field.FieldType.IsEnum)
        {
            object enumVal = Enum.ToObject(field.FieldType, algorithmValue);
            field.SetValue(layer, enumVal);
        }
        else if (field != null)
        {
            field.SetValue(layer, algorithmValue);
        }
        else
        {
            Debug.LogWarning("[GraphBuilder] Could not find 'algorithm' field on Blend200.Layer");
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //  CURVE SETUP
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Bell/Parabola curve: (0, 0.05) -> (0.5, 1.0) with flat tangents -> (1, 0.05)
    /// </summary>
    private static void SetBellCurve(Generator curveNode)
    {
        // Create a new Den.Tools.Curve
        Type curveType = typeof(Den.Tools.Curve);
        object curve = Activator.CreateInstance(curveType);

        // Create the Node type
        Type nodeType = curveType.GetNestedType("Node",
            BindingFlags.Public | BindingFlags.NonPublic);

        if (nodeType == null)
        {
            Debug.LogError("[GraphBuilder] Could not find Den.Tools.Curve.Node type!");
            return;
        }

        // Create 3 nodes for the bell curve
        Array nodes = Array.CreateInstance(nodeType, 3);

        // Node 0: (0, 0.05) - start low
        object node0 = Activator.CreateInstance(nodeType);
        SetFieldReflection(node0, "pos", new Vector2(0f, 0.05f));
        SetFieldReflection(node0, "inTangent", 0f);
        SetFieldReflection(node0, "outTangent", 0f);
        SetFieldReflection(node0, "linear", false);
        nodes.SetValue(node0, 0);

        // Node 1: (0.5, 1.0) - peak with flat tangents
        object node1 = Activator.CreateInstance(nodeType);
        SetFieldReflection(node1, "pos", new Vector2(0.5f, 1.0f));
        SetFieldReflection(node1, "inTangent", 0f);
        SetFieldReflection(node1, "outTangent", 0f);
        SetFieldReflection(node1, "linear", false);
        nodes.SetValue(node1, 1);

        // Node 2: (1, 0.05) - end low
        object node2 = Activator.CreateInstance(nodeType);
        SetFieldReflection(node2, "pos", new Vector2(1f, 0.05f));
        SetFieldReflection(node2, "inTangent", 0f);
        SetFieldReflection(node2, "outTangent", 0f);
        SetFieldReflection(node2, "linear", false);
        nodes.SetValue(node2, 2);

        // Set points on the curve
        SetFieldReflection(curve, "points", nodes);

        // Recalculate LUT
        RecalcCurveLut(curve, curveType);

        // Set the curve on the node
        SetFieldReflection(curveNode, "curve", curve);
    }

    /// <summary>
    /// Trench curve: flat with a spike in the middle.
    /// (0,0) -> (0.45,0) -> (0.5,1) -> (0.55,0) -> (1,0)
    /// </summary>
    private static void SetTrenchCurve(Generator curveNode)
    {
        Type curveType = typeof(Den.Tools.Curve);
        object curve = Activator.CreateInstance(curveType);

        Type nodeType = curveType.GetNestedType("Node",
            BindingFlags.Public | BindingFlags.NonPublic);

        if (nodeType == null)
        {
            Debug.LogError("[GraphBuilder] Could not find Den.Tools.Curve.Node type!");
            return;
        }

        Array nodes = Array.CreateInstance(nodeType, 5);

        // Node 0: (0, 0)
        object n0 = Activator.CreateInstance(nodeType);
        SetFieldReflection(n0, "pos", new Vector2(0f, 0f));
        SetFieldReflection(n0, "inTangent", 0f);
        SetFieldReflection(n0, "outTangent", 0f);
        SetFieldReflection(n0, "linear", true);
        nodes.SetValue(n0, 0);

        // Node 1: (0.45, 0)
        object n1 = Activator.CreateInstance(nodeType);
        SetFieldReflection(n1, "pos", new Vector2(0.45f, 0f));
        SetFieldReflection(n1, "inTangent", 0f);
        SetFieldReflection(n1, "outTangent", 0f);
        SetFieldReflection(n1, "linear", true);
        nodes.SetValue(n1, 1);

        // Node 2: (0.5, 1) - the spike
        object n2 = Activator.CreateInstance(nodeType);
        SetFieldReflection(n2, "pos", new Vector2(0.5f, 1f));
        SetFieldReflection(n2, "inTangent", 0f);
        SetFieldReflection(n2, "outTangent", 0f);
        SetFieldReflection(n2, "linear", true);
        nodes.SetValue(n2, 2);

        // Node 3: (0.55, 0)
        object n3 = Activator.CreateInstance(nodeType);
        SetFieldReflection(n3, "pos", new Vector2(0.55f, 0f));
        SetFieldReflection(n3, "inTangent", 0f);
        SetFieldReflection(n3, "outTangent", 0f);
        SetFieldReflection(n3, "linear", true);
        nodes.SetValue(n3, 3);

        // Node 4: (1, 0)
        object n4 = Activator.CreateInstance(nodeType);
        SetFieldReflection(n4, "pos", new Vector2(1f, 0f));
        SetFieldReflection(n4, "inTangent", 0f);
        SetFieldReflection(n4, "outTangent", 0f);
        SetFieldReflection(n4, "linear", true);
        nodes.SetValue(n4, 4);

        SetFieldReflection(curve, "points", nodes);
        RecalcCurveLut(curve, curveType);
        SetFieldReflection(curveNode, "curve", curve);
    }

    /// <summary>
    /// Attempts to recalculate the LUT for a Den.Tools.Curve instance.
    /// Tries multiple method names across MapMagic versions.
    /// </summary>
    private static void RecalcCurveLut(object curve, Type curveType)
    {
        string[] methodNames = { "Bake", "CalcLut", "UpdateLut", "RecalcLut", "Calculate" };

        foreach (string methodName in methodNames)
        {
            MethodInfo method = curveType.GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (method != null)
            {
                ParameterInfo[] parms = method.GetParameters();
                if (parms.Length == 0)
                {
                    try
                    {
                        method.Invoke(curve, null);
                        return;
                    }
                    catch (global::System.Exception e)
                    {
                        Debug.LogWarning($"[GraphBuilder] Curve.{methodName}() failed: {e.Message}");
                    }
                }
            }
        }

        // If no recalc method works, null out the LUT so MapMagic recalculates at runtime
        SetFieldReflection(curve, "lut", null);
    }

    // ════════════════════════════════════════════════════════════════════
    //  LEVELS NODE SETUP
    // ════════════════════════════════════════════════════════════════════

    private static void SetLevelsParams(Generator levels, float inLow, float inHigh,
        float gamma, float outLow, float outHigh)
    {
        SetFieldReflection(levels, "inLow", inLow);
        SetFieldReflection(levels, "inHigh", inHigh);
        SetFieldReflection(levels, "gamma", gamma);
        SetFieldReflection(levels, "outLow", outLow);
        SetFieldReflection(levels, "outHigh", outHigh);
    }

    // ════════════════════════════════════════════════════════════════════
    //  CONTRAST / NORMALIZE NODE (with fallback)
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates a Contrast node. If Contrast200 doesn't exist in this version,
    /// falls back to Normalize200 or searches by name.
    /// </summary>
    private static Generator CreateContrastNode(Graph graph, Vector2 guiPos,
        float intensity, float contrast)
    {
        // Try Contrast200 first
        Generator gen = TryCreateGeneratorByExactType<Contrast200>(graph, guiPos);

        if (gen != null)
        {
            SetFieldReflection(gen, "intensity", intensity);
            SetFieldReflection(gen, "contrast", contrast);
            return gen;
        }

        // Fallback: search for common contrast/normalize node names
        string[] fallbackNames = {
            "Contrast200", "Normalize200", "Contrast", "Normalize",
            "ContrastMatrix200", "NormalizeMatrix200"
        };

        foreach (string name in fallbackNames)
        {
            gen = CreateGeneratorByName(graph, name, guiPos);
            if (gen != null)
            {
                SetFieldReflection(gen, "intensity", intensity);
                SetFieldReflection(gen, "contrast", contrast);
                return gen;
            }
        }

        // Last resort: create a Levels node configured to act as contrast
        Debug.LogWarning("[GraphBuilder] No Contrast/Normalize node found. Using Levels as substitute.");
        gen = CreateGenerator<Levels200>(graph, guiPos);
        float clamped = Mathf.Clamp01(1f / (contrast + 0.001f));
        SetLevelsParams(gen, 0.5f - clamped * 0.5f, 0.5f + clamped * 0.5f, 1f, 0f, 1f);
        return gen;
    }

    /// <summary>
    /// Tries to create a generator of a specific type. Returns null if the type
    /// doesn't compile or isn't available.
    /// </summary>
    private static Generator TryCreateGeneratorByExactType<T>(Graph graph, Vector2 guiPos) where T : Generator
    {
        try
        {
            Type t = typeof(T);
            if (t == null) return null;

            Generator gen = (Generator)Activator.CreateInstance(t);

            SetFieldReflection(gen, "guiPosition", guiPos);
            SetFieldReflection(gen, "enabled", true);

            ulong newId = GenerateId();
            SetPrivateField(gen, "id", newId);
            SetPrivateField(gen, "Id", newId);

            graph.Add(gen);
            return gen;
        }
        catch
        {
            return null;
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //  TERRACE NODE (with fallback)
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates a Terrace node. Handles the case where Terrace200 may have
    /// a different name across versions.
    /// </summary>
    private static Generator CreateTerraceNode(Graph graph, Vector2 guiPos,
        int num, float steepness, float uniformity)
    {
        Generator gen = TryCreateGeneratorByExactType<Terrace200>(graph, guiPos);

        if (gen != null)
        {
            SetFieldReflection(gen, "num", num);
            SetFieldReflection(gen, "steepness", steepness);
            SetFieldReflection(gen, "uniformity", uniformity);
            return gen;
        }

        // Fallback search
        string[] fallbackNames = { "Terrace200", "Terrace", "TerraceMatrix200" };
        foreach (string name in fallbackNames)
        {
            gen = CreateGeneratorByName(graph, name, guiPos);
            if (gen != null)
            {
                SetFieldReflection(gen, "num", num);
                SetFieldReflection(gen, "steepness", steepness);
                SetFieldReflection(gen, "uniformity", uniformity);
                return gen;
            }
        }

        Debug.LogError("[GraphBuilder] Could not create Terrace node in any form!");
        return null;
    }

    // ════════════════════════════════════════════════════════════════════
    //  MAIN ENTRY POINT
    // ════════════════════════════════════════════════════════════════════

    [MenuItem("Tools/Generate MapMagic Graph")]
    public static void GenerateGraph()
    {
        // Reset ID counter for each generation
        _idCounter = 1000UL;

        // ── Validate Selection ──────────────────────────────────────
        Graph graph = Selection.activeObject as Graph;
        if (graph == null)
        {
            EditorUtility.DisplayDialog(
                "MapMagic Graph Builder",
                "Please select a MapMagic Graph asset in the Project window first.\n\n" +
                "The asset should be a MapMagic Graph (.asset) file.",
                "OK");
            return;
        }

        Undo.RecordObject(graph, "Generate Asymmetric Continental Divide Graph");

        try
        {
            BuildGraph(graph);
        }
        catch (global::System.Exception e)
        {
            Debug.LogError($"[GraphBuilder] Graph generation failed: {e}\n{e.StackTrace}");
            EditorUtility.DisplayDialog(
                "MapMagic Graph Builder - Error",
                $"Graph generation failed:\n{e.Message}\n\nCheck the Console for details.",
                "OK");
        }
    }

    private static void BuildGraph(Graph graph)
    {
        // ── Clear existing generators ───────────────────────────────
        ClearGraph(graph);

        Debug.Log("[GraphBuilder] Starting Asymmetric Continental Divide graph construction...");

        // ════════════════════════════════════════════════════════════
        //  PART 1: THE MACRO SHAPE (ISLAND RIDGE)
        // ════════════════════════════════════════════════════════════

        // ── Node 1: IMPORT (Base Terrain) ───────────────────────────
        Debug.Log("[GraphBuilder] Creating Node 1: Import200...");
        Import200 import200 = CreateGenerator<Import200>(graph, new Vector2(-800, -200));
        SetFieldReflection(import200, "scale", 15f);
        SetVector2Field(import200, "offset", new Vector2(-12000f, -7500f));
        SetEnumField(import200, "wrapMode", 0); // Clamp
        // Note: matrixAsset must be assigned manually in the editor

        // ── Node 2: SIMPLE FORM (Sinking Gradient) ──────────────────
        Debug.Log("[GraphBuilder] Creating Node 2: SimpleForm200 (Gradient Z)...");
        SimpleForm200 simpleForm = CreateGenerator<SimpleForm200>(graph, new Vector2(-800, 200));
        SetEnumField(simpleForm, "type", 3); // GradientZ
        SetFieldReflection(simpleForm, "intensity", 1f);
        SetFieldReflection(simpleForm, "scale", 15f);
        SetFieldReflection(simpleForm, "ratio", 1f);
        SetVector2Field(simpleForm, "offset", new Vector2(0f, -7500f));
        SetEnumField(simpleForm, "wrap", 0); // Clamp

        // ── Node 3: NOISE 1 (Domain Warp for Gradient) ──────────────
        Debug.Log("[GraphBuilder] Creating Node 3: Noise200 (Simplex, domain warp)...");
        Noise200 noise1 = CreateGenerator<Noise200>(graph, new Vector2(-800, 500));
        SetEnumField(noise1, "type", 0); // Simplex
        SetFieldReflection(noise1, "intensity", 1f);
        SetFieldReflection(noise1, "size", 3000f);
        SetFieldReflection(noise1, "detail", 0.3f);
        SetFieldReflection(noise1, "turbulence", 0f);
        SetFieldReflection(noise1, "seed", 12345);

        // ── Node 4: BLEND 1 (Warping the Gradient) ──────────────────
        Debug.Log("[GraphBuilder] Creating Node 4: Blend200 (Mix, warp gradient)...");
        Blend200 blend1 = CreateBlend(graph, new Vector2(-400, 300),
            new BlendLayerDef(simpleForm, BlendAlgorithm.Mix, 1.0f),    // Base
            new BlendLayerDef(noise1,     BlendAlgorithm.Mix, 0.15f)    // Layer 1: Mix at 0.15
        );

        // ── Node 5: CURVE 1 (Making the Ridge — Bell/Parabola) ─────
        Debug.Log("[GraphBuilder] Creating Node 5: Curve200 (Bell curve)...");
        Curve200 curve1 = CreateGenerator<Curve200>(graph, new Vector2(0, 300));
        SetBellCurve(curve1);
        LinkNodes(graph, blend1, curve1);

        // ── Node 6: NOISE 2 (Macro Noise) ───────────────────────────
        Debug.Log("[GraphBuilder] Creating Node 6: Noise200 (Macro simplex)...");
        Noise200 noise2 = CreateGenerator<Noise200>(graph, new Vector2(-800, -500));
        SetEnumField(noise2, "type", 0); // Simplex
        SetFieldReflection(noise2, "intensity", 1f);
        SetFieldReflection(noise2, "size", 2000f);
        SetFieldReflection(noise2, "detail", 0f);
        SetFieldReflection(noise2, "turbulence", 0f);
        SetFieldReflection(noise2, "seed", 54321);

        // ── Node 7: BLEND 2 (Adding Noise to Import) ────────────────
        Debug.Log("[GraphBuilder] Creating Node 7: Blend200 (Add noise to import)...");
        Blend200 blend2 = CreateBlend(graph, new Vector2(-400, -300),
            new BlendLayerDef(import200, BlendAlgorithm.Mix, 1.0f),     // Base
            new BlendLayerDef(noise2,    BlendAlgorithm.Add, 0.15f)     // Layer 1: Add at 0.15
        );

        // ── Node 8: BLEND 3 (Applying Sinking Mask) = MAIN_SHAPE ───
        Debug.Log("[GraphBuilder] Creating Node 8: Blend200 (Multiply mask) -> MAIN_SHAPE...");
        Blend200 blend3_mainShape = CreateBlend(graph, new Vector2(200, 0),
            new BlendLayerDef(blend2, BlendAlgorithm.Mix,      1.0f),   // Base
            new BlendLayerDef(curve1, BlendAlgorithm.Multiply, 1.0f)    // Layer 1: Multiply at 1.0
        );

        // ════════════════════════════════════════════════════════════
        //  PART 2: TERRACES (ALPINE FJORDS)
        // ════════════════════════════════════════════════════════════

        // ── Node 9: TERRACE ──────────────────────────────────────────
        Debug.Log("[GraphBuilder] Creating Node 9: Terrace200 -> TERRACED_SHAPE...");
        Generator terrace = CreateTerraceNode(graph, new Vector2(500, 0),
            num: 40, steepness: 0.95f, uniformity: 0.5f);

        if (terrace != null)
        {
            LinkNodes(graph, blend3_mainShape, terrace);
        }
        else
        {
            Debug.LogError("[GraphBuilder] CRITICAL: Terrace node creation failed. Using MAIN_SHAPE directly.");
            terrace = blend3_mainShape; // Fallback: skip terracing
        }

        // ════════════════════════════════════════════════════════════
        //  PART 3: MACRO PLATEAUS (FLAT TOPS)
        // ════════════════════════════════════════════════════════════

        // ── Node 10: NOISE 3 (Plateau Blocks — Voronoi) ─────────────
        Debug.Log("[GraphBuilder] Creating Node 10: Noise200 (Voronoi plateaus)...");
        Noise200 noise3 = CreateGenerator<Noise200>(graph, new Vector2(300, 500));
        SetEnumField(noise3, "type", 3); // Voronoi
        SetFieldReflection(noise3, "intensity", 1f);
        SetFieldReflection(noise3, "size", 2000f);
        SetFieldReflection(noise3, "detail", 0f);
        SetFieldReflection(noise3, "turbulence", 0f);
        SetFieldReflection(noise3, "seed", 67890);

        // ── Node 11: LEVELS 1 (Flattening the blocks) ───────────────
        Debug.Log("[GraphBuilder] Creating Node 11: Levels200 (Flatten plateaus)...");
        Levels200 levels1 = CreateGenerator<Levels200>(graph, new Vector2(600, 500));
        SetLevelsParams(levels1, inLow: 0f, inHigh: 1f, gamma: 1f, outLow: 0.2f, outHigh: 0.4f);
        LinkNodes(graph, noise3, levels1);

        // ── Node 12: BLEND 4 (Injecting Plateaus) = SHAPE_WITH_PLATEAUS
        Debug.Log("[GraphBuilder] Creating Node 12: Blend200 (Max plateaus) -> SHAPE_WITH_PLATEAUS...");
        Blend200 blend4_plateaus = CreateBlend(graph, new Vector2(800, 200),
            new BlendLayerDef(terrace, BlendAlgorithm.Mix, 1.0f),       // Base
            new BlendLayerDef(levels1, BlendAlgorithm.Max, 0.5f)        // Layer 1: Max at 0.5
        );

        // ════════════════════════════════════════════════════════════
        //  PART 4: RIVER CANYONS (TRENCHES)
        // ════════════════════════════════════════════════════════════

        // ── Node 13: NOISE 4 (Canyon Veins) ─────────────────────────
        Debug.Log("[GraphBuilder] Creating Node 13: Noise200 (Canyon veins)...");
        Noise200 noise4 = CreateGenerator<Noise200>(graph, new Vector2(500, -500));
        SetEnumField(noise4, "type", 0); // Simplex
        SetFieldReflection(noise4, "intensity", 1f);
        SetFieldReflection(noise4, "size", 1500f);
        SetFieldReflection(noise4, "detail", 0f);
        SetFieldReflection(noise4, "turbulence", 0f);
        SetFieldReflection(noise4, "seed", 11111);

        // ── Node 14: CURVE 2 (Trench Form — spike in middle) ────────
        Debug.Log("[GraphBuilder] Creating Node 14: Curve200 (Trench spike)...");
        Curve200 curve2 = CreateGenerator<Curve200>(graph, new Vector2(800, -500));
        SetTrenchCurve(curve2);
        LinkNodes(graph, noise4, curve2);

        // ── Node 15: NOISE 5 (Canyon Clusters Mask) ─────────────────
        Debug.Log("[GraphBuilder] Creating Node 15: Noise200 (Canyon mask)...");
        Noise200 noise5 = CreateGenerator<Noise200>(graph, new Vector2(500, -800));
        SetEnumField(noise5, "type", 0); // Simplex
        SetFieldReflection(noise5, "intensity", 1f);
        SetFieldReflection(noise5, "size", 2500f);
        SetFieldReflection(noise5, "detail", 0.2f);
        SetFieldReflection(noise5, "turbulence", 0f);
        SetFieldReflection(noise5, "seed", 22222);

        // ── Node 16: CONTRAST 1 (Hard Mask) ─────────────────────────
        Debug.Log("[GraphBuilder] Creating Node 16: Contrast/Normalize (Hard mask)...");
        Generator contrast1 = CreateContrastNode(graph, new Vector2(800, -800),
            intensity: 1f, contrast: 2.0f);
        LinkNodes(graph, noise5, contrast1);

        // ── Node 17: BLEND 5 (Masking the Canyons) ──────────────────
        Debug.Log("[GraphBuilder] Creating Node 17: Blend200 (Multiply canyon mask)...");
        Blend200 blend5 = CreateBlend(graph, new Vector2(1100, -650),
            new BlendLayerDef(curve2,    BlendAlgorithm.Mix,      1.0f),  // Base
            new BlendLayerDef(contrast1, BlendAlgorithm.Multiply, 1.0f)   // Layer 1: Multiply at 1.0
        );

        // ── Node 18: BLEND 6 (Carving into Terrain) ─────────────────
        Debug.Log("[GraphBuilder] Creating Node 18: Blend200 (Subtract canyons) -> FINAL...");
        Blend200 blend6_final = CreateBlend(graph, new Vector2(1300, 0),
            new BlendLayerDef(blend4_plateaus, BlendAlgorithm.Mix,      1.0f),  // Base
            new BlendLayerDef(blend5,          BlendAlgorithm.Subtract, 0.15f)  // Layer 1: Subtract at 0.15
        );

        // ════════════════════════════════════════════════════════════
        //  PART 5: OUTPUT
        // ════════════════════════════════════════════════════════════

        // ── Node 19: HEIGHT OUTPUT ──────────────────────────────────
        Debug.Log("[GraphBuilder] Creating Node 19: HeightOutput200...");
        HeightOutput200 heightOut = CreateGenerator<HeightOutput200>(graph, new Vector2(1600, 0));
        SetEnumField(heightOut, "outputLevel", 3); // OutputLevel.Both = 3
        LinkNodes(graph, blend6_final, heightOut);

        // ── Finalize ────────────────────────────────────────────────
        EditorUtility.SetDirty(graph);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        int count = CountGenerators(graph);
        Debug.Log(
            $"<color=green>[GraphBuilder] SUCCESS!</color> " +
            $"Asymmetric Continental Divide graph built with {count} generators.\n" +
            $"Node breakdown:\n" +
            $"  - Import200 x1\n" +
            $"  - SimpleForm200 x1\n" +
            $"  - Noise200 x5\n" +
            $"  - Blend200 x6\n" +
            $"  - Curve200 x2\n" +
            $"  - Terrace200 x1\n" +
            $"  - Levels200 x1\n" +
            $"  - Contrast/Normalize x1\n" +
            $"  - HeightOutput200 x1\n" +
            $"  TOTAL: {count}");

        EditorUtility.DisplayDialog(
            "MapMagic Graph Builder",
            $"Graph built successfully with {count} generators!\n\n" +
            "IMPORTANT: You must manually assign the Import200 node's\n" +
            "'matrixAsset' reference in the MapMagic graph editor.",
            "OK");
    }
}
#endif
