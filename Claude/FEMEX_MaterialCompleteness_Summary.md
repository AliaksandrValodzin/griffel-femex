# Material completeness — Implementation Summary

Implemented bump **1.7** of `Claude/FEMEX_SAF_Fit_Update_Plan.md` as written. Clean build
(0 warnings, 0 errors); **276 tests pass** (was 254). Bump 1.8 — units, restraint sense, bedding
semantics — is not started, and the two held bumps still wait on Step 0'.

A FEMEX material now says **what it is** and **what it can be designed against**. Before this bump a
`Material` was five numbers and a label: E, ν, ρ, one unnamed `Strength`, and a `Name` three programs
key by and no code writes. SAF marks `StructuralMaterial.Type` and `.Quality` mandatory, so an
exporter could not write a workbook SAF's own validator would accept without inventing values — and
underneath that, `TemperatureLoad` had no α behind it anywhere in the format, which
`FEMEX_Interop_Review.md` §5.5 called *"an internal inconsistency, not just an omission"* and which
this bump makes executable rather than merely documented.

Two arguments reached this bump independently, which is why it went first.
`FEMEX_BusinessModel.md` §9 kept it against a general demotion of P1 items because a check report is
only worth reading if the numbers in it mean something; `FEMEX_SAF_Fit.md` §8.1 ranked it first off
SAF's own required-column list. They agree on the same five fields.

## New files

| File | What |
| --- | --- |
| `Materials/MaterialType.cs` | `Concrete, Steel, Timber, Aluminium, Masonry, Other` — SAF's set exactly. Closed, and the doc argues why against its free-text sibling `Quality`, the way `SectionManufacture` argues against `SectionCatalogue.Source` |
| `Materials/MaterialProperties.cs` | `class MaterialProperties : IExtensible` — SAF's 22 `Design properties` as `double?` under three headings: steel `Fy, Fu, FuMinimum, Ry, Rt`; concrete `Fck, Fcm, Fctm, Fctk05, Fctk95, EpsC2, EpsCu2, EpsC3, EpsCu3`; timber `E005, E90Mean, Fmk, Ft0k, Ft90k, Fc0k, Fc90k, Fvk` |
| `griffel-femex.Tests/MaterialTests.cs` | 22 facts |

`MaterialProperties` deliberately copies `Geometry/Sections/SectionProperties.cs` — the same
per-type property bag that lets a value cross when the receiver does not recognise the grade. It is
`IExtensible` and not `IIdentified`, for the reason `SectionProperties` is: it is a value block, not
an authored entity a receiving program merges by.

## Modified

- **`Materials/Material.cs`** — five nullable properties, declared where the examples expect their
  keys: `Type` and `Quality` after `Name`; `ShearModulus` after `PoissonsRatio`, beside the two
  constants it may contradict; `ThermalExpansion` before `Strength`; `Properties` last.
  `GetShearModulus()` became `ShearModulus ?? E / (2(1+ν))`, reusing `Section.GetArea()`'s
  doc-comment wording. `Strength` is untouched and its doc gained one sentence saying `Properties`
  is where a design value belongs from 1.7 on. The class doc no longer says "isotropic" flatly: a
  stated `G` is the one place a FEMEX material may contradict the isotropic relation, and the doc
  says so and says it is not an orthotropic model.
- **`FemexModel.cs`** — `CurrentSchemaVersion` `"1.6"` → `"1.7"`, a clause in the version-ledger doc
  comment, and `"1.6"` appended to `ReadableSchemaVersions`.
- **`FemexModel.Validation.cs`** — a `// ----- Materials -----` block of **two** rules across the two
  call sites (`ValidateMaterials` into the error block beside `ValidateSections`,
  `ValidateMaterialCompleteness` into the warning block beside `ValidateSectionCompleteness`), the
  private `EnumerateStatedMaterialValues` helper mirroring `EnumerateStatedProperties`, a `"1.6"`
  branch in `ValidateSchemaVersion()`, `"1.6"` on `SelfWeightVersions`, and an element → material
  map plus `TryGetElementMaterialId` on `ValidationContext`.
- **`FemexModel.Unknown.cs:87`** — the materials walk became a block, yielding the `properties`
  value block beside the material itself, and the class doc's list of what it reaches now says
  "the sections' and materials' own value blocks".
- **`Examples/Example1.femex`** — `schemaVersion` bumped; both concretes gained `type`, `quality`
  and `thermalExpansion`. Still byte-identical through a load-and-save.
- **`Examples/Example2.femex`** — `schemaVersion` bumped; S355 gained `type`, `quality`,
  `thermalExpansion` and a `properties` block stating `fy` and `fu`. Still byte-identical.
- **`griffel-femex.Tests/SampleModels.cs`** — the one material gained `Type`, `Quality` and
  `ThermalExpansion`, which is what `SampleModels.Build()` needs to keep validating silently.
- **`Claude/FEMEX.md`** — one "Extended by" blockquote in the root-design section, the seventh there.

`FemexModel.Identity.cs` is untouched: this bump adds no identified entity. `griffel-femex.csproj`
and `griffel-femex.Tests.csproj` are untouched: no new package, no new example file.

## Two validators, not one — and why one of them warns on absence

There was no `ValidateMaterials` before this bump. Materials were reached only by
`ValidateDuplicateIds`, `ValidateNameKeys` and reference resolution from bars, plates and mesh
faces; **nothing checked a material's own numbers**. Both new rules are new methods at new call
sites, not edits to existing ones.

**The error.** A stated `ShearModulus`, `ThermalExpansion` or design value that is not strictly
positive. The reasoning is `ValidateSections`' verbatim: a stated property is a claim, and zero is
not a claim anything downstream can build with. It is scoped to what 1.7 added — E, ν and ρ stay
unpoliced, because they have been legal FEMEX since the first commit and a zero density is already
reported, in its own words, by `ValidateSelfWeight`. That is the same line `ValidateSections` draws
when it rejects a zero stated area while leaving a zero-width `Rectangle` legal.

α is required strictly positive with the rest. Every family `MaterialType` names expands when it is
heated, so a non-positive α is far more often a sign error than a statement about an exotic
composite — and FEMEX has no orthotropic material for the exotic composite to be stated as anyway.

**The warnings.** A material that states no `Type`, and a `TemperatureLoad` whose elements resolve
to a material with no `ThermalExpansion`.

The first warns on **absence**, which `ValidateSectionCompleteness` deliberately never does — it
warns only about incoherent claims. The precedent is `ReportNameKeys`, which already warns that an
unnamed entity will have a name invented for it. The argument is the same and so is the consequence:
SAF marks the column mandatory, so there is no writing the material out without a value, and what an
exporter cannot read it will guess from the density and the modulus.

The second is the §4 item 6 inconsistency made executable. It reports **once per material a load
reaches**, not once per element: `Example1.femex`'s thermal load names eleven mesh faces of one
concrete, and eleven copies of one message would bury every other message in the report — the
`ReportUnknownMembers` argument, applied to a different fact.

## The element → material lookup

The thermal rule needs to resolve an element id to a material across all three element kinds, which
nothing in `ValidationContext` did. The map is built once per `Validate()` call, beside the existing
lookups:

- a **bar** answers with `Bar.MaterialId`, which is non-nullable;
- a **plate** answers with its own `MaterialId` unless it is an `Opening`;
- a **mesh face** answers with its resolved cache, and where that cache is null it falls back to
  `GetEffectiveProperties(plate, region)` — the same region-inheritance rule the self-weight
  arithmetic and `ValidatePlates` already share, so a mesher that filled the cache and one that left
  it null reach the same material.

An element that resolves to no material at all is simply absent from the map, and the rule says
nothing about it: `ValidatePlates` already reports that in its own words, and an unknown element id
is already reported by `ValidateLoads`.

## Verified

- **`GetShearModulus()` had no call sites.** `Materials/Material.cs:107` was the only `.cs` hit in
  the repository, so making a stated `G` authoritative broke nothing and audited nothing.
- **Byte identity, both files.** `Example1_ReSerializesToItself` (`RoundTripIdentityTests.cs`) and
  `Example2_ReSerializesToItself` (`SectionTests.cs`) both pass unchanged. `1E-05` and `1.2E-05` are
  exactly what `System.Text.Json` writes for those doubles; the hand-edited files were checked
  against the serializer, not guessed at. The examples are CRLF and end without a trailing newline,
  which is what `WriteIndented` produces on Windows and what byte identity means here.
- **Silence, three files.** `SampleModels.Build()`, `Example1.femex` and `Example2.femex` all still
  satisfy `Assert.Empty(model.Validate())`. Example1 is the one that mattered: its thermal load
  reaches eleven mesh faces, so it is silent only because material 1 now states an α.
- **Additivity.** A model whose material uses none of the five new fields writes not one of the five
  keys — asserted including `"type"`, in a model holding nothing else that carries a discriminator.
- **Backward read.** A raw 1.6 JSON literal opens, migrates nothing (the bump is purely additive),
  is told what it lacks by both the version branch and the completeness warning, and re-saves as 1.7
  having gained no key.
- **Forward read.** A `"gammaM0"` invented on the design block survives in `UnknownMembers` and is
  named by `Validate()` as *"on Material 1 properties"*, which is `EnumerateExtensible`'s new entry
  working.
- **The whole suite.** 276 pass, 0 fail; 254 before.

## Deviations from the plan

1. **The two "no type" warnings are one rule with two wordings, not two rules.** The plan listed *a
   material stating no `Type`* and *a material stating `Quality` with no `Type`* as separate
   warnings. The second is a strict subset of the first, so a graded material with no type would
   have drawn both messages, and the repository's rule — stated in as many words in
   `ValidateSelfWeight` and `ReportMigrations` — is that one fact is stated once. The graded wording
   says strictly more, so it replaces the plain one rather than joining it. Asserted:
   `Warns_WhenAGradeHasNoCodeFamilyBehindIt` checks that exactly one message fires.
2. **`ValidateMaterialCompleteness` takes the `ValidationContext`.** The thermal half needs the
   element → material lookup; the plan placed the rule beside `ValidateSectionCompleteness`, which
   takes no parameters.
3. **`ValidationContext` gained a `Dictionary<int, int>`, not a `HashSet<int>`.** Convention 5 says
   new referenced collections get a set. A set answers *does this id exist*; this rule asks *what is
   this element made of*, so the map is what the question needs.
4. **The examples got more than a `type`.** The plan required only that each material gain one, so
   the new warning would not fire. Both files also gained `quality` and `thermalExpansion`, and
   Example2's S355 gained a `properties` block stating `fy` and `fu`. Example1 needed the α — its
   thermal load would otherwise have broken the silence gate — and the rest is deliberate: Example2
   is the steel reference file, and *"S355 with no fy anywhere in it"* was precisely the hole this
   bump closes. Example1 deliberately carries **no** `properties` block, so it stays the file that
   proves the bump additive, exactly as it does for sections.
   Example2's design values are stated in the same units its existing `strength` and
   `modulusOfElasticity` use. Those are Pa while the file's `units` block says `m`/`kN`, which is a
   pre-existing inconsistency in that file; it was matched, not corrected, because correcting it
   belongs to 1.8's units work and not here.

## Still open

- **Bump 1.8** — units as typed enums, `RestraintSense`, bedding semantics — not started. It is the
  only non-additive change among the bumps that proceed, and it hand-migrates both examples' units
  blocks.
- **Step 0'** — one real SAF 2.2.0 workbook, recorded in `Claude/FEMEX_SAF_Corpus_Notes.md`. It
  confirms `MaterialType`'s six spellings retroactively, and the two held bumps wait on it.
- **The stale-document corrections are deliberately not made yet.** The plan's *Documentation*
  section schedules them for the end of the pass, after 1.8: `FEMEX_SAF_Fit.md` §8.1 items 1 and 7
  and §4 items 6 (second half) and 8; `FEMEX_Interop_Review.md` §5.5;
  `FEMEX_Interop_Status_16082026.md` §5 item 4. All four are closed by this bump and none of them
  says so yet.
- **`MaterialType` does not tolerate an unknown value.** `"type": "Composite"` throws on read, as
  `SectionManufacture` and every other enum in the repository do. Consistent, and worth a decision
  of its own rather than a quiet exception here.
- **`StructuralMaterial.Subtype`** — *hot rolled*, *cold formed*, *stainless*, *prestressed
  concrete* — is still unmapped. `SectionManufacture` carries three of those four on the *section*,
  which `FEMEX_SAF_Fit.md` §7.8 records as the wrong object.
- **`Model.National code`** stays out, and partial safety factors stay out of `MaterialProperties`
  with it, for the reason its doc comment states: γ_M is a statement about which code is being
  applied at which limit state, and it changes between two checks of the same steel.
- **The end-to-end `Examples/Example3.femex`** waits for 1.8, which is where the plan puts it —
  remember its per-file `<None Include>` line in `griffel-femex.Tests.csproj`, which is deliberately
  not a glob.
