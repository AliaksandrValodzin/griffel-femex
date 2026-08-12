# Load combinations — the factored case list, and the envelope a design reads

> **Step 0 (repo convention):** this document sits alongside `Claude/FEMEX.md`,
> `Claude/FEMEX_Plates.md`, `Claude/FEMEX_Node_Sharing.md` and `Claude/FEMEX_Gridlines.md`;
> `Claude/FEMEX_LoadCombinations_Summary.md` records what was actually built.

## Context

`Claude/FEMEX_Assessment.md` §4 item 1 and `Claude/FEMEX_Interop_Review.md` §2.2 row 1 both name the
same thing as FEMEX's largest single gap: **there are no load combinations.** FEMEX has `LoadCase`
and `Load` and stops. Every one of the five target programs has the concept — Robot
`Cases.CreateCombination` + `CaseFactors`, Revit `LoadCombination`, ETABS `COMBO`, RCB's combination
table, RFEM `loadCombination`, SAF `StructuralLoadCombination` — and none of them can guess it. A
FEMEX file today can describe a building's geometry and loading faithfully and still arrive
un-analysable, with the engineer re-entering every combination by hand: most of the setup work the
transfer was supposed to save.

The change is purely additive. Nothing existing moves, no existing file stops loading, and a
consumer that ignores the new collection reads every model it could read before.

Loads live in a load case; a combination combines load cases. That containment is already the shape
of the format (`Load.LoadCaseNumber` → `LoadCase.Number`) and the new entity extends it by one link
rather than reworking it.

## The five decisions

1. **Terms reference load cases only — the structure is flat.** A `LoadCombinationTerm` is
   `(LoadCaseNumber, Factor)` and nothing else. This is SAF's `StructuralLoadCombination` shape. No
   cycle detection, no resolution pass, no combination-of-combinations. ETABS and Robot do allow a
   combo to factor another combo; a nested *linear-add* combo flattens exactly on import. A nested
   *envelope* combo does **not**: it envelopes a **named subset**, whereas
   `IncludeInDesignEnvelope` envelopes everything flagged within one limit state. Such a
   combination is **lost** — an importer should drop it and report it rather than flatten it into
   something that means less. Accepting that loss is the price of the flat structure; it is the
   same gap the "Multiple named envelopes per limit state" entry under *Deliberately out of scope*
   names from the other side, and it stays out of scope for the same reason.
2. **`Number` + optional `Label`, in the format's own idiom.** `Number` because that is what
   `LoadCase` uses (`LoadCase.Number`, and `Load.LoadCaseNumber` refers to it); `Label` as `string?`
   for the same reason. Combination numbers live in **their own id space**, separate from load-case
   numbers — consistent with every other FEMEX collection and with RFEM's per-type `no`. An exporter
   targeting Robot, where cases and combinations share one number space, remaps; that is the
   exporter's job, not the format's.
3. **`IncludeInDesignEnvelope`, a plain `bool` defaulting to `true`.** Semantics, stated exactly:
   *combinations are enveloped within their own limit state, and this flag gates participation.*
   That gives the flag a definition the format can defend — an envelope mixing ULS and SLS results is
   meaningless — while still allowing a combination that exists for reporting or checking to be kept
   out of design. `FemexModel.GetDesignEnvelope(limitState)` implements the rule so a consumer and
   the format cannot disagree about it, the argument `GetGridsForLevel` already made for grids.
4. **Two enums the terms cannot imply: `LimitState` and `CombinationType`.** Neither is inferable
   from a factor list, and both are carried by all five programs and SAF. Without `LimitState` a
   receiver cannot tell a strength combination from a deflection one; without `CombinationType`
   every combination is assumed to be a linear sum, so ETABS/Robot envelope and SRSS combinations
   degrade to a wrong answer rather than an obviously incomplete one.
5. **No code-generation mode.** There is no "generate combinations per standard" flag and no
   `Standard` string. §4.1 of the review is explicit that the explicit factor form is needed
   regardless, because Robot's code combinations do not round-trip as factor lists. Adding a second,
   non-round-tripping way to say the same thing before anything consumes the first one is cost
   without a reader. Reserved in the XML doc, the way `SurfaceProperty` reserves `"variable"`.

---

## Data model

New folder `Loads/Combinations/`, mirroring `Geometry/Sections/`, `Geometry/Surfaces/` and
`Geometry/Grids/`. Four files, one type each — the repo's one-enum-per-file rule
(`GridDirection.cs`, `PlateBehaviour.cs`).

Folder maps to namespace strictly here (`Geometry/Grids/` → `griffel_femex.Geometry.Grids`), so the
four types land in **`griffel_femex.Loads.Combinations`** and six files gain a `using` for it:
`FemexModel.cs`, `FemexModel.Validation.cs`, the new `FemexModel.LoadCombinations.cs`,
`SampleModels.cs`, `RoundTripTests.cs` and `ValidationTests.cs`.

The subfolder is a choice, not the only reading of the rule: the repo usually puts an enum *beside*
its owner (`Loads/LoadNature.cs`, `Geometry/PlateBehaviour.cs`, `Geometry/PlateRegionKind.cs`) and
only `GridDirection.cs` sits inside a subfolder. Four related types earn their own folder the way
`Geometry/Grids/` did; the alternative — all four flat in `Loads/` — keeps the namespace unchanged
and would be equally defensible.

### `Loads/Combinations/LoadCombination.cs`

| Member | Type | Notes |
| --- | --- | --- |
| `Number` | `int` | its own id space; matches `LoadCase.Number` in kind and naming |
| `Label` | `string?` | `"1.2G + 1.5Q"` — optional, as on `LoadCase` and `Load` |
| `LimitState` | `LimitState` | the envelope this combination belongs to |
| `CombinationType` | `LoadCombinationType` | how the terms are combined; `LinearAdd` is the default |
| `IncludeInDesignEnvelope` | `bool` | initialised `= true` |
| `Terms` | `List<LoadCombinationTerm>` | owned children, initialised `= new()`, like `Grid.Lines` / `Plate.Regions` |

Plus a `(number, label, limitState)` convenience constructor and a parameterless one for
serialization, and `ToString() => $"[{Number}] {Label} ({LimitState})"` — the `LoadCase.cs` shape
line for line.

The class XML doc carries the three rules a consumer can otherwise get wrong: **terms repeating a
load case add** (ETABS behaviour, and what `GetTotalFactor` implements); **the envelope rule** from
decision 3; and — the one this entity makes easy to conflate — that the two members with "envelope"
in their meaning are unrelated. `CombinationType.Envelope` says how *this combination's own
load-case terms* combine. `IncludeInDesignEnvelope` says whether this combination takes part in the
per-limit-state design envelope. A combination can be `LinearAdd` and in the envelope, or
`Envelope` and out of it. Task 1 is marked medium-risk for exactly this sentence.

### `Loads/Combinations/LoadCombinationTerm.cs`

| Member | Type | Notes |
| --- | --- | --- |
| `LoadCaseNumber` | `int` | references `LoadCase.Number` — the same comment `Load.cs:17` carries |
| `Factor` | `double` | dimensionless |

Deliberately not an `Element` and deliberately not id-bearing: a term is a value, owned by exactly
one combination, addressed only by its position. Same call as `PlateRegion`'s within-plate id, one
step further.

### `Loads/Combinations/LimitState.cs`

```
enum LimitState { Unspecified, Ultimate, Serviceability, Accidental }
```

Spelled out rather than `Uls`/`Sls`/`Als`: enum values serialize **PascalCase as declared** (the
repo's `JsonStringEnumConverter` has no naming policy, so `"nature": "Dead"`), and `"limitState":
"Uls"` reads as neither the acronym nor a word. `Ultimate`/`Serviceability` is Revit's own vocabulary
and standard AS/NZS 1170 language; the ULS/SLS/ALS mapping goes in the XML doc. `Unspecified` is
first so `default(LimitState)` is the honest answer for a combination whose source did not say.

### `Loads/Combinations/LoadCombinationType.cs`

```
enum LoadCombinationType { LinearAdd, Envelope, AbsoluteAdd, Srss }
```

`LinearAdd` first so it is the default. The four cover ETABS' set, Revit's `Combination | Envelope`
and RFEM's; RFEM's further variants are beyond an essential subset. Named `LoadCombinationType` and
exposed as the property `CombinationType`, **not** `Type` — `"type"` is FEMEX's polymorphic
discriminator key on `Load`, `Element`, `Section`, `SurfaceProperty` and `Gridline`, and a
`"type"` that means something else on a non-polymorphic entity is a trap for anyone reading the JSON.
The C# stutter is the cheaper cost.

### Modified: `FemexModel.cs`

One property in the Loads block, after `Loads`, keeping the file's referenced-before-referencer
order (`LoadCases` → `Loads` → `LoadCombinations`):

```
List<LoadCase> LoadCases
List<Load> Loads
List<LoadCombination> LoadCombinations = new List<LoadCombination>();
```

Nothing else in `FemexModel.cs` changes; `JsonOptions` already handles camelCase names and string
enums.

---

## Resulting JSON

```json
"loadCombinations": [
  {
    "number": 101,
    "label": "1.2G \u002B 1.5Q",
    "limitState": "Ultimate",
    "combinationType": "LinearAdd",
    "includeInDesignEnvelope": true,
    "terms": [
      { "loadCaseNumber": 1, "factor": 1.2 },
      { "loadCaseNumber": 2, "factor": 1.5 }
    ]
  },
  {
    "number": 202,
    "label": "G \u002B Q unfactored (reporting only)",
    "limitState": "Serviceability",
    "combinationType": "LinearAdd",
    "includeInDesignEnvelope": false,
    "terms": [
      { "loadCaseNumber": 1, "factor": 1 },
      { "loadCaseNumber": 2, "factor": 1 }
    ]
  }
]
```

Note the labels. `System.Text.Json`'s default encoder escapes `+` as `\u002B`, so a label
written `"1.2G + 1.5Q"` in C# lands in the file as shown above — `Example1.femex` already carries
`"label": "Wind \u002BX"` for the same reason. It round-trips exactly and no consumer that parses
JSON sees a difference, but anyone hand-authoring the `Examples/Example1.femex` block below, or
asserting on a label substring, has to write the escape rather than the `+`.

`includeInDesignEnvelope` is written on every combination rather than suppressed when true:
`DefaultIgnoreCondition = WhenWritingNull` suppresses nulls only, and the format is explicit
elsewhere by choice — `Example1.femex` writes `"fy": 0, "fz": 0` on point loads. A nullable
"null means true" would be smaller JSON and a worse contract.

---

## `FemexModel.LoadCombinations.cs` — a small partial

New partial at the repo root beside `FemexModel.Grids.cs` and `FemexModel.Nodes.cs`, carrying its
own `/// <summary>` on the `public partial class FemexModel` declaration as those two do.

The half of the `FemexModel.Grids.cs` failure-mode rule that applies is **lookups return `null`**.
The other half — authoring helpers throw `InvalidOperationException` with an `/// <exception>` tag
— has nothing to bite on here: all four members are lookups, and none of them adds anything to the
model. There is no `GetOrAdd…` counterpart, by decision 5.

`GetDesignEnvelope` is **eager**, building a list rather than using `yield`, matching
`GetGridsForLevel` (deviation 5 in `FEMEX_Gridlines_Summary.md`).

| Member | Notes |
| --- | --- |
| `IEnumerable<LoadCombination> GetDesignEnvelope(LimitState limitState)` | **the point of the flag** — combinations of that limit state with `IncludeInDesignEnvelope` set, in list order |
| `LoadCombination? FindLoadCombination(int number)` | |
| `double GetTotalFactor(LoadCombination combination, int loadCaseNumber)` | sums repeated terms; `0.0` when the case is absent — the repeats-add rule made executable. Taking the entity rather than a number is an **open decision**, below |
| `int NextLoadCombinationNumber()` | one past the highest in use, 1 for an empty model; matches `NextNodeNumber()` / `NextGridId()` |

Four methods is the whole surface. No combination *builder*, no code generator, no resolver that
walks loads — decision 5, and the reasoning that kept `MergeCoincidentNodes()` out of node sharing.

---

## `Validate()` additions

**Two validator methods, not one.** A validator returns bare `string`s and `Validate()`
(`FemexModel.Validation.cs:20-39`) decides the severity at the **call site**:

```csharp
foreach (var message in ValidateLoads(ctx))          yield return ValidationMessage.Error(message);
...
foreach (var message in ValidateGridGeometry(ctx))   yield return ValidationMessage.Warning(message);
```

So a single method cannot emit both severities. Grids already split for exactly this reason —
`ValidateGrids` (errors, registered second) and `ValidateGridGeometry` (warnings, registered last).
Combinations follow:

| Method | Severity | Registers |
| --- | --- | --- |
| `ValidateLoadCombinations` | Error | immediately after `ValidateLoads`, matching data order |
| `ValidateLoadCombinationUsage` | Warning | in the trailing warning block, beside `ValidateCoincidentNodes` / `ValidateGridGeometry` |

Two `foreach` lines in `Validate()`, then. One further line joins `ValidateDuplicateIds`;
`ValidationContext` needs **no new field** — the only cross-reference is to `ctx.LoadCaseNumbers`,
which it already builds (`FemexModel.Validation.cs:939`).

`…Usage` rather than `…Labels` because the second method carries both warnings — the repeated load
case as well as the duplicate label — and both are about a combination that is legal FEMEX a
receiver will still get wrong.

**Errors** — a combination that cannot be evaluated:

| Check | Message |
| --- | --- |
| duplicate combination number | `Duplicate load combination number {n}.` (via the existing `ReportDuplicates`) |
| no terms | `Load combination {n} has no terms.` |
| unknown load case | `Load combination {n} references unknown load case {c}.` |

Phrasing follows the established `"<Owner> <id> references unknown <thing> <id>."` convention
(`ValidateLoads` at `:382`, `ValidateBars` at `:267`).

**Warnings** — evaluable, but a receiver will probably get it wrong:

| Check | Message |
| --- | --- |
| a load case repeated in one combination | `Load combination {n} includes load case {c} more than once; the factors add.` |
| a non-null label used by more than one combination | `More than one load combination is labelled "{x}". A program that keys combinations by name cannot tell them apart.` |

The repeat is a **warning, not an error**, deliberately breaking with the `"{owner} repeats node
{id}."` error used for node lists: repeating a case with two factors is legal in ETABS and sums,
whereas a repeated node in a contour is meaningless. The label warning is half the format's answer
to §4.6 — Robot, ETABS and SAF key combinations by *name*, so a duplicate label is a real collision
on export even though FEMEX itself references by number. The other half, which §4.6 actually asks
for, is a *required* name; see *Open decisions*.

The label warning is **one message per duplicated label**, not one per colliding pair — three
combinations sharing a label produce one message, not three. That is the `seen`/`reported`
double-`HashSet` idiom `ReportDuplicates` (`:76-85`) already uses, and it matches how grids report
the same problem: `Grid 1 has more than one line labelled "A".` The label is the message's subject,
so no combination number appears in it; a reader finds them by searching the label.

Per the never-double-report convention, **both** methods — the errors as well as the warnings —
skip a combination whose number was already reported as duplicate. `ValidateGrids` sets the
precedent for applying the guard in an error validator (`:110-118`: *"A repeated grid id is one
grid as far as its contents go, and is already reported as an error in its own right."*).

---

## `Examples/Example1.femex`

The reference file gains a `loadCombinations` block after `loads`, over its four existing cases
(1 Dead-superimposed, 2 Live-office, 3 Wind +X, 4 Temperature-roof) — a representative AS/NZS 1170.0
set: five Ultimate (`1.35G`; `1.2G + 1.5Q`; `1.2G + Wu + 0.4Q`; `0.9G + Wu`; `1.2G + 1.5Q + 1.0T`),
one Serviceability (`G + 0.4Q`), and one Serviceability combination with
`"includeInDesignEnvelope": false` so the file demonstrates the flag doing something rather than
being true seven times.

Numbering: `101…105` for Ultimate, `201…202` for Serviceability, following the file's existing
banded convention (nodes `101…420` by level, plates `3001…3004` and `4100…4403`, mesh faces
`3100…3411` — a hundred-band per level).

Two things to get right when authoring the block. Labels take the `+` escape, per the note
above. And `1.2G + 1.5Q + 1.0T` is the loosest of the seven against AS/NZS 1170.0, which pairs an
other-action with a ψc-factored `Q` rather than the full `1.5` — it is there to give load case 4 a
combination at all, so either relabel it or drop the "representative" claim to "illustrative".

Note in passing, not fixed here: the file has no self-weight case, because FEMEX has no self-weight
(review §4.3). The combinations factor superimposed dead load only. That is the next gap, not this
one.

---

## Tests

**`SampleModels.cs`** — the fixture already has cases 1 (Dead) and 2 (Thermal). Add three
combinations and `public const int` numbers beside `BarId` / `SlabId`:

| # | Const | Label | Shape |
| --- | --- | --- | --- |
| 101 | `UltimateCombinationNumber` | `"U1"` | Ultimate, LinearAdd, `1 × 1.2` + `2 × 1.0` |
| 102 | `EnvelopeCombinationNumber` | `"U2"` | Ultimate, **Envelope**, `1 × 1.35` |
| 201 | `ExcludedCombinationNumber` | `"S1"` | Serviceability, `IncludeInDesignEnvelope = false`, `1 × 1.0` |

Three combinations cover both limit states, a non-default `CombinationType`, and both states of the
flag — so every new test starts from `SampleModels.Build()` and mutates one thing, as the existing
ones do.

The labels are **distinct and non-null on purpose**, in the fixture's own short style (`P1`, `L1`,
`A1`, `T1`). The sample has to raise no message at all —
`RoundTripTests.SampleModel_IsValid` asserts `Assert.Empty(model.Validate())`, and `Validate()`
returns warnings as well as errors — so it must not trip the new duplicate-label warning, and no
`+` in a label means no escape to reason about in the round-trip assertions.

**`LoadCombinationTests.cs`** (new, xUnit `[Fact]`, `Verb_Condition` names) —
`GetDesignEnvelope_ReturnsCombinationsOfThatLimitStateOnly`,
`GetDesignEnvelope_SkipsExcludedCombinations`,
`GetDesignEnvelope_IsEmptyForALimitStateNothingUses`,
`GetTotalFactor_SumsRepeatedTerms`, `GetTotalFactor_IsZeroForAnAbsentCase`,
`FindLoadCombination_ReturnsNullForAnUnknownNumber`,
`NextLoadCombinationNumber_IsOnePastTheHighest`, `NextLoadCombinationNumber_IsOneForAnEmptyModel`.

**`RoundTripTests.cs`** — `LoadCombination_RoundTrips` with typed assertions (term count, factors,
`IncludeInDesignEnvelope` false survives), and two literal substrings added to
`ToJson_IsCamelCase_AndHasDiscriminators`: `"\"limitState\": \"Ultimate\""` and
`"\"combinationType\": \"Envelope\""` — the second pinning both the string-enum serialization and
the deliberate non-use of the `"type"` key.

**`ValidationTests.cs`** — a `// ----- Load combinations -----` group: one `Reports_*` per error
message, one `Warns_*` per warning, `RepeatedLoadCase_IsAWarning_NotAnError` (the decision made
visible), and `Accepts_TwoCombinationsWithNullLabels` guarding the near miss.

**`ValidationTests.Example1_LoadsAndValidates`** — add `Assert.Equal(7, model.LoadCombinations.Count)`
beside the existing hard counts (20 plates, 44 mesh faces, 8 area loads), and assert
`GetDesignEnvelope(LimitState.Serviceability)` returns exactly one of the file's two SLS
combinations.

## Migration and compatibility

- **No existing file breaks.** A missing `loadCombinations` deserializes to the initialized empty
  list. `Example1.femex` would load unchanged even without the edit above; the edit is to give the
  format a worked reference, not to keep it valid.
- Every re-serialized model gains `"loadCombinations": []` — the same visible change
  `surfaceProperties` and `grids` each made. No test asserts a whole-document string.
- No `schemaVersion` is needed for this change specifically (review §4.5 item 1). Unlike load
  direction, adding combinations changes the meaning of no existing field, so it does not have to
  wait behind the version block.

## Ordered tasks

| # | Task | Risk |
| --- | --- | --- |
| 1 | `Loads/Combinations/` — the four types, XML docs written **before** the bodies | **medium — semantics.** The envelope rule and the repeats-add rule are the contract; cheap now, expensive later |
| 2 | `FemexModel.cs`: `LoadCombinations` after `Loads` | low |
| 3 | `FemexModel.LoadCombinations.cs` — the four helpers | low |
| 4 | `ValidateLoadCombinations` (errors, after `ValidateLoads`) **and** `ValidateLoadCombinationUsage` (warnings, in the trailing warning block) — two `foreach` lines in `Validate()` — plus one line in `ValidateDuplicateIds` | low |
| 5 | `SampleModels.Build()` — three combinations and their consts | low |
| 6 | `LoadCombinationTests.cs`, plus the `RoundTripTests` / `ValidationTests` additions | low |
| 7 | `Examples/Example1.femex` — the seven-combination block; re-run `Example1_LoadsAndValidates` | low |
| 8 | `Claude/FEMEX_LoadCombinations_Summary.md`; `> **Extended by …**` blockquotes into `FEMEX.md` and `FEMEX_Interop_Review.md` §4.1 rather than editing them | low |

## Open decisions

Two questions this plan currently answers by default rather than by argument. Both are cheap to
settle now and awkward to change once anything reads the format.

1. **Should `Label` be required?** The plan keeps `string?`, matching `LoadCase.Label`, and adds
   `Accepts_TwoCombinationsWithNullLabels` — which blesses a combination that cannot be exported by
   name at all. But §4.6 of `FEMEX_Interop_Review.md` recommends a **required** name precisely on
   the entities other programs key by name, and Robot, ETABS and SAF all key combinations by name.
   The duplicate-label warning is half the answer; the other half is either making `Label`
   non-nullable or adding a third warning, `Load combination {n} has no label; a program that keys
   combinations by name will invent one.` The cheap option is the third warning: it keeps the
   `LoadCase` symmetry and still tells an exporter the truth.
2. **Should `GetTotalFactor` take a number rather than the entity?** Every other public helper is
   id-based — `GetGridsForLevel(int)`, `FindGrid(int)`, `FindGridline(int, string)`,
   `GetOrAddNodeAtGrid(int, string, string, int)` — and `GetTotalFactor(LoadCombination, int)` never
   touches `this`, so it reads as a static that happens to live on the model.
   `GetTotalFactor(int combinationNumber, int loadCaseNumber)` returning `0.0` for an unknown
   combination would match the idiom and give the "lookups return `null`/nothing" rule something to
   apply to. The cost is that "unknown combination" and "case absent from a known combination"
   become the same answer.

## Deliberately out of scope

- **Nested combinations.** Decision 1 — including the nested *envelope* case, which is genuinely
  lost rather than re-expressed. If ever needed it is a target-kind field on the term plus
  cycle detection, additively.
- **Code-generated combinations** — a `Standard` string or a "generate per code" mode. Decision 5;
  reserved in the XML doc only. `LoadNature` already carries what a code engine would key on, and
  the review's own "Still open" notes that a sub-nature would be needed first.
- **Result combinations** (RFEM `resultCombination`, combinations of *results* rather than loads) —
  a solver concept, and §6 of the review already rules solver settings out.
- **Numeric sanity checks on `Factor`** — NaN, infinity, a factor of zero. The repo validates no
  numeric field anywhere today, and starting with this one would be inconsistent rather than
  thorough.
- **Multiple named envelopes** per limit state (strength vs deflection vs crack width). One flag,
  one envelope per limit state. This is the same limit decision 1 states from the other side: an
  imported envelope combination naming a subset has nowhere to land. A `DesignEnvelope` entity
  remains available later without changing anything built here.
- **Design output** — which combination governs, utilisation, enveloped results. FEMEX carries no
  results and should not start.

## Verification

1. `dotnet build` — 0 warnings, 0 errors.
2. `dotnet test` — the existing 85 facts pass unchanged, plus the new ones.
3. `Assert.Empty(SampleModels.Build().Validate())` — the sample, combinations included, is clean.
4. Eyeball the emitted sample JSON against the shape above: `"loadCombinations"` after `"loads"`,
   camelCase keys, `"limitState": "Ultimate"` and `"combinationType": "LinearAdd"` as PascalCase
   strings, `"includeInDesignEnvelope"` present on every combination, no `"type"` key anywhere in
   the block, and every `+` in a label written as `\u002B` (expected, not a bug).
5. End-to-end gate: load `Examples/Example1.femex`, call
   `GetDesignEnvelope(LimitState.Ultimate)` and confirm it returns the five ULS combinations and
   neither SLS one; call `GetTotalFactor` for load case 1 on `1.2G + 1.5Q + 1.0T` and get `1.2`.
   One call exercises the flag, the limit-state partition and the term resolution together.
