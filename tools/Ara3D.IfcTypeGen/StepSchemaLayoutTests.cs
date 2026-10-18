using System.Collections.Concurrent;

namespace Ara3D.IfcTypeGen;

/// <summary>
/// Checks the Part 21 attribute order computed from the STEP long forms against the records of
/// sandbox/step-brep-mesh/research/STEP-BREP-MODEL.md section 3 (in the toolkit repository). A record is written
/// as there: attribute names in file order, '*' before a slot the file always writes as '*'.
/// The tests are skipped when the schemas have not been fetched (see <see cref="StepSchemaFiles"/>).
/// </summary>
public static class StepSchemaLayoutTests
{
    private static readonly ConcurrentDictionary<string, ExpressSchema> Schemas = new();
    private static readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, ExpressEntity>> Lookups = new();

    public static ExpressSchema LoadSchemaOrIgnore(string edition)
    {
        if (StepSchemaFiles.Find(edition) == null)
            Assert.Ignore($"No {edition} schema. {StepSchemaFiles.FetchHint}");
        return Schemas.GetOrAdd(edition, e => StepSchemaFiles.Load(e).GetSchema());
    }

    public static IReadOnlyDictionary<string, ExpressEntity> LoadOrIgnore(string edition)
        => Lookups.GetOrAdd(edition, e => LoadSchemaOrIgnore(e).ToEntityLookup());

    public static string Record(this IEnumerable<AttributeSlot> slots)
        => string.Join(", ", slots.Select(s => (s.IsDerived ? "*" : "") + s.Attribute.Name));

    [TestCase("cartesian_point", "name, coordinates")]
    [TestCase("direction", "name, direction_ratios")]
    [TestCase("vector", "name, orientation, magnitude")]
    [TestCase("axis2_placement_3d", "name, location, axis, ref_direction")]
    [TestCase("cartesian_transformation_operator_3d", "name, name, description, axis1, axis2, local_origin, scale, axis3")]
    [TestCase("circle", "name, position, radius")]
    [TestCase("b_spline_curve_with_knots", "name, degree, control_points_list, curve_form, closed_curve, self_intersect, knot_multiplicities, knots, knot_spec")]
    [TestCase("b_spline_surface_with_knots", "name, u_degree, v_degree, control_points_list, surface_form, u_closed, v_closed, self_intersect, u_multiplicities, v_multiplicities, u_knots, v_knots, knot_spec")]
    [TestCase("rational_b_spline_surface", "name, u_degree, v_degree, control_points_list, surface_form, u_closed, v_closed, self_intersect, weights_data")]
    [TestCase("trimmed_curve", "name, basis_curve, trim_1, trim_2, sense_agreement, master_representation")]
    [TestCase("surface_curve", "name, curve_3d, associated_geometry, master_representation")]
    [TestCase("rectangular_trimmed_surface", "name, basis_surface, u1, u2, v1, v2, usense, vsense")]
    [TestCase("vertex_point", "name, vertex_geometry")]
    [TestCase("edge_curve", "name, edge_start, edge_end, edge_geometry, same_sense")]
    [TestCase("oriented_edge", "name, *edge_start, *edge_end, edge_element, orientation")]
    [TestCase("edge_loop", "name, edge_list")]
    [TestCase("face_bound", "name, bound, orientation")]
    [TestCase("advanced_face", "name, bounds, face_geometry, same_sense")]
    [TestCase("closed_shell", "name, cfs_faces")]
    [TestCase("oriented_closed_shell", "name, *cfs_faces, closed_shell_element, orientation")]
    [TestCase("manifold_solid_brep", "name, outer")]
    [TestCase("si_unit", "*dimensions, prefix, name")]
    [TestCase("conversion_based_unit", "dimensions, name, conversion_factor")]
    [TestCase("uncertainty_measure_with_unit", "value_component, unit_component, name, description")]
    [TestCase("geometric_representation_context", "context_identifier, context_type, coordinate_space_dimension")]
    [TestCase("shape_representation_relationship", "name, description, rep_1, rep_2")]
    public static void SimpleInstanceRecord(string entity, string expected)
        => Assert.That(LoadOrIgnore(StepSchemaFiles.Ap242).Layout(entity).Record(), Is.EqualTo(expected));

    [Test]
    public static void SimpleInstancePositionsCountUp()
    {
        var slots = LoadOrIgnore(StepSchemaFiles.Ap242).Layout("b_spline_surface_with_knots");
        Assert.That(slots.Select(s => s.Position), Is.EqualTo(Enumerable.Range(0, slots.Count)));
        Assert.That(slots.Single(s => s.Attribute.Name == "u_knots").Position, Is.EqualTo(10));
    }

    [Test]
    public static void ConversionBasedUnitDerivesDimensionsOnlyInAp214()
    {
        Assert.That(LoadOrIgnore(StepSchemaFiles.Ap214).Layout("conversion_based_unit").Record(), Is.EqualTo("*dimensions, name, conversion_factor"));
        Assert.That(LoadOrIgnore(StepSchemaFiles.Ap203).Layout("conversion_based_unit").Record(), Is.EqualTo("dimensions, name, conversion_factor"));
    }

    private static Dictionary<string, string> PartialRecords(string edition, params string[] partials)
        => LoadOrIgnore(edition).Layout(partials)
            .GroupBy(s => s.Entity)
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.OwnPosition).Record());

    [Test]
    public static void ComplexUnitPartialRecords()
    {
        var records = PartialRecords(StepSchemaFiles.Ap242, "length_unit", "named_unit", "si_unit");
        Assert.That(records["named_unit"], Is.EqualTo("*dimensions"));
        Assert.That(records["si_unit"], Is.EqualTo("prefix, name"));
        Assert.That(records.ContainsKey("length_unit"), Is.False, "length_unit has no attributes of its own");
    }

    [Test]
    public static void ComplexRationalBSplineSurfacePartialRecords()
    {
        var records = PartialRecords(StepSchemaFiles.Ap242, "bounded_surface", "b_spline_surface", "b_spline_surface_with_knots",
            "geometric_representation_item", "rational_b_spline_surface", "representation_item", "surface");
        Assert.That(records["b_spline_surface"], Is.EqualTo("u_degree, v_degree, control_points_list, surface_form, u_closed, v_closed, self_intersect"));
        Assert.That(records["b_spline_surface_with_knots"], Is.EqualTo("u_multiplicities, v_multiplicities, u_knots, v_knots, knot_spec"));
        Assert.That(records["rational_b_spline_surface"], Is.EqualTo("weights_data"));
        Assert.That(records["representation_item"], Is.EqualTo("name"));
        Assert.That(records.Keys, Is.EquivalentTo(new[] { "b_spline_surface", "b_spline_surface_with_knots", "rational_b_spline_surface", "representation_item" }));
    }

    [TestCase(StepSchemaFiles.Ap242, 2140, 238)]
    [TestCase(StepSchemaFiles.Ap214, 915, 63)]
    [TestCase(StepSchemaFiles.Ap203, 1006, 92)]
    public static void EveryEntityHasALayout(string edition, int entities, int withSeveralSupertypes)
    {
        var lookup = LoadOrIgnore(edition);
        Assert.That(lookup.Count, Is.EqualTo(entities));
        Assert.That(lookup.Values.Count(e => e.Supertypes.Count > 1), Is.EqualTo(withSeveralSupertypes));
        foreach (var e in lookup.Values)
            Assert.That(lookup.Layout(e.Name).Count, Is.GreaterThanOrEqualTo(e.Attributes.Count), e.Name);
    }
}
