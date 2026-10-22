using System.Reflection;
using System.Text.Json.Serialization;
using Ara3D.StepBrep;
using Type = System.Type;

namespace Ara3D.Step.CorpusHarness;

/// <summary>The part of a prepared model a set of faces needs, as a model of its own: those faces, and every loop,
/// edge, vertex, curve, 2D curve, pcurve and surface they reach, renumbered in their original order; the closure marks
/// whose curve or surface is kept; one open shell holding the faces, in one shell-set body; the model's tolerances and
/// unit. Reaching works on the generated C# values by reflection, so a new field or sum case of the library is
/// followed without a change here: an index type (BrepEdgeIndex, CurveIndex, ...) leads into its table, and a sum
/// value's inactive case fields are left alone. <see cref="Faces"/> maps each face of the sub-model to its face in
/// the source model.</summary>
public sealed record SubModel(BrepModel Model, IReadOnlyList<int> Faces)
{
    /// <summary>The faces of <paramref name="m"/> named, with their neighbours <paramref name="ring"/> edges deep (faces
    /// sharing an edge), as a model of their own.</summary>
    public static SubModel Of(BrepModel m, IReadOnlyList<int> faces, int ring = 0)
    {
        var chosen = Ring(m, faces, ring);
        var tables = new Tables(m);
        var kept = tables.Kinds.ToDictionary(k => k, _ => new SortedSet<int>());
        foreach (var f in chosen)
            tables.Reach(typeof(BrepFaceIndex), f, kept);
        // Closure marks name a curve, 2D curve or surface and nothing names them: keep each whose subject is kept.
        var closures = m.Geometry.Closures.Where(c => Refs(c).All(r => kept[r.Type].Contains(r.Index))).ToArray();
        var map = kept.ToDictionary(kv => kv.Key, kv => kv.Value.Select((old, i) => (old, i)).ToDictionary(p => p.old, p => p.i));
        T[] Table<T>(IReadOnlyList<T> source, Type index)
            => kept[index].Select(i => (T)Remap(source[i]!, map)).ToArray();
        var geometry = new GeometrySet(
            Table(m.Geometry.Curves, typeof(CurveIndex)), Table(m.Geometry.Curves2D, typeof(Curve2DIndex)),
            Table(m.Geometry.Pcurves, typeof(PcurveIndex)), Table(m.Geometry.Surfaces, typeof(SurfaceIndex)),
            closures.Select(c => (ClosureMark)Remap(c, map)).ToArray());
        var faceTable = Table(m.Faces, typeof(BrepFaceIndex));
        var shell = new BrepShell(Enumerable.Range(0, faceTable.Length).Select(i => new BrepFaceIndex(i)).ToArray(), false);
        var model = new BrepModel(geometry, Table(m.Vertices, typeof(BrepVertexIndex)), Table(m.Edges, typeof(BrepEdgeIndex)),
            Table(m.Loops, typeof(BrepLoopIndex)), faceTable, [shell], [BrepBody.ShellSet([new ShellIndex(0)])], m.Tolerances, m.Unit);
        return new(model, kept[typeof(BrepFaceIndex)].ToArray());
    }

    /// <summary>The faces, then every face sharing an edge with one of them, <paramref name="ring"/> times.</summary>
    static IReadOnlyList<int> Ring(BrepModel m, IReadOnlyList<int> faces, int ring)
    {
        var byFace = m.EdgesByFace();
        var facesOfEdge = new Dictionary<int, List<int>>();
        for (var f = 0; f < byFace.Length; f++)
            foreach (var e in byFace[f])
                (facesOfEdge.TryGetValue((int)e.Value, out var l) ? l : facesOfEdge[(int)e.Value] = []).Add(f);
        var set = new SortedSet<int>(faces);
        for (var r = 0; r < ring; r++)
            foreach (var f in set.ToArray())
                foreach (var e in byFace[f])
                    set.UnionWith(facesOfEdge[(int)e.Value]);
        return set.ToArray();
    }

    /// <summary>The model's tables by the index type that points into them.</summary>
    sealed class Tables(BrepModel m)
    {
        readonly Dictionary<Type, Func<int, object>> rows = new()
        {
            [typeof(CurveIndex)] = i => m.Geometry.Curves[i],
            [typeof(Curve2DIndex)] = i => m.Geometry.Curves2D[i],
            [typeof(PcurveIndex)] = i => m.Geometry.Pcurves[i],
            [typeof(SurfaceIndex)] = i => m.Geometry.Surfaces[i],
            [typeof(BrepVertexIndex)] = i => m.Vertices[i],
            [typeof(BrepEdgeIndex)] = i => m.Edges[i],
            [typeof(BrepLoopIndex)] = i => m.Loops[i],
            [typeof(BrepFaceIndex)] = i => m.Faces[i],
        };

        public IEnumerable<Type> Kinds => rows.Keys;

        /// <summary>Keeps row <paramref name="i"/> of the table <paramref name="index"/> points into, and everything it reaches.</summary>
        public void Reach(Type index, int i, Dictionary<Type, SortedSet<int>> kept)
        {
            if (!kept[index].Add(i))
                return;
            foreach (var (type, j) in Refs(rows[index](i)))
                if (rows.ContainsKey(type))
                    Reach(type, j, kept);
        }
    }

    /// <summary>Every index a value holds in its active fields, with the index type.</summary>
    static IEnumerable<(Type Type, int Index)> Refs(object value)
    {
        if (value is null)
            yield break;
        var t = value.GetType();
        if (IsIndex(t))
        {
            yield return (t, (int)(Integer)IndexField(t).GetValue(value)!);
            yield break;
        }
        if (IsList(t))
        {
            foreach (var x in (System.Collections.IEnumerable)value)
                foreach (var r in Refs(x))
                    yield return r;
            yield break;
        }
        foreach (var f in ActiveFields(value))
            foreach (var r in Refs(f.GetValue(value)!))
                yield return r;
    }

    /// <summary>The value with every index in an active field renumbered by <paramref name="map"/>, built again through
    /// its JSON constructor (the all-fields constructor every generated record and sum has).</summary>
    static object Remap(object value, Dictionary<Type, Dictionary<int, int>> map)
    {
        var t = value.GetType();
        if (IsIndex(t))
        {
            var old = (int)(Integer)IndexField(t).GetValue(value)!;
            if (!map.TryGetValue(t, out var m) || !m.TryGetValue(old, out var now))
                return value; // a table this cut does not renumber (shells, bodies), or a none index
            return Activator.CreateInstance(t, (Integer)now)!;
        }
        if (IsList(t))
        {
            var element = ElementType(t);
            var source = ((System.Collections.IEnumerable)value).Cast<object>().ToArray();
            var array = Array.CreateInstance(element, source.Length);
            for (var i = 0; i < source.Length; i++)
                array.SetValue(Remap(source[i], map), i);
            return array;
        }
        var ctor = JsonConstructor(t);
        if (ctor == null)
            return value; // a primitive: Number, Integer, Boolean, String
        var active = ActiveFields(value).ToHashSet();
        var args = DataFields(t).Select(f => active.Contains(f) ? Remap(f.GetValue(value)!, map) : f.GetValue(value)).ToArray();
        return ctor.Invoke(args);
    }

    /// <summary>The fields a value's identity rests on, in declaration order: every field of a record; the
    /// discriminant and the active case's fields of a sum.</summary>
    public static IEnumerable<FieldInfo> ActiveFields(object value)
    {
        var t = value.GetType();
        var fields = DataFields(t);
        var kind = CaseName(value);
        return kind == null ? fields : fields.Where(f => f.Name.StartsWith(kind + "_", StringComparison.Ordinal));
    }

    /// <summary>The case name of a sum value (from its Kind_ constants), or null for a value that is not a sum.</summary>
    public static string? CaseName(object value)
    {
        var t = value.GetType();
        var kind = t.GetField("Kind", BindingFlags.Public | BindingFlags.Instance);
        if (kind == null || kind.FieldType != typeof(int))
            return null;
        var k = (int)kind.GetValue(value)!;
        return t.GetFields(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(c => c.IsLiteral && c.Name.StartsWith("Kind_") && (int)c.GetValue(null)! == k)?.Name["Kind_".Length..];
    }

    /// <summary>The serialized fields of a generated type, in their DataMember order.</summary>
    public static IReadOnlyList<FieldInfo> DataFields(Type t)
        => t.GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Select(f => (f, order: f.GetCustomAttribute<System.Runtime.Serialization.DataMemberAttribute>()?.Order))
            .Where(p => p.order != null)
            .OrderBy(p => p.order)
            .Select(p => p.f)
            .ToArray();

    public static ConstructorInfo? JsonConstructor(Type t)
        => t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(c => c.GetCustomAttribute<JsonConstructorAttribute>() != null);

    /// <summary>An index type: a generated record whose one field is an Integer named Value, and whose name ends in Index.</summary>
    static bool IsIndex(Type t)
        => t.Name.EndsWith("Index", StringComparison.Ordinal) && DataFields(t) is [{ Name: "Value", FieldType: var ft }] && ft == typeof(Integer);

    static FieldInfo IndexField(Type t) => DataFields(t)[0];

    /// <summary>An array or another IReadOnlyList, the form every Plato Array field takes in the generated C#.</summary>
    public static bool IsList(Type t) => t.IsArray || ListInterface(t) != null;

    public static Type ElementType(Type t) => t.IsArray ? t.GetElementType()! : ListInterface(t)!.GetGenericArguments()[0];

    static Type? ListInterface(Type t)
        => (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IReadOnlyList<>) ? [t] : t.GetInterfaces())
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IReadOnlyList<>));
}
