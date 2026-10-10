"""Opens a USD stage with usd-core (pip install usd-core) and prints one JSON object:
the composition errors, every UsdValidation finding (the checks usdchecker runs),
stage metadata, prim counts, and the attributes of the prims named on the command line.

usage: python check_usda.py STAGE [PRIM_PATH ...]

The usd-core wheel (checked with 0.26.8 on Windows) ships no usdchecker script;
UsdValidation's registry holds the validators usdchecker runs, so this runs all of them.
"""
import json
import sys

from pxr import Kind, Usd, UsdGeom, UsdValidation


def attribute_values(prim):
    values = {}
    for attr in prim.GetAuthoredAttributes():
        name = attr.GetName()
        if not name.startswith("bim:"):
            continue
        value = attr.Get()
        if hasattr(value, "__len__") and not isinstance(value, str):
            value = list(value)
        values[name] = {
            "type": str(attr.GetTypeName()),
            "value": value,
            "displayName": attr.GetMetadata("displayName"),
            "displayGroup": attr.GetMetadata("displayGroup"),
        }
    return values


def main():
    path, prim_paths = sys.argv[1], sys.argv[2:]
    stage = Usd.Stage.Open(path)
    validators = UsdValidation.ValidationRegistry().GetOrLoadAllValidators()
    findings = UsdValidation.ValidationContext(validators).Validate(stage)

    counts = {"prims": 0, "meshes": 0, "instances": 0, "elements": 0, "materials": 0, "invisible": 0}
    for prim in Usd.PrimRange(stage.GetPseudoRoot(), Usd.PrimAllPrimsPredicate):
        if prim.IsPseudoRoot():
            continue
        counts["prims"] += 1
        if prim.IsA(UsdGeom.Mesh):
            counts["meshes"] += 1
        if prim.IsInstance():
            counts["instances"] += 1
        if Usd.ModelAPI(prim).GetKind() == Kind.Tokens.component:
            counts["elements"] += 1
        if prim.GetTypeName() == "Material":
            counts["materials"] += 1
        if prim.IsA(UsdGeom.Imageable) and UsdGeom.Imageable(prim).GetVisibilityAttr().Get() == "invisible":
            counts["invisible"] += 1

    prims = {}
    for p in prim_paths:
        prim = stage.GetPrimAtPath(p)
        prims[p] = attribute_values(prim) if prim else None

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
