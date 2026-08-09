# Node Sharing — Implementation Summary

Implemented `Claude/FEMEX_Node_Sharing.md` in full. Clean build (0 warnings, 0 errors);
50 tests pass (was 35). The `griffel-femex-models` sample builds, validates clean and
regenerates `ModelOutput/SampleModel.femex`.

A node is the model's unit of connectivity — elements are joined where they name the same
node number, and only there. Model code now shares nodes, the format still allows several at
one location (that is how a deliberately disconnected joint is written), and `Validate()`
reports the ambiguity as a **warning**.

## New files

| File | What |
| --- | --- |
| `ValidationSeverity.cs` | `Error \| Warning`. The rule the doc comment states: anything the format forbids is an error, a warning is always about legal FEMEX. |
| `ValidationMessage.cs` | `Severity` + `Text`, `Error`/`Warning` factories, `ToString()` = `"{Severity}: {Text}"`. |
| `FemexModel.Nodes.cs` | `GetCoincidenceTolerance()`, `FindNodeAt`, `GetOrAddNode`, `NextNodeNumber()` — a second partial of `FemexModel`, so node lookup does not land in either `FemexModel.cs` or the validation file. |
| `griffel-femex.Tests/NodeTests.cs` | 8 facts covering the helpers and the sample model's one-node-per-location property. |

## Modified

- **`FemexModel.Validation.cs`** — `Validate()` now returns `IEnumerable<ValidationMessage>`;
  added the `Validate(ValidationSeverity)` filter. The thirteen existing check groups keep
  their exact wording and are wrapped as `Error`. Added `ValidateCoincidentNodes` (warning),
  `FindCoincidentGroups` and `FormatNodeList`. `ValidationContext` gained a
  `TryGetPoint(Node, …)` overload — the number-keyed one collapses duplicate node numbers, and
  the coincidence check has to see the `Node` objects themselves. The planarity tolerance now
  reads the shared `RelativeGeometricTolerance` / `MinimumGeometricTolerance` constants
  instead of two inline literals.
- **`Geometry/Node.cs`** — class doc: what a node *is*, why the format permits duplicates, why
  that is a warning rather than an error, and a pointer to `GetOrAddNode`.
- **`griffel-femex.Tests/SampleModels.cs`** — one node per location (below).
- **`griffel-femex.Tests/{RoundTripTests,PlateTests,ValidationTests}.cs`** — renumbered for
  the merged nodes; `AssertReports` gained a severity argument.
- **`griffel-femex-models/SampleModel.cs`**, **`Program.cs`** — the same node merge, and the
  console output now counts errors and warnings separately.

## The check

Last in the pipeline with the other geometric checks. Nodes are resolved to absolute points,
bucketed into a grid of tolerance-sized cells, compared against the 26 neighbouring cells so a
pair straddling a boundary is still found, and merged with union-find so **one location gives
one message** however many nodes are stacked there:

```
Warning: Nodes 13, 98 and 99 are at the same location (10, 10, 148.5). Elements only
connect where they reference the same node number, so unless the joint is meant to be
disconnected they should share one node.
```

Tolerance is `max(1e-6 × bounding-box diagonal, 1e-9)` — the rule the coplanarity check
already used, lifted into shared constants. Being relative it means the same thing in metres
or millimetres, and because `FindNodeAt` uses the same method, the authoring helper and the
validator cannot disagree about what "the same location" is.

Nodes whose level is unknown, and second occurrences of a node number, are skipped — both are
already errors, and a repeated number must not be reported as coincident with itself.

## The sample models

| Was | Now |
| --- | --- |
| 2 (column top, `verticalOffset: 0.2`) and 11 (slab corner) | node 2, offset dropped — the column now actually holds the slab up |
| 41 (wall base) | node 1, the column base |
| 43 (wall top right) | node 12, a slab corner |
| 44 (wall top left) | node 2 |

The library's sample drops from 20 nodes to 16; the models project's, which has a second wall
along `y = 10` sharing the slab's far edge, from 20 to 16. Removed nodes stay as commented-out
lines naming their replacement — which nodes are shared is the point of the file.

## What the check found immediately

`ValidationTests.Accepts_RegionsThatOnlyTouchAtAnEdge` built a second drop panel butting
against the first at `x = 7` with four fresh nodes, two of which duplicated the first panel's
corners 22 and 23. It now shares them and adds only the two nodes that are genuinely new. The
test still proves what it was written to prove (touching bounding boxes are not an overlap)
and no longer models two panels that are not attached to each other.

`Examples/Example1.femex` needed no change — its 100 nodes were already at 100 distinct
locations.

## Deviations from the plan

1. **`RoundTripTests` gained `Node_VerticalOffset_RoundTrips`.** Merging the column top into
   the slab corner removed the sample's only non-zero `VerticalOffset`, so the assertion that
   rode on it would have become `Assert.Equal(0.0, …)`. It is now a fact of its own that sets
   an offset, round-trips and asserts it back.
2. **`Accepts_ContourThatIsPlanarButNotAxisAligned` tilts the wall about its top edge, not its
   base.** The top corners are slab corners now, so moving them would move the slab; moving
   the base (nodes 1 and 42) tilts only the wall and keeps the contour exactly planar.

## Still open

- No `MergeCoincidentNodes()`. Which node the elements at a stacked location should keep is a
  modelling decision, and the duplicate may be intended.
- Mesh nodes are not checked for coincidence — separate id space, generated rather than
  authored, and FEMEX has no mesher.
- Nothing else warns yet. Unreferenced nodes, zero-length bars and unused load cases are the
  obvious next candidates now that severity exists.
  > **Extended by `FEMEX_Gridlines_Summary.md`:** grids were the first thing to take the axis
  > up — a line drawn twice in one grid, and one label reaching two grids on a level, are both
  > legal FEMEX and both warnings.
- A model that *wants* a disconnected joint has no way to say so explicitly, so it carries a
  warning forever. If that becomes irritating, the answer is a marker on the node (`"detached
  from": nodeNumber`, or a joint object), not a looser check.
