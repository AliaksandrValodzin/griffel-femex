# Closing the SAF gaps — schema 1.7 → 1.8, and two bumps held

> **Step 0 (repo convention).** Read `Claude/FEMEX_SAF_Fit.md` first — this plan is its
> implementation half and cites its section numbers throughout. Read also `Claude/FEMEX.md` (the
> root design and its "Extended by" ledger), `Claude/FEMEX_StandardSections.md` and
> `Claude/FEMEX_StandardSections_Summary.md` (the closest template for a multi-bump schema change),
> and `Claude/FEMEX_Interop_Status_16082026.md` §5 items 4, 5 and **6**. Two summaries follow this
> document, one per bump, named in *Documentation* below.

## Context

`Claude/FEMEX_SAF_Fit.md` measured FEMEX 1.6 object-by-object against SAF 2.2.0 and found two
things worth acting on. First, **five columns SAF marks mandatory have no FEMEX home at all**, so
an exporter cannot write a workbook SAF's own validator will accept without inventing values
(`StructuralMaterial.Type`, `.Quality`, `StructuralLoadCase.Load group`, `Model.System of units`,
`Model.National code`). Second, **eight concepts cross silently wrong** — models that open,
validate, solve, and are wrong. That second class is the one that matters commercially:
`FEMEX_BusinessModel.md` §4 defines the product as catching exactly that failure, so an adapter
that manufactures it is off-brand in a way a merely lossy one is not.

An earlier draft of this plan implemented all seven of §8.1's items **plus the four silent wrong
answers §8.1 left out**, as four themed bumps 1.7 → 1.10. **That scope is now cut in half.** This
plan lands **two** bumps — materials, then units and restraint sense — and **holds** load groups and
member variation until one real SAF file has been read.

### Why two and not four

Three documents in this repository already argued for the smaller scope, and the earlier draft did
not engage with any of them squarely.

- **`FEMEX_BusinessModel.md` §9** retained exactly two items against its general demotion of P1
  work: *"Items 4 and 5 (material completeness, units as enums) stand: both are small, and both are
  what make the numbers in a check report mean anything."* Those two are this plan's 1.7 and 1.8.
  The same section demotes the nine P1 entities **below the SAF adapter and the diff**. It never
  retained load groups — the earlier draft's defence, that *"the two items with real shape are the
  two `FEMEX_BusinessModel.md` §9 already retained on independent grounds"*, conflated load groups
  with units and is withdrawn.
- **`FEMEX_Interop_Status_16082026.md` §5 item 6**: *"building nine more P1 entities against
  documentation, before a single real file has been read, is how a format acquires the wrong
  vocabulary confidently."*
- **`FEMEX_SAF_Fit.md`'s own *Still open***: items 4–7 *"are single properties with one obvious
  spelling each and are hard to get wrong. Items 1–3 involve enums and shapes, and are exactly what
  a real file would inform."* `LoadGroupRelation`, `LoadGroupType`, `BarAlignment`'s nine values and
  `BarEccentricity`'s eight fields are all enums and shapes.

The cost is the other half of the argument. Four bumps is roughly **+80 facts**, four sequential
schema versions and eight careful hand-edits across two byte-identity-gated example files — all
before `AdaptersPlans/SAF_Adapter.md` Phase A2, the diff utility `FEMEX_BusinessModel.md` §7 moved
to the front of Phase A *because it is a product surface*, exists at all. For a solo, part-time,
unfunded project that is the wrong order.

Holding costs nothing irreversible. Both held bumps are additive, both keep their design work in
this document under *Held pending a real SAF file*, and both are cheaper to get right after Step 0'
than to correct after shipping.

### Two findings from re-reading the doc against the code

- **§3's mandatory list is one short.** §4 item 4 records that SAF's
  `Analysis Y/Z Eccentricity (Begin)/(End)` and `System line` are *mandatory* on every
  `StructuralCurveMember`, against nothing on `Bar`. That is a sixth un-writable mandatory column.
  It is also pure invented shape, which is why it is now held rather than built.
- **`GetShearModulus()` is called nowhere** — verified: `Materials/Material.cs:107` is the only
  `.cs` hit in the repository. So making a stated `G` authoritative over the derived one (§4 item 8)
  has no call sites to audit.

### What is deliberately not implemented

`Model.National code` — per §8.1, a national code is a statement about a design process, not about a
model. The adapter reports it as *Invented*, the same treatment `FEMEX_Adapters.md` §6.6 gives
units.

`Model.System of units` — **and this is a correction to the earlier draft**, which claimed 1.10
closed §3 row 4. SAF's mandatory column is `Metric | Imperial`, one flag about the whole model. Five
independent per-quantity enums do not supply it, and they permit combinations (`Metre` + `Kip`) that
map to neither value. The typed enums are still worth having — they are what make the numbers in a
check report mean anything — but the mandatory column itself stays *Invented*, and so does
`Model.LCS of cross-section`, §3's second mandatory conditional, which the earlier draft did not
mention at all.

---

## Conventions this plan follows

Established by `FEMEX_StandardSections.md` / `_Summary.md`, verified in the source:

1. One type per file, XML doc arguing the design **and what was deliberately excluded**. A new
   enum must argue why it is closed where a sibling is free text (`SectionManufacture.cs:4-11` is
   the template, and states the argument in those terms).
2. **New properties nullable with no initializer**, so `DefaultIgnoreCondition = WhenWritingNull`
   (`FemexModel.cs:157`) keeps existing files byte-identical — a model not using a feature gains not
   one byte. Note `WriteIndented = true` (`:156`): byte identity is sensitive to **declaration
   order**, so a new property must be declared where the example files expect its key to appear.
3. Every new nested type implements `IExtensible` with its **own** `[JsonExtensionData]` property —
   `IExtensible.cs:30-32` states outright that the attribute is not inherited through the interface
   — and is registered in `FemexModel.Unknown.cs:32::EnumerateExtensible()`. Authored, mergeable
   entities also implement `IIdentified` and register in
   `FemexModel.Identity.cs:70::EnumerateIdentified()`.
4. No custom converters and no `[JsonPropertyName]` except legacy shims — the global
   `JsonStringEnumConverter` + camelCase policy in `FemexModel.cs:151-159` covers everything.
5. Rules return `IEnumerable<string>`; **severity is assigned once, at the call site** in
   `Validate()` (`FemexModel.Validation.cs:22-61`). That method is **three** blocks, not two: errors
   at `:26-40`, warnings at `:45-50`, then a **mixed** geometric block at `:54-60` carrying both
   severities, deliberately last because those are the only rules needing coordinates. Anything
   inserted after `:53` must state its severity explicitly rather than rely on "the warning block".
   A feature normally brings *two* validators. New referenced collections get a set in
   `ValidationContext` (`:1640`).
6. Per bump: `CurrentSchemaVersion` in `FemexModel.cs:61` plus a clause in its doc comment; the
   outgoing version appended to `ReadableSchemaVersions` (`:71`) and to `SelfWeightVersions`
   (`FemexModel.Validation.cs:984`); a branch in `ValidateSchemaVersion()` (`:889`).
7. Migrations hang off the single `IJsonOnDeserialized.OnDeserialized()` hook
   (`FemexModel.SelfWeight.cs:49`), record what they did in private non-serialised fields, and
   report through `ReportMigrations()` — which lives in **`FemexModel.Validation.cs:1079`**, wired
   as a warning at `:46`, not beside the hook. So a migration touches **three** files: the type
   (getter-less setter plus private field), the hook, and the report string.
   `Materials/Material.cs:48-57` (`unitWeight` → `density`, drained by `TryTakeLegacyUnitWeight` at
   `:91`) is the pattern for renaming a field across a version.
8. Tests: one `<Feature>Tests.cs`, facts named `Reports_` / `Warns_` / `Accepts_` / `_RoundTrips` /
   `_OmitsTheKeyEntirely`, built on `SampleModels.Build()` (`SampleModels.cs:51`) — which must keep
   validating **silently**. `SectionTests.cs` is the closest template.
9. Both `Examples/*.femex` get their `schemaVersion` bumped each time and must stay byte-identical
   under `Example1_ReSerializesToItself` (`RoundTripIdentityTests.cs:508`) and
   `Example2_ReSerializesToItself` (`SectionTests.cs:480`) — which live in **different** files.
   **Where a bump renames a JSON key, bumping the version is not enough**; see 1.8 and
   *Verification* gate 2. Example-touching tests live in **six** files:
   `SectionTests.cs:457,480,487`, `RoundTripIdentityTests.cs:484,508`, `ValidationTests.cs:735`,
   `SelfWeightTests.cs:413`, `LoadDirectionTests.cs:491`, `MetadataTests.cs:116`.

---

## 1.7 — Materials

Closes §3 rows 1–2, §4 item 6 (second half), §4 item 8. Also closes `FEMEX_Interop_Review.md`
§5.5 and status item 4. The bump with two independent arguments behind it — the business model
reached it from the report's credibility, `FEMEX_SAF_Fit.md` from SAF's required-column list.

**New files**

| File | What |
|---|---|
| `Materials/MaterialType.cs` | `Concrete, Steel, Timber, Aluminium, Masonry, Other` — SAF's set exactly. Closed because it is small and closed, the `SectionManufacture` argument. |
| `Materials/MaterialProperties.cs` | `class MaterialProperties : IExtensible`, all `double?`, SAF's 22 `Design properties` under three comment headings — steel `Fy, Fu, FuMinimum, Ry, Rt`; concrete `Fck, Fcm, Fctm, Fctk05, Fctk95, EpsC2, EpsCu2, EpsC3, EpsCu3`; timber `E005, E90Mean, Fmk, Ft0k, Ft90k, Fc0k, Fc90k, Fvk`. |

`MaterialProperties` deliberately copies `Geometry/Sections/SectionProperties.cs` — the same
per-type property bag that lets a value cross when the receiver does not recognise the grade.
Mirror its doc-comment argument: every field `double?` so "not stated" is distinct from zero.

**Modified `Materials/Material.cs`**

- `MaterialType? Type` — nullable, so 1.6 files gain nothing.
- `string? Quality` — the grade designation (`S235`, `C25/30`), distinct from `Name`, which is a
  free label programs key by.
- `double? ThermalExpansion` — α in 1/K. This is what makes `TemperatureLoad` mean anything;
  the doc calls its absence *"an internal inconsistency, not just an omission"*.
- `double? ShearModulus` — G, and `GetShearModulus()` (`:107`) becomes
  `ShearModulus ?? ModulusOfElasticity / (2 * (1 + PoissonsRatio))`. **Stated wins over derived**,
  the identical rule `Section.GetArea()` (`Geometry/Sections/Section.cs:75`) already states for
  area; reuse its doc-comment wording. No call sites to audit — the method is currently dead code.
- `MaterialProperties? Properties`.
- `Strength` (`:63`) stays (removing it is a break) and its doc gains one sentence saying
  `Properties` is where a design value belongs from 1.7 on.

**Validation** — **two new methods and two new wirings, not edits.** There is no `ValidateMaterials`
today: materials are currently reached only by `ValidateDuplicateIds` (`:78`), `ValidateNameKeys`
(`:201`) and reference resolution from bars and plates (`:430-431`, `:491-492`), and nothing checks
a material's own numbers. Add `ValidateMaterials()` into the **error** block (`:26-40`) and
`ValidateMaterialCompleteness()` into the **warning** block (`:45-50`), placed beside
`ValidateSections` (`:556`) / `ValidateSectionCompleteness` (`:627`) and modelled on them:

- Error: a stated `MaterialProperties` value, `ThermalExpansion` or `ShearModulus` that is not
  strictly positive. Same reasoning as `ValidateSections` — a stated property is a claim, and zero
  is not a claim a solver can build with.
- Warning: a material stating no `Type`; a material stating `Quality` with no `Type` (a grade is
  meaningless without the code family it belongs to); a `TemperatureLoad` whose elements resolve to
  a material with no `ThermalExpansion` — the §4 item 6 inconsistency, made executable. Warning on
  *absence* has precedent even though `ValidateSectionCompleteness` warns only on incoherent claims:
  `ReportNameKeys` (`:218-238`) already warns that an unnamed entity will have a name invented for
  it, which is the same argument.

**Register** `material.Properties` in `EnumerateExtensible()`.

**Tests** `MaterialTests.cs` — the file does not exist yet. Both example files gain a `type` on each
material so the new warning does not fire on them. No key is renamed at this bump, so convention 9's
plain form holds and the units and thermal blocks are untouched.

---

## 1.8 — Units, restraint sense, bedding semantics

*(The earlier draft's 1.10, renumbered.)* Closes §4 items 2 and 7, review §5.7 and §5.9, and status
item 5. **Reports** §3 row 4 rather than closing it, per *Context*. The **only non-additive** change
among the bumps that proceed.

**New files** — `LengthUnit.cs`, `ForceUnit.cs`, `TemperatureUnit.cs`, `AngleUnit.cs`,
`MassUnit.cs` at the repository root beside `Units.cs`, plus
`BoundaryConditions/RestraintSense.cs`.

Suggested members: length `Millimetre, Centimetre, Metre, Inch, Foot`; force
`Newton, Kilonewton, Meganewton, PoundForce, Kip`; temperature `Celsius, Fahrenheit, Kelvin`;
angle `Degree, Radian`; mass `Kilogram, Tonne, Pound, Slug`. Each enum's doc must argue why it is
closed — the set of units an analysis model uses is small and closed, unlike
`SectionCatalogue.Source`. `Mass` is annotation only, like the others: `Material.Density` remains
in the unit consistent with the model's own force and length, as its doc comment already states.

`Units`' class doc must also record what these five enums **do not** supply: SAF's mandatory
`Model.System of units` is a single `Metric | Imperial` flag, five independent enums can express a
mixed model (`Metre` + `Kip`) that maps to neither, and an exporter therefore reports the system as
*Invented*. `Model.LCS of cross-section` is recorded in the same sentence, for the same reason.

**Modified `Units.cs`** — today `string? Length` (`:12`) and `string? Force` (`:15`), free text with
only comment-level guidance. The migration follows `Material.UnitWeight` exactly:

```csharp
// The typed 1.8 spellings.
[JsonPropertyName("lengthUnit")] public LengthUnit? Length { get; set; }
[JsonPropertyName("forceUnit")]  public ForceUnit?  Force  { get; set; }
public TemperatureUnit? Temperature { get; set; }
public AngleUnit?       Angle       { get; set; }
public MassUnit?        Mass        { get; set; }

// The 1.6/1.7 free-text spellings, bound on read so nothing is dropped in silence.
// Getter-less: System.Text.Json can never write them back, so a 1.8 file
// cannot contain them.
[JsonPropertyName("length")] [EditorBrowsable(Never)] public string LegacyLength { set { ... } }
[JsonPropertyName("force")]  [EditorBrowsable(Never)] public string LegacyForce  { set { ... } }
```

New JSON keys for the typed properties, because `"length": "m"` and `"length": "Metre"` cannot
share a key without a custom converter, and a converter would be the first in the repository.
`MigrateLegacyUnits()` hangs off `OnDeserialized()` (`FemexModel.SelfWeight.cs:49`), drains its
private fields the way `TryTakeLegacyUnitWeight` (`Material.cs:91`) does, parses the common symbols
case-insensitively, and reports through `ReportMigrations()` (`FemexModel.Validation.cs:1079`).
**Unparseable text is not carried** — `"length": "banana"` becomes no length unit at all, and the
report says so by name. That text round-tripping clean is the exact defect §3 row 4 cites, so losing
it loudly is the point of the change, not a regression.

**The example files must be hand-migrated at this bump, not merely version-bumped.** Both carry
`"length": "m"` / `"force": "kN"` (`Example1.femex:8-11`, `Example2.femex:8-11`). Under the design
above, re-serialisation emits `lengthUnit`/`forceUnit` and **cannot** emit the old keys, so
convention 9's plain form fails twice over:

- `Example1_ReSerializesToItself` and `Example2_ReSerializesToItself` break **gate 2**.
- `ReportMigrations()` is wired as a **warning** at `:46`, so `Example2_LoadsAndValidates`
  (`SectionTests.cs:457`) and `Example1_LoadsAndValidates_AfterTheBump` (`:487`) — both asserting
  `Assert.Empty(model.Validate())` — break **gate 3**.

Rewrite both units blocks to the typed spellings. Migration coverage then lives entirely in the raw
JSON-literal tests, which is where gate 4 already puts it, so nothing is lost.

**Modified `BoundaryConditions/Restraint.cs`** — `RestraintSense? Sense`, null meaning
bidirectional, beside the existing `bool Fixed` (`:16`) and `double? Stiffness` (`:19`). Enum:
`Both, CompressionOnly, TensionOnly`. Crossed with the existing pair this reaches **seven of SAF's
eight** translation states:

| SAF | FEMEX |
|---|---|
| Rigid | `Fixed = true` |
| Free | `Fixed = false`, `Stiffness = null` |
| Flexible | `Stiffness = k` |
| Compression only | `Fixed = true`, `Sense = CompressionOnly` |
| Tension only | `Fixed = true`, `Sense = TensionOnly` |
| Flexible compression only | `Stiffness = k`, `Sense = CompressionOnly` |
| Flexible tension only | `Stiffness = k`, `Sense = TensionOnly` |
| Non linear | **unmapped** — a stiffness curve, not a state |

The class doc must record that eighth explicitly. It must also record that `Support`
(`BoundaryConditions/Support.cs:32-37`) applies `Restraint` uniformly across all six DOFs, so a
**rotational** compression-only restraint is representable and meaningless — one sentence, or a
warning if it is cheap where the other new rules go.

This corrects `FEMEX_Interop_Review.md` §3.5's claim that the 6-DOF pattern is *"correct, and
correctly factored"* — true of the shape, and it was the value set that was short.

**Bedding semantics** — documentation and one validation rule, no schema change (§8.1 item 6, the
cheapest item in the whole document and the only one closable by writing a sentence). State on
`Restraint.Stiffness` and again on `Support` what the number means per `SupportTarget`
(`SupportTarget.cs:6-11`): a **total spring** (force/length) for `Point`, per unit **length** for
`Linear`, and a **bedding modulus per unit area** (force/length³ — SAF's Winkler `C1`) for `Area`.
Also record that SAF's Pasternak `C2` is deliberately unmapped. Add a warning when an `Area` support
states a `Stiffness` and the model states no `Units` — the executable half, since the number is
meaningless without them.

**Tests** `UnitsTests.cs` (typed round-trip, legacy `"m"`/`"kN"` migration from a raw JSON literal,
`"banana"` reported and dropped) and additions to `ValidationTests.cs` for `Sense` and the bedding
warning.

---

## Step 0' — one real SAF workbook

**Between the bumps that proceed and the two that are held.** The earlier draft said the real-file
tension was *"not one this plan pretends to resolve"* and treated a real file as something an
engagement supplies. It is not, and that framing is what let the scope grow to four bumps.

`FEMEX_SAF_Fit.md`'s *Sources* records **11 published `.xlsx` files** at
`github.com/StructuralAnalysisFormat`, spanning 1.0.5 → 2.2.0, one or two of them at 2.2.0.
`AdaptersPlans/SAF_Adapter.md` B1 records the SDK as an ordinary NuGet reference
(`StructuralAnalysisFormat 1.7.3`, with EPPlus pinned to `[4.5.3.3]` for the licence reason given
there). Opening one 2.2.0 workbook and reading four sheets is about a day, not a blocked dependency.

The step: fetch one 2.2.0 workbook; read `StructuralLoadGroup`,
`StructuralSurfaceActionDistribution`, `StructuralCurveMemberVarying` and `StructuralMaterial`;
record what was actually found — column names, enum spellings, which mandatory columns real files
leave blank, whether varying members use one span or several — in
`Claude/FEMEX_SAF_Corpus_Notes.md`. That confirms or corrects every enum spelling and both invented
shapes below, and confirms 1.7's `MaterialType` retroactively. It is
`FEMEX_Interop_Status_16082026.md` §5 item 6 applied at the scale it was written for, and it turns
this plan's largest stated risk into a bounded task.

---

## Held pending a real SAF file

Neither section is deleted. Both are designed as far as a specification alone can take them; both
wait for Step 0' and are then renumbered 1.9 and 1.10. Each carries a defect found in review that
must be fixed when it resumes.

### Held — loads

*Would close §3 row 3 (the mandatory `Load group` reference), §4 item 3 (spanning direction), §4
item 6 (first half — the gradient axis), and review §5.8.*

**New files**

| File | What |
|---|---|
| `Loads/LoadGroup.cs` | `class LoadGroup : IIdentified, IExtensible` — `int Id`, `Guid? Uid`, `string? Name`, `LoadGroupType Type`, `LoadGroupRelation Relation`. |
| `Loads/LoadGroupType.cs` | `Permanent, Variable, Accidental, Seismic, Tensioning` — SAF's set. |
| `Loads/LoadGroupRelation.cs` | `Standard, Exclusive, Together`. This is the part `LoadNature` cannot express and the reason the group is an entity rather than a string. |
| `Geometry/SurfaceLoadSpanning.cs` | `TwoWay, OneWayX, OneWayY`. |
| `Geometry/LoadDistribution.cs` | `class LoadDistribution : IExtensible` — `SurfaceLoadSpanning Spanning`, `double RotationAngle` (degrees, rotating the panel's local x, per `TryGetPlateLocalAxes`), `List<int>? BarIds` (the members that receive the load; null = whatever bounds the panel). |

**Modified** — `FemexModel.cs` gains `List<LoadGroup> LoadGroups` immediately **before** `LoadCases`,
because a case references a group; `Loads/LoadCase.cs` gains `int? LoadGroupId`; `Geometry/Plate.cs`
and `Geometry/PlateRegion.cs` gain `LoadDistribution? Distribution`, null on a region inheriting the
plate and null on a plate meaning two-way — following `PlateRegion`'s existing inherit-from-plate
rule for `SurfacePropertyId` (`:41-43`), `Alignment` (`:52-53`) and `SurfaceOffset` (`:55-57`).
Spanning lives on the **panel**, not the load: a slab spans one way for every load on it, and two
loads on one panel must not be able to disagree about how it spans.

`Loads/TemperatureLoad.cs` gains `double? GradientY` and `double? GradientZ`, **signed**, referenced
to the element's local axes (`FemexModel.LocalAxes.cs:38,86`): positive means temperature increases
along the +local axis. `GradientPerDepth` (`:17`) becomes a getter-less legacy shim
(`[JsonPropertyName("gradientPerDepth")]`, `[EditorBrowsable(Never)]`, the `Material.UnitWeight`
pattern) migrated on read into `GradientZ`.

**Defect to fix on resume — the migration would assign meaning to a real number.**
`Examples/Example1.femex:3539` carries `"gradientPerDepth": 30`, and the 1.6 field carried **no sign
convention at all** — that absence is precisely what §4 item 6 complains about. Migrating it picks a
sign nobody ever stated, on a real file in this repository, inside a plan whose premise is that a
model which opens, solves and is wrong is the failure the product exists to catch. Do not let the
migration choose: read which bar the load sits on, work out which way `TryGetBarLocalAxes` points
its local z there, decide deliberately, and record the decision and its reasoning in the summary. If
the intent is genuinely unrecoverable, say that rather than choosing silently. The rename is also
**non-additive** by the same mechanism as 1.8's units change, and worse in kind — a number changing
meaning rather than a string being dropped loudly — so `ReportMigrations()` must call it a
*reinterpretation* in those words, and `Example1.femex` must be hand-migrated exactly as 1.8's units
blocks are.

**Defect to fix on resume — two sources of truth for load category.** After this bump a `LoadCase`
carries both `Nature` (`LoadCase.cs:27` → `LoadNature.cs:7`: `Dead, Live, Wind, Seismic, Snow,
Accidental, Temperature`) and `LoadGroupId` → `LoadGroupType` (`Permanent, Variable, Accidental,
Seismic, Tensioning`). They overlap almost entirely and can disagree: nothing would stop
`Nature = Dead` in a group typed `Variable`, and combination factors are exactly what that changes.
That is a manufactured silent wrong answer, introduced by a bump written to close one. The fix is
cheap — one warning over a stated compatibility map (`Dead → Permanent`;
`Live | Wind | Snow | Temperature → Variable`; `Accidental → Accidental`; `Seismic → Seismic`;
`Tensioning` has no `LoadNature` equivalent, itself worth a sentence in the enum's doc comment) —
but it must be designed in, not discovered afterwards.

**Validation** — `ValidationContext` (`:1640`) gains `LoadGroupIds`; `ValidateDuplicateIds` (`:71`)
gains load groups. Error: `LoadCase.LoadGroupId` referencing an unknown group;
`LoadDistribution.BarIds` referencing unknown bars. Wording template:
`"{Owner} references unknown {kind} {id}."` Warning: a `TemperatureLoad` stating `GradientY` on a
plate element (a plate has one through-thickness axis and the other gradient has nowhere to go); a
load group naming no cases; the nature/type disagreement above; `ValidateNameKeys` (`:188`) extended
to load groups, which SAF keys by name.

**Register** load groups in both `EnumerateIdentified()` and `EnumerateExtensible()`; the
`Distribution` blocks in `EnumerateExtensible()`.

**Tests** `LoadGroupTests.cs`, plus additions to `LoadDirectionTests.cs` for the gradient migration
(a raw 1.6 JSON literal carrying `gradientPerDepth`, asserting it lands in `GradientZ` and that
`Validate()` reports the reinterpretation).

### Held — members

*Would close §4 items 1, 4 and 5, and review §5.2 and §5.3. The largest of the four bumps and the
one carrying the most invented shape — which is why it waits for Step 0'.*

**New files**

| File | What |
|---|---|
| `Geometry/BarBehaviour.cs` | `Standard, AxialOnly, CompressionOnly, TensionOnly` — SAF's four values exactly. Note in the doc that `PlateBehaviour.CompressionOnly` (`Geometry/PlateBehaviour.cs:20`) is the sibling concept on surfaces. |
| `Geometry/BarAlignment.cs` | The system line, SAF's nine: `Centre, Top, Bottom, Left, Right, TopLeft, TopRight, BottomLeft, BottomRight`. Sibling of `Geometry/SurfaceAlignment.cs:8-13`, whose three-value doc-comment style it should copy. |
| `Geometry/BarEccentricity.cs` | `class BarEccentricity : IExtensible` — `double?` × 8: `StructuralYBegin/ZBegin/YEnd/ZEnd` and `AnalysisYBegin/ZBegin/YEnd/ZEnd`, in the bar's local y and z. |

The two eccentricity families are SAF's split and the doc calls it *"the most honest model of the
three and worth copying"*: **Structural** is the BIM offset and does not change internal forces;
**Analysis** moves the analysis line and does. The class doc must say that plainly, because a
receiver that collapses the two produces geometry that looks right and stiffness that is wrong.

**Modified `Geometry/Bar.cs`** — which today carries only `StartNodeId`/`EndNodeId` (`:28-29`),
`SectionId` (`:32`), `MaterialId` (`:35`) and `RotationAngle` (`:40`):

- `BarBehaviour? Behaviour` — null means `Standard`, which is exactly what a 1.6 file means.
  Nullable rather than a defaulted non-nullable so no existing file gains `"behaviour": "Standard"`
  on every bar.
- `BarAlignment? Alignment` — null means `Centre`.
- `BarEccentricity? Eccentricity`.
- `int? EndSectionId` — null is prismatic; when set, the section varies linearly from `SectionId`
  at the start node to `EndSectionId` at the end. `SectionId` stays the fallback a receiver that
  ignores the taper builds from, the same degrade-don't-lose rule sections already follow. The
  `tapered` discriminator reserved in `Geometry/Sections/Section.cs:24-27` stays reserved and
  unimplemented — a taper is a property of the member, not a kind of section.

Update `Bar`'s class doc, which currently documents only the local-axis convention, to add the
eccentricity frame — it is the same local y/z that `TryGetBarLocalAxes` produces, so nothing new is
invented.

**Correction to the earlier draft — this downgrades §4 item 5, it does not close it.** SAF's
`StructuralCurveMemberVarying` states a member as *spans*, each with its own section and alignment,
relative spans summing to 1.0. A single linear taper converts a silent wrong answer into a reported
*Approximated*, which is worth having — but the common real case, a rafter haunched at **both** ends,
still arrives with the wrong moment distribution, now with a message attached. Word it as *downgrades
to Approximated*, and let Step 0' say whether real files use one span or several.

**Validation** — `ValidateBars` (`:420`) gains the reference check; a new `ValidateBarCompleteness()`
goes in the warning block. Error: `EndSectionId` referencing an unknown section. Warning:
`EndSectionId` equal to `SectionId` (says nothing; use null); a taper between two sections with
different `type` discriminators (nothing can build it); an `Eccentricity` block whose eight values
are all null (an empty claim, mirroring `ValidateSectionCompleteness`); a
`TensionOnly`/`CompressionOnly` bar that also carries a hinge releasing `Ux`.

**Note for that last rule** — `Bar` carries no `Hinge`. Hinges are a top-level list
(`FemexModel.cs:128`) pointing back via `Hinge.ElementId` (`BoundaryConditions/Hinge.cs:25`), so the
warning needs an element-id → hinge lookup added to `ValidationContext` (`:1640`), which the earlier
draft did not list.

**Register** `bar.Eccentricity` in `EnumerateExtensible()`.

**Tests** `BarTests.cs` — the file does not exist yet; behaviour round-trip, taper round-trip,
eccentricity round-trip and additivity (`Assert.DoesNotContain("\"eccentricity\"", json)`), plus
one fact per new message.

---

## Critical files

Touched at both bumps: `FemexModel.cs` (`:61` `CurrentSchemaVersion`, `:71`
`ReadableSchemaVersions`, and the version-ledger doc comment), `FemexModel.Validation.cs`
(`Validate()` wiring at `:22-61`, `ValidateSchemaVersion` `:889`, `SelfWeightVersions` `:984`,
`ReportMigrations` `:1079`, `ValidationContext` `:1640`), `FemexModel.Unknown.cs:32`
(`EnumerateExtensible`), `Examples/Example1.femex`, `Examples/Example2.femex`,
`griffel-femex.Tests/SampleModels.cs`.

Per bump: `Materials/Material.cs` · `Units.cs`, `BoundaryConditions/{Restraint,Support}.cs`,
`FemexModel.SelfWeight.cs:49` (the `OnDeserialized` hook).

`FemexModel.Identity.cs:70` is untouched by either bump — the held loads bump is the one that adds
an identified entity.

## Documentation

Per the repository's pair convention, one summary per bump, written after the code:
`FEMEX_MaterialCompleteness_Summary.md` and `FEMEX_UnitsAndRestraints_Summary.md`. Each carries
`## New files`, `## Modified`, `## Verified`, `## Deviations from the plan`, `## Still open`, and
the before/after fact count. This document is the plan half for both, and holds the design for the
two that wait.

`Claude/FEMEX.md` gains **two** "Extended by" blockquotes in its root-design section, matching the
existing seven.

Docs this pass makes stale, to be corrected at the end: `FEMEX_SAF_Fit.md` §8.1 — marked **items 1,
3, 6 and 7 closed; items 2, 4 and 5 held pending corpus** — and §4 items 2, 6 (second half), 7 and
8; `FEMEX_Interop_Review.md` §3.5, §5.5, §5.7 and §5.9;
`FEMEX_Interop_Status_16082026.md` §3 and §5 items 4 and 5.

Deliberately **not** corrected yet: `FEMEX_Interop_Review.md` §2.2's priority column, §5.2, §5.3 and
§5.8, which stay stale until the held bumps land; and `FEMEX_SAF_Fit.md` §3 row 4, which is not
closed — the mandatory column is reported, not filled.

## Deliberately not in scope

Everything in §8.3, which the doc says should be decided only after a real SAF file has been read:
`ParentUid` and curved geometry (§6.1), position along a member (§7.1 — the largest single
omission), loads and supports on plate edges and internal edges (§7.2–7.3), free point and line
loads (§7.4), form codes 9–23 (§7.6), ribs (§6.3), and results (§6.5). `Model.National code`,
`Model.System of units` and `Model.LCS of cross-section` are excluded as columns and reported as
*Invented*. All of these stay *Unmapped* or *Approximated* and are reported by the adapter per §8.2.

---

## Verification

The test project is **not in `griffel-femex.sln`** — the solution contains exactly one project
(`griffel-femex.sln:6`), so a bare `dotnet test` at the repo root runs zero tests silently. After
each bump:

```
dotnet build griffel-femex.csproj
dotnet test griffel-femex.Tests\griffel-femex.Tests.csproj
```

Target: 0 warnings, 0 errors, and roughly +20 facts per bump from **254** (~295 after 1.8, rather
than the earlier draft's ~335 after four). Each summary records the before/after count, as every
prior summary does.

Per-bump gates, all of which have a precedent in `SectionTests.cs`:

1. **Additivity.** A model that does not use the feature gains not one byte —
   `Assert.DoesNotContain("\"shearModulus\"", json)` and equivalents.
2. **Byte identity.** `Assert.Equal(File.ReadAllText(path), FemexModel.Load(path).ToJson())` for
   both example files. At 1.7 that means bumping `schemaVersion` and adding a material `type`. At
   1.8 the units blocks are **hand-rewritten** to `lengthUnit`/`forceUnit` as well — a bump that
   renames a JSON key cannot leave the examples carrying the old spelling. `WriteIndented = true`
   means new properties must be declared where the example files expect their key to appear.
3. **Silence.** `SampleModels.Build()` must still satisfy `Assert.Empty(model.Validate())`, and so
   must both examples — the same assertion appears at `SectionTests.cs:457` and `:487`. Note that
   `ReportMigrations()` feeds `Validate()` as a **warning** (`FemexModel.Validation.cs:46`), so a
   file that still needs migrating is not silent. The sample gains a material `type` and whatever
   else a new warning demands.
4. **Backward read.** A raw 1.6 JSON literal (C# `"""` string) opens, migrates, and `Validate()`
   names what the migration did — specifically `"length": "m"` → `lengthUnit`, and
   `"length": "banana"` dropped and named, at 1.8.
5. **Forward read.** A literal carrying an invented future member on each new type survives in
   `UnknownMembers` and is named by `Validate()`.

End-to-end, once 1.8 lands: hand-author a small model exercising a typed material with `Quality`, α
and a stated `G`, typed units, an uplift-free bearing (`Fixed = true`, `Sense = CompressionOnly`)
and an area support stating a bedding modulus; save it; reopen it; confirm byte identity and that
`Validate()` is silent. That model becomes `Examples/Example3.femex` — remember the per-file
`<None Include>` line in `griffel-femex.Tests.csproj:33-38`, which is deliberately not a glob and
fails with `FileNotFoundException` if omitted. The tension-only brace, the haunched rafter and the
one-way slab panel join it when the held bumps land.
