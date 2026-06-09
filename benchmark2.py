import json
from collections import Counter
import timeit

def benchmark():
    # simulate the data
    analysis = {"using_edges": [["DomainA", "DomainB", 5]] * 100000}

    def func1():
        using_edges = Counter()
        for edge in analysis.get("using_edges", []):
            if not isinstance(edge, list) or len(edge) != 3:
                continue
            using_edges[(str(edge[0]), str(edge[1]))] += int(edge[2])

    def func2():
        using_edges = Counter()
        for edge in analysis.get("using_edges", []):
            if not isinstance(edge, list) or len(edge) != 3:
                continue
            using_edges[(edge[0], edge[1])] += edge[2]

    print("Baseline:", timeit.timeit(func1, number=100))
    print("Optimized:", timeit.timeit(func2, number=100))

benchmark()
