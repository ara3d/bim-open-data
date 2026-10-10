"""Opens a USD stage with usd-core (pip install usd-core) and prints one JSON object:
the composition errors, every UsdValidation finding (the checks usdchecker runs),
stage metadata, prim counts, and the type and bim: properties of the prims named on
the command line.

usage: python check_usda.py STAGE [PRIM_PATH ...]

The usd-core wheel (checked with 0.26.8 on Windows) ships no usdchecker script;
UsdValidation's registry holds the validators usdchecker runs, so this runs all of them.
"""
import json
import sys

from pxr import Kind, Usd, UsdGeom, UsdValidation


def properties(prim):
    """bim: attributes as {type, value, displayName, displayGroup} and bim: relationships
    as {type: "rel", targets, displayName, displayGroup}."""
    values = {}
    for prop in prim.GetAuthoredProperties():
        name = prop.GetName()
        if not name.startswith("bim:"):
            continue
        entry = {
            "displayName": prop.GetMetadata("displayName"),
            "displayGroup": prop.GetMetadata("displayGroup"),
        }
        if isinstance(prop, Usd.Relationship):
            entry["type"] = "rel"
            entry["targets"] = [str(t) for t in prop.GetTargets()]
        else:
            value = prop.Get()
            if hasattr(value, "__len__") and not isinstance(value, str):
                value = list(value)
            entry["type"] = str(prop.GetTypeName())
            entry["value"] = value
        values[name] = entry
    return values


def main():
    path, prim_paths = sys.argv[1], sys.argv[2:]
    stage = Usd.Stage.Open(path)
    validators = UsdValidation.ValidationRegistry().GetOrLoadAllValidators()
    findings = UsdValidation.ValidationContext(validators).Validate(stage)

    counts = {"prims": 0, "meshes": 0, "instances": 0, "entities": 0, "elements": 0, "scopes": 0,
              "materials": 0, "invisible": 0, "relationshipTargets": 0}
    for prim in Usd.PrimRange(stage.GetPseudoRoot(), Usd.PrimAllPrimsPredicate):
        if prim.IsPseudoRoot():
            continue
        counts["prims"] += 1
        if prim.IsA(UsdGeom.Mesh):
            counts["meshes"] += 1
        if prim.IsInstance():
            counts["instances"] += 1
        if prim.HasAttribute("bim:entityIndex"):
            counts["entities"] += 1
            if prim.GetTypeName() == "Scope":
                counts["scopes"] += 1
            for rel in prim.GetRelationships():
                if rel.GetName().startswith("bim:") and not rel.GetName().startswith("bim:param:"):
                    counts["relationshipTargets"] += len(rel.GetTargets())
        if Usd.ModelAPI(prim).GetKind() == Kind.Tokens.component:
            counts["elements"] += 1
        if prim.GetTypeName() == "Material":
            counts["materials"] += 1
        if prim.IsA(UsdGeom.Imageable) and UsdGeom.Imageable(prim).GetVisibilityAttr().Get() == "invisible":
            counts["invisible"] += 1

    prims = {}
    for p in prim_paths:
        prim = stage.GetPrimAtPath(p)
        prims[p] = {"typeName": str(prim.GetTypeName()), "properties": properties(prim)} if prim else None

    result = {
        "usdVersion": ".".join(str(v) for v in Usd.GetVersion()),
        "compositionErrors": [str(e) for e in stage.GetCompositionErrors()],
        "findings": [
            {"type": str(f.GetType()), "name": str(f.GetName()), "message": f.GetMessage()}
            for f in findings
        ],
        "defaultPrim": stage.GetDefaultPrim().GetName() if stage.GetDefaultPrim() else None,
        "upAxis": UsdGeom.GetStageUpAxis(stage),
        "metersPerUnit": UsdGeom.GetStageMetersPerUnit(stage),
        "prototypes": len(stage.GetPrototypes()),
        "counts": counts,
        "prims": prims,
    }
    json.dump(result, sys.stdout, default=str)


if __name__ == "__main__":
    main()
