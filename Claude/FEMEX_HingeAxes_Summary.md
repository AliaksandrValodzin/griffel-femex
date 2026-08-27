# The axes a hinge's releases are in — Implementation Summary

Documentation and two helpers, **no schema change**: `CurrentSchemaVersion` stays `"1.10"` and every
existing file is byte-identical after a re-save. Clean build (0 warnings, 0 errors, both legs);
**477 tests pass** (was 462), of which 15 are new. The viewer mirrors it and the parity harness still
reports five of five examples in step.

This is the shape `FEMEX_SAF_Fit.md` §4 item 7 closed at 1.8 — the bedding-modulus semantics — for
the same reason: the format was **silent about the frame a number is measured in**, and silence is
not a default. It is now §4 item **9**, and it is the only item on that list that was found by being
asked the question rather than by reading SAF against FEMEX.

---

## The finding

`Hinge` carries `Release Ux, Uy, Uz, Rx, Ry, Rz` and no coordinate system. No doc comment and no
document in `Claude/` said which axes those are.

**The intent was never in doubt.** Every program FEMEX targets means the member frame for a member
end release — SAF's `RelConnectsStructuralMember`, RFEM's `memberHinge`, ETABS' `SetReleases`,
Robot's `I_LT_BAR_RELEASE` — so the rule states an existing agreement rather than choosing among
candidates. And one piece of FEMEX already relied on it: `ValidateBarCompleteness` rejects a
tension-only bar whose hinge releases `Ux`, on the grounds that the member "carries axial force and
nothing else", which is true of the **axial** DOF and of no other. In global axes `ux` is axial only
for a bar that happens to run along global X.

**One validation rule inferring a convention is not a convention.** That is item 7 again: two
adapters read the same file, one takes `rz` as global, and the model opens, validates against
`Validate()`, solves, and is wrong — with neither adapter wrong against a spec that did not exist.

**And the plate-edge half had no default to fall back on at all.** A slab-edge hinge could plausibly
have meant the panel's local axes (`TryGetPlateLocalAxes`) or an edge-aligned frame, and the two
differ for every edge not parallel to the panel's local x. `FemexModel.LocalAxes.cs` had no edge
frame to point at either way.

---

## The rule

Stated on `Hinge`, executable in `FemexModel.LocalAxes.cs`.

| The hinge sits on | The frame its six releases are in |
| --- | --- |
| a **bar** | that bar's own local axes, roll included — so `ux` is axial, `rx` torsion, `rz` the major-axis release that pins a beam end |
| a **plate edge** | the **edge's** frame: x along the edge, z the panel's normal, y = ẑ × x̂ |
| a **mesh face** | the same edge rule over the face's own nodes and normal, for the edge `EndOrEdgeIndex` names |

The edge frame in full:

- local **x** = the chord `EdgeStartNodeId` → `EdgeEndNodeId`, with its out-of-plane part removed.
  Where the hinge names no edge, `EndOrEdgeIndex` picks edge *i* → *i*+1 out of the contour it
  belongs to — the region's when it names one, the plate's otherwise.
- local **z** = the *panel's* normal, exactly as `TryGetPlateLocalAxes` gives it.
- local **y** = ẑ × x̂, which for an edge taken in its contour's own order points **into** the panel.

### Three choices inside that, each deliberate

**The edge's frame and not the panel's.** SAF makes the same choice — `RelConnectsSurfaceEdge` is in
the edge LCS — and it is what makes the common statement the simple one: "this edge is hinged about
itself" is `rx`, on every edge of every panel, whichever way the panel is set out. The panel's
`LocalAxisAngle` therefore **does not reach an edge**, and cannot: it turns the panel's x and y about
the normal, and an edge takes its x from the edge.

**A region does not change z.** The normal is the panel's whichever contour the edge belongs to. A
region contour has no orientation rule of its own, so an opening is routinely wound against its
panel; had each contour's winding decided, a hinge on a void's edge and one on the outer contour
would disagree about which side is up. `HingeAxesTests` reverses the stair void's contour and asserts
z stays the slab's +Z.

**`EdgeStartNodeId`/`EdgeEndNodeId` order is not cosmetic.** It is what x runs along, so naming the
same edge backwards reverses x and y and turns a release stated in y into its own opposite.
Validation accepts either order — an edge is adjacent in a contour whichever way it is named — which
makes this the one thing the pair says that the adjacency check does not, and it is now stated on the
two properties.

### What this is not

It is **not** `FEMEX_Interop_Review.md` §5.6, *"Supports have no local axes"*. That gap is a
`Support` wanting a frame it can **choose** — SAF, RFEM, Revit and ETABS all offer one, and an
inclined bearing or a raking prop is unrepresentable without it. A hinge needs no flag: its frame is
a function of what it sits on, which is the reading every target program already takes. Closing §5.6
leaves this rule untouched, and this change deliberately adds no coordinate-system property that a
future §5.6 fix would then have to reconcile.

---

## Modified

- **`BoundaryConditions/Hinge.cs`** — the convention, as the bulk of the class doc, plus the
  order-matters note on the edge-node pair and a pointer on the six release properties. No members
  added.
- **`BoundaryConditions/Release.cs`** — which DOF and in which axes is `Hinge`'s to say; the number
  lives here and its frame is set there, the split `Restraint` and `Support` already make about a
  stiffness. So a partial release is a spring about a **local** axis.
- **`Geometry/Bar.cs`** — the sentence listing what the member frame is the frame *for* (alignment,
  eccentricity, thermal gradients, all 1.10) gains hinge releases.
- **`Geometry/Plate.cs`** — a hinge on an edge of this panel is in the edge's frame, not this one, so
  `LocalAxisAngle` does not reach it; the one thing the panel lends an edge is its normal.
- **`FemexModel.LocalAxes.cs`** — `TryGetEdgeLocalAxes(plateId, startNodeId, endNodeId, out x, y, z)`
  and `TryGetHingeLocalAxes(hinge, out x, y, z)`, plus three privates: `TryGetEdgeAxes` (the rule
  itself, given a normal and two ends), `TryGetEdgeEnds` (which two contour positions an edge index
  names — no answer rather than a silent clamp, matching what `Validate()` does with an index it
  rejects) and `TryGetMeshPoints`. Both public helpers are lookups like every other member of the
  file: a degenerate geometry is answered with `false`, never an exception.
- **`griffel-femex.Tests/SampleModels.cs`** — a **third hinge**: a vertical movement joint on the
  wall's far edge, 42 → 12, releasing `ux`. It is the case the edge frame exists for — the edge runs
  straight up, so its x is global +Z, its z is the wall's normal (−Y) and its y is −X — and "the wall
  slips vertically at this joint" is a sentence no global frame can write for a wall of arbitrary
  orientation. Its `EndOrEdgeIndex` names the same edge the node pair does, so the fixture exercises
  the fallback agreeing with the named form. The two existing hinges gain comments saying what their
  releases mean in the frame that is now stated.

## New

| File | What |
| --- | --- |
| `griffel-femex.Tests/HingeAxesTests.cs` | 15 facts — the tests the convention did not have |

The suite covers: the slab edge (x along it, z the panel normal, y into the panel); the angle **not**
reaching it; naming the edge backwards reversing x and y; a wall edge, where no local axis is a global
one; a bar hinge equalling `TryGetBarLocalAxes` with the sample's 30° roll applied; the unnamed-edge
fallback landing on the same edge as the named form; a region wound against its panel keeping the
panel's z; a mesh-face hinge; and the four cases that answer `false` — unknown plate, unknown node,
an edge of no length, an index outside the contour. One sweep asserts every hinge in the sample
resolves to a right-handed unit triad.

## Viewer

`femex-viewer.html` mirrors the two helpers by hand, as it mirrors the rest of
`FemexModel.LocalAxes.cs` — `edgeAxes`, `edgeLocalAxes`, `hingeLocalAxes`, `edgeEnds` — and shows the
frame two ways: a **`Hinge axes` toggle** (`R`), off by default and drawn only when `Hinges` is on,
which puts the triad on the hinge glyph itself; and three rows in the hinge properties panel, which
name the frame in words and give the three axes as vectors. See `FEMEXViewer.md`, *As built
(2026-08-27)*.

Off by default is the point of the toggle: a model with a hinge at every member end would drown in
triads, and the existing `Local axes` toggle answers a different question — "which way is this
element turned", against this one's "what does `rx` on this hinge mean".

## Verification

- `dotnet test` — 477 pass, 0 fail, 0 warnings.
- `parity-check.ps1` — five of five examples agree; the viewer's validator mirror is untouched by
  this change and still in step.
- A headless probe of the viewer's `hingeLocalAxes` over `Parity1.femex` returns, for the bar hinge
  on the 30°-rolled column, x = (0, 0, 1) and y = (0.866025, 0.5, 0), and for the slab-edge hinge
  x = (1, 0, 0), y = (0, 1, 0), z = (0, 0, 1) — the same numbers `HingeAxesTests` asserts of the C#.
