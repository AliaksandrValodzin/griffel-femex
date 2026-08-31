# Line loads on plate edges — Implementation Summary

Schema **1.11**, additive, no migration: `LinearLoad` gains `PlateId` and `RegionId` beside the
`BarId` 1.9 gave it. Clean build (0 warnings, 0 errors, both legs); **632 tests pass** (was 616), of
which 16 are new. The viewer mirrors it and the parity harness reports **six of six** examples in
step, up from five — the sixth is the fixture the new rules needed.

`femex convert SAF_example_HOUSE_metric_ZYX_220.xlsx` goes from **36 findings — 25 error, 11
warning** to **25 findings — 14 error, 11 warning**. The eleven that went are the ones the tool
manufactured. The fourteen that remain are the generic-section findings, which are about the
workbook and are item 4's business.

---

## The finding

Converting SCIA's own reference workbook produced a model in which load 39 was:

```json
{ "type": "linear", "startNode": 70, "endNode": 72,
  "startPosition": 0, "endPosition": 1,
  "coordinateSystem": "Local", "direction": "Z",
  "magnitudeStart": -3000, "label": "LFS1" }
```

`Validate()` rightly returned three errors for it and eleven across the file: a local direction with
no host to resolve against, and two positions measured along a member the load does not name.

**The load is not wrong; the format could not hold it.** SAF's `StructuralCurveAction` places a line
load on a plate contour edge, states its direction in that edge's frame, and bounds it with two
stations. FEMEX 1.10's `LinearLoad` could name a **bar** and nothing else, so the adapter carried the
direction and the stations across and had nowhere to put the host. Two independent causes, both in
`SafImporter.Loads.cs`: an edge-hosted action left `bar` null, so `length` was `0.0` and the stations
were computed against it anyway — `0` to `1`, the full extent, which is no information; and
`CoordinateSystem` was assigned unconditionally, so `Local` landed on a load whose host FEMEX could
not name.

**The gap that allowed it is the more important half.** Phase B's round-trip test asserts
*equivalence modulo declared losses*, and these loads round-tripped perfectly: what went out came
back. An adapter can emit an internally inconsistent model and pass every check the suite had.

---

## Why not mint nodes

The instinct is to mint the two ends and split the member, which closes the gap in one move. The
repository had already answered it three times, against itself, in writing — `PointLoad`'s
doc-comment (decision **P2** of `SAF_Adapter.md`), the adapter's own refusal to mint for
`StructuralSurfaceActionFree` and `StructuralPointActionFree`, and `Node.LevelNumber` being a
required foreign key enforced as an error, so every minted node forces a level decision too. And it
breaks §7.2 equivalence, which would make A2's diff report phantom differences between two exports
of the same model.

---

## The change

### `LinearLoad.PlateId` and `LinearLoad.RegionId`

The shape the format already had, on `Support`:

```csharp
["Support.PlateId"]  = new Reference(RefTarget.Plate),
["Support.RegionId"] = new Reference(RefTarget.Region, scope: "PlateId"),
```

copied verbatim for `LinearLoad` in `Comparison/MemberComparer.cs`, so the diff resolves the two new
references the way it already resolves the support's.

**At most one host.** A load naming both a bar and a plate says two different things about what its
direction and its positions are measured against.

**The edge is named by `StartNode` and `EndNode`, and their order is not cosmetic** — exactly as
`Hinge.EdgeStartNodeId`/`EdgeEndNodeId` names a hinged edge. It is what local x runs along, so
writing the same edge the other way round reverses x and y.
`WritingTheSameEdgeBackwards_ReversesTheFrame` asserts it.

**The positions are measured along the host**: along the bar where there is a bar, and along the edge
segment where there is a plate.

### The frame, resolved through machinery that already existed

`TryGetHostAxes` gained one case:

```csharp
case LinearLoad line when line.PlateId.HasValue:
    return TryGetEdgeLocalAxes(line.PlateId.Value, line.StartNode, line.EndNode, out x, out y, out z);
```

`TryGetEdgeLocalAxes` is what `Hinge` already documents at length — local x along the edge, local z
the panel's normal, local y = ẑ × x̂ — and is the same frame SAF's own `RelConnectsSurfaceEdge` uses.
**No new geometry was written anywhere in this item.** A load on a panel's edge and a hinge on that
same edge are now the same claim about the same two nodes, resolved by the same call, which is what
stops the two conventions from coming to disagree.

### The rules

Reworded, because they now have two hosts to name:

- *"has a local direction but no barId"* becomes *"has a local direction but names neither a bar nor
  a plate"*.
- The position rule split in two on the wording alone, with the 0-to-1 range check shared rather than
  copied: a support and a hinge sit on a member and nothing else, so telling their author to name a
  plate would be advice they cannot take.

New, and each one mirrors the hinge's:

- both hosts stated at once;
- a `plateId` naming an unknown element, or an element that is not a plate;
- a `regionId` the named plate does not have, and a `regionId` with no `plateId`;
- the two nodes **not adjacent** in the named contour — the plate's, or the region's where one is
  named — reusing the same `AreAdjacent` the hinge rule calls.

### A second payoff on the export leg

`SafExporter.Loads.cs` used to **search every plate** for a contour containing the load's two nodes
and take the first match. Two plates sharing an edge have opposite normals, so the exported local
direction was decided by list order. With `PlateId` the exporter names the plate the load says it is
on. The search survives only as the fallback for a 1.10-or-earlier file, and now declares an
*Approximated* loss — `GuessedLinearLoadHost` — on the occasions it still has to guess.

---

## Two things the corpus made us decide

**`ClampedEdgeExtent`.** The house workbook's `LFS3` sits on an opening edge and states an absolute
extent of 0 to 2.5 m. Every edge of that opening is at most 2 m long, so measuring the station along
the one edge FEMEX names gives 1.25 — a position outside 0 to 1, which is an error, and which no
receiver has a rule for. The extent is cut at the edge's end and the cut is declared. Before 1.11
this load silently carried no extent at all, so the fact is new information rather than a new
problem.

**`CatalogueSectionShape`.** The new conformance check below went red against the SAF adapter on
Conformance1's `UPN200` and `CHS 168.3x6` — two sections with a catalogue block and no `properties`.
Exporting them writes a manufactured profile, which crosses as a **name** and not as a shape; the
model that came back had a section with neither dimensions nor stiffness, which `Validate()` calls an
error, and nothing said why. Now something does. This is the harness finding a real defect on its
first run, in a place unrelated to the item that added it.

---

## The rule that would have caught it

`Interop/Conformance/` gained a **Tier-1** check — *Imported validity*:

> No Error-severity finding on an imported model goes unnamed by a message.

It belongs beside the seven existing Tier-1 checks rather than in the SAF suite, because it is a
property of every adapter and because §7.3's design is that a later adapter inherits the rules and
cannot skip one by not writing its test.

**Why loss coverage could not see this.** That check compares a model against its round trip, so it
only ever sees *differences*. These loads produced none.

**What counts as named** is stated rather than left to the loop, because a `ValidationMessage` carries
no `ObjectRef` of its own — it is a sentence. A message names a finding when the finding's text
contains a token that message supplies: its native handle, or its subject written the way the
validator writes that entity (`"Section 26"`). Generous on purpose: the failure worth catching is
silence, and a check that failed on a message merely worded differently would be turned off. A
per-concept message supplies no token and does not count — *"Load"* appears in every message about
every load.

**Warnings do not fail it.** §2.4's obligation is about what did not cross, and an adapter reporting
an imperfect but usable model is doing its job. An *error* is the model saying there is nothing to
fall back on.

`InvalidatingAdapter` is the eighth deliberately broken adapter: it imports one line load with a local
direction and no host, and says nothing. It round-trips perfectly, loss coverage passes it, and the
new check catches it — which is the argument Phase A already made with seven broken adapters, made
once more for the check that would have caught this.

---

## Scope

**In, and closed:** partial-extent and locally-directed line loads on a **plate contour edge** —
`ExcelCurveForceAction.OnEdge`, `OnSubregionEdge` and `OnOpeningEdge` — which is the whole of the
eleven.

**Out, and still declared losses, unchanged:** `StructuralCurveActionFree`, a line load along a free
polyline; loads on `StructuralCurveEdge` internal edges; and the other free load families. Closing
those wants **coordinate-addressed loads** — a load bounded by absolute points rather than by nodes,
which is what SAF itself does, and which would close four Unmapped families at once without inventing
topology. That is a format expansion and it deserves its own document and its own bump.

---

## Fixtures

**`Examples/Example3.femex`** gains *Parapet*: a line load along the deck's east edge, in that edge's
own frame, over the middle half of it — the shape no 1.10 file could hold. Authored rather than
converted, so the new rules have a fixture that does not move when the adapter does.

**`Examples/Parity2.femex`** is Example3 with nine deliberately broken edge loads, one per new or
reworded message. It exists because `parity-check.ps1`'s guarantee is bounded by its corpus: a rule no
example triggers is unmirrored-and-undeclared without anything going red. The harness now reports six
of six, the sixth carrying nine messages that did not exist before this item — and changing one
character of one of them in the viewer turns it red on that message, which is the check the harness
was built for and had never been asked to make.

---

## Files

| | |
| --- | --- |
| `Loads/LinearLoad.cs` | `PlateId`, `RegionId`, and what their order means |
| `FemexModel.cs` | `CurrentSchemaVersion` to `1.11`, `1.10` readable, the ledger clause |
| `FemexModel.Validation.cs` | `ValidateLinearLoadHost`, `ValidateLoadPosition`, the 1.10 version note, `1.10` in `SelfWeightVersions` |
| `FemexModel.LocalAxes.cs` | one case in `TryGetHostAxes` |
| `Comparison/MemberComparer.cs` | two reference-table entries |
| `Interop/Conformance/ConformanceHarness.cs` | the *Imported validity* check |
| `SafImporter.Loads.cs`, `SafImporter.Restraints.cs`, `SafPosition.cs` | `EdgeOf`, the edge length, the host, the clamp |
| `SafExporter.Loads.cs`, `SafExporter.cs` | `Named`, the declared guess, the catalogue-section loss |
| `SafLoss.cs`, `SafMessages.cs` | three new catalogue entries |
| `femex-viewer.html` | the mirror, `loadHostAxes`, `PROPS`, `HIGHLIGHT`, `CURRENT_SCHEMA_VERSION` |
| `Examples/Example3.femex`, `Examples/Parity2.femex` | the two fixtures |
