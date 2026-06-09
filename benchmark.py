import timeit
from collections import Counter

def benchmark():
    using_edges = Counter()
    edges = [["domainA", "domainB", 1]] * 100000

    start = timeit.default_timer()
    for edge in edges:
        using_edges[(str(edge[0]), str(edge[1]))] += int(edge[2])
    print("Baseline:", timeit.default_timer() - start)

    using_edges = Counter()
    start = timeit.default_timer()
    for edge in edges:
        using_edges[(edge[0], edge[1])] += edge[2]
    print("Optimized:", timeit.default_timer() - start)

benchmark()
