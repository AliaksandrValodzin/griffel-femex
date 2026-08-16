# Self-weight — gravity as a property of the model, mass as a property of the material

> **Step 0 (repo convention):** this document sits alongside `Claude/FEMEX.md`, `Claude/FEMEX_Plates.md`,
> `Claude/FEMEX_Node_Sharing.md`, `Claude/FEMEX_Gridlines.md`, `Claude/FEMEX_LoadCombinations.md` and
> `Claude/FEMEX_BarLocalAxes_LoadDirection.md`; `Claude/FEMEX_SelfWeight_Summary.md` records what was
> actually built.

## Context

`Claude/FEMEX_Assessment.md` §4 item 3 and `Claude/FEMEX_Interop_Review.md` §2.2 row 4 / §4.3 name the
same P0 gap: **FEMEX has no self-weight.** `Materials/Material.cs:17` carries `UnitWeight` — "weight per
unit volume (γ)" — and nothing anywhere states whether gravity is applied, in which direction, with what
factor, or into which load case.

Review §4.3 puts the consequence plainly. The failure mode is not a missing feature, it is a **silent
wrong answer**: a model round-tripped through FEMEX either loses its self-weight entirely or gains it
twice, and nothing in the file reveals which. Robot's `I_LRT_DEAD`, Revit's built-in density-driven
weight, ETABS' `LOADPATTERN … SELFWEIGHT 1`, RCB's automatic weight and RFEM's per-case `self_weight`
flag would each produce a different answer on import of the same file.

**Is a load case reserved for self-weight today? No — and nothing else is either.** `LoadNature.Dead`
exists but has no special meaning anywhere in the code; the only validation that touches a load case is
a duplicate-number check (`FemexModel.Validation.cs:63`) and a referenced-case-exists check (`:389`).
`Examples/Example1.femex` case 1 is deliberately labelled `"Dead - superimposed"`, and
`Claude/FEMEX_LoadCombinations.md` says so outright:

> the file has no self-weight case, because FEMEX has no self-weight (review §4.3). The combinations
> factor superimposed dead load only. That is the next gap, not this one.

This is that gap. `Claude/FEMEX_BarLocalAxes_LoadDirection.md:262` names it too, as "the other half of
'where does gravity come from'".

The outcome: a FEMEX file states, in one place, which way gravity acts and how strong it is; each
material states its mass; and each load case states whether — and by how much — self-weight is applied
to it. Nothing is inferred, and the absence of self-weight is written down rather than left silent.

---

## The six decisions

### 1. Gravity is a property of the model's frame, not a direction an author may choose

A new root `Gravity` block carries a direction (`dx`, `dy`, `dz`, defaulting to `0, 0, −1`) and an
`acceleration` (defaulting to `9.80665`, in model units). It sits immediately after `Units`: it is the
same class of statement as "Z is up", which the previous change wrote into `Claude/FEMEX.md`.

This does **not** reopen the argument `Loads/LoadDirection.cs` makes when it refuses a `Gravity` value —
*"it would give the format two ways to say 'down' and make the sign of a magnitude depend on which one
the author picked."* Five reasons:

1. **Nothing gains a second spelling.** `LoadDirection` gains no member; no `AreaLoad`, `LinearLoad` or
   `PointLoad` can name gravity; no distributed load's meaning changes, and every load in every existing
   file means exactly what it meant before. The rejected `LoadDirection.Gravity` was an alternative
   spelling of a field that already existed. `Gravity` is a field for a quantity that did not.
2. **The objection was about a magnitude's sign, and there is no magnitude here.**
   `Direction = Z, magnitude = −6` and `Direction = Gravity, magnitude = +6` are two spellings of one
   load whose sign flips between them. `SelfWeightFactor` is dimensionless and multiplies a quantity the
   model computes for itself (ρ·g·A); `1.0` is normal gravity, `0` is none, and there is no second
   convention it could be read against. The one place a sign could hide — which way — is read from
   exactly one place in the file, and `GetGravityDirection()` is that place made executable.
3. **Self-weight had no other spelling at all**, per review §4.3. This is not a second way to say
   something; it is the only way to say a thing the format could not say.
4. **Putting the vector on the root is itself the anti-duplication decision.** RFEM carries per-load-case
   `fx, fy, fz` self-weight factors, so two cases in one model can disagree about which way down is and a
   receiver has no rule for choosing. FEMEX splits it deliberately: **direction and strength once, on the
   root; a dimensionless participation factor per case.** A per-case direction is what *would* reopen the
   objection, and it is exactly what this change declines to copy.
5. **The residual overlap, named honestly.** A file can still describe the same physical loading twice —
   once as `selfWeightFactor: 1.0`, once as an author-computed area load pointing down. That overlap is
   inherent to any format that has self-weight; all five target programs have it, and it is the *original*
   §4.3 double-count risk rather than a new one. The format's answer is the pair of warnings W1/W3 below,
   plus one sentence stated once in `LoadCase.SelfWeightFactor`'s XML doc: **a case's own loads are
   additional to its self-weight, never a substitute for it.**

Making the direction explicit is also what defuses RFEM's Z-down default, which review §1.5 calls "the
single most common source of translator bugs against any Z-up format": a translator from RFEM writes
`(0, 0, −1)` into FEMEX's Z-up frame, and the trap is stated rather than inherited.

### 2. `Material.UnitWeight` becomes `Material.Density`

ρ, mass per unit volume; weight density γ = ρ·g. Revit's `Density`, SAF's `Unit mass [kg/m3]`, ETABS'
mass-per-volume and RFEM's density are all **mass**; Robot's `RO` is weight and is the outlier review
§5.5 flags — *"the distinction is a factor of g"*, and *"nothing in the format says so"*.

Mass is the value that cannot be derived — weight follows from it given g, not the other way round — and
it is what a dynamic model would need later. Choosing it closes the units trap in the direction that
matters and lets the format say `g` out loud.

### 3. A load case says whether self-weight applies, with a scalar factor

`LoadCase.SelfWeightFactor`, a non-nullable `double` defaulting to `0`. `1.0` is normal gravity along
the model gravity vector. ETABS' `SELFWEIGHT 1` per-pattern multiplier is exactly this shape; SAF's
`Load type = Self weight` and Robot's `I_DRV_COEFF` carry the same information.

Non-nullable, and therefore **written on every load case in every file**, deliberately:
`"selfWeightFactor": 0` is the statement "no self-weight here", which is precisely what §4.3 says the
format cannot make today. A nullable field omitted from the JSON would re-create the silence the change
exists to remove — the same call `includeInDesignEnvelope` made, where "a nullable 'null means true'
would be smaller JSON and a worse contract".

### 4. No reserved load case and no reserved nature

FEMEX does not mint a self-weight case, and `LoadNature.Dead` stays a plain category with no special
meaning. Any case may carry the factor. More than one carrying it is a **warning**, not an error: it is
legal in ETABS and RFEM, and it is exactly the double-count §4.3 describes — legal FEMEX that a receiver
will probably get wrong, which is what the repo reserves warnings for.

### 5. The arithmetic is executable, and stops at per-element intensity

`FemexModel.SelfWeight.cs` states γ = ρ·g, a bar's γ·A and a plate's γ·t as code — the argument
`GetDesignEnvelope` and `TryGetLoadDirection` already made for their own rules. It also makes executable,
for the first time, a rule that exists only as prose and as an inline argument inside a validator: a
region inheriting its plate's surface property and material wherever it leaves them null
(`Geometry/PlateRegion.cs:29–35`, used at `FemexModel.Validation.cs:317–322`).

**Total model self-weight is deliberately not built.** It needs the overlapping priority regions
resolved into non-overlapping areas, and there is no polygon-boolean code in the repo. That is a geometry
sub-project, and the per-element intensities are its input.

### 6. `schemaVersion` goes to `"1.2"`, and `ToJson()` restamps a version it has migrated

Renaming a key is the case the field exists for: a 1.2 file read by a 1.1-era consumer loses self-weight
entirely, and a 1.1 file read by 1.2 needs converting.
`Claude/FEMEX_BarLocalAxes_LoadDirection.md:280–283` left "a real version policy — what a consumer
should do with `1.2` versus `2.0`" open; this is the first change that has to answer it, and the
minimum honest answer is a **list of readable versions**, not an ordering rule invented for versions
that do not exist yet.

`ToJson()` today stamps only when the version is null, so a loaded 1.1 file would be re-written as a
`"1.1"`-stamped file containing `"density"` — a file that lies about itself. The stamp is widened to
replace any version this build has migrated, and to leave an unrecognised one alone: it was not
migrated, so it is not ours to restate.

---

## Data model

### New files

| File | What |
|---|---|
| `Gravity.cs` (repo root, `namespace griffel_femex`) | `Dx`, `Dy`, `Dz`, `Acceleration` |
| `FemexModel.SelfWeight.cs` | the migration, the extracted inheritance rule, and the helpers below |
| `griffel-femex.Tests/SelfWeightTests.cs` | the facts below |

`Gravity.cs` goes at the root beside `Units.cs`, following that precedent exactly: small, model-wide,
non-geometric metadata that is not an entity and has no id. `Dx`/`Dy`/`Dz` reuses the `DistributedLoad`
component idiom, so "a direction as three components" is spelled one way in the format.

```csharp
/// <summary>
/// Which way gravity acts in this model, and how strong it is. Stated once, on the
/// root, and read only by self-weight: no <see cref="Load"/> can name it, so the
/// one way to point a distributed load is still its own
/// CoordinateSystem × Direction × Projected.
///
/// Written on every model rather than omitted when it is the default, because a
/// file that does not say which way down is is the problem this block exists to fix.
/// </summary>
public class Gravity
{
    // Direction only. Its magnitude is discarded — GetGravityDirection normalizes
    // it — so putting 9.80665 here as well as in Acceleration is harmless, not a
    // gravity of 96.
    public double Dx { get; set; }
    public double Dy { get; set; }
    public double Dz { get; set; } = -1.0;

    /// <summary>
    /// In the model's own length units per second squared: 9.80665 for a metre
    /// model, <b>9806.65 for a millimetre one</b>. The default is metre-specific,
    /// and a millimetre model that accepts it is 1000x light.
    /// </summary>
    public double Acceleration { get; set; } = 9.80665;
}
```

### Modified

**`FemexModel.cs`**

- `public Gravity Gravity { get; set; } = new Gravity();` immediately after `Units` (`:48`), so it is the
  third key in the file. Non-nullable with an initializer, unlike `Units? Units`: `Units` is pure
  annotation that nothing in the library computes with, whereas `Gravity` is *consumed* — by the
  migration and by three helpers — and a nullable one would force every consumer to invent the default at
  the point of use.
- `CurrentSchemaVersion` → `"1.2"`, its XML doc gaining the 1.2 line.
- A private `ReadableSchemaVersions = { "1.1", CurrentSchemaVersion }`, documented as a matched list and
  not a comparison rule.
- `ToJson()` restamps per decision 6; its "one deliberate mutation" paragraph is **rewritten**, not
  quietly broken.

```csharp
public string ToJson()
{
    if (SchemaVersion is null || Array.IndexOf(ReadableSchemaVersions, SchemaVersion) >= 0)
        SchemaVersion = CurrentSchemaVersion;

    return JsonSerializer.Serialize(this, JsonOptions);
}
```

**`Materials/Material.cs`** — `UnitWeight` → `Density`; the file's `//` comments upgraded to XML docs
carrying γ = ρ·g and the unit rule; the convenience constructor's `unitWeight` parameter renamed. Plus
the legacy binding, which is **set-only**: System.Text.Json uses the setter on read and skips a
getter-less property entirely on write, so the key can never be re-emitted — a stronger guarantee than
`double?` plus `WhenWritingNull`, which only holds while nothing assigns to it.

```csharp
/// <summary>
/// Mass per unit volume (ρ). Weight density γ = ρ·g, with g from the model's
/// <see cref="FemexModel.Gravity"/>; <c>FemexModel.GetWeightDensity</c> is that
/// product, executable.
///
/// In mass units consistent with the model's force and length units, where
/// mass = force·time²/length — so with kN and m that is <b>tonnes</b>, and concrete
/// is 2.5, not 2500. Replaces the 1.1 field <c>unitWeight</c>, which was γ directly.
/// </summary>
public double Density { get; set; }

/// <summary>
/// The 1.1 spelling, γ, bound on read so nothing is silently lost. Deliberately
/// <b>getter-less</b>: System.Text.Json can never write it back, so a 1.2 file
/// cannot contain it. The migration divides it by the model's gravity acceleration
/// into <see cref="Density"/> and clears it.
/// </summary>
[JsonPropertyName("unitWeight")]
[EditorBrowsable(EditorBrowsableState.Never)]
public double UnitWeight { set => _legacyUnitWeight = value; }

internal bool TryTakeLegacyUnitWeight(out double unitWeight);
```

`[EditorBrowsable]` rather than `[Obsolete]`, because `Obsolete` produces CS0618 at use sites and the
repo's standing bar is 0 warnings.

> **Risk, called out because the compiler will not.**
> `new Material(1, "Concrete C30", 33e9, 0.2, 25.0, 30e6)` still compiles after the rename and now means
> 25 t/m³ — a silent factor-of-g break for any positional caller. There is exactly one such call site in
> this repo, `griffel-femex.Tests/SampleModels.cs:144`. The `schemaVersion` bump is the only signal
> available to callers outside it, and the constructor's XML doc must say so outright.

**`Loads/LoadCase.cs`** — `public double SelfWeightFactor { get; set; }`, no initializer needed since 0
is both the C# default and the right one. XML-documented with decision 4 and the "additional to, never a
substitute for" rule from decision 1.5. A constructor overload takes it; the file's one-line class doc is
upgraded to house style at the same time.

**`FemexModel.Validation.cs`** — the checks below; `ValidatePlates:317–322` rewired to the extracted
`GetEffectiveProperties`; `FormatNodeList` (`:1046`) renamed `FormatNumberList`, its body already being
number-generic, since it now has two callers.

**`Geometry/Plate.cs`, `Geometry/PlateRegion.cs`** — one sentence each pointing at the helper as the
executable form of the inheritance rule. No behaviour.

### Resulting JSON

```json
{
  "schemaVersion": "1.2",
  "units": { "length": "m", "force": "kN" },
  "gravity": { "dx": 0, "dy": 0, "dz": -1, "acceleration": 9.80665 },

  "materials": [
    { "id": 1, "name": "Concrete C30/37", "modulusOfElasticity": 33000000,
      "poissonsRatio": 0.2, "density": 2.5, "strength": 30000 }
  ],
  "loadCases": [
    { "number": 1, "label": "Dead - superimposed", "nature": "Dead", "selfWeightFactor": 0 },
    { "number": 6, "label": "Dead - self weight",  "nature": "Dead", "selfWeightFactor": 1 }
  ]
}
```

---

## `FemexModel.SelfWeight.cs`

A lookup-only partial, mirroring `FemexModel.LocalAxes.cs`: nothing here changes the model, and an
unresolvable reference is answered with `false` rather than an exception, because `Validate()` reports
each of those cases in its own words.

```csharp
Vector3d GetGravityDirection();                    // unit; Vector3d.Zero if degenerate
double   GetWeightDensity(int materialId);         // ρ·g; 0.0 for an unknown material

bool TryGetBarSelfWeightPerLength(int barId, out Vector3d forcePerLength);
bool TryGetPlateSelfWeightPerArea(int plateId, out Vector3d forcePerArea);
bool TryGetPlateSelfWeightPerArea(int plateId, int? regionId, out Vector3d forcePerArea);

IEnumerable<LoadCase> GetSelfWeightCases();        // SelfWeightFactor != 0, in list order
```

Two overloads rather than an optional `regionId`, since an optional parameter cannot precede an `out`
one. `null` means the base panel — what the plate contributes outside every region. Id-based throughout,
matching `FindGrid(int)` / `GetGridsForLevel(int)` and the decision argued in
`Claude/FEMEX_LoadCombinations.md`.

Reuses `Section.CalculateArea()` (`Geometry/Sections/Section.cs:19`) and
`SurfaceProperty.GetNominalThickness()` (`Geometry/Surfaces/SurfaceProperty.cs:32`).

### Why these return a `Vector3d` and not a scalar

1. **It is the whole lesson of the previous change.**
   `Claude/FEMEX_BarLocalAxes_LoadDirection.md:10–12` names *"a magnitude that says how much and never
   which way"* as the format's original sin. Shipping a new bare magnitude in the very next change would
   be the same bug, freshly made.
2. **For a plate a scalar would be wrong, not merely incomplete.** A plate's natural scalar axis is its
   normal, and self-weight is not along the normal. For the sample's wall, in the y = 0 plane, the normal
   is horizontal and the weight is vertical; a scalar plus an implied axis would report a wall's
   self-weight as a lateral pressure. `PlateSelfWeightPerArea_OnAWall_ActsDownward_NotAlongTheNormal`
   pins it.
3. **`Vector3d` exists for exactly this** and is never serialized, so no format surface is added.
4. **It composes.** `TryGetLoadDirection` returns a global unit vector; these return a global force
   vector in the same frame, and `‖forcePerArea‖ = γ·t` recovers the scalar for anyone who wants it.

### The rules these helpers state

- **Unfactored.** The result is the weight itself, at gravity 1.0. A load case applies its own
  `SelfWeightFactor`; multiplying inside the helper would make it answer a different question depending
  on which case was asking.
- **An `Opening` or a `LoadOnly` area weighs nothing** — `true` with `Vector3d.Zero`, which is a definite
  answer and not a refusal. Robot's cladding, ETABS' area type `None` and RFEM's `TYPE_LOAD_TRANSFER`
  all carry load without stiffness or mass, and a caller summing regions should not have to branch.
  `false` is reserved for genuinely unanswerable: an unknown id, or a governing surface property or
  material that does not resolve.
- **The inheritance rule, extracted.** A private
  `GetEffectiveProperties(Plate, PlateRegion?) → (Kind, SurfacePropertyId, MaterialId)` states
  `region.X ?? plate.X` once. `ValidatePlates` is rewired to call it, so validation and the self-weight
  helpers cannot drift apart — the same argument that made `TryGetNewellNormal` shared between the
  planarity check and the local-axis convention.
- **`MeshFace` is not consulted.** `Mesh/MeshFace.cs:4–11` says the back-links are authoritative and its
  property fields are a mesher cache that validation does not check for agreement. A mesh-face helper is
  out of scope for the same reason.
- **`GetWeightDensity` returns `0.0` for an unknown material** rather than being a `Try`, following
  `GetTotalFactor`'s "0.0 when the case is absent". See *Open decisions*.

---

## Migration from 1.1

The conversion needs the root's `Gravity.Acceleration`, so it cannot live on `Material`. It hangs off
**`IJsonOnDeserialized`** rather than a hook inside `FromJson`, so it runs from every deserialization
entry point and no consumer calling `JsonSerializer.Deserialize<FemexModel>` directly can skip it:

```csharp
public partial class FemexModel : IJsonOnDeserialized
{
    void IJsonOnDeserialized.OnDeserialized() => MigrateLegacyUnitWeight();
}
```

Explicit interface implementation keeps it off the public surface. For each material with a pending
legacy γ: set `Density = γ / Gravity.Acceleration` and record the id. A material that states **both**
`unitWeight` and `density` keeps its density — the two cannot both be right and the newer spelling wins
— and is recorded separately. A non-positive acceleration leaves the density at 0 rather than producing
an infinity; `ValidateGravity` reports it as an error in its own right.

The two records are **private fields**, which System.Text.Json never serializes — no `[JsonIgnore]`
needed and no key can leak. Two consequences, both correct:

- The record does not survive `ToJson() → FromJson()`. The re-emitted file *is* 1.2 and carries a
  density, so it must not warn again: the warning is a property of the read, not of the model.
- A model built in memory never migrates and never warns.

**The conversion preserves weight density exactly, in any unit system.** ρ = γ/g on load and γ = ρ·g on
read use the same `Gravity.Acceleration`, so a 1.1 file's γ comes back to floating-point equality
whatever units it was written in, and the migration never has to know what those units were. A millimetre
model converted with the metre default gets a physically odd ρ and a numerically correct γ — nothing
downstream is wrong, which is why this is a warning rather than an error.

`ValidateSchemaVersion` (`FemexModel.Validation.cs:584–597`) is reshaped around the readable-versions
list so `"1.1"` is *recognised and migrated* rather than *unrecognised*:

- `null` — the existing wrong-sign warning, unchanged.
- `"1.1"` — recognised: "written before self-weight existed, so no load case in it carries any, and each
  material's unit weight has been read as a density through the model's gravity. Re-saving it writes the
  current format."
- anything else — the existing unrecognised-version warning, unchanged.

---

## `Validate()` additions

Two methods, because a validator yields bare strings and `Validate()` picks the severity at the call
site, so one method cannot emit both.

| Method | Severity | Registered |
|---|---|---|
| `ValidateGravity` | Error | immediately after `ValidateDuplicateIds` (`:24`) — model-wide, and every check below reads it |
| `ValidateSelfWeight` | Warning | last in the trailing warning block, after `ValidateProjectedLoads` (`:44`) |

**Errors**

```
Gravity has dx, dy and dz all zero, which is no direction at all.
Gravity has a non-positive acceleration (0). Which way gravity acts is dx/dy/dz's job; the
acceleration is only how strong it is.
```

The repo "validates no numeric field anywhere" today, so this exception is argued rather than assumed:
the gravity block is *entirely* numeric — refuse to check its numbers and it has no checks at all — and a
zero acceleration does not produce a visibly wrong number, it **deletes a load**, which is §4.3's exact
failure mode. `Density` stays unchecked for sign and magnitude, and gets only the contextual warning W4.

**Warnings**

| # | When | Message |
|---|---|---|
| W1 | more than one case with a non-zero factor | `Load cases 1 and 6 both carry self-weight; the structure's own weight is applied once in each of them, and any combination naming more than one counts it twice.` |
| W2 | non-zero factor on a case whose nature is not `Dead` | `Load case 3 carries self-weight but its nature is Wind; the structure's own weight is a dead action.` |
| W3 | the model has elements and materials, and no case carries self-weight | `No load case carries self-weight: every selfWeightFactor is zero, so the structure's own weight is nowhere in this model and a receiving program will not add it.` |
| W4 | a material used by a bar or plate has density 0 while self-weight is active | `Material 2 has a density of zero, so every bar and plate made of it weighs nothing in the self-weight case.` |
| W5 | migration record | `Material 1 was written as a unit weight and has been read as a density of 2.5493 through the model's gravity (9.80665). Re-saving the model writes the density.` |
| W6 | both spellings present | `Material 1 carries both a unitWeight and a density; the density is used and the unit weight ignored.` |

W1 is the *double-counted* half of §4.3 made visible and W3 is the *silently dropped* half. W3 is scoped
so it cannot nag a model with nothing to weigh — only when the model has bars or plates **and** at least
one material with a non-zero density — and is **skipped when `SchemaVersion != CurrentSchemaVersion`,**
because the version warning already says "no load case in it carries any", and the repo's
never-double-report convention is established at `:117–118` and `:936–938`. W4 is gated on self-weight
being active, so a model that never uses density is not nagged about it.

W1 reuses `FormatNumberList`.

**Which fire on the fixtures, and what the fixtures must become**

| Check | `Example1.femex` | `SampleModels.Build()` |
|---|---|---|
| gravity errors | no — defaults are valid | no |
| W1 / W2 / W4 | no | no |
| **W3 no self-weight anywhere** | **fires unless a case carries it** | **fires unless case 1 carries it** |
| W5 / W6 / version | no — the file is 1.2 | no — never deserialized |

**W3 is the forcing function**, and that is the point: it is the check that would have caught the gap
`Claude/FEMEX_LoadCombinations.md` noted in passing and could not fix. Both
`ValidationTests.Example1_LoadsAndValidates` and the sample-model validity fact assert
`Assert.Empty(model.Validate())` — no errors *and* no warnings — so:

- `SampleModels.Build()` sets `Density = 2.5` on material 1 (`:144`) and `SelfWeightFactor = 1.0` on load
  case 1 (`:148`). That is the whole fixture change; the existing combinations already factor case 1.
- `Examples/Example1.femex` gains a case that carries self-weight, below.

---

## `Examples/Example1.femex`

**Regenerated, not hand-edited:** load the current file, mutate the object graph, save through
`ToJson()`, so the file is exactly what the serializer emits rather than hand-edited into approximately
that — the method the load-direction migration used.

> **Correction to a premise worth recording:** the example is *not* asserted byte-identical to `ToJson()`
> anywhere. It is exercised by exactly two facts, `ValidationTests.Example1_LoadsAndValidates:576` and
> `LoadDirectionTests.Example1_GravityLoadsResolveDownward:468`, neither of which compares the whole
> document. It demonstrably *is* serializer output — key order proves it — and regenerating keeps that
> true, but nothing goes red on key order, which lowers the risk of this step.

- `"schemaVersion": "1.2"` first; the `gravity` block third, after `units`.
- Both materials `"unitWeight": 25` → `"density": 2.5` (t/m³, force kN + length m implying tonnes).
  **Authored, not migrated:** 2.5 t/m³ is what an engineer writes for concrete, and the honest
  consequence is that γ becomes 2.5 × 9.80665 = 24.517 kN/m³, down from the 25 the file previously
  implied. Stating mass rather than weight makes that arithmetic visible, which is the change working as
  intended. The exact reversibility of the γ/g migration is demonstrated by a 1.1 test fixture instead,
  where it belongs.
- Every load case gains `"selfWeightFactor"`, and the file gains **load case 6, `"Dead - self weight"`,
  nature `Dead`, factor `1.0`**.
- Each of the eight combinations gains one term `{ "loadCaseNumber": 6, "factor": f }` with `f` copied
  from its case-1 term: 101→1.35, 102→1.2, 103→1.2, 104→0.9, 105→1.2, 106→1.2, 201→1.0, 202→1.0. "G"
  keeps meaning total dead load.

### Why a separate case and not a factor on case 1

1. **Every target program keeps self-weight in its own pattern.** ETABS'
   `LOADPATTERN "DEAD" TYPE "Dead" SELFWEIGHT 1` is per-pattern and RFEM's `self_weight` flag is
   per-load-case precisely so self-weight can be factored, staged or switched off independently of
   superimposed dead load. A worked reference that fuses them teaches the habit that makes staged
   construction inexpressible.
2. **Folding it into case 1 tells a lie.** Case 1's loads are a 1.5 kPa SDL and 6 kN/m cladding — those
   genuinely *are* superimposed. Relabelling it `"Dead"` and switching self-weight on would make the file
   assert that the 1.5 kPa includes the slab's own 6.13 kPa, and would silently change what all eight
   existing combinations mean without touching them. A silent meaning change is the thing `schemaVersion`
   exists to prevent.
3. **The example is the format's worked demonstration** — the same argument that justified *adding* two
   loads in the previous change, since "neither new concept is currently exercised by it".
4. **A separate case exercises the risk that actually matters.** Self-weight in a case no combination
   factors never reaches a result — the second half of §4.3's silent wrong answer. A separate case
   demonstrates the combination wiring; folding it in hides the question.
5. **It proves that a case with no loads is not an empty case.** Case 6 has zero entries in the `loads`
   array and contributes the largest load in the model. That is a genuinely surprising property of the
   new field, and the example should show it.

### Hard counts that move

| Location | Assertion | Change |
|---|---|---|
| `ValidationTests.cs:585` | `Assert.Empty(model.Validate())` | text unchanged — **holds only once case 6 exists**; this is the gate |
| `ValidationTests.cs:588–599` | 20 plates / 2 surface properties / 44 mesh faces / 10 area loads / 8 combinations / 6-member ULS envelope / SLS 201 / `GetTotalFactor(105, 1) == 1.2` | **all unchanged** |
| `ValidationTests.cs` | — | **add** `LoadCases.Count == 6`, `GetTotalFactor(105, 6) == 1.2`, and a density assertion |
| `LoadDirectionTests.cs:481` | `Assert.Equal(64, gravity.Count)` over cases 1 and 2 | **unchanged** — self-weight is not a `Load`, so case 6 disturbs nothing in the load array. Worth an explicit comment saying so |
| `LoadDirectionTests.cs:131` | `Assert.StartsWith(… "schemaVersion": "1.1" …)` | **→ `"1.2"`** |
| `LoadDirectionTests.cs:147` | `ToJson` keeps `"0.9"` | passes unchanged — `"0.9"` is unrecognised — but **rename** it `ToJson_KeepsAnUnrecognisedVersion`, since it now guards a narrower rule |

Nothing anywhere asserts on `Material` fields today, so the rename breaks exactly one test-project line
(`SampleModels.cs:144`).

---

## Tests

New `griffel-femex.Tests/SelfWeightTests.cs`, roughly 26 facts.

**Defaults and round trip**
`NewModel_HasGravityDownAtStandardAcceleration` · `NewLoadCase_CarriesNoSelfWeight` ·
`Gravity_RoundTrips` · `Gravity_IsWrittenOnEveryModel` · `SelfWeightFactor_RoundTrips` ·
`SelfWeight_JsonHasNoUnitWeightKey`

**Migration**
`LegacyUnitWeight_IsReadAsADensity` · `LegacyUnitWeight_IsNeverWrittenBack` ·
`LegacyUnitWeight_SurvivesAsTheSameWeightDensity` · `LegacyUnitWeight_UsesTheModelsOwnGravity` (with
`"gravity"` placed **after** `"materials"` in the literal JSON, proving the hook does not depend on key
order) · `LegacyUnitWeight_IsIgnoredWhenADensityIsAlsoPresent` ·
`ToJson_UpgradesALegacySchemaVersionStamp` · `ToJson_KeepsAnUnrecognisedVersion`

**Helpers**
`GetGravityDirection_IsStraightDownByDefault` · `GetGravityDirection_IsNormalized` ·
`GetWeightDensity_IsDensityTimesAcceleration` · `GetWeightDensity_IsZeroForAnUnknownMaterial` ·
`BarSelfWeightPerLength_IsWeightDensityTimesArea` · `BarSelfWeightPerLength_PointsAlongGravity` ·
`BarSelfWeightPerLength_FollowsATiltedGravityVector` ·
`BarSelfWeightPerLength_IsNotFound_ForAnUnknownBar` ·
`PlateSelfWeightPerArea_IsWeightDensityTimesThickness` ·
`PlateSelfWeightPerArea_Region_UsesItsOwnThickness` (the drop panel, 0.45 not 0.25) ·
`PlateSelfWeightPerArea_Region_InheritsThePlatesMaterial` ·
`PlateSelfWeightPerArea_IsZeroForAnOpening` (the stair void) ·
`PlateSelfWeightPerArea_IsZeroForALoadOnlyPanel` ·
`PlateSelfWeightPerArea_IsNotFound_ForAnUnknownRegion` ·
`PlateSelfWeightPerArea_OnAWall_ActsDownward_NotAlongTheNormal` ·
`SelfWeight_IsUnfactored_ByTheCasesFactor` · `GetSelfWeightCases_ReturnsOnlyCasesWithANonZeroFactor`

**`ValidationTests.cs`**, a `// ----- Self weight -----` group
`Reports_GravityWithNoDirection` · `Reports_ZeroGravityAcceleration` ·
`Reports_NegativeGravityAcceleration` · `Warns_MoreThanOneLoadCaseCarryingSelfWeight` ·
`MoreThanOneSelfWeightCase_IsAWarning_NotAnError` · `Warns_SelfWeightOnANonDeadCase` ·
`Warns_ModelWithNoSelfWeightAnywhere` · `Warns_ZeroDensityMaterialUnderSelfWeight` ·
`Warns_MaterialConvertedFromAUnitWeight` · `Warns_OlderSchemaVersion` ·
`Accepts_ZeroDensityWhenNothingCarriesSelfWeight` · `Accepts_ANegativeSelfWeightFactor` ·
`Accepts_ASelfWeightCaseWithNoLoadsOfItsOwn`

**The permanent end-to-end assertion**, following the precedent that verification step 5 becomes a fact
rather than a one-off: `Example1_SelfWeightResolves` — `GetSelfWeightCases()` returns case 6 alone, a
named column's self-weight per length equals ρ·g·A, and it resolves to `(0, 0, −1)` × that magnitude.
This is the fact that says the migration was right rather than merely self-consistent.

---

## Ordered tasks

| # | Task | Risk |
|---|---|---|
| 1 | `Gravity.cs` at the root beside `Units.cs`; XML docs written before the bodies, carrying the acceleration-unit rule | Medium — the unit rule is the contract; cheap now, expensive later |
| 2 | `Material.Density` replacing `UnitWeight`; the set-only legacy binder and `TryTakeLegacyUnitWeight`; constructor parameter renamed | **High — a silent source break** (R2) |
| 3 | `LoadCase.SelfWeightFactor` + constructor overload + XML docs | Low |
| 4 | `FemexModel.cs` — `Gravity` after `Units`; `CurrentSchemaVersion` → `"1.2"`; `ReadableSchemaVersions`; `ToJson()` restamping, with its "one deliberate mutation" paragraph rewritten | Medium — `ToJson()`'s contract is documented in three places |
| 5 | `FemexModel.SelfWeight.cs` — `IJsonOnDeserialized`, the migration, the two private records, `GetEffectiveProperties`, the helpers | Medium — R1 lives here |
| 6 | `FemexModel.Validation.cs` — `ValidateGravity`, `ValidateSelfWeight`, `ValidateSchemaVersion` reshaped, `ValidatePlates` rewired, `FormatNodeList` → `FormatNumberList` | Medium — must not fire on either fixture |
| 7 | `SampleModels.cs` — density 2.5, self-weight on case 1. **Same commit as 6**, or every `Assert.Empty(Validate())` goes red between them | Low, ordering-critical |
| 8 | Regenerate `Examples/Example1.femex` through `ToJson()`; case 6 and the eight terms; densities set to 2.5 | Medium |
| 9 | `SelfWeightTests.cs`; the `ValidationTests` group and counts; the three `LoadDirectionTests` edits | Low |
| 10 | `Claude/FEMEX_SelfWeight_Summary.md`; `> **Extended by …**` blockquotes into `Claude/FEMEX.md` (root ~line 46, Loads ~line 138) and `Claude/FEMEX_Interop_Review.md` §4.3 | — |

**Risks, collected**

- **R1** — the set-only-property serialization behaviour is an asserted System.Text.Json contract, not
  one that has been run here. Pinned by `LegacyUnitWeight_IsNeverWrittenBack`; the fallback is
  `public double? UnitWeight` suppressed by the existing `WhenWritingNull`, with the migration nulling
  it. Same JSON, weaker guarantee.
- **R2** — `Material(…, double density, …)` is a silent factor-of-g break for any external positional
  caller. Unavoidable; the version bump is the only signal.
- **R3** — W3 is *designed* to fire on both fixtures. Tasks 6, 7 and 8 must land together.
- **R4** — the migration reads `Gravity.Acceleration`, which appears before `materials` in emitted order;
  `IJsonOnDeserialized` removes the dependence on key order entirely, and one test proves it by putting
  `"gravity"` last.
- **R5** — the default 9.80665 is metre-specific, so a millimetre model that accepts it is 1000× light.
  Documented, not validated. The highest-consequence residual in the change.

---

## Considered and rejected

- **A `Units.Mass` field.** Rejected on three grounds. `Units` is free text by design, so a file could
  say `force: "kN"`, `length: "m"`, `mass: "kg"` — inconsistent, since kN·s²/m is a tonne — and FEMEX
  would have no way to say so without a unit table that knows "kN" from "kip"; that is a unit *system*,
  review §5.9's own ranked item, not a field. The mass unit is in any case **implied**: mass =
  force·time²/length, so declaring force and length already determines it. And no mass unit ever
  surfaces — `Density` is the format's only mass-dimensioned quantity and it is read through one door,
  `GetWeightDensity`, which returns force per volume. When mass does need a reader (a mass source for
  dynamics), `Units.Mass` arrives with it. The trap is paid for in documentation instead, on
  `Material.Density` and `Gravity.Acceleration`.
- **A warning for a non-unit gravity direction.** The format already accepts an unnormalized `dx/dy/dz`
  on every distributed load and normalizes it silently, with no validator complaining; making gravity
  stricter would be an inconsistency rather than a thoroughness. Normalization also makes the obvious
  trap harmless: an author who writes `dz: −9.80665` *and* `acceleration: 9.80665` gets 9.80665, not
  96.2.
- **RFEM's per-case `fx/fy/fz` self-weight factors.** They would give the format two places that say
  which way gravity points — the objection decision 1 survives only by not doing this — and their one
  extra capability, horizontal pseudo-static gravity, is load-to-mass conversion, which review §6 puts
  out of scope.
- **Upgrading the schema stamp inside the migration** rather than in `ToJson()`. It would destroy the one
  fact `ValidateSchemaVersion` exists to report: a 1.1 file with an empty `materials` block would become
  indistinguishable from a 1.2 one.

## Deliberately out of scope

Named so this is not read as a to-do list.

- **Total model self-weight** — needs the overlapping priority regions resolved into non-overlapping
  areas, and there is no polygon-boolean code in the repo. The per-element intensities are its input.
- **Mesh-face self-weight** — `MeshFace`'s property fields are a mesher cache, not authority.
- **Mass source and load-to-mass conversion** — review §6 puts these outside FEMEX entirely.
- **Material completeness** (α, type enum, grade string) — review §5.5, its own ranked item 7.
- **Units as enums**, and temperature, angle and mass units — review §5.9, ranked item 8.
- **Producer, project and timestamp metadata**, and `UnmappedMemberHandling` — review §4.5, still
  deferred, as it was by the previous change.
- **Per-element mass overrides** (ETABS' additional mass), non-structural mass, and self-weight scoped to
  a subset of elements.

## Open decisions

Answered here by default rather than by argument, and worth revisiting.

- **`GetWeightDensity` returns `0.0` for an unknown material** rather than being a `Try`. It follows
  `GetTotalFactor`'s precedent, and the dangling reference is already an error at
  `FemexModel.Validation.cs:284`/`:340` — but it does conflate "weighs nothing" with "cannot tell",
  exactly the cost `Claude/FEMEX_LoadCombinations.md` named when it made the same call.
- **`Opening` and `LoadOnly` answer `true` with a zero vector** rather than `false`. Zero is the correct
  physical answer and saves every caller a branch; the case for `false` is that an opening generates no
  elements at all, so there is arguably nothing to report.
- **`SelfWeightFactor` is not sanity-checked** — NaN, infinity and absurd values all pass, matching
  `LoadCombinationTerm.Factor`, which the repo already declined to validate for consistency's sake. A
  negative factor is legal and meaningful (uplift), and has a fact saying so.

## Verification

1. `dotnet build` — 0 warnings, 0 errors, the repo's standing bar.
2. `dotnet test` — the 136 existing facts pass, three literal edits aside, plus the new ones.
3. `Assert.Empty(SampleModels.Build().Validate())` and
   `Assert.Empty(FemexModel.Load("Examples/Example1.femex").Validate())` — no errors **and** no warnings,
   W3 included. The fixtures proving the new check, as the previous change's gate did.
4. **The reversibility check, as an assertion rather than by hand:** load a pre-migration 1.1 fixture
   whose materials carry `"unitWeight": 25`, and assert `GetWeightDensity(1) == 25.0` to 1e-12. This is
   the result that says the migration preserved the physics rather than merely being self-consistent —
   the counterpart of the previous change's sign check.
5. **The arithmetic check, by hand once and then as a fact:** a 300×500 concrete column at ρ = 2.5 t/m³
   and g = 9.80665 m/s² weighs 0.15 m² × 24.517 kN/m³ = **3.68 kN/m**, acting `(0, 0, −1)`. Confirm
   `TryGetBarSelfWeightPerLength` agrees.
6. Round-trip the migrated example: `Load → ToJson → Load`, confirm `ToJson()` is idempotent, and eyeball
   that `"schemaVersion": "1.2"` is first, `"gravity"` third, `"selfWeightFactor"` beside `"nature"`, and
   `"unitWeight"` nowhere in the output.
