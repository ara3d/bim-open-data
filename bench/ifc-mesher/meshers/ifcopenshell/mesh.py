"""Meshes one IFC file with IfcOpenShell's geometry iterator and writes a .meshdump.

usage: python mesh.py <model.ifc> <out.meshdump>

One mesh per product, in the product's local frame; the instance carries the product's placement
matrix. Vertices come back in metres (IfcOpenShell's default output units).
"""
import multiprocessing
import os
import sys
import time

import numpy as np
import ifcopenshell
import ifcopenshell.geom

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import meshdump  # noqa: E402

NAME = "IfcOpenShell"


def mesh(ifc_path, out_path):
    source = meshdump.source_of(ifc_path)
    started = time.perf_counter()
    model = ifcopenshell.open(ifc_path)
    settings = ifcopenshell.geom.settings()
    settings.set("use-world-coords", False)
    settings.set("weld-vertices", True)
    iterator = ifcopenshell.geom.iterator(settings, model, multiprocessing.cpu_count())
    meshes = []
    instances = []
    if iterator.initialize():
        while True:
            shape = iterator.get()
            geometry = shape.geometry
            verts = np.asarray(geometry.verts, dtype=np.float32)
            faces = np.asarray(geometry.faces, dtype=np.int32)
            if len(faces) > 0:
                matrix = list(shape.transformation.matrix)
                instances.append((shape.id, len(meshes), matrix))
                meshes.append((verts.tobytes(), faces.tobytes(), len(verts) // 3, len(faces) // 3))
            if not iterator.next():
                break
    elapsed_ms = (time.perf_counter() - started) * 1000
    meshdump.write(out_path, NAME, ifcopenshell.version, source, elapsed_ms, meshes, instances)
    print(f"{NAME} {source['fileName']}: {len(meshes)} meshes, {len(instances)} instances, "
          f"{sum(m[3] for m in meshes)} triangles, {elapsed_ms:.0f} ms -> {out_path}")


if __name__ == "__main__":
    if len(sys.argv) == 2 and sys.argv[1] == "--version":
        print(ifcopenshell.version)
    else:
        mesh(sys.argv[1], sys.argv[2])
