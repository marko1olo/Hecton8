using System;
using System.Collections.Generic;

public class Prototype {
    public int id;
    public bool regardPrefabRotation = false;
    public bool regardPrefabScale = false;
}

public class Pool {
    public Prototype prototype;
    public Pool(Prototype p) { prototype = p; }
    public void Clear() { }
}

public class ObjectsPoolTest {
    public Pool[] pools = new Pool[0];

    public void ClearPrototypes (Prototype[] prototypes)
    {
        HashSet<Prototype> prototypesHashSet = new HashSet<Prototype>(prototypes);

        List<Pool> newPools = new List<Pool>();
        for (int i=0; i<pools.Length; i++)
        {
            if (prototypesHashSet.Contains(pools[i].prototype))
                pools[i].Clear();
            else
                newPools.Add(pools[i]);
        }

        pools = newPools.ToArray();
    }

    public void SetPrototypes (Prototype[] prototypes)
    {
        //clearing pools with null prefab (rare case)
        // Omitted null check for brevity

        // identify prototypes that are no longer used
        HashSet<Prototype> newPrototypes = new HashSet<Prototype>(prototypes);
        List<Prototype> prototypesToClear = new List<Prototype>();
        for (int i=0; i<pools.Length; i++)
        {
            if (!newPrototypes.Contains(pools[i].prototype))
                prototypesToClear.Add(pools[i].prototype);
        }

        // clear them using the existing method
        ClearPrototypes(prototypesToClear.ToArray());

        // reposition
        Pool[] newPools = new Pool[prototypes.Length];
        List<Pool> remainingPools = new List<Pool>(pools);

        for (int i=0; i<prototypes.Length; i++)
        {
            int index = remainingPools.FindIndex(p => p.prototype == prototypes[i]);
            Pool pool;
            if (index >= 0)
            {
                pool = remainingPools[index];
                remainingPools.RemoveAt(index);
            }
            else
            {
                pool = new Pool(prototypes[i]);
            }

            //other than instantiateClones and allowReposition (they are in comparer) should be copied
            pool.prototype.regardPrefabRotation = prototypes[i].regardPrefabRotation;
            pool.prototype.regardPrefabScale = prototypes[i].regardPrefabScale;

            newPools[i] = pool;
        }

        // clearing unused duplicates
        for (int i=0; i<remainingPools.Count; i++)
        {
            remainingPools[i].Clear();
        }

        pools = newPools;
    }
}
class Program { static void Main() {
    var t = new ObjectsPoolTest();
    t.pools = new Pool[] { new Pool(new Prototype { id = 1 }), new Pool(new Prototype { id = 2 }) };
    t.SetPrototypes(new Prototype[] { t.pools[1].prototype, new Prototype { id = 3 } });
    Console.WriteLine(t.pools.Length);
} }
