# Ara3D.Ids

Evaluates buildingSMART IDS 1.0 (Information Delivery Specification) files over
BIM Open Schema data and returns a verdict table.

- `IdsFile.Load` reads an `.ids` file with Xbim.InformationSpecifications, which
  carries the IDS value rules (enumerations, patterns, bounds, tolerances).
- `IdsModel.FromBimData` or `IdsModel.FromTables` (the `EntityText` and
  `ParameterText` tables, and optionally `RelationText`) is what the check runs
  over. The relations (`IdsRelations`) are built on first use, as sorted arrays.
- `IdsEvaluator.Evaluate` gives an `IdsReport`: one `IdsVerdict` per entity per
  specification, plus the warnings raised.
- `IdsTables.ToTable` renders the report with the `IssueColumns` names
  (GlobalId, EntityIndex, CheckId, CheckTitle, Citation, Verdict, Reason), and
  `IdsTables.Specifications` lists what an IDS asks before any model is read.

Facets evaluated: entity (class, exact match as IDS 1.0 defines it, so an IDS
names IFCWALLSTANDARDCASE beside IFCWALL when it means both; predefined type,
with ObjectType standing in for USERDEFINED), property (set, name, value, and
`dataType` at the level BOS records: number, logical, or text), attribute,
material, and part-of. Classification and document facets give
`InfoNotAvailable` with a warning, and so do material and part-of when the
model was built without relations.

Material and part-of read the BOS relations (`IdsRelationFacets`):

- Material: the names reached from the element through `HasMaterial` and
  `HasLayer` (a material, a layer, a layer set), plus each one's `Category`
  attribute and a layer's `LayerSet` (the converter writes the set's name
  there). The facet holds when any name satisfies its value; an element with no
  material is absent. A model with no material relation at all gives
  `InfoNotAvailable`.
- Part-of: the IDS relation becomes BOS relation types as the IFC converter
  writes them: IFCRELAGGREGATES and IFCRELASSIGNSTOGROUP are `MemberOf` (told
  apart by whether the container is a group class), IFCRELCONTAINEDINSPATIALSTRUCTURE
  is `ContainedIn`, IFCRELNESTS is `ChildOf`, and IFCRELVOIDSELEMENT
  IFCRELFILLSELEMENT is `Fills` then `Voids`. The walk is recursive: an element
  contained in a storey is part of the storey's building and site, through the
  aggregation of the spatial structure. The container's entity facet matches
  the class exactly. No relation stated means any of them. A relation whose BOS
  type the model has no edge of (the Duplex has no `ChildOf`), or one IDS 1.0
  does not name, gives `InfoNotAvailable` with a warning; so does a miss under
  "any relation" when one kind is missing.
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
