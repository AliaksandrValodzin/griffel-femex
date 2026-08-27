# FEMEX against SAF — can the format hold the model?

*Measured against **SAF 2.2.0**, published 28 November 2022 and still the current stable release, and
against `griffel-femex` at schema **1.6**.*

> **Scope.** Not a new assessment of FEMEX, and not a replacement for `FEMEX_Interop_Review.md`. That
> document measured FEMEX against five programs with SAF as the benchmark, and its priorities were set
> for that five-program world. This one asks a narrower question with a sharper edge, because
> `AdaptersPlans/SAF_Adapter.md` commits to building a SAF adapter and nothing else: **object by
> object, can the FEMEX schema hold a SAF model, and where does it produce a model that opens, solves,
> and is wrong?**
>
> No `.cs` file changes follow from this document, and no schema bump. It names and ranks the changes
> it implies; making them is a separate decision, and `SAF_Adapter.md` B5 is explicit that a mapping
> document is properly written *after* a real file has been read.

## Context

Three things make this worth writing before Phase B rather than after it.

**The product framing changed what a loss costs.** `FEMEX_BusinessModel.md` §2–3 makes the assurance
report the thing that is sold, and §4 defines what it is for: models that *"open cleanly, solve, and
are wrong"*. An adapter that manufactures exactly that failure class is off-brand in a way a merely
lossy one is not. So the interesting question is no longer *how much* FEMEX loses — the `LossCategory`
taxonomy exists to make loss survivable — but how much it loses **quietly**.

**The existing gap work does not answer this, in four specific ways.**

- `FEMEX_Interop_Review.md` §6, *deliberately out of scope*, was drawn to bound a five-program
  essential subset. Several of its items — curved geometry, tapered members — are things SAF carries
  as first-class objects. Now that `FEMEX_BusinessModel.md` §7 makes SAF *"the only adapter built on
  spec"*, they stop being hypothetical. §6 below re-opens them.
- The §2.2 priority column is wrong in both directions against SAF specifically.
  `StructuralLoadGroup` sits at **P2** and is a *mandatory* reference on every SAF load case. Support
  local axes sit at **P1**, and SAF's point supports in a node are effectively global-only, so the
  item is narrower here than the review implies.
- `FEMEX_Interop_Status_16082026.md` records the six P0 items closed and the nine P1 items untouched,
  but §7.1's headline — *"roughly 70% of the way to the intersection these vendors have already
  agreed on in SAF"* — has never been restated since 1.6 landed.
- **No FEMEX document has looked at SAF's required-versus-optional split.** This is the finding that
  reframes the rest. FEMEX's gaps are not confined to SAF's optional columns. There are columns SAF
  marks *mandatory* that FEMEX cannot supply at all, which means an exporter cannot write a workbook
  SAF's own validator will accept without inventing values. §3 is that list.

**And SAF has moved since the review.** 2.1 and 2.2 added objects that change the picture:
`ResultInternalForce1D` and `ResultInternalForce2DEdge`, `StructuralPointSupportDeformation`, point
supports positioned along a beam, `Nonlinear` combinations, and internal edges as a first-class
reference target for both edge connections and curve actions.

---

## 0. Verdict

**The container holds. The vocabulary holds a storey-framed building of straight members and flat
polygonal panels — which is most of SAF's own published example corpus — and fails in an enumerable
set of places, of which five sit on SAF's mandatory column list rather than its optional one.**

Said less kindly: FEMEX can read a SAF file today and produce something useful. It cannot write one
that validates, because `StructuralMaterial.Type`, `StructuralMaterial.Quality`,
`StructuralLoadCase.Load group`, `Model.System of units` and `Model.National code` are all required
and none has a FEMEX home. That is a different and more decisive kind of gap than the P1 list, and it
has not been stated in this repository before.

Underneath that, eight concepts cross **silently wrong** rather than merely incomplete (§4). One class
of loss — curved geometry — is the only one in this document that is **non-reversible on every round
trip**, which matters because `SAF_Adapter.md` rests part of its argument on *"every conversion is a
round-trip test"*.

Against which: the container really is right, and in three places FEMEX is ahead of the benchmark
rather than behind it (§5). One of them, `SectionProperties`, is a genuine superset — worth saying,
because every other document in this set records sections as the place FEMEX is weakest.

---

## 1. Three structural differences that decide everything downstream

Not features. Shape. Most of §2 follows from these.

### 1.1 A SAF node has a Z; a FEMEX node has a landlord

`StructuralPointConnection` is `Name`, `Coordinate X`, `Coordinate Y`, `Coordinate Z`, `Id`. That is
the whole class — no storey reference, no restraint, no layer, no local frame. And **SAF has no
member-to-storey assignment anywhere in the schema**: `StructuralStorey` is `Name`, `Height level [m]`,
`Id`, a bare level marker that nothing points at.

`Geometry/Node.cs` has no `Z` at all. Elevation is `Level.AbsoluteElevation + VerticalOffset`, and
`LevelNumber` is a required foreign key enforced as an **error**, not a warning
(`FemexModel.Validation.cs:415` — *"Node {n} references unknown level {l}."*).

The asymmetry therefore runs both ways and is unavoidable:

- **Import.** Every SAF file arrives as a bag of free coordinates. Levels must be synthesised before a
  single node can be written, and a model with no storey meaning at all — a truss, a ramp, a transfer
  structure — acquires levels that were never in the source. `FEMEX_Adapters.md` §6.1 already settles
  the policy (*"snap an incoming elevation to an existing `Level` within tolerance; otherwise create
  one. Always emit an *Invented* message for a level the native model did not have"*) and §6.2 the
  ordering (two-phase: collect every candidate, cluster once against the finished extent, then
  create). Neither the tolerance nor the clustering helper exists — §6.1 says so, and §7.6 lists the
  helper as unwritten.
- **Export.** Every FEMEX model writes a `StructuralStorey` sheet the receiving program has nothing to
  do with, because nothing in SAF references a storey.

This is the highest-traffic *Invented* the adapter will produce — on every file, for every distinct
elevation — and it deserves saying plainly rather than sitting in the contract's small print. Review
§3.7 defends the level-based node as *"a feature, not a bug, for the target domain"* and §7.3 leaves
open whether it survives contact with a real non-storey model. SAF is where that question gets
answered, and the answer is that it survives at the price of a permanent invention on every import.

### 1.2 SAF geometry is typed segments; FEMEX geometry is chords

SAF states geometry as an ordered `Nodes` list plus a parallel `Segments` (1D) or `Edges` (2D) keyword
list, one keyword per span:

| Keyword | Nodes consumed | |
|---|---|---|
| `Line` | 2 | |
| `Circular Arc` | 3 | start, intermediate, end |
| `Parabolic arc` | 3 | |
| `Bezier` | 4 | cubic; two control-polygon vertices |
| `Spline-n` | n | |
| `Circle and Point` | 2 | surfaces only |
| `Circle by 3 points` | 3 | surfaces only |
| `Polyline` | — | derived when a member has more than one segment |

`Bar` is straight between two nodes and says so. Plate contours are node lists with straight segments,
*"curves as chords"*. A `StructuralSurfaceMember` with `Shape = Curved` fails
`ValidateContourPlanarity` outright.

The part that is easy to miss: **SAF has a mechanism for surviving this, and FEMEX declined it.**
`Parent ID` exists precisely so that an application which cannot handle curves may segment one into
straight pieces while the original curve keeps its identity, to be reconstituted on the return trip.
`FEMEX_Identity.md` left it out on the reasoning that *"its documented use is reconstituting a curved
member a receiver segmented, and FEMEX's curves-as-chords decision leaves nothing to reconstitute"*.
That is true inside FEMEX and false at the SAF boundary, where the curve does exist — in the file
being read, one layer up.

The consequence is the only irreversible loss in this document. Everything else degrades. A curve
chorded on import and written back out as a polyline is a *different SAF file*, and no amount of loss
reporting puts the arc back.

### 1.3 SAF is Name-keyed; FEMEX names are optional

Every cross-sheet reference in SAF is a `Name` string — `StructuralCurveMember.Cross section = "CS1"`,
`Nodes = "N2; N3; N4"`. `Name` is required and unique per sheet; `Id` and `Parent ID` are advisory
UUIDs whose documented purpose is round-tripping. `FEMEX_Identity.md` copied that three-layer answer
deliberately, and `SAF_Adapter.md` B4's uid ↔ SAF-name mapping works.

Two asymmetries are worth recording rather than assuming away:

- FEMEX names are `string?` throughout, with a blank or duplicate reported as a **warning**
  (`ValidateNameKeys`) — the deliberate half-step described in `FEMEX.md`'s identity blockquote. SAF
  treats a duplicate name within a sheet as fatal.
- **`Bar`, `Node`, `Support` and `Hinge` carry no name property at all.** Their SAF counterparts —
  `StructuralCurveMember`, `StructuralPointConnection`, `StructuralPointSupport`,
  `RelConnectsStructuralMember` — all require one. A name is therefore synthesised on export for four
  of the largest sheets in the file, in `FEMEX_Adapters.md` §5.4's `{Kind}-{8 hex}` form. A round trip
  through FEMEX renames most of the model. That is legal and visible, and worth telling a user once
  rather than per object.

---

## 2. Sheet by sheet

**Legend:** ● direct match · ◐ present but lossy · ○ no FEMEX home. The `LossCategory` column is the
value an adapter must emit per `FEMEX_Adapters.md` §4, on the leg named. Organised by the SAF
specification's own chapters so a Phase B implementer can walk the workbook in order.

### 2.1 General

| SAF | FEMEX | | Loss |
|---|---|---|---|
| `Project` (all optional) | `FileMetadata.ProjectName` | ◐ | 10 of 11 columns *Dropped* on export; *Unmapped* per concept on import |
| `Model.SAF Version` | — | ○ | `SchemaVersion` is a statement about FEMEX, not about SAF; *Invented* on export |
| `Model.Global coordinate system` | fixed Z-up + `Gravity` | ● | FEMEX is Z-up by definition (`Gravity.cs`, `Vector3d.cs`); a non-Z-vertical SAF file is normalised at the boundary per §6.3 |
| `Model.LCS of cross-section` (8 values) | fixed convention | ◐ | derivable but must be asserted; *Invented* on export |
| `Model.System of units` (Metric/Imperial) | `Units` free text | ○ | **mandatory** — see §3 |
| `Model.National code` (40+ NAs) | — | ○ | **mandatory** — see §3 |
| `Model.Ignored objects` / `Ignored groups` | — | ○ | *Unmapped*; an update-semantics feature FEMEX has no equivalent for |

### 2.2 Structural elements

| SAF | FEMEX | | Loss |
|---|---|---|---|
| `StructuralPointConnection` | `Node` | ◐ | Z→(level, offset): *Invented* level on import (§1.1) |
| `StructuralStorey` | `Level` | ● | name + elevation map; `IsGround`, `RelativeElevation`, `GridIds` *Dropped* on export |
| `StructuralCurveMember` — `Nodes`/`Segments` = `Line` | `Bar` | ● | |
| `StructuralCurveMember` — any other segment type | chorded `Bar` chain | ◐ | *Approximated*, and **irreversible** (§1.2) |
| `StructuralCurveMember.Type` (17 values) | — | ○ | *Dropped*; annotation, no analysis meaning |
| `StructuralCurveMember.System line` (9) + `Structural`/`Analysis` eccentricities | — | ○ | *Dropped* — **silently wrong**, see §4 |
| `StructuralCurveMember.Behaviour in analysis` (4) | — | ○ | *Dropped* — **silently wrong**, see §4 |
| `StructuralCurveMember.LCS` + `LCS Rotation` | `Bar.RotationAngle` | ◐ | FEMEX's default frame is the ETABS/SAP rule (`TryGetBarLocalAxes`); SAF's `y by vector`/`z by point` forms must be resolved to a roll angle, which is exact only when the vector lies in the plane FEMEX's rule produces |
| `StructuralCurveMember.Layer`, `Color` | — | ○ | *Dropped*; annotation |
| `StructuralCurveMemberVarying` | — | ○ | *Approximated* to the first section — **silently wrong**, see §4 and §6 |
| `StructuralCurveMemberRib` | unrelated `Bar` + `Plate` | ○ | *Approximated*; composite action, effective width and shear connection all *Dropped*. See §6 |
| `StructuralCurveEdge` (internal edge) | — | ○ | *Unmapped*; and it is a reference target for edge connections and curve actions, so what points at it is lost too |
| `StructuralSurfaceMember` (flat, `Line` edges) | `Plate` | ● | |
| `StructuralSurfaceMember.Shape = Curved` | — | ○ | rejected by `ValidateContourPlanarity` |
| `StructuralSurfaceMember.Thickness type` (8) | `ConstantThickness` | ◐ | 7 of 8 *Approximated* to a nominal thickness; `"variable"` is reserved and unimplemented |
| `StructuralSurfaceMember.Behavior in analysis` | `PlateBehaviour` | ◐ | enums disagree in both directions — see below |
| `StructuralSurfaceMember.System plane at` + `Analysis Z Eccentricity` | `SurfaceAlignment` + `SurfaceOffset` | ● | the one place the eccentricity story lands cleanly |
| `StructuralSurfaceMemberOpening` | `PlateRegion` (`Opening`) | ● | |
| `StructuralSurfaceMemberRegion` | `PlateRegion` | ● | and FEMEX adds `Priority`, which SAF has no answer to (§5) |
| `StructuralProxyElement` ×3 sheets | — | ○ | *Unmapped* per concept, not per object (§4.4 of the adapters contract) |

**The behaviour enums do not line up.** SAF has `Isotropic, Orthotropic, Membrane, Press only`;
FEMEX has `Shell, Plate, Membrane, CompressionOnly`. `Membrane` and `Press only`/`CompressionOnly`
match. SAF's `Orthotropic` has no FEMEX value — `FEMEX_Plates.md` rejected it deliberately, arguing
directionality belongs on the surface property, and review §5.7 agrees while noting *"the surface
property side of that decision was never built"*. FEMEX's `Plate` (bending only) has no SAF value.
So one value is lost in each direction, and SAF carries no orthotropy parameters anyway, so even a
future orthotropic `SurfaceProperty` would arrive empty.

### 2.3 Cross-sections and materials

| SAF | FEMEX | | Loss |
|---|---|---|---|
| `StructuralCrossSection` type `Parametric` (33 shapes) | 8 parametric shapes | ◐ | the 8 land exactly; the other 25 fall back to `generic` + `Properties` — *Approximated* |
| `StructuralCrossSection` type `Manufactured` | `SectionCatalogue` | ◐ | `Profile` ● ; `Form code` **mandatory** and unrepresentable for form codes 9–23 (§7); `Description ID` is an enum against free text |
| `StructuralCrossSection` type `Compound` (12) | — | ○ | `compound` reserved and unimplemented; *Approximated* to `generic` |
| `StructuralCrossSection` type `General` → `CompositeShapeDef` | `GenericSection` | ◐ | stiffness survives; the y;z polygon, its up-to-99 contours and their per-contour materials are *Dropped* |
| `A, Iy, Iz, It, Iw, Wply, Wplz` (all optional) | `SectionProperties` | ● | **superset** — see §5 |
| `StructuralMaterial.Type` (M) | — | ○ | **mandatory** — §3 |
| `StructuralMaterial.Quality` (M) | `Name`, informally | ○ | **mandatory** — §3 |
| `StructuralMaterial.Subtype` | — | ○ | *Dropped* |
| `Unit mass`, `E modulus`, `Poisson Coefficient` | `Density`, `ModulusOfElasticity`, `PoissonsRatio` | ● | mass density on both sides, so no factor-of-g trap here |
| `G modulus` (stated) | `GetShearModulus()` (derived) | ◐ | **silently wrong** where the two differ — §4 |
| `Thermal expansion [1/K]` | — | ○ | *Dropped*, and it breaks FEMEX's own `TemperatureLoad` — §4 |
| `Design properties` (22 labelled values) | `Strength` (one scalar) | ◐ | *Dropped* for all but one; see §7 |

### 2.4 Supports, hinges and links

| SAF | FEMEX | | Loss |
|---|---|---|---|
| `StructuralPointSupport`, `Boundary condition = In node` | `Support` (`Point`) | ◐ | the 6-DOF shape matches; the value set does not — §4 |
| `StructuralPointSupport`, `Boundary condition = On beam` (2.2) | — | ○ | position along a member has no FEMEX home — §7 |
| `StructuralCurveConnection` (line support on member/rib) | `Support` (`Linear`) | ◐ | FEMEX names nodes, not a member; `Start point`/`End point`, `Origin`, `Coordinate definition` and `Coordinate system` all *Dropped* |
| `StructuralEdgeConnection` (line support on 2D edge, 4 boundary conditions) | `Support` (`Linear`) | ◐ | same; and `On internal edge` has nothing to point at |
| `StructuralSurfaceConnection` (subsoil, `C1x/y/z` + `C2x/y`) | `Support` (`Area`) + `PlateId` | ◐ | C2 (Pasternak) *Dropped* entirely; C1 lands in a stiffness whose units are undefined — §4 |
| `RelConnectsStructuralMember` (member end hinge) | `Hinge` | ● | `Position = Both` becomes two hinges; otherwise exact |
| `RelConnectsSurfaceEdge` (line hinge on 2D edge) | `Hinge` (`Linear`) | ◐ | FEMEX names the edge by two nodes, which is more robust (§5); the partial-length `Start point`/`End point` is *Dropped* |
| `RelConnectsRigidLink` (master/slave nodes) | — | ○ | *Unmapped*. Review §5.1 |
| `RelConnectsRigidCross` (crossing members) | — | ○ | *Unmapped* |
| `RelConnectsRigidMember` (node/edge/member coupling) | — | ○ | *Unmapped* |

### 2.5 Loads

| SAF | FEMEX | | Loss |
|---|---|---|---|
| `StructuralLoadGroup` | — | ○ | **mandatory reference** — §3 |
| `StructuralLoadCase` | `LoadCase` | ◐ | `Action type` and `Load type` fold into `LoadNature`; `Duration` and `Load group` *Dropped* |
| `Load type = Self weight` | `LoadCase.SelfWeightFactor` + `Gravity` | ● | see the note below |
| `StructuralLoadCombination` | `LoadCombination` | ◐ | `Load factor` × `Multiplier` → one `Factor` ● ; `Category` → `LimitState` ● ; `National standard` *Dropped*; `AbsoluteAdd` and `Srss` have no SAF value on export |
| `StructuralPointAction`, `In node` | `PointLoad` | ◐ | `Coordinate system = Local` and `Direction = Vector` *Dropped* — `PointLoad` does not derive from `DistributedLoad` |
| `StructuralPointAction`, `On beam` (+ `Repeat`/`Delta x`) | — | ○ | §7 |
| `StructuralPointMoment` | `PointLoad.Mx/My/Mz` | ◐ | same two limitations; `On beam` has no home |
| `StructuralCurveAction`, `On beam`, full length | `LinearLoad` | ● | `Uniform`/`Trapez` → `MagnitudeStart`/`End` ● ; `Location` → `Projected` ● ; `Coordinate system` and `Direction` ● |
| `StructuralCurveAction`, partial (`Start point`/`End point`, `Extent`) | — | ○ | §7 |
| `StructuralCurveAction`, `On edge` / `subregion` / `opening` / `internal edge` | — | ○ | §7 |
| `StructuralCurveAction.Eccentricity ey/ez` | — | ○ | *Dropped* |
| `StructuralCurveMoment` | `LinearLoad.MomentStart`/`End` | ◐ | same positional limitations |
| `StructuralSurfaceAction` (uniform) | `AreaLoad` | ● | `On 2D member` and `On 2D member region` both land |
| `StructuralSurfaceActionDistribution` | `PlateRegionKind.LoadOnly` | ◐ | the panel maps; `Distribution to` does not — **silently wrong**, §4 |
| `StructuralCurveActionThermal` | `TemperatureLoad` | ◐ | `Variation = Constant` ● ; `Linear` collapses four fibre temperatures to one unsigned gradient — §4 |
| `StructuralSurfaceActionThermal` | `TemperatureLoad` | ◐ | `TempT`/`TempB` collapse likewise |
| `StructuralPointActionFree` | — | ○ | *Unmapped* |
| `StructuralCurveActionFree` | — | ○ | *Unmapped* |
| `StructuralSurfaceActionFree` | `AreaLoad.NodeSequence` | ◐ | uniform only; and FEMEX's free polygon is bounded by *node numbers*, so it consumes model nodes where SAF carries raw coordinates |
| `StructuralPointSupportDeformation` (2.2) | — | ○ | *Unmapped*; imposed support displacement — §7 |

**Self-weight maps well, with one wrinkle.** SAF has no self-weight load object; it is a load case
with `Load type = Self weight`, generated by the receiver from `Unit mass` and the global vertical,
and scaled through the combination's `Multiplier`. FEMEX states direction and strength once on the
root and puts a dimensionless factor on the case, which is the same idea. The wrinkle is that a
`SelfWeightFactor` other than 0 or 1 has no SAF home *on the case* — it has to be pushed into the
`Multiplier` of every combination that names the case, which is only equivalent when the case appears
in combinations at all. Report it as *Approximated* rather than assuming the arithmetic works out.

### 2.6 Results

| SAF | FEMEX | | Loss |
|---|---|---|---|
| `ResultInternalForce1D` (2.1) | — | ○ | *Unmapped* per concept |
| `ResultInternalForce2DEdge` (2.2) | — | ○ | *Unmapped* per concept |

Deliberate on both sides — review §6 refuses results, and `FEMEX_BusinessModel.md` §7 defers
`FemexResults` behind §8 question 6. Recorded here because SAF *does* carry them, so an importer must
say so rather than passing over two sheets in silence.

### 2.7 What SAF does not have

`Grid`/`Gridline`, `FemexMesh`, region `Priority`, and `PlateBehaviour.Plate`. All *Dropped* on the
export leg. See §5.

---

## 3. The five mandatory columns FEMEX cannot fill

This is the answer to *"is anything missing"*, and it is a different claim from the P1 list. These are
not degradations an adapter can report and move past. They are values SAF requires, which means an
exporter must invent them to produce a workbook the SAF validator will accept — and per
`FEMEX_Adapters.md` §4.3, *Invented* is *"the important category, and the one naive adapters never
report, because from inside the adapter an invention does not feel like a loss — it feels like
success."*

| SAF column, mandatory | FEMEX today | What an exporter must do |
|---|---|---|
| `StructuralMaterial.Type` — `Concrete, Steel, Timber, Aluminium, Masonry, Other` | nothing; `Material` is `Name`, `E`, `ν`, `ρ`, `Strength` | guess from density and modulus, or write `Other` |
| `StructuralMaterial.Quality` — the grade, `S235`, `C25/30` | `Name`, used informally with no convention behind it | pass `Name` through and hope, or invent |
| `StructuralLoadCase.Load group` → the whole `StructuralLoadGroup` sheet | nothing; `LoadNature` is per-case and carries no relation | synthesise one group per nature, inventing `Relation` |
| `Model.System of units` — `Metric` \| `Imperial` | `Units.Length`/`Force`, free text, unvalidated, `"length": "banana"` round-trips clean | assume metric per `FEMEX_Adapters.md` §6.6 and report it |
| `Model.National code` — 40+ national annexes | nothing | write `EC-Standard-EN` and report it |

> **Rows 1–2 are closed at 1.7.** `Material.Type` is `MaterialType`, SAF's closed six exactly, and
> `Material.Quality` is the grade as its code writes it — free text, and deliberately distinct from
> `Name`, which stays the label Robot and ETABS key by. An exporter no longer guesses either from the
> density and the modulus, and `Validate()` warns about a material that leaves the type silent.

> **Row 4, after 1.8 — partly stale, and the part that stands is the point.** `Units` is now five
> enums (`LengthUnit`, `ForceUnit`, `TemperatureUnit`, `AngleUnit`, `MassUnit`); `"length": "banana"`
> no longer round-trips clean, it is dropped and named by `Validate()`. **The mandatory column is
> still not filled.** SAF's `System of units` is one `Metric | Imperial` flag about a whole model, and
> five independent per-quantity enums do not supply it — they permit `Metre` with `Kip`, which maps to
> neither value. So the exporter's column stays exactly as written above: assume, and report
> *Invented*. Independence is still the right shape; real models are mixed, with section tables in
> millimetres and coordinates in metres, and a flag forbidding that would be a worse annotation than
> none. `Model.LCS of cross-section`, below, is *Invented* for the same reason and is now recorded as
> such on `Units` itself.

Two conditionals belong beside them:

- **`StructuralCrossSection.Form code`** is mandatory whenever `Cross-section type = Manufactured`.
  `SectionCatalogue` has `Source`, `Profile` and `Manufacture` and no form code, by an explicit
  decision (§7 below argues that decision is half right).
- **`Model.LCS of cross-section`** is mandatory and is one of eight enum values. FEMEX has a single
  fixed convention, so the value is derivable — but it is still an assertion the file must carry and
  that nothing in FEMEX states.

**Two of these five are already on the roadmap, arrived at independently.** Review §7.2 item 7 is
material completeness (α, type enum, grade string) and item 8 is units as enums.
`FEMEX_Interop_Status_16082026.md` §5 keeps them as items 4 and 5, and — this is the part worth
noticing — `FEMEX_BusinessModel.md` §9 explicitly **retains** both against its general demotion of P1
work: *"Items 4 and 5 (material completeness, units as enums) stand: both are small, and both are
what make the numbers in a check report mean anything."* That was reasoned from the report's
credibility. This document reaches the same two items from the SAF spec's required-column list. Two
independent arguments converging on the same small change is about as strong a signal as this
repository generates.

The third, `StructuralLoadGroup`, is on the roadmap at **P2** and should not be. It is not an
optional refinement of the load model; it is a reference SAF will not let a load case omit.

---

## 4. Silent wrong answers

Every item here produces a model that opens, validates against `Validate()`, solves, and is wrong.
This is the list that matters for the product, and it is why this document exists rather than a note
appended to the interop review.

**1. Member behaviour in analysis.** `StructuralCurveMember.Behaviour in analysis` ∈
`Standard | Axial force only | Compression only | Tension only`. FEMEX has no equivalent on `Bar` —
`CompressionOnly` exists only as a `PlateBehaviour` value. A tension-only brace imports as a full
frame element that takes compression. Review §5.3 named this; SAF's four-value enum is exactly the
size the review recommended, so the mapping is trivial once the field exists.

**2. Support degrees of freedom.** SAF translations carry **eight** values — `Rigid, Free, Flexible,
Compression only, Tension only, Flexible compression only, Flexible tension only, Non linear` —
against `Restraint { bool Fixed; double? Stiffness; }`, which spans three. Five collapse to a
bidirectional restraint: an uplift-free bearing resists uplift, a tension-only tie takes compression.
`RelConnectsRigidLink` and `RelConnectsRigidCross` use the same eight-value set plus `Resistance`
columns.

Review §3.5 calls the 6-DOF pattern *"the universal one"* and *"correct, and correctly factored"*.
That is true of the **shape** — six DOFs, a state and a stiffness, reused across point, line and area
targets — and false of the **value set**. The claim needs qualifying rather than withdrawing.

> **Closed at 1.8.** `Restraint.Sense` — `RestraintSense? Sense`, `Both | CompressionOnly |
> TensionOnly`, null meaning both — crossed with `Fixed` and `Stiffness` reaches **seven of the
> eight**; `Restraint`'s own doc carries the mapping table. The eighth, `Non linear`, stays
> deliberately unmapped and is recorded as such on the type: it is a reference to a stiffness curve,
> not a state, and carrying it would mean adding a curve type to FEMEX rather than a value to an enum.
> An adapter reports it *Approximated* or *Dropped*. The 1.8 qualification of review §3.5 is written
> into `Restraint`'s class doc rather than left in this document. `RelConnectsRigidLink` and
> `RelConnectsRigidCross` are still unmapped entirely, so the eight-value set on those sheets is not
> reached by this change.

**3. Load panel spanning direction.** `StructuralSurfaceActionDistribution` carries
`Distribution to` ∈ `One way - X | One way - Y | Two way`, an `LCS Rotation` that orients it, and a
`Load applied to` list naming which beams receive load. FEMEX's `PlateRegionKind.LoadOnly` has the
panel and none of the three. A one-way slab load imports as a two-way one, which redistributes the
whole load path.

Review §3.3 celebrates `LoadOnly` as *"a genuine four-way match"* — Robot cladding, ETABS area type
`None`, RFEM `TYPE_LOAD_TRANSFER`, INDUCTA area loads. The *concept* is a four-way match. The
*parameterisation* is not, and SAF is the target that makes the difference visible.

**4. Analysis eccentricity.** SAF separates two eccentricity families on a curve member: `Structural`
(the BIM offset, does **not** affect internal forces) and `Analysis` (does). **The
`Analysis Y/Z Eccentricity (Begin)/(End)` columns are mandatory**, along with `System line`, which
picks one of nine positions on the section. `Bar` has neither, and nothing else on it can stand in.
Geometry comes across looking right and the stiffness is wrong.

Review §5.2 called this out and named SAF's split as *"the most honest model of the three and worth
copying if this is ever addressed"*. What the review did not say — because it was not looking at
required columns — is that SAF does not treat it as optional detail. The asymmetry it identified
(plates have `Alignment` and `SurfaceOffset`, bars have nothing) is now a mandatory column against
nothing.

**5. Varying members.** `StructuralCurveMemberVarying` states a member as spans, each with a section
or a comma-separated pair for a linear transition, each with an alignment, relative spans summing to
1.0. `tapered` is reserved and unimplemented on `Section`. A haunched portal rafter imports prismatic
at whatever section the adapter picks, and the frame's moment distribution changes.

**6. Thermal loads, twice over.** `StructuralCurveActionThermal` with `Variation = Linear` carries
`TempL`, `TempR`, `TempT`, `TempB` — **two independent gradient axes** referenced to the member LCS.
`TemperatureLoad.GradientPerDepth` is one axis with no sign convention at all, so the top/bottom
gradient loses its direction and the left/right gradient is lost outright. Review §5.8 has the first
half; the second is new here.

And underneath it, `Material` has no thermal expansion coefficient — verified absent
(`grep -ril expansion --include=*.cs` returns nothing). So even the `Constant` variation, which maps
cleanly, arrives as a temperature the receiver cannot turn into a strain. Review §5.5 already calls
this *"an internal inconsistency, not just an omission"*. SAF makes it concrete: `Thermal expansion
[1/K]` is a column on the sheet the adapter is reading, and FEMEX drops it on the floor.

> **The second half is closed at 1.7; the first half is held.** `Material.ThermalExpansion` exists —
> α in 1/K — and the inconsistency is executable rather than merely documented: `Validate()` warns
> when a `TemperatureLoad`'s elements resolve to a material that states none, once per material a
> load reaches rather than once per element. The **two gradient axes** are untouched, so
> `TempL`/`TempR` is still lost outright and `TempT`/`TempB` still loses its sign convention. That is
> §8.1 item 5, held pending Step 0'.

**7. Subsoil.** `StructuralSurfaceConnection` is a Winkler-Pasternak pair: `C1x`, `C1y`, `C1z` in
MN/m³ and `C2x`, `C2y` in MN/m. FEMEX has an area `Support` that may follow a plate, with a
`Restraint.Stiffness` per DOF — and review §5.7 records that *"it is not stated whether that stiffness
is a total spring or a bedding modulus per unit area, and those differ by the plate area"*, still
undefined per status §3. So C2 is *Dropped* and C1 lands in a field whose units nobody has fixed. Two
adapters could read the same SAF file and differ by a factor of the slab area, with neither wrong
against the spec, because there is no spec.

Review §7.2 costs the fix at **XS** — *"documentation + validation, possibly no schema change"*. It is
the cheapest item in this document and the only one that can be closed by writing a sentence.

> **Closed at 1.8, and at exactly that cost — no schema change.** `Restraint.Stiffness` and `Support`
> both now state what the number is measured against per `SupportTarget`: a **total spring**
> (force/length) at a `Point`, **per unit length** (force/length²) along a `Linear`, and a **bedding
> modulus per unit area** (force/length³ — SAF's Winkler `C1`) over an `Area`, in the model's own
> units rather than SAF's MN/m³. So `C1` lands in a field whose units are now fixed, and the two
> adapters differing by a factor of the slab area is no longer a thing the spec permits.
>
> The validation half is one warning: an `Area` support stating a stiffness in a model that does not
> state **both** its length and its force unit. Force per length cubed is a dimension whose magnitude
> cannot be read at all without them, and kN/m³ and kN/mm³ are nine orders of magnitude apart.
>
> **`C2` is still *Dropped*, and is now recorded as deliberately unmapped** on `Restraint` rather than
> only here: the Pasternak terms resist the subsoil's *shear* and couple neighbouring points, which no
> per-DOF spring can express. Carrying them would mean a subsoil type of its own.

**8. Shear modulus.** SAF states `G modulus [MPa]` as its own column, independent of `E modulus` and
`Poisson Coefficient`. `Material` has no `G` and `GetShearModulus()` returns `E / (2(1+ν))`. Where
SAF's stated G is not that quotient — timber above all, where the ratio is nothing like the isotropic
one — FEMEX silently substitutes a different number into every shear-deformation calculation
downstream. This appears in no FEMEX document.

> **Closed at 1.7.** `Material.ShearModulus` is optional and **authoritative over the derived
> value**: `GetShearModulus()` is `ShearModulus ?? E/(2(1+ν))`, the identical stated-wins-over-derived
> rule `Section.GetArea()` already stated for area. `Examples/Example3.femex` is the worked case —
> GL24h stating 650 MPa where the quotient gives 4 423.

---

## 5. Where FEMEX is ahead, and what that costs on the write leg

Short, and honest about being short. Each of these is a *Dropped* when a FEMEX model is written out
as SAF.

**Region priority.** Confirmed against the current spec: `StructuralSurfaceMemberRegion` and
`StructuralSurfaceMemberOpening` carry no precedence field of any kind, so overlapping regions are
undefined behaviour. FEMEX's rule — highest `Priority` wins, base panel as `int.MinValue`, ties broken
`Opening > LoadOnly > Structural`, then list order — is total and deterministic. Review §3.2 stands
without qualification, and it is the one place `FEMEX_BusinessModel.md` §6 can point at when asked
why a format of one's own exists.

**Plate edges named by their two nodes.** `Hinge.EdgeStartNodeId`/`EdgeEndNodeId` survives inserting a
vertex into a contour. SAF uses an integer `Edge` index, and — a detail review §3.6 did not have —
**SAF's own indexing is inconsistent**: `StructuralEdgeConnection.Edge`, `StructuralCurveAction.Edge`,
`RelConnectsRigidMember.Edges` and `ResultInternalForce2DEdge.Edge` are 1-based, while
`RelConnectsSurfaceEdge.Edge` and the `StructuralProxyElement` vertex and face indices are 0-based.
An adapter must handle both, and the two-node form is what makes that safe on the FEMEX side.

**`SectionProperties` is a superset.** SAF's optional numeric columns are `A`, `Iy`, `Iz`, `It`, `Iw`,
`Wply`, `Wplz` — seven. FEMEX carries all seven (`J` is SAF's `It`) **plus** `ShearAreaY`,
`ShearAreaZ`, `Wely` and `Welz`. `FEMEX_StandardSections.md` decision 3 included the design group
*"because it is what makes a SAF conversion lossless rather than merely possible"*, and that has
turned out to be true and then some. This is worth stating loudly because every other document in
this set records sections as FEMEX's weakest area; on the numeric layer it is SAF's.

**Grids and mesh.** SAF has no grid concept at all — no axis, no gridline, nothing. `Grid` and
`FemexMesh` are both *Dropped* on export, both correctly, and both were already predicted by
`SAF_Adapter.md` B3.

---

## 6. The interop review's §6, re-opened for SAF

§6 of `FEMEX_Interop_Review.md` lists what FEMEX should deliberately not chase. It was written to
bound an essential subset across five programs, and most of it holds. Four items do not, because SAF
carries them as first-class objects and SAF is now the only target.

### 6.1 Curved geometry — the one that should change

§6 says: *"NURBS, splines, arcs, circles as first-class edges (RFEM, SAF). FEMEX's 'curves as chords'
decision is the right simplification for an essential subset."*

Against SAF that reasoning is weaker than it looks, for a reason §6 could not have known it was
conceding: **SAF's `Parent ID` exists specifically to make chording survivable**, and FEMEX declined
`Parent ID` on the grounds that chording leaves nothing to reconstitute (§1.2). Taken together, FEMEX
made the simplification *and* refused the mechanism the format it is reading provides for surviving
it. That is the combination that makes the loss irreversible rather than merely lossy.

The cheap half is worth doing on its own: **add `ParentUid` beside `Uid`**. It is one nullable Guid on
`IIdentified`, it costs nothing to anyone who does not use it, and it lets a chorded arc's pieces all
point at the object they came from — so a FEMEX → SAF write can re-emit the arc, and a diff can tell
that eight bars are one member. The expensive half — typed segments on `Bar` and on plate contours —
is a different order of change and is not recommended here.

**Recommendation:** carry `ParentUid`; keep curves-as-chords; report the chording as *Approximated*
and say in the message that the original curve identity is preserved.

### 6.2 Tapered members — should change

§6 does not list tapers; review §2.2 rates `StructuralCurveMemberVarying` at **P2**. It belongs
higher, because §4 item 5 shows it is a silent wrong answer rather than a missing feature, and because
SAF's model of it is small and copyable: an ordered list of spans, each with a section or a pair, each
with an alignment, relative spans summing to one. `Section.cs` already reserves `tapered` in a doc
comment. Whether it is a section discriminator or a property of `Bar` is the open design question —
SAF puts it on neither, in a third object referenced from the member, which is probably right.

**Recommendation:** decide after a real file, but treat it as P1 rather than P2 from now on.

### 6.3 Ribs — should not change

`StructuralCurveMemberRib` is a beam acting compositely with a slab, with `Type of connection`,
`Shape of the rib`, `Effective width` and four separate width columns for checks versus internal
forces. That is a design concept, not a geometry one, and review §6 is right that design parameters
are out of scope. A rib imports as an ordinary `Bar` with an *Approximated* message naming the
composite action that was lost.

**Recommendation:** leave out; report per object, since the loss is per member.

### 6.4 Free loads — genuinely open

Not in §6 at all, which is itself a finding. SAF has three free-load classes and FEMEX has one
free-polygon form (`AreaLoad.NodeSequence`), which is bounded by node numbers rather than by
independent coordinates. See §7.

### 6.5 Results — should stay closed

SAF's result support is two classes, both section forces only — no reactions, no displacements, no
modal, no surface result grids. `FEMEX_BusinessModel.md` §7 already defers `FemexResults` behind §8
question 6 and specifies its shape if it is ever built. Nothing about SAF changes that: it would be a
lot of machinery for two thin sheets.

**Recommendation:** unchanged. Report the two sheets as *Unmapped* per concept.

---

## 7. What is missing that no FEMEX document names

Everything in this section is absent from review §2.2, from review §5, from review §6, and from
`FEMEX_Interop_Status_16082026.md`. It is the part of this exercise that could not have been done by
re-reading what was already written.

### 7.1 Position along a member

The largest omission, and it is not one field but a pattern SAF applies on six classes:

| SAF class | The positional columns |
|---|---|
| `StructuralPointAction` | `Force action = On beam`, `Position x`, `Origin`, `Coordinate definition`, plus `Repeat (n)` and `Delta x` for a series |
| `StructuralPointMoment` | the same |
| `StructuralCurveAction` | `Start point`, `End point`, `Origin`, `Coordinate definition`, `Extent = Full \| Span` |
| `StructuralCurveMoment` | the same |
| `StructuralCurveConnection` | the same, for a line support |
| `StructuralPointSupport` (2.2) | `Boundary condition = On beam`, `Member`, `Position x` |

FEMEX addresses all six by node id. `PointLoad` names a `NodeNumber`; `LinearLoad` names
`StartNode`/`EndNode`; `Support` and `Hinge` carry `List<int> NodeIds`. So a point load at mid-span, a
line load from 0.2L to 0.6L, or a bearing 300 mm from a column face has no home unless nodes exist
there — and a node existing there does not put it *on* the bar, because `Bar` is two nodes and knows
nothing about a third.

The two available answers are both bad. Minting nodes and splitting the member changes the model's
topology, its element count and its member identity, and makes the round trip fail §7.2 equivalence.
Snapping the load to the nearer end changes the answer. Either way it is a loss that recurs on nearly
every real file, and it is on no list anywhere.

`LinearLoad` is closest to a fix: it already carries an optional `BarId` for local-direction
resolution, so relative start and end positions along that bar would be additive. `PointLoad` would
need the host reference it does not have.

### 7.2 Loads and supports on plate edges

`StructuralCurveAction.Force action` reaches `On beam`, `On edge`, `On subregion edge`,
`On opening edge`, `On rib` and — new in 2.2 — `On internal edge`. `StructuralEdgeConnection` has the
same reach for supports. `LinearLoad` names two nodes and, optionally, a **bar**. There is no way to
say "this line load runs along the free edge of that slab", and a line load whose two nodes happen to
be adjacent in a plate contour is expressing it by coincidence, not by reference. Nothing in
`Validate()` distinguishes the two.

### 7.3 Internal edges

`StructuralCurveEdge` is `Name`, `2D member`, `Nodes`, `Segments` — an edge drawn *inside* a surface,
used to force mesh lines, to attach a support, or to carry a load. SAF 2.2 made it a reference target
for both `StructuralEdgeConnection` and `StructuralCurveAction`. FEMEX contours have an outer
boundary and region sub-contours and no notion of an internal edge, so the edge and everything
pointing at it are lost together.

### 7.4 Free loads beyond the polygon

- **`StructuralPointActionFree`** and **`StructuralCurveActionFree`** have no FEMEX equivalent at all.
- **`StructuralSurfaceActionFree`** maps roughly onto `AreaLoad.NodeSequence`, with two differences.
  SAF free loads carry **raw coordinate lists** (`Coordinate X/Y/Z` as semicolon-separated values),
  independent of the model; FEMEX's free polygon is a list of node numbers, so importing one either
  consumes existing nodes or mints new ones that then appear in the model's node table as though they
  were structure. And SAF's `Distribution` ∈ `Uniform, DirectionX, DirectionY, DirectionXY` allows a
  varying free surface load, against `AreaLoad.Magnitude`, one scalar.

One correction to an easy assumption while here: on **real** members SAF surface loads are uniform
only — `StructuralSurfaceAction` has a single `Value [kN/m²]` and no distribution — so `AreaLoad` is
not behind there. Variability exists only on the free form.

### 7.5 Imposed support displacement

`StructuralPointSupportDeformation`, new in 2.2: a support, a direction, and a translation in mm or a
rotation in mrad, in a load case. Settlement is a routine load case in foundation work and FEMEX has
no way to state it. Verified absent (`grep -ril settlement --include=*.cs` returns nothing), and it
appears in no FEMEX gap list.

### 7.6 Form codes 9–23, and the argument that half holds

`FEMEX_StandardSections.md` decision 7 declined SAF's CIS/2 form code on the grounds that *"the `type`
discriminator already is a form code"*. Checked against the actual enum, that is a better argument
than it looked and a narrower one.

SAF form codes 1–8 are: I-section, rectangular hollow section, circular hollow section, L-section,
channel, T-section, full rectangular section, full circular section. FEMEX's eight geometric
discriminators are `ishape`, `box`, `pipe`, `angle`, `channel`, `tshape`, `rectangle`, `circle`. That
is the same eight shapes in a different order. Decision 7 is **literally correct for form codes 1–8**,
which is a stronger vindication than it claims for itself.

It fails for 9–23: T-section up, L-section III, L-section I, L-section IV, channel section left,
asymmetric I-section, asymmetric I-section up, Z-section, Z-section right, omega, omega down, and four
sigmas. Fifteen of twenty-four form codes have no FEMEX discriminator. A SAF `Manufactured` section
with form code 16 crosses as `generic` plus whatever properties the file stated, and on the way back
out the **mandatory** form code has to be invented — most honestly as `0` (`-`, provisional), which
tells the receiving program the shape is unknown.

`Description ID` has the same shape of problem one layer up. It is an enum of 119 catalogue sources
(0 = not specified, 33 = American W-shapes, 91–99 Chinese, and so on) against `SectionCatalogue.Source`,
free text stored exactly as written per decision 7's no-normalisation rule. Import can stringify the
integer; export cannot recover it from an arbitrary string.

**Neither is a reason to reverse decision 7.** Cold-formed sigmas and asymmetric I-sections are not
the essential subset. It is a reason to record that the decision covers a third of the enum, and that
`SectionCatalogue` is where a `formCode` integer would go if a real file ever makes the case.

### 7.7 Material design properties

SAF's `Design properties` column is a labelled list — `"1|235; 2|360"` — drawn from a defined set of
22 values: steel `fy`, `fu`, `fu(minimum)`, `Ry`, `Rt`; concrete `fck`, `fcm`, `fctm`, `fctk 05`,
`fctk 95`, `eps c2`, `eps cu2`, `eps c3`, `eps cu3`; timber `E0.05`, `E90,mean`, `fmk`, `ft0k`,
`ft90k`, `fc0k`, `fc90k`, `fvk`. FEMEX has one `double Strength`.

This is structurally the same idea as `SectionProperties`: a per-type property bag that lets a value
cross even when the receiver does not recognise the grade. FEMEX built that pattern for sections in
1.5 and did not build it for materials. If §3's material work is done, this is the shape to copy —
one optional block, not more scalars on `Material`.

### 7.8 Smaller items, recorded rather than argued

- **Prestress and imposed strain.** SAF has `Load type = Prestress` and a `Tensioning` load-group
  type. Absent from FEMEX and from every FEMEX gap list.
- **`StructuralMaterial.Subtype`** — *hot rolled*, *cold formed*, *stainless*, *prestressed concrete*.
  `SectionManufacture` carries three of these on the *section*, which is the wrong object.
- **`StructuralLoadCase.Duration`** — `Long, Medium, Short, Instantaneous`, mandatory for variable
  cases. Drives timber design.
- **`Model.Ignored objects` / `Ignored groups`** — SAF's update-semantics feature, telling a receiver
  which sheets to leave alone on re-import. FEMEX has no equivalent and no need for one yet, but it is
  the sort of thing a merge story would want, and `FEMEX_Identity.md` notes merging is *"now possible
  and still not implemented"*.

---

## 8. Recommendation

Three lists, deliberately separated, because they are three different decisions.

### 8.1 Close before an importer ships

Each turns a silent wrong answer or an un-writable mandatory column into something correct or
declarable. Each is additive. Sizes are review §7.2's own.

> **Status, after `FEMEX_SAF_Fit_Update_Plan.md` landed schema 1.7 and 1.8 and Phase A′ of
> `AdaptersPlans/SAF_Adapter.md` landed 1.9 and 1.10.** **All seven are closed.** The three that were
> held waited for Step 0', one real SAF 2.2.0 workbook — `FEMEX_SAF_Corpus_Notes.md` — and shipped
> once it existed. Item 3 keeps its qualification: the enums shipped, but §3 row 4's mandatory column
> is **reported, not filled** — see the note there. Item 5 gains one of its own: the gradient's sign
> convention shipped, and the 1.6 files carrying the old unsigned key are migrated with a
> *reinterpretation* message rather than silently re-signed.

| # | Change | Size | Closes | Status |
|---|---|---:|---|---|
| 1 | **`Material.Type` + `Quality` + `ThermalExpansion`** — and prefer SAF's `Design properties` shape (one optional block) over more scalars | S | §3 rows 1–2, §4 item 6 | **Closed** — 1.7 |
| 2 | **Load groups**, or the minimum that satisfies SAF's mandatory reference | S | §3 row 3 | **Closed** — 1.9 |
| 3 | **`Units` as enums**, plus temperature, angle and mass | S | §3 row 4 | **Closed** — 1.8, but see §3 row 4 |
| 4 | **`Bar.Behaviour`** — SAF's four values, exactly | XS | §4 item 1 | **Closed** — 1.10 |
| 5 | **Temperature gradient axis** — a sign convention referenced to the local frame | XS | §4 item 6 | **Closed** — 1.9, with a migration message |
| 6 | **Bedding semantics** — state whether an area `Restraint.Stiffness` is a total spring or a modulus per unit area | XS | §4 item 7 | **Closed** — 1.8 |
| 7 | **`Material.ShearModulus`**, optional, authoritative over the derived value | XS | §4 item 8 | **Closed** — 1.7 |

Items 1 and 3 are review §7.2 items 7 and 8, which `FEMEX_BusinessModel.md` §9 already retained. Item
6 needed documentation and a validation rule and no schema change at all, exactly as predicted. Items
4, 5 and 7 are one property each.

**Four changes not on this list closed with them**, and each was one of the silent wrong answers §8.1
left out: **`Restraint.Sense`** (1.8), which takes §4 item 2 from three of SAF's eight translation
states to seven; **`LoadDistribution` on the panel** (1.9), which closes §4 item 3 — a one-way slab
had been crossing as a two-way one; **`BarEccentricity`** (1.10), which keeps SAF's structural /
analysis split rather than fusing it, closing §4 item 4; and **`Bar.EndSectionId`** (1.10), which
**downgrades** §4 item 5 to *Approximated* rather than closing it — SAF states a varying member as
spans and the corpus's one real example has three of them, so a rafter haunched at both ends still
arrives with the wrong moment distribution, now with a message attached. That leaves **curved
geometry (§6.1) as the last of the eight**, and 1.9's `ParentUid` makes it recoverable where it had
been the one non-reversible loss in this document.

One item this table did not list is closed with them: **`Restraint.Sense`**, which takes §4 item 2
from three of SAF's eight translation states to seven. It was one of the four silent wrong answers
§8.1 left out, and the update plan's *Context* argues why two of those four were built and two held.

`Model.National code` is deliberately not on this list. It is mandatory, it has no FEMEX home, and the
right answer is probably to keep it out of the format and let the adapter report the assumption — the
same treatment `FEMEX_Adapters.md` §6.6 gives units. A national code is a statement about a design
process, not about a model.

### 8.2 Declare, do not close

Everything an adapter can report honestly, with the category it should carry:

- *Invented* — synthesised `Level`s (every file), assumed unit system, `Model.National code`,
  `Model.SAF Version`, `Model.LCS of cross-section`, synthesised names for bars, nodes, supports and
  hinges, placeholder sections for `Bar.SectionId == 0`, `Form code = 0` for a shape FEMEX did not
  model.
- *Dropped* (export leg) — `Grid`, `FemexMesh`, region `Priority`, `PlateBehaviour.Plate`,
  `LoadCombinationType.AbsoluteAdd` and `Srss`, `Level.IsGround`, `Level.RelativeElevation`.
- *Dropped* (import leg), one message per concept because each is annotation rather than analysis —
  `StructuralCurveMember.Type` (17 values), `Layer` and `Color` on every object that carries them,
  `StructuralCurveAction.Eccentricity ey/ez`, `StructuralMaterial.Subtype`,
  `StructuralLoadCase.Duration`, `StructuralLoadCombination.National standard`, ten of the eleven
  `Project` columns, and the `CompositeShapeDef` polygon behind a `General` cross-section.
- *Approximated* — chorded curves, flattened varying members, `generic` sections, ribs as plain bars,
  a `SelfWeightFactor` other than 0 or 1, seven of eight thickness types.
- *Unmapped*, **per concept, not per object** — `StructuralProxyElement`, `RelConnectsRigidLink`,
  `RelConnectsRigidCross`, `RelConnectsRigidMember`, `StructuralCurveEdge`,
  `StructuralPointActionFree`, `StructuralCurveActionFree`, `StructuralPointSupportDeformation`, both
  `ResultInternalForce*` classes, `Model.Ignored objects`.

That list is `SAF_Adapter.md` §B3's five predicted losses, corrected and completed. B3 predicted grids,
region priority, mesh, `StructuralProxyElement` and the placeholder section — five of the twenty-eight
above.

### 8.3 Decide only after a real file

Per `SAF_Adapter.md` B5 and status item 6. Argument on both sides stated in §6 and §7; resolved by
nothing here.

- Curved geometry — but `ParentUid` is cheap and separable, and §6.1 recommends it on its own.
- Varying and tapered members — promote from P2 to P1 in the meantime.
- Position along a member (§7.1) — the largest single omission, and the one most likely to be forced
  by the first real file.
- Loads and supports on plate edges, and internal edges (§7.2–7.3).
- Free point and free line loads (§7.4).

---

## 9. What this makes stale

- **`AdaptersPlans/SAF_Adapter.md` §B3.** The mapping table's nine rows and five predicted losses are
  correct as far as they go and cover roughly a fifth of the surface. §2 and §8.2 above replace them.
  B5's promise of `FEMEX_SAF_Mapping.md` after a real file has been read stands; this document is the
  prior step, not that one.
- **`FEMEX_Interop_Review.md` §3.3.** *"`LoadOnly` is a genuine four-way match"* — the concept is; the
  parameterisation is not. `StructuralSurfaceActionDistribution` carries a spanning direction FEMEX
  cannot express (§4 item 3).
- **`FEMEX_Interop_Review.md` §3.5.** *"The 6-DOF boundary-condition pattern is the universal one"* —
  true of the shape, false of the value set. SAF has eight translation states to FEMEX's three (§4
  item 2).
- **`FEMEX_Interop_Review.md` §6.** Re-opened for curved geometry and tapered members; confirmed for
  ribs, results, solver settings, design parameters and the rest (§6).
- **`FEMEX_Interop_Review.md` §7.1.** The *"roughly 70%"* figure predates 1.4, 1.5 and 1.6 and has
  never been restated. Against SAF specifically the number is higher on the numeric layer — where
  `SectionProperties` is now a superset — and lower on the geometry layer, where segment types and
  per-position addressing were never counted.
- **`FEMEX_Interop_Review.md` §2.2.** `StructuralLoadGroup` at P2 is wrong: it is a mandatory
  reference (§3). `StructuralCurveMemberVarying` at P2 is wrong: it is a silent wrong answer (§4).
- **`FEMEX_StandardSections.md` decision 7.** Holds for form codes 1–8, which are exactly FEMEX's
  eight shapes. Fails for 9–23, and the form code is mandatory for `Manufactured` sections (§7.6).
- **`FEMEX_Identity.md`'s reasoning for omitting `Parent ID`.** *"Curves-as-chords leaves nothing to
  reconstitute"* is true inside FEMEX and false at the SAF boundary (§1.2, §6.1).

No `.cs` file changes follow from this document.

---

## Still open

- **Nothing here has been checked against a real SAF file.** The same caveat every document in this
  set carries, and it bites hardest on §2, which is built from the specification alone.
  `SAF_Adapter.md` makes it worse rather than better: the published corpus is **11 `.xlsx` files**
  spanning versions 1.0.5 → 2.2.0, only one or two at 2.2.0. Until Graphisoft's and SCIA's exports are
  added, or an engagement supplies real files, §2's verdict column is a set of predictions.
- **Whether §8.1 should be done at all before that corpus improves.** The argument for is that all
  seven items are additive, XS–S, and two of them are already retained by the business model. The
  argument against is `FEMEX_Interop_Status_16082026.md` §5's own warning: *"building nine more P1
  entities against documentation, before a single real file has been read, is how a format acquires
  the wrong vocabulary confidently."* Items 4–7 are single properties with one obvious spelling each
  and are hard to get wrong. Items 1–3 involve enums and shapes, and are exactly what a real file
  would inform.
- **Whether `Model.National code` belongs in FEMEX at all.** §8.1 argues no. It is mandatory in SAF,
  which is an argument the other way, and nobody has asked an engineer whether a code reference on a
  transferred model is information they would want to see preserved.
- **What an adapter does about §7.1** — minting nodes, snapping to the nearest end, or refusing.
  All three are wrong in different ways, and the choice should not be made by whoever writes the
  importer first, for the same reason `FEMEX_Adapters.md` §7.2 fixed the equivalence definition before
  the diff was written.
- **Whether the eight-state support enum matters in practice**, or whether compression-only supports
  are rare enough in the models this network builds that collapsing them is acceptable. §4 item 2
  assumes it matters. It is a good question for `FEMEX_BusinessModel.md` §8's conversations.
- **`ParentUid`'s scope.** §6.1 recommends it for chorded curves. It would also serve segmented
  members generally and the diff's "these eight bars used to be one member" problem, which is
  `FEMEX_BusinessModel.md` §3 Claim 2 territory. Whether it is one field or the thin end of a
  derivation-tracking design has not been thought about.

---

## Sources

**SAF 2.2.0**, verified at <https://www.saf.guide/> (the `stable` branch is 2.2.0) — the *Getting
started*, *Structural analysis elements*, *Supports and hinges*, *Loads*, *Results* and *Annexes*
chapters, plus <https://www.saf.guide/en/stable/getting-started/release-notes.html> for the 2.0/2.1/2.2
deltas. Note that the GitBook mirror at <https://gitbook.saf.guide/> is pinned at **2.0.0** and must
not be cited for anything added in 2.1 or 2.2. Corpus and SDK:
<https://github.com/StructuralAnalysisFormat>.

**FEMEX**, verified directly in this repository at schema 1.6: `FemexModel.cs`,
`FemexModel.Validation.cs` (`:415` for the level foreign key, `ValidateContourPlanarity`,
`ValidateNameKeys`, `ValidateSections`, `ValidateRegionPriorities`), `FemexModel.LocalAxes.cs`
(`TryGetBarLocalAxes`, `TryGetPlateLocalAxes`, `TryGetLoadDirection`), `FemexModel.SelfWeight.cs`,
`FemexModel.Identity.cs`, `FemexModel.Nodes.cs`, `Geometry/Node.cs`, `Geometry/Level.cs`,
`Geometry/Bar.cs`, `Geometry/Plate.cs`, `Geometry/PlateRegion.cs`, `Geometry/PlateBehaviour.cs`,
`Geometry/SurfaceAlignment.cs`, `Geometry/Sections/*` (all nine shapes plus `SectionCatalogue`,
`SectionProperties`, `SectionManufacture`), `Geometry/Surfaces/*`, `Materials/Material.cs`,
`BoundaryConditions/*`, `Loads/*` including `Combinations/*`, `Units.cs`, `Gravity.cs`,
`FileMetadata.cs`, `IIdentified.cs`, `IExtensible.cs`.

Absence claims were checked by grep across `*.cs` before being asserted, following the method of
`FEMEX_Interop_Status_16082026.md` §3. *diaphragm*, *rigid link*, *constraint*, *eccentric*,
*subsoil*, *prestress*, *settlement*, *expansion*, *haunch* and *modifier* return no type or property
in the model.

**Prior FEMEX documents** — `FEMEX_Interop_Review.md` §1.6, §2.1, §2.2, §3.2, §3.3, §3.5, §3.6, §3.7,
§5.1–§5.9, §6, §7.1, §7.2, §7.3 · `FEMEX_Interop_Status_16082026.md` §0, §1, §3, §5 ·
`FEMEX_Adapters.md` §4 (the `LossCategory` taxonomy), §5.4, §6.1–§6.6, §7.2, §7.6, §9 ·
`FEMEX_StandardSections.md` decisions 3, 5, 6, 7 · `FEMEX_Plates.md` · `FEMEX_Identity.md` ·
`FEMEX_BusinessModel.md` §2, §3, §4, §6, §7, §8, §9 · `AdaptersPlans/SAF_Adapter.md` B3, B4, B5 and
*Still open*.
