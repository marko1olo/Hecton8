from pathlib import Path
P=chr(37)
root=Path(r"C:/hades/Hecton8")
def wb(path, text):
    path.parent.mkdir(parents=True, exist_ok=True)
    data=text.replace(chr(10), chr(13)+chr(10)).encode("utf-8")
    path.write_bytes(data)
    print("WROTE", path, path.stat().st_size)
print("header_ok")
