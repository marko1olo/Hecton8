using System;
using System.Collections.Generic;

class Prototype {
    public string prefab;
    public override int GetHashCode() { return prefab.GetHashCode(); }
    public Prototype(string p) { prefab = p; }
    public Prototype(Prototype p) { prefab = p.prefab; }
}

class Program {
    static void Main() {
        Prototype p1 = new Prototype("test");
        Prototype p2 = new Prototype(p1);
        Dictionary<Prototype, string> dict = new Dictionary<Prototype, string>();
        dict.Add(p2, "pool");
        Console.WriteLine(dict.ContainsKey(p1));
    }
}
