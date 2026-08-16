# Self-weight — Implementation Summary

Implemented `Claude/FEMEX_SelfWeight.md` in full. Clean build (0 warnings, 0 errors);
**179 tests pass** (was 136). Schema version `1.1` → `1.2`.

The gap review §4.3 named — *"FEMEX has no self-weight"*, whose failure mode is a silent wrong answer
rather than a missing feature — is closed. A FEMEX file now states, in one place, which way gravity acts
and how strong it is; each material states its mass; and each load case states whether, and by how much,
self-weight is applied to it. The absence of self-weight is written down (`"selfWeightFactor": 0`) rather
than left silent, and both halves of the §4.3 failure are warnings.

## New files

| File | What |
| --- | --- |
| `Gravity.cs` | `Dx`, `Dy`, `Dz` (default `0, 0, −1`) and `Acceleration` (default `9.80665`) — at the root beside `Units.cs`, following that precedent |
| `FemexModel.SelfWeight.cs` | the 1.1 migration on `IJsonOnDeserialized`, the extracted `GetEffectiveProperties`, and the five public helpers |
| `griffel-femex.Tests/SelfWeightTests.cs` | 30 facts |

## Modified

- **`FemexModel.cs`** — `public Gravity Gravity { get; set; } = new Gravity();` immediately after
  `Units`, so it is the third key in the file. Non-nullable with an initializer, unlike `Units?`:
  gravity is *consumed*, by the migration and by three helpers, and a nullable one would force every
  consumer to invent the default at the point of use. `CurrentSchemaVersion` → `"1.2"`; a private
  `ReadableSchemaVersions = { "1.1", CurrentSchemaVersion }`, documented as a matched list and **not**
  a comparison rule; `ToJson()` restamps rather than merely filling in, and its "one deliberate
  mutation" paragraph is rewritten rather than quietly broken.
- **`Materials/Material.cs`** — `UnitWeight` → `Density` (ρ, mass per unit volume), the `//` comments
  upgraded to XML docs carrying γ = ρ·g and the unit rule, and the convenience constructor's parameter
  renamed with the factor-of-g break stated in its own doc. Plus a **set-only** `[JsonPropertyName("unitWeight")]`
  binder: System.Text.Json uses the setter on read and skips a getter-less property entirely on write,
  so the key can never be re-emitted. `[EditorBrowsable(Never)]` rather than `[Obsolete]`, since
  `Obsolete` produces CS0618 at use sites and the repo's standing bar is 0 warnings.
- **`Loads/LoadCase.cs`** — `public double SelfWeightFactor { get; set; }`, a constructor overload
  taking it, and the one-line class doc upgraded to house style.
- **`FemexModel.Validation.cs`** — `ValidateGravity` (Error, registered straight after
  `ValidateDuplicateIds`) and `ValidateSelfWeight` (Warning, last in the trailing warning block);
  `ValidateSchemaVersion` reshaped around the readable-versions list; `ValidatePlates` rewired to the
  extracted `GetEffectiveProperties`; `FormatNodeList` → `FormatNumberList`, now with two callers.
- **`Geometry/Plate.cs`**, **`Geometry/PlateRegion.cs`** — one sentence each pointing at
  `GetEffectiveProperties` as the executable form of the inheritance rule. No behaviour.
- **`Examples/Example1.femex`** — regenerated through `ToJson()`; see below.
- **`griffel-femex.Tests/SampleModels.cs`** — material 1 `Density = 2.5`, load case 1
  `SelfWeightFactor = 1.0`. That is the whole fixture change; the three combinations already factor
  case 1.
- **`griffel-femex.Tests/ValidationTests.cs`**, **`LoadDirectionTests.cs`** — a
  `// ----- Self weight -----` group of 13 facts, the example counts, and three literal edits.
- **`Claude/FEMEX.md`** (root and Loads), **`Claude/FEMEX_Interop_Review.md`** §4.3 — cross-references.

## The six decisions, as built

**1. Gravity is a property of the model's frame.** Direction and strength are stated once, on the root;
a case carries a dimensionless participation factor. This does not reopen the argument
`Loads/LoadDirection.cs` makes when it refuses a `Gravity` value: `LoadDirection` gains no member, no
load can name gravity, and `SelfWeightFactor` is dimensionless with no second convention to be read
against. RFEM's per-case `fx/fy/fz` is exactly what was *not* copied — it lets two cases in one model
disagree about which way down is.

**2. `Material.UnitWeight` → `Material.Density`.** ρ, mass per unit volume; γ = ρ·g. Mass is the value
that cannot be derived, and it is what a dynamic model would need later. With kN and m the mass unit is
the **tonne**, so concrete is 2.5, not 2500 — documented on `Density` and on `Gravity.Acceleration`
rather than policed, since a `Units.Mass` field would be a unit *system* (review §5.9's own ranked item).

**3. A load case says whether self-weight applies.** Non-nullable, so written on every case in every
file — the same call `IncludeInDesignEnvelope` made.

**4. No reserved case, no reserved nature.** More than one case carrying the factor is a warning, not an
error: legal in ETABS and RFEM, and exactly the double count §4.3 describes.

**5. The arithmetic is executable, and stops at per-element intensity.** Total model self-weight is
deliberately not built — it needs the overlapping priority regions resolved into non-overlapping areas,
and there is no polygon-boolean code in the repo. The per-element intensities are its input.

**6. `schemaVersion` → `"1.2"`, and `ToJson()` restamps what it has migrated.** The minimum honest
version policy is a **list of readable versions**, not an ordering rule invented for versions that do
not exist yet.

## The helpers

```csharp
Vector3d GetGravityDirection();                    // unit; Vector3d.Zero if degenerate
double   GetWeightDensity(int materialId);         // ρ·g; 0.0 for an unknown material

bool TryGetBarSelfWeightPerLength(int barId, out Vector3d forcePerLength);
bool TryGetPlateSelfWeightPerArea(int plateId, out Vector3d forcePerArea);
bool TryGetPlateSelfWeightPerArea(int plateId, int? regionId, out Vector3d forcePerArea);

IEnumerable<LoadCase> GetSelfWeightCases();        // SelfWeightFactor != 0, in list order
```

A lookup-only partial mirroring `FemexModel.LocalAxes.cs`: nothing changes the model, and an
unresolvable reference is answered with `false` rather than an exception.

**They return a `Vector3d`, not a scalar.** For a plate a scalar would be *wrong*, not merely
incomplete: a plate's natural scalar axis is its normal, and self-weight is not along it. The sample's
wall stands in the y = 0 plane, so its normal is horizontal and its weight vertical — a scalar plus an
implied axis would report a wall's self-weight as a lateral pressure.
`PlateSelfWeightPerArea_OnAWall_ActsDownward_NotAlongTheNormal` pins it, asserting both that
`w · normal == 0` and that `w` is `(0, 0, −γt)`. `‖forcePerArea‖` recovers the scalar for anyone who
wants it.

**Unfactored.** The result is the weight itself, at gravity 1.0; a load case applies its own factor.
Multiplying inside the helper would make it answer a different question depending on which case asked.

**An `Opening` or a `LoadOnly` area weighs nothing** — `true` with `Vector3d.Zero`, a definite answer
rather than a refusal, so a caller summing regions need not branch. `false` is reserved for genuinely
unanswerable: an unknown id, or a governing surface property or material that does not resolve.

**The inheritance rule, extracted.** `GetEffectiveProperties(Plate, PlateRegion?) → (Kind,
SurfacePropertyId, MaterialId)` states `region.X ?? plate.X` once, and `ValidatePlates` is rewired to
call it — so validation and the self-weight helpers cannot drift apart, the same argument that made
`TryGetNewellNormal` shared between the planarity check and the local-axis convention.

## Migration from 1.1

The conversion needs the root's `Gravity.Acceleration`, so it cannot live on `Material`. It hangs off
**`IJsonOnDeserialized`**, explicitly implemented, so it runs from every deserialization entry point —
including a bare `JsonSerializer.Deserialize<FemexModel>` a consumer writes for itself — and cannot
depend on key order. `LegacyUnitWeight_UsesTheModelsOwnGravity` proves that by putting `"gravity"`
*after* `"materials"` in the literal JSON.

For each material with a pending legacy γ: `Density = γ / Gravity.Acceleration`, and the id is recorded.
A material stating **both** spellings keeps its density and is recorded separately. A non-positive
acceleration leaves the density at 0 rather than producing an infinity; `ValidateGravity` reports that
acceleration as an error in its own right.

**The conversion preserves weight density exactly, in any unit system.** ρ = γ/g on load and γ = ρ·g in
`GetWeightDensity` read the same acceleration, so a 1.1 file's γ returns to floating-point equality
whatever units it was written in — the migration never has to know what those were.
`LegacyUnitWeight_SurvivesAsTheSameWeightDensity` asserts `GetWeightDensity(1) == 25.0` to 1e-12.

The two records are **private fields**, which System.Text.Json never serializes — no `[JsonIgnore]`
needed and no key can leak. Two consequences, both correct: the record does not survive
`ToJson() → FromJson()` (the re-emitted file *is* 1.2 and carries a density, so it must not warn again —
the warning is a property of the read), and a model built in memory never migrates and never warns.

## Validation

Two errors, on the one block in FEMEX whose numbers are checked at all. The exception to "validate no
numeric field" is argued: the gravity block is *entirely* numeric, so refusing to check its numbers
leaves it with no checks, and neither failure produces a visibly wrong number — each **deletes a load**,
which is §4.3's exact failure mode.

```
Gravity has dx, dy and dz all zero, which is no direction at all.
Gravity has a non-positive acceleration (0). Which way gravity acts is dx/dy/dz's job; the
acceleration is only how strong it is.
```

Six warnings:

| # | When | Message |
|---|---|---|
| W1 | more than one case with a non-zero factor | `Load cases 1 and 6 both carry self-weight; the structure's own weight is applied once in each of them, and any combination naming more than one counts it twice.` |
| W2 | non-zero factor on a case whose nature is not `Dead` | `Load case 3 carries self-weight but its nature is Wind; the structure's own weight is a dead action.` |
| W3 | the model has elements and used materials with mass, and no case carries self-weight | `No load case carries self-weight: every selfWeightFactor is zero, so the structure's own weight is nowhere in this model and a receiving program will not add it.` |
| W4 | a material used by a bar or plate has density 0 while self-weight is active | `Material 2 has a density of zero, so every bar and plate made of it weighs nothing in the self-weight case.` |
| W5 | migration record | `Material 1 was written as a unit weight and has been read as a density of 2.54929 through the model's gravity (9.80665). Re-saving the model writes the density.` |
| W6 | both spellings present | `Material 1 carries both a unitWeight and a density; the density is used and the unit weight ignored.` |

W1 is the *double-counted* half of §4.3 made visible; W3 is the *silently dropped* half. W3 is scoped so
it cannot nag a model with nothing to weigh — the model needs bars or plates **and** a material with
non-zero density that something is actually made of — and is skipped when `SchemaVersion !=
CurrentSchemaVersion`, because the version warning already says "no load case in it carries any" and the
repo never reports one fact twice. W4 is gated on self-weight being active, so a model that never uses
density is not nagged about it. W1 reuses `FormatNumberList`.

`ValidateSchemaVersion` now has three branches rather than two, so `"1.1"` is *recognised and migrated*
rather than *unrecognised*:

```
The model declares schemaVersion "1.1", written before self-weight existed, so no load case in it
carries any, and each material's unit weight has been read as a density through the model's gravity.
Re-saving it writes the current format.
```

## `Examples/Example1.femex`

Regenerated, not hand-edited: loaded, mutated in the object graph, saved through `ToJson()`.

- `"schemaVersion": "1.2"` first; `"gravity"` third, after `"units"`; `"selfWeightFactor"` beside
  `"nature"`; `"unitWeight"` nowhere in the output; `ToJson()` idempotent on the result.
- Both materials `"unitWeight": 25` → `"density": 2.5`. **Authored, not migrated:** 2.5 t/m³ is what an
  engineer writes for concrete, and the honest consequence is that γ becomes 2.5 × 9.80665 = 24.517
  kN/m³, down from the 25 the file previously implied. Stating mass rather than weight makes that
  arithmetic visible, which is the change working as intended. The exact reversibility of the γ/g
  migration is demonstrated by a 1.1 test fixture instead, where it belongs.
- The file gains **load case 6, `"Dead - self weight"`, nature `Dead`, factor `1.0`** — not a factor on
  case 1, whose 1.5 kPa SDL and 6 kN/m cladding genuinely *are* superimposed. Folding it in would make
  the file assert that the 1.5 kPa includes the slab's own weight, and would silently change what all
  eight combinations mean without touching them.
- Each of the eight combinations gained one term `{ "loadCaseNumber": 6, "factor": f }` with `f` copied
  from its case-1 term — 101→1.35, 102→1.2, 103→1.2, 104→0.9, 105→1.2, 106→1.2, 201→1.0, 202→1.0 — so
  "G" keeps meaning total dead load. Self-weight in a case no combination factors never reaches a
  result, which is the second half of §4.3's silent wrong answer.
- Case 6 has **zero entries in the `loads` array and contributes the largest load in the model**. That
  is a genuinely surprising property of the new field, and the example shows it.
  `LoadDirectionTests`' `Assert.Equal(64, gravity.Count)` is unchanged and now carries a comment saying
  why: self-weight is not a `Load`.

## One edit the plan did not anticipate

`LoadDirectionTests.Dxyz_AreOmitted_WhenDirectionIsNotVector` asserted `DoesNotContain("\"dx\"")` over
the **whole document**. The root gravity block spells its direction with the same three component names
— deliberately, so "a direction as three components" is said one way in the format — so the assertion
had to be scoped to the `"loads"` array. The fact's intent is unchanged and the scoping is commented.

## Verification

1. `dotnet build` — 0 warnings, 0 errors.
2. `dotnet test` — 179 pass, 0 fail.
3. `Assert.Empty(SampleModels.Build().Validate())` and
   `Assert.Empty(FemexModel.Load("Examples/Example1.femex").Validate())` — no errors **and** no
   warnings, W3 included. W3 was *designed* to fire on both fixtures until they carried self-weight;
   that is the forcing function, and it is what would have caught the gap
   `Claude/FEMEX_LoadCombinations.md` noted in passing and could not fix.
4. **Reversibility, as an assertion:** a 1.1 fixture with `"unitWeight": 25` gives
   `GetWeightDensity(1) == 25.0` to 1e-12.
5. **The arithmetic, by hand and then as a fact:** a 300×500 concrete column at ρ = 2.5 t/m³ and
   g = 9.80665 m/s² weighs 0.15 m² × 24.517 kN/m³ = **3.67749375 kN/m**, acting `(0, 0, −1)`.
   `TryGetBarSelfWeightPerLength` agrees.
6. `Load → ToJson → Load` on the migrated example round-trips, and `ToJson()` is idempotent.

## Risks, as they landed

- **R1 — set-only property serialization.** It behaves as asserted: System.Text.Json binds through the
  setter on read and skips the getter-less property on write.
  `LegacyUnitWeight_IsNeverWrittenBack` pins it, and the `double?` + `WhenWritingNull` fallback was not
  needed.
- **R2 — `Material(…, double density, …)` is a silent factor-of-g break** for any external positional
  caller. Unavoidable; the version bump and the constructor's XML doc are the only signals. Exactly one
  call site in this repo (`SampleModels.cs`).
- **R3 — W3 fires on both fixtures by design.** Tasks 6, 7 and 8 landed together.
- **R4 — key order.** `IJsonOnDeserialized` removes the dependence entirely; one test proves it.
- **R5 — the default 9.80665 is metre-specific**, so a millimetre model that accepts it is 1000× light.
  Documented on `Gravity.Acceleration`, not validated. The highest-consequence residual in the change.

## Still out of scope

Total model self-weight (needs polygon booleans over the priority regions); mesh-face self-weight
(`MeshFace`'s property fields are a mesher cache, not authority); mass source and load-to-mass
conversion (review §6); material completeness (§5.5); units as enums and a mass unit (§5.9); producer
and project metadata (§4.5); per-element mass overrides and self-weight scoped to a subset of elements.
