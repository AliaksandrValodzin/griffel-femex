# File metadata and unknown members — Implementation Summary

Implemented `Claude/FEMEX_Metadata.md` in full, all six sections as written. Clean build
(0 warnings, 0 errors); **224 tests pass** (was 211).

Two things landed under one version bump, as the plan's "Still open" anticipated. A FEMEX file can now
say what wrote it — the thing every format it targets says on line 1 and it could not say at all. And
**1.4 is the last build that can lose a future field in silence**: from here on, a build reading a file
from a schema it has never heard of keeps the payload it does not understand and says so.

The second half is the failure interop review §4.5 calls *disqualifying* — no error, no warning, a model
that validates and is wrong — and the one `FEMEX_Identity_Summary.md:169` flagged against itself.

## New files

| File | What |
| --- | --- |
| `IExtensible.cs` | `Dictionary<string, JsonElement>? UnknownMembers { get; set; }` — one member, root namespace, beside `IIdentified.cs` |
| `FileMetadata.cs` | Four nullable free-text fields: `Producer`, `ProducerVersion`, `ProjectName`, `CreatedAt` |
| `FemexModel.Unknown.cs` | `EnumerateExtensible()`, `ReportUnknownMembers()`, and the two six-DOF helpers |
| `griffel-femex.Tests/MetadataTests.cs` | 13 facts: the block, the walk, the discriminator, the composing halves, the regression |

## Modified

- **Twenty-five declaration sites** gained `[JsonExtensionData] public Dictionary<string, JsonElement>?
  UnknownMembers` and `: IExtensible` — the **five abstract bases** whose derived types inherit it
  (`Geometry/Element.cs`, `Sections/Section.cs`, `Surfaces/SurfaceProperty.cs`, `Loads/Load.cs`,
  `Grids/Gridline.cs`) and the **twenty standalone** types (`FemexModel`, `FileMetadata`, `Units`,
  `Gravity`, `Grid`, `GridExtent`, `Level`, `Node`, `PlateRegion`, `Material`, `LoadCase`,
  `LoadCombination`, `LoadCombinationTerm`, `Support`, `Restraint`, `Hinge`, `Release`, `FemexMesh`,
  `MeshNode`, `MeshFace`). Mechanical and identical. A null dictionary writes nothing, so **no existing
  file gained a byte**.
- **`FemexModel.cs`** — `FileMetadata? Metadata` declared immediately after `SchemaVersion`;
  `CurrentSchemaVersion` → `"1.4"` with the 1.4 sentence in its doc comment; `ReadableSchemaVersions` →
  four entries. `ToJson()` untouched: it stamps the version and nothing else.
- **`FemexModel.Validation.cs`** — `ReportUnknownMembers()` wired in as a warning after
  `ReportMigrations()`; a `"1.3"` branch in `ValidateSchemaVersion()` (its doc comment's "four cases"
  is now five); and the self-weight version gate rewritten against a new `SelfWeightVersions` matched
  list.
- **`Examples/Example1.femex`** — `schemaVersion` → `"1.4"` and the `metadata` block inserted as the
  second key. Seven lines; nothing else moved.
- **`griffel-femex.Tests/`** — `SampleModels.cs` gained a fully populated `Metadata`; three hard-coded
  `"1.3"` assertions became `"1.4"` (`LoadDirectionTests.cs`, `RoundTripIdentityTests.cs`,
  `SelfWeightTests.cs`), and `ALegacyFile_ReEmitsAs13_WithItsLoadIds` was renamed to `…As14…`.
- **`Claude/FEMEX_Adapters.md` §3.7, §4.5** and **`Claude/FEMEX_Adapters_Plan.md`** — corrected. Both
  asserted item 1 was blocked on the `netstandard2.0;net8.0` retarget. See below.

## The constraint that shaped it, and how it was answered

`JsonSerializerOptions.UnmappedMemberHandling` — what review §4.5 literally asked for — is
System.Text.Json **8.0** API. `griffel-femex.csproj:4` targets `net7.0`, carries no `PackageReference`
at all, and only SDK 7.0.302 is installed. `FEMEX_Adapters.md` §3.7 found this and parked item 1 behind
a retarget.

**`[JsonExtensionData]` took the other route** — no package, no SDK, no csproj change — and is the
better answer regardless, not merely the available one:

| | `Disallow` | `[JsonExtensionData]` |
| --- | --- | --- |
| A 1.4 build reads a 1.5 file | throws; the file is unreadable | reads it, keeps the payload |
| Re-saving it | impossible | member and version both survive |
| What the user is told | an exception | one warning per distinct member name |

**Recorded as a trade, not a dominance.** Extension data preserves *syntax*, not *referential
integrity*: a 1.4 build that reads a 1.5 file, deletes a plate and re-saves leaves an unknown
`"diaphragms"` entry pointing at a plate that no longer exists — a file that is internally inconsistent
and looks authoritative, where `Disallow` would have produced no file at all. Both are losses; only one
is quiet, which is still the right way round, and the `Validate()` warning is the mitigation.
`UnmappedMemberHandling` is now an **option** a future retarget could adopt, not a gap.

**What this does not fix.** Not `FEMEX_Identity_Summary.md:169`'s own case — a 1.3 file read by a **1.2**
build. A 1.2 build is already written; nothing added in 1.4 reaches it. What closes is the loss *class*,
forwards. That is the honest claim and the one worth making.

## The risk, resolved before the work

Fact 8 — *does System.Text.Json 7 route the `"type"` discriminator into extension data?* — was written
first, because `"type"` is on every section, surface property, load and gridline in `Example1.femex` and
every fixture. Had it leaked, `ReportUnknownMembers()` would have fired model-wide and **about
twenty-eight tests would have gone red**: all 22 `Assert.Empty(Validate())` sites,
`Example1_LoadsAndValidates`, `Example1_ReSerializesToItself`, and the five sites asserting exactly one
warning.

**It does not leak.** STJ 7 consumes the discriminator before extension data is populated, on both the
root and polymorphic entities. The `OnDeserialized` fallback the plan held in reserve was not needed and
was not written.

## Two things the design turns on

**`ToJson()` stamps the version and invents nothing else.** Same argument `AssignMissingUids` makes:
auto-stamping a timestamp would mean the same model built twice from the same source produced different
files, and `Example1_ReSerializesToItself` could never hold. `schemaVersion` is stamped because it is a
statement about the *format*, which the library knows; who produced the file and when is a statement
about the *caller*, which it does not.

**One message per distinct member name, not per object** — keyed on the pair *(name, kind)*, for the
reason `ValidateUidCoverage` gives: the fact is about the file, and a future-schema file would otherwise
bury every other message under hundreds of copies of one. Keyed on the pair rather than the name alone
because the message names a kind, and a `"stiffnessModifier"` on both bars and plates has no single kind
to name. A single occurrence is *named* rather than counted — "on Plate 3001" says more than "on 1
plate", and in a hand-editable format it is the commonest case.

## The self-weight regression the bump introduced

`ValidateSelfWeight` gated on `SchemaVersion == "1.2" || == CurrentSchemaVersion`. With the current
version now `"1.4"`, a **1.3** file stopped satisfying it and the "no load case carries self-weight"
warning would have silently stopped firing for it.

The tempting fix — invert it to "any version from 1.2 on" — was **not** taken. It is a version-*ordering*
rule, and `FemexModel.cs:52-59` declines to have one in as many words; and it would quietly change
behaviour for unrecognised versions, since `"2.0"` fails the gate today and would pass an inverted one.
A `SelfWeightVersions` matched list preserves current behaviour exactly. The cost is one more line at
every future bump, which is the price of the no-ordering policy and is paid knowingly.

Fact 12 covers it, and would have caught it.

## Verified

- `dotnet build` — 0 warnings, 0 errors.
- `dotnet test` — **224 passed, 0 failed** (211 + 13). The 22 `Assert.Empty(Validate())` sites and the
  five asserting exactly one warning are all unaffected, which is the discriminator result stated as a
  test result.
- `Example1_ReSerializesToItself` and `Example1_LoadsAndValidates` pass against the edited example —
  byte-identical re-serialization, which is the check that the block is in the right position and
  carries exactly its non-null fields. `producerVersion` is omitted for that reason.
- **The hand check that motivated the whole item.** `Example1.femex` with `"diaphragmId": 3` added to a
  plate and its version set to `"1.5"`, loaded and re-saved:

  ```
  version read back: 1.5
  version re-saved:  1.5 (preserved)
  member re-saved:   diaphragmId: 3 (preserved)

  [Warning] The model declares schemaVersion "1.5", which this build does not recognise; it is read as 1.4.
  [Warning] The file carries a member this build does not know: "diaphragmId", on Plate 3001.
            It is preserved when the model is re-saved, but nothing here reads it.
  ```

  That is the 1.3-read-by-1.2 failure, inverted and now benign — and the two halves composing, since
  `ToJson()` leaves the unrecognised version alone while the payload survives.

  As the plan predicted, the member comes back at the **end** of the plate object rather than where it
  was put: STJ writes extension data after every declared property. The content survives, the byte
  position does not — so `Example1_ReSerializesToItself` is a guarantee about files this build fully
  understands, and must not be read as one about files it does not.

## One thing the plan did not list

The plan named two hard-coded `"1.3"` assertions. There are **three**:
`SelfWeightTests.cs:197` (`ToJson_UpgradesALegacySchemaVersionStamp`) is the same class of assertion and
also failed on the bump. Found by the test run, fixed the same way.

## Still open

Carried from the plan, and all still true of what was built:

- **Preserve-and-reemit is a trade, not a strict win over refusing.** Recorded above rather than left
  implied.
- **`UnmappedMemberHandling` remains unset**, and is now an option a future retarget could adopt.
  Whether FEMEX ever wants a hard refusal in place of preserve-and-warn is a real question, and
  `FEMEX_Adapters.md` §4.5 already asks it.
- **The metadata block is not validated**, matching `Units` — `FemexModel.Validation.cs` deliberately
  does not mention that either. Status §5.9's units work is where a presence-and-value rule for both
  should land.
- **Whether `Producer` should ever become required.** `ValidateNameKeys` took the half-step of warning
  rather than requiring; the same half-step is available here and is deliberately not taken, because it
  would fire on every hand-authored file including this repo's own example.
- **Nothing here has been checked against a real exported file**, review §7.3's admission unchanged. The
  four fields are modelled on ETABS' `.e2k` header and SAF's `Model` worksheet as documented, not as
  seen.
- **The two halves did land as one commit, not two.** The plan offered the split and the risk that
  justified it — fact 8 — resolved cleanly before any of the twenty-five declarations were written, so
  the bisect argument lost its force.
