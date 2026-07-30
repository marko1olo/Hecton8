from pathlib import Path
lit=bytes([92,114,92,110])
crlf=bytes([13,10])
base=Path(r"C:/hades/Hecton8/Tools/_cline_scratch")
for name in ["launch_v0_L06_probe.bat","poll_v0_L06.bat"]:
 p=base/name
 b=p.read_bytes()
 print(name, b.count(lit))
 b2=b.replace(lit, crlf)
 p.write_bytes(b2)
 print("fixed", name, len(b2), b2.count(crlf))
