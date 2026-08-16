# FEMEX Interoperability — Status, 16 August 2026

*What is left before FEMEX can realistically move a model between Autodesk Robot, the Revit
analytical model, CSI ETABS, INDUCTA RCB and Dlubal RFEM 6.*

> **Scope.** A status note, not a new assessment. It measures the current repository against
> `FEMEX_Assessment.md` (the brief) and `FEMEX_Interop_Review.md` (the resulting review), both
> written at commit `ce86991`, 2026-08-10. Every claim about FEMEX below was checked against the
> source in this repository; no claim about an external program is restated here that the review has
> not already sourced.

---

## 0. Where things stand

The review's headline was **"the container is right, the vocabulary is not yet complete"**, and it
named six blocking gaps. **Four of the six are closed.** Five commits have landed since the review,
each closing one of them; the schema went from unversioned to `1.3` and the suite from 85 facts to
211.

What remains splits into three quite different kinds of work, and conflating them is the main risk
in reading the review as a to-do list:

1. **Two P0 items were never done** — sections, and the half of the metadata gap that is not
   `schemaVersion`. These are the ones that still make a transfer lossy in a way the receiver cannot
   detect.
2. **All of §5 (P1) is untouched** — nine items, none started. These make a transfer *faithful*
   rather than merely possible, and diaphragms in particular decide whether FEMEX can carry a
   lateral model at all.
3. **There is no connector code of any kind.** The review was scoped to the schema and so never said
   this, but it is the largest single item between here and a working transfer.

---

## 1. Closed since the review

| Review § | Item | Landed as | Schema |
| --- | --- | --- | --- |
| §4.1 | Load combinations | `Loads/Combinations/{LoadCombination,LoadCombinationTerm,LimitState,LoadCombinationType}.cs`, `FemexModel.LoadCombinations.cs` | 1.0 |
| §4.2 | Load direction, coordinate system, projected flag | `Loads/DistributedLoad.cs` (`CoordinateSystem`, `Direction`, `Dx/Dy/Dz`, `Projected`), `LoadDirection.cs`, `LoadCoordinateSystem.cs`, `FemexModel.LocalAxes.cs` | 1.1 |
| §4.3 | Self-weight | `Gravity.cs`, `LoadCase.SelfWeightFactor`, `Material.Density` replacing γ, `FemexModel.SelfWeight.cs` | 1.2 |
| §4.6 | Round-trip identity | `IIdentified.Uid` on 13 entity families, `FemexModel.Identity.cs`, `Load.Id` | 1.3 |
| §4.5 *(half)* | Schema version | `FemexModel.SchemaVersion`, `CurrentSchemaVersion`, `ReadableSchemaVersions`, `ValidateSchemaVersion` | 1.1 |

Each carries its own "still open" list in the matching `FEMEX_*_Summary.md`; those residuals are
deliberate and are not repeated here.

---

## 2. Still blocking

### 2.1 Sections (review §4.4) — the largest remaining hole

`Geometry/Sections/` is unchanged since the review: `Rectangle` (width, depth), `Circle` (diameter),
`TSection` (flange width, flange thickness, web thickness, total depth). The base `Section` carries
`Id`, `Uid`, `Name` and one abstract `CalculateArea()` — the only derived section quantity anywhere
in the repository. There is no second moment of area, no torsion constant, no shear area and no
section modulus in any file.

All three of the review's consequences therefore still hold in full:

- **No steel.** No I/H, channel, angle, box or hollow shape exists. A steel frame cannot cross FEMEX
  at all.
- **No catalogue identity.** `Name` is a free label validated only for duplicates; there is no
  standard, profile designation or form code. This is how Robot, ETABS, RFEM and SAF all actually
  name a section.
- **No numeric escape hatch.** Nothing lets an unrecognised shape round-trip by its stiffness, so
  anything FEMEX has no class for is lost rather than degraded.

### 2.2 Metadata is half done (review §4.5)

`schemaVersion` landed with the load-direction change and does its job — `ToJson()` stamps it,
`ValidateSchemaVersion` warns on a null or unrecognised one, and `Example1.femex` opens with
`"schemaVersion": "1.3"`. The rest of §4.5 did not land:

- **No producer, producing version, project name or timestamp** at the root. The precedent already
  exists one level down: `FemexMesh` has `Generator` and `GeneratedAt`. The root has neither.
- **`UnmappedMemberHandling` is still not set.** `FemexModel.cs:110-120` configures camelCase,
  indenting, ignore-nulls and the enum converter, and nothing else. Unknown JSON members are dropped
  in silence — the failure mode review §4.5 calls disqualifying, and the one
  `FEMEX_Identity_Summary.md` flags against itself: a 1.3 file read by a 1.2 build loses its uids
  without a word.

---

## 3. Still open — all of §5

None of the nine P1 items has been started. Verified absent:

| Review § | Item | Evidence |
| --- | --- | --- |
| §5.1 | Rigid diaphragms, rigid links, constraints | No `.cs` file in the repository matches *diaphragm*, *rigid link* or *constraint*. The nearest concept, `Support` with a finite `Restraint.Stiffness`, cannot express a node-to-node coupling. |
| §5.2 | Bar end offsets, rigid ends, insertion point | `Geometry/Bar.cs` is `StartNodeId`, `EndNodeId`, `SectionId`, `MaterialId`, `RotationAngle`. The asymmetry the review named is unchanged: `Plate` has `Alignment` and `SurfaceOffset`, `Bar` has nothing. |
| §5.3 | Bar behaviour (truss / tension-only / compression-only) | `CompressionOnly` exists only as a `PlateBehaviour` value. A bar is always a full frame element; releases are the only approximation. |
| §5.4 | Stiffness modifiers | No `.cs` file matches *modifier*. Neither `Bar` nor `Plate` nor the property entities carry one. |
| §5.5 | Material completeness | `Materials/Material.cs` is E, ν, ρ, Strength. No thermal expansion α anywhere in the repository, no material-type enum, no grade string. α remains an internal inconsistency, not just an omission: `TemperatureLoad` exists and nothing can apply it. |
| §5.6 | Support local axes / inclined supports | `BoundaryConditions/Support.cs` carries six `Restraint`s and no coordinate system. Note the asymmetry now that §4.2 has landed — a *load* can say `Local`, a *support* cannot. |
| §5.7 | Variable / layered / orthotropic thickness; bedding semantics | `ConstantThickness` is still the only `SurfaceProperty`. `"variable"` and `"layered"` are named in the base class doc comment and not implemented; orthotropy is not even reserved. `Restraint.Stiffness` for an area target is still undefined as total-spring versus bedding modulus. |
| §5.8 | Temperature gradient axis | `TemperatureLoad` is `ElementIds`, `DeltaT`, `GradientPerDepth?` — no axis, no sign convention. It derives from `Load`, not `DistributedLoad`, so it inherits no frame either. Unblocked by §4.2's conventions but not done. |
| §5.9 | Units | `Units.cs` is unchanged: two nullable free-text strings, no temperature, angle or mass unit, and no mention in `FemexModel.Validation.cs` at all. `"length": "banana"` round-trips clean. |

---

## 4. The gap the review did not measure: there are no connectors

The review assessed the *format*. Read as a project status it is therefore silent on the largest
item, which this note states plainly:

`griffel-femex.sln` contains **one project**. Every occurrence of "Robot", "ETABS", "Revit", "RFEM"
or "SAF" in `*.cs` is an XML doc comment. FEMEX today is a validated in-memory model with a JSON
round-trip and no reader or writer for any of the five programs.

Closing §4.4 and §4.5 would make the format *able to carry* a transfer. It would not make FEMEX
*perform* one. That needs, per the review's own §1:

- **Robot** — a COM/RobotOM client. The `.str` text format is frozen and cannot carry panels; the API
  is the real model. Properties are name-keyed, so the exporter must mint stable names.
- **Revit** — a 2023+ analytical-API add-in against `AnalyticalMember` / `AnalyticalPanel` /
  `AnalyticalOpening` / `BoundaryConditions`. Nodes are derived, so a FEMEX importer must author
  members and let Revit produce the nodes.
- **ETABS** — a CSI OAPI client. `.e2k` has no published specification and the review's grammar is
  reconstruction; the OAPI is the documented model and the one to target.
- **RFEM 6** — a SOAP Web Services client. Watch the default Z-down base data.
- **INDUCTA RCB** — no public API, schema or neutral text format exists. Realistically a DXF or
  Revit-Link detour, or nothing.

And the review's §7.3 admission still stands unaddressed: **nothing here has ever been checked
against a real exported file.**

---

## 5. Recommended order

| # | Work | Size | Why here |
| --- | --- | ---: | --- |
| 1 | Producer / project / timestamp metadata + `UnmappedMemberHandling` | XS | Completes §4.5. Unblocks the next breaking change the way `schemaVersion` unblocked load direction, and stops silent field loss between builds. |
| 2 | Section numeric escape hatch — explicit A, Iy, Iz, J subtype | S | Highest value per unit of work in the whole list: it converts "lost" into "degraded" for every shape FEMEX does not model, and is strictly additive to the existing discriminated union. |
| 3 | Section shapes (I/H, channel, angle, box, hollow) + catalogue subtype | M | Closes §4.4. Item 2 first so that item 3 failing to recognise a profile is survivable. |
| 4 | Material completeness — α, type enum, grade string | S | α is an internal inconsistency; the type enum and grade are how every target program resolves a material. |
| 5 | Units as enums, plus temperature, angle and mass | S | Small, and it is what makes items 2–4's numbers mean anything. |
| 6 | **One real ETABS or RFEM export, round-tripped** | M | The step that decides whether items 1–5 were right. Everything above is built from vendor documentation and has never met a real file. |
| 7 | §5 in the review's order — diaphragms and rigid links first | L | Diaphragms decide whether a lateral model can cross at all; the review argued they arguably belong in P0 and this note agrees. |
| 8 | The first connector | XL | Pick one target, not five. ETABS is the closest architectural relative and the OAPI is documented, so it is the cheapest first proof that the format works. |

Items 1–5 are roughly a week of schema work and are all additive except the units change. Item 6 is
the one that should not be deferred behind item 7: building nine more P1 entities against
documentation, before a single real file has been read, is how a format acquires the wrong
vocabulary confidently.

---

## Still open

- **Whether item 8 should come before item 7.** A first connector against an incomplete schema finds
  out which of §5 actually matters, rather than guessing. The argument against is that a connector
  written against a schema that is about to gain diaphragms will be rewritten.
- **Whether FEMEX should target SAF as an intermediate.** RFEM, and via SCIA a good deal else,
  already reads and writes SAF. A FEMEX ↔ SAF converter would reach more programs per unit of work
  than any single native connector — at the cost of inheriting SAF's weaker plate model, which
  review §3.2 shows is the one place FEMEX is ahead.
- **Nothing in §2 or §3 has been re-argued.** This note records what changed and what did not; the
  reasoning for each gap is `FEMEX_Interop_Review.md` and has not been revisited.
- **The review's §7.3 questions are all still open**, the level-based node against a real Robot or
  RFEM model most of all.
