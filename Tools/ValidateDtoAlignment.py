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
    "Vector2": (8, 4),
    "Vector3": (12, 4),
    "Vector4": (16, 4),
    "float2": (8, 4),
    "float3": (12, 4),
    "float4": (16, 4),
    "int2": (8, 4),
    "int3": (12, 4),
    "int4": (16, 4),
    "quaternion": (16, 4),
    "Quaternion": (16, 4),
    "Color": (16, 4)
}

def parse_cs_file(filepath):
    content = filepath.read_text(encoding='utf-8', errors='ignore')
    # simplified parsing for structs ending in DTO or Payload
    structs = []

    lines = content.split('\n')
    in_struct = False
    current_struct = None
    brace_depth = 0

    for i, line in enumerate(lines):
        line = line.split('//')[0].strip()

        # very basic brace counting
        if '{' in line:
            brace_depth += line.count('{')
        if '}' in line:
            brace_depth -= line.count('}')

        if not in_struct:
            match = re.search(r'struct\s+(\w+(?:DTO|Payload))', line)
            if match:
                in_struct = True
                struct_name = match.group(1)
                current_struct = {
                    'name': struct_name,
                    'file': str(filepath),
                    'line': i + 1,
                    'fields': [],
                    'declared_size': None
                }

                # Check for explicit size
                # [StructLayout(LayoutKind.Explicit, Size = 16)]
                size_match = re.search(r'Size\s*=\s*(\d+)', content[:content.find(line)])
                if size_match:
                    current_struct['declared_size'] = int(size_match.group(1))

        elif in_struct:
            if brace_depth == 0 and '}' in line:
                in_struct = False
                structs.append(current_struct)
                current_struct = None
                continue

            # Look for fields
            # [FieldOffset(0)] public int Id;
            field_match = re.search(r'(?:\[FieldOffset\((\d+)\)\])?.*?(?:public|private|internal)\s+(\w+)\s+(\w+)\s*;', line)
            if field_match:
                offset_str, type_name, field_name = field_match.groups()
                offset = int(offset_str) if offset_str else None
                current_struct['fields'].append({
                    'name': field_name,
                    'type': type_name,
                    'offset': offset,
                    'line': i + 1
                })

    return structs

def validate_struct(struct):
    errors = []
    current_offset = 0
    max_end = 0

    for field in struct['fields']:
        if field['type'] == 'bool':
            errors.append(f"Line {field['line']}: Field '{field['name']}' uses raw 'bool'. Must use byte/int flags.")
        elif field['type'] not in TYPE_INFO:
            errors.append(f"Line {field['line']}: Field '{field['name']}' uses managed or unknown type '{field['type']}'.")
        else:
            size, align = TYPE_INFO[field['type']]
            offset = field['offset']

            if offset is None:
                errors.append(f"Line {field['line']}: Field '{field['name']}' lacks explicit FieldOffset.")
            else:
                if offset % align != 0:
                    errors.append(f"Line {field['line']}: Field '{field['name']}' offset {offset} is not aligned to {align} bytes.")
                if offset != current_offset and offset > current_offset:
                    # check if missing explicit padding
                    if "pad" not in field['name'].lower():
                        errors.append(f"Line {field['line']}: Field '{field['name']}' at offset {offset} has implicit padding before it. Use explicit padding fields.")
                current_offset = offset + size
                max_end = max(max_end, current_offset)

    total_size = struct['declared_size'] if struct['declared_size'] else max_end
    if total_size % 8 != 0:
        errors.append(f"Struct size ({total_size}) is not a multiple of 8 bytes.")

    return errors

def main():
    assets_dir = Path("Assets/_Project")
    if not assets_dir.exists():
        assets_dir = Path(".")

    cs_files = list(assets_dir.rglob("*.cs"))
    all_errors = []

    for f in cs_files:
        structs = parse_cs_file(f)
        for s in structs:
            errors = validate_struct(s)
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
