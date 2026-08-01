import pathlib, os
os.chdir(r"C:/hades/Hecton8")
files=["_l14_idisp_hop.txt","_l14_idisp_gs_1359.txt","_l14_hpih.txt","_l14_hpm_fixed.txt","_l14_hpm_sample.txt","_l14_hpm_reg.txt","_l14_idisp_grep.txt","_l14_hpm_grep.txt","_l14_sd_grep.txt"]
for f in files:
    t=pathlib.Path(f).read_text(encoding="utf-8",errors="replace")
    pathlib.Path(f+".a").write_text(t.encode("ascii","replace").decode("ascii"),encoding="ascii")
    print(f,len(t))
