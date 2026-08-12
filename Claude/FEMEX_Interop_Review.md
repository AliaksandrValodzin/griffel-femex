# FEMEX Interoperability Review

*An assessment of the FEMEX schema against Autodesk Robot Structural Analysis, the Revit analytical model,
CSI ETABS, INDUCTA RCB and Dlubal RFEM 6.*

> **Scope.** This is an assessment only. Nothing in the schema is changed by this document. The question is
> not "can FEMEX describe these programs fully" — it cannot, and is not meant to. The question is whether
> FEMEX is the right container for the **essential, transferable subset**: can a model cross from one
> program to another without losing something the receiver cannot recover or infer?

---

## 0. Verdict

**The container is right. The vocabulary is not yet complete.**

FEMEX's structural decisions are sound and, in one case, better than the open standard it most resembles.
Flat id-referenced collections, discriminated unions with a `type` field, the design-panel-versus-generated-
mesh split, and the priority-based plate region model all hold up against five real programs. If the format
were being redesigned from scratch today with this research in hand, the shape would come out much the same.

What does not hold up is coverage. There are six things that **all five programs require, that FEMEX cannot
express at all, and that the receiving program cannot guess**. Chief among them: FEMEX has no load
combinations, and its distributed loads have no direction. A FEMEX file today can describe the geometry of a
building faithfully and still not be analysable on arrival.

The good news is that every one of these is additive. None of them requires unpicking a decision already
made. The format does not need to be re-architected; it needs a handful of missing nouns.

---

## 1. How the five programs are built

Enough of each to judge FEMEX by — not an API reference.

### 1.1 Autodesk Robot Structural Analysis

Robot is a **hybrid identity model**, and this is the most important thing about it for interoperability.
Structural elements are integer-numbered (`RobotNode.Number`, `RobotBar.Number`), but **properties are
name-keyed labels**. `Structure.Labels` is a dictionary keyed by `(IRobotLabelType, string name)`:

```
label = Labels.Create(IRobotLabelType.I_LT_SUPPORT, "");
Labels.StoreWithName(label, "Fixed");                  // the name IS the identity
node.SetLabel(IRobotLabelType.I_LT_SUPPORT, "Fixed");  // assignment by name
```

There are no property ids. Two supports with identical data but different names are different labels; two
labels with the same name cannot coexist — `StoreWithName` silently overwrites. Label types
(`I_LT_BAR_SECTION`, `I_LT_MATERIAL`, `I_LT_BAR_RELEASE`, `I_LT_BAR_OFFSET`, `I_LT_PANEL_THICKNESS`,
`I_LT_CLADDING`, `I_LT_NODE_RIGID_LINK`) are separate namespaces.

Robot has one geometry object (`RobotObjObject`) that becomes a meshed FE panel, a load-distributing
cladding, or an opening depending on `Main.Attribs.Meshed` and which label it carries. Loads are
`IRobotLoadRecord`s created on a case (`I_LRT_NODE_FORCE`, `I_LRT_BAR_UNIFORM`, `I_LRT_BAR_TRAPEZOIDALE`,
`I_LRT_UNIFORM`, `I_LRT_IN_CONTOUR`, `I_LRT_DEAD`, `I_LRT_BAR_THERMAL`, …), each with a value enum.

**On `.str`:** do not build against it. Autodesk's own position on the forum is that the text format is no
longer developed and is language-version-dependent; it cannot carry panels, meshing, job preferences or
design parameters. SCIA's Robot text importer supports only nodes, beams, sections, supports and hinges.
The COM API is Robot's real data model.

- <https://forums.autodesk.com/t5/robot-structural-analysis-forum/str-reference-manual/td-p/3191542>
- <https://www.scia.net/en/support/addons/robot-text-import-scia-engineer>
- <https://github.com/BHoM/Robot_Toolkit> — the most complete public RobotOM mapping

### 1.2 Revit analytical model (2023+)

Revit 2023 replaced the analytical model wholesale, with no deprecation period. The old
`AnalyticalModel`/`AnalyticalModelStick`/`AnalyticalModelSurface` family and `Element.GetAnalyticalModel()`
are gone. The new model is a set of **independent, first-class, author-able elements**:

| Concept | Class | Key members |
|---|---|---|
| 1D member | `AnalyticalMember` | `SectionTypeId`, `MaterialId`, `CrossSectionRotation` (rad), `StructuralRole`, `GetCurve()`, `GetReleaseConditions()`, `GetTransform()` |
| 2D member | `AnalyticalPanel` | `Thickness`, `MaterialId`, `SketchId`, `GetOuterContour()` → `CurveLoop`, `GetAnalyticalOpeningsIds()` |
| Void | `AnalyticalOpening` | `CurveLoop` + host panel |
| Node | `AnalyticalNodeData` | **derived, not authored** — nodes materialise where members meet; `GetConnectionStatus()` |
| Rigid link | `AnalyticalLink` + `AnalyticalLinkType` | connects two *Hubs*; fixity lives on the type |
| Support | `BoundaryConditions` | typed `Point | Line | Area`; DOFs exposed as **Parameters**, not typed properties |

Sections are `FamilySymbol` + `StructuralSectionShape` (34 shapes). Materials are `StructuralAsset`:
`YoungModulus`, `PoissonRatio`, `ShearModulus`, `Density` (**mass** density, unlike Robot's `RO` weight
density), `ThermalExpansionCoefficient`, `DampingRatio`, yield/ultimate, plus concrete/timber specifics.
Loads (`PointLoad`, `LineLoad`, `AreaLoad`) are always hosted on the *analytical* element and carry
`LoadCaseId`, `OrientTo`, `IsProjected`. `LoadCombination` is `Combination | Envelope` ×
`Serviceability | Ultimate` with `(factor, case)` components.

Notably, `AnalyticalMember` has **no offset property** — Revit expresses eccentricity geometrically, by
placing the analytical curve where it belongs, decoupled from the physical solid.

- <https://www.revitapidocs.com/2023/news?section=toc3>
- <https://help.autodesk.com/cloudhelp/2024/ENU/Revit-API/files/Revit_API_Developers_Guide/Discipline_Specific_Functionality/Structural_Engineering/Revit_API_Revit_API_Developers_Guide_Discipline_Specific_Functionality_Structural_Engineering_Analytical_Model_html.html>

### 1.3 CSI ETABS

**ETABS is FEMEX's closest architectural relative**, and the resemblance is not superficial.

Geometry is stored as **plan coordinates plus a story reference plus a vertical offset** — exactly FEMEX's
`Node = (X, Y, LevelNumber, VerticalOffset)`. CSI's own spreadsheet-editing documentation gives the columns
as Point: `Type, X, Y, DZ`; Line: `Type, Section, XI, YI, DZI, BelowI, XJ, YJ, DZJ, BelowJ`; Area: `Type,
Section, #Points, X-n, Y-n, DZ-n, Below-n`. The `Below` flag lets a column span story-to-story regardless of
story height.

ETABS also splits **objects** (what the user draws: `PointObj`, `FrameObj`, `AreaObj`) from **elements**
(what the solver sees, produced by auto-meshing: `PointElm`, `LineElm`, `AreaElm`) — the same two-tier idea
as FEMEX's `Plate` versus `FemexMesh`. This is why `.e2k` stores connectivity and assignments but never the
mesh.

On top of that it adds the concepts that make it a building program: ordered stories with master/similar
replication, **diaphragms** (rigid or semi-rigid, named, assigned to points or areas), **pier and spandrel
labels** for wall design, cardinal points 1–11 plus joint offsets, end length offsets with a rigid zone
factor, per-DOF releases with partial fixity springs, shell types (Membrane / Plate / Shell-Thin /
Shell-Thick / Layered), stiffness modifiers, self-weight via a load pattern multiplier, and a separate
**Mass Source**.

*Caveat on `.e2k`:* CSI publishes no specification. The record syntax quoted in this document beyond a
handful of verified lines (`PROGRAM`/`VERSION`, `STORY`, `POINT`, `LINE`, `POINTLOAD`) is reconstructed and
should be validated against a real export before anyone writes a parser. The **OAPI is the documented
model** and is what an implementation should target.

- <https://docs.csiamerica.com/help-files/etabs/Keyboard_Commands_and_Special_Features/Editing_ETABS_Geometry_Using_a_Spreadsheet.htm>
- <https://docs.csiamerica.com/help-files/etabs-api-2016/html/15293a27-a035-e64b-a4b4-78356479aa98.htm>
- <https://web.wiki.csiamerica.com/wiki/spaces/etabs/pages/1474609/Rigid+vs.+Semi-rigid+diaphragm>

### 1.4 INDUCTA RCB

**Honesty first: there is no public data model, file schema or API for INDUCTA products.** `.rcb`, `.slb`
and `.ptd` are proprietary zipped containers. What follows is reconstructed from the vendor's product pages,
the training-video topic indexes (which mirror the input menus and are therefore a decent proxy for the data
model) and — most usefully — the public modification-history logs. Treat it as informed inference.

RCB is storey-based like ETABS, but **deliberately grid-free**: the vendor's own pitch is modelling "without
specifying a working grid or creating beam strips". A model is levels (name, storey height, **Level R.L.**,
master/slave, optional per-level material), then per level: columns (with a *column type* table, arbitrary
profile shapes, rotation, and inclined columns as a distinct entity), walls (with door/window openings, and
**wall groups** — up to 1,000 — as its lift-core/pier analogue), beams (line and band, with a torsion
stiffness factor and vertical offset), **slab zones** (polygons with thickness and type), joints and corbels,
piles and Winkler/SSI foundations.

Two things matter for FEMEX. First, RCB caps at **40 primary load cases**. Second, and more interesting,
RCB distinguishes **Pressure Loads** (UDL bound to a slab zone) from **Area Loads** (UDL on a free polygon,
*independent* of any slab zone, added specifically so Revit-imported models don't need extra geometry lines,
and surviving geometry edits). That is precisely the distinction FEMEX already draws between
`AreaLoad.PlateId` and `AreaLoad.NodeSequence`, and it is the acknowledged precedent for
`PlateRegionKind.LoadOnly`.

Import/export: DXF both ways, a per-version Revit Link (2017–2026, actively maintained), an ETABS import,
and **RCBLink** (geometry + material properties + internal forces from CSI SAFE, CSI ETABS and Bentley RAM
Concept into RCB, requiring the source program installed — which strongly implies API-driven, not
file-parsing). No IFC, no SAF, no documented neutral text format, and no documented RCB → ETABS path.

- <https://www.inducta.com.au/RCB_main.html> · <https://inducta.com.au/RCB_arealoads.html>
- <https://www.inducta.com.au/b6/ModificationHistory/RCBuilding_ModificationHistory.txt>
- <https://www.inducta.com.au/RCBLink/RCBLink.html>

### 1.5 Dlubal RFEM 6

The richest of the five, and the one that most exceeds FEMEX's ambitions. Objects are integer-numbered per
type (`{no, ..., comment, params}`) and reached through SOAP Web Services; the Python client's package
layout *is* the object model.

Scale of the vocabulary: **16 member types** (beam, rigid, truss, truss-only-N, tension, compression,
buckling, cable, spring, definable stiffness, rib, result beam, four couplings), **6 surface stiffness
types** (standard, without membrane tension, membrane, rigid, without thickness, load transfer), **9
thickness types** (uniform, four variable variants, layers, shape orthotropy, stiffness matrix, thickness
phase), **12 member-load distributions** (uniform, uniform-total, trapezoidal, tapered, parabolic, varying,
varying-in-Z, and four concentrated forms), plus solids, imperfections, design situations, combination
wizards and result combinations.

Two details worth carrying forward:

- **Load direction encodes projection.** `LOAD_DIRECTION_GLOBAL_Z_OR_USER_DEFINED_W_TRUE` versus
  `..._W_PROJECTED` — true length (self-weight-like) versus projected (snow-like) is baked into the
  direction enum rather than being a separate flag. Local directions have no projected variant.
- **Global Z points *down* by default.** Set in Base Data; flipping to Z-up automatically applies a −1.0
  self-weight factor in Z. This is the single most common source of translator bugs against any Z-up format.

RFEM exports and imports **SAF**, IFC, DXF, DSTV, SDNF and Excel tables — and the tables mirror the object
model one-to-one.

- <https://github.com/dlubal-software/RFEM_Python_Client> (`RFEM/enums.py`)
- <https://www.dlubal.com/en/downloads-and-information/documents/online-manuals/rfem-6/000269> (member loads)
- <https://www.dlubal.com/en/support-and-learning/support/faq/005046> (axis systems)

### 1.6 SAF 2.2 — the benchmark

`FEMEX_Plates.md` already called SAF "FEMEX's closest peer". Having now compared all five programs, that
judgement is confirmed and worth strengthening: **SAF is the intersection these programs have already
agreed on**. It is an open, royalty-free, Excel-based format from the Nemetschek Group, managed by SCIA,
supported by SCIA, Archicad, ALLPLAN, RISA, FRILO, StruSoft, AxisVM, **Dlubal**, SOFiSTiK, ConSteel, IDEA
StatiCa, Prota and others — one worksheet per object type, one row per instance, one column per attribute.

Its design decisions are the ones worth measuring FEMEX against:

- **Identity is three-layered.** `Name` is required, human-readable, unique per sheet, and is *the*
  cross-sheet reference key (`StructuralCurveMember.Nodes = "N2; N3; N4"`). `Id` is an optional UUID whose
  documented purpose is round-tripping — so a receiving application recognises on re-import that this is the
  same object it exported, and merges rather than duplicates. `Parent ID` is an optional UUID pointing at
  the object this one was *derived from*, so that when a receiver segments a curved member into straight
  pieces, the curve can be reconstituted on the return trip.
- **The 6-DOF pattern is reused verbatim** across every support, hinge, release and link sheet:
  `ux, uy, uz, fix, fiy, fiz`, each `Free | Rigid | Flexible | …`, plus six matching stiffness columns.
- **Load direction is factored into three orthogonal columns**: `Coordinate system` (Global/Local) ×
  `Direction` (X/Y/Z/Vector) × `Location` (**Length | Projection**). Cleaner than RFEM's fused enum.
- **Sections are identified by provenance**: `Parametric` (shape + parameters), `Manufactured` (profile name
  + a CIS/2-derived **form code** to disambiguate across vendor libraries), `Compound`, or `General` —
  with optional explicit `A, Iy, Iz, It, Iw, Wply, Wplz`.
- **Materials are identified by grade** (`S235`, `C25/30`) with numeric properties *optional*; the receiver
  is expected to resolve the grade in its own library and fall back to the numbers only if it cannot.

- <https://www.saf.guide/> · <https://www.saf.guide/en/stable/getting-started/introduction.html>

---

## 2. Entity-by-entity mapping

**Legend:** ● direct match · ◐ present but lossy or under-specified · ○ absent from FEMEX

### 2.1 What FEMEX has, and how well it lands

| FEMEX concept | Robot | Revit | ETABS | RCB | RFEM | Verdict |
|---|---|---|---|---|---|---|
| `Level` (abs + rel elevation, `IsGround`) | Storeys (derived selection) | Level + `LevelAssociationData` | `$ STORIES` (ordered, heights) | Levels with R.L., master/slave | none (annotation only) | ● for ETABS/RCB, ◐ elsewhere |
| `Node` = (X, Y, level, offset) | `Number` + X, Y, Z | derived `AnalyticalNodeData` | `POINT` X, Y + story + DZ | per-level plan geometry | `Node` + 3 coords | ● ETABS/RCB, ◐ Robot/RFEM/Revit — see §5.1 |
| `Bar` (2 nodes, section, material, `RotationAngle`) | `Bar` + `Gamma` + labels | `AnalyticalMember` + `CrossSectionRotation` | `LINE` COLUMN/BEAM/BRACE | column/wall/beam objects | `Member.Beam` | ● core is universal |
| `Plate` (contour + regions + priority) | `RobotObjObject` + hosted openings | `AnalyticalPanel` + `AnalyticalOpening` | `AREA` FLOOR/PANEL + opening flag | slab zones + voids | `Surface` + `Opening` | ● **and better** — see §3.2 |
| `PlateRegionKind.Opening` | hosted opening object | `AnalyticalOpening` | area "opening" flag | void | `Opening` | ● |
| `PlateRegionKind.LoadOnly` | `I_LT_CLADDING` | — | area type `None` | **Area Load** (free polygon) | `TYPE_LOAD_TRANSFER` | ● four-way match |
| `PlateBehaviour` (Shell/Plate/Membrane/CompressionOnly) | thickness type | — | `eShellType` | — | `SurfaceType` (6) | ● |
| `SurfaceAlignment` + `SurfaceOffset` | panel attribs | geometric | `AREAASSIGN` offsets | — | `surfaceEccentricity` | ● matches SAF `System plane at` |
| `Material` (E, ν, γ, strength) | `IRobotMaterialData` | `StructuralAsset` | `$ MATERIAL PROPERTIES` | material table | `Material` | ◐ — missing α, type, grade; see §5.5 |
| `Section` (rect / circle / T) | catalogue, parametric **or** explicit A/I/J | `FamilySymbol` + 34 shapes | `FRAMESECTION` + shape/file | column/beam type tables | library or parametric | ◐ **badly** — see §4.4 |
| `SurfaceProperty` (constant only) | homogeneous **or** orthotropic | `Thickness` | slab/wall/deck + layered | slab types | 9 thickness types | ◐ |
| `Support` + `Restraint {Fixed, Stiffness?}` | `I_LT_SUPPORT` (KX…HZ, one-dir) | `BoundaryConditions` Point/Line/Area | `RESTRAINT` + springs | base fixity, piles, Winkler | nodal/line/surface support | ● the universal pattern |
| `Hinge` + `Release {Released, ResidualStiffness?}` | `I_LT_BAR_RELEASE` | `ReleaseConditions` | `SetReleases` + partial fixity | pin/fix walls & columns | `memberHinge`, `lineHinge` | ● |
| `LoadCase` + `LoadNature` | `IRobotCaseNature` + sub-nature | `LoadCase` + `LoadNature` | `LOADPATTERN TYPE` | numbered cases (≤40) | `action_category` | ● |
| `PointLoad` (Fx…Mz) | `I_LRT_NODE_FORCE` | `PointLoad` | `POINTLOAD` | point/moment load | `nodalLoad` | ● |
| `LinearLoad` | `I_LRT_BAR_UNIFORM` / `_TRAPEZOIDALE` | `LineLoad` | `LINELOAD` | line load | `memberLoad` (12 dists) | ◐ no direction — §4.2 |
| `AreaLoad` | `I_LRT_UNIFORM` / `_IN_CONTOUR` | `AreaLoad` | `AREALOAD` | pressure + area load | `surfaceLoad` | ◐ no direction — §4.2 |
| `TemperatureLoad` | `I_LRT_BAR_THERMAL` | — | `TEMP` | — | temperature load types | ◐ gradient has no axis |
| `Grid` / `Gridline` | — (not in `.str`) | grids | `$ GRIDS` | **deliberately none** | `gridlines` | ● annotation, correctly scoped |
| `FemexMesh` | mesh params | — | auto/manual mesh | auto mesh (exported to SLB) | FE mesh | ● correctly optional |

### 2.2 What the programs have that FEMEX does not

| Missing concept | Robot | Revit | ETABS | RCB | RFEM | SAF | Priority |
|---|---|---|---|---|---|---|---|
| **Load combination** | `CreateCombination` + `CaseFactors` | `LoadCombination` | `COMBO` | combination table | `loadCombination` | `StructuralLoadCombination` | **P0** |
| **Load direction / coord system** | `I_URV_PX/PY/PZ`, `_LOCAL_SYSTEM` | `OrientTo` | `Dir` 1–11 | directional loads | direction enums | `Direction` + `Coordinate system` | **P0** |
| **True-length vs projected** | `I_URV_PROJECTED` | `IsProjected` | `Dir` 7–9, 11 | — | `_TRUE` / `_PROJECTED` | `Location` | **P0** |
| **Self-weight** | `I_LRT_DEAD` | built-in | `SELFWEIGHT` multiplier | automatic | `self_weight` + `fx/fy/fz` | `Load type = Self weight` | **P0** |
| **I/H, channel, angle, box sections** | `I_BST_NS_I`, `_C`, `_BOX_*` | 34 shapes | `SHAPE` | arbitrary profiles | library + parametric | `Shape` enum | **P0** |
| **Catalogue section reference** | `LoadFromDBase` | `FamilySymbol` | `FILE`+`SHAPE` | type tables | library name | `Profile` + `Form code` | **P0** |
| **Explicit A / Iy / Iz / J** | `I_BSDV_AX`, `_IY`, … | — | — | — | computed | `A, Iy, Iz, It, Iw` | **P0** |
| **Schema version / producer** | — | — | `PROGRAM`/`VERSION` | file version | file version | `Model` sheet | **P0** |
| **Round-trip identity** | name-keyed labels | `UniqueId` | GUIDs on objects | — | integer `no` | `Id` + `Parent ID` | **P0** |
| **Rigid diaphragm** | rigid links | `AnalyticalLink` | `DIAPHRAGM` rigid/semi | wall groups (partial) | `rigidLink` diaphragm | — | **P0/P1** |
| **Rigid link / constraint** | `I_LT_NODE_RIGID_LINK` | `AnalyticalLink` | `ConstraintDef` | — | `rigidLink`, couplings | `RelConnectsRigidLink` | **P1** |
| **Bar end offset / insertion point** | `I_LT_BAR_OFFSET` | geometric | cardinal point + end offsets | beam vertical offset | `memberEccentricity` | `System line` + eccentricities | **P1** |
| **Bar behaviour (truss/tension/compression-only)** | `I_BTC_*`, `TrussBar` | — | tension/compression limits | — | 16 member types | `Behaviour in analysis` | **P1** |
| **Stiffness modifiers** | reduced-stiffness ortho | — | `AMOD/I2MOD/I3MOD/…` | — | `…StiffnessModification` | — | **P1** |
| **Thermal expansion α** | `LX` | `ThermalExpansionCoefficient` | `A` | material table | α | `Thermal expansion` | **P1** |
| **Material type / grade** | `I_MT_*` | `StructuralAssetClass` | `TYPE` | material table | `material_type` | `Type` + `Quality` | **P1** |
| **Support local axes** | — | `GetDegreesOfFreedomCoordinateSystem` | local restraints | — | local supports | `Coordinate system` | **P1** |
| **Elastic foundation (Winkler)** | `I_LT_BAR_ELASTIC_GROUND` | — | area springs | **piles / Winkler / SSI** | Cu / Cv | `C1x/y/z`, `C2x/y` | **P1** |
| **Variable / layered / orthotropic thickness** | `I_TT_ORTHOTROPIC` | — | ribbed, waffle, layered | — | 9 thickness types | `Thickness type` (8) | **P1** |
| **Load group / relation** | — | `LoadUsage` | — | — | `action` | `StructuralLoadGroup` | **P2** |
| **Tapered members** | `CreateNonstd(0/1)` | — | non-prismatic | — | distribution types | `StructuralCurveMemberVarying` | **P2** |
| **Mass source / load-to-mass** | `I_LRT_MASS_ACTIVATION` | — | Mass Source | — | mass load types | — | out of scope |

---

## 3. What FEMEX gets right

Worth stating explicitly, because the gap list below is long and should not be read as an indictment.

### 3.1 The container shape is correct

Flat, top-level, id-referenced collections with `type` discriminators on the polymorphic ones is exactly the
shape SAF uses (one worksheet per object type) and exactly the shape RFEM's object model and table export
use. The `referenced-before-referencer` declaration order in `FemexModel.cs` and the decision to put `Mesh`
last so it sorts last and vanishes when null are the kind of details that make a format pleasant to read by
hand. This was the right call and nothing in this research argues against it.

### 3.2 Plate regions with integer priority are better than the alternatives

This is FEMEX's strongest single design decision, and it beats the benchmark.

- SAF has `StructuralSurfaceMemberRegion` and `StructuralSurfaceMemberOpening` as separate sheets with **no
  precedence mechanism at all** — overlapping regions are undefined.
- ETABS and SAFE resolve precedence **implicitly by type** (`Opening > Stiff > Drop > Slab`), which means you
  cannot express "this drop panel wins over that one".
- RFEM makes openings child objects with no overlap semantics.
- Only RAM Concept has the integer priority that FEMEX adopted.

FEMEX's rule — highest `Priority` wins, base panel behaves as `int.MinValue`, ties broken
`Opening > LoadOnly > Structural`, further ties by list order — is total, deterministic and expressible.
Nothing found in this research suggests changing it.

### 3.3 `LoadOnly` is a genuine four-way match

`PlateRegionKind.LoadOnly` maps cleanly onto Robot's cladding (`I_LT_CLADDING` on a non-meshed object),
ETABS' area type `None`, RFEM's `TYPE_LOAD_TRANSFER` surface, and INDUCTA's zone-independent Area Load. Four
independent programs converged on "a surface that carries load but no stiffness" and FEMEX has it. That is
strong evidence the concept belongs in an essential subset.

### 3.4 The design-panel / generated-mesh split mirrors how these programs actually work

ETABS' object-versus-element distinction and RFEM's surface-versus-FE-mesh are the same idea. Keeping the
mesh optional, data-only, back-linked to the authored plate, and explicitly *not* generating it in FEMEX
matches what every one of these programs does internally. Sharing an element-id space between bars, plates
and mesh faces so `Hinge.ElementId` and `TemperatureLoad.ElementIds` can address any of them is a reasonable
simplification.

### 3.5 The 6-DOF boundary-condition pattern is the universal one

`Restraint { bool Fixed; double? Stiffness; }` and `Release { bool Released; double? ResidualStiffness; }`
are structurally identical to SAF's `Free | Rigid | Flexible` + stiffness column pair, RFEM's spring and
rotational-restraint arrays, ETABS' boolean array + partial-fixity values, and Robot's `SetFixed` + `KX…HZ`.
This pattern is reused for point, linear and area targets in FEMEX exactly as SAF reuses it across
`StructuralPointSupport` / `StructuralCurveConnection` / `StructuralEdgeConnection`. Correct, and correctly
factored.

### 3.6 Naming a plate edge by its two nodes rather than an edge index

`Hinge.EdgeStartNodeId` / `EdgeEndNodeId` survives inserting a vertex into the contour; SAF's
`StructuralEdgeConnection` uses a 1-based integer `Edge` index, which does not. FEMEX is more robust here.

### 3.7 Level-based nodes are a feature, not a bug, for the target domain

See §5.1 — this is the decision most obviously at odds with three of the five programs, and it is still
defensible.

### 3.8 Relative geometric tolerances

`max(1e-6 × bounding-box diagonal, 1e-9)`, shared between `FindNodeAt` and the validator so they cannot
disagree, sidesteps the millimetres-versus-metres trap that catches most formats. Good.

---

## 4. Blocking gaps (P0)

Six things all five programs require, that FEMEX cannot express, and that the receiver cannot infer.

### 4.1 There are no load combinations

FEMEX has `LoadCase` and `Load`, and stops there.

| Program | Mechanism |
|---|---|
| Robot | `Cases.CreateCombination(no, name, I_CBT_ULS/I_CBT_SLS, nature, I_CAT_COMB)` + `comb.CaseFactors.New(case, factor)` |
| Revit | `LoadCombination.Create(doc, name, Combination\|Envelope, Serviceability\|Ultimate)` + `SetComponents()` |
| ETABS | `$ LOAD COMBINATIONS` → `COMBO "UDCon1" TYPE "Linear Add"` then `COMBO … LOADCASE … SF 1.4`; OAPI `RespCombo.Add` + `SetCaseList` |
| RCB | user combination table, saveable as a default library, drives envelopes and design |
| RFEM | `loadCombination` with `items = [(load_case_no, factor, action_no)]` + `design_situation`; plus `resultCombination` |
| SAF | `StructuralLoadCombination`: `Category` (ULS/SLS/ALS/national standard), `Type`, and the indexed `Load case name #` / `Load factor #` / `Multiplier #` columns |

This is the single largest gap. A model transferred without combinations arrives with cases that cannot be
analysed to any code without the engineer re-entering every combination by hand — which is most of the setup
work the transfer was supposed to save. And unlike, say, a stiffness modifier, there is no default the
receiver could reasonably assume.

Worth noting the escape hatch every program provides: both SAF (`Category = According national standard`)
and Robot (code combinations generated from case natures and sub-natures) let you ship *unfactored* cases
and have the receiver's code engine generate the combinations. FEMEX already carries `LoadNature`, so it is
part-way to supporting that path — but it needs somewhere to say "use the code" versus "here are my explicit
factors", and it needs the explicit form regardless, because Robot's code combinations do not round-trip as
factor lists.

**Shape of the fix:** a `LoadCombination` entity — number, label, a limit-state enum (ULS/SLS/ALS), a
combination type (linear add / envelope / absolute add / SRSS, per ETABS' five and RFEM's set), and a list of
`(loadCaseNumber, factor)` terms. Optionally a flag for "generate per code" plus the code name. Additive; no
existing entity changes.

> **Closed by `FEMEX_LoadCombinations.md`:** built as described, minus the code-generation flag.
> `Loads/Combinations/` carries `LoadCombination` (`Number`, `Label`, `LimitState`,
> `CombinationType`, `IncludeInDesignEnvelope`, `Terms`), `LoadCombinationTerm`, and the `LimitState`
> / `LoadCombinationType` enums; `FemexModel.GetDesignEnvelope(limitState)` states the envelope rule
> once so a consumer cannot disagree with the format about it. Two gaps remain deliberately: a
> nested *envelope* combination is lost rather than flattened, because it envelopes a named subset
> and FEMEX has one envelope per limit state; and there is no "generate per code" mode, because the
> explicit factor form is needed regardless — as §4.1 says, Robot's code combinations do not
> round-trip as factor lists — and a second, non-round-tripping way to say the same thing has no
> reader yet.

### 4.2 Distributed loads have no direction

From `Loads/AreaLoad.cs`:

```csharp
/// <summary>
/// Magnitude of the pressure load (Force per unit area).
/// </summary>
public double Magnitude { get; set; }
```

and from `Loads/LinearLoad.cs`, `MagnitudeStart` / `MagnitudeEnd` / `MomentStart` / `MomentEnd` — all bare
scalars. `PointLoad` is fine (it has explicit `Fx, Fy, Fz, Mx, My, Mz`), which makes the inconsistency
sharper: a point load knows where it points and an area load does not.

Every program requires three orthogonal pieces of information:

1. **Direction** — a global axis, a local axis, or a vector.
2. **Coordinate system** — global or element-local. For a wall, "downward" and "normal to the surface" are
   entirely different loads.
3. **True-length versus projected** — whether a load per unit area is per real area or per plan-projected
   area. The difference between snow and dead load on a pitched roof.

| Program | How it says it |
|---|---|
| Robot | `I_URV_PX/PY/PZ` + `I_URV_LOCAL_SYSTEM` + `I_URV_PROJECTED` |
| Revit | `ForceVector1/2`, `OrientTo` (`LoadOrientationOption`), `IsProjected` |
| ETABS | `Dir` 1–11: 1–3 local, 4–6 global, 7–9 projected, 10 gravity, 11 projected gravity |
| RFEM | fused into the direction enum: `LOAD_DIRECTION_GLOBAL_Z_OR_USER_DEFINED_W_TRUE` vs `..._PROJECTED` |
| SAF | three separate columns: `Coordinate system` × `Direction` × `Location` (`Length \| Projection`) |

SAF's factoring is the cleanest and is the model to copy. `FEMEX_Plates_Summary.md` already lists this under
"Still open" — this review confirms it as blocking rather than cosmetic. A receiver reading
`"magnitude": 1.5` on a wall panel genuinely cannot tell whether that is self-weight of finishes acting down
or wind pressure acting normal to the wall.

**Shape of the fix:** a small shared value type — direction enum (`GlobalX/Y/Z`, `LocalX/Y/Z`, `Vector`),
optional vector components, and a `Projected` flag — on `AreaLoad` and `LinearLoad`. This *is* a breaking
change to existing files unless a default is chosen (global −Z, true length, which is what
`Examples/Example1.femex` means today), so it needs the version field from §4.5 to land alongside it.

### 4.3 There is no self-weight

`Material.UnitWeight` exists ("Weight per unit volume (γ) - e.g., kN/m³"), and nothing anywhere states
whether gravity is applied, in which direction, with what factor, or into which load case.

| Program | Mechanism |
|---|---|
| Robot | `I_LRT_DEAD` load record with `I_DRV_X/Y/Z` direction and `I_DRV_COEFF` |
| Revit | built into the analysis, driven by material density |
| ETABS | `LOADPATTERN "DEAD" TYPE "Dead" SELFWEIGHT 1` — a per-pattern multiplier |
| RCB | automatic from geometry and materials |
| RFEM | per-load-case `self_weight` active flag with `fx, fy, fz` factors |
| SAF | `StructuralLoadCase.Load type = Self weight` |

The failure mode is not a missing feature, it is a **silent wrong answer**: a model round-tripped through
FEMEX either loses its self-weight entirely or gains it twice, and nothing in the file reveals which. Every
one of the five programs would produce a different result on import.

**Shape of the fix:** a `SelfWeight` block or flag on `LoadCase` — active, direction, factor. Smallest of the
P0 items; roughly ETABS' and RFEM's shape.

### 4.4 Sections cannot describe most real members

`Section` has exactly three concrete types: `Rectangle` (width, depth), `Circle` (diameter) and `TSection`
(flange width, flange thickness, web thickness, total depth). There is no I or H section. Not a
non-standard one, not a catalogue one, none at all.

This has three separate consequences:

1. **No steel.** All five programs are steel-capable; Robot, ETABS and RFEM ship section databases as a core
   feature. A steel frame cannot cross FEMEX at all today. Even staying purely in concrete, L-shapes, boxes
   and hollow sections are missing.
2. **No catalogue identity.** This is how these programs actually name sections — Robot's
   `secData.LoadFromDBase("HEB180")` against a `Preferences.SetCurrentDatabase` selection, ETABS'
   `FILE "AISC15.xml" SHAPE "W12X26"`, RFEM's library key, SAF's `Profile` + a CIS/2-derived **form code**
   specifically to disambiguate the same profile name across vendor libraries. A format that can only carry
   dimensions forces every exporter to resolve a catalogue name into numbers and every importer to try to
   match numbers back to a name — lossy in both directions, and Robot's `SHSH` vs `SHSC` hot/cold-formed
   naming problem is a documented example of exactly this failing.
3. **No numeric escape hatch.** Robot exposes `I_BSDV_AX`, `I_BSDV_IX`, `I_BSDV_IY`, `I_BSDV_IZ` and friends;
   SAF has optional `A, Iy, Iz, It, Iw, Wply, Wplz`. This is the mechanism that lets an **unrecognised**
   section survive: even if the receiver has never heard of the shape, it can build a member with the right
   stiffness. Without it, anything FEMEX does not have a shape class for is simply lost.

Of the three, the escape hatch is the highest-value single addition — it makes the format *degrade
gracefully* instead of failing, and it is a strictly additive `Section` subtype.

**Shape of the fix:** three additions to the existing `Section` hierarchy — more parametric shapes (I/H,
channel, angle, box, hollow), a `catalogue` type (`standard` + `profile` + optional form code), and a
`numeric` type (A, Iy, Iz, J, and optionally shear areas and section moduli). The existing discriminated
union absorbs all three without disturbing what is there.

### 4.5 There is no schema version and no producer metadata

The root `FemexModel` has `Units`, then goes straight into geometry. No `schemaVersion`, no format
identifier, no producing application, no project name, no date. Only `FemexMesh.Generator` and
`FemexMesh.GeneratedAt` exist, and only inside the mesh block.

The docs already flag the failure mode, and it has already happened once. From `FEMEX_Plates.md`:

> "The old file's `plates[].thickness` is **silently dropped** on deserialize (no `UnmappedMemberHandling` is
> set), so an un-migrated model loads clean but semantically empty."

That is the worst possible failure for an interchange format: no error, no warning, a model that validates
and is wrong. For a single-application format it is survivable. For a format whose entire purpose is being
written by one program and read by another — possibly a different version, possibly years later — it is
disqualifying. ETABS stamps `PROGRAM "ETABS" VERSION "21.0.0"` as the first line of every `.e2k`; SAF
devotes a whole `Model` worksheet to project and model specifications.

Note this interacts with §4.2: adding direction to loads changes the meaning of existing files, and without a
version field there is no safe way to tell an old file from a new one.

**Shape of the fix:** a version and metadata block at the root — schema version, optionally producer,
producing version, project name, timestamp — plus setting `UnmappedMemberHandling` so unknown members are an
error rather than silence. Trivially additive; should land first, because the other changes depend on it.

### 4.6 Identity: GUID as the unique id, label as the optional name

Today every entity is keyed by an authored integer, with `Name`/`Label` optional and unused as a reference
(`Material.Name` is `string?`, `LoadCase.Label` is `string?`, `Section.Name` is `string?`). The direction
chosen for this review is **GUID as the unique id, with a label as an optional human name**. Assessing that
against what these programs actually key on:

**What GUID-primary buys:**

- **Round-trip identity**, which is the real prize. This is the documented purpose of SAF's optional `Id`
  column: so the receiving application recognises on re-import that an object is the same one it exported,
  and *merges* rather than duplicating. Revit is GUID-native (`UniqueId`); IFC mandates `GlobalId`; ETABS
  carries GUIDs on objects (`FrameObj.SetGUID`). Integer ids cannot do this — nothing stops two programs
  minting id 1 for different things.
- **Merging models without renumbering.** Combining two FEMEX files today requires an id remap on every
  reference; with GUIDs it is concatenation.
- **Stability under editing.** An integer id is a position-like thing that tempts renumbering; a GUID is not.

**What it costs:**

- **Readability.** `"startNodeId": 101` becomes a 36-character string. `Examples/Example1.femex` is
  hand-authored and hand-migrated — the plate migration in `FEMEX_Plates.md` involved keeping 44 mesh faces
  at their original ids so a temperature load survived byte-identical. That kind of hand surgery gets
  materially harder.
- **Ordering.** Integer ids sort meaningfully; GUIDs do not. Node 1–100 on level 1 is a readable convention.
- **Size.** Every reference grows roughly 5×; `Example1.femex` is already 97 KB with ~100 nodes.
- **Churn.** Every one of the 85 tests, the whole example file, and `NextNodeNumber()` /
  `GetOrAddNode()` / all validation duplicate-id checks are affected.
- **It does not solve the Robot problem.** Robot's properties are keyed by *name* —
  `Labels.StoreWithName(label, "Fixed")`, and the name is the foreign key. A GUID does not help an exporter
  targeting Robot; a required, stable label does. Same for ETABS, where section and story names are the key.

**The observation worth acting on:** SAF, which had exactly this decision to make and had every vendor in the
room, chose **neither** exclusively. Its reference key is `Name` — required, human-readable, unique per
sheet. Its `Id` GUID is *optional* and exists solely for round-trip matching. Its `Parent ID` GUID handles
derived objects. That is three layers doing three different jobs, and it is not an accident.

The nearest equivalent for FEMEX would be to keep the integer id as the in-file reference (it is what every
`Bar.StartNodeId`, `Plate.NodeIds`, `AreaLoad.PlateId` already uses, and it is what RFEM does too), make
`Name` **required** on the property entities that other programs key by name (`Section`, `SurfaceProperty`,
`Material`, `LoadCase`), and add an **optional** GUID for round-trip identity. That gets the round-tripping
benefit and the Robot/ETABS name-key compatibility without rewriting every reference in the format.

If GUID-as-primary is preferred regardless, the changes it forces are listed above and are real but
mechanical; the point to be clear about is that it delivers round-trip identity and *not* Robot/ETABS
compatibility, which still needs required names alongside it.

---

## 5. Important gaps (P1)

Needed for a faithful transfer, but either inferable, degradable, or narrower in reach than the P0 list.

### 5.1 Rigid diaphragms — the strongest P1 candidate for promotion

FEMEX is a level-based building format. It has `Level`, `IsGround`, storey elevations, and grids. What it
does not have is any way to say that a floor acts as a rigid diaphragm — or any rigid link or constraint at
all.

- ETABS: `$ DIAPHRAGM NAMES`, named, rigid or semi-rigid, assigned to points and areas. This is *the*
  defining concept of an ETABS lateral model.
- RFEM: `rigidLink` with a diaphragm type.
- Robot: `Structure.Nodes.RigidLinks.Set(master, slaves, label)` with per-DOF booleans.
- Revit: `AnalyticalLink` + `AnalyticalLinkType`.
- RCB: wall groups serve a related purpose for cores, though not the same one.

For a gravity-only transfer this is ignorable. For any lateral model it is not, and a level-based format that
cannot express a rigid floor is a level-based format that cannot describe the thing levels exist for. There
is a defensible argument that this belongs in §4 rather than here; it sits at P1 only because the P0 items
break *every* transfer while this one breaks lateral transfers.

### 5.2 Bar end offsets, rigid ends and insertion points

FEMEX gives plates `SurfaceAlignment` (Bottom/Centre/Top) and `SurfaceOffset`, carefully documented as
measured along the plate normal. Bars get `RotationAngle` and nothing else.

- Robot: `I_LT_BAR_OFFSET` with `Start.UX/UY/UZ` and `End.UX/UY/UZ`.
- ETABS: `SetInsertionPoint` with cardinal points 1–11 plus two 3-component joint offsets plus
  `StiffTransform`; separately `SetEndLengthOffset(Length1, Length2, RZ)` with a rigid-zone factor.
- RFEM: `memberEccentricity`, either absolute `ex/ey/ez` per end or relative alignment enums referenced to
  the section, the connected surface thickness, or another member's section.
- SAF: `System line` (nine positions) plus *two* eccentricity families — `Structural` (the BIM offset) and
  `Analysis` (the offset actually applied in the FE model).
- RCB: user-defined vertical offset on beams.

The asymmetry is the tell: FEMEX already accepted that a surface needs to say where its mid-plane sits
relative to its reference geometry. A beam under a slab has exactly the same problem, and it is FEMEX's core
use case. SAF's structural-versus-analysis split is the most honest model of the three and worth copying if
this is ever addressed.

### 5.3 Bar behaviour

`PlateBehaviour` has `CompressionOnly`. Bars have no equivalent — no truss, no tension-only, no
compression-only.

Robot has `TrussBar` and `I_BTC_TENSION_ONLY` / `I_BTC_COMPRESSION_ONLY`; RFEM has sixteen member types;
SAF has `Behaviour in analysis` ∈ `Standard | Axial force only | Compression only | Tension only`; ETABS has
tension/compression limits. Bracing that should be tension-only silently becomes a full beam on import,
which is a wrong answer rather than a missing one. SAF's four-value enum is the right size — RFEM's sixteen
types are well beyond an essential subset.

### 5.4 Stiffness modifiers

ETABS `AMOD / A2MOD / A3MOD / JMOD / I2MOD / I3MOD / MMOD / WMOD`; RFEM `memberStiffnessModification` and
`surfaceStiffnessModification`; Robot encodes them into the reduced-stiffness orthotropy slots. Cracked-
section factors are near-universal in concrete building design, and the ETABS community documents them as
*the* classic silently-dropped item — the reason two programs give different answers for the "same" model.
SAF does not carry them either, which is a mark against SAF rather than an argument for omitting them.

### 5.5 Material is under-specified

```csharp
public class Material {
    public int Id { get; set; }
    public string? Name { get; set; }
    public double ModulusOfElasticity { get; set; }
    public double PoissonsRatio { get; set; }
    public double UnitWeight { get; set; }
    public double Strength { get; set; }
}
```

Three things missing:

- **Thermal expansion coefficient α.** FEMEX has a `TemperatureLoad` with a `DeltaT`. Without α the receiver
  cannot compute the resulting strain and has to substitute its own default — so the thermal load transfers
  as a number whose effect is unpredictable. This is an internal inconsistency, not just an omission. Robot
  has `LX`, Revit `ThermalExpansionCoefficient`, ETABS `A`, RFEM α, SAF `Thermal expansion [1/K]`.
- **Material type.** Robot `I_MT_STEEL/CONCRETE/ALUMINIUM/TIMBER`, Revit `StructuralAssetClass`, ETABS
  `TYPE "Concrete"`, RFEM `material_type`, SAF `Type`. Design behaviour, and often which library to search,
  depends on it.
- **Grade / quality string.** SAF's design point is worth quoting: material is identified by grade from a
  standard (`S235`, `C25/30`) with numeric properties *optional*, and the receiver resolves the grade in its
  own library, falling back to explicit numbers only if it cannot. FEMEX's optional `Name` is doing this job
  informally with no convention behind it.

Also worth noting a units trap in this area: Robot's `RO` is **weight** density and Revit's `Density` is
**mass** density. FEMEX's `UnitWeight` is weight density (matching Robot), which is fine — but nothing in the
format says so, and the distinction is a factor of g.

### 5.6 Supports have no local axes

`Support` carries `Restraint Ux, Uy, Uz, Rx, Ry, Rz` with no coordinate system, so every restraint is
implicitly global. SAF has `Coordinate system = Global | Local` on every support sheet; RFEM supports local
nodal supports; Revit exposes `GetDegreesOfFreedomCoordinateSystem()` (origin plus rotation); ETABS supports
local restraints. Inclined supports — a bearing on a slope, a raking prop — cannot be expressed. Narrower in
reach than the other P1 items, but genuinely unrepresentable rather than merely lossy.

### 5.7 Surface properties and elastic foundations

`SurfaceProperty` has one concrete type, `ConstantThickness`. The doc comment reserves `"variable"` and
`"layered"` discriminators but neither exists. RFEM has nine thickness types, SAF eight
(`Constant`, four `Variable in …`, `Variable in direction XY`, `Variable radially`), Robot has
`I_TT_HOMOGENEOUS` / `I_TT_ORTHOTROPIC` with three sub-types, ETABS has ribbed, waffle and layered. `FEMEX_
Plates.md` deliberately rejected SAF's `Orthotropic` behaviour on the grounds that orthotropy belongs on the
surface property rather than the behaviour enum — which is the right call, but the surface property side of
that decision was never built.

Separately, **elastic foundations**. RFEM's `surfaceSupport` carries Winkler `Cu,x / Cu,y / Cu,z` plus
Pasternak `Cv,xz / Cv,yz`; SAF's `StructuralSurfaceConnection` carries `C1x / C1y / C1z` and `C2x / C2y` —
near-identical, which is a strong signal the formulation is settled. RCB's whole foundation story (piles,
Winkler springs, full soil-structure interaction, ground slabs) lives here. FEMEX's `Support` with
`SupportTarget.Area` and `Restraint.Stiffness` is *close*, but the semantics are undefined: it is not stated
whether that stiffness is a total spring or a bedding modulus per unit area, and those differ by the plate
area. Worth resolving even if nothing else in this section is.

### 5.8 Temperature loads are under-specified

`TemperatureLoad.GradientPerDepth` is "difference per unit depth" with **no axis** — nothing says which face
is hotter. SAF's `StructuralSurfaceActionThermal` names `TempT` (top) and `TempB` (bottom, LCS −z)
explicitly; `StructuralCurveActionThermal` names top/bottom/left/right fibres. A sign convention referenced
to the local axes is the minimum needed to make the value mean anything.

### 5.9 Units are unvalidated free text

```csharp
public class Units {
    public string? Length { get; set; }   // "m", "mm", "ft"
    public string? Force  { get; set; }   // "kN", "N", "kip"
}
```

Both optional, both arbitrary strings, nothing converts or checks anything against them, and there is no
temperature, angle or mass unit — despite `TemperatureLoad` carrying degrees and `RotationAngle` /
`LocalAxisAngle` / `Grid.RotationAngle` carrying degrees by comment-only convention. SAF declares a unit
system in its `Model` sheet and additionally permits units in column headers (`Coordinate X [m]`,
`Value [kN/m2]`, `TempT [°C]`).

The mitigating factor is real: FEMEX's tolerances are relative to the bounding-box diagonal, so geometry
works in any consistent unit. But loads and material properties are not tolerance-relative, and
`"modulusOfElasticity": 33000000` in `Example1.femex` is only interpretable if you already know the file's
convention. Making these enums, adding temperature and angle, and requiring them is a small change with
disproportionate value.

---

## 6. Deliberately out of scope

Listing these so the gap analysis is not mistaken for a to-do list. FEMEX should **not** chase:

- **Solids and 3D volumes** — RFEM only; SAF and IFC-structural have none either.
- **Curved geometry** — NURBS, splines, arcs, circles as first-class edges (RFEM, SAF). FEMEX's "curves as
  chords" decision is the right simplification for an essential subset.
- **Imperfections** — RFEM's imperfection cases, initial sway, buckling-mode imperfections. SAF omits them.
- **Solver and analysis settings** — RFEM's static analysis type (linear / P-Δ / large deformation),
  iteration methods, convergence criteria; Robot's `CalcEngine.AnalysisParams`. SAF transports none of this
  and is right not to.
- **Code-combination engines** — Robot's regulation-driven combinations, RFEM's combination wizard and
  design situations, ETABS' auto lateral load patterns. Carry the *result* (explicit combinations) or a
  reference to the code, not the engine.
- **Design parameters and reinforcement** — Robot's `I_LT_MEMBER_TYPE`, ETABS' pier/spandrel design output,
  RCB's entire design side, rebar and cover data.
- **Results** — beyond FEMEX's existing sensible refusal. SAF carries two thin result sheets; IFC defines
  results and nobody implements them.
- **Auto wind and seismic generators** — ETABS auto-lateral patterns, RCB's AS/NZS 1170.2 calculator with
  crosswind and dynamic response factors, RFEM's wind simulation. These are code engines, not data.
- **Meshing options** — Robot's `RobotMeshParams` (Delaunay/Coons, element size), ETABS' cookie-cut and
  auto-line-constraint options, RCB's refinement settings. FEMEX correctly stores a mesh as data and refuses
  to generate one; it should equally refuse to store the recipe.
- **Mass source and load-to-mass conversion** — ETABS Mass Source, Robot `I_LRT_MASS_ACTIVATION`. Needed only
  for dynamics, which is beyond a basic transfer.
- **Program-specific constructs** — Robot claddings and one-way/two-way spanning labels, ETABS pier/spandrel
  labels and cardinal-point stiffness transforms, RCB wall groups and master/slave levels, RFEM result beams
  and couplings and rib effective widths, Revit physical-analytical association / `AnalyzeAs` /
  `StructuralRole` / phases / worksets.

---

## 7. Recommendation

### 7.1 Direct answer

**Is FEMEX the optimal file format for the basic data from these five programs?**

The *format* — JSON, flat id-referenced collections, discriminated unions, a design/mesh split — is a good
choice and needs no rework. On the plate model specifically FEMEX is ahead of SAF. On levels and nodes it is
a better fit for ETABS and RCB than SAF or IFC would be.

The *schema* is not yet sufficient. Six things (§4) mean that a model crossing between any two of these
programs today loses information the receiver cannot reconstruct, and in two cases (self-weight, load
direction) produces a confidently wrong answer rather than an obviously incomplete one.

The most useful reframing: **FEMEX is roughly 70% of the way to the intersection these vendors have already
agreed on in SAF, with a better plate model and a worse section model.** Closing §4 would put it at parity
for the essential subset. Closing §5 would make it genuinely competitive for building work.

### 7.2 Ranked, in dependency order

| # | Change | Size | Touches |
|---|---|---|---|
| 1 | **Schema version + producer metadata**, and set `UnmappedMemberHandling` | XS | `FemexModel.cs`, round-trip tests, `Example1.femex` header |
| 2 | **`LoadCombination` entity** | S | new file, `FemexModel.cs` list, validation (case references, self-reference), new tests |
| 3 | **Self-weight on `LoadCase`** | XS | `LoadCase.cs`, validation, `Example1.femex` |
| 4 | **Load direction + coordinate system + projected flag** | M | `AreaLoad.cs`, `LinearLoad.cs`, shared value type, validation, `Example1.femex` (8 area + linear loads), round-trip tests — **breaking; must follow #1** |
| 5 | **Section escape hatch** (explicit A/Iy/Iz/J subtype) | S | `Geometry/Sections/`, validation, tests |
| 6 | **Section shapes** (I/H, channel, angle, box, hollow) + **catalogue subtype** | M | `Geometry/Sections/`, validation, tests |
| 7 | **Material completeness** (α, type enum, grade string) | S | `Material.cs`, `Example1.femex`, tests |
| 8 | **Units as enums**, plus temperature and angle | S | `Units.cs`, validation, `Example1.femex` |
| 9 | **Optional GUID identity** (or GUID-primary — see §4.6 for the cost of each) | S or L | all entities; L if GUID-primary, since every reference, the example file and the id-related validation change |
| 10 | **Rigid diaphragm / rigid link** | M | new entity, validation, tests |
| 11 | **Bar end offsets** | S | `Bar.cs`, validation |
| 12 | **Bar behaviour enum** (SAF's four values) | XS | `Bar.cs` |
| 13 | **Stiffness modifiers** | S | `Bar.cs`, `Plate.cs` or the property entities |
| 14 | **Support local axes** | S | `Support.cs`, validation |
| 15 | **Surface bedding semantics** (define `Restraint.Stiffness` for area targets) | XS | documentation + validation, possibly no schema change |
| 16 | **Temperature gradient axis** | XS | `TemperatureLoad.cs` |

Items 1–3 are additive and non-breaking and could land together. Item 4 is the first that changes the meaning
of existing files and is the reason item 1 comes first.

### 7.3 Still open

- **Whether §5.1 (rigid diaphragms) belongs in P0.** For a level-based building format, arguably yes. Left at
  P1 because gravity-only transfers survive without it.
- **Whether the level-based node survives contact with Robot and RFEM.** The decision to keep
  `Node = (X, Y, LevelNumber, VerticalOffset)` is confirmed for this review, and it is genuinely the right
  shape for ETABS and RCB. The open cost is that an importer from Robot, RFEM, Revit or SAF must synthesise a
  `Level` for every distinct elevation it encounters — including for members that have no storey meaning at
  all (a truss diagonal, a raking column, a ramp, a bridge deck). Nothing about that is unimplementable; it
  is a real, recurring translation cost that should be measured against a real Robot or RFEM model before the
  decision is considered settled.
- **Whether the `Section` numeric escape hatch should also exist for `SurfaceProperty`** — the equivalent
  would be an explicit stiffness matrix, which RFEM has (`TYPE_STIFFNESS_MATRIX`) and SAF does not.
- **Whether `Bar` should reference a section *and* a material independently**, as it does now, or whether the
  section should carry the material. Robot does both — `IRobotBarSectionData.MaterialName` *and* a bar
  material label — and reading a Robot model means reconciling the pair. FEMEX's independent references are
  cleaner; the open question is what an exporter does when Robot's two disagree.
- **Whether `LoadNature` needs a sub-nature.** Robot's `SetNatureExt(int)` sub-nature is what drives its code
  combination factors; SAF's `StructuralLoadGroup` (`Load group type` + `Relation` + `Load type`) plays a
  similar role. If FEMEX ever supports "generate combinations per code", it needs one of these.
- **Nothing here has been tested against a real exported file.** Every claim about FEMEX is verified against
  this repository. The external claims are verified against vendor documentation and, for Robot, a large
  open-source consumer — but the ETABS `.e2k` grammar and essentially all of INDUCTA RCB are reconstruction,
  and the honest next step before implementing anything is to export one real model from ETABS and one from
  Robot or RFEM and check this analysis against them.

---

## Sources

**FEMEX** — verified directly in this repository: `FemexModel.cs`, `FemexModel.Validation.cs`,
`Units.cs`, `Geometry/Node.cs`, `Geometry/Level.cs`, `Geometry/Bar.cs`, `Geometry/Plate.cs`,
`Geometry/PlateRegion.cs`, `Geometry/Sections/*`, `Geometry/Surfaces/*`, `Materials/Material.cs`,
`Loads/*`, `BoundaryConditions/*`, `Mesh/*`, `Examples/Example1.femex`, and the design docs in this folder.

**Robot** — [BHoM Robot_Toolkit](https://github.com/BHoM/Robot_Toolkit) ·
[Robot API Getting Started](https://forums.autodesk.com/autodesk/attachments/autodesk/robot-structural-analysis-forum-en/1707/1/Getting%20Started%20Guide%20Robot%20API.pdf) ·
[`.str` status](https://forums.autodesk.com/t5/robot-structural-analysis-forum/str-reference-manual/td-p/3191542) ·
[SCIA Robot text import](https://www.scia.net/en/support/addons/robot-text-import-scia-engineer) ·
[automatic combinations](https://www.autodesk.com/support/technical/article/caas/sfdcarticles/sfdcarticles/How-to-automate-load-combinations-generation-in-Robot-Structural-Analysis.html)

**Revit** — [2023 API changes](https://www.revitapidocs.com/2023/news?section=toc3) ·
[AnalyticalMember](https://www.revitapidocs.com/2023/67d7cab9-7549-2a32-7f40-28172e56885f.htm) ·
[AnalyticalPanel](https://www.revitapidocs.com/2024/52825e43-5e98-d848-b6dc-32cce704c4fa.htm) ·
[BoundaryConditions](https://www.revitapidocs.com/2025/58a98f0e-e2e5-4c8b-bea1-8228b30f1685.htm) ·
[StructuralAsset](https://www.revitapidocs.com/2015/dd81dd32-5167-647a-171e-cc376d75b62e.htm) ·
[Analytical Model dev guide](https://help.autodesk.com/cloudhelp/2024/ENU/Revit-API/files/Revit_API_Developers_Guide/Discipline_Specific_Functionality/Structural_Engineering/Revit_API_Revit_API_Developers_Guide_Discipline_Specific_Functionality_Structural_Engineering_Analytical_Model_html.html)

**ETABS** — [cSapModel](https://docs.csiamerica.com/help-files/etabs-api-2016/html/15293a27-a035-e64b-a4b4-78356479aa98.htm) ·
[spreadsheet geometry](https://docs.csiamerica.com/help-files/etabs/Keyboard_Commands_and_Special_Features/Editing_ETABS_Geometry_Using_a_Spreadsheet.htm) ·
[SetReleases](https://docs.csiamerica.com/help-files/etabs-api-2015/html/001b9395-8174-fc49-30fc-a7968db308e1.htm) ·
[SetInsertionPoint](https://docs.csiamerica.com/help-files/etabs-api-2015/html/2d10b1f6-3681-9816-51df-cbcfebb26364.htm) ·
[diaphragms](https://web.wiki.csiamerica.com/wiki/spaces/etabs/pages/1474609/Rigid+vs.+Semi-rigid+diaphragm) ·
[Mass Source](https://docs.csiamerica.com/help-files/etabs/Menus/Define/Mass_Source.htm) ·
[SCIA ETABS interface](https://help.scia.net/22.0/en/data_transfer/etabs/etabs.htm) ·
[Karamba3D e2k export](https://manual.karamba3d.com/beta/3-in-depth-component-reference/3.7-export/3.8.5-export-model-to-etabs-.e2k)

**INDUCTA** — [RCB](https://www.inducta.com.au/RCB_main.html) ·
[area loads](https://inducta.com.au/RCB_arealoads.html) ·
[RCBLink](https://www.inducta.com.au/RCBLink/RCBLink.html) ·
[RCB modification history](https://www.inducta.com.au/b6/ModificationHistory/RCBuilding_ModificationHistory.txt) ·
[SLABS modification history](https://www.inducta.com.au/b6/ModificationHistory/SLABS_ModificationHistory.txt)

**RFEM** — [Python client](https://github.com/dlubal-software/RFEM_Python_Client) ·
[member loads](https://www.dlubal.com/en/downloads-and-information/documents/online-manuals/rfem-6/000269) ·
[member types](https://www.dlubal.com/en/solutions/online-services/structural-analysis-wiki/000016) ·
[member eccentricities](https://www.dlubal.com/en/downloads-and-information/documents/online-manuals/rfem-6/000052) ·
[axis systems / Z direction](https://www.dlubal.com/en/support-and-learning/support/faq/005046) ·
[interfaces — export](https://www.dlubal.com/en/downloads-and-information/documents/online-manuals/rfem-6-interfaces/004027)

**SAF** — [spec home](https://www.saf.guide/) ·
[introduction: IDs, coordinate systems, units](https://www.saf.guide/en/stable/getting-started/introduction.html) ·
[StructuralCurveMember](https://www.saf.guide/en/stable/structural-analysis-elements/structuralcurvemember.html) ·
[StructuralSurfaceMember](https://www.saf.guide/en/stable/structural-analysis-elements/structuralsurfacemember.html) ·
[StructuralCrossSection](https://www.saf.guide/en/stable/structural-analysis-elements/structuralcrosssection.html) ·
[StructuralMaterial](https://www.saf.guide/en/stable/structural-analysis-elements/structuralmaterial.html) ·
[StructuralLoadCombination](https://www.saf.guide/en/stable/loads/structuralloadcombination.html) ·
[StructuralCurveAction](https://www.saf.guide/en/stable/loads/structuralcurveaction.html) ·
[StructuralSurfaceConnection](https://www.saf.guide/en/stable/supports-and-hinges/structuralsurfaceconnection.html)

**Context** — [IFC 4.3 structural analysis domain](https://ifc43-docs.standards.buildingsmart.org/IFC/RELEASE/IFC4x3/HTML/ifcstructuralanalysisdomain/content.html) ·
[SDNF](https://www.structuralwiki.org/en/SDNF) ·
[SDNF vs CIS/2 comparison](https://engineering.purdue.edu/~frosch/ftp/Talbott/11%20-%20References/files/ASCE%20Structures%20Congress%202004/PDFs/2st04-3376.pdf)
