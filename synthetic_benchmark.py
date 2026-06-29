import timeit

setup_code = """
class CandiceSaveItem:
    def __init__(self):
        self.text = type('obj', (object,), {'text': ''})()
        self.path = ""

class GameObject:
    def __init__(self):
        self._components = {"CandiceSaveItem": CandiceSaveItem()}

    def GetComponent(self, name):
        # Simulate interop overhead
        for i in range(10): pass
        return self._components.get(name)

    def TryGetComponent(self, name):
        # Simulate lower overhead
        return True, self._components.get(name)

obj = GameObject()
names = ["folder", "file.cndc"]
folderName = "saves"
"""

benchmark_unoptimized = """
# Unoptimized (2 GetComponents)
obj.GetComponent("CandiceSaveItem").text.text = names[-1].split('.')[0]
obj.GetComponent("CandiceSaveItem").path = folderName + "/" + names[-1]
"""

benchmark_optimized = """
# Optimized (1 TryGetComponent and cache)
success, saveItem = obj.TryGetComponent("CandiceSaveItem")
if success:
    saveItem.text.text = names[-1].split('.')[0]
    saveItem.path = folderName + "/" + names[-1]
"""

# Run benchmarks
n_iterations = 1000000
time_unoptimized = timeit.timeit(stmt=benchmark_unoptimized, setup=setup_code, number=n_iterations)
time_optimized = timeit.timeit(stmt=benchmark_optimized, setup=setup_code, number=n_iterations)

print(f"Baseline (2 GetComponents): {time_unoptimized:.4f} seconds")
print(f"Optimized (TryGetComponent + Cache): {time_optimized:.4f} seconds")
print(f"Improvement: {(time_unoptimized - time_optimized) / time_unoptimized * 100:.2f}% faster")
