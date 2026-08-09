# Plate Redefinition — Implementation Summary

Implemented `Claude/FEMEX_Plates.md` in full. Clean build (0 warnings, 0 errors);
35 tests pass (was 3).

`Plate` is no longer a flat N-gon with a scalar thickness. It is an **authored
design panel**: an outer contour plus any number of subregions that override
thickness/material or punch holes. The finite elements now live in a separate,
optional `Mesh` block that back-links to the panel and region each face came from.
The model follows RAM Concept's overlapping-slab-areas-with-integer-priority
scheme, with openings as an explicit kind rather than a zero thickness.

## New files

| File | What |
| --- | --- |
| `Geometry/Surfaces/SurfaceProperty.cs` | Abstract, polymorphic (`"constant"`), `Id`/`Name`/`GetNominalThickness()`. The plate counterpart of `Section`. `"variable"` and `"layered"` are reserved in the doc comment only. |
| `Geometry/Surfaces/ConstantThickness.cs` | `double Thickness`. |
| `Geometry/PlateRegion.cs` | `Id` (plate-scoped), `Name`, `NodeIds`, `Kind`, `SurfacePropertyId?`, `MaterialId?` (null = inherit), `Priority`, `Alignment?`, `SurfaceOffset?`. Carries the resolution rule in its XML doc. |
| `Geometry/PlateRegionKind.cs` | `Structural \| Opening \| LoadOnly`. |
| `Geometry/PlateBehaviour.cs` | `Shell \| Plate \| Membrane \| CompressionOnly`. |
| `Geometry/SurfaceAlignment.cs` | `Bottom \| Centre \| Top`, normal-relative for walls. |
| `Mesh/FemexMesh.cs` | `Generator`, `GeneratedAt` (free text), `Nodes`, `Faces`. |
| `Mesh/MeshNode.cs` | Own id space; absolute `X/Y/Z`; `SourceNodeId?` back to an authored node. |
| `Mesh/MeshFace.cs` | Shares the element-id space; `NodeIds` (3 or 4), `PlateId`, `RegionId?`, plus the resolved `SurfacePropertyId?`/`MaterialId?`/`Thickness?`/`SurfaceOffset` cache. |
| `FemexModel.Validation.cs` | `Validate()`, moved out of `FemexModel.cs` and split into helpers. |
| `griffel-femex.Tests/SampleModels.cs` | The shared `Build()` factory. |
| `griffel-femex.Tests/PlateTests.cs`, `ValidationTests.cs` | Split out of `RoundTripTests.cs`. |

## Modified

- **`Geometry/Plate.cs`** — rewritten: `Name`, `NodeIds` (outer contour), `Kind`,
  `SurfacePropertyId?`, `MaterialId?`, `Behaviour`, `Alignment`, `SurfaceOffset`,
  `LocalAxisAngle`, `Regions`. `Thickness` is gone. `GetNodeIds()` now yields the
  outer contour followed by every region contour. The `< 3 nodes` constructor throw
  is gone — it is a validation message now.
- **`Geometry/Element.cs`** — `MaterialId` removed; **`Geometry/Bar.cs`** gains it.
- **`Loads/AreaLoad.cs`** — `PlateId?`, `RegionId?`; `NodeSequence` is now
  `List<int>?`. Exactly one targeting form per load.
- **`BoundaryConditions/Support.cs`** — `PlateId?`, `RegionId?` for area supports
  that should follow a panel. The node-list form stays (the example needs it).
- **`BoundaryConditions/Hinge.cs`** — `RegionId?`, `EdgeStartNodeId?`,
  `EdgeEndNodeId?`. `EndOrEdgeIndex` is now documented as bar-only: a plate edge is
  named by its two nodes, which survives inserting a vertex into the contour.
- **`FemexModel.cs`** — now `partial`; adds `SurfaceProperties` and `Mesh?`
  (declared last, so `"mesh"` is the last key and is omitted entirely when null).

## The resolution rule

Regions may overlap each other and may hang over the outer contour (the overhang is
clipped). At every point inside the outer contour:

1. highest `Priority` wins — the base panel behaves as `int.MinValue`;
2. on a tie, `Opening` > `LoadOnly` > `Structural`;
3. on a further tie, the region later in `Plate.Regions` wins.

Mesh back-links are authoritative; the resolved property fields on a `MeshFace` are
a cache the mesher writes. `Validate()` checks those ids exist but does not re-run
resolution — FEMEX has no mesher.

## Validation

> **Extended by `FEMEX_Node_Sharing_Summary.md`:** messages now carry a
> `ValidationSeverity`, and a fourteenth group warns about two nodes at one
> location. Everything below is an `Error` and is worded exactly as it was.

`Validate()` keeps its contract (deferred, non-throwing, one message per problem)
and grew from 6 checks to 13 groups: duplicate ids across every entity; element-id
collisions across bars/plates/mesh faces; contour node existence, minimum count and
repeats; surface-property and material references; kind rules (`Structural` needs
both, `Opening` may carry neither, `LoadOnly` unchecked); region-id uniqueness
within a plate; mesh integrity including faces on openings; area-load, support and
hinge targeting; and two geometric checks — contour coplanarity via Newell's method
with a size-scaled tolerance, and a bounding-box heuristic for two same-kind
same-priority regions that overlap.

## `Examples/Example1.femex`

Hand-migrated with a throwaway program (not committed). Semantics are unchanged;
the file is 49 lines shorter and far less repetitive.

| Before | After |
| --- | --- |
| 44 slab quads, 11 per level, core cell omitted by skipping an id | 4 slab panels (3001–3004) each with one `Opening` region for the core shaft |
| 16 wall quads | 16 wall panels, **ids and contours unchanged** |
| thickness repeated on 60 plates | 2 `SurfaceProperty` entries (`SLAB-220`, `WALL-300`) |
| — | `mesh` block: 80 nodes, 44 faces **keeping the original ids 3100–3411** |
| 88 area loads, one per quad | 8 area loads, one per panel per case |
| temperature load on `[3400…3411]` | unchanged — those are mesh face ids now |
| support 2 (`Area`, nodes 7,8,13,12) | unchanged |
| 136 bars, 20 hinges | unchanged |

Two things the migration confirmed. The old 88 loads covered only the 11 quads per
level, i.e. they already excluded the core — a single whole-panel load on a plate
with an `Opening` region reproduces that exactly, which is independent evidence that
"openings carry no surface load" is the right rule. And putting mesh faces in the
element-id space let the roof temperature load survive byte-identical.

## Deviations from the plan

1. **Tests were split three ways** (`RoundTripTests` / `PlateTests` /
   `ValidationTests`) sharing `SampleModels.Build()`. The plan flagged this as a
   convention change rather than assuming it; ~15 new facts in one file would have
   pushed it past 500 lines.
2. **The sample model's node layout was rebuilt.** The old one had the plate contour
   spanning two levels with one node offset 0.2 — genuinely non-planar, so it would
   have tripped the new coplanarity check. Nodes are now laid out as a column, a
   10×10 slab at first floor, a drop panel, a void, and a wall in the y = 0 plane.
3. **The example keeps unmeshed wall panels.** Only the slabs have mesh faces, since
   only the slabs had a mesh worth preserving. Nothing requires a plate to be meshed.

## Still open

- No mesher. The `Mesh` block is data-only.
- `Orthotropic` behaviour, variable and layered thickness: reserved, not implemented.
- The `bars`/`plates` discriminator wart stays — `Plate` is ceasing to be an element,
  so a base-typed `List<Element>` is a worse bet than it was.
- `AreaLoad` still does not state its direction or whether it is projected. A
  `LoadOnly` region exists to carry a pressure, so this is now more visible than it
  was; worth its own change.
- The format still has no version field, so an old file deserializes quietly with
  `thickness` dropped. Validation catches it ("is Structural but has no surface
  property"), but a `schemaVersion` would catch it sooner.
