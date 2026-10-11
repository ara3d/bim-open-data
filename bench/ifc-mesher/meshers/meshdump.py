"""Writes a .meshdump file: the format tests/data/Ara3D.IfcMeshingComparison/Harness/MeshDump.cs reads.

Header: ASCII MDMP, int32 version 1, int32 JSON length, UTF-8 JSON. Body: for each mesh in order,
points x 3 float32 then triangles x 3 int32, little-endian. Lengths in metres. An instance matrix is
16 floats with the translation at indices 12, 13, 14.
"""
import hashlib
import json
import os
import struct
from datetime import datetime, timezone

IDENTITY = [1.0, 0, 0, 0, 0, 1.0, 0, 0, 0, 0, 1.0, 0, 0, 0, 0, 1.0]


def source_of(ifc_path):
    with open(ifc_path, "rb") as f:
        data = f.read()
    return {"fileName": os.path.basename(ifc_path), "sha256": hashlib.sha256(data).hexdigest(), "bytes": len(data)}


def write(path, mesher, version, source, elapsed_ms, meshes, instances):
    """meshes: list of (points_bytes_f32, triangles_bytes_i32, point_count, triangle_count).
    instances: list of (entity_id, mesh_index, matrix16)."""
    header = {
        "mesher": mesher,
        "mesherVersion": version,
        "source": source,
        "elapsedMs": int(elapsed_ms),
        "generatedUtc": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "meshes": [{"points": p, "triangles": t} for (_, _, p, t) in meshes],
        "instances": [{"entity": e, "mesh": m, "matrix": [float(x) for x in mat]} for (e, m, mat) in instances],
    }
    blob = json.dumps(header, separators=(",", ":")).encode("utf-8")
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    with open(path, "wb") as f:
        f.write(b"MDMP")
        f.write(struct.pack("<ii", 1, len(blob)))
        f.write(blob)
        for (pts, tris, _, _) in meshes:
            f.write(pts)
            f.write(tris)
