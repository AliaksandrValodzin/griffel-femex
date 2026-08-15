# Bar local axes, and load direction — coordinate system, axis and true length vs projection

> **Step 0 (repo convention):** this document sits alongside `Claude/FEMEX.md`, `Claude/FEMEX_Plates.md`,
> `Claude/FEMEX_Node_Sharing.md`, `Claude/FEMEX_Gridlines.md` and `Claude/FEMEX_LoadCombinations.md`;
> `Claude/FEMEX_BarLocalAxes_LoadDirection_Summary.md` records what was actually built.

## Context

`Claude/FEMEX_Assessment.md` §4 item 2 and `Claude/FEMEX_Interop_Review.md` §2.2 rows 2–3 name the same
P0 gap: **FEMEX's distributed loads are bare scalars.** `AreaLoad.Magnitude` and
`LinearLoad.MagnitudeStart/End` say how much and never which way. `PointLoad` already has `Fx…Mz`, which
makes the inconsistency sharper — a point load knows where it points and an area load does not.

Every one of the five target programs requires three orthogonal facts, and none of them can guess:

1. **Direction** — a global axis, a local axis, or a vector.
2. **Coordinate system** — global or element-local. On a wall, "down" and "normal to the surface" are
   entirely different loads.
3. **True length vs projected** — per real area or per plan-projected area. Snow versus dead load on a
   pitched roof.

A receiver reading `"magnitude": 1.5` on a wall panel cannot tell self-weight of finishes from wind
pressure. Review §4.2 names SAF's three-column factoring (`Coordinate system` × `Direction` ×
`Location`) as "the cleanest and the model to copy"; this change copies it.

The change is *not* purely additive, and that is the interesting part: with a direction, a magnitude's
sign starts to mean something, and every load already written means something different. Hence the
version field, below.

### Does FEMEX have element local axes?

Investigated before planning, because the answer decides how much of this change is even possible. It
is **partly, and it is written down nowhere**:

| | Status |
|---|---|
| **Plate** | **Yes, fully derivable.** `Plate.LocalAxisAngle` is "rotation of the local X-axis about the plate normal; unrotated local X runs from the first contour node to the second". The normal follows from the contour winding, and `FemexModel.Validation.cs::TryGetPlanarityDeviation` already computes it with Newell's method. Nothing exposes it, and the sign convention is not stated. |
| **Bar** | **No.** `Bar.RotationAngle` exists, but its comment — *"Rotation of local X-axis relative to global X-axis"* — does not define a usable system: local x is fixed by the two nodes, so the angle can only be a **roll about local x**, and there is no rule anywhere for the default orientation of local y/z, nor for the vertical-member special case where the usual construction degenerates. |
| **`LinearLoad`** | **No host at all.** It targets `StartNode`/`EndNode` — two node numbers, not a bar — so there is no roll angle to resolve a local direction against. |
| **Free-polygon `AreaLoad`** | Derivable from `NodeSequence` by the same rule as a plate, with angle 0. |
| **`Support`** | None. `Restraint` is implicitly global — review §5.6, P1, out of scope here. |

So writing the bar and plate local-axis convention down is **a prerequisite of this change**, not a side
quest, which is why it is half the title. It also unblocks review §5.8 (`TemperatureLoad.GradientPerDepth`
has no axis) for a later pass.

## The five decisions

1. **SAF's three fields, not RFEM's fused enum.** `CoordinateSystem` × `Direction` × `Projected`, as
   three independent things. RFEM bakes projection into the direction enum
   (`LOAD_DIRECTION_GLOBAL_Z_OR_USER_DEFINED_W_TRUE` vs `..._PROJECTED`) and ETABS into a 1–11 integer;
   both are the same information with the factors multiplied out, and both are harder to read and to
   validate. Review §4.2 already reached this conclusion.
2. **The sign lives in the magnitude, and global +Z is up.** Direction defaults to global `Z`, so a
   gravity load is a **negative** magnitude. There is deliberately no `Gravity` convenience value
   (ETABS' `Dir` 10) — it would give the format two ways to say "down" and make the sign of a magnitude
   depend on which one the author picked. The cost is real and is paid in one place: every distributed
   load in `Examples/Example1.femex` is re-signed, and any file written before this change means the
   opposite of what it now says.
3. **`schemaVersion` ships in this change.** Review ranked item #1, and §4.5 states plainly that it is
   the prerequisite for this one: "adding direction changes the meaning of existing files, and without a
   version field there is no safe way to tell an old file from a new one." Minimal — one string plus a
   validation warning. Producer, project and timestamp metadata stay deferred, and so does
   `UnmappedMemberHandling`: the failure mode *that* guards is an old file read by a new consumer, while
   the one created here is the reverse, and a version field is what addresses it.
4. **Bar local axes follow the ETABS/SAP convention.** ETABS is FEMEX's closest architectural relative
   (review §1.3) and the convention is the most widely recognised of the candidates. Local 2 in the
   vertical plane pointing up, local 3 horizontal, with the documented substitution for vertical members.
5. **`LinearLoad` gains an optional `BarId`.** A local direction on a line load is meaningless without a
   roll angle, and the two-node form has none. The alternative — deriving axes from the load's own line
   with no roll — was rejected because "local" would then quietly mean something different for loads than
   for bars: a load on a beam with `RotationAngle = 30°` could not follow the beam it sits on.

---

## The local-axis convention

Stated once, in XML docs on the types and here, and made **executable** by the helpers below so that a
consumer and the format cannot disagree about it — the argument `GetDesignEnvelope` and
`GetGridsForLevel` already made for their own rules.

### Global

Right-handed, **Z up**: `Level.AbsoluteElevation` is global Z and increases upward. Worth stating
explicitly, because RFEM's global Z points *down* by default (review §1.5), and that is the single most
common source of translator bugs against a Z-up format.

### Bar (`Geometry/Bar.cs`)

With `RotationAngle = 0`:

- local **x** = unit vector from `StartNodeId` to `EndNodeId`.
- **Non-vertical bar:** local **y** = `normalize(Ẑ − (Ẑ·x̂) x̂)` — the vertical direction with the axial
  part removed, so it lies in the vertical plane through the member and points upward. Local **z** =
  `x̂ × ŷ`, which is horizontal.
- **Vertical bar** (`|x̂·Ẑ| > 1 − tol`, where the construction above degenerates): local **y** = global
  **+X**, local **z** = global **+Y**. Right-handed for a column pointing up, and the ETABS convention.
- `RotationAngle` is a **right-hand rotation of local y and z about local x**, in degrees. The existing
  comment ("relative to global X-axis") is replaced.

### Plate (`Geometry/Plate.cs`)

- local **z** = the contour normal, by Newell's method over `NodeIds` in order, right-hand rule: a
  contour that is counter-clockwise seen from above has normal +Z.
- local **x** = the `NodeIds[0] → NodeIds[1]` chord projected into the plane, then rotated by
  `LocalAxisAngle` about local z, counter-clockwise seen from **+z** — the same sign convention
  `Grid.RotationAngle` already documents ("counter-clockwise about global +Z").
- local **y** = `ẑ × x̂`.
- A free-polygon `AreaLoad` (`NodeSequence`) uses the same rule with `LocalAxisAngle` = 0.

All 136 `rotationAngle` and all 20 `localAxisAngle` values in `Examples/Example1.femex` are 0, so
stating the convention changes nothing that already exists.

---

## Data model

### New files

| File | What |
|---|---|
| `Loads/LoadCoordinateSystem.cs` | `Global \| Local`. Flat in `Loads/`, beside `LoadNature.cs` — the repo puts an enum next to its owner and reserves subfolders for a family of types, as `Loads/Combinations/` earned. |
| `Loads/LoadDirection.cs` | `X \| Y \| Z \| Vector`. |
| `Loads/DistributedLoad.cs` | **abstract `DistributedLoad : Load`** — the shared orientation, defined once rather than duplicated on both load types. |
| `Geometry/Vector3d.cs` | `readonly record struct Vector3d(double X, double Y, double Z)` with `Length`, `Normalized()`, `Dot`, `Cross`. Never serialized; it exists so the helpers have a return type instead of nine `out double`s. |
| `FemexModel.LocalAxes.cs` | The helpers below. Mirrors `FemexModel.Nodes.cs` and `FemexModel.LoadCombinations.cs`. |

`DistributedLoad` carries:

```csharp
public LoadCoordinateSystem CoordinateSystem { get; set; } = LoadCoordinateSystem.Global;

// Property initializer, not an enum member ordering trick: System.Text.Json leaves a
// property untouched when its key is absent, so this is what a legacy file reads back as.
public LoadDirection Direction { get; set; } = LoadDirection.Z;

// Direction == Vector only; omitted from the JSON otherwise (DefaultIgnoreCondition).
public double? Dx { get; set; }
public double? Dy { get; set; }
public double? Dz { get; set; }

public bool Projected { get; set; }
```

Its XML doc states the two rules a consumer would otherwise get wrong:

1. The **force** acts along the resolved axis; the **moment** (`LinearLoad.MomentStart/End`) acts
   *about that same axis*, right-hand rule. One direction serves both, so a torsional line load is
   `Direction = X`, `CoordinateSystem = Local` with the moment terms set.
2. **`Projected`** means the magnitude is per unit of the loaded geometry **projected onto the plane
   perpendicular to the load direction** — so total force is `magnitude × projected extent`, not
   `magnitude × real extent`. This is the snow-versus-dead distinction, and the two agree only when the
   load is perpendicular to the surface.

### Modified

- **`Loads/AreaLoad.cs`**, **`Loads/LinearLoad.cs`** — reparented to `DistributedLoad`; nothing else
  about them changes. The `[JsonDerivedType]` registrations stay on `Load` exactly as they are: STJ
  matches the runtime type against the list declared on the base, and an intermediate abstract class is
  transparent to it. A round-trip fact pins this rather than assuming it.
- **`Loads/LinearLoad.cs`** — gains `int? BarId`, referencing `Element.Id` of a `Bar`. Optional, and it
  is the *host* whose local axes a local direction resolves against. `StartNode`/`EndNode` keep their
  existing job as the load's extent, so a part-length load along a bar stays expressible and no existing
  file changes shape.
- **`Geometry/Bar.cs`**, **`Geometry/Plate.cs`** — the axis conventions above, as comments. No behaviour.
- **`FemexModel.cs`** — `public string? SchemaVersion { get; set; }` declared **first**, so it is the
  first key in the file, as ETABS' `PROGRAM`/`VERSION` line is; plus
  `public const string CurrentSchemaVersion = "1.1"`, 1.0 being the unstamped format that had no load
  direction. **No property initializer** — a legacy file must read back as `null` to be detectable — and
  `ToJson()` stamps `SchemaVersion ??= CurrentSchemaVersion` before serializing, documented as the one
  deliberate mutation, so that every file FEMEX writes is stamped and every file it reads keeps what it
  had.
- **`FemexModel.Validation.cs`** — the checks below, and `TryGetPlanarityDeviation` refactored to call
  the extracted Newell normal, so the planarity check and `TryGetPlateLocalAxes` cannot drift apart.

### Helpers (`FemexModel.LocalAxes.cs`)

```csharp
bool TryGetBarLocalAxes(int barId,     out Vector3d x, out Vector3d y, out Vector3d z);
bool TryGetPlateLocalAxes(int plateId, out Vector3d x, out Vector3d y, out Vector3d z);
bool TryGetLoadDirection(Load load,    out Vector3d direction);   // unit vector, global coordinates
```

`TryGetLoadDirection` is the one call a consumer actually needs: it resolves `Global` / `Local` /
`Vector` for any `DistributedLoad` into a global unit vector, and returns false when the host or the
geometry does not resolve — which `Validate()` reports separately, so the helper stays silent rather
than throwing. Both axis helpers reuse the existing private `TryGetAbsolutePoint` (same partial class),
and the vertical-bar threshold is derived from `GetCoincidenceTolerance()` so it scales with the model
like every other tolerance in the format.

---

## Validation

New messages in `ValidateLoads`, worded in the existing style.

**Errors**

- `CoordinateSystem = Local` on a `LinearLoad` with no `BarId` — there is nothing to resolve against.
- `BarId` naming an unknown element, or naming a plate or mesh face rather than a bar.
- `Direction = Vector` with any of `Dx`/`Dy`/`Dz` null, or with all three zero.
- `Direction ≠ Vector` with any of `Dx`/`Dy`/`Dz` set — a contradiction, not a harmless extra.
- `Projected` together with `CoordinateSystem = Local`. None of the five programs has a projected local
  variant; RFEM says so explicitly (review §1.5), and the concept is not meaningful — a local direction
  is already defined relative to the surface being projected.

**Warnings**

- `Projected` where the resolved direction is parallel to the loaded line, or lies in the plate's plane:
  the projected extent is zero, so the load means nothing. Skipped when geometry does not resolve.
- `SchemaVersion` is null — "written before load directions existed; its magnitudes are read as global
  +Z, so gravity loads in it have the wrong sign." An unrecognised version gets a separate warning.

---

## `Examples/Example1.femex`

- `"schemaVersion": "1.1"` as the first key.
- 8 area loads re-signed: SDL `1.5 → −1.5` (×4), Live `3 → −3` (×3) and `1 → −1`.
- 56 linear cladding loads re-signed `6 → −6`.
- Every distributed load gains explicit `"coordinateSystem"`, `"direction"` and `"projected"` — enums
  and bools are never null, so they serialize whatever their value, which is what an example file wants.
- The 16 point loads and the temperature load are untouched; `PointLoad` already carries its direction.
- **Two loads added**, because the example is the format's worked demonstration and neither new concept
  is currently exercised by it: a wind pressure on a wall panel as `Local` + `Z` (normal to the wall,
  load case 3), and one roof area load with `"projected": true`.

## Tests

New `griffel-femex.Tests/LoadDirectionTests.cs`, roughly 12 facts, with `SampleModels.Build()` gaining a
directed area load, a local linear load with a `BarId`, and a vector load:

- **Defaults** — a new `AreaLoad` is global, `Z`, not projected.
- **The legacy path** — JSON with no direction keys deserializes to global `Z`, `projected: false`.
- **Round trip** — all five keys survive; `dx`/`dy`/`dz` are omitted when the direction is not `Vector`;
  `schemaVersion` is present and first.
- **Each of the five errors and both warnings.**
- **`TryGetBarLocalAxes`** — horizontal beam (y upward in the vertical plane, z horizontal), vertical
  column (y = +X, z = +Y), and `RotationAngle = 90` rolling y onto z.
- **`TryGetPlateLocalAxes`** — horizontal slab (normal +Z for a counter-clockwise contour), the sample's
  wall in the y = 0 plane, and `LocalAxisAngle = 15` (the value `SampleModels` already sets).
- **`TryGetLoadDirection`** — a `Local` + `Z` area load on a wall resolves to the wall normal.

Existing files touched: `RoundTripTests` (the `"localAxisAngle"` key assertion gains company, plus
`"schemaVersion"`), `ValidationTests` (beside the `Reports_AreaLoad*` facts), and the example-loading
fact that asserts 8 area loads if the example gains one.

## Verification

1. `dotnet build` — 0 warnings, 0 errors, the repo's standing bar.
2. `dotnet test` — the 103 existing facts still pass, plus the new ones.
3. `FemexModel.Load("Examples/Example1.femex")` then `Validate()` returns no errors **and no warnings**
   — including no missing-version warning, which is the new file proving the new check.
4. Round-trip the migrated example: load → `ToJson()` → load → compare; eyeball that `"schemaVersion"`
   is the first key and `"direction"` reads next to `"magnitude"`.
5. **The sign check, by hand:** the migrated dead-load area loads resolve through `TryGetLoadDirection`
   to `(0, 0, −1)` × |magnitude|. Same physical load as before the change, now stated rather than
   assumed. This is the one result that says the migration was right rather than merely consistent.

## Deliberately out of scope

Named so this is not read as a to-do list. **Self-weight** (review §4.3) — the other half of "where does
gravity come from", and its own change. **Support local axes** (§5.6) and **bar end offsets** (§5.2).
**`TemperatureLoad.GradientPerDepth`'s missing axis** (§5.8) — unblocked by the convention written down
here, but a separate edit. **`PointLoad` gaining a coordinate system** — its `Fx…Mz` are unambiguous
today and nothing is lost by leaving them global. **Producer, project and timestamp metadata** beyond
`schemaVersion`, and **`UnmappedMemberHandling`** — both belong to the metadata change that
`schemaVersion` is the down payment on.

## Still open

- **Whether `Vector` should be allowed with `CoordinateSystem = Local`.** It falls out of the SAF
  factoring for free and the helpers resolve it correctly, but no target program has a local vector
  load, so nothing would produce or consume one. Allowed by the model, untested, and worth removing if
  it never finds a reader.
- **Whether the vertical-bar threshold should be a tolerance or an angle.** Derived from
  `GetCoincidenceTolerance()` here for consistency with the rest of the format, but the quantity being
  compared is a direction cosine, not a distance — a fixed small angle (say 0.1°) may be the more honest
  formulation.
- **`schemaVersion` is a string with no comparison rule.** `"1.1"` is compared for equality, and
  anything else warns. A real version policy — what a consumer should do with `"1.2"` versus `"2.0"` —
  belongs with the metadata change and is not invented here.
- **Nothing is validated against a real export.** The same caveat review §7.3 closes with: the sign and
  projection conventions are taken from vendor documentation, and one real ETABS or RFEM export should
  be round-tripped before they are treated as settled.
