"""Meshes one IFC file with IFClite (the ifclite-geom wheel) and writes a .meshdump.

usage: python mesh.py <model.ifc> <out.meshdump>

IFClite returns one welded mesh per element in world coordinates (Z up, metres, the RTC offset
folded in), so every instance carries the identity matrix and the element's STEP id.
"""
import os
import sys
import time
from importlib import metadata

import numpy as np
import ifclite_geom

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import meshdump  # noqa: E402

NAME = "IfcLite"


def version():
    return metadata.version("ifclite-geom")


def mesh(ifc_path, out_path):
    source = meshdump.source_of(ifc_path)
    with open(ifc_path, "rb") as f:
        ifc_bytes = f.read()
    started = time.perf_counter()
    data = ifclite_geom.geometry_data_buffers(ifc_bytes)
    meshes = []
    instances = []
    for step_id, element in data["elements"].items():
        verts = np.frombuffer(element["vertices"], dtype=np.float64).astype(np.float32)
        faces = np.frombuffer(element["faces"], dtype=np.uint32).astype(np.int32)
        if len(faces) == 0:
            continue
        instances.append((int(step_id), len(meshes), meshdump.IDENTITY))
        meshes.append((verts.tobytes(), faces.tobytes(), len(verts) // 3, len(faces) // 3))
    elapsed_ms = (time.perf_counter() - started) * 1000
    meshdump.write(out_path, NAME, version(), source, elapsed_ms, meshes, instances)
    print(f"{NAME} {source['fileName']}: {len(meshes)} meshes, {len(instances)} instances, "
          f"{sum(m[3] for m in meshes)} triangles, {elapsed_ms:.0f} ms -> {out_path}")


if __name__ == "__main__":
    if len(sys.argv) == 2 and sys.argv[1] == "--version":
        print(version())
    else:
        mesh(sys.argv[1], sys.argv[2])
