using System;
using System.Reflection;
using UnityEditor.Experimental.GraphView;
public class Test {
    public static void Run() {
        foreach (var p in typeof(ITemplateHelper).GetProperties()) {
            Console.WriteLine(p.PropertyType.Name + " " + p.Name + " (canRead: " + p.CanRead + ", canWrite: " + p.CanWrite + ")");
        }
        foreach (var m in typeof(ITemplateHelper).GetMethods()) {
            Console.WriteLine(m.ReturnType.Name + " " + m.Name);
        }
    }
}
