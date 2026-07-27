import math
import struct
import ctypes

def float32(val):
    return ctypes.c_float(val).value

class Float3:
    def __init__(self, x, y, z):
        self.x = float32(x)
        self.y = float32(y)
        self.z = float32(z)
    def __repr__(self):
        return f"Float3({self.x}, {self.y}, {self.z})"

# Task 1: Bounding Box math test
class WorldChunkPhysicsBakedSignal:
    def __init__(self, terrain_pos: Float3, terrain_size: Float3):
        self.TerrainPosition = terrain_pos
        self.TerrainSize = terrain_size

    @staticmethod
    def contains_world_xz(signal: 'WorldChunkPhysicsBakedSignal', world_x: float, world_z: float) -> bool:
        wx = float32(world_x)
        wz = float32(world_z)
        min_x = signal.TerrainPosition.x
        min_z = signal.TerrainPosition.z
        max_x = float32(min_x + signal.TerrainSize.x)
        max_z = float32(min_z + signal.TerrainSize.z)
        return (wx >= min_x) and (wz >= min_z) and (wx <= max_x) and (wz <= max_z)

def test_contains_world_xz():
    print("=== TASK 1: Testing WorldChunkPhysicsBakedSignal.ContainsWorldXZ ===")
    results = []
    
    # Setup chunk pos and size
    pos = Float3(1000.0, 50.0, -2000.0)
    size_val = 100.0
    size = Float3(size_val, size_val, size_val)
    min_corner = Float3(pos.x - size_val * 0.5, pos.y, pos.z - size_val * 0.5)
    
    signal = WorldChunkPhysicsBakedSignal(min_corner, size)
    
    min_x = min_corner.x
    max_x = float32(min_corner.x + size.x)
    min_z = min_corner.z
    max_z = float32(min_corner.z + size.z)
    
    print(f"Chunk Center pos: ({pos.x}, {pos.z}), size: {size.x}")
    print(f"Calculated minCorner: ({min_x}, {min_z}), maxCorner: ({max_x}, {max_z})")
    
    test_cases = [
        ("Center", pos.x, pos.z, True),
        ("Bottom-Left Corner (minX, minZ)", min_x, min_z, True),
        ("Top-Right Corner (maxX, maxZ)", max_x, max_z, True),
        ("Top-Left Corner (minX, maxZ)", min_x, max_z, True),
        ("Bottom-Right Corner (maxX, minZ)", max_x, min_z, True),
        ("Inside Point 1", pos.x + 10.0, pos.z - 15.0, True),
        ("Inside Point 2", min_x + 0.001, min_z + 0.001, True),
        ("Inside Point 3", max_x - 0.001, max_z - 0.001, True),
        ("Outside Left (minX - 0.01)", min_x - 0.01, pos.z, False),
        ("Outside Right (maxX + 0.01)", max_x + 0.01, pos.z, False),
        ("Outside Bottom (minZ - 0.01)", pos.x, min_z - 0.01, False),
        ("Outside Top (maxZ + 0.01)", pos.x, max_z + 0.01, False),
        ("Outside Far", pos.x + 500.0, pos.z + 500.0, False),
    ]
    
    all_passed = True
    for name, wx, wz, expected in test_cases:
        res = WorldChunkPhysicsBakedSignal.contains_world_xz(signal, wx, wz)
        passed = (res == expected)
        if not passed:
            all_passed = False
        status = "PASS" if passed else "FAIL"
        print(f"  [{status}] Case '{name}': pos=({wx}, {wz}) -> Got {res}, Expected {expected}")
        results.append((name, wx, wz, res, expected, passed))
        
    return all_passed, results


# Task 2: VoxelSurfaceNetsJobs.PackColorFromNormal & VoxelSurfaceColorEncoding.ResolveFloorWeight
class VoxelSurfaceColorEncoding:
    FloorTransitionMin = float32(0.375)
    FloorTransitionRange = float32(0.45)

    @staticmethod
    def resolve_floor_weight(normal: Float3) -> float:
        is_finite = math.isfinite(normal.x) and math.isfinite(normal.y) and math.isfinite(normal.z)
        len_sq = float32(normal.x * normal.x + normal.y * normal.y + normal.z * normal.z)
        
        if is_finite and len_sq > 1e-6:
            inv_len = float32(1.0 / math.sqrt(len_sq))
            safe_normal = Float3(normal.x * inv_len, normal.y * inv_len, normal.z * inv_len)
        else:
            safe_normal = Float3(0.0, 1.0, 0.0)
            
        t = float32((safe_normal.y - VoxelSurfaceColorEncoding.FloorTransitionMin) * (1.0 / VoxelSurfaceColorEncoding.FloorTransitionRange))
        t_sat = float32(max(0.0, min(1.0, t)))
        return float32(t_sat * t_sat * (3.0 - 2.0 * t_sat))

class VoxelSurfaceNetsJobs:
    @staticmethod
    def pack_color_from_normal(normal: Float3, ao: float) -> int:
        is_finite = math.isfinite(normal.x) and math.isfinite(normal.y) and math.isfinite(normal.z)
        len_sq = float32(normal.x * normal.x + normal.y * normal.y + normal.z * normal.z)
        
        if is_finite and len_sq > 1e-6:
            inv_len = float32(1.0 / math.sqrt(len_sq))
            safe_normal = Float3(normal.x * inv_len, normal.y * inv_len, normal.z * inv_len)
        else:
            safe_normal = Float3(0.0, 1.0, 0.0)
            
        floor_weight = VoxelSurfaceColorEncoding.resolve_floor_weight(safe_normal)
        
        floor_byte = int(round(floor_weight * 255.0))
        floor_byte = max(0, min(255, floor_byte))
        
        wall_byte = 255 - floor_byte
        blue_byte = 0
        
        ao_sat = max(0.0, min(1.0, float32(ao)))
        ao_byte = int(round(ao_sat * 255.0))
        ao_byte = max(0, min(255, ao_byte))
        
        return floor_byte | (wall_byte << 8) | (blue_byte << 16) | (ao_byte << 24)

def test_pack_color_from_normal():
    print("\n=== TASK 2: Testing VoxelSurfaceNetsJobs.PackColorFromNormal & ResolveFloorWeight ===")
    
    test_cases = [
        ("(0, 1, 0) [Up/Floor]", Float3(0.0, 1.0, 0.0), 1.0, 255, 0),
        ("(0, -1, 0) [Down/Ceiling]", Float3(0.0, -1.0, 0.0), 1.0, 0, 255),
        ("(0, 0, 0) [Zero vector]", Float3(0.0, 0.0, 0.0), 1.0, 255, 0),
        ("(0, 10, 0) [Large Up vector]", Float3(0.0, 10.0, 0.0), 1.0, 255, 0),
        ("(NaN, 0, 0) [NaN vector]", Float3(float('nan'), 0.0, 0.0), 1.0, 255, 0),
        ("(0, Inf, 0) [Inf vector]", Float3(0.0, float('inf'), 0.0), 1.0, 255, 0),
        ("(1, 0, 0) [Pure Wall]", Float3(1.0, 0.0, 0.0), 1.0, 0, 255),
        ("(0.5, 0.6, 0.0) [Transition Slope]", Float3(0.5, 0.6, 0.0), 1.0, None, None)
    ]
    
    all_passed = True
    results = []
    
    for name, normal, ao, exp_floor, exp_wall in test_cases:
        weight = VoxelSurfaceColorEncoding.resolve_floor_weight(normal)
        packed = VoxelSurfaceNetsJobs.pack_color_from_normal(normal, ao)
        
        floor_byte = packed & 0xFF
        wall_byte = (packed >> 8) & 0xFF
        blue_byte = (packed >> 16) & 0xFF
        ao_byte = (packed >> 24) & 0xFF
        
        if exp_floor is not None:
            passed = (floor_byte == exp_floor) and (wall_byte == exp_wall)
        else:
            passed = True # Just inspection for continuous transition
            
        if not passed:
            all_passed = False
            
        status = "PASS" if passed else "FAIL"
        print(f"  [{status}] Case '{name}': weight={weight:.4f}, packed=0x{packed:08X} (R/floor={floor_byte}, G/wall={wall_byte}, B={blue_byte}, A/ao={ao_byte})")
        results.append((name, weight, packed, floor_byte, wall_byte, passed))
        
    return all_passed, results

if __name__ == '__main__':
    t1_pass, t1_res = test_contains_world_xz()
    t2_pass, t2_res = test_pack_color_from_normal()
    
    print("\n=== SUMMARY ===")
    print(f"Task 1 (ContainsWorldXZ): {'PASS' if t1_pass else 'FAIL'}")
    print(f"Task 2 (PackColorFromNormal): {'PASS' if t2_pass else 'FAIL'}")
