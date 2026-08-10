# Plan — FEMEX interoperability review against Robot, Revit, ETABS, INDUCTA RCB and Dlubal RFEM

## Context

FEMEX is a C# library defining a JSON model format for FE structural models: `FemexModel` holds flat lists
of `Level`, `Node`, `Section`, `SurfaceProperty`, `Bar`, `Plate`, `Material`, `LoadCase`, `Load`, `Support`,
`Hinge`, `Grid`, plus an optional generated `Mesh`, all cross-referenced by integer ids. It has grown
through four design rounds documented in this folder, and has never been checked against the programs it is
meant to move data between.

The question is whether FEMEX's current shape is the right container for the **essential, transferable
subset** of models authored in Autodesk Robot, the Revit analytical model, CSI ETABS, INDUCTA RCB and
Dlubal RFEM. Nobody expects FEMEX to describe any of them fully; the test is whether a model can cross from
one to another without losing something the receiving program cannot recover or infer.

I researched all five data models (Robot COM/`.str`, Revit 2023+ analytical API, ETABS `.e2k` + CSI OAPI,
INDUCTA RCB from vendor docs and change logs, RFEM 6 Web Services), plus **SAF 2.2** — the open
Nemetschek/SCIA analysis-exchange standard that every one of these vendors except Autodesk and INDUCTA
supports, and which `FEMEX_Plates.md` already calls "FEMEX's closest peer".

**Deliverable: a written assessment only. No schema or code changes in this pass.**

## Decisions locked in

1. **Deliverable is the document.** Gaps get documented and prioritised, not implemented.
2. **Node stays level-based.** `Node = (X, Y, LevelNumber, VerticalOffset)` is kept. The review states the
   consequence honestly rather than proposing to change it.
3. **Identity direction: GUID as the unique id, label as an optional name.** The review evaluates this
   against what the five programs actually key on, rather than assuming the current integer ids.

## Headline verdict to be argued in the document

FEMEX's **structure** is sound and close to right: flat id-referenced collections, discriminated unions with
a `type` field, the design-panel/mesh two-tier split, and the priority-based region model are all defensible
and in places (plate regions) better than SAF. The problem is **coverage**: there are concepts that all five
programs require, that FEMEX cannot express at all, and where the receiving program cannot guess the answer.
Those, not the file shape, are what would make a transfer lossy today.

## Document to produce

Single file: **`Claude/FEMEX_Interop_Review.md`**, following the house style of the other docs in this
folder (each ends with an explicit "Still open" section).

### Section 1 — How the five programs are built

One subsection each, at the level of detail needed to judge FEMEX, not an API reference:

- **Robot** — hybrid identity: integer-numbered nodes/bars/objects, but *properties are name-keyed labels*
  (`Labels.StoreWithName`; the name **is** the foreign key, and storing a duplicate name overwrites
  silently). One `RobotObjObject` serves as panel, cladding and opening, distinguished by `Attribs.Meshed`
  and the `I_LT_CLADDING` label. `.str` text export is frozen, undocumented publicly and cannot carry panels
  at all — the COM API is the real model.
- **Revit (2023+)** — analytical model rebuilt as first-class authorable elements: `AnalyticalMember`,
  `AnalyticalPanel`, `AnalyticalOpening`, `AnalyticalLink`, `BoundaryConditions`. Nodes are *derived*, not
  authored. Sections are `FamilySymbol` + `StructuralSectionShape`; materials are `StructuralAsset`
  (E, G, ν, density, α, damping, yield/ultimate). Everything keyed by `ElementId`/`UniqueId`.
- **ETABS** — the closest architectural relative of FEMEX. Story-based: points are plan `(X, Y)` plus a story
  plus `DZ`; lines/areas carry a per-vertex `Below` flag so columns span stories automatically. Object-vs-
  element split (user objects auto-mesh into analysis elements) mirrors FEMEX's plate/mesh split. Adds
  diaphragms, pier/spandrel labels, cardinal points, end length offsets, stiffness modifiers, mass source.
- **INDUCTA RCB** — also storey-based, deliberately grid-free ("model without specifying a working grid").
  Levels with R.L. and master/slave; column/wall/beam/slab *type tables*; wall groups (its pier analogue);
  slab zones; **zone-independent area loads on free polygons** — the documented precedent for FEMEX's
  `PlateRegionKind.LoadOnly` and `AreaLoad.NodeSequence`. Caps at 40 load cases. No public schema; findings
  are reconstructed from vendor pages, training indexes and the public modification-history logs, and the
  document will say so.
- **RFEM 6** — the richest: 16 member types, 9 surface stiffness types, 9 thickness types, 12 member-load
  distributions, full solids. Integer `no` per object type. **Global Z points down by default** — a sign trap
  for any translator.
- **SAF 2.2** — the benchmark. Name as reference key + optional GUID `Id` + `Parent ID` for objects derived by
  segmentation; explicit `StructuralSurfaceMemberRegion` and `...Opening`; the reusable 6-DOF
  `ux/uy/uz/fix/fiy/fiz` × `Free|Rigid|Flexible` + stiffness pattern; `Location = Length|Projection`.

### Section 2 — Entity-by-entity mapping table

The core of the document: a table with a row per FEMEX concept and a column per program, marking exact match
/ lossy / absent, then a second table of concepts the programs have that FEMEX does not. Built from the
field-level evidence already gathered (Robot `I_LT_*` label types and `I_LRT_*` load records, ETABS `.e2k`
sections and OAPI setter signatures, RFEM enum names, SAF column names, Revit API properties).

### Section 3 — What FEMEX gets right

To be argued, not asserted:

- Flat id-referenced collections with a `type` discriminator — the same shape as SAF worksheets and the RFEM
  object model. Correct choice.
- **Plate regions with integer priority** — strictly more expressive than SAF's
  `StructuralSurfaceMemberRegion` and ETABS' type-implicit precedence, and a fair match to RAM Concept.
  Genuinely good.
- `PlateRegionKind.LoadOnly` maps cleanly to RCB area loads, ETABS `None` areas, Robot claddings and RFEM
  `TYPE_LOAD_TRANSFER` — a real four-way match.
- Design-panel vs generated-mesh separation mirrors ETABS object/element and RFEM surface/FE mesh.
- `Restraint {Fixed, Stiffness?}` and `Release {Released, ResidualStiffness?}` are the universal 6-DOF pattern
  in all five programs and SAF.
- Level-based nodes are a *better* fit for ETABS and RCB than absolute XYZ would be.
- Relative geometric tolerances tied to the bounding-box diagonal — avoids the mm/m unit trap.

### Section 4 — Blocking gaps (P0): all five programs need it, FEMEX cannot say it

Each written up as: what the five programs call it, why the receiver cannot infer it, what it would cost.

1. **No load combinations.** Robot `Cases.CreateCombination` + `CaseFactors`, Revit `LoadCombination`
   (Combination/Envelope × ULS/SLS), ETABS `$ LOAD COMBINATIONS` `COMBO`, RCB's combination table,
   RFEM `loadCombination`, SAF `StructuralLoadCombination`. The single largest gap — a model without
   combinations cannot be analysed on arrival.
2. **Loads have no direction.** `AreaLoad.Magnitude` and `LinearLoad.MagnitudeStart/End` are bare scalars.
   Every program requires magnitude **+ direction + coordinate system (global/local)**, and separately
   **true-length vs projected** (SAF `Location`, RFEM `_TRUE`/`_PROJECTED`, Robot `I_URV_PROJECTED`, ETABS
   `Dir` 1–11). A receiver cannot guess whether 1.5 kN/m² is gravity or wall pressure.
   `FEMEX_Plates_Summary.md` already lists this as still open.
3. **No self-weight.** `Material.UnitWeight` exists but nothing states whether gravity is applied or into
   which case. Robot `I_LRT_DEAD`, ETABS `LOADPATTERN ... SELFWEIGHT 1`, RFEM load-case `self_weight` with
   `fx/fy/fz`, SAF `Load type = Self weight`. Either double-counted or silently dropped on every transfer.
4. **Sections cannot describe most real members.** Only `rectangle`, `circle`, `tshape`. No I/H, channel,
   angle, hollow or box; no catalogue reference (which is how Robot, ETABS, RFEM and SAF actually identify
   steel — plus SAF's CIS/2 form code for disambiguation); and no explicit A/Iy/Iz/J escape hatch, which is
   the only mechanism that lets an unrecognised section round-trip numerically instead of being lost.
5. **No `schemaVersion` / producer metadata.** No `UnmappedMemberHandling` is set either, so the plate
   migration already demonstrated the failure mode: an old file loads clean and semantically empty. Every
   comparable format carries a version; SAF has a whole `Model` sheet.
6. **Identity.** Per the locked-in direction, the review assesses **GUID-as-unique-id + optional label**
   against what the programs key on: Robot properties are *name*-keyed (so a label must survive the round
   trip or the exporter has to mint names), ETABS sections and stories are name-keyed, SAF references by Name
   and carries a GUID purely for re-import matching, Revit/IFC are GUID-native. The document will set out
   what GUID-primary buys (stable round-trip identity, no id collisions when merging models), what it costs
   (unreadable JSON, no natural ordering, larger files, every existing test and `Example1.femex` rewritten),
   and the SAF-style middle path.

### Section 5 — Important gaps (P1): needed for a faithful transfer, but inferable or degradable

- **Rigid diaphragms** — the defining ETABS concept for lateral models (`$ DIAPHRAGM NAMES`, rigid vs
  semi-rigid); RFEM `rigidLink` diaphragm type; Robot rigid links; Revit `AnalyticalLink`. FEMEX is a
  *level-based building* format with no diaphragm and no rigid link at all. Arguably belongs in P0 for
  buildings; the document will make the case and let the reader place it.
- **Bar end offsets / rigid ends / insertion point** — Robot `I_LT_BAR_OFFSET`, ETABS cardinal point 1–11 +
  end length offsets + rigid zone factor, RFEM `memberEccentricity`, SAF `System line` + Structural/Analysis
  eccentricities. FEMEX has plate `Alignment`/`SurfaceOffset` but nothing for bars — inconsistent, and it
  matters for beams under slabs, which is FEMEX's core use case.
- **Bar behaviour** (truss / tension-only / compression-only) — Robot `I_BTC_*` and `TrussBar`, RFEM's 16
  member types, SAF `Behaviour in analysis`, ETABS. FEMEX has `CompressionOnly` for plates only.
- **Stiffness modifiers** — ETABS `AMOD/I2MOD/I3MOD/…` cracked-section factors, RFEM
  `memberStiffnessModification`/`surfaceStiffnessModification`, Robot's reduced-stiffness orthotropy.
  Near-universal in concrete design and, as the ETABS community documents, the classic silent-loss item.
- **Material completeness** — no thermal expansion coefficient α, despite FEMEX having `TemperatureLoad`;
  no material type enum (concrete/steel/timber); no grade/standard string, which is how every program
  actually resolves a material.
- **Support local axes / inclined supports** — SAF `Coordinate system = Global|Local`, RFEM, ETABS. FEMEX
  restraints are implicitly global.
- **`AreaLoad` and `TemperatureLoad` under-specification** — `GradientPerDepth` has no axis (which face is
  hotter), `SurfaceProperty` has only `constant` (no variable, layered or orthotropic, all of which RFEM,
  Robot and SAF carry).
- **Units are unvalidated free text** — `Units { Length?, Force? }` as arbitrary strings, with no temperature,
  angle or mass unit and nothing enforcing them, versus SAF's declared unit system with per-column units.

### Section 6 — Deliberately out of scope

State plainly what FEMEX should **not** chase, so the gap list is not read as a to-do: solids, NURBS and
curved geometry, imperfections, solver/analysis settings, code-combination engines, design parameters and
reinforcement, results (beyond what FEMEX already declines), auto wind/seismic generators, meshing options,
and Robot/ETABS program-specific constructs (claddings, pier/spandrel labels, mass source, load-to-mass
conversion, RCB wall groups).

### Section 7 — Verdict and prioritised recommendation

A direct answer to "is FEMEX optimal": the container is right, the vocabulary is not yet complete. A ranked
list — P0 items in dependency order, then P1 — with a one-line note on the size of each change and which of
the 85 tests and `Examples/Example1.femex` it touches, so a future implementation pass can be scoped from
the document alone. Closes with a "Still open" section matching the house style of the other docs here.

## Verification

The document is the deliverable, so verification is review-based, not test-based:

- Every claim about FEMEX cites a file and type in this repo (verified directly against `FemexModel.cs`,
  `Geometry/Node.cs`, `Loads/AreaLoad.cs`, `Loads/LinearLoad.cs`, `Materials/Material.cs`,
  `Geometry/Sections/*`, `BoundaryConditions/*`, `Examples/Example1.femex`).
- Every claim about an external program cites a URL, and anything reconstructed rather than documented — the
  ETABS `.e2k` grammar beyond the handful of verified record lines, and essentially all of INDUCTA RCB — is
  explicitly labelled as such rather than presented as spec.
- `dotnet build` and `dotnet test` are run once at the end to confirm the repo is untouched and still green
  (85 facts), since this pass changes no code.
