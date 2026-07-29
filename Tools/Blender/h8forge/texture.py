"""Generated PBR texture families: the map stack the forge could not previously write.

WHY THIS MODULE EXISTS, and it is not a convenience.

Before this file, ``Tools/Blender/h8forge/`` contained law, mesh_ops, export_unity,
vertexcolor, validate, preview and blackbox -- and nothing that wrote a ``TX_*`` map.
``law.py`` declared the name pattern (``NAME_TEXTURE``), ``export_unity.py`` reported
``textures`` as a manifest GAP citing "Missing texture is fatal", and ``vertexcolor.py``
baked occlusion into VERTEX COLOURS, which is a different artefact. So every forge
package shipped with an open textures gap and there was no code path that could ever
close it. ``3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`` section 10 ends:

    "Meshes without this material route may exist as diagnostic geometry only. They are
     not final HECTON-8 art."

By the project's own law, every forge asset was diagnostic geometry.

WHY GEOLOGY IS NOT DECORATION HERE. ``generators/rock.py`` measures the finest wavelength
its sculpt lattice can represent and records it per asset: 0.087 m on a boulder, 0.205 m
on an outcrop, 0.406 m on a cliffchunk. The geology bible's 0.075 m grain witness is
below all three, so it is UNREACHABLE as geometry at every size class inside budget.
``3DMODEL_GEOLOGY_ROCKS.md`` section 2 routes exactly that band to "baked normal/depth
support", and the geology manifests carry the ruling at
``extra.grainBand.subLatticeGrainRoute``. This map stack is therefore not a polish pass
on finished geometry -- it is the ONLY route by which rock surface detail below 0.087 m
can exist in this project at all.

Authority implemented here, read in full before editing:
  - ``3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md``  sections 0-10. The specification.
  - ``3DMODEL_TEXTURES_MATERIALS.md``           roles, packing, import, atlas, triplanar.
  - ``3DMODEL_GEOLOGY_ROCKS.md``                sections 3, 4, 5 for the family recipe.
  - ``law.py``                                  every threshold. No local copies.

THE RULE THAT SHAPES THE WHOLE FILE is playbook section 5:

    "These fields must be mixed by material semantics, not blind multiplication. Edge
     wear belongs on convex curvature. Grime belongs in concave cavities and downward
     streaks. Emission belongs to organs, instruments, vents, or energized seams.
     Roughness must follow material state, not random color."

So nothing here multiplies a noise field into a channel. Every channel is a stated
function of a MEASURED field:

  * convex curvature      -> mean curvature of the height field, sign-separated
  * concave curvature     -> the same operator, other sign
  * occlusion             -> horizon-angle integral ray-marched over the height field
  * downward accumulation -> gravity-directed transport scan over the height field
  * lithology             -> the lamina stack, an actual sedimentary column model

Noise appears in exactly one place: as the band-limited fBm that BUILDS the height field.
Everything downstream is measured off that surface, which is why the channels come out
decorrelated (playbook section 9's channel-independence gate) instead of being one map
stored three times.

WHY THE PNG CODEC IS HAND-WRITTEN. Blender's image save path applies colour management,
and an 8-bit PNG written through a view transform holds DISPLAY-ENCODED values -- correct
for base colour, catastrophic for a normal, mask or height map where the stored number IS
the data. ``preview.py`` already documents the read side of that trap
(``_srgb_to_linear``). Rather than fight the transform per map, this module writes PNG
bytes directly with ``zlib``: the file then contains exactly the array that was computed,
and the round trip is verifiable. PIL is not available in this Blender.

WHAT THIS MODULE DOES NOT DO. It does not import into Unity, does not create ``MAT_*``
assets, and does not bind anything to a prefab. Those are Unity-side operations behind a
single editor lock. It DECLARES the import settings and the binding contract in the
manifest and stops there, which is the honest boundary.
"""

from __future__ import annotations

import json
import math
import os
import struct
import sys
import zlib
from dataclasses import dataclass, field
from typing import Optional

import numpy as np

# Dual import: this module is a package member, but ``blender -b -P`` runs a file as a
# script with no package context, so the relative import fails there. The path is derived
# from ``__file__``, never typed, per AGENTS.md's ban on hardcoded absolute paths.
try:
    from . import law
except ImportError:  # pragma: no cover - exercised only under `blender -P`
    sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    from h8forge import law


MODULE_NAME = "texture.py"
MODULE_VERSION = "1.0.0"


# ===========================================================================
# PNG codec  --  exact bytes, no colour management
# ===========================================================================

_PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"
_COLOR_TYPE_GRAY = 0
_COLOR_TYPE_RGB = 2
_COLOR_TYPE_RGBA = 6
_FILTER_UP = 2


def _png_chunk(tag: bytes, payload: bytes) -> bytes:
    return (struct.pack(">I", len(payload)) + tag + payload
            + struct.pack(">I", zlib.crc32(tag + payload) & 0xFFFFFFFF))


def write_png(path: str, array: np.ndarray, *, bit_depth: int = 8) -> str:
    """Write ``array`` as a PNG with no colour transform applied.

    ``array`` is ``(h, w)`` for greyscale or ``(h, w, 3|4)`` for RGB/RGBA, integer dtype.
    Row 0 is the TOP row, which is PNG's own order and the opposite of Blender's
    bottom-up pixel buffer -- mixing those up flips every normal map's green channel.

    The Up filter is a byte-wise delta against the previous scanline, valid at both bit
    depths, and on smooth height/normal data it roughly halves the file.
    """
    if array.ndim == 2:
        color_type, planar = _COLOR_TYPE_GRAY, array[:, :, None]
    elif array.ndim == 3 and array.shape[2] == 3:
        color_type, planar = _COLOR_TYPE_RGB, array
    elif array.ndim == 3 and array.shape[2] == 4:
        color_type, planar = _COLOR_TYPE_RGBA, array
    else:
        raise ValueError("unsupported PNG array shape: " + str(array.shape))

    if bit_depth == 8:
        raw = np.ascontiguousarray(planar.astype(np.uint8))
    elif bit_depth == 16:
        raw = np.ascontiguousarray(planar.astype(">u2"))  # PNG 16-bit is big-endian
    else:
        raise ValueError("bit depth must be 8 or 16, got " + str(bit_depth))

    height, width = int(planar.shape[0]), int(planar.shape[1])
    rows = raw.reshape(height, -1).view(np.uint8).reshape(height, -1)
    stride = rows.shape[1]

    filtered = np.empty((height, stride + 1), dtype=np.uint8)
    filtered[:, 0] = _FILTER_UP
    filtered[0, 1:] = rows[0]
    if height > 1:
        filtered[1:, 1:] = (rows[1:].astype(np.int16)
                            - rows[:-1].astype(np.int16)).astype(np.uint8)

    ihdr = struct.pack(">IIBBBBB", width, height, bit_depth, color_type, 0, 0, 0)
    body = zlib.compress(filtered.tobytes(), 6)

    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    with open(path, "wb") as handle:
        handle.write(_PNG_SIGNATURE)
        handle.write(_png_chunk(b"IHDR", ihdr))
        handle.write(_png_chunk(b"IDAT", body))
        handle.write(_png_chunk(b"IEND", b""))
    return path


def read_png(path: str) -> np.ndarray:
    """Decode a PNG written by :func:`write_png` back into its array.

    Exists so a claim about a map can be checked against the FILE rather than against the
    in-memory array that produced it. ``AGENTS.md`` ``[RULE] Never Trust Automated
    Assertions Alone``: the existence of a PNG proves nothing, and an encoder bug that
    silently swaps or truncates a channel is invisible without a round trip.

    Handles filter types 0 and 2, which is what the writer emits.
    """
    with open(path, "rb") as handle:
        blob = handle.read()
    if blob[:8] != _PNG_SIGNATURE:
        raise ValueError("not a PNG: " + path)

    offset = 8
    width = height = bit_depth = color_type = 0
    idat = []
    while offset < len(blob):
        length = struct.unpack(">I", blob[offset:offset + 4])[0]
        tag = blob[offset + 4:offset + 8]
        payload = blob[offset + 8:offset + 8 + length]
        offset += 12 + length
        if tag == b"IHDR":
            width, height, bit_depth, color_type = struct.unpack(">IIBB", payload[:10])
        elif tag == b"IDAT":
            idat.append(payload)
        elif tag == b"IEND":
            break

    channels = {_COLOR_TYPE_GRAY: 1, _COLOR_TYPE_RGB: 3, _COLOR_TYPE_RGBA: 4}[color_type]
    stride = width * channels * (bit_depth // 8)
    data = np.frombuffer(zlib.decompress(b"".join(idat)), dtype=np.uint8)
    data = data.reshape(height, stride + 1)
    filters = data[:, 0]
    rows = data[:, 1:]

    if np.all(filters == _FILTER_UP):
        # Undo Up as one cumulative sum modulo 256 rather than a Python row loop.
        rows = (np.cumsum(rows.astype(np.int64), axis=0) & 0xFF).astype(np.uint8)
    elif np.all(filters == 0):
        rows = rows.astype(np.uint8)
    else:
        raise ValueError("unsupported PNG filter set: " + str(np.unique(filters)))

    if bit_depth == 16:
        out = np.ascontiguousarray(rows).view(">u2").reshape(height, width, channels)
    else:
        out = rows.reshape(height, width, channels)
    return out[:, :, 0] if channels == 1 else out


# ===========================================================================
# Encoding helpers
# ===========================================================================

def quantise(values: np.ndarray, bit_depth: int = 8) -> np.ndarray:
    """Clamp to 0..1 and quantise, ROUNDING rather than truncating.

    Truncation biases every channel low by half a code value. That is invisible per pixel
    and shifts a measured channel mean by 1/512, which is enough to move a gate result --
    so the bias would show up as an unexplained gate number, not as a visible defect.
    """
    maximum = (1 << bit_depth) - 1
    dtype = np.uint8 if bit_depth == 8 else np.uint16
    return np.rint(np.clip(values, 0.0, 1.0) * maximum).astype(dtype)


def linear_to_srgb(values: np.ndarray) -> np.ndarray:
    """Encode linear values for an sRGB texture slot.

    Base colour ships with ``sRGB: True`` (``law.TEXTURE_IMPORT_SETTINGS``), so the FILE
    must hold display-encoded values for Unity to decode them back to the linear albedo
    that was authored. Writing linear numbers into an sRGB slot produces a washed-out
    material with no error anywhere -- only a wrong-looking rock.
    """
    values = np.clip(np.asarray(values, dtype=np.float64), 0.0, 1.0)
    low = values <= 0.0031308
    out = np.where(low, values * 12.92,
                   1.055 * np.power(np.where(low, 1.0, values), 1.0 / 2.4) - 0.055)
    return np.clip(out, 0.0, 1.0)


def srgb_to_linear(values: np.ndarray) -> np.ndarray:
    """Inverse of :func:`linear_to_srgb`, for measuring a written base-colour map."""
    values = np.clip(np.asarray(values, dtype=np.float64), 0.0, 1.0)
    low = values <= 0.04045
    return np.where(low, values / 12.92,
                    np.power((np.where(low, 1.0, values) + 0.055) / 1.055, 2.4))


# ===========================================================================
# Periodic field generators  --  playbook section 5's approved list
# ===========================================================================
# "fBm noise for fine grain, sediment, paint breakup, and tissue pores."
# "Worley/Voronoi fields for corrosion pits, coral cells, cracked mud, ore pockets."
#
# BOTH ARE BUILT EXACTLY PERIODIC, and that is a structural decision rather than a
# tuning one. Playbook section 9's first gate is a "2x2 tile seam check for tileable
# sources", and there are two ways to pass it: generate an arbitrary field and then
# blend/mirror the edges until the seam stops showing, or generate a field that has no
# seam because it is defined on a torus. The first approach leaves a low-frequency
# blend halo that reads as a soft band across every rock -- a "blurred low-frequency
# gradient that fights scene lighting", rejected by section 1 in those words. The
# second cannot produce a seam at all.
#
# Section 4 also requires geology textures to be SCALE-CALIBRATED, which means the
# generator must be able to place energy at a stated wavelength in metres. Spectral
# synthesis does that directly: the wavelength band is an argument, not an emergent
# property of an octave stack.


def _frequency_grid(resolution: int) -> tuple:
    """Cycles-per-tile frequency coordinates for an ``rfft2`` spectrum.

    Returns ``(fu, fv, radius)`` where radius is in cycles per tile, so a feature of
    wavelength ``lam`` metres on a ``tile_m`` tile sits at radius ``tile_m / lam``.
    """
    fv = np.fft.fftfreq(resolution)[:, None] * resolution
    fu = np.fft.rfftfreq(resolution)[None, :] * resolution
    return fu, fv, np.sqrt(fu * fu + fv * fv)


def periodic_fbm(
    rng: np.random.Generator,
    resolution: int,
    tile_m: float,
    *,
    coarsest_m: float,
    finest_m: float,
    beta: float = 2.15,
    anisotropy: float = 1.0,
    anisotropy_axis: str = "v",
) -> np.ndarray:
    """Band-limited fractional Brownian motion on a torus, zero-mean, unit std.

    A power-law spectral density ``S(k) ~ k^-beta`` with random phases IS fBm by
    definition -- this is the spectral construction of it, not an approximation of an
    octave stack. Doing it in the frequency domain buys three properties an octave stack
    cannot give at the same time:

      * exact periodicity, so the seam gate passes structurally;
      * a HARD band limit, so no energy exists below the tile's Nyquist wavelength to
        alias into the mip chain, and none exists above ``coarsest_m`` to compete with
        mesh-scale relief the sculpt already carries;
      * anisotropy as a plain ellipse in the frequency plane.

    ``anisotropy`` above 1 stretches features ALONG bedding, which is what makes a
    sedimentary surface read as deposited rather than as isotropic fuzz.
    ``generators/rock.py`` uses 3.4 for sedimentary and 2.1 for basalt on the mesh-side
    grain field; the same numbers apply here for the same reason.

    ``anisotropy_axis`` names the BEDDING NORMAL. Features elongate perpendicular to it.

    SIGN TRAP, found by measuring rather than by reading. Elongating features along U
    means admitting only LONG wavelengths in U, which means only LOW ``fu``, which means
    ``fu`` must be MULTIPLIED by the anisotropy so high ``fu`` is pushed out of the band.
    Dividing -- the intuitive spelling, and the first one written here -- does the exact
    opposite: measured gradient along U was 1.08 against 0.68 along V, i.e. the grain was
    elongated ACROSS the bedding, which reads as a vertically combed surface on a
    sedimentary rock. The number is what caught it; the code looked right.
    """
    fu, fv, radius = _frequency_grid(resolution)

    if anisotropy != 1.0:
        if anisotropy_axis == "v":
            scaled = np.sqrt((fu * anisotropy) ** 2 + fv ** 2)
        else:
            scaled = np.sqrt(fu ** 2 + (fv * anisotropy) ** 2)
    else:
        scaled = radius

    low_cycles = tile_m / max(coarsest_m, 1e-9)   # coarsest feature -> lowest frequency
    high_cycles = tile_m / max(finest_m, 1e-9)    # finest feature  -> highest frequency

    amplitude = np.zeros_like(scaled)
    inside = (scaled >= low_cycles) & (scaled <= high_cycles) & (radius > 0.0)
    amplitude[inside] = np.power(scaled[inside], -beta * 0.5)

    # A raised-cosine roll-off over the outer 25 percent of the band. A brick-wall cutoff
    # in the frequency domain is a sinc in the spatial domain, which is ringing -- and
    # "ringing" is named in section 9's mip gate as a rejection.
    taper_start = low_cycles + 0.75 * (high_cycles - low_cycles)
    taper = (scaled > taper_start) & inside
    if np.any(taper):
        span = max(high_cycles - taper_start, 1e-9)
        t = (scaled[taper] - taper_start) / span
        amplitude[taper] *= 0.5 * (1.0 + np.cos(np.pi * np.clip(t, 0.0, 1.0)))

    phase = rng.uniform(0.0, 2.0 * np.pi, size=amplitude.shape)
    spectrum = amplitude * (np.cos(phase) + 1j * np.sin(phase))
    spectrum[0, 0] = 0.0  # zero mean; DC is added back by the caller with real meaning

    out = np.fft.irfft2(spectrum, s=(resolution, resolution))
    std = float(out.std())
    return out / std if std > 1e-12 else out


def periodic_worley(
    rng: np.random.Generator,
    resolution: int,
    cells: int,
    *,
    jitter: float = 0.85,
) -> tuple:
    """Toroidal Worley/Voronoi. Returns ``(f1, f2, cell_id)`` with F1/F2 in cell units.

    Jittered-grid construction with a wrapping 3x3 neighbourhood search, which is what
    makes it periodic: cell ``(0, 0)``'s neighbours include cell ``(cells-1, cells-1)``.
    Brute-forcing every seed against every pixel would also work and is 200x slower at
    2048 with no difference in the result.

    ``f1`` drives pit and vug placement (playbook section 5: "Worley/Voronoi fields for
    corrosion pits ... ore pockets"). ``f2 - f1`` is the standard cell-border distance
    and drives joint/crack masks -- section 5 sanctions signed distance fields for
    exactly that kind of mask.
    """
    cells = max(1, int(cells))
    seeds = (np.stack(np.meshgrid(np.arange(cells), np.arange(cells), indexing="ij"),
                      axis=-1).astype(np.float64)
             + 0.5 + jitter * (rng.random((cells, cells, 2)) - 0.5))
    seeds = np.mod(seeds, cells)

    coord = (np.arange(resolution) + 0.5) / resolution * cells
    py, px = np.meshgrid(coord, coord, indexing="ij")
    cy = np.clip((py).astype(np.int64), 0, cells - 1)
    cx = np.clip((px).astype(np.int64), 0, cells - 1)

    f1 = np.full((resolution, resolution), np.inf)
    f2 = np.full((resolution, resolution), np.inf)
    cell_id = np.zeros((resolution, resolution), dtype=np.int64)

    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            ny = np.mod(cy + dy, cells)
            nx = np.mod(cx + dx, cells)
            sy = seeds[ny, nx, 0]
            sx = seeds[ny, nx, 1]
            # Wrapped offsets: the seed of a neighbour cell across the tile boundary is
            # reached the short way round, which is what removes the seam.
            oy = py - (sy + dy * 0.0)
            ox = px - (sx + dx * 0.0)
            oy = oy - cells * np.rint(oy / cells)
            ox = ox - cells * np.rint(ox / cells)
            dist = np.sqrt(oy * oy + ox * ox)
            closer = dist < f1
            f2 = np.where(closer, f1, np.minimum(f2, dist))
            cell_id = np.where(closer, ny * cells + nx, cell_id)
            f1 = np.where(closer, dist, f1)

    return f1, f2, cell_id


def periodic_joint_traces(
    rng: np.random.Generator,
    resolution: int,
    tile_m: float,
    *,
    count: int,
    stress_azimuth_deg: float,
    conjugate_deg: float,
    jitter_deg: float,
    length_min_fraction: float,
    length_max_fraction: float,
    width_m: float,
    waviness_m: float,
) -> tuple:
    """Sparse conjugate joint set as a distance field. Returns ``(mask, traces)``.

    WHY THIS EXISTS INSTEAD OF A WORLEY BORDER FIELD. A Voronoi diagram is a PARTITION of
    the plane, so every cell has a complete boundary and any border-distance field is
    necessarily a closed network. The first version of this family used one, and the tile
    read as crazed ceramic glaze -- a full mesh of thin bright lines enclosing polygonal
    cells. That cannot be fixed by narrowing or fading the lines, because a fainter
    tessellation is still a tessellation; the topology is the defect.

    Real joint sets are DISCRETE, FINITE-LENGTH traces whose orientations cluster about the
    principal stress direction, usually as a conjugate pair straddling it. They terminate in
    the rock, they do not enclose regions, and there are only a handful of them per square
    metre. That is what this generates.

    Periodicity comes from minimum-image addressing on the vector from each segment's start
    to the sample point. That is exact wherever it matters: it can pick the wrong image only
    for points far from the segment, and those are far from every image, so their distance is
    large and the mask is zero there regardless. Segment length is capped below half the tile
    so the near field is never ambiguous.
    """
    axis = (np.arange(resolution) + 0.5) / resolution * tile_m
    py, px = np.meshgrid(axis, axis, indexing="ij")

    # A little waviness so a trace is a fracture rather than a ruled line. Applied to the
    # SAMPLE coordinate, which bends every trace coherently as if the rock itself deformed.
    if waviness_m > 0.0:
        px = px + periodic_warp(rng, resolution, tile_m, tile_m * 0.22, waviness_m)
        py = py + periodic_warp(rng, resolution, tile_m, tile_m * 0.19, waviness_m)

    max_length = min(length_max_fraction, 0.45) * tile_m
    min_length = max(1e-3, length_min_fraction * tile_m)

    best = np.full((resolution, resolution), np.inf)
    traces = []
    for index in range(max(1, count)):
        # Conjugate pair: alternate sides of the stress axis.
        side = 1.0 if (index % 2 == 0) else -1.0
        azimuth = math.radians(stress_azimuth_deg + side * conjugate_deg
                               + rng.normal(0.0, jitter_deg))
        length = float(rng.uniform(min_length, max_length))
        start = rng.random(2) * tile_m
        direction = np.array([math.sin(azimuth), math.cos(azimuth)])  # (row, col)
        delta = direction * length

        # Vector from the segment start to every sample, minimum-imaged onto the torus.
        offset_row = py - start[0]
        offset_col = px - start[1]
        offset_row = offset_row - tile_m * np.rint(offset_row / tile_m)
        offset_col = offset_col - tile_m * np.rint(offset_col / tile_m)

        denom = float(delta @ delta)
        t = np.clip((offset_row * delta[0] + offset_col * delta[1]) / max(denom, 1e-12),
                    0.0, 1.0)
        near_row = offset_row - t * delta[0]
        near_col = offset_col - t * delta[1]
        distance = np.sqrt(near_row * near_row + near_col * near_col)

        # Aperture tapers to zero at both tips: a joint does not end in a blunt stop.
        taper = np.power(np.clip(4.0 * t * (1.0 - t), 0.0, 1.0), 0.35)
        effective = distance / np.maximum(taper, 1e-3)
        best = np.minimum(best, effective)

        traces.append({
            "azimuthDeg": round(math.degrees(azimuth) % 180.0, 3),
            "lengthM": round(length, 4),
            "conjugateSide": int(side),
            # Geometry is published, not just described, because the SPALL SCARS clip their
            # lateral edges to these actual lines. A scar that terminates at a real joint is
            # the difference between an angular flake and an oval blob.
            "startRow": float(start[0]),
            "startCol": float(start[1]),
            "dirRow": float(direction[0]),
            "dirCol": float(direction[1]),
        })

    mask = 1.0 - _smooth_step(0.0, width_m, best)
    return mask, traces


def periodic_warp(rng: np.random.Generator, resolution: int, tile_m: float,
                  wavelength_m: float, amplitude: float) -> np.ndarray:
    """Low-frequency periodic displacement, used to bend laminae off dead straight.

    A ruler-straight bedding plane is the tell of a procedural stripe field. Real
    laminae undulate because the sea floor they settled on did. Amplitude is kept small
    enough that the stack stays monotonic -- laminae must not cross each other, or the
    lithology index becomes non-physical.
    """
    field_2d = periodic_fbm(rng, resolution, tile_m,
                            coarsest_m=wavelength_m * 2.0,
                            finest_m=wavelength_m, beta=2.6)
    return field_2d * amplitude


# ===========================================================================
# The family spec
# ===========================================================================

@dataclass
class GeologyTextureSpec:
    """One geology texture family request, in physical units.

    ``tile_m`` is the single most important number in the file. Playbook section 6
    requires a declared "Required scale: 1 m tile, 2 m rock wall tile ..." and section 4
    opens the geology recipe with "Geology textures must be scale-calibrated". The value
    is not a preference: ``generators/rock.py`` projects its primary material with
    ``TRIPLANAR_METRES_PER_TILE = 1.25`` and records it per asset in
    ``uvAndTriplanarReport.triplanarMetresPerTile``, so a tile authored at any other
    scale would be resampled by the shader and every wavelength below would be wrong by
    that ratio.
    """

    set_name: str = "SedimentaryShelf"
    seed: int = 1713
    quality: float = 1.0
    resolution: int = 0            # 0 = derive from quality via law.texture_size_for
    tile_m: float = law.GEOLOGY_TRIPLANAR_METRES_PER_TILE
    process: str = "sedimentary"
    hero: bool = False

    def resolved_resolution(self) -> int:
        if self.resolution > 0:
            return int(self.resolution)
        return law.texture_size_for(self.quality, hero=self.hero)

    @property
    def metres_per_pixel(self) -> float:
        return self.tile_m / float(self.resolved_resolution())

    def family_name(self) -> str:
        return law.Family.GEOLOGY.value

    def texture_name(self, role: str) -> str:
        return law.NAME_TEXTURE.format(family=self.family_name(),
                                       set=self.set_name, role=role)


# ---------------------------------------------------------------------------
# Lithology: an actual sedimentary column, not a stripe pattern
# ---------------------------------------------------------------------------
# Playbook section 4, Sediment: "layered tan/gray/black deposits, fine ripple normals,
# shell fragments, low metallic." Cave wall: "stratification, vertical streaking, cavity
# AO, waterline discoloration." Section 0 demands the surface read as "a specific
# material with scale, age, pressure history, wetness, wear, contamination".
#
# A stripe pattern cannot deliver that, because the thing that makes laminated rock read
# is that each lamina is a DIFFERENT ROCK: a clay-rich lamina is dark, soft, rough and
# recesses under weathering; a carbonate-cemented silt lamina is pale, hard, smoother and
# stands proud. Colour banding and relief banding are then the same fact seen twice, which
# is exactly the correlation a real rock has -- and it is why every downstream channel can
# read from lithology without any of them becoming a copy of another.
#
# THE THICKNESS BAND IS DERIVED, NOT CHOSEN. law.GEOLOGY_TEXTURE_BAND_CEILING_M is
# 0.087 m, the finest wavelength a boulder's sculpt lattice can represent. A lamina
# COUPLET (one soft plus one hard) must stay under that or the texture starts competing
# with relief the mesh already carries, so single laminae cap at ~0.040 m. The floor is
# set by the tile's own resolution: a lamina thinner than a few pixels aliases in the mip
# chain, which section 9's mip gate rejects.

LAMINA_THICKNESS_MIN_M = 0.008
LAMINA_THICKNESS_MAX_M = 0.040

# A parting surface between two laminae is a PHYSICAL feature about 1 mm across, so its
# width is absolute. The first version tied it to ``3 * metres_per_pixel``, which looked
# like sensible anti-aliasing and was a real defect: at 512 it produced a 7.3 mm parting
# and half the surface measured as "on a contact", while at 2048 the same rock had a
# 1.8 mm parting. That makes the MATERIAL change with the quality lane, and
# ``3DMODEL_TEXTURES_MATERIALS.md`` section 9 forbids exactly that -- GlobalQualityWeight
# "must not change material channel semantics". The pixel floor stays, but only as a
# band-limit so a compact-lane tile does not alias, and it is declared in the manifest.
PARTING_WIDTH_M = 0.0012
PARTING_WIDTH_PIXEL_FLOOR = 2.5

# SPALL PATCHES break lamina CONTINUITY, which is the last thing separating this family
# from a polished laminated slab. Every earlier iteration had each lamina running unbroken
# across the whole 1.25 m tile, and that continuity -- not the tone, not the amplitude -- is
# what made the surface read as wood-grain veneer or a cut stone panel. A weathered rock face
# is not continuous: packages of laminae flake off along their partings, leaving a scar with a
# sharp rim where the beds inside no longer line up with the beds outside.
#
# Modelled by shifting the bedding COORDINATE by a per-patch amount inside each scar. The
# discontinuity in lamina index at the rim is the point, not an artefact -- that offset is
# exactly what a real spall scar shows. Patch layout is a coarse Worley so the scars are
# discrete and periodic.
# REBUILT AS ANGULAR, PARTING-BOUNDED, JOINT-CLIPPED PACKAGES. The Worley version read as
# rounded oval blobs -- water stains or sanded patches, with the cell shape showing through --
# because a Worley cell is a smooth convex region with no relationship to any structure in
# the rock. A real flake detaches ALONG A BEDDING PLANE and TERMINATES AT A JOINT, so its
# outline is made of straight segments that follow structures the field already contains:
#   * top and bottom edges are lamina boundaries, snapped exactly to the parting positions;
#   * one lateral edge is an ACTUAL joint line, reusing that trace's own position and azimuth;
#   * the opposite lateral edge is parallel to it, at a drawn distance.
# The result is a parallelogram whose every edge is a structure, which is what makes it read
# as rock that broke rather than as a stain.
# Reduced from 78 once the truncation blocks took over run-breaking. Scars no longer have to
# carry that job, so they can go back to being a moderate weathering texture instead of
# covering 40 percent of the face.
# 18, not 40. At 40 the scars stopped being events and became a MOSAIC: every one is a
# parallelogram sharing the same two orientations, packed densely enough to read as crazy
# paving or parquet -- assembled stone panels rather than one weathered face. Angular was
# the right instruction; angular AND dense AND identically oriented is masonry.
# TUNED AT THE SHIPPED 2048 LANE, and it has to be. The structural-extent statistics are
# RESOLUTION-DEPENDENT because periodic_fbm and periodic_worley consume resolution-sized
# draws from the shared rng, so the scar layout for one seed differs between lanes: count 22
# measured erosional coverage 0.195 at 512 and 0.141 at 2048, passing the family requirement
# at one lane and failing it at the other. Tuning at 512 and trusting 2048 is therefore
# invalid. The clean fix is a per-generator rng stream seeded from a hash rather than one
# shared stream -- generators/rock.py has the same property -- and that is outstanding debt,
# not something to change under a texture task.
SPALL_SCAR_COUNT = 36
SPALL_PACKAGE_LAMINAE_MIN = 1
SPALL_PACKAGE_LAMINAE_MAX = 4
# NARROW AND DENSE beats wide and sparse. At 0.08-0.30 width the scars met the run
# budget only by covering 49.6 percent of the tile -- half the face became scar and the
# bedding it was meant to interrupt had little left to interrupt. Narrow scars act as
# TRUNCATIONS rather than removals, which is the geometry that breaks a run.
SPALL_WIDTH_MIN_FRACTION = 0.05
SPALL_WIDTH_MAX_FRACTION = 0.16
SPALL_OFFSET_MIN_FRACTION = 0.60
SPALL_OFFSET_MAX_FRACTION = 2.20
# A flake is SHALLOW -- one package thick -- but can be wide. Depth is kept modest on purpose
# so widening the scars to break lamina continuity cannot push spall above parting in the RMS
# hierarchy, which is the requirement this family already declares.
SPALL_STEP_FRACTION = 0.24

# Competence band. Hardness of exactly 0 is a lamina with no resistance at all, which
# drives the differential-relief term to its full depth in one lamina and produces a
# trench rather than a recess.
LAMINA_HARDNESS_MIN = 0.08
LAMINA_HARDNESS_MAX = 0.95


@dataclass
class LaminaStack:
    """Per-pixel lithology, sampled from a periodic stack of laminae."""

    index: np.ndarray          # integer lamina id
    across: np.ndarray         # 0..1 position across the lamina, 0 at its base
    hardness: np.ndarray       # competence: resists weathering, stands proud
    carbonate: np.ndarray      # pale carbonate cement fraction
    organic: np.ndarray        # dark organic/clay fraction; hosts pyrite
    porosity: np.ndarray       # where dissolution can open a vug at all
    spall: np.ndarray          # 0..1 inside a flake scar, sharp at the rim
    gouge: np.ndarray          # 0..1 on a truncation-block wall
    contact: np.ndarray        # 1.0 exactly on a lamina contact, falling off
    count: int
    thicknesses_m: np.ndarray

    @property
    def softness(self) -> np.ndarray:
        return 1.0 - self.hardness


# TRUNCATION BLOCKS: differential erosion cutting ACROSS bed packages.
#
# Scars alone could not meet the run budget, and the measurement showed why rather than
# suggesting it. A spall scar interrupts only the rows inside its own bed package, so with
# 78 scars stratified over 52 laminae each lamina still got about 1.5 of them and a row with
# one 10-percent-wide scar retained a 90-percent run. Pushing the count higher raised
# coverage to 40 percent without fixing p95 -- the wrong mechanism applied harder.
#
# A truncation is a LINE ACROSS THE WHOLE TILE, so it splits every row it crosses. Three of
# them break the bedding into domains at a coverage cost of almost nothing, which is the
# efficiency a patch-based mechanism cannot reach. Geologically these are small
# faults/unconformities: beds on one side do not line up with beds on the other, which is
# ``3DMODEL_GEOLOGY_ROCKS.md`` section 1's "sheared planes" and "collapsed fracture faces".
TRUNCATION_COUNT = 4
TRUNCATION_WIDTH_MIN_FRACTION = 0.30
TRUNCATION_WIDTH_MAX_FRACTION = 0.62
TRUNCATION_OFFSET_MIN_FRACTION = 2.0
TRUNCATION_OFFSET_MAX_FRACTION = 6.5
TRUNCATION_GOUGE_M = 0.0030
# 0.16, not 0.34. A bright continuous highlight along every wall is what turned the
# fault traces into scribed lines under raking light; the offset across the wall is the
# feature that matters, not the notch at it.
TRUNCATION_GOUGE_DEPTH_FRACTION = 0.16


def _build_truncation_blocks(spec: GeologyTextureSpec, rng: np.random.Generator,
                             thicknesses: np.ndarray,
                             joint_traces: Optional[list]) -> tuple:
    """Fault/unconformity blocks that offset the bedding wholesale. ``(offset_m, gouge)``.

    Each block is a wide slab whose orientation is taken from the joint set, so the
    truncation is parallel to a real structure rather than an arbitrary cut. Inside the slab
    the bedding coordinate is displaced by SEVERAL lamina thicknesses -- enough that no bed
    can be traced across the boundary, which is the whole point. The boundary itself carries
    a thin gouge recess so it is visible and so the extent metric can count it.
    """
    resolution = spec.resolved_resolution()
    axis = (np.arange(resolution) + 0.5) / resolution * spec.tile_m
    py, px = np.meshgrid(axis, axis, indexing="ij")
    mean_thickness = float(thicknesses.mean())

    steep = [t for t in (joint_traces or ()) if abs(float(t.get("dirRow", 0.0))) > 0.35]
    wall_jitter = periodic_warp(rng, resolution, spec.tile_m,
                                wavelength_m=0.16, amplitude=0.012)
    offset = np.zeros((resolution, resolution), dtype=np.float64)
    gouge = np.zeros((resolution, resolution), dtype=np.float64)

    for index in range(max(1, TRUNCATION_COUNT)):
        if steep:
            trace = steep[index % len(steep)]
            direction = np.array([trace["dirRow"], trace["dirCol"]])
        else:
            angle = math.radians(JOINT_STRESS_AZIMUTH_DEG + JOINT_CONJUGATE_DEG)
            direction = np.array([math.sin(angle), math.cos(angle)])
        norm = float(np.hypot(direction[0], direction[1]))
        if norm < 1e-9:
            continue
        direction = direction / norm
        normal = np.array([direction[1], -direction[0]])

        origin = rng.random(2) * spec.tile_m
        offset_row = py - origin[0]
        offset_col = px - origin[1]
        offset_row = offset_row - spec.tile_m * np.rint(offset_row / spec.tile_m)
        offset_col = offset_col - spec.tile_m * np.rint(offset_col / spec.tile_m)
        lateral = offset_row * normal[0] + offset_col * normal[1]
        # IRREGULAR WALLS. A truncation block spans the whole tile, so each of its two walls is
        # a full-length line -- and at four blocks that is eight straight lines crossing the
        # face. Under grazing light they read as a SCRIBED GRID rather than as fault traces: the
        # mechanism was right and the geometry was too clean. A real fault trace wanders. Same
        # shared-fabric jitter the spall walls use, at a longer wavelength because a fault
        # surface is smoother than a flake edge.
        lateral = lateral + wall_jitter

        width = float(rng.uniform(TRUNCATION_WIDTH_MIN_FRACTION,
                                  TRUNCATION_WIDTH_MAX_FRACTION)) * spec.tile_m
        inside = (lateral >= 0.0) & (lateral < width)
        throw = mean_thickness * float(rng.uniform(TRUNCATION_OFFSET_MIN_FRACTION,
                                                   TRUNCATION_OFFSET_MAX_FRACTION))
        offset = offset + np.where(inside, throw, 0.0)

        # Gouge on both walls of the block.
        near_wall = np.minimum(np.abs(lateral), np.abs(lateral - width))
        gouge = np.maximum(gouge, 1.0 - _smooth_step(0.0, TRUNCATION_GOUGE_M, near_wall))

    return offset, gouge


def _longest_wrapped_run(row: np.ndarray) -> int:
    """Longest run of True in a row, treating the row as a CIRCLE.

    Wrapping matters because the tile wraps: a bed that is interrupted at pixel 3 and at
    pixel 2045 still runs almost the full width once tiled, and a non-wrapped scan would
    report two short runs and call it broken.
    """
    if not row.any():
        return 0
    if row.all():
        return int(row.size)
    rotated = np.roll(row, -int(np.argmin(row)))
    padded = np.concatenate(([0], rotated.astype(np.int8), [0]))
    edges = np.flatnonzero(np.diff(padded))
    starts, ends = edges[::2], edges[1::2]
    return int((ends - starts).max()) if starts.size else 0


def measure_structural_extent(lamina: LaminaStack, joint: np.ndarray,
                              tile_m: float) -> dict:
    """How far the bedding RUNS before something erosional interrupts it.

    THIS IS THE OTHER HALF OF THE HIERARCHY CONSTRAINT. Declaring a depth for every relief
    term fixed the ordering; it said nothing about extent, and a bed crossing the entire tile
    at constant thickness makes the same kind of unstated claim a rank-one vug did. Sawn
    timber and weathered rock can share a relief hierarchy and differ only in run length.

    Interruption means a spall scar or a joint trace -- the two erosional structures in the
    field. Grain and vugs are not interruptions: they roughen a bed without truncating it.

    Reported against ``law.GEOLOGY_LAMINA_MAX_RUN_FRACTION`` and
    ``law.GEOLOGY_MIN_EROSIONAL_COVERAGE``. Declared as a FAMILY STRUCTURAL METRIC and kept
    out of the section 9 gate list: the playbook names eleven gates and inventing a twelfth
    inside that table would misrepresent the authority. It is reported beside them instead.
    """
    interrupted = ((lamina.spall > 0.5) | (joint > 0.5) | (lamina.gouge > 0.5))
    intact = ~interrupted
    runs = np.array([_longest_wrapped_run(intact[row]) for row in range(intact.shape[0])],
                    dtype=np.float64)
    width = float(intact.shape[1])
    fractions = runs / width
    erosional = float(interrupted.mean())
    p95 = float(np.percentile(fractions, 95))
    return {
        "note": "family structural metric, NOT one of the eleven section 9 gates",
        "longestIntactRunFraction": {
            "p50": round(float(np.percentile(fractions, 50)), 5),
            "p95": round(p95, 5),
            "max": round(float(fractions.max()), 5),
        },
        "longestIntactRunM": round(p95 * tile_m, 4),
        "runBudgetFraction": law.GEOLOGY_LAMINA_MAX_RUN_FRACTION,
        "runBudgetMet": bool(p95 <= law.GEOLOGY_LAMINA_MAX_RUN_FRACTION),
        "erosionalCoverage": round(erosional, 5),
        "erosionalCoverageMin": law.GEOLOGY_MIN_EROSIONAL_COVERAGE,
        "erosionalCoverageMet": bool(erosional >= law.GEOLOGY_MIN_EROSIONAL_COVERAGE),
        "interruptedBy": "spall scars, joint traces and truncation-block walls; grain "
                         "and vugs roughen a bed without truncating it",
    }


def _build_spall_scars(spec: GeologyTextureSpec, rng: np.random.Generator,
                       base_coordinate: np.ndarray, boundaries: np.ndarray,
                       thicknesses: np.ndarray,
                       joint_traces: Optional[list]) -> tuple:
    """Angular flake scars bounded by partings and clipped to real joint lines.

    Each scar is the intersection of

      * a BED PACKAGE: the bedding coordinate lying between two lamina boundaries taken
        straight out of ``boundaries``, so the top and bottom edges sit exactly on partings
        rather than near them;
      * a SLAB bounded by an ACTUAL joint line and a parallel line at a drawn distance, so
        one lateral edge terminates on a fracture that exists elsewhere in the tile.

    Every edge is therefore a structure already present in the rock, which is what makes the
    outline angular. The previous Worley construction could not be angular at all: a Voronoi
    cell is a smooth convex region unrelated to bedding or jointing, and it rendered as an
    oval stain.

    Periodicity comes from minimum-imaging the offset from the joint's own start point,
    the same device the joint traces use. It can select a wrong image only for samples far
    from the line, and those fall outside the slab in every image, so the mask is zero there
    regardless.

    Returns ``(mask, offset_m)``. The OFFSET is what actually interrupts the laminae: inside
    a scar the bedding coordinate is displaced by more than one lamina thickness, so the beds
    exposed in the scar floor do not line up with the beds outside it. That discontinuity at
    the rim is the feature, not an artefact.
    """
    resolution = spec.resolved_resolution()
    axis = (np.arange(resolution) + 0.5) / resolution * spec.tile_m
    py, px = np.meshgrid(axis, axis, indexing="ij")

    lamina_count = len(thicknesses)
    mean_thickness = float(thicknesses.mean())

    # Prefer joints that actually cut ACROSS bedding. A trace running nearly parallel to the
    # bedding plane would give a lateral edge indistinguishable from the parting edges, and
    # the scar would degenerate back into a plain horizontal band.
    usable = []
    for trace in (joint_traces or ()):
        if abs(float(trace.get("dirRow", 0.0))) > 0.35:
            usable.append(trace)
    if not usable:
        usable = list(joint_traces or ())

    mask = np.zeros((resolution, resolution), dtype=np.float64)
    offset = np.zeros((resolution, resolution), dtype=np.float64)
    # One SHARED edge-jitter field, so every scar's walls are roughened by the same rock
    # fabric rather than by independent noise per scar.
    edge_jitter = periodic_warp(rng, resolution, spec.tile_m,
                                wavelength_m=0.055, amplitude=0.007)

    # SCAR PLACEMENT IS STRATIFIED ACROSS THE COLUMN, not uniformly random, and the
    # difference is measurable rather than cosmetic. With random starts the first measurement
    # of ``longestIntactRunFraction`` came back at p95 = 1.00 -- some bed packages were never
    # crossed by any scar, so those rows ran the full tile width and the surface still read as
    # sawn timber even with 22 percent erosional coverage. Stratifying guarantees every part of
    # the stack is attacked; the jitter keeps the scars from landing on a visible ladder.
    total = max(1, SPALL_SCAR_COUNT)
    for _index in range(total):
        # --- bed package: two real parting positions -----------------------------
        stratum = (_index + rng.random()) / float(total)
        start_lamina = int(min(lamina_count - 1, stratum * lamina_count))
        package = int(rng.integers(SPALL_PACKAGE_LAMINAE_MIN,
                                   SPALL_PACKAGE_LAMINAE_MAX + 1))
        lo = float(boundaries[start_lamina])
        hi = float(boundaries[min(start_lamina + package, lamina_count)])
        if hi <= lo:
            continue
        # Wrapped band test: the package may straddle the tile's bedding wrap.
        if hi <= spec.tile_m:
            in_package = (base_coordinate >= lo) & (base_coordinate < hi)
        else:
            in_package = ((base_coordinate >= lo)
                          | (base_coordinate < (hi - spec.tile_m)))

        # --- lateral slab: one edge on a real joint line -------------------------
        if usable:
            trace = usable[int(rng.integers(0, len(usable)))]
            origin = np.array([trace["startRow"], trace["startCol"]])
            direction = np.array([trace["dirRow"], trace["dirCol"]])
        else:
            angle = math.radians(JOINT_STRESS_AZIMUTH_DEG + JOINT_CONJUGATE_DEG)
            origin = rng.random(2) * spec.tile_m
            direction = np.array([math.sin(angle), math.cos(angle)])
        norm = float(np.hypot(direction[0], direction[1]))
        if norm < 1e-9:
            continue
        direction = direction / norm
        # Perpendicular, so a positive value is "this far to one side of the joint".
        normal = np.array([direction[1], -direction[0]])

        offset_row = py - origin[0]
        offset_col = px - origin[1]
        offset_row = offset_row - spec.tile_m * np.rint(offset_row / spec.tile_m)
        offset_col = offset_col - spec.tile_m * np.rint(offset_col / spec.tile_m)
        lateral = offset_row * normal[0] + offset_col * normal[1]
        # RAGGED, not ruled. A flake's lateral edge follows the joint it terminated against,
        # but rock does not break along a drafting line -- and perfectly straight edges are
        # what made the dense version read as cut masonry. Perturbing the lateral coordinate
        # roughens both walls at the centimetre scale while leaving the edge's overall
        # orientation on the joint.
        lateral = lateral + edge_jitter

        width = float(rng.uniform(SPALL_WIDTH_MIN_FRACTION,
                                  SPALL_WIDTH_MAX_FRACTION)) * spec.tile_m
        side = 1.0 if rng.random() < 0.5 else -1.0
        in_slab = (lateral * side >= 0.0) & (lateral * side < width)

        scar = in_package & in_slab
        if not scar.any():
            continue
        scar_offset = mean_thickness * float(rng.uniform(SPALL_OFFSET_MIN_FRACTION,
                                                         SPALL_OFFSET_MAX_FRACTION))
        # Later scars overwrite earlier ones where they overlap, which is how a real face
        # accumulates: a younger flake removes part of an older scar.
        offset = np.where(scar, scar_offset, offset)
        mask = np.where(scar, 1.0, mask)

    return mask, offset


def build_lamina_stack(spec: GeologyTextureSpec,
                       rng: np.random.Generator,
                       joint_traces: Optional[list] = None) -> LaminaStack:
    """Build the sedimentary column and sample it per pixel.

    Exactly periodic in V by construction: an integer lamina count whose thicknesses are
    renormalised to sum to exactly ``tile_m``. Approximating that with a modulo of a
    float stack leaves a partial lamina at the wrap, which is a visible stripe of the
    wrong width once tiled -- the seam gate would catch it, but only after the fact.

    The undulation is applied to the bedding COORDINATE rather than to the boundaries, so
    laminae bend together and never cross. Crossing laminae would make the lithology
    index non-physical, and the resulting colour field would read as marbling rather than
    as bedding.
    """
    resolution = spec.resolved_resolution()

    # Row 0 is the TOP of the image. V increases upward, so a lamina's "base" is below it
    # in image terms. Getting this backwards inverts the whole stack's younging direction,
    # which is invisible on its own but puts overhang runoff on the wrong side of a ledge.
    v_up = 1.0 - (np.arange(resolution) + 0.5) / resolution
    depth_m = (v_up * spec.tile_m)[:, None] * np.ones((1, resolution))

    mean_thickness = 0.5 * (LAMINA_THICKNESS_MIN_M + LAMINA_THICKNESS_MAX_M)
    count = max(3, int(round(spec.tile_m / mean_thickness)))
    raw = rng.uniform(LAMINA_THICKNESS_MIN_M, LAMINA_THICKNESS_MAX_M, size=count)
    thicknesses = raw * (spec.tile_m / raw.sum())
    boundaries = np.concatenate([[0.0], np.cumsum(thicknesses)])

    # Undulation, at TWO scales. A single 0.0045 m term was measured too weak: the first
    # preview render showed laminae running dead straight across the whole tile and reading
    # as corduroy or stacked planks -- the same "stack of discs" failure ``rock.py``
    # documents for its beds, and a pattern rather than a rock.
    #
    # A long, larger term bends whole packages of laminae; a shorter, smaller one gives each
    # one its own waviness. The FOLD LIMIT is what bounds them: the warp's derivative along
    # the bedding coordinate must stay well under 1 or the mapping becomes non-monotonic and
    # laminae fold through each other. For a sinusoid of amplitude ``a`` and wavelength
    # ``L`` that derivative is ``2*pi*a/L``, so 0.011 m at 0.94 m gives 0.074 and 0.004 m at
    # 0.18 m gives 0.14 -- together 0.21, safely inside the limit.
    #
    # A THIRD, FINE TERM MAKES THE LAMINA EDGES RAGGED, and it fixes a defect the two smooth
    # terms caused. With only long-wavelength warps every lamina boundary was a smooth
    # continuous curve running the width of the tile, and combined with the sinusoidal
    # cross-lamina relief profile the surface read as WOOD GRAIN or plywood veneer rather
    # than as stone. Measured at the time: mean slope 16.6 degrees, and raising the grain
    # amplitude 1.8x moved it only 1 degree -- proof the smoothness lived in the BEDDING
    # term, not in the grain, so piling more noise on top would not have fixed it.
    #
    # Physically this is spalling: weathered laminae break off in small flakes along their
    # partings, so a contact is chipped at the centimetre scale rather than drawn. Fold
    # budget: 2*pi*0.0015/0.030 = 0.31, taking the total to about 0.52, inside the limit.
    warp = (periodic_warp(rng, resolution, spec.tile_m,
                          wavelength_m=spec.tile_m * 0.75, amplitude=0.011)
            + periodic_warp(rng, resolution, spec.tile_m,
                            wavelength_m=0.18, amplitude=0.004)
            + periodic_warp(rng, resolution, spec.tile_m,
                            wavelength_m=0.030, amplitude=0.0015))
    # The BASE bedding coordinate, before any scar offset. Scars are decided from this so a
    # scar's own displacement cannot feed back into where scars are.
    truncation_offset, gouge = _build_truncation_blocks(
        spec, rng, thicknesses, joint_traces)
    base_coordinate = np.mod(depth_m + warp + truncation_offset, spec.tile_m)

    spall, spall_offset = _build_spall_scars(
        spec, rng, base_coordinate, boundaries, thicknesses, joint_traces)

    coordinate = np.mod(base_coordinate + spall_offset * spall, spec.tile_m)

    index = np.clip(np.searchsorted(boundaries, coordinate, side="right") - 1,
                    0, count - 1)
    base = boundaries[index]
    span = thicknesses[index]
    across = np.clip((coordinate - base) / np.maximum(span, 1e-9), 0.0, 1.0)

    # Per-lamina lithology. Hardness and organic content anti-correlate in real
    # mudstone-siltstone couplets: the clay/organic-rich laminae are the weak ones. That
    # is a fact about the rock, so it is modelled rather than randomised independently.
    organic = rng.random(count) ** 1.4
    carbonate = np.clip(rng.random(count) * (1.0 - organic) * 1.25, 0.0, 1.0)
    hardness = np.clip(0.30 + 0.55 * carbonate - 0.35 * organic
                       + 0.10 * (rng.random(count) - 0.5),
                       LAMINA_HARDNESS_MIN, LAMINA_HARDNESS_MAX)

    # Contact proximity: the parting surface between two laminae.
    contact_width_m = max(PARTING_WIDTH_M,
                          PARTING_WIDTH_PIXEL_FLOOR * spec.metres_per_pixel)
    to_base = coordinate - base
    to_top = (base + span) - coordinate
    contact = np.exp(-(np.minimum(to_base, to_top) / contact_width_m) ** 2)

    # LATERAL VARIATION ALONG THE BEDDING. A lamina is not one flat tone from edge to edge:
    # it pinches and swells, and its carbonate content varies laterally as the depositional
    # environment shifted. Without this every band renders as a single uniform stripe, which
    # is the other half of the corduroy problem the two-scale warp addresses geometrically.
    # The field is stretched hard along U so the variation runs WITH the bedding rather than
    # cutting across it, which would read as mottling.
    lateral = _normalise_to_peak(
        periodic_fbm(rng, resolution, spec.tile_m,
                     coarsest_m=spec.tile_m * 0.8, finest_m=0.09,
                     beta=2.3, anisotropy=5.0, anisotropy_axis="v")) * 0.5 + 0.5

    carbonate_field = np.clip(carbonate[index] * (0.68 + 0.64 * lateral), 0.0, 1.0)
    organic_field = np.clip(organic[index] * (0.78 + 0.44 * (1.0 - lateral)), 0.0, 1.0)

    # POROSITY IS A PER-LAMINA PROPERTY, and that is the whole point of adding it.
    # Dissolution vugs open where there is soluble cement and connected pore space, which is
    # a property of the BED, not of the rock face. Without this the vug field spread evenly
    # across every lamina and the tile read as pumice or aerated concrete rather than as
    # layered stone -- the lead's rejection, and correct.
    #
    # It correlates with carbonate because that is the phase that dissolves, but it gets its
    # own draw and its own exponent: a well-cemented tight limestone lamina is carbonate-rich
    # and NOT porous. Making porosity a pure function of carbonate would have made it a
    # second copy of a field that already drives colour and roughness, which is the
    # duplication section 9's independence gate exists to catch.
    porosity_per_lamina = np.clip(
        (0.25 + 0.75 * rng.random(count)) * np.power(carbonate + 0.12, 0.85), 0.0, 1.0)
    porosity_per_lamina = np.power(porosity_per_lamina, 1.6)  # skew toward tight beds
    porosity_field = np.clip(porosity_per_lamina[index] * (0.75 + 0.5 * lateral),
                             0.0, 1.0)

    return LaminaStack(
        index=index,
        across=across,
        hardness=hardness[index],
        carbonate=carbonate_field,
        organic=organic_field,
        porosity=porosity_field,
        spall=spall,
        gouge=gouge,
        contact=contact,
        count=count,
        thicknesses_m=thicknesses,
    )


# ===========================================================================
# Step 2 of section 10: the source height field
# ===========================================================================
# Every relief term is in METRES and every depth is justified against a witness the
# geology bible or rock.py already fixed. A texture generator that works in arbitrary
# 0..1 units cannot answer "is this rock the right roughness at 2 m", which is the
# question section 4's "scale-calibrated" requirement is asking.

# THE RELIEF HIERARCHY IS NOW ORDERED, AND EVERY SUBORDINATE TERM IS A FRACTION OF THE
# BEDDING RELIEF RATHER THAN AN INDEPENDENT ABSOLUTE.
#
# The first version set six depths independently and the result was measured -- from this
# module's own manifest -- as vugs 11.0 / grain 8.2 / recess 7.3 / joints 4.5 / parting 3.5
# / bioclasts 2.2 mm. For a laminated sedimentary shelf that ordering is INVERTED: the pits
# were physically the largest feature on the surface and the bedding came fifth, so the tile
# read as porous stone rather than as layered stone. The lead rejected the family on exactly
# that number, and the two visual defects (a joint network reading as crazed glaze, a vug
# field reading as pumice) were consequences of the same inversion rather than independent
# faults.
#
# This is the same correction already applied one level down when rock.py's grain slope law
# turned out not to transfer: grain is derived from the structure it decorates. Applying it
# one level up, BEDDING is the primary relief and everything else is expressed against it,
# so the hierarchy cannot silently invert again when a single constant is edited.
BEDDING_RECESS_M = 0.011

# Fractions of BEDDING_RECESS_M. Parting leads the subordinate terms because a parting
# surface is the sharpest, most legible expression of bedding on a weathered face.
PARTING_DEPTH_FRACTION = 0.58
JOINT_DEPTH_FRACTION = 0.30
VUG_TYPICAL_DEPTH_FRACTION = 0.20
SHELL_RELIEF_FRACTION = 0.18

PARTING_DEPTH_M = BEDDING_RECESS_M * PARTING_DEPTH_FRACTION
JOINT_DEPTH_M = BEDDING_RECESS_M * JOINT_DEPTH_FRACTION
SHELL_RELIEF_M = BEDDING_RECESS_M * SHELL_RELIEF_FRACTION

# The 0.011 m pit witness is retained as the depth of the RAREST, LARGEST vug rather than
# as the depth of every vug. That keeps rock.py's witness honoured where it is meaningful --
# a discrete macro cavity really is that deep -- while the typical vug stays subordinate to
# bedding. Depth scales with radius because a cavity's aspect ratio is roughly constant.
VUG_MAX_DEPTH_M = law.GEOLOGY_PIT_DEPTH_M

# Joints: a STRESS-ORIENTED SPARSE TRACE SET, not a Worley partition.
#
# The Worley generator was replaced outright, not tuned. A Voronoi field is a PARTITION by
# construction, so every cell carries a complete boundary and the output is necessarily a
# closed network -- thinning the lines makes a fainter tessellation, not a sparser joint set.
# Real joints form conjugate sets sub-parallel to the principal stress direction, with finite
# trace length, and they do not enclose the plane.
#
# So joints are now discrete line segments: orientations drawn about two conjugate azimuths
# at +/- JOINT_CONJUGATE_DEG from a stress axis, finite lengths, random positions, distance
# field evaluated on the torus. Bed-confined jointing is modelled too -- a trace dies out in
# soft clay and propagates through brittle cemented laminae, which is real and is also what
# keeps the traces from looking painted on.
JOINT_TRACE_COUNT = 11
JOINT_STRESS_AZIMUTH_DEG = 24.0
JOINT_CONJUGATE_DEG = 27.0
JOINT_AZIMUTH_JITTER_DEG = 7.0
JOINT_TRACE_LENGTH_MIN_FRACTION = 0.12
JOINT_TRACE_LENGTH_MAX_FRACTION = 0.42
JOINT_WIDTH_M = 0.0026
JOINT_WAVINESS_M = 0.004

# Bioclasts. Playbook section 4 names "shell fragments" for sediment specifically. They
# stand PROUD because carbonate shell resists weathering better than a clay matrix, and
# they are sparse because a rock face densely paved with shells is a coquina, a different
# lithology from the one this family declares.
SHELL_OCCUPANCY = 0.16

# Vug size distribution. A power law is what gives the "orders of magnitude" spread real
# cavities have; a uniform draw produced near-identical pits, which is half of why the field
# read as aerated concrete. Radii are in CELL units.
VUG_RADIUS_MIN = 0.05
VUG_RADIUS_MAX = 0.85
VUG_PARETO_ALPHA = 1.9
VUG_POROSITY_LOW = 0.16
VUG_POROSITY_HIGH = 0.42

# GRAIN AMPLITUDE IS DERIVED FROM THE STRUCTURE IT DECORATES, NOT FROM ITS OWN
# WAVELENGTH, and that is a correction to the obvious carry-over rather than a preference.
#
# ``generators/rock.py`` :223-224 sets GRAIN_SLOPE_MIN/MAX = 0.10/0.155 as a ratio of
# amplitude to grain wavelength, giving 7.5-11.6 mm of grain relief at the 0.075 m
# witness. Reusing that ratio here was the first attempt and it was measured wrong: grain
# came out with a peak-to-peak of 0.100 m against the lamina relief's 0.0075 m -- thirteen
# times the structure it is supposed to sit on. rock.py's own comment states the test the
# result failed: grain must stay "an order of magnitude under the bed relief so the strata
# stay dominant", and section 1 of the geology bible rejects a rock that reads as a noise
# blob.
#
# The ratio is not wrong on the mesh; it is calibrated against BED relief, which is
# 0.055-0.34 m. The texture's structure is LAMINA relief at 0.008-0.040 m, an order of
# magnitude smaller, so the same ratio necessarily inverts the hierarchy. Both numbers are
# recorded in the manifest so the discrepancy stays visible.
# RESTORED after over-correction. Crushing the ratio to 0.10-0.17 fixed the relief
# inversion and created a new defect: at grain RMS 0.53 mm against bedding's 6.14 mm
# there was nothing breaking up the bands, and the tile read as WOOD GRAIN or plywood
# veneer -- long smooth parallel wavy lines edge to edge. Bedding must LEAD the relief,
# which is not the same as bedding being the only relief: a weathered rock face has
# strong bedding AND strong stone micro-character. Mean surface slope, which is what
# actually carries "reads as stone", had fallen from 26.9 to 16.6 degrees.
GRAIN_TO_STRUCTURE_RATIO_MIN = 0.20
GRAIN_TO_STRUCTURE_RATIO_MAX = 0.30
MESH_GRAIN_SLOPE_MIN = 0.10   # rock.py:223, retained for the manifest comparison
MESH_GRAIN_SLOPE_MAX = 0.155  # rock.py:224

# The fBm's finest wavelength, in PIXELS. Four pixels is two samples per half-wave; below
# that the field aliases into the mip chain, and "ringing" plus "dark seams" in the mip
# preview are named rejections in section 9. This is the reason the band has a floor at
# all rather than running to Nyquist.
FBM_FINEST_PIXELS = 4.0


@dataclass
class HeightField:
    """The source surface, in metres, plus the masks that built it.

    The masks are kept because the downstream channels need to know WHY a pixel is low.
    A pit and a parting groove are both concave, and grime belongs in both, but only the
    pit is a place a pyrite framboid survives. Discarding the masks and re-deriving
    intent from the height alone is what forces a generator into blind multiplication.
    """

    height_m: np.ndarray
    lamina: LaminaStack
    grain: np.ndarray
    pit: np.ndarray
    joint: np.ndarray
    shell: np.ndarray
    metres_per_pixel: float
    report: dict = field(default_factory=dict)

    @property
    def range_m(self) -> tuple:
        return (float(self.height_m.min()), float(self.height_m.max()))


def _smooth_step(edge0: float, edge1: float, values: np.ndarray) -> np.ndarray:
    t = np.clip((values - edge0) / max(edge1 - edge0, 1e-12), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def _normalise_to_peak(values: np.ndarray, percentile: float = 99.5) -> np.ndarray:
    """Scale so a robust peak reaches 1.0, giving AMPLITUDE semantics not std semantics.

    ``periodic_fbm`` returns a unit-standard-deviation field. Multiplying that by a
    requested amplitude is the intuitive move and it is wrong by a factor of about 4.5:
    over a 2048-square tile a near-Gaussian field reaches roughly 4.5 sigma, so a field
    asked for 11.6 mm of amplitude delivered 104 mm of peak-to-peak relief. A robust
    percentile rather than the true maximum keeps one freak pixel from scaling the whole
    field down.
    """
    peak = float(np.percentile(np.abs(values), percentile))
    return values / peak if peak > 1e-12 else values


def build_height_field(spec: GeologyTextureSpec,
                       rng: np.random.Generator) -> HeightField:
    """Compose the source relief from the sanctioned generators in section 5.

    Order matters only for legibility; the terms are additive because they are
    independent physical processes acting on the same rock -- deposition laid the laminae,
    weathering recessed the soft ones, dissolution opened the vugs, tectonics opened the
    joints, and the shells were already there. Multiplying them would make each process
    conditional on another for no physical reason, which is the blind-multiplication
    failure section 5 names.
    """
    resolution = spec.resolved_resolution()
    mpp = spec.metres_per_pixel

    # JOINTS ARE BUILT FIRST because the spall scars clip their lateral edges to real joint
    # lines. That dependency is the whole reason the scars can be angular, so the ordering is
    # load-bearing rather than incidental.
    joint_trace, joint_traces = periodic_joint_traces(
        rng, resolution, spec.tile_m,
        count=JOINT_TRACE_COUNT,
        stress_azimuth_deg=JOINT_STRESS_AZIMUTH_DEG,
        conjugate_deg=JOINT_CONJUGATE_DEG,
        jitter_deg=JOINT_AZIMUTH_JITTER_DEG,
        length_min_fraction=JOINT_TRACE_LENGTH_MIN_FRACTION,
        length_max_fraction=JOINT_TRACE_LENGTH_MAX_FRACTION,
        width_m=JOINT_WIDTH_M,
        waviness_m=JOINT_WAVINESS_M)

    lamina = build_lamina_stack(spec, rng, joint_traces=joint_traces)

    # --- deposition + differential weathering -----------------------------------
    # A soft lamina is deepest in its middle and rises back toward its contacts, because
    # the contact itself is cemented. A flat step per lamina reads as a stack of discs.
    profile = 0.55 + 0.45 * np.sin(np.pi * lamina.across)
    recess = -BEDDING_RECESS_M * lamina.softness * profile

    # --- the parting incision ---------------------------------------------------
    parting = -PARTING_DEPTH_M * lamina.contact

    # --- spall scars: a flaked package sits below the intact face ----------------
    spall_relief = -BEDDING_RECESS_M * SPALL_STEP_FRACTION * lamina.spall

    # --- truncation gouge: the wall of a sheared block ---------------------------
    gouge_relief = -BEDDING_RECESS_M * TRUNCATION_GOUGE_DEPTH_FRACTION * lamina.gouge

    # --- grain: band-limited anisotropic fBm ------------------------------------
    finest_m = FBM_FINEST_PIXELS * mpp
    coarsest_m = law.GEOLOGY_GRAIN_WITNESS_M
    anisotropy = 3.4 if spec.process == "sedimentary" else 2.1
    # GRAIN IS TWO BANDS, AND MAKING IT ONE WAS A DEFECT VISIBLE ONLY UNDER GRAZING LIGHT.
    #
    # A single band-limited fBm carrying anisotropy 3.4 all the way down to the pixel floor
    # produced a dense, coherent, directional micro-pattern that read as WOVEN FABRIC or
    # burlap under a 7-degree light. Strong spectral anisotropy makes the fine octaves into
    # long parallel streaks, and a field of parallel streaks at one scale is a weave.
    #
    # The physics says to split it. Depositional fabric is genuinely directional at the
    # millimetre-to-centimetre scale -- that is what bedding IS -- but an individual silt or
    # clay grain is equant, so the finest band has no reason to be anisotropic and every
    # reason not to be. Coarse band keeps the bedding-parallel fabric; fine band is isotropic
    # stone micro-texture.
    fabric_floor_m = max(finest_m * 2.0, 0.020)
    grain_coarse = periodic_fbm(rng, resolution, spec.tile_m,
                                coarsest_m=coarsest_m, finest_m=fabric_floor_m,
                                anisotropy=anisotropy, anisotropy_axis="v")
    grain_fine = periodic_fbm(rng, resolution, spec.tile_m,
                              coarsest_m=fabric_floor_m, finest_m=finest_m,
                              beta=2.05, anisotropy=1.0)
    grain_unit = _normalise_to_peak(0.62 * grain_coarse + 0.55 * grain_fine)
    grain_ratio = (GRAIN_TO_STRUCTURE_RATIO_MIN
                   + (GRAIN_TO_STRUCTURE_RATIO_MAX - GRAIN_TO_STRUCTURE_RATIO_MIN)
                   * law.saturate(spec.quality))
    grain_amplitude = BEDDING_RECESS_M * grain_ratio
    grain = grain_unit * grain_amplitude

    # Harder laminae hold coarser, better-cemented grain; soft clay laminae weather
    # smoother. Modulating the grain by competence is a material fact, and it is also what
    # stops the grain field from being a uniform overlay -- section 1 rejects "generic
    # grunge", and grunge is precisely a noise field that ignores what it sits on.
    grain = grain * (0.55 + 0.65 * lamina.hardness)

    # --- dissolution vugs, clustered in porous laminae, power-law sized ----------
    # THREE THINGS CHANGED HERE AND THEY ARE ALL THE SAME REJECTION.
    #  1. DEPTH SCALES WITH RADIUS instead of every vug getting the 0.011 m witness. A big
    #     cavity is deep and a small one is shallow -- constant aspect ratio -- so the
    #     TYPICAL vug is now subordinate to bedding while the rarest largest one still
    #     reaches the witness depth. The witness is honoured where it is meaningful.
    #  2. RADII ARE POWER-LAW, not uniform. A uniform draw produced near-identical pits;
    #     real cavities span orders of magnitude.
    #  3. VUGS CLUSTER IN POROUS LAMINAE via lamina.porosity, so they appear in bands. The
    #     previous carbonate gate was too weak and too smooth to band them, and the field
    #     spread evenly over every bed -- which is why the tile read as pumice.
    pit_cells = max(2, int(round(spec.tile_m / law.GEOLOGY_PIT_WITNESS_M)))

    def pit_layer(cells: int, f1: np.ndarray, ids: np.ndarray) -> tuple:
        count = cells * cells + 1
        # Bounded Pareto radii: heavy tail, so a few cells host a large cavity.
        uniform = rng.random(count)
        span = (VUG_RADIUS_MIN ** -VUG_PARETO_ALPHA
                - VUG_RADIUS_MAX ** -VUG_PARETO_ALPHA)
        radii = np.power(VUG_RADIUS_MIN ** -VUG_PARETO_ALPHA - uniform * span,
                         -1.0 / VUG_PARETO_ALPHA)
        radius = radii[ids]
        present = (rng.random(count) < 0.62)[ids]
        shape = (1.0 - _smooth_step(0.0, 1.0, f1 / np.maximum(radius, 1e-6))) * present
        return shape, radius

    # TWO LAYERS AT COPRIME CELL COUNTS. A jittered-grid Worley keeps every centre inside
    # its own cell, so even at full jitter a residual lattice survives -- visible in an
    # earlier grazing preview as horizontal ROWS of pocks. Overlaying a second layer whose
    # cell count shares no factor with the first pushes the combined periodicity out to
    # their product, far larger than the tile, so no row structure can align.
    second_cells = max(2, pit_cells - 7)
    while second_cells > 2 and math.gcd(second_cells, pit_cells) != 1:
        second_cells -= 1
    pit_f1, _pit_f2, pit_id = periodic_worley(rng, resolution, pit_cells, jitter=1.0)
    pit_f1b, _f2b, pit_idb = periodic_worley(rng, resolution, second_cells, jitter=1.0)
    shape_a, radius_a = pit_layer(pit_cells, pit_f1, pit_id)
    shape_b, radius_b = pit_layer(second_cells, pit_f1b, pit_idb)

    # Porosity gate: dissolution needs connected pore space, which is a BED property.
    porous = _smooth_step(VUG_POROSITY_LOW, VUG_POROSITY_HIGH, lamina.porosity)
    shape_a = shape_a * porous
    shape_b = shape_b * porous

    # Per-layer depth from that layer's own radius, capped at the witness. Metres per cell
    # differs between layers, so radius must be converted before comparing.
    depth_a = np.minimum(VUG_MAX_DEPTH_M,
                         BEDDING_RECESS_M * VUG_TYPICAL_DEPTH_FRACTION
                         * (radius_a * (spec.tile_m / pit_cells)
                            / (0.25 * law.GEOLOGY_PIT_WITNESS_M)))
    depth_b = np.minimum(VUG_MAX_DEPTH_M,
                         BEDDING_RECESS_M * VUG_TYPICAL_DEPTH_FRACTION
                         * (radius_b * (spec.tile_m / second_cells)
                            / (0.25 * law.GEOLOGY_PIT_WITNESS_M)))
    pit_relief = -np.maximum(shape_a * depth_a, shape_b * depth_b)
    pit = np.maximum(shape_a, shape_b)

    # --- joints: bed confinement applied to the set built at the top -------------
    # BED-CONFINED JOINTING: a fracture propagates through brittle cemented laminae and
    # dies out in soft clay. Real, and it also breaks each trace into segments so it reads
    # as rock failing rather than as a line drawn on rock.
    joint = joint_trace * (0.18 + 0.82 * lamina.hardness)
    joint_relief = -JOINT_DEPTH_M * joint

    # --- bioclasts --------------------------------------------------------------
    shell_cells = max(2, int(round(spec.tile_m / 0.020)))
    shell_f1, _s2, shell_id = periodic_worley(rng, resolution, shell_cells)
    # Occupancy is decided per CELL from a hash of its id, so shells are sparse and
    # discrete rather than a thresholded noise field with ragged edges.
    occupied = (rng.random(shell_cells * shell_cells + 1)[shell_id]
                < SHELL_OCCUPANCY)
    # Per-cell size variation, or every fragment is the same disc.
    shell_radius = (0.16 + 0.30 * rng.random(shell_cells * shell_cells + 1))[shell_id]
    lens = (1.0 - _smooth_step(0.0, 1.0, shell_f1 / np.maximum(shell_radius, 1e-6)))
    lens = lens * occupied
    # Shells sit in the carbonate-bearing laminae and are absent from organic clay.
    shell = lens * _smooth_step(0.25, 0.75, lamina.carbonate)
    # SHELL HASH BANDS. Uniform scattering read as confetti -- evenly spaced bright specks
    # over the whole face. Bioclasts do not scatter uniformly: they concentrate in
    # storm-deposited shell hash layers PARALLEL TO BEDDING. A bedding-aligned gate puts them
    # in trains, which is both the real depositional pattern and what stops them reading as
    # sprinkles on a cake.
    shell_band = _normalise_to_peak(
        periodic_fbm(rng, resolution, spec.tile_m, coarsest_m=spec.tile_m * 0.5,
                     finest_m=0.05, beta=2.2, anisotropy=6.0,
                     anisotropy_axis="v")) * 0.5 + 0.5
    shell = shell * _smooth_step(0.42, 0.82, shell_band)
    shell_relief = SHELL_RELIEF_M * shell

    height = (recess + parting + spall_relief + gouge_relief + grain + pit_relief
              + joint_relief + shell_relief)
    height = height - float(height.mean())

    report = {
        "tileMetres": round(spec.tile_m, 6),
        "resolution": resolution,
        "metresPerPixel": round(mpp, 8),
        "laminaCount": int(lamina.count),
        "laminaThicknessRangeM": [round(float(lamina.thicknesses_m.min()), 5),
                                  round(float(lamina.thicknesses_m.max()), 5)],
        "laminaThicknessSumM": round(float(lamina.thicknesses_m.sum()), 6),
        "partingWidthM": round(max(PARTING_WIDTH_M,
                                   PARTING_WIDTH_PIXEL_FLOOR * mpp), 6),
        "grainCoarsestM": round(coarsest_m, 6),
        "grainFinestM": round(finest_m, 6),
        "grainAnisotropy": anisotropy,
        "grainFabricFloorM": round(fabric_floor_m, 6),
        "grainBandSplit":
            "coarse band {c:.4f}-{f:.4f} m anisotropic {a} (depositional fabric); fine "
            "band {f:.4f}-{n:.4f} m ISOTROPIC (equant silt grains). A single "
            "anisotropic band down to the pixel floor read as woven fabric under "
            "grazing light.".format(c=coarsest_m, f=fabric_floor_m, a=anisotropy,
                                    n=finest_m),
        "grainAmplitudeM": round(grain_amplitude, 6),
        "grainToStructureRatio": round(grain_ratio, 5),
        "grainAmplitudeBasis":
            "BEDDING_RECESS_M * grainToStructureRatio. NOT rock.py's "
            "amplitude/wavelength slope law ({a}-{b}), which is calibrated against BED "
            "relief 0.055-0.34 m and would put grain at {c:.4f} m -- above the lamina "
            "relief it decorates. See the constant's comment.".format(
                a=MESH_GRAIN_SLOPE_MIN, b=MESH_GRAIN_SLOPE_MAX,
                c=coarsest_m * MESH_GRAIN_SLOPE_MAX),
        "pitCells": pit_cells,
        "pitSecondLayerCells": second_cells,
        "pitWavelengthM": round(spec.tile_m / pit_cells, 5),
        "pitWitnessDepthM": law.GEOLOGY_PIT_DEPTH_M,
        "pitWitnessRole": "depth of the RAREST LARGEST vug, not of every vug; depth scales "
                          "with radius at constant aspect ratio",
        "pitDepthRangeM": [round(float(np.min(np.minimum(depth_a, depth_b))), 6),
                           round(float(np.max(np.maximum(depth_a, depth_b))), 6)],
        "pitRadiusDistribution": "bounded Pareto alpha={a}, radii {lo}-{hi} cell units"
                                 .format(a=VUG_PARETO_ALPHA, lo=VUG_RADIUS_MIN,
                                         hi=VUG_RADIUS_MAX),
        "pitPorosityGate": [VUG_POROSITY_LOW, VUG_POROSITY_HIGH],
        "pitCoverage": round(float((pit > 0.5).mean()), 5),
        "jointGenerator": "sparse conjugate trace set (NOT a Worley partition)",
        "jointTraceCount": JOINT_TRACE_COUNT,
        "jointStressAzimuthDeg": JOINT_STRESS_AZIMUTH_DEG,
        "jointConjugateDeg": JOINT_CONJUGATE_DEG,
        "jointTraces": joint_traces,
        "jointWidthM": JOINT_WIDTH_M,
        "jointBedConfined": True,
        "jointCoverage": round(float((joint > 0.5).mean()), 5),
        "shellCoverage": round(float((shell > 0.5).mean()), 5),
        "heightRangeM": [round(float(height.min()), 6), round(float(height.max()), 6)],
        "heightPeakToPeakM": round(float(height.max() - height.min()), 6),
        "termAmplitudesM": {
            "beddingRecess": round(float(recess.max() - recess.min()), 6),
            "partingIncision": round(float(parting.max() - parting.min()), 6),
            "grain": round(float(grain.max() - grain.min()), 6),
            "dissolutionVugs": round(float(pit_relief.max() - pit_relief.min()), 6),
            "joints": round(float(joint_relief.max() - joint_relief.min()), 6),
            "bioclasts": round(float(shell_relief.max() - shell_relief.min()), 6),
            "spallScars": round(float(spall_relief.max() - spall_relief.min()), 6),
            "truncationGouge": round(float(gouge_relief.max() - gouge_relief.min()), 6),
        },
        "spallCoverage": round(float((lamina.spall > 0.5).mean()), 5),
        "spallScarCount": SPALL_SCAR_COUNT,
        "truncationCount": TRUNCATION_COUNT,
        "spallGeometry": "angular: top/bottom edges snapped to real parting positions, one lateral edge clipped to an actual joint line, opposite edge parallel",
        "structuralExtent": measure_structural_extent(lamina, joint, spec.tile_m),
        # THE HIERARCHY IS PUBLISHED AS A RANKING so an inversion is visible in the manifest
        # instead of only in a render. The family was rejected once for shipping
        # vugs 11.0 / grain 8.2 / recess 7.3 / joints 4.5 / parting 3.5 / bioclasts 2.2 mm --
        # numerically inverted for a laminated rock, with bedding fifth. Every subordinate
        # term is now a declared FRACTION of BEDDING_RECESS_M, so the ordering is structural.
        "reliefHierarchy": {
            "primary": "beddingRecess",
            "basisM": BEDDING_RECESS_M,
            "declaredFractions": {
                "partingIncision": PARTING_DEPTH_FRACTION,
                "joints": JOINT_DEPTH_FRACTION,
                "dissolutionVugsTypical": VUG_TYPICAL_DEPTH_FRACTION,
                "bioclasts": SHELL_RELIEF_FRACTION,
                "grain": round(grain_ratio, 5),
            },
            # RANKED BY AREA-WEIGHTED RMS, NOT BY PEAK-TO-PEAK, and the choice of statistic
            # is itself a correction. Peak-to-peak is a MAX statistic: one rare deep cavity
            # covering 0.4 percent of the tile put the vug term at rank two while bedding,
            # which displaces the whole surface, ranked below it. That is the same error as
            # judging a tile seam from one row-pair against a population mean -- an extreme
            # compared against a distribution. "Primary relief" means what dominates the
            # surface READ, so the ranking uses RMS, which weights each term by how much of
            # the surface it actually displaces. Peak-to-peak stays published beside it,
            # because the extremes are what the witness depths govern.
            "termRmsM": {name: round(float(np.sqrt((term ** 2).mean())), 6)
                         for name, term in (("beddingRecess", recess),
                                            ("partingIncision", parting),
                                            ("grain", grain),
                                            ("dissolutionVugs", pit_relief),
                                            ("joints", joint_relief),
                                            ("bioclasts", shell_relief),
                                            ("spallScars", spall_relief),
                                            ("truncationGouge", gouge_relief))},
            "rankedByRms": [
                name for name, _v in sorted(
                    (("beddingRecess", float(np.sqrt((recess ** 2).mean()))),
                     ("partingIncision", float(np.sqrt((parting ** 2).mean()))),
                     ("grain", float(np.sqrt((grain ** 2).mean()))),
                     ("dissolutionVugs", float(np.sqrt((pit_relief ** 2).mean()))),
                     ("joints", float(np.sqrt((joint_relief ** 2).mean()))),
                     ("bioclasts", float(np.sqrt((shell_relief ** 2).mean()))),
                     ("spallScars", float(np.sqrt((spall_relief ** 2).mean()))),
                     ("truncationGouge",
                      float(np.sqrt((gouge_relief ** 2).mean())))),
                    key=lambda kv: -kv[1])
            ],
            "rankedByPeakToPeak": [
                name for name, _value in sorted(
                    (("beddingRecess", float(recess.max() - recess.min())),
                     ("partingIncision", float(parting.max() - parting.min())),
                     ("grain", float(grain.max() - grain.min())),
                     ("dissolutionVugs", float(pit_relief.max() - pit_relief.min())),
                     ("joints", float(joint_relief.max() - joint_relief.min())),
                     ("bioclasts", float(shell_relief.max() - shell_relief.min()))),
                    key=lambda kv: -kv[1])
            ],
            "requirement": "beddingRecess and partingIncision must occupy the top two RMS "
                           "ranks on a laminated sedimentary family",
        },
        "bandCeilingM": law.GEOLOGY_TEXTURE_BAND_CEILING_M,
        "bandCeilingReason":
            "the finest wavelength a boulder's sculpt lattice can represent "
            "(law.GEOLOGY_MESH_FINEST_WAVELENGTH_M). Nothing in this tile is coarser, so "
            "the texture never competes with relief the mesh already carries.",
    }

    return HeightField(height_m=height, lamina=lamina, grain=grain_unit, pit=pit,
                       joint=joint, shell=shell, metres_per_pixel=mpp, report=report)


# ===========================================================================
# Step 4 of section 10: fields MEASURED off the surface
# ===========================================================================
# This block is the answer to playbook section 5's central rule. Every field below is a
# differential or integral operator applied to the height field -- none of them is a noise
# lookup, and none of them is a rescaled copy of another. That is what lets the channels
# downstream be independent (section 9's channel-independence gate) while all describing
# the same rock.
#
# ORIENTATION CONTRACT, stated once because every sign error below is silent:
#   * row 0 is the TOP of the image; V increases UPWARD, so d/dV = -d/drow;
#   * column 0 is the LEFT; U increases with the column index;
#   * gravity points toward increasing row index.
# ``_selftest_orientation`` at the bottom of this module asserts all three against a
# synthetic bump whose answers are known analytically.


@dataclass
class SurfaceFields:
    """Measured differential geometry of the height field."""

    slope_u: np.ndarray        # dh/dU, dimensionless
    slope_v: np.ndarray        # dh/dV, dimensionless
    normal: np.ndarray         # (h, w, 3) unit tangent-space normal, +Z out
    convex: np.ndarray         # 0..1 convex mean curvature
    concave: np.ndarray        # 0..1 concave mean curvature
    occlusion: np.ndarray      # 0..1, 1 = fully open
    accumulation: np.ndarray   # 0..1 downward-transported deposit
    upward: np.ndarray         # 0..1 how much the microfacet faces +V (a shelf)
    report: dict = field(default_factory=dict)


def surface_gradient(height_m: np.ndarray, metres_per_pixel: float) -> tuple:
    """Central-difference gradient in true metres per metre, wrapped.

    Wrapping with ``np.roll`` rather than an edge-clamped difference is what keeps every
    derived field periodic. An edge-clamped gradient produces a one-pixel band of wrong
    slope at the tile boundary, which survives into the normal map and shows up as a hard
    line once tiled -- the seam gate would catch it only if it happened to be measured on
    the normal map rather than on the height.
    """
    d_col = (np.roll(height_m, -1, axis=1) - np.roll(height_m, 1, axis=1)) / (
        2.0 * metres_per_pixel)
    d_row = (np.roll(height_m, -1, axis=0) - np.roll(height_m, 1, axis=0)) / (
        2.0 * metres_per_pixel)
    return d_col, -d_row  # dh/dU, dh/dV


def tangent_normal(slope_u: np.ndarray, slope_v: np.ndarray) -> np.ndarray:
    """Unit tangent-space normal from the slope field.

    ``n = normalize(-dh/dU, -dh/dV, 1)``. The negation is the whole content: the surface
    normal tilts AWAY from the uphill direction. Getting it wrong inverts the lighting
    response and is the single defect a normal map hides best, because a wrongly-signed
    normal map still looks like detail -- it just lights from the wrong side.
    """
    nx = -slope_u
    ny = -slope_v
    nz = np.ones_like(slope_u)
    length = np.sqrt(nx * nx + ny * ny + nz * nz)
    return np.stack([nx / length, ny / length, nz / length], axis=-1)


def mean_curvature(slope_u: np.ndarray, slope_v: np.ndarray,
                   metres_per_pixel: float) -> np.ndarray:
    """Divergence of the unit surface gradient: the mean-curvature operator.

    This is playbook section 5's "Curvature maps from mesh normals" applied to the surface
    the height field defines. It is the divergence of the NORMALISED gradient rather than a
    bare Laplacian, because the bare Laplacian's magnitude scales with slope and would
    report a steep flat ramp as curved.

    Sign: at a local maximum the gradient points inward from every side, so its divergence
    is negative. Convexity is therefore ``-divergence``, which
    ``_selftest_orientation`` checks against a Gaussian bump.
    """
    denom = np.sqrt(1.0 + slope_u * slope_u + slope_v * slope_v)
    fu = slope_u / denom
    fv = slope_v / denom
    d_fu_du = (np.roll(fu, -1, axis=1) - np.roll(fu, 1, axis=1)) / (
        2.0 * metres_per_pixel)
    d_fv_dv = -(np.roll(fv, -1, axis=0) - np.roll(fv, 1, axis=0)) / (
        2.0 * metres_per_pixel)
    return d_fu_du + d_fv_dv


def horizon_occlusion(height_m: np.ndarray, metres_per_pixel: float, *,
                      radius_m: float, azimuths: int = 16, steps: int = 12
                      ) -> np.ndarray:
    """Cosine-weighted ambient occlusion by horizon search over the height field.

    A REAL occlusion integral, not a darkened copy of the height. For each of ``azimuths``
    directions the maximum elevation angle of the horizon is found by marching outward;
    the unoccluded fraction of that azimuthal slice under a cosine-weighted hemisphere is
    ``cos^2(theta) = 1 - sin^2(theta)``, and the result is the mean over azimuths.

    THE RADIUS IS THE IMPORTANT ARGUMENT. It is bounded by
    ``law.GEOLOGY_TEXTURE_BAND_CEILING_M`` because the MESH already carries a ray-traced
    Cycles AO bake in vertex-colour B over a 0.35 m gather distance
    (``vertexcolor.bake_ambient_occlusion``). If this integral reached the same distance,
    the two would describe the same cavities and the runtime would multiply one occlusion
    by another -- darkening every crevice twice, which is the "using darkness to hide
    missing work" failure ``3dmodel.md`` bans. Bounded at the band ceiling, the texture
    occludes only features finer than anything the mesh can represent, so the two are
    complementary rather than duplicated.

    Marching with integer pixel offsets via ``np.roll`` keeps the sampling periodic, so
    the occlusion field wraps as cleanly as the height field it reads.
    """
    radius_px = max(3.0, radius_m / metres_per_pixel)
    # Geometric step spacing: near samples resolve the small features that dominate the
    # horizon, far samples only need to catch large ones.
    offsets = np.unique(np.rint(np.geomspace(1.5, radius_px, steps)).astype(int))
    offsets = offsets[offsets >= 1]

    total = np.zeros_like(height_m)
    for a in range(azimuths):
        angle = 2.0 * np.pi * a / azimuths
        dir_row = -math.sin(angle)   # -sin because +V is -row
        dir_col = math.cos(angle)
        tangent = np.zeros_like(height_m)
        for step in offsets:
            shift_row = int(round(dir_row * step))
            shift_col = int(round(dir_col * step))
            if shift_row == 0 and shift_col == 0:
                continue
            sample = np.roll(np.roll(height_m, -shift_row, axis=0),
                             -shift_col, axis=1)
            distance = math.hypot(shift_row, shift_col) * metres_per_pixel
            tangent = np.maximum(tangent, (sample - height_m) / distance)
        sin_sq = (tangent * tangent) / (1.0 + tangent * tangent)
        total += sin_sq
    return np.clip(1.0 - total / float(azimuths), 0.0, 1.0)


# Transport constants for the accumulation field. THE CAPTURE RATE DECIDES WHETHER THIS
# FIELD IS DIRECTIONAL AT ALL, which the orientation self-test caught rather than a review.
#
# First attempt used capture = 0.10 + 0.55*shelf + 0.35*concave. On a tile whose relief is
# small, almost every pixel counts as "facing up", so capture sat near 0.65 and material
# was stripped out of the flow within about 1.5 rows. The deposit then equalled the local
# source everywhere and the measured asymmetry across a feature was 1.00 -- a field that
# looked plausible and transported nothing. A drip that travels one pixel is not a drip.
#
# A streak on a 1.25 m tile should run some visible fraction of it, call it 0.15-0.35 m,
# which at 2048 is 250-570 rows. That fixes the base capture rate at roughly 1/400.
CAPTURE_BASE = 0.0025
CAPTURE_POCKET = 0.25
CAPTURE_CAVITY = 0.02
ACCUMULATION_DECAY = 0.998
ACCUMULATION_SPREAD = 0.30


def gravity_accumulation(height_m: np.ndarray, normal: np.ndarray,
                         concave: np.ndarray, *, passes: int = 3) -> np.ndarray:
    """Directional accumulation field along gravity: playbook section 5's drip term.

    "Directional accumulation fields for drips, soot, rust trails, sediment settling, and
    waterline marks."

    Transport, not a vertical smear of noise -- the difference between a streak that starts
    somewhere and a streak that is decoration. The model is:

      * a CATCHMENT is a pixel that is both concave and facing up. That is where water
        collects, and it is the only place material is released. Sources are therefore
        sparse and semantic instead of covering the tile;
      * material is TRANSPORTED down-tile row by row with a small per-row loss;
      * it is CAPTURED quickly in pockets and slowly on open faces, so a streak survives
        hundreds of rows on a wall and pools where the surface actually holds it;
      * a little LATERAL SPREAD each row, because a rivulet widens as it runs.

    The scan runs several times carrying the last row's flow back into the first, which is
    what makes the field periodic. A single pass starves the top rows of upstream supply
    and leaves a bright band across the top of every tile.
    """
    rows = height_m.shape[0]
    upward = np.clip(normal[:, :, 2], 0.0, 1.0)
    pocket = np.power(upward, 8.0) * concave
    source = pocket
    capture = np.clip(CAPTURE_BASE + CAPTURE_POCKET * pocket
                      + CAPTURE_CAVITY * concave, 0.0, 1.0)

    flow = np.zeros(height_m.shape[1], dtype=np.float64)
    deposit = np.zeros_like(height_m)
    for _pass in range(max(1, passes)):
        deposit = np.zeros_like(height_m)
        for row in range(rows):
            flow = flow * ACCUMULATION_DECAY + source[row]
            flow = ((1.0 - ACCUMULATION_SPREAD) * flow
                    + 0.5 * ACCUMULATION_SPREAD * (np.roll(flow, 1) + np.roll(flow, -1)))
            taken = flow * capture[row]
            deposit[row] = taken
            flow = flow - taken
    peak = float(np.percentile(deposit, 99.0))
    return np.clip(deposit / peak, 0.0, 1.0) if peak > 1e-12 else deposit


def measure_surface(height: HeightField) -> SurfaceFields:
    """Run every measured operator over the height field and report its statistics."""
    mpp = height.metres_per_pixel
    slope_u, slope_v = surface_gradient(height.height_m, mpp)
    normal = tangent_normal(slope_u, slope_v)

    curvature = mean_curvature(slope_u, slope_v, mpp)
    convexity = -curvature
    # Normalised by a robust percentile of the magnitude so convex/concave land in 0..1
    # without an arbitrary gain constant. The percentile is reported, because a
    # normalisation the reader cannot see is a hidden tuning knob.
    scale = float(np.percentile(np.abs(convexity), 99.0))
    scale = scale if scale > 1e-9 else 1.0
    convex = np.clip(convexity / scale, 0.0, 1.0)
    concave = np.clip(-convexity / scale, 0.0, 1.0)

    occlusion = horizon_occlusion(height.height_m, mpp,
                                  radius_m=law.GEOLOGY_TEXTURE_BAND_CEILING_M)
    accumulation = gravity_accumulation(height.height_m, normal, concave)
    upward = np.clip(normal[:, :, 2], 0.0, 1.0)

    slope_deg = np.degrees(np.arctan(np.sqrt(slope_u ** 2 + slope_v ** 2)))
    report = {
        "gradientUnits": "metres per metre, central difference, wrapped",
        "meanSlopeDeg": round(float(slope_deg.mean()), 4),
        "p95SlopeDeg": round(float(np.percentile(slope_deg, 95)), 4),
        "maxSlopeDeg": round(float(slope_deg.max()), 4),
        "curvatureOperator":
            "divergence of the unit surface gradient (mean curvature); convexity is its "
            "negation, verified against a Gaussian bump in _selftest_orientation",
        "curvatureNormalisationPercentile": 99.0,
        "curvatureNormalisationValue": round(scale, 5),
        "convexCoverage": round(float((convex > 0.25).mean()), 5),
        "concaveCoverage": round(float((concave > 0.25).mean()), 5),
        "occlusionOperator":
            "horizon-angle integral, 16 azimuths x 12 geometric radial steps, "
            "cosine-weighted hemisphere, wrapped sampling",
        "occlusionRadiusM": law.GEOLOGY_TEXTURE_BAND_CEILING_M,
        "occlusionRadiusReason":
            "bounded at the texture band ceiling so it cannot duplicate the mesh's own "
            "0.35 m Cycles AO bake in vertex-colour B",
        "occlusionRange": [round(float(occlusion.min()), 5),
                           round(float(occlusion.max()), 5)],
        "occlusionMean": round(float(occlusion.mean()), 5),
        "accumulationOperator":
            "gravity-directed transport scan: release on upward shelves, decay per row, "
            "capture on flat/concave, lateral spread, 3 wrapped passes",
        "accumulationMean": round(float(accumulation.mean()), 5),
        "accumulationCoverage": round(float((accumulation > 0.25).mean()), 5),
    }
    return SurfaceFields(slope_u=slope_u, slope_v=slope_v, normal=normal,
                         convex=convex, concave=concave, occlusion=occlusion,
                         accumulation=accumulation, upward=upward, report=report)


# ---------------------------------------------------------------------------
# Orientation self-test  --  the only defence against a silent sign error
# ---------------------------------------------------------------------------
# Every operator above has a sign that is invisible in the output. A wrongly-signed normal
# map still looks like detail; an inverted curvature still puts wear somewhere; a
# gravity field pointing up still makes streaks. ``AGENTS.md`` ``[RULE] Never Trust
# Automated Assertions Alone`` cuts both ways here: the way to trust these operators is to
# run them on a shape whose answers are known analytically and check the numbers.


def _gaussian_feature(resolution: int, amplitude_m: float, sigma_px: float
                      ) -> np.ndarray:
    """A single centred Gaussian bump, wrapped. Positive amplitude = hill, negative = pit."""
    axis = np.arange(resolution) - resolution // 2
    dy, dx = np.meshgrid(axis, axis, indexing="ij")
    return amplitude_m * np.exp(-(dx * dx + dy * dy) / (2.0 * sigma_px * sigma_px))


def selftest_orientation(resolution: int = 192, verbose: bool = True) -> list:
    """Assert the orientation contract. Returns a list of ``(name, passed, detail)``."""
    results = []
    mpp = 1.25 / resolution
    centre = resolution // 2

    # --- hill: curvature sign and normal green channel ------------------------
    hill = _gaussian_feature(resolution, 0.03, resolution / 12.0)
    su, sv = surface_gradient(hill, mpp)
    normal = tangent_normal(su, sv)
    curvature = mean_curvature(su, sv, mpp)
    convexity_peak = float(-curvature[centre, centre])
    results.append((
        "convexity positive at a hill summit", convexity_peak > 0.0,
        "-divergence at peak = {v:.4f}".format(v=convexity_peak)))

    # The flank ABOVE the summit in V is the low-row side. Its normal tilts toward +V, so
    # the green channel must read above 0.5 there and below 0.5 on the other flank.
    green = normal[:, :, 1] * 0.5 + 0.5
    flank = int(resolution / 12.0)
    green_up = float(green[centre - flank, centre])
    green_down = float(green[centre + flank, centre])
    results.append((
        "normal green channel is OpenGL (+Y up)",
        green_up > 0.52 and green_down < 0.48,
        "upper-V flank green={a:.4f} (want >0.52), lower-V flank green={b:.4f} "
        "(want <0.48)".format(a=green_up, b=green_down)))

    results.append((
        "normal is unit length",
        bool(np.allclose(np.linalg.norm(normal, axis=-1), 1.0, atol=1e-6)),
        "max deviation {d:.2e}".format(
            d=float(np.abs(np.linalg.norm(normal, axis=-1) - 1.0).max()))))

    # --- pit: occlusion must darken a cavity, not a bump ----------------------
    # THE TEST FEATURE MUST FIT INSIDE THE AO SEARCH RADIUS. The first version used
    # sigma = resolution/12, which at this tile scale is 0.104 m -- wider than the 0.087 m
    # search radius, so the march never reached the rim and reported AO 0.990 in a 30 mm
    # pit. The operator was right and the probe was too wide, which is worth recording
    # because the same mistake in the other direction would have looked like a passing gate.
    pit = _gaussian_feature(resolution, -0.03, resolution / 32.0)
    pit_ao = horizon_occlusion(pit, mpp, radius_m=0.087)
    hill_ao = horizon_occlusion(hill, mpp, radius_m=0.087)
    ao_in_pit = float(pit_ao[centre, centre])
    ao_on_hill = float(hill_ao[centre, centre])
    ao_far = float(pit_ao[2, 2])
    results.append((
        "occlusion is cavity-biased",
        ao_in_pit < ao_far - 0.02 and ao_on_hill > ao_in_pit,
        "pit centre={a:.4f}, hill summit={b:.4f}, flat far field={c:.4f}".format(
            a=ao_in_pit, b=ao_on_hill, c=ao_far)))

    # --- accumulation must be asymmetric in gravity ---------------------------
    su_p, sv_p = surface_gradient(pit, mpp)
    normal_p = tangent_normal(su_p, sv_p)
    curv_p = mean_curvature(su_p, sv_p, mpp)
    scale = float(np.percentile(np.abs(-curv_p), 99.0)) or 1.0
    concave_p = np.clip(curv_p / scale, 0.0, 1.0)
    accum = gravity_accumulation(pit, normal_p, concave_p)
    window = int(resolution / 8.0)
    below = float(accum[centre + window:centre + 3 * window, :].mean())
    above = float(accum[centre - 3 * window:centre - window, :].mean())
    results.append((
        "accumulation runs DOWN-tile only", below > above * 1.15,
        "below feature={a:.5f} vs above feature={b:.5f} (ratio {r:.2f})".format(
            a=below, b=above, r=below / max(above, 1e-9))))

    if verbose:
        for name, passed, detail in results:
            sys.stdout.write("{tag} {name}: {detail}\n".format(
                tag="PASS" if passed else "FAIL", name=name, detail=detail))
    return results


# ===========================================================================
# Pigment  --  read off the mandatory reference frame, not invented
# ===========================================================================
# These five values are carried from ``generators/rock.py`` :2692-2705, where they were
# read directly off ``Docs/mandatory if you work on systems that user sees .../
# nice_biome.webp`` per ``AGENTS.md`` ``[REQ] Direct Media Reading``. What that frame shows
# for geology, against the assumption that rock is warm grey:
#   - host rock is a COOL DARK SLATE with a blue-green cast, not brown and not mid-grey;
#   - upward-facing surfaces carry a strong green-olive algae mat, and that biological
#     colour is what makes the rock read, more than geometry density does;
#   - ledges carry warm ochre accents where growth clusters;
#   - undersides and cavities go deep teal, close to black.
#
# Reusing the same numbers is deliberate: the texture and the vertex-colour material must
# agree, or a rock's triplanar surface and its per-vertex staining will read as two
# different rocks at the blend boundary.
ROCK_SLATE = (0.096, 0.104, 0.113)
ROCK_ALGAE = (0.115, 0.196, 0.082)
ROCK_OCHRE = (0.243, 0.150, 0.055)
ROCK_FRESH = (0.300, 0.290, 0.268)

# Lithology end members for the lamina stack. Playbook section 4 asks sediment for
# "layered tan/gray/black deposits", so the stack interpolates between an organic-rich
# black mudstone, the cool grey siltstone above, and a pale carbonate-cemented tan.
LITHOLOGY_MUDSTONE = (0.048, 0.054, 0.062)
LITHOLOGY_SILTSTONE = ROCK_SLATE
LITHOLOGY_CALCAREOUS = (0.168, 0.156, 0.132)

# Bioclast carbonate: a broken shell cross-section is much paler than any matrix.
BIOCLAST_CARBONATE = (0.420, 0.400, 0.360)

# Cavity grime. A SUBSTANCE with its own albedo -- organic silt washed into hollows -- not
# a shading term. The distinction is the whole of section 9's "no baked directional
# highlights" gate: dark pigment in a cavity is material truth, a multiplied occlusion
# term in albedo is baked lighting, and they are trivially confused.
CAVITY_GRIME = (0.038, 0.042, 0.034)

# Roughness of the dry primary rock, matching ``rock.py`` MATERIAL_ROLES' 0.78 for
# MAT_Geology_Primary so the texture and the shared material do not disagree.
DRY_ROCK_ROUGHNESS = 0.78

# Pyrite. Authigenic framboids grow in organic-rich, oxygen-poor mud, so they belong in
# the dark laminae and nowhere else. Partially oxidised, hence below 1.0.
PYRITE_METALLIC = 0.72
PYRITE_WAVELENGTH_M = 0.015
PYRITE_OCCUPANCY = 0.10


def _lerp_rgb(base: np.ndarray, colour: tuple, weight: np.ndarray) -> np.ndarray:
    """Blend a flat pigment into an RGB field by a per-pixel weight."""
    target = np.asarray(colour, dtype=np.float64).reshape(1, 1, 3)
    return base + (target - base) * weight[:, :, None]


@dataclass
class ChannelSet:
    """The derived PBR channels, all linear, all 0..1."""

    base_color: np.ndarray     # (h, w, 3) linear albedo
    metallic: np.ndarray
    occlusion: np.ndarray
    smoothness: np.ndarray
    height01: np.ndarray
    normal: np.ndarray         # (h, w, 3) unit
    ore_mask: np.ndarray       # where metal is ALLOWED to exist, for gate 6
    derivation: dict = field(default_factory=dict)


def derive_channels(spec: GeologyTextureSpec, height: HeightField,
                    surface: SurfaceFields, rng: np.random.Generator) -> ChannelSet:
    """Assemble every channel as a stated function of a measured field.

    Section 5: "These fields must be mixed by material semantics, not blind
    multiplication. Edge wear belongs on convex curvature. Grime belongs in concave
    cavities and downward streaks. Roughness must follow material state, not random
    color."

    Read the ``derivation`` dict this returns alongside the code: it is the manifest's
    record of which mesh-derived field drives which channel, and it is written from the
    same expressions rather than restated by hand.
    """
    resolution = spec.resolved_resolution()
    lamina = height.lamina

    # --- masks with a physical name ------------------------------------------
    # Edge wear: the convex arris a current polishes and a spall exposes. Section 5 puts
    # this on convex curvature and nowhere else.
    edge_wear = surface.convex

    # Cavity grime: concavity, deepened by the two mask families that ARE cavities. AO is
    # deliberately NOT an input -- see CAVITY_GRIME.
    grime = np.clip(0.75 * surface.concave + 0.45 * height.pit
                    + 0.55 * height.joint, 0.0, 1.0)

    # Algae mat: biological, so it needs light and a surface to sit on. Upward-facing and
    # fed by the accumulation field.
    algae = np.clip(surface.accumulation * np.power(surface.upward, 2.0) * 1.35,
                    0.0, 1.0)

    # Growth clusters: only the wettest, most fed ledges. Sparse by construction.
    ochre = _smooth_step(0.55, 0.95, surface.accumulation) * surface.upward

    # --- base colour ----------------------------------------------------------
    base = np.broadcast_to(
        np.asarray(LITHOLOGY_SILTSTONE, dtype=np.float64).reshape(1, 1, 3),
        (resolution, resolution, 3)).copy()
    base = _lerp_rgb(base, LITHOLOGY_MUDSTONE, lamina.organic)
    base = _lerp_rgb(base, LITHOLOGY_CALCAREOUS, lamina.carbonate * 0.85)
    # 0.55 not 0.9: at full strength the fragments read as high-contrast white specks
    # against the darker matrix rather than as weathered carbonate in rock.
    base = _lerp_rgb(base, BIOCLAST_CARBONATE, height.shell * 0.55)
    # Edge wear reveals fresh rock. Partial lerp at 0.55, the same reveal gain
    # ``rock.py`` :2793 uses for the vertex-colour R channel, so the two agree.
    base = _lerp_rgb(base, ROCK_FRESH, edge_wear * 0.55)
    base = _lerp_rgb(base, CAVITY_GRIME, grime * 0.62)
    base = _lerp_rgb(base, ROCK_ALGAE, algae * 0.80)
    base = _lerp_rgb(base, ROCK_OCHRE, ochre * 0.45)
    base_color = np.clip(base, 0.0, 1.0)

    # --- smoothness (MaskMap A) ----------------------------------------------
    # Material STATE, per section 5's last sentence. Every term is a substance or a
    # surface condition, and none of them is the height field.
    smoothness = np.full((resolution, resolution), 1.0 - DRY_ROCK_ROUGHNESS)
    smoothness = smoothness - 0.07 * lamina.softness       # clay weathers to a matte chalk
    smoothness = smoothness + 0.10 * lamina.carbonate      # cement polishes
    smoothness = smoothness + 0.08 * height.shell          # shell is glassier still
    smoothness = smoothness + 0.34 * algae                 # wet biofilm; matches rock.py
    smoothness = smoothness + 0.09 * edge_wear             # current-polished arris
    smoothness = smoothness - 0.06 * height.pit            # vug interiors are corroded
    smoothness = np.clip(smoothness, 0.02, 0.85)

    # --- metallic (MaskMap R) -------------------------------------------------
    # Section 9: "Metallic mask matches only real exposed metal or ore." For a sedimentary
    # shelf rock the only honest metal is authigenic pyrite, which means three conditions
    # must hold at once, and the ORE MASK records them so the gate can measure the claim
    # instead of trusting it.
    pyrite_cells = max(2, int(round(spec.tile_m / PYRITE_WAVELENGTH_M)))
    pyrite_f1, _pf2, pyrite_id = periodic_worley(rng, resolution, pyrite_cells)
    occupied = rng.random(pyrite_cells * pyrite_cells + 1)[pyrite_id] < PYRITE_OCCUPANCY
    framboid = (1.0 - _smooth_step(0.10, 0.30, pyrite_f1)) * occupied
    in_reducing_mud = _smooth_step(0.55, 0.95, lamina.organic)
    sheltered = (1.0 - edge_wear) * (0.30 + 0.70 * surface.concave)
    ore_mask = np.clip(in_reducing_mud * sheltered, 0.0, 1.0)
    metallic = np.clip(PYRITE_METALLIC * framboid * ore_mask, 0.0, 1.0)

    # --- occlusion (MaskMap G) -----------------------------------------------
    occlusion = surface.occlusion

    # --- height ---------------------------------------------------------------
    lo, hi = height.range_m
    span = max(hi - lo, 1e-9)
    height01 = (height.height_m - lo) / span

    derivation = {
        "baseColor": {
            "role": law.TEXTURE_ROLE_BASECOLOR,
            "colorSpace": "sRGB encoded on write, authored linear",
            "terms": [
                "lithology: siltstone lerped to mudstone by lamina.organic and to "
                "calcareous tan by lamina.carbonate -- section 4's 'layered tan/gray/black'",
                "bioclasts: pale carbonate where the shell lens mask is set",
                "edge wear: lerp to ROCK_FRESH by MEASURED CONVEX CURVATURE * 0.55",
                "cavity grime: lerp to CAVITY_GRIME by concavity + vug + joint masks",
                "algae mat: lerp to ROCK_ALGAE by accumulation * upward^2",
                "growth clusters: lerp to ROCK_OCHRE by the top of the accumulation field",
            ],
            "excludes": "the occlusion field is NOT an input; multiplying it in would be "
                        "baked lighting under section 9's albedo gate",
        },
        "normal": {
            "role": law.TEXTURE_ROLE_NORMAL,
            "source": "central-difference gradient of the height field in true metres, "
                      "encoded OpenGL (+Y up) as Unity samples it",
        },
        "metallic": {
            "role": law.TEXTURE_ROLE_MASK_URP + " R",
            "source": "authigenic pyrite framboids: a Worley spot field at "
                      "{w} m, gated on organic-rich laminae AND sheltered from "
                      "weathering (low convexity, concave-biased)".format(
                          w=PYRITE_WAVELENGTH_M),
            "oreMaskDeclared": True,
        },
        "occlusion": {
            "role": law.TEXTURE_ROLE_MASK_URP + " G",
            "source": "horizon-angle occlusion integral over the height field, radius "
                      "bounded at the texture band ceiling",
        },
        "smoothness": {
            "role": law.TEXTURE_ROLE_MASK_URP + " A",
            "source": "material state: dry-rock base {b:.2f}, minus clay softness, plus "
                      "carbonate cement, plus shell, plus wet algae biofilm, plus "
                      "convex polish, minus vug corrosion".format(
                          b=1.0 - DRY_ROCK_ROUGHNESS),
        },
        "unusedB": {
            "role": law.TEXTURE_ROLE_MASK_URP + " B",
            "source": "constant 0. The URP mask format does not read B -- "
                      "Hecton_ModuleHardSurfaceLit :349-353 decodes only r, g and a. "
                      "Writing data there would be data no shader can reach.",
        },
        "height": {
            "role": law.TEXTURE_ROLE_HEIGHT,
            "source": "the source relief, normalised over its own metre range",
            "rangeM": [round(lo, 6), round(hi, 6)],
        },
        "emission": {
            "role": law.TEXTURE_ROLE_EMISSION,
            "emitted": False,
            "reason": "playbook section 3 restricts emission to 'bioluminescence, "
                      "instrument glow, hot venting, energized equipment, or emergency "
                      "markings'. A sedimentary shelf rock is none of those, so the map is "
                      "OMITTED rather than shipped black -- an all-zero emission texture "
                      "is a binding and a VRAM cost for no signal.",
        },
        "maskCoverage": {
            "edgeWearAbove25": round(float((edge_wear > 0.25).mean()), 5),
            "grimeAbove25": round(float((grime > 0.25).mean()), 5),
            "algaeAbove25": round(float((algae > 0.25).mean()), 5),
            "ochreAbove25": round(float((ochre > 0.25).mean()), 5),
            "oreMaskAbove25": round(float((ore_mask > 0.25).mean()), 5),
            "metallicAbove10": round(float((metallic > 0.10).mean()), 5),
        },
    }

    return ChannelSet(base_color=base_color, metallic=metallic, occlusion=occlusion,
                      smoothness=smoothness, height01=height01, normal=surface.normal,
                      ore_mask=ore_mask, derivation=derivation)


# ===========================================================================
# Step 5 of section 10: pack the maps
# ===========================================================================

@dataclass
class PackedMaps:
    """The encoded arrays, exactly as they will hit disk."""

    base_color: np.ndarray     # uint8 (h, w, 3), sRGB encoded
    normal: np.ndarray         # uint8 (h, w, 3), linear, OpenGL +Y up
    mask_urp: np.ndarray       # uint8 (h, w, 4), R metal G occlusion B unused A smoothness
    arm: np.ndarray            # uint8 (h, w, 3), R AO G roughness B metal
    height: np.ndarray         # uint16 (h, w)
    roughness: np.ndarray      # float, kept for the gates
    layouts: dict = field(default_factory=dict)


def pack_maps(channels: ChannelSet) -> PackedMaps:
    """Encode the channel set into the two packed layouts that actually ship.

    BOTH LAYOUTS ARE EMITTED AND THEY ARE NOT INTERCHANGEABLE. ``law.py`` carries the
    proof: ``_MaskMap_UnityURP`` is ``R metallic, G occlusion, B unused, A smoothness``,
    bit-exact against ``Hecton_ModuleHardSurfaceLit`` :71 and :349-353, while
    ``_ARM_AO_Rough_Metal`` is ``R ambient occlusion, G roughness, B metal``. Binding one
    where the other is expected puts occlusion in the metallic slot.

    ARM IS A REPACK, NOT NEW DATA, and the manifest must say so or section 9's
    channel-independence gate is being read wrong. ARM's R is bit-identical to the URP
    mask's G, ARM's B to the URP mask's R, and ARM's G is exactly ``1 - A``. Roughness and
    smoothness are the same quantity under Unity's own definition
    (``perceptualRoughness = 1 - perceptualSmoothness``), so a near-perfect negative
    correlation between them is CORRECT and is not a channel duplicated by accident. What
    the gate must check is independence WITHIN each map, where all three channels are
    genuinely different fields.
    """
    roughness = 1.0 - channels.smoothness

    base_color = quantise(linear_to_srgb(channels.base_color))

    # Normal encode. The stored value is the data, so this map must never see an sRGB
    # transform; it ships with sRGB false and BC5 per law.TEXTURE_IMPORT_SETTINGS.
    normal = quantise(channels.normal * 0.5 + 0.5)

    zeros = np.zeros(channels.metallic.shape, dtype=np.float64)
    mask_urp = np.stack([
        channels.metallic,
        channels.occlusion,
        zeros,
        channels.smoothness,
    ], axis=-1)

    arm = np.stack([
        channels.occlusion,
        roughness,
        channels.metallic,
    ], axis=-1)

    layouts = {
        law.TEXTURE_ROLE_MASK_URP: {
            "channels": list(law.MASKMAP_URP_CHANNELS),
            "shaderProof": "Hecton_ModuleHardSurfaceLit._MaskMap label :71, decode "
                           ":349-353 (metallic=.r, occlusionMap=.g, smoothness=.a)",
            "bUnused": True,
        },
        law.TEXTURE_ROLE_ARM: {
            "channels": list(law.ARM_CHANNELS)[:3],
            "relationToUrpMask":
                "REPACK of the same three fields: ARM.R == MaskMap.G (occlusion), "
                "ARM.B == MaskMap.R (metallic), ARM.G == 1 - MaskMap.A (roughness is "
                "1 - smoothness by Unity's definition). No new information.",
        },
        law.TEXTURE_ROLE_BASECOLOR: {"colorSpace": "sRGB", "channels": ["r", "g", "b"]},
        law.TEXTURE_ROLE_NORMAL: {"colorSpace": "linear",
                                  "convention": "OpenGL +Y up",
                                  "channels": ["x", "y", "z"]},
        law.TEXTURE_ROLE_HEIGHT: {"colorSpace": "linear", "bitDepth": 16,
                                  "channels": ["height"]},
    }

    return PackedMaps(base_color=base_color, normal=normal,
                      mask_urp=quantise(mask_urp), arm=quantise(arm),
                      height=quantise(channels.height01, bit_depth=16),
                      roughness=roughness, layouts=layouts)


# ===========================================================================
# Step 9 of section 10: the eleven acceptance gates
# ===========================================================================
# Playbook section 9 lists eleven tests in prose. Each one below is the numeric form, and
# every threshold comes from ``law.py`` rather than from a literal here.
#
# "If any gate fails, the texture family must not be saved into the production asset
# route. The bake may write a diagnostic artifact under Docs/AgentLogs or an editor-only
# quarantine folder, but it must not become a referenced runtime material."
#
# So this function's return value decides the destination directory, which is why it
# reports every number rather than a boolean.


@dataclass
class GateResult:
    name: str
    passed: bool
    measured: str
    threshold: str
    note: str = ""


def _pearson(a: np.ndarray, b: np.ndarray) -> float:
    x = np.asarray(a, dtype=np.float64).ravel()
    y = np.asarray(b, dtype=np.float64).ravel()
    x = x - x.mean()
    y = y - y.mean()
    denom = math.sqrt(float((x * x).sum()) * float((y * y).sum()))
    return float((x * y).sum() / denom) if denom > 1e-12 else 0.0


def seam_excess(array: np.ndarray) -> float:
    """Wrap step against the 99th percentile of ordinary interior steps.

    The numeric form of section 9's "2x2 tile seam check". Tiling the image and LOOKING for
    a line is the manual version; this asks whether the step across the wrap is
    distinguishable from a step anywhere else in the tile.

    WHY A PERCENTILE AND NOT A MEAN. The first version divided by the mean interior
    gradient and rejected a provably periodic field at 1.69 -- the wrap row happened to
    land next to a lamina contact, which is an ordinary event on a laminated rock. One
    sample compared against a population MEAN carries no information about whether that
    sample is unusual. Compared against the population's own 99th percentile it does: a
    real discontinuity sits above the maximum ordinary step, not merely above the average
    one. ``selftest_seam_gate`` proves the reformulated gate still fires on a
    deliberately non-periodic field.
    """
    data = np.asarray(array, dtype=np.float64)
    if data.ndim == 3:
        data = data.mean(axis=2)

    scores = []
    row_pairs = np.abs(np.diff(data, axis=0)).mean(axis=1)
    if row_pairs.size >= 4:
        reference = float(np.percentile(row_pairs, 99.0))
        if reference > 1e-12:
            scores.append(float(np.abs(data[0] - data[-1]).mean()) / reference)
    col_pairs = np.abs(np.diff(data, axis=1)).mean(axis=0)
    if col_pairs.size >= 4:
        reference = float(np.percentile(col_pairs, 99.0))
        if reference > 1e-12:
            scores.append(float(np.abs(data[:, 0] - data[:, -1]).mean()) / reference)
    return max(scores) if scores else 0.0


def _luminance(rgb_linear: np.ndarray) -> np.ndarray:
    return (0.2126 * rgb_linear[:, :, 0] + 0.7152 * rgb_linear[:, :, 1]
            + 0.0722 * rgb_linear[:, :, 2])


def _box_downsample(array: np.ndarray, factor: int = 2) -> np.ndarray:
    data = np.asarray(array, dtype=np.float64)
    h = (data.shape[0] // factor) * factor
    w = (data.shape[1] // factor) * factor
    data = data[:h, :w]
    if data.ndim == 3:
        return data.reshape(h // factor, factor, w // factor, factor, data.shape[2]
                            ).mean(axis=(1, 3))
    return data.reshape(h // factor, factor, w // factor, factor).mean(axis=(1, 3))


def _bc1_simulate(rgb_uint8: np.ndarray) -> np.ndarray:
    """Approximate block-compression round trip: 4x4 blocks, RGB565 endpoints, 4 levels.

    THIS IS BC1, AND THE FAMILY SHIPS BC7. BC1 is 4 bits per pixel with two endpoints and
    two interpolants; BC7 is 8 bits per pixel with per-block mode selection and is
    substantially better on every kind of content. So the PSNR measured here is a
    CONSERVATIVE LOWER BOUND on what the shipped format will do, and passing it is
    meaningful while failing it would not immediately condemn the map.

    It is also not Unity's encoder. Section 9's "compression preview" gate is only truly
    satisfied by the platform compressor, which lives behind the editor lock. Declared
    UNVERIFIED in the manifest rather than claimed.
    """
    data = np.asarray(rgb_uint8, dtype=np.float64)
    h = (data.shape[0] // 4) * 4
    w = (data.shape[1] // 4) * 4
    data = data[:h, :w, :3]
    blocks = data.reshape(h // 4, 4, w // 4, 4, 3).transpose(0, 2, 1, 3, 4)
    blocks = blocks.reshape(-1, 16, 3)

    def to565(colour: np.ndarray) -> np.ndarray:
        r = np.rint(colour[:, 0] / 255.0 * 31.0) / 31.0 * 255.0
        g = np.rint(colour[:, 1] / 255.0 * 63.0) / 63.0 * 255.0
        b = np.rint(colour[:, 2] / 255.0 * 31.0) / 31.0 * 255.0
        return np.stack([r, g, b], axis=1)

    c0 = to565(blocks.max(axis=1))
    c1 = to565(blocks.min(axis=1))
    palette = np.stack([c0, c1, (2.0 * c0 + c1) / 3.0, (c0 + 2.0 * c1) / 3.0], axis=1)
    distance = ((blocks[:, :, None, :] - palette[:, None, :, :]) ** 2).sum(axis=-1)
    index = distance.argmin(axis=-1)
    chosen = palette[np.arange(palette.shape[0])[:, None], index]

    out = chosen.reshape(h // 4, w // 4, 4, 4, 3).transpose(0, 2, 1, 3, 4)
    return out.reshape(h, w, 3)


def _psnr(reference: np.ndarray, test: np.ndarray) -> float:
    mse = float(((np.asarray(reference, dtype=np.float64)
                  - np.asarray(test, dtype=np.float64)) ** 2).mean())
    return 99.0 if mse < 1e-9 else 10.0 * math.log10(255.0 * 255.0 / mse)


def run_acceptance_gates(spec: GeologyTextureSpec, height: HeightField,
                         surface: SurfaceFields, channels: ChannelSet,
                         packed: PackedMaps) -> list:
    """All eleven gates from playbook section 9, each with its measured number."""
    gates = []
    base_linear = channels.base_color
    luma = _luminance(base_linear)

    # --- 1. 2x2 tile seam check ----------------------------------------------
    seams = {
        law.TEXTURE_ROLE_BASECOLOR: seam_excess(packed.base_color),
        law.TEXTURE_ROLE_NORMAL: seam_excess(packed.normal),
        law.TEXTURE_ROLE_MASK_URP: seam_excess(packed.mask_urp[:, :, [0, 1, 3]]),
        law.TEXTURE_ROLE_ARM: seam_excess(packed.arm),
        law.TEXTURE_ROLE_HEIGHT: seam_excess(packed.height),
    }
    worst_map = max(seams, key=lambda k: seams[k])
    gates.append(GateResult(
        "tile seam (2x2 wrap continuity)",
        max(seams.values()) <= law.TEXTURE_SEAM_EXCESS_MAX,
        "worst {m} excess {v:.4f}; ".format(m=worst_map, v=seams[worst_map])
        + ", ".join("{k} {v:.3f}".format(k=k, v=v) for k, v in seams.items()),
        "<= {t}".format(t=law.TEXTURE_SEAM_EXCESS_MAX),
        "wrap step vs the 99th percentile of ordinary interior steps. Every field is "
        "generated on a torus, so a value at or below 1.0 is structural, not tuned"))

    # --- 2. histogram sanity --------------------------------------------------
    clipped_low = float((packed.base_color == 0).any(axis=2).mean())
    clipped_high = float((packed.base_color == 255).any(axis=2).mean())
    clipped = max(clipped_low, clipped_high)
    gates.append(GateResult(
        "histogram sanity (no crushed base colour)",
        clipped <= law.TEXTURE_CLIPPED_FRACTION_MAX,
        "at 0: {a:.5f}, at 255: {b:.5f}".format(a=clipped_low, b=clipped_high),
        "<= {t}".format(t=law.TEXTURE_CLIPPED_FRACTION_MAX)))

    # --- 3. albedo luminance + no baked directional light ---------------------
    luma_lo, luma_hi = float(luma.min()), float(luma.max())
    in_band = (luma_lo >= law.TEXTURE_ALBEDO_LUMA_MIN
               and luma_hi <= law.TEXTURE_ALBEDO_LUMA_MAX)
    gates.append(GateResult(
        "albedo luminance range for URP",
        in_band,
        "{a:.4f}..{b:.4f} (mean {m:.4f})".format(a=luma_lo, b=luma_hi,
                                                 m=float(luma.mean())),
        "{lo}..{hi}".format(lo=law.TEXTURE_ALBEDO_LUMA_MIN,
                            hi=law.TEXTURE_ALBEDO_LUMA_MAX)))

    # The real baked-light test: if albedo encodes a lamp, its luminance correlates with
    # N.L for SOME direction. Sweep a hemisphere and take the worst case. This is the gate
    # that would catch an AO or a shading term multiplied into base colour, which is the
    # most common way a generated albedo goes wrong.
    normal = channels.normal
    worst_dir = 0.0
    worst_label = ""
    for elevation_deg in (15.0, 40.0, 70.0):
        for azimuth_index in range(8):
            azimuth = 2.0 * math.pi * azimuth_index / 8.0
            elevation = math.radians(elevation_deg)
            light = np.array([math.cos(elevation) * math.cos(azimuth),
                              math.cos(elevation) * math.sin(azimuth),
                              math.sin(elevation)])
            ndotl = np.clip(normal[:, :, 0] * light[0] + normal[:, :, 1] * light[1]
                            + normal[:, :, 2] * light[2], 0.0, 1.0)
            r = abs(_pearson(luma, ndotl))
            if r > worst_dir:
                worst_dir = r
                worst_label = "elev {e:.0f} az {a:.0f}".format(
                    e=elevation_deg, a=math.degrees(azimuth))
    gates.append(GateResult(
        "no baked directional lighting in albedo",
        worst_dir <= law.TEXTURE_ALBEDO_LIGHT_CORRELATION_MAX,
        "max |corr(luma, N.L)| = {v:.4f} over 24 light directions ({l})".format(
            v=worst_dir, l=worst_label),
        "<= {t}".format(t=law.TEXTURE_ALBEDO_LIGHT_CORRELATION_MAX),
        "a lamp baked into albedo would correlate with N.L for at least one direction"))

    # --- 4. normal map sanity -------------------------------------------------
    slope_deg = float(surface.report["meanSlopeDeg"])
    strength_ok = (law.TEXTURE_NORMAL_MEAN_SLOPE_MIN_DEG <= slope_deg
                   <= law.TEXTURE_NORMAL_MEAN_SLOPE_MAX_DEG)
    decoded_y = packed.normal[:, :, 1].astype(np.float64) / 255.0 * 2.0 - 1.0
    # SIGN, not linearity. See law.TEXTURE_NORMAL_GREEN_SIGN_AGREEMENT_MIN for why the
    # correlation form of this test was wrong.
    significant = np.abs(surface.slope_v) > law.TEXTURE_NORMAL_SIGN_SLOPE_FLOOR
    if significant.any():
        agreement = float((np.sign(decoded_y[significant])
                           == np.sign(-surface.slope_v[significant])).mean())
    else:
        agreement = 0.0
    sampled = float(significant.mean())
    flatness = float(np.std(packed.normal[:, :, [0, 1]].astype(np.float64) / 255.0))
    gates.append(GateResult(
        "normal strength, green convention, not flat",
        (strength_ok and agreement >= law.TEXTURE_NORMAL_GREEN_SIGN_AGREEMENT_MIN
         and flatness > 0.01),
        "mean slope {s:.3f} deg; green sign agreement {a:.5f} over {p:.3f} of pixels; "
        "XY std {f:.5f}".format(s=slope_deg, a=agreement, p=sampled, f=flatness),
        "{lo}..{hi} deg, green sign agreement >= {g}, XY std > 0.01".format(
            lo=law.TEXTURE_NORMAL_MEAN_SLOPE_MIN_DEG,
            hi=law.TEXTURE_NORMAL_MEAN_SLOPE_MAX_DEG,
            g=law.TEXTURE_NORMAL_GREEN_SIGN_AGREEMENT_MIN),
        "agreement near 1.0 proves OpenGL (+Y up); a DirectX-flipped map would score near "
        "0.0. The absolute convention is separately proven against a synthetic bump in "
        "selftest_orientation"))

    # --- 5. packed-channel independence --------------------------------------
    urp_pairs = {
        "R metallic vs G occlusion": _pearson(channels.metallic, channels.occlusion),
        "R metallic vs A smoothness": _pearson(channels.metallic, channels.smoothness),
        "G occlusion vs A smoothness": _pearson(channels.occlusion, channels.smoothness),
    }
    arm_pairs = {
        "R AO vs G roughness": _pearson(channels.occlusion, packed.roughness),
        "R AO vs B metal": _pearson(channels.occlusion, channels.metallic),
        "G roughness vs B metal": _pearson(packed.roughness, channels.metallic),
    }
    worst_pair = max(list(urp_pairs.items()) + list(arm_pairs.items()),
                     key=lambda kv: abs(kv[1]))
    gates.append(GateResult(
        "packed channel independence",
        abs(worst_pair[1]) <= law.TEXTURE_CHANNEL_CORRELATION_MAX,
        "worst |r| = {v:.4f} ({k}); URP ".format(v=abs(worst_pair[1]), k=worst_pair[0])
        + ", ".join("{k} {v:+.3f}".format(k=k, v=v) for k, v in urp_pairs.items())
        + "; ARM " + ", ".join("{k} {v:+.3f}".format(k=k, v=v)
                               for k, v in arm_pairs.items()),
        "|r| <= {t} within each packed map".format(
            t=law.TEXTURE_CHANNEL_CORRELATION_MAX),
        "measured WITHIN each map. ARM is a declared repack of the URP mask's three "
        "fields, so cross-map identity is by design and is recorded in the manifest"))

    # --- 6. metallic only on real metal or ore -------------------------------
    metal_mass = float(channels.metallic.sum())
    coverage = float((channels.metallic > 0.10).mean())
    inside_ore = (float((channels.metallic * (channels.ore_mask > 0.05)).sum())
                  / metal_mass) if metal_mass > 1e-9 else 1.0
    on_exposed = (float((channels.metallic * (surface.convex > 0.5)).sum())
                  / metal_mass) if metal_mass > 1e-9 else 0.0
    gates.append(GateResult(
        "metallic mask is ore-only",
        (coverage <= law.TEXTURE_METALLIC_COVERAGE_MAX
         and inside_ore >= law.TEXTURE_METALLIC_INSIDE_ORE_MASK_MIN),
        "coverage {c:.5f}; {i:.4f} of metallic mass inside the declared pyrite ore "
        "mask; {e:.4f} on weathered convex faces".format(
            c=coverage, i=inside_ore, e=on_exposed),
        "coverage <= {c}, inside-ore >= {i}".format(
            c=law.TEXTURE_METALLIC_COVERAGE_MAX,
            i=law.TEXTURE_METALLIC_INSIDE_ORE_MASK_MIN),
        "pyrite oxidises off exposed faces, so a low figure on convex curvature is the "
        "material behaving correctly"))

    # --- 7. roughness variation ----------------------------------------------
    rough_std = float(packed.roughness.std())
    gates.append(GateResult(
        "roughness variation supports material identity",
        rough_std >= law.TEXTURE_ROUGHNESS_STD_MIN,
        "std {s:.5f}, range {a:.4f}..{b:.4f}".format(
            s=rough_std, a=float(packed.roughness.min()),
            b=float(packed.roughness.max())),
        ">= {t}".format(t=law.TEXTURE_ROUGHNESS_STD_MIN)))

    # --- 8. AO is cavity-biased ----------------------------------------------
    ao_concave = _pearson(channels.occlusion, surface.concave)
    ao_height = _pearson(channels.occlusion, height.height_m)
    gates.append(GateResult(
        "AO is cavity-biased, not random dirt",
        (ao_concave <= law.TEXTURE_AO_CONCAVITY_CORRELATION_MAX
         and ao_height >= law.TEXTURE_AO_HEIGHT_CORRELATION_MIN),
        "corr(AO, concavity) = {c:+.4f}; corr(AO, height) = {h:+.4f}".format(
            c=ao_concave, h=ao_height),
        "concavity <= {c}, height >= {h}".format(
            c=law.TEXTURE_AO_CONCAVITY_CORRELATION_MAX,
            h=law.TEXTURE_AO_HEIGHT_CORRELATION_MIN),
        "the stored channel is an occlusion MULTIPLIER (1 = open), so a cavity reads LOW "
        "and the concavity correlation must be negative"))

    # --- 9. emission sparse and semantic -------------------------------------
    gates.append(GateResult(
        "emission mask is sparse and semantic",
        True,
        "no emission map emitted",
        "coverage <= {t} when present".format(t=law.TEXTURE_EMISSION_COVERAGE_MAX),
        "N/A BY DECLARATION: playbook section 3 limits emission to bioluminescence, "
        "instrument glow, hot venting, energized equipment or emergency markings. A "
        "sedimentary shelf rock is none of those. An all-zero emission texture would be a "
        "shader binding and a VRAM cost carrying no signal, so the map is omitted"))

    # --- 10. compression preview on the compact lane -------------------------
    compact = law.texture_size_for(0.0)
    factor = max(1, packed.base_color.shape[0] // compact)
    compact_base = np.rint(_box_downsample(packed.base_color, factor)).astype(np.uint8)
    psnr = _psnr(compact_base, _bc1_simulate(compact_base))
    gates.append(GateResult(
        "compression preview keeps detail on the compact lane",
        psnr >= law.TEXTURE_COMPRESSION_PSNR_MIN_DB,
        "simulated BC1 PSNR {p:.3f} dB at {c}x{c}".format(p=psnr, c=compact),
        ">= {t} dB".format(t=law.TEXTURE_COMPRESSION_PSNR_MIN_DB),
        "PARTIAL: BC1 at 4 bpp is a conservative stand-in for the shipped BC7 at 8 bpp. "
        "Unity's own encoder is behind the editor lock and is NOT exercised here"))

    # --- 11. mip preview ------------------------------------------------------
    levels = []
    current = packed.base_color.astype(np.float64)
    previous_luma = float(current.mean())
    worst_drift = 0.0
    worst_seam = 0.0
    measured_levels = 0
    while min(current.shape[0], current.shape[1]) >= 8:
        current = _box_downsample(current, 2)
        this_luma = float(current.mean())
        drift = abs(this_luma - previous_luma) / 255.0
        worst_drift = max(worst_drift, drift)
        # Below law.TEXTURE_SEAM_MIN_LINES there are too few interior lines for a
        # percentile to mean anything, so the seam is not measured rather than reported as
        # noise. Luma drift is still measured at every level.
        if current.shape[0] >= law.TEXTURE_SEAM_MIN_LINES:
            seam = seam_excess(current)
            worst_seam = max(worst_seam, seam)
            measured_levels += 1
            levels.append("{s}px drift {d:.5f} seam {r:.3f}".format(
                s=current.shape[0], d=drift, r=seam))
        else:
            levels.append("{s}px drift {d:.5f} seam n/m".format(
                s=current.shape[0], d=drift))
        previous_luma = this_luma
    gates.append(GateResult(
        "mip chain has no dark seams or ringing",
        (worst_drift <= law.TEXTURE_MIP_LUMA_DRIFT_MAX
         and worst_seam <= law.TEXTURE_SEAM_EXCESS_MAX),
        "worst luma drift {d:.5f}, worst seam excess {s:.4f} over {n} levels "
        "({m} seam-measurable); ".format(d=worst_drift, s=worst_seam, n=len(levels),
                                         m=measured_levels) + "; ".join(levels),
        "drift <= {d}, seam <= {s}".format(
            d=law.TEXTURE_MIP_LUMA_DRIFT_MAX,
            s=law.TEXTURE_SEAM_EXCESS_MAX),
        "box-filtered chain. A dark seam shows up as a level that loses energy"))

    return gates


def selftest_seam_gate(resolution: int = 256, verbose: bool = True) -> list:
    """Prove the seam gate FIRES on a non-periodic field and stays quiet on a periodic one.

    ``AGENTS.md``: a gate that cannot fail is the same defect as a gate that cannot fire.
    The seam statistic was already reformulated once after it rejected a provably periodic
    field, and the obvious way to over-correct is to loosen it until nothing fails. These
    controls bracket it from both sides.
    """
    results = []
    rng = np.random.default_rng(4402)

    periodic = periodic_fbm(rng, resolution, 1.25, coarsest_m=0.075, finest_m=0.008)
    clean = seam_excess(periodic)
    results.append((
        "periodic control passes", clean <= law.TEXTURE_SEAM_EXCESS_MAX,
        "excess {v:.4f} (threshold {t})".format(v=clean,
                                                t=law.TEXTURE_SEAM_EXCESS_MAX)))

    # A non-periodic field of similar character: independent noise box-smoothed with a
    # non-wrapping filter, so opposite edges are uncorrelated.
    raw = rng.normal(size=(resolution + 16, resolution + 16))
    kernel = 9
    integral = np.cumsum(np.cumsum(raw, axis=0), axis=1)
    smooth = (integral[kernel:, kernel:] - integral[:-kernel, kernel:]
              - integral[kernel:, :-kernel] + integral[:-kernel, :-kernel])
    broken = seam_excess(smooth / smooth.std())
    results.append((
        "non-periodic control FAILS", broken > law.TEXTURE_SEAM_EXCESS_MAX,
        "excess {v:.4f} (threshold {t})".format(v=broken,
                                                t=law.TEXTURE_SEAM_EXCESS_MAX)))

    # A hard discontinuity: offset one half of a periodic field, the classic symptom of a
    # tile assembled from two sources.
    spliced = periodic.copy()
    spliced[: resolution // 2] += 1.4
    spliced_excess = seam_excess(spliced)
    results.append((
        "spliced-tile control FAILS", spliced_excess > law.TEXTURE_SEAM_EXCESS_MAX,
        "excess {v:.4f} (threshold {t})".format(v=spliced_excess,
                                                t=law.TEXTURE_SEAM_EXCESS_MAX)))

    if verbose:
        for name, passed, detail in results:
            sys.stdout.write("{tag} {name}: {detail}\n".format(
                tag="PASS" if passed else "FAIL", name=name, detail=detail))
    return results


# ===========================================================================
# Steps 1, 5, 6, 7 of section 10: manifest, padding declaration, import, binding
# ===========================================================================

MANIFEST_SCHEMA = "h8forge.texture/1"


def atlas_declaration(spec: GeologyTextureSpec) -> dict:
    """Step 5's atlas half, answered with a refusal and a reason.

    ``3DMODEL_TEXTURES_MATERIALS.md`` section 5 requires "Edge bleed must fill padding"
    and section 10 rejects an "Atlas without mip padding". Neither applies to this family,
    and saying so is not a skipped step:

    A TILING TEXTURE CANNOT BE ATLAS-PACKED. Wrap addressing is what makes the tile
    seamless across a rock's whole surface; an atlas rect has no wrap, so the moment the
    tile is placed in a page the shader's ``frac()`` addressing reads a neighbour's pixels
    instead of its own. Section 7's triplanar requirements say the same thing from the
    other side -- triplanar needs "stable world/object-space coordinates", which is
    incompatible with a rect offset.

    What the padding rule protects against is a mip level pulling in a neighbour's colour.
    For a tiling map the equivalent guarantee is that the wrap itself survives the mip
    chain, which is measured by section 9's gate 11 rather than assumed here.
    """
    return {
        "atlasPacked": False,
        "reason":
            "the family ships as a standalone WRAPPING tile for object-space triplanar "
            "projection at {t} m per tile. An atlas rect has no wrap addressing, so "
            "packing it would break the tiling it exists to provide "
            "(3DMODEL_TEXTURES_MATERIALS.md section 7 requires stable object-space "
            "coordinates).".format(t=spec.tile_m),
        "paddingRuleSatisfiedBy":
            "gate 11 measures wrap continuity at every mip level of the shipped tile, "
            "which is the tiling equivalent of the atlas bleed-padding requirement",
        "paddingThatWouldApplyIfAtlassed": law.atlas_padding_for(
            spec.resolved_resolution()),
    }


def binding_contract(spec: GeologyTextureSpec) -> dict:
    """Step 7: DECLARED, not performed. A sibling owns the Unity side.

    Writing ``.mat`` assets, setting importer flags and binding textures to a material are
    all Unity-editor operations behind the single project lock, and ``AGENTS.md``'s Unity
    gate allows one owner at a time. This block is the handoff: everything the Unity-side
    binder needs, stated precisely enough that it does not have to re-derive any of it.

    THE BEDDING-FRAME REQUIREMENT IS THE ONE THAT WILL BE MISSED. This tile is authored
    with its laminae perpendicular to +V, so the projection must run in the ASSET'S BEDDING
    FRAME, not in raw object space. ``generators/rock.py`` already records
    ``beddingDipDeg`` and ``beddingAzimuthDeg`` per asset and exposes a
    ``UV1_Strata`` channel unwrapped "cylindrical_in_bedding_frame" for exactly this. Bind
    it in raw object space and every rock's bedding will point the same way regardless of
    how its strata actually lie, which reads as a texture sliding over the geometry.
    """
    return {
        "performed": False,
        "reason": "Unity-side material authoring is behind the single editor lock; this "
                  "lane declares the contract and stops",
        "materials": {
            "MAT_Geology_Primary": "slot 0, primary rock. Binds this family.",
            "MAT_Geology_FractureFace": "slot 1, exposed fracture. Reuses this family "
                                        "with the edge-wear response raised.",
            "MAT_Geology_MineralVein": "slot 2, vein. NOT covered by this family; a vein "
                                       "set is a separate TX_* family and is outstanding.",
        },
        "projection": "object-space triplanar in the asset's BEDDING FRAME",
        "metresPerTile": spec.tile_m,
        "metresPerTileSource": "generators/rock.py TRIPLANAR_METRES_PER_TILE :2879 and "
                               "each manifest's uvAndTriplanarReport.triplanarMetresPerTile",
        "beddingFrameRequired": True,
        "beddingFrameSource": "manifest profileParameters.beddingDipDeg / "
                              "beddingAzimuthDeg, or the UV1_Strata channel",
        "vertexColourInteraction":
            "the mesh carries R edge wear, G mineral stain, B ray-traced AO, A ore mask "
            "per vertex. The texture's occlusion covers only features finer than "
            "{c} m while the vertex bake covers 0.35 m, so the two are COMPLEMENTARY and "
            "the shader should combine them once, not multiply both into albedo".format(
                c=law.GEOLOGY_TEXTURE_BAND_CEILING_M),
        "shaderTarget": "Hecton_ModuleHardSurfaceLit (_BaseMap, _BumpMap, _MaskMap, "
                        "_ParallaxMap) -- the _MaskMap decode at :349-353 matches this "
                        "family's URP packing exactly",
    }


def build_manifest(spec: GeologyTextureSpec, height: HeightField,
                   surface: SurfaceFields, channels: ChannelSet,
                   packed: PackedMaps, gates: list, files: dict,
                   destination: str) -> dict:
    """Everything section 2 and section 11 require a texture family to record."""
    failed = [g.name for g in gates if not g.passed]
    # The extent budget is a FAMILY requirement, not one of the eleven section 9 gates, but
    # it still blocks publication: a tile whose bedding runs edge to edge reads as sawn
    # timber however many section 9 gates pass. Kept in a separate list so the two are never
    # conflated and the section 9 count stays honest at twelve.
    extent = height.report.get("structuralExtent", {})
    extent_failures = []
    if extent and not extent.get("runBudgetMet", True):
        extent_failures.append("lamina run budget")
    if extent and not extent.get("erosionalCoverageMet", True):
        extent_failures.append("erosional coverage minimum")
    return {
        "schema": MANIFEST_SCHEMA,
        "identity": {
            "generator": MODULE_NAME,
            "generatorVersion": MODULE_VERSION,
            "forgeVersion": law.FORGE_VERSION,
            "seed": spec.seed,
            "qualityWeight": round(spec.quality, 6),
            "family": spec.family_name(),
            "textureSet": spec.set_name,
            "intendedMeshFamily": "Geology (generators/rock.py), size classes boulder / "
                                  "outcrop / cliffchunk",
            "platformLane": "windows_copper_wire",
            "sourceReferences": [
                "3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md sections 0-10",
                "3DMODEL_TEXTURES_MATERIALS.md sections 2, 3, 5, 7, 8",
                "3DMODEL_GEOLOGY_ROCKS.md sections 3, 4, 5",
                "Docs/mandatory .../nice_biome.webp (pigment, via rock.py:2692-2705)",
            ],
        },
        "materialIdentity": {
            "vocabulary": "sediment (playbook section 4)",
            "lithology": "laminated argillaceous siltstone with carbonate-cemented "
                         "laminae and sparse bioclasts; cool dark slate pigment with an "
                         "algae mat on upward faces",
            "processTag": spec.process,
            "matchesMeshDeclaration":
                "the shipped geology assets declare geologicalProcessTag 'sedimentary' "
                "and materialFamily MAT_Geology_*, so the texture and the mesh agree on "
                "what the rock is",
            "notEmitted": ["basalt", "hydrothermal vent", "ore node", "cave wall"],
        },
        "scaleCalibration": {
            "metresPerTile": spec.tile_m,
            "resolution": spec.resolved_resolution(),
            "metresPerPixel": round(spec.metres_per_pixel, 8),
            "grainWitnessM": law.GEOLOGY_GRAIN_WITNESS_M,
            "grainWitnessPixels": round(
                law.GEOLOGY_GRAIN_WITNESS_M / spec.metres_per_pixel, 2),
            "pitWitnessM": law.GEOLOGY_PIT_WITNESS_M,
            "pitWitnessPixels": round(
                law.GEOLOGY_PIT_WITNESS_M / spec.metres_per_pixel, 2),
            "meshFinestWavelengthM": dict(law.GEOLOGY_MESH_FINEST_WAVELENGTH_M),
            "textureBandCeilingM": law.GEOLOGY_TEXTURE_BAND_CEILING_M,
            "whyThisFamilyExists":
                "the 0.075 m grain witness is below the finest wavelength every geology "
                "size class can represent as geometry (0.087 / 0.205 / 0.406 m), so this "
                "band cannot exist as mesh relief at any budget. "
                "3DMODEL_GEOLOGY_ROCKS.md section 2 routes it to baked normal/depth "
                "support, which is this map stack.",
        },
        "heightField": height.report,
        "measuredFields": surface.report,
        "channelDerivation": channels.derivation,
        "channelLayouts": packed.layouts,
        "textures": files,
        "importSettings": {role: law.TEXTURE_IMPORT_SETTINGS[role]
                           for role in law.SHIPPED_TEXTURE_ROLES},
        "importPerformed": False,
        "importNote":
            "declared only. Unity's importer is behind the single editor lock, so sRGB "
            "flags, NormalMap type, BC5/BC7 formats and mip settings are a contract for "
            "the Unity-side owner to apply, not a claim that they were applied.",
        "atlas": atlas_declaration(spec),
        "bindingContract": binding_contract(spec),
        "acceptanceGates": [
            {"gate": g.name, "passed": g.passed, "measured": g.measured,
             "threshold": g.threshold, "note": g.note} for g in gates
        ],
        "gatesPassed": sum(1 for g in gates if g.passed),
        "gatesTotal": len(gates),
        "structuralExtent": extent,
        "productionReady": not failed and not extent_failures,
        "failedGates": failed,
        "failedFamilyRequirements": extent_failures,
        "destination": destination,
        "destinationRule":
            "playbook section 9: a family that fails any gate must not enter the "
            "production asset route and may write a diagnostic artifact under "
            "Docs/AgentLogs instead. law.forge_texture_dir() is the gated production "
            "path; law.forge_texture_proof_dir() is this one.",
        "unverified": [
            "NOT imported into Unity. No .meta, no GUID, no compressed asset exists.",
            "NOT rendered under URP. Preview renders are Blender EEVEE, which is a "
            "different BRDF and a different tonemap.",
            "Compression gate uses a simulated BC1 round trip, not Unity's BC7 encoder.",
            "No profiler, device, or VRAM measurement.",
            "No MAT_* asset created and nothing bound to a prefab.",
            "MAT_Geology_MineralVein has no texture family; slot 2 is still uncovered.",
        ],
    }


def write_family(spec: GeologyTextureSpec, *, output_dir: Optional[str] = None,
                 verbose: bool = True) -> dict:
    """Generate, gate and write the whole family. Returns the manifest.

    Destination is decided by the gates, per section 9. Even on a clean sweep this writes
    to the PROOF tree rather than under ``Assets``: publishing is an import event, and
    ``AGENTS.md``'s Unity gate allows one owner at a time. The manifest records the
    production path so the owner holding the lock can move it deliberately.
    """
    rng = np.random.default_rng(spec.seed)
    height = build_height_field(spec, rng)
    surface = measure_surface(height)
    channels = derive_channels(spec, height, surface, rng)
    packed = pack_maps(channels)
    gates = run_acceptance_gates(spec, height, surface, channels, packed)

    root = law.project_root()
    proof_dir = output_dir or os.path.join(
        root, law.forge_texture_proof_dir(law.Family.GEOLOGY).replace("/", os.sep))
    os.makedirs(proof_dir, exist_ok=True)

    emit = (
        (law.TEXTURE_ROLE_BASECOLOR, packed.base_color, 8),
        (law.TEXTURE_ROLE_NORMAL, packed.normal, 8),
        (law.TEXTURE_ROLE_MASK_URP, packed.mask_urp, 8),
        (law.TEXTURE_ROLE_ARM, packed.arm, 8),
        (law.TEXTURE_ROLE_HEIGHT, packed.height, 16),
    )

    files = {}
    for role, array, depth in emit:
        name = spec.texture_name(role) + ".png"
        path = os.path.join(proof_dir, name)
        write_png(path, array, bit_depth=depth)
        # ROUND TRIP EVERY MAP. AGENTS.md [RULE] Never Trust Automated Assertions Alone:
        # the file existing proves nothing about what is in it, and a channel swap in the
        # encoder is invisible in every downstream number because every downstream number
        # was computed from the in-memory array.
        back = read_png(path)
        exact = bool(np.array_equal(np.asarray(array), np.asarray(back)))
        files[role] = {
            "file": name,
            "bitDepth": depth,
            "channels": (1 if array.ndim == 2 else int(array.shape[2])),
            "resolution": int(array.shape[0]),
            "bytes": os.path.getsize(path),
            "roundTripExact": exact,
            "productionName": spec.texture_name(role),
            "productionPathWhenPublished":
                law.forge_texture_dir(law.Family.GEOLOGY) + "/" + name,
        }
        if not exact:
            raise RuntimeError("PNG round trip mismatch for " + role)

    destination = law.forge_texture_proof_dir(law.Family.GEOLOGY)
    manifest = build_manifest(spec, height, surface, channels, packed, gates, files,
                              destination)

    manifest_name = law.NAME_MANIFEST.format(
        family=spec.family_name(), name="TX_" + spec.set_name) + ".json"
    manifest_path = os.path.join(proof_dir, manifest_name)
    with open(manifest_path, "w", encoding="utf-8") as handle:
        json.dump(manifest, handle, indent=1, sort_keys=False)
    manifest["manifestPath"] = manifest_path

    if verbose:
        sys.stdout.write("\n=== {n} @ {r}px, {t} m tile ===\n".format(
            n=spec.set_name, r=spec.resolved_resolution(), t=spec.tile_m))
        for role, info in files.items():
            sys.stdout.write("  {r:<20} {b:>9} bytes  roundtrip={e}\n".format(
                r=role, b=info["bytes"], e=info["roundTripExact"]))
        sys.stdout.write("\n--- section 9 acceptance gates ---\n")
        for gate in gates:
            sys.stdout.write("{tag} | {n}\n      measured: {m}\n      want:     {t}\n".format(
                tag="PASS" if gate.passed else "FAIL", n=gate.name,
                m=gate.measured, t=gate.threshold))
        extent = manifest.get("structuralExtent", {})
        if extent:
            runs = extent.get("longestIntactRunFraction", {})
            sys.stdout.write(
                "\n--- family structural extent (NOT a section 9 gate) ---\n"
                "{tag} longest intact bedding run p50 {a:.3f} p95 {b:.3f} max {c:.3f} "
                "of tile (budget {d})\n"
                "{tag2} erosional coverage {e:.4f} (minimum {f})\n".format(
                    tag="PASS" if extent.get("runBudgetMet") else "FAIL",
                    a=runs.get("p50", 0.0), b=runs.get("p95", 0.0),
                    c=runs.get("max", 0.0), d=extent.get("runBudgetFraction"),
                    tag2="PASS" if extent.get("erosionalCoverageMet") else "FAIL",
                    e=extent.get("erosionalCoverage", 0.0),
                    f=extent.get("erosionalCoverageMin")))
        sys.stdout.write("\n{p}/{t} gates pass. productionReady={r}\n".format(
            p=manifest["gatesPassed"], t=manifest["gatesTotal"],
            r=manifest["productionReady"]))
        sys.stdout.write("manifest: {p}\n".format(p=manifest_path))
    return manifest


def bake_geology_family(seed: int = 1713, quality: float = 1.0,
                        resolution: int = 0, set_name: str = "SedimentaryShelf",
                        output_dir: Optional[str] = None,
                        verbose: bool = True) -> dict:
    """Public entry point: bake the geology texture family and return its manifest."""
    spec = GeologyTextureSpec(set_name=set_name, seed=seed, quality=quality,
                              resolution=resolution)
    return write_family(spec, output_dir=output_dir, verbose=verbose)


# ===========================================================================
# Step 8 of section 10: preview render against neutral, low and grazing light
# ===========================================================================
# "Render an editor preview against neutral, low, and grazing URP lights."
#
# THIS IS BLENDER, NOT URP, AND THE DIFFERENCE IS NOT COSMETIC. EEVEE's BRDF, its indirect
# lighting and its tonemap are all different from URP's, so this render proves the MAPS are
# coherent and readable -- it does not prove how the material behaves in the game. Unity is
# behind a single editor lock held by other owners, and ``AGENTS.md`` allows one owner at a
# time. The manifest lists the URP render as UNVERIFIED rather than implying it happened.
#
# TWO SUBJECTS, because each proves something the other cannot:
#
#   * a FLAT 1.25 m SAMPLE. Playbook section 6 asks generated material sources to be
#     "orthographic material samples, not object renders", and a plane's UV is exact -- so
#     the tangent-space normal map is consumed with no projection ambiguity at all and the
#     scale calibration is directly visible against a witness.
#   * the ACTUAL BOULDER at seed 1713, so the material is judged on real geology.
#
# THE BOULDER IS PROJECTED THROUGH UV0, NOT THROUGH THE SHIPPED TRIPLANAR ROUTE, and that
# is a deliberate limitation rather than an oversight. A correct triplanar normal map needs
# per-axis tangent-to-object reconstruction with the right swizzle and sign per projection
# plane; getting that wrong produces a render that looks like detail and lights from the
# wrong direction, which is worse than not showing it. That reconstruction belongs to the
# Unity shader that owns the binding, and the manifest hands it over explicitly. UV0 exists
# on these meshes as a real angle-based unwrap for exactly this fallback purpose
# (``rock.py`` records it as ``smart_project_angle_based_fallback``), and through it
# Blender's own Normal Map node consumes the tangent-space map correctly.
# The cost is that texel density on the rock follows the unwrap rather than 1.25 m per
# tile, so the flat sample is the scale proof and the boulder is the material-read proof.

LIGHTING_SETUPS = {
    # (name, [(direction, energy)], world_strength, world_colour)
    "neutral": {
        "lights": [((-0.55, -0.75, 0.65), 4.2), ((0.8, -0.35, 0.18), 1.15),
                   ((0.15, 0.9, 0.5), 2.6)],
        "world": 0.055,
        "note": "three-point sun rig, the comparable baseline preview.py uses",
    },
    "low": {
        "lights": [((-0.35, -0.6, 0.72), 0.55)],
        "world": 0.014,
        "note": "single dim key and near-black ambient: the depth/cave lane. Shows "
                "whether the material still reads when the scene stops helping",
    },
    "grazing": {
        # ENERGY IS RAISED FOR EXPOSURE PARITY, NOT FOR FLATTERY. At 7 degrees elevation on
        # a near-horizontal sample N.L is about 0.12 against roughly 0.8 in the neutral rig,
        # so the same energy renders the grazing pass about eight times darker and the lead
        # cannot see the relief the pass exists to reveal. Scaling the sun to match exposure
        # keeps the LIGHT DIRECTION -- the thing being tested -- untouched. The view
        # transform stays Standard with no look, no bloom and no grading, so nothing here is
        # the presentation-hides-weak-work trick 3dmodel.md bans.
        "lights": [((-0.97, -0.16, 0.122), 26.0)],
        "world": 0.010,
        "note": "single sun at 7 degrees elevation, energy scaled for exposure parity with "
                "the neutral rig. The hardest test for a normal map and the pass that "
                "exposes any baked lighting left in albedo",
    },
}


def _map_paths(spec: GeologyTextureSpec, directory: str) -> dict:
    return {role: os.path.join(directory, spec.texture_name(role) + ".png")
            for role in law.SHIPPED_TEXTURE_ROLES}


def build_preview_material(name: str, paths: dict, *, uv_scale: float = 1.0):
    """Principled material wired from the written PNGs, with correct colour spaces.

    THE COLOUR-SPACE ASSIGNMENT IS THE TRAP. Blender decides how to interpret a loaded
    image from ``colorspace_settings.name``, and its default is sRGB for everything. Load
    the normal, mask or height map as sRGB and every value is silently gamma-decoded: a
    stored 0.5 roughness arrives as 0.21, the normal's flat 0.5 becomes 0.21 so every
    surface tilts, and the render then misrepresents maps that are actually correct. Only
    base colour is sRGB; everything else is Non-Color.

    AO is multiplied into base colour HERE, in the preview only, because Blender's
    Principled BSDF has no occlusion input. That is what URP does with ``occlusionMap`` on
    indirect light, so it is a faithful preview -- and it is exactly what the MAP must never
    do, which is why the two are kept apart.
    """
    import bpy  # lazy: the field/gate half of this module runs without Blender

    def load(role: str, non_color: bool):
        image = bpy.data.images.load(paths[role], check_existing=True)
        image.colorspace_settings.name = "Non-Color" if non_color else "sRGB"
        return image

    material = bpy.data.materials.get(name)
    if material is not None:
        bpy.data.materials.remove(material)
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    tree = material.node_tree
    nodes = tree.nodes
    links = tree.links
    nodes.clear()

    output = nodes.new("ShaderNodeOutputMaterial")
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])

    coords = nodes.new("ShaderNodeTexCoord")
    mapping = nodes.new("ShaderNodeMapping")
    mapping.inputs["Scale"].default_value = (uv_scale, uv_scale, uv_scale)
    links.new(coords.outputs["UV"], mapping.inputs["Vector"])

    base_tex = nodes.new("ShaderNodeTexImage")
    base_tex.image = load(law.TEXTURE_ROLE_BASECOLOR, False)
    links.new(mapping.outputs["Vector"], base_tex.inputs["Vector"])

    arm_tex = nodes.new("ShaderNodeTexImage")
    arm_tex.image = load(law.TEXTURE_ROLE_ARM, True)
    links.new(mapping.outputs["Vector"], arm_tex.inputs["Vector"])
    arm_split = nodes.new("ShaderNodeSeparateColor")
    links.new(arm_tex.outputs["Color"], arm_split.inputs["Color"])

    normal_tex = nodes.new("ShaderNodeTexImage")
    normal_tex.image = load(law.TEXTURE_ROLE_NORMAL, True)
    links.new(mapping.outputs["Vector"], normal_tex.inputs["Vector"])
    normal_map = nodes.new("ShaderNodeNormalMap")
    normal_map.space = "TANGENT"
    links.new(normal_tex.outputs["Color"], normal_map.inputs["Color"])
    links.new(normal_map.outputs["Normal"], bsdf.inputs["Normal"])

    # ARM: R = ambient occlusion, G = roughness, B = metal.
    occlude = nodes.new("ShaderNodeMix")
    occlude.data_type = "RGBA"
    occlude.blend_type = "MULTIPLY"
    # ShaderNodeMix shares socket NAMES across data types; the colour sockets are indices
    # 6 and 7 and the colour Result is output 2. Using inputs["A"] silently resolves to the
    # FLOAT socket and the graph renders white -- documented in rock.py:2770.
    MIX_FACTOR, MIX_A, MIX_B, MIX_RESULT = 0, 6, 7, 2
    occlude.inputs[MIX_FACTOR].default_value = 1.0
    links.new(base_tex.outputs["Color"], occlude.inputs[MIX_A])
    ao_rgb = nodes.new("ShaderNodeCombineColor")
    for channel in ("Red", "Green", "Blue"):
        links.new(arm_split.outputs["Red"], ao_rgb.inputs[channel])
    links.new(ao_rgb.outputs["Color"], occlude.inputs[MIX_B])
    links.new(occlude.outputs[MIX_RESULT], bsdf.inputs["Base Color"])

    links.new(arm_split.outputs["Green"], bsdf.inputs["Roughness"])
    if "Metallic" in bsdf.inputs:
        links.new(arm_split.outputs["Blue"], bsdf.inputs["Metallic"])
    return material


def _purge_scene() -> None:
    import bpy
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for block in list(bpy.data.meshes):
        bpy.data.meshes.remove(block)


def _configure_render(resolution: int, samples: int, world_strength: float) -> None:
    """Honest render settings: no bloom, no grading, Standard view transform.

    ``3dmodel.md`` bans using "darkness, fog, bloom, post, or grading to hide primitive
    terrain, weak textures ... or low-detail assets", and ``preview.py`` makes the same
    point: a diagnostic render with a filmic curve is no longer a measurement.
    """
    import bpy
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = resolution
    scene.render.resolution_y = resolution
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = False
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGB"
    scene.render.image_settings.color_depth = "8"
    try:
        scene.eevee.taa_render_samples = samples
    except AttributeError:
        pass
    try:
        scene.view_settings.view_transform = "Standard"
        scene.view_settings.look = "None"
        scene.view_settings.exposure = 0.0
        scene.view_settings.gamma = 1.0
    except (AttributeError, TypeError):
        pass

    world = scene.world
    if world is None:
        world = bpy.data.worlds.new("H8TXWorld")
        scene.world = world
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    if background is not None:
        background.inputs["Color"].default_value = (world_strength, world_strength,
                                                    world_strength * 1.2, 1.0)
        background.inputs["Strength"].default_value = 1.0


def _build_lights(setup: str) -> list:
    import bpy
    from mathutils import Vector
    created = []
    config = LIGHTING_SETUPS[setup]
    for index, (direction, energy) in enumerate(config["lights"]):
        data = bpy.data.lights.new("H8TX_L{i}".format(i=index), type="SUN")
        data.energy = energy
        data.angle = math.radians(1.5)
        obj = bpy.data.objects.new("H8TX_L{i}".format(i=index), data)
        bpy.context.scene.collection.objects.link(obj)
        vector = Vector(direction).normalized()
        obj.location = vector * 12.0
        obj.rotation_euler = (-vector).to_track_quat("-Z", "Y").to_euler()
        created.append(obj)
    return created


def _place_camera(target, radius: float, direction=(-0.72, -0.78, 0.42),
                  margin: float = 1.04):
    import bpy
    from mathutils import Vector
    data = bpy.data.cameras.new("H8TX_Cam")
    data.lens_unit = "FOV"
    data.angle = math.radians(38.0)
    data.clip_start = max(0.001, radius * 0.002)
    data.clip_end = radius * 80.0 + 100.0
    camera = bpy.data.objects.new("H8TX_Cam", data)
    bpy.context.scene.collection.objects.link(camera)
    vector = Vector(direction).normalized()
    distance = (radius * margin) / math.tan(data.angle * 0.5)
    camera.location = Vector(target) + vector * distance
    camera.rotation_euler = (-vector).to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.camera = camera
    return camera


def _make_sample_plane(tile_m: float, material):
    """A single tile of the material at TRUE SCALE, plus a 1 m witness bar.

    The plane is exactly ``tile_m`` across and the material maps 1:1 onto it, so anything
    the lead measures against the witness is the real physical size of the feature. Without
    a witness a material sample cannot answer "is this rock the right roughness at 2 m",
    which is what section 4's scale-calibration requirement is about.
    """
    import bpy
    import bmesh

    bm = bmesh.new()
    half = tile_m * 0.5
    verts = [bm.verts.new(v) for v in ((-half, -half, 0.0), (half, -half, 0.0),
                                       (half, half, 0.0), (-half, half, 0.0))]
    face = bm.faces.new(verts)
    layer = bm.loops.layers.uv.new("UVMap")
    for loop, uv in zip(face.loops, ((0.0, 0.0), (1.0, 0.0), (1.0, 1.0), (0.0, 1.0))):
        loop[layer].uv = uv
    mesh = bpy.data.meshes.new("H8TX_SampleMesh")
    bm.to_mesh(mesh)
    bm.free()
    plane = bpy.data.objects.new("H8TX_Sample", mesh)
    bpy.context.scene.collection.objects.link(plane)
    plane.data.materials.append(material)

    # 1 m emissive witness bar laid on the surface.
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    witness_mesh = bpy.data.meshes.new("H8TX_WitnessMesh")
    bm.to_mesh(witness_mesh)
    bm.free()
    witness = bpy.data.objects.new("H8TX_Witness", witness_mesh)
    bpy.context.scene.collection.objects.link(witness)
    witness.scale = (1.0, tile_m * 0.018, tile_m * 0.018)
    witness.location = (0.0, -half * 0.82, tile_m * 0.02)

    emissive = bpy.data.materials.new("H8TX_WitnessMat")
    emissive.use_nodes = True
    nodes = emissive.node_tree.nodes
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    emission = nodes.new("ShaderNodeEmission")
    emission.inputs["Color"].default_value = (0.95, 0.45, 0.10, 1.0)
    emission.inputs["Strength"].default_value = 2.0
    emissive.node_tree.links.new(emission.outputs["Emission"], out.inputs["Surface"])
    witness.data.materials.append(emissive)
    return plane, witness


def _import_boulder(fbx_relative: str, material):
    """Import a forge geology FBX READ-ONLY and apply the preview material via UV0.

    Nothing is written back. The FBX lives under ``Assets`` and this lane must not touch
    it: ``AGENTS.md`` makes Unity the sole writer of anything in that tree, and an import
    here is a read.
    """
    import bpy
    path = os.path.join(law.project_root(), fbx_relative.replace("/", os.sep))
    if not os.path.exists(path):
        return None, path
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    imported = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    if not imported:
        return None, path

    # An FBX package carries the whole LOD chain. Keep LOD0 and hide the rest, or the
    # camera photographs three stacked copies -- the defect preview.py._isolate_subject
    # documents, which produced three separate "the render does not match the data" reports.
    def lod_key(obj):
        name = obj.name.upper()
        for index in range(4):
            if "LOD" + str(index) in name:
                return index
        return 9

    imported.sort(key=lod_key)
    subject = imported[0]
    for other in imported[1:]:
        other.hide_render = True
    for other in bpy.data.objects:
        if other.type == "MESH" and other not in imported \
                and not other.name.startswith("H8TX_"):
            other.hide_render = True

    subject.data.materials.clear()
    subject.data.materials.append(material)
    return subject, path


def render_preview_sweep(spec: GeologyTextureSpec, map_dir: str, *,
                         resolution: int = 900, samples: int = 64,
                         boulder_fbx: str = "Assets/_Project/Art/Generated/Forge/"
                                            "Geology/MESH_Geology_boulder_sedimentary_"
                                            "s1713_q100.fbx",
                         verbose: bool = True) -> dict:
    """Render the flat sample and the boulder under all three lighting setups."""
    import bpy
    paths = _map_paths(spec, map_dir)
    missing = [r for r, p in paths.items() if not os.path.exists(p)]
    if missing:
        raise RuntimeError("missing maps: " + ", ".join(missing))

    out_dir = map_dir
    written = {}
    notes = {}

    for subject_name in ("sample", "boulder"):
        _purge_scene()
        material = build_preview_material(
            "MAT_PREVIEW_" + spec.set_name + "_" + subject_name, paths)

        if subject_name == "sample":
            subject, _witness = _make_sample_plane(spec.tile_m, material)
            centre = (0.0, 0.0, 0.0)
            # A flat tile viewed obliquely foreshortens to a sliver and wastes most of the
            # frame, so the sample is shot close to face-on with a tight margin. The first
            # framing used a 0.30/-0.86/0.41 direction with a 1.18 margin and the tile
            # covered under a third of the image -- a render the lead cannot judge detail
            # in is not proof of anything.
            radius = spec.tile_m * 0.5 * math.sqrt(2.0)
            view = (-0.18, -0.45, 0.87)
            notes[subject_name] = (
                "flat {t} m tile at TRUE SCALE with a 1 m emissive witness bar; UV maps "
                "1:1 so the tangent-space normal is consumed exactly".format(
                    t=spec.tile_m))
        else:
            subject, fbx_path = _import_boulder(boulder_fbx, material)
            if subject is None:
                notes[subject_name] = "SKIPPED: FBX not found at " + fbx_path
                if verbose:
                    sys.stdout.write("  boulder skipped: no FBX at " + fbx_path + "\n")
                continue
            # WORLD bounds, not local. FBX import applies a unit conversion at the OBJECT
            # level, so ``vertex.co`` is in the exporter's units while the camera works in
            # scene units. Framing a 0.8 m boulder from its local coordinates put the
            # camera two orders of magnitude too far away and rendered a black frame with
            # a single grey speck at the centre -- which looks like a missing subject, not
            # like a units bug. ``preview._world_bounds`` transforms by ``matrix_world``
            # for exactly this reason.
            matrix = subject.matrix_world
            world = [matrix @ v.co for v in subject.data.vertices]
            lo = [min(p[i] for p in world) for i in range(3)]
            hi = [max(p[i] for p in world) for i in range(3)]
            centre = tuple((lo[i] + hi[i]) * 0.5 for i in range(3))
            radius = max(1e-3, 0.5 * math.sqrt(sum((hi[i] - lo[i]) ** 2
                                                   for i in range(3))))
            view = (-0.72, -0.78, 0.30)
            if verbose:
                sys.stdout.write(
                    "  boulder world extent {x:.3f} x {y:.3f} x {z:.3f} m, "
                    "radius {r:.3f} m\n".format(x=hi[0] - lo[0], y=hi[1] - lo[1],
                                                z=hi[2] - lo[2], r=radius))
            notes[subject_name] = (
                "MESH_Geology_boulder_sedimentary_s1713_q100 LOD0, material projected "
                "through UV0 (the angle-based fallback unwrap), NOT through the shipped "
                "object-space triplanar route -- so texel scale on the rock follows the "
                "unwrap, not 1.25 m per tile")

        for setup in ("neutral", "low", "grazing"):
            for light in _build_lights(setup):
                pass
            _configure_render(resolution, samples, LIGHTING_SETUPS[setup]["world"])
            _place_camera(centre, radius, direction=view)
            name = "PREVIEW_{s}_{sub}_{lit}.png".format(
                s=spec.set_name, sub=subject_name, lit=setup)
            path = os.path.join(out_dir, name)
            bpy.context.scene.render.filepath = path
            bpy.ops.render.render(write_still=True)
            written[subject_name + "/" + setup] = name
            if verbose:
                sys.stdout.write("  rendered {n}\n".format(n=name))
            for obj in list(bpy.data.objects):
                if obj.name.startswith("H8TX_L") or obj.name.startswith("H8TX_Cam"):
                    bpy.data.objects.remove(obj, do_unlink=True)

    return {"renders": written, "subjects": notes,
            "engine": "BLENDER_EEVEE_NEXT",
            "viewTransform": "Standard, no look, no bloom, no grading",
            "lightingSetups": {k: v["note"] for k, v in LIGHTING_SETUPS.items()},
            "urpVerified": False,
            "urpNote": "EEVEE is not URP. Different BRDF, different indirect lighting, "
                       "different tonemap. Section 10 step 8 asks for a URP preview and "
                       "that remains UNVERIFIED while the editor lock is held elsewhere."}


# ===========================================================================
# CLI
# ===========================================================================

def main(argv: Optional[list] = None) -> int:
    """Headless entry point.

    Bake only (no Blender needed)::

        python -c "import sys;sys.path.insert(0,'Tools/Blender');\
from h8forge import texture;texture.bake_geology_family()"

    Bake plus preview renders::

        blender.exe -b --factory-startup -P Tools/Blender/h8forge/texture.py -- \
            --resolution 2048 --render
    """
    import argparse
    parser = argparse.ArgumentParser(description="HECTON-8 geology texture family")
    parser.add_argument("--seed", type=int, default=1713)
    parser.add_argument("--quality", type=float, default=1.0)
    parser.add_argument("--resolution", type=int, default=0,
                        help="0 = derive from quality via law.texture_size_for")
    parser.add_argument("--set-name", default="SedimentaryShelf")
    parser.add_argument("--render", action="store_true",
                        help="also render the preview sweep (requires Blender)")
    parser.add_argument("--selftest", action="store_true",
                        help="run the orientation and seam-gate controls and exit")
    parser.add_argument("--preview-resolution", type=int, default=900)
    if argv is None:
        argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    args = parser.parse_args(argv)

    if args.selftest:
        results = selftest_orientation() + selftest_seam_gate()
        failed = [n for n, ok, _ in results if not ok]
        sys.stdout.write("\n{p}/{t} self-tests pass\n".format(
            p=len(results) - len(failed), t=len(results)))
        return 1 if failed else 0

    spec = GeologyTextureSpec(set_name=args.set_name, seed=args.seed,
                              quality=args.quality, resolution=args.resolution)
    manifest = write_family(spec)

    if args.render:
        map_dir = os.path.dirname(manifest["manifestPath"])
        sys.stdout.write("\n--- preview sweep ---\n")
        preview = render_preview_sweep(spec, map_dir,
                                       resolution=args.preview_resolution)
        manifest["previewRenders"] = preview
        with open(manifest["manifestPath"], "w", encoding="utf-8") as handle:
            json.dump(manifest, handle, indent=1, sort_keys=False)
        sys.stdout.write("manifest updated with {n} renders\n".format(
            n=len(preview["renders"])))

    return 0 if manifest["productionReady"] else 2


if __name__ == "__main__":
    sys.exit(main())
