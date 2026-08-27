# Load groups, panel spanning, signed gradients, positions along a member — Implementation Summary

Implemented bump **1.9**, the first of the two held bumps of `Claude/FEMEX_SAF_Fit_Update_Plan.md`,
as Phase A′ of `Claude/AdaptersPlans/SAF_Adapter.md` schedules it. Clean build (0 warnings, 0 errors,
both legs); **431 tests pass** (was 390 after Phase A). The second held bump, 1.10, follows in
`FEMEX_MemberVariation_Summary.md`.

Three of `FEMEX_SAF_Fit.md` §4's eight *silently wrong* concepts close here, and each of them is the
same failure in a different place: a model that opens, validates, solves, and is wrong.

**A load case belonged to no group.** SAF marks `StructuralLoadCase.Load group` mandatory, so an
exporter could not write a workbook without inventing one — and the invention is not free, because a
group carries a `Relation` that decides whether its cases may act at once. `LoadNature` cannot
express that.

**A panel could not say which way it spans.** A one-way slab read as a two-way one puts half its load
on the wrong beams. Nothing in 1.8 could state otherwise, and
`Claude/FEMEX_SAF_Corpus_Notes.md` §3.10 found all three of SAF's spanning values, a 45-degree frame
rotation and an explicit member list exercised in the one reference workbook.

**A thermal gradient had no sign convention.** `gradientPerDepth` was a bare number, and which face of
the element is the hot one decides which way it curves.

Two further items land here because Phase 0 decided them and they are load-side:
**position along a member** (P2) and **`ParentUid`** (P3).

---

## New files

| File | What |
| --- | --- |
| `Loads/LoadGroupType.cs` | `Permanent, Variable, Accidental, Seismic, Tensioning` — SAF's set, all five present in the reference workbook |
| `Loads/LoadGroupRelation.cs` | `Standard, Exclusive, Together` — the part `LoadNature` cannot express |
| `Loads/LoadGroup.cs` | `class LoadGroup : IIdentified, IExtensible` — `Id`, `Uid`, `ParentUid`, `Name`, `Type`, `Relation` |
| `Geometry/SurfaceLoadSpanning.cs` | `TwoWay, OneWayX, OneWayY` |
| `Geometry/LoadDistribution.cs` | `class LoadDistribution : IExtensible` — `Spanning`, `RotationAngle`, `BarIds?` |
| `FemexModel.Gradients.cs` | the 1.8 → 1.9 migration: `MigrateLegacyGradients()` and the two private fields `ReportMigrations()` drains |
| `griffel-femex.Tests/LoadGroupTests.cs` | 33 facts, including a seven-case `[Theory]` over the nature/type compatibility map |

Each enum's doc argues why it is **closed** where the thing beside it is free text — the
`SectionManufacture` / `MaterialType` argument applied again: what a group is *called* is a project's
business and unbounded, whereas the set of categories a design code combines by is small and has been
the same five for as long as anyone has written one.

`LoadGroupRelation`'s doc records the finding that makes the invention policy honest rather than
convenient: **the corpus's two producers disagree about which relation a wind or snow group takes.**
HOUSE writes `Standard` where the 2.1.0 HALL file writes `Exclusive`. An exporter guessing that value
is guessing something two real programs already answer differently.

`LoadDistribution`'s doc argues the placement at length, because the placement is the design:
**spanning lives on the panel and never on the load.** A slab spans one way for every load it
carries; putting the direction on the load would let two loads on one panel contradict each other
about a property of the panel, and the format would then have to arbitrate. It is also where SAF puts
it.

## Modified

- **`IIdentified.cs`** — gains `Guid? ParentUid`, decided in `FEMEX_SAF_Corpus_Notes.md` §7 and scoped
  there as *provenance and nothing more*. The doc names its four consumers, all of them concrete:
  a chorded arc's pieces pointing at the arc (which is what makes chording reversible rather than
  merely lossy), a diff that can tell eight bars are one member, loads expanded from one repeating
  native object, and SAF's own `Parent ID`, which is present on **seventeen of the reference
  workbook's forty-three sheets** — so this is a pass-through, not an invention. It is explicitly
  **not** the thin end of a derivation-tracking design; nothing in the library traverses it.
- **The thirteen `IIdentified` implementers** — `ParentUid` declared immediately after `Uid` on
  `Grid`, `Level`, `Node`, `Section`, `SurfaceProperty`, `Element`, `PlateRegion`, `Material`,
  `LoadCase`, `Load`, `LoadCombination`, `Support` and `Hinge`.
- **`FemexModel.cs`** — `List<LoadGroup> LoadGroups` immediately before `LoadCases`, because a case
  references a group; `CurrentSchemaVersion` → `"1.9"` with its ledger clause; `"1.8"` appended to
  `ReadableSchemaVersions`.
- **`Loads/LoadCase.cs`** — `int? LoadGroupId`, whose doc states outright that the group is not a
  second spelling of `Nature` and that the two are checked against each other.
- **`Loads/TemperatureLoad.cs`** — rewritten. `double? GradientY` and `double? GradientZ`, signed and
  referenced to the element's own local axes; `GradientPerDepth` becomes a getter-less
  `[JsonPropertyName("gradientPerDepth")]` shim with `TryTakeLegacyGradient`, exactly the
  `Material.UnitWeight` / `Units.LegacyLength` contract.
- **`Loads/PointLoad.cs`** — `int? BarId`, `double? Position`. The class doc carries P2's argument:
  the three answers the plan offered are each wrong differently, and the fourth — store the number —
  costs two nullable fields and is exactly reversible.
- **`Loads/LinearLoad.cs`** — `double? StartPosition`, `double? EndPosition`. Purely additive: `BarId`
  was already there.
- **`Geometry/Plate.cs`, `Geometry/PlateRegion.cs`** — `LoadDistribution? Distribution`. Null on a
  plate means two-way with no named members; null on a region means **inherit the plate's**,
  following the rule the region already applies to `SurfacePropertyId`, `Alignment` and
  `SurfaceOffset`.
- **`FemexModel.SelfWeight.cs`** — `MigrateLegacyGradients()` added to the single `OnDeserialized`
  hook, in version order after the units migration.
- **`FemexModel.Identity.cs`**, **`FemexModel.Unknown.cs`** — load groups registered in both walks;
  the two `Distribution` blocks in `EnumerateExtensible()`.
- **`FemexModel.Validation.cs`** — eleven new wordings; see below.
- **`Interop/FemexEntity.cs`**, **`Interop/Conformance/ConformanceHarness.cs`** — `LoadGroup` added to
  the capability vocabulary and to the population census, so an adapter that drops load groups is
  caught by the same rule that catches one dropping load cases.
- **`Comparison/EntityIndex.cs`, `Comparison/MemberComparer.cs`** — `RefTarget.LoadGroup`, and the
  three new integer references (`LoadCase.LoadGroupId`, `PointLoad.BarId`,
  `LoadDistribution.BarIds`). The comparer is exceptions-first, so everything else 1.9 added is
  compared by serialized form the day it was added, with no table to remember.

## The migration is a reinterpretation, and says so in that word

`FEMEX_SAF_Fit_Update_Plan.md` recorded a defect to fix on resume: *the migration would assign meaning
to a real number.* It would. 1.6's `gradientPerDepth` had **no sign convention stated anywhere** —
that absence is precisely what §4 item 6 complains about — and `GradientZ` has one.

**The rule does not choose.** `MigrateLegacyGradients()` carries the number across unaltered and
reports, per load and with the value, that the reading is a reinterpretation rather than a rename and
that the author should confirm the sign. Nothing in it inspects the element or guesses an intent.
1.8's units change dropped text that named no unit and said so; this one keeps a number and changes
what it means, which is worse in kind, and the message says so.

**`Examples/Example1.femex` was hand-migrated and its sign decided deliberately**, which is what the
plan asked for. The load is `"Roof slab cooling"` (id 83, case 4 *Temperature - roof*), `deltaT` −12,
`gradientPerDepth` 30. The plan assumed it sat on a bar; it does not — it acts on eleven **mesh
faces of plate 3004**, the *Level 4 slab*, whose contour runs (0,0) → (24,0) → (24,18) → (0,18),
anticlockwise seen from above, so by Newell its local +z points **up**. A roof losing heat from its
exposed top face against a warmer soffit has a temperature that *falls* along +z. The file therefore
carries `"gradientZ": -30`, and `LoadDirectionTests.Example1_CarriesASignedGradient_HandMigrated`
holds that reasoning as an assertion rather than as a comment.

`Examples/Conformance1.femex` carries the same key on bar 1 in a synthetic fixture where the number
stands for nothing; it was carried across unchanged as `"gradientZ": 5`.

`Examples/Parity1.femex` deliberately **keeps** `gradientPerDepth`. It is the file that exists to be
defective, and it now exercises the migration for the viewer's parity harness as well.

## The second source of truth this bump introduced, designed against

The other recorded defect: after 1.9 a case carries **two** statements of its category — its own
`LoadNature` and the `LoadGroupType` of the group it names — and nothing stops them disagreeing.
A bump written to close one silent wrong answer would otherwise have manufactured another.

The compatibility map is stated once, in `NatureGroupType()`, and warned about at the call site:

| `LoadNature` | `LoadGroupType` |
| --- | --- |
| `Dead` | `Permanent` |
| `Live`, `Wind`, `Snow`, `Temperature` | `Variable` |
| `Accidental` | `Accidental` |
| `Seismic` | `Seismic` |
| *(none)* | `Tensioning` |

`Tensioning` gets its own wording, because it is not an author's slip: FEMEX has no `LoadNature` for
prestress, so *every* case in such a group disagrees with it and changing the nature is not the fix.
`LoadGroupType`'s doc comment says so, as the plan required.

Worth recording: **the SAF reference workbook contains exactly this defect** — its `LC3` is
`Action type = Variable` sitting in `LG1`, which is `Permanent` — and no validator on that side said a
word about it.

## Validation

Errors:

- a load case referencing an unknown load group;
- a duplicate load group id;
- a load distribution naming an unknown bar, or naming an element that is not a bar;
- a `position` / `startPosition` / `endPosition` outside 0…1, or stated with no bar to measure along;
- a point load's `barId` naming an unknown element or a non-bar;
- a `parentUid` that is the nil guid, or that is the object's own uid.

Warnings:

- a load group naming no case;
- a load group with a blank or duplicated name (`ValidateNameKeys` extended — SAF keys groups by name
  and treats a duplicate in the sheet as fatal);
- the nature/type disagreement above, in its two wordings;
- a rotation stated on a two-way panel, which nothing reads;
- an empty `barIds` list, which says the load goes to no member at all — a different claim from null;
- a line load whose extent along its bar runs backwards or is empty;
- a `gradientY` reaching a surface element, which has one through-thickness axis;
- a `parentUid` naming no object in this model;
- the two migration reports.

**`ParentUid`'s rule is three wordings and not one**, which is a deliberate reading of
`FEMEX_SAF_Corpus_Notes.md` §7. That section says *"if stated, it must resolve to some object's `Uid`
in the same model"* and then that *"the rule must tolerate a parent that is only ever a former
object"* — which cannot both be an error. The nil guid and self-parenting are errors, because both are
false rather than absent; an unresolved parent is a **warning**, because a chord's parent is an arc
that was never a FEMEX object and pointing at it is the field working rather than failing. One message
per distinct parent, so eight chords of one arc produce one line.

## `Examples/Example3.femex`

Extended rather than replaced, as `FEMEX_SAF_Fit_Update_Plan.md`'s *Verification* asks — *"the
one-way slab panel join[s] it when the held bumps land"*. It gains a second beam line, a **deck panel
that spans one way** onto the two beams under it and names them, **three load groups** (one per action
category, all `Standard`, all agreeing with their cases' natures), a **point load at 0.4 along a
member** with no node minted for it, and a **signed thermal gradient**. 1.10 adds the tie and the
haunch to the same file.

## Verified

```
dotnet build griffel-femex.csproj      0 warnings, 0 errors, both legs
dotnet test                            431 passed, 0 failed   (390 before)
```

Gates, all with a precedent in `SectionTests.cs`:

1. **Additivity.** `Assert.DoesNotContain("\"loadGroupId\"", json)`, `"\"distribution\""`,
   `"\"parentUid\""`, and `Assert.DoesNotContain("\"gradient", …)`. A model that uses none of the
   feature gains one line at the root and nothing else — see *Deviations*.
2. **Byte identity.** `Example1_ReSerializesToItself`, `Example2_ReSerializesToItself`,
   `Example3_ReSerializesToItself` and `Example3_IsTheModelBuiltAbove` all hold.
3. **Silence.** `SampleModels.Build()` and all four clean examples still satisfy
   `Assert.Empty(model.Validate())`; the parity artefacts `Example1/2/3/Conformance1.expected.json`
   are still `[]`.
4. **Backward read.** A raw 1.8 JSON literal carrying `gradientPerDepth` opens, migrates, is named as
   a *reinterpretation*, does not report twice on re-read, and cannot re-emit the old key.
5. **Forward read.** A literal carrying an invented member on `LoadGroup` and on `LoadDistribution`
   survives in `UnknownMembers` and is named.

## Deviations from the plan

- **`LoadGroups` is a root list and therefore not additive at the root.** Every file gains
  `"loadGroups": [],` — one line — because FEMEX's root lists are non-nullable and always written,
  which is why `Examples/Example2.femex` already carries `"grids": []` and `"defaultGridIds": []`.
  Making this one nullable would have made it the only nullable root list and forced a null check on
  every consumer. The four checked-in examples were hand-edited for it, as they were for the version
  stamp.
- **The plan put `TemperatureLoad`'s `GradientPerDepth` migration on a bar.** `Example1.femex`'s only
  such load is on plate mesh faces, so the sign was decided from the *plate's* local +z instead. The
  reasoning is above and is held as a test.
- **`ParentUid`'s validation is three wordings across two severities**, for the reason given above.
- **The nature/type warning uses the plan's map exactly**, with `Temperature → Variable` — the plan
  listed it and it is what every code does with a thermal action.

## Still open

- **Nothing consumes `LoadGroup.Relation`.** FEMEX generates no combinations, so an `Exclusive` group
  changes no number this library computes. That is correct for now — combinations are explicit lists
  of factored cases — but a check report that claimed to have verified a combination set would need
  to read it.
- **`Repeat (n)` / `Delta x`** (`FEMEX_SAF_Corpus_Notes.md` §3.8) is still an adapter-side expansion,
  decided but not built: three FEMEX loads for one SAF row, reported *Approximated*, with `ParentUid`
  now available to make the expansion collapsible. Phase B.
- **An absolute position converts through the bar's length**, which is exact on a straight member and
  an approximation on a chorded arc. The format states the relative form only; the conversion and its
  message are the adapter's.
- **A load group carries no `Load type`** (SAF's `Domestic | Roofs | Snow | Wind` on variable groups).
  Deliberately: it is a sub-category of a sub-category, blank on three of the reference file's seven
  groups, and P5 already states the invention policy for it.
