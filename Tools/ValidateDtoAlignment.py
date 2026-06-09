#!/usr/bin/env python3
import os
import re
import sys
from pathlib import Path

# Common types and their (size, alignment)
TYPE_INFO = {
    "byte": (1, 1),
    "sbyte": (1, 1),
    "short": (2, 2),
    "ushort": (2, 2),
    "char": (2, 2),
    "int": (4, 4),
    "uint": (4, 4),
    "float": (4, 4),
    "long": (8, 8),
    "ulong": (8, 8),
    "double": (8, 8),
    # Unity / Unity.Mathematics types
    "Vector2": (8, 4),
    "Vector3": (12, 4),
    "Vector4": (16, 4),
    "float2": (8, 4),
    "float3": (12, 4),
    "float4": (16, 4),
    "int2": (8, 4),
    "int3": (12, 4),
    "int4": (16, 4),
    "uint2": (8, 4),
    "uint3": (12, 4),
    "uint4": (16, 4),
    "double2": (16, 8),
    "double3": (24, 8),
    "double4": (32, 8),
    "float4x4": (64, 4),
    "float3x3": (36, 4),
    "quaternion": (16, 4),
    "Quaternion": (16, 4),
    "Color": (16, 4),
    # Fixed strings
    "FixedString32Bytes": (32, 1),
    "FixedString64Bytes": (64, 1),
    "FixedString128Bytes": (128, 1),
    "FixedString512Bytes": (512, 1),
    # Custom known unmanaged types in Hecton8
    "AbsoluteUniversePosition": (48, 8),
    "AbsoluteUniversePositionBlit128": (48, 8),
    "SymbiosisAup48": (48, 8),
}

# Known managed types that make a struct a managed structure (not an unmanaged DTO)
MANAGED_TYPES = {
    "ComputeShader", "GraphicsBuffer", "RTHandle", "GraphicsFormat", 
    "RenderTexture", "Texture2D", "Texture", "Material", "Shader", 
    "GameObject", "Transform", "Component", "UnityWebRequest", "Bounds",
    "BrgRuntimeHeader", "string", "object", "Action"
}

def parse_cs_file(filepath):
    content = filepath.read_text(encoding='utf-8', errors='ignore')
    structs = []

    lines = content.split('\n')
    brace_depth = 0
    in_struct = False
    current_struct = None
    struct_start_depth = 0
    pending_offset = None

    for i, line in enumerate(lines):
        line_clean = line.split('//')[0].strip()
        if not line_clean:
            continue

        if not in_struct:
            match = re.search(r'struct\s+(\w*(?:DTO|Payload))\b', line_clean)
            if match:
                in_struct = True
                struct_start_depth = brace_depth
                struct_name = match.group(1)
                
                declared_size = None
                for idx in range(max(0, i-5), i):
                    size_match = re.search(r'Size\s*=\s*(\d+)', lines[idx])
                    if size_match:
                        declared_size = int(size_match.group(1))
                        break

                current_struct = {
                    'name': struct_name,
                    'file': str(filepath),
                    'line': i + 1,
                    'fields': [],
                    'declared_size': declared_size
                }
                pending_offset = None

        if in_struct:
            for char in line_clean:
                if char == '{':
                    brace_depth += 1
                elif char == '}':
                    brace_depth -= 1
                    if brace_depth <= struct_start_depth:
                        in_struct = False
                        structs.append(current_struct)
                        current_struct = None
                        break
            
            if not in_struct:
                continue

            # Only parse fields if we are at the top level of the struct body
            if brace_depth == struct_start_depth + 1:
                # Check for FieldOffset attribute
                offset_match = re.search(r'\[FieldOffset\((\d+)\)\]', line_clean)
                if offset_match:
                    pending_offset = int(offset_match.group(1))
                    line_clean = re.sub(r'\[FieldOffset\(\d+\)\]', '', line_clean).strip()
                
                # Check for field declaration
                if ';' in line_clean and 'const' not in line_clean and '(' not in line_clean and '=' not in line_clean:
                    field_match = re.search(r'(?:public|private|internal|protected)?\s*(?:readonly\s+|static\s+)?([\w<>_\[\]]+)\s+(\w+)\s*;', line_clean)
                    if field_match:
                        type_name, field_name = field_match.groups()
                        if type_name not in ('public', 'private', 'internal', 'protected', 'readonly', 'static', 'class', 'struct', 'enum'):
                            current_struct['fields'].append({
                                'name': field_name,
                                'type': type_name,
                                'offset': pending_offset,
                                'line': i + 1
                            })
                            pending_offset = None
        else:
            brace_depth += line_clean.count('{') - line_clean.count('}')

    return structs

def is_managed_struct(struct):
    for field in struct['fields']:
        ftype = field['type']
        if ftype in MANAGED_TYPES or "NativeArray" in ftype or "List<" in ftype or "[]" in ftype:
            return True
    return False

def validate_struct(struct, resolved_types=None):
    if resolved_types is None:
        resolved_types = TYPE_INFO
    errors = []
    current_offset = 0
    max_end = 0

    # Skip managed RenderGraph, save schema, or BRG payload structs
    if is_managed_struct(struct):
        return []

    for field in struct['fields']:
        if field['type'] == 'bool':
            errors.append(f"Line {field['line']}: Field '{field['name']}' uses raw 'bool'. Must use byte/int flags.")
        elif field['type'] not in resolved_types:
            errors.append(f"Line {field['line']}: Field '{field['name']}' uses managed or unknown type '{field['type']}'.")
        else:
            size, align = resolved_types[field['type']]
            offset = field['offset']

            if offset is None:
                errors.append(f"Line {field['line']}: Field '{field['name']}' lacks explicit FieldOffset.")
            else:
                if offset % align != 0:
                    errors.append(f"Line {field['line']}: Field '{field['name']}' offset {offset} is not aligned to {align} bytes.")
                if offset != current_offset and offset > current_offset:
                    if "pad" not in field['name'].lower():
                        errors.append(f"Line {field['line']}: Field '{field['name']}' at offset {offset} has implicit padding before it. Use explicit padding fields.")
                current_offset = offset + size
                max_end = max(max_end, current_offset)

    total_size = struct['declared_size'] if struct['declared_size'] else max_end
    if total_size % 8 != 0:
        errors.append(f"Struct size ({total_size}) is not a multiple of 8 bytes.")

    return errors

def main():
    assets_dir = Path("Assets/_Project/Scripts")
    if not assets_dir.exists():
        assets_dir = Path("Assets/_Project")
    if not assets_dir.exists():
        assets_dir = Path(".")

    cs_files = list(assets_dir.rglob("*.cs"))
    all_structs = []

    for f in cs_files:
        # Exclude Editor scripts
        if "Editor" in f.parts:
            continue
        all_structs.extend(parse_cs_file(f))

    struct_dict = {s['name']: s for s in all_structs}
    resolved_types = TYPE_INFO.copy()
    
    changed = True
    while changed:
        changed = False
        for sname, s in struct_dict.items():
            if sname in resolved_types:
                continue
            if s['declared_size'] is not None:
                resolved_types[sname] = (s['declared_size'], 8)
                changed = True
                continue
            
            can_resolve = True
            max_end = 0
            max_align = 1
            
            for field in s['fields']:
                ftype = field['type']
                if ftype in resolved_types:
                    fsize, falign = resolved_types[ftype]
                else:
                    can_resolve = False
                    break
                
                offset = field['offset']
                if offset is not None:
                    max_end = max(max_end, offset + fsize)
                max_align = max(max_align, falign)
            
            if can_resolve and s['fields']:
                size = max_end
                if size % 8 != 0:
                    size = ((size + 7) // 8) * 8
                resolved_types[sname] = (size, max_align)
                changed = True

    all_errors = []
    for s in all_structs:
        errors = validate_struct(s, resolved_types)
        if errors:
            for e in errors:
                all_errors.append(f"{s['file']} ({s['name']}): {e}")

    if all_errors:
        for e in all_errors:
            print(e)
        sys.exit(1)
    else:
        print("All DTOs are ARM64 compliant.")
        sys.exit(0)

if __name__ == '__main__':
    main()
