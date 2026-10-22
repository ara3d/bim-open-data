"""Writes the STEP mesher gallery page from the GLB and JSON sidecars the harness's gallery mode produced.

    python gallery-page.py <folder with the .glb and .json files> [index.html]

The cases are listed in CASES below: the GLB's stem, the group, the verdict, the ticket, and a sentence on what the
case shows. The counts, times and checks come from each case's JSON sidecar, so the page never carries a number the
mesher did not produce. The page loads three.js from jsDelivr and each GLB from a base64 .glb.txt beside itself, which this script also writes
(the artifact host serves text files, not model/gltf-binary).
"""
import base64
import json
import sys
from pathlib import Path

# (stem, group, verdict, ticket, title, what it shows)
CASES = [
    ("w-UnitBox", "witness", "ok", "TKT-314", "Unit box",
     "Chunk A, the cube through every layer. Six faces, one quad each: the grid path with no refinement."),
    ("w-Cylinder", "witness", "ok", "TKT-324", "Cylinder",
     "The seam cut and the two circles. The side is a quad strip (TKT-405); the caps are refined triangles."),
    ("w-Sphere", "witness", "ok", "TKT-325", "Sphere",
     "Closed in two directions with two poles, where the chart collapses a line to a point."),
    ("w-Torus", "witness", "ok", "TKT-325", "Torus",
     "Closed both ways with no pole: the periodic chart needs two seam cuts and the grid meshes it in quads."),
    ("w-Cone", "witness", "ok", "TKT-325", "Cone",
     "The apex is a degenerate line of the chart; the triangles meet it without a sliver."),
    ("w-BlockWithThroughHole", "witness", "ok", "TKT-328", "Block with a through hole",
     "Genus one: two faces carry an inner loop; the hole's cylinder faces close the shell."),
    ("w-HollowBox", "witness", "ok", "TKT-328", "Hollow box",
     "A void: an inner shell whose faces point inward, so the solid's volume is the shell difference."),
    ("w-BoxWithFlippedFace", "witness", "caught", "TKT-328", "Box with a flipped face",
     "One face reversed in the file. The mesh is closed but the check reports four misoriented edges: the defect is found, not hidden."),
    ("w-TwoBoxesSharingCorner", "witness", "caught", "TKT-328", "Two boxes sharing a corner",
     "A pinched vertex. The edge checks pass (closed, oriented); only the vertex-component check sees the pinch."),
    ("w-BumpSolid", "witness", "wrong", "new", "Bump solid, bump 0.3",
     "A cube whose top is a B-spline patch raised 0.3 in the middle. The four-edge face takes the quad-grid path and comes out as one flat quad: the mesh hash equals the unit box's. The chordal bound is tested along the straight edges, where the deviation is zero, so the bump vanishes. Not ticketed before this run; TKT-401 is the nearby defect."),

    ("c-emmy-w1", "part", "ok", "TKT-323", "EMMY-W1",
     "The smallest fully meshed part. Closed and oriented; 10.6 s on Oct 19, now a quarter of a second."),
    ("c-nina-b400", "part", "ok", "TKT-323", "NINA-B400",
     "A shielded module with many pins. Every face meshed, most by the quad grid."),
    ("c-odin-w160", "part", "ok", "TKT-409", "ODIN-W160",
     "Hung for 358 s on Oct 19 on a 212-edge plane; now under a second, closed and oriented. Its four ellipse-bounded oblique cylinder cuts fall back to CDT (TKT-409)."),
    ("z-odin-plane212", "part", "slow", "TKT-349", "ODIN-W260, the two 212-edge planes",
     "The faces of TKT-349 (86 s to over 8 minutes before the complexity sweep). Face 34 still takes 1.2 s for 2,328 triangles: the slowest plane in the corpus."),
    ("z-odin-slowcyl", "part", "slow", "TKT-349", "ODIN-W260, the slowest cylinder faces",
     "Twelve-edge cylinder faces that the grid refuses and CDT refines to about 950 triangles each, 1.2 to 1.3 s apiece: where face time goes now."),

    ("c-lena-r8", "failure", "holes", "TKT-410", "LENA-R8",
     "488 of its 562 B-spline edges throw the compiler's stub for SagittaByHullBoundGiven, so 355 faces have no boundary to mesh. The red outlines are the edges of unmeshed faces that did discretize; the stubbed edges leave nothing to draw."),
    ("z-lena-r8-spheres", "failure", "threw", "TKT-411", "LENA-R8, three-edge sphere faces",
     "Eight sphere faces bounded by one loop of three edges throw an index-out-of-range in layer 5. Shown with their neighbours; the red triangles are the unmeshed faces."),
    ("c-lara", "failure", "holes", "TKT-410", "LARA",
     "566 B-spline edges throw the stub; 347 faces unmeshed. The same cause as LENA-R8 on a different antenna."),
    ("c-rcb-f9tap214", "failure", "holes", "TKT-410", "RCB-F9T (AP214)",
     "The largest and slowest file: 8,475 faces in 8.6 s warm. 824 faces lost to the stub; everything else meshes."),
    ("c-m2-zed-f9t-25b-00", "failure", "holes", "TKT-410", "M2-ZED-F9T-25B-00",
     "1,004 edges throw the stub and 648 faces go unmeshed; one cone face throws TKT-411's index error."),
    ("z-m2zed-cone", "failure", "threw", "TKT-411", "M2-ZED-F9T-25B-00, cone face 753",
     "A single three-edge cone face that throws, with its two neighbours meshed around it."),
    ("c-nora-b100", "failure", "holes", "TKT-410", "NORA-B100",
     "153 faces lost to the stub and one three-edge cone face to TKT-411."),
    ("c-maya-w260_revc", "failure", "bad-edges", "TKT-413", "MAYA-W260_REVC",
     "The only kind of file whose meshed faces disagree: 4 non-manifold and 4 misoriented edges in the assembled mesh (TKT-413), plus 13 planes reported not simple (TKT-412) and 7 faces that throw (TKT-411)."),
    ("z-maya-notsimple", "failure", "not-simple", "TKT-412", "MAYA-W260_REVC, planes reported not simple",
     "Six 12-edge planes whose boundary segments come within the parameter tolerance of each other in UV. Whether the file self-intersects or the tolerance is too loose is the ticket's question. Nothing meshed: only the red loops."),
    ("z-maya-cylinders", "failure", "threw", "TKT-411", "MAYA-W260_REVC, eight-edge cylinder faces",
     "Two cylinder faces with one loop of eight edges that throw the same index error as the three-edge spheres and cones."),
    ("c-odin-w260", "failure", "not-simple", "TKT-412", "ODIN-W260",
     "2,710 of 2,714 faces; the four missing planes are reported not simple. Hung in October, 1.1 s now."),
    ("z-odin-notsimple", "failure", "not-simple", "TKT-412", "ODIN-W260, the four not-simple planes",
     "Tiny planes with 2 to 6 edges at the board's edge, with their neighbours. Red loops mark the four."),
]

GROUPS = [
    ("witness", "Primitives the library proves itself on", "The library's own witness models, meshed at a chordal bound of 0.01 on a one-unit model. Each is a law's subject."),
    ("part", "Real parts that mesh completely", "u-blox antenna and module models from the corpus, every face meshed, closed and oriented."),
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


def manifest(folder: Path):
    out = []
    for stem, group, verdict, ticket, title, blurb in CASES:
        j = json.loads((folder / f"{stem}.json").read_text(encoding="utf-8"))
        c = j["check"]
        out.append({
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
footer{font-size:11.5px;color:var(--fg3);border-top:1px solid var(--line);padding-top:10px;line-height:1.6}
footer code{font-family:var(--mono)}
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
</header>

<div class="viewer" id="viewer">
  <div class="stage" id="stage"><div class="spin" id="spin">Pick a case below</div><div class="hint">drag: orbit · wheel: zoom · right-drag: pan</div></div>
  <div class="side">
    <div id="vbadge"></div>
    <h3 id="vtitle">Nothing loaded</h3>
    <p id="vblurb"></p>
    <div class="kv" id="vkv"></div>
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
        <div class="stats"><span>faces <b>${fmt(c.meshed)}${missing ? ' / ' + fmt(c.drawn) : ''}</b></span><span>triangles <b>${fmt(c.triangles)}</b></span><span>time <b>${c.ms >= 1000 ? (c.ms/1000).toFixed(1)+' s' : c.ms.toFixed(0)+' ms'}</b></span><span>${c.closed ? 'closed' : 'open, ' + fmt(c.boundary) + ' boundary edges'}${c.nonmanifold ? ', ' + c.nonmanifold + ' non-manifold' : ''}${c.misoriented ? ', ' + c.misoriented + ' misoriented' : ''}</span></div></div>`;
      el.addEventListener('click', () => load(c.id));
      el.addEventListener('keydown', e => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); load(c.id); } });
      grid.appendChild(el);
    }
    sections.appendChild(s);
  }
  document.getElementById('foot').innerHTML = `${cases.length} cases, ${(cases.reduce((a, c) => a + c.glbBytes, 0) / 1048576).toFixed(1)} MB of GLB, written by <code>step-corpus-harness gallery</code> from the library compiled at bim-open-toolkit 8984df4b, the corpus run of 2026-10-21. Corpus files use the host's chordal bound; witnesses use 0.01 on a one-unit model. Not shown: ELLA-W1, where every edge throws the stub and no face meshes, and NINA-B222-W1x2, which does not finish edge discretization (TKT-414).`;

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
    document.getElementById('vkv').innerHTML = `<span>faces drawn</span><b>${fmt(c.drawn)} of ${fmt(c.faces)}</b><span>meshed</span><b>${fmt(c.meshed)}</b><span>triangles</span><b>${fmt(c.triangles)}</b><span>edges that threw</span><b>${fmt(c.edgesThrew)}</b><span>branches</span><b>${br}</b><span>time, warm</span><b>${c.ms.toFixed(0)} ms</b><span>check</span><b>${c.closed ? 'closed' : 'boundary ' + fmt(c.boundary)}, ${c.oriented ? 'oriented' : 'misoriented ' + c.misoriented}${c.nonmanifold ? ', non-manifold ' + c.nonmanifold : ''}</b><span>extent</span><b>${c.size.map(x => x.toFixed(1)).join(' × ')}</b>`;
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


def main():
    folder = Path(sys.argv[1])
    out = Path(sys.argv[2]) if len(sys.argv) > 2 else folder / "index.html"
    page = (PAGE.replace("__MANIFEST__", json.dumps(manifest(folder)))
            .replace("__GROUPS__", json.dumps(GROUPS))
            .replace("__VERDICTS__", json.dumps(VERDICTS)))
    out.write_text(page, encoding="utf-8")
    for stem, *_ in CASES:
        glb = folder / f"{stem}.glb"
        (folder / f"{stem}.glb.txt").write_text(base64.b64encode(glb.read_bytes()).decode("ascii"), encoding="ascii")
    print(f"wrote {out} with {len(CASES)} cases")


if __name__ == "__main__":
    main()
