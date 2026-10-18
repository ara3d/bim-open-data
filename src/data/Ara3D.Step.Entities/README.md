# Ara3D.Step.Entities

The STEP entity model over a parsed `StepDocument` (`Ara3D.IO.StepParser`), with no native code (`net8.0`). It
compiles in the generated AP242 descriptors of `../Ara3D.StepTypes` and reads attributes only through them, never by
name string or a hand-counted index.

```csharp
using var document = new StepDocument(path);
var model = new StepModel(document);
var face = model.Get(3242);
if (face.Is<IAdvancedFace>())
{
    var surface = face.Ref(AdvancedFace.Instance.FaceGeometry);       // a StepEntity, simple or complex
    var sameSense = face.Boolean(AdvancedFace.Instance.SameSense);
    var weights = surface?.Numbers(RationalBSplineSurface.Instance.WeightsData);
}
```

A complex instance, `#81=(LENGTH_UNIT()NAMED_UNIT(*)SI_UNIT(.MILLI.,.METRE.))`, answers the same calls: it is an X
when any of its parts is X or a subtype of X, and an attribute is read from the part of the entity that declares it
(`unit.Enum(SiUnit.Instance.Prefix)` is `SiPrefix.Milli`). A simple instance of a subtype reads a supertype's
descriptor too (`face.Refs(Face.Instance.Bounds)`); where a second supertype moves the slot, `PositionOf` finds it.

| File | Holds |
|---|---|
| `StepModel.cs` | A document's instances by id and in file order |
| `StepEntity.cs` | One instance: `Is<T>()`, `Parts`, `IsKnown`, and `TryGet(attribute, out token)` |
| `StepEntityReaders.cs` | Typed reads chosen by the descriptor's type: `Number`, `Integer`, `Boolean`, `Enum`, `Ref`, `Numbers`, `Integers`, `Refs`; null for absent, `$` or `*`, `InvalidDataException` for the wrong kind of value |
| `StepEnumItems.cs` | A file's `.ITEM.` to the generated enum member |
| `StepSchemaIndex.cs` | An entity name token to its descriptor, without allocating |
| `StepTokenLists.cs` | The n-th element of a list token without building the list |

Strings are not decoded yet (`\X2\` escapes); read them with `Value` and the token. Tests: `tests/data/Ara3D.StepTypes.Tests`.
