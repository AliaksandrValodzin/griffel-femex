# Standard sections — Implementation Summary

Implemented `Claude/FEMEX_StandardSections.md` in full, all eight decisions as written, in the two
version bumps the plan sequenced. Clean build (0 warnings, 0 errors); **254 tests pass** (was 224).

A steel frame now crosses FEMEX. It crosses **by name** where the receiver knows the library, **by
dimensions** where it knows the shape, and **by stiffness** where it knows neither — so a section is
never lost, only degraded. That was the interop review's §4.4, the last of its six blocking gaps, and
`FEMEX_Adapters.md` §4.2's *"there is no shape to approximate it with"* is now a corrected row rather
than an open hole.

FEMEX ships **no catalogue rows**, and that is the decision everything else follows from. It ships the
vocabulary to *name* any of the thousands of standard profiles and the numbers to survive *not
recognising* one, both travelling in the same file. `griffel-femex.csproj` keeps its zero
`PackageReference`s and its zero embedded resources, as it has had since the first commit.

## New files

| File | What |
| --- | --- |
| `Geometry/Sections/SectionProperties.cs` | Eleven `double?` in two named groups — analysis (`Area`, `ShearAreaY`, `ShearAreaZ`, `Iy`, `Iz`, `J`) and design (`Iw`, `Wely`, `Welz`, `Wply`, `Wplz`). `IExtensible`, not `IIdentified` |
| `Geometry/Sections/GenericSection.cs` | `"generic"` — no geometry at all, SAF's `General`. `CalculateArea()` returns `0.0` |
| `Geometry/Sections/ISection.cs` | `"ishape"` — doubly-symmetric I/H, `TSection`'s four field names exactly |
| `Geometry/Sections/Channel.cs` | `"channel"` — the same four names, and deliberately the same area formula |
| `Geometry/Sections/Angle.cs` | `"angle"` — `LegLengthY`, `LegLengthZ`, `Thickness` |
| `Geometry/Sections/Box.cs` | `"box"` — a hollow `Rectangle`, reusing `Width`/`Depth` |
| `Geometry/Sections/Pipe.cs` | `"pipe"` — a hollow `Circle`, reusing `Diameter` |
| `Geometry/Sections/SectionCatalogue.cs` | Free-text `Source` and `Profile`, closed-enum `Manufacture`. `IExtensible` |
| `Geometry/Sections/SectionManufacture.cs` | `HotRolled \| ColdFormed \| Welded \| Other` |
| `Examples/Example2.femex` | A steel portal frame — six nodes, seven bars, S355, four sections exercising every combination of the three layers |
| `griffel-femex.Tests/SectionTests.cs` | 30 facts: 16 for 1.5, 14 for 1.6 |

## Modified

- **`Geometry/Sections/Section.cs`** — two nullable properties on the base (`Catalogue`, then
  `Properties`, declared after `Name`), the non-abstract `GetArea()`, six `[JsonDerivedType]` lines,
  the three-layer doc comment and the reserved-discriminator list (`tapered`, `asymmetric`,
  `compound`). Nothing existing moved and no field was renamed.
- **`FemexModel.cs`** — `CurrentSchemaVersion` `"1.4"` → `"1.5"` → `"1.6"`, a sentence in the version
  doc comment per bump, and `ReadableSchemaVersions` grown twice.
- **`FemexModel.SelfWeight.cs:153`** — `section.CalculateArea()` → `section.GetArea()`. The **only**
  behavioural change to an existing model, and a correctness improvement.
- **`FemexModel.Validation.cs`** — a `// ----- Sections -----` block as **two** methods across the two
  call sites, `SectionAreaAgreementTolerance`, a `"1.4"` and a `"1.5"` branch in
  `ValidateSchemaVersion()`, and **two lines** on `SelfWeightVersions`.
- **`FemexModel.Unknown.cs:61`** — the sections walk became a block, yielding the `catalogue` and
  `properties` value blocks beside the section itself.
- **`griffel-femex.Tests/griffel-femex.Tests.csproj`** — a second `<None Include>` for `Example2.femex`.
  The copy rule is per-file and not a glob.
- **`Examples/Example1.femex`** — `schemaVersion` bumped at each step. Nothing else touched, and it is
  still byte-identical through a load-and-save at both.
- **`Claude/FEMEX.md`, `FEMEX_Adapters.md`, `FEMEX_Adapters_Plan.md`, `FEMEX_Interop_Review.md`,
  `FEMEX_Interop_Status_16082026.md`** — corrected; see below.

## The three layers, and why they are not three subtypes

Review §4.4 proposed a `catalogue` type and a `numeric` type as **siblings** in the discriminated
union. They are not siblings here, and the reason is that the sibling design fails on the review's own
motivating example: a real IPE300 is a catalogue name, a shape and a set of numbers *at once*. As
siblings, a `catalogue` section the receiver cannot resolve would carry no numbers to fall back on —
precisely the loss the escape hatch exists to prevent, and it would leave the format's most-used
section type as the one type that cannot degrade.

So they are layers on one section, any subset, at least one:

```
Section
 ├ id, uid, name                                        existing
 ├ catalogue?  { source, profile, manufacture }         identity   (1.6)
 ├ type ─────► rectangle | circle | tshape              existing
 │             ishape | channel | angle | box | pipe    geometry   (1.6)
 │             generic                                  geometry   (1.5)
 │             + that shape's dimensions
 └ properties? { area, iy, iz, j, … }                   stiffness  (1.5)
```

Precedence, stated once: **resolve the catalogue name; else build the parametric shape; else build a
member with the stated stiffness.** And where a property is stated it is authoritative over the
parametric one, which is what `GetArea()` executes.

**This is a property of the JSON as read by an adapter, not forward compatibility for this library.**
System.Text.Json throws on an unrecognised polymorphic discriminator, so a 1.4 build handed
`"type": "ishape"` fails to deserialize and never reaches the `properties` it could have degraded to —
and `ReadableSchemaVersions` refuses that file first anyway. Said here so the promise is not read as
wider than it is.

## Two validators, not one

`Validate()` wraps severity **per method at the call site**, so a single validator yielding bare
strings cannot emit both. The split follows the `ValidateGrids` / `ValidateGridGeometry` precedent:

| Method | Call site | Yields |
| --- | --- | --- |
| `ValidateSections()` | the error block, after `ValidateNodes` | a `generic` section stating no area; any stated property that is not positive |
| `ValidateSectionCompleteness()` | the warning block, after `ValidateNameKeys` | a stated area disagreeing with the dimensions past 10%; a `generic` section missing `iy` or `iz`; a profile named with no source |

**Zero is rejected with the negatives.** A stated property is a claim about stiffness, and zero is not
a claim a solver can build with. That is not in tension with a zero-width `Rectangle` staying legal:
that field has been legal FEMEX since the first commit, whereas these are new and could be given a
contract from the start.

**The two warnings partition the space, and each is scoped so the other's case cannot trip it.** The
disagreement check excludes `generic`, because `GenericSection.CalculateArea()` returns `0.0` and an
unscoped version would read every correctly-authored generic section — a stated `5.381e-3` against a
computed `0.0` — as a 100% disagreement and fire on the exact case 1.5 exists to make legal. The
missing-`iy`/`iz` check is scoped *to* `generic` for the mirror reason: a shaped section with no
properties is fine, because it hands the receiver its dimensions. So one is *geometry and stiffness
disagree* and the other is *no geometry, and the stiffness is incomplete*; no input trips both, and a
generic section carrying `area`, `iy` and `iz` trips neither. `SectionTests.cs` locks that last case
with a fact of its own.

## The trap, paid twice

`ValidateSelfWeight` gates on `SelfWeightVersions`, a **matched list** rather than a comparison,
because `FemexModel.cs` explicitly declines to have a version-*ordering* policy. Bumping
`CurrentSchemaVersion` without adding the new version to that list silently stops the "no load case
carries self-weight" warning from firing for files at the previous version. `FEMEX_Metadata_Summary.md`
recorded it biting on the last bump; it was paid knowingly here, one line per bump, two bumps.

`ValidateSchemaVersion()` needed the same treatment for the opposite reason: without a branch per
newly-old version, a `1.4` or `1.5` file would fall through to *"this build does not recognise"* while
sitting in `ReadableSchemaVersions`.

## Verified

```
dotnet build
dotnet test griffel-femex.Tests\griffel-femex.Tests.csproj
```

The test project is **not** in `griffel-femex.sln`, so a bare `dotnet test` at the repo root reports
*"Build succeeded, 0 Warning(s), 0 Error(s)"* and runs **zero** tests without saying so. Naming the
project is what makes the run real.

1. **0 warnings, 0 errors; 254 tests pass, 0 failed.** Baseline was `Passed! - Failed: 0, Passed: 224`,
   confirmed before the work started.
2. Both tests that assert `Example1.femex` is silent still do — `Example1_LoadsAndValidates`
   (`ValidationTests.cs`) and `Example1_CarriesLoadIdsAndNoUids` (`RoundTripIdentityTests.cs`) — and
   `Example1_ReSerializesToItself` is byte-identical after **each** bump. Together: the check that both
   changes really are additive, and that a null `properties` and a null `catalogue` write not one extra
   byte.
3. `Example2_ReSerializesToItself` — byte-identical, which is what proves the three layers serialize in
   a stable order. The file was generated through `ToJson()` for exactly that reason, and the key order
   it shows confirms the plan's prediction: `catalogue` and `properties` follow the derived type's
   dimensions and `id`/`name`, being declared on the base.
4. **The hand check that motivates the whole item.** An IPE300 authored as `ishape` + `catalogue` +
   `properties`, saved, reloaded:

   ```
   CalculateArea()   = 0.00518806
   GetArea()         = 0.005381
   selfweight        = 0.414241  =  gamma * GetArea()        (match)
                                 != gamma * CalculateArea()  (0.399388)
   validate          = 0 messages
   ```

   The 3.6% between them is what the root fillets account for, and it is decision 2 as a number.
5. A `generic` section with `properties` stripped produces exactly one **Error**; the same section with
   only `iz` stripped produces exactly one **Warning**; with `area`, `iy` and `iz` all present it
   produces **none**. The third is what proves the disagreement check is scoped away from `generic`.
6. `SelfWeightTests.cs` `Example1_SelfWeightResolves` still passes untouched. It calls
   `section.CalculateArea()` directly against Example1, whose sections carry no `properties`, so
   `GetArea()` falls back and the assertion holds — the evidence that the call-site switch changes no
   existing model.

## Documents corrected in the same pass

- **`Claude/FEMEX.md`** — the format spec of record, and it had fallen **two** versions behind: the 1.4
  metadata pass scheduled no blockquote, so the file stopped at 1.3 with no mention of `FileMetadata`
  or `IExtensible`. Both gaps closed in one edit: an *Extended by `FEMEX_Metadata.md`* blockquote for
  1.4, an *Extended by `FEMEX_StandardSections.md`* blockquote covering 1.5 and 1.6, and a third on the
  `Sections/` bullet, which documented the union literally and had explicitly left open whether to
  *"drop the `CalculateArea` hack or keep [it] as a real per-section computation"*. It was kept, and
  `GetArea()` was added beside it.
- **`FEMEX_Adapters.md` §4.2** — the *Dropped* ruling became *Approximated*, with a `> **Closed by …**`
  blockquote naming both bumps and narrowing what stays genuinely approximate (an angle's principal
  axes; the reserved shapes). §4.5's stale *"`CurrentSchemaVersion` is `"1.3"` … (`FemexModel.cs:50,60`)"*
  was corrected to `"1.6"` and `:54,64` while the file was open.
- **`FEMEX_Adapters_Plan.md` §4** — a `> **Superseded by …**` note on the Approximated bullet, which
  said the same thing in the plan's own words.
- **`FEMEX_Interop_Review.md` §4.4** — a `> **Closed by …**` blockquote, following the majority wording
  of the three the file already carries (§4.1, §4.3, §4.6). §4.2 carried none and still does; this is
  §4.4's. It records the one deliberate departure from the review's literal proposal — layers, not
  siblings — and why.
- **`FEMEX_Interop_Status_16082026.md`** — §0, §2.1, §2.2 and §5 items 1, 2 and 3 brought current, §1's
  table given rows for 1.4, 1.5 and 1.6, and §2 retitled *"No longer blocking"*. **All six** of the
  review's blocking gaps are now closed. A dated note, so the original wording is kept where the
  reasoning still earns its place and the update is a blockquote against it.

## Deviations from the plan

- **Sixteen 1.5 facts, not fifteen.** The extra is `AnUnstatedProperty_IsToldApartFromZero`, which
  locks the `double?`-not-`double` decision directly rather than only through validation. Total 254,
  where the plan predicted ~253.
- **`Example2.femex` is six nodes and seven bars**, where the plan said "around six nodes, three bars".
  The frame is two columns, two rafters, a two-part eaves tie and an apex hanger, and the extra members
  are what let **all four** of its sections be used by something — including the `generic` one, which
  is the whole point of 1.5 and would otherwise have sat in the file unreferenced.
- **`MetadataTests.cs`'s unrecognised-version fixture moved from `"1.5"` to `"2.0"`.** It was authored
  as a version this build has never heard of; the first bump would have made it a recognised one and
  turned three assertions red for the wrong reason. The plan did not foresee this.
- **Four version assertions were rewritten against `FemexModel.CurrentSchemaVersion`** rather than
  bumped twice (`LoadDirectionTests.cs`, `MetadataTests.cs`, `RoundTripIdentityTests.cs`,
  `SelfWeightTests.cs`), and `ALegacyFile_ReEmitsAs14_WithItsLoadIds` was renamed to
  `…ReEmitsAsTheCurrentVersion…`. Each of those spelled a version out in a `StartsWith` and would have
  needed touching at every future bump; none of them is *about* which version it is, only that the
  stamp is the current one.
- **`SampleModels.cs` was left alone.** The plan listed the fixture's three sections as gaining the new
  layers. They did not: every layer is optional, so the fixture exercises none of them and stays the
  clean *no properties, no catalogue* baseline that `ASectionWithNoProperties_OmitsTheKeyEntirely` and
  `ASectionWithNoCatalogue_OmitsTheKeyEntirely` assert against. `SectionTests.cs` adds the layers per
  fact instead, which is what keeps `Assert.Empty(model.Validate())` usable across the other 224.

## Still open

- **Whether `Bar.SectionId` and `Bar.MaterialId` should become `int?`** like their `Plate`
  counterparts. `FEMEX_Adapters.md` §2.2 raises it on its own founding example — a bar drawn before a
  section has been chosen carries `SectionId = 0` and is un-exportable — and §9 parks it as a schema
  question. This change makes it *more* pressing, not less, because a `generic` section is now the
  cheapest honest placeholder an adapter can synthesise.
- **Principal axes.** An angle's `iy` and `iz` are about geometric axes; its principal axes are rotated
  from them. There is no `iu`, `iv` or principal angle, so an angle crosses with geometric-axis
  stiffness only — a real approximation for a single angle in bending, and the first thing a real file
  is likely to challenge.
- **Whether `Section.Name` and `Catalogue.Profile` should ever be reconciled.** They will usually hold
  the same string. `ValidateNameKeys` warns on a blank or duplicated `Name`; nothing warns when the two
  disagree, and it is not obvious that anything should.
- **Positivity of the existing dimension fields.** Validating *new* fields cannot invalidate a file
  that is legal today; validating the existing ones can, and a `Rectangle` with a zero width has been
  legal FEMEX since the first commit. Deliberately not done here.
- **`SurfaceProperty`'s equivalent escape hatch**, and **section–material coupling** — review §7.3's
  two open questions, both out of scope and both unanswered.
- **Nothing here has been checked against a real exported file.** The catalogue vocabulary is modelled
  on SAF, Robot and ETABS *as documented*, not as seen. Status item 6 — one real ETABS or RFEM export,
  round-tripped — is the step that tests it, and sections are the part of the schema most likely to be
  found wrong by it.
- **Whether `griffel-femex.Tests.csproj` should switch to a glob** over `..\Examples\*.femex`, and
  **whether the test project belongs in `griffel-femex.sln`** — both raised by the review of the plan,
  both left where they were. Example3 will hit the first; the second is what makes a bare `dotnet test`
  silently empty.
