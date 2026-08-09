# Architectural Gridlines — Implementation Summary

Implemented `Claude/FEMEX_Gridlines.md` in full. Clean build (0 warnings, 0 errors);
85 tests pass (was 50).

## New files

| File | What |
| --- | --- |
| `Geometry/Grids/Grid.cs` | a named set of lines, with an origin, a rotation and an optional drawing extent |
| `Geometry/Grids/GridExtent.cs` | where a viewer stops drawing — a rectangle in grid-local coordinates |
| `Geometry/Grids/Gridline.cs` | the polymorphic base; `Label` is the identity within a grid |
| `Geometry/Grids/OrthogonalGridline.cs` | `"orthogonal"` — direction + offset |
| `Geometry/Grids/FreeGridline.cs` | `"free"` — two grid-local points, any angle |
| `Geometry/Grids/GridDirection.cs` | `X` / `Y`, naming the direction a line **runs** |
| `FemexModel.Grids.cs` | the transform, the lookups, `TryGetIntersection`, `GetOrAddNodeAtGrid` |
| `griffel-femex.Tests/GridTests.cs` | 16 facts: level resolution, lookups, intersections, snapping |

## Modified

- **`FemexModel.cs`** — gained `Grids` and `DefaultGridIds`, placed before `Levels` so the JSON
  keeps its referenced-before-referencer order.
- **`Geometry/Level.cs`** — gained `List<int>? GridIds` and, with it, a `/// <summary>` class doc
  it did not have; the three-way resolution rule is stated there.
- **`FemexModel.Validation.cs`** — `ValidateGrids` (errors) after `ValidateDuplicateIds`,
  `ValidateGridGeometry` (warnings) last; `ValidationContext` gained `GridIds`; one
  `ReportDuplicates` line for the grid id space.
- **`griffel-femex.Tests/SampleModels.cs`** — two grids, `PrimaryGridId` / `CoreGridId`, and the
  `PrimaryGrid()` / `CoreGrid()` accessors.
- **`griffel-femex.Tests/RoundTripTests.cs`**, **`ValidationTests.cs`** — 4 and 13 new facts.

## The resolution rule

`Level.GridIds` is the format's first deliberately un-initialized collection outside
`AreaLoad.NodeSequence`, and for the same reason — `null` means something an empty list does not:

```
null       - inherit FemexModel.DefaultGridIds
empty list - this level deliberately has no grid
non-empty  - replaces the default entirely; it is NOT merged with it
```

Only one piece of code resolves it, `GetGridsForLevel`. `ValidateGridGeometry` calls that helper
rather than re-implementing the rule, so the validator and the authoring path cannot disagree
about which grids a level has — the argument the Node Sharing change made for sharing one
tolerance between `FindNodeAt` and `Validate()`.

The sample model makes the "not merged" half visible: level 1 wants the primary grid *and* the
core grid, so it names both, even though the primary one is already the model default.

## The snapping primitive

```csharp
model.GetOrAddNodeAtGrid(CoreGridId, "CA", "C1", levelNumber: 1)   // -> node 21, no node added
```

`TryGetIntersection` converts both labelled lines to a point and a **unit** direction in
grid-local coordinates, solves the crossing there, and transforms the result once. Keeping the
solve in grid-local means the rotation is applied to exactly one point rather than to every line,
and unit directions make the parallel test `|d₁ × d₂| < ε` a true angular one — a sine, not a
length, so unlike every other tolerance in the codebase it needs no scaling to the model's
extent. That is why `ParallelDirectionTolerance` exists as its own name even though its value is
`RelativeGeometricTolerance`.

Because it finishes through `GetOrAddNode`, a contour authored entirely from grid labels shares
every node it meets and raises no coincident-node warning.

## Validation

Eight errors and two warnings. The severity split follows the rule the Node Sharing change set:
anything the format forbids is an error, a warning is always about legal FEMEX.

Nothing about a grid can make a model unsolvable — grids are annotation. The errors are errors
anyway because a grid whose lines cannot be told apart cannot locate anything, which is the whole
of what a grid is for:

```
Grid 1 has a line with no label.
Grid 1 has more than one line labelled "A".
Grid 1 line "D2" has coincident end points and defines no direction.
Grid 1 has an extent whose minX is not less than its maxX.
Level 1 references unknown grid 99.
Level 1 repeats grid 2.
```

The two warnings are about grids that are legal but hard to use:

```
Grid 1 lines "A" and "A2" are the same line.
Level 1 uses grids 1 and 2, which both have a line labelled "B". A location given by
label alone is ambiguous.
```

The second is the cost of decision 2. Letting a level carry several grids means labels can
collide across them, and "grid B" stops being an address at exactly the moment someone is
standing on site trying to use it.

Both reference lists — the model default and a level's override — go through one
`ValidateGridReferences(ctx, gridIds, owner)` helper, so they cannot drift apart in wording or in
what they check.

## Deviations from the plan

1. **`ReportDuplicates` was not generalized to `IEnumerable<T>`.** The plan expected a string
   overload for labels, but a duplicate label is grid-scoped and reads far better with its own
   message naming the grid than as a bare `Duplicate gridline label A.` — so the generic
   refactor bought nothing and was dropped.
2. **`ValidateGrids` registers before `ValidateNodes`, not after.** Grids sit before levels in
   the model, and levels before nodes; the check order now mirrors the data order.
3. **`ParallelDirectionTolerance` is a new named constant.** The plan said to test the cross
   product against "the shared tolerance", but the shared tolerance is a length and the cross
   product of two unit directions is a sine. Same value, different dimension, so it needed its
   own name rather than a misleading reuse.
4. **The core grid gained a third line, `C2`.** With only `CA` and `C1` the sample's rotated
   intersection lands on the grid origin, where a wrong rotation is indistinguishable from a
   right one. `CA` ∩ `C2` is at `(3 − 4/√2, 3 + 4/√2)`, which only a correct transform produces.
5. **`GetGridsForLevel` is eager.** Declared `IEnumerable<Grid>` as planned, but it builds a list
   rather than using `yield`, so its unknown-level `InvalidOperationException` is thrown when it
   is called and not when the result is first enumerated.
6. **Six tests beyond the plan.** `GetGridsForLevel_OverrideIsNotMergedWithTheDefault` and
   `Level_GridIds_RoundTripEmptyAsDistinctFromNull` pin the resolution rule, which is the one
   thing here a consumer can silently get wrong; `TryGetIntersection_IsIndependentOfArgumentOrder`
   and `ToGridLocal_InvertsToModelPoint` guard the geometry; and two `Accepts_*` facts guard the
   near misses each warning could over-report on.
7. **Coordinate assertions use `Assert.Equal(expected, actual, precision)`.** xUnit 2.4.2 has no
   `double tolerance` overload — that arrived in 2.5 — so the tests compare to 9 decimal places.

## Still open

- **`"radial"` and `"circular"`** are named in `Gridline`'s XML doc and not implemented. A curved
  gridline needs a snap rule of its own (a line and an arc cross twice), which is why they were
  not carried in on the back of this change.
- **`Examples/Example1.femex` has no grids.** It deserializes and validates unchanged — missing
  keys become the initialized empty lists — but the one real model in the repo is still set out
  in raw coordinates.
- **Nothing consumes the extent.** There is no viewer, so `GridExtent` is validated and
  round-tripped but never drawn from.
