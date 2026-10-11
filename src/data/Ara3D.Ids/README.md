# Ara3D.Ids

Evaluates buildingSMART IDS 1.0 (Information Delivery Specification) files over
BIM Open Schema data and returns a verdict table.

- `IdsFile.Load` reads an `.ids` file with Xbim.InformationSpecifications, which
  carries the IDS value rules (enumerations, patterns, bounds, tolerances).
- `IdsModel.FromBimData` or `IdsModel.FromTables` (the `EntityText` and
  `ParameterText` tables) is what the check runs over.
- `IdsEvaluator.Evaluate` gives an `IdsReport`: one `IdsVerdict` per entity per
  specification, plus the warnings raised.
- `IdsTables.ToTable` renders the report with the `IssueColumns` names
  (GlobalId, EntityIndex, CheckId, CheckTitle, Citation, Verdict, Reason), and
  `IdsTables.Specifications` lists what an IDS asks before any model is read.

Facets evaluated: entity (class, exact match; predefined type, with ObjectType
standing in for USERDEFINED), property (set, name, value, and `dataType` at the
level BOS records: number, logical, or text), and attribute. Classification,
material, part-of and document facets give `InfoNotAvailable` with a warning.
Numbers are compared in the model's own units; IDS values are in SI, so a model
not in metres needs its lengths converted first.

Verdicts: `Pass`, `Fail`, `InfoNotAvailable`. A required specification that
applies to no entity, and a prohibited one that applies to any, produce `Fail`.
