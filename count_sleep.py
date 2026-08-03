import time

for i in range(1, 6):
    print(i, flush=True)
    if i < 5:
        time.sleep(5)
