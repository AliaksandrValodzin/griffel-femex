# Standard sections — catalogue identity, parametric shapes, and the numeric escape hatch

> **Step 0 (repo convention):** this document sits alongside `Claude/FEMEX.md`, `Claude/FEMEX_Plates.md`,
> `Claude/FEMEX_Node_Sharing.md`, `Claude/FEMEX_Gridlines.md`, `Claude/FEMEX_LoadCombinations.md`,
> `Claude/FEMEX_BarLocalAxes_LoadDirection.md`, `Claude/FEMEX_SelfWeight.md`, `Claude/FEMEX_Identity.md`
> and `Claude/FEMEX_Metadata.md`; `Claude/FEMEX_StandardSections_Summary.md` will record what was
> actually built.

---

## Context

`Claude/FEMEX_Interop_Status_16082026.md` §2.1 calls sections **"the largest remaining hole"** — one of
the two P0 items that were never done, and the one that still makes a transfer lossy in a way the
receiver cannot detect. With item 1 closed as schema 1.4, this is items 2 and 3 of that note's
recommended order.

`Geometry/Sections/` is `Rectangle` (width, depth), `Circle` (diameter) and `TSection` (flange width,
flange thickness, web thickness, total depth). The base `Section` (`Geometry/Sections/Section.cs`)
carries `Id`, `Uid`, `Name` and one abstract `CalculateArea()` — the only derived section quantity
anywhere in the repository. There is no second moment of area, no torsion constant, no shear area, no
section modulus, no profile designation in any file.

Review §4.4's three consequences all still hold in full:

1. **No steel.** No I/H, channel, angle, box or hollow shape exists. A steel frame cannot cross FEMEX
   at all.
2. **No catalogue identity.** `Section.Name` is a free label, validated only for blanks and duplicates
   by `ValidateNameKeys` (`FemexModel.Validation.cs:185`). Robot uses
   `secData.LoadFromDBase("HEB180")` against a `Preferences.SetCurrentDatabase` selection, ETABS
   `FILE "AISC15.xml" SHAPE "W12X26"`, RFEM a library key, SAF `Profile` plus a CIS/2-derived form
   code. FEMEX can say none of it.
3. **No numeric escape hatch.** Nothing lets an unrecognised shape round-trip by its stiffness, so
   anything FEMEX has no class for is *lost* rather than *degraded*.

`Claude/FEMEX_Adapters.md` §4.2 states the same fact as the loss taxonomy's largest single hole: a
steel member crossing FEMEX today is **Dropped**, not Approximated — *"there is no shape to approximate
it with."*

**Intended outcome.** After this change a steel frame crosses FEMEX by name where the receiver knows
the library, by dimensions where it knows the shape, and by stiffness where it knows neither — so a
section is never lost, only degraded.

**"Receiver" means an adapter, not an older FEMEX build.** The graceful degradation below is a
property of the JSON as read by a program that resolves what it recognises and ignores what it does
not. It is *not* forward compatibility for this library: System.Text.Json throws on an unrecognised
polymorphic discriminator, so a 1.4 build handed `"type": "ishape"` fails to deserialize and never
reaches the `properties` block it could have degraded to. `ReadableSchemaVersions` refuses that file
first — by design, and stated here so the promise is not read as wider than it is.

### The constraint that shapes the fix

There are thousands of standard profiles across a dozen national standards, and this is the first
question the work has to answer: what does FEMEX actually ship?

**It ships none of them.** It ships the vocabulary to *name* any of them and the numbers to survive
*not recognising* one.

A `.femex` file contains only the sections its model uses, each carrying its catalogue name **and** its
resolved numbers, so the file stays self-contained: the receiver resolves by name if it has the
library, and falls back to stiffness if it does not. There are no catalogue rows anywhere in the
repository — no curation, no licensing, no AISC-v14-versus-v15 versioning, and no duplication of tables
Robot, ETABS and RFEM already ship as a core feature. `griffel-femex.csproj` keeps its zero
`PackageReference`s and its zero embedded resources, both of which it has had since the first commit.

The database is a **vocabulary**, not **data**. Everything below follows from that.

---

## The eight decisions

### 1. Three orthogonal layers on one section, not three sibling subtypes

Review §4.4's literal wording — *"a `catalogue` type … and a `numeric` type"* — makes them **siblings**
in the discriminated union, so a section would be *either* named *or* dimensioned *or* numeric.

That is the wrong shape, and it fails on its own motivating example. A real IPE300 is all three at
once. As siblings, a `catalogue` section that the receiver cannot resolve carries no numbers to fall
back on — which is precisely the loss the escape hatch exists to prevent. The sibling design would
leave the format's most-used section type as the one type that still cannot degrade.

So the three layers sit on **one** section, any subset, at least one:

```
Section  (Geometry/Sections/Section.cs)
 ├ id, uid, name                                        existing
 ├ catalogue?  { source, profile, manufacture }         ← identity   (1.6)
 ├ type ─────► rectangle | circle | tshape              existing
 │             ishape | channel | angle | box | pipe    ← geometry   (1.6)
 │             generic                                  ← geometry   (1.5)
 │             + that shape's dimensions
 └ properties? { area, iy, iz, j, … }                   ← stiffness  (1.5)
```

This is SAF's own shape. SAF carries cross-section type, profile name, form code, shape parameters
*and* optional `A, Iy, Iz, It, Iw, Wply, Wplz` on one row — review §1.6 records it, and review §7.1's
verdict is that FEMEX is *"a better plate model and a worse section model"* than SAF. This closes the
second half.

It is also strictly additive: six `[JsonDerivedType]` lines and two nullable properties on the base.
Nothing existing moves, no field is renamed, and the union absorbs all of it — exactly as review §4.4
predicted it would.

### 2. Precedence, stated once

A receiver takes the richest layer it can act on:

> **Resolve the catalogue name; else build the parametric shape; else build a member with the stated
> stiffness.**

And where a stated property exists it is **authoritative over the parametric one**. A tabulated IPE300
area is 5.381e-3 m²; the parametric formula over the same four dimensions gives 5.188e-3, about 3.6%
low, because the tabulated number includes root fillets that no idealisation carries. The stated number
is the measured one and wins.

### 3. `SectionProperties` — the escape hatch, in two named groups

`Geometry/Sections/SectionProperties.cs`, `public class SectionProperties : IExtensible`. Every field
`double?`, so *not stated* is distinct from zero — the same distinction `Restraint.Stiffness` already
draws.

| Analysis — what a solver needs | Design — what SAF carries and a checker wants |
| --- | --- |
| `Area` (A) | `Iw` — warping constant |
| `ShearAreaY`, `ShearAreaZ` (Ay, Az) | `Wely`, `Welz` — elastic section moduli |
| `Iy`, `Iz` — second moments of area | `Wply`, `Wplz` — plastic section moduli |
| `J` — torsion constant (SAF's `It`) | |

Robot exposes the first group as `I_BSDV_AX`, `I_BSDV_IX`, `I_BSDV_IY`, `I_BSDV_IZ` and friends; SAF
carries both groups. The design group is included for the reason `Material.Strength` is — *"not needed
for analysis, but useful for design"* — and because it is what makes a SAF conversion lossless rather
than merely possible.

**The axes are already pinned, and this is the quiet win.** `Iy` is the second moment about the bar's
local y, which `Geometry/Bar.cs` defines in full and `FemexModel.TryGetBarLocalAxes`
(`FemexModel.LocalAxes.cs`) makes executable. Before 1.1 this addition would have needed a convention
invented alongside it; §4.2 already paid that cost, so the doc cites it and invents nothing.

### 4. `GenericSection` — the numbers-only shape, and `GetArea()`

`Geometry/Sections/GenericSection.cs`, discriminator `"generic"`. A section with no geometry at all —
SAF's `General`. It is the type an adapter reaches for when the native model had a shape FEMEX does not
model, and it is the whole of what converts **Dropped** to **Approximated**.

`CalculateArea()` stays exactly as it is: geometry only, abstract, unchanged for the existing three.
`GenericSection` returns `0.0` from it, with a doc comment saying it has no geometry and that
`GetArea()` is the only meaningful accessor. One new non-abstract method on the base carries decision 2:

```csharp
/// The area to build a member with: the stated one where the section carries it,
/// the parametric one otherwise.
public double GetArea() => Properties?.Area ?? CalculateArea();
```

`FemexModel.SelfWeight.cs:153` changes `section.CalculateArea()` to `section.GetArea()`. That single
call site is the change's **only** behavioural effect on an existing model, and it is a correctness
improvement in the direction decision 2 argues: a section that states its area now weighs by the
tabulated number rather than the idealised one.

### 5. Five parametric shapes, named so the union reads as one family

Review §2.2 lists I/H, channel, angle and box as a single P0 row, and status item 3 adds hollow. Field
names are chosen so that the nine discriminators read as one vocabulary rather than nine unrelated
classes — `box` is a hollow `rectangle`, `pipe` is a hollow `circle`, and `ISection` and `Channel` reuse
`TSection`'s four field names exactly:

| File | Discriminator | Fields | `CalculateArea()` |
| --- | --- | --- | --- |
| `ISection.cs` | `ishape` | `FlangeWidth`, `FlangeThickness`, `WebThickness`, `TotalDepth` | `2·bf·tf + tw·(h − 2·tf)` |
| `Channel.cs` | `channel` | the same four | the same |
| `Angle.cs` | `angle` | `LegLengthY`, `LegLengthZ`, `Thickness` | `(ly + lz − t)·t` |
| `Box.cs` | `box` | `Width`, `Depth`, `WallThickness` | `w·d − (w−2t)·(d−2t)` |
| `Pipe.cs` | `pipe` | `Diameter`, `WallThickness` | `π/4·(D² − (D−2t)²)` |

Channel and I share an area formula because they differ only in where the web sits, which moves the
centroid and not the area — worth a comment at both sites so the duplication reads as deliberate.

Doubly-symmetric I only: it covers IPE, HEA, HEB, UB, UC and W, which is very nearly all rolled steel.
Asymmetric, tapered and compound go into a reserved-discriminator doc comment on `Section`, following
the precedent `Geometry/Surfaces/SurfaceProperty.cs` already sets with its unimplemented `"variable"`
and `"layered"`. `Section.cs` has no such reserved list today; this adds one.

### 6. `SectionCatalogue` — free-text source, closed-enum manufacture

`Geometry/Sections/SectionCatalogue.cs`, `public class SectionCatalogue : IExtensible`, reached as
`Section.Catalogue`:

| Field | Type | Why |
| --- | --- | --- |
| `Source` | `string?` | The library or standard **as the producing program names it** — `"Euronorm"`, `"AISC15.xml"`, `"BS 5950"`. Provenance, not a controlled vocabulary. |
| `Profile` | `string?` | The designation as that program spells it — `"IPE300"`, `"W12X26"`, `"HEB180"`. |
| `Manufacture` | `SectionManufacture?` | `HotRolled \| ColdFormed \| Welded \| Other`. |

**`Source` is free text and `Manufacture` is an enum, and the line between them is the design.** The set
of national standards is open, unbounded and still growing; a closed enum could never be complete and
would need a schema bump per country — which is the whole of what "thousands of sections from various
countries" costs a format that tries to enumerate them. The set of ways a steel section is made is
small and closed, and it is exactly the distinction Robot's documented `SHSH` versus `SHSC`
hot-versus-cold-formed naming problem turns on — review §4.4 cites it as the worked example of
catalogue naming failing.

The same argument distinguishes this from status item 5: units *should* become enums, because there are
about a dozen of them and the set is closed. Standards are not that.

### 7. No CIS/2 form code, and no normalisation

SAF carries a CIS/2-derived form code specifically to disambiguate one profile name across vendor
libraries. FEMEX does not need one, because **the `type` discriminator already is a form code** —
`ishape` says what SAF's form code says, in the same file, one key earlier. The one distinction the
discriminator cannot make is manufacture, and `Manufacture` covers it in four values.

Importing 111 CIS/2 codes to restate a distinction the discriminated union already makes is the
opposite of concise, and it would put two fields in every file that must agree and could disagree.
Recorded here as a decision with its rejected alternative rather than left as an omission.

Likewise **no normalisation**: `"IPE 300"` and `"IPE300"` are stored exactly as written. Matching
profile names across libraries is an adapter's job against its own database, and a format that
normalises silently makes the round trip lossy in the one place it was trying to be lossless.

### 8. Two version bumps, in the order status §5 gives

Status §5 sequences items 2 and 3 deliberately: item 2 first *"so that item 3 failing to recognise a
profile is survivable."* The escape hatch has to exist before the shapes do, otherwise an `ishape` whose
profile the receiver does not recognise is back to being lost.

| | What lands | Effect |
| --- | --- | --- |
| **1.5** | decisions 3 and 4 — `properties`, `generic`, `GetArea()`, self-weight, validation | Every shape FEMEX does not model goes from **Dropped** to **Approximated** |
| **1.6** | decisions 5, 6 and 7 — five shapes, `catalogue`, validation | Steel crosses by name |

Both bumps are **purely additive**. No field is renamed, no meaning changes, so there is no migration,
no `Material.UnitWeight`-style legacy shim, and `ReportMigrations()` is untouched. Every existing file
reads unchanged.

---

## Data model

```
Geometry/Sections/
  Section.cs             + SectionProperties? Properties
                         + SectionCatalogue?  Catalogue
                         + double GetArea()
                         + 6 [JsonDerivedType] lines
                         + reserved-discriminator doc comment
  SectionProperties.cs   new — 11 double?, IExtensible
  SectionCatalogue.cs    new — Source, Profile, Manufacture, IExtensible
  SectionManufacture.cs  new — HotRolled | ColdFormed | Welded | Other
  GenericSection.cs      new — no geometry
  ISection.cs            new
  Channel.cs             new
  Angle.cs               new
  Box.cs                 new
  Pipe.cs                new
```

`SectionProperties` and `SectionCatalogue` are **value blocks, not authored entities** — like
`Restraint` and `Release` they are `IExtensible` but **not** `IIdentified`, so
`FemexModel.Identity.cs:81` is untouched. Each carries a parameterless constructor for serialization
and a convenience constructor, matching every other type in the repository.

## Resulting JSON

```json
{
  "type": "ishape",
  "flangeWidth": 0.150, "flangeThickness": 0.0107,
  "webThickness": 0.0071, "totalDepth": 0.300,
  "id": 7,
  "name": "IPE300",
  "catalogue": { "source": "Euronorm", "profile": "IPE300", "manufacture": "HotRolled" },
  "properties": { "area": 5.381e-3, "iy": 8.356e-5, "iz": 6.038e-6, "j": 2.012e-7 }
}
```

Note the key order. `catalogue` and `properties` are declared on the **base**, so System.Text.Json
writes them after the derived type's dimensions and after `id`/`name` — the same ordering
`Example1.femex` already shows, where `"id"` and `"name"` follow `"flangeWidth"`. Harmless, and stated
here so a reader does not take it for a bug. `[JsonPropertyOrder]` would hoist them; the repository does
not use it anywhere and this is not the change to start.

A section that states no catalogue and no properties writes **neither key** — `DefaultIgnoreCondition =
WhenWritingNull` (`FemexModel.cs:150`) is already set, so no existing file gains a byte, exactly as in
the 1.4 pass.

## `Validate()` additions

A new `// ----- Sections -----` block — the first section-specific validation in the file's history,
sections being currently validated only for duplicate ids (`FemexModel.Validation.cs:73`), name keys
(`:187`) and as a bar's referent (`:425`).

**Two methods, not one.** `Validate()` (`:21-58`) wraps severity **per method at the call site** —
`foreach (var m in ValidateBars(ctx)) yield return ValidationMessage.Error(m);` — so a single
validator cannot emit both severities. The errors and the warnings split exactly as `ValidateGrids`
(`:31`, error) and `ValidateGridGeometry` (`:54`, warning) already do:

| Method | Call site | Yields |
| --- | --- | --- |
| `ValidateSections()` | the error block | E1, E2 |
| `ValidateSectionCompleteness()` | the warning block | W1, W2, W3 |

Neither needs the `ValidationContext`, so both take the no-argument form `ValidateNameKeys()` uses.
Both yield bare strings, in the house pattern.

**Errors** — the model cannot be built as written:

| | Message |
| --- | --- |
| **E1** | `Section 7 is generic and states no area, so it has no geometry and no stiffness; nothing can be built from it.` |
| **E2** | `Section 7 states an area of -0.01, which is not a positive quantity.` (also `iy`, `iz`, `j`, the shear areas and the moduli). **Zero is rejected too** — a stated property is a claim about stiffness, and zero is not a claim a solver can build with. This is not in tension with leaving a zero-width `Rectangle` legal: that field exists today and a file using it is valid FEMEX, whereas these fields are new and can be given a contract from the start. |

**Warnings** — legal FEMEX, and a receiver gets it wrong:

| | Message |
| --- | --- |
| **W1** | `Section 7 states an area of 5.38 and its dimensions give 0.0054; one of the two is wrong.` — fires beyond **10%**, and **only for a section that has dimensions**. Root radii and fillets explain a few percent, as decision 2 shows; ten is past what any idealisation accounts for, and a unit error is what it usually is. |
| **W2** | `Section 7 is generic and states an area but no iz; every bar using it will weigh correctly and bend wrongly.` — fires when **either** `iy` or `iz` is absent, naming the one that is. |
| **W3** | `Section 7 names profile "IPE300" with no source; the same designation names different profiles in different libraries.` — the failure SAF's form code exists to prevent. |

**W1 and W2 partition the space, and each is scoped so the other's case cannot trip it.**

W1 excludes `generic` for a reason that would otherwise be a bug: `GenericSection.CalculateArea()`
returns `0.0` by decision 4, so an unscoped W1 would read every correctly-authored generic section — a
stated 5.381e-3 against a computed 0.0 — as a 100% disagreement and fire on the exact case 1.5 exists
to make legal. Where there are no dimensions there is nothing to disagree with.

W2 is scoped to `generic` for the mirror reason. An `ishape` with no properties is fine — it hands the
receiver four dimensions, and every one of the five target programs can integrate them. It is only when
there is no geometry *and* no stiffness that the number is unrecoverable.

So W1 is *geometry and stiffness disagree*, W2 is *no geometry, and the stiffness is incomplete*. No
input trips both, and a generic section carrying `area`, `iy` and `iz` trips neither.

E1 already covers the dangerous catalogue case, which is why there is no separate rule for it: a
`generic` section named `"IPE300"` with no properties is an **error**, because a receiver without that
library gets nothing at all from it.

Extensibility: `FemexModel.Unknown.cs:61` gains the two nested blocks, so an unknown member inside
`properties` is reported as *"on Section 1 properties"* rather than swallowed:

```csharp
foreach (var section in Sections)
{
    yield return (section, $"Section {section.Id}", "sections");

    if (section.Catalogue is not null)
        yield return (section.Catalogue, $"Section {section.Id} catalogue", "section catalogues");

    if (section.Properties is not null)
        yield return (section.Properties, $"Section {section.Id} properties", "section properties");
}
```

## `Examples/`

`Example1.femex` is 109 KB, entirely concrete, and is the byte-identical round-trip regression
(`Example1_ReSerializesToItself`). It gets its `schemaVersion` bumped at each step and **nothing else
touched**.

`Examples/Example2.femex` is new at 1.6: a small steel portal frame — around six nodes, three bars, an
S355 material, and sections that exercise all three layers together. It is the first file in the
repository that a steel adapter author can read, and it gets the matching pair of tests. The alternative
— editing a steel member into Example1 — would disturb the self-weight and validation assertions that
109 KB file anchors, for no gain.

**It has to be authored to validate silently**, which constrains it more than its size suggests. If
`Example2_LoadsAndValidates` asserts zero messages the way Example1's does, then:

- every section, material and load case needs a non-blank, non-duplicated `Name`, or `ValidateNameKeys`
  (`:185`) warns;
- some load case must carry a non-zero `SelfWeightFactor`, or `ValidateSelfWeight` (`:874-890`) warns
  *"No load case carries self-weight"* — the file has bars and a non-zero density, so it has something
  to weigh, and 1.6 will be in `SelfWeightVersions` by then;
- the sections it uses must satisfy W1, W2 and W3 — so the `ishape` carrying tabulated properties needs
  its dimensions within 10% of them, and its `catalogue` needs a `source` beside its `profile`.

`griffel-femex.Tests/griffel-femex.Tests.csproj:30-33` copies the example to the test output with an
**explicit per-file** `<None Include>`, not a glob, and every example test resolves
`Path.Combine(AppContext.BaseDirectory, "Examples", ...)`. Example2 needs its own second line there or
both its tests fail with `FileNotFoundException`.

---

## Critical files

| File | What changes, and why it matters |
| --- | --- |
| `Geometry/Sections/Section.cs` | The three-shape union behind `FEMEX_Adapters.md` §4's *Dropped* ruling. Gains two properties, `GetArea()`, six `[JsonDerivedType]` lines and the reserved list. |
| `Geometry/Sections/` × 9 new files | Decisions 3–7. |
| `FemexModel.cs:54,64` | `CurrentSchemaVersion` → `"1.5"` then `"1.6"`; `ReadableSchemaVersions` grows twice; the version doc comment gains a sentence per bump, as it has at every bump. |
| `FemexModel.Validation.cs` | The new `// ----- Sections -----` block, as **two** methods across the error and warning call sites — **and `SelfWeightVersions`, see Risk.** |
| `FemexModel.SelfWeight.cs:153` | `CalculateArea()` → `GetArea()`. The only behavioural change to an existing model. |
| `FemexModel.Unknown.cs:61` | The two nested value blocks. |
| `griffel-femex.Tests/SampleModels.cs:135-140` | The fixture's three sections gain the new layers; every one of the 224 existing facts builds on it. |
| `Examples/Example1.femex`, `Example2.femex` | Version bump; new steel example. |
| `griffel-femex.Tests/griffel-femex.Tests.csproj:30-33` | A second `<None Include>` for `Example2.femex`. The copy rule is per-file, not a glob; without it both Example2 tests fail with `FileNotFoundException`. |
| `Claude/FEMEX.md` | The format spec of record: a `> **Extended by …**` blockquote per bump, see task 4. |

Untouched, and worth stating: `FemexModel.Identity.cs`, `Geometry/Bar.cs`, `ReportMigrations()`,
`griffel-femex.csproj` — the *library* csproj, whose zero `PackageReference`s the constraint above
turns on, as distinct from the test csproj that gains a line.

## Risk, and the trap to avoid

`FEMEX_Metadata_Summary.md` records this one biting on the last bump, and it will bite twice here.

`ValidateSelfWeight` gates on a `SelfWeightVersions` **matched list**, not a comparison, because
`FemexModel.cs:56-63` explicitly declines to have a version-*ordering* policy — *"inventing one here
would be inventing behaviour for versions that do not exist yet."* Bumping `CurrentSchemaVersion`
without adding the new version to that list silently stops the "no load case carries self-weight"
warning from firing for files at the previous version. It costs one line per bump and is paid
knowingly. **Two bumps, two lines.**

A smaller one, already resolved by evidence: `Section` is polymorphic, and 1.4's fact 8 established that
System.Text.Json 7 consumes the `"type"` discriminator *before* extension data is populated, on both the
root and polymorphic entities. Six new discriminators do not change that, so no new subtype leaks
`"type"` into `UnknownMembers`.

## Tests — `griffel-femex.Tests/SectionTests.cs`

xUnit `[Fact]`s only, `Reports_` / `Warns_` / `Accepts_` verb convention, assertions against
message-text fragments. Baseline is **224** — 224 `[Fact]`, no `[Theory]`, so facts and test cases are
the same number.

`AssertReports` and `AssertWarns` are **not shared helpers**: there is no test base class, and five
files each declare their own private statics with identical bodies (`ValidationTests.cs:13,18`,
`RoundTripIdentityTests.cs:544,549,554`, `MetadataTests.cs:291`, `SelfWeightTests.cs:200`, and
`LoadDirectionTests.cs:24`, which names its error overload `AssertReportsError`). `SectionTests.cs`
declares its own pair, following that convention rather than introducing a base class.

**1.5 — fifteen facts.** `properties` round-trips on each existing subtype; an omitted block writes no
key; `GetArea()` returns the stated area; `GetArea()` falls back to `CalculateArea()` when absent;
self-weight uses the stated area and not the parametric one; `generic` round-trips with its
discriminator; `GenericSection.CalculateArea()` is zero while `GetArea()` is the stated area;
`Reports_` a generic with no area; `Reports_` a negative stated property; `Warns_` on a disagreement
past 10%; `Accepts_` a fillet-sized disagreement; `Warns_` a generic with an area but no `iy`; an
unknown member inside `properties` round-trips and reports *"on Section 1 properties"*; and
`Example1_LoadsAndValidates` still yields **zero** messages.

And the fifteenth, which the W1 scoping earns: `Accepts_` a generic section stating `area`, `iy` and
`iz` with **no** messages at all — the regression that catches an unscoped W1 firing on
`CalculateArea()`'s zero.

**1.6 — fourteen facts.** One per new shape asserting a hand-computed area and its discriminator (five);
`catalogue` round-trips; an omitted catalogue writes no key; `Manufacture` serializes as a string
through the existing `JsonStringEnumConverter`; `Warns_` a profile with no source; `Accepts_` an
`ishape` carrying a catalogue and no properties, since geometry is the fallback; an unknown member
inside `catalogue` reports *"on Section 1 catalogue"*; all **nine** discriminators survive one round
trip; `Example2_LoadsAndValidates` and `Example2_ReSerializesToItself`.

## Ordered tasks

1. **1.5** — decisions 3 and 4: `SectionProperties`, `GenericSection`, `Section.Properties`,
   `GetArea()`, the self-weight call site, the extensible walk, `ValidateSections` (E1, E2) and
   `ValidateSectionCompleteness` (W1, W2), the version bump **and its `SelfWeightVersions` line**, the
   fifteen facts, `Example1.femex`. Build, test, commit.
2. **1.6** — decisions 5, 6 and 7: the five shapes, `SectionCatalogue`, `SectionManufacture`, W3, the
   second version bump **and its `SelfWeightVersions` line**, the fourteen facts, `Example2.femex`
   **and its `<None Include>` line in `griffel-femex.Tests.csproj`**. Build, test, commit.
3. `Claude/FEMEX_StandardSections_Summary.md`, in the shape the nine existing `_Summary.md` documents
   share: test count and build state first, then `## New files`, `## Modified`, the topic sections,
   `## Verified`, `## Deviations from the plan`, `## Still open`.
4. **Documents to correct**, in the same pass:
   - `Claude/FEMEX.md` — the format spec of record. It documents the section union literally
     (`:95-102`, the three current `[JsonDerivedType]` lines), the `CalculateArea` decision
     (`:143-149`) and the running schema version, and this change touches all three. A
     `> **Extended by `Claude/FEMEX_StandardSections.md`:**` blockquote per bump, following
     `FEMEX_LoadCombinations.md:377`, `FEMEX_SelfWeight.md:567` and `FEMEX_Identity.md:260`, each of
     which scheduled one as an explicit task. **The 1.4 metadata pass did not**, which is why
     `FEMEX.md` today stops at 1.3 with no mention of `FileMetadata` or `IExtensible`; close that gap
     in the same edit rather than letting a second version open behind it.
   - `FEMEX_Adapters.md` §4.2 — says sections are *Dropped* and names the fixing change precisely
     (*"status item 2 — the numeric A/Iy/Iz/J escape hatch"*). 1.5 is that change; the row becomes
     *Approximated*. While the file is open, §4.5 at `:500` still says `CurrentSchemaVersion` is
     `"1.3"` with three readable versions at `FemexModel.cs:50,60` — stale since 1.4.
   - `FEMEX_Adapters_Plan.md` §4 — the Approximated bullet also says sections are *Dropped*, though it
     names only *"until review §4.4 lands"* rather than the escape hatch by name.
   - `FEMEX_Interop_Review.md` §4.4 — an inline blockquote. §4.1 (`:376`), §4.3 (`:449`) and §4.6
     (`:563`) carry the only three in the file; **§4.2 carries none**, and two of the three read
     `> **Closed by …**` against §4.3's *Addressed by*. Follow the majority: `> **Closed by
     `Claude/FEMEX_StandardSections.md`** (schema 1.5 and 1.6).`
   - `FEMEX_Interop_Status_16082026.md` §2.1 and §5 items 2 and 3. Its §0 and §2.2 also still describe
     the pre-1.4 world — 211 facts, `"schemaVersion": "1.3"`, *"`UnmappedMemberHandling` is still not
     set"* — and can be brought current in the same edit.

## Verification

```powershell
dotnet build
dotnet test griffel-femex.Tests\griffel-femex.Tests.csproj
```

**Name the test project explicitly.** `griffel-femex.sln` contains only the library — the test project
is not in it — so a bare `dotnet test` at the repo root builds the solution, reports *"Build succeeded,
0 Warning(s), 0 Error(s)"* and runs **zero** tests without ever saying so. It is an easy success to
mistake for a real one, and every step below depends on not making that mistake.

1. **0 warnings, 0 errors**; **~253 tests pass** (224 + 15 + 14), 0 failed. The baseline was confirmed
   before this work started: `Passed! - Failed: 0, Passed: 224`.
2. **Both** tests that assert Example1 is silent still do — `Example1_LoadsAndValidates`
   (`ValidationTests.cs:735`) and `Example1_CarriesLoadIdsAndNoUids` (`RoundTripIdentityTests.cs:481`),
   each of which calls `Assert.Empty(model.Validate())` — and `Example1_ReSerializesToItself` is
   byte-identical after **each** bump. Together: the check that both changes really are additive and
   that a null `properties` and a null `catalogue` write not one extra byte.
3. `Example2_ReSerializesToItself` — byte-identical, which is what proves the three layers serialize in
   a stable order.
4. **The hand check that motivates the whole item.** An IPE300 authored as `ishape` + `catalogue` +
   `properties`, loaded and re-saved: `CalculateArea()` returns the parametric **5.188e-3**, `GetArea()`
   returns the tabulated **5.381e-3**, and `TryGetBarSelfWeightPerLength` uses the latter — the 3.6% the
   root fillets account for, which is decision 2 as a number.
5. A `generic` section with `properties` stripped produces exactly one **Error**; the same section with
   only `iz` stripped produces exactly one **Warning**; and with `area`, `iy` and `iz` all present it
   produces **none** — the third of these is what proves W1 is scoped away from `generic`, since an
   unscoped W1 would read `CalculateArea()`'s zero as a 100% disagreement and fire on all three.
6. `SelfWeightTests.cs:427` still passes untouched. It calls `section.CalculateArea()` directly against
   Example1, whose sections carry no `properties`, so `GetArea()` falls back and the assertion holds —
   the evidence that the `:153` call-site switch changes no existing model.

## Considered and rejected

- **Sibling `catalogue` and `numeric` subtypes**, as review §4.4 literally proposes. Decision 1: it
  fails on its own motivating example, because an unresolvable catalogue section would carry no numbers.
- **A bundled section database** — embedded tables for Euronorm, AISC and the rest, with a
  `SectionCatalogue.TryResolve` lookup. It would give the repository its first embedded resource and
  its first curation, licensing and versioning burden, to duplicate tables all five target programs
  already ship. The layered file makes it unnecessary: the numbers travel *with* the name.
- **An `ISectionCatalogue` provider interface with no bundled rows.** Better than shipping data, and
  still premature — there is no connector yet to plug into it, and review §7.3's admission stands that
  nothing here has met a real file. It stays available and is not built.
- **`Source` as an enum.** Decision 6: the set is open, national and unbounded.
- **A CIS/2 form code.** Decision 7: the `type` discriminator already is one.
- **Normalising profile designations.** Decision 7: it makes the round trip lossy in the one place it
  was trying not to be.
- **Validating the existing dimension fields for positivity.** Validating *new* fields cannot invalidate
  a file that is legal today; validating the existing ones can, and a `Rectangle` with a zero width has
  been legal FEMEX since the first commit. It goes to *Still open*, not into an additive change.
- **Editing a steel member into `Example1.femex`** rather than adding Example2 — it would disturb the
  self-weight and validation assertions that file anchors.

## Deliberately out of scope

- **`SurfaceProperty`'s equivalent escape hatch.** Review §7.3 asks whether the plate counterpart should
  be an explicit stiffness matrix, which RFEM has (`TYPE_STIFFNESS_MATRIX`) and SAF does not. A separate
  question about a separate hierarchy.
- **Section-material coupling.** Review §7.3's other open question — Robot carries
  `IRobotBarSectionData.MaterialName` *and* a bar material label, and reading a Robot model means
  reconciling the pair. FEMEX's independent references stay independent.
- **Tapered, asymmetric and compound sections**, reserved in the doc comment and not implemented, on
  the `SurfaceProperty` precedent.
- **Composite and reinforced sections.** Not in review §4.4, not in status §5.

## Still open

- **Whether `Bar.SectionId` and `Bar.MaterialId` should become `int?`** like their `Plate` counterparts.
  `FEMEX_Adapters.md` §2.2 raises it on its own founding example — a bar drawn before a section has been
  chosen carries `SectionId = 0` and is un-exportable — and §9 parks it as a schema question. This change
  makes it more pressing, not less, because a `generic` section is now the cheapest honest placeholder an
  adapter can synthesise. Deliberately not settled here.
- **Principal axes.** An angle's `Iy` and `Iz` are about geometric axes; its principal axes are rotated
  from them. No `Iu`, `Iv` or `PrincipalAngle`, so an angle crosses with geometric-axis stiffness only —
  a real approximation for a single angle in bending, and the first thing a real file is likely to
  challenge.
- **Whether `Section.Name` and `Catalogue.Profile` should ever be reconciled.** They will usually hold
  the same string. `ValidateNameKeys` warns on a blank or duplicated `Name`; nothing warns when the two
  disagree, and it is not obvious that anything should.
- **Positivity of the existing dimension fields**, argued above.
- **Nothing here has been checked against a real exported file** — review §7.3's admission, unchanged.
  The catalogue vocabulary is modelled on SAF, Robot and ETABS *as documented*, not as seen. Status item
  6 — one real ETABS or RFEM export, round-tripped — is the step that tests it, and sections are the
  part of the schema most likely to be found wrong by it.
