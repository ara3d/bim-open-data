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
    ("w-UnitBox", "witness:UnitBox --chordal 0.01", "witness", "auto", "TKT-314", "Unit box",
     "Chunk A, the cube through every layer. Six faces, one quad each: the grid path with no refinement."),
    ("w-Cylinder", "witness:Cylinder --chordal 0.01", "witness", "auto", "TKT-324", "Cylinder",
     "The seam cut and the two circles. The side is a quad strip (TKT-405); the caps are refined triangles."),
    ("w-Sphere", "witness:Sphere --chordal 0.01", "witness", "auto", "TKT-325", "Sphere",
     "Closed in two directions with two poles, where the chart collapses a line to a point."),
    ("w-Torus", "witness:Torus --chordal 0.01", "witness", "auto", "TKT-325", "Torus",
     "Closed both ways with no pole: the periodic chart needs two seam cuts and the grid meshes it in quads."),
    ("w-Cone", "witness:Cone --chordal 0.01", "witness", "auto", "TKT-325", "Cone",
     "The apex is a degenerate line of the chart; the triangles meet it without a sliver."),
    ("w-BlockWithThroughHole", "witness:BlockWithThroughHole --chordal 0.01", "witness", "auto", "TKT-328", "Block with a through hole",
     "Genus one: two faces carry an inner loop; the hole's cylinder faces close the shell."),
    ("w-HollowBox", "witness:HollowBox --chordal 0.01", "witness", "auto", "TKT-328", "Hollow box",
     "A void: an inner shell whose faces point inward, so the solid's volume is the shell difference."),
    ("w-BoxWithFlippedFace", "witness:BoxWithFlippedFace --chordal 0.01", "witness", "caught", "TKT-328", "Box with a flipped face",
     "One face reversed in the file. The mesh is closed but the check reports four misoriented edges: the defect is found, not hidden."),
    ("w-TwoBoxesSharingCorner", "witness:TwoBoxesSharingCorner --chordal 0.01", "witness", "caught", "TKT-328", "Two boxes sharing a corner",
     "A pinched vertex. The edge checks pass (closed, oriented); only the vertex-component check sees the pinch."),
    ("w-BumpSolid", "witness:BSplinePatchSolid:0.3 --chordal 0.01", "witness", "auto", "TKT-415", "Bump solid, bump 0.3",
     "A cube whose top is a B-spline patch raised 0.3 in the middle. On 2026-10-21 the four-edge face took the quad-grid path and came out as one flat quad, the mesh hash equal to the unit box's (TKT-415, filed from this gallery). Now the top is 38 triangles that follow the bump and the chordal contract holds."),
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
    ("c-emmy-w1", "corpus:SHO/EMMY-W1.STEP", "part", "auto", "TKT-323", "EMMY-W1",
     "The smallest fully meshed part, closed and oriented. It took 10.6 s on Oct 19."),
    ("c-nina-b400", "corpus:SHO/NINA-B400.STEP", "part", "auto", "TKT-323", "NINA-B400",
     "A shielded module with many pins. Every face meshed, most by the quad grid."),
    ("c-odin-w160", "corpus:SHO/ODIN-W160.STEP", "part", "auto", "TKT-409", "ODIN-W160",
     "Hung for 358 s on Oct 19; now closed and oriented in a couple of seconds. Its slowest faces are two 80-edge planes. Four ellipse-bounded oblique cylinder cuts fall back to CDT (TKT-409)."),
    ("z-odin-plane212", "corpus:SHO/ODIN-W260.STEP --faces 3,34", "part", "auto", "TKT-349", "ODIN-W260, the two 212-edge planes",
     "The faces of TKT-349 (86 s to over 8 minutes before the complexity sweep). Face 34 took 1.2 s on Oct 21 and 0.44 s now, 992 triangles for the two."),
    ("z-odin-slowcyl", "corpus:SHO/ODIN-W260.STEP --faces 2584,2579 --ring 1", "part", "auto", "TKT-349", "ODIN-W260, the slowest cylinder faces",
     "Twelve-edge cylinder faces that the grid refuses and CDT refines. On Oct 21 they took 1.2 to 1.3 s each; now about 20 ms, since TKT-406 stops insertions the 15 degree test can never satisfy."),
    ("k-lily-w131", "corpus:SHO/LILY-W131.STEP", "corpus", "auto", "TKT-323", "LILY-W131",
     "One of the 14 corpus files that meshed completely on 2026-10-21: a small Wi-Fi module."),
    ("k-nina-b111", "corpus:SHO/NINA-B111.STEP", "corpus", "auto", "TKT-323", "NINA-B111",
     "A Bluetooth module with a shield can: fully meshed on 2026-10-21."),
    ("k-nina-b3x2", "corpus:SHO/NINA-B3x2.STEP", "corpus", "auto", "TKT-323", "NINA-B3x2",
     "A module with a PCB antenna cut-out: fully meshed on 2026-10-21."),
    ("k-nina-w1x6", "corpus:SHO/NINA-W1x6.STEP", "corpus", "auto", "TKT-323", "NINA-W1x6",
     "The smallest fully meshed file after EMMY-W1."),
    ("k-odin-w262", "corpus:SHO/ODIN-W262.STEP", "corpus", "auto", "TKT-409", "ODIN-W262",
     "ODIN-W160's sibling with a pin header: fully meshed on 2026-10-21."),
    ("k-sam-ap203", "corpus:POS/SAM_AP203.STEP", "corpus", "auto", "TKT-323", "SAM (AP203)",
     "The smallest positioning module, 364 KB, written as AP203; SAM_AP214 is the same part in the other protocol."),
    ("k-max-ap203", "corpus:\"POS/MAX (STEP-AP203).STEP\"", "corpus", "auto", "TKT-323", "MAX (AP203)",
     "A GNSS module with a metal lid, in AP203."),
    ("k-mia-m10q", "corpus:POS/MIA-M10Q.STEP", "corpus", "auto", "TKT-323", "MIA-M10Q",
     "A small GNSS module with castellated edges: many cylinder half-faces along the sides."),
    ("k-neo", "corpus:POS/NEO.STEP", "corpus", "auto", "TKT-323", "NEO",
     "The NEO form factor: a lidded module with a row of pads."),
    ("k-cam", "corpus:POS/CAM.STEP", "corpus", "auto", "TKT-412", "CAM",
     "A positioning module whose planes were reported not simple on 2026-10-21 (TKT-412)."),
    ("k-anna-b4", "corpus:SHO/ANNA-B4.step", "corpus", "auto", "TKT-412", "ANNA-B4",
     "A tiny Bluetooth module; one of the eight files with a not-simple plane on 2026-10-21, and the v1 gate's witness file."),
    ("k-nora-b2x1", "corpus:SHO/NORA-B2x1.step", "corpus", "auto", "TKT-323", "NORA-B2x1",
     "The smallest NORA module: a lidded part with an antenna pin."),
    ("k-jody-w4", "corpus:SHO/JODY-W4.step", "corpus", "auto", "TKT-323", "JODY-W4",
     "A Wi-Fi 6 module with a stamped lid."),
    ("k-r41z", "corpus:SHO/R41Z.STEP", "corpus", "auto", "TKT-323", "R41Z",
     "A module with a chip antenna on a raised pad."),
    ("k-bmd-350", "corpus:SHO/BMD-350.STEP", "corpus", "auto", "TKT-323", "BMD-350",
     "One of the seven BMD modules; the whole family has the same lid and pad pattern."),
    ("k-m2-maya-w271", "corpus:SHO/M2-MAYA-W271.step", "corpus", "auto", "TKT-420", "M2-MAYA-W271",
     "An M.2 card: the file whose 2 non-manifold and 4 misoriented edges come from two needle cone faces (TKT-420), not from a broken loop."),
    ("k-c211", "corpus:SHO/c211_case_with_odin_v1.step", "corpus", "auto", "TKT-468", "c211 case with ODIN",
     "A plastic case around an ODIN module: the file v1-edges took its E5d crossing cases from (faces 81, 84, 88, 92, 93)."),
    ("k-theo-p173", "corpus:SHO/THEO-P173.STEP", "corpus", "auto", "TKT-323", "THEO-P173",
     "A V2X module, 4 MB: the largest SHO file shown whole."),
    ("k-lexi", "corpus:CEL/LEXI.STEP", "corpus", "auto", "TKT-410", "LEXI",
     "The smallest cellular module (4 MB); the CEL files are the ones with the most B-spline edges."),
    ("k-sara", "corpus:CEL/SARA.STEP", "corpus", "auto", "TKT-410", "SARA",
     "A 12 MB cellular module, one of five SARA files that share a body; LENA-R8's cause (the stub on B-spline edges) applies here too."),
    ("k-ella-w1", "corpus:SHO/ELLA-W1.STEP", "corpus", "auto", "TKT-410", "ELLA-W1",
     "The file where every edge was a surface curve and nothing meshed on 2026-10-21 (TKT-410): 7,754 faces."),
    ("k-nina-b222-w1x2", "corpus:SHO/NINA-B222-W1x2.STEP", "corpus", "auto", "TKT-414", "NINA-B222-W1x2",
     "The file whose edge discretization did not finish in 120 s on 2026-10-21 (TKT-414); the gallery gives a case 600 s."),
    ("c-lena-r8", "corpus:CEL/LENA-R8.STEP", "failure", "auto", "TKT-410", "LENA-R8",
     "On 2026-10-21, 488 of its 562 B-spline edges threw the compiler's stub and 355 faces had no boundary to mesh (TKT-410). Now every edge discretizes and 3,310 of 3,311 faces mesh in 10 s warm; the one left, plane 258 with 1,017 edges, is refused as not simple, and the 2,026 boundary edges lie around it."),
    ("z-lena-r8-spheres", "corpus:CEL/LENA-R8.STEP --faces 202,203,204,205,216,217,218,219 --ring 1", "failure", "auto", "TKT-411", "LENA-R8, three-edge sphere faces",
     "Eight sphere faces bounded by one loop of three edges that threw an index-out-of-range in layer 5 on 2026-10-21 (TKT-411). They mesh now, with their neighbours."),
    ("c-lara", "corpus:CEL/LARA.STEP", "failure", "auto", "TKT-410", "LARA",
     "566 B-spline edges threw the stub and 347 faces went unmeshed on 2026-10-21 (TKT-410). Now all 3,281 faces mesh, closed and oriented, in 6.4 s warm."),
    ("c-rcb-f9tap214", "corpus:\"POS/RCB-F9T(AP214).STEP\"", "failure", "auto", "TKT-410", "RCB-F9T (AP214)",
     "The largest file, 8,475 faces; 824 were lost to the stub on 2026-10-21. Now every face meshes, closed and oriented, in 18 s warm; the slowest face is a four-edge B-spline at 1.1 s."),
    ("c-m2-zed-f9t-25b-00", "corpus:POS/M2-ZED-F9T-25B-00.STEP", "failure", "auto", "TKT-410", "M2-ZED-F9T-25B-00",
     "1,004 edges threw the stub and 648 faces went unmeshed on 2026-10-21, and one cone face threw TKT-411's index error. Now all 5,188 faces mesh, oriented, and 8 boundary edges are left: the shell does not quite close."),
    ("z-m2zed-cone", "corpus:POS/M2-ZED-F9T-25B-00.STEP --faces 753 --ring 1", "failure", "auto", "TKT-411", "M2-ZED-F9T-25B-00, cone face 753",
     "The single three-edge cone face 753 that threw on 2026-10-21 (TKT-411), meshed now with its two neighbours."),
    ("c-nora-b100", "corpus:SHO/NORA-B100.step", "failure", "auto", "TKT-410", "NORA-B100",
     "153 faces were lost to the stub and one three-edge cone face threw on 2026-10-21. Now all 3,149 faces mesh, closed and oriented, in 2.0 s warm."),
    ("c-maya-w260_revc", "corpus:SHO/MAYA-W260_REVC.step", "failure", "auto", "TKT-413", "MAYA-W260_REVC",
     "On 2026-10-21 the only kind of file whose meshed faces disagreed: 4 non-manifold and 4 misoriented edges (TKT-413), 13 planes not simple (TKT-412), 7 faces that threw (TKT-411). Now every face meshes and the mesh is oriented. The 2,712 boundary edges are the file's own: it is an open-shell surface model whose faces are separate sheets (v1-edges E1 counted 2,592 B-rep edges cited by one face), so 'open' is the file's state, not a defect."),
    ("z-maya-notsimple", "corpus:SHO/MAYA-W260_REVC.step --faces 1481,1483,1499,1504,1509,1510 --ring 1", "failure", "auto", "TKT-412", "MAYA-W260_REVC, planes reported not simple",
     "Six 12-edge planes whose boundary segments came within the parameter tolerance of each other in UV on 2026-10-21 (TKT-412). They mesh now."),
    ("z-maya-cylinders", "corpus:SHO/MAYA-W260_REVC.step --faces 1566,1577 --ring 1", "failure", "auto", "TKT-411", "MAYA-W260_REVC, eight-edge cylinder faces",
     "Two cylinder faces with one loop of eight edges that threw the same index error as the three-edge spheres and cones (TKT-411). They mesh now."),
    ("c-odin-w260", "corpus:SHO/ODIN-W260.STEP", "failure", "auto", "TKT-412", "ODIN-W260",
     "2,713 of 2,714 faces: plane 2613, five edges at the board's edge, is still refused as not simple (TKT-412); the other three planes refused on 2026-10-21 mesh now. The 5 boundary edges are around it. It hung in October."),
    ("z-odin-notsimple", "corpus:SHO/ODIN-W260.STEP --faces 2613,2615,2616,2625 --ring 1", "failure", "auto", "TKT-412", "ODIN-W260, the four not-simple planes",
     "The four tiny planes at the board's edge that were refused on 2026-10-21, with their neighbours: three mesh now, 2613 stays red."),
    ("p-io1-tc-214", "sample:steptools-ap214/io1-tc-214.stp", "public", "auto", "TKT-323", "STEP Tools io1-tc-214 (AP214)",
     "The public AP214 sample the epic's fourth criterion names. 48 surfaces of revolution and 10 planes, closed and outward."),
    ("p-nist_ftc_06_asme1_rd", "sample:\"nist/NIST-PMI-STEP-Files/AP203 geometry only/nist_ftc_06_asme1_rd.stp\"", "public", "auto", "TKT-323", "NIST FTC 06",
     "A NIST test part with planes, cylinders, tori, spheres and cones, meshed closed and oriented."),
    ("p-cubcylso", "sample:steptools-ap203/cubcylso.stp", "public", "auto", "TKT-323", "STEP Tools cube and cylinder",
     "A cube with a cylinder: planes on the quad grid, the cylinder by CDT."),
    ("p-nist_ctc_01_asme1_rd", "sample:\"nist/NIST-PMI-STEP-Files/AP203 geometry only/nist_ctc_01_asme1_rd.stp\"", "public", "auto", "TKT-410", "NIST CTC 01",
     "Every face meshes now (on the morning run a few cylinder faces lost an edge to the stub), but the assembled mesh has 42 non-manifold, 60 misoriented and 8 boundary edges: the one case left whose meshed faces disagree on shared edges, TKT-413's family; its slowest face is a one-edge cone."),
    ("p-moon_buggy_asm", "sample:steptools-ap203/moon_buggy_asm.stp --faces 14,21,23", "public", "auto", "TKT-428", "STEP Tools moon buggy, three full spheres",
     "Three whole spheres with no boundary edges. On the morning run each refined to about 19,000 triangles in about 6 s (TKT-428); now 8,874 triangles for the three, the slowest 2.4 s. The whole assembly's mesh is still too large to publish; the full file's 180 misoriented edges (TKT-429) and the axle cylinder off the quad grid (TKT-430) are not re-checked here."),
    ("p-nist_ftc_07", "sample:\"nist/NIST-PMI-STEP-Files/AP203 geometry only/nist_ftc_07_asme1_rd.stp\"", "public", "auto", "TKT-323", "NIST FTC 07",
     "A NIST test part with pockets, bosses and a slot."),
    ("p-nist_ftc_08", "sample:\"nist/NIST-PMI-STEP-Files/AP203 geometry only/nist_ftc_08_asme1_rc.stp\"", "public", "auto", "TKT-323", "NIST FTC 08",
     "A NIST test part with angled faces and counterbored holes."),
    ("p-nist_ftc_09", "sample:\"nist/NIST-PMI-STEP-Files/AP203 geometry only/nist_ftc_09_asme1_rd.stp\"", "public", "auto", "TKT-323", "NIST FTC 09",
     "A NIST test part with a revolved body and cross holes."),
    ("p-nist_ftc_10", "sample:\"nist/NIST-PMI-STEP-Files/AP203 geometry only/nist_ftc_10_asme1_rb.stp\"", "public", "auto", "TKT-323", "NIST FTC 10",
     "A NIST test part with fillets along a curved profile."),
    ("p-nist_ftc_11", "sample:\"nist/NIST-PMI-STEP-Files/AP203 geometry only/nist_ftc_11_asme1_rb.stp\"", "public", "auto", "TKT-323", "NIST FTC 11",
     "The smallest NIST file, 7.6 KB: a block with a few features."),
    ("p-nist_ctc_02", "sample:\"nist/NIST-PMI-STEP-Files/AP203 geometry only/nist_ctc_02_asme1_rc.stp\"", "public", "auto", "TKT-323", "NIST CTC 02",
     "A NIST test part with a freeform face, 1.2 MB."),
    ("p-nist_ctc_03", "sample:\"nist/NIST-PMI-STEP-Files/AP203 geometry only/nist_ctc_03_asme1_rc.stp\"", "public", "auto", "TKT-323", "NIST CTC 03",
     "A NIST test part with hole patterns on several faces."),
    ("p-nist_ctc_04", "sample:\"nist/NIST-PMI-STEP-Files/AP203 geometry only/nist_ctc_04_asme1_rd.stp\"", "public", "auto", "TKT-323", "NIST CTC 04",
     "A NIST test part with a sculpted top, 0.8 MB."),
    ("p-nist_ctc_05", "sample:\"nist/NIST-PMI-STEP-Files/AP203 geometry only/nist_ctc_05_asme1_rd.stp\"", "public", "auto", "TKT-323", "NIST CTC 05",
     "A NIST test part with a tilted boss and threads modelled as grooves."),
    ("p-screw", "sample:occt/screw.step", "public", "auto", "TKT-323", "OCCT screw",
     "Open CASCADE's screw sample: a helical thread as B-spline faces."),
    ("p-linkrods", "sample:occt/linkrods.step", "public", "auto", "TKT-323", "OCCT link rods",
     "Open CASCADE's link rods: an assembly of rods with filleted ends, 1.8 MB."),
    ("p-turbine", "sample:steptools-ap203/turbine.stp", "public", "auto", "TKT-323", "STEP Tools turbine",
     "A turbine wheel: blades as B-spline surfaces around a revolved hub, 2.3 MB."),
    ("p-bull", "sample:steptools-ap203/bull.stp", "public", "auto", "TKT-323", "STEP Tools bull",
     "A sculpted bull: freeform B-spline faces throughout."),
    ("p-as1_pe", "sample:steptools-ap203/as1_pe.stp", "public", "auto", "TKT-323", "STEP Tools as1 (AP203)",
     "The classic as1 bracket assembly: plates, bolts and nuts, in AP203."),
    ("p-as1-ec-214", "sample:steptools-ap214/as1-ec-214.stp", "public", "auto", "TKT-323", "STEP Tools as1 (AP214, external)",
     "The as1 assembly in AP214 with each part placed by an external reference."),
    ("p-as1-ug-214", "sample:steptools-ap214/as1-ug-214.stp", "public", "auto", "TKT-323", "STEP Tools as1 (AP214, UG)",
     "The as1 assembly as Unigraphics writes AP214."),
    ("p-block", "sample:steptools-ap203/block.stp", "public", "auto", "TKT-410", "STEP Tools block",
     "A plain box, and nothing meshes: its 12 line edges are wrapped in INTERSECTION_CURVE, a surface-curve kind that throws the stub on every edge."),
]

GROUPS = [
    ("witness", "Primitives the library proves itself on", "The library's own witness models, meshed at a chordal bound of 0.01 on a one-unit model. Each is a law's subject."),
    ("operation", "First-level operations", "One solid per operation a modeller makes in one step: a chamfer, a fillet, a slot, a hole, a boss, a cut, a revolve. Plato witnesses (sandbox/step-brep-mesh/07-validation/operation-witnesses.library.plato) meshed at a chordal bound of 0.01 on parts one to four units across; each is also a law subject. The verdict is read from the sidecar, not typed."),
    ("part", "Real parts that mesh completely", "u-blox antenna and module models from the corpus, every face meshed, closed and oriented, with two close-ups of the faces that were once the slowest."),
    ("corpus", "More of the corpus", "Twenty more u-blox files across the three families, chosen for size and variety rather than for a known verdict; each verdict is read from its sidecar."),
    ("public", "Public samples", "Files from STEP Tools and NIST, fetched by scripts/fetch-step-samples.ps1: the first files outside the u-blox corpus."),
    ("failure", "Files with a ticket, and their close-ups", "The corpus files that failed on 2026-10-21, each with the ticket it opened, re-meshed on every run so a fix shows here first. Red lines are the discretized edges of faces that did not mesh. On 2026-10-22 the compiler stub on B-spline edges (TKT-410) and the three-edge loops that threw (TKT-411) were fixed, and most of this group now meshes."),
]

VERDICTS = {
    "ok": ("Meshed, closed, oriented", "ok"),
    "caught": ("Defect in the file, caught by the checks", "ok"),
    "slow": ("Meshed, slow", "warn"),
    "wrong": ("Meshed wrong", "bad"),
    "holes": ("Faces missing: compiler stub", "bad"),
    "threw": ("Faces missing: library threw", "bad"),
    "not-simple": ("Faces missing: reported not simple", "warn"),
    "open": ("Meshed, oriented; open shell with boundary edges", "warn"),
    "bad-edges": ("Non-manifold or misoriented edges", "bad"),
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
    contract holds and the assembled mesh is oriented and closed. A file whose meshed faces agree on every shared edge
    but leave boundary edges is 'open' (a surface model, or a file with one face missing), not a mesher defect. A
    close-up (fewer faces drawn than the file has) is judged on its drawn faces alone."""
    unmeshed = j.get("unmeshed", [])
    if unmeshed:
        if any(u.get("edgesThrew") for u in unmeshed) or j.get("edgesThrew"):
            return "holes"
        if any(u.get("exception") not in ("", None, "undiscretized") for u in unmeshed):
            return "threw"
        if any(u.get("branch") == "not-simple" or "parameter tolerance" in (u.get("message") or "") for u in unmeshed):
            return "not-simple"
        return "threw"
    if str((j.get("contract") or {}).get("verdict", "")).startswith("contract broken"):
        return "wrong"
    if j["drawnFaces"] < j["faces"]:
        return "ok"  # a close-up: the check below is the whole file's, and every drawn face meshed
    c = j["check"]
    if c["NonManifoldEdges"] or c["MisorientedEdges"] or not c["Oriented"]:
        return "bad-edges"
    if c["BoundaryEdges"] or not c["Closed"]:
        return "open"
    return "ok"


def summary(cases: list) -> list:
    """One row per group and one for all: cases, cases whose verdict is a pass (tone ok), faces meshed of drawn,
    triangles, the meshing time of the drawn faces summed over the cores' rows (CPU time), and the whole-file wall time
    (cold for the witness groups, which run without --warmup; warm for files), read from the manifest so the table says
    what the cards say."""
    rows = []
    for key, title, _ in [*GROUPS, ("all", "All", "")]:
        picked = [c for c in cases if key == "all" or c["group"] == key]
        rows.append({
            "group": title, "cases": len(picked), "ok": sum(1 for c in picked if VERDICTS[c["verdict"]][1] == "ok"),
            "meshed": sum(c["meshed"] for c in picked), "drawn": sum(c["drawn"] for c in picked),
            "triangles": sum(c["triangles"] for c in picked), "ms": sum((c["drawnMs"] or c["ms"]) for c in picked),
            "wall": sum(c["ms"] for c in picked),
        })
    return rows


def manifest(folder: Path, previous, compare_time: bool):
    out = []
    for stem, source, group, verdict, ticket, title, blurb in CASES:
        if not (folder / f"{stem}.json").exists():
            print(f"no sidecar for {stem}: not run yet, left off the page", file=sys.stderr)
            continue
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
.stage{position:relative;background:var(--scene);min-height:380px;height:52vh;outline:none}
.stage:focus-visible{box-shadow:inset 0 0 0 2px var(--accent)}
.stage .views{position:absolute;right:10px;top:8px;display:flex;gap:4px}
.stage .views button{font:inherit;font-size:12px;padding:3px 9px;border:1px solid var(--line);border-radius:4px;background:var(--panel);color:var(--fg2);cursor:pointer}
.stage .views button:hover{color:var(--fg);border-color:var(--accent)}
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
@media (max-width:760px){.viewer{grid-template-columns:1fr}.side{border-left:0;border-top:1px solid var(--line)}.stage{height:44vh;min-height:280px}}
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
  <table class="summary" id="summary"><thead><tr><th>Group</th><th>Cases</th><th>Pass</th><th>Faces meshed</th><th>Triangles</th><th>Face CPU time</th><th>Whole-file time</th></tr></thead><tbody></tbody></table>
</header>

<div class="viewer" id="viewer">
  <div class="stage" id="stage" tabindex="0"><div class="spin" id="spin">Pick a case below</div>
    <div class="views"><button data-view="iso" title="Isometric (5)">Iso</button><button data-view="top" title="Top (7)">Top</button><button data-view="front" title="Front (1)">Front</button><button data-view="right" title="Right (3)">Right</button><button data-view="fit" title="Fit the model (F)">Fit</button></div>
    <div class="hint">drag: orbit · wheel: zoom at the pointer · right or middle drag, or shift-drag: pan · double-click: orbit about that point · F: fit · 1 3 5 7: front right iso top</div></div>
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
<script type="importmap">{"imports":{"three":"https://cdn.jsdelivr.net/npm/three@0.160.0/build/three.module.js","three/addons/":"https://cdn.jsdelivr.net/npm/three@0.160.0/examples/jsm/"}}</script>
<script type="module">
import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
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
    const secs = x => x >= 1000 ? (x / 1000).toFixed(1) + ' s' : x.toFixed(0) + ' ms';
    tr.innerHTML = `<td>${r.group}</td><td class="num">${r.cases}</td><td class="num ${tone}">${r.ok} / ${r.cases}</td><td class="num">${fmt(r.meshed)} / ${fmt(r.drawn)}</td><td class="num">${fmt(r.triangles)}</td><td class="num">${secs(r.ms)}</td><td class="num">${secs(r.wall)}</td>`;
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
  document.getElementById('foot').innerHTML = `${cases.length} cases, ${(cases.reduce((a, c) => a + c.glbBytes, 0) / 1048576).toFixed(1)} MB of GLB, written by <code>step-corpus-harness gallery</code>: __LABEL__. Corpus files use the host's chordal bound; witnesses and operations use 0.01 on a model one to four units across. Not shown: NASA's RASSOR (78,659 edges, over the case's 600 s) and the moon buggy's full assembly (its GLB is over the artifact host's 16 MB).`;

  // Shared loader and cache
  const loader = new GLTFLoader();
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
    // r155 and later drop the legacy pi factor on light intensities, so these are the r128 values times pi.
    scene.add(new THREE.HemisphereLight(0xffffff, 0x8a8f99, 2.8));
    const d = new THREE.DirectionalLight(0xffffff, 2.2); d.position.set(-1, -2, 3); scene.add(d);
    const d2 = new THREE.DirectionalLight(0xffffff, 0.9); d2.position.set(2, 1, -1); scene.add(d2);
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
  const controls = new OrbitControls(camera, renderer.domElement);
  controls.enableDamping = true; controls.dampingFactor = 0.12;
  controls.zoomToCursor = true;
  controls.screenSpacePanning = true;
  controls.mouseButtons = { LEFT: THREE.MOUSE.ROTATE, MIDDLE: THREE.MOUSE.PAN, RIGHT: THREE.MOUSE.PAN };
  controls.touches = { ONE: THREE.TOUCH.ROTATE, TWO: THREE.TOUCH.DOLLY_PAN };
  controls.minDistance = 1e-4; controls.maxDistance = 1e6;
  let current = null, wire = null;
  // The camera at the given direction from the orbit centre, at its present distance (or fitted).
  const VIEWS = { iso: [-0.8, -1.0, 0.75], top: [0, -0.03, 1], front: [0, -1, 0.0001], right: [1, 0, 0.0001] };
  function view(name) {
    if (!current) return;
    if (name === 'fit') { frame(camera, controls, current, camera.aspect); return; }
    const dist = camera.position.distanceTo(controls.target);
    camera.position.copy(controls.target).add(new THREE.Vector3(...VIEWS[name]).normalize().multiplyScalar(dist));
    camera.lookAt(controls.target); controls.update();
  }
  document.querySelectorAll('.views button').forEach(b => b.addEventListener('click', () => { view(b.dataset.view); stage.focus(); }));
  const KEYS = { f: 'fit', F: 'fit', '1': 'front', '3': 'right', '5': 'iso', '7': 'top' };
  stage.addEventListener('keydown', e => { if (KEYS[e.key]) { e.preventDefault(); view(KEYS[e.key]); } });
  stage.addEventListener('pointerdown', () => stage.focus());
  // Double-click: the surface point under the pointer becomes the orbit centre; the camera shifts with it so the
  // picture does not jump, and the pointer's point stays put while orbiting and zooming.
  const raycaster = new THREE.Raycaster();
  renderer.domElement.addEventListener('dblclick', e => {
    if (!current) return;
    const r = renderer.domElement.getBoundingClientRect();
    raycaster.setFromCamera(new THREE.Vector2((e.clientX - r.left) / r.width * 2 - 1, -((e.clientY - r.top) / r.height) * 2 + 1), camera);
    const meshes = []; current.traverse(o => { if (o.isMesh && o.visible) meshes.push(o); });
    const hit = raycaster.intersectObjects(meshes, false)[0];
    if (!hit) return;
    const shift = hit.point.clone().sub(controls.target);
    controls.target.copy(hit.point); camera.position.add(shift); controls.update();
  });
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
    location.hash = id;
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
        if not glb.exists():
            continue
        (folder / f"{stem}.glb.txt").write_text(base64.b64encode(glb.read_bytes()).decode("ascii"), encoding="ascii")
    print(f"wrote {out} with {len(cases)} of {len(CASES)} cases")


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
