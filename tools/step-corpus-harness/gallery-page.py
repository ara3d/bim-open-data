"""The STEP mesher gallery: meshes every case with the harness's gallery mode, then writes the three.js page.

    python gallery-page.py run <folder> --harness <step-corpus-harness.exe> [--corpus <u-blox library>] [--samples <data/step-samples>] [--only stem,stem]
    python gallery-page.py page <folder> [out.html] [--previous <folder of an earlier run's .json>] [--label <text>] [--compare-time]

Each case in CASES names its GLB stem, its source and flags, its group, verdict, ticket, title, and a sentence on what
it shows. A verdict of 'auto' is read from the sidecar by auto_verdict (what did not mesh and why, else closed and
oriented and the contract), so the operations group never carries a hand-typed verdict; the page's status table
(summary) counts cases, passes, faces meshed, triangles and face time per group from the same manifest. A source is `witness:<Name>[:x]`, `corpus:<path in the u-blox library>` or `sample:<path in data/step-samples>`,
then any gallery-mode flags; corpus and sample cases run with --warmup. `run` writes <stem>.glb and <stem>.json per case.
`page` reads the sidecars, so the page never carries a count the mesher did not produce; with --previous it adds what
changed since that run (faces meshed, edges and checks, and with --compare-time the warm whole-file time when both
runs were warm). The page loads three.js
from jsDelivr and each GLB from a base64 .glb.txt beside itself, which `page` also writes (the artifact host serves
text files, not model/gltf-binary).
"""
import argparse
import base64
import json
import shlex
import subprocess
import sys
import time
from pathlib import Path

TOOLKIT = Path(__file__).resolve().parents[4]
DEFAULT_CORPUS = Path("C:/Users/cdigg/git/3d-format-shootout/data/submodules/3D-Step-Models-Library")
DEFAULT_SAMPLES = TOOLKIT / "data" / "step-samples"

# (stem, source and flags, group, verdict, ticket, title, what it shows)
CASES = [
    ("w-UnitBox", "witness:UnitBox --chordal 0.01", "witness", "ok", "TKT-314", "Unit box",
     "Chunk A, the cube through every layer. Six faces, one quad each: the grid path with no refinement."),
    ("w-Cylinder", "witness:Cylinder --chordal 0.01", "witness", "ok", "TKT-324", "Cylinder",
     "The seam cut and the two circles. The side is a quad strip (TKT-405); the caps are refined triangles."),
    ("w-Sphere", "witness:Sphere --chordal 0.01", "witness", "ok", "TKT-325", "Sphere",
     "Closed in two directions with two poles, where the chart collapses a line to a point."),
    ("w-Torus", "witness:Torus --chordal 0.01", "witness", "ok", "TKT-325", "Torus",
     "Closed both ways with no pole: the periodic chart needs two seam cuts and the grid meshes it in quads."),
    ("w-Cone", "witness:Cone --chordal 0.01", "witness", "ok", "TKT-325", "Cone",
     "The apex is a degenerate line of the chart; the triangles meet it without a sliver."),
    ("w-BlockWithThroughHole", "witness:BlockWithThroughHole --chordal 0.01", "witness", "ok", "TKT-328", "Block with a through hole",
     "Genus one: two faces carry an inner loop; the hole's cylinder faces close the shell."),
    ("w-HollowBox", "witness:HollowBox --chordal 0.01", "witness", "ok", "TKT-328", "Hollow box",
     "A void: an inner shell whose faces point inward, so the solid's volume is the shell difference."),
    ("w-BoxWithFlippedFace", "witness:BoxWithFlippedFace --chordal 0.01", "witness", "caught", "TKT-328", "Box with a flipped face",
     "One face reversed in the file. The mesh is closed but the check reports four misoriented edges: the defect is found, not hidden."),
    ("w-TwoBoxesSharingCorner", "witness:TwoBoxesSharingCorner --chordal 0.01", "witness", "caught", "TKT-328", "Two boxes sharing a corner",
     "A pinched vertex. The edge checks pass (closed, oriented); only the vertex-component check sees the pinch."),
    ("w-BumpSolid", "witness:BSplinePatchSolid:0.3 --chordal 0.01", "witness", "wrong", "TKT-415", "Bump solid, bump 0.3",
     "A cube whose top is a B-spline patch raised 0.3 in the middle. The four-edge face takes the quad-grid path and comes out as one flat quad: the mesh hash equals the unit box's. The chordal bound is tested along the straight edges, where the deviation is zero, so the bump vanishes. Filed from this gallery as TKT-415; the fix (F1, a grid kept only within the surface chordal bound) is staged, not yet in the library."),
    ("o-Hexagon", "witness:Hexagon --chordal 0.01", "operation", "auto", "TKT-482", "Hexagonal prism",
     "Six planes meeting at 120 degrees: the plainest prism beyond the box."),
    ("o-LBracket", "witness:LBracket --chordal 0.01", "operation", "auto", "TKT-482", "L bracket",
     "A concave prism: the inner corner is a reflex vertex, so the caps are non-convex polygons."),
    ("o-Chamfer", "witness:Chamfer --chordal 0.01", "operation", "auto", "TKT-482", "Chamfered edge",
     "A box with one long edge cut at 45 degrees: a seventh plane, tangent to nothing."),
    ("o-Fillet", "witness:Fillet --chordal 0.01", "operation", "auto", "TKT-482", "Filleted edge",
     "A box with one long edge rounded: a quarter cylinder tangent to two planes, the commonest curved face in any part."),
    ("o-ExtrudedFillet", "witness:ExtrudedFillet --chordal 0.01", "operation", "auto", "TKT-482", "Fillet as an extrusion surface",
     "The same fillet written as a surface of linear extrusion of its arc, as some exporters write swept faces, instead of a cylinder."),
    ("o-InnerFillet", "witness:InnerFillet --chordal 0.01", "operation", "auto", "TKT-482", "Inner fillet",
     "The L bracket with its reflex corner filled: a concave quarter cylinder, its normal reversed."),
    ("o-Slot", "witness:Slot --chordal 0.01", "operation", "auto", "TKT-482", "Slot",
     "A stadium-shaped through slot: two half cylinders and two planes bound the hole; the caps carry a four-edge inner loop."),
    ("o-DShaft", "witness:DShaft --chordal 0.01", "operation", "auto", "TKT-482", "D-shaft",
     "A cylinder with a flat: the band is one arc face with four edges, meeting a plane along two lines."),
    ("o-Pyramid", "witness:Pyramid --chordal 0.01", "operation", "auto", "TKT-482", "Pyramid",
     "Four triangles share the apex, a vertex of valence four; the base is a square."),
    ("o-BlindHole", "witness:BlindHole --chordal 0.01", "operation", "auto", "TKT-482", "Blind hole",
     "A round hole that does not go through: the top plane carries a circular inner loop, the wall is a cylinder band with its normal reversed, the floor a disk."),
    ("o-Boss", "witness:Boss --chordal 0.01", "operation", "auto", "TKT-482", "Boss",
     "The blind hole's three faces with every normal the other way: a cylinder standing on a plane."),
    ("o-Counterbore", "witness:Counterbore --chordal 0.01", "operation", "auto", "TKT-482", "Counterbore",
     "A through hole with a wider mouth: two cylinder bands and an annular shoulder, genus one."),
    ("o-Countersink", "witness:Countersink --chordal 0.01", "operation", "auto", "TKT-482", "Countersink",
     "A through hole with a 45 degree cone at its mouth: a cone band between two circles, no apex."),
    ("o-SphereDimple", "witness:SphereDimple --chordal 0.01", "operation", "auto", "TKT-482", "Spherical dimple",
     "A shallow spherical pocket in a plane: one sphere face bounded by a single circle, holding a pole, normal reversed."),
    ("o-SphereDome", "witness:SphereDome --chordal 0.01", "operation", "auto", "TKT-482", "Spherical dome",
     "The dimple's cap turned up: a sphere face with one circular bound and the pole inside it."),
    ("o-OpenBox", "witness:OpenBox --chordal 0.01", "operation", "auto", "TKT-482", "Shelled open box",
     "A box shelled to a 0.3 wall and open at the top: the top face is an annulus with a square inner loop; the cavity's walls and floor face inward."),
    ("o-Tube", "witness:Tube --chordal 0.01", "operation", "auto", "TKT-482", "Tube",
     "A cylinder with a coaxial hole: annular caps, an outer band and an inner band whose normal points into the hole."),
    ("o-ObliqueCut", "witness:ObliqueCut --chordal 0.01", "operation", "auto", "TKT-482", "Oblique cut",
     "A cylinder cut by a plane at 30 degrees: the cut is an ellipse edge, shared by the band and the cap. ODIN-W160 has such cuts (TKT-409)."),
    ("o-TorusFillet", "witness:TorusFillet --chordal 0.01", "operation", "auto", "TKT-482", "Rounded puck",
     "A cylinder whose top edge is rounded: a torus band between the cylinder band and the top disk, tangent to both."),
    ("o-RevolvedBarrel", "witness:RevolvedBarrel --chordal 0.01", "operation", "auto", "TKT-482", "Revolved barrel",
     "An arc revolved about the axis, written as a surface of revolution rather than a torus: the revolve operation as exporters write it."),
    ("o-RevolvedCone", "witness:RevolvedCone --chordal 0.01", "operation", "auto", "TKT-482", "Revolved cone",
     "A truncated cone written as a revolved line, not a conical surface: the countersink's cone by the other surface kind."),
    ("o-CrossHole", "witness:CrossHole --chordal 0.01", "operation", "auto", "TKT-482", "Cross hole",
     "A smaller cylinder bored crosswise through a larger one. The two intersection curves are quartics with no rational form, so they are cubic B-splines through 97 points of the true curve, within 3.5e-7 of both cylinders: the only B-spline edges among the operations, and the gallery's test of the B-spline edge path (TKT-410)."),
    ("c-emmy-w1", "corpus:SHO/EMMY-W1.STEP", "part", "ok", "TKT-323", "EMMY-W1",
     "The smallest fully meshed part, closed and oriented. It took 10.6 s on Oct 19."),
    ("c-nina-b400", "corpus:SHO/NINA-B400.STEP", "part", "ok", "TKT-323", "NINA-B400",
     "A shielded module with many pins. Every face meshed, most by the quad grid."),
    ("c-odin-w160", "corpus:SHO/ODIN-W160.STEP", "part", "ok", "TKT-409", "ODIN-W160",
     "Hung for 358 s on Oct 19; now closed and oriented in a couple of seconds. Its slowest faces are two 80-edge planes. Four ellipse-bounded oblique cylinder cuts fall back to CDT (TKT-409)."),
    ("z-odin-plane212", "corpus:SHO/ODIN-W260.STEP --faces 3,34", "part", "slow", "TKT-349", "ODIN-W260, the two 212-edge planes",
     "The faces of TKT-349 (86 s to over 8 minutes before the complexity sweep). Face 34 took 1.2 s on Oct 21 and 0.2 s now, the same 2,328 triangles: TKT-407's allocation-free inside test."),
    ("z-odin-slowcyl", "corpus:SHO/ODIN-W260.STEP --faces 2584,2579 --ring 1", "part", "slow", "TKT-349", "ODIN-W260, the slowest cylinder faces",
     "Twelve-edge cylinder faces that the grid refuses and CDT refines. On Oct 21 they took 1.2 to 1.3 s and about 970 triangles each; now about 0.1 s and 805 triangles, since TKT-406 stops insertions the 15 degree test can never satisfy."),
    ("c-lena-r8", "corpus:CEL/LENA-R8.STEP", "failure", "holes", "TKT-410", "LENA-R8",
     "488 of its 562 B-spline edges throw the compiler's stub for SagittaByHullBoundGiven, so 355 faces have no boundary to mesh. The red outlines are the edges of unmeshed faces that did discretize; the stubbed edges leave nothing to draw."),
    ("z-lena-r8-spheres", "corpus:CEL/LENA-R8.STEP --faces 202,203,204,205,216,217,218,219 --ring 1", "failure", "threw", "TKT-411", "LENA-R8, three-edge sphere faces",
     "Eight sphere faces bounded by one loop of three edges throw an index-out-of-range in layer 5. Shown with their neighbours; the red triangles are the unmeshed faces."),
    ("c-lara", "corpus:CEL/LARA.STEP", "failure", "holes", "TKT-410", "LARA",
     "566 B-spline edges throw the stub; 347 faces unmeshed. The same cause as LENA-R8 on a different antenna."),
    ("c-rcb-f9tap214", "corpus:\"POS/RCB-F9T(AP214).STEP\"", "failure", "holes", "TKT-410", "RCB-F9T (AP214)",
     "The largest file, 8,475 faces. 824 are lost to the stub; everything else meshes. Its slowest face is now a 559-edge plane at about 2 s."),
    ("c-m2-zed-f9t-25b-00", "corpus:POS/M2-ZED-F9T-25B-00.STEP", "failure", "holes", "TKT-410", "M2-ZED-F9T-25B-00",
     "1,004 edges throw the stub and 648 faces go unmeshed; one cone face throws TKT-411's index error."),
    ("z-m2zed-cone", "corpus:POS/M2-ZED-F9T-25B-00.STEP --faces 753 --ring 1", "failure", "threw", "TKT-411", "M2-ZED-F9T-25B-00, cone face 753",
     "A single three-edge cone face that throws, with its two neighbours meshed around it."),
    ("c-nora-b100", "corpus:SHO/NORA-B100.step", "failure", "holes", "TKT-410", "NORA-B100",
     "153 faces lost to the stub and one three-edge cone face to TKT-411."),
    ("c-maya-w260_revc", "corpus:SHO/MAYA-W260_REVC.step", "failure", "bad-edges", "TKT-413", "MAYA-W260_REVC",
     "The only kind of file whose meshed faces disagree: 4 non-manifold and 4 misoriented edges in the assembled mesh (TKT-413), plus 13 planes reported not simple (TKT-412) and 7 faces that throw (TKT-411)."),
    ("z-maya-notsimple", "corpus:SHO/MAYA-W260_REVC.step --faces 1481,1483,1499,1504,1509,1510 --ring 1", "failure", "not-simple", "TKT-412", "MAYA-W260_REVC, planes reported not simple",
     "Six 12-edge planes whose boundary segments come within the parameter tolerance of each other in UV. Whether the file self-intersects or the tolerance is too loose is the ticket's question. Nothing meshed: only the red loops."),
    ("z-maya-cylinders", "corpus:SHO/MAYA-W260_REVC.step --faces 1566,1577 --ring 1", "failure", "threw", "TKT-411", "MAYA-W260_REVC, eight-edge cylinder faces",
     "Two cylinder faces with one loop of eight edges that throw the same index error as the three-edge spheres and cones."),
    ("c-odin-w260", "corpus:SHO/ODIN-W260.STEP", "failure", "not-simple", "TKT-412", "ODIN-W260",
     "2,710 of 2,714 faces; the four missing planes are reported not simple. It hung in October."),
    ("z-odin-notsimple", "corpus:SHO/ODIN-W260.STEP --faces 2613,2615,2616,2625 --ring 1", "failure", "not-simple", "TKT-412", "ODIN-W260, the four not-simple planes",
     "Tiny planes with 2 to 6 edges at the board's edge, with their neighbours. Red loops mark the four."),
    ("p-io1-tc-214", "sample:steptools-ap214/io1-tc-214.stp", "public", "ok", "TKT-323", "STEP Tools io1-tc-214 (AP214)",
     "The public AP214 sample the epic's fourth criterion names. 48 surfaces of revolution and 10 planes, closed and outward."),
    ("p-nist_ftc_06_asme1_rd", "sample:\"nist/NIST-PMI-STEP-Files/AP203 geometry only/nist_ftc_06_asme1_rd.stp\"", "public", "ok", "TKT-323", "NIST FTC 06",
     "A NIST test part with planes, cylinders, tori, spheres and cones, meshed closed and oriented."),
    ("p-cubcylso", "sample:steptools-ap203/cubcylso.stp", "public", "ok", "TKT-323", "STEP Tools cube and cylinder",
     "A cube with a cylinder: planes on the quad grid, the cylinder by CDT."),
    ("p-nist_ctc_01_asme1_rd", "sample:\"nist/NIST-PMI-STEP-Files/AP203 geometry only/nist_ctc_01_asme1_rd.stp\"", "public", "holes", "TKT-410", "NIST CTC 01",
     "A few cylinder faces lose an edge to the compiler stub; the rest of the part meshes."),
    ("p-moon_buggy_asm", "sample:steptools-ap203/moon_buggy_asm.stp --faces 14,21,23", "public", "slow", "TKT-428", "STEP Tools moon buggy, three full spheres",
     "Whole spheres with no boundary edges, each refined to about 19,000 triangles in about 6 s, most of them near the poles (TKT-428; the units are right). The whole assembly's mesh is too large to publish. The full file also loses 20 faces to the stub (TKT-410), its 180 misoriented edges come from torus bands meshed on the wrong half of the tube (TKT-429), and its axle cylinder misses the quad grid (TKT-430)."),
    ("p-block", "sample:steptools-ap203/block.stp", "public", "holes", "TKT-410", "STEP Tools block",
     "A plain box, and nothing meshes: its 12 line edges are wrapped in INTERSECTION_CURVE, a surface-curve kind that throws the stub on every edge."),
]

GROUPS = [
    ("witness", "Primitives the library proves itself on", "The library's own witness models, meshed at a chordal bound of 0.01 on a one-unit model. Each is a law's subject."),
    ("operation", "First-level operations", "One solid per operation a modeller makes in one step: a chamfer, a fillet, a slot, a hole, a boss, a cut, a revolve. Plato witnesses (sandbox/step-brep-mesh/07-validation/operation-witnesses.library.plato) meshed at a chordal bound of 0.01 on parts one to four units across; each is also a law subject. The verdict is read from the sidecar, not typed."),
    ("part", "Real parts that mesh completely", "u-blox antenna and module models from the corpus, every face meshed, closed and oriented."),
    ("public", "Public samples", "Files from STEP Tools and NIST, fetched by scripts/fetch-step-samples.ps1: the first files outside the u-blox corpus."),
    ("failure", "Failures, each with a ticket", "Corpus files and close-ups where faces are missing or wrong. Red lines are the discretized edges of faces that did not mesh."),
]

VERDICTS = {
    "ok": ("Meshed, closed, oriented", "ok"),
    "caught": ("Defect in the file, caught by the checks", "ok"),
    "slow": ("Meshed, slow", "warn"),
    "wrong": ("Meshed wrong", "bad"),
    "holes": ("Faces missing: compiler stub", "bad"),
    "threw": ("Faces missing: library threw", "bad"),
    "not-simple": ("Faces missing: reported not simple", "warn"),
    "bad-edges": ("Non-manifold and misoriented edges", "bad"),
}


def command(source: str, glb: Path, harness: str, corpus: Path, samples: Path):
    """The harness command line for one case's source."""
    target, *flags = shlex.split(source)
    kind, _, rest = target.partition(":")
    if kind == "witness":
        return [harness, "gallery", target, str(glb), "--parallel", *flags]
    root = corpus if kind == "corpus" else samples
    return [harness, "gallery", str(root / rest), str(glb), "--parallel", "--warmup", *flags]


def run(folder: Path, harness: str, corpus: Path, samples: Path, only: set):
    folder.mkdir(parents=True, exist_ok=True)
    for stem, source, *_ in CASES:
        if only and stem not in only:
            continue
        start = time.time()
        result = subprocess.run(command(source, folder / f"{stem}.glb", harness, corpus, samples), capture_output=True, text=True, timeout=600)
        lines = [l for l in result.stdout.splitlines() if l.startswith("gallery ")] or result.stderr.strip().splitlines()[-1:] or ["no output"]
        print(f"{stem:28} {time.time() - start:6.1f} s  exit {result.returncode}  {lines[0]}", flush=True)


def change(j: dict, p, compare_time: bool):
    """What moved between an earlier sidecar p and this one j, or None when there is no earlier run of the case."""
    if p is None:
        return None
    warm = j.get("warm", False) and p.get("warm", False)

    def pair(a, b):
        return None if a == b else [a, b]

    return {
        "sameMesh": (j.get("digest") or {}).get("Hash") == (p.get("digest") or {}).get("Hash"),
        "faces": pair(p["faces"], j["faces"]), "meshed": pair(p["meshedFaces"], j["meshedFaces"]),
        "triangles": pair(p["triangles"], j["triangles"]), "edgesThrew": pair(p["edgesThrew"], j["edgesThrew"]),
        "boundary": pair(p["check"]["BoundaryEdges"], j["check"]["BoundaryEdges"]),
        "nonmanifold": pair(p["check"]["NonManifoldEdges"], j["check"]["NonManifoldEdges"]),
        "misoriented": pair(p["check"]["MisorientedEdges"], j["check"]["MisorientedEdges"]),
        "ms": [p["ms"]["total"], j["ms"]["total"]] if warm and compare_time else None,
    }


def auto_verdict(j: dict) -> str:
    """The verdict a sidecar supports, for a case whose verdict is 'auto': what did not mesh and why, else whether the
    assembled mesh is closed and oriented and the version 1 contract holds."""
    unmeshed = j.get("unmeshed", [])
    if unmeshed:
        branches = {u.get("branch") for u in unmeshed}
        if any(u.get("edgesThrew") for u in unmeshed) or j.get("edgesThrew"):
            return "holes"
        if "not-simple" in branches:
            return "not-simple"
        return "threw"
    c = j["check"]
    if not (c["Closed"] and c["Oriented"]) or c["NonManifoldEdges"] or c["MisorientedEdges"]:
        return "bad-edges"
    if str((j.get("contract") or {}).get("verdict", "")).startswith("contract broken"):
        return "wrong"
    return "ok"


def summary(cases: list) -> list:
    """One row per group and one for all: cases, cases whose verdict is a pass (tone ok), faces meshed of drawn,
    triangles, and the meshing time of the drawn faces, read from the manifest so the table says what the cards say."""
    rows = []
    for key, title, _ in [*GROUPS, ("all", "All", "")]:
        picked = [c for c in cases if key == "all" or c["group"] == key]
        rows.append({
            "group": title, "cases": len(picked), "ok": sum(1 for c in picked if VERDICTS[c["verdict"]][1] == "ok"),
            "meshed": sum(c["meshed"] for c in picked), "drawn": sum(c["drawn"] for c in picked),
            "triangles": sum(c["triangles"] for c in picked), "ms": sum((c["drawnMs"] or c["ms"]) for c in picked),
        })
    return rows


def manifest(folder: Path, previous, compare_time: bool):
    out = []
    for stem, source, group, verdict, ticket, title, blurb in CASES:
        j = json.loads((folder / f"{stem}.json").read_text(encoding="utf-8"))
        prev = previous / f"{stem}.json" if previous else None
        c = j["check"]
        if verdict == "auto":
            verdict = auto_verdict(j)
        out.append({
            "source": source, "warm": j.get("warm", False), "drawnMs": j.get("drawnMs"),
            "change": change(j, json.loads(prev.read_text(encoding="utf-8")) if prev and prev.exists() else None, compare_time),
            "id": stem, "file": f"{stem}.glb", "group": group, "verdict": verdict, "ticket": ticket, "title": title, "blurb": blurb,
            "faces": j["faces"], "drawn": j["drawnFaces"], "meshed": j["meshedFaces"], "edgesThrew": j["edgesThrew"],
            "triangles": j["triangles"], "ms": j["ms"]["total"], "size": j["size"],
            "closed": c["Closed"], "oriented": c["Oriented"], "boundary": c["BoundaryEdges"], "nonmanifold": c["NonManifoldEdges"], "misoriented": c["MisorientedEdges"],
            "branches": j["branches"], "glbBytes": (folder / f"{stem}.glb").stat().st_size,
        })
    return out


PAGE = r"""<meta charset="utf-8">
<title>STEP Mesher Gallery</title>
<meta name="claude:viewport-fit-sides" content="cover">
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=IBM+Plex+Sans+Condensed:wght@500;600;700&family=IBM+Plex+Sans:wght@400;500;600&family=IBM+Plex+Mono:wght@400;500&display=swap">
<style>
/* Layout: a sticky viewer on top, then one section per group of cases, each a grid of cards with a rendered thumbnail. */
:root{--bg:#f4f5f7;--panel:#fff;--fg:#141820;--fg2:#4d5664;--fg3:#7b8493;--line:#d9dee6;--line2:#edf0f4;--accent:#2a78d6;
  --ok:#008300;--okbg:#e2f3e2;--warn:#b97b00;--warnbg:#fff1d2;--bad:#d63a3a;--badbg:#fde3e3;--scene:#e9ecf1;
  --display:"IBM Plex Sans Condensed",system-ui,sans-serif;--body:"IBM Plex Sans",system-ui,sans-serif;--mono:"IBM Plex Mono",ui-monospace,Consolas,monospace}
@media (prefers-color-scheme:dark){:root:not([data-theme="light"]){--bg:#14171c;--panel:#1c2026;--fg:#eef1f5;--fg2:#b4bcc8;--fg3:#848e9d;--line:#30363f;--line2:#262b33;--accent:#3987e5;
  --ok:#3fb43f;--okbg:#1b3220;--warn:#e0a92a;--warnbg:#382d10;--bad:#e66767;--badbg:#3b2022;--scene:#22262d;color-scheme:dark}}
:root[data-theme="dark"]{--bg:#14171c;--panel:#1c2026;--fg:#eef1f5;--fg2:#b4bcc8;--fg3:#848e9d;--line:#30363f;--line2:#262b33;--accent:#3987e5;
  --ok:#3fb43f;--okbg:#1b3220;--warn:#e0a92a;--warnbg:#382d10;--bad:#e66767;--badbg:#3b2022;--scene:#22262d;color-scheme:dark}
*{box-sizing:border-box}
body{margin:0;background:var(--bg);color:var(--fg);font-family:var(--body);font-size:13.5px;line-height:1.45;
  padding-left:max(16px,env(safe-area-inset-left,0px));padding-right:max(16px,env(safe-area-inset-right,0px));padding-block:18px 40px}
.wrap{max-width:1360px;margin:0 auto;display:grid;gap:18px}
h1,h2,h3{font-family:var(--display);margin:0;text-wrap:balance}
h1{font-size:28px;font-weight:700}
h2{font-size:20px;font-weight:600}
h3{font-size:15px;font-weight:600}
header p{margin:4px 0 0;color:var(--fg2);max-width:80ch}
.num{font-family:var(--mono);font-variant-numeric:tabular-nums}
.viewer{position:sticky;top:env(safe-area-inset-top,0px);z-index:5;background:var(--panel);border:1px solid var(--line);border-radius:6px;overflow:hidden;display:grid;grid-template-columns:1fr minmax(240px,320px)}
.viewer canvas{display:block;width:100%;height:100%}
.stage{position:relative;background:var(--scene);min-height:340px;height:40vh}
.stage .hint{position:absolute;left:10px;bottom:8px;font-size:11px;color:var(--fg3);pointer-events:none}
.stage .spin{position:absolute;inset:0;display:grid;place-items:center;color:var(--fg3);font-size:13px;pointer-events:none}
.side{padding:12px 14px;display:grid;gap:8px;align-content:start;border-left:1px solid var(--line);min-width:0;overflow:auto}
.side h3{font-size:16px}
.side p{margin:0;color:var(--fg2)}
.toggles{display:flex;flex-wrap:wrap;gap:6px}
.toggles label{display:inline-flex;gap:5px;align-items:center;font-size:12px;padding:4px 8px;border:1px solid var(--line);border-radius:4px;cursor:pointer;background:var(--bg)}
.toggles input{margin:0}
.kv{display:grid;grid-template-columns:auto 1fr;gap:2px 10px;font-size:12px;color:var(--fg2)}
.kv b{font-weight:500;color:var(--fg);font-family:var(--mono);font-size:11.5px}
.badge{display:inline-flex;align-items:center;gap:6px;font-size:11px;font-weight:500;padding:3px 8px;border-radius:3px;white-space:nowrap}
.badge::before{content:"";width:7px;height:7px;border-radius:50%;background:currentColor}
.badge.ok{color:var(--ok);background:var(--okbg)}.badge.warn{color:var(--warn);background:var(--warnbg)}.badge.bad{color:var(--bad);background:var(--badbg)}
.tk{font-family:var(--mono);font-size:11px;color:var(--fg3)}
section{display:grid;gap:10px}
section>p{margin:0;color:var(--fg2);max-width:90ch}
.cards{display:grid;grid-template-columns:repeat(auto-fill,minmax(260px,1fr));gap:12px}
.card{background:var(--panel);border:1px solid var(--line);border-radius:6px;overflow:hidden;display:grid;grid-template-rows:auto 1fr;cursor:pointer;transition:border-color .15s}
.card:hover,.card:focus-visible{border-color:var(--accent);outline:none}
.card.active{border-color:var(--accent);box-shadow:0 0 0 2px var(--accent)}
.thumb{aspect-ratio:3/2;background:var(--scene);display:grid;place-items:center;color:var(--fg3);font-size:11px;position:relative}
.thumb img{width:100%;height:100%;object-fit:cover;display:block}
.thumb .badge{position:absolute;left:8px;top:8px}
.body{padding:10px 12px;display:grid;gap:5px;align-content:start}
.body h3{display:flex;justify-content:space-between;gap:8px;align-items:baseline}
.body p{margin:0;color:var(--fg2);font-size:12.5px}
.stats{display:flex;flex-wrap:wrap;gap:4px 12px;font-size:11.5px;color:var(--fg3)}
.stats b{font-weight:500;color:var(--fg2);font-family:var(--mono)}
.legend{display:flex;flex-wrap:wrap;gap:6px 14px;font-size:11.5px;color:var(--fg2)}
.legend span::before{content:"";display:inline-block;width:10px;height:10px;border-radius:2px;margin-right:5px;vertical-align:-1px;background:var(--c)}
.chg{font-size:11.5px;color:var(--fg2);border-top:1px dashed var(--line);padding-top:5px;margin-top:2px}
.chg b{font-family:var(--mono);font-weight:500}
.chg .up{color:var(--ok)}.chg .down{color:var(--bad)}
.label{font-size:12px;color:var(--fg3);margin:6px 0 0}
footer{font-size:11.5px;color:var(--fg3);border-top:1px solid var(--line);padding-top:10px;line-height:1.6}
footer code{font-family:var(--mono)}
.summary{border-collapse:collapse;margin-top:10px;font-size:13px}
.summary th,.summary td{text-align:right;padding:3px 10px;border-bottom:1px solid var(--line2)}
.summary th:first-child,.summary td:first-child{text-align:left}
.summary th{color:var(--fg3);font-weight:500}
.summary tr:last-child td{font-weight:600;border-top:1px solid var(--line)}
.summary .pass-all{color:var(--ok)}.summary .pass-some{color:var(--warn)}
@media (max-width:760px){.viewer{grid-template-columns:1fr}.side{border-left:0;border-top:1px solid var(--line)}.stage{height:40vh;min-height:260px}}
@media (prefers-reduced-motion:reduce){.card{transition:none}}
</style>

<div class="wrap">
<header>
  <h1>STEP Mesher Gallery</h1>
  <p>What the Plato STEP mesher produces today, in three.js: the library's own witness solids, real u-blox parts that mesh completely, and the failures with their tickets. Click a card to load it into the viewer; drag to orbit, scroll to zoom. Faces are coloured by surface kind; thin dark lines are every discretized edge; red lines are the edges of faces that did not mesh.</p>
  <div class="legend" style="margin-top:8px">
    <span style="--c:#b8c2d1">plane</span><span style="--c:#eb6834">cylinder</span><span style="--c:#eda100">cone</span><span style="--c:#1baf7a">sphere</span><span style="--c:#e87ba4">torus</span><span style="--c:#008300">revolution</span><span style="--c:#6250d6">extrusion</span><span style="--c:#2a78d6">B-spline</span><span style="--c:#e34948">edges of unmeshed faces</span>
  </div>
  <p class="label">__LABEL__</p>
  <table class="summary" id="summary"><thead><tr><th>Group</th><th>Cases</th><th>Pass</th><th>Faces meshed</th><th>Triangles</th><th>Face time</th></tr></thead><tbody></tbody></table>
</header>

<div class="viewer" id="viewer">
  <div class="stage" id="stage"><div class="spin" id="spin">Pick a case below</div><div class="hint">drag: orbit · wheel: zoom · right-drag: pan</div></div>
  <div class="side">
    <div id="vbadge"></div>
    <h3 id="vtitle">Nothing loaded</h3>
    <p id="vblurb"></p>
    <div class="kv" id="vkv"></div>
    <div id="vchg"></div>
    <div class="toggles">
      <label><input type="checkbox" id="tFaces" checked>faces</label>
      <label><input type="checkbox" id="tEdges" checked>edges</label>
      <label><input type="checkbox" id="tFailed" checked>unmeshed outlines</label>
      <label><input type="checkbox" id="tWire">wireframe</label>
    </div>
  </div>
</div>

<div id="sections"></div>

<footer id="foot"></footer>
</div>

<script id="manifest" type="application/json">__MANIFEST__</script>
<script id="summary-rows" type="application/json">__SUMMARY__</script>
<script src="https://cdn.jsdelivr.net/npm/three@0.128.0/build/three.min.js"></script>
<script src="https://cdn.jsdelivr.net/npm/three@0.128.0/examples/js/loaders/GLTFLoader.js"></script>
<script src="https://cdn.jsdelivr.net/npm/three@0.128.0/examples/js/controls/OrbitControls.js"></script>
<script>
(function(){
  const cases = JSON.parse(document.getElementById('manifest').textContent);
  const groups = __GROUPS__;
  const verdicts = __VERDICTS__;
  const byId = Object.fromEntries(cases.map(c => [c.id, c]));
  const css = n => getComputedStyle(document.documentElement).getPropertyValue(n).trim();
  const fmt = n => n.toLocaleString('en-US');
  // Meshing time of the drawn faces summed over the cores' rows; the whole file's time when an older sidecar lacks it.
  const ft = c => { const t = c.drawnMs ?? c.ms; return t >= 1000 ? (t / 1000).toFixed(1) + ' s' : t.toFixed(0) + ' ms'; };
  // What moved since the previous run. "better" says which direction is an improvement for each measure.
  const better = { meshed: 1, triangles: 0, edgesThrew: -1, boundary: -1, nonmanifold: -1, misoriented: -1, ms: -1, faces: 0 };
  const names = { faces: 'faces', meshed: 'faces meshed', triangles: 'triangles', edgesThrew: 'edges that threw', boundary: 'boundary edges', nonmanifold: 'non-manifold', misoriented: 'misoriented', ms: 'warm time' };
  function changeHtml(c) {
    const ch = c.change;
    if (!ch) return '<div class="chg">New case: no earlier run.</div>';
    const parts = [];
    for (const k of Object.keys(names)) {
      const v = ch[k]; if (!v) continue;
      const [a, b] = v, dir = Math.sign(b - a) * better[k];
      const show = x => k === 'ms' ? (x >= 1000 ? (x / 1000).toFixed(1) + ' s' : x.toFixed(0) + ' ms') : fmt(x);
      if (k === 'ms' && Math.abs(b - a) < 0.1 * a) continue;
      parts.push(`<span class="${dir > 0 ? 'up' : dir < 0 ? 'down' : ''}">${names[k]} <b>${show(a)} → ${show(b)}</b></span>`);
    }
    const head = ch.sameMesh ? 'Same mesh as Oct 21' : 'Changed since Oct 21';
    return `<div class="chg">${head}${parts.length ? ': ' + parts.join(', ') : '.'}</div>`;
  }

  // The status table: rows made by the page script from the same manifest as the cards.
  const tbody = document.querySelector('#summary tbody');
  for (const r of JSON.parse(document.getElementById('summary-rows').textContent)) {
    const tr = document.createElement('tr');
    const tone = r.ok === r.cases ? 'pass-all' : r.ok ? 'pass-some' : '';
    const t = r.ms >= 1000 ? (r.ms / 1000).toFixed(1) + ' s' : r.ms.toFixed(0) + ' ms';
    tr.innerHTML = `<td>${r.group}</td><td class="num">${r.cases}</td><td class="num ${tone}">${r.ok} / ${r.cases}</td><td class="num">${fmt(r.meshed)} / ${fmt(r.drawn)}</td><td class="num">${fmt(r.triangles)}</td><td class="num">${t}</td>`;
    tbody.appendChild(tr);
  }

  // Cards
  const sections = document.getElementById('sections');
  for (const [key, title, intro] of groups) {
    const s = document.createElement('section');
    s.innerHTML = `<h2>${title}</h2><p>${intro}</p><div class="cards"></div>`;
    const grid = s.querySelector('.cards');
    for (const c of cases.filter(c => c.group === key)) {
      const [label, tone] = verdicts[c.verdict];
      const el = document.createElement('article');
      el.className = 'card'; el.tabIndex = 0; el.dataset.id = c.id;
      const missing = c.drawn - c.meshed;
      el.innerHTML = `<div class="thumb" id="th-${c.id}"><span class="badge ${tone}">${label}</span><span>rendering…</span></div>
        <div class="body"><h3><span>${c.title}</span><span class="tk">${c.ticket}</span></h3><p>${c.blurb}</p>
        <div class="stats"><span>faces <b>${fmt(c.meshed)}${missing ? ' / ' + fmt(c.drawn) : ''}</b></span><span>triangles <b>${fmt(c.triangles)}</b></span><span>face time <b>${ft(c)}</b></span><span>${c.closed ? 'closed' : 'open, ' + fmt(c.boundary) + ' boundary edges'}${c.nonmanifold ? ', ' + c.nonmanifold + ' non-manifold' : ''}${c.misoriented ? ', ' + c.misoriented + ' misoriented' : ''}</span></div>${changeHtml(c)}</div>`;
      el.addEventListener('click', () => load(c.id));
      el.addEventListener('keydown', e => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); load(c.id); } });
      grid.appendChild(el);
    }
    sections.appendChild(s);
  }
  document.getElementById('foot').innerHTML = `${cases.length} cases, ${(cases.reduce((a, c) => a + c.glbBytes, 0) / 1048576).toFixed(1)} MB of GLB, written by <code>step-corpus-harness gallery</code>: __LABEL__. Corpus files use the host's chordal bound; witnesses and operations use 0.01 on a model one to four units across. Not shown: ELLA-W1, where every edge throws the stub and no face meshes, and NINA-B222-W1x2, which does not finish edge discretization (TKT-414).`;

  // Shared loader and cache
  const loader = new THREE.GLTFLoader();
  const cache = {};
  // The GLBs travel base64-encoded in .glb.txt files (the artifact host serves text, not model/gltf-binary).
  function fetchCase(id) {
    if (!cache[id]) cache[id] = fetch(byId[id].file + '.txt').then(r => { if (!r.ok) throw new Error(r.status + ' ' + byId[id].file); return r.text(); }).then(b64 => {
      const bin = atob(b64.trim()), buf = new Uint8Array(bin.length);
      for (let i = 0; i < bin.length; i++) buf[i] = bin.charCodeAt(i);
      return new Promise((res, rej) => loader.parse(buf.buffer, '', g => res(g.scene), rej));
    });
    return cache[id];
  }
  function prepare(scene) {
    scene.traverse(o => {
      if (o.isMesh) { o.material.vertexColors = true; o.material.flatShading = true; o.material.needsUpdate = true; o.userData.kind = 'faces'; }
      else if (o.isLineSegments || o.isLine) {
        const red = o.material.color.r > 0.6 && o.material.color.g < 0.3;
        o.userData.kind = red ? 'failed' : 'edges';
        if (red) { o.material.depthTest = false; o.renderOrder = 10; o.material.color.set('#e34948'); }
        else { o.material.color.set(css('--fg')); o.material.opacity = 0.55; o.material.transparent = true; }
      }
    });
    return scene;
  }
  function frame(camera, controls, scene, aspect) {
    const box = new THREE.Box3().setFromObject(scene);
    const size = box.getSize(new THREE.Vector3()), centre = box.getCenter(new THREE.Vector3());
    const radius = Math.max(size.length() / 2, 1e-6);
    camera.up.set(0, 0, 1);
    const dist = radius / Math.sin(THREE.MathUtils.degToRad(camera.fov) / 2) * 1.05 / Math.min(1, aspect);
    camera.position.copy(centre).add(new THREE.Vector3(-0.8, -1.0, 0.75).normalize().multiplyScalar(dist));
    camera.near = dist / 100; camera.far = dist * 10; camera.updateProjectionMatrix();
    camera.lookAt(centre);
    if (controls) { controls.target.copy(centre); controls.update(); }
  }
  function lights(scene) {
    scene.add(new THREE.HemisphereLight(0xffffff, 0x8a8f99, 0.9));
    const d = new THREE.DirectionalLight(0xffffff, 0.7); d.position.set(-1, -2, 3); scene.add(d);
    const d2 = new THREE.DirectionalLight(0xffffff, 0.3); d2.position.set(2, 1, -1); scene.add(d2);
  }

  // Thumbnails: one shared renderer, rendered when the card scrolls into view
  const thumbRenderer = new THREE.WebGLRenderer({ antialias: true, preserveDrawingBuffer: true });
  thumbRenderer.setSize(480, 320); thumbRenderer.setPixelRatio(1);
  async function thumbnail(id) {
    const holder = document.getElementById('th-' + id);
    try {
      const src = await fetchCase(id);
      const scene = new THREE.Scene(); scene.background = new THREE.Color(css('--scene'));
      lights(scene);
      const model = prepare(src.clone(true)); scene.add(model);
      const cam = new THREE.PerspectiveCamera(32, 1.5, 0.01, 1000);
      frame(cam, null, model, 1.5);
      thumbRenderer.render(scene, cam);
      const img = new Image(); img.alt = byId[id].title; img.src = thumbRenderer.domElement.toDataURL('image/png');
      holder.querySelector('span:last-child').replaceWith(img);
    } catch (e) { holder.querySelector('span:last-child').textContent = 'could not load ' + byId[id].file; console.error(e); }
  }
  const io = new IntersectionObserver(entries => { for (const e of entries) if (e.isIntersecting) { io.unobserve(e.target); thumbnail(e.target.dataset.id); } }, { rootMargin: '200px' });
  document.querySelectorAll('.card').forEach(c => io.observe(c));

  // Main viewer
  const stage = document.getElementById('stage');
  const renderer = new THREE.WebGLRenderer({ antialias: true });
  renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
  stage.appendChild(renderer.domElement);
  const scene = new THREE.Scene();
  lights(scene);
  const camera = new THREE.PerspectiveCamera(32, 1, 0.01, 1000);
  const controls = new THREE.OrbitControls(camera, renderer.domElement);
  controls.enableDamping = true;
  let current = null, wire = null;
  function resize() {
    const w = stage.clientWidth, h = stage.clientHeight;
    renderer.setSize(w, h, false); camera.aspect = w / h; camera.updateProjectionMatrix();
  }
  new ResizeObserver(resize).observe(stage); resize();
  function tick() { requestAnimationFrame(tick); scene.background = new THREE.Color(css('--scene')); controls.update(); renderer.render(scene, camera); }
  tick();
  const toggles = { faces: 'tFaces', edges: 'tEdges', failed: 'tFailed' };
  function applyToggles() {
    if (!current) return;
    current.traverse(o => { if (o.userData.kind) o.visible = document.getElementById(toggles[o.userData.kind]).checked; });
    if (wire) wire.visible = document.getElementById('tWire').checked;
  }
  for (const id of [...Object.values(toggles), 'tWire']) document.getElementById(id).addEventListener('change', applyToggles);
  async function load(id) {
    const c = byId[id];
    document.querySelectorAll('.card').forEach(el => el.classList.toggle('active', el.dataset.id === id));
    document.getElementById('spin').textContent = 'loading ' + c.file + '…';
    const [label, tone] = verdicts[c.verdict];
    document.getElementById('vbadge').innerHTML = `<span class="badge ${tone}">${label}</span> <span class="tk">${c.ticket}</span>`;
    document.getElementById('vtitle').textContent = c.title;
    document.getElementById('vblurb').textContent = c.blurb;
    const br = Object.entries(c.branches).map(([k, v]) => `${k} ${fmt(v)}`).join(', ');
    document.getElementById('vkv').innerHTML = `<span>faces drawn</span><b>${fmt(c.drawn)} of ${fmt(c.faces)}</b><span>meshed</span><b>${fmt(c.meshed)}</b><span>triangles</span><b>${fmt(c.triangles)}</b><span>edges that threw</span><b>${fmt(c.edgesThrew)}</b><span>branches</span><b>${br}</b><span>face time, drawn faces</span><b>${ft(c)}</b><span>whole file${c.warm ? ', warm' : ''}</span><b>${(c.ms/1000).toFixed(1)} s</b><span>check</span><b>${c.closed ? 'closed' : 'boundary ' + fmt(c.boundary)}, ${c.oriented ? 'oriented' : 'misoriented ' + c.misoriented}${c.nonmanifold ? ', non-manifold ' + c.nonmanifold : ''}</b><span>extent</span><b>${c.size.map(x => x.toFixed(1)).join(' × ')}</b>`;
    document.getElementById('vchg').innerHTML = changeHtml(c);
    try {
      const src = await fetchCase(id);
      if (current) { scene.remove(current); scene.remove(wire); }
      current = prepare(src.clone(true)); scene.add(current);
      wire = new THREE.Group();
      current.traverse(o => { if (o.isMesh) wire.add(new THREE.Mesh(o.geometry, new THREE.MeshBasicMaterial({ color: css('--fg'), wireframe: true, transparent: true, opacity: 0.35 }))); });
      scene.add(wire);
      frame(camera, controls, current, camera.aspect);
      applyToggles();
      document.getElementById('spin').textContent = '';
      document.getElementById('viewer').scrollIntoView({ block: 'nearest' });
    } catch (e) { document.getElementById('spin').textContent = 'could not load ' + c.file; console.error(e); }
  }
  const first = location.hash.slice(1);
  load(byId[first] ? first : cases[0].id);
})();
</script>
"""


def page(folder: Path, previous, label: str, out: Path, compare_time: bool):
    cases = manifest(folder, previous, compare_time)
    html = (PAGE.replace("__MANIFEST__", json.dumps(cases))
            .replace("__SUMMARY__", json.dumps(summary(cases)))
            .replace("__GROUPS__", json.dumps(GROUPS))
            .replace("__VERDICTS__", json.dumps(VERDICTS))
            .replace("__LABEL__", label or "an unlabelled run"))
    out.write_text(html, encoding="utf-8")
    for stem, *_ in CASES:
        glb = folder / f"{stem}.glb"
        (folder / f"{stem}.glb.txt").write_text(base64.b64encode(glb.read_bytes()).decode("ascii"), encoding="ascii")
    print(f"wrote {out} with {len(CASES)} cases")


def main():
    args = sys.argv[1:]
    if args and args[0] not in ("run", "page", "-h", "--help"):
        args = ["page", *args]
    parser = argparse.ArgumentParser(prog="gallery-page.py")
    sub = parser.add_subparsers(dest="mode", required=True)
    r = sub.add_parser("run")
    r.add_argument("folder", type=Path)
    r.add_argument("--harness", required=True)
    r.add_argument("--corpus", type=Path, default=DEFAULT_CORPUS)
    r.add_argument("--samples", type=Path, default=DEFAULT_SAMPLES)
    r.add_argument("--only", default="")
    g = sub.add_parser("page")
    g.add_argument("folder", type=Path)
    g.add_argument("out", type=Path, nargs="?")
    g.add_argument("--previous", type=Path)
    g.add_argument("--label", default="")
    g.add_argument("--compare-time", action="store_true", help="show warm whole-file time changes; leave off when the machine was shared")
    a = parser.parse_args(args)
    if a.mode == "run":
        run(a.folder, str(Path(a.harness).resolve()), a.corpus, a.samples, {x for x in a.only.split(",") if x})
    else:
        page(a.folder, a.previous, a.label, a.out or a.folder / "index.html", a.compare_time)


if __name__ == "__main__":
    main()
