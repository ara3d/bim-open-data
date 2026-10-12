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
IDS values are in SI (metres, square metres, cubic metres), so `IdsUnits`
converts a model's number before comparing it, when its unit can be told: a unit
stated in the descriptor's Units column (`mm`, `ft`, `m2`), else an attribute
listed in `IdsUnits.LengthAttributes` (OverallWidth, Elevation, LayerThickness
and others; add a name there to extend it), else the property facet's
`dataType` (IFCLENGTHMEASURE scales by the model's `Ifc:LengthUnitToMetre`,
IFCAREAMEASURE by its square, IFCVOLUMEMEASURE by its cube). A number whose unit
cannot be told is compared as stored, with a warning naming it, unless the model
is in metres.

Verdicts: `Pass`, `Fail`, `InfoNotAvailable`. A required specification that
applies to no entity, and a prohibited one that applies to any, produce `Fail`.
