# SAF corpus notes — one real workbook, read

**Phase 0 of `Claude/AdaptersPlans/SAF_Adapter.md`, executed 25 August 2026.** Its *Verification*
asks for exactly this file: *"recording what one real 2.2.0 workbook actually contains, with the four
decisions of P2–P5 answered or explicitly deferred with a reason."*

`FEMEX_SAF_Fit_Update_Plan.md`'s *Step 0'* said an afternoon. It was, and it paid for itself three
times over: it confirmed 1.7's material enum retroactively, it corrected both held bumps' invented
shapes, and it produced four findings that no amount of re-reading the specification would have
produced — including one that changes what Phase B's round-trip test is allowed to assert.

Everything below is **measured**, not read off saf.guide. Where a claim comes from the specification
rather than from a file, it says so.

---

## 0. What was found, in one screen

1. **1.7's `MaterialType` is confirmed exactly.** The reference workbook's twelve materials use
   `Concrete, Steel, Timber, Aluminium, Masonry, Other` — FEMEX's six enum members, same spellings,
   nothing else. Six of six, first try.
2. **1.7's shear-modulus decision is vindicated by real numbers.** Three timber materials state
   `G = 690 MPa` against `E = 11 000 MPa` and `ν = 0`, where `E/(2(1+ν))` gives **5 500** — a factor
   of eight. A FEMEX build without `Material.ShearModulus` would have silently substituted the wrong
   number for every timber member in this file.
3. **P2 is forced, exactly as `FEMEX_SAF_Fit.md` §7.1 predicted.** Five of the six positional classes
   are exercised in the very first real file: two point loads and two point moments *on beams*, one
   support *on a beam*, one line support from 0.2 m to 1.5 m measured *from the end*, three line
   loads at absolute stations, and one point moment repeated three times at 0.1 m spacing. Nothing
   in FEMEX can hold any of it.
4. **The SAF SDK loses 75 cells round-tripping the reference file to itself, with FEMEX nowhere in
   the picture** (§5). That is the finding with teeth: it means Phase B's *"every difference is named
   by a `TransferMessage`"* assertion cannot be made against the workbook, only against the object
   model, and the plan's *independent oracle* step inherits a 75-cell noise floor it did not know
   about.
5. **The SDK reads SAF 2.2.0 and writes SAF 2.3.0.** Not 2.2.0. The exporter does not get to choose;
   the version stamp is the SDK's.
6. **The corpus is thinner than 11 files.** It is **two models**, re-emitted at nine spec versions.
7. **The reference file contains an invalid enum value and neither the SDK nor its validator
   notices** — `StructuralCurveMember.Type = "CS26"` on member B48. It survives the round trip
   verbatim. FEMEX, handed the equivalent, refuses the whole file (§8).

---

## 1. The corpus, measured

`StructuralAnalysisFormat/StructuralAnalysisFormat-Examples`, `HEAD`, 25 August 2026 — **11 `.xlsx`
files** across nine versions, confirming the plan's *Still open* count. All eleven were imported with
the official SDK; all eleven read with **exit 0 and zero warnings or errors**.

| File | Spec version | Object types | Objects |
|---|---|---|---|
| `SAF_example_STEEL_HALL_metrix_ZYX_105` → `_210` (8 files) | 1.0.5 → 2.1.0 | 11 | 133 each |
| `SAF_example_HOUSE_metric_ZYX_200` | 2.0.0 | 37 | 352 |
| `SAF_example_HOUSE_metric_ZYX_210` | 2.1.0 | 37 | 352 |
| `SAF_example_HOUSE_metric_ZYX_220` | **2.2.0** | 38 | 357 |

**The corpus is two models, not eleven.** The eight STEEL_HALL files are the same 133-object frame
re-emitted at successive spec versions, and the three HOUSE files differ by five objects. So the
plan's *Still open* — *"only one or two of them at 2.2.0 — not the broad corpus Phase 1 implies"* —
is right and is worse than it says: **Phase B's corpus test over all eleven files exercises two
distinct models and nine version-compatibility paths**, which is a version-migration test wearing a
corpus test's clothes. It is worth having for exactly that, and it is not coverage.

The one 2.2.0 file, `SAF_example_HOUSE_metric_ZYX_220.xlsx`, is SCIA's *"model containing all
supported objects"* — 43 sheets, deliberately exhaustive, deliberately synthetic. It is the best
single artefact available and it is **not** a building anybody designed. Note what that means for the
*Invented*-detection work: a file built to exercise every column fills columns that real exports leave
blank, so **this file understates how much an exporter must invent, not overstates it.**

---

## 2. The SDK, measured

`StructuralAnalysisFormat` **1.7.3** on nuget.org, `Apache-2.0`, authored by `scia-nv`.

| Fact | Value | Why it matters here |
|---|---|---|
| Target frameworks | **`netstandard2.0` only** | loads on both legs of the planned `netstandard2.0;net8.0` multi-target, and on `net48`. Decision 5 of the plan is unobstructed. |
| Dependencies | **EPPlus 4.5.3.3**, FluentValidation 10.2.3, UnitsNet 4.72.0 | see below |
| Bootstrapping | `StructuralAnalysisFormat.Bootstrappers.SimpleInjector5` (or `…4`) is **required** — *"most of the actual implementation details [are] internal"* | SimpleInjector is not optional, which is the second half of the plan's *"EPPlus and SimpleInjector are a real trimming risk under WASM"*. That argument is confirmed. |
| Spec version written | **2.3.0** | the exporter cannot claim 2.2.0 |
| Spec versions read | 1.0.0 → 2.3.0, with per-property version ranges | eleven-of-eleven corpus read |

**EPPlus, answered.** The plan's *Still open* says *"B1's original answer has been withdrawn; the
replacement is stated but not confirmed."* Confirmed now, and better than feared: the SDK pins
**EPPlus 4.5.3.3**, the last release before EPPlus 5 moved to the Polyform Noncommercial licence.
4.5.3.3 is **LGPL**, which a commercially-sold product may consume as an unmodified NuGet-delivered
assembly. FEMEX therefore does not need to choose, cost or migrate to ClosedXML/NPOI to sell a report:
it never references EPPlus itself, and the transitive dependency is licensed compatibly. What FEMEX
*does* inherit is EPPlus 4.5.3.3's own constraints — an unmaintained 2019 assembly on the netstandard
leg, and a hard binding conflict if any host process ever loads EPPlus 5+. That is a note for the
add-in question, not a blocker for anything in this plan.

**Import shape, verified working.** `IExcelImportService.Import(Stream)` → `ExcelModel`, whose
`.Objects` is a flat, typed collection; `IExcelExportService.Export(Stream, ExcelModel)`. No file
paths, no threading, no UI — it fits `FEMEX_Adapters.md` §3.6's synchronous in-process contract
without adaptation. The probe that produced every measurement in this document is 50 lines.

**The SDK's log is an events subscription, not exceptions.** `IEventService.Subscribe<LogEvent>` with
a `Severity`. Reading the reference file emits **205 events, none above `INFO`**. This is the natural
input to `TransferResult.Messages` and it is already the right shape: the adapter subscribes for the
duration of the call and folds `WARN`/`ERROR` into the transfer report rather than inventing its own
diagnostics for things the SDK already noticed.

---

## 3. `SAF_example_HOUSE_metric_ZYX_220.xlsx`, sheet by sheet

43 sheets, 357 objects. Only what bears on the mapping is recorded; everything stated is from the
file.

### 3.1 `Model` and `Project`

```
Global coordinate system   Z vertical
LCS of cross-section       ZYX
System of units            Metric
National code              EC-Standard-EN
SAF Version                2.2.0
Ignored objects            (blank; the 2.1.0 HALL file has "Rel connects rigid cross")
```

Three of §3's mandatory columns are right there, and **all three are filled with exactly the values
`SAF_Adapter.md` P5 proposed to invent.** `EC-Standard-EN` and `ZYX` are not guesses any more; they
are what the reference files say. `Metric` is confirmed as load-bearing rather than decorative — the
SDK logs *"Determined ExcelSystemOfUnits from file: Metric"* **before** it reads a single sheet, and
drives its UnitsNet conversions from it. Writing the wrong value there rescales the whole model.

`Created` and `Last update` are **Excel serial numbers** (`43101`, `44932`), not ISO strings.
A reader that takes the cell as text gets `43101`.

### 3.2 `StructuralMaterial` — 1.7 confirmed, and one thing 1.7 got right by accident

Columns: `Name | Type | Subtype | Quality | Unit mass [kg/m3] | E modulus [MPa] | G modulus [MPa] |
Poisson coefficient | Thermal expansion [1/K] | Design properties | Id`.

- **`Type`**: `Concrete ×3, Steel ×2, Aluminium ×2, Timber ×3, Masonry ×1, Other ×1`. Six of six
  against `MaterialType`. **1.7's spellings are correct; nothing to change.**
- **`Quality`** is the grade — `C20/25`, `S235`, `EN-AW 5083`, `D30 (EN 338)`, `GL 30c (EN 14080)` —
  and 1.7's decision to keep it *distinct from* `Name` is vindicated in the crudest possible way:
  the file contains a material **named `C20/30` whose quality is `C25/30`**. Name and grade disagree
  in the reference workbook. An adapter that had mapped `Name` → `Quality` would have propagated
  that.
- **`G modulus`** is stated on every row, and on the three timber rows it is **690 MPa against
  E = 11 000 and ν = 0** — the derived value is 5 500. §4 item 8's *"timber above all, where the ratio
  is nothing like the isotropic one"* is not a hypothesis. `Material.ShearModulus`, stated-wins-over-
  derived, is what makes this file readable without silently changing eight-fold every shear term on
  a timber member.
- **`Thermal expansion`** is stated on every row (1.0e-5, 1.2e-5, 2.3e-5). `Material.ThermalExpansion`
  from 1.7 receives it.
- **`Subtype` and `Design properties` are blank on all twelve rows.** So §7.7's proposed material
  property bag has **no supporting evidence in the corpus** — the one file built to exercise every
  column does not exercise those two. That is a reason to leave §7.7 where it is.

### 3.3 `StructuralCrossSection`

`Cross-section Type` ∈ `Parametric (25) | Manufactured (3) | General (1)`. `Shape` for parametric
sections uses fourteen distinct spellings, of which FEMEX's eight discriminators cover nine:
`Rectangle`, `I section`, `T section`, `L section`, `Circle`, `Tube`, `Pipe`, `Angle`, `Channel`.
The five it does not: **`Oval`, `I section with haunch`, `Double rectangle`, `Triple rectangle`,
`T tee`** — three of which are timber built-ups. They cross as `generic` with stated properties.

`Form code` is **blank on every parametric row and `1` on every manufactured row** (three IPE180s,
`Description ID = 1`). That settles the conditional-column question empirically: form code is
required only for `Manufactured`, and `1` is I-section, exactly as §7.6 reconstructed. It also
sharpens P5 — see §9.

### 3.4 `StructuralCurveMember` — 42 members

| Column | Observed | Note |
|---|---|---|
| `Type` | `Column ×33, Beam ×3, beam ×4, CS26 ×2` | casing is inconsistent **and one value is not an enum member at all** |
| `Behaviour in analysis` | `Axial force only ×33, Standard ×9` | the held bump's target column, populated on every row |
| `System line` | `Top left ×33, Centre ×9` | mandatory, and *not* always `Centre` |
| `Analysis Y/Z Eccentricity` ×4 | **`0` on all 42 rows** | mandatory, and zero throughout the corpus |
| `Structural Y/Z Eccentricity` ×4 | `0` on all 42 rows | |
| `Geometrical shape` | `Line ×40, Circular Arc ×1, Polyline ×1` | |
| `Segments` | `Line`, `Circular Arc`, `Line;Line;Circular Arc;Line` | |
| `Length [m]` | blank on the polyline; **`0` on the arc** | not a usable length |
| `Parent ID` | blank on all 42 rows | the column exists — see §7 |
| `Id` | **blank on 7 of 42** | see §4 |

Two things follow immediately. **`Behaviour in analysis` is populated on every single row**, so
§4 item 1 is not a corner case — a FEMEX 1.8 import of this file gets 33 axial-only members wrong.
And **`Analysis Y/Z Eccentricity` is zero on every row of every file in the corpus**, so the held
bump that adds it has, today, no file that would exercise it. Both are held bumps; the evidence
splits them.

### 3.5 `StructuralCurveMemberVarying` — the held bump's shape, corrected

One row, and it is worth quoting whole:

```
Name  Cross sections 1  Span 1  Alignment 1   Cross sections 2  Span 2  Alignment 2   Cross sections 3  Span 3  Alignment 3
AD1   CS1               0.25    Centre        CS1,CS9           0.5     Left          CS1               0.25    Centre
```

- Spans are **relative and sum to 1.0** — confirmed, as §6.2 reconstructed.
- A span states **either one section or a comma-separated pair** (`CS1,CS9`) for a linear transition.
  The separator is a **comma**, where every other list column in SAF uses a semicolon.
- `Alignment` ∈ `Centre | Left` here, per span.
- **The sheet is a fixed-width repeating group**, not a normalised child table: three spans means
  nine columns, and a four-span member means twelve. `StructuralLoadCombination` has the same shape
  (`Load Factor n | Multiplier n | Load Case name n`, up to seven in the HALL file). A reader must
  discover the group count from the header, and an exporter must decide how many groups to emit.
- It is referenced **from** the member: `StructuralCurveMember.Arbitrary definition = AD1` on B1.

The held bump was designed against the specification. Everything above matches it except the comma,
which is the kind of detail that costs an afternoon of debugging if it is met for the first time in
Phase B.

### 3.6 Surfaces

`StructuralSurfaceMember`: 11 rows, `Type = Plate`, `Behavior in analysis = Membrane`,
`System plane at = Centre`. `Thickness type` ∈ `Constant ×10`, **`Variable in direction XY` ×1**.
FEMEX's `SurfaceProperty` is abstract with exactly one implementation, `ConstantThickness`, so the
eleventh plate is a declared loss — and the abstract base is the extension point if it ever stops
being one.

`StructuralCurveEdge` (3), `…Opening` (7), `…Region` (4) are all present, including an **opening
whose contour contains a `Circular Arc`** and an **internal edge that is a `Circular Arc`** (`ES3`).
§7.3's *"the edge and everything pointing at it are lost together"* is exercised: `LFS4` and `LFS5`
are line loads on `ES1`/`ES2`, and `Sle3` is an edge support on `ES1`.

### 3.7 Supports, hinges, links

| Sheet | Rows | What is in it |
|---|---|---|
| `StructuralPointSupport` | 2 | `Sn1` in a node; **`Sb1` on beam B38**, `Relative`, `Position x = 1` |
| `StructuralCurveConnection` | 2 | `Slb2` on B4, **`Absolute`, `From end`, 0.2 m → 1.5 m** |
| `StructuralEdgeConnection` | 3 | on edge / on opening edge / on internal edge |
| `RelConnectsStructuralMember` | 22 | hinges; `Position` ∈ `Begin \| Both \| End` |
| `RelConnectsRigidLink` | 1 | **all six DOFs `Non linear`**, with stiffnesses *and* resistances, one of them **negative** (`-1.36`) |
| `RelConnectsRigidCross` / `…RigidMember` | 1 each | unmapped in FEMEX entirely |
| `StructuralSurfaceConnection` | 2 | Winkler `C1x/C1y/C1z` + Pasternak `C2x/C2y`, plus a `C1z Spring` ∈ `Flexible \| Linear` column that §4 item 7 did not list |
| `StructuralPointSupportDef` | 1 | `TRS1_Z`: support `Sn1`, direction Z, **−500 mm imposed translation**, in load case LC2 |

The eighth restraint state, `Non linear` — the one 1.8 deliberately left unmapped — **appears in the
first real file**, and it appears with a negative stiffness. 1.8's judgement that carrying it would
mean adding a curve type rather than an enum member holds; what changes is that *Dropped* on that
value is a message the very first import will emit, not a theoretical one.

### 3.8 Loads

| Sheet | Rows | Positional? |
|---|---|---|
| `StructuralPointAction` | 8 | 6 `In node`, **2 `On beam`** (`Relative`, x = 0.5, `Repeat (n) = 1`) |
| `StructuralPointMoment` | 4 | 2 `In node`, **2 `On beam`** — `M4` is `Relative 0.3`, **`Repeat (n) = 3`, `Delta x = 0.1`** |
| `StructuralCurveAction` | 31 | every row carries `Coordinate definition`, `Origin`, `Extent`, `Start point`, `End point`. 28 `Relative`, **3 `Absolute`** (0 → 2.5 m). `Extent` ∈ `Full ×26 \| Span ×5`. `Force action` ∈ `On beam ×26 \| On edge ×2 \| On internal edge ×2 \| On opening edge ×1` |
| `StructuralCurveMoment` | 5 | same columns; reaches `On rib` and `On subregion edge` |
| `StructuralSurfaceAction` | 5 | `On 2D member ×3`, `On 2D member region ×1`, **`On 2D member distribution ×1`** — a surface load applied to a load panel |
| `…ActionThermal` (curve, surface) | 4 + 2 | `Variation` ∈ `Constant \| Linear`; the linear surface one states **`TempT = −273.15 °C`** |
| `…ActionFree` (point, curve, surface) | 1 each | raw coordinate lists — `Coordinate X = "0;5;4"` — exactly as §7.4 describes. One is `Type = Water pressure`, `Location = Projection` |

`TempT = −273.15` is absolute zero, in a file whose purpose is to be exemplary. It is a free test case:
a check report that does not flag it is not checking anything.

Two mapping notes that are not in any document:

- **SAF splits force from moment; FEMEX does not.** `StructuralPointAction` and
  `StructuralPointMoment` are separate sheets with separate `Id`s; `PointLoad` carries `Fx…Mz` in one
  object. So an import merges two SAF objects into one FEMEX object *when they share a node and a
  load case*, and an export splits one into two — and the two SAF uids cannot both survive on one
  FEMEX `Uid`. The same applies to `StructuralCurveAction`/`StructuralCurveMoment` against
  `LinearLoad.MagnitudeStart`/`MomentStart`. This is a **uid-identity loss on a merge**, and it is the
  second independent argument for §7.
- **`Repeat (n)` + `Delta x` has no FEMEX equivalent.** `M4` is one SAF object standing for three
  moments at 0.3, 0.4 and 0.5 relative. Expanding it on import is numerically exact and structurally
  lossy: three FEMEX loads cannot be re-collapsed into one SAF row without knowing they came from one.

### 3.9 Load groups, cases, combinations — the held bump's shape

```
StructuralLoadGroup     Name | Load group type | Relation | Load type | Id
LG1 Permanent  Standard  (blank)     LG5 Variable   Standard  Wind
LG2 Variable   Standard  Domestic    LG6 Accidental Exclusive (blank)
LG3 Variable   Standard  Roofs       LG7 Seismic    Together  (blank)
LG4 Variable   Standard  Snow
```

`Relation` ∈ `Standard | Exclusive | Together`, and the two files **disagree about which to use**:
HOUSE gives wind and snow groups `Standard`, the 2.1.0 HALL file gives them `Exclusive`. So the
invention P5 has to make is one where the corpus itself shows two producers choosing differently.
`Load type` is blank for permanent, accidental and seismic groups and populated
(`Domestic | Roofs | Snow | Wind`) for variable ones.

`StructuralLoadCase`: `Action type` ∈ `Permanent | Variable`; `Load type` ∈
`Self weight | Others | Static`; `Duration = Short` on the one variable case. Note `LC3` is
`Action type = Variable` but sits in `Load group = LG1`, which is `Permanent` — a second internal
inconsistency in the reference file, and one no validator flagged.

`StructuralLoadCombination`: `Category` ∈ `ULS (Ultimate Limit State) | ALS (Accidental Limit State)`
in HOUSE and **`According National Standard`** in the HALL file, with
`National standard = EN-ULS(STR/GEO) Set B`. `LimitState` has no member for *"whatever the national
standard says"*, and `LoadCombination` has nowhere to put the standard's name — so the HALL file's
two combinations lose their entire definition, not a detail of it. **This is not in `FEMEX_SAF_Fit.md`
§4 and belongs there.** Each term is a **pair** — `Load Factor n` *and* `Multiplier n` — against
`LoadCombinationTerm.Factor`, one number; HOUSE's `CO1` uses factor 2.5 with multiplier 3, so the
product is not incidental. And `Type = Nonlinear` has no `LoadCombinationType` member.

### 3.10 `StructuralSurfaceActionDistribution` — the sheet name is not what the plan says

**The sheet is called `StructuralSurfaceActionDistri`.** Excel caps sheet names at 31 characters and
`StructuralSurfaceActionDistribution` is 35. Anything keyed on the full name finds nothing.

Three rows, and between them they exercise the whole of §4 item 3:

| | `Type` | `Distribution to` | `LCS Type` | `LCS Rotation` | `Load applied to` |
|---|---|---|---|---|---|
| FL1 | `Nodes` | `Two way` | `x by vector` | 0 | (blank) |
| FL2 | `Edges` | `One way - X` | `y by vector` | **45** | (blank) |
| FL3 | `Beams and edges` | `One way - Y` | `Tilt of vector defined by point` | (blank) | **`B46;B47`** |

`Layer = Load panel` on all three. `PlateRegionKind.LoadOnly` holds the panel and **none of these five
columns**. The held bump's design is confirmed and one detail is corrected: `Load applied to` is a
semicolon list of member names and it is *populated* on the row that most needs it.

### 3.11 Results

`ResultInternalForce1D` (2 rows) and `ResultInternalForce2DEdge` (4 rows, under 32 trailing blank
rows — the row count in a sheet is not the object count). §6.5's *"results should stay closed"* is
untouched by anything here.

---

## 4. Nine things that will break a naive reader

Every one of these is in the reference file. None is in any FEMEX document.

1. **Sheet names are truncated to 31 characters.** `StructuralSurfaceActionDistri`.
2. **Column names change case between sheets and between versions.** The member sheet says
   `Behaviour in analysis`; the surface sheet says **`Behavior in analysis`**. The SDK's own writer
   emits `Member rib` where the 2.2.0 file has `Member Rib`, and `Parent id` where it has `Parent ID`.
3. **Column *order* is not stable across versions.** 2.1.0 has `Cross section` then `Type`; 2.2.0 has
   `Type` then `Cross section`. 2.1.0 carries `Begin node`/`End node`; 2.2.0 drops them; the SDK
   writes them back at 2.3.0. **Read by header name, never by position.**
4. **Enum values are not consistently cased.** `beam` and `Beam` and `Column` in one column of one
   file. FEMEX's `JsonStringEnumConverter` is case-insensitive, verified — `"steel"` and `"metre"`
   both read — so this one is survivable, but only by accident.
5. **A value in an enum column need not be in the enum.** B48's `Type = CS26`. Neither the SDK nor
   its FluentValidation pass says a word, and it round-trips verbatim.
6. **`Id` is blank on 42 of the file's rows** — 21 nodes, 7 members, 4 thermal loads, 3 load panels,
   2 curve moments, and one each of `LC3`, `NC1`, `Sb1`, `Sle3` and `TRS1_Z`. A GUID column being
   present does not mean uids exist.
7. **List separators vary.** Semicolons everywhere, **commas** in `StructuralCurveMemberVarying`'s
   section pairs, and **`"N105; N106"` with a space** in `RelConnectsRigidLink`. Split and trim.
8. **Dates are Excel serials.** `43101`.
9. **Trailing blank rows are part of the sheet.** 37 rows in `ResultInternalForce2DEdge`, 4 of them
   real.

---

## 5. The SDK round-trips the reference file to itself with **75 differences**

This is the finding this exercise exists to have produced. Reading
`SAF_example_HOUSE_metric_ZYX_220.xlsx` with the official SDK and immediately writing it back out —
**no FEMEX, no mapping, no conversion, the SDK's own object model in and out** — changes 75 cells:

| Kind | Count | Examples |
|---|---|---|
| **Invented `Id`s** | **42** | every blank `Id` gets a fresh GUID: `B39` → `8861ab47-…`, `N107` → `11f3ebe6-…` |
| Column renamed / added / dropped | 19 | `Begin node`, `End node` restored; `Validity`, `Validity from [m]`, `Local Z direction` added to free surface loads; **`CompositeShapeDef.Id` dropped entirely** |
| Value normalised | 12 | `beam` → `Beam`; `Nonlinear` → **`Non linear`**; `x by vector` → `X by vector`; `"N105; N106"` → `"N105;N106"`; blank `Length` → `0`; blank `Coordinate system` → `Global`; blank `LCS Rotation` → `0` |
| Row added | 1 | `Module version` |
| Spec version | 1 | `SAF Version 2.2.0` → **`2.3.0`** |

Four consequences, all of which land on the plan:

- **Decision 10 must be read strictly at the object level.** *"SAF → FEMEX → SAF must produce a model
  in which every difference is named by a `TransferMessage`"* cannot be asserted cell-by-cell against
  the input workbook, because 75 of those differences are the SDK's and FEMEX cannot name what it did
  not do. Phase B's assertion is: **import the exported workbook and compare object models**, with the
  SDK on both sides of the comparison so its own normalisation cancels.
- **The independent oracle inherits a noise floor.** The plan's *"feed the exported `.xlsx` to SAF's
  own published viewer"* remains the only check against the specification rather than against our own
  reader — and it now has a known 75-cell baseline that is nothing to do with us. Establish that
  baseline first, by feeding the oracle the SDK's own round trip of the reference file, and compare
  against *that*, not against the original.
- **`Invented` starts before the adapter does.** 42 GUIDs are invented by the SDK on write, whatever
  FEMEX does. If a `TransferMessage` claims *"uid preserved"* for an object whose SAF row had no `Id`,
  it is claiming something the layer beneath it disproved. The importer must record **"this uid was
  minted, not read"** at import time — which `AssignMissingUids` already does internally and does not
  currently report.
- **The exporter writes 2.3.0.** Every user-facing string, every report line and every test fixture
  name that says the adapter targets 2.2.0 is wrong on the write leg. The adapter reads 1.0.0–2.3.0
  and writes 2.3.0.

---

## 6. P2 — Position along a member: **decided — carry the position; never mint, never snap**

**The question is forced.** Five of the six positional classes appear in the first real file (§3.7,
§3.8). This is not a corner case to defer past Phase B.

The plan offers three answers and calls all three wrong. They are, and there is a fourth that it
half-names when it says *"`LinearLoad` is closest to a fix… relative start and end positions along
that bar would be additive"*:

> **A position along a member is data about the load, not a fact about the topology.** Store it.

| Type | Addition | Shape |
|---|---|---|
| `PointLoad` | `int? BarId`, `double? Position` | position relative, 0 → 1 from the bar's start node; `BarId` null means the existing `NodeNumber` behaviour, unchanged |
| `LinearLoad` | `double? StartPosition`, `double? EndPosition` | **`BarId` already exists**; these are purely additive |
| `Support`, `Hinge` | `int? BarId`, `double? Position` (`Support`: a start/end pair for `Linear`) | mirrors the `PlateId`/`RegionId` pair `Support` already carries |

Why this and not the other three:

- It **preserves topology**, so `FEMEX_Adapters.md` §7.2 equivalence survives and Phase B's round-trip
  test is meaningful rather than doomed. Minting nodes fails this by construction.
- It is **exactly reversible**, which none of the three plan answers is.
- It **costs one nullable field per type** and nothing at all to any file that does not use it — the
  same argument `ParentUid` makes in §7, and the same shape `LinearLoad.BarId` already set.
- Snapping to the nearer end is not on the table at any point. `FEMEX_BusinessModel.md` §4 says the
  product exists to catch silently-changed answers; an adapter that manufactures one is
  self-refuting.

**Canonical form is relative.** SAF states both (`Coordinate definition` ∈ `Relative | Absolute`,
`Origin` ∈ `From start | From end`) and the file uses all four combinations. Store
relative-from-start; convert absolute using the bar length. That conversion is exact on a straight
member and **`Approximated` on a chorded arc**, because the chord length is not the arc length — one
more thing that only becomes visible once curves and positions are in the same file, as they are in
this one.

**Scope and sequencing.** This is a schema change, so per the plan's own rule it **joins the held
bumps** — 1.9/1.10, before Phase B, not during it. Until it lands the importer reports `Dropped` with
the position in the message text. It does not snap.

**`Repeat (n)` / `Delta x` is decided separately and against the grain: expand on import.** Three
FEMEX loads for `M4`'s one SAF row, reported `Approximated` — *"one SAF object became three; the
values are exact and the grouping is lost"*. Expanding is numerically exact; representing it would
mean a repeat-series concept FEMEX has no other use for. With `ParentUid` (§7) the three carry a
pointer back to `M4` and the export leg can re-collapse them; without it, it cannot.

---

## 7. P3 — `ParentUid`: **decided — yes, one nullable Guid on `IIdentified`, scoped as provenance**

The recommendation was already `FEMEX_SAF_Fit.md` §6.1's. The workbook adds the argument that
settles it:

**SAF has this column, on seventeen of the file's 43 sheets.** `Parent ID` appears on
`StructuralCurveMember`, `…Rib`, `StructuralCurveEdge`, `StructuralSurfaceMember`, `…Opening`,
`…Region`, `StructuralCurveAction`, `…Moment`, `StructuralSurfaceAction`, both thermal action sheets,
`StructuralCurveConnection`, `…EdgeConnection`, `…SurfaceConnection`, `RelConnectsStructuralMember`,
`RelConnectsSurfaceEdge` and `RelConnectsRigidCross`. It is blank on every row of the reference file —
but a blank column is still a column, and it means **`ParentUid` is a pass-through, not an
invention**. FEMEX declining it while
reading a format that carries it is the combination §6.1 identified as the one that makes chording
irreversible rather than merely lossy.

It now has **four** consumers, where the second draft argued from one and a half:

1. A chorded arc's pieces point at the arc, so FEMEX → SAF can re-emit `Geometrical shape = Circular
   Arc` instead of eight `Line`s. This is what makes decision 10's *"one class, chorded curves, is
   reversible only if Phase 0 decides to carry `ParentUid`"* resolve to *reversible*.
2. A2's diff can tell that eight bars are one member — `FEMEX_BusinessModel.md` §3 Claim 2.
3. **The `Repeat (n)` expansion of §6** — three loads that know they were one.
4. **SAF's own `Parent ID` values**, when a producer fills them, have somewhere to land instead of
   being dropped.

**Scope, which §6.1 left open and this decision closes.** `Guid? ParentUid` on `IIdentified`, and
that is all:

- It is a **provenance pointer, not a containment or ownership model.** Nothing in the library
  traverses it, nothing derives from it, and no behaviour changes when it is null — which is every
  object in every file written before it exists.
- **One validation rule**: if stated, it must resolve to some object's `Uid` in the same model.
  Nothing about cycles, depth, or type compatibility — a chord's parent is an arc that is not itself
  a FEMEX object, so the rule must tolerate a parent that is only ever a *former* object.
- It is **not** the thin end of a derivation-tracking design. If one is ever wanted, it will want a
  typed relation and a reason, and this field will be one input to it rather than a half-built version
  of it. That is a decision to take when something needs it; nothing does.

Note SAF places `Parent ID` on geometry, loads and connections but **not** on
`StructuralPointConnection`, materials or cross-sections. FEMEX putting it on `IIdentified` is wider
than SAF and simpler than a per-type list, and costs nothing on the types that never use it.

Ships with the held bumps, before Phase B, because Phase B's round-trip assertion depends on it.

---

## 8. P4 — an unrecognised enum value on a `.femex` read: **decided — catch at the boundary now; tolerant parsing designed and deferred**

**Measured, not assumed.** A probe against the library at `14de087`:

| Input | Result |
|---|---|
| `"lengthUnit": "Metre"` | reads |
| `"lengthUnit": "metre"` | **reads** — the converter is case-insensitive |
| `"lengthUnit": "Furlong"` | **`JsonException`** — `FromJson` throws, the file is unreadable |
| `"schemaVersion": "1.99"` + `"lengthUnit": "Furlong"` | **`JsonException`** — a later-schema file is fatal |
| `"length": "banana"` (the 1.6/1.7 free-text key) | reads; value dropped; **2 validation messages** |
| an unknown *member* (`"somethingNew": 42`) | reads; preserved in `UnknownMembers` |

So the asymmetry the schema summaries flagged twice is real and is worse than stated: **the graceful
path is the legacy one.** A 1.7 file saying `banana` degrades and is named; a 1.9 file saying
`Furlong` is unreadable. And the layer beneath — the SAF SDK — sits at the opposite extreme, carrying
`Type = "CS26"` through a full round trip without comment (§4 item 5).

**Decision, in two parts.**

**(a) The boundary catch is mandatory and lands with Phase A/C.** No `.femex` read failure reaches a
caller as an unhandled exception. `FemexModel.Load`/`FromJson` keep their current behaviour — they are
the library's strict, byte-honest entry point and other code depends on that — and the adapter and CLI
gain a wrapper that returns `TransferResult` with a read-failure message instead. `FEMEX_Adapters.md`
§3.6's *"a failure returns, it does not throw"* is thereby satisfied at the layer that states it, and
Phase C's *"a `.femex` carrying an unrecognised enum value exits **1**, not 2"* becomes true. This is
the plan's own stated minimum and it costs a try/catch.

**(b) Tolerant enum parsing is accepted in principle and deferred, with the reason recorded.** The
shape would be a converter that maps an unrecognised value to null, preserves the raw string in
`UnknownMembers`, and emits a `Validate()` message at **Error** severity. Deferred because:

- `Units.cs` states the standing rule — *"there is not one converter in this repository… the first
  exception would be the expensive one"* — and a custom converter also has to be mirrored in the
  viewer's JavaScript, which A8 exists to police.
- **Silent tolerance is worse than throwing.** A `Restraint.Sense` of `NonLinear` quietly becoming
  null is a support that resists both ways: the silent wrong answer the product exists to catch. So
  the tolerant version is only correct if it is loud, and being loud is most of the work.
- The common real-world case — casing — **already works**, verified above. The remaining case is a
  file from a future schema, which nothing has yet produced.

Revisit when the first 1.9 file meets a 1.8 build, which is a real event with a date rather than a
hypothetical.

---

## 9. P5 — the export-leg invention policy: **decided, and three rows change**

The reference file fills every mandatory column, which is how the invention policy gets checked
rather than guessed. *Invented* per `FEMEX_Adapters.md` §4.3 on every row that is not a pass-through.

| SAF column | Policy | Evidence |
|---|---|---|
| `StructuralMaterial.Type` / `.Quality` | pass through | closed at 1.7; six of six confirmed §3.2 |
| `Model.National code` | write **`EC-Standard-EN`**, report *Invented* | both reference files say exactly that |
| `Model.LCS of cross-section` | write **`ZYX`**, report *Invented* | both reference files say exactly that |
| `Model.System of units` | **`Imperial` iff `Units.Length ∈ {Inch, Foot}` or `Units.Force ∈ {PoundForce, Kip}`, else `Metric`**; report *Invented* always; report **Error** when the model's own units are mixed or unstated | the SDK reads this flag *before* any sheet and drives UnitsNet from it — getting it wrong rescales every number in the file |
| `StructuralLoadCase.Load group` + the `StructuralLoadGroup` sheet | one group per `LoadNature`; `Load group type` = `Dead→Permanent, Live/Wind/Snow→Variable, Accidental→Accidental, Seismic→Seismic`; `Relation` = **`Standard` always**; `Load type` = `Wind→Wind, Snow→Snow, Live→Domestic`. *Invented* per group, and per case for `Live` | the corpus's two producers disagree about `Relation` (§3.9), which is the proof that guessing it is guessing |
| `StructuralCrossSection.Form code` *(conditional)* | **write the matching code 1–8 for the eight FEMEX discriminators** (`ishape→1, box→2, pipe→3, angle→4, channel→5, tshape→6, rectangle→7, circle→8`), no message; **`0` (`-`, provisional) plus *Invented* only for `generic`** | the file's three `Manufactured` IPE180s carry `Form code = 1`, confirming §7.6's reconstruction of 1–8 |
| `Analysis Y/Z Eccentricity` + `System line` *(conditional)* | write `0` and `Centre`; **one *Invented* message per model, not per member** | zero on all 42 rows of every file in the corpus — so the invention is almost always right and stating it 42 times is noise |
| `Name`, on every sheet | synthesise (`B{id}`, `N{id}`, …) where FEMEX's optional name is absent; *Invented* once per sheet | SAF is name-keyed (§1.3) |
| `Id`, on every sheet | `AssignMissingUids`, and **report which uids were minted rather than read** | the SDK invents 42 of them anyway (§5); a silent mint is a false provenance claim |

**Three rows changed from the plan's table.** `Form code` is no longer a blanket `0` — for eight of
FEMEX's nine section kinds the code is *known*, and writing `0` would throw away information the
receiving program can use. `Analysis eccentricity` becomes one message rather than forty-two.
`System of units` gains a rule and an error case instead of *"assume"*.

**And the plan's precondition is met.** Its *Verification* says the oracle *"rejects a workbook
missing mandatory columns"*, so P5 gates that step. Every mandatory column now has a policy, and the
`Form code` and `System of units` rows are the two that would most plausibly have got a workbook
rejected or silently rescaled.

---

## 10. What this makes stale

- **`SAF_Adapter.md` P5's table**, three rows, per §9 above.
- **`SAF_Adapter.md`'s *Verification*, the round-trip line.** Equivalence is asserted on object
  models with the SDK on both sides, not on workbooks — §5.
- **Every "2.2.0" on the write leg.** The SDK writes **2.3.0**.
- **`SAF_Adapter.md`'s *Still open*, the EPPlus entry.** Answered: EPPlus 4.5.3.3, LGPL, transitive,
  no action required — §2.
- **`SAF_Adapter.md`'s *Still open*, the corpus entry.** Sharpened: eleven files, **two models**.
- **`FEMEX_SAF_Fit.md` §4** should gain a ninth silent wrong answer: **load combination category and
  the national-standard reference** (§3.9). `Category = According National Standard` with
  `National standard = EN-ULS(STR/GEO) Set B` loses the combination's entire definition, and
  `Load Factor` × `Multiplier` collapses two numbers into one.
- **`FEMEX_SAF_Fit.md` §6.2's** *"decide after a real file"* on tapers — decided by §3.5: the held
  bump's shape is right, the separator is a comma, and the sheet is a fixed-width repeating group.
- **`FEMEX_SAF_Fit.md` §7.7** is weakened, not strengthened: `Design properties` is blank throughout
  the corpus.

---

## Still open

- **Nothing in the corpus exercises analysis eccentricity.** The held bump that adds it will ship
  against zero real evidence, which is an argument for shipping it and for not spending long on it.
- **Two models is not a corpus.** Graphisoft's and SCIA's published SAF files are the cheap next
  broadening; `FEMEX_BusinessModel.md` §5's *"every engagement is a corpus of real exported files"* is
  the durable answer, and it is still the case that nothing here has been tested against a file
  someone exported from a job.
- **`RelConnectsRigidCross`, `RelConnectsRigidLink`, `RelConnectsRigidMember`** are unmapped in FEMEX
  entirely, and all three are in the reference file. Three objects, `Dropped`, on file one.
- **Variable plate thickness.** One row of the reference file; `SurfaceProperty` has one
  implementation.
- **Whether the adapter should surface the SDK's own `LogEvent` stream in the transfer report.** It
  should; the shape of the message when the SDK and FEMEX both have an opinion about the same object
  is not designed.

---

## Sources, and how to reproduce

- Corpus: `github.com/StructuralAnalysisFormat/StructuralAnalysisFormat-Examples`, `examples/*/*.xlsx`,
  `HEAD` as of 25 August 2026 — 11 files, 1.0.5 → 2.2.0.
- SDK: `StructuralAnalysisFormat` **1.7.3** and `StructuralAnalysisFormat.Bootstrappers.SimpleInjector5`
  **1.7.3**, nuget.org, Apache-2.0.
- Every count, spelling and enum value above was read out of the workbooks directly (the `.xlsx` is a
  zip; the sheets are OOXML) **and** cross-checked against the SDK's own import, which agrees
  object-for-object.
- The 75-difference round trip is `Import(stream)` immediately followed by `Export(stream, model)`
  with nothing in between, compared header-name-keyed with a numeric tolerance of 1e-9.
- The FEMEX-side enum probe in §8 ran against the working tree at `14de087`.
