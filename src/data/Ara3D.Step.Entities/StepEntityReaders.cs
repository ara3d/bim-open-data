using Ara3D.IO.StepParser;
using Ara3D.StepTypes;

namespace Ara3D.Step.Entities;

/// <summary>
/// Typed reads through generated descriptors: e.Numbers(CartesianPoint.Instance.Coordinates). The descriptor's type
/// picks the reader, so a reader that does not fit the attribute does not compile. Each returns null when the slot
/// is absent, unset ($) or derived (*), and throws InvalidDataException when the file holds the wrong kind of value.
/// </summary>
public static class StepEntityReaders
{
    /// <summary>The slot's token, or null when it is absent, $ or *.</summary>
    public static StepToken? Value(this StepEntity e, StepAttribute attribute)
        => e.TryGet(attribute, out var token) && !token.IsUnassignedOrRedeclared ? token : null;

    public static double? Number(this StepEntity e, StepAttribute<double> attribute)
        => e.Value(attribute) is { } t ? e.ToNumber(attribute, t) : null;

    public static long? Integer(this StepEntity e, StepAttribute<long> attribute)
        => e.Value(attribute) is { } t ? (long)e.ToNumber(attribute, t) : null;

    /// <summary>.T. or .F.; the LOGICAL .U. reads as null.</summary>
    public static bool? Boolean(this StepEntity e, StepAttribute<bool> attribute)
        => e.Value(attribute) is { } t
            ? e.Expect(attribute, t, t.IsSymbol, "a boolean").Match(".T."u8) ? true : t.Match(".F."u8) ? false : null
            : null;

    public static T? Enum<T>(this StepEntity e, StepAttribute<T> attribute) where T : struct, Enum
        => e.Value(attribute) is { } t
            ? StepEnumItems<T>.Find(e.Expect(attribute, t, t.IsSymbol, "an enumeration item"))
              ?? throw e.Error(attribute, t, $"an item of {typeof(T).Name}")
            : null;

    /// <summary>The referenced instance. A SELECT slot that holds a typed value rather than a reference throws.</summary>
    public static StepEntity? Ref<T>(this StepEntity e, StepAttribute<T> attribute) where T : IStepValue
        => e.Value(attribute) is { } t ? e.ToRef(attribute, t) : null;

    public static IReadOnlyList<double>? Numbers(this StepEntity e, StepAttribute<IReadOnlyList<double>> attribute)
        => e.List(attribute, t => e.ToNumber(attribute, t));

    public static IReadOnlyList<IReadOnlyList<double>>? Numbers(this StepEntity e, StepAttribute<IReadOnlyList<IReadOnlyList<double>>> attribute)
        => e.List(attribute, row => e.Elements(attribute, row).Select(t => e.ToNumber(attribute, t)).ToList());

    public static IReadOnlyList<long>? Integers(this StepEntity e, StepAttribute<IReadOnlyList<long>> attribute)
        => e.List(attribute, t => (long)e.ToNumber(attribute, t));

    public static IReadOnlyList<StepEntity>? Refs<T>(this StepEntity e, StepAttribute<IReadOnlyList<T>> attribute) where T : IStepValue
        => e.List(attribute, t => e.ToRef(attribute, t));

    public static IReadOnlyList<IReadOnlyList<StepEntity>>? Refs<T>(this StepEntity e, StepAttribute<IReadOnlyList<IReadOnlyList<T>>> attribute) where T : IStepValue
        => e.List(attribute, row => e.Elements(attribute, row).Select(t => e.ToRef(attribute, t)).ToList());

    private static IReadOnlyList<TItem>? List<TItem>(this StepEntity e, StepAttribute attribute, Func<StepToken, TItem> item)
        => e.Value(attribute) is { } t ? e.Elements(attribute, t).Select(item).ToList() : null;

    private static IReadOnlyList<StepToken> Elements(this StepEntity e, StepAttribute attribute, StepToken token)
        => e.Expect(attribute, token, token.IsList, "a list").AsList(e.Model.Document);

    private static double ToNumber(this StepEntity e, StepAttribute attribute, StepToken token)
        => e.Expect(attribute, token, token.IsNumber, "a number").AsNumber();

    private static StepEntity ToRef(this StepEntity e, StepAttribute attribute, StepToken token)
        => e.Model.Get(e.Expect(attribute, token, token.IsId, "a reference").AsId());

    private static StepToken Expect(this StepEntity e, StepAttribute attribute, StepToken token, bool fits, string expected)
        => fits ? token : throw e.Error(attribute, token, expected);

    private static InvalidDataException Error(this StepEntity e, StepAttribute attribute, StepToken token, string expected)
        => new($"#{e.Id} {attribute.Entity.ToLowerInvariant()}.{attribute.Name}: expected {expected}, found {token.ToString(e.Model.Document)}");
}
