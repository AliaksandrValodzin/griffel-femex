# Member behaviour, system lines, eccentricity, taper — Implementation Summary

Implemented bump **1.10**, the second of the two held bumps of
`Claude/FEMEX_SAF_Fit_Update_Plan.md`, completing Phase A′ of
`Claude/AdaptersPlans/SAF_Adapter.md`. Clean build (0 warnings, 0 errors, both legs);
**461 tests pass** (was 431 after 1.9, 390 after Phase A). 1.9 is
`FEMEX_LoadGroups_Summary.md`.

Four more of `FEMEX_SAF_Fit.md` §4's eight *silently wrong* concepts close here — with the last
of the eight, curved geometry, still open and now at least **reversible**, since 1.9's `ParentUid`
gives a chord somewhere to point.

**The evidence behind the four is not equal, and the code says which is which.**
`Behaviour in analysis` is populated on **every one** of the reference workbook's forty-two members
and is `Axial force only` on **thirty-three** of them, so a 1.9 import of that one file gets
thirty-three members wrong. `System line` is `Top left` on the same thirty-three. Against that, the
four `Analysis Y/Z Eccentricity` columns are **zero on every row of every published SAF file**
(`Claude/FEMEX_SAF_Corpus_Notes.md` §3.4) — which is an argument for shipping the shape and for not
spending long on it, not for leaving it out: the columns are mandatory, so an exporter must write
something, and a format with nowhere to read one has to invent all four.

---

## New files

| File | What |
| --- | --- |
| `Geometry/BarBehaviour.cs` | `Standard, AxialOnly, CompressionOnly, TensionOnly` — SAF's four values exactly |
| `Geometry/BarAlignment.cs` | `Centre, Top, Bottom, Left, Right, TopLeft, TopRight, BottomLeft, BottomRight` — SAF's nine system lines |
| `Geometry/BarEccentricity.cs` | `class BarEccentricity : IExtensible` — eight `double?`, the Structural and Analysis families kept apart |
| `griffel-femex.Tests/BarTests.cs` | 23 facts |

`BarBehaviour`'s doc names `PlateBehaviour` as the sibling concept on surfaces and
`PlateBehaviour.CompressionOnly` as the one value the two sets share. Both new enums argue **null is
not the same fact as the default value** — a pre-1.10 file states nothing because the concept did not
exist, where a 1.10 file writing `Standard` or `Centre` is an author saying so. That is the
distinction `RestraintSense` drew at 1.8, and it is what keeps every existing file byte-identical.

`BarEccentricity`'s doc argues the split, because the split is the point of the type. **Structural**
is the BIM offset: it moves the picture and the clash model and changes no internal force.
**Analysis** moves the analysis line, so axial force acts on a lever arm it did not have before, and
it does change forces. `FEMEX_SAF_Fit.md` calls SAF's separation *"the most honest model of the
three and worth copying"*; a receiver that collapses them produces geometry that looks right and
stiffness that is wrong. Most programs collapse them. FEMEX does not, and an adapter crossing into
one that does reports the loss.

## Modified

- **`Geometry/Bar.cs`** — `int? EndSectionId` declared beside `SectionId`, then `BarBehaviour?
  Behaviour`, `BarAlignment? Alignment` and `BarEccentricity? Eccentricity` after `RotationAngle`.
  The class doc, which previously documented only the local-axis convention, now states that the same
  local y and z is the frame the alignment names, the eccentricity offsets in, and a temperature
  load's two gradients are stated along — so nothing new is invented by any of them.
- **`BoundaryConditions/Support.cs`** — `int? BarId`, `double? Position`, `double? EndPosition`,
  mirroring the `PlateId`/`RegionId` pair the type already carried. This is the boundary-condition
  half of P2, and it is forced: the reference workbook has a point support **on beam B38** at a
  relative station and a curve connection on B4 running `Absolute`, `From end`, 0.2 m → 1.5 m.
- **`BoundaryConditions/Hinge.cs`** — `double? Position` only. **No `BarId` beside it**, and that is a
  deliberate departure from the corpus notes' table; see *Deviations*.
- **`FemexModel.cs`** — `CurrentSchemaVersion` → `"1.10"` with its ledger clause; `"1.9"` appended to
  `ReadableSchemaVersions`.
- **`FemexModel.Validation.cs`** — `"1.9"` appended to `SelfWeightVersions`, a `"1.9"` branch in
  `ValidateSchemaVersion`, a reference check in `ValidateBars`, a new `ValidateBarCompleteness` in
  the warning block, the support and hinge position checks, and an element-id → hinge index on
  `ValidationContext`.
- **`FemexModel.Unknown.cs`** — `bar.Eccentricity` registered in `EnumerateExtensible()`.
- **`Comparison/MemberComparer.cs`** — `Bar.EndSectionId` → `Section` and `Support.BarId` → `Bar`.
  Everything else 1.10 added compares by serialized form with no table entry, which is the
  exceptions-first design working.

## Validation

Error:

- `EndSectionId` referencing an unknown section;
- a support's `barId` naming an unknown element or a non-bar; an `Area` support following a bar; a
  support following both a bar and a plate; an `endPosition` on a support that is not `Linear`;
- a support or hinge `position` outside 0…1, or with nothing to measure it along;
- a hinge `position` on a plate.

Warning, all four from the plan:

- a taper whose end section **is** its start section, which says nothing;
- a taper between two sections of **different shapes**, which nothing can build — a warning rather
  than an error because a receiver falling back on `SectionId` gets the prismatic member, which is
  the degrade-don't-lose rule sections already follow;
- an eccentricity block whose eight fields are all null, mirroring `ValidateSectionCompleteness`'s
  treatment of a claim that states nothing;
- a `TensionOnly` or `CompressionOnly` bar carrying a hinge that releases its `ux` — such a member
  carries axial force and nothing else, so releasing that too leaves it resisting nothing at all.

That last rule needed the lookup the earlier draft did not list: **a bar carries no hinge**. Hinges
are a root list pointing back at their element, so `ValidationContext` gained
`HingesOn(elementId)`, built once per `Validate()` call.

## The taper downgrades §4 item 5; it does not close it

Recorded as the plan required, and held as a test
(`BarTests.ATaper_IsOneSpan_NotSAFsVaryingMember`).

SAF states a varying member as **spans** — each with its own section *and* alignment, relative spans
summing to 1.0 — and `Claude/FEMEX_SAF_Corpus_Notes.md` §3.5 read the one real example:

```
Name  Cross sections 1  Span 1  Alignment 1   Cross sections 2  Span 2  Alignment 2   Cross sections 3  Span 3  Alignment 3
AD1   CS1               0.25    Centre        CS1,CS9           0.5     Left          CS1               0.25    Centre
```

Three spans, a **comma**-separated section pair on the middle one where every other list column in
SAF uses a semicolon, and a fixed-width repeating group rather than a normalised child table. So the
corpus answered the plan's open question — *whether real files use one span or several* — with
**several**, on the only row available.

`EndSectionId` carries the one-span case exactly and turns the rest into a reported *Approximated*.
A rafter haunched at **both** ends still arrives with the wrong moment distribution, now with a
message attached. That is worth having and it is not the same as being right. The `tapered`
discriminator reserved at `Geometry/Sections/Section.cs:24-27` stays reserved and unimplemented: a
taper is a property of the member, not a kind of section, and two bars can share one section while
only one of them tapers.

## `Examples/Example3.femex`

Completed here, as `FEMEX_SAF_Fit_Update_Plan.md`'s *Verification* asks — *"the tension-only brace,
the haunched rafter and the one-way slab panel join it when the held bumps land"*. All three are now
in it. 1.10's additions:

- a **tension-only tie** in steel S355 back to the raft — which also puts a third `MaterialType` in
  the file;
- the first beam **tapered** into a `GL 200x900` haunch and set out on `BarAlignment.Top`, which is
  what a floor level is;
- a second beam carrying the **eccentricity block with both families stated separately** — 300 mm of
  drawn offset that changes no force, 20 mm of analysis offset that changes several. A receiver that
  fused the two would apply 300 mm of lever arm.

The file is still byte-identical to `Example3Tests.Build()` and still validates silently.

## Verified

```
dotnet build griffel-femex.csproj      0 warnings, 0 errors, both legs
dotnet build                           0 warnings, 0 errors (solution)
dotnet test                            461 passed, 0 failed   (431 after 1.9, 390 before)
```

Gates:

1. **Additivity.** `BarTests.AModelUsingNoneOfIt_GainsNotOneByte` serialises the sample's bar on its
   own and asserts none of the four keys appears. It is asserted on the bar rather than on the whole
   file deliberately: a plate's `alignment` is a non-nullable `SurfaceAlignment` that has always been
   written, and matching the file would have found that instead.
2. **Byte identity.** All three example identity facts hold, plus
   `Example3_IsTheModelBuiltAbove`.
3. **Silence.** `SampleModels.Build()` and all four clean examples validate empty; every
   `*.expected.json` but `Parity1`'s is still `[]`.
4. **Backward read.** A raw `"1.9"` literal opens and is told what it lacks.
5. **Forward read.** A literal carrying `analysisXBegin` on an eccentricity block survives in
   `UnknownMembers` and is named.

## Deviations from the plan

- **`Hinge` gained `Position` but no `BarId`.** `FEMEX_SAF_Corpus_Notes.md` §6's table lists
  `int? BarId, double? Position` for both `Support` and `Hinge`. A hinge's `ElementId` **is** the
  member, so a second reference to it would be two statements of one fact that could disagree — the
  exact defect this pass spent a warning closing on the load-case side. `Support` genuinely needed
  one, because its `NodeIds`/`PlateId` pair names no bar.
- **`BarEccentricity.IsEmpty` and `MovesTheAnalysisLine` are methods, not properties.** Written first
  as computed properties, which System.Text.Json duly serialized into `Example3.femex`. There is no
  `[JsonIgnore]` anywhere in this repository and the first one would be the expensive one, so they
  follow `Section.CalculateArea` and `Material.GetShearModulus` instead. `Example3Tests` asserts
  `isEmpty` does not appear in the file.
- **`ValidateBarCompleteness` takes the context**, where the plan implied a parameterless method: the
  hinge lookup lives there.
- **The taper's shape check compares runtime types**, not the `type` discriminator string. Same
  distinction, one less place for the discriminator table to be restated.

## Still open

- **SAF's multi-span varying member** stays unmapped, and the corpus says the multi-span case is the
  one that occurs. Closing it would mean a span list with a per-span alignment — a real schema change
  with a real shape, and one the adapter can now at least *report* rather than silently flatten.
- **Nothing in any published SAF file exercises analysis eccentricity.** The shape ships against
  documentation alone. First contact with a file that uses it is the test.
- **`BarAlignment` and `BarEccentricity` change no geometry this library computes.**
  `TryGetBarLocalAxes` still returns the axes at the two nodes; neither the system line nor the
  offsets move a coordinate anywhere in FEMEX. That is correct for a format whose job is to carry the
  statement, and it means a viewer or a mesher wanting the offset member has to apply them itself.
- **`RelConnectsRigidCross`, `RelConnectsRigidLink` and `RelConnectsRigidMember`** are still unmapped
  entirely, and all three are in the reference file. Three objects *Dropped* on file one, unchanged
  by this pass.
