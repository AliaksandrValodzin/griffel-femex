# Architectural gridlines — setting out, and the grids a level is set out on

> **Step 0 (repo convention):** this document sits alongside `Claude/FEMEX.md`,
> `Claude/FEMEX_Plates.md` and `Claude/FEMEX_Node_Sharing.md`;
> `Claude/FEMEX_Gridlines_Summary.md` records what was actually built.

## Context

FEMEX has no way to say *where* anything is in architectural terms. A node is a bare
`(x, y, levelNumber, verticalOffset)`; nothing records that it sits on grid A/1. That costs in
two places: authoring geometry means typing raw coordinates instead of snapping to the grid the
drawings are set out on, and a model handed to site carries no way to locate an element the way
a setting-out drawing does.

Gridlines are **non-structural annotation**. They generate no elements, no nodes and no degrees
of freedom, and deleting every grid from a model leaves the analysis unchanged. The nearest
precedent in the format is `Level` — pure reference data, referenced by id, with no back-links.

## The four decisions

1. **Per-line list, not parallel spacing arrays.** A `Grid` holds one flat list of lines; a
   regular orthogonal grid is just evenly spaced offsets. Each line carries its own label, so
   labels and geometry cannot fall out of step, and a grid can be part regular and part not
   without changing shape.
2. **Model default + per-level override, both sets.** `FemexModel.DefaultGridIds` is a list;
   `Level.GridIds` is a nullable list that overrides it entirely. One grid can serve a whole
   building at the cost of one line, and a level can still carry a second, rotated grid.
3. **Lines are mathematically infinite in plan.** A `Grid` carries an optional rectangular
   drawing extent, expected to reach *past* the model's bounding box. It is a drawing hint and
   never limits snapping.
4. **Orthogonal + free only.** `"radial"` and `"circular"` are reserved as discriminators in the
   XML doc and not implemented — the move `SurfaceProperty` already made for `"variable"` /
   `"layered"`.

---

## Data model

New folder `Geometry/Grids/`, alongside `Geometry/Sections/` and `Geometry/Surfaces/`.

### `Geometry/Grids/Grid.cs`

| Member | Type | Notes |
| --- | --- | --- |
| `Id` | `int` | its own id space |
| `Name` | `string?` | "Primary", "Core" |
| `OriginX`, `OriginY` | `double` | grid-local origin, in model plan coordinates |
| `RotationAngle` | `double` | degrees counter-clockwise about global +Z — the sign convention of `Plate.LocalAxisAngle` |
| `Extent` | `GridExtent?` | drawing only; omitted from JSON when null |
| `Lines` | `List<Gridline>` | owned children, labels unique within the list |

### `Geometry/Grids/GridExtent.cs`

`MinX`, `MaxX`, `MinY`, `MaxY`, all `double`, all in **grid-local** coordinates. Where a viewer
stops drawing and places label bubbles; normally larger than the model's bounding box, because a
grid is drawn running past the building it sets out. When absent, a viewer falls back to the
model's own bounds. It never limits snapping and never clips an intersection.

### `Geometry/Grids/Gridline.cs` (abstract base)

```
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(OrthogonalGridline), "orthogonal")]
[JsonDerivedType(typeof(FreeGridline), "free")]

abstract class Gridline
    string Label            // identity within its grid; must not be blank or repeat
```

`Label` is the first **string** key in FEMEX and the first identity the format cannot
auto-number — compared ordinally and case-sensitively, and always authored.

### `Geometry/Grids/OrthogonalGridline.cs` : `Gridline`

| Member | Type | Notes |
| --- | --- | --- |
| `Direction` | `GridDirection` | `X` or `Y` — **the direction the line runs** |
| `Offset` | `double` | position along the *perpendicular* local axis |

Precisely: `Direction = Y` is the grid-local vertical line at local X = `Offset` (conventionally
the lettered lines); `Direction = X` is the grid-local horizontal line at local Y = `Offset`
(conventionally the numbered ones).

### `Geometry/Grids/GridDirection.cs`

`enum GridDirection { X, Y }`, one enum per file, serialized as a string.

### `Geometry/Grids/FreeGridline.cs` : `Gridline`

`X1`, `Y1`, `X2`, `Y2` — `double`, grid-local. Two distinct points giving the line's position and
direction. They are not its ends: like every gridline it is infinite in plan.

### Coordinate transform

All line geometry is grid-local. With `θ = RotationAngle` in degrees:

```
x_model = OriginX + u·cos θ − v·sin θ
y_model = OriginY + u·sin θ + v·cos θ
```

and its inverse for model → grid-local. It lives in `FemexModel.Grids.cs`, not on `Grid`, so the
model stays a plain data class.

### Modified: `FemexModel.cs`

Two properties before `Levels`, following the file's referenced-before-referencer order
(`Sections` / `SurfaceProperties` precede `Bars` / `Plates`):

```
List<Grid> Grids
List<int>  DefaultGridIds     // the grids every level uses unless it names its own
List<Level> Levels
```

### Modified: `Geometry/Level.cs`

```
List<int>? GridIds
    null       - inherit FemexModel.DefaultGridIds
    empty list - this level deliberately has no grid
    non-empty  - replaces the default entirely; it is NOT merged with it
```

Deliberately not initialized: `null` means something the empty list does not. The rule goes in
`Level`'s XML class doc, the way `PlateRegion` documents its resolution rule. `Level` gains a
`/// <summary>` while being touched — it is one of the older `//`-only files.

---

## Resulting JSON

```json
{
  "grids": [
    {
      "id": 1,
      "name": "Primary",
      "originX": 0, "originY": 0, "rotationAngle": 0,
      "extent": { "minX": -2, "maxX": 12, "minY": -2, "maxY": 12 },
      "lines": [
        { "type": "orthogonal", "direction": "Y", "offset": 0,  "label": "A" },
        { "type": "orthogonal", "direction": "Y", "offset": 10, "label": "B" },
        { "type": "orthogonal", "direction": "X", "offset": 0,  "label": "1" },
        { "type": "orthogonal", "direction": "X", "offset": 10, "label": "2" },
        { "type": "free", "x1": 0, "y1": 0, "x2": 10, "y2": 5, "label": "D1" }
      ]
    },
    {
      "id": 2, "name": "Core",
      "originX": 3, "originY": 3, "rotationAngle": 45,
      "lines": [
        { "type": "orthogonal", "direction": "Y", "offset": 0, "label": "CA" },
        { "type": "orthogonal", "direction": "X", "offset": 0, "label": "C1" },
        { "type": "orthogonal", "direction": "X", "offset": 4, "label": "C2" }
      ]
    }
  ],
  "defaultGridIds": [1],
  "levels": [
    { "levelNumber": 0, "name": "Ground", "absoluteElevation": 145.5, "relativeElevation": 0, "isGround": true },
    { "levelNumber": 1, "name": "First Floor", "absoluteElevation": 148.5, "relativeElevation": 3, "isGround": false, "gridIds": [1, 2] }
  ]
}
```

`System.Text.Json` emits derived-class properties before base-class ones, so `"label"` lands after
`direction` / `offset` — the ordering today's plates already show with `"regions"` before `"id"`.
Level 0's `gridIds` and grid 2's `extent` are absent rather than `null`, via the existing
`DefaultIgnoreCondition = WhenWritingNull`.

---

## `FemexModel.Grids.cs` — authoring helpers

A new partial, mirroring `FemexModel.Nodes.cs` in shape and doc style, and following its rule on
failure modes: lookups return `null` / `bool`-try, authoring helpers **throw**
`InvalidOperationException` for an id that does not resolve, documented with `/// <exception>`.

| Member | Notes |
| --- | --- |
| `IEnumerable<Grid> GetGridsForLevel(int levelNumber)` | applies the null / empty / non-empty rule; skips ids that do not resolve; throws for an unknown level |
| `Grid? FindGrid(int gridId)` | |
| `Gridline? FindGridline(int gridId, string label)` | ordinal comparison |
| `void ToModelPoint(Grid grid, double u, double v, out double x, out double y)` | grid-local → model plan |
| `void ToGridLocal(Grid grid, double x, double y, out double u, out double v)` | the inverse |
| `bool TryGetIntersection(int gridId, string labelA, string labelB, out double x, out double y)` | **the snapping primitive**; false for an unknown label, a degenerate line, or two parallel lines |
| `Node GetOrAddNodeAtGrid(int gridId, string labelA, string labelB, int levelNumber, double verticalOffset = 0.0)` | composes the above with `GetOrAddNode`, so geometry set out from a grid shares the nodes it meets |
| `int NextGridId()` | one past the highest in use, 1 for an empty model |

There is deliberately no `NextLabel()` — labels are authored, not generated.

### Tolerances

Lengths reuse `GetCoincidenceTolerance()` and the `RelativeGeometricTolerance` /
`MinimumGeometricTolerance` constants from `FemexModel.Nodes.cs`; no new length literals.
Parallelism needs one new named constant, `ParallelDirectionTolerance`, because it is a *sine*
and not a length: `TryGetLocalRay` returns unit directions, so the cross product of two of them
is `sin θ` and the test is a true angular one, needing no scaling to the model's extent. Its
value is `RelativeGeometricTolerance` — the same number, a different dimension.

---

## `Validate()` additions

`ValidateGrids` registers immediately after `ValidateDuplicateIds`, mirroring the data order
grids → levels → nodes; `ValidateGridGeometry` registers last, with the other geometric and
warning-producing checks.

Nothing here can make a model unsolvable — grids are annotation. They are errors anyway because a
grid whose lines cannot be told apart cannot locate anything, which is the whole of what a grid
is for.

**Errors** — `ValidateGrids`, plus one line in `ValidateDuplicateIds`:

| Check | Message |
| --- | --- |
| duplicate grid id | `Duplicate grid id {id}.` (via the existing `ReportDuplicates`) |
| unknown grid in the model default | `Model default grid list references unknown grid {id}.` |
| unknown grid on a level | `Level {n} references unknown grid {id}.` |
| repeated id in either list | `Model default grid list repeats grid {id}.` / `Level {n} repeats grid {id}.` |
| blank label | `Grid {id} has a line with no label.` |
| duplicate label in one grid | `Grid {id} has more than one line labelled "{label}".` |
| degenerate free line | `Grid {id} line "{label}" has coincident end points and defines no direction.` |
| back-to-front extent | `Grid {id} has an extent whose minX is not less than its maxX.` (and the Y counterpart) |

Both reference lists go through one `ValidateGridReferences(ctx, gridIds, owner)` helper, so the
model default and a level's override cannot drift apart in wording or in what they check.

**Warnings** — `ValidateGridGeometry`:

| Check | Message |
| --- | --- |
| two lines in one grid that are the same infinite line | `Grid {id} lines "{a}" and "{b}" are the same line.` |
| one label reaching two grids a level uses | `Level {n} uses grids {a} and {b}, which both have a line labelled "{label}". A location given by label alone is ambiguous.` |

The second resolves a level's grids by calling `GetGridsForLevel` rather than re-implementing the
null / empty / non-empty rule, so the validator and the authoring helper cannot disagree about it
— the argument the node-sharing change made for sharing one tolerance.

`ValidationContext` gains `GridIds`. Per the never-double-report convention, both checks skip a
grid whose id was already seen, since a repeated id is an error in its own right.

---

## Tests

**`SampleModels.cs`** — `PrimaryGridId = 1`, `CoreGridId = 2`, the two grids above,
`DefaultGridIds = { PrimaryGridId }`, level 0's `GridIds` left null (inherits) and level 1 given
`{ PrimaryGridId, CoreGridId }` (an override that must name the primary grid again, which is the
rule made visible). Extension accessors `model.PrimaryGrid()` / `model.CoreGrid()` next to
`Slab()` / `Wall()`. The core grid's origin is the drop panel's near corner, so `CA` ∩ `C1` is
node 21 and `CA` ∩ `C2` lands at `(3 − 4/√2, 3 + 4/√2)` — a point only a correct rotation
produces.

**`GridTests.cs`** (new) — level resolution (inherit, override, override-is-not-a-merge, empty
means none, throws for an unknown level); lookups (`FindGridline` is case-sensitive,
`NextGridId`); intersections (plan point, rotated grid, free line, argument order,
`ToGridLocal` inverts `ToModelPoint`, false for parallel and for an unknown label); snapping
(`GetOrAddNodeAtGrid` returns the node already there, adds where nothing is, throws for an
unknown label and for parallel lines).

**`RoundTripTests.cs`** — `Gridline_IsPolymorphic` (`"type": "orthogonal"` / `"free"`,
`"direction": "Y"` as literal substrings), `Grid_RoundTrips` (typed assertions including
`Assert.IsType<FreeGridline>`), `Level_GridIds_AreOmitted_WhenNull`, and
`Level_GridIds_RoundTripEmptyAsDistinctFromNull` — the one assertion that pins the difference
between "inherit" and "no grid" across serialization.

**`ValidationTests.cs`** — a `// ----- Gridlines -----` group with one `Reports_*` per error
message, one `Warns_*` per warning, `DuplicateLine_IsAWarning_NotAnError`, and two `Accepts_*`
facts guarding the near misses: lines that are parallel but not coincident, and a shared label on
grids that no single level uses together.

## Migration and compatibility

- `Examples/Example1.femex` needs **no change**: missing `grids` / `defaultGridIds` deserialize to
  the initialized empty lists, missing `gridIds` to `null` (inherit an empty default — no grid).
  `Example1_LoadsAndValidates` and its hard counts are unaffected.
- Every re-serialized model gains `"grids": []` and `"defaultGridIds": []`. Empty collections are
  not suppressed, only nulls — the same visible change `surfaceProperties` made. No existing test
  asserts a whole-document string.
- `Validate()` gains warnings from a second and third source. `Validate(Error)` still answers "is
  this model usable", and a consumer that ignores grids entirely still reads every model it could
  read before.

## Ordered tasks

| # | Task | Risk |
| --- | --- | --- |
| 1 | `Geometry/Grids/` — the six types, XML docs written **before** the bodies | **medium — semantics.** The doc is the contract; cheap now, expensive once files exist |
| 2 | `FemexModel.cs`: `Grids` + `DefaultGridIds` before `Levels` | low |
| 3 | `Level.GridIds` + the three-way rule in its XML doc | **medium.** null vs empty vs non-empty is the one rule a consumer can silently get wrong |
| 4 | `FemexModel.Grids.cs` — transform, lookups, `TryGetIntersection`, `GetOrAddNodeAtGrid`, `NextGridId` | medium — the rotation sign and the parallel degenerate case |
| 5 | `ValidationContext.GridIds`; `ValidateGrids` + `ValidateGridGeometry`, registered in pipeline order | low |
| 6 | Extend `SampleModels.Build()` + extension accessors | low |
| 7 | `GridTests.cs`, `RoundTripTests` and `ValidationTests` additions | low |
| 8 | `Claude/FEMEX_Gridlines_Summary.md`; `> **Extended by …**` blockquotes into the older docs rather than editing them | low |

## Deliberately out of scope

- **Radial and circular gridlines** — reserved in the XML doc only.
- **Snapping existing geometry.** No `SnapNodeToGrid`, no nearest-gridline query. `ToGridLocal`
  gives a caller the offset to decide for itself; which node moves is a modelling decision — the
  reasoning that kept `MergeCoincidentNodes()` out of the node-sharing change.
- **Grid-relative node storage.** Nodes keep model plan `X` / `Y`; nothing records "node 21 is at
  CA/C1". A node that remembered its grid would need re-solving whenever the grid moved. That is
  a CAD feature, not a file-format one.
- **A grid's own level range or elevation.** The reference direction is `Level → Grid`. If a grid
  ever needs a vertical extent that is a field on `Grid`, not a reversal of the reference.
- **Case-insensitive label collisions.** `"a"` and `"A"` are two lines. Genuinely ambiguous on a
  drawing, but a warning for it is noise until someone hits it.
- **A `Point` type.** The repo has none — every geometric API here uses `out` parameters, matching
  `TryGetPoint` / `TryGetBounds`.

## Verification

1. `dotnet build` — 0 warnings, 0 errors.
2. `dotnet test` — the existing 50 pass unchanged, plus the new facts.
3. `Assert.Empty(SampleModels.Build().Validate())` — the sample, grids included, is clean.
4. Eyeball the emitted sample JSON against the shape above: camelCase, `"type"` discriminators,
   `"direction": "Y"` as a string, `extent` absent on the core grid, `gridIds` absent on level 0.
5. End-to-end gate: `GetOrAddNodeAtGrid(CoreGridId, "CA", "C1", levelNumber: 1)` on the sample
   exercises the 45° rotation, the intersection solve, the coincidence tolerance and node sharing
   in one call, and must return node 21 rather than adding one.
