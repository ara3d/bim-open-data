"""Runs IfcOpenShell's ifctester over an IFC file and one or more IDS files and prints, per
specification, the pass and fail counts in the shape Ara3D.Ids's tests compare against:

    <name> pass=<n> fail=<n>

Usage: python -I run-ifctester.py model.ifc spec.ids [more.ids ...]
Exit code 3 means ifctester (or ifcopenshell) is not installed."""

import sys

try:
    import ifcopenshell
    import ifctester
except ImportError as error:  # pragma: no cover
    print(f"ifctester not installed: {error}", file=sys.stderr)
    sys.exit(3)


def main(argv):
    if len(argv) < 3:
        print(__doc__, file=sys.stderr)
        return 2
    model = ifcopenshell.open(argv[1])
    for path in argv[2:]:
        ids = ifctester.ids.open(path)
        ids.validate(model)
        for spec in ids.specifications:
            passed = len(spec.passed_entities)
            failed = len(spec.failed_entities)
            print(f"{spec.name} pass={passed} fail={failed}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
