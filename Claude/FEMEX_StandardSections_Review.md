# Review of `FEMEX_StandardSections.md` — what was checked, and what was corrected

> This is the review that produced the current revision of `Claude/FEMEX_StandardSections.md`.
> Every correction listed here has already been applied to that document; this file records what
> was wrong and why, so the plan does not have to carry its own errata.

**Verdict: the design is sound and should be built as written.** All eight decisions stand, and
decisions 1, 2 and 8 are the strongest parts of the document. What the review found was not a
design problem but a specification problem — three defects that would have surfaced as a failing
build or failing tests during implementation, two wrong numbers that would have been copied into
tests and into `Example2.femex`, and eleven smaller inaccuracies.

---

## 1. What was verified as correct

Checked against the working tree, not assumed. Listed so the corrections below are not mistaken
for distrust of the whole document.

| Claim | Verdict |
| --- | --- |
| `FemexModel.cs:54` `CurrentSchemaVersion = "1.4"` | ✔ |
| `FemexModel.cs:56-63` the no-version-ordering policy | ✔ (the metadata summary's cite of `:52-59` is the stale one) |
| `FemexModel.cs:64` `ReadableSchemaVersions` | ✔ private, `{ "1.1","1.2","1.3", CurrentSchemaVersion }` |
| `FemexModel.cs:150` `DefaultIgnoreCondition = WhenWritingNull` | ✔ |
| `FemexModel.SelfWeight.cs:153` `section.CalculateArea()` | ✔ and it is the **only** call site in the library |
| `FemexModel.Validation.cs:73` section duplicate ids | ✔ |
| `FemexModel.Validation.cs:185/:187` `ValidateNameKeys` | ✔ |
| `FemexModel.Validation.cs:425` bar → section referent | ✔ |
| `FemexModel.Validation.cs:823` `SelfWeightVersions` matched list | ✔ gated at `:881-883` by `Array.IndexOf(...) >= 0` |
| `FemexModel.Unknown.cs:61` the sections walk | ✔ tuple is `(IExtensible, string Owner, string Kind)` |
| `FemexModel.Identity.cs:81` | ✔ yields only `IIdentified`, so value blocks leave it untouched |
| `SampleModels.cs:135-140` three sections | ✔ `Rectangle`, `Circle`, `TSection` |
| Baseline of **224** | ✔ exactly — 224 `[Fact]`, 0 `[Theory]`, 0 `[InlineData]` |
| `csproj` has zero packages and zero embedded resources | ✔ |
| `JsonStringEnumConverter` already registered | ✔ `FemexModel.cs:152`, so `Manufacture` needs no converter |
| `SurfaceProperty`'s reserved discriminators are prose in an XML doc | ✔ `SurfaceProperty.cs:15-17` |
| The nested value-block walk has precedent | ✔ `FemexModel.Unknown.cs:101-115`, support restraints |

Two claims deserve singling out because they were checked empirically rather than reasoned about:

- **The key-order claim is true.** `Example1.femex:774-788` shows `"id"` and `"name"` trailing
  `"width"`/`"depth"` and `"flangeWidth"`, so base-declared `catalogue` and `properties` will
  indeed be written after the derived type's dimensions.
- **The "only behavioural change" claim survives.** `SelfWeightTests.cs:427` calls
  `section.CalculateArea()` directly against Example1. Because Example1's sections carry no
  `properties`, `GetArea()` falls back and that assertion still holds.

The baseline was also run, not just counted: `Passed! - Failed: 0, Passed: 224`, clean build.

---

## 2. The three blocking defects

### 2.1 W1 fired on every well-formed `generic` section

`GenericSection.CalculateArea()` returns `0.0` by decision 4. W1 compared a stated area against
`CalculateArea()` with a 10% band and was **not scoped**, so a generic section stating
`area = 5.381e-3` — the exact case the 1.5 escape hatch exists to serve — read as a 100%
disagreement and tripped W1 on every correct file.

The document already contradicted itself: Verification step 5 expected a generic section with only
`iz` stripped to produce *"exactly one Warning"*, which is only true if W1 stays silent.

**Applied:** W1 scoped to sections that have dimensions, in the same sentence and for the same
reason W2 is scoped to `generic`. The two now partition the space — W1 is *geometry and stiffness
disagree*, W2 is *no geometry, and the stiffness is incomplete* — with the reasoning stated so the
scoping is not later "simplified" away. A fifteenth 1.5 fact was added to lock it: a generic
section with `area`, `iy` and `iz` yields no messages at all.

### 2.2 One `ValidateSections` could not yield both errors and warnings

`Validate()` (`FemexModel.Validation.cs:21-58`) wraps severity **per method at the call site**:
`foreach (var m in ValidateBars(ctx)) yield return ValidationMessage.Error(m);`. A single method
yielding bare strings cannot produce E1/E2 as errors and W1–W3 as warnings.

**Applied:** two methods — `ValidateSections()` in the error block and
`ValidateSectionCompleteness()` in the warning block — following the `ValidateGrids` (`:31`) /
`ValidateGridGeometry` (`:54`) precedent. Neither needs the `ValidationContext`, so both take the
no-argument form `ValidateNameKeys()` uses.

### 2.3 `Example2.femex` would never have reached the test output directory

`griffel-femex.Tests/griffel-femex.Tests.csproj:30-33` copies the example with an **explicit
per-file** entry, not a glob, and every example test resolves
`Path.Combine(AppContext.BaseDirectory, "Examples", ...)`:

```xml
<None Include="..\Examples\Example1.femex" Link="Examples\Example1.femex"
      CopyToOutputDirectory="PreserveNewest" />
```

Without a second line, `Example2_LoadsAndValidates` and `Example2_ReSerializesToItself` fail with
`FileNotFoundException`. The test csproj appeared nowhere in the plan.

**Applied:** added to Critical files and to ordered task 2.

---

## 3. The two wrong numbers

### 3.1 The IPE300 parametric area

`2(0.150)(0.0107) + 0.0071(0.300 − 2(0.0107))` = **5.188e-3 m²**, not the 5.14e-3 stated. The gap
to the tabulated 5.381e-3 is therefore **3.6%**, not "about 4%".

This mattered because Verification step 4 made 5.14e-3 a hand-check assertion — a test written to
the document as it stood would have failed. **Applied** in decision 2 and in Verification step 4.

### 3.2 The torsion constant in the Resulting-JSON block

IPE300 `It` = 20.12 cm⁴ = **2.012e-7 m⁴**. The block read `"j": 2.01e-8` — off by a factor of ten.
`Iy` (8.356e-5) and `Iz` (6.038e-6) were both correct, which is exactly what would have made the
wrong one easy to copy in good faith into `Example2.femex` and the test fixture. **Applied.**

---

## 4. The smaller corrections

| # | What was wrong | Applied |
| --- | --- | --- |
| 1 | *"the existing `AssertReports` / `AssertWarns` helpers"* — there is no test base class; five files each declare their own private statics (`ValidationTests.cs:13,18`, `RoundTripIdentityTests.cs:544,549,554`, `MetadataTests.cs:291`, `SelfWeightTests.cs:200`, and `LoadDirectionTests.cs:24` naming its error overload `AssertReportsError`) | Tests preamble now says `SectionTests.cs` declares its own pair |
| 2 | Fact counts understated — the 1.5 list was twelve *plus* two, the 1.6 list was called thirteen but enumerated fourteen | 15 and 14; total **~253**, not ~249 |
| 3 | Verification step 2 named one Example1 silence test; there are two — `ValidationTests.cs:735` and `RoundTripIdentityTests.cs:481`, both calling `Assert.Empty(model.Validate())` | Both named |
| 4 | W2's wording (*"no `iy` or `iz`"*) read as *both* missing, but the test list and Verification step 5 both wanted *either* to fire | Reworded to name the missing one |
| 5 | E2 said "not a positive quantity" without saying whether zero is rejected | States that it is, and distinguishes the case from the zero-width `Rectangle` argued elsewhere |
| 6 | *"matching the ones §4.1, §4.2, §4.3 and §4.6 already carry"* — `FEMEX_Interop_Review.md` has exactly three such blockquotes: §4.1 `:376`, §4.3 `:449`, §4.6 `:563`. **§4.2 carries none**, and two of the three read *Closed by*, not *Addressed by* | Corrected to three, with the majority wording |
| 7 | *"both name the change that would fix it"* — `FEMEX_Adapters.md` §4.2 names the escape hatch precisely; `FEMEX_Adapters_Plan.md` §4 says only *"until review §4.4 lands"* | Claim softened, each described accurately |
| 8 | Two adjacent stale facts in files the pass already opens: `FEMEX_Adapters.md:500` still says version `"1.3"` at `FemexModel.cs:50,60`; the status note's §0 and §2.2 still describe the pre-1.4 world | Both added to task 4 |

---

## 5. Three design points now stated explicitly

**The escape hatch does not protect older FEMEX builds.** The plan's promise — *"falls back to
stiffness if it does not [have the library]"* — holds for a third-party adapter reading the JSON.
It does not hold for an older `griffel-femex` build: System.Text.Json throws on an unrecognised
polymorphic discriminator, so a 1.4 build handed `"type": "ishape"` fails to deserialize and never
reaches the `properties` it could have degraded to. `ReadableSchemaVersions` refuses that file
first, so nothing is broken — but the claim now says "adapter" where it meant one.

**`Example2.femex` has to be authored to validate silently.** If its test asserts zero messages the
way Example1's does, the file is more constrained than "six nodes, three bars, an S355 material"
suggests: `ValidateNameKeys` warns on any blank or duplicated name across sections, materials and
load cases; `ValidateSelfWeight` (`:874-890`) warns *"No load case carries self-weight"* for any 1.6
file with bars and a non-zero density unless some case carries a non-zero `SelfWeightFactor`; and
its own sections must satisfy W1, W2 and W3. Now stated as a requirement rather than discovered.

**`FEMEX.md` joins the correction list.** It is the format spec of record and documents the three
things this change touches — the section union literally (`:95-102`), the `CalculateArea` decision
(`:143-149`) and the running schema version. Precedent was split:
`FEMEX_LoadCombinations.md:377`, `FEMEX_SelfWeight.md:567` and `FEMEX_Identity.md:260` each
scheduled a `FEMEX.md` blockquote as an explicit task; the 1.4 metadata pass did not, and
`FEMEX.md` consequently stops at 1.3 with no mention of `FileMetadata` or `IExtensible`. The plan
now follows the majority and closes the 1.4 gap in the same edit, rather than letting a second
version open behind it.

---

## 6. One trap found outside the plan

**`dotnet test` at the repo root runs zero tests and says "Build succeeded".** `griffel-femex.sln`
contains only the library — the test project is not in it — so the bare command in the plan's
Verification block would have reported *0 Warning(s), 0 Error(s)* without executing a single fact.
The Verification block now names the project explicitly:

```powershell
dotnet test griffel-femex.Tests\griffel-femex.Tests.csproj
```

This is not a defect of the sections plan — it would mislead any verification pass in this
repository — but it is worth recording where the next person will look.

---

## Still open after this review

- **Nothing here has been checked against a real exported file.** The review verified the plan
  against the *repository*; it did not and could not verify the catalogue vocabulary against
  Robot, ETABS, RFEM or SAF output. Status item 6 remains the step that tests that, and the plan's
  own "Still open" entry saying so is correct and stands.
- **Whether `griffel-femex.Tests.csproj` should switch to a glob** over `..\Examples\*.femex`
  rather than gaining one line per example. Out of scope here; noted because Example3 will hit it.
- **Whether the test project belongs in `griffel-femex.sln`.** Adding it would make the bare
  `dotnet test` correct instead of silently empty. A repository question, not a sections one.
