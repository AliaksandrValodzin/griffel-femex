# File metadata — who wrote this file, and what it says that this build cannot read

> **Step 0 (repo convention):** this document sits alongside `Claude/FEMEX.md`, `Claude/FEMEX_Plates.md`,
> `Claude/FEMEX_Node_Sharing.md`, `Claude/FEMEX_Gridlines.md`, `Claude/FEMEX_LoadCombinations.md`,
> `Claude/FEMEX_BarLocalAxes_LoadDirection.md`, `Claude/FEMEX_SelfWeight.md` and
> `Claude/FEMEX_Identity.md`; `Claude/FEMEX_Metadata_Summary.md` will record what was actually built.

## Context

`Claude/FEMEX_Interop_Status_16082026.md` §2.2 records interop review §4.5 as **half done**.
`schemaVersion` landed with the load-direction change and works — `ToJson()` stamps it,
`ValidateSchemaVersion()` warns on a null or unrecognised one, `Examples/Example1.femex` opens with
`"schemaVersion": "1.3"`. Two halves did not land:

1. **No producer, producing version, project name or timestamp at the root.** The precedent exists one
   level down — `FemexMesh.Generator` / `FemexMesh.GeneratedAt` (`Mesh/FemexMesh.cs:14,17`) — but the
   root has neither. ETABS stamps `PROGRAM "ETABS" VERSION "21.0.0"` on line 1 of every `.e2k`; SAF
   devotes a whole `Model` worksheet to project and model specifications. A FEMEX file today cannot say
   what wrote it.
2. **Unknown JSON members are dropped in silence.** `FemexModel.cs:110-120` sets camelCase, indenting,
   ignore-nulls and the enum converter, and nothing else. This is the failure review §4.5 calls
   *disqualifying* — no error, no warning, a model that validates and is wrong — and the one
   `FEMEX_Identity_Summary.md:169` flags against itself: a 1.3 file read by a 1.2 build loses its uids
   without a word. `FEMEX_Adapters.md` §4 makes it a whole loss category, *Stale*.

This is item **1** in the status note's recommended order (size XS), placed there because it unblocks the
next breaking change the way `schemaVersion` unblocked load direction, and because it stops silent field
loss between builds.

### The constraint that shapes the fix

`JsonSerializerOptions.UnmappedMemberHandling` — what review §4.5 literally asked for — is
**System.Text.Json 8.0** API. `griffel-femex.csproj:4` targets `net7.0` and carries **no
`PackageReference` at all**, and only SDK **7.0.302** is installed, so retargeting to `net8.0` is not
possible without an SDK install. `FEMEX_Adapters.md` §3.7 already found this and parked item 1 behind a
`netstandard2.0;net8.0` retarget.

**This document takes the other route: `[JsonExtensionData]`, which works on the current toolchain with
no package, no SDK and no csproj change** — and is the better answer regardless, not merely the available
one. `Disallow` converts silent loss into a hard read failure, so a 1.4 build could not read a 1.5 file at
all. Extension data *preserves* the unknown members and writes them back, and `Validate()` reports them,
so the loss stops being silent without the file becoming unreadable.

**What this does and does not fix.** It does not rescue `FEMEX_Identity_Summary.md:169`'s own case — a
1.3 file read by a **1.2** build. A 1.2 build is already written; nothing added in 1.4 reaches it. What
this closes is the loss *class*, forwards: **1.4 is the last build that can lose a future field in
silence.** From 1.4 on, a build reading a file from a schema it has never heard of keeps the payload it
does not understand and says so. That is the honest claim, and it is the one worth making — the Identity
summary's instance is the illustration, not the thing being repaired.

The two halves compose. `ToJson()` already leaves an unrecognised `SchemaVersion` alone
(`FemexModel.cs:141`, *"it was not migrated, so it is not ours to restate"*), so an older build
round-tripping a newer file now preserves the version **and** the payload it does not understand.

---

## The change

### 1. `FileMetadata.cs` — new, root namespace, beside `Units.cs` and `Gravity.cs`

```csharp
public class FileMetadata
{
    public string? Producer { get; set; }         // "griffel-etabs"
    public string? ProducerVersion { get; set; }  // "0.1.0"
    public string? ProjectName { get; set; }
    public string? CreatedAt { get; set; }        // ISO-8601 as free text

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? UnknownMembers { get; set; }
}
```

Four nullable free-text fields, deliberately mirroring `FemexMesh.Generator` / `GeneratedAt`, whose doc
comment already sets the convention: *"ISO-8601 timestamp as free text, in keeping with Units being free
text."* Not `DateTimeOffset?` — System.Text.Json would reformat it on write, and consistency with the
spelling already one level down is worth more than a type nothing in the library computes with.

### 2. `FemexModel.Metadata`

`public FileMetadata? Metadata { get; set; }`, declared **immediately after `SchemaVersion`** in
`FemexModel.cs`, so it is the second key in the file, ahead of `Units` and `Gravity`. Nullable with no
initializer, like `Units` and unlike `Gravity` — the distinction `FemexModel.cs:65-76` already draws:
gravity is *consumed*, by the 1.1 migration and the self-weight helpers, and this is pure annotation.

**`ToJson()` does not stamp it.** Same argument `AssignMissingUids` makes at `FemexModel.Identity.cs:32-37`:
auto-stamping a timestamp would mean the same model built twice from the same source produced different
files, and `Example1_ReSerializesToItself` (`griffel-femex.Tests/RoundTripIdentityTests.cs:505`) could
never hold. `SchemaVersion` is stamped because it is a statement about the *format*, which the library
knows; who produced the file and when is a statement about the *caller*, which it does not.

**No validation of the block in this pass.** `FemexModel.Validation.cs` deliberately does not mention
`Units` either — annotation that nothing interprets. Everything the validator warns about is something
that makes a receiver get a model *wrong*: a name it must invent, a weight applied twice, a partly-stamped
uid coverage. A missing producer does not change how one number in the file is read. Recorded as a
residual instead, alongside status §5.9's units work, which is where a presence-and-value rule for both
belongs.

### 3. `IExtensible.cs` — new — and `[JsonExtensionData]` across the serializable types

```csharp
public interface IExtensible
{
    Dictionary<string, JsonElement>? UnknownMembers { get; set; }
}
```

The interface is the enumeration handle only; `[JsonExtensionData]` must sit on the concrete property, so
each class declares:

```csharp
[JsonExtensionData]
public Dictionary<string, JsonElement>? UnknownMembers { get; set; }
```

Declared on the **five abstract bases**, which their derived types inherit — `Geometry/Element.cs`
(→ `Bar`, `Plate`), `Geometry/Sections/Section.cs` (→ `Rectangle`, `Circle`, `TSection`),
`Geometry/Surfaces/SurfaceProperty.cs` (→ `ConstantThickness`), `Loads/Load.cs` (→ `PointLoad`,
`LinearLoad`, `AreaLoad`, `TemperatureLoad`, via `DistributedLoad`) and `Geometry/Grids/Gridline.cs`
(→ `OrthogonalGridline`, `FreeGridline`) — plus the **twenty standalone** types: `FemexModel`,
`FileMetadata`, `Units`, `Gravity`, `Grid`, `GridExtent`, `Level`, `Node`, `PlateRegion`, `Material`,
`LoadCase`, `LoadCombination`, `LoadCombinationTerm`, `Support`, `Restraint`, `Hinge`, `Release`,
`FemexMesh`, `MeshNode`, `MeshFace`.

Twenty-five declarations, mechanical and identical. A null dictionary writes nothing, so no existing file
gains a byte.

### 4. `FemexModel.Unknown.cs` — new — the walk and the report

Two members, following `FemexModel.Identity.cs`'s discipline exactly:

- **`EnumerateExtensible()`** — the single statement of what can carry unknown members, yielding
  `(IExtensible Entity, string Owner, string Kind)` with the wording the validation messages already use
  ("Material 3", "Plate 4 region 2"). Broader than `EnumerateIdentified()`, which covers only the 13
  uid-bearing families: this also reaches `Units`, `Gravity`, `Restraint`, `Release`, `Gridline`,
  `GridExtent`, `LoadCombinationTerm`, the mesh and the root itself.
- **`ReportUnknownMembers()`** — **one message per distinct member name**, not per object, for the reason
  `ValidateUidCoverage` gives at `FemexModel.Validation.cs:144-151`: the fact is about the file, and a
  future-schema file would otherwise bury every other message under hundreds of copies.

  > `The file carries a member this build does not know: "endOffset", on 142 bars. It is preserved when the model is re-saved, but nothing here reads it.`

  Keyed on **(member name, kind)**, not on the name alone: the message names a kind, and a
  `"stiffnessModifier"` appearing on both bars and plates has no single kind to name. Still one message
  per key rather than per object, which is the property `ValidateUidCoverage` is actually after.

Wired into `Validate()` beside the other file-level checks, as a warning after `ReportMigrations()`
(`FemexModel.Validation.cs:43`).

### 5. Schema version → 1.4

In `FemexModel.cs`: `CurrentSchemaVersion = "1.4"`, its doc comment gains the 1.4 sentence, and
`ReadableSchemaVersions` becomes `{ "1.1", "1.2", "1.3", CurrentSchemaVersion }`.
`ValidateSchemaVersion()` gains a `"1.3"` branch in the existing shape — written before file metadata
existed, so it does not say what produced it or when. Its doc comment at `FemexModel.Validation.cs:730`
says *"The four cases are the four answers `ReadableSchemaVersions` allows"*; there are now five.

**Two tests hardcode the current version and must move with it.** Both assert the literal `"1.3"`
rather than `CurrentSchemaVersion`, so both fail on the bump:

- `griffel-femex.Tests/LoadDirectionTests.cs:146` — `SchemaVersion_IsTheFirstKey`.
- `griffel-femex.Tests/RoundTripIdentityTests.cs:419` — `ALegacyFile_ReEmitsAs13_WithItsLoadIds`,
  whose *name* also encodes 1.3.

Everything else that touches the version tracks it automatically: `SampleModels.cs:58` sets
`SchemaVersion = FemexModel.CurrentSchemaVersion` deliberately, which is why the 22
`Assert.Empty(Validate())` sites are safe from the bump.

**A regression the bump introduces, and its fix.** `ValidateSelfWeight` at
`FemexModel.Validation.cs:860-862` reads:

```csharp
bool versionHasSelfWeight =
    string.Equals(SchemaVersion, "1.2", StringComparison.Ordinal) ||
    string.Equals(SchemaVersion, CurrentSchemaVersion, StringComparison.Ordinal);
```

With `CurrentSchemaVersion` now `"1.4"`, a **1.3** file stops satisfying this and the "no load case
carries self-weight" warning silently stops firing for it.

The tempting fix is to invert it — `SchemaVersion is not null && != "1.1"`, "any version from 1.2 on".
**Don't**, for two reasons. It is a version-*ordering* rule, and `FemexModel.cs:52-59` declines to have
one in as many words: *"A matched list and deliberately **not** a comparison rule: FEMEX has no ordering
policy over versions, and inventing one here would be inventing behaviour for versions that do not exist
yet."* And it quietly changes behaviour for unrecognised versions — today `"2.0"` fails the gate and
draws no self-weight warning; inverted, it passes and does.

Use the matched-list form, which is the shape the file already committed to and preserves current
behaviour exactly:

```csharp
private static readonly string[] SelfWeightVersions = { "1.2", "1.3", CurrentSchemaVersion };
...
bool versionHasSelfWeight =
    SchemaVersion is not null &&
    Array.IndexOf(SelfWeightVersions, SchemaVersion) >= 0;
```

The cost is one more line to touch at every future bump, which is the price of the no-ordering policy and
is paid knowingly. `MigrateLegacyLoadIds` (`FemexModel.Identity.cs:132-137`) is already a matched list —
an early return on anything that is not null, `"1.1"` or `"1.2"` — and needs no change.

### 6. `Examples/Example1.femex`

Bump to `"1.4"` and insert the block as the second key, matching the mesh's existing honesty about being
hand-authored (`"generator": "hand-authored (migrated from the pre-panel schema)"`):

```json
{
  "schemaVersion": "1.4",
  "metadata": {
    "producer": "hand-authored",
    "projectName": "FEMEX example 1",
    "createdAt": "2026-08-05T00:00:00Z"
  },
  "units": { ... },
```

`producerVersion` omitted — with `DefaultIgnoreCondition = WhenWritingNull` the file must contain exactly
the non-null fields, in declaration order, or `Example1_ReSerializesToItself` fails. `createdAt` matches
the mesh's existing `generatedAt` rather than inventing a second date for one file.

`SampleModels.Build()` (`griffel-femex.Tests/SampleModels.cs`) gains the same block so the suite exercises
a populated one.

---

## Critical files

- `FemexModel.cs:39,50,60,110-120,139` — `SchemaVersion`, `CurrentSchemaVersion`, `ReadableSchemaVersions`,
  `CreateJsonOptions`, and the `ToJson` stamping rule that the unrecognised-version case already gets right.
- `FemexModel.Validation.cs:42-45,144-151,739,860-862` — where file-level checks are wired, the
  one-message-model-wide precedent, `ValidateSchemaVersion`, and the self-weight version gate to fix.
- `FemexModel.Identity.cs:32-37,70-115` — the no-auto-stamp argument, and `EnumerateIdentified()` as the
  shape `EnumerateExtensible()` copies.
- `Mesh/FemexMesh.cs:14,17` — the `Generator` / `GeneratedAt` precedent this promotes to the root.
- `Units.cs`, `Gravity.cs` — the two existing root annotation blocks, and the nullable-versus-initialized
  distinction.
- `Materials/Material.cs:47` — `unitWeight` is a *declared* property, so the 1.1 migration is untouched by
  extension data.
- `FemexModel.SelfWeight.cs:27,49` — `IJsonOnDeserialized`, already a post-read pass over the whole
  document, and the fallback hook if the risk below materialises.
- `griffel-femex.csproj:4` — the `net7.0` target and the absent `PackageReference` behind the whole
  choice of approach.
- `griffel-femex.Tests/LoadDirectionTests.cs:146`, `griffel-femex.Tests/RoundTripIdentityTests.cs:419` —
  the two hardcoded `"1.3"` assertions the bump breaks; and `griffel-femex.Tests/SampleModels.cs:58`,
  which tracks `CurrentSchemaVersion` and is why nothing else does.

---

## Tests — `griffel-femex.Tests/MetadataTests.cs`

Against the 211-fact baseline; roughly a dozen new facts.

**Metadata**

1. All four fields round-trip.
2. `metadata` is the second key, immediately after `schemaVersion`.
3. A model with no metadata omits the key entirely.
4. `ToJson()` invents nothing — the same model built twice yields byte-identical JSON.
5. A 1.3 file loads clean with `Metadata` null and draws the new version warning.

**Unknown members**

6. An unknown member at the **root** survives a round trip.
7. An unknown member on an entity in a **base-typed polymorphic list** — a section, a load — survives.
   This is the `JsonPolymorphic` × `JsonExtensionData` interaction and the main technical risk.
8. **The `"type"` discriminator does not leak into extension data** — read a normal file, assert every
   section's `UnknownMembers` is null.
9. `Validate()` warns once per distinct member name, with the count and the kind.
10. The Identity summary's failure, inverted: a file carrying a member this build does not know, on a
    bar, is preserved and re-emitted rather than dropped. (Not the summary's *own* scenario — that one
    is a 1.2 build, which this change cannot reach. See "What this does and does not fix" above.)
11. An unrecognised `schemaVersion` is still left alone by `ToJson()` while unknown members survive — the
    two halves composing.

**Regression**

12. A 1.3 file with something to weigh and no self-weight case still draws the warning — §5's fix.

---

## Risk, and the fallback

The one thing that could bite is fact **8**: if System.Text.Json 7 routes the `"type"` discriminator into
extension data on a polymorphic type, every section, surface property, load and gridline would round-trip
a spurious `"type"` key. **Write fact 8 first.**

(Not *element*: `FemexModel.cs:88-89` exposes `List<Bar>` and `List<Plate>`, concrete rather than
`List<Element>`, so `Geometry/Element.cs:9`'s `[JsonPolymorphic]` is dormant and `"type": "bar"` appears
in no file this library writes. The four that are serialized through a base really do emit it.)

**If it leaks, about twenty-eight tests go red, not one.** `"type"` is on every section, surface
property, load and gridline in `Examples/Example1.femex` and in every `SampleModels.Build()` fixture, so
`ReportUnknownMembers()` would fire model-wide: all 22 `Assert.Empty(Validate())` sites,
`Example1_LoadsAndValidates` (`ValidationTests.cs:744`), `Example1_ReSerializesToItself`, and the five
sites asserting an exact warning count of one (`ValidationTests.cs:289,299,429,524,544`). That is not an
argument against the approach — it is why fact 8 is written before the twenty-five declarations rather
than after, and why the fallback below is load-bearing rather than tidying.

If it leaks, the fallback is to strip the discriminator name in `IJsonOnDeserialized.OnDeserialized`
(`FemexModel.SelfWeight.cs:49`), which already runs a post-read pass over the whole document — and
`EnumerateExtensible()` is exactly the walk that needs.

Nothing else here is load-bearing: the metadata block is additive and optional, and extension data on a
type with no unknown members writes zero bytes.

---

## Documents to correct

Two existing documents assert this item is **blocked** on the retarget, and are now wrong. Correct them
rather than leaving them to mislead the next pass:

- `Claude/FEMEX_Adapters.md:407,480` — *"on `net7.0` the setting §4.5 turns on **cannot be written**… The
  status note's item 1 sits behind the same retarget."*
- `Claude/FEMEX_Adapters_Plan.md:169,205,455` — the same claim.

Both are accurate about `UnmappedMemberHandling` **specifically** and wrong about item 1 being gated on
it. The retarget question stands on its own merits — reach into `net48` hosts — and loses its schema
prerequisite.

---

## Verification

```powershell
dotnet build
dotnet test
```

1. `dotnet test` green, at 211 plus the new facts. Watch specifically the **22 sites asserting
   `Validate()` is empty**, and the five asserting exactly one warning
   (`ValidationTests.cs:289,299,429,524,544`): all 27 should be unaffected. Every JSON key in
   `Example1.femex` and in the eight inline test fixtures maps to a declared property — `unitWeight` is
   real at `Materials/Material.cs:47`, and the only other non-property key is the `"type"` discriminator,
   which is fact 8's subject. If one turns red, either its JSON has a stray key that was being silently
   dropped — which is the change working — or the discriminator leaked.

   A second, narrower reason a previously-clean file might start reporting: `PropertyNameCaseInsensitive`
   is left at its `false` default, so a casing slip like `"UnitWeight"` matches nothing today and is
   dropped in silence. After this change it lands in extension data and warns. Also the change working,
   but worth recognising rather than debugging.
2. `Example1_ReSerializesToItself` and `Example1_LoadsAndValidates` pass against the edited example.
   Byte-identical re-serialization is the check that the block is in the right position and carries
   exactly the non-null fields.
3. Open `Examples/Example1.femex` and confirm `schemaVersion` then `metadata` are the first two keys.
4. Hand-check the round trip that motivated the whole item: take `Example1.femex`, add a
   `"diaphragmId": 3` to a plate and set its version to `"1.5"`, load and re-save, and confirm the member
   and the version both survive and that `Validate()` reports the unknown member once. That is the
   1.3-read-by-1.2 failure, inverted and now benign.

   Expect the member to come back at the **end** of the plate object, not where it was put: STJ writes
   extension data after every declared property. The content survives, the byte position does not — so
   `Example1_ReSerializesToItself` is a guarantee about files this build fully understands, and must not
   be read as one about files it does not.

---

## Still open

- **Preserve-and-reemit is a trade, not a strict win over refusing.** The case against it, which §"The
  constraint" does not make: a 1.4 build reads a 1.5 file carrying an unknown root-level `"diaphragms"`
  array, deletes a plate, and re-saves. The diaphragm entry survives untouched, still stamped `"1.5"`,
  now referencing a plate that no longer exists. Extension data preserves *syntax*, not *referential
  integrity* — so it can produce a file that is internally inconsistent and looks authoritative, where
  `Disallow` would have produced no file at all. Both are losses; only one is quiet, which is still the
  right way round, and the `Validate()` warning is the mitigation. But the choice should be recorded as
  a trade rather than a dominance.
- **This is two changes sharing a version bump, and could land as two commits.** The metadata block is
  additive and about an hour; the extension-data half is twenty-five declarations gated on an
  empirically unknown STJ 7 behaviour. Only the metadata block actually requires 1.4 — extension data
  changes no serialized shape. Splitting means the risky half cannot hold up the safe one, and the
  bisect is clean if fact 8 goes badly.
- **`UnmappedMemberHandling` remains unset**, and after this change is an *option* rather than a
  blocker — a stricter posture a future retarget could adopt, not a gap. Whether FEMEX ever wants a hard
  refusal in place of preserve-and-warn is a real question, and `FEMEX_Adapters.md` §4.5 already asks it
  ("whether reporting is sufficient, or whether it must throw").
- **The metadata block is not validated**, matching `Units`. Status §5.9's units work is where a
  presence-and-value rule for both should land.
- **Whether `Producer` should ever become required.** `ValidateNameKeys` took the half-step of warning
  rather than requiring for the four name-keyed families; the same half-step is available here and is
  deliberately not taken yet, because it would fire on every hand-authored file including this repo's own
  example.
- **Nothing here has been checked against a real exported file**, review §7.3's admission unchanged. The
  four fields are modelled on ETABS' `.e2k` header and SAF's `Model` worksheet as documented, not as seen.
