# FEMEX ⇄ SAF — the mapping, as implemented

**B5 of `Claude/AdaptersPlans/SAF_Adapter.md`, written 28 August 2026**, and the first per-program
mapping document. §8 of `FEMEX_Adapters.md` requires that one be written *after* a real file has
been read; `FEMEX_SAF_Fit.md` is the prior step and `FEMEX_SAF_Corpus_Notes.md` is the raw material.

**This document does not restate `FEMEX_SAF_Fit.md` §2 or §8.2.** A mapping table maintained in two
places is a mapping table that disagrees with itself. §2 walks the workbook sheet by sheet and is
still the map; §8.2 is the declared-loss catalogue and is now *code* —
`griffel-femex.Adapters.Saf/SafLoss.cs` and `SafMessages.cs`, **81 entries**, each with a category, a
leg, an anchoring rule and its text, read by the adapter and by the test that checks the adapter said
them.

What is here is the part neither document could contain: **the decisions the implementation had to
make, and what each was decided against.** Where a decision rests on a measurement, the measurement
is quoted.

---

## 0. The shape of the crossing

| | |
|---|---|
| **Read** | SAF 1.0.0 – 2.3.0, whatever the SDK accepts |
| **Written** | SAF **2.3.0**. Not 2.2.0, and not the caller's choice — the SDK stamps its own |
| **Both legs** | `ExcelModel.Objects`, a flat heterogeneous bag, grouped once by `SafObjects` and dispatched |
| **Units** | every import lands in metre · newton · celsius · degree · kilogram; every export reads the model's own statement |
| **Identity** | SAF name ⇄ `TransferMessage.NativeHandle` and `ExportReceipt`; SAF `Id` ⇄ `Uid`, with the caveat of §4 |
| **Declared losses** | 81, of which 53 are import-leg and 28 export-leg |

---

## 1. Nine decisions, and what each was decided against

### 1.1 A parametric section's dimensions — measured where the corpus settles it, inferred where it does not

SAF states a parametric section as a `Shape` from a 45-value library and an **ordered `Parameters`
list whose meaning is per-shape**. The SDK exposes the list and not its meaning; the order is in the
specification, which is not in the package. Reading a depth as a width silently changes a section, so
this could not be guessed.

**Settled by measurement.** `CS7` in the reference workbook is a symmetric steel I-section:
`[500, 200, 200, 25, 25, 15]`. Only one reading of six numbers gives a 500-deep I with 200-wide,
25-thick flanges and a 15-thick web. That fixes the I-section order — depth, both flange widths, both
flange thicknesses, web — and with three `Rectangle` rows (`250×200`, `450×300`, `30×30`) and
`Pipe`'s unambiguous `[150, 8]`, four of FEMEX's eight shapes are settled.

**Inferred.** T, L, angle, channel and tube follow that same convention. Every section built that way
carries `InferredShapeParameters`, an *Approximated* message naming the inference. Closing it is an
afternoon with the specification's parameter tables, and it is in *Still open*.

**Not attempted.** An I-section with unequal flanges, and the thirty-odd shapes outside FEMEX's eight
— `Oval`, `ISectionWithHaunch`, `DoubleRectangle`, `TripleRectangle`, `TTee` and the rest — arrive as
`GenericSection` carrying whatever stiffness the workbook stated. Taking one flange of an asymmetric
I and discarding the other would move the elastic centroid without saying so.

**Form codes** are the one place the export leg is exact: 1–8 are FEMEX's eight discriminators, which
the file's three manufactured IPE180 rows confirm at `Form code = 1`. Only `generic` needs P5's
provisional `0`, and it says so.

### 1.2 Position along a member — carried, never minted, never snapped

1.9 and 1.10 landed the fields; this is where the four SAF spellings become the one FEMEX form.
`Coordinate definition` ∈ `Relative | Absolute` × `Origin` ∈ `From start | From end` — the reference
workbook uses all four — become **relative, from the start**.

Two details that cost time if met for the first time in a debugger:

- The SDK types the station cell as `object`, because its meaning depends on the column beside it: a
  bare `double` when relative, a UnitsNet `Length` when absolute. `SafPosition` checks rather than
  casts.
- `From end` reverses a *pair*. A line support from 0.2 m to 1.5 m from the end runs from
  (L − 1.5) to (L − 0.2) from the start, so the pair arrives inverted and is swapped.

The absolute conversion is exact on a straight member and `ChordedPosition` — *Approximated* — on a
chorded arc, because the chord length is not the arc length.

### 1.3 Edge indexing — both bases, handled rather than assumed

`StructuralEdgeConnection.Edge` and `StructuralCurveAction.Edge` are **1-based**;
`RelConnectsSurfaceEdge.Edge` is **0-based**. Confirmed in the reference file, which uses edges 0, 2
and 3 on one sheet and 1 on the other for the same surface. FEMEX naming an edge by its two nodes is
what makes writing either of them safe, and the export leg reverses the lookup — find the contour
edge whose node pair matches — and reports `UnplaceableEdgeSupport` or `UnplaceableLinearLoad` when
no plate owns that pair, rather than attaching the object somewhere plausible.

### 1.4 One SAF row into several FEMEX objects, and back

Three SAF rows become more than one FEMEX object: a curved member chorded into a chain of bars, a
`Position = Both` hinge split into one per end, and a `Repeat (n)` point load expanded into its
series. All three are read back on the export leg through `ParentUid`, so the crossing is reversible
rather than merely lossy — one member out, not eight; one hinge row, not two.

The piece that keeps the row's uid is the first. **The rest get derived uids, not minted ones** —
`SafIdentity.Derived`, the row's uid with its last four bytes mixed with the piece's index. A minted
uid would make the same workbook produce a different model on every read, which is the instability
§7.3's name-stability rule forbids, applied to the other half of identity.

The one thing that does not come back is the shape: FEMEX stores chords, not arcs, so a circular arc
returns as a polyline through the same points. That difference is what the `ChordedCurve` message
names.

### 1.5 The unit boundary

The import leg **normalises to SI and says so** — one canonical answer, so two workbooks written in
different systems produce FEMEX models that compare directly. It is still an invention: SAF stated a
coarse `Metric | Imperial` flag and FEMEX carries five typed enums. `StampedUnitSystem` says it once
for the model, and `RestatedInSiUnits` says it once for each entity kind whose numbers were restated,
because a rescaled load with no message against it reads as an unexplained change of value.

The export leg reads the model's own statement, and P5's rule decides the flag: imperial iff length ∈
{inch, foot} or force ∈ {pound-force, kip}. **Metre with kip is a legal FEMEX statement and SAF has no
flag for it**, so a mixed model is reported at **Error** severity rather than quietly halved — the SDK
logs *"Determined ExcelSystemOfUnits from file"* before it reads a sheet and drives every conversion
from it, so getting this wrong rescales the model rather than mislabelling it.

One trap worth naming: SAF's thermal cells are °C numbers wrapped in UnitsNet's **absolute**
`Temperature`. `FromKelvins` would add 273.15 to every gradient in the file;
`FromDegreesCelsius`/`.DegreesCelsius` is the pair that round-trips the cell.

### 1.6 Levels — synthesised on every file, and the highest-traffic invention there is

Nothing in SAF references a storey, and `Node.LevelNumber` is a required foreign key enforced as an
**error**. So every SAF file arrives as a bag of free coordinates and levels must be synthesised
before one node can be written — the reference workbook declares two storeys and uses nine distinct
elevations, so **seven of its nine levels are the adapter's**, and each is reported.

`GeometrySynthesis` does the clustering, two-phase per §6.2. The adapter adds one thing the helper
cannot: **the candidates are declared in a canonical order**, storeys sorted by elevation and points
sorted by coordinate, because the declaration order is the creation order and therefore the numbering
order. A workbook whose rows are shuffled is the same model and must produce the same node table,
numbering included. A synthesised level's uid is derived from its elevation, because its elevation is
all it is.

### 1.7 What the export leg invents, and why each is unavoidable

FEMEX cannot write a workbook SAF's own validator will accept without inventing. Decision 12 is the
rule — every invented mandatory column is an *Invented* message, never a silent default — and the
validator is what found the ones P5 had not predicted:

| SAF requires | FEMEX has | Written | Message |
|---|---|---|---|
| `Model.System of units` | five typed enums | per P5's rule; **Error** if mixed or unstated | `InventedSystemOfUnits` |
| `Model.National code` | nothing | `EC-Standard-EN` | `InventedNationalCode` |
| `Model.LCS of cross-section` | a fixed convention, stated nowhere | `ZYX` | `InventedCrossSectionLcs` |
| SAF version | a FEMEX schema version | `2.3.0` | `InventedSafVersion` |
| `System line`, four analysis eccentricities | often nothing | `Centre`, zero | `InventedMemberEccentricity` |
| **`LCS Adjustment` type + vector, on every member and surface** | a roll angle from a rule | SAF's own default for the form | `InventedLocalFrame` |
| **A `Profile` on any non-parametric section** | nothing on a generic one | the section's name | — |
| **A `Material` on every cross-section** | the material is on the member | the member's, or the model's first | `InventedSectionMaterial` |
| **Pasternak `C2x`/`C2y` on a subsoil connection** | nothing | zero | `InventedPasternakSubsoil` |
| `Load group` on every case | often nothing | one per load nature, `Relation = Standard` | `InventedLoadGroup` |
| A name on every row | none at all on bar, node, support, hinge | `{Kind}-{8 hex}` | `SynthesisedNames` |

The five in bold are **not in P5's table**. They were found by the SDK's own validator refusing the
workbook, which is the nearest thing to an independent oracle that runs without a browser.

Two things the validator also refused, and what was done instead:

- **`Flexible compression only` is not allowed on the line and edge support sheets** — five of SAF's
  eight constraint types are. The sense is kept and the stiffness is not (`NarrowedLineRestraint`): a
  support that lifts off and resists rigidly is wrong by a stiffness, where one that is flexible both
  ways is wrong about whether it lifts off, which is the thing the model was built to show.
- **`Direction = Vector` with a value is refused, and the vector columns throw in the SDK's writer.**
  A load stated by vector is written along the axis its vector leans on hardest, reported
  `FlattenedLoadDirection` — the right size in the wrong direction, which is a difference a check
  report should show and a receiving program will not.

### 1.8 Where two SAF objects become one, and one becomes two

- **SAF splits force from moment**; FEMEX carries `Fx…Mz` on one object. A force and a moment at the
  same station in the same case merge on import, and one of the two SAF uids cannot survive
  (`MergedForceAndMoment`). On export the object splits back into up to six rows, of which the first
  carries the uid.
- **FEMEX shares a surface property; SAF states thickness on each surface.** The shared object
  dissolves on export and re-forms on import as one property per distinct thickness. Its uid is
  derived from the thickness, because that is all it is (`DissolvedSurfaceProperty`).
- **FEMEX puts material on the member; SAF puts it on the section.** Two members giving one section
  different materials is a statement SAF cannot make; the first wins, and `SharedSectionMaterial`
  says so.

### 1.9 What is refused rather than half-done

A workbook declaring `Y vertical` or `X vertical` is **refused with a reason**. FEMEX is Z-up by
definition, and permuting every coordinate, every load direction and every local frame is real work
with no file in the published corpus to test it against. A half-done permutation is the silent wrong
answer the product exists to catch.

A stream containing no SAF objects at all is also refused. An empty stream reads as an empty package
and would otherwise arrive as a successful import of nothing.

---

## 2. Five things about the SDK that will break a naive reader

Beyond the nine in `FEMEX_SAF_Corpus_Notes.md` §4, which are about the workbook. These are about the
package.

1. **The SDK mints a GUID on read for every blank `Id` cell**, not only on write. `Id` is
   non-nullable and never `Guid.Empty`, so an invented uid and an authored one are the same shape —
   and **the same workbook read twice gives different uids for those rows**. This is §4 below.
2. **`ExcelStructuralCurveMemberVarying` uses a comma** to separate the two sections of a tapered
   span, where every other list column in the format uses a semicolon.
3. **`ExcelCurveLCSType.Standard` is obsolete** and documented as identical to `VectorY`, so the
   local frame must be tested by its vector rather than by the enum member.
4. **`ExcelStructuralCurveAction.Value1` is obsolete** in favour of `Value`, while
   `ExcelStructuralCurveMoment.Value1` is not. The two sheets do not agree with each other.
5. **The SDK's log is an event subscription**, `IEventService.Subscribe<LogEvent>`, not a return
   value or an exception. Only its `Error` level crosses into the transfer report — see §5.

---

## 3. Round trip, measured

SAF → FEMEX → SAF → FEMEX, comparing the two FEMEX models, with the SDK on both sides of the
comparison so its own 75-cell normalisation cancels (`FEMEX_SAF_Corpus_Notes.md` §5).

| Workbook | Differences | What they are |
|---|---|---|
| `SAF_example_STEEL_HALL_*` (8 files) | **0** | — |
| `SAF_example_HOUSE_metric_ZYX_200/210` | 4 | as below |
| `SAF_example_HOUSE_metric_ZYX_220` | **4** | one line load on an edge no plate owns; one edge support on an opening edge; two expanded repeat loads whose `ParentUid` does not come back |

Every one of the four is named by a message, which is the assertion
`EveryDifferenceAcrossTheRoundTrip_IsNamedByAMessage` makes over all eleven workbooks.

The steel-hall result is the one worth keeping in view: **47 members, 45 nodes, ten supports and
sixteen hinges cross both legs intact**. It is what shows the four differences on the house are the
house's and not the adapter's baseline noise.

---

## 4. The identity problem this phase found

`FEMEX_SAF_Corpus_Notes.md` §5 recorded that the SDK invents 42 GUIDs *on write* for the reference
workbook's blank `Id` cells. **It does the same on read**, and that is worse, because it is invisible:

- `ExcelObjectBase.Id` is a non-nullable `Guid` and is never `Guid.Empty` after a read.
- So an adapter cannot tell a uid the file stated from one the SDK made up.
- And two reads of the same file give **different** uids for those rows — which means the same
  workbook produces two models that nothing can match, and §7.2 equivalence cannot be asserted at
  all.

**What the adapter does.** `SafGateway.Read` reads the same bytes twice and compares the `Id` of each
row, addressed by sheet and row number. Any Id that moved was invented. The importer then replaces
those with a uid **derived from the row address**, so the read becomes a function of the file, and
reports `MintedUids` with the count — 42, for the reference workbook, exactly as the corpus notes
predicted.

The cost is one extra parse per import, roughly doubling the read. That is the price of being able to
say which uids are provenance. It is recorded in *Still open* as the first thing to revisit if a
batch run over a hundred models ever needs the time back.

---

## 5. Where the contract pinches

Two places where `FEMEX_Adapters.md` §3's types could not express what the adapter had to say.

**`TransferMessage` has two severities and one of them requires a loss category.** `Error` needs
none; `Warning` needs one. An SDK warning is not a loss of a known category, so carrying it as a
warning means inventing a category and carrying it as an error overstates it. Only SDK **errors**
cross into the report. Recorded rather than worked around.

**`Restraint.Stiffness` has no declared unit,** which `FEMEX_SAF_Fit.md` §2.4 already flags. It bites
here in a specific way: SAF states a point support's stiffness per support, a line support's per unit
length, and a subsoil's per unit area — three different quantities behind one FEMEX property. The
adapter picks the right one per sheet, and nothing in the format says it was right to.

---

## 6. Still open

- **The parameter order for T, L, angle, channel and tube** is inferred from the I-section's, not
  measured. Every affected section says so. The fix is the specification's per-shape parameter
  tables.
- **The double read.** Exact, and it doubles import time. If a batch run needs the time back, the
  alternative is reading the `Id` columns out of the OOXML directly, which means a second Excel
  reader in a project that deliberately has one.
- **A non-Z-vertical workbook is refused.** No file in the published corpus exercises it, which is
  both why it was not built and why building it would be untested.
- **A generic section with no stated stiffness fails `Validate()`** — correctly: SAF gave no
  properties and FEMEX cannot hold the shape, so nothing can be built from it. Ten of the reference
  workbook's 29 sections are in that state. Computing properties for the shapes FEMEX understands but
  does not model is a separate piece of work.
- **`StructuralProxyElement`, the three rigid-connection sheets, the three free-load sheets, internal
  edges and imposed support displacements** are all *Unmapped* per concept. Each is a FEMEX format
  question, not an adapter one.
- **The SDK's own warnings have no home in the transfer report.** See §5.

---

## Sources

- `Claude/FEMEX_SAF_Fit.md` §2 (the sheet-by-sheet map) and §8.2 (the loss catalogue).
- `Claude/FEMEX_SAF_Corpus_Notes.md` — the measurements this document builds on, and P2–P5.
- `Claude/FEMEX_Adapters.md` §3–§7 — the contract, the categories and the conformance rules.
- The corpus itself, vendored at `griffel-femex.Adapters.Saf.Tests/Corpus/`: eleven workbooks,
  Apache-2.0, from `StructuralAnalysisFormat/StructuralAnalysisFormat-Examples`.
- Every count and enum spelling in this document was read out of the SDK or the workbooks by the code
  that ships beside it, not off saf.guide.
