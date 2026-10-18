namespace Ara3D.StepTypes.Tests;

/// <summary>
/// The generated descriptors against the Part 21 records of the toolkit's
/// sandbox/step-brep-mesh/research/STEP-BREP-MODEL.md section 3: attribute names in file order, '*' before a slot the
/// file writes as '*'. Ara3D.IfcTypeGen's StepSchemaLayoutTests check the same records against the schema; these
/// check the generated code.
/// </summary>
public static class StepDescriptorTests
{
    public static string Record(this StepEntityType type)
        => string.Join(", ", type.Attributes.Select(a => (a.IsDerived ? "*" : "") + a.Name));

    public static IEnumerable<TestCaseData> Records()
    {
        yield return new TestCaseData(AdvancedFace.Instance, "name, bounds, face_geometry, same_sense");
        yield return new TestCaseData(BSplineSurfaceWithKnots.Instance, "name, u_degree, v_degree, control_points_list, surface_form, u_closed, v_closed, self_intersect, u_multiplicities, v_multiplicities, u_knots, v_knots, knot_spec");
        yield return new TestCaseData(CartesianPoint.Instance, "name, coordinates");
        yield return new TestCaseData(Axis2Placement3d.Instance, "name, location, axis, ref_direction");
        yield return new TestCaseData(EdgeCurve.Instance, "name, edge_start, edge_end, edge_geometry, same_sense");
        yield return new TestCaseData(OrientedEdge.Instance, "name, *edge_start, *edge_end, edge_element, orientation");
        yield return new TestCaseData(VertexPoint.Instance, "name, vertex_geometry");
        yield return new TestCaseData(CylindricalSurface.Instance, "name, position, radius");
        yield return new TestCaseData(ManifoldSolidBrep.Instance, "name, outer");
        yield return new TestCaseData(ShapeRepresentation.Instance, "name, items, context_of_items");
    }

    [TestCaseSource(nameof(Records))]
    public static void RecordMatchesTheCatalogue(StepEntityType type, string expected)
    {
        Assert.That(type.Record(), Is.EqualTo(expected));
        Assert.That(type.Attributes.Select(a => a.Position), Is.EqualTo(Enumerable.Range(0, type.Attributes.Count)));
    }

    /// <summary>A representation item's record starts with REPRESENTATION_ITEM.name unless its first supertype is
    /// another entity: AP242 has ten such, none of them geometry or topology.</summary>
    [Test]
    public static void RepresentationItemsStartWithTheirName()
    {
        var items = StepSchema.Entities.Where(t => t is IRepresentationItem).ToList();
        Assert.That(items, Has.Count.GreaterThan(500));
        Assert.That(items.Select(t => t.Attributes.Count(a => a.Entity == "REPRESENTATION_ITEM" && a.Name == "name")), Has.All.EqualTo(1));
        var otherFirst = items.Where(t => t.Attributes[0].Entity != "REPRESENTATION_ITEM").Select(t => t.EntityName);
        Assert.That(otherFirst, Is.EquivalentTo(new[]
        {
            "ABSTRACT_VARIABLE", "ANGLE_DIRECTION_REFERENCE", "EXTERNALLY_DEFINED_HATCH_STYLE", "EXTERNALLY_DEFINED_TILE_STYLE",
            "MIN_AND_MAJOR_PLY_ORIENTATION_BASIS", "RANGE_CHARACTERISTIC", "REP_ITEM_GROUP", "ROW_VARIABLE", "SCALAR_VARIABLE",
            "USER_DEFINED_CURVE_FONT",
        }));
        Assert.That(items.Where(t => t is IGeometricRepresentationItem or ITopologicalRepresentationItem && t.Attributes[0].Entity != "REPRESENTATION_ITEM")
            .Select(t => t.EntityName), Is.SubsetOf(new[] { "ANGLE_DIRECTION_REFERENCE", "EXTERNALLY_DEFINED_HATCH_STYLE", "EXTERNALLY_DEFINED_TILE_STYLE", "MIN_AND_MAJOR_PLY_ORIENTATION_BASIS" }));
    }

    [Test]
    public static void FieldsCarryBothAddresses()
    {
        var face = AdvancedFace.Instance;
        Assert.That((face.Name.Entity, face.Name.Position, face.Name.OwnPosition), Is.EqualTo(("REPRESENTATION_ITEM", 0, 0)));
        Assert.That((face.Bounds.Entity, face.Bounds.Position, face.Bounds.OwnPosition), Is.EqualTo(("FACE", 1, 0)));
        Assert.That((face.FaceGeometry.Entity, face.FaceGeometry.Position, face.FaceGeometry.OwnPosition), Is.EqualTo(("FACE_SURFACE", 2, 0)));
        Assert.That((face.SameSense.Entity, face.SameSense.Position, face.SameSense.OwnPosition), Is.EqualTo(("FACE_SURFACE", 3, 1)));

        var knots = BSplineSurfaceWithKnots.Instance.UKnots;
        Assert.That((knots.Entity, knots.Position, knots.OwnPosition), Is.EqualTo(("B_SPLINE_SURFACE_WITH_KNOTS", 10, 2)));

        var prefix = SiUnit.Instance.Prefix;
        Assert.That((prefix.Entity, prefix.Position, prefix.OwnPosition), Is.EqualTo(("SI_UNIT", 1, 0)));
        Assert.That(SiUnit.Instance.Dimensions.IsDerived, Is.True);
    }

    [Test]
    public static void SeveralSupertypesAreInterfaces()
    {
        Assert.That(VertexPoint.Instance, Is.InstanceOf<IVertex>().And.InstanceOf<IGeometricRepresentationItem>());
        Assert.That(RationalBSplineSurface.Instance, Is.InstanceOf<IBSplineSurface>().And.InstanceOf<ISurface>());
        Assert.That(AdvancedFace.Instance, Is.Not.InstanceOf<IEdge>());
    }

    [Test]
    public static void ASecondSupertypesSlotsMoveInASubtype()
    {
        var name = FunctionallyDefinedTransformation.Instance.Name;
        var cto = CartesianTransformationOperator3d.Instance;
        Assert.That(name.Position, Is.EqualTo(0));
        Assert.That(cto.PositionOf(name), Is.EqualTo(1));
        Assert.That(cto.PositionOf(cto.FunctionallyDefinedTransformationName), Is.EqualTo(1));
        Assert.That(cto.PositionOf(RepresentationItem.Instance.Name), Is.EqualTo(0));
        Assert.That(CartesianPoint.Instance.PositionOf(AdvancedFace.Instance.FaceGeometry), Is.EqualTo(-1));
    }

    [Test]
    public static void SchemaListsEveryEntityOnce()
    {
        Assert.That(StepSchema.Entities, Has.Count.EqualTo(StepSchema.EntityCount).And.Count.EqualTo(2140));
        Assert.That(StepSchema.Entities.Select(t => t.EntityName), Is.Unique);
    }
}
