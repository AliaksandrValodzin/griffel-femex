# Units, restraint sense, bedding semantics — Implementation Summary

Implemented bump **1.8** of `Claude/FEMEX_SAF_Fit_Update_Plan.md` as written. Clean build
(0 warnings, 0 errors); **324 tests pass** (was 276 after 1.7, 254 before the pass). Both bumps that
proceed are now landed; the two held bumps still wait on Step 0'.

Two things a FEMEX file said badly, and one it did not say at all.

**The unit convention was two free-text strings.** `Units.Length` and `.Force` were `string?` with
comment-level guidance and no validation, so `"length": "banana"` round-tripped clean — an annotation
nothing could rely on, which is the only defect an annotation can have and the one
`FEMEX_SAF_Fit.md` §3 cites by name. Five enums now, three of them for quantities FEMEX had never
annotated: temperature, angle, mass.

**A restraint had no direction.** `Restraint { bool Fixed; double? Stiffness; }` spans three of SAF's
eight translation states. Five collapsed to a bidirectional restraint, and the collapse is the
product's own failure mode: an uplift-free pad bearing imported as a rigid support resists an uplift
the real pad cannot, and the model opens, validates, solves, and is wrong. `RestraintSense` takes
that to **seven of eight**.

**A spring stiffness had no dimension.** `FEMEX_SAF_Fit.md` §4 item 7 — *"two adapters could read the
same SAF file and differ by a factor of the slab area, with neither wrong against the spec, because
there is no spec"* — and §7.2 costs the fix at XS, *"documentation + validation, possibly no schema
change"*. That is exactly what it took: no schema change, a doc comment on two types, and one
warning.

This is the **first bump in FEMEX to rename a JSON key**, and the only non-additive change among the
bumps that proceed. Everything about how it was landed follows from that.

## New files

| File | What |
| --- | --- |
| `LengthUnit.cs` | `Millimetre, Centimetre, Metre, Inch, Foot` |
| `ForceUnit.cs` | `Newton, Kilonewton, Meganewton, PoundForce, Kip` |
| `TemperatureUnit.cs` | `Celsius, Fahrenheit, Kelvin` |
| `AngleUnit.cs` | `Degree, Radian` |
| `MassUnit.cs` | `Kilogram, Tonne, Pound, Slug` |
| `BoundaryConditions/RestraintSense.cs` | `Both, CompressionOnly, TensionOnly` |
| `FemexModel.Units.cs` | the 1.7 → 1.8 migration: `MigrateLegacyUnits()`, the two symbol tables, and the three private fields `ReportMigrations()` drains |
| `Examples/Example3.femex` | the end-to-end file the plan's *Verification* asks for |
| `griffel-femex.Tests/UnitsTests.cs` | 13 facts and two `[Theory]` symbol tables — 30 cases |
| `griffel-femex.Tests/Example3Tests.cs` | 7 facts, one of which is the model in code |

Each enum's doc argues why it is **closed**, which is the `SectionManufacture` argument read the
other way round: the set of national section libraries is open and still growing, whereas the set of
units an analysis model measures anything in is small and has been closed since the metre and the
foot. Each also names what it deliberately excludes — `Kilometre` and `Mile` from length,
`Tonne-force` and `Kilogram-force` from force, gradians from angle — because an enum that admits a
unit no structural model uses invites an exporter to write one.

Three of the five are annotation with **no consumer inside the library**, and each says so in its own
words rather than leaving it to be inferred. `AngleUnit` is the sharpest case: every angle in FEMEX
is degrees, stated on each of the three properties that carry one, so a file saying `Radian` is
contradicting the format rather than configuring it. `MassUnit` is sharper still — `Material.Density`
is ρ in whatever unit makes mass = force·time²/length consistent with the model's own force and
length, which is what `GetWeightDensity` relies on and what this enum cannot change.

## Modified

- **`Units.cs`** — rewritten. `LengthUnit? Length` keyed `lengthUnit` and `ForceUnit? Force` keyed
  `forceUnit`, then `Temperature`, `Angle` and `Mass` on the camelCase policy; the 1.6/1.7 spellings
  bound to getter-less `LegacyLength`/`LegacyForce`, drained through
  `TryTakeLegacyLength`/`TryTakeLegacyForce` exactly as `Material.TryTakeLegacyUnitWeight` is. The
  class doc records what the block **does not** supply: SAF's mandatory `Model.System of units` is
  one `Metric | Imperial` flag about a whole model, five independent enums can express `Metre` with
  `Kip`, and the column is therefore reported *Invented* — as are `Model.National code` and
  `Model.LCS of cross-section`.
- **`BoundaryConditions/Restraint.cs`** — `RestraintSense? Sense`, plus two factories,
  `CompressionOnly(k?)` and `TensionOnly(k?)`, beside the existing three. The class doc carries the
  eight-state mapping table and names the eighth, `Non linear`, as deliberately unmapped: it is a
  stiffness curve, not a state, and carrying it would mean a curve type rather than an enum value.
  `Stiffness`'s doc now states what the number is measured against per `SupportTarget`, and that
  SAF's Pasternak `C2` is unmapped.
- **`BoundaryConditions/Support.cs`** — the same three-line dimension table in the class doc, plus
  the note that one `Restraint` applied across all six DOFs is what makes a rotational
  compression-only restraint representable and meaningless. Stated on both types because the number
  lives on one and its meaning is set by the other, and a reader reaching either first must find it.
- **`FemexModel.cs`** — `CurrentSchemaVersion` `"1.7"` → `"1.8"`, a clause in the version-ledger doc
  comment, `"1.7"` appended to `ReadableSchemaVersions`, and the root's `Units` comment retitled from
  *"length/force convention"*.
- **`FemexModel.SelfWeight.cs:55`** — one call, `MigrateLegacyUnits()`, third in the hook after the
  1.1 and 1.2 migrations and in version order with them.
- **`FemexModel.Validation.cs`** — a `"1.7"` branch in `ValidateSchemaVersion()`, `"1.7"` on
  `SelfWeightVersions`, three new report blocks in `ReportMigrations()`, and
  `ValidateSupportCompleteness()` wired into the warning block after `ValidateMaterialCompleteness`.
  `FormatNameList` joins the rotational DOF names the way `FormatNumberList` joins ids.
- **`Examples/Example1.femex`, `Examples/Example2.femex`** — `schemaVersion` bumped **and both units
  blocks hand-rewritten** to `"lengthUnit": "Metre"` / `"forceUnit": "Kilonewton"`. Still
  byte-identical.
- **`griffel-femex.Tests/SampleModels.cs`** — `new Units("m", "kN")` became
  `new Units(LengthUnit.Metre, ForceUnit.Kilonewton)`.
- **`griffel-femex.Tests/RoundTripTests.cs:140`** — the same, on the assertion side.
- **`griffel-femex.Tests/MaterialTests.cs:361`** — the 1.6-file fact asserted the literal `"1.7"`
  stamp; it asserts `FemexModel.CurrentSchemaVersion` now, because what the fact is about is the
  1.7 material staying additive and the stamp bumps underneath it.
- **`griffel-femex.Tests/ValidationTests.cs`** — a `// ----- Supports -----` block of 11 facts.
- **`griffel-femex.Tests/griffel-femex.Tests.csproj`** — the third `<None Include>` line. The copy
  rule is per-file and not a glob; without it the new example fails with `FileNotFoundException`.
- **`Claude/FEMEX.md`** — one "Extended by" blockquote, the eighth in the root-design section.

`FemexModel.Unknown.cs` and `FemexModel.Identity.cs` are both **untouched**. The units block and the
six restraints have been in `EnumerateExtensible()` since 1.4, and this bump adds no nested type and
no identified entity — it adds enum-valued properties to two types already registered.

## The rename, and why there was no other way

`"length": "m"` and `"length": "Metre"` cannot share a key. A `JsonConverter` could read both, and it
would be the **first converter in this repository** — convention 4 says the global
`JsonStringEnumConverter` plus the camelCase policy cover everything, and the first exception is the
expensive one. New keys instead, with `[JsonPropertyName]`, which is the shim convention 4 already
allows for legacy.

That decision cascades:

- The old keys bind to **getter-less** properties, so `System.Text.Json` can never write them and a
  1.8 file cannot contain them. Same contract `Material.unitWeight` has held since 1.2, and the
  reason a migration can only run once.
- **Both example files had to be hand-migrated, not merely version-bumped.** Re-serialisation emits
  the new keys and cannot emit the old ones, so leaving them would have broken byte identity in two
  files and silence in four tests — `ReportMigrations()` is a *warning*, so a file still needing
  migration is not silent.
- Migration coverage therefore lives entirely in raw JSON literals, which is where gate 4 puts it
  anyway. Nothing was lost by moving the examples forward.

## What does not parse is not carried

`"length": "banana"` becomes **no length unit at all**, and the report names the text it dropped.
This is the only migration in FEMEX that loses something, and it is the point rather than a
regression in it: that text round-tripping clean is the defect §3 row 4 cites, so losing it loudly is
the change. The alternatives were a second free-text field beside the enum — the 1.7 design with an
extra step — or the converter.

The symbol tables are closed and deliberately not lenient. Each entry is a spelling some program
actually writes: the symbol, the enum's own name, and the American `-er` spelling of the three metric
lengths. A parser that guessed would be inventing units, which `FEMEX_Adapters.md` §4.3 calls the
category naive adapters never report *"because from inside the adapter an invention does not feel
like a loss — it feels like success."*

Three distinct reports come out of `ReportMigrations()`, not one:

| Case | What it says |
| --- | --- |
| parsed | *states a length of "m" as free text, which has been read as Metre* |
| unparseable | *names no unit this build knows. It has been dropped, and the model now states no length unit at all* |
| both spellings | *states the length unit both as free text and as a typed lengthUnit; the typed one is used* |

The third is the rule `MigrateLegacyUnitWeight` already applies to a material carrying both spellings
of its density: they cannot both be right, the newer one wins, and it is reported because silently
preferring one of two contradictory statements is what this repository never does. An **empty or
whitespace** value is none of the three — a key written and left blank says no more than one never
written, so it is not a migration and is not reported.

## Two warnings on supports

Both are new; `ValidateBoundaryConditions` is an error rule and stays one.

**The bedding rule** is the executable half of the documentation change. Saying *what* the number is
measured against fixes the §4 item 7 ambiguity; it does not fix the other half, because force/length³
is a dimension whose magnitude cannot be read at all without units — kN/m³ and kN/mm³ are nine orders
of magnitude apart. So an `Area` support stating a stiffness in a model that does not state **both**
its length and its force unit is warned about. Scoped to the area case deliberately: a point spring
is unit-dependent too, but it has been legal since the first commit, and this rule is not for nagging
every existing model about its units — the same line `ValidateMaterials` draws around what 1.7 added.

**The rotational rule** is the price of the factoring. `Support` applies one `Restraint` uniformly
across all six DOFs, which `FEMEX_Interop_Review.md` §3.5 rightly calls the universal pattern, and
which is exactly why the type cannot forbid `Rx.Sense = CompressionOnly`. A moment has no compression
side; the value parses, serializes, and describes nothing. A warning rather than a schema rule, which
is the line this repository draws everywhere — nothing the format forbids is ever only a warning, and
this the format permits. One message per support naming every rotational DOF, not one per DOF: three
DOFs of one mistake are one mistake, the discipline `ValidateMaterialCompleteness` follows for a
thermal load.

`Both` on a rotational DOF is **not** warned about. It is a true statement about any degree of
freedom; only the two directional values describe nothing there.

## Example3.femex

A glulam beam on two uplift-free bearings, and a concrete raft on soil — two fragments in one file
rather than one building, and deliberately: each is there for what it demonstrates, and a model
contrived to join them would have taught less per line.

- **Timber** because its measured G is nothing like E/(2(1+ν)) — 650 MPa stated against 4 423
  derived — so 1.7's stated-wins rule is visible in the file rather than merely present. `GL24h` with
  a `quality`, an α, and eight timber design values.
- **Two `Compression only` bearings**, which is what 1.7 could only write as a support resisting an
  uplift the real pad cannot.
- **An area support stating 50 000**, which is a Winkler bedding modulus in kN/m³ and readable as one
  only because the file states all five units.

It is authored **in code as well as in JSON**: `Example3Tests.Build()` is the same model, and one
fact asserts the two agree byte for byte. That is what lets either be read as the explanation of the
other, and it is how the file was produced rather than hand-typed and then checked.

## Verified

- **Byte identity, three files.** `Example1_ReSerializesToItself`, `Example2_ReSerializesToItself` and
  `Example3_ReSerializesToItself` all pass. The first two are the ones that mattered: a bump renaming
  a key cannot leave an example carrying the old spelling, and the hand-rewritten blocks were checked
  against the serializer rather than guessed at.
- **Silence, four models.** `SampleModels.Build()` and all three examples satisfy
  `Assert.Empty(model.Validate())`. Example3 is the interesting one — it exercises every rule this
  bump adds and trips none of them.
- **Additivity.** A model stating no units omits the key entirely; a block stating length and force —
  which is every model written so far — omits `temperature`, `angle` and `mass`; a restraint stating
  no sense writes no `sense` key, asserted across a whole sample model's worth of them.
- **Backward read.** A raw 1.7 literal with `"length": "m"` / `"force": "kN"` opens, migrates, is
  named by `Validate()`, and re-saves carrying `lengthUnit` and no `length`. Re-reading that output
  reports nothing, which is what makes the migration a property of the read.
- **`"banana"` dropped and named** — and asserted absent from the re-saved JSON *and* absent from
  `UnknownMembers`, because `length` is a declared property and never reaches extension data. That is
  what makes "dropped" mean dropped rather than moved.
- **Case-insensitive parsing**, 17 spellings across two `[Theory]` facts, including surrounding
  whitespace.
- **Forward read.** An `"energyUnit"` invented on the units block survives in `UnknownMembers` and is
  named by `Validate()` as *"on the units block"* — the pre-existing `EnumerateExtensible` entry
  working, with no registration added.
- **The seven SAF states**, each constructed and asserted, in one fact that is the mapping table
  executable.
- **The whole suite.** 324 pass, 0 fail; 276 before.

## Deviations from the plan

1. **`MigrateLegacyUnits()` lives in a new `FemexModel.Units.cs`, not in `FemexModel.SelfWeight.cs`.**
   The plan put it beside `MigrateLegacyUnitWeight`. But that migration is in the self-weight file
   for a stated reason — converting γ into ρ needs the root's gravity — and the repository's actual
   discipline is the one `MigrateLegacyLoadIds` follows: a migration lives beside the feature it
   concerns, which for load ids is `FemexModel.Identity.cs`. This one needs nothing but the string.
   The hook call is still at `FemexModel.SelfWeight.cs`, which is what the plan required of it.
2. **The `Units(string?, string?)` constructor is gone, not overloaded.** The plan did not mention
   it. A call written against 1.7 now fails to **compile**, which is deliberate: 1.7's
   `Material(…, density, …)` kept its shape while its meaning changed by a factor of g, and its own
   doc comment records that a positional call written against 1.1 *"still compiles and now means
   something a thousandth of the size the author intended"*. A loud break is the better of the two,
   and a bump that already renames a key is where FEMEX gets to choose it.
3. **Three migration reports, not two.** The plan named the parsed and the unparseable cases. The
   third — a block carrying both a free-text and a typed spelling of one unit — is reachable from
   any hand-edited file and needed its own wording; silently preferring the typed one would have been
   the class of thing the whole bump is against.
4. **The rotational-sense rule is a warning, not only a sentence.** The plan offered *"one sentence,
   or a warning if it is cheap where the other new rules go"*. A support-completeness method was
   being written for the bedding rule anyway, so it was cheap. Both were done: the sentence is on
   `Restraint.Sense` and on `Support`, and the warning is beside the bedding one.
5. **The bedding warning fires on a half-stated units block, not only on a missing one.** The plan
   said *"the model states no `Units`"*. Force per length cubed needs both halves, so a block naming
   one of the two is not an answer; a fact asserts it.
6. **`Restraint` gained two factories.** `CompressionOnly(k?)` and `TensionOnly(k?)` are not in the
   plan. The existing three — `FixedDof`, `Free`, `Spring` — are the 1.7 state space stated as
   constructors, and leaving the two new states to be assembled by hand would have made the seven-row
   mapping table something a caller re-derives each time. Each returns `Fixed = true` with no
   stiffness and `Fixed = false` with one, which is the table.
7. **The bump is ~48 facts, not ~20.** The plan targeted roughly +20 and ~295 total. The rename is
   what did it: a migration with three outcomes, two symbol tables worth asserting, and three
   reference files to hold to byte identity rather than two.

## Still open

- **Step 0'** — one real SAF 2.2.0 workbook, recorded in `Claude/FEMEX_SAF_Corpus_Notes.md`. Both
  held bumps wait on it, and it confirms 1.7's `MaterialType` spellings retroactively.
- **The held bumps**, 1.9 and 1.10 when they resume: load groups and spanning direction; member
  behaviour, analysis eccentricity and varying members. Each carries a defect found in review that
  the plan records against it.
- **`Model.System of units` is reported, not closed.** `FEMEX_SAF_Fit.md` §3 row 4 stays open, and
  the plan says so in as many words: SAF's column is one `Metric | Imperial` flag about a whole
  model, and five independent enums do not supply it and permit combinations mapping to neither.
  `Model.National code` and `Model.LCS of cross-section` stay *Invented* beside it.
- **SAF's eighth translation state, `Non linear`**, and its Pasternak `C2`, both stay unmapped and
  both are now recorded as such on the type rather than only in a review document.
- **The unit enums do not tolerate an unknown value.** `"lengthUnit": "Furlong"` throws on read, as
  every other enum in the repository does. Consistent, and still worth a decision of its own — the
  same note 1.7 left against `MaterialType`.
- **Nothing in the library converts by these enums**, and nothing should without a decision: a
  format that silently rescales numbers on read is a different product from one that annotates them.
