# Bar local axes and load direction — Implementation Summary

Implemented `Claude/FEMEX_BarLocalAxes_LoadDirection.md` in full. Clean build (0 warnings, 0 errors);
136 tests pass (was 103).

## New files

| File | What |
| --- | --- |
| `Loads/LoadCoordinateSystem.cs` | `Global` / `Local` |
| `Loads/LoadDirection.cs` | `X` / `Y` / `Z` / `Vector` |
| `Loads/DistributedLoad.cs` | abstract `DistributedLoad : Load` — `CoordinateSystem`, `Direction`, `Dx`/`Dy`/`Dz`, `Projected` |
| `Geometry/Vector3d.cs` | `readonly record struct` with `Length`, `Normalized()`, `Dot`, `Cross`, `Zero`/`UnitX`/`UnitY`/`UnitZ` and `+ - *` |
| `FemexModel.LocalAxes.cs` | `TryGetBarLocalAxes`, `TryGetPlateLocalAxes`, `TryGetLoadDirection`, plus the private `TryGetNewellNormal` / `TryGetContourAxes` / `Roll` |
| `griffel-femex.Tests/LoadDirectionTests.cs` | 33 facts |

## Modified

- **`FemexModel.cs`** — `string? SchemaVersion` declared **first**, no initializer, plus
  `public const string CurrentSchemaVersion = "1.1"`. `ToJson()` stamps `SchemaVersion ??=
  CurrentSchemaVersion` before serializing, documented as its one deliberate mutation.
- **`Loads/AreaLoad.cs`**, **`Loads/LinearLoad.cs`** — reparented to `DistributedLoad`; `LinearLoad`
  gained `int? BarId`. The `[JsonDerivedType]` registrations on `Load` were left exactly as they
  were, and a round-trip fact confirms STJ sees straight through the intermediate abstract class.
- **`Geometry/Bar.cs`**, **`Geometry/Plate.cs`** — the axis conventions as XML docs. `RotationAngle`'s
  comment ("relative to global X-axis") is replaced: it is a roll of y and z *about local x*.
- **`FemexModel.Validation.cs`** — `ValidateLoadOrientation` (five errors) inside `ValidateLoads`,
  `ValidateSchemaVersion` and `ValidateProjectedLoads` (three warnings), a shared `Describe(Load)`,
  and `TryGetPlanarityDeviation` refactored onto the extracted Newell normal.
- **`Examples/Example1.femex`** — migrated; see below.
- **`griffel-femex.Tests/SampleModels.cs`** — a `SchemaVersion`, three directed loads (`A2`, `A3`,
  `L2`) and three accessors (`AreaLoad(label)`, `LinearLoad(label)`, `Column()`).
- **`griffel-femex.Tests/RoundTripTests.cs`**, **`ValidationTests.cs`** — new key/enum assertions,
  three `Reports_AreaLoad*` facts retargeted from `.OfType<AreaLoad>().Single()` to
  `AreaLoad("A1")`, and the example counts updated.
- **`Claude/FEMEX.md`** — four cross-reference notes: the root's `SchemaVersion`, the Z-up global
  frame, and the bar and plate conventions.

## The convention, executable

```csharp
model.TryGetBarLocalAxes(barId,     out var x, out var y, out var z);
model.TryGetPlateLocalAxes(plateId, out var x, out var y, out var z);
model.TryGetLoadDirection(load,     out var direction);   // unit vector, global coordinates
```

`TryGetLoadDirection` is the one call a consumer needs: it resolves `Global`/`Local`/`Vector` for
any `DistributedLoad` into a global unit vector and answers false — never throws — when the host or
the geometry does not resolve. On the migrated example the four `SDL` loads come back as
`(0, 0, 1)` against a negative magnitude, and the new wind panel as `(1, 0, 0)`.

`Roll(axis, degrees, ref first, ref second)` turned out to be one operation serving both
`Bar.RotationAngle` (about local x) and `Plate.LocalAxisAngle` (about local z), so the two angles
cannot drift apart in sign either.

## Validation

Five errors — a load whose direction cannot be resolved, or that says two things at once:

```
Linear load 'L2' has a local direction but no barId; there is nothing to resolve it against.
Linear load 'L2' references unknown bar 99.
Linear load 'L2' names element 10 as its bar, but that element is not a bar.
Area load 'A3' has direction Vector but does not set all of dx, dy and dz.
Area load 'A3' has direction Vector with dx, dy and dz all zero, which is no direction at all.
Area load 'A1' sets dx/dy/dz but its direction is Z; they are only read for direction Vector.
Area load 'A2' is projected and in local coordinates. None of the programs FEMEX targets has a
projected local variant, and the concept is not meaningful: a local direction is already defined
relative to the surface being projected.
```

Three warnings — legal FEMEX that is more often an oversight:

```
The model has no schemaVersion, so it was written before load directions existed: its distributed
loads are read as acting along global +Z, and every gravity load in it therefore has the wrong sign.
The model declares schemaVersion "2.0", which this build does not recognise; it is read as 1.1.
Area load 'A2' is projected but its direction lies in the loaded surface's plane, so the projected
area is zero.
Linear load 'L1' is projected but its direction runs along the loaded line, so the projected
length is zero.
```

The two projection warnings are computed rather than asserted: the projected length is
`|extent × direction|` and the projected area factor is `|normal · direction|`, both of which the
unit direction makes the quantity they look like. Both are skipped when the geometry or the
direction does not resolve, and for a local load, whose projection is already an error.

## `Examples/Example1.femex`

Migrated by loading it, mutating the object graph and saving it back through `ToJson()`, so the
file is exactly what the serializer emits rather than hand-edited into approximately that.

- `"schemaVersion": "1.1"` as the first key.
- 8 area loads re-signed (`1.5 → −1.5` ×4, `3 → −3` ×3, `1 → −1`) and 56 linear cladding loads
  re-signed `6 → −6`.
- Every distributed load now carries explicit `"coordinateSystem"`, `"direction"` and
  `"projected"`; `"direction"` reads next to `"magnitude"`.
- Two loads added: `Snow L4`, a projected global-Z load on the roof panel, and `Wind +X face 4101`,
  a `Local` + `Z` pressure on a core wall whose contour normal is global +X.
- The 16 point loads and the temperature load are untouched.

## Deviations from the plan

1. **The vertical-bar threshold is a plan distance, not a direction cosine.** The plan wrote
   `|x̂·Ẑ| > 1 − tol` with `tol` from `GetCoincidenceTolerance()`, and flagged the formulation as
   open. That comparison is dimensionally wrong: the tolerance is a length scaled to the model, so
   in a millimetre model `1 − tol` would call anything within ~10° of vertical vertical. Implemented
   instead as *the bar's two ends are coincident in plan to within the coincidence tolerance* —
   the same tolerance, used as the distance it actually is, and still scaling with the model. This
   answers the plan's second open question with a third option rather than either of the two it
   offered.
2. **A vertical bar's local z is `x̂ × ŷ`, not literally global +Y.** For the upward column the
   convention describes they are the same vector; for a bar drawn downward, taking +Y literally
   would hand out a left-handed triad to anyone who then takes a cross product.
   `BarLocalAxes_AreRightHanded_ForABarDrawnDownward` pins it.
3. **The example gained a load case and a combination.** The plan asked for "one roof area load with
   `projected: true`" without saying which case. Snow is the load the projection concept exists for,
   and filing it under "Live - office" would have been wrong, so the file gained load case 5
   ("Snow - roof", `Snow`) and combination 106 (`1.2G + 1.5S + 0.4Q`, ultimate, in the envelope) so
   the new case is not an orphan. `Example1_LoadsAndValidates` now expects 10 area loads, 8
   combinations and 6 ULS envelope members.
4. **The example also gained `"grids": []` and `"defaultGridIds": []`.** A side effect of
   regenerating it through the serializer: empty lists are always written, and the hand-authored
   file had omitted them. This restores the property the load-combinations change valued — the
   reference file is now byte-identical to `ToJson()` and does not drift on the next round trip,
   which a fact in the migration run checked directly.
5. **The wind panel is a core wall, not an external one.** The example has no facade panels — its
   wind is applied as 16 perimeter point loads — so core wall 4101, whose contour normal is
   global +X, is the panel that demonstrates `Local` + `Z` most legibly against load case "Wind +X".
6. **`Vector3d` carries a little more than the plan's list**: `Zero`/`UnitX`/`UnitY`/`UnitZ` and
   `+ - *`, without which the axis code reads as component arithmetic rather than as the vector
   algebra it is. It is still never serialized and never stored on an entity.
7. **The Newell extraction went further than "call the shared normal".**
   `TryGetPlanarityDeviation` now takes `IReadOnlyList<Vector3d>` instead of three `double[]`s,
   which is what let it share `TryGetNewellNormal` at all. Its degenerate-contour floor (`1e-12`,
   twice a vector area rather than a length) became the named `DegenerateContourArea`.
8. **33 new facts rather than "roughly 12".** The plan's list is all there; the extra ones cover the
   `ToJson()` stamp in both directions, a reversed contour reversing the normal, a downward bar, a
   free-polygon host, a projected load that projects to *something*, and the loads that carry no
   direction at all.
9. **Verification step 5 is a permanent assertion, not a one-off**, following the load-combinations
   precedent: `Example1_GravityLoadsResolveDownward` resolves all 64 dead and live loads in the
   reference file through `TryGetLoadDirection` and asserts each one points down, then checks the
   wind panel resolves to the wall normal. This is the fact that says the re-signing was right
   rather than merely self-consistent.

## Verified

- `dotnet build`: 0 warnings, 0 errors. `dotnet test`: 136 passed, 0 failed.
- `FemexModel.Load("Examples/Example1.femex").Validate()` returns **no errors and no warnings** —
  the missing-version warning included, which is the new file proving the new check.
- The example round-trips exactly: `file == ToJson()` and `ToJson()` is idempotent, with
  `"schemaVersion"` first and `"direction"` beside `"magnitude"`.
- A 1.0 fixture — no `schemaVersion`, bare magnitudes — deserializes to `Global`/`Z`/`false` and
  raises the wrong-sign warning, so the legacy path is exercised rather than assumed.

## Still open

- **`Vector` with `CoordinateSystem = Local`** is allowed and resolved correctly, and is now
  covered by a fact, but still has no producer or consumer among the five target programs. Worth
  removing if it never finds a reader.
- **`schemaVersion` is a string compared for equality.** `"1.1"` passes, anything else warns. A real
  version policy — what to do with `"1.2"` versus `"2.0"` — belongs with the metadata change.
- **Nothing is validated against a real export.** The sign and projection conventions are taken from
  vendor documentation; one real ETABS or RFEM export should be round-tripped before they are
  treated as settled.
- **Everything the plan put out of scope stays out**: self-weight, support local axes, bar end
  offsets, `TemperatureLoad.GradientPerDepth`'s missing axis (now unblocked by the convention
  written down here), a coordinate system on `PointLoad`, and the rest of the file metadata.
