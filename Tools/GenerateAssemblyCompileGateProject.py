#!/usr/bin/env python
"""Generate a lock-free compile-gate .csproj for a Unity asmdef that does not have one.

WHY THIS EXISTS
---------------
The 56 .csproj files at this repo root are HAND-WRITTEN, not Unity-generated, and they are gitignored
(`.gitignore:68  *.csproj`), so they exist only on a machine where somebody typed them. The scaffold was
frozen 2026-06-12. Any asmdef created after that date has no project file, is invisible to the lock-free
`dotnet build` gate in CONTRIBUTING.md, and is invisible to `Tools/PlayerConfigCompileGate.py` - which
globs `Hecton8.*.csproj` and therefore gates whatever happens to be on disk.

Measured 2026-07-29: 179 first-party `Hecton8.*` assemblies exist in Library/ScriptAssemblies and 7 had a
csproj. `Hecton8.Plugins` - which owns the MapMagic terrain bridge, where a non-compiling change silently
breaks world generation - was one of the missing ones, two days newer than the frozen scaffold. Because the
csprojs are gitignored, the omission could never be fixed by committing one. It has to be regenerated, so
this generator is the artifact worth keeping in git.

WHAT MAKES A GATE HONEST, AND WHY THE REFERENCE SET IS THE WHOLE PROBLEM
-----------------------------------------------------------------------
A gate that references MORE than Unity does INVENTS errors. First run against Hecton8.Plugins reported
three CS0234 on `Time.time` in MapMagicRuntimeBridge.cs, purely because the reference wildcard pulled in
Hecton8.Core.Time.dll: the project namespace `Hecton8.Core.Time` then shadows `UnityEngine.Time` for every
file inside a `Hecton8.*` namespace. Same family as `Hecton8.Environment` shadowing `System.Environment`.
Unity compiles that assembly cleanly precisely BECAUSE its asmdef does not reference Hecton8.Core.Time.

A gate that references LESS invents CS0012/CS0246 instead, because Unity hands an assembly a large
implicit reference set (engine modules, auto-referenced packages).

So the rule implemented here: keep vendor, package and Unity DLLs on a broad wildcard, and filter ONLY the
first-party `Hecton8.*` assemblies down to what the asmdef actually declares. That is derived from the
asmdef itself, so it cannot drift from Unity's view by hand-editing.

Deliberately NO <ProjectReference>. Directory.Build.props:13 sets BuildProjectReferences=false and
DisableTransitiveProjectReferences=true for MSBuildProjectName == 'Hecton8.Core' ONLY; a ProjectReference
from any other project would not inherit that and would start slow nested builds of projects tuned for the
opposite. Dependencies bind as prebuilt DLLs from Library/ScriptAssemblies instead, which also means the
gate is only as fresh as Unity's last successful compile of those dependencies - stated here because it is
a real limitation, not a hidden one.

USAGE
-----
    python -B Tools/GenerateAssemblyCompileGateProject.py Hecton8.Plugins
    python -B Tools/GenerateAssemblyCompileGateProject.py Hecton8.Plugins --print-build-command

Then gate it (UnityEditorManagedDir MUST be overridden - the value baked into every csproj here points at
a Unity install that is not on this machine):

    "C:/Program Files/Unity/Hub/Editor/<ver>/Editor/Data/DotNetSdk/dotnet.exe" build <Name>.csproj \
        -p:UnityEditorManagedDir="C:\\Program Files\\Unity\\Hub\\Editor\\<ver>\\Editor\\Data\\Managed" \
        -v:minimal --nologo

Remember the output is localised on this host: success prints `Ошибок: 0`, so grepping for "Error" finds
nothing and looks like success. Grep `error CS`.

VERIFIED: regenerating Hecton8.Plugins with this script and building it produced `Ошибок: 0` with one
pre-existing CS0649 (MapMagicRuntimeBridge.distantTerrainShadowMaskOverride never assigned).
NOT VERIFIED: any other assembly. This has been exercised on exactly one asmdef.
"""

import argparse
import glob
import json
import os
import re
import sys

BS = "\\"
TEMPLATE_PROJECT = "Hecton8.Core.csproj"


def die(msg):
    sys.stderr.write("error: %s\n" % msg)
    raise SystemExit(2)


def find_asmdef(assembly_name):
    """Locate the .asmdef whose declared name matches, not whose filename matches.

    Filename and assembly name usually agree here but nothing enforces it, and reading the declared
    name is the only correct source.
    """
    hits = []
    for path in glob.glob("Assets/**/*.asmdef", recursive=True):
        try:
            with open(path, encoding="utf-8") as fh:
                data = json.load(fh)
        except (OSError, ValueError):
            continue
        if data.get("name") == assembly_name:
            hits.append((path, data))
    if not hits:
        die("no .asmdef under Assets declares name %r" % assembly_name)
    if len(hits) > 1:
        die("%d asmdefs declare name %r: %s" % (len(hits), assembly_name, [h[0] for h in hits]))
    return hits[0]


def nested_asmdef_dirs(root_dir, own_path):
    """Folders under this asmdef that belong to a DIFFERENT assembly.

    An asmdef claims every script beneath it EXCEPT where a nested asmdef intercepts. Missing these
    produces duplicate-type errors that look like real defects.
    """
    out = []
    for path in glob.glob(os.path.join(root_dir, "**", "*.asmdef"), recursive=True):
        if os.path.abspath(path) == os.path.abspath(own_path):
            continue
        out.append(os.path.dirname(path).replace("/", BS))
    return sorted(out)


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("assembly", help="asmdef name, e.g. Hecton8.Plugins")
    ap.add_argument("--print-build-command", action="store_true",
                    help="print the gate command for this assembly and exit")
    args = ap.parse_args()

    if not os.path.isfile(TEMPLATE_PROJECT):
        die("run from the repo root: %s not found" % TEMPLATE_PROJECT)

    name = args.assembly
    if args.print_build_command:
        print('"C:/Program Files/Unity/Hub/Editor/6000.5.0f1/Editor/Data/DotNetSdk/dotnet.exe" build '
              '%s.csproj -p:UnityEditorManagedDir="C:%sProgram Files%sUnity%sHub%sEditor%s6000.5.0f1'
              '%sEditor%sData%sManaged" -v:minimal --nologo' % ((name,) + (BS,) * 9))
        return

    asmdef_path, asmdef = find_asmdef(name)
    root_dir = os.path.dirname(asmdef_path)
    print("asmdef: %s" % asmdef_path)

    for key in ("overrideReferences", "precompiledReferences", "defineConstraints", "versionDefines"):
        val = asmdef.get(key)
        if val:
            print("  NOTE %s = %r - a generated gate does not model this; verify by hand." % (key, val))

    template = open(TEMPLATE_PROJECT, encoding="utf-8").read()

    m = re.search(r'<Compile Include="([^"]+)"', template)
    if not m:
        die("no <Compile Include> in %s to copy the include style from" % TEMPLATE_PROJECT)

    m = re.search(r'Exclude="([^"]*ScriptAssemblies[^"]*)"', template)
    if not m:
        die("no ScriptAssemblies Exclude list in %s" % TEMPLATE_PROJECT)
    inherited = m.group(1).split(";")

    # TWO DISTINCT SETS, and conflating them cost a 470-error build:
    #  declared_all       - every reference the asmdef names, vendor included (Den.Tools, MapMagic,
    #                       Unity.Collections...). These must be REMOVED from the inherited exclude list
    #                       so they resolve as prebuilt DLLs. Leaving Den.Tools and MapMagic excluded
    #                       produced 470 CS0246 on this very assembly.
    #  declared_first_party - the Hecton8.* subset only. Used for the shadow filter, because the
    #                       shadowing hazard is specific to project namespaces.
    declared_all = {r for r in asmdef.get("references", [])}
    declared_all.discard(name)
    declared = {r for r in declared_all if r.startswith("Hecton8.")}
    print("asmdef declares %d references (%d first-party)" % (len(declared_all), len(declared)))

    first_party = [p.replace("/", BS) for p in glob.glob("Library/ScriptAssemblies/Hecton8.*.dll")]
    if not first_party:
        print("  WARNING Library/ScriptAssemblies has no Hecton8.*.dll - Unity has never compiled this "
              "project here, so prebuilt dependencies will not resolve.")

    # Filter first-party only. Vendor/package/Unity DLLs stay on the broad wildcard: narrowing those is
    # what produces false CS0012/CS0246.
    shadow = [p for p in first_party
              if os.path.basename(p)[:-4] not in declared and os.path.basename(p) != name + ".dll"]
    own = "Library" + BS + "ScriptAssemblies" + BS + name + ".dll"

    # Drop EVERY declared dependency out of the inherited exclude list so it resolves as a prebuilt DLL.
    keep = [e for e in inherited if e.rsplit(BS, 1)[-1][:-4] not in declared_all]
    freed = sorted(e.rsplit(BS, 1)[-1] for e in inherited if e.rsplit(BS, 1)[-1][:-4] in declared_all)
    if freed:
        print("un-excluded so they resolve as prebuilt refs: %s" % freed)
    exclude = ";".join(sorted(set(keep + shadow + [own])))
    print("excluding %d of %d first-party dlls not declared by the asmdef" % (len(shadow), len(first_party)))

    include_root = root_dir.replace("/", BS)
    nested = nested_asmdef_dirs(root_dir, asmdef_path)
    removes = "\n".join(
        '    <Compile Remove="%s%s**%s*.cs" />' % (d, BS, BS) for d in nested)
    if nested:
        print("nested asmdefs intercept %d folder(s), excluded from this assembly:" % len(nested))
        for d in nested:
            print("    %s" % d)

    defines = ("TRACE;UNITY_6000;UNITY_6000_4_OR_NEWER;UNITY_6000_4_1;UNITY_STANDALONE;"
               "UNITY_STANDALONE_WIN;ENABLE_UNITY_COLLECTIONS_CHECKS;UNITY_ADDRESSABLES_EXIST;"
               "UNITY_EDITOR;UNITY_EDITOR_WIN")

    out = """<Project Sdk="Microsoft.NET.Sdk">
  <!--
    GENERATED by Tools/GenerateAssemblyCompileGateProject.py from {asmdef}.
    Do not hand-edit: regenerate. This file is gitignored (.gitignore:68 *.csproj), which is why the
    GENERATOR is the tracked artifact and this is not.

    Reference policy: vendor/package/Unity DLLs on a broad wildcard; first-party Hecton8.* filtered to
    exactly what the asmdef declares. Referencing more than Unity does invents errors - pulling in
    Hecton8.Core.Time.dll makes the namespace Hecton8.Core.Time shadow UnityEngine.Time and produces
    three phantom CS0234 in MapMagicRuntimeBridge.cs. Referencing less invents CS0012/CS0246.

    No <ProjectReference> on purpose: Directory.Build.props:13 disables transitive project builds for
    Hecton8.Core only, so a ProjectReference here would start slow nested builds. Dependencies bind as
    prebuilt DLLs, so this gate is only as fresh as Unity's last successful compile of them.

    UNITY_EDITOR is defined, so a plain build is the EDITOR configuration;
    Tools/PlayerConfigCompileGate.py strips those tokens for the player configuration.
    UnityEditorManagedDir must be overridden on the command line.
  -->
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <AssemblyName>{name}</AssemblyName>
    <RootNamespace>{rootns}</RootNamespace>
    <AllowUnsafeBlocks>{unsafe}</AllowUnsafeBlocks>
    <LangVersion>latest</LangVersion>
    <Nullable>disable</Nullable>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <OutputPath>Temp{bs}CodexBuild{bs}{name}{bs}</OutputPath>
    <DefineConstants>{defines}</DefineConstants>
    <UnityEditorManagedDir>C:{bs}Program Files{bs}Unity{bs}Hub{bs}Editor{bs}6000.4.10f1{bs}Editor{bs}Data{bs}Managed</UnityEditorManagedDir>
    <UnityEngineManagedDir>$(UnityEditorManagedDir){bs}UnityEngine</UnityEngineManagedDir>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="{include}{bs}**{bs}*.cs" />
{removes}
  </ItemGroup>

  <ItemGroup>
    <Reference Include="$(UnityEditorManagedDir){bs}*.dll" Private="false" />
    <Reference Include="$(UnityEngineManagedDir){bs}*.dll" Private="false" />
    <Reference Include="Library{bs}ScriptAssemblies{bs}*.dll" Exclude="{exclude}" Private="false" />
    <Reference Include="Library{bs}PackageCache{bs}**{bs}*.dll" Private="false" />
    <Reference Include="Assets{bs}Plugins{bs}**{bs}*.dll" Private="false" />
  </ItemGroup>
</Project>
""".format(asmdef=asmdef_path, name=name, rootns=asmdef.get("rootNamespace") or name,
           unsafe="true" if asmdef.get("allowUnsafeCode") else "false",
           defines=defines, include=include_root, removes=removes, exclude=exclude, bs=BS)

    target = name + ".csproj"
    with open(target, "w", encoding="utf-8", newline="\r\n") as fh:
        fh.write(out)
    print("wrote %s (%d bytes)" % (target, len(out)))
    print("next: python -B Tools/GenerateAssemblyCompileGateProject.py %s --print-build-command" % name)


if __name__ == "__main__":
    main()
